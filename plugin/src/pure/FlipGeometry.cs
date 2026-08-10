/*
 * DragonScreen - FlipGeometry
 *
 * PURE. Where the booster's nose must END UP after the turnaround, and which axis to swing it about.
 * Ported from `SPACEX/BOOSTER.ks:295 Flip1`, the three lines that set up the rotation:
 *
 *     local tangentVector is vxcl(up:vector, srfretrograde:vector:normalized):normalized.
 *     local rotateVector  is vcrs(tangentVector, body:position:normalized):normalized.
 *     local finalVector   is (-tangentVector * angleAxis(finalAttitude, rotateVector)):normalized.
 *
 * ---- ⛔ THIS EXISTS BECAUSE THE GLUE GOT THE SIGN WRONG AND IT COST A FLIGHT ----
 * The 08:40 recording: the flip completed at MET 255 pointing flat PROGRADE. `BoostbackKill`, which
 * follows it immediately, aims flat RETROGRADE - so the stage inherited a **149.7° attitude error at
 * the instant three Merlins came up to full throttle**. It tumbled at ±0.85 rad/s for the whole burn,
 * spent 24 tonnes of propellant, drove itself 15 km FURTHER downrange (55.7 → 70.4 km) during the
 * manoeuvre whose entire job is to reverse downrange velocity, ran the tanks dry at 11 km, and was
 * destroyed. Flip1's own comment predicts it exactly: *"A single big command makes the steering
 * manager saturate and the stage tumbles."*
 *
 * The cause was one word. F9I builds its tangent from **`srfretrograde`**; the port built it from
 * `v.srf_velocity`, which is PROgrade. Both then negate and rotate 180°, so F9I finishes retrograde
 * and the port finished prograde - a perfect inversion that looks right in every line it is written
 * on. The rotation AXIS came out reversed by the same slip.
 *
 * ---- ⚠ WHY IT IS PURE, WHEN THE PROJECT SAYS PURE HAS NO VECTORS ----
 * It is components in and components out precisely so this can be TESTED. The invariant is one line -
 * **a 180° RTLS flip must finish pointing where the boostback begins** - and no test could state it
 * while the arithmetic lived inside a method that takes a `Vessel`. That is the whole reason the bug
 * survived a line-by-line audit of §4.
 */
namespace DragonScreen
{
    public static class FlipGeometry
    {
        /// <summary>
        /// Set up the turnaround.
        ///
        /// <para><paramref name="flipDeg"/> is 180 for RTLS - fully reversed - and 170 for a
        /// droneship, which only has to point back far enough to trim.</para>
        ///
        /// <para>`retro*` is the ground track FLATTENED ONTO THE HORIZON and reversed: the direction
        /// the boostback will burn in, and the reference everything else here is built from.
        /// `axis*` is perpendicular to both it and "down", so rotating about it swings the nose
        /// through the vertical PLANE OF FLIGHT and never yaws the stage sideways. `final*` is where
        /// the nose ends up.</para>
        ///
        /// Returns false if the vehicle is too slow for a ground track to mean anything, in which
        /// case none of the outputs are usable.
        /// </summary>
        public static bool Solve(double upX, double upY, double upZ,
                                 double velX, double velY, double velZ,
                                 double flipDeg,
                                 out double retroX, out double retroY, out double retroZ,
                                 out double axisX, out double axisY, out double axisZ,
                                 out double finalX, out double finalY, out double finalZ)
        {
            retroX = 0.0; retroY = 0.0; retroZ = 0.0;
            axisX = 0.0; axisY = 0.0; axisZ = 0.0;
            finalX = 0.0; finalY = 0.0; finalZ = 0.0;

            double un = Norm(upX, upY, upZ);
            if (un < 1e-9) return false;
            upX /= un; upY /= un; upZ /= un;

            // ---- THE TANGENT IS RETROGRADE, NOT PROGRADE. See the header. ----
            // vxcl(up, srfretrograde): the reversed velocity, flattened onto the horizon.
            double rx = -velX, ry = -velY, rz = -velZ;
            double dot = rx * upX + ry * upY + rz * upZ;
            rx -= dot * upX; ry -= dot * upY; rz -= dot * upZ;
            double rn = Norm(rx, ry, rz);
            if (rn < 1.0) return false;               // no usable ground track
            retroX = rx / rn; retroY = ry / rn; retroZ = rz / rn;

            // vcrs(tangent, down). `body:position` in kOS points FROM the ship TO the body: down.
            double dx = -upX, dy = -upY, dz = -upZ;
            Cross(retroX, retroY, retroZ, dx, dy, dz, out axisX, out axisY, out axisZ);
            double an = Norm(axisX, axisY, axisZ);
            if (an < 1e-9) return false;              // straight up or straight down
            axisX /= an; axisY /= an; axisZ /= an;

            // -tangentVector rotated by flipDeg. Starting from flat PROGRADE and turning 180° lands
            // on flat retrograde - which is where BoostbackKill takes over.
            Rotate(-retroX, -retroY, -retroZ, axisX, axisY, axisZ, flipDeg,
                   out finalX, out finalY, out finalZ);
            double fn = Norm(finalX, finalY, finalZ);
            if (fn < 1e-9) return false;
            finalX /= fn; finalY /= fn; finalZ /= fn;
            return true;
        }

        /// <summary>Rodrigues rotation of v about a UNIT axis k by `deg` degrees.</summary>
        public static void Rotate(double vx, double vy, double vz,
                                  double kx, double ky, double kz, double deg,
                                  out double ox, out double oy, out double oz)
        {
            double a = deg * System.Math.PI / 180.0;
            double c = System.Math.Cos(a), s = System.Math.Sin(a);
            double cx, cy, cz;
            Cross(kx, ky, kz, vx, vy, vz, out cx, out cy, out cz);
            double kv = kx * vx + ky * vy + kz * vz;
            ox = vx * c + cx * s + kx * kv * (1.0 - c);
            oy = vy * c + cy * s + ky * kv * (1.0 - c);
            oz = vz * c + cz * s + kz * kv * (1.0 - c);
        }

        /// <summary>Angle between two vectors, degrees. For the tests and the logs.</summary>
        public static double AngleDeg(double ax, double ay, double az,
                                      double bx, double by, double bz)
        {
            double na = Norm(ax, ay, az), nb = Norm(bx, by, bz);
            if (na < 1e-12 || nb < 1e-12) return 0.0;
            double c = (ax * bx + ay * by + az * bz) / (na * nb);
            if (c > 1.0) c = 1.0; else if (c < -1.0) c = -1.0;
            return System.Math.Acos(c) * 180.0 / System.Math.PI;
        }

        private static void Cross(double ax, double ay, double az,
                                  double bx, double by, double bz,
                                  out double ox, out double oy, out double oz)
        {
            ox = ay * bz - az * by;
            oy = az * bx - ax * bz;
            oz = ax * by - ay * bx;
        }

        private static double Norm(double x, double y, double z)
        {
            return System.Math.Sqrt(x * x + y * y + z * z);
        }
    }
}
