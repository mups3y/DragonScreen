// DragonScreen — Conic  (autopilot rebuild L3 support: two-body conic propagation)
// ============================================================================================
// Universal-variable Kepler propagation with Stumpff functions (the standard Bate-Mueller-White /
// Vallado algorithm), written fresh from the equations. Given (r, v) at a time, returns (r, v) after
// Δt under pure two-body gravity — no integration. UPFG (block 7, "conic state extrapolation") uses it
// for the gravity term over the time-to-go, so the ascent guidance does NOT assume constant gravity.
// Verified headless against analytic conic cases (quarter-orbit rotation, period identity, energy +
// angular-momentum conservation, reversibility).
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class Conic
    {
        // Stumpff functions C(z), S(z) with the small-|z| series near parabolic (z ~ 0).
        public static double StumpffC(double z)
        {
            if (z > 1e-6) { double s = Math.Sqrt(z); return (1.0 - Math.Cos(s)) / z; }
            if (z < -1e-6) { double s = Math.Sqrt(-z); return (Math.Cosh(s) - 1.0) / (-z); }
            return 0.5 - z / 24.0 + z * z / 720.0;
        }

        public static double StumpffS(double z)
        {
            if (z > 1e-6) { double s = Math.Sqrt(z); return (s - Math.Sin(s)) / (s * s * s); }
            if (z < -1e-6) { double s = Math.Sqrt(-z); return (Math.Sinh(s) - s) / (s * s * s); }
            return 1.0 / 6.0 - z / 120.0 + z * z / 5040.0;
        }

        // Propagate (r0, v0) by dt under gravity parameter mu. Returns true + (r, v); false if it fails.
        public static bool Propagate(Vec3 r0, Vec3 v0, double mu, double dt, out Vec3 r, out Vec3 v)
        {
            r = r0; v = v0;
            if (mu <= 0.0) return false;
            if (Math.Abs(dt) < 1e-12) return true;

            double sqrtMu = Math.Sqrt(mu);
            double r0m = r0.Magnitude;
            if (r0m < 1e-6) return false;
            double v0m2 = v0.SqrMagnitude;
            double vr0 = Vec3.Dot(r0, v0) / r0m;          // radial speed
            double alpha = 2.0 / r0m - v0m2 / mu;         // 1 / semi-major axis

            // initial guess for the universal anomaly chi
            double chi = sqrtMu * Math.Abs(alpha) * dt;
            if (Math.Abs(alpha) < 1e-12) chi = sqrtMu * dt / r0m;   // near-parabolic

            // Newton-Raphson on the universal Kepler equation
            double z = 0.0, C = 0.0, S = 0.0;
            bool converged = false;
            for (int i = 0; i < 200; i++)
            {
                z = alpha * chi * chi;
                C = StumpffC(z);
                S = StumpffS(z);
                double chi2 = chi * chi, chi3 = chi2 * chi;
                double f = r0m * vr0 / sqrtMu * chi2 * C
                           + (1.0 - alpha * r0m) * chi3 * S
                           + r0m * chi
                           - sqrtMu * dt;
                double fp = r0m * vr0 / sqrtMu * chi * (1.0 - alpha * chi2 * S)
                            + (1.0 - alpha * r0m) * chi2 * C
                            + r0m;
                if (Math.Abs(fp) < 1e-30) break;
                double dchi = f / fp;
                chi -= dchi;
                if (Math.Abs(dchi) < 1e-8) { converged = true; break; }
            }
            if (!converged) return false;

            z = alpha * chi * chi;
            C = StumpffC(z);
            S = StumpffS(z);
            double chi2b = chi * chi, chi3b = chi2b * chi;

            // Lagrange coefficients
            double fLag = 1.0 - chi2b / r0m * C;
            double gLag = dt - chi3b / sqrtMu * S;
            r = r0 * fLag + v0 * gLag;
            double rm = r.Magnitude;
            if (rm < 1e-6) return false;
            double fdot = sqrtMu / (rm * r0m) * (alpha * chi3b * S - chi);
            double gdot = 1.0 - chi2b / rm * C;
            v = r0 * fdot + v0 * gdot;

            return r.IsFinite && v.IsFinite;
        }
    }
}
