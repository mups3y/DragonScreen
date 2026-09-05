// DragonScreen — RendezvousPage  (PURE: the rendezvous orbital-ellipse plot, T6)
// ============================================================================================
// The CENTRE cockpit screen during proximity ops, confirmed by a real flight screengrab: the BBC
// explainer's `_112570366_touchscreens.png` shows all three touchscreens during the "Hold Capture"
// phase of a real rendezvous (docs/SCREEN_INVENTORY.md #23/#87, tier-1 - a real photo, not a
// recreation). LEFT was the attitude/docking HUD (already built - Frame58Hud/DockingSimPage,
// which the same photo CONFIRMS); RIGHT was a scrolling checklist page (out of this task); CENTRE -
// this page - showed a left vertical ICON sub-nav rail, a "Hold Capture" procedure card (◄/► +
// RUNNING + status text + a circular mission-patch icon), and a large 2D ORBITAL-ELLIPSE plot with
// the vehicle position and an approach chord.
//
// ---- WHAT IS REAL, WHAT IS OURS (§1.4) ----
// The ellipse plot is NOT a second orbit renderer: it calls NavPage.Orbit, the same real conic math
// (apogee/perigee -> ellipse, current radius -> true anomaly) the plain NAV page already trusts, so
// there is one orbit calculation in the codebase, not two that could drift. The "approach chord" is
// the one addition NavPage.Orbit gained for this page - see there for what it draws and why.
// The rail icons are NOT label-legible in the reference photo (SCREEN_INVENTORY residual research),
// so they are drawn as the reference's own visual grammar (a vertical icon strip) with no invented
// destinations - inert chrome, like every other control this build cannot yet verify a real
// function for (§14.4b's inferred-panel precedent).
// The Hold Capture card's RUNNING/NOT ENGAGED state is the real StationApproach.Engaged/.Note the
// glue already threads through PageState (RendezvousEngaged/RendezvousNote) - "not engaged" until
// Part B wires it, the same honest stub every other command surface in this build reports.
// The circular "mission-patch" icon is drawn as a plain roundel: the SHAPE is confirmed by the
// photo; no specific patch artwork is invented to fill it.
// The ◄/► step controls are drawn but inert - display-only until T14 (touch wiring), the same
// footing as T5's ALERTS toggle and DockingSimPage's Instructions/Reset/Settings row.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class RendezvousPage
    {
        // rail (4 icons) + card (frame+title+patch+status+note+2 arrows) + plot label/well/box, plus
        // NavPage.Orbit's real cost - its Globe() is the same GlobeStrips=64 draw ManualChuteDeployPage
        // already budgets 320 for ("includes the live globe command load") - + the dotted ellipse
        // (72 steps) + apsides + vehicle + chord. Matched to that sibling's headroom.
        public const int Commands = 320;

        const float RefW = 3427f, RefH = 2112f;
        const float HCX = 1713f;

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f; if (ox < 0f) ox = 0f;
            float X(float x) => x * sc + ox;
            float Y(float y) => y * sc;
            float Z(float v) => v * sc;
            int St(float rs) { int p = (int)Math.Round(rs * sc); return p < 1 ? 1 : p; }

            dl.Rect(0, 0, w, h, DragonPalette.Background);
            dl.Text("RENDEZVOUS", X(HCX), Y(60), Z(44), TextAlign.Centre, DragonPalette.Accent);

            DrawRail(dl, X, Y, Z, St);
            DrawHoldCaptureCard(dl, X, Y, Z, St, s);

            // ---- the 2D orbital-ellipse plot: the real conic NavPage.Orbit already trusts ----
            const float PlotX = 1260f, PlotY = 220f, PlotW = 2127f, PlotH = 1580f;
            dl.Text("ORBITAL PLOT", X(PlotX), Y(PlotY - 40f), Z(28), TextAlign.Left, DragonPalette.Text6);
            dl.Rect(X(PlotX), Y(PlotY), Z(PlotW), Z(PlotH), DragonPalette.Inset2);
            NavPage.Orbit(dl, s, X(PlotX), Y(PlotY), Z(PlotW), Z(PlotH), true);
            dl.Box(X(PlotX), Y(PlotY), Z(PlotW), Z(PlotH), St(2), DragonPalette.Hairline);

            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame
        }

        // ---- LEFT icon sub-nav rail ----
        // The reference's "sidebar sub-nav" template (SCREEN_INVENTORY #79/#87), redrawn as
        // primitive slots. Chrome only: the real icons are not label-legible in the source photo,
        // so no destination is invented for a tap here (falls through to FigmaUI's default None).
        const int RailIcons = 4;
        const float RailX = 40f, RailW = 180f, RailTop = 220f, RailBottom = 1800f, RailBox = 140f;

        static void DrawRail(DisplayList dl, Func<float, float> X, Func<float, float> Y,
                             Func<float, float> Z, Func<float, int> St)
        {
            float pitch = (RailBottom - RailTop) / RailIcons;
            float bx = RailX + (RailW - RailBox) * 0.5f;
            for (int i = 0; i < RailIcons; i++)
            {
                float cy = RailTop + pitch * i + pitch * 0.5f;
                float by = cy - RailBox * 0.5f;
                dl.Rect(X(bx), Y(by), Z(RailBox), Z(RailBox), DragonPalette.Panel);
                dl.Box(X(bx), Y(by), Z(RailBox), Z(RailBox), St(2), DragonPalette.Hairline);
                dl.ArcBand(X(bx + RailBox * 0.5f), Y(cy), Z(18), Z(24), 0, 360, DragonPalette.Text6);
            }
        }

        // ---- "Hold Capture" procedure card ----
        const float CardX = 260f, CardY = 220f, CardW = 920f, CardH = 460f;

        static void DrawHoldCaptureCard(DisplayList dl, Func<float, float> X, Func<float, float> Y,
                                        Func<float, float> Z, Func<float, int> St, PageState s)
        {
            dl.Rect(X(CardX), Y(CardY), Z(CardW), Z(CardH), DragonPalette.Panel);
            dl.Box(X(CardX), Y(CardY), Z(CardW), Z(CardH), St(2), DragonPalette.Hairline);

            dl.Text("HOLD CAPTURE", X(CardX + 40f), Y(CardY + 60f), Z(40), TextAlign.Left, DragonPalette.Accent);

            // circular mission-patch icon (roundel) - shape confirmed by the photo, artwork not
            // invented (§1.4).
            float px = CardX + CardW - 110f, py = CardY + 90f;
            dl.ArcBand(X(px), Y(py), Z(52), Z(58), 0, 360, DragonPalette.Hairline);
            dl.ArcBand(X(px), Y(py), Z(30), Z(34), 0, 360, DragonPalette.Text7);

            // RUNNING / NOT ENGAGED - the real StationApproach state (PageState.RendezvousEngaged/
            // .RendezvousNote), the honest "not engaged" idiom every other command surface uses.
            bool running = s.RendezvousEngaged;
            string status = running
                ? ("RUNNING" + (string.IsNullOrEmpty(s.RendezvousNote) ? "" : "  " + s.RendezvousNote))
                : "NOT ENGAGED";
            dl.Text(status, X(CardX + CardW * 0.5f), Y(CardY + 190f), Z(46), TextAlign.Centre,
                    running ? DragonPalette.Go : DragonPalette.Text6);

            // ◄ / ► step controls - display-only for now (T14 wires touch), same footing as
            // DockingSimPage's Instructions/Reset/Settings row.
            const float ArrowW = 160f, ArrowH = 110f;
            float arrowY = CardY + CardH - 150f;
            dl.Box(X(CardX + 40f), Y(arrowY), Z(ArrowW), Z(ArrowH), St(2), DragonPalette.Hairline);
            dl.Text("◄", X(CardX + 40f + ArrowW * 0.5f), Y(arrowY + 34f), Z(40), TextAlign.Centre, DragonPalette.Text3);
            dl.Box(X(CardX + CardW - 40f - ArrowW), Y(arrowY), Z(ArrowW), Z(ArrowH), St(2), DragonPalette.Hairline);
            dl.Text("►", X(CardX + CardW - 40f - ArrowW * 0.5f), Y(arrowY + 34f), Z(40), TextAlign.Centre, DragonPalette.Text3);
        }
    }
}
