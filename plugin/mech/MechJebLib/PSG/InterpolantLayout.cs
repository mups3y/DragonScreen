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

using System.Collections.Generic;
using MechJebLib.Primitives;

namespace MechJebLib.PSG
{
    public struct InterpolantLayout
    {
        public const int INTERPOLANT_LAYOUT_LEN = 10;

        public V3 R;
        public V3 V;
        public double M;
        public V3 U;

        public void CopyTo(IList<double> other)
        {
            R.CopyTo(other);
            V.CopyTo(other, 3);
            other[6] = M;
            U.CopyTo(other, 7);
        }

        public void CopyFrom(IList<double> other)
        {
            R.CopyFrom(other);
            V.CopyFrom(other, 3);
            M = other[6];
            U.CopyFrom(other, 7);
        }

        public static InterpolantLayout CreateFrom(IList<double> other)
        {
            var a = new InterpolantLayout();

            a.CopyFrom(other);

            return a;
        }
    }
}

}
