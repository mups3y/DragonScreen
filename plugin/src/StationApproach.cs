/*
 * DragonScreen - StationApproach
 *
 * GLUE. Flies the rendezvous ladder in `pure/Approach.cs` and `pure/CwTargeting.cs` against the
 * station. Ported from `F9I/station_ops.ks` - `StFindStation:142`, `StRelVel:737`, `StCwLeg:1037`,
 * `StTerminal:1199`.
 *
 * ---- ⛔ THE RULE THAT COST A VEHICLE ----
 * `falcon-rendezvous-approach-law`: NEVER chase a co-orbital target. Pursuit steering de-orbited
 * flight 012 - about 1.6 t of second stage and 38 units of monopropellant spent driving straight at
 * the station, ending with its own periapsis 15.6 km underground.
 *
 * So nothing here points at the station and pushes. Outside the terminal range it plans a CW
 * transfer and burns it once; inside, it flies a speed ladder with a deadband. The one place it does
 * point at the target is the last few hundred metres, at 1 m/s, which is the only regime where
 * "toward" and "correct" are the same direction.
 *
 * ---- THE FRAME IS BUILT FROM CURRENT STATE, DELIBERATELY ----
 * F9I solves at a future burn time using `positionat`/`velocityat`. We solve for a burn NOW and take
 * the state from `v.CoM - body.position` and `v.obt_velocity`, which are unambiguous. KSP's
 * `getOrbitalVelocityAtUT` family swaps Y and Z - MechJeb carries SwappedOrbitalVelocityAtUT for
 * exactly that reason - and a frame error here looks identical to a tuning problem right up until
 * the capsule burns the wrong way. Solving now costs the ability to plan ahead and buys certainty.
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

        /// <summary>The station is found by NAME, as F9I does. `StFindStation:142`.</summary>
        public const string StationName = "Space X Station";

        /// <summary>Where the approach stops and station-keeping begins, metres.</summary>
        public const double GoalRangeM = 60.0;

        public static bool Engaged { get; private set; }
        public static ApproachLeg Leg { get; private set; }
        public static Vessel Station { get; private set; }

        /// <summary>RSS (Earth) flies the real named-burn co-elliptic rendezvous (NamedRendezvousOps);
        /// stock keeps the ladder in this file. Set at Engage from the body radius.</summary>
        private static bool useNamedBurn;

        /// <summary>For the pages and the recorder.</summary>
        public static double RangeM, ClosingMps, LateralMps, AlongTrackM, LastDvMps;
        public static string Note = "-";

        private static Vessel ship;
        private static double startedAt;
        private static double lastBurnAt = -999.0;
        /// <summary>One phasing refusal is a finding; 4515 of them is noise. Latched, reset on success.</summary>
        private static bool phaseRefusalLogged;

        /// <summary>When the phasing coast returns us to the burn point. 0 = not phasing.</summary>
        private static double phaseReturnUt;

        /// <summary>
        /// Rails-warp a long, empty coast, and drop out in time to fly the burn.
        ///
        /// A phasing lap at 120 km is about 34 minutes of doing nothing, and `MaxLaps` allows five
        /// of them - close to three hours of real time watching a static screen. F9I warps its
        /// coasts for exactly this reason.
        ///
        /// ⛔ RAILS WARP ONLY, AND ONLY WHILE NOTHING IS BEING FLOWN. Physics warp would corrupt
        /// the attitude solution, and warping through a burn is how a manoeuvre gets missed
        /// entirely. It steps DOWN as the burn approaches rather than dropping from 1000x to 1x in
        /// one frame, because KSP needs several physics ticks to settle a vessel coming off rails
        /// and the node executor needs a settled vessel to aim.
        ///
        /// ⚠ The crew can always override - this only ever RAISES warp toward what the remaining
        /// time allows, and never fights a rate the crew has chosen for themselves.
        /// </summary>
        private static void Warp(double secondsLeft)
        {
            if (TimeWarp.fetch == null) return;
            if (!ship.Landed && ship.situation != Vessel.Situations.ORBITING) return;

            int want;
            if (secondsLeft > 600.0) want = 4;          // 100x
            else if (secondsLeft > 180.0) want = 3;     //  50x
            else if (secondsLeft > 60.0) want = 2;      //  10x
            else if (secondsLeft > 25.0) want = 1;      //   5x
            else want = 0;

            if (want < TimeWarp.CurrentRateIndex) TimeWarp.SetRate(want, true);
            else if (want > TimeWarp.CurrentRateIndex
                     && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
                TimeWarp.SetRate(want, false);
        }
        /// <summary>Phasing laps flown this engagement. Bounded by `Approach.PhaseMaxPass`.</summary>
        private static int phasePass;
        /// <summary>Latch, so using up the laps is said once and not every tick.</summary>
        private static bool phaseCapReported;
        /// <summary>
        /// Set once the range first reaches the direct-approach gate. After that, NO new phasing laps -
        /// if the approach is thrown back out of the gate (a fast crossing the phasing left), riding the
        /// existing intercept to match velocity is right; going back out to 22 km for another whole lap
        /// is the "rendezvous that takes longer" the crew flagged 2026-08-18 (flight_0818_154218).
        /// </summary>
        private static bool reachedGate;
        private static int lastFrame = -1;

        /// <summary>
        /// Which leg planned the node the executor is currently flying. A node in flight is routed back
        /// to its owner so no other leg can grab it (the `CONTENDED` bug) and, above all, so a long warp
        /// to one leg's node cannot silently starve the others. flight_0820_221434: `MatchAltitude`
        /// planned a circularise node ~2800 s ahead and the executor warped to it at 100x, holding the
        /// tick for the ENTIRE coast - the drift closed 91 -> 15 km underneath it and `RideIntercept`,
        /// the leg that should have matched that pass, never ran.
        /// </summary>
        private static ApproachLeg nodeOwner = ApproachLeg.Phasing;

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
            // RSS/Earth: fly the real co-elliptic named-burn profile (NC -> NSR -> Ti -> L-approach)
            // instead of the stock phasing ladder. Body radius > 1e6 is the Earth gate (Kerbin 600 km).
            useNamedBurn = (v.mainBody != null && v.mainBody.Radius > 1.0e6);
            if (useNamedBurn && NamedRendezvousOps.Engage(v, Station))
                Debug.Log(Tag + "RSS - flying the named-burn co-elliptic rendezvous");
            startedAt = Planetarium.GetUniversalTime();
            lastBurnAt = -999.0;
            haltReported = false;
            phasePass = 0; phaseCapReported = false; reachedGate = false; nodeOwner = ApproachLeg.Phasing;
            phaseReturnUt = 0.0;
            altMatchBurns = 0; altCapReported = false;
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
            useNamedBurn = false;
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
            useNamedBurn = false;
            Engaged = false; Station = null; ship = null;
            phaseReturnUt = 0.0;
            phasePass = 0; phaseCapReported = false;
            altMatchBurns = 0; altCapReported = false;
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
                if (s.vesselName == StationName) return s;                 // exact stock name wins
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

            // ---- RSS: the named-burn co-elliptic profile owns everything up to the L-approach. ----
            // NC -> NSR -> Ti, then it hands to WaypointApproachOps above. The stock ladder below never
            // runs on Earth. Docking (checked earlier) still wins if it has the vehicle.
            if (useNamedBurn)
            {
                RangeM = Vector3d.Distance(ship.CoM, Station.CoM);
                NamedRendezvousOps.Tick();
                Leg = (NamedRendezvousOps.Leg == RdvLeg.Arrived) ? ApproachLeg.Arrived : ApproachLeg.Phasing;
                Note = "NAMED-BURN " + NamedRendezvousOps.Leg + " - " + NamedRendezvousOps.Note;
                return;
            }

            CwState st = BuildState(out AlongTrackM);
            double ourSma = (ship.orbit != null) ? ship.orbit.semiMajorAxis : 0.0;
            double stnSma = (Station.orbit != null) ? Station.orbit.semiMajorAxis : 0.0;
            Leg = Approach.LegFor(RangeM, AlongTrackM, GoalRangeM, ourSma, stnSma);

            // ================================================================================
            //  F9I'S LIVE RENDEZVOUS IS `StCloseIn` (station_ops.ks:855) AND IT HAS THREE LEGS.
            //
            //  ⚠ THIS BANNER USED TO CLAIM THERE WAS ONLY ONE, AND THAT WAS MY ERROR. It read
            //  "arrived, or inside the gate and flying it directly, or STOPPED. There is no fourth
            //  branch, and that is the whole point." I had conflated two different deletions:
            //
            //      StMatchStationOrbit  DELETED after flight 011, and its own source says so in
            //                           capitals at station_ops.ks:1951. Circularising at apoapsis
            //                           to match altitude OVERSHOT - SMA 683.06 -> 691.61 km
            //                           against a station at 686.75. Dead, and it stays dead.
            //
            //      StPhaseLeg           ALIVE. Called from StCloseIn:1737, inside a bounded loop.
            //                           Nothing was ever wrong with it.
            //
            //  The cost of the confusion was a real one: on the 2026-08-11 13:44 flight the capsule
            //  sat 10.2 km out RECEDING at 116 m/s, and the only answer this file had was STOPPED.
            //  There was nothing wrong with the vehicle and nothing wrong with the gate - the leg
            //  that closes a gap that size had been marked dead by mistake.
            //
            //  What was genuinely true, and still is: a ladder that plans nodes in a per-tick
            //  classifier re-fires forever. Ours did - 28 orbit-match burns, SMA error growing
            //  593 -> 12 986 m, 11.7 hours of warp requested in a twenty-minute session. F9I's
            //  planner lost 60 days on flight 020, 89 on 015, 13 on 014. So the phasing loop below
            //  is BOUNDED: `Approach.PhaseMaxPass` laps, then it closes in from wherever it got to,
            //  exactly as F9I does. The bound is what makes a loop safe, not the absence of one.
            //
            //  The three legs, in F9I's order:
            //      1. PHASING     until the along-track gap is under `Approach.PhaseMinM`
            //      2. INTERCEPT   ride an existing close pass and match velocity there, rather than
            //                     buying a transfer we already have
            //      3. DIRECT      inside the gate, `DirectApproachOps`
            // ================================================================================

            // ---- HAND THE INSIDE-GATE APPROACH TO THE L-PROFILE (RSS, off by default). ----
            // DirectApproach closes the kilometre-scale gap straight in; at the L-approach initiation
            // envelope the RSS build swaps to the R-bar/V-bar waypoint profile for the terminal run.
            // Stock and disabled builds never take this branch - DirectApproach flies to its 200 m goal
            // and hands to the docking exactly as before. Enabling it is the first RSS validation flight.
            if (WaypointApproachOps.Enabled && RssHere()
                && RangeM <= WaypointApproachOps.EnvelopeM && RangeM > GoalRangeM)
            {
                if (DirectApproachOps.Engaged) DirectApproachOps.Disengage("handing to the L-approach");
                if (WaypointApproachOps.Engage(ship, Station))
                {
                    WaypointApproachOps.Tick();
                    Note = "L-APPROACH - " + WaypointApproachOps.Note;
                    return;
                }
            }

            if (DirectApproachOps.Engaged)
            {
                DirectApproachOps.Tick();
                Note = "DIRECT - " + DirectApproachOps.Note;
                if (DirectApproachOps.Complete) Arrived();
                // Thrown back outside the gate: the approach refuses on its own and we fall through
                // to the report below on the next tick.
                else if (DirectApproachOps.Phase == DirectPhase.Refused) Halt("pushed outside the gate");
                return;
            }

            if (Leg == ApproachLeg.Arrived) { Arrived(); return; }

            // ⚠ DISTANCE AND NOTHING ELSE. The previous gate also demanded `Leg != MatchOrbit`,
            // which is exactly the leg the vehicle was in at 4.4 km - so the branch that exists for
            // this range could never run at that range. F9I gates on `target:distance < stDirectMax`
            // "and nowhere else".
            if (DirectApproach.InsideGate(RangeM))
            {
                reachedGate = true;   // from here on, ride intercepts rather than phase out again
                if (DirectApproachOps.Engage(ship, Station)) { Note = "DIRECT APPROACH"; return; }
                Halt(DirectApproachOps.Note);
                return;
            }

            // ---- A NODE IN FLIGHT BELONGS TO THE LEG THAT PLANNED IT. ----
            // Route the tick back to that leg. Without this the first leg to test `NodeExecutor.Active`
            // grabs whatever node is flying, so a MatchAltitude warp got handled as "phasing" and, worse,
            // blocked RideIntercept for the whole coast (flight_0820_221434). See `nodeOwner`.
            if (NodeExecutor.Active)
            {
                switch (nodeOwner)
                {
                    case ApproachLeg.MatchOrbit: MatchAltitude(); return;
                    case ApproachLeg.Intercept:  RideIntercept(); return;
                    default:                     Leg = ApproachLeg.Phasing; FlyPhasing(); return;
                }
            }

            // A phasing COAST (not a burn) in flight is seen through by FlyPhasing, which rides a pass
            // mid-coast on its own - checking the gap again here would re-plan a gap it is already closing.
            if (phaseReturnUt > 0.0) { Leg = ApproachLeg.Phasing; FlyPhasing(); return; }

            // ---- FREE INTERCEPT FIRST - before circularising OR phasing. ----
            // ⛔ 2026-08-20 (sim-validated, docs/rendezvous_sim/). The drift usually delivers a close
            // pass on its own, and matching velocity there costs tens of m/s where phasing costs hundreds:
            // flight_0820 ran the tank toward dry phasing an oscillating gap when a ~22 m/s match at the
            // 13 km pass was sitting there. Riding it BEFORE MatchAltitude is what stops a circularise
            // warp from committing the whole coast to the wrong thing. It also co-orbits us in one burn -
            // a velocity match IS getting onto the station's orbit - and collapses the relative-motion
            // football, so any phasing that still runs works a clean gap, not the oscillating `Ry`.
            // Only when no pass is on offer (RideIntercept returns false past `CaUseMaxM`) do we
            // circularise and then phase.
            Leg = ApproachLeg.Intercept;
            if (RideIntercept()) return;

            // ---- LEG 0: no pass on offer - get onto the station's orbit before phasing in it. ----
            if (MatchAltitude()) return;

            // ---- LEG 1: PHASING, BOUNDED. Reached only when no free intercept is on offer. ----
            if (!reachedGate
                && Math.Abs(AlongTrackM) > Approach.PhaseMinM && phasePass < Approach.PhaseMaxPass)
            {
                Leg = ApproachLeg.Phasing;
                FlyPhasing();
                return;
            }

            if (Math.Abs(AlongTrackM) > Approach.PhaseMinM && !phaseCapReported)
            {
                phaseCapReported = true;
                Debug.Log(Tag + Approach.PhaseMaxPass + " phasing laps used and the gap is still "
                          + (Math.Abs(AlongTrackM) / 1000.0).ToString("F1")
                          + " km - closing in from here anyway.");
            }

            // ---- NOTHING LEFT TO TRY FROM HERE. ----
            Halt((RangeM / 1000.0).ToString("F1") + " km out, along-track gap "
                 + (AlongTrackM / 1000.0).ToString("F1") + " km, and no intercept inside "
                 + (Approach.CaUseMaxM / 1000.0).ToString("F0") + " km");
        }

        /// <summary>
        /// LEG 0. Circularise WHERE OUR ORBIT CROSSES THE STATION'S RADIUS.
        ///
        /// ---- ⛔ THIS IS NOT `StMatchStationOrbit`, AND THE DIFFERENCE IS THE WHOLE POINT. ----
        /// The burn flight 011 deleted circularised at OUR APOAPSIS. Our apoapsis is not the
        /// station's radius, so "circularise here" lands in the wrong orbit BY CONSTRUCTION - it
        /// went to SMA 691.61 km against a station at 686.75 and was rightly killed.
        ///
        /// This burns where our radius EQUALS the station's. There, `sqrt(mu/r)` is the station's own
        /// orbital speed, so a correct burn cannot land anywhere else. It cannot overshoot the way
        /// flight 011 did, because the target speed is measured at the target radius rather than
        /// assumed to be wherever we happened to be.
        ///
        /// ---- WHY IT HAS TO EXIST AT ALL: PHASING ALONE DOES NOT CONVERGE FROM A WRONG ORBIT ----
        /// `Phasing.ExitDvMps` circularises at whatever radius the coast returns us to. If that is
        /// not the station's radius we end circular at the WRONG ALTITUDE and immediately drift
        /// again. Simulated against the orbit the 2026-08-11 flight was actually in - 182 x 81 km
        /// against a station at 86.3 - the exit leaves a 411 s period error and 933 km of fresh
        /// along-track drift PER LAP. Three bounded laps could never close that, and the crew would
        /// watch it fail three times and stop. The gap was never the problem; the orbit was.
        ///
        /// Cost, simulated: 69.0 m/s and about 89 units of monopropellant from 182 x 81, 2.8 m/s and
        /// 3.7 units from 90 x 86, nothing at all when already co-orbital. Expensive from a bad
        /// orbit, which is the honest price of arriving in one, and paid ONCE rather than per lap.
        ///
        /// ⚠ IT REFUSES RATHER THAN GUESSES when our orbit does not reach the station's radius at
        /// all - an orbit entirely below it needs a real transfer, and inventing one here is how the
        /// deleted burn came to exist. Returns true when it took the tick.
        /// </summary>
        private static bool MatchAltitude()
        {
            if (NodeExecutor.Active)
            {
                Note = "ALTITUDE MATCH - " + NodeExecutor.Phase + " " + NodeExecutor.Note;
                return true;
            }

            Orbit mo = ship.orbit, so = Station.orbit;
            if (mo == null || so == null) return false;
            CelestialBody b = ship.mainBody;
            if (b == null) return false;

            // The station's own radius. Near-circular by measurement, so its SMA is that radius.
            double rStn = so.semiMajorAxis;
            double rNow = (ship.CoM - b.position).magnitude;

            // Close enough already? Then this leg has nothing to do, now or ever this engagement.
            if (Math.Abs(rNow - rStn) <= Approach.CoOrbitalTolM
                && Math.Abs(mo.semiMajorAxis - rStn) <= Approach.CoOrbitalTolM)
                return false;

            // ---- BOUND, LIKE THE PHASING LAPS. A per-tick classifier that re-plans forever ate a
            // twenty-minute session once (the 28-burn bug). A Hohmann is two burns - raise then
            // circularise - so three is margin, after which we phase from wherever we reached.
            if (altMatchBurns >= AltMatchMaxBurns)
            {
                if (!altCapReported)
                {
                    altCapReported = true;
                    Debug.LogWarning(Tag + AltMatchMaxBurns + " altitude-match burns used and still "
                        + (Math.Abs((ship.CoM - b.position).magnitude - rStn) / 1000.0).ToString("F1")
                        + " km off the station's radius - phasing from here.");
                }
                return false;
            }

            double now = Planetarium.GetUniversalTime();
            if (now - lastBurnAt < 30.0) { Hold(); Note = "ALTITUDE MATCH - settling"; return true; }

            double rAp = b.Radius + mo.ApA, rPe = b.Radius + mo.PeA;
            double mu = b.gravParameter;

            // ---- ⛔ THE STATION IS ABOVE OUR APOAPSIS: RAISE TO IT (Crew Dragon inserts LOW). ----
            // Real Crew-2 went into a ~190 km orbit and climbed to the ISS at ~420 km. So when the
            // station sits above our whole orbit we do the FIRST Hohmann burn now - prograde at
            // periapsis to lift apoapsis onto the station's radius - and the circularise branch below
            // finishes it at the new apoapsis on a later tick. This replaces the old "refuse, needs a
            // transfer" dead-end that stranded the rendezvous (flight_0822_221316: 200x173 vs 420 km).
            if (rStn > rAp + Approach.CoOrbitalTolM)
            {
                double dvMag = Hohmann.RaiseOppositeApsisDv(rPe, mo.semiMajorAxis, rStn, mu);
                double at = now + mo.timeToPe;
                if (Math.Abs(dvMag) > Approach.MaxDvMps) { altCapReported = true; return LogAltCap(dvMag); }
                Vector3d dv = WorldVelAt(mo, at).normalized * dvMag;
                nodeOwner = ApproachLeg.MatchOrbit;
                if (NodeExecutor.Begin(ship, dv, at, "raise to station altitude"))
                {
                    lastBurnAt = now; altMatchBurns++; LastDvMps = Math.Abs(dvMag);
                    Debug.Log(Tag + "altitude match: raising apoapsis " + mo.ApA.ToString("F0")
                        + " m -> the station's " + (rStn - b.Radius).ToString("F0") + " m at periapsis for "
                        + dvMag.ToString("F1") + " m/s (Hohmann burn 1). Circularise follows at apoapsis.");
                    return true;
                }
                Note = NodeExecutor.Note; return false;
            }

            // ---- THE STATION IS BELOW OUR PERIAPSIS: LOWER TO IT (retrograde at apoapsis). ----
            if (rStn < rPe - Approach.CoOrbitalTolM)
            {
                double dvMag = Hohmann.RaiseOppositeApsisDv(rAp, mo.semiMajorAxis, rStn, mu);   // negative
                double at = now + mo.timeToAp;
                if (Math.Abs(dvMag) > Approach.MaxDvMps) { altCapReported = true; return LogAltCap(dvMag); }
                Vector3d dv = WorldVelAt(mo, at).normalized * dvMag;
                nodeOwner = ApproachLeg.MatchOrbit;
                if (NodeExecutor.Begin(ship, dv, at, "lower to station altitude"))
                {
                    lastBurnAt = now; altMatchBurns++; LastDvMps = Math.Abs(dvMag);
                    Debug.Log(Tag + "altitude match: lowering periapsis " + mo.PeA.ToString("F0")
                        + " m -> the station's " + (rStn - b.Radius).ToString("F0") + " m at apoapsis for "
                        + dvMag.ToString("F1") + " m/s (Hohmann burn 1). Circularise follows at periapsis.");
                    return true;
                }
                Note = NodeExecutor.Note; return false;
            }

            // ---- THE STATION IS WITHIN OUR ORBIT: circularise where our radius equals its. ----
            // After a raise/lower this is the SECOND Hohmann burn (station ~ our apoapsis/periapsis).
            // If it is at our apsis, circularise there directly - TrueAnomalyAtRadius throws at the apsis.
            if (rStn >= rAp - Approach.CoOrbitalTolM)
                return CirculariseAtApsis(mo, b, rStn, now, now + mo.timeToAp, "apoapsis");
            if (rStn <= rPe + Approach.CoOrbitalTolM)
                return CirculariseAtApsis(mo, b, rStn, now, now + mo.timeToPe, "periapsis");

            double ta;
            try { ta = mo.TrueAnomalyAtRadius(rStn); }
            catch (Exception) { altMatchBurns = AltMatchMaxBurns; return false; }

            // Two crossings per orbit, at +/-ta. Take whichever comes first.
            double t1 = mo.GetUTforTrueAnomaly(ta, 0.0);
            double t2 = mo.GetUTforTrueAnomaly(-ta, 0.0);
            while (t1 < now + Approach.LeadS) t1 += mo.period;
            while (t2 < now + Approach.LeadS) t2 += mo.period;
            double atCross = (t1 < t2) ? t1 : t2;

            // ---- ⛔ HAND THE WHOLE NODE TO THE EXECUTOR NOW (user, 2026-08-19), as the intercept does.
            // At the crossing our radius IS the station's, so circularising there drops us into its
            // orbit. The Δv is the velocity AT the crossing from the CURRENT orbit, unchanged while we
            // coast, so it is exact to compute up front - and the executor then warps to 10 min out,
            // orients (on reaction wheels in stock, on RCS in RO where there are none - NodeExecutor),
            // warps to the node and burns, never holding attitude across the long coast to the crossing.
            Vector3d vHere = WorldVelAt(mo, atCross);
            double dvCross = Math.Sqrt(b.gravParameter / rStn) - vHere.magnitude;
            if (Math.Abs(dvCross) > Approach.MaxDvMps) return LogAltCap(dvCross);
            Vector3d dvC = vHere.normalized * dvCross;
            nodeOwner = ApproachLeg.MatchOrbit;
            if (NodeExecutor.Begin(ship, dvC, atCross, "altitude match"))
            {
                altMatchBurns++;
                LastDvMps = Math.Abs(dvCross);
                Debug.Log(Tag + "altitude match - our " + (mo.ApA / 1000.0).ToString("F1") + " x "
                          + (mo.PeA / 1000.0).ToString("F1") + " km crosses the station's "
                          + ((rStn - b.Radius) / 1000.0).ToString("F1") + " km in "
                          + (atCross - now).ToString("F0") + " s; circularising there for "
                          + dvCross.ToString("F2") + " m/s. Executor warps, orients, then burns.");
                return true;
            }
            Note = NodeExecutor.Note;
            return false;
        }

        /// <summary>Circularise directly at an apsis (radius rApsis) reached at time `at`. Used for the
        /// SECOND Hohmann burn, where the station sits at our apoapsis/periapsis and TrueAnomalyAtRadius
        /// would throw. Signed dv along the velocity there.</summary>
        private static bool CirculariseAtApsis(Orbit mo, CelestialBody b, double rStn, double now,
                                               double at, string which)
        {
            double dvMag = Hohmann.CirculariseDv(rStn, mo.semiMajorAxis, b.gravParameter);
            if (Math.Abs(dvMag) > Approach.MaxDvMps) return LogAltCap(dvMag);
            Vector3d dv = WorldVelAt(mo, at).normalized * dvMag;
            nodeOwner = ApproachLeg.MatchOrbit;
            if (NodeExecutor.Begin(ship, dv, at, "circularise at station altitude"))
            {
                lastBurnAt = now; altMatchBurns++; LastDvMps = Math.Abs(dvMag);
                Debug.Log(Tag + "altitude match: circularising at " + which + " ("
                          + ((rStn - b.Radius) / 1000.0).ToString("F1") + " km) for "
                          + dvMag.ToString("F1") + " m/s (Hohmann burn 2) - onto the station's orbit.");
                return true;
            }
            Note = NodeExecutor.Note; return false;
        }

        private static bool LogAltCap(double dvMag)
        {
            altMatchBurns = AltMatchMaxBurns;   // stop trying; phase from here
            Debug.LogWarning(Tag + "altitude match wants " + dvMag.ToString("F1") + " m/s - over the "
                             + Approach.MaxDvMps.ToString("F0") + " m/s cap. Phasing from here instead.");
            return false;
        }

        /// <summary>Altitude-match burns flown this engagement (Hohmann is two), bounded by AltMatchMaxBurns.</summary>
        private static int altMatchBurns;
        private static bool altCapReported;
        private const int AltMatchMaxBurns = 3;

        /// <summary>
        /// LEG 2. If the orbits already bring us close, warp to that pass and match velocity there.
        ///
        /// ⚠ CHEAPER THAN BUYING A TRANSFER, AND F9I SAYS SO IN THOSE WORDS: "riding the existing
        /// intercept - closest approach N m in T s. Matching velocity there rather than buying a
        /// transfer." After the phasing laps the two orbits usually already cross close; planning a
        /// CW leg at that point spends propellant to arrange something we have for free.
        ///
        /// Returns true when it took the tick. False means there is no intercept worth riding.
        /// </summary>
        private static bool RideIntercept()
        {
            if (NodeExecutor.Active)
            {
                Note = "INTERCEPT - " + NodeExecutor.Phase + " " + NodeExecutor.Note;
                return true;
            }

            double now = Planetarium.GetUniversalTime();
            Orbit so = Station.orbit, mo = ship.orbit;
            if (so == null || mo == null || so.period <= 0.0) return false;

            // Look for a close pass. Coarse-to-fine over `CaSpanPeriods` of the station's orbit.
            interceptShip = mo; interceptStation = so; interceptFrom = now;
            Predict.Approach ca = Predict.ClosestApproach(
                new Func<double, double>(SeparationAt),
                so.period * Approach.CaSpanPeriods, Approach.CaSteps, Approach.CaRefine);
            if (!ca.Valid) return false;

            // Too far to be worth riding, or so close there is nothing left to match.
            if (ca.DistanceM > Approach.CaUseMaxM || ca.DistanceM <= Approach.MatchDistM)
                return false;

            // ---- ⛔ HAND THE WHOLE NODE TO THE EXECUTOR NOW (user, 2026-08-19). ----
            // "set the intercept exactly as you have been BUT then warp to within 10 minutes of the
            // node and only then point the right way for the burn." The velocity to kill is the
            // relative velocity AT the pass, computed from the CURRENT orbits - and nothing burns
            // before the pass, so those orbits (and this Δv) do not change while we coast. Handing it
            // over up front is therefore exact, not a guess, and it lets the executor warp the coast
            // BEFORE it orients, so no attitude is held across the coast to nudge the plotted intercept.
            // The turn itself is flown on reaction wheels in stock, or on RCS in RO (no wheels) - and
            // because the coast before it is warped on rails, that RCS costs only the turn. The executor
            // warps to the orient point and then to ignition, both strictly before closest approach,
            // so it never warps PAST the pass.
            double at = now + ca.TimeS;
            Vector3d dv = WorldVelAt(so, at) - WorldVelAt(mo, at);
            double mag = dv.magnitude;
            if (mag < Approach.MatchVelMps)
            {
                Debug.Log(Tag + "intercept - already matched to " + mag.ToString("F2") + " m/s");
                return false;
            }
            if (mag > Approach.MaxDvMps)
            {
                Debug.LogWarning(Tag + "intercept match wants " + mag.ToString("F1")
                                 + " m/s - over the " + Approach.MaxDvMps.ToString("F0")
                                 + " m/s cap, not flying it");
                return false;
            }
            // NodeExecutor checks the periapsis floor itself before it turns or lights anything.
            nodeOwner = ApproachLeg.Intercept;
            if (NodeExecutor.Begin(ship, dv, at, "closest-approach velocity match"))
            {
                LastDvMps = mag;
                Debug.Log(Tag + "riding the existing intercept - closest approach "
                          + ca.DistanceM.ToString("F0") + " m in " + ca.TimeS.ToString("F0")
                          + " s, " + mag.ToString("F2") + " m/s to match. Executor warps to 10 min "
                          + "out, orients (wheels/RCS), warps to the pass, then burns.");
                return true;
            }
            Note = NodeExecutor.Note;
            return false;
        }

        // ---- ⛔ `.xzy`. KSP's orbit sampler returns the SWIZZLED frame with Y and Z exchanged. ----
        // `getPositionAtUT` is already world; `getOrbitalVelocityAtUT` is NOT, and MechJeb carries
        // `WorldOrbitalVelocityAtUT(o, ut) => o.getOrbitalVelocityAtUT(ut).xzy` (OrbitExtensions:22)
        // for exactly this. A match burn computed in the wrong frame points somewhere plausible and
        // is wrong by up to 180 degrees.
        private static Vector3d WorldVelAt(Orbit o, double ut)
        {
            return o.getOrbitalVelocityAtUT(ut).xzy;
        }

        private static Orbit interceptShip, interceptStation;
        private static double interceptFrom;

        /// <summary>
        /// Separation at `t` seconds from the scan's start. A named method because C# 5 has no
        /// lambdas here and `Predict.ClosestApproach` wants a delegate.
        /// </summary>
        private static double SeparationAt(double t)
        {
            double ut = interceptFrom + t;
            return (interceptStation.getPositionAtUT(ut) - interceptShip.getPositionAtUT(ut)).magnitude;
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

        /// <summary>RSS body test, matching AutoPilot's `RssBody` gate (Earth's radius dwarfs Kerbin's).
        /// The L-approach is an RSS-only fidelity path; stock keeps the straight-in DirectApproach.</summary>
        private static bool RssHere()
        {
            return Station != null && Station.mainBody != null && Station.mainBody.Radius > 1.0e6;
        }

        private static CwState BuildState(out double alongTrack)
        {
            CwState s = new CwState();
            alongTrack = 0.0;

            CelestialBody b = Station.mainBody;
            Vector3d rt = Station.CoM - b.position;
            Vector3d vt = Station.obt_velocity;
            Vector3d rs = ship.CoM - b.position;
            Vector3d vs = ship.obt_velocity;

            Vector3d xh = rt.normalized;
            Vector3d yh = Vector3d.Exclude(xh, vt).normalized;
            Vector3d zh = Vector3d.Cross(xh, yh).normalized;

            Vector3d dr = rs - rt;
            Vector3d dv = vs - vt;

            s.Rx = Vector3d.Dot(dr, xh); s.Ry = Vector3d.Dot(dr, yh); s.Rz = Vector3d.Dot(dr, zh);
            s.Vx = Vector3d.Dot(dv, xh); s.Vy = Vector3d.Dot(dv, yh); s.Vz = Vector3d.Dot(dv, zh);

            double period = (Station.orbit != null) ? Station.orbit.period : 0.0;
            s.N = (period > 0.0) ? 2.0 * Math.PI / period : 0.0;

            // Along-track is the y component: negative means we are BEHIND, which is where we want
            // to be. The ladder only cares about the magnitude of the gap.
            alongTrack = s.Ry;
            return s;
        }

        // ------------------------------------------------------------------ the legs

        /// <summary>
        /// MATCH ORBIT. Circularise at OUR apoapsis, which both rounds the orbit off and matches the
        /// station's altitude in one burn - the ascent already put apoapsis within a few hundred
        /// metres of the station's radius, so anywhere else would take a Hohmann.
        /// </summary>
        // =====================================================================================
        //  ⛔ EVERYTHING BELOW THIS LINE IS DEAD BY DECISION - EXCEPT `FlyPhasing`, WHICH IS LIVE.
        //
        //  ⚠ `FlyPhasing` WAS LISTED HERE AND SHOULD NOT HAVE BEEN. It is called from the live
        //  path above, as leg 1, because F9I calls `StPhaseLeg` from `StCloseIn:1737`. It is kept
        //  physically below this line only because moving it would obscure the diff; the banner is
        //  the authority, not the position in the file. See the correction at the top of Tick.
        //
        //  `FlyMatchOrbit`, `FlyCw` and `FlyTerminal` are faithful ports of laws F9I itself stopped
        //  using. Its own source marks the first of them the same way:
        //
        //      station_ops.ks:1951
        //      ---- DEAD BY DECISION: NOTHING CALLS StMatchStationOrbit, AND NOTHING SHOULD. ----
        //
        //  They are kept, not deleted, for one reason: the arithmetic was read out of the source and
        //  is worth not losing if a mission ever needs it - a launch into the WRONG orbit, or a
        //  station somewhere other than the one we ferry to. What must not happen is one of them
        //  being quietly reconnected because the name reads like something the mission needs.
        //
        //  What it cost when they WERE connected, on 2026-08-11: 28 orbit-match burns, semi-major
        //  axis error growing 593 -> 12 986 m, 11.7 hours of warp requested inside a twenty-minute
        //  session, and an abandoned flight. F9I's equivalent numbers for the phasing planner are
        //  60 days (flight 020), 89 days (015) and 13 days (014).
        //
        //  ⚠ IF YOU RECONNECT ANY OF THESE, THEY NEED THE THING THEY NEVER HAD: a bound. An
        //  attempt count, a Δv budget, and a convergence test that fails loudly. That is exactly
        //  what leg 1 above now has - `Approach.PhaseMaxPass` laps and then a documented
        //  fall-through - and it is the difference between a loop that converges and one that ate
        //  a twenty-minute session.
        // =====================================================================================

        private static void FlyMatchOrbit()
        {
            if (NodeExecutor.Active)
            {
                Note = "ORBIT MATCH - " + NodeExecutor.Phase + " " + NodeExecutor.Note;
                return;
            }

            double now = Planetarium.GetUniversalTime();
            if (now - lastBurnAt < 30.0) { Hold(); Note = "ORBIT MATCH - settling"; return; }

            CelestialBody b = ship.mainBody;
            Orbit o = ship.orbit;
            if (o == null) { Hold(); Note = "ORBIT MATCH - no orbit"; return; }

            double ra = b.Radius + o.ApA;
            double dv = OrbitMatch.CirculariseAtApoapsisDv(ra, o.semiMajorAxis, b.gravParameter);
            double burnUt = now + o.timeToAp;

            // Prograde at apoapsis. The Δv direction is the velocity direction AT THE NODE, not now -
            // half an orbit away those differ by 180 degrees, and using the current one would burn
            // exactly backwards.
            // ---- ⛔ `.xzy` IS NOT DECORATION. WITHOUT IT THIS BURNS IN THE WRONG DIRECTION. ----
            // KSP's `getOrbitalVelocityAtUT` returns the SWIZZLED orbit frame with Y and Z exchanged;
            // everything else here - `obt_velocity`, `CoM`, the attitude controller - is world. MechJeb
            // carries `WorldOrbitalVelocityAtUT(o, ut) => o.getOrbitalVelocityAtUT(ut).xzy`
            // (OrbitExtensions.cs:22) for exactly this. This file's own header warned about the trap
            // and the code did it anyway: "a frame error here looks identical to a tuning problem right
            // up until the capsule burns the wrong way."
            Vector3d velAtAp = o.getOrbitalVelocityAtUT(burnUt).xzy;
            if (velAtAp.sqrMagnitude < 1.0) { Hold(); Note = "ORBIT MATCH - no velocity"; return; }

            if (NodeExecutor.Begin(ship, velAtAp.normalized * dv, burnUt, "orbit match"))
            {
                lastBurnAt = now;
                Debug.Log(Tag + "orbit match: SMA off by "
                          + Math.Abs(o.semiMajorAxis - Station.orbit.semiMajorAxis).ToString("F0")
                          + " m, circularising at apoapsis for " + dv.ToString("F2") + " m/s");
            }
            else Note = NodeExecutor.Note;
        }

        /// <summary>
        /// PHASING. Change our PERIOD and let the geometry close the gap.
        ///
        /// F9I's simulated table: a 51 km gap costs 17.7 m/s over one lap against 289 m/s for a
        /// direct CW transfer - and the CW version puts periapsis 15.6 km below the surface doing it.
        /// Ahead of the station means a LONGER period, i.e. a HIGHER orbit, which is why this can
        /// never drop us into the atmosphere the way flight 012's pursuit controller did.
        /// </summary>
        private static void FlyPhasing()
        {
            if (NodeExecutor.Active)
            {
                Note = "PHASING - " + NodeExecutor.Phase + " " + NodeExecutor.Note;
                return;
            }

            double now = Planetarium.GetUniversalTime();

            // Coasting the phasing orbit: nothing to do but wait for the return to this point.
            if (now < phaseReturnUt)
            {
                // ---- ⛔ RIDE A CLOSE PASS RATHER THAN WARP PAST IT (user, 2026-08-20). ----
                // The lap's circularisation point (phaseReturnUt) is NOT the closest approach, and
                // the dispatch's `lapInFlight` guard sends every mid-lap tick straight here and skips
                // LEG 2 - so the coast warped at 100x through passes of 4.1 and 3.5 km on flight_0820
                // and only dropped out at 6.8 km and OPENING. "You are also warping past the closest
                // approach point." If the current orbits already give a ridable pass, hand it to the
                // executor NOW: it warps to 10 min out, orients (wheels in stock, RCS in RO), warps to the pass and stops
                // BEFORE it (WarpToOrient/WarpToIgnition target strictly pre-approach), then matches
                // velocity AT the pass. RideIntercept's own gates (CaUseMaxM / MatchDistM / MaxDvMps)
                // decide whether a pass is worth riding - the same ones the post-phasing leg uses - so
                // this only ever supersedes the lap with something at least as good, never worse.
                if (!NodeExecutor.Active && RideIntercept())
                {
                    phaseReturnUt = 0.0;   // abandon the lap; we are matching at the pass instead
                    Leg = ApproachLeg.Intercept;
                    return;
                }
                Hold();
                double left = phaseReturnUt - now;
                Note = "PHASING - circularise in " + left.ToString("F0") + " s";
                Warp(left);
                return;
            }

            // Back at the burn point after the coast: circularise into the station's orbit.
            if (phaseReturnUt > 0.0)
            {
                phaseReturnUt = 0.0;
                CelestialBody bx = ship.mainBody;
                double rx = (ship.CoM - bx.position).magnitude;
                double dvx = Phasing.ExitDvMps(rx, ship.obt_velocity.magnitude, bx.gravParameter);
                Vector3d dirx = ship.obt_velocity.normalized * dvx;
                nodeOwner = ApproachLeg.Phasing;
                if (NodeExecutor.Begin(ship, dirx, now, "phasing exit")) return;
                Note = NodeExecutor.Note;
                return;
            }

            if (now - lastBurnAt < 30.0) { Hold(); Note = "PHASING - settling"; return; }

            CelestialBody b = ship.mainBody;
            PhasingInputs p = new PhasingInputs();
            p.GapM = AlongTrackM;
            p.RadiusM = (ship.CoM - b.position).magnitude;
            p.SpeedMps = ship.obt_velocity.magnitude;
            p.StationPeriodS = (Station.orbit != null) ? Station.orbit.period : 0.0;
            p.StationSmaM = (Station.orbit != null) ? Station.orbit.semiMajorAxis : 0.0;
            p.Mu = b.gravParameter;
            p.Orbits = Approach.PhaseOrbits;
            // F9I's `stPeriFloor`, at last connected to the thing that plans the burn. The solver
            // clears this by 5 km, so `NodeExecutor.PeriapsisSafe`'s atmosphere test never has to
            // catch a phasing entry - and if it ever does, that is a real bug rather than a
            // disagreement between two floors.
            // ---- ⛔ THE FLOOR IS THE ATMOSPHERE, NOT A KERBIN NUMBER. ----
            // A flat 75 km (Rendezvous.PeriapsisFloorM) is 5 km above Kerbin's 70 km atmosphere - but
            // it is 65 km INSIDE Earth's 140 km atmosphere, so in RSS a phasing periapsis "cleared" at
            // 75 km would re-enter. Take the floor from the body's OWN atmosphere depth: Kerbin stays
            // exactly 75 km (70 + margin), Earth becomes ~145 km. Body-agnostic, stock-transparent.
            p.PeriFloorRadiusM = b.Radius + b.atmosphereDepth + Rendezvous.PeriapsisMarginM;

            int laps;
            PhasingSolution sol = Phasing.SolveAdaptive(p, out laps);
            if (!sol.Ok)
            {
                // ---- ⛔ A REFUSAL THAT REPEATS IDENTICALLY IS A DEADLOCK, NOT A GUARD. ----
                // 4515 identical refusals in 102 s on 2026-08-13, none of them on the glass, while
                // the crew re-engaged four times and then gave up. If more laps cannot fix it, the
                // phasing cannot fix it, and the crew needs to be told that once and clearly.
                Hold();
                Note = "PHASING REFUSED - " + sol.Note;
                if (!phaseRefusalLogged)
                {
                    phaseRefusalLogged = true;
                    Debug.LogWarning(Tag + Note + " - tried up to " + Phasing.MaxLaps
                                     + " laps. The approach is holding; it will not retry silently.");
                }
                return;
            }
            phaseRefusalLogged = false;
            p.Orbits = laps;                       // DirectionSane must see the flown solution

            // ⚠ THE DIRECTION CHECK IS NOT DECORATION. Flight 014's bad semi-major axis produced a
            // burn of roughly the right SIZE pointing the wrong WAY - 1353 m/s retrograde, periapsis
            // -539.9 km. A solution that would lower the orbit to catch something ahead of us is
            // wrong however plausible its magnitude.
            if (!Phasing.DirectionSane(p, sol))
            {
                Hold();
                Note = "PHASING REFUSED - the solution would move the orbit the wrong way";
                Debug.LogWarning(Tag + Note + " (gap " + p.GapM.ToString("F0") + " m, a_phase "
                                 + sol.PhaseSmaM.ToString("F0") + " vs a_stn "
                                 + p.StationSmaM.ToString("F0") + ")");
                return;
            }

            // Prograde/retrograde at this point, so it becomes an apsis and we return to exactly here.
            Vector3d dv = ship.obt_velocity.normalized * sol.EntryDvMps;
            nodeOwner = ApproachLeg.Phasing;
            if (NodeExecutor.Begin(ship, dv, now, "phasing entry"))
            {
                lastBurnAt = now;
                phaseReturnUt = now + sol.CoastS;
                phasePass++;
                Debug.Log(Tag + "phasing lap " + phasePass + " of " + Approach.PhaseMaxPass + ": "
                          + (Math.Abs(p.GapM) / 1000.0).ToString("F1") + " km "
                          + (sol.Ahead ? "AHEAD" : "BEHIND") + ", " + sol.Note
                          + (laps > Approach.PhaseOrbits
                             ? "  (spread over " + laps + " laps to stay under the dv cap)" : ""));
            }
            else Note = NodeExecutor.Note;
        }

        /// <summary>
        /// CLOHESSY-WILTSHIRE. One planned transfer, burned once, then coast and re-solve.
        ///
        /// The periapsis floor is checked BEFORE the burn is applied and the burn abandoned if it
        /// would breach it. That check is the whole lesson of flight 012 and it is not optional.
        /// </summary>
        private static void FlyCw(CwState st)
        {
            // While the executor has it, leave it alone - it owns the attitude and the throttle.
            if (NodeExecutor.Active) { Note = "CW - " + NodeExecutor.Phase + " " + NodeExecutor.Note; return; }

            double now = Planetarium.GetUniversalTime();
            if (now - lastBurnAt < 30.0) { Hold(); Note = "CW - coasting the transfer"; return; }

            double period = (Station.orbit != null) ? Station.orbit.period : 0.0;
            if (period <= 0.0) { Hold(); Note = "CW - no station period"; return; }

            double tof;
            CwSolution sol = CwTargeting.Best(st, period * 0.05, period * 0.5, 40,
                                              Approach.CwAimBehindM, out tof);
            if (!sol.Ok) { Hold(); Note = "CW - " + sol.Note; return; }

            // Back to world. The basis has to be rebuilt identically to BuildState's.
            CelestialBody b = Station.mainBody;
            Vector3d xh = (Station.CoM - b.position).normalized;
            Vector3d yh = Vector3d.Exclude(xh, Station.obt_velocity).normalized;
            Vector3d zh = Vector3d.Cross(xh, yh).normalized;
            Vector3d dv = sol.DvX * xh + sol.DvY * yh + sol.DvZ * zh;

            LastDvMps = dv.magnitude;
            Note = "CW burn " + LastDvMps.ToString("F1") + " m/s, tof "
                 + (tof / 60.0).ToString("F1") + " min, arrive "
                 + sol.ArrivalRelSpeed.ToString("F1") + " m/s";

            // The executor checks the periapsis floor itself, before it turns or lights anything -
            // that check lives with the burn rather than with each caller, so no future caller can
            // forget it. `StNodeSafe` is "the guard flight 012 did not have".
            if (NodeExecutor.Begin(ship, dv, now, "CW transfer")) lastBurnAt = now;
            else Note = NodeExecutor.Note;
        }

        /// <summary>TERMINAL. RCS only, straight line, on the speed ladder with a deadband.</summary>
        private static void FlyTerminal()
        {
            double elapsed = Planetarium.GetUniversalTime() - startedAt;
            TerminalCommand c = Approach.Terminal(RangeM, ClosingMps, LateralMps,
                                                  GoalRangeM, elapsed);
            Note = c.Note + "  want " + c.WantClosingMps.ToString("F1") + " m/s";

            if (c.Coast)
            {
                // Stop steering AND stop pushing. F9I: the old loop re-locked steering every 0.1 s
                // and spent 38 units of mono on attitude alone. Coasting is free.
                AttitudeController.Ascent.Release(ship);
                Translate(0.0);
                return;
            }

            Vector3d rel = Station.CoM - ship.CoM;
            Vector3d relVel = Station.obt_velocity - ship.obt_velocity;
            Vector3d dir;

            if (c.KillLateral)
            {
                // Lateral drift has to be killed too, or we arrive ALONGSIDE the station.
                Vector3d lat = Vector3d.Exclude(rel.normalized, -relVel);
                if (lat.sqrMagnitude < 1e-6) { Translate(0.0); return; }
                dir = -lat.normalized;
            }
            else
            {
                bool tooSlow = c.WantClosingMps > ClosingMps;
                dir = tooSlow ? rel.normalized : -rel.normalized;
            }

            if (!ship.ActionGroups[KSPActionGroup.RCS])
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
            AttitudeController.Ascent.SteerTo(ship, dir, Vector3d.zero);

            // Only push once the nose is actually pointing where the thrust should go. Pushing
            // through a 40-degree error is how a correction becomes a new error.
            double off = Vector3d.Angle(ship.ReferenceTransform.up, dir);
            Translate(off < Approach.ThrustConeDeg ? 1.0 : 0.0);
        }

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

        /// <summary>
        /// Would applying this impulse put periapsis into the atmosphere?
        ///
        /// Estimated with the vis-viva energy after the burn rather than by asking KSP to build a
        /// node, so it is cheap enough to check on every candidate. Conservative: it uses the
        /// CURRENT radius as the burn point, which is exactly where the impulse is applied.
        /// </summary>
        private static bool WouldBreachFloor(Vector3d dv)
        {
            CelestialBody b = ship.mainBody;
            if (b == null) return true;

            Vector3d r = ship.CoM - b.position;
            Vector3d v = ship.obt_velocity + dv;
            double rm = r.magnitude;
            double mu = b.gravParameter;
            if (rm <= 0.0 || mu <= 0.0) return true;

            double energy = v.sqrMagnitude / 2.0 - mu / rm;
            if (energy >= 0.0) return true;                 // escaping is also a refusal
            double sma = -mu / (2.0 * energy);

            Vector3d h = Vector3d.Cross(r, v);
            double ecc = Math.Sqrt(Math.Max(0.0, 1.0 - h.sqrMagnitude / (sma * mu)));
            double peri = sma * (1.0 - ecc) - b.Radius;
            return peri < b.atmosphereDepth;
        }
    }
}
