// Tests for pure/BoosterDrag.cs (S63). This suite exists for one reason that is NOT ordinary coverage:
// THE TEN NUMBERS IT PINS CANNOT BE RE-DERIVED. They are the median ballistic coefficient per 0.5-Mach bin
// over 18,080 clean unpowered in-atmosphere descent samples across 48 recorded RSS/RO flights, and the raw
// `flight_0825_*.csv` corpus behind them was GITIGNORED AND NEVER COMMITTED (R1 §3.5 / §4.3, §B16.8 ruling 1).
// If a digit is changed — deliberately or by a stray keystroke — the symptom is a LANDING MISS, not a build
// failure. This suite is the tripwire that turns the second into the first.
//
// ---- WHERE THE AUTHORITY FOR THESE VALUES LIVES ----
// `docs/AUTOPILOT_RECOVERY_AUDIT.md` (R1) §3.5, under "The drag curve — plugin/src/pure/BoosterDrag.cs",
// quotes the table VERBATIM from commit `0d6423d`:
//
//       Mach  0.5   1.0   1.5   2.0   2.5   3.0   3.5   4.0   4.5   5.0
//       bc    2582  1485  1796  1075  1331  1321  1481  1580  1582  1439   kg/m2
//
// ⭐ So R1 §3.5 is a SECOND SURVIVING COPY of the ten digits inside this repository, and the values pinned
// below are transcribed from it rather than from the module under test — which is what makes this a guard
// and not a tautology. It is still not evidence: R1 is a quotation of the same lost corpus, not an
// independent measurement, so a value wrong in `0d6423d` is wrong in both. Re-derivation needs the corpus
// rebuilt by RECORDED RE-FLIGHTS (owner decision 2026-09-03 on R1 Q2: RE-FLY) — the BlackBox
// (`docs/BLACKBOX_RESEARCH.md`) plus GLASS TIME, a separate owner gate. No suite can converge this curve.
//
// ⛔ WHAT GREEN HERE DOES NOT MEAN. It does not mean the curve is right, and it does not mean the booster
// lands: R1 §3.5 records that the drag data came from flights that mostly did NOT land, and that no
// after-case was ever recorded for the miss it was written to fix (`flight_0825_184857`, 25 km long → 16 km
// short). It means the table in the tree still reads what R1 says it read, and the interpolator over it
// still behaves as its own header claims. That is the whole claim.
using System;
using DragonScreen;

public static class BoosterDragTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }
    static void Exact(string what, double got, double want)
    { checks++; if (got != want) { failures++; Console.WriteLine("  FAIL  " + what + "   got " + got.ToString("R") + " want " + want.ToString("R")); } }
    static void Near(string what, double got, double want, double tol)
    { checks++; if (Math.Abs(got - want) > tol) { failures++; Console.WriteLine("  FAIL  " + what + "   got " + got.ToString("R") + " want " + want.ToString("R")); } }

    // Transcribed from R1 §3.5's verbatim quotation, NOT read back out of the module under test.
    static readonly double[] R1Mach = { 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0 };
    static readonly double[] R1Bc = { 2582, 1485, 1796, 1075, 1331, 1321, 1481, 1580, 1582, 1439 };

    public static int Run()
    {
        Console.WriteLine("DragonScreen S63 BoosterDrag corpus-curve guard (R1 §3.5 — the last copy)");

        // ---- (a) THE TEN VALUES, EXACTLY. Equality, not a tolerance: every breakpoint falls on a bin edge
        // where the interpolation weight is exactly 1.0 (each gap is 0.5, which is exact in binary), so an
        // exact compare is available and it is the only compare that catches a one-digit edit. ----
        for (int i = 0; i < R1Mach.Length; i++)
            Exact("bin " + R1Mach[i].ToString("F1") + " holds R1 §3.5's median bc",
                  BoosterDrag.BcAtMach(R1Mach[i]), R1Bc[i]);

        // ---- (b) HOLD FLAT OUTSIDE THE CORPUS. Below Mach 0.5 the subsonic value; above Mach 5 the top
        // hypersonic value (the entry-burn regime, where thrust blocked clean measurement). ----
        Exact("Mach 0 holds the subsonic value", BoosterDrag.BcAtMach(0.0), 2582);
        Exact("Mach 0.25 holds the subsonic value", BoosterDrag.BcAtMach(0.25), 2582);
        Exact("a negative Mach holds the subsonic value rather than extrapolating",
              BoosterDrag.BcAtMach(-3.0), 2582);
        Exact("Mach 6 holds the top hypersonic value", BoosterDrag.BcAtMach(6.0), 1439);
        Exact("Mach 25 (entry-burn regime) still holds it — never extrapolated",
              BoosterDrag.BcAtMach(25.0), 1439);

        // ---- (c) LINEAR INTERPOLATION BETWEEN BINS, checked at midpoints where the answer is the mean ----
        Exact("midpoint 1.25 = mean(1485, 1796)", BoosterDrag.BcAtMach(1.25), 1640.5);
        Exact("midpoint 2.25 = mean(1075, 1331)", BoosterDrag.BcAtMach(2.25), 1203.0);
        Exact("midpoint 4.75 = mean(1582, 1439)", BoosterDrag.BcAtMach(4.75), 1510.5);
        Near("quarter-point 1.625 sits a quarter of the way from 1796 to 1075",
             BoosterDrag.BcAtMach(1.625), 1796 + (1075 - 1796) * 0.25, 1e-9);
        Check("interpolation stays strictly inside the two bracketing bins (no overshoot)",
              BoosterDrag.BcAtMach(1.25) > 1485 && BoosterDrag.BcAtMach(1.25) < 1796, "");

        // ---- (d) DragFactor IS 1/bc, and Reynolds is genuinely ignored ----
        for (int i = 0; i < R1Mach.Length; i++)
            Exact("DragFactor at Mach " + R1Mach[i].ToString("F1") + " = 1/bc",
                  BoosterDrag.DragFactor(R1Mach[i], 0.0), 1.0 / R1Bc[i]);
        Exact("DragFactor ignores pseudo-Reynolds (corpus is Mach-binned only)",
              BoosterDrag.DragFactor(2.75, 1.0e9), BoosterDrag.DragFactor(2.75, 0.0));
        Check("DragFactor is positive and small everywhere on the curve (a drag ACCEL scale, not a bc)",
              BoosterDrag.DragFactor(0.0, 0) > 0 && BoosterDrag.DragFactor(0.0, 0) < 1e-3
              && BoosterDrag.DragFactor(30.0, 0) > 0 && BoosterDrag.DragFactor(30.0, 0) < 1e-3, "");
        // ⚠ THE ZERO-GUARD (`bc > 1.0`) IS UNREACHABLE THROUGH THIS API, and saying so is the honest test.
        // The smallest value on the curve is 1075 kg/m2, so no Mach — none, including the held tails —
        // can drive bc to 1.0 or below. The branch is defensive against a future table, not dead-lettered
        // by accident; what CAN be pinned is that nothing on the real curve ever trips it.
        {
            double worst = double.MaxValue;
            for (int i = -1000; i <= 30000; i++) { double b = BoosterDrag.BcAtMach(i / 1000.0); if (b < worst) worst = b; }
            Check("the zero-guard never fires on the real curve (min bc over Mach -1..30 is > 1)",
                  worst > 1.0, "min bc = " + worst.ToString("R"));
            Check("...and that minimum is the transonic 1075, so the guard is defensive, not live",
                  worst == 1075, "min bc = " + worst.ToString("R"));
        }

        // ---- (e) THE SHAPE CLAIM THE HEADER MAKES: the transonic MINIMUM sits at Mach 2.0, and the
        // subsonic end is the maximum. A digit edit that preserved every value but moved the trough would
        // pass (a)-(d) and still mis-predict, so the shape is pinned as its own claim. ----
        {
            double lo = double.MaxValue, hi = double.MinValue, atLo = -1, atHi = -1;
            for (int i = 0; i <= 6000; i++)
            {
                double m = i / 1000.0, b = BoosterDrag.BcAtMach(m);
                if (b < lo) { lo = b; atLo = m; }
                if (b > hi) { hi = b; atHi = m; }
            }
            Exact("the curve's minimum is the transonic 1075 kg/m2", lo, 1075);
            Exact("...and it sits at Mach 2.0, where R1 §3.5 puts the transonic drag rise", atLo, 2.0);
            Exact("the curve's maximum is the subsonic 2582 kg/m2", hi, 2582);
            Check("...and it sits at the subsonic end (Mach <= 0.5, the held tail)", atHi <= 0.5,
                  "at Mach " + atHi.ToString("R"));
            Check("bc FALLS from subsonic into the transonic trough",
                  BoosterDrag.BcAtMach(0.5) > BoosterDrag.BcAtMach(2.0), "");
            Check("bc RECOVERS to the 1400-1580 hypersonic band above the trough",
                  BoosterDrag.BcAtMach(4.0) > 1400 && BoosterDrag.BcAtMach(4.0) < 1600
                  && BoosterDrag.BcAtMach(5.0) > 1400 && BoosterDrag.BcAtMach(5.0) < 1600, "");
            Check("the curve is NOT a scalar — the whole point (subsonic differs from hypersonic by >1000)",
                  BoosterDrag.BcAtMach(0.5) - BoosterDrag.BcAtMach(2.0) > 1000, "");
        }

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }
}
