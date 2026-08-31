// DragonScreen — Steering  (KSP glue: point the vehicle where the guidance says)
// ============================================================================================
// The inner attitude servo + the world-direction math the flying controllers share. The guidance
// (pure) decides WHERE to point (a pitch-program direction, a UPFG thrust vector, a retrograde, an
// LVLH burn axis); this turns that into a held attitude on the live vessel.
//
// ⭐ INNER LOOP = the DIRECT gimbal/RCS loop (AttitudePilot), the committed inner loop — Point() routes to
// AttitudePilot when UseGimbalLoop=true (the default + current state). It drives whatever authority the
// vehicle actually has (S1 engine gimbal on ascent, the Dracos on the capsule). Stock SAS was too slow for
// FAR's transonic divergence (lost control 3×) and remains ONLY as a one-flip fallback behind
// UseGimbalLoop=false. (History: the very first flights used SAS while the guidance was validated; that is
// long superseded — do not read this as "SAS is live".) Frame conventions are the standard KSP ones (ENU
// from the body); a mirrored heading would show as a plane error in the CSV and is a one-line sign fix —
// and SelfCal.SteerSign is the guard for exactly that.
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
            // ⛔ USE KSP'S OWN SURFACE FRAME (v.north / v.east), NOT a hand-rolled one from body.transform.up.
            // MechJeb (the proven KSP ascent guidance — primary source) does exactly this: VesselState.North =
            // vessel.north, East = vessel.east. Our old body.transform.up derivation is WRONG under RSS/Kopernicus,
            // where bodies are reoriented, so the pole axis was not true north — azimuth 42.8° (NE) mapped to a
            // retrograde world direction and the orbit came out inc 116° instead of 51.6° (flights 190114/201648).
            // v.north / v.east are unit world vectors KSP maintains from the body's ACTUAL rotation — correct on
            // any body. Keep our radial 'up' (matches MechJeb's OrbitalPosition-normalized up).
            up = Up(v);
            north = v.north;
            east = v.east;
            // Degenerate guard (should never trigger for a loaded vessel): fall back to a built frame.
            if (north.sqrMagnitude < 1e-9 || east.sqrMagnitude < 1e-9)
            {
                Vector3d poleAxis = (v.mainBody != null) ? (Vector3d)v.mainBody.transform.up : Vector3d.up;
                north = (poleAxis - up * Vector3d.Dot(poleAxis, up));
                if (north.magnitude < 1e-6) north = Vector3d.Cross(up, v.transform.right);
                north = north.normalized;
                east = Vector3d.Cross(north, up).normalized;
            }
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

        // ⛔ Hold the given WORLD direction. ⭐ ATTITUDE CONTROL MODE. true (owner 2026-09-01, "use our sas") =
        // the custom direct gimbal/RCS loop (AttitudePilot) — OUR SAS. Its hold authority is scaled by
        // AttitudePilot.HoldAuthorityScale (1.5× per owner). false = hand attitude to STOCK KSP SAS (the last
        // test build; it over-steered the pitch-over into a flip). The autopilot computes the aim either way.
        [Tunable] public static bool UseGimbalLoop = true;
        static bool sasReady;

        public static void Point(Vessel v, Vector3d worldDir) { Hold(v, worldDir, true, Vector3d.zero); }
        public static void PointNoRoll(Vessel v, Vector3d worldDir) { Hold(v, worldDir, false, Vector3d.zero); }

        // Point the nose AND actively hold roll so the vehicle's dorsal axis tracks worldUpRef (ascent: keeps
        // the booster from free-spinning, so the gravity turn stays in the launch plane → correct inclination).
        public static void PointHoldRoll(Vessel v, Vector3d worldDir, Vector3d worldUpRef)
        { Hold(v, worldDir, true, worldUpRef); }

        static void Hold(Vessel v, Vector3d worldDir, bool dampRoll, Vector3d rollUpRef)
        {
            if (v == null || worldDir.magnitude < 1e-6) return;
            if (UseGimbalLoop) { AttitudePilot.Point(v, worldDir, dampRoll, rollUpRef); return; }
            try   // ---- STOCK KSP SAS holds attitude on the Dracos (UseGimbalLoop=false) ----
            {
                FlightDriver.ReleaseAttitude();   // yield the direct-loop pitch/yaw/roll so OnFlyByWire cannot override SAS
                if (!v.ActionGroups[KSPActionGroup.RCS]) v.ActionGroups.SetGroup(KSPActionGroup.RCS, true);   // SAS holds on RCS (no reaction wheels)
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
                Debug.LogWarning("[DragonScreen] steering point (SAS) failed: " + e.Message);
            }
        }

        // Release the hold (e.g. at handover / disengage) so the next controller re-seeds it.
        public static void Release() { sasReady = false; AttitudePilot.Reset(); }

        // Angle (deg) between the vessel's nose (control-forward) and a world direction — the pointing error.
        public static double PointingErrorDeg(Vessel v, Vector3d worldDir)
        {
            Transform rt = v.ReferenceTransform;
            if (rt == null || worldDir.magnitude < 1e-6) return 0.0;
            return Vector3d.Angle(rt.up, worldDir);   // control-forward is transform.up for a rocket
        }
    }
}
