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

using System;

namespace MechJebLib.Control
{
    public class MultiVariableGrid
    {
        public double[] GrrValues { get; }
        public double[] TsValues  { get; }
        public double[] MValues   { get; }

        public MultiVariableGrid(double[] grrValues, double[] tsValues, double[] mValues)
        {
            GrrValues = new double[grrValues.Length];
            TsValues = tsValues;
            MValues = new double[mValues.Length];

            for (int i = 0; i < grrValues.Length; i++)
                GrrValues[i] = Math.Log(grrValues[i]);
            for (int i = 0; i < mValues.Length; i++)
                MValues[i] = Math.Log(mValues[i]);
        }

        public (int Grr, int Ts, int M) GetDimensions() => (GrrValues.Length, TsValues.Length, MValues.Length);
    }
}

}
