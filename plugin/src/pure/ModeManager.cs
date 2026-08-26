// DragonScreen — ModeManager  (autopilot rebuild L4: the mission conductor / phase sequencer)
// ============================================================================================
// The mission is an ordered list of STEPS: a GATE (the autopilot HOLDS until the crew's GO clears it —
// pure/CrewGate.cs) or a FLY phase (an L3 controller flies until it reports the phase complete). The
// conductor walks the list — it never flies anything itself; it decides WHICH phase is active and
// whether the autopilot may advance. This is the real Crew Dragon operating concept: autonomous between
// gates, authorised by the crew at each real decision point (docs/TRUE_AUTOPILOT_ARCHITECTURE.md).
//
// The plan is built FROM a MissionProfile (mission-as-data, pure/MissionProfile.cs): countdown gates →
// ascent → (rendezvous holds → dock → docked → undock, ISS crew only, else free-flight) → deorbit gate →
// return. ABORT at any gate is absorbing and hands to the abort responder. PURE + headless-tested.
// ============================================================================================
namespace DragonScreen
{
    public enum StepKind : byte { Gate, Fly }

    public struct MissionStep
    {
        public StepKind Kind;
        public GateId Gate;        // when Kind == Gate
        public MissionPhase Phase; // when Kind == Fly — the L3 controller that flies this step
        public string Label;

        public static MissionStep AtGate(GateId id, string label)
        { MissionStep s; s.Kind = StepKind.Gate; s.Gate = id; s.Phase = MissionPhase.Unknown; s.Label = label; return s; }
        public static MissionStep Fly(MissionPhase p, string label)
        { MissionStep s; s.Kind = StepKind.Fly; s.Gate = GateId.None; s.Phase = p; s.Label = label; return s; }
    }

    public struct ModeInputs
    {
        public bool GateGo;         // the current gate cleared (CrewGate returned Go)
        public bool GateAbort;      // abort commanded at the current gate / phase
        public bool PhaseComplete;  // the current FLY phase's L3 FSM reported done
    }

    public struct ModeStep
    {
        public int Index;
        public MissionStep Step;
        public bool Holding;        // at a Gate, waiting for the crew's GO
        public bool Flying;         // at a Fly phase
        public bool Aborted;
        public bool Complete;       // walked off the end of the plan → mission done (splashed)
        public GateId PendingGate;  // the gate to raise now (None while flying)
        public MissionPhase ActivePhase;
    }

    public static class ModeManager
    {
        // The mission plan for a profile. ISS crew = full timeline; free-flyer omits rendezvous/dock/undock.
        public static MissionStep[] Plan(MissionProfile m)
        {
            var steps = new System.Collections.Generic.List<MissionStep>();

            // ---- countdown gates (every crewed mission) ----
            steps.Add(MissionStep.AtGate(GateId.IngressCommG1,  "Crew ingress & comm check"));
            steps.Add(MissionStep.AtGate(GateId.SuitLeakG2,     "Suit leak check"));
            steps.Add(MissionStep.AtGate(GateId.HatchCloseG3,   "Hatch close & cabin leak check"));
            steps.Add(MissionStep.AtGate(GateId.PropLoadGoG4,   "GO for propellant load"));
            steps.Add(MissionStep.AtGate(GateId.LesArmG5,       "Launch escape system ARM"));
            steps.Add(MissionStep.AtGate(GateId.InternalPowerG6,"Dragon to internal power"));
            steps.Add(MissionStep.AtGate(GateId.LaunchGoG7,     "GO for launch"));

            // ---- ascent (monitored, abort armed) ----
            steps.Add(MissionStep.Fly(MissionPhase.Ascent, "Ascent to orbit"));

            if (m.HasRendezvous)
            {
                // ---- rendezvous + prox-ops (the L-approach holds are gates) ----
                steps.Add(MissionStep.Fly(MissionPhase.Phasing, "Phasing to Approach Initiation"));
                steps.Add(MissionStep.AtGate(GateId.ApproachInitGoG9, "GO for Approach Initiation"));
                steps.Add(MissionStep.Fly(MissionPhase.Approach, "Approach to WP0"));
                steps.Add(MissionStep.AtGate(GateId.WP0HoldG10, "WP0 hold — GO to enter KOS"));
                steps.Add(MissionStep.Fly(MissionPhase.Approach, "Approach to WP1"));
                steps.Add(MissionStep.AtGate(GateId.WP1HoldG11, "WP1 hold — GO"));
                steps.Add(MissionStep.Fly(MissionPhase.Approach, "Approach to WP2"));
                steps.Add(MissionStep.AtGate(GateId.WP2DockGoG12, "WP2 hold — GO for docking"));
                steps.Add(MissionStep.Fly(MissionPhase.Docked, "Soft → hard capture"));
                steps.Add(MissionStep.AtGate(GateId.DockingCompleteG13, "Docking complete — vestibule"));
                steps.Add(MissionStep.Fly(MissionPhase.Docked, "Docked — crew aboard"));
                steps.Add(MissionStep.AtGate(GateId.UndockGoG14, "GO for undock"));
                steps.Add(MissionStep.Fly(MissionPhase.Phasing, "Departure & phasing"));
            }
            else
            {
                // ---- free-flight dwell (no rendezvous) ----
                steps.Add(MissionStep.Fly(MissionPhase.Coast, "Free-flight"));
            }

            // ---- return ----
            steps.Add(MissionStep.AtGate(GateId.DeorbitGoG15, "GO for deorbit burn"));
            steps.Add(MissionStep.Fly(MissionPhase.Entry, "Deorbit → lifting entry"));
            steps.Add(MissionStep.Fly(MissionPhase.Drogues, "Drogues → mains → splashdown"));

            return steps.ToArray();
        }

        // Walk the plan one tick. `index` is the current step; returns the (possibly advanced) step + status.
        public static ModeStep Advance(MissionStep[] plan, int index, ModeInputs s)
        {
            ModeStep r = new ModeStep();
            if (plan == null || plan.Length == 0) { r.Complete = true; return r; }
            if (index < 0) index = 0;

            // ABORT is absorbing — hold the index, flag it, the responder takes over.
            if (s.GateAbort)
            { r.Index = index; r.Step = plan[index < plan.Length ? index : plan.Length - 1]; r.Aborted = true; return r; }

            if (index >= plan.Length)
            { r.Index = plan.Length; r.Complete = true; r.ActivePhase = MissionPhase.Splashdown; return r; }

            MissionStep step = plan[index];

            if (step.Kind == StepKind.Gate)
            {
                if (s.GateGo) index++;          // crew cleared the gate → advance to the next step
            }
            else // Fly
            {
                if (s.PhaseComplete) index++;   // the L3 controller finished this phase → advance
            }

            if (index >= plan.Length)
            { r.Index = plan.Length; r.Complete = true; r.ActivePhase = MissionPhase.Splashdown; return r; }

            r.Index = index;
            r.Step = plan[index];
            r.Holding = plan[index].Kind == StepKind.Gate;
            r.Flying = plan[index].Kind == StepKind.Fly;
            r.PendingGate = r.Holding ? plan[index].Gate : GateId.None;
            r.ActivePhase = r.Flying ? plan[index].Phase : MissionPhase.Unknown;
            return r;
        }

        // Convenience: the gate to raise at the current step (None if the step is a Fly phase).
        public static GateId GateAt(MissionStep[] plan, int index)
        {
            if (plan == null || index < 0 || index >= plan.Length) return GateId.None;
            return plan[index].Kind == StepKind.Gate ? plan[index].Gate : GateId.None;
        }
    }
}
