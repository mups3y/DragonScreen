// DragonScreen — CrewGate  (autopilot rebuild L4: the crew-in-the-loop GATE state machine)
// ============================================================================================
// The real Crew Dragon is autonomous BETWEEN gates and stops at each real crew DECISION POINT until the
// crew authorises it (docs/TRUE_AUTOPILOT_ARCHITECTURE.md; the crew-launch/return timeline in
// data/crew_missions.json). A GATE is a titled checklist of items — each either CREW-ACTIONED (the user
// taps it: "arm the launch-escape system", "Dragon crew — GO") or AUTO (the system confirms it from
// vessel state: "on internal power", "stable orbit") — plus the GO/NO-GO/ABORT decision. The autopilot
// HOLDS at the gate; only the crew's GO on a fully-satisfied checklist clears it and lets the conductor
// (ModeManager) fly the next phase. NO-GO holds; ABORT is absorbing.
//
// PURE + deterministic: given the gate, the per-item satisfied bits, and which button was pressed, Step()
// returns the resolved GatePhase — a headless test drives a whole countdown/approach through it. The glue
// (CrewProcedureOps, later) owns the live bits (crew taps + auto-items read from the vessel) and renders
// the card (pure/GateCard.cs). These four display types are the AUTHORITATIVE ones the screens read
// (moved here from the demolition stub); GatePhase lives in pure/MissionPhase.cs.
// ============================================================================================
namespace DragonScreen
{
    public enum ItemKind : byte { Auto, CrewAck }

    public struct ChecklistItem
    {
        public string Label;
        public ItemKind Kind;
        public static ChecklistItem Crew(string label) { ChecklistItem c; c.Label = label; c.Kind = ItemKind.CrewAck; return c; }
        public static ChecklistItem Sys(string label)  { ChecklistItem c; c.Label = label; c.Kind = ItemKind.Auto;    return c; }
    }

    public struct Gate
    {
        public GateId Id;
        public string Title;
        public ChecklistItem[] Items;
    }

    // Live state of the current gate: the overall phase + which items are satisfied so far.
    public struct ProcState
    {
        public GatePhase Phase;
        public bool[] Satisfied;
    }

    public struct CrewGateInputs
    {
        public Gate Gate;
        public bool[] Satisfied;       // per-item: crew taps (CrewAck) + auto-truth (Auto); len == Items.Length
        public bool GoPressed;
        public bool NoGoPressed;
        public bool AbortPressed;
    }

    public struct CrewGateStep
    {
        public GatePhase Phase;
        public bool Cleared;           // Phase == Go → the conductor may fly the next phase
        public bool Aborted;           // Phase == Abort → hand to the abort responder
        public bool Holding;           // Phase == NoGo → mission holds at this gate
    }

    public static class CrewGate
    {
        // Every item satisfied? (CrewAck taps + Auto truth are both folded into `satisfied`.)
        public static bool AllSatisfied(Gate g, bool[] satisfied)
        {
            int n = (g.Items == null) ? 0 : g.Items.Length;
            if (satisfied == null || satisfied.Length < n) return false;
            for (int i = 0; i < n; i++) if (!satisfied[i]) return false;
            return true;
        }

        // Resolve the gate phase from the current state + the button pressed this tick. Latching:
        // ABORT is absorbing; GO on a ready gate latches to Go; NO-GO holds until a fresh GO on a ready
        // gate resumes it.
        public static CrewGateStep Step(CrewGateInputs s, GatePhase current)
        {
            CrewGateStep r = new CrewGateStep();

            // ABORT — absorbing, wins over everything.
            if (s.AbortPressed || current == GatePhase.Abort)
            { r.Phase = GatePhase.Abort; r.Aborted = true; return r; }

            // already cleared — stay cleared (the conductor has moved on / is flying).
            if (current == GatePhase.Go)
            { r.Phase = GatePhase.Go; r.Cleared = true; return r; }

            bool allSat = AllSatisfied(s.Gate, s.Satisfied);

            // crew GO on a fully-satisfied checklist clears the gate (also resumes from a NO-GO hold).
            if (s.GoPressed && allSat)
            { r.Phase = GatePhase.Go; r.Cleared = true; return r; }

            // crew NO-GO holds the mission at this gate.
            if (s.NoGoPressed || current == GatePhase.NoGo)
            { r.Phase = GatePhase.NoGo; r.Holding = true; return r; }

            // otherwise: ready and waiting for the crew's GO, or still working the checklist.
            r.Phase = allSat ? GatePhase.GoReady : GatePhase.Holding;
            return r;
        }
    }
}
