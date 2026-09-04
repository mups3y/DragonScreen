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
using MechJebLib.Primitives;

namespace MechJebLib.ODE
{
    using ConditionFunc = Func<Vec, double, AbstractIVP, double>;
    using AssertFunc = Action<AbstractIVP>;

    public class Event : IComparable<Event>
    {
        public readonly ConditionFunc F;
        public readonly AssertFunc? Assert;
        public bool SaveBefore = true;
        public bool SaveAfter = true;
        public readonly bool Terminal = true;
        public readonly int Direction = 0;
        public double LastValue;
        public double NewValue;
        public double Time;

        public Event(ConditionFunc f, AssertFunc? assert = null)
        {
            F = f;
            Assert = assert;
        }

        public int CompareTo(Event other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return Time.CompareTo(other.Time);
        }
    }
}

}
