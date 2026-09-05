// DragonScreen — DockingSimPage  (PURE: the manual "ISS Docking" prox-ops screen)
// ============================================================================================
// The manual docking screen — distinct from the attitude HUD (Frame 58). Specced from the live
// iss-sim.spacex.com DOM (the sim SpaceX built from the real UI; its own "actual interface" link points
// to the training video): two concentric HUD rings + centre reticle over the docking-adapter view, a
// green target diamond, the ROLL / PITCH / YAW readouts + a PYR block, RANGE + RATE, a ROTATION control
// cluster (Roll/Pitch/Yaw) and a TRANSLATION cluster (Up/Down/Left/Right/Fwd/Back) each with a centre
// LARGE↔precise toggle, and Instructions / Reset Positions / Settings.
//
// The READOUTS are live (T13c): ROLL / PITCH / YAW, RANGE and RATE all read PageState — the same
// relative-attitude and closing geometry the attitude HUD draws — and dash with no target, because
// there is nothing to be misaligned with. The centre reticle is not a bug: it is our own boresight, and
// it belongs at the centre.
//
// S26 (this pass): the green TARGET DIAMOND used to sit at a FIXED offset that disagreed with the
// numbers beside it, and the page drew the SAME pitch/yaw/roll correction twice (once around the rings,
// once as the "PYR" block) where SCREEN_EVIDENCE_MATRIX describes one group. Both are fixed together:
//   - the diamond now moves from the live PitchDeg/YawDeg bearings (PageState, raw doubles behind
//     PitchDegText/YawDegText) and is HIDDEN entirely with no target — nothing to be off-boresight from.
//   - the ring readouts go GREEN when that axis is within a small tolerance of zero ("corrected",
//     iss-sim: SCREEN_INVENTORY #11) and WHITE otherwise, instead of a blanket green whenever a target
//     exists (which was true regardless of how far off the axis actually was).
//   - the "PYR" block, no longer a redundant echo, now carries the reference's OTHER confirmed
//     quantity: the BLUE per-axis RATE (PitchRateText/YawRateText/RollRateText — vehicle body rates,
//     already in PageState from T13b). DockingPage.cs's own header names this exact "GREEN correction /
//     BLUE rate, two numbers per axis" scheme as "the key design takeaway from iss-sim" — this page had
//     the correction twice and the rate nowhere; now each of iss-sim's two confirmed colours has one
//     place on screen, matching that precedent instead of inventing a new one.
//
// Reached from the attitude HUD (a "MANUAL DOCKING" affordance in its letterbox margin).
//
// ---- THE CONTROLS, AFTER THE TOUCH PASS (T14) ----
// The two centre LARGE↔PRECISE toggles are REAL: they are the clusters' own magnitude selector, both
// states are in the iss-sim spec this page was built from, and selecting one is screen state — nothing
// flies. Tapping either flips it and the plate re-labels itself, per screen.
//
// The twelve DIRECTION buttons are not, and deliberately: pressing one would fire RCS, which is flying
// the vehicle. BUILD_PLAN §14.4(a) settles that class — "Flight/actuation … = honest no-op in the
// screens-only build until Part B wires them" — so they resolve to their own act, log once, and do
// nothing: no light, no action, and no red. They are a named seam, not a dead rect: Part B (§B12.5)
// replaces the dispatch without touching the geometry or the drawing. ⚠ WHETHER they should instead fly
// the capsule by hand (the owner's "hidden mini-game") is an OPEN OWNER DECISION, not this task's to
// take — see the register.
//
// "Settings" opens the settings page, the same destination the Cover's own Settings button has.
// "Instructions" and "Reset Positions" have nothing behind them, for two DIFFERENT recorded reasons
// (S29, owner via the overseer, 2026-09-02 — confirming rather than changing what T14 already built):
// "Instructions" is simply content this build does not have — no source gives it a body, so it stays
// inert with nothing to be actuation OR screen-state (not in IsActuation). "Reset Positions" IS actuation
// — the reference does not say whether it resets the VEHICLE (flying it, §14.4(a) no-op) or only the
// PAGE'S OWN VIEW (screen state, could act) — so per §1.4 it stays classified the conservative way, the
// same no-op as the twelve direction pads, until a source confirms which. Neither is invented open;
// see REGISTER.md S29.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class DockingSimPage
    {
        public const int Commands = 200;
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Dim    = DragonPalette.Text6;
        static readonly Rgba Faint  = DragonPalette.Text7;
        static readonly Rgba Accent = DragonPalette.Accent;
        static readonly Rgba Go     = DragonPalette.Go;
        static readonly Rgba Hair   = DragonPalette.Hairline;
        static readonly Rgba Panel  = DragonPalette.Panel;

        const float HCX = 1713f, HCY = 900f, R1 = 600f, R2 = 388f;
        const string Dash = "—";     // no target / no feed — never a plausible zero

        /// <summary>FULL SCALE for the target diamond, STATED (S26, no source gives the real ring a
        /// number — the same position VehicleSubsystemPage.RateFullScaleDps was in): the diamond reaches
        /// the INNER ring (R2, the one the crosshair sits inside of) at this many degrees of pitch/yaw
        /// pointing error, and PEGS there for anything larger — a rough capture attempt pins the diamond
        /// at the ring edge instead of flying off the HUD, the same "peg rather than escape the dial"
        /// rule RateFullScaleDps uses. 8° gives the diamond real travel for the few-degree corrections a
        /// final approach actually makes.</summary>
        public const float RingFullScaleDeg = 8f;

        /// <summary>How close a pointing error must be to zero to read as CORRECTED — iss-sim: the axis
        /// goes GREEN when corrected (SCREEN_INVENTORY #11). STATED, not sourced: tight enough that
        /// "green" still means aligned, loose enough that RollDegText/PitchDegText/YawDegText's own
        /// one-decimal rounding doesn't make the colour flicker at the boundary.</summary>
        public const float CorrectedToleranceDeg = 0.5f;

        static float Clamp11(double v)
        {
            if (v > 1.0) return 1f;
            if (v < -1.0) return -1f;
            return (float)v;
        }

        public static void Build(DisplayList dl, int w, int h, PageState s)
        { Build(dl, w, h, s, PageControls.Default); }

        /// <summary>As Build, told where the two cluster magnitude toggles are set (T14). The painter owns
        /// that state per screen, exactly as it owns the Cover's camera and the suit countdown.</summary>
        public static void Build(DisplayList dl, int w, int h, PageState s, PageControls ctl)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f; if (ox < 0f) ox = 0f;
            float X(float x) => x * sc + ox;
            float Y(float y) => y * sc;
            float Z(float v) => v * sc;
            int St(float rs) { int p = (int)Math.Round(rs * sc); return p < 1 ? 1 : p; }
            void L(string t, float x, float y, float z, Rgba c) => dl.Text(t, X(x), Y(y), Z(z), TextAlign.Left, c);
            void C(string t, float cx, float y, float z, Rgba c) => dl.Text(t, X(cx), Y(y), Z(z), TextAlign.Centre, c);

            dl.Rect(0, 0, w, h, DragonPalette.Background);
            C("MANUAL DOCKING", HCX, 60, 26, Accent);

            // ---- HUD: two concentric rings + graticule ticks + centre reticle ----
            dl.ArcBand(X(HCX), Y(HCY), Z(R1 - 4), Z(R1), 0, 360, DragonPalette.AccentDim);
            dl.ArcBand(X(HCX), Y(HCY), Z(R2 - 3), Z(R2), 0, 360, Faint);
            for (int i = 0; i < 12; i++)
            {
                double a = i * Math.PI / 6.0;
                float cs = (float)Math.Cos(a), sn = (float)Math.Sin(a);   // not c/s: the page now takes a PageState s
                dl.Line(X(HCX + cs * (R1 - 30)), Y(HCY + sn * (R1 - 30)), X(HCX + cs * R1), Y(HCY + sn * R1), St(2), Faint);
            }
            // No target: there is nothing to be misaligned WITH or off-boresight from, so every readout
            // below dashes and the diamond does not appear at all.
            bool tgt = s.Valid && s.HasTarget;
            string A(string t) => (tgt && !string.IsNullOrEmpty(t)) ? t : Dash;

            // green target diamond — placed from the LIVE pitch/yaw bearing (S26), not a fixed offset.
            // Sign: positive yaw (target toward our +right) moves the diamond right; positive pitch
            // (target toward our +forward) moves it up. Full scale is RingFullScaleDeg, pegged at the
            // inner ring (R2) past that — see the constant's own comment for why 8°.
            bool haveBearing = tgt && !string.IsNullOrEmpty(s.PitchDegText) && !string.IsNullOrEmpty(s.YawDegText);
            if (haveBearing)
            {
                float yawOff = Clamp11(s.YawDeg / RingFullScaleDeg) * R2;
                float pitchOff = Clamp11(s.PitchDeg / RingFullScaleDeg) * R2;
                float tx = HCX + yawOff, ty = HCY - pitchOff, d = 26f;
                dl.Line(X(tx), Y(ty - d), X(tx + d), Y(ty), St(3), Go); dl.Line(X(tx + d), Y(ty), X(tx), Y(ty + d), St(3), Go);
                dl.Line(X(tx), Y(ty + d), X(tx - d), Y(ty), St(3), Go); dl.Line(X(tx - d), Y(ty), X(tx), Y(ty - d), St(3), Go);
            }
            // The centre reticle is not target-dependent — it is our own boresight, always at centre.
            TargetReticle.Crosshair(dl, X(HCX), Y(HCY), Z(60), DragonPalette.Text2);

            // ---- axis readouts around the rings: the pitch/yaw/roll CORRECTION, LIVE (T13c) ----
            // GREEN when that axis is within CorrectedToleranceDeg of zero ("corrected", iss-sim:
            // SCREEN_INVENTORY #11), WHITE while a target exists but the axis is not yet aligned, DIM
            // with no target — not a blanket green whenever a target exists regardless of how far off.
            Rgba RingTint(double deg) => !tgt ? Dim : (Math.Abs(deg) <= CorrectedToleranceDeg ? Go : White);
            string roll = A(s.RollDegText), pitch = A(s.PitchDegText), yaw = A(s.YawDegText);
            Rgba rollTint = RingTint(s.RollDeg), pitchTint = RingTint(s.PitchDeg), yawTint = RingTint(s.YawDeg);
            C("ROLL", HCX, HCY - R1 - 96, 26, Dim);  C(roll, HCX, HCY - R1 - 60, 40, rollTint);
            C("YAW", HCX, HCY + R1 + 30, 26, Dim);   C(yaw, HCX, HCY + R1 + 66, 40, yawTint);
            L("PITCH", HCX + R1 + 44, HCY - 34, 26, Dim); L(pitch, HCX + R1 + 44, HCY + 2, 40, pitchTint);

            // ---- PYR block (left): the OTHER iss-sim-confirmed quantity, BLUE per-axis RATE (S26) ----
            // This used to redraw the same correction the ring already shows (SCREEN_EVIDENCE_MATRIX
            // describes ONE rotation-readout group, not two). Dropping it would lose a confirmed iss-sim
            // quantity this build already has wired (PitchRateText/YawRateText/RollRateText, vehicle body
            // rates, T13b) — DockingPage.cs's "GREEN correction / BLUE rate" scheme, its own header names
            // as iss-sim's key design takeaway, is exactly this: one axis, two numbers, two colours. So
            // the ring keeps the correction and PYR becomes the rate, rather than two of the same number.
            L("PYR", HCX - R1 - 230, HCY - 118, 24, Accent);
            string[] pyr = { A(s.PitchRateText), A(s.YawRateText), A(s.RollRateText) };
            Rgba rateTint = tgt ? Accent : Dim;
            for (int i = 0; i < 3; i++)
                L(pyr[i], HCX - R1 - 230, HCY - 64 + i * 60, 40, rateTint);

            // ---- RANGE / RATE (below the rings): LIVE (T13c) ----
            // The same RangeText/RateText the attitude HUD draws — one range and one closing rate in the
            // build, not a second pair that could disagree. RATE is signed and closing is NEGATIVE
            // (VesselData's stated convention), which is what the reference's own "-0.2 m/s" showed.
            C("RANGE", HCX - 260, 1590, 24, Dim);
            C(A(s.RangeText), HCX - 260, 1626, 44, tgt ? White : Dim);
            C("RATE", HCX + 260, 1590, 24, Dim);
            C(A(s.RateText), HCX + 260, 1626, 44, tgt ? Accent : Dim);

            // ---- control clusters ----
            Cluster(dl, X, Y, Z, St, RotCx, ClusterCy, "ROTATION", ctl.DockRotLarge,
                    "ROLL", "ROLL", "PITCH", "PITCH", "YAW", "YAW", "▲", "▼", "◄", "►");
            Cluster(dl, X, Y, Z, St, TransCx, ClusterCy, "TRANSLATION", ctl.DockTransLarge,
                    "FWD", "BACK", "UP", "DOWN", "LEFT", "RIGHT", "", "", "", "");

            // ---- bottom controls ----
            for (int i = 0; i < BottomLabel.Length; i++)
            {
                float bx, by, bw, bh;
                BottomRect(i, w, h, out bx, out by, out bw, out bh);
                dl.Box(bx, by, bw, bh, St(2), Hair);
                // S106 / QC DK-02: `Settings` routes (FigmaUI sends it to the settings page) and stays
                // White; `Instructions` and `Reset Positions` do not, for the two different recorded
                // reasons in this file's header, so they take the inert tint.
                C(BottomLabel[i], BottomCx[i], 1742, 26,
                  BottomLabel[i] == "Settings" ? White : Dim);
            }

            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame
        }

        // a rotation/translation control cluster: a plus of direction buttons around a centre magnitude
        // toggle. Corner labels (cUL/cUR) name the extra axis (Roll pair / Fwd-Back); edge labels the axes.
        // `large` is the centre toggle's own state — LARGE or PRECISE, the two the iss-sim spec names.
        static void Cluster(DisplayList dl, Func<float,float> X, Func<float,float> Y, Func<float,float> Z,
                             Func<float,int> St, float ccx, float ccy, string title, bool large,
                             string cUL, string cUR, string top, string bot, string lft, string rgt,
                             string aTop, string aBot, string aLft, string aRgt)
        {
            void Btn(float gx, float gy, string a, string b)
            {
                if (a.Length == 0 && b.Length == 0) return;
                dl.Box(X(ccx + gx * Cell - Btw * 0.5f), Y(ccy + gy * Cell - Btw * 0.5f), Z(Btw), Z(Btw), St(2), DragonPalette.Hairline);
                // ---- S106 / QC DK-02 + DK-01: TWELVE INERT PADS, DRAWN AS ONE THING ----
                // Every direction pad is a 14.4(a) no-op with a recorded reason (S29 + T14 + S85), and
                // all twelve were drawn White - indistinguishable from the two magnitude toggles, which
                // are the only controls on this page that act and are drawn in `Accent`. The page had a
                // distinguishing tint and was spending it on the wrong half.
                // ⚠ This also closes DK-01. The `a` slot was White and the `b` slot Text6, so ROTATION
                // (arrows in `a`, axis words in `b`) read coherently while TRANSLATION - which has no
                // arrows, so UP/DOWN/LEFT/RIGHT fell into `b` - had four pads faint and their two corner
                // siblings FWD/BACK bright, for six controls that behave identically. One tint, one
                // weight, and the inconsistency has nowhere left to live.
                // ⛔ The hit rects STAY. S85's `rec.Acted = false` record depends on the press being
                // received and logged - "the record that lets a flight prove a direction pad was pressed
                // and flew nothing". Inert here means drawn as not acting, not un-hit-testable.
                if (a.Length > 0) dl.Text(a, X(ccx + gx * Cell), Y(ccy + gy * Cell - (b.Length > 0 ? 34f : 18f)), Z(30), TextAlign.Centre, DragonPalette.Text6);
                if (b.Length > 0) dl.Text(b, X(ccx + gx * Cell), Y(ccy + gy * Cell + 6f), Z(22), TextAlign.Centre, DragonPalette.Text6);
            }
            dl.Text(title, X(ccx), Y(ccy - Cell - 90f), Z(28), TextAlign.Centre, DragonPalette.Accent);
            Btn(-1, -1, cUL, "");            // top-left  (Roll ↺ / Fwd)
            Btn( 1, -1, cUR, "");            // top-right (Roll ↻ / Back)
            Btn( 0, -1, aTop, top);          // up
            Btn( 0,  1, aBot, bot);          // down
            Btn(-1,  0, aLft, lft);          // left
            Btn( 1,  0, aRgt, rgt);          // right
            // centre magnitude toggle — the one control in this cluster that acts (T14)
            dl.Rect(X(ccx - Btw * 0.5f), Y(ccy - Btw * 0.5f), Z(Btw), Z(Btw), DragonPalette.Panel);
            dl.Box(X(ccx - Btw * 0.5f), Y(ccy - Btw * 0.5f), Z(Btw), Z(Btw), St(2), DragonPalette.Accent);
            dl.Text(large ? "LARGE" : "PRECISE", X(ccx), Y(ccy - 16f), Z(large ? 26f : 22f),
                    TextAlign.Centre, DragonPalette.Accent);
        }

        // ============================================================================================
        // INTERACTIVITY (T14) — the rects below are the ones Cluster and Build DRAW. One source, per
        // PageAction's rule: a control that is hit somewhere other than where it is drawn is invisible
        // in a PNG and only ever found in the capsule, at the cost of a restart.
        // ============================================================================================
        const float Cell = 150f, Btw = 128f;
        const float RotCx = 560f, TransCx = 2867f, ClusterCy = 980f;
        static readonly string[] BottomLabel = { "Instructions", "Reset Positions", "Settings" };
        static readonly float[] BottomCx = { 1360f, 1713f, 2066f };
        const float BottomY = 1720f, BottomW = 300f, BottomH = 74f;

        /// <summary>What a touch on this page resolved to. The twelve direction acts exist so the touch is
        /// ROUTED and testable even while §14.4(a) says the vehicle must not move for them.</summary>
        public enum DockAct : byte
        {
            None = 0,
            RotRollCcw, RotRollCw, RotPitchUp, RotPitchDown, RotYawLeft, RotYawRight, RotMagnitude,
            TransFwd, TransBack, TransUp, TransDown, TransLeft, TransRight, TransMagnitude,
            Instructions, ResetPositions, Settings
        }

        /// <summary>True for the acts that would MOVE the vehicle — §14.4(a) flight actuation, an honest
        /// no-op until Part B. One predicate so the glue, the tests and any later wiring agree on the
        /// membership of that set rather than each keeping a list.</summary>
        public static bool IsActuation(DockAct a)
        {
            switch (a)
            {
                case DockAct.RotRollCcw: case DockAct.RotRollCw:
                case DockAct.RotPitchUp: case DockAct.RotPitchDown:
                case DockAct.RotYawLeft: case DockAct.RotYawRight:
                case DockAct.TransFwd: case DockAct.TransBack:
                case DockAct.TransUp: case DockAct.TransDown:
                case DockAct.TransLeft: case DockAct.TransRight:
                case DockAct.ResetPositions:
                    return true;
                default: return false;
            }
        }

        // grid slot -> the act each cluster's button carries, in the order Cluster draws them.
        static readonly float[] Gx = { -1f,  1f,  0f, 0f, -1f, 1f, 0f };
        static readonly float[] Gy = { -1f, -1f, -1f, 1f,  0f, 0f, 0f };
        static readonly DockAct[] RotAct = {
            DockAct.RotRollCcw, DockAct.RotRollCw, DockAct.RotPitchUp, DockAct.RotPitchDown,
            DockAct.RotYawLeft, DockAct.RotYawRight, DockAct.RotMagnitude };
        static readonly DockAct[] TransAct = {
            DockAct.TransFwd, DockAct.TransBack, DockAct.TransUp, DockAct.TransDown,
            DockAct.TransLeft, DockAct.TransRight, DockAct.TransMagnitude };

        /// <summary>Cluster button <paramref name="slot"/>'s box (0..6, the Cluster draw order; 6 is the
        /// centre toggle), in PANEL pixels. <paramref name="rotation"/> picks which cluster.</summary>
        public static void ClusterRect(bool rotation, int slot, int w, int h,
                                       out float x, out float y, out float bw, out float bh)
        {
            x = y = bw = bh = 0f;
            if (slot < 0 || slot >= Gx.Length || h <= 0) return;
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f; if (ox < 0f) ox = 0f;
            float ccx = rotation ? RotCx : TransCx;
            x = (ccx + Gx[slot] * Cell - Btw * 0.5f) * sc + ox;
            y = (ClusterCy + Gy[slot] * Cell - Btw * 0.5f) * sc;
            bw = Btw * sc; bh = Btw * sc;
        }

        /// <summary>Bottom control <paramref name="i"/>'s box (0 Instructions, 1 Reset Positions,
        /// 2 Settings), in PANEL pixels.</summary>
        public static void BottomRect(int i, int w, int h, out float x, out float y, out float bw, out float bh)
        {
            x = y = bw = bh = 0f;
            if (i < 0 || i >= BottomLabel.Length || h <= 0) return;
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f; if (ox < 0f) ox = 0f;
            x = (BottomCx[i] - BottomW * 0.5f) * sc + ox;
            y = BottomY * sc; bw = BottomW * sc; bh = BottomH * sc;
        }

        public static DockAct HitTest(float px, float py, int w, int h)
        {
            for (int slot = 0; slot < Gx.Length; slot++)
            {
                float x, y, bw, bh;
                ClusterRect(true, slot, w, h, out x, out y, out bw, out bh);
                if (Control.Hit(px, py, x, y, bw, bh)) return RotAct[slot];
                ClusterRect(false, slot, w, h, out x, out y, out bw, out bh);
                if (Control.Hit(px, py, x, y, bw, bh)) return TransAct[slot];
            }
            for (int i = 0; i < BottomLabel.Length; i++)
            {
                float x, y, bw, bh;
                BottomRect(i, w, h, out x, out y, out bw, out bh);
                if (Control.Hit(px, py, x, y, bw, bh))
                    return i == 0 ? DockAct.Instructions : i == 1 ? DockAct.ResetPositions : DockAct.Settings;
            }
            return DockAct.None;
        }
    }
}
