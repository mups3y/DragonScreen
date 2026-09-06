/*
 * DragonScreen headless tests — VehicleOverviewPage's centre-block geometry (S185, UNIT 3)
 *
 * ⛔ EVERY NUMBER IN THIS FILE IS A LITERAL AND NONE OF THEM IS READ OFF THE PAGE. That is the whole
 * design of this suite, and it is the specific failure [[S176]]'s two unit-1 edits hit twice and
 * [[S181]] then designed against: a suite derived from the value under test cannot notice that value
 * changing. If this file said `VehicleOverviewPage.<anything>`, mutating the page would move the draw
 * AND the assertion together and the suite would stay green while the page walked off its sources.
 *
 * The numbers below come from THREE independent sources, all in-repo (C7), all re-checked by S185:
 *
 *   1. `assets/reference/dragon2-ui-assets/src/components/Overview.vue` — this page's own declared
 *      source (see the page's file header). Its CSS puts the centre block on the page centreline:
 *      `.circular-progess-menu { left: 50%; transform: translate(-50%,-50%) }` and
 *      `#dragon-crew { left: 50% }`.
 *   2. `assets/reference/nasa/interface_1950x1260.png` — the owner's rendered mock OF THIS PAGE,
 *      landed and hashed by [[S184]] on 2026-09-07. Measured on that render, against a card frame of
 *      1708x1019 at origin (99,99): the four big gauge labels centre at 0.5007 of frame width, the
 *      four small ones at 0.4952, and the capsule at ~0.50.
 *   3. `plugin/src/pure/VehicleSubsystemPage.cs` — OUR OWN sibling page, which draws the same
 *      `dragon_crew` asset in the same 520x760 design slot at x 1453, i.e. slot centre 1713.
 *
 * ---- WHY IT DRAWS AT 3427x2112 ----
 * That is the page's own design frame, so sx = sy = 1 and a drawn coordinate IS a design coordinate.
 * Every assertion can then be read against the sources with no scale arithmetic in between.
 *
 * ---- WHAT THIS SUITE IS *NOT* ----
 * ⛔ It does not pin how a gauge is DRAWN — not the ring's weight, its gap angle, or where the label
 * sits relative to it. S185 measured all three as divergences from both sources and refused to change
 * them here, because `Gauge` is shared verbatim with `VehicleSubsystemPage` across seven page-views
 * (see the register). Pinning the drawing would go blind the moment the drawing legitimately changes;
 * these checks ask "is this element WHERE ITS SOURCES PUT IT", which survives that.
 * ⛔ It also does not pin the gauge PITCH as correct — only as UNCHANGED. The CSS wants 0.1375 of
 * width and the mock renders 0.1225; the two sources disagree, so S185 kept this build's own 450
 * design units and wrote the disagreement up (C1.14) rather than picking. The check below exists so
 * that a later chat cannot pick one silently.
 */
using System;
using DragonScreen;

public static class VehicleGeometryTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Near(string what, float got, float want, float tol)
    { Check(what, Math.Abs(got - want) <= tol, "got " + got + ", want " + want); }

    const int W = 3427, H = 2112;

    /// <summary>The page centreline as a FRACTION of width — the one number all three sources agree
    /// on, typed as a literal so a mutation to the page's own 1713 cannot hide behind it.</summary>
    const float CentreFrac = 0.5f;

    /// <summary>The capsule, in design units. ⚠ S192 REPLACED THE SLOT WITH A HEIGHT: the owner ruled
    /// the render "short and fat ... it should be the same size and proportions as the one in green
    /// box", so the draw is now anchored on its HEIGHT and takes its width from the art's own aspect at
    /// draw time. `CapH` is his mock's own figure — the capsule's ink is 468 of that render's 1019-px
    /// card, 0.4593 of frame height, and 0.4593 x 2112 = 970. `CapTop` seats it clear of the big gauges
    /// above and CABIN MICS below.</summary>
    const float CapH = 970f, CapTop = 630f;

    /// <summary>`dragon_crew.png` is 294x468 on disk, and this is now the CHECK rather than a note.
    /// ⭐ S185 measured the old draw as a 22.2 % horizontal stretch of a PNG carrying the SPACEX, NASA
    /// and DRAGON wordmarks — QC `C-04`'s rule — and was told to write it up rather than fix it. S192 is
    /// the owner fixing it, in his own words. The assertion below therefore tests the DEVICE aspect of
    /// the drawn box against this ratio, which is the only form of the check that means anything: this
    /// page maps x through sx and y through sy, so a fixed pair of design constants has a DIFFERENT
    /// device aspect at every resolution, and the suite runs at the design frame while the mod ships at
    /// 2560x1406. A page that passed by luck at one size would fail here at the other.</summary>
    const float ArtW = 294f, ArtH = 468f;

    /// <summary>The small-gauge row (S192, closing S186). `SmallPitch` is the mock's own 0.1013 of
    /// width = 347 design units; the offsets are from the page centreline. Typed from the mock, not
    /// read from the page.</summary>
    const float SmallInner = 430f, SmallOuter = 778f, SmallPitch = 348f, SmallCy = 1000f;

    /// <summary>The big-gauge pitch this build draws. UNCHANGED by S185 and pinned as unchanged —
    /// see the header on why it is not pinned as *correct*.</summary>
    const float BigPitch = 450f;

    /// <summary>Design radii, and the gauge label size. The label size is here to guard R-01: S185
    /// moved draws and moved no sizes, and [[S153b]] is HELD on all 441 of this family's.</summary>
    const float BigR = 175f, SmallR = 120f, LabelSize = 24f;

    static DisplayList Built(bool valid)
    {
        PageState s = new PageState();
        s.Valid = valid;
        if (valid)
        {
            s.Ppo2Text = "2.86"; s.CabinTempText = "21.8"; s.PressText = "14.72"; s.Co2Text = "1.64";
            s.LoopAText = "26.4"; s.LoopBText = "20.1";
            s.NetPwr1Text = "-59"; s.NetPwr2Text = "-49";
        }
        DisplayList dl = new DisplayList(VehicleOverviewPage.Commands + BottomBar.Commands + 64);
        VehicleOverviewPage.Build(dl, W, H, s);
        return dl;
    }

    /// <summary>The x of the centred Text command whose string is exactly <paramref name="s"/>.
    /// A centred draw's x IS its centre, so this is the gauge's own centre x.</summary>
    static float CentredX(DisplayList dl, string s, out float size, out bool found)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == s && c.Align == TextAlign.Centre)
            { size = c.C; found = true; return c.A; }
        }
        size = 0f; found = false; return float.NaN;
    }

    static float Label(DisplayList dl, string s)
    {
        float size; bool ok;
        float x = CentredX(dl, s, out size, out ok);
        Check("\"" + s + "\" is drawn, centred", ok, "no centred text command");
        if (ok) Near("\"" + s + "\" label size is unmoved (R-01)", size, LabelSize, 0.01f);
        return x;
    }

    /// <summary>The `dragon_crew` asset command's box.</summary>
    static bool Capsule(DisplayList dl, out float x, out float y, out float aw, out float ah)
    {
        x = y = aw = ah = 0f;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Image && c.AssetKey == "dragon_crew")
            { x = c.A; y = c.B; aw = c.C; ah = c.D; return true; }
        }
        return false;
    }

    public static int Run()
    {
        Console.WriteLine("VehicleGeometryTest (S185 / unit 3: the centre block sits on the page centreline)");
        DisplayList dl = Built(true);
        float centre = CentreFrac * W;          // 1713.5 design units

        // ---- 1. THE CAPSULE ------------------------------------------------------------------------
        // Sources 1 and 2 put it on the centreline; source 3 — our own sibling page — draws the same
        // asset in the same box at x 1453. All three agree, and this page used to draw it at 1560.
        float ax, ay, aw, ah;
        Check("the capsule asset is drawn", Capsule(dl, out ax, out ay, out aw, out ah), "no dragon_crew");
        Near("capsule top y", ay, CapTop, 0.6f);
        Near("capsule height is the mock's own 0.4593 of frame height", ah, CapH, 0.6f);
        Near("capsule CENTRE is the page centreline", ax + aw * 0.5f, centre, 0.6f);

        // ⭐ THE ASPECT IS NOW ASSERTED, NOT MERELY RECORDED — S192, on the owner's ruling. And it is
        // asserted TWICE, at two resolutions, because that is the only way the check can fail for the
        // right reason: this page maps x by sx and y by sy, so a fixed design box has a different
        // device aspect at every size. A single-resolution check would pass a page that is undistorted
        // here and stretched on the glass, which is exactly the state S185 measured and reported.
        Near("the drawn capsule has the ART's aspect at the design frame", aw / ah, ArtW / ArtH, 0.002f);
        DisplayList ship = new DisplayList(VehicleOverviewPage.Commands + BottomBar.Commands + 64);
        PageState sp = new PageState(); sp.Valid = true;
        VehicleOverviewPage.Build(ship, 2560, 1406, sp);
        float bx, by, bw, bh;
        Check("the capsule is drawn at the shipped size too",
              Capsule(ship, out bx, out by, out bw, out bh), "no dragon_crew at 2560x1406");
        Near("and it has the ART's aspect THERE too (the 22.2 % stretch is gone)",
             bw / bh, ArtW / ArtH, 0.002f);
        Near("and it is still centred at the shipped size", (bx + bw * 0.5f) / 2560f, CentreFrac, 0.001f);

        // ---- 2. THE FOUR BIG GAUGES ---------------------------------------------------------------
        float g0 = Label(dl, "PPO2"),  g1 = Label(dl, "CABIN TEMP");
        float g2 = Label(dl, "CABIN PRESSURE"), g3 = Label(dl, "CO2");
        Near("big-gauge cluster centre is the page centreline", (g0 + g3) * 0.5f, centre, 0.6f);
        Near("big-gauge inner pair is centred too", (g1 + g2) * 0.5f, centre, 0.6f);
        Near("big-gauge pitch is UNCHANGED (0 -> 1)", g1 - g0, BigPitch, 0.6f);
        Near("big-gauge pitch is UNCHANGED (1 -> 2)", g2 - g1, BigPitch, 0.6f);
        Near("big-gauge pitch is UNCHANGED (2 -> 3)", g3 - g2, BigPitch, 0.6f);

        // ---- 3. THE FOUR SMALL GAUGES — ONE ROW, NOT TWO STACKS (S192, closing S186) ---------------
        // 🟢 OWNER, 2026-09-07: "Loop a loop b net pwr 1 net pwr 2 need to be arranged in the same
        // layout" — the mock's, which is one row of four flanking the capsule. Both of this page's
        // sources agree on that row to within 1.5 % (Overview.vue 0.100 of width pitch, the mock
        // 0.1013), so the pitch below is a source figure and not a preference.
        float la = Label(dl, "LOOP A"), lb = Label(dl, "LOOP B");
        float n1 = Label(dl, "NET PWR1"), n2 = Label(dl, "NET PWR2");
        Near("LOOP A is the outer left gauge", la, centre - SmallOuter, 0.6f);
        Near("LOOP B is the inner left gauge", lb, centre - SmallInner, 0.6f);
        Near("NET PWR1 is the inner right gauge", n1, centre + SmallInner, 0.6f);
        Near("NET PWR2 is the outer right gauge", n2, centre + SmallOuter, 0.6f);
        Near("the small-gauge pitch is the mock's own", lb - la, SmallPitch, 0.6f);
        Near("and the same on the right", n2 - n1, SmallPitch, 0.6f);
        Near("the row is centred on the centreline", (la + n2) * 0.5f, centre, 0.6f);
        Check("all four small gauges are on ONE row, not two stacks",
              Math.Abs(la - lb) > 300f && Math.Abs(n1 - n2) > 300f,
              "LOOP A/B dx " + (lb - la) + ", NET PWR dx " + (n2 - n1));
        // ⭐ AND THE INNER PAIR CLEARS THE ENLARGED CAPSULE. This is the check that would have caught
        // the collision the bigger render creates: at the mock's own 407-unit inner offset the rings
        // would overlap its box, which is why the page opens it to 430.
        Check("the inner rings clear the capsule's box on both sides",
              (centre - SmallInner) + SmallR <= ax + 1f && (centre + SmallInner) - SmallR >= ax + aw - 1f,
              "LOOP B right " + ((centre - SmallInner) + SmallR) + " vs capsule left " + ax);

        // ---- 4. THE TITLE THE CENTRELINE WAS TAKEN FROM -------------------------------------------
        // The page has always centred its title at 1713. The defect S185 fixed was that nothing else
        // did; this check is what makes the centreline one fact rather than four coincidences.
        float tsize; bool tok;
        float tx = CentredX(dl, "VEHICLE OVERVIEW", out tsize, out tok);
        Check("the title is drawn, centred", tok, "");
        Near("the title is on the same centreline", tx, centre, 0.6f);

        // ---- 5. THE RADII AND THE ROW HEIGHTS DID NOT MOVE ----------------------------------------
        // A translation must not have become a resize. The rings are ArcBands at the gauge centres;
        // assert one of each size sits at the expected radius, centred where its label is.
        Check("a big ring of radius 175 sits under PPO2", Ring(dl, g0, 430f, BigR), "");
        Check("a small ring of radius 120 sits under LOOP A", Ring(dl, la, SmallCy, SmallR), "");

        // ---- 6. THE DEAD-FEED PATH STILL DRAWS THE SAME GEOMETRY ----------------------------------
        // S22's guard changes the STRINGS, never the places. If a future edit gates a coordinate on
        // `valid`, the page would move when the feed dies and nothing else would notice.
        DisplayList dead = Built(false);
        float dax, day, daw, dah;
        Check("the capsule is drawn on a dead feed too", Capsule(dead, out dax, out day, out daw, out dah), "");
        Near("dead-feed capsule x is unmoved", dax, ax, 0.6f);
        Near("dead-feed capsule width is unmoved", daw, aw, 0.6f);
        float dsz; bool dok;
        Near("dead-feed PPO2 label x is unmoved", CentredX(dead, "PPO2", out dsz, out dok), g0, 0.6f);
        Check("dead-feed PPO2 label was found", dok, "");

        // ---- 7. S192: THE BOTTOM-LEFT CONTROLS MUST NOT OVERLAP EACH OTHER ------------------------
        // ⛔ THIS CHECK EXISTS BECAUSE THE COLLISION HAPPENED. The owner's button pair went in at design
        // x124..884, y1700..1851, and `VehicleSubsystemPage`'s FUNCTIONS | ALERTS toggle was sitting at
        // x150..530, y1736..1820 — inside it. Nothing caught that: it was found by eye on a sibling
        // page's render, as a stray "S" painted behind the SYS P&ID pill. It was a HIT collision too —
        // `FigmaUI` routes the pills for every vehicle page while `ScreenPainter` routes the toggle, so
        // one touch resolved two ways — and a paint-order accident is what made it visible at all.
        // ⚠ The numbers are literals on BOTH sides on purpose. Reading either rect from the code under
        // test would let the two move together into a new overlap and keep this green.
        // ⛔ AND THE TWO RECTS ARE PROBED FROM THE CODE, NOT TYPED IN — which is the opposite of this
        // suite's usual rule and is right HERE for a reason worth recording. A first version compared
        // two sets of literals and a mutation that moved the pills straight back on top of the toggle
        // SURVIVED it: literals on both sides test that the DESIGN does not overlap, never that the
        // CODE still matches the design. A collision check has to read where the two things actually
        // are. `Rect` is the same function `Draw` and `HitTest` share, and the toggle band is found by
        // asking `ToggleHit` itself where it answers.
        float p0x, p0y, p0w, p0h, p1x, p1y, p1w, p1h;
        VehicleDeepViewLinks.Rect(0, out p0x, out p0y, out p0w, out p0h);
        VehicleDeepViewLinks.Rect(1, out p1x, out p1y, out p1w, out p1h);
        float pillL = Math.Min(p0x, p1x), pillR = Math.Max(p0x + p0w, p1x + p1w);
        float pillT = Math.Min(p0y, p1y), pillB = Math.Max(p0y + p0h, p1y + p1h);

        // Sweep the toggle's own answer to find the band it claims, in design units.
        float togL = float.MaxValue, togR = float.MinValue, togT = float.MaxValue, togB = float.MinValue;
        for (int dy = 1400; dy < 1900; dy += 2)
            for (int dx = 100; dx < 900; dx += 2)
                if (VehicleSubsystemPage.ToggleHit(dx * W / (float)W, dy * H / (float)H, W, H) >= 0)
                {
                    if (dx < togL) togL = dx;
                    if (dx > togR) togR = dx;
                    if (dy < togT) togT = dy;
                    if (dy > togB) togB = dy;
                }
        Check("the FUNCTIONS|ALERTS toggle still claims a band at all", togL < togR, "none found");
        bool xOverlap = togL < pillR && togR > pillL;
        bool yOverlap = togT < pillB && togB > pillT;
        Check("the deep-view pills and the FUNCTIONS|ALERTS toggle do not overlap",
              !(xOverlap && yOverlap),
              "pills x" + pillL + ".." + pillR + " y" + pillT + ".." + pillB +
              "  toggle x" + togL + ".." + togR + " y" + togT + ".." + togB);
        Check("...and the clearance is in Y, which is how it is actually achieved",
              togB <= pillT, "toggle bottom " + togB + " vs pill top " + pillT);
        // ⛔ AND THE PILLS MUST SIT ABOVE THE BOTTOM BAR'S VISIBLE EDGE. BottomBar draws over everything,
        // so a pill running past it is half-hidden on the glass and its lower half is a touch target the
        // crew cannot see.
        // ⚠ THE NUMBER IS 1983, NOT `BottomBar.BarY`'s 1877, AND THE DIFFERENCE IS THE WHOLE POINT.
        // 1877 is the top of the bar's BOX; `component_48` carries ~106 design units of TRANSPARENT
        // margin above its artwork, so the visible edge is at 1983. S192's first pass ended the panel at
        // 1877 and looked flush by the code while leaving a 105-unit gap on the glass, which is what the
        // owner then asked to close. Measured on the render: the frame rule is flat at 1983 for every
        // column from x124 to x2530. Literals on both sides, because the bar is not this page's to read.
        const float BarVisibleTop = 1983f;
        Check("the deep-view pills sit clear of the bottom bar's VISIBLE edge",
              pillB <= BarVisibleTop, "pill bottom " + pillB + " vs bar edge " + BarVisibleTop);
        Check("...and they sit CLOSE to it, which is what the owner asked for",
              BarVisibleTop - pillB <= 40f,
              "gap " + (BarVisibleTop - pillB) + " design units, want <= 40");

        // The pills must also clear the tab strip's leftmost hit edge (CentreX(0) - half-pitch).
        Check("the pills clear the tab strip's leftmost hit edge",
              pillR < VehicleTabBar.CentreX(0) - 100f,
              "pill right " + pillR + " vs tab edge " + (VehicleTabBar.CentreX(0) - 100f));

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    /// <summary>Is there an ArcBand centred at (cx, cy) whose outer radius is <paramref name="r"/>?
    /// `Gauge` draws the track as one band from r-rw to r, so the outer radius is the design radius.</summary>
    static bool Ring(DisplayList dl, float cx, float cy, float r)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.ArcBand
                && Math.Abs(c.A - cx) <= 0.6f && Math.Abs(c.B - cy) <= 0.6f
                && Math.Abs(c.D - r) <= 0.6f) return true;
        }
        return false;
    }
}
