// DragonScreen — DockingSimPage  (PURE: the manual "ISS Docking" prox-ops screen)
// ============================================================================================
// The manual docking screen — distinct from the attitude HUD (Frame 58). Specced from the live
// iss-sim.spacex.com DOM (the sim SpaceX built from the real UI; its own "actual interface" link points
// to the training video): two concentric HUD rings + centre reticle over the docking-adapter view, a
// green target diamond, the ROLL / PITCH / YAW readouts + a PYR block, RANGE + RATE, a ROTATION control
// cluster (Roll/Pitch/Yaw) and a TRANSLATION cluster (Up/Down/Left/Right/Fwd/Back) each with a centre
// LARGE↔precise toggle, and Instructions / Reset Positions / Settings.
//
// Reached from the attitude HUD (a "MANUAL DOCKING" affordance in its letterbox margin). Controls are
// display-only for now — wiring them to RCS (the owner's "hidden mini-game" idea) is a later decision.
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

        public static void Build(DisplayList dl, int w, int h)
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
                float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
                dl.Line(X(HCX + c * (R1 - 30)), Y(HCY + s * (R1 - 30)), X(HCX + c * R1), Y(HCY + s * R1), St(2), Faint);
            }
            // green target diamond (offset from centre) + centre reticle
            float tx = HCX + 70f, ty = HCY - 48f, d = 26f;
            dl.Line(X(tx), Y(ty - d), X(tx + d), Y(ty), St(3), Go); dl.Line(X(tx + d), Y(ty), X(tx), Y(ty + d), St(3), Go);
            dl.Line(X(tx), Y(ty + d), X(tx - d), Y(ty), St(3), Go); dl.Line(X(tx - d), Y(ty), X(tx), Y(ty - d), St(3), Go);
            TargetReticle.Crosshair(dl, X(HCX), Y(HCY), Z(60), DragonPalette.Text2);

            // ---- axis readouts around the rings ----
            C("ROLL", HCX, HCY - R1 - 96, 26, Dim);  C("0.0°", HCX, HCY - R1 - 60, 40, Go);
            C("YAW", HCX, HCY + R1 + 30, 26, Dim);   C("0.0°", HCX, HCY + R1 + 66, 40, Go);
            L("PITCH", HCX + R1 + 44, HCY - 34, 26, Dim); L("0.0°", HCX + R1 + 44, HCY + 2, 40, Go);
            // PYR block (left) — the three axis errors
            L("PYR", HCX - R1 - 230, HCY - 118, 24, Accent);
            string[] pyr = { "180.0", "180.0", "180.0" };
            for (int i = 0; i < 3; i++) L(pyr[i], HCX - R1 - 230, HCY - 64 + i * 60, 40, White);

            // ---- RANGE / RATE (below the rings) ----
            C("RANGE", HCX - 260, 1590, 24, Dim);  C("11.6 m", HCX - 260, 1626, 44, White);
            C("RATE", HCX + 260, 1590, 24, Dim);   C("-0.2 m/s", HCX + 260, 1626, 44, Accent);

            // ---- control clusters ----
            Cluster(dl, X, Y, Z, St, 560f, 980f, "ROTATION",
                    "ROLL", "ROLL", "PITCH", "PITCH", "YAW", "YAW", "▲", "▼", "◄", "►");
            Cluster(dl, X, Y, Z, St, 2867f, 980f, "TRANSLATION",
                    "FWD", "BACK", "UP", "DOWN", "LEFT", "RIGHT", "", "", "", "");

            // ---- bottom controls ----
            string[] ctl = { "Instructions", "Reset Positions", "Settings" };
            float[] cx = { 1360f, 1713f, 2066f };
            for (int i = 0; i < ctl.Length; i++)
            {
                dl.Box(X(cx[i] - 150), Y(1720), Z(300), Z(74), St(2), Hair);
                C(ctl[i], cx[i], 1742, 26, White);
            }

            dl.Asset("component_48", 0f, Y(1877), w, Z(235), White);
        }

        // a rotation/translation control cluster: a plus of direction buttons around a centre precision
        // toggle. Corner labels (cUL/cUR) name the extra axis (Roll pair / Fwd-Back); edge labels the axes.
        static void Cluster(DisplayList dl, Func<float,float> X, Func<float,float> Y, Func<float,float> Z,
                             Func<float,int> St, float ccx, float ccy, string title,
                             string cUL, string cUR, string top, string bot, string lft, string rgt,
                             string aTop, string aBot, string aLft, string aRgt)
        {
            const float cell = 150f, bw = 128f;
            void Btn(float gx, float gy, string a, string b)
            {
                if (a.Length == 0 && b.Length == 0) return;
                dl.Box(X(ccx + gx * cell - bw * 0.5f), Y(ccy + gy * cell - bw * 0.5f), Z(bw), Z(bw), St(2), DragonPalette.Hairline);
                if (a.Length > 0) dl.Text(a, X(ccx + gx * cell), Y(ccy + gy * cell - (b.Length > 0 ? 34f : 18f)), Z(30), TextAlign.Centre, DragonPalette.White);
                if (b.Length > 0) dl.Text(b, X(ccx + gx * cell), Y(ccy + gy * cell + 6f), Z(22), TextAlign.Centre, DragonPalette.Text6);
            }
            dl.Text(title, X(ccx), Y(ccy - cell - 90f), Z(28), TextAlign.Centre, DragonPalette.Accent);
            Btn(-1, -1, cUL, "");            // top-left  (Roll ↺ / Fwd)
            Btn( 1, -1, cUR, "");            // top-right (Roll ↻ / Back)
            Btn( 0, -1, aTop, top);          // up
            Btn( 0,  1, aBot, bot);          // down
            Btn(-1,  0, aLft, lft);          // left
            Btn( 1,  0, aRgt, rgt);          // right
            // centre precision toggle
            dl.Rect(X(ccx - bw * 0.5f), Y(ccy - bw * 0.5f), Z(bw), Z(bw), DragonPalette.Panel);
            dl.Box(X(ccx - bw * 0.5f), Y(ccy - bw * 0.5f), Z(bw), Z(bw), St(2), DragonPalette.Accent);
            dl.Text("LARGE", X(ccx), Y(ccy - 16f), Z(26), TextAlign.Centre, DragonPalette.Accent);
        }
    }
}
