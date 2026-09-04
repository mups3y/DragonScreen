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

namespace MechJebLib.FuelFlowSimulation
{
    public struct SimResource
    {
        public bool Free;
        public double MaxAmount;
        private double _amount;

        public double Amount
        {
            get => _amount + _rcsAmount;
            set => _amount = value;
        }

        private double _rcsAmount;
        public int Id;
        public double Density;
        public double Residual;

        public double ResidualThreshold => Residual * MaxAmount;

        public SimResource Drain(double resourceDrain)
        {
            _amount -= resourceDrain;
            if (_amount < 0)
                _amount = 0;

            return this;
        }

        public SimResource RCSDrain(double rcsDrain)
        {
            _rcsAmount -= rcsDrain;
            if (Amount < 0)
                _rcsAmount = -_amount;

            return this;
        }

        public SimResource ResetRCS()
        {
            _rcsAmount = 0;
            return this;
        }
    }
}

}
