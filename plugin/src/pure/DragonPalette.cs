// DragonScreen - DragonPalette
namespace DragonScreen
{
    public struct Rgba
    {
        public readonly float R, G, B, A;

        public Rgba(float r, float g, float b, float a)
        {
            R = r; G = g; B = b; A = a;
        }

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

    public static class DragonPalette
    {
        // ---- structure. Measured counts from the SVG set in brackets. ----
        public static readonly Rgba Background = Rgba.Hex("020738");
        public static readonly Rgba Panel = Rgba.Hex("111B52");
        public static readonly Rgba Hairline = Rgba.Hex("313D7B");

        public static readonly Rgba Inset1 = Rgba.Hex("10102C");
        public static readonly Rgba Inset2 = Rgba.Hex("0E0D2C");
        public static readonly Rgba Inset3 = Rgba.Hex("0D0D29");

        // ---- state ----
        public static readonly Rgba Accent = Rgba.Hex("20FBFD");
        public static readonly Rgba AccentDim = Rgba.Hex("24D2FD");
        public static readonly Rgba Go = Rgba.Hex("1FE327");
        public static readonly Rgba Caution = Rgba.Hex("FFB74B");
        public static readonly Rgba Alarm = Rgba.Hex("D12C30");

        public static readonly Rgba White = new Rgba(1f, 1f, 1f, 1f);

        // ---- GAUGE IDENTITY COLOURS ----
        public static readonly Rgba GaugePpo2 = Rgba.Hex("D7B733");
        public static readonly Rgba GaugeCabinTemp = Rgba.Hex("D12C30");
        public static readonly Rgba GaugePress = Rgba.Hex("FCD533");
        public static readonly Rgba GaugeCo2 = Rgba.Hex("2983ED");
        public static readonly Rgba GaugeLoop = Rgba.Hex("2886F6");
        public static readonly Rgba GaugePower = Rgba.Hex("2886F6");

        public static readonly Rgba GaugePropellant = Rgba.Hex("D7B733");
        public static readonly Rgba GaugeGForce = Rgba.Hex("D7B733");
        public static readonly Rgba GaugeAcceleration = Rgba.Hex("D7B733");

        public static readonly Rgba GaugeTrack = Rgba.Hex("777777", 0.35f);

        public static readonly Rgba BarTrack = new Rgba(1f, 1f, 1f, 0.25f);
        public static readonly Rgba BarFill = Rgba.Hex("35A9EB");

        // ---- text, brightest to dimmest ----
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
