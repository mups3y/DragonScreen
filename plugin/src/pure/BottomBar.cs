// DragonScreen — BottomBar  (PURE: component_48, the persistent bottom-bar navigation)
// ============================================================================================
// ONE SOURCE OF TRUTH FOR THE BAR: its rectangle, its draw, its hit map and its active-tab marker.
// Every one of the 21 pages that shows the bar calls Draw here; FigmaUI's HitTest calls Hit here;
// FigmaUI's per-page marker calls Marker here. That is MenuPage.CellRect's rule — "the one source of
// truth Build, HitTest and the headless nav test all share, so the drawn grid and the hit grid can
// never drift apart" — applied to the one control the crew can always rely on.
//
// ---- WHY THIS FILE EXISTS (S103 / QC batch 1: C-04 + H-07) ----
// The bar used to be drawn by each page as `dl.Asset("component_48", 0, Y(1877), w, Z(235))` — 21
// sites, all of them FULL PANEL WIDTH against a HEIGHT-derived scale. At the shipped 1280x703 that is
// x-scale 0.3735 against y-scale 0.3329: the asset was STRETCHED 12.2% HORIZONTALLY on every page. Its
// crosshair icon is exactly 130x130 in the PNG and rendered 23x21; every glyph and every word baked
// into the bar was 12% wide (QC C-04). And because the frame pages draw their art fit-to-height at
// `ox` while the bar was drawn 0..w, the frame's own border became a vertical rule crossing the bar and
// the design's single rounded corner became two, ~50px apart (QC H-07).
//
// The bar is drawn UNIFORMLY now, in the design frame's own coordinates: `ox .. ox + RefW*sc`, the
// same fit-to-height box the letterboxed pages already draw their art in. That is not a compromise, it
// is where the asset belongs — component_48 carries the design frame's OWN bottom border (2px white at
// its rows 105-106 and 233-234) and its own left/right edges, so drawing it anywhere but the design
// frame put a page border in the middle of a page.
//
// ---- WHAT THE STRIPS EITHER SIDE ARE, AND WHY THEY ARE NOT PAINTED ----
// A panel wider than the design aspect leaves `ox` of page ground at each end. On the eleven
// letterboxed pages that IS the letterbox and it is already page ground, so the bar now ends exactly
// where the page art does — which is H-07's fix. On the ten pages that spread x across the full width
// the strips are new, and they are deliberately left as page ground rather than filled: filling them
// would put component_48's own left/right border in the MIDDLE of a filled bar, which is the defect
// this file removes, one step to the right. See docs/QC_FINDINGS.md, batch 1.
//
// ⛔ THE HIT MAP AND THE MARKER MOVE WITH THE DRAW OR NOT AT ALL. FigmaUI.HitTest tests this bar
// FIRST, before any page control, because it is the one touch the crew can always rely on. It used to
// map a touch by `BarIconX[i] / RefW * w` — the stretched mapping — which agreed with the stretched
// draw. Changing one without the other slides every nav icon's touch target off its icon on all 35
// pages, silently. They are in this file, together, for that reason.
// ============================================================================================
namespace DragonScreen
{
    public static class BottomBar
    {
        const float RefW = 3427f, RefH = 2112f;

        /// <summary>The bar's own box in the design frame: full width, 235 tall, at design y 1877.</summary>
        const float BarY = 1877f, BarH = 235f;

        // The five icons are baked into component_48.png at these design x's (each 80 wide, at design
        // y 2003 = the bar top 1877 + the icon's local y 126). Left-to-right: compass, target, rocket,
        // folder, gear. FigmaUI.BarTarget maps icon N to the page it opens; the routing is its, the
        // geometry is this file's.
        public static readonly float[] IconX = { 46f, 174f, 302f, 430f, 558f };
        public const float IconY = 2003f, IconS = 80f;

        // The active-tab marker was baked under the FIRST icon in component_48.png and erased there so
        // it can be drawn dynamically under whichever tab is active. These are the erased block's own
        // component_48 coordinates: a thin white line just above the bar's bottom edge.
        // ⚠ The erased BLOCK is deliberately larger than the marker drawn into it (S103 finished that
        // erase — the original left the pill's glow behind, so every page carried a ghost marker under
        // icon 0 whatever tab was really active; QC C-12). Do not shrink the block to fit these.
        const float MarkY = BarY + 223f, MarkH = 10f, MarkW = 108f;

        /// <summary>
        /// The bar's rectangle in panel pixels: the design frame's own bottom bar, fit to height and
        /// centred. THE one geometry — Draw, Hit and Marker all read it, and so does the headless test.
        /// </summary>
        public static void Rect(int w, int h, out float x, out float y, out float bw, out float bh)
        {
            x = y = bw = bh = 0f;
            if (w <= 0 || h <= 0) return;
            float sc = h / RefH;
            bw = RefW * sc;
            bh = BarH * sc;
            y = BarY * sc;
            x = (w - bw) * 0.5f;
            // ⛔ NOT CLAMPED TO THE PANEL, DELIBERATELY, and this was got wrong once already.
            // A panel TALLER than the design aspect (w < RefW*sc) makes `x` negative and the bar hangs
            // off both ends. Clamping it to the panel width was tried and is wrong twice over: it
            // re-introduces the very distortion this file exists to remove (bw would shrink while bh
            // did not - measured 0.2918 against 0.3788 at 1000x800), and it would put the bar in a
            // DIFFERENT frame from the page art, which is H-07 all over again. Every page here is
            // fit-to-height and lets its own art overflow at that aspect; the bar overflows WITH it.
            // The shipped screens are 1280x703/710 - aspect 1.82 against the design's 1.623 - so `x`
            // is always positive in the build. See FigmaUINavTest.BottomBarUndistorted.
        }

        /// <summary>Draw the bar, undistorted, where the design puts it.</summary>
        public static void Draw(DisplayList dl, int w, int h)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float x, y, bw, bh;
            Rect(w, h, out x, out y, out bw, out bh);
            dl.Asset("component_48", x, y, bw, bh, DragonPalette.White);
        }

        /// <summary>Which bottom-bar icon (0..4) a touch hit, or -1. Present on every page.</summary>
        public static int Hit(float px, float py, int w, int h)
        {
            float bx, by, bw, bh;
            Rect(w, h, out bx, out by, out bw, out bh);
            if (bw <= 0f) return -1;
            float k = bw / RefW;                       // the bar's own uniform scale
            float y0 = by + (IconY - BarY) * k, y1 = y0 + IconS * k;
            if (py < y0 - 12f || py > y1 + 12f) return -1;
            for (int i = 0; i < IconX.Length; i++)
            {
                // Padding kept under half the icon PITCH so neighbours never share a hit region: the
                // pitch is 128 design px and the icon 80, so the 48-px design gap absorbs 6px a side
                // at any scale this bar is drawn at.
                float x0 = bx + IconX[i] * k, x1 = x0 + IconS * k;
                if (px >= x0 - 6f && px < x1 + 6f) return i;
            }
            return -1;
        }

        /// <summary>Slide the bar's white marker under the active tab (the reference App.vue's
        /// `.marker`). `icon` is an index into <see cref="IconX"/>; out-of-range draws nothing.</summary>
        public static void Marker(DisplayList dl, int w, int h, int icon)
        {
            if (dl == null || icon < 0 || icon >= IconX.Length) return;
            float bx, by, bw, bh;
            Rect(w, h, out bx, out by, out bw, out bh);
            if (bw <= 0f) return;
            float k = bw / RefW;
            float mw = MarkW * k;
            float cx = bx + (IconX[icon] + IconS * 0.5f) * k;
            dl.Rect(cx - mw * 0.5f, by + (MarkY - BarY) * k, mw, MarkH * k, DragonPalette.White);
        }
    }
}
