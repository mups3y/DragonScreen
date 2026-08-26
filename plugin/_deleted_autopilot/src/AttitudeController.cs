// DragonScreen - AttitudeController
// ---- ⛔ ONE INSTANCE PER VEHICLE. THIS USED TO BE A STATIC CLASS AND THAT WAS THE CEILING. ----
// ---- WHY THROTTLE LIVES HERE TOO ----
// ---- THE FRAME CORRECTION IS NOT OPTIONAL ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public class AttitudeController
    {
        private const string Tag = "[DragonScreen] ";

        // ---- THE TWO VEHICLES WE EVER FLY AT ONCE ----
        public static readonly AttitudeController Ascent = new AttitudeController("ascent");
        public static readonly AttitudeController Booster = new AttitudeController("booster");

        public static AttitudeController For(Vessel v)
        {
            if (v == null) return null;
            if (Ascent.attached == v) return Ascent;
            if (Booster.attached == v) return Booster;
            return null;
        }

        private readonly string who;
        public AttitudeController(string name) { who = name; }

        private Vector3d targetForward, targetTop;
        private bool active;
        private Vessel attached;

        private Vector3d actuation = Vector3d.zero;

        /// ---- ⛔ THIS WAS A CONSTANT 5 AND F9I SETS IT TO 45 FOR THE FLIP. ----
        public double RollControlRangeDeg = Attitude.RollControlRangeDeg;

        public bool LockRoll = false;

        public double TimeConstantS = Attitude.DefaultTimeConstantS;

        public double MaxRateDps = Attitude.AscentMaxRateDps;

        public double Throttle;

        public double UllageFore;

        public double TranslateX, TranslateY;

        public double ErrorDeg { get; private set; }

        public Vessel Vehicle { get { return attached; } }

        // ---- INTERNALS EXPOSED FOR THE RECORDER ----
        public Vector3d Phi, TargetOmega, Omega, TargetTorque, Actuation, Torque, Moi;
        public bool Steering { get { return active; } }

        // ------------------------------------------------------------------ public

        public void SteerTo(Vessel v, Vector3d forward, Vector3d up)
        {
            if (v == null || forward.sqrMagnitude < 1e-6) { Release(v); return; }

            Attach(v);
            targetForward = forward.normalized;

            // ---- ⛔ THIS FALLBACK SPUN THE ROCKET ON THE PAD AT 64 DEG/S. ----
            Vector3d u = up;
            if (u.sqrMagnitude < 1e-6 || Math.Abs(Vector3d.Dot(u.normalized, targetForward)) > 0.999)
            {
                QuaternionD nowRot = (QuaternionD)(v.ReferenceTransform.rotation
                                                   * Quaternion.Euler(-90f, 0f, 0f));
                u = nowRot * Vector3d.up;
            }
            targetTop = Vector3d.Exclude(targetForward, u).normalized;
            active = true;
        }

        public void Release(Vessel v)
        {
            active = false;
            Throttle = 0.0;
            UllageFore = 0.0;
            TranslateX = 0.0; TranslateY = 0.0;
            if (attached != null && attached.ctrlState != null) attached.ctrlState.mainThrottle = 0f;
            Detach();
            actuation = Vector3d.zero;
            RollControlRangeDeg = Attitude.RollControlRangeDeg;
            LockRoll = false;
            TimeConstantS = Attitude.DefaultTimeConstantS;
            MaxRateDps = Attitude.AscentMaxRateDps;
        }

        private void Attach(Vessel v)
        {
            if (attached == v) return;
            Detach();
            v.OnFlyByWire += Drive;
            attached = v;
            v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            Debug.Log(Tag + who + " controller attached to '" + v.vesselName + "', SAS off");
        }

        private void Detach()
        {
            if (attached == null) return;
            attached.OnFlyByWire -= Drive;
            attached = null;
        }

        // ------------------------------------------------------------------ the loop

        private void Drive(FlightCtrlState s)
        {
            try
            {
                s.mainThrottle = Mathf.Clamp01((float)Throttle);
                if (UllageFore > 0.001 || UllageFore < -0.001)
                    s.Z = -Mathf.Clamp((float)UllageFore, -1f, 1f);
                // ---- ⛔ NEGATED, AND THE NEGATION IS MEASURED. ----
                if (TranslateX > 0.001 || TranslateX < -0.001)
                    s.X = -Mathf.Clamp((float)TranslateX, -1f, 1f);
                if (TranslateY > 0.001 || TranslateY < -0.001)
                    s.Y = Mathf.Clamp((float)TranslateY, -1f, 1f);
                if (active) DriveInner(s);
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + who + " controller FAILED and has detached - the vehicle is "
                               + "not being steered: " + e);
                active = false;
                Detach();
            }
        }

        private void DriveInner(FlightCtrlState s)
        {
            Vessel v = attached;
            if (v == null || v.ReferenceTransform == null) return;

            double dt = TimeWarp.fixedDeltaTime;
            if (dt <= 0.0) return;

            // ---- STATE VECTORS ----
            // ---- ⛔ NOT QuaternionD.Euler. IT THROWS MissingMethodException IN THIS KSP. ----
            QuaternionD rot = (QuaternionD)(v.ReferenceTransform.rotation
                                            * Quaternion.Euler(-90f, 0f, 0f));
            Vector3d fwd = rot * Vector3d.forward;
            Vector3d top = rot * Vector3d.up;
            Vector3d star = rot * Vector3d.right;
            Vector3d omega = -v.angularVelocity;

            // ---- ATTITUDE ERROR ----
            double phiTotal = Vector3d.Angle(fwd, targetForward) * Mathf.Deg2Rad;
            if (Vector3d.Angle(top, targetForward) > 90.0) phiTotal *= -1.0;
            ErrorDeg = Math.Abs(phiTotal) * Mathf.Rad2Deg;

            Vector3d phi = Vector3d.zero;
            phi[0] = Vector3d.Angle(fwd, Vector3d.Exclude(star, targetForward)) * Mathf.Deg2Rad;
            if (Vector3d.Angle(top, Vector3d.Exclude(star, targetForward)) > 90.0) phi[0] *= -1.0;
            phi[1] = Vector3d.Angle(top, Vector3d.Exclude(fwd, targetTop)) * Mathf.Deg2Rad;
            if (Vector3d.Angle(star, Vector3d.Exclude(fwd, targetTop)) > 90.0) phi[1] *= -1.0;
            phi[2] = Vector3d.Angle(fwd, Vector3d.Exclude(top, targetForward)) * Mathf.Deg2Rad;
            if (Vector3d.Angle(star, Vector3d.Exclude(top, targetForward)) > 90.0) phi[2] *= -1.0;

            EnsureRcsAuthority(v);

            Vector3d moi = v.MOI;
            Vector3d torque = AvailableTorque(v);

            // ---- ⛔ NATIVE CONTROL LAW. `pure/Attitude.cs`. NO kOS IN IT. ----
            Vector3d targetOmega = Vector3d.zero;
            // ---- ⛔ THE ERROR GOES IN WITH ITS OWN SIGN. NOT NEGATED. ----
            double capRad = MaxRateDps * Mathf.Deg2Rad;
            targetOmega[0] = Attitude.RateCommand(phi[0], torque.x, moi.x, capRad);
            targetOmega[1] = Attitude.RateCommand(phi[1], torque.y, moi.y, capRad);
            targetOmega[2] = Attitude.RateCommand(phi[2], torque.z, moi.z, capRad);

            // ---- DO NOT FIGHT FOR ROLL WHILE STILL SLEWING ----
            if (Math.Abs(phiTotal) > RollControlRangeDeg * Mathf.Deg2Rad) targetOmega[1] = 0.0;

            // ---- ⛔ LOCK ROLL: NEVER COMMAND A POSITIONAL ROLL. (user, 2026-08-20) ----
            if (LockRoll) targetOmega[1] = 0.0;

            Vector3d targetTorque = Vector3d.zero;
            targetTorque[0] = Attitude.TorqueCommand(targetOmega[0] - omega[0], moi.x, TimeConstantS);
            targetTorque[1] = Attitude.TorqueCommand(targetOmega[1] - omega[1], moi.y, TimeConstantS);
            targetTorque[2] = Attitude.TorqueCommand(targetOmega[2] - omega[2], moi.z, TimeConstantS);

            actuation[0] = Attitude.Actuate(targetTorque[0], torque.x, actuation[0]);
            actuation[1] = Attitude.Actuate(targetTorque[1], torque.y, actuation[1]);
            actuation[2] = Attitude.Actuate(targetTorque[2], torque.z, actuation[2]);

            Phi = phi; TargetOmega = targetOmega; Omega = omega;
            TargetTorque = targetTorque; Actuation = actuation; Torque = torque; Moi = moi;

            // ---- WRITE THE AXES ----
            s.pitch = Clamp((float)actuation[0]);
            s.roll = Clamp((float)actuation[1]);
            s.yaw = Clamp((float)actuation[2]);
        }

        private static float Clamp(float f)
        {
            if (float.IsNaN(f)) return 0f;
            return Mathf.Clamp(f, -1f, 1f);
        }

        /// ---- ⛔ THIS USED TO COUNT REACTION WHEELS ONLY. THAT IS NOT "CONSERVATIVE". ----
        /// ---- ASK EVERY TORQUE PROVIDER, WHICH IS WHAT MECHJEB DOES ----
        private Vector3d AvailableTorque(Vessel v)
        {
            Vector3d t = Vector3d.zero;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        PartModule pm = p.Modules[m];
                        if (!pm.isEnabled) continue;

                        // ---- ⛔ RCS IS MEASURED FROM ITS NOZZLES, NOT ASKED FOR. ----
                        ModuleRCS rcs = pm as ModuleRCS;
                        if (rcs != null) { AddRcsTorque(v, rcs, ref t); continue; }

                        ITorqueProvider tp = pm as ITorqueProvider;
                        if (tp == null) continue;

                        Vector3 pos, neg;
                        tp.GetPotentialTorque(out pos, out neg);
                        t.x += Math.Max(Math.Abs(pos.x), Math.Abs(neg.x));
                        t.y += Math.Max(Math.Abs(pos.y), Math.Abs(neg.y));
                        t.z += Math.Max(Math.Abs(pos.z), Math.Abs(neg.z));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "a torque provider threw, using the partial sum: " + e.Message);
            }

            if (t.x < 0.1) t.x = 0.1;
            if (t.y < 0.1) t.y = 0.1;
            if (t.z < 0.1) t.z = 0.1;
            return t;
        }

        private void EnsureRcsAuthority(Vessel v)
        {
            if (v == null) return;
            if (HasWheelAuthority(v)) return;
            if (Powered(v)) return;
            if (!v.ActionGroups[KSPActionGroup.RCS])
                v.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
        }

        private static bool HasWheelAuthority(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleReactionWheel> ws = v.parts[i].Modules.GetModules<ModuleReactionWheel>();
                for (int m = 0; m < ws.Count; m++)
                {
                    ModuleReactionWheel w = ws[m];
                    if (w.wheelState != ModuleReactionWheel.WheelState.Active) continue;
                    t += (w.PitchTorque + w.YawTorque + w.RollTorque) * (w.authorityLimiter / 100f);
                    if (t >= 1.0) return true;
                }
            }
            return false;
        }

        private static bool Powered(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleEngines> es = v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (es[m].EngineIgnited && !es[m].flameout && es[m].finalThrust > 1.0) return true;
            }
            return false;
        }

        private void AddRcsTorque(Vessel v, ModuleRCS rcs, ref Vector3d t)
        {
            if (!rcs.rcsEnabled || !rcs.isEnabled || rcs.isJustForShow || rcs.flameout) return;
            if (!v.ActionGroups[KSPActionGroup.RCS]) return;
            if (rcs.part.ShieldedFromAirstream) return;
            if (rcs.thrusterTransforms == null) return;

            Vector3d com = v.CoM;
            Transform vt = v.GetTransform();
            double power = rcs.thrusterPower * rcs.thrustPercentage * 0.01;
            if (power <= 0.0) return;

            for (int i = 0; i < rcs.thrusterTransforms.Count; i++)
            {
                Transform tr = rcs.thrusterTransforms[i];
                if (tr == null || !tr.gameObject.activeInHierarchy) continue;

                Vector3d pos = (Vector3d)tr.position - com;
                Vector3d dir = rcs.useZaxis ? (Vector3d)(-tr.forward) : (Vector3d)(-tr.up);
                Vector3d torque = Vector3d.Cross(pos, dir * power);
                Vector3d local = vt.InverseTransformDirection(torque);

                if (rcs.enablePitch) t.x += Math.Abs(local.x);
                if (rcs.enableRoll) t.y += Math.Abs(local.y);
                if (rcs.enableYaw) t.z += Math.Abs(local.z);
            }
        }
    }
}
