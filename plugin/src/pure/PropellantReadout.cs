// DragonScreen - PropellantReadout
// ---- THE GAUGE READ 100% ALL THE WAY TO ORBIT ----
// ---- WHAT IT SHOWS NOW: WHAT THE LIT ENGINES ARE ACTUALLY DRINKING ----
// ---- AND THE CAPTION HAS TO NAME IT ----
namespace DragonScreen
{
    public static class PropellantReadout
    {
        public const int MaxSources = 6;

        public static double Fraction(double[] fractions, int count)
        {
            if (fractions == null || count <= 0) return -1.0;
            if (count > fractions.Length) count = fractions.Length;

            double lowest = double.MaxValue;
            for (int i = 0; i < count; i++)
            {
                double f = fractions[i];
                if (double.IsNaN(f)) continue;
                if (f < 0.0) f = 0.0;
                if (f > 1.0) f = 1.0;
                if (f < lowest) lowest = f;
            }
            return (lowest == double.MaxValue) ? -1.0 : lowest;
        }

        public static string Caption(string[] names, int count)
        {
            string list = Join(names, count);
            return string.IsNullOrEmpty(list) ? "PROPELLANT" : "PROPELLANT " + list;
        }

        public static string Join(string[] names, int count)
        {
            if (names == null || count <= 0) return "";
            if (count > names.Length) count = names.Length;

            string s = "";
            int used = 0;
            for (int i = 0; i < count; i++)
            {
                string n = Short(names[i]);
                if (string.IsNullOrEmpty(n)) continue;
                if (used == 3) return s + "/...";
                s = (used == 0) ? n : s + "/" + n;
                used++;
            }
            return s;
        }

        public static string Short(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName)) return "";
            switch (resourceName)
            {
                case "LiquidFuel": return "LF";
                case "Oxidizer": return "OX";
                case "MonoPropellant": return "MONOPROP";
                case "SolidFuel": return "SOLID";
                case "XenonGas": return "XENON";
                case "Ore": return "ORE";
            }
            string up = resourceName.ToUpperInvariant();
            return (up.Length > 8) ? up.Substring(0, 8) : up;
        }
    }
}
