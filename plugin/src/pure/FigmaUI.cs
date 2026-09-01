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
        DeorbitBurnPrep = 29
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

        public const int PageCount = 30;

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
            "MANUAL CHUTE DEPLOY", "MANUAL DOCKING", "RENDEZVOUS", "DEORBIT BURN PREP"
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

        /// <summary>As Build, plus the three bits of page state the painter owns: the Suit Leak Check's
        /// countdown/popup, the Cover's selected deorbit phase, and the Cover's camera view (T4 - which
        /// of First.vue's Earth / Map / Capsule views its right-hand slot is showing). Every other page
        /// ignores them.</summary>
        public static void Build(DisplayList dl, UiPage page, int w, int h, PageState s, MapView view,
                                 int suitCountdown, bool suitPopup, int coverPhase,
                                 CoverPage.CoverCam coverCam)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            switch (page)
            {
                case UiPage.Cover:     CoverPage.Build(dl, w, h, s, view, coverPhase, coverCam); break;
                case UiPage.Menu:      MenuPage.Build(dl, w, h); break;
                case UiPage.Hud:       Frame58Hud.Build(dl, w, h, s); break;
                case UiPage.Audio:     SettingsAudioPage.Build(dl, w, h, 2); break;
                case UiPage.Procedure: FigmaFramePage.Build(dl, w, h, "frame59"); break;
                case UiPage.Cabin:     FigmaFramePage.Build(dl, w, h, "frame66"); break;
                case UiPage.SuitCheck: SuitCheckPage.Build(dl, w, h, suitCountdown, suitPopup); break;
                case UiPage.Vehicle:   VehicleOverviewPage.Build(dl, w, h, s); break;
                case UiPage.VehicleMech: VehicleMechPage.Build(dl, w, h); break;
                case UiPage.AudioVideo:  SettingsVideoPage.Build(dl, w, h, s); break;
                case UiPage.VrioTest:    VrioTestPage.Build(dl, w, h); break;
                case UiPage.VehicleCrew:       VehicleSubsystemPage.Build(dl, w, h, VehicleSubsystemPage.Sub.Crew, s); break;
                case UiPage.VehiclePropulsion: VehicleSubsystemPage.Build(dl, w, h, VehicleSubsystemPage.Sub.Propulsion, s); break;
                case UiPage.VehiclePower:      VehicleSubsystemPage.Build(dl, w, h, VehicleSubsystemPage.Sub.Power, s); break;
                case UiPage.VehicleAvionics:   VehicleSubsystemPage.Build(dl, w, h, VehicleSubsystemPage.Sub.Avionics, s); break;
                case UiPage.VehicleGnc:        VehicleSubsystemPage.Build(dl, w, h, VehicleSubsystemPage.Sub.Gnc, s); break;
                case UiPage.VehicleThermal:    VehicleSubsystemPage.Build(dl, w, h, VehicleSubsystemPage.Sub.Thermal, s); break;
                case UiPage.ManualChute:       ManualChuteDeployPage.Build(dl, w, h, s, view); break;
                case UiPage.Docking:           DockingSimPage.Build(dl, w, h); break;
                case UiPage.Rendezvous:        RendezvousPage.Build(dl, w, h, s); break;
                case UiPage.DeorbitBurnPrep:   DeorbitBurnPrepPage.Build(dl, w, h, s); break;
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
                case UiPage.DeorbitBurnPrep:
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
            }

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
