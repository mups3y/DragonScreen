/*
 * DragonScreen - DragonPalette
 *
 * PURE. No Unity Color, no KSP - plain floats, so the tests can build headless. The glue layer
 * converts these to UnityEngine.Color at the boundary.
 *
 * EVERY VALUE HERE WAS MEASURED, NOT PICKED. Extracted 2026-08-04 by counting fill/stroke attributes
 * across the nine exported Figma SVGs, then cross-checked against a completely independent source -
 * the hex literals in the Apache-2.0 Vue recreation. The two agree exactly on every structural
 * colour. Two sources arriving at #020738 and #20FBFD independently is why these can be trusted;
 * one source could have been a designer's approximation.
 *
 * See docs/PALETTE.md for the counts and the full text ladder.
 */
namespace DragonScreen
{
    /// <summary>An RGBA colour as four 0..1 floats. Deliberately not UnityEngine.Color.</summary>
    public struct Rgba
    {
        public readonly float R, G, B, A;

        public Rgba(float r, float g, float b, float a)
        {
            R = r; G = g; B = b; A = a;
        }

        /// <summary>Build from a 6-digit hex string, with or without the leading '#'.</summary>
        public static Rgba Hex(string hex, float alpha)
        {
            if (string.IsNullOrEmpty(hex)) return new Rgba(0f, 0f, 0f, alpha);
            int i = (hex[0] == '#') ? 1 : 0;
            int v = System.Convert.ToInt32(hex.Substring(i, 6), 16);
            return new Rgba(((v >> 16) & 0xFF) / 255f,
                            ((v >> 8) & 0xFF) / 255f,
                            (v & 0xFF) / 255f,
                            alpha);
        }

        public static Rgba Hex(string hex) { return Hex(hex, 1f); }
    }

    /// <summary>
    /// The Dragon screen palette.
    ///
    /// THE LOOK IS NOT "CYAN ON NAVY". It is a tightly graded set of blue-greys on near-black navy,
    /// with cyan used sparingly for the one value that matters right now. Overusing Accent is the
    /// fastest way to make this look wrong - in the source art, #20FBFD appears 172 times against
    /// #111B52's 412, and most of those are thin accents rather than fills.
    /// </summary>
    public static class DragonPalette
    {
        // ---- structure. Measured counts from the SVG set in brackets. ----
        /// <summary>Screen background, the darkest surface. [74 uses]</summary>
        public static readonly Rgba Background = Rgba.Hex("020738");
        /// <summary>Panel and card fill - the most common colour in the design. [412]</summary>
        public static readonly Rgba Panel = Rgba.Hex("111B52");
        /// <summary>Hairline dividers and borders. [3]</summary>
        public static readonly Rgba Hairline = Rgba.Hex("313D7B");

        // Insets and wells: slightly darker and less blue than Background. These are what give the
        // panels depth without any bevel or drop shadow, which the design uses none of.
        public static readonly Rgba Inset1 = Rgba.Hex("10102C");
        public static readonly Rgba Inset2 = Rgba.Hex("0E0D2C");
        public static readonly Rgba Inset3 = Rgba.Hex("0D0D29");

        // ---- state ----
        /// <summary>Primary accent. Use sparingly. [172]</summary>
        public static readonly Rgba Accent = Rgba.Hex("20FBFD");
        /// <summary>Secondary cyan, for links and non-critical highlights.</summary>
        public static readonly Rgba AccentDim = Rgba.Hex("24D2FD");
        /// <summary>Nominal / GO. [7]</summary>
        public static readonly Rgba Go = Rgba.Hex("1FE327");
        /// <summary>Caution. [3]</summary>
        public static readonly Rgba Caution = Rgba.Hex("FFB74B");
        /// <summary>Alarm / NO-GO.</summary>
        public static readonly Rgba Alarm = Rgba.Hex("D12C30");

        /// <summary>
        /// Untinted. Passed to DisplayList.Image when the artwork should be drawn as authored -
        /// the tint MULTIPLIES, so white is the identity and there is no "no tint" option.
        /// </summary>
        public static readonly Rgba White = new Rgba(1f, 1f, 1f, 1f);

        // ---- GAUGE IDENTITY COLOURS ----
        // MEASURED, not chosen: these are the literal `style="stroke: #xxxxxx"` on each dial's fill
        // circle in Overview.vue (lines 59, 109, 159, 209, 260, 310, 360, 410), and they match both
        // the Figma mock and the user's photograph of the real screen.
        //
        // A DIAL'S COLOUR IS ITS NAME, NOT ITS CONDITION. Each metric keeps its colour whatever it
        // reads; alarm is routed through the chrome bar instead - see Alarms. Ours was
        // threshold-coloured until 2026-08-06, and that was a real inaccuracy, not a style choice.
        //
        // Note CabinTemp is the same hex as Alarm (#d12c30) IN THE SOURCE. That is not a mistake to
        // correct: the real screen really does draw the cabin temperature dial red at all times.
        public static readonly Rgba GaugePpo2 = Rgba.Hex("D7B733");
        public static readonly Rgba GaugeCabinTemp = Rgba.Hex("D12C30");
        public static readonly Rgba GaugePress = Rgba.Hex("FCD533");
        public static readonly Rgba GaugeCo2 = Rgba.Hex("2983ED");
        public static readonly Rgba GaugeLoop = Rgba.Hex("2886F6");
        public static readonly Rgba GaugePower = Rgba.Hex("2886F6");

        // The three gauges on FLIGHT have no counterpart in the reference, so they are assigned from
        // the same families rather than invented: a consumable takes the consumable mustard, anything
        // electrical the power blue, and structural stress the thermal red.
        public static readonly Rgba GaugePropellant = Rgba.Hex("D7B733");
        public static readonly Rgba GaugeGForce = Rgba.Hex("D7B733");
        /// <summary>
        /// ACCELERATION on the docking page. MEASURED: `style="stroke: #d7b733"` in Second.vue, the
        /// same mustard as PPO2 - so g-force is a consumable-family colour in the reference, not the
        /// thermal red I had assigned it by analogy. The source wins over the family argument.
        /// </summary>
        public static readonly Rgba GaugeAcceleration = Rgba.Hex("D7B733");

        /// <summary>Dial track. #777777 in the source, where it is a DOTTED ring (dasharray 0 4).</summary>
        public static readonly Rgba GaugeTrack = Rgba.Hex("777777", 0.35f);

        // Horizontal bar gauges, from #horizontal-bar / #progress-horizontal-bar in Overview.vue:
        // a white 25% track with a #35a9eb fill. Ours drew a flat rule and no fill at all.
        public static readonly Rgba BarTrack = new Rgba(1f, 1f, 1f, 0.25f);
        public static readonly Rgba BarFill = Rgba.Hex("35A9EB");

        // ---- text, brightest to dimmest ----
        // This ladder is the thing to copy most carefully; it carries the hierarchy that would
        // otherwise need weight or size changes.
        public static readonly Rgba Text0 = Rgba.Hex("F3F3F3");
        public static readonly Rgba Text1 = Rgba.Hex("E8EBFF");
        public static readonly Rgba Text2 = Rgba.Hex("DAE7FA");
        public static readonly Rgba Text3 = Rgba.Hex("C1C3DF");
        public static readonly Rgba Text4 = Rgba.Hex("B2B8DE");
        public static readonly Rgba Text5 = Rgba.Hex("A6ABC9");
        public static readonly Rgba Text6 = Rgba.Hex("8489A3");
        public static readonly Rgba Text7 = Rgba.Hex("585D7C");
        public static readonly Rgba Text8 = Rgba.Hex("515670");
    }
}
