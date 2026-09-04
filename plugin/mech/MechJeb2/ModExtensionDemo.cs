// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
using JetBrains.Annotations;

namespace MuMech
{
    // This file is to show how to use the callback in VesselState (and the other module who will have them soon)

    [UsedImplicitly]
    public class ModExtensionDemo : ComputerModule
    {
        public ModExtensionDemo(MechJebCore core) : base(core) { }

        private void partUpdate(Part p)
        {
            //vesselState.ctrlTorqueAvailable.Add(new Vector3d(1, 1, 0));
            //vesselState.ctrlTorqueAvailable.Add(new Vector3d(-1, -1, 0));
        }

        private void partModuleUpdate(PartModule pm)
        {
            //vesselState.mass += 2;
        }

        public override void OnStart(PartModule.StartState state)
        {
            //print("ModExtensionTest adding MJ2 callback");
            //vesselState.vesselStatePartExtensions.Add(partUpdate);
            //vesselState.vesselStatePartModuleExtensions.Add(partModuleUpdate);
        }
    }
}

}
