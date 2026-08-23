/*
 * DragonScreen - LaunchAzimuth
 *
 * PURE. The heading to launch on to reach a target orbital inclination from a given launch latitude.
 *
 * ---- WHY THIS EXISTS: 'heading 90' IS A KERBIN ARTEFACT ----
 * The ascent autopilot has always engaged with a hard-coded due-east heading, because the stock
 * Space X Station sits at 0.133 deg inclination over an equatorial pad - due east IS the plane.
 * `docs/REAL_CREW_DRAGON_MISSION.md`: at the real ISS's 51.6 deg, launched from LC-39A at 28.6 N,
 * due east misses the plane by ~23 deg, and no amount of gravity turn recovers a plane error. Crew-1
 * needs a real azimuth. This is the RSS/Crew-1 piece; the stock path keeps heading ~90 because the
 * same formula returns ~90 for a near-equatorial inclination from a near-equatorial pad.
 *
 * ---- THE FORMULA, AND WHY THERE ARE TWO OF THEM ----
 * Spherical trig fixes the INERTIAL azimuth relative to the stars:
 *
 *      sin(beta_inertial) = cos(inclination) / cos(latitude)          [azimuth clockwise from north]
 *
 * But the pad is already moving east with the planet's spin, so the heading actually flown - relative
 * to the ground - is rotated off the inertial one. Real launch providers steer the GROUND azimuth,
 * which is the inertial velocity minus the pad's eastward surface velocity, re-expressed as a heading.
 * The correction is a couple of degrees at LEO from the Cape and it is the difference between hitting
 * the plane and having to dog-leg for it, so both are here and the glue flies the ground one.
 *
 * Returns the ASCENDING-node (north-of-east) azimuth. The descending alternative for the same
 * inclination is (180 - beta); Crew-1 flies ascending, north-east out of the Cape.
 */
using System;

namespace DragonScreen
{
    public static class LaunchAzimuth
    {
        private const double Deg2Rad = Math.PI / 180.0;
        private const double Rad2Deg = 180.0 / Math.PI;

        /// <summary>
        /// Inertial launch azimuth, degrees clockwise from north, for the ascending node.
        ///
        /// `sin(beta) = cos(i)/cos(lat)`. A target inclination below the launch latitude is
        /// unreachable directly - the ratio exceeds 1 - so it is clamped to give due east (90),
        /// the lowest inclination a direct ascent from that latitude can reach (inclination = |lat|).
        /// A retrograde target (i > 90) returns a heading west of north, which is correct.
        /// </summary>
        public static double InertialHeadingDeg(double inclinationDeg, double latitudeDeg)
        {
            double cosLat = Math.Cos(latitudeDeg * Deg2Rad);
            if (Math.Abs(cosLat) < 1e-9) return 90.0;      // at the pole every heading is "east"

            double s = Math.Cos(inclinationDeg * Deg2Rad) / cosLat;
            if (s > 1.0) s = 1.0;
            if (s < -1.0) s = -1.0;
            return Math.Asin(s) * Rad2Deg;                 // [-90, 90] from north; +ve = north-east
        }

        /// <summary>
        /// Eastward speed of the launch site due to the body's rotation, m/s.
        /// `2*pi*R*cos(lat) / rotationPeriod`. This is the free velocity a prograde launch keeps.
        /// </summary>
        public static double SurfaceEastwardSpeedMps(double bodyRadiusM, double bodyRotationPeriodS,
                                                     double latitudeDeg)
        {
            if (bodyRotationPeriodS <= 0.0) return 0.0;
            return 2.0 * Math.PI * bodyRadiusM * Math.Cos(latitudeDeg * Deg2Rad) / bodyRotationPeriodS;
        }

        /// <summary>
        /// Ground-relative launch azimuth, degrees clockwise from north in [0, 360) - the heading to
        /// actually fly. The inertial velocity for the plane is `orbitalSpeed` at the inertial azimuth;
        /// subtracting the pad's eastward surface speed and re-taking the heading gives the ground one.
        ///
        /// At the equator with a fast-spinning body the correction is large; from the Cape into the
        /// ISS plane it pulls the heading a couple of degrees north of the inertial 45.
        /// </summary>
        public static double GroundHeadingDeg(double inclinationDeg, double latitudeDeg,
                                              double orbitalSpeedMps, double surfaceEastwardSpeedMps)
        {
            double betaI = InertialHeadingDeg(inclinationDeg, latitudeDeg) * Deg2Rad;
            double east = orbitalSpeedMps * Math.Sin(betaI) - surfaceEastwardSpeedMps;
            double north = orbitalSpeedMps * Math.Cos(betaI);
            double h = Math.Atan2(east, north) * Rad2Deg;   // atan2(east, north) = heading from north
            return (h < 0.0) ? h + 360.0 : h;
        }
    }
}
