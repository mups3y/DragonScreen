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
using DragonScreen.BlackBox;   // S137c: the schema's own append rule is asserted here

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
        BottomBarNav();
        SuitCheck();
        SuitLeakSimulation();
        VehicleTabs();
        VehicleDeepViewLinksTest();
        CoverPhases();
        CoverPhaseStepping();
        MarginAffordances();
        RangeRingsOnTop();
        MenuGridFits();
        PlaceholderUnreachable();
        CoverCamera();
        SpeccedPages();
        Menu();
        MenuHidesPlaceholders();
        OneScreenOneRenderer();
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
        S75InertPaintedControls();
        CoverEntryEnabled();
        CoverDroppedArrow();
        AudioPageChannels();
        CoverTargetReadouts();
        AlertsView();
        DiscreteEmergencies();
        SubsystemStateWords();
        OrbitRingScale();
        BottomBarCurrentState();
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
        // S110 / QC F-01: and an ALIAS is left off too - `Procedure` (3) and `VrioTest` (19) are one
        // real screen, so the grid must not offer two doors onto it. The value stays reachable.
        int wantEntries = 0;
        for (int i = 0; i < FigmaUI.PageCount; i++)
            if ((UiPage)i != UiPage.Menu && !FigmaUI.IsPlaceholder((UiPage)i)
                && !FigmaUI.IsAlias((UiPage)i)) wantEntries++;
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
            // S103: derived from BottomBar, not a third hardcoded copy of the stretched mapping -
            // this probe silently stopped landing on the bar when the draw was un-stretched, which is
            // exactly the drift the shared geometry exists to prevent.
            // ⭐ S176 — AND THIS PROBE MOVED WITH THE DRAW, WHICH IS THE POINT. The Menu is a
            // SPREAD page, so its bar reaches both glass edges and its first icon is ~109 px left of
            // where the letterboxed bar put it at 2560. A probe that kept the old number would have
            // gone on testing an empty strip. The centre comes from the page's OWN fit.
            float bcx, bcy;
            IconCentre(UiPage.Menu, 0, W, H, out bcx, out bcy);
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

            bool want = p != UiPage.Menu && !FigmaUI.IsPlaceholder(p) && !FigmaUI.IsAlias(p);
            Check("menu " + (want ? "lists" : "hides") + " " + p, present == want,
                  "got present=" + present + " want=" + want);
        }
    }

    // ---- S110 / QC F-01: one real screen, one renderer ----
    // Page 3 (`Procedure`) and page 19 (`VrioTest`) are the same screen - "4.700 Deorbit Preparation /
    // Test VRIO Health LEDs". The build shipped both: 3 as the baked `frame59` PNG, 19 as the element
    // rebuild, each with its own Menu card, and the two drawings had drifted apart (different tick
    // style, an extra glyph, differently placed note cards, a different read-only icon).
    static void OneScreenOneRenderer()
    {
        var a = new DisplayList(FigmaUI.Commands);
        var b = new DisplayList(FigmaUI.Commands);
        var st = new PageState();
        var view = MapProjection.Default();
        FigmaUI.Build(a, UiPage.Procedure, W, H, st, view);
        FigmaUI.Build(b, UiPage.VrioTest, W, H, st, view);

        // 1. The alias resolves - identical command streams, not merely "both non-empty".
        Check("Procedure and VrioTest draw the same command count",
              a.Count == b.Count, a.Count + " vs " + b.Count);
        bool same = a.Count == b.Count;
        for (int c = 0; same && c < a.Count; c++)
        {
            var x = a.At(c); var y = b.At(c);
            if (x.Kind != y.Kind || x.A != y.A || x.B != y.B || x.C != y.C || x.D != y.D
                || x.Str != y.Str || !Same(x.Colour, y.Colour)) same = false;
        }
        Check("Procedure and VrioTest are the SAME screen, command for command", same, "");

        // 2. It is still the VRIO page and not something empty - guards a vacuous pass.
        bool sawTitle = false;
        for (int c = 0; c < a.Count; c++)
            if (a.At(c).Kind == DrawKind.Text && a.At(c).Str == "Test VRIO Health LEDs") sawTitle = true;
        Check("page 3 now draws the VRIO procedure", sawTitle, "");

        // 3. The enum value is KEPT and still reachable - UiPage's own rule is that the int persists per
        //    screen, so a save written on page 3 must still open something real.
        Check("Procedure is not a placeholder", !FigmaUI.IsPlaceholder(UiPage.Procedure), "");
        Check("Procedure is an alias of VrioTest",
              FigmaUI.Canonical(UiPage.Procedure) == UiPage.VrioTest, "");
        Check("VrioTest is its own canonical page",
              FigmaUI.Canonical(UiPage.VrioTest) == UiPage.VrioTest, "");

        // 4. Exactly one Menu card leads to this screen.
        int cards = 0;
        for (int j = 0; j < MenuPage.Entries.Length; j++)
            if (FigmaUI.Canonical(MenuPage.Entries[j]) == UiPage.VrioTest) cards++;
        Check("exactly one Menu card opens the VRIO procedure", cards == 1, "got " + cards);
    }

    static void Rendezvous()
    {
        // T6: reached from Docking's letterbox margin - mirrors the HUD's own Docking affordance
        // (SpeccedPages) - and carries the bottom bar like every page.
        float sc = (float)H / RefH, ox = (W - RefW * sc) * 0.5f;

        NavHit toRdv = FigmaUI.HitTest(UiPage.Docking, 30f, 0.5f * H, W, H);
        Check("Docking margin -> Rendezvous", toRdv.Act == NavAct.Goto && toRdv.Target == UiPage.Rendezvous,
              "got " + toRdv.Act + " " + toRdv.Target);

        // *** S176 - THE PROBE AND THE ASSERTION WERE BOTH WRONG, AND HAD BEEN SINCE S103.
        // `(46+40)/RefW*W` is the STRETCHED mapping S103 removed from the draw, left behind here as a
        // hand-written copy; at 1280x703 it lands at x 32.1 against this page's icon box of
        // 85.0..111.6, so it had been missing the bar entirely. It passed because `.Target ==
        // UiPage.Cover` is ALSO what a MISS returns - `NavHit.None`'s Target is `UiPage.Cover`. The
        // probe now comes from `IconCentre`, which reads the page's own fit, and the assertion
        // requires an actual `Goto`.
        float bcx, bcy;
        IconCentre(UiPage.Rendezvous, 0, W, H, out bcx, out bcy);
        NavHit toCover = FigmaUI.HitTest(UiPage.Rendezvous, bcx, bcy, W, H);
        Check("Rendezvous bottom-bar -> Cover",
              toCover.Act == NavAct.Goto && toCover.Target == UiPage.Cover,
              "got " + toCover.Act + " " + toCover.Target);

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
        // *** S176 - THE PROBE AND THE ASSERTION WERE BOTH WRONG, AND HAD BEEN SINCE S103.
        // `(46+40)/RefW*W` is the STRETCHED mapping S103 removed from the draw, left behind here as a
        // hand-written copy; at 1280x703 it lands at x 32.1 against this page's icon box of
        // 85.0..111.6, so it had been missing the bar entirely. It passed because `.Target ==
        // UiPage.Cover` is ALSO what a MISS returns - `NavHit.None`'s Target is `UiPage.Cover`. The
        // probe now comes from `IconCentre`, which reads the page's own fit, and the assertion
        // requires an actual `Goto`.
        float bcx, bcy;
        IconCentre(UiPage.DeorbitBurnPrep, 0, W, H, out bcx, out bcy);
        NavHit toCover = FigmaUI.HitTest(UiPage.DeorbitBurnPrep, bcx, bcy, W, H);
        Check("DeorbitBurnPrep bottom-bar -> Cover",
              toCover.Act == NavAct.Goto && toCover.Target == UiPage.Cover,
              "got " + toCover.Act + " " + toCover.Target);

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
        // *** S176 - THE PROBE AND THE ASSERTION WERE BOTH WRONG, AND HAD BEEN SINCE S103.
        // `(46+40)/RefW*W` is the STRETCHED mapping S103 removed from the draw, left behind here as a
        // hand-written copy; at 1280x703 it lands at x 32.1 against this page's icon box of
        // 85.0..111.6, so it had been missing the bar entirely. It passed because `.Target ==
        // UiPage.Cover` is ALSO what a MISS returns - `NavHit.None`'s Target is `UiPage.Cover`. The
        // probe now comes from `IconCentre`, which reads the page's own fit, and the assertion
        // requires an actual `Goto`.
        float bcx, bcy;
        IconCentre(UiPage.EntryProcedure, 0, W, H, out bcx, out bcy);
        NavHit toCover = FigmaUI.HitTest(UiPage.EntryProcedure, bcx, bcy, W, H);
        Check("EntryProcedure bottom-bar -> Cover",
              toCover.Act == NavAct.Goto && toCover.Target == UiPage.Cover,
              "got " + toCover.Act + " " + toCover.Target);

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
        // *** S176 - THE PROBE AND THE ASSERTION WERE BOTH WRONG, AND HAD BEEN SINCE S103.
        // `(46+40)/RefW*W` is the STRETCHED mapping S103 removed from the draw, left behind here as a
        // hand-written copy; at 1280x703 it lands at x 32.1 against this page's icon box of
        // 85.0..111.6, so it had been missing the bar entirely. It passed because `.Target ==
        // UiPage.Cover` is ALSO what a MISS returns - `NavHit.None`'s Target is `UiPage.Cover`. The
        // probe now comes from `IconCentre`, which reads the page's own fit, and the assertion
        // requires an actual `Goto`.
        float tabX = VehicleTabBar.CentreX(4) / RefW * W, tabY = 1812f * sc;

        foreach (UiPage p in new[] { UiPage.SystemsTree, UiPage.SystemsPid })
        {
            float bcx, bcy;
            IconCentre(p, 0, W, H, out bcx, out bcy);
            NavHit toCover = FigmaUI.HitTest(p, bcx, bcy, W, H);
            Check(p + " bottom-bar -> Cover",
                  toCover.Act == NavAct.Goto && toCover.Target == UiPage.Cover,
                  "got " + toCover.Act + " " + toCover.Target);

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
        // *** S176 - THE PROBE AND THE ASSERTION WERE BOTH WRONG, AND HAD BEEN SINCE S103.
        // `(46+40)/RefW*W` is the STRETCHED mapping S103 removed from the draw, left behind here as a
        // hand-written copy; at 1280x703 it lands at x 32.1 against this page's icon box of
        // 85.0..111.6, so it had been missing the bar entirely. It passed because `.Target ==
        // UiPage.Cover` is ALSO what a MISS returns - `NavHit.None`'s Target is `UiPage.Cover`. The
        // probe now comes from `IconCentre`, which reads the page's own fit, and the assertion
        // requires an actual `Goto`.
        float bcx, bcy;
        IconCentre(UiPage.Ascent, 0, W, H, out bcx, out bcy);
        NavHit toCover = FigmaUI.HitTest(UiPage.Ascent, bcx, bcy, W, H);
        Check("Ascent bottom-bar -> Cover",
              toCover.Act == NavAct.Goto && toCover.Target == UiPage.Cover,
              "got " + toCover.Act + " " + toCover.Target);

        bool sawIt = false;
        for (int i = 0; i < MenuPage.Entries.Length; i++)
            if (MenuPage.Entries[i] == UiPage.Ascent) sawIt = true;
        Check("Menu lists Ascent", sawIt, "");

        NavHit body = FigmaUI.HitTest(UiPage.Ascent, 0.5f * W, 0.4f * H, W, H);
        Check("Ascent body is inert", body.Act == NavAct.None, "got " + body.Act);
        Check("Ascent is a real page, not a placeholder", !FigmaUI.IsPlaceholder(UiPage.Ascent), "");

        AscentMarks();
    }

    /// <summary>The eleven T+ event callouts against the live step machine (S159 / S49 H34 / QC AS-01).
    /// All eleven used to draw in one tint while `StepList` — which resolves most of them — ran unread.
    /// </summary>
    static void AscentMarks()
    {
        var m = new AscentPage.EventMark[11];

        // ---- ON THE PAD: nothing has happened, and LIFTOFF is what is next ----
        PageState pad = AscentFixture();
        pad.Steps.OnPad = true; pad.Steps.Clamped = true;
        pad.Steps.MaxQPassed = false; pad.Steps.BoosterAttached = true; pad.Steps.S2Attached = true;
        pad.Steps.S2Lit = false; pad.Steps.InSpace = false; pad.Steps.NoseConeOpen = false;
        pad.Steps.RadarAltitude = 0.0;
        Check("pad: all eleven events are marked", AscentPage.Marks(pad, m) == 11, "");
        Check("pad: LIFTOFF is the current milestone",
              m[0] == AscentPage.EventMark.Current, "got " + m[0]);
        int padPassed = CountMark(m, 11, AscentPage.EventMark.Passed);
        Check("pad: nothing has passed", padPassed == 0, "got " + padPassed);
        Check("pad: everything above liftoff is pending",
              CountMark(m, 11, AscentPage.EventMark.Pending) == 10, "");

        // ---- JUST AFTER LIFTOFF: the window the ordering rule CANNOT answer, and must not pretend to.
        // The clamps have let go and nothing else has been observed, so PITCH KICK - which nothing in
        // this build sees - is the milestone the ascent has REACHED. Not passed: reached. That is the
        // one honest thing to say about an unobserved event with no later observation behind it.
        PageState early = AscentFixture();
        early.Steps.MaxQPassed = false; early.Steps.BoosterAttached = true; early.Steps.BoosterLit = true;
        early.Steps.S2Attached = true; early.Steps.S2Lit = false; early.Steps.InSpace = false;
        early.Steps.RadarAltitude = 800.0;
        AscentPage.Marks(early, m);
        Check("early: LIFTOFF has passed - the clamps let go",
              m[0] == AscentPage.EventMark.Passed, "got " + m[0]);
        Check("early: PITCH KICK is current, NOT passed - nothing has been observed after it",
              m[1] == AscentPage.EventMark.Current, "got " + m[1]);
        Check("early: exactly one event has passed",
              CountMark(m, 11, AscentPage.EventMark.Passed) == 1, "");
        Check("early: MAX-Q is still pending with no peak latched",
              m[2] == AscentPage.EventMark.Pending, "got " + m[2]);

        // ---- MID-ASCENT: QC AS-01's own fixture. Max-Q latched, booster gone, S2 burning ----
        PageState mid = AscentFixture();
        Check("mid: eleven marks", AscentPage.Marks(mid, m) == 11, "");
        Check("mid: SECO-1 is current while S2 is still lit",
              m[8] == AscentPage.EventMark.Current, "got " + m[8]);
        Check("mid: MECO and STAGE SEPARATION have passed",
              m[5] == AscentPage.EventMark.Passed && m[6] == AscentPage.EventMark.Passed, "");
        Check("mid: DRAGON SEPARATION and NOSE-CONE OPEN are still pending",
              m[9] == AscentPage.EventMark.Pending && m[10] == AscentPage.EventMark.Pending, "");
        // ⭐ S2 IGNITION is the one event sourced from a raw StepInputs FIELD (`S2Lit`), not a StepList
        // row - QC counts it among the five fields proving the fixture has flown.
        Check("mid: S2 IGNITION is passed because S2Lit says the engine is burning",
              mid.Steps.S2Lit && m[7] == AscentPage.EventMark.Passed, "got " + m[7]);
        // ⭐ AND THE THREE UNOBSERVED EVENTS ARE ANSWERED BY ORDER, NOT BY A GUESS. Nothing in this
        // build sees a pitch program, a Mach number, or the real 1B abort call. But MECO has been
        // observed, and all three are printed BEFORE it on a page whose events are chronological, so
        // they are behind us. This is the whole of the inference the page makes.
        Check("mid: PITCH KICK, MACH 1 and STAGE-1B ABORT MODE are passed by ORDERING",
              m[1] == AscentPage.EventMark.Passed && m[3] == AscentPage.EventMark.Passed &&
              m[4] == AscentPage.EventMark.Passed, "");
        int midPassed = CountMark(m, 11, AscentPage.EventMark.Passed);
        Check("mid: eight of eleven have passed", midPassed == 8, "got " + midPassed);

        // ---- POST-INSERTION: everything is behind, so nothing is current ----
        PageState orbit = AscentFixture();
        orbit.Steps.BoosterAttached = false; orbit.Steps.S2Attached = false; orbit.Steps.S2Lit = false;
        orbit.Steps.InSpace = true; orbit.Steps.NoseConeOpen = true; orbit.Steps.RadarAltitude = 400000.0;
        AscentPage.Marks(orbit, m);
        Check("orbit: all eleven have passed",
              CountMark(m, 11, AscentPage.EventMark.Passed) == 11, "");
        Check("orbit: nothing is current once the last event is behind",
              CountMark(m, 11, AscentPage.EventMark.Current) == 0, "");
        // S2 IGNITION must NOT un-happen when the engine stops. `S2Lit` is false here; only the
        // backward ordering rule keeps it passed.
        Check("orbit: S2 IGNITION stays passed after the engine has shut down",
              !orbit.Steps.S2Lit && m[7] == AscentPage.EventMark.Passed, "");

        // ---- THE MARKING IS MONOTONE. A passed event can never sit above a pending one. ----
        Check("marks never go backwards up the timeline",
              MonotoneMarks(mid, m) && MonotoneMarks(pad, m) && MonotoneMarks(orbit, m)
              && MonotoneMarks(early, m), "");

        // ---- NO FEED: the page marks NOTHING rather than eleven confident pendings ----
        PageState dead = AscentFixture(); dead.Valid = false; dead.Steps.Valid = false;
        Check("no feed = no marking at all", AscentPage.Marks(dead, m) == 0, "");
        // ...and the DRAW follows it: with nothing known there is no marker on the page at all.
        DisplayList dl = new DisplayList(AscentPage.Commands + 40);
        AscentPage.Build(dl, W, H, dead);
        DisplayList live = new DisplayList(AscentPage.Commands + 40);
        AscentPage.Build(live, W, H, mid);
        Check("a live ascent draws eleven event markers and a dead one draws none",
              Rects(live) - Rects(dl) == 11, "live " + Rects(live) + " dead " + Rects(dl));

        // ---- THE T+ TIMES ARE REFERENCE COPY AND THIS LINE DOES NOT TOUCH THEM (QC AS-01) ----
        // Marking WHICH events have happened must not change WHEN they are printed to happen.
        string[] times = { "LIFTOFF", "T+0:10 — PITCH KICK", "T+1:00 — MAX-Q", "T+1:09 — MACH 1",
                           "T+1:14 — STAGE-1B ABORT MODE", "T+2:30–2:35 — MECO",
                           "T+2:35–2:39 — STAGE SEPARATION", "T+2:36–2:47 — S2 IGNITION",
                           "T+4:20–8:43 — SECO-1 / ORBIT INSERTION", "T+9:00–12:02 — DRAGON SEPARATION",
                           "T+12:48–13:23 — NOSE-CONE OPEN" };
        bool allThere = true;
        for (int i = 0; i < times.Length; i++)
            if (!Drew(live, times[i]) || !Drew(dl, times[i])) allThere = false;
        Check("all eleven T+ strings are drawn verbatim, marked or not", allThere, "");

        // ---- and the three states are VISIBLY different, not three names for one tint ----
        Check("passed / current / pending are three different colours",
              !SameColour(ColourOf(live, "T+2:30–2:35 — MECO"),
                          ColourOf(live, "T+4:20–8:43 — SECO-1 / ORBIT INSERTION")) &&
              !SameColour(ColourOf(live, "T+4:20–8:43 — SECO-1 / ORBIT INSERTION"),
                          ColourOf(live, "T+12:48–13:23 — NOSE-CONE OPEN")) &&
              !SameColour(ColourOf(live, "T+2:30–2:35 — MECO"),
                          ColourOf(live, "T+12:48–13:23 — NOSE-CONE OPEN")), "");
    }

    /// <summary>QC AS-01's own frame: mid-ascent, Max-Q latched, booster gone, second stage burning.
    /// The five fields it names as proof that events have occurred (`PreviewMain`'s shared fixture).
    /// </summary>
    static PageState AscentFixture()
    {
        PageState s = new PageState();
        s.Valid = true;
        s.Phase = "ASCENT";
        s.Steps.Valid = true;
        s.Steps.Crew = 4;
        s.Steps.OnPad = false; s.Steps.Clamped = false; s.Steps.Powered = true;
        s.Steps.Propellant01 = 0.62; s.Steps.EscapeArmed = true;
        s.Steps.RadarAltitude = 96000.0; s.Steps.VerticalSpeed = 480.0;
        s.Steps.MaxQPassed = true;
        s.Steps.BoosterAttached = false; s.Steps.S2Attached = true; s.Steps.S2Lit = true;
        return s;
    }

    static int CountMark(AscentPage.EventMark[] m, int n, AscentPage.EventMark want)
    { int c = 0; for (int i = 0; i < n; i++) if (m[i] == want) c++; return c; }

    /// <summary>Once an event is not passed, nothing above it may be. The page prints its events in
    /// chronological order, so a passed one sitting above a pending one would be the timeline
    /// contradicting itself.</summary>
    static bool MonotoneMarks(PageState s, AscentPage.EventMark[] m)
    {
        int n = AscentPage.Marks(s, m);
        bool seenUnpassed = false;
        for (int i = 0; i < n; i++)
        {
            if (m[i] != AscentPage.EventMark.Passed) seenUnpassed = true;
            else if (seenUnpassed) return false;
        }
        return true;
    }

    /// <summary>How many filled rectangles the page drew. The event markers are the only Rects
    /// AscentPage adds beyond its background, so this counts them without reaching into the page.
    /// </summary>
    static int Rects(DisplayList dl)
    {
        int n = 0;
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == DrawKind.Rect) n++;
        return n;
    }

    static void NavOrbitPlot()
    {
        // S15: the circular nav/orbit plot (SCREEN_INVENTORY #28) - same reachability footing as
        // Ascent (T12) / DeorbitBurnPrep (T7) / EntryProcedure (T8) - reached only via the Menu grid
        // for now (a real entry point is T14's job), carries the bottom bar, and its content (the
        // concentric rings, the shared NavPage.Orbit conic, the colour key, the g/rate readout) is
        // display-only - no invented destinations.
        // *** S176 - THE PROBE AND THE ASSERTION WERE BOTH WRONG, AND HAD BEEN SINCE S103.
        // `(46+40)/RefW*W` is the STRETCHED mapping S103 removed from the draw, left behind here as a
        // hand-written copy; at 1280x703 it lands at x 32.1 against this page's icon box of
        // 85.0..111.6, so it had been missing the bar entirely. It passed because `.Target ==
        // UiPage.Cover` is ALSO what a MISS returns - `NavHit.None`'s Target is `UiPage.Cover`. The
        // probe now comes from `IconCentre`, which reads the page's own fit, and the assertion
        // requires an actual `Goto`.
        float bcx, bcy;
        IconCentre(UiPage.NavOrbitPlot, 0, W, H, out bcx, out bcy);
        NavHit toCover = FigmaUI.HitTest(UiPage.NavOrbitPlot, bcx, bcy, W, H);
        Check("NavOrbitPlot bottom-bar -> Cover",
              toCover.Act == NavAct.Goto && toCover.Target == UiPage.Cover,
              "got " + toCover.Act + " " + toCover.Target);

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

    /// <summary>The tint of an ASSET draw, found by WHERE it is rather than by which one it is
    /// (S158). `ic_check` is drawn six times on the Suit Leak Check — the two procedure step ticks
    /// and the four STATUS markers — so "the n-th one" would silently follow any re-ordering of the
    /// page. yFrac is the design-space Y divided by the design height, which is a property of the
    /// LAYOUT and changes only if the tick actually moves. Returns transparent black if nothing sits
    /// there, which no colour check can pass by accident.</summary>
    static Rgba AssetTintAtFrac(DisplayList dl, string key, float yFrac, int h)
    {
        float want = yFrac * h, bestD = 8f;      // 8 px: tighter than the 108-design-px tick pitch
        Rgba best = new Rgba(0f, 0f, 0f, 0f);
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Image || c.AssetKey != key) continue;
            float d = c.B - want; if (d < 0f) d = -d;
            if (d < bestD) { bestD = d; best = c.Colour; }
        }
        return best;
    }

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
    /// <summary>
    /// How many gauge rings are FILLED, whatever colour they are (S104 / QC V-01 + S-01).
    ///
    /// The three checks below used to count rings by their hue - `Arcs(dl, Hex("D12C30"))` and friends -
    /// which only worked while every ring was a hardcoded constant. The ring colour is now the model's
    /// computed severity, so a nominal cabin draws green and an alarming one red, and counting by hue
    /// counts the FIXTURE rather than the page. What the checks are actually for is stated in their own
    /// comment - "a fill per gauge is the count to hold" - so they count fills.
    ///
    /// A gauge's FILL is the second ArcBand at that centre: `Gauge` draws the dim 300-degree track first
    /// and the coloured fill over it, so a fill is any ArcBand that is not drawn in the track's colour.
    /// </summary>
    static int RingFills(DisplayList dl)
    {
        int n = 0;
        Rgba track = DragonPalette.Text7;      // `Faint`, the dim track every gauge draws first
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.ArcBand) continue;
            if (c.Colour.R == track.R && c.Colour.G == track.G && c.Colour.B == track.B) continue;
            n++;
        }
        return n;
    }

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

    /// <summary>The first draw command for a NAMED asset, or a zeroed command if it was not drawn.
    /// S75 needs the asset's TINT and its drawn rect: a glyph that is painted but has no hit rect is
    /// distinguishable from a real button only by its colour, and the rect is what the "no rect" half
    /// of the claim has to be aimed at.</summary>
    static DrawCmd AssetCmd(DisplayList dl, string key)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Image && c.AssetKey == key) return c;
        }
        return new DrawCmd();
    }

    // ------------------------------------------------------------------------------------------
    // S75 — THE PAINTED CONTROLS THAT ARE NOT CONTROLS.
    // S54 pinned the mirror-image defect: a hit rect that fires on a phase whose label is not drawn.
    // These two are the opposite — a label drawn with no hit rect anywhere — which H18 calls worse,
    // because a no-op at least resolves to a named action. Neither could be given a rectangle without
    // first deciding what the rectangle DOES (a §1.4 question for `gridicons_refresh`, and the still-
    // unfilled MARGIN column for `SHOW MARGINS TO` — S76), so both take S75's other branch and are
    // drawn INERT. The claim under test is therefore two-sided and has to stay two-sided: the glyph is
    // drawn in the inert tint AND nothing hit-tests where it is drawn. Pinning only the colour would
    // let a later chat re-brighten it; pinning only the miss would let a later chat leave it white.
    // ------------------------------------------------------------------------------------------
    static void S75InertPaintedControls()
    {
        const int VW = 2560, VH = 1406;

        // ---- (1) VEHICLE OVERVIEW: "SHOW MARGINS TO" ----
        // Checked on a LIVE fixture, not the dead-feed one: everything on this page dims with no feed,
        // so a dead-feed assertion would pass even if the control were still painted as a live link.
        DisplayList ov = new DisplayList(VehicleOverviewPage.Commands + 60);
        VehicleOverviewPage.Build(ov, VW, VH, VehicleFixture(0));

        Check("S75 overview still draws SHOW MARGINS TO", Drew(ov, "SHOW MARGINS TO"), "");
        Check("S75 SHOW MARGINS TO is drawn INERT, not as a live control",
              SameColour(ColourOf(ov, "SHOW MARGINS TO"), DragonPalette.Text6),
              "painted in " + ColourOf(ov, "SHOW MARGINS TO").R + "," +
              ColourOf(ov, "SHOW MARGINS TO").G + "," + ColourOf(ov, "SHOW MARGINS TO").B);
        Check("S75 SHOW MARGINS TO is not painted in the touchable-link tint",
              !SameColour(ColourOf(ov, "SHOW MARGINS TO"), DragonPalette.Accent), "");
        // The contrast is the whole point, and it is only meaningful while the links it is being
        // distinguished FROM are still drawn in Accent and still touchable. If a later task restyles
        // VehicleDeepViewLinks, this reddens and the inert treatment has to be rechosen, not silently
        // lost — which is exactly the failure S75 exists to stop happening again.
        Check("S75 the two links that ARE touchable stay in the Accent idiom",
              SameColour(ColourOf(ov, "SYSTEMS TREE"), DragonPalette.Accent) &&
              SameColour(ColourOf(ov, "SYSTEMS P&ID"), DragonPalette.Accent), "");

        // ---- (2) COVER: the gridicons_refresh glyph ----
        // Phase 0 (Deport & Burn), Earth camera: a plain, non-Reference build, so none of S54's
        // ReferenceSkipKeys suppression is in play and the glyph is drawn by the ordinary asset loop.
        PageState cs = new PageState(); cs.Valid = true;
        DisplayList cv = new DisplayList(CoverPage.Commands + 80);
        CoverPage.Build(cv, VW, VH, cs, MapProjection.Default(), 0, CoverPage.CoverCam.Earth);

        DrawCmd refresh = AssetCmd(cv, "gridicons_refresh");
        Check("S75 cover still draws the refresh glyph", refresh.AssetKey == "gridicons_refresh", "");
        // Pinned to Text6 by NAME, not to CoverPage.InertTint: comparing the drawn colour against the
        // page's own constant is self-referential — re-pointing that constant at White would keep such
        // a check green while putting the glyph straight back into the button idiom. Mutation-verified.
        Check("S75 the refresh glyph is drawn INERT, not in the white button idiom",
              SameColour(refresh.Colour, DragonPalette.Text6), "");
        Check("S75 the cover's inert tint is still the no-source tint",
              SameColour(CoverPage.InertTint, DragonPalette.Text6), "");
        Check("S75 the refresh glyph is not painted white",
              !SameColour(refresh.Colour, DragonPalette.White), "");

        // The three white glyphs on this page that ARE buttons must stay white — same reasoning as the
        // Accent check above: "inert is dimmer than a button" only says something while buttons are lit.
        Check("S75 the cover's real glyph buttons stay white",
              SameColour(AssetCmd(cv, "eva_menu_fill").Colour, DragonPalette.White) &&
              SameColour(AssetCmd(cv, "ic_sharp_arrow_back").Colour, DragonPalette.White) &&
              SameColour(AssetCmd(cv, "ic_sharp_arrow_back_1").Colour, DragonPalette.White), "");

        // ---- the other half of the claim: nothing fires where the glyph is drawn ----
        // Aimed at the CENTRE of the rect the page actually drew, so the check follows the asset if it
        // is ever repositioned rather than at a coordinate copied out of the Box table.
        float cx = refresh.A + refresh.C * 0.5f, cy = refresh.B + refresh.D * 0.5f;
        Check("S75 no button fires at the refresh glyph's centre",
              CoverPage.HitTest(cx, cy, VW, VH, CoverPage.CoverCam.Earth, 0) == CoverPage.CoverButton.None,
              "hit " + CoverPage.HitTest(cx, cy, VW, VH, CoverPage.CoverCam.Earth, 0));
        // ...and the three real ones still do, on the same page build, so a blanket break in HitTest
        // cannot make the line above pass.
        DrawCmd menu = AssetCmd(cv, "eva_menu_fill");
        Check("S75 the Menu glyph still fires",
              CoverPage.HitTest(menu.A + menu.C * 0.5f, menu.B + menu.D * 0.5f, VW, VH,
                                CoverPage.CoverCam.Earth, 0) == CoverPage.CoverButton.Menu, "");
    }

    /// <summary>A vehicle fixture whose every live readout is a distinct, recognisable string.</summary>
    static PageState VehicleFixture(int variant)
    {
        PageState s = new PageState();
        s.Valid = true;
        // ⚠ S147: a live vessel always HAS a mission phase - `Mission.Classify` gives one on every
        // frame - and since S147 the bottom bar prints it as CURRENT STATE on every page. A fixture
        // without one made that row dash, which broke this suite's "GNC has no unsourced readout"
        // check for a reason that was about the FIXTURE, not the page.
        s.Phase = "Orbit";
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
        // ---- S104 / QC V-01: THE ENGINEERING VALUES, WHICH THIS FIXTURE NEVER SET ----
        // The ring's COLOUR is now the model's verdict, and `Alarms.Band` reads the raw quantity, not the
        // 0..1 fraction. These fields defaulted to 0.0 here because nothing had ever read them - and 0.0
        // psia of oxygen at 0.0 psia of cabin pressure is a genuine ALARM, so the first honest run of the
        // new code lit two red rings on a fixture whose own text says "3.0 / 14.7". The fixture was
        // incomplete, not the page. Set to agree with the text and the fractions above: one nominal cabin,
        // described the same way three times.
        s.Cabin.Ppo2Psia = 3.0; s.Cabin.CabinTempC = 21.0;
        s.Cabin.PressPsia = 14.7; s.Cabin.Co2MmHg = 1.0;
        s.Cabin.LoopAC = 27.0; s.Cabin.LoopBC = 20.0;
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
        int crewFills = RingFills(cr);
        Check("crew fills one ring per sourced gauge", crewFills == 4, "got " + crewFills);
        int crewDeadFills = RingFills(crDead);
        Check("crew fills no ring with no feed", crewDeadFills == 0, "got " + crewDeadFills);
        // THERMAL has three sourced gauges of four: the RADIATOR has no model and must stay empty.
        DisplayList th = new DisplayList(VehicleSubsystemPage.Commands + 60);
        VehicleSubsystemPage.Build(th, VW, VH, VehicleSubsystemPage.Sub.Thermal, a);
        int thermFills = RingFills(th);
        Check("thermal leaves the unsourced radiator ring empty", thermFills == 3, "got " + thermFills);

        // ---- S104 / QC V-01: THE RING'S COLOUR IS THE VERDICT, SO IT MUST MOVE WITH THE VALUE ----
        // The whole finding was that CABIN TEMP drew alarm-red at a nominal 21.8 C because the colour was
        // a constant. A count of fills cannot catch that coming back, so this does: the SAME gauge, two
        // fixtures either side of `CabinLimits.CabinTempAlarm`, must not come out the same colour - and
        // the nominal one must not be the alarm colour.
        PageState hot = VehicleFixture(0);
        hot.Cabin.CabinTempC = CabinLimits.CabinTempAlarm + 5.0;
        DisplayList crHot = new DisplayList(VehicleSubsystemPage.Commands + 60);
        VehicleSubsystemPage.Build(crHot, VW, VH, VehicleSubsystemPage.Sub.Crew, hot);
        int alarmNominal = Arcs(cr,    DragonPalette.Alarm);
        int alarmHot     = Arcs(crHot, DragonPalette.Alarm);
        Check("a nominal cabin draws no alarm-red ring", alarmNominal == 0, "got " + alarmNominal);
        Check("an over-limit cabin does draw one",       alarmHot   >  0,   "got " + alarmHot);

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
        SuitCheckPage.Build(sa, VW, VH, 5, false, sca, false);
        SuitCheckPage.Build(sb, VW, VH, 5, false, scb, false);
        SuitCheckPage.Build(sd, VW, VH, 5, false, SuitLeak.From(dead, 5, false, 0u), false);
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
        NavPage.Orbit(nav, a, 0f, 0f, VW * 0.5f, VH * 0.5f, 1f);
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
        // ⛔ TWO DEFECTS FOUND HERE BY S176, AND BOTH WERE ALREADY PRESENT. (1) The probe was
        // `(46+40)/RefW*W` - a fourth hand-written copy of the STRETCHED mapping S103 removed - and
        // one x was used for two pages that do not share a map. (2) The assertion was
        // `.Target == UiPage.Cover`, and `NavHit.None`'s Target IS `UiPage.Cover`: a MISS passed this
        // check. Measured at 1280x703, the Docking probe landed at x 32.1 against an icon box of
        // 85.0..111.6 - it had been missing the bar entirely and reporting success. Both halves are
        // fixed together, because either one alone still hides the other.
        {
            float mcx, mcy, dkx, dky;
            IconCentre(UiPage.ManualChute, 0, W, H, out mcx, out mcy);
            IconCentre(UiPage.Docking, 0, W, H, out dkx, out dky);
            NavHit mc = FigmaUI.HitTest(UiPage.ManualChute, mcx, mcy, W, H);
            NavHit dk = FigmaUI.HitTest(UiPage.Docking, dkx, dky, W, H);
            Check("ManualChute bottom-bar -> Cover",
                  mc.Act == NavAct.Goto && mc.Target == UiPage.Cover, "got " + mc.Act + " " + mc.Target);
            Check("Docking bottom-bar -> Cover",
                  dk.Act == NavAct.Goto && dk.Target == UiPage.Cover, "got " + dk.Act + " " + dk.Target);
        }
    }

    // ---- S107 / QC C-07: THE RAIL AND THE ARROWS MUST AGREE ABOUT WHAT A SLOT DOES ----
    // The Cover's seven rail slots are reachable two ways, and before this they behaved differently.
    // A TAP on slot 6 navigated (FigmaUI.HitTest runs before the painter's Cover branch, and MapCover
    // sends PhaseManual to UiPage.ManualChute). An ARROW onto slot 6 - ► from 5, or ◄ wrapping from
    // 0 - just set coverPhase = 6, leaving the Cover with the heading "Manual Chute Deploy" over the
    // Coast to Trunk Jettison body, naming a REAL page whose real content was one tap away.
    //
    // The painter's rule is now three lines over two pure functions, so all of it is testable here:
    //     next = CoverPage.StepPhase(coverPhase, dir)
    //     nav  = FigmaUI.PhaseNav(next);  nav >= 0 ? open it (coverPhase unchanged) : coverPhase = next
    static void CoverPhaseStepping()
    {
        float sc = (float)H / RefH;
        float[] slotY = { 253f, 421f, 589f, 757f, 925f, 1093f, 1261f };

        // 1. Which slots are pages, and which select in-page? Derived from MapCover, not typed twice.
        Check("rail slot 6 opens Manual Chute",
              FigmaUI.PhaseNav(6) == (int)UiPage.ManualChute, "got " + FigmaUI.PhaseNav(6));
        for (int i = 0; i < 6; i++)
            Check("rail slot " + i + " selects a phase in-page", FigmaUI.PhaseNav(i) < 0,
                  "got " + FigmaUI.PhaseNav(i));
        Check("PhaseNav is -1 out of range",
              FigmaUI.PhaseNav(-1) < 0 && FigmaUI.PhaseNav(CoverPage.PhaseCount) < 0, "");

        // 2. A TAP on each rail row must reach the SAME verdict PhaseNav gives.  This is the check
        //    that pins "one rail item, one navigation model" - if either side is ever changed alone,
        //    it fails here rather than on the glass.
        for (int i = 0; i < CoverPage.PhaseCount; i++)
        {
            NavHit nh = FigmaUI.HitTest(UiPage.Cover, 110f * sc, (slotY[i] + 80f) * sc, W, H);
            int tapped = nh.Act == NavAct.Goto ? (int)nh.Target : -1;
            Check("rail row " + i + ": a TAP and PhaseNav agree", tapped == FigmaUI.PhaseNav(i),
                  "tap=" + tapped + " PhaseNav=" + FigmaUI.PhaseNav(i));
        }

        // 3. StepPhase wraps over all seven, from anywhere, including out of range.
        for (int p = 0; p < CoverPage.PhaseCount; p++)
        {
            Check("step + from " + p, CoverPage.StepPhase(p, +1) == (p + 1) % 7,
                  "got " + CoverPage.StepPhase(p, +1));
            Check("step - from " + p, CoverPage.StepPhase(p, -1) == (p + 6) % 7,
                  "got " + CoverPage.StepPhase(p, -1));
        }
        Check("step clamps a negative phase", CoverPage.StepPhase(-5, +1) == 1,
              "got " + CoverPage.StepPhase(-5, +1));
        Check("step clamps an over-range phase", CoverPage.StepPhase(99, +1) == 0,
              "got " + CoverPage.StepPhase(99, +1));

        // 4. THE INVARIANT, and the whole point of the finding: whatever the crew does with the
        //    arrows, the Cover is never LEFT DISPLAYING a slot whose heading names another page.
        for (int p = 0; p < CoverPage.PhaseCount; p++)
            for (int d = -1; d <= 1; d += 2)
            {
                int next = CoverPage.StepPhase(p, d);
                int nav = FigmaUI.PhaseNav(next);
                int shown = nav >= 0 ? p : next;   // navigating leaves coverPhase where it was
                Check("arrow " + (d > 0 ? "+" : "-") + " from " + p + " never parks the Cover on a page",
                      FigmaUI.PhaseNav(shown) < 0, "would show slot " + shown);
            }

        // 5. And slot 6 is genuinely no longer reachable as a DISPLAYED phase - the fault the render
        //    ui_cover_phase6.png showed. (That PNG still exists: the preview asks CoverPage.Build for
        //    the slot directly, below the layer that decides reachability. It is a fixture, not a state.)
        bool reachable = false;
        for (int p = 0; p < CoverPage.PhaseCount; p++)
            for (int d = -1; d <= 1; d += 2)
            {
                int next = CoverPage.StepPhase(p, d);
                if (next == 6 && FigmaUI.PhaseNav(6) < 0) reachable = true;
            }
        Check("no arrow step can display rail slot 6", !reachable, "");
    }

    // ---- S109 / QC NO-01: a range ring is an OVERLAY, so it goes ON TOP ----
    // The four rings used to be emitted BEFORE NavPage.Orbit, which draws the body disc over them, so
    // the page drew four rings and rendered one. Draw ORDER is not visible in any single command, only
    // in the sequence - which is exactly what a display-list test can see and a PNG diff cannot explain.
    static void RangeRingsOnTop()
    {
        var dl = new DisplayList(FigmaUI.Commands);
        var s = new PageState();
        s.Valid = true;
        s.BodyRadiusM = 600000.0; s.ApogeeM = 124000.0; s.PerigeeM = 121900.0;
        s.AltitudeM = 123600.0;
        FigmaUI.Build(dl, UiPage.NavOrbitPlot, W, H, s, MapProjection.Default());

        // The plot well, in panel space - the globe is drawn inside it. ⚠ The obvious probe ("the last
        // Image command") is WRONG and this test caught it: BottomBar.Draw emits an Asset after
        // everything else, so "last Image" is the nav bar, 600 px below the plot. Only Images INSIDE
        // the well can be the body.
        float psc = (float)H / RefH, pox = (W - RefW * psc) * 0.5f;
        float wx = 380f * psc + pox, wy = 180f * psc, ww = 2667f * psc, wh = 1670f * psc;

        int lastBody = -1, rings = 0, firstRing = int.MaxValue;
        for (int c = 0; c < dl.Count; c++)
        {
            var cmd = dl.At(c);
            if (cmd.Kind == DrawKind.Image
                && cmd.A >= wx - 1f && cmd.B >= wy - 1f
                && cmd.A + cmd.C <= wx + ww + 1f && cmd.B + cmd.D <= wy + wh + 1f) lastBody = c;
            // a ring: a full-circle ArcBand in the ring tint
            if (cmd.Kind == DrawKind.ArcBand && cmd.StartDeg == 0.0 && cmd.EndDeg == 360.0
                && Same(cmd.Colour, DragonPalette.Text7))
            {
                rings++;
                if (c < firstRing) firstRing = c;
            }
        }

        Check("nav plot draws all four range rings", rings == 4, "got " + rings);
        // The guard against a vacuous pass: if the fixture drew no body there is nothing to be on top of.
        Check("nav plot drew a body to be on top of", lastBody >= 0, "no Image command");
        if (lastBody >= 0)
            Check("the range rings are drawn AFTER the body disc", firstRing > lastBody,
                  "first ring at " + firstRing + ", last body draw at " + lastBody);
    }

    static bool Same(Rgba a, Rgba b)
    {
        return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
    }

    // ---- S108 / QC H-04 + DK-04 + H-06: the drawn rect and the hit rect are ONE rect ----
    // H-04's own verify line: "a headless check that the drawn rect and the hit rect are the same rect,
    // for both the HUD and Docking margin affordances. That check is what would have caught this."
    static void MarginAffordances()
    {
        UiPage[] pages = { UiPage.Hud, UiPage.Docking };
        UiPage[] dest  = { UiPage.Docking, UiPage.Rendezvous };
        string[] la    = { "MANUAL", "RENDEZVOUS" };
        string[] lb    = { "DOCKING", null };

        for (int p = 0; p < pages.Length; p++)
        {
            float x, y, bw, bh;
            Check(pages[p] + ": the margin box exists at the shipped size",
                  MarginAffordance.Rect(W, H, out x, out y, out bw, out bh), "");

            // 1. THE CONTROL IS ACTUALLY PAINTED. This is the whole of DK-04: the Docking page's
            //    rectangle fired for years with nothing drawn in it.
            var dl = new DisplayList(FigmaUI.Commands);
            var st = new PageState();
            FigmaUI.Build(dl, pages[p], W, H, st, MapProjection.Default());
            bool drewBox = false, drewLabel = false;
            for (int c = 0; c < dl.Count; c++)
            {
                var cmd = dl.At(c);
                // DisplayList.Box is four Rects, so the thing to look for is the FILLED plate the
                // helper lays down first - at exactly the rect the hit test uses.
                if (cmd.Kind == DrawKind.Rect && Near(cmd.A, x) && Near(cmd.B, y)
                    && Near(cmd.C, bw) && Near(cmd.D, bh)) drewBox = true;
                if (cmd.Kind == DrawKind.Text && cmd.Str == la[p]) drewLabel = true;
            }
            Check(pages[p] + ": the margin affordance is DRAWN at the shared rect", drewBox, "");
            Check(pages[p] + ": its label \"" + la[p] + "\" is drawn", drewLabel, "");

            // 2. THE HIT RECT IS THE SAME RECT. Inside every edge hits; just outside every edge misses.
            //    Before S108 the band was 0.40..0.60 h behind a 0.44..0.56 h box, so the two "outside
            //    vertically" probes below would both have navigated from blank letterbox.
            Check(pages[p] + ": centre of the box routes to " + dest[p],
                  Route(pages[p], x + bw * 0.5f, y + bh * 0.5f) == dest[p], "");
            Check(pages[p] + ": 4 px ABOVE the box is inert",
                  Route(pages[p], x + bw * 0.5f, y - 4f) != dest[p], "");
            Check(pages[p] + ": 4 px BELOW the box is inert",
                  Route(pages[p], x + bw * 0.5f, y + bh + 4f) != dest[p], "");
            Check(pages[p] + ": 4 px LEFT of the box is inert",
                  Route(pages[p], x - 4f, y + bh * 0.5f) != dest[p], "");
            Check(pages[p] + ": 4 px RIGHT of the box is inert",
                  Route(pages[p], x + bw + 4f, y + bh * 0.5f) != dest[p], "");
            Check(pages[p] + ": just inside the top edge hits",
                  Route(pages[p], x + bw * 0.5f, y + 1f) == dest[p], "");
            Check(pages[p] + ": just inside the bottom edge hits",
                  Route(pages[p], x + bw * 0.5f, y + bh - 1f) == dest[p], "");

            // 3. H-06: THE INK STAYS INSIDE THE BOX. "MANUAL" used to render 56 px of ink in a 45.6 px
            //    box - 4.0 px over the left border and 5.4 px over the right.
            float ts = MarginAffordance.FitSize(bw, H * 0.020f, la[p], lb[p], Typography.ScaleFor(W));
            float ink = MarginAffordance.InkWidth(ts, la[p], lb[p]);
            Check(pages[p] + ": the widest label's ink clears both borders",
                  ink <= bw - 8f + 0.01f,
                  "ink " + ink.ToString("0.0") + " in box " + bw.ToString("0.0"));
        }

        // 4. THE MinMargin GUARD HOLDS ON BOTH SIDES TOGETHER - which is what sharing one function buys.
        //    A panel with no letterbox must draw nothing AND hit nothing; the old code could only get
        //    that right by two separate `ox > 40f` tests staying in step.
        {
            int nw = 1140, nh = 703;   // 1.62:1, essentially the design aspect -> no margin
            float x, y, bw, bh;
            bool has = MarginAffordance.Rect(nw, nh, out x, out y, out bw, out bh);
            Check("no letterbox -> no margin box", !has, "");
            Check("no letterbox -> the HUD margin cannot be hit",
                  !MarginAffordance.Hit(6f, nh * 0.5f, nw, nh), "");
            var dl = new DisplayList(FigmaUI.Commands);
            FigmaUI.Build(dl, UiPage.Hud, nw, nh, new PageState(), MapProjection.Default());
            bool drewLabel = false;
            for (int c = 0; c < dl.Count; c++)
                if (dl.At(c).Kind == DrawKind.Text && dl.At(c).Str == "MANUAL") drewLabel = true;
            Check("no letterbox -> the HUD draws no margin label", !drewLabel, "");
        }

        // 5. \u26d4 REPORTED, NOT ASSERTED: whether the fitted type clears the floor. This suite
        //    runs at W = 1280, where Typography.MinFor(W) IS the measured 16 - but it is written as
        //    MinFor(W) so the yardstick follows the width if this harness size ever moves (QC R-02).
        //    At the shipped size the HUD's does not, and RENDEZVOUS is far below it - the box is 61.6
        //    px wide and the word needs 106 px at 16 px type. That is a DESIGN question about the
        //    margin's width (Q8), not something a fit can solve, and failing the build on it would
        //    block the H-04 fix that stands on its own. Printed so it cannot be forgotten.
        {
            float x, y, bw, bh;
            MarginAffordance.Rect(W, H, out x, out y, out bw, out bh);
            Console.WriteLine("  note  margin affordance type at " + W + "x" + H + ": MANUAL/DOCKING "
                + MarginAffordance.FitSize(bw, H * 0.020f, "MANUAL", "DOCKING", Typography.ScaleFor(W)).ToString("0.00")
                + " px, RENDEZVOUS "
                + MarginAffordance.FitSize(bw, H * 0.020f, "RENDEZVOUS", null, Typography.ScaleFor(W)).ToString("0.00")
                + " px, floor " + Typography.MinFor(W) + "  (QC H-06 / Q8)");
        }
    }

    static bool Near(float a, float b) { float d = a - b; return d < 0.01f && d > -0.01f; }

    static UiPage Route(UiPage from, float px, float py)
    {
        NavHit nh = FigmaUI.HitTest(from, px, py, W, H);
        return nh.Act == NavAct.Goto ? nh.Target : from;
    }

    // ---- S107 / QC M-01: the grid is derived from the data, and stays legible ----
    static void MenuGridFits()
    {
        int n = MenuPage.Entries.Length;
        float x, y, cw, ch;

        // Every card is inside the grid band AND clear of the bottom bar. `Rows` used to be a typed
        // constant that had already been bumped by hand once; at Rows = 10 the 31st entry would have
        // landed at design y 1854..1994 - drawn, mostly under the bar (which starts at 1877), and
        // rejected outright by HitTest's `dy0 > Bottom` guard at 1830. Derived, that cannot happen.
        MenuPage.CellRect(n - 1, out x, out y, out cw, out ch);
        Check("menu: the last card ends inside the grid band", y + ch <= 1830f + 0.01f,
              "ends at " + (y + ch));
        Check("menu: the last card clears the bottom bar", y + ch <= 1877f, "ends at " + (y + ch));

        // And every card is TAPPABLE - the failure mode was a visible card the hit test refused.
        for (int i = 0; i < n; i++)
        {
            MenuPage.CellRect(i, out x, out y, out cw, out ch);
            float px = (x + cw * 0.5f) * W / RefW, py = (y + ch * 0.5f) * H / RefH;
            Check("menu card " + i + " (" + MenuPage.Entries[i] + ") is tappable",
                  MenuPage.HitTest(px, py, W, H) == i, "got " + MenuPage.HitTest(px, py, W, H));
        }

        // ⚠ THE ONE THAT WILL ACTUALLY FIRE ONE DAY. Deriving the row count fixes the overflow but
        // not the squeeze: the pitch is fixed by (Bottom - Top), so each appended page makes every
        // cell shorter. When a cell can no longer hold its own label the grid must PAGINATE, not
        // shrink - that is C-05's guard and real work. This fails the build on the append that
        // crosses the line, instead of shipping an illegible menu.
        float cellPanelPx = MenuPage.CellHeight * H / RefH;
        float labelPanelPx = MenuPage.LabelSize * H / RefH;
        Check("menu: a cell still fits its own label with room around it",
              cellPanelPx >= labelPanelPx * 2f,
              "cell " + cellPanelPx.ToString("0.0") + "px, label " + labelPanelPx.ToString("0.0") + "px");

        // ⛔ NOT ASSERTED HERE, DELIBERATELY: whether the label clears Typography.Min. It does not -
        // SZ(32) is 10.7 panel px against a floor of 16 - but that is QC R-01, which samples this very
        // element at 67% of the floor along with 16 others across 9 pages, and R-01 is one owner
        // decision (Q5) for all of them. Failing the build here would turn one page's grid fix into a
        // red build for a page-wide question the owner has not answered, and would have to be undone
        // whichever way Q5 goes. The RATIO check above is this finding's own: it is about the grid
        // squeezing its cells, and it holds whatever the absolute size turns out to be.
    }

    // ---- S107 / QC M-02: the placeholder card's copy is only true while this is ----
    // The card now says "no button in this build opens this page". That is a claim ABOUT THE BUILD,
    // so it needs a check that fails when it stops being true - otherwise it rots exactly the way the
    // sentence it replaced did ("this button is wired; the destination is coming", true when written,
    // false the moment S14 took these values off the Menu grid, and left standing for months).
    static void PlaceholderUnreachable()
    {
        // Nothing on the Menu grid.
        for (int j = 0; j < MenuPage.Entries.Length; j++)
            Check("menu entry " + j + " is a real page",
                  !FigmaUI.IsPlaceholder(MenuPage.Entries[j]), "got " + MenuPage.Entries[j]);

        // And nothing ROUTES to one, from any page, anywhere on the glass. A coarse sweep is enough:
        // every nav rect on every page is far bigger than this grid's step.
        int hits = 0;
        for (int i = 0; i < FigmaUI.PageCount; i++)
            for (int gx = 0; gx < 64; gx++)
                for (int gy = 0; gy < 36; gy++)
                {
                    NavHit nh = FigmaUI.HitTest((UiPage)i, (gx + 0.5f) * W / 64f, (gy + 0.5f) * H / 36f, W, H);
                    if (nh.Act != NavAct.Goto) continue;
                    hits++;
                    if (FigmaUI.IsPlaceholder(nh.Target))
                        Check("page " + (UiPage)i + " routes to placeholder " + nh.Target, false,
                              "at " + gx + "," + gy);
                }
        Check("the nav sweep actually found routes (guards against a vacuous pass)", hits > 0,
              "got " + hits);
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

        // ---- S54 / audit H8: A CONTROL THAT IS NOT DRAWN MUST NOT FIRE ----
        // On rail slot 5 (Reference Content) `CoverPage.Build` swaps the baked panel BODY out and draws the
        // deorbit quick-reference over that space. Six `Hits` rows are labels inside that swapped-out body:
        // the four Act* rows and the two Entry rows. Their rectangles used to fire anyway — over the ENTRY
        // TIMELINE / PARACHUTES / CONTINGENCY text — which is harmless only while the targets are no-ops.
        // Wire the Cover actions (H5) with this unfixed and tapping reference text triggers deorbit actions.
        // Each pair below aims at the SAME pixel twice: it must hit on a normal phase and miss on slot 5.
        {
            const int RefPhase = 5;   // CoverPage.ReferencePhase — private, so mirrored (and pinned below)
            float ox = W - RefW * sc; // right-block reflow offset, as the Hits table's own x's are frame-local
            // button | frame x,y of a point INSIDE its rect (from CoverPage.Hits) | is it right-of-Split
            // ⚠ S129 TOOK THE TWO ENTRY ROWS OUT OF THIS LIST, and the paragraph above is kept verbatim
            // because it is still the reason the FOUR that remain are tested this way. The Entry rows
            // are no longer hit-testable AT ALL - QC C-08 / the 2026-09-06 overseer settlement made
            // ENTRY ENABLED a READOUT of the autopilot's readiness check rather than a crew control -
            // so "hits on a normal phase, misses on slot 5" is no longer the property to assert about
            // them. ⭐ The stronger assertion that replaces it is below, and it covers every phase.
            float[][] hidden = new float[][] {
                new float[] { 800f, 950f },    // ActOnSpaceX      779,930  591x60
                new float[] { 1150f, 1010f },  // ActDeorbitBrief 1093,996  277x60
                new float[] { 1000f, 1080f },  // ActReview        964,1062 406x60
                new float[] { 1200f, 1140f },  // ActAcknowledge  1158,1128 212x60
            };
            CoverPage.CoverButton[] hidWant = {
                CoverPage.CoverButton.ActOnSpaceX, CoverPage.CoverButton.ActDeorbitBrief,
                CoverPage.CoverButton.ActReview, CoverPage.CoverButton.ActAcknowledge };

            for (int i = 0; i < hidden.Length; i++)
            {
                // ⚠ S174 — "all six sit left of the 1500 Split, so the frame->panel map is a plain
                // scale" was true until the slack redistribution and is NOT any more. These four points
                // (design x 779..1370) are INSIDE the content panel, which now takes a share of the
                // horizontal slack and stretches its interior with it. A test that keeps its own copy of
                // the map is the third copy PageAction's rule exists to prevent — *one rect shared by
                // the draw, the hit test AND THE TEST* — so this aims through the same function the page
                // draws with. It failed loudly when the map changed under it, which is the point of it.
                float px = SplitReflow.X(hidden[i][0], W, H), py = hidden[i][1] * sc;
                Check("cover " + hidWant[i] + " hits on a normal phase (phase 0)",
                      CoverPage.HitTest(px, py, W, H, CoverPage.CoverCam.Earth, 0) == hidWant[i],
                      "got " + CoverPage.HitTest(px, py, W, H, CoverPage.CoverCam.Earth, 0));
                Check("cover " + hidWant[i] + " does NOT fire on Reference Content (phase 5)",
                      CoverPage.HitTest(px, py, W, H, CoverPage.CoverCam.Earth, RefPhase)
                          == CoverPage.CoverButton.None,
                      "got " + CoverPage.HitTest(px, py, W, H, CoverPage.CoverCam.Earth, RefPhase));
                // every OTHER phase leaves it live - the gate is slot 5's alone, not a blanket disable
                for (int ph = 0; ph < CoverPage.PhaseCount; ph++)
                {
                    if (ph == RefPhase) continue;
                    Check("cover " + hidWant[i] + " still live on phase " + ph,
                          CoverPage.HitTest(px, py, W, H, CoverPage.CoverCam.Earth, ph) == hidWant[i], "");
                }
            }

            // ---- S129 / QC C-08: THE ENTRY ROWS ARE A READOUT AND TAKE NO TOUCH, ON ANY PHASE ----
            // They had a hit rect and no dispatcher case anywhere in the tree, which promised the crew
            // a touch that could never do anything - and over a SAFETY VERDICT they are not allowed to
            // set. Both rectangles are gone. Asserted at the exact pixels the old rows used, across
            // every phase, so restoring either one has to be a deliberate act with a test to change.
            foreach (float[] pt in new float[][] { new float[] { 800f, 1560f },      // was EntryTrue
                                                   new float[] { 1150f, 1560f } })   // was EntryFalse
                for (int ph = 0; ph < CoverPage.PhaseCount; ph++)
                    Check("cover ENTRY ENABLED takes no touch (phase " + ph + ", x " + pt[0] + ")",
                          CoverPage.HitTest(pt[0] * sc, pt[1] * sc, W, H,
                                            CoverPage.CoverCam.Earth, ph) == CoverPage.CoverButton.None,
                          "got " + CoverPage.HitTest(pt[0] * sc, pt[1] * sc, W, H,
                                                     CoverPage.CoverCam.Earth, ph));

            // WHAT IS STILL DRAWN ON SLOT 5 IS STILL TOUCHABLE. The gate must not cost the crew the rail,
            // the chrome or the camera on the one phase that most needs a way out of itself.
            Check("Reference Content: Menu still hits",
                  CoverPage.HitTest(98f * sc, 108f * sc, W, H, CoverPage.CoverCam.Earth, RefPhase)
                      == CoverPage.CoverButton.Menu, "");
            Check("Reference Content: Settings still hits",
                  CoverPage.HitTest(3194f * sc + ox, 1865f * sc, W, H, CoverPage.CoverCam.Earth, RefPhase)
                      == CoverPage.CoverButton.Settings, "");
            Check("Reference Content: Back still hits",
                  CoverPage.HitTest(300f * sc, 300f * sc, W, H, CoverPage.CoverCam.Earth, RefPhase)
                      == CoverPage.CoverButton.Back, "");
            Check("Reference Content: Forward still hits",
                  CoverPage.HitTest(420f * sc, 300f * sc, W, H, CoverPage.CoverCam.Earth, RefPhase)
                      == CoverPage.CoverButton.Forward, "");
            for (int i = 0; i < CoverPage.PhaseCount; i++)
                Check("Reference Content: rail row " + i + " still selectable",
                      CoverPage.HitTest(110f * sc, (slotY[i] + 80f) * sc, W, H,
                                        CoverPage.CoverCam.Earth, RefPhase) == want[i], "");

            // The mirrored slot index is pinned to the rail, so a re-pitch of the phases cannot silently
            // point this gate at the wrong row: slot 5 IS the phase whose button is PhaseReference.
            Check("slot 5 is the Reference Content phase (the mirrored index is still right)",
                  CoverPage.PhaseOf(CoverPage.CoverButton.PhaseReference) == RefPhase,
                  "got " + CoverPage.PhaseOf(CoverPage.CoverButton.PhaseReference));

            // The legacy overloads keep their pre-S54 behaviour (NoPhase = every row live) — the painter
            // is the caller that dispatches, and it passes the real phase.
            // S174: design x 1000 is inside the content panel, which the slack redistribution stretches,
            // so the aim goes through the page's own map rather than a plain scale.
            Check("the phase-less overload still resolves the Act* rows (NoPhase, not slot 5)",
                  CoverPage.HitTest(SplitReflow.X(1000f, W, H), 1080f * sc, W, H) == CoverPage.CoverButton.ActReview, "");
        }
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
        SuitCheckPage.Build(dok, VW, VH, 0, true, ok, false);
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
        SuitCheckPage.Build(dbad, VW, VH, 0, true, bad, false);
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
        SuitCheckPage.Build(dtable, VW, VH, 0, false, bad, false);          // the box closed: what the crew acts in
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

        // ================= S158 / S49 H19 / QC SC-01: THE PROCEDURE'S OWN STEP FLOW =================
        // Both left ticks used to draw White unconditionally, so the page showed a two-step procedure
        // COMPLETE before it began. What follows pins the three-way that replaced them, and it is
        // written around the TWO STATES A COUNTDOWN-ONLY IMPLEMENTATION CANNOT SEE, because those are
        // the whole reason `runActive` is threaded in:
        //   • el < 0.9 s   the counter still reads 5, which is ALSO its idle value  -> under-claims
        //   • el in [4.5,5) the counter already reads 0, which is ALSO its finished value, and the
        //                   popup has not been raised yet                            -> OVER-claims
        // The second is the one that matters: it would tick "EXECUTE SUIT LEAK CHECK" half a second
        // before a result exists — a smaller copy of the defect this task removes.
        const float TickY1 = 452f / 2112f, TickY2 = 560f / 2112f;   // SuitCheckPage's own design frame
        Check("StepOf: an unopened procedure has not started",
              SuitCheckPage.StepOf(5, false, false) == SuitCheckPage.ProcStep.NotStarted, "");
        Check("StepOf: the first 0.9s of a run is RUNNING even though the counter still reads 5",
              SuitCheckPage.StepOf(5, false, true) == SuitCheckPage.ProcStep.Running, "");
        Check("StepOf: the last 0.5s of a run is RUNNING even though the counter already reads 0",
              SuitCheckPage.StepOf(0, false, true) == SuitCheckPage.ProcStep.Running, "");
        Check("StepOf: a raised result box is a completed run",
              SuitCheckPage.StepOf(0, true, false) == SuitCheckPage.ProcStep.Complete, "");
        Check("StepOf: a completed run stays complete once its box is closed",
              SuitCheckPage.StepOf(0, false, false) == SuitCheckPage.ProcStep.Complete, "");
        // HALT sets countdown 5 + popup false + suitStart -1 (ScreenPainter:534), and a page change does
        // the same (:789). An abandoned run is NOT a done one: the crew stopped it.
        Check("StepOf: HALT puts the procedure back to not-started",
              SuitCheckPage.StepOf(5, false, false) == SuitCheckPage.ProcStep.NotStarted, "");

        // ---- and the PAGE draws that three-way, tick and label together ----
        DisplayList dIdle = new DisplayList(SuitCheckPage.Commands + 60);
        SuitCheckPage.Build(dIdle, VW, VH, 5, false, SuitLeak.From(s, 5, false, 0u), false);
        DisplayList dRunA = new DisplayList(SuitCheckPage.Commands + 60);
        SuitCheckPage.Build(dRunA, VW, VH, 5, false, SuitLeak.From(s, 5, false, clean), true);
        DisplayList dRunZ = new DisplayList(SuitCheckPage.Commands + 60);
        SuitCheckPage.Build(dRunZ, VW, VH, 0, false, SuitLeak.From(s, 0, false, clean), true);
        DisplayList dDone = new DisplayList(SuitCheckPage.Commands + 60);
        SuitCheckPage.Build(dDone, VW, VH, 0, false, SuitLeak.From(s, 0, false, clean), false);

        Check("before a run BOTH steps are unticked",
              SameColour(AssetTintAtFrac(dIdle, "ic_check", TickY1, VH), DragonPalette.Text6) &&
              SameColour(AssetTintAtFrac(dIdle, "ic_check", TickY2, VH), DragonPalette.Text6),
              "t1 " + AssetTintAtFrac(dIdle, "ic_check", TickY1, VH).R.ToString("F2") +
              " t2 " + AssetTintAtFrac(dIdle, "ic_check", TickY2, VH).R.ToString("F2"));
        Check("before a run BOTH step labels are unticked too",
              SameColour(ColourOf(dIdle, "1. PREPARE SUITS FOR LEAK CHECK"), DragonPalette.Text6) &&
              SameColour(ColourOf(dIdle, "2. EXECUTE SUIT LEAK CHECK"), DragonPalette.Text6), "");
        Check("the first second of a run ticks step 1 and NOT step 2",
              SameColour(AssetTintAtFrac(dRunA, "ic_check", TickY1, VH), DragonPalette.White) &&
              SameColour(AssetTintAtFrac(dRunA, "ic_check", TickY2, VH), DragonPalette.Text6), "");
        // THE OVER-CLAIM GUARD. This is the state a countdown-only page gets wrong.
        Check("the last half-second of a run still has step 2 UNTICKED",
              SameColour(AssetTintAtFrac(dRunZ, "ic_check", TickY2, VH), DragonPalette.Text6) &&
              SameColour(ColourOf(dRunZ, "2. EXECUTE SUIT LEAK CHECK"), DragonPalette.Text6), "");
        Check("a finished run ticks both steps",
              SameColour(AssetTintAtFrac(dDone, "ic_check", TickY1, VH), DragonPalette.White) &&
              SameColour(AssetTintAtFrac(dDone, "ic_check", TickY2, VH), DragonPalette.White), "");
        Check("the result box is up on a finished run and both steps are ticked",
              SameColour(AssetTintAtFrac(dok, "ic_check", TickY1, VH), DragonPalette.White) &&
              SameColour(AssetTintAtFrac(dok, "ic_check", TickY2, VH), DragonPalette.White), "");

        // ⛔ QC SC-01's own "must not break": the TICK means DONE, the STATUS column means PASSED, and
        // they are two different claims. A check that ran and found a leak is COMPLETE — ticking step 2
        // off `AnyFailed` would make the page state a verdict in two places, which is the one thing this
        // page has never done. dtable is exactly that run with its box closed.
        Check("a completed run that FAILED still ticks step 2 — done is not passed",
              bad.AnyFailed &&
              SameColour(AssetTintAtFrac(dtable, "ic_check", TickY2, VH), DragonPalette.White), "");

        // The step flow reads the PROCEDURE, not the vessel: a dead feed dashes the table (checked
        // above) and must not un-run a run the crew actually made.
        DisplayList dDead = new DisplayList(SuitCheckPage.Commands + 60);
        SuitCheckPage.Build(dDead, VW, VH, 0, true, SuitLeak.From(nofeed, 0, true, leaky), false);
        Check("a dead feed does not un-tick a completed procedure",
              SameColour(AssetTintAtFrac(dDead, "ic_check", TickY2, VH), DragonPalette.White), "");

        // ⚠ THE RESIDUAL, PINNED ON PURPOSE. "SECTION 2: IN PROGRESS" is reference copy and the words
        // for its other states are attested in NO source — tier 1 (the capsule photographs) or tier 2
        // (Fourth.vue, which hardcodes this same string). §1.4 sends that to the owner, so the header
        // stays a literal in every state. This check exists so that a later chat inventing "COMPLETE"
        // or "NOT STARTED" trips a test and has to go and get the ruling, rather than quietly shipping
        // vocabulary. Delete it when the owner has answered — not before.
        Check("the section header is still a literal in all four states (S158's held half)",
              Drew(dIdle, "SECTION 2: IN PROGRESS") && Drew(dRunA, "SECTION 2: IN PROGRESS") &&
              Drew(dRunZ, "SECTION 2: IN PROGRESS") && Drew(dDone, "SECTION 2: IN PROGRESS"), "");
    }

    /// <summary>Did the page draw this ASSET at all? The ENTRY ENABLED row's whole defect was that its
    /// answer was a PNG, so "the PNG is gone" is half of the fix and has to be asserted, not eyeballed.
    /// </summary>
    static bool DrewAsset(DisplayList dl, string key)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Image && c.AssetKey == key) return true;
        }
        return false;
    }

    /// <summary>The pixel size the page drew this exact string at. The R-01 policy is a statement about
    /// SIZE, so a check on it needs the size and not the colour.</summary>
    static float SizeOf(DisplayList dl, string text)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == text) return c.C;
        }
        return 0f;
    }

    /// <summary>The size of a string drawn NEAR a given panel point. ⚠ Needed because SizeOf takes the
    /// FIRST match and the Cover draws the project dash in eight places - the seven top-strip readouts
    /// resolve to one whenever their field is empty, and they are drawn before the ENTRY ENABLED row.
    /// The first version of the S129 size check found a top-strip dash at 29.3 px and reported the
    /// wrong element as failing; the row itself was correct all along. Matching by POSITION is what
    /// makes the check about the element it names.</summary>
    static float SizeOfNear(DisplayList dl, string text, float x, float y, float tol)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Text || c.Str != text) continue;
            if (Math.Abs(c.A - x) <= tol && Math.Abs(c.B - y) <= tol) return c.C;
        }
        return 0f;
    }

    // ================= S137 / S49 H14: THE ALERTS VIEW LISTS WHAT IS WRONG =================
    // The panel showed ONE WORD at 110 design px and nothing else, beside a FDIR "gauge" whose fill was
    // 0.15 / 0.6 / 1 chosen by severity - a bar implying a magnitude that does not exist for a
    // three-valued enum.
    static void AlertsView()
    {
        const int VW = 2560, VH = 1406;
        AlertItem[] buf = new AlertItem[AlertList.Max];

        // ---- 1. THE INVARIANT THAT MAKES THE PANEL CORRECT --------------------------------------
        // ⭐ The list is the WORKING of the word above it, so the worst row must BE the word - for
        // every scope, in every state. A panel that could show one and say the other is the "one
        // panel, two answers" defect S51 fixed here once already, and the first version of this line
        // had it: a whole-vehicle list under a per-subsystem word.
        AlertScope[] scopes = { AlertScope.LifeSupport, AlertScope.Thermal, AlertScope.Power,
                                AlertScope.Propellant, AlertScope.Fdir };
        int states = 0, mismatches = 0, withRows = 0;
        for (int k = 0; k < 40; k++)
        {
            PageState s2 = AlertFixture(k);
            foreach (AlertScope sc in scopes)
            {
                states++;
                int n = AlertList.Build(s2, sc, buf);
                Severity worst = Severity.Nominal;
                for (int i = 0; i < n; i++) if (buf[i].Sev > worst) worst = buf[i].Sev;
                if (worst != AlertList.SeverityOf(s2, sc)) mismatches++;
                if (n > 0) withRows++;
                // worst-first ordering, on every one of them
                for (int i = 1; i < n; i++)
                    if (buf[i].Sev > buf[i - 1].Sev) mismatches++;
            }
        }
        Check("the worst row IS the word, across " + states + " scope/state combinations",
              mismatches == 0, mismatches + " disagreed");
        Check("...and the sweep actually produced alerts to compare", withRows > 20,
              "only " + withRows + " of " + states + " had rows - the fixture is not exercising it");

        // ---- 2. A NOMINAL BAND IS NOT A ROW, AND A DEAD FEED IS NOT A LIST ----------------------
        PageState good = AlertFixture(0);
        Check("a nominal life-support state lists nothing",
              AlertList.Build(good, AlertScope.LifeSupport, buf) == 0, "");
        PageState dead = AlertFixture(7); dead.Valid = false;
        int dn = 0;
        foreach (AlertScope sc in scopes) dn += AlertList.Build(dead, sc, buf);
        Check("a dead feed lists nothing at all, in any scope", dn == 0, "got " + dn + " rows");

        // ---- 3. THE ROW CANNOT DISAGREE WITH ITSELF ---------------------------------------------
        // ⛔ Caught in the preview: the first version banded one field and printed another, so a
        // fixture that moved only the numeric produced "PPO2 2.86" in ALARM RED. The value is now
        // formatted from the same number that was banded, so the reading proves the verdict.
        PageState p2 = AlertFixture(0);
        p2.Cabin.Ppo2Psia = 1.5;                     // hard alarm
        p2.Ppo2Text = "9.99 psia";                   // a stale display field, deliberately wrong
        int n2 = AlertList.Build(p2, AlertScope.LifeSupport, buf);
        Check("the row prints the number it was banded on, not a stale display field",
              n2 >= 1 && buf[0].Label == "PPO2" && buf[0].Value.StartsWith("1.5")
              && buf[0].Sev == Severity.Alarm,
              n2 >= 1 ? buf[0].Label + " = " + buf[0].Value : "no rows");

        // ⛔ THE SAME TRAP, ON THE ONE ROW WHERE THE TWO FIELDS ARE GENUINELY DIFFERENT QUANTITIES.
        // `Alarms.PropellantSeverity` bands `DragonProp01` on purpose - its own comment says the
        // whole-stack `Propellant01` is meaningless once the booster is gone - so a row printing
        // `Propellant01` would cite a healthy 90% beside an alarm reached on 5%. ⚠ A fixture that
        // sets both to the same value cannot see this, which is exactly what let it survive one
        // mutation run.
        PageState pp = AlertFixture(0);
        pp.DragonProp01 = 0.05;   // the field the verdict is reached on: hard alarm
        pp.Propellant01 = 0.90;   // the whole-stack figure: looks fine
        int n3 = AlertList.Build(pp, AlertScope.Propellant, buf);
        Check("the propellant row cites the fraction its own verdict was reached on",
              n3 == 1 && buf[0].Sev == Severity.Alarm && buf[0].Value == "5%",
              n3 == 1 ? "row reads " + buf[0].Value : "rows " + n3);

        // ---- 4. THE PAGE: THE WORD AND THE WORST ROW ARE ONE COLOUR -----------------------------
        PageState alarmed = AlertFixture(0);
        alarmed.Cabin.Ppo2Psia = 1.8; alarmed.Cabin.Co2MmHg = 9.0;
        DisplayList dl = Subsys(VehicleSubsystemPage.Sub.Crew, alarmed, VW, VH);
        Check("the alerted page draws the failing quantities by name",
              Drew(dl, "PPO2") && Drew(dl, "CO2"), "");
        Check("...at the LIVE floor, because an alert nobody can read is not an alert",
              SizeOf(dl, "PPO2") >= Typography.MinFor(VW),
              "got " + SizeOf(dl, "PPO2") + ", floor " + Typography.MinFor(VW));
        Check("the summary word and the worst row are the same colour",
              SameColour(ColourOf(dl, Alarms.Word(Alarms.LifeSupport(alarmed.Cabin))),
                         ColourOf(dl, "PPO2")), "");

        // A nominal page lists nothing - the word alone is the right answer when nothing is wrong.
        DisplayList ok = Subsys(VehicleSubsystemPage.Sub.Crew, good, VW, VH);
        Check("a nominal page lists no rows", !Drew(ok, "PPO2") && !Drew(ok, "CO2"), "");

        // ---- 5. THE FDIR BAR IS A POSITION INDICATOR, NOT A FILL --------------------------------
        // Three equal segments at design y 410; exactly ONE is lit while there is a feed, and NONE
        // when there is not. The old bar drew two rects - a track and a fraction of it.
        Check("the FDIR bar is three segments and exactly one is lit",
              BarSegments(dl, VW, VH, true) == 1 && BarSegments(dl, VW, VH, false) == 2,
              "lit " + BarSegments(dl, VW, VH, true) + ", unlit " + BarSegments(dl, VW, VH, false));
        DisplayList nofeed = Subsys(VehicleSubsystemPage.Sub.Crew, dead, VW, VH);
        Check("...and with no feed none of them is lit",
              BarSegments(nofeed, VW, VH, true) == 0 && BarSegments(nofeed, VW, VH, false) == 3,
              "lit " + BarSegments(nofeed, VW, VH, true));
    }

    /// <summary>Rects sitting on the FDIR bar's own row (design y 410, height 8). `lit` counts the
    /// ones that are NOT the dim track colour.</summary>
    static int BarSegments(DisplayList dl, int w, int h, bool lit)
    {
        float sy = (float)h / 2112f;
        float y = 410f * sy, hh = 8f * sy;
        int n = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Rect) continue;
            if (Math.Abs(c.B - y) > 1f || Math.Abs(c.D - hh) > 1f) continue;
            bool isLit = !SameColour(c.Colour, DragonPalette.Text7)
                         && !SameColour(c.Colour, DragonPalette.Text8)
                         && !SameColour(c.Colour, DragonPalette.Hairline);
            if (isLit == lit) n++;
        }
        return n;
    }

    static DisplayList Subsys(VehicleSubsystemPage.Sub sub, PageState s, int w, int h)
    {
        DisplayList dl = new DisplayList(VehicleSubsystemPage.Commands + 80);
        VehicleSubsystemPage.Build(dl, w, h, sub, s, true);
        return dl;
    }

    /// <summary>A spread of cabin/power/propellant states, deterministic in k, that walks every band
    /// through nominal, caution and alarm so the invariant above is exercised rather than asserted
    /// against one happy value.</summary>
    static PageState AlertFixture(int k)
    {
        PageState s = new PageState();
        s.Valid = true;
        s.Cabin.Ppo2Psia    = 3.2 - 0.05 * (k % 30);
        s.Cabin.Co2MmHg     = 1.0 + 0.25 * (k % 32);
        s.Cabin.PressPsia   = 14.7 - 0.15 * (k % 30);
        s.Cabin.CabinTempC  = 20.0 + 0.6 * (k % 30);
        s.Cabin.LoopAC      = 30.0 + 1.0 * (k % 30);
        s.Cabin.LoopBC      = 28.0 + 1.1 * ((k * 7) % 30);
        s.Power01           = 1.0 - 0.03 * (k % 33);
        // ⚠ BOTH propellant fields, and the severity reads DragonProp01 - `Alarms.PropellantSeverity`
        // bands the Dragon-only fraction on purpose. A fixture that set only `Propellant01` left every
        // sweep state in propellant ALARM at 0.0, which is how the row's own two-field mismatch was
        // found: it printed Propellant01 beside a verdict reached on DragonProp01.
        s.Propellant01      = 1.0 - 0.03 * ((k * 5) % 33);
        s.DragonProp01      = 1.0 - 0.03 * ((k * 5) % 33);
        s.GForce01          = 0.02 * (k % 40);
        s.Fault = FaultKind.None; s.FaultText = "NOMINAL";
        // ⭐ S137b: the discrete emergencies are IN the sweep, so the "worst row IS the word"
        // invariant is exercised over them and not only over the bands. Deterministic in k.
        s.Systems = SystemsState.Fresh();
        s.Systems.Bus1On = (k % 3) != 1;
        s.Systems.Bus2On = (k % 5) != 2;
        if (k % 7 == 3) s.Systems.FireIntensity = 0.35;
        if (k % 11 == 5) s.Systems.LeakRate = 0.02;
        if (k % 4 == 1) s.Systems.A1 = StringState.Tripped;
        if (k % 6 == 2) { s.Systems.A1 = s.Systems.B1 = s.Systems.C1 = StringState.Tripped; }
        return s;
    }

    // ================= S147 / S49 H40: CURRENT STATE STOPS BEING A PICTURE =================
    // `CURRENT STATE`, `POINTING MODE`, the comm block and a counter were all pixels in
    // component_48.png, so 21 pages carried one frozen sentence - "Far Field Pointing Deorbit" -
    // whatever the vehicle was doing. The value box is erased and CURRENT STATE drawn live.
    static void BottomBarCurrentState()
    {
        const int VW = 2560, VH = 1406;

        // ---- 1. THE SOURCE ORDER IS THE REGISTRY'S, THEN THE LIVE CLASSIFIER, THEN NOTHING ------
        // TELEMETRY_REGISTRY names `CrewProcedureOps`'s step label, which reaches the screens as
        // `AutoPhase` - null unless the conductor is engaged. `Phase` is the live classifier the
        // Cover's own ACTIVE PHASE row prints, so the two surfaces cannot disagree (C7.1).
        PageState both = new PageState();
        both.Valid = true; both.AutoPhase = "DEORBIT BURN"; both.Phase = "Orbit";
        Check("the conductor's own step label wins when there is one",
              BottomBar.CurrentState(both) == "DEORBIT BURN",
              "got " + BottomBar.CurrentState(both));
        PageState classifier = new PageState();
        classifier.Valid = true; classifier.Phase = "Orbit";
        Check("...and the live classifier is the fallback",
              BottomBar.CurrentState(classifier) == "Orbit",
              "got " + BottomBar.CurrentState(classifier));
        PageState blank = new PageState(); blank.Valid = true;
        Check("neither = a dash, never a plausible sentence",
              BottomBar.CurrentState(blank) == Dashes.None, "got " + BottomBar.CurrentState(blank));
        PageState dead = new PageState(); dead.Valid = false; dead.Phase = "Orbit";
        Check("a dead feed dashes even with a phase still on the state",
              BottomBar.CurrentState(dead) == Dashes.None, "got " + BottomBar.CurrentState(dead));

        // ---- 2. IT IS DRAWN, AND IT MOVES - WHICH A PICTURE CANNOT DO --------------------------
        DisplayList a = BarOf(classifier, VW, VH);
        PageState other = new PageState(); other.Valid = true; other.Phase = "ENTRY INTERFACE";
        DisplayList b = BarOf(other, VW, VH);
        Check("the bar prints the phase it was given", BarValue(a, VW, VH, BarFit.Frame) == "Orbit",
              "bar reads \"" + BarValue(a, VW, VH, BarFit.Frame) + "\"");
        Check("...and a different phase gives a different bar",
              BarValue(b, VW, VH, BarFit.Frame) == "ENTRY INTERFACE",
              "bar reads \"" + BarValue(b, VW, VH, BarFit.Frame) + "\"");
        // ⛔ The frozen sentence is gone from the ART, so it cannot be drawn at all any more.
        Check("the baked sentence is not drawn by any state",
              !Drew(a, "Far Field Pointing Deorbit") && !Drew(b, "Far Field Pointing Deorbit"), "");

        DisplayList none = BarOf(blank, VW, VH);
        Check("with no phase the bar dashes", BarValue(none, VW, VH, BarFit.Frame) == Dashes.None,
              "bar reads \"" + BarValue(none, VW, VH, BarFit.Frame) + "\"");

        // ---- 3. GEOMETRY: right-aligned on the erased box, at the glanceable floor --------------
        float sc = (float)VH / 2112f;
        float bx, by, bw, bh;
        BottomBar.Rect(VW, VH, BarFit.Frame, out bx, out by, out bw, out bh);
        // ⚠ S176 — `bw / RefW` IS NO LONGER THE DRAW SCALE and this is where that first bites. On a
        // spread page the box is the whole panel while the glyphs stay at h/RefH; the two agree only
        // under BarFit.Frame, which is what BarOf renders. `BottomBar.Scale` is the honest form.
        float k = BottomBar.Scale(VH);
        // The value hangs off the RULE now, not off the bar's left end - which is the same number
        // under Frame and the only form that survives the other two fits.
        float vx = BottomBar.MapX(1464f, VW, VH, BarFit.Frame) - 3f * k;
        Check("the value is right-aligned exactly where the erased box ended",
              Math.Abs(XOf(a, "Orbit") - vx) < 0.5f,
              "drawn at " + XOf(a, "Orbit") + ", box right edge " + vx);
        // ⚠ LIVE type, so S153's glanceable floor - the baked value was ~29 design px, 60% of it, and
        // is one of QC R-01's own samples.
        Check("...and at the glanceable floor, not the baked size",
              SizeOf(a, "Orbit") >= Typography.MinFor(VW),
              "got " + SizeOf(a, "Orbit") + ", floor " + Typography.MinFor(VW));

        // ---- 4. THE FIVE PAGES WITHOUT STATE DASH, AND THAT IS HONEST --------------------------
        // MenuPage / PlaceholderPage / FigmaFramePage / SuitCheckPage / VrioTestPage genuinely do not
        // receive a PageState. A dash there says so; a frozen sentence said something false.
        DisplayList menu = new DisplayList(MenuPage.Commands + 40);
        MenuPage.Build(menu, VW, VH);
        Check("a page with no vessel state dashes rather than inventing one",
              Drew(menu, Dashes.None) && !Drew(menu, "Far Field Pointing Deorbit"), "");

        // ---- 5. THE SWEEP: EVERY PAGE, BECAUSE THE FINDING WAS "ON EVERY PAGE" -----------------
        // ⛔ A per-page check is what this line needs and it is what the first version of these tests
        // did NOT have: a mutation that reverted ONE page to the stateless overload passed everything.
        // The bar is the one thing the crew see from anywhere, so the guard has to be page-wide.
        // ⚠ FIVE PAGES LEGITIMATELY DASH - they do not receive a PageState at all - and naming them
        // here is the record of which five, so a sixth cannot join them quietly.
        UiPage[] stateless = { UiPage.Menu, UiPage.Cabin, UiPage.SuitCheck, UiPage.Procedure,
                               UiPage.VrioTest };
        PageState sweepState = new PageState();
        sweepState.Valid = true; sweepState.Phase = "ENTRY INTERFACE";
        int live = 0, dashed = 0, wrong = 0;
        foreach (UiPage up in (UiPage[])Enum.GetValues(typeof(UiPage)))
        {
            if (FigmaUI.IsPlaceholder(up)) continue;
            DisplayList dl = new DisplayList(1200);
            FigmaUI.Build(dl, up, VW, VH, sweepState, MapProjection.Default());
            bool has = BarValue(dl, VW, VH, BottomBar.FitFor(up)) == "ENTRY INTERFACE";
            bool exempt = Array.IndexOf(stateless, up) >= 0;
            if (has) live++; else dashed++;
            if (has == exempt) { wrong++; Console.WriteLine("    S147  " + up + " bar=\""
                                                            + BarValue(dl, VW, VH, BottomBar.FitFor(up)) + "\" exempt="
                                                            + exempt); }
        }
        Check("every page that receives vessel state prints it in the bar; the five that do not, do not",
              wrong == 0, wrong + " page(s) disagreed with the exemption list");
        Check("...and that is most of them, not a handful", live >= 18,
              "only " + live + " pages printed it, " + dashed + " dashed");
    }

    /// <summary>The string the BOTTOM BAR drew as CURRENT STATE, or null if it drew none there.
    /// ⛔ BY POSITION, and that is not fussiness: the first version of S147's sweep searched the whole
    /// page for the phase string and every page that prints the phase for its OWN reasons - the Cover's
    /// ACTIVE PHASE row, for one - passed whatever the bar did. A mutation reverting one page to the
    /// stateless overload went uncaught because of it.</summary>
    static string BarValue(DisplayList dl, int w, int h, BarFit fit)
    {
        float bx, by, bw, bh;
        BottomBar.Rect(w, h, fit, out bx, out by, out bw, out bh);
        if (bw <= 0f) return null;
        float k = BottomBar.Scale(h);
        // ⚠ The band is generous on purpose. The live value is drawn at `185 - 0.553 * size`,
        // which at the floor size is PNG row 158.4 - a band starting at 160 missed it by 1.6 px
        // and the whole sweep read empty. Nothing else in the bar is DRAWN text (the captions
        // are baked), so a wide band costs nothing.
        // ⭐ SUPERSEDED IN PLACE 2026-09-06 (S176) - THE LAST SENTENCE IS STILL TRUE AND IS NOW A
        // FINDING RATHER THAN A CONVENIENCE. The captions are still not drawn text: §14.2a clause (1)
        // asks for them TYPED and they are still TILES, because typing them puts them at the nav
        // bar's glanceable floor (Typography.Dense excludes "anything on the nav bar") - a 2.4x raise
        // that is S153a-Q1's open owner question. See REGISTER.md S176. If that question is ever
        // answered YES, this band stops being unambiguous and the search must narrow to the row.
        // ⚠ AND THE ANCHOR MOVED: the value hangs off the RULE, which is where the page's own column
        // divider is, so it must be found through the page's own fit and not off the bar's left end.
        float x = BottomBar.MapX(1464f, w, h, fit) - 3f * k, y0 = by + 110f * k, y1 = by + 225f * k;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && Math.Abs(c.A - x) < 0.5f && c.B >= y0 && c.B <= y1)
                return c.Str;
        }
        return null;
    }

    static DisplayList BarOf(PageState s, int w, int h)
    {
        DisplayList dl = new DisplayList(64);
        BottomBar.Draw(dl, w, h, s, BarFit.Frame);
        return dl;
    }

    /// <summary>An icon's DRAWN centre on a given page, through that page's OWN fit (S176).
    /// ⛔ THE ONE PLACE A PROBE IS COMPUTED. Every bar probe in this file goes through here, so a fit
    /// change moves the probes with the draw instead of leaving them testing an empty strip - which is
    /// what the S103 comment above the Menu probe warned about and what S174 saw happen for real.</summary>
    static void IconCentre(UiPage page, int icon, int w, int h, out float cx, out float cy)
    {
        BarFit fit = BottomBar.FitFor(page);
        float bx, by, bw, bh;
        BottomBar.Rect(w, h, fit, out bx, out by, out bw, out bh);
        float k = BottomBar.Scale(h);
        cx = bx + (BottomBar.IconX[icon] + BottomBar.IconS * 0.5f) * k;
        cy = by + (BottomBar.IconY - 1877f + BottomBar.IconS * 0.5f) * k;
    }

    // ================= S145 / S49 H35: THE RANGE RINGS CARRY A SCALE =================
    // Four circles at `rmax * i/4` are a pure fraction of the BOX - no range behind them at all - so a
    // crew could not tell a 200 km rendezvous from a lunar transfer. The scale existed; it was a LOCAL
    // inside `NavPage.Orbit`, and S145 lifted it out so the draw and the labels read ONE number.
    static void OrbitRingScale()
    {
        const int VW = 2560, VH = 1406;
        // The plot well, exactly as NavOrbitPlotPage passes it.
        float sc = (float)VH / 2112f;
        float mw = 2667f * sc, mh = 1670f * sc;
        float rmax = sc * Math.Min(2667f, 1670f) * 0.46f;

        PageState kerbin = new PageState();
        kerbin.Valid = true; kerbin.Regime = FlightRegime.Space;
        kerbin.BodyRadiusM = 600000.0; kerbin.AtmosphereDepthM = 70000.0;
        kerbin.ApogeeM = 124000.0; kerbin.PerigeeM = 121900.0; kerbin.AltitudeM = 123400.0;
        kerbin.ApogeeShown = true; kerbin.PerigeeShown = true; kerbin.Ascending = true;

        float ppm = NavPage.OrbitPixelsPerMetre(kerbin, mw, mh, MapProjection.Default());
        Check("the plot has a scale at all", ppm > 0f, "got " + ppm);

        // ⭐ THE GOLDEN NUMBER, DERIVED INDEPENDENTLY. Worked out by hand from the fixture's own
        // figures - Kerbin R = 600 km, ap 124 km, pe 121.9 km, a well of 1775.4 x 1111.7 px, the fit
        // rule's 0.42 margin and the 0.46 ring fraction - giving 6.4588e-4 px/m and an outer ring of
        // 791.8 km. This asserts the page against that derivation, not against itself.
        float outerKm = (rmax / ppm) / 1000f;
        Check("the outer ring is 792 km on the Kerbin fixture",
              Math.Abs(outerKm - 791.8f) < 1f, "got " + outerKm + " km");

        // The rings are evenly spaced, so their VALUES must be too - a scale that is not linear in the
        // radius is not a scale.
        for (int i = 1; i <= 4; i++)
        {
            float km = ((rmax * i / 4f) / ppm) / 1000f;
            Check("ring " + i + " is " + i + "/4 of the outer one",
                  Math.Abs(km - outerKm * i / 4f) < 0.01f, "got " + km);
        }

        // ⭐ AN INDEPENDENT PROPERTY: zoom is one multiplier on the scale, so at 2x the SAME ring must
        // read HALF the range. Nothing about the ring geometry changes; only what it measures.
        MapView z2 = MapProjection.Default();
        float zoomStepScale = MapProjection.OrbitScale(z2.OrbitZoom);
        Check("the default view is unzoomed", Math.Abs(zoomStepScale - 1f) < 1e-6f,
              "got " + zoomStepScale);
        // ⚠ The zoom field is public on MapView, so this sets it directly - the projection's own
        // stepper (`MapProjection.Zoom`) takes a control act and is a different surface. What matters
        // is that the SCALE is read through `OrbitScale`, which it is.
        MapView zin = MapProjection.Default(); zin.OrbitZoom = 1;
        float ppm2 = NavPage.OrbitPixelsPerMetre(kerbin, mw, mh, zin);
        Check("zooming in makes each ring measure LESS range",
              ppm2 > ppm && Math.Abs((rmax / ppm2) - (rmax / ppm) / (ppm2 / ppm)) < 1f,
              "ppm " + ppm + " -> " + ppm2);

        // A bigger body means a bigger picture, so the same rings measure more.
        PageState earth = kerbin;
        earth.BodyRadiusM = 6371000.0; earth.ApogeeM = 202000.0; earth.PerigeeM = 198000.0;
        float ppmE = NavPage.OrbitPixelsPerMetre(earth, mw, mh, MapProjection.Default());
        Check("a body ten times larger makes the rings measure much more range",
              (rmax / ppmE) > (rmax / ppm) * 5f,
              "kerbin " + (rmax / ppm / 1000f) + " km, earth " + (rmax / ppmE / 1000f) + " km");

        // ---- ⭐ THE PAD, WHICH IS WHY THE BODY IS IN THE EXTENT AT ALL -------------------------
        // `NavPage`'s own note: on the pad the "orbit" is a degenerate ellipse - apoapsis at the
        // surface, periapsis at the centre of the planet, periapsis reading -598.4 km - and scaling to
        // its minor axis "blew the globe up to 790 px inside a 520 px panel, seen in game 2026-08-06".
        // ⚠ THE ORBITAL FIXTURE ABOVE CANNOT TEST THAT: its half-minor axis (723 km) is already larger
        // than the body, so the `Max(..., BodyRadiusM)` never binds and a mutation removing it passed.
        // This is the state where it does bind.
        PageState pad = new PageState();
        pad.Valid = true; pad.Regime = FlightRegime.Space;
        pad.BodyRadiusM = 600000.0; pad.ApogeeM = 0.0; pad.PerigeeM = -598400.0;
        pad.ApogeeShown = true; pad.Ascending = true;
        float ppmPad = NavPage.OrbitPixelsPerMetre(pad, mw, mh, MapProjection.Default());
        Check("the pad's degenerate ellipse still has a scale", ppmPad > 0f, "got " + ppmPad);
        Check("...and the BODY still fits the well, which is what that rule is for",
              (float)pad.BodyRadiusM * ppmPad <= mh * 0.5f,
              "globe would draw at radius " + ((float)pad.BodyRadiusM * ppmPad)
              + " px in a well " + mh + " px tall");

        // ---- AND NO ORBIT MEANS NO LABEL, WHICH IS THE HONEST STATE --------------------------
        // ⚠ THE DEAD FIXTURE KEEPS ITS BODY, and that is the point. A `new PageState()` has
        // `BodyRadiusM == 0`, so a second guard inside OrbitFit catches it and the `!Valid` check
        // itself is never exercised - a mutation removing `!s.Valid` passed. A dropped feed in flight
        // still has the last body it knew about, which is exactly this state.
        PageState dead = kerbin; dead.Valid = false;
        Check("a dead feed has no plot scale, even with a body still on the state",
              NavPage.OrbitPixelsPerMetre(dead, mw, mh, MapProjection.Default()) == 0f,
              "got " + NavPage.OrbitPixelsPerMetre(dead, mw, mh, MapProjection.Default()));
        PageState noBody = new PageState(); noBody.Valid = true; noBody.BodyRadiusM = 0.0;
        Check("...and neither does a state with no body",
              NavPage.OrbitPixelsPerMetre(noBody, mw, mh, MapProjection.Default()) == 0f, "");

        DisplayList live = new DisplayList(NavOrbitPlotPage.Commands + 40);
        NavOrbitPlotPage.Build(live, VW, VH, kerbin);
        DisplayList off = new DisplayList(NavOrbitPlotPage.Commands + 40);
        NavOrbitPlotPage.Build(off, VW, VH, dead);
        Check("the page draws the outer ring's range", Drew(live, "792 km"),
              "expected 792 km among the labels");
        Check("...and all four rings are labelled",
              Drew(live, "198 km") && Drew(live, "396 km") && Drew(live, "594 km"), "");
        // ⛔ A ring labelled from a scale that does not exist would be worse than an unlabelled one -
        // and "no label" has to mean NO TEXT, not a dash where a range should be. ⚠ Checking for the
        // absence of "792 km" was not enough: with the guard removed, `RingLabel(r/0)` returns the
        // project dash and the page draws FOUR of them, which that check cannot see. Counted by
        // POSITION instead - the ring labels are the only text centred on the plot's vertical axis.
        // ⚠ A Y BAND AS WELL AS AN X. The plot is centred on the panel, so `rcx` is also `w * 0.5` -
        // the page title and NavPage's own "NO ORBIT" both land on that column. The ring labels are
        // the only text in the band ABOVE the plot centre and BELOW its top edge; the innermost sits
        // a full ring-gap up, so the bound is generous rather than fitted.
        float rcx = (380f + 2667f * 0.5f) * sc + (VW - 3427f * sc) * 0.5f;
        float rcy = (180f + 1670f * 0.5f) * sc;
        float yTop = 180f * sc, yBot = rcy - 100f * sc;
        Check("the live page carries four ring labels on the plot's axis",
              TextsAtX(live, rcx, yTop, yBot) == 4, "got " + TextsAtX(live, rcx, yTop, yBot));
        Check("a dead feed labels no ring at all - not even with a dash",
              TextsAtX(off, rcx, yTop, yBot) == 0 && !Drew(off, "792 km"),
              "got " + TextsAtX(off, rcx, yTop, yBot) + " labels on the axis");

        // ⚠ LIVE type: a range that cannot be read at a glance is not a range (S153's policy).
        Check("the ring labels clear the glanceable floor",
              SizeOf(live, "792 km") >= Typography.MinFor(VW),
              "got " + SizeOf(live, "792 km") + ", floor " + Typography.MinFor(VW));
    }

    // ================= S138: THE SUBSYSTEM STATE WORDS THAT HAD A SOURCE =================
    // 23 of the 36 checklist words were literals. SIX of them had a model already - and three of those
    // were not merely frozen but CONTRADICTED the Systems P&ID, which draws the same components from
    // the same numbers in the same frame.
    //
    // ⭐ EVERY CHECK BELOW DRIVES A REAL MODEL INPUT AND ASSERTS THE WORD MOVES. That is the only test
    // a re-hardcoded constant cannot pass: a literal renders identically in every preview ever taken,
    // so "the word is present" proves nothing and "the word CHANGED when the vessel changed" proves
    // everything.
    static void SubsystemStateWords()
    {
        const int VW = 2560, VH = 1406;

        // ---- CREW: three rows, three of the P&ID's own verdicts ----------------------------------
        PageState ok = SubsysFixture();
        PageState bad = SubsysFixture();
        bad.Cabin.Ppo2Psia = 1.8;          // life support -> ALARM (the P&ID's CABIN component)
        bad.Systems.Oxygen = 0.05;         // O2 TANK -> ALARM  (Alarms.Low)
        bad.Cabin.Co2MmHg = 9.0;           // CO2 SCRUBBER -> ALARM (the CO2 band)
        DisplayList a = Subsys(VehicleSubsystemPage.Sub.Crew, ok, VW, VH);
        DisplayList b = Subsys(VehicleSubsystemPage.Sub.Crew, bad, VW, VH);
        Check("CREW reads Nominal three times when the cabin is healthy",
              Times(a, "Nominal") >= 3, "got " + Times(a, "Nominal"));
        Check("...and three of those words move to Alarm when the model does",
              Times(b, "Alarm") >= 3, "got " + Times(b, "Alarm"));
        // ⛔ The literal that is GONE for good: "Active" was CO2 SCRUBBER's hardcoded word and no
        // computed path can produce it. This is the `gone` idiom - a constant wired once must never be
        // quietly re-hardcoded.
        Check("CO2 SCRUBBER's old literal cannot come back",
              !Drew(a, "Active") && !Drew(b, "Active"), "");

        // ---- PROPULSION: the SuperDracos ARE the launch escape system --------------------------
        PageState armed = SubsysFixture(); armed.Steps.EscapeArmed = true;
        PageState safed = SubsysFixture(); safed.Steps.EscapeArmed = false;
        DisplayList da = Subsys(VehicleSubsystemPage.Sub.Propulsion, armed, VW, VH);
        DisplayList ds = Subsys(VehicleSubsystemPage.Sub.Propulsion, safed, VW, VH);
        Check("SUPERDRACO reads Armed with the escape system armed", Drew(da, "Armed"), "");
        Check("...and Disarmed when it is not", Drew(ds, "Disarmed") && !Drew(ds, "Armed"), "");
        // ⭐ ONE SWITCH, ONE VOCABULARY: `StepList.AbortMode` returns "DISARMED" for this same
        // condition, so the word is the tree's own rather than one coined here.
        StepInputs si = new StepInputs();
        si.Valid = true; si.EscapeArmed = false;
        Check("...and that word is StepList's own, not a new one",
              StepList.AbortMode(si) == "DISARMED", "got " + StepList.AbortMode(si));

        // ---- POWER: PWR DISTRIB is the two buses, through S137b's derived rule -------------------
        PageState pOk = SubsysFixture();
        PageState pTrip = SubsysFixture(); pTrip.Systems.A1 = StringState.Tripped;
        PageState pDead = SubsysFixture();
        pDead.Systems.A1 = pDead.Systems.B1 = pDead.Systems.C1 = StringState.Tripped;
        DisplayList d1 = Subsys(VehicleSubsystemPage.Sub.Power, pOk, VW, VH);
        DisplayList d2 = Subsys(VehicleSubsystemPage.Sub.Power, pTrip, VW, VH);
        DisplayList d3 = Subsys(VehicleSubsystemPage.Sub.Power, pDead, VW, VH);
        Check("PWR DISTRIB is Nominal with both buses whole", Drew(d1, "Nominal"), "");
        Check("...Caution on a tripped string", Drew(d2, "Caution"), "");
        Check("...and Alarm on a powered bus with nothing online", Drew(d3, "Alarm"), "");

        // ---- THERMAL: HX FLOW is the cabin fan, in the P&ID's own two words ---------------------
        PageState hot = SubsysFixture();
        PageState cold = SubsysFixture();
        cold.Systems.Bus1On = false; cold.Systems.Bus2On = false;   // no bus, no fan
        DisplayList h1 = Subsys(VehicleSubsystemPage.Sub.Thermal, hot, VW, VH);
        DisplayList h2 = Subsys(VehicleSubsystemPage.Sub.Thermal, cold, VW, VH);
        Check("HX FLOW reads RUNNING while a bus is up", Drew(h1, "RUNNING"), "");
        Check("...and OFF when both buses are down", Drew(h2, "OFF") && !Drew(h2, "RUNNING"), "");
        // ⭐ Same source as the P&ID's CABIN FAN, so the two pages cannot disagree about one fan.
        Check("...which is exactly the P&ID's own source", hot.Systems.FanOn && !cold.Systems.FanOn, "");

        // ---- AND THE ROWS DELIBERATELY LEFT ALONE ARE STILL LITERAL ------------------------------
        // ⛔ S138 wired SIX of the 23 and handed SEVENTEEN to [[S139]] - including GPS, which was wired
        // to `HasFix` and then UNWIRED: that field is `body != null` (`VesselData.cs:147`), true in
        // flight essentially always, so a row reading "Lock" from it would LOOK computed and BEHAVE
        // like the constant it replaced. This pins that it was left honest rather than dressed up.
        DisplayList av = Subsys(VehicleSubsystemPage.Sub.Avionics, ok, VW, VH);
        PageState noFix = SubsysFixture(); noFix.HasFix = false;
        DisplayList av2 = Subsys(VehicleSubsystemPage.Sub.Avionics, noFix, VW, VH);
        Check("GPS is still a literal, and is NOT wired to HasFix",
              Drew(av, "Lock") && Drew(av2, "Lock"),
              "if this fails, GPS was wired to a field that is body != null");
    }

    /// <summary>A healthy vessel with both buses up: the state every S138 row moves AWAY from.</summary>
    static PageState SubsysFixture()
    {
        PageState s = AlertFixture(0);
        s.Systems = SystemsState.Fresh();
        s.Systems.Bus1On = true; s.Systems.Bus2On = true;
        s.Systems.Oxygen = 1.0; s.Systems.Nitrogen = 1.0;
        s.HasFix = true;
        s.Steps.Valid = true; s.Steps.EscapeArmed = true;
        return s;
    }

    // ================= S137b: A CABIN FIRE NOW RAISES A SEVERITY =================
    // VehicleSystems modelled fire, cabin leak and six power strings, and SystemsPidPage drew them -
    // but `Alarms` read none of them, so a fire raised no severity anywhere: not the tab strip's
    // red-nav, not the chrome bar, not Alarms.Mask, not S137's own alert list. Found while scoping
    // that list and deliberately NOT patched there.
    /// <summary>The black-box column table as it stood when S137c appended `sev_events`: how many
    /// columns preceded it, and an FNV-1a hash of their names in order. A pure APPEND leaves both
    /// alone; an insert, a removal or a reorder changes the hash, which is precisely what
    /// `BlackBoxSchema.SchemaVersion` is meant to be bumped for.</summary>
    const int PrefixCount = 205;
    const uint PrefixHash = 164112981u;

    static void DiscreteEmergencies()
    {
        const int VW = 2560, VH = 1406;
        PageState clean = AlertFixture(0);
        clean.Systems = SystemsState.Fresh();
        Check("the baseline fixture really is quiet",
              Alarms.VehicleSeverity(clean) == Severity.Nominal
              && Alarms.CabinEvents(clean.Systems) == Severity.Nominal,
              "vehicle " + Alarms.VehicleSeverity(clean));

        // ---- 1. THE HEADLINE, AS A BEFORE/AFTER ON ONE FIELD ------------------------------------
        PageState fire = clean; fire.Systems.FireIntensity = 0.4;
        Check("a cabin FIRE is an alarm", Alarms.CabinEvents(fire.Systems) == Severity.Alarm, "");
        Check("...and it raises the VEHICLE severity, which is what every surface reads",
              Alarms.VehicleSeverity(fire) == Severity.Alarm,
              "got " + Alarms.VehicleSeverity(fire));
        Check("...so Alarms.Mask lights the vehicle bit", (Alarms.Mask(fire) & (1 << 1)) != 0,
              "mask " + Alarms.Mask(fire));
        Check("...and the CREW subsystem carries it", Alarms.CrewSeverity(fire) == Severity.Alarm, "");

        PageState leak = clean; leak.Systems.LeakRate = 0.05;
        Check("a cabin LEAK likewise", Alarms.VehicleSeverity(leak) == Severity.Alarm
              && Alarms.CrewSeverity(leak) == Severity.Alarm, "");

        // ---- 2. THE POWER RULE IS DERIVED, NOT COUNTED -------------------------------------------
        PageState one = clean; one.Systems.A1 = StringState.Tripped;
        Check("one tripped string is a CAUTION - the bus behind it is redundant",
              Alarms.PowerEvents(one.Systems) == Severity.Caution,
              "got " + Alarms.PowerEvents(one.Systems));
        PageState busDead = clean;
        busDead.Systems.Bus1On = true;
        busDead.Systems.A1 = busDead.Systems.B1 = busDead.Systems.C1 = StringState.Tripped;
        Check("...but a POWERED bus with nothing online is an ALARM - the redundancy is gone",
              Alarms.PowerEvents(busDead.Systems) == Severity.Alarm,
              "got " + Alarms.PowerEvents(busDead.Systems));
        // ⛔ AND THE CREW SWITCHING A BUS OFF IS NOT A FAULT. That is the difference between "no
        // strings online" and "no strings online BECAUSE THE CREW SAID SO", and a rule that counted
        // online strings without asking would have alarmed on a deliberate act.
        PageState busOff = clean;
        busOff.Systems.Bus1On = false;
        busOff.Systems.A1 = busOff.Systems.B1 = busOff.Systems.C1 = StringState.Online;
        Check("a bus the crew switched OFF is not an alarm",
              Alarms.PowerEvents(busOff.Systems) == Severity.Nominal,
              "got " + Alarms.PowerEvents(busOff.Systems));

        // ---- 3. THE LIST GAINS ITS ROWS, UNDER A WORD THAT MOVED WITH THEM -----------------------
        AlertItem[] buf = new AlertItem[AlertList.Max];
        int n = AlertList.Build(fire, AlertScope.LifeSupport, buf);
        bool named = false;
        for (int i = 0; i < n; i++) if (buf[i].Label == "CABIN FIRE") named = true;
        Check("the CREW alert list names the fire", named && n >= 1, "rows " + n);
        Check("...and the word above it is the same alarm",
              AlertList.SeverityOf(fire, AlertScope.LifeSupport) == Severity.Alarm, "");

        int pn = AlertList.Build(busDead, AlertScope.Power, buf);
        bool strung = false;
        for (int i = 0; i < pn; i++)
            if (buf[i].Label == "POWER STRINGS" && buf[i].Value.Contains("BUS DEAD")) strung = true;
        Check("the POWER list names the dead bus, and its row proves its own severity", strung,
              "rows " + pn);

        // ---- 4. THE PAGE: THE CREW TAB READS ALARM ON A FIRE -------------------------------------
        DisplayList dl = Subsys(VehicleSubsystemPage.Sub.Crew, fire, VW, VH);
        Check("the CREW page's ALERTS word reads ALARM on a fire", Drew(dl, "ALARM"), "");
        Check("...and the page names the fire", Drew(dl, "CABIN FIRE"), "");

        // ---- 5b. S137c: THE RECORDING CAN BE READ BACK -------------------------------------------
        // ⛔ Folding the events into `VehicleSeverity` made `sev_vehicle` unreconstructible from the
        // columns beside it: a flight could record Alarm next to two Nominal component columns with
        // nothing saying why. `sev_events` is that missing column.
        Check("sev_events is in the schema", BlackBoxSchema.Index("sev_events") >= 0, "");
        Check("...and it is a PURE APPEND, at the very end of the table",
              BlackBoxSchema.Index("sev_events") == BlackBoxSchema.Columns.Length - 1,
              "index " + BlackBoxSchema.Index("sev_events") + " of "
              + BlackBoxSchema.Columns.Length);
        // ⭐ Which is why the version does NOT move. This file's own rule: "bumped when a column is
        // REORDERED or REMOVED; a pure append keeps the version (§4.2)". A recording made before this
        // line still chains with one made after it.
        Check("...so SchemaVersion is unchanged and old recordings still chain",
              BlackBoxSchema.SchemaVersion == 1, "got " + BlackBoxSchema.SchemaVersion);

        // ⛔ AND THE PREFIX IS PINNED, which is the assertion that actually enforces the append rule.
        // "sev_events is last" does NOT: inserting a column in the middle leaves it last and shifts
        // everything between - the exact reorder SchemaVersion exists to forbid. A hash over the names
        // BEFORE it is exact and, unlike a whole-table hash, a legal pure APPEND still passes.
        // ⚠ Re-pin BOTH numbers only when a reorder is deliberate, and bump SchemaVersion with them.
        int pn2 = BlackBoxSchema.Columns.Length - 1;
        uint h = 2166136261u;
        for (int i = 0; i < pn2; i++)
        {
            string nm = BlackBoxSchema.Columns[i].Name;
            for (int j = 0; j < nm.Length; j++) { h ^= nm[j]; h *= 16777619u; }
            h ^= (uint)','; h *= 16777619u;
        }
        Console.WriteLine("  note  black-box column prefix: " + pn2 + " names, FNV-1a " + h);
        Check("the column ORDER before the append is unchanged", pn2 == PrefixCount && h == PrefixHash,
              "count " + pn2 + " (want " + PrefixCount + "), hash " + h + " (want " + PrefixHash + ")");

        // ⭐ THE PROPERTY THAT MAKES THE COLUMN WORTH HAVING: nothing the vehicle severity says is
        // hidden from the columns recorded beside it. Swept over the same 40 states.
        int hidden = 0;
        for (int k = 0; k < 40; k++)
        {
            PageState v = AlertFixture(k);
            Severity parts = Alarms.Worst(Alarms.LifeSupport(v.Cabin), Alarms.Thermal(v.Cabin));
            parts = Alarms.Worst(parts, Alarms.PropellantSeverity(v));
            parts = Alarms.Worst(parts, Alarms.Low(v.Power01));
            parts = Alarms.Worst(parts, Alarms.Worst(Alarms.CabinEvents(v.Systems),
                                                     Alarms.PowerEvents(v.Systems)));
            if (parts != Alarms.VehicleSeverity(v)) hidden++;
        }
        Check("sev_vehicle is exactly the worst of the columns recorded beside it, over 40 states",
              hidden == 0, hidden + " states had a severity no column explains");

        // And the fire case specifically: the events column is the ONLY one that can carry it.
        Check("on a fire, sev_events is the only non-nominal component",
              Alarms.Worst(Alarms.CabinEvents(fire.Systems), Alarms.PowerEvents(fire.Systems))
                  == Severity.Alarm
              && Alarms.LifeSupport(fire.Cabin) == Severity.Nominal
              && Alarms.Thermal(fire.Cabin) == Severity.Nominal, "");

        // ---- 5. WHAT WAS DELIBERATELY LEFT ALONE ------------------------------------------------
        // ⛔ `Alarms.LifeSupport(CabinReadout)` is UNCHANGED, and that is not an oversight: the black
        // box records `sev_ls` through that exact signature and `BlackBoxSchema` names it as the
        // source, so widening it in place would silently change what a recorded column means.
        Check("LifeSupport(CabinReadout) still means the three cabin bands ONLY",
              Alarms.LifeSupport(fire.Cabin) == Severity.Nominal,
              "a fire has leaked into the recorded sev_ls column");
    }

    /// <summary>How many text commands sit at this x. ⚠ Needed because "the label is absent" and
    /// "the label is a dash" look the same to a string search, and S145's mutation A exploited
    /// exactly that: with its guard removed the page drew four dashes where four ranges belong.
    /// </summary>
    static int TextsAtX(DisplayList dl, float x, float yMin, float yMax)
    {
        int n = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && Math.Abs(c.A - x) < 0.5f && c.B >= yMin && c.B <= yMax) n++;
        }
        return n;
    }

    /// <summary>The x a string was drawn at, first match. -1 if it was not drawn.</summary>
    static float XOf(DisplayList dl, string text)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == text) return c.A;
        }
        return -1f;
    }

    // ================= S126 / S49 H3 / QC C-14: THE TWO TARGET READOUTS =================
    // ⛔ THE DEFECT WAS WORSE THAN "BAKED". The two assets are `target_latitude_26deg_15_00deg_n` and
    // `target_longitude_26deg_15_00deg_n` - the key names carry it - so BOTH printed the same string
    // and the LONGITUDE showed a latitude's value with a LATITUDE'S HEMISPHERE LETTER. A longitude
    // cannot be "N". That is a wrong reading, not a frozen one, and it is what these checks are for.
    static void CoverTargetReadouts()
    {
        const int VW = 2560, VH = 1406;
        PageState with = new PageState();
        with.Valid = true; with.HasTargetGround = true;
        with.TargetLatText = "28.50 N"; with.TargetLonText = "80.60 W";
        // ⚠ THE SAME STRINGS, WITH THE FLAG OFF. The first version of this fixture left the text
        // fields null, so "no target dashes" passed even when the code ignored `HasTargetGround`
        // entirely - there was nothing to leak. A stale value can only be caught by having one.
        PageState without = new PageState();
        without.Valid = true; without.HasTargetGround = false;
        without.TargetLatText = "28.50 N"; without.TargetLonText = "80.60 W";

        DisplayList a = CoverCam(with, CoverPage.CoverCam.Earth, VW, VH);
        DisplayList b = CoverCam(without, CoverPage.CoverCam.Earth, VW, VH);
        DisplayList map = CoverCam(with, CoverPage.CoverCam.Map, VW, VH);

        // The pictures are gone.
        Check("neither baked TARGET asset is drawn any more",
              !DrewAsset(a, "target_latitude_26deg_15_00deg_n")
              && !DrewAsset(a, "target_longitude_26deg_15_00deg_n"), "");
        // The captions are the reference's own strings (docs/UI_AUDIT.md's Cover label list).
        Check("both captions are drawn as text",
              Drew(a, "TARGET LATITUDE") && Drew(a, "TARGET LONGITUDE"), "");

        // ⛔ THE FINDING, AS A CHECK: two DIFFERENT values, and the longitude carries E/W.
        Check("the two readouts show DIFFERENT values",
              Drew(a, "28.50 N") && Drew(a, "80.60 W"), "");
        Check("the longitude is right of the latitude, as the C-13 geometry puts them",
              XOf(a, "80.60 W") > XOf(a, "28.50 N"),
              "lat x " + XOf(a, "28.50 N") + ", lon x " + XOf(a, "80.60 W"));
        // ⭐ And the pair is still SYMMETRIC ABOUT THE CAMERA SLOT - S105/C-13's whole point, which this
        // line must not undo.
        // ⛔ THE CENTRE IS COMPUTED INDEPENDENTLY, and the first version of this check was a TAUTOLOGY
        // that mutation testing caught: it took the midpoint OF THE TWO READOUTS and then asserted
        // they were equidistant from it, which is true of any two numbers. The slot centre comes from
        // the page's own geometry instead - `(ViewLeft * sc + w) / 2`, with ViewLeft = 1442 mirrored
        // here because it is private (the same move, and the same note, as the S54 phase-index test).
        float scq2 = (float)VH / 2112f;
        float slotCx = (1442f * scq2 + VW) * 0.5f;
        float dLat = slotCx - XOf(a, "TARGET LATITUDE"), dLon = XOf(a, "TARGET LONGITUDE") - slotCx;
        Check("the two readouts stay symmetric about the CAMERA SLOT's centre",
              Math.Abs(dLat - dLon) < 0.01f && dLat > 0f,
              "lat is " + dLat + " left of centre, lon is " + dLon + " right");

        // No target = no reading. Not a stale one, and not a plausible one.
        Check("with no ground target both values dash",
              Drew(b, Dashes.None) && !Drew(b, "28.50 N") && !Drew(b, "80.60 W"), "");
        Check("...and the captions still name the row", Drew(b, "TARGET LATITUDE"), "");

        // Earth view only - the flat map and the capsule plot no ground target (First.vue's own v-if).
        Check("the readouts are Earth-view only",
              !Drew(map, "TARGET LATITUDE") && !Drew(map, "28.50 N"), "");

        // ---- S153's two-floor policy, applied per content type ----
        float floor = Typography.MinFor(VW), dense = Typography.DenseFor(VW);
        Check("the VALUES are LIVE, so they clear the glanceable floor",
              SizeOf(a, "28.50 N") >= floor && SizeOf(a, "80.60 W") >= floor,
              "lat " + SizeOf(a, "28.50 N") + ", lon " + SizeOf(a, "80.60 W") + ", floor " + floor);
        // ⚠ The CAPTIONS are static labels - the ruling names "pad captions" as exactly that - so they
        // sit at the static-reference floor, which is BELOW the glanceable one by design. Both bounds
        // are asserted: at Dense or above, and deliberately under the live floor.
        Check("the CAPTIONS sit at the static-reference floor",
              SizeOf(a, "TARGET LATITUDE") >= dense && SizeOf(a, "TARGET LATITUDE") < floor,
              "caption " + SizeOf(a, "TARGET LATITUDE") + ", Dense " + dense + ", floor " + floor);
        // And the pair fits the baked box it replaces: 90 design px tall.
        Check("caption + value fit the baked box's 90 design px",
              (SizeOf(a, "TARGET LATITUDE") + SizeOf(a, "28.50 N")) <= 90f * ((float)VH / 2112f),
              "stack " + (SizeOf(a, "TARGET LATITUDE") + SizeOf(a, "28.50 N")) + " panel px, box "
              + (90f * ((float)VH / 2112f)));
    }

    static DisplayList CoverCam(PageState s, CoverPage.CoverCam cam, int w, int h)
    {
        DisplayList dl = new DisplayList(CoverPage.Commands + 200);
        CoverPage.Build(dl, w, h, s, MapProjection.Default(), 0, cam, Turntable.Front());
        return dl;
    }

    /// <summary>The y a string was drawn at (the TOP of the line), first match.</summary>
    static float TopOf(DisplayList dl, string text)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == text) return c.B;
        }
        return -1f;
    }

    // ================= S129 / S49 H6 / QC C-08: ENTRY ENABLED IS COMPUTED =================
    // Two baked PNGs with the answer exported into which one was set bolder - and the exported answer
    // is FALSE, permanently, on every phase, on the page whose whole body is the deorbit procedure.
    // The class of the row was the thing that blocked it for two days: crew latch, or vehicle arming
    // flag? The overseer settled it 2026-09-06 - the checklist and its gate command nothing that flies,
    // so the row is a §14.4(f) READOUT of the autopilot's own readiness check, and the crew-GO ->
    // autopilot edge is the part that stays an honest no-op.
    static void CoverEntryEnabled()
    {
        // The SHIPPED panel, not the suite's 1280 default: R-01 is a statement about the size the crew
        // actually get, and checking the floor at 1280 is the one width where the check cannot fail.
        const int VW = 2560, VH = 1406;
        // ---- 1. THE MODEL ----------------------------------------------------------------------
        // ⛔ THE ORDERING TEST FIRST, because it is the one that stops a false verdict. An unengaged
        // conductor leaves every other field at its struct default, and a rule that asked "at the
        // gate?" before "running at all?" would read that default `Holding` as a real "not enabled".
        // ⚠ TWO CASES, because the first version of this check had ONE and it did not test what its
        // own comment claimed. It set AtEntryGate = true, so reordering the guards still produced
        // Unknown and a mutation that moved the not-running check to LAST went uncaught. The case the
        // comment was really about is the UNFILLED struct - every field at its default - which is what
        // an unengaged conductor actually hands over.
        EntryReadinessInputs blank = new EntryReadinessInputs();
        Check("a struct nobody filled in is UNKNOWN, not a verdict",
              EntryReadiness.Of(blank) == EntryVerdict.Unknown, "got " + EntryReadiness.Of(blank));

        EntryReadinessInputs off = new EntryReadinessInputs();
        off.ConductorEngaged = false;
        off.AtEntryGate = true; off.ChecklistComplete = true; off.EntryGatePhase = GatePhase.Go;
        Check("nothing running = no verdict, even when every other field says GO",
              EntryReadiness.Of(off) == EntryVerdict.Unknown, "got " + EntryReadiness.Of(off));

        EntryReadinessInputs e = new EntryReadinessInputs();
        e.ConductorEngaged = true;

        e.AtEntryGate = false; e.EntryGatePhase = GatePhase.Holding; e.ChecklistComplete = false;
        Check("running but not at the deorbit gate = NOT enabled (a verdict, not an absence)",
              EntryReadiness.Of(e) == EntryVerdict.NotEnabled, "got " + EntryReadiness.Of(e));

        e.AtEntryGate = true;
        Check("at the gate with the checklist still working = NOT enabled",
              EntryReadiness.Of(e) == EntryVerdict.NotEnabled, "got " + EntryReadiness.Of(e));

        e.ChecklistComplete = true; e.EntryGatePhase = GatePhase.GoReady;
        Check("checklist complete = ENABLED",
              EntryReadiness.Of(e) == EntryVerdict.Enabled, "got " + EntryReadiness.Of(e));

        e.EntryGatePhase = GatePhase.Go;
        Check("the gate cleared = ENABLED",
              EntryReadiness.Of(e) == EntryVerdict.Enabled, "got " + EntryReadiness.Of(e));

        // NO-GO holds rather than cancels (CrewGate:109-110) - the "way to retrigger" the owner asked
        // for. While the hold stands, entry is not enabled however complete the list is.
        e.EntryGatePhase = GatePhase.NoGo;
        Check("a crew NO-GO holds, and entry is NOT enabled while it does",
              EntryReadiness.Of(e) == EntryVerdict.NotEnabled, "got " + EntryReadiness.Of(e));

        e.EntryGatePhase = GatePhase.Abort;
        Check("ABORT is absorbing here too",
              EntryReadiness.Of(e) == EntryVerdict.NotEnabled, "got " + EntryReadiness.Of(e));

        // ⛔ The words are REFERENCE COPY - docs/UI_AUDIT.md's Cover label list carries `ENTRY ENABLED`,
        // `True` and `False` verbatim. Not ours to reword (§1.4).
        Check("the verdict words are the reference's own",
              EntryReadiness.Text(EntryVerdict.Enabled) == "True"
              && EntryReadiness.Text(EntryVerdict.NotEnabled) == "False", "");
        Check("and no verdict is the project's dash, not a third word",
              EntryReadiness.Text(EntryVerdict.Unknown) == Dashes.None, "");

        // ---- 2. THE PAGE ------------------------------------------------------------------------
        PageState live = new PageState(); live.Valid = true;
        DisplayList unk = CoverAt(live, EntryVerdict.Unknown);
        DisplayList yes = CoverAt(live, EntryVerdict.Enabled);
        DisplayList no  = CoverAt(live, EntryVerdict.NotEnabled);

        // THE PNGs ARE GONE. Half the finding was that the answer was a picture.
        Check("the baked `true` glyph is no longer drawn on any of the three states",
              !DrewAsset(unk, "true") && !DrewAsset(yes, "true") && !DrewAsset(no, "true"), "");
        Check("...nor the baked `false` one",
              !DrewAsset(unk, "false") && !DrewAsset(yes, "false") && !DrewAsset(no, "false"), "");
        // ...but the CAPTION is a label, not a verdict, and stays exactly as exported.
        Check("the ENTRY ENABLED caption asset is untouched",
              DrewAsset(unk, "entry_enabled") && DrewAsset(yes, "entry_enabled"), "");

        // ⛔ QC's own must-not-break: "the row must dash, not read False, when there is no source."
        Check("no source draws a DASH and neither word",
              Drew(unk, Dashes.None) && !Drew(unk, "True") && !Drew(unk, "False"), "");
        Check("enabled lights True and dims False",
              SameColour(ColourOf(yes, "True"), DragonPalette.White)
              && SameColour(ColourOf(yes, "False"), DragonPalette.Text6), "");
        Check("not-enabled lights False and dims True",
              SameColour(ColourOf(no, "False"), DragonPalette.White)
              && SameColour(ColourOf(no, "True"), DragonPalette.Text6), "");

        // A dead feed cannot produce a verdict either, whatever the field happens to hold.
        PageState dead = new PageState(); dead.Valid = false; dead.EntryEnabled = EntryVerdict.Enabled;
        DisplayList dl0 = CoverAt(dead, EntryVerdict.Enabled);
        Check("a dead feed dashes even with the field set to Enabled",
              Drew(dl0, Dashes.None) && !Drew(dl0, "True"), "");

        // ---- 3. THE R-01 POLICY, ON THE FIRST ROW BUILT AFTER IT ---------------------------------
        // This is a LIVE safety verdict, so S153's policy puts it at the glanceable floor, NOT at the
        // baked PNG's 34-design-px box (~22 panel px, two thirds of the floor). The per-page ratchet
        // would fail the build for a new sub-floor element; this asserts the intent rather than
        // relying on the ratchet to notice.
        float floor = Typography.MinFor(VW);
        Check("the verdict is drawn at or above the glanceable floor",
              SizeOf(yes, "True") >= floor && SizeOf(no, "False") >= floor,
              "True " + SizeOf(yes, "True") + ", False " + SizeOf(no, "False") + ", floor " + floor);
        // ⚠ BY POSITION, not by string: the Cover draws the dash in up to eight places and the seven
        // top-strip readouts get theirs first. See SizeOfNear.
        float scq = (float)VH / 2112f;
        // x is the `true` asset's own box left, tight; y is the row's band rather than an exact
        // baseline, because S129 centres the verdict on the CAPTION box instead of on either asset's
        // top (the two exported tops disagree by 6 design px). Pinning the exact y here would just
        // mirror the page's own arithmetic back at it.
        // S174: design x 783 is inside the content panel and the panel now stretches, so this locates
        // the dash through the same map the page drew it with. The y is untouched - the reflow is x-only.
        float dashSize = SizeOfNear(unk, Dashes.None, SplitReflow.X(783f, VW, VH), 1570f * scq, 40f);
        Check("...and the dash with it", dashSize >= floor, "got " + dashSize + ", floor " + floor);

        // ⛔ ONE BASELINE. The two exported boxes disagree by 6 design px (`true` at y 1555, `false` at
        // 1549) - invisible while both were 34-px PNGs, a visible step once they are drawn at the floor
        // size. They are ONE control and the page centres both on the caption box instead.
        Check("both verdict words sit on exactly one baseline",
              TopOf(yes, "True") == TopOf(yes, "False"),
              "True y " + TopOf(yes, "True") + ", False y " + TopOf(yes, "False"));

        // ⭐ AND IT STILL FITS, measured against MarginAffordance's own em advance rather than by eye.
        // `false` sits at design x 1132 and the panel body ends at 1427, so "False" has 295 design px.
        float sc = (float)VH / 2112f;
        float size = Typography.MinDesignFor(VW, sc);
        float inkFalse = 5f * MarginAffordance.CapAdvance * size;
        Check("False at the floor size still fits the run to the panel body's edge",
              inkFalse < (1427f - 1132f), "ink " + inkFalse + " design px, room 295");
        float inkTrue = 4f * MarginAffordance.CapAdvance * size;
        Check("True likewise, clear of the False column",
              inkTrue < (1132f - 783f), "ink " + inkTrue + " design px, room 349");
    }

    // ================= S135 / QC A-02 / A-05: THE AUDIO PAGE'S CHANNELS ARE REAL =================
    // Five channel values were hardcoded strings in three unit systems — "12dB", "0dB", "100", "+9dB",
    // "50" — plus a "17" for VOX, and TEN painted buttons with no hit test anywhere in the tree.
    // `SettingsPage.cs:20-23` names that exact shape: "drawing eight buttons where seven do nothing is
    // the dead-control failure this project refuses."
    //
    // 🟢 Owner Q6 (2026-09-05, verbatim): "make the volume controls control the game sound levels."
    // The mapping was chosen from presented options on 2026-09-06 - "MAP THE FOUR THAT FIT" - which is
    // a SELECTION and is never quoted as words he typed.
    //
    // ⭐ AND IT IS NOT AN OVERRIDE OF THE 2026-08-06 "NO VOLUME SLIDERS" DECISION. That decision forbids
    // a fader on a stated PREMISE - "KSP has no cabin audio, so a fader would be a control bound to
    // nothing". Binding these to the game's own layers falsifies the premise; it does not overrule the
    // rule. A real control is not a simulated one.
    static void AudioPageChannels()
    {
        const int VW = 2560, VH = 1406;

        // ---- 1. THE MAPPING IS THE OWNER'S FOUR, AND NOTHING ELSE -------------------------------
        Check("MAIN is the master volume",
              AudioChannels.LayerFor("MAIN") == AudioLayer.Master, "");
        Check("AUX is ambience", AudioChannels.LayerFor("AUX") == AudioLayer.Ambience, "");
        Check("VOX is voice", AudioChannels.LayerFor("VOX") == AudioLayer.Voice, "");
        Check("ALERTS is the ship layer", AudioChannels.LayerFor("ALERTS") == AudioLayer.Ship, "");
        // ⛔ GROUND is a crew ROLE the Figma page put in a channel slot (Audio.vue's slot list is
        // "dB, AUX, MAIN, Vox, INTERCOM, ALERTS"); INTERCOM the ruling keeps as a reading. Neither is
        // a layer, and an unknown label must not silently become one either.
        Check("GROUND maps to nothing", AudioChannels.LayerFor("GROUND") == AudioLayer.None, "");
        Check("INTERCOM maps to nothing", AudioChannels.LayerFor("INTERCOM") == AudioLayer.None, "");
        Check("an unknown label maps to nothing",
              AudioChannels.LayerFor("MUSIC") == AudioLayer.None && AudioChannels.LayerFor(null) == AudioLayer.None, "");

        // ---- 2. VALUES: A PERCENT WHEN THERE IS ONE, A DASH WHEN THERE IS NOT --------------------
        AudioLevels lv = new AudioLevels();
        lv.Valid = true; lv.Master = 0.8f; lv.Ambience = 0.62f; lv.Voice = 0.45f; lv.Ship = 0.5f;
        Eq2("MAIN prints the master gain as a percent", AudioChannels.Text(lv, AudioLayer.Master), "80%");
        Eq2("AUX likewise", AudioChannels.Text(lv, AudioLayer.Ambience), "62%");
        Eq2("VOX likewise", AudioChannels.Text(lv, AudioLayer.Voice), "45%");
        Eq2("ALERTS likewise", AudioChannels.Text(lv, AudioLayer.Ship), "50%");
        Eq2("an unmapped channel dashes", AudioChannels.Text(lv, AudioLayer.None), Dashes.None);
        // ⛔ THE ONE THAT MATTERS: unreadable settings must NOT print 0%. "0%" says the game is muted,
        // which is a different claim from "this could not be read".
        AudioLevels bad = new AudioLevels();   // Valid false, every gain 0
        Eq2("unreadable settings dash rather than reading 0%",
            AudioChannels.Text(bad, AudioLayer.Master), Dashes.None);

        // ---- 3. THE NUDGE: step, clamp, both ends ------------------------------------------------
        Check("one press moves one step",
              Math.Abs(AudioChannels.Nudge(0.5f, 1) - (0.5f + AudioChannels.Step)) < 1e-6f, "");
        Check("...and down the same",
              Math.Abs(AudioChannels.Nudge(0.5f, -1) - (0.5f - AudioChannels.Step)) < 1e-6f, "");
        Check("it clamps at full scale", AudioChannels.Nudge(1f, 1) == 1f, "got " + AudioChannels.Nudge(1f, 1));
        Check("...and at silence", AudioChannels.Nudge(0f, -1) == 0f, "got " + AudioChannels.Nudge(0f, -1));
        Check("a zero direction moves nothing", AudioChannels.Nudge(0.37f, 0) == 0.37f, "");

        // ---- 4. THE PAGE ------------------------------------------------------------------------
        PageState live = new PageState(); live.Valid = true; live.Audio = lv; live.CrewText = "4";
        PageState dead = new PageState(); live.Valid = true;
        DisplayList on = Audio(live, VW, VH);
        DisplayList off = Audio(dead, VW, VH);

        // ⛔ THE LITERALS ARE GONE. This is the finding.
        Check("none of the five hardcoded channel values is drawn any more",
              !Drew(on, "12dB") && !Drew(on, "0dB") && !Drew(on, "100") && !Drew(on, "+9dB")
              && !Drew(on, "50") && !Drew(on, "17"), "");
        Check("the mapped channels print their live gains",
              Drew(on, "80%") && Drew(on, "62%") && Drew(on, "45%") && Drew(on, "50%"), "");
        // INTERCOM is the crew reading the ruling keeps it as - the same field the legacy page shows.
        Check("INTERCOM is the crew reading", Drew(on, "4"), "");
        // GROUND has nothing behind it, so it dashes rather than showing a plausible level.
        Check("GROUND dashes", Drew(on, Dashes.None), "");
        Check("a dead feed dashes every channel and prints no percent",
              Drew(off, Dashes.None) && !Drew(off, "80%") && !Drew(off, "62%"), "");

        // ---- 5. THE TEN BUTTONS: FOUR REAL, SIX PAINTED INERT ------------------------------------
        // The ± glyphs are Line commands, so counting them by colour is how "which buttons look live"
        // is measured without reaching into the page. AUX and ALERTS give 3 lines each (minus, plus-,
        // plus|); GROUND and INTERCOM give 3 each in the dim tint, and the two signal plates add two
        // ArcBands rather than lines.
        int white = Lines(on, DragonPalette.White).Count;
        int dim = Lines(on, DragonPalette.Text6).Count;
        Check("exactly two channels' worth of button glyphs are painted live", white == 6,
              "white button lines " + white + " (expected 6: AUX and ALERTS, 3 each)");
        Check("...and two channels' worth are painted inert", dim >= 6,
              "dim button lines " + dim + " (expected at least 6: GROUND and INTERCOM)");
        Check("with the settings unreadable NOTHING is painted live", Lines(off, DragonPalette.White).Count == 0,
              "got " + Lines(off, DragonPalette.White).Count);

        // ⛔ THE TWO SIGNAL PLATES ARE ARCS, NOT LINES, so the line count above cannot see them. No
        // source says what a signal button on an audio channel DOES - the same §1.4 wall S29 hit on
        // the Suit Leak Check's read-only plates - so they are drawn dim and take no touch. Counting
        // WHITE arcs is how that is measured: after this line there are none on the page at all.
        int whiteArcs = 0;
        for (int i = 0; i < on.Count; i++)
        {
            DrawCmd c = on.At(i);
            if (c.Kind == DrawKind.ArcBand && SameColour(c.Colour, DragonPalette.White)) whiteArcs++;
        }
        Check("the two signal plates are painted inert (no white arc anywhere on the page)",
              whiteArcs == 0, "got " + whiteArcs + " white arcs");

        // ---- 6. THE HIT TEST AGREES WITH THE PAINT ------------------------------------------------
        // ⭐ S32's rule on a second page: a dimmed button cannot act, a live one cannot look
        // unavailable. Probed at each button's own centre, computed the way Build places them.
        float sx = VW / 3427f, sy = VH / 2112f;
        // ⚠ S134e: the probe points come from the page's OWN slot geometry, not from copies of the
        // literals it used to hold - the grid snap moved six of these eight by up to 46 design px, and
        // a test carrying its own stale copy would have gone on passing at the wrong place. That is
        // not circular: what is asserted here is the GATE (live / inert), never the position.
        float by = SettingsAudioPage.BtnCy * sy;
        Check("AUX minus is live", SettingsAudioPage.HitTest(SettingsAudioPage.MinusCx(1) * sx, by, VW, VH, live)
              == SettingsAudioPage.AudioAct.AuxMinus, "");
        Check("AUX plus is live", SettingsAudioPage.HitTest(SettingsAudioPage.PlusCx(1) * sx, by, VW, VH, live)
              == SettingsAudioPage.AudioAct.AuxPlus, "");
        Check("ALERTS minus is live", SettingsAudioPage.HitTest(SettingsAudioPage.MinusCx(3) * sx, by, VW, VH, live)
              == SettingsAudioPage.AudioAct.AlertsMinus, "");
        Check("ALERTS plus is live", SettingsAudioPage.HitTest(SettingsAudioPage.PlusCx(3) * sx, by, VW, VH, live)
              == SettingsAudioPage.AudioAct.AlertsPlus, "");
        Check("GROUND's pair takes no touch",
              SettingsAudioPage.HitTest(SettingsAudioPage.MinusCx(0) * sx, by, VW, VH, live) == SettingsAudioPage.AudioAct.None
              && SettingsAudioPage.HitTest(SettingsAudioPage.PlusCx(0) * sx, by, VW, VH, live) == SettingsAudioPage.AudioAct.None, "");
        Check("INTERCOM's pair takes no touch",
              SettingsAudioPage.HitTest(SettingsAudioPage.MinusCx(2) * sx, by, VW, VH, live) == SettingsAudioPage.AudioAct.None
              && SettingsAudioPage.HitTest(SettingsAudioPage.PlusCx(2) * sx, by, VW, VH, live) == SettingsAudioPage.AudioAct.None, "");
        Check("the two signal plates take no touch either",
              SettingsAudioPage.HitTest(SettingsAudioPage.SignalCx(0) * sx, by, VW, VH, live) == SettingsAudioPage.AudioAct.None
              && SettingsAudioPage.HitTest(SettingsAudioPage.SignalCx(1) * sx, by, VW, VH, live) == SettingsAudioPage.AudioAct.None, "");
        Check("a touch above the button row misses",
              SettingsAudioPage.HitTest(SettingsAudioPage.MinusCx(1) * sx, 1500f * sy, VW, VH, live)
              == SettingsAudioPage.AudioAct.None, "");
        // ⛔ AND WITH THE SETTINGS UNREADABLE, NOTHING IS LIVE - the paint and the touch fail together.
        Check("unreadable settings make every button inert",
              SettingsAudioPage.HitTest(SettingsAudioPage.MinusCx(1) * sx, by, VW, VH, dead) == SettingsAudioPage.AudioAct.None
              && SettingsAudioPage.HitTest(SettingsAudioPage.MinusCx(3) * sx, by, VW, VH, dead) == SettingsAudioPage.AudioAct.None, "");
    }

    static DisplayList Audio(PageState s, int w, int h)
    {
        DisplayList dl = new DisplayList(SettingsAudioPage.Commands + 200);
        SettingsAudioPage.Build(dl, w, h, 2, s);
        return dl;
    }

    static void Eq2(string what, string got, string want)
    { Check(what, got == want, "got '" + got + "', want '" + want + "'"); }

    // ================= S131 / QC C-02: THE STRAY ARROW IS DROPPED =================
    // `bi_arrow_right_short` is a 16x16 glyph with TWELVE opaque pixels, placed by masked template
    // match - the smallest, lowest-information target in the set, and exactly where a template match
    // returns a false peak. It did: design x 1706 is 264 px right of the content panel's own right
    // edge, and the fill-to-fit reflow pushed it further, onto panel (1414, 698) - dead centre of the
    // LIVE camera slot, over the globe, on every Cover render but phase 5. It is also pure BLACK ink
    // drawn with a White tint, so at its correct position it would have been invisible anyway.
    // 🟢 Owner Q1: option selected "Drop it" (2026-09-05, via the overseer) - a SELECTION, not words
    // he typed. This is the standing guard that it does not come back with the next asset sweep.
    static void CoverDroppedArrow()
    {
        const int VW = 2560, VH = 1406;
        int drawn = 0;
        foreach (CoverPage.CoverCam cam in new[] { CoverPage.CoverCam.Earth, CoverPage.CoverCam.Map,
                                                   CoverPage.CoverCam.Capsule })
            for (int ph = 0; ph < CoverPage.PhaseCount; ph++)
            {
                PageState s2 = new PageState(); s2.Valid = true;
                DisplayList dl = new DisplayList(CoverPage.Commands + 200);
                CoverPage.Build(dl, VW, VH, s2, MapProjection.Default(), ph, cam, Turntable.Front());
                if (DrewAsset(dl, "bi_arrow_right_short")) drawn++;
            }
        Check("the dropped arrow is drawn on none of the 21 phase/camera states", drawn == 0,
              "drawn on " + drawn + " of 21");

        // ⚠ AND ITS Keys/Box ROW IS STILL THERE, deliberately. The two arrays are index-paired and
        // every other placement is measured against them, so deleting a row would shift twelve boxes
        // for no gain. "Dropped" means never drawn, not excised - and this pins the difference, so a
        // later reader does not "tidy up" the row and silently renumber the table.
        Check("...but its measured box row is NOT deleted", CoverPage.HasAssetRow("bi_arrow_right_short"),
              "the Keys/Box pairing has been edited - check every placement that follows it");
    }

    /// <summary>The Cover on a normal phase with one ENTRY ENABLED verdict set. Phase 0, not the
    /// Reference Content phase - slot 5 swaps this whole row out with the rest of the baked body.</summary>
    static DisplayList CoverAt(PageState s, EntryVerdict v)
    {
        const int VW = 2560, VH = 1406;
        PageState c = s; c.EntryEnabled = v;
        DisplayList dl = new DisplayList(CoverPage.Commands + 200);
        CoverPage.Build(dl, VW, VH, c, MapProjection.Default(), 0,
                        CoverPage.CoverCam.Earth, Turntable.Front());
        return dl;
    }

    static void BottomBarNav()
    {
        // ---- S103: THE PROBE COMES FROM THE BAR'S OWN GEOMETRY, NOT FROM A COPY OF IT ----
        // This used to compute the icon centres as `(x[i] + s*0.5f) / RefW * W` - the STRETCHED
        // mapping, hardcoded here as a second copy of what FigmaUI's hit test happened to do. So it
        // proved the hit map agreed with itself and nothing about whether it agreed with the DRAW.
        // Both now read `BottomBar`, which is the one geometry the bar is drawn from (QC C-04/H-07).
        float[] x = BottomBar.IconX;
        const float s = BottomBar.IconS;
        // Must match FigmaUI.BarTarget (from the reference demo: icon N -> panel N).
        UiPage[] want = { UiPage.Cover, UiPage.Hud, UiPage.Vehicle, UiPage.SuitCheck, UiPage.Audio };

        // ⭐ S176: THE PROBE IS PER PAGE NOW, because the bar's ends are. The HUD letterboxes and the
        // Cover spreads, so one probe cannot serve both any more - and the pair below is the check
        // that matters: the SAME icon, on two pages with two different maps, still routes.
        for (int i = 0; i < x.Length; i++)
        {
            float hx, hy, cvx, cvy;
            IconCentre(UiPage.Hud, i, W, H, out hx, out hy);
            IconCentre(UiPage.Cover, i, W, H, out cvx, out cvy);

            Check("bar icon " + i + " hittable", FigmaUI.BottomBarHit(UiPage.Hud, hx, hy, W, H) == i,
                  "got " + FigmaUI.BottomBarHit(UiPage.Hud, hx, hy, W, H));

            // The bar routes to its target from ANY page — tested here from a sub-page and the hub.
            NavHit fromSub = FigmaUI.HitTest(UiPage.Hud, hx, hy, W, H);
            Check("bar icon " + i + " routes (sub-page)",
                  fromSub.Act == NavAct.Goto && fromSub.Target == want[i],
                  "act " + fromSub.Act + " tgt " + fromSub.Target);

            NavHit fromCover = FigmaUI.HitTest(UiPage.Cover, cvx, cvy, W, H);
            Check("bar icon " + i + " wins over cover controls",
                  fromCover.Act == NavAct.Goto && fromCover.Target == want[i],
                  "act " + fromCover.Act + " tgt " + fromCover.Target);
        }

        // Neighbours must not share a hit region (the icon pitch is 128 design px, the icon 80).
        {
            float bx, by, bw, bh;
            BottomBar.Rect(W, H, BarFit.Frame, out bx, out by, out bw, out bh);
            float k = BottomBar.Scale(H);
            for (int i = 0; i + 1 < x.Length; i++)
            {
                float edge = bx + ((x[i] + s) + x[i + 1]) * 0.5f * k;     // midpoint of the gap
                float cy = by + (BottomBar.IconY - 1877f + s * 0.5f) * k;
                Check("gap after icon " + i + " hits nothing",
                      FigmaUI.BottomBarHit(UiPage.Hud, edge, cy, W, H) == -1,
                      "got " + FigmaUI.BottomBarHit(UiPage.Hud, edge, cy, W, H));
            }
        }

        // A touch above the bar is not a bar hit.
        Check("above the bar misses", FigmaUI.BottomBarHit(UiPage.Hud, 100f, H * 0.5f, W, H) == -1, "");

        BottomBarUndistorted();
        BarFollowsItsPage();
        BarReachesTheGlass();
    }

    // ============================================================================================
    // S103 / QC C-04 + H-07 — THE BAR IS DRAWN UNDISTORTED, AND ITS HIT MAP FOLLOWS IT
    //
    // The bar was drawn `0..w` against a height-derived scale at 21 sites, so component_48 was
    // stretched 12.2% horizontally on every page - its 130x130 crosshair rendered 23x21 - while the
    // hit map encoded the same stretch independently. This is the fence: the drawn box must be
    // UNIFORM, and every icon's drawn centre must map back inside its OWN hit band, at more than one
    // panel aspect. A future "just make the bar reach both edges again" fails here rather than on the
    // glass.
    // ============================================================================================
    static void BottomBarUndistorted()
    {
        // The shipped screens plus two deliberately different aspects, including one NARROWER than the
        // design (where Rect clamps and there is no letterbox to sit in).
        int[,] sizes = { { 1280, 703 }, { 1280, 710 }, { 2560, 1406 }, { 1000, 800 } };
        BarFit[] fits = { BarFit.Frame, BarFit.Stretch, BarFit.Split };
        for (int i = 0; i < sizes.GetLength(0); i++)
        {
            int w = sizes[i, 0], h = sizes[i, 1];
            string at = " @" + w + "x" + h;

            float bx, by, bw, bh;
            BottomBar.Rect(w, h, BarFit.Frame, out bx, out by, out bw, out bh);

            // UNDISTORTED: the bar's own x-scale and y-scale are the same number. This is the whole
            // finding - `bw / RefW` used to be `w / RefW` while `bh / 235` was `h / RefH`.
            float kx = bw / RefW, ky = bh / 235f;
            Check("bar is undistorted" + at, System.Math.Abs(kx - ky) < 1e-4f,
                  "x-scale " + kx + " vs y-scale " + ky);

            // ...and where the panel is at least as wide as the design aspect - which every shipped
            // screen is (1280x703 is 1.82 against the design's 1.623) - it sits inside the panel.
            // On a TALLER panel the bar overflows with the page art rather than being clamped; see
            // BottomBar.Rect's own note for why clamping is the wrong answer there.
            if (w >= RefW * (h / RefH) - 0.01f)
                Check("bar fits the panel" + at, bx >= -0.01f && bx + bw <= w + 0.01f,
                      "x " + bx + " w " + bw);

            // *** SUPERSEDED IN PLACE 2026-09-06 (S176) - THE TWO CHECKS ABOVE ARE STILL EXACTLY
            // RIGHT FOR BarFit.Frame AND ARE KEPT AS THEY WERE. What has changed is that `bw / RefW`
            // is no longer "the bar's scale" on the other two fits: a spread bar's BOX is the whole
            // panel while every glyph in it is still drawn at h/RefH. So the undistorted property is
            // now asserted where it actually lives - on the emitted COMMANDS - by the sweep below,
            // and this block keeps proving the letterbox case it was written for.
            //
            // THE HEADER'S WARNING STILL STANDS, RE-READ: "A future 'just make the bar reach both
            // edges again' fails here rather than on the glass." S176 IS that change, and it does not
            // fail here, because it did not do the thing the fence forbids - it moved the ANCHORS and
            // left every SIZE at the uniform scale. EveryTileIsSquareToItsSource is the fence for
            // that, and it is the one a real re-stretch would trip.
            for (int f = 0; f < fits.Length; f++)
            {
                BarFit fit = fits[f];
                string atf = at + " " + fit;
                float k = BottomBar.Scale(h);
                float fx, fy, fw, fh;
                BottomBar.Rect(w, h, fit, out fx, out fy, out fw, out fh);

                // Every icon's DRAWN centre is a hit on ITS OWN index - the draw and the hit map
                // agreeing, which is what having one geometry is for.
                for (int n = 0; n < BottomBar.IconX.Length; n++)
                {
                    float cx = fx + (BottomBar.IconX[n] + BottomBar.IconS * 0.5f) * k;
                    float cy = fy + (BottomBar.IconY - 1877f + BottomBar.IconS * 0.5f) * k;
                    Check("icon " + n + " drawn centre hits itself" + atf,
                          BottomBar.Hit(cx, cy, w, h, fit) == n,
                          "got " + BottomBar.Hit(cx, cy, w, h, fit));
                }

                EveryTileIsSquareToItsSource(w, h, fit, atf);
                TheCornersStayRounded(w, h, fit, atf);
            }
        }
    }

    // ============================================================================================
    // S176 / S172 - THE BAR REACHES THE GLASS, AND NOTHING IN IT IS STRETCHED TO GET THERE
    //
    // The owner's finding, verbatim (2026-09-06 glass pass): "the bottom bar does not go to the edge
    // of the screen as it should", then "all pages have the bottom bar problem". The fix moves the
    // bar's ANCHORS into the page's own x-map and leaves every SIZE at the uniform h/RefH. These
    // three suites are that sentence, each half asserted where it can actually be measured.
    // ============================================================================================

    /// <summary>Every tile the bar emits is drawn at the SAME scale in x and y as the box it was cut
    /// from - the QC C-04 property, asserted on the COMMANDS rather than on a constant, so it holds
    /// under a fit that deliberately makes the bar's box wider than the design frame.</summary>
    static void EveryTileIsSquareToItsSource(int w, int h, BarFit fit, string at)
    {
        // key -> the cut's own pixel size in component_48 (plugin/tools/slice_bottom_bar.py).
        string[] keys = { "bar_cap_left", "bar_cap_right", "bar_nav_0", "bar_nav_1", "bar_nav_2",
                          "bar_nav_3", "bar_nav_4", "bar_label_current_state",
                          "bar_label_pointing_mode", "bar_value_pointing_mode", "bar_comm_block" };
        float[,] src = { {132,105},{132,105},{80,80},{80,80},{80,80},{80,80},{80,80},
                         {158,22},{158,22},{144,30},{686,59} };

        PageState st = new PageState(); st.Valid = true; st.Phase = "ORBIT";
        DisplayList dl = new DisplayList(64);
        BottomBar.Draw(dl, w, h, st, fit);
        float k = BottomBar.Scale(h);

        int found = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            // The flattened raster is GONE - clause (1)'s "never left flattened once its own export
            // exists". A revert to dl.Asset("component_48", ...) is exactly what this catches.
            Check("the bar no longer draws the flattened component" + at,
                  c.AssetKey != "component_48", "");
            if (c.Kind != DrawKind.Image || c.AssetKey == null) continue;
            int n = System.Array.IndexOf(keys, c.AssetKey);
            Check("bar tile is one of the cuts" + at, n >= 0, "stray key " + c.AssetKey);
            if (n < 0) continue;
            found++;
            Check(c.AssetKey + " is drawn at its own scale in x" + at,
                  System.Math.Abs(c.C - src[n, 0] * k) < 0.01f,
                  "w " + c.C + ", want " + (src[n, 0] * k));
            Check(c.AssetKey + " is drawn at its own scale in y" + at,
                  System.Math.Abs(c.D - src[n, 1] * k) < 0.01f,
                  "h " + c.D + ", want " + (src[n, 1] * k));
        }
        Check("all 11 bar tiles are drawn" + at, found == keys.Length, "found " + found);
    }

    /// <summary>The frame's bottom corners are ROUNDED, so the straight top rule must stop short of
    /// them - measured: row 105's white run in the asset is x 90..3336 of 3427, and inside that the
    /// border is the corner ARC, which is a tile.
    ///
    /// ⛔ WRITTEN BECAUSE A MUTATION SURVIVED. Drawing the rule `left..left+bw` instead of insetting it
    /// by `RuleStart` squares off both corners of every page in the build, and nothing failed. It is
    /// asserted here as a property of the RENDER - no White rect may cover the corner region on the
    /// rule's own row - rather than as a repeat of the arithmetic, so a second way of getting it wrong
    /// is caught too.</summary>
    static void TheCornersStayRounded(int w, int h, BarFit fit, string at)
    {
        PageState st = new PageState(); st.Valid = true; st.Phase = "ORBIT";
        DisplayList dl = new DisplayList(64);
        BottomBar.Draw(dl, w, h, st, fit);

        float bx, by, bw, bh;
        BottomBar.Rect(w, h, fit, out bx, out by, out bw, out bh);
        float k = BottomBar.Scale(h);
        // Halfway into the corner region, on the rule's own row: inside the arc, well clear of the
        // 2 px side border, and exactly where a full-width rule would paint.
        float probeY = by + 105f * k + 0.5f;
        float[] probeX = { bx + 45f * k, bx + bw - 45f * k };
        for (int e = 0; e < 2; e++)
        {
            bool painted = false;
            for (int i = 0; i < dl.Count; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind != DrawKind.Rect) continue;
                if (c.Colour.R < 0.99f || c.Colour.G < 0.99f || c.Colour.B < 0.99f) continue;
                if (probeX[e] >= c.A && probeX[e] < c.A + c.C
                    && probeY >= c.B && probeY < c.B + c.D) painted = true;
            }
            Check("the bar's " + (e == 0 ? "left" : "right") + " corner stays rounded" + at,
                  !painted, "a white rect covers the corner at x " + probeX[e]);
        }
    }

    /// <summary>Every page's bar is laid out in that page's OWN map - checked against the tile the
    /// page ACTUALLY drew, not against the table that decided it. This is the trap the S103 header
    /// names: draw, hit and marker move together or not at all.</summary>
    static void BarFollowsItsPage()
    {
        const int VW = 2560, VH = 1406;
        PageState st = new PageState(); st.Valid = true; st.Phase = "ORBIT";
        foreach (UiPage up in (UiPage[])System.Enum.GetValues(typeof(UiPage)))
        {
            DisplayList dl = new DisplayList(1200);
            FigmaUI.Build(dl, up, VW, VH, st, MapProjection.Default());

            // Find the first nav icon the page drew, and use its own rectangle as the probe.
            float ix = -1f, iy = -1f, iw = 0f, ih = 0f;
            for (int i = 0; i < dl.Count; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind == DrawKind.Image && c.AssetKey == "bar_nav_0")
                { ix = c.A; iy = c.B; iw = c.C; ih = c.D; }
            }
            Check(up + ": drew the first nav icon", ix >= 0f, "no bar_nav_0 command");
            if (ix < 0f) continue;

            Check(up + ": nav icon is square (QC C-04)", System.Math.Abs(iw - ih) < 0.01f,
                  iw + " x " + ih);
            Check(up + ": the drawn icon's own centre is a hit on icon 0",
                  FigmaUI.BottomBarHit(up, ix + iw * 0.5f, iy + ih * 0.5f, VW, VH) == 0,
                  "drawn at " + ix + ".." + (ix + iw) + ", hit "
                  + FigmaUI.BottomBarHit(up, ix + iw * 0.5f, iy + ih * 0.5f, VW, VH));
        }
    }

    /// <summary>S172's own DONE-when, as arithmetic: the spread pages reach both glass edges, the
    /// letterboxed ones still agree with their frame art, and the Cover's bar rule lands back on the
    /// Cover's own column divider.</summary>
    static void BarReachesTheGlass()
    {
        const int VW = 2560, VH = 1406;
        int spread = 0, boxed = 0;
        foreach (UiPage up in (UiPage[])System.Enum.GetValues(typeof(UiPage)))
        {
            BarFit fit = BottomBar.FitFor(up);
            float bx, by, bw, bh;
            BottomBar.Rect(VW, VH, fit, out bx, out by, out bw, out bh);
            if (fit == BarFit.Frame)
            {
                boxed++;
                // The design frame's own box, where the frame ART is - H-07's fix, unchanged.
                Check(up + ": letterboxed bar still sits in the design frame",
                      System.Math.Abs(bx - (VW - 3427f * BottomBar.Scale(VH)) * 0.5f) < 0.01f,
                      "x " + bx);
            }
            else
            {
                spread++;
                Check(up + ": bar reaches the left edge", System.Math.Abs(bx) < 0.01f, "x " + bx);
                Check(up + ": bar reaches the right edge",
                      System.Math.Abs(bx + bw - VW) < 0.01f, "right " + (bx + bw));
            }
        }
        // Counted, not asserted loosely: 16 spread page-views against 19 letterboxed ones. If a new
        // page lands in the wrong half of FitFor, this moves and says so.
        Check("16 page-views spread, 19 letterbox", spread == 16 && boxed == 19,
              spread + " spread, " + boxed + " letterboxed");

        // *** S172's SECOND defect, the one that reads as broken: the bar's first rule CONTINUES the
        // Cover's procedure column divider at design 1441. The reference puts it 23 design px right
        // of it; ours sat 155 px right because the whole bar carried the letterbox offset.
        // ⛔ READ FROM THE CONSTANT THE DRAW USES, not from a copy of it. Written with a literal
        // 1464 first, and the mutation that moved Rule1X sailed straight past this check.
        float divider = SplitReflow.X(1441f, VW, VH);
        float rule = BottomBar.MapX(BottomBar.Rule1X, VW, VH, BarFit.Split);
        Check("the Cover's bar rule continues its column divider",
              rule - divider > 0f && rule - divider < 20f,
              "gap " + (rule - divider) + " px (was 155)");

        // CURRENT STATE's clear run, in DESIGN px, on each fit - the run its own docstring quotes.
        // The longest string the baked art ever showed was 798 design px wide.
        Console.WriteLine("  note  CURRENT STATE clear run (design px): Frame "
            + BottomBar.ValueRun(VW, VH, BarFit.Frame).ToString("0")
            + ", Split " + BottomBar.ValueRun(VW, VH, BarFit.Split).ToString("0")
            + ", Stretch " + BottomBar.ValueRun(VW, VH, BarFit.Stretch).ToString("0"));
        Check("the value's clear run never shrinks below the letterbox case",
              BottomBar.ValueRun(VW, VH, BarFit.Split) >= BottomBar.ValueRun(VW, VH, BarFit.Frame)
              && BottomBar.ValueRun(VW, VH, BarFit.Stretch) >= BottomBar.ValueRun(VW, VH, BarFit.Frame),
              "Frame " + BottomBar.ValueRun(VW, VH, BarFit.Frame)
              + " Split " + BottomBar.ValueRun(VW, VH, BarFit.Split)
              + " Stretch " + BottomBar.ValueRun(VW, VH, BarFit.Stretch));
    }
}
