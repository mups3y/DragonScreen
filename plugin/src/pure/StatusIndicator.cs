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
        { Badge(dl, x, y, w, h, word, colour, 1f); }

        /// <summary>
        /// As Badge, on a panel whose type scale is <paramref name="sc"/> = Typography.ScaleFor(panelW).
        ///
        /// ⭐ THIS IS ONE HALF OF THE LATENT PAIR [[S121]] NAMES (the other is GateCard's DrawItem/Plate).
        /// The centring expression `y + (h - Typography.Caption) * 0.5f - 1f` is correct ONLY while the
        /// caller is itself unscaled: `h` is a real panel-pixel height that grows with the panel, while
        /// `Typography.Caption` is a size AT RefPanelW that does not — so the moment a caller starts
        /// passing a scaled `h`, the word drifts off the centre of its own plate. Scaling the constant
        /// alongside `h` is what makes the two comparable again. ⚠ It is NOT a bug today, and that is
        /// precisely why it had to be fixed here rather than left for the page that trips it.
        ///
        /// ⚠ `w`, `h`, `x`, `y` are the CALLER's box and are never scaled here — only this widget's own
        /// RefPanelW constants are: the type, its 1 px optical nudge, and the 2 px frame.
        /// The 7-argument overload delegates at sc = 1 (S121a, 2026-09-06).
        /// </summary>
        public static void Badge(DisplayList dl, float x, float y, float w, float h, string word, Rgba colour,
                                 float sc)
        {
            if (dl == null) return;
            dl.Rect(x, y, w, h, DragonPalette.Panel);
            dl.Box(x, y, w, h, 2f * sc, colour);
            // Centred on cap height, matching Control.Button.
            dl.Text(word ?? Dashes.None, x + w * 0.5f, y + (h - Typography.Caption * sc) * 0.5f - 1f * sc,
                    Typography.Caption * sc, TextAlign.Centre, colour);
        }

        /// <summary>A small caption above the status word in <paramref name="colour"/> (Value size).</summary>
        public static void Lamp(DisplayList dl, float x, float y, string caption, string word, Rgba colour)
        { Lamp(dl, x, y, caption, word, colour, 1f); }

        /// <summary>As Lamp, on a panel whose type scale is <paramref name="sc"/>. Both sizes and the
        /// gap between them are this widget's own RefPanelW constants, so all three scale together.
        /// The 6-argument overload delegates at sc = 1 (S121a, 2026-09-06).</summary>
        public static void Lamp(DisplayList dl, float x, float y, string caption, string word, Rgba colour,
                                float sc)
        {
            if (dl == null) return;
            dl.Text(caption, x, y, Typography.Caption * sc, TextAlign.Left, DragonPalette.Text6);
            dl.Text(word ?? Dashes.None, x, y + (Typography.Caption + 4f) * sc, Typography.Value * sc, TextAlign.Left, colour);
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
