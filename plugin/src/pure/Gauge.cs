// DragonScreen - Gauge
// ---- GEOMETRY TAKEN FROM THE REFERENCE, NOT EYEBALLED ----
namespace DragonScreen
{
    public static class Gauge
    {
        public const double StartDeg = -135.0;
        public const double EndDeg = 135.0;

        public const int Commands = 7;

        public static float ValueSize(float radius)
        { return ValueSize(radius, 1f); }

        /// <summary>
        /// As ValueSize, on a panel whose type scale is <paramref name="sc"/> = Typography.ScaleFor(panelW).
        ///
        /// ---- WHY THIS OVERLOAD EXISTS (S121a, 2026-09-06) ----
        /// ⛔ THIS IS THE ONE MEMBER OF THE R-02 FAMILY THAT IS CLAMPED RATHER THAN MERELY UN-SCALED,
        /// and the difference matters. `Typography.Min` and `Typography.Value` are sizes AT
        /// Typography.RefPanelW (1280). Used as bare panel-pixel bounds they are a CEILING, so the
        /// dial's number could not follow the panel at all — not "rendered small", but pinned:
        ///
        ///     panelW 1280 -> step 289.33, radius 115.73, radius*0.46 =  53.24 -> clamped to 28.00
        ///     panelW 2560 -> step 588.00, radius 235.20, radius*0.46 = 108.19 -> clamped to 28.00
        ///
        /// 28 px at both widths is 2.1875 % of the panel at 1280 and 1.0938 % at 2560 — exactly halved,
        /// on a gauge whose radius doubled. ⚠ And it is the 28f ceiling that does it, not the vertical
        /// allowance: that never binds (192 and 383 against radii of 116 and 235, at any StripHeight
        /// from 0 to 140). Scaling both bounds is what restores the dial's own proportion.
        ///
        /// ⭐ `Typography.Min * sc` is `Typography.MinFor(panelW)` by definition when sc comes from
        /// ScaleFor — the floor is a RATIO ([[R-02]]), so this stays one floor and not a second one.
        ///
        /// The 1-argument overload above delegates with sc = 1, so every caller that has not had its
        /// scale pass yet renders byte-identically. [[S121b]] / [[S121d]] are the passes that will
        /// start supplying a real sc.
        /// </summary>
        public static float ValueSize(float radius, float sc)
        {
            float v = radius * 0.46f;
            float lo = Typography.Min * sc, hi = Typography.Value * sc;
            if (v < lo) v = lo;
            if (v > hi) v = hi;
            return v;
        }

        public static void Ring(DisplayList dl, float cx, float cy, float radius, float thickness,
                                double value01, Rgba track, Rgba fill)
        {
            if (dl == null || radius <= 0f || thickness <= 0f) return;
            float rIn = radius - thickness;
            if (rIn < 0f) rIn = 0f;

            dl.ArcBand(cx, cy, rIn, radius, StartDeg, EndDeg, track);

            double end = ArcGeometry.ValueToAngle(value01, 0.0, 1.0, StartDeg, EndDeg);
            if (end - StartDeg > 1.0)
                dl.ArcBand(cx, cy, rIn, radius, StartDeg, end, fill);
        }

        public static void Labelled(DisplayList dl, float cx, float cy, float radius, float thickness,
                                    double value01, string valueText, string unit, string caption,
                                    Rgba track, Rgba fill)
        { Labelled(dl, cx, cy, radius, thickness, value01, valueText, unit, caption, track, fill, 1f); }

        /// <summary>As Labelled, on a panel whose type scale is <paramref name="sc"/>. The value size
        /// comes from the scaled ValueSize above; the unit and the caption are Typography sizes AT
        /// RefPanelW and are the widget's own to scale, so they are scaled here. ⚠ The offsets that go
        /// WITH the type scale with it — `vs * 1.05f` and `vs * 0.95f` already do, being derived from
        /// `vs`, but the `+2f` between the two caption lines is a raw pixel and did not.
        /// The 11-argument overload delegates at sc = 1 (S121a, 2026-09-06).</summary>
        public static void Labelled(DisplayList dl, float cx, float cy, float radius, float thickness,
                                    double value01, string valueText, string unit, string caption,
                                    Rgba track, Rgba fill, float sc)
        {
            Ring(dl, cx, cy, radius, thickness, value01, track, fill);

            float vs = ValueSize(radius, sc);
            bool hasUnit = !string.IsNullOrEmpty(unit);

            float vy = hasUnit ? (cy - vs * 0.95f) : (cy - vs * 0.55f);
            dl.Text(valueText ?? Dashes.None, cx, vy, vs, TextAlign.Centre, DragonPalette.Text0);
            if (hasUnit)
                dl.Text(unit, cx, vy + vs * 1.05f, Typography.Caption * sc, TextAlign.Centre,
                        DragonPalette.Text5);

            // ---- A LONG CAPTION WRAPS AT THE SPACE, IT DOES NOT RUN INTO THE DIAL ----
            string cap = caption ?? "";
            float capY = cy + radius * 0.62f;
            int split = (cap.Length > 12) ? cap.LastIndexOf(' ') : -1;
            if (split > 0)
            {
                dl.Text(cap.Substring(0, split), cx, capY, Typography.Caption * sc, TextAlign.Centre,
                        DragonPalette.Text6);
                dl.Text(cap.Substring(split + 1), cx, capY + (Typography.Caption + 2f) * sc,
                        Typography.Dense * sc, TextAlign.Centre, DragonPalette.Text7);
            }
            else
            {
                dl.Text(cap, cx, capY, Typography.Caption * sc, TextAlign.Centre, DragonPalette.Text6);
            }
        }

        public static void Bar(DisplayList dl, float x, float y, float width,
                               string caption, string value, string unit, double value01, Rgba fill)
        { Bar(dl, x, y, width, caption, value, unit, value01, fill, 1f); }

        /// <summary>As Bar, on a panel whose type scale is <paramref name="sc"/>. ⚠ The bar is not only
        /// type: `56f` is the unit's right inset, `28f` the gap down to the track and `BarH` the track's
        /// own thickness — all measured at RefPanelW, all of them boxes rather than type, which is
        /// exactly [[S117]]'s trap. `width`, `x` and `y` are the caller's and are left alone.
        /// The 9-argument overload delegates at sc = 1 (S121a, 2026-09-06).</summary>
        public static void Bar(DisplayList dl, float x, float y, float width,
                               string caption, string value, string unit, double value01, Rgba fill,
                               float sc)
        {
            dl.Text(caption, x, y, Typography.Caption * sc, TextAlign.Left, DragonPalette.Text6);
            dl.Text(value ?? Dashes.None, x + width, y, Typography.Body * sc, TextAlign.Right,
                    DragonPalette.Text0);
            if (!string.IsNullOrEmpty(unit))
                dl.Text(unit, x + width - 56f * sc, y + 2f * sc, Typography.Caption * sc, TextAlign.Right,
                        DragonPalette.Text6);

            float BarH = 6f * sc;
            float by = y + 28f * sc;
            dl.Rect(x, by, width, BarH, DragonPalette.BarTrack);
            if (value01 > 0.0)
            {
                double f = (value01 > 1.0) ? 1.0 : value01;
                float fw = (float)(width * f);
                if (fw < BarH) fw = BarH;
                dl.Rect(x, by, fw, BarH, fill);
            }
        }

        // ---- WHERE LowIsBad / HighIsBad WENT ----
    }
}
