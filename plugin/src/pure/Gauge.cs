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
        {
            float v = radius * 0.46f;
            if (v < Typography.Min) v = Typography.Min;
            if (v > Typography.Value) v = Typography.Value;
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
        {
            Ring(dl, cx, cy, radius, thickness, value01, track, fill);

            float vs = ValueSize(radius);
            bool hasUnit = !string.IsNullOrEmpty(unit);

            float vy = hasUnit ? (cy - vs * 0.95f) : (cy - vs * 0.55f);
            dl.Text(valueText ?? "-", cx, vy, vs, TextAlign.Centre, DragonPalette.Text0);
            if (hasUnit)
                dl.Text(unit, cx, vy + vs * 1.05f, Typography.Caption, TextAlign.Centre,
                        DragonPalette.Text5);

            // ---- A LONG CAPTION WRAPS AT THE SPACE, IT DOES NOT RUN INTO THE DIAL ----
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

        public static void Bar(DisplayList dl, float x, float y, float width,
                               string caption, string value, string unit, double value01, Rgba fill)
        {
            dl.Text(caption, x, y, Typography.Caption, TextAlign.Left, DragonPalette.Text6);
            dl.Text(value ?? "-", x + width, y, Typography.Body, TextAlign.Right,
                    DragonPalette.Text0);
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
                if (fw < BarH) fw = BarH;
                dl.Rect(x, by, fw, BarH, fill);
            }
        }

        // ---- WHERE LowIsBad / HighIsBad WENT ----
    }
}
