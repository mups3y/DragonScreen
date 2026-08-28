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

        // Control-torque low-pass (MechJeb BetterController SmoothTorque). The available torque fluctuates —
        // gimbal authority scales with throttle (dips in the max-Q bucket), RCS toggles on/off — and feeding
        // the raw value into actuation=−MOI·α/controlTorque spikes the actuation. Smooth the RISES with an
        // EMA; keep DROPS to zero authority instant (so cutting the engines reads zero immediately).
        const double SmoothTorque = 0.10;
        static double smTx, smTy, smTz;
        static bool smInit;

        // ---- B4 actuator-lag lead compensation: command the gimbal HARDER when it slews slowly, so the torque
        // reaches the demand faster (a snappier, non-oscillating loop through max-Q). actEst tracks the modeled
        // gimbal position per axis; the issued command is Compensate()'d against it. Self-disables when the
        // gimbal is fast / absent (Compensate → desired). Flag lets I-B flight-verify or fall back.
        [Tunable] public static bool UseLagComp = true;
        static readonly double[] actEst = { 0.0, 0.0, 0.0 };   // modeled gimbal deflection (pitch, roll, yaw)

        // ---- diagnostics for the FlightRecorder (loop internals — the standing instrument-everything rule) ----
        public static bool Active;
        public static double PointErrDeg, RateCmdRads, RateMeasRads;
        public static double ActPitch, ActYaw, ActRoll;
        public static double CtrlTorquePitchNm, CtrlTorqueYawNm, CtrlTorqueRollNm;
        public static double PitchAccelRadS2;   // live pitch control angular-accel = τ_pitch / I_pitch (B2 q·α cap)

        // Clear the loop's integrators without dropping the hold — called every tick while the rocket is still
        // clamped, so the position PID cannot wind up against the hold-downs and kick the gimbal at release.
        public static void ResetIntegrators()
        {
            for (int i = 0; i < 3; i++) { posPid[i].Reset(); velPid[i].Reset(); }
        }

        public static void Reset()
        {
            for (int i = 0; i < 3; i++) { posPid[i].Reset(); velPid[i].Reset(); }
            smInit = false; smTx = smTy = smTz = 0.0;
            rcsFallbackLogged = false;
            Active = false;
            PointErrDeg = RateCmdRads = RateMeasRads = 0.0;
            ActPitch = ActYaw = ActRoll = 0.0;
            CtrlTorquePitchNm = CtrlTorqueYawNm = CtrlTorqueRollNm = 0.0;
            PitchAccelRadS2 = 0.0;
            actEst[0] = actEst[1] = actEst[2] = 0.0;   // B4: clear the modeled gimbal state on scene load
            FlightDriver.ReleaseAttitude();
        }

        // Point the nose at a world direction. dampRoll = also null the roll RATE (ascent/booster/prox-ops);
        // pass false where a separate roll channel owns roll (the entry bank loop).
        public static void Point(Vessel v, Vector3d worldDir, bool dampRoll)
        { Point(v, worldDir, dampRoll, Vector3d.zero); }

        // rollUpRef: a WORLD "up" the vehicle's dorsal axis should track — this HOLDS roll (no free spin), so
        // e.g. the ascent pitch-over stays in the launch plane and the inclination comes out right. Zero →
        // the old behaviour: the roll reference is the CURRENT roll, i.e. pitch+yaw only, roll left free.
        public static void Point(Vessel v, Vector3d worldDir, bool dampRoll, Vector3d rollUpRef)
        {
            if (v == null || v.ReferenceTransform == null || worldDir.magnitude < 1e-6) return;
            try
            {
                double dt = TimeWarp.fixedDeltaTime;
                if (dt <= 0.0) dt = 0.02;

                // --- frame conversion → body-frame error (pitch, roll, yaw), yaw negated ---
                Quaternion current = v.ReferenceTransform.rotation * Quaternion.Euler(-90f, 0f, 0f);
                Vector3 dir = ((Vector3)worldDir).normalized;
                // Roll reference: a fixed WORLD up (rollUpRef) HOLDS roll; else the current roll = free roll.
                Vector3 up = rollUpRef.magnitude > 1e-6 ? ((Vector3)rollUpRef).normalized : current * Vector3.up;
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
                double ctxRaw, ctyRaw, ctzRaw; ControlTorque(v, out ctxRaw, out ctyRaw, out ctzRaw);
                if (!smInit) { smTx = ctxRaw; smTy = ctyRaw; smTz = ctzRaw; smInit = true; }
                else
                {
                    smTx += SmoothTorque * (ctxRaw - smTx);
                    smTy += SmoothTorque * (ctyRaw - smTy);
                    smTz += SmoothTorque * (ctzRaw - smTz);
                }
                if (ctxRaw == 0.0) smTx = 0.0;   // drop to zero authority is instant (don't lag an engine cut)
                if (ctyRaw == 0.0) smTy = 0.0;
                if (ctzRaw == 0.0) smTz = 0.0;
                double[] ct = { smTx, smTy, smTz };

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

                // --- B4 actuator-lag lead compensation: issue a command that pulls the LAGGED gimbal to the
                //     loop's demand this tick (Compensate), tracking the modeled deflection (Step). rs = the
                //     gimbal gap-closing rate (KSP lerps by responseSpeed·dt); no active gimbal → instant → no-op.
                double rs = UseLagComp ? GimbalResponseSpeed(v) : 1e9;
                double[] issue = { act[0], act[1], act[2] };
                if (UseLagComp)
                    for (int i = 0; i < 3; i++)
                    {
                        issue[i] = ActuatorLag.Compensate(actEst[i], act[i], rs, dt);
                        actEst[i] = ActuatorLag.Step(actEst[i], issue[i], rs, dt);
                    }

                // --- apply: pitch + yaw always; roll only when we own it (else release + keep its PID clean,
                //     so the entry bank loop's own roll channel is not fighting a stale damping command) ---
                FlightDriver.SetAttitude(issue[0], issue[2]);
                if (dampRoll) FlightDriver.SetAttitudeRoll(issue[1]);
                else { FlightDriver.ReleaseAttitudeRoll(); posPid[1].Reset(); velPid[1].Reset(); actEst[1] = 0.0; }

                // --- diagnostics ---
                Active = true;
                PointErrDeg = distanceDeg;
                RateMeasRads = omega[0];
                ActPitch = act[0]; ActYaw = act[2]; ActRoll = act[1];
                CtrlTorquePitchNm = smTx; CtrlTorqueYawNm = smTz; CtrlTorqueRollNm = smTy;   // authority per axis
                PitchAccelRadS2 = moi[0] > 1e-6 ? smTx / moi[0] : 0.0;   // pitch angular authority for the q·α cap
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
            double rcsReported = 0.0;   // how much of the total came from stock RCS GetPotentialTorque
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
                    double ax = Math.Max(Math.Abs(pos.x), Math.Abs(neg.x));
                    double ay = Math.Max(Math.Abs(pos.y), Math.Abs(neg.y));
                    double az = Math.Max(Math.Abs(pos.z), Math.Abs(neg.z));
                    px += ax; py += ay; pz += az;
                    if (pm is ModuleRCS) rcsReported += ax + ay + az;
                }
            }

            // ⛔ STOCK ModuleRCS.GetPotentialTorque BUG WORKAROUND (data-confirmed, flights 135356/174959/180029):
            // on the SEPARATED abort capsule the RCS reported ~0 potential torque even with the master ON and the
            // Dracos able to fire ~21 kN → the loop saw ZERO authority → commanded nothing → the capsule tumbled at
            // ~17°/s and the crew died. (Normal flight reports ~735 N·m and is untouched.) MechJeb doesn't trust
            // stock GetPotentialTorque for RCS either — it estimates. So when the master is on and enabled RCS
            // thrusters exist but the reported RCS torque is ~0, fall back to a geometric estimate (Σ thrusterPower ×
            // moment-arm-to-CoM) so AttitudePilot has authority to actuate. KSP applies the REAL torque (the Dracos
            // fire); the estimate only sets the actuation SCALE. ⚠ magnitude to confirm on the next instrumented abort.
            if (rcsOn && rcsReported < RcsTorqueFloorNm)
            {
                // ⭐ PER-AXIS r×F estimate (MechJeb VesselState.RCSTorqueAvailable, ported faithfully). The old
                // estimate summed thrusterPower × |arm| over ALL thrusters and put the SAME value on every axis —
                // it over-counted ~9× (16 Dracos × 2 kN × ~3.5 m ≈ 112 kN·m vs a real ~10-15 kN·m/axis). That made
                // the arrestable-rate law α=τ/MOI over-estimate the authority → it commanded ω=√(2αθ) rates far too
                // high → the capsule OVERSHOT retrograde and oscillated (deorbit ptErr p95 128-178°, flights 235430/
                // 232712/000624) → the deorbit burn fired mid-swing, mis-pointed → it did NOT deorbit → stranded.
                // The fix: each thruster's torque = (pos−CoM) × (thrustDir · power), projected into the control
                // frame, accumulated per axis as Max(Σ+, Σ−) — the same convention as the GetPotentialTorque path.
                Vector3d com = v.CoM;
                Transform ctf = v.ReferenceTransform;
                if (ctf != null)
                {
                    Vector3 posT = Vector3.zero, negT = Vector3.zero;   // per-axis +/- torque sums (control frame)
                    for (int i = 0; i < v.parts.Count; i++)
                    {
                        var rl = v.parts[i].Modules.GetModules<ModuleRCS>();
                        for (int k = 0; k < rl.Count; k++)
                        {
                            ModuleRCS r = rl[k];
                            if (!r.rcsEnabled || r.thrusterTransforms == null) continue;
                            double power = r.thrusterPower * 1000.0 * Math.Max(r.thrustPercentage * 0.01, 0.0);   // N
                            if (!(power > 0.0)) continue;
                            for (int t = 0; t < r.thrusterTransforms.Count; t++)
                            {
                                Transform tt = r.thrusterTransforms[t];
                                if (tt == null || !tt.gameObject.activeInHierarchy) continue;
                                Vector3d dir = r.useZaxis ? -(Vector3d)tt.forward : -(Vector3d)tt.up;  // RCS thrusts −nozzle
                                Vector3d torqueW = Vector3d.Cross((Vector3d)tt.position - com, dir * power);
                                Vector3 tL = ctf.InverseTransformDirection((Vector3)torqueW);
                                if (tL.x >= 0f) posT.x += tL.x; else negT.x -= tL.x;
                                if (tL.y >= 0f) posT.y += tL.y; else negT.y -= tL.y;
                                if (tL.z >= 0f) posT.z += tL.z; else negT.z -= tL.z;
                            }
                        }
                    }
                    double ex = Math.Max(posT.x, negT.x), ey = Math.Max(posT.y, negT.y), ez = Math.Max(posT.z, negT.z);
                    if (ex + ey + ez > 0.0)
                    {
                        px = Math.Max(px, ex); py = Math.Max(py, ey); pz = Math.Max(pz, ez);
                        if (!rcsFallbackLogged)
                        { Debug.LogWarning("[DragonScreen] AttitudePilot: stock RCS GetPotentialTorque ~0 — using the "
                            + "per-axis geometric RCS torque (" + ex.ToString("F0") + "/" + ey.ToString("F0") + "/"
                            + ez.ToString("F0") + " N·m pitch/roll/yaw) so the capsule can be controlled."); rcsFallbackLogged = true; }
                    }
                }
            }

            tx = px; ty = py; tz = pz;
        }
        static bool rcsFallbackLogged;
        [Tunable] public static double RcsTorqueFloorNm = 1.0;   // below this reported RCS torque → use the geometric estimate

        // The gimbal gap-closing rate (per second) to feed the B4 lag model: KSP lerps the gimbal toward its
        // target by gimbalResponseSpeed·dt each tick, so responseSpeed IS the model's 1/τ. Take the SLOWEST
        // active gimbal (the one that governs the lag). No active gimbal, or none using response-speed (instant
        // gimbals) → a large value so Compensate becomes a no-op. Guarded: any KSP-access issue → instant.
        static double GimbalResponseSpeed(Vessel v)
        {
            double slowest = double.PositiveInfinity;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        ModuleGimbal g = p.Modules[m] as ModuleGimbal;
                        if (g == null || !g.gimbalActive || !g.useGimbalResponseSpeed) continue;
                        double rs = g.gimbalResponseSpeed;
                        if (rs > 0.0 && rs < slowest) slowest = rs;
                    }
                }
            }
            catch { return 1e9; }
            return double.IsPositiveInfinity(slowest) ? 1e9 : slowest;   // no lagging gimbal → instant
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
