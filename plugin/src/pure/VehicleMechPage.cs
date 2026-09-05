// DragonScreen — VehicleMechPage  (PURE: the "MECH PANEL", the Vehicle page's Mech sub-tab)
// ============================================================================================
// The reference UI's Mech view (components/Mech.vue) is a radial schematic: a central ring holding the
// seat tachometers, ringed by the mechanical nodes (ACCELERATION / CENTRIPETAL / PRESSURE / RESISTANCE
// / WATER UPRIGHTING), each a donut with a big count. Reproduced here as spokes + donut nodes + the
// central SEATS block, with the Overview/Mech tab strip (Mech active).
//
// T13a (live-data wiring, §6). The five node values used to be the demo's own randomised ~7x,xxx —
// decoration, and the exact thing "never draw an invented number" exists to stop. Three of the five now
// read the acceleration terms VesselData has been computing FOR THIS PAGE all along and nothing was
// drawing (PageState.AccelPosText / AccelNegText / AccelCentText — the split the field's own comment
// calls "broken out the way the reference's MECH PANEL does"), plus the cabin pressure the same model
// feeds every other page:
//
//     ACCELERATION      along-axis acceleration, +g   REAL (vessel.acceleration on the control axis)
//     CENTRIPETAL       v² / r, g                     REAL (orbital speed + radius)
//     PRESSURE          cabin, psia                   SIMULATED from real state (CabinEnvironment)
//     RESISTANCE        along-axis DEceleration, g    REAL — drag/braking is the same term, negated
//     WATER UPRIGHTING  —                             NO SOURCE. The uprighting bags are not modelled
//                                                     by anything on the vessel, so the node dashes.
//
// The four SEAT n TACH rows dash for the same reason: a per-seat tachometer has no sensor behind it
// anywhere in this build. What IS live there is how many seats the capsule HAS — PageState.SeatCount,
// the capsule's own numbering — so the block draws that many rows and no more ("draw what exists, never
// a button bound to nothing", Pages.cs on LightCount), instead of four rows that are right by luck.
// §6 scopes this task to the VALUES, so the reference COPY — the node names, "SEAT n TACH", and the
// "ALL SYSTEMS CHECK / Awaiting" line under the seats — is reproduced untouched.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class VehicleMechPage
    {
        public const int Commands = 200;
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Bg     = DragonPalette.Background;
        static readonly Rgba Accent = DragonPalette.Accent;
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Dim    = DragonPalette.Text6;
        static readonly Rgba Faint  = DragonPalette.Text7;
        static readonly Rgba Hair   = DragonPalette.Hairline;
        static readonly Rgba Amber  = DragonPalette.Caution;

        // outer nodes: label | unit | angle (deg, 0 = top, clockwise). The VALUE is live — see NodeValue.
        static readonly string[] NodeLabel = { "ACCELERATION", "CENTRIPETAL", "PRESSURE", "RESISTANCE", "WATER UPRIGHTING" };
        static readonly string[] NodeUnit  = { "g", "g", "psia", "g", "" };
        static readonly float[]  NodeAng   = { 0f, 72f, 144f, 216f, 288f };

        static readonly string[] SeatLabel = { "SEAT 1 TACH", "SEAT 2 TACH", "SEAT 3 TACH", "SEAT 4 TACH" };

        /// <summary>No-source dash — the idiom the whole mod uses for a value nothing can supply.</summary>
        const string Dash = "—";

        /// <summary>This node's live reading, or null where nothing on the vehicle can answer it.
        /// Index order is <see cref="NodeLabel"/>'s; see the file header for what each term is.</summary>
        static string NodeValue(int i, PageState s)
        {
            if (!s.Valid) return null;
            switch (i)
            {
                case 0: return s.AccelPosText;
                case 1: return s.AccelCentText;
                case 2: return s.PressText;
                case 3: return s.AccelNegText;
                default: return null;              // WATER UPRIGHTING — not modelled anywhere
            }
        }

        /// <summary>The same node's dial fraction, 0..1, against the full scale its source states
        /// (VesselData.Acceleration for the three g terms, Cabin.PressFullScale for the pressure).
        /// The ring and the number are the same reading, so neither can drift from the other.</summary>
        static float NodeFrac(int i, PageState s)
        {
            if (!s.Valid) return 0f;
            switch (i)
            {
                case 0: return (float)s.AccelPos01;
                case 1: return (float)s.AccelCent01;
                case 2: return (float)s.Cabin.Press01;
                case 3: return (float)s.AccelNeg01;
                default: return 0f;
            }
        }

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            int St(float rs) => Strokes.Px(rs, sy);   // ONE rule, in Strokes.cs - rounds UP (R-02 family)
            void C(string t, float cx, float y, float s, Rgba c) => dl.Text(t, PX(cx), PY(y), SZ(s), TextAlign.Centre, c);
            void L(string t, float x, float y, float s, Rgba c) => dl.Text(t, PX(x), PY(y), SZ(s), TextAlign.Left, c);
            void R(string t, float rx, float y, float s, Rgba c) => dl.Text(t, PX(rx), PY(y), SZ(s), TextAlign.Right, c);

            dl.Rect(0, 0, w, h, Bg);
            C("MECH PANEL", 1713, 40, 46, White);

            const float ccx = 1713f, ccy = 1040f, ring = 440f, nodeR = 740f, donut = 110f;

            // spokes + central ring
            for (int i = 0; i < NodeLabel.Length; i++)
            {
                double a = NodeAng[i] * Math.PI / 180.0;
                float nx = ccx + (float)Math.Sin(a) * nodeR, ny = ccy - (float)Math.Cos(a) * nodeR;
                float ex = ccx + (float)Math.Sin(a) * ring, ey = ccy - (float)Math.Cos(a) * ring;
                dl.Line(PX(ex), PY(ey), PX(nx), PY(ny), St(2), Hair);
            }
            dl.ArcBand(PX(ccx), PY(ccy), SZ(ring - 5), SZ(ring), 0, 360, Faint);

            // central SEATS tachometers. The ROW COUNT is live (the capsule's own seat count); the
            // reading is not — no per-seat tachometer exists on this vehicle, so it dashes.
            C("SEATS", ccx, ccy - 190, 30, Accent);
            int seats = (s.Valid && s.SeatCount > 0) ? s.SeatCount : SeatLabel.Length;
            if (seats > SeatLabel.Length) seats = SeatLabel.Length;
            // ---- S39 (finishing S38's sweep): THE SEAT ROWS' DASH COLUMN COMES IN ----
            // Up to seven stacked label-value rows, 440 design units apart at a 26-unit type size
            // (19x) on an 80-unit pitch, with nothing joining label to value across the gap. Milder
            // than the NavOrbitPlot block (the pitch is nearly twice as forgiving) but the same class
            // and the same cheap remedy: bring the column in. 440 -> 240, and the block STAYS CENTRED
            // in the donut - moving only the value column would have pulled it off the ring's axis,
            // which is the one thing this page's layout cannot give up.
            const float SeatHalfSpan = 120f;
            for (int i = 0; i < seats; i++)
            {
                L(SeatLabel[i], ccx - SeatHalfSpan, ccy - 120 + i * 80, 26, White);
                R(Dash, ccx + SeatHalfSpan, ccy - 120 + i * 80, 26, Dim);
            }
            // S22: "Awaiting" is reference COPY, not a live reading — dash-and-dim it on a dead feed,
            // same rule as VehicleOverviewPage's checklist, so the two pages can't disagree. The
            // "ALL SYSTEMS CHECK" label itself is untouched.
            // ---- S105 / QC MP-01: THE WORD IS THE REFERENCE'S, THE CAUTION COLOUR WAS OURS ----
            // "Awaiting" is reference copy and stays (see the header: §6 scopes this page to the VALUES).
            // Drawing it in CAUTION AMBER was not the reference's - it was a severity this build added on
            // top of a reproduced word, and §14.4(a) is explicit that a colour that means "fault" is not
            // spent on something that is not one. A permanent amber the crew can never clear also spends
            // the signal: when something really does need attention here, nothing changes.
            // ⚠ It reads WHITE now, not green: a hardcoded green would be the same defect inverted, an
            // all-clear with no model behind it (S31/S32). White asserts nothing, which is the truth.
            // ⚠ The Vehicle Overview prints "Normal" for a row of the SAME NAME. That contradiction is
            // between two REFERENCE mockups, not between two of our choices, so §1.4 keeps both as they
            // are - it is an owner question, recorded in docs/QC_FINDINGS.md (MP-01), not a build fix.
            C("ALL SYSTEMS CHECK", ccx, ccy + 250, 24, Dim);
            C(s.Valid ? "Awaiting" : Dash, ccx, ccy + 290, 26, s.Valid ? White : Dim);

            // outer donut nodes
            for (int i = 0; i < NodeLabel.Length; i++)
            {
                double a = NodeAng[i] * Math.PI / 180.0;
                float nx = ccx + (float)Math.Sin(a) * nodeR, ny = ccy - (float)Math.Cos(a) * nodeR;
                // The ring was a FIXED 240° sweep on every node — decoration beside a number that now
                // moves. It is the same 300°-arc-with-a-60°-gap the gauges on every other vehicle page
                // use, filled by this node's own reading; a node with no source draws no fill at all,
                // so "unanswerable" and "reading zero" do not look alike.
                string val = NodeValue(i, s);
                bool live = !string.IsNullOrEmpty(val);
                float frac = NodeFrac(i, s);
                dl.ArcBand(PX(nx), PY(ny), SZ(donut - 8), SZ(donut), -150, 150, Faint);
                if (live && frac > 0f)
                    dl.ArcBand(PX(nx), PY(ny), SZ(donut - 8), SZ(donut),
                               -150, -150 + 300f * (frac > 1f ? 1f : frac), Accent);
                C(NodeLabel[i], nx, ny - donut - 40, 24, White);
                C(live ? val : Dash, nx, ny - 22, 34, live ? White : Dim);
                if (live && NodeUnit[i].Length > 0) C(NodeUnit[i], nx, ny + 26, 22, Dim);
            }

            // ---- subsystem tab bar (Mech active) + bottom status bar ----
            // S12: same Severities(s) T5 wired into Overview/Subsystem — this page was out of T5's
            // declared scope, so its tab bar was still reading nominal on a genuine subsystem alert.
            VehicleTabBar.Draw(dl, w, h, 3, VehicleTabBar.Severities(s));
            VehicleDeepViewLinks.Draw(dl, w, h);
            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame
        }
    }
}
