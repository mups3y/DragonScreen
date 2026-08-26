/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */

/*
 * ---- SUBSET of MechJebLib/Utils/Statics.cs, ported into DragonScreen ----
 * Per docs/MECHJEBLIB_PORT.md: the FuelFlowSimulation uses only `Statics.Clamp` (x27) and `EPS` (x5),
 * so only those members are taken - the full Statics.cs is ~700 lines of orbital/vector helpers the
 * sim never touches, most of which reference V3/M3/Q3 (deliberately NOT ported; "pure has no vector
 * type"). Every member below is COPIED verbatim from the source, not re-derived. When the PSG port
 * lands it will need more of Statics; add them here from the source then, one at a time.
 */
namespace MechJebLib.Utils
{
    public static class Statics
    {
        /// <summary>
        ///     Machine epsilon (the difference between 1.0 and the next representable double).
        /// </summary>
        public const double EPS = 2.2204460492503131e-16;

        /// <summary>
        ///     Twice machine epsilon.
        /// </summary>
        public const double EPS2 = EPS * 2;

        /// <summary>
        ///     Value of the standard gravity constant in m/s.
        /// </summary>
        public const float G0 = 9.80665f;

        /// <summary>
        ///     Clamp first value between min and max.
        /// </summary>
        public static double Clamp(double x, double min, double max) => x < min ? min : x > max ? max : x;

        /// <summary>
        ///     Clamp first value between min and max by truncating.
        /// </summary>
        public static int Clamp(int x, int min, int max) => x < min ? min : x > max ? max : x;

        /// <summary>
        ///     Clamps the value between 0 and 1.
        /// </summary>
        public static double Clamp01(double x) => Clamp(x, 0, 1);

        /// <summary>
        ///     Linear interpolation, clamped to [a, b]. Used by SimModuleEngines' throttle blending.
        /// </summary>
        public static double Lerp(double a, double b, double t) => a + (b - a) * Clamp01(t);

        /// <summary>
        ///     Indent every line of a string by n spaces. Used only by the sim's ToString() debug dumps.
        /// </summary>
        public static string Indent(this string s, int n)
        {
            string pad = new string(' ', n);
            return pad + s.Replace("\n", "\n" + pad);
        }
    }
}
