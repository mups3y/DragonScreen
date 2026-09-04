// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */

using MechJebLib.Primitives;

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

        public static V3 CubicHermiteInterpolant(double x1, V3 y1, V3 yp1, double x2, V3 y2,
            V3 yp2, double x)
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

}
