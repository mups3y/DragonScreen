// DragonScreen - NamedRendezvousOps
// ---- WHY THIS SHAPE, AFTER THE 2026-08-25 ZERO-BURN FLIGHT ----
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
        public static double PassiveMarginM;
        public static string LastBurn = "-";

        private static Vessel ship, station;
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

            if (DockShroud.Open(v))
                Debug.Log(Tag + "nose cone OPENED for rendezvous - forward Dracos + docking port exposed");
            else
                Debug.LogWarning(Tag + "nose cone open event NOT found - if the RCS reads 'obstructed', "
                                     + "the cone is still shut and the phasing burns will tumble");

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

            if (WaypointApproachOps.Engaged) { Leg = RdvLeg.Arrived; Note = "L-approach flying"; return; }

            RangeKm = Vector3d.Distance(ship.CoM, station.CoM) / 1000.0;

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

            if (!p.FireNow) { Leg = p.NextLeg; }

            if (p.FireNow)
            {
                double atUt = FireTimeFor(p.FireAt);
                if (PlanProgradeBurn(p.DvMps, atUt, p.Burn))
                {
                    LastBurn = p.Burn;
                    Leg = p.NextLeg;
                }
                return;
            }

            if (p.WarpWaitS > 0.0)
                WarpToUt(Planetarium.GetUniversalTime() + p.WarpWaitS, WarpLeadS);
        }

        // ================= THE CO-ELLIPTIC DRIFT (warp to terminal range) =================

        private static void TickDrift()
        {
            LvlhSample g = TerminalGeometry();
            if (!g.Valid) { Note = "drift - degenerate frame"; return; }

            double behindM = -g.L.AlongM;
            RadialKm = g.L.RadialM / 1000.0;
            AlongKm = g.L.AlongM / 1000.0;
            ElevDeg = NamedRendezvous.ElevationDeg(System.Math.Abs(g.L.RadialM), behindM);

            if (behindM <= NamedRendezvous.TransferRangeM && behindM > 0.0)
            {
                Leg = RdvLeg.Transfer;
                Note = "co-elliptic drift complete at " + (behindM / 1000.0).ToString("F1")
                     + " km behind - to TRANSFER";
                TickTerminal();
                return;
            }

            double rate = g.L.AlongRateMps;
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
                    if (FireCwIntercept(g, NamedRendezvous.AiPointM, "TRANSFER"))
                        Leg = RdvLeg.Coelliptic;
                    return;

                case RdvLeg.Coelliptic:
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
                    if (FireCwIntercept(g, NamedRendezvous.AeEntryM, "AI"))
                    {
                        midcourseDone = false;
                        termMidUt = 0.5 * (now + termArrivalUt);
                        Leg = RdvLeg.Midcourse;
                    }
                    return;

                case RdvLeg.Midcourse:
                    if (g.L.RangeM <= WaypointApproachOps.EnvelopeM) { HandToLApproach(); return; }

                    if (!midcourseDone && now >= termMidUt)
                    {
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
            double safeM = WaypointApproach.KeepOutRadiusM + PassiveAbortMarginM;
            CwSolution sol = CwTargeting.Best(g.Cw, CwMinTofS, period * CwMaxTofFrac, CwTofSteps,
                                              aimBehindM, out bestTof,
                                              safeM, period, CwTargeting.DefaultCoastSamples);
            if (!sol.Ok)
            {
                Note = label + " - no CW solution yet (" + sol.Note + "), holding";
                return false;
            }
            ArrRelMps = sol.ArrivalRelSpeed;
            PassiveMarginM = sol.MinFreeDriftRangeM;
            if (!sol.PassiveAbortSafe)
                Debug.LogWarning(Tag + label + " PASSIVE-ABORT: closest free-drift "
                    + sol.MinFreeDriftRangeM.ToString("F0") + " m is inside the " + safeM.ToString("F0")
                    + " m safe margin - flew the safest transfer; the keep-out backstop guards a real breach.");

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
                          + " km behind; passive-abort free-drift clears "
                          + sol.MinFreeDriftRangeM.ToString("F0") + " m (safe "
                          + (sol.PassiveAbortSafe ? "YES" : "NO") + ")");
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
                return true;
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
            return now + BurnStartLeadS;
        }

        private static bool PlanProgradeBurn(double dvMag, double atUt, string label)
        {
            if (Mathf.Abs((float)dvMag) < 0.05) { Note = label + " - nothing to burn"; return false; }
            Vector3d dv = WorldVelAt(ship.orbit, atUt).normalized * dvMag;
            return Fire(dv, atUt, label, System.Math.Abs(dvMag));
        }

        private static bool PlanVectorBurn(Vector3d dvWorld, double atUt, string label)
        {
            if (dvWorld.magnitude < 0.05) { Note = label + " - nothing to burn"; return false; }
            return Fire(dvWorld, atUt, label, dvWorld.magnitude);
        }

        private static bool Fire(Vector3d dvWorld, double atUt, string label, double dvMag)
        {
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

        private static void WarpToUt(double targetUt, double leadS)
        {
            double now = Planetarium.GetUniversalTime();
            double wait = targetUt - leadS - now;
            if (wait < WarpWorthwhileS) return;
            if (TimeWarp.CurrentRateIndex != 0) return;
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
            s.TargetRadiusM = station.orbit.semiMajorAxis;
            s.FloorM = b.Radius + b.atmosphereDepth + 5000.0;
            s.RangeM = Vector3d.Distance(ship.CoM, station.CoM);
            s.PhaseAngleDeg = PhaseAngleDeg();
            return s;
        }

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

        private static double PhaseAngleDeg()
        {
            CelestialBody b = ship.mainBody;
            Vector3d pc = ship.CoM - b.position;
            Vector3d ps = station.CoM - b.position;
            Vector3d n = ship.orbit.GetOrbitNormal().xzy;
            if (n.sqrMagnitude < 1e-6) return 0.0;
            double ang = Vector3d.Angle(pc, ps);
            double sign = (Vector3d.Dot(Vector3d.Cross(pc, ps), n) >= 0.0) ? 1.0 : -1.0;
            return sign * ang;
        }

        private struct LvlhSample
        {
            public bool Valid;
            public LvlhState L;
            public CwState Cw;
            public double N;
            public Vector3d StnR, StnV;
        }

        // ---- tuning ----
        private const double BurnStartLeadS = 8.0;
        private const double WarpLeadS = 90.0;
        private const double CwArriveLeadS = 30.0;
        private const double WarpWorthwhileS = 25.0;
        private const double CwMinTofS = 300.0;
        private const double CwMaxTofFrac = 0.9;
        private const int CwTofSteps = 60;
        private const double PassiveAbortMarginM = 50.0;
    }
}
