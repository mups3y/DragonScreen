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
        public static float BodyHeight(int w, int h) { return h - ChromeBar.HeightFor(w); }

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            if (dl == null) return;
            float sc = Typography.ScaleFor(w);   // [[S121d]], 2026-09-06 — see DockingPage's note
            float body = BodyHeight(w, h);
            float cx = w * 0.5f, cy = body * 0.53f;
            float radius = body * 0.30f;

            // ---- background: the live docking camera, edge to edge; a vignette so text reads over a
            // sunlit target. Dark when no camera (the page works without it).
            dl.Rect(0f, 0f, w, body, DragonPalette.Background);
            dl.Image(ImageId.DockingCamLive, 0f, 0f, w, body, DragonPalette.White);
            float vig = body * 0.92f;
            dl.Image(ImageId.HudDarken, cx - vig * 0.5f, cy - vig * 0.5f, vig, vig, DragonPalette.White);

            // ---- header: phase (left) · target (centre) · GNC AUTO/MANUAL (right, rule C6) ----
            dl.Text(s.Valid ? (string.IsNullOrEmpty(s.Phase) ? "PROX OPS" : s.Phase) : Dashes.None,
                    24f * sc, 16f * sc, Typography.Body * sc, TextAlign.Left, DragonPalette.Text5);
            dl.Text(s.TargetName ?? "NO TARGET", cx, 14f * sc, Typography.Body * sc, TextAlign.Centre, DragonPalette.Text1);
            StatusIndicator.Lamp(dl, w - 150f * sc, 10f * sc, "GNC",
                                 AuthorityManager.Name(s.Mode), StatusIndicator.Colour(s.Mode), sc);

            // ---- the central attitude HUD: the LIVE navball + corrections/rates + X/Y/Z + RANGE/RATE ----
            AttitudeHud.Draw(dl, cx, cy, radius, FromState(s), sc);

            // A thin alignment sweep just outside the ball — a DEVIATION, so threshold-coloured.
            Gauge.Ring(dl, cx, cy, radius + 14f * sc, 4f * sc, s.Valid ? s.Align01 : 0.0,
                       DragonPalette.Inset1, Alarms.Colour(Alarms.High(s.Align01)));

            // ---- right column: FLIGHT COMMANDS / FAR FIELD POSITIONING / ALERT ACTIVITY (Frame 58) ----
            float rx = w - 296f * sc, ry = body * 0.16f;
            dl.Text("FLIGHT COMMANDS", rx, ry, Typography.Caption * sc, TextAlign.Left, DragonPalette.Text6);
            Control.Button(dl, rx, ry + 26f * sc, 260f * sc, 42f * sc, "FAR FIELD POSITIONING", false, true, sc);
            dl.Text("ALERT ACTIVITY", rx, ry + 88f * sc, Typography.Caption * sc, TextAlign.Left, DragonPalette.Text6);
            if (s.Valid && !string.IsNullOrEmpty(s.FaultText) && s.FaultText != "NOMINAL")
                dl.Text(s.FaultText, rx, ry + 112f * sc, Typography.Body * sc, TextAlign.Left,
                        Alarms.Colour(Alarms.FdirSeverity(s)));
            else
                dl.Text("— none —", rx, ry + 112f * sc, Typography.Caption * sc, TextAlign.Left, DragonPalette.Text7);

            // ---- bottom selectors: FRAME · CAMERA (display-only; MANUAL clusters come with commands) ----
            Selector(dl, cx - 236f * sc, body - 60f * sc, "FRAME", "LVLH", sc);
            Selector(dl, cx + 36f * sc, body - 60f * sc, "CAMERA", "VIRTUAL", sc);
            dl.Text("MANUAL CONTROL CLUSTERS ADDED WITH COMMAND WIRING (PHASE 7 / REVIEW)",
                    cx, body - 12f * sc, Typography.Dense * sc, TextAlign.Centre, DragonPalette.Text7);
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
        /// ⛔ `sc` is PASSED IN — this pill takes an x and a y and no width ([[S121d]], 2026-09-06).
        private static void Selector(DisplayList dl, float x, float y, string caption, string value, float sc)
        {
            dl.Rect(x, y, 200f * sc, 46f * sc, DragonPalette.Panel);
            dl.Box(x, y, 200f * sc, 46f * sc, 2f * sc, DragonPalette.Hairline);
            dl.Text(caption, x + 14f * sc, y + 7f * sc, Typography.Dense * sc, TextAlign.Left, DragonPalette.Text6);
            dl.Text(value ?? Dashes.None, x + 14f * sc, y + 22f * sc, Typography.Caption * sc, TextAlign.Left, DragonPalette.Text1);
        }
    }
}
