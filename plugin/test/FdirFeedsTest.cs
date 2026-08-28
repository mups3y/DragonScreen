// Tests for L5 FDIR feed-shaping (pure/FdirFeeds.cs, task T2b): the honest normalisation + the guards that
// keep an UNMEASURABLE moment reading nominal instead of false-tripping the spine.
using System;
using DragonScreen;

public static class FdirFeedsTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen L5 FDIR feed-shaping tests");

        // ---- ThrustDeliveredFrac ----
        Check("healthy full-throttle burn reads ~1.0",
              Math.Abs(FdirFeeds.ThrustDeliveredFrac(6681.0, 6681.0, 1.0) - 1.0) < 1e-6, "");
        Check("healthy half-throttle burn still reads ~1.0 (throttle folded in)",
              Math.Abs(FdirFeeds.ThrustDeliveredFrac(3340.5, 6681.0, 0.5) - 1.0) < 1e-3,
              FdirFeeds.ThrustDeliveredFrac(3340.5, 6681.0, 0.5).ToString());
        Check("one engine of nine out reads ~0.89 (above the 0.6 trip → tolerated)",
              Math.Abs(FdirFeeds.ThrustDeliveredFrac(6681.0 * 8.0 / 9.0, 6681.0, 1.0) - 8.0 / 9.0) < 1e-3, "");
        Check("catastrophic thrust loss reads well below the trip fraction",
              FdirFeeds.ThrustDeliveredFrac(2000.0, 6681.0, 1.0) < 0.6, "");
        Check("a coast (throttle ~0) is nominal, never a shortfall",
              FdirFeeds.ThrustDeliveredFrac(0.0, 6681.0, 0.0) == 1.0, "");
        Check("a Draco-only burn (no main engines committed) is nominal",
              FdirFeeds.ThrustDeliveredFrac(0.0, 0.0, 1.0) == 1.0, "");
        Check("negative/garbage thrust clamps to 0 (never negative)",
              FdirFeeds.ThrustDeliveredFrac(-5.0, 6681.0, 1.0) == 0.0, "");

        // ---- ControlLost (tumble predicate) ----
        // representative thresholds: no authority < 50 N·m, tumbling > 0.15 rad/s (~8.6°/s), off > 30°
        Check("healthy ascent (high authority) is NOT a loss of control",
              !FdirFeeds.ControlLost(true, 12000.0, 0.02, 1.0, 50.0, 0.15, 30.0), "");
        Check("healthy hard slew (authority present, big error) is NOT a loss of control",
              !FdirFeeds.ControlLost(true, 8000.0, 0.30, 120.0, 50.0, 0.15, 30.0), "");
        Check("no-authority tumble while holding IS a loss of control",
              FdirFeeds.ControlLost(true, 5.0, 0.30, 120.0, 50.0, 0.15, 30.0), "");
        Check("no-authority + spinning but pointing fine is NOT a loss (recoverable)",
              !FdirFeeds.ControlLost(true, 5.0, 0.30, 2.0, 50.0, 0.15, 30.0), "");
        Check("no-authority + off-target but not spinning is NOT a loss",
              !FdirFeeds.ControlLost(true, 5.0, 0.01, 120.0, 50.0, 0.15, 30.0), "");
        Check("not holding attitude (coast, loop released) is never a loss of control",
              !FdirFeeds.ControlLost(false, 0.0, 5.0, 180.0, 50.0, 0.15, 30.0), "");

        // ---- ClosingProgress ----
        Check("actively closing feeds the closing rate through",
              Math.Abs(FdirFeeds.ClosingProgress(2.5, true) - 2.5) < 1e-9, "");
        Check("actively burning but SEPARATING feeds a negative (stall) rate through",
              FdirFeeds.ClosingProgress(-1.0, true) < 0.0, "");
        Check("an intended coast/hold (not actively closing) reads nominal +1, never a stall",
              FdirFeeds.ClosingProgress(-5.0, false) == 1.0, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
