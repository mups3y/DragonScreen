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
    }
}
