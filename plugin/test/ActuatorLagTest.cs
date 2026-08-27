// Tests for pure/ActuatorLag.cs (B4) — the first-order actuator model + lead compensation.
// The model must lag with the response speed and converge; the compensator must invert it (issuing the
// compensated command makes the actuator reach the demand in one tick when unclamped), command harder when
// slow, not over-command when fast, and clamp to the rail.
using System;
using DragonScreen;

public static class ActuatorLagTest
{
    static int checks = 0, failures = 0;
    static void Near(string what, double got, double want, double tol)
    { checks++; if (Math.Abs(got - want) > tol) { failures++; Console.WriteLine("  FAIL  " + what + "   got " + got.ToString("F5") + " want " + want.ToString("F5")); } }
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen B4 ActuatorLag tests");
        double dt = 0.02;

        // ---- model: a fast actuator (dt·speed ≥ 1) reaches the command in one tick ----
        Near("fast actuator reaches command", ActuatorLag.Step(0.0, 0.8, 1000.0, dt), 0.8, 1e-9);

        // ---- model: a slow actuator closes a fraction α=dt·speed of the gap ----
        double speedSlow = 10.0;                 // α = 0.02·10 = 0.2
        Near("slow actuator closes α of the gap", ActuatorLag.Step(0.0, 1.0, speedSlow, dt), 0.2, 1e-9);

        // ---- model converges toward a held command ----
        double a = 0.0;
        for (int i = 0; i < 200; i++) a = ActuatorLag.Step(a, 0.5, speedSlow, dt);
        Near("model converges to the held command", a, 0.5, 1e-3);

        // ---- compensator inverts the model: Step(actual, Compensate(...)) == desired (command stays in-rail) ----
        double actual = 0.3, desired = 0.4;      // gap 0.1 / α 0.2 → cmd 0.8, within [−1,1]
        double cmd = ActuatorLag.Compensate(actual, desired, speedSlow, dt);
        Near("compensated command reaches the demand next tick", ActuatorLag.Step(actual, cmd, speedSlow, dt), desired, 1e-9);
        Check("commands HARDER when slow (cmd past the demand)", cmd > desired, "cmd=" + cmd.ToString("F4"));

        // ---- fast actuator: no over-command (cmd ≈ desired) ----
        Near("fast actuator: cmd == desired (no over-command)", ActuatorLag.Compensate(0.3, 0.45, 1000.0, dt), 0.45, 1e-9);

        // ---- clamps to the rail for a big gap on a slow actuator ----
        Check("compensator clamps to +1", ActuatorLag.Compensate(0.0, 0.9, speedSlow, dt) == 1.0, "");
        Check("compensator clamps to −1", ActuatorLag.Compensate(0.0, -0.9, speedSlow, dt) == -1.0, "");

        // ---- no demand change when already there ----
        Check("desired == actual → no change", ActuatorLag.Compensate(0.4, 0.4, speedSlow, dt) == 0.4, "");

        // ---- frozen actuator drives toward the demand rail, bounded ----
        Check("frozen actuator → +1 toward a higher demand", ActuatorLag.Compensate(0.0, 0.2, 0.0, dt) == 1.0, "");
        Check("frozen actuator → −1 toward a lower demand", ActuatorLag.Compensate(0.0, -0.2, 0.0, dt) == -1.0, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
