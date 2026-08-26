// DragonScreen - PlaneWindow
// ---- WHY THIS EXISTS: WE WERE LAUNCHING ON PHASE, NOT PLANE ----
// ---- THE MATH, IN THE INERTIAL FRAME ----
namespace DragonScreen
{
    public static class PlaneWindow
    {
        public static double SecondsToPlane(
            double rx, double ry, double rz,
            double nx, double ny, double nz,
            double omega, bool north)
        {
            if (omega <= 0.0) return -1.0;

            double A = nx * rx + ny * ry;
            double B = ny * rx - nx * ry;
            double C = nz * rz;
            double R = System.Math.Sqrt(A * A + B * B);
            if (R < 1e-9) return -1.0;

            double ratio = -C / R;
            if (ratio > 1.0) ratio = 1.0;
            if (ratio < -1.0) ratio = -1.0;
            double phi = System.Math.Atan2(A, B);
            double asinv = System.Math.Asin(ratio);
            double twoPi = 2.0 * System.Math.PI;

            double th1 = Norm(asinv - phi, twoPi);
            double th2 = Norm(System.Math.PI - asinv - phi, twoPi);

            double d1 = -A * System.Math.Sin(th1) + B * System.Math.Cos(th1);
            bool th1North = d1 > 0.0;

            double want;
            if (north)  want = th1North ? th1 : th2;
            else        want = th1North ? th2 : th1;

            double t = want / omega;
            if (t < 1e-6) t += twoPi / omega;
            return t;
        }

        private static double Norm(double a, double period)
        {
            a %= period;
            if (a < 0.0) a += period;
            return a;
        }

        public static void NormalFromElements(double incRad, double lanRad,
                                              out double nx, out double ny, out double nz)
        {
            double si = System.Math.Sin(incRad), ci = System.Math.Cos(incRad);
            nx = System.Math.Sin(lanRad) * si;
            ny = -System.Math.Cos(lanRad) * si;
            nz = ci;
        }

        // ==================================================================================
        // ==================================================================================
        private const double D2R = System.Math.PI / 180.0;
        private const double TAU = 2.0 * System.Math.PI;
        private const double PlaneEps = 1e-6;

        public static double TimeToPlane(double rotationPeriodS, double latitudeDeg,
                                         double celestialLongitudeDeg, double lanDeg, double incDeg)
        {
            double latitude = latitudeDeg * D2R;
            double celestialLongitude = celestialLongitudeDeg * D2R;
            double lan = lanDeg * D2R;
            double inc = incDeg * D2R;

            if (System.Math.Abs(System.Math.Abs(latitude) - System.Math.PI / 2.0) < PlaneEps) return 0.0;
            if (System.Math.Abs(inc) < PlaneEps || System.Math.Abs(System.Math.Abs(inc) - System.Math.PI) < PlaneEps)
                return 0.0;

            double angleEastOfAN = SafeAsin(System.Math.Tan(latitude) / System.Math.Tan(System.Math.Abs(inc)));

            if (inc < 0.0) angleEastOfAN = System.Math.PI - angleEastOfAN;

            double lanNow = celestialLongitude - angleEastOfAN;
            double lanDiff = lan - lanNow;

            if (rotationPeriodS < 0.0) lanDiff = -lanDiff;

            return Clamp2Pi(lanDiff) / TAU * System.Math.Abs(rotationPeriodS);
        }

        private static double SafeAsin(double x)
        {
            if (x < -1.0) x = -1.0; else if (x > 1.0) x = 1.0;
            return System.Math.Asin(x);
        }

        private static double Clamp2Pi(double x)
        {
            x %= TAU;
            if (x < 0.0) x += TAU;
            return x >= TAU ? 0.0 : x;
        }

        // ==================================================================================
        // ==================================================================================

        public static void PickPhasedCrossing(
            double firstWaitS, double siderealS, double phase0Deg, double stepDeg,
            double acceptMinDeg, double acceptMaxDeg, double desiredLeadDeg,
            double minWaitS, double maxWaitS,
            out double waitS, out int index, out double predictedLeadDeg)
        {
            waitS = firstWaitS; index = 0; predictedLeadDeg = Norm360(phase0Deg);
            if (siderealS <= 0.0) return;
            if (maxWaitS < minWaitS) maxWaitS = minWaitS;

            bool haveBest = false;
            double bestErr = 0.0;

            int kMax = (int)(maxWaitS / siderealS) + 2;
            if (kMax > 100000) kMax = 100000;
            if (kMax < 0) kMax = 0;

            for (int k = 0; k <= kMax; k++)
            {
                double wait = firstWaitS + k * siderealS;
                if (wait > maxWaitS + 0.5) break;
                if (wait < minWaitS) continue;
                double phase = Norm360(phase0Deg + k * stepDeg);

                if (phase >= acceptMinDeg && phase <= acceptMaxDeg)
                { waitS = wait; index = k; predictedLeadDeg = phase; return; }

                double err = System.Math.Abs(Wrap180(phase - desiredLeadDeg));
                if (!haveBest || err < bestErr - 1e-9)
                { haveBest = true; bestErr = err; waitS = wait; index = k; predictedLeadDeg = phase; }
            }
        }

        public static double Norm360(double d)
        {
            d %= 360.0;
            if (d < 0.0) d += 360.0;
            return d;
        }

        public static double Wrap180(double d)
        {
            d = Norm360(d);
            return (d > 180.0) ? d - 360.0 : d;
        }
    }
}
