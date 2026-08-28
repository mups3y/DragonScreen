// Tests for pure/CoastEta.cs — the coast-length estimate that gives warp-to-maneuvers a target UT for a
// range-closing coast (rendezvous co-elliptic chase → CW hand-off). The properties that matter for SAFETY:
// already-there returns 0 (no warp into the event), closing gives a sensible ETA, not-closing is bounded, and
// nothing ever exceeds the horizon (so one warp step can't leap far past the event). Sign convention: + = opening.
using System;
using DragonScreen;

public static class CoastEtaTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen coast-ETA (warp-to-maneuvers) tests");

        double horizon = 5400.0;   // ~one LEO orbital period

        // ---- already at/inside the target → 0 (the event is here; do not warp) ----
        Check("range at target → 0", CoastEta.TimeToRange(50000, -5.0, 50000, horizon) == 0.0, "");
        Check("range inside target → 0", CoastEta.TimeToRange(40000, -5.0, 50000, horizon) == 0.0, "");

        // ---- closing → (range−target)/closing, and always ≤ horizon ----
        // 100 km above target, closing at 50 m/s → (100000−0)/50 = 2000 s (well inside the horizon here).
        double eta = CoastEta.TimeToRange(150000, -50.0, 50000, horizon);
        Check("closing gives the linear ETA", Math.Abs(eta - 2000.0) < 1e-6, "eta=" + eta.ToString("F1"));
        Check("closing ETA is within the horizon", eta <= horizon, "eta=" + eta.ToString("F1"));

        // ---- a slow close that would exceed the horizon is CAPPED (never warp an unbounded span) ----
        double slow = CoastEta.TimeToRange(1.0e7, -1.0, 50000, horizon);   // 10,000 km at 1 m/s = 1e7 s uncapped
        Check("slow close is capped at the horizon", slow == horizon, "slow=" + slow.ToString("F1"));

        // ---- not closing (holding co-elliptic, or opening) → bounded look-ahead = the horizon ----
        Check("holding (≈0 rate) → horizon", CoastEta.TimeToRange(200000, 0.0, 50000, horizon) == horizon, "");
        Check("opening (separating) → horizon", CoastEta.TimeToRange(200000, +30.0, 50000, horizon) == horizon, "");
        Check("closing below the eps → treated as not closing", CoastEta.TimeToRange(200000, -0.05, 50000, horizon) == horizon, "");

        // ---- degenerate horizon → 0 (a warp of no span) ----
        Check("zero horizon → 0", CoastEta.TimeToRange(200000, -50.0, 50000, 0.0) == 0.0, "");

        // ---- INVARIANT: the result is always in [0, horizon] over a dispersed sweep ----
        var rnd = new Random(20260828);
        bool boundOk = true;
        for (int i = 0; i < 20000; i++)
        {
            double r = rnd.NextDouble() * 2.0e7;               // 0..20,000 km
            double rate = (rnd.NextDouble() - 0.5) * 400.0;    // ±200 m/s
            double tgt = rnd.NextDouble() * 1.0e5;             // 0..100 km
            double h = 60.0 + rnd.NextDouble() * 7200.0;       // 1 min .. 2 h
            double e = CoastEta.TimeToRange(r, rate, tgt, h);
            if (e < 0.0 || e > h) { boundOk = false; break; }
        }
        Check("ETA is always within [0, horizon] (20k dispersed cases)", boundOk, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
