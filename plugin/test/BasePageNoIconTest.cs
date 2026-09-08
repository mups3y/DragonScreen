/*
 * BasePageNoIconTest — `SPEC_BASE_SCREENS.md` §11's NON-ICON column, asserted rather than argued.
 *
 * ⭐⭐ THE LOAD-BEARING IDEA: THE SPEC'S OWN PATH STRINGS ARE ONE SOURCE AND THE DRAW CODE IS
 * ANOTHER, AND THIS SUITE MAKES THEM AGREE. `BasePageNoIcon.OuterBorderPath` and `.WindowPath` are
 * §5 and §6 verbatim; nothing in the draw path parses them. Here they ARE parsed, and every extreme,
 * every corner radius and both 45-degree chamfers are checked against the geometry the page actually
 * emitted. ⛔ A suite that derives its expectation from the value under test proves nothing — that is
 * S176's finding, and it is why the expectations here come out of the path text and out of §11.
 *
 * ⚠ WHAT THIS SUITE CANNOT REACH, SAID UP FRONT. `build.py test` compiles `src/pure` + `test` only,
 * so no rasteriser is reachable from here: this proves the DESIGN-SPACE column exactly, and §11's
 * DEVICE-SPACE table is proved separately by `DragonScreenPreview.exe --basecheck`, which renders the
 * real page at 2560x1405 and 2560x1419 and probes actual pixels. Both are gates in `build.py test`.
 *
 * ⭐ THE FIXTURE RENDERS AT 1920x1054 ON PURPOSE. There k = 1 and both offsets are 0, so device
 * coordinates ARE design coordinates and every assertion below is a direct read of what was drawn —
 * no expectation is computed through the same expression it is testing. The fit itself is then
 * checked separately, at both shipped sizes, where k is not 1.
 */
using DragonScreen;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

public static class BasePageNoIconTest
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

    /// <summary>
    /// ⛔ Text assertions match commented-out code. S220 shipped two surviving mutants for exactly
    /// that reason, and this file's own headers NAME the things it forbids ("BottomBar", "#111B52",
    /// "bar_cap") in prose, so a naive scan would find every ban it is checking for and pass on a
    /// file that had reintroduced all of them. Only live lines are searched.
    /// </summary>
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

    // ---- the fixture: the page at k = 1, so design space and device space coincide ---------------
    const int FW = 1920, FH = 1054;
    static DisplayList Page(string ev1, string ev2)
    {
        DisplayList dl = new DisplayList(BasePageNoIcon.Commands + 8);
        BasePageNoIcon.Draw(dl, FW, FH, ev1, ev2);
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
    /// <summary>The first Rect with these exact bounds, or -1. Tolerance is float noise only.</summary>
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
        Console.WriteLine("BasePageNoIconTest (spec §11, the NON-ICON column - design space; pixels by --basecheck)");
        checks = 0; failures = 0;

        ThePathsAndTheCodeAreTwoSourcesThatAgree();
        TheOuterBorderIsDrawnOnTheSpecPath();
        TheContentWindowIsDrawnOnTheSpecPath();
        TheInsetIsSixteenOnAllFourSides();
        TheColoursAreSection3s();
        ImageCount_PageWithoutTheBar_IsZero();
        ImageCount_CompletePageIncludingTheBar_IsTen();
        TheBarHasNoCapsNoCurvesAndNoBorder();
        TheBarBandAndItsTwoRules();
        TheTenTilesAreWhereSection8Puts();
        TheFitIsOneFactorForBothAxes();
        TheTwoShippedSizesGiveIdenticalRatios();
        TheEventPopUpIsWiredNotAbsent();
        TheOldBarIsNotReferencedInLiveCode();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + (failures == 0 ? "   (device space is proved separately by --basecheck)" : ""));
        return failures;
    }

    // ============================================================================================
    //  1. ⭐⭐ THE SPEC'S PATH TEXT, PARSED, AGAINST THE CONSTANTS THE DRAW CODE USES.
    // ============================================================================================
    struct SvgPath { public List<float[]> Pts; public List<float> Radii; }

    /// <summary>M/L take 2 numbers; A takes rx ry rot large-arc sweep x y; Z ends. Nothing else.</summary>
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
            if (op == "M" || op == "L")
            {
                p.Pts.Add(new float[] { F(tok[i]), F(tok[i + 1]) }); i += 2;
            }
            else if (op == "A")
            {
                p.Radii.Add(F(tok[i]));                       // rx; ry is asserted equal below
                p.Radii.Add(F(tok[i + 1]));
                p.Pts.Add(new float[] { F(tok[i + 5]), F(tok[i + 6]) }); i += 7;
            }
            else throw new InvalidOperationException("unexpected path token '" + op + "'");
        }
        return p;
    }
    static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }
    static float Min(List<float[]> p, int ax) { float v = p[0][ax]; foreach (var q in p) if (q[ax] < v) v = q[ax]; return v; }
    static float Max(List<float[]> p, int ax) { float v = p[0][ax]; foreach (var q in p) if (q[ax] > v) v = q[ax]; return v; }
    static bool Has(List<float[]> p, float x, float y)
    {
        foreach (var q in p) if (Math.Abs(q[0] - x) < 0.002f && Math.Abs(q[1] - y) < 0.002f) return true;
        return false;
    }

    static void ThePathsAndTheCodeAreTwoSourcesThatAgree()
    {
        SvgPath o = Parse(BasePageNoIcon.OuterBorderPath);
        Check("§5 the border path has 9 points and 2 arcs", o.Pts.Count == 9 && o.Radii.Count == 4,
              o.Pts.Count + " pts, " + (o.Radii.Count / 2) + " arcs");
        Near("§5 path min x == OuterLeft", Min(o.Pts, 0), BasePageNoIcon.OuterLeft, 0.002);
        Near("§5 path max x == OuterRight", Max(o.Pts, 0), BasePageNoIcon.OuterRight, 0.002);
        Near("§5 path min y == OuterTop", Min(o.Pts, 1), BasePageNoIcon.OuterTop, 0.002);
        Near("§5 path max y == OuterBottom", Max(o.Pts, 1), BasePageNoIcon.OuterBottom, 0.002);
        foreach (float r in o.Radii) Near("§5 every arc radius == OuterRadius", r, BasePageNoIcon.OuterRadius, 0.002);
        Check("§5 path carries the chamfer corners the code uses",
              Has(o.Pts, BasePageNoIcon.OuterRight, BasePageNoIcon.ChamferY)
              && Has(o.Pts, BasePageNoIcon.ChamferXRight, BasePageNoIcon.OuterBottom)
              && Has(o.Pts, BasePageNoIcon.ChamferXLeft, BasePageNoIcon.OuterBottom)
              && Has(o.Pts, BasePageNoIcon.OuterLeft, BasePageNoIcon.ChamferY), "");
        // ⭐ §11: "bevel rise vs run — equal (true 45 deg)". The border's chamfers are the same claim.
        Near("§5 the LEFT chamfer is a true 45 deg (rise == run)",
             BasePageNoIcon.ChamferXLeft - BasePageNoIcon.OuterLeft,
             BasePageNoIcon.OuterBottom - BasePageNoIcon.ChamferY, 0.002);
        Near("§5 the RIGHT chamfer is a true 45 deg (rise == run)",
             BasePageNoIcon.OuterRight - BasePageNoIcon.ChamferXRight,
             BasePageNoIcon.OuterBottom - BasePageNoIcon.ChamferY, 0.002);

        SvgPath win = Parse(BasePageNoIcon.WindowPath);
        Check("§6 the NON-ICON window path has 9 points and 4 arcs",
              win.Pts.Count == 9 && win.Radii.Count == 8, win.Pts.Count + " pts");
        Near("§6 path min x == WindowLeft", Min(win.Pts, 0), BasePageNoIcon.WindowLeft, 0.002);
        Near("§6 path max x == WindowRight", Max(win.Pts, 0), BasePageNoIcon.WindowRight, 0.002);
        Near("§6 path min y == WindowTop", Min(win.Pts, 1), BasePageNoIcon.WindowTop, 0.002);
        Near("§6 path max y == WindowBottom", Max(win.Pts, 1), BasePageNoIcon.WindowBottom, 0.002);
        foreach (float r in win.Radii) Near("§6 every arc radius == WindowRadius", r, BasePageNoIcon.WindowRadius, 0.002);
        // ⛔ The NON-ICON window has NO notch. Its shelf points would be at y 893 - none may appear.
        bool shelf = false;
        foreach (var q in win.Pts) if (Math.Abs(q[1] - 893f) < 1f) shelf = true;
        Check("§6 the NON-ICON window has NO notch (no shelf point at y 893)", !shelf, "");
    }

    // ============================================================================================
    //  2. THE BORDER, AS DRAWN. 3px white centred on the path, #070810 inside it.
    // ============================================================================================
    static void TheOuterBorderIsDrawnOnTheSpecPath()
    {
        DisplayList dl = Page();
        // The stroke is drawn as the path outset by half the stroke, then inset by half the stroke.
        const float o = 1.5f;                       // OuterStroke / 2
        const float e = 1.5f * 0.41421356f;         // the 45-degree edge's own offset

        // The white outer region's body: x from -0.5 to 1920.5, y from 13 down to 942 + e.
        Check("§5 the WHITE stroke's outer edge is the path outset by 1.5",
              FindRect(dl, BasePageNoIcon.OuterLeft - o, 13f,
                       (BasePageNoIcon.OuterRight + o) - (BasePageNoIcon.OuterLeft - o),
                       (BasePageNoIcon.ChamferY + e) - 13f) >= 0, "");
        // The #070810 region's body: x from 2.5 to 1917.5, y from 13 down to 942 - e.
        Check("§5 the #070810 fill is the same path INSET by 1.5",
              FindRect(dl, BasePageNoIcon.OuterLeft + o, 13f,
                       (BasePageNoIcon.OuterRight - o) - (BasePageNoIcon.OuterLeft + o),
                       (BasePageNoIcon.ChamferY - e) - 13f) >= 0, "");

        // ⭐ THE CHAMFERS ARE `Tri` (§4), and both regions draw two - four in all, all true 45 deg.
        int tris = Count(dl, DrawKind.Tri);
        Check("§4 the four 45-degree chamfer fills are Tri commands", tris == 4, "got " + tris);
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Tri) continue;
            // packed A,B = p0 · C,D = p1 · StartDeg,EndDeg = p2; the right-angle is at p1.
            float run = Math.Abs(c.C - c.A), rise = Math.Abs(c.EndDeg - c.D);
            Near("§5 chamfer tri rise == run (true 45 deg)", rise, run, 0.01);
        }

        // The top corners: two arcs per region, centred where the path's own arcs are.
        int corners = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.ArcBand) continue;
            if (Math.Abs(c.B - 13f) > 0.002f) continue;
            if (Math.Abs(c.A - 13f) < 0.002f || Math.Abs(c.A - 1907f) < 0.002f) corners++;
        }
        Check("§5 the two top corners are ArcBand quarter-discs, in both regions",
              corners == 4, "got " + corners);
        // ⛔ THE BOTTOM CORNERS ARE CHAMFERS, NOT RADII. A rounded bottom corner here would be the
        // old bar's chrome creeping back in, so it is checked for rather than assumed absent.
        int lowArcs = 0;
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.ArcBand && dl.At(i).B > 970f) lowArcs++;
        Check("§5 there is NO rounded bottom corner anywhere on the page", lowArcs == 0, "got " + lowArcs);
    }

    static void TheContentWindowIsDrawnOnTheSpecPath()
    {
        DisplayList dl = Page();
        float l = BasePageNoIcon.WindowLeft, r = BasePageNoIcon.WindowRight;
        float t = BasePageNoIcon.WindowTop, b = BasePageNoIcon.WindowBottom;
        float rad = BasePageNoIcon.WindowRadius, o = BasePageNoIcon.WindowStroke * 0.5f;

        // the fill, to the path: the full-width band of the §0(a) rounded rectangle
        Check("§6 the window fill runs to the path (full-width band inset by r)",
              FindRect(dl, l, t + rad, r - l, (b - t) - 2f * rad) >= 0, "");
        // the ring, centred on the path
        Check("§6 the 2px ring's TOP band sits astride the path",
              FindRect(dl, l + rad, t - o, (r - rad) - (l + rad), BasePageNoIcon.WindowStroke) >= 0, "");
        Check("§6 ...and its BOTTOM band",
              FindRect(dl, l + rad, b - o, (r - rad) - (l + rad), BasePageNoIcon.WindowStroke) >= 0, "");
        Check("§6 ...and its LEFT band",
              FindRect(dl, l - o, t + rad, BasePageNoIcon.WindowStroke, (b - rad) - (t + rad)) >= 0, "");
        Check("§6 ...and its RIGHT band",
              FindRect(dl, r - o, t + rad, BasePageNoIcon.WindowStroke, (b - rad) - (t + rad)) >= 0, "");

        // The four corner quarter-ANNULI: inner r-1, outer r+1, so the ring is 2px round the corner.
        int annuli = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.ArcBand) continue;
            if (Math.Abs(c.C - (rad - o)) < 0.002f && Math.Abs(c.D - (rad + o)) < 0.002f) annuli++;
        }
        Check("§6 the ring turns four corners as quarter-annuli of exactly 2px", annuli == 4, "got " + annuli);
    }

    static void TheInsetIsSixteenOnAllFourSides()
    {
        // §11: "inset outer→window, all four sides — 16.0". Measured face to face: the border
        // stroke's INNER face against the window stroke's OUTER face.
        float bo = BasePageNoIcon.OuterStroke * 0.5f, wo = BasePageNoIcon.WindowStroke * 0.5f;
        Near("§11 inset outer->window, LEFT",
             (BasePageNoIcon.WindowLeft - wo) - (BasePageNoIcon.OuterLeft + bo), 16.0, 0.002);
        Near("§11 inset outer->window, RIGHT",
             (BasePageNoIcon.OuterRight - bo) - (BasePageNoIcon.WindowRight + wo), 16.0, 0.002);
        Near("§11 inset outer->window, TOP",
             (BasePageNoIcon.WindowTop - wo) - (BasePageNoIcon.OuterTop + bo), 16.0, 0.002);
        Near("§11 inset outer->window, BOTTOM",
             (BasePageNoIcon.OuterBottom - bo) - (BasePageNoIcon.WindowBottom + wo), 16.0, 0.002);
    }

    // ============================================================================================
    //  3. §3's COLOURS, AS DRAWN. ⛔ The margin black is the OWNER'S CHOSEN value, not the measured
    //  one; `#14152C` had a source and was rejected. A revert to it must fail here.
    // ============================================================================================
    static void TheColoursAreSection3s()
    {
        DisplayList dl = Page();
        Rgba ground = Rgba.Hex("1A1F35"), margin = Rgba.Hex("070810");

        // ⭐ §2.1, OWNER RULING 2026-09-09: the whole device surface is `#070810`, NOT the page
        // ground. ~~The surface was `#1A1F35` when this suite was written ([[S245]], `BOB-27`).~~
        // ⛔ SUPERSEDED IN PLACE: the approved design has no letterbox band at all, and `#070810`
        // reads as unlit screen where the page ground read as a lighter stripe framing the border.
        Check("§2.1 the letterbox surface is #070810, not the page ground",
              SameColour(dl.At(0).Colour, margin), "");
        // ⚠ AND IT BLEEDS A PIXEL PAST EVERY EDGE. Drawn exactly 0..w the render left row 0 at
        // rgb(5,7,36), half this colour and half the renderer's own clear, because GDI+ samples pixel
        // CENTRES - and the glass clears to something else again. ⛔ One pixel of bleed removes a
        // row that would otherwise differ between the two renderers for no reason anyone chose.
        Check("§2.1 ...and it covers the whole surface, bleeding a pixel past every edge",
              dl.At(0).Kind == DrawKind.Rect && dl.At(0).A == -1f && dl.At(0).B == -1f
              && dl.At(0).C == FW + 2f && dl.At(0).D == FH + 2f, "");
        // ⛔ AND §3's PAGE GROUND IS A SECOND, DIFFERENT RECT, over the MAPPED FRAME only. Collapsing
        // the two was a real defect for the length of one render: the bar band came out `#070810` and
        // the two 1px rules dimmed measurably because they composited over the wrong colour.
        Check("§3 the page ground #1A1F35 is drawn over the mapped frame, on top of the surface",
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
        Check("§3 the surface and the border shape are filled #070810", marginCmds == 10, "got " + marginCmds + " commands");
        Check("§3 the page ground and the window fill are #1A1F35", groundCmds == 8, "got " + groundCmds);
        Check("§3 nothing on the page is any OTHER colour (no event driven)",
              marginCmds + groundCmds + whiteish == dl.Count,
              dl.Count + " commands, " + (marginCmds + groundCmds + whiteish) + " accounted");

        // ⛔ THE SUPERSEDED BLACK MUST NOT BE ON THE PAGE. `#14152C` was measured, and rejected.
        Rgba old = Rgba.Hex("14152C");
        int oldFound = 0;
        for (int i = 0; i < dl.Count; i++) if (SameColour(dl.At(i).Colour, old)) oldFound++;
        Check("§3.1 the SUPERSEDED #14152C appears nowhere", oldFound == 0, "got " + oldFound);
        // ⛔ And neither does the OLD BAR's ground, which is what a copied bar would bring with it.
        Rgba panel = Rgba.Hex("111B52");
        int panelFound = 0;
        for (int i = 0; i < dl.Count; i++) if (SameColour(dl.At(i).Colour, panel)) panelFound++;
        Check("§8 the old bar's #111B52 ground appears nowhere", panelFound == 0, "got " + panelFound);

        Check("§3 the window stroke is white at 0.55, not opaque",
              SameColour(BasePageNoIcon.WindowInk, new Rgba(1f, 1f, 1f, 0.55f)), "");
    }

    // ============================================================================================
    //  4. §10's IMAGE COUNTS. ⛔ THE SCOPE IS IN THE NAME. An unscoped count is the exact shape of
    //  mistake the spec's two-row table exists to prevent: the earlier bare "0 and 9" was true only
    //  of the page WITHOUT its bar.
    // ============================================================================================
    static void ImageCount_PageWithoutTheBar_IsZero()
    {
        DisplayList page = Page();
        DisplayList bar = new DisplayList(BaseBar.Commands + 4);
        BaseBar.Draw(bar, BaseFit.For(FW, FH), null, null);
        int full = Count(page, DrawKind.Image), bars = Count(bar, DrawKind.Image);
        Check("§10 Image count, the page WITHOUT the bar (border, window, grounds) = 0",
              full - bars == 0, "page " + full + " - bar " + bars);
    }

    static void ImageCount_CompletePageIncludingTheBar_IsTen()
    {
        DisplayList page = Page();
        Check("§10 Image count, the COMPLETE page including §8's ten bar tiles = 10",
              Count(page, DrawKind.Image) == 10, "got " + Count(page, DrawKind.Image));
        // ...and they are the ten the spec names, in the order it gives.
        string[] want = { "b_nav0", "b_nav1", "b_nav2", "b_nav3", "b_nav4",
                          "b_state", "b_point", "r_spx", "r_iss", "r_count" };
        int k = 0;
        for (int i = 0; i < page.Count; i++)
        {
            if (page.At(i).Kind != DrawKind.Image) continue;
            Check("§8.1 tile " + k + " is " + want[k],
                  k < want.Length && page.At(i).AssetKey == want[k], page.At(i).AssetKey);
            k++;
        }
        Check("§8.1 b_nav0 draws FIRST, carrying the baked selection marker",
              k == 10 && want[0] == "b_nav0", "");
    }

    // ============================================================================================
    //  5. ⛔ WHAT THE OLD BAR HAS AND THIS ONE DOES NOT. Checked for, never assumed absent — the
    //  curves came back once already because a prompt pointed a build at the old class.
    // ============================================================================================
    static void TheBarHasNoCapsNoCurvesAndNoBorder()
    {
        DisplayList dl = Page();
        int caps = 0, oldTiles = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            string key = dl.At(i).AssetKey;
            if (key == null) continue;
            if (key.IndexOf("bar_cap", StringComparison.Ordinal) >= 0) caps++;
            if (key.StartsWith("bar_", StringComparison.Ordinal)) oldTiles++;
        }
        Check("§11 count of bar_cap_* (the curved frame corners) = 0", caps == 0, "got " + caps);
        Check("§8.2 none of the old opaque bar_* tiles is drawn", oldTiles == 0, "got " + oldTiles);

        // ⛔ NO CURVE ANYWHERE ON THE BAR. The page has four rounded corners and they are all above
        // y 979; anything curved at or below the bar's rules would be chrome this bar does not have.
        int barArcs = 0;
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.ArcBand && dl.At(i).B >= BaseBar.RuleTop) barArcs++;
        Check("§8.1 no curve of any kind on the bar", barArcs == 0, "got " + barArcs);

        // ⛔ NO BORDER: the bar's whole command list is ten tiles and two rules. No side rule, no
        // bottom rule, no top rule cap-to-cap, and no ground of its own.
        DisplayList bar = new DisplayList(BaseBar.Commands + 4);
        BaseBar.Draw(bar, BaseFit.For(FW, FH), null, null);
        Check("§8.1 the bar is EXACTLY ten tiles and two rules - nothing else",
              bar.Count == 12 && Count(bar, DrawKind.Image) == 10 && Count(bar, DrawKind.Rect) == 2,
              bar.Count + " commands");
        Check("§8.1 the bar draws NO ground of its own",
              Count(bar, DrawKind.Rect) == 2, "got " + Count(bar, DrawKind.Rect) + " rects");
    }

    static void TheBarBandAndItsTwoRules()
    {
        DisplayList dl = Page();
        float top = 1e9f, bottom = -1e9f;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Image) continue;
            if (c.B < top) top = c.B;
            if (c.B + c.D > bottom) bottom = c.B + c.D;
        }
        Near("§11 bar band TOP = 997.4", top, 997.4, 0.002);
        Near("§11 bar band BOTTOM = 1054 (the frame's own bottom edge)", bottom, 1054.0, 0.002);

        // ⛔ EVERY NUMBER BELOW IS A LITERAL TYPED FROM §8.1's TABLE, never read off the page. A suite
        // that spells its expectation `BaseBar.RuleTop` cannot fail when `BaseBar.RuleTop` is the thing
        // that moved - that is S176's finding, and S220's two survivors.
        Check("§8.1 the two 1px rules stand at x 820.2 and x 1088.6, top 989, height 54.9",
              FindRect(dl, 820.2f, 989f, 1f, 54.9f) >= 0
              && FindRect(dl, 1088.6f, 989f, 1f, 54.9f) >= 0, "");
        // ⭐ §9's box spans exactly the rows the rules occupy: 989..1044.
        Near("§9 the pop-up spans the rules' own rows (989..1044)",
             BaseBar.PopTop + BaseBar.PopHeight, 1044.0, 0.002);
    }

    /// <summary>
    /// ⭐⭐ §8.1's TEN TILES, EVERY ROW OF THE TABLE, AS LITERALS.
    ///
    /// ⛔ WITHOUT THIS THE POSITIONS ARE PINNED BY NOTHING. §11's acceptance table does not probe a
    /// tile's left edge and neither does the device check, so a tile moved by a pixel would have been
    /// a MUTANT THAT SURVIVED - and this suite would have been decorative about the one part of the
    /// bar that is pure data. The numbers here are typed from the spec's own table, not read from
    /// `BaseBar`'s arrays, so the two are independent expressions of one source.
    /// </summary>
    static void TheTenTilesAreWhereSection8Puts()
    {
        DisplayList dl = Page();
        string[] key = { "b_nav0", "b_nav1", "b_nav2", "b_nav3", "b_nav4",
                         "b_state", "b_point", "r_spx", "r_iss", "r_count" };
        float[] left = { 13.4f, 102.0f, 176.5f, 243.2f, 316.0f, 615.2f, 1103.1f, 1429.2f, 1669.0f, 1807.5f };
        float[] top = { 997.4f, 997.4f, 997.4f, 997.4f, 997.4f, 997.4f, 997.4f, 998.5f, 998.5f, 998.5f };
        float[] wide = { 69.5f, 35.9f, 28.0f, 40.3f, 38.1f, 192.7f, 85.2f, 191.2f, 94.2f, 99.1f };
        float[] high = { 56.6f, 56.6f, 56.6f, 56.6f, 56.6f, 56.6f, 56.6f, 35.9f, 35.9f, 35.9f };

        for (int t = 0; t < key.Length; t++)
        {
            bool found = false;
            for (int i = 0; i < dl.Count && !found; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind != DrawKind.Image || c.AssetKey != key[t]) continue;
                found = Math.Abs(c.A - left[t]) < 0.002f && Math.Abs(c.B - top[t]) < 0.002f
                     && Math.Abs(c.C - wide[t]) < 0.002f && Math.Abs(c.D - high[t]) < 0.002f;
                if (!found)
                    Check("§8.1 " + key[t] + " at " + left[t] + "," + top[t] + " "
                          + wide[t] + "x" + high[t], false,
                          "drawn at " + c.A + "," + c.B + " " + c.C + "x" + c.D);
            }
            Check("§8.1 " + key[t] + " is drawn at its table position", found, "");

            // ⭐ AND IT IS NOT SQUASHED. The shipped PNG's own aspect must match the box §8.1 draws it
            // into: a transposed width and height would place the tile correctly and render the
            // artwork stretched, which no position check can see. The size is read out of the PNG's
            // IHDR (bytes 16..23, big-endian) because this build has no imaging library at all.
            string png = Repo("plugin", "GameData", "DragonScreen", "art", "cover", key[t] + ".png");
            Check("§8.2 " + key[t] + ".png is harvested into the repo", File.Exists(png), png);
            if (!File.Exists(png)) continue;
            byte[] head = new byte[24];
            using (FileStream fs = File.OpenRead(png)) fs.Read(head, 0, 24);
            int pw = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            int ph = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
            double drawn = wide[t] / (double)high[t], native = pw / (double)ph;
            Check("§8.2 " + key[t] + " is drawn at its own aspect (" + pw + "x" + ph + ")",
                  ph > 0 && Math.Abs(drawn - native) / native < 0.005,
                  "box " + drawn.ToString("F4") + " vs file " + native.ToString("F4"));
        }

        // ⭐ THE BAKED SELECTION MARKER MUST FIT INSIDE THE TILE THAT CARRIES IT (§8.1: left 17.3,
        // width 61.7, top 1043.9, height 10.1). Recorded as a CONTAINMENT, not as a restatement, so
        // the future task that has to take the marker out of the tile and draw it as a `Rect` can see
        // that these numbers still describe the tile actually shipped.
        Check("§8.1 the marker's box lies inside b_nav0's box",
              17.3f >= left[0] && 17.3f + 61.7f <= left[0] + wide[0]
              && 1043.9f >= top[0] && 1043.9f + 10.1f <= top[0] + high[0] + 0.002f,
              "marker 17.3+61.7 / 1043.9+10.1 vs tile " + left[0] + "+" + wide[0]
              + " / " + top[0] + "+" + high[0]);
        Near("§8.1 ...and is flush to the frame's bottom edge (1043.9 + 10.1 = 1054)",
             1043.9 + 10.1, 1054.0, 0.002);
    }

    // ============================================================================================
    //  6. §2 — THE FIT. ⛔ ONE FACTOR, BOTH AXES. Never a per-axis scale.
    // ============================================================================================
    static void TheFitIsOneFactorForBothAxes()
    {
        // ⭐ SCREEN 1/3, 2560x1405, is HEIGHT-limited: 1405/1054 = 1.33302 is smaller than
        // 2560/1920 = 1.33333. ⛔ So a `min` replaced by width-only is caught HERE and nowhere else -
        // at 2560x1419 the width IS the limit and the two expressions agree.
        BaseFit a = BaseFit.For(2560, 1405);
        Near("§2 k at 2560x1405 is the HEIGHT ratio (the min)", a.K, 1405.0 / 1054.0, 1e-6);
        Check("§2 ...which is NOT the width ratio", Math.Abs(a.K - 2560.0 / 1920.0) > 1e-5,
              "k " + a.K.ToString("F6"));
        Near("§2 the frame is centred: spare width split equally", a.OffX, (2560 - 1920 * a.K) / 2f, 1e-4);
        Near("§2 ...and 1405 leaves no spare height at all", a.OffY, 0.0, 1e-4);

        BaseFit b = BaseFit.For(2560, 1419);
        Near("§2 k at 2560x1419 is the WIDTH ratio (the min)", b.K, 2560.0 / 1920.0, 1e-6);
        Near("§2 the ~7px band top and bottom is the spare height, split equally",
             b.OffY, (1419 - 1054 * (2560.0 / 1920.0)) / 2.0, 1e-3);
        Check("§2 the band is about 7px, as the spec says", b.OffY > 6.0f && b.OffY < 7.5f,
              b.OffY.ToString("F2"));

        // ⛔ ONE FACTOR MEANS THE SAME FACTOR: x and y must scale identically at both sizes.
        foreach (BaseFit f in new[] { a, b })
        {
            Near("§2 a design length scales the same on x as on y",
                 f.X(100f) - f.X(0f), f.Y(100f) - f.Y(0f), 1e-4);
            Near("§2 ...and S() is that same factor with no origin", f.S(100f), f.K * 100f, 1e-4);
        }
    }

    static void TheTwoShippedSizesGiveIdenticalRatios()
    {
        // ⭐ §11: "At 2560x1419 every ratio must be identical and the frame centred." Both sizes are
        // real (§1, verified three ways). ⛔ Never force one to the other.
        DisplayList p1 = new DisplayList(BasePageNoIcon.Commands + 8);
        DisplayList p2 = new DisplayList(BasePageNoIcon.Commands + 8);
        BasePageNoIcon.Draw(p1, 2560, 1405);
        BasePageNoIcon.Draw(p2, 2560, 1419);
        Check("§11 both shipped sizes emit the same command list", p1.Count == p2.Count,
              p1.Count + " vs " + p2.Count);

        BaseFit f1 = BaseFit.For(2560, 1405), f2 = BaseFit.For(2560, 1419);
        int compared = 0;
        for (int i = 1; i < p1.Count && i < p2.Count; i++)     // 0 is the surface ground, in device px
        {
            DrawCmd c1 = p1.At(i), c2 = p2.At(i);
            if (c1.Kind != c2.Kind) { Check("§11 command " + i + " is the same kind", false, ""); continue; }
            if (c1.Kind != DrawKind.Rect && c1.Kind != DrawKind.Image) continue;
            // Back out of device space into design space: the two must land on the same design number.
            double dx1 = (c1.A - f1.OffX) / f1.K, dx2 = (c2.A - f2.OffX) / f2.K;
            double dy1 = (c1.B - f1.OffY) / f1.K, dy2 = (c2.B - f2.OffY) / f2.K;
            double dw1 = c1.C / f1.K, dw2 = c2.C / f2.K;
            if (Math.Abs(dx1 - dx2) > 0.01 || Math.Abs(dy1 - dy2) > 0.01 || Math.Abs(dw1 - dw2) > 0.01)
                Check("§11 command " + i + " lands on the same DESIGN coordinate at both sizes", false,
                      "(" + dx1.ToString("F2") + "," + dy1.ToString("F2") + ") vs ("
                      + dx2.ToString("F2") + "," + dy2.ToString("F2") + ")");
            else compared++;
        }
        Check("§11 every mapped rect and tile agrees between 1405 and 1419", compared > 20,
              compared + " commands compared");
    }

    // ============================================================================================
    //  7. §9 — THE POP-UP IS WIRED. ⚠ "draws nothing" and "is not connected" produce IDENTICAL
    //  display lists, so the ONLY proof is to drive a value in and watch it appear.
    // ============================================================================================
    static void TheEventPopUpIsWiredNotAbsent()
    {
        DisplayList quiet = Page();
        Check("§9 with no event the pop-up draws NOTHING - and that is the correct state",
              Count(quiet, DrawKind.Text) == 0, Count(quiet, DrawKind.Text) + " text commands");
        int quietCount = quiet.Count;

        // ⭐ DRIVE A VALUE IN. ⛔ Not the reference's sample text - that string sizes the box and is
        // never shipped; this is a fixture string, chosen so it cannot be mistaken for content.
        DisplayList live = Page("BOB TEST LINE ONE", "LINE TWO");
        Check("§9 driving an event in makes the pop-up APPEAR", live.Count > quietCount,
              quietCount + " -> " + live.Count);
        Check("§9 ...as the rounded box plus its 1px border plus two lines",
              live.Count - quietCount == 16, "delta " + (live.Count - quietCount));

        int texts = 0; float y1 = -1f, y2 = -1f, cx = -1f;
        for (int i = 0; i < live.Count; i++)
        {
            DrawCmd c = live.At(i);
            if (c.Kind != DrawKind.Text) continue;
            texts++;
            if (c.Str == "BOB TEST LINE ONE") { y1 = c.B; cx = c.A; }
            if (c.Str == "LINE TWO") y2 = c.B;
            Check("§9 the pop-up text is centred on its box", c.Align == TextAlign.Centre, "");
            Near("§9 the pop-up text is 18px", c.C, BaseBar.PopTextPx, 0.002);
        }
        Check("§9 both driven lines reached the display list", texts == 2, "got " + texts);
        // §9 as literals: left 832.5 width 243.9 -> centre 954.45; top 989 height 55, two lines of
        // 17.5 -> a 35-high block starting 10 below the top.
        Near("§9 the block is centred on the box's x", cx, 954.45, 0.002);
        Near("§9 the two-line block is centred vertically", y1, 999.0, 0.002);
        Near("§9 the second line sits one line-height below the first", y2 - y1, 17.5, 0.002);

        // the box itself: the #334970 edge and the #1D2C4D fill, border-box (the edge is INSIDE).
        bool edge = false, fill = false;
        for (int i = 0; i < live.Count; i++)
        {
            Rgba c = live.At(i).Colour;
            if (SameColour(c, Rgba.Hex("334970"))) edge = true;
            if (SameColour(c, Rgba.Hex("1D2C4D"))) fill = true;
        }
        Check("§9 the pop-up draws its #334970 border", edge, "");
        Check("§9 ...and its #1D2C4D fill", fill, "");
        Check("§9 the fill is inset by the 1px border (border-box)",
              FindRect(live, BaseBar.PopLeft + 1f, BaseBar.PopTop + 1f + (BaseBar.PopRadius - 1f),
                       BaseBar.PopWidth - 2f, BaseBar.PopHeight - 2f - 2f * (BaseBar.PopRadius - 1f)) >= 0, "");

        // ⭐ ONE LINE ALONE STILL CENTRES - the box is not two-line-only.
        DisplayList one = Page("ONLY ONE", null);
        for (int i = 0; i < one.Count; i++)
            if (one.At(i).Kind == DrawKind.Text)
                Near("§9 a single line centres in the box", one.At(i).B, 989.0 + (55.0 - 17.5) / 2.0, 0.002);
    }

    // ============================================================================================
    //  8. ⛔ THE BAN, CHECKED AT THE SOURCE, WHERE IT CAN ACTUALLY BE CHECKED.
    //  OWNER, 2026-09-09: "he should only reference what you tell him too regarding building the
    //  shells how you tell him too. not the old builds."
    // ============================================================================================
    static void TheOldBarIsNotReferencedInLiveCode()
    {
        string[] files = { Repo("plugin", "src", "pure", "BaseBar.cs"),
                           Repo("plugin", "src", "pure", "BasePageNoIcon.cs") };
        foreach (string f in files)
        {
            Check("the new file exists: " + Path.GetFileName(f), File.Exists(f), f);
            if (!File.Exists(f)) continue;
            string live = Live(File.ReadAllText(f));
            string name = Path.GetFileName(f);
            Check(name + " does not name BottomBar in live code",
                  live.IndexOf("BottomBar", StringComparison.Ordinal) < 0, "");
            Check(name + " does not use DragonPalette in live code (§3's colours are literals)",
                  live.IndexOf("DragonPalette", StringComparison.Ordinal) < 0, "");
            Check(name + " does not carry the old ground #111B52 in live code",
                  live.IndexOf("111B52", StringComparison.Ordinal) < 0, "");
            Check(name + " does not name a bar_cap asset in live code",
                  live.IndexOf("bar_cap", StringComparison.Ordinal) < 0, "");
            Check(name + " does not carry the old bar's 3427x2112 frame in live code",
                  live.IndexOf("3427", StringComparison.Ordinal) < 0
                  && live.IndexOf("2112", StringComparison.Ordinal) < 0, "");
        }
        // ⭐ And the fit is a MIN in live code, not a width-only scale.
        string bar = Live(File.ReadAllText(Repo("plugin", "src", "pure", "BaseBar.cs")));
        Check("§2 the fit is a Math.Min of both ratios, in LIVE code",
              bar.IndexOf("Math.Min(w / FrameW, h / FrameH)", StringComparison.Ordinal) >= 0,
              "the one-factor fit is not where the guard can see it");
    }
}
