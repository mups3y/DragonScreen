// DragonScreen - WaypointApproachOps
// ---- IT INVENTS NO CONTROL LAW. IT POINTS TWO PROVEN PARTS AT A NEW TARGET. ----
// ---- ⚠ RSS ONLY, AND OFF BY DEFAULT (`Enabled`). ----
// ---- THE WP0->WP1 KEEP-OUT GRAZE IS FIXED (fly-around arc), BUT THIS HAS STILL NEVER FLOWN IN RSS. ----
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class WaypointApproachOps
    {
        private const string Tag = "[DragonScreen] ";

        [Tunable] public static bool Enabled = true;

        [Tunable] public static double EnvelopeM = 2500.0;

        public static bool Engaged { get; private set; }
        public static WpPhase Phase { get; private set; }
        public static string Note = "-";

        public static double RangeM, ClosingMps, RadialM, AlongM, CrossM;

        public static bool Complete { get { return Phase == WpPhase.Handover; } }

        private static Vessel ship, station;
        private static readonly Pid pidF = new Pid();
        private static readonly Pid pidS = new Pid();
        private static readonly Pid pidT = new Pid();
        private static double holdStartedAt;

        public static bool Engage(Vessel v, Vessel target)
        {
            if (v == null || target == null) return false;

            double range = Vector3d.Distance(v.CoM, target.CoM);
            if (range > EnvelopeM)
            {
                Note = "REFUSED - " + range.ToString("F0") + " m is beyond the "
                     + EnvelopeM.ToString("F0") + " m L-approach initiation envelope; close in first";
                Phase = WpPhase.Idle;
                return false;
            }

            pidF.Reset(); pidS.Reset(); pidT.Reset();
            ship = v; station = target;
            Engaged = true;
            Phase = WpPhase.ToWP0;
            holdStartedAt = Planetarium.GetUniversalTime();
            if (!v.ActionGroups[KSPActionGroup.RCS]) v.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
            Debug.Log(Tag + "L-approach engaged at " + range.ToString("F0")
                      + " m - flying WP0 (400 m below) -> WP1 (220 m ahead) -> WP2 (20 m), each a hold");
            return true;
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
            StopTranslating();
            AttitudeController.Ascent.Throttle = 0.0;
            AttitudeController.Ascent.Release(ship);
            Debug.Log(Tag + "L-approach ended - " + why + " (" + RangeM.ToString("F0") + " m)");
            ship = null; station = null;
        }

        public static void Reset()
        {
            Engaged = false; Phase = WpPhase.Idle; Note = "-";
            ship = null; station = null;
            RangeM = 0.0; ClosingMps = 0.0; RadialM = 0.0; AlongM = 0.0; CrossM = 0.0;
        }

        public static void Tick()
        {
            if (!Engaged) return;
            if (ship == null || ship.state == Vessel.State.DEAD
                || station == null || station.state == Vessel.State.DEAD)
            { Disengage("vessel lost"); return; }

            CapsuleRcs.Set(ship, CapsuleRcs.ApproachPct);

            CelestialBody b = station.mainBody;
            if (b == null) { Disengage("no body"); return; }

            // ---- PROJECT OUR STATE INTO THE STATION'S LVLH FRAME (pure/Lvlh.cs) ----
            Vector3d stnR = station.CoM - b.position;
            Vector3d stnV = station.obt_velocity;
            Vector3d rel = ship.CoM - station.CoM;
            Vector3d relV = ship.obt_velocity - station.obt_velocity;
            LvlhState L = Lvlh.Project(stnR.x, stnR.y, stnR.z, stnV.x, stnV.y, stnV.z,
                                       rel.x, rel.y, rel.z, relV.x, relV.y, relV.z);
            if (!L.Valid) { Disengage("degenerate frame"); return; }
            RadialM = L.RadialM; AlongM = L.AlongM; CrossM = L.CrossM; RangeM = L.RangeM;
            RelState rm = RelativeMotion.Of(ship, station);
            ClosingMps = rm.Valid ? rm.ClosingMps : 0.0;

            WpInputs s = new WpInputs();
            s.Valid = true; s.HasTarget = true;
            s.RadialM = L.RadialM; s.AlongM = L.AlongM; s.CrossM = L.CrossM; s.RangeM = L.RangeM;
            s.RadialRateMps = L.RadialRateMps; s.AlongRateMps = L.AlongRateMps; s.CrossRateMps = L.CrossRateMps;
            s.Docked = false;
            s.HoldElapsedS = Planetarium.GetUniversalTime() - holdStartedAt;
            s.RequireCrewGo = CrewProcedureOps.Engaged;
            s.Go = CrewProcedureOps.Engaged && CrewProcedureOps.ReleasedHold == Phase;

            // ---- THE PHASE MACHINE (pure/WaypointApproach.cs) ----
            WpCommand c = WaypointApproach.Guide(s, Phase);
            Note = WaypointApproach.PhaseName(Phase) + " - " + c.Note;

            if (c.KeepOutBreach)
            {
                Phase = WpPhase.Abort;
                Note = "KEEP-OUT ABORT - inside 200 m off the corridor; releasing to the crew";
                Debug.LogWarning(Tag + Note);
                Disengage("keep-out breach");
                return;
            }

            WpPhase next = WaypointApproach.StepPhase(s, Phase, c);
            if (next != Phase)
            {
                Phase = next;
                if (next == WpPhase.Hold0 || next == WpPhase.Hold1 || next == WpPhase.Hold2)
                    holdStartedAt = Planetarium.GetUniversalTime();
            }

            if (Phase == WpPhase.Handover)
            {
                StopTranslating();
                AttitudeController.Ascent.Throttle = 0.0;
                // ---- ⛔ STAND DOWN, LIKE DirectApproach.Match ON Done. ----
                Engaged = false;
                Note = "AT WP2 (20 m) - handed to the docking controller";
                Debug.Log(Tag + "L-approach complete - " + Note);
                return;
            }
            if (Phase == WpPhase.Abort) { StopTranslating(); return; }

            // ---- ACTUATE: point DockControl's servo at the CURRENT waypoint's world position. ----
            double tr, ta, tc;
            WaypointApproach.Waypoint(Phase, out tr, out ta, out tc);
            double ox, oy, oz;
            Lvlh.OffsetToWorld(stnR.x, stnR.y, stnR.z, stnV.x, stnV.y, stnV.z, tr, ta, tc,
                               out ox, out oy, out oz);
            Vector3d worldTgt = station.CoM + new Vector3d(ox, oy, oz);
            Vector3d to = worldTgt - ship.CoM;

            Vector3d up = (ship.CoM - b.position).normalized;
            Vector3d aim = (to.sqrMagnitude > 1e-4) ? to.normalized : up;
            AttitudeController.Ascent.SteerTo(ship, aim, up);
            AttitudeController.Ascent.Throttle = 0.0;

            if (!ship.ActionGroups[KSPActionGroup.RCS])
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

            Transform rt = ship.ReferenceTransform;
            Vector3d nose = rt.up;
            DockState ds = new DockState();
            ds.Valid = true;
            ds.DistF = Vector3d.Dot(to, nose);
            ds.DistS = Vector3d.Dot(to, rt.right);
            ds.DistT = Vector3d.Dot(to, -rt.forward);
            ds.VelF = Vector3d.Dot(relV, nose);
            ds.VelS = Vector3d.Dot(relV, rt.right);
            ds.VelT = Vector3d.Dot(relV, -rt.forward);
            ds.SpeedCap = DockControl.SpeedCapFor(to.magnitude);

            double dt = Time.fixedDeltaTime;
            if (dt <= 0.0) dt = 0.02;
            DockCommand dc = DockControl.Solve(ds, pidF, pidS, pidT, dt);
            AttitudeController.Ascent.UllageFore = dc.Fore;
            AttitudeController.Ascent.TranslateX = dc.Starboard;
            AttitudeController.Ascent.TranslateY = dc.Top;
        }

        private static void StopTranslating()
        {
            AttitudeController.Ascent.UllageFore = 0.0;
            AttitudeController.Ascent.TranslateX = 0.0;
            AttitudeController.Ascent.TranslateY = 0.0;
        }
    }
}
