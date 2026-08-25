/*
 * DragonScreen - NamedRendezvous (PURE)
 *
 * The REAL Crew Dragon / ISS rendezvous, as the named-burn sequence NASA and SpaceX actually fly
 * (docs/REAL_CREW_DRAGON_MISSION.md, the Crew-4 timeline): Phase -> Boost -> Close -> Transfer ->
 * Co-elliptic -> Approach Initiation (AI) -> Approach Midcourse, handing to the R-bar/V-bar L-approach.
 *
 * ---- WHY THIS IS A REBUILD, AND WHAT THE OLD ONE GOT WRONG ----
 * The previous version fired NC only inside a 0.6 deg phase window reached by a ONE-SHOT warp that
 * latched forever on any overshoot; flown 2026-08-25 (flight_0825_081808) it fired ZERO burns and the
 * orbit sat frozen. Two lessons drive this design:
 *   1. NEVER a knife-edge trigger. A raise fires when the phase angle has CLOSED TO its lead angle
 *      (gap <= tol), and fires even if a warp overshoots it slightly - being a little past the lead is
 *      a correctable arrival error, not a reason to wait a full synodic period. The warp is re-armable
 *      (guarded on the live TimeWarp state, never a sticky bool).
 *   2. Phase amplifies: 1 deg of phase error is ~118 km of along-track at 419 km. So only the FIRST
 *      raise (Boost) is phase-angle-triggered, aimed to arrive comfortably BEHIND the station; every
 *      terminal burn after the co-elliptic is triggered on directly-measured RANGE and flown by the
 *      Clohessy-Wiltshire two-impulse solver (CwTargeting), which re-solves from the measured relative
 *      state and aims at an offset point BEHIND the station - never at it (falcon-rendezvous-approach-law).
 *
 * ---- THE SPLIT: pure owns the CLIMB, glue owns the CW TERMINAL ----
 * The climb (Phase/Boost/Close) is coplanar apsis mechanics - vis-viva and lead angles - so it lives
 * here, headless-tested. The terminal phase (Transfer/Co-elliptic/AI/Midcourse) is Clohessy-Wiltshire
 * targeting in the station's LVLH frame, which needs live vectors, so NamedRendezvousOps drives it with
 * the already-tested pure CwTargeting + Lvlh. This file owns the climb decisions, the geometry helpers,
 * and the constants the terminal triggers read; the glue owns the leg transitions past Close.
 *
 * All climb burns are plain prograde apsis impulses (see Hohmann.cs); the glue turns the signed prograde
 * dv into a world node for NodeExecutor (Draco RCS). Coplanar throughout - the out-of-plane trim is the
 * glue's cross-track burn, kept small by launching into the ISS plane.
 */
namespace DragonScreen
{
    /// <summary>Where a planned burn fires: nowhere (coast/wait), at once, or at the next apoapsis.</summary>
    public enum RdvFire { None, Now, Apoapsis }

    public enum RdvLeg
    {
        /// <summary>Nothing to do / no target.</summary>
        Idle,
        /// <summary>PHASE: clean the insertion dispersions into a circular phasing orbit, then wait.</summary>
        Phase,
        /// <summary>BOOST: waiting the phase lead angle to fire the Hohmann raise to the co-elliptic.</summary>
        Boost,
        /// <summary>CLOSE: coasting the boost ellipse to apoapsis to circularise onto the co-elliptic.</summary>
        Close,
        /// <summary>Co-elliptic drift: warping down to the terminal (CW) range behind the station.</summary>
        Drift,
        /// <summary>TRANSFER: the CW two-impulse departure toward the Approach Initiation point.</summary>
        Transfer,
        /// <summary>CO-ELLIPTIC: the CW arrival velocity-match that parks us at the AI hold.</summary>
        Coelliptic,
        /// <summary>APPROACH INITIATION: the CW departure from the AI hold into the approach ellipsoid.</summary>
        ApproachInit,
        /// <summary>MIDCOURSE: the mid-transfer CW correction of the AI leg.</summary>
        Midcourse,
        /// <summary>At the approach box below/behind the station - hand to the L-approach.</summary>
        Arrived
    }

    /// <summary>Live rendezvous geometry, all from the two orbits. Near-circular coplanar assumed for the
    /// climb; the glue handles the plane and the LVLH terminal geometry separately.</summary>
    public struct RdvInputs
    {
        public double Mu;
        public double BodyRadiusM;        // the body's radius (for altitude instrumentation only)
        public double ChaserRadiusM;      // current orbital radius (from the body centre)
        public double ChaserSmaM;         // current semi-major axis
        public double ChaserApoapsisM;    // current apoapsis radius (for the circularise dv)
        public double ChaserPeriapsisM;   // current periapsis radius
        public double TargetRadiusM;      // target circular radius
        /// <summary>Target true longitude minus chaser's, degrees. POSITIVE = target AHEAD (we are behind).</summary>
        public double PhaseAngleDeg;
        /// <summary>Slant range to the target, metres.</summary>
        public double RangeM;
        /// <summary>The floor our periapsis may never cross, metres (radius, not altitude).</summary>
        public double FloorM;
    }

    /// <summary>What the CLIMB wants this tick. The glue reads FireAt/Burn/DvMps to plan a node, or
    /// WarpWaitS to warp the wait, or NextLeg to advance. Terminal legs are glue-driven (see the file
    /// header) and do not come through here.</summary>
    public struct RdvPlan
    {
        public RdvLeg Leg;
        /// <summary>The named burn to plan now, or "" to coast/wait. PHASE / BOOST / CLOSE.</summary>
        public string Burn;
        /// <summary>Signed prograde dv for the burn, m/s (apply along velocity at the fire apsis).</summary>
        public double DvMps;
        /// <summary>Where this burn fires.</summary>
        public RdvFire FireAt;
        /// <summary>Convenience: a burn is due (FireAt != None).</summary>
        public bool FireNow;
        /// <summary>The leg to enter once the glue has begun this burn (or immediately, for a skip).</summary>
        public RdvLeg NextLeg;
        /// <summary>Refused because the burn would breach the periapsis floor.</summary>
        public bool FloorBlocked;
        /// <summary>Seconds the glue should warp toward the next gate (0 = ride it in real time).</summary>
        public double WarpWaitS;
        // ---- instrumentation (rv_ columns) ----
        /// <summary>The phase lead angle this leg is waiting for, degrees (0 when N/A).</summary>
        public double LeadDeg;
        /// <summary>Degrees of phase still to close to the lead angle (0 when N/A / fired).</summary>
        public double GapDeg;
        /// <summary>The co-elliptic target altitude this climb aims at, km (0 when N/A).</summary>
        public double CoAltKm;
        public string Note;
    }

    public static class NamedRendezvous
    {
        // ---- CLIMB profile constants ----

        /// <summary>Final co-elliptic height below the target, metres. BOOST raises to here and CLOSE
        /// circularises here - the stable concentric platform the terminal (CW) phase departs from.
        /// 10 km: a real co-elliptic offset, far enough to be a stable slow catch-up, close enough that
        /// the terminal CW transfers are small.</summary>
        public const double CoellipticDhM = 10000.0;

        /// <summary>Fire BOOST to arrive on the co-elliptic with the target still AHEAD (us behind) by
        /// this margin, degrees. Sized so a firing/warp dispersion of ~1 deg still leaves us safely
        /// BEHIND (never ahead of a lower target, which never closes) - the co-elliptic drift and the CW
        /// terminal then close the rest. ~1.5 deg is ~178 km behind at 419 km.</summary>
        public const double BoostArriveAheadDeg = 1.5;

        /// <summary>Phase-angle firing tolerance, degrees. A raise fires when the gap to the lead has
        /// closed to within this (and fires anyway if a warp nudges slightly past - see Plan).</summary>
        public const double PhaseGateTolDeg = 0.15;

        /// <summary>An insertion orbit already this circular (apoapsis - periapsis) skips the PHASE
        /// circularise burn, metres.</summary>
        public const double CircularTolM = 1500.0;

        // ---- TERMINAL (CW) trigger constants, read by NamedRendezvousOps ----

        /// <summary>Fire TRANSFER (the first CW intercept) once the co-elliptic drift brings the target
        /// within this along-track lead, metres. CW solves cleanly from tens of km, so this is a range to
        /// be WITHIN, not a knife-edge.</summary>
        public const double TransferRangeM = 20000.0;

        /// <summary>The real Approach Initiation standoff: 7.5 km behind the station (Crew-4). TRANSFER
        /// aims the CW intercept here; CO-ELLIPTIC matches velocity here to make the AI hold.</summary>
        public const double AiPointM = 7500.0;

        /// <summary>Approach-ellipsoid entry standoff behind the station, metres. AI aims the CW intercept
        /// here; at this range the L-approach (WaypointApproachOps, 2.5 km envelope) takes the vehicle.</summary>
        public const double AeEntryM = 2000.0;

        // ---- geometry helpers (all pure, all tested) ----

        /// <summary>Mean motion (rad/s) of a circular orbit at radius r.</summary>
        public static double MeanMotion(double r, double mu)
        {
            if (r <= 0.0 || mu <= 0.0) return 0.0;
            return System.Math.Sqrt(mu / (r * r * r));
        }

        /// <summary>Half-period (Hohmann transfer time) from rFrom to rTo, seconds.</summary>
        public static double TransferTimeS(double rFrom, double rTo, double mu)
        {
            double a = 0.5 * (rFrom + rTo);
            if (a <= 0.0 || mu <= 0.0) return 0.0;
            return System.Math.PI * System.Math.Sqrt(a * a * a / mu);
        }

        /// <summary>
        /// The phase angle (target AHEAD, degrees) at which to fire a Hohmann raise from rFrom so the
        /// chaser arrives at rTo with the target still ahead by <paramref name="arriveAheadDeg"/>.
        ///
        /// The chaser sweeps 180 deg on the transfer ellipse in time t_H; the target sweeps n_tgt*t_H.
        /// For the chaser to arrive `arriveAhead` behind the target:
        ///   180 = phase + targetSweep - arriveAhead   ->   phase = 180 - targetSweep + arriveAhead.
        /// (Degrees; result normalised to [0,360).)
        /// </summary>
        public static double LeadAngleDeg(double rFrom, double rTo, double rTarget, double mu,
                                          double arriveAheadDeg)
        {
            double tH = TransferTimeS(rFrom, rTo, mu);
            double targetSweepDeg = MeanMotion(rTarget, mu) * tH * 180.0 / System.Math.PI;
            double lead = 180.0 - targetSweepDeg + arriveAheadDeg;
            while (lead < 0.0) lead += 360.0;
            while (lead >= 360.0) lead -= 360.0;
            return lead;
        }

        /// <summary>The chaser-catches-target closing rate, deg/s (positive when the lower chaser gains).
        /// The glue turns a phase gap into a warp wait with this.</summary>
        public static double ClosingRateDegS(double rChaser, double rTarget, double mu)
        {
            double d = (MeanMotion(rChaser, mu) - MeanMotion(rTarget, mu)) * 180.0 / System.Math.PI;
            return d;
        }

        /// <summary>Along-track lead of the target (metres, ahead) at which its elevation above the
        /// chaser's local horizontal equals <paramref name="elevDeg"/>, for a height difference dhM.
        /// tan(elev) = dh / alongTrack. Instrumentation / geometry aid.</summary>
        public static double AlongTrackForElevation(double dhM, double elevDeg)
        {
            double t = System.Math.Tan(elevDeg * System.Math.PI / 180.0);
            return (t > 1e-6) ? dhM / t : 0.0;
        }

        /// <summary>Target elevation (degrees) above the chaser's local horizontal for a height difference
        /// dh and along-track separation x (both metres, target ahead+above). Instrumentation.</summary>
        public static double ElevationDeg(double dhM, double alongTrackM)
        {
            return System.Math.Atan2(dhM, System.Math.Max(1.0, alongTrackM)) * 180.0 / System.Math.PI;
        }

        /// <summary>The co-elliptic radius for a given target radius.</summary>
        public static double CoellipticRadius(double rTarget) { return rTarget - CoellipticDhM; }

        /// <summary>Would a transfer to semi-major axis <paramref name="aNew"/> burned at rBurn drop
        /// periapsis below the floor? A raise never does; a lower might. Guards every climb burn.</summary>
        public static bool BreachesFloor(RdvInputs s, double aNew, double rBurn)
        {
            double other = 2.0 * aNew - rBurn;
            double newPeri = System.Math.Min(rBurn, other);
            return newPeri < s.FloorM;
        }

        /// <summary>Signed phase gap still to close to a lead angle, degrees in (-180,180]. Positive means
        /// the target is still further ahead than the lead (keep closing); &lt;= tol means fire.</summary>
        public static double GapToLeadDeg(double phaseDeg, double leadDeg)
        {
            double phase = phaseDeg; if (phase < 0.0) phase += 360.0;
            double gap = phase - leadDeg;
            while (gap <= -180.0) gap += 360.0;
            while (gap > 180.0) gap -= 360.0;
            return gap;
        }

        /// <summary>
        /// The CLIMB plan for this tick (Idle/Phase/Boost/Close). Pure and deterministic; the glue
        /// executes FireAt/DvMps, or warps WarpWaitS, or advances to NextLeg. Legs past Close are
        /// glue-driven (CW terminal) and never reach here.
        /// </summary>
        public static RdvPlan Plan(RdvInputs s, RdvLeg leg)
        {
            RdvPlan p = new RdvPlan();
            p.Leg = leg;
            p.NextLeg = leg;
            p.FireAt = RdvFire.None;

            double mu = s.Mu;
            double rTgt = s.TargetRadiusM;
            double rCo = CoellipticRadius(rTgt);
            p.CoAltKm = 0.0;

            switch (leg)
            {
                case RdvLeg.Idle:
                case RdvLeg.Phase:
                {
                    p.Leg = RdvLeg.Phase;
                    // PHASE: clean the insertion ellipse into a circular phasing orbit - a small, real
                    // burn that gives the lead-angle geometry an exact circular chaser to work from. If
                    // insertion is already circular enough, skip straight to BOOST.
                    double spread = s.ChaserApoapsisM - s.ChaserPeriapsisM;
                    if (spread <= CircularTolM)
                    {
                        p.NextLeg = RdvLeg.Boost;
                        p.Note = "insertion already circular - to BOOST phasing";
                        return p;
                    }
                    double dv = Hohmann.CirculariseDv(s.ChaserApoapsisM, s.ChaserSmaM, mu);  // at apoapsis
                    p.Burn = "PHASE"; p.DvMps = dv; p.FireAt = RdvFire.Apoapsis; p.FireNow = true;
                    p.NextLeg = RdvLeg.Boost;
                    p.Note = "PHASE - circularise the insertion orbit";
                    return p;
                }

                case RdvLeg.Boost:
                {
                    p.Leg = RdvLeg.Boost;
                    // BOOST: fire the Hohmann raise to the co-elliptic radius when the phase angle has
                    // closed to the lead angle. Lower chaser -> catches up -> phase decreases toward lead.
                    double lead = LeadAngleDeg(s.ChaserRadiusM, rCo, rTgt, mu, BoostArriveAheadDeg);
                    double gap = GapToLeadDeg(s.PhaseAngleDeg, lead);
                    p.LeadDeg = lead;
                    p.GapDeg = gap;
                    p.CoAltKm = (rCo - s.BodyRadiusM) / 1000.0;

                    // Fire when we have reached the lead (gap small) OR a warp has nudged us just past it
                    // (gap gone slightly negative). Never wait a whole synodic period for a small overshoot.
                    if (gap <= PhaseGateTolDeg)
                    {
                        double aNew = Hohmann.TransferSma(s.ChaserRadiusM, rCo);
                        if (BreachesFloor(s, aNew, s.ChaserRadiusM))
                        { p.FloorBlocked = true; p.Note = "BOOST blocked - periapsis floor"; return p; }
                        double dv = Hohmann.RaiseOppositeApsisDv(s.ChaserRadiusM, s.ChaserSmaM, rCo, mu);
                        p.Burn = "BOOST"; p.DvMps = dv; p.FireAt = RdvFire.Now; p.FireNow = true;
                        p.NextLeg = RdvLeg.Close;
                        p.Note = "BOOST - Hohmann raise to the co-elliptic";
                        return p;
                    }
                    // still closing - hand the glue a warp wait toward the lead angle
                    double rateDegS = ClosingRateDegS(s.ChaserRadiusM, rTgt, mu);
                    p.WarpWaitS = (rateDegS > 1e-9) ? gap / rateDegS : 0.0;
                    p.Note = "phasing - " + gap.ToString("F2") + " deg to the BOOST lead angle";
                    return p;
                }

                case RdvLeg.Close:
                {
                    p.Leg = RdvLeg.Close;
                    p.CoAltKm = (rCo - s.BodyRadiusM) / 1000.0;
                    // CLOSE: circularise at the boost apoapsis onto the co-elliptic. Fires at apoapsis;
                    // the glue plans the node at timeToAp and NodeExecutor warps to it. dv from the
                    // MEASURED apoapsis, so whatever apoapsis BOOST actually reached is what we circularise.
                    double dv = Hohmann.CirculariseDv(s.ChaserApoapsisM, s.ChaserSmaM, mu);
                    if (System.Math.Abs(dv) < 0.05)
                    {
                        p.NextLeg = RdvLeg.Drift;
                        p.Note = "already circular at the co-elliptic - to DRIFT";
                        return p;
                    }
                    p.Burn = "CLOSE"; p.DvMps = dv; p.FireAt = RdvFire.Apoapsis; p.FireNow = true;
                    p.NextLeg = RdvLeg.Drift;
                    p.Note = "CLOSE - circularise onto the co-elliptic";
                    return p;
                }

                default:
                    // Drift and the terminal legs are glue-driven; echo back unchanged.
                    p.Note = "glue-driven leg";
                    return p;
            }
        }
    }
}
