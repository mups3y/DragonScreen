/*
 * DragonScreen - NamedRendezvousOps
 *
 * GLUE. Flies the real Crew Dragon named-burn rendezvous and hands to the R-bar/V-bar L-approach at the
 * approach box. RSS-gated: StationApproach delegates here when the body is Earth.
 *
 *   PHASE      circularise the insertion dispersions into a clean phasing orbit (pure NamedRendezvous).
 *   BOOST      at the phase lead angle, Hohmann-raise apoapsis to the co-elliptic radius.
 *   CLOSE      circularise at that apoapsis onto the co-elliptic (10 km below the station).
 *   DRIFT      warp the slow co-elliptic catch-up down to the terminal (CW) range behind the station.
 *   TRANSFER   Clohessy-Wiltshire two-impulse intercept to the Approach Initiation point (7.5 km behind).
 *   CO-ELLIPTIC arrival velocity-match - a station-keeping hold at the AI point.
 *   AI         CW intercept from the AI hold to the approach-ellipsoid entry (2 km behind).
 *   MIDCOURSE  the mid-transfer CW re-solve of the AI leg, then hand to WaypointApproachOps.
 *
 * ---- WHY THIS SHAPE, AFTER THE 2026-08-25 ZERO-BURN FLIGHT ----
 * The old co-elliptic NC/NSR/Ti fired nothing: NC needed a 0.6 deg phase window reached by a one-shot
 * warp that latched forever. Two rules replace it and are the whole point of the rebuild:
 *   - ROBUST TRIGGERS. A climb raise fires the moment the phase gap has CLOSED to its lead (and fires
 *     anyway on a small overshoot); the warp is re-armable, guarded only on the live TimeWarp state, so
 *     there is no sticky bool and no synodic-period lock-up.
 *   - CW FOR THE TERMINAL. Phase error amplifies (~118 km/deg at 419 km), so only BOOST is phase-angle
 *     triggered - aimed to arrive comfortably BEHIND - and everything past the co-elliptic is measured
 *     RANGE fed to CwTargeting, which re-solves from the live relative state and aims at an offset point
 *     behind the station, never at it (falcon-rendezvous-approach-law). Each burn is re-decided from
 *     what the vehicle ACTUALLY did, so a dispersed burn corrects rather than compounds.
 *
 * Every climb burn is a prograde apsis impulse; every terminal burn is a world-frame CW impulse. Both
 * run on the Draco (NodeExecutor useRcs) and are floor-checked before ignition. The rv_ recorder columns
 * read the fields below, so a flight can be read back burn by burn.
 */
using UnityEngine;

namespace DragonScreen
{
    public static class NamedRendezvousOps
    {
        private const string Tag = "[DragonScreen] ";

        public static bool Engaged { get; private set; }
        public static RdvLeg Leg { get; private set; }
        public static string Note = "-";

        // ---- instrumentation (read by FlightRecorder rv_ block) ----
        public static double RangeKm, PhaseDeg, LastDvMps;
        public static double AlongKm, RadialKm, ElevDeg, LeadDeg, GapDeg, CoAltKm, ArrRelMps;
        public static string LastBurn = "-";

        private static Vessel ship, station;
        // Terminal-phase timing, all absolute UT. Set when a CW burn is planned, read while coasting.
        private static double termArrivalUt, termMidUt;
        private static bool midcourseDone;

        public static bool Engage(Vessel v, Vessel target)
        {
            if (v == null || target == null) return false;
            if (v.orbit == null || v.mainBody == null || v.orbit.PeA < v.mainBody.atmosphereDepth)
            {
                Note = "not in a stable orbit yet"; return false;
            }
            ship = v; station = target; Engaged = true; Leg = RdvLeg.Phase;
            termArrivalUt = 0.0; termMidUt = 0.0; midcourseDone = false;
            LastBurn = "-"; LastDvMps = 0.0;
            Debug.Log(Tag + "NAMED-BURN rendezvous ENGAGED (Phase/Boost/Close -> CW terminal) - target '"
                          + target.vesselName + "', "
                          + (Vector3d.Distance(v.CoM, target.CoM) / 1000.0).ToString("F1") + " km");
            return true;
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
            if (NodeExecutor.Active) NodeExecutor.Abort("rendezvous " + why);
            Debug.Log(Tag + "named-burn rendezvous disengaged - " + why);
        }

        public static void Reset()
        {
            Engaged = false; ship = null; station = null; Leg = RdvLeg.Idle; Note = "-";
            RangeKm = 0.0; PhaseDeg = 0.0; LastDvMps = 0.0;
            AlongKm = 0.0; RadialKm = 0.0; ElevDeg = 0.0; LeadDeg = 0.0; GapDeg = 0.0;
            CoAltKm = 0.0; ArrRelMps = 0.0; LastBurn = "-";
            termArrivalUt = 0.0; termMidUt = 0.0; midcourseDone = false;
        }

        public static void Tick()
        {
            if (!Engaged || ship == null || station == null) return;
            if (ship.state == Vessel.State.DEAD || station.state == Vessel.State.DEAD)
            { Disengage("vessel lost"); return; }

            // Once the L-approach has the vehicle, this stage is done steering.
            if (WaypointApproachOps.Engaged) { Leg = RdvLeg.Arrived; Note = "L-approach flying"; return; }

            RangeKm = Vector3d.Distance(ship.CoM, station.CoM) / 1000.0;

            // A node in flight belongs to the leg that planned it - let the executor run to completion.
            if (NodeExecutor.Active) { Note = "executing " + LastBurn + " - " + NodeExecutor.Note; return; }

            switch (Leg)
            {
                case RdvLeg.Idle:
                case RdvLeg.Phase:
                case RdvLeg.Boost:
                case RdvLeg.Close:
                    TickClimb();
                    return;
                case RdvLeg.Drift:
                    TickDrift();
                    return;
                case RdvLeg.Transfer:
                case RdvLeg.Coelliptic:
                case RdvLeg.ApproachInit:
                case RdvLeg.Midcourse:
                    TickTerminal();
                    return;
                default:
                    Note = "arrived - awaiting L-approach";
                    return;
            }
        }

        // ================= THE CLIMB (Phase / Boost / Close), pure-decided =================

        private static void TickClimb()
        {
            RdvInputs s = ClimbGeometry();
            PhaseDeg = s.PhaseAngleDeg;

            RdvPlan p = NamedRendezvous.Plan(s, Leg);
            Leg = p.Leg; Note = p.Note;
            LeadDeg = p.LeadDeg; GapDeg = p.GapDeg; CoAltKm = p.CoAltKm;

            if (p.FloorBlocked) return;

            // A skip (already circular) advances the leg with no burn; a wait leaves the leg unchanged
            // (NextLeg == Leg) and warps below.
            if (!p.FireNow) { Leg = p.NextLeg; }

            if (p.FireNow)
            {
                double atUt = FireTimeFor(p.FireAt);
                if (PlanProgradeBurn(p.DvMps, atUt, p.Burn))
                {
                    LastBurn = p.Burn;
                    Leg = p.NextLeg;   // executor now owns the burn; the next idle tick runs the next leg
                }
                return;
            }

            // Still waiting - warp toward the gate (re-armable, guarded on live TimeWarp state).
            if (p.WarpWaitS > 0.0)
                WarpToUt(Planetarium.GetUniversalTime() + p.WarpWaitS, WarpLeadS);
        }

        // ================= THE CO-ELLIPTIC DRIFT (warp to terminal range) =================

        private static void TickDrift()
        {
            LvlhSample g = TerminalGeometry();
            if (!g.Valid) { Note = "drift - degenerate frame"; return; }

            double behindM = -g.L.AlongM;          // positive = we are behind the station
            RadialKm = g.L.RadialM / 1000.0;
            AlongKm = g.L.AlongM / 1000.0;
            ElevDeg = NamedRendezvous.ElevationDeg(System.Math.Abs(g.L.RadialM), behindM);

            // Fire TRANSFER once the drift has brought us within CW range and we are still BEHIND.
            if (behindM <= NamedRendezvous.TransferRangeM && behindM > 0.0)
            {
                Leg = RdvLeg.Transfer;
                Note = "co-elliptic drift complete at " + (behindM / 1000.0).ToString("F1")
                     + " km behind - to TRANSFER";
                TickTerminal();
                return;
            }

            // Warp the slow catch-up. The along-track closing rate is measured (AlongRateMps > 0 means we
            // are gaining); target the moment we reach the TRANSFER range, drop out a minute short.
            double rate = g.L.AlongRateMps;        // + = along increasing (closing from behind)
            if (rate > 1e-4 && behindM > NamedRendezvous.TransferRangeM)
            {
                double waitS = (behindM - NamedRendezvous.TransferRangeM) / rate;
                WarpToUt(Planetarium.GetUniversalTime() + waitS, WarpLeadS);
            }
            Note = "co-elliptic drift - " + (behindM / 1000.0).ToString("F1") + " km behind, closing "
                 + rate.ToString("F1") + " m/s";
        }

        // ================= THE TERMINAL PHASE (CW two-impulse), glue-driven =================

        private static void TickTerminal()
        {
            LvlhSample g = TerminalGeometry();
            if (!g.Valid) { Note = "terminal - degenerate frame"; return; }

            double behindM = -g.L.AlongM;
            RadialKm = g.L.RadialM / 1000.0;
            AlongKm = g.L.AlongM / 1000.0;
            ElevDeg = NamedRendezvous.ElevationDeg(System.Math.Abs(g.L.RadialM), System.Math.Max(1.0, behindM));

            double now = Planetarium.GetUniversalTime();

            switch (Leg)
            {
                case RdvLeg.Transfer:
                    // First CW intercept: depart the co-elliptic toward the 7.5 km AI point.
                    if (FireCwIntercept(g, NamedRendezvous.AiPointM, "TRANSFER"))
                        Leg = RdvLeg.Coelliptic;
                    return;

                case RdvLeg.Coelliptic:
                    // Coast to the AI point, then null the relative velocity - the station-keeping hold.
                    if (now >= termArrivalUt - CwArriveLeadS
                        || behindM <= NamedRendezvous.AiPointM * 1.05)
                    {
                        if (FireVelocityMatch(g, "CO-ELLIPTIC"))
                            Leg = RdvLeg.ApproachInit;
                    }
                    else
                    {
                        WarpToUt(termArrivalUt, CwArriveLeadS);
                        Note = "TRANSFER coast - " + (behindM / 1000.0).ToString("F1") + " km behind";
                    }
                    return;

                case RdvLeg.ApproachInit:
                    // Approach Initiation: CW intercept from the AI hold into the approach ellipsoid.
                    if (FireCwIntercept(g, NamedRendezvous.AeEntryM, "AI"))
                    {
                        midcourseDone = false;
                        termMidUt = 0.5 * (now + termArrivalUt);   // mid-transfer correction time
                        Leg = RdvLeg.Midcourse;
                    }
                    return;

                case RdvLeg.Midcourse:
                    // Hand off the instant we are inside the L-approach envelope - it brakes and flies WPs.
                    if (g.L.RangeM <= WaypointApproachOps.EnvelopeM) { HandToLApproach(); return; }

                    if (!midcourseDone && now >= termMidUt)
                    {
                        // Re-solve the AI leg from the measured state - the real Approach Midcourse.
                        if (FireCwIntercept(g, NamedRendezvous.AeEntryM, "MIDCOURSE"))
                            midcourseDone = true;
                        return;
                    }
                    WarpToUt(termArrivalUt, CwArriveLeadS);
                    Note = "AI coast - " + (g.L.RangeM / 1000.0).ToString("F2") + " km"
                         + (midcourseDone ? " (post-midcourse)" : " (pre-midcourse)");
                    return;
            }
        }

        // ---- CW intercept: solve, convert to world, plan the node. Sets termArrivalUt. ----
        private static bool FireCwIntercept(LvlhSample g, double aimBehindM, string label)
        {
            double period = (station.orbit != null && station.orbit.period > 0.0)
                            ? station.orbit.period : (2.0 * System.Math.PI / System.Math.Max(1e-9, g.N));
            double bestTof;
            CwSolution sol = CwTargeting.Best(g.Cw, CwMinTofS, period * CwMaxTofFrac, CwTofSteps,
                                              aimBehindM, out bestTof);
            if (!sol.Ok)
            {
                Note = label + " - no CW solution yet (" + sol.Note + "), holding";
                return false;
            }
            ArrRelMps = sol.ArrivalRelSpeed;

            double ox, oy, oz;
            Lvlh.OffsetToWorld(g.StnR.x, g.StnR.y, g.StnR.z, g.StnV.x, g.StnV.y, g.StnV.z,
                               sol.DvX, sol.DvY, sol.DvZ, out ox, out oy, out oz);
            Vector3d dvWorld = new Vector3d(ox, oy, oz);

            double now = Planetarium.GetUniversalTime();
            if (PlanVectorBurn(dvWorld, now + BurnStartLeadS, label))
            {
                termArrivalUt = now + bestTof;
                LastBurn = label;
                Debug.Log(Tag + label + " CW intercept: " + dvWorld.magnitude.ToString("F1")
                          + " m/s, TOF " + (bestTof / 60.0).ToString("F0") + " min, arrival "
                          + sol.ArrivalRelSpeed.ToString("F1") + " m/s, aim " + (aimBehindM / 1000.0).ToString("F1")
                          + " km behind");
                return true;
            }
            Note = label + " refused: " + NodeExecutor.Note;
            return false;
        }

        // ---- velocity-match: null the relative velocity (station-keeping hold). ----
        private static bool FireVelocityMatch(LvlhSample g, string label)
        {
            Vector3d relV = ship.obt_velocity - station.obt_velocity;
            double now = Planetarium.GetUniversalTime();
            if (relV.magnitude < 0.05)
            {
                LastBurn = label; Note = label + " - already matched (" + relV.magnitude.ToString("F2") + " m/s)";
                return true;   // nothing to burn, but the hold is established
            }
            if (PlanVectorBurn(-relV, now + BurnStartLeadS, label))
            {
                LastBurn = label;
                Debug.Log(Tag + label + " velocity-match: " + relV.magnitude.ToString("F2")
                          + " m/s nulled at " + (g.L.RangeM / 1000.0).ToString("F1") + " km");
                return true;
            }
            Note = label + " refused: " + NodeExecutor.Note;
            return false;
        }

        // ================= BURN CONSTRUCTION (Draco, via NodeExecutor) =================

        private static double FireTimeFor(RdvFire at)
        {
            double now = Planetarium.GetUniversalTime();
            if (at == RdvFire.Apoapsis)
            {
                double tAp = ship.orbit.timeToAp;
                return (tAp > 5.0) ? now + tAp : now + BurnStartLeadS;
            }
            return now + BurnStartLeadS;   // RdvFire.Now: current point becomes the transfer periapsis
        }

        /// <summary>A signed prograde apsis impulse (the climb burns), on the Draco.</summary>
        private static bool PlanProgradeBurn(double dvMag, double atUt, string label)
        {
            if (Mathf.Abs((float)dvMag) < 0.05) { Note = label + " - nothing to burn"; return false; }
            Vector3d dv = WorldVelAt(ship.orbit, atUt).normalized * dvMag;
            return Fire(dv, atUt, label, System.Math.Abs(dvMag));
        }

        /// <summary>A world-frame impulse (the CW terminal burns), on the Draco.</summary>
        private static bool PlanVectorBurn(Vector3d dvWorld, double atUt, string label)
        {
            if (dvWorld.magnitude < 0.05) { Note = label + " - nothing to burn"; return false; }
            return Fire(dvWorld, atUt, label, dvWorld.magnitude);
        }

        private static bool Fire(Vector3d dvWorld, double atUt, string label, double dvMag)
        {
            // ⛔ DRACO, NOT SUPERDRACO (user 2026-08-24, "copy real Crew-2"). Every orbital maneuver is on
            // the 16 Draco RCS thrusters; the SuperDracos are launch-abort only. CapsuleRcs.BurnPct gives
            // the Draco its real full strength; NodeExecutor useRcs points the nose along dv and fires
            // UllageFore, measuring the delivered Δv off the orbit (AccountByVelocity).
            CapsuleRcs.Set(ship, CapsuleRcs.BurnPct);
            if (NodeExecutor.Begin(ship, dvWorld, atUt, label, useRcs: true))
            {
                LastDvMps = dvMag;
                Debug.Log(Tag + label + ": " + dvMag.ToString("F1") + " m/s at T+"
                          + (atUt - Planetarium.GetUniversalTime()).ToString("F0") + " s");
                return true;
            }
            Note = NodeExecutor.Note;
            return false;
        }

        /// <summary>Warp toward an absolute UT, dropping out <paramref name="leadS"/> short so the burn
        /// plans in real time. Re-armable: it only refuses to re-issue while a warp is already running
        /// (or a manual one is), never a sticky bool - so a slight overshoot just re-plans and fires.</summary>
        private static void WarpToUt(double targetUt, double leadS)
        {
            double now = Planetarium.GetUniversalTime();
            double wait = targetUt - leadS - now;
            if (wait < WarpWorthwhileS) return;            // ride the last bit in real time
            if (TimeWarp.CurrentRateIndex != 0) return;    // a warp is already running - leave it be
            TimeWarp.fetch.WarpTo(targetUt - leadS);
        }

        private static void HandToLApproach()
        {
            WaypointApproachOps.Enabled = true;
            if (WaypointApproachOps.Engage(ship, station))
            {
                Leg = RdvLeg.Arrived;
                Debug.Log(Tag + "named-burn rendezvous complete - handing to the R-bar/V-bar L-approach at "
                          + RangeKm.ToString("F2") + " km");
            }
            else Note = "L-approach refused: " + WaypointApproachOps.Note;
        }

        // ================= GEOMETRY =================

        private static Vector3d WorldVelAt(Orbit o, double ut) { return o.getOrbitalVelocityAtUT(ut).xzy; }

        /// <summary>The coplanar climb geometry (radii, phase angle) for the pure state machine.</summary>
        private static RdvInputs ClimbGeometry()
        {
            RdvInputs s = new RdvInputs();
            CelestialBody b = ship.mainBody;
            s.Mu = b.gravParameter;
            s.BodyRadiusM = b.Radius;
            s.ChaserRadiusM = (ship.CoM - b.position).magnitude;
            s.ChaserSmaM = ship.orbit.semiMajorAxis;
            s.ChaserApoapsisM = b.Radius + ship.orbit.ApA;
            s.ChaserPeriapsisM = b.Radius + ship.orbit.PeA;
            s.TargetRadiusM = station.orbit.semiMajorAxis;             // near-circular ISS
            s.FloorM = b.Radius + b.atmosphereDepth + 5000.0;          // scales to any body
            s.RangeM = Vector3d.Distance(ship.CoM, station.CoM);
            s.PhaseAngleDeg = PhaseAngleDeg();
            return s;
        }

        /// <summary>The station-LVLH terminal geometry + a ready-to-solve CW state, computed once per tick.</summary>
        private static LvlhSample TerminalGeometry()
        {
            LvlhSample g = new LvlhSample();
            CelestialBody b = station.mainBody;
            if (b == null) return g;

            g.StnR = station.CoM - b.position;
            g.StnV = station.obt_velocity;
            Vector3d relR = ship.CoM - station.CoM;
            Vector3d relV = ship.obt_velocity - station.obt_velocity;
            g.L = Lvlh.Project(g.StnR.x, g.StnR.y, g.StnR.z, g.StnV.x, g.StnV.y, g.StnV.z,
                               relR.x, relR.y, relR.z, relV.x, relV.y, relV.z);
            if (!g.L.Valid) return g;

            g.N = NamedRendezvous.MeanMotion(station.orbit.semiMajorAxis, b.gravParameter);
            g.Cw = new CwState();
            g.Cw.Rx = g.L.RadialM; g.Cw.Ry = g.L.AlongM; g.Cw.Rz = g.L.CrossM;
            g.Cw.Vx = g.L.RadialRateMps; g.Cw.Vy = g.L.AlongRateMps; g.Cw.Vz = g.L.CrossRateMps;
            g.Cw.N = g.N;
            g.Valid = true;
            return g;
        }

        /// <summary>Signed angular separation, target AHEAD of the chaser positive (prograde), degrees.</summary>
        private static double PhaseAngleDeg()
        {
            CelestialBody b = ship.mainBody;
            Vector3d pc = ship.CoM - b.position;
            Vector3d ps = station.CoM - b.position;
            Vector3d n = ship.orbit.GetOrbitNormal().xzy;             // MechJeb swizzle
            if (n.sqrMagnitude < 1e-6) return 0.0;
            double ang = Vector3d.Angle(pc, ps);                      // 0..180
            double sign = (Vector3d.Dot(Vector3d.Cross(pc, ps), n) >= 0.0) ? 1.0 : -1.0;
            return sign * ang;
        }

        /// <summary>Station-LVLH geometry bundle for one terminal tick.</summary>
        private struct LvlhSample
        {
            public bool Valid;
            public LvlhState L;
            public CwState Cw;
            public double N;
            public Vector3d StnR, StnV;
        }

        // ---- tuning ----
        /// <summary>Lead before a burn's node, seconds - the executor orients inside this.</summary>
        private const double BurnStartLeadS = 8.0;
        /// <summary>Warp drop-out lead before a gate, seconds - close the last bit in real time.</summary>
        private const double WarpLeadS = 90.0;
        /// <summary>Warp drop-out lead before a CW arrival, seconds.</summary>
        private const double CwArriveLeadS = 30.0;
        /// <summary>Do not warp a wait shorter than this, seconds.</summary>
        private const double WarpWorthwhileS = 25.0;
        /// <summary>CW transfer-time sweep: floor, fraction-of-period ceiling, and step count.</summary>
        private const double CwMinTofS = 300.0;
        private const double CwMaxTofFrac = 0.9;
        private const int CwTofSteps = 60;
    }
}
