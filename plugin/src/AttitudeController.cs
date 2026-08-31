// DragonScreen — AttitudeController  (KSP glue: the per-vehicle direct gimbal/RCS attitude loop)
// ============================================================================================
// The stateful half of the attitude pilot, made INSTANTIABLE so a second vehicle (the separated booster
// flown on its own OnFlyByWire while the Dragon stays active — C2 Step-2) has its OWN loop state instead of
// colliding with the Dragon's. This is the exact frame-correct wrapper around the pure BetterController law
// (pure/AttitudeLoop.cs) that used to live inside the static AttitudePilot — extracted VERBATIM, the only
// change being that Compute() RETURNS the (pitch,yaw,roll) command instead of writing it to FlightDriver's
// active-vessel channels. The caller chooses the sink:
//   • the Dragon (active): the static AttitudePilot facade holds one default instance and writes via
//     FlightDriver.SetAttitude/… — byte-identical to the old behaviour.
//   • the booster (non-active): BoosterControl holds its own instance and writes the command into the
//     booster's OWN FlightCtrlState (the one KSP hands its OnFlyByWire callback).
// The pure loop math (AttitudeLoop.Axis, which already takes the PID state as parameters) is untouched;
// only the glue that HOLDS state + is told where to write moved. Shared [Tunable] config (UseLagComp,
// RcsTorqueFloorNm) stays on AttitudePilot so the tuning surface is unchanged.
//
// Frame conversion (docs/ATTITUDE_CONTROL_RESEARCH.md §1-2, ported from BetterController + DirectionTracker):
//   current  = ReferenceTransform.rotation · Euler(-90,0,0)   (nose → +Z, LookRotation convention)
//   requested= LookRotation(dir, up)   with up = current roll reference (so roll error ≈ 0, roll left free)
//   delta    = Inverse(current)·requested;  euler = delta.eulerAngles (deg)
//   error    = ( ClampPi(euler.x), ClampPi(euler.z), −ClampPi(euler.y) )   // (pitch, roll, yaw); yaw NEGATED
// Then per axis i: AttitudeLoop.Axis(error[i], angVel[i], MOI[i], controlTorque[i], …) → actuation[i].
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    // The result of one Compute(): the actuation to write onto pitch/yaw (always) and roll (when HasRoll).
    // −1..1 each. HasRoll=false means "release roll" (the entry bank owns roll on a separate channel).
    public struct AttitudeCmd
    {
        public double Pitch, Yaw, Roll;
        public bool HasRoll;
    }

    public class AttitudeController
    {
        const double Deg2Rad = Math.PI / 180.0, Rad2Deg = 180.0 / Math.PI;

        readonly Pid2[] posPid = { new Pid2(), new Pid2(), new Pid2() };
        readonly Pid2[] velPid = { new Pid2(), new Pid2(), new Pid2() };

        // Control-torque low-pass (MechJeb BetterController SmoothTorque). The available torque fluctuates —
        // gimbal authority scales with throttle (dips in the max-Q bucket), RCS toggles on/off — and feeding
        // the raw value into actuation=−MOI·α/controlTorque spikes the actuation. Smooth the RISES with an
        // EMA; keep DROPS to zero authority instant (so cutting the engines reads zero immediately).
        const double SmoothTorque = 0.10;
        double smTx, smTy, smTz;
        bool smInit;

        // ---- B4 actuator-lag lead compensation: command the gimbal HARDER when it slews slowly, so the torque
        // reaches the demand faster (a snappier, non-oscillating loop through max-Q). actEst tracks the modeled
        // gimbal position per axis; the issued command is Compensate()'d against it. Self-disables when the
        // gimbal is fast / absent (Compensate → desired). The UseLagComp flag lives on AttitudePilot (shared).
        readonly double[] actEst = { 0.0, 0.0, 0.0 };   // modeled gimbal deflection (pitch, roll, yaw)

        // ---- diagnostics for the FlightRecorder (loop internals — the standing instrument-everything rule) ----
        // Instance fields: the Dragon's default instance publishes these through the AttitudePilot facade (so the
        // recorder + FDIR + AscentControl read the ACTIVE vessel's loop exactly as before); the booster's instance
        // keeps its own, for its own logging.
        public bool Active;
        public double PointErrDeg, RateCmdRads, RateMeasRads;
        public double ActPitch, ActYaw, ActRoll;
        public double CtrlTorquePitchNm, CtrlTorqueYawNm, CtrlTorqueRollNm;
        // Campaign 6 diagnostic: the RAW per-axis geometric RCS torque estimate (before the max() + smoothing), so a
        // flight can PROVE it isn't over-reading vs the stock report's own good spikes. 0 when the RCS master is off.
        public double GeoTorquePitchNm, GeoTorqueYawNm, GeoTorqueRollNm;
        public double PitchAccelRadS2;   // live pitch control angular-accel = τ_pitch / I_pitch (B2 q·α cap)

        bool rcsFallbackLogged;

        // Clear the loop's integrators without dropping the hold — called every tick while the rocket is still
        // clamped, so the position PID cannot wind up against the hold-downs and kick the gimbal at release.
        public void ResetIntegrators()
        {
            for (int i = 0; i < 3; i++) { posPid[i].Reset(); velPid[i].Reset(); }
        }

        // Full reset (scene load / handover). Does NOT touch any FlightDriver channel — the sink owner (the
        // AttitudePilot facade for the active instance) releases its own channels.
        public void Reset()
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
        }

        // Compute the attitude actuation to point the nose at a world direction. dampRoll = also null the roll
        // RATE (ascent/booster/prox-ops); pass false where a separate roll channel owns roll (the entry bank).
        // rollUpRef: a WORLD "up" the vehicle's dorsal axis should track — HOLDS roll (no free spin); zero →
        // the roll reference is the CURRENT roll, i.e. pitch+yaw only, roll left free.
        // Returns the command; the caller writes it to whichever FlightCtrlState / channel it owns.
        public AttitudeCmd Compute(Vessel v, Vector3d worldDir, bool dampRoll, Vector3d rollUpRef)
        {
            AttitudeCmd cmd = new AttitudeCmd();
            if (v == null || v.ReferenceTransform == null || worldDir.magnitude < 1e-6) return cmd;
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
                bool lag = AttitudePilot.UseLagComp;
                double rs = lag ? GimbalResponseSpeed(v) : 1e9;
                double[] issue = { act[0], act[1], act[2] };
                if (lag)
                    for (int i = 0; i < 3; i++)
                    {
                        issue[i] = ActuatorLag.Compensate(actEst[i], act[i], rs, dt);
                        actEst[i] = ActuatorLag.Step(actEst[i], issue[i], rs, dt);
                    }

                // --- build the command: pitch + yaw always; roll only when we own it (else release + keep its
                //     PID clean, so the entry bank loop's own roll channel is not fighting a stale damping cmd) ---
                cmd.Pitch = issue[0]; cmd.Yaw = issue[2];
                if (dampRoll) { cmd.Roll = issue[1]; cmd.HasRoll = true; }
                else { cmd.HasRoll = false; posPid[1].Reset(); velPid[1].Reset(); actEst[1] = 0.0; }

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
                Debug.LogWarning("[DragonScreen] AttitudeController.Compute failed: " + ex.Message);
            }
            return cmd;
        }

        // Sum the available control torque about each body axis from every torque provider (gimbal, RCS,
        // grid fins, …). Per axis: Σ max(|pos|,|neg|) — equal to MechJeb's Max(Σpos,Σneg) for the symmetric
        // gimbal/RCS this vehicle has. Zero when nothing can produce torque (e.g. engines off in coast) →
        // the loop then commands nothing, which is correct.
        void ControlTorque(Vessel v, out double tx, out double ty, out double tz)
        {
            // ⛔ RCS torque only counts when the master is ON — otherwise the loop sees authority it cannot
            // actuate (during gimbal-only ascent RCS is off), overcounts the available torque, and under-drives
            // the gimbal. Gimbal/control-surface torque always counts (it needs no master). This makes coast
            // (engines off, RCS off) correctly read zero authority, so the loop commands nothing.
            //
            // RCS authority per axis = MAX(stock GetPotentialTorque, geometric Σr×F). Total = non-RCS + RCS.
            // Both stock and geometric are in kN·m (KSP force unit), consistent with the gimbal report and with
            // MOI (t·m²), so maxAlpha = controlTorque/MOI comes out in rad/s².
            //
            // ⚠ UNITS BUG FIXED 2026-08-31 (docs/FLIGHT_VERIFICATION.md, DS-ASC-001/002 + the deorbit runs):
            // the geometric sum used to compute per-thruster power as thrusterPower*1000 (N), making the whole
            // geometric estimate N·m — 1000× the kN·m that stock, the gimbal, and the MOI divisor are in. Since
            // MAX() then compared a kN·m stock value against an N·m geometric value, the geometric ALWAYS won and
            // fed maxAlpha a number 1000× too large: S2 maxAlpha read 37.7 rad/s² (real 0.27), capsule 919 rad/s²
            // (real 0.5) → the loop commanded rates the vehicle can't produce → the S2 ascent tumble AND the
            // capsule-spins-under-autopilot deorbit failure. Flight regression (net τ = MOI·Δω vs actuation):
            //     S2  gimbal stock gx = 464 ≈ DELIVERED 445 kN·m (stock gimbal is ACCURATE); RCS delivered ≈ 0
            //     capsule (no gimbal): stock RCS 2, geometric 12.9, DELIVERED 7 kN·m
            // So the FIX is the units (power in kN below), NOT dropping the geometric: Campaign-6's MAX(stock,
            // geometric) is kept because stock under-reads the capsule RCS (2 vs a real 7) and the geometric floors
            // it closer. A closed-loop sim of pure/AttitudeLoop.Axis converges in both configs with the corrected
            // estimate and diverges with the old one (plugin/test/AttitudeLoopTest.cs). KNOWN RESIDUAL (secondary,
            // does NOT destabilise — deferred): the geometric still over-counts ACHIEVABLE torque for asymmetric
            // layouts (S2 RCS: geometric 62 vs delivered ~0, harmless because the 464 gimbal dominates) — a proper
            // achievability cap is future work, tracked in FLIGHT_VERIFICATION.
            // Rule note (V4): flight-proven ControlTorque changed → ascent/abort/booster/deorbit proofs must be
            // re-flown. Not a phase rule, not a slew clamp; roll-trim + tuning untouched.
            bool rcsOn = v.ActionGroups[KSPActionGroup.RCS];
            double gx = 0.0, gy = 0.0, gz = 0.0;               // non-RCS (gimbal/fins) reported (kN·m), per control axis
            double rrx = 0.0, rry = 0.0, rrz = 0.0;            // RCS reported (stock GetPotentialTorque, kN·m), per axis
            Vector3 posT = Vector3.zero, negT = Vector3.zero;  // RCS GEOMETRIC ± torque sums (kN·m, control frame)
            Vector3d com = v.CoM;
            Transform ctf = v.ReferenceTransform;

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    PartModule pm = p.Modules[m];
                    ModuleRCS rcs = pm as ModuleRCS;
                    if (rcs != null)
                    {
                        if (!rcsOn) continue;                  // RCS authority only when the master is ON
                        // (a) keep the stock report so max() can still use it when it IS the true (higher) value.
                        try
                        {
                            Vector3 pos, neg; rcs.GetPotentialTorque(out pos, out neg);
                            rrx += Math.Max(Math.Abs(pos.x), Math.Abs(neg.x));
                            rry += Math.Max(Math.Abs(pos.y), Math.Abs(neg.y));
                            rrz += Math.Max(Math.Abs(pos.z), Math.Abs(neg.z));
                        }
                        catch { }
                        // (b) PER-AXIS r×F geometric estimate (MechJeb VesselState.RCSTorqueAvailable, ported
                        //     faithfully): each thruster's torque = (pos−CoM) × (thrustDir · power), into the
                        //     control frame, accumulated as Max(Σ+, Σ−) per axis — the same convention as the
                        //     report path. power is in kN and arm in m → torque in kN·m, matching stock + the
                        //     gimbal + the MOI divisor. (Was thrusterPower*1000 = N → an N·m estimate 1000× too
                        //     large; that units bug drove the S2 + deorbit tumbles — see the header note above.)
                        if (ctf != null && rcs.rcsEnabled && rcs.thrusterTransforms != null)
                        {
                            double power = rcs.thrusterPower * Math.Max(rcs.thrustPercentage * 0.01, 0.0); // kN
                            if (power > 0.0)
                                for (int t = 0; t < rcs.thrusterTransforms.Count; t++)
                                {
                                    Transform tt = rcs.thrusterTransforms[t];
                                    if (tt == null || !tt.gameObject.activeInHierarchy) continue;
                                    Vector3d dir = rcs.useZaxis ? -(Vector3d)tt.forward : -(Vector3d)tt.up;  // thrusts −nozzle
                                    Vector3d torqueW = Vector3d.Cross((Vector3d)tt.position - com, dir * power);
                                    Vector3 tL = ctf.InverseTransformDirection((Vector3)torqueW);
                                    if (tL.x >= 0f) posT.x += tL.x; else negT.x -= tL.x;
                                    if (tL.y >= 0f) posT.y += tL.y; else negT.y -= tL.y;
                                    if (tL.z >= 0f) posT.z += tL.z; else negT.z -= tL.z;
                                }
                        }
                        continue;
                    }

                    // non-RCS torque providers (gimbal, control surfaces) — always count (need no master).
                    ITorqueProvider tp = pm as ITorqueProvider;
                    if (tp == null) continue;
                    Vector3 gpos, gneg;
                    try { tp.GetPotentialTorque(out gpos, out gneg); }
                    catch { continue; }
                    gx += Math.Max(Math.Abs(gpos.x), Math.Abs(gneg.x));
                    gy += Math.Max(Math.Abs(gpos.y), Math.Abs(gneg.y));
                    gz += Math.Max(Math.Abs(gpos.z), Math.Abs(gneg.z));
                }
            }

            // RCS authority per axis = MAX(stock report, geometric), both now in kN·m (see the header note).
            // Stock wins for the gimbal-backed providers and where it is genuinely higher; the geometric floors
            // the RCS where stock under-reads it (capsule: stock 2 vs a delivered ~7 kN·m). Total = non-RCS + RCS.
            double rcsx = 0.0, rcsy = 0.0, rcsz = 0.0;
            if (rcsOn)
            {
                double ex = Math.Max(posT.x, negT.x), ey = Math.Max(posT.y, negT.y), ez = Math.Max(posT.z, negT.z);
                rcsx = Math.Max(rrx, ex); rcsy = Math.Max(rry, ey); rcsz = Math.Max(rrz, ez);
                GeoTorquePitchNm = ex; GeoTorqueRollNm = ey; GeoTorqueYawNm = ez;   // diagnostic (x=pitch, y=roll, z=yaw)
                if (!rcsFallbackLogged && ex + ey + ez > 0.0 && ex > rrx + AttitudePilot.RcsTorqueFloorNm)
                {
                    Debug.Log("[DragonScreen] AttitudeController: stock RCS GetPotentialTorque ("
                        + rrx.ToString("F1") + "/" + rry.ToString("F1") + "/" + rrz.ToString("F1")
                        + " kN·m) floored by geometric (" + ex.ToString("F1") + "/"
                        + ey.ToString("F1") + "/" + ez.ToString("F1") + " kN·m pitch/roll/yaw).");
                    rcsFallbackLogged = true;
                }
            }
            else { GeoTorquePitchNm = 0.0; GeoTorqueYawNm = 0.0; GeoTorqueRollNm = 0.0; }

            tx = gx + rcsx; ty = gy + rcsy; tz = gz + rcsz;
        }

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
