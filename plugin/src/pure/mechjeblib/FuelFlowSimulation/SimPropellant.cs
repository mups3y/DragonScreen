/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */

/*
 * ---- PORTED VERBATIM into DragonScreen from MechJebLib/FuelFlowSimulation/SimPropellant.cs ----
 * Per docs/MECHJEBLIB_PORT.md. One propellant entry on an engine/RCS (id, ratio, flow mode, density).
 */
using MechJebLib.FuelFlowSimulation.PartModules;

namespace MechJebLib.FuelFlowSimulation
{
    public readonly struct SimPropellant
    {
        public readonly int id;
        public readonly bool ignoreForIsp;
        public readonly double ratio;
        public readonly SimFlowMode FlowMode;
        public readonly double density;

        public SimPropellant(int id, bool ignoreForIsp, double ratio, SimFlowMode flowMode, double density)
        {
            this.id = id;
            this.ignoreForIsp = ignoreForIsp;
            this.ratio = ratio;
            FlowMode = flowMode;
            this.density = density;
        }
    }
}
