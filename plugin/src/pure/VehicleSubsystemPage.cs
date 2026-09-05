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
// the source fixed both looks at once. AVIONICS dashes almost end to end — this build models none of
// its computer/bus/storage/GPS state — and that is the correct answer rather than a gap; see the block
// above its own case. S24 (owner decision) wires its S-BAND COMMS / Uplink / Downlink to stock KSP's
// own CommNet, the one honest source the tab has.
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
//
// S27: VehicleDeepViewLinks (SYSTEMS TREE / SYSTEMS P&ID, right of the tab strip) is drawn here too —
// same footing as the FUNCTIONS/ALERTS toggle above, an invented control on a real page, marked as ours.
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
                case Sub.Propulsion: return Alarms.PropellantSeverity(s);
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
            // S51 / audit H14: THIS COLUMN NEVER GOT S22'S GUARD. S22 gave `VehicleOverviewPage` a dead-feed
            // rule — a dashed gauge must not sit beside a confident green state word — and stopped there.
            // On these six tabs a dead feed dashed all four gauges while the left column went on reading
            // `Nominal / Active / Clear / 16 / 16 / Open / Ready / …` in green. Same failure, one page over.
            // The guard is now the overview's, verbatim: `!valid` dims the WHOLE row (icon + word) and the
            // word goes through `T()`, exactly as `VehicleOverviewPage.cs:113` does. The LABEL stays put —
            // it says which subsystem the row is, which is true whether or not there is a feed.
            bool ckValid = s.Valid;
            string CT(string live) => (ckValid && !string.IsNullOrEmpty(live)) ? live : Dash;
            for (int i = 0; i < d.CkLabel.Length; i++)
            {
                float y = 300 + i * 195;
                // 0 neutral · 1 go · 2 caution · 3 alarm. Key 3 arrived with the bus rows (QC-AUDIT
                // finding 3): a bus the crew has powered whose three strings are ALL down is an alarm on
                // the systems tree, and this column has to be able to say the same thing.
                Rgba sc = !ckValid ? Dim
                        : d.CkKey[i] == 1 ? Go : d.CkKey[i] == 2 ? Amber
                        : d.CkKey[i] == 3 ? DragonPalette.Alarm : White;
                dl.Asset("ic_check", PX(90), PY(y), SZ(38), SZ(38), sc);
                L(d.CkLabel[i], 150, y + 4, 28, White);
                L(CT(d.CkState[i]), 150, y + 48, 26, sc);
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
                    // S38: 3160, not 3360. The value used to sit on the far end of the 600-unit bar
                    // from its label; the bar underneath is a connector, which helps, but 600 units is
                    // still enough for an IVA viewing angle to lift the value toward the row above.
                    // It stays right-aligned and still sits over its own bar, just closer in.
                    R(d.RVal[i], 3160, y, 34, White);
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
                // S51 / audit H16: on a dead feed this printed a green "NOMINAL" — a confident all-clear
                // computed from `LiveSeverity`'s own `!Valid -> Nominal` shortcut — directly beside the
                // honest "NO DATA" in the FDIR column below it. One panel, two answers. With no feed there
                // is no alert activity to report, so the word dashes and dims like every other unsourced
                // readout on this page; the severity itself is unchanged.
                Rgba sevCol = !s.Valid ? Dim : sev == Severity.Nominal ? Go : Alarms.Colour(sev);
                C("ALERT ACTIVITY", 1845, 340, 38, Accent);
                dl.Rect(PX(1300), PY(390), 1090 * sx, SZ(2), Faint);
                C(s.Valid ? Alarms.Word(sev) : "NO DATA", 1845, 560, 110, sevCol);

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
            // Live since T14: the two words are hit-tested from the SAME TabX/TabW below that place them.
            Rgba fnCol = !alerts ? White : Dim;
            Rgba alCol = alerts ? White : (sev != Severity.Nominal ? Alarms.Colour(sev) : Dim);
            L("FUNCTIONS", TabX[0], TabTextY, 28, fnCol);
            L("ALERTS", TabX[1], TabTextY, 28, alCol);
            dl.Rect(PX(TabX[alerts ? 1 : 0]), PY(TabRuleY), TabW[alerts ? 1 : 0] * sx, SZ(4),
                    alerts && sev != Severity.Nominal ? Alarms.Colour(sev) : Accent);

            // ---- subsystem tab bar + global bottom bar ----
            VehicleTabBar.Draw(dl, w, h, d.Tab, VehicleTabBar.Severities(s));
            VehicleDeepViewLinks.Draw(dl, w, h);
            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame
        }

        // ---- THE FUNCTIONS | ALERTS TOGGLE (T5 drew it, T14 wired it) ----
        // Design-space x of each word and the width of the rule under it. Build draws from these and
        // ToggleHit tests them, so the word and its touch target are one thing (PageAction's rule). The
        // hit band is the word's own row, grown to the rule below it: these are 28px words on a 2112px
        // design, and a touch target the exact size of the glyphs would be unusable on the glass.
        static readonly float[] TabX = { 150f, 420f };
        static readonly float[] TabW = { 190f, 110f };
        const float TabTextY = 1760f, TabRuleY = 1798f, TabHitTop = 1736f, TabHitBot = 1820f;

        /// <summary>Which half of the FUNCTIONS | ALERTS toggle a touch hit: 0 FUNCTIONS, 1 ALERTS,
        /// -1 neither. The page it is drawn on decides what to do with that - here it only says where the
        /// finger landed.</summary>
        public static int ToggleHit(float px, float py, int w, int h)
        {
            if (w <= 0 || h <= 0) return -1;
            float dx = px * RefW / w, dy = py * RefH / h;
            if (dy < TabHitTop || dy >= TabHitBot) return -1;
            for (int i = 0; i < TabX.Length; i++)
                if (dx >= TabX[i] - 20f && dx < TabX[i] + TabW[i] + 20f) return i;
            return -1;
        }

        // ---- MAIN BUS A / B: the systems tree's own truth, in this column's idiom (QC-AUDIT finding 3)
        // Pre-built so the draw path never formats a string, exactly as SystemsTreePage.Online3 is. The
        // words are shorter here because the checklist column is narrower than a tree node, but they say
        // the same thing about the same SystemsState, and "3 / 3 online" is spelled the way every other
        // healthy row on this template is spelled: Nominal.
        static readonly string[] BusOnline3 = { "0 / 3 Online", "1 / 3 Online", "2 / 3 Online", "Nominal" };

        // ---- S51 / audit H15: EIGHT WORDS THAT CONTRADICTED LIVE STATE ON THEIR OWN SCREEN ----
        // The deeper half of S51. Beyond the dead-feed guard above, eight of this column's literals
        // asserted a state NOTHING CHECKED, while the field that would have contradicted them was already
        // being drawn on the same page — SMOKE DETECT "Clear" beside a live `Systems.Fire`, OMS/RCS
        // "Ready" and RCS AUTHORITY "Enabled" beside a live `RcsOn`, ATT CONTROL "Auto" four rows above a
        // "Pointing" readout printing `ModeText`, COOLANT LOOP A/B and HEAT SHIELD "Nominal" beside the
        // very numbers `Alarms` bands. NO NEW MODEL AND NO NEW THRESHOLD: every one reads a field that was
        // already there, through the severity rules the rest of the mod already uses. This is exactly what
        // QC-AUDIT finding 3 did for MAIN BUS A/B, applied to the rest of the column.

        /// <summary>A severity in this column's idiom. `Alarms.Word` shouts in caps for the ALERTS banner;
        /// the checklist is title-case ("Nominal"), so the two are spelled differently on purpose and both
        /// come from the SAME `Severity` — they cannot disagree about the state, only about typography.</summary>
        static string SevWord(Severity v)
        {
            return v == Severity.Alarm ? "Alarm" : v == Severity.Caution ? "Caution" : "Nominal";
        }

        /// <summary>...and its checklist colour key. 1 go · 2 caution · 3 alarm, the keys already in use.</summary>
        static int SevKey(Severity v)
        {
            return v == Severity.Alarm ? 3 : v == Severity.Caution ? 2 : 1;
        }

        /// <summary>ATT CONTROL: who owns the vehicle, off the SAME `ModeText` this tab already prints in
        /// its own "Pointing" readout. With no flight software that reads IDLE, which is §14.4(a)'s honest
        /// answer — the old literal "Auto" claimed an autopilot was flying.</summary>
        static int ModeKey(ControlMode m)
        {
            if (m == ControlMode.Abort) return 3;
            if (m == ControlMode.Recovery) return 2;
            if (m == ControlMode.Auto) return 1;
            return 0;                                  // Idle / Manual: neutral, not a fault
        }

        static bool BusOn(PageState st, int bus)
        {
            return bus == 1 ? st.Systems.Bus1On : st.Systems.Bus2On;
        }

        /// <summary>MAIN BUS A/B's state word, off the same SystemsState the systems tree draws.</summary>
        static string BusWord(PageState st, bool valid, int bus)
        {
            if (!valid) return Dash;
            if (!BusOn(st, bus)) return "Off";
            return BusOnline3[Systems.OnlineCount(st.Systems, bus)];
        }

        /// <summary>...and its checklist colour key, mirroring SystemsTreePage's bus rule exactly:
        /// unpowered is neutral, 3/3 is go, 0/3 on a powered bus is an alarm, anything else a caution.</summary>
        static int BusKey(PageState st, bool valid, int bus)
        {
            if (!valid || !BusOn(st, bus)) return 0;
            int online = Systems.OnlineCount(st.Systems, bus);
            return online == 3 ? 1 : online == 0 ? 3 : 2;
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
                    // S51/H15: SMOKE DETECT reads the live fire model — the same `Systems.Fire` the P&ID
                    // draws as FIRE DETECTED / NONE — instead of a hardcoded "Clear" that stayed green
                    // through a cabin fire. Same source, same two states, this column's wording.
                    bool smoke = valid && st.Systems.Fire;
                    s.CkState = new[] { "Nominal", "Nominal", "Active", "Standby", "Nominal",
                                        smoke ? "Detected" : "Clear" };
                    s.CkKey   = new[] { 1, 1, 1, 0, 1, smoke ? 3 : 1 };
                    // The four cabin gauges are the overview's four, read off the same CabinReadout.
                    s.GLabel  = new[] { "PPO2", "CABIN TEMP", "CABIN PRESS", "CO2" };
                    s.GVal    = new[] { T(st.Ppo2Text), T(st.CabinTempText), T(st.PressText), T(st.Co2Text) };
                    s.GUnit   = new[] { "psia", "°C", "psia", "mmHg" };
                    s.GFrac   = new[] { F(st.Cabin.Ppo201), F(st.Cabin.CabinTemp01),
                                        F(st.Cabin.Press01), F(st.Cabin.Co201) };
                    // S104 / QC S-01: the ring's colour is the model's verdict where the model HAS one,
                    // and the neutral reading colour where it does not. These were constants.
                    s.GCol    = new[] {
                        Alarms.GaugeColour(Alarms.Band(st.Cabin.Ppo2Psia,  CabinLimits.Ppo2Caution,      CabinLimits.Ppo2Alarm),      valid),
                        Alarms.GaugeColour(Alarms.Band(st.Cabin.CabinTempC, CabinLimits.CabinTempCaution, CabinLimits.CabinTempAlarm), valid),
                        Alarms.GaugeColour(Alarms.Band(st.Cabin.PressPsia, CabinLimits.PressCaution,     CabinLimits.PressAlarm),     valid),
                        Alarms.GaugeColour(Alarms.Band(st.Cabin.Co2MmHg,   CabinLimits.Co2Caution,       CabinLimits.Co2Alarm),       valid) };
                    s.RLabel  = new[] { "Humidity", "O2 Tank", "N2 Tank", "Potable Water", "Crew Aboard" };
                    // Humidity: nothing in this build models cabin humidity, so it dashes.
                    s.RVal    = new[] { Dash, T(st.O2TankText), T(st.N2TankText), T(st.WaterText), T(st.CrewText) };
                    s.RFrac   = new[] { 0f, F(st.Systems.Oxygen), F(st.Systems.Nitrogen),
                                        F(st.Water01), F(st.Crew01) };
                    break;

                case Sub.Propulsion:
                    s.Title = "PROPULSION"; s.Tab = 2;
                    s.CkLabel = new[] { "DRACO x16", "SUPERDRACO x8", "PROP ISOLATION", "HE PRESSURANT", "OMS / RCS", "MANIFOLD LEAK" };
                    // S51/H15, two rows.
                    // OMS / RCS: reads the live RCS action group (`RcsOn`, off `KSPActionGroup.RCS`) rather
                    // than a hardcoded "Ready" that said Ready with the RCS switched off. It is the SAME
                    // field GNC's RCS AUTHORITY row reads, so the two tabs cannot disagree about one switch.
                    // MANIFOLD LEAK: DASHED, deliberately, and this is the one row in the eight that is NOT
                    // wired. The only leak this build models is the CABIN's (`SystemsState.LeakRate`, sprung
                    // by G-overstress and reported under its own label on the P&ID as CABIN LEAK). Reading
                    // it here would put a cabin leak under a PROPELLANT MANIFOLD label — trading one false
                    // word for a worse one. Nothing models a propellant manifold leak, so §14.4(e)'s dash
                    // is the honest answer: no source, no claim. See S51's open question in REGISTER.md.
                    bool rcsUp = valid && st.RcsOn;
                    s.CkState = new[] { "16 / 16", "Armed", "Open", "Nominal",
                                        rcsUp ? "Ready" : "Off", Dash };
                    s.CkKey   = new[] { 1, 2, 1, 1, rcsUp ? 1 : 0, 0 };
                    s.GLabel  = new[] { "OX (NTO)", "FUEL (MMH)", "HELIUM", "PROP TEMP" };
                    // HELIUM pressurant and propellant temperature: no KSP resource and no model answers
                    // either, and a bar pressure would be a number invented to fill the dial.
                    s.GVal    = new[] { T(st.DragonOxText), T(st.DragonFuelText), Dash, Dash };
                    s.GUnit   = new[] { "%", "%", "bar", "°C" };
                    s.GFrac   = new[] { F(st.DragonOx01), F(st.DragonFuel01), 0f, 0f };
                    // S104 / QC S-01: the ring's colour is the model's verdict where the model HAS one,
                    // and the neutral reading colour where it does not. These were constants.
                    // OX and FUEL are Dragon propellant fractions, so they take `Alarms.Low` - the same
                    // 0..1 low-side band `PropellantSeverity` already applies to DragonProp01, not a new
                    // one. HELIUM and PROP TEMP are DASHES with no reading: a dash gets no verdict.
                    s.GCol    = new[] {
                        Alarms.GaugeColour(Alarms.Low(st.DragonOx01),   valid),
                        Alarms.GaugeColour(Alarms.Low(st.DragonFuel01), valid),
                        Accent, Accent };
                    s.RLabel  = new[] { "Chamber Press", "Prop Remaining", "Draco Duty", "SuperDraco Temp", "Thrust Avail" };
                    // Chamber pressure and SuperDraco temperature have no source: KSP models no per-engine
                    // chamber pressure and no pod temperature. THRUST AVAIL now does (S46) - Kerbal Engineer's
                    // fuel-flow simulation of the real part tree, the MAXIMUM the current stage can make,
                    // because the label asks what is available rather than what the throttle is using. Tier-2
                    // (§14.4(e) step 1), null when KER is absent / has no result / we are docked, and null
                    // dashes exactly like the two beside it. Its ring stays EMPTY: a fraction needs a full
                    // scale, and this vehicle publishes no rated thrust to divide by - inventing one to make
                    // the bar look alive is precisely what §14.4(e) forbids.
                    s.RVal    = new[] { Dash, T(st.PropRemainingText), T(st.DracoDutyText), Dash, T(st.Ker.ThrustAvailText) };
                    s.RFrac   = new[] { 0f, F(st.DragonProp01), valid ? PropSchematic.MaxDuty(st) : 0f, 0f, 0f };
                    break;

                case Sub.Power:
                    s.Title = "ELECTRICAL POWER"; s.Tab = 4;
                    // S23 (owner decision (b), 2026-09-02): the real screen's "BATTERIES ×4" names the
                    // real vehicle's fixed battery count; dropped here — and on the systems tree,
                    // SystemsTreePage.cs — because a static count claim over the live count in the row
                    // below misleads on any craft that isn't 4 batteries. See REGISTER.md S23.
                    s.CkLabel = new[] { "MAIN BUS A", "MAIN BUS B", "BATTERIES", "SOLAR ARRAY", "PWR DISTRIB", "LOAD SHED" };
                    // S25: BATTERIES / SOLAR ARRAY now read the SAME live PageState fields the systems
                    // tree draws (T13a) — T() so a dead feed dashes them like every other live row on
                    // this tab, never a stale "4 / 4" / "Deployed". Icon colour mirrors the tree's own
                    // logic exactly: batteries are Go whenever the vessel carries charge-holding parts at
                    // all (neutral only if it carries none, or the feed is dead); the array is Go only
                    // when fully DEPLOYED, amber for any other real state (STOWED / mid-deploy / NONE),
                    // neutral with no feed.
                    bool cellsUp = valid && !string.IsNullOrEmpty(st.BatteryText) && st.BatteryText != "NONE";
                    bool arrayUp = valid && st.SolarArrayText == "DEPLOYED";
                    // QC-AUDIT finding 3, 2026-09-03: MAIN BUS A / B were a hard-coded green "Nominal"
                    // while the systems tree read the SAME two buses off the live model and said BUS OFF.
                    // Two surfaces, one truth, disagreeing on glass (C7.1) — and the buses START OFF
                    // (VehicleSystems.Fresh), so "Nominal" was wrong the moment the page opened. Both
                    // rows now read SystemsState, in exactly the tree's own logic: off is off, a fully
                    // online bus is Nominal, a partly online one is a caution, and a powered bus with no
                    // string left is an alarm.
                    s.CkState = new[] { BusWord(st, valid, 1), BusWord(st, valid, 2),
                                        T(st.BatteryText), T(st.SolarArrayText), "Nominal", "Off" };
                    s.CkKey   = new[] { BusKey(st, valid, 1), BusKey(st, valid, 2),
                                        cellsUp ? 1 : 0, !valid ? 0 : (arrayUp ? 1 : 2), 1, 0 };
                    s.GLabel  = new[] { "BATTERY SOC", "BUS A", "BUS B", "ARRAY" };
                    // BUS A / BUS B are VOLTAGES. KSP's ElectricCharge has no voltage, and the two buses
                    // this build does model are ON/OFF (pure/VehicleSystems.cs) — a different fact, and
                    // one the systems tree already shows. 120 V here would assert a meter that is not there.
                    s.GVal    = new[] { T(st.PowerText), Dash, Dash, T(st.ArrayKwText) };
                    s.GUnit   = new[] { "%", "V", "V", "kW" };
                    s.GFrac   = new[] { F(st.Power01), 0f, 0f, F(st.Array01) };
                    // S104 / QC S-01: the ring's colour is the model's verdict where the model HAS one,
                    // and the neutral reading colour where it does not. These were constants.
                    // Power01 takes `Alarms.Low`, the same read `VehicleSeverity` makes of it. BUS A/B
                    // are dashes, and ARRAY kW has no threshold in the model - Accent, not an invented one.
                    s.GCol    = new[] {
                        Alarms.GaugeColour(Alarms.Low(st.Power01), valid),
                        Accent, Accent, Accent };
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
                    // S24 (owner decision (b)): S-BAND COMMS is the one checklist row this tab wires,
                    // off stock KSP's OWN CommNet (VesselData.Avionics) — T(), so a dead feed or CommNet
                    // itself being off dashes it exactly like every other unsourced row, rather than
                    // reading a stale "Linked". The other five rows are untouched (S25 territory, not
                    // this task) and GPS stays exactly as it was — CommNet is a comm link, not a GPS fix.
                    s.CkState = new[] { "3 / 3", "Nominal", "Nominal", "Lock", T(st.SBandText), "Armed" };
                    s.CkKey   = new[] { 1, 1, 1, 1, (valid && st.SBandText != null) ? (st.SBandLinked ? 1 : 2) : 0, 0 };
                    // ---- MOST OF THIS TAB STILL DASHES, AND THAT IS THE ANSWER ----
                    // The real vehicle's avionics — triple-redundant flight computers, the data bus, GPS,
                    // storage, a link budget — are almost entirely a subsystem this build models NOTHING
                    // of, and no KSP quantity stands in for FC LOAD, BUS TRAFFIC, LINK MARGIN (no dB
                    // conversion exists for a 0..1 CommNet strength), STORAGE, FC1/2/3, GPS Sats or Data
                    // Rate — docs/TELEMETRY_REGISTRY.md, so all seven stay a dash rather than seven
                    // invented numbers that would look exactly as convincing when the feed is dead.
                    // Uplink/Downlink are the exception (S24): stock CommNet is a real, honest source for
                    // a comm link, so those two — and S-BAND COMMS above — are wired to it. The tab's
                    // other live signal is its FDIR severity, which colours the tab and fills the ALERTS
                    // view (see LiveSeverity).
                    s.GLabel  = new[] { "FC LOAD", "BUS TRAFFIC", "LINK MARGIN", "STORAGE" };
                    s.GVal    = new[] { Dash, Dash, Dash, Dash };
                    s.GUnit   = new[] { "%", "%", "dB", "%" };
                    s.GFrac   = new[] { 0f, 0f, 0f, 0f };
                    // S104 / QC S-01: the ring's colour is the model's verdict where the model HAS one,
                    // and the neutral reading colour where it does not. These were constants.
                    // ALL FOUR ARE DASHES. The third was `Go` - a hardcoded GREEN all-clear on a gauge
                    // with no reading behind it, which is S31/S32 read backwards and worse than a false
                    // caution: it asserts health. A dash gets the neutral colour.
                    s.GCol    = new[] { Accent, Accent, Accent, Accent };
                    s.RLabel  = new[] { "FC1 / 2 / 3", "GPS Sats", "Uplink", "Downlink", "Data Rate" };
                    // Uplink and Downlink report the SAME real CommNet signal strength — the link has no
                    // separate up/down budget in stock KSP — as a percentage bar/text, never a fabricated
                    // unit (no "dB", no "kbps": nothing here models a data rate).
                    s.RVal    = new[] { Dash, Dash, T(st.UplinkText), T(st.DownlinkText), Dash };
                    s.RFrac   = new[] { 0f, 0f, F(st.CommSignal01), F(st.CommSignal01), 0f };
                    break;

                case Sub.Gnc:
                    s.Title = "GUIDANCE, NAV & CONTROL"; s.Tab = 6;
                    s.CkLabel = new[] { "IMU 1 / 2", "STAR TRACKERS", "GPS NAV", "RCS AUTHORITY", "NAV STATE", "ATT CONTROL" };
                    // S51/H15, two rows.
                    // RCS AUTHORITY: the same live `RcsOn` the PROP tab's OMS / RCS row reads — one switch,
                    // one source, two tabs. "Enabled" beside a disabled RCS was the contradiction.
                    // ATT CONTROL: reads `ModeText`, the control-authority word this very tab already
                    // prints four rows below in its "Pointing" readout. They were disagreeing on one screen:
                    // the checklist said "Auto" while the readout said IDLE. With no flight software the
                    // honest answer is IDLE (§14.4(a)) and both now say it.
                    bool rcsAuth = valid && st.RcsOn;
                    s.CkState = new[] { "Nominal", "2 / 2", "Lock", rcsAuth ? "Enabled" : "Disabled",
                                        "Valid", T(st.ModeText) };
                    s.CkKey   = new[] { 1, 1, 1, rcsAuth ? 1 : 0, 1, valid ? ModeKey(st.Mode) : 0 };
                    s.GLabel  = new[] { "ROLL RATE", "PITCH RATE", "YAW RATE", "RCS FUEL" };
                    // The Dracos ARE the RCS, so "RCS FUEL" is the propulsion tab's own tank fraction —
                    // one datum, one source, two pages.
                    s.GVal    = new[] { T(st.BodyRollText), T(st.BodyPitchText), T(st.BodyYawText),
                                        T(st.DragonPropText) };
                    s.GUnit   = new[] { "°/s", "°/s", "°/s", "%" };
                    s.GFrac   = new[] { F(Rate01(st.BodyRollDps)), F(Rate01(st.BodyPitchDps)),
                                        F(Rate01(st.BodyYawDps)), F(st.DragonProp01) };
                    // S104 / QC S-01: the ring's colour is the model's verdict where the model HAS one,
                    // and the neutral reading colour where it does not. These were constants.
                    // The three body rates have no threshold in the model. The fourth is DragonProp01,
                    // which is exactly what `Alarms.PropellantSeverity` reads.
                    s.GCol    = new[] { Accent, Accent, Accent,
                        Alarms.GaugeColour(Alarms.Low(st.DragonProp01), valid) };
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
                    // S51/H15, three rows — all reading numbers this same tab already draws.
                    // COOLANT LOOP A/B: banded by `Alarms.Band(LoopAC/LoopBC, LoopCaution, LoopAlarm)` —
                    // the IDENTICAL call `SystemsPidPage` makes for the same two loops, so the P&ID and
                    // this column cannot band one loop two ways. The old "Nominal" stayed green at 60 °C
                    // while the LOOP A gauge two zones right printed that very number.
                    // HEAT SHIELD: banded by `Alarms.High(HullTemp01)` — the hottest structure over its OWN
                    // maximum, which is the fraction the SHIELD gauge on this tab already rings, through the
                    // shared 0.75/0.90 rule. NO NEW THRESHOLD is introduced: `Alarms.High` is the existing
                    // one, so this row and the ring beside it move together.
                    Severity loopA = valid ? Alarms.Band(st.Cabin.LoopAC, CabinLimits.LoopCaution,
                                                         CabinLimits.LoopAlarm) : Severity.Nominal;
                    Severity loopB = valid ? Alarms.Band(st.Cabin.LoopBC, CabinLimits.LoopCaution,
                                                         CabinLimits.LoopAlarm) : Severity.Nominal;
                    Severity shield = valid ? Alarms.High(st.HullTemp01) : Severity.Nominal;
                    s.CkState = new[] { SevWord(loopA), SevWord(loopB), "Deployed", SevWord(shield),
                                        "Auto", "Nominal" };
                    s.CkKey   = new[] { SevKey(loopA), SevKey(loopB), 1, SevKey(shield), 0, 1 };
                    s.GLabel  = new[] { "LOOP A", "LOOP B", "RADIATOR", "SHIELD" };
                    // RADIATOR: the coolant model (pure/CabinEnvironment.cs) carries the two loops but no
                    // separate radiator outlet, and the trunk radiators are not modelled at all.
                    s.GVal    = new[] { T(st.LoopAText), T(st.LoopBText), Dash, T(st.HullTempText) };
                    s.GUnit   = new[] { "°C", "°C", "°C", "°C" };
                    s.GFrac   = new[] { F(st.Cabin.LoopA01), F(st.Cabin.LoopB01), 0f, F(st.HullTemp01) };
                    // S104 / QC S-01: the ring's colour is the model's verdict where the model HAS one,
                    // and the neutral reading colour where it does not. These were constants.
                    // The two loops take CabinLimits' own band. RADIATOR is a dash. SHIELD was `Red` at
                    // any hull temperature; it stays Accent rather than gaining a band, because what
                    // HullTemp01 is normalised AGAINST is not established here and a band invented to
                    // justify a colour is the defect, not the fix (QC S-01).
                    s.GCol    = new[] {
                        Alarms.GaugeColour(Alarms.Band(st.Cabin.LoopAC, CabinLimits.LoopCaution, CabinLimits.LoopAlarm), valid),
                        Alarms.GaugeColour(Alarms.Band(st.Cabin.LoopBC, CabinLimits.LoopCaution, CabinLimits.LoopAlarm), valid),
                        Accent, Accent };
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
