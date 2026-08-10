/*
 * DragonScreen - PropellantReadout
 *
 * PURE. Which propellant the PROPELLANT gauge is showing, and what to call it.
 *
 * ---- THE GAUGE READ 100% ALL THE WAY TO ORBIT ----
 * Reported in flight, 2026-08-06. It was reading MonoPropellant - the Draco tanks - which is exactly
 * right for a separated capsule and completely useless during ascent, when the thing being burned is
 * the booster's LF/OX and the Dracos have not been touched. Nothing was broken; the gauge was
 * answering a question nobody was asking. Same family as orbital speed reading 175 m/s on the pad.
 *
 * ---- WHAT IT SHOWS NOW: WHAT THE LIT ENGINES ARE ACTUALLY DRINKING ----
 * The glue collects the resources consumed by engines that are burning right now (and, when nothing
 * is lit, by every engine still attached, so the pad and a coast show the stage you are about to
 * burn). This file decides what to do with that set:
 *
 *   FRACTION = the MINIMUM across the sources, because a bipropellant stage is empty when the FIRST
 *   of its propellants runs out. Averaging LF and OX would read 50% at the moment the oxidiser hit
 *   zero and the engines died, which is the worst possible time to be reassuring.
 *
 * ---- AND THE CAPTION HAS TO NAME IT ----
 * This gauge now means different things at different times: booster LF/OX, then the second stage,
 * then Draco monopropellant. The velocity readout already taught this lesson - a number that
 * silently changes meaning is worse than one that is consistently wrong, because nothing on the
 * screen says it changed. So the caption carries the source, always.
 */
namespace DragonScreen
{
    public static class PropellantReadout
    {
        /// <summary>Most distinct propellants one gauge will summarise. Bipropellant is two.</summary>
        public const int MaxSources = 6;

        /// <summary>
        /// The fraction to draw: the LOWEST of the sources, or -1 when there is nothing to show.
        ///
        /// -1 rather than 0 because "this vehicle has no propellant tank" and "the tank is empty"
        /// are different facts, and the second one should be alarming while the first should not.
        /// </summary>
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

        /// <summary>
        /// Caption for the gauge, naming what it is measuring.
        ///
        /// Plain ASCII and a space separator on purpose: the font is whatever Windows resolved for
        /// D-DIN, and a middot or an en-dash is one more glyph that can come back as a blank box on
        /// somebody else's install. The screens have to read at a glance, not look typeset.
        /// </summary>
        public static string Caption(string[] names, int count)
        {
            string list = Join(names, count);
            return string.IsNullOrEmpty(list) ? "PROPELLANT" : "PROPELLANT " + list;
        }

        /// <summary>The sources as one short string, e.g. "LF/OX". Empty when there are none.</summary>
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
                // Three is where the caption stops fitting under a gauge; beyond that say so rather
                // than run into the neighbouring dial.
                if (used == 3) return s + "/...";
                s = (used == 0) ? n : s + "/" + n;
                used++;
            }
            return s;
        }

        /// <summary>
        /// A resource name shortened to something that fits under a dial.
        ///
        /// The mapping covers what a Falcon 9 and a Dragon actually burn. Anything else falls back
        /// to the name in capitals, truncated - an unfamiliar propellant should read as itself
        /// rather than vanish, because the whole point of the caption is to say what is being shown.
        /// </summary>
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
