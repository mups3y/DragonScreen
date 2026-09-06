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
        public const int Commands = 320;   // +BottomBar.Commands (S176: the bar is 19 commands, not 2)
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
            "ALL SYSTEMS CHECK", "RENDEZVOUS BURN SLOW", "PREPARE RENDEZVOUS BURN", "THERMAL SHIELD",
            "BURN GO/NO-GO", "POWER COMPLETION", "STATION DECK CHECK" };
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

        // ---- S192 GEOMETRY, 2026-09-07 — every number below is derived, and the derivation is here ----
        // The owner's five named edits, on the marked-up S185 preview. Sources: `Overview.vue`'s CSS
        // (assets/reference/dragon2-ui-assets/) and his own rendered mock (assets/reference/nasa/
        // interface_1950x1260.png, landed and hashed by S184, measured against its 1708x1019 card).

        /// <summary>`dragon_crew.png`'s own pixel size. The capsule's drawn WIDTH is computed from its
        /// height and this ratio at draw time, so the render is undistorted at every resolution —
        /// which a pair of design constants cannot be, because this page maps x and y by different
        /// scales. ⚠ If the art is ever swapped (S185 Q1 has a proven 771x1232 replacement waiting),
        /// these two must be updated with it or the draw goes back to being a stretch.</summary>
        const float ArtW = 294f, ArtH = 468f;

        /// <summary>The capsule's ink height as a share of the frame, taken off the owner's mock:
        /// 468 of its 1019-px card = 0.4593, and 0.4593 x 2112 = 970. The old draw rendered 0.3506 of
        /// height, which is the "short" half of his "short and fat".</summary>
        const float CapH = 970f;

        /// <summary>The capsule's top, in design units. NOT the mock's own 0.3405 of height: the mock
        /// has no global bottom bar and this build's `component_48` starts at y1877, so the taller
        /// capsule is seated to clear CABIN MICS and the tab strip's panel beneath it. 630 puts its
        /// ink at 630..1600 with 25 units clear of the big gauges above.</summary>
        const float CapTop = 630f;

        /// <summary>The small-gauge row. `SmallPitch` is the mock's own 0.1013 of width (347 design
        /// units) and both offsets are measured from this page's centreline. `SmallInner` is the mock's
        /// 407 opened to 430 — the smallest value that clears the enlarged capsule's box — and
        /// `SmallOuter` keeps the mock's pitch above it.</summary>
        const float SmallInner = 430f, SmallOuter = 778f, SmallCy = 1000f;

        /// <summary>CONNECTIONS' left edge: 932 of 3427 = 0.2719 of width, which is the mock's own
        /// 0.2722 and `Overview.vue`'s 0.300 to within its own panel padding. It also lands on LOOP A's
        /// column, exactly as the mock does.</summary>
        const float ConnX = 932f;

        /// <summary>CABIN MICS. The anchor is the page centreline, so the block reads as centred; the
        /// label is drawn RIGHT-aligned just before it and the state LEFT-aligned at it, so a dead feed
        /// shortening the state to a dash moves only the dash. `MicY` clears the capsule's ink bottom
        /// (1600) above and the tab strip's panel top (1682) below.</summary>
        const float MicAnchor = 1713.5f, MicY = 1622f;

        /// <summary>The CONSUMABLES card and its type. The owner called the old sizes — label 23,
        /// value 25, header 24 — "to small"; these are the same table at 34/34/28, inside a bordered
        /// panel. The columns keep T5's own x's, opened left to 2700 so a 34 px label still clears the
        /// QTY column: the longest string, "Orbit 1 Subtank Oxidizer", is 24 characters.</summary>
        /// ⚠ THE COLUMNS WERE OPENED AFTER MEASURING THE FIRST RENDER, NOT BEFORE. At 34 px the longest
        /// label, "Usable Deorbit Oxidizer", ran to design x3020 while its value started at 3040 — a
        /// 20-unit gap, which is the S38/S39 label-to-value failure in the opposite direction: too
        /// close to read as two columns rather than too far. Opened to a 78-unit clearance on that same
        /// row, with the card's left edge still 8 units clear of NET PWR2's ring.
        const float ConsX0 = 2620f, ConsX1 = 3412f, ConsTop = 240f, ConsBot = 1640f;
        const float ConsLabelX = 2665f, ConsQtyX = 3195f, ConsMarginX = 3385f;
        const float ConsRowSize = 34f, ConsHeadSize = 28f, ConsPitch = 145f;

        /// <summary>S148 / S49 H45: the colour a VALUE should draw in — dimmed when it is a dash.
        ///
        /// ⛔ THE POINT IS NOT TIDINESS. A dash drawn in `White` reads with exactly the weight of a live
        /// reading, so at a glance a page of dashes looks like a page of data. That is the third form of
        /// the "can't tell dead from live" failure [[S22]] was opened for — the first was a confident
        /// green word on a dead feed, the second was a fixed gauge colour asserting a verdict (QC S-01).
        ///
        /// ⭐ The CONSUMABLES table on the Overview already did this right — `string.IsNullOrEmpty(qty)
        /// ? Dim : White` — so this is that page's own idiom applied to the gauges and detail rows
        /// beside it, not a new convention.</summary>
        static Rgba ValueTint(string v)
        { return (string.IsNullOrEmpty(v) || v == Dash) ? Dim : White; }

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
                C(val, cxd, cyd - rd * 0.34f, rd * 0.42f, ValueTint(val));
                C(unit, cxd, cyd + rd * 0.30f, 24, Dim);
            }

            dl.Rect(0, 0, w, h, Bg);
            C("VEHICLE OVERVIEW", 1713, 40, 46, White);

            // ---- CENTRE-TOP: four cabin gauges (LIVE — pure/CabinEnvironment.cs) ----
            // Value and ring come from the same CabinReadout, so the needle can never disagree with the
            // number printed inside it. No feed -> a dash and an empty ring, never a confident zero.
            bool valid = s.Valid;
            float F(double frac) => valid ? (float)frac : 0f;
            string T(string live) => (valid && !string.IsNullOrEmpty(live)) ? live : Dash;

            // ---- LEFT: systems checklist ----
            // S22: these seven states are reference COPY, not live data (§6 scoped T13 to the numeric
            // VALUES) — but a dead feed must not leave a confident green "Normal" beside dashed gauges,
            // so the whole row (icon + state word) dashes-and-dims exactly like the CONSUMABLES table's
            // no-source rows and every other dash on this page. The label stays put.
            for (int i = 0; i < ChkLabel.Length; i++)
            {
                float y = 300 + i * 200;
                Rgba sc = !valid ? Dim : ChkKey[i] == 1 ? Go : ChkKey[i] == 2 ? Amber : White;
                dl.Asset("ic_check", PX(90), PY(y), SZ(38), SZ(38), sc);
                L(ChkLabel[i], 150, y + 4, 28, White);
                L(T(ChkState[i]), 150, y + 48, 26, sc);
            }

            // ---- S185 / UNIT 3: THE CENTRE BLOCK IS CENTRED ON THE PAGE, BECAUSE EVERY SOURCE SAYS SO ----
            //
            // ⛔ WHAT WAS WRONG, MEASURED RATHER THAN ASSERTED. This page's centre block did not sit on
            // its own centreline and its two halves did not agree with each other either. The four big
            // gauges ran 1170..2520, centre 1845; the capsule slot ran 1560..2080 and the small gauges
            // 1230/2410, centre 1820. The page's centreline is 1713 (= RefW/2, the value `C("VEHICLE
            // OVERVIEW", 1713, …)` on the line above has always used). So the big row sat +132 design
            // units right of the title above it, the rest sat +107, and the two were 25 units apart.
            //
            // ⭐ THREE INDEPENDENT SOURCES PUT ALL OF IT ON THE CENTRELINE, AND THEY WERE CHECKED, NOT
            // REMEMBERED:
            //   1. `Overview.vue`'s own CSS — `.circular-progess-menu { left: 50%; transform:
            //      translate(-50%,-50%) }` and `#dragon-crew { left: 50% }`. This page's declared source
            //      (see the file header), in-repo at assets/reference/dragon2-ui-assets/.
            //   2. The owner's rendered mock of THIS page, landed and hashed by [[S184]] at
            //      `assets/reference/nasa/interface_1950x1260.png`. Measured on the render: the four big
            //      gauge labels centre at 0.5007 of frame width, the four small ones at 0.4952, the
            //      capsule at ~0.50, all against a page centreline of 0.5000.
            //   3. ⭐ OUR OWN SIBLING PAGE. `VehicleSubsystemPage.cs` draws the SAME asset in the SAME
            //      520x760 slot at `PX(1453)` — slot centre exactly 1713. This page had it at 1560.
            //      Two pages, one asset, one slot, 107 design units apart; one of them was on the
            //      centreline and it was not this one.
            //
            // ---- WHAT MOVED, AND WHAT DELIBERATELY DID NOT ------------------------------------------
            // Every x below is the old value minus a single per-group offset that lands the group's own
            // centre on 1713 — big gauges −132, capsule and small gauges −107. ⛔ NO COORDINATE HERE IS
            // CHOSEN: the 450-unit big-gauge pitch, the 175/120 radii, the 520x760 slot and the ±590
            // small-gauge symmetry are all UNCHANGED, and 1453 is the sibling page's own literal. This
            // is a translation, not a redesign.
            // ⛔ THE PITCH IS UNTOUCHED ON PURPOSE, and that is a §1.4 refusal rather than an oversight:
            // the CSS wants a 0.1375-of-width pitch and the mock renders 0.1225, this build draws
            // 0.1313, and the two sources DISAGREE — so unit 3 kept its own and wrote the disagreement
            // up (C1.14) instead of picking one. Only the CENTRE is agreed, so only the centre moved.
            // ⛔ CONNECTIONS and CABIN MICS did NOT move. The reference places them independently
            // (`.connections-panel { left: 35% }`, `#dragon-main-heading { left: 50% }`), so they are
            // not part of this group's symmetry, and CABIN MICS' own divergence — both sources centre
            // it on the page, this build left-aligns it at 1130 — is logged, not smuggled in here.
            //
            // ---- S104 / QC V-01: THE RING'S COLOUR IS THE MODEL'S VERDICT, NOT A CONSTANT ----
            // These four used to be `Gold, Red, Yellow, Blue` — fixed at any value. The arc's LENGTH was
            // live and its COLOUR was decoration, so CABIN TEMP was alarm-red at 21.8 °C (caution is 30,
            // alarm 35) while SystemsPidPage, banding the SAME value through the SAME function in the
            // SAME frame, printed it green. Two surfaces, one quantity, opposite verdicts — C7.1's own
            // failure — and a permanently-red ring also destroys the signal for when the cabin really
            // does pass 30. `Alarms.Band` handles both directions, so the low-side pair (PPO2, PRESSURE:
            // caution 2.5/13.0, alarm 2.0/11.0) and the high-side pair (TEMP, CO2) take the one call.
            Rgba GC(Severity sev) => Alarms.GaugeColour(sev, valid);
            Gauge(1038, 430, 175, F(s.Cabin.Ppo201),
                  GC(Alarms.Band(s.Cabin.Ppo2Psia, CabinLimits.Ppo2Caution, CabinLimits.Ppo2Alarm)),
                  "PPO2", T(s.Ppo2Text), "psia");
            Gauge(1488, 430, 175, F(s.Cabin.CabinTemp01),
                  GC(Alarms.Band(s.Cabin.CabinTempC, CabinLimits.CabinTempCaution, CabinLimits.CabinTempAlarm)),
                  "CABIN TEMP", T(s.CabinTempText), "°C");
            Gauge(1938, 430, 175, F(s.Cabin.Press01),
                  GC(Alarms.Band(s.Cabin.PressPsia, CabinLimits.PressCaution, CabinLimits.PressAlarm)),
                  "CABIN PRESSURE", T(s.PressText), "psia");
            Gauge(2388, 430, 175, F(s.Cabin.Co201),
                  GC(Alarms.Band(s.Cabin.Co2MmHg, CabinLimits.Co2Caution, CabinLimits.Co2Alarm)),
                  "CO2", T(s.Co2Text), "mmHg");

            // ---- CENTRE: capsule + loop/power gauges ----
            // ⚠ DIVERGENCE from the tier-2 source, 2026-09-02 (S20, owner decision via the overseer):
            // Overview.vue labels BOTH coolant gauges "LOOP A" (lines 222 + 272) — a recreation copy-paste
            // error, not a deliberate reference choice. The real Dragon has two coolant loops, A and B
            // (tier-1 fact); our model computes two distinct loops (Cabin.LoopAC / LoopBC) and the second
            // gauge is wired to Loop B's live value (T13a) — so reproducing "LOOP A" on both would show two
            // different temperatures under one label. docs/REFERENCE_PAGES.md already documents this pair
            // as LOOP A / LOOP B. Owner's call (C1.4): label the second gauge "LOOP B".
            // ---- S192: THE CAPSULE IS THE MOCK'S SIZE AND THE ART'S OWN PROPORTIONS ------------------
            // 🟢 OWNER, 2026-09-07, verbatim: "our 3d render is short and fat, it should be the same size
            // and proportions as the one in green box."
            //
            // ⛔ "SHORT AND FAT" WAS MEASURABLE AND HE IS RIGHT ON BOTH COUNTS.
            //   FAT: the old draw was `520 * sx, 760 * sy` — a design box whose DEVICE aspect at the
            //     shipped 2560x1406 is 0.768 against the art's own 0.628. A 22.2 % horizontal stretch of
            //     a PNG carrying the SPACEX, NASA and DRAGON wordmarks, which is exactly what QC `C-04`
            //     forbids. S185 measured it and was told to write it up rather than fix it; this is the
            //     owner answering that question in favour of the render's own proportions.
            //   SHORT: on his mock the capsule's ink is 0.4593 of frame height. The old draw rendered
            //     0.3506. He is asking for it 1.31x taller, which is what `CapH` below is.
            //
            // ⭐ AND THE WIDTH IS COMPUTED AT DRAW TIME, WHICH IS THE ONLY THING THAT ACTUALLY FIXES IT.
            // This page maps x through `sx` and y through `sy`, so a FIXED design box has a
            // RESOLUTION-DEPENDENT device aspect — at 2560x1406 the same 520x760 reads 0.768, at the
            // design frame it reads 0.684. Any pair of design constants is therefore wrong at every
            // resolution but one. The height is design-anchored and the width falls out of it and the
            // art's own pixel aspect, in DEVICE units — the pattern `Images.FitHeight` and S103's
            // `BarFit` already use for exactly this reason.
            float capH = CapH * sy;
            float capW = capH * (ArtW / ArtH);
            dl.Asset("dragon_crew", PX(1713.5f) - capW * 0.5f, PY(CapTop), capW, capH, White);

            // ---- S192: THE FOUR SMALL GAUGES ARE ONE ROW, FLANKING THE CAPSULE ----------------------
            // 🟢 OWNER, 2026-09-07, verbatim: "Loop a loop b net pwr 1 net pwr 2 need to be arranged in
            // the same layout" — i.e. the mock's, which is one row of four rather than two stacks of two.
            // This closes [[S186]], which measured the divergence and left it for him.
            //
            // ⛔ THE POSITIONS ARE DERIVED, NOT PICKED. Both of this page's sources agree on this row to
            // within 1.5 %: `Overview.vue` puts the four `#sub-sub-slot` divs at 0.25 / 0.35 / 0.65 /
            // 0.75 of screen width (a 0.100 pitch), and the mock renders them at 0.2749 / 0.3762 /
            // 0.6139 / 0.7155 (a 0.1013 pitch). `SmallPitch` below is the mock's, and the pair is
            // symmetric about this page's own centreline rather than about the mock's 0.4952 — the
            // centreline S185 put everything else on.
            // ⚠ ONE ADJUSTMENT, AND IT IS STATED RATHER THAN HIDDEN: the mock's inner offset is 0.1187
            // of width (407 design units), which at the capsule's NEW width would put the two inner
            // rings 21 units INSIDE its box. `SmallInner` is 430 — the smallest offset that clears the
            // enlarged capsule — and `SmallOuter` keeps the mock's pitch above it. The alternative was
            // to shrink the capsule, which is the thing the owner asked to make bigger.
            Gauge(1713.5f - SmallOuter, SmallCy, 120, F(s.Cabin.LoopA01),
                  GC(Alarms.Band(s.Cabin.LoopAC, CabinLimits.LoopCaution, CabinLimits.LoopAlarm)),
                  "LOOP A", T(s.LoopAText), "°C");
            Gauge(1713.5f - SmallInner, SmallCy, 120, F(s.Cabin.LoopB01),
                  GC(Alarms.Band(s.Cabin.LoopBC, CabinLimits.LoopCaution, CabinLimits.LoopAlarm)),
                  "LOOP B", T(s.LoopBText), "°C");
            // Net power is SIGNED — the sign lives in the printed number, the ring shows how hard the
            // bus is working either way, against the same full scale the model states.
            // ⛔ THESE TWO STAY `Accent`, DELIBERATELY (S104 / QC V-01). Nothing in the model bands net
            // power — a severity here would mean "discharging faster than X" and no X exists. Accent is
            // this build's "a reading, not a verdict" colour, and inventing a threshold to justify a
            // colour is the defect V-01 removes, not a smaller version of the fix.
            Gauge(1713.5f + SmallInner, SmallCy, 120, F(NetPwr01(s.Cabin.NetPwr1W)), Accent,
                  "NET PWR1", T(s.NetPwr1Text), "W");
            Gauge(1713.5f + SmallOuter, SmallCy, 120, F(NetPwr01(s.Cabin.NetPwr2W)), Accent,
                  "NET PWR2", T(s.NetPwr2Text), "W");

            // ---- CONNECTIONS (left of the capsule base) ----
            // S22: "Connected" / "RECORDING" are reference COPY too, same as the checklist above — dash
            // and dim them on a dead feed rather than let a confident green/red word sit beside dashed
            // gauges. The row labels and the CONNECTIONS/CABIN MICS section labels are untouched.
            // ---- S192: THIS BLOCK MOVED, AND THE MOVE WAS FORCED RATHER THAN CHOSEN -----------------
            // The owner named five edits and this was not one of them; his green box covers it, and his
            // OWN change makes it unavoidable. The capsule he asked to enlarge now spans design x
            // 1405..2022, and CONNECTIONS' values sat at x1400 — they would overlap. So the block goes
            // to the x BOTH of this page's sources already put it at, which is also where [[S189]]
            // measured it should be: `Overview.vue` has `.connections-panel { left: 35%; width: 10% }`,
            // i.e. a left edge at 0.300 of width, and the mock renders its left edge at 0.2722. 932
            // design units is 0.2719 — the mock's, to three decimal places.
            // ⭐ AND IT LANDS ON LOOP A'S OWN COLUMN, which is what the mock does too: mock CONNECTIONS
            // left 0.2722 against mock LOOP A centre 0.2749. Here 932 against 935.5.
            // ⚠ THE ROWS ALSO MOVED UP, for the same forced reason: the tab strip's new panel starts at
            // design y1682 (VehicleTabBar), and the old last row sat at 1668 with its ink below that.
            L("CONNECTIONS", ConnX, 1330, 26, Accent);
            string[] cn = { "Manual Rings", "Changelog", "Airlock", "Wing" };
            for (int i = 0; i < 4; i++)
            {
                L(cn[i], ConnX, 1390 + i * 56, 24, White);
                L(T("Connected"), ConnX + 270f, 1390 + i * 56, 24, valid ? Go : Dim);
            }
            // ---- S192: CABIN MICS IS CENTRED UNDER THE CAPSULE, WHICH IS WHERE BOTH SOURCES PUT IT ---
            // The second forced move. It sat left-aligned at x1130, y1748 — and y1748 is now inside the
            // tab strip's panel (1682..1877), so it could not stay. `Overview.vue` has
            // `#dragon-main-heading { top: 82.5%; left: 50%; transform: translate(-50%,-50%) }` and the
            // mock renders the block centred at 0.5003 of width; [[S189]] measured this build at 0.3729,
            // a −0.127-of-width divergence and the largest single one left on the page. Centring it
            // closes that item as a side-effect of clearing the panel.
            // ⛔ IT IS TWO DRAWS WITH TWO TINTS AND THE SECOND CHANGES WIDTH WITH THE FEED, so it cannot
            // be centred by shifting an x. The label is drawn RIGHT-aligned to a fixed anchor and the
            // state LEFT-aligned just past it: the anchor is what is centred, the label's right edge is
            // therefore fixed, and a dead feed shortening "RECORDING" to a dash moves only the dash.
            // A block centred on the live string would jump left when the feed died, which is worse.
            R("CABIN MICS:", MicAnchor - 16f, MicY, 26, White);
            // ---- S105 / QC V-02: RECORDING IS A STATE, NOT A FAULT ----
            // This was drawn in `Red` - the alarm colour - for a recorder that is working. §14.4(a): no
            // red for something that is not a fault, and CLAUDE.md quotes that rule for exactly this
            // reason. It reads `Go` now, matching the four `Connected` rows immediately above it: they
            // are the same kind of thing, a state that is currently true, and the block should read as
            // one. A FAILED recorder would be the red case, and nothing models one.
            dl.Text(T("RECORDING"), PX(MicAnchor), PY(MicY), SZ(26), TextAlign.Left, valid ? Go : Dim);

            // ---- RIGHT: CONSUMABLES table (T5) ----
            // ---- S192: BIGGER TYPE, AND A BLOCK INSTEAD OF EIGHT LOOSE ROWS -------------------------
            // 🟢 OWNER, 2026-09-07, verbatim: "The consumable list on the right hand side text is to
            // small and the whole list looks plain and out of place."
            //
            // ⭐ THIS IS THE RULING [[S153b]] HAS BEEN HELD WAITING FOR, ON THIS BLOCK. R-01's census
            // counts draws BELOW the legibility floor, and this table was the second-worst offender on
            // the page: `docs/QC_FINDINGS.md` measures its rows at `SZ(23)` = 7.7 mm, 48 % of the floor,
            // and the header/values at 24/25. The owner has now said in as many words that it is too
            // small. ⛔ So the census will read IMPROVED, not regressed — which is the one direction
            // S153b's hold was never about, since nothing here takes a draw further below the floor.
            // ⚠ [[S153b]] IS NOT CLOSED BY THIS. It owns 441 draws across eight page-views; this moves
            // the twenty-seven in this table and nothing else. Its own question stands.
            //
            // ⛔ AND "PLAIN AND OUT OF PLACE" IS THE OTHER HALF, WHICH TYPE ALONE DOES NOT FIX. Every
            // other block on this page is bounded — the gauges by their rings, the checklist by its
            // icons, the capsule by its own silhouette — and this one was eight rows of loose text
            // floating on the ground with a hairline under each. It now sits on a panel with a border
            // and a ruled header, the same construction `DragonPalette.Panel` + `Strokes.Px` gives
            // every other card in this build. The COLUMNS and the CONTENT are untouched: T5 settled
            // what this table says, [[S79]] settled what MARGIN computes, and neither is reopened here.
            float cardX = PX(ConsX0), cardY = PY(ConsTop);
            float cardW = (ConsX1 - ConsX0) * sx, cardH = (ConsBot - ConsTop) * sy;
            dl.Rect(cardX, cardY, cardW, cardH, Panel);
            dl.Box(cardX, cardY, cardW, cardH, Strokes.Px(2f, sy), Faint);
            L("CONSUMABLE", ConsLabelX, 300, ConsHeadSize, Accent);
            R("QTY", ConsQtyX, 300, ConsHeadSize, Accent);
            R("MARGIN", ConsMarginX, 300, ConsHeadSize, Accent);
            dl.Rect(PX(ConsLabelX), PY(348), (ConsMarginX - ConsLabelX) * sx, SZ(3), Accent);
            for (int i = 0; i < ConsLabel.Length; i++)
            {
                float y = 400 + i * ConsPitch;
                string qty = valid ? Qty(i, s) : null;
                string margin = valid ? Margin(i, s) : null;
                L(ConsLabel[i], ConsLabelX, y, ConsRowSize, White);
                R(qty ?? Dash, ConsQtyX, y, ConsRowSize, string.IsNullOrEmpty(qty) ? Dim : White);
                R(margin ?? Dash, ConsMarginX, y, ConsRowSize,
                  string.IsNullOrEmpty(margin) || margin == Dashes.None ? Dim : White);
                if (i < ConsLabel.Length - 1)
                    dl.Rect(PX(ConsLabelX), PY(y + ConsRowSize + 14f),
                            (ConsMarginX - ConsLabelX) * sx, SZ(2), Faint);
            }
            // ---- S75: "SHOW MARGINS TO" IS NOT A CONTROL, SO IT MUST NOT BE PAINTED AS ONE ----
            // The reference render carries this as a toggle (SCREEN_INVENTORY.md's DillonBaird alt-text
            // capture, 2026-09-01), and it was drawn here in Accent — the SAME tint this page gives the
            // two deep-view links that ARE touchable (SYSTEMS TREE / SYSTEMS P&ID, VehicleDeepViewLinks)
            // — while having no hit rect anywhere. SCREEN_LIVENESS_AUDIT.md H18 calls that worse than a
            // no-op: a no-op at least resolves to a named action, whereas this resolves to nothing and a
            // crew would still press it. It cannot be given a rectangle until it is decided what the
            // rectangle DOES, and that answer is not a build chat's to invent (C1.4/§1.4): the alt-text
            // capture records the toggle's EXISTENCE and none of its targets, and the MARGIN column it
            // would switch between is itself still unfilled (S76).
            // So it takes S75's OTHER branch — drawn as INERT TEXT rather than as a button. Dim is this
            // page's own "no live source behind this" tint (the dashed CONSUMABLES rows, the dead-feed
            // checklist), which is exactly what this is until S76 lands. When the MARGIN column reads
            // modelled margins and a target set is settled, this goes back to Accent AND gains a rect —
            // the two happen together or not at all. Pinned by FigmaUINavTest.
            L("SHOW MARGINS TO", ConsLabelX, 400 + ConsLabel.Length * ConsPitch + 10f, ConsHeadSize, Dim);

            // ---- subsystem tab bar (All active) + bottom status bar ----
            // The real Vehicle page carries the eight-subsystem strip (VehicleTabBar); "All" is this
            // overview. It replaces the reference-demo's SYSTEMS/CABIN + Overview/Mech + MORE cluster.
            // T5: severity-aware (VehicleTabBar.Severities) so a faulted subsystem's tab reads red from
            // here too — the real "reached in one touch from anywhere" behaviour, not just on its own page.
            VehicleTabBar.Draw(dl, w, h, 0, VehicleTabBar.Severities(s));
            VehicleDeepViewLinks.Draw(dl, w, h);
            BottomBar.Draw(dl, w, h, s, BarFit.Stretch);   // S103: undistorted; S147: CURRENT STATE live;
            // S176 / S172: FULL BLEED - the bar takes this page's own x-map, not the letterbox.
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
        /// <summary>
        /// ⭐ S79: THE MARGIN COLUMN, AND IT IS TIME-TO-DEPLETION ON EVERY ROW THAT HAS ONE.
        ///
        /// The owner answered S79-Q1 on 2026-09-06 by selecting, from presented options:
        ///
        ///     1. Time-to-depletion, one currency for the whole column — hours or days remaining at
        ///        the current modelled rate.
        ///
        /// ⛔ Recorded as a SELECTION, not a verbatim quote (C1.12's evidentiary standard). ONE
        /// currency for the whole column — not split by row family, which the options explicitly did
        /// not recommend because one header over two units is what S38/S39 show crews misread.
        ///
        /// ---- WHAT THIS REPLACED, AND WHY IT WAS WORSE THAN EMPTY ------------------------------
        /// `R(Dash, 3360, y, 25, Dim)` — the same literal on all eight rows, on a live feed and a dead
        /// one alike, under a header that says MARGIN. The page was asking a question it never
        /// answered. ⛔ NOTHING HERE PRINTS A DASH OF ITS OWN: every cell goes through `Depletion`,
        /// which produces one only when the inputs say no depletion time exists. That is what lets a
        /// fixture-A-vs-fixture-B test prove this column is not a constant.
        ///
        /// ---- ⛔ AND `LifeSupport.Margins` IS NOT WIRED HERE, DELIBERATELY -----------------------
        /// `SCREEN_LIVENESS_AUDIT.md` H18 and H39 both name it as "the natural filling for H18's MARGIN
        /// column". That is true of its SHAPE and false of its CONTENT: `LsMargins` carries
        /// Food / Water / Oxygen days, and NONE of these eight rows is food, water or oxygen. It fills
        /// none of them, and a task that takes H39 at its word finds that out after wiring it. The Crew
        /// tab is the likelier host; that is S57's call, not this column's.
        /// </summary>
        static string Margin(int row, PageState s)
        {
            switch (row)
            {
                // ---- the two power units: stored charge over what the bus is actually drawing ----
                // ⚠ GATED ON THE QTY TEXT, which is this page's existing single answer to "is there a
                // source" — so the MARGIN cell can never claim a reading the QTY cell says is absent.
                // ⛔ AND THE SIGN IS THE POINT. `CabinEnvironment` publishes NET power: negative while
                // draining, positive while the arrays make more than the load. The draw is therefore
                // −NetPwr, and a CHARGING bus falls out as a dash through `Depletion`'s rate test
                // rather than through a branch here. Passing the magnitude would print a countdown for
                // a pack that is filling up.
                case 0:
                    return string.IsNullOrEmpty(s.PowerUnit1Text) ? Dashes.None
                        : Depletion.BusText(s.EcUnits, Depletion.WattsPerEcPerSecond, -s.Cabin.NetPwr1W);
                case 1:
                    return string.IsNullOrEmpty(s.PowerUnit2Text) ? Dashes.None
                        : Depletion.BusText(s.EcUnits, Depletion.WattsPerEcPerSecond, -s.Cabin.NetPwr2W);

                // ---- the two deorbit tanks: kg left over kg/s being burned ----
                // ⚠ ACCEPTED CONSEQUENCE OF ONE CURRENCY, RECORDED ON S79 AND NOT A HOLE: these read a
                // real countdown only WHILE A BURN IS DRAWING THEM DOWN, and dash the rest of the
                // mission, because at zero rate no depletion time exists. ⛔ That dash is COMPUTED — it
                // comes out of `DeorbitFuelFlowKgS` being zero — and a printed literal here would fail
                // this row's own fixture-A-vs-fixture-B check, because a constant cannot start reading
                // a countdown when a fixture starts burning.
                case 2:
                    return string.IsNullOrEmpty(s.DeorbitFuelText) ? Dashes.None
                        : Depletion.Text(s.DeorbitFuelKg, s.DeorbitFuelFlowKgS);
                case 3:
                    return string.IsNullOrEmpty(s.DeorbitOxText) ? Dashes.None
                        : Depletion.Text(s.DeorbitOxKg, s.DeorbitOxFlowKgS);

                // ---- the four Orbit n Subtank rows: a REASONED dash, and the reason is here ----
                // Their QTY is already dashed because the real vehicle's tank split has no KSP
                // counterpart and guessing which litres belong to which subtank would be inventing the
                // number (docs/TELEMETRY_REGISTRY.md). ⛔ A MARGIN ON A QUANTITY THAT DOES NOT EXIST
                // DOES NOT EXIST EITHER — this is §14.4(e)'s "a dash ONLY where the quantity truly does
                // not exist", and it is the one place on this column where that clause applies.
                // ⚠ It is not a literal escaping the rule above: `Dashes.None` here is the answer to a
                // question about the VEHICLE, not a placeholder for an answer nobody computed.
                default: return Dashes.None;
            }
        }

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
