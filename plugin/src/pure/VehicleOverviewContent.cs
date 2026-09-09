/*
 * DragonScreen — VehicleOverviewContent : the rebuilt VEHICLE OVERVIEW, drawn on the ICON base page.
 *
 * ⛔ BUILT FROM THE SPECS AND NOTHING ELSE — `SPEC_OVERVIEW_STATUS_ROWS.md` §2/§3 the rows and their
 * sources, §7 the words and the four-state marker, §8 the geometry, §9 the right panel;
 * `SPEC_GAUGES.md` §5.5 the eight dials' placement and the vehicle, §5.6 the consumables panel, §6
 * where the values live; `SPEC_BASE_SCREENS.md` §6/§7 the page underneath, via `BasePageIcon`.
 *
 * ⛔ `pure/VehicleOverviewPage.cs` WAS NOT READ, COPIED OR PATTERNED ON. It is the OLD page on a
 * 3427×2112 reference frame — a different design, not a wrong one — and reading it is how its layout
 * leaks into this one. The one thing this build takes from it is in the prompt's §5, which is a
 * RULING about what dashes, quoted below, not a line of its code.
 *
 * ⛔ IT IS A RENDERER (prompt §7, as `S245`/`S246` are): no `UiPage` value, no routing, no
 * `PageAction`, `PageCount` stays 36, no hit map. Nothing here is reachable or clickable, and the
 * toggle's two states are DRAWN, not switched.
 *
 * ---- ⭐ THE MAP, AND THE TRAP INSIDE IT (§8) ----
 * `design_x = 0.816149 * ref_x`, and ⛔ THERE IS NO y MAP. Our frame is 1.8216 against the
 * reference's 1.6681, so the locked gauge rows were pulled up to fit and a two-point y fit through
 * them extrapolates the page title to y 11.7 — INSIDE THE BORDER. SIZES scale by S; PLACEMENTS use
 * the reference's own page fraction. Every number below is the spec's, typed from it, and the
 * arithmetic that produced it is re-derived independently in `VehicleOverviewContentTest`.
 *
 * ---- ⚠ FOUR THINGS THE SPECS DO NOT CARRY. Measured, stated, and raised — not chosen ----
 *  (1) THE MARKER'S TICK GLYPH. §8.2 gives the disc a DIAMETER and stops; every approved render
 *      draws a check knocked out of it. Measured off `gauge_assets/rail_icons_3x.png` (the owner's
 *      own three-state strip) as fractions of the disc diameter, with the round caps backed off:
 *      (-0.159, +0.042) → (-0.024, +0.164) → (+0.222, -0.075), stroke 0.085 D. The knockout ink
 *      measures rgb(21,21,51) against the page ground's rgb(26,31,53) — one channel and ten counts
 *      apart on a 1.4 px stroke — so it is drawn AS the ground, which is what a knockout is. `BOB-43`.
 *  (2) THE INK COLOUR OF THE `CONNECTIONS` HEADER AND LABELS AND THE PANEL'S LABELS. §8.3 gives the
 *      rule's colour and no other; §5.6 gives the bars' two colours and no ink. All three measure
 *      the same grey-lilac as §8.2's rail titles on the approved page, so they are drawn in
 *      `RailTitle` — ONE constant, not three that can drift. `BOB-44`.
 *  (3) THE RULE'S THICKNESS. §8.3 gives its endpoints and its colour. One design pixel on the
 *      approved render; drawn as one.
 *  (4) WHICH ROWS HAVE BEEN "CHECKED". §7.1 makes the marker GREY until the row's check completes,
 *      and `SPEC_CHECKLIST_ROWS.md` §6 leaves the per-row source table explicitly OPEN — so no
 *      per-row completion signal exists in the build to read. It is therefore an INPUT
 *      (`OverviewInputs.ChecksComplete`), not something invented here, and all four marker states
 *      are proved by driving it. `BOB-46`.
 *
 * ---- ⚠ AND ONE PLACE THIS PAGE PRINTS TWO DIFFERENT DASHES, DELIBERATELY ----
 * `SPEC_GAUGES.md` §5.7 says an invalid DIAL shows `--`; `SPEC_OVERVIEW_STATUS_ROWS.md` §9.3.3 says
 * a value with no source draws `—`, which is `Dashes.None` and the project's own S148 ruling that
 * ONE meaning gets ONE glyph. Both specs are followed in their own domain rather than one being
 * quietly overruled, so this page currently prints both. ⛔ That is a collision to RESOLVE, not a
 * style: `BOB-47`.
 */
using System;

namespace DragonScreen
{
    /// <summary>
    /// What the page cannot read out of `PageState` — the two control states the owner specified, and
    /// §7.1's completion flag. ⛔ All three are the CALLER's, so nothing here invents them.
    /// </summary>
    public struct OverviewInputs
    {
        /// <summary>§8.4.3: the white pill sits over the SELECTED half.</summary>
        public bool CabinSelected;
        /// <summary>§8.4.3: MORE's active state is a CHOSEN placeholder, owner-ruled — ⛔ not a claim
        /// about what the real control does.</summary>
        public bool MoreActive;
        /// <summary>§7.1: false leaves every marker GREY and every status word DIM. ⛔ Grey is not a
        /// severity — it means the check has not completed. See the header (4).</summary>
        public bool ChecksComplete;
    }

    public static class VehicleOverviewContent
    {
        // ==========================================================================================
        //  §8.1 — THE PAGE TITLE.  ⭐ 66.52 IS DERIVED, NOT TYPED: it centres the ink in the band
        //  between the window inner top (21.5) and the top gauge row's top edge, 45.02 above and
        //  45.02 below. ⛔ Move the gauge row and this moves with it — which is why the test
        //  re-derives it from the row rather than reading this constant back.
        // ==========================================================================================
        public const string Title = "VEHICLE OVERVIEW";
        public const float TitleCx = 960f;
        public const float TitleInk = 23.67f;
        public const float TitleInkTop = 66.52f;
        public const float WindowInnerTop = 21.5f;

        // ==========================================================================================
        //  §8.2 — THE LEFT RAIL, 7 ROWS.  SIZES by S, PLACEMENT by page fraction — except row 1.
        //  ⚠ ROW 1 KEEPS THE UNIFORM-SCALE START DELIBERATELY: by page fraction it would sit at 77.8,
        //  43 px higher, leaving a 3 px gap to the title. Only the PITCH compresses.
        // ==========================================================================================
        public const int RailRows = 7;
        public const float MarkerDiameter = 28.97f;
        public const float MarkerLeft = 68.56f;
        public const float RailTextLeft = 112.63f;
        public const float RailTitleInk = 13.06f;
        public const float RailStatusInk = 13.06f;
        public const float RailTitleToStatus = 29.38f;
        public const float Row1MarkerCy = 131.81f;
        public const float RowPitch = 100.19f;
        /// <summary>§8.2: "the ink is EXACTLY centred on the icon (measured)" — the title's ink TOP
        /// sits 6.53 above the marker centre, which for 13.06 of ink is dead centre.</summary>
        public const float TitleTopAboveMarker = 6.53f;

        // ---- §8.2 / §7.1 / §7.2 the rail's colours. ⛔ All measured, none invented. ----
        public static readonly Rgba RailTitle = Rgba.Hex("9499C3");
        public static readonly Rgba StatusDim = Rgba.Hex("555779");
        public static readonly Rgba StatusLit = new Rgba(1f, 1f, 1f, 1f);
        /// <summary>§7.1: the check has NOT completed. ⛔ Not a severity.</summary>
        public static readonly Rgba MarkerUnchecked = Rgba.Hex("9499C3");
        public static readonly Rgba MarkerGo = Rgba.Hex("40C110");
        public static readonly Rgba MarkerCaution = Rgba.Hex("EA7B15");

        // ---- the tick knocked out of the disc. ⚠ MEASURED, not specified — header (1), `BOB-43`. --
        private const float TickAx = -0.159f, TickAy = 0.042f;
        private const float TickBx = -0.024f, TickBy = 0.164f;
        private const float TickCx = 0.222f, TickCy = -0.075f;
        private const float TickStroke = 0.085f;

        // ==========================================================================================
        //  §8.3 — `CONNECTIONS`.  ⛔ THE BLOCK WIDTH IS THE SPEC'S, AND src/pure CANNOT COMPUTE IT.
        //
        //  §8.3 requires the width to be MEASURED FROM THE LIVE FONT and never typed, because a
        //  hard-coded width has already failed twice here. ⛔ `DisplayList.Text`'s own contract says
        //  the opposite is impossible in this layer: "WIDTH IS NOT KNOWN HERE, AND THAT IS A REAL
        //  LIMIT... the answer is a measurement interface the two renderers implement — NOT a font in
        //  here." So one of the two rules has to give, and the choice is stated rather than hidden:
        //  the width is typed from the spec, and the PROPERTY §8.3 actually wants — that the widest
        //  label and the widest value do not collide — is asserted in DEVICE space by
        //  `DragonScreenPreview.exe --overviewcheck`, which renders the page and measures the ink.
        //  ⭐ Independently measured through the preview's own text call on 2026-09-09: widest label
        //  125.28 + 50.61 + widest value 77.27 = 253.16 design px, against the spec's 253.23. `BOB-48`.
        // ==========================================================================================
        public const float CnLeft = 513.83f;
        public const float CnWidth = 253.23f;
        public const float CnRuleX0 = 513.01f;
        public const float CnRuleX1 = 767.06f;
        public const float CnRuleY = 583.39f;
        public const float CnRuleH = 1f;
        public const float CnHeaderInkTop = 552f;
        public const float CnHeaderInk = 13.06f;
        public const float CnRow1InkTop = 596.10f;
        public const float CnRowPitch = 31.40f;
        public const float CnRowInk = 12.24f;
        /// <summary>§8.3: block left + widest label + the reference's 50.61 label-to-value gap. The
        /// values are RIGHT-aligned on the block's right edge, which puts the widest value's left
        /// edge here by construction — the two readings of §8.3 agree for the widest string, and the
        /// approved render right-aligns.</summary>
        public const float CnValueColumn = 630.94f;
        public static readonly Rgba CnRule = Rgba.Hex("3A3F63");
        public const string CnHeader = "CONNECTIONS";

        // ==========================================================================================
        //  `SPEC_GAUGES.md` §5.6 — THE RIGHT-HAND PANEL. Consumables, eight rows in four pairs.
        //  ⚠ `pitch 97.4 across a pair boundary` is a TOTAL pitch, not an addition.
        // ==========================================================================================
        public const int PanelRows = 8;
        public const float PanelLeft = 1538f;
        public const float PanelValueRight = 1849.8f;
        public const float PanelBar1Y = 144f;
        public const float PanelTrackW = 187.8f;
        public const float PanelTrackH = 8.2f;
        public const float PanelPairPitch = 68.6f;
        public const float PanelGroupPitch = 97.4f;
        public const float PanelLabelInk = 12.3f;
        /// <summary>§5.6: "baseline 12.2 above the bar" — for a label with no descender the baseline
        /// IS the ink bottom.</summary>
        public const float PanelLabelBaseline = 12.2f;
        public const float PanelValueInk = 16.5f;
        public static readonly Rgba PanelFill = Rgba.Hex("298BFE");
        public static readonly Rgba PanelTrack = Rgba.Hex("2E304B");

        // ==========================================================================================
        //  §8.4 — THE TWO BOTTOM CONTROLS. 🔒 OWNER-APPROVED.
        //  ⛔ TWO OVERLAPPING SHAPES, NOT ONE BOX WITH A WHITE HALF INSIDE IT. The pill is 18.77 px
        //  TALLER than the outline box and sits PROUD above and below it.
        // ==========================================================================================
        public const float BoxX = 68.56f;
        public const float BoxW = 302.79f;
        public const float BoxY = 823.63f;
        public const float BoxH = 69.37f;
        public const float BoxRadius = 6f;
        public const float BoxStroke = 2f;
        public const float PillW = 164.05f;
        public const float PillY = 814.25f;
        public const float PillH = 88.14f;
        public const float MoreX = 1713.51f;
        public const float MoreW = 137.93f;
        public const float CtrlInk = 13.87f;
        /// <summary>§8.4.1: every label centres on the PILL's vertical centre — which is also the
        /// outline box's, because the pill is centred on the box.</summary>
        public const float CtrlInkCy = 858.32f;
        public const string LabelSystems = "SYSTEMS";
        public const string LabelCabin = "CABIN";
        public const string LabelMore = "MORE";
        public static readonly Rgba CtrlPill = new Rgba(1f, 1f, 1f, 1f);
        public static readonly Rgba CtrlSelectedInk = Rgba.Hex("1A1C48");
        public static readonly Rgba CtrlUnselectedInk = new Rgba(1f, 1f, 1f, 1f);
        public static readonly Rgba CtrlStroke = Rgba.Hex("575A81");
        /// <summary>
        /// §8.4.2 — ⛔ A DELIBERATE DEPARTURE FROM THE REFERENCE, NOT A MEASUREMENT.
        /// 🟢 OWNER 2026-09-09: "the colour within the new buttons should match the background colour
        /// of the tabs section", then "I like that better lock it in". ~~`#161738`, measured on the
        /// reference~~ SUPERSEDED. ⛔ Do not "correct" it back to the measured value.
        /// ⭐ It is `BasePageNoIcon.Margin` — the SAME expression the band is painted from, so the
        /// two cannot drift apart, which is the whole point of the ruling.
        /// </summary>
        public static Rgba CtrlInfill { get { return BasePageNoIcon.Margin; } }

        // ==========================================================================================
        //  `SPEC_GAUGES.md` §5.5 — THE EIGHT DIALS. ⛔ "These are the numbers. They are not derived
        //  and must not be re-derived." Scaled by FRAME WIDTH, not by a uniform fit.
        // ==========================================================================================
        // ⛔⛔ THE TWO SPECS DISAGREE ABOUT THIS ONE NUMBER BY 14.3 px, AND THE APPROVED PAGE SETTLES
        // IT. `SPEC_GAUGES.md` §5.5 says "top row y 212.0" and computes its own clearances from it
        // (bottom 303.1, vehicle gap 31.4). `SPEC_OVERVIEW_STATUS_ROWS.md` says 226.3 — written out
        // in §8.1's title derivation, `(21.5 + (226.3 - 91.1)) / 2 - 23.67 / 2`, and again in §8.5's
        // clearance table as "gauge row 1 bottom 317.40 -> vehicle top 334.50 + 17.10". 317.40 - 91.1
        // = 226.3.
        // ⭐ MEASURED ON THE PAGE THE OWNER APPROVED (`gauge_assets/overview_v2.png`): the CO2 dial's
        // dotted ring spans y 133..318, centre 225.5, R 92.5 including the dot — 226.3, not 212.0.
        // Its x lands at 1312.5 against §5.5's 1312.4, and the second row at 428.5/72.5 against
        // 429.0/71.8, so ONLY the top row's y is in dispute and the rest of §5.5 is confirmed.
        // ⛔ So §5.5's 212.0 is a superseded number sitting in an owner-LOCKED file, and its 69.4 /
        // 31.4 clearances go with it (§8.5 reads 91.9 / 17.10). Raised as `BOB-51`; the page is built
        // to the value the owner actually approved.
        public const float TopRowCy = 226.3f;
        public const float TopRowR = 91.1f;
        public const float SecondRowCy = 429f;
        public const float SecondRowR = 71.8f;
        public static readonly float[] TopRowCx = { 606.9f, 842.1f, 1077.3f, 1312.4f };
        public static readonly float[] SecondRowCx = { 548.1f, 741.4f, 1178.8f, 1372.2f };

        // ==========================================================================================
        //  §8.6 / §5.5 — THE VEHICLE. WIDTH-FIT, NEVER HEIGHT-FIT; NEVER SCALED PER-AXIS.
        //
        //  🟢 The owner approved `dragon_crew_hi_771x1232.png` — "I want the decals" — ⛔ but it lives
        //  in `assets/reference/`, which is C7.1 look-don't-ship, and the promotion is NOT RULED. So
        //  this draws the turntable frame that already ships, exactly as the prompt's §6 directs.
        //
        //  ⚠⚠ AND THE PROMPT'S "THE GEOMETRY IS IDENTICAL EITHER WAY" IS NOT TRUE OF THESE TWO FILES,
        //  MEASURED: `dragon_crew_hi` carries non-zero alpha at every edge, so its opaque box IS the
        //  whole 771×1232 file (aspect 0.6258). `dragon_turn_000.png` is 512×1024 with the capsule
        //  occupying x 24..487, y 113..910 — 464×798, aspect 0.5815. Width-fitting the ARTWORK to the
        //  locked 292.0 therefore runs it to 502.18 tall against `crew_hi`'s 466.59: the capsule ends
        //  at 836.68 instead of 801.10 and the shelf clearance falls 91.9 → 56.3. ⛔ NOTHING COLLIDES
        //  and the locked 0.9 / 0.9 side gaps are exact, because it is the WIDTH that is bound. The
        //  alternative — fitting the FILE rather than the artwork — holds the height but shrinks the
        //  capsule to 264.6 wide and opens the side gaps to 14.5, which is the one thing §8.6 says the
        //  width-fit rule exists to prevent. `BOB-49`, and the swap is the four constants below.
        //
        //  ⭐⭐ S258 — THE SWAP HAPPENED, AND `BOB-49` IS CLOSED BY THE OWNER, 2026-09-10, verbatim:
        //  *"I would like to replace the current 3d render on the new vehicle overview page with this
        //  one."* · *"I prefer the new render, do not trim off anything it fits it's place perfectly at
        //  that size without trimming."*
        //  ⛔ EVERYTHING ABOVE IS KEPT (C1.16) AND NONE OF IT WAS WRONG — it is the measurement that
        //  chose `dragon_turn_000` over `dragon_crew_hi` while the promotion was unruled, and the
        //  WIDTH-FIT rule it establishes is what the new asset is fitted by. What changed is the FILE.
        //  ⚠ ~~`dragon_crew_hi_771x1232.png` … C7.1 look-don't-ship … the promotion is NOT RULED~~ —
        //  MOOT: neither file is used now. The C7.1 blocker went with it, unresolved rather than
        //  resolved, and that distinction is worth keeping.
        //  ⭐ The new asset SHIPS in `plugin/GameData/DragonScreen/art/cover/`, so there is no C7.1
        //  question about it at all: `ImageStore.cs:73` and `PreviewMain.cs:3655` both build
        //  `art/cover/<key>.png`, and the KEY IS THE FILENAME. There is no manifest and no enum.
        //  ⛔ `dragon_turn_000.png` STAYS — `Turntable.KeyPrefix` = `"dragon_turn_"` and the COVER
        //  page still builds frame 000's key from it. Removing it would break a different page.
        // ==========================================================================================
        public const string VehicleAsset = "dragon_crew_v3";     // S258: was "dragon_turn_000"
        public const float VehicleCx = 960f;
        public const float VehicleTop = 334.5f;
        public const float VehicleW = 292f;
        /// <summary>The asset's own pixels, and the box its artwork actually occupies inside them.
        /// ⛔ MEASURED off the file (alpha &gt; 0), not assumed — three plausible "trim to the artwork"
        /// rules give three different heights and §8.6 names alpha &gt; 0 as the one to use.
        /// ⭐ S258 — re-measured off `dragon_crew_v3.png` by this session, not taken from the prompt:
        /// 1800 × 3010 RGBA, md5 `6987dfebeb58c73f5ce61e42e72161ad`, alpha&gt;0 bbox
        /// x 93..1707, y 80..2944 → 1614 × 2864. ⚠ ~~512 × 1024, artwork 24,113 464×798~~ superseded
        /// in place (C1.16). ⭐ The RULE is unchanged and so is the code that reads these; only the
        /// numbers moved, which is exactly what `BOB-49` said the swap would be.</summary>
        public const float AssetW = 1800f, AssetH = 3010f;
        public const float AssetOpaqueX = 93f, AssetOpaqueY = 80f;
        public const float AssetOpaqueW = 1614f, AssetOpaqueH = 2864f;

        // ==========================================================================================
        //  ⭐⭐ S258 — THE SHADOW + GLOW, AND WHY IT IS AN ASSET RATHER THAN PRIMITIVES
        //
        //  🟢 OWNER, 2026-09-10: *"I would definitely like number 3 shadow+glow no trimming."*, at
        //  glow brightness **46 %** and glow width **×2.00** — *"2.0 all confirmed"*.
        //
        //  ⛔ `DisplayList` HAS NO BLUR, NO BLEND MODE AND NO GRADIENT. Its primitives are `Rect`,
        //  `Box`, `Text`, `Image`, `Asset`, `ImageCircle`, `ImageUV`, `Line`, `Tri`, `ArcBand`. A
        //  blurred overlay ellipse cannot be drawn, so it arrives pre-rendered. ⛔ Do NOT "improve"
        //  this by adding blend modes to the renderer — one flat layer is EXACT here, not an
        //  approximation, and the arithmetic is why: on a ground whose every channel is below 0.5
        //  (ours is `#1A1F35` = 26,31,53), `overlay` with black is identical to alpha-compositing
        //  black, and `screen` with white is identical to alpha-compositing white. The three layers
        //  were solved into one straight-alpha layer exactly (`A = 1−(1−g)(1−s)(1−p)`).
        //
        //  ⛔ PROVENANCE — TWO OF THE THREE LAYERS ARE MEASURED AND ONE IS NOT, and a later session
        //  must not mistake the invented one for a source value:
        //     drop shadow   🟢 MEASURED  vehicle alpha, dy 5.07, blur stdDev 44.39, black 15 %
        //     contact pool  🟢 MEASURED  cx 960, cy 848.06, rx 190.25, ry 41.58, blur 15.85
        //     glow          ⛔ INVENTED  same centre, rx 380.50 (×2.00), ry 79.01, blur 34.88, white 46 %
        //     shelf fade    ⛔ INVENTED  alpha × smoothstep, reaching 0 at y = 893.0 over the last 40 px
        //  ⭐⭐ THERE IS NO WHITE GLOW IN THE SOURCE ASSET — proved, not assumed: along the bottom the
        //  designer's own composite only ever DARKENS. "Glow" is a word in their filename. 🟢 The owner
        //  asked for it anyway and was right to — their ground is `#1A1C48` (blue 72) against our
        //  `#1A1F35` (blue 53), so a black pool has about a third less room to work in on our page.
        //  ⛔ 46 % and ×2.00 are OWNER-RULED off rendered ladders, not defaults.
        //
        //  ⚠ AT ×2.00 THE GLOW REACHES TWO DIALS — alpha 37/255 under `LOOP B` and `NET PWR 1`
        //  (`LOOP A` and `NET PWR 2` are untouched at 0). 🟢 The owner was shown that number for every
        //  width on the ladder and chose ×2.00. ⛔ A DECISION, NOT A DEFECT. Drawing this layer FIRST
        //  is what keeps the dial INK clean: the glow passes under it, never over it.
        //  ⚠ THE SHELF FADE EXISTS BECAUSE THE EFFECT WANTED TO CROSS THE SHELF. Cut flat at 893 it
        //  left alpha 82/255 sitting on the shelf line — a visible straight chop. ⛔ Do not remove the
        //  fade and do not extend the layer past 893.
        //
        //  ⛔⛔ THE BOX IS SYMMETRIC ON 960 BY CONSTRUCTION (`960 ∓ 393`) AND ITS BOTTOM IS THE SHELF.
        //  The asset is exactly 3× the design box (786×3 = 2358, 620×3 = 1860). The overseer's first
        //  bake auto-trimmed to the alpha and came out 0.33 px lopsided; it was re-baked symmetric on
        //  purpose. ⛔ S256 took a 0.5 px lean out of the tab strip — do not re-introduce one here.
        //
        //  ⚠⚠ `ShadowY` IS 273, NOT THE 271 THE S258 PROMPT'S §4 TABLE PRINTS, AND THE ASSET SETTLED
        //  IT. §4 says 271; §5 says the fade reaches "exactly 0 at y = 893.0"; §6 asks for a test that
        //  `ShadowY + ShadowH == 893.0` and reports the ink ending at 885.8, "7.2 px clear of the
        //  shelf". 271 + 620 = **891**, which contradicts all three. ⭐ MEASURED on the file by this
        //  session: the alpha ramp reaches 1/255 at the asset's own LAST ROW (3 at 1.3 px up, 16 at
        //  6.3 px up, peak 223 around 39.7 px up), so the asset's bottom edge IS design y 893.0 and
        //  `ShadowY = 893 − 620 = 273`. At 273 the perceptible ink ends 7.2 px clear, matching §6's own
        //  measurement; at 271 it would be 9.2. ⛔ Reported as `BOB-70` rather than silently corrected.
        // ==========================================================================================
        public const string ShadowAsset = "dragon_shadow_glow";
        public const float ShadowX = 567f, ShadowY = 273f, ShadowW = 786f, ShadowH = 620f;

        // ==========================================================================================
        //  §2 / §7.3 / §7.4 — THE ROWS. ⛔ Every title names ITS OWN SOURCE and nothing more.
        // ==========================================================================================
        private static readonly string[] RowTitle = {
            "ALL SYSTEMS CHECK", "CABIN LIFE SUPPORT", "CABIN THERMAL CONTROL",
            "ELECTRICAL POWER SYSTEM", "PROPELLANT QUANTITY", "TRUNK SEPARATION",
            "PARACHUTE DEPLOYMENT"
        };
        public static string RailTitleAt(int i) { return RowTitle[i]; }

        /// <summary>§3 / §7.5 — the four `CONNECTIONS` labels, in order.</summary>
        private static readonly string[] CnLabel = {
            "Airlock", "Docking Interface", "Comms Link", "Nose Cone"
        };
        public static string ConnectionLabelAt(int i) { return CnLabel[i]; }

        /// <summary>§9's eight consumable rows, in order.</summary>
        private static readonly string[] PanelLabel = {
            "Power Unit 1 Energy", "Power Unit 2 Energy",
            "Usable Deorbit Fuel", "Usable Deorbit Oxidizer",
            "Orbit 1 Subtank Fuel", "Orbit 1 Subtank Oxidizer",
            "Orbit 2 Subtank Fuel", "Orbit 2 Subtank Oxidizer"
        };
        public static string PanelLabelAt(int i) { return PanelLabel[i]; }

        /// <summary>`SPEC_GAUGES.md` §5.5's dial titles and §5.7's units, in draw order: top row
        /// then second row. ⭐ Units verbatim from §5.7.
        /// ⚠ THE DEGREE SIGN IS A NON-ASCII LITERAL IN A FILE WITH NO BYTE-ORDER MARK, like
        /// `Dashes.None`'s em dash beside it, so it depends on the compiler reading this file as
        /// UTF-8. That is not asserted by hoping: `VehicleOverviewContentTest` compares the unit
        /// against `"°C"` written as an escape, which is the one form that cannot be
        /// mis-decoded (U+00B0 followed by a capital C) — if the encoding ever slips, the suite says
        /// so instead of the glass.</summary>
        private static readonly string[] DialTitle = {
            "PPO2", "CABIN TEMP", "CABIN PRESSURE", "CO2",
            "LOOP A", "LOOP B", "NET PWR 1", "NET PWR 2"
        };
        private static readonly string[] DialUnit = {
            "psia", "°C", "psia", "mmHg", "°C", "°C", "W", "W"
        };
        public static string DialTitleAt(int i) { return DialTitle[i]; }
        public static string DialUnitAt(int i) { return DialUnit[i]; }

        /// <summary>§5.7's invalid value. ⛔ NOT `Dashes.None` — see the header's last paragraph and
        /// `BOB-47`; this is the one the gauge spec states, in the one place it states it.</summary>
        public const string DialNoValue = "--";

        // ==========================================================================================
        //  THE COMMAND BUDGET. ⛔ 1184 of it is the eight dials and 1080 of THAT is §4.1's dotted
        //  track — 135 dots per dial, drawn faithfully and NOT approximated by an `ArcBand`, which is
        //  what the gauge prompt asked to be measured rather than guessed. For scale: `Pages.Commands`
        //  is 480 and the busiest flown page draws 520.
        // ==========================================================================================
        public const int DialCount = 8;
        public const int Commands =
            1                                   // §8.1 the title
            + RailRows * 5                      // disc + two tick strokes + title + status
            + DialCount * DialGauge.Commands     // §5.5's eight dials
            + 1                                 // ⭐ S258 the shadow+glow, drawn FIRST
            + 1                                 // §8.6 the vehicle
            + 2 + 4 * 2                          // §8.3 header + rule + four label/value pairs
            + PanelRows * 4                     // §5.6 track + fill + label + value
            + 3 * 7 + 3;                        // §8.4 three round rects at 7 each, three labels

        /// <summary>The whole page: `SPEC_BASE_SCREENS.md` §6/§7's ICON shell, then this content.</summary>
        public static void Draw(DisplayList dl, int w, int h, PageState s, OverviewInputs ui)
        {
            BasePageIcon.Draw(dl, w, h);
            Content(dl, w, h, s, ui);
        }

        /// <summary>The content ALONE, on whatever is already in the list. ⛔ Separate from
        /// <see cref="Draw"/> so a test can count and read this page's own commands without the
        /// shell's 97 in the way.</summary>
        public static void Content(DisplayList dl, int w, int h, PageState s, OverviewInputs ui)
        {
            BaseFit fit = BaseFit.For(w, h);

            InkLine(dl, fit, TitleCx, TitleInkTop + TitleInk * 0.5f, TitleInk,
                    TextAlign.Centre, StatusLit, Title);

            // ⛔⛔ FIRST, AND THAT IS THE ONE THING HERE THAT IS EASY TO GET WRONG. The effect is
            // LIGHT AND SHADE ON THE DECK, so it belongs UNDER EVERYTHING — not next to `Vehicle`,
            // which draws AFTER the dials. Moved down to sit beside the thing it belongs to, it would
            // lay the glow ON TOP of the `LOOP B` and `NET PWR 1` dials. ⚠ This is exactly the kind of
            // line a later session "tidies" by grouping it with `Vehicle`; the suite asserts its index
            // is lower than every rail, dial and vehicle command precisely so that tidy-up fails.
            Shadow(dl, fit);
            Rail(dl, fit, s, ui);
            Dials(dl, fit, s);
            Vehicle(dl, fit);
            Connections(dl, fit, s);
            RightPanel(dl, fit, s);
            Controls(dl, fit, ui);
        }

        // ==========================================================================================
        //  THE LEFT RAIL
        // ==========================================================================================
        private static void Rail(DisplayList dl, BaseFit fit, PageState s, OverviewInputs ui)
        {
            for (int i = 0; i < RailRows; i++)
            {
                float cy = Row1MarkerCy + i * RowPitch;
                Severity sev = RowSeverity(s, i);
                string word = RowWord(s, i);

                Marker(dl, fit, MarkerLeft + MarkerDiameter * 0.5f, cy, MarkerDiameter,
                       MarkerColour(sev, ui.ChecksComplete));

                float titleInkTop = cy - TitleTopAboveMarker;
                InkLine(dl, fit, RailTextLeft, titleInkTop + RailTitleInk * 0.5f, RailTitleInk,
                        TextAlign.Left, RailTitle, RowTitle[i]);
                // §7.2 — the status ink keys on the MARKER, not on the severity: unchecked reads
                // quiet, a completed check reads bright WHATEVER ITS OUTCOME.
                InkLine(dl, fit, RailTextLeft,
                        titleInkTop + RailTitleToStatus + RailStatusInk * 0.5f, RailStatusInk,
                        TextAlign.Left, ui.ChecksComplete ? StatusLit : StatusDim, word);
            }
        }

        /// <summary>§7.1's four-state marker: a filled disc with §8.2's diameter and a check knocked
        /// out of it in the page ground. ⚠ The glyph is measured, not specified — header (1).</summary>
        private static void Marker(DisplayList dl, BaseFit fit, float cx, float cy, float d, Rgba col)
        {
            dl.ArcBand(fit.X(cx), fit.Y(cy), 0f, fit.S(d * 0.5f), 0.0, 360.0, col);
            Rgba knock = BasePageNoIcon.Ground;
            float wStroke = fit.S(TickStroke * d);
            dl.Line(fit.X(cx + TickAx * d), fit.Y(cy + TickAy * d),
                    fit.X(cx + TickBx * d), fit.Y(cy + TickBy * d), wStroke, knock);
            dl.Line(fit.X(cx + TickBx * d), fit.Y(cy + TickBy * d),
                    fit.X(cx + TickCx * d), fit.Y(cy + TickCy * d), wStroke, knock);
        }

        /// <summary>§7.1's marker colour. ⛔ GREY IS NOT A SEVERITY — it is asked FIRST, because
        /// "how healthy" and "has it been checked" are orthogonal and the overseer conflated them
        /// once already.</summary>
        public static Rgba MarkerColour(Severity sev, bool complete)
        {
            if (!complete) return MarkerUnchecked;
            if (sev == Severity.Alarm) return DragonPalette.Alarm;
            if (sev == Severity.Caution) return MarkerCaution;
            return MarkerGo;
        }

        /// <summary>
        /// §2's seven sources. ⛔ FIVE OF SEVEN ARE `Alarms.*` CALLS AND THAT IS THE POINT: they
        /// return a severity directly, so the marker maps with zero invention — and they are the same
        /// functions the tab strip reads, which satisfies `SPEC_GAUGES.md` §7 for free.
        /// ⛔ Do not write a second classifier for these rows.
        /// </summary>
        public static Severity RowSeverity(PageState s, int row)
        {
            switch (row)
            {
                case 0: return Alarms.VehicleSeverity(s);
                case 1: return Alarms.LifeSupport(s.Cabin);
                case 2: return Alarms.Thermal(s.Cabin);
                case 3: return Alarms.PowerSeverity(s);
                case 4: return Alarms.PropellantSeverity(s);
                // §7.4 — the two DISCRETE rows report a CONFIGURATION, not a health, so their marker
                // state comes from the booleans and never from `Alarms.Band`. `Unscheduled` is an
                // ALARM, per the owner's standing rule: "all parts that have unscheduled events
                // should alarm".
                case 5: return TrunkUnscheduled(s) ? Severity.Alarm : Severity.Nominal;
                default: return ChuteUnscheduled(s) ? Severity.Alarm : Severity.Nominal;
            }
        }

        /// <summary>§7.3/§7.4's words. ⛔ The five severity rows read
        /// `VehicleSubsystemPage.SevWord` — the vocabulary ALREADY IN THE FLYING CODE. Picking
        /// anything else would create a second vocabulary for one `Severity`, which is exactly what
        /// the one-severity-source rule forbids.</summary>
        public static string RowWord(PageState s, int row)
        {
            if (row <= 4) return VehicleSubsystemPage.SevWord(RowSeverity(s, row));
            if (row == 5)
            {
                if (TrunkUnscheduled(s)) return "Unscheduled";
                if (s.TrunkFired) return "Separated";
                return s.TrunkSet ? "Armed" : "Attached";
            }
            if (ChuteUnscheduled(s)) return "Unscheduled";
            if (s.MainsReleased) return "Released";
            if (s.MainsFired) return "Mains Out";
            return s.DroguesFired ? "Drogues Out" : "Stowed";
        }

        /// <summary>
        /// ⚠ HALF OF §7.4's `Unscheduled`, AND ONLY HALF — stated so nobody reads it as the whole
        /// test. §7.4 says which phase makes an event SCHEDULED is the phase-expectation table, "and
        /// that is still open", so the phase half cannot be built. What CAN be decided without it is
        /// an ORDER violation: a trunk that has fired without ever being armed. That is unscheduled
        /// by any phase table anyone writes later, so it is safe to alarm on now, and the rest waits
        /// for the table. `BOB-50`.
        /// </summary>
        public static bool TrunkUnscheduled(PageState s) { return s.TrunkFired && !s.TrunkSet; }

        /// <summary>The same, for the chutes: mains without drogues, or a release without mains.
        /// ⚠ Also only the order half — see <see cref="TrunkUnscheduled"/>.</summary>
        public static bool ChuteUnscheduled(PageState s)
        {
            return (s.MainsFired && !s.DroguesFired) || (s.MainsReleased && !s.MainsFired);
        }

        // ==========================================================================================
        //  THE EIGHT DIALS
        // ==========================================================================================
        private static void Dials(DisplayList dl, BaseFit fit, PageState s)
        {
            for (int i = 0; i < DialCount; i++)
            {
                bool top = i < 4;
                float cx = top ? TopRowCx[i] : SecondRowCx[i - 4];
                float cy = top ? TopRowCy : SecondRowCy;
                float r = top ? TopRowR : SecondRowR;
                DialGauge.Draw(dl, fit, cx, cy, r, Reading(s, i));
            }
        }

        /// <summary>
        /// §6 — WHERE EVERY NUMBER COMES FROM. ⛔ The thresholds are `CabinLimits`, the full scales
        /// and the fractions are `CabinEnvironment`, the verdict is `Alarms.Band`. Nothing here
        /// restates a limit and no prompt may either.
        /// </summary>
        public static DialReading Reading(PageState s, int i)
        {
            DialReading d = new DialReading();
            d.Title = DialTitle[i];
            d.Unit = DialUnit[i];
            d.Loop = (i == 4 || i == 5);
            CabinReadout c = s.Cabin;
            double v;
            switch (i)
            {
                case 0:
                    v = c.Ppo2Psia; d.T = c.Ppo201;
                    d.Sev = Alarms.Band(v, CabinLimits.Ppo2Caution, CabinLimits.Ppo2Alarm); break;
                case 1:
                    v = c.CabinTempC; d.T = c.CabinTemp01;
                    d.Sev = Alarms.Band(v, CabinLimits.CabinTempCaution, CabinLimits.CabinTempAlarm); break;
                case 2:
                    v = c.PressPsia; d.T = c.Press01;
                    d.Sev = Alarms.Band(v, CabinLimits.PressCaution, CabinLimits.PressAlarm); break;
                case 3:
                    v = c.Co2MmHg; d.T = c.Co201;
                    d.Sev = Alarms.Band(v, CabinLimits.Co2Caution, CabinLimits.Co2Alarm); break;
                case 4:
                    v = c.LoopAC; d.T = c.LoopA01;
                    d.Sev = Alarms.Band(v, CabinLimits.LoopCaution, CabinLimits.LoopAlarm); break;
                case 5:
                    v = c.LoopBC; d.T = c.LoopB01;
                    d.Sev = Alarms.Band(v, CabinLimits.LoopCaution, CabinLimits.LoopAlarm); break;
                default:
                    // ⛔ NET PWR HAS NO THRESHOLD IN `CabinLimits`, so it is handed `Nominal` and
                    // `Alarms.Band` is never called on it. `Alarms.GaugeColour`'s own docstring:
                    // a gauge whose quantity has no threshold must NOT be "given an invented band to
                    // justify a colour". In this family the un-banded state IS the blue.
                    v = (i == 6) ? c.NetPwr1W : c.NetPwr2W;
                    d.Sev = Severity.Nominal;
                    // ⚠ `CabinReadout` carries no `01` fraction for these two and the watts are
                    // SIGNED. The reference reads 0.00 W at t≈0 (§1), which rules out a centred
                    // scale; the arc therefore shows MAGNITUDE against `NetPwrFullScale` and the
                    // SIGN survives in the printed value, which is the only place it can. `BOB-39`.
                    d.T = Math.Abs(v) / Cabin.NetPwrFullScale;
                    break;
            }
            if (d.T < 0.0) d.T = 0.0; else if (d.T > 1.0) d.T = 1.0;
            d.Valid = s.Valid && !double.IsNaN(v);
            d.Value = d.Valid ? Formatted(i, v) : DialNoValue;
            return d;
        }

        // ---- §5.7's TWO DECIMAL PLACES ON EVERY CHANNEL, cached ----------------------------------
        // ⛔ `DisplayList.Text`'s contract: "Pass a literal or a CACHED string. Formatting a number
        // here allocates a string every frame, on three screens, forever." So each channel keeps its
        // last value and its last string and re-formats only when the number actually changes.
        // ⚠ A NaN never reaches here — `Reading` dashes first — so the key comparison cannot be
        // defeated by NaN != NaN.
        private static readonly double[] lastValue = new double[DialCount];
        private static readonly string[] lastText = new string[DialCount];

        private static string Formatted(int i, double v)
        {
            if (lastText[i] != null && lastValue[i] == v) return lastText[i];
            lastValue[i] = v;
            lastText[i] = v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            return lastText[i];
        }

        // ==========================================================================================
        //  THE VEHICLE
        // ==========================================================================================
        /// <summary>
        /// ⭐ S258 — the baked shadow + glow, drawn at full white tint exactly like the vehicle, so the
        /// asset's own straight alpha is what composites. ⛔ Called FIRST from <see cref="Content"/> —
        /// see the note there and the constant block above for why the position is load-bearing.
        /// </summary>
        private static void Shadow(DisplayList dl, BaseFit fit)
        {
            dl.Asset(ShadowAsset, fit.X(ShadowX), fit.Y(ShadowY), fit.S(ShadowW), fit.S(ShadowH),
                     new Rgba(1f, 1f, 1f, 1f));
        }

        private static void Vehicle(DisplayList dl, BaseFit fit)
        {
            // WIDTH-FIT on the ARTWORK, not on the file — see the header block above.
            float k = VehicleW / AssetOpaqueW;
            float x = VehicleCx - VehicleW * 0.5f - AssetOpaqueX * k;
            float y = VehicleTop - AssetOpaqueY * k;
            dl.Asset(VehicleAsset, fit.X(x), fit.Y(y), fit.S(AssetW * k), fit.S(AssetH * k),
                     new Rgba(1f, 1f, 1f, 1f));
        }

        /// <summary>Where the artwork actually lands, for the clearance checks §8.5 requires to be
        /// RECOMPUTED after any move. Design px.</summary>
        public static void VehicleBox(out float left, out float top, out float right, out float bottom)
        {
            float k = VehicleW / AssetOpaqueW;
            left = VehicleCx - VehicleW * 0.5f;
            right = left + VehicleW;
            top = VehicleTop;
            bottom = top + AssetOpaqueH * k;
        }

        // ==========================================================================================
        //  `CONNECTIONS`
        // ==========================================================================================
        private static void Connections(DisplayList dl, BaseFit fit, PageState s)
        {
            InkLine(dl, fit, CnLeft, CnHeaderInkTop + CnHeaderInk * 0.5f, CnHeaderInk,
                    TextAlign.Left, RailTitle, CnHeader);
            dl.Rect(fit.X(CnRuleX0), fit.Y(CnRuleY), fit.S(CnRuleX1 - CnRuleX0), fit.S(CnRuleH),
                    CnRule);
            for (int i = 0; i < 4; i++)
            {
                float inkCy = CnRow1InkTop + i * CnRowPitch + CnRowInk * 0.5f;
                InkLine(dl, fit, CnLeft, inkCy, CnRowInk, TextAlign.Left, RailTitle, CnLabel[i]);
                InkLine(dl, fit, CnRuleX1, inkCy, CnRowInk, TextAlign.Right, StatusLit,
                        ConnectionValue(s, i));
            }
        }

        /// <summary>
        /// §7.5's values. ⚠ TWO OF THE FOUR DASH, AND FOR TWO DIFFERENT REASONS.
        ///
        /// ⛔ `Airlock` HAS NO SOURCE — chased across 131 mods, nothing exists
        /// (`FINDINGS_GAME_INSTALL.md` §12.2). Draw the row, dash the value, wire it to NOTHING.
        /// ⚠ `Nose Cone` IS actuated (`Actuator.OpenNoseShroud`) but its state is not yet surfaced
        /// into `PageState`. ⛔ That is PLUMBING, not a sourcing gap — and it is explicitly not part
        /// of this task, so the row draws and the value dashes until it is wired.
        /// </summary>
        public static string ConnectionValue(PageState s, int i)
        {
            // ⛔ A DEAD FEED DASHES ALL FOUR, AND THIS WAS FOUND ON THE RENDER, NOT REASONED. The
            // first no-feed picture printed "Docked" and "S-Band" beside eight dashed dials and a
            // rail of grey markers — a confident claim about the vehicle at the exact moment there
            // is nothing to claim it from. `Docked` is a bool and cannot be null, so the only "is
            // there a source" test it has is the feed's own, the same one `Frame58Hud` dashes its
            // six attitude readouts on. ⚠ The RAIL is different and is deliberately NOT gated here:
            // its marker already says "not checked" in §7.1's own grey, and its word is dimmed to
            // match (§7.2), so the row is not claiming anything.
            if (!s.Valid) return Dashes.None;
            if (i == 1) return s.Docked ? "Docked" : "Undocked";
            if (i == 2)
            {
                if (s.SBandLinked) return "S-Band";
                return s.CommSignal01 > 0.0 ? "Acquiring" : "No Link";
            }
            return Dashes.None;
        }

        // ==========================================================================================
        //  THE RIGHT-HAND PANEL
        // ==========================================================================================
        private static void RightPanel(DisplayList dl, BaseFit fit, PageState s)
        {
            for (int i = 0; i < PanelRows; i++)
            {
                float y = PanelRowY(i);
                string value = PanelValue(s, i);
                bool live = !string.IsNullOrEmpty(value) && value != Dashes.None;

                dl.Rect(fit.X(PanelLeft), fit.Y(y), fit.S(PanelTrackW), fit.S(PanelTrackH),
                        PanelTrack);
                if (live)
                {
                    double f = PanelFraction(s, i);
                    if (f > 0.0)
                        dl.Rect(fit.X(PanelLeft), fit.Y(y), fit.S((float)(PanelTrackW * f)),
                                fit.S(PanelTrackH), PanelFill);
                }
                InkLine(dl, fit, PanelLeft, y - PanelLabelBaseline - PanelLabelInk * 0.5f,
                        PanelLabelInk, TextAlign.Left, RailTitle, PanelLabel[i]);
                InkLine(dl, fit, PanelValueRight, y + PanelTrackH * 0.5f, PanelValueInk,
                        TextAlign.Right, StatusLit, value);
            }
        }

        /// <summary>§5.6: `pitch 68.6 within a pair`, `97.4 across a pair boundary` — ⚠ a TOTAL
        /// pitch, not an addition.</summary>
        public static float PanelRowY(int i)
        {
            float y = PanelBar1Y;
            for (int k = 1; k <= i; k++) y += (k % 2 == 0) ? PanelGroupPitch : PanelPairPitch;
            return y;
        }

        /// <summary>
        /// §9 — ⛔ HALF THIS PANEL DASHES AND THAT IS THE POINT.
        ///
        /// The Crew Dragon has ONE MMH tank and ONE NTO tank, so deciding which KSP litres are "Orbit
        /// 2 Subtank Oxidizer" is INVENTING THE NUMBER THE LABEL ASKS FOR — `S147b`, the failure this
        /// project has a rule against. §9.3.3 keeps them dashed until the owner supplies best-guess
        /// budgets; ⛔ the overseer must not invent them either.
        ///
        /// ⚠ "IS THERE A SOURCE" HAS ONE TEST AND IT ALREADY EXISTS: the matching `*Text` field being
        /// non-null. ⛔ Do not invent a second one — `PageState` is a struct, so `new PageState()`
        /// zeroes every field and a "−1 means no source" sentinel would be a lie the moment anyone
        /// default-constructed one.
        /// </summary>
        public static string PanelValue(PageState s, int i)
        {
            switch (i)
            {
                // ⚠ IDENTICAL TO ROW 2 BY DESIGN. KSP has ONE ElectricCharge pool; the real vehicle's
                // two independent power units are not modelled. ⛔ It CANNOT differ. Do not make it.
                case 0: return s.PowerUnit1Text ?? Dashes.None;
                case 1: return s.PowerUnit2Text ?? Dashes.None;
                case 2: return s.DeorbitFuelText ?? Dashes.None;
                case 3: return s.DeorbitOxText ?? Dashes.None;
                default: return Dashes.None;      // §9.3.3 — the four subtank rows
            }
        }

        /// <summary>§5.6: "EVERY BAR IS remaining / capacity". The fractions are already in the build;
        /// a row with no source draws its track and no fill, the same way §5.7's invalid dial draws
        /// its track and no arc — ⛔ an empty bar and an absent bar must not look the same.</summary>
        public static double PanelFraction(PageState s, int i)
        {
            switch (i)
            {
                case 0:
                case 1: return s.Power01;
                case 2: return s.DragonFuel01;
                case 3: return s.DragonOx01;
                default: return 0.0;
            }
        }

        // ==========================================================================================
        //  THE TWO BOTTOM CONTROLS
        // ==========================================================================================
        private static void Controls(DisplayList dl, BaseFit fit, OverviewInputs ui)
        {
            // ---- the segmented toggle: the outline box FIRST, the pill OVER it -------------------
            RoundBox(dl, fit, BoxX, BoxY, BoxW, BoxH, BoxRadius);
            float pillX = ui.CabinSelected ? BoxX + BoxW - PillW : BoxX;
            BaseBar.RoundRect(dl, fit, pillX, PillY, PillW, PillH, BoxRadius, CtrlPill);

            // ⛔ THE LABEL SPLIT IS THE PILL'S INNER EDGE — the edge that is NOT the box's own edge.
            // Splitting at the pill's START centres the unselected label UNDERNEATH the pill and
            // clips it: `CABIN` renders as `N`. ⚠ Visible in only ONE of the four states, which is
            // why all four are rendered before this is believed.
            float split = ui.CabinSelected ? pillX : pillX + PillW;
            float leftCx = (BoxX + split) * 0.5f;
            float rightCx = (split + BoxX + BoxW) * 0.5f;
            InkLine(dl, fit, leftCx, CtrlInkCy, CtrlInk, TextAlign.Centre,
                    ui.CabinSelected ? CtrlUnselectedInk : CtrlSelectedInk, LabelSystems);
            InkLine(dl, fit, rightCx, CtrlInkCy, CtrlInk, TextAlign.Centre,
                    ui.CabinSelected ? CtrlSelectedInk : CtrlUnselectedInk, LabelCabin);

            // ---- MORE, in the SAME band, mirrored ------------------------------------------------
            if (ui.MoreActive)
                BaseBar.RoundRect(dl, fit, MoreX, BoxY, MoreW, BoxH, BoxRadius, CtrlPill);
            else
                RoundBox(dl, fit, MoreX, BoxY, MoreW, BoxH, BoxRadius);
            InkLine(dl, fit, MoreX + MoreW * 0.5f, CtrlInkCy, CtrlInk, TextAlign.Centre,
                    ui.MoreActive ? CtrlSelectedInk : CtrlUnselectedInk, LabelMore);
        }

        /// <summary>An outline box: §8.4.2's stroke with §8.4.2's infill inside it. ⭐ Drawn as two
        /// round rects, the border-box way `BaseBar`'s pop-up already does it — the edge is INSIDE
        /// the box, and there is no seam because the inner shape simply covers the middle.</summary>
        private static void RoundBox(DisplayList dl, BaseFit fit, float x, float y, float w, float h,
                                     float r)
        {
            BaseBar.RoundRect(dl, fit, x, y, w, h, r, CtrlStroke);
            BaseBar.RoundRect(dl, fit, x + BoxStroke, y + BoxStroke, w - 2f * BoxStroke,
                              h - 2f * BoxStroke, r - BoxStroke, CtrlInfill);
        }

        // ==========================================================================================
        //  ONE LINE OF TYPE, PLACED BY ITS INK
        // ==========================================================================================
        /// <summary>
        /// ⛔ THE SPECS ARE WRITTEN IN INK HEIGHTS AND `DisplayList.Text` TAKES A PIXEL SIZE, so the
        /// conversion happens HERE and exactly once. Both constants live in `Typography` and both
        /// were measured off a render rather than derived — see `Typography.CapHeightOfSize`.
        /// ⛔ ASSERT THE INK, NEVER THE FONT SIZE: the two renderers' font metrics differ, and the
        /// device check measures the ink this produces rather than trusting the arithmetic.
        /// </summary>
        private static void InkLine(DisplayList dl, BaseFit fit, float x, float inkCy, float ink,
                                    TextAlign align, Rgba col, string text)
        {
            if (string.IsNullOrEmpty(text) || ink <= 0f) return;
            float px = Typography.SizeForInk(ink);
            dl.Text(text, fit.X(x), fit.Y(inkCy - Typography.CapCentreOfTop * px), fit.S(px),
                    align, col);
        }
    }
}
