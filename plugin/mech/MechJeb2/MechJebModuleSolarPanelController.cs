// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
using KSP.Localization;
using UnityEngine;

namespace MuMech
{
    //[UsedImplicitly]
    public class MechJebModuleSolarPanelController : MechJebModuleDeployableController
    {
        public MechJebModuleSolarPanelController(MechJebCore core)
            : base(core)
        {
        }

        [GeneralInfoItem("#MechJeb_ToggleSolarPanels", InfoItem.Category.Misc, showInEditor = false)] //Toggle solar panels
        public void SolarPanelDeployButton()
        {
            AutoDeploy = GUILayout.Toggle(AutoDeploy, Localizer.Format("#MechJeb_SolarPanelDeployButton")); //"Auto-deploy solar panels"

            if (!GUILayout.Button(ButtonText)) return;

            if (ExtendingOrRetracting())
                return;

            if (!Extended)
                ExtendAll();
            else
                RetractAll();
        }

        protected override bool IsModules(ModuleDeployablePart p) => p is ModuleDeployableSolarPanel;

        protected override string GetButtonText(DeployablePartState deployablePartState)
        {
            return deployablePartState switch
            {
                DeployablePartState.EXTENDED  => Localizer.Format("#MechJeb_SolarPanelDeploy"), //"Toggle solar panels (currently extended)"
                DeployablePartState.RETRACTED => Localizer.Format("#MechJeb_SolarPanelRetracted"), //"Toggle solar panels (currently retracted)"
                _                             => Localizer.Format("#MechJeb_SolarPanelToggle")
            };
        }
    }
}

}
