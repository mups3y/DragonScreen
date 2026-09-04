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
using static MechJebLib.Utils.Statics;

namespace MuMech
{
    [UsedImplicitly]
    public class OperationPeriapsis : Operation
    {
        private static readonly string _name = Localizer.Format("#MechJeb_Pe_title");
        public override string GetName() => _name;

        [UsedImplicitly, Persistent(pass = (int)Pass.GLOBAL)]
        public readonly EditableDoubleMult NewPeA = new EditableDoubleMult(100000, 1000);

        private static readonly TimeReference[] _timeReferences = { TimeReference.APOAPSIS, TimeReference.PERIAPSIS, TimeReference.X_FROM_NOW, TimeReference.ALTITUDE };

        private static readonly TimeSelector _timeSelector = new TimeSelector(_timeReferences);

        public override void DoParametersGUI(Orbit o, double universalTime, MechJebModuleTargetController target)
        {
            GuiUtils.SimpleTextBox(Localizer.Format("#MechJeb_Pe_label"), NewPeA, "km"); //New periapsis:
            _timeSelector.DoChooseTimeGUI();
        }

        protected override List<ManeuverParameters> MakeNodesImpl(Orbit o, double universalTime, MechJebModuleTargetController target)
        {
            double ut = _timeSelector.ComputeManeuverTime(o, universalTime, target);

            if (NewPeA < -o.referenceBody.Radius)
            {
                throw new OperationException(Localizer.Format("#MechJeb_Pe_Exception2", o.referenceBody.displayName.LocalizeRemoveGender()) + "(-" +
                    o.referenceBody.Radius.ToSI(3) + "m)"); //new periapsis cannot be lower than minus the radius of <<1>>
            }

            return new List<ManeuverParameters> { new ManeuverParameters(OrbitalManeuverCalculator.DeltaVToChangePeriapsis(o, ut, NewPeA + o.referenceBody.Radius), ut) };
        }
    }
}

}
