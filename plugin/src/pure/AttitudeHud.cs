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
        {
            if (dl == null || radius <= 0f) return;
            float d = radius * 2f;

            // The LIVE navball sphere (real game render; a clipped circular skin in the offline preview),
            // with a thin ring around it.
            dl.Image(ImageId.NavBallLive, cx - radius, cy - radius, d, d, DragonPalette.White);
            dl.ArcBand(cx, cy, radius, radius + 2f, 0.0, 360.0, DragonPalette.Text5);

            // Centre aim crosshair — the mark the target is brought onto.
            TargetReticle.Crosshair(dl, cx, cy, radius * 0.14f, DragonPalette.Text2);

            if (!s.Valid)
            {
                dl.Text("NO TARGET", cx, cy + radius + 10f, Typography.Body, TextAlign.Centre, DragonPalette.Text7);
                return;
            }

            float pad = radius + 16f;
            // ROLL top · YAW bottom · PITCH right — green correction over blue rate.
            NumericReadout.Paired(dl, cx - 34f, cy - pad - 46f, "ROLL", s.RollErr, s.RollRate);
            NumericReadout.Paired(dl, cx - 34f, cy + pad, "YAW", s.YawErr, s.YawRate);
            NumericReadout.Paired(dl, cx + pad, cy - 34f, "PITCH", s.PitchErr, s.PitchRate);

            // X / Y / Z offsets, stacked left of the ring.
            float lx = cx - pad - 108f;
            XyzRow(dl, lx, cy - 40f, "X", s.OffX);
            XyzRow(dl, lx, cy - 22f, "Y", s.OffY);
            XyzRow(dl, lx, cy - 4f,  "Z", s.OffZ);

            // RANGE / RATE beneath the ring — the two numbers a manual approach is flown on.
            NumericReadout.Value(dl, lx, cy + radius * 0.55f, "RANGE", s.Range, DragonPalette.Go, Typography.Value);
            Rgba rateCol = s.ClosingFast ? DragonPalette.Alarm
                         : s.Closing ? DragonPalette.Go : DragonPalette.Caution;
            NumericReadout.Value(dl, cx + pad, cy + radius * 0.55f, "RATE", s.Rate, rateCol, Typography.Value);
        }

        private static void XyzRow(DisplayList dl, float x, float y, string axis, string value)
        {
            dl.Text(axis, x, y, Typography.Caption, TextAlign.Left, DragonPalette.Text5);
            dl.Text(NumericReadout.Show(value), x + 100f, y, Typography.Caption, TextAlign.Right, DragonPalette.Text0);
        }
    }
}
