/*
 * DragonScreen - DockApproach
 *
 * PURE. WHICH POINT THE CAPSULE IS FLYING AT, and whether it is allowed to start the final run.
 *
 * ---- ⛔ WHY THIS FILE EXISTS: THE TESTED LAYER WAS NOT THE BROKEN LAYER ----
 * `DockControlTest.Converge` flies the real servo for 40 000 steps and passes 4704 checks, and
 * docking has never once succeeded in flight. The test feeds `DistF/S/T` measured STRAIGHT TO THE
 * PORT; the flight code feeds the distance to whichever WAYPOINT the stage machine currently wants.
 * Every failure has been in the waypoint choice, and the waypoint choice lived in KSP glue where no
 * test could reach it. So the decision is here, in scalars, and both the flight code and the
 * simulator call it - AND THE SIMULATOR MUST START WHERE THE RENDEZVOUS ACTUALLY HANDS OVER.
 *
 * ---- ⛔ SIMPLIFIED 2026-08-17: THE LVLH "L" IS GONE. gate -> standoff -> axial. ----
 * The 2026-08-13 rebuild flew the real Crew-Dragon L: WP0 400 m BELOW the station, WP1 220 m out on
 * the axis, WP2 20 m, then the port, in the station's LVLH frame. It PASSED headless and EMPTIED THE
 * TANK IN FLIGHT (flight_0814_172345.csv: 54 units, never past `Corridor`, `x_ctlZ` slamming +/-0.5).
 * The reason is the handover: the L assumes the capsule arrives BEHIND AND BELOW - which is exactly
 * where the test started it, `V(-900,-500,0)` - but `StationApproach.Arrived()` hands over at ~60 m,
 * co-orbital, IN FRONT of the port. That close, `axialM` oscillates about 0 inside the keep-out
 * sphere: the wrong-side branch commanded a back-out while `Wp1Transit` commanded a point 400 m
 * BELOW, and the target flipped every few ticks.
 *
 * A gate -> standoff -> axial profile has no such assumption: every point it flies to is NEAR the
 * port (gate ~ standoff ~ 25 m, then the port), so it converges from wherever it is handed the
 * vehicle. This is `docs/DOCKING_REBUILD_PLAN.md` Phase 1 - keep the gate/corridor/standoff profile,
 * keep the ported `DockControl` velocity inner loop, drop the L.
 *
 * ---- WHERE THE RULES COME FROM ----
 *   · COMMIT AT 1 m OF LATERAL, NOT 15. `MechJebModuleDockingAutopilot.cs:59`,
 *     `dockingcorridorRadius = 1`. The axial run may only START when the lateral is inside
 *     `CorridorRadiusM`, so the capsule is lined up BEFORE it closes rather than nulling a large
 *     offset while closing. This also replaces the self-falsifying `AtStandoff` predicate - see the
 *     note on the commit test in `Select`, and `DockGeometry`'s removed `AtStandoff`.
 *   · WRONG SIDE IS A STATE, NOT AN ACCIDENT. If our port is behind theirs we go ROUND to the front;
 *     we never reverse straight through the station.
 *
 * ---- AND THE STAGE MACHINE IS MONOTONE ----
 * Every proximity test falsifies itself by succeeding, so a stage may only ever advance. `reached`
 * is the high-water mark; pass the previous result back in. See `Rank`.
 */
namespace DragonScreen
{
    // ---- THE STAGE. A plain enum with no KSP in it, so the layer that DECIDES it is testable. ----
    public enum DockStage : byte
    {
        Idle = 0,
        /// <summary>No usable pair of ports. Says so rather than flying at the hull.</summary>
        NoPort,
        /// <summary>Range raised, waiting for KSP to load the station so its ports can be read.</summary>
        AwaitingTarget,
        /// <summary>Flying to the gate - the point where the port axis leaves the keep-out sphere.</summary>
        ToGate,
        /// <summary>The direct path cuts the hull; sliding round the sphere instead.</summary>
        Rounding,
        /// <summary>
        /// At the gate and running IN along the port axis to the standoff, lining up as it goes.
        ///
        /// This is the approach-corridor transit: hold outside the keep-out sphere, then enter along
        /// the port axis to a close hold, rather than closing on the station from wherever it happens
        /// to be.
        /// </summary>
        Corridor,
        /// <summary>Lined up on the axis and running straight down it to contact.</summary>
        Axial,
        Docked
    }

    /// <summary>Where the capsule should be flying, this tick.</summary>
    public enum DockWaypoint : byte
    {
        /// <summary>No usable geometry.</summary>
        None = 0,
        /// <summary>Round the keep-out sphere - the direct path cuts the hull.</summary>
        Skirt,
        /// <summary>The point where the port axis leaves the keep-out sphere.</summary>
        Gate,
        /// <summary>On the axis, `StandoffM` out from the port.</summary>
        Standoff,
        /// <summary>The port itself. Only once lined up inside the corridor.</summary>
        Port
    }

    /// <summary>Everything the choice depends on, in metres. No vectors - see the file header.</summary>
    public struct DockApproachInputs
    {
        public bool Valid;
        /// <summary>Along the port axis, POSITIVE when we are in front of the port.</summary>
        public double AxialM;
        /// <summary>Perpendicular distance from the port axis. Always positive.</summary>
        public double LateralM;
        /// <summary>Straight-line distance to the standoff point.</summary>
        public double ToStandoffM;
        /// <summary>Straight-line distance to the gate.</summary>
        public double ToGateM;
        /// <summary>Does the straight run to the gate clear the keep-out sphere?</summary>
        public bool PathClear;
        /// <summary>Half the docking node's own `acquireRange` - magnets take it from here.</summary>
        public double AcquireM;
        /// <summary>Station bounding radius plus ours, metres. Sets the wrong-side clearance.</summary>
        public double SafeM;
    }

    public struct DockApproachResult
    {
        public DockWaypoint Waypoint;
        public DockStage Stage;
        public bool Captured;
        public string Note;
    }

    public static class DockApproach
    {
        /// <summary>
        /// Lateral error permitted before the final run may START, metres.
        ///
        /// ⛔ `MechJebModuleDockingAutopilot.cs:59`, `dockingcorridorRadius = 1`. The axial run is
        /// committed only when the lateral is inside this, so the capsule is lined up before it
        /// closes. Ours committed on `GateToleranceM` 15 m and `StandoffToleranceM` 12 m and entered
        /// `Axial` up to fifteen metres off the axis, then tried to null that while closing.
        /// </summary>
        [Tunable] public static double CorridorRadiusM = 1.0;

        /// <summary>Lateral error that sends the final run back to re-line-up. Hysteresis, not a
        /// second value.</summary>
        [Tunable] public static double CorridorAbortM = 2.0;

        /// <summary>How far in front of the port the capsule lines up before the axial run. Follows
        /// the (tunable) `DockGeometry.StandoffM` so the two can never disagree.</summary>
        public static double StandoffM { get { return DockGeometry.StandoffM; } }

        /// <summary>Stage ordering. ToGate and Rounding share a rank - rounding is a curve, not a
        /// milestone.</summary>
        public static int Rank(DockStage s)
        {
            switch (s)
            {
                case DockStage.ToGate:
                case DockStage.Rounding: return 1;   // transit to the gate
                case DockStage.Corridor: return 2;   // gate -> standoff, lining up
                case DockStage.Axial:    return 3;   // straight down the axis to contact
                case DockStage.Docked:   return 4;
                default: return 0;
            }
        }

        /// <summary>
        /// Choose the waypoint. `reached` is the high-water mark; pass the previous result back in.
        /// </summary>
        public static DockApproachResult Select(DockApproachInputs s, DockStage reached)
        {
            DockApproachResult r = new DockApproachResult();
            r.Stage = reached;

            if (!s.Valid)
            {
                r.Waypoint = DockWaypoint.None;
                r.Note = "no geometry";
                return r;
            }

            // ---- CAPTURED ----
            // ⛔ THE AXIAL TEST IS TWO-SIDED. `AxialM <= AcquireM` alone is true for every NEGATIVE
            // axial too, so a capsule that flew straight through the station reports a capture.
            // ⛔ AND THE LATERAL TOLERANCE IS THE PORT'S OWN CAPTURE ENVELOPE, NOT THE CORRIDOR.
            // Declaring capture means "stop thrusting and let the magnets take it", and a metre off
            // centre the magnets do not reach. `AcquireM` is `theirPort.acquireRange * 0.5` - the
            // port's own number - so the final run keeps trimming until the capsule is inside it,
            // which is the whole point of trimming to contact.
            if (s.AxialM <= s.AcquireM && s.AxialM > -s.AcquireM
                && s.LateralM <= s.AcquireM)
            {
                r.Captured = true;
                r.Stage = DockStage.Docked;
                r.Waypoint = DockWaypoint.Port;
                r.Note = "CAPTURE";
                return r;
            }

            // ---- WRONG SIDE falls through to the gate logic below, NOT a special branch. ----
            // Behind the port (axial < 0) the gate is in FRONT of the port, so the normal
            // "fly to the gate if clear, else round the hull" leg already pulls the capsule to the
            // front and round the station - no reversing through it. The ONE thing that must not
            // happen is the axial commit firing while behind: `axial <= StandoffM` is trivially true
            // for every negative axial, so the commit is guarded with `axial > 0` below.

            // ================= THE NOMINAL PROFILE: gate -> standoff -> axial =================
            // ⚠ MONOTONE. `reached` only advances; a stage falsifies its own test by succeeding.

            // ---- AXIAL: lined up, run straight in. ----
            // ⛔ THE COMMIT TEST IS LATERAL ALIGNMENT, NOT PROXIMITY TO THE STANDOFF POINT. The old
            // `AtStandoff` - "within 12 m of a point 25 m out" - is FALSE at 13 m from the port, so
            // the axial entry failed BECAUSE THE AXIAL RUN HAD WORKED. That was the 13 m stall. Here
            // the run starts once the lateral is inside `CorridorRadiusM` and we are at or inside the
            // standoff axially, and `reached` latches it - it cannot self-falsify. `DockControl`
            // already slows the closure while a lateral remains, so committing at 1 m is safe.
            if (Rank(reached) >= Rank(DockStage.Axial)
                || (s.AxialM > 0.0 && s.AxialM <= StandoffM && s.LateralM <= CorridorRadiusM))
            {
                r.Stage = DockStage.Axial;
                if (s.LateralM > CorridorAbortM)
                {
                    // Lateral blew out past the abort band mid-run: re-line-up on the standoff
                    // rather than crabbing into the port. Stays `Axial` rank (monotone).
                    r.Waypoint = DockWaypoint.Standoff;
                    r.Note = "OFF THE AXIS - re-lining up, lateral " + s.LateralM.ToString("F1") + " m";
                    return r;
                }
                r.Waypoint = DockWaypoint.Port;
                r.Note = "AXIAL - " + s.AxialM.ToString("F1") + " m";
                return r;
            }

            // ---- CORRIDOR: at the gate, run in along the port axis to the standoff. ----
            if (Rank(reached) >= Rank(DockStage.Corridor) || DockGeometry.AtGate(s.ToGateM))
            {
                r.Waypoint = DockWaypoint.Standoff;
                r.Stage = DockStage.Corridor;
                r.Note = "CORRIDOR - " + s.ToStandoffM.ToString("F0") + " m to the standoff";
                return r;
            }

            // ---- TO THE GATE: straight there if the path is clear, else slide round the hull. ----
            if (s.PathClear)
            {
                r.Waypoint = DockWaypoint.Gate;
                r.Stage = DockStage.ToGate;
                r.Note = "TO GATE - " + s.ToGateM.ToString("F0") + " m";
                return r;
            }
            r.Waypoint = DockWaypoint.Skirt;
            r.Stage = DockStage.Rounding;
            r.Note = "ROUNDING HULL";
            return r;
        }
    }
}
