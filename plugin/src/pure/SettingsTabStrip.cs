// DragonScreen — SettingsTabStrip  (PURE: the Audio / Cabin / Video strip, and where it is hit)
// ============================================================================================
// [[S134a]] / QC `F-04`, 2026-09-06.
//
// ---- ⛔ THE DEFECT, MEASURED ----
// Three pages share one tab strip and one hit block. They do NOT share a projection:
//   Audio, Video  draw it in code at `x * (w / RefW)` — the panel is STRETCHED to the design frame.
//   Cabin         does not draw it at all: it is baked into `frame66.png`, which `FigmaFramePage`
//                 places LETTERBOXED at `ox + x * sc`, `sc = h / RefH`.
// The hit test used the stretched mapping for all three. So on Cabin a tab painted at design x *d* was
// hit-tested as if it were at `(ox + d·sc)·RefW/w`. At the shipped 2560×1406 that is:
//
//     drawn 1585 -> hit-tested 1599.0   (+14.0)
//     drawn 1716 -> hit-tested 1715.7   (−0.3)
//     drawn 1846 -> hit-tested 1831.6   (−14.4)
//
// ⭐ AND IT DOES NOT MISS TODAY. The bands are 130 px wide, so every tab still lands inside its own with
// ~50 px to spare; sweeping the width shows the first actual miss at **w = 4416, a 3.14:1 panel**. This
// is [[H-04]]'s class of bug — one rectangle drawn, another hit-tested — caught before it bit, which is
// the only time it is cheap to fix.
//
// ---- ⭐ THE FIX IS ONE GEOMETRY IN DESIGN SPACE, WITH EACH PAGE'S OWN PROJECTION ----
// QC's plan was to draw the strip once in code for all three and suppress Cabin's baked one. ⚠ THAT WAS
// TRIED AND THE RASTER SAYS NO: an ink profile of `frame66.png` shows the strip embedded in a panel
// whose surround is NOT flat background — the band above (y 1840-1858) and below (1980-2008) is solid
// artwork, and the columns either side of the labels carry more of it. A patch big enough to hide the
// baked strip would erase the frame's own drawing, and `FigmaFramePage` has no skip mechanism because
// the frame is a single PNG.
//
// So the rule is honoured the other way round, which is better: the tab boxes live HERE, once, in
// design coordinates, and each page applies the projection it actually draws in — to the drawing AND to
// the hit test. Nothing is patched, no pixel moves, and the two can no longer disagree because neither
// owns the geometry any more.
//
// ⚠ WHAT THIS DOES NOT FIX, said plainly: F-04's second complaint is that the two strips do not LOOK
// alike — Audio's is text with an accent underline, Cabin's is baked icons above labels. That is a
// look, not a correctness defect, and unifying it needs the element rebuild F-02 asks for. Recorded on
// [[S134a]] as the half that remains.
// ============================================================================================
namespace DragonScreen
{
    public static class SettingsTabStrip
    {
        public const float RefW = 3427f, RefH = 2112f;

        /// <summary>The three tabs, in the order they are drawn and hit.</summary>
        public static readonly string[] Labels = { "Audio", "Cabin", "Video" };

        /// <summary>Where each tab goes. ⚠ `AudioVideo` IS the Video page — the enum name is older than
        /// the page's title.</summary>
        public static readonly UiPage[] Targets = { UiPage.Audio, UiPage.Cabin, UiPage.AudioVideo };

        /// <summary>Label centres in the design frame. ⭐ These are BOTH pages' numbers: `SettingsAudio`
        /// and `SettingsVideo` drew at 1584 / 1714 / 1843, and an ink profile of `frame66.png` puts the
        /// baked clusters' peaks at 1584 / 1716 / 1844. The baked strip and the drawn one were always
        /// the same design geometry — only the projection differed.</summary>
        public static readonly float[] Cx = { 1584f, 1714f, 1843f };

        /// <summary>The hit bands, in design x. ⛔ KEPT EXACTLY as `FigmaUI` had them so no page's
        /// behaviour changes at the shipped aspect — this line fixes WHICH SPACE they are measured in,
        /// not where they are. Widening them would hide the very defect being fixed.</summary>
        public static readonly float[] X0 = { 1520f, 1652f, 1782f };
        public static readonly float[] X1 = { 1650f, 1780f, 1910f };
        public const float Y0 = 1890f, Y1 = 2000f;

        /// <summary>Label baseline, its size, and the active tab's underline — the drawn form's own
        /// numbers, lifted verbatim from `SettingsAudioPage` so its render does not move.</summary>
        public const float LabelY = 1921f, LabelSize = 28f;
        public const float UnderY = 1974f, UnderW = 120f, UnderH = 8f;
        public static readonly float[] UnderX = { 1524f, 1654f, 1783f };

        public const int Commands = 4;

        /// <summary>
        /// Which tab a touch fell on, or −1.
        ///
        /// ⛔ `letterboxed` IS NOT OPTIONAL, and that is the whole of this line. A page that draws its
        /// strip STRETCHED must be hit stretched; a page that draws it LETTERBOXED must be hit
        /// letterboxed. Getting it wrong is silent — every tab still lands inside its band at the
        /// shipped aspect — so the parameter is required rather than defaulted, the same fail-closed
        /// move [[S120]], [[S124]] and [[S121a]] each made for the same reason.
        /// </summary>
        public static int HitTest(float px, float py, int w, int h, bool letterboxed)
        {
            if (w <= 0 || h <= 0) return -1;
            float dx, dy;
            if (letterboxed)
            {
                float sc = h / RefH;
                if (sc <= 0f) return -1;
                dx = (px - (w - RefW * sc) * 0.5f) / sc;
                dy = py / sc;
            }
            else
            {
                dx = px * RefW / w;
                dy = py * RefH / h;
            }
            if (dy < Y0 || dy >= Y1) return -1;
            for (int i = 0; i < Labels.Length; i++)
                if (dx >= X0[i] && dx < X1[i]) return i;
            return -1;
        }

        /// <summary>
        /// Draw the strip, STRETCHED — the form `SettingsAudioPage` and `SettingsVideoPage` use.
        /// ⚠ There is deliberately no letterboxed Draw: the one page that needs it draws its strip from
        /// the raster, and adding a second drawn form is how the two get to disagree again.
        /// </summary>
        public static void Draw(DisplayList dl, int w, int h, int active)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float sx = w / RefW, sy = h / RefH;
            for (int i = 0; i < Labels.Length; i++)
                dl.Text(Labels[i], Cx[i] * sx, LabelY * sy, LabelSize * sy, TextAlign.Centre,
                        (i == active) ? DragonPalette.White : DragonPalette.Text6);
            if (active >= 0 && active < Labels.Length)
                dl.Rect(UnderX[active] * sx, UnderY * sy, UnderW * sx, UnderH * sy, DragonPalette.Accent);
        }

        /// <summary>Which tab index a page IS, or −1 for a page that has no strip.</summary>
        public static int IndexOf(UiPage p)
        {
            for (int i = 0; i < Targets.Length; i++) if (Targets[i] == p) return i;
            return -1;
        }

        /// <summary>⭐ Does this page draw its strip LETTERBOXED? Exactly one does — Cabin, whose strip
        /// is baked into `frame66` and placed by `FigmaFramePage`. Asked as a function so the hit test
        /// and any future caller cannot answer it differently.</summary>
        public static bool IsLetterboxed(UiPage p) { return p == UiPage.Cabin; }
    }
}
