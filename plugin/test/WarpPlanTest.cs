// Tests for pure/WarpPlan.cs (mission-conductor warp). The decisive properties: the chosen on-rails rate can
// NEVER overshoot the drop-out point in one decision window, and the safe rate ladders DOWN monotonically as
// the drop-out point nears (never speeds up on approach) — so a burn/maneuver is never overshot out of warp.
using System;
using DragonScreen;

public static class WarpPlanTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen WarpPlan (mission-conductor warp) tests");
        double[] rates = { 1, 5, 10, 50, 100, 1000, 10000, 100000 };
        double tick = 0.02;   // one physics frame (s of real time)

        // ---- drop-out / gates ----
        Check("DropOutUT is BurnLeadS before the event", Math.Abs(WarpPlan.DropOutUT(1000.0) - (1000.0 - WarpPlan.BurnLeadS)) < 1e-9, "");
        Check("ShouldWarp: a big gap warps", WarpPlan.ShouldWarp(3600.0), "");
        Check("ShouldWarp: inside the lead does NOT warp", !WarpPlan.ShouldWarp(WarpPlan.BurnLeadS + 1.0), "");
        Check("MustBeRealtime just before the event", WarpPlan.MustBeRealtime(WarpPlan.BurnLeadS), "");
        Check("MustBeRealtime false far out", !WarpPlan.MustBeRealtime(10000.0), "");

        // ---- essentially there → 1× ----
        Check("at the drop-out point → 1×", WarpPlan.SafeRate(0.0, rates, tick) == 1.0, "");
        Check("inside the settle margin → 1×", WarpPlan.SafeRate(WarpPlan.SettleMarginS * 0.5, rates, tick) == 1.0, "");
        Check("far out → the maximum rate", WarpPlan.SafeRate(1.0e9, rates, tick) == 100000.0, WarpPlan.SafeRate(1.0e9, rates, tick).ToString());

        // ---- THE overshoot guard + monotonicity, swept across the whole approach ----
        double prev = double.PositiveInfinity;
        bool monotone = true, noOvershoot = true;
        for (double ttd = 200000.0; ttd >= 0.0; ttd -= 25.0)
        {
            double r = WarpPlan.SafeRate(ttd, rates, tick);
            // never speeds up as we get closer
            if (r > prev + 1e-9) monotone = false;
            prev = r;
            // the chosen rate cannot advance past the drop-out point within the lookahead window
            if (r > 1.0 && WarpPlan.LookaheadTicks * r * tick >= ttd + 1e-9) noOvershoot = false;
        }
        Check("safe rate ladders DOWN monotonically on approach", monotone, "");
        Check("chosen rate can never overshoot the drop-out in one window", noOvershoot, "");

        // ---- a concrete step-down example ----
        Check("close approach (2 s) → 1×", WarpPlan.SafeRate(2.0, rates, tick) == 1.0, WarpPlan.SafeRate(2.0, rates, tick).ToString());
        double rMid = WarpPlan.SafeRate(60.0, rates, tick);   // 60 s out: high rates (100000·0.02·3=6000 s) far too fast → a modest rate
        Check("mid approach picks a rate whose window fits the time left", WarpPlan.LookaheadTicks * rMid * tick < 60.0, "r=" + rMid);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
