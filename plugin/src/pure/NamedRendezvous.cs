/*
 * DragonScreen - NamedRendezvous
 *
 * PURE. The REAL Crew Dragon / ISS rendezvous: the named-burn co-elliptic profile, not an ad-hoc
 * "phase then ride whatever intercept". Verified against a headless orbital sim (scratchpad/rdv_sim.py)
 * for the Crew-2 geometry (chaser 200 km, ISS 420 km, 51.6 deg) before a line of this was written -
 * CLAUDE.md "simulate before you write".
 *
 * ---- THE PROFILE (Shuttle/Dragon heritage, the burns real ops actually flies) ----
 *   NC  (phasing)   raise the low insertion orbit's period so the chaser CLOSES the phase angle and
 *                   ARRIVES a controlled distance BEHIND the target. Fired at a computed lead angle.
 *   NSR (co-elliptic) circularise a fixed height dH BELOW the target - a concentric orbit that catches
 *                   up SLOWLY (~0.8 deg/hr at 15 km) and predictably, the stable platform the terminal
 *                   phase starts from. This is the piece the old ladder lacked: it matched the station's
 *                   exact altitude and then phased CO-ORBITAL, which never converges cleanly.
 *   Ti  (terminal init) when the target rises to a fixed ELEVATION ANGLE above the chaser's local
 *                   horizontal (27.5 deg, the Shuttle/Dragon value), transfer up to intercept an offset
 *                   point just below the station. The elevation trigger makes the terminal geometry
 *                   repeatable regardless of the exact phasing residual.
 *   -> hands off to the R-bar/V-bar L-approach (WaypointApproach) at the approach box.
 *
 * All burns are plain vis-viva apsis impulses (see Hohmann.cs); the glue turns the signed prograde dv
 * and the fire time into a world-frame node for the executor. Everything here is coplanar orbital
 * mechanics - the out-of-plane (NPC) correction is a separate cross-track burn the glue owns.
 */
namespace DragonScreen
{
    public enum RdvLeg
    {
        /// <summary>Nothing to do / no target.</summary>
        Idle,
        /// <summary>NC: closing the phase angle in the low orbit, waiting for the transfer lead angle.</summary>
        Phasing,
        /// <summary>Coasting the NC transfer ellipse up to the co-elliptic apsis.</summary>
        Transfer,
        /// <summary>On the co-elliptic orbit dH below the target, drifting to the Ti elevation angle.</summary>
        Coelliptic,
        /// <summary>Ti fired: coasting the terminal transfer to the approach box.</summary>
        Terminal,
        /// <summary>At the approach box below the station - hand to the L-approach.</summary>
        Arrived
    }

    /// <summary>Live rendezvous geometry, all from the two orbits. Near-circular coplanar assumed;
    /// the glue handles the plane separately.</summary>
    public struct RdvInputs
    {
        public double Mu;
        public double ChaserRadiusM;      // current orbital radius (≈ circular)
        public double ChaserSmaM;         // current semi-major axis
        public double TargetRadiusM;      // target circular radius
        /// <summary>Target true longitude minus chaser's, degrees in (-180,180]. POSITIVE = target AHEAD.</summary>
        public double PhaseAngleDeg;
        /// <summary>Slant range to the target, metres.</summary>
        public double RangeM;
        /// <summary>Our periapsis and the floor it may never cross, metres.</summary>
        public double PeriapsisM, FloorM;
    }

    /// <summary>What to do this tick. The glue reads Leg + (Burn, DvMps, FireNow) and either plans the
    /// node or keeps coasting.</summary>
    public struct RdvPlan
    {
        public RdvLeg Leg;
        /// <summary>The named burn to plan now, or "" to coast. NC / NSR / Ti.</summary>
        public string Burn;
        /// <summary>Signed prograde dv for the burn, m/s (apply along velocity at the apsis).</summary>
        public double DvMps;
        /// <summary>The burn is due now (the geometry gate is met).</summary>
        public bool FireNow;
        /// <summary>Refused because the burn would breach the periapsis floor.</summary>
        public bool FloorBlocked;
        public string Note;
    }

    public static class NamedRendezvous
    {
        // ---- profile constants (heritage values; tune from a flight) ----
        /// <summary>Co-elliptic height below the target, metres. NSR circularises here. 15 km is a
        /// standard co-elliptic offset - far enough to be a stable catch-up (~0.8 deg/hr), close enough
        /// that Ti is a small burn.</summary>
        public const double CoellipticDhM = 15000.0;

        /// <summary>Terminal-initiation elevation angle, degrees. Ti fires when the target rises to this
        /// angle above the chaser's local horizontal. 27.5 deg is the Shuttle/Dragon value.</summary>
        public const double TiElevationDeg = 27.5;

        /// <summary>Ti transfers to this height below the target - the approach box the L-approach takes
        /// over at. ~2 km below is inside the approach ellipsoid, above the 400 m R-bar first waypoint.</summary>
        public const double ApproachBoxDhM = 2000.0;

        /// <summary>Fire NC to arrive on the co-elliptic with the target still AHEAD by this much, so the
        /// slow co-elliptic drift then closes it to the Ti elevation point rather than overshooting.
        /// ~1 deg ≈ one co-elliptic lap of drift before Ti.</summary>
        public const double CoellipticArriveAheadDeg = 1.0;

        /// <summary>Phase-angle tolerance for firing NC, degrees.</summary>
        public const double PhaseGateDeg = 0.6;

        /// <summary>The co-elliptic radius for a given target radius.</summary>
        public static double CoellipticRadius(double rTarget) { return rTarget - CoellipticDhM; }

        /// <summary>Mean motion (rad/s) of a circular orbit at radius r.</summary>
        public static double MeanMotion(double r, double mu)
        {
            if (r <= 0.0 || mu <= 0.0) return 0.0;
            return System.Math.Sqrt(mu / (r * r * r));
        }

        /// <summary>Half-period (transfer time) of a Hohmann from rFrom to rTo, seconds.</summary>
        public static double TransferTimeS(double rFrom, double rTo, double mu)
        {
            double a = 0.5 * (rFrom + rTo);
            if (a <= 0.0 || mu <= 0.0) return 0.0;
            return System.Math.PI * System.Math.Sqrt(a * a * a / mu);
        }

        /// <summary>
        /// The phase angle (target AHEAD of chaser, degrees) at which to fire the NC raise so the chaser
        /// arrives at the co-elliptic radius with the target ahead by <paramref name="arriveAheadDeg"/>.
        ///
        /// Geometry: the chaser sweeps 180 deg on the transfer ellipse in time t_H; the target sweeps
        /// n_tgt·t_H. For the chaser to arrive `arriveAhead` behind the target:
        ///   180 = phase + targetSweep - arriveAhead   ->   phase = 180 - targetSweep + arriveAhead.
        /// (All degrees. Result normalised to (0,360).)
        /// </summary>
        public static double NcLeadAngleDeg(double rFrom, double rTo, double rTarget, double mu,
                                            double arriveAheadDeg)
        {
            double tH = TransferTimeS(rFrom, rTo, mu);
            double targetSweepDeg = MeanMotion(rTarget, mu) * tH * 180.0 / System.Math.PI;
            double lead = 180.0 - targetSweepDeg + arriveAheadDeg;
            while (lead < 0.0) lead += 360.0;
            while (lead >= 360.0) lead -= 360.0;
            return lead;
        }

        /// <summary>Along-track lead of the target (metres, ahead) at which its elevation above the
        /// chaser's local horizontal equals <paramref name="elevDeg"/>, given the height difference
        /// <paramref name="dhM"/>. tan(elev) = dh / alongTrack.</summary>
        public static double AlongTrackForElevation(double dhM, double elevDeg)
        {
            double t = System.Math.Tan(elevDeg * System.Math.PI / 180.0);
            return (t > 1e-6) ? dhM / t : 0.0;
        }

        /// <summary>Target elevation (degrees) above the chaser's local horizontal for a height
        /// difference dh and an along-track separation x (both metres, target ahead+above).</summary>
        public static double ElevationDeg(double dhM, double alongTrackM)
        {
            return System.Math.Atan2(dhM, System.Math.Max(1.0, alongTrackM)) * 180.0 / System.Math.PI;
        }

        /// <summary>Would a burn to transfer-SMA <paramref name="aNew"/> drop periapsis below the floor?
        /// A raise never does; a lower might. Guards NC/Ti like the old ladder guarded every burn.</summary>
        public static bool BreachesFloor(RdvInputs s, double aNew, double rBurn)
        {
            // periapsis of the new orbit = 2*aNew - apoapsis; with the burn at rBurn the opposite apsis is
            // 2*aNew - rBurn. The lower of (rBurn, 2*aNew - rBurn) is the new periapsis.
            double other = 2.0 * aNew - rBurn;
            double newPeri = System.Math.Min(rBurn, other);
            return newPeri < s.FloorM;
        }

        /// <summary>
        /// The plan for this tick. Pure state machine over the named-burn profile; the glue supplies the
        /// live geometry and executes whatever burn comes back FireNow.
        /// </summary>
        public static RdvPlan Plan(RdvInputs s, RdvLeg leg)
        {
            RdvPlan p = new RdvPlan();
            p.Leg = leg;

            double rTgt = s.TargetRadiusM;
            double rCo = CoellipticRadius(rTgt);
            double mu = s.Mu;

            switch (leg)
            {
                case RdvLeg.Idle:
                case RdvLeg.Phasing:
                {
                    p.Leg = RdvLeg.Phasing;
                    // NC: fire the raise to the co-elliptic radius when the phase angle reaches the lead
                    // angle. Phase is target-ahead; the low chaser catches up, so phase decreases toward it.
                    double lead = NcLeadAngleDeg(s.ChaserRadiusM, rCo, rTgt, mu, CoellipticArriveAheadDeg);
                    double phase = s.PhaseAngleDeg;
                    if (phase < 0.0) phase += 360.0;          // 0..360, target ahead
                    double gap = phase - lead;                 // >0 means still closing toward the lead
                    while (gap < -180.0) gap += 360.0;
                    while (gap > 180.0) gap -= 360.0;

                    if (System.Math.Abs(gap) <= PhaseGateDeg)
                    {
                        double dv = Hohmann.RaiseOppositeApsisDv(s.ChaserRadiusM, s.ChaserSmaM, rCo, mu);
                        double aNew = Hohmann.TransferSma(s.ChaserRadiusM, rCo);
                        if (BreachesFloor(s, aNew, s.ChaserRadiusM))
                        { p.FloorBlocked = true; p.Note = "NC blocked - periapsis floor"; return p; }
                        p.Burn = "NC"; p.DvMps = dv; p.FireNow = true;
                        p.Note = "NC phasing raise to co-elliptic";
                        return p;
                    }
                    p.Note = "phasing - " + gap.ToString("F1") + " deg to the NC lead angle";
                    return p;
                }

                case RdvLeg.Transfer:
                {
                    // Coasting the NC ellipse up to the co-elliptic apsis; the glue fires NSR at apoapsis.
                    p.Leg = RdvLeg.Transfer;
                    p.Note = "coasting to co-elliptic apoapsis for NSR";
                    return p;
                }

                case RdvLeg.Coelliptic:
                {
                    p.Leg = RdvLeg.Coelliptic;
                    // Drift on the co-elliptic until the target rises to the Ti elevation angle, then Ti.
                    double alongForTi = AlongTrackForElevation(CoellipticDhM, TiElevationDeg);
                    // current along-track from the phase angle (target ahead), at the target radius
                    double along = s.PhaseAngleDeg * System.Math.PI / 180.0 * rTgt;   // small-angle arc
                    if (along <= 0.0) { p.Note = "co-elliptic - target behind, waiting"; return p; }
                    if (along <= alongForTi + 200.0)
                    {
                        // Ti: raise from the co-elliptic circular orbit to the approach box below the target.
                        double rBox = rTgt - ApproachBoxDhM;
                        double aCo = rCo;   // circular
                        double dv = Hohmann.RaiseOppositeApsisDv(rCo, aCo, rBox, mu);
                        p.Burn = "Ti"; p.DvMps = dv; p.FireNow = true;
                        p.Note = "Ti terminal initiation at " + TiElevationDeg.ToString("F1") + " deg elevation";
                        return p;
                    }
                    p.Note = "co-elliptic drift - " + ((along - alongForTi) / 1000.0).ToString("F1")
                           + " km to the Ti point";
                    return p;
                }

                case RdvLeg.Terminal:
                {
                    p.Leg = (s.RangeM <= ApproachBoxDhM * 1.5) ? RdvLeg.Arrived : RdvLeg.Terminal;
                    p.Note = (p.Leg == RdvLeg.Arrived)
                           ? "at the approach box - hand to the L-approach"
                           : "coasting the Ti transfer to the approach box";
                    return p;
                }

                default:
                    p.Leg = RdvLeg.Arrived;
                    p.Note = "arrived";
                    return p;
            }
        }
    }
}
