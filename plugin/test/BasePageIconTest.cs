/*
 * BasePageIconTest — `SPEC_BASE_SCREENS.md` §11's ICON column, asserted rather than argued.
 *
 * ⭐ SAME THREE IDIOMS AS `BasePageNoIconTest`, because they earned it:
 *  (1) THE SPEC'S OWN PATH STRING IS ONE SOURCE AND THE DRAW CODE IS ANOTHER. `BasePageIcon.WindowPath`
 *      is §6 ICON verbatim; nothing in the draw path parses it. Here it IS parsed, and the notch's four
 *      points, both bevels' 45 degrees and every extreme are checked against what the page emitted.
 *  (2) EVERY POSITION IS A LITERAL TYPED FROM THE SPEC, never read from `BasePageIcon`'s own arrays.
 *      A suite that spells its expectation `TabCentre(i)` cannot fail when `TabCentre` is what moved —
 *      S176's finding, and the reason S245 raised M6 against itself.
 *  (3) THE SCOPE IS IN THE TEST NAME for both `DrawKind.Image` counts. §10's two-row table exists
 *      because a bare "9" was true only of the page without its bar.
 *
 * ⚠ DESIGN SPACE ONLY. No rasteriser is reachable from `build.py test`; §11's DEVICE table for this
 * page is proved by `DragonScreenPreview.exe --basecheck`, which renders it at 2560x1405 and
 * 2560x1419, probes the shelf and the tab band, and is made to fail on purpose before it is believed.
 *
 * ⭐ THE FIXTURE RENDERS AT 1920x1054, where k = 1 and both offsets are 0, so device coordinates ARE
 * design coordinates and every assertion is a direct read of what was drawn.
 */
using DragonScreen;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

public static class BasePageIconTest
{
    static int checks, failures;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + (detail == "" ? "" : "   " + detail)); }
    }
    static void Near(string what, double got, double want, double tol)
    {
        Check(what, Math.Abs(got - want) <= tol,
              "got " + got.ToString("F4") + " want " + want.ToString("F4"));
    }

    static string Repo(params string[] parts)
    {
        var bits = new List<string> {
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", ".." };
        bits.AddRange(parts);
        return Path.GetFullPath(Path.Combine(bits.ToArray()));
    }

    /// <summary>⛔ Text assertions match commented-out code (S220). Only live lines are searched, and
    /// this file's own headers name every token it bans.</summary>
    static string Live(string src)
    {
        string[] lines = src.Replace("\r\n", "\n").Split(new char[] { (char)10 });
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].TrimStart();
            if (t.StartsWith("//") || t.StartsWith("///") || t.StartsWith("*") || t.StartsWith("/*")) continue;
            sb.Append(lines[i]).Append((char)10);
        }
        return sb.ToString();
    }

    const int FW = 1920, FH = 1054;
    static DisplayList Page(string ev1, string ev2)
    {
        DisplayList dl = new DisplayList(BasePageIcon.Commands + 8);
        BasePageIcon.Draw(dl, FW, FH, ev1, ev2);
        return dl;
    }
    static DisplayList Page() { return Page(null, null); }

    static int Count(DisplayList dl, DrawKind k)
    {
        int n = 0;
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == k) n++;
        return n;
    }
    static bool SameColour(Rgba a, Rgba b)
    {
        return Math.Abs(a.R - b.R) < 0.002f && Math.Abs(a.G - b.G) < 0.002f
            && Math.Abs(a.B - b.B) < 0.002f && Math.Abs(a.A - b.A) < 0.002f;
    }
    static int FindRect(DisplayList dl, float x, float y, float w, float h)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Rect) continue;
            if (Math.Abs(c.A - x) < 0.002f && Math.Abs(c.B - y) < 0.002f
                && Math.Abs(c.C - w) < 0.002f && Math.Abs(c.D - h) < 0.002f) return i;
        }
        return -1;
    }

    public static int Run()
    {
        Console.WriteLine("BasePageIconTest (spec §11, the ICON column - design space; pixels by --basecheck)");
        checks = 0; failures = 0;

        TheIconPathAndTheCodeAreTwoSourcesThatAgree();
        TheNotchIsDrawnOnTheSpecPath();
        TheTabStackIsDerivedAndLandsOnTheBorder();
        TheNineTabsAreWhereSection7PutsThem();
        TheSelectorMarksTabZeroAndNothingElse();
        ThereIsNoDimmingAnywhere();
        TheLetterSpacingIsRecordedButNotApplied();
        ImageCount_PageWithoutTheBar_IsNine();
        ImageCount_CompletePageIncludingTheBar_IsNineteen();
        TheBorderAndTheBarAreTheSameAsTheNonIconPage();
        TheColoursAreSection3s();
        TheTwoShippedSizesGiveIdenticalRatios();
        TheEventPopUpIsWiredOnThisPageToo();
        TheOldBarIsNotReferencedInLiveCode();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + (failures == 0 ? "   (device space is proved separately by --basecheck)" : ""));
        return failures;
    }

    // ============================================================================================
    //  1. §6 ICON's PATH TEXT, PARSED, AGAINST THE CONSTANTS AND THE DRAWING.
    // ============================================================================================
    struct SvgPath { public List<float[]> Pts; public List<float> Radii; }

    static SvgPath Parse(string d)
    {
        SvgPath p = new SvgPath();
        p.Pts = new List<float[]>(); p.Radii = new List<float>();
        string[] tok = d.Split(new char[] { ' ', ',', (char)10, (char)13 }, StringSplitOptions.RemoveEmptyEntries);
        int i = 0;
        while (i < tok.Length)
        {
            string op = tok[i++];
            if (op == "Z" || op == "z") break;
            if (op == "M" || op == "L") { p.Pts.Add(new float[] { F(tok[i]), F(tok[i + 1]) }); i += 2; }
            else if (op == "A")
            {
                p.Radii.Add(F(tok[i])); p.Radii.Add(F(tok[i + 1]));
                p.Pts.Add(new float[] { F(tok[i + 5]), F(tok[i + 6]) }); i += 7;
            }
            else throw new InvalidOperationException("unexpected path token '" + op + "'");
        }
        return p;
    }
    static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }
    static bool Has(List<float[]> p, float x, float y)
    {
        foreach (var q in p) if (Math.Abs(q[0] - x) < 0.002f && Math.Abs(q[1] - y) < 0.002f) return true;
        return false;
    }

    static void TheIconPathAndTheCodeAreTwoSourcesThatAgree()
    {
        SvgPath w = Parse(BasePageIcon.WindowPath);
        // ⭐ The ICON path is the NON-ICON path PLUS FOUR POINTS - that is what §6 says it is, and it
        // is checked as a difference rather than restated.
        SvgPath plain = Parse(BasePageNoIcon.WindowPath);
        Check("§6 the ICON path is the NON-ICON path plus exactly four points",
              w.Pts.Count == plain.Pts.Count + 4, w.Pts.Count + " vs " + plain.Pts.Count);
        Check("§6 ...and carries the same four corner arcs",
              w.Radii.Count == plain.Radii.Count, w.Radii.Count + " radii");

        // The four new points, as literals typed from §6.
        Check("§6 the ICON path carries the notch: (1453.5,960.5)", Has(w.Pts, 1453.5f, 960.5f), "");
        Check("§6 ...(1386,893)", Has(w.Pts, 1386f, 893f), "");
        Check("§6 ...(534,893)", Has(w.Pts, 534f, 893f), "");
        Check("§6 ...(466.5,960.5)", Has(w.Pts, 466.5f, 960.5f), "");

        // ...and they are what the code says they are.
        Near("§6 Shelf == 893", BasePageIcon.Shelf, 893.0, 0.002);
        Near("§6 ShelfLeft == 534", BasePageIcon.ShelfLeft, 534.0, 0.002);
        Near("§6 ShelfRight == 1386", BasePageIcon.ShelfRight, 1386.0, 0.002);
        // ⛔ RUN IS DERIVED, and 67.5 is the arithmetic - gen.py's own comment says 46.5 and is STALE.
        Near("§6 RUN = B - SHELF = 67.5 (gen.py's '46.5' comment is stale)",
             BasePageIcon.Run, 67.5, 0.002);
        Near("§6 the left bevel foot is 466.5", BasePageIcon.BevelLeft, 466.5, 0.002);
        Near("§6 the right bevel foot is 1453.5", BasePageIcon.BevelRight, 1453.5, 0.002);

        // ⭐ §11: "bevel rise vs run - equal (true 45 deg)". Read off the PATH, both bevels.
        Near("§11 the LEFT bevel's rise equals its run (true 45 deg)",
             960.5 - 893.0, 534.0 - 466.5, 0.002);
        Near("§11 the RIGHT bevel's rise equals its run (true 45 deg)",
             960.5 - 893.0, 1453.5 - 1386.0, 0.002);
        // ...and the notch is symmetric about the window's own centreline.
        Near("§6 the notch is centred on the window",
             (BasePageIcon.ShelfLeft + BasePageIcon.ShelfRight) / 2.0,
             (BasePageNoIcon.WindowLeft + BasePageNoIcon.WindowRight) / 2.0, 0.002);
    }

    // ============================================================================================
    //  2. THE NOTCH, AS DRAWN.
    // ============================================================================================
    static void TheNotchIsDrawnOnTheSpecPath()
    {
        DisplayList dl = Page();
        // §6's shelf stroke: a 2px band across the shelf, at 0.55 white.
        Check("§6 the shelf is stroked across 534..1386 at y 893, 2px",
              FindRect(dl, 534f, 892f, 852f, 2f) >= 0, "");
        // the bottom edge is SPLIT by the notch: it stops at 466.5 and restarts at 1453.5
        Check("§6 the window's bottom stroke stops at the left bevel foot (466.5)",
              FindRect(dl, 29.5f, 959.5f, 466.5f - 29.5f, 2f) >= 0, "");
        Check("§6 ...and restarts at the right bevel foot (1453.5)",
              FindRect(dl, 1453.5f, 959.5f, 1890.5f - 1453.5f, 2f) >= 0, "");

        // ⭐ §4: the two notch BEVELS are `Tri`. Four tris for the border's chamfers (two offset
        // regions x two) plus two fill wedges plus four for the two 2px bevel bands = 10.
        int tris = Count(dl, DrawKind.Tri);
        Check("§4 the notch bevels are drawn with Tri, not Line", tris == 10, "got " + tris);

        // Every bevel-band tri must lie in the notch's own x range and below the shelf: a tri that
        // wandered would be a bevel drawn somewhere else entirely.
        int inNotch = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Tri) continue;
            float ymin = Math.Min(c.B, Math.Min(c.D, c.EndDeg));
            if (ymin > 890f && ymin < 895f) inNotch++;      // the four bevel-band tris + two wedges
        }
        Check("§6 six tris start at the shelf (two fill wedges + two 2px bevel bands)",
              inNotch == 6, "got " + inNotch);

        // ⛔ THE FILL WEDGES' HYPOTENUSE IS THE SHAPE'S OWN EDGE AND MUST BE EXACTLY 45 DEGREES.
        int wedges = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Tri) continue;
            // the fill wedge: (bevelFoot, SHELF) (shelfEnd, SHELF) (bevelFoot, B)
            bool left = Math.Abs(c.A - 466.5f) < 0.002f && Math.Abs(c.B - 893f) < 0.002f
                     && Math.Abs(c.C - 534f) < 0.002f && Math.Abs(c.StartDeg - 466.5f) < 0.002f
                     && Math.Abs(c.EndDeg - 960.5f) < 0.002f;
            bool right = Math.Abs(c.A - 1453.5f) < 0.002f && Math.Abs(c.B - 893f) < 0.002f
                      && Math.Abs(c.C - 1386f) < 0.002f && Math.Abs(c.StartDeg - 1453.5f) < 0.002f
                      && Math.Abs(c.EndDeg - 960.5f) < 0.002f;
            if (left || right) wedges++;
        }
        Check("§6 both fill wedges sit exactly on the spec's bevel corners", wedges == 2, "got " + wedges);
    }

    // ============================================================================================
    //  3. §7's DERIVED STACK. ⛔ Derived from SHELF, not typed - and it must land on §5's border.
    // ============================================================================================
    static void TheTabStackIsDerivedAndLandsOnTheBorder()
    {
        // ⚠⚠ S256 — ~~`893 + 5.1 + 37 + 4 + 12.3 + 16 + 4.8 + 5.3 = 977.5`~~ SUPERSEDED IN PLACE
        // (C1.16). 🟢 OWNER, 2026-09-10: *"I would like the tab icons to be brought down a little to
        // sit exactly middle between the line above and below. BUT! I only want this if it is possible
        // to leave the selector line that runs underneath the tabs stays at it's current height."*
        // ⭐ `IconPad` and `LabelSelectorGap` TRADE 8.6 px, so the block moves down and nothing below
        // the label does: 893 + 13.7 + 37 + 4 + 12.3 + 7.4 + 4.8 + 5.3 = 977.5.
        Near("S256 §7 the icons start 13.7 below the shelf (was 5.1)", BasePageIcon.IconTop, 906.7, 0.002);
        Near("§7 the labels start 4 below the icons", BasePageIcon.LabelTop, 947.7, 0.002);
        // ⛔⛔ THESE TWO ARE THE OWNER'S CONDITION EXPRESSED AS ASSERTIONS. He allowed the move ONLY
        // if the selector stayed where it is; the second is §7's cross-check against §5's border.
        // ⛔ Neither may be quietly relaxed — if a later task needs one of them to move, that is a
        // question for the owner, not an edit to this line.
        Near("S256 ⛔ the selector has NOT moved — the owner's condition (967.4, held)",
             BasePageIcon.SelectorTop, 967.4, 0.002);
        Near("S256 ⛔ ...and the stack still sums to exactly 977.5", BasePageIcon.StackBottom, 977.5, 0.002);
        // ⭐ AND THE MOVE IS EVEN, which is the other half of what he asked for: *"Make sure to even it
        // out if it isnt"*. The two edges his eye reads are DRAWN INK, measured by luminance scan off
        // `plugin/build/preview/ui_baseicon_screen1.png` — the shelf line's visible lower edge at
        // 894.5 (its stroke runs ~1.5 px below `Shelf = 893.0`) and the selector bar's bottom at 972.2.
        // ⛔ ASSERTED AS THE EQUALITY, not as two literals: "even" is the requirement, 12.2 is only
        // today's value of it.
        const double ShelfInkBottom = 894.5, SelectorInkBottom = 972.2;
        double gapAbove = BasePageIcon.IconTop - ShelfInkBottom;
        double gapBelow = SelectorInkBottom - (BasePageIcon.LabelTop + BasePageIcon.LabelLineHeight);
        Near("S256 ⭐⭐ the icon+label block is EVENLY spaced between the two lines the eye reads",
             gapAbove, gapBelow, 0.002);
        Near("S256 ...and that even gap is 12.2 either side", gapAbove, 12.2, 0.002);
        // ⛔ CENTRING THE ICON ALONE IS IMPOSSIBLE AND THIS IS WHY: a 37px icon centred in the band
        // would drive the labels straight THROUGH the stationary selector. Pinned so nobody tries.
        double iconOnlyTop = ShelfInkBottom + ((978.0 - ShelfInkBottom) - BasePageIcon.IconSize) / 2.0;
        double iconOnlyLabelBottom = iconOnlyTop + BasePageIcon.IconSize + 4.0
                                   + BasePageIcon.LabelLineHeight;
        Check("S256 ⛔ centring the ICON alone would drive the labels THROUGH the selector",
              iconOnlyLabelBottom > BasePageIcon.SelectorTop,
              "icon-only top " + iconOnlyTop.ToString("F1")
              + ", label bottom " + iconOnlyLabelBottom.ToString("F1")
              + " vs selector " + BasePageIcon.SelectorTop);
        // ⭐ ...whereas the block that IS centred clears it, which is the whole reason for the choice.
        Check("S256 ⭐ ...and the block that IS centred clears it",
              BasePageIcon.LabelTop + BasePageIcon.LabelLineHeight <= BasePageIcon.SelectorTop,
              BasePageIcon.LabelTop + BasePageIcon.LabelLineHeight + " vs " + BasePageIcon.SelectorTop);
        // ⭐⭐ AND THAT NUMBER IS ALSO §5's BORDER, ARRIVED AT FROM THE OTHER DIRECTION: the outer
        // border's inner face is 979 less half its 3px stroke. Two independent stacks, one answer.
        Near("§7 ...which IS the outer border's inner face (§5: 979 - 1.5)",
             BasePageIcon.StackBottom,
             BasePageNoIcon.OuterBottom - BasePageNoIcon.OuterStroke * 0.5f, 0.002);
        // ⛔ AND IT IS DERIVED, NOT TYPED. Move the shelf and the whole stack must move with it -
        // asserted by the DIFFERENCES rather than the absolutes, which a literal stack would fail.
        Near("§7 the stack is measured from the shelf, not from the page",
             BasePageIcon.StackBottom - BasePageIcon.Shelf, 977.5 - 893.0, 0.002);
    }

    // ============================================================================================
    //  4. §7's NINE TABS. ⛔ EVERY NUMBER A LITERAL TYPED FROM THE SPEC.
    // ============================================================================================
    static void TheNineTabsAreWhereSection7PutsThem()
    {
        DisplayList dl = Page();
        string[] name = { "All", "Crew", "Comms", "Prop", "Mech", "Power", "Avionics", "GNC", "Thermal" };
        string[] key = { "ic_tab_all", "ic_tab_crew", "ic_tab_comms", "ic_tab_prop", "ic_tab_mech",
                         "ic_tab_power", "ic_tab_avionics", "ic_tab_gnc", "ic_tab_thermal" };
        // §7: pitch 90.3, centres cx = 598.8 + i*90.3   (S256: ~~599.3~~, the 0.5px lean)
        float[] cx = new float[9];
        for (int i = 0; i < 9; i++) cx[i] = 598.8f + i * 90.3f;

        for (int i = 0; i < 9; i++)
        {
            // icons 37 x 37, LEFT = cx - 18.5, top y = 906.7   (S256: ~~898.1~~)
            bool found = false;
            for (int j = 0; j < dl.Count && !found; j++)
            {
                DrawCmd c = dl.At(j);
                if (c.Kind != DrawKind.Image || c.AssetKey != key[i]) continue;
                found = Math.Abs(c.A - (cx[i] - 18.5f)) < 0.01f && Math.Abs(c.B - 906.7f) < 0.01f
                     && Math.Abs(c.C - 37f) < 0.002f && Math.Abs(c.D - 37f) < 0.002f;
                if (!found)
                    Check("§7 " + key[i] + " at " + (cx[i] - 18.5f) + ",906.7 37x37", false,
                          "drawn at " + c.A + "," + c.B + " " + c.C + "x" + c.D);
            }
            Check("§7 tab " + i + " (" + name[i] + ") draws " + key[i] + " at its centre", found, "");

            // labels: 12.3px, centred on cx, top y = 947.7   (S256: ~~939.1~~)
            bool lab = false;
            for (int j = 0; j < dl.Count && !lab; j++)
            {
                DrawCmd c = dl.At(j);
                if (c.Kind != DrawKind.Text || c.Str != name[i]) continue;
                lab = Math.Abs(c.A - cx[i]) < 0.01f && Math.Abs(c.B - 947.7f) < 0.01f
                   && Math.Abs(c.C - 12.3f) < 0.002f && c.Align == TextAlign.Centre;
                if (!lab)
                    Check("§7 label " + name[i] + " at " + cx[i] + ",947.7 12.3px centred", false,
                          "drawn at " + c.A + "," + c.B + " " + c.C + "px " + c.Align);
            }
            Check("§7 tab " + i + "'s label is \"" + name[i] + "\", centred in its own pitch", lab, "");

            // ⭐ the label BOX is cx - 45.15 wide 90.3 - one full pitch - and the text is centred in
            // it, so the anchor IS the tab centre. Asserted as the identity it is, not assumed.
            Near("§7 tab " + i + "'s label box centre is the tab centre",
                 (cx[i] - 45.15f) + 90.3f / 2f, cx[i], 0.01);
        }
        // ...and the shelf CONTAINS the label run it was sized off (§6: ~~554.1..1366.8 + 20 pad~~ →
        // S256: 553.65..1366.35 + 19.65 pad).
        Check("§6 the shelf contains the label run it was sized off (553.65..1366.35)",
              BasePageIcon.ShelfLeft < 553.65f && BasePageIcon.ShelfRight > 1366.35f, "");

        // ⛔⛔ S256 — THIS BLOCK USED TO ASSERT THE LEAN, AND CALLED IT DELIBERATE. It read, verbatim:
        //     ⚠ AND THE PAD IS NOT 20 ON BOTH SIDES - MEASURED, AND IT IS NOT A DEFECT TO "FIX".
        //     §6's "+ 20 pad" is prose: the shelf is 534..1386, which is 20.1 left of the label run and
        //     19.2 right of it. ⭐ The reason is that the SHELF is centred on the WINDOW's centreline
        //     (960.0) while the TAB BLOCK is centred on tab 4's centre (960.5) - a deliberate half-pixel
        //     in the approved design, pinned here so a later session does not align one to the other and
        //     call it a tidy-up.
        //       Near("§6 the left pad is 20.1", 554.1 - BasePageIcon.ShelfLeft, 20.1, 0.01);
        //       Near("§6 the right pad is 19.2", BasePageIcon.ShelfRight - 1366.8, 19.2, 0.01);
        //       Near("§7 the TAB BLOCK is centred half a pixel off it, on tab 4 (960.5)",
        //            BasePageIcon.TabCentre(4), 960.5, 0.01);
        // SUPERSEDED IN PLACE (C1.16), and kept because it is the more useful half of the record:
        // ⭐⭐ THE LEAN WAS REAL, IT WAS PINNED, AND IT WAS PINNED AS *INTENDED*. A check that asserts a
        // defect is indistinguishable from one that asserts a decision — the only thing that separates
        // them is whether the owner has looked. 🟢 He has: *"Make sure to even it out if it isnt that
        // will make my brain hurt if it look uneven left to right"* (2026-09-10). It is not a
        // half-pixel of taste; it is a 1.00 px difference between the two pads.
        // ⛔ THE SHELF DID NOT MOVE. It was always centred on 960.0 and was always right; the STRIP
        // moved onto it.
        Near("§6 the shelf is centred on the WINDOW centreline (960.0)",
             (BasePageIcon.ShelfLeft + BasePageIcon.ShelfRight) / 2.0, 960.0, 0.002);
        // ⛔⛔ ASSERTED AS THE EQUALITY, NOT AS THE LITERAL — "even left to right" is what the owner
        // asked for, and 19.65 is only today's value of it. A future pitch change that kept the strip
        // centred should pass this; one that re-introduced a lean must not.
        double padL = (BasePageIcon.TabCentre(0) - 45.15) - BasePageIcon.ShelfLeft;
        double padR = BasePageIcon.ShelfRight - (BasePageIcon.TabCentre(8) + 45.15);
        Near("S256 ⭐⭐ the tab strip is EVEN left to right — the two shelf pads are equal",
             padL, padR, 0.002);
        Near("S256 ...and both are 19.65 today", padL, 19.65, 0.002);
        // ⭐ ...which is the same statement made from the other side: the middle tab is ON the centreline.
        Near("S256 ⭐ the TAB BLOCK is centred on the window centreline, on tab 4 (960.0)",
             BasePageIcon.TabCentre(4), 960.0, 0.002);
    }

    static void TheSelectorMarksTabZeroAndNothingElse()
    {
        DisplayList dl = Page();
        // §7: y = 967.4, height 4.8, width 81.5, LEFT = cx - 40.75 of the ACTIVE tab (0), #FFFFFF
        Check("§7 the selector sits under tab 0 (All): x 558.05, y 967.4, 81.5 x 4.8",
              FindRect(dl, 598.8f - 40.75f, 967.4f, 81.5f, 4.8f) >= 0, "");
        // ⛔ EXACTLY ONE. A selector under a second tab would mean the active index leaked somewhere.
        int selectors = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Rect) continue;
            if (Math.Abs(c.D - 4.8f) < 0.002f && Math.Abs(c.C - 81.5f) < 0.002f) selectors++;
        }
        Check("§7 there is exactly ONE selector on the page", selectors == 1, "got " + selectors);
        Near("§10.2 the active tab is 0 and does not move", BasePageIcon.ActiveTab, 0.0, 0.0);
        // ⭐ measured on the approved render at design x 558.0 width 83.0 (= 558.55 / 81.5 plus
        // antialiasing) - the drawn value must sit inside that measurement's own tolerance.
        Near("§7 the selector matches the render it was measured from (558.0 +/- 1)",
             598.8 - 40.75, 558.0, 1.0);
    }

    // ============================================================================================
    //  5. ⛔ NO DIMMING. NONE. Measured on the approved render: all nine icons and all nine labels
    //  peak at 255 whether active or not. An inactive opacity, a grey or a tint is invention.
    // ============================================================================================
    static void ThereIsNoDimmingAnywhere()
    {
        DisplayList dl = Page();
        Rgba white = new Rgba(1f, 1f, 1f, 1f);
        int icons = 0, labels = 0, dim = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            bool isTabIcon = c.Kind == DrawKind.Image && c.AssetKey != null
                             && c.AssetKey.StartsWith("ic_tab_", StringComparison.Ordinal);
            bool isTabLabel = c.Kind == DrawKind.Text;
            if (!isTabIcon && !isTabLabel) continue;
            if (isTabIcon) icons++; else labels++;
            if (!SameColour(c.Colour, white)) dim++;
        }
        Check("§7 all nine tab icons are drawn at the identity tint - NO DIMMING", icons == 9, "got " + icons);
        Check("§7 all nine tab labels are drawn full white - NO DIMMING", labels == 9, "got " + labels);
        Check("§7 not one tab element carries an inactive opacity, grey or tint", dim == 0,
              dim + " dimmed");
        // ⛔ And the ACTIVE tab is not brighter either - the selector is the only difference. Checked
        // by comparing tab 0's ink against tab 8's, which a dimming scheme could not survive.
        Rgba first = new Rgba(0f, 0f, 0f, 0f), last = new Rgba(0f, 0f, 0f, 0f);
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Image || c.AssetKey == null) continue;
            if (c.AssetKey == "ic_tab_all") first = c.Colour;
            if (c.AssetKey == "ic_tab_thermal") last = c.Colour;
        }
        Check("§7 the ACTIVE tab's icon is no brighter than the last tab's", SameColour(first, last), "");
    }

    /// <summary>
    /// ⚠ §7's LETTER-SPACING 0.3px IS RECORDED AND NOT APPLIED, AND THIS SAYS SO IN THOSE WORDS.
    ///
    /// ⛔ It is NOT a decorative check dressed up as a passing one. `DisplayList.Text` has no
    /// letter-spacing field and neither renderer can honour one, so there is nothing on the page to
    /// assert. What IS asserted: the value matches §7, and the Text commands carry no spacing of any
    /// kind - so when the primitive gains one, this test is where the omission is already written
    /// down. `BOB-30`.
    /// </summary>
    static void TheLetterSpacingIsRecordedButNotApplied()
    {
        Near("§7 the label letter-spacing is recorded as 0.3", BasePageIcon.LabelLetterSpacing, 0.3, 0.002);
        Near("§7 the label line-height is pinned to 12.3", BasePageIcon.LabelLineHeight, 12.3, 0.002);
        Check("§7 line-height == font size, so the text box cannot render taller than its ink",
              BasePageIcon.LabelLineHeight == BasePageIcon.LabelPx, "");
        // ⛔ The honest half: the primitive has no field for it. If a later task adds one, THIS is the
        // check that must change, and the register line above it says what to change it to.
        string dlsrc = Live(File.ReadAllText(Repo("plugin", "src", "pure", "DisplayList.cs")));
        Check("§7 ...and DisplayList.Text still has no letter-spacing to apply it to (BOB-30)",
              dlsrc.IndexOf("letterSpacing", StringComparison.OrdinalIgnoreCase) < 0,
              "the primitive gained one - apply it and rewrite this check");
    }

    // ============================================================================================
    //  6. §10's IMAGE COUNTS. ⛔ THE SCOPE IS IN THE NAME.
    // ============================================================================================
    static void ImageCount_PageWithoutTheBar_IsNine()
    {
        DisplayList page = Page();
        DisplayList bar = new DisplayList(BaseBar.Commands + 4);
        BaseBar.Draw(bar, BaseFit.For(FW, FH), null, null);
        int full = Count(page, DrawKind.Image), bars = Count(bar, DrawKind.Image);
        Check("§10 Image count, the page WITHOUT the bar (border, window, grounds, tab strip) = 9",
              full - bars == 9, "page " + full + " - bar " + bars);
        // ...and all nine are tab icons, not something else that happens to number nine.
        int tabs = 0;
        for (int i = 0; i < page.Count; i++)
        {
            DrawCmd c = page.At(i);
            if (c.Kind == DrawKind.Image && c.AssetKey != null
                && c.AssetKey.StartsWith("ic_tab_", StringComparison.Ordinal)) tabs++;
        }
        Check("§10 ...and every one of the nine is a tab icon", tabs == 9, "got " + tabs);
    }

    static void ImageCount_CompletePageIncludingTheBar_IsNineteen()
    {
        DisplayList page = Page();
        Check("§10 Image count, the COMPLETE page including §8's ten bar tiles = 19",
              Count(page, DrawKind.Image) == 19, "got " + Count(page, DrawKind.Image));
        // ⭐ and it is 9 + 10, not 19 of anything: the two groups are counted apart.
        int tabs = 0, bar = 0;
        for (int i = 0; i < page.Count; i++)
        {
            DrawCmd c = page.At(i);
            if (c.Kind != DrawKind.Image || c.AssetKey == null) continue;
            if (c.AssetKey.StartsWith("ic_tab_", StringComparison.Ordinal)) tabs++;
            else bar++;
        }
        Check("§10 ...made of 9 tab icons and 10 bar tiles", tabs == 9 && bar == 10,
              tabs + " + " + bar);
    }

    // ============================================================================================
    //  7. WHAT MUST BE IDENTICAL TO THE PROVED PAGE.
    // ============================================================================================
    static void TheBorderAndTheBarAreTheSameAsTheNonIconPage()
    {
        DisplayList icon = Page();
        DisplayList plain = new DisplayList(BasePageNoIcon.Commands + 8);
        BasePageNoIcon.Draw(plain, FW, FH);

        // ⭐⭐ §5: "identical on BOTH pages". Both pages' first 20 commands are the surface, the
        // page ground and the two border regions, and they must be command-for-command equal - which
        // is what calling ONE expression of the border buys, and is the check that would catch a
        // second copy drifting.
        int same = 0;
        for (int i = 0; i < 20 && i < icon.Count && i < plain.Count; i++)
        {
            DrawCmd a = icon.At(i), b = plain.At(i);
            bool eq = a.Kind == b.Kind && Math.Abs(a.A - b.A) < 0.002f && Math.Abs(a.B - b.B) < 0.002f
                   && Math.Abs(a.C - b.C) < 0.002f && Math.Abs(a.D - b.D) < 0.002f
                   && SameColour(a.Colour, b.Colour);
            if (eq) same++;
            else Check("§5 border command " + i + " is identical on both pages", false,
                       a.Kind + " (" + a.A + "," + a.B + ") vs " + b.Kind + " (" + b.A + "," + b.B + ")");
        }
        Check("§5 the surface, the ground and the whole outer border are identical on both pages",
              same == 20, same + " of 20");

        // §11: the bar band and the two rules are the same on this page as on the other.
        Check("§11 the two 1px rules stand at x 820.2 and x 1088.6, top 989, height 54.9",
              FindRect(icon, 820.2f, 989f, 1f, 54.9f) >= 0
              && FindRect(icon, 1088.6f, 989f, 1f, 54.9f) >= 0, "");
        float top = 1e9f, bottom = -1e9f;
        for (int i = 0; i < icon.Count; i++)
        {
            DrawCmd c = icon.At(i);
            if (c.Kind != DrawKind.Image || c.AssetKey == null) continue;
            if (c.AssetKey.StartsWith("ic_tab_", StringComparison.Ordinal)) continue;
            if (c.B < top) top = c.B;
            if (c.B + c.D > bottom) bottom = c.B + c.D;
        }
        Near("§11 bar band TOP = 997.4", top, 997.4, 0.002);
        Near("§11 bar band BOTTOM = 1054", bottom, 1054.0, 0.002);

        // §11: the 16px inset is the same claim on both pages.
        float bo = BasePageNoIcon.OuterStroke * 0.5f, wo = BasePageNoIcon.WindowStroke * 0.5f;
        Near("§11 inset outer->window, LEFT",
             (BasePageNoIcon.WindowLeft - wo) - (BasePageNoIcon.OuterLeft + bo), 16.0, 0.002);
        Near("§11 inset outer->window, RIGHT",
             (BasePageNoIcon.OuterRight - bo) - (BasePageNoIcon.WindowRight + wo), 16.0, 0.002);
        Near("§11 inset outer->window, TOP",
             (BasePageNoIcon.WindowTop - wo) - (BasePageNoIcon.OuterTop + bo), 16.0, 0.002);
        Near("§11 inset outer->window, BOTTOM",
             (BasePageNoIcon.OuterBottom - bo) - (BasePageNoIcon.WindowBottom + wo), 16.0, 0.002);

        // ⛔ §11: no bar_cap, no curve on the bar, no bar border - checked on THIS page too, because
        // "it was fine on the other one" is not evidence about this one.
        int caps = 0, oldTiles = 0, barArcs = 0;
        for (int i = 0; i < icon.Count; i++)
        {
            DrawCmd c = icon.At(i);
            if (c.AssetKey != null)
            {
                if (c.AssetKey.IndexOf("bar_cap", StringComparison.Ordinal) >= 0) caps++;
                if (c.AssetKey.StartsWith("bar_", StringComparison.Ordinal)) oldTiles++;
            }
            if (c.Kind == DrawKind.ArcBand && c.B >= BaseBar.RuleTop) barArcs++;
        }
        Check("§11 count of bar_cap_* on the ICON page = 0", caps == 0, "got " + caps);
        Check("§8.2 none of the old opaque bar_* tiles is drawn", oldTiles == 0, "got " + oldTiles);
        Check("§8.1 no curve of any kind on the bar", barArcs == 0, "got " + barArcs);
    }

    static void TheColoursAreSection3s()
    {
        DisplayList dl = Page();
        Rgba ground = Rgba.Hex("1A1F35"), margin = Rgba.Hex("070810");

        // ⭐ §2.1, OWNER 2026-09-09: the WHOLE device surface is #070810 before the frame goes on it.
        Check("§2.1 the letterbox surface is #070810, not the page ground",
              SameColour(dl.At(0).Colour, margin), "");
        // ⚠ AND IT BLEEDS A PIXEL PAST EVERY EDGE. Drawn exactly 0..w the render left row 0 at
        // rgb(5,7,36), half this colour and half the renderer's own clear, because GDI+ samples pixel
        // CENTRES - and the glass clears to something else again. ⛔ One pixel of bleed removes a
        // row that would otherwise differ between the two renderers for no reason anyone chose.
        Check("§2.1 ...and it covers the whole surface, bleeding a pixel past every edge",
              dl.At(0).Kind == DrawKind.Rect && dl.At(0).A == -1f && dl.At(0).B == -1f
              && dl.At(0).C == FW + 2f && dl.At(0).D == FH + 2f, "");
        Check("§3 the page ground #1A1F35 is a SECOND rect, over the mapped frame only",
              SameColour(dl.At(1).Colour, ground) && dl.At(1).Kind == DrawKind.Rect
              && dl.At(1).C == BaseFit.FrameW && dl.At(1).D == BaseFit.FrameH, "");

        int marginCmds = 0, groundCmds = 0, whiteish = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            Rgba c = dl.At(i).Colour;
            if (SameColour(c, margin)) marginCmds++;
            else if (SameColour(c, ground)) groundCmds++;
            else if (c.R > 0.99f && c.G > 0.99f && c.B > 0.99f) whiteish++;
        }
        Check("§3 the surface and the border shape are filled #070810", marginCmds == 10,
              "got " + marginCmds);
        Check("§3 the page ground and the notched window fill are #1A1F35", groundCmds == 17, "got " + groundCmds);
        Check("§3 nothing on the page is any OTHER colour (no event driven)",
              marginCmds + groundCmds + whiteish == dl.Count,
              dl.Count + " commands, " + (marginCmds + groundCmds + whiteish) + " accounted");

        // ⛔ §3.1: the SUPERSEDED black had a source and was REJECTED. It must not be on this page.
        int oldFound = 0, panelFound = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            if (SameColour(dl.At(i).Colour, Rgba.Hex("14152C"))) oldFound++;
            if (SameColour(dl.At(i).Colour, Rgba.Hex("111B52"))) panelFound++;
        }
        Check("§3.1 the SUPERSEDED #14152C appears nowhere", oldFound == 0, "got " + oldFound);
        Check("§8 the old bar's #111B52 ground appears nowhere", panelFound == 0, "got " + panelFound);
        Check("§3 the window stroke is white at 0.55, not opaque",
              SameColour(BasePageNoIcon.WindowInk, new Rgba(1f, 1f, 1f, 0.55f)), "");
    }

    static void TheTwoShippedSizesGiveIdenticalRatios()
    {
        DisplayList p1 = new DisplayList(BasePageIcon.Commands + 8);
        DisplayList p2 = new DisplayList(BasePageIcon.Commands + 8);
        BasePageIcon.Draw(p1, 2560, 1405);
        BasePageIcon.Draw(p2, 2560, 1419);
        Check("§11 both shipped sizes emit the same command list", p1.Count == p2.Count,
              p1.Count + " vs " + p2.Count);

        BaseFit f1 = BaseFit.For(2560, 1405), f2 = BaseFit.For(2560, 1419);
        int compared = 0;
        for (int i = 1; i < p1.Count && i < p2.Count; i++)     // 0 is the surface, in device px
        {
            DrawCmd c1 = p1.At(i), c2 = p2.At(i);
            if (c1.Kind != c2.Kind) { Check("§11 command " + i + " is the same kind", false, ""); continue; }
            if (c1.Kind != DrawKind.Rect && c1.Kind != DrawKind.Image && c1.Kind != DrawKind.Text) continue;
            double dx1 = (c1.A - f1.OffX) / f1.K, dx2 = (c2.A - f2.OffX) / f2.K;
            double dy1 = (c1.B - f1.OffY) / f1.K, dy2 = (c2.B - f2.OffY) / f2.K;
            if (Math.Abs(dx1 - dx2) > 0.01 || Math.Abs(dy1 - dy2) > 0.01)
                Check("§11 command " + i + " lands on the same DESIGN coordinate at both sizes", false,
                      "(" + dx1.ToString("F2") + "," + dy1.ToString("F2") + ") vs ("
                      + dx2.ToString("F2") + "," + dy2.ToString("F2") + ")");
            else compared++;
        }
        Check("§11 every mapped rect, tile and label agrees between 1405 and 1419", compared > 40,
              compared + " commands compared");
    }

    static void TheEventPopUpIsWiredOnThisPageToo()
    {
        // ⚠ "draws nothing" and "is not connected" produce IDENTICAL display lists, so the ICON page
        // gets its own proof rather than inheriting the other page's.
        DisplayList quiet = Page();
        Check("§9 with no event the ICON page's pop-up draws NOTHING",
              Count(quiet, DrawKind.Text) == 9, "got " + Count(quiet, DrawKind.Text) + " text (9 labels)");
        DisplayList live = Page("ICON PAGE LINE ONE", "LINE TWO");
        Check("§9 driving an event in makes it APPEAR on this page too",
              live.Count - quiet.Count == 16, "delta " + (live.Count - quiet.Count));
        Check("§9 ...and both lines reach the list",
              Count(live, DrawKind.Text) == 11, "got " + Count(live, DrawKind.Text));
        bool found1 = false, found2 = false;
        for (int i = 0; i < live.Count; i++)
        {
            if (live.At(i).Str == "ICON PAGE LINE ONE") found1 = true;
            if (live.At(i).Str == "LINE TWO") found2 = true;
        }
        Check("§9 the driven strings are the ones drawn", found1 && found2, "");
    }

    static void TheOldBarIsNotReferencedInLiveCode()
    {
        string f = Repo("plugin", "src", "pure", "BasePageIcon.cs");
        Check("the new file exists: BasePageIcon.cs", File.Exists(f), f);
        if (!File.Exists(f)) return;
        string live = Live(File.ReadAllText(f));
        Check("BasePageIcon.cs does not name BottomBar in live code",
              live.IndexOf("BottomBar", StringComparison.Ordinal) < 0, "");
        Check("BasePageIcon.cs does not use DragonPalette in live code",
              live.IndexOf("DragonPalette", StringComparison.Ordinal) < 0, "");
        Check("BasePageIcon.cs does not carry the old ground #111B52 in live code",
              live.IndexOf("111B52", StringComparison.Ordinal) < 0, "");
        Check("BasePageIcon.cs does not name a bar_cap asset in live code",
              live.IndexOf("bar_cap", StringComparison.Ordinal) < 0, "");
        Check("BasePageIcon.cs does not carry the old bar's 3427x2112 frame in live code",
              live.IndexOf("3427", StringComparison.Ordinal) < 0
              && live.IndexOf("2112", StringComparison.Ordinal) < 0, "");
        // ⭐ §7's stack is DERIVED. A literal 906.7 / 947.7 / 967.4 in live code would mean the stack
        // stopped following the shelf, which is the one thing §7 asks for by name.
        // ⚠ S256 — ~~898.1 / 939.1~~ re-pointed (C1.16): the block moved down 8.6 px, the ban did not
        // move. ⭐ 967.4 is unchanged and stays banned, which matters MORE now than before: the owner's
        // condition is that the selector holds at 967.4, and the safe way to hold a number is to keep
        // deriving it, not to start typing it.
        foreach (string lit in new[] { "906.7", "947.7", "967.4" })
            Check("§7 the stack is derived, not typed: no literal " + lit + " in live code",
                  live.IndexOf(lit, StringComparison.Ordinal) < 0,
                  "the tab stack no longer moves with the shelf");
    }
}
