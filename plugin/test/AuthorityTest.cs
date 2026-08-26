// Tests for the control-authority helpers (pure/Authority.cs) — the vehicle knowing its own limits.
using System;
using DragonScreen;

public static class AuthorityTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }
    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, got.ToString("F4") + " vs " + want.ToString("F4")); }

    public static int Run()
    {
        Console.WriteLine("DragonScreen control-authority tests");

        // alpha = torque / inertia
        Near("angular accel = torque/inertia", Authority.AngularAccel(1000.0, 500.0), 2.0, 1e-9);
        Check("zero inertia gives no accel (guard, not a divide-by-zero)",
              Authority.AngularAccel(1000.0, 0.0) == 0.0, "");
        Check("zero torque gives no accel", Authority.AngularAccel(0.0, 500.0) == 0.0, "");

        // arrestable rate omega_max = sqrt(2 * alpha * theta)
        Near("arrestable rate sqrt(2*alpha*theta)", Authority.ArrestableRate(2.0, 1.0), 2.0, 1e-9);
        Near("...at half the angle", Authority.ArrestableRate(2.0, 0.5), Math.Sqrt(2.0), 1e-9);
        Check("a bigger error allows a faster rate",
              Authority.ArrestableRate(2.0, 1.0) > Authority.ArrestableRate(2.0, 0.5), "");
        Check("more authority allows a faster rate",
              Authority.ArrestableRate(4.0, 1.0) > Authority.ArrestableRate(2.0, 1.0), "");
        Check("no error -> no rate (settled)", Authority.ArrestableRate(2.0, 0.0) == 0.0, "");
        Check("no authority -> no rate (cannot move, cannot command)",
              Authority.ArrestableRate(0.0, 1.0) == 0.0, "");

        // torque summation (glue collects per-effector contributions; negatives ignored)
        Near("torque sums the positive contributions",
             Authority.SumTorque(new double[] { 100.0, 200.0, -50.0, 50.0 }), 350.0, 1e-9);
        Check("null contributions -> zero", Authority.SumTorque(null) == 0.0, "");

        // the struct
        ControlAuthority a = new ControlAuthority {
            PitchTorqueNm = 1000.0, YawTorqueNm = 1000.0, RollTorqueNm = 200.0,
            PitchInertiaKgM2 = 500.0, YawInertiaKgM2 = 500.0, RollInertiaKgM2 = 100.0 };
        Near("struct pitch accel", a.PitchAccel, 2.0, 1e-9);
        Near("struct roll accel", a.RollAccel, 2.0, 1e-9);
        Check("has pitch/yaw authority", a.AnyPitchYaw, "");
        ControlAuthority dead = new ControlAuthority();
        Check("a vehicle with no authority reports none", !dead.AnyPitchYaw, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
