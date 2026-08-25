/*
 * DragonScreen - CrewProcedureOps
 *
 * GLUE. The mission conductor, flown crew-in-the-loop: it walks the real Crew Dragon gate sequence
 * (pure/CrewGates) and, at each gate, HOLDS the mission until the crew clears it - then engages the phase
 * that gate opens. The autopilot flies the vehicle between gates; the user does what the real astronauts
 * do to authorise each step. This is the AUTO SEQUENCE button now.
 *
 * ---- WHY THIS REPLACED THE FLOW-THROUGH CONDUCTOR ----
 * The earlier AutoSequence chained ascent->rendezvous->dock->deorbit hands-off, on its own signals. That
 * is not how a crewed mission flies: the vehicle does not pass a real decision point - GO for launch, GO
 * to enter the keep-out sphere, GO for the deorbit burn - without the crew's GO, and the crew can HOLD or
 * ABORT at those points. So the conductor is now GATE-DRIVEN: the gate list IS the timeline, each gate
 * maps to the controller it engages, and an ungated phase (ascent, the phasing burns, entry) simply flies
 * between gates while the next gate waits on its precondition. See dragonscreen-crew-autopilot-direction.
 *
 * ---- ONE BUTTON, TWO LEGS ----
 * OUTBOUND: countdown gates -> GO FOR LAUNCH -> ascent -> GO for approach -> the L-approach holds (WP0/
 * WP1/WP2, each a crew GO) -> dock. Then it idles for the crew to transfer, ReturnArmed latched.
 * RETURN (a second press): GO FOR UNDOCK -> depart -> GO FOR DEORBIT -> deorbit/entry/splashdown.
 * The manual path (touchscreen RENDEZVOUS/AUTO-DOCK, the physical STRING buttons) is untouched.
 *
 * ---- THE HELM STAYS WITH THE TESTED CONTROLLERS ----
 * This engages AutoPilot (ascent), MissionOps.Rendezvous (StationApproach -> the L-approach), UndockOps
 * and DeorbitOps - each already flies itself once engaged and is ticked by FlightDriver. This decides only
 * WHEN each starts, and feeds the crew's GO into the waypoint holds. It invents no guidance.
 */
using UnityEngine;

namespace DragonScreen
{
    public static class CrewProcedureOps
    {
        private const string Tag = "[DragonScreen] ";

        public static bool Engaged { get; private set; }
        /// <summary>Latched when the outbound leg reaches its goal (docked, or in orbit for a free-flyer).</summary>
        public static bool ReturnArmed { get; private set; }

        // ---- the live gate machine ----
        private static Gate[] gates = new Gate[0];
        private static ProcState proc;
        private static bool onReturnLeg;

        /// <summary>Which waypoint hold the crew has GO'd, so WaypointApproachOps releases exactly that one.</summary>
        public static WpPhase ReleasedHold = WpPhase.Idle;

        /// <summary>The gate the crew is currently working - for the checklist UI. None when idle/flying.</summary>
        public static Gate CurrentGate() { return CrewProcedureCore.Current(gates, proc); }
        public static ProcState Proc { get { return proc; } }

        /// <summary>The AUTO SEQUENCE button label: the current gate's title, or the phase being flown.</summary>
        public static string PhaseName
        {
            get
            {
                if (!Engaged) return "";
                Gate g = CurrentGate();
                if (g.Id == GateId.None) return onReturnLeg ? "RETURN" : "OUTBOUND";
                // A gate the crew can act on reads as its title; one still waiting on the flight reads as
                // the phase being flown (ASCENT, RENDEZVOUS, ...).
                return CrewActionNeeded() ? g.Title : FlyingName(g);
            }
        }

        /// <summary>
        /// The crew has something to do on the current gate NOW - so the checklist card should take the
        /// screen. True when the gate is GO-ready or held on a NO-GO, or when every AUTO precondition is
        /// met and a crew item is still unchecked. It is FALSE while the autopilot is still flying to the
        /// point the gate authorises (an unmet Auto item), so ascent and phasing are not interrupted.
        /// </summary>
        public static bool CrewActionNeeded()
        {
            if (!Engaged) return false;
            Gate g = CurrentGate();
            if (g.Id == GateId.None) return false;
            if (proc.Phase == GatePhase.GoReady || proc.Phase == GatePhase.NoGo) return true;
            if (proc.Phase != GatePhase.Holding) return false;
            return AllAutoSatisfied(g) && AnyCrewUnchecked(g);
        }

        private static bool AllAutoSatisfied(Gate g)
        {
            if (g.Items == null || proc.Satisfied == null) return true;
            for (int i = 0; i < g.Items.Length; i++)
                if (g.Items[i].Kind == ItemKind.Auto && !proc.Satisfied[i]) return false;
            return true;
        }

        private static bool AnyCrewUnchecked(Gate g)
        {
            if (g.Items == null || proc.Satisfied == null) return false;
            for (int i = 0; i < g.Items.Length; i++)
                if (g.Items[i].Kind == ItemKind.CrewAck && !proc.Satisfied[i]) return true;
            return false;
        }

        // ---- crew inputs, from the UI ----
        /// <summary>The crew taps checklist item i of the current gate (a CrewAck toggles; taps only).</summary>
        public static void ToggleItem(int i)
        {
            if (!Engaged) return;
            Gate g = CurrentGate();
            if (g.Items == null || i < 0 || i >= g.Items.Length) return;
            if (g.Items[i].Kind != ItemKind.CrewAck) return;      // Auto items are the system's to set
            bool now = (proc.Satisfied != null && i < proc.Satisfied.Length) && proc.Satisfied[i];
            CrewProcedureCore.SetItem(g, ref proc, i, !now);

            // Arming the launch escape system is a real state change, not just a tick.
            if (g.Id == GateId.ArmLaunchEscape && !now) AbortResponder.Arm();
        }

        /// <summary>The crew presses GO. Clears the gate if ready, runs its action, advances the mission.</summary>
        public static void PressGo()
        {
            if (!Engaged) return;
            Gate g = CurrentGate();
            if (!CrewProcedureCore.Go(g, ref proc)) return;       // refused - not GO-ready
            ExecuteGateAction(g.Id);
            CrewProcedureCore.Advance(gates, ref proc);
            Debug.Log(Tag + "GATE CLEARED - " + g.Title);
        }

        /// <summary>The crew calls NO-GO: the mission holds at this gate.</summary>
        public static void PressNoGo()
        {
            if (!Engaged) return;
            CrewProcedureCore.NoGo(ref proc);
            Debug.Log(Tag + "NO-GO - holding at " + CurrentGate().Title);
        }

        /// <summary>The crew calls ABORT: latch it and hand to the phase-correct abort responder.</summary>
        public static void PressAbort()
        {
            if (!Engaged) { AbortResponder.Trigger("crew abort"); return; }
            CrewProcedureCore.Abort(ref proc);
            AbortResponder.Trigger("crew abort at " + CurrentGate().Title);
        }

        // ---- engage / disengage ----
        public static void Toggle()
        {
            if (Engaged) { Disengage("crew"); FlightCommands.CancelAllSequences(); return; }
            Engage();
        }

        public static void Engage()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            onReturnLeg = ReturnArmed && InStableOrbit(v) && !DockedSide.Docked(v);
            gates = onReturnLeg ? CrewGates.Return(Missions.Active) : CrewGates.Outbound(Missions.Active);
            proc = CrewProcedureCore.Begin(gates);
            SkipClearedGates(v);      // a mid-mission engage picks up where the vehicle already is
            Engaged = true;
            ReleasedHold = WpPhase.Idle;
            Debug.Log(Tag + "AUTO SEQUENCE engaged - " + (onReturnLeg ? "RETURN" : "OUTBOUND")
                      + " leg, " + CurrentGate().Title);
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
            ReleasedHold = WpPhase.Idle;
            Debug.Log(Tag + "AUTO SEQUENCE disengaged - " + why);
        }

        // ---- the per-frame authority ----
        private static int lastTickFrame = -1;

        public static void Tick()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            // A fresh flight clears the return latch (a capsule back on the pad has not flown out yet).
            if (v != null && v.situation == Vessel.Situations.PRELAUNCH && ReturnArmed && !Engaged)
                ReturnArmed = false;

            if (!Engaged) return;
            if (Time.frameCount == lastTickFrame) return;   // once per frame, not once per screen
            lastTickFrame = Time.frameCount;
            if (v == null) return;

            // A settled abort hands to the responder and stops conducting.
            if (proc.Phase == GatePhase.Abort) return;

            Gate g = CurrentGate();

            if (CrewProcedureCore.Complete(proc))
            {
                OnLegComplete(v);
                return;
            }

            // ---- refresh the Auto items from real vessel / life-support state ----
            if (g.Items != null && proc.Satisfied != null)
                for (int i = 0; i < g.Items.Length; i++)
                    if (g.Items[i].Kind == ItemKind.Auto)
                        CrewProcedureCore.SetItem(g, ref proc, i, EvalAuto(g.Items[i].Auto, v));
        }

        // ---- gate actions: engage the controller the cleared gate opens ----
        private static void ExecuteGateAction(GateId id)
        {
            switch (id)
            {
                case GateId.GoForLaunch:
                    if (!AutoPilot.Engaged) AutoPilot.Engage();
                    break;

                case GateId.ApproachInitiation:
                    if (AutoPilot.Engaged) AutoPilot.Disengage("crew-ops: insertion complete");
                    if (!StationApproach.Engaged && !DockingOps.Engaged) MissionOps.Rendezvous();
                    break;

                case GateId.HoldWp0: ReleasedHold = WpPhase.Hold0; break;
                case GateId.HoldWp1: ReleasedHold = WpPhase.Hold1; break;
                case GateId.HoldWp2: ReleasedHold = WpPhase.Hold2; break;

                case GateId.GoForUndock:
                    MissionOps.UndockAndLand();
                    break;

                case GateId.GoForDeorbit:
                    if (!DeorbitOps.Engaged && !EntryOps.Engaged) DeorbitOps.Engage();
                    break;
            }
        }

        // ---- auto-item evaluation: the system confirming a real fact ----
        private static bool EvalAuto(AutoCheck a, Vessel v)
        {
            switch (a)
            {
                case AutoCheck.StableOrbit:    return InStableOrbit(v);
                case AutoCheck.Docked:         return DockedSide.Docked(v);
                case AutoCheck.OnInternalPower: return Powered(v);
                case AutoCheck.CabinNominal:   return CabinNominal(v);
                case AutoCheck.AtWp0:          return WaypointApproachOps.Phase == WpPhase.Hold0;
                case AutoCheck.AtWp1:          return WaypointApproachOps.Phase == WpPhase.Hold1;
                case AutoCheck.AtWp2:          return WaypointApproachOps.Phase == WpPhase.Hold2;
                case AutoCheck.ConsumablesOk:
                    return LifeSupport.SufficientFor(LifeSupportBridge.Margins(v),
                                                     Missions.Active.MissionDurationDays,
                                                     Missions.Active.ConsumablesReserveDays);
                default:                        return true;
            }
        }

        private static void OnLegComplete(Vessel v)
        {
            if (!onReturnLeg)
            {
                // Outbound goal reached: docked (a docking mission) or in orbit (a free-flyer).
                bool reached = Missions.Active.HasRendezvous ? DockedSide.Docked(v) : InStableOrbit(v);
                if (reached)
                {
                    ReturnArmed = true;
                    Disengage(Missions.Active.HasRendezvous
                              ? "outbound complete - transfer crew, then UNDOCK"
                              : "in orbit - free flight; press AUTO SEQUENCE to return");
                }
                // else: last gate cleared but not yet at the goal - the phase is still flying; keep waiting.
            }
            else
            {
                Disengage("return underway - deorbit -> entry -> splashdown");
            }
        }

        // ---- a mid-mission engage: mark gates whose goal is already met as cleared ----
        private static void SkipClearedGates(Vessel v)
        {
            int guard = 0;
            while (!CrewProcedureCore.Complete(proc) && guard++ < 64)
            {
                Gate g = CrewProcedureCore.Current(gates, proc);
                if (!AlreadyPast(g.Id, v)) break;
                // Force it satisfied and advance past it.
                for (int i = 0; i < proc.Satisfied.Length; i++)
                    CrewProcedureCore.SetItem(g, ref proc, i, true);
                CrewProcedureCore.Go(g, ref proc);
                CrewProcedureCore.Advance(gates, ref proc);
            }
        }

        /// <summary>A gate whose outcome is already true when we engage mid-mission (no need to re-do it).</summary>
        private static bool AlreadyPast(GateId id, Vessel v)
        {
            bool airborne = v.situation != Vessel.Situations.PRELAUNCH;
            switch (id)
            {
                case GateId.Ingress:
                case GateId.SuitLeakCheck:
                case GateId.HatchClose:
                case GateId.GoForPropLoad:
                case GateId.ArmLaunchEscape:
                case GateId.InternalPower:
                case GateId.GoForLaunch:
                    return airborne;                       // already launched - the countdown is behind us
                case GateId.ApproachInitiation:
                    return DockedSide.Docked(v);           // already docked - approach is done
                default:
                    return false;
            }
        }

        // ---- helpers ----
        private static bool InStableOrbit(Vessel v)
        {
            return v.orbit != null && v.mainBody != null && v.orbit.PeA >= v.mainBody.atmosphereDepth;
        }

        private static bool Powered(Vessel v)
        {
            double amt, max;
            v.GetConnectedResourceTotals(PartResourceLibrary.ElectricityHashcode, out amt, out max);
            return max > 0.0 && amt / max > 0.01;
        }

        private static bool CabinNominal(Vessel v)
        {
            if (!Powered(v)) return false;
            LsState ls = LifeSupportBridge.Read(v);
            if (!ls.Present) return true;                  // no LS mod: cannot fault it, treat as nominal
            return ls.Oxygen01 > 0.05 && ls.Co201 < 0.95;  // real O2 supply left, CO2 not saturated
        }

        /// <summary>The phase being flown while a gate waits on its precondition - the button/label text.</summary>
        private static string FlyingName(Gate g)
        {
            switch (g.Id)
            {
                case GateId.ApproachInitiation: return "ASCENT";
                case GateId.HoldWp0:            return "RENDEZVOUS";
                case GateId.HoldWp1:            return "L-APPROACH";
                case GateId.HoldWp2:            return "L-APPROACH";
                case GateId.DockingComplete:    return "DOCKING";
                case GateId.GoForDeorbit:       return "DEPARTURE";
                default:                        return g.Title;
            }
        }
    }
}
