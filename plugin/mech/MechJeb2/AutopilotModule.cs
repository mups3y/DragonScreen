// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
using System;
using UnityEngine;

namespace MuMech
{
    public class AutopilotModule : ComputerModule
    {
        protected AutopilotModule(MechJebCore core) : base(core)
        {
        }

        public override void Drive(FlightCtrlState s)
        {
            if (CurrentStep == null) return;

            try
            {
                CurrentStep = CurrentStep.Drive(s);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public override void OnFixedUpdate()
        {
            if (CurrentStep == null) return;

            try
            {
                CurrentStep = CurrentStep.OnFixedUpdate();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        protected void SetStep(AutopilotStep step) => CurrentStep = step;

        public string Status => CurrentStep == null ? "Off" : CurrentStep.Status;

        protected bool Active => CurrentStep != null;

        public AutopilotStep CurrentStep { get; private set; }
    }

    public class AutopilotStep
    {
        protected readonly MechJebCore Core;

        //conveniences:
        protected VesselState   VesselState => Core.VesselState;
        protected Vessel        Vessel      => Core.part.vessel;
        protected CelestialBody MainBody    => Core.part.vessel.mainBody;
        protected Orbit         Orbit       => Core.part.vessel.orbit;

        protected AutopilotStep(MechJebCore core)
        {
            Core = core;
        }

        public virtual AutopilotStep Drive(FlightCtrlState s) => this;
        public virtual AutopilotStep OnFixedUpdate()          => this;
        public         string        Status                   { get; protected set; }
    }
}

}
