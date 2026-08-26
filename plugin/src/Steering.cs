// DragonScreen — Steering  (KSP glue: point the vehicle where the guidance says)
// ============================================================================================
// The inner attitude servo + the world-direction math the flying controllers share. The guidance
// (pure) decides WHERE to point (a pitch-program direction, a UPFG thrust vector, a retrograde, an
// LVLH burn axis); this turns that into a held attitude on the live vessel.
//
// FIRST-CUT INNER LOOP = stock SAS target-hold. SAS drives whatever authority the vehicle actually has
// (S1 engine gimbal on ascent, the Dracos on the capsule), which is exactly the right effector set, and
// it is battle-tested — so the FIRST flights validate the GUIDANCE (ours) without a bespoke steering
// controller underneath it. The pure ControlLaw + Authority attitude loop is a clean later swap here once
// the guidance is proven against the flight recording (docs plan §8b, glue seams). Frame conventions are
// the standard KSP ones (ENU from the body); a mirrored heading would show as a plane error in the CSV
// and is a one-line sign fix — and SelfCal.SteerSign is the guard for exactly that.
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class Steering
    {
        // local vertical (radial out), world frame
        public static Vector3d Up(Vessel v)
        {
            if (v.mainBody == null) return v.transform.up;
            return (v.CoM - v.mainBody.position).normalized;
        }

        // horizontal north + east at the vessel (ENU, right-handed: East = North × Up)
        public static void NorthEast(Vessel v, out Vector3d up, out Vector3d north, out Vector3d east)
        {
            up = Up(v);
            Vector3d poleAxis = (v.mainBody != null) ? (Vector3d)v.mainBody.transform.up : Vector3d.up;
            north = (poleAxis - up * Vector3d.Dot(poleAxis, up));
            if (north.magnitude < 1e-6) north = Vector3d.Cross(up, v.transform.right);   // at a pole
            north = north.normalized;
            east = Vector3d.Cross(north, up).normalized;
        }

        // A world direction at flight-path pitch (deg, 90 = straight up) on ground azimuth (rad, CW from N).
        public static Vector3d PitchHeadingDir(Vessel v, double pitchDeg, double azRad)
        {
            Vector3d up, north, east;
            NorthEast(v, out up, out north, out east);
            Vector3d horiz = north * Math.Cos(azRad) + east * Math.Sin(azRad);
            double p = pitchDeg * Math.PI / 180.0;
            return (up * Math.Sin(p) + horiz * Math.Cos(p)).normalized;
        }

        public static Vector3d Prograde(Vessel v)
        {
            Vector3d vv = v.obt_velocity;
            return vv.magnitude > 0.1 ? vv.normalized : v.transform.up;
        }

        // ⛔ ZERO-AoA LOAD RELIEF. A launch vehicle must NEVER be commanded far off its velocity vector in
        // the atmosphere — the aero side-force is q·Cn·α and at max-Q even a few degrees of angle of attack
        // will rip the stack apart (RUD). So clamp a desired attitude to within maxAoaDeg of SURFACE
        // prograde: the pitch program then only ever LEADS the velocity by a bounded amount, and the gravity
        // turn stays load-relieved. Below ~30 m/s there is no meaningful prograde yet, so pass it through.
        public static Vector3d LimitToProgradeCone(Vessel v, Vector3d desired, double maxAoaDeg)
        {
            Vector3d vel = v.srf_velocity;
            if (vel.magnitude < 30.0 || desired.magnitude < 1e-6) return desired;
            Vector3d pro = vel.normalized, des = desired.normalized;
            double ang = Vector3d.Angle(pro, des);
            if (ang <= maxAoaDeg || ang < 1e-6) return desired;
            Vector3d axis = Vector3d.Cross(pro, des);
            if (axis.magnitude < 1e-9) return desired;
            return (Vector3d)(Quaternion.AngleAxis((float)maxAoaDeg, ((Vector3)axis).normalized) * (Vector3)pro);
        }

        // Angle of attack (deg): the vehicle's nose (control-forward) vs its surface velocity.
        public static double AngleOfAttackDeg(Vessel v)
        {
            Transform rt = v.ReferenceTransform;
            if (rt == null || v.srf_velocity.magnitude < 1.0) return 0.0;
            return Vector3d.Angle(rt.up, v.srf_velocity);
        }

        // Hold the given WORLD direction with stock SAS (our guidance owns the direction).
        static bool sasReady;
        public static void Point(Vessel v, Vector3d worldDir)
        {
            if (v == null || worldDir.magnitude < 1e-6) return;
            try
            {
                if (!v.ActionGroups[KSPActionGroup.SAS]) v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                if (v.Autopilot != null)
                {
                    v.Autopilot.Enable();
                    if (v.Autopilot.Mode != VesselAutopilot.AutopilotMode.StabilityAssist)
                        v.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);
                    if (v.Autopilot.SAS != null)
                        v.Autopilot.SAS.SetTargetOrientation(worldDir.normalized, !sasReady);
                }
                sasReady = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] steering point failed: " + e.Message);
            }
        }

        // Release the hold (e.g. at handover / disengage) so the next controller re-seeds it.
        public static void Release() { sasReady = false; }

        // Angle (deg) between the vessel's nose (control-forward) and a world direction — the pointing error.
        public static double PointingErrorDeg(Vessel v, Vector3d worldDir)
        {
            Transform rt = v.ReferenceTransform;
            if (rt == null || worldDir.magnitude < 1e-6) return 0.0;
            return Vector3d.Angle(rt.up, worldDir);   // control-forward is transform.up for a rocket
        }
    }
}
