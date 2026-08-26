// DragonScreen - Hoverslam
// ---- WHY THIS EXISTS, AND WHAT IT TAKES FROM MECHJEB ----
namespace DragonScreen
{
    public struct HoverslamInputs
    {
        public double AltitudeM;
        public double VerticalSpeed;
        public double MassT;
        public double GravityMps2;
        public double ThrustKn;
        public double MdotTps;
        public double DragRefAccel;
        public double DragRefSpeed;
        public double DeadTimeS;
        public double SpoolS;
    }

    public static class HoverslamSolver
    {
        private const double DtS = 0.05;

        public static double IgnitionAltitude(HoverslamInputs s)
        {
            if (s.ThrustKn <= 0.0 || s.MassT <= 0.0) return s.AltitudeM;

            double lo = 0.0;
            double hi = System.Math.Max(s.AltitudeM * 2.0, 5000.0);
            if (StopAltitude(hi, s) < 0.0) return s.AltitudeM;

            for (int i = 0; i < 60; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (StopAltitude(mid, s) > 0.0) hi = mid; else lo = mid;
                if (hi - lo < 0.5) break;
            }
            return 0.5 * (lo + hi);
        }

        public static double StopAltitude(double hIgn, HoverslamInputs s)
        {
            double h = hIgn;
            double v = s.VerticalSpeed;
            double m = s.MassT;
            double t = 0.0;
            double g = s.GravityMps2;

            for (int step = 0; step < 20000; step++)
            {
                if (v >= 0.0) return h;
                if (h <= -2000.0) return h;

                double thrustAccel = 0.0, spool = 0.0;
                if (t >= s.DeadTimeS)
                {
                    spool = (s.SpoolS > 0.0) ? System.Math.Min(1.0, (t - s.DeadTimeS) / s.SpoolS) : 1.0;
                    thrustAccel = (m > 0.0) ? s.ThrustKn * spool / m : 0.0;
                }
                double dragAccel = DragAccel(-v, s);
                double a = -g + dragAccel + thrustAccel;

                v += a * DtS;
                h += v * DtS;
                m -= s.MdotTps * spool * DtS;
                t += DtS;
            }
            return h;
        }

        private static double DragAccel(double speed, HoverslamInputs s)
        {
            if (s.DragRefSpeed <= 0.0 || s.DragRefAccel <= 0.0) return 0.0;
            double r = speed / s.DragRefSpeed;
            return s.DragRefAccel * r * r;
        }
    }
}
