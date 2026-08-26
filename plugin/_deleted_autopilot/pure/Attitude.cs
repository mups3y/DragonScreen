// DragonScreen - Attitude
// ---- ⛔ WHY THIS REPLACED THE kOS PORT, AND WHY IT WILL NOT BE PUT BACK ----
// ---- THE ONE IDEA THAT MATTERS: THE RATE BOUND IS A BRAKING CURVE ----
// ---- STATELESS ON PURPOSE ----
namespace DragonScreen
{
    public static class Attitude
    {
        public const double RateMargin = 0.9;

        public const double DefaultTimeConstantS = 1.0;

        public const double SettleTimeS = 1.0;

        // ---- MAXIMUM COMMANDED RATE PER PHASE, degrees/second. ALL MEASURED. ----
        public const double AscentMaxRateDps = 2.0;
        public const double FlipMaxRateDps = 15.0;
        public const double EntryMaxRateDps = 25.0;
        public const double DescentMaxRateDps = 10.0;
        public const double LandingMaxRateDps = 3.0;

        [Tunable] public static double CoastMaxRateDps = 4.0;

        public const double CapsuleMaxRateDps = 10.0;

        public const double DeadbandRad = 0.0015;

        public const double MaxSlewPerTick = 0.08;

        public static double ArrestableRate(double errorRad, double torque, double moi)
        {
            if (moi <= 0.0 || torque <= 0.0) return 0.0;
            double e = (errorRad < 0.0) ? -errorRad : errorRad;
            double alpha = torque / moi;
            return RateMargin * System.Math.Sqrt(2.0 * alpha * e);
        }

        public static double RateCommand(double errorRad, double torque, double moi,
                                         double maxRateRad)
        {
            if (errorRad > -DeadbandRad && errorRad < DeadbandRad) return 0.0;

            // ---- ⛔ PROPORTIONAL, CLAMPED. THE BRAKING CURVE ALONE WAS A DISASTER. ----
            double w = System.Math.Abs(errorRad) / SettleTimeS;
            if (maxRateRad > 0.0 && w > maxRateRad) w = maxRateRad;

            double stop = ArrestableRate(errorRad, torque, moi);
            if (stop > 0.0 && w > stop) w = stop;

            return (errorRad < 0.0) ? -w : w;
        }

        public static double TorqueCommand(double rateErrorRad, double moi, double timeConstantS)
        {
            if (timeConstantS <= 0.0) return 0.0;
            return moi * rateErrorRad / timeConstantS;
        }

        public static double Actuate(double torqueCmd, double torqueAvail, double previous)
        {
            if (torqueAvail <= 0.0) return 0.0;
            double a = torqueCmd / torqueAvail;
            if (a > 1.0) a = 1.0;
            if (a < -1.0) a = -1.0;

            double d = a - previous;
            if (d > MaxSlewPerTick) a = previous + MaxSlewPerTick;
            else if (d < -MaxSlewPerTick) a = previous - MaxSlewPerTick;
            return a;
        }

        public static double Axis(double errorRad, double rateRad, double torque, double moi,
                                  double timeConstantS, double previous)
        {
            double want = RateCommand(errorRad, torque, moi, 0.0);
            double tq = TorqueCommand(want - rateRad, moi, timeConstantS);
            return Actuate(tq, torque, previous);
        }

        public const double RollControlRangeDeg = 45.0;
    }
}
