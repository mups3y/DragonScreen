/*
 * DragonScreen — BasePageNoIcon : the NON-ICON base screen.
 *
 * ⛔ BUILT FROM `SPEC_BASE_SCREENS.md` AND NOTHING ELSE — §2 the fit, §3 the colours, §4 which
 * primitive fills which shape, §5 the outer border, §6 the content window, §8 the bar (via
 * `BaseBar`), §9 the pop-up. ⛔ No existing page or bar class was read, called, copied or
 * "patterned on". OWNER, 2026-09-09, verbatim: *"he should only reference what you tell him too
 * regarding building the shells how you tell him too. not the old builds."*
 *
 * ⭐ IT IS A RENDERER AND NOTHING ON IT IS CLICKABLE (§10.2). No `UiPage` value, no routing, no
 * `PageAction`, and `PageCount` does not change. ⚠ A hit map that cannot be exercised is untested
 * code; wiring comes after both base screens are proved.
 *
 * ---- ⭐⭐ THE PATHS ARE HELD AS THE SPEC'S OWN TEXT, AND THE TEST IS WHAT MAKES THAT WORTH DOING ----
 * `OuterBorderPath` and `WindowPath` below are §5 and §6 VERBATIM. ⛔ Nothing in the draw path parses
 * them — the drawing has its own numbers. `BasePageNoIconTest` parses the strings and asserts the
 * DRAWN geometry agrees with them, so the page is pinned against the specification's own characters
 * by two independent expressions of it. Change one coordinate in either and the suite fails.
 *
 * ---- ⚠ TWO PLACES THIS FILE MAKES A CHOICE THE SPEC DOES NOT MAKE FOR IT ----
 * Both are recorded rather than buried, and both are raised to the overseer with this commit:
 *  (1) ⭐ THE GROUND IS PAINTED OVER THE WHOLE DEVICE SURFACE, not only inside the mapped frame.
 *      §2 leaves the spare pixels' colour unstated, and at 2560x1419 that is a ~6.8 px band top and
 *      bottom. Painting them the page ground is the only option that renders IDENTICALLY in both
 *      renderers: leaving them unpainted shows GDI+'s clear colour in the preview and GL's on the
 *      glass, which is a silent two-renderer divergence — the exact failure this project keeps
 *      paying for. One `Rect` to change if the owner wants black bands instead. (`BOB-27`)
 *  (2) ⭐ THE BORDER'S 3px STROKE IS DRAWN AS TWO OFFSET REGIONS — the path outset by 1.5 in white,
 *      then the path inset by 1.5 in `#070810` — rather than as separate edge bands. The border has
 *      45-degree chamfer joins, and butted bands leave a notch at a mitre that offset regions cannot
 *      leave. The window has no chamfers, so its 2px stroke IS drawn as a ring (4 bands + 4
 *      quarter-annuli), which is what keeps its 0.55 alpha compositing over the right two grounds:
 *      the outer half over `#070810` and the inner half over the window's own `#1A1F35`, exactly as
 *      a centred SVG stroke does.
 */
using System;

namespace DragonScreen
{
    public static class BasePageNoIcon
    {
        /// <summary>Ground + border (9+9) + window fill (7) + window ring (8) + the bar.</summary>
        public const int Commands = 34 + BaseBar.Commands;

        // ==========================================================================================
        //  §5 — THE OUTER BORDER, VERBATIM. 3px #FFFFFF, fill #070810, top corners r=12, bottom
        //  chamfers a true 45 degrees. ⛔ A past session replaced this with a small inset rounded
        //  rect and the owner caught it. The path is the specification.
        // ==========================================================================================
        public const string OuterBorderPath =
            "M 13 1 L 1907 1 A 12 12 0 0 1 1919 13 L 1919 942 L 1882 979 L 38 979 L 1 942 L 1 13 "
            + "A 12 12 0 0 1 13 1 Z";

        public const float OuterLeft = 1f;
        public const float OuterRight = 1919f;
        public const float OuterTop = 1f;
        public const float OuterBottom = 979f;
        public const float OuterRadius = 12f;
        /// <summary>Where each side edge meets its chamfer (§5's `L 1919 942` / `L 1 942`).</summary>
        public const float ChamferY = 942f;
        /// <summary>Where each chamfer meets the bottom edge (§5's `L 1882 979` / `L 38 979`).</summary>
        public const float ChamferXLeft = 38f;
        public const float ChamferXRight = 1882f;
        public const float OuterStroke = 3f;

        // ==========================================================================================
        //  §6 — THE CONTENT WINDOW (NON-ICON), VERBATIM. 2px #FFFFFF at stroke-opacity 0.55, fill
        //  #1A1F35, r=10, inset 16px from the border's inner face on all four sides.
        // ==========================================================================================
        public const string WindowPath =
            "M 29.5 19.5 L 1890.5 19.5 A 10 10 0 0 1 1900.5 29.5 L 1900.5 950.5 A 10 10 0 0 1 "
            + "1890.5 960.5 L 29.5 960.5 A 10 10 0 0 1 19.5 950.5 L 19.5 29.5 A 10 10 0 0 1 "
            + "29.5 19.5 Z";

        public const float WindowLeft = 19.5f;
        public const float WindowRight = 1900.5f;
        public const float WindowTop = 19.5f;
        public const float WindowBottom = 960.5f;
        public const float WindowRadius = 10f;
        public const float WindowStroke = 2f;

        // ==========================================================================================
        //  §3 — COLOURS. Every value measured from source. ⛔ Do not "improve" them.
        // ==========================================================================================
        /// <summary>Page ground · bottom-bar ground · content-window fill.</summary>
        public static readonly Rgba Ground = Rgba.Hex("1A1F35");

        /// <summary>
        /// The margin between the window line and the outer border, AND the tab band.
        ///
        /// ⛔⛔ `#070810` IS A CHOSEN VALUE, NOT A MEASURED ONE, AND A FUTURE SESSION MUST NOT
        /// "CORRECT" IT BACK. Every other colour here is sampled from a source; this one is not.
        /// `#14152C` — the measured "screen-on black", both top corners of the NASA sheet exactly
        /// rgb(20,21,44) — WAS the value and it was REJECTED on the render: it sits only rgb(6,10,9)
        /// from the window fill across 6.3 % of the page against the fill's 89.1 %, too close for the
        /// eye to read two zones, and it is a lifted BLUE-CAST black (blue 44 is more than double red
        /// 20) so it reads as dark blue beside dark blue rather than as black framing blue.
        /// ⭐ The owner reviewed four candidates side by side and chose this one — *"C looks right,
        /// lock it in"*, 2026-09-09. ⭐ A MEASURED VALUE CAN STILL BE THE WRONG VALUE.
        /// </summary>
        public static readonly Rgba Margin = Rgba.Hex("070810");

        /// <summary>The outer border stroke: opaque white.</summary>
        public static readonly Rgba BorderInk = new Rgba(1f, 1f, 1f, 1f);

        /// <summary>The content-window stroke: white at stroke-opacity 0.55 (§3).</summary>
        public static readonly Rgba WindowInk = new Rgba(1f, 1f, 1f, 0.55f);

        /// <summary>
        /// √2 − 1. Offsetting a 45-degree edge inward by d moves its intersection with the
        /// neighbouring square edge by exactly d·(√2−1) along that edge — which is what keeps the
        /// chamfer a TRUE 45 degrees at every stroke offset instead of only on the path itself.
        /// </summary>
        private const float Diag = 0.41421356f;

        public static void Draw(DisplayList dl, int w, int h)
        {
            Draw(dl, w, h, null, null);
        }

        /// <summary>
        /// The same page with §9's event pop-up driven. ⭐ This overload exists so the pop-up can be
        /// proved the only way it can be: by putting a value in and watching it appear. With both
        /// lines null the page is byte-identical to the two-argument form.
        /// </summary>
        public static void Draw(DisplayList dl, int w, int h, string eventLine1, string eventLine2)
        {
            BaseFit fit = BaseFit.For(w, h);

            // ---- the page ground. See this file's header, note (1), for why it covers the SURFACE
            // and not merely the mapped frame.
            dl.Rect(0f, 0f, w, h, Ground);

            // ---- §5: the border. Outset by half the stroke in white, then inset by half the stroke
            // in #070810 — a 3px stroke centred on the path, with the mitres exact at the chamfers.
            BorderRegion(dl, fit, -OuterStroke * 0.5f, BorderInk);
            BorderRegion(dl, fit, OuterStroke * 0.5f, Margin);

            // ---- §6: the window. Fill TO THE PATH, then the 2px ring centred on it, so the
            // stroke's inner half composites over #1A1F35 and its outer half over #070810.
            BaseBar.RoundRect(dl, fit, WindowLeft, WindowTop,
                              WindowRight - WindowLeft, WindowBottom - WindowTop,
                              WindowRadius, Ground);
            WindowRing(dl, fit);

            // ---- §8 + §9: the bar draws itself, on the page ground, with no ground of its own.
            BaseBar.Draw(dl, fit, eventLine1, eventLine2);
        }

        /// <summary>
        /// §5's path, offset INWARD by <paramref name="d"/> (negative outsets), filled.
        ///
        /// ⭐ §4 assigns the shapes: straight runs → `Rect`, the 45-degree diagonals → `Tri`, the
        /// rounded corners → `ArcBand` quarter-discs. The two bottom chamfers are the diagonals.
        /// ⛔ `Line` could stroke a diagonal but cannot FILL beside one — which is the whole reason
        /// `Tri` was added (S240) and why §4 names it here.
        ///
        /// The corner ARC CENTRES do not move with the offset: the centre is r from each of two
        /// edges, so shrinking the radius by d as both edges move in by d keeps it tangent to both.
        /// That is why 13 and 1907 appear as literals below and the radius does not.
        /// </summary>
        private static void BorderRegion(DisplayList dl, BaseFit fit, float d, Rgba col)
        {
            float e = d * Diag;
            float l = OuterLeft + d, r = OuterRight - d;
            float t = OuterTop + d, b = OuterBottom - d;
            float yc = ChamferY - e;                       // side edge meets chamfer
            float xl = ChamferXLeft + e, xr = ChamferXRight - e;   // chamfer meets bottom edge
            float rad = OuterRadius - d;
            float cxl = OuterLeft + OuterRadius;           // 13 — invariant under the offset
            float cxr = OuterRight - OuterRadius;          // 1907
            float cy = OuterTop + OuterRadius;             // 13

            // ⭐ Every internal boundary below overlaps by `BaseBar.Seam` - see its header for the
            // measured hairline that overlap removes. Each overlap is provably INSIDE this region, so
            // no edge of the shape moves: the top strip and the bottom band reach into the body, and
            // the corner discs sweep just past their own flat ends into the strips they meet.
            float ov = BaseBar.Seam;
            double sw = BaseBar.SweepOver(rad);

            // the body, from below the corner arcs down to where the chamfers start
            dl.Rect(fit.X(l), fit.Y(cy), fit.S(r - l), fit.S(yc - cy), col);
            // the top strip, between the two corner arcs
            dl.Rect(fit.X(cxl), fit.Y(t), fit.S(cxr - cxl), fit.S(cy - t + ov), col);
            // the two rounded top corners
            dl.ArcBand(fit.X(cxl), fit.Y(cy), 0f, fit.S(rad), 270.0 - sw, 360.0 + sw, col);
            dl.ArcBand(fit.X(cxr), fit.Y(cy), 0f, fit.S(rad), 0.0 - sw, 90.0 + sw, col);
            // the bottom band between the two chamfers
            dl.Rect(fit.X(xl), fit.Y(yc - ov), fit.S(xr - xl), fit.S(b - yc + ov), col);
            // ⭐ the two 45-degree chamfers — §4's `Tri`. ⛔ THEIR HYPOTENUSE IS THE SHAPE'S OWN EDGE
            // and does not move; the overlap they need against the bottom band is carried by the two
            // narrow rects below, whose every pixel is interior (they stop `Seam` short of the bottom
            // edge, which is exactly where the chamfer reaches them).
            dl.Tri(fit.X(l), fit.Y(yc), fit.X(xl), fit.Y(yc), fit.X(xl), fit.Y(b), col);
            dl.Tri(fit.X(r), fit.Y(yc), fit.X(xr), fit.Y(yc), fit.X(xr), fit.Y(b), col);
            dl.Rect(fit.X(xl - ov), fit.Y(yc - ov), fit.S(2f * ov), fit.S(b - ov - (yc - ov)), col);
            dl.Rect(fit.X(xr - ov), fit.Y(yc - ov), fit.S(2f * ov), fit.S(b - ov - (yc - ov)), col);
        }

        /// <summary>
        /// §6's 2px stroke, centred on the window path: four straight bands and four quarter-ANNULI.
        ///
        /// ⭐ A ring rather than two offset fills, because this stroke is 0.55 alpha and the two
        /// halves sit on different grounds. ⭐ And it needs no mitres: a rounded rectangle's corners
        /// meet their edges tangentially, so each band butts exactly onto the arc's endpoint —
        /// `ArcBand`'s inner/outer radii are what a 2px ring is FOR.
        /// </summary>
        private static void WindowRing(DisplayList dl, BaseFit fit)
        {
            float o = WindowStroke * 0.5f;                 // 1 — half the stroke, each side of the path
            float l = WindowLeft, r = WindowRight, t = WindowTop, b = WindowBottom;
            float rad = WindowRadius;
            float cxl = l + rad, cxr = r - rad, cyt = t + rad, cyb = b - rad;

            dl.Rect(fit.X(cxl), fit.Y(t - o), fit.S(cxr - cxl), fit.S(WindowStroke), WindowInk);
            dl.Rect(fit.X(cxl), fit.Y(b - o), fit.S(cxr - cxl), fit.S(WindowStroke), WindowInk);
            dl.Rect(fit.X(l - o), fit.Y(cyt), fit.S(WindowStroke), fit.S(cyb - cyt), WindowInk);
            dl.Rect(fit.X(r - o), fit.Y(cyt), fit.S(WindowStroke), fit.S(cyb - cyt), WindowInk);

            dl.ArcBand(fit.X(cxl), fit.Y(cyt), fit.S(rad - o), fit.S(rad + o), 270.0, 360.0, WindowInk);
            dl.ArcBand(fit.X(cxr), fit.Y(cyt), fit.S(rad - o), fit.S(rad + o), 0.0, 90.0, WindowInk);
            dl.ArcBand(fit.X(cxr), fit.Y(cyb), fit.S(rad - o), fit.S(rad + o), 90.0, 180.0, WindowInk);
            dl.ArcBand(fit.X(cxl), fit.Y(cyb), fit.S(rad - o), fit.S(rad + o), 180.0, 270.0, WindowInk);
        }
    }
}
