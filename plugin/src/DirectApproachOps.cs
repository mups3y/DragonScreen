/*
 * DragonScreen - DirectApproachOps
 *
 * GLUE. Flies the short-range rendezvous. Law and every constant in `pure/DirectApproach.cs`, and its
 * four traps are documented there - read them before touching this. Ported from
 * `F9I/station_ops.ks:1365 StDirectApproach`.
 *
 * ---- WHAT THIS FILE ACTUALLY DOES, IN ONE LINE ----
 * Solve `dv = wantSpeed × toTarget − relativeVelocity` **every tick**, point at it, and burn along it
 * whenever it is worth burning. That is the whole approach. There is no coast phase, no braking
 * phase, and no second law for the last few hundred metres - adding any of those back is what every
 * failed version of this did.
 *
 * ---- ⚠ WE BURN ON THE PODS, NOT ON A SECOND STAGE ----
 * F9I's profile runs both burns on the S2 engine and keeps monopropellant flat throughout: *"RCS
 * translation is never used to close - the S2's liquid fuel is discarded at jettison, the
 * monopropellant is the de-orbit and the landing."* We separate the S2 before circularising, so by
 * the time this runs there is no MVac to light and the SuperDracos are all there is. That makes the
 * propellant argument POINT THE OTHER WAY: every metre per second here comes out of the return
 * budget, which is why the speed cap and the tolerance below are left exactly as flown rather than
 * loosened for a faster arrival.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class DirectApproachOps
    {
        private const string Tag = "[DragonScreen] ";

        public static bool Engaged { get; private set; }
        public static DirectPhase Phase { get; private set; }
        public static string Note = "-";

        /// <summary>For the pages and the recorder.</summary>
        public static double RangeM, ClosingMps, DvMps, WantMps, AimErrorDeg, ThrottleCmd;

        private static Vessel ship, station;
        private static double startedAt, phaseStartedAt;

        /// <summary>Arrived at the handover point, matched. The caller hands to the docking.</summary>
        public static bool Complete { get { return Phase == DirectPhase.Done; } }

        public static bool Engage(Vessel v, Vessel target)
        {
            if (v == null || target == null) return false;

            double range = Vector3d.Distance(v.CoM, target.CoM);
            if (!DirectApproach.InsideGate(range))
            {
                // ⛔ THE GATE. See pure/DirectApproach.cs - pursuit beyond it de-orbited flight 012.
                Note = "REFUSED - " + (range / 1000.0).ToString("F1") + " km is beyond the "
                     + (DirectApproach.GateM / 1000.0).ToString("F0")
                     + " km direct-approach gate; pursuit at that range is what de-orbited flight 012";
                Debug.LogWarning(Tag + Note);
                Phase = DirectPhase.Refused;
                return false;
            }

            ship = v; station = target;
            Engaged = true;
            startedAt = Planetarium.GetUniversalTime();
            Go(DirectPhase.Vectoring);
            if (!v.ActionGroups[KSPActionGroup.RCS]) v.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
            Debug.Log(Tag + "direct approach engaged at " + range.ToString("F0") + " m - closing to "
                      + DirectApproach.GoalM.ToString("F0") + " m at "
                      + DirectApproach.MatchVelMps.ToString("F1") + " m/s");
            return true;
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
            AttitudeController.Ascent.Throttle = 0.0;
            ThrottleCmd = 0.0;
            Debug.Log(Tag + "direct approach ended - " + why + " ("
                      + RangeM.ToString("F0") + " m, " + RelSpeed().ToString("F2") + " m/s)");
            ship = null; station = null;
        }

        public static void Reset()
        {
            Engaged = false; Phase = DirectPhase.Idle; Note = "-";
            ship = null; station = null;
            RangeM = 0.0; ClosingMps = 0.0; DvMps = 0.0; WantMps = 0.0;
            AimErrorDeg = 0.0; ThrottleCmd = 0.0;
        }

        private static void Go(DirectPhase p)
        {
            Phase = p;
            phaseStartedAt = Planetarium.GetUniversalTime();
        }

        // ------------------------------------------------------------------ the loop

        public static void Tick()
        {
            if (!Engaged) return;
            if (ship == null || ship.state == Vessel.State.DEAD
                || station == null || station.state == Vessel.State.DEAD)
            { Disengage("vessel lost"); return; }

            double now = Planetarium.GetUniversalTime();

            Vector3d toTarget = station.CoM - ship.CoM;
            RangeM = toTarget.magnitude;
            if (RangeM < 1e-3) { Disengage("coincident"); return; }
            Vector3d los = toTarget / RangeM;

            // ⚠ POSITIVE = CLOSING. Proven from flight 035: written the other way round it read
            // −10.0179 m/s while genuinely closing, and every gate built on it had the wrong sign.
            Vector3d relVel = ship.obt_velocity - station.obt_velocity;
            ClosingMps = Vector3d.Dot(-relVel, los);

            // ---- THE ONE COMMANDED CORRECTION. Trap 1: never split aim from speed. ----
            WantMps = DirectApproach.WantSpeedMps(RangeM);
            Vector3d dv = (los * WantMps) - (-relVel);
            DvMps = dv.magnitude;

            // Trap 2: re-solved and re-pointed EVERY tick. A pursuit vector in orbit is valid only at
            // the instant it is computed; flight 062 coasted 720 m on a stale one and stopped dead.
            Vector3d aim = (DvMps > 1e-4) ? dv.normalized : los;
            Vector3d up = (ship.CoM - ship.mainBody.position).normalized;
            AttitudeController.Ascent.SteerTo(ship, aim, up);
            AimErrorDeg = Vector3d.Angle(ship.ReferenceTransform.up, aim);

            if (now - startedAt > DirectApproach.CloseTimeoutS)
            {
                Phase = DirectPhase.Refused;
                Note = "TIMED OUT at " + RangeM.ToString("F0") + " m";
                Disengage("timed out");
                return;
            }

            switch (Phase)
            {
                case DirectPhase.Vectoring: Vectoring(now); break;
                case DirectPhase.Accelerating:
                case DirectPhase.Closing: Close(); break;
                case DirectPhase.Matching: Match(); break;
            }
        }

        /// <summary>
        /// Swing onto the correction before opening the throttle.
        ///
        /// ⚠ TIME-CAPPED. A stack that will not reach 1.5° must still be flown rather than sitting
        /// here until the station is somewhere else - the same argument F9I's flip makes for its
        /// 8-second roll ceiling.
        /// </summary>
        private static void Vectoring(double now)
        {
            AttitudeController.Ascent.Throttle = 0.0;
            ThrottleCmd = 0.0;
            Note = "VECTORING - " + AimErrorDeg.ToString("F1") + " deg, corr "
                 + DvMps.ToString("F2") + " m/s";

            if (AimErrorDeg < DirectApproach.AimAlignDeg
                || now - phaseStartedAt > DirectApproach.AccelMaxS)
                Go(DirectPhase.Accelerating);
        }

        /// <summary>
        /// The whole approach, from wherever we are to the handover point.
        ///
        /// ⛔ ONE LAW ALL THE WAY IN. Traps 3 and 4: the commanded speed tapers to the handover speed
        /// as the range reaches the goal, so this brakes itself. It used to hand over to a
        /// flip-and-brake at a computed stopping distance, and that is what put a capsule into the
        /// station - by then there was no coast left to swing the nose round in, the braking gate
        /// never opened, and the range kept closing with nothing slowing it.
        /// </summary>
        private static void Close()
        {
            if (RangeM <= DirectApproach.GoalM) { Go(DirectPhase.Matching); return; }

            if (DirectApproach.Burn(DvMps, AimErrorDeg, ClosingMps, RangeM))
            {
                ThrottleCmd = DirectApproach.Throttle(DvMps, ship.GetTotalMass(),
                                                      PodEngines.ThrustKn(ship));
            }
            else ThrottleCmd = 0.0;
            AttitudeController.Ascent.Throttle = ThrottleCmd;

            if (Phase == DirectPhase.Accelerating && ClosingMps >= WantMps * 0.9)
                Go(DirectPhase.Closing);

            Note = "CLOSING - " + RangeM.ToString("F0") + " m at " + ClosingMps.ToString("F2")
                 + " / " + WantMps.ToString("F1") + " m/s, corr " + DvMps.ToString("F2");
        }

        /// <summary>
        /// At the goal: kill what relative velocity is left, pointing retrograde to it.
        ///
        /// ⚠ RCS ON BEFORE THE LOOP, not inside it. F9I's coast turned RCS off to hold the
        /// monopropellant flat and nothing turned it back on, so the braking gate - nose within 10°
        /// of retrograde - never opened and the station arrived. "A toggle per tick is its own bug."
        /// </summary>
        private static void Match()
        {
            Vector3d relVel = ship.obt_velocity - station.obt_velocity;
            double speed = relVel.magnitude;

            if (speed <= DirectApproach.MatchVelMps)
            {
                AttitudeController.Ascent.Throttle = 0.0;
                ThrottleCmd = 0.0;
                Phase = DirectPhase.Done;
                Note = "MATCHED - " + RangeM.ToString("F0") + " m at " + speed.ToString("F2") + " m/s";
                Debug.Log(Tag + "direct approach complete - " + Note
                          + ". Handing to the docking.");
                Engaged = false;
                return;
            }

            if (!ship.ActionGroups[KSPActionGroup.RCS])
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

            Vector3d retro = -relVel.normalized;
            Vector3d up = (ship.CoM - ship.mainBody.position).normalized;
            AttitudeController.Ascent.SteerTo(ship, retro, up);
            double off = Vector3d.Angle(ship.ReferenceTransform.up, retro);
            AimErrorDeg = off;

            ThrottleCmd = (off < DirectApproach.BrakeAlignDeg)
                        ? DirectApproach.Throttle(speed, ship.GetTotalMass(),
                                                  PodEngines.ThrustKn(ship))
                        : 0.0;
            AttitudeController.Ascent.Throttle = ThrottleCmd;
            Note = "MATCHING - " + speed.ToString("F2") + " m/s, " + off.ToString("F0") + " deg off";
        }

        private static double RelSpeed()
        {
            if (ship == null || station == null) return 0.0;
            return (ship.obt_velocity - station.obt_velocity).magnitude;
        }
    }
}
