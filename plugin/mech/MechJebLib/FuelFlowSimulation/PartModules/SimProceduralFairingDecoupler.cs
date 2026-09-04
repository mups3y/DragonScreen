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
using MechJebLib.Utils;
using static System.FormattableString;

namespace MechJebLib.FuelFlowSimulation.PartModules
{
    public class SimProceduralFairingDecoupler : SimPartModule
    {
        private static readonly ObjectPool<SimProceduralFairingDecoupler> _pool = new ObjectPool<SimProceduralFairingDecoupler>(New, Clear);

        public bool IsDecoupled = false;

        public override void Dispose() => _pool.Release(this);

        public static SimProceduralFairingDecoupler Borrow(SimPart part)
        {
            SimProceduralFairingDecoupler decoupler = _pool.Borrow();
            decoupler.Part = part;
            return decoupler;
        }

        private static SimProceduralFairingDecoupler New() => new SimProceduralFairingDecoupler();

        private static void Clear(SimProceduralFairingDecoupler m)
        {
        }

        public override string ToString()
        {
            List<string> fields = CommonFieldList();
            AddField(fields, "IsDecoupled", IsDecoupled, false);
            return ModuleLine("SimProceduralFairingDecoupler", fields);
        }
    }
}

}
