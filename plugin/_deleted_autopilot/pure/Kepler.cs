// DragonScreen - Kepler
using System;
using MechJebLib.Primitives;

namespace DragonScreen
{
    public static class Kepler
    {
        public static double StumpffC(double z)
        {
            if (z > 1e-9) { double s = Math.Sqrt(z); return (1.0 - Math.Cos(s)) / z; }
            if (z < -1e-9) { double s = Math.Sqrt(-z); return (Math.Cosh(s) - 1.0) / (-z); }
            return 0.5 - z / 24.0;
        }

        public static double StumpffS(double z)
        {
            if (z > 1e-9) { double s = Math.Sqrt(z); return (s - Math.Sin(s)) / (s * s * s); }
            if (z < -1e-9) { double s = Math.Sqrt(-z); return (Math.Sinh(s) - s) / (s * s * s); }
            return 1.0 / 6.0 - z / 120.0;
        }

        public static bool Propagate(V3 r0, V3 v0, double mu, double dt, out V3 r, out V3 v)
        {
            r = r0; v = v0;
            if (mu <= 0.0) return false;
            if (Math.Abs(dt) < 1e-12) return true;

            double r0m = r0.magnitude;
            if (r0m <= 0.0) return false;
            double sqrtMu = Math.Sqrt(mu);
            double vr0 = V3.Dot(r0, v0) / r0m;
            double alpha = 2.0 / r0m - v0.sqrMagnitude / mu;

            double chi = sqrtMu * Math.Abs(alpha) * dt;
            if (Math.Abs(alpha) < 1e-12) chi = sqrtMu * dt / r0m;

            for (int iter = 0; iter < 200; iter++)
            {
                double chi2 = chi * chi;
                double z = alpha * chi2;
                double C = StumpffC(z);
                double S = StumpffS(z);
                double chi3 = chi2 * chi;

                double F = r0m * vr0 / sqrtMu * chi2 * C
                         + (1.0 - alpha * r0m) * chi3 * S
                         + r0m * chi
                         - sqrtMu * dt;
                double dF = r0m * vr0 / sqrtMu * chi * (1.0 - alpha * chi2 * S)
                          + (1.0 - alpha * r0m) * chi2 * C
                          + r0m;
                if (Math.Abs(dF) < 1e-30) return false;
                double dchi = F / dF;
                chi -= dchi;
                if (Math.Abs(dchi) < 1e-8) break;
            }

            double zf = alpha * chi * chi;
            double Cf = StumpffC(zf);
            double Sf = StumpffS(zf);

            double f = 1.0 - chi * chi / r0m * Cf;
            double g = dt - chi * chi * chi / sqrtMu * Sf;
            V3 rNew = f * r0 + g * v0;
            double rNewMag = rNew.magnitude;
            if (rNewMag <= 0.0) return false;

            double fdot = sqrtMu / (r0m * rNewMag) * (alpha * chi * chi * chi * Sf - chi);
            double gdot = 1.0 - chi * chi / rNewMag * Cf;

            r = rNew;
            v = fdot * r0 + gdot * v0;
            if (Math.Abs(f * gdot - fdot * g - 1.0) > 1e-3) return false;
            return true;
        }
    }
}
