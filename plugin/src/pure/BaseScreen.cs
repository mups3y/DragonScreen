// DragonScreen — BASE SCREEN (the NON-ICON shell)  (register S242; overseer PROMPT 1 v2)
// ============================================================================================
// PURE. The shell every page draws inside: the outer border, the two grounds, the content window,
// and the bottom bar. `BaseScreenTabbed` extends it with the nine-tab strip.
//
// ---- ⛔ THE RULE THIS FILE EXISTS TO OBEY ----
// Owner, verbatim: *"The only 'baked in' things should be the colours, border lines, bottom bar lines.
// The rest should be live and working as intended."*
// BAKED here means the GEOMETRY IS A CONSTANT DRAWN BY CODE — not a picture. Nothing in this file
// loads a tile. ⭐ That was settled as `BOB-10`: a tile would have to UPSCALE (a ~49 px chamfer and
// ~90 px bevels at the shipped 2560), where the one tiling precedent in this codebase
// (`bar_cap_left.png`, 132x105 into 88x70) DOWNSCALES. An upscaled hard 45 degree edge goes visibly
// soft, and H-01 was a resolution-mismatch defect.
//
// ---- ⛔ TWO REFERENCE FRAMES EXIST AND MIXING THEM IS THE CLASSIC FAILURE ----
// Everything in THIS file is in a 1920 x 1054 frame. `BottomBar` has its OWN 3427 x 2112 frame and
// its own `Scale()`, and is called through its own entry point with its own fit. ⛔ Never convert
// between them by hand; never hardcode 1920 — the glass is 2560 x 1405 (screen 2 measures 1419, a
// 1 % outlier still unexplained), so every number below is scaled by `Sc(w)`.
//
// ---- HOW THE SHAPES ARE MADE, NOW THAT THEY CAN BE ----
//   straight runs        -> Rect
//   the four 45 deg diagonals -> Tri   (S240 — the primitive S239 stopped for)
//   the rounded corners  -> ArcBand(cx, cy, 0, r, ...)  — the `BarEvent.RoundRect` pattern, reused
//                           rather than reinvented, so there is one construction for a rounded
//                           rectangle in this codebase and not two.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>
    /// The shell's own palette. ⛔ MEASURED from the locked design, not chosen — do not "improve" one.
    /// They are here rather than in `DragonPalette` because they are the REBUILD's colours and the
    /// shipping pages still use their own; merging the two sets before the replacement lands would
    /// change the build the owner is flying.
    /// </summary>
    public static class BasePalette
    {
        /// <summary>Page ground, bottom-bar ground, and the content window's fill.</summary>
        public static readonly Rgba Ground = Rgba.Hex("#1A1F35", 1f);
        /// <summary>The margin between the window line and the outer border — and the tab band. ⭐ The
        /// band needs no shape of its own: it is simply inside the border and outside the window.</summary>
        public static readonly Rgba Margin = Rgba.Hex("#14152C", 1f);
        /// <summary>The event pop-up's fill.</summary>
        public static readonly Rgba PopupFill = Rgba.Hex("#1D2C4D", 1f);
        /// <summary>The event pop-up's 1px border.</summary>
        public static readonly Rgba PopupBorder = Rgba.Hex("#334970", 1f);
        /// <summary>The outer border stroke, and the tab selector line.</summary>
        public static readonly Rgba Stroke = Rgba.Hex("#FFFFFF", 1f);
        /// <summary>⚠ The content-window stroke is white at `stroke-opacity 0.55` — the opacity is part
        /// of the measurement, not a rendering preference.</summary>
        public static readonly Rgba WindowStroke = Rgba.Hex("#FFFFFF", 0.55f);
    }

    public static class BaseScreen
    {
        // ---- the reference frame ----------------------------------------------------------------
        public const float RefW = 1920f, RefH = 1054f;

        /// <summary>
        /// Uniform scale from the 1920-wide reference frame onto the panel.
        /// ⛔ ONE factor for both axes. The shipped glass is 2560 x 1405: 2560/1920 = 1.3333 and
        /// 1405/1054 = 1.3330, so the frame is within 0.03 % of square and a per-axis scale would buy
        /// nothing while making every diagonal a different angle than the one that was measured.
        /// </summary>
        public static float Sc(int w) { return w / RefW; }

        // ---- the outer border, verbatim from the locked path -------------------------------------
        //   M 13 1 L 1907 1 A 12 12 0 0 1 1919 13 L 1919 942 L 1882 979 L 38 979 L 1 942 L 1 13
        //   A 12 12 0 0 1 13 1 Z
        // ⛔ FULL-BLEED. A previous session replaced this with a small inset rounded rectangle and the
        // owner caught it. The numbers below are that path and nothing else.
        public const float BorderL = 1f, BorderT = 1f, BorderR = 1919f;
        /// <summary>Where the two bottom chamfers begin. Below this the shape narrows.</summary>
        public const float ChamferTop = 942f;
        /// <summary>The bottom edge of the border — the short edge between the two chamfers.</summary>
        public const float BorderB = 979f;
        /// <summary>The chamfer's run. ⭐ 979-942 == 38-1, so it is a true 45 degrees by construction.</summary>
        public const float ChamferRun = BorderB - ChamferTop;   // 37
        public const float BorderR12 = 12f;
        public const float BorderStrokePx = 3f;

        // ---- the content window ------------------------------------------------------------------
        public const float WinL = 19.5f, WinT = 19.5f, WinR = 1900.5f, WinB = 960.5f;
        public const float WinR10 = 10f;
        public const float WinStrokePx = 2f;

        /// <summary>
        /// ⚠ The ICON shelf — the flat top of the notch the tab strip sits in. CHANGED 914 -> 893 when
        /// the selector line was added below the labels; every y in `BaseScreenTabbed` is derived from
        /// it, so moving it moves the whole stack.
        /// </summary>
        public const float Shelf = 893f;
        /// <summary>The notch's bevel run. ⭐ `WinB - Shelf` == 67.5, and the path's own x offsets are
        /// 534-466.5 and 1453.5-1386 == 67.5, so the bevels are a true 45 degrees.</summary>
        public const float NotchRun = WinB - Shelf;             // 67.5
        /// <summary>Where the notch's flat top starts and ends, from the locked path.</summary>
        public const float NotchInnerL = 534f, NotchInnerR = 1386f;
        public const float NotchOuterL = NotchInnerL - NotchRun;   // 466.5
        public const float NotchOuterR = NotchInnerR + NotchRun;   // 1453.5

        // ============================================================================================
        //  SHARED CONSTRUCTIONS — ⭐ factored on first use, not on the third, because Prompt 2 builds
        //  34 more pages on them and a geometry copy-pasted twice is a geometry that will disagree.
        // ============================================================================================

        /// <summary>
        /// A filled rounded rectangle in REFERENCE units, scaled onto the panel.
        /// ⭐ The construction is `BarEvent.RoundRect`'s — a cross of three rects plus four filled
        /// quarter-discs — reused deliberately so this codebase has ONE rounded rectangle and not two.
        /// </summary>
        public static void RoundRectFill(DisplayList dl, float sc,
                                         float x, float y, float w, float h, float r, Rgba c)
        {
            if (r > w * 0.5f) r = w * 0.5f;
            if (r > h * 0.5f) r = h * 0.5f;
            dl.Rect((x + r) * sc, y * sc, (w - 2f * r) * sc, h * sc, c);
            dl.Rect(x * sc, (y + r) * sc, r * sc, (h - 2f * r) * sc, c);
            dl.Rect((x + w - r) * sc, (y + r) * sc, r * sc, (h - 2f * r) * sc, c);
            Corner(dl, sc, x + r,         y + r,         r, 270.0, 360.0, c);   // top-left
            Corner(dl, sc, x + w - r,     y + r,         r,   0.0,  90.0, c);   // top-right
            Corner(dl, sc, x + w - r,     y + h - r,     r,  90.0, 180.0, c);   // bottom-right
            Corner(dl, sc, x + r,         y + h - r,     r, 180.0, 270.0, c);   // bottom-left
        }

        /// <summary>One filled quarter-disc. ⚠ `ArcBand`'s angles are instrument convention — 0 at
        /// twelve o'clock, increasing clockwise — which is why the quadrants read 270..360 for the
        /// TOP-LEFT corner and not 90..180. Same numbers `BarEvent.RoundRect` uses.</summary>
        public static void Corner(DisplayList dl, float sc, float cx, float cy, float r,
                                  double a0, double a1, Rgba c)
        {
            dl.ArcBand(cx * sc, cy * sc, 0f, r * sc, a0, a1, c);
        }

        /// <summary>A stroked arc of the same quadrant — the corner's OUTLINE rather than its fill,
        /// as a thin annulus centred on the path.</summary>
        public static void CornerStroke(DisplayList dl, float sc, float cx, float cy, float r,
                                        double a0, double a1, float px, Rgba c)
        {
            float half = px * 0.5f;
            dl.ArcBand(cx * sc, cy * sc, (r - half) * sc, (r + half) * sc, a0, a1, c);
        }

        /// <summary>One straight stroke segment in reference units.</summary>
        public static void Seg(DisplayList dl, float sc, float x0, float y0, float x1, float y1,
                               float px, Rgba c)
        {
            dl.Line(x0 * sc, y0 * sc, x1 * sc, y1 * sc, px * sc, c);
        }

        // ============================================================================================
        //  THE OUTER BORDER
        // ============================================================================================

        /// <summary>
        /// The border's FILL — `#14152C`, the margin colour. Everything the window does not cover shows
        /// this, which is what makes the tab band a band without needing a shape of its own.
        ///
        /// Decomposed exactly as the path reads: a rounded-top body down to the chamfer line, then a
        /// trapezoid — a rect between the two chamfers plus one `Tri` per chamfer.
        /// </summary>
        public static void BorderFill(DisplayList dl, int w)
        {
            float sc = Sc(w);
            Rgba c = BasePalette.Margin;
            // The body, y 1..942, with the two rounded TOP corners only.
            dl.Rect((BorderL + BorderR12) * sc, BorderT * sc,
                    (BorderR - BorderL - 2f * BorderR12) * sc, BorderR12 * sc, c);
            dl.Rect(BorderL * sc, (BorderT + BorderR12) * sc,
                    (BorderR - BorderL) * sc, (ChamferTop - BorderT - BorderR12) * sc, c);
            Corner(dl, sc, BorderL + BorderR12, BorderT + BorderR12, BorderR12, 270.0, 360.0, c);
            Corner(dl, sc, BorderR - BorderR12, BorderT + BorderR12, BorderR12,   0.0,  90.0, c);
            // The chamfered foot, y 942..979.
            dl.Rect((BorderL + ChamferRun) * sc, ChamferTop * sc,
                    (BorderR - BorderL - 2f * ChamferRun) * sc, ChamferRun * sc, c);
            // ⭐ The two 45 degree fills S240 exists for. Left: the solid lies ABOVE-RIGHT of the
            // hypotenuse (1,942)->(38,979); right: above-left of (1919,942)->(1882,979).
            dl.Tri(BorderL * sc, ChamferTop * sc,
                   (BorderL + ChamferRun) * sc, ChamferTop * sc,
                   (BorderL + ChamferRun) * sc, BorderB * sc, c);
            dl.Tri(BorderR * sc, ChamferTop * sc,
                   (BorderR - ChamferRun) * sc, ChamferTop * sc,
                   (BorderR - ChamferRun) * sc, BorderB * sc, c);
        }

        /// <summary>The border's 3px white outline, segment by segment along the locked path.</summary>
        public static void BorderStroke(DisplayList dl, int w)
        {
            float sc = Sc(w);
            Rgba c = BasePalette.Stroke;
            float px = BorderStrokePx;
            Seg(dl, sc, BorderL + BorderR12, BorderT, BorderR - BorderR12, BorderT, px, c);   // top
            Seg(dl, sc, BorderR, BorderT + BorderR12, BorderR, ChamferTop, px, c);            // right
            Seg(dl, sc, BorderR, ChamferTop, BorderR - ChamferRun, BorderB, px, c);           // chamfer R
            Seg(dl, sc, BorderR - ChamferRun, BorderB, BorderL + ChamferRun, BorderB, px, c); // bottom
            Seg(dl, sc, BorderL + ChamferRun, BorderB, BorderL, ChamferTop, px, c);           // chamfer L
            Seg(dl, sc, BorderL, ChamferTop, BorderL, BorderT + BorderR12, px, c);            // left
            CornerStroke(dl, sc, BorderL + BorderR12, BorderT + BorderR12, BorderR12, 270.0, 360.0, px, c);
            CornerStroke(dl, sc, BorderR - BorderR12, BorderT + BorderR12, BorderR12,   0.0,  90.0, px, c);
        }

        // ============================================================================================
        //  THE CONTENT WINDOW
        // ============================================================================================

        /// <summary>The NON-ICON window: a plain rounded rectangle, filled `#1A1F35`.</summary>
        public static void WindowFill(DisplayList dl, int w)
        {
            RoundRectFill(dl, Sc(w), WinL, WinT, WinR - WinL, WinB - WinT, WinR10, BasePalette.Ground);
        }

        /// <summary>The NON-ICON window's 2px white @0.55 outline.</summary>
        public static void WindowStroke(DisplayList dl, int w)
        {
            float sc = Sc(w);
            Rgba c = BasePalette.WindowStroke;
            float px = WinStrokePx, r = WinR10;
            Seg(dl, sc, WinL + r, WinT, WinR - r, WinT, px, c);
            Seg(dl, sc, WinR, WinT + r, WinR, WinB - r, px, c);
            Seg(dl, sc, WinR - r, WinB, WinL + r, WinB, px, c);
            Seg(dl, sc, WinL, WinB - r, WinL, WinT + r, px, c);
            CornerStroke(dl, sc, WinL + r, WinT + r, r, 270.0, 360.0, px, c);
            CornerStroke(dl, sc, WinR - r, WinT + r, r,   0.0,  90.0, px, c);
            CornerStroke(dl, sc, WinR - r, WinB - r, r,  90.0, 180.0, px, c);
            CornerStroke(dl, sc, WinL + r, WinB - r, r, 180.0, 270.0, px, c);
        }

        // ============================================================================================
        //  THE WHOLE NON-ICON SHELL
        // ============================================================================================

        /// <summary>
        /// Ground, border, window — in that order, because each is drawn OVER the last and the tab band
        /// is defined by what the window does not cover.
        /// ⛔ The bottom bar is NOT drawn here: it has its own reference frame and its own fit, and the
        /// caller passes it through `BottomBar`'s own entry point. Mixing the two frames in one method
        /// is the failure this file's header warns about.
        /// </summary>
        public static void Draw(DisplayList dl, int w, int h)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            dl.Rect(0f, 0f, w, h, BasePalette.Ground);
            BorderFill(dl, w);
            WindowFill(dl, w);
            BorderStroke(dl, w);
            WindowStroke(dl, w);
        }
    }
}
