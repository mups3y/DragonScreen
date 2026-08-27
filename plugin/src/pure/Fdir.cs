// DragonScreen — Fdir  (autopilot rebuild L5: Fault Detection, Isolation, Recovery — the safety spine)
// ============================================================================================
// Detect → Isolate → Recover (docs/TRUE_AUTOPILOT_ARCHITECTURE.md §9). Concrete debounced monitors
// (pure/FaultMonitor.cs) watch the flight residuals; the highest-priority tripped fault is mapped, by a
// phase-aware decision table, onto the least-intervention RECOVERY LADDER:
//     Continue → Retry → Reconfigure → Replan → Downmode → Abort / SafeMode
// Try the cheap local fix first; fall to the guaranteed floor (abort-to-home) only when nothing local can
// hold the mission. The monitors:
//   • ThrustShortfall   — delivered Δv-rate far below expected (engine underperformance/failure).
//   • TrajectoryDivergence — position error growing past a bound.
//   • ConvergenceStall  — a plan not progressing (SUPPRESSED during an intended crew GO-gate HOLD, so
//                          FDIR never mistakes a hold for a frozen-plan fault — §11).
//   • ResourceCritical  — propellant / heat / consumables margin near zero.
//   • NoControlSolution — the control layer reports it cannot hold attitude.
//   • KeepOutBreach     — an unplanned penetration of the station keep-out sphere.
// PURE + deterministic; abort-to-home is the guaranteed floor. AbortResponder.cs turns an Abort into the
// phase-correct action (LES / KOS-retreat / safe-hold).
// ============================================================================================
using System;

namespace DragonScreen
{
    public enum FaultKind : byte
    {
        None, KeepOutBreach, ThrustShortfall, NoControlSolution, ResourceCritical,
        TrajectoryDivergence, ConvergenceStall
    }

    // The least-intervention ladder (index order = increasing severity).
    public enum Recovery : byte { Continue, Retry, Reconfigure, Replan, Downmode, Abort, SafeMode }

    public struct FdirState
    {
        public MonitorState Thrust, Divergence, Stall, Resource, Control, KeepOut;
        // Escalation ladder tracking (§9): a fault a recovery rung does NOT clear within RungGraceS escalates
        // to the next rung, so a persistent fault reaches Abort rather than being retried forever.
        public FaultKind EscFault;   // the fault currently being recovered (None = idle)
        public Recovery EscRung;     // the rung currently applied
        public double EscRungS;      // seconds the current rung has run without the fault clearing
    }

    public struct FdirInputs
    {
        public bool Valid;
        public double Dt;
        public MissionPhase Phase;
        public bool GateHolding;             // an intended crew GO-gate hold → suppress the stall monitor
        public bool Powered;                 // a burn is expected right now (thrust monitor only then)

        public double ThrustDeliveredFrac;   // delivered / expected Δv-rate (1 = nominal; <1 = shortfall)
        public double TrajErrorM;            // guidance position error
        public double PlanProgressRate;      // range-closing / plan-progress rate (≤0 = stalled)
        public double ResourceMargin01;      // worst of propellant / heat / consumables margin (0 = none)
        public bool ControlSolutionOk;       // the control layer can hold attitude
        public double KosRangeM, KosRadiusM; // station keep-out sphere
        public bool CorridorOk;              // on the planned approach corridor (false + inside KOS = breach)
    }

    public struct FdirReport
    {
        public FaultKind Fault;              // highest-priority tripped fault (None = healthy)
        public Recovery Response;            // the ladder rung to take
        public bool Abort;                   // Response reached the floor (Abort or SafeMode)
    }

    public static class Fdir
    {
        // ---- thresholds (research-seeded; self-cal will refine the margins later) ----
        [Tunable] public static double ThrustTripFrac = 0.6;    // delivered < 60% of expected = shortfall
        [Tunable] public static double ThrustClearFrac = 0.8;   // recovered above 80% clears it
        [Tunable] public static double TrajTripM = 5000.0;      // 5 km guidance error trips divergence
        [Tunable] public static double TrajClearM = 2000.0;
        [Tunable] public static double ResourceTrip01 = 0.05;   // <5% margin is critical
        [Tunable] public static double ResourceClear01 = 0.15;
        [Tunable] public static double ConfirmS = 2.0;          // a fault must persist this long to trip
        [Tunable] public static double ClearS = 3.0;            // and clear this long to reset
        [Tunable] public static double FastConfirmS = 0.3;      // KOS breach / lost-control trip fast
        [Tunable] public static double RungGraceS = 5.0;        // a recovery rung gets this long to clear the
                                                                // fault before FDIR escalates to the next rung

        public static FdirReport Update(ref FdirState st, FdirInputs s)
        {
            FdirReport r = new FdirReport();
            r.Fault = FaultKind.None; r.Response = Recovery.Continue;
            if (!s.Valid || s.Dt <= 0.0) return r;
            double dt = s.Dt;

            // ---- KEEP-OUT BREACH: inside the KOS AND off the planned corridor → fast trip ----
            bool kosOver = s.KosRadiusM > 0 && s.KosRangeM < s.KosRadiusM && !s.CorridorOk;
            bool kosUnder = !(s.KosRadiusM > 0 && s.KosRangeM < s.KosRadiusM);
            bool kos = FaultMonitor.Update(ref st.KeepOut, kosOver, kosUnder, dt, FastConfirmS, ClearS);

            // ---- THRUST SHORTFALL (only while a burn is expected) ----
            bool thrOver = s.Powered && s.ThrustDeliveredFrac < ThrustTripFrac;
            bool thrUnder = !s.Powered || s.ThrustDeliveredFrac > ThrustClearFrac;
            bool thrust = FaultMonitor.Update(ref st.Thrust, thrOver, thrUnder, dt, ConfirmS, ClearS);

            // ---- NO CONTROL SOLUTION ----
            bool ctlOver = !s.ControlSolutionOk;
            bool ctlUnder = s.ControlSolutionOk;
            bool control = FaultMonitor.Update(ref st.Control, ctlOver, ctlUnder, dt, FastConfirmS, ClearS);

            // ---- RESOURCE CRITICAL ----
            bool resOver = s.ResourceMargin01 < ResourceTrip01;
            bool resUnder = s.ResourceMargin01 > ResourceClear01;
            bool resource = FaultMonitor.Update(ref st.Resource, resOver, resUnder, dt, ConfirmS, ClearS);

            // ---- TRAJECTORY DIVERGENCE ----
            bool divOver = s.TrajErrorM > TrajTripM;
            bool divUnder = s.TrajErrorM < TrajClearM;
            bool diverge = FaultMonitor.Update(ref st.Divergence, divOver, divUnder, dt, ConfirmS, ClearS);

            // ---- CONVERGENCE STALL (suppressed during an intended crew hold) ----
            bool stallOver = !s.GateHolding && s.PlanProgressRate <= 0.0;
            bool stallUnder = s.GateHolding || s.PlanProgressRate > 0.0;
            bool stall = FaultMonitor.Update(ref st.Stall, stallOver, stallUnder, dt, ConfirmS * 3.0, ClearS);

            // ---- ISOLATE: pick the highest-priority tripped fault (severity order) ----
            if (kos)            r.Fault = FaultKind.KeepOutBreach;
            else if (thrust)    r.Fault = FaultKind.ThrustShortfall;
            else if (control)   r.Fault = FaultKind.NoControlSolution;
            else if (resource)  r.Fault = FaultKind.ResourceCritical;
            else if (diverge)   r.Fault = FaultKind.TrajectoryDivergence;
            else if (stall)     r.Fault = FaultKind.ConvergenceStall;

            r.Response = Escalate(ref st, r.Fault, s.Phase, s.ResourceMargin01, dt);
            r.Abort = r.Response == Recovery.Abort || r.Response == Recovery.SafeMode;
            return r;
        }

        // Escalation over the least-intervention rung from Recover(): hold that rung, but if the SAME fault
        // persists past RungGraceS (the rung did not clear it) step UP the ladder — Retry→Reconfigure→Replan→
        // Downmode→Abort→SafeMode — so a fault can never be retried indefinitely; a stubborn one is guaranteed
        // to reach Abort. Resets to Continue when the fault clears; a new/different fault restarts at its own
        // phase-correct base rung. Never de-escalates below that base (e.g. a fresh ascent thrust-shortfall
        // starts at Abort, not below).
        public static Recovery Escalate(ref FdirState st, FaultKind fault, MissionPhase phase,
                                        double resourceMargin01, double dt)
        {
            if (fault == FaultKind.None)
            {
                st.EscFault = FaultKind.None; st.EscRung = Recovery.Continue; st.EscRungS = 0.0;
                return Recovery.Continue;
            }

            Recovery baseRung = Recover(fault, phase, resourceMargin01);

            if (fault != st.EscFault)
            {
                st.EscFault = fault; st.EscRung = baseRung; st.EscRungS = 0.0;   // a new fault → its base rung
            }
            else
            {
                st.EscRungS += dt;
                if (st.EscRungS >= RungGraceS && (byte)st.EscRung < (byte)Recovery.SafeMode)
                {
                    st.EscRung = (Recovery)((byte)st.EscRung + 1);   // escalate one rung up the ladder
                    st.EscRungS = 0.0;
                }
            }

            if ((byte)st.EscRung < (byte)baseRung) st.EscRung = baseRung;   // never below the phase-correct base
            return st.EscRung;
        }

        // The phase-aware fault→recovery decision table. Least-intervention first; abort-to-home is the floor.
        public static Recovery Recover(FaultKind fault, MissionPhase phase, double resourceMargin01)
        {
            switch (fault)
            {
                case FaultKind.None:
                    return Recovery.Continue;

                case FaultKind.KeepOutBreach:
                    return Recovery.Abort;                 // any unplanned KOS breach aborts (retreat) — hard rule

                case FaultKind.ThrustShortfall:
                    // during ascent an engine-out is a launch abort; elsewhere re-solve the burn/leg.
                    if (phase == MissionPhase.Ascent || phase == MissionPhase.Prelaunch) return Recovery.Abort;
                    return Recovery.Replan;

                case FaultKind.NoControlSolution:
                    // in a powered/critical phase this is an abort; otherwise downmode and safe.
                    if (phase == MissionPhase.Ascent || phase == MissionPhase.Entry) return Recovery.Abort;
                    return Recovery.Downmode;

                case FaultKind.ResourceCritical:
                    return resourceMargin01 <= 0.001 ? Recovery.SafeMode : Recovery.Downmode;

                case FaultKind.TrajectoryDivergence:
                    return Recovery.Replan;

                case FaultKind.ConvergenceStall:
                    return Recovery.Replan;

                default:
                    return Recovery.Continue;
            }
        }
    }
}
