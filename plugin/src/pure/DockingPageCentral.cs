// DragonScreen — DockingPageCentral  (Phase 7 PROTOTYPE, display-only)
// ============================================================================================
// A CENTRAL-NAVBALL docking layout — the arrangement the real cockpit photos + real-HUD video +
// Figma Frame 58 show (SCREEN_EVIDENCE_MATRIX.md): a large central attitude sphere (the LIVE game
// navball) with the corrections/rates around it, versus the existing `DockingPage`'s four-corner-ring
// layout (from the dragon2-ui Vue demo). Built so the two can be compared before the Phase-7 decision.
//
// ⛔ DISPLAY-ONLY. This is the AUTO monitoring view. The MANUAL translation/rotation CONTROL clusters
// (iss-sim) and their real RCS/attitude commands are added with the command wiring — which is
// review-gated (does the AuthorityManager gate the actuation path). Nothing here commands anything.
//
// Built from the reusable Phase-6 components (AttitudeHud on ImageId.NavBallLive, StatusIndicator,
// Gauge). Values are pre-formatted PageState strings (E4: real or "—", never a fake number).
// ============================================================================================
namespace DragonScreen
{
    public static class DockingPageCentral
    {
        public static float BodyHeight(int h) { return h - ChromeBar.Height; }

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            if (dl == null) return;
            float body = BodyHeight(h);
            float cx = w * 0.5f, cy = body * 0.53f;
            float radius = body * 0.30f;

            // ---- background: the live docking camera, edge to edge; a vignette so text reads over a
            // sunlit target. Dark when no camera (the page works without it).
            dl.Rect(0f, 0f, w, body, DragonPalette.Background);
            dl.Image(ImageId.DockingCamLive, 0f, 0f, w, body, DragonPalette.White);
            float vig = body * 0.92f;
            dl.Image(ImageId.HudDarken, cx - vig * 0.5f, cy - vig * 0.5f, vig, vig, DragonPalette.White);

            // ---- header: phase (left) · target (centre) · GNC AUTO/MANUAL (right, rule C6) ----
            dl.Text(s.Valid ? (string.IsNullOrEmpty(s.Phase) ? "PROX OPS" : s.Phase) : "-",
                    24f, 16f, Typography.Body, TextAlign.Left, DragonPalette.Text5);
            dl.Text(s.TargetName ?? "NO TARGET", cx, 14f, Typography.Body, TextAlign.Centre, DragonPalette.Text1);
            StatusIndicator.Lamp(dl, w - 150f, 10f, "GNC",
                                 AuthorityManager.Name(s.Mode), StatusIndicator.Colour(s.Mode));

            // ---- the central attitude HUD: the LIVE navball + corrections/rates + X/Y/Z + RANGE/RATE ----
            AttitudeHud.Draw(dl, cx, cy, radius, FromState(s));

            // A thin alignment sweep just outside the ball — a DEVIATION, so threshold-coloured.
            Gauge.Ring(dl, cx, cy, radius + 14f, 4f, s.Valid ? s.Align01 : 0.0,
                       DragonPalette.Inset1, Alarms.Colour(Alarms.High(s.Align01)));

            // ---- right column: FLIGHT COMMANDS / FAR FIELD POSITIONING / ALERT ACTIVITY (Frame 58) ----
            float rx = w - 296f, ry = body * 0.16f;
            dl.Text("FLIGHT COMMANDS", rx, ry, Typography.Caption, TextAlign.Left, DragonPalette.Text6);
            Control.Button(dl, rx, ry + 26f, 260f, 42f, "FAR FIELD POSITIONING", false, true);
            dl.Text("ALERT ACTIVITY", rx, ry + 88f, Typography.Caption, TextAlign.Left, DragonPalette.Text6);
            if (s.Valid && !string.IsNullOrEmpty(s.FaultText) && s.FaultText != "NOMINAL")
                dl.Text(s.FaultText, rx, ry + 112f, Typography.Body, TextAlign.Left,
                        Alarms.Colour(Alarms.FdirSeverity(s)));
            else
                dl.Text("— none —", rx, ry + 112f, Typography.Caption, TextAlign.Left, DragonPalette.Text7);

            // ---- bottom selectors: FRAME · CAMERA (display-only; MANUAL clusters come with commands) ----
            Selector(dl, cx - 236f, body - 60f, "FRAME", "LVLH");
            Selector(dl, cx + 36f, body - 60f, "CAMERA", "VIRTUAL");
            dl.Text("MANUAL CONTROL CLUSTERS ADDED WITH COMMAND WIRING (PHASE 7 / REVIEW)",
                    cx, body - 12f, Typography.Dense, TextAlign.Centre, DragonPalette.Text7);
        }

        private static AttitudeHudState FromState(PageState s)
        {
            AttitudeHudState a = new AttitudeHudState();
            a.Valid = s.Valid && s.HasTarget;
            a.RollErr = s.RollText;   a.RollRate = s.RollRateText;
            a.PitchErr = s.PitchText; a.PitchRate = s.PitchRateText;
            a.YawErr = s.YawText;     a.YawRate = s.YawRateText;
            a.OffX = s.OffXText; a.OffY = s.OffYText; a.OffZ = s.OffZText;
            a.Range = s.RangeText; a.Rate = s.RateText;
            a.Closing = s.Closing; a.ClosingFast = s.ClosingFast;
            return a;
        }

        // A display-only labelled pill (caption over value). Not a Control — nothing to press yet.
        private static void Selector(DisplayList dl, float x, float y, string caption, string value)
        {
            dl.Rect(x, y, 200f, 46f, DragonPalette.Panel);
            dl.Box(x, y, 200f, 46f, 2f, DragonPalette.Hairline);
            dl.Text(caption, x + 14f, y + 7f, Typography.Dense, TextAlign.Left, DragonPalette.Text6);
            dl.Text(value ?? "-", x + 14f, y + 22f, Typography.Caption, TextAlign.Left, DragonPalette.Text1);
        }
    }
}
