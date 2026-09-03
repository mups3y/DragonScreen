// Tests for pure/SafeLandingSite.cs (W15, Wave E-3). The decisive check: the abort's splashdown target is
// the NEAREST WATER sample that falls INSIDE the reachable entry-glide window — not the nearest water, and
// not the nearest sample. A water site too close to reach is as unusable as land, and saying so is the whole
// job of `PickDeorbitTarget`; `PickNearestWater` is the looser fallback for a degenerate track.
//
// ⚠ W15, 2026-09-04. THE THREE CHECKS BELOW MARKED (recovered) ARE RECOVERED VERBATIM, fixture and all,
// from `plugin/test/FdirTest.cs:152-164` at `8b81816^` — that is where the deleted tree tested this module,
// and R1 §5.3 lists no `SafeLandingSiteTest.cs` because none existed. FdirTest itself cannot come back: it
// is mostly `AbortResponder` / `Fdir` coverage and neither type is in the tree (W19 owns `AbortResponder`).
// So the SafeLandingSite block was lifted into its own suite, its assertions unaltered, and the rest of
// FdirTest stays deleted. The checks NOT marked (recovered) are new here — the null guards, the boundary of
// the window and the tie/order rule — added because a fresh suite that only ever tests the interior of the
// band would not catch an inclusive/exclusive slip at its edge.
// ⛔ THIS SUITE PROVES SELECTION ARITHMETIC ONLY. `SafeLandingSite` never flew (R1 §5.1: regime RSS,
// flown ❌ NO) and the glide window it selects against is [UN-CONVERGED] for RSS-RO (§B16.8 ruling 2) —
// the marking lives on `src/LandingSiteScan.cs`, which supplies the band. The fixtures here are ANALYTIC
// (hand-placed samples with a known right answer), not flight data. Green means the selector picks the
// sample it should. It says nothing about whether the window is the right window.
using System;
using DragonScreen;

public static class SafeLandingSiteTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }

    static GroundSample S(double downrangeM, bool water)
    { GroundSample g = new GroundSample(); g.DownrangeM = downrangeM; g.Water = water; return g; }

    public static int Run()
    {
        Console.WriteLine("DragonScreen W15 SafeLandingSite (splashdown site selection) tests");

        // ---- the recovered fixture: `8b81816^` test/FdirTest.cs:153-158, unaltered ----
        GroundSample[] gs = new GroundSample[] {
            S(0.5e6, true),    // too near (before the window)
            S(2.0e6, false),   // land
            S(3.0e6, true),    // ← nearest water in window
            S(5.0e6, true),
        };

        Check("(recovered) safe site = nearest water inside the glide window (not the too-near one)",
              SafeLandingSite.PickDeorbitTarget(gs, 1.0e6, 9.0e6) == 2, "");
        Check("(recovered) no water in window → -1 (glue coasts a step)",
              SafeLandingSite.PickDeorbitTarget(new GroundSample[] { S(2e6, false) }, 1e6, 9e6) == -1, "");
        Check("(recovered) fallback = nearest water beyond the min lead",
              SafeLandingSite.PickNearestWater(gs, 1.0e6) == 2, "");

        // ---- the window has an UPPER bound too, and that is what separates the two selectors ----
        Check("water only BEYOND the window → -1 (unreachable is not a target)",
              SafeLandingSite.PickDeorbitTarget(gs, 1.0e6, 2.5e6) == -1, "");
        Check("...and the fallback takes that same too-far site rather than nothing",
              SafeLandingSite.PickNearestWater(gs, 1.0e6) == 2, "");
        Check("fallback still honours its min lead (0.5e6 water is skipped)",
              SafeLandingSite.PickNearestWater(gs, 1.0e6) != 0, "");
        Check("fallback with no lead takes the very nearest water",
              SafeLandingSite.PickNearestWater(gs, 0.0) == 0, "");

        // ---- band edges are INCLUSIVE at both ends (the code rejects on `< min` / `> max`) ----
        GroundSample[] edge = new GroundSample[] { S(1.0e6, true), S(9.0e6, true) };
        Check("a site exactly ON the near edge is in-window",
              SafeLandingSite.PickDeorbitTarget(edge, 1.0e6, 9.0e6) == 0, "");
        Check("a site exactly ON the far edge is in-window",
              SafeLandingSite.PickDeorbitTarget(new GroundSample[] { S(9.0e6, true) }, 1.0e6, 9.0e6) == 0, "");
        Check("a site just inside the near edge is rejected",
              SafeLandingSite.PickDeorbitTarget(new GroundSample[] { S(0.999e6, true) }, 1.0e6, 9.0e6) == -1, "");

        // ---- NEAREST, not first: the array order must not decide the answer ----
        GroundSample[] unordered = new GroundSample[] { S(7.0e6, true), S(3.0e6, true), S(5.0e6, true) };
        Check("nearest wins regardless of array order",
              SafeLandingSite.PickDeorbitTarget(unordered, 1.0e6, 9.0e6) == 1, "");
        Check("nearest wins regardless of array order (fallback too)",
              SafeLandingSite.PickNearestWater(unordered, 1.0e6) == 1, "");
        Check("an exact tie keeps the FIRST of the tied samples (deterministic, no coin-flip)",
              SafeLandingSite.PickDeorbitTarget(new GroundSample[] { S(4e6, true), S(4e6, true) },
                                                1.0e6, 9.0e6) == 0, "");

        // ---- degenerate inputs refuse rather than throw: the glue coasts a step on -1 ----
        Check("null samples → -1, not an exception",
              SafeLandingSite.PickDeorbitTarget(null, 1.0e6, 9.0e6) == -1, "");
        Check("null samples → -1 for the fallback too",
              SafeLandingSite.PickNearestWater(null, 1.0e6) == -1, "");
        Check("empty samples → -1",
              SafeLandingSite.PickDeorbitTarget(new GroundSample[0], 1.0e6, 9.0e6) == -1, "");
        Check("an all-land track → -1 from BOTH selectors (never target a mountainside)",
              SafeLandingSite.PickDeorbitTarget(new GroundSample[] { S(2e6, false), S(4e6, false) }, 1e6, 9e6) == -1
              && SafeLandingSite.PickNearestWater(new GroundSample[] { S(2e6, false), S(4e6, false) }, 1e6) == -1, "");
        Check("an inverted window (min > max) selects nothing rather than anything",
              SafeLandingSite.PickDeorbitTarget(gs, 9.0e6, 1.0e6) == -1, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }
}
