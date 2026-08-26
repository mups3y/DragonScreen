// DragonScreen - Landing
// ---- THE LANDING LAW IS MECHJEB'S, READ FROM SOURCE, AND IT IS THE RIGHT ONE ----
// ---- WHAT IS SPACEX'S METHOD AND WHAT IS OURS ----
// ---- THE ONE NUMBER THAT BITES ----
namespace DragonScreen
{
    public enum LandingPhase : byte
    {
        Idle = 0,
        Flip,
        BoostbackKill,
        Boostback,
        Coast,
        EntryBurn,
        Descent,
        LandingBurn,
        Touchdown,
        NoSolution
    }

    public enum LandingAim : byte
    {
        Hold = 0,
        SurfaceRetrograde,
        TowardTarget,
        Flip,
        FlatRetrograde,
        Up
    }

    public struct LandingInputs
    {
        public bool Valid;
        public double AltitudeRadar;
        public double AltitudeAsl;
        public double VerticalSpeed;
        public double HorizontalSpeed;
        public double SurfaceSpeed;
        public double MaxThrustAccel;
        public double Mass;
        public int EngineCount;
        public double RecoveryPropFrac;

        public double TerminalSpeedMps;
        public double RecoveryPropKg;

        // ---- ⚠ THE OCTAWEB'S MODES ARE NOT MULTIPLES OF ONE ENGINE ----
        public double AccelThreeEngine;
        public double AccelOneEngine;

        public double PhaseElapsedS;
        public double Gravity;
        public double DownrangeM;

        public double RangeToPartnerM;

        public bool FlipDone;

        public double HorizRetroMag;

        // ---- THE BOOSTBACK IS FLOWN AGAINST A PREDICTED IMPACT POINT, NOT A VELOCITY ----
        public double PredictedMissM;
        public bool PredictedMissValid;
        public double DownMissM;
        public double CrossMissM;
        public double InitialMissM;
        public double AtmosphereDepthM;
        public double DynamicPressureKpa;
        public bool Landed;

        public bool Droneship;
    }

    public struct LandingCommand
    {
        public LandingPhase Phase;
        public double Throttle;
        public LandingAim Aim;
        public string Note;
        public double IgnitionAltitude;
        public int Engines;
        public bool DeployLegs;

        public bool Rcs;

        public bool GuidedLean;

        public double StoppingTime;
    }

    public static class Landing
    {
        public const double SpeedMargin = 0.9;

        // ---- EVERY NUMBER BELOW IS F9I'S, AND FLOWN. RETUNE THEM IN BOOSTER.ks, NOT HERE. ----

        public const double EntryBurnGateAsl = 32500.0;

        public const double EntryBurnCutVs = -300.0;

        [Tunable] public static double EntryBurnTargetSpeedMps = 1300.0;

        [Tunable] public static double EntryAimBufferM = 2000.0;

        [Tunable] public static double EntryBurnMinSpeedMps = 700.0;

        /// ---- ⛔ WHY A DRONESHIP ENTRY BURN MUST NOT RUN ON THE VERTICAL-SPEED CUT ALONE. ----
        /// ---- MEASURED (2026-08-23) ----
        /// ---- REFINED 0.35 -> 0.20 (2026-08-24), from the first SOFT touchdown ----
        [Tunable] public static double EntryBurnReserveFrac = 0.20;

        public const double BoosterHeightM = 31.02;

        /// ---- ⛔ WHY A PURE SUICIDE BURN IS UNFLYABLE UNDER TestFlight ----
        [Tunable] public static double LandingIgnitionLeadS = 0.3;

        [Tunable] public static double LandingSpoolS = 1.2;

        [Tunable] public static double LandingDeadTimeS = 6.0;

        public const double MerlinIspS = 282.0;

        public const double BulkMargin = 0.06;

        public const double FlareRadarM = 25.0, FlareMargin = 0.34;

        public const double OneEngineRatio = 2.23;

        public const double HandoverPad = 1.35, HandoverVs = -80.0;

        [Tunable] public static double HandoverVsMps = 60.0;

        [Tunable] public static double HandoverEnvPad = 1.12;

        [Tunable] public static double HandoverGapS = 0.6;

        [Tunable] public static double LandingMinThrottle = 0.0;

        public const double GuidanceHandAltM = 4000.0;

        public const double PostHandoverAoaDeg = -0.25;

        public const double ArcOverVs = -50.0;

        public const double LegsAltitudeM = 200.0;

        public const double BoostbackTolerance = 400.0;

        public const double BoostbackOvershootM = 2700.0;

        public const double BoostbackMinThrottle = 0.25;

        // ---- THE TURNAROUND. BOOSTER.ks:295-381 `Flip1(180, 0.333)`. ----
        public const double FlipPowerDeg = 0.333;
        public const double FlipSettleS = 2.0;
        public const double SepQuietS = 2.0;
        public const double FlipHoldS = SepQuietS + FlipSettleS;
        public const double FlipRollToleranceDeg = 10.0;
        public const double FlipRollMinS = 1.0;
        public const double FlipRollMaxS = 8.0;
        public const double FlipNoseCatchDeg = 7.5;
        public const double FlipCoarseDeg = 25.0;
        public const double FlipFineDeg = 15.0;
        public const int FlipEngines = 3;
        public const double FlipStoppingTime = 3.0;

        // ---- ⛔ DEAD kOS SCALE-FACTOR KNOBS. NOT WIRED. Re-flagged 2026-08-18 (audit D1). ----

        public const double FlipRollControlRangeDeg = 45.0;

        public const double FlipRollTorqueFactor = 3.0;

        public const double FlipPitchYawStoppingScale = 1.5;

        public const double DescentRollStoppingScale = 10.0;

        // ---- THE BURN. BOOSTER.ks:394-518 `Boostback`. ----
        public const double BoostbackStoppingTime = 15.0;
        public const double HorizVelocityDead = 0.03;
        public const double ThrottleRampPerS = 1.333;

        public const double SafeSeparationM = 200.0;

        public const double MaxSeparationWaitS = 20.0;

        // ---- STEERING GAIN PER PHASE. BOOSTER.ks sets maxstoppingtime three times. ----
        public const double EntryStoppingTime = 10.0;
        public const double GlideStoppingTime = 1.0;
        public const double LandingStoppingTime = 0.05;

        public const double TouchdownAltitude = 3.0, TouchdownSpeed = 3.0;

        // ------------------------------------------------------------------ the core law

        public static double MaxAllowedSpeed(double altitude, double thrustAccel, double gravity)
        {
            double net = thrustAccel - gravity;
            if (net <= 0.0 || altitude <= 0.0) return 0.0;
            return SpeedMargin * System.Math.Sqrt(2.0 * net * altitude);
        }

        public static double IgnitionAltitude(double speed, double thrustAccel, double gravity)
        {
            double net = thrustAccel - gravity;
            if (net <= 0.0) return 0.0;
            return (speed * speed) / (2.0 * net * SpeedMargin * SpeedMargin);
        }

        public static double TimeToGround(double altitude, double verticalSpeed, double gravity)
        {
            if (gravity <= 0.0) return 0.0;
            double disc = verticalSpeed * verticalSpeed + 2.0 * gravity * altitude;
            if (disc < 0.0) return 0.0;
            return (verticalSpeed + System.Math.Sqrt(disc)) / gravity;
        }

        public static double RequiredClosingSpeed(LandingInputs s)
        {
            double t = TimeToGround(s.AltitudeRadar, s.VerticalSpeed, s.Gravity);
            if (t < 1.0) return 0.0;
            return s.DownrangeM / t;
        }

        // ------------------------------------------------------------------ where to pick it up

        /// ---- WHY THIS IS NOT ALWAYS Boostback ----
        public static LandingPhase InitialPhase(LandingInputs s)
        {
            if (s.Landed) return LandingPhase.Touchdown;
            if (NearPartner(s)) return LandingPhase.Flip;
            if (s.VerticalSpeed > 0.0) return s.Droneship ? LandingPhase.Coast : LandingPhase.Boostback;
            if (s.AltitudeAsl <= EntryGateAsl(s)) return LandingPhase.EntryBurn;
            return LandingPhase.Coast;
        }

        // ------------------------------------------------------------------ engine selection

        public const int BoostbackEngines = 3, EntryEngines = 3;

        public const double MinLandingTwr = 1.25;

        /// ---- THIS IS THE REAL PROFILE, AND IT IS ALSO A TRAP WE HAVE ALREADY FALLEN INTO ----
        public const double EntrySoftStartS = 0.75;

        public static bool FiresEngine(LandingPhase phase)
        {
            return phase == LandingPhase.BoostbackKill || phase == LandingPhase.Boostback
                || phase == LandingPhase.EntryBurn || phase == LandingPhase.LandingBurn
                || phase == LandingPhase.NoSolution;
        }

        public static int EnginesFor(LandingPhase phase, LandingInputs s)
        {
            int have = (s.EngineCount > 0) ? s.EngineCount : 1;

            if (phase == LandingPhase.Flip || phase == LandingPhase.BoostbackKill
                || phase == LandingPhase.Boostback)
                return Min(FlipEngines, have);

            if (phase == LandingPhase.EntryBurn)
                return (s.PhaseElapsedS < EntrySoftStartS)
                     ? Min(1, have)
                     : Min(EntryEngines, have);

            if (phase == LandingPhase.NoSolution) return have;

            // ---- ⛔ NO MID-BURN 3->1 SWITCH. IT FREE-FALLS, AND THE REAL FALCON DOES NOT. ----
            if (phase == LandingPhase.Descent || phase == LandingPhase.LandingBurn)
                return LandingEngines(s, have);

            return 0;
        }

        public static int LandingEngines(LandingInputs s, int have)
        {
            double h = TrueRadar(s);
            if (s.AccelOneEngine > 0.0 && s.AccelOneEngine >= s.Gravity * MinLandingTwr
                && CanArrest(s, s.AccelOneEngine, h))
                return Min(1, have);
            if (s.AccelThreeEngine > 0.0 && CanArrest(s, s.AccelThreeEngine, h))
                return Min(3, have);
            if (s.AccelOneEngine <= 0.0 && s.AccelThreeEngine <= 0.0)
                return Min(3, have);
            return have;
        }

        public static bool CanArrest(LandingInputs s, double accel, double heightM)
        {
            double net = accel - s.Gravity;
            if (net <= 0.1 || heightM <= 0.0) return false;
            double vDown = (s.VerticalSpeed < 0.0) ? -s.VerticalSpeed : 0.0;
            double stop = vDown * vDown / (2.0 * net);
            return stop < heightM;
        }

        public static double LandingFuelKg(LandingInputs s)
        {
            if (s.TerminalSpeedMps <= 0.0 || s.Mass <= 0.0) return 0.0;
            int have = (s.EngineCount > 0) ? s.EngineCount : 1;
            int landEng = LandingEngines(s, have);
            double aLand = (landEng <= 1 && s.AccelOneEngine > 0.0) ? s.AccelOneEngine
                         : (landEng == 3 && s.AccelThreeEngine > 0.0 ? s.AccelThreeEngine
                            : (s.MaxThrustAccel > 0.0 ? s.MaxThrustAccel : s.AccelThreeEngine));
            double net = aLand - s.Gravity;
            if (aLand <= 0.0 || net <= 0.1) return 0.0;
            double dvLand = s.TerminalSpeedMps * aLand / net;
            double ve = MerlinIspS * 9.80665;
            double frac = 1.0 - System.Math.Exp(-dvLand / ve);
            return s.Mass * 1000.0 * frac;
        }

        [Tunable] public static double LandingReserveMargin = 0.4;

        public static bool LandingReserveReached(LandingInputs s)
        {
            double needKg = LandingFuelKg(s);
            if (needKg > 0.0 && s.RecoveryPropKg > 0.0)
                return s.RecoveryPropKg <= needKg * (1.0 + LandingReserveMargin);
            return s.RecoveryPropFrac >= 0.0 && s.RecoveryPropFrac <= EntryBurnReserveFrac;
        }

        private static int Min(int a, int b) { return a < b ? a : b; }

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

            // ---- THE IGNITION POINT. THIS WAS WRONG IN BOTH ARGUMENTS. ----
            double landAccel = PhaseAccel(LandingPhase.LandingBurn, s);
            // ---- ⛔ THE HOVERSLAM IGNITES ON VERTICAL SPEED, NOT SURFACE SPEED. ----
            // ---- DRAG-AWARE HOVERSLAM IGNITION, ONE CONTINUOUS ENGINE MODE (pure/Hoverslam.cs). ----
            double vTerm = (s.VerticalSpeed < 0.0) ? -s.VerticalSpeed : 1.0;
            double vDown = (s.VerticalSpeed < 0.0) ? -s.VerticalSpeed : 0.0;
            double thrustLandKn = landAccel * s.Mass;
            double ign;
            if (thrustLandKn > 0.0 && s.Mass > 0.0)
            {
                HoverslamInputs hs = new HoverslamInputs
                {
                    AltitudeM = s.AltitudeRadar,
                    VerticalSpeed = s.VerticalSpeed,
                    MassT = s.Mass,
                    GravityMps2 = s.Gravity,
                    ThrustKn = thrustLandKn,
                    MdotTps = (MerlinIspS > 0.0) ? thrustLandKn / (MerlinIspS * 9.80665) : 0.0,
                    DragRefAccel = s.Gravity,
                    DragRefSpeed = vTerm,
                    DeadTimeS = LandingDeadTimeS,
                    SpoolS = LandingSpoolS
                };
                ign = HoverslamSolver.IgnitionAltitude(hs) + BoosterHeightM + LandingIgnitionLeadS * vDown;
            }
            else
            {
                ign = StopDistance(s.VerticalSpeed, landAccel, s.Gravity) + BoosterHeightM
                    + LandingIgnitionLeadS * vDown;
            }
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
            if (landAccel <= s.Gravity && s.AltitudeRadar < s.AtmosphereDepthM * 0.5)
            {
                c.Phase = LandingPhase.NoSolution;
                c.Throttle = 1.0;
                c.Engines = (s.EngineCount > 0) ? s.EngineCount : 1;
                c.StoppingTime = LandingStoppingTime;
                c.Note = "NO SOLUTION - TWR BELOW 1";
                return c;
            }

            // ---- TRANSITIONS FIRST, THEN THE COMMAND FOR WHATEVER PHASE WE LANDED IN ----
            // ---- ⛔ ONE TRANSITION PER TICK. THESE WERE SEQUENTIAL `if`s AND IT COST THE RTLS. ----
            if (phase == LandingPhase.Idle) phase = LandingPhase.Flip;

            // ---- CLEARANCE OVERRIDES EVERYTHING EXCEPT BEING DOWN ----
            if (NearPartner(s) && phase != LandingPhase.Touchdown
                && s.PhaseElapsedS < MaxSeparationWaitS)
                phase = LandingPhase.Flip;

            // ---- THE FLIP ENDS WHEN THE STAGE IS ROUND, NOT WHEN A TIMER SAYS SO ----
            // ---- ⛔ A DRONESHIP BOOSTER DOES NOT BOOST BACK. ----
            else if (phase == LandingPhase.Flip && s.FlipDone && s.Droneship
                     && (!NearPartner(s) || s.PhaseElapsedS >= MaxSeparationWaitS))
                phase = LandingPhase.Coast;

            else if (phase == LandingPhase.Flip && s.FlipDone
                     && (!NearPartner(s) || s.PhaseElapsedS >= MaxSeparationWaitS))
                phase = LandingPhase.BoostbackKill;

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

            // ---- DRONESHIP: the entry burn TARGETS THE BARGE. Bleed retrograde (which shortens the
            // (EntryBurnMinSpeedMps) and the reserve floor. RTLS keeps F9I's vertical-speed cut. ----
            else if (phase == LandingPhase.EntryBurn
                     && ((s.Droneship ? (EntryAimReached(s) || s.SurfaceSpeed <= EntryBurnMinSpeedMps)
                                      : s.VerticalSpeed > EntryBurnCutVs)
                         || !InEntryBand(s)
                         || (s.Droneship && LandingReserveReached(s))))
                phase = LandingPhase.Descent;

            else if (phase == LandingPhase.Descent && Hoverslam(s, ign))
                phase = LandingPhase.LandingBurn;

            c.Phase = phase;

            switch (phase)
            {
                case LandingPhase.Flip:
                    // ---- ENGINES OFF, THREE-ENGINE MODE, AIM WALKED ROUND IN STEPS ----
                    c.Aim = (s.PhaseElapsedS < FlipHoldS) ? LandingAim.Hold : LandingAim.Flip;
                    c.Throttle = 0.0;
                    c.StoppingTime = FlipStoppingTime;
                    // ---- ⛔ RCS ON. THIS IS THE ONE THAT COST 152 SECONDS. ----
                    c.Rcs = true;
                    c.Note = (s.PhaseElapsedS < FlipHoldS) ? "SEP QUIET" : "FLIP";
                    break;

                case LandingPhase.BoostbackKill:
                    c.Aim = LandingAim.FlatRetrograde;
                    c.Throttle = Ramp(s.PhaseElapsedS);
                    c.StoppingTime = BoostbackStoppingTime;
                    // ---- ⚠ OFF UNTIL THE THROTTLE SPOOLS, THEN ON. NOT SIMPLY ON. ----
                    c.Rcs = Ramp(s.PhaseElapsedS) > 0.0;
                    c.Note = "BOOSTBACK - KILLING DOWNRANGE";
                    break;

                case LandingPhase.Boostback:
                    c.Aim = LandingAim.TowardTarget;
                    // ---- THROTTLE ON THE FRACTION OF THE ERROR STILL LEFT ----
                    c.Throttle = BoostbackThrottle(s);
                    c.StoppingTime = BoostbackStoppingTime;
                    c.Rcs = true;
                    c.Note = "BOOSTBACK";
                    break;

                case LandingPhase.Coast:
                    // ---- NOSE UP THROUGH THE ARC. WE WERE COASTING RETROGRADE. ----
                    c.Throttle = 0.0;
                    c.StoppingTime = GlideStoppingTime;
                    // ---- ⛔ RCS ON FOR THE WHOLE COAST, NOT JUST THE DESCENT. ----
                    c.Rcs = true;
                    if (s.VerticalSpeed > ArcOverVs)
                    {
                        c.Aim = LandingAim.Up;
                        c.Note = "COAST - OVER THE TOP";
                    }
                    else
                    {
                        // ---- ⛔ COAST IS PURE SURFACE RETROGRADE. NO LEAN. (user, 2026-08-19) ----
                        c.Aim = LandingAim.SurfaceRetrograde;
                        c.Rcs = true;
                        c.Note = "COAST - DESCENDING";
                    }
                    break;

                case LandingPhase.NoSolution:
                    c.Throttle = 1.0;
                    c.StoppingTime = LandingStoppingTime;
                    c.Note = "NO SOLUTION - TWR BELOW 1";
                    break;

                case LandingPhase.EntryBurn:
                    c.Throttle = 1.0;
                    c.StoppingTime = EntryStoppingTime;
                    c.Rcs = true;
                    c.Note = "ENTRY BURN";
                    break;

                case LandingPhase.Descent:
                    c.Throttle = 0.0;
                    c.GuidedLean = true;
                    c.StoppingTime = GlideStoppingTime;
                    c.Rcs = true;
                    c.Note = "DESCENT";
                    break;

                case LandingPhase.LandingBurn:
                {
                    // ---- F9I'S SUICIDE BURN, PORTED WHOLE ----
                    double margin = (TrueRadar(s) < FlareRadarM) ? FlareMargin : BulkMargin;
                    double th = BurnThrottle(s, landAccel) + margin;
                    if (th < 0.0) th = 0.0; else if (th > 1.0) th = 1.0;
                    // ---- ONE CONTINUOUS MODE: THE HOVERSLAM MODULATES IT THE WHOLE WAY. ----
                    if (th > 0.0 && th < LandingMinThrottle) th = LandingMinThrottle;
                    if (s.VerticalSpeed > 0.0) th = 0.0;
                    c.Throttle = th;
                    c.Aim = (s.AltitudeRadar < 60.0) ? LandingAim.Up : LandingAim.SurfaceRetrograde;
                    c.GuidedLean = true;
                    c.StoppingTime = LandingStoppingTime;
                    c.Rcs = false;
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

        private static bool Hoverslam(LandingInputs s, double ign)
        {
            return s.VerticalSpeed < 0.0 && TrueRadar(s) <= ign;
        }

        public static double StopDistance(double verticalSpeed, double thrustAccel, double gravity)
        {
            double decel = thrustAccel - gravity;
            if (decel <= 0.1) return 999999.0;
            return verticalSpeed * verticalSpeed / (2.0 * decel);
        }

        public const double MaxBoostbackS = 75.0;

        /// ---- WHY OVERSHOOT ON PURPOSE ----
        /// ---- WHAT THIS REPLACED ----
        private static bool BoostbackDone(LandingInputs s)
        {
            if (s.PhaseElapsedS >= MaxBoostbackS) return true;
            return s.PredictedMissM < -BoostbackOvershootM;
        }

        public static bool NearPartner(LandingInputs s)
        {
            return s.RangeToPartnerM > 0.0 && s.RangeToPartnerM < SafeSeparationM;
        }

        public static double Ramp(double elapsedS)
        {
            double t = elapsedS * ThrottleRampPerS;
            if (t < 0.0) return 0.0;
            return (t > 1.0) ? 1.0 : t;
        }

        public static double BoostbackThrottle(LandingInputs s)
        {
            if (s.InitialMissM <= 0.0) return 1.0;
            double left = s.PredictedMissM;
            if (left < 0.0) left = -left;
            double frac = left / s.InitialMissM;
            if (frac > 1.0) frac = 1.0;
            return (frac < BoostbackMinThrottle) ? BoostbackMinThrottle : frac;
        }

        public const double EntryGateFraction = 32500.0 / 70000.0;

        public static double EntryGateAsl(LandingInputs s)
        {
            return (s.AtmosphereDepthM > 0.0) ? s.AtmosphereDepthM * EntryGateFraction : EntryBurnGateAsl;
        }

        private static bool InEntryBand(LandingInputs s)
        {
            return s.AltitudeAsl < EntryGateAsl(s) && s.AltitudeRadar > 1000.0;
        }

        public static bool EntryAimReached(LandingInputs s)
        {
            return s.Droneship && s.PredictedMissValid && s.PredictedMissM >= -EntryAimBufferM;
        }

        public static double TrueRadar(LandingInputs s)
        {
            double h = s.AltitudeRadar - BoosterHeightM;
            return (h > 0.0) ? h : 0.0;
        }

        public static double BurnThrottle(LandingInputs s, double thrustAccel)
        {
            double decel = thrustAccel - s.Gravity;
            double h = TrueRadar(s);
            if (decel <= 0.0 || h <= 0.0) return 1.0;
            double stop = s.VerticalSpeed * s.VerticalSpeed / (2.0 * decel);
            return stop / h;
        }

        public static double OneEngineStopDist(LandingInputs s, double threeEngineAccel)
        {
            double decel = threeEngineAccel / OneEngineRatio - s.Gravity;
            if (decel <= 0.0) return double.MaxValue;
            return s.VerticalSpeed * s.VerticalSpeed / (2.0 * decel);
        }

        public static bool HandoverReady(LandingInputs s, double threeEngineAccel)
        {
            if (s.VerticalSpeed <= HandoverVs) return false;
            return OneEngineStopDist(s, threeEngineAccel) * HandoverPad < TrueRadar(s);
        }

        // ------------------------------------------------------------------ landing-zone guidance

        public const double GuidanceDeadbandM = 5.0;

        public const double RollRefMinDeg = 15.0, RollRefMaxDeg = 165.0;

        /// ---- ⛔ RAISED 15 -> 25 (user + flight_0826_083854..092722, 2026-08-26). ----
        [Tunable] public static double AeroAoaDeg = 25.0;

        [Tunable] public static double DescentLeadTauS = 6.0;

        [Tunable] public static double DescentLeadFilterS = 1.5;

        public const double PoweredAoaStartDeg = -3.0;
        public const double PoweredAoaMinDeg = -4.0, PoweredAoaMaxDeg = -1.0;

        public static double LeanFraction(double errorMagnitudeM, double aoaDeg)
        {
            double scale = errorMagnitudeM / GuidanceDeadbandM;
            if (scale > 1.0) scale = 1.0;
            if (scale < 0.0) scale = 0.0;
            return System.Math.Tan(aoaDeg * System.Math.PI / 180.0) * scale;
        }

        /// ---- ⛔ WHY `Math.Min(naiveLean, ceiling)` WAS NOT A CLAMP ----
        public static double ClampLean(double naiveLean, double ceiling)
        {
            if (naiveLean < 0.0) return ceiling;
            return (naiveLean < ceiling) ? naiveLean : ceiling;
        }

        public static double GuidanceAoaDeg(double altitudeRadarM, bool enginesLit)
        {
            return GuidanceAoaDeg(altitudeRadarM, enginesLit, false);
        }

        public static double GuidanceAoaDeg(double altitudeRadarM, bool enginesLit, bool handedOver)
        {
            if (enginesLit && handedOver) return PostHandoverAoaDeg;
            // ---- UNPOWERED: 15 deg HIGH UP, alt/100 BELOW 4 km ----
            if (!enginesLit)
            {
                if (altitudeRadarM >= GuidanceHandAltM) return AeroAoaDeg;
                // ---- ⛔ A PLAIN TAPER. THE max() HERE WAS MINE AND IT WAS WRONG. ----
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
