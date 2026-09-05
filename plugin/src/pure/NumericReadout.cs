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
        { Value(dl, x, y, caption, value, valueColour, valueSize, 1f); }

        /// <summary>As Value, on a panel whose type scale is <paramref name="sc"/> =
        /// Typography.ScaleFor(panelW). ⚠ `valueSize` is the CALLER's number and arrives already
        /// scaled — it is not multiplied here. The caption and the `Gap` beneath it are this widget's
        /// own RefPanelW constants and are. The 7-argument overload delegates at sc = 1, so an
        /// un-passed caller renders byte-identically (S121a, 2026-09-06; the idiom is Readouts.Row's).</summary>
        public static void Value(DisplayList dl, float x, float y, string caption,
                                 string value, Rgba valueColour, float valueSize, float sc)
        {
            if (dl == null) return;
            dl.Text(caption, x, y, Typography.Caption * sc, TextAlign.Left, DragonPalette.Text6);
            dl.Text(Show(value), x, y + (Typography.Caption + Gap) * sc, valueSize, TextAlign.Left, valueColour);
        }

        /// <summary>
        /// The paired correction+rate readout the real docking HUD uses: a GREEN correction (Value size)
        /// above a BLUE rate (Caption size). Each independently placeholder-safe.
        /// </summary>
        public static void Paired(DisplayList dl, float x, float y, string caption,
                                  string correction, string rate)
        { Paired(dl, x, y, caption, correction, rate, 1f); }

        /// <summary>As Paired, on a panel whose type scale is <paramref name="sc"/>. ⛔ Every size in
        /// this one is the widget's own — the caller passes no size at all — so all three lines and the
        /// two gaps between them scale together, which is what keeps the stack from overlapping itself
        /// at 2560. The 6-argument overload delegates at sc = 1 (S121a, 2026-09-06).</summary>
        public static void Paired(DisplayList dl, float x, float y, string caption,
                                  string correction, string rate, float sc)
        {
            if (dl == null) return;
            dl.Text(caption, x, y, Typography.Caption * sc, TextAlign.Left, DragonPalette.Text6);
            float vy = y + (Typography.Caption + 2f) * sc;
            dl.Text(Show(correction), x, vy, Typography.Value * sc, TextAlign.Left, DragonPalette.Go);
            dl.Text(Show(rate), x, vy + Typography.Value * sc, Typography.Caption * sc, TextAlign.Left, DragonPalette.AccentDim);
        }

        /// <summary>Null/empty → the em-dash placeholder; otherwise the caller's string unchanged.</summary>
        public static string Show(string value)
        {
            return string.IsNullOrEmpty(value) ? Blank : value;
        }
    }
}
