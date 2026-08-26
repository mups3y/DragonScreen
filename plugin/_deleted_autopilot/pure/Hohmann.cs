// DragonScreen - Hohmann
namespace DragonScreen
{
    public static class Hohmann
    {
        public static double SpeedAt(double r, double a, double mu)
        {
            if (r <= 0.0 || a <= 0.0 || mu <= 0.0) return 0.0;
            double v2 = mu * (2.0 / r - 1.0 / a);
            return v2 > 0.0 ? System.Math.Sqrt(v2) : 0.0;
        }

        public static double ApsisBurnDv(double rBurn, double aOld, double aNew, double mu)
        {
            return SpeedAt(rBurn, aNew, mu) - SpeedAt(rBurn, aOld, mu);
        }

        public static double TransferSma(double rBurn, double rOther)
        {
            return 0.5 * (rBurn + rOther);
        }

        public static double RaiseOppositeApsisDv(double rBurn, double aOld, double rTarget, double mu)
        {
            return ApsisBurnDv(rBurn, aOld, TransferSma(rBurn, rTarget), mu);
        }

        public static double CirculariseDv(double r, double a, double mu)
        {
            return SpeedAt(r, r, mu) - SpeedAt(r, a, mu);
        }
    }
}
