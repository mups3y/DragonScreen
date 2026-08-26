/*
 * Tests for the clamp-release + ullage-settle gates (pure/IgnitionGate.cs).
 *
 * These pin the two safety decisions the proving flight depends on: never release the hold-downs onto an
 * engine that hasn't reached full thrust (flight-1/2 RUD), and never light an engine before the RealFuels
 * propellant is settled (a failed relight has no retry).
 */
using DragonScreen;
using System;

public static class IgnitionGateTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen ignition-gate (clamp release + ullage) tests");

        const double AVAIL = 6.68e6;   // octaweb AllEngines ~6681 kN

        // ---- clamp release ----
        Check("hold below 99% thrust",
              IgnitionGate.Evaluate(0.90 * AVAIL, AVAIL, 1, 0.3) == ClampAction.Hold, "");
        Check("release at 99% thrust, engine lit",
              IgnitionGate.Evaluate(0.99 * AVAIL, AVAIL, 1, 0.3) == ClampAction.Release, "");
        Check("release at full thrust",
              IgnitionGate.Evaluate(AVAIL, AVAIL, 1, 0.5) == ClampAction.Release, "");
        Check("do NOT release with no engine lit (litCount 0)",
              IgnitionGate.Evaluate(AVAIL, AVAIL, 0, 0.3) != ClampAction.Release, "");
        Check("do NOT release with no available thrust",
              IgnitionGate.Evaluate(0, 0, 0, 0.3) != ClampAction.Release, "");
        Check("still holding just before timeout",
              IgnitionGate.Evaluate(0.5 * AVAIL, AVAIL, 1, IgnitionGate.MaxHoldS - 0.1) == ClampAction.Hold, "");
        Check("SAFE-ABORT after timeout with thrust still low (failed light)",
              IgnitionGate.Evaluate(0.5 * AVAIL, AVAIL, 1, IgnitionGate.MaxHoldS + 0.1) == ClampAction.SafeAbort, "");
        Check("thrust reaching 99% even at timeout still RELEASES (not abort)",
              IgnitionGate.Evaluate(AVAIL, AVAIL, 1, IgnitionGate.MaxHoldS + 1.0) == ClampAction.Release, "");

        // ---- ullage settle ----
        Check("not ready during the minimum separation coast",
              !IgnitionGate.UllageReady(1.0, 0.5, 1.0, 6.0), "");
        Check("ready once past min coast AND settled",
              IgnitionGate.UllageReady(0.999, 1.5, 1.0, 6.0), "");
        Check("not ready past min coast but still unsettled",
              !IgnitionGate.UllageReady(0.80, 2.0, 1.0, 6.0), "");
        Check("ready at the backstop even if never settled",
              IgnitionGate.UllageReady(0.80, 6.5, 1.0, 6.0), "");
        Check("exactly at the 0.996 threshold counts as settled",
              IgnitionGate.UllageReady(IgnitionGate.UllageStable, 1.5, 1.0, 6.0), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
