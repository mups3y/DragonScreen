/*
 * DragonScreen - AttitudeController
 *
 * GLUE. Points the vehicle. Replaces stock SAS with kOS's steering manager, which is what F9I has
 * always flown. Control law in `pure/AttitudePid.cs`; this file is the vector maths and the
 * FlightCtrlState write.
 *
 * Ported from `Desktop/mechjeb_src/MechJeb2/AttitudeControllers/KosAttitudeController.cs`.
 *
 * ---- WHAT CHANGES, AND WHY IT IS NOT A TUNING TWEAK ----
 * `SAS.SetTargetOrientation(dir, false)` takes a DIRECTION. `lookdirup(dir, up)` takes a full
 * ATTITUDE - where the nose points AND where the top faces. Stock SAS therefore leaves roll
 * uncommanded, has no torque feed-forward, and is designed to hold a fixed navball marker rather
 * than track a guidance vector that moves every frame. All three showed up as the wander.
 *
 * ---- THE FRAME CORRECTION IS NOT OPTIONAL ----
 *      _vesselRotation = ReferenceTransform.rotation * Euler(-90, 0, 0)
 * A KSP command part's transform has +Y out of the nose, but the controller works in a
 * forward/top/starboard frame where FORWARD is +Z. Skip the -90 and every axis is permuted - the
 * same class of error as the navball's transposed texture, and just as invisible until it flies.
 *
 * ---- WHY IT WRITES FlightCtrlState DIRECTLY, AND WHAT THAT UNLOCKS ----
 * SAS is switched OFF and the three axes are driven through the OnFlyByWire callback, exactly as
 * MechJeb does. That is also how `ctlPitch/ctlYaw/ctlRoll` finally start recording: those columns
 * are dead in all 554 black-box flights BECAUSE kOS cooked steering and SAS both bypass them. With
 * this controller the corpus starts capturing command/response pairs, and the system-identification
 * pass in FLIGHT_SOFTWARE_PLAN.md stops being blocked.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class AttitudeController
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>Where we want to point. Null means "not steering" - hands back to the player.</summary>
        private static Vector3d targetForward, targetTop;
        private static bool active;
        private static Vessel attached;

        private static readonly TorquePi pitchPi = new TorquePi();
        private static readonly TorquePi yawPi = new TorquePi();
        private static readonly TorquePi rollPi = new TorquePi();

        // Rate loops: Kp 1, Ki 0.1, Kd 0, extraUnwind ON - KosAttitudeController.cs:24-26.
        private static readonly KosPid pitchRate = new KosPid(1.0, 0.1, 0.0, true);
        private static readonly KosPid yawRate = new KosPid(1.0, 0.1, 0.0, true);
        private static readonly KosPid rollRate = new KosPid(1.0, 0.1, 0.0, true);

        private static Vector3d actuation = Vector3d.zero;

        /// <summary>Seconds allowed to arrest a rotation. F9I retunes this per phase.</summary>
        public static double MaxStoppingTime = AttitudeCascade.DefaultMaxStoppingTime;

        /// <summary>Last attitude error, degrees. For the pages and the logs.</summary>
        public static double ErrorDeg { get; private set; }

        // ---- INTERNALS EXPOSED FOR THE RECORDER ----
        // Not decoration: without the COMMAND beside the RESPONSE you can see the vehicle was 12
        // degrees off and still not know whether guidance asked for the wrong thing or the
        // controller failed to deliver it. F9I hit this and solved it with its x1..x4 scratch
        // columns - "they record what the guidance ASKED for, which no KSP telemetry exposes".
        public static Vector3d Phi, TargetOmega, Omega, TargetTorque, Actuation, Torque, Moi;
        public static bool Steering { get { return active; } }

        // ------------------------------------------------------------------ public

        /// <summary>
        /// Steer at a direction, with an optional roll reference. Pass `up` as zero to let the
        /// controller pick one - but prefer giving it one: an uncommanded roll is what SAS did.
        /// </summary>
        public static void SteerTo(Vessel v, Vector3d forward, Vector3d up)
        {
            if (v == null || forward.sqrMagnitude < 1e-6) { Release(v); return; }

            Attach(v);
            targetForward = forward.normalized;

            // A roll reference must not be parallel to the direction, or the frame is degenerate.
            // BOOSTER.ks bails to the current top vector at exactly this point rather than clamping,
            // because "a moment of un-held roll is harmless, a snap is not".
            Vector3d u = up;
            if (u.sqrMagnitude < 1e-6 || Math.Abs(Vector3d.Dot(u.normalized, targetForward)) > 0.999)
                u = v.ReferenceTransform.forward;
            targetTop = Vector3d.Exclude(targetForward, u).normalized;
            active = true;
        }

        /// <summary>Stop steering and give the vehicle back.</summary>
        public static void Release(Vessel v)
        {
            active = false;
            Detach();
            actuation = Vector3d.zero;
            pitchPi.ResetI(); yawPi.ResetI(); rollPi.ResetI();
            pitchRate.ResetI(); yawRate.ResetI(); rollRate.ResetI();
        }

        private static void Attach(Vessel v)
        {
            if (attached == v) return;
            Detach();
            v.OnFlyByWire += Drive;
            attached = v;
            // SAS OFF. It and this controller both write the same axes, and two controllers on one
            // set of axes is worse than either alone. MechJebModuleAttitudeController.cs:401 does
            // exactly this for the same reason.
            v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            Debug.Log(Tag + "attitude controller attached to '" + v.vesselName + "', SAS off");
        }

        private static void Detach()
        {
            if (attached == null) return;
            attached.OnFlyByWire -= Drive;
            attached = null;
        }

        // ------------------------------------------------------------------ the loop

        /// <summary>
        /// ⛔ AN EXCEPTION HERE IS INVISIBLE AND INFINITE. KSP calls this from FeedInputFeed and
        /// swallows whatever it throws, so a fault does not stop the flight - it just means the
        /// vehicle is never steered, once per physics tick, forever. That is exactly what happened,
        /// and from inside the cockpit it looked like the autopilot doing nothing.
        ///
        /// So: catch, DETACH, and say so once. A controller that has failed must stop pretending to
        /// fly the vehicle, and the log must say why the first time rather than the ten-thousandth.
        /// </summary>
        private static void Drive(FlightCtrlState s)
        {
            try { DriveInner(s); }
            catch (Exception e)
            {
                Debug.LogError(Tag + "attitude controller FAILED and has detached - the vehicle is "
                               + "not being steered: " + e);
                active = false;
                Detach();
            }
        }

        private static void DriveInner(FlightCtrlState s)
        {
            Vessel v = attached;
            if (!active || v == null || v.ReferenceTransform == null) return;

            double dt = TimeWarp.fixedDeltaTime;
            if (dt <= 0.0) return;

            // ---- STATE VECTORS ----
            // The -90 puts us in the controller's forward/top/starboard frame. See the header.
            // ---- ⛔ NOT QuaternionD.Euler. IT THROWS MissingMethodException IN THIS KSP. ----
            // MechJeb's line is  - ITS OWN implementation -
            // and I substituted KSP's QuaternionD.Euler assuming they were equivalent. They are not:
            // MechJeb wrote its own BECAUSE QuaternionD.Euler is broken. Internal_FromEulerRad is
            // missing, so every single FixedUpdate threw, KSP swallowed it inside FeedInputFeed, and
            // the controller silently never ran - 119 000 exception lines in four minutes, and a
            // vehicle that flew ballistically while the guidance computed perfectly good commands.
            //
            // The float Quaternion.Euler is Unity's own and works. A fixed -90 about X needs no
            // double precision anyway; ReferenceTransform.rotation is a float quaternion to begin
            // with, so nothing is lost.
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

            // Per-axis error, each measured in the plane that axis actually rotates in.
            Vector3d phi = Vector3d.zero;
            phi[0] = Vector3d.Angle(fwd, Vector3d.Exclude(star, targetForward)) * Mathf.Deg2Rad;
            if (Vector3d.Angle(top, Vector3d.Exclude(star, targetForward)) > 90.0) phi[0] *= -1.0;
            phi[1] = Vector3d.Angle(top, Vector3d.Exclude(fwd, targetTop)) * Mathf.Deg2Rad;
            if (Vector3d.Angle(star, Vector3d.Exclude(fwd, targetTop)) > 90.0) phi[1] *= -1.0;
            phi[2] = Vector3d.Angle(fwd, Vector3d.Exclude(top, targetForward)) * Mathf.Deg2Rad;
            if (Vector3d.Angle(star, Vector3d.Exclude(top, targetForward)) > 90.0) phi[2] *= -1.0;

            Vector3d moi = v.MOI;
            Vector3d torque = AvailableTorque(v);

            // ---- CASCADE: error -> rate -> torque -> actuation ----
            Vector3d maxOmega = new Vector3d(
                AttitudeCascade.MaxOmega(torque.x, moi.x, MaxStoppingTime),
                AttitudeCascade.MaxOmega(torque.y, moi.y, MaxStoppingTime),
                AttitudeCascade.MaxOmega(torque.z, moi.z, MaxStoppingTime));

            Vector3d targetOmega = Vector3d.zero;
            targetOmega[0] = pitchRate.Update(-phi[0], 0.0, maxOmega[0], dt);
            targetOmega[1] = rollRate.Update(-phi[1], 0.0, maxOmega[1], dt);
            targetOmega[2] = yawRate.Update(-phi[2], 0.0, maxOmega[2], dt);

            // ---- DO NOT FIGHT FOR ROLL WHILE STILL SLEWING ----
            // Outside 5 degrees of total error the roll axis is commanded to zero and its integral
            // is reset. Rolling mid-slew wastes authority and couples the axes; get the nose there,
            // then worry about which way up.
            if (Math.Abs(phiTotal) > AttitudeCascade.RollControlRangeDeg * Mathf.Deg2Rad)
            {
                targetOmega[1] = 0.0;
                rollRate.ResetI();
            }

            Vector3d targetTorque = Vector3d.zero;
            targetTorque[0] = pitchPi.Update(omega[0], targetOmega[0], moi[0], torque[0], dt);
            targetTorque[1] = rollPi.Update(omega[1], targetOmega[1], moi[1], torque[1], dt);
            targetTorque[2] = yawPi.Update(omega[2], targetOmega[2], moi[2], torque[2], dt);

            for (int i = 0; i < 3; i++)
                actuation[i] = AttitudeCascade.Actuation(targetTorque[i], torque[i], actuation[i]);

            Phi = phi; TargetOmega = targetOmega; Omega = omega;
            TargetTorque = targetTorque; Actuation = actuation; Torque = torque; Moi = moi;

            // ---- WRITE THE AXES ----
            // KSP's control state is pitch / roll / yaw against the controller's 0 / 1 / 2.
            s.pitch = Clamp((float)actuation[0]);
            s.roll = Clamp((float)actuation[1]);
            s.yaw = Clamp((float)actuation[2]);
        }

        private static float Clamp(float f)
        {
            if (float.IsNaN(f)) return 0f;
            return Mathf.Clamp(f, -1f, 1f);
        }

        /// <summary>
        /// Torque the vehicle can actually produce, per axis.
        ///
        /// Summed from the parts rather than taken from a cached total, because the whole point of
        /// the MoI-scaled gains is that they track the vehicle as it stages - and a stale torque
        /// figure would undo that. Reaction wheels and RCS both count; engine gimbal does not, which
        /// makes the estimate CONSERVATIVE and the controller gentle rather than twitchy.
        /// </summary>
        private static Vector3d AvailableTorque(Vessel v)
        {
            Vector3d t = Vector3d.zero;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleReactionWheel w = p.Modules[m] as ModuleReactionWheel;
                    if (w != null && w.isEnabled && w.wheelState == ModuleReactionWheel.WheelState.Active)
                    {
                        t.x += w.PitchTorque; t.y += w.RollTorque; t.z += w.YawTorque;
                    }
                }
            }
            // Never zero: the cascade divides by it, and a vehicle with no wheels still has RCS and
            // gimbal that this does not count. A small floor keeps the actuation finite and the
            // controller simply asks for its maximum, which is the right answer when authority is
            // unknown rather than absent.
            if (t.x < 0.1) t.x = 0.1;
            if (t.y < 0.1) t.y = 0.1;
            if (t.z < 0.1) t.z = 0.1;
            return t;
        }
    }
}
