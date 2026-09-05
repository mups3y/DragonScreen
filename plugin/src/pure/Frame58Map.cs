// DragonScreen — Frame58Map  (PURE: where every element of Figma "Frame 58" actually is)
// ============================================================================================
// [[S154a]], 2026-09-06. Step one of [[S154]]: the HUD is one baked raster and all twelve of its
// numbers are painted into it (QC `H-02`). Before a single one can be redrawn live, we need to know
// where each sits — in OUR design frame, not in someone's screenshot. This file is that answer, and
// it draws nothing. [[S154b]] / [[S154c]] / [[S154d]] are the lines that draw.
//
// ⭐ THE HEADLINE, AND IT IS BETTER NEWS THAN THE LINE EXPECTED: THERE IS NO MAPPING TO DERIVE.
// `assets/figma/dashboard_ui/Frame 58.svg` is `viewBox="0 0 3427 2112"` — the SAME 3427x2112 design
// frame `Frame58Hud` already draws in. So the transform from the frame's own source to our
// coordinates is the IDENTITY, and every number below is read straight off the drawing rather than
// fitted. C7.1 permits exactly this use of `assets/`: look, do not ship. We keep shipping the PNG.
//
// ⛔ AND THE ROUTE S154 SCOUTED IS A DEAD END — SAID PLAINLY, BECAUSE I AM THE ONE WHO SCOUTED IT.
// The plan was to map the Vue app's CSS percentages into this frame. Two things kill that:
//
//   1. The percentages are not the page's. `#roll-number` and its siblings live in `.hud-measures`,
//      which has NO CSS rule at all — so it is `position: static`, it is NOT a containing block, and
//      the nearest positioned ancestor is `#hud-ring` (`Second.vue:32`, `:1596`). Their percentages
//      resolve against THAT square, not the page. The scouting note read them as page-relative
//      siblings of `#lower-left`; the markup at `Second.vue:173-176` says otherwise.
//   2. Even read correctly they do not reproduce this drawing. Solving for the containing block from
//      the ROLL and YAW numbers' measured centres gives a box of height 1360.03 at y 303.88 — and
//      that box then puts PITCH's `top:50%` at y 983.89 when the drawing has it at 966.53, 17.4 px
//      out, with `left:86%` 42 px out. No single box satisfies all three.
//
// ⭐ So the scouting's CONCLUSION was right — "the Figma frame is not a pixel-exact render of the Vue
// app" — while its stated reason (a 72 px vertical disagreement between page centre and bowl centre)
// was measuring the wrong thing. The CSS keeps exactly one job here: it says WHICH readout is which,
// and that is how the names below were assigned. It supplies no geometry.
//
// ---- THE TWO ANCHORS, AND WHY THEY ARE ENOUGH -------------------------------------------------
// The line called for two independent anchors. Both are here and both hold:
//
//   ANCHOR 1 — THE BOWL. The SVG's `<circle cx="1706.86" cy="984.697">` is the attitude bowl.
//   `Frame58Hud.BowlCx/BowlCy` have carried 1706 / 984 since they were taken from the frame
//   metadata. They agree to 0.86 px and 0.70 px — an independent confirmation of a constant this
//   build has been drawing the docking camera into for weeks.
//
//   ANCHOR 2 — THE RING, MEASURED IN THE RASTER WE ACTUALLY SHIP. `frame58.png` (2048x1263) was
//   circle-fitted over its 8285 bright pixels (5105 inliers after trimming): centre (1704.93,
//   986.22) design, outer radius 585.33 across / 584.94 down. The SVG's ring path samples to two
//   radii, 549.83 inner and 585.48 outer, about (1706.30, 986.28). **Radius agrees to 0.15 px and
//   centre to 1.37 px.** That is what proves the PNG on disk and the SVG being measured are one
//   drawing — without it, every box below would be geometry from a file we do not render.
//
// ---- AND THEN EVERY BOX WAS CHECKED AGAINST THE RASTER, NOT JUST THE TWO ANCHORS ---------------
// Each box below was re-measured by thresholding ink in `frame58.png` inside that box's own window
// and comparing. **17 of 18 agree within 1.7 design px.** The 18th, `PitchLabel`, came out 8.09 px
// wide of its x0 — not a mapping error: the outer arc segment overlaps its window, so a colour
// threshold cannot separate label from arc. Narrowing the search pad from 7 px to 3 px removes the
// arc and the disagreement falls to 0.94 px. Recorded rather than quietly dropped.
//
// ⚠ WHAT THESE BOXES ARE. Each is the INK bounding box of the baked artwork — the extent of the
// glyphs as drawn, with the sample values the export happened to contain ("15.0deg", "200.0 m"). It
// is NOT a text box and NOT a baseline. A live readout replacing one has to be placed by its own
// metric — typically centred on `Cx`/`Cy` — because live text is a different string of a different
// width.
// ⛔ In particular, do NOT read `H` as a font size. `RollValue` is 26.25 tall for digits with no
// descender; the em is larger, and [[S153]]'s floor applies to the TYPE, not to this box.
// ============================================================================================
namespace DragonScreen
{
    /// <summary>Where Frame 58's baked elements are, in the 3427x2112 design frame. Read off
    /// `assets/figma/dashboard_ui/Frame 58.svg`, whose viewBox IS that frame, and verified against
    /// `art/cover/frame58.png`. Geometry only — this type draws nothing and knows no vessel.</summary>
    public static class Frame58Map
    {
        /// <summary>The design frame these numbers live in — the SVG's own viewBox, and the same
        /// `RefW`/`RefH` `Frame58Hud` fits the raster to.</summary>
        public const float RefW = 3427f, RefH = 2112f;

        /// <summary>ANCHOR 1: the attitude bowl's centre, from the SVG's own circle. ⚠ `Frame58Hud`
        /// keeps its own rounded `BowlCx`/`BowlCy` (1706/984) and this does NOT replace them — it is
        /// the independent measurement that confirms them, which is why it is stated to 2 dp
        /// here.</summary>
        public const float BowlCx = 1706.86f, BowlCy = 984.697f;

        /// <summary>ANCHOR 2: the HUD ring. Centre and the two radii of its band, from the SVG; the
        /// shipped raster's own circle fit puts the outer edge within 0.15 px of `RingOuterR`.</summary>
        public const float RingCx = 1706.30f, RingCy = 986.28f;
        public const float RingInnerR = 549.83f, RingOuterR = 585.48f;

        /// <summary>The raster we ship, and the scale that carries it into the design frame. ⚠ The
        /// two axes differ in the 4th decimal (1.673340 vs 1.672209) because 2048x1263 is not exactly
        /// the frame's 1.62263 aspect — 0.068 %, under a tenth of a pixel over the ring's diameter,
        /// but it is why the verification above quotes x- and y-scaled radii separately.</summary>
        public const int RasterW = 2048, RasterH = 1263;

        /// <summary>An ink bounding box in design-frame units. ⛔ Ink, not a text box — see the
        /// header.</summary>
        public struct Box
        {
            public readonly float X0, Y0, X1, Y1;
            public Box(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
            public float W { get { return X1 - X0; } }
            public float H { get { return Y1 - Y0; } }
            public float Cx { get { return (X0 + X1) * 0.5f; } }
            public float Cy { get { return (Y0 + Y1) * 0.5f; } }
        }

        // ---- THE TWELVE BAKED NUMBERS (QC `H-02`'s count, arrived at independently here) ----------
        // Six attitude — [[S154b]]. The values are green (#1FE327), the rates cyan (#20FBFD); that
        // colour split is the drawing's own and is how value was told from rate.
        public static readonly Box RollValue  = new Box(1668.31f,  460.45f, 1745.37f,  486.70f);
        public static readonly Box RollRate   = new Box(1674.77f,  504.26f, 1736.04f,  520.66f);
        public static readonly Box PitchValue = new Box(2114.90f,  953.13f, 2206.32f,  979.38f);
        public static readonly Box PitchRate  = new Box(2130.26f,  997.58f, 2191.53f, 1013.98f);
        public static readonly Box YawValue   = new Box(1661.81f, 1446.48f, 1753.23f, 1472.73f);
        public static readonly Box YawRate    = new Box(1674.77f, 1490.94f, 1736.04f, 1507.34f);

        // Six translation — [[S154c]]. ⚠ X/Y/Z are LEFT-aligned in the drawing (all three share
        // x0 1173.4 +/- 0.1 and have different widths), so a live value replaces them from the LEFT
        // edge, not from `Cx`. RANGE and RATE are their own runs. ACCELERATION is the big dial value.
        public static readonly Box XValue     = new Box(1173.46f,  942.07f, 1255.59f,  958.59f);
        public static readonly Box YValue     = new Box(1173.51f,  977.15f, 1242.24f,  993.67f);
        public static readonly Box ZValue     = new Box(1173.42f, 1011.14f, 1246.20f, 1027.66f);
        public static readonly Box RangeValue = new Box(1303.06f, 1285.51f, 1381.54f, 1302.03f);
        public static readonly Box RateValue  = new Box(2015.79f, 1288.03f, 2126.67f, 1304.54f);
        public static readonly Box AccelValue = new Box( 656.91f,  344.71f,  880.95f,  419.36f);

        // ---- THE STATIC LABELS BESIDE THEM -------------------------------------------------------
        // Not readouts and not S154b/c's business to redraw — they are here so a live value can be
        // positioned RELATIVE to the label that names it, and so that a later pass can tell at a
        // glance which pixels are already correct. ⚠ `PitchLabel` is the vertical P-I-T-C-H stack,
        // which is why it is 14.4 wide and 112.1 tall.
        public static readonly Box RollLabel  = new Box(1678.37f,  405.78f, 1735.25f,  422.09f);
        public static readonly Box PitchLabel = new Box(2270.45f,  932.32f, 2284.86f, 1044.40f);
        public static readonly Box YawLabel   = new Box(1679.79f, 1547.50f, 1729.31f, 1563.58f);
        public static readonly Box RangeLabel = new Box(1311.17f, 1250.16f, 1372.92f, 1264.66f);
        public static readonly Box RateLabel  = new Box(2046.17f, 1250.26f, 2090.23f, 1264.56f);
        public static readonly Box AccelLabel = new Box( 666.17f,  287.62f,  877.73f,  308.19f);
    }
}
