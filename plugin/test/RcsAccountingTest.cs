/*
 * Tests for pure/RcsAccounting — the physics-rate RCS actuation accountant. Verifies the category split
 * (attitude-only / translation-only / SIMULTANEOUS / neither), the delivered-impulse bucketing, the
 * requested/applied command-second integrals, and Reset. Instrumentation logic, so it must be exact.
 */
using DragonScreen;
using System;

public static class RcsAccountingTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }
    static void Near(string what, double got, double want)
    { checks++; if (Math.Abs(got - want) > 1e-9) { failures++; Console.WriteLine("  FAIL  " + what + "   got " + got + " want " + want); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen RcsAccounting (physics-rate RCS accounting) tests");
        double dt = 0.02;
        var a = new RcsAccounting();

        // 10 ticks attitude-only (appAtt on, appTrans off) at 1000 N; 5 ticks translation-only at 2000 N;
        // 4 ticks SIMULTANEOUS at 3000 N; 3 ticks neither.
        for (int i = 0; i < 10; i++) a.Add(dt, true,  false, 1000, /*reqAtt*/1.0, /*appAtt*/1.0, /*reqTrans*/0.0, /*appTrans*/0.0);
        for (int i = 0; i < 5;  i++) a.Add(dt, false, true,  2000, 0.0, 0.0, 0.8, 0.8);
        for (int i = 0; i < 4;  i++) a.Add(dt, true,  true,  3000, 1.0, 1.0, 1.0, 1.0);
        for (int i = 0; i < 3;  i++) a.Add(dt, false, false, 0,    0.5, 0.0, 0.0, 0.0);   // requested att but PWPF killed it → applied 0

        Near("interval = 22 ticks", a.IntervalS, 22 * dt);
        Near("attitude-only time = 10 ticks", a.AttOnlyS, 10 * dt);
        Near("translation-only time = 5 ticks", a.TransOnlyS, 5 * dt);
        Near("simultaneous time = 4 ticks", a.BothS, 4 * dt);
        Near("neither time = 3 ticks", a.NoneS, 3 * dt);
        Near("attitude-only impulse", a.AttOnlyImpNs, 10 * 1000 * dt);
        Near("translation-only impulse", a.TransOnlyImpNs, 5 * 2000 * dt);
        Near("simultaneous impulse", a.BothImpNs, 4 * 3000 * dt);
        // requested-vs-applied: the 3 neither ticks requested att 0.5 but applied 0 → req>app (PWPF removed it).
        Near("requested-att command-seconds", a.ReqAttCs, (10 * 1.0 + 4 * 1.0 + 3 * 0.5) * dt);
        Near("applied-att command-seconds", a.AppAttCs, (10 * 1.0 + 4 * 1.0) * dt);
        Check("PWPF removed some requested attitude (req>app)", a.ReqAttCs > a.AppAttCs, "");

        // category times sum to the interval (partition, no double-count)
        Near("category times partition the interval", a.AttOnlyS + a.TransOnlyS + a.BothS + a.NoneS, a.IntervalS);

        a.Reset();
        Check("Reset zeroes the interval", a.IntervalS == 0.0, "int=" + a.IntervalS);
        Check("Reset zeroes a bucket", a.AttOnlyImpNs == 0.0 && a.BothS == 0.0, "");
        // dt<=0 is ignored (no NaN/negative accumulation)
        a.Add(0.0, true, true, 5000, 1, 1, 1, 1);
        Check("dt<=0 ignored", a.IntervalS == 0.0, "int=" + a.IntervalS);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
