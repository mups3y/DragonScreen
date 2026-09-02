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
        VehicleTabs();
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
        VehicleLiveValues();
        SubsystemLiveValues();
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
        s.BatteryText = variant == 0 ? "4 / 4" : "1 / 4";
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
        Check("tree still names the real vehicle's battery count", Drew(ta, "BATTERIES ×4"), "");
    }


    // ---- T13b: the six subsystem sub-tabs read the vessel, not a constant ----
    // Same shape and the same reasoning as VehicleLiveValues above: build each tab with fixture A, build
    // it again with a DIFFERENT fixture B, and assert every value moved. The extra thing worth proving
    // here is the OPPOSITE for AVIONICS — this build models none of that subsystem, so its nine values
    // must dash and must NOT move with the fixture; a later "improvement" that quietly fills them with a
    // plausible constant is exactly what this catches.
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
            // POWER: state of charge, array output twice (gauge + row), net flow twice (W and kW).
            new[] { a.PowerText, a.ArrayKwText, a.ArrayOutputText, a.NetPowerText, a.ChargeRateText },
            new string[0],                                                   // AVIONICS — see below
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
            new[] { "100", "120", "3.4", "3.4 kW", "+68 W", "50 %", "19 °C", "0 kW" },
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

        // ---- AVIONICS: nothing on it moves, because nothing on it is modelled ----
        // Stronger than "it drew a dash": not one value from EITHER fixture may appear anywhere on it.
        DisplayList av = new DisplayList(VehicleSubsystemPage.Commands + 60);
        DisplayList av2 = new DisplayList(VehicleSubsystemPage.Commands + 60);
        VehicleSubsystemPage.Build(av, VW, VH, VehicleSubsystemPage.Sub.Avionics, a);
        VehicleSubsystemPage.Build(av2, VW, VH, VehicleSubsystemPage.Sub.Avionics, b);
        for (int i = 0; i < live.Length; i++)
            for (int k = 0; k < live[i].Length; k++)
                Check("avionics invents no value (" + live[i][k] + ")",
                      !Drew(av, live[i][k]) && !Drew(av2, live[i][k]), "");
        Check("avionics fills no gauge ring", Arcs(av, DragonPalette.Accent) == 0
              && Arcs(av, DragonPalette.Go) == 0, "");

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
