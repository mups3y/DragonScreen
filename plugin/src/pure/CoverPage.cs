// DragonScreen — CoverPage  (PURE: the Figma "cover" screen, the Deorbit dashboard)
// ============================================================================================
// A PIXEL-EXACT rebuild that places the design's OWN Figma-exported PNG assets (art/cover/*.png) at
// their exact positions. Every position/size below was measured by locating each asset inside the
// reference frame "Frame 67.png" (masked template match, node 12221-244, 3427 x 2112). So this is
// the real artwork in the real places, not a reproduction.
//
// The frame is mapped onto the panel with a single UNIFORM scale + centering (Fit); the letterbox is
// the same #020738 as the design, so the seam is invisible. The ONE thing not from a static asset is
// the globe (Group 65): the owner wants it LIVE, so NavPage.Planet draws the real Earth+track filling
// the globe rect instead. The 10 hairline assets (Line 85–94) are drawn as primitives (a 1px PNG will
// not survive the downscale) at their measured positions.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class CoverPage
    {
        public const int Commands = 240;
        const float RefW = 3427f, RefH = 2112f;

        // key | x | y | w | h  — measured asset placements (art/cover/<key>.png)
        static readonly string[] Keys = {
            "rectangle_173","rectangle_178","rectangle_179","rectangle_180","rectangle_181",
            "rectangle_183","rectangle_95","rectangle_176","rectangle_177","rectangle_182","rectangle_174",
            "rectangle_169","splashdown_time_t_01_24_51","inertial_velocity_7_69km_s","altitude_393_3km",
            "apogee_416_2km","perigee_379_4km","inclination_51_62deg","active_phase_deorbit_coast","eva_menu_fill",
            "gridicons_refresh","running_00_22_57","coast_to_trunk_jettison","ic_sharp_arrow_back","ic_sharp_arrow_back_1",
            "deport_burn","coast_to_trunk","crew_interrupt_conditions","union","30deg_sustained_altitude_error",
            "far_field_pointing_1","claw_separati","600deg_m_altitude_rate","far_field_pointing","procedure",
            "crew_deorbit_preparation","union_6","deorbit_burn_3_hrs","on_spacex_on_begin_procedure_4_700","manual_chute",
            "nlt_deorbit_burn_1_hr","deorbit_burn_brief","bi_arrow_right_short","nlt_deorbit_burn_30_min",
            "review_reference_content","deorbit_entry_and_landing_go_no_go","acknowledge",
            "1_monitor_slow_to_free_flight_altitude_sun_geo_pointing","2_after_spacex_go_for_deorbit_verify_entry_is_enabled",
            "false","entry_enabled","true","3_after_entry_is_enabled_dragon_transitions_to_claw","camera_auto_earth_io",
            "target_latitude_26deg_15_00deg_n","target_longitude_26deg_15_00deg_n","ic_sharp_subtract","settings",
            "union_2","union_5","union_4","union_3","union_1"
        };
        static readonly int[,] Box = {
            {0,0,3427,220},{218,216,1224,1779},{240,443,1187,317},{240,792,1187,449},{240,1273,1187,550},
            {21,427,178,150},{29,557,162,8},{260,258,110,110},{382,258,110,110},{1427,438,15,920},{2994,1810,401,111},
            {21,31,154,154},{749,56,303,113},{1765,56,266,113},{2151,56,211,113},
            {2482,56,200,113},{2802,56,203,113},{3125,56,182,113},{197,64,263,89},{70,80,56,56},
            {1370,279,55,55},{1222,282,152,83},{532,283,566,65},{291,289,48,48},{413,289,48,48},
            {49,301,122,68},{53,475,114,68},{362,499,496,56},{315,509,36,36},{316,588,413,45},
            {1053,588,317,45},{43,649,134,68},{316,654,317,45},{1053,654,317,45},{39,824,142,34},
            {362,848,487,56},{315,858,36,36},{316,937,308,45},{779,937,591,45},{59,964,102,68},
            {316,1003,343,45},{1093,1003,277,45},{1706,1048,16,16},{316,1069,394,45},
            {964,1069,406,45},{316,1135,565,45},{1158,1135,212,45},
            {316,1329,1064,56},{316,1453,1014,56},
            {1132,1549,81,45},{351,1555,195,34},{783,1555,51,34},{316,1666,969,56},{3032,1718,346,59},
            {2014,1822,251,90},{2361,1822,280,90},{3024,1838,56,56},{3124,1847,140,37},
            {96,271,24,24},{96,445,24,24},{96,619,24,24},{96,794,24,24},{96,934,24,24}
        };
        // leader / divider hairlines (Line 85–94): x0 | x1 | y  — drawn as primitives
        static readonly int[,] Lines = {
            {316,1368,566},{729,1053,621},{633,1053,687},{316,1368,915},{624,779,970},
            {659,1093,1036},{710,964,1102},{876,1158,1168},{351,1427,1532},{351,1427,1609}
        };

        /// <summary>The SEVEN real deorbit phases the left rail selects — the centre heading + rail
        /// highlight track this IN-PAGE (they do not navigate). The community Figma export baked only
        /// five rail rows; the real capsule rail has seven (REAL_SPACEX_SCREENSHOTS / docs/SCREEN_
        /// INVENTORY), so the whole rail is redrawn as primitives here. Order matches PhaseButton + the
        /// rail draw. PhaseName is the full heading; RailL1/RailL2 are the two-line strip labels.</summary>
        public static readonly string[] PhaseName = {
            "Deport & Burn", "Coast to Trunk Jettison", "Claw Separation Prep",
            "Procedure", "Procedure", "Reference Content", "Manual Chute Deploy" };
        static readonly string[] RailL1 = { "Deport &", "Coast to", "Claw",      "Procedure", "Procedure", "Reference", "Manual" };
        static readonly string[] RailL2 = { "Burn",     "Trunk",    "Sep. Prep", "",          "",          "Content",   "Chute"  };
        public const int PhaseCount = 7;

        // top-y of each rail highlight slot (the baked rectangle_183 border box is 178×150; slots are
        // pitched 168 apart so the seven rows run down the left strip without their boxes touching).
        static readonly float[] SlotY = { 253f, 421f, 589f, 757f, 925f, 1093f, 1261f };
        const float RailCx = 110f, RailBoxH = 150f;   // strip centre-x, highlight-box height (design px)

        // Baked assets NOT drawn from the export: the dynamic highlight + heading, plus the five baked
        // rail labels and their union dots (the rail is redrawn as seven primitive rows instead).
        static readonly string[] SkipKeys = {
            "rectangle_178", "rectangle_183", "rectangle_95", "coast_to_trunk_jettison",
            "deport_burn", "coast_to_trunk", "claw_separati", "procedure", "manual_chute",
            "union_1", "union_2", "union_3", "union_4", "union_5" };

        public static void Build(DisplayList dl, int w, int h, PageState s, MapView view)
        { Build(dl, w, h, s, view, 1); }

        public static void Build(DisplayList dl, int w, int h, PageState s, MapView view, int selectedPhase)
        {
            // ---- FILL-TO-FIT reflow: scale to the HEIGHT (fills vertically, no top/bottom gap), and
            // put the horizontal slack into the empty gap between the left panel and the globe: anchor
            // everything left of Split at its position, shift everything right of Split to the right
            // edge, and stretch the two full-width bars across. Nothing is scaled non-uniformly, so the
            // globe stays round and text/icons keep their exact size. ----
            float sc = h / RefH;
            float extra = w - RefW * sc; if (extra < 0f) extra = 0f;
            const float Split = 1500f;
            float X(float x) => x * sc + (x >= Split ? extra : 0f);
            float Y(float y) => y * sc;
            float Z(float v) => v * sc;
            float Wd(float x, float wref) => wref * sc + (x < Split && x + wref > Split ? extra : 0f); // stretch straddlers (bars)
            int St(float rs) { int p = (int)Math.Round(rs * sc); return p < 1 ? 1 : p; }

            dl.Rect(0, 0, w, h, DragonPalette.Background);

            // content panel border FIRST — in the Figma it runs a few px under the top + bottom bars, so
            // those (drawn later: rectangle_173 in the loop, component_48 right below) cover its overhang.
            dl.Asset("rectangle_178", X(218), Y(216), Wd(218, 1224), Z(1779), DragonPalette.White);

            // bottom status bar (Component 48: bg + CURRENT STATE / POINTING MODE / SPX·TDRS·ISS text) — full width
            dl.Asset("component_48", X(0), Y(1877), Wd(0, 3427), Z(235), DragonPalette.White);

            // the LIVE globe, circular, CENTRED in the space to the right of the left content panel
            // (panel right edge = X(1442)) and vertically centred between the top and bottom bars.
            float gs = Z(1809);
            float gcx = (X(1442f) + w) * 0.5f;
            float gcy = (Y(220f) + Y(1877f)) * 0.5f;
            NavPage.Planet(dl, s, view, gcx - gs * 0.5f, gcy - gs * 0.5f, gs, gs);

            // every placed asset — anchored left/right of the split, bars stretched. rectangle_178 was
            // already drawn behind the bars above, so skip it here. The rail highlight (rectangle_183 +
            // rectangle_95) and the centre heading (coast_to_trunk_jettison) are DYNAMIC — the export
            // baked them onto one phase. The five baked rail rows (labels deport_burn…manual_chute + the
            // union_1…5 dots) are ALSO skipped: the rail is redrawn below as seven primitive rows.
            for (int i = 0; i < Keys.Length; i++)
            {
                string k = Keys[i];
                if (Array.IndexOf(SkipKeys, k) >= 0) continue;
                dl.Asset(k, X(Box[i, 0]), Y(Box[i, 1]), Wd(Box[i, 0], Box[i, 2]), Z(Box[i, 3]), DragonPalette.White);
            }

            int sp = selectedPhase < 0 ? 0 : (selectedPhase >= PhaseCount ? PhaseCount - 1 : selectedPhase);

            // the seven-item deorbit phase rail + the selected phase's highlight (shared verbatim with the
            // Manual Chute Deploy page via DrawRail), then the centre heading (Cover-specific).
            DrawRail(dl, w, h, sp);
            dl.Text(PhaseName[sp], X(490), Y(286), Z(58), TextAlign.Left, DragonPalette.White);

            // the hairlines, as crisp primitives at their measured positions
            for (int i = 0; i < Lines.GetLength(0); i++)
                dl.Line(X(Lines[i, 0]), Y(Lines[i, 2]), X(Lines[i, 1]), Y(Lines[i, 2]), St(2), DragonPalette.Text6);
        }

        /// <summary>Draw the seven-item deorbit phase rail (ring marker + two-line label per row) plus the
        /// selected row's highlight box + cyan underline, using the same fill-to-fit reflow as Build. Shared
        /// by the Cover and the Manual Chute Deploy page so their rails are pixel-identical. `selected` is the
        /// lit phase index (0..6); pass 6 on the Manual Chute page.</summary>
        public static void DrawRail(DisplayList dl, int w, int h, int selected)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float sc = h / RefH; float extra = w - RefW * sc; if (extra < 0f) extra = 0f; const float Split = 1500f;
            float X(float x) => x * sc + (x >= Split ? extra : 0f);
            float Y(float y) => y * sc;
            float Z(float v) => v * sc;
            float Wd(float x, float wref) => wref * sc + (x < Split && x + wref > Split ? extra : 0f);

            int sp = selected < 0 ? 0 : (selected >= PhaseCount ? PhaseCount - 1 : selected);
            for (int i = 0; i < PhaseCount; i++)
            {
                bool on = (i == sp);
                float cy = SlotY[i] + 34f;
                dl.ArcBand(X(RailCx), Y(cy), Z(9), Z(15), 0, 360, on ? DragonPalette.Accent : DragonPalette.Text5);
                dl.Text(RailL1[i], X(RailCx), Y(SlotY[i] + 56f), Z(32), TextAlign.Centre, DragonPalette.White);
                if (RailL2[i].Length > 0)
                    dl.Text(RailL2[i], X(RailCx), Y(SlotY[i] + 92f), Z(32), TextAlign.Centre, DragonPalette.White);
            }
            float slot = SlotY[sp];
            dl.Asset("rectangle_183", X(21), Y(slot), Wd(21, 178), Z(RailBoxH), DragonPalette.White);
            dl.Asset("rectangle_95", X(29), Y(slot + 130f), Wd(29, 162), Z(8), DragonPalette.White);
        }

        // ---- INTERACTIVITY: the touch targets, exact rectangles from the Figma layer bounds ----
        public enum CoverButton
        {
            None, Menu, Back, Forward,
            PhaseDeport, PhaseCoast, PhaseClaw, PhaseProcedure, PhaseProcedure2, PhaseReference, PhaseManual,
            Settings, ActOnSpaceX, ActDeorbitBrief, ActReview, ActAcknowledge, EntryTrue, EntryFalse
        }

        // Rail row index (0..6) → its button. Same order as PhaseName/SlotY. The rail hit rows are
        // derived from SlotY in HitTest so they stay in lockstep with the drawn strip.
        static readonly CoverButton[] PhaseButton = {
            CoverButton.PhaseDeport, CoverButton.PhaseCoast, CoverButton.PhaseClaw, CoverButton.PhaseProcedure,
            CoverButton.PhaseProcedure2, CoverButton.PhaseReference, CoverButton.PhaseManual };

        /// <summary>The phase index (0..6) a rail button selects, or -1 if it is not a rail button.</summary>
        public static int PhaseOf(CoverButton b)
        {
            for (int i = 0; i < PhaseButton.Length; i++) if (PhaseButton[i] == b) return i;
            return -1;
        }

        // button | x | y | w | h  (frame-local; rendered positions, so rotated boxes are where they LOOK).
        // The seven phase-rail rows are NOT here — they are hit-tested from SlotY (see HitTest).
        static readonly int[,] Hits = {
            {(int)CoverButton.Menu,          21,  31, 154, 154},
            {(int)CoverButton.Back,         260, 258, 110, 110},
            {(int)CoverButton.Forward,      382, 258, 110, 110},
            {(int)CoverButton.Settings,    2994,1810, 401, 111},
            {(int)CoverButton.ActOnSpaceX,  779, 930, 591,  60},
            {(int)CoverButton.ActDeorbitBrief,1093,996,277,  60},
            {(int)CoverButton.ActReview,    964,1062, 406,  60},
            {(int)CoverButton.ActAcknowledge,1158,1128,212, 60},
            {(int)CoverButton.EntryTrue,    770,1548, 90,  50},
            {(int)CoverButton.EntryFalse,  1125,1544, 100, 55}
        };

        /// <summary>Which cover-page button a touch at panel pixel (px,py) hit — None if it missed.
        /// Uses the SAME Fit map as Build, inverted, so the touch lands on the exact Figma rectangle.</summary>
        public static CoverButton HitTest(float px, float py, int w, int h)
        {
            float sc = h / RefH;
            if (sc <= 0f) return CoverButton.None;
            float extra = w - RefW * sc; if (extra < 0f) extra = 0f;
            const float Split = 1500f;
            float thr = Split * sc;                                  // panel-x where the right block starts
            float fx = (px < thr) ? px / sc : (px - extra) / sc;     // inverse of the reflow map
            float fy = py / sc;

            // the seven phase-rail rows — one per SlotY slot, spanning the left strip. Checked first so
            // the rail owns its column; the slot band is the full pitch so there are no dead gaps.
            if (fx >= 15f && fx < 205f)
                for (int i = 0; i < PhaseCount; i++)
                    if (fy >= SlotY[i] - 4f && fy < SlotY[i] + RailBoxH + 14f)
                        return PhaseButton[i];

            for (int i = 0; i < Hits.GetLength(0); i++)
            {
                if (fx >= Hits[i, 1] && fx < Hits[i, 1] + Hits[i, 3] &&
                    fy >= Hits[i, 2] && fy < Hits[i, 2] + Hits[i, 4])
                    return (CoverButton)Hits[i, 0];
            }
            return CoverButton.None;
        }
    }
}
