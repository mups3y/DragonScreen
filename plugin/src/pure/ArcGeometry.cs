/*
 * DragonScreen - ArcGeometry
 *
 * PURE. No KSP, no Unity. Vertex maths for the arc gauges - the element that made the kOS route
 * impossible in the first place (kOS GUI can only place label/button/box widgets, so a swept arc means
 * one pre-rendered PNG per value, which is where the "143 images" estimate came from).
 *
 * ---- PORTED FROM MAS, NOT INVENTED ----
 * MASPageEllipse.cs:441-446 is the whole of it:
 *      vertices[i].x = radiusX * cosTheta;
 *      vertices[i].y = radiusY * sinTheta;
 * MIT licensed, source at assets/reference/AvionicsSystems-master/.
 *
 * What does NOT port is the material: MAS builds one from
 * `MASLoader.shaders["MOARdV/Monitor"]`, which lives in MAS's own AssetBundle. We do not ship that
 * bundle, so the glue side must substitute a stock shader. Noting it here because "copy the line, not
 * the reason" is how this project keeps losing flights - the maths is portable, the material is not.
 *
 * ---- WHY THIS IS THE PURE HALF ----
 * F9_LOP's LopGeometry was validated 37/37 headless while its KSP glue, which had no tests, is where
 * every failed flight came from. Same split: every number that decides where a pixel lands is computed
 * here and tested with the game closed. The glue only uploads the result.
 *
 * ---- ANGLE CONVENTION, STATED ONCE ----
 * Degrees, ZERO AT TWELVE O'CLOCK, INCREASING CLOCKWISE. That is how an instrument is described
 * ("sweeps from -120 to +120"), and it is NOT the maths convention (0 at +X, counter-clockwise), so
 * the conversion happens in exactly one place - ToMathRadians - and nowhere else.
 */
using System;

namespace DragonScreen
{
    public static class ArcGeometry
    {
        /// <summary>
        /// Instrument degrees (0 = up, clockwise) to maths radians (0 = +X, counter-clockwise).
        /// The single point of conversion. If an arc ever comes out mirrored or 90 degrees out, the
        /// bug is here or in the caller's convention - not in the trig below.
        /// </summary>
        public static double ToMathRadians(double instrumentDegrees)
        {
            return (90.0 - instrumentDegrees) * Math.PI / 180.0;
        }

        /// <summary>
        /// Vertex count for an arc. Segments are chosen from the ANGLE SWEPT, not a fixed number:
        /// a 20-degree tick and a 300-degree gauge drawn with the same count give visibly different
        /// smoothness, and the big one is the one that shows facets.
        /// Clamped to a floor of 2 (a straight chord is still a valid degenerate arc) and a ceiling
        /// that stops a malformed sweep allocating an enormous buffer.
        /// </summary>
        public static int VertexCount(double sweepDegrees, double degreesPerSegment)
        {
            if (degreesPerSegment <= 0.0) degreesPerSegment = 3.0;
            double sweep = Math.Abs(sweepDegrees);
            int segments = (int)Math.Ceiling(sweep / degreesPerSegment);
            if (segments < 1) segments = 1;
            if (segments > 1024) segments = 1024;
            return segments + 1;   // N segments need N+1 points
        }

        /// <summary>
        /// Fill <paramref name="outXY"/> with an arc: x,y interleaved, centred on (cx,cy).
        /// Writes exactly 2*count floats and returns the number of POINTS written.
        ///
        /// The caller supplies the buffer because this runs in the draw path: allocating here would be
        /// the same per-frame-allocation trap already caught three times in OnGUI. Build the buffer
        /// once per element, refill it when the value changes.
        /// </summary>
        public static int Arc(float[] outXY, int count,
                              double cx, double cy, double radiusX, double radiusY,
                              double startDeg, double endDeg)
        {
            if (outXY == null || count < 2) return 0;
            if (outXY.Length < count * 2) return 0;

            // count-1 intervals, so the LAST point lands exactly on endDeg. Dividing by `count`
            // instead leaves the arc a fraction short - invisible on a 300 degree sweep, glaring
            // where two arcs are meant to meet.
            double step = (endDeg - startDeg) / (count - 1);

            for (int i = 0; i < count; i++)
            {
                double theta = ToMathRadians(startDeg + step * i);
                outXY[i * 2]     = (float)(cx + radiusX * Math.Cos(theta));
                outXY[i * 2 + 1] = (float)(cy + radiusY * Math.Sin(theta));
            }
            return count;
        }

        /// <summary>
        /// Arc in SCREEN space - top-left origin, y increasing DOWNWARD - as a circle, not an ellipse.
        ///
        /// THE Y NEGATION LIVES HERE AND NOWHERE ELSE. Arc() works in maths convention with y up;
        /// every render target in this project has y down. Flipping the sign of the y radius mirrors
        /// the arc back so that "0 degrees is twelve o'clock, increasing clockwise" is true on screen,
        /// which is what this file's header promises the caller.
        ///
        /// It matters that this is one function: there are TWO renderers (GL in game, System.Drawing
        /// for the PNG preview) and they must not each carry their own copy of a sign flip. A preview
        /// that disagreed with the screen would be worse than no preview at all.
        /// </summary>
        public static int ArcScreen(float[] outXY, int count,
                                    double cx, double cy, double radius,
                                    double startDeg, double endDeg)
        {
            return Arc(outXY, count, cx, cy, radius, -radius, startDeg, endDeg);
        }

        /// <summary>
        /// Map a gauge reading onto its sweep. Returns the instrument angle for <paramref name="value"/>.
        ///
        /// Clamps to the ends deliberately. A gauge whose needle leaves the dial is worse than one
        /// that pegs: pegged reads as "at or beyond the limit", which is true, while a needle drawn
        /// 40 degrees off the scale reads as a different value entirely. Out-of-range is normal here -
        /// the entry phase overshoots every scale we would draw for it.
        /// </summary>
        public static double ValueToAngle(double value, double min, double max,
                                          double startDeg, double endDeg)
        {
            // Degenerate range: no sane needle position exists, so park it at the start rather than
            // dividing by zero and drawing a NaN (which Unity renders as a vertex at the origin -
            // a spike across the whole gauge, and a genuinely confusing thing to debug).
            if (max == min) return startDeg;

            double t = (value - min) / (max - min);
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
            return startDeg + t * (endDeg - startDeg);
        }

        /// <summary>
        /// True when the reading is outside the dial, INDEPENDENT of the clamping above.
        /// The clamp keeps the needle on the face; this tells the page it should also colour the arc
        /// as off-scale. Without it, pegged and at-the-limit look identical - and only one of them is
        /// a reason to look at the number instead of the picture.
        /// </summary>
        public static bool IsOffScale(double value, double min, double max)
        {
            double lo = Math.Min(min, max);
            double hi = Math.Max(min, max);
            return value < lo || value > hi;
        }
    }
}
