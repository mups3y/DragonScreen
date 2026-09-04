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
        public class CourseCorrection : AutopilotStep
        {
            private bool _courseCorrectionBurning;

            public CourseCorrection(MechJebCore core) : base(core)
            {
            }

            public override AutopilotStep Drive(FlightCtrlState s)
            {
                if (!Core.Landing.PredictionReady)
                    return this;

                // If the atomospheric drag is at least 100mm/s2 then start trying to target the overshoot using the parachutes
                if (Core.Landing.DeployChutes)
                {
                    if (Core.Landing.ParachutesDeployable())
                    {
                        Core.Landing.ControlParachutes();
                    }
                }

                double currentError = Vector3d.Distance(Core.Target.GetPositionTargetPosition(), Core.Landing.LandingSite);

                if (currentError < 150)
                {
                    Core.Thrust.TargetThrottle = 0;
                    if (Core.Landing.RCSAdjustment)
                        Core.RCS.Enabled = true;
                    return new CoastToDeceleration(Core);
                }

                // If we're off course, but already too low, skip the course correction
                if (VesselState.AltitudeASL < Core.Landing.DecelerationEndAltitude() + 5)
                {
                    return new DecelerationBurn(Core);
                }


                // If a parachute has already been deployed then we will not be able to control attitude anyway, so move back to the coast to deceleration step.
                if (VesselState.ParachuteDeployed)
                {
                    Core.Thrust.TargetThrottle = 0;
                    return new CoastToDeceleration(Core);
                }

                // We are not in .90 anymore. Turning while under drag is a bad idea
                if (VesselState.DragAcceleration > 0.1)
                {
                    return new CoastToDeceleration(Core);
                }

                Vector3d deltaV = Core.Landing.ComputeCourseCorrection(true);

                Status = Localizer.Format("#MechJeb_LandingGuidance_Status3",
                    deltaV.magnitude.ToString("F1")); //"Performing course correction of about " +  + " m/s"

                Core.Attitude.attitudeTo(deltaV.normalized, AttitudeReference.INERTIAL, Core.Landing);

                if (Core.Attitude.attitudeAngleFromTarget() < 2)
                    _courseCorrectionBurning = true;
                else if (Core.Attitude.attitudeAngleFromTarget() > 30)
                    _courseCorrectionBurning = false;

                if (_courseCorrectionBurning)
                {
                    const double TIME_CONSTANT = 2.0;
                    Core.Thrust.ThrustForDv(deltaV.magnitude, TIME_CONSTANT);
                }
                else
                {
                    Core.Thrust.TargetThrottle = 0;
                }

                return this;
            }
        }
    }
}

}
