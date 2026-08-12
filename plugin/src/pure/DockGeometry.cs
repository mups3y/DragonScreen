/*
 * DragonScreen - DockGeometry
 *
 * PURE. The scalar geometry of arriving at a docking port without hitting the station. Ported from
 * `falcon9.ks:10700-10765` (`FalconGateVector`, `FalconPathClear`, `FalconSkirtVector`) and
 * `station_ops.ks:369 StClosestPort`.
 *
 * ---- ⛔ THE KEEP-OUT SPHERE IS CENTRED ON THE STATION, NOT ON THE PORT ----
 * `falcon-station-ferry`: the berths sit at the TIPS OF THE ARMS, and the keep-out radius is measured
 * from the station's centre. So "stand off N metres from the port" would put the gate roughly two
 * arm-lengths clear and make the capsule fly a long way for nothing. The gate is instead the point
 * where the port's own outward axis LEAVES the sphere:
 *
 *      |port + d*u - centre|² = R²   ->   d² - 2d(u·c) + (|c|² - R²) = 0
 *
 * and the exit is the larger root. The port is always inside the sphere - R is the furthest part plus
 * a pad - so the discriminant is positive and that root is non-negative.
 *
 * ---- ⛔ AND THE SPHERE TEST ONLY MEANS ANYTHING FROM OUTSIDE THE SPHERE ----
 * Written without that guard the path test DEADLOCKS, and it did: the segment starts at our own
 * position, so once we are at or inside radius R the closest approach is trivially ≤ R and the answer
 * is "blocked" wherever we point. Paired with a skirt target sitting exactly ON the sphere, the
 * approach converged to the surface and sat there reporting "rounding hull" at 0.03 m/s, 16.8 m from
 * the station centre, going nowhere.
 *
 * Inside the sphere the bounding sphere is simply the wrong model - a capsule 25 m off a berth is
 * inside the hull's bounding radius and in completely free space. Down there the PORT-AXIS geometry
 * governs: fly to the standoff point, then straight down the axis.
 *
 * ---- WHY THE SKIRT AIMS AT R + PAD AND NOT AT R ----
 * A target exactly on the surface leaves us exactly on the surface, which is the boundary case the
 * path test can never call clear. Aiming outside the sphere gives the test room to flip back to
 * clear. That is hysteresis, and its absence is what produced the stuck loop above.
 */
namespace DragonScreen
{
    public static class DockGeometry
    {
        /// <summary>Metres directly out from the port to hold before the final axial run. 25 m.</summary>
        public const double StandoffM = 25.0;

        /// <summary>How close to the standoff point counts as arrived, metres.</summary>
        public const double StandoffToleranceM = 12.0;

        /// <summary>Added to the measured hull radius, metres. TUNABLE.</summary>
        public const double KeepOutPadM = 20.0;

        /// <summary>
        /// Distance along the port's outward axis at which that axis leaves the keep-out sphere,
        /// plus the pad - the GATE. Never less than the plain standoff.
        ///
        /// <paramref name="cDotU"/> is u·c and <paramref name="cSqrMag"/> is |c|², where c runs from
        /// the PORT to the station CENTRE and u is the port's outward axis. The caller does those two
        /// dot products; everything else is scalar.
        /// </summary>
        public static double GateDistanceM(double cDotU, double cSqrMag, double keepOutRadiusM)
        {
            double disc = cDotU * cDotU - cSqrMag + keepOutRadiusM * keepOutRadiusM;
            if (disc <= 0.0) return StandoffM;          // no intersection; the plain standoff will do
            double exit = cDotU + System.Math.Sqrt(disc) + KeepOutPadM;
            return (exit > StandoffM) ? exit : StandoffM;
        }

        /// <summary>
        /// Closest approach of the sphere centre to the segment from us to a target offset.
        ///
        /// <paramref name="cSqrMag"/> is |c|² and <paramref name="cDotU"/> is c·û, with c the vector
        /// from us to the station centre and û the unit direction of the segment. The projection is
        /// clamped to the segment: an obstacle behind us, or beyond the target, is not in the way.
        /// </summary>
        public static double ClosestApproachM(double cSqrMag, double cDotU, double segmentLenM)
        {
            double t = cDotU;
            if (t < 0.0) t = 0.0;
            if (t > segmentLenM) t = segmentLenM;
            double d2 = cSqrMag - 2.0 * t * cDotU + t * t;
            return (d2 > 0.0) ? System.Math.Sqrt(d2) : 0.0;
        }

        /// <summary>
        /// Does the straight line from here to the target stay outside the keep-out sphere?
        ///
        /// ⚠ Returns TRUE whenever we are already inside the sphere. That is not a loophole - see the
        /// header. Inside, the bounding sphere is the wrong model and the port-axis geometry governs.
        /// </summary>
        public static bool PathClear(double distanceToCentreM, double cSqrMag, double cDotU,
                                     double segmentLenM, double keepOutRadiusM)
        {
            if (keepOutRadiusM <= 0.0) return true;
            if (distanceToCentreM <= keepOutRadiusM) return true;     // the close-in regime
            if (segmentLenM < 0.001) return true;
            return ClosestApproachM(cSqrMag, cDotU, segmentLenM) > keepOutRadiusM;
        }

        /// <summary>
        /// How far out the skirt aims: the keep-out radius PLUS the pad, never the radius itself.
        /// See the header - aiming at the surface is what produced the stuck "rounding hull" loop.
        /// </summary>
        public static double SkirtRadiusM(double keepOutRadiusM)
        {
            return keepOutRadiusM + KeepOutPadM;
        }

        /// <summary>Arrived at the standoff point?</summary>
        /// ⛔ SUPERSEDED 2026-08-13 AND KEPT ONLY AS THE RECORD OF A BUG. NOTHING CALLS IT.
        /// This predicate WAS the 13 m stall: "within 12 m of a point 25 m out" is false at 13 m
        /// from the port, so Axial's entry condition failed BECAUSE AXIAL HAD WORKED. The final run
        /// is now gated on lateral error alone (`DockApproach.CorridorRadiusM`, 1 m), which cannot
        /// falsify itself by succeeding. Do not wire it back in.
        public static bool AtStandoff(double distanceToStandoffM)
        {
            return distanceToStandoffM < StandoffToleranceM;
        }

        /// <summary>
        /// Close enough to the gate to start down the corridor.
        ///
        /// ---- ⛔ THE LEG THIS TEST EXISTS FOR WAS MISSING, AND IT DEADLOCKED THE DOCKING. ----
        /// The stage machine was `if (AtStandoff) Axial; else ToGate`, with the gate as the target
        /// in every non-Axial case. So the capsule flew to the gate and then held it - because the
        /// gate WAS its target - while `AtStandoff` tested a point 20 m further in that nothing was
        /// ever going to take it to.
        ///
        /// Measured 2026-08-12, 1904 rows, every one of them `ToGate`: the capsule parked at the
        /// gate and oscillated between 31 and 48 m for six minutes across four attempts, holding
        /// its offsets to within 3 m the whole time. The controller was flying well. There was
        /// simply no instruction to go in.
        ///
        /// The geometry: `keepOutR` measured 31 m, `GateDistanceM` puts the gate at the sphere's
        /// exit plus `KeepOutPadM` (20 m) - about 45 m - and `StandoffM` is 25 m. 45 minus 25 is
        /// 20 m, outside `StandoffToleranceM` of 12. The deadlock was structural, not marginal.
        /// </summary>
        public static bool AtGate(double distanceToGateM)
        {
            return distanceToGateM < GateToleranceM;
        }

        /// <summary>
        /// How close counts as having arrived at the gate, metres.
        ///
        /// Wider than <see cref="StandoffToleranceM"/> on purpose: the gate is a waypoint to leave,
        /// not a position to hold, and the capsule arrives there with the approach's residual drift
        /// still being nulled. Requiring the same precision as the standoff would just move the
        /// deadlock 20 m further out.
        /// </summary>
        public const double GateToleranceM = 15.0;
    }
}
