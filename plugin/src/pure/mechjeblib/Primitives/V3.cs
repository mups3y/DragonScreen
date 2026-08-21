/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */

/*
 * ---- MINIMAL SUBSET of MechJebLib/Primitives/V3.cs, ported into DragonScreen ----
 *
 * ⚠ DELIBERATE DEVIATION from docs/MECHJEBLIB_PORT.md, which said "V3 — SKIP ... the sim uses only
 * V3.zero x6". That was an under-count: SimModuleEngines carries `List<V3> ThrustDirectionVectors`
 * and `V3 ThrustCurrent/Max/Min`, and both it and SimVessel do real vector arithmetic
 * (V3 + V3, double * V3, .magnitude) to sum canted engine thrust before reducing to a scalar. So a
 * vector type is genuinely required. But the FULL V3.cs (481 lines) drags in M3 (V3.Outer) and Q3
 * (V3.Slerp) plus more of Statics - a cascade the fuel-flow sim never touches - so the doc's INTENT
 * (don't import the vector cascade for this port) still holds.
 *
 * The resolution: keep all 15 sim files VERBATIM and provide EXACTLY the V3 members they use. Every
 * member below is copied unchanged from the source V3.cs. This is a strict subset of that type, so
 * when the PSG port lands (which needs the full V3 + M3 + Q3) this file is REPLACED by the complete
 * V3.cs additively - nothing here has to be un-done. The approved plan (step 3, PSG) makes a real
 * vector type in src/pure correct; it supersedes the old "pure has no vector type" note.
 *
 * Members the fuel-flow sim actually uses: ctor(x,y,z), the x/y/z fields, `zero`, operator + (V3,V3),
 * operator * (double,V3) and (V3,double), `magnitude`, and ToString() for the debug dumps.
 */
using System.Globalization;
using System.Runtime.CompilerServices;
using static System.Math;

namespace MechJebLib.Primitives
{
    /// <summary>
    ///     Double Precision, Right-Handed 3-Vector class using Radians.
    ///     (Fuel-flow subset; see the file header. Expand to the full MechJebLib V3 for PSG.)
    /// </summary>
    public struct V3
    {
        public double x;
        public double y;
        public double z;

        public V3(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static V3 zero { get; } = new V3(0.0, 0.0, 0.0);

        public static V3 operator +(V3 a, V3 b) => new V3(a.x + b.x, a.y + b.y, a.z + b.z);

        public static V3 operator -(V3 a, V3 b) => new V3(a.x - b.x, a.y - b.y, a.z - b.z);

        public static V3 operator -(V3 a) => new V3(-a.x, -a.y, -a.z);

        public static V3 operator *(V3 a, double d) => new V3(a.x * d, a.y * d, a.z * d);

        public static V3 operator *(double d, V3 a) => new V3(a.x * d, a.y * d, a.z * d);

        public double sqrMagnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => x * x + y * y + z * z;
        }

        public double magnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Sqrt(x * x + y * y + z * z);
        }

        public override string ToString() =>
            $"[{x.ToString("G17", CultureInfo.InvariantCulture)}, {y.ToString("G17", CultureInfo.InvariantCulture)}, {z.ToString("G17", CultureInfo.InvariantCulture)}]";
    }
}
