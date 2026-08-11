/*
 * DragonScreen - MissionOps
 *
 * GLUE. The three crew commands that start the middle of a station ferry, and the guards that decide
 * whether each one means anything right now.
 *
 * ---- ⛔ WHY THIS FILE HAD TO BE WRITTEN AT ALL ----
 * `StationApproach.Engage` and `UndockOps.Engage` were ported, tested, and marked DONE in the port
 * map - and a search of the whole plugin found **no caller for either**. Sections 6 and 7 of the
 * mission could not be started from the cockpit, from a panel button, or from anywhere else. The
 * dead-code sweep that found them also found `pure/LaunchWindow.cs` and `pure/DockControl.cs` in the
 * same state: written, tested, referenced by nothing.
 *
 * That is the failure mode this project keeps hitting, and it is worse than an untested port because
 * it LOOKS finished from every angle except the one that matters. The rule it earns:
 *
 *      **A phase with no caller is not ported. Wire it the same day you write it, or it does not
 *      count - and the port map row stays open until something presses it.**
 *
 * ---- WHAT EACH ONE REFUSES, AND WHY REFUSING IS THE POINT ----
 * Every command here can be pressed at a moment when it is meaningless, because a touchscreen has no
 * way to stop the crew. A press that quietly does nothing is indistinguishable from a dead screen, so
 * each one says what it would have done and why it will not.
 */
using UnityEngine;

namespace DragonScreen
{
    public static class MissionOps
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>What the last command said when it refused. Shown to the crew.</summary>
        public static string LastRefusal = "-";

        /// <summary>
        /// §6. Match the station's orbit, phase, close on Clohessy-Wiltshire, hand to the docking.
        /// </summary>
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
            // ⚠ `falcon-rendezvous-approach-law`: the whole ladder assumes an orbit to work from.
            // Below the atmosphere there is no orbit, only a fall.
            if (v.orbit == null || v.mainBody == null
                || v.orbit.PeA < v.mainBody.atmosphereDepth)
            {
                Refuse("not in a stable orbit - circularise first");
                return;
            }

            StationApproach.Engage();
            if (!StationApproach.Engaged) Refuse(StationApproach.Note);
        }

        /// <summary>
        /// §7. The last few hundred metres onto the port.
        ///
        /// ⚠ SAFE FROM ANY RANGE, deliberately - `falcon-rendezvous-design` records that the reach is
        /// a forced load distance, not drift. What it must NOT do is drive at the station: the gate
        /// and the hull skirt in `DockingOps` are what keep it off the structure, and pressing this
        /// from 10 km simply means the approach starts further out.
        /// </summary>
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

            DockingOps.Engage(v, station);
            if (!DockingOps.Engaged) Refuse(DockingOps.Note);
        }

        /// <summary>
        /// §7 → §8. Top up, release, back away. The de-orbit is a separate press: backing away and
        /// leaving orbit are different decisions and the crew should get to make them separately.
        /// </summary>
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
            // F9I: "Flight 026 undocked 53 units short and nothing noticed until the de-orbit burn
            // died on the reserve floor." The refuel runs during the hold, so this is the reading
            // BEFORE it - the one at release is logged by UndockOps and is the one that counts.
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
