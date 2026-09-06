// Tests for [[S79]] — the Vehicle Overview's MARGIN column, and the `Depletion` core under it.
//
// ⛔ WHAT WAS WRONG. `VehicleOverviewPage` drew `R(Dash, 3360, y, 25, Dim)` — the SAME LITERAL on all
// eight rows, on a live feed and a dead one alike, under a header that says MARGIN. The page asked a
// question it never answered, which `SCREEN_LIVENESS_AUDIT.md` H18 files beside S75's painted controls
// and §14.4(f) forbids for a real screen's readout.
//
// ⭐ THE OWNER ANSWERED S79-Q1 ON 2026-09-06 by selecting, from presented options:
//
//     1. Time-to-depletion, one currency for the whole column — hours or days remaining at the
//        current modelled rate.
//
// ⛔ Recorded as a SELECTION, not a verbatim quote (C1.12's evidentiary standard).
//
// ---- WHAT THIS SUITE IS FOR, AND THE SHAPE THE LINE ASKED FOR ---------------------------------
// S79's DONE-when requires the column to be *"pinned by a fixture-A-vs-fixture-B test (the
// `VehicleLiveValues` idiom: a constant cannot pass it)"*. That is the half nothing else can do: the
// R-01 census counts SIZES, `FigmaUINavTest` pins the QTY column, and neither can tell a computed dash
// from a printed one. Every check below is either arithmetic on `Depletion` or a comparison of TWO
// RENDERS of the same page under different state.
//
// ⚠ AND THE DELIBERATE DASHES ARE PINNED AS HARD AS THE NUMBERS. Two of the four sourced rows dash for
// most of a mission by design — at zero burn rate no depletion time exists — so a suite that only
// checked for numbers would call the correct behaviour a regression, and one that only checked for
// dashes would pass the defect this line removed. Both directions are here, on the same rows.
using System;
using DragonScreen;

public static class MarginColumnTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    const int W = 2560, H = 1406;
    const float RefW = 3427f, RefH = 2112f;
    /// <summary>The MARGIN column's design x — right-aligned, from the page's own `R(..., 3360, ...)`.</summary>
    /// ⚠ S192 moved the column from 3360 to 3385 when the owner asked for the CONSUMABLES block to be
    /// re-typed and boxed ("the consumable list on the right hand side text is to small and the whole
    /// list looks plain and out of place"). The COLUMN'S CONTENT did not change and neither did any
    /// check below — only where it is drawn. This literal is the reason the move could not be silent:
    /// the suite went red the moment the page moved, which is what it is for.
    const float MarginX = 3385f;

    public static int Run()
    {
        Console.WriteLine("MarginColumnTest (S79: the MARGIN column reads time-to-depletion, and its dashes are computed)");
        checks = 0; failures = 0;

        Arithmetic();
        TheDashIsComputed();
        ScaleInvariance();
        ThePage();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (a constant cannot pass the fixture-A-vs-B block)");
        return failures > 0 ? 1 : 0;
    }

    // ---------------------------------------------------------------------------------------------
    static void Arithmetic()
    {
        // 3600 units at 1 unit/s is exactly one hour. Chosen so the expected answer is checkable by
        // eye rather than by re-running the formula the code under test uses.
        Check("S79 3600 units at 1/s is 1.0 h",
              Math.Abs(Depletion.Hours(3600.0, 1.0) - 1.0) < 1e-9,
              "got " + Depletion.Hours(3600.0, 1.0));
        Check("S79 ...and halving the rate doubles the time",
              Math.Abs(Depletion.Hours(3600.0, 0.5) - 2.0) < 1e-9,
              "got " + Depletion.Hours(3600.0, 0.5));
        Check("S79 the hours form prints its unit", Depletion.Text(3600.0, 1.0) == "1.0 h",
              Depletion.Text(3600.0, 1.0));

        // The unit switch, checked from BOTH sides of the boundary so an off-by-one in the comparison
        // cannot hide: 48 h exactly stays hours, a hair over becomes days.
        double justUnder = Depletion.DaysAboveHours * Depletion.SecondsPerHour;
        Check("S79 exactly " + Depletion.DaysAboveHours + " h still reads in hours",
              Depletion.Text(justUnder, 1.0).EndsWith(" h"), Depletion.Text(justUnder, 1.0));
        Check("S79 ...and past it, in days",
              Depletion.Text(justUnder * 1.01, 1.0).EndsWith(" d"),
              Depletion.Text(justUnder * 1.01, 1.0));
        Check("S79 the days form prints its unit and a sane number",
              Depletion.Text(96.0 * 3600.0, 1.0) == "4.0 d", Depletion.Text(96.0 * 3600.0, 1.0));
        // ⚠ Both forms carry a unit, so "3.0" can never be read as either. That is the only thing
        // stopping one currency from becoming two units nobody can tell apart (the S38/S39 lesson the
        // owner's own option 3 was rejected on).
        Check("S79 every non-dash reading carries a unit",
              Depletion.Text(3600.0, 1.0).EndsWith(" h") && Depletion.Text(1e7, 1.0).EndsWith(" d"), "");
    }

    // ---------------------------------------------------------------------------------------------
    static void TheDashIsComputed()
    {
        // ⭐ THE CENTRAL CLAIM OF THIS LINE. Each of these produces a dash from the INPUTS, and the
        // check below them produces a number from the same code path — which is what "computed" means
        // and what a printed literal could never do.
        Check("S79 a zero rate has no depletion time", Depletion.Text(1000.0, 0.0) == Dashes.None,
              Depletion.Text(1000.0, 0.0));
        Check("S79 ...and neither does a NEGATIVE rate (a tank being filled)",
              Depletion.Text(1000.0, -2.0) == Dashes.None, Depletion.Text(1000.0, -2.0));
        Check("S79 a negative quantity is 'no source', not a countdown",
              Depletion.Text(-1.0, 1.0) == Dashes.None, Depletion.Text(-1.0, 1.0));
        Check("S79 NaN in, dash out (quantity)", Depletion.Text(double.NaN, 1.0) == Dashes.None, "");
        Check("S79 NaN in, dash out (rate)", Depletion.Text(1000.0, double.NaN) == Dashes.None, "");
        Check("S79 infinity in, dash out", Depletion.Text(double.PositiveInfinity, 1.0) == Dashes.None, "");

        // ⛔ AND THE ONE THAT IS NOT A DASH, which is the reading a crew most needs to be able to tell
        // apart from "no data": an EMPTY tank that something is still drawing on.
        Check("S79 ⭐ ZERO left at a real rate is 0.0 h, NOT a dash",
              Depletion.Text(0.0, 1.0) == "0.0 h", Depletion.Text(0.0, 1.0));

        // ...and the same inputs, one field changed, produce a number. A literal cannot do this.
        Check("S79 ⭐ the SAME call that dashed produces a number when the rate starts",
              Depletion.Text(3600.0, 0.0) == Dashes.None && Depletion.Text(3600.0, 1.0) == "1.0 h", "");
    }

    // ---------------------------------------------------------------------------------------------
    static void ScaleInvariance()
    {
        // ⭐ THE W-PER-EC SCALE CANCELS, and pinning that is worth more than pinning the constant.
        // A bus's stored energy is `ec × k` and its draw is `flow × k × share`, so the depletion time
        // is independent of k. If a later edit makes the two halves use DIFFERENT constants — which is
        // a live risk, since the number exists three times in this tree — this fails.
        const double ec = 500.0, flowEcPerS = 0.25;
        string at120 = Depletion.BusText(ec, 120.0, flowEcPerS * 120.0);
        string at250 = Depletion.BusText(ec, 250.0, flowEcPerS * 250.0);
        Check("S79 ⭐ the bus reading does not depend on the arbitrary W-per-EC scale",
              at120 == at250 && at120 != Dashes.None, at120 + " vs " + at250);

        // A CHARGING bus has no depletion time, and that falls out of the sign rather than a branch.
        Check("S79 a charging bus dashes", Depletion.BusText(ec, 120.0, -30.0) == Dashes.None,
              Depletion.BusText(ec, 120.0, -30.0));
        Check("S79 ...and a draining one does not",
              Depletion.BusText(ec, 120.0, 30.0) != Dashes.None, "");
        Check("S79 a negative charge is 'no source'",
              Depletion.BusText(-1.0, 120.0, 30.0) == Dashes.None, "");
    }

    // ---------------------------------------------------------------------------------------------
    // The fixture-A-vs-fixture-B block the line's DONE-when names.
    static PageState Fixture(double ec, double net1, double net2,
                             double fuelKg, double oxKg, double fuelFlow, double oxFlow)
    {
        PageState s = new PageState();
        s.Valid = true;
        s.PowerUnit1Text = "84 %"; s.PowerUnit2Text = "84 %";
        s.DeorbitFuelText = "791.1 kg"; s.DeorbitOxText = "1308.0 kg";
        s.EcUnits = ec;
        s.Cabin.NetPwr1W = net1; s.Cabin.NetPwr2W = net2;   // NEGATIVE while draining
        s.DeorbitFuelKg = fuelKg; s.DeorbitOxKg = oxKg;
        s.DeorbitFuelFlowKgS = fuelFlow; s.DeorbitOxFlowKgS = oxFlow;
        return s;
    }

    /// <summary>Every string drawn in the MARGIN column, top row first.</summary>
    static string[] MarginCells(PageState s)
    {
        DisplayList dl = new DisplayList(VehicleOverviewPage.Commands + 60);
        VehicleOverviewPage.Build(dl, W, H, s);
        float px = MarginX * (W / RefW);
        var found = new System.Collections.Generic.List<string>();
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Text || Math.Abs(c.A - px) > 0.5f) continue;
            if (c.Str == "MARGIN") continue;                 // the header, not a cell
            found.Add(c.Str);
        }
        return found.ToArray();
    }

    static void ThePage()
    {
        // A: coasting — buses draining, nothing burning.  B: a deorbit burn, and a different load.
        PageState a = Fixture(500.0, -60.0, -40.0, 791.1, 1308.0, 0.0, 0.0);
        PageState b = Fixture(500.0, -30.0, -20.0, 791.1, 1308.0, 0.9, 1.4);
        PageState dead = Fixture(500.0, -60.0, -40.0, 791.1, 1308.0, 0.9, 1.4); dead.Valid = false;

        string[] ca = MarginCells(a), cb = MarginCells(b), cd = MarginCells(dead);

        Check("S79 the column draws one cell per consumable row", ca.Length == 8,
              "got " + ca.Length);
        if (ca.Length != 8 || cb.Length != 8) { Console.WriteLine("  (column shape wrong; rest skipped)"); return; }

        // ---- ⭐ THE TWO POWER ROWS: a real number, and NOT THE SAME ONE under a different load ----
        Check("S79 Power Unit 1 reads a depletion time", ca[0] != Dashes.None, ca[0]);
        Check("S79 Power Unit 2 reads a depletion time", ca[1] != Dashes.None, ca[1]);
        Check("S79 ⭐ Power Unit 1's margin is NOT A CONSTANT", ca[0] != cb[0],
              "fixture A and B both read " + ca[0]);
        Check("S79 ⭐ Power Unit 2's margin is NOT A CONSTANT", ca[1] != cb[1],
              "fixture A and B both read " + ca[1]);
        // Bus 1 carries the larger share, so it runs out SOONER off one shared pool. A column that
        // printed one number for both rows would pass every check above and fail this one.
        Check("S79 the two buses do not read the same time off different loads", ca[0] != ca[1],
              "both " + ca[0]);

        // ---- ⭐ THE TWO DEORBIT ROWS: the accepted consequence, pinned in BOTH directions ----
        Check("S79 ⭐ deorbit FUEL dashes while nothing is burning", ca[2] == Dashes.None, ca[2]);
        Check("S79 ⭐ deorbit OX dashes while nothing is burning", ca[3] == Dashes.None, ca[3]);
        Check("S79 ⭐ ...and reads a countdown DURING a burn — so the dash is computed, not printed",
              cb[2] != Dashes.None && cb[3] != Dashes.None, cb[2] + " / " + cb[3]);
        Check("S79 the two propellants do not read the same time off different flows", cb[2] != cb[3],
              "both " + cb[2]);

        // ---- the four subtank rows: a REASONED dash, under every fixture ----
        // ⚠ These must NEVER read a number: the real vehicle's tank split has no KSP counterpart, so a
        // margin on a quantity that does not exist does not exist either (§14.4(e)). A change that
        // made the column "more live" by filling these would be inventing the number.
        for (int i = 4; i < 8; i++)
        {
            Check("S79 subtank row " + i + " dashes under fixture A", ca[i] == Dashes.None, ca[i]);
            Check("S79 subtank row " + i + " dashes under fixture B too", cb[i] == Dashes.None, cb[i]);
        }

        // ---- a dead feed dashes everything, and the live readings are gone ----
        for (int i = 0; i < 8; i++)
            Check("S79 row " + i + " dashes with no feed", cd[i] == Dashes.None, cd[i]);
        Check("S79 no live margin survives a dead feed",
              Array.IndexOf(cd, ca[0]) < 0 && Array.IndexOf(cd, ca[1]) < 0, "");

        // ---- ⛔ AND THE OLD DEFECT CANNOT COME BACK ----
        // The whole column was one literal. If it ever is again, every cell matches every other under
        // BOTH fixtures — which is exactly what this asks about.
        bool allSame = true;
        for (int i = 1; i < 8 && allSame; i++) if (ca[i] != ca[0]) allSame = false;
        Check("S79 ⛔ the column is not one literal repeated eight times", !allSame,
              "every cell reads " + ca[0]);

        // ---- and a source-less row cannot claim a margin the QTY column says is absent ----
        PageState noProp = Fixture(500.0, -60.0, -40.0, 791.1, 1308.0, 0.9, 1.4);
        noProp.DeorbitFuelText = null; noProp.DeorbitOxText = null;
        string[] cn = MarginCells(noProp);
        Check("S79 a row whose QTY has no source dashes its MARGIN too, even mid-burn",
              cn[2] == Dashes.None && cn[3] == Dashes.None, cn[2] + " / " + cn[3]);
        PageState noPwr = Fixture(500.0, -60.0, -40.0, 791.1, 1308.0, 0.0, 0.0);
        noPwr.PowerUnit1Text = null;
        string[] cp = MarginCells(noPwr);
        Check("S79 ...and the same for a power row", cp[0] == Dashes.None, cp[0]);
    }
}
