// DragonScreen - CwTargeting
// ---- THE FRAME, AND WHY IT CANNOT BE GOT WRONG ----
// ---- THE TERM THAT IS NOT OPTIONAL ----
// ---- SCALARS IN, SCALARS OUT ----
// ---- ⚠ AND THE RULE THAT GOVERNS ITS USE ----
namespace DragonScreen
{
    public struct CwState
    {
        public double Rx, Ry, Rz;
        public double Vx, Vy, Vz;
        public double N;
    }

    public struct CwSolution
    {
        public bool Ok;
        public double DvX, DvY, DvZ;
        public double ArrivalRelSpeed;
        public double Vx1, Vy1, Vz1;
        public double MinFreeDriftRangeM;
        public bool PassiveAbortSafe;
        public string Note;
    }

    public static class CwTargeting
    {
        public const double DetFloor = 0.05;

        public const double SinFloor = 0.02;

        public static CwSolution Solve(CwState s, double tofS, double aimBehindM)
        {
            CwSolution r = new CwSolution();
            r.Ok = false;

            if (s.N <= 0.0 || tofS <= 0.0)
            {
                r.Note = "CW: no orbit period or no time of flight";
                return r;
            }

            double v0x = s.Vx + s.N * s.Ry;
            double v0y = s.Vy - s.N * s.Rx;
            double v0z = s.Vz;

            double tau = s.N * tofS;
            double sn = System.Math.Sin(tau);
            double cs = System.Math.Cos(tau);

            double det = 8.0 - 8.0 * cs - 3.0 * tau * sn;
            if (System.Math.Abs(det) < DetFloor || System.Math.Abs(sn) < SinFloor)
            {
                r.Note = "CW singular at this transfer time - no burn planned";
                return r;
            }

            double fx = (4.0 - 3.0 * cs) * s.Rx;
            double fy = 6.0 * (sn - tau) * s.Rx + s.Ry;
            double fz = cs * s.Rz;

            double bx = -fx;
            double by = -aimBehindM - fy;
            double bz = -fz;

            double w1x = s.N / det * ((4.0 * sn - 3.0 * tau) * bx - 2.0 * (1.0 - cs) * by);
            double w1y = s.N / det * (2.0 * (1.0 - cs) * bx + sn * by);
            double w1z = s.N * bz / sn;

            r.DvX = w1x - v0x;
            r.DvY = w1y - v0y;
            r.DvZ = w1z - v0z;

            r.Vx1 = w1x; r.Vy1 = w1y; r.Vz1 = w1z;
            r.PassiveAbortSafe = true;

            double avx = 3.0 * s.N * sn * s.Rx + cs * w1x + 2.0 * sn * w1y;
            double avy = -6.0 * s.N * (1.0 - cs) * s.Rx - 2.0 * sn * w1x + (4.0 * cs - 3.0) * w1y;
            double avz = -s.N * sn * s.Rz + cs * w1z;
            r.ArrivalRelSpeed = System.Math.Sqrt(avx * avx + avy * avy + avz * avz);

            r.Ok = true;
            return r;
        }

        public static double DvMagnitude(CwSolution r)
        {
            return System.Math.Sqrt(r.DvX * r.DvX + r.DvY * r.DvY + r.DvZ * r.DvZ);
        }

        // ================= PASSIVE ABORT (offset targeting made a proven property) =================

        public static void FreeDrift(double x0, double y0, double z0,
                                     double vx0, double vy0, double vz0,
                                     double n, double t,
                                     out double x, out double y, out double z)
        {
            double nt = n * t, s = System.Math.Sin(nt), c = System.Math.Cos(nt);
            x = (4.0 - 3.0 * c) * x0 + (s / n) * vx0 + (2.0 / n) * (1.0 - c) * vy0;
            y = 6.0 * (s - nt) * x0 + y0 - (2.0 / n) * (1.0 - c) * vx0 + ((4.0 * s - 3.0 * nt) / n) * vy0;
            z = c * z0 + (s / n) * vz0;
        }

        public static double FreeDriftMinRangeM(double x0, double y0, double z0,
                                                double vx0, double vy0, double vz0,
                                                double n, double horizonS, int samples)
        {
            if (n <= 0.0 || horizonS <= 0.0) return 0.0;
            if (samples < 1) samples = 1;
            double min = double.MaxValue;
            for (int i = 0; i <= samples; i++)
            {
                double t = horizonS * i / samples;
                double x, y, z;
                FreeDrift(x0, y0, z0, vx0, vy0, vz0, n, t, out x, out y, out z);
                double rr = System.Math.Sqrt(x * x + y * y + z * z);
                if (rr < min) min = rr;
            }
            return min;
        }

        public const int DefaultCoastSamples = 240;

        public static CwSolution Best(CwState s, double minTofS, double maxTofS, int steps,
                                      double aimBehindM, out double bestTofS)
        {
            return Best(s, minTofS, maxTofS, steps, aimBehindM, out bestTofS, 0.0, 0.0, 0);
        }

        public static CwSolution Best(CwState s, double minTofS, double maxTofS, int steps,
                                      double aimBehindM, out double bestTofS,
                                      double passiveSafeM, double coastCheckPeriodS, int coastSamples)
        {
            CwSolution best = new CwSolution();
            best.Ok = false;
            bestTofS = 0.0;
            if (steps < 1) steps = 1;
            if (coastSamples < 1) coastSamples = DefaultCoastSamples;
            bool filter = passiveSafeM > 0.0 && coastCheckPeriodS > 0.0;

            double bestSafeCost = double.MaxValue;
            double bestSafeTof = 0.0; CwSolution bestSafe = new CwSolution(); bool haveSafe = false;
            double bestMargin = -1.0;
            double bestMarginTof = 0.0; CwSolution bestMarginSol = new CwSolution(); bool haveAny = false;

            for (int i = 0; i <= steps; i++)
            {
                double tof = minTofS + (maxTofS - minTofS) * i / steps;
                CwSolution c = Solve(s, tof, aimBehindM);
                if (!c.Ok) continue;

                double margin = double.MaxValue;
                if (filter)
                {
                    margin = FreeDriftMinRangeM(s.Rx, s.Ry, s.Rz, c.Vx1, c.Vy1, c.Vz1,
                                                s.N, tof + coastCheckPeriodS, coastSamples);
                    c.MinFreeDriftRangeM = margin;
                    c.PassiveAbortSafe = margin >= passiveSafeM;
                }

                double cost = DvMagnitude(c);
                if (!haveAny || margin > bestMargin)
                { haveAny = true; bestMargin = margin; bestMarginTof = tof; bestMarginSol = c; }

                if (!filter || c.PassiveAbortSafe)
                {
                    if (cost < bestSafeCost) { bestSafeCost = cost; bestSafeTof = tof; bestSafe = c; haveSafe = true; }
                }
            }

            if (haveSafe)
            {
                best = bestSafe; bestTofS = bestSafeTof;
            }
            else if (haveAny)
            {
                best = bestMarginSol; bestTofS = bestMarginTof;
                if (filter)
                    best.Note = "no passively-safe transfer (closest free-drift "
                              + bestMargin.ToString("F0") + " m < " + passiveSafeM.ToString("F0")
                              + " m) - flew the safest available";
            }
            if (!best.Ok) best.Note = "no non-singular transfer in the window";
            return best;
        }
    }
}
