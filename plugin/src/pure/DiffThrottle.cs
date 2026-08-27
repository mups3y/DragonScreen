// DragonScreen — DiffThrottle  (autopilot rebuild B3: engine-out differential octaweb throttle)
// ============================================================================================
// When an octaweb engine fails, the remaining engines' thrust is asymmetric and induces a net torque the
// gimbal must fight. This rebalances it AT THE SOURCE: throttle the engines differentially so the net torque
// holds the guidance's demand (≈0 for a straight axial burn) while keeping as much axial thrust as possible.
// A thin wrapper over pure/ThrustBalance (all live engines at nominal 1.0 — "keep full thrust, steal a little
// for torque"); a FAILED engine is simply absent from the arrays. The glue (Actuator) builds each engine's
// force (thrust dir × magnitude) + torque (position × force, about CoM) from the live vessel and applies the
// returned per-engine thrustPercentage. Feasible=false → the FDIR "insufficient control authority" signal.
// ============================================================================================
namespace DragonScreen
{
    public static class DiffThrottle
    {
        // engineForce[i]/engineTorque[i] = the live engine's force/torque at FULL throttle (body frame).
        // demandedTorque = the net control torque to hold (Vec3.Zero for a pure axial burn).
        public static BalanceResult Solve(Vec3[] engineForce, Vec3[] engineTorque, Vec3 demandedTorque)
        {
            int n = engineForce == null ? 0 : engineForce.Length;
            double[] nominal = new double[n];
            for (int i = 0; i < n; i++) nominal[i] = 1.0;      // engines run full; the solver trims for torque
            return ThrustBalance.Solve(engineForce, engineTorque, nominal, demandedTorque);
        }
    }
}
