/*
 * DragonScreen - BargeWaypoint
 *
 * GLUE. Puts a real KSP map/navball WAYPOINT on the droneship, so the crew can see where the booster is
 * aiming and fly toward it. Uses the stock FinePrint custom-waypoint system (the same one the "Custom
 * Waypoints" mod drives), so the marker shows in map view, the tracking station, and on the navball.
 *
 * ---- IT READS THE SAME COORDINATE THE GUIDANCE AIMS AT ----
 * The lat/lon come straight from BoosterRecovery's droneship target, not a second copy, so if the barge
 * is ever moved the marker moves with it - the map can never disagree with where the stage is flying.
 *
 * ---- PLACED ONCE, DE-DUPED, AND MOVED IF THE BARGE MOVED ----
 * Custom waypoints persist in the save, so this checks for an existing marker with our name before adding
 * one, and never stacks duplicates across scene loads. But the barge DOES move (the droneship is repositioned
 * to where the booster's flown profile actually lands - user rule 2026-08-24), and a persisted marker at the
 * OLD coords must follow it, or the map lies about the target. So when an existing marker is found at coords
 * that differ from the live droneship, it is removed and re-dropped at the new spot rather than left stale.
 * All of it is wrapped so a waypoint-API hiccup can never take the flight down - a missing map marker is a
 * cosmetic loss, not a flight one.
 */
using System.Collections.Generic;
using FinePrint;
using UnityEngine;

namespace DragonScreen
{
    public static class BargeWaypoint
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>The marker's name - also how we find it again to avoid duplicating it.</summary>
        private const string MarkerName = "OCISLY droneship";

        /// <summary>Fixed seed so the marker keeps one colour rather than flickering per load.</summary>
        private const int ColourSeed = 0x0CE1;

        /// <summary>Coords this close (degrees) count as "already on the barge" - no move needed.</summary>
        private const double SameCoordDeg = 1.0e-4;

        private static bool done;

        /// <summary>Ticked by FlightDriver. Cheap once the marker exists; places it on the first frame it can.</summary>
        public static void Ensure()
        {
            if (done) return;
            try
            {
                Vessel v = FlightGlobals.ActiveVessel;
                if (v == null || v.mainBody == null) return;

                // The droneship recovery is an Earth/RSS thing; on Kerbin the booster lands at an LZ, not
                // this barge. Only mark the body the droneship actually sits on.
                if (!IsDroneshipBody(v.mainBody)) return;

                if (ScenarioCustomWaypoints.Instance == null) return;   // scenario not up yet - try next tick
                if (WaypointManager.Instance() == null) return;

                double lat = BoosterRecovery.DroneshipEarthLatDeg;
                double lon = BoosterRecovery.DroneshipEarthLonDeg;

                Waypoint existing = FindExisting();
                if (existing != null)
                {
                    // Already on the barge? Nothing to do. Otherwise the barge has moved and the stale
                    // marker must follow it - remove it and fall through to drop a fresh one at the new spot.
                    if (System.Math.Abs(existing.latitude - lat) < SameCoordDeg
                        && System.Math.Abs(existing.longitude - lon) < SameCoordDeg)
                    {
                        done = true;
                        return;
                    }
                    ScenarioCustomWaypoints.RemoveWaypoint(existing);
                    Debug.Log(Tag + "barge waypoint moved from "
                              + existing.latitude.ToString("F4") + ", " + existing.longitude.ToString("F4")
                              + " to " + lat.ToString("F4") + ", " + lon.ToString("F4"));
                }

                Waypoint wp = new Waypoint();
                wp.celestialName = v.mainBody.name;
                wp.latitude = lat;
                wp.longitude = lon;
                wp.altitude = 0.0;                 // sea level - the deck floats
                wp.name = MarkerName;
                wp.id = "custom";                  // the stock pushpin icon
                wp.seed = ColourSeed;
                wp.isOnSurface = true;             // clamp the marker to the sea surface
                wp.isNavigatable = true;           // the crew can activate it as the navball target
                ScenarioCustomWaypoints.AddWaypoint(wp);
                Debug.Log(Tag + "barge waypoint placed at "
                          + wp.latitude.ToString("F4") + ", " + wp.longitude.ToString("F4")
                          + " on " + wp.celestialName);
                done = true;
            }
            catch (System.Exception e)
            {
                // A cosmetic marker must never break the flight. Log once and stop trying.
                Debug.LogWarning(Tag + "barge waypoint could not be placed: " + e.Message);
                done = true;
            }
        }

        /// <summary>Reset the once-only latch (e.g. on scene change), so the marker is re-checked next flight.</summary>
        public static void Reset() { done = false; }

        private static bool IsDroneshipBody(CelestialBody body)
        {
            // Earth in RSS is the home body and is NOT named "Kerbin". Anything that is the home body and
            // not Kerbin is the RSS Earth the barge sits on.
            return body != null && body.isHomeWorld && body.name != "Kerbin";
        }

        private static Waypoint FindExisting()
        {
            // All waypoints (contract + custom) live in the global WaypointManager; a persisted marker
            // from a previous session shows up here, so this is what stops us stacking duplicates - and
            // is the handle we need to MOVE it when the barge has been repositioned.
            List<Waypoint> all = WaypointManager.Instance().Waypoints;
            if (all == null) return null;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].name == MarkerName) return all[i];
            return null;
        }
    }
}
