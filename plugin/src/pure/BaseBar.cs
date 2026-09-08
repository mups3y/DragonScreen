/*
 * DragonScreen — BaseBar : the bottom bar of the two BASE SCREENS, and the design→device fit both
 * base pages are drawn through.
 *
 * ⛔⛔ THIS IS NOT `BottomBar`. IT IS A DIFFERENT BAR AND THEY MUST NEVER MEET.
 * Built from `SPEC_BASE_SCREENS.md` §2, §8 and §9 ALONE — no line of this file was read from, copied
 * from, or patterned on `plugin/src/pure/BottomBar.cs`, which keeps drawing untouched for the pages
 * the owner flies. ⛔ OWNER'S INSTRUCTION, 2026-09-09, verbatim: *"he should only reference what you
 * tell him too regarding building the shells how you tell him too. not the old builds."*
 *
 * ⛔ THE FOUR DIFFERENCES, WRITTEN DOWN SO A LATER SESSION CANNOT "UNIFY" THE TWO BY MISTAKE:
 *   · the old bar has `bar_cap_left/right.png` — CURVED FRAME CORNERS. ⛔ THIS BAR HAS NO CURVES.
 *   · the old bar draws a border — side rules, a bottom rule, a top rule cap to cap.
 *     ⛔ THIS BAR HAS NO BORDER. Two 1px vertical rules is the complete list of lines on it.
 *   · the old bar has its own 3427 x 2112 reference frame and its own `Scale(h)`.
 *     ⛔ THIS BAR IS IN 1920 x 1054 AND §2's SINGLE `k`.
 *   · the old bar's ground is `DragonPalette.Panel` (`#111B52`). ⛔ THIS BAR DRAWS NO GROUND AT ALL —
 *     the page ground `#1A1F35` shows through.
 * ⚠ The owner had to have the curves deleted once already; they came back because a prompt pointed a
 * build at the old class. Ten tiles is not eleven tiles: they are two different bars.
 *
 * ---- WHY `BaseFit` LIVES IN THIS FILE ----
 * ⭐ The fit is needed by BOTH base pages and by this bar, and this is the file both pages share
 * (Prompt 3's ICON page reuses `BaseBar` unchanged). Putting the one mapping in the one shared file
 * is what keeps a second copy of `min(w/1920, h/1054)` from appearing — and a second copy is exactly
 * how a per-axis scale gets reintroduced later without anyone deciding to.
 *
 * ---- THE ASSETS ----
 * The ten tiles are re-cuts supplied with the spec (§8.2) and harvested into
 * `plugin/GameData/DragonScreen/art/cover/` by this task. ⛔ They are NOT the shipped `bar_nav_*` /
 * `bar_label_*` / `bar_value_*` / `bar_comm_block` set: nine of those are fully opaque with `#111B52`
 * baked in, and a tint cannot repair one — a tint is a MULTIPLY in both renderers, so it only darkens,
 * and `#111B52` → `#1A1F35` needs the RED CHANNEL TO RISE. (That measurement is this build's own, from
 * the discarded S244 working copy; §8.2 carries it and the re-cuts exist because of it.)
 */
using System;

namespace DragonScreen
{
    /// <summary>
    /// §2 — THE DESIGN FRAME AND THE FIT. One factor for BOTH axes; the mapped frame is CENTRED and
    /// the spare pixels are split equally.
    ///
    /// ⛔ NEVER A PER-AXIS SCALE. `k = min(w/1920, h/1054)` is the whole rule, and the reason it is a
    /// `min` and not two factors is that the three screens are not the same shape: screen 2 is 0.98 %
    /// narrower than screens 1 and 3 (2560x1419 against 2560x1405), so a per-axis fit would put a 1 %
    /// vertical stretch on the middle screen and turn every ring gauge on it into an ellipse.
    /// ⭐ The spare pixels are REAL and visible: at 2560x1419 the frame is width-limited and leaves a
    /// band of about 6.8 px at top and bottom.
    /// </summary>
    public struct BaseFit
    {
        /// <summary>§2 — the design frame everything in the base-screen spec is expressed in.</summary>
        public const float FrameW = 1920f;
        public const float FrameH = 1054f;

        public float K, OffX, OffY;

        public static BaseFit For(int w, int h)
        {
            BaseFit f = new BaseFit();
            f.K = (float)Math.Min(w / FrameW, h / FrameH);
            f.OffX = (w - FrameW * f.K) * 0.5f;
            f.OffY = (h - FrameH * f.K) * 0.5f;
            return f;
        }

        /// <summary>A design x in device pixels.</summary>
        public float X(float dx) { return OffX + dx * K; }
        /// <summary>A design y in device pixels.</summary>
        public float Y(float dy) { return OffY + dy * K; }
        /// <summary>A design LENGTH in device pixels — no origin, so it is not an X or a Y.</summary>
        public float S(float d) { return d * K; }
    }

    /// <summary>
    /// §8 — the bar. A flat band on the page ground: ten image tiles, two 1px rules, one pop-up.
    /// ⛔ Nothing else exists on it. No frame, no caps, no rule cap-to-cap, no ground of its own.
    /// </summary>
    public static class BaseBar
    {
        /// <summary>Worst case: 10 tiles + 2 rules + the pop-up's 14 shapes and 2 text lines.</summary>
        public const int Commands = 28;

        // ---- §8.1 THE BAND ----------------------------------------------------------------------
        public const float BandTop = 997.4f;
        public const float BandBottom = 1054f;      // the design frame's own bottom edge
        public const float BandHeight = 56.6f;

        // ---- §8.1 THE TWO VERTICAL RULES — the only lines on this bar ---------------------------
        public const float RuleTop = 989f;
        public const float RuleHeight = 54.9f;
        public const float RuleWidth = 1f;
        public const float RuleXLeft = 820.2f;
        public const float RuleXRight = 1088.6f;

        // ---- §9 THE EVENT POP-UP ----------------------------------------------------------------
        public const float PopLeft = 832.5f;
        public const float PopTop = 989f;
        public const float PopWidth = 243.9f;
        public const float PopHeight = 55f;
        public const float PopRadius = 2.5f;
        public const float PopBorder = 1f;          // border-box: the 1px edge is INSIDE the box
        public const float PopTextPx = 18f;
        public const float PopLineHeight = 17.5f;

        // ---- §3 COLOURS. Every value measured from source; do not "improve" them. ---------------
        public static readonly Rgba PopFill = Rgba.Hex("1D2C4D");
        public static readonly Rgba PopEdge = Rgba.Hex("334970");
        public static readonly Rgba Ink = new Rgba(1f, 1f, 1f, 1f);

        /// <summary>
        /// §8.1's ten tiles, in the order they draw. ⛔ `b_nav0` FIRST and its selection marker is
        /// BAKED INTO THE ASSET — a solid white bar at design left 17.3, width 61.7, top 1043.9,
        /// height 10.1, flush to the frame's bottom edge at 1054.
        ///
        /// ⛔ FOR THE SHELL THE MARKER IS STATIC, UNDER NAV 0, EXACTLY AS BAKED (§8.1). The shells
        /// have no navigation wired (§10.2), so there is nothing for it to follow: do not extract it,
        /// do not redraw it, do not make it move.
        /// ⚠ KNOWN FUTURE WORK, NOT THIS COMMIT'S: when navigation IS wired, the marker comes out of
        /// the tile and becomes a drawn `Rect` at that geometry, with the DRAW rectangle and the HIT
        /// rectangle from ONE EXPRESSION so neither can move without the other.
        /// </summary>
        private static readonly string[] TileKey = {
            "b_nav0", "b_nav1", "b_nav2", "b_nav3", "b_nav4",
            "b_state", "b_point", "r_spx", "r_iss", "r_count"
        };
        private static readonly float[] TileLeft = {
            13.4f, 102.0f, 176.5f, 243.2f, 316.0f, 615.2f, 1103.1f, 1429.2f, 1669.0f, 1807.5f
        };
        private static readonly float[] TileTop = {
            997.4f, 997.4f, 997.4f, 997.4f, 997.4f, 997.4f, 997.4f, 998.5f, 998.5f, 998.5f
        };
        private static readonly float[] TileWidth = {
            69.5f, 35.9f, 28.0f, 40.3f, 38.1f, 192.7f, 85.2f, 191.2f, 94.2f, 99.1f
        };
        private static readonly float[] TileHeight = {
            56.6f, 56.6f, 56.6f, 56.6f, 56.6f, 56.6f, 56.6f, 35.9f, 35.9f, 35.9f
        };

        /// <summary>How many tiles the bar draws — the §10 image count, from the table itself.</summary>
        public static int TileCount { get { return TileKey.Length; } }
        public static string Key(int i) { return TileKey[i]; }

        /// <summary>
        /// Draw the bar into <paramref name="dl"/>, mapped through <paramref name="fit"/>.
        ///
        /// ⭐ THE POP-UP IS WIRED AND DRAWS NOTHING WHEN THERE IS NO EVENT, AND THAT IS THE CORRECT
        /// STATE, not a missing feature. Owner: *"have them all ready to go and wired in so when the
        /// prompts arrive they are ready."*
        /// ⚠ "draws nothing" and "is not connected" produce IDENTICAL display lists, so the only
        /// honest proof is to DRIVE A VALUE IN AND WATCH IT APPEAR — which is what
        /// `BasePageNoIconTest.TheEventPopUpIsWiredNotAbsent` does.
        /// ⛔ The words in the reference render — "Trunk Jettison and Deorbit / Burn Enabled" — are
        /// SAMPLE TEXT sizing the box, not content. No string is shipped here.
        /// </summary>
        public static void Draw(DisplayList dl, BaseFit fit, string eventLine1, string eventLine2)
        {
            // ---- the ten tiles, in the order given, drawn AS CUT ----
            // Opaque white is the identity tint: "as drawn". The tiles carry their own colour (the SPX
            // and countdown blocks are cyan ink, measured, not a tint applied here).
            for (int i = 0; i < TileKey.Length; i++)
                dl.Asset(TileKey[i], fit.X(TileLeft[i]), fit.Y(TileTop[i]),
                         fit.S(TileWidth[i]), fit.S(TileHeight[i]), Ink);

            // ---- the two vertical rules ----
            dl.Rect(fit.X(RuleXLeft), fit.Y(RuleTop), fit.S(RuleWidth), fit.S(RuleHeight), Ink);
            dl.Rect(fit.X(RuleXRight), fit.Y(RuleTop), fit.S(RuleWidth), fit.S(RuleHeight), Ink);

            // ---- the event pop-up ----
            DrawEvent(dl, fit, eventLine1, eventLine2);
        }

        /// <summary>
        /// §9 — the pop-up. It spans exactly the rows the two rules occupy (989..1044); the 9px gaps
        /// above and below it are the bar's own measured rhythm, not a chosen number.
        /// </summary>
        private static void DrawEvent(DisplayList dl, BaseFit fit, string line1, string line2)
        {
            bool has1 = !string.IsNullOrEmpty(line1);
            bool has2 = !string.IsNullOrEmpty(line2);
            if (!has1 && !has2) return;                       // ⛔ no event: NOTHING is drawn, box included

            // border-box: the edge is drawn full size, the fill inset by the 1px border.
            RoundRect(dl, fit, PopLeft, PopTop, PopWidth, PopHeight, PopRadius, PopEdge);
            RoundRect(dl, fit, PopLeft + PopBorder, PopTop + PopBorder,
                      PopWidth - 2f * PopBorder, PopHeight - 2f * PopBorder,
                      PopRadius - PopBorder, PopFill);

            // Centred on BOTH axes, off the line-height — so one line centres as surely as two.
            int lines = (has1 ? 1 : 0) + (has2 ? 1 : 0);
            float blockTop = PopTop + (PopHeight - lines * PopLineHeight) * 0.5f;
            float cx = PopLeft + PopWidth * 0.5f;
            float y = blockTop;
            if (has1)
            {
                dl.Text(line1, fit.X(cx), fit.Y(y), fit.S(PopTextPx), TextAlign.Centre, Ink);
                y += PopLineHeight;
            }
            if (has2)
                dl.Text(line2, fit.X(cx), fit.Y(y), fit.S(PopTextPx), TextAlign.Centre, Ink);
        }

        // ==========================================================================================
        //  ⭐⭐ THE SEAM OVERLAP, AND IT IS NOT A FUDGE — IT IS A MEASURED DEFECT'S FIX.
        //
        //  ⛔ TWO ANTIALIASED FILLS OF THE SAME COLOUR THAT MERELY ABUT DO NOT ADD UP TO ONE. Where
        //  their shared edge lands mid-pixel, each covers part of that row - say 0.33 and 0.67 - and
        //  the compositor gives 1 - (1-0.33)(1-0.67) = 0.78, not 1. The row keeps 22 % of whatever was
        //  underneath, and that reads as a HAIRLINE the full width of the shape.
        //
        //  ⚠ MEASURED, NOT FEARED. Before this existed, the border's fill carried a line across the
        //  whole page at design y 13 - rgb(45,46,53) against `#070810`'s rgb(7,8,16), a 15 % step,
        //  because the colour showing through was the white stroke beneath. The window fill carried a
        //  fainter one at design y 29.5. Both are in the render `--basecheck` probes.
        //
        //  ⭐ THE FIX IS AN OVERLAP OF ONE DESIGN PIXEL, applied ONLY where the overlapping region is
        //  provably INSIDE the shape - a band pushed into the middle of its own rectangle, a
        //  quarter-disc swept just past its own end. It changes no edge of any shape.
        //  ⛔ IT IS NEVER APPLIED TO AN ALPHA FILL. Two 0.55 whites overlapping composite to 0.80 and
        //  the corner would render BRIGHTER than the straight run beside it - which is why §6's window
        //  RING is drawn as butted bands and quarter-annuli with no overlap at all, and wears an
        //  invisible 1px notch at each of its eight junctions instead of a visible bright one.
        // ==========================================================================================
        internal const float Seam = 1f;

        /// <summary>
        /// How far past its own end a quarter-disc of this radius must sweep for its rim to reach
        /// <see cref="Seam"/> design pixels into the band it abuts. Degrees.
        /// </summary>
        internal static double SweepOver(float r)
        {
            if (r <= 0f) return 0.0;
            double s = Seam / r;
            if (s > 0.5) s = 0.5;                       // a small radius gets 30 deg and no more
            return Math.Asin(s) * 180.0 / Math.PI;
        }

        /// <summary>
        /// ⭐ THE ROUNDED RECTANGLE, THE ONE CONSTRUCTION (spec §0(a)): 3 `Rect`s — a full-width band
        /// inset by r top and bottom, plus the two end bands — and 4 `ArcBand` quarter-discs at the
        /// corners. ⛔ Do not invent a second one; `BasePageNoIcon` calls THIS.
        ///
        /// The angles are the project's ONE instrument convention: 0 at twelve o'clock, increasing
        /// clockwise (`ArcGeometry.ToMathRadians`). So 270..360 is the top-LEFT quadrant.
        ///
        /// ⭐ The two end bands reach <see cref="Seam"/> INTO the middle one and each disc sweeps
        /// <see cref="SweepOver"/> past both its ends, for the reason written above. Every one of
        /// those overlaps lies inside the shape, so the outline is unchanged.
        /// </summary>
        internal static void RoundRect(DisplayList dl, BaseFit fit,
                                       float x, float y, float w, float h, float r, Rgba col)
        {
            if (r > w * 0.5f) r = w * 0.5f;
            if (r > h * 0.5f) r = h * 0.5f;
            float ov = Seam < r ? Seam : r;
            double sw = SweepOver(r);
            dl.Rect(fit.X(x), fit.Y(y + r), fit.S(w), fit.S(h - 2f * r), col);
            dl.Rect(fit.X(x + r), fit.Y(y), fit.S(w - 2f * r), fit.S(r + ov), col);
            dl.Rect(fit.X(x + r), fit.Y(y + h - r - ov), fit.S(w - 2f * r), fit.S(r + ov), col);
            dl.ArcBand(fit.X(x + r), fit.Y(y + r), 0f, fit.S(r), 270.0 - sw, 360.0 + sw, col);
            dl.ArcBand(fit.X(x + w - r), fit.Y(y + r), 0f, fit.S(r), 0.0 - sw, 90.0 + sw, col);
            dl.ArcBand(fit.X(x + w - r), fit.Y(y + h - r), 0f, fit.S(r), 90.0 - sw, 180.0 + sw, col);
            dl.ArcBand(fit.X(x + r), fit.Y(y + h - r), 0f, fit.S(r), 180.0 - sw, 270.0 + sw, col);
        }
    }
}
