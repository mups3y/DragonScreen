// DragonScreen — AttitudePilot  (KSP glue: the direct gimbal/RCS attitude loop — replaces SAS)
// ============================================================================================
// The frame-correct wrapper around the pure BetterController law (pure/AttitudeLoop.cs). The guidance
// hands a WORLD direction to point the nose; this converts it to the body-frame error exactly as MechJeb
// does, reads the live authority + inertia + rates, runs the per-axis cascade, and writes s.pitch/roll/yaw
// through FlightDriver's fly-by-wire hook — driving the ENGINE GIMBAL (ascent/burns) or the RCS/Dracos
// (capsule) directly. No SAS. This is the fix for the max-Q loss of control: a loop fast enough to catch
// FAR's transonic divergence with the ample gimbal authority the vehicle actually has.
//
// Frame conversion (docs/ATTITUDE_CONTROL_RESEARCH.md §1-2, ported from BetterController + DirectionTracker):
//   current  = ReferenceTransform.rotation · Euler(-90,0,0)   (nose → +Z, LookRotation convention)
//   requested= LookRotation(dir, up)   with up = current roll reference (so roll error ≈ 0, roll left free)
//   delta    = Inverse(current)·requested;  euler = delta.eulerAngles (deg)
//   error    = ( ClampPi(euler.x), ClampPi(euler.z), −ClampPi(euler.y) )   // (pitch, roll, yaw); yaw NEGATED
// Then per axis i: AttitudeLoop.Axis(error[i], angVel[i], MOI[i], controlTorque[i], …) → actuation[i];
//   s.pitch=act[0], s.roll=act[1], s.yaw=act[2]. controlTorque = Σ ITorqueProvider.GetPotentialTorque
//   (KSPCommunityFixes makes the gimbal report trustworthy). Roll is rate-damped (dampRoll) or left to the
//   caller's own roll channel (entry bank → FlightDriver.SetRoll).
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class AttitudePilot
    {
        const double Deg2Rad = Math.PI / 180.0, Rad2Deg = 180.0 / Math.PI;

        static readonly Pid2[] posPid = { new Pid2(), new Pid2(), new Pid2() };
        static readonly Pid2[] velPid = { new Pid2(), new Pid2(), new Pid2() };

        // ---- diagnostics for the FlightRecorder (loop internals — the standing instrument-everything rule) ----
        public static bool Active;
        public static double PointErrDeg, RateCmdRads, RateMeasRads;
        public static double ActPitch, ActYaw, ActRoll;
        public static double CtrlTorquePitchNm, CtrlTorqueYawNm;

        public static void Reset()
        {
            for (int i = 0; i < 3; i++) { posPid[i].Reset(); velPid[i].Reset(); }
            Active = false;
            PointErrDeg = RateCmdRads = RateMeasRads = 0.0;
            ActPitch = ActYaw = ActRoll = 0.0;
            CtrlTorquePitchNm = CtrlTorqueYawNm = 0.0;
            FlightDriver.ReleaseAttitude();
        }

        // Point the nose at a world direction. dampRoll = also null the roll RATE (ascent/booster/prox-ops);
        // pass false where a separate roll channel owns roll (the entry bank loop).
        public static void Point(Vessel v, Vector3d worldDir, bool dampRoll)
        {
            if (v == null || v.ReferenceTransform == null || worldDir.magnitude < 1e-6) return;
            try
            {
                double dt = TimeWarp.fixedDeltaTime;
                if (dt <= 0.0) dt = 0.02;

                // --- frame conversion → body-frame error (pitch, roll, yaw), yaw negated ---
                Quaternion current = v.ReferenceTransform.rotation * Quaternion.Euler(-90f, 0f, 0f);
                Vector3 dir = ((Vector3)worldDir).normalized;
                Vector3 up = current * Vector3.up;                       // current roll reference
                if (Mathf.Abs(Vector3.Dot(dir, up)) > 0.99f) up = current * Vector3.right;   // avoid degenerate LookRotation
                Quaternion requested = Quaternion.LookRotation(dir, up);
                Quaternion delta = Quaternion.Inverse(current) * requested;
                Vector3 e = delta.eulerAngles;                          // degrees, 0..360

                double errPitch = ClampPi(e.x * Deg2Rad);
                double errRoll  = ClampPi(e.z * Deg2Rad);
                double errYaw   = -ClampPi(e.y * Deg2Rad);
                double[] error = { errPitch, errRoll, errYaw };

                // --- live plant: inertia, body rates, available control torque (gimbal + RCS + fins) ---
                Vector3 moiV = v.MOI, avV = v.angularVelocity;
                double[] moi = { moiV.x, moiV.y, moiV.z };
                double[] omega = { avV.x, avV.y, avV.z };
                double ctx, cty, ctz; ControlTorque(v, out ctx, out cty, out ctz);
                double[] ct = { ctx, cty, ctz };

                // --- roll-control-range gate: don't fight roll until the nose is pointed ---
                double distanceDeg = AttitudeLoop.PointingDistanceRad(errPitch, errYaw) * Rad2Deg;
                bool rollGate = distanceDeg > AttitudeLoop.RollControlRangeDeg;

                double[] act = new double[3];
                for (int i = 0; i < 3; i++)
                {
                    bool suppress = (i == 1) && rollGate;               // index 1 = roll
                    AttitudeAxisResult res = AttitudeLoop.Axis(error[i], omega[i], moi[i], ct[i], dt,
                                                               suppress, posPid[i], velPid[i]);
                    act[i] = res.Actuation;
                    if (i == 0) RateCmdRads = res.TargetOmega;          // pitch rate command (representative)
                }

                // --- apply: pitch + yaw always; roll only when we own it (else release + keep its PID clean,
                //     so the entry bank loop's own roll channel is not fighting a stale damping command) ---
                FlightDriver.SetAttitude(act[0], act[2]);
                if (dampRoll) FlightDriver.SetAttitudeRoll(act[1]);
                else { FlightDriver.ReleaseAttitudeRoll(); posPid[1].Reset(); velPid[1].Reset(); }

                // --- diagnostics ---
                Active = true;
                PointErrDeg = distanceDeg;
                RateMeasRads = omega[0];
                ActPitch = act[0]; ActYaw = act[2]; ActRoll = act[1];
                CtrlTorquePitchNm = ctx; CtrlTorqueYawNm = ctz;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DragonScreen] AttitudePilot.Point failed: " + ex.Message);
            }
        }

        // Sum the available control torque about each body axis from every torque provider (gimbal, RCS,
        // grid fins, …). Per axis: Σ max(|pos|,|neg|) — equal to MechJeb's Max(Σpos,Σneg) for the symmetric
        // gimbal/RCS this vehicle has. Zero when nothing can produce torque (e.g. engines off in coast) →
        // the loop then commands nothing, which is correct.
        static void ControlTorque(Vessel v, out double tx, out double ty, out double tz)
        {
            // ⛔ RCS torque only counts when the master is ON — otherwise the loop sees authority it cannot
            // actuate (during gimbal-only ascent RCS is off), overcounts the available torque, and under-drives
            // the gimbal. Gimbal/control-surface torque always counts (it needs no master). This makes coast
            // (engines off, RCS off) correctly read zero authority, so the loop commands nothing.
            bool rcsOn = v.ActionGroups[KSPActionGroup.RCS];
            double px = 0.0, py = 0.0, pz = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    PartModule pm = p.Modules[m];
                    if (!rcsOn && pm is ModuleRCS) continue;
                    ITorqueProvider tp = pm as ITorqueProvider;
                    if (tp == null) continue;
                    Vector3 pos, neg;
                    try { tp.GetPotentialTorque(out pos, out neg); }
                    catch { continue; }
                    px += Math.Max(Math.Abs(pos.x), Math.Abs(neg.x));
                    py += Math.Max(Math.Abs(pos.y), Math.Abs(neg.y));
                    pz += Math.Max(Math.Abs(pos.z), Math.Abs(neg.z));
                }
            }
            tx = px; ty = py; tz = pz;
        }

        // Wrap radians to [-π, π].
        static double ClampPi(double a)
        {
            const double TwoPi = 2.0 * Math.PI;
            a %= TwoPi;
            if (a > Math.PI) a -= TwoPi;
            else if (a < -Math.PI) a += TwoPi;
            return a;
        }
    }
}
