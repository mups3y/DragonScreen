// DragonScreen — StatusIndicator  (Phase 6: pure display component)
// ============================================================================================
// A READ-ONLY status readout: a word in its state colour. Two forms:
//   Badge(...) — a framed plate with a centred word (e.g. the GNC AUTO/MANUAL/ABORT indicator, C6).
//   Lamp(...)  — a small caption above the status word (e.g. an alert severity, a go/no-go lamp).
//
// Read-only ON PURPOSE: hit-testing is `Control`'s job. This never returns a PageHit — it shows state.
// The mode→colour mapping lives here; for alert severity the caller passes `Alarms.Colour(sev)` (one
// severity→colour function, no second copy — rule P5).
//
// PURE: no KSP/Unity.
// ============================================================================================
namespace DragonScreen
{
    public static class StatusIndicator
    {
        // Badge = Rect + Box(4 rects) + Text = 6 commands. Lamp = 2 text commands. Budget for the larger.
        public const int Commands = 6;

        /// <summary>A framed badge with a centred status word, both edge and word in <paramref name="colour"/>.</summary>
        public static void Badge(DisplayList dl, float x, float y, float w, float h, string word, Rgba colour)
        {
            if (dl == null) return;
            dl.Rect(x, y, w, h, DragonPalette.Panel);
            dl.Box(x, y, w, h, 2f, colour);
            // Centred on cap height, matching Control.Button.
            dl.Text(word ?? "-", x + w * 0.5f, y + (h - Typography.Caption) * 0.5f - 1f,
                    Typography.Caption, TextAlign.Centre, colour);
        }

        /// <summary>A small caption above the status word in <paramref name="colour"/> (Value size).</summary>
        public static void Lamp(DisplayList dl, float x, float y, string caption, string word, Rgba colour)
        {
            if (dl == null) return;
            dl.Text(caption, x, y, Typography.Caption, TextAlign.Left, DragonPalette.Text6);
            dl.Text(word ?? "-", x, y + Typography.Caption + 4f, Typography.Value, TextAlign.Left, colour);
        }

        /// <summary>
        /// Control-authority mode → colour (rule C6, "automation must be visible"):
        /// AUTO green · MANUAL cyan · RECOVERY amber · ABORT red · IDLE dim.
        /// </summary>
        public static Rgba Colour(ControlMode m)
        {
            switch (m)
            {
                case ControlMode.Abort:    return DragonPalette.Alarm;
                case ControlMode.Recovery: return DragonPalette.Caution;
                case ControlMode.Manual:   return DragonPalette.Accent;
                case ControlMode.Auto:     return DragonPalette.Go;
                default:                   return DragonPalette.Text7;   // IDLE
            }
        }
    }
}
