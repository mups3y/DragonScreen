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
        public static bool AtStandoff(double distanceToStandoffM)
        {
            return distanceToStandoffM < StandoffToleranceM;
        }
    }
}
