// DragonScreen - Ascent
// ---- THIS IS A GRAVITY TURN, NOT THE OPTIMAL-CONTROL ASCENT IN THE PLAN ----
// ---- WHY A GRAVITY TURN IS THE RIGHT INTERIM, NOT A PITCH TABLE ----
// ---- THE NUMBERS, AND WHERE THEY COME FROM ----
namespace DragonScreen
{
    public enum AscentPhase : byte
    {
        Idle = 0,
        VerticalRise,
        GravityTurn,
        Meco,
        StageSep,
        Shutdown,
        BurnToApoapsis,
        Coast,
        Circularise,
        Done
    }

    public struct AscentTarget
    {
        public double AltitudeM;
        public double HeadingDeg;

        // ---- F9I'S ASCENT CONSTANTS. FLOWN, NOT CHOSEN HERE. ----
        public double MecoAngleDeg;
        public double PitchGain;
        public double StageAltM;

        public double MecoSurfaceSpeedCapMps;

        public double PitchRefAltM;

        public double MaxQKpa;

        public double GLimitMps2;

        public static AscentTarget Station() { return Station(LandingProfile.Rtls); }

        public const double StationAltitudeM = 120000.0;

        public const double ShutdownSettleS = 2.0;

        public static AscentTarget Station(LandingProfile p)
        {
            AscentTarget t = new AscentTarget();
            // ---- ⛔ THE STATION'S ALTITUDE, AND IT IS NOT A ROUND NUMBER BY ACCIDENT. ----
            t.AltitudeM = StationAltitudeM;
            t.HeadingDeg = 90.0;
            double meco, stageAlt, gain, payload;
            LandingSites.AscentFor(p, out meco, out stageAlt, out gain, out payload);
            t.MecoAngleDeg = meco;
            t.PitchGain = gain;
            t.StageAltM = stageAlt;
            t.PitchRefAltM = 0.0;
            t.MaxQKpa = Ascent.MaxQKpa;
            return t;
        }

        public static AscentTarget ForBody(LandingProfile p, double parkingAltitudeM)
        {
            AscentTarget t = Station(p);
            t.AltitudeM = parkingAltitudeM;
            t.StageAltM = 150000.0;
            t.PitchRefAltM = 48000.0;
            t.MecoAngleDeg = 25.0;
            t.MaxQKpa = 30.0;
            t.GLimitMps2 = 3.5 * 9.80665;
            t.MecoSurfaceSpeedCapMps = 2300.0;
            return t;
        }

    }

    public struct AscentInputs
    {
        public bool Valid;
        public double RadarAltitude;
        public double Altitude;
        public double ApoapsisM, PeriapsisM;
        public double AtmosphereDepthM;
        public double VerticalSpeed;
        public double SurfaceSpeed;
        public double DynamicPressureKpa;
        public double TimeToApoapsisS;

        public double RangeToBoosterM;
        public double AvailableThrust;
        public double MaxThrustKn;
        public double MassT;
        public bool Landed;
        public bool SecondStage;
        public double PhaseElapsedS;

        // ---- CIRCULARISATION, F9I-STYLE ----
        public double CircDvMps;
        public bool CircDvFlipped;
    }

    public struct AscentCommand
    {
        public AscentPhase Phase;
        public double PitchDeg;
        public double HeadingDeg;
        public double Throttle;
        public double UllageFore;
        public bool Stage;
        public bool SeparateS2;
        public bool Rcs;
        public string Note;
    }

    public static class Ascent
    {
        public const double VerticalRiseM = 250.0;

        public const double TurnEndFraction = 0.62;

        public const double MaxQKpa = 20.0;

        public const double PeriapsisToleranceM = 2000.0;

        public const double CircBurnLeadS = 12.0;

        // ---- ⛔ MECO IS A DISCRETE STEP AT ITS OWN, LOWER, APOAPSIS TARGET ----

        public const double MecoHoldS = 2.5;

        public const double UllageSeconds = 6.0;

        public const double PostSepHoldS = 3.0;

        public const double MaxSepWaitS = 20.0;

        public const double SepPeTargetM = 40000.0;
        public const double UllageThrottle = 0.075;
        public const double UllageFore = 0.75;

        /// ---- ⛔ WHY ULLAGE IS GATED ON THRUST, NOT A FIXED 6 s WINDOW. ----
        public const double UllageThrustConfirmKn = 100.0;

        // ---- CIRCULARISATION: THE BURN THAT PUT US ON AN ESCAPE TRAJECTORY ----

        public const double CircDvToleranceMps = 0.5;

        public const double CircDvFullMps = 25.0;

        public const double CircThrottleMin = 0.05;

        public const double ApoapsisRunawayFactor = 1.5;

        public static AscentCommand Guide(AscentInputs s, AscentTarget t, AscentPhase phase)
        {
            AscentCommand c = new AscentCommand();
            c.HeadingDeg = t.HeadingDeg;
            c.Phase = phase;

            if (!s.Valid) { c.Phase = AscentPhase.Idle; c.Note = "no vessel"; return c; }

            bool stageNow = false;
            bool sepS2Now = false;

            // ---- ⛔ EXACTLY ONE TRANSITION PER CALL. THIS `else` CHAIN IS LOAD-BEARING. ----
            if (phase == AscentPhase.Idle || phase == AscentPhase.VerticalRise)
            {
                phase = (s.RadarAltitude < VerticalRiseM && !Above(s, t))
                      ? AscentPhase.VerticalRise : AscentPhase.GravityTurn;
            }
            else if (phase == AscentPhase.GravityTurn
                     && (s.ApoapsisM >= StageTarget(t)
                         || (t.MecoSurfaceSpeedCapMps > 0.0 && s.SurfaceSpeed >= t.MecoSurfaceSpeedCapMps)
                         || FirstStageSpent(s)))
                phase = AscentPhase.Meco;

            else if (phase == AscentPhase.Meco && s.PhaseElapsedS >= MecoHoldS)
            {
                // ---- ⛔ AND IT GOES TO StageSep, NOT STRAIGHT TO THE MVac. ----
                phase = AscentPhase.StageSep;
                stageNow = true;
            }

            // ---- ⛔ TIME, NOT DISTANCE - AND THE DISTANCE VERSION WAS A DEADLOCK. ----
            else if (phase == AscentPhase.StageSep && s.PhaseElapsedS >= PostSepHoldS)
                phase = AscentPhase.BurnToApoapsis;

            // ---- SECOND STAGE RAISES APOAPSIS, THEN THE S2 IS DROPPED ----
            else if (phase == AscentPhase.BurnToApoapsis
                     && (Above(s, t) || s.PeriapsisM >= SepPeTargetM))
            {
                phase = AscentPhase.Coast;
            }

            else if (phase == AscentPhase.Coast
                     && (s.TimeToApoapsisS <= CircBurnLeadS || s.PeriapsisM > t.AltitudeM * 0.5))
            {
                phase = AscentPhase.Circularise;
            }
            else if (phase == AscentPhase.Circularise && Circularised(s, t))
            {
                // ---- ⛔ THE SECOND STAGE FINISHES THE JOB, THEN LEAVES. ----
                phase = AscentPhase.Shutdown;
            }

            // ---- THE SPOOL-DOWN HOLD, THEN SEPARATE ----
            else if (phase == AscentPhase.Shutdown && s.PhaseElapsedS >= AscentTarget.ShutdownSettleS)
            {
                sepS2Now = true;
                phase = AscentPhase.Done;
            }

            // ---- ⛔ AND A DIVERGING BURN IS NOT A FINISHED ONE ----
            if (phase == AscentPhase.Circularise && s.CircDvFlipped
                && s.CircDvMps > CircDvDivergedMps)
            {
                c.Phase = AscentPhase.Done;
                c.Throttle = 0.0;
                c.Note = "ABORT - CIRCULARISATION DIVERGING";
                return c;
            }

            if (s.ApoapsisM > t.AltitudeM * ApoapsisRunawayFactor)
            {
                c.Phase = AscentPhase.Done;
                c.Throttle = 0.0;
                c.PitchDeg = 0.0;
                c.Note = "ABORT - APOAPSIS RUNAWAY";
                return c;
            }

            c.Phase = phase;

            switch (phase)
            {
                case AscentPhase.VerticalRise:
                    c.PitchDeg = 90.0;
                    c.Throttle = 1.0;
                    c.Note = "VERTICAL RISE";
                    break;

                case AscentPhase.GravityTurn:
                    c.PitchDeg = TurnPitch(s, t);
                    c.Throttle = System.Math.Min(QThrottle(s, t.MaxQKpa), GThrottle(s, t.GLimitMps2));
                    c.Note = "GRAVITY TURN";
                    break;

                case AscentPhase.Meco:
                    c.PitchDeg = (t.MecoAngleDeg > 0.0) ? t.MecoAngleDeg : 45.0;
                    c.Throttle = 0.0;
                    c.Rcs = true;
                    c.Note = "MECO";
                    break;

                case AscentPhase.StageSep:
                    c.PitchDeg = (t.MecoAngleDeg > 0.0) ? t.MecoAngleDeg : 45.0;
                    c.Throttle = 0.0;
                    c.Rcs = true;
                    c.Note = "STAGE SEP";
                    break;

                case AscentPhase.BurnToApoapsis:
                    c.PitchDeg = TurnPitch(s, t);
                    if (s.PhaseElapsedS < UllageSeconds)
                    {
                        c.Throttle = UllageThrottle;
                        c.UllageFore = UllageFore;
                        c.Note = "ULLAGE";
                    }
                    else if (s.AvailableThrust < UllageThrustConfirmKn)
                    {
                        c.Throttle = ApoapsisThrottle(s, t);
                        c.UllageFore = UllageFore;
                        c.Note = "ULLAGE + LIGHT";
                    }
                    else
                    {
                        c.Throttle = ApoapsisThrottle(s, t);
                        c.Note = "BURN TO APOAPSIS";
                    }
                    break;

                case AscentPhase.Coast:
                    c.PitchDeg = 0.0;
                    c.Throttle = 0.0;
                    c.Rcs = true;
                    c.Note = "COAST TO APOAPSIS";
                    break;

                case AscentPhase.Shutdown:
                    c.Throttle = 0.0;
                    c.Rcs = true;
                    c.Note = "SHUTDOWN - engine spooling down before separation";
                    break;

                case AscentPhase.Circularise:
                    c.PitchDeg = 0.0;
                    c.Throttle = CircThrottle(s.CircDvMps);
                    c.Note = "CIRCULARISE";
                    break;

                default:
                    c.PitchDeg = 0.0;
                    c.Throttle = 0.0;
                    c.Note = (phase == AscentPhase.Done) ? "INSERTION COMPLETE" : "IDLE";
                    break;
            }

            c.Stage = stageNow;
            c.SeparateS2 = sepS2Now;
            return c;
        }

        public static double StageTarget(AscentTarget t)
        {
            return (t.StageAltM > 0.0) ? t.StageAltM : 60000.0;
        }

        public const double FirstStageSpentThrustKn = 1.0;
        public const double FirstStageSpentMinAltM = 30000.0;

        public static bool FirstStageSpent(AscentInputs s)
        {
            return !s.SecondStage
                && s.Altitude > FirstStageSpentMinAltM
                && s.AvailableThrust < FirstStageSpentThrustKn;
        }

        public static double ApoapsisThrottle(AscentInputs s, AscentTarget t)
        {
            double span = t.AltitudeM - s.AtmosphereDepthM;
            if (span < 1000.0) span = 1000.0;
            double deficit = (t.AltitudeM - s.ApoapsisM) / span;
            if (deficit < 0.1) deficit = 0.1;

            double fire = (30.0 - s.TimeToApoapsisS) * 0.075;
            if (fire < 0.0) fire = 0.0;

            double th = deficit + fire;
            if (th > 1.0) th = 1.0;
            return th;
        }

        public const double CircDvDivergedMps = 5.0;

        public static bool Circularised(AscentInputs s, AscentTarget t)
        {
            if (s.PeriapsisM <= s.AtmosphereDepthM) return false;
            if (s.CircDvMps <= CircDvToleranceMps) return true;
            return s.PeriapsisM >= t.AltitudeM - PeriapsisToleranceM;
        }

        public static double CircThrottle(double dvMps)
        {
            if (dvMps <= CircDvToleranceMps) return 0.0;
            double t = dvMps / CircDvFullMps;
            if (t > 1.0) t = 1.0;
            if (t < CircThrottleMin) t = CircThrottleMin;
            return t;
        }

        private static bool Above(AscentInputs s, AscentTarget t)
        {
            return s.ApoapsisM >= t.AltitudeM;
        }

        public static double TurnPitch(AscentInputs s) { return TurnPitch(s, AscentTarget.Station()); }

        /// ---- F9I'S ASCENT LAW, PORTED. NOT THE sqrt CURVE I INVENTED FIRST. ----
        public static double TurnPitch(AscentInputs s, AscentTarget t)
        {
            double meco = (t.MecoAngleDeg > 0.0) ? t.MecoAngleDeg : 45.0;

            if (s.SecondStage)
            {
                double tanAlt = (s.AtmosphereDepthM > 0.0) ? s.AtmosphereDepthM : 70000.0;
                double p = 90.0 * (1.0 - s.Altitude / tanAlt);
                if (p < 0.1) p = 0.1;
                double eta = s.TimeToApoapsisS;
                double fire = 5.0 * ((30.0 - eta) * 0.075);
                if (fire < 0.0) fire = 0.0;
                p += fire;
                return (p > meco) ? meco : p;
            }

            double denom;
            if (t.PitchRefAltM > 0.0)
                denom = t.PitchRefAltM;
            else
            {
                double gain = (t.PitchGain > 0.0) ? t.PitchGain : 110.0;
                double stageAlt = (t.StageAltM > 0.0) ? t.StageAltM : 60000.0;
                denom = stageAlt * (gain / 100.0);
                if (denom <= 0.0) denom = 66000.0;
            }

            double pitch = 90.0 * (1.0 - s.Altitude / denom);
            if (pitch < meco) pitch = meco;
            return (pitch < 0.0) ? 0.0 : pitch;
        }

        public static double QThrottle(AscentInputs s) { return QThrottle(s, MaxQKpa); }

        public static double QThrottle(AscentInputs s, double maxQKpa)
        {
            if (maxQKpa <= 0.0) maxQKpa = MaxQKpa;
            if (s.DynamicPressureKpa <= maxQKpa) return 1.0;
            double over = (s.DynamicPressureKpa - maxQKpa) / maxQKpa;
            double th = 1.0 - over * 2.0;
            if (th < 0.35) th = 0.35;
            return th;
        }

        public static double GThrottle(AscentInputs s, double gLimitMps2)
        {
            if (gLimitMps2 <= 0.0 || s.MassT <= 0.0 || s.MaxThrustKn <= 0.0) return 1.0;
            double fullAccel = s.MaxThrustKn / s.MassT;
            if (fullAccel <= gLimitMps2) return 1.0;
            double th = gLimitMps2 / fullAccel;
            if (th < 0.35) th = 0.35;
            return th;
        }

        public static string Name(AscentPhase p)
        {
            switch (p)
            {
                case AscentPhase.VerticalRise: return "VERTICAL RISE";
                case AscentPhase.GravityTurn:  return "GRAVITY TURN";
                case AscentPhase.Meco:         return "MECO";
                case AscentPhase.StageSep:     return "STAGE SEP";
                case AscentPhase.BurnToApoapsis: return "BURN TO APOAPSIS";
                case AscentPhase.Coast:        return "COAST";
                case AscentPhase.Shutdown:     return "SHUTDOWN";
                case AscentPhase.Circularise:  return "CIRCULARISE";
                case AscentPhase.Done:         return "INSERTION COMPLETE";
                default:                       return "STANDBY";
            }
        }
    }
}
