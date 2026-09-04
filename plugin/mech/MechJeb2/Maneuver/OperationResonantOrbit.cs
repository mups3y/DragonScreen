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
    public class OperationResonantOrbit : Operation
    {
        private static readonly string _name = Localizer.Format("#MechJeb_resonant_title");
        public override string GetName() => _name;

        [UsedImplicitly, Persistent(pass = (int)Pass.GLOBAL)]
        public EditableInt ResonanceNumerator = 2;

        [UsedImplicitly, Persistent(pass = (int)Pass.GLOBAL)]
        public EditableInt ResonanceDenominator = 3;

        private readonly TimeSelector _timeSelector =
            new TimeSelector(new[] { TimeReference.APOAPSIS, TimeReference.PERIAPSIS, TimeReference.X_FROM_NOW, TimeReference.ALTITUDE });

        public override void DoParametersGUI(Orbit o, double universalTime, MechJebModuleTargetController target)
        {
            GUILayout.Label(Localizer.Format("#MechJeb_resonant_label1",
                ResonanceNumerator.Val + "/" + ResonanceDenominator.Val)); //"Change your orbital period to <<1>> of your current orbital period"
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("#MechJeb_resonant_label2"), GuiUtils.LayoutExpandWidth); //New orbital period ratio :
            ResonanceNumerator.Text = GUILayout.TextField(ResonanceNumerator.Text, GuiUtils.LayoutWidth(30));
            GUILayout.Label("/", GuiUtils.LayoutNoExpandWidth);
            ResonanceDenominator.Text = GUILayout.TextField(ResonanceDenominator.Text, GuiUtils.LayoutWidth(30));
            GUILayout.EndHorizontal();
            _timeSelector.DoChooseTimeGUI();
        }

        protected override List<ManeuverParameters> MakeNodesImpl(Orbit o, double universalTime, MechJebModuleTargetController target)
        {
            double ut = _timeSelector.ComputeManeuverTime(o, universalTime, target);
            Vector3d dV = OrbitalManeuverCalculator.DeltaVToResonantOrbit(o, ut, (double)ResonanceNumerator.Val / ResonanceDenominator.Val);

            return new List<ManeuverParameters> { new ManeuverParameters(dV, ut) };
        }
    }
}

}
