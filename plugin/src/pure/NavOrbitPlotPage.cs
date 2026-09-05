// DragonScreen — NavOrbitPlotPage  (PURE: the circular nav/orbit plot, S15)
// ============================================================================================
// SCREEN_INVENTORY.md #28 / BUILD_PLAN.md §11b, the third of §11b's three newly-characterised
// screens (the other two are T9's systems tree + P&ID): JSC `jsc2024e064449`'s RIGHT screen (sim
// rig) — concentric rings, coloured target markers (yellow + cyan), orbit arcs and a g/rate
// readout — corroborated by the BBC `_112570366_touchscreens.png` frame, the SAME real flight
// screengrab T6's rendezvous ellipse plot is built from (§3: "pairs with the Rendezvous
// ellipse"). Layout-real / labels-reconstructed + MARKED, the same footing as T6/T9/T12 — the
// JSC shots are upward-at-crew and not label-legible at any resolution found, so exact on-screen
// text was never transcribable (§11b's own verdict).
//
// ---- WHAT IS REAL, WHAT IS OURS (§1.4) ----
// The orbit arc is NOT a second orbit renderer: it calls NavPage.Orbit, the SAME real conic
// (apogee/perigee -> ellipse, current radius -> true anomaly, target phase -> approach chord)
// T6's rendezvous ellipse already trusts — one orbit calculation in the codebase, not two that
// could drift. The AP/PE markers, the vehicle cross and the target diamond it draws are its own
// real markers, unchanged here.
// The g/rate readout reuses PageState.GForceText/.GForce01 (real — VesselData's g-meter, the SAME
// field the Vehicle Overview's G-FORCE dial reads) and .RateText/.RangeText/.TargetName (real —
// the SAME docking-approach fields the Rendezvous plot's chord and the Docking page already
// read). No new PageState field was added for this page.
// OURS, and stated as such: the concentric range rings (ring count and spacing — no scale is
// legible in either source, so none is printed, the same "shape confirmed, artwork not invented"
// call T6 made for its mission-patch roundel) and the small colour-key chips ("VEHICLE" cyan /
// target-name yellow) — the JSC frame shows two coloured markers but not their exact glyphs, so
// the key names the colour convention instead of inventing an unreadable icon shape.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class NavOrbitPlotPage
    {
        // background + title + 4 concentric rings + NavPage.Orbit's own budget (RendezvousPage.
        // Commands=320 already covers a full Orbit call including its live Globe(), matched here
        // since this page's plot well is the same order of size) + colour key (2 dots + 2 labels)
        // + 3 readout rows (label+value each) + bottom bar.
        public const int Commands = 340;

        const float RefW = 3427f, RefH = 2112f;
        const float PlotX = 380f, PlotY = 180f, PlotW = 2667f, PlotH = 1670f;

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f; if (ox < 0f) ox = 0f;
            float X(float x) => x * sc + ox;
            float Y(float y) => y * sc;
            float Z(float v) => v * sc;
            int St(float rs) { int p = (int)Math.Round(rs * sc); return p < 1 ? 1 : p; }

            dl.Rect(0, 0, w, h, DragonPalette.Background);
            dl.Text("NAV / ORBIT PLOT", w * 0.5f, Y(60), Z(44), TextAlign.Centre, DragonPalette.Accent);

            // ---- the plot well ----
            dl.Rect(X(PlotX), Y(PlotY), Z(PlotW), Z(PlotH), DragonPalette.Inset2);

            // ---- concentric range rings (ours — no scale is legible in either source, §1.4) ----
            float rcx = X(PlotX + PlotW * 0.5f), rcy = Y(PlotY + PlotH * 0.5f);
            float rmax = Z(Math.Min(PlotW, PlotH)) * 0.46f;
            for (int i = 1; i <= 4; i++)
            {
                float r = rmax * i / 4f;
                dl.ArcBand(rcx, rcy, r - St(2), r, 0.0, 360.0, DragonPalette.Hairline);
            }

            // ---- the real orbit conic (T6's calculation): body, dotted ellipse, AP/PE, our
            // vehicle marker, and — with a target set — the approach-chord diamond ----
            NavPage.Orbit(dl, s, X(PlotX), Y(PlotY), Z(PlotW), Z(PlotH), true);

            dl.Box(X(PlotX), Y(PlotY), Z(PlotW), Z(PlotH), St(2), DragonPalette.Hairline);

            // ---- colour key (ours): the reference shows two coloured markers, not their exact
            // glyphs, so this names the colour convention rather than inventing an icon shape ----
            float keyX = PlotX + 30f, keyY = PlotY + 26f;
            dl.ArcBand(X(keyX + 10f), Y(keyY + 10f), 0, Z(9), 0, 360, DragonPalette.Accent);
            dl.Text("VEHICLE", X(keyX + 32f), Y(keyY + 2f), Z(26), TextAlign.Left, DragonPalette.Text5);
            float key2Y = keyY + 44f;
            dl.ArcBand(X(keyX + 10f), Y(key2Y + 10f), 0, Z(9), 0, 360, DragonPalette.Caution);
            dl.Text(s.Valid && s.HasTarget ? (s.TargetName ?? "TARGET") : "NO TARGET",
                    X(keyX + 32f), Y(key2Y + 2f), Z(26), TextAlign.Left, DragonPalette.Text5);

            // ---- g / rate readout: real PageState fields, the same ones the Vehicle Overview's
            // G-FORCE dial and the Docking/Rendezvous approach readouts already read ----
            // ---- S39 (finishing S38's sweep): THE VALUE COLUMN SITS BESIDE ITS LABEL ----
            // These three rows were the widest REACHABLE stacked label-value block left after S38:
            // 580 design units of empty space at a 26-unit type size (22.3x), on a row pitch of only
            // 44 - a span-to-pitch ratio of 13, the second-tightest in the build after the
            // DeorbitBurnPrep block S38 could not cure by distance. The console is a tilted quad in
            // IVA, so a horizontal row is a SLOPING line to the crew and a wide gap lifts the value
            // column toward its neighbour's line; three stacked rows with nothing joining them across
            // the gap is exactly the arrangement that misread on the glass.
            // The remedy is the owner's own choice from S38 - move the value column IN - not a new
            // mechanism. Values stay RIGHT-aligned so the digits still line up and the column is still
            // scannable; the span goes 580 -> 280, which is well under the 44-unit row pitch's reach.
            // ⛔ No PNG check can find this class (the preview renders the panel flat and square-on),
            // so the guard is LayoutTest's headless span assertion, not the preview.
            const float ValueSpan = 280f;
            float rowX0 = PlotX + PlotW - 40f, rowLabelX = rowX0 - ValueSpan, rowY = PlotY + 26f;
            void Row(string label, string value, float ry)
            {
                dl.Text(label, X(rowLabelX), Y(ry), Z(26), TextAlign.Left, DragonPalette.Text6);
                dl.Text(value, X(rowX0), Y(ry), Z(26), TextAlign.Right, DragonPalette.Text2);
            }
            Row("G-FORCE", s.Valid && s.GForceText != null ? s.GForceText + " g" : "-", rowY);
            Row("RATE", s.Valid && s.HasTarget ? s.RateText : "-", rowY + 44f);
            Row("RANGE", s.Valid && s.HasTarget ? s.RangeText : "-", rowY + 88f);

            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame
        }
    }
}
