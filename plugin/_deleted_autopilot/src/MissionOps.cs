// DragonScreen - MissionOps
// ---- ⛔ WHY THIS FILE HAD TO BE WRITTEN AT ALL ----
// ---- WHAT EACH ONE REFUSES, AND WHY REFUSING IS THE POINT ----
using UnityEngine;

namespace DragonScreen
{
    public static class MissionOps
    {
        private const string Tag = "[DragonScreen] ";

        public static string LastRefusal = "-";

        public static void Rendezvous()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            if (StationApproach.Engaged) { StationApproach.Disengage("crew"); return; }

            if (DockedSide.Docked(v))
            {
                Refuse("already docked - there is nothing to rendezvous with");
                return;
            }
            if (v.situation == Vessel.Situations.PRELAUNCH
                || v.situation == Vessel.Situations.LANDED
                || v.situation == Vessel.Situations.SPLASHED)
            {
                Refuse("on the ground - the rendezvous starts from orbit");
                return;
            }
            if (v.orbit == null || v.mainBody == null
                || v.orbit.PeA < v.mainBody.atmosphereDepth)
            {
                Refuse("not in a stable orbit - circularise first");
                return;
            }

            StationApproach.Engage();
            if (!StationApproach.Engaged) Refuse(StationApproach.Note);
        }

        public static void AutoDock()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            if (DockingOps.Engaged) { DockingOps.Reset(); Log("auto-dock cancelled by the crew"); return; }

            if (DockedSide.Docked(v)) { Refuse("already docked"); return; }

            Vessel station = StationApproach.Station;
            if (station == null) station = StationApproach.Find();
            if (station == null)
            {
                Refuse("no station found - it must be loaded to dock with it");
                return;
            }

            // ---- ONE BUTTON, THE WHOLE JOB. Far -> rendezvous; close -> dock. ----
            double range = Vector3d.Distance(v.CoM, station.CoM);
            if (range > DockingOps.DockEnvelopeM)
            {
                if (StationApproach.Engaged) { Log("auto-dock: rendezvous already running"); return; }
                StationApproach.Engage();
                if (StationApproach.Engaged)
                    Log("auto-dock from " + (range / 1000.0).ToString("F1")
                        + " km - flying the rendezvous, which hands to docking at ~200 m");
                else
                    Refuse(StationApproach.Note);
                return;
            }

            DockingOps.Engage(v, station);
            if (!DockingOps.Engaged) Refuse(DockingOps.Note);
        }

        public static void UndockAndLand()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            if (UndockOps.Engaged) { UndockOps.Reset(); Log("undock cancelled by the crew"); return; }

            if (!DockedSide.Docked(v))
            {
                Refuse("not docked - use DEORBIT NOW to come home from here");
                return;
            }

            Vessel station = StationApproach.Station;
            if (station == null) station = StationApproach.Find();

            // ---- SAY WHAT THE RETURN BUDGET LOOKS LIKE BEFORE LETTING GO. ----
            Debug.Log(Tag + "undock requested - capsule monopropellant "
                      + DockedSide.Mono(v).ToString("F1") + " / "
                      + DockedSide.MonoCapacity(v).ToString("F1") + " units before the top-up");

            UndockOps.Engage(v, station);
            if (!UndockOps.Engaged) Refuse(UndockOps.Note);
        }

        private static void Refuse(string why)
        {
            LastRefusal = why;
            Debug.LogWarning(Tag + "command refused - " + why);
            ScreenMessages.PostScreenMessage("Dragon: " + why, 6f,
                                             ScreenMessageStyle.UPPER_CENTER);
        }

        private static void Log(string what) { Debug.Log(Tag + what); }
    }
}
