// DragonScreen — NumericReadout  (Phase 6: pure display component)
// ============================================================================================
// A single telemetry readout: a caption above a value. Two forms:
//   Value(...)  — caption + one value, in a chosen colour.
//   Paired(...) — the real Crew Dragon "two numbers per axis" (SCREEN_EVIDENCE_MATRIX.md, Frame 58):
//                 a GREEN *correction* (drive to 0) above a BLUE *rate* (current speed).
//
// HONESTY (rule E4): the widget draws whatever STRING the caller gives it. The caller passes a real
// formatted value, or an explicit "—" / "NO DATA" / "INVALID" — never a fake number. A null/empty
// string is rendered as the em-dash placeholder (so a dead feed reads as dead, not as 0).
//
// PURE: no KSP/Unity. Anchored at x (top-left of the caption); no width measurement (see DisplayList).
// ============================================================================================
namespace DragonScreen
{
    public static class NumericReadout
    {
        public const string Blank = "—";

        // Value() emits 2 text commands; Paired() emits 3. Budget for the larger.
        public const int Commands = 3;

        // Spacing between the caption line and the value line.
        private const float Gap = 4f;

        /// <summary>Caption above a single value, the value in <paramref name="valueColour"/>.</summary>
        public static void Value(DisplayList dl, float x, float y, string caption,
                                 string value, Rgba valueColour, float valueSize)
        {
            if (dl == null) return;
            dl.Text(caption, x, y, Typography.Caption, TextAlign.Left, DragonPalette.Text6);
            dl.Text(Show(value), x, y + Typography.Caption + Gap, valueSize, TextAlign.Left, valueColour);
        }

        /// <summary>
        /// The paired correction+rate readout the real docking HUD uses: a GREEN correction (Value size)
        /// above a BLUE rate (Caption size). Each independently placeholder-safe.
        /// </summary>
        public static void Paired(DisplayList dl, float x, float y, string caption,
                                  string correction, string rate)
        {
            if (dl == null) return;
            dl.Text(caption, x, y, Typography.Caption, TextAlign.Left, DragonPalette.Text6);
            float vy = y + Typography.Caption + 2f;
            dl.Text(Show(correction), x, vy, Typography.Value, TextAlign.Left, DragonPalette.Go);
            dl.Text(Show(rate), x, vy + Typography.Value, Typography.Caption, TextAlign.Left, DragonPalette.AccentDim);
        }

        /// <summary>Null/empty → the em-dash placeholder; otherwise the caller's string unchanged.</summary>
        public static string Show(string value)
        {
            return string.IsNullOrEmpty(value) ? Blank : value;
        }
    }
}
