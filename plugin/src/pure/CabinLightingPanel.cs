// DragonScreen — CabinLightingPanel  (PURE: Frame 66's LIGHTING panel, rebuilt over the baked one)
// ============================================================================================
// [[S134d]] / QC `F-03`, 2026-09-06.
//
// ---- ⛔ WHAT THE BAKED PANEL SAYS, AND WHY ALL OF IT IS WRONG ----
// Four faults, all of them pixels in `frame66.png`, all visible in `ui_cabin.png`:
//   1. three of the four column headings read `CABIN DISPLAYS`, identically;
//   2. `- DISPLAY 3` appears TWICE in columns 1, 2 and 3, and there is no `DISPLAY 4` — the rows read
//      1, 2, 3, 3;
//   3. column 4 has three rows and ends higher than the other three, so the columns do not align;
//   4. the caption ends mid-clause: *"Tap to disable display"* / *"or"*, and stops.
//
// ⛔ AND THE CONTROLS SHOULD NOT BE THERE AT ALL. `SettingsPage`'s own header records the finding, in
// the code, from the config: *"the pod carries exactly ONE ModuleColorChanger, on the Light action
// group. There is no Back light, no Tip light, no per-zone anything to bind to. Drawing eight buttons
// where seven do nothing is the dead-control failure this project refuses."* That was about EIGHT
// buttons. This panel draws **fifteen** lighting rows — and an instruction to tap them, on a page with
// no hit test. ⚠ An instruction to tap is the strongest form of the dead-control defect: it does not
// merely look interactive, it SAYS it is.
//
// ---- ⭐ SO THE PANEL IS REBUILT, AND THE ART FAULTS GO WITH IT ----
// QC's plan says faults 1-4 *"cannot be fixed in code — they are pixels in `frame66.png`"* and offers
// two routes: a Figma re-export, or an element rebuild with the baked one skipped. ⭐ The second is
// available and cheap HERE, for a reason [[S154a]] established and [[S134a]] could not use: this panel is
// a **solid opaque box on a flat ground**, so it can simply be painted over. Measured, not assumed —
// a fill scan of `frame66.png` puts it at design **x 1008-2420, y 1157-1815**, uniformly
// `DragonPalette.Background`, with the word LIGHTING sitting ABOVE it on the illustration and therefore
// untouched. (`S134a`'s tab strip was the opposite case: embedded in artwork, unpatchable.)
//
// ⚠ QC's "must not break: the cabin illustration behind it stays" holds — the patch is exactly the
// panel's own box and the illustration was never visible through it.
//
// ---- ⚠ AND IT IS A READOUT, NOT A CONTROL ----
// The honest content is what `SettingsPage` already draws on the legacy CABIN tab: the master light's
// state, and the count of light modules actually found on the vessel. ⛔ It is drawn with NO button
// affordance and no instruction to tap, because this page has no hit test for it — a bordered plate
// here would re-commit the same defect in a tidier font. If the toggle is ever wanted live it needs a
// hit test and a painter branch, which is a separate line, not a silent addition.
// ============================================================================================
namespace DragonScreen
{
    public static class CabinLightingPanel
    {
        const float RefW = 3427f, RefH = 2112f;

        /// <summary>The baked panel's box, measured off `frame66.png` by a fill scan. ⭐ The word
        /// LIGHTING is ABOVE this and is deliberately outside it — the heading is correct and is kept.</summary>
        public const float X0 = 1008f, Y0 = 1157f, X1 = 2420f, Y1 = 1815f;

        public const int Commands = 6;

        /// <summary>
        /// Paint out the baked panel and draw what the vehicle actually has.
        ///
        /// ⚠ `sc` is the frame's own letterbox scale — this page is placed by `FigmaFramePage`, not
        /// stretched, so everything here goes through the same `ox + x * sc` the raster does. Getting
        /// that wrong is [[S134a]]'s defect one panel over.
        /// </summary>
        public static void Draw(DisplayList dl, int w, int h, PageState s)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float sc = h / RefH;
            if (sc <= 0f) return;
            float ox = (w - RefW * sc) * 0.5f;

            // the baked panel, painted out in its own ground
            dl.Rect(ox + X0 * sc, Y0 * sc, (X1 - X0) * sc, (Y1 - Y0) * sc, DragonPalette.Background);

            float live = Typography.MinDesignFor(w, sc);
            float label = Typography.DenseDesignFor(w, sc);
            float cx = (X0 + X1) * 0.5f;
            // ⚠ CENTRED IN THE BOX, not hung from its top. The rebuilt panel says four short lines
            // where the baked one had fifteen rows, so anchoring at the top left two thirds of the box
            // empty below the text and read as truncated - which is the impression the baked panel's
            // own mid-clause caption already gives, and the last one this should repeat.
            // The block's height is the sum of the gaps below, so it stays centred if a line is added.
            float block = label * 1.5f + live * 1.8f + label * 2.2f + label;
            float y = Y0 + ((Y1 - Y0) - block) * 0.5f;

            // ---- the master light, as a READOUT ----
            dl.Text("CABIN LIGHTS", ox + cx * sc, y * sc, label * sc, TextAlign.Centre,
                    DragonPalette.Text5);
            y += label * 1.5f;
            dl.Text(StateText(s), ox + cx * sc, y * sc, live * sc, TextAlign.Centre, StateInk(s));

            // ---- what is actually bindable, by count ----
            y += live * 1.8f;
            dl.Text(GroupsText(s), ox + cx * sc, y * sc, label * sc, TextAlign.Centre,
                    DragonPalette.Text6);

            // ⛔ AND WHY THERE ARE NO PER-ZONE ROWS. Said on the glass, because the baked panel
            // promised fifteen of them and a crew who saw that version will look for them.
            y += label * 2.2f;
            dl.Text("NO PER-ZONE LIGHTING ON THIS VEHICLE", ox + cx * sc, y * sc, label * sc,
                    TextAlign.Centre, DragonPalette.Text7);
        }

        /// <summary>ON / OFF, or a dash with no feed. ⚠ Never "OFF" for "cannot read" — that is a claim
        /// about the vehicle, and rule E4 is exactly about not making it.</summary>
        public static string StateText(PageState s)
        {
            if (!s.Valid) return Dashes.None;
            return s.LightsOn ? "ON" : "OFF";
        }

        public static Rgba StateInk(PageState s)
        {
            if (!s.Valid) return DragonPalette.Text6;
            return s.LightsOn ? DragonPalette.Accent : DragonPalette.Text4;
        }

        /// <summary>
        /// How many light modules the vessel actually carries — `SettingsPage`'s own wording, so the two
        /// surfaces that answer this question cannot answer it differently (rule C7.1).
        /// </summary>
        public static string GroupsText(PageState s)
        {
            if (!s.Valid) return Dashes.None;
            return s.LightCount > 1 ? (s.LightCount + " LIGHT GROUPS") : "SINGLE CABIN LIGHT GROUP";
        }
    }
}
