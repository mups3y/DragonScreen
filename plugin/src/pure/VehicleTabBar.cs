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
        public const int Commands = 24;   // 8 labels + 1 underline (+ headroom)
        const float RefW = 3427f, RefH = 2112f;

        /// <summary>The eight subsystem tabs, in order. Index is the "active tab" the pages pass in
        /// and the value FigmaUI maps to a UiPage — never reorder.</summary>
        public static readonly string[] Tabs =
            { "All", "Crew", "Prop", "Mech", "Power", "Avionics", "GNC", "Thermal" };

        // Row centred on the screen (design centre x = 1713.5), sitting just above the bottom bar
        // (component_48 starts at design y1877). 8 slots at this pitch span x≈996..2431.
        const float Pitch = 205f, LabelY = 1812f, LabelSize = 28f;
        const float MarkY = 1858f, MarkW = 140f, MarkH = 6f;
        static float Start { get { return 1713.5f - Pitch * (Tabs.Length - 1) * 0.5f; } }

        /// <summary>Design-x of tab i's centre.</summary>
        public static float CentreX(int i) { return Start + i * Pitch; }

        /// <summary>T5: the real Crew Dragon "subview nav bar ... turns red when that subview holds an
        /// alert" (REAL_DRAGON_SCREENS.md §2) computed per tab from the SAME live signals the rest of the
        /// screens already use — Alarms.LifeSupport/Thermal on the cabin, Alarms.Low on propellant/power,
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
            Severity prop = Alarms.Low(s.Propellant01);
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
            for (int i = 0; i < Tabs.Length; i++)
            {
                float cx = CentreX(i);
                bool on = (i == active);
                Severity sev = (tabSeverity != null && i < tabSeverity.Length) ? tabSeverity[i] : Severity.Nominal;
                Rgba col = sev != Severity.Nominal ? Alarms.Colour(sev)
                         : on ? DragonPalette.White : DragonPalette.Text6;
                dl.Text(Tabs[i], cx * sx, LabelY * sy, LabelSize * sy, TextAlign.Centre, col);
                if (on)
                    dl.Rect((cx - MarkW * 0.5f) * sx, MarkY * sy, MarkW * sx, MarkH * sy,
                            sev != Severity.Nominal ? Alarms.Colour(sev) : DragonPalette.Accent);
            }
        }

        /// <summary>Which tab (0..7) a touch hit, or -1. Contiguous slots (half-pitch each side).</summary>
        public static int HitTest(float px, float py, int w, int h)
        {
            float dx = px * RefW / w, dy = py * RefH / h;
            if (dy < LabelY - 34f || dy > MarkY + 20f) return -1;
            for (int i = 0; i < Tabs.Length; i++)
            {
                float cx = CentreX(i);
                if (dx >= cx - Pitch * 0.5f && dx < cx + Pitch * 0.5f) return i;
            }
            return -1;
        }
    }
}
