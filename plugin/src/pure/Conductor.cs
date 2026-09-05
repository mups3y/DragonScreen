// DragonScreen — Conductor  (PURE: the decision layer that turns a plan step into a ConductorAction)
// ============================================================================================
// Register T16, §B12.2 / §B12.3 / §B12.4. This is the "decision layer ON TOP of the sequencer" the
// re-scope asked for: `ModeManager` already answers *which step of the plan we are on*; this answers
// *what to do about it*.
//
// ---- ⛔ IT COMMANDS NOTHING. `Decide` IS A PURE FUNCTION AND RETURNS A VALUE ----
// No Unity, no KSP, no MechJeb reference; nothing here can touch a vessel. Executing the returned
// `ConductorAction` against the embedded `MechJebCore` is the glue's job and is NEW work arriving
// with T18 onward (§B12.2's corrected entry). Until then this is decided, tested and never acted on.
//
// ---- WHAT IS TAKEN FROM THE PLAN, WORD FOR WORD, AND WHAT IS NOT DECIDED HERE ----
// §B12.3's phase table is transcribed, not designed:
//   Prelaunch  → load profile + target + ARM PVG
//   Ascent     → PVG ascent (§B8)
//   Coast      → Maneuver-Planner circularize/apsis ops + Node Executor (§B10.2, P2)
//   Phasing    → as Coast
//   Approach   → Plane → Transfer → CourseCorrection → KillRelVel + Node Executor, RE-PLANNED LIVE
//                (§B12.4), then hand off at the Keep-Out Sphere to the Docking AP — the DEFAULT (O6)
//   Docked     → idle / KILL-ROT
//   Entry      → deorbit via OperationPeriapsis beforehand, then SmartASS heat-shield-forward (§B10.5)
//   Drogues    → chute triggering; the conductor does not fly it
//   Mains      → as Drogues
//   Splashdown → release control
//   Landed     → release control
//
// ⛔ THE ONE THING §B12.3 SAYS THAT THIS FILE DELIBERATELY DOES NOT DO. Its Approach entry ends
// "the crew's manual docking button overrides to the Manual ISS Docking screen and shuts the Docking
// AP down". That override is a CREW COMMAND arriving through the screens, i.e. §B12.5's front-end and
// §14.4(a)'s honest no-op today. It is represented here only as an INPUT (`ManualDockingRequested`),
// never as something this core initiates.
//
// ---- NO THRESHOLD IS DEFINED IN THIS FILE (§1.4 / C1.15) ----
// §B12.4's rule is `closestApproachErr > εd OR residual > tol OR drift → rebuild Operation k`. The
// RULE is the pure core's and is implemented below. The NUMBERS are §B7-B11 "locked params" and
// T22's empirical tune; inventing them is what §1.4 reserves. So every threshold arrives on
// `ConductorInputs` from the caller and this file contains no magic number at all — verified by
// `ConductorTest`, which asserts the file's own behaviour is unchanged when the caller moves them.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>The telemetry + policy snapshot a decision is made from. Plain data: the caller fills
    /// it from `VesselData` (glue) or a test fixture, and this core never reaches for anything else.</summary>
    public struct ConductorInputs
    {
        // ---- where the plan says we are (from ModeManager) ----
        public bool Holding;                 // a crew gate is up and not cleared
        public bool Aborted;                 // abort commanded
        public bool Complete;                // walked off the end of the plan
        public bool PhaseComplete;           // the current FLY phase's L3 FSM reported done
        public MissionPhase Phase;

        // ---- what the vehicle is doing ----
        public bool NodeExists;              // a maneuver node is on the flight plan
        public bool NodeBurned;              // the node executor reported the burn finished
        public bool InKeepOutSphere;         // inside the ISS KOS - §B12.3's hand-off point
        public bool ManualDockingRequested;  // the crew took manual docking (§B12.5's front-end)

        // ---- §B12.4's re-plan measurements, and the thresholds they are judged against ----
        // ⛔ BOTH SIDES COME FROM THE CALLER. The rule is ours; the numbers are T22's.
        public double ClosestApproachErrM;
        public double ClosestApproachTolM;   // εd
        public double NodeResidualMps;
        public double NodeResidualTolMps;    // tol
        public bool Drifting;                // the third disjunct, measured by the caller

        /// <summary>Which of Approach's four operations has been executed so far, 0..4. §B12.3's
        /// chain is ordered: Plane → Transfer → CourseCorrection → KillRelVel.</summary>
        public int ApproachStepsDone;
    }

    /// <summary>The pure decision core. One entry point, no state, no clock.</summary>
    public static class Conductor
    {
        /// <summary>§B12.3's Approach chain, in the plan's own order. Exposed so a test walks the real
        /// sequence rather than a copy of it.</summary>
        public static readonly ConductorOp[] ApproachChain =
        {
            ConductorOp.MatchPlane, ConductorOp.Transfer,
            ConductorOp.CourseCorrection, ConductorOp.KillRelVel
        };

        /// <summary>
        /// What to do this tick.
        ///
        /// ⛔ THE ORDER OF THESE TESTS IS THE SAFETY ORDER AND MUST NOT BE REARRANGED FOR TIDINESS.
        /// Abort outranks everything, including a gate hold: an abort raised while the crew are being
        /// asked for a GO must not be swallowed by the hold. Complete outranks the phase table so a
        /// finished plan cannot re-engage a module. A crew HOLD outranks every fly decision, because
        /// that is what a gate IS — §B12.3's "gated by telemetry + crew Go/No-Go where the real
        /// mission holds", and CrewGate's own rule that NO-GO holds rather than cancels.
        /// </summary>
        public static ConductorAction Decide(ConductorInputs s)
        {
            // 1. ABORT — outranks the gate, outranks the plan.
            if (s.Aborted)
                return ConductorAction.Of(ConductorVerb.Abort, ConductorModule.None, ConductorOp.None,
                                          "abort commanded");

            // 2. THE PLAN IS FINISHED. Nothing may re-engage after this.
            if (s.Complete)
                return ConductorAction.Of(ConductorVerb.Release, ConductorModule.None, ConductorOp.None,
                                          "mission complete — control released to the crew");

            // 3. A CREW GATE IS UP. Hold; do not fly past a question nobody has answered.
            if (s.Holding)
                return ConductorAction.Of(ConductorVerb.Hold, ConductorModule.None, ConductorOp.None,
                                          "holding at a crew gate — waiting for GO");

            // 4. THE CURRENT FLY STEP FINISHED. Advance before deciding anything about the next one:
            //    deciding first would engage a module for a phase the plan has already left.
            if (s.PhaseComplete)
                return ConductorAction.Of(ConductorVerb.Advance, ConductorModule.None, ConductorOp.None,
                                          "phase complete — advancing the plan");

            // 5. THE PHASE TABLE (§B12.3).
            switch (s.Phase)
            {
                case MissionPhase.Prelaunch:
                    return ConductorAction.Of(ConductorVerb.Engage, ConductorModule.AscentPvg,
                                              ConductorOp.None, "prelaunch — arm PVG");

                case MissionPhase.Ascent:
                    return ConductorAction.Of(ConductorVerb.Engage, ConductorModule.AscentPvg,
                                              ConductorOp.None, "PVG ascent");

                case MissionPhase.Coast:
                case MissionPhase.Phasing:
                    return PlanThenBurn(s, ConductorOp.Circularize, "coast/phasing");

                case MissionPhase.Approach:
                    return Approach(s);

                case MissionPhase.Docked:
                    return ConductorAction.Of(ConductorVerb.Engage, ConductorModule.SmartAss,
                                              ConductorOp.KillRot, "docked — holding attitude");

                case MissionPhase.Entry:
                    // §B12.3: the deorbit burn comes BEFOREHAND (OperationPeriapsis); once there is no
                    // node left to burn, the entry attitude is the job.
                    if (s.NodeExists && !s.NodeBurned)
                        return ConductorAction.Of(ConductorVerb.Engage, ConductorModule.NodeExecutor,
                                                  ConductorOp.Periapsis, "entry — burning the deorbit node");
                    return ConductorAction.Of(ConductorVerb.Engage, ConductorModule.SmartAss,
                                              ConductorOp.HeatShieldForward, "entry — heat shield forward");

                case MissionPhase.Drogues:
                case MissionPhase.Mains:
                    // §B12.3 gives these to chute triggering and the Manual Chute page, not to MechJeb.
                    return ConductorAction.Idle("under chutes — the conductor flies nothing here");

                case MissionPhase.Splashdown:
                case MissionPhase.Landed:
                    return ConductorAction.Of(ConductorVerb.Release, ConductorModule.None, ConductorOp.None,
                                              "splashdown — control released");

                default:
                    // ⛔ Unknown is NOT an error and must not engage anything. W10 made ActivePhase
                    // report Unknown for any phase no controller claims, so this is the normal reading
                    // for a phase this conductor does not fly.
                    return ConductorAction.Idle("no controller for this phase");
            }
        }

        /// <summary>Plan-then-burn: build the node if there is none, execute it if there is.
        /// §B12.3's "Maneuver-Planner … ops + Node Executor" is two modules and this is the order.</summary>
        static ConductorAction PlanThenBurn(ConductorInputs s, ConductorOp op, string why)
        {
            if (!s.NodeExists)
                return ConductorAction.Of(ConductorVerb.Engage, ConductorModule.ManeuverPlanner, op,
                                          why + " — planning " + op);
            if (!s.NodeBurned)
                return ConductorAction.Of(ConductorVerb.Engage, ConductorModule.NodeExecutor, op,
                                          why + " — executing the node");
            return ConductorAction.Of(ConductorVerb.Advance, ConductorModule.None, ConductorOp.None,
                                      why + " — node burned");
        }

        /// <summary>
        /// §B12.3's Approach + §B12.4's live re-plan.
        ///
        /// ⛔ THE KEEP-OUT SPHERE HAND-OFF IS TESTED FIRST, BEFORE THE OPERATION CHAIN. Inside the KOS
        /// the Docking AP is "the DEFAULT" (O6, §B10.3), and continuing to plan transfer burns next to
        /// a station because the chain has an unfinished step would be exactly wrong.
        /// </summary>
        static ConductorAction Approach(ConductorInputs s)
        {
            if (s.InKeepOutSphere)
            {
                // §B12.3: the crew's manual docking button overrides and shuts the Docking AP down.
                // Represented as an input; this core never initiates it.
                if (s.ManualDockingRequested)
                    return ConductorAction.Idle("inside the KOS — crew took manual docking");
                return ConductorAction.Of(ConductorVerb.Engage, ConductorModule.DockingAutopilot,
                                          ConductorOp.None, "inside the keep-out sphere — docking AP");
            }

            // §B12.4's re-plan rule, implemented as the plan states it and with the caller's numbers:
            //     closestApproachErr > εd  OR  residual > tol  OR  drift  ->  rebuild Operation k
            // ⛔ Checked BEFORE advancing the chain: a step whose burn missed is not a step that is
            // done, and advancing past it is how a rendezvous walks itself out of range.
            int k = s.ApproachStepsDone;
            if (k > 0 && k <= ApproachChain.Length)
            {
                ConductorOp last = ApproachChain[k - 1];
                if (s.ClosestApproachTolM > 0.0 && s.ClosestApproachErrM > s.ClosestApproachTolM)
                    return ConductorAction.Of(ConductorVerb.Replan, ConductorModule.ManeuverPlanner, last,
                                              "closest-approach error exceeds tolerance — rebuilding " + last);
                if (s.NodeResidualTolMps > 0.0 && s.NodeResidualMps > s.NodeResidualTolMps)
                    return ConductorAction.Of(ConductorVerb.Replan, ConductorModule.ManeuverPlanner, last,
                                              "node residual exceeds tolerance — rebuilding " + last);
                if (s.Drifting)
                    return ConductorAction.Of(ConductorVerb.Replan, ConductorModule.ManeuverPlanner, last,
                                              "drifting — rebuilding " + last);
            }

            if (k >= ApproachChain.Length)
                return ConductorAction.Of(ConductorVerb.Advance, ConductorModule.None, ConductorOp.None,
                                          "approach chain complete — awaiting the keep-out sphere");

            return PlanThenBurn(s, ApproachChain[k], "approach step " + (k + 1) + "/" + ApproachChain.Length);
        }
    }
}
