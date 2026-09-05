// DragonScreen — PlaceholderPage  (PURE: an honest "not built yet" screen)
// ============================================================================================
// The new Figma navigation wires EVERY button to a destination now, but many of those destinations
// are pages we have not built yet. Rather than a dead button or a silent no-op, the button leads
// here: a card that names where it was going and says plainly it is not built. That keeps the whole
// UI navigable and testable in game while the remaining pages are filled in one at a time — the same
// one-page-at-a-time cadence the built pages followed. The back chevron (drawn by FigmaUI) returns.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class PlaceholderPage
    {
        public const int Commands = 24;
        const float RefW = 3427f, RefH = 2112f;

        public static void Build(DisplayList dl, int w, int h, string title)
        {
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f;
            float X(float x) => ox + x * sc;
            float Y(float y) => y * sc;
            float Z(float v) => v * sc;
            int St(float rs) { int p = (int)Math.Round(rs * sc); return p < 1 ? 1 : p; }

            dl.Rect(0, 0, w, h, DragonPalette.Background);

            // centred card
            dl.Rect(X(940), Y(720), Z(1547), Z(620), DragonPalette.Panel);
            dl.Box(X(940), Y(720), Z(1547), Z(620), St(3), DragonPalette.Hairline);

            dl.Text(title ?? "?", w * 0.5f, Y(880), Z(96), TextAlign.Centre, DragonPalette.White);
            dl.Text("PAGE NOT YET BUILT", w * 0.5f, Y(1040), Z(44), TextAlign.Centre, DragonPalette.Accent);
            dl.Text("this button is wired; the destination is coming",
                    w * 0.5f, Y(1120), Z(30), TextAlign.Centre, DragonPalette.Text6);

            // bottom status bar (shared chrome)
            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame
        }
    }
}
