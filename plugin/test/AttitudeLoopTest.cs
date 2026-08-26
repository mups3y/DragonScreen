/*
 * Tests for the ported MechJeb BetterController attitude law (pure/AttitudeLoop.cs + Pid2).
 *
 * The important one is CONVERGENCE: a closed-loop sim of a rigid body (error' = -omega, omega' = achieved
 * alpha) must drive a large pointing error to ~0 without diverging or wild overshoot — the property the
 * three max-Q RUDs lacked. Also pins: no-authority safety, the arrestable-rate cap, the MechJeb negative
 * actuation sign convention, and the roll-gate rate damping.
 */
using DragonScreen;
using System;

public static class AttitudeLoopTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    // A representative axis: full stack ~ MOI 3e5 kg·m², gimbal torque ~1e7 N·m (ATTITUDE_CONTROL_RESEARCH).
    const double MOI = 3.0e5, CTRL = 1.0e7, DT = 0.02;

    public static int Run()
    {
        Console.WriteLine("DragonScreen attitude-loop (BetterController port) tests");

        // ---- no authority is safe: zero torque / zero MOI → zero actuation, no throw ----
        Pid2 p = new Pid2(), q = new Pid2();
        Check("zero control torque -> 0 actuation", AttitudeLoop.Axis(0.3, 0, MOI, 0.0, DT, false, p, q).Actuation == 0.0, "");
        Check("zero MOI -> 0 actuation", AttitudeLoop.Axis(0.3, 0, 0.0, CTRL, DT, false, p, q).Actuation == 0.0, "");
        Check("NaN error -> 0 actuation", AttitudeLoop.Axis(double.NaN, 0, MOI, CTRL, DT, false, p, q).Actuation == 0.0, "");

        // ---- at target, at rest → ~no command ----
        p.Reset(); q.Reset();
        double a0 = AttitudeLoop.Axis(0.0, 0.0, MOI, CTRL, DT, false, p, q).Actuation;
        Check("at target + at rest -> ~0 actuation", Math.Abs(a0) < 1e-6, "act=" + a0);

        // ---- MechJeb sign convention: positive error -> negative actuation ----
        p.Reset(); q.Reset();
        AttitudeAxisResult big = AttitudeLoop.Axis(0.5, 0.0, MOI, CTRL, DT, false, p, q);
        Check("positive error -> positive targetOmega", big.TargetOmega > 0.0, "w=" + big.TargetOmega);
        Check("positive error -> NEGATIVE actuation (KSP orientation)", big.Actuation < 0.0, "act=" + big.Actuation);
        Check("maxAlpha = ctrl/MOI", Math.Abs(big.MaxAlpha - CTRL / MOI) < 1e-9, "");

        // ---- arrestable-rate cap: targetOmega never exceeds maxAlpha·MaxStoppingTime ----
        p.Reset(); q.Reset();
        double maxOmega = (CTRL / MOI) * AttitudeLoop.MaxStoppingTime;
        AttitudeAxisResult huge = AttitudeLoop.Axis(2.0, 0.0, MOI, CTRL, DT, false, p, q);
        Check("targetOmega clamped to arrestable cap", huge.TargetOmega <= maxOmega + 1e-9, "w=" + huge.TargetOmega + " cap=" + maxOmega);

        // ---- roll gate: suppressOmega damps the rate to zero (actuation opposes omega) ----
        p.Reset(); q.Reset();
        AttitudeAxisResult damp = AttitudeLoop.Axis(0.4, +0.05, MOI, CTRL, DT, true, p, q);
        Check("roll-gate: targetOmega forced 0", damp.TargetOmega == 0.0, "");
        Check("roll-gate: positive rate -> positive actuation (opposes rate)", damp.Actuation > 0.0, "act=" + damp.Actuation);

        // ---- CONVERGENCE: a rigid body driven by the loop reaches the target and stays ----
        Check("30° slew converges to <0.5° without divergence", ConvergesFrom(30.0 * Math.PI / 180.0), "");
        Check("5° slew converges to <0.5°", ConvergesFrom(5.0 * Math.PI / 180.0), "");
        Check("120° flip converges to <0.5°", ConvergesFrom(120.0 * Math.PI / 180.0), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // Closed-loop sim of one rigid axis: error' = -omega, omega' = achieved angular accel. The effector
    // produces torque = -actuation·controlTorque (the MechJeb sign), so achieved alpha = that / MOI. Verify
    // the pointing error settles small and never blows up.
    static bool ConvergesFrom(double error0)
    {
        Pid2 pos = new Pid2(), vel = new Pid2();
        double error = error0, omega = 0.0;
        double worst = 0.0;
        int steps = (int)(20.0 / DT);   // 20 s
        for (int i = 0; i < steps; i++)
        {
            AttitudeAxisResult r = AttitudeLoop.Axis(error, omega, MOI, CTRL, DT, false, pos, vel);
            double alpha = -r.Actuation * CTRL / MOI;   // physical angular accel achieved (full authority)
            omega += alpha * DT;
            error += -omega * DT;
            double mag = Math.Abs(error);
            if (mag > worst) worst = mag;
            if (double.IsNaN(error) || mag > 4.0 * Math.PI) return false;   // diverged
        }
        // settled within 0.5°, and never overshot past ~2× the initial excursion
        return Math.Abs(error) < 0.5 * Math.PI / 180.0 && worst < 2.0 * Math.Abs(error0) + 0.05;
    }
}
