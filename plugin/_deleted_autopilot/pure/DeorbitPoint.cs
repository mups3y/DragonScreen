// DragonScreen - DeorbitPoint
// ---- ⛔ WHY THIS EXISTS: THE FIXED LEAD WAS F9I'S, NOT OURS ----
// ---- WHAT IT DOES ----
using System;

namespace DragonScreen
{
    public struct IgnitionMiss
    {
        public bool Ok;
        public double MissM;
        public double PeriapsisM;
        public double DvMps;
    }

    public delegate IgnitionMiss IgnitionMissAtUt(double ut);

    public static class DeorbitPoint
    {
        public static double PeriapsisRadius(double px, double py, double pz,
                                             double vx, double vy, double vz, double mu)
        {
            double r = Math.Sqrt(px * px + py * py + pz * pz);
            if (r < 1.0 || mu <= 0.0) return r;
            double v2 = vx * vx + vy * vy + vz * vz;
            double energy = v2 / 2.0 - mu / r;

            double hx = py * vz - pz * vy;
            double hy = pz * vx - px * vz;
            double hz = px * vy - py * vx;
            double h2 = hx * hx + hy * hy + hz * hz;

            if (Math.Abs(energy) < 1e-12)
                return h2 / (2.0 * mu);

            double a = -mu / (2.0 * energy);
            double eArg = 1.0 + 2.0 * energy * h2 / (mu * mu);
            double e = Math.Sqrt(eArg > 0.0 ? eArg : 0.0);
            return a * (1.0 - e);
        }

        public static double DvForPeriapsis(double px, double py, double pz,
                                            double vx, double vy, double vz,
                                            double mu, double targetRpM)
        {
            double vmag = Math.Sqrt(vx * vx + vy * vy + vz * vz);
            if (vmag < 1e-6 || mu <= 0.0) return 0.0;

            double rp0 = PeriapsisRadius(px, py, pz, vx, vy, vz, mu);
            if (rp0 <= targetRpM) return 0.0;

            double lo = 0.0, hi = vmag * 0.999;
            for (int i = 0; i < 60; i++)
            {
                double mid = 0.5 * (lo + hi);
                double s = 1.0 - mid / vmag;
                double rp = PeriapsisRadius(px, py, pz, vx * s, vy * s, vz * s, mu);
                if (rp > targetRpM) lo = mid;
                else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        // ---- the ignition-time search. Same coarse-then-refine shape as Overflight.Search. ----

        public const double CoarseStepS = 20.0;
        public const double RefineStartStepS = 5.0;
        public const double RefineFloorS = 0.25;
        public const int RefineHalfWidthSteps = 10;
        public const double RefineDivisor = 5.0;

        public static IgnitionMiss Search(double loUt, double hiUt, IgnitionMissAtUt f,
                                          out double bestUt)
        {
            IgnitionMiss best = new IgnitionMiss();
            best.MissM = double.MaxValue;
            bestUt = loUt;

            if (f == null || hiUt <= loUt) { best.Ok = false; return best; }

            for (double t = loUt; t <= hiUt; t += CoarseStepS)
            {
                IgnitionMiss m = f(t);
                if (m.Ok && m.MissM < best.MissM) { best = m; bestUt = t; }
            }
            if (!best.Ok) return best;

            double step = RefineStartStepS;
            while (step >= RefineFloorS)
            {
                double lo = bestUt - step * RefineHalfWidthSteps;
                double hi = bestUt + step * RefineHalfWidthSteps;
                if (lo < loUt) lo = loUt;
                if (hi > hiUt) hi = hiUt;
                for (double u = lo; u <= hi; u += step)
                {
                    IgnitionMiss m = f(u);
                    if (m.Ok && m.MissM < best.MissM) { best = m; bestUt = u; }
                }
                step /= RefineDivisor;
            }
            return best;
        }
    }
}
