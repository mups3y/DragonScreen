// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace MuMech
{
    public abstract class MechJebModuleDeployableController : ComputerModule
    {
        protected MechJebModuleDeployableController(MechJebCore core) : base(core)
        {
            Priority = 200;
            Enabled = true;
        }

        protected string ButtonText;
        protected bool Extended;

        [UsedImplicitly, Persistent(pass = (int)Pass.GLOBAL)]
        public bool AutoDeploy;

        [UsedImplicitly, Persistent(pass = (int)Pass.LOCAL)]
        public bool PrevShouldDeploy;

        public bool PrevAutoDeploy = true;

        [UsedImplicitly]
        protected readonly List<ModuleDeployablePart> CachedPartModules = new List<ModuleDeployablePart>(16);

        [UsedImplicitly]
        protected void DiscoverDeployablePartModules()
        {
            CachedPartModules.Clear();
            foreach (Part p in Vessel.Parts)
            foreach (PartModule pm in p.Modules)
                if (pm != null && pm is ModuleDeployablePart mdp && IsModules(mdp))
                    CachedPartModules.Add(mdp);
        }

        [UsedImplicitly]
        protected bool IsDeployable(ModuleDeployablePart sa) => sa.Events["Extend"].active || sa.Events["Retract"].active;

        public void ExtendAll()
        {
            foreach (ModuleDeployablePart mdp in CachedPartModules)
                if (!(mdp is null) && IsDeployable(mdp) && !mdp.part.ShieldedFromAirstream)
                    mdp.Extend();
        }

        public void RetractAll()
        {
            foreach (ModuleDeployablePart mdp in CachedPartModules)
                if (!(mdp is null) && IsDeployable(mdp) && !mdp.part.ShieldedFromAirstream)
                    mdp.Retract();
        }

        public bool AllRetracted()
        {
            foreach (ModuleDeployablePart mdp in CachedPartModules)
                if (!(mdp is null) && IsDeployable(mdp) && mdp.deployState != ModuleDeployablePart.DeployState.RETRACTED)
                    return false;
            return true;
        }

        private bool ShouldDeploy()
        {
            if (!MainBody.atmosphere)
                return true;

            if (!Vessel.LiftedOff())
                return false;

            if (Vessel.LandedOrSplashed)
                return false; // True adds too many complex case

            const double DT = 10;
            double minAlt; // minimum altitude between now and now+dt seconds
            double t = Planetarium.GetUniversalTime();

            double peT = Orbit.NextPeriapsisTime(t) - t;
            if (peT > 0 && peT < DT)
                minAlt = Orbit.PeA;
            else
                minAlt = Math.Sqrt(Math.Min(Orbit.getRelativePositionAtUT(t).sqrMagnitude, Orbit.getRelativePositionAtUT(t + DT).sqrMagnitude)) -
                    MainBody.Radius;

            return minAlt > MainBody.RealMaxAtmosphereAltitude();
        }

        public override void OnFixedUpdate()
        {
            // Let the ascent guidance handle the solar panels to retract them before launch
            if (AutoDeploy && !Core.Ascent.Enabled)
            {
                bool tmp = ShouldDeploy();

                switch (tmp)
                {
                    case true when !PrevShouldDeploy || AutoDeploy != PrevAutoDeploy:
                        ExtendAll();
                        break;
                    case false when PrevShouldDeploy || AutoDeploy != PrevAutoDeploy:
                        RetractAll();
                        break;
                }

                PrevShouldDeploy = tmp;
                PrevAutoDeploy = true;
            }
            else
            {
                PrevAutoDeploy = false;
            }

            bool extendedThisPass = !AllRetracted();
            if (Extended != extendedThisPass)
                ButtonText = GetButtonText(extendedThisPass ? DeployablePartState.EXTENDED : DeployablePartState.RETRACTED);

            Extended = extendedThisPass;
        }

        protected bool ExtendingOrRetracting()
        {
            foreach (ModuleDeployablePart mdp in CachedPartModules)
                if (mdp != null && IsDeployable(mdp)
                    && (mdp.deployState == ModuleDeployablePart.DeployState.EXTENDING ||
                        mdp.deployState == ModuleDeployablePart.DeployState.RETRACTING))
                    return true;
            return false;
        }

        protected abstract bool IsModules(ModuleDeployablePart p);

        protected enum DeployablePartState
        {
            RETRACTED,
            EXTENDED
        }

        protected abstract string GetButtonText(DeployablePartState deployablePartState);

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsFlight)
                DiscoverDeployablePartModules();
        }

        public override void OnVesselWasModified(Vessel v) => DiscoverDeployablePartModules();
    }
}

}
