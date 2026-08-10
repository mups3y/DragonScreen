/*
 * DragonScreen - Landing
 *
 * PURE. Booster recovery: boostback, entry burn, and the landing burn. No KSP, no Unity, so the
 * whole descent can be flown headless before a booster is risked on it.
 *
 * ---- THE LANDING LAW IS MECHJEB'S, READ FROM SOURCE, AND IT IS THE RIGHT ONE ----
 * `MechJebModuleLandingAutopilot.cs:563-567`, class `ConstantThrustDescentSpeedPolicy`:
 *
 *      public double MaxAllowedSpeed(Vector3d pos, Vector3d vel)
 *      {
 *          double altitude = pos.magnitude - _terrainRadius;
 *          return 0.9 * Math.Sqrt(2 * (_thrust - _g) * altitude);
 *      }
 *
 * That is the hoverslam curve, and it falls out of v^2 = 2*a*d with a = (thrust accel - gravity):
 * the fastest you may be going at height h and still stop by h = 0. The 0.9 is a 10% margin, and it
 * is doing real work - at full-throttle-or-nothing a booster that solves this exactly has no way to
 * correct, because its only correction is MORE thrust and it is already at maximum.
 *
 * ---- WHAT IS SPACEX'S METHOD AND WHAT IS OURS ----
 * The four-phase sequence - boostback, coast, entry burn, landing burn - IS the real profile and is
 * public. The landing burn being a single full-thrust decelerate-to-zero is also real: Falcon 9
 * cannot hover, its minimum thrust on one engine exceeds its landed weight, so it MUST arrive at
 * zero velocity and zero altitude simultaneously. That constraint is why the hoverslam is the
 * correct law here rather than a convenience.
 *
 * What is OURS: the trigger altitudes and speeds. Blackmore's convexified G-FOLD is the published
 * SpaceX guidance and it is a proper optimal-control solve with a real divert capability; this is
 * the degenerate case of it - stop the fall, do not steer far. See docs/FLIGHT_SOFTWARE_PLAN.md
 * section 2, which says exactly this and says to start here.
 *
 * ---- THE ONE NUMBER THAT BITES ----
 * `falcon-booster-landing-twr`: at 11% propellant our booster has TWR 0.81 on ONE engine and CANNOT
 * land. So the landing solve must use the thrust the vehicle will ACTUALLY have, and if
 * (thrust accel - gravity) is not positive there is no solution at any altitude - which this reports
 * rather than dividing by it.
 */
namespace DragonScreen
{
    public enum LandingPhase : byte
    {
        Idle = 0,
        /// <summary>Flip and burn back toward the pad.</summary>
        Boostback,
        /// <summary>Engines off, arcing over the top.</summary>
        Coast,
        /// <summary>Slow down before the thick air.</summary>
        EntryBurn,
        /// <summary>Falling retrograde, waiting for the hoverslam.</summary>
        Descent,
        /// <summary>The single full-thrust burn to zero.</summary>
        LandingBurn,
        Touchdown,
        /// <summary>No solution - not enough thrust to stop. Said out loud, not hidden.</summary>
        NoSolution
    }

    /// <summary>Where the guidance wants the vehicle pointed. The glue turns these into vectors.</summary>
    public enum LandingAim : byte
    {
        Hold = 0,
        /// <summary>Against the surface velocity - the braking attitude.</summary>
        SurfaceRetrograde,
        /// <summary>Horizontally toward the landing zone, for boostback.</summary>
        TowardTarget,
        /// <summary>Straight up, for the last few metres.</summary>
        Up
    }

    public struct LandingInputs
    {
        public bool Valid;
        /// <summary>Height above the terrain, metres, to the LOWEST PART.</summary>
        public double AltitudeRadar;
        /// <summary>Altitude above sea level. The entry-burn gate is on ASL, not radar.</summary>
        public double AltitudeAsl;
        /// <summary>Positive up.</summary>
        public double VerticalSpeed;
        public double HorizontalSpeed;
        public double SurfaceSpeed;
        /// <summary>Full-throttle acceleration at the CURRENT mass with ALL engines, m/s^2.</summary>
        public double MaxThrustAccel;
        /// <summary>How many engines the booster actually has available.</summary>
        public int EngineCount;

        // ---- ⚠ THE OCTAWEB'S MODES ARE NOT MULTIPLES OF ONE ENGINE ----
        // See VehicleParts: 2560 / 1706 / 764 kN for nine / three / one, which is 284 / 569 / 764 kN
        // "per engine". Scaling MaxThrustAccel by an engine COUNT - which is what PhaseAccel used to
        // do, and all it could do - overstates the one-engine landing burn by 2.2x. That number goes
        // straight into the hoverslam solve, so it puts the ignition altitude too low and flies the
        // stage into the pad, which is the same failure ISSUE 2 was written to prevent.
        //
        // Zero means "this vehicle has no such discrete mode"; PhaseAccel then falls back to the
        // linear estimate, which is right for a conventional cluster of identical engines.
        /// <summary>Accel in the three-engine landing mode, m/s^2. Zero if the vehicle has none.</summary>
        public double AccelThreeEngine;
        /// <summary>Accel on the centre engine alone, m/s^2. Zero if the vehicle has none.</summary>
        public double AccelOneEngine;

        /// <summary>Seconds in the current phase. The entry burn's soft start is timed off this.</summary>
        public double PhaseElapsedS;
        public double Gravity;
        /// <summary>Ground distance to the landing zone, metres.</summary>
        public double DownrangeM;

        // ---- THE BOOSTBACK IS FLOWN AGAINST A PREDICTED IMPACT POINT, NOT A VELOCITY ----
        // BOOSTER.ks does this with the Trajectories add-on; we cannot take that dependency, so the
        // glue integrates a ballistic impact and signs it here. POSITIVE means the impact point is
        // still short of the landing zone - on the booster's side of it - and NEGATIVE means it has
        // walked past. Boostback burns until it is past by BoostbackOvershootM.
        /// <summary>Signed miss of the predicted impact point, metres. Negative = beyond the LZ.</summary>
        public double PredictedMissM;
        /// <summary>|PredictedMissM| when the boostback burn began. Throttle tapers against it.</summary>
        public double InitialMissM;
        public double AtmosphereDepthM;
        public double DynamicPressureKpa;
        public bool Landed;
    }

    public struct LandingCommand
    {
        public LandingPhase Phase;
        public double Throttle;
        public LandingAim Aim;
        public string Note;
        /// <summary>Height the landing burn must start at, for display. Zero when unsolvable.</summary>
        public double IgnitionAltitude;
        /// <summary>How many engines this phase should be flying on. See EnginesFor.</summary>
        public int Engines;
        /// <summary>Legs out. F9I drops them at 200 m radar.</summary>
        public bool DeployLegs;
    }

    public static class Landing
    {
        /// <summary>MechJeb's margin on the descent policy. Ported with the formula, not invented.</summary>
        public const double SpeedMargin = 0.9;

        // ---- EVERY NUMBER BELOW IS F9I'S, AND FLOWN. RETUNE THEM IN BOOSTER.ks, NOT HERE. ----
        // `SPACEX/BOOSTER.ks`. Those boosters land 0.34-0.56 m from the pad.

        /// <summary>
        /// Entry burn gate, metres ASL. BOOSTER.ks:707 - "32500 m IS THE ENTRY BURN GATE, AND THIS
        /// LITERAL IS NOW ITS ONLY DEFINITION." 35 000 for a droneship. Gated on ASL, not radar:
        /// the deleted older version gated on alt:radar, so notes quoting that are NOT comparable.
        /// </summary>
        public const double EntryBurnGateAsl = 32500.0;

        /// <summary>Entry burn cuts on VERTICAL speed, not airspeed. BOOSTER.ks:728.</summary>
        public const double EntryBurnCutVs = -300.0;

        /// <summary>
        /// Lowest part to the engine bells, metres. `F9L_BoosterHeight`. alt:radar measures to the
        /// LOWEST PART, so the engines have this much less height to fly out - and 31 m is most of
        /// a landing burn's final margin. Forgetting it lands the stage 31 m underground.
        /// </summary>
        public const double BoosterHeightM = 31.02;

        /// <summary>Bulk of the landing burn: 6% over what the maths says. `F9L_BulkMargin`.</summary>
        public const double BulkMargin = 0.06;

        /// <summary>
        /// The flare. In the last 25 m above the BELLS, add 34%. F9I's own note: this "is what
        /// converts a controlled descent into a near-zero touchdown - without it the stage arrives
        /// at the ground still doing several m/s because the burn is only ever asymptotically
        /// finished."
        /// </summary>
        public const double FlareRadarM = 25.0, FlareMargin = 0.34;

        /// <summary>
        /// Three-engine to one-engine thrust ratio. NOT a guess: `TE_19_F9_S1_Engine.cfg` gives
        /// ThreeLanding 1706 kN and CenterOnly 764 kN, and 1706/764 = 2.233.
        /// </summary>
        public const double OneEngineRatio = 2.23;

        /// <summary>
        /// Hand to the centre engine only when one Merlin could still stop us with a third to spare.
        /// F9I: "Getting this wrong is not a soft failure. At low propellant this stage does not have
        /// TWR 1 on one Merlin, and a handover that happens too early cannot be undone."
        /// </summary>
        public const double HandoverPad = 1.35, HandoverVs = -40.0;

        /// <summary>Legs out at 200 m radar. BOOSTER.ks:797.</summary>
        public const double LegsAltitudeM = 200.0;

        /// <summary>Boostback is finished when the predicted miss is inside this. Metres.</summary>
        public const double BoostbackTolerance = 400.0;

        /// <summary>
        /// Deliberate overshoot of the landing zone at boostback cutoff, metres. BOOSTER.ks:455-458.
        /// Drag can only ever SHORTEN a trajectory, so the burn aims long and the entry burn plus the
        /// guided descent bleed the rest off - "undershooting cannot be recovered, overshooting can".
        /// </summary>
        public const double BoostbackOvershootM = 2700.0;

        /// <summary>Boostback throttle floor. Keeps the engines lit and the gimbal authoritative.</summary>
        public const double BoostbackMinThrottle = 0.25;

        /// <summary>Touchdown when this low and this slow.</summary>
        public const double TouchdownAltitude = 3.0, TouchdownSpeed = 3.0;

        // ------------------------------------------------------------------ the core law

        /// <summary>
        /// Fastest we may be falling at this height and still stop by the ground.
        /// MechJebModuleLandingAutopilot.cs:563-567, unchanged.
        /// </summary>
        public static double MaxAllowedSpeed(double altitude, double thrustAccel, double gravity)
        {
            double net = thrustAccel - gravity;
            if (net <= 0.0 || altitude <= 0.0) return 0.0;
            return SpeedMargin * System.Math.Sqrt(2.0 * net * altitude);
        }

        /// <summary>
        /// The same law inverted: how high to light the engine, given how fast we are coming down.
        ///
        /// Returns 0 when there is NO SOLUTION - net acceleration at or below zero means the vehicle
        /// cannot stop at any altitude. `falcon-booster-landing-twr` records exactly that case at 11%
        /// propellant on one engine, so it is a real state and not a defensive branch.
        /// </summary>
        public static double IgnitionAltitude(double speed, double thrustAccel, double gravity)
        {
            double net = thrustAccel - gravity;
            if (net <= 0.0) return 0.0;
            return (speed * speed) / (2.0 * net * SpeedMargin * SpeedMargin);
        }

        /// <summary>
        /// Ballistic time to the ground from here, seconds. Used to work out how much horizontal
        /// velocity boostback has to leave the booster with.
        ///
        /// t = (v_up + sqrt(v_up^2 + 2gh)) / g - the positive root of h + v*t - g*t^2/2 = 0. Vacuum;
        /// drag makes the real fall longer, which makes boostback slightly under-burn and land short
        /// rather than long. Erring toward the sea is the right way to be wrong.
        /// </summary>
        public static double TimeToGround(double altitude, double verticalSpeed, double gravity)
        {
            if (gravity <= 0.0) return 0.0;
            double disc = verticalSpeed * verticalSpeed + 2.0 * gravity * altitude;
            if (disc < 0.0) return 0.0;
            return (verticalSpeed + System.Math.Sqrt(disc)) / gravity;
        }

        /// <summary>
        /// Horizontal speed toward the pad that would put the booster on it. Closed loop: recomputed
        /// every cycle from the CURRENT miss and the CURRENT time of flight, so it converges rather
        /// than flying a precomputed burn.
        /// </summary>
        public static double RequiredClosingSpeed(LandingInputs s)
        {
            double t = TimeToGround(s.AltitudeRadar, s.VerticalSpeed, s.Gravity);
            if (t < 1.0) return 0.0;
            return s.DownrangeM / t;
        }

        // ------------------------------------------------------------------ where to pick it up

        /// <summary>
        /// Which phase a booster should be flown from, given the state it is ACTUALLY in when we
        /// take it over.
        ///
        /// ---- WHY THIS IS NOT ALWAYS Boostback ----
        /// Handover used to start at Boostback unconditionally, which is right only if we take the
        /// stage seconds after separation. We do not: the upper stage cannot be abandoned mid-ascent
        /// (one vessel at a time under the ~300 km clamp), so handover waits for Coast - and in the
        /// 21:01 flight that was 155 s after separation, by which time the booster is already most
        /// of the way down. Commanding a boostback burn there points a falling stage back up the
        /// range and wastes the propellant the landing burn needs.
        ///
        /// So: still climbing means separation was recent and a boostback is both possible and
        /// useful. Descending means that moment has passed, and the honest thing is to join the
        /// profile wherever the stage really is and fly the part that is left.
        /// </summary>
        public static LandingPhase InitialPhase(LandingInputs s)
        {
            if (s.Landed) return LandingPhase.Touchdown;
            if (s.VerticalSpeed > 0.0) return LandingPhase.Boostback;
            if (s.AltitudeAsl <= EntryBurnGateAsl) return LandingPhase.EntryBurn;
            return LandingPhase.Coast;
        }

        // ------------------------------------------------------------------ engine selection

        /// <summary>Falcon 9 burns three engines for boostback and for the entry burn. Real.</summary>
        public const int BoostbackEngines = 3, EntryEngines = 3;

        /// <summary>
        /// A one-engine landing burn needs at least this much thrust-to-weight to be flyable. Below
        /// it the booster commits to three and accepts the fuel cost.
        /// </summary>
        public const double MinLandingTwr = 1.25;

        /// <summary>
        /// How many engines to light, per phase.
        ///
        /// ---- THIS IS THE REAL PROFILE, AND IT IS ALSO A TRAP WE HAVE ALREADY FALLEN INTO ----
        /// Falcon 9 uses THREE engines for boostback, THREE for the entry burn, and normally ONE -
        /// the centre engine - for the landing burn, occasionally three on high-energy missions
        /// because it saves fuel at the cost of margin. Treating the landing as "all nine" would give
        /// a hoverslam ignition altitude of a few hundred metres and a TWR of about twenty.
        ///
        /// And the one-versus-three choice is not cosmetic here: `falcon-booster-landing-twr` records
        /// that at 11% propellant our booster has TWR 0.81 on a single engine and CANNOT land on it.
        /// That memory's own instruction is "pick engine mode from MEASURED thrust, before the
        /// landing solve" - which is exactly what this does.
        /// </summary>
        /// <summary>
        /// Entry-burn soft start, seconds. BOOSTER.ks:721 - "light the CENTRE engine alone at full
        /// throttle, give it 0.75 s to establish, then add the other two. Lighting three at once
        /// into supersonic flow is the shock the stage does not need."
        /// </summary>
        public const double EntrySoftStartS = 0.75;

        public static int EnginesFor(LandingPhase phase, LandingInputs s)
        {
            int have = (s.EngineCount > 0) ? s.EngineCount : 1;

            if (phase == LandingPhase.Boostback) return Min(BoostbackEngines, have);

            // The entry burn opens on the centre engine alone and adds the outboards at 0.75 s. F9I
            // does this with two EngSwitch calls either side of a `wait`; we have no `wait`, so the
            // schedule is expressed against phase-elapsed time and the glue follows it per tick.
            if (phase == LandingPhase.EntryBurn)
                return (s.PhaseElapsedS < EntrySoftStartS)
                     ? Min(1, have)
                     : Min(EntryEngines, have);

            if (phase != LandingPhase.LandingBurn) return 0;

            // Landing: measure what ONE engine actually gives at this mass. Prefer the vehicle's own
            // centre-engine figure - dividing the all-engine accel by the engine count is the 2.2x
            // error described on AccelOneEngine, and this is the exact test that decides whether the
            // booster commits to a one-engine landing.
            double perEngine = (s.AccelOneEngine > 0.0) ? s.AccelOneEngine : s.MaxThrustAccel / have;
            double twrOne = (s.Gravity > 0.0) ? perEngine / s.Gravity : 0.0;
            return (twrOne >= MinLandingTwr) ? 1 : Min(3, have);
        }

        private static int Min(int a, int b) { return a < b ? a : b; }

        /// <summary>
        /// Thrust acceleration available on the engines this phase will actually light.
        ///
        /// Uses the vehicle's MEASURED per-mode accelerations when it has them, because the octaweb's
        /// modes are not multiples of one engine (see AccelOneEngine). The linear fallback is kept
        /// for a conventional cluster of identical engines, where it is exactly right.
        /// </summary>
        public static double PhaseAccel(LandingPhase phase, LandingInputs s)
        {
            int have = (s.EngineCount > 0) ? s.EngineCount : 1;
            int use = EnginesFor(phase, s);
            if (use <= 0) use = have;

            if (use == 1 && s.AccelOneEngine > 0.0) return s.AccelOneEngine;
            if (use == 3 && s.AccelThreeEngine > 0.0) return s.AccelThreeEngine;
            return s.MaxThrustAccel * use / have;
        }

        // ------------------------------------------------------------------ the sequence

        public static LandingCommand Guide(LandingInputs s, LandingPhase phase)
        {
            LandingCommand c = new LandingCommand();
            c.Phase = phase;
            c.Aim = LandingAim.SurfaceRetrograde;

            if (!s.Valid) { c.Phase = LandingPhase.Idle; c.Note = "no vessel"; return c; }

            // SOLVE ON THE ENGINES THE LANDING BURN WILL ACTUALLY LIGHT, not on all nine. Solving on
            // full thrust and then lighting one engine is how a booster arrives at the pad still
            // doing 200 m/s: the ignition altitude would be nine times too low.
            double landAccel = PhaseAccel(LandingPhase.LandingBurn, s);
            double ign = IgnitionAltitude(s.SurfaceSpeed, landAccel, s.Gravity);
            c.IgnitionAltitude = ign;

            if (s.Landed || (s.AltitudeRadar < TouchdownAltitude && s.SurfaceSpeed < TouchdownSpeed))
            {
                c.Phase = LandingPhase.Touchdown;
                c.Throttle = 0.0;
                c.Aim = LandingAim.Up;
                c.Note = "TOUCHDOWN";
                return c;
            }

            // ---- NO SOLUTION IS A STATE, NOT AN EXCEPTION ----
            // Not enough thrust to stop, at any altitude. Say so while there is still time to do
            // something about it rather than discovering it in the crater.
            if (landAccel <= s.Gravity && s.AltitudeRadar < s.AtmosphereDepthM * 0.5)
            {
                c.Phase = LandingPhase.NoSolution;
                c.Throttle = 1.0;                       // everything we have, it is still correct
                c.Note = "NO SOLUTION - TWR BELOW 1";
                return c;
            }

            // ---- TRANSITIONS FIRST, THEN THE COMMAND FOR WHATEVER PHASE WE LANDED IN ----
            // These used to be interleaved, and the bug that produced is worth stating: the Coast
            // branch set Phase = EntryBurn and then returned Coast's throttle of zero, so the
            // command described one phase while the label described another. Harmless for one frame
            // in game, invisible in a log, and exactly the kind of thing that makes a state machine
            // untrustworthy. Deciding the phase and then rendering it removes the whole class.
            // ---- ⛔ ONE TRANSITION PER TICK. THESE WERE SEQUENTIAL `if`s AND IT COST THE RTLS. ----
            // 22:18 flight: BOOSTBACK completed at 119.9 s, fell straight through the Coast block on
            // the SAME TICK, and landed in LANDING BURN at 44 km altitude while CLIMBING at 828 m/s.
            // It then burned for 370 seconds, flew the booster up to 90 km, ran the tanks dry and
            // reported NO SOLUTION at 6.8 km.
            //
            // This is the same `if` versus `else if` class that already cost seven ascent failures
            // and has its own rule in CLAUDE.md. A chain cannot cascade.
            if (phase == LandingPhase.Idle) phase = LandingPhase.Boostback;

            else if (phase == LandingPhase.Boostback && BoostbackDone(s))
                phase = LandingPhase.Coast;

            else if (phase == LandingPhase.Coast && InEntryBand(s)
                     && s.VerticalSpeed < EntryBurnCutVs)
                phase = LandingPhase.EntryBurn;

            else if (phase == LandingPhase.Coast && Hoverslam(s, ign))
                phase = LandingPhase.LandingBurn;

            // F9I cuts on VERTICAL SPEED: `until (ship:verticalspeed > -300)`.
            else if (phase == LandingPhase.EntryBurn
                     && (s.VerticalSpeed > EntryBurnCutVs || !InEntryBand(s)))
                phase = LandingPhase.Descent;

            else if (phase == LandingPhase.Descent && Hoverslam(s, ign))
                phase = LandingPhase.LandingBurn;

            c.Phase = phase;

            switch (phase)
            {
                case LandingPhase.Boostback:
                    c.Aim = LandingAim.TowardTarget;
                    // ---- THROTTLE ON THE FRACTION OF THE ERROR STILL LEFT ----
                    // BOOSTER.ks:478 - `set throt to max(0.25, impDist / intDist)`. Burns hard while
                    // the error is large and eases as it closes, which is what stops the boostback
                    // overshooting its own overshoot target. The floor is not a minimum useful
                    // thrust: it keeps the engines lit so the gimbal stays authoritative.
                    c.Throttle = BoostbackThrottle(s);
                    c.Note = "BOOSTBACK";
                    break;

                case LandingPhase.Coast:
                    c.Throttle = 0.0;
                    c.Note = "COAST";
                    break;

                case LandingPhase.EntryBurn:
                    c.Throttle = 1.0;
                    c.Note = "ENTRY BURN";
                    break;

                case LandingPhase.Descent:
                    c.Throttle = 0.0;
                    c.Note = "DESCENT";
                    break;

                case LandingPhase.LandingBurn:
                {
                    // ---- F9I'S SUICIDE BURN, PORTED WHOLE ----
                    // Fly the ratio directly plus a margin, so the burn self-corrects UP the
                    // remaining height rather than down through the ground. The flare in the last
                    // 25 m is what makes the touchdown soft rather than merely survivable.
                    double margin = (TrueRadar(s) < FlareRadarM) ? FlareMargin : BulkMargin;
                    double th = BurnThrottle(s, landAccel) + margin;
                    if (th < 0.0) th = 0.0; else if (th > 1.0) th = 1.0;
                    c.Throttle = th;
                    c.Aim = (s.AltitudeRadar < 60.0) ? LandingAim.Up : LandingAim.SurfaceRetrograde;
                    c.DeployLegs = s.AltitudeRadar < LegsAltitudeM;
                    c.Note = "LANDING BURN";
                    break;
                }

                default:
                    c.Throttle = 0.0;
                    c.Note = Name(phase);
                    break;
            }

            c.Engines = EnginesFor(c.Phase, s);
            return c;
        }

        /// <summary>
        /// ⛔ THE HOVERSLAM IS ONLY ARMED ON THE WAY DOWN.
        ///
        /// `ign` is derived from SURFACE speed, which during boostback is ~830 m/s of mostly
        /// HORIZONTAL velocity - so it evaluated to 89.6 km while the stage was at 31 km, and the
        /// gate was wide open from the moment of handover. The 22:18 booster lit its landing burn
        /// climbing through 44 km.
        ///
        /// A landing burn is a thing you do while falling. Requiring that costs nothing and makes
        /// the gate mean what its name says.
        /// </summary>
        private static bool Hoverslam(LandingInputs s, double ign)
        {
            return s.VerticalSpeed < 0.0 && s.AltitudeRadar <= ign;
        }

        /// <summary>
        /// Boostback is finished when the PREDICTED IMPACT POINT has walked back past the landing
        /// zone by <see cref="BoostbackOvershootM"/>.
        ///
        /// ---- WHY OVERSHOOT ON PURPOSE ----
        /// BOOSTER.ks:455 states the reasoning and it is not a fudge: "Deliberately overshoot the pad
        /// by 2.7 km and stop burning. The stage still has the whole entry and the guided descent to
        /// bleed that off, and AtmGNC steers with drag far more cheaply than this burn does -
        /// undershooting cannot be recovered, overshooting can." Drag only ever shortens a
        /// trajectory, so the boostback must aim long.
        ///
        /// ---- WHAT THIS REPLACED ----
        /// A miss estimated as (requiredClosingSpeed - horizontalSpeed) x timeToGround. During
        /// boostback the stage is CLIMBING, so timeToGround is nonsense and so was the estimate. The
        /// signed impact prediction comes from the glue, which has the vessel and the body.
        /// </summary>
        private static bool BoostbackDone(LandingInputs s)
        {
            return s.PredictedMissM < -BoostbackOvershootM;
        }

        /// <summary>Proportional boostback throttle. BOOSTER.ks:478.</summary>
        public static double BoostbackThrottle(LandingInputs s)
        {
            if (s.InitialMissM <= 0.0) return 1.0;
            double left = s.PredictedMissM;
            if (left < 0.0) left = -left;
            double frac = left / s.InitialMissM;
            if (frac > 1.0) frac = 1.0;
            return (frac < BoostbackMinThrottle) ? BoostbackMinThrottle : frac;
        }

        /// <summary>The entry-burn window, on ASL and vertical speed exactly as BOOSTER.ks.</summary>
        private static bool InEntryBand(LandingInputs s)
        {
            return s.AltitudeAsl < EntryBurnGateAsl && s.AltitudeRadar > 1000.0;
        }

        /// <summary>Height the ENGINES must fly out. Radar is to the lowest part; bells are higher.</summary>
        public static double TrueRadar(LandingInputs s)
        {
            double h = s.AltitudeRadar - BoosterHeightM;
            return (h > 0.0) ? h : 0.0;
        }

        /// <summary>
        /// F9I's whole suicide burn in one number (`F9L_BurnThrottle`): the fraction of the height
        /// left that the stop actually needs.
        ///
        ///     StopDist = verticalSpeed^2 / (2 * (thrustAccel - g))
        ///     throttle = StopDist / TrueRadar
        ///
        /// Below 1 there is room to spare; at 1 the burn must be full throttle right now; above 1 it
        /// is already too late. This replaced a proportional error term of mine and is strictly
        /// better: dimensionless, self-correcting UPWARD, and with no gain to tune.
        /// </summary>
        public static double BurnThrottle(LandingInputs s, double thrustAccel)
        {
            double decel = thrustAccel - s.Gravity;
            double h = TrueRadar(s);
            if (decel <= 0.0 || h <= 0.0) return 1.0;
            double stop = s.VerticalSpeed * s.VerticalSpeed / (2.0 * decel);
            return stop / h;
        }

        /// <summary>
        /// Stopping distance on ONE engine. Only valid because the entry burn deliberately leaves
        /// the octaweb in three-engine mode - that ordering is what makes 2.23 the right ratio.
        /// </summary>
        public static double OneEngineStopDist(LandingInputs s, double threeEngineAccel)
        {
            double decel = threeEngineAccel / OneEngineRatio - s.Gravity;
            if (decel <= 0.0) return double.MaxValue;
            return s.VerticalSpeed * s.VerticalSpeed / (2.0 * decel);
        }

        /// <summary>Safe to drop to the centre engine? BOTH conditions are required.</summary>
        public static bool HandoverReady(LandingInputs s, double threeEngineAccel)
        {
            if (s.VerticalSpeed <= HandoverVs) return false;
            return OneEngineStopDist(s, threeEngineAccel) * HandoverPad < TrueRadar(s);
        }

        // ------------------------------------------------------------------ landing-zone guidance

        /// <summary>
        /// Inside this the steering authority winds down linearly. `F9L_GuidanceDeadband`.
        /// Without it the guidance keeps fighting for the last couple of metres exactly when the
        /// stage most needs to be settling vertical, and it arrives cranked over.
        /// </summary>
        public const double GuidanceDeadbandM = 5.0;

        /// <summary>Unpowered aerodynamic trim during the descent. `F9L_AOA` = 15.</summary>
        public const double AeroAoaDeg = 15.0;

        /// <summary>Powered lean at ignition, and the floor/ceiling of the taper.</summary>
        public const double PoweredAoaStartDeg = -3.0;
        public const double PoweredAoaMinDeg = -4.0, PoweredAoaMaxDeg = -1.0;

        /// <summary>
        /// How far off retrograde to lean, as a TANGENT fraction. `LandingZoneGuidance`.
        ///
        /// F9I builds the aim as `velVec + errorVec` and then checks whether that leans further than
        /// the allowed angle; if it does - which is nearly always, because the naive sum compares
        /// metres of error against metres per second of velocity and is meaningless when the error
        /// is large - it REBUILDS the direction as
        ///
        ///     retrograde_unit + tan(AoA) * errScale * error_unit
        ///
        /// so the lean is exactly AoA and no more. Returning the tangent fraction lets the glue build
        /// that vector without this file needing vectors.
        /// </summary>
        public static double LeanFraction(double errorMagnitudeM, double aoaDeg)
        {
            double scale = errorMagnitudeM / GuidanceDeadbandM;
            if (scale > 1.0) scale = 1.0;
            if (scale < 0.0) scale = 0.0;
            return System.Math.Tan(aoaDeg * System.Math.PI / 180.0) * scale;
        }

        /// <summary>
        /// The angle of attack to lean by, and ⚠ ITS SIGN FLIPS THE MOMENT THE ENGINES LIGHT.
        ///
        /// `BOOSTER.ks:783`, and the reason is physical rather than a convention:
        ///
        ///   UNPOWERED the lean works AERODYNAMICALLY - the stage flies at an angle of attack and the
        ///   resulting side force walks the impact point. A POSITIVE angle moves it the right way,
        ///   which is why AtmGNC uses +15.
        ///
        ///   UNDER THRUST the force is along the NOSE instead, so the same lean pushes the vehicle
        ///   the OPPOSITE way. The sign has to invert or the guidance drives the error open.
        ///
        /// Under power the authority also tapers with height - 4 degrees down to 1, hitting the floor
        /// at 75 m - so the stage stops steering and starts simply standing up as it arrives.
        /// </summary>
        public static double GuidanceAoaDeg(double altitudeRadarM, bool enginesLit)
        {
            if (!enginesLit) return AeroAoaDeg;

            double a = -(altitudeRadarM / 100.0) - 0.25;
            if (a < PoweredAoaMinDeg) a = PoweredAoaMinDeg;
            if (a > PoweredAoaMaxDeg) a = PoweredAoaMaxDeg;
            return a;
        }

        public static string Name(LandingPhase p)
        {
            switch (p)
            {
                case LandingPhase.Boostback:   return "BOOSTBACK";
                case LandingPhase.Coast:       return "COAST";
                case LandingPhase.EntryBurn:   return "ENTRY BURN";
                case LandingPhase.Descent:     return "DESCENT";
                case LandingPhase.LandingBurn: return "LANDING BURN";
                case LandingPhase.Touchdown:   return "TOUCHDOWN";
                case LandingPhase.NoSolution:  return "NO SOLUTION";
                default:                       return "STANDBY";
            }
        }
    }
}
