/*
 * DragonScreen - Strokes
 *
 * PURE. One rule for turning a DESIGN-frame stroke width into whole device pixels.
 *
 * ---- WHY THIS IS ONE FUNCTION AND NOT TWELVE (QC R-02 family, 2026-09-06) ----
 * Twelve Figma-era pages each carried their own copy of the same local lambda:
 *
 *     int St(float rs) { int p = (int)Math.Round(rs * sc); return p < 1 ? 1 : p; }
 *
 * Identical, character for character, in CoverPage, DockingSimPage, ManualChuteDeployPage, MenuPage,
 * NavOrbitPlotPage, PlaceholderPage, RendezvousPage, SettingsAudioPage, SettingsVideoPage,
 * SuitCheckPage, VehicleMechPage and VrioTestPage - 39 call sites between them. MarginAffordance's own
 * header records what happens next: the same rectangle written out three times in two files, and
 * "every copy disagreed with at least one other". A rule with twelve homes has no home.
 *
 * ---- WHY IT ROUNDS UP AND NOT TO NEAREST, WHICH IS THE DEFECT THIS FIXES ----
 * A stroke is snapped to a WHOLE device pixel on purpose: the renderers antialias, and a 1.33 px rule
 * comes out as a grey smear rather than a line. That part was always right.
 *
 * Rounding to NEAREST was not. The design frame is 3427 x 2112 and sc = panelH / 2112, so a 2 px
 * design rule wants:
 *
 *      panel        rs * sc     Round (was)   Ceiling (now)    as a fraction of the panel
 *      1280 x 703   0.666       1             1                1/1280 = 0.078%
 *      2560 x 1406  1.331       1             2                2/2560 = 0.078%
 *
 * Round gave ONE device pixel at both widths - so when S115 doubled the shipped screenWidth on
 * 2026-09-05, every 2 px rule in the design became PHYSICALLY HALF AS THICK to the crew, on a screen
 * that had not moved and eyes that had not moved. That is exactly QC R-02's shape, in strokes instead
 * of type: a device-pixel quantity that did not follow the panel. QC measured it independently while
 * answering S101.
 *
 * Ceiling makes the thickness a constant fraction of the panel for every width in use (St(2), St(3)
 * and St(5) are all exactly proportional between 1280 and 2560), it errs THICK rather than thin -
 * which is the safe direction for a hairline that S101 found dropping out - and at 1280 it returns
 * the same integer Round did for every argument the build actually uses, so nothing moves at the
 * reference width.
 *
 * ⛔ St(1) IS AT THE FLOOR AND CANNOT BE PROPORTIONAL. A 1 px design rule is 0.33 device px at 1280
 * and 0.67 at 2560; both round up to the 1 px minimum a renderer can draw, so it is 3x too thick at
 * 1280 and 1.5x too thick at 2560, and it does halve physically between them. That is not fixable
 * here - it needs a panel wider than the 3427 px design frame - and it is stated rather than hidden.
 */
using System;

namespace DragonScreen
{
    public static class Strokes
    {
        /// <summary>
        /// A design-frame stroke width in WHOLE device pixels, never thinner than one, never thinner
        /// than the design asks for in proportion.
        /// </summary>
        /// <param name="refPx">the width in design-frame pixels, e.g. 2 for the design's hairline</param>
        /// <param name="sc">the page's design-to-panel scale, panelH / RefH</param>
        public static int Px(float refPx, float sc)
        {
            int p = (int)Math.Ceiling(refPx * sc);
            return p < 1 ? 1 : p;
        }
    }
}
