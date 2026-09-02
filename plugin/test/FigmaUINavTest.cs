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
