// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
using System.Collections.Generic;
using JetBrains.Annotations;
using KSP.Localization;

namespace MuMech
{
    [UsedImplicitly]
    public class OperationKillRelVel : Operation
    {
        private static readonly string _name = Localizer.Format("#MechJeb_match_v_title");
        public override string GetName() => _name;

        private static readonly TimeReference[] _timeReferences = { TimeReference.CLOSEST_APPROACH, TimeReference.X_FROM_NOW };

        private static readonly TimeSelector _timeSelector = new TimeSelector(_timeReferences);

        public override void DoParametersGUI(Orbit o, double universalTime, MechJebModuleTargetController target) => _timeSelector.DoChooseTimeGUI();

        protected override List<ManeuverParameters> MakeNodesImpl(Orbit o, double universalTime, MechJebModuleTargetController target)
        {
            if (!target.NormalTargetExists)
                throw new OperationException(Localizer.Format("#MechJeb_match_v_Exception1")); //must select a target to match velocities with.
            if (o.referenceBody != target.TargetOrbit.referenceBody)
                throw new OperationException(Localizer.Format("#MechJeb_match_v_Exception2")); //target must be in the same sphere of influence.

            double ut = _timeSelector.ComputeManeuverTime(o, universalTime, target);

            Vector3d dV = OrbitalManeuverCalculator.DeltaVToMatchVelocities(o, ut, target.TargetOrbit);

            return new List<ManeuverParameters> { new ManeuverParameters(dV, ut) };
        }
    }
}

}
