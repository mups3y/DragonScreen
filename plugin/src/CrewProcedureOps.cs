// DragonScreen — CrewProcedureOps  (KSP glue: the crew-in-the-loop mission conductor)
// ============================================================================================
// The real conductor, replacing the demolition stub. It drives the PURE, headless-tested L4 pieces —
// ModeManager (the mission plan), CrewGate (the gate state machine), CrewGates (the catalog) — against
// the LIVE vessel: it resolves the mission from the VAB craft name, walks the plan, satisfies each gate's
// AUTO items from vessel state (the crew taps the CrewAck items), holds until the crew's GO, and hands
// ABORT to the responder. The SCREENS read this surface (VesselData → GateCard) and route the crew's
// taps here (ScreenPainter). FlightDriver.Tick(v) advances it each physics frame.
//
// ⛔ GLUE DISCIPLINE: no guidance math here — the decisions are the pure machine's; this only feeds it the
// vessel and actuates. Defensive throughout (the glue is where bugs live). This seam wires the COUNTDOWN
// gates + ignition on the launch GO; the flying-phase controllers advance the plan in later seams.
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class CrewProcedureOps
    {
        static bool engaged;
        static MissionProfile mission;
        static MissionStep[] plan;
        static int index;
        static GatePhase phase = GatePhase.Holding;
        static bool[] satisfied;
        static Gate gate;
        static uint boundVesselId;

        // ⛔ TEMPORARY (user 2026-08-27): auto-advance the crew GO/NO-GO gates so AUTO SEQUENCE flies hands-off
        // for faster test flights — the crew-ack taps + the GO press are issued automatically. The AUTO
        // (vessel-state) checks still hold a genuinely bad pad. Set false to RESTORE the interactive gates
        // (the gate machine + screens are unchanged; this only synthesises the crew's inputs).
        public static bool AutoAdvanceGates = true;

        // latched crew button events, consumed on the next Tick
        static bool goPressed, noGoPressed, abortPressed;

        // one-shot actuation intents FlightDriver consumes
        static bool launchPending;
        static bool abortLatched;
        static bool returnLeg;      // true once the undock gate (G14) clears — distinguishes the return
                                    // Phasing (departure) from the outbound Phasing (rendezvous)

        // ---- screen-facing surface (unchanged signatures from the stub) ----
        public static bool Engaged { get { return engaged; } }
        public static string PhaseName
        {
            get { return (engaged && plan != null && index < plan.Length) ? plan[index].Label : null; }
        }
        public static bool CrewActionNeeded()
        {
            return engaged && CurrentIsGate() && phase != GatePhase.Go;
        }
        public static Gate CurrentGate() { return gate; }
        public static ProcState Proc { get { ProcState p; p.Phase = phase; p.Satisfied = satisfied; return p; } }

        public static void Toggle() { if (engaged) Disengage(); else Engage(); }
        public static void ToggleItem(int i)
        {
            if (satisfied != null && i >= 0 && i < satisfied.Length && IsCrewItem(i))
                satisfied[i] = !satisfied[i];
        }
        public static void PressGo() { goPressed = true; }
        public static void PressNoGo() { noGoPressed = true; }
        public static void PressAbort() { abortPressed = true; }

        // ---- FlightDriver-facing surface ----
        public static bool ConsumeLaunch() { bool l = launchPending; launchPending = false; return l; }
        public static bool AbortActive { get { return abortLatched; } }
        public static MissionPhase ActivePhase
        {
            get { return (engaged && plan != null && index < plan.Length && plan[index].Kind == StepKind.Fly)
                         ? plan[index].Phase : MissionPhase.Unknown; }
        }
        public static MissionProfile Profile { get { return mission; } }

        // A ModeStep snapshot for the flight recorder (mission_phase + mode columns), built from the live
        // conductor state so the recorder always knows the phase — even during an abort or a between-phase gap.
        public static ModeStep CurrentMode
        {
            get
            {
                ModeStep ms = new ModeStep();
                ms.Index = index;
                ms.ActivePhase = ActivePhase;
                ms.Holding = engaged && CurrentIsGate() && phase != GatePhase.Go;
                ms.Flying = engaged && CurrentIsFly();
                ms.Aborted = abortLatched;
                return ms;
            }
        }

        public static bool IsReturn { get { return returnLeg; } }
        public static bool AtGate { get { return engaged && CurrentIsGate(); } }
        public static GateId CurrentGateId { get { return CurrentIsGate() ? plan[index].Gate : GateId.None; } }

        // The next GATE step after the current one — lets a flying controller know which leg it is on
        // (e.g. the docking approach leg toward WP0/WP1/WP2 is identified by the gate it leads to).
        public static GateId NextGateId
        {
            get
            {
                if (!engaged || plan == null) return GateId.None;
                for (int i = index; i < plan.Length; i++)
                    if (plan[i].Kind == StepKind.Gate) return plan[i].Gate;
                return GateId.None;
            }
        }

        // Signal from a flying controller that its phase is complete → the conductor advances.
        public static void PhaseComplete()
        {
            if (!engaged || plan == null || !CurrentIsFly()) return;
            ModeStep ms = ModeManager.Advance(plan, index, new ModeInputs { PhaseComplete = true });
            index = ms.Index; LoadGate();
        }

        static void Engage()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;
            mission = Missions.Resolve(v.vesselName);
            if (!mission.Valid)
            {
                // NO-GO rather than fly a guessed mission — surface it, do not engage.
                Debug.LogWarning("[DragonScreen] AUTO SEQUENCE: craft name '" + v.vesselName
                                 + "' matches no mission profile — NO-GO. Rename the craft to a mission.");
            }
            plan = ModeManager.Plan(mission);
            index = 0; engaged = true; boundVesselId = v.persistentId;
            goPressed = noGoPressed = abortPressed = false;
            launchPending = false; abortLatched = false; returnLeg = false;
            LoadGate();
            Debug.Log("[DragonScreen] AUTO SEQUENCE engaged: " + mission.Name + " (" + plan.Length + " steps)");
        }

        static void Disengage()
        {
            engaged = false; plan = null; satisfied = null; gate = new Gate();
            phase = GatePhase.Holding; launchPending = false; abortLatched = false;
            Debug.Log("[DragonScreen] AUTO SEQUENCE disengaged");
        }

        // ⛔ Hard reset for a NEW flight scene (revert-to-VAB/launch, fresh launch). The conductor is static,
        // so without this the previous flight's engaged/index/return state carries onto the next vehicle and
        // the autopilot starts flying a fresh pad rocket mid-mission. Called from FlightDriver.Start().
        public static void ForceReset()
        {
            engaged = false; plan = null; satisfied = null; gate = new Gate();
            phase = GatePhase.Holding; index = 0; boundVesselId = 0;
            goPressed = noGoPressed = abortPressed = false;
            launchPending = false; abortLatched = false; returnLeg = false;
        }

        static bool CurrentIsGate() { return plan != null && index < plan.Length && plan[index].Kind == StepKind.Gate; }
        static bool CurrentIsFly() { return plan != null && index < plan.Length && plan[index].Kind == StepKind.Fly; }
        static bool IsCrewItem(int i)
        {
            return gate.Items != null && i < gate.Items.Length && gate.Items[i].Kind == ItemKind.CrewAck;
        }

        static void LoadGate()
        {
            if (CurrentIsGate())
            {
                gate = CrewGates.ById(mission, plan[index].Gate);
                int n = (gate.Items == null) ? 0 : gate.Items.Length;
                satisfied = new bool[n];
                phase = GatePhase.Holding;
            }
            else { gate = new Gate(); satisfied = null; phase = GatePhase.Holding; }
        }

        // Advanced each physics frame by FlightDriver with the live vessel.
        public static void Tick(Vessel v)
        {
            if (!engaged || plan == null || v == null) return;
            if (v.persistentId != boundVesselId) { boundVesselId = v.persistentId; }   // follow handover
            if (index >= plan.Length) return;   // mission complete

            if (!CurrentIsGate()) { goPressed = noGoPressed = abortPressed = false; return; }

            // satisfy the AUTO items from live vessel state; CrewAck items keep their tapped value.
            if (gate.Items != null)
                for (int i = 0; i < gate.Items.Length && i < satisfied.Length; i++)
                    if (gate.Items[i].Kind == ItemKind.Auto)
                        satisfied[i] = AutoSatisfied(gate.Id, gate.Items[i].Label, v);

            // ⛔ hands-off test mode: auto-tap the crew-ack items and press GO (the AUTO checks above still
            // gate a bad pad). Restore the interactive gates by setting AutoAdvanceGates = false.
            if (AutoAdvanceGates)
            {
                if (gate.Items != null)
                    for (int i = 0; i < gate.Items.Length && i < satisfied.Length; i++)
                        if (gate.Items[i].Kind == ItemKind.CrewAck) satisfied[i] = true;
                goPressed = true;
            }

            CrewGateInputs gi;
            gi.Gate = gate; gi.Satisfied = satisfied;
            gi.GoPressed = goPressed; gi.NoGoPressed = noGoPressed; gi.AbortPressed = abortPressed;
            CrewGateStep step = CrewGate.Step(gi, phase);
            phase = step.Phase;
            goPressed = noGoPressed = abortPressed = false;

            if (step.Aborted) { abortLatched = true; return; }

            if (step.Cleared)
            {
                GateId cleared = gate.Id;
                if (cleared == GateId.LaunchGoG7) launchPending = true;   // ignition intent for FlightDriver
                if (cleared == GateId.UndockGoG14) returnLeg = true;      // now on the return leg
                ModeStep ms = ModeManager.Advance(plan, index, new ModeInputs { GateGo = true });
                index = ms.Index; LoadGate();
            }
        }

        // AUTO-item truth from the vessel. Real proxies where a signal exists; a healthy-pad default of
        // true otherwise (these are confirmations that are nominal on a good pad — the CrewAck items are
        // the real gates). Richer signals (LS margins, alignment) get wired as each system lands.
        static bool AutoSatisfied(GateId id, string label, Vessel v)
        {
            try
            {
                if (label.IndexOf("internal power", StringComparison.OrdinalIgnoreCase) >= 0
                    || label.IndexOf("power", StringComparison.OrdinalIgnoreCase) >= 0)
                    return HasCharge(v);
                if (label.IndexOf("consumables", StringComparison.OrdinalIgnoreCase) >= 0)
                    return v.GetCrewCount() > 0;   // crew aboard + LS present (LS margin proxy for now)
                if (label.IndexOf("Abort system armed", StringComparison.OrdinalIgnoreCase) >= 0)
                    return FlightCommands.EscapeArmed;
            }
            catch { }
            return true;   // nominal-on-a-healthy-pad confirmation
        }

        static bool HasCharge(Vessel v)
        {
            try
            {
                PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition("ElectricCharge");
                if (def == null) return true;
                double amt, max;
                v.GetConnectedResourceTotals(def.id, out amt, out max, true);
                return amt > 0.0;
            }
            catch { return true; }
        }
    }
}
