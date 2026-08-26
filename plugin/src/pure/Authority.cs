// DragonScreen — Authority  (autopilot rebuild L1: the vehicle's own control authority)
// ============================================================================================
// A true autopilot must know its OWN authority so the control law never commands a rate it cannot
// arrest. The glue sums the torque each effector can produce about each body axis — engine gimbal
// (thrust x sin(gimbal range) x moment arm), RCS (thrust x arm x count), grid fins (aero force x
// arm) — and the vehicle's moments of inertia; these pure functions turn that into angular
// acceleration and the arrestable-rate bound. Because it is built from the vehicle's OWN inertia and
// torque, it is automatically right for the full stack, a spent booster, or a bare capsule, with NO
// per-phase gain tuning — the one idea the how-to-build guide (§4.1) says matters most. RO strips
// reaction wheels, so the summed torque is RCS + gimbal + fins only.
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct ControlAuthority
    {
        // Total available control torque about each body axis (N·m) and the axis moments of inertia.
        public double PitchTorqueNm, YawTorqueNm, RollTorqueNm;
        public double PitchInertiaKgM2, YawInertiaKgM2, RollInertiaKgM2;

        public double PitchAccel { get { return Authority.AngularAccel(PitchTorqueNm, PitchInertiaKgM2); } }
        public double YawAccel   { get { return Authority.AngularAccel(YawTorqueNm,   YawInertiaKgM2); } }
        public double RollAccel  { get { return Authority.AngularAccel(RollTorqueNm,  RollInertiaKgM2); } }

        public bool AnyPitchYaw { get { return PitchAccel > 0.0 || YawAccel > 0.0; } }
    }

    public static class Authority
    {
        // Angular acceleration a body axis can produce: alpha = torque / inertia (rad/s^2).
        public static double AngularAccel(double torqueNm, double inertiaKgM2)
        {
            if (inertiaKgM2 <= 1e-9 || torqueNm <= 0.0) return 0.0;
            return torqueNm / inertiaKgM2;
        }

        // ⛔ THE ONE THAT MATTERS: never command a rate you cannot arrest. The fastest rate that can
        // still be braked to zero over an angle error theta, at angular acceleration alpha, is the
        // braking curve  omega_max = sqrt(2 * alpha * theta)  — the same law a hoverslam uses, and it
        // falls out of the vehicle's own authority, so no gains are tuned. The control law (L2) caps its
        // commanded rate at this. Radians in, radians/s out.
        public static double ArrestableRate(double angularAccel, double angleErrorRad)
        {
            if (angularAccel <= 0.0 || angleErrorRad <= 0.0) return 0.0;
            return Math.Sqrt(2.0 * angularAccel * angleErrorRad);
        }

        // Sum a set of per-effector torque contributions about one axis (the glue collects them per part).
        public static double SumTorque(double[] contributionsNm)
        {
            if (contributionsNm == null) return 0.0;
            double t = 0.0;
            for (int i = 0; i < contributionsNm.Length; i++)
                if (contributionsNm[i] > 0.0) t += contributionsNm[i];
            return t;
        }
    }
}
