// DragonScreen - DirectApproachOps
// ---- WHAT THIS FILE ACTUALLY DOES, IN ONE LINE ----
// ---- ⚠ WE BURN ON THE PODS, NOT ON A SECOND STAGE ----
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

        public static double RangeM, ClosingMps, DvMps, WantMps, AimErrorDeg, ThrottleCmd;

        private static Vessel ship, station;
        private static double startedAt, phaseStartedAt;

        public static bool Complete { get { return Phase == DirectPhase.Done; } }

        public static bool Engage(Vessel v, Vessel target)
        {
            if (v == null || target == null) return false;

            double range = Vector3d.Distance(v.CoM, target.CoM);
            if (!DirectApproach.InsideGate(range))
            {
                Note = "REFUSED - " + (range / 1000.0).ToString("F1") + " km is beyond the "
                     + (DirectApproach.GateM / 1000.0).ToString("F0")
                     + " km direct-approach gate; pursuit at that range is what de-orbited flight 012";
                Debug.LogWarning(Tag + Note);
                Phase = DirectPhase.Refused;
                return false;
            }

            // ---- ⛔ AND IT NEVER FLIES OUTWARD TO REACH ITS OWN GOAL. ----
            if (range <= DirectApproach.GoalM)
            {
                Note = "REFUSED - already inside the " + DirectApproach.GoalM.ToString("F0")
                     + " m handover point at " + range.ToString("F0")
                     + " m; the approach does not fly outward";
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

            // ---- ⛔ ONE DEFINITION OF CLOSING, IN RelativeMotion. THIS FILE HAD ITS OWN. ----
            RelState rel = RelativeMotion.Of(ship, station);
            if (!rel.Valid) { Disengage("coincident"); return; }
            RangeM = rel.RangeM;
            Vector3d los = rel.Los;
            ClosingMps = rel.ClosingMps;

            // ---- ⛔ KEEP-OUT BACKSTOP (see pure/DirectApproach.cs) ----
            if (RangeM < DirectApproach.HardAbortM)
            {
                Note = "KEEP-OUT ABORT - " + RangeM.ToString("F0")
                     + " m, too close to brake safely; releasing to the crew";
                Debug.LogWarning(Tag + Note);
                Phase = DirectPhase.Refused;
                Disengage("keep-out abort");
                return;
            }
            if (RangeM < DirectApproach.KeepOutFloorM && Phase != DirectPhase.Matching)
                Go(DirectPhase.Matching);

            // ---- THE ONE COMMANDED CORRECTION. Trap 1: never split aim from speed. ----
            WantMps = DirectApproach.WantSpeedMps(RangeM);
            Vector3d dv = RelativeMotion.Correction(rel, WantMps);
            DvMps = dv.magnitude;

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

        private static void Close()
        {
            if (RangeM <= DirectApproach.GoalM) { Go(DirectPhase.Matching); return; }

            if (DirectApproach.Burn(DvMps, AimErrorDeg, ClosingMps, RangeM,
                                    Phase == DirectPhase.Accelerating))
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

        private static void Match()
        {
            RelState rel = RelativeMotion.Of(ship, station);
            Vector3d relVel = rel.Relative;
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
            return RelativeMotion.Of(ship, station).Relative.magnitude;
        }
    }
}
