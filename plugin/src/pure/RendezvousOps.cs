// DragonScreen — RendezvousOps  (PURE: the on-orbit leg geometry, the op parameters, §B12.4's numbers)
// ============================================================================================
// Register T19, §B9 Phase 2-3 / §B10.2 / §B11 / §B12.4. `pure/Conductor.cs` (T16) already owns the
// rendezvous DECISION — the Plane → Transfer → CourseCorrection → KillRelVel chain and §B12.4's
// re-plan rule — and it is mutation-proven. What it deliberately does NOT own is any NUMBER: its own
// header says so, and its tolerances arrive on `ConductorInputs` from the caller.
//
// ⭐ THIS FILE IS THAT CALLER'S HALF. Which waypoint a leg is walking to, how far the intercept ladder
// should reach on this pass, when a leg is finished, and what εd / tol / "drifting" actually mean in
// metres and m/s. All of it is §B11's published approach geometry, cited line by line, and none of it
// is a threshold somebody typed.
//
// ---- WHERE THE GEOMETRY COMES FROM (§B11, tagged [DOC] in the plan itself) ----
//   "Approach Ellipsoid = **4 × 2 km** egg zone. Dragon halts at **~1 km** for the Go/No-Go before the
//    Keep-Out Sphere. Keep-Out Sphere ≈ **200 m**. Waypoints: **WP0 = 400 m below**, **WP1 (docking
//    axis) = 220 m ahead**, **WP2 = 20 m** from the port. → OperationCourseCorrection
//    `intercept_distance` ladder (4 km → 1 km → 220 m → 20 m) and the waypoint sequence."
// and the gate catalog `pure/CrewGates.cs` names the same three waypoints in its own titles —
// "HOLD — WP0 (400 m below)", "HOLD — WP1 (~220 m on the V-bar)", "HOLD — WP2 (20 m)". The two agree,
// which is why the ladder can be read off the gate the leg is walking toward.
//
// ---- ⭐ HOW A LEG KNOWS WHICH ONE IT IS: THE SEAM ALREADY EXISTS ----
// `pure/ModeManager.cs`'s plan has THREE `Fly(Approach)` steps separated by the WP0/WP1/WP2 gates, and
// `CrewProcedureOps.NextGateId` was written for exactly this — its own comment reads *"lets a flying
// controller know which leg it is on (e.g. the docking approach leg toward WP0/WP1/WP2 is identified
// by the gate it leads to)"*. So the leg is a function of the next gate, and nothing here has to count.
//
// ---- ⛔ WHAT IS **NOT** DECIDED HERE ----
// The op chain's ORDER (that is `Conductor.ApproachChain`), the re-plan RULE (that is
// `Conductor.Approach`), and anything that touches a vessel. This file is geometry and thresholds.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>
    /// ⭐ S219 JOB 2 — WHICH RENDEZVOUS DRIVER IS FLYING. A SELECTABLE MODE, because the owner asked
    /// for one and because both drivers are real.
    ///
    /// Owner, 2026-09-07, verbatim (an `OVERRIDE` of §B1/§B12.4's default, C1.8):
    ///     *"mechjeb rendezvous autopilot just for now to get things moving… Then we move to the more
    ///      complicated, mission accurate fidelity way"*
    ///
    /// ⛔ NEITHER PATH IS DELETED, and the brief is explicit: *"DO NOT DELETE the node-composing path
    /// (§B10.1/§B12.4) — add this as a selectable mode and mark the other SUPERSEDED-FOR-NOW. He is
    /// coming back to it."*
    /// </summary>
    public enum RendezvousDrive : byte
    {
        /// <summary>⛔ **SUPERSEDED-FOR-NOW, 2026-09-07, NOT retired.** §B1/§B12.4's design: the conductor
        /// composes MechJeb's Maneuver-Planner operations itself, flies them one node at a time through
        /// the Node Executor, and re-plans when a burn missed its intent. It walks §B11's real waypoint
        /// ladder (WP0 400 m → WP1 220 m → WP2 20 m) with a crew gate at each rung, which is the
        /// "mission accurate fidelity way" the owner is coming back to. Every line of it still stands.</summary>
        Conductor = 0,

        /// <summary>⭐ THE OWNER'S CHOICE FOR NOW. MechJeb's own `MechJebModuleRendezvousAutopilot` —
        /// `docs/MECHJEB_MASTER_MAP.md` §8's eight-branch decision tree — flown to the Keep-Out Sphere
        /// and handed to the Docking Autopilot there. ⚠ IT DOES NOT WALK §B11's WAYPOINTS: it goes
        /// phasing → Hohmann → intercept → match velocities and stops at `desiredDistance`. That is the
        /// fidelity the owner has knowingly traded for motion.</summary>
        MechJebAutopilot = 1
    }

    /// <summary>Which on-orbit leg the conductor is flying, identified by the gate it ends at.</summary>
    public enum RendezvousLeg : byte
    {
        /// <summary>Not an on-orbit leg the conductor flies.</summary>
        None = 0,
        /// <summary>§B9 Phase 2 — insertion trim + the phasing orbit, out to the G9 Approach-Initiation poll.</summary>
        Phasing,
        /// <summary>→ gate G10. §B11's WP0, 400 m below.</summary>
        ToWaypoint0,
        /// <summary>→ gate G11. §B11's WP1, ~220 m on the V-bar.</summary>
        ToWaypoint1,
        /// <summary>→ gate G12. §B11's WP2, 20 m from the port — the last leg before the docking hand-off.</summary>
        ToWaypoint2
    }

    /// <summary>§B11's approach geometry, and the numbers §B12.4's rule is judged against.</summary>
    public static class RendezvousOps
    {
        // ── §B11's published geometry. Every one of these is a [DOC] figure in the plan. ──────────

        /// <summary>The Approach Ellipsoid's long axis — §B11 "Approach Ellipsoid = 4 × 2 km egg zone".
        /// The first rung of the intercept ladder.</summary>
        public const double ApproachEllipsoidM = 4000.0;

        /// <summary>§B11 "Dragon halts at ~1 km for the Go/No-Go before the Keep-Out Sphere". The second
        /// rung.</summary>
        public const double GoNoGoHoldM = 1000.0;

        /// <summary>§B11 "Keep-Out Sphere ≈ 200 m". ⭐ THE HAND-OFF RANGE: inside this the Docking
        /// Autopilot is the DEFAULT (O6 / §B10.3 / §B12.3), and `Conductor.Approach` tests it FIRST,
        /// before the operation chain, so a transfer burn can never be planned next to the station.</summary>
        public const double KeepOutSphereM = 200.0;

        /// <summary>§B11 "WP0 = 400 m below"; `CrewGates` G10 "HOLD — WP0 (400 m below)".</summary>
        public const double Waypoint0M = 400.0;
        /// <summary>§B11 "WP1 (docking axis) = 220 m ahead"; `CrewGates` G11 "~220 m on the V-bar".</summary>
        public const double Waypoint1M = 220.0;
        /// <summary>§B11 "WP2 = 20 m from the port"; `CrewGates` G12 "HOLD — WP2 (20 m)".</summary>
        public const double Waypoint2M = 20.0;

        /// <summary>
        /// The relative speed at which velocities count as MATCHED at a waypoint hold.
        /// ⚠ **A BORROWED FIGURE, AND SAID SO.** §B11's tightest documented rate is *"rate must stay
        /// **&lt; 0.2 m/s inside 5 m** range"* — a DOCKING-CORRIDOR limit, not a definition of
        /// station-keeping. It is used here because it is the only published number of the right kind
        /// and it is certainly conservative at 400 m, where the real vehicle is slower still. T22
        /// converges it against a flown approach; the caller can override it meanwhile.
        /// </summary>
        public const double NulledRelativeSpeedMps = 0.2;

        /// <summary>
        /// §B10.1's Node-Executor cutoff: *"MechJeb stock default **0.1** … Target: ~0.1 for big burns"*.
        /// This is the residual §B12.4 compares against on the phasing and transfer burns.
        /// </summary>
        public const double NodeResidualToleranceMps = 0.1;

        /// <summary>
        /// §B10.1 again: *"tighten toward **0.05** for the fine rendezvous corrections (smaller = more
        /// precise, too small = chases engine/RCS noise and won't converge)"*. Used from WP0 inward.
        /// </summary>
        public const double FineNodeResidualToleranceMps = 0.05;

        // ── the leg ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Which leg the conductor is on, from the gate it is walking toward
        /// (`CrewProcedureOps.NextGateId`). ⛔ Anything else is `None`, and a `None` leg flies nothing —
        /// the conductor must never guess which waypoint it is aiming at.
        /// </summary>
        public static RendezvousLeg LegFor(GateId nextGate)
        {
            switch (nextGate)
            {
                case GateId.ApproachInitGoG9: return RendezvousLeg.Phasing;
                case GateId.WP0HoldG10:       return RendezvousLeg.ToWaypoint0;
                case GateId.WP1HoldG11:       return RendezvousLeg.ToWaypoint1;
                case GateId.WP2DockGoG12:     return RendezvousLeg.ToWaypoint2;
                default:                      return RendezvousLeg.None;
            }
        }

        /// <summary>
        /// ⭐⭐ S219 JOB 2 — **DOES A NODE BURN ON THIS VEHICLE HAVE TO RUN ON RCS?**
        ///
        /// `docs/MECHJEB_MASTER_MAP.md` §8's closing note: *"it drives via **maneuver nodes +
        /// NodeExecutor**, so a Dragon on Dracos executes on RCS (`RCSOnly`)."* That is not a
        /// refinement — on this craft it is the difference between a burn and a hang.
        ///
        /// ⛔ READ OFF THE CRAFT FILE (`docs/reference/Crew-2.craft`): once the Dragon has separated,
        /// the **only `ModuleEnginesRF` left on the vessel is on `TE.18.DRAGONV2.POD` — the
        /// SuperDracos**, which `Actuation.EngineRoleOf` classifies `EngineRole.PodAbort`. The Dracos
        /// that actually fly a rendezvous are `ModuleRCSFX`. So a Node Executor left at `RCSOnly = false`
        /// commands `mainThrottle` against an ABORT motor that is not ignited: `VesselState`'s engine
        /// walk skips it (`AddNewEngine` returns on `!e.EngineIgnited`), `ThrustAvailable` is zero, and
        /// the executor has no thrust to compute a burn time from. **Every on-orbit and deorbit burn
        /// hangs, silently, with the vehicle pointed correctly and nothing happening** — which is the
        /// exact shape of the owner's *"sit there ready to go but do nothing"*.
        ///
        /// ⚠ It is deliberately a function of a MEASURED fact — is there a main engine on this vessel
        /// right now — and not of a mission phase, because the phase is a plan and the engine is a part.
        /// </summary>
        public static bool NodeBurnsOnRcs(bool hasMainEngine) { return !hasMainEngine; }

        /// <summary>
        /// ⭐ §8 / §B11 — WHERE MECHJEB'S RENDEZVOUS AUTOPILOT MUST STOP AND HAND OVER.
        /// Its own default is 100 m, which is **inside** §B11's 200 m Keep-Out Sphere — so left alone it
        /// would fly the Dragon through the KOS on maneuver nodes, which §B11 forbids and which
        /// `Conductor.Approach` already refuses to plan. The hand-off range is the published KOS, so
        /// this is a MISSION FACT and not a tune: inside it the Docking Autopilot is the DEFAULT
        /// (O6 / §B10.3 / §B12.3).
        /// </summary>
        public static double AutopilotHandoffRangeM { get { return KeepOutSphereM; } }

        /// <summary>The range this leg is walking to, metres. `Phasing` has no range target — it ends on
        /// its orbit, not on a distance — and returns 0.</summary>
        public static double TargetRangeM(RendezvousLeg leg)
        {
            switch (leg)
            {
                case RendezvousLeg.ToWaypoint0: return Waypoint0M;
                case RendezvousLeg.ToWaypoint1: return Waypoint1M;
                case RendezvousLeg.ToWaypoint2: return Waypoint2M;
                default:                        return 0.0;
            }
        }

        /// <summary>
        /// §B10.2's `intercept_distance` for THIS pass: *"set to the 4 km Approach-Ellipsoid then walk
        /// down **on later passes**"* — §B11's ladder 4 km → 1 km → the waypoint.
        ///
        /// ⛔ THE RUNG IS CHOSEN BY THE PASS COUNT, NOT BY THE CURRENT RANGE, AND THAT IS A CORRECTION.
        /// A range-driven ladder STALLS at its own rung boundary, and `MissionWalkTest`'s closed-loop
        /// rendezvous is what found it: a correction aiming at the 4 km rung that lands at 4.3 km is
        /// still outside 4 km, so the next pass picks the same rung, lands in the same place, and the
        /// approach never descends — while every individual burn looks fine and every error is inside
        /// tolerance. §B10.2's own words are "on later passes", so the pass count is what drives it, and
        /// the ladder then converges by construction.
        ///
        /// Two guards, both one-directional:
        ///  • never aim FURTHER OUT than where the vehicle already is — a leg that starts inside a rung
        ///    must not be told to fly back out to it;
        ///  • never aim INSIDE the leg's own waypoint — that is the next leg's job, and the next gate's.
        /// </summary>
        /// <param name="pass">How many times this leg has already walked the chain. 0 on the first.</param>
        public static double InterceptDistanceM(RendezvousLeg leg, double currentRangeM, int pass)
        {
            double target = TargetRangeM(leg);
            double rung = pass <= 0 ? ApproachEllipsoidM
                        : pass == 1 ? GoNoGoHoldM
                        : target;
            if (currentRangeM > 0.0 && rung > currentRangeM) rung = currentRangeM;
            return rung < target ? target : rung;
        }

        /// <summary>
        /// ⭐ THE HAND-OFF TEST. Inside the Keep-Out Sphere the Docking Autopilot is the DEFAULT
        /// (O6, owner 2026-09-03; §B10.3 / §B12.3) and the planner chain stops. `Conductor.Approach`
        /// consumes this through `ConductorInputs.InKeepOutSphere`.
        /// </summary>
        public static bool InsideKeepOutSphere(double rangeM)
        {
            return rangeM > 0.0 && rangeM <= KeepOutSphereM;
        }

        /// <summary>
        /// Has this leg arrived? Inside its waypoint — to within the SAME tolerance §B12.4 judges a
        /// missed burn by — AND with the relative velocity matched.
        ///
        /// ⛔ BOTH, NOT EITHER. Arriving at 400 m still closing at 3 m/s is not a hold, it is a
        /// collision in ninety seconds; and a matched velocity two kilometres out is not an arrival.
        ///
        /// ⭐⭐ AND THE TOLERANCE IS εd, DELIBERATELY THE SAME NUMBER — THIS IS WHAT MAKES THE APPROACH
        /// CONVERGE, AND IT WAS FOUND BY FAILING TO. With an EXACT arrival test (`range <= target`) and
        /// a separate εd, a course correction aiming at the 400 m hold that lands at 429 m is (a) not a
        /// miss, because 29 m is well inside εd, so §B12.4 does not re-plan it, and (b) not an arrival,
        /// because 429 &gt; 400 — so the leg walks the chain again, aims at 400 m again, lands at 429 m
        /// again, and does that forever while every individual burn and every error reads nominal.
        /// `MissionWalkTest`'s closed-loop rendezvous stalled there twice before this was coupled.
        ///
        /// Coupling them states one operational rule instead of two half-rules: **a miss small enough
        /// not to be worth re-planning IS an arrival.** In metres that is "within one waypoint-radius
        /// of the waypoint" — inside 800 m for the 400 m hold, inside 440 m for WP1. ⚠ That is loose,
        /// and it is loose ON PURPOSE for a first flight at RO defaults: the NEXT leg tightens it by an
        /// order of magnitude, and the Keep-Out Sphere hand-off is a hard 200 m test that no tolerance
        /// widens. T22 converges εd against a flown approach, and both halves move together.
        /// </summary>
        public static bool LegComplete(RendezvousLeg leg, double rangeM, double relSpeedMps,
                                       double nulledSpeedMps)
        {
            double target = TargetRangeM(leg);
            if (target <= 0.0) return false;              // Phasing does not end on a range
            if (nulledSpeedMps <= 0.0) nulledSpeedMps = NulledRelativeSpeedMps;
            double arrive = target + ClosestApproachToleranceM(leg);
            return rangeM > 0.0 && rangeM <= arrive && relSpeedMps <= nulledSpeedMps;
        }

        /// <summary>What a leg does when its operation chain has run out of steps.</summary>
        public enum ChainEnd : byte
        {
            /// <summary>The leg is finished — advance the mission plan and raise its gate.</summary>
            CompleteLeg,
            /// <summary>Run the chain again, one rung further down the intercept ladder.</summary>
            AnotherPass
        }

        /// <summary>
        /// ⭐ WHAT HAPPENS WHEN `Conductor` SAYS "approach chain complete" BUT THE LEG HAS NOT ARRIVED.
        ///
        /// Two different legs, two different answers, and getting this wrong strands a flight either
        /// way — a phasing leg that loops forever never raises its gate, and an approach leg that ends
        /// on the first pass raises a WP0 hold from four kilometres out.
        ///
        ///  • **Phasing** (§B9 Phase 2, "Insertion trim &amp; phasing setup") ends on its ORBIT, not on a
        ///    range. Its chain IS the leg: once the trim burn is flown there is nothing more to do, and
        ///    the G9 Approach-Initiation poll is the next thing that should happen.
        ///  • **An approach leg** ends on a RANGE, and one pass of the chain does not get there from a
        ///    phasing orbit. §B10.2 says so in as many words about the course correction — *"Run 1-2x to
        ///    walk the closest approach down"* — so the chain runs again, and
        ///    <see cref="InterceptDistanceM"/> gives it the next rung down because the PASS has advanced.
        ///
        /// ⛔ An arrived leg never reaches this decision: arrival is `LegComplete`, which the caller
        /// feeds to `Conductor` as `PhaseComplete`, and that is tested BEFORE the phase table.
        /// </summary>
        public static ChainEnd OnChainComplete(RendezvousLeg leg)
        {
            return leg == RendezvousLeg.Phasing ? ChainEnd.CompleteLeg : ChainEnd.AnotherPass;
        }

        // ── §B12.4's numbers ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// εd — the closest-approach error above which §B12.4 rebuilds the operation.
        ///
        /// ⭐ DERIVED FROM THE LEG, NOT CHOSEN: **εd = the leg's own target range.** A predicted closest
        /// approach that misses the waypoint by more than the waypoint's own distance has not got a
        /// small error, it has the wrong plan. That scales correctly by construction — 400 m of slack
        /// at WP0, 20 m at WP2 — and it means no number is invented for a quantity §B12.4 states only
        /// as a symbol. ⭐ <see cref="LegComplete"/> uses the SAME number as its arrival band, and
        /// that coupling is what makes the approach converge — see the argument there.
        /// ⚠ Legs with no range target (`Phasing`) return **0**, which `Conductor`'s own
        /// rule reads as "this disjunct is off" (its test: *"an UNSET tolerance disables the check
        /// rather than failing everything"*).
        /// </summary>
        public static double ClosestApproachToleranceM(RendezvousLeg leg)
        {
            return TargetRangeM(leg);
        }

        /// <summary>
        /// tol — the node residual above which §B12.4 rebuilds. §B10.1's two values: the stock 0.1 for
        /// the big burns, tightened to 0.05 "for the fine rendezvous corrections", which is what every
        /// leg from WP0 inward is.
        /// </summary>
        public static double NodeResidualToleranceFor(RendezvousLeg leg)
        {
            return leg == RendezvousLeg.Phasing || leg == RendezvousLeg.None
                 ? NodeResidualToleranceMps
                 : FineNodeResidualToleranceMps;
        }

        /// <summary>
        /// §B12.4's third disjunct, which the plan leaves to the caller to measure: *"or drift"*.
        ///
        /// The rule: the range is OPENING, faster than the rate at which velocities count as matched,
        /// while there is still range to close. ⭐ It reuses <see cref="NulledRelativeSpeedMps"/>
        /// rather than introducing a second threshold — a vehicle receding faster than it is allowed to
        /// close is, by the only published figure available, not station-keeping.
        /// </summary>
        /// <param name="openingRateMps">Positive = the range is growing. The caller smooths it.</param>
        public static bool Drifting(double openingRateMps, double rangeM, double targetRangeM,
                                    double tolMps)
        {
            if (tolMps <= 0.0) tolMps = NulledRelativeSpeedMps;
            if (targetRangeM <= 0.0) return false;         // no range target -> drift is undefined
            return rangeM > targetRangeM && openingRateMps > tolMps;
        }
    }

    /// <summary>
    /// The MechJeb `Operation` CLASS NAMES behind each `ConductorOp`, and the `TimeReference` each one
    /// must schedule against.
    ///
    /// ---- ⛔ WHY THIS EXISTS: ONE OF THE SEVEN NAMES IS NOT WHAT THE PLAN CALLS IT ----
    /// §B10.2 and `pure/ConductorAction.cs` both name **`OperationTransfer`**, and ConductorAction's
    /// header records that all seven were "confirmed by `find` under `plugin/mech/`". That `find` was a
    /// FILE-name check and it is right for six of the seven. **The file `OperationTransfer.cs` declares
    /// a class called `OperationGeneric`** (`plugin/mech/MechJeb2/Maneuver/OperationTransfer.cs:17`,
    /// `grep "public class Operation"` over that directory). No class named `OperationTransfer` exists
    /// in the pinned tree. ⚠ **SUPERSEDED IN PLACE, NOT DELETED** (C1.16): ConductorAction's claim is
    /// corrected here rather than edited out of it, and §B10.2's own ⚠ anticipated exactly this —
    /// *"Verify exact C# class names vs the pinned MechJeb source when embedding."*
    ///
    /// ---- ⛔ AND WHY THE TimeReference IS PINNED RATHER THAN SET ----
    /// Every `Operation` holds its `TimeSelector` in a **`private static readonly`** field, so the
    /// conductor cannot set one without reflecting into a vendored type. It does not need to: each
    /// selector's FIRST allowed reference is its default (`_currentTimeRef` starts at 0), and for every
    /// operation the conductor uses that default is already the one §B9/§B10.2 names. So the conductor
    /// sets nothing and `RendezvousOpsTest` pins the defaults against the vendored source itself — a
    /// re-pin that reorders one fails the build instead of quietly rescheduling a burn.
    /// </summary>
    public static class MechOps
    {
        /// <summary>The vendored class that backs a `ConductorOp`, or null where the op is a SmartASS
        /// attitude rather than a planner operation.</summary>
        public static string ClassFor(ConductorOp op)
        {
            switch (op)
            {
                case ConductorOp.Circularize:      return "OperationCircularize";
                case ConductorOp.Periapsis:        return "OperationPeriapsis";
                case ConductorOp.Apoapsis:         return "OperationApoapsis";
                case ConductorOp.MatchPlane:       return "OperationPlane";
                // ⛔ NOT `OperationTransfer` — see this class's header.
                case ConductorOp.Transfer:         return "OperationGeneric";
                case ConductorOp.CourseCorrection: return "OperationCourseCorrection";
                case ConductorOp.KillRelVel:       return "OperationKillRelVel";
                default:                           return null;   // KillRot / HeatShieldForward / None
            }
        }

        /// <summary>The vendored FILE each class lives in, relative to `plugin/mech/MechJeb2/Maneuver/`.
        /// Separate from <see cref="ClassFor"/> precisely because for Transfer the two disagree.</summary>
        public static string FileFor(ConductorOp op)
        {
            return op == ConductorOp.Transfer ? "OperationTransfer.cs"
                 : ClassFor(op) == null ? null : ClassFor(op) + ".cs";
        }

        /// <summary>
        /// The `TimeReference` §B9/§B10.2 names for each operation — which must be the FIRST entry of
        /// that operation's own `_timeReferences` array, because that is the default the conductor
        /// relies on. Null where the plan names none.
        /// </summary>
        public static string TimeReferenceFor(ConductorOp op)
        {
            switch (op)
            {
                // §B10.2: "no params; TimeSelector (apoapsis/periapsis/altitude/computed)". Apoapsis is
                // the default and is the right one for an insertion clean-up.
                case ConductorOp.Circularize:      return "APOAPSIS";
                // Burn at apoapsis to move the periapsis — the efficient node for both the phasing
                // shaping (P2) and the deorbit Pe (P7).
                case ConductorOp.Periapsis:        return "APOAPSIS";
                // ...and at periapsis to move the apoapsis.
                case ConductorOp.Apoapsis:         return "PERIAPSIS";
                // §B9: "OperationPlane (no params, TimeSelector = relative ascending/descending node)".
                // REL_HIGHEST_AD is the cheaper of the two nodes, which is §B9's "kill the plane error
                // early and cheap".
                case ConductorOp.MatchPlane:       return "REL_HIGHEST_AD";
                // §B9: "TimeSelector `computed` (optimum) for the transfer to intercept".
                case ConductorOp.Transfer:         return "COMPUTED";
                // §B10.2's course correction is scheduled COMPUTED too.
                case ConductorOp.CourseCorrection: return "COMPUTED";
                // §B10.2: "no params; TimeSelector `closest_approach`".
                case ConductorOp.KillRelVel:       return "CLOSEST_APPROACH";
                default:                           return null;
            }
        }

        /// <summary>Every op the conductor actually builds a node from — the set the tests sweep.</summary>
        public static readonly ConductorOp[] PlannerOps =
        {
            ConductorOp.Circularize, ConductorOp.Periapsis, ConductorOp.Apoapsis,
            ConductorOp.MatchPlane, ConductorOp.Transfer, ConductorOp.CourseCorrection,
            ConductorOp.KillRelVel
        };
    }
}
