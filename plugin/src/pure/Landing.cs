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
        /// <summary>
        /// The turnaround. Engines OFF, three-engine mode selected, aim vector walked round in
        /// small steps. This IS the post-separation wait - F9I measures it settled at 16 s - and
        /// unlike a timer it does useful work while it runs.
        /// </summary>
        Flip,
        /// <summary>
        /// Burning flat-retrograde to kill DOWNRANGE velocity. Ends when the velocity is vertical.
        /// </summary>
        BoostbackKill,
        /// <summary>Burning at the pad's horizon bearing, until the impact point overshoots it.</summary>
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
        /// <summary>The stepped turnaround vector. The glue owns it; see BoosterRecovery.</summary>
        Flip,
        /// <summary>
        /// Retrograde FLATTENED ONTO THE HORIZON, not the full 3D retrograde. BOOSTER.ks:417 -
        /// while the stage is killing downrange velocity it must not pitch down at the ground.
        /// </summary>
        FlatRetrograde,
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
        /// <summary>
        /// Fraction of the RECOVERY propellant still aboard, 1.0 at separation falling to 0.0 when the
        /// recovery tank is dry. The glue measures it (current propellant / propellant at handover). It
        /// is how the entry burn knows to STOP and save fuel for the landing burn - see EntryBurnReserveFrac.
        /// -1 when the glue cannot read it (then the entry burn keeps its speed-based cut).
        /// </summary>
        public double RecoveryPropFrac;

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

        /// <summary>
        /// Distance to the OTHER vehicle, metres. Zero means "nothing to hit" - either it is gone or
        /// we could not measure it.
        ///
        /// ⛔ THIS EXISTS BECAUSE A FIXED COAST IS NOT A SAFETY CONDITION. The 23:19 flight measured
        /// 11.4 m between booster and upper stage 0.6 s after separation, and the gap was still
        /// closing seven seconds later. There is almost no separation impulse, so "wait three
        /// seconds" bought no clearance at all. Distance is the actual condition; time is a proxy
        /// that happened to be wrong.
        /// </summary>
        public double RangeToPartnerM;

        /// <summary>The glue reports the stepped flip has reached its final attitude.</summary>
        public bool FlipDone;

        /// <summary>
        /// Magnitude of the horizontal component of the UNIT retrograde vector. BOOSTER.ks:421 - it
        /// is NOT a speed: it goes to zero when the velocity becomes purely vertical, i.e. when the
        /// stage has stopped travelling downrange at all. 0.03 is that moment to within ~1.7 deg.
        /// </summary>
        public double HorizRetroMag;

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

        /// <summary>
        /// This is a DRONESHIP (ASDS) recovery, not RTLS - the glue sets it from
        /// BoosterRecovery.Profile. A droneship booster keeps going downrange to a barge at its natural
        /// impact point, so it SKIPS the boostback and goes flip -> coast -> entry burn -> landing burn.
        /// Crew-2 flew this. RTLS (false) keeps the boostback burn home.
        /// </summary>
        public bool Droneship;
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

        /// <summary>
        /// RCS wanted. AtmGNC turns it ON for the coast - the fins and cold gas are the only
        /// authority before the engines relight - and Land turns it OFF for the landing burn,
        /// because "below here the gimbal has all the authority needed and the cold gas is only
        /// spending propellant to fight it".
        /// </summary>
        public bool Rcs;

        /// <summary>
        /// May the glue lean the stage off retrograde to walk the impact point onto the pad?
        ///
        /// ⛔ NOT DURING THE ENTRY BURN. BOOSTER.ks:716 is explicit and gives the reason: "Straight
        /// retrograde for the burn, not guidance: the point of the entry burn is to kill speed
        /// through the thickest air, and leaning the stage off retrograde while doing it puts a
        /// large side load on it. Guidance is handed back the moment the burn ends."
        ///
        /// Our glue leaned whenever the aim was SurfaceRetrograde and there was a downrange error,
        /// which is every phase including the entry burn - full thrust, maximum dynamic pressure,
        /// and an angle of attack on top.
        /// </summary>
        public bool GuidedLean;

        /// <summary>
        /// Seconds the steering may take to arrest a rotation, for THIS phase. F9I retunes it three
        /// times down the descent - `maxstoppingtime` 10 through the entry burn, 1 for the glide,
        /// 0.05 for the landing burn - because those phases want opposite behaviour and one setting
        /// cannot serve all three. A big number through the entry burn is what stops the controller
        /// fighting the airflow while the stage is a lawn dart at full throttle.
        /// </summary>
        public double StoppingTime;
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
        /// The entry burn stops once the recovery propellant falls to this fraction, RESERVING the rest
        /// for the landing burn.
        ///
        /// ---- ⛔ WHY A DRONESHIP ENTRY BURN MUST NOT RUN ON THE VERTICAL-SPEED CUT ALONE. ----
        /// F9I's `verticalspeed > -300` cut is an RTLS/Kerbin number: the RTLS booster has already killed
        /// its horizontal velocity in boostback, so retrograde is nearly straight down and the burn is
        /// short. A DRONESHIP booster keeps ~2 km/s of downrange velocity (it must, to reach a barge ~500
        /// km out), so a SurfaceRetrograde burn held to vs > -300 arrests that horizontal too and eats the
        /// WHOLE recovery load: flight_0823_082234 spent 22.8 t of ~22.7 t on the entry burn, left nothing
        /// for the landing ("NO SOLUTION", splashed at 471 m/s), AND killed the downrange so it fell 370 km
        /// short of the barge. Real Crew-2 does two SHORT burns (entry T+7:27, landing T+9:03). Reserving
        /// half the recovery propellant makes the entry burn short, keeps the downrange, and guarantees a
        /// landing burn. 0.5 is a starting split; tune from the barge miss + landing residual.
        /// </summary>
        [Tunable] public static double EntryBurnReserveFrac = 0.5;

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

        /// <summary>
        /// Below this the AoA ceiling follows alt:radar/100; above it, the flat 15. AtmGNC:753
        /// `wait until ship:altitude < 4000` is the gate onto that law.
        /// </summary>
        public const double GuidanceHandAltM = 4000.0;

        /// <summary>
        /// AoA once the stage is on one engine. Land:833 `set F9L_AOA to -0.25.` - "almost no
        /// steering authority left; at this point the only job is to arrive upright." Our taper
        /// floors at -1, so this value was unreachable and the stage kept steering to the ground.
        /// </summary>
        public const double PostHandoverAoaDeg = -0.25;

        /// <summary>
        /// Vertical speed at which the arc is unambiguously over. AtmGNC:666
        /// `wait until ship:verticalspeed < -50`. Until then the nose is held UP.
        /// </summary>
        public const double ArcOverVs = -50.0;

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

        // ---- THE TURNAROUND. BOOSTER.ks:295-381 `Flip1(180, 0.333)`. ----
        /// <summary>Degrees the aim vector advances per tick. A RATE LIMITER, not a torque.</summary>
        public const double FlipPowerDeg = 0.333;
        /// <summary>Settle back onto the ascent track before the flip proper. `wait 2.`</summary>
        /// ⚠ RESTORED to 2.0 on 2026-08-20: the brief "ride the plume" experiment that set this to zero
        /// regressed the flip (user), so it goes back with the rest of that day's flip edits. The flip's
        /// roll problem was never the settle timing - it was the roll REFERENCE (see the roll-lock note
        /// in BoosterRecovery.Aim); do not re-open this without fixing that first.
        public const double FlipSettleS = 2.0;
        /// <summary>
        /// Dead time immediately after separation, before ANY steering. WaitForSep's closing line,
        /// and its reason: "KSP is still resolving the vessel split and the physics of two bodies a
        /// metre apart. Steering into that produces a lurch and can push the booster back into the
        /// upper stage's plume."
        /// </summary>
        public const double SepQuietS = 2.0;
        /// <summary>Total hold before the rotation starts: the quiet, then the settle.</summary>
        public const double FlipHoldS = SepQuietS + FlipSettleS;
        /// <summary>Roll must be within this of the flip plane before pitching starts.</summary>
        public const double FlipRollToleranceDeg = 10.0;
        /// <summary>Floor, so a lucky first frame does not count as settled.</summary>
        public const double FlipRollMinS = 1.0;
        /// <summary>Ceiling, so a stage that will not roll still gets flipped.</summary>
        public const double FlipRollMaxS = 8.0;
        /// <summary>
        /// Coarse phase advances the aim ONLY when the nose is within this of it, so the demand can
        /// never run away from what the vehicle is achieving. BOOSTER.ks:358 - "the stage leads the
        /// target rather than the other way round. This is what stops the flip diverging."
        /// </summary>
        public const double FlipNoseCatchDeg = 7.5;
        /// <summary>Coarse until the aim is this close to final; then advance unconditionally.</summary>
        public const double FlipCoarseDeg = 25.0;
        /// <summary>Fine phase ends here and the aim snaps to the exact final attitude.</summary>
        public const double FlipFineDeg = 15.0;
        /// <summary>Three engines BEFORE the flip - nine on a near-empty stage fight the rotation.</summary>
        public const int FlipEngines = 3;
        /// <summary>Gains are wound UP for the flip: the one moment it must rotate hard.</summary>
        public const double FlipStoppingTime = 3.0;

        // ---- ⛔ DEAD kOS SCALE-FACTOR KNOBS. NOT WIRED. Re-flagged 2026-08-18 (audit D1). ----
        // These four are the literal kOS steering knobs BoosterRecovery.cs:1510 already declares GONE:
        // "maxstoppingtime, pitchts, rollts and rolltorquefactor were kOS's knobs, and porting them
        // was the mistake... pure/Attitude.cs derives the rate bound from the vehicle's own torque and
        // inertia." Grep-confirmed referenced NOWHERE but their own definitions. They read as live
        // settings but drive nothing - the exact "constant that looks like a setting" trap. Kept, not
        // deleted, only as the read-from-source numbers a future roll investigation may need.
        //
        // ⛔ DO NOT reactivate them as a "fix" for the coast/descent roll problem. docs/F9I_BOOSTER_
        // TARGETS.md records this project decoded kOS's rollts/torque knobs once and made the roll
        // measurably WORSE. The roll problem was DIAGNOSED 2026-08-18 (flight_0818_104520): the cause is
        // physical roll AUTHORITY (~10x below pitch/yaw), not a controller knob - see BoosterRecovery.cs
        // ~1595. The audit's H1 (the RollRefMinDeg/RollRefMaxDeg gate) was tested and REFUTED. Neither
        // these dead knobs nor that gate is the fix; reducing the aero roll torque is.

        /// <summary>
        /// DEAD here - not wired. The flip's roll-control range actually comes from the live
        /// AttitudeController.RollControlRangeDeg; this is the dead duplicate with a near-identical
        /// name. Ported from `Flip1` at `BOOSTER.ks:157`, which is live in F9I.
        /// </summary>
        public const double FlipRollControlRangeDeg = 45.0;

        /// <summary>DEAD here - not wired. Ports kOS's `rolltorquefactor` (`BOOSTER.ks:156` `Flip1`),
        /// which is live in F9I; superseded here by Attitude.cs's torque-derived rate bound (see
        /// BoosterRecovery.cs:1510).</summary>
        public const double FlipRollTorqueFactor = 3.0;

        /// <summary>DEAD here - not wired. Ports kOS's `pitchts` (`BOOSTER.ks:152-153`), which is live
        /// in F9I; superseded here by Attitude.cs's torque-derived rate bound (see
        /// BoosterRecovery.cs:1510).</summary>
        public const double FlipPitchYawStoppingScale = 1.5;

        /// <summary>
        /// DEAD here - not wired. Ports kOS's `rollts` ("rollts to 10", `AtmGNC:697`), which is live in
        /// F9I: F9I flies the coast at 3 deg/s of roll, we flew it at 24. Superseded here by
        /// Attitude.cs's torque-derived rate bound (see BoosterRecovery.cs:1510). (The old
        /// "See RollStoppingScale" pointer was removed 2026-08-18 - no such identifier ever existed in
        /// this tree; it was a ghost citation, and audit_comments.py's own docstring cites it as the
        /// canonical example of a name left in a comment after deletion.)
        /// </summary>
        public const double DescentRollStoppingScale = 10.0;

        // ---- THE BURN. BOOSTER.ks:394-518 `Boostback`. ----
        /// <summary>
        /// Steady, not fast. BOOSTER.ks:400 - "the boostback wants a steady hold, not a fast one."
        /// Fifteen times looser than the glide, and the value is the whole difference between a
        /// stage that holds its aim through the burn and one that chases it.
        /// </summary>
        public const double BoostbackStoppingTime = 15.0;
        /// <summary>Horizontal retrograde magnitude at which downrange velocity counts as dead.</summary>
        public const double HorizVelocityDead = 0.03;
        /// <summary>
        /// EngSpl RAMPS the throttle at about this per second rather than stepping it. A step to 1.0
        /// on three Merlins would shock the stage mid-rotation (BOOSTER.ks:379), and a hard CUT at
        /// the end "kicks the stage off the aim it has just spent the whole burn reaching" (:512).
        /// </summary>
        public const double ThrottleRampPerS = 1.333;

        /// <summary>
        /// How far the booster must be from the upper stage before it may light anything, metres.
        ///
        /// 23:19 flight: the booster lit three engines at full throttle 11.4 m from the upper stage,
        /// the vertical gap went NEGATIVE - they passed through each other - and the booster came
        /// apart, 59.20 t down to 9.40 t of debris. A stage that is still alongside its payload does
        /// not get to burn, and no amount of tuning the coast duration fixes that.
        /// </summary>
        public const double SafeSeparationM = 200.0;

        /// <summary>
        /// Longest the booster will wait for clearance before burning anyway, seconds.
        ///
        /// A deadlock here is worse than a close pass: the stage would hold attitude all the way to
        /// the ground with a landing it could still have flown. F9I's own callout schedule has the
        /// flip settled by 16 s, so 20 is past the point where the real vehicle has moved on.
        /// </summary>
        public const double MaxSeparationWaitS = 20.0;

        // ---- STEERING GAIN PER PHASE. BOOSTER.ks sets maxstoppingtime three times. ----
        /// <summary>Through the entry burn (:721). Loose, so the controller does not fight the air.</summary>
        public const double EntryStoppingTime = 10.0;
        /// <summary>The guided glide (:734).</summary>
        public const double GlideStoppingTime = 1.0;
        /// <summary>The landing burn. Tight, because the last hundred metres need it.</summary>
        public const double LandingStoppingTime = 0.05;

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
            // Still alongside whatever we just left. Nothing lights until that is not true.
            if (NearPartner(s)) return LandingPhase.Flip;
            // A droneship booster never boosts back - still climbing just means "coast to entry".
            if (s.VerticalSpeed > 0.0) return s.Droneship ? LandingPhase.Coast : LandingPhase.Boostback;
            if (s.AltitudeAsl <= EntryGateAsl(s)) return LandingPhase.EntryBurn;
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

            // Three from the flip onward - selected BEFORE the rotation, held through both halves
            // of the burn. BOOSTER.ks:326 EngSwitch(0, 1).
            if (phase == LandingPhase.Flip || phase == LandingPhase.BoostbackKill
                || phase == LandingPhase.Boostback)
                return Min(FlipEngines, have);

            // The entry burn opens on the centre engine alone and adds the outboards at 0.75 s. F9I
            // does this with two EngSwitch calls either side of a `wait`; we have no `wait`, so the
            // schedule is expressed against phase-elapsed time and the glue follows it per tick.
            if (phase == LandingPhase.EntryBurn)
                return (s.PhaseElapsedS < EntrySoftStartS)
                     ? Min(1, have)
                     : Min(EntryEngines, have);

            // Everything available, because there is no solution and thrust is all that is left.
            // The switch sets Throttle 1.0 for this phase; without a matching engine count that
            // throttle reaches nothing, which is the defect the sweep names as "asking for thrust
            // means asking for engines".
            if (phase == LandingPhase.NoSolution) return have;

            if (phase != LandingPhase.LandingBurn) return 0;

            // Landing: measure what ONE engine actually gives at this mass. Prefer the vehicle's own
            // centre-engine figure - dividing the all-engine accel by the engine count is the 2.2x
            // error described on AccelOneEngine, and this is the exact test that decides whether the
            // booster commits to a one-engine landing.
            // ---- THE 3 -> 1 HANDOVER. Land:805, and it is not a TWR test. ----
            // BOTH conditions, and F9I is explicit that getting it wrong is not a soft failure:
            // "at low propellant this stage does not have TWR 1 on one Merlin, and a handover that
            // happens too early cannot be undone."
            //     slow enough             verticalspeed > -40
            //     provably able to stop   OneEngStopDist(2.23) * 1.35 < TrueRadar
            // So the burn STARTS on three - which is what makes the 2.23 ratio valid, because AtmGNC
            // leaves the octaweb in three-engine mode - and drops to one only once the stage has
            // earned it. Ours committed to one engine up front on a TWR check.
            double threeAccel = (s.AccelThreeEngine > 0.0) ? s.AccelThreeEngine : s.MaxThrustAccel;
            return HandoverReady(s, threeAccel) ? 1 : Min(3, have);
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
            // ---- THE IGNITION POINT. THIS WAS WRONG IN BOTH ARGUMENTS. ----
            // F9I, LandBurnVars + AtmGNC:757:
            //     F9L_MaxDecel = availablethrust / mass - g       the thrust it ACTUALLY has, which
            //                                                     after the entry burn is THREE
            //     F9L_StopDist = verticalspeed^2 / (2 * MaxDecel) VERTICAL speed, not surface
            //     ignite when F9L_TrueRadar <= F9L_StopDist + F9L_BoosterHeight
            //
            // Ours passed SURFACE speed - much larger on a lofted descent because it carries all the
            // horizontal velocity the landing burn is not trying to arrest - and solved it against
            // ONE engine, which is not even lit yet. Two errors pulling opposite ways, so the
            // altitude came out wrong by a different amount on every flight.
            //
            // The one-engine figure does have a job. It is the HANDOVER test, further down.
            double descentAccel = PhaseAccel(LandingPhase.EntryBurn, s);
            double landAccel = PhaseAccel(LandingPhase.LandingBurn, s);
            // ---- ⛔ THE HOVERSLAM IGNITES ON VERTICAL SPEED, NOT SURFACE SPEED. ----
            // F9I stops on VERTICAL speed. A droneship booster keeps big horizontal velocity down the arc,
            // but DRAG bleeds it: with the entry burn now cut short to reserve landing fuel and PRESERVE
            // the downrange (EntryBurnReserveFrac), the stage reaches the deck with the horizontal mostly
            // gone (flight_0823: 43 m/s horizontal, 227 vertical at touchdown) and only the vertical to
            // kill. Sizing the ignition on the SURFACE speed instead was a mistake: high on the arc that
            // speed is ~1.5 km/s, so the ignition altitude came out 30-70 km and the landing burn LIT AT
            // 48 km, throttled down, shut the engine, and could not relight at the ground (out of ignitions)
            // - the stage fell in at 231 m/s. Vertical speed puts ignition where the hoverslam belongs, a
            // few hundred metres up, one clean light.
            double ign = StopDistance(s.VerticalSpeed, descentAccel, s.Gravity) + BoosterHeightM;
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
                // ⛔ AND THAT MEANS LIGHTING SOMETHING. This returns before `c.Engines` is assigned
                // at the bottom of Guide, so it commanded full throttle with an engine count of
                // ZERO - and SetEngines(v, 0) shuts every engine down. "Everything we have" was
                // nothing at all, on the one path that exists for a stage that cannot stop.
                c.Engines = (s.EngineCount > 0) ? s.EngineCount : 1;
                c.StoppingTime = LandingStoppingTime;
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
            if (phase == LandingPhase.Idle) phase = LandingPhase.Flip;

            // ---- CLEARANCE OVERRIDES EVERYTHING EXCEPT BEING DOWN ----
            // Not just the phase we happen to start in: ANY phase, from any path. If the other
            // vehicle is within reach, the only correct command is hold and burn nothing, and that
            // is true whether we got here through Boostback or through something nobody has thought
            // of yet. Costs one comparison and removes a whole class of "but how would it even get
            // there" reasoning - which is exactly the reasoning that lost a booster.
            // ⚠ AND IT RESPECTS THE TIMEOUT. Written without the elapsed test, this guard ran
            // before the Separating -> Boostback transition and forced Separating back on every
            // tick, so MaxSeparationWaitS could never fire and a stage that never drifted clear
            // would have held attitude to the ground. The sweep caught that within a minute of it
            // being written.
            if (NearPartner(s) && phase != LandingPhase.Touchdown
                && s.PhaseElapsedS < MaxSeparationWaitS)
                phase = LandingPhase.Flip;


            // ---- THE FLIP ENDS WHEN THE STAGE IS ROUND, NOT WHEN A TIMER SAYS SO ----
            // My previous version waited for 200 m of separation or 20 s, which was dead time doing
            // nothing. The wait was never the point; the flip was.
            //
            // ⚠ "F9I's turnaround takes about the same 16 s" stood here and was INVENTED - I wrote
            // it, not F9I. MEASURED on 2026-08-11: ours took 152 s of game time (MET 99 -> 255), and
            // it is rate-limited rather than idle - the stage physically rotates at about 2.5 deg/s on
            // cold gas at 59 t. There is no evidence F9I is faster, and the flip's duration was never
            // what lost a booster; the flip's DIRECTION was. Do not tune against the 16.
            // ---- ⛔ A DRONESHIP BOOSTER DOES NOT BOOST BACK. ----
            // RTLS reverses course to fly home; a droneship (ASDS) booster - Crew-2's profile - keeps
            // going downrange to a barge parked at its natural impact point, so it goes straight from
            // the flip to the coast, skipping BoostbackKill + Boostback entirely. Real Crew-2 booster
            // sequence is flip -> coast -> entry burn (T+7:27) -> landing burn (T+9:03) -> land (T+9:30),
            // with NO boostback burn. See CREW2_RSS_RESEARCH.md.
            else if (phase == LandingPhase.Flip && s.FlipDone && s.Droneship
                     && (!NearPartner(s) || s.PhaseElapsedS >= MaxSeparationWaitS))
                phase = LandingPhase.Coast;

            else if (phase == LandingPhase.Flip && s.FlipDone
                     && (!NearPartner(s) || s.PhaseElapsedS >= MaxSeparationWaitS))
                phase = LandingPhase.BoostbackKill;

            // Downrange velocity is dead - the retrograde direction is now meaningless as a steering
            // reference (BOOSTER.ks:451), so switch to the pad's horizon bearing.
            else if (phase == LandingPhase.BoostbackKill
                     && s.HorizRetroMag <= HorizVelocityDead)
                phase = LandingPhase.Boostback;

            else if (phase == LandingPhase.Boostback && BoostbackDone(s))
                phase = LandingPhase.Coast;

            else if (phase == LandingPhase.Coast && InEntryBand(s)
                     && s.VerticalSpeed < EntryBurnCutVs)
                phase = LandingPhase.EntryBurn;

            else if (phase == LandingPhase.Coast && Hoverslam(s, ign))
                phase = LandingPhase.LandingBurn;

            // F9I cuts on VERTICAL SPEED: `until (ship:verticalspeed > -300)`. A droneship ALSO cuts the
            // instant the recovery propellant hits the reserve, so the landing burn is never starved and
            // the downrange velocity that carries it to the barge is not thrown away. See EntryBurnReserveFrac.
            else if (phase == LandingPhase.EntryBurn
                     && (s.VerticalSpeed > EntryBurnCutVs || !InEntryBand(s)
                         || (s.Droneship && s.RecoveryPropFrac >= 0.0
                             && s.RecoveryPropFrac <= EntryBurnReserveFrac)))
                phase = LandingPhase.Descent;

            else if (phase == LandingPhase.Descent && Hoverslam(s, ign))
                phase = LandingPhase.LandingBurn;

            c.Phase = phase;

            switch (phase)
            {
                case LandingPhase.Flip:
                    // ---- ENGINES OFF, THREE-ENGINE MODE, AIM WALKED ROUND IN STEPS ----
                    // The mode switch is commanded here and not at the burn because it has to be
                    // DONE before the rotation: "nine engines on a nearly empty stage is far more
                    // thrust than the boostback needs and the gimbal authority of the outer ring
                    // fights the rotation" (BOOSTER.ks:324). Engines selected, throttle zero.
                    // Two seconds of nothing while KSP resolves the split, then two settling on
                    // the ascent track, and only then does the aim start walking round. Steering
                    // into an unresolved vessel split lurches the stage back into the plume.
                    c.Aim = (s.PhaseElapsedS < FlipHoldS) ? LandingAim.Hold : LandingAim.Flip;
                    c.Throttle = 0.0;
                    c.StoppingTime = FlipStoppingTime;
                    // ---- ⛔ RCS ON. THIS IS THE ONE THAT COST 152 SECONDS. ----
                    // `Rcs` defaulted to false here, and BoosterRecovery obeys it literally: it
                    // SetGroup(RCS,false). With the throttle at zero there is no gimbal torque
                    // either, so the stage was flipping on REACTION WHEELS ALONE - 4.1 kN.m against
                    // a 4919 t.m^2 pitch inertia. BOOSTER.ks:303 is `set steeringmanager:
                    // maxstoppingtime to 3. rcs on.` on one line, and Flip1 says why: "this is the
                    // one moment the booster is asked to rotate hard, on a nearly empty stage, with
                    // no aerodynamic authority."
                    c.Rcs = true;
                    c.Note = (s.PhaseElapsedS < FlipHoldS) ? "SEP QUIET" : "FLIP";
                    break;

                case LandingPhase.BoostbackKill:
                    // Flat retrograde until the downrange velocity is gone. Throttle RAMPS - EngSpl
                    // walks it up at ~1.333/s rather than stepping, because a step to 1.0 on three
                    // Merlins shocks the stage mid-rotation.
                    c.Aim = LandingAim.FlatRetrograde;
                    c.Throttle = Ramp(s.PhaseElapsedS);
                    c.StoppingTime = BoostbackStoppingTime;
                    // ---- ⚠ OFF UNTIL THE THROTTLE SPOOLS, THEN ON. NOT SIMPLY ON. ----
                    // BOOSTER.ks does `rcs off` on Boostback's FIRST LINE (:408) and `rcs on` only
                    // once EngSpl(1) starts spooling (:429). The stage establishes its aim on wheels
                    // and gimbal, and cold gas joins when there is thrust to steer. Reproduced from
                    // the ramp rather than a second timer, so the two cannot drift apart.
                    c.Rcs = Ramp(s.PhaseElapsedS) > 0.0;
                    c.Note = "BOOSTBACK - KILLING DOWNRANGE";
                    break;

                    // (The old Separating case stood here. It was a 200 m / 20 s hold that did
                    // nothing but wait, and it is superseded by the Flip above, which spends the time
                    // rotating the stage and stepping down to three engines. Its one real lesson
                    // survives in the clearance guard at the top of the transitions: nothing burns or
                    // slews while the two vehicles are alongside.)

                case LandingPhase.Boostback:
                    c.Aim = LandingAim.TowardTarget;
                    // ---- THROTTLE ON THE FRACTION OF THE ERROR STILL LEFT ----
                    // BOOSTER.ks:478 - `set throt to max(0.25, impDist / intDist)`. Burns hard while
                    // the error is large and eases as it closes, which is what stops the boostback
                    // overshooting its own overshoot target. The floor is not a minimum useful
                    // thrust: it keeps the engines lit so the gimbal stays authoritative.
                    c.Throttle = BoostbackThrottle(s);
                    c.StoppingTime = BoostbackStoppingTime;
                    // Lit throughout this phase, so RCS is on - BOOSTER.ks:429 onward.
                    c.Rcs = true;
                    c.Note = "BOOSTBACK";
                    break;

                case LandingPhase.Coast:
                    // ---- NOSE UP THROUGH THE ARC. WE WERE COASTING RETROGRADE. ----
                    // AtmGNC:665 `lock steering to up.` then `wait until ship:verticalspeed < -50`.
                    // The boostback ends well BEFORE apoapsis, so the stage is still climbing - and
                    // retrograde while climbing points it at the ground, which is the wrong attitude
                    // for the arc and the wrong one to meet the air in.
                    c.Throttle = 0.0;
                    c.StoppingTime = GlideStoppingTime;
                    // ---- ⛔ RCS ON FOR THE WHOLE COAST, NOT JUST THE DESCENT. ----
                    // The engines are out (no gimbal) and the grid fins are stowed, so the ONLY attitude
                    // authority on a coasting booster is the cold-gas RCS - exactly what the real Falcon 9
                    // holds attitude on across the coast. This branch (climbing over the top) left Rcs at
                    // its default FALSE, so from the flip's residual rate the stage had nothing to arrest
                    // it and tumbled - attitude error swinging 0-166 deg with the actuators railed at +-1
                    // doing nothing (flight_0823, crew: "left tumbling through space"). RCS belongs on
                    // both halves of the coast; only the AIM differs (nose up over the top, retrograde down).
                    c.Rcs = true;
                    if (s.VerticalSpeed > ArcOverVs)
                    {
                        c.Aim = LandingAim.Up;
                        c.Note = "COAST - OVER THE TOP";
                    }
                    else
                    {
                        // ---- ⛔ COAST IS PURE SURFACE RETROGRADE. NO LEAN. (user, 2026-08-19) ----
                        // "after boost back burn you command all kinds of AOA changes when all you
                        // really have to do is point retrograde (surface not orbital) and wait for the
                        // atmosphere braking burn." The coast has no job but to present the heat shield
                        // to the airflow and hold; the guided lean that steers toward the pad belongs to
                        // the DESCENT glide (after the entry burn), where there is air to steer in and a
                        // pad to steer at. Leaning up here - in negligible dynamic pressure - only wags
                        // the stage and cross-couples into the weak roll axis. So GuidedLean is OFF:
                        // SurfaceRetrograde aims at -srf_velocity (Aim():1391), roll stays locked
                        // (upHint = zero), and the law resumes at Descent, not here.
                        c.Aim = LandingAim.SurfaceRetrograde;
                        c.Rcs = true;
                        c.Note = "COAST - DESCENDING";
                    }
                    break;

                case LandingPhase.NoSolution:
                    // Reachable as an INPUT phase, not only through the early return above, and the
                    // switch had no case for it - so it rendered the default: no throttle, no gain,
                    // no engines. A stage that cannot stop was being told to do nothing at all.
                    c.Throttle = 1.0;
                    c.StoppingTime = LandingStoppingTime;
                    c.Note = "NO SOLUTION - TWR BELOW 1";
                    break;

                case LandingPhase.EntryBurn:
                    c.Throttle = 1.0;
                    // Straight retrograde. No lean - see GuidedLean.
                    c.StoppingTime = EntryStoppingTime;
                    // AtmGNC:659 `rcs on` covers the whole of the coast, the entry burn and the
                    // guided descent. Only Land() turns it off again, at :781.
                    c.Rcs = true;
                    c.Note = "ENTRY BURN";
                    break;

                case LandingPhase.Descent:
                    c.Throttle = 0.0;
                    // Guidance is handed back the moment the burn ends. This is the glide where the
                    // stage actually steers, and it does it with drag, which is far cheaper than
                    // propellant.
                    c.GuidedLean = true;
                    c.StoppingTime = GlideStoppingTime;
                    c.Rcs = true;                       // still inside AtmGNC's `rcs on`
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
                    // ⛔ AND IF WE HAVE OVER-BRAKED INTO A CLIMB, STOP PUSHING. BurnThrottle is
                    // StopDist/TrueRadar, and StopDist is computed from SPEED - it does not care
                    // which way the speed points, so a stage that over-corrected upward would read a
                    // huge stopping distance and hold full throttle while flying away from the pad.
                    if (s.VerticalSpeed > 0.0) th = 0.0;
                    c.Throttle = th;
                    c.Aim = (s.AltitudeRadar < 60.0) ? LandingAim.Up : LandingAim.SurfaceRetrograde;
                    // Still steering to the pad, and now the only thing that can.
                    c.GuidedLean = true;
                    c.StoppingTime = LandingStoppingTime;
                    c.Rcs = false;          // gimbal has it from here; cold gas only fights it
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
            // TrueRadar, not alt:radar - the bells are BoosterHeightM below what the altimeter
            // reports, and that is most of a landing burn's final margin.
            return s.VerticalSpeed < 0.0 && TrueRadar(s) <= ign;
        }

        /// <summary>
        /// Distance needed to arrest a VERTICAL descent rate. F9I `F9L_StopDist`: v^2/2a with
        /// gravity already taken out of a. Returns an unreachably large number when there is no
        /// deceleration to be had, so every "is there room" test fails instead of dividing by zero.
        /// </summary>
        public static double StopDistance(double verticalSpeed, double thrustAccel, double gravity)
        {
            double decel = thrustAccel - gravity;
            if (decel <= 0.1) return 999999.0;
            return verticalSpeed * verticalSpeed / (2.0 * decel);
        }

        /// <summary>
        /// Longest a boostback may burn before it is stopped regardless, seconds.
        ///
        /// PredictedMiss returns 0 when it cannot solve, and `0 < -2700` is false for ever - so an
        /// unsolvable prediction would have burned the stage dry with no landing propellant and no
        /// error anywhere. A termination condition that can silently never fire is not one.
        /// F9I's measured boostback ends 54-58 s after separation, so 75 s is well past nominal.
        /// </summary>
        public const double MaxBoostbackS = 75.0;

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
            if (s.PhaseElapsedS >= MaxBoostbackS) return true;      // never burn for ever
            return s.PredictedMissM < -BoostbackOvershootM;
        }

        /// <summary>Is the other vehicle close enough that lighting an engine would hit it?</summary>
        public static bool NearPartner(LandingInputs s)
        {
            return s.RangeToPartnerM > 0.0 && s.RangeToPartnerM < SafeSeparationM;
        }

        /// <summary>
        /// EngSpl's ramp, as a throttle against seconds since the burn began. BOOSTER.ks calls
        /// EngSpl(1) rather than setting throttle, and EngSpl walks it - a step to full on three
        /// Merlins mid-rotation is a shock the stage does not need.
        /// </summary>
        public static double Ramp(double elapsedS)
        {
            double t = elapsedS * ThrottleRampPerS;
            if (t < 0.0) return 0.0;
            return (t > 1.0) ? 1.0 : t;
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
        /// <summary>32.5 km over Kerbin's 70 km atmosphere = this fraction; RSS/Earth's deeper air
        /// moves the gate up proportionally, to ~65 km (real Falcon 9 entry burn is ~55-70 km).</summary>
        public const double EntryGateFraction = 32500.0 / 70000.0;

        /// <summary>The entry-burn altitude gate for THIS body, metres ASL - atmosphere-relative so it
        /// is right on Earth, and reproduces the flown 32.5 km on Kerbin. Falls back to the const.</summary>
        public static double EntryGateAsl(LandingInputs s)
        {
            return (s.AtmosphereDepthM > 0.0) ? s.AtmosphereDepthM * EntryGateFraction : EntryBurnGateAsl;
        }

        private static bool InEntryBand(LandingInputs s)
        {
            return s.AltitudeAsl < EntryGateAsl(s) && s.AltitudeRadar > 1000.0;
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

        /// <summary>
        /// The landing roll reference is used only when the aim is between these angles from it.
        /// `BOOSTER.ks:367-368`, both escapes meaning "keep the roll you have".
        ///
        /// ⚠ NOT TUNING. `lookdirup` - and Unity's `Quaternion.LookRotation` behind our own
        /// SteerTo - has no defined answer when the look direction is parallel to the up reference,
        /// and a booster standing vertically over its pad is exactly parallel to a horizontal north
        /// reference. Outside this band the roll must be left alone or the command is a singularity.
        /// </summary>
        public const double RollRefMinDeg = 15.0, RollRefMaxDeg = 165.0;

        /// <summary>Unpowered aerodynamic trim during the descent. `F9L_AOA` = 15.</summary>
        public const double AeroAoaDeg = 15.0;

        /// <summary>Powered lean at ignition, and the floor/ceiling of the taper.</summary>
        public const double PoweredAoaStartDeg = -3.0;
        public const double PoweredAoaMinDeg = -4.0, PoweredAoaMaxDeg = -1.0;

        /// <summary>
        /// How far off retrograde to lean, as a TANGENT fraction. `LandingZoneGuidance`.
        ///
        /// F9I builds the aim as `velVec + errorVec` and then checks whether that leans further than
        /// the allowed angle; if it does it REBUILDS the direction as
        ///
        ///     retrograde_unit + tan(AoA) * errScale * error_unit
        ///
        /// so the lean is exactly AoA and no more. Returning the tangent fraction lets the glue build
        /// that vector without this file needing vectors.
        ///
        /// ⛔ THIS IS THE CLAMPED CASE ONLY, AND THAT SENTENCE USED TO BE MISSING. This comment
        /// once claimed the rebuild fires "nearly always", which is what justified the glue applying
        /// it unconditionally - and that is what made the booster swing. At 250 m/s a 100 m impact
        /// error leans only 21.8 degrees, under every ceiling in the schedule, so the rebuild does
        /// NOT fire and F9I flies `velVec + errorVec` directly. The caller must test the angle first.
        /// </summary>
        /// <remarks>
        /// ⛔ <paramref name="errorMagnitudeM"/> MUST be the magnitude of the very vector whose
        /// direction the caller leans along. The glue passed downrange instead, which is never small,
        /// so this returned a full lean along an azimuth derived from a few metres of noise. See the
        /// argument at the call site in `BoosterRecovery`.
        /// </remarks>
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
            return GuidanceAoaDeg(altitudeRadarM, enginesLit, false);
        }

        /// <summary>As above, but <paramref name="handedOver"/> once the stage is on one engine.</summary>
        public static double GuidanceAoaDeg(double altitudeRadarM, bool enginesLit, bool handedOver)
        {
            // Past the handover the stage stops steering and simply stands up.
            if (enginesLit && handedOver) return PostHandoverAoaDeg;
            // ---- UNPOWERED: 15 deg HIGH UP, alt/100 BELOW 4 km ----
            // `BOOSTER.ks:455-459`, verbatim:
            //
            //     wait until ship:altitude < 4000.
            //     until (F9L_TrueRadar <= (F9L_StopDist + F9L_BoosterHeight)) {
            //         if (F9L_AOA > 15) { set F9L_AOA to 15. }
            //         if (ship:altitude < 10000) { set F9L_AOA to (alt:radar / 100). }
            //     }
            //
            // The loop is entered only below 4 km, so the second line always fires and the clamp on
            // the first is overwritten before it is used. Above 4 km `F9L_AOA` is still the 15 set at
            // :437. Hence 15 high up, alt/100 below - 40 deg at 4 km, decaying with height.
            //
            // ⚠ AND IT IS A CEILING, NOT A DEMAND. `LandingZoneGuidance` only rebuilds the aim
            // when the natural lean already exceeds this angle - see the port note at the call site
            // in `BoosterRecovery`. Reading this number as a commanded lean is what made the stage
            // swing; forty degrees is the most it may ever be asked for, not what it is asked for.
            if (!enginesLit)
            {
                if (altitudeRadarM >= GuidanceHandAltM) return AeroAoaDeg;
                // ---- ⛔ A PLAIN TAPER. THE max() HERE WAS MINE AND IT WAS WRONG. ----
                // BOOSTER.ks:755 is `set F9L_AOA to (alt:radar / 100).` and nothing else - so F9I
                // runs 40 deg at 4 km down to 1 deg at 100 m, giving the stage less and less
                // authority as it runs out of height to use it in. Flooring it at 15 held a
                // fifteen-degree angle of attack all the way to touchdown, and made the command
                // JUMP from 15 to 40 crossing 4 km instead of continuing smoothly.
                return altitudeRadarM / 100.0;
            }

            double a = -(altitudeRadarM / 100.0) - 0.25;
            if (a < PoweredAoaMinDeg) a = PoweredAoaMinDeg;
            if (a > PoweredAoaMaxDeg) a = PoweredAoaMaxDeg;
            return a;
        }

        public static string Name(LandingPhase p)
        {
            switch (p)
            {
                case LandingPhase.Flip:        return "FLIP";
                case LandingPhase.BoostbackKill: return "BOOSTBACK KILL";
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
