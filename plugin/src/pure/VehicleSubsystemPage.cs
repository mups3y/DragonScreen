// DragonScreen — VehicleSubsystemPage  (PURE: the Vehicle page's subsystem sub-tabs)
// ============================================================================================
// The real Crew Dragon Vehicle page has eight subsystem sub-tabs (see VehicleTabBar). Two are already
// their own pages — "All" (VehicleOverviewPage) and "Mech" (VehicleMechPage). This builds the other
// six — Crew, Prop, Power, Avionics, GNC, Thermal — from ONE template so they read as one family, the
// way the real pages share chrome and differ only in the subsystem's data + which tab is lit. There is
// no Figma frame for these; the layout follows the confirmed real grammar (blue capsule line-art
// surrounded by large numeric readouts — SCREEN_EVIDENCE_MATRIX) mapped onto the Vehicle Overview's
// three-zone form: LEFT subsystem checklist · CENTRE capsule + four headline gauges · RIGHT detail
// readouts · the shared subsystem tab bar. Values are representative, like the overview's; the real
// vessel telemetry replaces them in a later pass.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class VehicleSubsystemPage
    {
        /// <summary>The six subsystem sub-tabs that share this template (All + Mech are their own pages).</summary>
        public enum Sub { Crew, Propulsion, Power, Avionics, Gnc, Thermal }

        public const int Commands = 300;
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Bg     = DragonPalette.Background;
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

        // ---- one subsystem's content ----
        struct Sys
        {
            public string Title; public int Tab;
            public string[] CkLabel, CkState; public int[] CkKey;                 // left checklist
            public string[] GLabel, GVal, GUnit; public float[] GFrac; public Rgba[] GCol; // 4 headline gauges
            public string[] RLabel, RVal; public float[] RFrac;                   // right detail readouts
        }

        public static void Build(DisplayList dl, int w, int h, Sub sub)
        {
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            void L(string t, float x, float y, float s, Rgba c) => dl.Text(t, PX(x), PY(y), SZ(s), TextAlign.Left, c);
            void C(string t, float cx, float y, float s, Rgba c) => dl.Text(t, PX(cx), PY(y), SZ(s), TextAlign.Centre, c);
            void R(string t, float rx, float y, float s, Rgba c) => dl.Text(t, PX(rx), PY(y), SZ(s), TextAlign.Right, c);

            // 300° gauge (60° gap at the bottom), value + unit centred, label above — the overview's gauge.
            void Gauge(float cxd, float cyd, float rd, float frac, Rgba col, string label, string val, string unit)
            {
                float cx = PX(cxd), cy = PY(cyd), r = SZ(rd), rw = SZ(rd * 0.16f);
                dl.ArcBand(cx, cy, r - rw, r, -150, 150, Faint);
                if (frac > 0f) dl.ArcBand(cx, cy, r - rw, r, -150, -150 + 300f * (frac > 1f ? 1f : frac), col);
                C(label, cxd, cyd - rd - 44f, 24, Dim);
                C(val, cxd, cyd - rd * 0.34f, rd * 0.42f, White);
                C(unit, cxd, cyd + rd * 0.30f, 24, Dim);
            }

            Sys d = DefOf(sub);

            dl.Rect(0, 0, w, h, Bg);
            C(d.Title, 1713, 40, 46, White);

            // ---- LEFT: subsystem checklist ----
            for (int i = 0; i < d.CkLabel.Length; i++)
            {
                float y = 300 + i * 195;
                Rgba sc = d.CkKey[i] == 1 ? Go : d.CkKey[i] == 2 ? Amber : White;
                dl.Asset("ic_check", PX(90), PY(y), SZ(38), SZ(38), sc);
                L(d.CkLabel[i], 150, y + 4, 28, White);
                L(d.CkState[i], 150, y + 48, 26, sc);
            }

            // ---- CENTRE-TOP: four headline gauges ----
            float[] gx = { 1170f, 1620f, 2070f, 2520f };
            for (int i = 0; i < d.GLabel.Length && i < 4; i++)
                Gauge(gx[i], 470, 170, d.GFrac[i], d.GCol[i], d.GLabel[i], d.GVal[i], d.GUnit[i]);

            // ---- CENTRE: capsule diagram (the vehicle, on every vehicle page) ----
            dl.Asset("dragon_crew", PX(1453), PY(760), 520 * sx, 760 * sy, White);

            // ---- RIGHT: detail readouts (label · value · bar) ----
            for (int i = 0; i < d.RLabel.Length; i++)
            {
                float y = 340 + i * 250;
                L(d.RLabel[i], 2760, y, 28, Dim);
                R(d.RVal[i], 3360, y, 34, White);
                dl.Rect(PX(2760), PY(y + 70), 600 * sx, SZ(8), Faint);
                float f = d.RFrac[i] > 1f ? 1f : (d.RFrac[i] < 0f ? 0f : d.RFrac[i]);
                if (f > 0f) dl.Rect(PX(2760), PY(y + 70), 600 * sx * f, SZ(8), Accent);
            }

            // ---- subsystem tab bar + global bottom bar ----
            VehicleTabBar.Draw(dl, w, h, d.Tab);
            dl.Asset("component_48", 0f, PY(1877), w, SZ(235), White);
        }

        // ---- per-subsystem content. Representative values, as the reference demo's are. ----
        static Sys DefOf(Sub sub)
        {
            Sys s = new Sys();
            switch (sub)
            {
                case Sub.Crew:
                    s.Title = "CREW · LIFE SUPPORT"; s.Tab = 1;
                    s.CkLabel = new[] { "CABIN ATMOSPHERE", "O2 SUPPLY", "CO2 SCRUBBER", "SUIT LOOP", "WATER SYSTEM", "SMOKE DETECT" };
                    s.CkState = new[] { "Nominal", "Nominal", "Active", "Standby", "Nominal", "Clear" };
                    s.CkKey   = new[] { 1, 1, 1, 0, 1, 1 };
                    s.GLabel  = new[] { "PPO2", "CABIN TEMP", "CABIN PRESS", "CO2" };
                    s.GVal    = new[] { "2.69", "22.4", "14.7", "1.05" };
                    s.GUnit   = new[] { "psia", "°C", "psia", "mmHg" };
                    s.GFrac   = new[] { 0.60f, 0.55f, 0.72f, 0.30f };
                    s.GCol    = new[] { Gold, Red, Yellow, Blue };
                    s.RLabel  = new[] { "Humidity", "O2 Tank", "N2 Tank", "Potable Water", "Crew Aboard" };
                    s.RVal    = new[] { "44 %", "96 %", "88 %", "72 L", "4 / 4" };
                    s.RFrac   = new[] { 0.44f, 0.96f, 0.88f, 0.60f, 1.00f };
                    break;

                case Sub.Propulsion:
                    s.Title = "PROPULSION"; s.Tab = 2;
                    s.CkLabel = new[] { "DRACO x16", "SUPERDRACO x8", "PROP ISOLATION", "HE PRESSURANT", "OMS / RCS", "MANIFOLD LEAK" };
                    s.CkState = new[] { "16 / 16", "Armed", "Open", "Nominal", "Ready", "None" };
                    s.CkKey   = new[] { 1, 2, 1, 1, 1, 1 };
                    s.GLabel  = new[] { "OX (NTO)", "FUEL (MMH)", "HELIUM", "PROP TEMP" };
                    s.GVal    = new[] { "84", "82", "310", "24.6" };
                    s.GUnit   = new[] { "%", "%", "bar", "°C" };
                    s.GFrac   = new[] { 0.84f, 0.82f, 0.78f, 0.50f };
                    s.GCol    = new[] { Gold, Gold, Blue, Red };
                    s.RLabel  = new[] { "Chamber Press", "Prop Remaining", "Draco Duty", "SuperDraco Temp", "Thrust Avail" };
                    s.RVal    = new[] { "0 psia", "83 %", "0 %", "18 °C", "100 %" };
                    s.RFrac   = new[] { 0.00f, 0.83f, 0.00f, 0.30f, 1.00f };
                    break;

                case Sub.Power:
                    s.Title = "ELECTRICAL POWER"; s.Tab = 4;
                    s.CkLabel = new[] { "MAIN BUS A", "MAIN BUS B", "BATTERIES x4", "SOLAR ARRAY", "PWR DISTRIB", "LOAD SHED" };
                    s.CkState = new[] { "Nominal", "Nominal", "4 / 4", "Deployed", "Nominal", "Off" };
                    s.CkKey   = new[] { 1, 1, 1, 1, 1, 0 };
                    s.GLabel  = new[] { "BATTERY SOC", "BUS A", "BUS B", "ARRAY" };
                    s.GVal    = new[] { "100", "120", "120", "3.4" };
                    s.GUnit   = new[] { "%", "V", "V", "kW" };
                    s.GFrac   = new[] { 1.00f, 0.80f, 0.80f, 0.68f };
                    s.GCol    = new[] { Accent, Accent, Accent, Yellow };
                    s.RLabel  = new[] { "Array Output", "Net Power", "Bus Load", "Battery Temp", "Charge Rate" };
                    s.RVal    = new[] { "3.4 kW", "+68 W", "50 %", "19 °C", "0 kW" };
                    s.RFrac   = new[] { 0.68f, 0.55f, 0.50f, 0.30f, 0.00f };
                    break;

                case Sub.Avionics:
                    s.Title = "AVIONICS"; s.Tab = 5;
                    s.CkLabel = new[] { "FLIGHT COMP x3", "VRIO 1 / 2", "DATA BUS", "GPS", "S-BAND COMMS", "SW WATCHDOG" };
                    s.CkState = new[] { "3 / 3", "Nominal", "Nominal", "Lock", "Linked", "Armed" };
                    s.CkKey   = new[] { 1, 1, 1, 1, 1, 0 };
                    s.GLabel  = new[] { "FC LOAD", "BUS TRAFFIC", "LINK MARGIN", "STORAGE" };
                    s.GVal    = new[] { "38", "42", "8.4", "61" };
                    s.GUnit   = new[] { "%", "%", "dB", "%" };
                    s.GFrac   = new[] { 0.38f, 0.42f, 0.70f, 0.61f };
                    s.GCol    = new[] { Accent, Accent, Go, Blue };
                    s.RLabel  = new[] { "FC1 / 2 / 3", "GPS Sats", "Uplink", "Downlink", "Data Rate" };
                    s.RVal    = new[] { "ONLINE", "11", "Strong", "Strong", "256 kbps" };
                    s.RFrac   = new[] { 1.00f, 0.85f, 0.90f, 0.92f, 0.60f };
                    break;

                case Sub.Gnc:
                    s.Title = "GUIDANCE, NAV & CONTROL"; s.Tab = 6;
                    s.CkLabel = new[] { "IMU 1 / 2", "STAR TRACKERS", "GPS NAV", "RCS AUTHORITY", "NAV STATE", "ATT CONTROL" };
                    s.CkState = new[] { "Nominal", "2 / 2", "Lock", "Enabled", "Valid", "Auto" };
                    s.CkKey   = new[] { 1, 1, 1, 1, 1, 0 };
                    s.GLabel  = new[] { "ROLL RATE", "PITCH RATE", "YAW RATE", "RCS FUEL" };
                    s.GVal    = new[] { "0.02", "0.01", "0.03", "83" };
                    s.GUnit   = new[] { "°/s", "°/s", "°/s", "%" };
                    s.GFrac   = new[] { 0.10f, 0.08f, 0.12f, 0.83f };
                    s.GCol    = new[] { Accent, Accent, Accent, Gold };
                    s.RLabel  = new[] { "Attitude Err", "Body Rate", "Altitude", "Velocity", "Pointing" };
                    s.RVal    = new[] { "0.4°", "0.04 °/s", "380.5 km", "6.68 km/s", "AUTO / SUN" };
                    s.RFrac   = new[] { 0.10f, 0.10f, 0.78f, 0.62f, 0.50f };
                    break;

                default: // Thermal
                    s.Title = "THERMAL CONTROL"; s.Tab = 7;
                    s.CkLabel = new[] { "COOLANT LOOP A", "COOLANT LOOP B", "RADIATORS", "HEAT SHIELD", "HEATERS", "HX FLOW" };
                    s.CkState = new[] { "Nominal", "Nominal", "Deployed", "Nominal", "Auto", "Nominal" };
                    s.CkKey   = new[] { 1, 1, 1, 1, 0, 1 };
                    s.GLabel  = new[] { "LOOP A", "LOOP B", "RADIATOR", "SHIELD" };
                    s.GVal    = new[] { "26.1", "21.1", "8.2", "34" };
                    s.GUnit   = new[] { "°C", "°C", "°C", "°C" };
                    s.GFrac   = new[] { 0.55f, 0.44f, 0.35f, 0.40f };
                    s.GCol    = new[] { Blue, Blue, Accent, Red };
                    s.RLabel  = new[] { "Loop A Flow", "Loop B Flow", "Heat Reject", "Cabin HX", "TPS Max" };
                    s.RVal    = new[] { "1.2 L/s", "1.1 L/s", "3.1 kW", "22 °C", "34 °C" };
                    s.RFrac   = new[] { 0.70f, 0.66f, 0.55f, 0.45f, 0.20f };
                    break;
            }
            return s;
        }
    }
}
