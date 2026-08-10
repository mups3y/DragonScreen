/*
 * DragonScreen - ScreenLayout
 *
 * PURE. No KSP, no Unity, no engine types of any kind. Everything in src/pure must stay that way so
 * it can be built and tested headless by build.py, with the game closed.
 *
 * That split is not a style preference - it is the lesson F9_LOP paid for in test flights:
 *     "THE BUG IS IN THE GLUE, AND THE GLUE IS THE PART WITH NO TESTS."
 * LopGeometry was pure and passed 37/37 headless; F9LopModule needed KSP types, was never tested, and
 * is where every failed flight died. So: arithmetic lives here, KSP calls live in src/.
 */
using System;

namespace DragonScreen
{
    /// <summary>
    /// Maps the Figma reference frames onto the actual on-screen panel.
    ///
    /// THE PROBLEM THIS EXISTS TO SOLVE. The reference art is 3427 x 2112 with 2 px hairlines. The
    /// panel is ~670 px wide. Scale a 2 px stroke by 670/3427 and you get 0.39 px - which a GPU
    /// renders as a smeared grey line, not a crisp one. Every hairline in the design would turn to
    /// mush, and that is most of what makes the Dragon screen look like the Dragon screen.
    ///
    /// So strokes are NOT scaled with the layout. Positions scale; stroke widths snap to whole
    /// device pixels with a floor of 1.
    /// </summary>
    public static class ScreenLayout
    {
        /// <summary>Width of the Figma reference frames, in their own units.</summary>
        public const float RefWidth = 3427f;
        /// <summary>Height of the Figma reference frames, in their own units.</summary>
        public const float RefHeight = 2112f;

        /// <summary>Hairline weight used throughout the reference art, in reference units.</summary>
        public const float RefStroke = 2f;

        /// <summary>
        /// Aspect of the reference art, width / height. 3427 / 2112 = 1.6226 - LANDSCAPE.
        /// The real Crew Dragon displays are wide, not tall, and the panel has to be the same shape or
        /// nothing composed for the reference will sit right in it.
        /// </summary>
        public const float RefAspect = RefWidth / RefHeight;

        /// <summary>
        /// Design width of the OUT-OF-VEHICLE window - the fourth surface, not an IVA screen.
        /// Half of a 1920-wide display, which leaves the flight view usable while giving the pages
        /// enough room to be read at a glance.
        ///
        /// (This said "the F9I panel" until 2026-08-05, which was true when it was written and is
        /// now actively misleading: DragonScreen is a separate project and nothing here talks to F9I
        /// or to kOS. The number is still the right number; only the reason had rotted.)
        /// </summary>
        public const float PanelWidth = 960f;

        /// <summary>
        /// Design height - DERIVED, never typed in.
        ///
        /// An earlier version declared width 670 and height 870 as two independent constants, which
        /// made the panel 0.77:1 PORTRAIT against a reference that is 1.62:1 LANDSCAPE - very nearly
        /// inverted. Two hand-written numbers cannot be kept in step by discipline; deriving one from
        /// the other means the aspect is correct by construction and a future size change cannot
        /// silently break it.
        /// </summary>
        public static float PanelHeight { get { return PanelWidth / RefAspect; } }

        /// <summary>
        /// Uniform scale from reference units to panel pixels for a given panel width.
        /// Uniform on purpose: a non-uniform fit would stretch the ring gauges into ellipses. Now that
        /// the panel carries the reference aspect exactly, a uniform scale maps the whole frame with
        /// no letterboxing and no distortion - which is the point of deriving the height.
        /// </summary>
        public static float Scale(float panelWidth)
        {
            if (panelWidth <= 0f) return 0f;
            return panelWidth / RefWidth;
        }

        /// <summary>
        /// Convert a reference-space length to panel pixels. For POSITIONS and SIZES only.
        /// Do not use it for stroke widths - see StrokePx.
        /// </summary>
        public static float ToPanel(float refUnits, float panelWidth)
        {
            return refUnits * Scale(panelWidth);
        }

        /// <summary>
        /// Stroke width in whole device pixels, never thinner than 1.
        ///
        /// This is the function that keeps the design crisp. A hairline must land on a whole pixel or
        /// it blurs across two, and at our scale the naive result is 0.39 px. Rounding to nearest with
        /// a floor of 1 gives a real, sharp line at every panel size we might use.
        /// </summary>
        public static int StrokePx(float refStroke, float panelWidth)
        {
            float px = refStroke * Scale(panelWidth);
            int snapped = (int)Math.Round((double)px, MidpointRounding.AwayFromZero);
            return snapped < 1 ? 1 : snapped;
        }

        /// <summary>
        /// Width/height aspect of a FLAT quad, given the three extents of its bounding box.
        ///
        /// A screen mesh is flat, so one of its three extents is ~0 and the other two are the ones
        /// that matter. Which axis is which depends entirely on how the artist built the model, so
        /// this does not care: it discards the smallest extent and returns largest/middle.
        ///
        /// Returning largest-over-middle - never the other way up - means the answer is always >= 1,
        /// i.e. LANDSCAPE. That is correct for these displays and is safe because the render target's
        /// U axis is known to run along the long side (proven in game 2026-08-05: the proof pattern
        /// came out upright, not rotated a quarter turn). If a portrait screen ever needs driving,
        /// this function is where that assumption has to be revisited, not the caller.
        ///
        /// Returns 0 for a degenerate box, which the caller must treat as "could not measure".
        /// </summary>
        public static float AspectFromExtents(float x, float y, float z)
        {
            float a = Math.Abs(x), b = Math.Abs(y), c = Math.Abs(z);
            float largest  = (a > b) ? ((a > c) ? a : c) : ((b > c) ? b : c);
            float smallest = (a < b) ? ((a < c) ? a : c) : ((b < c) ? b : c);
            float middle   = a + b + c - largest - smallest;
            if (middle <= 0f) return 0f;
            return largest / middle;
        }

        /// <summary>
        /// Render-target height for a given width and aspect. WIDTH IS CHOSEN, HEIGHT IS DERIVED.
        ///
        /// Same rule as PanelHeight above, and for the same reason it was written: typing width and
        /// height as two independent numbers produced a PORTRAIT panel against a LANDSCAPE reference
        /// and nobody noticed. Here the cost of getting it wrong is subtler and worse - the whole page
        /// is silently stretched, so every circle is an ellipse and every square a rectangle. That is
        /// exactly what 1024x640 did on screens that are nearer 2:1, caught in game on the first look.
        ///
        /// Returns 0 for unusable inputs so the caller can fall back rather than create a 1 px texture.
        /// </summary>
        public static int PixelHeightFor(int width, float aspect)
        {
            if (width <= 0 || aspect <= 0f) return 0;
            int h = (int)Math.Round(width / (double)aspect, MidpointRounding.AwayFromZero);
            return (h < 1) ? 0 : h;
        }

        /// <summary>
        /// Snap a coordinate to a pixel boundary appropriate for the given stroke width.
        /// Odd strokes straddle a pixel centre, even strokes sit on a pixel edge - getting this wrong
        /// is the other half of why thin lines look soft.
        /// </summary>
        public static float SnapToPixel(float coord, int strokePx)
        {
            float f = (strokePx % 2 == 1) ? 0.5f : 0f;
            return (float)Math.Floor((double)coord) + f;
        }
    }
}
