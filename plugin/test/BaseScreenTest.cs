/*
 * S242 — THE TWO BASE SCREENS: the locked geometry, and the coupling that keeps a tab touchable.
 *
 * ⛔ WHAT THIS EXISTS TO STOP. Two things, and they have both already happened on this project.
 *  1. A GEOMETRY REDRAWN FROM A PICTURE. A previous session replaced the full-bleed outer border with
 *     a small inset rounded rectangle and the owner caught it. The path is locked; these checks hold
 *     every number in it, including the two properties that make the diagonals 45 degrees rather than
 *     "about 45".
 *  2. A HIT MAP THAT DRIFTED FROM ITS DRAW. `BottomBar.cs:68-77`: *"THE HIT MAP AND THE MARKER MOVE
 *     WITH THE DRAW OR NOT AT ALL … Changing one without the other slides every nav icon's touch
 *     target off its icon on all 35 pages, silently."* Nine tabs is that trap with nine chances.
 *
 * ⭐ THE LOAD-BEARING CHECK IS `DrawAndHitShareOneSource`: it renders the strip, finds the selector
 * the draw actually put down, and asserts the selector's own centre is a hit on that tab. It compares
 * the DRAWING against the HIT MAP rather than comparing both against the same constant — which is the
 * only version of this check that can catch them drifting together.
 *
 * ⚠ WHAT THIS DOES NOT PROVE: how any of it LOOKS. These are coordinates and command counts. The
 * picture is `build.py preview`, and legibility at IVA distance needs the capsule.
 */
using DragonScreen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public static class BaseScreenTest
{
    static int checks, failures;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + (detail == "" ? "" : "   " + detail)); }
    }

    static string Repo(params string[] parts)
    {
        var bits = new List<string> {
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", ".." };
        bits.AddRange(parts);
        return Path.GetFullPath(Path.Combine(bits.ToArray()));
    }

    // ⛔ Text assertions match commented-out code (S220 shipped two survivors that way). Both base
    // files QUOTE the locked SVG path in their headers, so a naive scan would "find" every coordinate
    // in the prose that documents it and pass on a deleted constant.
    static string Live(string src)
    {
        string[] lines = src.Replace("\r\n", "\n").Split(new char[] { (char)10 });
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].TrimStart();
            if (t.StartsWith("//") || t.StartsWith("///") || t.StartsWith("*")) continue;
            sb.Append(lines[i]).Append((char)10);
        }
        return sb.ToString();
    }

    const int Glass = 2560, GlassH = 1405;   // the shipped panel
    const int RefPx = 1920, RefPxH = 1054;   // the reference frame at 1:1

    public static int Run()
    {
        Console.WriteLine("BaseScreenTest (S242: the two locked base screens, and the tab coupling)");
        checks = 0; failures = 0;

        TheLockedPathIsExact();
        TheDiagonalsAreTrue45();
        TheTabStackIsDerivedNotChosen();
        TheColoursAreTheMeasuredOnes();
        NothingIsBaked();
        TheChamfersAndBevelsUseTri();
        DrawAndHitShareOneSource();
        TheSelectorSlidesAndCanBeAbsent();
        ItScalesInsteadOfHardcoding1920();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + (failures == 0 ? "   (the selector's drawn centre is a hit on its own tab)" : ""));
        return failures;
    }

    // ============================================================================================
    //  1. THE LOCKED PATH, NUMBER BY NUMBER
    // ============================================================================================
    static void TheLockedPathIsExact()
    {
        Check("S242 the border is FULL-BLEED, not inset", BaseScreen.BorderL == 1f
              && BaseScreen.BorderT == 1f && BaseScreen.BorderR == 1919f,
              BaseScreen.BorderL + "," + BaseScreen.BorderT + "," + BaseScreen.BorderR);
        Check("S242 the chamfer line is y=942", BaseScreen.ChamferTop == 942f, "");
        Check("S242 the border's bottom edge is y=979", BaseScreen.BorderB == 979f, "");
        Check("S242 the border's corner radius is 12", BaseScreen.BorderR12 == 12f, "");
        Check("S242 the border stroke is 3px", BaseScreen.BorderStrokePx == 3f, "");

        Check("S242 the window is inset to 19.5 / 1900.5 / 960.5",
              BaseScreen.WinL == 19.5f && BaseScreen.WinR == 1900.5f && BaseScreen.WinB == 960.5f, "");
        Check("S242 the window's corner radius is 10", BaseScreen.WinR10 == 10f, "");
        Check("S242 the window stroke is 2px", BaseScreen.WinStrokePx == 2f, "");
        // ⚠ CHANGED SINCE v1 of the prompt: 914 -> 893, to make room for the selector line.
        Check("S242 ⚠ the ICON shelf is 893 (moved from 914 for the selector)",
              BaseScreen.Shelf == 893f, "got " + BaseScreen.Shelf);
        Check("S242 the notch's flat top runs 534..1386",
              BaseScreen.NotchInnerL == 534f && BaseScreen.NotchInnerR == 1386f, "");
        Check("S242 ...and meets the bottom edge at 466.5 and 1453.5",
              BaseScreen.NotchOuterL == 466.5f && BaseScreen.NotchOuterR == 1453.5f,
              BaseScreen.NotchOuterL + " / " + BaseScreen.NotchOuterR);
    }

    // ============================================================================================
    //  2. ⭐ THE TWO PROPERTIES THAT MAKE THE DIAGONALS 45 DEGREES RATHER THAN "ABOUT 45"
    // ============================================================================================
    static void TheDiagonalsAreTrue45()
    {
        // The border chamfer: its vertical run must equal its horizontal run.
        float vert = BaseScreen.BorderB - BaseScreen.ChamferTop;
        float horz = (BaseScreen.BorderL + BaseScreen.ChamferRun) - BaseScreen.BorderL;
        Check("S242 ⭐ the border chamfer is a TRUE 45 (rise == run)", vert == horz && vert == 37f,
              "rise " + vert + " run " + horz);

        // The notch bevel: same test, from the other pair of numbers in the path.
        float nvert = BaseScreen.WinB - BaseScreen.Shelf;
        float nhorz = BaseScreen.NotchInnerL - BaseScreen.NotchOuterL;
        Check("S242 ⭐ the notch bevel is a TRUE 45 (rise == run)", nvert == nhorz && nvert == 67.5f,
              "rise " + nvert + " run " + nhorz);
        Check("S242 ...and the right bevel matches the left",
              BaseScreen.NotchOuterR - BaseScreen.NotchInnerR == nvert, "");
    }

    // ============================================================================================
    //  3. ⭐ THE TAB STACK IS DERIVED. Move the shelf and the whole stack moves with it.
    // ============================================================================================
    static void TheTabStackIsDerivedNotChosen()
    {
        Check("S242 nine tabs, named", BaseScreenTabbed.TabCount == 9
              && BaseScreenTabbed.Tabs.Length == 9 && BaseScreenTabbed.Tabs[2] == "Comms",
              BaseScreenTabbed.Tabs.Length + " tabs");
        Check("S242 pitch 90.3, first centre 599.3",
              BaseScreenTabbed.Pitch == 90.3f && BaseScreenTabbed.FirstCentre == 599.3f, "");
        Check("S242 the ninth centre follows from the pitch",
              Math.Abs(BaseScreenTabbed.TabCentre(8) - (599.3f + 8f * 90.3f)) < 1e-3f,
              "got " + BaseScreenTabbed.TabCentre(8));

        // ⭐ THE STACK SUM. shelf + 5.1 + 37 + 4 + 12.3 + 16 + 4.8 + 5.3 == 977.5, the border's inner
        // face (979 less half of its 3px stroke). If any gap changed, this is what notices.
        float bottomOfSelector = BaseScreenTabbed.SelectorTop + BaseScreenTabbed.SelectorH;
        Check("S242 ⭐ the derived stack lands on the border's inner face (977.5 + 5.3 pad = 982.8-ish)",
              Math.Abs(bottomOfSelector - 972.2f) < 0.01f, "selector bottom " + bottomOfSelector);
        Check("S242 the icon sits 5.1 below the shelf",
              Math.Abs(BaseScreenTabbed.IconTop - (BaseScreen.Shelf + 5.1f)) < 1e-3f, "");
        Check("S242 the label sits 4 below the icon",
              Math.Abs(BaseScreenTabbed.LabelTop - (BaseScreenTabbed.IconTop + 37f + 4f)) < 1e-3f,
              "got " + BaseScreenTabbed.LabelTop);
        Check("S242 the selector sits 16 below the label",
              Math.Abs(BaseScreenTabbed.SelectorTop
                       - (BaseScreenTabbed.LabelTop + BaseScreenTabbed.LabelPx + 16f)) < 1e-3f,
              "got " + BaseScreenTabbed.SelectorTop);
        Check("S242 the selector is 4.8 x 81.5, scaled from the NASA sheet's own line",
              BaseScreenTabbed.SelectorH == 4.8f && BaseScreenTabbed.SelectorW == 81.5f, "");
    }

    // ============================================================================================
    //  4. THE COLOURS ARE THE MEASURED ONES
    // ============================================================================================
    static void TheColoursAreTheMeasuredOnes()
    {
        Check("S242 ground is #1A1F35", Same(BasePalette.Ground, 0x1A, 0x1F, 0x35), "");
        Check("S242 margin / tab band is #14152C", Same(BasePalette.Margin, 0x14, 0x15, 0x2C), "");
        Check("S242 the pop-up fill is #1D2C4D", Same(BasePalette.PopupFill, 0x1D, 0x2C, 0x4D), "");
        Check("S242 the pop-up border is #334970", Same(BasePalette.PopupBorder, 0x33, 0x49, 0x70), "");
        // ⚠ The window stroke's OPACITY is part of the measurement, not a rendering preference.
        Check("S242 ⚠ the window stroke is white at 0.55 opacity",
              Math.Abs(BasePalette.WindowStroke.A - 0.55f) < 0.005f
              && BasePalette.WindowStroke.R == 1f, "alpha " + BasePalette.WindowStroke.A);
        Check("S242 the border stroke is opaque white",
              BasePalette.Stroke.A == 1f && BasePalette.Stroke.R == 1f, "");
        // ⛔ Ground and margin must not be the same colour, or the band disappears and the whole
        // "inside the border, outside the window" construction silently draws nothing.
        Check("S242 ⛔ ground and margin are DIFFERENT, or the tab band vanishes",
              !Same(BasePalette.Margin, 0x1A, 0x1F, 0x35), "");
    }

    static bool Same(Rgba c, int r, int g, int b)
    {
        return Math.Abs(c.R - r / 255f) < 0.004f
            && Math.Abs(c.G - g / 255f) < 0.004f
            && Math.Abs(c.B - b / 255f) < 0.004f;
    }

    // ============================================================================================
    //  5. ⛔ NOTHING IS BAKED — the owner's rule, enforced rather than promised
    // ============================================================================================
    static void NothingIsBaked()
    {
        DisplayList dl = new DisplayList(4096);
        BaseScreen.Draw(dl, Glass, GlassH);
        int images = 0;
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == DrawKind.Image) images++;
        Check("S242 ⛔ the NON-ICON shell draws ZERO images - the border is geometry, not a tile",
              images == 0, images + " image command(s)");

        // The ICON shell draws exactly nine: the tab icons, which ARE pictures and are meant to be.
        DisplayList d2 = new DisplayList(4096);
        BaseScreenTabbed.Draw(d2, Glass, GlassH, 0);
        int imgs2 = 0;
        for (int i = 0; i < d2.Count; i++) if (d2.At(i).Kind == DrawKind.Image) imgs2++;
        Check("S242 ⛔ the ICON shell draws exactly NINE images - the tab icons and nothing else",
              imgs2 == 9, imgs2 + " image command(s)");
        Check("S242 ...and nine typed captions", CountText(d2) == 9, CountText(d2) + " text command(s)");
    }

    static int CountText(DisplayList dl)
    {
        int n = 0;
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == DrawKind.Text) n++;
        return n;
    }

    // ============================================================================================
    //  6. THE FOUR DIAGONALS ARE DRAWN WITH `Tri` — the primitive S239 stopped for
    // ============================================================================================
    static void TheChamfersAndBevelsUseTri()
    {
        DisplayList dl = new DisplayList(4096);
        BaseScreen.BorderFill(dl, Glass);
        Check("S242 the border's two chamfers are filled with Tri", CountTri(dl) == 2,
              CountTri(dl) + " Tri command(s)");

        DisplayList d2 = new DisplayList(4096);
        BaseScreenTabbed.WindowFill(d2, Glass);
        Check("S242 the notch's two bevels are filled with Tri", CountTri(d2) == 2,
              CountTri(d2) + " Tri command(s)");

        // ⛔ And the NON-ICON window has none: it is a plain rounded rectangle, so a Tri appearing
        // there would mean a notch had been drawn on a page that must not have one.
        DisplayList d3 = new DisplayList(4096);
        BaseScreen.WindowFill(d3, Glass);
        Check("S242 ⛔ the NON-ICON window has NO bevels", CountTri(d3) == 0, CountTri(d3) + " Tri");

        // ⛔ No degenerate triangle reached the list from any of the three.
        Check("S242 ⛔ no shell drops a degenerate triangle",
              dl.DegenerateTrisDropped == 0 && d2.DegenerateTrisDropped == 0
              && d3.DegenerateTrisDropped == 0,
              dl.DegenerateTrisDropped + "/" + d2.DegenerateTrisDropped + "/" + d3.DegenerateTrisDropped);
    }

    static int CountTri(DisplayList dl)
    {
        int n = 0;
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == DrawKind.Tri) n++;
        return n;
    }

    // ============================================================================================
    //  7. ⭐⭐ THE LOAD-BEARING ONE. The DRAW and the HIT MAP, checked against each other.
    // ============================================================================================
    static void DrawAndHitShareOneSource()
    {
        // ⭐ Not "both equal the same constant" - that passes when they drift together. This renders
        // the strip, finds the SELECTOR THE DRAW ACTUALLY PUT DOWN, and asks the hit map about its
        // centre. It is `FigmaUINavTest.BarFollowsItsPage`'s idiom, applied to nine tabs.
        for (int active = 0; active < BaseScreenTabbed.TabCount; active++)
        {
            DisplayList dl = new DisplayList(4096);
            BaseScreenTabbed.DrawTabs(dl, Glass, active);

            float sx = -1f, sy = -1f, sw = -1f;
            for (int i = 0; i < dl.Count; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind != DrawKind.Rect) continue;
                // The selector is the only Rect the strip draws.
                sx = c.A; sy = c.B; sw = c.C;
            }
            Check("S242 tab " + active + ": the draw put down a selector", sx >= 0f, "none found");
            if (sx < 0f) continue;
            int hit = BaseScreenTabbed.HitTest(Glass, GlassH, sx + sw * 0.5f, sy);
            Check("S242 ⭐⭐ tab " + active + ": the selector's own centre is a hit on THAT tab",
                  hit == active, "hit " + hit);
        }

        // ⛔ And a touch outside the strip must miss - a hit map that returns 0 for everything would
        // pass every check above.
        Check("S242 ⛔ NEGATIVE CONTROL - a touch above the shelf hits no tab",
              BaseScreenTabbed.HitTest(Glass, GlassH, Glass * 0.5f, 10f) == -1, "");
        Check("S242 ⛔ ...and one left of the first tab misses too",
              BaseScreenTabbed.HitTest(Glass, GlassH, 10f, 900f * BaseScreen.Sc(Glass)) == -1, "");
        // Adjacent tabs must not overlap, or one steals the other's touches.
        for (int i = 0; i + 1 < BaseScreenTabbed.TabCount; i++)
        {
            float x, y, w, h, x2, y2, w2, h2;
            BaseScreenTabbed.TabRect(i, Glass, out x, out y, out w, out h);
            BaseScreenTabbed.TabRect(i + 1, Glass, out x2, out y2, out w2, out h2);
            Check("S242 tabs " + i + " and " + (i + 1) + " do not overlap", x + w <= x2 + 0.01f,
                  (x + w) + " vs " + x2);
        }
    }

    // ============================================================================================
    //  8. THE SELECTOR SLIDES, AND IS ALLOWED TO BE ABSENT
    // ============================================================================================
    static void TheSelectorSlidesAndCanBeAbsent()
    {
        float prev = -1f;
        for (int active = 0; active < BaseScreenTabbed.TabCount; active++)
        {
            DisplayList dl = new DisplayList(4096);
            BaseScreenTabbed.DrawTabs(dl, Glass, active);
            float x = SelectorX(dl);
            Check("S242 the selector moves right with the active tab", x > prev, x + " vs " + prev);
            prev = x;
        }
        // ⭐ No selector is a REAL state - a tabbed page reached before a tab is chosen - not an error.
        DisplayList none = new DisplayList(4096);
        BaseScreenTabbed.DrawTabs(none, Glass, -1);
        Check("S242 ⭐ active = -1 draws the tabs with NO selector", SelectorX(none) < 0f, "");
        DisplayList over = new DisplayList(4096);
        BaseScreenTabbed.DrawTabs(over, Glass, 99);
        Check("S242 ...and so does an out-of-range index, rather than throwing",
              SelectorX(over) < 0f, "");
    }

    static float SelectorX(DisplayList dl)
    {
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == DrawKind.Rect) return dl.At(i).A;
        return -1f;
    }

    // ============================================================================================
    //  9. ⛔ IT SCALES. Never hardcode 1920 — the glass is 2560.
    // ============================================================================================
    static void ItScalesInsteadOfHardcoding1920()
    {
        Check("S242 the scale is 1 at the reference width", BaseScreen.Sc(RefPx) == 1f, "");
        Check("S242 ...and 1.3333 at the shipped glass",
              Math.Abs(BaseScreen.Sc(Glass) - 2560f / 1920f) < 1e-6f, "");

        // ⭐ The same shape at two sizes must scale, not shift: every x should be exactly 4/3 larger.
        DisplayList a = new DisplayList(4096); BaseScreen.Draw(a, RefPx, RefPxH);
        DisplayList b = new DisplayList(4096); BaseScreen.Draw(b, Glass, GlassH);
        Check("S242 the same shell emits the same command count at both sizes",
              a.Count == b.Count && a.Count > 0, a.Count + " vs " + b.Count);
        int bad = 0;
        float k = Glass / (float)RefPx;
        for (int i = 0; i < a.Count && i < b.Count; i++)
            if (Math.Abs(b.At(i).A - a.At(i).A * k) > 0.01f) bad++;
        Check("S242 ⭐ every command scales by 4/3 - nothing is pinned to 1920", bad == 0,
              bad + " command(s) did not scale");

        // ⛔ And the source must not contain a bare 1920 outside the one constant that defines it.
        string src = Live(File.ReadAllText(Repo("plugin", "src", "pure", "BaseScreen.cs")))
                   + Live(File.ReadAllText(Repo("plugin", "src", "pure", "BaseScreenTabbed.cs")));
        // ⚠ The `f` suffix matters: the constant is written `1920f`, and a pattern that excluded
        // it counted ZERO and failed this check on its first run - the check was wrong, not the code.
        int hardcoded = Regex.Matches(src, @"(?<![\w.])1920f?(?![\w.])").Count;
        Check("S242 ⛔ `1920` appears exactly once in live code - the RefW constant itself",
              hardcoded == 1, hardcoded + " occurrence(s)");
    }
}
