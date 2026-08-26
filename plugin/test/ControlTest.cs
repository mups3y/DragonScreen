// Tests for the L2 control laws (pure/ControlLaw.cs) — attitude rate/actuation, throttle limiter, RCS.
using System;
using DragonScreen;

public static class ControlTest
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
        Console.WriteLine("DragonScreen L2 control tests");

        // ---- RATE COMMAND: proportional near zero, braking-curve for large errors, never un-arrestable ----
        Check("inside the deadband commands no rate", ControlLaw.RateCommand(0.001, 1.0, 0.0) == 0.0, "");
        // small error (θ=0.5, α=1): linear 0.5 < brake 0.9√1=0.636 → proportional 0.5
        Near("small error is proportional (θ/τ)", ControlLaw.RateCommand(0.5, 1.0, 0.0), 0.5, 1e-9);
        // large error (θ=2, α=1): linear 2 > brake 0.9√4=1.8 → braking-curve limited to 1.8
        Near("large error is braking-curve limited", ControlLaw.RateCommand(2.0, 1.0, 0.0), 1.8, 1e-9);
        Check("a bigger error never commands a smaller rate than a small one",
              ControlLaw.RateCommand(2.0, 1.0, 0.0) > ControlLaw.RateCommand(0.5, 1.0, 0.0), "");
        // an optional max-rate cap wins if lower
        Near("an explicit max-rate cap applies", ControlLaw.RateCommand(2.0, 1.0, 1.0), 1.0, 1e-9);
        // sign follows the error
        Check("rate follows the sign of the error", ControlLaw.RateCommand(-0.5, 1.0, 0.0) < 0.0, "");
        // more authority → higher arrestable rate for the same large error
        Check("more authority allows a faster rate on a big error",
              ControlLaw.RateCommand(2.0, 4.0, 0.0) > ControlLaw.RateCommand(2.0, 1.0, 0.0), "");

        // ---- ACTUATE: torque-based, clamped, slew-limited, authority-aware ----
        Near("small rate error → proportional actuation", ControlLaw.Actuate(0.01, 100.0, 50.0, 0.0), 0.04, 1e-9);
        Near("a big rate error saturates but is slew-limited from 0", ControlLaw.Actuate(10.0, 100.0, 50.0, 0.0),
             ControlLaw.MaxSlewPerTick, 1e-9);
        Near("slew from near the rail reaches full", ControlLaw.Actuate(10.0, 100.0, 50.0, 0.95), 1.0, 1e-9);
        Check("no torque authority → no actuation (RCS must be turned on by the glue)",
              ControlLaw.Actuate(1.0, 100.0, 0.0, 0.0) == 0.0, "");
        Check("negative rate error actuates negative", ControlLaw.Actuate(-0.01, 100.0, 50.0, 0.0) < 0.0, "");

        // ---- AXIS end-to-end: drives toward the target, correct sign ----
        Check("a positive pointing error commands a positive correction",
              ControlLaw.AxisCommand(0.5, 0.0, 100.0, 50.0, 0.0, 0.0) > 0.0, "");
        Check("a negative pointing error commands a negative correction",
              ControlLaw.AxisCommand(-0.5, 0.0, 100.0, 50.0, 0.0, 0.0) < 0.0, "");
        Check("on target with no rate → no command",
              ControlLaw.AxisCommand(0.0, 0.0, 100.0, 50.0, 0.0, 0.0) == 0.0, "");

        // ---- THROTTLE LIMITER: g-limit (physics) + max-Q bucket, more restrictive wins ----
        // g-limit: 4 g on a 5 t stage with 800 kN → 4*9.80665*5000/800000 = 0.2452
        Near("g-limit throttles a light stage", ControlLaw.ThrottleLimit(1.0, 0, 20000, 35000, 0.7, 4.0, 5000, 800000),
             0.24517, 1e-4);
        // a full stack at liftoff (500 t, 7000 kN → TWR ~1.4, ~1.4 g) is far under a 4 g limit → no throttle
        Check("g-limit does not bite a heavy stage at liftoff TWR",
              ControlLaw.ThrottleLimit(1.0, 0, 20000, 35000, 0.7, 4.0, 500000, 7000000) == 1.0, "");
        // max-Q bucket: q midway [20k,35k] → throttle 0.8; at the ceiling → floor 0.7; below qSoft → full
        Near("bucket throttles down through max-Q", ControlLaw.ThrottleLimit(1.0, 30000, 20000, 35000, 0.7, 0, 0, 0),
             0.8, 1e-9);
        Near("bucket reaches its floor at the q ceiling", ControlLaw.ThrottleLimit(1.0, 35000, 20000, 35000, 0.7, 0, 0, 0),
             0.7, 1e-9);
        Check("no bucket below the soft-q threshold",
              ControlLaw.ThrottleLimit(1.0, 10000, 20000, 35000, 0.7, 0, 0, 0) == 1.0, "");
        Check("the more restrictive of g-limit and bucket wins",
              ControlLaw.ThrottleLimit(1.0, 35000, 20000, 35000, 0.7, 4.0, 5000, 800000) < 0.7, "");
        Check("a base throttle above 1 is clamped first",
              ControlLaw.ThrottleLimit(1.5, 0, 20000, 35000, 0.7, 0, 0, 0) == 1.0, "");

        // ---- RCS TRANSLATION ----
        Near("translation scales by available RCS accel", ControlLaw.TranslateAxis(2.0, 4.0), 0.5, 1e-9);
        Check("translation clamps to full demand", ControlLaw.TranslateAxis(10.0, 4.0) == 1.0, "");
        Check("translation clamps negative", ControlLaw.TranslateAxis(-10.0, 4.0) == -1.0, "");
        Check("no RCS authority → no translation", ControlLaw.TranslateAxis(2.0, 0.0) == 0.0, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
