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
        // The MAP camera view is the heaviest: its 90-sample ground track is a line command per segment
        // on top of the placed assets, and it measured 258 in the preview at zoom 0 (the Earth view, the
        // old peak, is 231). Headroom over that.
        public const int Commands = 340;
        const float RefW = 3427f, RefH = 2112f;

        // key | x | y | w | h  — measured asset placements (art/cover/<key>.png)
        static readonly string[] Keys = {
            "rectangle_173","rectangle_178","rectangle_179","rectangle_180","rectangle_181",
            "rectangle_183","rectangle_95","rectangle_176","rectangle_177","rectangle_182","rectangle_174",
            "rectangle_169","splashdown_time_t_01_24_51","inertial_velocity_7_69km_s","altitude_393_3km",
            "apogee_416_2km","perigee_379_4km","inclination_51_62deg","active_phase_deorbit_coast","eva_menu_fill",
            "gridicons_refresh","running_00_22_57","coast_to_trunk_jettison","ic_sharp_arrow_back","ic_sharp_arrow_back_1",
            // 2026-09-02 (S13, owner decision via the overseer): the human-facing label for these two baked
            // assets reads ATTITUDE, not altitude — "30° sustained attitude error" / "600°/min attitude
            // rate" — a blurry-photo transcription corrected on physics grounds (C1.4/C7.1), applied in
            // DeorbitBurnPrepPage.cs (full writeup there). The key strings below are the community Figma's
            // own baked filenames (still literally "altitude") and are left verbatim so the asset loader
            // still finds art/cover/<key>.png — only the label/comments changed, never the baked key.
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
        // `camera_auto_earth_io` joins them (T4): the baked caption names ONE of the three camera
        // views, so it is redrawn as live text at the asset's own measured metrics — see DrawCameraChrome.
        static readonly string[] SkipKeys = {
            "rectangle_178", "rectangle_183", "rectangle_95", "coast_to_trunk_jettison",
            "deport_burn", "coast_to_trunk", "claw_separati", "procedure", "manual_chute",
            "union_1", "union_2", "union_3", "union_4", "union_5", "camera_auto_earth_io" };

        // ---- S13's RESIDUAL, CLOSED 2026-09-03 (QC-AUDIT, owner-directed in that chat) ----
        // S13 decided the deorbit interrupt criteria are ATTITUDE, not altitude, and applied it to
        // DeorbitBurnPrepPage.cs — but left these two BAKED captions reading "altitude", so the two
        // surfaces that state the same criterion disagreed on glass (C7.1). The baked PNGs cannot be
        // relabelled without re-rendering community art, so the two captions are SKIPPED here and
        // redrawn as primitives from DeorbitBurnPrepPage's own S13-corrected strings. The asset KEYS
        // are still untouched (S13's rule) — the files stay on disk, they are simply not placed.
        static readonly string[] AttitudeSkipKeys = {
            "30deg_sustained_altitude_error", "600deg_m_altitude_rate" };

        // Their replacements sit in the baked assets' OWN measured boxes, read out of Keys/Box rather
        // than re-typed, so the rows cannot drift off the hairlines if the placement is ever re-measured.
        // Vertically centred in the box the PNG occupied; the size is set to match the neighbouring
        // baked captions (FAR FIELD POINTING, on the same two rows, is still baked).
        const float AttSize = 32f;
        static readonly float AttX  = BoxOf("30deg_sustained_altitude_error", 0);
        static readonly float AttY1 = BoxOf("30deg_sustained_altitude_error", 1)
                                    + (BoxOf("30deg_sustained_altitude_error", 3) - AttSize) * 0.5f;
        static readonly float AttY2 = BoxOf("600deg_m_altitude_rate", 1)
                                    + (BoxOf("600deg_m_altitude_rate", 3) - AttSize) * 0.5f;

        /// <summary>One column of a placed asset's measured box, by key. Used only by static
        /// initialisers, never per frame.</summary>
        static float BoxOf(string key, int col)
        {
            int i = Array.IndexOf(Keys, key);
            return i < 0 ? 0f : Box[i, col];
        }

        // Rail index of "Reference Content" (§14.4(c)): NOT a standalone page — a deorbit quick-reference
        // that replaces the content-panel BODY only, in-page, when this phase is selected. The three baked
        // panel-body cards (rectangle_179/180/181) and their captions are all specific to the ONE phase the
        // community export happened to bake ("Coast to Trunk Jettison") — real content, but the wrong
        // phase's content — so those particular keys are swapped out here and replaced by real §8 data,
        // never invented (§1.4). The card BACKGROUNDS (rectangle_179/180/181) are real Figma layout and
        // stay; only their baked captions/rows are swapped for the reference text.
        const int ReferencePhase = 5;
        static readonly string[] ReferenceSkipKeys = {
            "crew_interrupt_conditions", "union", "30deg_sustained_altitude_error", "far_field_pointing_1",
            "600deg_m_altitude_rate", "far_field_pointing", "crew_deorbit_preparation", "union_6",
            "deorbit_burn_3_hrs", "on_spacex_on_begin_procedure_4_700", "nlt_deorbit_burn_1_hr",
            "deorbit_burn_brief", "bi_arrow_right_short", "nlt_deorbit_burn_30_min", "review_reference_content",
            "deorbit_entry_and_landing_go_no_go", "acknowledge",
            "1_monitor_slow_to_free_flight_altitude_sun_geo_pointing",
            "2_after_spacex_go_for_deorbit_verify_entry_is_enabled", "false", "entry_enabled", "true",
            "3_after_entry_is_enabled_dragon_transitions_to_claw" };

        // ============================================================================================
        // THE CAMERA (T4) - the right-hand view slot, and the NEXT VIEW cycle that changes it
        // ============================================================================================
        // NOT invented: the reference UI's own source. First.vue (the deorbit page) composes the
        // right-hand slot from THREE interchangeable components and cycles them with one button -
        // assets/reference/dragon2-ui-master/src/views/First.vue:
        //
        //     components: { 'view-00': View01, 'view-01': NavEarth, 'view-02': Capsule }
        //     swapComponent() { this.count = (this.count + 1) % 3 ... }
        //         view-00 -> viewHeading = 'Auto - Earth IO'      (the 3D Earth, a three.js sphere)
        //         view-01 -> viewHeading = 'Auto - Map IO'        (NavEarth: the flat, pannable map)
        //         view-02 -> viewHeading = 'Auto - Capsule IO'    (Capsule: the vehicle itself)
        //     <button @click="swapComponent()" id="swap-view"> ... NEXT VIEW </button>
        //
        // All three occupy the SAME region - #scroll-earth-wrapper and #capsule-wrapper are both
        // top:10% left:40.5% width:60% height:90% - which is the slot our live globe already fills.
        // "Camera", the heading and "NEXT VIEW" are all in docs/UI_AUDIT.md's First.vue label list, and
        // DillonBaird's Navigation render carries the same pair ("2D & 3D map views ... camera-mode
        // label"), so this is tier-1 on both counts (§1.4). Two deliberate departures, both stated:
        //   · we OPEN on Earth, not the Vue's view-01 default, because Frame 67 - the design we build -
        //     bakes "Auto - Earth IO" and the TARGET LAT/LON pair that only that view shows;
        //   · the Capsule view draws the shipped dragon.png still. The reference spins a 3D model; the
        //     sprite TURNTABLE that replaces this still is register task T11 (§5), not T4.
        public enum CoverCam { Earth = 0, Map = 1, Capsule = 2 }
        public const int CamCount = 3;

        static readonly string[] CamHeadings = { "Auto - Earth IO", "Auto - Map IO", "Auto - Capsule IO" };

        /// <summary>The viewHeading this camera view puts under the CAMERA caption. Verbatim from
        /// First.vue's swapComponent (and, for Earth, from the baked Figma asset).</summary>
        public static string CamHeading(CoverCam c)
        {
            int i = (int)c;
            return (i >= 0 && i < CamHeadings.Length) ? CamHeadings[i] : CamHeadings[0];
        }

        /// <summary>NEXT VIEW: count = (count + 1) % 3, so Earth -> Map -> Capsule -> Earth.</summary>
        public static CoverCam NextCam(CoverCam c)
        {
            int i = (int)c + 1;
            if (i < 0 || i >= CamCount) i = 0;
            return (CoverCam)i;
        }

        /// <summary>Which NavMode the shared MapView must be in for this camera view's pan/zoom/centre
        /// to mean the right thing - MapProjection.Pan/Zoom/Centre branch on it (the flat map pans in
        /// lat/lon, the globe spins about its axis). Capsule has no map, so it keeps the globe's.</summary>
        public static NavMode CamMapMode(CoverCam c)
        { return (c == CoverCam.Map) ? NavMode.Map : NavMode.Planet; }

        // Shown ONLY on the Earth view - v-if="currentComponent === 'view-00'" on both readouts in
        // First.vue. They are a ground target's lat/lon, which the flat map and the capsule do not plot.
        static readonly string[] EarthOnlyKeys = {
            "target_latitude_26deg_15_00deg_n", "target_longitude_26deg_15_00deg_n" };

        // ---- S75: THE GLYPHS THAT ARE NOT BUTTONS, DRAWN SO THEY DO NOT LOOK LIKE ONES ----
        // Every OTHER white glyph on this page is touchable: eva_menu_fill is CoverButton.Menu, and the
        // two ic_sharp_arrow_back exports are Back and Forward — all three sit in the Hits table below.
        // White-glyph-means-button is therefore this page's own idiom, and gridicons_refresh (top-right
        // of the content panel, inline with RUNNING / 00:22:57) was riding that idiom with NO hit rect
        // at all. SCREEN_LIVENESS_AUDIT.md H18 files it with `SHOW MARGINS TO` as the same defect class:
        // a painted control that resolves to nothing, which is worse than a no-op because a no-op at
        // least names an action. S54 fixed the mirror-image defect (a rect that fires with no label);
        // this is the label with no rect.
        // It does NOT get a rect here. What a refresh control refreshes on this page is a §1.4 source
        // question and there is no source for it — the community Figma baked the glyph and recorded no
        // behaviour — so inventing "re-read the procedure" or "restart the timer" would be inventing the
        // action the icon asks for, exactly what C1.4 forbids. It takes S75's other branch instead:
        // drawn INERT (Text6, this build's "no live source behind this" tint) so it reads as part of the
        // RUNNING status line it sits in rather than as a fourth white affordance beside three real ones.
        // If a real source for the action ever appears, it goes back to White AND enters Hits — together.
        static readonly string[] InertKeys = { "gridicons_refresh" };

        /// <summary>The tint an inert, un-hit-testable glyph is drawn in. Text6 is the same "nothing
        /// live behind this" tint the dashed readouts use, so the distinction reads at IVA distance
        /// without moving the asset or changing the layout.</summary>
        public static readonly Rgba InertTint = DragonPalette.Text6;

        // ---- camera geometry, all in the 3427x2112 design frame ----
        // The slot: from the content panel's right edge to the frame edge, between the top strip and the
        // bottom bar (First.vue's wrapper starts at 40.5% = design x 1388; the panel edge, 1442, is the
        // same slot one hairline in).
        const float ViewLeft = 1442f, ViewTop = 220f, ViewBottom = 1877f;
        const float ViewInset = 40f;

        // NEXT VIEW: rectangle_174's EXACT size, on rectangle_174's row, at the other end of the slot.
        // The reference puts #swap-view bottom-right (top:90% right:5% width:10%) - in Frame 67 that
        // corner is the SETTINGS button, so the pill moves to the free left end of the same row and is
        // built as SETTINGS' twin (same size, same dash-then-label interior) so the two read as a pair.
        const float NextX = 1500f, NextY = 1810f, NextW = 401f, NextH = 111f;

        // The MAP view's pan/centre/zoom cluster, NavEarth.vue's arrangement exactly: a centre button
        // with the four arrows ONE pitch away (centre right:7em top:7em; arrows at 2/12em, so the pitch
        // is 5em), and the zoom pair a row below (top:17em) HALF a pitch either side of the centre line
        // (ZOOM IN right:9em, ZOOM OUT right:4.5em - + on the LEFT is the reference's ordering, not a
        // slip). Anchored to the MAP's own top-right corner, as NavEarth anchors it to its wrapper's,
        // rather than to fixed frame coordinates the map band would slide out from under.
        const float PadS = 104f, PadPitch = 118f, PadInset = 46f, PadLabel = 26f;

        /// <summary>NavEarth's `background-color: rgba(2, 7, 56, 0.75)`: the page background, translucent,
        /// so the map still reads through the cluster.</summary>
        static readonly Rgba PadFace = Rgba.Hex("020738", 0.75f);

        // The CAMERA caption, measured off the baked camera_auto_earth_io asset it replaces (346x59 at
        // 3032,1718): "CAMERA" occupies cap rows 5..19, the heading rows 35..56, both centred on x+173.
        // Cap height is ~0.7em and a text y is the top of the line box, ~0.1em above the cap.
        const float CamCx = 3205f;
        const float CamCapY = 1721f, CamCapSize = 21f;
        const float CamHeadY = 1750f, CamHeadSize = 31f;

        /// <summary>Panel-pixel rect of the NEXT VIEW pill. One calculation for the draw and the hit -
        /// PageAction's rule: a control drawn from one and hit from another drifts on first touch.</summary>
        public static void NextViewRect(int w, int h, out float x, out float y, out float rw, out float rh)
        {
            float sc = h / RefH, extra = w - RefW * sc; if (extra < 0f) extra = 0f;
            x = NextX * sc + extra; y = NextY * sc; rw = NextW * sc; rh = NextH * sc;
        }

        /// <summary>Panel-pixel rect of one cluster button, in pitches from the centre button: (0,0) is
        /// CTR, the four unit steps are the arrows, and dy=2 with dx=-0.5/+0.5 is the zoom row. Measured
        /// off MapRect so the cluster cannot drift off the map when the panel aspect changes, and shared
        /// by the draw, the hit test and the tests - PageAction's rule.</summary>
        public static void PadRect(int w, int h, float dx, float dy, out float x, out float y,
                                   out float rw, out float rh)
        {
            float sc = h / RefH;
            float mx, my, mw, mh;
            MapRect(w, h, out mx, out my, out mw, out mh);
            rw = rh = PadS * sc;
            float pitch = PadPitch * sc, inset = PadInset * sc;
            // The centre button one pitch in from the map's top-right corner, so the RIGHT arrow's edge
            // lands on the inset and the UP arrow's top does too.
            float cx = mx + mw - inset - rw * 0.5f - pitch;
            float cy = my + inset + rh * 0.5f + pitch;
            x = cx + dx * pitch - rw * 0.5f;
            y = cy + dy * pitch - rh * 0.5f;
        }

        /// <summary>The 2D MAP view's panel rect: the widest 2:1 band that fits the slot, centred in it.
        /// 2:1 is the equirectangular aspect, so MapProjection's zoom 0 FILLS it instead of letterboxing
        /// - which is how the reference's #scroll-earth (background-size cover) fills its wrapper. A
        /// letterboxed default would put the crew's first look at the map inside two dead bands.</summary>
        public static void MapRect(int w, int h, out float x, out float y, out float rw, out float rh)
        {
            float sc = h / RefH;
            float m = ViewInset * sc;
            float l = ViewLeft * sc + m, r = w - m;
            float t = ViewTop * sc + m, b = ViewBottom * sc - m;
            float aw = r - l, ah = b - t;
            if (aw < 8f) aw = 8f;
            if (ah < 8f) ah = 8f;
            rw = aw; rh = rw * 0.5f;
            if (rh > ah) { rh = ah; rw = rh * 2f; }
            x = l + (aw - rw) * 0.5f;
            y = t + (ah - rh) * 0.5f;
        }

        public static void Build(DisplayList dl, int w, int h, PageState s, MapView view)
        { Build(dl, w, h, s, view, 1, CoverCam.Earth); }

        public static void Build(DisplayList dl, int w, int h, PageState s, MapView view, int selectedPhase)
        { Build(dl, w, h, s, view, selectedPhase, CoverCam.Earth); }

        public static void Build(DisplayList dl, int w, int h, PageState s, MapView view,
                                 int selectedPhase, CoverCam cam)
        { Build(dl, w, h, s, view, selectedPhase, cam, Turntable.Front()); }

        /// <summary>As above, told where the capsule TURNTABLE is pointing (T11a, §5). Every other
        /// overload passes the front frame, which is what the view opens on and — until the glue
        /// carries a drag (T11b) — what it stays on.</summary>
        public static void Build(DisplayList dl, int w, int h, PageState s, MapView view,
                                 int selectedPhase, CoverCam cam, TurntableState turn)
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
            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame

            // the camera slot: the LIVE globe, the flat map, or the capsule. Drawn HERE, before the
            // placed assets, so the caption/readouts/bars in the loop below sit over it exactly as the
            // globe alone used to.
            DrawCameraView(dl, w, h, s, view, cam, turn);

            int sp = selectedPhase < 0 ? 0 : (selectedPhase >= PhaseCount ? PhaseCount - 1 : selectedPhase);
            bool refPhase = (sp == ReferencePhase);

            // every placed asset — anchored left/right of the split, bars stretched. rectangle_178 was
            // already drawn behind the bars above, so skip it here. The rail highlight (rectangle_183 +
            // rectangle_95) and the centre heading (coast_to_trunk_jettison) are DYNAMIC — the export
            // baked them onto one phase. The five baked rail rows (labels deport_burn…manual_chute + the
            // union_1…5 dots) are ALSO skipped: the rail is redrawn below as seven primitive rows. On the
            // Reference Content phase the baked panel-BODY captions (ReferenceSkipKeys) are swapped out
            // too — the three card backgrounds (rectangle_179/180/181) stay, their content is redrawn below.
            for (int i = 0; i < Keys.Length; i++)
            {
                string k = Keys[i];
                if (Array.IndexOf(SkipKeys, k) >= 0) continue;
                if (Array.IndexOf(AttitudeSkipKeys, k) >= 0) continue;
                if (refPhase && Array.IndexOf(ReferenceSkipKeys, k) >= 0) continue;
                if (cam != CoverCam.Earth && Array.IndexOf(EarthOnlyKeys, k) >= 0) continue;
                // S75: a glyph in InertKeys is painted but has no hit rect, so it is tinted OUT of this
                // page's white-glyph-means-button idiom rather than left to imply a touch it cannot take.
                Rgba tint = (Array.IndexOf(InertKeys, k) >= 0) ? InertTint : DragonPalette.White;
                dl.Asset(k, X(Box[i, 0]), Y(Box[i, 1]), Wd(Box[i, 0], Box[i, 2]), Z(Box[i, 3]), tint);
            }

            // the seven-item deorbit phase rail + the selected phase's highlight (shared verbatim with the
            // Manual Chute Deploy page via DrawRail), then the centre heading (Cover-specific).
            DrawRail(dl, w, h, sp);
            dl.Text(PhaseName[sp], X(490), Y(286), Z(58), TextAlign.Left, DragonPalette.White);

            // the two skipped interrupt-condition captions, redrawn as ATTITUDE (see AttitudeSkipKeys).
            // Not on the Reference Content phase: there the whole baked body is swapped out anyway.
            if (!refPhase) DrawAttitudeCriteria(dl, X, Y, Z);

            if (refPhase)
                DrawReferenceContent(dl, X, Y, Z);
            else
                // the hairlines, as crisp primitives at their measured positions — all ten are dividers
                // within the baked (non-Reference) panel body, so they are skipped on Reference Content.
                for (int i = 0; i < Lines.GetLength(0); i++)
                    dl.Line(X(Lines[i, 0]), Y(Lines[i, 2]), X(Lines[i, 1]), Y(Lines[i, 2]), St(2), DragonPalette.Text6);

            // the camera caption, the NEXT VIEW pill and (on the MAP view) its d-pad, over everything.
            DrawCameraChrome(dl, w, h, view, cam);
        }

        // ---- the three camera views ----------------------------------------------------------------

        /// <summary>Draw whichever of First.vue's three views is up, into the slot they share.</summary>
        static void DrawCameraView(DisplayList dl, int w, int h, PageState s, MapView view,
                                   CoverCam cam, TurntableState turn)
        {
            float sc = h / RefH; float extra = w - RefW * sc; if (extra < 0f) extra = 0f;

            if (cam == CoverCam.Map)
            {
                float mx, my, mw, mh;
                MapRect(w, h, out mx, out my, out mw, out mh);
                // The well first and always, so the panel has a shape before anything resolves inside
                // it - and so the map degrades to a graticule rather than to a hole if BodyMap does not
                // resolve. Same order, and the same reason, as NavPage.Build.
                dl.Rect(mx, my, mw, mh, DragonPalette.Inset2);
                NavPage.Map(dl, s, view, mx, my, mw, mh);
                dl.Box(mx, my, mw, mh, Stroke(sc, 2f), DragonPalette.Hairline);
                return;
            }

            if (cam == CoverCam.Capsule)
            {
                // The vehicle itself (First.vue's view-02). No longer the shipped dragon.png still:
                // T11a puts the §5 TURNTABLE SEQUENCE in the slot, one frame of it chosen by `turn`.
                DrawTurntable(dl, w, h, sc, turn);
                return;
            }

            // EARTH (First.vue's view-00): the live globe, circular, CENTRED in the space to the right
            // of the left content panel (panel right edge = 1442) and vertically centred between bars.
            float gs = 1809f * sc;
            float gcx = (ViewLeft * sc + w) * 0.5f;
            float gcy = (ViewTop + ViewBottom) * 0.5f * sc;
            NavPage.Planet(dl, s, view, gcx - gs * 0.5f, gcy - gs * 0.5f, gs, gs);
        }

        // ---- THE CAPSULE TURNTABLE (T11a, §5) ------------------------------------------------
        //
        // The sprite fills the same share of the slot the dragon.png still did (0.86 of its height,
        // centred), with one difference: while the sequence on disk is a PLACEHOLDER set, a strip is
        // reserved at the bottom of the slot for the label that says so, and the sprite is centred in
        // what is left. T11b's render half landed the real frames and cleared Turntable.Placeholder,
        // so the strip is zero today and the geometry is exactly what the still had — the branch is
        // kept because the marking mechanism is (see Turntable.Placeholder).
        const float CapsuleFill = 0.86f;              // of the available slot height
        const float CapsuleLabelStrip = 96f;          // design px, placeholder marking only

        /// <summary>Panel-pixel rect of the turntable sprite. ONE function for the draw and for
        /// T11b's gesture region below, which is PageAction's standing rule: a control drawn from
        /// one rectangle and hit from another drifts on first touch.</summary>
        public static void CapsuleRect(int w, int h, out float x, out float y,
                                       out float rw, out float rh)
        {
            x = y = rw = rh = 0f;
            if (w <= 0 || h <= 0) return;
            float sc = h / RefH;
            float strip = Turntable.Placeholder ? CapsuleLabelStrip : 0f;
            float cx = (ViewLeft * sc + w) * 0.5f;
            float cy = (ViewTop + ViewBottom - strip) * 0.5f * sc;
            float ih = (ViewBottom - ViewTop - strip) * sc * CapsuleFill;
            Turntable.FitHeight(cx, cy, ih, out x, out y, out rw, out rh);
        }

        /// <summary>
        /// Did this touch land on the capsule — i.e. does it start a turntable gesture (T11b)? The
        /// region is the SPRITE, from CapsuleRect, not the whole camera slot: what the crew can grab
        /// is what they can see, and the slot around it is empty background that the globe and the
        /// map also use. Only on the Capsule view, so a touch on the globe or the map can never turn
        /// a vehicle that is not being drawn.
        ///
        /// Tested LAST by the painter, after the rail, the pill and the map cluster: the capsule is
        /// the biggest thing on the page and a control that overlapped it would otherwise be eaten.
        /// </summary>
        public static bool CapsuleHit(float px, float py, int w, int h, CoverCam cam)
        {
            if (cam != CoverCam.Capsule) return false;
            float x, y, rw, rh;
            CapsuleRect(w, h, out x, out y, out rw, out rh);
            if (rw <= 0f || rh <= 0f) return false;
            return Control.Hit(px, py, x, y, rw, rh);
        }

        /// <summary>The turntable: one frame of art/cover/dragon_turn_NNN.png, plus — only while the
        /// shipped sequence is the stand-in set — the label that marks it as one. §1.4: a stand-in
        /// that is not labelled is an invented source, so the marking is drawn by the same code that
        /// draws the sprite and disappears with it.</summary>
        static void DrawTurntable(DisplayList dl, int w, int h, float sc, TurntableState turn)
        {
            float ix, iy, iw, ih;
            CapsuleRect(w, h, out ix, out iy, out iw, out ih);
            if (iw <= 0f || ih <= 0f) return;

            dl.Asset(Turntable.KeyOf(turn), ix, iy, iw, ih, DragonPalette.White);

            if (!Turntable.Placeholder) return;

            int frame = Turntable.FrameOf(turn);
            float lx = ix + iw * 0.5f, ly = iy + ih + 26f * sc;
            dl.Text(Turntable.PlaceholderLabel, lx, ly, 30f * sc, TextAlign.Centre,
                    DragonPalette.Caution);
            dl.Text("FRAME " + frame + " / " + Turntable.Count
                    + "   AZ " + (int)Turntable.AngleOf(frame) + " DEG",
                    lx, ly + 38f * sc, 26f * sc, TextAlign.Centre, DragonPalette.Text5);
        }

        /// <summary>The CAMERA caption + heading, the NEXT VIEW pill, and - on the MAP view only - the
        /// NavEarth pan/centre/zoom cluster. Drawn after the placed assets so nothing covers them.</summary>
        static void DrawCameraChrome(DisplayList dl, int w, int h, MapView view, CoverCam cam)
        {
            float sc = h / RefH; float extra = w - RefW * sc; if (extra < 0f) extra = 0f;
            float X(float v) => v * sc + extra;      // every camera control lives right of the Split
            float Y(float v) => v * sc;
            float Z(float v) => v * sc;

            // CAMERA / <heading>, at the baked asset's own measured metrics (see CamCx and friends).
            dl.Text("CAMERA", X(CamCx), Y(CamCapY), Z(CamCapSize), TextAlign.Centre, DragonPalette.White);
            dl.Text(CamHeading(cam), X(CamCx), Y(CamHeadY), Z(CamHeadSize), TextAlign.Centre,
                    DragonPalette.White);

            // NEXT VIEW, built as the SETTINGS pill's twin: the dash icon (ic_sharp-subtract sits at
            // rectangle_174 + 30,28 in a 56 box) then the label (the `settings` asset at +130,37).
            float px, py, pw, ph;
            NextViewRect(w, h, out px, out py, out pw, out ph);
            Pill(dl, px, py, pw, ph, Stroke(sc, 2f));
            dl.Rect(px + Z(36f), py + Z(53f), Z(44f), Stroke(sc, 6f), DragonPalette.White);
            dl.Text("NEXT VIEW", px + Z(130f), py + Z(32f), Z(53f), TextAlign.Left, DragonPalette.White);

            if (cam != CoverCam.Map) return;

            // NavEarth's cluster. Words, not glyph arrows, for NavPage's reason: the font is whatever
            // the OS resolved and a triangle drawn from rects is not a triangle.
            PadButton(dl, w, h, 0, -1, "UP", sc, false);
            PadButton(dl, w, h, -1, 0, "LEFT", sc, false);
            // CTR lights while the map is FOLLOWING the vehicle, as NAV's does: whether the map is
            // tracking or has been panned off by hand is otherwise only learnable by watching it drift.
            PadButton(dl, w, h, 0, 0, "CTR", sc, view.Follow);
            PadButton(dl, w, h, 1, 0, "RIGHT", sc, false);
            PadButton(dl, w, h, 0, 1, "DOWN", sc, false);
            PadButton(dl, w, h, -0.5f, 2, "+", sc, false);   // NavEarth puts ZOOM IN on the left
            PadButton(dl, w, h, 0.5f, 2, "-", sc, false);

            float zx, zy, zw, zh;
            PadRect(w, h, 0, 2, out zx, out zy, out zw, out zh);
            dl.Text("ZOOM x" + (1 << (view.ZoomStep < 0 ? 0 : view.ZoomStep > MapProjection.MaxZoom
                                      ? MapProjection.MaxZoom : view.ZoomStep)),
                    zx + zw * 0.5f, zy + zh + Z(10f), Z(30f), TextAlign.Centre, DragonPalette.Text3);
        }

        /// <summary>A pill exactly like the Figma's SETTINGS button (rectangle_174) and the reference's
        /// own #swap-view: Panel fill, white 1px edge, fully rounded ends. There is no rounded-rect
        /// primitive, so the caps are ArcBands and the middle is a rect.</summary>
        static void Pill(DisplayList dl, float x, float y, float pw, float ph, float stroke)
        {
            float r = ph * 0.5f, cy = y + r;
            if (pw < ph) pw = ph;
            dl.ArcBand(x + r, cy, 0f, r, 180.0, 360.0, DragonPalette.Panel);          // left cap
            dl.ArcBand(x + pw - r, cy, 0f, r, 0.0, 180.0, DragonPalette.Panel);       // right cap
            dl.Rect(x + r, y, pw - 2f * r, ph, DragonPalette.Panel);
            dl.ArcBand(x + r, cy, r - stroke, r, 180.0, 360.0, DragonPalette.White);
            dl.ArcBand(x + pw - r, cy, r - stroke, r, 0.0, 180.0, DragonPalette.White);
            dl.Rect(x + r, y, pw - 2f * r, stroke, DragonPalette.White);
            dl.Rect(x + r, y + ph - stroke, pw - 2f * r, stroke, DragonPalette.White);
        }

        /// <summary>One cluster button: NavEarth's `border: 1px solid white; background: rgba(2,7,56,.75)`
        /// square, with the label centred on its cap height.</summary>
        static void PadButton(DisplayList dl, int w, int h, float dx, float dy, string label,
                              float sc, bool on)
        {
            float x, y, bw, bh;
            PadRect(w, h, dx, dy, out x, out y, out bw, out bh);
            dl.Rect(x, y, bw, bh, on ? DragonPalette.Accent : PadFace);
            dl.Box(x, y, bw, bh, Stroke(sc, 2f), DragonPalette.White);
            float ts = PadLabel * sc;
            dl.Text(label, x + bw * 0.5f, y + bh * 0.5f - ts * 0.45f, ts, TextAlign.Centre,
                    on ? DragonPalette.Background : DragonPalette.White);
        }

        /// <summary>A design-frame stroke width in panel pixels, never thinner than one.</summary>
        static float Stroke(float sc, float refPx)
        {
            float t = refPx * sc;
            return (t < 1f) ? 1f : t;
        }

        // ---- THE THREE REFERENCE-CONTENT CARD SLOTS, AND THE TYPE THAT HAS TO FIT IN THEM ----
        // The slots are the baked card BACKGROUNDS (rectangle_179/180/181) and their measured heights are
        // very unequal — 317 / 449 / 550 — while the densest list, the seven-step ENTRY TIMELINE, sits in
        // the SHORTEST one. At the design row pitch its last row overhung the card by 13 design units and
        // rendered half on the panel, half on the page ground (QC-AUDIT 2026-09-03, finding 6). FitRows
        // scales a block to its own slot instead, so no card can overflow when a row is added later.
        public const float RowTop = 56f, RowSize = 26f, RowPad = 12f;
        static readonly float Card1Bottom = BoxOf("rectangle_179", 1) + BoxOf("rectangle_179", 3);
        static readonly float Card2Bottom = BoxOf("rectangle_180", 1) + BoxOf("rectangle_180", 3);
        static readonly float Card3Bottom = BoxOf("rectangle_181", 1) + BoxOf("rectangle_181", 3);

        /// <summary>Fit `count` rows starting at `top` inside a slot ending at `slotBottom`. Hands back
        /// the wanted size/pitch untouched when the block already fits; otherwise scales BOTH by the same
        /// factor, so the block keeps its proportions rather than just crushing the leading. Type never
        /// goes below Typography.Min (the measured legibility floor) and rows never overlap — a slot too
        /// short for one legible line overflows visibly instead of turning to mush.</summary>
        public static void FitRows(float top, float slotBottom, int count, float wantSize, float wantGap,
                                   out float size, out float gap)
        {
            size = wantSize; gap = wantGap;
            if (count < 1) return;
            float avail = slotBottom - RowPad - top;
            float need = wantGap * (count - 1) + wantSize;
            if (avail <= 0f || need <= avail) return;
            float k = avail / need;
            size = wantSize * k; gap = wantGap * k;
            if (size < Typography.Min)
            {
                size = Typography.Min;
                gap = (count > 1) ? (avail - size) / (count - 1) : wantGap;
            }
            if (gap < size) gap = size;
        }

        /// <summary>The two Crew Interrupt Conditions captions, drawn as primitives because their baked
        /// PNGs read "altitude" and S13 settled the quantity as ATTITUDE. The strings are
        /// DeorbitBurnPrepPage's own S13-corrected ones, so the two surfaces that state this criterion
        /// now read identically (C7.1). Geometry is the baked assets' own measured box (Keys/Box rows
        /// `30deg_sustained_altitude_error` and `600deg_m_altitude_rate`), so the rows stay on the
        /// hairlines and keep their FAR FIELD POINTING values aligned.</summary>
        static void DrawAttitudeCriteria(DisplayList dl, Func<float, float> X, Func<float, float> Y, Func<float, float> Z)
        {
            dl.Text("30° sustained attitude error", X(AttX), Y(AttY1), Z(AttSize), TextAlign.Left,
                    DragonPalette.White);
            dl.Text("600°/min attitude rate", X(AttX), Y(AttY2), Z(AttSize), TextAlign.Left,
                    DragonPalette.White);
        }

        /// <summary>The deorbit quick-reference (§14.4(c)): entry timeline, parachutes, contingency — all
        /// real §8/§4 flight facts, laid out in the three real card slots (rectangle_179/180/181) the baked
        /// export used for the Coast-phase body. Drawn only when the Reference Content rail item (index 5)
        /// is selected.</summary>
        static void DrawReferenceContent(DisplayList dl, Func<float, float> X, Func<float, float> Y, Func<float, float> Z)
        {
            void Card(float titleY, float slotBottom, string title, string[] lines, float spacing)
            {
                float size, gap;
                FitRows(titleY + RowTop, slotBottom, lines.Length, RowSize, spacing, out size, out gap);
                dl.ArcBand(X(333), Y(titleY + 28), Z(4), Z(9), 0, 360, DragonPalette.Accent);
                dl.Text(title, X(362), Y(titleY), Z(34), TextAlign.Left, DragonPalette.White);
                float ry = titleY + RowTop;
                for (int i = 0; i < lines.Length; i++)
                {
                    dl.Text(lines[i], X(340), Y(ry), Z(size), TextAlign.Left, DragonPalette.Text2);
                    ry += gap;
                }
            }

            // Return/deorbit sequence — §8 "Return/deorbit". Times are the ones §8 actually gives; no
            // invented numbers (§1.4).
            Card(499f, Card1Bottom, "ENTRY TIMELINE", new[] {
                "Undock → trunk jettison",
                "Deorbit burn — ~15 min",
                "Claw separation — ~1 h 20 m before splashdown",
                "Nose cone close & lock",
                "Entry interface",
                "Drogues, then mains at ~2 km",
                "Splashdown — T+50 min from burn start" }, 32f);

            // §8 "Parachutes (Mark 3)".
            Card(848f, Card2Bottom, "PARACHUTES (MARK 3)", new[] {
                "2 drogues deploy first",
                "4 mains deploy at ~2 km",
                "Land under ≥ 3 mains",
                "CUT MAINS after splashdown" }, 40f);

            // Contingency / abort notes — the CONFIRMED-real panel functions (§4) + the §8 deorbit
            // go/no-go timing.
            Card(1329f, Card3Bottom, "CONTINGENCY", new[] {
                "EJECT — SuperDraco abort (8 modes)",
                "WATER DEORBIT / DEORBIT NOW — contingency immediate deorbit",
                "Water landing is the norm — 7 designated splashdown sites",
                "Deorbit go/no-go — ~30 min before claw-sep prep" }, 40f);
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
            Settings, ActOnSpaceX, ActDeorbitBrief, ActReview, ActAcknowledge, EntryTrue, EntryFalse,
            // T4, the camera: NEXT VIEW is on every view; the cluster belongs to the MAP view alone.
            NextView,
            MapPanUp, MapPanDown, MapPanLeft, MapPanRight, MapCentre, MapZoomIn, MapZoomOut
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

        // ---- S54 / audit H8: THE SIX ROWS THAT ARE NOT DRAWN ON THE REFERENCE CONTENT PHASE ----
        // `Build` swaps the whole baked panel BODY out on rail slot 5 (`refPhase` → `ReferenceSkipKeys`)
        // and draws the deorbit quick-reference over that space instead. Six of the `Hits` rows below are
        // labels inside that swapped-out body, so on slot 5 they are INVISIBLE — but the rectangles were
        // unconditional, and fired over the ENTRY TIMELINE / PARACHUTES / CONTINGENCY text.
        // ⛔ IT IS HARMLESS ONLY WHILE THE TARGETS ARE NO-OPS. The moment the Cover action buttons are
        // wired (audit H5), a tap on the reference text would trigger deorbit actions — which is why H8
        // says fix this FIRST. Each entry names the `ReferenceSkipKeys` key that suppresses its label, so
        // the two lists can be checked against each other by eye.
        static bool HiddenOnReferencePhase(CoverButton b)
        {
            return b == CoverButton.ActOnSpaceX        // "on_spacex_on_begin_procedure_4_700"
                || b == CoverButton.ActDeorbitBrief    // "deorbit_burn_brief"
                || b == CoverButton.ActReview          // "review_reference_content"
                || b == CoverButton.ActAcknowledge     // "acknowledge"
                || b == CoverButton.EntryTrue          // "true"  (with "entry_enabled")
                || b == CoverButton.EntryFalse;        // "false" (with "entry_enabled")
        }

        /// <summary>Passed as `selectedPhase` when the caller genuinely has no phase to give. It is NOT
        /// the Reference Content phase, so every row stays live — the behaviour these overloads had
        /// before S54. Only a caller that can actually dispatch an action needs to pass the real phase.</summary>
        public const int NoPhase = -1;

        /// <summary>Which cover-page button a touch at panel pixel (px,py) hit — None if it missed.
        /// Uses the SAME Fit map as Build, inverted, so the touch lands on the exact Figma rectangle.</summary>
        public static CoverButton HitTest(float px, float py, int w, int h)
        { return HitTest(px, py, w, h, CoverCam.Earth, NoPhase); }

        /// <summary>As above, told which camera view is up: the pan/centre/zoom cluster exists only
        /// while the MAP view is, so it must not be hit-testable behind the globe or the capsule.</summary>
        public static CoverButton HitTest(float px, float py, int w, int h, CoverCam cam)
        { return HitTest(px, py, w, h, cam, NoPhase); }

        /// <summary>The full test: which camera view is up AND which rail phase is selected. The phase
        /// matters because the Reference Content phase (slot 5) replaces the panel body, so the six rows
        /// `HiddenOnReferencePhase` names are not on the glass and must not be touchable (S54 / H8).
        /// A control the crew cannot see must never fire.</summary>
        public static CoverButton HitTest(float px, float py, int w, int h, CoverCam cam, int selectedPhase)
        {
            float sc = h / RefH;
            if (sc <= 0f) return CoverButton.None;
            float extra = w - RefW * sc; if (extra < 0f) extra = 0f;

            // NEXT VIEW first: it is on every view, it is how the crew gets out of any of them, and it
            // must not be shadowed by anything drawn later. (The same "chrome first" rule the bottom bar
            // gets in FigmaUI.HitTest.)
            float bx, by, bw, bh;
            NextViewRect(w, h, out bx, out by, out bw, out bh);
            if (Control.Hit(px, py, bx, by, bw, bh)) return CoverButton.NextView;

            if (cam == CoverCam.Map)
            {
                PadRect(w, h, 0, 0, out bx, out by, out bw, out bh);
                if (Control.Hit(px, py, bx, by, bw, bh)) return CoverButton.MapCentre;
                PadRect(w, h, 0, -1, out bx, out by, out bw, out bh);
                if (Control.Hit(px, py, bx, by, bw, bh)) return CoverButton.MapPanUp;
                PadRect(w, h, 0, 1, out bx, out by, out bw, out bh);
                if (Control.Hit(px, py, bx, by, bw, bh)) return CoverButton.MapPanDown;
                PadRect(w, h, -1, 0, out bx, out by, out bw, out bh);
                if (Control.Hit(px, py, bx, by, bw, bh)) return CoverButton.MapPanLeft;
                PadRect(w, h, 1, 0, out bx, out by, out bw, out bh);
                if (Control.Hit(px, py, bx, by, bw, bh)) return CoverButton.MapPanRight;
                PadRect(w, h, -0.5f, 2, out bx, out by, out bw, out bh);
                if (Control.Hit(px, py, bx, by, bw, bh)) return CoverButton.MapZoomIn;
                PadRect(w, h, 0.5f, 2, out bx, out by, out bw, out bh);
                if (Control.Hit(px, py, bx, by, bw, bh)) return CoverButton.MapZoomOut;
            }
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

            // The same clamp `Build` applies to `selectedPhase`, so the hit map and the drawing agree on
            // which phase is up even for an out-of-range index — they must never disagree about slot 5.
            bool refPhase = selectedPhase != NoPhase
                && (selectedPhase < 0 ? 0 : (selectedPhase >= PhaseCount ? PhaseCount - 1 : selectedPhase))
                   == ReferencePhase;

            for (int i = 0; i < Hits.GetLength(0); i++)
            {
                CoverButton b = (CoverButton)Hits[i, 0];
                if (refPhase && HiddenOnReferencePhase(b)) continue;   // S54: not drawn → not touchable
                if (fx >= Hits[i, 1] && fx < Hits[i, 1] + Hits[i, 3] &&
                    fy >= Hits[i, 2] && fy < Hits[i, 2] + Hits[i, 4])
                    return b;
            }
            return CoverButton.None;
        }
    }
}
