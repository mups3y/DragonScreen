/*
 * DragonScreen headless tests - node execution (GNC tranche 3).
 *
 * Every check here corresponds to a specific way a burn goes wrong, and F9I's source names most of
 * them. This is a throttle law that was tuned until a residual landed on 0.085 m/s; the tests exist
 * so a later "simplification" cannot quietly undo that.
 */
using System;
using DragonScreen;

public static class BurnExecTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    static void Near(string what, double got, double want, double tol)
    {
        Check(what, Math.Abs(got - want) <= tol,
              "got " + got.ToString("G8") + " want " + want.ToString("G8"));
    }

    static BurnState B(double dv, double accel, double angle)
    {
        BurnState s = new BurnState();
        s.Valid = true; s.RemainingDvMps = dv; s.MaxAccel = accel; s.PointingErrorDeg = angle;
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen burn executor tests");
        Alignment();
        Throttling();
        Timing();
        Finishing();
        Pointing();
        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    static void Alignment()
    {
        Near("aligned is full authority", BurnExec.AlignmentMultiplier(0.0), 1.0, 1e-9);
        Near("half the limit is half authority", BurnExec.AlignmentMultiplier(2.5), 0.5, 1e-9);
        Near("at the limit it is zero", BurnExec.AlignmentMultiplier(5.0), 0.0, 1e-9);

        // ---- BEYOND THE LIMIT IT MUST CLAMP, NOT GO NEGATIVE ----
        // GNC.ks lets it go negative and relies on the throttle clamp downstream. Letting a negative
        // escape this function invites someone to multiply by it later and get thrust backwards.
        for (double a = 5.0; a <= 180.0; a += 5.0)
            Check("never negative at " + a + " degrees", BurnExec.AlignmentMultiplier(a) >= 0.0,
                  BurnExec.AlignmentMultiplier(a).ToString("F4"));

        // Monotonic - more error is never more authority.
        double prev = 2.0;
        for (double a = 0.0; a <= 20.0; a += 0.5)
        {
            double m = BurnExec.AlignmentMultiplier(a);
            Check("monotonic at " + a, m <= prev + 1e-12, m + " after " + prev);
            prev = m;
        }
    }

    static void Throttling()
    {
        // ---- THROTTLE IS SECONDS OF BURN LEFT, NOT dv ----
        // A long burn is full throttle; the taper only starts inside the last second.
        Near("a long burn is full throttle", BurnExec.Throttle(B(500.0, 10.0, 0.0)), 1.0, 1e-9);
        Near("ten seconds left is still full", BurnExec.Throttle(B(100.0, 10.0, 0.0)), 1.0, 1e-9);

        // Same dv, different vehicle: the WEAKER one is still at full throttle when the strong one
        // has begun tapering. That is the whole point of timing it rather than dv-ing it.
        double weak = BurnExec.Throttle(B(5.0, 1.0, 0.0));     // 5 s of burn
        double strong = BurnExec.Throttle(B(5.0, 50.0, 0.0));  // 0.1 s of burn
        Check("weak vehicle still full, strong one tapering", weak > strong,
              weak.ToString("F3") + " vs " + strong.ToString("F3"));

        // ---- THE FLOOR-LIFT ----
        // A small demand is DOUBLED so the engine stays above its minimum useful throttle instead of
        // dribbling. 0.2 s of burn left -> 0.2 raw -> 0.4 commanded.
        Near("a small demand is lifted", BurnExec.Throttle(B(0.2, 1.0, 0.0)), 0.4, 1e-9);
        Check("but a large one is not", BurnExec.Throttle(B(0.8, 1.0, 0.0)) <= 0.8 + 1e-9,
              BurnExec.Throttle(B(0.8, 1.0, 0.0)).ToString("F3"));

        // ---- MISPOINTED MEANS OFF ----
        // "Thrusting 10 degrees off is worse than not thrusting."
        Near("ten degrees off is no throttle", BurnExec.Throttle(B(500.0, 10.0, 10.0)), 0.0, 1e-9);
        Near("at the limit is no throttle", BurnExec.Throttle(B(500.0, 10.0, 5.0)), 0.0, 1e-9);
        Check("slightly off still burns, but less",
              BurnExec.Throttle(B(500.0, 10.0, 1.0)) > 0.0
              && BurnExec.Throttle(B(500.0, 10.0, 1.0)) < 1.0,
              BurnExec.Throttle(B(500.0, 10.0, 1.0)).ToString("F3"));
        // ---- ⚠ MONOTONIC IN POINTING ERROR. THIS CHECK FOUND A REAL DEFECT IN F9I'S ORDERING. ----
        // GNC.ks applies the floor-lift AFTER the alignment term, so 3 deg off got doubled to 0.80
        // while 2 deg off stayed at 0.60 - a throttle KICK as the pointing degrades. Lifting the
        // time-based demand first keeps the intent ("small demands ... rather than dribbling") and
        // removes the artifact.
        double worse = 2.0;
        for (double a = 0.0; a <= 6.0; a += 0.25)
        {
            double t = BurnExec.Throttle(B(500.0, 10.0, a));
            Check("throttle never rises as pointing worsens at " + a, t <= worse + 1e-12,
                  t.ToString("F4") + " after " + worse.ToString("F4"));
            worse = t;
        }
        Check("and it strictly falls across the band",
              BurnExec.Throttle(B(500.0, 10.0, 1.0)) > BurnExec.Throttle(B(500.0, 10.0, 3.0)),
              BurnExec.Throttle(B(500.0, 10.0, 1.0)).ToString("F3") + " vs "
              + BurnExec.Throttle(B(500.0, 10.0, 3.0)).ToString("F3"));

        // Never out of range, whatever it is fed.
        foreach (double dv in new[] { 0.0, 0.05, 1.0, 1e6 })
            foreach (double ac in new[] { 0.0, 0.001, 1.0, 1e4 })
                foreach (double an in new[] { 0.0, 3.0, 90.0, 180.0 })
                {
                    double t = BurnExec.Throttle(B(dv, ac, an));
                    Check("throttle in range for " + dv + "/" + ac + "/" + an,
                          t >= 0.0 && t <= 1.0 && !double.IsNaN(t), t.ToString());
                }

        // An invalid state must never command thrust.
        Check("invalid commands nothing", BurnExec.Throttle(new BurnState()) == 0.0, "");
        // Residual inside tolerance is done, not a dribble.
        Near("residual under tolerance is off", BurnExec.Throttle(B(0.05, 10.0, 0.0)), 0.0, 1e-9);
    }

    static void Timing()
    {
        // Duration, and the cap that stops a dead engine planning a lead longer than the orbit.
        Near("duration is dv over accel", BurnExec.BurnDuration(100.0, 10.0), 10.0, 1e-9);
        Check("duration is capped", BurnExec.BurnDuration(1e9, 0.0001) <= BurnExec.MaxBurnDurationS,
              BurnExec.BurnDuration(1e9, 0.0001).ToString());
        Check("zero thrust does not divide by zero",
              !double.IsInfinity(BurnExec.BurnDuration(100.0, 0.0)), "");

        // ---- THE BURN IS CENTRED ON THE NODE ----
        // Ignition is half a burn EARLY. This is what makes a finite burn approximate the impulse.
        Near("ignition leads the node by half the burn",
             BurnExec.IgnitionEtaS(100.0, 40.0), 80.0, 1e-9);
        Check("a late node gives a negative lead", BurnExec.IgnitionEtaS(5.0, 40.0) < 0.0, "");

        // ---- THE `>` vs `-` BUG, WRITTEN AS A REAL COMPARISON ----
        // kOS read the arithmetic expression as a boolean, so the coast branch was ALWAYS taken.
        Check("plenty of time means coast", BurnExec.HasCoastTime(1000.0, 40.0), "");
        Check("a node that is due does NOT coast", !BurnExec.HasCoastTime(30.0, 40.0), "");
        // Exactly on the boundary must not coast - that was the failing case.
        Check("exactly at the boundary does not coast",
              !BurnExec.HasCoastTime(140.0, 40.0), "");

        // ---- SETTLED TIME, NOT ITERATIONS ----
        // A vessel drifting in and out of tolerance must never accumulate.
        double settled = 0.0;
        for (int i = 0; i < 20; i++) settled = BurnExec.UpdateSettled(settled, 0.5, 0.1);
        Near("continuous alignment accumulates", settled, 2.0, 1e-9);
        settled = BurnExec.UpdateSettled(settled, 3.0, 0.1);
        Near("one bad tick resets it to zero", settled, 0.0, 1e-9);
    }

    static void Finishing()
    {
        Check("a big residual is not complete", !BurnExec.Complete(B(50.0, 10.0, 0.0), false), "");
        Check("a small residual is complete", BurnExec.Complete(B(0.05, 10.0, 0.0), false), "");

        // ---- THE REVERSAL TEST. WITHOUT IT AN OVERSHOOT CHASES ITSELF. ----
        // If the burn overshoots between ticks the remaining dv points the OTHER WAY, and thrusting
        // on undoes the burn. This is the same guard the circularisation needed.
        Check("a reversed dv ends the burn", BurnExec.Complete(B(50.0, 10.0, 0.0), true), "");
        Check("an invalid state is complete, not stuck",
              BurnExec.Complete(new BurnState(), false), "");
    }

    static void Pointing()
    {
        // ---- ⚠ THE ONE THAT APPLIES A BURN BACKWARDS ----
        // A Dragon on Dracos points at MINUS the burn vector. GNC.ks:1051.
        BurnState capsuleRcs = B(10.0, 1.0, 0.0);
        capsuleRcs.CapsuleThrusters = true; capsuleRcs.OnRcs = true;
        Check("capsule on RCS points BACKWARDS",
              BurnExec.Sense(capsuleRcs) == ThrustSense.Reversed, "");

        // ...but the same capsule on its MAIN engine does not.
        BurnState capsuleMain = capsuleRcs; capsuleMain.OnRcs = false;
        Check("capsule on the main engine points forwards",
              BurnExec.Sense(capsuleMain) == ThrustSense.Forward, "");

        // ...and a fairing stack on RCS does not either.
        BurnState fairingRcs = B(10.0, 1.0, 0.0); fairingRcs.OnRcs = true;
        Check("a non-capsule on RCS points forwards",
              BurnExec.Sense(fairingRcs) == ThrustSense.Forward, "");
        Check("the plain case is forwards",
              BurnExec.Sense(B(10.0, 1.0, 0.0)) == ThrustSense.Forward, "");
    }
}
