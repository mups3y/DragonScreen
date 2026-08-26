// DragonScreen — SelfCal  (autopilot rebuild L6: in-flight self-calibration — the concurrent RLS bank)
// ============================================================================================
// The piece that makes CLAUDE a TRUE autopilot: it estimates the vehicle's OWN parameters in flight so
// the guidance's "constants" become MEASUREMENTS, never hand-tuned guesses (docs/TRUE_AUTOPILOT_
// ARCHITECTURE.md §10). A bank of concurrent scalar RLS estimators (pure/Rls.cs), each fed the live
// signal it can observe, each exposing its current best estimate for the guidance to read back:
//   • Thrust        — F = a·m (measured accel × mass), smoothed; VFF tracks throttle/stage/engine-mode.
//   • Ballistic β   — dragAccel = ½ρv²·(1/β): regress 1/β on q, expose β (drag prediction, L1).
//   • Control eff.  — α = τ·(1/I): regress angular-accel on commanded torque, expose 1/I and available τ.
//   • Entry L/D     — smooth the measured L/D (Trajectory.MeasureAero) so the entry predictor tracks trim.
//   • Steer gain    — response = g·command: expose the SIGN and SCALE of the steering response, so a
//                     flipped-frame sign (which has cost real flights) is DETECTED and corrected, not tuned.
// PURE + headless-tested. The glue feeds live measurements each tick and reads the estimates back into the
// guidance; this file owns none of that — it is the estimator math only.
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct SelfCalState
    {
        public RlsScalar Thrust;      // θ = thrust force (N or kN, caller's unit)
        public RlsScalar InvBeta;     // θ = 1/ballistic-coefficient
        public RlsScalar InvInertia;  // θ = 1/I (per-axis control effectiveness: α per unit torque)
        public RlsScalar LoverD;      // θ = lift-to-drag
        public RlsScalar SteerGain;   // θ = response per unit steering command (sign + scale)
    }

    public static class SelfCal
    {
        // ---- VFF tuning per estimator (research-seeded scales; λ_min = fastest forgetting) ----
        [Tunable] public static double LambdaMin = 0.9;
        [Tunable] public static double ThrustP0 = 1e6,  ThrustERef = 5.0e4;   // N
        [Tunable] public static double BetaP0 = 1e3,    BetaERef = 5.0;       // m/s² drag-accel scale
        [Tunable] public static double InertiaP0 = 1e2, InertiaERef = 0.05;   // rad/s² scale
        [Tunable] public static double LdP0 = 1.0,      LdERef = 0.1;         // L/D scale
        [Tunable] public static double SteerP0 = 1.0,   SteerERef = 1.0;      // response scale

        // Thrust F = a·m, smoothed. VFF lets it jump when the throttle / engine mode changes.
        public static double Thrust(ref SelfCalState s, double accelMps2, double massKg)
        {
            return Rls.Smooth(ref s.Thrust, accelMps2 * massKg, ThrustP0, LambdaMin, ThrustERef);
        }

        // Ballistic coefficient from measured drag: dragAccel = q·(1/β), q = ½ρv². Regress 1/β on q.
        public static double BallisticCoefficient(ref SelfCalState s, double dragAccelMps2, double qPa)
        {
            double invBeta = Rls.Update(ref s.InvBeta, qPa, dragAccelMps2, BetaP0, LambdaMin, BetaERef);
            return invBeta > 1e-9 ? 1.0 / invBeta : 0.0;   // β
        }

        // Control effectiveness: α = τ·(1/I). Regress measured angular accel on commanded torque → 1/I.
        public static double InverseInertia(ref SelfCalState s, double angAccelRadS2, double torqueNm)
        {
            return Rls.Update(ref s.InvInertia, torqueNm, angAccelRadS2, InertiaP0, LambdaMin, InertiaERef);
        }
        // Available torque to reach a desired angular accel, from the estimated 1/I (τ = α / (1/I)).
        public static double TorqueFor(SelfCalState s, double desiredAngAccel)
        {
            double inv = s.InvInertia.Theta;
            return Math.Abs(inv) > 1e-9 ? desiredAngAccel / inv : 0.0;
        }

        // Entry lift-to-drag: smooth the live measurement so the entry predictor tracks the real trim.
        public static double LiftToDrag(ref SelfCalState s, double measuredLoverD)
        {
            return Rls.Smooth(ref s.LoverD, measuredLoverD, LdP0, LambdaMin, LdERef);
        }

        // Steering response gain: response = g·command. Exposes the SIGN and SCALE of the steering law so
        // a wrong-frame sign is caught. Only observable when a real command was applied (|command| > eps).
        public static double SteerResponse(ref SelfCalState s, double response, double command)
        {
            if (Math.Abs(command) < 1e-6) return s.SteerGain.Theta;   // no command → nothing to learn
            return Rls.Update(ref s.SteerGain, command, response, SteerP0, LambdaMin, SteerERef);
        }
        // +1 / −1: is the steering response in the commanded direction? (−1 → the guidance sign is flipped.)
        public static int SteerSign(SelfCalState s)
        {
            if (!s.SteerGain.Init) return +1;                          // assume nominal until observed
            return s.SteerGain.Theta >= 0.0 ? +1 : -1;
        }
    }
}
