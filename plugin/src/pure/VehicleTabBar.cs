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

        /// <summary>Draw the strip with tab <paramref name="active"/> lit + underlined.</summary>
        public static void Draw(DisplayList dl, int w, int h, int active)
        {
            float sx = w / RefW, sy = h / RefH;
            for (int i = 0; i < Tabs.Length; i++)
            {
                float cx = CentreX(i);
                bool on = (i == active);
                dl.Text(Tabs[i], cx * sx, LabelY * sy, LabelSize * sy, TextAlign.Centre,
                        on ? DragonPalette.White : DragonPalette.Text6);
                if (on)
                    dl.Rect((cx - MarkW * 0.5f) * sx, MarkY * sy, MarkW * sx, MarkH * sy, DragonPalette.Accent);
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
