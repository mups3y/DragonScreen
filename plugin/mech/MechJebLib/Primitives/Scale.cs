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

using static System.Math;

namespace MechJebLib.Primitives
{
    public readonly struct Scale
    {
        public readonly double LengthScale;
        public readonly double MassScale;
        public readonly double VelocityScale;

        public double TimeScale     => LengthScale / VelocityScale;
        public double AccelScale    => VelocityScale / TimeScale;
        public double ForceScale    => MassScale * AccelScale;
        public double MdotScale     => MassScale / TimeScale;
        public double AreaScale     => LengthScale * LengthScale;
        public double VolumeScale   => AreaScale * LengthScale;
        public double DensityScale  => MassScale / VolumeScale;
        public double PressureScale => ForceScale / AreaScale;

        public Scale(double lengthScale, double velocityScale, double massScale)
        {
            LengthScale = lengthScale;
            MassScale = massScale;
            VelocityScale = velocityScale;
        }

        public static Scale Create(double mu, double r0, double m0 = 1.0)
        {
            double massScale = m0;
            double lengthScale = r0;
            double velocityScale = Sqrt(mu / lengthScale);
            return new Scale(lengthScale, velocityScale, massScale);
        }

        public Scale ConvertTo(Scale other) =>
            new Scale(
                other.LengthScale / LengthScale,
                other.VelocityScale / VelocityScale,
                other.MassScale / MassScale
            );
    }
}

}
