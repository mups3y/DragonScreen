/*
 * DragonScreen - StationApproach
 *
 * GLUE. Finds and measures the station (the ISS), then runs the Crew-2 co-elliptic named-burn
 * rendezvous (NamedRendezvousOps: NC -> NSR -> Ti), which hands to the R-bar/V-bar L-approach
 * (WaypointApproachOps) and then to docking (DockingOps). Station found by name/type - F9I's
 * `StFindStation`.
 *
 * ---- ⛔ THE RULE THAT COST A VEHICLE ----
 * `falcon-rendezvous-approach-law`: NEVER chase a co-orbital target. Pursuit steering de-orbited
 * flight 012 - about 1.6 t of second stage and 38 units of monopropellant spent driving straight at
 * the station, ending with its own periapsis 15.6 km underground. So nothing here points at the
 * station and pushes; the named-burn transfers get onto the station's orbit, and only the L-approach's
 * final few hundred metres, at cm/s, point at the target - the one regime where "toward" and
 * "correct" are the same direction.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    // NOTE: the constants class  calls StationOps is a different thing - it holds
    // the landing-calibrated de-orbit orbit. This is the approach FLIGHT SOFTWARE, hence the name.
    public static class StationApproach
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>The Crew-2 target station: the ISS. Matched by exact name first, then by the
        /// vesselType == Station fallback in Find (so a differently-named ISS is still found).</summary>
        public const string StationName = "ISS USOS Real Size";

        /// <summary>Where the approach stops and station-keeping begins, metres.</summary>
        public const double GoalRangeM = 60.0;

        public static bool Engaged { get; private set; }
        public static ApproachLeg Leg { get; private set; }
        public static Vessel Station { get; private set; }

        /// <summary>For the pages and the recorder.</summary>
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
            // Fly the real co-elliptic named-burn profile (NC -> NSR -> Ti -> L-approach).
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
            // flight_0821_060847: the crew cancelled the rendezvous, this cleared, but the DIRECT
            // approach we had handed off to stayed Engaged and stopped ticking (RangeM froze at
            // 815.8 m for 702 s). Owner() then read it as a live controller and every de-orbit after
            // it was CONTENDED:deorbit - two owners, the node never aligned, the return never ran.
            // Only Reset() used to clean it up; a crew CANCEL calls Disengage, so it must too.
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

        /// <summary>
        /// The station to rendezvous with. Public so the LAUNCH WINDOW can find it before liftoff.
        ///
        /// ---- ⛔ DETECT BY WHAT IT IS, NOT ONLY WHAT IT IS CALLED. ----
        /// The stock build ships a station literally named "Space X Station", so that exact name wins
        /// when present. But in RSS/RO the user builds and NAMES THEIR OWN ISS (measured 2026-08-22:
        /// "ISS USOS Real Size"), and a launch that matched only the stock name found nothing, so the
        /// launch azimuth defaulted to due east and the rendezvous had no target. KSP still TYPES that
        /// vessel as a Station (`type = Station` in the save), and the type survives any rename - so we
        /// fall back to the first orbiting Station-type vessel. `falcon-detect-by-capability`.
        /// </summary>
        public static Vessel Find()
        {
            List<Vessel> all = FlightGlobals.Vessels;
            Vessel typed = null;
            for (int i = 0; i < all.Count; i++)
            {
                Vessel s = all[i];
                if (s == null || s.state == Vessel.State.DEAD) continue;
                if (s.vesselName == StationName) return s;                 // exact ISS name wins
                if (s.vesselType == VesselType.Station && s != FlightGlobals.ActiveVessel
                    && typed == null) typed = s;                          // else: it IS a station
            }
            return typed;
        }

        /// <summary>
        /// Keep the station numbers live while the approach is idle.
        ///
        /// ⚠ ONE SOURCE, STILL. This writes the same fields `Tick` writes, from the same
        /// `RelativeMotion.Of` - it is not a second computation of the same quantity, which is the
        /// thing the recorder's own comment warns against. It just means the fields are measured
        /// rather than remembered.
        /// </summary>
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
            // This used to `return` here, so `RangeM` and `ClosingMps` kept whatever they held when
            // the crew last disengaged - and the recorder reads exactly those fields. On the
            // 2026-08-11 13:44 flight they last changed at MET 53 and were then written unchanged
            // into every one of the next 6700 rows: "13.71 km, closing -127.47 m/s" for 99% of a two
            // hour flight, in the instrument CLAUDE.md calls the primary one.
            //
            // The same shape of fault as the docking refusal that started this: a true statement
            // about a stale variable and a false one about the world. Observing costs one vector
            // subtraction per frame and there is no version of this where a frozen number is better.
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
            // `Leg` is recomputed from range on EVERY tick, twenty lines below, so `Arrived` never
            // stuck. On 2026-08-12 the approach matched at 43 m and handed over correctly - and then
            // the capsule drifted to 60 m, this re-classified it as still approaching, and re-engaged
            // the direct approach, whose goal is `DirectApproach.GoalM` = 200 m:
            //
            //     09:50:31  direct approach complete - MATCHED - 43 m at 0.50 m/s. Handing to the docking.
            //     09:50:31  docking engaged - KDSS-A to KDSS-P, keep-out 31 m
            //     09:50:47  direct approach engaged at 60 m - closing to 200 m at 0.5 m/s
            //
            // So from 60 m the approach flew AWAY, because 200 m is where it had been told to be,
            // while the docking controller pulled toward the port. Two owners of one set of
            // thrusters, pulling opposite ways, for eleven minutes - which is the crew's report in
            // both its halves: "docking seems to move us away instead of towards", and the tank at
            // 0.0 units by 10:01.
            //
            // The fix is the one the fix plan already names as step 6, ONE OWNER PER ACTUATOR, and
            // the order is not arbitrary: docking is the more specific controller and it is the one
            // holding a port pair, so it wins and this yields entirely - no re-classification, no
            // re-engage, not even a measurement that could tempt a later branch.
            if (DockingOps.Engaged || DockingOps.Stage == DockStage.Docked)
            {
                if (DirectApproachOps.Engaged) DirectApproachOps.Disengage("docking has the vehicle");
                Leg = ApproachLeg.Arrived;
                Observe();
                Note = "DOCKING - " + DockingOps.Stage + " " + DockingOps.Note;
                return;
            }

            // ---- MEASURE ----
            // One definition, in RelativeMotion - this file and DirectApproachOps used to compute
            // it separately with opposite operand order, and one of them was wrong.
            RelState rm = RelativeMotion.Of(ship, Station);
            RangeM = rm.RangeM;
            ClosingMps = rm.ClosingMps;
            LateralMps = rm.LateralMps;

            // ---- RSS L-APPROACH OWNS THE VEHICLE ONCE ENGAGED (like the DirectApproach block below). ----
            // The R-bar/V-bar waypoint profile (WaypointApproachOps) is the RSS terminal approach, off by
            // default. Once it has the vehicle it flies WP0->WP1->WP2 and this file is a passenger until it
            // completes (hand to docking), aborts (keep-out), or releases (envelope). Stock never sees it.
            if (WaypointApproachOps.Engaged)
            {
                WaypointApproachOps.Tick();
                Note = "L-APPROACH - " + WaypointApproachOps.Note;
                if (WaypointApproachOps.Complete) { Arrived(); return; }
                if (!WaypointApproachOps.Engaged) { Halt("L-approach released - " + WaypointApproachOps.Note); return; }
                return;
            }

            // ---- Crew-2 co-elliptic named-burn rendezvous (NC -> NSR -> Ti). It hands the vehicle to
            // the L-approach (WaypointApproachOps, run in the block above) itself; docking already won
            // above if it has the vehicle.
            RangeM = Vector3d.Distance(ship.CoM, Station.CoM);
            NamedRendezvousOps.Tick();
            Leg = (NamedRendezvousOps.Leg == RdvLeg.Arrived) ? ApproachLeg.Arrived : ApproachLeg.Phasing;
            Note = "NAMED-BURN " + NamedRendezvousOps.Leg + " - " + NamedRendezvousOps.Note;
        }



        /// <summary>
        /// Stop the approach where it is, holding attitude, and say why.
        ///
        /// ⛔ STOPPING IS A RESULT, NOT A FAILURE. The capsule is left co-moving and safe with every
        /// option open, which is worth more than any burn this code could pick on its own. The crew
        /// press RENDEZVOUS again to retry from here.
        /// </summary>
        private static void Halt(string why)
        {
            Hold();
            Note = "STOPPED - " + why + ". Press RENDEZVOUS again to retry from here.";
            // ⚠ LATCH ON THE FACT, NOT THE SENTENCE. The first version compared the whole `why`
            // string, which carries the range - so it changed every tick and logged every tick:
            // twelve identical warnings in ten seconds on 2026-08-11 as the capsule drifted out.
            if (haltReported) return;
            haltReported = true;
            Debug.LogWarning(Tag + "rendezvous stopped - " + why + ". Range "
                             + (RangeM / 1000.0).ToString("F2") + " km, closing "
                             + ClosingMps.ToString("F2") + " m/s. Nothing burned.");
        }

        private static bool haltReported;



        private static void Arrived()
        {
            // The ladder has done its job. Docking is a different problem with a different frame -
            // port axes and a keep-out sphere rather than orbits - so it gets its own controller
            // rather than another branch in here.
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

        /// <summary>Fore translation, through the controller so it reaches this vessel's own state.</summary>
        private static void Translate(double fore)
        {
            AttitudeController.Ascent.UllageFore = fore;
        }


    }
}
