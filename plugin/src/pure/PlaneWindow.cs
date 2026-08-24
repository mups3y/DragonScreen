/*
 * DragonScreen - PlaneWindow
 *
 * PURE. When does the launch site pass THROUGH the target's orbital plane? That instant is the launch
 * window for a rendezvous: launch then and the ascent inserts into the station's plane (matching its
 * RAAN), so only the along-track phase is left for the rendezvous to close.
 *
 * ---- WHY THIS EXISTS: WE WERE LAUNCHING ON PHASE, NOT PLANE ----
 * `LaunchWindowOps` targets the along-track PHASE angle only - correct for the stock Kerbin station at
 * 0.133 deg, where the plane is degenerate. In RSS the ISS is at 51.6 deg, and matching only the
 * inclination is not enough: the plane also has a RAAN, and you only fly into it at the moment the
 * rotating launch site crosses the plane. Miss that and you launch into a plane rotated tens of degrees
 * off the station's, which no amount of phasing can fix. This finds that moment.
 *
 * ---- THE MATH, IN THE INERTIAL FRAME ----
 * The launch site is IN the plane when its position vector is perpendicular to the plane's normal:
 * dot(r_site, n) = 0. The site rotates with the body about +Z at rate omega, so r_site(t) = Rz(omega t)
 * r_site(0), and dot(r_site(t), n) = A cos(omega t) + B sin(omega t) + C with
 *      A = n.x r.x + n.y r.y,   B = n.y r.x - n.x r.y,   C = n.z r.z.
 * The zeros of A cos + B sin + C are the two crossings per body-rotation (ascending/descending node
 * passes). We return the next one in the ASCENDING sense (site moving from the southern to the northern
 * side of the plane) for a standard northbound launch, or the descending one if asked.
 */
namespace DragonScreen
{
    public static class PlaneWindow
    {
        /// <summary>
        /// Seconds until the launch site next crosses the target plane. `north` picks the crossing that
        /// launches northbound (site going from below the plane to above it, dot rising through 0);
        /// false picks the descending crossing. Returns 0 if we are essentially on the plane already, and
        /// -1 if the geometry is degenerate (site on the axis, or the site never reaches the plane -
        /// impossible for |lat| <= inclination, which the caller guarantees by only using this when the
        /// site latitude is reachable).
        ///
        /// All vectors in the SAME inertial frame; `omega` is the body's rotation rate, rad/s (&gt;0).
        /// </summary>
        public static double SecondsToPlane(
            double rx, double ry, double rz,        // launch site position (inertial), any length
            double nx, double ny, double nz,        // target orbit plane normal (inertial), any length
            double omega, bool north)
        {
            if (omega <= 0.0) return -1.0;

            double A = nx * rx + ny * ry;
            double B = ny * rx - nx * ry;
            double C = nz * rz;
            double R = System.Math.Sqrt(A * A + B * B);
            if (R < 1e-9) return -1.0;                 // site on the rotation axis - never crosses

            // Already on the plane (within a hair) and moving the right way? window is now.
            // dot(t) = A cos + B sin + C; d(dot)/dt at t=0 = omega*(B) (since derivative of A cos is
            // -A sin -> 0 at 0, of B sin is B cos -> B). North launch wants dot rising through 0.
            // We still solve for the NEXT proper crossing rather than special-casing, for robustness.

            // Solve A cos th + B sin th = -C  ->  R sin(th + phi) = -C, phi = atan2(A, B).
            double ratio = -C / R;
            if (ratio > 1.0) ratio = 1.0;
            if (ratio < -1.0) ratio = -1.0;            // |C| slightly over R from rounding: clamp
            double phi = System.Math.Atan2(A, B);
            double asinv = System.Math.Asin(ratio);
            double twoPi = 2.0 * System.Math.PI;

            // Two solutions per revolution: th = asinv - phi  and  th = (pi - asinv) - phi.
            double th1 = Norm(asinv - phi, twoPi);
            double th2 = Norm(System.Math.PI - asinv - phi, twoPi);

            // Sign of d(dot)/dt at a crossing th: derivative = omega*(-A sin th + B cos th).
            // North launch = site rising through the plane = derivative > 0.
            double d1 = -A * System.Math.Sin(th1) + B * System.Math.Cos(th1);
            bool th1North = d1 > 0.0;

            double want;
            if (north)  want = th1North ? th1 : th2;
            else        want = th1North ? th2 : th1;

            double t = want / omega;
            // A crossing essentially at t=0 (already on the plane) returns ~0; otherwise the next one.
            if (t < 1e-6) t += twoPi / omega;
            return t;
        }

        /// <summary>Wrap an angle into [0, period).</summary>
        private static double Norm(double a, double period)
        {
            a %= period;
            if (a < 0.0) a += period;
            return a;
        }

        /// <summary>
        /// The plane normal (unit) for an orbit of inclination `incRad` and longitude-of-ascending-node
        /// `lanRad`, in the standard inertial frame (Z = north pole, X = reference longitude). This is
        /// the direction of the orbital angular momentum. Handy when the caller has orbital elements
        /// rather than a state vector.
        /// </summary>
        public static void NormalFromElements(double incRad, double lanRad,
                                              out double nx, out double ny, out double nz)
        {
            double si = System.Math.Sin(incRad), ci = System.Math.Cos(incRad);
            nx = System.Math.Sin(lanRad) * si;
            ny = -System.Math.Cos(lanRad) * si;
            nz = ci;
        }

        // ==================================================================================
        //  MechJeb launch-into-plane timing - PORTED VERBATIM from MechJebLib Astro.TimeToPlane.
        //
        //  This is the proven, in-game-validated launch window and it REPLACES the vector-normal
        //  crossing search above (which mixed a swizzled orbit normal with a world pad and ended up
        //  tracking the station's POSITION, not its plane - the user's "X"). The genius of MechJeb's
        //  version is that it is ALL SCALARS in KSP's own celestial frame: the pad's celestial
        //  longitude, and the target orbit's LAN and inclination straight off `orbit.LAN` /
        //  `orbit.inclination`. Nothing to mis-swizzle. Standard spherical trig (Napier).
        //
        //  TAILORED TO CREW DRAGON: pass +inc (never -inc). Our ascent always flies the ASCENDING
        //  (north-going, north-east) azimuth, so we want the north-going plane crossing every time -
        //  NOT MinimumTimeToPlane, which would sometimes pick the south-going (descending) pass that
        //  our fixed NE ascent cannot fly.
        // ==================================================================================
        private const double D2R = System.Math.PI / 180.0;
        private const double TAU = 2.0 * System.Math.PI;
        private const double PlaneEps = 1e-6;

        /// <summary>
        /// Seconds until the launch site rotates into the target plane, launching NORTH-going. Ported
        /// from MechJebLib Astro.TimeToPlane. All angles in degrees. `inc &gt; 0` = north-going (the
        /// ascending, north-east launch a Crew Dragon flies); a negative `inc` would ask for the
        /// south-going pass. A backwards-spinning body (rotationPeriod &lt; 0) is handled.
        /// </summary>
        public static double TimeToPlane(double rotationPeriodS, double latitudeDeg,
                                         double celestialLongitudeDeg, double lanDeg, double incDeg)
        {
            double latitude = latitudeDeg * D2R;
            double celestialLongitude = celestialLongitudeDeg * D2R;
            double lan = lanDeg * D2R;
            double inc = incDeg * D2R;

            // singularity at the poles where tan(lat) is infinite
            if (System.Math.Abs(System.Math.Abs(latitude) - System.Math.PI / 2.0) < PlaneEps) return 0.0;
            // equatorial target: longitude does not matter, launch now
            if (System.Math.Abs(inc) < PlaneEps || System.Math.Abs(System.Math.Abs(inc) - System.Math.PI) < PlaneEps)
                return 0.0;

            // Napier's rules for spherical trig; the clamped Asin is correct for |inc| < |lat|.
            double angleEastOfAN = SafeAsin(System.Math.Tan(latitude) / System.Math.Tan(System.Math.Abs(inc)));

            // South-going trajectories: the AN sits "behind" the planet, [90,270].
            if (inc < 0.0) angleEastOfAN = System.Math.PI - angleEastOfAN;

            double lanNow = celestialLongitude - angleEastOfAN;
            double lanDiff = lan - lanNow;

            if (rotationPeriodS < 0.0) lanDiff = -lanDiff;   // backwards-spinning body

            return Clamp2Pi(lanDiff) / TAU * System.Math.Abs(rotationPeriodS);
        }

        /// <summary>asin clamped to [-1,1] (MechJebLib Statics.SafeAsin).</summary>
        private static double SafeAsin(double x)
        {
            if (x < -1.0) x = -1.0; else if (x > 1.0) x = 1.0;
            return System.Math.Asin(x);
        }

        /// <summary>Wrap to [0, 2pi) (MechJebLib Statics.Clamp2Pi).</summary>
        private static double Clamp2Pi(double x)
        {
            x %= TAU;
            if (x < 0.0) x += TAU;
            return x >= TAU ? 0.0 : x;
        }
    }
}
