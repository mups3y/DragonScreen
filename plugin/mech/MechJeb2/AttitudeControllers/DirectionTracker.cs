// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
using UnityEngine;
using static MechJebLib.Utils.Statics;
using static System.Math;

namespace MuMech.AttitudeControllers
{
    public class DirectionTracker
    {
        private QuaternionD _previousRotation;
        public Vector3d TrackedRotation;

        private bool NeedsInitialization() => _previousRotation == new QuaternionD(0, 0, 0, 0);

        public DirectionTracker()
        {
            Reset();
        }

        public Vector3d Update(QuaternionD currentRotation)
        {
            if (NeedsInitialization())
            {
                _previousRotation = currentRotation;
                TrackedRotation = Vector3d.zero;
                return TrackedRotation;
            }

            // Calculate the change in rotation and the change in euler angles
            QuaternionD deltaRotation = QuaternionD.Inverse(_previousRotation) * currentRotation;
            Vector3d deltaEuler = MathExtensions.EulerAngles(deltaRotation);

            // Convert to radians and clamp to [-pi, pi]
            double deltaPitch = ClampPi(Deg2Rad(deltaEuler.x));
            double deltaYaw = -ClampPi(Deg2Rad(deltaEuler.y));
            double deltaRoll = ClampPi(Deg2Rad(deltaEuler.z));

            // Apply in the KSP/MJ pitch/roll/yaw order
            TrackedRotation.x += deltaPitch;
            TrackedRotation.y += deltaRoll;
            TrackedRotation.z += deltaYaw;

            // Save the current rotation as previous for next update
            _previousRotation = currentRotation;

            return TrackedRotation;
        }

        public (Vector3d desired, Vector3d error, double distance) Desired(QuaternionD desiredRotation)
        {
            QuaternionD deltaRotation = QuaternionD.Inverse(_previousRotation) * desiredRotation;
            Vector3d deltaEuler = MathExtensions.EulerAngles(deltaRotation);

            // Convert to radians and clamp to [-pi, pi]
            double deltaPitch = ClampPi(Deg2Rad(deltaEuler.x));
            double deltaYaw = -ClampPi(Deg2Rad(deltaEuler.y));
            double deltaRoll = ClampPi(Deg2Rad(deltaEuler.z));

            var error = new Vector3d(deltaPitch, deltaRoll, deltaYaw);

            double distance = SafeAcos(Cos(deltaPitch) * Cos(deltaYaw));

            return (TrackedRotation + error, error, distance);
        }

        // This method is used to reset the accumulated rotation
        public void Reset()
        {
            _previousRotation = new QuaternionD(0, 0, 0, 0);
            TrackedRotation = Vector3d.zero;
        }

        // This method is used to reset the accumulated rotation for a specific axis
        public void Reset(int i) => TrackedRotation[i] = 0;
    }
}

}
