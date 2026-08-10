/*
 * DragonScreen - Gauge
 *
 * PURE. The ring gauge: a 3/4 dial with a track and a filled arc, a value in the middle and a caption
 * under it. The single most repeated element in the reference art, so it is a function, not something
 * each page draws its own version of.
 *
 * ---- GEOMETRY TAKEN FROM THE REFERENCE, NOT EYEBALLED ----
 * `assets/reference/dragon2-ui-vue/src/components/Overview.vue:36-66` builds its dials from a circle
 * of r=70 at cx=75,cy=75 with a clipPath circle at cx=0,cy=150,r=162.5 cutting a THREE-QUARTER dial.
 * Their trick is stroke-dasharray/dashoffset, so value -> arc length is one multiply with no trig.
 * That technique does not port to GL, but the SHAPE does: a 270 degree sweep, symmetric about
 * twelve o'clock, opening downward. Hence -135 to +135.
 *
 * The value->angle mapping goes through ArcGeometry.ValueToAngle, which is already pure and tested
 * and already clamps: a gauge whose needle leaves the dial is worse than one that pegs, because
 * pegged reads as "at or beyond the limit", which is true.
 */
namespace DragonScreen
{
    public static class Gauge
    {
        /// <summary>Dial start, instrument degrees (0 = twelve o'clock, clockwise).</summary>
        public const double StartDeg = -135.0;
        /// <summary>Dial end. 270 degrees of sweep, opening downward - the reference's shape.</summary>
        public const double EndDeg = 135.0;

        /// <summary>Commands one labelled gauge emits, for display-list sizing.</summary>
        public const int Commands = 7;

        /// <summary>
        /// Value text size for a ring of this radius.
        ///
        /// A FIXED size overflows a small dial: at radius 55 px, "2.86 psia" in 28 px text ran well
        /// outside the ring - caught in the preview. The number has to scale with the thing it sits
        /// inside, but never below the measured 16 px glanceable floor, and never above the Value
        /// step, because a gauge is not the one hero number on the page.
        /// </summary>
        public static float ValueSize(float radius)
        {
            float v = radius * 0.46f;
            if (v < Typography.Min) v = Typography.Min;
            if (v > Typography.Value) v = Typography.Value;
            return v;
        }

        /// <summary>
        /// Ring only: the full track, then the filled portion over it.
        ///
        /// The track is always drawn, even at zero. An empty ring says "this reads zero"; a missing
        /// ring says nothing at all, and the difference matters when the value being shown is
        /// propellant.
        /// </summary>
        public static void Ring(DisplayList dl, float cx, float cy, float radius, float thickness,
                                double value01, Rgba track, Rgba fill)
        {
            if (dl == null || radius <= 0f || thickness <= 0f) return;
            float rIn = radius - thickness;
            if (rIn < 0f) rIn = 0f;

            dl.ArcBand(cx, cy, rIn, radius, StartDeg, EndDeg, track);

            double end = ArcGeometry.ValueToAngle(value01, 0.0, 1.0, StartDeg, EndDeg);
            // Only draw the fill once it is wide enough to be a shape rather than a speck. Below
            // about a degree the arc is thinner than the anti-aliasing and reads as a dirty mark on
            // the track, which looks like a rendering fault rather than a low reading.
            if (end - StartDeg > 1.0)
                dl.ArcBand(cx, cy, rIn, radius, StartDeg, end, fill);
        }

        /// <summary>
        /// Ring, value in the middle, UNIT under the value, caption under the dial.
        ///
        /// Value and unit are separate because that is the reference's anatomy - a big number with a
        /// small unit beneath it - and because it is the only way a long reading fits a small dial.
        /// The unit is a page-level constant ("psia", "mmHg"), never formatted per frame.
        ///
        /// Caption BELOW the dial: it opens downward, so the gap is already there and putting the
        /// caption in it costs no vertical space.
        /// </summary>
        public static void Labelled(DisplayList dl, float cx, float cy, float radius, float thickness,
                                    double value01, string valueText, string unit, string caption,
                                    Rgba track, Rgba fill)
        {
            Ring(dl, cx, cy, radius, thickness, value01, track, fill);

            float vs = ValueSize(radius);
            bool hasUnit = !string.IsNullOrEmpty(unit);

            // With a unit the pair is centred as a block; without one the number alone is centred.
            float vy = hasUnit ? (cy - vs * 0.95f) : (cy - vs * 0.55f);
            dl.Text(valueText ?? "-", cx, vy, vs, TextAlign.Centre, DragonPalette.Text0);
            if (hasUnit)
                dl.Text(unit, cx, vy + vs * 1.05f, Typography.Caption, TextAlign.Centre,
                        DragonPalette.Text5);

            // ---- A LONG CAPTION WRAPS AT THE SPACE, IT DOES NOT RUN INTO THE DIAL ----
            // "PROPELLANT LF/OX" is wide enough to reach the arc's lower ends, which sit at
            // +/-135 degrees - i.e. exactly where a caption under an opening-downward dial goes.
            // Splitting at the LAST space puts the qualifier on its own line, and short captions
            // are untouched because they have nothing to split.
            string cap = caption ?? "";
            float capY = cy + radius * 0.62f;
            int split = (cap.Length > 12) ? cap.LastIndexOf(' ') : -1;
            if (split > 0)
            {
                dl.Text(cap.Substring(0, split), cx, capY, Typography.Caption, TextAlign.Centre,
                        DragonPalette.Text6);
                dl.Text(cap.Substring(split + 1), cx, capY + Typography.Caption + 2f,
                        Typography.Dense, TextAlign.Centre, DragonPalette.Text7);
            }
            else
            {
                dl.Text(cap, cx, capY, Typography.Caption, TextAlign.Centre, DragonPalette.Text6);
            }
        }

        /// <summary>
        /// A horizontal bar gauge: caption, a track FILLED TO A FRACTION, and the value to the right.
        ///
        /// The reference's right-hand column, and it really is filled - `#progress-horizontal-bar` in
        /// Overview.vue is a width-driven span on a translucent track, not the flat rule this project
        /// drew until 2026-08-06. A bar that never fills is just an underline, and it throws away the
        /// one thing a bar is for: showing where a value sits in its range at a glance.
        ///
        /// <paramref name="value01"/> below zero means "no range known" and draws the track only,
        /// which is honest for a reading whose scale genuinely depends on the mission.
        /// </summary>
        public static void Bar(DisplayList dl, float x, float y, float width,
                               string caption, string value, string unit, double value01, Rgba fill)
        {
            dl.Text(caption, x, y, Typography.Caption, TextAlign.Left, DragonPalette.Text6);
            dl.Text(value ?? "-", x + width, y, Typography.Body, TextAlign.Right,
                    DragonPalette.Text0);
            // The unit sits to the LEFT of the right-aligned value, dimmer, so the numbers stay in
            // one column and the eye can read down them without the units breaking the alignment.
            if (!string.IsNullOrEmpty(unit))
                dl.Text(unit, x + width - 56f, y + 2f, Typography.Caption, TextAlign.Right,
                        DragonPalette.Text6);

            const float BarH = 6f;
            float by = y + 28f;
            dl.Rect(x, by, width, BarH, DragonPalette.BarTrack);
            if (value01 > 0.0)
            {
                double f = (value01 > 1.0) ? 1.0 : value01;
                float fw = (float)(width * f);
                // Never narrower than the bar is tall: a 2 px sliver reads as a rendering fault
                // rather than as a low reading, the same trap the ring's one-degree floor guards.
                if (fw < BarH) fw = BarH;
                dl.Rect(x, by, fw, BarH, fill);
            }
        }

        // ---- WHERE LowIsBad / HighIsBad WENT ----
        // They used to live here and returned a THRESHOLD COLOUR that the dial was filled with. The
        // real screen does not do that: every dial has a fixed identity colour and alarm is routed
        // through the chrome bar instead (user's call, 2026-08-06, confirmed by three independent
        // sources - see Alarms and DragonPalette's gauge block).
        //
        // The threshold logic itself was not wrong and did not go away - it moved to Alarms.Low /
        // Alarms.High, which return a SEVERITY. Colour is now the caller's decision, which is the
        // point: a dial paints itself with its own colour, and severity goes to the one place that
        // shows alarm.
    }
}
