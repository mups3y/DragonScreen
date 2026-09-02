// DragonScreen — VehicleSubsystemPage  (PURE: the Vehicle page's subsystem sub-tabs)
// ============================================================================================
// The real Crew Dragon Vehicle page has eight subsystem sub-tabs (see VehicleTabBar). Two are already
// their own pages — "All" (VehicleOverviewPage) and "Mech" (VehicleMechPage). This builds the other
// six — Crew, Prop, Power, Avionics, GNC, Thermal — from ONE template so they read as one family, the
// way the real pages share chrome and differ only in the subsystem's data + which tab is lit. There is
// no Figma frame for these; the layout follows the confirmed real grammar (blue capsule line-art
// surrounded by large numeric readouts — SCREEN_EVIDENCE_MATRIX) mapped onto the Vehicle Overview's
// three-zone form: LEFT subsystem checklist · CENTRE capsule + four headline gauges · RIGHT detail
// readouts · the shared subsystem tab bar.
//
// T13b (live-data wiring, §6): the 54 values on these six tabs — 4 headline gauges + 5 detail readouts
// each — were representative constants, like the overview's were before T13a. Every one of them now
// comes from PageState or is an honest dash; see DefOf, which is where the whole wiring lives. Prop's
// numbers travel unchanged into PropSchematic's bottom data band (they are passed through), so wiring
// the source fixed both looks at once. AVIONICS is the one tab that dashes end to end, and that is the
// correct answer rather than a gap — see the block above its own case.
//
// T5: DillonBaird's Vehicle render (+ alt-text, SCREEN_INVENTORY.md "IMAGERY HUNT 2026-09-01") confirms
// a FUNCTIONS|ALERTS toggle bottom-left next to this subsystem tab bar, and that "the Subview Nav Bar …
// displays red when alerts exist in that subview." FUNCTIONS is this page's existing content; ALERTS
// swaps the four-gauge + right-readout zone for an "ALERT ACTIVITY" summary (that column header is
// itself a REAL confirmed label — Frame 58's own attitude HUD carries one, REFERENCE_PAGES.md) driven
// by the SAME real severity that colours this subsystem's own tab in VehicleTabBar, so the tab and the
// page it names can never disagree. Toggle geometry (exact pixel placement) is OURS — not measurable
// from the source render — same footing as Menu/Reference Content's §14.4(c) layout. Left inert per
// T14 (touch wiring); this task's DONE-when is preview only.
//
// T9: Prop is the one tab whose REAL look is known and is not this template — the JSC training photo
// shows a Draco thruster schematic (§11b / SCREEN_INVENTORY #26), so its FUNCTIONS view delegates the
// centre+right zone to PropSchematic and skips the upright capsule render. Everything else here — the
// title, the left checklist, the FUNCTIONS/ALERTS toggle, the ALERTS view and the tab bar — is shared
// with its five siblings exactly as before.
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

        /// <summary>No-source dash - the one idiom the whole mod uses for a value nothing can supply.</summary>
        const string Dash = "—";

        // ---- one subsystem's content ----
        struct Sys
        {
            public string Title; public int Tab;
            public string[] CkLabel, CkState; public int[] CkKey;                 // left checklist
            public string[] GLabel, GVal, GUnit; public float[] GFrac; public Rgba[] GCol; // 4 headline gauges
            public string[] RLabel, RVal; public float[] RFrac;                   // right detail readouts
        }

        /// <summary>This subsystem's real live severity — the same signal that colours its VehicleTabBar
        /// tab (T5). Avionics and GNC share the one real fault channel this build has (Alarms.FdirSeverity);
        /// there is no second, separately-modelled fault source to split them on.</summary>
        static Severity LiveSeverity(Sub sub, PageState s)
        {
            if (!s.Valid) return Severity.Nominal;
            switch (sub)
            {
                case Sub.Crew:       return Alarms.LifeSupport(s.Cabin);
                case Sub.Propulsion: return Alarms.Low(s.Propellant01);
                case Sub.Power:      return Alarms.Low(s.Power01);
                case Sub.Avionics:
                case Sub.Gnc:        return Alarms.FdirSeverity(s);
                default:             return Alarms.Thermal(s.Cabin); // Thermal
            }
        }

        public static void Build(DisplayList dl, int w, int h, Sub sub, PageState s) { Build(dl, w, h, sub, s, false); }

        /// <summary><paramref name="alerts"/> selects the FUNCTIONS (false, default) or ALERTS (true) tab
        /// of the T5 toggle — see the file header. FigmaUI doesn't wire the toggle to a touch yet
        /// (T14), so every caller today passes the 4-arg overload and gets FUNCTIONS.</summary>
        public static void Build(DisplayList dl, int w, int h, Sub sub, PageState s, bool alerts)
        {
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            void L(string t, float x, float y, float s2, Rgba c) => dl.Text(t, PX(x), PY(y), SZ(s2), TextAlign.Left, c);
            void C(string t, float cx, float y, float s2, Rgba c) => dl.Text(t, PX(cx), PY(y), SZ(s2), TextAlign.Centre, c);
            void R(string t, float rx, float y, float s2, Rgba c) => dl.Text(t, PX(rx), PY(y), SZ(s2), TextAlign.Right, c);

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

            Sys d = DefOf(sub, s);

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

            Severity sev = LiveSeverity(sub, s);

            if (!alerts && sub == Sub.Propulsion)
            {
                // ---- T9: Prop's real look is the Draco thruster schematic, not the gauge template
                // (§3 REFINE / §11b / SCREEN_INVENTORY #26). It fills the centre AND right zones the way
                // the source photo does, and carries this subsystem's own gauge + detail values into its
                // bottom data band, so nothing the template showed is lost. See PropSchematic.
                PropSchematic.Draw(dl, w, h, s, d.GLabel, d.GVal, d.GUnit, d.GFrac, d.RLabel, d.RVal);
            }
            else if (!alerts)
            {
                // ---- CENTRE-TOP: four headline gauges ----
                float[] gx = { 1170f, 1620f, 2070f, 2520f };
                for (int i = 0; i < d.GLabel.Length && i < 4; i++)
                    Gauge(gx[i], 470, 170, d.GFrac[i], d.GCol[i], d.GLabel[i], d.GVal[i], d.GUnit[i]);

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
            }
            else
            {
                // ---- ALERTS: "ALERT ACTIVITY" (real label, REFERENCE_PAGES.md Frame 58) in place of the
                // gauges, then the one real fault channel (FDIR) in place of the readout column. Both are
                // driven by LiveSeverity — the exact value already colouring this subsystem's own tab, so
                // the toggle content and the red-nav can never say different things.
                Rgba sevCol = sev == Severity.Nominal ? Go : Alarms.Colour(sev);
                C("ALERT ACTIVITY", 1845, 340, 38, Accent);
                dl.Rect(PX(1300), PY(390), 1090 * sx, SZ(2), Faint);
                C(Alarms.Word(sev), 1845, 560, 110, sevCol);

                L("FDIR", 2760, 340, 28, Dim);
                Rgba fdirCol = s.Valid ? Alarms.Colour(Alarms.FdirSeverity(s)) : Dim;
                R(s.Valid ? s.FaultText : "NO DATA", 3360, 340, 34, fdirCol);
                float fdirFrac = Alarms.FdirSeverity(s) == Severity.Nominal ? 0.15f
                               : Alarms.FdirSeverity(s) == Severity.Caution ? 0.6f : 1f;
                dl.Rect(PX(2760), PY(410), 600 * sx, SZ(8), Faint);
                dl.Rect(PX(2760), PY(410), 600 * sx * fdirFrac, SZ(8), fdirCol);
            }

            // ---- CENTRE: capsule diagram (the vehicle, on every vehicle page). Prop's schematic view
            // draws its OWN vehicle — the horizontal profile line-art the real page uses — so the
            // rendered upright capsule would be a second, contradictory one (T9). ----
            if (alerts || sub != Sub.Propulsion)
                dl.Asset("dragon_crew", PX(1453), PY(760), 520 * sx, 760 * sy, White);

            // ---- FUNCTIONS | ALERTS toggle (T5, bottom-left; geometry ours, see file header) ----
            Rgba fnCol = !alerts ? White : Dim;
            Rgba alCol = alerts ? White : (sev != Severity.Nominal ? Alarms.Colour(sev) : Dim);
            L("FUNCTIONS", 150, 1760, 28, fnCol);
            L("ALERTS", 420, 1760, 28, alCol);
            dl.Rect(PX(!alerts ? 150f : 420f), PY(1798), (!alerts ? 190f : 110f) * sx, SZ(4),
                    alerts && sev != Severity.Nominal ? Alarms.Colour(sev) : Accent);

            // ---- subsystem tab bar + global bottom bar ----
            VehicleTabBar.Draw(dl, w, h, d.Tab, VehicleTabBar.Severities(s));
            dl.Asset("component_48", 0f, PY(1877), w, SZ(235), White);
        }

        // ---- per-subsystem content. T13b (live-data wiring, §6): every one of the 54 numbers here now
        // comes from PageState, in the idiom SystemsPidPage (T9) and the VEHICLE family (T13a) already
        // ship — `T(s.SomeText)`, with each gauge's fraction taken from the SAME source that produced
        // the text, so a ring can never disagree with the number inside it. A quantity this build models
        // nothing for is `Dash` with an empty ring: never a plausible constant, and never a confident
        // zero (docs/TELEMETRY_REGISTRY.md; Pages.cs). Labels, units and the left checklist are the
        // template's own COPY and are untouched — §6 scopes this to the VALUES.
        static Sys DefOf(Sub sub, PageState st)
        {
            bool valid = st.Valid;
            // A live string, or the no-source dash. The same helper, by the same name, as the overview's.
            string T(string live) => (valid && !string.IsNullOrEmpty(live)) ? live : Dash;
            // A live fraction, or an empty ring: a dead feed must not leave a ring sitting where it was.
            float F(double frac) => valid ? (float)frac : 0f;

            Sys s = new Sys();
            switch (sub)
            {
                case Sub.Crew:
                    s.Title = "CREW · LIFE SUPPORT"; s.Tab = 1;
                    s.CkLabel = new[] { "CABIN ATMOSPHERE", "O2 SUPPLY", "CO2 SCRUBBER", "SUIT LOOP", "WATER SYSTEM", "SMOKE DETECT" };
                    s.CkState = new[] { "Nominal", "Nominal", "Active", "Standby", "Nominal", "Clear" };
                    s.CkKey   = new[] { 1, 1, 1, 0, 1, 1 };
                    // The four cabin gauges are the overview's four, read off the same CabinReadout.
                    s.GLabel  = new[] { "PPO2", "CABIN TEMP", "CABIN PRESS", "CO2" };
                    s.GVal    = new[] { T(st.Ppo2Text), T(st.CabinTempText), T(st.PressText), T(st.Co2Text) };
                    s.GUnit   = new[] { "psia", "°C", "psia", "mmHg" };
                    s.GFrac   = new[] { F(st.Cabin.Ppo201), F(st.Cabin.CabinTemp01),
                                        F(st.Cabin.Press01), F(st.Cabin.Co201) };
                    s.GCol    = new[] { Gold, Red, Yellow, Blue };
                    s.RLabel  = new[] { "Humidity", "O2 Tank", "N2 Tank", "Potable Water", "Crew Aboard" };
                    // Humidity: nothing in this build models cabin humidity, so it dashes.
                    s.RVal    = new[] { Dash, T(st.O2TankText), T(st.N2TankText), T(st.WaterText), T(st.CrewText) };
                    s.RFrac   = new[] { 0f, F(st.Systems.Oxygen), F(st.Systems.Nitrogen),
                                        F(st.Water01), F(st.Crew01) };
                    break;

                case Sub.Propulsion:
                    s.Title = "PROPULSION"; s.Tab = 2;
                    s.CkLabel = new[] { "DRACO x16", "SUPERDRACO x8", "PROP ISOLATION", "HE PRESSURANT", "OMS / RCS", "MANIFOLD LEAK" };
                    s.CkState = new[] { "16 / 16", "Armed", "Open", "Nominal", "Ready", "None" };
                    s.CkKey   = new[] { 1, 2, 1, 1, 1, 1 };
                    s.GLabel  = new[] { "OX (NTO)", "FUEL (MMH)", "HELIUM", "PROP TEMP" };
                    // HELIUM pressurant and propellant temperature: no KSP resource and no model answers
                    // either, and a bar pressure would be a number invented to fill the dial.
                    s.GVal    = new[] { T(st.DragonOxText), T(st.DragonFuelText), Dash, Dash };
                    s.GUnit   = new[] { "%", "%", "bar", "°C" };
                    s.GFrac   = new[] { F(st.DragonOx01), F(st.DragonFuel01), 0f, 0f };
                    s.GCol    = new[] { Gold, Gold, Blue, Red };
                    s.RLabel  = new[] { "Chamber Press", "Prop Remaining", "Draco Duty", "SuperDraco Temp", "Thrust Avail" };
                    // Chamber pressure, SuperDraco temperature and thrust availability have no source:
                    // KSP models no per-engine chamber pressure and no pod temperature, and nothing here
                    // tracks which of the sixteen Dracos would still answer a command.
                    s.RVal    = new[] { Dash, T(st.PropRemainingText), T(st.DracoDutyText), Dash, Dash };
                    s.RFrac   = new[] { 0f, F(st.DragonProp01), valid ? PropSchematic.MaxDuty(st) : 0f, 0f, 0f };
                    break;

                case Sub.Power:
                    s.Title = "ELECTRICAL POWER"; s.Tab = 4;
                    s.CkLabel = new[] { "MAIN BUS A", "MAIN BUS B", "BATTERIES x4", "SOLAR ARRAY", "PWR DISTRIB", "LOAD SHED" };
                    s.CkState = new[] { "Nominal", "Nominal", "4 / 4", "Deployed", "Nominal", "Off" };
                    s.CkKey   = new[] { 1, 1, 1, 1, 1, 0 };
                    s.GLabel  = new[] { "BATTERY SOC", "BUS A", "BUS B", "ARRAY" };
                    // BUS A / BUS B are VOLTAGES. KSP's ElectricCharge has no voltage, and the two buses
                    // this build does model are ON/OFF (pure/VehicleSystems.cs) — a different fact, and
                    // one the systems tree already shows. 120 V here would assert a meter that is not there.
                    s.GVal    = new[] { T(st.PowerText), Dash, Dash, T(st.ArrayKwText) };
                    s.GUnit   = new[] { "%", "V", "V", "kW" };
                    s.GFrac   = new[] { F(st.Power01), 0f, 0f, F(st.Array01) };
                    s.GCol    = new[] { Accent, Accent, Accent, Yellow };
                    s.RLabel  = new[] { "Array Output", "Net Power", "Bus Load", "Battery Temp", "Charge Rate" };
                    // "Array Output" is the ARRAY gauge's own datum in the row's format, and "Charge Rate"
                    // is "Net Power" in kW — the template shows each of those quantities twice, so both
                    // readings come from ONE source rather than two that could drift apart. Bus load and
                    // battery temperature have none: no per-bus load model, no battery thermal model.
                    s.RVal    = new[] { T(st.ArrayOutputText), T(st.NetPowerText), Dash, Dash, T(st.ChargeRateText) };
                    s.RFrac   = new[] { F(st.Array01), F(NetPwr01(st)), 0f, 0f, F(NetPwr01(st)) };
                    break;

                case Sub.Avionics:
                    s.Title = "AVIONICS"; s.Tab = 5;
                    s.CkLabel = new[] { "FLIGHT COMP x3", "VRIO 1 / 2", "DATA BUS", "GPS", "S-BAND COMMS", "SW WATCHDOG" };
                    s.CkState = new[] { "3 / 3", "Nominal", "Nominal", "Lock", "Linked", "Armed" };
                    s.CkKey   = new[] { 1, 1, 1, 1, 1, 0 };
                    // ---- EVERY VALUE ON THIS TAB DASHES, AND THAT IS THE ANSWER ----
                    // The real vehicle's avionics — triple-redundant flight computers, the data bus, GPS,
                    // S-band — are the one subsystem this build models NOTHING of: there is no computer
                    // load, no bus traffic, no link budget, no storage and no GPS state anywhere in the
                    // tree, and no KSP quantity stands in for them. Under docs/TELEMETRY_REGISTRY.md that
                    // makes all nine a dash; the alternative is nine invented numbers that would look
                    // exactly as convincing when the feed is dead. The tab's own live signal is its FDIR
                    // severity, which colours the tab and fills the ALERTS view (see LiveSeverity).
                    s.GLabel  = new[] { "FC LOAD", "BUS TRAFFIC", "LINK MARGIN", "STORAGE" };
                    s.GVal    = new[] { Dash, Dash, Dash, Dash };
                    s.GUnit   = new[] { "%", "%", "dB", "%" };
                    s.GFrac   = new[] { 0f, 0f, 0f, 0f };
                    s.GCol    = new[] { Accent, Accent, Go, Blue };
                    s.RLabel  = new[] { "FC1 / 2 / 3", "GPS Sats", "Uplink", "Downlink", "Data Rate" };
                    s.RVal    = new[] { Dash, Dash, Dash, Dash, Dash };
                    s.RFrac   = new[] { 0f, 0f, 0f, 0f, 0f };
                    break;

                case Sub.Gnc:
                    s.Title = "GUIDANCE, NAV & CONTROL"; s.Tab = 6;
                    s.CkLabel = new[] { "IMU 1 / 2", "STAR TRACKERS", "GPS NAV", "RCS AUTHORITY", "NAV STATE", "ATT CONTROL" };
                    s.CkState = new[] { "Nominal", "2 / 2", "Lock", "Enabled", "Valid", "Auto" };
                    s.CkKey   = new[] { 1, 1, 1, 1, 1, 0 };
                    s.GLabel  = new[] { "ROLL RATE", "PITCH RATE", "YAW RATE", "RCS FUEL" };
                    // The Dracos ARE the RCS, so "RCS FUEL" is the propulsion tab's own tank fraction —
                    // one datum, one source, two pages.
                    s.GVal    = new[] { T(st.BodyRollText), T(st.BodyPitchText), T(st.BodyYawText),
                                        T(st.DragonPropText) };
                    s.GUnit   = new[] { "°/s", "°/s", "°/s", "%" };
                    s.GFrac   = new[] { F(Rate01(st.BodyRollDps)), F(Rate01(st.BodyPitchDps)),
                                        F(Rate01(st.BodyYawDps)), F(st.DragonProp01) };
                    s.GCol    = new[] { Accent, Accent, Accent, Gold };
                    s.RLabel  = new[] { "Attitude Err", "Body Rate", "Altitude", "Velocity", "Pointing" };
                    // Attitude error is an error AGAINST SOMETHING: with no target there is nothing to be
                    // misaligned with, so it dashes rather than reporting a confident zero — the same
                    // precondition the docking page states for its own HUD. ALTITUDE and VELOCITY are the
                    // vehicle's live orbital state, and VELOCITY goes through OrbitReadout so this page
                    // cannot show orbital speed on the pad while FLIGHT shows surface speed (Pages.cs's
                    // "this is the third time"). POINTING is the live control-authority word.
                    string align = st.HasTarget ? st.AlignText : null;
                    string vCap, vVal; double vMps;
                    OrbitReadout.Velocity(st, out vCap, out vVal, out vMps);
                    s.RVal    = new[] { T(align), T(st.BodyRateText), T(st.Altitude), T(vVal), T(st.ModeText) };
                    s.RFrac   = new[] { F(st.HasTarget ? st.Align01 : 0.0), F(Rate01(BodyRateDps(st))),
                                        F(BarScale.Altitude(st.AltitudeM, st.AtmosphereDepthM, st.BodyRadiusM)),
                                        F(BarScale.Velocity(vMps, st.CircularSpeedMps)), 0f };
                    break;

                default: // Thermal
                    s.Title = "THERMAL CONTROL"; s.Tab = 7;
                    s.CkLabel = new[] { "COOLANT LOOP A", "COOLANT LOOP B", "RADIATORS", "HEAT SHIELD", "HEATERS", "HX FLOW" };
                    s.CkState = new[] { "Nominal", "Nominal", "Deployed", "Nominal", "Auto", "Nominal" };
                    s.CkKey   = new[] { 1, 1, 1, 1, 0, 1 };
                    s.GLabel  = new[] { "LOOP A", "LOOP B", "RADIATOR", "SHIELD" };
                    // RADIATOR: the coolant model (pure/CabinEnvironment.cs) carries the two loops but no
                    // separate radiator outlet, and the trunk radiators are not modelled at all.
                    s.GVal    = new[] { T(st.LoopAText), T(st.LoopBText), Dash, T(st.HullTempText) };
                    s.GUnit   = new[] { "°C", "°C", "°C", "°C" };
                    s.GFrac   = new[] { F(st.Cabin.LoopA01), F(st.Cabin.LoopB01), 0f, F(st.HullTemp01) };
                    s.GCol    = new[] { Blue, Blue, Accent, Red };
                    s.RLabel  = new[] { "Loop A Flow", "Loop B Flow", "Heat Reject", "Cabin HX", "TPS Max" };
                    // The loops are modelled as TEMPERATURES, not as a flow rate, a rejected-heat figure or
                    // a heat-exchanger outlet; three litres-per-second that nothing computes would be three
                    // inventions. "TPS Max" is the SHIELD gauge's own datum in the row's format.
                    s.RVal    = new[] { Dash, Dash, Dash, Dash, T(st.TpsMaxText) };
                    s.RFrac   = new[] { 0f, 0f, 0f, 0f, F(st.HullTemp01) };
                    break;
            }
            return s;
        }

        /// <summary>The net-power ROWS' bar: both buses together against both dials' full scale, so this
        /// bar and the overview's two NET PWR dials are the same reading at the same scale. A signed value
        /// has no bar direction, so the bar carries the MAGNITUDE and the printed number keeps the sign —
        /// exactly what VehicleOverviewPage's own net-power dials do.</summary>
        static double NetPwr01(PageState s)
        {
            double w = s.Cabin.NetPwr1W + s.Cabin.NetPwr2W;
            if (w < 0.0) w = -w;
            double f = w / (Cabin.NetPwrFullScale * 2.0);
            return f > 1.0 ? 1.0 : f;
        }

        /// <summary>Total body rate, deg/s, from the same three axes the gauges above draw — so the
        /// "Body Rate" row and the three rate dials cannot tell different stories.</summary>
        static double BodyRateDps(PageState s)
        {
            return Math.Sqrt(s.BodyRollDps * s.BodyRollDps
                           + s.BodyPitchDps * s.BodyPitchDps
                           + s.BodyYawDps * s.BodyYawDps);
        }

        /// <summary>FULL SCALE for a body-rate dial, STATED: 2 °/s. Every rate that matters on this
        /// vehicle — a docking approach, a coast attitude hold — lives well under it, so the needle sits
        /// where the useful readings are, and a hard manual slew pegs the dial, which is itself the right
        /// reading. A rate's nominal IS zero, so this is the one kind of dial that belongs at the bottom
        /// of its scale rather than in the middle third (CabinEnvironment's rule is for set points).</summary>
        public const double RateFullScaleDps = 2.0;

        static double Rate01(double dps)
        {
            double d = (dps < 0.0) ? -dps : dps;
            double f = d / RateFullScaleDps;
            return f > 1.0 ? 1.0 : f;
        }
    }
}
