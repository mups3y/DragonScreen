// DragonScreen — BottomBar  (PURE: component_48, the persistent bottom-bar navigation)
// ============================================================================================
// ONE SOURCE OF TRUTH FOR THE BAR: its rectangle, its draw, its hit map and its active-tab marker.
// Every one of the 21 pages that shows the bar calls Draw here; FigmaUI's HitTest calls Hit here;
// FigmaUI's per-page marker calls Marker here. That is MenuPage.CellRect's rule — "the one source of
// truth Build, HitTest and the headless nav test all share, so the drawn grid and the hit grid can
// never drift apart" — applied to the one control the crew can always rely on.
//
// ---- WHY THIS FILE EXISTS (S103 / QC batch 1: C-04 + H-07) ----
// The bar used to be drawn by each page as `dl.Asset("component_48", 0, Y(1877), w, Z(235))` — 21
// sites, all of them FULL PANEL WIDTH against a HEIGHT-derived scale. At the shipped 1280x703 that is
// x-scale 0.3735 against y-scale 0.3329: the asset was STRETCHED 12.2% HORIZONTALLY on every page. Its
// crosshair icon is exactly 130x130 in the PNG and rendered 23x21; every glyph and every word baked
// into the bar was 12% wide (QC C-04). And because the frame pages draw their art fit-to-height at
// `ox` while the bar was drawn 0..w, the frame's own border became a vertical rule crossing the bar and
// the design's single rounded corner became two, ~50px apart (QC H-07).
//
// The bar is drawn UNIFORMLY now, in the design frame's own coordinates: `ox .. ox + RefW*sc`, the
// same fit-to-height box the letterboxed pages already draw their art in. That is not a compromise, it
// is where the asset belongs — component_48 carries the design frame's OWN bottom border (2px white at
// its rows 105-106 and 233-234) and its own left/right edges, so drawing it anywhere but the design
// frame put a page border in the middle of a page.
//
// ---- WHAT THE STRIPS EITHER SIDE ARE, AND WHY THEY ARE NOT PAINTED ----
// A panel wider than the design aspect leaves `ox` of page ground at each end. On the eleven
// letterboxed pages that IS the letterbox and it is already page ground, so the bar now ends exactly
// where the page art does — which is H-07's fix. On the ten pages that spread x across the full width
// the strips are new, and they are deliberately left as page ground rather than filled: filling them
// would put component_48's own left/right border in the MIDDLE of a filled bar, which is the defect
// this file removes, one step to the right. See docs/QC_FINDINGS.md, batch 1.
//
// ⭐ SUPERSEDED IN PLACE 2026-09-06 (S176) — THE LAST PARAGRAPH IS WHAT THE OWNER FOUND ON THE GLASS,
// AND IT IS NOW FIXED. It is kept verbatim because it is the record of WHY the bar was letterboxed
// everywhere and of the defect (H-07) that any fix must not re-create; only its conclusion has moved.
// The owner's finding, verbatim (2026-09-06 glass pass): *"the bottom bar does not go to the edge of
// the screen as it should"*, and then *"all pages have the bottom bar problem"* — measured and
// confirmed by a sweep of every shipped-size preview: 94 of 94 pages that draw the bar had it at
// 139..2421 on a 2560-wide panel, NOT ONE edge-to-edge (REGISTER.md S172).
// ⛔ AND THE PARAGRAPH'S OWN REASONING IS WHY THE FIX IS NOT "STRETCH IT AGAIN". `component_48` is a
// GLYPH-BEARING PNG and stretching it is QC C-04, which is what the paragraph above says. The route
// this file now takes is the one S172 prescribed: the bar's GROUND is a PRIMITIVE drawn across the
// page's own full width, and its CONTENTS are placed with the page's own x-map at a UNIFORM scale, so
// nothing is stretched and the bar's internal rule lands back on the page's own column divider. The
// eleven letterboxed pages keep the design-frame box exactly — there the bar must agree with the frame
// art or H-07 comes straight back, and whether that ART should letterbox at all is [[S173]]'s question,
// not this file's.
//
// ---- ⭐ THE BAR IS NO LONGER ONE RASTER (S176 / §14.2a clause (1), G13 2026-09-06) ----
// "An element PRESENT in the export is BUILT FROM the export — its own per-element PNG or its own
// vector path, LAYERED at the export's own coordinates ... never left flattened once its own export
// exists." The bar's own elements have NO individual export (checked across all seven zips, S175), so
// they are SLICED from `component_48.png`'s flat uniform ground by `plugin/tools/slice_bottom_bar.py`,
// which prints the measured ink bbox of every cut and refuses to write one that clips its element.
// Ground, borders, the two horizontal rules and the two vertical rules are PRIMITIVES; the two frame
// corners, the five nav icons, the two captions, the baked POINTING MODE value and the comm/counter
// block are LAYERED TILES; CURRENT STATE is TYPED and live.
//
// ⛔ THE ONE HALF OF CLAUSE (1) THIS DOES NOT CLOSE, AND IT IS NOT AN OVERSIGHT. "Text the export
// renders as text is TYPED, not imported as pixels." Four of the tiles above ARE text. Typing them
// puts them under S153's floor policy, and for this bar that floor is the GLANCEABLE one, not the
// static-reference one — `Typography.Dense`'s own docstring excludes "anything on the nav bar" in as
// many words. That is 48.07 design px at the shipped panel against the export's MEASURED 20.3 for the
// captions and 27.5 for the comm block: a 1.7x-2.4x raise that re-flows the whole bar and would put a
// fake "22:33" at the same size as the live phase. That is exactly [[S153a]]'s wall, it is governed by
// S153a-Q1 which is OPEN and the owner's, and three of the four values are additionally [[S147b]]'s
// held §1.4 question. Written up in REGISTER.md S176 rather than decided here (C1.12/C1.14).
//
// ⛔ THE HIT MAP AND THE MARKER MOVE WITH THE DRAW OR NOT AT ALL. FigmaUI.HitTest tests this bar
// FIRST, before any page control, because it is the one touch the crew can always rely on. It used to
// map a touch by `BarIconX[i] / RefW * w` — the stretched mapping — which agreed with the stretched
// draw. Changing one without the other slides every nav icon's touch target off its icon on all 35
// pages, silently. They are in this file, together, for that reason.
// ⭐ AND SINCE S176 THE FIT IS PART OF THAT COUPLING. `Hit`, `Marker` and `Draw` all take the same
// `BarFit`, `FigmaUI` resolves it from ONE table (`FitFor`), and `FigmaUINavTest.BarFollowsItsPage`
// renders EVERY page, finds the `bar_nav_0` tile it actually drew, and asserts that tile's own centre
// is a hit on icon 0 — so the draw and the hit map are checked against each other per page, by
// measurement, rather than by both reading the same constant.
// ============================================================================================
namespace DragonScreen
{
    /// <summary>
    /// How a page maps the 3427-wide design frame onto its panel — and therefore where the bar's ends
    /// and its internal rules land. ⛔ NOT a style choice: it must be the SAME map the page's own body
    /// uses, or the bar's rule stops continuing the page's column divider and the bar stops agreeing
    /// with the frame art above it. <see cref="BottomBar.FitFor"/> is the one table; a page passes the
    /// value that matches its own `X()` and `FigmaUINavTest.BarFollowsItsPage` proves it did.
    /// </summary>
    public enum BarFit
    {
        /// <summary>Letterboxed: the design frame centred, `ox + x*sc`. The eleven pages that draw a
        /// fit-to-height frame raster (Frame 58/59/66, the reconstructed procedure pages, the plots).
        /// The bar ends where the frame art ends — H-07's fix, and it must stay that way while the art
        /// letterboxes ([[S173]] owns whether it should).</summary>
        Frame = 0,

        /// <summary>Spread: `x * w / RefW`. The pages whose own body is drawn with `sx = w/RefW`
        /// (Menu, the settings pages, the Vehicle family, SuitCheck, VRIO). The bar reaches both glass
        /// edges. ⚠ Only the ANCHORS take this map — every glyph is still drawn at the uniform
        /// `h/RefH`, which is what keeps QC C-04 closed.</summary>
        Stretch = 1,

        /// <summary>Split reflow: <see cref="SplitReflow.X"/>. The Cover and Manual Chute Deploy, the
        /// only two pages that reflow with a split rather than a letterbox. The bar reaches both edges
        /// AND its first rule lands 15 px right of the page's own column divider, which is the +23
        /// design px the reference has.</summary>
        Split = 2
    }

    public static class BottomBar
    {
        const float RefW = 3427f, RefH = 2112f;

        /// <summary>Draw commands the bar emits, worst case — every page that shows it must carry this
        /// much capacity on top of its own. ⚠ It was 2 (one asset + one text) until S176 un-flattened
        /// the raster; the pages' `Commands` constants were raised by 20 in the same commit.</summary>
        public const int Commands = 20;

        /// <summary>The bar's own box in the design frame: full width, 235 tall, at design y 1877.</summary>
        const float BarY = 1877f, BarH = 235f;

        // ---- THE ASSET'S OWN STRUCTURE, MEASURED (S176) --------------------------------------------
        // All of these are `component_48.png` pixels. The asset spans the design frame's full width at
        // design y 1877, so its x IS design x and its y is design y minus 1877. Every number below was
        // read off the file (row histograms + ink bounding boxes), never chosen; the same measurements
        // drive plugin/tools/slice_bottom_bar.py's cut list and are printed by its --verify.

        /// <summary>Local y where the bar's opaque body starts. Above it the asset is transparent except
        /// the frame's two corner arcs — measured: rows 0..104 carry only the arcs, 105..106 are the
        /// white border rule, 107..232 are flat ground, 233..234 are the white bottom border.</summary>
        const float BodyY = 105f;

        /// <summary>The design frame's border stroke, in asset pixels. Measured: white at x 0..1 and
        /// 3425..3426 for the whole body, and rows 105..106 / 233..234 across it.</summary>
        const float Border = 2f;

        /// <summary>The two rounded frame corners, cut as tiles because they are the only CURVES in the
        /// bar's chrome. 132x105 contains the arc (measured: its ink reaches x 0..101 by row 104) and
        /// stops exactly where the border stops curving — the straight top rule below it is a
        /// primitive, and <see cref="RuleStart"/> is where the arc hands over to it.
        ///
        /// ⚠ 105 IS MEASURED AND 112 WAS TRIED FIRST. A cap is drawn 132x105 source into 88x70 panel
        /// px, so a 2-row rule inside it comes out as a ~1.3 px SOFT band while the primitive rule
        /// beside it is a crisp 2 device px (`Strokes.Px`). With rows 105-106 inside the tile the two
        /// met end-to-end and the seam read as a GAP at the bar's right end in `ui_cover.png`. Cutting
        /// at 105 leaves the tile carrying only the curve.</summary>
        const float CapW = 132f, CapH = 105f;

        /// <summary>Where the frame's straight top rule begins, measured from each end: row 105's white
        /// run in the asset is x 90..3336, and 3427-3337 is the same 90. Inside that the corner is
        /// ROUNDED and the rule must not be drawn, or the bar grows two square corners.</summary>
        const float RuleStart = 90f;

        // The five icons are baked into component_48.png at these design x's (each 80 wide, at design
        // y 2003 = the bar top 1877 + the icon's local y 126). Left-to-right: compass, target, rocket,
        // folder, gear. FigmaUI.BarTarget maps icon N to the page it opens; the routing is its, the
        // geometry is this file's.
        // ⭐ S176: these are now also the SLICE boxes — `bar_nav_0..4.png` are cut at exactly
        // (IconX[i], IconY-BarY, IconS, IconS), so the drawn icon and its touch target come from one
        // set of numbers rather than from a raster and a constant that could drift.
        public static readonly float[] IconX = { 46f, 174f, 302f, 430f, 558f };
        public const float IconY = 2003f, IconS = 80f;

        /// <summary>The two vertical rules inside the bar. MEASURED: white at x 1464..1465 and
        /// 1943..1944, rows 121..218. ⭐ The FIRST one is the whole point of S172's second defect — the
        /// reference puts it 23 design px right of the Cover's procedure column divider at 1441, so it
        /// CONTINUES that divider down through the bar. It only does that if the bar is laid out in the
        /// page's own x-map, which is what <see cref="BarFit"/> is for.</summary>
        public const float Rule1X = 1464f;
        const float Rule2X = 1943f, RuleTop = 121f, RuleBot = 219f;

        // The active-tab marker was baked under the FIRST icon in component_48.png and erased there so
        // it can be drawn dynamically. These are the erased block's own component_48 coordinates: a
        // thin white line just above the bar's bottom edge.
        // ⚠ The erased BLOCK is deliberately larger than the marker drawn into it (S103 finished that
        // erase — the original left the pill's glow behind, so every page carried a ghost marker under
        // icon 0 whatever tab was really active; QC C-12). Do not shrink the block to fit these.
        //
        // ⭐ SUPERSEDED IN PLACE 2026-09-06 (S175) — THERE IS NO ERASED BLOCK ANY MORE, AND THAT IS WHY
        // QC C-12 IS CLOSED. The paragraph above is kept because it is the record of how the marker came
        // to be dynamic and why the constants are what they are; only its premise has changed. S103 was
        // erasing a `filter10_dd` DROP SHADOW — a soft gradient with no edge — which is why two attempts
        // still left ink behind. The owner supplied the component's own per-element export on 2026-09-06,
        // and one of the five variants (the zip whose Component 48 carries the marker under NO icon) is
        // simply the art WITHOUT the pill: MEASURED 0 non-ground pixels in the marker band under all
        // five icons, against 707 in the file S103 left. `component_48.png` is now that export, so the
        // marker area is clean BY CONSTRUCTION rather than by erasure. The 707 were the owner's "white
        // smudge"; the swap removed exactly those and changed nothing else outside them.
        // ⛔ ONE THING MUST BE RE-APPLIED IF THIS ASSET IS EVER RE-EXPORTED, and it is not this block —
        // it is the CURRENT STATE value erase at x 1098..1461, y 170..208 (see the S147 block below).
        // The raw export still carries the baked sentence "Far Field Pointing Deorbit" there, and that
        // box is now the ONLY difference between the shipped PNG and the export. Dropping a fresh export
        // in without re-cutting it re-bakes a frozen literal behind a live readout.
        // ➕ S176, 2026-09-06 — ADDED, nothing above changed: `component_48.png` is no longer DRAWN by
        // anything. It is now the SOURCE the eleven `bar_*.png` tiles are cut from, and the re-export
        // warning above applies to re-running the slicer, not to a draw call.
        const float MarkY = BarY + 223f, MarkH = 10f, MarkW = 108f;

        // ---- THE LAYERED TILES, AND THE ANCHOR EACH ONE HANGS OFF -----------------------------------
        // Four GROUPS, because the bar is four groups: the nav icons pin to the LEFT edge, CURRENT
        // STATE pins to its rule, POINTING MODE pins to the other rule, and the comm block pins to the
        // RIGHT edge. Every offset inside a group is a design-px distance from that group's anchor,
        // drawn at the UNIFORM scale — so a group never stretches internally, whatever the page's x-map
        // does to where its anchor lands. That separation is the whole mechanism of S172's fix.

        /// <summary>CURRENT STATE's caption tile, as a distance LEFT of Rule1X, and its size.</summary>
        const float StateLabelDx = Rule1X - 1287f, StateLabelY = 141f, StateLabelW = 158f, StateLabelH = 22f;

        /// <summary>POINTING MODE's caption and its baked value, as distances RIGHT of Rule2X.
        /// ⚠ The value is BAKED and its source is [[S147b]]'s held owner question — the registry's
        /// authority is the attitude controller's `Steering` target and `src/Steering.cs` is deleted
        /// (§B12.8 rider), so there is nothing to read until Part B. Do NOT substitute `ModeText`:
        /// that is the control AUTHORITY (IDLE / AUTO / MANUAL), a different quantity.</summary>
        const float PointLabelDx = 1966f - Rule2X, PointLabelY = 141f, PointLabelW = 158f, PointLabelH = 22f;
        const float PointValueDx = 1965f - Rule2X, PointValueY = 171f, PointValueW = 144f, PointValueH = 30f;

        /// <summary>The comm block and the counter, as a distance LEFT of the frame's right edge.
        /// ⚠ Cut and drawn as ONE tile: its parts are laid out against each other, and all three of its
        /// VALUES are [[S147b]]'s — stock CommNet supplies ONE real signal strength against the four
        /// named links this block draws (SPX / GND / TDRS / ISS), and the counter "79/1450122" has no
        /// registry entry and nothing in the tree names what it counts.</summary>
        const float CommDx = RefW - 2663f, CommY = 136f, CommW = 686f, CommH = 59f;

        /// <summary>The UNIFORM scale everything in the bar is DRAWN at — the design frame's own
        /// fit-to-height scale, and never the page's x-map.
        ///
        /// ⛔ THIS IS THE ONE NUMBER QC C-04 TURNED ON. The old bug was `w / RefW` for x against
        /// `h / RefH` for y — a 12.2% horizontal stretch on every glyph in the bar at the shipped
        /// aspect. Anchors may move with the page (see <see cref="MapX"/>); SIZES may not.</summary>
        public static float Scale(int h) { return (h > 0) ? h / RefH : 0f; }

        /// <summary>
        /// Design x -> panel x, in the page's OWN map. This is the only place a fit differs.
        ///
        /// ⚠ Used for ANCHORS ONLY. Anything with an aspect — a tile, a glyph, a stroke width — takes
        /// <see cref="Scale"/>. Mixing the two is QC C-04.
        /// </summary>
        public static float MapX(float x, int w, int h, BarFit fit)
        {
            switch (fit)
            {
                case BarFit.Stretch: return (w > 0) ? x * w / RefW : 0f;
                case BarFit.Split:   return SplitReflow.X(x, w, h);
                default:             return x * Scale(h) + (w - RefW * Scale(h)) * 0.5f;
            }
        }

        /// <summary>
        /// The bar's rectangle in panel pixels — the box its GROUND fills, in the page's own map.
        /// THE one geometry: Draw, Hit and Marker all read it, and so does the headless test.
        ///
        /// ⚠ SINCE S176 `bw / RefW` IS NOT THE DRAW SCALE. On a spread page the box is the full panel
        /// while every glyph in it is still drawn at <see cref="Scale"/>; the two agree only under
        /// <see cref="BarFit.Frame"/>. Anything that used `bw / RefW` as "the bar's scale" must read
        /// `Scale(h)` instead — that is what the old code meant, and it only happened to be the same
        /// number while the bar was always letterboxed.
        /// </summary>
        public static void Rect(int w, int h, BarFit fit,
                                out float x, out float y, out float bw, out float bh)
        {
            x = y = bw = bh = 0f;
            if (w <= 0 || h <= 0) return;
            float sc = Scale(h);
            bh = BarH * sc;
            y = BarY * sc;
            x = MapX(0f, w, h, fit);
            bw = MapX(RefW, w, h, fit) - x;
            // ⛔ NOT CLAMPED TO THE PANEL, DELIBERATELY, and this was got wrong once already.
            // A panel TALLER than the design aspect (w < RefW*sc) makes `x` negative and the bar hangs
            // off both ends. Clamping it to the panel width was tried and is wrong twice over: it
            // re-introduces the very distortion this file exists to remove (bw would shrink while bh
            // did not - measured 0.2918 against 0.3788 at 1000x800), and it would put the bar in a
            // DIFFERENT frame from the page art, which is H-07 all over again. Every page here is
            // fit-to-height and lets its own art overflow at that aspect; the bar overflows WITH it.
            // The shipped screens are 1280x703/710 - aspect 1.82 against the design's 1.623 - so `x`
            // is always positive in the build. See FigmaUINavTest.BottomBarUndistorted.
            // ⚠ S118, 2026-09-06 — ADDED, nothing above changed. "The shipped screens" meant
            // 1280x703/710 when this was written; screenWidth went 1280 -> 2560 on 2026-09-05 (Q5 /
            // S115), so the shipped panel is now 2560x1406.
            // ⭐ THE ARGUMENT ABOVE IS UNAFFECTED AND WAS NEVER RESOLUTION-DEPENDENT: 2560x1406 is
            // the SAME 1.82:1 aspect as 1280x703, so `x` is positive at either size. Only the figure
            // is stale, which is why it is restated here rather than corrected in place.
            // FigmaUINavTest still runs at 1280x703 on purpose - it is a SHAPE check, and a second
            // size would not make it a better one.
            // ➕ S176, 2026-09-06 — ADDED, nothing above changed. The whole block above is about
            // BarFit.Frame, which is unchanged. Under Stretch and Split there is no letterbox to be
            // negative: MapX(0) is 0 and MapX(RefW) is w at any aspect, so the bar spans the panel
            // exactly and the clamp question does not arise.
        }

        /// <summary>Which page uses which map. ⛔ ONE TABLE, and it is the reason there is an enum at
        /// all: a page that passed the wrong fit would draw a bar its own hit map disagreed with, which
        /// is the S103 defect in a new coat. `FigmaUINavTest.BarFollowsItsPage` renders every page and
        /// checks the tile it ACTUALLY drew against this table, so the two cannot drift.</summary>
        public static BarFit FitFor(UiPage page)
        {
            switch (page)
            {
                // SplitReflow's own two pages — verified by its header: `grep` for `Split = 1500`
                // returns CoverPage and ManualChuteDeployPage and nothing else.
                case UiPage.Cover:
                case UiPage.ManualChute:
                    return BarFit.Split;

                // The pages whose body is drawn with `sx = w / RefW, sy = h / RefH` — they already
                // spread across the full panel, so a letterboxed bar was the odd element out.
                case UiPage.Menu:
                case UiPage.Audio:
                case UiPage.AudioVideo:
                case UiPage.SuitCheck:
                case UiPage.Vehicle:
                case UiPage.VehicleMech:
                case UiPage.VehicleCrew:
                case UiPage.VehiclePropulsion:
                case UiPage.VehiclePower:
                case UiPage.VehicleAvionics:
                case UiPage.VehicleGnc:
                case UiPage.VehicleThermal:
                // Procedure is an ALIAS for VrioTest (S110 / QC F-01) and draws VrioTestPage, so it
                // takes VrioTest's map, not FigmaFramePage's.
                case UiPage.Procedure:
                case UiPage.VrioTest:
                    return BarFit.Stretch;

                // Everything else draws a fit-to-height frame raster and letterboxes with it — the
                // Cabin frame, the HUD, the docking/rendezvous/plot pages, the reconstructed procedure
                // pages, the systems deep-views, and every PlaceholderPage.
                default:
                    return BarFit.Frame;
            }
        }

        /// <summary>Draw the bar, undistorted, where the design puts it.</summary>
        /// <summary>The bar with NO vessel state — CURRENT STATE dashes. ⚠ Five pages call this
        /// (`MenuPage`, `PlaceholderPage`, `FigmaFramePage`, `SuitCheckPage`, `VrioTestPage`) because
        /// they genuinely do not receive a `PageState`; a dash there is the honest reading and is
        /// LOGGED rather than papered over — see REGISTER.md S147.</summary>
        public static void Draw(DisplayList dl, int w, int h, BarFit fit)
        {
            Draw(dl, w, h, new PageState(), fit);
        }

        // =========================================================================================
        //  S147 / S49 H40 — CURRENT STATE STOPS BEING A PICTURE
        // =========================================================================================
        // ⛔ THE FINDING, ON EVERY PAGE IN THE BUILD. `CURRENT STATE`, `POINTING MODE`, the
        // SPX/GND/TDRS/ISS block and a counter were all PIXELS IN `component_48.png` — so 21 pages
        // carried one frozen sentence ("Far Field Pointing Deorbit") whatever the vehicle was doing.
        //
        // ⭐ THE METHOD IS THIS PNG'S OWN. The active-tab marker was baked under icon 0 and ERASED so
        // it could be drawn dynamically (S103; QC C-12 closed the glow that erase left behind), and
        // this file already documents that. CURRENT STATE's value box is erased the same way:
        // MEASURED at x 1098..1461, y 170..208 — right-aligned at 1461, which is where its caption
        // ends too — with the vertical rule at x 1464..1465 and the caption band (y 143..158) left
        // untouched and re-counted afterwards.
        //
        // ---- WHAT IS WIRED, AND WHAT IS NOT, AND WHY ----
        // ⭐ CURRENT STATE has a source and it is the registry's own: `TELEMETRY_REGISTRY.md:67` names
        //    `CrewProcedureOps`'s step label, which reaches the screens as `PageState.AutoPhase`. That
        //    is null unless the conductor is engaged, so it falls back to `s.Phase` — the live
        //    classifier the Cover's own ACTIVE PHASE row already prints, so the two surfaces cannot
        //    disagree about what the vehicle is doing (C7.1).
        // ⛔ POINTING MODE IS NOT WIRED, and that is a source problem rather than an effort one. The
        //    registry names its authority as *"attitude controller / `Steering` target"* — and
        //    `src/Steering.cs` is DELETED and, per §B12.8's rider, never recovered. There is nothing
        //    to read. `PageState.ModeText` is the control AUTHORITY (IDLE / AUTO / MANUAL), which is a
        //    different quantity; printing it under a POINTING MODE label would be a wrong reading
        //    rather than a missing one. Left baked, and written up.
        // ⛔ THE COMM BLOCK IS NOT WIRED EITHER, and the C1.15 search is already ON FILE:
        //    `docs/reference/INSTALLED_MODS.md:86-91` records that stock CommNet supplies ONE real
        //    signal strength (wired by S24) and that no third-party comms mod is installed. The bar
        //    draws FOUR named links — SPX / GND / TDRS / ISS. One real signal cannot honestly fill
        //    four station indicators, and inventing three is §1.4 tier-3. Left baked, and written up.
        // ⛔ THE COUNTER ("79/1450122") has no entry in the registry at all and no source names what it
        //    counts. Left baked, and written up.
        // ➕ S176, 2026-09-06 — ADDED, nothing above changed. All three are still baked and still
        //    [[S147b]]'s, but they are no longer baked into ONE 3427-px raster: each is its own tile,
        //    layered at its own coordinate, so wiring any one of them is a local change to this file
        //    rather than an edit to a shared PNG.

        /// <summary>
        /// The bar, with CURRENT STATE drawn live over the erased box.
        ///
        /// ⚠ THE TYPE IS AT THE GLANCEABLE FLOOR, NOT AT THE BAKED SIZE. The exported value was ~29
        /// design px — 19.3 panel px, 60% of the floor, and one of QC R-01's own samples. It is a LIVE
        /// value, so [[S153]]'s policy puts it at `Typography.MinDesignFor`. ⭐ It fits: right-aligned
        /// at design x 1461 with the icon strip ending at 625, there are 836 design px of clear run,
        /// against 798 for the longest string the baked art ever showed.
        ///
        /// ⚠ S176: THE CLEAR RUN IS NOW A PROPERTY OF THE FIT, not a single number. Under Frame it is
        /// unchanged. Under Stretch and Split the icons pin left and the rule moves right relative to
        /// them, so the run only ever GROWS — measured at the shipped 2560x1406: 836 design px under
        /// Frame, 1004 under Split, 1104 under Stretch. Pinned by `FigmaUINavTest.BarValueHasItsRun`.
        /// </summary>
        public static void Draw(DisplayList dl, int w, int h, PageState s, BarFit fit)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float x, y, bw, bh;
            Rect(w, h, fit, out x, out y, out bw, out bh);
            if (bw <= 0f) return;

            float k = Scale(h);                    // ⛔ the UNIFORM draw scale — never bw / RefW
            float left = x, right = x + bw;
            float body = y + BodyY * k;            // top of the opaque body
            float bot = y + bh;                    // the bar's own bottom = the frame's bottom
            // ⛔ WHOLE DEVICE PIXELS, THROUGH THE ONE RULE. `Border * k` is 0.67 px at 1280 and
            // 1.33 at 2560 - a grey smear at both, and the R-02 family's own defect (a device-pixel
            // quantity that does not follow the panel). `Strokes.Px` is the project's single answer
            // and `LegibilityFloorTest` sweeps every thin Rect the Cover draws to prove it: with the
            // float form the 2560 set contained 1.3314, which is what caught this.
            float bd = Strokes.Px(Border, k);

            // ---- 1. THE CHROME, AS PRIMITIVES ----
            // ⭐ Ground first, full width. This is the S172 fix in one line: the ground is a FLAT
            // uniform fill (`#111B52`, measured), so spreading it across the page costs nothing, while
            // the glyph-bearing parts below are placed on it at a uniform scale. Stretching the raster
            // instead is QC C-04, which is why the two are separated at all.
            dl.Rect(left, body, bw, bot - body, DragonPalette.Panel);
            // The frame's own border: sides for the body's whole height, bottom rule across it, and the
            // top rule from cap to cap (the caps carry the curve where it turns).
            dl.Rect(left, body, bd, bot - body, DragonPalette.White);
            dl.Rect(right - bd, body, bd, bot - body, DragonPalette.White);
            dl.Rect(left, bot - bd, bw, bd, DragonPalette.White);
            dl.Rect(left + RuleStart * k, body, bw - 2f * RuleStart * k, bd, DragonPalette.White);

            // ---- 2. THE TWO FRAME CORNERS, AS TILES ----
            // ⚠ Drawn AFTER the rules so the arc's own transition overwrites the straight ends. They
            // are the only curves in the bar's chrome and the only reason a tile is needed here at all.
            dl.Asset("bar_cap_left", left, y, CapW * k, CapH * k, DragonPalette.White);
            dl.Asset("bar_cap_right", right - CapW * k, y, CapW * k, CapH * k, DragonPalette.White);

            // ---- 3. THE TWO VERTICAL RULES, AT THE PAGE'S OWN X ----
            // ⭐ THIS is S172's second defect, fixed: the first rule CONTINUES the page's own column
            // divider, which it can only do if it is placed with the page's map rather than the bar's.
            float ruleY = y + RuleTop * k, ruleH = (RuleBot - RuleTop) * k;
            dl.Rect(MapX(Rule1X, w, h, fit), ruleY, bd, ruleH, DragonPalette.White);
            dl.Rect(MapX(Rule2X, w, h, fit), ruleY, bd, ruleH, DragonPalette.White);

            // ---- 4. THE FIVE NAV ICONS, PINNED LEFT, UNIFORM ----
            float iconY = y + (IconY - BarY) * k, iconS = IconS * k;
            for (int i = 0; i < IconX.Length; i++)
                dl.Asset(NavKey[i], left + IconX[i] * k, iconY, iconS, iconS, DragonPalette.White);

            // ---- 5. THE TWO CAPTIONS AND THE BAKED POINTING VALUE, PINNED TO THEIR RULES ----
            float r1 = MapX(Rule1X, w, h, fit), r2 = MapX(Rule2X, w, h, fit);
            dl.Asset("bar_label_current_state", r1 - StateLabelDx * k, y + StateLabelY * k,
                     StateLabelW * k, StateLabelH * k, DragonPalette.White);
            dl.Asset("bar_label_pointing_mode", r2 + PointLabelDx * k, y + PointLabelY * k,
                     PointLabelW * k, PointLabelH * k, DragonPalette.White);
            dl.Asset("bar_value_pointing_mode", r2 + PointValueDx * k, y + PointValueY * k,
                     PointValueW * k, PointValueH * k, DragonPalette.White);

            // ---- 6. THE COMM BLOCK AND COUNTER, PINNED RIGHT ----
            dl.Asset("bar_comm_block", right - CommDx * k, y + CommY * k, CommW * k, CommH * k,
                     DragonPalette.White);

            // ---- 7. CURRENT STATE — THE ONE LIVE THING ON THE BAR ----
            string state = CurrentState(s);
            // The ink CENTRE of the erased value sat at PNG row 185; a line drawn at `top` puts its
            // ink centre 0.553 * size below it (measured off a render in S129). So the live text lands
            // on the same optical line the baked value did, at whatever size it is drawn.
            float size = Typography.MinDesignFor(w, k);
            float top = ValueInkMid - InkCentreOfTop * size;
            dl.Text(state, r1 - (Rule1X - ValueRight) * k, y + top * k, size * k, TextAlign.Right,
                    StateInk(s, state));
        }

        /// <summary>The five nav-icon tiles, in <see cref="IconX"/> order. Cut by
        /// plugin/tools/slice_bottom_bar.py at exactly the boxes above.</summary>
        static readonly string[] NavKey =
            { "bar_nav_0", "bar_nav_1", "bar_nav_2", "bar_nav_3", "bar_nav_4" };

        /// <summary>
        /// The ink CURRENT STATE is drawn in — and the Figma UI's whole alarm channel ([[S130]], H7).
        ///
        /// ---- ⛔ THE DEFECT THIS CLOSES ----
        /// `Alarms.Mask` folds G-force, propellant, power and the entire FDIR spine every frame and was
        /// then DISCARDED on every Figma page. `Alarms.cs`' own header says it in terms: *"THE ALERT
        /// ROUTING IS THE POINT, NOT THE DECORATION."* The crew's home page could not show a caution.
        ///
        /// ---- ⭐ WHY THE COLOUR AND NOT A NEW FIELD ----
        /// The Figma pages have no alert element — `First.vue` has none either — so ADDING one would be
        /// invention (§1.4). Tinting an element that is already there is not: it is the grammar this
        /// project already uses, where the legacy `ChromeBar` leaves a page link SAYING "VEHICLE" and
        /// lets the colour say there is a problem behind it. ⚠ The TEXT is not overloaded — it still
        /// reads the phase, which is what it means — only the ink carries the severity.
        ///
        /// ⚠ NOMINAL STAYS WHITE, deliberately, rather than becoming `Alarms.Colour`'s green. A bar that
        /// is green whenever nothing is wrong trains the eye to ignore it, and it would have re-tinted
        /// every existing render for no reading gain. Colour appears when there is something to say.
        /// ⭐ And the two states that DO speak come from `Alarms.Colour` — the one severity-to-colour
        /// function (rule P5), never a second copy of the mapping.
        ///
        /// ⚠ A DASH IS NOT NOMINAL. No feed keeps `Text6`: a dead channel must not read as a quiet one.
        /// </summary>
        public static Rgba StateInk(PageState s, string state)
        {
            if (state == Dashes.None) return DragonPalette.Text6;
            Severity sev = Alarms.SystemSeverity(s);
            return (sev >= Severity.Caution) ? Alarms.Colour(sev) : DragonPalette.White;
        }

        /// <summary>What CURRENT STATE reads. The registry's own source first, the live classifier
        /// second, a dash when there is neither — never a plausible sentence.</summary>
        public static string CurrentState(PageState s)
        {
            if (!s.Valid) return Dashes.None;
            if (!string.IsNullOrEmpty(s.AutoPhase)) return s.AutoPhase;
            if (!string.IsNullOrEmpty(s.Phase)) return s.Phase;
            return Dashes.None;
        }

        /// <summary>The erased value box, in component_48's own pixels: right edge, and the ink centre
        /// of the row it sat on. MEASURED off the PNG, not chosen — see the block above.</summary>
        const float ValueRight = 1461f, ValueInkMid = 185f;

        /// <summary>Where a line of this type puts its ink centre below the y it is drawn at, as a
        /// fraction of the size. Measured on a render in [[S129]] and reused here rather than
        /// re-derived — one number, one measurement.
        ///
        /// ➕ S176, 2026-09-06 — ADDED, nothing above changed and NOTHING RENDERS DIFFERENTLY. The
        /// literal 0.553 moved to `Typography.CapCentreOfTop` when a SECOND call site needed it (the
        /// Cover's two pills), and this reads it rather than keeping a private copy — "one number,
        /// one measurement" applied to itself. The value is identical, so every render is
        /// byte-identical; only the home moved.</summary>
        const float InkCentreOfTop = Typography.CapCentreOfTop;

        /// <summary>The design x where CURRENT STATE's clear run begins — the right edge of the last
        /// nav icon's box. Used only to MEASURE the run in the tests; the draw does not need it.</summary>
        public const float ValueRunLeft = 558f + IconS;

        /// <summary>How much clear design-space run CURRENT STATE has on this page's map, in DESIGN px
        /// — the distance from the last icon to the value's right edge, expressed back in design units
        /// so it can be compared against a string's design width at any panel size.</summary>
        public static float ValueRun(int w, int h, BarFit fit)
        {
            float k = Scale(h);
            if (k <= 0f) return 0f;
            float x0 = MapX(0f, w, h, fit) + ValueRunLeft * k;
            float x1 = MapX(Rule1X, w, h, fit) - (Rule1X - ValueRight) * k;
            return (x1 - x0) / k;
        }

        /// <summary>Which bottom-bar icon (0..4) a touch hit, or -1. Present on every page.</summary>
        public static int Hit(float px, float py, int w, int h, BarFit fit)
        {
            float bx, by, bw, bh;
            Rect(w, h, fit, out bx, out by, out bw, out bh);
            if (bw <= 0f) return -1;
            float k = Scale(h);                    // ⛔ the UNIFORM scale, matching the DRAW above
            float y0 = by + (IconY - BarY) * k, y1 = y0 + IconS * k;
            if (py < y0 - 12f || py > y1 + 12f) return -1;
            for (int i = 0; i < IconX.Length; i++)
            {
                // Padding kept under half the icon PITCH so neighbours never share a hit region: the
                // pitch is 128 design px and the icon 80, so the 48-px design gap absorbs 6px a side
                // at any scale this bar is drawn at.
                float x0 = bx + IconX[i] * k, x1 = x0 + IconS * k;
                if (px >= x0 - 6f && px < x1 + 6f) return i;
            }
            return -1;
        }

        /// <summary>Slide the bar's white marker under the active tab (the reference App.vue's
        /// `.marker`). `icon` is an index into <see cref="IconX"/>; out-of-range draws nothing.</summary>
        public static void Marker(DisplayList dl, int w, int h, int icon, BarFit fit)
        {
            if (dl == null || icon < 0 || icon >= IconX.Length) return;
            float bx, by, bw, bh;
            Rect(w, h, fit, out bx, out by, out bw, out bh);
            if (bw <= 0f) return;
            float k = Scale(h);
            float mw = MarkW * k;
            float cx = bx + (IconX[icon] + IconS * 0.5f) * k;
            dl.Rect(cx - mw * 0.5f, by + (MarkY - BarY) * k, mw, MarkH * k, DragonPalette.White);
        }
    }
}
