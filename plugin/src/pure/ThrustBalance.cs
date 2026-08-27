// DragonScreen — ThrustBalance  (autopilot rebuild B3: the shared thrust-limiter balancing solver)
// ============================================================================================
// The core both P0 balancers share (docs/MODS_HARVEST_2.md §1, distilled from TCA's EngineOptimizer.cs /
// RCSOptimizer.cs): "change the thrust limiters of engines / RCS thrusters in real time to control both total
// thrust AND torque." Given a set of effectors, each producing a force and a torque at full activation, find
// per-effector limits in [0, nominal] that drive the NET torque to a demanded value (usually zero) while
// keeping as much of the thrust objective as possible.
//
// Method: iterative torque-nulling projected descent — REDUCE-ONLY (start at nominal, never exceed it: "keep
// the thrust, steal a little for torque"). Each pass reduces the limit of effectors whose torque aligns with
// the residual imbalance (projected-gradient step on ‖Σ limit·τ − τ_demand‖², clamped to [0, nominal]). It is
// a bounded descent (MaxIterations), keeps the best limit-set, and raises Feasible=false when a demanded
// torque could not be met — the FDIR "insufficient control authority" signal. Pure + headless-tested; the glue
// builds the effectors from the live vessel (thrust dir × magnitude = force; position×force = torque about CoM)
// and applies the limits (engine thrustPercentage / RCS thrustPercentage).
//
// Used by pure/DiffThrottle.cs (engine-out octaweb rebalance) and pure/RcsBalance.cs (pure translation).
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct BalanceResult
    {
        public double[] Limits;      // solved activation per effector, in [0, nominal]
        public Vec3 NetForceN;       // Σ limit·force
        public Vec3 NetTorqueNm;     // Σ limit·torque
        public double TorqueErrNm;   // ‖NetTorque − demanded‖
        public bool Feasible;        // torque error within the cutoff
        public int Iterations;
    }

    public static class ThrustBalance
    {
        [Tunable] public static int MaxIterations = 50;          // TCA: 50/frame
        [Tunable] public static double TorqueCutoffNm = 1.0;     // "met" when residual below this
        [Tunable] public static double PrecisionFrac = 0.01;     // stop when |Δerr| < PrecisionFrac·err
        [Tunable] public static double StepFactor = 1.0;         // ≤1 keeps the projected-gradient step stable

        // Solve the limiter set. force[i]/torque[i] are the effector's output at FULL activation (body frame);
        // nominal[i] is its starting/max limit (engines 1.0; an RCS thruster 1.0 only if it serves the demand,
        // else 0). demandedTorque is the net torque to hold (Vec3.Zero for a torque-free translation / trim).
        public static BalanceResult Solve(Vec3[] force, Vec3[] torque, double[] nominal, Vec3 demandedTorque)
        {
            BalanceResult r = new BalanceResult();
            int n = force == null ? 0 : force.Length;
            if (n == 0 || torque == null || torque.Length != n || nominal == null || nominal.Length != n)
            {
                r.Limits = new double[0]; r.Feasible = true; return r;   // nothing to balance
            }

            double[] lim = new double[n];
            for (int i = 0; i < n; i++) lim[i] = Clamp(nominal[i], 0.0, nominal[i]);

            // projected-gradient step size: 1 / Σ‖τ‖² (Jacobi bound) keeps the quadratic descent non-divergent.
            double tSq = 0.0;
            for (int i = 0; i < n; i++) tSq += Vec3.Dot(torque[i], torque[i]);
            double alpha = tSq > 1e-12 ? StepFactor / tSq : 0.0;

            double[] best = (double[])lim.Clone();
            double bestErr = double.PositiveInfinity;
            double lastErr = double.PositiveInfinity;
            int iter = 0;
            for (; iter < MaxIterations; iter++)
            {
                Vec3 net = NetTorque(torque, lim, n);
                Vec3 residual = net - demandedTorque;
                double err = residual.Magnitude;
                if (err < bestErr) { bestErr = err; Array.Copy(lim, best, n); }
                if (err < TorqueCutoffNm) break;
                if (lastErr - err >= 0.0 && lastErr - err < PrecisionFrac * lastErr) break;  // converged
                lastErr = err;
                if (alpha <= 0.0) break;                                     // no torque authority at all

                // reduce-only projected-gradient descent: x ← clamp(x − α·∂‖res‖²/∂x , 0, nominal)
                for (int i = 0; i < n; i++)
                    lim[i] = Clamp(lim[i] - alpha * Vec3.Dot(torque[i], residual), 0.0, nominal[i]);
            }

            r.Limits = best;
            r.NetTorqueNm = NetTorque(torque, best, n);
            r.NetForceN = NetForce(force, best, n);
            r.TorqueErrNm = (r.NetTorqueNm - demandedTorque).Magnitude;
            r.Feasible = r.TorqueErrNm < TorqueCutoffNm;
            r.Iterations = iter;
            return r;
        }

        static Vec3 NetTorque(Vec3[] torque, double[] lim, int n)
        {
            Vec3 s = Vec3.Zero;
            for (int i = 0; i < n; i++) s = s + torque[i] * lim[i];
            return s;
        }
        static Vec3 NetForce(Vec3[] force, double[] lim, int n)
        {
            Vec3 s = Vec3.Zero;
            for (int i = 0; i < n; i++) s = s + force[i] * lim[i];
            return s;
        }
        static double Clamp(double x, double lo, double hi)
        {
            if (x < lo) return lo; if (x > hi) return hi; return x;
        }
    }
}
