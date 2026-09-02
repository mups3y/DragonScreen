/*
 * DragonScreen headless tests — the new Figma UI navigation (FigmaUI).
 *
 * The PNG preview shows the pages; it cannot show that a touch on a bottom-bar icon lands on the
 * right icon and routes to the right page, or that the back chevron returns. That is arithmetic on
 * the hit rects, and it would otherwise be found in the capsule at the cost of a restart. Same shape
 * as PageTest: aim at the centre of the rect the UI defines, assert the hit test agrees.
 *
 * NOTE: the icon->page MAPPING itself (FigmaUI.BarTarget) is an inferred design decision, not a fact
 * these tests can prove; they assert the four icons are individually reachable and distinct, and that
 * routing/back behave. If the owner reorders BarTarget, update `want` below to match.
 */
using System;
using DragonScreen;

public static class FigmaUINavTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    const int W = 1280, H = 703;
    const float RefW = 3427f, RefH = 2112f;

    public static int Run()
    {
        Console.WriteLine("DragonScreen Figma UI nav tests");
        BottomBar();
        SuitCheck();
        SuitLeakSimulation();
        VehicleTabs();
        VehicleDeepViewLinksTest();
        CoverPhases();
        CoverCamera();
        SpeccedPages();
        Menu();
        MenuHidesPlaceholders();
        Rendezvous();
        DeorbitBurnPrep();
        EntryProcedure();
        SystemsDeepViews();
        PropSchematicDuty();
        Ascent();
        NavOrbitPlot();
        VehicleLiveValues();
        SubsystemLiveValues();
        ProcedureLiveValues();
        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    static void Menu()
    {
        // T2: the Menu page (UiPage.Menu) is a grid of every OTHER page — one card per entry, tap to
        // jump. Aim at each card's drawn centre (MenuPage.CellRect, the same source Build draws from);
        // assert HitTest resolves it to that entry's real page.
        // S14: the grid no longer lists EVERY page but Menu — a page that resolves to the honest
        // PlaceholderPage (FigmaUI.IsPlaceholder, no real Build case) is left off until a real case
        // is added for it. Count independently from the same predicate MenuPage itself reads.
        int wantEntries = 0;
        for (int i = 0; i < FigmaUI.PageCount; i++)
            if ((UiPage)i != UiPage.Menu && !FigmaUI.IsPlaceholder((UiPage)i)) wantEntries++;
        Check("menu lists every real page but itself", MenuPage.Entries.Length == wantEntries,
              "got " + MenuPage.Entries.Length + " want " + wantEntries);

        bool sawSelf = false;
        for (int i = 0; i < MenuPage.Entries.Length; i++)
            if (MenuPage.Entries[i] == UiPage.Menu) sawSelf = true;
        Check("menu never lists itself", !sawSelf, "");

        for (int i = 0; i < MenuPage.Entries.Length; i++)
        {
            float cx, cy, cw, ch;
            MenuPage.CellRect(i, out cx, out cy, out cw, out ch);
            float px = (cx + cw * 0.5f) / RefW * W;
            float py = (cy + ch * 0.5f) / RefH * H;

            UiPage want = MenuPage.Entries[i];
            NavHit hit = FigmaUI.HitTest(UiPage.Menu, px, py, W, H);
            Check("menu card " + i + " (" + want + ") routes",
                  hit.Act == NavAct.Goto && hit.Target == want, "got " + hit.Act + " " + hit.Target);
        }

        // A touch in the gap between two cards (row 0, between column 0 and column 1) is inert.
        {
            float c0x, c0y, c0w, c0h, c1x, c1y, c1w, c1h;
            MenuPage.CellRect(0, out c0x, out c0y, out c0w, out c0h);
            MenuPage.CellRect(1, out c1x, out c1y, out c1w, out c1h);
            float gx = (c0x + c0w + c1x) * 0.5f / RefW * W;   // midpoint of the gap
            float gy = (c0y + c0h * 0.5f) / RefH * H;
            NavHit hit = FigmaUI.HitTest(UiPage.Menu, gx, gy, W, H);
            Check("menu gap between cards is inert", hit.Act == NavAct.None, "got " + hit.Act);
        }

        // Back: Menu is reached from the Cover (CoverPage.CoverButton.Menu -> UiPage.Menu) and carries
        // the same global bottom bar as every other page, whose Cover icon returns to the Cover — the
        // one back route every page in this UI has (see BottomBar()).
        {
            float sc = H / RefH;
            float bcx = (46f + 40f) / RefW * W, bcy = (2003f + 40f) * sc;
            NavHit back = FigmaUI.HitTest(UiPage.Menu, bcx, bcy, W, H);
            Check("menu bottom-bar -> Cover (back)",
                  back.Act == NavAct.Goto && back.Target == UiPage.Cover, "got " + back.Act + " " + back.Target);

            NavHit toMenu = MapCoverMenu();
            Check("cover Menu button -> Menu page",
                  toMenu.Act == NavAct.Goto && toMenu.Target == UiPage.Menu, "got " + toMenu.Act + " " + toMenu.Target);
        }
    }

    /// <summary>Cover's own Menu button (top-left), reached the same way a crew member would.</summary>
    static NavHit MapCoverMenu()
    {
        float sc = H / RefH;
        return FigmaUI.HitTest(UiPage.Cover, 98f * sc, 108f * sc, W, H);
    }

    static void MenuHidesPlaceholders()
    {
        // S14: UiPage.PhaseDeport/PhaseCoast/PhaseClaw (and every other page with no real Build case)
        // are KEPT — never deleted or renumbered, since the int is what a screen persists — but
        // MenuPage now leaves them off the grid instead of surfacing a look-alike dead card. Don't
        // just trust FigmaUI.IsPlaceholder's own switch: actually BUILD every page and confirm it
        // agrees with what PlaceholderPage.Build really draws ("PAGE NOT YET BUILT"), so the
        // predicate and FigmaUI.Build's switch can never quietly drift apart.
        var dl = new DisplayList(FigmaUI.Commands);
        var s = new PageState();
        var view = MapProjection.Default();

        for (int i = 0; i < FigmaUI.PageCount; i++)
        {
            UiPage p = (UiPage)i;
            dl.Clear();
            FigmaUI.Build(dl, p, W, H, s, view);

            bool drewPlaceholder = false;
            for (int c = 0; c < dl.Count; c++)
                if (dl.At(c).Kind == DrawKind.Text && dl.At(c).Str == "PAGE NOT YET BUILT")
                { drewPlaceholder = true; break; }

            Check("FigmaUI.IsPlaceholder(" + p + ") matches what Build actually draws",
                  drewPlaceholder == FigmaUI.IsPlaceholder(p), "drew placeholder=" + drewPlaceholder);
        }

        // The Menu grid must carry exactly the non-Menu, non-placeholder pages — nothing more, and
        // nothing less (a real page silently dropped would be just as wrong as a dead one showing).
        for (int i = 0; i < FigmaUI.PageCount; i++)
        {
            UiPage p = (UiPage)i;
            bool present = false;
            for (int j = 0; j < MenuPage.Entries.Length; j++)
                if (MenuPage.Entries[j] == p) { present = true; break; }

            bool want = p != UiPage.Menu && !FigmaUI.IsPlaceholder(p);
            Check("menu " + (want ? "lists" : "hides") + " " + p, present == want,
                  "got present=" + present + " want=" + want);
        }
    }

    static void Rendezvous()
    {
        // T6: reached from Docking's letterbox margin - mirrors the HUD's own Docking affordance
        // (SpeccedPages) - and carries the bottom bar like every page.
        float sc = (float)H / RefH, ox = (W - RefW * sc) * 0.5f;

        NavHit toRdv = FigmaUI.HitTest(UiPage.Docking, 30f, 0.5f * H, W, H);
        Check("Docking margin -> Rendezvous", toRdv.Act == NavAct.Goto && toRdv.Target == UiPage.Rendezvous,
              "got " + toRdv.Act + " " + toRdv.Target);

        float bcx = (46f + 40f) / RefW * W, bcy = (2003f + 40f) * sc;
        Check("Rendezvous bottom-bar -> Cover",
              FigmaUI.HitTest(UiPage.Rendezvous, bcx, bcy, W, H).Target == UiPage.Cover, "");

        // Menu (every page but itself) must have picked the new page up automatically.
        bool sawIt = false;
        for (int i = 0; i < MenuPage.Entries.Length; i++)
            if (MenuPage.Entries[i] == UiPage.Rendezvous) sawIt = true;
        Check("Menu lists Rendezvous", sawIt, "");

        // The left icon rail (RendezvousPage: x 40..220, y 220..1800) is chrome only - the real
        // icons are not label-legible in the reference photo, so no destination is invented here.
        NavHit rail = FigmaUI.HitTest(UiPage.Rendezvous, 130f * sc + ox, 420f * sc, W, H);
        Check("Rendezvous rail is inert", rail.Act == NavAct.None, "got " + rail.Act);

        // The Hold Capture card (RendezvousPage: x 260..1180, y 220..680) and its ◄/► arrows are
        // display-only until T14 wires touch.
        NavHit card = FigmaUI.HitTest(UiPage.Rendezvous, 720f * sc + ox, 450f * sc, W, H);
        Check("Rendezvous Hold-Capture card is inert", card.Act == NavAct.None, "got " + card.Act);
    }

    static void DeorbitBurnPrep()
    {
        // T7: reached only via the Menu grid for now (its natural phase-rail entry point is T14's
        // job - see FigmaUI's DeorbitBurnPrep enum comment). Carries the bottom bar like every page,
        // and the reconstructed content cards are display-only (no invented destinations).
        float sc = (float)H / RefH;
        float bcx = (46f + 40f) / RefW * W, bcy = (2003f + 40f) * sc;
        Check("DeorbitBurnPrep bottom-bar -> Cover",
              FigmaUI.HitTest(UiPage.DeorbitBurnPrep, bcx, bcy, W, H).Target == UiPage.Cover, "");

        bool sawIt = false;
        for (int i = 0; i < MenuPage.Entries.Length; i++)
            if (MenuPage.Entries[i] == UiPage.DeorbitBurnPrep) sawIt = true;
        Check("Menu lists DeorbitBurnPrep", sawIt, "");

        // A tap in the content area (well clear of the bottom bar) is inert - no interactive control
        // is claimed by this reconstruction.
        NavHit body = FigmaUI.HitTest(UiPage.DeorbitBurnPrep, 0.5f * W, 0.4f * H, W, H);
        Check("DeorbitBurnPrep body is inert", body.Act == NavAct.None, "got " + body.Act);
    }

    static void EntryProcedure()
    {
        // T8: same footing as DeorbitBurnPrep (T7) - reached only via the Menu grid for now (its
        // natural nav entry point is T14's job), carries the bottom bar, and its one reconstructed
        // content card is display-only (no invented destinations). Distinct from the unrelated
        // UiPage.Entry (14) - see FigmaUI's EntryProcedure enum comment.
        float sc = (float)H / RefH;
        float bcx = (46f + 40f) / RefW * W, bcy = (2003f + 40f) * sc;
        Check("EntryProcedure bottom-bar -> Cover",
              FigmaUI.HitTest(UiPage.EntryProcedure, bcx, bcy, W, H).Target == UiPage.Cover, "");

        bool sawIt = false;
        for (int i = 0; i < MenuPage.Entries.Length; i++)
            if (MenuPage.Entries[i] == UiPage.EntryProcedure) sawIt = true;
        Check("Menu lists EntryProcedure", sawIt, "");

        NavHit body = FigmaUI.HitTest(UiPage.EntryProcedure, 0.5f * W, 0.4f * H, W, H);
        Check("EntryProcedure body is inert", body.Act == NavAct.None, "got " + body.Act);
    }

    static void SystemsDeepViews()
    {
        // T9: the two systems deep-views (SCREEN_INVENTORY #27 + the P&ID entry). Same reachability
        // footing as T7/T8 - Menu grid only, bottom bar always present, content display-only. They are
        // deliberately NOT VehicleTabBar tabs (that strip's eight tabs are confirmed-real, C1.4), so a
        // touch where the tab strip sits on a REAL vehicle page must stay inert here.
        float sc = (float)H / RefH;
        float bcx = (46f + 40f) / RefW * W, bcy = (2003f + 40f) * sc;
        float tabX = VehicleTabBar.CentreX(4) / RefW * W, tabY = 1812f * sc;

        foreach (UiPage p in new[] { UiPage.SystemsTree, UiPage.SystemsPid })
        {
            Check(p + " bottom-bar -> Cover",
                  FigmaUI.HitTest(p, bcx, bcy, W, H).Target == UiPage.Cover, "");

            bool sawIt = false;
            for (int i = 0; i < MenuPage.Entries.Length; i++) if (MenuPage.Entries[i] == p) sawIt = true;
            Check("Menu lists " + p, sawIt, "");

            Check(p + " body is inert",
                  FigmaUI.HitTest(p, 0.5f * W, 0.4f * H, W, H).Act == NavAct.None, "");
            Check(p + " has no subsystem tab strip",
                  FigmaUI.HitTest(p, tabX, tabY, W, H).Act == NavAct.None, "");
            Check(p + " is a real page, not a placeholder", !FigmaUI.IsPlaceholder(p), "");
        }
    }

    static void PropSchematicDuty()
    {
        // T9: the Draco quad indicators are the LIVE RCS demand resolved onto each pod, not decoration.
        // The properties that must hold: nothing fires with RCS off; a pure roll works every pod's
        // tangential thruster and only that one; a fore/aft demand works one axial thruster per pod and
        // not its opposite; and a lateral demand is answered by SOME pods and not all four (a thruster
        // pushes one way only, so the pods on the demand's own side stay quiet).
        PageState s = new PageState();
        s.Valid = true;

        s.RcsOn = false; s.RotRoll = 1f;
        bool anyOff = false;
        for (int q = 0; q < 4; q++) if (PropSchematic.QuadDuty(s, q) > 0f) anyOff = true;
        Check("prop schematic: RCS off means no quad fires", !anyOff, "");

        s.RcsOn = true;
        bool allRoll = true, otherRoles = false;
        for (int q = 0; q < 4; q++)
        {
            if (PropSchematic.ThrusterDuty(s, q, 3) < 0.99f) allRoll = false;
            for (int r = 0; r < 3; r++) if (PropSchematic.ThrusterDuty(s, q, r) > 0f) otherRoles = true;
        }
        Check("prop schematic: roll demand works every tangential thruster", allRoll, "");
        Check("prop schematic: roll demand works nothing else", !otherRoles, "");

        s.RotRoll = 0f; s.TransZ = 0.5f;
        bool fwdAll = true, aftAny = false;
        for (int q = 0; q < 4; q++)
        {
            if (Math.Abs(PropSchematic.ThrusterDuty(s, q, 0) - 0.5f) > 0.001f) fwdAll = false;
            if (PropSchematic.ThrusterDuty(s, q, 1) > 0f) aftAny = true;
        }
        Check("prop schematic: +Z works the forward thruster in every pod", fwdAll, "");
        Check("prop schematic: +Z leaves the opposing aft thrusters idle", !aftAny, "");

        s.TransZ = 0f; s.TransY = 1f;
        int lit = 0;
        for (int q = 0; q < 4; q++) if (PropSchematic.ThrusterDuty(s, q, 2) > 0f) lit++;
        Check("prop schematic: a lateral demand lights some pods, not all", lit > 0 && lit < 4,
              "lit " + lit);
    }

    static void Ascent()
    {
        // T12: same reachability footing as DeorbitBurnPrep (T7) / EntryProcedure (T8) - reached only
        // via the Menu grid for now (a real entry point is T14's job), carries the bottom bar, and its
        // reconstructed content (the F9 schematic + event callouts) is display-only.
        float sc = (float)H / RefH;
        float bcx = (46f + 40f) / RefW * W, bcy = (2003f + 40f) * sc;
        Check("Ascent bottom-bar -> Cover",
              FigmaUI.HitTest(UiPage.Ascent, bcx, bcy, W, H).Target == UiPage.Cover, "");

        bool sawIt = false;
        for (int i = 0; i < MenuPage.Entries.Length; i++)
            if (MenuPage.Entries[i] == UiPage.Ascent) sawIt = true;
        Check("Menu lists Ascent", sawIt, "");

        NavHit body = FigmaUI.HitTest(UiPage.Ascent, 0.5f * W, 0.4f * H, W, H);
        Check("Ascent body is inert", body.Act == NavAct.None, "got " + body.Act);
        Check("Ascent is a real page, not a placeholder", !FigmaUI.IsPlaceholder(UiPage.Ascent), "");
    }

    static void NavOrbitPlot()
    {
        // S15: the circular nav/orbit plot (SCREEN_INVENTORY #28) - same reachability footing as
        // Ascent (T12) / DeorbitBurnPrep (T7) / EntryProcedure (T8) - reached only via the Menu grid
        // for now (a real entry point is T14's job), carries the bottom bar, and its content (the
        // concentric rings, the shared NavPage.Orbit conic, the colour key, the g/rate readout) is
        // display-only - no invented destinations.
        float sc = (float)H / RefH;
        float bcx = (46f + 40f) / RefW * W, bcy = (2003f + 40f) * sc;
        Check("NavOrbitPlot bottom-bar -> Cover",
              FigmaUI.HitTest(UiPage.NavOrbitPlot, bcx, bcy, W, H).Target == UiPage.Cover, "");

        bool sawIt = false;
        for (int i = 0; i < MenuPage.Entries.Length; i++)
            if (MenuPage.Entries[i] == UiPage.NavOrbitPlot) sawIt = true;
        Check("Menu lists NavOrbitPlot", sawIt, "");

        NavHit body = FigmaUI.HitTest(UiPage.NavOrbitPlot, 0.5f * W, 0.4f * H, W, H);
        Check("NavOrbitPlot body is inert", body.Act == NavAct.None, "got " + body.Act);
        Check("NavOrbitPlot is a real page, not a placeholder", !FigmaUI.IsPlaceholder(UiPage.NavOrbitPlot), "");
    }


    // ---- T13a: the VEHICLE family reads the vessel, not a constant ----
    // The PNG preview shows that the numbers LOOK right; it cannot show that they came from PageState.
    // These do, and they are worth more than an eyeball pass: the failure this catches is a readout that
    // was wired once, then quietly re-hardcoded, which renders identically in every preview ever taken.
    // The shape is the same for all three pages — build with fixture A, build with a DIFFERENT fixture B,
    // and assert every value moved. A constant cannot pass that, whatever its value.

    /// <summary>Did the page draw this exact string anywhere?</summary>
    static bool Drew(DisplayList dl, string text)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == text) return true;
        }
        return false;
    }

    /// <summary>The colour the page drew this exact string in, the first time it drew it. A control
    /// that is DIMMED to say it is unavailable differs from a live one only by its colour, so that
    /// claim cannot be tested by looking for the string (S32's TROUBLESHOOT).</summary>
    static Rgba ColourOf(DisplayList dl, string text)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == text) return c.Colour;
        }
        return new Rgba(0f, 0f, 0f, 0f);
    }

    static bool SameColour(Rgba a, Rgba b)
    { return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A; }

    /// <summary>How many TIMES the page drew this exact string. A page that draws one datum in two
    /// places (DockingSimPage's ring readouts and its PYR block are the same group) has to draw the SAME
    /// string in both, and this is how that is proved rather than assumed.</summary>
    static int Times(DisplayList dl, string text)
    {
        int n = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == text) n++;
        }
        return n;
    }

    /// <summary>The line commands drawn in this colour, in order. The rendezvous plot's approach chord
    /// is the only thing on that page drawn as a Caution line, so this is how a chord that moved, or a
    /// chord that should not be there at all, is checked without reaching into the page.</summary>
    static System.Collections.Generic.List<DrawCmd> Lines(DisplayList dl, Rgba want)
    {
        var hits = new System.Collections.Generic.List<DrawCmd>();
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Line) continue;
            if (c.Colour.R == want.R && c.Colour.G == want.G && c.Colour.B == want.B) hits.Add(c);
        }
        return hits;
    }

    /// <summary>How many arc bands the page drew in this colour — the gauge FILLS, as distinct from the
    /// faint rings they sit in. A ring that never moves is decoration, and this is how that shows up.</summary>
    static int Arcs(DisplayList dl, Rgba want)
    {
        int n = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.ArcBand) continue;
            if (c.Colour.R == want.R && c.Colour.G == want.G && c.Colour.B == want.B) n++;
        }
        return n;
    }

    /// <summary>A vehicle fixture whose every live readout is a distinct, recognisable string.</summary>
    static PageState VehicleFixture(int variant)
    {
        PageState s = new PageState();
        s.Valid = true;
        string k = variant == 0 ? "1" : "2";
        s.Ppo2Text = "3.0" + k;  s.CabinTempText = "21." + k;
        s.PressText = "14.7" + k; s.Co2Text = "1.0" + k;
        // 27, not 26: "26.1" was the THERMAL tab's own old hard-coded LOOP A, and a fixture value
        // equal to the constant it replaced makes that tab's "no longer hard-codes" guard vacuous.
        s.LoopAText = "27." + k; s.LoopBText = "20." + k;
        s.NetPwr1Text = "-5" + k; s.NetPwr2Text = "-4" + k;
        s.PowerUnit1Text = "8" + k + " %"; s.PowerUnit2Text = "8" + k + " %";
        s.DeorbitFuelText = "70" + k + ".0 kg"; s.DeorbitOxText = "130" + k + ".0 kg";
        s.SolarArrayText = variant == 0 ? "DEPLOYED" : "STOWED";
        // 2 / 5, not 4 / 4: "4 / 4" was the Power checklist's own old hard-coded BATTERIES state
        // (pre-S25), and a fixture value equal to the constant it replaced makes that tab's
        // "no longer hard-codes" guard vacuous — the same reasoning LoopAText's comment gives above.
        // "/ 5" (not 3, 4 or 2) also keeps it distinct from AVIONICS' own static "3 / 3" and GNC's
        // static "2 / 2", which the cross-tab "avionics/other tabs invent no value" checks would
        // otherwise trip on by coincidence.
        s.BatteryText = variant == 0 ? "2 / 5" : "1 / 5";
        s.AccelPosText = "1.4" + k; s.AccelNegText = "0.3" + k; s.AccelCentText = "0.88" + k;
        // The rings come from the raw numbers, never from the text, so they are set independently.
        s.Cabin.Ppo201 = 0.6; s.Cabin.CabinTemp01 = 0.55; s.Cabin.Press01 = 0.73; s.Cabin.Co201 = 0.2;
        s.Cabin.LoopA01 = 0.33; s.Cabin.LoopB01 = 0.25;
        s.Cabin.NetPwr1W = -51.0; s.Cabin.NetPwr2W = -41.0;
        s.AccelPos01 = 0.28; s.AccelNeg01 = 0.06; s.AccelCent01 = 0.44;
        s.SeatCount = 4;
        s.Systems = SystemsState.Fresh();

        // ---- T13b: the six subsystem tabs' own sources ----
        // A second variant digit, so a new value can never accidentally equal one of the T13a strings
        // above (which would make a "this moved" check pass for the wrong reason).
        string j = variant == 0 ? "3" : "4";
        s.CrewText = (variant == 0 ? "3" : "2") + " / 4";
        s.Crew01 = variant == 0 ? 0.75 : 0.5;
        s.O2TankText = "6" + j + " %"; s.N2TankText = "7" + j + " %";
        s.WaterText = "10" + j + " L"; s.Water01 = 0.72;
        s.DragonOxText = "9" + j; s.DragonFuelText = "7" + j; s.DragonPropText = "5" + j;
        s.PropRemainingText = "5" + j + " %"; s.DracoDutyText = "3" + j + " %";
        s.DragonOx01 = 0.87; s.DragonFuel01 = 0.83; s.DragonProp01 = 0.85;
        s.PowerText = "1" + j; s.Power01 = 0.13;
        s.ArrayKwText = "2.6" + j; s.ArrayOutputText = "2.6" + j + " kW"; s.Array01 = 0.76;
        s.NetPowerText = "-9" + j + " W"; s.ChargeRateText = "-0.09" + j + " kW";
        s.HullTempText = "31" + j; s.TpsMaxText = "31" + j + " °C"; s.HullTemp01 = 0.41;
        s.BodyRollText = "-0.05" + j; s.BodyPitchText = "0.12" + j; s.BodyYawText = "0.31" + j;
        s.BodyRateText = "0." + j + "3 deg/s";
        s.BodyRollDps = -0.05; s.BodyPitchDps = 0.12; s.BodyYawDps = 0.31;
        // GNC's four right-hand rows come from state the FLIGHT pages already carry, so the fixture has
        // to carry it too: a target to be misaligned with, an orbit, and a control-authority word.
        s.HasTarget = true; s.Align01 = 0.06; s.AlignText = "5.4" + j + " deg";
        s.ModeText = variant == 0 ? "AUTO" : "MANUAL";
        // S24: AVIONICS' one wired checklist row + its two CommNet readouts. Uplink and Downlink share
        // ONE value (CommNet has no separate up/down budget), like PowerUnit1Text/PowerUnit2Text above.
        s.SBandText = variant == 0 ? "Linked" : "No Signal"; s.SBandLinked = variant == 0;
        s.UplinkText = "8" + j + " %"; s.DownlinkText = "8" + j + " %";
        s.CommSignal01 = variant == 0 ? 0.83 : 0.84;
        s.Regime = FlightRegime.Space;
        s.Altitude = "123.4" + j + " km";
        s.Velocity = "228" + j + " m/s"; s.SurfaceVelocity = "17" + j + " m/s";
        s.VelocityMps = 2280.0; s.SurfaceVelocityMps = 175.0; s.CircularSpeedMps = 2426.0;
        s.AltitudeM = 123400.0; s.AtmosphereDepthM = 70000.0; s.BodyRadiusM = 600000.0;
        return s;
    }

    static void VehicleLiveValues()
    {
        const int VW = 2560, VH = 1406;

        PageState a = VehicleFixture(0), b = VehicleFixture(1);
        PageState dead = VehicleFixture(0); dead.Valid = false;

        // ---------------- VEHICLE OVERVIEW ----------------
        DisplayList da = new DisplayList(VehicleOverviewPage.Commands + 60);
        DisplayList db = new DisplayList(VehicleOverviewPage.Commands + 60);
        DisplayList dd = new DisplayList(VehicleOverviewPage.Commands + 60);
        VehicleOverviewPage.Build(da, VW, VH, a);
        VehicleOverviewPage.Build(db, VW, VH, b);
        VehicleOverviewPage.Build(dd, VW, VH, dead);

        string[] ov = { a.Ppo2Text, a.CabinTempText, a.PressText, a.Co2Text,
                        a.LoopAText, a.LoopBText, a.NetPwr1Text, a.NetPwr2Text,
                        a.PowerUnit1Text, a.DeorbitFuelText, a.DeorbitOxText };
        for (int i = 0; i < ov.Length; i++)
        {
            Check("overview draws PageState value " + ov[i], Drew(da, ov[i]), "");
            Check("overview value " + ov[i] + " is not a constant", !Drew(db, ov[i]), "still drawn for a different state");
            Check("overview drops " + ov[i] + " with no feed", !Drew(dd, ov[i]), "");
        }
        // The eight ring FILLS: one per gauge, and none of them when the feed is dead.
        Check("overview draws no gauge fill with no feed",
              Arcs(dd, DragonPalette.Go) == 0 && Arcs(dd, DragonPalette.Accent) == 0, "");
        // Both loop gauges read the two DIFFERENT loops the model computes (S20 is about the label).
        Check("overview loop gauges read Loop A and Loop B",
              Drew(da, a.LoopAText) && Drew(da, a.LoopBText) && a.LoopAText != a.LoopBText, "");
        // The four subtank rows and MARGIN have no source and must stay dashed, never zeroed.
        Check("overview dashes the unsourced consumables rows", Drew(da, "—"), "");
        // The values these pages used to hard-code must never come back.
        string[] gone = { "2.69", "16.43", "14.0", "1.05", "26.05", "21.06", "0.03", "3.02",
                          "100 %", "791.1 kg", "1308 kg", "67.76 kg", "111.3 kg" };
        for (int i = 0; i < gone.Length; i++)
            Check("overview no longer hard-codes " + gone[i], !Drew(da, gone[i]), "");

        // ---------------- MECH PANEL ----------------
        DisplayList ma = new DisplayList(VehicleMechPage.Commands + 60);
        DisplayList mb = new DisplayList(VehicleMechPage.Commands + 60);
        DisplayList md = new DisplayList(VehicleMechPage.Commands + 60);
        VehicleMechPage.Build(ma, VW, VH, a);
        VehicleMechPage.Build(mb, VW, VH, b);
        VehicleMechPage.Build(md, VW, VH, dead);

        string[] mech = { a.AccelPosText, a.AccelNegText, a.AccelCentText, a.PressText };
        for (int i = 0; i < mech.Length; i++)
        {
            Check("mech draws PageState value " + mech[i], Drew(ma, mech[i]), "");
            Check("mech value " + mech[i] + " is not a constant", !Drew(mb, mech[i]), "");
            Check("mech drops " + mech[i] + " with no feed", !Drew(md, mech[i]), "");
        }
        // Four of the five nodes have a source; WATER UPRIGHTING has none and must draw no fill.
        Check("mech fills one ring per sourced node", Arcs(ma, DragonPalette.Accent) == 4,
              "got " + Arcs(ma, DragonPalette.Accent));
        Check("mech fills no ring with no feed", Arcs(md, DragonPalette.Accent) == 0,
              "got " + Arcs(md, DragonPalette.Accent));
        string[] mgone = { "79610.01", "71367.02", "73225.03", "75169.04", "71228.05",
                           "1204", "1198", "1211", "1207" };
        for (int i = 0; i < mgone.Length; i++)
            Check("mech no longer hard-codes " + mgone[i], !Drew(ma, mgone[i]), "");
        // The SEAT rows follow the capsule's real seat count, not a fixed four.
        PageState twoSeats = VehicleFixture(0); twoSeats.SeatCount = 2;
        DisplayList m2 = new DisplayList(VehicleMechPage.Commands + 60);
        VehicleMechPage.Build(m2, VW, VH, twoSeats);
        Check("mech draws a row per real seat", Drew(m2, "SEAT 2 TACH") && !Drew(m2, "SEAT 3 TACH"), "");

        // ---------------- SYSTEMS TREE ----------------
        DisplayList ta = new DisplayList(SystemsTreePage.Commands + 60);
        DisplayList tb = new DisplayList(SystemsTreePage.Commands + 60);
        DisplayList td = new DisplayList(SystemsTreePage.Commands + 60);
        SystemsTreePage.Build(ta, VW, VH, a);
        SystemsTreePage.Build(tb, VW, VH, b);
        SystemsTreePage.Build(td, VW, VH, dead);

        Check("tree draws the live array state", Drew(ta, a.SolarArrayText), "");
        Check("tree array state is not a constant", !Drew(tb, a.SolarArrayText), "");
        Check("tree draws the live battery count", Drew(ta, a.BatteryText), "");
        Check("tree battery count is not a constant", !Drew(tb, a.BatteryText), "");
        Check("tree dashes both sources with no feed",
              !Drew(td, a.SolarArrayText) && !Drew(td, a.BatteryText), "");
        // S23 (owner decision (b)): the count claim is DROPPED — the box now reads plain "BATTERIES",
        // never "×4"/"x4" beside a live count that can disagree with it.
        Check("tree battery label carries no count claim", Drew(ta, "BATTERIES"), "");
        Check("tree battery label drops the ×4", !Drew(ta, "BATTERIES ×4") && !Drew(ta, "BATTERIES x4"), "");
    }


    // ---- T13b: the six subsystem sub-tabs read the vessel, not a constant ----
    // Same shape and the same reasoning as VehicleLiveValues above: build each tab with fixture A, build
    // it again with a DIFFERENT fixture B, and assert every value moved. The extra thing worth proving
    // here is the OPPOSITE for most of AVIONICS — this build models almost none of that subsystem, so
    // its seven unsourced gauge/row values must dash and must NOT move with the fixture; a later
    // "improvement" that quietly fills them with a plausible constant is exactly what this catches.
    // S24 (owner decision) is the one exception: S-BAND COMMS + Uplink/Downlink are wired to stock
    // CommNet, so those three DO move like every other tab's live values — checked the same way below.
    static void SubsystemLiveValues()
    {
        const int VW = 2560, VH = 1406;

        PageState a = VehicleFixture(0), b = VehicleFixture(1);
        PageState dead = VehicleFixture(0); dead.Valid = false;

        VehicleSubsystemPage.Sub[] subs = {
            VehicleSubsystemPage.Sub.Crew, VehicleSubsystemPage.Sub.Propulsion,
            VehicleSubsystemPage.Sub.Power, VehicleSubsystemPage.Sub.Avionics,
            VehicleSubsystemPage.Sub.Gnc, VehicleSubsystemPage.Sub.Thermal };

        // Every value each tab is claimed to have wired, by tab. Avionics is deliberately EMPTY.
        string[][] live = {
            // CREW: four cabin gauges + O2 / N2 / water / crew aboard.
            new[] { a.Ppo2Text, a.CabinTempText, a.PressText, a.Co2Text,
                    a.O2TankText, a.N2TankText, a.WaterText, a.CrewText },
            // PROP: both tanks, the combined remaining, the live Draco duty.
            new[] { a.DragonOxText, a.DragonFuelText, a.PropRemainingText, a.DracoDutyText },
            // POWER: state of charge, array output twice (gauge + row), net flow twice (W and kW),
            // and (S25) the checklist's own BATTERIES / SOLAR ARRAY states — the same two fields the
            // systems tree draws (T13a), so the two pages can no longer disagree.
            new[] { a.PowerText, a.ArrayKwText, a.ArrayOutputText, a.NetPowerText, a.ChargeRateText,
                    a.BatteryText, a.SolarArrayText },
            // AVIONICS (S24): S-BAND COMMS' checklist state + the two CommNet readouts. Everything else
            // on this tab is asserted NOT to move, below.
            new[] { a.SBandText, a.UplinkText, a.DownlinkText },
            // GNC: three body rates, the RCS tank, alignment, total rate, altitude, velocity, authority.
            new[] { a.BodyRollText, a.BodyPitchText, a.BodyYawText, a.DragonPropText,
                    a.AlignText, a.BodyRateText, a.Altitude, a.Velocity, a.ModeText },
            // THERMAL: both loops and the hull temperature, twice (SHIELD gauge + TPS Max row).
            new[] { a.LoopAText, a.LoopBText, a.HullTempText, a.TpsMaxText } };

        // The constants each tab used to hard-code. None may EVER come back — a value wired once and
        // then quietly re-hardcoded renders identically in every preview ever taken.
        string[][] gone = {
            new[] { "2.69", "22.4", "14.7", "1.05", "44 %", "96 %", "88 %", "72 L" },
            new[] { "84", "82", "310", "24.6", "0 psia", "83 %", "18 °C", "100 %" },
            new[] { "100", "120", "3.4", "3.4 kW", "+68 W", "50 %", "19 °C", "0 kW", "4 / 4", "Deployed" },
            new[] { "38", "42", "8.4", "61", "ONLINE", "11", "Strong", "256 kbps" },
            new[] { "0.02", "0.01", "0.03", "83", "0.4°", "0.04 °/s", "380.5 km", "6.68 km/s", "AUTO / SUN" },
            new[] { "26.1", "21.1", "8.2", "34", "1.2 L/s", "1.1 L/s", "3.1 kW", "22 °C", "34 °C" } };

        for (int i = 0; i < subs.Length; i++)
        {
            DisplayList da = new DisplayList(VehicleSubsystemPage.Commands + 60);
            DisplayList db = new DisplayList(VehicleSubsystemPage.Commands + 60);
            DisplayList dd = new DisplayList(VehicleSubsystemPage.Commands + 60);
            VehicleSubsystemPage.Build(da, VW, VH, subs[i], a);
            VehicleSubsystemPage.Build(db, VW, VH, subs[i], b);
            VehicleSubsystemPage.Build(dd, VW, VH, subs[i], dead);

            for (int k = 0; k < live[i].Length; k++)
            {
                string want = live[i][k];
                Check(subs[i] + " draws PageState value " + want, Drew(da, want), "");
                Check(subs[i] + " value " + want + " is not a constant", !Drew(db, want),
                      "still drawn for a different state");
                Check(subs[i] + " drops " + want + " with no feed", !Drew(dd, want), "");
            }
            for (int k = 0; k < gone[i].Length; k++)
                Check(subs[i] + " no longer hard-codes " + gone[i][k], !Drew(da, gone[i][k]), "");

            // A tab with an unsourced readout must SAY so on the LIVE fixture too — that is where a
            // plausible invented number would otherwise hide. GNC is the exception and the exception is
            // the point: with a target present every one of its nine values has a source, so it is the
            // one tab that legitimately shows no dash at all.
            if (subs[i] != VehicleSubsystemPage.Sub.Gnc)
                Check(subs[i] + " dashes what it has no source for", Drew(da, "—"), "");
            else
                Check("gnc has no unsourced readout with a target", !Drew(da, "—"), "");
            // And with no feed at all, every value on every tab dashes.
            Check(subs[i] + " dashes everything with no feed", Drew(dd, "—"), "");
        }

        // ---- S23: the Power checklist's BATTERIES label carries no count claim ----
        // The label itself is static (CkLabel, not a fixture-driven value), so the main loop above
        // doesn't touch it — checked once here, on either fixture build.
        DisplayList pw = new DisplayList(VehicleSubsystemPage.Commands + 60);
        VehicleSubsystemPage.Build(pw, VW, VH, VehicleSubsystemPage.Sub.Power, a);
        Check("power checklist names BATTERIES with no count claim", Drew(pw, "BATTERIES"), "");
        Check("power checklist drops the ×4",
              !Drew(pw, "BATTERIES ×4") && !Drew(pw, "BATTERIES x4"), "");

        // ---- AVIONICS: no OTHER tab's numbers leak onto it, and it fills no gauge ring ----
        // Stronger than "it drew a dash": not one value from another subsystem, in EITHER fixture, may
        // appear anywhere on this tab. Its OWN three CommNet values (S24) are checked in the main loop
        // above like any other tab's live values — skipped here, or this would assert the opposite of
        // what S24 wired in.
        DisplayList av = new DisplayList(VehicleSubsystemPage.Commands + 60);
        DisplayList av2 = new DisplayList(VehicleSubsystemPage.Commands + 60);
        VehicleSubsystemPage.Build(av, VW, VH, VehicleSubsystemPage.Sub.Avionics, a);
        VehicleSubsystemPage.Build(av2, VW, VH, VehicleSubsystemPage.Sub.Avionics, b);
        for (int i = 0; i < live.Length; i++)
        {
            if (subs[i] == VehicleSubsystemPage.Sub.Avionics) continue;
            for (int k = 0; k < live[i].Length; k++)
                Check("avionics invents no value (" + live[i][k] + ")",
                      !Drew(av, live[i][k]) && !Drew(av2, live[i][k]), "");
        }
        // The four headline gauges (FC LOAD / BUS TRAFFIC / LINK MARGIN / STORAGE) stay unsourced and
        // must never fill — unaffected by S24, which only wires two RIGHT-column readouts.
        Check("avionics fills no gauge ring", Arcs(av, DragonPalette.Accent) == 0
              && Arcs(av, DragonPalette.Go) == 0, "");

        // ---- S24's OWN guard: an OTHERWISE-VALID vessel with CommNet off/absent must dash gracefully ----
        // Distinct from the "dead"/Valid=false fixture above (no vessel feed at all): this is a live
        // vessel where VesselData.Avionics found no CommNetVessel (CommNet off in difficulty settings,
        // RemoteTech installed, or — RSS/RO — no comm hardware) and left the three fields null, exactly
        // as it does. The page must never keep showing a stale "Linked" / signal percentage.
        PageState commOff = VehicleFixture(0);
        commOff.SBandText = null; commOff.SBandLinked = false;
        commOff.UplinkText = null; commOff.DownlinkText = null; commOff.CommSignal01 = 0.0;
        DisplayList avOff = new DisplayList(VehicleSubsystemPage.Commands + 60);
        VehicleSubsystemPage.Build(avOff, VW, VH, VehicleSubsystemPage.Sub.Avionics, commOff);
        Check("avionics dashes S-BAND with no CommNet", !Drew(avOff, a.SBandText), "");
        Check("avionics dashes Uplink with no CommNet", !Drew(avOff, a.UplinkText), "");
        Check("avionics dashes Downlink with no CommNet", !Drew(avOff, a.DownlinkText), "");
        Check("avionics still shows a dash with no CommNet", Drew(avOff, "—"), "");

        // ---- the rings move with the numbers, and empty when the feed dies ----
        // CREW is the tab where all four gauges have a source; a fill per gauge is the count to hold.
        DisplayList cr = new DisplayList(VehicleSubsystemPage.Commands + 60);
        DisplayList crDead = new DisplayList(VehicleSubsystemPage.Commands + 60);
        VehicleSubsystemPage.Build(cr, VW, VH, VehicleSubsystemPage.Sub.Crew, a);
        VehicleSubsystemPage.Build(crDead, VW, VH, VehicleSubsystemPage.Sub.Crew, dead);
        int crewFills = Arcs(cr, Rgba.Hex("D7B733")) + Arcs(cr, Rgba.Hex("D12C30"))
                      + Arcs(cr, Rgba.Hex("FCD533")) + Arcs(cr, Rgba.Hex("2983ED"));
        Check("crew fills one ring per sourced gauge", crewFills == 4, "got " + crewFills);
        int crewDeadFills = Arcs(crDead, Rgba.Hex("D7B733")) + Arcs(crDead, Rgba.Hex("D12C30"))
                          + Arcs(crDead, Rgba.Hex("FCD533")) + Arcs(crDead, Rgba.Hex("2983ED"));
        Check("crew fills no ring with no feed", crewDeadFills == 0, "got " + crewDeadFills);
        // THERMAL has three sourced gauges of four: the RADIATOR has no model and must stay empty.
        DisplayList th = new DisplayList(VehicleSubsystemPage.Commands + 60);
        VehicleSubsystemPage.Build(th, VW, VH, VehicleSubsystemPage.Sub.Thermal, a);
        int thermFills = Arcs(th, Rgba.Hex("2983ED")) + Arcs(th, Rgba.Hex("D12C30"))
                       + Arcs(th, DragonPalette.Accent);
        Check("thermal leaves the unsourced radiator ring empty", thermFills == 3, "got " + thermFills);

        // ---- the PROP data band carries the same numbers, because it is passed the same source ----
        // PropSchematic re-draws this tab's gauge + detail values; wiring the source had to fix both.
        Check("prop schematic band draws the live tank fractions",
              Drew(DrawProp(a, VW, VH), a.DragonOxText) && Drew(DrawProp(a, VW, VH), a.DragonFuelText), "");
        // Draco duty is derived from the LIVE RCS demand, and MaxDuty is what both the band's number and
        // the schematic's own segments read — so a firing vehicle cannot show an idle duty.
        PageState firing = VehicleFixture(0);
        firing.RcsOn = true; firing.TransZ = 0.5f; firing.RotRoll = 0.25f;
        Check("prop duty follows the live RCS demand",
              PropSchematic.MaxDuty(firing) > 0.4f && PropSchematic.MaxDuty(a) == 0f,
              "firing " + PropSchematic.MaxDuty(firing) + " idle " + PropSchematic.MaxDuty(a));

        // ---- GNC's body rates survive with NO TARGET, which is the bug that moved them out of Docking ----
        PageState noTgt = VehicleFixture(0); noTgt.HasTarget = false;
        DisplayList gn = new DisplayList(VehicleSubsystemPage.Commands + 60);
        VehicleSubsystemPage.Build(gn, VW, VH, VehicleSubsystemPage.Sub.Gnc, noTgt);
        Check("gnc keeps its rates with no target",
              Drew(gn, a.BodyRollText) && Drew(gn, a.BodyPitchText) && Drew(gn, a.BodyYawText), "");
        Check("gnc dashes attitude error with no target", !Drew(gn, a.AlignText), "");
        // VELOCITY goes through the shared OrbitReadout rule, so this page cannot read orbital speed on
        // the ground while FLIGHT reads surface speed (Pages.cs, "this is the third time").
        PageState ground = VehicleFixture(0);
        ground.Regime = FlightRegime.Ground;
        DisplayList gg = new DisplayList(VehicleSubsystemPage.Commands + 60);
        VehicleSubsystemPage.Build(gg, VW, VH, VehicleSubsystemPage.Sub.Gnc, ground);
        Check("gnc velocity follows OrbitReadout on the ground",
              Drew(gg, ground.SurfaceVelocity) && !Drew(gg, ground.Velocity), "");

        // ---- the rate dial's full scale is STATED, and the dial has to survive it ----
        Check("body-rate dial full scale is stated", VehicleSubsystemPage.RateFullScaleDps > 0.0, "");
    }

    /// <summary>The Prop tab's FUNCTIONS view, which is PropSchematic — its data band is fed the same
    /// values the template's gauges and rows carry, so it is checked through the page, not around it.</summary>
    static DisplayList DrawProp(PageState s, int w, int h)
    {
        DisplayList dl = new DisplayList(VehicleSubsystemPage.Commands + 60);
        VehicleSubsystemPage.Build(dl, w, h, VehicleSubsystemPage.Sub.Propulsion, s);
        return dl;
    }

    /// <summary>A prox-ops / procedure fixture: every live readout a distinct, recognisable string, and
    /// a target sitting somewhere specific on the orbit plot.</summary>
    static PageState ProcFixture(int variant)
    {
        PageState s = new PageState();
        s.Valid = true;
        string k = variant == 0 ? "1" : "2";

        // ---- the Manual Chute top strip ----
        s.Phase = variant == 0 ? "DEORBIT COAST" : "ENTRY";
        s.SplashdownShown = true; s.SplashdownText = "T- 0" + k + ":08:36";
        s.Velocity = "7.6" + k + " km/s";
        s.Altitude = "406." + k + " km";
        s.ApogeeShown = true;  s.Apoapsis = "428." + k + " km";
        s.PerigeeShown = true; s.Periapsis = "380." + k + " km";
        s.InclinationText = "51.6" + k + " deg";
        s.InclinationDegText = "51.6" + k + "°";

        // ---- the manual docking readouts ----
        s.HasTarget = true; s.TargetName = "SPACE X STATION";
        s.RollDegText = "15." + k + "°";  s.RollDeg  = variant == 0 ? 15.1 : 15.2;
        s.PitchDegText = "3." + k + "°";  s.PitchDeg = variant == 0 ? 3.1  : 3.2;
        s.YawDegText = "7." + k + "°";    s.YawDeg   = variant == 0 ? 7.1  : 7.2;
        s.RangeText = "11." + k + " m"; s.RateText = "-0." + k + "4 m/s";
        // S26: the PYR block's own quantity (body rates, T13b) — distinct strings from the DegText
        // trio above so a test that finds one can never accidentally be satisfied by the other.
        s.PitchRateText = "0." + k + "5 deg/s";
        s.YawRateText   = "0." + k + "6 deg/s";
        s.RollRateText  = "0." + k + "7 deg/s";

        // ---- the cabin the SUIT LEAK CHECK measures against (S31) ----
        // The suit differential is suit-loop pressure minus THIS, so a different cabin here has to move
        // all four rows. That is the whole "it is a simulation, not a constant" proof for that page.
        s.Cabin.PressPsia = variant == 0 ? 14.70 : 14.62;

        // ---- the orbit plot the approach chord is drawn on ----
        s.Regime = FlightRegime.Space;
        s.BodyRadiusM = 600000.0; s.AtmosphereDepthM = 70000.0;
        s.AltitudeM = 123400.0; s.ApogeeM = 124000.0; s.PerigeeM = 121900.0;
        s.Ascending = true;
        s.HasTargetOrbit = true;
        s.TargetRadiusM = 728000.0;
        s.TargetPhaseRad = variant == 0 ? 0.7 : -0.9;   // ahead of us, then behind
        return s;
    }

    // ---- T13c: the procedure + prox-ops pages read the vessel, not a constant ----
    // Same shape and the same reasoning as VehicleLiveValues / SubsystemLiveValues: build each page with
    // fixture A, build it again with a DIFFERENT fixture B, and assert every wired value MOVED - a
    // constant cannot pass that whatever its value. Then the inverses that matter on these pages: the
    // Manual Chute strip must dash on a dead feed and on a quantity its own flags call meaningless; the
    // docking readouts must dash with NO TARGET rather than read a confident zero error against nothing;
    // the suit delta pressures and the four deorbit SLEW rows must stay dashed and must never pick up a
    // fixture value (this build models no suit, and the slew is Part B's to command).
    static void ProcedureLiveValues()
    {
        const int VW = 2560, VH = 1406;
        PageState a = ProcFixture(0), b = ProcFixture(1);
        PageState dead = ProcFixture(0); dead.Valid = false;

        // ---------------- MANUAL CHUTE DEPLOY: the top telemetry strip ----------------
        DisplayList ca = new DisplayList(ManualChuteDeployPage.Commands + 60);
        DisplayList cb = new DisplayList(ManualChuteDeployPage.Commands + 60);
        DisplayList cd = new DisplayList(ManualChuteDeployPage.Commands + 60);
        ManualChuteDeployPage.Build(ca, VW, VH, a, MapProjection.Default());
        ManualChuteDeployPage.Build(cb, VW, VH, b, MapProjection.Default());
        ManualChuteDeployPage.Build(cd, VW, VH, dead, MapProjection.Default());

        string[] strip = { a.Phase, a.SplashdownText, a.Velocity, a.Altitude,
                           a.Apoapsis, a.Periapsis, a.InclinationDegText };
        for (int i = 0; i < strip.Length; i++)
        {
            Check("chute strip draws PageState value " + strip[i], Drew(ca, strip[i]), "");
            Check("chute strip value " + strip[i] + " is not a constant", !Drew(cb, strip[i]),
                  "still drawn for a different state");
            Check("chute strip drops " + strip[i] + " with no feed", !Drew(cd, strip[i]), "");
        }
        Check("chute strip dashes with no feed", Drew(cd, "—"), "");
        // The apsides follow the same flags every other page's do, and SPLASHDOWN TIME follows the
        // registry's "N/A off-return" - none of the three may keep printing a stale number.
        PageState onPad = ProcFixture(0);
        onPad.ApogeeShown = false; onPad.PerigeeShown = false; onPad.SplashdownShown = false;
        DisplayList cp = new DisplayList(ManualChuteDeployPage.Commands + 60);
        ManualChuteDeployPage.Build(cp, VW, VH, onPad, MapProjection.Default());
        Check("chute strip dashes an apogee that is not meaningful", !Drew(cp, a.Apoapsis), "");
        Check("chute strip dashes a perigee that is not meaningful", !Drew(cp, a.Periapsis), "");
        Check("chute strip dashes splashdown off a return", !Drew(cp, a.SplashdownText), "");
        Check("chute strip keeps the live values that ARE meaningful",
              Drew(cp, a.Velocity) && Drew(cp, a.Altitude), "");
        // The reference export's own baked strings must never come back.
        string[] cgone = { "7.67 km/s", "406.4 km", "428.9 km", "380.7 km", "51.64°",
                           "T-01:08:36", "Deorbit Coast" };
        for (int i = 0; i < cgone.Length; i++)
            Check("chute strip no longer hard-codes " + cgone[i], !Drew(ca, cgone[i]), "");
        // The PROCEDURE COPY beneath it is reference text, not a value - it must be untouched.
        Check("chute procedure copy is untouched",
              Drew(ca, "ENABLE BACKUP PYROS") && Drew(ca, "DEPLOY MAINS"), "");

        // ---------------- MANUAL DOCKING: the axis readouts, PYR, RANGE, RATE ----------------
        PageState noTgt = ProcFixture(0); noTgt.HasTarget = false; noTgt.HasTargetOrbit = false;
        DisplayList da = new DisplayList(DockingSimPage.Commands + 60);
        DisplayList db = new DisplayList(DockingSimPage.Commands + 60);
        DisplayList dn = new DisplayList(DockingSimPage.Commands + 60);
        DockingSimPage.Build(da, VW, VH, a);
        DockingSimPage.Build(db, VW, VH, b);
        DockingSimPage.Build(dn, VW, VH, noTgt);

        string[] dock = { a.RollDegText, a.PitchDegText, a.YawDegText, a.RangeText, a.RateText,
                          a.PitchRateText, a.YawRateText, a.RollRateText };
        for (int i = 0; i < dock.Length; i++)
        {
            Check("docking draws PageState value " + dock[i], Drew(da, dock[i]), "");
            Check("docking value " + dock[i] + " is not a constant", !Drew(db, dock[i]), "");
            Check("docking drops " + dock[i] + " with no target", !Drew(dn, dock[i]), "");
        }
        Check("docking dashes with no target", Drew(dn, "—"), "");
        // S26: the ring readouts (the correction) and the PYR block (now the rate, not an echo of the
        // correction) are TWO DIFFERENT quantities, so each string appears exactly ONCE - the "one datum
        // drawn twice" failure this task fixed would show up here as a 2.
        Check("docking draws the ring correction once, not echoed in PYR",
              Times(da, a.RollDegText) == 1 && Times(da, a.PitchDegText) == 1 &&
              Times(da, a.YawDegText) == 1,
              "roll " + Times(da, a.RollDegText) + " pitch " + Times(da, a.PitchDegText) +
              " yaw " + Times(da, a.YawDegText));
        Check("docking draws the PYR rate once, not the correction value",
              Times(da, a.PitchRateText) == 1 && Times(da, a.YawRateText) == 1 &&
              Times(da, a.RollRateText) == 1,
              "pitch " + Times(da, a.PitchRateText) + " yaw " + Times(da, a.YawRateText) +
              " roll " + Times(da, a.RollRateText));
        string[] dgone = { "0.0°", "180.0", "11.6 m", "-0.2 m/s" };
        for (int i = 0; i < dgone.Length; i++)
            Check("docking no longer hard-codes " + dgone[i], !Drew(da, dgone[i]), "");

        // ---------------- MANUAL DOCKING: the target diamond (S26) ----------------
        // The diamond is the only Line command this page draws in Go - the graticule ticks are Faint and
        // every button is a Box, so counting/inspecting Go lines isolates it cleanly.
        var diamondNoTgt = Lines(dn, DragonPalette.Go);
        Check("docking diamond is hidden with no target", diamondNoTgt.Count == 0,
              "drew " + diamondNoTgt.Count + " green line(s) with no target");
        var diamondA = Lines(da, DragonPalette.Go);
        var diamondB = Lines(db, DragonPalette.Go);
        Check("docking diamond is drawn with a target", diamondA.Count == 4,
              "drew " + diamondA.Count + " green line(s)");
        Check("docking diamond moves with the pitch/yaw bearings",
              diamondA.Count == 4 && diamondB.Count == 4 &&
              (diamondA[0].A != diamondB[0].A || diamondA[0].B != diamondB[0].B),
              "fixture A and B (different YawDeg/PitchDeg) drew the diamond at the same spot");

        // ---------------- MANUAL DOCKING: readouts go GREEN when corrected (S26) ----------------
        PageState corrected = ProcFixture(0);
        corrected.RollDeg = 0.1; corrected.RollDegText = "0.1°";     // within CorrectedToleranceDeg
        corrected.YawDeg = -0.2; corrected.YawDegText = "-0.2°";     // within CorrectedToleranceDeg
        corrected.PitchDeg = 5.0; corrected.PitchDegText = "5.0°";   // NOT corrected
        DisplayList dc = new DisplayList(DockingSimPage.Commands + 60);
        DockingSimPage.Build(dc, VW, VH, corrected);
        Check("docking axis reads GREEN when corrected",
              SameColour(ColourOf(dc, corrected.RollDegText), DragonPalette.Go) &&
              SameColour(ColourOf(dc, corrected.YawDegText), DragonPalette.Go), "");
        Check("docking axis reads WHITE with a target but not yet corrected",
              SameColour(ColourOf(dc, corrected.PitchDegText), DragonPalette.White), "");

        // ---------------- SUIT LEAK CHECK: the four delta pressures follow the CABIN ----------------
        // S31 / §14.4(e). These were dashed (T13c: nothing modelled a suit); they are now a marked
        // simulation measured against the real cabin pressure, so the same A/B shape applies to them as
        // to every other live readout - build with two different cabins and assert all four moved.
        SuitCheckState sca = SuitLeak.From(a, 5, false, 0u), scb = SuitLeak.From(b, 5, false, 0u);
        DisplayList sa = new DisplayList(SuitCheckPage.Commands + 60);
        DisplayList sb = new DisplayList(SuitCheckPage.Commands + 60);
        DisplayList sd = new DisplayList(SuitCheckPage.Commands + 60);
        SuitCheckPage.Build(sa, VW, VH, 5, false, sca);
        SuitCheckPage.Build(sb, VW, VH, 5, false, scb);
        SuitCheckPage.Build(sd, VW, VH, 5, false, SuitLeak.From(dead, 5, false, 0u));
        for (int i = 0; i < 4; i++)
        {
            string va = SuitLeak.Text(sca.Delta(i));
            Check("suit " + (i + 1) + " delta pressure is live", Drew(sa, va), va);
            Check("suit " + (i + 1) + " delta pressure moved with the cabin", !Drew(sb, va), va);
            Check("suit " + (i + 1) + " reads its own differential",
                  Times(sa, va) == 1, "drew " + va + " " + Times(sa, va) + " times");
        }
        Check("suit check no longer hard-codes 0.01psi", !Drew(sa, "0.01psi"), "");
        // The inverse, and the one that matters most on this page: with NO FEED there is no cabin to
        // measure against, so there is no differential AND no verdict - not a confident green word.
        Check("suit check dashes the whole table on a dead feed",
              Drew(sd, "—") && !Drew(sd, "Nominal") && !Drew(sd, "Failed Low"), "");
        // The row LABELS and the procedure countdown are the page's own copy - still drawn.
        Check("suit check keeps its reference copy",
              Drew(sa, "SUIT 1 DELTA PRESSURE") && Drew(sa, "SUIT 1 STATUS") && Drew(sa, "5s"), "");

        // ---------------- DEORBIT BURN PREP: the four SLEW rows stay dashed (T13c's call) ----------
        DisplayList pa = new DisplayList(DeorbitBurnPrepPage.Commands + 60);
        DeorbitBurnPrepPage.Build(pa, VW, VH, a);
        Check("deorbit prep dashes the four slew rows", Drew(pa, "—"), "");
        // The inverse, and the point of the check: NOTHING in the fixture may appear on those rows. A
        // later pass that "improves" them by reaching for the nearest plausible number fails here.
        string[] notSlew = { a.RollDegText, a.PitchDegText, a.YawDegText, a.InclinationDegText,
                             a.RangeText, a.RateText, a.Velocity, a.Altitude };
        for (int i = 0; i < notSlew.Length; i++)
            Check("deorbit prep invents no slew value from " + notSlew[i], !Drew(pa, notSlew[i]), "");
        // FC SLEW underneath them IS live, and stays so.
        PageState eng = ProcFixture(0); eng.DeorbitEngaged = true;
        DisplayList pe = new DisplayList(DeorbitBurnPrepPage.Commands + 60);
        DeorbitBurnPrepPage.Build(pe, VW, VH, eng);
        Check("deorbit prep FC SLEW reads the Part B seam",
              Drew(pa, "NOT ENGAGED") && Drew(pe, "ENGAGED") && !Drew(pe, "NOT ENGAGED"), "");

        // ---------------- RENDEZVOUS PLOT: the approach chord runs to the TARGET ----------------
        DisplayList ra = new DisplayList(RendezvousPage.Commands + 60);
        DisplayList rb = new DisplayList(RendezvousPage.Commands + 60);
        RendezvousPage.Build(ra, VW, VH, a);
        RendezvousPage.Build(rb, VW, VH, b);
        var la = Lines(ra, DragonPalette.Caution);
        var lb = Lines(rb, DragonPalette.Caution);
        Check("rendezvous draws the chord and its endpoint marker", la.Count == 5, "got " + la.Count);
        // The chord ENDS where the target is, so a different phase angle has to move that end. This is
        // the "not a constant" proof for a line rather than for a string.
        Check("chord endpoint follows the target",
              lb.Count == 5 && la.Count == 5 &&
              (Math.Abs(la[0].C - lb[0].C) > 1f || Math.Abs(la[0].D - lb[0].D) > 1f),
              "a " + la.Count + " b " + lb.Count);
        // T6 ran the chord to PERIAPSIS as a stated stand-in. It must not do that any more: with a
        // target whose orbit is not around our body there is no honest endpoint, so there is NO chord.
        PageState noOrbit = ProcFixture(0); noOrbit.HasTargetOrbit = false;
        PageState noneAtAll = ProcFixture(0); noneAtAll.HasTarget = false; noneAtAll.HasTargetOrbit = false;
        DisplayList rn = new DisplayList(RendezvousPage.Commands + 60);
        DisplayList rz = new DisplayList(RendezvousPage.Commands + 60);
        RendezvousPage.Build(rn, VW, VH, noOrbit);
        RendezvousPage.Build(rz, VW, VH, noneAtAll);
        Check("no chord to periapsis when the target has no comparable orbit",
              Lines(rn, DragonPalette.Caution).Count == 0,
              "got " + Lines(rn, DragonPalette.Caution).Count);
        Check("no chord with no target at all",
              Lines(rz, DragonPalette.Caution).Count == 0,
              "got " + Lines(rz, DragonPalette.Caution).Count);
        // The plain NAV page never grows one: its overload passes the chord flag false.
        DisplayList nav = new DisplayList(400);
        NavPage.Orbit(nav, a, 0f, 0f, VW * 0.5f, VH * 0.5f);
        Check("the plain NAV orbit view draws no chord",
              Lines(nav, DragonPalette.Caution).Count == 0,
              "got " + Lines(nav, DragonPalette.Caution).Count);
    }

    static void SpeccedPages()
    {
        // Manual Chute Deploy + Manual Docking: the two pages reached outside the bottom bar.
        float sc = (float)H / RefH;
        float[] slotY = { 253f, 421f, 589f, 757f, 925f, 1093f, 1261f };
        float railX = 110f * sc;

        // Cover: the "Manual Chute" rail item (row 6) is a real page — it navigates.
        NavHit a = FigmaUI.HitTest(UiPage.Cover, railX, (slotY[6] + 80f) * sc, W, H);
        Check("cover Manual Chute rail -> ManualChute page",
              a.Act == NavAct.Goto && a.Target == UiPage.ManualChute, "got " + a.Act + " " + a.Target);
        // Cover: any other rail item stays in-page (phase select, not nav).
        NavHit b = FigmaUI.HitTest(UiPage.Cover, railX, (slotY[1] + 80f) * sc, W, H);
        Check("cover Coast rail stays in-page", b.Act == NavAct.None, "got " + b.Act);

        // ManualChute page: a non-manual rail item returns to the Cover; its own item is inert.
        NavHit c = FigmaUI.HitTest(UiPage.ManualChute, railX, (slotY[1] + 80f) * sc, W, H);
        Check("ManualChute other rail -> Cover", c.Act == NavAct.Goto && c.Target == UiPage.Cover, "got " + c.Act + " " + c.Target);
        NavHit d = FigmaUI.HitTest(UiPage.ManualChute, railX, (slotY[6] + 80f) * sc, W, H);
        Check("ManualChute self rail stays", d.Act == NavAct.None, "got " + d.Act);

        // Attitude HUD: the letterbox-margin affordance opens the Docking screen.
        NavHit e = FigmaUI.HitTest(UiPage.Hud, 30f, 0.5f * H, W, H);
        Check("HUD margin -> Docking", e.Act == NavAct.Goto && e.Target == UiPage.Docking, "got " + e.Act + " " + e.Target);

        // Both new pages carry the global bottom bar, so the Cover icon returns from either.
        float bcx = (46f + 40f) / RefW * W, bcy = (2003f + 40f) * sc;
        Check("ManualChute bottom-bar -> Cover", FigmaUI.HitTest(UiPage.ManualChute, bcx, bcy, W, H).Target == UiPage.Cover, "");
        Check("Docking bottom-bar -> Cover", FigmaUI.HitTest(UiPage.Docking, bcx, bcy, W, H).Target == UiPage.Cover, "");
    }

    static void CoverCamera()
    {
        // T4. The Cover's right-hand slot is the reference UI's own three-view camera (First.vue's
        // swapComponent). None of this shows up in a PNG: that the cycle wraps, that the pan cluster
        // exists ONLY on the map view, and that neither control navigates off the Cover are all
        // arithmetic on the hit rects — the kind of thing otherwise found in the capsule, at the cost
        // of a restart.
        float sc = (float)H / RefH;
        float extra = W - RefW * sc; if (extra < 0f) extra = 0f;

        Check("cover camera has 3 views", CoverPage.CamCount == 3, "got " + CoverPage.CamCount);

        // swapComponent(): count = (count + 1) % 3.
        CoverPage.CoverCam c = CoverPage.CoverCam.Earth;
        c = CoverPage.NextCam(c);
        Check("NEXT VIEW Earth -> Map", c == CoverPage.CoverCam.Map, "got " + c);
        c = CoverPage.NextCam(c);
        Check("NEXT VIEW Map -> Capsule", c == CoverPage.CoverCam.Capsule, "got " + c);
        c = CoverPage.NextCam(c);
        Check("NEXT VIEW Capsule -> Earth (wraps)", c == CoverPage.CoverCam.Earth, "got " + c);

        // The headings are First.vue's own viewHeading strings, verbatim.
        Check("Earth heading", CoverPage.CamHeading(CoverPage.CoverCam.Earth) == "Auto - Earth IO",
              CoverPage.CamHeading(CoverPage.CoverCam.Earth));
        Check("Map heading", CoverPage.CamHeading(CoverPage.CoverCam.Map) == "Auto - Map IO",
              CoverPage.CamHeading(CoverPage.CoverCam.Map));
        Check("Capsule heading", CoverPage.CamHeading(CoverPage.CoverCam.Capsule) == "Auto - Capsule IO",
              CoverPage.CamHeading(CoverPage.CoverCam.Capsule));

        // The MapView mode each view needs, so pan/zoom/centre mean the right thing (MapProjection
        // branches on it): the flat map pans in lat/lon, the other two spin the globe.
        Check("Map view -> NavMode.Map", CoverPage.CamMapMode(CoverPage.CoverCam.Map) == NavMode.Map, "");
        Check("Earth view -> NavMode.Planet",
              CoverPage.CamMapMode(CoverPage.CoverCam.Earth) == NavMode.Planet, "");
        Check("Capsule view -> NavMode.Planet",
              CoverPage.CamMapMode(CoverPage.CoverCam.Capsule) == NavMode.Planet, "");
        Check("WithMode sets the mode without touching the pan",
              MapProjection.WithMode(MapProjection.Default(), NavMode.Planet).Mode == NavMode.Planet, "");

        CoverPage.CoverCam[] all = {
            CoverPage.CoverCam.Earth, CoverPage.CoverCam.Map, CoverPage.CoverCam.Capsule };

        // NEXT VIEW is on EVERY view — it is the only way out of one, so it must never be shadowed.
        float nx, ny, nw, nh;
        CoverPage.NextViewRect(W, H, out nx, out ny, out nw, out nh);
        float ncx = nx + nw * 0.5f, ncy = ny + nh * 0.5f;
        for (int i = 0; i < all.Length; i++)
            Check("NEXT VIEW hits on the " + all[i] + " view",
                  CoverPage.HitTest(ncx, ncy, W, H, all[i]) == CoverPage.CoverButton.NextView,
                  "got " + CoverPage.HitTest(ncx, ncy, W, H, all[i]));

        // ...and it is NOT navigation: the camera changes in-page, like the phase rail.
        NavHit nv = FigmaUI.HitTest(UiPage.Cover, ncx, ncy, W, H);
        Check("NEXT VIEW does not navigate", nv.Act == NavAct.None, "got " + nv.Act);

        // The cluster: every button hits its own action on the MAP view, and NOTHING on the other two
        // (it is not drawn there, so it must not be touchable there either).
        float[,] pad = { { 0f, 0f }, { 0f, -1f }, { 0f, 1f }, { -1f, 0f }, { 1f, 0f }, { -0.5f, 2f }, { 0.5f, 2f } };
        CoverPage.CoverButton[] want = {
            CoverPage.CoverButton.MapCentre, CoverPage.CoverButton.MapPanUp,
            CoverPage.CoverButton.MapPanDown, CoverPage.CoverButton.MapPanLeft,
            CoverPage.CoverButton.MapPanRight, CoverPage.CoverButton.MapZoomIn,
            CoverPage.CoverButton.MapZoomOut };

        for (int i = 0; i < want.Length; i++)
        {
            float bx, by, bw, bh;
            CoverPage.PadRect(W, H, pad[i, 0], pad[i, 1], out bx, out by, out bw, out bh);
            float cx = bx + bw * 0.5f, cy = by + bh * 0.5f;

            CoverPage.CoverButton got = CoverPage.HitTest(cx, cy, W, H, CoverPage.CoverCam.Map);
            Check("map cluster " + want[i] + " hits", got == want[i], "got " + got);
            Check("map cluster " + want[i] + " is inert on Earth",
                  CoverPage.HitTest(cx, cy, W, H, CoverPage.CoverCam.Earth) == CoverPage.CoverButton.None,
                  "got " + CoverPage.HitTest(cx, cy, W, H, CoverPage.CoverCam.Earth));
            Check("map cluster " + want[i] + " is inert on Capsule",
                  CoverPage.HitTest(cx, cy, W, H, CoverPage.CoverCam.Capsule) == CoverPage.CoverButton.None,
                  "got " + CoverPage.HitTest(cx, cy, W, H, CoverPage.CoverCam.Capsule));
            Check("map cluster " + want[i] + " does not navigate",
                  FigmaUI.HitTest(UiPage.Cover, cx, cy, W, H).Act == NavAct.None, "");
        }

        // The cluster is drawn OVER the map, so every button must actually be on it.
        {
            float mx, my, mw, mh;
            CoverPage.MapRect(W, H, out mx, out my, out mw, out mh);
            Check("map rect is the 2:1 equirectangular aspect (zoom 0 fills it)",
                  Math.Abs(mw - mh * 2f) < 1.5f, "got " + mw + " x " + mh);
            for (int i = 0; i < want.Length; i++)
            {
                float bx, by, bw, bh;
                CoverPage.PadRect(W, H, pad[i, 0], pad[i, 1], out bx, out by, out bw, out bh);
                Check("map cluster " + want[i] + " sits inside the map",
                      bx >= mx && by >= my && bx + bw <= mx + mw && by + bh <= my + mh,
                      "button " + bx + "," + by + " " + bw + "x" + bh
                      + "  map " + mx + "," + my + " " + mw + "x" + mh);
            }
        }

        // Regression: the new controls must not have eaten the ones that shared their rows. SETTINGS is
        // the NEXT VIEW pill's twin at the other end of the same row; the rail is the whole left strip.
        Check("cover Settings still hits with the camera controls in",
              CoverPage.HitTest(3194f * sc + extra, 1865f * sc, W, H, CoverPage.CoverCam.Map)
                  == CoverPage.CoverButton.Settings, "");
        Check("cover rail still hits with the camera controls in",
              CoverPage.HitTest(110f * sc, (253f + 80f) * sc, W, H, CoverPage.CoverCam.Map)
                  == CoverPage.CoverButton.PhaseDeport, "");
    }

    static void CoverPhases()
    {
        // The Cover's left rail selects one of the SEVEN deorbit phases in-page. Aim at each rail row's
        // centre; assert HitTest returns that phase button and PhaseOf maps it back to the right index.
        // SlotY mirrors CoverPage.SlotY (a layout decision — update here if the rail is re-pitched).
        float sc = (float)H / RefH;
        float[] slotY = { 253f, 421f, 589f, 757f, 925f, 1093f, 1261f };
        CoverPage.CoverButton[] want = {
            CoverPage.CoverButton.PhaseDeport, CoverPage.CoverButton.PhaseCoast,
            CoverPage.CoverButton.PhaseClaw, CoverPage.CoverButton.PhaseProcedure,
            CoverPage.CoverButton.PhaseProcedure2, CoverPage.CoverButton.PhaseReference,
            CoverPage.CoverButton.PhaseManual };

        Check("cover has 7 phases", CoverPage.PhaseCount == 7, "got " + CoverPage.PhaseCount);
        for (int i = 0; i < CoverPage.PhaseCount; i++)
        {
            float px = 110f * sc;                 // rail strip centre (design x < Split → plain *sc)
            float py = (slotY[i] + 80f) * sc;     // inside the row's band
            CoverPage.CoverButton b = CoverPage.HitTest(px, py, W, H);
            Check("cover rail row " + i + " hits its phase", b == want[i], "got " + b);
            Check("cover rail row " + i + " PhaseOf round-trips", CoverPage.PhaseOf(b) == i,
                  "got " + CoverPage.PhaseOf(b));
        }

        // A non-phase button is not a rail index; Menu/Settings still resolve after the rail rework.
        Check("PhaseOf(Menu) is -1", CoverPage.PhaseOf(CoverPage.CoverButton.Menu) == -1, "");
        Check("cover Menu still hits", CoverPage.HitTest(98f * sc, 108f * sc, W, H) == CoverPage.CoverButton.Menu, "");
        Check("cover Settings still hits",
              CoverPage.HitTest(3194f * sc + (W - RefW * sc), 1865f * sc, W, H) == CoverPage.CoverButton.Settings, "");
    }

    static void VehicleTabs()
    {
        // The Vehicle page's eight subsystem sub-tabs (VehicleTabBar) each route to a sibling vehicle
        // page. Aim at each tab's drawn centre; assert HitTest agrees. Order must match VehicleTabBar.Tabs.
        UiPage[] want = {
            UiPage.Vehicle, UiPage.VehicleCrew, UiPage.VehiclePropulsion, UiPage.VehicleMech,
            UiPage.VehiclePower, UiPage.VehicleAvionics, UiPage.VehicleGnc, UiPage.VehicleThermal };

        for (int i = 0; i < VehicleTabBar.Tabs.Length; i++)
        {
            float cx = VehicleTabBar.CentreX(i) / RefW * W;
            float cy = 1815f / RefH * H;   // inside the tab band (label ≈1812)

            Check("veh tab " + i + " hittable", VehicleTabBar.HitTest(cx, cy, W, H) == i,
                  "got " + VehicleTabBar.HitTest(cx, cy, W, H));

            // Routes from the All page and from another subsystem page (proves it works on every sibling).
            NavHit fromAll = FigmaUI.HitTest(UiPage.Vehicle, cx, cy, W, H);
            Check("veh tab " + i + " routes (from All)",
                  fromAll.Act == NavAct.Goto && fromAll.Target == want[i],
                  "act " + fromAll.Act + " tgt " + fromAll.Target);

            NavHit fromThermal = FigmaUI.HitTest(UiPage.VehicleThermal, cx, cy, W, H);
            Check("veh tab " + i + " routes (from a subsystem)",
                  fromThermal.Act == NavAct.Goto && fromThermal.Target == want[i],
                  "act " + fromThermal.Act + " tgt " + fromThermal.Target);
        }

        // Neighbours must not share a hit region.
        for (int i = 0; i + 1 < VehicleTabBar.Tabs.Length; i++)
        {
            float edge = (VehicleTabBar.CentreX(i) + VehicleTabBar.CentreX(i + 1)) * 0.5f / RefW * W;
            float cy = 1815f / RefH * H;
            int a = VehicleTabBar.HitTest(edge - 2f, cy, W, H);
            int b = VehicleTabBar.HitTest(edge + 2f, cy, W, H);
            Check("veh tabs " + i + "/" + (i + 1) + " split at the boundary", a == i && b == i + 1,
                  "got " + a + "/" + b);
        }

        // The strip is inert on a non-vehicle page (a tap there is not a tab).
        float tx = VehicleTabBar.CentreX(2) / RefW * W, ty = 1815f / RefH * H;
        Check("veh tab strip inert off-vehicle",
              FigmaUI.HitTest(UiPage.Hud, tx, ty, W, H).Act == NavAct.None, "");
    }

    static void VehicleDeepViewLinksTest()
    {
        // S27: an our-geometry affordance (VehicleDeepViewLinks), same footing as T5's FUNCTIONS|ALERTS
        // toggle and T6's Docking->Rendezvous affordance, reaching the two vehicle systems deep-views
        // (T9) from every Vehicle-family page — because no source assigns either a real Cover rail
        // "Procedure" slot to them (the option NOT taken, see the register).
        UiPage[] vehiclePages = {
            UiPage.Vehicle, UiPage.VehicleMech, UiPage.VehicleCrew, UiPage.VehiclePropulsion,
            UiPage.VehiclePower, UiPage.VehicleAvionics, UiPage.VehicleGnc, UiPage.VehicleThermal };
        UiPage[] want = { UiPage.SystemsTree, UiPage.SystemsPid };

        float cy = 1815f / RefH * H;   // inside the link band, same row as the tab strip

        foreach (UiPage vp in vehiclePages)
        {
            for (int i = 0; i < want.Length; i++)
            {
                // Aim at the same centre Draw uses (link left edge + half its width) — one rect, shared.
                float cx = (2650f + 155f + i * 310f) / RefW * W;   // X[i] + LinkW*0.5 for i=0,1
                NavHit hit = FigmaUI.HitTest(vp, cx, cy, W, H);
                Check(vp + " deep-view link " + i + " (" + want[i] + ") routes",
                      hit.Act == NavAct.Goto && hit.Target == want[i],
                      "got " + hit.Act + " " + hit.Target);
            }
        }

        // The two links must not share a hit region with each other or with the real tab strip's own
        // rightmost tab (Thermal, index 7) — geometry that would let one touch resolve two ways.
        float thermalCx = VehicleTabBar.CentreX(7) / RefW * W;
        NavHit thermal = FigmaUI.HitTest(UiPage.Vehicle, thermalCx, cy, W, H);
        Check("thermal tab still routes to VehicleThermal (no link overlap)",
              thermal.Act == NavAct.Goto && thermal.Target == UiPage.VehicleThermal,
              "got " + thermal.Act + " " + thermal.Target);
        float gapCx = ((2650f + 260f) + 2960f) * 0.5f / RefW * W;   // midpoint of the gap between links
        Check("gap between the two links is inert",
              FigmaUI.HitTest(UiPage.Vehicle, gapCx, cy, W, H).Act == NavAct.None, "");

        // Inert on a non-vehicle page — this is a Vehicle-family affordance, not a global one.
        Check("deep-view links inert off-vehicle",
              FigmaUI.HitTest(UiPage.Hud, (2650f + 155f) / RefW * W, cy, W, H).Act == NavAct.None, "");

        // Both destinations are real pages a crew member can actually land on.
        Check("SystemsTree is a real page, not a placeholder", !FigmaUI.IsPlaceholder(UiPage.SystemsTree), "");
        Check("SystemsPid is a real page, not a placeholder", !FigmaUI.IsPlaceholder(UiPage.SystemsPid), "");
    }

    static void SuitCheck()
    {
        // Aim at the centre of each control's drawn rect; assert HitTest returns its action.
        float sx = (float)W / 3427f, sy = (float)H / 2112f;
        float PX(float x) => x * sx;
        float PY(float y) => y * sy;

        // START @ (2300,400,470,120), HALT @ (2900,1600,470,120) — live only when the popup is down.
        Check("suit START hits Start",
              SuitCheckPage.HitTest(PX(2535f), PY(460f), W, H, false) == SuitCheckPage.SuitAct.Start, "");
        Check("suit HALT hits Halt",
              SuitCheckPage.HitTest(PX(3135f), PY(1660f), W, H, false) == SuitCheckPage.SuitAct.Halt, "");

        // With the popup up, only CLOSE is live; START behind the scrim does nothing.
        const float ph = 1040f, py = (2112f - ph) * 0.5f - 40f, cx = 3427f * 0.5f;
        Check("popup CLOSE hits Close",
              SuitCheckPage.HitTest(PX(cx), PY(py + ph - 155f), W, H, true) == SuitCheckPage.SuitAct.Close, "");
        Check("popup swallows START",
              SuitCheckPage.HitTest(PX(2535f), PY(460f), W, H, true) == SuitCheckPage.SuitAct.None, "");
        // Empty space is inert.
        Check("suit empty misses",
              SuitCheckPage.HitTest(PX(1500f), PY(1000f), W, H, false) == SuitCheckPage.SuitAct.None, "");
    }

    // ============================================================================================
    // S31 / BUILD_PLAN §14.4(e) - THE SUIT LEAK CHECK'S SIMULATION AND ITS TWO OUTCOMES.
    //
    // Three things have to be provable here, and none of them can be proved from a preview PNG:
    //   1. the 5% roll is INJECTABLE - a seed decides, so BOTH branches are reachable from a test
    //      rather than one of them being a thing that happens to a player twenty runs from now;
    //   2. the STATUS words are a VERDICT ON THE SIM, never a green word printed anyway (§14.4(e)'s
    //      guardrail, and the whole reason S31 exists);
    //   3. a leaking suit actually BLEEDS DOWN through the countdown, so step 2.4's "monitor suit
    //      delta pressure" describes something that happens.
    // ============================================================================================
    static void SuitLeakSimulation()
    {
        const int VW = 2560, VH = 1406;
        PageState s = new PageState();
        s.Valid = true;
        s.Cabin.PressPsia = 14.70;

        // ---- the roll is a function of its seed, and both outcomes are reachable ----
        uint clean = 0;
        for (uint k = 1; k < 1000 && clean == 0; k++) if (SuitLeak.LeakingSuit(k) == 0) clean = k;
        uint leaky = SuitLeak.SeedForLeak(3);
        Check("a seed that finds no leak exists", clean != 0, "");
        Check("a seed that finds a leak in suit 3 exists",
              leaky != 0 && SuitLeak.LeakingSuit(leaky) == 3, "seed " + leaky);
        Check("the roll is stable for one run", SuitLeak.LeakingSuit(leaky) == SuitLeak.LeakingSuit(leaky), "");
        Check("no run has found nothing", SuitLeak.LeakingSuit(0) == 0, "");
        Check("a fresh seed is never the no-run seed",
              SuitLeak.SeedFrom(0.0, 0) != 0u && SuitLeak.SeedFrom(1234.5, 7) != 0u, "");
        Check("re-running re-rolls", SuitLeak.SeedFrom(1234.5, 7) != SuitLeak.SeedFrom(1234.5, 8), "");

        // ---- and it is the 5% the owner asked for, not "sometimes" ----
        int hits = 0; const int N = 40000;
        bool[] seen = new bool[5];
        for (uint k = 1; k <= N; k++)
        {
            int su = SuitLeak.LeakingSuit(k);
            if (su != 0) { hits++; seen[su] = true; }
        }
        double rate = hits / (double)N;
        Check("the leak roll lands near 5%", rate > 0.04 && rate < 0.06, "got " + rate.ToString("F4"));
        Check("any of the four suits can be the leaking one",
              seen[1] && seen[2] && seen[3] && seen[4], "");

        // ---- A CLEAN RUN: four verdicts the model justifies, and the completion box ----
        SuitCheckState ok = SuitLeak.From(s, 0, true, clean);
        DisplayList dok = new DisplayList(SuitCheckPage.Commands + 60);
        SuitCheckPage.Build(dok, VW, VH, 0, true, ok);
        Check("a clean run finds no leak", !ok.Leak && ok.LeakSuit == 0, "");
        Check("a clean run holds all four suits",
              !ok.Failed(0) && !ok.Failed(1) && !ok.Failed(2) && !ok.Failed(3), "");
        Check("a clean run reads Nominal exactly four times",
              Times(dok, "Nominal") == 4, "got " + Times(dok, "Nominal"));
        Check("a clean run raises the completion box",
              Drew(dok, "PROCEDURE COMPLETE") && !Drew(dok, "Repair suit and rerun suit check."), "");

        // ---- A LEAK RUN: the verdict FOLLOWS the sim, and the repair box replaces the other one ----
        SuitCheckState bad = SuitLeak.From(s, 0, true, leaky);
        DisplayList dbad = new DisplayList(SuitCheckPage.Commands + 60);
        SuitCheckPage.Build(dbad, VW, VH, 0, true, bad);
        Check("the leaking suit fell below the pass threshold",
              bad.Failed(2) && bad.Delta(2) < SuitLeak.PassPsi, "delta " + bad.Delta(2).ToString("F3"));
        Check("the other three still hold",
              !bad.Failed(0) && !bad.Failed(1) && !bad.Failed(3), "");
        // THE S31 CHECK. A page that hard-codes "Nominal" draws it four times whatever the model says;
        // this run must draw it three times and name the fourth suit's failure.
        Check("STATUS is a verdict, not a word",
              Times(dbad, "Nominal") == 3 && Times(dbad, "Failed Low") == 1,
              "nominal " + Times(dbad, "Nominal") + " failed " + Times(dbad, "Failed Low"));
        Check("the failed suit's differential is on the page",
              Drew(dbad, SuitLeak.Text(bad.Delta(2))), SuitLeak.Text(bad.Delta(2)));
        Check("a leak run raises the repair-and-rerun box",
              Drew(dbad, "Repair suit and rerun suit check.") && !Drew(dbad, "PROCEDURE COMPLETE"), "");
        Check("the leak box names the suit that failed",
              Drew(dbad, "Suit 3 did not hold pressure.") && Drew(dbad, "SUIT LEAK DETECTED"), "");
        // Same box, same close control: the two outcomes must not become two dialogs.
        Check("both outcomes use the one box",
              Drew(dok, "4.011 - Suit Leak Check") && Drew(dbad, "4.011 - Suit Leak Check"), "");

        // ---- the leak BLEEDS DOWN through the countdown rather than snapping at the end ----
        double t5 = SuitLeak.From(s, 5, false, leaky).Delta(2);
        double t3 = SuitLeak.From(s, 3, false, leaky).Delta(2);
        double t0 = SuitLeak.From(s, 0, false, leaky).Delta(2);
        Check("a leaking suit bleeds down while the timer runs",
              t5 > t3 && t3 > t0 && t0 <= bad.Delta(2) + 1e-9,
              t5.ToString("F3") + " -> " + t3.ToString("F3") + " -> " + t0.ToString("F3"));
        Check("it still reads Nominal before it has bled",
              !SuitLeak.From(s, 5, false, leaky).Failed(2), "");

        // ---- an idle page has made no run, so it has found nothing ----
        SuitCheckState idle = SuitLeak.From(s, 5, false, 0u);
        Check("an idle page reports no leak", !idle.Leak, "");
        Check("an idle page still shows live differentials",
              idle.Valid && idle.Delta(0) > 0.0, "");

        // ---- no feed, no verdict ----
        PageState nofeed = new PageState(); nofeed.Valid = false;
        SuitCheckState none = SuitLeak.From(nofeed, 0, true, leaky);
        Check("no feed = no reading and no verdict",
              !none.Valid && !none.Failed(0) && !none.Failed(2), "");
        // A half-built feed (Valid true, cabin never filled) must not produce a 15 psi differential and
        // a confident Nominal beside it.
        PageState halfBuilt = new PageState(); halfBuilt.Valid = true;
        Check("an unfilled cabin is not a cabin", !SuitLeak.From(halfBuilt, 0, true, 0u).Valid, "");

        // ---- S32: TROUBLESHOOT answers the verdict, and only the verdict ----
        // The control's action is a MARKED reconstruction-from-function (owner, via the overseer,
        // 2026-09-02; §14.4(d)+(e)) - repair the failed suit and re-run the check, which is what the
        // result box already tells the crew to do. What has to hold is that it responds to a real
        // failure and to nothing else: a control that lights, or acts, over four holding suits would be
        // the same "screen asserting something the model does not say" S31 just removed from this page.
        Check("the fail branch has an action at all (S32)", SuitCheckPage.FailBranchLive, "");
        Check("a leak run makes TROUBLESHOOT available",
              bad.AnyFailed && SuitCheckPage.Available(SuitCheckPage.SuitAct.Troubleshoot, bad), "");
        Check("a clean run leaves TROUBLESHOOT inert",
              !ok.AnyFailed && !SuitCheckPage.Available(SuitCheckPage.SuitAct.Troubleshoot, ok), "");
        Check("no feed leaves TROUBLESHOOT inert",
              !SuitCheckPage.Available(SuitCheckPage.SuitAct.Troubleshoot, none), "");

        // The LIT control and the LIVE control are one control: the page paints it from the same
        // Available() the glue gates the press on, so these two colours are the visible half of the
        // assertions above rather than a second opinion about them.
        DisplayList dtable = new DisplayList(SuitCheckPage.Commands + 60);
        SuitCheckPage.Build(dtable, VW, VH, 0, false, bad);          // the box closed: what the crew acts in
        Check("the failed table draws TROUBLESHOOT lit",
              SameColour(ColourOf(dtable, "TROUBLESHOOT"), DragonPalette.White),
              "got " + ColourOf(dtable, "TROUBLESHOOT").R.ToString("F2"));
        Check("a clean page draws TROUBLESHOOT dimmed",
              SameColour(ColourOf(dok, "TROUBLESHOOT"), DragonPalette.Text6), "");
        Check("the failed table still reads its verdict with the box closed",
              Times(dtable, "Failed Low") == 1 && Times(dtable, "Nominal") == 3, "");

        // PRESSING IT: repair + re-run. The press mints a fresh run seed and puts the countdown back to
        // the top - the same state change TRY ADDITIONAL TIMER makes - so the failure the crew was
        // looking at is gone and the NEXT verdict is rolled rather than declared clean.
        uint reseed = SuitLeak.SeedFrom(1234.5, 2);
        Check("a repair mints a different run", reseed != 0u && reseed != leaky, "seed " + reseed);
        SuitCheckState after = SuitLeak.From(s, 5, false, reseed);
        Check("the repaired suit is holding again at the top of the new run",
              after.Valid && !after.AnyFailed && !after.Failed(2), "");
        Check("and the control goes back to inert until the new run finds something",
              !SuitCheckPage.Available(SuitCheckPage.SuitAct.Troubleshoot, after), "");
        // ...and a re-run that DOES find one lights it again, so the recovery is repeatable rather than
        // a one-shot that silently stops working the second time a suit fails.
        SuitCheckState again = SuitLeak.From(s, 0, true, SuitLeak.SeedForLeak(1));
        Check("a re-run that finds a leak lights it again",
              again.Failed(0) && SuitCheckPage.Available(SuitCheckPage.SuitAct.Troubleshoot, again), "");
    }

    static void BottomBar()
    {
        float sc = H / RefH;
        float[] x = { 46f, 174f, 302f, 430f, 558f };
        const float iconY = 2003f, s = 80f;
        // Must match FigmaUI.BarTarget (from the reference demo: icon N -> panel N).
        UiPage[] want = { UiPage.Cover, UiPage.Hud, UiPage.Vehicle, UiPage.SuitCheck, UiPage.Audio };

        for (int i = 0; i < x.Length; i++)
        {
            float cx = (x[i] + s * 0.5f) / RefW * W;
            float cy = (iconY + s * 0.5f) * sc;

            Check("bar icon " + i + " hittable", FigmaUI.BottomBarHit(cx, cy, W, H) == i,
                  "got " + FigmaUI.BottomBarHit(cx, cy, W, H));

            // The bar routes to its target from ANY page — tested here from a sub-page and the hub.
            NavHit fromSub = FigmaUI.HitTest(UiPage.Hud, cx, cy, W, H);
            Check("bar icon " + i + " routes (sub-page)",
                  fromSub.Act == NavAct.Goto && fromSub.Target == want[i],
                  "act " + fromSub.Act + " tgt " + fromSub.Target);

            NavHit fromCover = FigmaUI.HitTest(UiPage.Cover, cx, cy, W, H);
            Check("bar icon " + i + " wins over cover controls",
                  fromCover.Act == NavAct.Goto && fromCover.Target == want[i],
                  "act " + fromCover.Act + " tgt " + fromCover.Target);
        }

        // Neighbours must not share a hit region (icons are ~48px apart at 1280 wide).
        for (int i = 0; i + 1 < x.Length; i++)
        {
            float edge = ((x[i] + s) + x[i + 1]) * 0.5f / RefW * W;   // midpoint of the gap
            float cy = (iconY + s * 0.5f) * sc;
            Check("gap after icon " + i + " hits nothing", FigmaUI.BottomBarHit(edge, cy, W, H) == -1,
                  "got " + FigmaUI.BottomBarHit(edge, cy, W, H));
        }

        // A touch above the bar is not a bar hit.
        Check("above the bar misses", FigmaUI.BottomBarHit(100f, H * 0.5f, W, H) == -1, "");
    }
}
