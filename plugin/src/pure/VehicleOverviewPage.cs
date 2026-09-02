// DragonScreen — VehicleOverviewPage  (PURE: "VEHICLE OVERVIEW", Panel 3 of the reference UI)
// ============================================================================================
// Our Figma set has no vehicle frame, so this is rebuilt from the reference mod (github:
// neel-dandiwala/SpaceX-Dragon2-UI, components/Overview.vue) — its exact layout, palette and copy.
// LEFT = the systems checklist; CENTRE = four cabin-atmosphere gauges (PPO2 / CABIN TEMP / CABIN
// PRESSURE / CO2) over the Dragon (dragon_crew.png, the demo's own art) flanked by the coolant-loop
// and net-power gauges, plus CONNECTIONS and CABIN MICS; RIGHT = CONSUMABLES (T5); bottom = the
// SYSTEMS/CABIN + Overview/Mech tabs and MORE. Values are representative (as the demo's are) — the
// real vessel/atmosphere feed replaces them later. The tabs are shown but not yet live.
//
// T5: the RIGHT column was orbit telemetry (inertial velocity / altitude / apogee / perigee /
// inclination / range) duplicating the FLIGHT page's own telemetry strip (REAL_DRAGON_SCREENS.md
// §3, `FLIGHT` = "telemetry strip ... inertial velocity, altitude, apogee, perigee"). DillonBaird's
// Vehicle render + alt-text (SCREEN_INVENTORY.md "IMAGERY HUNT 2026-09-01") gives this column's real
// content instead: a CONSUMABLE / QTY / MARGIN table — Power Unit 1/2 Energy, Usable Deorbit Fuel/
// Oxidizer, Orbit 1/2 Subtank Fuel/Oxidizer, + a "SHOW MARGINS TO" toggle. MARGIN itself isn't in the
// captured alt-text, so it draws as "—", the same dash idiom the rest of the mod uses for a value with
// no source yet (STATE_CONTRACT.md) rather than inventing a number.
//
// T13a (live-data wiring, §6): every NUMBER on this page now comes from PageState, in the exact idiom
// SystemsPidPage (T9) already ships — `valid ? s.SomeText : "—"`, with the gauge fraction taken from the
// SAME CabinReadout that produced the text so a ring can never disagree with the number inside it. The
// eight gauges read the simulated-from-real cabin model (pure/CabinEnvironment.cs); the CONSUMABLES
// column reads the vessel's real charge, its real bus state and its real propellant mass (VesselData.
// VehicleSources). §6 scopes this to the VALUES: the reference COPY — the seven checklist rows and their
// state words, CONNECTIONS, CABIN MICS — is untouched, and so is the layout. The second coolant gauge's
// label is the one exception: see S20 below.
//
// WHAT STAYS DASHED, AND WHY: the four "Orbit n Subtank" rows and MARGIN. The real vehicle splits its
// propellant across a deorbit tank and two orbit subtanks; KSP has no such split, and deciding which
// KSP litres are "Orbit 2 Subtank Oxidizer" would be inventing the number the label asks for
// (docs/TELEMETRY_REGISTRY.md). "Usable Deorbit Fuel / Oxidizer" IS answerable — the Dragon's own tanks
// feed the Dracos that fly the deorbit burn — so those two are live and the other four are honest dashes.
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

        // right column: CONSUMABLES (T5) — real values from DillonBaird's Vehicle render + alt-text.
        static readonly string[] ConsLabel = {
            "Power Unit 1 Energy", "Power Unit 2 Energy",
            "Usable Deorbit Fuel", "Usable Deorbit Oxidizer",
            "Orbit 1 Subtank Fuel", "Orbit 1 Subtank Oxidizer",
            "Orbit 2 Subtank Fuel", "Orbit 2 Subtank Oxidizer" };
        /// <summary>No-source dash — the one idiom the whole mod uses for a value nothing can supply.</summary>
        const string Dash = "—";

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            void L(string t, float x, float y, float sz, Rgba c) => dl.Text(t, PX(x), PY(y), SZ(sz), TextAlign.Left, c);
            void C(string t, float cx, float y, float sz, Rgba c) => dl.Text(t, PX(cx), PY(y), SZ(sz), TextAlign.Centre, c);
            void R(string t, float rx, float y, float sz, Rgba c) => dl.Text(t, PX(rx), PY(y), SZ(sz), TextAlign.Right, c);

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

            // ---- CENTRE-TOP: four cabin gauges (LIVE — pure/CabinEnvironment.cs) ----
            // Value and ring come from the same CabinReadout, so the needle can never disagree with the
            // number printed inside it. No feed -> a dash and an empty ring, never a confident zero.
            bool valid = s.Valid;
            float F(double frac) => valid ? (float)frac : 0f;
            string T(string live) => (valid && !string.IsNullOrEmpty(live)) ? live : Dash;

            Gauge(1170, 430, 175, F(s.Cabin.Ppo201),      Gold,   "PPO2",           T(s.Ppo2Text),      "psia");
            Gauge(1620, 430, 175, F(s.Cabin.CabinTemp01), Red,    "CABIN TEMP",     T(s.CabinTempText), "°C");
            Gauge(2070, 430, 175, F(s.Cabin.Press01),     Yellow, "CABIN PRESSURE", T(s.PressText),     "psia");
            Gauge(2520, 430, 175, F(s.Cabin.Co201),       Blue,   "CO2",            T(s.Co2Text),       "mmHg");

            // ---- CENTRE: capsule + loop/power gauges ----
            // ⚠ DIVERGENCE from the tier-2 source, 2026-09-02 (S20, owner decision via the overseer):
            // Overview.vue labels BOTH coolant gauges "LOOP A" (lines 222 + 272) — a recreation copy-paste
            // error, not a deliberate reference choice. The real Dragon has two coolant loops, A and B
            // (tier-1 fact); our model computes two distinct loops (Cabin.LoopAC / LoopBC) and the second
            // gauge is wired to Loop B's live value (T13a) — so reproducing "LOOP A" on both would show two
            // different temperatures under one label. docs/REFERENCE_PAGES.md already documents this pair
            // as LOOP A / LOOP B. Owner's call (C1.4): label the second gauge "LOOP B".
            dl.Asset("dragon_crew", PX(1560), PY(760), 520 * sx, 760 * sy, White);
            Gauge(1230, 900,  120, F(s.Cabin.LoopA01), Blue, "LOOP A", T(s.LoopAText), "°C");
            Gauge(1230, 1200, 120, F(s.Cabin.LoopB01), Blue, "LOOP B", T(s.LoopBText), "°C");
            // Net power is SIGNED — the sign lives in the printed number, the ring shows how hard the
            // bus is working either way, against the same full scale the model states.
            Gauge(2410, 900,  120, F(NetPwr01(s.Cabin.NetPwr1W)), Accent, "NET PWR1", T(s.NetPwr1Text), "W");
            Gauge(2410, 1200, 120, F(NetPwr01(s.Cabin.NetPwr2W)), Accent, "NET PWR2", T(s.NetPwr2Text), "W");

            // ---- CONNECTIONS (left of the capsule base) ----
            L("CONNECTIONS", 1130, 1440, 26, Accent);
            string[] cn = { "Manual Rings", "Changelog", "Airlock", "Wing" };
            for (int i = 0; i < 4; i++)
            {
                L(cn[i], 1130, 1500 + i * 56, 24, White);
                L("Connected", 1400, 1500 + i * 56, 24, Go);
            }
            L("CABIN MICS:", 1130, 1748, 26, White); dl.Text("RECORDING", PX(1290), PY(1748), SZ(26), TextAlign.Left, Red);

            // ---- RIGHT: CONSUMABLES table (T5) ----
            L("CONSUMABLE", 2760, 300, 24, Accent);
            R("QTY", 3160, 300, 24, Accent);
            R("MARGIN", 3360, 300, 24, Accent);
            for (int i = 0; i < ConsLabel.Length; i++)
            {
                float y = 360 + i * 145;
                string qty = valid ? Qty(i, s) : null;
                L(ConsLabel[i], 2760, y, 23, White);
                R(qty ?? Dash, 3160, y, 25, string.IsNullOrEmpty(qty) ? Dim : White);
                R(Dash, 3360, y, 25, Dim);
                dl.Rect(PX(2760), PY(y + 30), 600 * sx, SZ(2), Faint);
            }
            L("SHOW MARGINS TO", 2760, 360 + ConsLabel.Length * 145 + 30, 24, Accent);

            // ---- subsystem tab bar (All active) + bottom status bar ----
            // The real Vehicle page carries the eight-subsystem strip (VehicleTabBar); "All" is this
            // overview. It replaces the reference-demo's SYSTEMS/CABIN + Overview/Mech + MORE cluster.
            // T5: severity-aware (VehicleTabBar.Severities) so a faulted subsystem's tab reads red from
            // here too — the real "reached in one touch from anywhere" behaviour, not just on its own page.
            VehicleTabBar.Draw(dl, w, h, 0, VehicleTabBar.Severities(s));
            VehicleDeepViewLinks.Draw(dl, w, h);
            dl.Asset("component_48", 0f, PY(1877), w, SZ(235), White);
        }

        /// <summary>A net-power dial's fill. The reading is SIGNED (negative = draining) and a ring
        /// cannot show a sign, so the ring carries the MAGNITUDE against the model's own stated full
        /// scale and the printed number keeps the sign.</summary>
        static double NetPwr01(double watts)
        {
            double f = (watts < 0.0 ? -watts : watts) / Cabin.NetPwrFullScale;
            return f > 1.0 ? 1.0 : f;
        }

        /// <summary>One CONSUMABLES row's quantity, or null where nothing can answer the label.
        /// Row order is <see cref="ConsLabel"/>'s, which is the render's own order (T5).</summary>
        static string Qty(int row, PageState s)
        {
            switch (row)
            {
                case 0: return s.PowerUnit1Text;    // Power Unit 1 Energy — real charge, on bus 1
                case 1: return s.PowerUnit2Text;    // Power Unit 2 Energy — real charge, on bus 2
                case 2: return s.DeorbitFuelText;   // Usable Deorbit Fuel      — the Dragon's own tanks
                case 3: return s.DeorbitOxText;     // Usable Deorbit Oxidizer  — the Dragon's own tanks
                // Orbit 1 / Orbit 2 subtank fuel + oxidizer: the real vehicle's tank split has no KSP
                // counterpart, and guessing which litres belong to which subtank would be inventing the
                // number (docs/TELEMETRY_REGISTRY.md). Dashed, like MARGIN beside them.
                default: return null;
            }
        }
    }
}
