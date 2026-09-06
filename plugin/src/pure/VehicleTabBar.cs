// DragonScreen — VehicleTabBar  (PURE: the Vehicle page's subsystem sub-tab strip)
// ============================================================================================
// The real Crew Dragon Vehicle page carries ONE sub-tab bar across every vehicle view — eight
// subsystem tabs, the selected one lit with a sliding accent underline (confirmed from the clean
// designer mockup, shanemielke.com "ui1"): All · Crew · Prop · Mech · Power · Avionics · GNC ·
// Thermal. It replaces the reference-demo's two-tab "Overview / Mech" strip. Drawn by every vehicle
// page just above the global bottom bar; FigmaUI.HitTest routes a touch on it to the sibling page.
//
// PURE: geometry only. The tab→page mapping and the "which vehicle page am I on" bookkeeping live in
// FigmaUI, which references CentreX/HitTest here so the drawn strip and the hit strip never drift.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class VehicleTabBar
    {
        // S192: 8 labels + 1 underline + the panel's 4 pieces + 8 icon assets (+ headroom).
        public const int Commands = 40;
        const float RefW = 3427f, RefH = 2112f;

        /// <summary>The eight subsystem tabs, in order. Index is the "active tab" the pages pass in
        /// and the value FigmaUI maps to a UiPage — never reorder.</summary>
        public static readonly string[] Tabs =
            { "All", "Crew", "Prop", "Mech", "Power", "Avionics", "GNC", "Thermal" };

        // Row centred on the screen (design centre x = 1713.5), sitting just above the bottom bar's
        // VISIBLE edge at design y1983 (see the panel block below: `BarY` 1877 is the bar's box, not its
        // artwork). 8 slots at this pitch span x≈996..2431.
        const float Pitch = 205f, LabelY = 1908f, LabelSize = 28f;
        const float MarkY = 1954f, MarkW = 140f, MarkH = 6f;
        static float Start { get { return 1713.5f - Pitch * (Tabs.Length - 1) * 0.5f; } }

        // ---- S192: THE PANEL AND THE ICONS, BECAUSE THE REFERENCE STRIP HAS BOTH -------------------
        // 🟢 OWNER, 2026-09-07, verbatim: "Overview tab bottom bar is missing its background and icons."
        //
        // The mock (assets/reference/nasa/interface_1950x1260.png) draws this strip on a RAISED PANEL,
        // a shade lighter than the page ground, with an icon above every label. This build drew eight
        // bare words on the background. Measured on the mock's own 1708x1019 card: the panel's top edge
        // sits at 0.9068 of height and it runs to the card's bottom; the icon band is 0.8950..0.9431,
        // i.e. icons about 0.048 of frame height.
        //
        // ⚠ THE MOCK'S Y CANNOT BE COPIED AND THAT IS NOT A DEFECT. The mock has no global bottom bar;
        // this build does. So the panel is fitted into the band this page actually has and the icon band
        // is scaled to it. The LABEL row keeps its size 28 — S153b is HELD on this family's type and
        // nothing here moves it.
        // ⚠ SUPERSEDED IN PLACE, 2026-09-07 (S192b): this paragraph originally said the band was
        // "1682..1877" because `component_48` "starts at design y1877". The band is now 1778..1973 and
        // the label row 1908, for the reason the S192b block immediately below sets out — 1877 is the
        // bar's BOX, not its visible edge, which is 1983. The original wording is kept above rather than
        // rewritten, because the mistake it records is the whole reason the next block exists.
        //
        // ⛔ THE PANEL'S ENDS ARE ROUNDED, NOT CHAMFERED, and that is a deliberate approximation. The
        // mock cuts a diagonal chamfer at each end; `DisplayList` has no filled-triangle primitive, and
        // faking one with a thick `Line` would leave a seam that moves with resolution. A rounded end is
        // the honest near-match with the primitives that exist — the same call `CoverPage`'s pill caps
        // already make. Recorded so a later chat does not read it as an oversight.
        // ---- S192b: DROPPED 96 UNITS, TO SIT JUST ABOVE THE BAR ITSELF ---------------------------
        // 🟢 OWNER, 2026-09-07, verbatim, with arrows drawn from the strip down to the bar:
        // "Move it so it sits just above the bottom bar".
        //
        // ⭐ AND THE REASON THERE WAS A GAP IS WORTH RECORDING, BECAUSE THE OBVIOUS NUMBER IS WRONG.
        // `BottomBar.BarY` is 1877 and the first version of this panel ended exactly there — flush with
        // the bar, by that number. But 1877 is the top of the bar's BOX, not of anything you can see:
        // `component_48` carries about 106 design units of TRANSPARENT margin above its own artwork, so
        // the bar's visible top edge is at design y1983. Measured on the render rather than assumed —
        // the frame rule is flat at 1983 for every column from x124 to x2530, rising only at the two
        // extreme corners, which neither this panel nor the pills reach.
        // ⛔ So the panel now ends at 1973, ten units clear of the bar's real edge, and everything
        // inside it moved by the same +96: the icons, the labels and the active underline. ⚠ THE LABEL
        // SIZE DID NOT MOVE — only its y. S153b is HELD on this family's type and a translation is not
        // a resize.
        const float PanelX0 = 900f, PanelX1 = 2530f, PanelTop = 1778f, PanelBot = 1973f, PanelR = 44f;
        const float IconCy = 1844f, IconSize = 84f;

        /// <summary>Design-x of tab i's centre.</summary>
        public static float CentreX(int i) { return Start + i * Pitch; }

        /// <summary>T5: the real Crew Dragon "subview nav bar ... turns red when that subview holds an
        /// alert" (REAL_DRAGON_SCREENS.md §2) computed per tab from the SAME live signals the rest of the
        /// screens already use — Alarms.LifeSupport/Thermal on the cabin, Alarms.PropellantSeverity/Low
        /// on Dragon's own propellant and power,
        /// Alarms.FdirSeverity on the fault spine (Avionics and GNC share the one real fault channel this
        /// build has; there is no second one to invent). Mech (index 3) has no live signal wired to it yet
        /// and reports Nominal — honest, not invented. Index matches Tabs: All·Crew·Prop·Mech·Power·
        /// Avionics·GNC·Thermal. Guarded on s.Valid per the SettingsPage/ScreenPainter precedent.</summary>
        public static Severity[] Severities(PageState s)
        {
            if (!s.Valid)
                return new[] { Severity.Nominal, Severity.Nominal, Severity.Nominal, Severity.Nominal,
                               Severity.Nominal, Severity.Nominal, Severity.Nominal, Severity.Nominal };

            Severity crew = Alarms.LifeSupport(s.Cabin);
            Severity prop = Alarms.PropellantSeverity(s);
            Severity power = Alarms.Low(s.Power01);
            Severity fdir = Alarms.FdirSeverity(s);
            Severity thermal = Alarms.Thermal(s.Cabin);
            Severity all = Alarms.Worst(Alarms.Worst(crew, prop), Alarms.Worst(power, Alarms.Worst(fdir, thermal)));
            return new[] { all, crew, prop, Severity.Nominal, power, fdir, fdir, thermal };
        }

        /// <summary>Draw the strip with tab <paramref name="active"/> lit + underlined. No alert data —
        /// every tab reads nominal (used by pages T5 hasn't wired yet, e.g. VehicleMechPage).</summary>
        public static void Draw(DisplayList dl, int w, int h, int active) { Draw(dl, w, h, active, null); }

        /// <summary>As above, plus per-tab alert severity (T5) — a tab in Caution/Alarm draws in that
        /// colour regardless of active state, so a faulted subsystem is visible from every vehicle page.</summary>
        public static void Draw(DisplayList dl, int w, int h, int active, Severity[] tabSeverity)
        {
            float sx = w / RefW, sy = h / RefH;

            // ---- the panel, behind everything else in this strip ----
            float px0 = PanelX0 * sx, px1 = PanelX1 * sx;
            float pt = PanelTop * sy, pb = PanelBot * sy, pr = PanelR * sy;
            Rgba plate = DragonPalette.Panel;
            dl.Rect(px0, pt + pr, px1 - px0, pb - pt - pr, plate);
            dl.Rect(px0 + pr, pt, px1 - px0 - pr * 2f, pr, plate);
            dl.ArcBand(px0 + pr, pt + pr, 0f, pr, 270.0, 360.0, plate);
            dl.ArcBand(px1 - pr, pt + pr, 0f, pr, 0.0, 90.0, plate);

            for (int i = 0; i < Tabs.Length; i++)
            {
                float cx = CentreX(i);
                bool on = (i == active);
                Severity sev = (tabSeverity != null && i < tabSeverity.Length) ? tabSeverity[i] : Severity.Nominal;

                // ---- S193: THE ICON IS WHITE AT NOMINAL, WHATEVER THE SELECTION -------------------
                // 🟢 OWNER, 2026-09-07, verbatim: "Icons in the bar you just moved are white when
                // nominal, turn orange then red for issues that need the user attention like low fuel
                // or power etc."
                //
                // ⛔ WHAT WAS WRONG. Icon and label shared ONE colour, and at nominal that colour was
                // `White` only on the SELECTED tab — every other icon was `Text6`, the dim tint this
                // build uses for "no live source behind this". So seven of the eight icons read as
                // half-dead on a page where every one of them was reporting nominal, and the dim tint
                // meant two different things on the same strip.
                //
                // ⭐ SO THE TWO ARE SPLIT, AND THE SPLIT IS THE POINT. The ICON carries the SUBSYSTEM'S
                // HEALTH — white nominal, `Caution` orange, `Alarm` red — and it says that whether or
                // not you are looking at that tab, which is the whole reason the real vehicle puts
                // severity on a nav bar (REAL_DRAGON_SCREENS.md §2: "displays red when alerts exist in
                // that subview"). The LABEL keeps carrying SELECTION, dim for the tabs you are not on,
                // which is what tells you where you are. One glyph, two facts, neither borrowing the
                // other's colour.
                // ⚠ A DEAD FEED IS STILL NOT NOMINAL: `Severities` returns all-Nominal when `s.Valid`
                // is false, and on that path the icon would go white while the page's gauges dash. So
                // the caller's severity array being NULL — the "no alert data" overload, used by pages
                // T5 never wired — keeps the icon dim rather than asserting white health it has not
                // been given. Absence of data is not a clean bill.
                bool known = tabSeverity != null && i < tabSeverity.Length;
                Rgba iconCol = sev != Severity.Nominal ? Alarms.Colour(sev)
                             : known ? DragonPalette.White : DragonPalette.Text6;
                Rgba labelCol = sev != Severity.Nominal ? Alarms.Colour(sev)
                              : on ? DragonPalette.White : DragonPalette.Text6;
                Icon(dl, i, cx * sx, IconCy * sy, IconSize * sy, iconCol);
                dl.Text(Tabs[i], cx * sx, LabelY * sy, LabelSize * sy, TextAlign.Centre, labelCol);
                if (on)
                    dl.Rect((cx - MarkW * 0.5f) * sx, MarkY * sy, MarkW * sx, MarkH * sy,
                            sev != Severity.Nominal ? Alarms.Colour(sev) : DragonPalette.Accent);
            }
        }

        /// <summary>The harvested tab icons, in `Tabs` order. Shipped PNGs under
        /// `art/cover/`, keyed out of the owner's own high-resolution render of this page.</summary>
        static readonly string[] IconKey = {
            "ic_tab_all", "ic_tab_crew", "ic_tab_prop", "ic_tab_mech",
            "ic_tab_power", "ic_tab_avionics", "ic_tab_gnc", "ic_tab_thermal" };

        /// <summary>
        /// One tab's icon, centred on (cx, cy) in a SQUARE box of side <paramref name="s"/>, tinted to
        /// the tab's own colour so a faulted subsystem's icon reddens with its label (T5) exactly as
        /// the label does.
        ///
        /// ---- S192, SECOND PASS: THESE ARE HARVESTED ART, NOT VECTORS -----------------------------
        /// 🟢 OWNER, 2026-09-07, verbatim, on the first render of this strip: "that strip looks shit.
        /// Harvest the icons as asset from the example screen."
        ///
        /// ⛔ WHAT THE FIRST PASS DID AND WHY IT WAS WRONG. It drew eight glyphs from `Rect`/`Line`/
        /// `ArcBand` primitives, on the reasoning that `art/cover/` held no subsystem icons and that
        /// inventing eight PNGs would be art with no source (§1.4). The premise was right and the
        /// conclusion was wrong: there IS a source, and it is the owner's own render of this page. The
        /// icons did not have to be invented, only cut out.
        ///
        /// ⭐ WHERE THEY CAME FROM, AND WHY FROM THE 2352x1410 FILE RATHER THAN THE 1950 ONE. Both are
        /// renders of this page; the larger has no bezel, so its card is 2352 px wide against the
        /// other's 1708 — 1.38x the resolution, which put the icons at 40-45 px instead of 30. Keyed
        /// off the panel's own uniform ground (26,28,72) by per-channel excess, normalised so each
        /// icon's own peak reaches full alpha (without that the two RED icons in the source — Overview
        /// and Life — would have keyed out at 80 % and rendered faded), then written WHITE so the tint
        /// below is what colours them. `docs/reference/NASA_REFERENCE_ART.md` carries the per-file
        /// hashes and the source rectangles.
        ///
        /// ⚠ SQUARE CANVASES, AND THAT IS QC `C-04`, NOT TIDINESS. The source glyphs are not square —
        /// Power is 21x40, Avionics 45x44 — so each was centred on a square canvas of its own longer
        /// side. The draw below is therefore `SZ(s)` on BOTH axes: a uniform scale, which cannot
        /// stretch a glyph however the strip is later resized. It is the same construction `ic_eye`
        /// already ships with (S181's own trap-1 table records its ink filling 0.688 of its box).
        ///
        /// ⚠ AND THE MOCK HAS NINE TABS WHERE THIS STRIP HAS EIGHT. Its Overview/Life/Comms become this
        /// build's All/Crew, so `ic_tab_all` is its rocket and `ic_tab_crew` its person; its Comms wifi
        /// glyph was NOT harvested, because this strip has no Comms tab to put it on (T9's eight tabs
        /// are confirmed-real from the clean designer mockup and are not changed to suit an icon).
        /// </summary>
        static void Icon(DisplayList dl, int i, float cx, float cy, float s, Rgba c)
        {
            if (i < 0 || i >= IconKey.Length) return;
            dl.Asset(IconKey[i], cx - s * 0.5f, cy - s * 0.5f, s, s, c);
        }

        /// <summary>Which tab (0..7) a touch hit, or -1. Contiguous slots (half-pitch each side).</summary>
        public static int HitTest(float px, float py, int w, int h)
        {
            float dx = px * RefW / w, dy = py * RefH / h;
            // S192 / TRAP 3 — the hit band follows the DRAWING. The strip used to be eight bare words
            // and the band was the words' own rows (LabelY-34 .. MarkY+20). It is now a panel with an
            // icon above each label, so a touch on the icon has to hit the tab it belongs to; the band
            // is the panel. Draw and hit moved together, which is the whole rule.
            if (dy < PanelTop || dy >= PanelBot) return -1;
            for (int i = 0; i < Tabs.Length; i++)
            {
                float cx = CentreX(i);
                if (dx >= cx - Pitch * 0.5f && dx < cx + Pitch * 0.5f) return i;
            }
            return -1;
        }
    }
}
