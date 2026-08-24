/*
 * DragonScreen - AutoSequence
 *
 * GLUE. The mission conductor: ONE button that flies the whole Crew-2 mission by chaining the phase
 * controllers that already exist, each of which already flies itself once engaged.
 *
 * ---- WHAT IT IS, AND WHAT IT IS NOT ----
 * It is a SUPERVISOR, not a new autopilot. It reads the vessel into SeqInputs, asks the pure state
 * machine (pure/AutoSequenceCore) which step we are in, and - only on a transition - engages the next
 * controller (AutoPilot, StationApproach, DeorbitOps). FlightDriver keeps ticking those controllers as
 * always. All the flying stays where it already works; this only decides WHEN each phase starts and when
 * the sequence is finished. pure/MissionPhase's note - "the flight sequencer ... does not exist yet" - is
 * what this closes.
 *
 * ---- ONE BUTTON, TWO LEGS (user 2026-08-24) ----
 * Press once on the pad: launch -> orbit -> rendezvous -> dock -> REFUEL the capsule, then it switches
 * ITSELF off so the crew can transfer. Press UNDOCK to leave the station. Press AUTO SEQUENCE again:
 * deorbit -> entry -> parachute splashdown. The same button means "out" then "home" because ReturnArmed
 * latches when the outbound leg finishes. The manual path (touchscreen RENDEZVOUS/AUTO-DOCK, the physical
 * STRING buttons) is untouched, and a second press of AUTO SEQUENCE cancels and hands straight back to it.
 *
 * ---- REFUEL IS MEASURED ON THE DRAGON'S OWN TANK ----
 * Docked, KSP merges both craft into one vessel, so an aggregate propellant total is the capsule PLUS the
 * station's farm and never reads full. The done-signal is Refuel.Full(v), which walks DockedSide.Ours(v)
 * and refuses to cross the docking joint - the capsule tank alone. A stall guard (no capsule-mono gain
 * for StallTimeoutS, e.g. the station has nothing to give) completes the step anyway so it cannot hang.
 */
using UnityEngine;

namespace DragonScreen
{
    public static class AutoSequence
    {
        private const string Tag = "[DragonScreen] ";

        public static bool Engaged { get; private set; }

        /// <summary>Latched when an outbound run finishes; selects the return leg on the next engage.</summary>
        public static bool ReturnArmed { get; private set; }

        private static SeqStep step = SeqStep.Idle;

        /// <summary>The current step's name for the AUTO SEQUENCE button. Empty when idle.</summary>
        public static string PhaseName { get { return Engaged ? AutoSequenceCore.StepName(step) : ""; } }

        // ---- bail debounce: a controller hand-off (rendezvous->dock, deorbit->entry) can show neither
        // engaged for a frame; only bail if the drop persists. ----
        private const double BailGraceS = 1.2;
        private static double bailSince = -1.0;

        // ---- refuel stall detection (station empty): watch the CAPSULE mono for progress. ----
        private const double StallTimeoutS = 60.0;
        private const double StallEpsilonUnits = 0.1;
        private static double lastMono = -1.0;
        private static double lastMonoGainAt = -1.0;

        /// <summary>The FLIGHT-page AUTO SEQUENCE button. Off -> engage and pick the leg; on -> cancel.</summary>
        public static void Toggle()
        {
            if (Engaged)
            {
                Disengage("crew");
                // Hand straight back to manual: stop whatever phase we had running.
                FlightCommands.CancelAllSequences();
                return;
            }
            Engage();
        }

        public static void Engage()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            SeqInputs s = Read(v);
            step = AutoSequenceCore.Begin(s);
            Engaged = true;
            bailSince = -1.0;
            EnterStep(v, step);
            Debug.Log(Tag + "AUTO SEQUENCE engaged - " + AutoSequenceCore.StepName(step)
                      + (ReturnArmed ? " (return leg)" : ""));
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
            step = SeqStep.Idle;
            bailSince = -1.0;
            Debug.Log(Tag + "AUTO SEQUENCE disengaged - " + why);
        }

        private static int lastTickFrame = -1;

        public static void Tick()
        {
            // A fresh flight clears the return latch (a capsule back on the pad has not flown out yet).
            Vessel v = FlightGlobals.ActiveVessel;
            if (v != null && v.situation == Vessel.Situations.PRELAUNCH && ReturnArmed)
                ReturnArmed = false;

            if (!Engaged) return;

            // Once per frame, not once per screen - three screens must not each advance the mission.
            if (Time.frameCount == lastTickFrame) return;
            lastTickFrame = Time.frameCount;

            if (v == null) return;

            SeqInputs s = Read(v);
            SeqResult r = AutoSequenceCore.Advance(step, s);

            if (r.Bail)
            {
                // Debounce a one-frame hand-off gap before actually handing back.
                double now = Time.realtimeSinceStartup;
                if (bailSince < 0.0) bailSince = now;
                if (now - bailSince < BailGraceS) return;
                Disengage("phase ended - handed back to manual");
                return;
            }
            bailSince = -1.0;

            if (r.Done)
            {
                if (r.ArmReturn) ReturnArmed = true;
                Disengage(r.ArmReturn ? "outbound complete - transfer crew, then UNDOCK"
                                      : "return complete - welcome home");
                return;
            }

            if (r.Step != step)
            {
                step = r.Step;
                EnterStep(v, step);
                Debug.Log(Tag + "AUTO SEQUENCE -> " + AutoSequenceCore.StepName(step));
            }
        }

        /// <summary>Start the controller a step needs. Idempotent - never re-engages a running one.</summary>
        private static void EnterStep(Vessel v, SeqStep st)
        {
            switch (st)
            {
                case SeqStep.Ascent:
                    if (!AutoPilot.Engaged) AutoPilot.Engage();
                    break;

                case SeqStep.Rendezvous:
                    // Insertion is done; hand attitude off the ascent controller before the rendezvous
                    // takes it, so the two never drive the axes at once.
                    if (AutoPilot.Engaged) AutoPilot.Disengage("auto-seq: insertion complete");
                    if (!StationApproach.Engaged && !DockingOps.Engaged) MissionOps.Rendezvous();
                    break;

                case SeqStep.Refuel:
                    // Nothing to engage - DockedRefuel already tops the tank every frame it is berthed.
                    break;

                case SeqStep.Deorbit:
                    if (!DeorbitOps.Engaged && !EntryOps.Engaged) DeorbitOps.Engage();
                    break;
            }
        }

        /// <summary>Read the vessel into the pure state machine's inputs.</summary>
        private static SeqInputs Read(Vessel v)
        {
            SeqInputs s = new SeqInputs();

            s.InStableOrbit = v.orbit != null && v.mainBody != null
                              && v.orbit.PeA >= v.mainBody.atmosphereDepth;
            s.S2Gone = !HasSecondStage(v);
            s.Docked = DockedSide.Docked(v);
            s.RefuelFull = Refuel.Full(v);
            s.Landed = v.situation == Vessel.Situations.LANDED
                       || v.situation == Vessel.Situations.SPLASHED;

            s.AscentEngaged = AutoPilot.Engaged;
            s.RendezvousEngaged = StationApproach.Engaged || DockingOps.Engaged;
            s.ReturnActive = DeorbitOps.Engaged || EntryOps.Engaged;

            s.RefuelStalled = UpdateStall(v, s.Docked, s.RefuelFull);
            s.ReturnArmed = ReturnArmed;
            return s;
        }

        private static bool HasSecondStage(Vessel v)
        {
            if (v.parts == null) return false;
            for (int i = 0; i < v.parts.Count; i++)
                if (VehicleParts.IsSecondStage(v.parts[i].name)) return true;
            return false;
        }

        /// <summary>
        /// True once the capsule mono has stopped rising for StallTimeoutS while docked but not full - the
        /// station has nothing left to give, so the refuel step should complete rather than wait forever.
        /// </summary>
        private static bool UpdateStall(Vessel v, bool docked, bool full)
        {
            double now = Time.realtimeSinceStartup;
            if (!docked || full)
            {
                lastMono = -1.0;
                lastMonoGainAt = -1.0;
                return false;
            }

            double mono = DockedSide.Mono(v);
            if (lastMono < 0.0 || mono > lastMono + StallEpsilonUnits)
            {
                lastMono = mono;
                lastMonoGainAt = now;
                return false;
            }
            if (mono > lastMono) lastMono = mono;   // track tiny gains without resetting the timer
            return (lastMonoGainAt > 0.0) && (now - lastMonoGainAt >= StallTimeoutS);
        }
    }
}
