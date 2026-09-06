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

    /// <summary>The capsule slot, in design units. LITERALS: `VehicleSubsystemPage` uses 1453 and a
    /// 520x760 box, and this page must match it asset-for-asset.</summary>
    const float SlotX = 1453f, SlotW = 520f, SlotY = 760f, SlotH = 760f;

    /// <summary>`dragon_crew.png` is 294x468 on disk. Typed here so the SLOT ASPECT QUESTION S185
    /// raised cannot be closed by accident: 520/760 = 0.6842 against the art's 294/468 = 0.6282, and
    /// at the shipped 2560x1406 that is a 22.2 % horizontal stretch of a PNG carrying the SPACEX,
    /// NASA and DRAGON wordmarks — QC `C-04`'s rule. ⛔ NOT FIXED IN S185, deliberately: the prompt
    /// ruled the slot aspect "a real question — write it up, do not do it quietly", and the same slot
    /// is drawn by the sibling page. This constant is here so the number stays visible.</summary>
    const float ArtW = 294f, ArtH = 468f;

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
        Near("capsule slot x (= VehicleSubsystemPage's own 1453)", ax, SlotX, 0.6f);
        Near("capsule slot y", ay, SlotY, 0.6f);
        Near("capsule slot width", aw, SlotW, 0.6f);
        Near("capsule slot height", ah, SlotH, 0.6f);
        Near("capsule slot CENTRE is the page centreline", ax + aw * 0.5f, centre, 0.6f);

        // ⚠ THE SLOT ASPECT IS RECORDED, NOT ASSERTED CORRECT. This is the open question, pinned so a
        // later chat that changes the slot has to come past this line and read why.
        Check("the slot is still the 520x760 the aspect question is ABOUT",
              Math.Abs(SlotW / SlotH - 0.684f) < 0.001f && Math.Abs(ArtW / ArtH - 0.628f) < 0.001f,
              "slot " + (SlotW / SlotH) + " vs art " + (ArtW / ArtH));

        // ---- 2. THE FOUR BIG GAUGES ---------------------------------------------------------------
        float g0 = Label(dl, "PPO2"),  g1 = Label(dl, "CABIN TEMP");
        float g2 = Label(dl, "CABIN PRESSURE"), g3 = Label(dl, "CO2");
        Near("big-gauge cluster centre is the page centreline", (g0 + g3) * 0.5f, centre, 0.6f);
        Near("big-gauge inner pair is centred too", (g1 + g2) * 0.5f, centre, 0.6f);
        Near("big-gauge pitch is UNCHANGED (0 -> 1)", g1 - g0, BigPitch, 0.6f);
        Near("big-gauge pitch is UNCHANGED (1 -> 2)", g2 - g1, BigPitch, 0.6f);
        Near("big-gauge pitch is UNCHANGED (2 -> 3)", g3 - g2, BigPitch, 0.6f);

        // ---- 3. THE FOUR SMALL GAUGES -------------------------------------------------------------
        // LOOP A/B share one column and NET PWR1/2 the other, so the check is that the two columns are
        // equidistant from the centreline — the symmetry the ±330 gaps around the capsule encode.
        float la = Label(dl, "LOOP A"), lb = Label(dl, "LOOP B");
        float n1 = Label(dl, "NET PWR1"), n2 = Label(dl, "NET PWR2");
        Near("LOOP A and LOOP B share one column", la, lb, 0.6f);
        Near("NET PWR1 and NET PWR2 share one column", n1, n2, 0.6f);
        Near("the two small-gauge columns are centred on the centreline", (la + n1) * 0.5f, centre, 0.6f);
        Near("LOOP column clears the capsule by the same gap the NET PWR column does",
             SlotX - la, n1 - (SlotX + SlotW), 0.6f);

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
        Check("a small ring of radius 120 sits under LOOP A", Ring(dl, la, 900f, SmallR), "");

        // ---- 6. THE DEAD-FEED PATH STILL DRAWS THE SAME GEOMETRY ----------------------------------
        // S22's guard changes the STRINGS, never the places. If a future edit gates a coordinate on
        // `valid`, the page would move when the feed dies and nothing else would notice.
        DisplayList dead = Built(false);
        float dax, day, daw, dah;
        Check("the capsule is drawn on a dead feed too", Capsule(dead, out dax, out day, out daw, out dah), "");
        Near("dead-feed capsule x is unmoved", dax, SlotX, 0.6f);
        float dsz; bool dok;
        Near("dead-feed PPO2 label x is unmoved", CentredX(dead, "PPO2", out dsz, out dok), g0, 0.6f);
        Check("dead-feed PPO2 label was found", dok, "");

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
