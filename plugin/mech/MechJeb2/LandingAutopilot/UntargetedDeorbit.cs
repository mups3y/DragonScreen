// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
using KSP.Localization;

namespace MuMech
{
    namespace Landing
    {
        public class UntargetedDeorbit : AutopilotStep
        {
            public UntargetedDeorbit(MechJebCore core) : base(core)
            {
            }

            public override AutopilotStep Drive(FlightCtrlState s)
            {
                if (Orbit.PeA < -0.1 * MainBody.Radius)
                {
                    Core.Thrust.TargetThrottle = 0;
                    return new FinalDescent(Core);
                }

                Core.Attitude.attitudeTo(Vector3d.back, AttitudeReference.ORBIT_HORIZONTAL, Core.Landing);
                Core.Thrust.TargetThrottle = Core.Attitude.attitudeAngleFromTarget() < 5 ? 1 : 0;

                Status = Localizer.Format("#MechJeb_LandingGuidance_Status16"); //"Doing deorbit burn."

                return this;
            }
        }
    }
}

}
