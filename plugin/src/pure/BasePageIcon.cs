/*
 * DragonScreen — BasePageIcon : the ICON base screen. The NON-ICON page plus a notch and a tab strip.
 *
 * ⛔ BUILT FROM `SPEC_BASE_SCREENS.md` AND NOTHING ELSE — §2/§2.1 the fit and the band, §3 the
 * colours, §4 which primitive fills which shape, §5 the outer border (shared, one expression),
 * §6 ICON the notched window, §7 the tab strip, §8 the bar via `BaseBar`, §9 the pop-up.
 * ⛔ No existing page or bar class was read, called, copied or "patterned on". OWNER, 2026-09-09,
 * verbatim: *"he should only reference what you tell him too regarding building the shells how you
 * tell him too. not the old builds."*
 *
 * ⭐ IT DIFFERS FROM `BasePageNoIcon` IN EXACTLY TWO WAYS, and everything else is shared or identical:
 * the window carries §6's notch, and §7's nine tabs sit in the band the notch opens up. The border,
 * the grounds, the fit and the bar are the same and are not re-derived.
 *
 * ⛔ NOT A SUBCLASS AND NOT A REFACTOR OF THE PROVED PAGE. `BasePageNoIcon` is verified and the owner
 * has seen it; this is a second renderer beside it. The ONE thing they share in code is
 * `BasePageNoIcon.BorderRegion`, because §5 says the outer border is *"identical on BOTH pages"* and
 * two copies of a thing that must be identical is how they stop being identical. ⚠ The window's ring
 * and fill are deliberately NOT shared: this one has a notch, and the abstraction that covers both is
 * better written once both exist and the duplication is visible.
 *
 * ---- ⚠ ONE THING §7 ASKS FOR THAT THE Text PRIMITIVE CANNOT EXPRESS ----
 * §7's labels carry **LETTER-SPACING 0.3px** (`reference_gen.py:59`, a CSS property on `.tl`).
 * ⛔ `DisplayList.Text` has no letter-spacing and neither renderer has one: the string is handed to
 * `Graphics.DrawString` / the GL text path whole, and pure has no font metrics to advance characters
 * itself ("WIDTH IS NOT KNOWN HERE, AND THAT IS A REAL LIMIT" — `DisplayList.Text`).
 * ⭐ So the value is RECORDED here and NOT APPLIED, and the test says so in those words rather than
 * pretending. Adding it means widening the Text command and teaching BOTH renderers to advance per
 * character — a primitive change of the same class as `Tri`, which got its own task (S240) and its own
 * two-renderer pin, and doing it inside a tab-strip commit is exactly how S75 happened (an attribute
 * one renderer honoured and the other silently ignored).
 * ⚠ MEASURED COST, so the question is answerable: CSS adds 0.3px per character, so the widest label
 * ("Avionics", 8 characters) renders 2.4px narrower than the approved canvas, centred, inside a 90.3px
 * box it fills to about 52px. Nothing clips and nothing moves — the lettering is fractionally tighter.
 * Raised as `BOB-30`.
 */
using System;

namespace DragonScreen
{
    public static class BasePageIcon
    {
        /// <summary>Surface + border (9+9) + window fill (16) + ring (14) + tabs (19) + the bar.</summary>
        public const int Commands = 69 + BaseBar.Commands;

        // ==========================================================================================
        //  §6 ICON — THE CONTENT WINDOW WITH THE NOTCH THE TAB STRIP SITS IN, VERBATIM.
        //  ⛔ Same path as the NON-ICON window plus four points: the bottom edge rises to the SHELF
        //  between two true-45-degree bevels.
        // ==========================================================================================
        public const string WindowPath =
            "M 29.5 19.5 L 1890.5 19.5 A 10 10 0 0 1 1900.5 29.5 L 1900.5 950.5 A 10 10 0 0 1 "
            + "1890.5 960.5 L 1453.5 960.5 L 1386 893 L 534 893 L 466.5 960.5 L 29.5 960.5 "
            + "A 10 10 0 0 1 19.5 950.5 L 19.5 29.5 A 10 10 0 0 1 29.5 19.5 Z";

        /// <summary>§6 — the raised floor over the tab block.</summary>
        public const float Shelf = 893f;
        /// <summary>§6 — the shelf's ends. ⭐ Sized off the tab LABELS (554.1..1366.8 + 20 pad), not
        /// the icons: the labels are wider and were being clipped when sized off the icons.</summary>
        public const float ShelfLeft = 534f;
        public const float ShelfRight = 1386f;

        /// <summary>
        /// §6 — the bevels' run, DERIVED. `B - SHELF = 67.5`, which is what makes them a true 45°.
        /// ⚠ `gen.py` carries a STALE COMMENT saying 46.5. The computed value is 67.5; trust the
        /// arithmetic. ⛔ Deriving it rather than typing it is what keeps the bevel at 45° if the
        /// shelf ever moves.
        /// </summary>
        public static float Run { get { return BasePageNoIcon.WindowBottom - Shelf; } }
        /// <summary>Where each bevel meets the window's bottom edge: 466.5 and 1453.5.</summary>
        public static float BevelLeft { get { return ShelfLeft - Run; } }
        public static float BevelRight { get { return ShelfRight + Run; } }

        // ==========================================================================================
        //  §7 — THE TAB STRIP. Nine tabs. ⛔ ICON page only.
        // ==========================================================================================
        public const int TabCount = 9;
        public const float TabPitch = 90.3f;
        public const float TabCx0 = 599.3f;
        public const float IconSize = 37f;
        public const float LabelPx = 12.3f;
        /// <summary>
        /// §7: "LINE-HEIGHT PINNED TO 12.3". ⭐ In this display list that pin is FREE and structural,
        /// not a setting: `Text`'s y IS the top of the line, so there is no line box that can render
        /// ~2.5px taller than its ink and push the block off-centre. The value is here because the
        /// stack below is derived from it.
        /// </summary>
        public const float LabelLineHeight = 12.3f;
        /// <summary>
        /// §7: "LETTER-SPACING 0.3px". ⛔ RECORDED, NOT APPLIED — see this file's header. The Text
        /// command cannot carry it and neither renderer can honour it, so claiming otherwise would be
        /// the S75 shape. `BOB-30`.
        /// </summary>
        public const float LabelLetterSpacing = 0.3f;
        public const float SelectorHeight = 4.8f;
        public const float SelectorWidth = 81.5f;
        /// <summary>§7 — tab 0 (`All`), the default the owner approved. ⛔ It never moves: §10.2, a
        /// shell is a renderer and nothing on it is clickable.</summary>
        public const int ActiveTab = 0;

        // ---- §7's DERIVED STACK. ⛔ Do NOT type these as literals — derive from `Shelf`, so moving
        // the shelf moves the whole block with it. ----
        private const float IconPad = 5.1f;        // shelf -> icon top
        private const float IconLabelGap = 4f;     // icon bottom -> label top
        private const float LabelSelectorGap = 16f;// label bottom -> selector top
        private const float SelectorPad = 5.3f;    // selector bottom -> the border's inner face

        public static float IconTop { get { return Shelf + IconPad; } }
        public static float LabelTop { get { return IconTop + IconSize + IconLabelGap; } }
        public static float SelectorTop { get { return LabelTop + LabelLineHeight + LabelSelectorGap; } }
        /// <summary>
        /// §7: `893 + 5.1 + 37 + 4 + 12.3 + 16 + 4.8 + 5.3 = 977.5` — and 977.5 is the OUTER BORDER's
        /// own inner face (§5's 979 less half its 3px stroke). ⭐ That the tab stack and the border
        /// arrive at the same number from opposite directions is the cross-check the suite asserts.
        /// </summary>
        public static float StackBottom { get { return SelectorTop + SelectorHeight + SelectorPad; } }

        /// <summary>§7's nine tabs, in order. ⛔ NO DIMMING: all nine icons and all nine labels peak
        /// at 255 whether active or not — measured on the approved render. The selector line is the
        /// ONLY thing that marks the active tab.</summary>
        private static readonly string[] TabLabel = {
            "All", "Crew", "Comms", "Prop", "Mech", "Power", "Avionics", "GNC", "Thermal"
        };
        private static readonly string[] TabIcon = {
            "ic_tab_all", "ic_tab_crew", "ic_tab_comms", "ic_tab_prop", "ic_tab_mech",
            "ic_tab_power", "ic_tab_avionics", "ic_tab_gnc", "ic_tab_thermal"
        };

        public static string Label(int i) { return TabLabel[i]; }
        public static string IconKey(int i) { return TabIcon[i]; }
        /// <summary>The centre of tab <paramref name="i"/>: `599.3 + i * 90.3`.</summary>
        public static float TabCentre(int i) { return TabCx0 + i * TabPitch; }

        public static void Draw(DisplayList dl, int w, int h)
        {
            Draw(dl, w, h, null, null);
        }

        public static void Draw(DisplayList dl, int w, int h, string eventLine1, string eventLine2)
        {
            BaseFit fit = BaseFit.For(w, h);

            // ---- §2.1: the whole device surface `#070810`, then §3's page ground over the mapped
            // frame ONLY. ⛔ Two different things: the band is `#070810` and the PAGE is `#1A1F35`,
            // which is what §8.1's bar sits on when it "draws NO ground of its own".
            // ⚠ IT BLEEDS ONE PIXEL PAST EVERY EDGE, AND THAT IS MEASURED, NOT CAUTIOUS.
            // Drawn exactly 0..w, the render left ROW 0 at rgb(5,7,36) - a blend of this colour with
            // whatever the renderer had cleared to - because GDI+ samples pixel CENTRES, so the
            // topmost row is only half covered by a rect whose edge sits exactly on 0. ⛔ The glass
            // clears to something else again, so that row would differ between the two renderers for
            // no reason anyone chose. One pixel of bleed removes the question.
            dl.Rect(-1f, -1f, w + 2f, h + 2f, BasePageNoIcon.Margin);
            dl.Rect(fit.X(0f), fit.Y(0f), fit.S(BaseFit.FrameW), fit.S(BaseFit.FrameH),
                    BasePageNoIcon.Ground);

            // ---- §5: the border, from the ONE expression of it. Identical on both pages.
            BasePageNoIcon.BorderRegion(dl, fit, -BasePageNoIcon.OuterStroke * 0.5f,
                                        BasePageNoIcon.BorderInk);
            BasePageNoIcon.BorderRegion(dl, fit, BasePageNoIcon.OuterStroke * 0.5f,
                                        BasePageNoIcon.Margin);

            // ---- §6 ICON: the notched window, filled to the path, then its 2px ring.
            WindowFill(dl, fit);
            WindowRing(dl, fit);

            // ---- §7: nine icons, nine labels, one selector.
            TabStrip(dl, fit);

            // ---- §8 + §9: the same bar, unchanged.
            BaseBar.Draw(dl, fit, eventLine1, eventLine2);
        }

        /// <summary>
        /// §6 ICON's fill, to the path: the window above the shelf, and the two pieces left and right
        /// of the notch below it.
        ///
        /// ⭐ Every internal boundary overlaps by `BaseBar.Seam` and every overlap is provably INSIDE
        /// the shape — see that constant's header for the 15 % hairline this prevents, measured on
        /// S245's first render. ⛔ The two bevel WEDGES are exact `Tri`s whose hypotenuse is the
        /// shape's own edge and does not move; their overlaps are carried by the two narrow rects that
        /// straddle x = BevelLeft / BevelRight and stop `Seam` short of the bottom edge, which is
        /// exactly where the bevel reaches them.
        /// </summary>
        private static void WindowFill(DisplayList dl, BaseFit fit)
        {
            float l = BasePageNoIcon.WindowLeft, r = BasePageNoIcon.WindowRight;
            float t = BasePageNoIcon.WindowTop, b = BasePageNoIcon.WindowBottom;
            float rad = BasePageNoIcon.WindowRadius;
            float sl = ShelfLeft, sr = ShelfRight, bl = BevelLeft, br = BevelRight;
            float ov = BaseBar.Seam;
            double sw = BaseBar.SweepOver(rad);
            Rgba col = BasePageNoIcon.Ground;

            // ---- above the shelf: the full-width window, with its two rounded top corners ----
            dl.Rect(fit.X(l), fit.Y(t + rad), fit.S(r - l), fit.S(Shelf - (t + rad)), col);
            dl.Rect(fit.X(l + rad), fit.Y(t), fit.S((r - rad) - (l + rad)), fit.S(rad + ov), col);
            dl.ArcBand(fit.X(l + rad), fit.Y(t + rad), 0f, fit.S(rad), 270.0 - sw, 360.0 + sw, col);
            dl.ArcBand(fit.X(r - rad), fit.Y(t + rad), 0f, fit.S(rad), 0.0 - sw, 90.0 + sw, col);

            // ---- left of the notch ----
            dl.Rect(fit.X(l), fit.Y(Shelf - ov), fit.S(bl - l), fit.S((b - rad) - (Shelf - ov)), col);
            dl.Rect(fit.X(l + rad), fit.Y(b - rad - ov), fit.S(bl - (l + rad)), fit.S(rad + ov), col);
            dl.ArcBand(fit.X(l + rad), fit.Y(b - rad), 0f, fit.S(rad), 180.0 - sw, 270.0 + sw, col);
            dl.Tri(fit.X(bl), fit.Y(Shelf), fit.X(sl), fit.Y(Shelf), fit.X(bl), fit.Y(b), col);
            dl.Rect(fit.X(bl - ov), fit.Y(Shelf), fit.S(2f * ov), fit.S((b - ov) - Shelf), col);
            dl.Rect(fit.X(bl), fit.Y(Shelf - ov), fit.S((sl - ov) - bl), fit.S(2f * ov), col);

            // ---- right of the notch ----
            dl.Rect(fit.X(br), fit.Y(Shelf - ov), fit.S(r - br), fit.S((b - rad) - (Shelf - ov)), col);
            dl.Rect(fit.X(br), fit.Y(b - rad - ov), fit.S((r - rad) - br), fit.S(rad + ov), col);
            dl.ArcBand(fit.X(r - rad), fit.Y(b - rad), 0f, fit.S(rad), 90.0 - sw, 180.0 + sw, col);
            dl.Tri(fit.X(br), fit.Y(Shelf), fit.X(sr), fit.Y(Shelf), fit.X(br), fit.Y(b), col);
            dl.Rect(fit.X(br - ov), fit.Y(Shelf), fit.S(2f * ov), fit.S((b - ov) - Shelf), col);
            dl.Rect(fit.X(sr + ov), fit.Y(Shelf - ov), fit.S(br - (sr + ov)), fit.S(2f * ov), col);
        }

        /// <summary>
        /// §6's 2px stroke at 0.55, centred on the ICON path: six straight bands, four quarter-annuli
        /// and the two bevels.
        ///
        /// ⛔ A RING, NOT §4's TWO OFFSET REGIONS, AND THE REASON IS THE ALPHA. §4 (`BOB-29`) rules
        /// that a diagonal is stroked as offset fill regions, and that is exactly what the opaque
        /// BORDER does. ⚠ An offset region is a MULTI-PIECE fill, and at 0.55 alpha its internal
        /// boundaries seam — a 26/255 step — with no overlap available to fix them, because two 0.55
        /// whites composite to 0.80 and would read brighter still. A ring's pieces meet ACROSS the
        /// band rather than along it, so a junction costs at most one device pixel of a 2px line.
        /// ⭐ Raised as `BOB-31` with the measurement rather than settled here.
        ///
        /// ⚠ THE FOUR BEVEL JUNCTIONS BUTT AT THE VERTEX, with each bevel band ending square across
        /// its own width. The exact mitre point is `±(√2−1)·o` away from that, so the gap or overlap
        /// is at most 0.41 design px — 0.55 device px, inside §11's ±1 — and `--basecheck` measures it
        /// rather than assuming it.
        /// </summary>
        private static void WindowRing(DisplayList dl, BaseFit fit)
        {
            float l = BasePageNoIcon.WindowLeft, r = BasePageNoIcon.WindowRight;
            float t = BasePageNoIcon.WindowTop, b = BasePageNoIcon.WindowBottom;
            float rad = BasePageNoIcon.WindowRadius;
            float o = BasePageNoIcon.WindowStroke * 0.5f;
            float sl = ShelfLeft, sr = ShelfRight, bl = BevelLeft, br = BevelRight;
            Rgba ink = BasePageNoIcon.WindowInk;
            float stroke = BasePageNoIcon.WindowStroke;

            dl.Rect(fit.X(l + rad), fit.Y(t - o), fit.S((r - rad) - (l + rad)), fit.S(stroke), ink);
            dl.Rect(fit.X(l - o), fit.Y(t + rad), fit.S(stroke), fit.S((b - rad) - (t + rad)), ink);
            dl.Rect(fit.X(r - o), fit.Y(t + rad), fit.S(stroke), fit.S((b - rad) - (t + rad)), ink);
            // the bottom edge, split by the notch
            dl.Rect(fit.X(l + rad), fit.Y(b - o), fit.S(bl - (l + rad)), fit.S(stroke), ink);
            dl.Rect(fit.X(br), fit.Y(b - o), fit.S((r - rad) - br), fit.S(stroke), ink);
            // the shelf
            dl.Rect(fit.X(sl), fit.Y(Shelf - o), fit.S(sr - sl), fit.S(stroke), ink);

            dl.ArcBand(fit.X(l + rad), fit.Y(t + rad), fit.S(rad - o), fit.S(rad + o), 270.0, 360.0, ink);
            dl.ArcBand(fit.X(r - rad), fit.Y(t + rad), fit.S(rad - o), fit.S(rad + o), 0.0, 90.0, ink);
            dl.ArcBand(fit.X(r - rad), fit.Y(b - rad), fit.S(rad - o), fit.S(rad + o), 90.0, 180.0, ink);
            dl.ArcBand(fit.X(l + rad), fit.Y(b - rad), fit.S(rad - o), fit.S(rad + o), 180.0, 270.0, ink);

            // ⭐ §4: the two notch bevels are `Tri`. A 2px band at 45° is a parallelogram, and two
            // triangles make one for the price of one extra command (`DisplayList.Tri`'s own note).
            // The half-width along each axis is o/√2, because the band is perpendicular to a 45° line.
            float n = o * 0.70710678f;
            Bevel(dl, fit, sl, Shelf, bl, b, n, n, ink);      // left: outward is +x, +y
            Bevel(dl, fit, sr, Shelf, br, b, -n, n, ink);     // right: outward is -x, +y
        }

        /// <summary>One 2px bevel band from (x0,y0) to (x1,y1), offset ±(nx,ny) perpendicular.</summary>
        private static void Bevel(DisplayList dl, BaseFit fit, float x0, float y0, float x1, float y1,
                                  float nx, float ny, Rgba ink)
        {
            float ax = x0 - nx, ay = y0 - ny;      // inner, at the shelf end
            float bx = x0 + nx, by = y0 + ny;      // outer, at the shelf end
            float cx = x1 + nx, cy = y1 + ny;      // outer, at the bottom end
            float dx = x1 - nx, dy = y1 - ny;      // inner, at the bottom end
            dl.Tri(fit.X(ax), fit.Y(ay), fit.X(bx), fit.Y(by), fit.X(cx), fit.Y(cy), ink);
            dl.Tri(fit.X(ax), fit.Y(ay), fit.X(cx), fit.Y(cy), fit.X(dx), fit.Y(dy), ink);
        }

        /// <summary>
        /// §7 — nine icons, nine labels, one selector under the active tab.
        ///
        /// ⛔ NO DIMMING. NONE. Measured on the approved render: all nine icons and all nine labels
        /// peak at 255 whether active or not, so every one is drawn with the identity tint and the
        /// same ink. ⛔ An inactive opacity, a grey or a tint is invention — the selector line is the
        /// ONLY thing that marks the active tab.
        /// </summary>
        private static void TabStrip(DisplayList dl, BaseFit fit)
        {
            Rgba ink = BaseBar.Ink;
            float iconTop = IconTop, labelTop = LabelTop;
            for (int i = 0; i < TabCount; i++)
            {
                float cx = TabCentre(i);
                dl.Asset(TabIcon[i], fit.X(cx - IconSize * 0.5f), fit.Y(iconTop),
                         fit.S(IconSize), fit.S(IconSize), ink);
            }
            for (int i = 0; i < TabCount; i++)
            {
                // §7: the label box is LEFT = cx − 45.15, WIDTH = 90.3 (one full pitch), text CENTRED
                // in it — so the anchor is the box's centre, which is the tab centre.
                dl.Text(TabLabel[i], fit.X(TabCentre(i)), fit.Y(labelTop), fit.S(LabelPx),
                        TextAlign.Centre, ink);
            }
            // ⛔ The selector never moves off tab 0 (§10.2). `reference_gen.py` defines SEL_Y/SEL_H/
            // SEL_W and then never emits the element; the selector IS in the design and is measured on
            // the approved render at design x 558.0 width 83.0 (= 558.55 / 81.5 plus antialiasing).
            dl.Rect(fit.X(TabCentre(ActiveTab) - SelectorWidth * 0.5f), fit.Y(SelectorTop),
                    fit.S(SelectorWidth), fit.S(SelectorHeight), ink);
        }
    }
}
