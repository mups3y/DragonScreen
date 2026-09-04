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
using UnityEngine;

namespace MuMech
{
    [UsedImplicitly]
    public class OperationLan : Operation
    {
        private static readonly string _name = Localizer.Format("#MechJeb_AN_title");
        public override string GetName() => _name;

        private static readonly TimeReference[] _timeReferences = { TimeReference.APOAPSIS, TimeReference.PERIAPSIS, TimeReference.X_FROM_NOW };

        private static readonly TimeSelector _timeSelector = new TimeSelector(_timeReferences);

        public override void DoParametersGUI(Orbit o, double universalTime, MechJebModuleTargetController target)
        {
            _timeSelector.DoChooseTimeGUI();
            GUILayout.Label(Localizer.Format("#MechJeb_AN_label")); //New Longitude of Ascending Node:
            target.targetLongitude.DrawEditGUI(EditableAngle.Direction.EW);
        }

        protected override List<ManeuverParameters> MakeNodesImpl(Orbit o, double universalTime, MechJebModuleTargetController target)
        {
            if (o.inclination < 10)
                ErrorMessage = Localizer.Format("#MechJeb_AN_error",
                    o.inclination); //"Warning: orbital plane has a low inclination of <<1>>º (recommend > 10º) and so maneuver may not be accurate"

            double ut = _timeSelector.ComputeManeuverTime(o, universalTime, target);

            Vector3d dV = OrbitalManeuverCalculator.DeltaVToShiftLAN(o, ut, target.targetLongitude);

            return new List<ManeuverParameters> { new ManeuverParameters(dV, ut) };
        }
    }
}

}
