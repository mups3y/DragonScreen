/*
 * DragonScreen - CrewProcedure (PURE)
 *
 * The crew-in-the-loop procedure engine: the mission is a sequence of GATES - the real Crew Dragon crew
 * decision points (readiness checklists, the GO/NO-GO poll, the approach hold-point GOs, GO for undock,
 * GO for deorbit). The autopilot flies the vehicle between gates; at a gate it HOLDS until the crew clears
 * it. This is the state machine that decides whether the autopilot may proceed. It flies nothing.
 *
 * ---- WHY PURE ----
 * "The user does exactly what the real astronauts do" is a safety-shaped requirement: the vehicle must not
 * pass a real decision point without the crew's GO, and must HOLD or ABORT on command. That is precisely
 * the kind of logic a headless test should drive through a whole countdown and approach and assert every
 * transition, so it lives here as a deterministic map over plain state. The glue (CrewProcedureOps) reads
 * the crew's taps and the vessel's auto-satisfied facts INTO this, and obeys the step it returns.
 *
 * ---- A GATE ----
 * A gate is a titled checklist. Each item is either CREW-ACTIONED (the user taps it: "arm the launch
 * escape system", "Dragon crew - GO") or AUTO (the system reports it true from vessel/LS state: "stable
 * orbit", "consumables sufficient"). A gate is GO-READY when every item is satisfied; the crew's GO then
 * CLEARS it and the autopilot proceeds to the next phase. NO-GO holds; ABORT hands to the abort responder.
 *
 * The catalog of gates for a mission is built by CrewGates from the MissionProfile (a free-flyer has no
 * approach gates, etc.). This file is the MODEL and the MACHINE; CrewGates is the DATA.
 */
namespace DragonScreen
{
    /// <summary>The real Crew Dragon decision points. The conductor maps each to the phase it authorizes.</summary>
    public enum GateId : byte
    {
        None = 0,
        // ---- COUNTDOWN ----
        Ingress,            // G1  crew ingress + comm check
        SuitLeakCheck,      // G2  suits pressurized, no leak
        HatchClose,         // G3  hatch closed + cabin leak check
        GoForPropLoad,      // G4  Launch Director: GO for propellant load
        ArmLaunchEscape,    // G5  Launch Escape System armed
        InternalPower,      // G6  Dragon on internal power / configured, cabin nominal
        GoForLaunch,        // G7  GO/NO-GO poll -> "Dragon crew is GO" -> launch
        // ---- ON ORBIT / APPROACH ----
        ApproachInitiation, // G9  GO for Approach Initiation (into the AE)
        HoldWp0,            // G10 hold 400 m below -> GO to enter the KOS
        HoldWp1,            // G11 hold 220 m on axis -> GO to continue
        HoldWp2,            // G12 hold 20 m -> GO for docking
        DockingComplete,    // G13 vestibule leak check, hatch open
        // ---- RETURN ----
        GoForUndock,        // G14 suit up, hatch close + leak, GO for undock
        GoForDeorbit,       // G15 departure complete -> GO for the deorbit burn
        EntryMonitor        // G16 entry - monitored, not gated (auto)
    }

    /// <summary>How a checklist item is satisfied.</summary>
    public enum ItemKind : byte
    {
        /// <summary>The crew taps it - the human doing the human job.</summary>
        CrewAck,
        /// <summary>The system reports it from real vessel / life-support state.</summary>
        Auto
    }

    /// <summary>Which real fact backs an Auto item, so the glue knows what to test.</summary>
    public enum AutoCheck : byte
    {
        None = 0,
        CabinNominal,       // cabin pressure + ppO2 in band (from LifeSupportBridge / CabinEnvironment)
        ConsumablesOk,      // LS margins cover mission + reserve (LifeSupport.SufficientFor)
        OnInternalPower,    // running on battery, umbilical clear
        StableOrbit,        // periapsis above the atmosphere
        Docked,             // hard-docked (DockedSide.Docked)
        AtWp0,              // station-keeping at WP0 (400 m below) - the L-approach is holding there
        AtWp1,              // station-keeping at WP1 (220 m ahead)
        AtWp2               // station-keeping at WP2 (20 m)
    }

    public struct ChecklistItem
    {
        public string Label;
        public ItemKind Kind;
        public AutoCheck Auto;   // meaningful only when Kind == Auto
    }

    public struct Gate
    {
        public GateId Id;
        public string Title;
        public ChecklistItem[] Items;
    }

    public enum GatePhase : byte
    {
        Holding,   // checklist open, not yet all satisfied
        GoReady,   // every item satisfied - the crew may press GO
        Go,        // cleared - the autopilot proceeds
        NoGo,      // crew called NO-GO - the mission holds here
        Abort      // crew called ABORT - hand to the abort responder
    }

    /// <summary>The live procedure state: where we are and the current gate's item bits.</summary>
    public struct ProcState
    {
        public int GateIndex;        // index into the mission's gate list; -1 = finished
        public GatePhase Phase;
        public bool[] Satisfied;     // one per item of the current gate
    }

    public static class CrewProcedureCore
    {
        /// <summary>Start at the first gate, Holding, with a fresh (all-false) satisfaction set.</summary>
        public static ProcState Begin(Gate[] gates)
        {
            ProcState st = new ProcState();
            if (gates == null || gates.Length == 0) { st.GateIndex = -1; st.Phase = GatePhase.Go; st.Satisfied = new bool[0]; return st; }
            st.GateIndex = 0;
            st.Phase = GatePhase.Holding;
            st.Satisfied = Fresh(gates[0]);
            return st;
        }

        /// <summary>True when the whole procedure is finished (past the last gate).</summary>
        public static bool Complete(ProcState st) { return st.GateIndex < 0; }

        /// <summary>The gate we are currently at, or a None gate when finished.</summary>
        public static Gate Current(Gate[] gates, ProcState st)
        {
            if (gates == null || st.GateIndex < 0 || st.GateIndex >= gates.Length)
            { Gate g = new Gate(); g.Id = GateId.None; g.Title = ""; g.Items = new ChecklistItem[0]; return g; }
            return gates[st.GateIndex];
        }

        /// <summary>Every item of the current gate is satisfied - GO is available.</summary>
        public static bool AllSatisfied(Gate g, ProcState st)
        {
            if (g.Items == null) return true;
            if (st.Satisfied == null || st.Satisfied.Length != g.Items.Length) return false;
            for (int i = 0; i < st.Satisfied.Length; i++)
                if (!st.Satisfied[i]) return false;
            return true;
        }

        /// <summary>
        /// Set item i's satisfaction (a crew tap toggling a CrewAck, or the glue reporting an Auto fact),
        /// then refresh Holding&lt;-&gt;GoReady. No-op once the gate is Go/NoGo/Abort - a cleared gate is settled.
        /// </summary>
        public static void SetItem(Gate g, ref ProcState st, int i, bool value)
        {
            if (st.Satisfied == null || i < 0 || i >= st.Satisfied.Length) return;
            if (st.Phase == GatePhase.Go || st.Phase == GatePhase.Abort) return;
            st.Satisfied[i] = value;
            Refresh(g, ref st);
        }

        /// <summary>Recompute Holding&lt;-&gt;GoReady from the item bits. NoGo relaxes back to Holding/GoReady.</summary>
        public static void Refresh(Gate g, ref ProcState st)
        {
            if (st.Phase == GatePhase.Go || st.Phase == GatePhase.Abort) return;
            st.Phase = AllSatisfied(g, st) ? GatePhase.GoReady : GatePhase.Holding;
        }

        /// <summary>The crew presses GO. Only clears when GO-ready. Returns true if it cleared.</summary>
        public static bool Go(Gate g, ref ProcState st)
        {
            if (AllSatisfied(g, st)) { st.Phase = GatePhase.Go; return true; }
            return false;
        }

        /// <summary>The crew calls NO-GO: the mission holds at this gate (does not advance).</summary>
        public static void NoGo(ref ProcState st)
        {
            if (st.Phase == GatePhase.Go || st.Phase == GatePhase.Abort) return;
            st.Phase = GatePhase.NoGo;
        }

        /// <summary>The crew calls ABORT.</summary>
        public static void Abort(ref ProcState st) { st.Phase = GatePhase.Abort; }

        /// <summary>
        /// A gate that has cleared (Go) advances to the next gate (Holding, fresh bits). Past the last gate
        /// the procedure is Complete. Call after the conductor has flown the phase the cleared gate opened.
        /// </summary>
        public static void Advance(Gate[] gates, ref ProcState st)
        {
            if (st.Phase != GatePhase.Go || gates == null) return;
            int next = st.GateIndex + 1;
            if (next >= gates.Length) { st.GateIndex = -1; st.Phase = GatePhase.Go; st.Satisfied = new bool[0]; return; }
            st.GateIndex = next;
            st.Phase = GatePhase.Holding;
            st.Satisfied = Fresh(gates[next]);
        }

        private static bool[] Fresh(Gate g)
        {
            int n = (g.Items == null) ? 0 : g.Items.Length;
            return new bool[n];
        }
    }
}
