// DragonScreen — FigmaFramePage  (PURE: a whole Figma frame shown from its own PNG export)
// ============================================================================================
// The complex, illustration-heavy screens (the attitude HUD, the procedure page, the cabin view) are
// far too intricate to rebuild element-by-element right away, so this shows the frame's exact Figma
// export as one image while we decide which parts to make live. It is drawn UNDISTORTED — fit to the
// screen height and centred, so circles (the attitude ball) stay round — with the full-width bottom
// status bar (component_48) painted over the top so the bar reaches both edges.
//
// This is the exact LOOK; it is not yet live/interactive. Live overlays (the navball, readouts) and
// the fill-to-the-edges reflow replace it per page as we build each one out properly (see CoverPage,
// SettingsAudioPage for the element-level treatment).
// ============================================================================================
namespace DragonScreen
{
    public static class FigmaFramePage
    {
        public const int Commands = 8;
        const float RefW = 3427f, RefH = 2112f;

        public static void Build(DisplayList dl, int w, int h, string frameKey)
        {
            dl.Rect(0, 0, w, h, DragonPalette.Background);
            // fit to height, centred (undistorted — the attitude ball stays circular)
            float sc = h / RefH;
            float dw = RefW * sc, ox = (w - dw) * 0.5f;
            dl.Asset(frameKey, ox, 0f, dw, h, DragonPalette.White);
            // full-width bottom status bar over the top so it reaches both edges
            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame
        }
    }
}
