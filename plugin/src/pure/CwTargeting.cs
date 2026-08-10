/*
 * DragonScreen - CwTargeting
 *
 * PURE. Clohessy-Wiltshire two-impulse targeting, ported from `F9I/station_ops.ks:756-838 StCwSolve`.
 *
 * ---- THE FRAME, AND WHY IT CANNOT BE GOT WRONG ----
 * LVLH centred on the STATION: x radial out, y along-track (direction of motion), z normal. F9I
 * builds x and y WITHOUT a cross product so their handedness cannot be wrong, and takes z = x cross y.
 * The cross-track CW equation is decoupled and odd in z, so a sign flip there cancels between input
 * and output. That property is the reason the basis is built the way it is; keep it.
 *
 * ---- THE TERM THAT IS NOT OPTIONAL ----
 * The LVLH frame ROTATES with the station, so the CW state velocity is the INERTIAL relative velocity
 * minus omega x r, with omega = n*z. F9I's own note: "Skipping this term is not a small error: at
 * 50 km along-track it is n*50000 = 165 m/s, larger than the whole manoeuvre."
 *
 * ---- SCALARS IN, SCALARS OUT ----
 * The glue builds the basis and converts the answer back to world coordinates, because pure has no
 * vector type and no KSP. That is the same split the landing guidance uses: pure owns the law, glue
 * owns the geometry.
 *
 * ---- ⚠ AND THE RULE THAT GOVERNS ITS USE ----
 * `falcon-rendezvous-approach-law`: NEVER chase a co-orbital target. Pursuit steering de-orbited
 * flight 012. This solver exists precisely so the approach is a planned two-impulse transfer rather
 * than a chase, and every burn it produces must still be checked against a periapsis floor.
 */
namespace DragonScreen
{
    /// <summary>A CW state in the station's LVLH frame. Metres and metres per second.</summary>
    public struct CwState
    {
        /// <summary>Relative position: x radial out, y along-track, z normal.</summary>
        public double Rx, Ry, Rz;
        /// <summary>Relative INERTIAL velocity in the same axes. The solver removes omega x r.</summary>
        public double Vx, Vy, Vz;
        /// <summary>Mean motion of the STATION's orbit, rad/s - it is the frame's reference.</summary>
        public double N;
    }

    public struct CwSolution
    {
        /// <summary>False when the transfer time is singular - no burn is planned, not a bad one.</summary>
        public bool Ok;
        /// <summary>The impulse, in the same LVLH axes the state came in. Glue rotates it to world.</summary>
        public double DvX, DvY, DvZ;
        /// <summary>Relative speed on arrival. Sizes the braking burn and is worth reporting.</summary>
        public double ArrivalRelSpeed;
        public string Note;
    }

    public static class CwTargeting
    {
        /// <summary>
        /// Determinant floor. Below this the position-from-velocity block is not invertible and the
        /// answer would be an enormous burn built out of numerical noise.
        /// </summary>
        public const double DetFloor = 0.05;

        /// <summary>Sine floor, for the same reason on the cross-track axis.</summary>
        public const double SinFloor = 0.02;

        /// <summary>
        /// Solve for the impulse that puts us <paramref name="aimBehindM"/> metres behind the station
        /// after <paramref name="tofS"/> seconds.
        ///
        /// The aim point is BEHIND, on -y, and never zero: arriving exactly at the station is how a
        /// rendezvous becomes a collision. The approach ladder closes the last of it under RCS.
        /// </summary>
        public static CwSolution Solve(CwState s, double tofS, double aimBehindM)
        {
            CwSolution r = new CwSolution();
            r.Ok = false;

            if (s.N <= 0.0 || tofS <= 0.0)
            {
                r.Note = "CW: no orbit period or no time of flight";
                return r;
            }

            // The rotating-frame correction. See the header - at 50 km this is 165 m/s.
            double v0x = s.Vx + s.N * s.Ry;
            double v0y = s.Vy - s.N * s.Rx;
            double v0z = s.Vz;

            double tau = s.N * tofS;
            double sn = System.Math.Sin(tau);
            double cs = System.Math.Cos(tau);

            // Determinant of the position-from-velocity block: (8 - 8c - 3*tau*s)/n^2.
            double det = 8.0 - 8.0 * cs - 3.0 * tau * sn;
            if (System.Math.Abs(det) < DetFloor || System.Math.Abs(sn) < SinFloor)
            {
                // A singular transfer time is a REPORTED state, not a burn. F9I logs and plans
                // nothing rather than firing whatever the algebra produced.
                r.Note = "CW singular at this transfer time - no burn planned";
                return r;
            }

            // Where free drift alone would leave us: Phi_rr * r0.
            double fx = (4.0 - 3.0 * cs) * s.Rx;
            double fy = 6.0 * (sn - tau) * s.Rx + s.Ry;
            double fz = cs * s.Rz;

            // Aim point in the station's frame: aimBehindM metres along -y.
            double bx = -fx;
            double by = -aimBehindM - fy;
            double bz = -fz;

            // v0+ = Phi_rv^-1 * b.
            double w1x = s.N / det * ((4.0 * sn - 3.0 * tau) * bx - 2.0 * (1.0 - cs) * by);
            double w1y = s.N / det * (2.0 * (1.0 - cs) * bx + sn * by);
            double w1z = s.N * bz / sn;

            // The impulse is the same in the rotating and inertial frames: r is unchanged across it,
            // so the omega x r term cancels in the difference. No conversion back is needed.
            r.DvX = w1x - v0x;
            r.DvY = w1y - v0y;
            r.DvZ = w1z - v0z;

            // Arrival relative velocity: vf = Phi_vr*r0 + Phi_vv*v0+.
            double avx = 3.0 * s.N * sn * s.Rx + cs * w1x + 2.0 * sn * w1y;
            double avy = -6.0 * s.N * (1.0 - cs) * s.Rx - 2.0 * sn * w1x + (4.0 * cs - 3.0) * w1y;
            double avz = -s.N * sn * s.Rz + cs * w1z;
            r.ArrivalRelSpeed = System.Math.Sqrt(avx * avx + avy * avy + avz * avz);

            r.Ok = true;
            return r;
        }

        /// <summary>Magnitude of a solution's impulse, for cost comparison across transfer times.</summary>
        public static double DvMagnitude(CwSolution r)
        {
            return System.Math.Sqrt(r.DvX * r.DvX + r.DvY * r.DvY + r.DvZ * r.DvZ);
        }

        /// <summary>
        /// Pick the cheapest transfer time by sweeping. F9I sweeps rather than solving analytically
        /// because the cost is not monotonic in time of flight - there are singular times in the
        /// middle of the range, and the cheapest answer is often just to one side of one.
        /// </summary>
        public static CwSolution Best(CwState s, double minTofS, double maxTofS, int steps,
                                      double aimBehindM, out double bestTofS)
        {
            CwSolution best = new CwSolution();
            best.Ok = false;
            bestTofS = 0.0;
            if (steps < 1) steps = 1;

            double bestCost = double.MaxValue;
            for (int i = 0; i <= steps; i++)
            {
                double tof = minTofS + (maxTofS - minTofS) * i / steps;
                CwSolution c = Solve(s, tof, aimBehindM);
                if (!c.Ok) continue;
                double cost = DvMagnitude(c);
                if (cost < bestCost) { bestCost = cost; best = c; bestTofS = tof; }
            }
            if (!best.Ok) best.Note = "no non-singular transfer in the window";
            return best;
        }
    }
}
