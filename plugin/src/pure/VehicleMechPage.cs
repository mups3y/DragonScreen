// DragonScreen — VehicleMechPage  (PURE: the "MECH PANEL", the Vehicle page's Mech sub-tab)
// ============================================================================================
// The reference UI's Mech view (components/Mech.vue) is a radial schematic: a central ring holding the
// seat tachometers, ringed by the mechanical nodes (ACCELERATION / CENTRIPETAL / PRESSURE / RESISTANCE
// / WATER UPRIGHTING), each a donut with a big count. Reproduced here as spokes + donut nodes + the
// central SEATS block, with the Overview/Mech tab strip (Mech active). Values are representative, as
// the demo's are (randomised ~7x,xxx); the real mechanism telemetry replaces them later.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class VehicleMechPage
    {
        public const int Commands = 200;
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Bg     = DragonPalette.Background;
        static readonly Rgba Accent = DragonPalette.Accent;
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Dim    = DragonPalette.Text6;
        static readonly Rgba Faint  = DragonPalette.Text7;
        static readonly Rgba Hair   = DragonPalette.Hairline;
        static readonly Rgba Amber  = DragonPalette.Caution;

        // outer nodes: label | value | angle (deg, 0 = top, clockwise)
        static readonly string[] NodeLabel = { "ACCELERATION", "CENTRIPETAL", "PRESSURE", "RESISTANCE", "WATER UPRIGHTING" };
        static readonly string[] NodeValue = { "79610.01", "71367.02", "73225.03", "75169.04", "71228.05" };
        static readonly float[]  NodeAng   = { 0f, 72f, 144f, 216f, 288f };

        public static void Build(DisplayList dl, int w, int h)
        {
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            int St(float rs) { int p = (int)Math.Round(rs * sy); return p < 1 ? 1 : p; }
            void C(string t, float cx, float y, float s, Rgba c) => dl.Text(t, PX(cx), PY(y), SZ(s), TextAlign.Centre, c);
            void L(string t, float x, float y, float s, Rgba c) => dl.Text(t, PX(x), PY(y), SZ(s), TextAlign.Left, c);
            void R(string t, float rx, float y, float s, Rgba c) => dl.Text(t, PX(rx), PY(y), SZ(s), TextAlign.Right, c);

            dl.Rect(0, 0, w, h, Bg);
            C("MECH PANEL", 1713, 40, 46, White);

            const float ccx = 1713f, ccy = 1040f, ring = 440f, nodeR = 740f, donut = 110f;

            // spokes + central ring
            for (int i = 0; i < NodeLabel.Length; i++)
            {
                double a = NodeAng[i] * Math.PI / 180.0;
                float nx = ccx + (float)Math.Sin(a) * nodeR, ny = ccy - (float)Math.Cos(a) * nodeR;
                float ex = ccx + (float)Math.Sin(a) * ring, ey = ccy - (float)Math.Cos(a) * ring;
                dl.Line(PX(ex), PY(ey), PX(nx), PY(ny), St(2), Hair);
            }
            dl.ArcBand(PX(ccx), PY(ccy), SZ(ring - 5), SZ(ring), 0, 360, Faint);

            // central SEATS tachometers
            C("SEATS", ccx, ccy - 190, 30, Accent);
            string[] seat = { "SEAT 1 TACH", "SEAT 2 TACH", "SEAT 3 TACH", "SEAT 4 TACH" };
            string[] tach = { "1204", "1198", "1211", "1207" };
            for (int i = 0; i < 4; i++)
            {
                L(seat[i], ccx - 220, ccy - 120 + i * 80, 26, White);
                R(tach[i], ccx + 220, ccy - 120 + i * 80, 26, Dim);
            }
            C("ALL SYSTEMS CHECK", ccx, ccy + 250, 24, Dim);
            C("Awaiting", ccx, ccy + 290, 26, Amber);

            // outer donut nodes
            for (int i = 0; i < NodeLabel.Length; i++)
            {
                double a = NodeAng[i] * Math.PI / 180.0;
                float nx = ccx + (float)Math.Sin(a) * nodeR, ny = ccy - (float)Math.Cos(a) * nodeR;
                dl.ArcBand(PX(nx), PY(ny), SZ(donut - 8), SZ(donut), 0, 360, Faint);
                dl.ArcBand(PX(nx), PY(ny), SZ(donut - 8), SZ(donut), -140, 100, Accent);
                C(NodeLabel[i], nx, ny - donut - 40, 24, White);
                C(NodeValue[i], nx, ny - 22, 34, White);
            }

            // ---- subsystem tab bar (Mech active) + bottom status bar ----
            VehicleTabBar.Draw(dl, w, h, 3);
            dl.Asset("component_48", 0f, PY(1877), w, SZ(235), White);
        }
    }
}
