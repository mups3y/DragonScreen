// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
namespace MuMech.AttitudeControllers
{
    public abstract class BaseAttitudeController
    {
        protected readonly MechJebModuleAttitudeController Ac;

        protected BaseAttitudeController(MechJebModuleAttitudeController controller)
        {
            Ac = controller;
        }

        public virtual void OnModuleDisabled()
        {
        }

        public virtual void OnModuleEnabled()
        {
        }

        public virtual void OnStart()
        {
        }

        public virtual void OnLoad(ConfigNode local, ConfigNode type, ConfigNode global)
        {
            if (global != null && global.HasNode(GetType().Name))
                ConfigNode.LoadObjectFromConfig(this, global.GetNode(GetType().Name), (int)Pass.GLOBAL);
            if (type != null && type.HasNode(GetType().Name)) ConfigNode.LoadObjectFromConfig(this, type.GetNode(GetType().Name), (int)Pass.TYPE);
            if (local != null && local.HasNode(GetType().Name)) ConfigNode.LoadObjectFromConfig(this, local.GetNode(GetType().Name), (int)Pass.LOCAL);
        }

        public virtual void OnSave(ConfigNode local, ConfigNode type, ConfigNode global)
        {
            if (global != null) ConfigNode.CreateConfigFromObject(this, (int)Pass.GLOBAL, null).CopyTo(global.AddNode(GetType().Name));
            if (type != null) ConfigNode.CreateConfigFromObject(this, (int)Pass.TYPE, null).CopyTo(type.AddNode(GetType().Name));
            if (local != null) ConfigNode.CreateConfigFromObject(this, (int)Pass.LOCAL, null).CopyTo(local.AddNode(GetType().Name));
        }

        public virtual void ResetConfig()
        {
        }

        public virtual void OnFixedUpdate()
        {
        }

        public virtual void OnUpdate()
        {
        }

        public virtual void Reset()
        {
        }

        public abstract void DrivePre(FlightCtrlState s, out Vector3d act, out Vector3d deltaEuler);

        public virtual void GUI()
        {
        }

        public virtual void Reset(int i) => Reset();
    }
}

}
