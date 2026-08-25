/*
 * DragonScreen - WaypointApproachOps
 *
 * GLUE. Flies the real Crew Dragon L-approach: the R-bar -> V-bar path through three station-keeping
 * waypoints (WP0 400 m below, WP1 220 m ahead, WP2 20 m), each a HOLD, inside a 200 m keep-out sphere.
 * The profile and its sequencing are pure and tested in `pure/WaypointApproach.cs`; the LVLH geometry
 * is pure and tested in `pure/Lvlh.cs`. This file is only the wiring between them and KSP.
 *
 * ---- IT INVENTS NO CONTROL LAW. IT POINTS TWO PROVEN PARTS AT A NEW TARGET. ----
 * The phase machine is `WaypointApproach.Guide`/`StepPhase`. The actuator is `DockControl.Solve` - the
 * SAME velocity servo the docking flies, with its braking curve and authority mixing - aimed at the
 * current waypoint's world position instead of a docking port. The world<->vessel projection is the
 * measured one from `DockingOps.FlyTo` (nose = ReferenceTransform.up, starboard = .right, top =
 * -.forward), the convention a flight established. Nothing here is a fresh guess; it is DirectApproach's
 * lifecycle around WaypointApproach's sequence and DockControl's servo.
 *
 * ---- ⚠ RSS ONLY, AND OFF BY DEFAULT (`Enabled`). ----
 * The stock build keeps the straight-in DirectApproach that has flown and docked. This is the RSS
 * fidelity upgrade, gated behind `Enabled` so turning it on is a deliberate, single-change test flight
 * per the project's discipline - it does not silently replace the working approach.
 *
 * ---- THE WP0->WP1 KEEP-OUT GRAZE IS FIXED (fly-around arc), BUT THIS HAS STILL NEVER FLOWN IN RSS. ----
 * The straight WP0->WP1 chord grazed ~193 m and would have aborted mid-L; `pure/WaypointApproach.cs`
 * now flies the ToArc1/ToArc2 fly-around, proven by test to stay at radius >= 220 m the whole way. So
 * the geometry is no longer the blocker. `Enabled` stays false only because the closed-loop behaviour
 * - the DockControl servo tracking a moving LVLH target through the arc, the DirectApproach->L handover
 * at the envelope, arrival dispersions at each hold - has not yet been MEASURED on an RSS flight.
 * Enabling it (`WaypointApproachOps.Enabled = true` in tuning.cfg) is the deliberate single-change test
 * that produces that first measurement; the recorder's x_la* columns are there to read it back.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class WaypointApproachOps
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>
        /// Master switch for the real Crew Dragon L-approach. ON by default: it is the full-fidelity
        /// proximity-operations profile (R-bar -> V-bar through WP0/WP1/WP2, each a crew-GO hold), which is
        /// what the crew-in-the-loop gates authorise. Live-tunable so it can be turned OFF to DirectApproach
        /// from tuning.cfg without a rebuild if a flight needs the straight-in fallback. The fly-around
        /// geometry is proven headless; the closed-loop RSS flight behaviour is validated on the crew flight.
        /// </summary>
        [Tunable] public static bool Enabled = true;

        /// <summary>
        /// Furthest range this will accept the job, metres. WP0 sits 400 m below the station and RCS has
        /// to fly to it. Raised to the real APPROACH ELLIPSOID scale (2000 m along-track × 1000 m): the
        /// named-burn Ti delivers the vehicle just below the station at ~this range, so the L-approach
        /// takes over there and flies WP0->WP1->WP2. 2.5 km slant covers the ellipsoid corner. (Stock
        /// never engages this - RSS only, and the named-burn hands off exactly at EnvelopeM.)
        /// </summary>
        [Tunable] public static double EnvelopeM = 2500.0;

        public static bool Engaged { get; private set; }
        public static WpPhase Phase { get; private set; }
        public static string Note = "-";

        /// <summary>For the pages and the recorder.</summary>
        public static double RangeM, ClosingMps, RadialM, AlongM, CrossM;

        /// <summary>At WP2 and handed over - the caller engages the docking, exactly as with DirectApproach.Done.</summary>
        public static bool Complete { get { return Phase == WpPhase.Handover; } }

        private static Vessel ship, station;
        // The servo's memory lives here, not in Solve - reset on Engage, never inside the loop.
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

            // R-bar/V-bar terminal approach: gentle Draco strength - a little more than docking, far less
            // than a burn, so the small station-relative corrections are precise not wasteful. (2026-08-24)
            CapsuleRcs.Set(ship, CapsuleRcs.ApproachPct);

            CelestialBody b = station.mainBody;
            if (b == null) { Disengage("no body"); return; }

            // ---- PROJECT OUR STATE INTO THE STATION'S LVLH FRAME (pure/Lvlh.cs) ----
            Vector3d stnR = station.CoM - b.position;
            Vector3d stnV = station.obt_velocity;
            Vector3d rel = ship.CoM - station.CoM;                 // ship - station, position
            Vector3d relV = ship.obt_velocity - station.obt_velocity;  // ship - station, velocity
            LvlhState L = Lvlh.Project(stnR.x, stnR.y, stnR.z, stnV.x, stnV.y, stnV.z,
                                       rel.x, rel.y, rel.z, relV.x, relV.y, relV.z);
            if (!L.Valid) { Disengage("degenerate frame"); return; }
            RadialM = L.RadialM; AlongM = L.AlongM; CrossM = L.CrossM; RangeM = L.RangeM;
            // Closing along the line of sight, for the pages/recorder - one definition, in RelativeMotion.
            RelState rm = RelativeMotion.Of(ship, station);
            ClosingMps = rm.Valid ? rm.ClosingMps : 0.0;

            WpInputs s = new WpInputs();
            s.Valid = true; s.HasTarget = true;
            s.RadialM = L.RadialM; s.AlongM = L.AlongM; s.CrossM = L.CrossM; s.RangeM = L.RangeM;
            s.RadialRateMps = L.RadialRateMps; s.AlongRateMps = L.AlongRateMps; s.CrossRateMps = L.CrossRateMps;
            s.Docked = false;
            s.HoldElapsedS = Planetarium.GetUniversalTime() - holdStartedAt;
            // Crew-in-the-loop: when the conductor is running, a hold is left ONLY on the crew's GO for
            // THIS waypoint (CrewProcedureOps.ReleasedHold), which is what makes each hold a real,
            // abortable decision. Un-conducted, RequireCrewGo is false and the hold auto-releases (HoldS).
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
                // Entering a hold starts its clock; entering a to-waypoint leg carries no clock.
                if (next == WpPhase.Hold0 || next == WpPhase.Hold1 || next == WpPhase.Hold2)
                    holdStartedAt = Planetarium.GetUniversalTime();
            }

            if (Phase == WpPhase.Handover)
            {
                StopTranslating();
                AttitudeController.Ascent.Throttle = 0.0;
                // ---- ⛔ STAND DOWN, LIKE DirectApproach.Match ON Done. ----
                // Clear Engaged but keep Phase == Handover so `Complete` stays true for the one caller
                // read that engages the docking. Left engaged, this would zombie behind the docking
                // controller - the exact two-owners fault StationApproach's own banner warns about.
                Engaged = false;
                Note = "AT WP2 (20 m) - handed to the docking controller";
                Debug.Log(Tag + "L-approach complete - " + Note);
                return;   // Complete == true; StationApproach engages DockingOps.
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

            // Hold a steady attitude: nose toward the current target (so the servo's dominant "fore"
            // axis lines up with the approach), orbital up as the roll reference. At a hold `to`->0, so
            // fall back to orbital up. RCS translation - not rotation - does the flying, exactly as the
            // docking does; a steady attitude is all the projection below needs.
            Vector3d up = (ship.CoM - b.position).normalized;
            Vector3d aim = (to.sqrMagnitude > 1e-4) ? to.normalized : up;
            AttitudeController.Ascent.SteerTo(ship, aim, up);
            AttitudeController.Ascent.Throttle = 0.0;   // RCS-only prox ops; no main-engine thrust.

            if (!ship.ActionGroups[KSPActionGroup.RCS])
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

            // Offsets and relative velocity resolved onto the capsule's OWN axes - the measured
            // convention from DockingOps.FlyTo (rt.forward is the controller's -top; see AttitudeController).
            Transform rt = ship.ReferenceTransform;
            Vector3d nose = rt.up;
            DockState ds = new DockState();
            ds.Valid = true;
            ds.DistF = Vector3d.Dot(to, nose);
            ds.DistS = Vector3d.Dot(to, rt.right);
            ds.DistT = Vector3d.Dot(to, -rt.forward);
            ds.VelF = Vector3d.Dot(relV, nose);          // relV is ours-minus-theirs, DockControl's convention
            ds.VelS = Vector3d.Dot(relV, rt.right);
            ds.VelT = Vector3d.Dot(relV, -rt.forward);
            ds.SpeedCap = DockControl.SpeedCapFor(to.magnitude);   // braking curve to THIS waypoint

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
