// DragonScreen — PlaceholderPage  (PURE: an honest "not built yet" screen)
// ============================================================================================
// A card that names a destination and says plainly it is not built — the graceful landing for a page
// int that does not resolve, so the UI stays navigable and testable in game while the remaining pages
// are filled in one at a time. The back chevron (drawn by FigmaUI) returns.
//
// ⚠ S107 / QC M-02 — THE PREMISE THIS FILE WAS WRITTEN ON IS NO LONGER TRUE, AND THE CARD SAID SO
// OUT LOUD. This comment used to open "The new Figma navigation wires EVERY button to a destination
// now", and the card printed "this button is wired; the destination is coming". Both were true when
// written. S14 then removed these values from the Menu grid — correctly, per the owner's decision, so
// a dead card would not read as a real page — and left the sentence behind.
//
// Nothing in this build opens a placeholder page. Verified, not assumed: `MenuPage.BuildEntries`
// skips every `FigmaUI.IsPlaceholder` value; `BarTarget` is the five real hubs; and every
// `NavHit.Go` in `plugin/src/` targets one of eight real pages (Audio, AudioVideo, Cabin, Cover,
// Docking, ManualChute, Menu, Rendezvous). The ONLY way here is a persisted page int from an older
// build — which is exactly what this page is for, and is worth telling the crew, because it says
// what to do next.
//
// ⛔ The page and the enum values STAY. UiPage's own rule is that the int persists per screen and
// values are never renumbered; deleting either would turn a stale save into a page that renders
// nothing. S49 H9 classes this (C) — record, don't build — and that stands: the PAGE was correct,
// one SENTENCE was not.
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
            int St(float rs) => Strokes.Px(rs, sc);   // ONE rule, in Strokes.cs - rounds UP (R-02 family)

            dl.Rect(0, 0, w, h, DragonPalette.Background);

            // centred card
            dl.Rect(X(940), Y(720), Z(1547), Z(620), DragonPalette.Panel);
            dl.Box(X(940), Y(720), Z(1547), Z(620), St(3), DragonPalette.Hairline);

            dl.Text(title ?? "?", w * 0.5f, Y(880), Z(96), TextAlign.Centre, DragonPalette.White);
            dl.Text("PAGE NOT YET BUILT", w * 0.5f, Y(1040), Z(44), TextAlign.Centre, DragonPalette.Accent);
            dl.Text("no button in this build opens this page",
                    w * 0.5f, Y(1114), Z(30), TextAlign.Centre, DragonPalette.Text6);
            dl.Text("remembered from an older save — the bar below goes anywhere",
                    w * 0.5f, Y(1178), Z(30), TextAlign.Centre, DragonPalette.Text6);

            // bottom status bar (shared chrome)
            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame
        }
    }
}
