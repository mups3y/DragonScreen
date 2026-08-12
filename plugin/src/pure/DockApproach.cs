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

        // ---- THE HOLDS. EVERY REAL WAYPOINT IS ONE. ----
        // Dragon stops and station-keeps at WP0, WP1 and WP2 awaiting a GO. Ours never stopped,
        // which is why it had to null a large lateral error WHILE closing - the real vehicle never
        // has to, because it arrives at each waypoint stationary and lined up before proceeding.
        // A hold is also what makes an approach abortable at every stage.
        /// <summary>Station-keeping 400 m below the station, awaiting GO.</summary>
        HoldWp0,
        /// <summary>Station-keeping 220 m out on the docking axis, awaiting GO.</summary>
        HoldWp1,
        /// <summary>Station-keeping 20 m from the port, awaiting GO. The last stop.</summary>
        HoldWp2,
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
        SideStep,

        // ---- THE REAL CREW DRAGON WAYPOINTS. docs/REAL_CREW_DRAGON_MISSION.md. ----
        /// <summary>WP0 - 400 m directly BELOW the station, on the +R-bar. The first hold.</summary>
        Wp0,
        /// <summary>WP1 - 220 m out along the docking axis. The second hold.</summary>
        Wp1,
        /// <summary>WP2 - 20 m from the port, inside the keep-out sphere. The last hold.</summary>
        Wp2,
        /// <summary>
        /// The WP0 -> WP1 transition: FORWARD at WP0's depth, then up onto the axis.
        ///
        /// ⛔ "swing up IN FRONT OF the station and enter the keep out sphere" - forward first,
        /// then up. Flown as a straight diagonal from 400 m below to 220 m ahead, the path grazes
        /// the 200 m keep-out sphere: the simulator measured the capsule 30 m INSIDE it, off the
        /// assigned corridor, which is an automatic abort on the real vehicle.
        /// </summary>
        Wp1Transit
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

        // ---- ⛔ THE STATION'S OWN ORBITAL FRAME (LVLH). WP0 IS DEFINED BY THE ORBIT, NOT THE PORT. ----
        // "Waypoint 0 is an imaginary spot 400 metres directly BELOW the station" - below means on
        // the R-bar, towards the planet, and no amount of port-axis geometry can express that. This
        // is the frame every real rendezvous is flown in and we did not have it.
        /// <summary>Radial offset from the station, metres. NEGATIVE is below, towards the planet.</summary>
        public double RadialM;
        /// <summary>Along-track offset, metres. POSITIVE is ahead of the station on the V-bar.</summary>
        public double AlongM;
        /// <summary>Out-of-plane offset, metres.</summary>
        public double CrossM;

        /// <summary>Speed relative to the station, m/s. A hold is not a hold until this is small.</summary>
        public double RelSpeedMps;
        /// <summary>How long we have been inside the current hold's box AND slow, seconds.</summary>
        public double HoldStableS;
        /// <summary>
        /// Has the crew given the GO for the next leg?
        ///
        /// ⚠ ON THE REAL VEHICLE EVERY WAYPOINT IS A GROUND/CREW DECISION, not a timer. Ours
        /// auto-releases once the hold is demonstrably stable, and this flag exists so the DOCKING
        /// page can take that decision back without the guidance changing shape. Wiring a GO button
        /// to it is a screen job, not a flight-software one.
        /// </summary>
        public bool CrewGo;
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

        // =============================================================================
        //  THE REAL CREW DRAGON PROXIMITY-OPERATIONS PROFILE
        //  docs/REAL_CREW_DRAGON_MISSION.md - sourced, not invented.
        //
        //  Arrive 7.5 km behind and below -> Approach Initiation -> into the Approach
        //  Ellipsoid -> WP0, 400 m BELOW, hold -> swing up and forward, entering the
        //  Keep Out Sphere on the assigned corridor -> WP1, 220 m on the axis, hold ->
        //  WP2, 20 m, hold -> contact and capture.
        //
        //  ⛔ IT IS AN L, NOT A LINE. Dragon comes up the R-bar from below, turns onto
        //  the docking axis in front, and runs in. We flew a straight line from wherever
        //  we happened to be to a gate on the port axis, which is why the capsule kept
        //  arriving off-axis and then tried to fix it while closing.
        // =============================================================================

        /// <summary>WP0 - 400 m directly below the station, on the R-bar.</summary>
        public const double Wp0BelowM = 400.0;
        /// <summary>WP1 - 220 m out along the docking axis.</summary>
        public const double Wp1AxialM = 220.0;
        /// <summary>WP2 - 20 m from the port. Inside the KOS, on the corridor.</summary>
        public const double Wp2AxialM = 20.0;

        /// <summary>
        /// Keep Out Sphere, 200 m RADIUS.
        ///
        /// ⚠ The popular sources say "a 200 metre wide safety zone"; the technical ones say a
        /// 200 m RADIUS, and that is what is used. It may be penetrated only on the assigned
        /// approach corridor - which is exactly the WP1 -> WP2 leg.
        /// </summary>
        public const double KeepOutRadiusM = 200.0;

        /// <summary>Approach Ellipsoid: 2000 m semi-axis along the V-bar, 1000 m orthogonal.</summary>
        public const double AeAlongM = 2000.0, AeCrossM = 1000.0;

        // ---- WHAT COUNTS AS ARRIVED, AND AS HELD ----
        /// <summary>Inside this of a waypoint, the leg is complete. Scales with the leg's length.</summary>
        public const double Wp0ToleranceM = 25.0, Wp1ToleranceM = 10.0, Wp2ToleranceM = 2.0;
        /// <summary>A hold is not a hold while still moving. Metres per second.</summary>
        public const double HoldSpeedMps = 0.15;
        /// <summary>...and it must stay that way this long before the next leg is released.</summary>
        public const double HoldDwellS = 5.0;

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
                // The real profile in order: out to WP0, hold; up and forward to WP1, hold;
                // in to WP2, hold; final run to contact. ToGate and Rounding share a rank -
                // rounding is a continuous hull-avoidance curve, not a milestone.
                case DockStage.ToGate:
                case DockStage.Rounding: return 1;   // transit to WP0
                case DockStage.HoldWp0:  return 2;   // 400 m below, holding
                case DockStage.Corridor: return 3;   // WP0 -> WP1
                case DockStage.HoldWp1:  return 4;   // 220 m on the axis, holding
                case DockStage.Axial:    return 5;   // WP1 -> WP2 -> port
                case DockStage.HoldWp2:  return 6;   // 20 m, holding
                case DockStage.Docked:   return 7;
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
            // ⛔ THE LATERAL TOLERANCE IS THE PORT'S OWN CAPTURE ENVELOPE, NOT THE CORRIDOR.
            // This tested `LateralM <= CorridorRadiusM` - a FULL METRE. Declaring capture means
            // "stop thrusting and let the magnets take it", and a metre off centre the magnets do
            // not reach: the ports never mate, the trim stops, and the capsule drifts on into the
            // station. That is the failure mode the crew has been watching.
            //
            // `AcquireM` is `theirPort.acquireRange * 0.5` - the port's own number, and the right
            // one in EVERY direction, not just along the axis. Until the capsule is inside it the
            // final run keeps trimming, which is the whole point of trimming to contact.
            if (s.AxialM <= s.AcquireM && s.AxialM > -s.AcquireM
                && s.LateralM <= s.AcquireM)
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
            //
            // ⛔ CLOSE-IN ONLY, AND THIS NEARLY ATE THE WHOLE PROFILE. Dragon's approach BEGINS
            // behind and below the station - "7.5 km behind and below" at Approach Initiation - so
            // a negative axial is the NORMAL starting condition, not a fault. Unqualified, this
            // branch caught the entire transit: the simulator flew 200 s of `Rounding / Gate` and
            // never went near WP0. It is a recovery for being behind the port at close range,
            // which is what the keep-out sphere bounds, so that is where it applies.
            if (s.AxialM < 0.0 && InsideKos(s))
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

            // ================= THE REAL PROFILE, IN ORDER =================
            //
            // Each leg ENDS AT A HOLD, and the next is released only when the hold is demonstrably
            // stable - inside its box, relative speed under `HoldSpeedMps`, for `HoldDwellS`. The
            // capsule therefore arrives at every waypoint stationary and lined up, and never has to
            // null a lateral error while closing. That is the whole difference from what we flew.
            //
            // ⚠ MONOTONE. `reached` only advances; a hold that has been released is not re-entered.

            bool held = s.RelSpeedMps <= HoldSpeedMps && s.HoldStableS >= HoldDwellS;
            bool go = held || s.CrewGo;

            // ---- WP2, 20 m: the last stop before contact ----
            if (Rank(reached) >= Rank(DockStage.HoldWp2))
            {
                if (s.LateralM > CorridorAbortM)
                {
                    r.Waypoint = DockWaypoint.Wp2;
                    r.Stage = DockStage.HoldWp2;
                    r.Note = "OFF THE CORRIDOR - back to WP2";
                    return r;
                }
                r.Waypoint = DockWaypoint.Port;
                r.Stage = DockStage.Axial;
                r.Note = "FINAL - " + s.AxialM.ToString("F1") + " m";
                return r;
            }

            if (Rank(reached) >= Rank(DockStage.HoldWp1) || AtWaypoint(s, Wp2AxialM, Wp2ToleranceM))
            {
                bool at2 = AtWaypoint(s, Wp2AxialM, Wp2ToleranceM);
                if (at2 && go)
                {
                    r.Waypoint = DockWaypoint.Port;
                    r.Stage = DockStage.HoldWp2;
                    r.Note = "WP2 GO - final approach";
                    return r;
                }
                r.Waypoint = DockWaypoint.Wp2;
                r.Stage = at2 ? DockStage.HoldWp2 : DockStage.Axial;
                r.Note = at2 ? ("WP2 HOLD - station-keeping, " + s.RelSpeedMps.ToString("F2") + " m/s")
                             : ("TO WP2 - " + (s.AxialM - Wp2AxialM).ToString("F0") + " m to run");
                return r;
            }

            // ---- WP1, 220 m on the docking axis: the corridor entry ----
            if (Rank(reached) >= Rank(DockStage.HoldWp0) || AtWaypoint(s, Wp1AxialM, Wp1ToleranceM))
            {
                bool at1 = AtWaypoint(s, Wp1AxialM, Wp1ToleranceM);
                if (at1 && go)
                {
                    r.Waypoint = DockWaypoint.Wp2;
                    r.Stage = DockStage.HoldWp1;
                    r.Note = "WP1 GO - entering the keep-out sphere on the corridor";
                    return r;
                }
                // Forward at depth first, then up onto the axis - never the diagonal. Once the
                // along-track is made good, the rise to WP1 is radial and stays clear of the KOS.
                bool ahead = s.AlongM >= Wp1AxialM * 0.9;
                r.Waypoint = at1 ? DockWaypoint.Wp1
                                 : (ahead ? DockWaypoint.Wp1 : DockWaypoint.Wp1Transit);
                r.Stage = at1 ? DockStage.HoldWp1 : DockStage.Corridor;
                r.Note = at1 ? ("WP1 HOLD - station-keeping, " + s.RelSpeedMps.ToString("F2") + " m/s")
                             : (ahead ? "TO WP1 - rising onto the docking axis"
                                      : "TO WP1 - forward at depth, clear of the keep-out sphere");
                return r;
            }

            // ---- WP0, 400 m below the station on the R-bar: the first hold ----
            bool at0 = AtWp0(s);
            if (at0 && go)
            {
                r.Waypoint = DockWaypoint.Wp1;
                r.Stage = DockStage.HoldWp0;
                r.Note = "WP0 GO - up and forward to the docking axis";
                return r;
            }
            r.Waypoint = DockWaypoint.Wp0;
            r.Stage = at0 ? DockStage.HoldWp0 : DockStage.ToGate;
            r.Note = at0 ? ("WP0 HOLD - 400 m below, " + s.RelSpeedMps.ToString("F2") + " m/s")
                         : ("TO WP0 - 400 m below the station, "
                            + Wp0RangeM(s).ToString("F0") + " m to run");
            return r;
        }

        /// <summary>Distance to WP0 - 400 m below the station on the R-bar.</summary>
        public static double Wp0RangeM(DockApproachInputs s)
        {
            double dr = s.RadialM + Wp0BelowM;          // WP0 sits at radial = -400
            return System.Math.Sqrt(dr * dr + s.AlongM * s.AlongM + s.CrossM * s.CrossM);
        }

        /// <summary>Are we parked at WP0?</summary>
        public static bool AtWp0(DockApproachInputs s)
        {
            return Wp0RangeM(s) <= Wp0ToleranceM;
        }

        /// <summary>Are we parked on the docking axis at `axialM` out from the port?</summary>
        public static bool AtWaypoint(DockApproachInputs s, double axialM, double tolM)
        {
            double da = s.AxialM - axialM;
            return System.Math.Sqrt(da * da + s.LateralM * s.LateralM) <= tolM;
        }

        /// <summary>Inside the Approach Ellipsoid - 2000 m along the V-bar, 1000 m across.</summary>
        public static bool InsideAe(DockApproachInputs s)
        {
            double a = s.AlongM / AeAlongM, r = s.RadialM / AeCrossM, c = s.CrossM / AeCrossM;
            return a * a + r * r + c * c <= 1.0;
        }

        /// <summary>Inside the 200 m Keep Out Sphere.</summary>
        public static bool InsideKos(DockApproachInputs s)
        {
            return System.Math.Sqrt(s.RadialM * s.RadialM + s.AlongM * s.AlongM
                                    + s.CrossM * s.CrossM) <= KeepOutRadiusM;
        }

    }
}
