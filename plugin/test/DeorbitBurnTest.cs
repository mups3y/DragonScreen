/*
 * DragonScreen headless tests - the de-orbit burn.
 *
 * The one that matters is the cut-out lead. Flight 035: "periapsis crossed the -31,800 m target at
 * t=289.53 s and the throttle stayed at 0.0642 until t=292.77 s - 3.24 s, one loop iteration -
 * ending at -39,699 m." 7.9 km of excess depth is a steeper entry than planned and the trim spends
 * the whole descent hauling the impact point back.
 */
using System;
using DragonScreen;

public static class DeorbitBurnTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    static DeorbitState Burning(double missM, double periM, double peRate)
    {
        DeorbitState s = new DeorbitState();
        s.AimMissM = missM;
        s.PeriapsisM = periM;
        s.PeriapsisRateMps = peRate;
        s.BestMissM = 9.9e12;
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen de-orbit burn tests");

        // ---- THE THROTTLE IS SQRT OF THE MISS ----
        Check("a huge miss is capped, not unbounded",
              Math.Abs(DeorbitBurn.Throttle(Burning(5000000.0, 100000.0, 0.0))
                       - DeorbitBurn.ThrottleMax) < 1e-9, "");
        Check("400 km of miss is full throttle",
              Math.Abs(DeorbitBurn.Throttle(Burning(400000.0, 100000.0, 0.0)) - 0.60) < 1e-9,
              DeorbitBurn.Throttle(Burning(400000.0, 100000.0, 0.0)).ToString("F3"));
        // sqrt(4000/400000) = 0.1 - still 10% thrust at 4 km out, which is the point of the sqrt.
        Check("4 km out it is still flying, not drifting",
              Math.Abs(DeorbitBurn.Throttle(Burning(4000.0, 100000.0, 0.0)) - 0.1) < 1e-6,
              DeorbitBurn.Throttle(Burning(4000.0, 100000.0, 0.0)).ToString("F3"));
        Check("and a sqrt law holds more thrust late than a linear one would",
              DeorbitBurn.Throttle(Burning(4000.0, 1e5, 0.0)) > 0.60 * (4000.0 / 400000.0), "");
        Check("no answer from the predictor is its own case, not a zero miss",
              Math.Abs(DeorbitBurn.Throttle(Burning(-1.0, 100000.0, 0.0))
                       - DeorbitBurn.ThrottleBlind) < 1e-9, "");

        // ---- ⛔ THE CUT-OUT LEAD ----
        // Falling at 2500 m/s, 0.35 s of lead is 875 m. Sitting 500 m above the target must already
        // trip, because by the time the next scan comes round we would be well past it.
        double rate = -2500.0;
        Check("500 m above the limit and falling fast, it cuts NOW",
              DeorbitBurn.DepthLimitReached(DeorbitBurn.PeriapsisTargetM + 500.0, rate), "");
        Check("but 5 km above it, not yet",
              !DeorbitBurn.DepthLimitReached(DeorbitBurn.PeriapsisTargetM + 5000.0, rate), "");
        Check("the projection trips EARLY, never late - the rate is negative while burning",
              DeorbitBurn.DepthLimitReached(DeorbitBurn.PeriapsisTargetM + 100.0, rate)
              && !DeorbitBurn.DepthLimitReached(DeorbitBurn.PeriapsisTargetM + 100.0, 0.0), "");
        // Without the lead, flight 035's 3.24 s at 2500 m/s is 8.1 km of overshoot. Assert the lead
        // is big enough to cover a scan interval.
        Check("the lead covers most of an aim-scan interval",
              DeorbitBurn.CutLeadS > DeorbitBurn.AimScanIntervalS * 0.5,
              DeorbitBurn.CutLeadS + " vs " + DeorbitBurn.AimScanIntervalS);

        // ---- STOP CONDITIONS ----
        string why;
        Check("inside the landing tolerance, done",
              DeorbitBurn.Complete(Burning(10.0, 50000.0, 0.0), out why), why);
        Check("and it says why", why.Length > 0, why);
        Check("a large miss with plenty of altitude keeps burning",
              !DeorbitBurn.Complete(Burning(200000.0, 50000.0, -10.0), out why), why);

        // Diverging while already close ends it - continuing trades a small miss for a steep entry.
        DeorbitState div = Burning(20000.0, 50000.0, -10.0);
        div.WorseCount = DeorbitBurn.WorseLimit + 1;
        Check("close but no longer improving, stop",
              DeorbitBurn.Complete(div, out why), why);
        // ...but the same divergence a long way out is not a reason to give up.
        DeorbitState far = Burning(200000.0, 50000.0, -10.0);
        far.WorseCount = DeorbitBurn.WorseLimit + 1;
        Check("diverging far out is not a stop - there is still range to fix",
              !DeorbitBurn.Complete(far, out why), why);

        DeorbitState dry = Burning(200000.0, 50000.0, -10.0);
        dry.UsingS2 = true; dry.S2FuelUnits = 1.0;
        Check("an empty S2 ends the burn", DeorbitBurn.Complete(dry, out why), why);

        DeorbitState runaway = Burning(200000.0, 50000.0, -10.0);
        runaway.ElapsedS = DeorbitBurn.MaxBurnS + 1.0;
        Check("and the backstop fires", DeorbitBurn.Complete(runaway, out why), why);
        Check("saying ABORTED, not looking like success", why.Contains("ABORTED"), why);

        // ---- BEST-MISS TRACKING ----
        DeorbitState t = Burning(100000.0, 50000.0, -10.0);
        DeorbitBurn.Track(ref t);
        Check("the first scan sets the best", Math.Abs(t.BestMissM - 100000.0) < 1e-9, "");
        t.AimMissM = 90000.0; DeorbitBurn.Track(ref t);
        Check("improving resets the worse count", t.WorseCount == 0, "");
        t.AimMissM = 95000.0; DeorbitBurn.Track(ref t);
        Check("a clearly worse scan counts", t.WorseCount == 1, t.WorseCount.ToString());
        t.AimMissM = 90100.0; DeorbitBurn.Track(ref t);
        Check("but noise inside the margin does not", t.WorseCount == 1, t.WorseCount.ToString());
        t.AimMissM = -1.0; DeorbitBurn.Track(ref t);
        Check("and a scan with no answer changes nothing", t.WorseCount == 1, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
