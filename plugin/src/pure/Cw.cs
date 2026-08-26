// DragonScreen — Cw  (autopilot rebuild L3 rendezvous: Clohessy-Wiltshire terminal targeting)
// ============================================================================================
// Clohessy-Wiltshire (Hill) relative motion about a circular-orbit target, in its LVLH frame, and the
// two-impulse transfer that flies the chaser from one relative point to another in a chosen time — the
// terminal legs of the rendezvous (Transfer / Co-elliptic / Approach-Initiation / Midcourse). Built
// fresh from the closed-form state-transition matrix (PHASE_3_RENDEZVOUS_RESEARCH §4.2/§4b). OFFSET
// TARGETING is mandatory: aim a point OFFSET from the station so a missed arrival burn drifts clear of
// the keep-out sphere — the free-drift check enforces it (passive abort).
//     ẍ = 3n²x + 2nẏ,  ÿ = −2nẋ,  z̈ = −n²z   (x radial, y along-track, z cross-track)
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct CwSolution
    {
        public bool Ok;
        public double Vx1, Vy1, Vz1;   // relative velocity just AFTER the departure burn (LVLH)
        public double Dvx1, Dvy1, Dvz1; // the departure burn Δv (Vx1 − current velocity)
        public double Dvx2, Dvy2, Dvz2; // the arrival burn Δv to null the relative velocity (station-keep)
        public double TofS;
    }

    public static class Cw
    {
        // Free-drift under CW dynamics: propagate (r0, v0) by time t → position (x,y,z). Used for the
        // passive-abort check (does an un-burned arrival stay outside the KOS) and to verify the STM.
        public static void FreeDrift(double x0, double y0, double z0, double vx0, double vy0, double vz0,
                                     double n, double t, out double x, out double y, out double z)
        {
            double nt = n * t, c = Math.Cos(nt), s = Math.Sin(nt);
            x = (4 - 3 * c) * x0 + (s / n) * vx0 + (2 * (1 - c) / n) * vy0;
            y = 6 * (s - nt) * x0 + y0 + (2 * (c - 1) / n) * vx0 + ((4 * s - 3 * nt) / n) * vy0;
            z = c * z0 + (s / n) * vz0;
        }

        // Two-impulse transfer: from relative position (x0,y0,z0) and current relative velocity
        // (vx0,vy0,vz0), reach the target offset (xf,yf,zf) in time tof. Solves the velocity needed just
        // after the burn (Φrv⁻¹·(rf − Φrr·r0)), the departure Δv, and the arrival Δv that nulls the
        // relative velocity (→ station-keep at the aim point).
        public static CwSolution TwoImpulse(double x0, double y0, double z0,
                                            double vx0, double vy0, double vz0,
                                            double xf, double yf, double zf, double n, double tof)
        {
            CwSolution r = new CwSolution(); r.TofS = tof;
            if (n <= 0.0 || tof <= 0.0) return r;
            double nt = n * tof, c = Math.Cos(nt), s = Math.Sin(nt);

            // Φrr · r0
            double rrx = (4 - 3 * c) * x0;
            double rry = 6 * (s - nt) * x0 + y0;
            double rrz = c * z0;
            // rhs = rf − Φrr·r0
            double bx = xf - rrx, by = yf - rry, bz = zf - rrz;

            // Φrv (in-plane 2×2 block + scalar z):
            double a = s / n, b = 2 * (1 - c) / n;
            double cc = 2 * (c - 1) / n, d = (4 * s - 3 * nt) / n;
            double e = s / n;
            double det = a * d - b * cc;
            if (Math.Abs(det) < 1e-12 || Math.Abs(e) < 1e-12) return r;   // singular tof (period multiple)

            // v0+ = Φrv⁻¹ · rhs
            double vx1 = (d * bx - b * by) / det;
            double vy1 = (-cc * bx + a * by) / det;
            double vz1 = bz / e;
            r.Vx1 = vx1; r.Vy1 = vy1; r.Vz1 = vz1;
            r.Dvx1 = vx1 - vx0; r.Dvy1 = vy1 - vy0; r.Dvz1 = vz1 - vz0;

            // arrival relative velocity = Φvr·r0 + Φvv·v0+   (null it → station-keep)
            double vrx = 3 * n * s * x0;
            double vry = 6 * n * (c - 1) * x0;
            double vrz = -n * s * z0;
            double avx = vrx + (c * vx1 + 2 * s * vy1);
            double avy = vry + (-2 * s * vx1 + (4 * c - 3) * vy1);
            double avz = vrz + (c * vz1);
            r.Dvx2 = -avx; r.Dvy2 = -avy; r.Dvz2 = -avz;
            r.Ok = true;
            return r;
        }

        public static double DvMag(double x, double y, double z) { return Math.Sqrt(x * x + y * y + z * z); }
        public static double TotalDv(CwSolution s)
        {
            return DvMag(s.Dvx1, s.Dvy1, s.Dvz1) + DvMag(s.Dvx2, s.Dvy2, s.Dvz2);
        }

        // Minimum range to the station over a free-drift (passive-abort safety): if the arrival burn is
        // never made, does the coast stay outside the keep-out sphere? Samples the drift over one period.
        public static double FreeDriftMinRangeM(double x0, double y0, double z0,
                                                double vx0, double vy0, double vz0,
                                                double n, int samples, double horizonS)
        {
            double best = double.MaxValue;
            for (int i = 0; i <= samples; i++)
            {
                double t = horizonS * i / samples;
                double x, y, z;
                FreeDrift(x0, y0, z0, vx0, vy0, vz0, n, t, out x, out y, out z);
                double d = Math.Sqrt(x * x + y * y + z * z);
                if (d < best) best = d;
            }
            return best;
        }
    }
}
