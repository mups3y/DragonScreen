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
            RangeM = 0.0; ClosingMps = 0.0; LateralMps = 0.0; AlongTrackM = 0.0; LastDvMps = 0.0;
            Note = "-";
        }

        private static Vessel Find()
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
            Leg = Approach.LegFor(RangeM, AlongTrackM, GoalRangeM);

            switch (Leg)
            {
                case ApproachLeg.Arrived: Arrived(); break;
                case ApproachLeg.Terminal: FlyTerminal(); break;
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
        /// PHASING. The gap is too large for CW at any sane cost - F9I's own table puts direct CW at
        /// 289 m/s for a 51 km gap against 17.7 m/s for one phasing lap, and the CW version drops
        /// periapsis 15.6 km below the surface doing it.
        ///
        /// ⚠ NOT IMPLEMENTED AS A BURN YET. It reports and holds rather than guessing at a manoeuvre,
        /// because a phasing burn that is wrong is the exact failure mode that de-orbited flight 012.
        /// `StPhaseLeg:932` is the port site.
        /// </summary>
        private static void FlyPhasing()
        {
            Hold();
            Note = "PHASING NEEDED - gap " + (Math.Abs(AlongTrackM) / 1000.0).ToString("F1")
                 + " km. Not yet flown; see StPhaseLeg.";
        }

        /// <summary>
        /// CLOHESSY-WILTSHIRE. One planned transfer, burned once, then coast and re-solve.
        ///
        /// The periapsis floor is checked BEFORE the burn is applied and the burn abandoned if it
        /// would breach it. That check is the whole lesson of flight 012 and it is not optional.
        /// </summary>
        private static void FlyCw(CwState st)
        {
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

            // ---- ⛔ THE PERIAPSIS FLOOR. NOT OPTIONAL. ----
            // A CW solution is a two-body transfer that knows nothing about the planet underneath it.
            // Flight 012's went 15.6 km below the surface and the display said "closing" the whole
            // way down. Anything that would put periapsis into the atmosphere is refused outright.
            if (WouldBreachFloor(dv))
            {
                Hold();
                Note = "CW REFUSED - that transfer breaches the periapsis floor";
                return;
            }

            AttitudeController.Ascent.SteerTo(ship, dv.normalized, Vector3d.zero);
            if (AttitudeController.Ascent.ErrorDeg < 5.0)
            {
                lastBurnAt = now;
                Debug.Log(Tag + "CW: " + Note);
                // The burn itself is the node executor's job - pure/BurnExec.cs. Until that is wired
                // the crew flies it, and saying so is better than a silent no-op.
                Note += " - ARMED, execute manually";
            }
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
            Hold();
            Note = "STATION KEEPING at " + RangeM.ToString("F0") + " m";
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
