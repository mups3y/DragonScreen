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
// ⛔ THE HIT MAP AND THE MARKER MOVE WITH THE DRAW OR NOT AT ALL. FigmaUI.HitTest tests this bar
// FIRST, before any page control, because it is the one touch the crew can always rely on. It used to
// map a touch by `BarIconX[i] / RefW * w` — the stretched mapping — which agreed with the stretched
// draw. Changing one without the other slides every nav icon's touch target off its icon on all 35
// pages, silently. They are in this file, together, for that reason.
// ============================================================================================
namespace DragonScreen
{
    public static class BottomBar
    {
        const float RefW = 3427f, RefH = 2112f;

        /// <summary>The bar's own box in the design frame: full width, 235 tall, at design y 1877.</summary>
        const float BarY = 1877f, BarH = 235f;

        // The five icons are baked into component_48.png at these design x's (each 80 wide, at design
        // y 2003 = the bar top 1877 + the icon's local y 126). Left-to-right: compass, target, rocket,
        // folder, gear. FigmaUI.BarTarget maps icon N to the page it opens; the routing is its, the
        // geometry is this file's.
        public static readonly float[] IconX = { 46f, 174f, 302f, 430f, 558f };
        public const float IconY = 2003f, IconS = 80f;

        // The active-tab marker was baked under the FIRST icon in component_48.png and erased there so
        // it can be drawn dynamically under whichever tab is active. These are the erased block's own
        // component_48 coordinates: a thin white line just above the bar's bottom edge.
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
        const float MarkY = BarY + 223f, MarkH = 10f, MarkW = 108f;

        /// <summary>
        /// The bar's rectangle in panel pixels: the design frame's own bottom bar, fit to height and
        /// centred. THE one geometry — Draw, Hit and Marker all read it, and so does the headless test.
        /// </summary>
        public static void Rect(int w, int h, out float x, out float y, out float bw, out float bh)
        {
            x = y = bw = bh = 0f;
            if (w <= 0 || h <= 0) return;
            float sc = h / RefH;
            bw = RefW * sc;
            bh = BarH * sc;
            y = BarY * sc;
            x = (w - bw) * 0.5f;
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
        }

        /// <summary>Draw the bar, undistorted, where the design puts it.</summary>
        /// <summary>The bar with NO vessel state — CURRENT STATE dashes. ⚠ Five pages call this
        /// (`MenuPage`, `PlaceholderPage`, `FigmaFramePage`, `SuitCheckPage`, `VrioTestPage`) because
        /// they genuinely do not receive a `PageState`; a dash there is the honest reading and is
        /// LOGGED rather than papered over — see REGISTER.md S147.</summary>
        public static void Draw(DisplayList dl, int w, int h) { Draw(dl, w, h, new PageState()); }

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

        /// <summary>
        /// The bar, with CURRENT STATE drawn live over the erased box.
        ///
        /// ⚠ THE TYPE IS AT THE GLANCEABLE FLOOR, NOT AT THE BAKED SIZE. The exported value was ~29
        /// design px — 19.3 panel px, 60% of the floor, and one of QC R-01's own samples. It is a LIVE
        /// value, so [[S153]]'s policy puts it at `Typography.MinDesignFor`. ⭐ It fits: right-aligned
        /// at design x 1461 with the icon strip ending at 625, there are 836 design px of clear run,
        /// against 798 for the longest string the baked art ever showed.
        /// </summary>
        public static void Draw(DisplayList dl, int w, int h, PageState s)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float x, y, bw, bh;
            Rect(w, h, out x, out y, out bw, out bh);
            dl.Asset("component_48", x, y, bw, bh, DragonPalette.White);
            if (bw <= 0f) return;

            float k = bw / RefW;                       // the bar's own uniform scale
            string state = CurrentState(s);
            // The ink CENTRE of the erased value sat at PNG row 185; a line drawn at `top` puts its
            // ink centre 0.553 * size below it (measured off a render in S129). So the live text lands
            // on the same optical line the baked value did, at whatever size it is drawn.
            float size = Typography.MinDesignFor(w, k);
            float top = ValueInkMid - InkCentreOfTop * size;
            dl.Text(state, x + ValueRight * k, y + top * k, size * k, TextAlign.Right,
                    StateInk(s, state));
        }

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
        /// re-derived — one number, one measurement.</summary>
        const float InkCentreOfTop = 0.553f;

        /// <summary>Which bottom-bar icon (0..4) a touch hit, or -1. Present on every page.</summary>
        public static int Hit(float px, float py, int w, int h)
        {
            float bx, by, bw, bh;
            Rect(w, h, out bx, out by, out bw, out bh);
            if (bw <= 0f) return -1;
            float k = bw / RefW;                       // the bar's own uniform scale
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
        public static void Marker(DisplayList dl, int w, int h, int icon)
        {
            if (dl == null || icon < 0 || icon >= IconX.Length) return;
            float bx, by, bw, bh;
            Rect(w, h, out bx, out by, out bw, out bh);
            if (bw <= 0f) return;
            float k = bw / RefW;
            float mw = MarkW * k;
            float cx = bx + (IconX[icon] + IconS * 0.5f) * k;
            dl.Rect(cx - mw * 0.5f, by + (MarkY - BarY) * k, mw, MarkH * k, DragonPalette.White);
        }
    }
}
