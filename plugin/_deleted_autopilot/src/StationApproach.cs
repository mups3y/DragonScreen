// DragonScreen - StationApproach
// ---- ⛔ THE RULE THAT COST A VEHICLE ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class StationApproach
    {
        private const string Tag = "[DragonScreen] ";

        public const string StationName = "ISS USOS Real Size";

        public const double GoalRangeM = 60.0;

        public static bool Engaged { get; private set; }
        public static ApproachLeg Leg { get; private set; }
        public static Vessel Station { get; private set; }

        public static double RangeM, ClosingMps, LateralMps, AlongTrackM, LastDvMps;
        public static string Note = "-";

        private static Vessel ship;
        private static int lastFrame = -1;

        // ------------------------------------------------------------------ lifecycle

        public static void Toggle()
        {
            if (Engaged) Disengage("crew"); else Engage();
        }

        public static void Engage()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            Station = Find();
            if (Station == null)
            {
                Debug.LogWarning(Tag + "RENDEZVOUS refused - no vessel named '" + StationName
                                     + "' in this game. Nothing to rendezvous with.");
                return;
            }
            if (v.orbit == null || v.mainBody == null
                || v.orbit.PeA < v.mainBody.atmosphereDepth)
            {
                Debug.LogWarning(Tag + "RENDEZVOUS refused - not in a stable orbit yet.");
                return;
            }

            ship = v;
            Engaged = true;
            Leg = ApproachLeg.Phasing;
            if (NamedRendezvousOps.Engage(v, Station))
                Debug.Log(Tag + "flying the named-burn co-elliptic rendezvous");
            haltReported = false;
            Debug.Log(Tag + "rendezvous ENGAGED - target '" + Station.vesselName + "', "
                          + (Vector3d.Distance(v.CoM, Station.CoM) / 1000.0).ToString("F1") + " km");
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
            // ---- ⛔ TAKE THE HANDED-OFF DIRECT APPROACH DOWN WITH US, OR IT ZOMBIES ----
            if (DirectApproachOps.Engaged) DirectApproachOps.Disengage("rendezvous cancelled");
            if (WaypointApproachOps.Engaged) WaypointApproachOps.Disengage("rendezvous cancelled");
            if (NamedRendezvousOps.Engaged) NamedRendezvousOps.Disengage("rendezvous cancelled");
            AttitudeController.Ascent.Release(ship);
            if (ship != null && ship.ctrlState != null)
            {
                ship.ctrlState.Z = 0f;
                ship.ctrlState.X = 0f;
                ship.ctrlState.Y = 0f;
            }
            ship = null;
            Note = "-";
            Debug.Log(Tag + "rendezvous DISENGAGED - " + why);
        }

        public static void Reset()
        {
            DirectApproachOps.Reset();
            WaypointApproachOps.Reset();
            NamedRendezvousOps.Reset();
            Engaged = false; Station = null; ship = null;
            haltReported = false;
            RangeM = 0.0; ClosingMps = 0.0; LateralMps = 0.0; AlongTrackM = 0.0; LastDvMps = 0.0;
            Note = "-";
        }

        /// ---- ⛔ DETECT BY WHAT IT IS, NOT ONLY WHAT IT IS CALLED. ----
        public static Vessel Find()
        {
            List<Vessel> all = FlightGlobals.Vessels;
            Vessel typed = null;
            for (int i = 0; i < all.Count; i++)
            {
                Vessel s = all[i];
                if (s == null || s.state == Vessel.State.DEAD) continue;
                if (s.vesselName == StationName) return s;
                if (s.vesselType == VesselType.Station && s != FlightGlobals.ActiveVessel
                    && typed == null) typed = s;
            }
            return typed;
        }

        private static void Observe()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (Station == null) Station = Find();
            if (v == null || Station == null || Station.state == Vessel.State.DEAD)
            {
                RangeM = 0.0; ClosingMps = 0.0; LateralMps = 0.0; AlongTrackM = 0.0;
                return;
            }
            RelState rm = RelativeMotion.Of(v, Station);
            if (!rm.Valid) return;
            RangeM = rm.RangeM;
            ClosingMps = rm.ClosingMps;
            LateralMps = rm.LateralMps;
        }

        // ------------------------------------------------------------------ the loop

        public static void Tick()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;

            // ---- ⛔ MEASURE EVEN WHEN WE ARE NOT FLYING IT. ----
            if (!Engaged)
            {
                Observe();
                return;
            }

            if (ship == null || ship.state == Vessel.State.DEAD
                || Station == null || Station.state == Vessel.State.DEAD)
            {
                Disengage("vessel lost");
                return;
            }

            // ---- ⛔ ONCE THE DOCKING HAS THE VEHICLE, THIS FILE IS A PASSENGER. ----
            if (DockingOps.Engaged || DockingOps.Stage == DockStage.Docked)
            {
                if (DirectApproachOps.Engaged) DirectApproachOps.Disengage("docking has the vehicle");
                Leg = ApproachLeg.Arrived;
                Observe();
                Note = "DOCKING - " + DockingOps.Stage + " " + DockingOps.Note;
                return;
            }

            // ---- MEASURE ----
            RelState rm = RelativeMotion.Of(ship, Station);
            RangeM = rm.RangeM;
            ClosingMps = rm.ClosingMps;
            LateralMps = rm.LateralMps;

            // ---- RSS L-APPROACH OWNS THE VEHICLE ONCE ENGAGED (like the DirectApproach block below). ----
            if (WaypointApproachOps.Engaged)
            {
                WaypointApproachOps.Tick();
                Note = "L-APPROACH - " + WaypointApproachOps.Note;
                if (WaypointApproachOps.Complete) { Arrived(); return; }
                if (!WaypointApproachOps.Engaged) { Halt("L-approach released - " + WaypointApproachOps.Note); return; }
                return;
            }

            // ---- Crew-2 co-elliptic named-burn rendezvous (NC -> NSR -> Ti). It hands the vehicle to
            RangeM = Vector3d.Distance(ship.CoM, Station.CoM);
            NamedRendezvousOps.Tick();
            Leg = (NamedRendezvousOps.Leg == RdvLeg.Arrived) ? ApproachLeg.Arrived : ApproachLeg.Phasing;
            Note = "NAMED-BURN " + NamedRendezvousOps.Leg + " - " + NamedRendezvousOps.Note;
        }

        private static void Halt(string why)
        {
            Hold();
            Note = "STOPPED - " + why + ". Press RENDEZVOUS again to retry from here.";
            if (haltReported) return;
            haltReported = true;
            Debug.LogWarning(Tag + "rendezvous stopped - " + why + ". Range "
                             + (RangeM / 1000.0).ToString("F2") + " km, closing "
                             + ClosingMps.ToString("F2") + " m/s. Nothing burned.");
        }

        private static bool haltReported;

        private static void Arrived()
        {
            if (!DockingOps.Engaged && DockingOps.Stage != DockStage.Docked
                && DockingOps.Stage != DockStage.NoPort)
            {
                DockingOps.Engage(ship, Station);
            }
            if (DockingOps.Engaged || DockingOps.Stage == DockStage.Docked)
            {
                Note = "DOCKING - " + DockingOps.Stage + " " + DockingOps.Note;
                return;
            }
            Hold();
            Note = "STATION KEEPING at " + RangeM.ToString("F0") + " m - " + DockingOps.Note;
        }

        // ------------------------------------------------------------------ helpers

        private static void Hold()
        {
            Translate(0.0);
            AttitudeController.Ascent.Release(ship);
        }

        private static void Translate(double fore)
        {
            AttitudeController.Ascent.UllageFore = fore;
        }

    }
}
