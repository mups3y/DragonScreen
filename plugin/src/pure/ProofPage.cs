/*
 * DragonScreen - ProofPage
 *
 * SCAFFOLDING. The render-path test pattern, now expressed as a page: it fills a DisplayList and
 * knows nothing about GL, System.Drawing, Unity or KSP. That is the point - the same description
 * draws on the IVA screen and into a PNG, so the two can be compared.
 *
 * It answers, in one game start, every question the render path can fail on:
 *
 *   anything at all appears  -> the RenderTexture reaches the material
 *   the arc SWEEPS           -> it is live, not a texture captured once
 *   red block / green block  -> which way up and which way round the mesh UVs run
 *   circle inside square     -> whether the render target aspect matches the physical screen
 *   N bars                   -> which transform is which display
 *
 * All five have now been answered - see CLAUDE.md, "The render path - FLOWN AND PROVEN". This stays
 * only until a real page draws here, and is then DELETED. It is also the first customer for the
 * display list, which is why it moved into src/pure rather than being rewritten.
 */
namespace DragonScreen
{
    public static class ProofPage
    {
        /// <summary>Commands this page can emit. Sized from the worst case below, with headroom.</summary>
        public const int Commands = 48;

        /// <summary>
        /// THE LEGIBILITY RAMP. The same string at each of these pixel sizes, down the left of the
        /// page, each labelled with its own size.
        ///
        /// This is the single most valuable thing one game load can buy right now. Every page design
        /// decision downstream depends on the smallest size that is still readable ON THE GLASS, at
        /// IVA distance, through perspective and cabin lighting - and that number cannot be guessed,
        /// cannot be derived from the render target resolution, and cannot be judged from the PNG
        /// preview, which is seen flat and head-on.
        /// </summary>
        public static readonly float[] LegibilitySizes = { 12f, 16f, 20f, 28f, 40f };

        /// <summary>
        /// Build the pattern into <paramref name="dl"/> at the given render-target size.
        ///
        /// PHASE IS PASSED IN, NOT READ. A pure page cannot reach for Unity's clock, and there is a
        /// second reason that matters more: the preview must be able to render a chosen, repeatable
        /// frame. A page that sampled time internally could not be diffed against itself.
        /// </summary>
        /// <param name="phase01">Sweep position, 0..1.</param>
        /// <param name="label">
        /// Pre-built caption, e.g. "SCREEN 1   1280x703". CACHED BY THE CALLER, never built here -
        /// concatenating it per frame would allocate a string every frame on three screens, which is
        /// exactly the rule DisplayList.Text states and the first place it would have been broken.
        /// </param>
        public static void Build(DisplayList dl, int w, int h, int screenIndex, double phase01,
                                 string label)
        {
            if (dl == null || w <= 0 || h <= 0) return;

            // No background fill: in game the camera clears to DragonPalette.Background, and the
            // preview clears to the same colour. Drawing it here would be a third place to keep in
            // step for no gain.

            float border = 6f;
            dl.Rect(0f, 0f, w, border, DragonPalette.Hairline);
            dl.Rect(0f, h - border, w, border, DragonPalette.Hairline);
            dl.Rect(0f, 0f, border, h, DragonPalette.Hairline);
            dl.Rect(w - border, 0f, border, h, DragonPalette.Hairline);

            // ORIENTATION. Two colours, two opposite corners, two different shapes. If red shows up
            // bottom-right the texture is flipped in Y; if they swap sides it is flipped in X.
            // A symmetric pattern could not have told us, which is why this one is not.
            dl.Rect(border, border, 160f, 80f, DragonPalette.Alarm);
            dl.Rect(w - border - 120f, h - border - 120f, 120f, 120f, DragonPalette.Go);

            // ASPECT. A square with a circle inscribed. A stretched target turns the circle into an
            // ellipse long before the eye notices anything wrong with a layout - which is exactly
            // how the original 1024x640 was caught.
            float side = h * 0.5f;
            float cx = w * 0.5f, cy = h * 0.5f;
            dl.Box(cx - side * 0.5f, cy - side * 0.5f, side, side, 2f, DragonPalette.Text8);
            dl.ArcBand(cx, cy, side * 0.5f - 2f, side * 0.5f, 0.0, 360.0, DragonPalette.Panel);

            // LIVENESS.
            double end = ArcGeometry.ValueToAngle(phase01, 0.0, 1.0, -120.0, 120.0);
            dl.ArcBand(cx, cy, side * 0.34f, side * 0.44f, -120.0, end, DragonPalette.Accent);

            // IDENTITY. N bars for screen N.
            for (int i = 0; i < screenIndex; i++)
                dl.Rect(border + 16f + i * 44f, h - border - 76f, 28f, 60f, DragonPalette.AccentDim);

            // ---- TEXT ----
            // Caption top-centre, in the accent, so a glance says which screen this is without
            // counting bars.
            dl.Text(label, w * 0.5f, border + 14f, 24f, TextAlign.Centre, DragonPalette.Accent);

            // THE LEGIBILITY RAMP. Each line labelled with its own size, so a photograph of the
            // screen answers "how small can text be" directly rather than by estimation. Drawn in
            // Text2 - a mid-brightness grey from the real ladder - because judging legibility against
            // pure white would flatter every size and give an answer we could not use.
            float ty = h * 0.30f;
            for (int i = 0; i < LegibilitySizes.Length; i++)
            {
                float px = LegibilitySizes[i];
                dl.Text(SizeLabels[i], border + 24f, ty, px, TextAlign.Left, DragonPalette.Text2);
                ty += px + 10f;
            }
        }

        /// <summary>
        /// Ramp captions, pre-built and static. One per LegibilitySizes entry, and they must stay in
        /// step - the headless tests check that they do, because a ramp that mislabels its own sizes
        /// answers the question wrongly and looks entirely convincing while doing it.
        /// </summary>
        public static readonly string[] SizeLabels = {
            "12 px  ALTITUDE 123.4 km",
            "16 px  ALTITUDE 123.4 km",
            "20 px  ALTITUDE 123.4 km",
            "28 px  ALTITUDE 123.4 km",
            "40 px  ALTITUDE 123.4"
        };
    }
}
