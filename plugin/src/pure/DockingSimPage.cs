// DragonScreen — DockingSimPage  (PURE: the manual "ISS Docking" prox-ops screen)
// ============================================================================================
// The manual docking screen — distinct from the attitude HUD (Frame 58). Specced from the live
// iss-sim.spacex.com DOM (the sim SpaceX built from the real UI; its own "actual interface" link points
// to the training video): two concentric HUD rings + centre reticle over the docking-adapter view, a
// green target diamond, the ROLL / PITCH / YAW readouts + a PYR block, RANGE + RATE, a ROTATION control
// cluster (Roll/Pitch/Yaw) and a TRANSLATION cluster (Up/Down/Left/Right/Fwd/Back) each with a centre
// LARGE↔precise toggle, and Instructions / Reset Positions / Settings.
//
// The READOUTS are live (T13c): ROLL / PITCH / YAW, the PYR block, RANGE and RATE all read PageState —
// the same relative-attitude and closing geometry the attitude HUD draws — and dash with no target,
// because there is nothing to be misaligned with. The green TARGET DIAMOND is still drawn at a fixed
// offset, which now visibly disagrees with the numbers beside it (S26). The centre reticle is not a
// bug: it is our own boresight, and it belongs at the centre.
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
// "Instructions" and "Reset Positions" have nothing behind them (there is no instructions content, and
// resetting a position is actuation), so they take the same honest no-op as the direction pads.
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
            // green target diamond (offset from centre) + centre reticle
            float tx = HCX + 70f, ty = HCY - 48f, d = 26f;
            dl.Line(X(tx), Y(ty - d), X(tx + d), Y(ty), St(3), Go); dl.Line(X(tx + d), Y(ty), X(tx), Y(ty + d), St(3), Go);
            dl.Line(X(tx), Y(ty + d), X(tx - d), Y(ty), St(3), Go); dl.Line(X(tx - d), Y(ty), X(tx), Y(ty - d), St(3), Go);
            TargetReticle.Crosshair(dl, X(HCX), Y(HCY), Z(60), DragonPalette.Text2);

            // ---- axis readouts around the rings, and the PYR block: LIVE (T13c) ----
            // ONE datum, drawn twice. SCREEN_EVIDENCE_MATRIX has the rotation readouts as
            // "ROLL / PITCH / YAW (grouped 'PYR'), each a value in degrees" — the ring labels and the
            // PYR block are the same group, and this page's layout happens to render both. So both read
            // the SAME PageState strings: the placeholder era had them disagreeing (0.0° around the
            // rings, 180.0 in the block), which is precisely the "a needle that disagrees with its own
            // readout" failure T13a's wiring exists to make impossible. VesselData formats
            // Roll/Pitch/YawDegText from the same values the attitude HUD prints as "15.0 deg", so the
            // two docking surfaces cannot drift either. (Whether the page should draw the group twice at
            // all is a LAYOUT question, not a value one — logged as S26, not decided here.)
            // No target: there is nothing to be misaligned WITH, so all three dash.
            bool tgt = s.Valid && s.HasTarget;
            string A(string t) => (tgt && !string.IsNullOrEmpty(t)) ? t : Dash;
            string roll = A(s.RollDegText), pitch = A(s.PitchDegText), yaw = A(s.YawDegText);
            Rgba axisTint = tgt ? Go : Dim;
            C("ROLL", HCX, HCY - R1 - 96, 26, Dim);  C(roll, HCX, HCY - R1 - 60, 40, axisTint);
            C("YAW", HCX, HCY + R1 + 30, 26, Dim);   C(yaw, HCX, HCY + R1 + 66, 40, axisTint);
            L("PITCH", HCX + R1 + 44, HCY - 34, 26, Dim); L(pitch, HCX + R1 + 44, HCY + 2, 40, axisTint);
            // PYR block (left) — the same three axis errors, in the order the label names them.
            L("PYR", HCX - R1 - 230, HCY - 118, 24, Accent);
            string[] pyr = { pitch, yaw, roll };
            for (int i = 0; i < 3; i++)
                L(pyr[i], HCX - R1 - 230, HCY - 64 + i * 60, 40, tgt ? White : Dim);

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
                C(BottomLabel[i], BottomCx[i], 1742, 26, White);
            }

            dl.Asset("component_48", 0f, Y(1877), w, Z(235), White);
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
                if (a.Length > 0) dl.Text(a, X(ccx + gx * Cell), Y(ccy + gy * Cell - (b.Length > 0 ? 34f : 18f)), Z(30), TextAlign.Centre, DragonPalette.White);
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
