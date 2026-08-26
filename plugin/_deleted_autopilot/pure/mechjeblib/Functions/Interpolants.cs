/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */

/*
 * ---- PORTED from MechJebLib/Functions/Interpolants.cs, double overload ONLY ----
 * Per docs/MECHJEBLIB_PORT.md the V3 vector type is NOT ported ("pure has no vector type"; the sim
 * uses only V3.zero x6). The source also carries a `V3 CubicHermiteInterpolant(...)` overload; it is
 * dropped here for the same reason. The `double` overload below - the one H1 evaluates the engine
 * curves through - is copied verbatim.
 */
namespace MechJebLib.Functions
{
    public class Interpolants
    {
        public static double CubicHermiteInterpolant(double x1, double y1, double yp1, double x2, double y2,
            double yp2, double x)
        {
            double t = (x - x1) / (x2 - x1);
            double t2 = t * t;
            double t3 = t2 * t;
            double h00 = 2 * t3 - 3 * t2 + 1;
            double h10 = t3 - 2 * t2 + t;
            double h01 = -2 * t3 + 3 * t2;
            double h11 = t3 - t2;

            return h00 * y1 + h10 * (x2 - x1) * yp1 + h01 * y2 + h11 * (x2 - x1) * yp2;
        }
    }
}
