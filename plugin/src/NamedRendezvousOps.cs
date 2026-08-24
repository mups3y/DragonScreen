/*
 * DragonScreen - NamedRendezvousOps
 *
 * GLUE. Flies the real Crew Dragon co-elliptic rendezvous - the named-burn sequence in
 * pure/NamedRendezvous.cs - and hands off to the R-bar/V-bar L-approach at the approach box. RSS-gated:
 * StationApproach delegates here when the body is Earth; the stock ladder is left untouched.
 *
 *   PHASING (NC)   drift the low insertion orbit, watching the phase angle; at the computed lead angle
 *                  fire NC prograde to raise apoapsis to the co-elliptic radius.
 *   TRANSFER       coast to the transfer apoapsis; fire NSR to circularise on the co-elliptic (dH below).
 *   COELLIPTIC     drift the co-elliptic; at the Ti elevation angle fire Ti prograde toward the box.
 *   TERMINAL       coast the Ti transfer to ~2 km below the station, then hand to WaypointApproachOps.
 *
 * Every burn is a plain prograde apsis impulse built the same way MatchAltitude builds one:
 * Hohmann dv along WorldVelAt(orbit, burnTime), executed by NodeExecutor. All coplanar; the glue owns
 * any out-of-plane (NPC) trim as a cross-track burn (kept small by launching into the ISS plane).
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
        public static double RangeKm, PhaseDeg, LastDvMps;

        private static Vessel ship, station;
        private static bool nsrPending, warpArmed;

        public static bool Engage(Vessel v, Vessel target)
        {
            if (v == null || target == null) return false;
            if (v.orbit == null || v.mainBody == null || v.orbit.PeA < v.mainBody.atmosphereDepth)
            {
                Note = "not in a stable orbit yet"; return false;
            }
            ship = v; station = target; Engaged = true; Leg = RdvLeg.Phasing;
            nsrPending = false; warpArmed = false;
            Debug.Log(Tag + "NAMED-BURN rendezvous ENGAGED (co-elliptic) - target '" + target.vesselName
                          + "', " + (Vector3d.Distance(v.CoM, target.CoM) / 1000.0).ToString("F1") + " km");
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
            nsrPending = false; RangeKm = 0.0; PhaseDeg = 0.0; LastDvMps = 0.0;
        }

        public static void Tick()
        {
            if (!Engaged || ship == null || station == null) return;
            if (ship.state == Vessel.State.DEAD || station.state == Vessel.State.DEAD)
            { Disengage("vessel lost"); return; }

            // Once the L-approach has the vehicle, this stage is done steering.
            if (WaypointApproachOps.Engaged) { Leg = RdvLeg.Arrived; Note = "L-approach flying"; return; }

            // A node in flight belongs to the leg that planned it - let the executor run.
            if (NodeExecutor.Active) { AdvanceOnBurnDone(); Note = "executing " + NodeExecutor.Note; return; }

            RdvInputs s = Geometry();
            RangeKm = s.RangeM / 1000.0; PhaseDeg = s.PhaseAngleDeg;

            // NSR and Ti are the SECOND halves of a transfer: after NC/Ti raise, coast to the far apsis
            // and circularise / arrive there. Handle those pending states before re-planning.
            if (nsrPending) { PlanNsr(s); return; }

            RdvPlan p = NamedRendezvous.Plan(s, Leg);
            Leg = p.Leg; Note = p.Note;

            if (p.FloorBlocked) { Note = p.Note; return; }

            if (p.FireNow && p.Burn == "NC")
            {
                warpArmed = false;
                if (PlanProgradeBurn(p.DvMps, BurnAtPeriapsis(), "NC phasing raise"))
                { Leg = RdvLeg.Transfer; nsrPending = true; }
                return;
            }

            // ---- WARP THE LONG WAITS. Phasing closes over several orbits (hours); the co-elliptic
            // drift to the Ti point is another lap. Warp toward the gate and drop out ~2 min short so the
            // burn can be planned in real time. Without this the rendezvous sits in 1x for hours. ----
            if (Leg == RdvLeg.Phasing && !p.FloorBlocked)
            {
                double lead = NamedRendezvous.NcLeadAngleDeg(s.ChaserRadiusM,
                                  NamedRendezvous.CoellipticRadius(s.TargetRadiusM), s.TargetRadiusM,
                                  s.Mu, NamedRendezvous.CoellipticArriveAheadDeg);
                double phase = s.PhaseAngleDeg; if (phase < 0.0) phase += 360.0;
                double gap = phase - lead; while (gap < 0.0) gap += 360.0;   // deg still to close
                WarpToClose(gap, s.ChaserRadiusM, s.TargetRadiusM, s.Mu);
            }
            else if (Leg == RdvLeg.Coelliptic)
            {
                double alongTi = NamedRendezvous.AlongTrackForElevation(NamedRendezvous.CoellipticDhM,
                                     NamedRendezvous.TiElevationDeg);
                double along = s.PhaseAngleDeg * System.Math.PI / 180.0 * s.TargetRadiusM;
                double gapDeg = System.Math.Max(0.0, along - alongTi) / s.TargetRadiusM * 180.0 / System.Math.PI;
                WarpToClose(gapDeg, s.ChaserRadiusM, s.TargetRadiusM, s.Mu);
            }
            if (p.FireNow && p.Burn == "Ti")
            {
                if (PlanProgradeBurn(p.DvMps, BurnNow(), "Ti terminal initiation"))
                { Leg = RdvLeg.Terminal; }
                return;
            }

            // Terminal coast: when we reach the L-approach envelope, hand it the vehicle. Using the
            // envelope directly keeps the handoff range and the acceptance gate in lockstep - no refusal.
            if (Leg == RdvLeg.Terminal || Leg == RdvLeg.Arrived)
            {
                if (s.RangeM <= WaypointApproachOps.EnvelopeM)
                {
                    HandToLApproach();
                    return;
                }
                Note = "coasting the Ti transfer - " + RangeKm.ToString("F1") + " km to the approach box";
            }
        }

        // ---- transfer second-halves --------------------------------------------------------------

        /// <summary>After NC completes, circularise at the transfer apoapsis (the co-elliptic radius).</summary>
        private static void PlanNsr(RdvInputs s)
        {
            CelestialBody b = ship.mainBody;
            Orbit o = ship.orbit;
            double rAp = b.Radius + o.ApA;
            double mu = b.gravParameter;
            double dv = Hohmann.CirculariseDv(rAp, o.semiMajorAxis, mu);   // prograde at apoapsis
            double at = Planetarium.GetUniversalTime() + o.timeToAp;
            if (PlanProgradeBurn(dv, at, "NSR co-elliptic circularise"))
            { nsrPending = false; Leg = RdvLeg.Coelliptic; }
        }

        private static void AdvanceOnBurnDone()
        {
            // NodeExecutor is flying a node; nothing to do until it finishes. Leg transitions on the
            // NEXT idle tick (Plan re-runs, or PlanNsr fires) - kept here so the state is explicit.
        }

        // ---- burn construction (mirrors StationApproach.MatchAltitude) ---------------------------

        private static double BurnNow()      { return Planetarium.GetUniversalTime() + 5.0; }
        private static double BurnAtPeriapsis()
        {
            // A near-circular insertion orbit has no meaningful periapsis; a prograde burn anywhere makes
            // that point the periapsis of the transfer. Fire soon, leaving the executor room to orient.
            double now = Planetarium.GetUniversalTime();
            double tPe = ship.orbit.timeToPe;
            return (tPe > 30.0 && tPe < ship.orbit.period * 0.5) ? now + tPe : now + 5.0;
        }

        private static bool PlanProgradeBurn(double dvMag, double atUt, string label)
        {
            if (Mathf.Abs((float)dvMag) < 0.05) { Note = label + " - nothing to burn"; return false; }
            Vector3d dv = WorldVelAt(ship.orbit, atUt).normalized * dvMag;
            // ⛔ BURN ON THE DRACO, NOT THE SUPERDRACO (user 2026-08-24, "copy real Crew-2"). Real Crew
            // Dragon flies EVERY orbital maneuver on its 16 Draco RCS thrusters; the SuperDracos are
            // launch-abort only and are NEVER fired nominally. The Draco now carries its real effective
            // thrust (DragonScreen.cfg x5) at BurnPct=100 %, so a phasing burn is minutes, not tens of
            // minutes - fast enough to fly on RCS. NodeExecutor.useRcs points the nose along dv and fires
            // UllageFore (RCS translation), measuring the delivered Δv off the orbit (RCS is not in
            // LiveThrust). CapsuleRcs.Set(BurnPct) below gives it full strength for the burn.
            CapsuleRcs.Set(ship, CapsuleRcs.BurnPct);
            if (NodeExecutor.Begin(ship, dv, atUt, label, useRcs: true))
            {
                LastDvMps = System.Math.Abs(dvMag);
                warpArmed = false;                            // the next long wait may warp again
                Debug.Log(Tag + label + ": " + dvMag.ToString("F1") + " m/s at T+"
                          + (atUt - Planetarium.GetUniversalTime()).ToString("F0") + " s");
                return true;
            }
            Note = NodeExecutor.Note; return false;
        }

        /// <summary>Warp toward the point where a wait's angular gap closes, dropping out ~2 min short so
        /// the burn plans in real time. One-shot per wait (re-armed at each burn). A manual warp is left be.</summary>
        private static void WarpToClose(double gapDeg, double rChaser, double rTgt, double mu)
        {
            if (gapDeg <= 0.0 || warpArmed) return;
            double closeRateDegS = (NamedRendezvous.MeanMotion(rChaser, mu)
                                    - NamedRendezvous.MeanMotion(rTgt, mu)) * 180.0 / System.Math.PI;
            if (closeRateDegS <= 1e-6) return;                // not catching up (shouldn't happen when lower)
            double tClose = gapDeg / closeRateDegS;
            if (tClose < 300.0) return;                       // within a few minutes - ride it in real time
            if (TimeWarp.CurrentRateIndex != 0) return;       // don't fight a manual warp
            warpArmed = true;
            TimeWarp.fetch.WarpTo(Planetarium.GetUniversalTime() + tClose - 120.0);
            Debug.Log(Tag + "warping " + (tClose / 60.0).ToString("F0") + " min toward the "
                      + (Leg == RdvLeg.Phasing ? "NC lead angle" : "Ti point"));
        }

        private static void HandToLApproach()
        {
            WaypointApproachOps.Enabled = true;               // the named-burn method IS the enable
            if (WaypointApproachOps.Engage(ship, station))
            {
                Leg = RdvLeg.Arrived;
                Debug.Log(Tag + "named-burn rendezvous complete - handing to the R-bar/V-bar L-approach at "
                          + RangeKm.ToString("F2") + " km");
            }
            else Note = "L-approach refused: " + WaypointApproachOps.Note;
        }

        // ---- geometry ----------------------------------------------------------------------------

        private static Vector3d WorldVelAt(Orbit o, double ut) { return o.getOrbitalVelocityAtUT(ut).xzy; }

        private static RdvInputs Geometry()
        {
            RdvInputs s = new RdvInputs();
            CelestialBody b = ship.mainBody;
            s.Mu = b.gravParameter;
            s.ChaserRadiusM = (ship.CoM - b.position).magnitude;
            s.ChaserSmaM = ship.orbit.semiMajorAxis;
            s.TargetRadiusM = station.orbit.semiMajorAxis;   // near-circular ISS
            s.PeriapsisM = b.Radius + ship.orbit.PeA;
            s.FloorM = b.Radius + b.atmosphereDepth + 5000.0;   // scales to any body (Earth 145 km)
            s.RangeM = Vector3d.Distance(ship.CoM, station.CoM);
            s.PhaseAngleDeg = PhaseAngleDeg();
            return s;
        }

        /// <summary>Signed angular separation, target AHEAD of the chaser positive (prograde), degrees.</summary>
        private static double PhaseAngleDeg()
        {
            CelestialBody b = ship.mainBody;
            Vector3d pc = ship.CoM - b.position;
            Vector3d ps = station.CoM - b.position;
            Vector3d n = ship.orbit.GetOrbitNormal().xzy;         // MechJeb swizzle
            if (n.sqrMagnitude < 1e-6) return 0.0;
            double ang = Vector3d.Angle(pc, ps);                  // 0..180
            double sign = (Vector3d.Dot(Vector3d.Cross(pc, ps), n) >= 0.0) ? 1.0 : -1.0;
            return sign * ang;
        }
    }
}
