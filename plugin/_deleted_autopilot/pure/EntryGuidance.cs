// DragonScreen - EntryGuidance
// ---- WHAT IT IS DOING ----
// ---- ⛔ FOUR THINGS HERE ARE COUNTER-INTUITIVE AND EVERY ONE COST A FLIGHT ----
// ---- ⚠ THIS IS NOT `pure/Entry.cs`, AND THEY ARE NOT DUPLICATES ----
// ---- AND LATERAL OUTLIVES VERTICAL ----
namespace DragonScreen
{
    public struct EntryGuideInputs
    {
        public double AltitudeM;
        public double DownrangeErrM;
        public double CrossTrackM;
        public double LzRangeM;
        public double LzBearingDeg;
        public double TrackBearingDeg;
        public double MissM;
        public double DtS;
    }

    public struct EntryMemory
    {
        public double FilteredRate, LastError;
        public bool HaveRate;
        public double LiftMin;
        public double WorstErrorM;
        public bool Dropped;
    }

    public struct EntryGuideCommand
    {
        public double VerticalCmd;
        public double LateralCmd;
        public double WantLongM;
        public double LeadErrorM;
        public bool BelowProfile;
        public string Note;
    }

    public static class EntryGuidance
    {
        // ---- F9I's CONSTANTS. dragon_deorbit.ks:277-475. ----
        [Tunable] public static double DownScaleM = 20000.0;
        [Tunable] public static double CrossScaleM = 5000.0;
        [Tunable] public static double LeadS = 20.0;
        [Tunable] public static double CapsuleBcKgM2 = 440.0;
        public const double LeadFrac = 0.5;
        public const double TermAltM = 12000.0;
        public const double LatFloorM = 3000.0;
        public const double BelowWarnM = 1500.0;
        public const double LzToleranceM = 50.0;
        public const double RateFilterOld = 0.7;
        public const double RateIntervalS = 0.5;

        // ---- HOW THE COMMAND BECOMES AN ATTITUDE. dragon_deorbit.ks:35, 322-323, 474, 2311-2320. ----

        public const double TrimAoaDeg = 15.0;

        public const double QSteerKpa = 0.003;

        public const double PitchSign = -1.0;

        public const double YawSign = 1.0;

        public const double WarpCancelAltM = 55000.0;
        public const int WarpIndex = 3;

        public static double LiftFraction(double verticalCmd, double lateralCmd)
        {
            double m = System.Math.Sqrt(verticalCmd * verticalCmd + lateralCmd * lateralCmd);
            return (m > 1.0) ? 1.0 : m;
        }

        public static double AoaCommandDeg(double verticalCmd, double lateralCmd)
        {
            return TrimAoaDeg * LiftFraction(verticalCmd, lateralCmd);
        }

        public static bool CanSteer(double dynamicPressureKpa)
        {
            return dynamicPressureKpa > QSteerKpa;
        }

        public static double AheadM(EntryGuideInputs s)
        {
            double dBearing = (s.LzBearingDeg - s.TrackBearingDeg) * System.Math.PI / 180.0;
            return s.LzRangeM * System.Math.Cos(dBearing);
        }

        public static EntryGuideCommand Update(EntryGuideInputs s, ref EntryMemory mem)
        {
            EntryGuideCommand c = new EntryGuideCommand();

            if (s.MissM >= 0.0 && s.MissM < LzToleranceM) mem.Dropped = true;
            if (s.AltitudeM <= TermAltM) mem.Dropped = true;

            bool rangeLive = !mem.Dropped;
            if (rangeLive)
            {
                double ahead = AheadM(s);
                double margin = EntryMargin.WantLongClampedM(s.AltitudeM, ahead);
                c.WantLongM = margin;

                double errNow = s.DownrangeErrM + margin;

                if (!mem.HaveRate)
                {
                    mem.LastError = errNow;
                    mem.HaveRate = true;
                }
                else if (s.DtS >= RateIntervalS)
                {
                    double raw = (errNow - mem.LastError) / s.DtS;
                    mem.FilteredRate = RateFilterOld * mem.FilteredRate
                                     + (1.0 - RateFilterOld) * raw;
                    mem.LastError = errNow;
                }

                double leadRaw = LeadS * mem.FilteredRate;
                double cap = LeadFrac * System.Math.Abs(errNow);
                if (leadRaw > cap) leadRaw = cap;
                if (leadRaw < -cap) leadRaw = -cap;
                double leadErr = errNow + leadRaw;
                c.LeadErrorM = leadErr;

                double cmd = leadErr / DownScaleM;
                if (cmd > 0.0) cmd = 0.0;
                if (cmd < -1.0) cmd = -1.0;
                c.VerticalCmd = cmd;

                if (cmd < mem.LiftMin) mem.LiftMin = cmd;
                if (leadErr > mem.WorstErrorM) mem.WorstErrorM = leadErr;

                if (leadErr > BelowWarnM)
                {
                    c.BelowProfile = true;
                    c.Note = "BELOW PROFILE by " + (leadErr / 1000.0).ToString("F1")
                           + " km - cannot extend; the de-orbit aim was too short";
                }
            }

            if (s.MissM >= LzToleranceM && s.AltitudeM > LatFloorM)
            {
                double lat = s.CrossTrackM / CrossScaleM;
                if (lat > 1.0) lat = 1.0;
                if (lat < -1.0) lat = -1.0;
                c.LateralCmd = lat;
            }

            if (c.Note == null || c.Note.Length == 0)
                c.Note = rangeLive ? "ENTRY GUIDANCE" : "TERMINAL - range loop latched";
            return c;
        }

        public static bool FlewOpenLoop(EntryMemory mem)
        {
            return mem.LiftMin > -0.001;
        }
    }
}
