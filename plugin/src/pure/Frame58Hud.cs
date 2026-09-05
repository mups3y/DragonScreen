// DragonScreen — Frame58Hud  (PURE: the Figma "Frame 58" attitude / docking HUD)
// ============================================================================================
// The docking HUD. The raw game navball read as a mess on the glass (a photographic sphere with
// mirrored numerals) next to the design's clean SYNTHETIC instrument, so this now shows the Figma
// frame itself (art/cover/frame58.png) — the light-blue attitude bowl + graticule, corner thruster
// rings, acceleration gauge — exactly as designed, fit to height and centred so the ball stays round.
//
// The owner's nose-cone rule is preserved: with the nose cone CLOSED the light-blue bowl shows; when
// it is toggled OPEN the LIVE docking-camera feed (ImageId.DockingCamLive) fills the bowl (clipped to
// the circle) with the centre crosshair over it. Making the attitude readouts live again is a later
// pass — a synthetic tilting bowl, not the navball.
// ============================================================================================
namespace DragonScreen
{
    public static class Frame58Hud
    {
        public const int Commands = 20;
        const float RefW = 3427f, RefH = 2112f;

        // the light-blue attitude bowl, from the frame metadata (Ellipse 6 centre) — the disc the
        // docking camera fills when the nose cone is open.
        const float BowlCx = 1706f, BowlCy = 984f, BowlR = 470f;
        static readonly Rgba BowlBlue = Rgba.Hex("2C4A7E");   // masks the cam's square corners to the bowl

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f;

            dl.Rect(0, 0, w, h, DragonPalette.Background);
            dl.Asset("frame58", ox, 0f, RefW * sc, h, DragonPalette.White);

            // nose cone open -> the docking camera fills the bowl; else the frame's own light-blue bowl.
            if (s.Steps.NoseConeOpen)
            {
                float bcx = ox + BowlCx * sc, bcy = BowlCy * sc, r = BowlR * sc;
                dl.ImageCircle(ImageId.DockingCamLive, bcx - r, bcy - r, 2f * r, 2f * r, DragonPalette.White, BowlBlue);
                TargetReticle.Crosshair(dl, bcx, bcy, r * 0.14f, DragonPalette.Text2);
            }

            // "MANUAL DOCKING" entry in the letterbox margin (screen-space, so it never overlaps the
            // fit-to-height frame art). Opens the manual docking screen; hit-tested in FigmaUI.HitTest.
            if (ox > 40f)
            {
                float bx = 12f, bw = ox - 24f, by = h * 0.44f, bh = h * 0.12f, ts = h * 0.020f;
                dl.Rect(bx, by, bw, bh, DragonPalette.Panel);
                dl.Box(bx, by, bw, bh, 2, DragonPalette.Accent);
                dl.Text("MANUAL", bx + bw * 0.5f, by + bh * 0.26f, ts, TextAlign.Centre, DragonPalette.White);
                dl.Text("DOCKING", bx + bw * 0.5f, by + bh * 0.54f, ts, TextAlign.Centre, DragonPalette.Accent);
            }

            // full-width bottom status bar over the frame so it reaches both edges.
            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame
        }
    }
}
