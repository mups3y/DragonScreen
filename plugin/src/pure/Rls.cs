// DragonScreen — Rls  (autopilot rebuild L6 self-cal: Recursive Least Squares with a variable
//                       forgetting factor — the estimator primitive that kills tuned constants)
// ============================================================================================
// The standard onboard parameter-identification method (docs/TRUE_AUTOPILOT_ARCHITECTURE.md §10; proven
// for spacecraft mass-property + disturbance-torque estimation). Scalar model  y = φ·θ + noise; each
// sample updates the estimate θ and its covariance P:
//     e = y − φ·θ                              (innovation / prediction error, BEFORE the update)
//     λ = λ_max − (λ_max−λ_min)·min(1, e²/e_ref²)  (VARIABLE forgetting: big error → λ→λ_min → forget
//                                                faster → track; small error → λ→λ_max → smooth out noise)
//     K = P·φ / (λ + φ²·P)                      (gain)
//     θ = θ + K·e
//     P = (P − K·φ·P) / λ
// Multiple concurrent scalar RLS (pure/SelfCal.cs) segment the unknowns into fast, compact estimators.
// With φ≡1 this reduces to an adaptive EWMA (α = 1−λ) — a self-tuning smoother. λ_max is held just BELOW
// 1 so a little forgetting always remains: otherwise, in a long steady stretch, λ=1 drives P→0 and the
// estimator falls asleep, unable to track a later step (thrust change, stage, entry). PURE + headless-
// tested: converges to a known θ from noisy samples, and the VFF tracks a step change faster than fixed λ.
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct RlsScalar
    {
        public double Theta;   // current parameter estimate
        public double P;       // covariance (uncertainty; shrinks as the estimate settles)
        public double Lambda;  // the forgetting factor used on the last sample (diagnostic)
        public bool Init;

        public static RlsScalar Seed(double theta0, double p0)
        { RlsScalar r; r.Theta = theta0; r.P = p0; r.Lambda = 1.0; r.Init = true; return r; }
    }

    public static class Rls
    {
        public const double Pmin = 1e-12;
        public const double Pmax = 1e12;
        // λ_max < 1 so steady-state forgetting never fully stops (keeps the estimator able to track a
        // later change). Regression of a truly constant parameter still converges tightly at this λ.
        [Tunable] public static double LambdaMax = 0.99;

        // One RLS sample. p0 seeds the covariance on first use; lambdaMin is the fastest forgetting
        // (e.g. 0.9); eRef is the innovation scale for the VFF (≤0 disables the VFF → fixed λ = λ_max).
        public static double Update(ref RlsScalar s, double phi, double y,
                                    double p0, double lambdaMin, double eRef)
        {
            if (!s.Init)
            {
                s.Theta = Math.Abs(phi) > 1e-12 ? y / phi : 0.0;   // seed from the first clean sample
                s.P = p0 > 0.0 ? p0 : 1.0;
                s.Init = true;
                s.Lambda = LambdaMax;
                return s.Theta;
            }
            if (Math.Abs(phi) < 1e-15) return s.Theta;             // θ is unobservable this sample

            double e = y - phi * s.Theta;                          // innovation

            // variable forgetting factor, bounded to [lambdaMin, LambdaMax]
            double lambda = LambdaMax;
            if (eRef > 0.0)
            {
                double r = (e * e) / (eRef * eRef);
                if (r > 1.0) r = 1.0;
                lambda = LambdaMax - (LambdaMax - lambdaMin) * r;
                if (lambda < lambdaMin) lambda = lambdaMin;
                if (lambda > LambdaMax) lambda = LambdaMax;
            }
            s.Lambda = lambda;

            double denom = lambda + phi * phi * s.P;
            if (Math.Abs(denom) < 1e-300) return s.Theta;
            double k = s.P * phi / denom;
            s.Theta += k * e;
            s.P = (s.P - k * phi * s.P) / lambda;
            if (s.P < Pmin) s.P = Pmin; else if (s.P > Pmax) s.P = Pmax;
            return s.Theta;
        }

        // Adaptive smoother: the φ≡1 special case (estimate the mean of a noisy signal, VFF tracking).
        public static double Smooth(ref RlsScalar s, double y, double p0, double lambdaMin, double eRef)
        {
            return Update(ref s, 1.0, y, p0, lambdaMin, eRef);
        }
    }
}
