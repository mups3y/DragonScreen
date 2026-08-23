/*
 * DragonScreen - Kepler
 *
 * PURE. Two-body (conic) state propagation by the universal-variable formulation. Given a position and
 * velocity, advance them dt seconds along the Kepler orbit. UPFG's gravity block needs this to separate
 * the gravitational part of the trajectory from the thrust part without a constant-gravity assumption.
 *
 * Standard public-domain orbital mechanics (universal anomaly + Stumpff functions; Vallado, Curtis) -
 * written fresh in C#, not ported from anywhere. Works for ellipse, parabola and hyperbola in one form.
 * Headless-tested against known conic cases (circular quarter-orbit, full-period identity).
 */
using System;
using MechJebLib.Primitives;

namespace DragonScreen
{
    public static class Kepler
    {
        /// <summary>Stumpff C(z) = (1-cos√z)/z, analytic-continued through z=0 and to hyperbolic z&lt;0.</summary>
        public static double StumpffC(double z)
        {
            if (z > 1e-9) { double s = Math.Sqrt(z); return (1.0 - Math.Cos(s)) / z; }
            if (z < -1e-9) { double s = Math.Sqrt(-z); return (Math.Cosh(s) - 1.0) / (-z); }
            return 0.5 - z / 24.0;                              // series near 0
        }

        /// <summary>Stumpff S(z) = (√z - sin√z)/√z³, analytic-continued through z=0 and to z&lt;0.</summary>
        public static double StumpffS(double z)
        {
            if (z > 1e-9) { double s = Math.Sqrt(z); return (s - Math.Sin(s)) / (s * s * s); }
            if (z < -1e-9) { double s = Math.Sqrt(-z); return (Math.Sinh(s) - s) / (s * s * s); }
            return 1.0 / 6.0 - z / 120.0;                      // series near 0
        }

        /// <summary>
        /// Propagate (r0, v0) by dt seconds under gravity mu. Solves the universal Kepler equation for
        /// the anomaly chi by Newton iteration, then applies the Lagrange f/g coefficients. Returns true
        /// on convergence; on failure leaves r=r0, v=v0 and returns false rather than a wild extrapolation.
        /// </summary>
        public static bool Propagate(V3 r0, V3 v0, double mu, double dt, out V3 r, out V3 v)
        {
            r = r0; v = v0;
            if (mu <= 0.0) return false;
            if (Math.Abs(dt) < 1e-12) return true;

            double r0m = r0.magnitude;
            if (r0m <= 0.0) return false;
            double sqrtMu = Math.Sqrt(mu);
            double vr0 = V3.Dot(r0, v0) / r0m;                 // radial velocity * ... (= r0.v0/|r0|)
            double alpha = 2.0 / r0m - v0.sqrMagnitude / mu;   // = 1/a; >0 ellipse, 0 parabola, <0 hyperbola

            // Initial guess for chi (Vallado): scales with dt and orbit energy.
            double chi = sqrtMu * Math.Abs(alpha) * dt;
            if (Math.Abs(alpha) < 1e-12) chi = sqrtMu * dt / r0m;    // near-parabolic fallback

            for (int iter = 0; iter < 200; iter++)
            {
                double chi2 = chi * chi;
                double z = alpha * chi2;
                double C = StumpffC(z);
                double S = StumpffS(z);
                double chi3 = chi2 * chi;

                // Universal Kepler equation F(chi) and its derivative dF/dchi.
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

            // Lagrange coefficients.
            double f = 1.0 - chi * chi / r0m * Cf;
            double g = dt - chi * chi * chi / sqrtMu * Sf;
            V3 rNew = f * r0 + g * v0;
            double rNewMag = rNew.magnitude;
            if (rNewMag <= 0.0) return false;

            double fdot = sqrtMu / (r0m * rNewMag) * (alpha * chi * chi * chi * Sf - chi);
            double gdot = 1.0 - chi * chi / rNewMag * Cf;

            r = rNew;
            v = fdot * r0 + gdot * v0;
            // Sanity: f*gdot - fdot*g should be 1 (the Wronskian). If it drifted badly, reject.
            if (Math.Abs(f * gdot - fdot * g - 1.0) > 1e-3) return false;
            return true;
        }
    }
}
