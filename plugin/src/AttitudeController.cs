/*
 * DragonScreen - AttitudeController
 *
 * GLUE. Points a vehicle and sets its throttle. Replaces stock SAS with kOS's steering manager,
 * which is what F9I has always flown. Control law in `pure/AttitudePid.cs`; this file is the vector
 * maths and the FlightCtrlState write.
 *
 * Ported from `Desktop/mechjeb_src/MechJeb2/AttitudeControllers/KosAttitudeController.cs`.
 *
 * ---- ⛔ ONE INSTANCE PER VEHICLE. THIS USED TO BE A STATIC CLASS AND THAT WAS THE CEILING. ----
 * A single static controller can fly exactly one vessel, so a booster recovery could only ever be
 * bought by abandoning the upper stage - and since our upper stage separates on a suborbital arc,
 * that meant the recovery had to wait until insertion was finished, by which time boostback was
 * three minutes gone.
 *
 * F9I has no such limit and never did, because it runs TWO kOS CPUs: BOOSTER.ks flies the booster
 * while F9_payload.ks flies the upper stage, and `FalconFocusBooster` says exactly what that buys -
 * "Focus -> Booster for landing. The upper stage circularizes on its own." KSP simulates every
 * LOADED vessel, not just the focused one, so both fly at once as long as the physics range holds
 * them (~300 km here; F9I measured 296.8-341.1 km on four flights).
 *
 * So this is now an ordinary class with two named instances. `Ascent` flies the upper stage,
 * `Booster` flies the first stage, and neither cares which one the camera is looking at. That is
 * the whole reason the real Falcon 9 profile is now reachable.
 *
 * ---- WHY THROTTLE LIVES HERE TOO ----
 * `FlightInputHandler.state.mainThrottle` is the ACTIVE vessel's throttle. Writing it while flying
 * two vehicles would put the booster's landing-burn throttle on whichever one had focus. The only
 * per-vessel write point is the FlightCtrlState handed to that vessel's own OnFlyByWire callback,
 * which is exactly where MechJeb puts it (`MechJebModuleThrustController.cs:437  s.mainThrottle =
 * TargetThrottle`). Its line 282 also mirrors to FlightInputHandler, with the comment "so that the
 * on-screen throttle gauge reflects the autopilot throttle" - cosmetic, active vessel only, and
 * that is the only thing it is good for.
 *
 * ---- THE FRAME CORRECTION IS NOT OPTIONAL ----
 *      _vesselRotation = ReferenceTransform.rotation * Euler(-90, 0, 0)
 * A KSP command part's transform has +Y out of the nose, but the controller works in a
 * forward/top/starboard frame where FORWARD is +Z. Skip the -90 and every axis is permuted - the
 * same class of error as the navball's transposed texture, and just as invisible until it flies.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    public class AttitudeController
    {
        private const string Tag = "[DragonScreen] ";

        // ---- THE TWO VEHICLES WE EVER FLY AT ONCE ----
        // Named rather than pooled: there are exactly two, they have different jobs, and a name in
        // a log line beats an index.
        public static readonly AttitudeController Ascent = new AttitudeController("ascent");
        public static readonly AttitudeController Booster = new AttitudeController("booster");

        /// <summary>Whichever controller is flying this vessel, or null. For the recorder.</summary>
        public static AttitudeController For(Vessel v)
        {
            if (v == null) return null;
            if (Ascent.attached == v) return Ascent;
            if (Booster.attached == v) return Booster;
            return null;
        }

        private readonly string who;
        public AttitudeController(string name) { who = name; }

        /// <summary>Where we want to point. Inactive means "not steering" - hands back to the player.</summary>
        private Vector3d targetForward, targetTop;
        private bool active;
        private Vessel attached;

        private readonly TorquePi pitchPi = new TorquePi();
        private readonly TorquePi yawPi = new TorquePi();
        private readonly TorquePi rollPi = new TorquePi();

        // Rate loops: Kp 1, Ki 0.1, Kd 0, extraUnwind ON - KosAttitudeController.cs:24-26.
        private readonly KosPid pitchRate = new KosPid(1.0, 0.1, 0.0, true);
        private readonly KosPid yawRate = new KosPid(1.0, 0.1, 0.0, true);
        private readonly KosPid rollRate = new KosPid(1.0, 0.1, 0.0, true);

        private Vector3d actuation = Vector3d.zero;

        /// <summary>Seconds allowed to arrest a rotation. F9I retunes this per phase.</summary>
        public double MaxStoppingTime = AttitudeCascade.DefaultMaxStoppingTime;

        /// <summary>
        /// Total attitude error beyond which the roll axis is left alone, degrees. PER PHASE.
        ///
        /// ---- ⛔ THIS WAS A CONSTANT 5 AND F9I SETS IT TO 45 FOR THE FLIP. ----
        /// 5 is kOS's DEFAULT, not F9I's value. `BOOSTER.ks:157`, inside the live `Flip1`:
        ///
        ///     set steeringmanager:rollcontrolanglerange to 45.
        ///
        /// and `Boostback:207` calls `resettodefault()`, so the 45 is scoped to the flip alone -
        /// which is exactly the phase the crew described as messy.
        ///
        /// What 5 did to the flip, measured 2026-08-12: the flip tracks with about 7.8 degrees of
        /// error, which is just outside 5. So roll control was commanded live while the nose had not
        /// yet moved, spun the stage up to 43 deg/s chasing the new reference, and then switched OFF
        /// the instant the slew began - abandoning that rate with only the rate damper to catch it.
        /// 320 degrees of roll through the flip, and 1330 degrees over the whole recovery.
        ///
        /// At 45 the error never leaves the band, so the roll is FLOWN to its reference instead of
        /// being started and dropped, and the integrator is never reset mid-manoeuvre.
        ///
        /// ⚠ NOT APPLIED OUTSIDE THE FLIP. F9I runs the coast, entry and descent at the default,
        /// so widening it there would be my choice rather than a port. The descent has its own roll
        /// problem - a limit cycle, `omegaR` swinging +/-30 deg/s - and it is a separate question.
        /// </summary>
        public double RollControlRangeDeg = AttitudeCascade.RollControlRangeDeg;

        /// <summary>
        /// Assumed-available roll torque is multiplied by this. `rolltorquefactor`, per phase.
        ///
        /// ---- ⛔ THE OTHER HALF OF THE FLIP ROLL PORT, AND WITHOUT IT THE FIRST HALF REGRESSED. ----
        /// `Flip1` sets TWO coupled things - `rollcontrolanglerange to 45` AND
        /// `rolltorquefactor to 3` (`BOOSTER.ks:156-157`). I ported the range alone, which turned
        /// roll control ON for the whole flip while leaving it as aggressive as pitch and yaw. It
        /// got worse, measured: flip roll 320 -> 743 degrees, actuation saturated 52.5% -> 78.7%,
        /// total 1330 -> 1682 degrees over the recovery.
        ///
        /// Our cascade computes `actuation = targetTorque / availableTorque`, so scaling the roll
        /// axis's available torque by 3 divides its commanded actuation by 3 - which is exactly the
        /// authority reduction a saturated, oscillating axis needs.
        ///
        /// ⚠ THE ONE THING HERE I COULD NOT READ FROM SOURCE. kOS ships as a DLL and F9I only
        /// sets the value, so the direction of the effect is inferred from our own cascade's algebra
        /// rather than confirmed. If the next flight shows saturation RISING, the sense is inverted
        /// and this becomes a divide. `b_actR` and the roll travel in `b_omegaRdps` decide it.
        /// </summary>
        public double RollTorqueFactor = 1.0;

        /// <summary>
        /// Pitch and yaw stopping time multiplier. `pitchts`/`yawts`, per phase.
        ///
        /// ---- ⛔ F9I SLOWS PITCH AND YAW FOR THE FLIP AND LEAVES ROLL ALONE. WE HAD ONE KNOB. ----
        /// `BOOSTER.ks:152-153`, inside the live `Flip1`:
        ///
        ///     set steeringmanager:pitchts to (steeringmanager:pitchts * 1.5).
        ///     set steeringmanager:yawts   to (steeringmanager:yawts   * 1.5).
        ///
        /// kOS carries a separate time-scale per axis; we collapsed all three into `MaxStoppingTime`,
        /// so this multiplier had nowhere to live and every axis flew the same aggression. Measured
        /// on 2026-08-12: through the flip, pitch actuation saturated 45.5% of the time and yaw
        /// 46.3%, with only 5.3 degrees of average error - a controller slamming to full deflection
        /// over five degrees is over-driven, and it is the pitch/yaw axes that then couple into roll.
        ///
        /// A LONGER stopping time means a lower rate limit and a gentler approach, which is exactly
        /// what `* 1.5` asks for. Roll is deliberately not scaled here, because Flip1 does not scale
        /// it either - it uses `rolltorquefactor` instead.
        /// </summary>
        public double PitchYawStoppingScale = 1.0;

        /// <summary>
        /// Roll stopping-time multiplier. `rollts`, per phase.
        ///
        /// ---- ⛔ F9I SLOWS THE ROLL AXIS TENFOLD FOR THE DESCENT. WE HAD NOTHING. ----
        /// `AtmGNC:434`, on the live path, set immediately before it hands over to
        /// `LandingZoneGuidance`:
        ///
        ///     set steeringmanager:rollts to 10.
        ///
        /// I previously said I had no citation for a descent roll setting and left it. It was in
        /// AtmGNC all along; I had been reading `Reentry1`, which has no callers.
        ///
        /// What it is worth, measured against F9I's own black box over eight booster flights
        /// (bb_booster_001..008, the vehicle that lands 0.34-0.56 m from the pad), across the
        /// identical coast window:
        ///
        ///                       roll travel      peak roll rate
        ///     F9I                 103 deg            3.1 deg/s
        ///     ours (08-12)        240 deg           24.3 deg/s
        ///
        /// A longer stopping time lowers the rate limit the roll axis is allowed to command, which
        /// is exactly the difference between those two rows. Applied from grid-fin deploy onward,
        /// which is where AtmGNC sets it.
        /// </summary>
        public double RollStoppingScale = 1.0;

        /// <summary>
        /// Throttle for THIS vehicle, 0-1. Written into its own FlightCtrlState every tick while
        /// attached - including while zero, so a released vehicle actually stops rather than keeping
        /// whatever it had.
        /// </summary>
        public double Throttle;

        /// <summary>
        /// Fore/aft RCS translation for THIS vehicle, 0..1. Positive settles propellant forward.
        ///
        /// Here rather than on the vessel because `v.ctrlState.Z` written from Update is rebuilt by
        /// KSP every FixedUpdate before physics sees it - the ullage command was almost certainly a
        /// no-op for its whole life. MechJeb writes Z into the FlightCtrlState it is handed
        /// (MechJebModuleNodeExecutor.cs:161,193), which is this one. NEGATIVE Z is forward.
        /// </summary>
        public double UllageFore;

        /// <summary>
        /// Lateral RCS translation for THIS vehicle, -1..1. X is starboard, Y is up-in-the-cockpit.
        ///
        /// Docking needs these: the terminal ladder computes a lateral drift and without a way to
        /// push sideways the only response is to YAW, which turns the capsule off the port axis it
        /// has to arrive on. Flight 035 "missed the port and bounced off the hull" and spent 21.95
        /// units of monopropellant on the docking alone - more than the whole approach that
        /// delivered it there.
        /// </summary>
        public double TranslateX, TranslateY;

        /// <summary>Last attitude error, degrees. For the pages and the logs.</summary>
        public double ErrorDeg { get; private set; }

        /// <summary>The vessel this controller is flying, or null.</summary>
        public Vessel Vehicle { get { return attached; } }

        // ---- INTERNALS EXPOSED FOR THE RECORDER ----
        // Not decoration: without the COMMAND beside the RESPONSE you can see the vehicle was 12
        // degrees off and still not know whether guidance asked for the wrong thing or the
        // controller failed to deliver it. F9I hit this and solved it with its x1..x4 scratch
        // columns - "they record what the guidance ASKED for, which no KSP telemetry exposes".
        public Vector3d Phi, TargetOmega, Omega, TargetTorque, Actuation, Torque, Moi;
        public bool Steering { get { return active; } }

        // ------------------------------------------------------------------ public

        /// <summary>
        /// Steer at a direction, with an optional roll reference. Pass `up` as zero to let the
        /// controller pick one - but prefer giving it one: an uncommanded roll is what SAS did.
        /// </summary>
        public void SteerTo(Vessel v, Vector3d forward, Vector3d up)
        {
            if (v == null || forward.sqrMagnitude < 1e-6) { Release(v); return; }

            Attach(v);
            targetForward = forward.normalized;

            // A roll reference must not be parallel to the direction, or the frame is degenerate.
            //
            // ---- ⛔ THIS FALLBACK SPUN THE ROCKET ON THE PAD AT 64 DEG/S. ----
            // The comment said "bails to the current top vector" and the code used
            // `ReferenceTransform.forward`, which is MINUS that: the controller's own top is
            // `rotation * Euler(-90,0,0) * up`, and the -90 about X maps controller-up onto
            // reference -Z. So the fallback asked for a top vector exactly 180 degrees from the one
            // the vehicle had.
            //
            // 180 degrees is the bistable point of the roll error. In the 21:01 recording phiRoll
            // sat pinned at +-pi from the first tick and flipped sign row to row, so the controller
            // commanded full roll one way, wound the stack up to -1.12 rad/s (-64 deg/s), watched
            // the sign flip, and drove it back the other way - a limit cycle that ran through the
            // whole vertical rise and into the gravity turn. attErrDeg looked FINE throughout,
            // 0.08-0.17 deg, because the nose was tracking perfectly; only the roll axis was insane.
            //
            // The current top is the right answer and the one the comment always claimed: it makes
            // the roll error ZERO, which is "hold whatever roll you have" - harmless, and exactly
            // what BOOSTER.ks means by "a moment of un-held roll is harmless, a snap is not".
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

        /// <summary>
        /// Stop steering and give the vehicle back.
        ///
        /// ⛔ MaxStoppingTime IS RESET HERE. BoosterRecovery drives it to 0.05 for the landing burn,
        /// which is right for the last few hundred metres and badly wrong for anything else; when
        /// this was one shared static, the upper stage inherited it and slewed at a crawl. It is
        /// per-instance now, but resetting on release is still correct - a controller handed a new
        /// vehicle must not remember the last one's tuning.
        /// </summary>
        public void Release(Vessel v)
        {
            active = false;
            Throttle = 0.0;
            UllageFore = 0.0;
            TranslateX = 0.0; TranslateY = 0.0;
            // Write the zero throttle out before letting go, or the vehicle keeps the last one.
            if (attached != null && attached.ctrlState != null) attached.ctrlState.mainThrottle = 0f;
            Detach();
            actuation = Vector3d.zero;
            MaxStoppingTime = AttitudeCascade.DefaultMaxStoppingTime;
            RollControlRangeDeg = AttitudeCascade.RollControlRangeDeg;
            RollTorqueFactor = 1.0;
            PitchYawStoppingScale = 1.0;
            RollStoppingScale = 1.0;
            pitchPi.ResetI(); yawPi.ResetI(); rollPi.ResetI();
            pitchRate.ResetI(); yawRate.ResetI(); rollRate.ResetI();
        }

        private void Attach(Vessel v)
        {
            if (attached == v) return;
            Detach();
            v.OnFlyByWire += Drive;
            attached = v;
            // SAS OFF. It and this controller both write the same axes, and two controllers on one
            // set of axes is worse than either alone. MechJebModuleAttitudeController.cs:401 does
            // exactly this for the same reason.
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

        /// <summary>
        /// ⛔ AN EXCEPTION HERE IS INVISIBLE AND INFINITE. KSP calls this from FeedInputFeed and
        /// swallows whatever it throws, so a fault does not stop the flight - it just means the
        /// vehicle is never steered, once per physics tick, forever. That is exactly what happened,
        /// and from inside the cockpit it looked like the autopilot doing nothing.
        ///
        /// So: catch, DETACH, and say so once. A controller that has failed must stop pretending to
        /// fly the vehicle, and the log must say why the first time rather than the ten-thousandth.
        /// </summary>
        private void Drive(FlightCtrlState s)
        {
            try
            {
                // Throttle goes out whether or not we are steering: a vehicle told to coast must
                // actually be at zero, and this is the only per-vessel place to say so.
                s.mainThrottle = Mathf.Clamp01((float)Throttle);
                // NEGATIVE Z is forward - verified at three MechJeb sites, see AutoPilot's note.
                if (UllageFore > 0.001 || UllageFore < -0.001)
                    s.Z = -Mathf.Clamp((float)UllageFore, -1f, 1f);
                // ---- ⛔ NEGATED, AND THE NEGATION IS MEASURED. ----
                // Every axis here needs exactly one sign flip between our body convention and
                // KSP's FlightCtrlState, and starboard was the only one with none: fore has it in
                // the write below, top carries it in `DistT = Dot(to, -rt.forward)`, starboard had
                // it nowhere. So commanding starboard moved the capsule to PORT.
                //
                // Measured on 2026-08-12 with the docking controller's own inputs, recorded for the
                // first time. Over 965 rows of docking, per axis, "did the commanded translation
                // shrink its own offset?":
                //
                //     FORE      shrank 766, grew 160   OK
                //     STARBOARD shrank 189, grew 739   INVERTED
                //     TOP       shrank 528, grew 382   OK
                //
                // and the trace is unambiguous - DistS 29.9 -> 86.7 -> 181.6 m while transX held
                // +0.25 then +0.50. The docking has never once worked, on any flight, because of
                // this line.
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
            // The roll axis may be told it has more torque than it does - see RollTorqueFactor.
            Vector3d rollScaled = new Vector3d(torque.x, torque.y * RollTorqueFactor, torque.z);

            // Per-axis time scale: pitch (x) and yaw (z) may be slowed together, roll (y) is not.
            double tsPY = MaxStoppingTime * PitchYawStoppingScale;
            double tsR  = MaxStoppingTime * RollStoppingScale;
            Vector3d maxOmega = new Vector3d(
                AttitudeCascade.MaxOmega(rollScaled.x, moi.x, tsPY),
                AttitudeCascade.MaxOmega(rollScaled.y, moi.y, tsR),
                AttitudeCascade.MaxOmega(rollScaled.z, moi.z, tsPY));

            Vector3d targetOmega = Vector3d.zero;
            targetOmega[0] = pitchRate.Update(-phi[0], 0.0, maxOmega[0], dt);
            targetOmega[1] = rollRate.Update(-phi[1], 0.0, maxOmega[1], dt);
            targetOmega[2] = yawRate.Update(-phi[2], 0.0, maxOmega[2], dt);

            // ---- DO NOT FIGHT FOR ROLL WHILE STILL SLEWING ----
            // Outside 5 degrees of total error the roll axis is commanded to zero and its integral
            // is reset. Rolling mid-slew wastes authority and couples the axes; get the nose there,
            // then worry about which way up.
            if (Math.Abs(phiTotal) > RollControlRangeDeg * Mathf.Deg2Rad)
            {
                targetOmega[1] = 0.0;
                rollRate.ResetI();
            }

            Vector3d targetTorque = Vector3d.zero;
            targetTorque[0] = pitchPi.Update(omega[0], targetOmega[0], moi[0], torque[0], dt);
            targetTorque[1] = rollPi.Update(omega[1], targetOmega[1], moi[1], rollScaled.y, dt);
            targetTorque[2] = yawPi.Update(omega[2], targetOmega[2], moi[2], torque[2], dt);

            for (int i = 0; i < 3; i++)
                actuation[i] = AttitudeCascade.Actuation(targetTorque[i], rollScaled[i], actuation[i]);

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
        /// figure would undo that.
        ///
        /// ---- ⛔ THIS USED TO COUNT REACTION WHEELS ONLY. THAT IS NOT "CONSERVATIVE". ----
        /// It is wrong by three orders of magnitude on a launch vehicle, and the comment that stood
        /// here claimed RCS was included when the code never looked at it - which is how it survived
        /// being read several times.
        ///
        /// MEASURED, not estimated. On this stack the only wheels are the interstage's 3.5 kN·m and
        /// the Dragon pod's 6, so this returned 9.5. Recorded pitch MoI in the gravity turn
        /// (flight_0810_182024.csv) is 21 949 t·m². The cascade's rate limit is
        ///     maxOmega = torque × MaxStoppingTime / MoI = 9.5 × 2.0 / 21 949 = 0.05 deg/s
        /// against roughly 0.45 deg/s needed to fly the turn. The vehicle would have rolled normally
        /// - roll MoI is only 122 t·m² - and simply refused to pitch over. Nine Merlin gimbals are
        /// worth order 10³ kN·m and were counted as zero. With this fixed the 21:01 flight tracked
        /// its pitch programme to 0.08-0.17 deg and made orbit.
        ///
        /// ---- ASK EVERY TORQUE PROVIDER, WHICH IS WHAT MECHJEB DOES ----
        /// `VesselState.cs:1024-1034` sums wheels + RCS + control surfaces + gimbal + anything else
        /// implementing ITorqueProvider. Grid fins join that set the moment they deploy, and gimbal
        /// authority correctly falls to zero at zero throttle because GetPotentialTorque scales with
        /// current thrust.
        ///
        /// One deliberate simplification against MechJeb: it accumulates the positive and negative
        /// directions separately in a Vector6 and takes the larger at the end, while this takes the
        /// larger per module and sums. The two agree whenever a module's pos and neg are symmetric,
        /// which MechJeb's own comments assert for wheels and gimbals (`VesselState.cs:891, 966`).
        /// </summary>
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
                        // `ModuleRCS.GetPotentialTorque` under-reports badly. Measured on the 08:40
                        // booster during the flip, throttle zero so RCS was the ONLY torque source:
                        // it returned 4.1 kN·m while the stage's own angular acceleration worked out
                        // at about 50. That is a factor of twelve, and `MaxOmega` is linear in it, so
                        // the controller allowed itself a twelfth of the slew rate it actually had -
                        // which is most of why a 180° turnaround took 152 s.
                        //
                        // MechJeb reached the same conclusion: `VesselState.cs:686` has the
                        // `rcs.GetPotentialTorque` call COMMENTED OUT and walks the thruster
                        // transforms instead. This is that walk. Everything else - reaction wheels,
                        // gimbals, control surfaces - reports honestly and goes through the
                        // interface below.
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
                // A third-party ITorqueProvider that throws must not take the controller with it -
                // Drive() would detach and stop steering the vehicle. Keep what was summed.
                Debug.LogWarning(Tag + "a torque provider threw, using the partial sum: " + e.Message);
            }

            // Never zero: the cascade divides by it. A small floor keeps the actuation finite and the
            // controller then simply asks for its maximum, which is the right answer when authority
            // is unknown rather than absent.
            if (t.x < 0.1) t.x = 0.1;
            if (t.y < 0.1) t.y = 0.1;
            if (t.z < 0.1) t.z = 0.1;
            return t;
        }

        /// <summary>
        /// One RCS block's contribution, from where its nozzles are and which way they point.
        /// Ported from `MechJeb2/VesselState.cs:676-740`.
        ///
        /// ⚠ THE AXIS MAPPING IS NOT THE OBVIOUS ONE. KSP's vessel transform has Y forward and Z up,
        /// while this controller works in forward/top/starboard (pitch about X, roll about Y, yaw
        /// about Z). MechJeb builds the enable mask as (pitch, roll, yaw) against a torque it has
        /// already transformed into the vessel frame, and the swap between them is why the mask is
        /// applied to (x, y, z) in that order rather than to the raw cross product.
        ///
        /// ⚠ AND ONLY LIVE NOZZLES COUNT. Since KSP 1.11 an RCS part carries the transforms of every
        /// part VARIANT, so a four-nozzle block can advertise sixteen. kOS culls on
        /// `activeInHierarchy` and MechJeb borrowed the fix; without it this over-reports instead,
        /// which is the same defect the other way round.
        /// </summary>
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
