// DragonScreen - DeorbitOrbit
// ---- ⛔ 85.1 × 79.2 km IS NOT A ROUND NUMBER, IT IS A CALIBRATION ----
// ---- ⚠ AND IT IS SKIPPABLE, DELIBERATELY ----
namespace DragonScreen
{
    public struct PhaseDownBurn
    {
        public bool Needed;
        public double DvMps;
        public bool AtApoapsis;
        public string Label;
    }

    public static class DeorbitOrbit
    {
        // ---- F9I's CONSTANTS. station_ops.ks:78-80. ----

        public const double TargetApoapsisM = 85100.0;
        public const double TargetPeriapsisM = 79200.0;
        public const double ToleranceM = 1500.0;

        public static bool AlreadyOnOrbit(double apoapsisM, double periapsisM)
        {
            double da = apoapsisM - TargetApoapsisM;
            double dp = periapsisM - TargetPeriapsisM;
            if (da < 0.0) da = -da;
            if (dp < 0.0) dp = -dp;
            return da < ToleranceM && dp < ToleranceM;
        }

        public static PhaseDownBurn LowerPeriapsis(double mu, double bodyRadiusM,
                                                   double apoapsisM, double smaNowM)
        {
            PhaseDownBurn b = new PhaseDownBurn();
            b.AtApoapsis = true;
            b.Label = "lower periapsis to " + (TargetPeriapsisM / 1000.0).ToString("F1") + " km";

            double r1 = bodyRadiusM + apoapsisM;
            double rt = bodyRadiusM + TargetPeriapsisM;
            double smaTarget = (r1 + rt) / 2.0;
            b.DvMps = Orbital.VisViva(mu, r1, smaTarget) - Orbital.VisViva(mu, r1, smaNowM);
            b.Needed = System.Math.Abs(b.DvMps) > 0.05;
            return b;
        }

        public static PhaseDownBurn LowerApoapsis(double mu, double bodyRadiusM,
                                                  double periapsisM, double smaNowM)
        {
            PhaseDownBurn b = new PhaseDownBurn();
            b.AtApoapsis = false;
            b.Label = "lower apoapsis to " + (TargetApoapsisM / 1000.0).ToString("F1") + " km";

            double r2 = bodyRadiusM + periapsisM;
            double ra = bodyRadiusM + TargetApoapsisM;
            double smaTarget = (r2 + ra) / 2.0;
            b.DvMps = Orbital.VisViva(mu, r2, smaTarget) - Orbital.VisViva(mu, r2, smaNowM);
            b.Needed = System.Math.Abs(b.DvMps) > 0.05;
            return b;
        }

        public static double TotalDvMps(double mu, double bodyRadiusM,
                                        double apoapsisM, double periapsisM, double smaNowM)
        {
            PhaseDownBurn one = LowerPeriapsis(mu, bodyRadiusM, apoapsisM, smaNowM);
            double r1 = bodyRadiusM + apoapsisM;
            double rt = bodyRadiusM + TargetPeriapsisM;
            double smaAfterOne = (r1 + rt) / 2.0;
            PhaseDownBurn two = LowerApoapsis(mu, bodyRadiusM, TargetPeriapsisM, smaAfterOne);
            return System.Math.Abs(one.DvMps) + System.Math.Abs(two.DvMps);
        }
    }
}
