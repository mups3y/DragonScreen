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
 * test could reach it.
 *
 * So the decision is here, in scalars, and both the flight code and the simulator call it.
 *
 * ---- WHAT CHANGED, AND WHERE IT COMES FROM ----
 * Read from `MechJebModuleDockingAutopilot.cs`, which is generic KSP docking that WORKS, against
 * ours which is Crew-Dragon-shaped and did not. We keep the profile - keep-out sphere, gate,
 * corridor - and take its inner rules:
 *
 *   · COMMIT AT 1 m OF LATERAL, NOT 15. `dockingcorridorRadius = 1` (:59). Ours committed to the
 *     axial run at `GateToleranceM` 15 m and `StandoffToleranceM` 12 m, so the capsule began its
 *     final approach up to fifteen metres off the axis and tried to null that while closing.
 *   · WRONG SIDE IS A STATE, NOT AN ACCIDENT (:296-306, :353-370). If our port is behind theirs we
 *     back out and go round; we never drive through the station. Ours had no such notion, so
 *     "behind the port" and "in front of it" produced the same commands.
 *
 * ---- AND THE STAGE MACHINE IS MONOTONE ----
 * Kept from the 2026-08-12 fix and now enforced here: every proximity test falsifies itself by
 * succeeding, so a stage may only ever advance. See `Rank`.
 */
namespace DragonScreen
{
    // ---- MOVED HERE FROM DockingOps ON 2026-08-13 ----
    // The stage is what both layers reason about, and the layer that DECIDES it has to be
    // testable. It is a plain enum with no KSP in it, so pure is where it belongs.
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
        /// At the gate and running IN along the port axis to the standoff.
        ///
        /// This is the real vehicle's approach-corridor transit: Crew Dragon holds outside the
        /// Keep Out Sphere and then enters along a defined corridor to a close hold, rather than
        /// closing on the station from wherever it happens to be. It is also the leg whose absence
        /// deadlocked every docking attempt up to 2026-08-12 - see DockGeometry.AtGate.
        /// </summary>
        Corridor,
        /// <summary>Holding at the standoff, lined up on the axis.</summary>
        Standoff,
        /// <summary>Straight down the port axis.</summary>
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
        /// <summary>The port itself. Only from inside the corridor.</summary>
        Port,
        /// <summary>Back out along the axis - we are too close to manoeuvre.</summary>
        BackOut,
        /// <summary>Move off the axis before backing up, so we do not reverse through the station.</summary>
        SideStep
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
        /// <summary>Station bounding radius plus ours, metres. The distance a back-out must reach.</summary>
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
        /// ⛔ `MechJebModuleDockingAutopilot.cs:59`, `dockingcorridorRadius = 1`. Ours was 15 m of
        /// gate tolerance and 12 m of standoff tolerance, and the 2026-08-12 recording shows the
        /// consequence: the capsule entered `Axial` with 3.3-4.0 m of lateral offset and pushed fore
        /// while the lateral never closed.
        /// </summary>
        public const double CorridorRadiusM = 1.0;

        /// <summary>Lateral error that ABORTS a run already started. Hysteresis, not a second value.</summary>
        public const double CorridorAbortM = 2.0;

        /// <summary>How far in front of the port the capsule lines up. Also the corridor's length.</summary>
        public const double StandoffM = DockGeometry.StandoffM;

        /// <summary>
        /// Behind the port by more than this and we go round rather than backing straight out.
        /// MechJeb uses half our own bounding box (`:355`); expressed here as a caller-supplied size.
        /// </summary>
        public static double WrongSideM(double safeM) { return safeM * 0.5; }

        /// <summary>Stage ordering. ToGate and Rounding share a rank - rounding is a curve, not a milestone.</summary>
        public static int Rank(DockStage s)
        {
            switch (s)
            {
                case DockStage.ToGate:
                case DockStage.Rounding: return 1;
                case DockStage.Corridor: return 2;
                case DockStage.Axial:    return 3;
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
            // axial too, so a capsule that flew straight through the station at 3.4 m/s reported a
            // capture - and, worse, the same test caught every wrong-side case before the
            // wrong-side branch below could see it. Found by the headless approach simulator on the
            // first run, which is the entire reason it exists.
            if (s.AxialM <= s.AcquireM && s.AxialM > -s.AcquireM
                && s.LateralM <= CorridorRadiusM)
            {
                r.Captured = true;
                r.Stage = DockStage.Docked;
                r.Waypoint = DockWaypoint.Port;
                r.Note = "CAPTURE";
                return r;
            }

            // ---- WRONG SIDE. Never reverse through the station. ----
            // Off the axis first, then back out, then round to the front. Order matters: backing up
            // while still on the axis drives us into the hull we are behind.
            if (s.AxialM < 0.0)
            {
                if (-s.AxialM > WrongSideM(s.SafeM))
                {
                    if (s.LateralM < s.SafeM)
                    {
                        r.Waypoint = DockWaypoint.SideStep;
                        r.Stage = DockStage.Rounding;
                        r.Note = "WRONG SIDE - clearing the axis before backing out";
                        return r;
                    }
                    r.Waypoint = DockWaypoint.Gate;
                    r.Stage = DockStage.Rounding;
                    r.Note = "WRONG SIDE - going round to the front";
                    return r;
                }
                r.Waypoint = DockWaypoint.BackOut;
                r.Stage = DockStage.Corridor;
                r.Note = "BEHIND THE PORT - backing out";
                return r;
            }

            // ---- THE FINAL RUN. Started only from inside the corridor; abandoned outside it. ----
            // Monotone in the sense that matters: once inside `CorridorRadiusM` the run continues
            // until capture or until the lateral exceeds `CorridorAbortM`, at which point the
            // capsule returns to the standoff rather than pressing on crooked.
            if (Rank(reached) >= Rank(DockStage.Axial))
            {
                if (s.LateralM > CorridorAbortM)
                {
                    r.Waypoint = DockWaypoint.Standoff;
                    r.Stage = DockStage.Corridor;
                    r.Note = "OFF THE CORRIDOR - back to the standoff";
                    return r;
                }
                r.Waypoint = DockWaypoint.Port;
                r.Stage = DockStage.Axial;
                r.Note = "FINAL - " + s.AxialM.ToString("F1") + " m";
                return r;
            }

            if (s.LateralM <= CorridorRadiusM && s.AxialM <= StandoffM + s.SafeM)
            {
                r.Waypoint = DockWaypoint.Port;
                r.Stage = DockStage.Axial;
                r.Note = "FINAL - " + s.AxialM.ToString("F1") + " m";
                return r;
            }

            // ---- THE CORRIDOR. Line up on the axis at the standoff. ----
            if (Rank(reached) >= Rank(DockStage.Corridor) || DockGeometry.AtGate(s.ToGateM))
            {
                r.Waypoint = DockWaypoint.Standoff;
                r.Stage = DockStage.Corridor;
                r.Note = "CORRIDOR - " + s.ToStandoffM.ToString("F0") + " m to the standoff";
                return r;
            }

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
