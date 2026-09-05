// DragonScreen — AttitudeHud  (Phase 6: pure display component)
// ============================================================================================
// The docking ATTITUDE display, built around the REAL GAME NAVBALL — not a synthetic sphere.
// `ImageId.NavBallLive` is the live 3D navball the game renders (NavBallRenderer → RenderTexture);
// this composites it as the central sphere and overlays the docking readouts around it, in the real
// HUD's cardinal arrangement (Frame 58 / real cockpit photos, see SCREEN_EVIDENCE_MATRIX.md):
//   ROLL top · YAW bottom · PITCH right  — each a GREEN correction over a BLUE rate (NumericReadout)
//   X/Y/Z offsets left · RANGE/RATE beneath · a centre aim crosshair (TargetReticle)
//
// DISPLAY-ONLY. It shows attitude; the manual translation/rotation COMMANDS are the docking page's
// separate, review-gated concern. Inputs are pre-formatted strings (rule E4: real values or "—",
// never a fake number). Offline (preview) the navball is a circular skin stand-in; in game it is the
// real oriented sphere.
// ============================================================================================
namespace DragonScreen
{
    /// <summary>Pre-formatted strings the attitude HUD shows. Populated FROM the authoritative snapshot.</summary>
    public struct AttitudeHudState
    {
        public bool Valid;
        public string RollErr, RollRate, PitchErr, PitchRate, YawErr, YawRate;
        public string OffX, OffY, OffZ;
        public string Range, Rate;
        public bool Closing, ClosingFast;
    }

    public static class AttitudeHud
    {
        public static void Draw(DisplayList dl, float cx, float cy, float radius, AttitudeHudState s)
        { Draw(dl, cx, cy, radius, s, ImageId.NavBallLive, DragonPalette.Background, 1f); }

        /// <summary>As Draw, on a panel whose type scale is <paramref name="sc"/>.
        /// ⛔ `sc` is PASSED IN and cannot be derived here: this widget is handed a centre and a RADIUS,
        /// never a panel width, and the radius is a fraction of the body — so it already tracks the
        /// panel while every caption, gap and column beside it is a RefPanelW pixel that does not.
        /// The 5-argument overload delegates at sc = 1 ([[S121d]], 2026-09-06).</summary>
        public static void Draw(DisplayList dl, float cx, float cy, float radius, AttitudeHudState s, float sc)
        { Draw(dl, cx, cy, radius, s, ImageId.NavBallLive, DragonPalette.Background, sc); }

        /// <summary>As Draw, but the central disc is a caller-chosen image — the live navball for the
        /// closed-nose attitude view, or ImageId.DockingCamLive when the nose cone is open (the HUD then
        /// overlays the docking-camera feed). The overlay (ring, crosshair, readouts) is identical.</summary>
        public static void Draw(DisplayList dl, float cx, float cy, float radius, AttitudeHudState s, ImageId centre)
        { Draw(dl, cx, cy, radius, s, centre, DragonPalette.Background, 1f); }

        /// <summary>As above; <paramref name="discBg"/> is the colour the disc sits on, used to mask the
        /// corners of an OPAQUE feed (the docking camera) into a circle. The navball needs no mask — its
        /// render already has transparent corners — so only the camera disc is clipped.</summary>
        public static void Draw(DisplayList dl, float cx, float cy, float radius, AttitudeHudState s, ImageId centre, Rgba discBg)
        { Draw(dl, cx, cy, radius, s, centre, discBg, 1f); }

        /// <summary>As above, scaled. See the `sc` note on the 5-argument overload.</summary>
        public static void Draw(DisplayList dl, float cx, float cy, float radius, AttitudeHudState s, ImageId centre, Rgba discBg, float sc)
        {
            if (dl == null || radius <= 0f) return;
            float d = radius * 2f;

            // The central disc: the live navball (closed nose) or the docking-cam feed (open nose), with
            // a thin ring around it. The camera is an opaque rectangle, so it is clipped to the circle;
            // the navball reads round for free and is drawn plain.
            if (centre == ImageId.DockingCamLive)
                dl.ImageCircle(centre, cx - radius, cy - radius, d, d, DragonPalette.White, discBg);
            else
                dl.Image(centre, cx - radius, cy - radius, d, d, DragonPalette.White);
            dl.ArcBand(cx, cy, radius, radius + 2f * sc, 0.0, 360.0, DragonPalette.Text5);

            // Centre aim crosshair — the mark the target is brought onto.
            TargetReticle.Crosshair(dl, cx, cy, radius * 0.14f, DragonPalette.Text2);

            if (!s.Valid)
            {
                dl.Text("NO TARGET", cx, cy + radius + 10f * sc, Typography.Body * sc, TextAlign.Centre, DragonPalette.Text7);
                return;
            }

            float pad = radius + 16f * sc;
            // ROLL top · YAW bottom · PITCH right — green correction over blue rate.
            NumericReadout.Paired(dl, cx - 34f * sc, cy - pad - 46f * sc, "ROLL", s.RollErr, s.RollRate, sc);
            NumericReadout.Paired(dl, cx - 34f * sc, cy + pad, "YAW", s.YawErr, s.YawRate, sc);
            NumericReadout.Paired(dl, cx + pad, cy - 34f * sc, "PITCH", s.PitchErr, s.PitchRate, sc);

            // X / Y / Z offsets, stacked left of the ring.
            float lx = cx - pad - 108f * sc;
            XyzRow(dl, lx, cy - 40f * sc, "X", s.OffX, sc);
            XyzRow(dl, lx, cy - 22f * sc, "Y", s.OffY, sc);
            XyzRow(dl, lx, cy - 4f * sc,  "Z", s.OffZ, sc);

            // RANGE / RATE beneath the ring — the two numbers a manual approach is flown on.
            NumericReadout.Value(dl, lx, cy + radius * 0.55f, "RANGE", s.Range, DragonPalette.Go, Typography.Value * sc, sc);
            Rgba rateCol = s.ClosingFast ? DragonPalette.Alarm
                         : s.Closing ? DragonPalette.Go : DragonPalette.Caution;
            NumericReadout.Value(dl, cx + pad, cy + radius * 0.55f, "RATE", s.Rate, rateCol, Typography.Value * sc, sc);
        }

        private static void XyzRow(DisplayList dl, float x, float y, string axis, string value, float sc)
        {
            dl.Text(axis, x, y, Typography.Caption * sc, TextAlign.Left, DragonPalette.Text5);
            dl.Text(NumericReadout.Show(value), x + 100f * sc, y, Typography.Caption * sc, TextAlign.Right, DragonPalette.Text0);
        }
    }
}
