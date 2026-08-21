/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */

/*
 * ---- PORTED VERBATIM into DragonScreen from MechJebLib/FuelFlowSimulation/SimPartModule.cs ----
 * Per docs/MECHJEBLIB_PORT.md. Base of every sim part-module. `#nullable enable annotations`
 * reproduces MechJebLib's nullable build context so `Part = null!` is legal without hand-editing.
 */
#nullable enable annotations
using System;
using static System.FormattableString;

namespace MechJebLib.FuelFlowSimulation
{
    public abstract class SimPartModule : IDisposable
    {
        public bool IsEnabled;
        public SimPart Part = null!;
        public bool ModuleIsEnabled;
        public bool StagingEnabled;

        public abstract void Dispose();

        // The fields common to every SimPartModule, for the concrete ToString() debug dumps to include.
        protected string CommonFields() =>
            Invariant($"IsEnabled={IsEnabled} ModuleIsEnabled={ModuleIsEnabled} StagingEnabled={StagingEnabled}");
    }
}
