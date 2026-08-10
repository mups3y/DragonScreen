/*
 * DragonScreen headless tests - the node executor.
 *
 * Rewritten 2026-08-11 when BurnExec stopped being mine and became a port of
 * `F9I/station_ops.ks:2469 StBurnNode`. These assert F9I's law, not a plausible one.
 *
 * The two that matter most both have a flight behind them:
 *   * the half-burn lead is sized on the acceleration the burn WILL USE, not on full thrust, or
 *     "we would arrive late for our own burn"
 *   * the burn stops on an OVERSHOOT test, not a countdown - flight 013's phasing burn is what
 *     happens when a node executor does not actually govern the throttle
 */
using System;
using DragonScreen;

public static class BurnExecTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok)
        {
            failures++;
            Console.WriteLine("  FAIL  " + what + "   " + detail);
        }
    }

    static BurnState Burning(double remaining, double initial, double massT, double thrustKn)
    {
        BurnState s = new BurnState();
        s.RemainingDvMps = remaining;
        s.InitialDvMps = initial;
        s.MassT = massT;
        s.AvailableThrustKn = thrustKn;
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen burn executor tests");

        // ---- THE CRUISE ACCELERATION IS A CEILING, AND SO IS WHAT WE ACTUALLY HAVE ----
        Check("cruise accel is F9I's 1.5", Math.Abs(BurnExec.CruiseAccel - 1.5) < 1e-9, "");
        // 20 t, 580 kN is the approach case F9I quotes: 29 m/s^2 available, far above cruise.
        Check("a strong stage still only cruises at 1.5",
              Math.Abs(BurnExec.BurnAccel(20.0, 580.0) - 1.5) < 1e-9,
              BurnExec.BurnAccel(20.0, 580.0).ToString("F3"));
        // A weak stage cannot reach cruise and must be sized on what it has.
        Check("a weak stage is sized on what it has",
              Math.Abs(BurnExec.BurnAccel(20.0, 10.0) - 0.5) < 1e-9,
              BurnExec.BurnAccel(20.0, 10.0).ToString("F3"));
        Check("and never on nothing", BurnExec.BurnAccel(20.0, 0.0) >= 0.05, "");

        // ---- THE HALF-BURN LEAD ----
        // 10 m/s at 1.5 m/s^2 is 6.67 s of burn, so 3.33 s of it happens before the node.
        double half = BurnExec.HalfBurnS(10.0, 20.0, 580.0);
        Check("half the burn happens before the node", Math.Abs(half - 10.0 / 3.0) < 1e-6,
              half.ToString("F3"));
        // ⛔ AND IT IS NOT SIZED ON FULL THRUST. At 29 m/s^2 the lead would be 0.17 s - the vehicle
        // would light 3 s late for a burn that actually runs at 1.5 m/s^2, and arrive past the node.
        Check("the lead is NOT the full-thrust figure", half > 1.0,
              "would have been " + (10.0 / (2.0 * 29.0)).ToString("F3"));

        // ---- THE THROTTLE: CRUISE, THEN TAPER ----
        // Far out: cruise wins. 1.5 m/s^2 * 20 t / 580 kN = 0.0517.
        BurnState far = Burning(50.0, 50.0, 20.0, 580.0);
        Check("far from the end the throttle is the cruise figure",
              Math.Abs(BurnExec.Throttle(far) - 1.5 * 20.0 / 580.0) < 1e-6,
              BurnExec.Throttle(far).ToString("F4"));
        // Inside the taper: remaining/4 wins. 2 m/s -> 0.5 m/s^2 -> 0.0172.
        BurnState close = Burning(2.0, 50.0, 20.0, 580.0);
        Check("inside the taper the remainder governs",
              Math.Abs(BurnExec.Throttle(close) - (2.0 / 4.0) * 20.0 / 580.0) < 1e-6,
              BurnExec.Throttle(close).ToString("F4"));
        Check("the taper is monotonic - it walks down, never up",
              BurnExec.Throttle(close) < BurnExec.Throttle(far), "");
        // A weak stage must be allowed to ask for everything.
        BurnState weak = Burning(50.0, 50.0, 20.0, 10.0);
        Check("a weak stage goes to full throttle",
              Math.Abs(BurnExec.Throttle(weak) - 1.0) < 1e-9,
              BurnExec.Throttle(weak).ToString("F3"));
        Check("and a burning engine is never commanded to exactly zero",
              BurnExec.Throttle(Burning(0.06, 50.0, 20.0, 580.0)) >= BurnExec.ThrottleMin, "");

        // ---- THE STOP CONDITION IS AN OVERSHOOT TEST ----
        BurnState over = Burning(30.0, 50.0, 20.0, 580.0);
        over.Overshot = true;
        Check("burning past the node ends the burn even with dv left",
              BurnExec.Complete(over), "");
        Check("and the throttle goes with it",
              Math.Abs(BurnExec.Throttle(over)) < 1e-9, "");
        Check("a small residual also ends it",
              BurnExec.Complete(Burning(BurnExec.StopDvMps * 0.5, 50.0, 20.0, 580.0)), "");
        Check("but a large one does not",
              !BurnExec.Complete(Burning(5.0, 50.0, 20.0, 580.0)), "");
        BurnState runaway = Burning(30.0, 50.0, 20.0, 580.0);
        runaway.ElapsedS = BurnExec.MaxBurnDurationS + 1.0;
        Check("and a burn that will not finish is stopped by the backstop",
              BurnExec.Complete(runaway), "");
        Check("which says so rather than looking like success",
              BurnExec.CompletionNote(runaway).Contains("ABORTED"),
              BurnExec.CompletionNote(runaway));

        // ---- ALIGN BEFORE IGNITING, AND BUY RCS ONLY WHEN THE CLOCK SAYS SO ----
        // "Every working burn in this project does this and the one that did not emptied a tank
        // slewing at full throttle."
        Check("three degrees is aligned", BurnExec.Aligned(2.9), "");
        Check("ten is not", !BurnExec.Aligned(10.0), "");
        Check("the turn must finish before the node, not after",
              BurnExec.AlignDeadlineS(60.0) < 60.0, "");
        // RCS is the de-orbit and landing budget - bought only when wheels will not finish in time.
        Check("a long coast turns on reaction wheels",
              !BurnExec.NeedRcsToAlign(300.0, 40.0), "");
        Check("a short coast buys RCS",
              BurnExec.NeedRcsToAlign(20.0, 40.0), "");
        Check("and an aligned vehicle never buys it",
              !BurnExec.NeedRcsToAlign(5.0, 1.0), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
