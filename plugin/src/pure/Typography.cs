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
 *
 * ---- THE FLOOR IS A RATIO, NOT A PIXEL COUNT (R-02, 2026-09-06) ----
 * ⛔ ADDED, NOT A REWRITE. Everything above this line is the ORIGINAL text, recovered VERBATIM
 * from commit 14b8c2a. It was deleted at 158eb2a (the ground-up autopilot rebuild), which took this
 * file from 55 lines to 20 and left the two headings above standing over nothing for ten days. Read
 * what follows as an amendment to it: the measurement above is still the measurement, and it is the
 * whole reason for this.
 *
 * READ THE PREMISE AGAIN: "at 1280 px across a screen 0.2844 m wide, seen from the seat". The 16 is
 * a count of RENDER-TARGET PIXELS, and it is the glanceable threshold only while the render target
 * is 1280 px across that same 0.2844 m of glass. What was measured is an ANGLE. The pixel count is
 * how the angle was written down.
 *
 * On 2026-09-05 the shipped screenWidth went 1280 -> 2560 (Q5 / S115). The glass did not move and
 * the crew's eyes did not move, so the measured angle is unchanged - but every comparison in the
 * build still read 16, and 16 px on a 2560-wide render is HALF the angle it was measured as. Every
 * legibility check in the build silently became twice as permissive on the day the cfg changed. Two
 * tasks then computed a fix for QC C-05 against that halved floor and called it safe (S112, S115;
 * unwound by the 2026-09-06 batch, job 1). Nobody could check the constant against its own premise,
 * because the premise had been deleted out of this file.
 *
 * So the floor is expressed as a RATIO to the width it was measured at and resolved at the point of
 * comparison: MinFor(panelW). Min stays exactly the number that was measured - it is the value AT
 * RefPanelW, and it is what MinFor returns there.
 *
 * ⛔ DO NOT "FIX" THIS BY RETYPING 16 AS 32. That is right for today's cfg and wrong for the next
 * one, and it throws the measurement away a second time: a bare constant cannot be checked against a
 * premise it does not carry. With the ratio form, a width change needs no edit here at all.
 *
 * ⛔ AND RAISING THE RESOLUTION DOES NOT MAKE TEXT MORE LEGIBLE. It buys crispness - more texture
 * pixels per glyph - and nothing else. Anything drawn as a FRACTION of the panel subtends the same
 * angle in the seat at any width, so every "too small to read" finding survives a width change
 * unchanged; only its pixel figures move (QC R-01, verified at both widths).
 */
namespace DragonScreen
{
    public static class Typography
    {
        /// <summary>
        /// The width every size in this class was MEASURED at: 1280 render-target px across the
        /// 0.2844 m screen, from the seat (see the header). Read each constant below as "px at this
        /// width"; ScaleFor / MinFor turn that into px at the width actually being rendered.
        ///
        /// ⛔ NOT the shipped width, and it must never be edited to follow it. The shipped width is
        /// screenWidth in plugin/GameData/DragonScreen/DragonScreen.cfg (2560 since S115) and is free
        /// to change. This is the width the eye test was done at, which is history and does not.
        /// </summary>
        public const float RefPanelW = 1280f;

        /// <summary>
        /// The scale from a size in this class to panel pixels, on a panel panelW device px wide.
        /// 1.0 at RefPanelW, 2.0 at 2560. Multiply any Typography.* size by this to draw it at the
        /// physical size it was measured at, whatever the render target is.
        /// </summary>
        public static float ScaleFor(float panelW)
        {
            return (panelW > 0f ? panelW : RefPanelW) / RefPanelW;
        }

        /// <summary>
        /// The glanceable floor IN PANEL PIXELS, on a panel panelW device px wide - the honest form of
        /// Min, and the one every legibility comparison should be written against.
        ///
        /// 16 at 1280, 32 at 2560, and the same ANGLE in the seat at both, which is what was actually
        /// measured. A comparison written against Min instead is measuring against the 1280 floor on
        /// whatever panel it is running on: that is R-02, and it is how C-05's fix came to be computed
        /// at half size twice and recorded as safe.
        /// </summary>
        public static float MinFor(float panelW)
        {
            return Min * ScaleFor(panelW);
        }

        /// <summary>
        /// The floor for anything that must be read at a glance. MEASURED. Values, alerts, the nav
        /// bar, anything that changes, anything that matters in a hurry.
        /// </summary>
        /// <remarks>
        /// ⛔ THIS IS THE VALUE AT RefPanelW - it is not "the floor" on an arbitrary panel. Comparing
        /// a rendered panel-pixel size against it is correct only when the panel IS 1280 wide;
        /// anywhere else the comparison belongs against MinFor(panelW). Kept as a named constant
        /// because it is the measured number, and MinFor is derived from it.
        /// </remarks>
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
