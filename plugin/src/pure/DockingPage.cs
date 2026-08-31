/*
 * DragonScreen - DockingPage  (Phase 7 gold-standard screen — AUTO monitoring view)
 *
 * PURE. The Crew Dragon docking display, laid out from the REAL HUD, not a navball.
 *
 * ---- WHY THIS WAS REBUILT (2026-08-31) ----
 * Two earlier attempts were REJECTED by the owner as "nothing like the real Crew Dragon docking
 * screen": the four-corner-ring `DockingPage` (from the dragon2-ui Vue demo) and the central-navball
 * `DockingPageCentral`. The navball-centric approach is wrong. The evidence hierarchy settles it
 * (SCREEN_EVIDENCE_MATRIX.md): the CONFIRMED real-HUD video (youtube MdJDBHzJF8E) and iss-sim.spacex.com
 * show a CAMERA / TARGET view of the docking port with a central RETICLE inside thin rings — not a
 * KSP attitude sphere. The community Figma Frame 58 shows a navball-ish sphere, but it is RECONSTRUCTED
 * and outranked by the real video. So: reticle in rings, never a navball.
 *
 * ---- THE REAL-HUD ARRANGEMENT (CONFIRMED, SCREEN_EVIDENCE_MATRIX.md §"real HUD") ----
 *   centre      thin HUD rings + a centre crosshair reticle; a green diamond marks the target
 *   left        ROTATION corrections — roll / pitch / yaw, each a GREEN correction over a BLUE rate
 *   right       TRANSLATION / ALIGNMENT — X / Y / Z lateral offsets and the alignment angle (green)
 *   bottom-left RANGE   (distance to the target)
 *   bottom-right RATE   (closing rate; negative = approaching)
 *   ultra-minimalist: thin rings, small captions, no chrome, dark background
 *
 * The two-number-per-axis scheme (GREEN correction to drive to 0, BLUE current rate) is the key
 * design takeaway from iss-sim and is carried by NumericReadout.Paired — one component, one rule.
 *
 * ---- THIS IS THE AUTO (MONITORING) VIEW ----
 * Dragon docks autonomously, so the real primary interface is this clean monitoring HUD. The MANUAL
 * translation/rotation CONTROL clusters (iss-sim's LEFT/RIGHT button pads) and their REAL RCS/attitude
 * commands through the AuthorityManager are the NEXT increment (command wiring, review-gated). This
 * page commands nothing yet; it only presents authoritative state (rules T1/T2/E4). Every value is a
 * pre-formatted PageState string — a real number or an explicit "—", never an invented one.
 *
 * ---- ONE THING STILL OWED UPSTREAM (rule T3, next increment) ----
 * The green target diamond is drawn on the boresight because the snapshot carries the pointing-error
 * MAGNITUDE (Align01) but not yet a 2-D bearing. Its lateral offset is honestly shown by the alignment
 * SWEEP around the outer ring, not by faking a direction. When a per-axis pointing-error vector is
 * plumbed into the snapshot the diamond moves off-boresight; until then it does not pretend to.
 */
namespace DragonScreen
{
    public static class DockingPage
    {
        /// <summary>Body height — everything above the chrome bar. Reused by drawing and the tests.</summary>
        public static float BodyHeight(int h) { return h - ChromeBar.Height; }

        /// <summary>Outer-ring radius as a fraction of the body height. Exposed so the layout test
        /// asserts placement against the ring rather than a magic number.</summary>
        public const float RingFraction = 0.30f;

        /// <summary>Radius of the central HUD ring the target is flown into.</summary>
        public static float OuterRadius(int h) { return BodyHeight(h) * RingFraction; }

        /// <summary>Centre of the HUD reticle: horizontally centred, biased up so RANGE/RATE clear the
        /// bottom. ONE function for drawing and for the tests, so the two cannot drift apart.</summary>
        public static void Centre(int w, int h, out float cx, out float cy)
        {
            cx = w * 0.5f;
            cy = BodyHeight(h) * 0.46f;
        }

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            if (dl == null) return;
            float body = BodyHeight(h);
            float cx, cy;
            Centre(w, h, out cx, out cy);
            float R = OuterRadius(h);

            // ---- THE LIVE DOCKING VIEW IS THE BACKGROUND ----
            // Full bleed, behind everything. With no camera the page simply has a dark background and
            // every instrument still works — it is designed to (rule S10, graceful degradation).
            dl.Rect(0f, 0f, w, body, DragonPalette.Background);
            dl.Image(ImageId.DockingCamLive, 0f, 0f, w, body, DragonPalette.White);
            // A darken square behind the rings so white/green numerals read over a sunlit target.
            float vig = body * 0.94f;
            dl.Image(ImageId.HudDarken, cx - vig * 0.5f, cy - vig * 0.5f, vig, vig, DragonPalette.White);

            // ---- HEADER: phase (left) · target (centre) · GNC AUTO/MANUAL (right, rule C6) ----
            dl.Text(s.Valid ? (string.IsNullOrEmpty(s.Phase) ? "PROX OPS" : s.Phase) : "-",
                    24f, 16f, Typography.Body, TextAlign.Left, DragonPalette.Text5);
            dl.Text(s.HasTarget ? (s.TargetName ?? "TARGET") : "NO TARGET",
                    cx, 14f, Typography.Body, TextAlign.Centre,
                    s.HasTarget ? DragonPalette.Text1 : DragonPalette.Text6);
            StatusIndicator.Badge(dl, w - 170f, 10f, 132f, 40f,
                                  AuthorityManager.Name(s.Mode), StatusIndicator.Colour(s.Mode));

            // ---- THE CENTRAL RETICLE: two thin concentric rings + a boresight crosshair ----
            // NOT a navball. The real HUD frames the docking target in thin rings with a centre
            // crosshair the target is brought onto.
            dl.ArcBand(cx, cy, R - 2f, R, 0.0, 360.0, DragonPalette.Text4);          // outer ring
            TargetReticle.Crosshair(dl, cx, cy, R * 0.55f, DragonPalette.Text2);      // inner ring + cross

            // The alignment SWEEP around the outer ring — the pointing-error MAGNITUDE, threshold
            // coloured. Honest: it shows how far off we are without inventing a 2-D direction.
            if (s.Valid && s.HasTarget)
                Gauge.Ring(dl, cx, cy, R + 12f, 4f, s.Align01,
                           DragonPalette.Inset1, Alarms.Colour(Alarms.High(s.Align01)));

            // The green diamond target marker. On the boresight until a bearing vector exists upstream
            // (see the file header); the sweep above carries the misalignment for now.
            if (s.Valid && s.HasTarget)
                TargetReticle.Marker(dl, cx, cy, 11f, DragonPalette.Go);

            if (!s.Valid || !s.HasTarget)
            {
                dl.Text("NO TARGET SELECTED", cx, cy + R + 24f, Typography.Body, TextAlign.Centre,
                        DragonPalette.Text7);
                return;   // the reticle is drawn; the target-relative readouts are withheld (nothing to show)
            }

            // ---- LEFT: ROTATION corrections, the two-number scheme (GREEN correction / BLUE rate) ----
            float lx = cx - R - 196f;
            float ry = cy - 132f;
            NumericReadout.Paired(dl, lx, ry,         "ROLL",  s.RollText,  s.RollRateText);
            NumericReadout.Paired(dl, lx, ry + 92f,   "PITCH", s.PitchText, s.PitchRateText);
            NumericReadout.Paired(dl, lx, ry + 184f,  "YAW",   s.YawText,   s.YawRateText);

            // ---- RIGHT: TRANSLATION / ALIGNMENT — lateral offsets + the alignment angle (green) ----
            float rx = cx + R + 44f;
            NumericReadout.Value(dl, rx, ry,        "X",     s.OffXText, DragonPalette.Go, Typography.Value);
            NumericReadout.Value(dl, rx, ry + 74f,  "Y",     s.OffYText, DragonPalette.Go, Typography.Value);
            NumericReadout.Value(dl, rx, ry + 148f, "Z",     s.OffZText, DragonPalette.Go, Typography.Value);
            NumericReadout.Value(dl, rx, ry + 222f, "ALIGN", s.AlignText, DragonPalette.Go, Typography.Value);

            // ---- BOTTOM: RANGE (left) and RATE (right), the two numbers a manual approach is flown on ----
            float by = body - 92f;
            dl.Text("RANGE", 64f, by, Typography.Caption, TextAlign.Left, DragonPalette.Text6);
            dl.Text(s.RangeText ?? NumericReadout.Blank, 64f, by + 22f, Typography.Hero,
                    TextAlign.Left, DragonPalette.Go);

            // RATE keeps the two-colour scheme (a rate → blue) with a safety override: amber if we are
            // NOT approaching, red if closing hard at short range (the one docking condition worth an alarm).
            Rgba rateColour = s.ClosingFast ? DragonPalette.Alarm
                            : s.Closing ? DragonPalette.AccentDim
                            : DragonPalette.Caution;
            dl.Text("RATE", w - 64f, by, Typography.Caption, TextAlign.Right, DragonPalette.Text6);
            dl.Text(s.RateText ?? NumericReadout.Blank, w - 64f, by + 22f, Typography.Hero,
                    TextAlign.Right, rateColour);
        }
    }
}
