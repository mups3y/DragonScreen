// DragonScreen — RcsBalance  (autopilot rebuild B3: the RCS translation balancer — pure translation)
// ============================================================================================
// The Dragon has no reaction wheels, so its 16 Dracos do BOTH attitude and translation, and firing the wrong
// set to translate induces a rotation. This picks the thruster-activation set that produces a demanded
// TRANSLATION while nulling net torque (TCA §1b: run the balancing solver with needed_torque = 0 and the
// translation as the thrust objective). Method: SELECT the thrusters that push toward the demand (force·dir̂ >
// threshold), then pure/ThrustBalance trims their limiters to null torque. A physically unbalanced layout can
// null the thrust almost entirely — Feasible/NetForce tell the caller how much translation actually survived.
// Pure; the glue builds each thruster's force (thrust dir × magnitude) + torque (position × force about CoM)
// and applies the returned per-thruster activation. Also drives the attitude-first-then-translate discipline:
// call this only once pointed, then fire the balanced set.
// ============================================================================================
// ============================================================================================
// ⚠ W2 MARKING (2026-09-04) — COMMENT ONLY, NOTHING CHANGED. R1 §7.4: SelectDotFrac below is a
// "researched default" (`e90a63f`) whose source is named nowhere in this repo, and R1 §5.1 records
// this path as DIAGNOSTIC ONLY — never actuated, never flown. UN-CONVERGED / UNATTRIBUTED; establish
// the regime from a recorded flight before trusting it (§B16.8). See pure/ThrustBalance.cs's marking.
// ============================================================================================
namespace DragonScreen
{
    public static class RcsBalance
    {
        // A thruster serves the demand when its force projects onto the demand direction beyond this (fraction
        // of full). >0 avoids firing near-perpendicular thrusters that add little translation but cost torque.
        [Tunable] public static double SelectDotFrac = 0.1;

        // force[i]/torque[i] = each thruster's force/torque at FULL activation (body frame). demandDirBody =
        // the desired translation direction (need not be unit). Returns per-thruster activation [0,1].
        public static BalanceResult Translate(Vec3[] force, Vec3[] torque, Vec3 demandDirBody)
        {
            int n = force == null ? 0 : force.Length;
            double[] nominal = new double[n];
            Vec3 dir = demandDirBody.Magnitude > 1e-9 ? demandDirBody.Normalized : Vec3.Zero;
            for (int i = 0; i < n; i++)
            {
                double fMag = force[i].Magnitude;
                // fire a thruster only if its thrust points meaningfully toward the demand.
                nominal[i] = (fMag > 1e-9 && Vec3.Dot(force[i], dir) > SelectDotFrac * fMag) ? 1.0 : 0.0;
            }
            return ThrustBalance.Solve(force, torque, nominal, Vec3.Zero);
        }
    }
}
