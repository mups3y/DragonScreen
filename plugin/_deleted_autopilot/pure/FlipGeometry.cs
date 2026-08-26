// DragonScreen - FlipGeometry
// ---- ⛔ THIS EXISTS BECAUSE THE GLUE GOT THE SIGN WRONG AND IT COST A FLIGHT ----
// ---- ⚠ WHY IT IS PURE, WHEN THE PROJECT SAYS PURE HAS NO VECTORS ----
namespace DragonScreen
{
    public static class FlipGeometry
    {
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
            double rx = -velX, ry = -velY, rz = -velZ;
            double dot = rx * upX + ry * upY + rz * upZ;
            rx -= dot * upX; ry -= dot * upY; rz -= dot * upZ;
            double rn = Norm(rx, ry, rz);
            if (rn < 1.0) return false;
            retroX = rx / rn; retroY = ry / rn; retroZ = rz / rn;

            double dx = -upX, dy = -upY, dz = -upZ;
            Cross(retroX, retroY, retroZ, dx, dy, dz, out axisX, out axisY, out axisZ);
            double an = Norm(axisX, axisY, axisZ);
            if (an < 1e-9) return false;
            axisX /= an; axisY /= an; axisZ /= an;

            Rotate(-retroX, -retroY, -retroZ, axisX, axisY, axisZ, flipDeg,
                   out finalX, out finalY, out finalZ);
            double fn = Norm(finalX, finalY, finalZ);
            if (fn < 1e-9) return false;
            finalX /= fn; finalY /= fn; finalZ /= fn;
            return true;
        }

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
