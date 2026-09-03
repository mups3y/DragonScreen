// DragonScreen — FigmaUI  (PURE: the new Figma-design navigation layer)
// ============================================================================================
// The mod's screens are being rebuilt from the Figma design. This is the NEW page map, built with the
// old 5-page model as a guide but driven by the design's own navigation: the Cover page is the hub,
// and its buttons (rail menu, phase selector, actions, settings) route here to the other pages. Every
// button is wired NOW — the ones whose pages exist go to the real page (Cover, the live attitude HUD,
// Audio settings, and the static Procedure/Cabin frame renders); the rest go to an honest Placeholder
// that names the destination, so the whole UI is navigable and testable while the remaining pages are
// filled in one at a time.
//
// PURE: this only decides WHICH page a screen shows and WHERE a touch on it leads (a NavHit). The
// per-screen "current page" + back/forward history + persistence live in the painter, exactly as the
// old model's page index did — see PageSelection.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>The pages in the new Figma UI. The integer value is what a screen persists (PageSelection),
    /// so append new pages at the END — never renumber, or a save reopens on the wrong page.</summary>
    public enum UiPage
    {
        Cover = 0, Hud = 1, Audio = 2, Procedure = 3, Cabin = 4,
        Menu = 5, PhaseDeport = 6, PhaseCoast = 7, PhaseClaw = 8, PhaseManual = 9,
        ActOnSpaceX = 10, ActDeorbitBrief = 11, ActReview = 12, ActAcknowledge = 13, Entry = 14,
        // Panels 3 and 4 of the reference UI (github demo): a vehicle/systems overview and the suit
        // leak check. Not yet designed in our Figma — placeholders until built. Appended (never
        // renumber): the int persists per screen. See [[dragonscreen-figma-ui-rebuild]].
        Vehicle = 15, SuitCheck = 16, VehicleMech = 17, AudioVideo = 18,
        // Real Dragon procedure screen reconstructed from photos (no Figma/demo ref). Nav entry point
        // (a phase-rail "Procedure" item) is wired in the touch pass.
        VrioTest = 19,
        // The Vehicle page's six remaining subsystem sub-tabs. The real tab bar (VehicleTabBar) is
        // All · Crew · Prop · Mech · Power · Avionics · GNC · Thermal — All=Vehicle and Mech=VehicleMech
        // already exist; these are the other six. Appended (never renumber): the int persists per screen.
        VehicleCrew = 20, VehiclePropulsion = 21, VehiclePower = 22,
        VehicleAvionics = 23, VehicleGnc = 24, VehicleThermal = 25,
        // Two real deorbit/prox-ops pages reconstructed from photos + the live docking sim. Manual Chute
        // Deploy is the Cover rail's phase-7 destination; Docking is reached from the attitude HUD.
        ManualChute = 26, Docking = 27,
        // The rendezvous orbital-ellipse plot (T6) - the BBC-photographed CENTRE cockpit screen during
        // a real "Hold Capture" rendezvous (SCREEN_INVENTORY #23/#87, tier-1). Reached from Docking's
        // own letterbox margin, the same construction as the HUD's Docking affordance. Appended (never
        // renumbered): the int persists per screen.
        Rendezvous = 28,
        // Deorbit Burn Prep (T7) - SCREEN_INVENTORY #24, reconstructed from a blurry photo frame (no
        // Figma/demo ref, same footing as VrioTest). Its natural nav entry point (a phase-rail
        // "Procedure" tap) is wired in the touch pass (T14, see FigmaUI's VrioTest comment); for now
        // reached only via the Menu grid. Appended (never renumbered): the int persists per screen.
        DeorbitBurnPrep = 29,
        // Entry (T8) - SCREEN_INVENTORY #25, reconstructed from a PARTIAL photo frame (only a
        // "Parachute Deployment Altitude" section title legible - thinner evidence than DeorbitBurnPrep).
        // NOT the same thing as UiPage.Entry (14) above, whose Titles entry is "ENTRY GO / NO-GO" - a
        // leftover phase-rail ACTION-item int from the old numbering (S14), unrelated to this standalone
        // screen despite the name collision; that is why this gets its own value rather than reusing it.
        // Same reachability footing as DeorbitBurnPrep: Menu grid only for now, T14 wires a real entry
        // point. Appended (never renumbered): the int persists per screen.
        EntryProcedure = 30,
        // The two Vehicle systems DEEP-VIEWS (T9) - SCREEN_INVENTORY #27 + the "Vehicle systems P&ID
        // schematic" entry, both real screens whose layout grammar is photographed but whose text is
        // not transcribable. Deliberately NOT extra VehicleTabBar tabs: that strip's eight tabs are
        // confirmed-real from the clean designer mockup, so a ninth would be editing a real-sourced
        // label set (C1.4). S27 (owner decision (b), 2026-09-02): rather than assign a Cover rail
        // "Procedure" slot to either (no source names what belongs there - a §1.4 tier-3 invention,
        // C1.4/C1.12), both are reachable from every Vehicle-family page via VehicleDeepViewLinks, our
        // own geometry, marked as ours - same footing as DeorbitBurnPrep/EntryProcedure's Menu-grid
        // reachability, plus this one extra path. Appended (never renumbered).
        SystemsTree = 31, SystemsPid = 32,
        // Ascent / Launch (T12) - SCREEN_INVENTORY #14, the one screen with NO public in-cabin frame at
        // all (confirmed absent, not just unfound) - "DATA-BUILDABLE, layout reconstructed + MARKED",
        // one step past DeorbitBurnPrep/EntryProcedure (those had a blurry/partial photo; this has none,
        // so the whole page chrome is ours, not just the spacing). Menu grid only for now; a real entry
        // point is T14's job, same footing as the other reconstructed pages. Appended (never renumbered).
        Ascent = 33,
        // The circular nav/orbit plot (S15) - SCREEN_INVENTORY #28, the third of §11b's three
        // newly-characterised JSC screens (T9 built the other two). Same footing as Ascent: Menu grid
        // only for now, a real entry point is T14's job. Appended (never renumbered): the int persists
        // per screen.
        NavOrbitPlot = 34
    }

    public enum NavAct { None, Goto, Back, Forward }

    /// <summary>What a touch on a page resolved to: go to a page, or step the history.</summary>
    public struct NavHit
    {
        public NavAct Act;
        public UiPage Target;
        public static NavHit None { get { NavHit n; n.Act = NavAct.None; n.Target = UiPage.Cover; return n; } }
        public static NavHit Go(UiPage p) { NavHit n; n.Act = NavAct.Goto; n.Target = p; return n; }
        public static NavHit BackStep { get { NavHit n; n.Act = NavAct.Back; n.Target = UiPage.Cover; return n; } }
        public static NavHit ForwardStep { get { NavHit n; n.Act = NavAct.Forward; n.Target = UiPage.Cover; return n; } }
    }

    public static class FigmaUI
    {
        /// <summary>Worst-case commands any page here emits - the Cover's MAP camera view is now the
        /// heaviest (CoverPage.Commands, its ground track a command per segment) - plus the back-chevron
        /// overlay. The painter sizes its list to the max of this and the old model.</summary>
        public const int Commands = 360;

        public const int PageCount = 35;

        const float RefW = 3427f, RefH = 2112f;

        // ---- component_48 BOTTOM-BAR NAVIGATION ----
        // The Figma has only five real page designs; the persistent page-to-page nav is the bottom bar
        // (component_48), present on every page — the new equivalent of the old ChromeBar tab strip.
        // Its four icons are baked into component_48.png at these design x's (each 80 wide, at design
        // y 2003 = the bar top 1877 + the icon's local y 126). The bar is drawn full-width (stretched),
        // so a touch maps design-x across the whole screen width and design-y by the height scale.
        static readonly float[] BarIconX = { 46f, 174f, 302f, 430f, 558f };
        const float BarIconY = 2003f, BarIconS = 80f;
        // Left-to-right: compass, target, rocket, folder, gear. The mapping is taken from the reference
        // UI's live demo (github: neel-dandiwala/SpaceX-Dragon2-UI), whose bottom bar routes icon N to
        // panel N: Cover, attitude HUD, vehicle overview, suit leak check, settings(audio). Vehicle and
        // SuitCheck are placeholders until designed.
        static readonly UiPage[] BarTarget =
            { UiPage.Cover, UiPage.Hud, UiPage.Vehicle, UiPage.SuitCheck, UiPage.Audio };

        /// <summary>Which bottom-bar icon (0..3) a touch hit, or -1. Present on every page.</summary>
        public static int BottomBarHit(float px, float py, int w, int h)
        {
            float sc = h / RefH;
            float y0 = BarIconY * sc, y1 = (BarIconY + BarIconS) * sc;
            if (py < y0 - 12f || py > y1 + 12f) return -1;
            for (int i = 0; i < BarIconX.Length; i++)
            {
                // Padding kept under half the ~48px icon pitch so neighbours never share a hit region.
                float x0 = BarIconX[i] / RefW * w, x1 = (BarIconX[i] + BarIconS) / RefW * w;
                if (px >= x0 - 6f && px < x1 + 6f) return i;
            }
            return -1;
        }

        static readonly string[] Titles = {
            "COVER", "ATTITUDE HUD", "AUDIO SETTINGS", "PROCEDURE", "CABIN",
            "MENU", "DEORBIT BURN", "COAST TO TRUNK JETTISON", "CLAW SEPARATION", "MANUAL CHUTE",
            "ON SPACEX — GO", "DEORBIT BURN BRIEF", "REVIEW REFERENCE", "ACKNOWLEDGE", "ENTRY GO / NO-GO",
            "VEHICLE OVERVIEW", "SUIT LEAK CHECK", "MECH PANEL", "VIDEO SETTINGS", "TEST VRIO HEALTH LEDS",
            "VEHICLE — CREW", "VEHICLE — PROP", "VEHICLE — POWER",
            "VEHICLE — AVIONICS", "VEHICLE — GNC", "VEHICLE — THERMAL",
            "MANUAL CHUTE DEPLOY", "MANUAL DOCKING", "RENDEZVOUS", "DEORBIT BURN PREP", "ENTRY",
            "SYSTEMS TREE", "SYSTEMS P&ID", "ASCENT / LAUNCH", "NAV / ORBIT PLOT"
        };

        public static string Name(UiPage p)
        {
            int i = (int)p;
            return (i >= 0 && i < Titles.Length) ? Titles[i] : "?";
        }

        /// <summary>Does the page's centre swap to the docking camera? (The HUD, when the nose is open.)
        /// The painter uses this to claim the camera before drawing, as the old DOCKING page did.</summary>
        public static bool WantsDockingCam(UiPage p, PageState s) { return p == UiPage.Hud && s.Steps.NoseConeOpen; }

        public static void Build(DisplayList dl, UiPage page, int w, int h, PageState s, MapView view)
        { Build(dl, page, w, h, s, view, 5, false, 1, CoverPage.CoverCam.Earth); }

        public static void Build(DisplayList dl, UiPage page, int w, int h, PageState s, MapView view,
                                 int suitCountdown, bool suitPopup, int coverPhase)
        { Build(dl, page, w, h, s, view, suitCountdown, suitPopup, coverPhase, CoverPage.CoverCam.Earth); }

        public static void Build(DisplayList dl, UiPage page, int w, int h, PageState s, MapView view,
                                 int suitCountdown, bool suitPopup, int coverPhase,
                                 CoverPage.CoverCam coverCam)
        { Build(dl, page, w, h, s, view, suitCountdown, suitPopup, coverPhase, coverCam, Turntable.Front()); }

        public static void Build(DisplayList dl, UiPage page, int w, int h, PageState s, MapView view,
                                 int suitCountdown, bool suitPopup, int coverPhase,
                                 CoverPage.CoverCam coverCam, TurntableState turn)
        { Build(dl, page, w, h, s, view, suitCountdown, suitPopup, coverPhase, coverCam, turn, PageControls.Default); }

        public static void Build(DisplayList dl, UiPage page, int w, int h, PageState s, MapView view,
                                 int suitCountdown, bool suitPopup, int coverPhase,
                                 CoverPage.CoverCam coverCam, TurntableState turn, PageControls ctl)
        { Build(dl, page, w, h, s, view, suitCountdown, suitPopup, coverPhase, coverCam, turn, ctl, 0u); }

        /// <summary>As Build, plus the page state the painter owns: the Suit Leak Check's
        /// countdown/popup, the Cover's selected deorbit phase, the Cover's camera view (T4 - which
        /// of First.vue's Earth / Map / Capsule views its right-hand slot is showing), where the
        /// capsule TURNTABLE is pointing (T11b - the drag the painter accumulates), the controls a
        /// touch flips (T14 - see PageControls), and the Suit Leak Check's RUN SEED (S31 - the painter
        /// mints one per run; 0 means no run has been made, so nothing has been found). Every other
        /// page ignores them.</summary>
        public static void Build(DisplayList dl, UiPage page, int w, int h, PageState s, MapView view,
                                 int suitCountdown, bool suitPopup, int coverPhase,
                                 CoverPage.CoverCam coverCam, TurntableState turn, PageControls ctl,
                                 uint suitSeed)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            switch (page)
            {
                case UiPage.Cover:     CoverPage.Build(dl, w, h, s, view, coverPhase, coverCam, turn); break;
                case UiPage.Menu:      MenuPage.Build(dl, w, h); break;
                case UiPage.Hud:       Frame58Hud.Build(dl, w, h, s); break;
                case UiPage.Audio:     SettingsAudioPage.Build(dl, w, h, 2); break;
                case UiPage.Procedure: FigmaFramePage.Build(dl, w, h, "frame59"); break;
                case UiPage.Cabin:     FigmaFramePage.Build(dl, w, h, "frame66"); break;
                // S31: the page's suit model is assembled HERE, from the same PageState every other
                // page reads, so the one thing the painter has to own is the run seed. suitPopup is
                // also "the run produced a result", which is what decides whether a leaking suit has
                // finished bleeding down - see SuitLeak.Compute.
                case UiPage.SuitCheck: SuitCheckPage.Build(dl, w, h, suitCountdown, suitPopup,
                                                           SuitLeak.From(s, suitCountdown, suitPopup, suitSeed)); break;
                case UiPage.Vehicle:   VehicleOverviewPage.Build(dl, w, h, s); break;
                case UiPage.VehicleMech: VehicleMechPage.Build(dl, w, h, s); break;
                case UiPage.AudioVideo:  SettingsVideoPage.Build(dl, w, h, s); break;
                case UiPage.VrioTest:    VrioTestPage.Build(dl, w, h); break;
                case UiPage.VehicleCrew:       VehicleSubsystemPage.Build(dl, w, h, VehicleSubsystemPage.Sub.Crew, s, ctl.Alerts); break;
                case UiPage.VehiclePropulsion: VehicleSubsystemPage.Build(dl, w, h, VehicleSubsystemPage.Sub.Propulsion, s, ctl.Alerts); break;
                case UiPage.VehiclePower:      VehicleSubsystemPage.Build(dl, w, h, VehicleSubsystemPage.Sub.Power, s, ctl.Alerts); break;
                case UiPage.VehicleAvionics:   VehicleSubsystemPage.Build(dl, w, h, VehicleSubsystemPage.Sub.Avionics, s, ctl.Alerts); break;
                case UiPage.VehicleGnc:        VehicleSubsystemPage.Build(dl, w, h, VehicleSubsystemPage.Sub.Gnc, s, ctl.Alerts); break;
                case UiPage.VehicleThermal:    VehicleSubsystemPage.Build(dl, w, h, VehicleSubsystemPage.Sub.Thermal, s, ctl.Alerts); break;
                case UiPage.ManualChute:       ManualChuteDeployPage.Build(dl, w, h, s, view); break;
                case UiPage.Docking:           DockingSimPage.Build(dl, w, h, s, ctl); break;
                case UiPage.Rendezvous:        RendezvousPage.Build(dl, w, h, s); break;
                case UiPage.DeorbitBurnPrep:   DeorbitBurnPrepPage.Build(dl, w, h, s); break;
                case UiPage.EntryProcedure:    EntryPage.Build(dl, w, h); break;
                case UiPage.SystemsTree:       SystemsTreePage.Build(dl, w, h, s); break;
                case UiPage.SystemsPid:        SystemsPidPage.Build(dl, w, h, s); break;
                case UiPage.Ascent:            AscentPage.Build(dl, w, h, s); break;
                case UiPage.NavOrbitPlot:      NavOrbitPlotPage.Build(dl, w, h, s); break;
                default:               PlaceholderPage.Build(dl, w, h, Name(page)); break;
            }
            BottomBarMarker(dl, w, h, page);
        }

        /// <summary>True if this page has no real case in Build's switch above, so a visit draws the
        /// honest PlaceholderPage card instead of real content. This is the ONE place that decides
        /// that — MenuPage reads it to leave such a page off the grid (S14: the enum values are kept,
        /// never deleted/renumbered per UiPage's own comment, they just don't get a card until a real
        /// Build case lands), and FigmaUINavTest cross-checks it against what Build actually draws so
        /// the two can never quietly drift apart. Mirror any change to the switch above here too.</summary>
        public static bool IsPlaceholder(UiPage page)
        {
            switch (page)
            {
                case UiPage.Cover: case UiPage.Menu: case UiPage.Hud: case UiPage.Audio:
                case UiPage.Procedure: case UiPage.Cabin: case UiPage.SuitCheck: case UiPage.Vehicle:
                case UiPage.VehicleMech: case UiPage.AudioVideo: case UiPage.VrioTest:
                case UiPage.VehicleCrew: case UiPage.VehiclePropulsion: case UiPage.VehiclePower:
                case UiPage.VehicleAvionics: case UiPage.VehicleGnc: case UiPage.VehicleThermal:
                case UiPage.ManualChute: case UiPage.Docking: case UiPage.Rendezvous:
                case UiPage.DeorbitBurnPrep: case UiPage.EntryProcedure:
                case UiPage.SystemsTree: case UiPage.SystemsPid: case UiPage.Ascent:
                case UiPage.NavOrbitPlot:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>Which bottom-bar icon (0..4) is "active" for this page — its own icon, or its parent's
        /// (the cover's phases/actions map back to the cover icon, Mech to Vehicle, etc.).</summary>
        static int ActiveBarIcon(UiPage p)
        {
            switch (p)
            {
                case UiPage.Hud: case UiPage.Docking: case UiPage.Rendezvous: return 1;
                case UiPage.Vehicle: case UiPage.VehicleMech:
                case UiPage.VehicleCrew: case UiPage.VehiclePropulsion: case UiPage.VehiclePower:
                case UiPage.VehicleAvionics: case UiPage.VehicleGnc: case UiPage.VehicleThermal:
                // The two systems deep-views are vehicle pages by subject even though they carry no
                // subsystem tab bar (see the UiPage comment), so the bar marker names their parent.
                case UiPage.SystemsTree: case UiPage.SystemsPid:
                    return 2;
                case UiPage.SuitCheck: return 3;
                case UiPage.Audio: case UiPage.Cabin: case UiPage.AudioVideo: return 4;
                default: return 0;   // Cover + everything reached from it (menu, phases, actions, procedure)
            }
        }

        // The marker was baked under the first icon in component_48.png; it has been erased there so it
        // can be drawn dynamically. These are the erased block's component_48 coords (bar is 235 tall,
        // sitting at design y1877): a thin white line just above the bar's bottom edge.
        const float MarkY = 1877f + 223f, MarkH = 10f, MarkW = 108f;

        /// <summary>Slide the bottom bar's white marker under the active tab (App.vue's `.marker`).</summary>
        static void BottomBarMarker(DisplayList dl, int w, int h, UiPage page)
        {
            float sc = h / RefH, mw = MarkW / RefW * w;
            float cx = (BarIconX[ActiveBarIcon(page)] + BarIconS * 0.5f) / RefW * w;
            dl.Rect(cx - mw * 0.5f, MarkY * sc, mw, MarkH * sc, DragonPalette.White);
        }

        public static NavHit HitTest(UiPage page, float px, float py, int w, int h)
        {
            // Bottom-bar nav is drawn over every page, so it is tested first — the same "chrome first"
            // rule the old ChromeBar followed, so a page control overlapping the bar cannot eat the one
            // touch the crew can always rely on.
            int bar = BottomBarHit(px, py, w, h);
            if (bar >= 0) return NavHit.Go(BarTarget[bar]);

            // Vehicle page's eight subsystem sub-tabs (All·Crew·Prop·Mech·Power·Avionics·GNC·Thermal)
            // switch between the sibling vehicle pages. The strip geometry lives in VehicleTabBar so the
            // drawn tabs and the hit regions never drift.
            if (IsVehiclePage(page))
            {
                int t = VehicleTabBar.HitTest(px, py, w, h);
                if (t >= 0) return NavHit.Go(VehicleTab[t]);

                // S27: VehicleDeepViewLinks (SYSTEMS TREE / SYSTEMS P&ID) - our own affordance, reachable
                // from every Vehicle-family page, since no source assigns either a real Cover rail slot.
                int link = VehicleDeepViewLinks.HitTest(px, py, w, h);
                if (link >= 0) return NavHit.Go(VehicleDeepViewLinks.Target[link]);
            }

            // Settings Audio/Cabin/Video sub-tabs switch between the three sibling pages.
            if (page == UiPage.Audio || page == UiPage.Cabin || page == UiPage.AudioVideo)
            {
                float dx = px * RefW / w, dy = py * RefH / h;
                if (dy >= 1890f && dy < 2000f)
                {
                    if (dx >= 1520f && dx < 1650f) return NavHit.Go(UiPage.Audio);
                    if (dx >= 1652f && dx < 1780f) return NavHit.Go(UiPage.Cabin);
                    if (dx >= 1782f && dx < 1910f) return NavHit.Go(UiPage.AudioVideo);
                }
            }

            // Attitude HUD: a "MANUAL DOCKING" affordance sits in the letterbox margin (screen-space, so
            // it can never overlap the fit-to-height frame art) and opens the manual docking screen.
            if (page == UiPage.Hud)
            {
                float sc = h / RefH, ox = (w - RefW * sc) * 0.5f;
                if (ox > 40f && px >= 12f && px < ox - 12f && py >= h * 0.40f && py < h * 0.60f)
                    return NavHit.Go(UiPage.Docking);
            }

            // Menu is a grid of every other page; a hit on a card jumps straight there. A tap in the
            // gaps between cards (or off-grid) is inert, same as everywhere else.
            if (page == UiPage.Menu)
            {
                int mi = MenuPage.HitTest(px, py, w, h);
                return (mi >= 0) ? NavHit.Go(MenuPage.Entries[mi]) : NavHit.None;
            }

            // Manual Chute Deploy shares the Cover's rail; tapping any other phase returns to the Cover.
            if (page == UiPage.ManualChute)
            {
                int ph = CoverPage.PhaseOf(CoverPage.HitTest(px, py, w, h));
                if (ph >= 0 && ph != 6) return NavHit.Go(UiPage.Cover);
                return NavHit.None;
            }

            // Manual Docking: a "RENDEZVOUS" affordance in the matching letterbox margin opens the
            // rendezvous ellipse plot - the two are the HUD/plot pairing the BBC photo actually shows
            // together during a real approach, same construction as the HUD's own Docking affordance.
            if (page == UiPage.Docking)
            {
                float sc = h / RefH, ox = (w - RefW * sc) * 0.5f;
                if (ox > 40f && px >= 12f && px < ox - 12f && py >= h * 0.40f && py < h * 0.60f)
                    return NavHit.Go(UiPage.Rendezvous);
                // T14: the page's own "Settings" control is navigation, so it resolves HERE rather than
                // in the painter's page switch — and it goes where the Cover's Settings button already
                // goes, because there is one settings destination in this UI and inventing a second
                // would be the drift. Its two neighbours are not navigation and are left to the page.
                if (DockingSimPage.HitTest(px, py, w, h) == DockingSimPage.DockAct.Settings)
                    return NavHit.Go(UiPage.Audio);
            }

            // No selected phase is passed, and that is correct rather than an oversight (S54): `MapCover`
            // resolves ONLY Menu and Settings, neither of which the Reference Content phase suppresses, and
            // this path dispatches no Act*/Entry action at all. The painter is the caller that can, and it
            // passes the real phase into the six-argument overload.
            if (page == UiPage.Cover) return MapCover(CoverPage.HitTest(px, py, w, h));
            return NavHit.None;
        }

        // ---- Cover hub NAVIGATION: only Settings + Menu leave the page. The phase rail and the ◄/►
        // arrows select the deorbit phase IN-PAGE (the painter handles those via CoverPage.HitTest), and
        // the action/entry rows are display-only, so they return None here. ----
        static NavHit MapCover(CoverPage.CoverButton b)
        {
            switch (b)
            {
                case CoverPage.CoverButton.Menu:     return NavHit.Go(UiPage.Menu);
                case CoverPage.CoverButton.Settings: return NavHit.Go(UiPage.Audio);
                // The Manual Chute rail item is a real page (the others select a phase in-page).
                case CoverPage.CoverButton.PhaseManual: return NavHit.Go(UiPage.ManualChute);
                default:                             return NavHit.None;
            }
        }

        // Tab index (VehicleTabBar order) → the sibling vehicle page it opens. Must stay in lockstep
        // with VehicleTabBar.Tabs: All · Crew · Prop · Mech · Power · Avionics · GNC · Thermal.
        static readonly UiPage[] VehicleTab = {
            UiPage.Vehicle, UiPage.VehicleCrew, UiPage.VehiclePropulsion, UiPage.VehicleMech,
            UiPage.VehiclePower, UiPage.VehicleAvionics, UiPage.VehicleGnc, UiPage.VehicleThermal };

        /// <summary>Is this one of the eight Vehicle-page sub-tabs (which share the subsystem tab bar)?</summary>
        static bool IsVehiclePage(UiPage p)
        {
            switch (p)
            {
                case UiPage.Vehicle: case UiPage.VehicleMech:
                case UiPage.VehicleCrew: case UiPage.VehiclePropulsion: case UiPage.VehiclePower:
                case UiPage.VehicleAvionics: case UiPage.VehicleGnc: case UiPage.VehicleThermal:
                    return true;
                default: return false;
            }
        }

    }
}
