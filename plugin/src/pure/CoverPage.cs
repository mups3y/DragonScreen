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
        // ⭐ S129 / QC C-08 ADDS `true` AND `false` TO THIS LIST. They were the ENTRY ENABLED row's two
        // value glyphs, and the exported art carries the SELECTION as well as the words - `false` is set
        // heavier and its box is bigger for that reason alone. So the page answered a safety question
        // from a picture, permanently, on every phase. They are redrawn as text from a computed verdict
        // (DrawEntryVerdict), at the assets' own measured boxes, which is C-01's method. The CAPTION
        // asset `entry_enabled` is NOT skipped - it is a label and says nothing about state.
        static readonly string[] SkipKeys = {
            "true", "false",
            // ---- S131 / QC C-02: `bi_arrow_right_short` IS DROPPED, BY OWNER DECISION ----
            // A 16x16 glyph with TWELVE OPAQUE PIXELS, placed by masked template match against
            // Frame 67.png - the smallest, lowest-information target in the whole set, and exactly the
            // case where a template match returns a false peak. It did: design x 1706 is 264 px right
            // of the content panel's own right edge (`rectangle_178` ends at 1442) and 336 px right of
            // the `deorbit_burn_brief` row it plainly belongs to. The fill-to-fit reflow then reads
            // 1706 as right of Split and shifts it further, landing it at panel (1414, 698) - dead
            // centre of the LIVE camera slot, over the globe, on every Cover render but phase 5.
            //
            // It is also PURE BLACK ink (RGB 0,0,0) where every comparable glyph on this page is pure
            // white, and it is drawn with a White tint - and a white multiply cannot lift black. So at
            // its CORRECT position it would have been invisible on #020738. It was visible only
            // because it landed on a photograph.
            //
            // 🟢 OWNER DECISION, Q1: option selected "Drop it" (2026-09-05, via the overseer).
            // ⛔ RECORDED AS A SELECTION, NOT A VERBATIM QUOTE - he chose from presented options and
            // did not write the words (C1.12's evidentiary standard; QC's own convention).
            // ⚠ QC's fix plan offered a REPLACEMENT (redraw it as primitives at the row's own metrics)
            // and left the PLACEMENT to §1.4 because guessing "just after x 1370" invents a layout
            // bound. The owner's answer removes that question rather than answering it.
            //
            // ⛔ THE Keys/Box ROWS STAY. They are index-paired and every other placement is measured
            // against them, so deleting a row would shift 12 boxes for no gain. The asset PNG stays on
            // disk too - `assets/` is reference (C7.1) and this file's placement being wrong says
            // nothing about the art. It is simply never drawn.
            // ⚠ Its `ReferenceSkipKeys` entry ALSO stays, deliberately, though this line makes it
            // redundant: QC's own must-not-break says "ReferenceSkipKeys must keep suppressing it on
            // phase 5", and the two lists are checked against each other by eye (see S54 / H8).
            "bi_arrow_right_short",
            "rectangle_178", "rectangle_183", "rectangle_95", "coast_to_trunk_jettison",
            "deport_burn", "coast_to_trunk", "claw_separati", "procedure", "manual_chute",
            "union_1", "union_2", "union_3", "union_4", "union_5", "camera_auto_earth_io",
            // ---- S105 / QC C-01: THE TOP TELEMETRY STRIP IS NO LONGER SOMEONE ELSE'S FLIGHT ----
            // These seven were baked PNGs of one particular descent — ALTITUDE 393.3km, APOGEE 416.2km,
            // INERTIAL VELOCITY 7.69km/s and so on — on the page the crew opens on. Six of the seven
            // contradicted the PageState the same frame's globe was drawn from (QC C-01 has the table).
            // Every one exists live and pre-formatted, and ManualChuteDeployPage — which shares this
            // page's own rail — has drawn the same seven live since T13c. They are placed as text at the
            // baked assets' OWN measured boxes now; see DrawTopStrip.
            // ⚠ `running_00_22_57` is NOT in this list and stays baked, deliberately — see DrawTopStrip.
            "active_phase_deorbit_coast", "splashdown_time_t_01_24_51", "inertial_velocity_7_69km_s",
            "altitude_393_3km", "apogee_416_2km", "perigee_379_4km", "inclination_51_62deg" };

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
        // S106 / QC C-06: `rectangle_182` joins it. That asset is a 15x920 SCROLLBAR THUMB whose right
        // edge is 1442 - the content panel's inner edge exactly - fixed length, fixed position, no hit
        // rect, no scroll model, and a panel whose content never overflows. A thumb's LENGTH says "there
        // is more below" and its POSITION says "you are here", and here both were lies. It was taking the
        // White tint, i.e. its baked lavender at full strength (93,104,164), which is the same tint every
        // live glyph on the page takes; the inert tint renders it (48,56,105), plainly dimmer than the
        // panel's white hairlines - the relationship `gridicons_refresh` already has.
        // It takes S75's branch rather than a rect because what it would scroll is not settled (C-05).
        static readonly string[] InertKeys = { "gridicons_refresh", "rectangle_182" };

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
            // ---- S105 / QC C-13: NEXT VIEW IS SETTINGS' MIRROR, NOT A LEFTOVER OF THE REFLOW ----
            // Owner, 2026-09-05, verbatim: *"next button should also be moved to look like it belongs"*
            // and *"I like well balanced layouts"*. It used to sit at `NextX * sc + extra` — NextX is
            // 1500, EXACTLY the reflow Split, so the pill took the full horizontal slack and landed 296
            // design px right of where it balances. SETTINGS' own right margin is 32 design px (its box
            // ends at 3395 in a 3427 frame), so the mirror is the camera slot's left edge plus the same
            // 32: `(ViewLeft + 32) * sc`, with no `extra` because it is left of the Split. The two pills
            // are then the same size, on the same row, at the same inset from their own ends of the slot.
            x = (ViewLeft + NextInset) * sc; y = NextY * sc; rw = NextW * sc; rh = NextH * sc;
        }

        /// <summary>SETTINGS' own margin from the frame edge (rectangle_174 ends at 3395 of 3427), which
        /// is what NEXT VIEW mirrors at the other end of the slot.</summary>
        const float NextInset = 32f;

        /// <summary>
        /// Half the gap between the two TARGET readouts, in design px, measured about the camera slot's
        /// own centre — which is also the globe's centre, so the pair is symmetric about the thing it
        /// sits under (QC C-13; owner: *"The coordinates bellow the map should be evenly spaced either
        /// side of the globe so they do not overrun the globe"*).
        ///
        /// 475 puts each readout in the middle of the clear span between its pill and the globe's foot:
        /// at the shipped 1280x703 the disc's half-width at the readouts' own row is 62.5 px, so the
        /// blocks land 54 px clear of the globe on the left and 49 px clear on the right, with 56 and 51
        /// to the pills. ⚠ The residual few px is the two BAKED PNGs being different widths (251 vs 280
        /// design px) — it closes only when they become live text, which is a different finding (S49 H3:
        /// both currently read "26° 15.00° N", and the longitude carries a latitude's N).
        /// </summary>
        /// ⚠ S118, 2026-09-06 — ADDED, nothing above changed. Every figure in the paragraph above was
        /// measured at 1280x703; the shipped panel has been 2560x1406 since S115 (2026-09-05). The
        /// same five quantities there:
        ///
        ///     disc half-width at the readouts' row   62.5 px  ->  125.0 px
        ///     clear of the globe, left / right       54 / 49  ->   108 / 98
        ///     clear to the pills, left / right       56 / 51  ->   112 / 102
        ///
        /// ⭐ EXACTLY doubled, not doubled by eye. The two shipped panels are exactly 2:1 (2560 =
        /// 2x1280, 1406 = 2x703) and this page is uniformly height-scaled with its horizontal slack
        /// `extra = w - RefW*sc` doubling too, so every coordinate and size it emits doubles. That is
        /// asserted over all 166 of this page's draw commands by
        /// LegibilityFloorTest.TheShippedPanelsAreExactlyTwoToOne, not by re-deriving these five.
        /// The RESIDUAL the note above describes is a ratio between two baked PNGs, so it is
        /// unchanged as an argument and merely twice as many pixels.
        const float ReadoutHalfGap = 475f;

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
            int St(float rs) => Strokes.Px(rs, sc);   // ONE rule, in Strokes.cs - rounds UP (R-02 family)

            dl.Rect(0, 0, w, h, DragonPalette.Background);

            // content panel border FIRST — in the Figma it runs a few px under the top + bottom bars, so
            // those (drawn later: rectangle_173 in the loop, component_48 right below) cover its overhang.
            dl.Asset("rectangle_178", X(218), Y(216), Wd(218, 1224), Z(1779), DragonPalette.White);

            // bottom status bar (Component 48: bg + CURRENT STATE / POINTING MODE / SPX·TDRS·ISS text) — full width
            BottomBar.Draw(dl, w, h, s);   // S103: undistorted, in the design frame; S147: CURRENT STATE live

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
                // S105/C-13: the two TARGET readouts are placed by DrawCameraChrome now, centred on
                // the slot rather than at their baked x, so the loop never draws them.
                if (Array.IndexOf(EarthOnlyKeys, k) >= 0) continue;
                // S75: a glyph in InertKeys is painted but has no hit rect, so it is tinted OUT of this
                // page's white-glyph-means-button idiom rather than left to imply a touch it cannot take.
                Rgba tint = (Array.IndexOf(InertKeys, k) >= 0) ? InertTint : DragonPalette.White;
                dl.Asset(k, X(Box[i, 0]), Y(Box[i, 1]), Wd(Box[i, 0], Box[i, 2]), Z(Box[i, 3]), tint);
            }

            // the seven-item deorbit phase rail + the selected phase's highlight (shared verbatim with the
            // Manual Chute Deploy page via DrawRail), then the centre heading (Cover-specific).
            // the seven top-bar telemetry values, live, over the boxes their baked PNGs used (S105/C-01)
            DrawTopStrip(dl, X, Y, Z, s);

            DrawRail(dl, w, h, sp);
            dl.Text(PhaseName[sp], X(490), Y(286), Z(58), TextAlign.Left, DragonPalette.White);

            // the two skipped interrupt-condition captions, redrawn as ATTITUDE (see AttitudeSkipKeys).
            // Not on the Reference Content phase: there the whole baked body is swapped out anyway.
            if (!refPhase) DrawAttitudeCriteria(dl, X, Y, Z);

            // S129: the ENTRY ENABLED verdict, over the boxes its two baked PNGs used. Not on the
            // Reference Content phase - the whole baked body including this row is swapped out there.
            if (!refPhase) DrawEntryVerdict(dl, X, Y, Z, w, sc, s);

            if (refPhase)
                // w and sc go through so FitRows can compare the legibility floor in the SAME units as
                // the size it is clamping (S116). Z alone is not enough: it converts design -> panel,
                // and the floor needs the conversion the other way, plus the panel width MinFor needs.
                DrawReferenceContent(dl, X, Y, Z, w, sc);
            else
                // the hairlines, as crisp primitives at their measured positions — all ten are dividers
                // within the baked (non-Reference) panel body, so they are skipped on Reference Content.
                for (int i = 0; i < Lines.GetLength(0); i++)
                    dl.Line(X(Lines[i, 0]), Y(Lines[i, 2]), X(Lines[i, 1]), Y(Lines[i, 2]), St(2), DragonPalette.Text6);

            // the camera caption, the NEXT VIEW pill and (on the MAP view) its d-pad, over everything.
            DrawCameraChrome(dl, w, h, view, cam, s);
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
                // S117 / QC R-02: the map's own markers, labels and track weight are RefPanelW
                // sizes, so they need this panel's type scale — not the Cover's design-frame `sc`,
                // which is px-per-design-px and a different quantity entirely.
                NavPage.Map(dl, s, view, mx, my, mw, mh, Typography.ScaleFor(w));
                dl.Box(mx, my, mw, mh, Strokes.Px(2f, sc), DragonPalette.Hairline);
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
            NavPage.Planet(dl, s, view, gcx - gs * 0.5f, gcy - gs * 0.5f, gs, gs, Typography.ScaleFor(w));
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
        static void DrawCameraChrome(DisplayList dl, int w, int h, MapView view, CoverCam cam,
                                     PageState s)
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
            Pill(dl, px, py, pw, ph, Strokes.Px(2f, sc));
            // S105/C-03: the dash moves in with the label (36 -> 30) so the cluster stays balanced
            // inside the pill rather than the label being pushed up against the right border.
            dl.Rect(px + Z(30f), py + Z(53f), Z(44f), Strokes.Px(6f, sc), DragonPalette.White);
            // ---- S105 / QC C-03: THE LABEL FITS ITS OWN BUTTON NOW ----
            // It was `Z(130)` in and `Z(53)` tall, and at the shipped 1280x703 that is 96 px of glyph in
            // 90.2 px of room — the pill's right border struck through the final "W" (verified as a real
            // overrun, not S101's hairline dropout: the border renders at x 770-772 and the W's two stems
            // straddle it at 764-765 and 777-778).
            // ⚠ QC's filed fix was "set it at the SETTINGS twin's own 37" — and that is WRONG at the
            // shipped width: 37 design px is 12.3 panel px, below `Typography.Min` = 16, so it would
            // trade an overrun for an unreadable label. The 130 inset is SETTINGS' own, and SETTINGS'
            // label is 140 design px wide where this one is ~288; the inset is the lever the filed plan
            // missed. 110 in at 50 tall keeps the label inside the pill AND at 16.6 panel px, above the
            // floor — the twin geometry gives a little, the legibility does not.
            // ⚠ S118 + R-02, 2026-09-06 — ADDED, nothing above changed. Those figures were measured at
            // 1280x703 and the shipped panel is 2560x1406 (Q5 / S115). Measured at both:
            //
            //     quantity                    @1280x703       @2560x1406     the floor there
            //     the pill's width             133.5 px         267.0 px          —
            //     room after the 110 inset      96.9 px         193.7 px          —
            //     the label, Z(50)              16.6 panel px    33.3 panel px   16 / 32
            //     QC's rejected 37              12.3 panel px    24.6 panel px   16 / 32
            //
            // ⭐ AND EVERY VERDICT ABOVE SURVIVES, WHICH IS THE POINT. The label clears the floor by
            // the same ~4% at both widths and QC's 37 falls short by the same margin at both, because
            // the floor is `Typography.MinFor(panelW)` and scales with them (R-02, landed 2026-09-06).
            // "Below Typography.Min = 16" above should now be read as "below the floor, which is 16
            // AT 1280" - the number moved, the verdict did not. Raising the resolution buys crispness,
            // never legibility; Typography's header says so in as many words.
            dl.Text("NEXT VIEW", px + Z(102f), py + Z(34f), Z(50f), TextAlign.Left, DragonPalette.White);

            // ---- S105 / QC C-13: THE TWO TARGET READOUTS, SYMMETRIC ABOUT THE GLOBE ----
            // They were placed at their baked design x (2014 and 2361) and then pushed right by the
            // reflow, so TARGET LATITUDE sat with 88% of its width over the globe's foot and TARGET
            // LONGITUDE with 17%. They are drawn HERE now, centred on the camera slot's own centre
            // ± ReadoutHalfGap — the same centre the globe is drawn about — so the pair is symmetric
            // about the thing it sits under and both clear the disc. Earth view only, as before:
            // the flat map and the capsule do not plot a ground target (First.vue's `v-if`).
            if (cam == CoverCam.Earth)
            {
                // ---- S126 / S49 H3 / QC C-14: THEY ARE LIVE TEXT NOW, NOT TWO PICTURES ----
                // ⛔ WHAT WAS WRONG IS WORSE THAN "BAKED". The two assets are
                // `target_latitude_26deg_15_00deg_n` and `target_longitude_26deg_15_00deg_n` - the
                // key names carry it - so BOTH readouts printed the SAME string, and the LONGITUDE
                // one therefore showed a latitude's value with a LATITUDE'S HEMISPHERE LETTER. A
                // longitude cannot be "N". It was a wrong reading, not just a frozen one.
                //
                // ⭐ ROUTE (i), AND CHOOSING WAS THE RESEARCH (H3 named two). The live nav target is
                // already on PageState - `HasTargetGround`, `TargetLatText`, `TargetLonText`, filled
                // at `VesselData.cs:504-520` from the vessel's own target and ALREADY formatted with
                // N/S and E/W - and `NavPage` reads the same fields, so the two surfaces cannot
                // disagree about where the target is (C7.1).
                // ⛔ ROUTE (ii) WAS REJECTED ON A SOURCE, NOT ON EFFORT: a SPLASHDOWN predictor would
                // need the seven real splashdown sites, and §B11 O7 records that they have NO
                // PUBLISHED COORDINATES. That readout could only ever be modelled, never sourced
                // (§1.4), and these labels say TARGET - not SPLASHDOWN - so the target is what they
                // get. If the owner later wants a splashdown site here, it is a different readout
                // with a different label and a §1.4 conversation of its own.
                //
                // ⚠ TWO SIZES, BY [[S153]]'s POLICY, AND THE SPLIT IS THE RULING'S OWN EXAMPLE.
                // The VALUE is live -> `MinDesignFor`. The CAPTION is a static label -> the ruling
                // names "pad captions" as static reference, so `DenseDesignFor`. Together they are
                // 36.05 + 48.07 = 84.1 design px against the baked box's 90 - it fits, measured, and
                // the pair is centred on that box so the row sits where the reference put it.
                //
                // The C-13 geometry ([[S105]]) is untouched: still `slotCx ± ReadoutHalfGap`, still
                // Earth-view only (the flat map and the capsule plot no ground target - First.vue's
                // own `v-if`). Only the ink changed.
                float slotCx = (ViewLeft * sc + w) * 0.5f;
                float capSz = Typography.DenseDesignFor(w, sc), valSz = Typography.MinDesignFor(w, sc);
                float boxTop = BoxOf(EarthOnlyKeys[0], 1), boxH = BoxOf(EarthOnlyKeys[0], 3);
                float top = boxTop + (boxH - (capSz + valSz)) * 0.5f;
                bool have = s.Valid && s.HasTargetGround;
                string[] cap = { "TARGET LATITUDE", "TARGET LONGITUDE" };
                string[] val = { have ? s.TargetLatText : Dashes.None,
                                 have ? s.TargetLonText : Dashes.None };
                for (int i = 0; i < 2; i++)
                {
                    float cx = slotCx + (i == 0 ? -Z(ReadoutHalfGap) : Z(ReadoutHalfGap));
                    dl.Text(cap[i], cx, Y(top), Z(capSz), TextAlign.Centre, DragonPalette.Text3);
                    dl.Text(val[i], cx, Y(top + capSz), Z(valSz), TextAlign.Centre,
                            have ? DragonPalette.White : DragonPalette.Text6);
                }
            }

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
            dl.Box(x, y, bw, bh, Strokes.Px(2f, sc), DragonPalette.White);
            float ts = PadLabel * sc;
            dl.Text(label, x + bw * 0.5f, y + bh * 0.5f - ts * 0.45f, ts, TextAlign.Centre,
                    on ? DragonPalette.Background : DragonPalette.White);
        }

        // ---- ⚠ `Stroke(sc, refPx)` LIVED HERE AND WAS RETIRED 2026-09-06 BY [[S122]] ----
        // Kept as a note rather than deleted, per C1.16/G12: removing the code does not license
        // removing the reasoning, and the reasoning here is a MEASUREMENT that corrects a previous
        // ruling. What it was, verbatim:
        //
        //     /// <summary>A design-frame stroke width in panel pixels, never thinner than one.</summary>
        //     static float Stroke(float sc, float refPx)
        //     {
        //         float t = refPx * sc;
        //         return (t < 1f) ? 1f : t;
        //     }
        //
        // WHY IT EXISTED: it is the obvious rule - scale the design width, never go under one pixel -
        // and it is proportional, which is why job 3 of the 2026-09-06 batch ruled it "correctly
        // screen-space rather than an instance of R-02's family" and left it alone.
        //
        // ⛔ THAT RULING WAS WRONG FOR THE CASE THAT ACTUALLY MATTERED, AND HERE IS THE ARITHMETIC.
        // It is proportional only ABOVE its own floor. Three of its four call sites asked for a 2 px
        // design rule, and 2 px is exactly where the floor fires at one width and not the other:
        //
        //     design   panel        Stroke               Strokes.Px
        //     2 px     1280 x 703   1.000 px = 0.0781%   1 px = 0.0781%   <- Stroke's floor clamps
        //     2 px     2560 x 1406  1.331 px = 0.0520%   2 px = 0.0781%   <- floor does not fire
        //     6 px     1280 x 703   1.997 px = 0.1560%   2 px = 0.1562%
        //     6 px     2560 x 1406  3.994 px = 0.1560%   4 px = 0.1562%
        //
        // So the page's 2 px rules were 33% PHYSICALLY THINNER at the shipped 2560 than at 1280 - the
        // same defect as R-02, arriving through the floor rather than through the formula. Strokes.Px
        // is exactly proportional at both. At 6 px the two agree to 0.15% and the choice is free; at
        // 2 px it is not a matter of taste at all.
        //
        // AND THE SECOND REASON, WHICH WAS ALWAYS THE STATED ONE (see Strokes.cs's header): a whole
        // device pixel is not a rounding convenience, it is the difference between a line and a grey
        // smear. 1.331 px antialiases across two rows. The four draws now go through Strokes.Px, so
        // this page answers "how thick is a 2 px design rule" exactly once.

        // ---- THE THREE REFERENCE-CONTENT CARD SLOTS, AND THE TYPE THAT HAS TO FIT IN THEM ----
        // The slots are the baked card BACKGROUNDS (rectangle_179/180/181) and their measured heights are
        // very unequal — 317 / 449 / 550 — while the densest list, the seven-step ENTRY TIMELINE, sits in
        // the SHORTEST one. At the design row pitch its last row overhung the card by 13 design units and
        // rendered half on the panel, half on the page ground (QC-AUDIT 2026-09-03, finding 6). FitRows
        // scales a block to its own slot instead, so no card can overflow when a row is added later.
        //
        // ⚠ SUPERSEDED IN PLACE 2026-09-06 (S123), one clause only — kept, not deleted, per C1.16/G12,
        // because it is the premise FitRows was written from and the reason the function exists at all.
        // WHAT IT CLAIMED: "the densest list, the seven-step ENTRY TIMELINE, sits in the SHORTEST one".
        // WHAT REPLACED IT: the owner's C-05 ruling ("option 2", 2026-09-06) swapped ENTRY TIMELINE into
        // card 3 (the TALLEST, avail 426) and CONTINGENCY into card 1 (avail 193). The unequal heights,
        // the 2026-09-03 overhang and FitRows' reason for existing are all still exactly as stated above
        // — only which list sits in which slot changed. See DrawReferenceContent for the ruling and the
        // arithmetic. FitRows now returns early on all three cards, so its clamp is unexercised by the
        // shipped content; it stays because a row added later would exercise it again.
        public const float RowTop = 56f, RowSize = 26f, RowPad = 12f;
        static readonly float Card1Bottom = BoxOf("rectangle_179", 1) + BoxOf("rectangle_179", 3);
        static readonly float Card2Bottom = BoxOf("rectangle_180", 1) + BoxOf("rectangle_180", 3);
        static readonly float Card3Bottom = BoxOf("rectangle_181", 1) + BoxOf("rectangle_181", 3);

        /// <summary>Fit `count` rows starting at `top` inside a slot ending at `slotBottom`. Hands back
        /// the wanted size/pitch untouched when the block already fits; otherwise scales BOTH by the same
        /// factor, so the block keeps its proportions rather than just crushing the leading. Type never
        /// goes below Typography.Min (the measured legibility floor) and rows never overlap — a slot too
        /// short for one legible line overflows visibly instead of turning to mush.
        ///
        /// ---- ⛔ THE CLAMP BELOW WAS A KNOWN, OPEN DEFECT. FIXED 2026-09-06 BY [[S116]]. ----
        /// ⚠ SUPERSEDED IN PLACE, per C1.16/G12 — the diagnosis below is kept VERBATIM because it is
        /// the reasoning that earned the fix, and because a reader who finds the new panelW/sc
        /// parameters needs to know what they are for. What it said, and what is now different.
        ///
        /// ITS OWN HEADING, VERBATIM, because a reader who greps for it must find it here rather than
        /// conclude it was quietly dropped:
        ///   "---- ⛔ THE CLAMP BELOW IS A KNOWN, OPEN DEFECT. DO NOT "TIDY" IT. (QC C-05, blocked) ----"
        ///
        /// WHAT IT CLAIMED (still an accurate description of the ORIGINAL code):
        ///   `top`, `slotBottom`, `wantSize`, `wantGap` and `size` are all DESIGN units (the 3427x2112
        ///   frame); the caller multiplies by Z() afterwards. `Typography.Min` is PANEL pixels. So the
        ///   comparison is design-px against panel-px and under-protects by the height scale — at the
        ///   shipped panel it permits type far below the real floor, and the clamp has never once fired.
        ///
        ///   It is LEFT ALONE ON PURPOSE. The unit fix is one line and its consequence is not: with an
        ///   honest floor (Typography.MinFor(panelW) — 32 px at the shipped 2560, not 16) the ENTRY
        ///   TIMELINE clamps to 48.07 design px and the block ends at design y 891.5 against a card
        ///   bottom of 760 — it OVERFLOWS BY 131 DESIGN PX, at every width. That overflow is what
        ///   FitRows was written to prevent, so the fix cannot land without one of C-05's layout
        ///   options, and two of the three touch the Reference Content page, which §14.2 classes TIER-3
        ///   (invention → joint discussion). That is an owner decision (C1.12) and it is not settled.
        ///
        ///   Tracked as [[S116]], BLOCKED. S112 and S115 each computed this fix as "safe at 2560" — both
        ///   against the un-doubled 16 px floor, which is R-02 — and job 1 of the 2026-09-06 batch
        ///   unwound that. Nothing here changes until the layout call is made.
        ///
        /// ⭐ THAT LAST PARAGRAPH IS NOW LOAD-BEARING EVIDENCE, not just history. The two false-safe
        /// computations it names both produced "block ends at design y 748, 12 px of margin". The
        /// LayoutTest checks this fix added REPRODUCE that exact 748 when the fix is mutated back out
        /// — so the suite now fails on the specific error S112 and S115 made, rather than merely
        /// describing it.
        ///
        /// WHAT REPLACED IT: the owner settled that decision on 2026-09-06 — verbatim, "option 2",
        /// C-05's option (b) — and [[S123]] swapped ENTRY TIMELINE into card 3 and CONTINGENCY into
        /// card 1. That supplies the room the diagnosis says was missing, so the unit fix could land,
        /// and it did: the clamp now compares LIKE WITH LIKE via FloorDesign(panelW, sc).
        ///
        /// ⭐ AND BECAUSE THE SWAP CAME FIRST, THIS FIX IS A NO-OP ON THE SHIPPED RENDER. Both cards
        /// now satisfy `need &lt;= avail` and take the early return, so the floor's value never reaches
        /// the render at all. That is deliberate and it is what made the fix safe to land: a correction
        /// to a comparison that is never reached cannot move a pixel. It is live insurance for the
        /// next time a row is added or a slot is re-cut — which is the case FitRows exists for.
        ///
        /// ⛔ THIS DOES NOT MAKE THE CARDS LEGIBLE, and a reader must not take it that way. The rows
        /// draw at RowSize 26 design = 17.31 panel px against a 32 px floor at the shipped 2560. That
        /// is QC [[R-01]], it is open, and only the early return is keeping this clamp from firing on
        /// it. If R-01 is ever fixed by letting the clamp raise these rows instead, the overflow
        /// arithmetic above becomes live again and must be re-derived first.</summary>
        /// <summary>
        /// ⭐ S124: THE FLOOR IS NOW THE CALLER'S TO NAME, AND THAT IS THE FIX.
        ///
        /// This method used to clamp against `FloorDesign` — the GLANCEABLE floor — for every caller.
        /// [[S116]] made that comparison honest (C-05: design px against panel px) and it was right in
        /// UNITS; what it could not know was that the owner would later split the floor BY CONTENT TYPE.
        /// He did, on 2026-09-06 ([[S153]]), and he named this page's rows on the STATIC side of it:
        /// *"permit STATIC reference tables (the Cover's timeline/contingency cards, pad captions) to sit
        /// at `Typography.Dense`"*.
        ///
        /// ⛔ SO THE SINGLE FLOOR WAS OVER-STRICT FOR THE ONE THING THIS METHOD IS USED ON. It held the
        /// Cover's reference rows to **48.068** design px — the floor for a LIVE readout — when the
        /// ruling puts them at **36.051**. And that is not academic: S124 recorded that the proportional
        /// branch is UNREACHABLE, because it needs `wantSize > floor` and no plausible row size exceeds
        /// 48.068. Against the static floor it is reachable, and the difference is the policy.
        ///
        /// ⚠ THE PARAMETER IS REQUIRED, NOT DEFAULTED — the same fail-closed move [[S120]] made with
        /// `ChromeBar.TopY` and [[S158]] with `runActive`. A default would let a LIVE block silently
        /// inherit the static floor, which is the one mistake this split makes possible.
        /// </summary>
        public static void FitRows(float top, float slotBottom, int count, float wantSize, float wantGap,
                                   float floorDesign, out float size, out float gap)
        {
            size = wantSize; gap = wantGap;
            if (count < 1) return;
            float avail = slotBottom - RowPad - top;
            float need = wantGap * (count - 1) + wantSize;
            if (avail <= 0f || need <= avail) return;
            float k = avail / need;
            size = wantSize * k; gap = wantGap * k;
            // ⛔ FloorDesign, NOT Typography.Min. `size` is a DESIGN-frame number; the floor is a
            // PANEL-pixel one. Comparing them directly is C-05, and it is the whole of that defect.
            //
            // ⚠ SUPERSEDED IN PLACE 2026-09-06 by [[S124]] (C1.16 / G12) - ONE CLAUSE, the method name.
            // The floor is no longer chosen HERE; the caller passes it, as either `FloorDesign` (live) or
            // `StaticFloorDesign` (static reference), because the owner's R-01 ruling split the floor by
            // content type. ⭐ THE UNIT STATEMENT ABOVE IS UNCHANGED AND IS STILL THE WHOLE OF C-05:
            // `size` is design px, and BOTH of those methods hand back design px. What this line must
            // never become is a comparison against a panel-pixel number, whoever supplies it.
            float floor = floorDesign;
            if (size < floor)
            {
                size = floor;
                gap = (count > 1) ? (avail - size) / (count - 1) : wantGap;
            }
            if (gap < size) gap = size;
        }

        /// <summary>The glanceable legibility floor expressed in DESIGN-FRAME units, for a page that
        /// draws design values through a scale `sc` (Z(v) = v * sc). This is the ONE place the
        /// design-px/panel-px conversion happens, so there is exactly one line to get right.
        ///
        /// A row drawn at design size `d` reaches the panel at `d * sc` px, and must clear
        /// Typography.MinFor(panelW). So the floor in design units is MinFor(panelW) / sc.
        ///
        /// ---- WHY THE ANSWER IS 48.068 AT BOTH SHIPPED WIDTHS, AND WHAT THAT DOES AND DOES NOT MEAN ----
        /// MinFor(w) = 16 * w / 1280 and sc = h / 2112, so this reduces to 26.4 * (w / h): the design
        /// floor depends ONLY ON THE PANEL'S ASPECT RATIO, not on its size. 1280x703 and 2560x1406 are
        /// the same aspect, so both give 48.068 — which is why C-05's swap is correct at either width
        /// and why the "not enough room" figure struck on 2026-09-06 was wrong at BOTH, rather than
        /// merely stale at one.
        ///
        /// ⛔ BUT "SCALE-FREE" IS NOT "CONSTANT", and writing 48.068 here as a literal would repeat
        /// R-02 in a new place. It is invariant under a change of SIZE at fixed aspect; a change of
        /// ASPECT moves it, because the design frame is letterboxed (sc comes from the height alone
        /// while the floor comes from the width). A 4:3 panel would give a different number.</summary>
        public static float FloorDesign(float panelW, float sc)
        {
            float floor = Typography.MinFor(panelW);
            return (sc > 0f) ? floor / sc : floor;
        }

        /// <summary>The STATIC-REFERENCE floor in the same design-frame units — `Typography.DenseFor`
        /// where the method above uses `MinFor`. **36.051 at both shipped widths**, by the identical
        /// aspect-only argument, and every caveat in the block above applies unchanged.
        ///
        /// ⛔ NOT a second glanceable floor. It is the level the owner's 2026-09-06 ruling permits for a
        /// printed reference table the crew lean in to read, and `Typography.Dense`'s own docstring
        /// draws the line: *"NOT for any live value, any alert, or anything on the nav bar."*</summary>
        public static float StaticFloorDesign(float panelW, float sc)
        {
            float floor = Typography.DenseFor(panelW);
            return (sc > 0f) ? floor / sc : floor;
        }

        // ---- S105 / QC C-01: THE TOP STRIP, LIVE, AT THE BAKED ASSETS' OWN METRICS ----
        // Measured off the seven PNGs this replaces, the same way DrawCameraChrome was measured off
        // camera_auto_earth_io. Every one of them is LEFT-aligned (ink starts at x 0..2 of its box) and
        // carries a small caption over a large value:
        //
        //      the six 113-tall assets   caption cap rows 7..26   value cap rows 56..97
        //      active_phase (89 tall)    caption cap rows 7..27   value cap rows 47..77
        //
        // A cap is ~0.7em and a text y is the top of the line box, ~0.1em above the cap, so a 20-row cap
        // is a 29px line whose top sits at y+4, and a 42-row cap is a 60px line topping at y+50. Reading
        // the geometry out of Keys/Box rather than re-typing it means the strip cannot drift off the bar
        // if a placement is ever re-measured — the same rule AttX/AttY1 follow.
        const float CapSize = 29f, CapTop = 4f;          // the caption over every value
        const float ValSize = 60f, ValTop = 50f;         // the six 113-tall telemetry boxes
        const float PhaseValSize = 44f, PhaseValTop = 43f;   // active_phase, which is 89 tall

        /// <summary>
        /// The seven live telemetry readouts across the top bar.
        ///
        /// ⚠ NO FEED IS A DASH, NOT A STALE NUMBER — and two of these dash even WITH a feed, because the
        /// vehicle's own state says the quantity is meaningless: apogee and perigee follow the same
        /// ApogeeShown/PerigeeShown flags every other page's apsides do (a conic through a landed vessel
        /// is a real solution and a meaningless number), and SPLASHDOWN TIME follows SplashdownShown,
        /// the registry's "N/A off-return" for SPLASHDOWN_ETA. That is ManualChuteDeployPage's rule,
        /// reused rather than re-derived, so the two pages cannot disagree about one value.
        ///
        /// ⛔ `RUNNING 00:22:57` IS DELIBERATELY LEFT BAKED (QC H-2). The label sits beside the phase
        /// heading and reads as TIME IN THE CURRENT PHASE. Nothing in the build keeps a phase-entry
        /// timestamp, and the one clock that does exist — `VesselData.Met`, which reaches `ChromeState`
        /// but not `PageState` — is a DIFFERENT quantity. Drawing MET under a "RUNNING" label would
        /// replace a frozen wrong number with a live wrong one, which is worse: it would look right.
        /// It needs a phase-entry timestamp owned by the painter beside `coverPhase`, and that is its
        /// own change with its own decision about what "the phase" means.
        /// </summary>
        static void DrawTopStrip(DisplayList dl, Func<float, float> X, Func<float, float> Y,
                                 Func<float, float> Z, PageState s)
        {
            bool ok = s.Valid;
            string T(string t) => (ok && !string.IsNullOrEmpty(t)) ? t : "—";

            void Cell(string key, string caption, string value, float valSize, float valTop)
            {
                float bx = BoxOf(key, 0), by = BoxOf(key, 1);
                dl.Text(caption, X(bx), Y(by + CapTop), Z(CapSize), TextAlign.Left, DragonPalette.Text3);
                dl.Text(value, X(bx), Y(by + valTop), Z(valSize), TextAlign.Left,
                        value == "—" ? DragonPalette.Text6 : DragonPalette.White);
            }

            Cell("active_phase_deorbit_coast", "ACTIVE PHASE", T(s.Phase), PhaseValSize, PhaseValTop);
            Cell("splashdown_time_t_01_24_51", "SPLASHDOWN TIME",
                 ok && s.SplashdownShown ? T(s.SplashdownText) : "—", ValSize, ValTop);
            Cell("inertial_velocity_7_69km_s", "INERTIAL VELOCITY", T(s.Velocity), ValSize, ValTop);
            Cell("altitude_393_3km",           "ALTITUDE",          T(s.Altitude), ValSize, ValTop);
            Cell("apogee_416_2km",             "APOGEE",
                 ok && s.ApogeeShown ? T(s.Apoapsis) : "—", ValSize, ValTop);
            Cell("perigee_379_4km",            "PERIGEE",
                 ok && s.PerigeeShown ? T(s.Periapsis) : "—", ValSize, ValTop);
            Cell("inclination_51_62deg",       "INCLINATION",       T(s.InclinationDegText), ValSize, ValTop);
        }

        /// <summary>
        /// S129 / S49 H6 / QC C-08: ENTRY ENABLED, computed and drawn, instead of two PNGs with the
        /// answer baked into which one was exported bolder.
        ///
        /// ⛔ THE SIZE IS NOT THE BAKED ONE, AND THAT IS THE POINT. The `true` asset's box is 51x34
        /// design px - about 22 panel px at the shipped size, two thirds of the measured legibility
        /// floor. This is a LIVE safety verdict, which the owner's 2026-09-06 R-01 policy puts at
        /// `Typography.MinDesignFor` and not at `DenseDesignFor`; drawing it at the PNG's size would be
        /// adding a new sub-floor element on the day that policy landed, and `LegibilityFloorTest`'s
        /// per-page ratchet would fail the build for it. So the words are drawn at the floor and the
        /// row's own geometry gives way, not the legibility.
        ///
        /// ⭐ THERE IS ROOM, MEASURED RATHER THAN ASSUMED. The `true` box starts at design x 783 and the
        /// `false` box at 1132, so `True` has 349 design px of clear run and `False` has 295 before the
        /// panel body's right edge at 1427. At the floor size (48.07) `MarginAffordance.CapAdvance`
        /// puts "False" at 5 * 0.6638 * 48.07 = 160 px. Both fit with room over, and a test asserts it
        /// against the same advance rather than against a look.
        ///
        /// ⛔ AND THE UNSOURCED CASE IS A DASH, NOT A DIMMED "False" - QC's own must-not-break: *"the
        /// row must dash, not read `False`, when there is no source."* Both words are replaced by one
        /// dash, because leaving a greyed `True`/`False` pair on screen still shows the crew a radio
        /// with a position, and there is no position to show.
        /// </summary>
        /// <summary>Where a line of this page's type puts its INK CENTRE, as a fraction of the size,
        /// below the y the text is drawn at. Measured off the rendered PNG rather than taken from a
        /// font table: 26.6 design px below the top at a 48.07 px size, identical on both verdict
        /// words. Used to sit the ENTRY ENABLED verdict on the same optical line as its caption.</summary>
        const float InkCentreOfTop = 0.553f;

        static void DrawEntryVerdict(DisplayList dl, Func<float, float> X, Func<float, float> Y,
                                     Func<float, float> Z, int w, float sc, PageState s)
        {
            EntryVerdict v = s.Valid ? s.EntryEnabled : EntryVerdict.Unknown;
            float size = Typography.MinDesignFor(w, sc);
            // The assets' own measured boxes (Keys/Box rows `true` and `false`), so the row stays where
            // the reference put it. The baked glyphs sat on the box top; the larger type is centred on
            // the same optical line by keeping the top and letting it grow downward into the 111 design
            // px of clear space before the next caption row at y 1666.
            float tx = BoxOf("true", 0);
            float fx = BoxOf("false", 0);
            // ⚠ ONE BASELINE FOR BOTH WORDS, and it is NOT either asset's own box top. The exported
            // boxes disagree - `true` starts at y 1555 and `false` at 1549 - which was invisible while
            // both were 34-px PNGs and is a visible 6-design-px step once they are drawn at the floor
            // size. They are one control and must sit on one line.
            //
            // So both are centred on the CAPTION's box (`entry_enabled`, {351,1555,195,34}), which is
            // the thing a reader lines them up against. `InkCentreOfTop` is MEASURED, not guessed: at
            // 48.07 design px the rendered ink centre of "True" came out 26.6 design px below the top
            // the text was drawn at, on both words independently (ui_cover_entry_*.png, ink rows
            // between the row's own two hairlines). 26.6 / 48.07 = 0.553.
            float capMid = BoxOf("entry_enabled", 1) + BoxOf("entry_enabled", 3) * 0.5f;
            float ty = capMid - InkCentreOfTop * size, fy = ty;

            if (v == EntryVerdict.Unknown)
            {
                // ONE dash, at the first value position. Nothing at the second: a dash in both slots
                // would read as two unknowns rather than one absent answer.
                dl.Text(EntryReadiness.Text(v), X(tx), Y(ty), Z(size), TextAlign.Left, DragonPalette.Text6);
                return;
            }
            bool enabled = v == EntryVerdict.Enabled;
            dl.Text("True",  X(tx), Y(ty), Z(size), TextAlign.Left,
                    enabled ? DragonPalette.White : DragonPalette.Text6);
            dl.Text("False", X(fx), Y(fy), Z(size), TextAlign.Left,
                    enabled ? DragonPalette.Text6 : DragonPalette.White);
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
        static void DrawReferenceContent(DisplayList dl, Func<float, float> X, Func<float, float> Y,
                                         Func<float, float> Z, float panelW, float sc)
        {
            void Card(float titleY, float slotBottom, string title, string[] lines, float spacing)
            {
                float size, gap;
                // ⭐ S124: THE STATIC floor. These are the Cover's reference cards - the ENTRY TIMELINE,
                // PARACHUTES and CONTINGENCY tables - which the owner's R-01 ruling names by description
                // as static reference content. Passing the LIVE floor here held a printed table to a
                // readout's standard and made FitRows' own scaling branch unreachable ([[S124]]).
                FitRows(titleY + RowTop, slotBottom, lines.Length, RowSize, spacing,
                        StaticFloorDesign(panelW, sc),
                        out size, out gap);
                dl.ArcBand(X(333), Y(titleY + 28), Z(4), Z(9), 0, 360, DragonPalette.Accent);
                dl.Text(title, X(362), Y(titleY), Z(34), TextAlign.Left, DragonPalette.White);
                float ry = titleY + RowTop;
                for (int i = 0; i < lines.Length; i++)
                {
                    dl.Text(lines[i], X(340), Y(ry), Z(size), TextAlign.Left, DragonPalette.Text2);
                    ry += gap;
                }
            }

            // ---- S123 / QC C-05: THE TWO LISTS ARE SWAPPED, AND THE CARDS ARE NOT ----
            // 🟢 OWNER RULING, 2026-09-06, verbatim: "option 2" — C-05's option (b). The Reference
            // Content page is §14.2 TIER-3 (no evidence, no asset → invention needs joint discussion), so
            // this was an owner call and not a build chat's; that quote is the whole of the authority.
            //
            // WHAT MOVED: the title, the lines and the spacing only. The three card BACKGROUNDS
            // (rectangle_179/180/181) are real baked Figma layout and do NOT move, so `titleY` and
            // `slotBottom` stay welded to their card — they ARE the card. Card 2 is untouched.
            //
            // WHY: the seven-row ENTRY TIMELINE was in card 1, the SHORTEST slot (avail 193 design px),
            // and could not clear the legibility floor there at any width — seven rows at the floor
            // (48.068 design px, and that number is SCALE-FREE: 32 ÷ sc 0.66572 at 2560 and 16 ÷ sc
            // 0.33286 at 1280 are the same figure) need 336.48. Card 3's avail is 426, so the timeline
            // clears it with 89.5 design px to spare, and CONTINGENCY's four rows need 192.27 of card
            // 1's 193. That is the whole of the swap: the densest list now sits in the tallest slot.
            //
            // ⚠ THIS MAKES [[S116]]'s UNIT FIX SAFE, NOT SUFFICIENT. Both blocks now fit at their WANTED
            // size, so FitRows returns early on both and the clamp never fires either way. The rows still
            // draw at RowSize 26 design = 17.31 panel px against a 32 px floor — that is [[R-01]]'s
            // legibility finding and nothing here closes it.

            // Contingency / abort notes — the CONFIRMED-real panel functions (§4) + the §8 deorbit
            // go/no-go timing.
            Card(499f, Card1Bottom, "CONTINGENCY", new[] {
                "EJECT — SuperDraco abort (8 modes)",
                "WATER DEORBIT / DEORBIT NOW — contingency immediate deorbit",
                "Water landing is the norm — 7 designated splashdown sites",
                "Deorbit go/no-go — ~30 min before claw-sep prep" }, 40f);

            // §8 "Parachutes (Mark 3)".
            Card(848f, Card2Bottom, "PARACHUTES (MARK 3)", new[] {
                "2 drogues deploy first",
                "4 mains deploy at ~2 km",
                "Land under ≥ 3 mains",
                "CUT MAINS after splashdown" }, 40f);

            // Return/deorbit sequence — §8 "Return/deorbit". Times are the ones §8 actually gives; no
            // invented numbers (§1.4).
            Card(1329f, Card3Bottom, "ENTRY TIMELINE", new[] {
                "Undock → trunk jettison",
                "Deorbit burn — ~15 min",
                "Claw separation — ~1 h 20 m before splashdown",
                "Nose cone close & lock",
                "Entry interface",
                "Drogues, then mains at ~2 km",
                "Splashdown — T+50 min from burn start" }, 32f);
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

        /// <summary>The rail slot a ◄/► step lands on from <paramref name="phase"/>, wrapping over all
        /// seven. PURE, deliberately: this is the RULE, and keeping it here means the painter's arrow
        /// branch is three lines that cannot get it wrong and a headless test can pin the whole thing.
        /// S107 / QC C-07 - it used to be inline arithmetic in ScreenPainter, where nothing could reach
        /// it. Out-of-range clamps rather than throws, the same way Build clamps selectedPhase.</summary>
        public static int StepPhase(int phase, int dir)
        {
            int p = phase < 0 ? 0 : (phase >= PhaseCount ? PhaseCount - 1 : phase);
            return (p + PhaseCount + (dir < 0 ? -1 : 1)) % PhaseCount;
        }

        /// <summary>Rail slot <paramref name="i"/>'s button - the inverse of <see cref="PhaseOf"/>, and
        /// out of range it is <c>None</c> rather than a throw. S107/QC C-07 added it so the ROUTING layer
        /// can ask "what does this slot do?" without a second copy of the slot order: the array stays
        /// private, and FigmaUI.PhaseNav is the one caller.</summary>
        public static CoverButton PhaseAt(int i)
        {
            return (i < 0 || i >= PhaseButton.Length) ? CoverButton.None : PhaseButton[i];
        }

        /// <summary>Is this asset key still in the measured Keys/Box table? ⚠ Exists for S131: a key
        /// that is DROPPED (never drawn) must keep its row, because Keys and Box are index-paired and
        /// every placement after a deleted row would shift. A test pins the difference so "dropped"
        /// cannot quietly become "deleted".</summary>
        public static bool HasAssetRow(string key)
        {
            for (int i = 0; i < Keys.Length; i++) if (Keys[i] == key) return true;
            return false;
        }

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
            {(int)CoverButton.ActAcknowledge,1158,1128,212, 60}
            // ⛔ S129 / QC C-08 REMOVED TWO ROWS HERE, and they are named rather than silently dropped:
            //     {(int)CoverButton.EntryTrue,    770,1548, 90,  50},
            //     {(int)CoverButton.EntryFalse,  1125,1544, 100, 55}
            // ENTRY ENABLED is a READOUT of the autopilot's own readiness check - the class the
            // overseer settled on 2026-09-06 - so it is neither a crew latch nor an arming flag. Both
            // rectangles had a hit test and NO dispatcher case anywhere in the tree, which is the
            // shape S75 and audit H18 call worse than an honestly-dim control: a rectangle that looks
            // touchable, over a verdict the crew are not allowed to set. The `CoverButton` members
            // STAY - `CrewPressTest` pins the control-id namespace by name and the ints persist - they
            // are simply no longer reachable. If Part B ever gives the crew an entry-arm control it
            // gets its own rows back, deliberately, with a dispatcher.
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
