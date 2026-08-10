/*
 * DragonScreen - Typography
 *
 * PURE. The type scale, in render-target pixels.
 *
 * ---- 16 PX IS MEASURED, NOT CHOSEN ----
 * Established in game 2026-08-05 from the proof page's legibility ramp, at 1280 px across a screen
 * 0.2844 m wide, seen from the seat:
 *
 *     "technically every single one is visible from the seat as you can use the mouse wheel to zoom
 *      view. 16px is legible without needing to zoom"
 *
 * Both halves of that matter. Zoom means nothing is UNREADABLE, so there is no hard floor - but
 * text the pilot has to zoom for is text they will not read during an approach. The threshold that
 * counts is GLANCEABLE, and it is 16.
 *
 * ---- THE RULE THAT FALLS OUT OF IT ----
 * NEVER SHRINK BELOW 16 PX TO CREATE HIERARCHY. GO DIMMER INSTEAD.
 *
 * That is not a compromise, it is what the source art already does: DragonPalette's nine-step text
 * ladder exists, in its own words, to carry "the hierarchy that would otherwise need weight or size
 * changes". Caption-versus-value on this design is a BRIGHTNESS difference far more than a size one.
 * Reaching for a smaller size is the instinct to resist.
 */
namespace DragonScreen
{
    public static class Typography
    {
        /// <summary>
        /// The floor for anything that must be read at a glance. MEASURED. Values, alerts, the nav
        /// bar, anything that changes, anything that matters in a hurry.
        /// </summary>
        public const float Min = 16f;

        /// <summary>Static labels and captions. At the floor deliberately - see the rule above.</summary>
        public const float Caption = 16f;

        /// <summary>Ordinary readable content.</summary>
        public const float Body = 20f;

        /// <summary>A value the pilot is actively watching.</summary>
        public const float Value = 28f;

        /// <summary>The one number a page is about. Use once per page, or it stops meaning anything.</summary>
        public const float Hero = 40f;

        /// <summary>
        /// Dense reference detail, BELOW the glanceable floor and legal only because zoom exists.
        ///
        /// Permitted for a table someone leans in to read. NOT for any live value, any alert, or
        /// anything on the nav bar. If it would be a problem to miss it, it is not this size.
        /// </summary>
        public const float Dense = 12f;
    }
}
