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

        /// <summary>For the pages and the recorder.</summary>
        public static double RangeM, ClosingMps, LateralMps, AlongTrackM, LastDvMps;
        public static string Note = "-";

        private static Vessel ship;
        private static double startedAt;
        private static double lastBurnAt = -999.0;
        /// <summary>When the phasing coast returns us to the burn point. 0 = not phasing.</summary>
        private static double phaseReturnUt;
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
            startedAt = Planetarium.GetUniversalTime();
            lastBurnAt = -999.0;
            Debug.Log(Tag + "rendezvous ENGAGED - target '" + Station.vesselName + "', "
                          + (Vector3d.Distance(v.CoM, Station.CoM) / 1000.0).ToString("F1") + " km");
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
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
            Engaged = false; Station = null; ship = null;
            phaseReturnUt = 0.0;
            RangeM = 0.0; ClosingMps = 0.0; LateralMps = 0.0; AlongTrackM = 0.0; LastDvMps = 0.0;
            Note = "-";
        }

        /// <summary>Public so the LAUNCH WINDOW can find the station before liftoff.</summary>
        public static Vessel Find()
        {
            List<Vessel> all = FlightGlobals.Vessels;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].vesselName == StationName) return all[i];
            return null;
        }

        // ------------------------------------------------------------------ the loop

        public static void Tick()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (!Engaged) return;

            if (ship == null || ship.state == Vessel.State.DEAD
                || Station == null || Station.state == Vessel.State.DEAD)
            {
                Disengage("vessel lost");
                return;
            }

            // ---- MEASURE ----
            Vector3d rel = Station.CoM - ship.CoM;
            RangeM = rel.magnitude;
            Vector3d relVel = Station.obt_velocity - ship.obt_velocity;
            // Closing is POSITIVE when the gap is shrinking, which is the sign the ladder expects.
            ClosingMps = Vector3d.Dot(-relVel, rel.normalized);
            LateralMps = Vector3d.Exclude(rel.normalized, -relVel).magnitude;

            CwState st = BuildState(out AlongTrackM);
            double ourSma = (ship.orbit != null) ? ship.orbit.semiMajorAxis : 0.0;
            double stnSma = (Station.orbit != null) ? Station.orbit.semiMajorAxis : 0.0;
            Leg = Approach.LegFor(RangeM, AlongTrackM, GoalRangeM, ourSma, stnSma);

            switch (Leg)
            {
                case ApproachLeg.Arrived: Arrived(); break;
                case ApproachLeg.Terminal: FlyTerminal(); break;
                case ApproachLeg.MatchOrbit: FlyMatchOrbit(); break;
                case ApproachLeg.Clohessy: FlyCw(st); break;
                default: FlyPhasing(); break;
            }
        }

        /// <summary>
        /// Build the CW state in the STATION's LVLH frame: x radial out, y along-track, z normal.
        ///
        /// x and y are built WITHOUT a cross product so their handedness cannot be wrong, exactly as
        /// F9I does - see CwTargeting's header for why that makes the solver handedness-proof.
        /// </summary>
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
                Hold();
                Note = "PHASING - circularise in " + (phaseReturnUt - now).ToString("F0") + " s";
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

            PhasingSolution sol = Phasing.Solve(p);
            if (!sol.Ok) { Hold(); Note = "PHASING - " + sol.Note; return; }

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
            if (NodeExecutor.Begin(ship, dv, now, "phasing entry"))
            {
                lastBurnAt = now;
                phaseReturnUt = now + sol.CoastS;
                Debug.Log(Tag + "phasing: " + (Math.Abs(p.GapM) / 1000.0).ToString("F1") + " km "
                          + (sol.Ahead ? "AHEAD" : "BEHIND") + ", " + sol.Note);
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
