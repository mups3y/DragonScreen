/*
 * DragonScreen headless tests — the Vehicle strip is LIVE (S193)
 *
 * 🟢 OWNER, 2026-09-07, verbatim, on the S192b preview:
 *
 *     "that will do for now build it so it works as intended, icons lead to the correct pages. Icons in
 *      the bar you just moved are white when nominal, turn orange then red for issues that need the user
 *      attention like low fuel or power etc. All gauges read accurately etc. Make it live"
 *
 * Four claims, four blocks. Each is written so that it could FAIL — which is the only reason to write it.
 *
 * ---- 1. "ICONS LEAD TO THE CORRECT PAGES" — A ROUND TRIP, NOT A LOOKUP -------------------------
 * ⛔ The obvious test is to compare `VehicleTabBar.Tabs` against `FigmaUI`'s mapping array, and it is
 * worth almost nothing: both are lists of the same thing written twice, so a transposition in one is
 * only caught if the test transposes it back by hand. Instead this presses tab i's ICON, follows the
 * NavHit to whatever page it names, DRAWS that page, and asserts the page's OWN tab strip underlines
 * tab i. The claim "the icon leads to the right page" is then tested end to end through two independent
 * pieces of code — the mapping and each page's own idea of which tab it is — and a swap in either one
 * breaks the loop.
 *
 * ---- 2. "WHITE WHEN NOMINAL, TURN ORANGE THEN RED" ---------------------------------------------
 * Asserted on the ICON commands specifically, not on the labels: S193 split the two, because an icon
 * carries its subsystem's HEALTH and a label carries SELECTION, and before that split seven of eight
 * icons were drawn in the dim "no source" tint while reporting nominal.
 *
 * ---- 3. "LIKE LOW FUEL OR POWER" ---------------------------------------------------------------
 * Driven from `PageState` through the REAL signals — `DragonProp01` and `Power01` — at the thresholds
 * `Alarms.Low` already publishes. The check that matters is not that a low tank reddens Prop, but that
 * it reddens Prop AND `All` (the roll-up) AND NOTHING ELSE: a severity that leaks across tabs is worse
 * than one that never fires, because it tells the crew to look in the wrong place.
 *
 * ---- 4. "ALL GAUGES READ ACCURATELY" -----------------------------------------------------------
 * The page takes each ring's fill and its printed number from one readout, so they agree BY
 * CONSTRUCTION — and a test that only re-checked that construction would pass just as happily on a page
 * wired to a constant. So this is fixture-A-vs-fixture-B (the `MarginColumnTest` idiom): two renders of
 * the same page under different cabin state, asserting every ring MOVED and moved in the direction and
 * by the proportion the model says. A constant cannot pass it.
 */
using System;
using DragonScreen;

public static class VehicleLiveTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, "got " + got + ", want " + want); }

    const int W = 2560, H = 1406;
    const float RefW = 3427f, RefH = 2112f;

    /// <summary>The icon row's design y, and the strip's design pitch/start. LITERALS: pressing the
    /// LABEL would not prove the owner's ask, which is about the icons he asked for.</summary>
    const float IconY = 1844f, Pitch = 205f, Start = 1713.5f - Pitch * 3.5f;

    static float IconCx(int i) { return Start + i * Pitch; }
    static float PxOf(float designX) { return designX * W / RefW; }
    static float PyOf(float designY) { return designY * H / RefH; }

    /// <summary>A live vessel with everything comfortable — the nominal baseline every block starts
    /// from. Cabin numbers are the fixture the preview already renders.</summary>
    static PageState Nominal()
    {
        PageState s = new PageState();
        s.Valid = true;
        s.Power01 = 0.85; s.DragonProp01 = 0.80; s.GForce01 = 0.0;
        s.Cabin.Ppo2Psia = 2.86;  s.Cabin.Ppo201 = 2.86 / 5.0;
        s.Cabin.CabinTempC = 21.8; s.Cabin.CabinTemp01 = 21.8 / 40.0;
        s.Cabin.PressPsia = 14.72; s.Cabin.Press01 = 14.72 / 20.0;
        s.Cabin.Co2MmHg = 1.64;   s.Cabin.Co201 = 1.64 / 8.0;
        s.Cabin.LoopAC = 26.4;    s.Cabin.LoopA01 = 26.4 / 80.0;
        s.Cabin.LoopBC = 20.1;    s.Cabin.LoopB01 = 20.1 / 80.0;
        s.Cabin.NetPwr1W = -59.0; s.Cabin.NetPwr2W = -49.0;
        s.Ppo2Text = "2.86"; s.CabinTempText = "21.8"; s.PressText = "14.72"; s.Co2Text = "1.64";
        s.LoopAText = "26.4"; s.LoopBText = "20.1";
        s.NetPwr1Text = "-59"; s.NetPwr2Text = "-49";
        return s;
    }

    static DisplayList Overview(PageState s)
    {
        DisplayList dl = new DisplayList(VehicleOverviewPage.Commands + BottomBar.Commands + 64);
        VehicleOverviewPage.Build(dl, W, H, s);
        return dl;
    }

    /// <summary>The colour of tab i's ICON — the Image command sitting on the icon row at its centre.
    /// Found by POSITION, not by asset key, so a later change of art cannot quietly skip the check.</summary>
    static bool IconColour(DisplayList dl, int i, out Rgba c)
    {
        float cx = PxOf(IconCx(i)), cy = PyOf(IconY);
        for (int k = 0; k < dl.Count; k++)
        {
            DrawCmd d = dl.At(k);
            if (d.Kind != DrawKind.Image) continue;
            float mx = d.A + d.C * 0.5f, my = d.B + d.D * 0.5f;
            if (Math.Abs(mx - cx) < 6f && Math.Abs(my - cy) < 6f) { c = d.Colour; return true; }
        }
        c = new Rgba(); return false;
    }

    static bool Same(Rgba a, Rgba b)
    { return Math.Abs(a.R - b.R) < 0.004f && Math.Abs(a.G - b.G) < 0.004f && Math.Abs(a.B - b.B) < 0.004f; }

    static string Name(Rgba c)
    {
        if (Same(c, DragonPalette.White)) return "White";
        if (Same(c, DragonPalette.Caution)) return "Caution/orange";
        if (Same(c, DragonPalette.Alarm)) return "Alarm/red";
        if (Same(c, DragonPalette.Text6)) return "Dim";
        return "(" + c.R + "," + c.G + "," + c.B + ")";
    }

    public static int Run()
    {
        Console.WriteLine("VehicleLiveTest (S193: the icons navigate, the icons warn, and the gauges move)");
        checks = 0; failures = 0;

        Navigation();
        IconColours();
        LowFuelAndPower();
        GaugesMove();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (a constant cannot pass the gauge block)");
        return failures > 0 ? 1 : 0;
    }

    // ---------------------------------------------------------------------------------------------
    // 1. Pressing an icon lands on the page that underlines that same icon.
    static void Navigation()
    {
        UiPage[] from = {
            UiPage.Vehicle, UiPage.VehicleMech, UiPage.VehicleCrew, UiPage.VehiclePropulsion,
            UiPage.VehiclePower, UiPage.VehicleAvionics, UiPage.VehicleGnc, UiPage.VehicleThermal };

        for (int i = 0; i < VehicleTabBar.Tabs.Length; i++)
        {
            NavHit hit = FigmaUI.HitTest(UiPage.Vehicle, PxOf(IconCx(i)), PyOf(IconY), W, H);
            Check("tab " + i + " (" + VehicleTabBar.Tabs[i] + "): pressing the ICON navigates",
                  hit.Act == NavAct.Goto, "got " + hit.Act);
            if (hit.Act != NavAct.Goto) continue;

            // ⭐ THE ROUND TRIP. Draw the page we landed on and ask ITS strip which tab is underlined.
            DisplayList dl = new DisplayList(4096);
            PageState s = Nominal();
            FigmaUI.Build(dl, hit.Target, W, H, s, MapProjection.Default());
            int lit = UnderlinedTab(dl);
            Check("tab " + i + " (" + VehicleTabBar.Tabs[i] + ") -> " + hit.Target +
                  ", and that page underlines tab " + i, lit == i, "it underlines " + lit);
        }

        // Every source page must route identically — the strip is shared, so a page that forgot to
        // route it would strand the crew on that page.
        foreach (UiPage p in from)
        {
            NavHit h = FigmaUI.HitTest(p, PxOf(IconCx(6)), PyOf(IconY), W, H);
            Check("the GNC icon routes from " + p, h.Act == NavAct.Goto && h.Target == UiPage.VehicleGnc,
                  "got " + h.Act + " " + h.Target);
        }

        // The two deep-view buttons, from the same page, at their own row.
        NavHit b0 = FigmaUI.HitTest(UiPage.Vehicle, PxOf(314f), PyOf(1890f), W, H);
        NavHit b1 = FigmaUI.HitTest(UiPage.Vehicle, PxOf(694f), PyOf(1890f), W, H);
        Check("the SYSTEMS button reaches the Systems Tree",
              b0.Act == NavAct.Goto && b0.Target == UiPage.SystemsTree, "got " + b0.Act + " " + b0.Target);
        Check("the SYS P&ID button reaches the Systems P&ID",
              b1.Act == NavAct.Goto && b1.Target == UiPage.SystemsPid, "got " + b1.Act + " " + b1.Target);
    }

    /// <summary>Which tab a drawn page underlines — the accent bar `Draw` puts under the active tab.
    /// Read back from the display list, so it is the page's own answer and not a parameter echoed.</summary>
    static int UnderlinedTab(DisplayList dl)
    {
        float markY = PyOf(1954f), markW = 140f * W / RefW;
        for (int k = 0; k < dl.Count; k++)
        {
            DrawCmd c = dl.At(k);
            if (c.Kind != DrawKind.Rect) continue;
            if (Math.Abs(c.B - markY) > 4f || Math.Abs(c.C - markW) > 4f) continue;
            float cx = c.A + c.C * 0.5f;
            for (int i = 0; i < VehicleTabBar.Tabs.Length; i++)
                if (Math.Abs(cx - PxOf(IconCx(i))) < 6f) return i;
        }
        return -1;
    }

    // ---------------------------------------------------------------------------------------------
    // 2. White at nominal — every icon, not just the selected one.
    static void IconColours()
    {
        DisplayList dl = Overview(Nominal());
        for (int i = 0; i < VehicleTabBar.Tabs.Length; i++)
        {
            Rgba c;
            Check(VehicleTabBar.Tabs[i] + " icon is drawn", IconColour(dl, i, out c), "no image at its centre");
            if (!IconColour(dl, i, out c)) continue;
            Check(VehicleTabBar.Tabs[i] + " icon is WHITE at nominal", Same(c, DragonPalette.White),
                  "drawn " + Name(c));
        }

        // ⛔ AND THE UNSELECTED ONES ARE THE POINT. `All` is the active tab on this page; before S193
        // the other seven were drawn in the dim "no live source" tint while reporting nominal.
        Rgba thermal; IconColour(dl, 7, out thermal);
        Check("an UNSELECTED nominal icon is white too (the S193 defect)",
              Same(thermal, DragonPalette.White) && !Same(thermal, DragonPalette.Text6), Name(thermal));

        // ⚠ A DEAD FEED IS NOT NOMINAL. With no severity array at all the strip must not claim health.
        DisplayList none = new DisplayList(VehicleTabBar.Commands + 8);
        VehicleTabBar.Draw(none, W, H, 0);
        Rgba unknown;
        Check("with NO severity data the icon stays dim, not white",
              IconColour(none, 5, out unknown) && Same(unknown, DragonPalette.Text6), Name(unknown));
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Low fuel and low power, at the thresholds the model publishes.
    static void LowFuelAndPower()
    {
        // `Alarms.Low`: Caution at <= 0.25, Alarm at <= 0.10. Typed as literals here rather than read
        // from Alarms, so moving the threshold has to come past this test.
        const int Prop = 2, Power = 4, All = 0;

        PageState lowFuel = Nominal(); lowFuel.DragonProp01 = 0.20;
        Rgba c;
        DisplayList a = Overview(lowFuel);
        IconColour(a, Prop, out c);
        Check("fuel at 20 % turns the Prop icon ORANGE", Same(c, DragonPalette.Caution), Name(c));
        IconColour(a, All, out c);
        Check("...and the All roll-up with it", Same(c, DragonPalette.Caution), Name(c));
        IconColour(a, Power, out c);
        Check("...and it does NOT leak onto Power", Same(c, DragonPalette.White), Name(c));
        IconColour(a, 7, out c);
        Check("...nor onto Thermal", Same(c, DragonPalette.White), Name(c));

        PageState deadFuel = Nominal(); deadFuel.DragonProp01 = 0.05;
        IconColour(Overview(deadFuel), Prop, out c);
        Check("fuel at 5 % turns the Prop icon RED", Same(c, DragonPalette.Alarm), Name(c));

        PageState lowPwr = Nominal(); lowPwr.Power01 = 0.22;
        DisplayList b = Overview(lowPwr);
        IconColour(b, Power, out c);
        Check("power at 22 % turns the Power icon ORANGE", Same(c, DragonPalette.Caution), Name(c));
        IconColour(b, Prop, out c);
        Check("...and it does NOT leak onto Prop", Same(c, DragonPalette.White), Name(c));

        PageState deadPwr = Nominal(); deadPwr.Power01 = 0.04;
        IconColour(Overview(deadPwr), Power, out c);
        Check("power at 4 % turns the Power icon RED", Same(c, DragonPalette.Alarm), Name(c));

        // ⭐ THE ORDER MATTERS AND IS ASSERTED: orange must come BEFORE red as the reading falls, which
        // is the owner's own phrasing ("turn orange then red"). A single-threshold model would pass the
        // two checks above and fail this one.
        PageState justOver = Nominal(); justOver.Power01 = 0.30;
        IconColour(Overview(justOver), Power, out c);
        Check("power at 30 % is still white — the caution band starts BELOW it", Same(c, DragonPalette.White),
              Name(c));
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Every ring moves with its number, and by the right proportion.
    static void GaugesMove()
    {
        PageState a = Nominal();
        PageState b = Nominal();
        b.Cabin.Ppo2Psia = 1.90; b.Cabin.Ppo201 = 1.90 / 5.0; b.Ppo2Text = "1.90";
        b.Cabin.CabinTempC = 33.0; b.Cabin.CabinTemp01 = 33.0 / 40.0; b.CabinTempText = "33.0";
        b.Cabin.PressPsia = 11.5; b.Cabin.Press01 = 11.5 / 20.0; b.PressText = "11.50";
        b.Cabin.Co2MmHg = 5.20; b.Cabin.Co201 = 5.20 / 8.0; b.Co2Text = "5.20";
        b.Cabin.LoopAC = 48.0; b.Cabin.LoopA01 = 48.0 / 80.0; b.LoopAText = "48.0";

        DisplayList da = Overview(a), db = Overview(b);

        // The fill arc of each gauge, found at the gauge's own centre. `Gauge` draws the dim track
        // first and the coloured fill second, so the fill is the LAST arc at that centre.
        Sweep(da, db, "PPO2",           1038f, 430f, 2.86 / 5.0, 1.90 / 5.0);
        Sweep(da, db, "CABIN TEMP",     1488f, 430f, 21.8 / 40.0, 33.0 / 40.0);
        Sweep(da, db, "CABIN PRESSURE", 1938f, 430f, 14.72 / 20.0, 11.5 / 20.0);
        Sweep(da, db, "CO2",            2388f, 430f, 1.64 / 8.0, 5.20 / 8.0);
        Sweep(da, db, "LOOP A",         1713.5f - 778f, 1000f, 26.4 / 80.0, 48.0 / 80.0);

        // ⭐ AND THE NUMBER MOVED WITH IT. A ring that swept while its label stayed put would mean the
        // two are not reading the same source, which is the exact defect T13a's idiom exists to stop.
        Check("the PPO2 number moved with its ring", Drew(db, "1.90") && !Drew(db, "2.86"), "");
        Check("the CO2 number moved with its ring", Drew(db, "5.20") && !Drew(db, "1.64"), "");

        // A dead feed empties every ring rather than freezing the last reading.
        PageState dead = Nominal(); dead.Valid = false;
        DisplayList dd = Overview(dead);
        Check("a dead feed leaves the PPO2 ring EMPTY, not frozen",
              FillSweep(dd, 1038f, 430f) <= 0.01f, "sweep " + FillSweep(dd, 1038f, 430f));
        Check("...and dashes its number", Drew(dd, "—") && !Drew(dd, "2.86"), "");
    }

    /// <summary>The coloured fill's sweep, in degrees, for the gauge centred at (dx, dy) design.
    /// `Gauge`'s track runs -150..150 and its fill -150..-150+300*frac, so the fill is the arc at that
    /// centre with the SMALLER end angle — or absent entirely at zero.</summary>
    static float FillSweep(DisplayList dl, float dx, float dy)
    {
        float cx = PxOf(dx), cy = PyOf(dy);
        float best = -1f;
        for (int k = 0; k < dl.Count; k++)
        {
            DrawCmd c = dl.At(k);
            if (c.Kind != DrawKind.ArcBand) continue;
            if (Math.Abs(c.A - cx) > 4f || Math.Abs(c.B - cy) > 4f) continue;
            float sweep = c.EndDeg - c.StartDeg;
            if (sweep < 299.5f) { if (sweep > best) best = sweep; }   // the track is the full 300
        }
        return best < 0f ? 0f : best;
    }

    static void Sweep(DisplayList da, DisplayList db, string what,
                      float dx, float dy, double fracA, double fracB)
    {
        float sa = FillSweep(da, dx, dy), sb = FillSweep(db, dx, dy);
        Near(what + ": fixture A's ring is 300 deg x its own fraction", sa, 300.0 * fracA, 1.5);
        Near(what + ": fixture B's ring is 300 deg x its own fraction", sb, 300.0 * fracB, 1.5);
        Check(what + ": ⭐ the ring MOVED between the two fixtures — a constant cannot do this",
              Math.Abs(sa - sb) > 3f, "A " + sa + " deg, B " + sb + " deg");
    }

    static bool Drew(DisplayList dl, string s)
    {
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.Text && dl.At(i).Str == s) return true;
        return false;
    }
}
