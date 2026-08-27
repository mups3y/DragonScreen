// DragonScreen — Lambert  (autopilot rebuild B7: the Lambert two-point boundary-value solver)
// ============================================================================================
// THE general intercept primitive: given where the chaser is (r1), where the target WILL BE in time-of-flight
// tof (r2), and mu, find the transfer-orbit velocities v1, v2 that connect them. The burn is v1 − v_chaser.
// This is what the current CW (linearised, near-field) and Hohmann (coplanar, circular) helpers cannot do —
// a general, arbitrary-geometry intercept for the far-field phasing/approach.
//
// Method: the universal-variable formulation (Bate-Mueller-White / Vallado "LambertUniversal"), reusing our
// OWN verified Stumpff functions (Conic.StumpffC/StumpffS) so it is consistent with Conic.Propagate — which
// gives the strongest possible check: v1 propagated by Conic.Propagate over tof must reach r2 (self-inversion).
// Bisection on the universal variable ψ (t is monotonic in ψ) → robust, bounded iterations. Single-revolution.
// Pure + headless-tested (self-inversion + known-orbit recovery). MechJeb ships Izzo/Gooding solvers; this is
// the same problem solved on our existing conic core, cross-checked by round-trip against it.
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct LambertSolution
    {
        public Vec3 V1, V2;    // velocities at r1 and r2 on the transfer orbit
        public bool Ok;
        public int Iterations;
    }

    public static class Lambert
    {
        [Tunable] public static int MaxIter = 80;
        [Tunable] public static double TofTolFrac = 1e-6;   // converge when |t − tof| < TofTolFrac·tof

        // shortWay = the transfer angle Δν < π (the usual prograde direct intercept); false = the long way.
        public static LambertSolution Solve(Vec3 r1v, Vec3 r2v, double tofS, double mu, bool shortWay)
        {
            LambertSolution sol = new LambertSolution();
            double r1 = r1v.Magnitude, r2 = r2v.Magnitude;
            if (mu <= 0.0 || r1 < 1e-6 || r2 < 1e-6 || tofS <= 0.0) return sol;

            double sqrtMu = Math.Sqrt(mu);
            double cosdnu = Vec3.Dot(r1v, r2v) / (r1 * r2);
            if (cosdnu > 1.0) cosdnu = 1.0; else if (cosdnu < -1.0) cosdnu = -1.0;
            double dm = shortWay ? 1.0 : -1.0;
            double A = dm * Math.Sqrt(r1 * r2 * (1.0 + cosdnu));
            if (Math.Abs(A) < 1e-9) return sol;                    // 180° / collinear: geometry degenerate

            double psiLow = -4.0 * Math.PI * Math.PI;              // allow hyperbolic-fast transfers
            double psiUp = 4.0 * Math.PI * Math.PI;                // just under one full revolution
            double psi = 0.0;
            double tol = TofTolFrac * tofS;
            double y = r1 + r2;
            int iter = 0;
            bool converged = false;
            for (; iter < MaxIter; iter++)
            {
                double c2 = Conic.StumpffC(psi);
                double c3 = Conic.StumpffS(psi);
                if (c2 <= 0.0) { psiLow = psi; psi = 0.5 * (psiUp + psiLow); continue; }
                y = r1 + r2 + A * (psi * c3 - 1.0) / Math.Sqrt(c2);
                if (A > 0.0 && y < 0.0) { psiLow = psi; psi = 0.5 * (psiUp + psiLow); continue; }  // raise ψ until y≥0
                double chi = Math.Sqrt(y / c2);
                double t = (chi * chi * chi * c3 + A * Math.Sqrt(y)) / sqrtMu;
                if (Math.Abs(t - tofS) < tol) { converged = true; break; }
                if (t <= tofS) psiLow = psi; else psiUp = psi;      // t is monotone increasing in ψ
                psi = 0.5 * (psiUp + psiLow);
            }

            double g = A * Math.Sqrt(y / mu);
            if (!converged || Math.Abs(g) < 1e-12) { sol.Iterations = iter; return sol; }
            double f = 1.0 - y / r1;
            double gdot = 1.0 - y / r2;
            sol.V1 = (r2v - r1v * f) * (1.0 / g);
            sol.V2 = (r2v * gdot - r1v) * (1.0 / g);
            sol.Ok = sol.V1.IsFinite && sol.V2.IsFinite;
            sol.Iterations = iter;
            return sol;
        }
    }
}
