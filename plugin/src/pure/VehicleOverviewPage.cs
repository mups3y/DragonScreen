// DragonScreen — VehicleOverviewPage  (PURE: "VEHICLE OVERVIEW", Panel 3 of the reference UI)
// ============================================================================================
// Our Figma set has no vehicle frame, so this is rebuilt from the reference mod (github:
// neel-dandiwala/SpaceX-Dragon2-UI, components/Overview.vue) — its exact layout, palette and copy.
// LEFT = the systems checklist; CENTRE = four cabin-atmosphere gauges (PPO2 / CABIN TEMP / CABIN
// PRESSURE / CO2) over the Dragon (dragon_crew.png, the demo's own art) flanked by the coolant-loop
// and net-power gauges, plus CONNECTIONS and CABIN MICS; RIGHT = the orbit-telemetry bar gauges;
// bottom = the SYSTEMS/CABIN + Overview/Mech tabs and MORE. Values are representative (as the demo's
// are) — the real vessel/atmosphere feed replaces them later. The tabs are shown but not yet live.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class VehicleOverviewPage
    {
        public const int Commands = 300;
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Bg     = DragonPalette.Background;
        static readonly Rgba Panel  = DragonPalette.Panel;
        static readonly Rgba Accent = DragonPalette.Accent;
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Dim    = DragonPalette.Text6;
        static readonly Rgba Faint  = DragonPalette.Text7;
        static readonly Rgba Go     = DragonPalette.Go;
        static readonly Rgba Amber  = DragonPalette.Caution;
        static readonly Rgba Gold   = Rgba.Hex("D7B733");
        static readonly Rgba Red    = Rgba.Hex("D12C30");
        static readonly Rgba Yellow = Rgba.Hex("FCD533");
        static readonly Rgba Blue   = Rgba.Hex("2983ED");

        // left checklist: label | status | status colour key (0 normal, 1 applied/go, 2 awaiting)
        static readonly string[] ChkLabel = {
            "ALL SYSTEMS CHECK", "RENDEZVOUS BURN BLOW", "PREPARE RENDEZVOUS BURN", "THERMAL SHIELD",
            "BURN GOING-GO", "POWER COMPLETION", "STATION DECK CHECK" };
        static readonly string[] ChkState = { "Normal", "Normal", "Normal", "Applied", "Normal", "Awaiting", "Normal" };
        static readonly int[]    ChkKey   = { 0, 0, 0, 1, 0, 2, 0 };

        // right telemetry bars: label | value | fill fraction
        static readonly string[] BarLabel = { "Inertial Velocity", "Altitude", "Apogee", "Perigee", "Inclination", "Range to ISS" };
        static readonly string[] BarValue = { "6.68 km/s", "380.5 km", "377.1 km", "366.1 km", "60.02°", "0.01 km" };
        static readonly float[]  BarFrac  = { 0.62f, 0.78f, 0.80f, 0.74f, 0.67f, 0.05f };

        public static void Build(DisplayList dl, int w, int h)
        {
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            void L(string t, float x, float y, float s, Rgba c) => dl.Text(t, PX(x), PY(y), SZ(s), TextAlign.Left, c);
            void C(string t, float cx, float y, float s, Rgba c) => dl.Text(t, PX(cx), PY(y), SZ(s), TextAlign.Centre, c);
            void R(string t, float rx, float y, float s, Rgba c) => dl.Text(t, PX(rx), PY(y), SZ(s), TextAlign.Right, c);

            // circular gauge: bottom-gap ring (dim) + coloured fill, value + unit centred, label above.
            void Gauge(float cxd, float cyd, float rd, float frac, Rgba col, string label, string val, string unit)
            {
                float cx = PX(cxd), cy = PY(cyd), r = SZ(rd), rw = SZ(rd * 0.16f);
                // 300° arc centred on top (0°), 60° gap at the bottom — like the demo's cut-off gauge.
                dl.ArcBand(cx, cy, r - rw, r, -150, 150, Faint);
                if (frac > 0f) dl.ArcBand(cx, cy, r - rw, r, -150, -150 + 300f * (frac > 1f ? 1f : frac), col);
                C(label, cxd, cyd - rd - 44f, 24, Dim);
                C(val, cxd, cyd - rd * 0.34f, rd * 0.42f, White);
                C(unit, cxd, cyd + rd * 0.30f, 24, Dim);
            }

            dl.Rect(0, 0, w, h, Bg);
            C("VEHICLE OVERVIEW", 1713, 40, 46, White);

            // ---- LEFT: systems checklist ----
            for (int i = 0; i < ChkLabel.Length; i++)
            {
                float y = 300 + i * 200;
                Rgba sc = ChkKey[i] == 1 ? Go : ChkKey[i] == 2 ? Amber : White;
                dl.Asset("ic_check", PX(90), PY(y), SZ(38), SZ(38), sc);
                L(ChkLabel[i], 150, y + 4, 28, White);
                L(ChkState[i], 150, y + 48, 26, sc);
            }

            // ---- CENTRE-TOP: four cabin gauges ----
            Gauge(1170, 430, 175, 0.60f, Gold,   "PPO2",           "2.69", "psia");
            Gauge(1620, 430, 175, 0.52f, Red,    "CABIN TEMP",     "16.43", "°C");
            Gauge(2070, 430, 175, 0.72f, Yellow, "CABIN PRESSURE", "14.0", "psia");
            Gauge(2520, 430, 175, 0.30f, Blue,   "CO2",            "1.05", "mmHg");

            // ---- CENTRE: capsule + loop/power gauges ----
            dl.Asset("dragon_crew", PX(1560), PY(760), 520 * sx, 760 * sy, White);
            Gauge(1230, 900,  120, 0.55f, Blue,   "LOOP A", "26.05", "°C");
            Gauge(1230, 1200, 120, 0.44f, Blue,   "LOOP A", "21.06", "°C");
            Gauge(2410, 900,  120, 0.10f, Accent, "NET PWR1", "0.03", "W");
            Gauge(2410, 1200, 120, 0.62f, Accent, "NET PWR2", "3.02", "W");

            // ---- CONNECTIONS (left of the capsule base) ----
            L("CONNECTIONS", 1130, 1440, 26, Accent);
            string[] cn = { "Manual Rings", "Changelog", "Airlock", "Wing" };
            for (int i = 0; i < 4; i++)
            {
                L(cn[i], 1130, 1500 + i * 56, 24, White);
                L("Connected", 1400, 1500 + i * 56, 24, Go);
            }
            L("CABIN MICS:", 1130, 1748, 26, White); dl.Text("RECORDING", PX(1290), PY(1748), SZ(26), TextAlign.Left, Red);

            // ---- RIGHT: telemetry bar gauges ----
            for (int i = 0; i < BarLabel.Length; i++)
            {
                float y = 320 + i * 250;
                L(BarLabel[i], 2760, y, 28, Dim);
                R(BarValue[i], 3360, y, 34, White);
                dl.Rect(PX(2760), PY(y + 70), 600 * sx, SZ(8), Faint);
                dl.Rect(PX(2760), PY(y + 70), 600 * sx * (BarFrac[i] > 1f ? 1f : BarFrac[i]), SZ(8), Accent);
            }

            // ---- subsystem tab bar (All active) + bottom status bar ----
            // The real Vehicle page carries the eight-subsystem strip (VehicleTabBar); "All" is this
            // overview. It replaces the reference-demo's SYSTEMS/CABIN + Overview/Mech + MORE cluster.
            VehicleTabBar.Draw(dl, w, h, 0);
            dl.Asset("component_48", 0f, PY(1877), w, SZ(235), White);
        }
    }
}
