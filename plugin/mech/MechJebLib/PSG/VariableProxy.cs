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
using MechJebLib.PSG.Terminal;

namespace MechJebLib.PSG
{
    public class VariableProxy
    {
        private readonly List<PhaseProxy> _proxies = new List<PhaseProxy>();

        public readonly int TotalVariables;
        public readonly int TotalConstraints;

        public VariableProxy(Problem problem, PhaseCollection phases, ITerminal terminal, int n)
        {
            int idx = 0;
            int cons = 0;

            for (int p = 0; p < phases.Count; p++)
            {
                var proxy = new PhaseProxy(problem, n, idx, p, phases[p]);
                _proxies.Add(proxy);
                idx += proxy.NumVars;
                cons += proxy.NumConstraints;
            }

            cons += terminal.NumConstraints; // terminal constraints

            cons += 0;

            TotalVariables = idx;
            TotalConstraints = cons;
        }

        public PhaseProxy this[int index]
        {
            get
            {
                if (index < 0)
                    return _proxies[_proxies.Count + index];
                return _proxies[index];
            }
        }

        public void WrapVars(double[] vars)
        {
            foreach (PhaseProxy proxy in _proxies)
                proxy.WrapVars(vars);
        }
    }
}

}
