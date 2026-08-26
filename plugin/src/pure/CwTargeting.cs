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
        /// <summary>The ROTATING-frame relative velocity right AFTER the departure impulse (v0+). This is
        /// what the vehicle coasts on if the arrival burn is never made - the input to the passive-abort
        /// free-drift check.</summary>
        public double Vx1, Vy1, Vz1;
        /// <summary>Closest the passive free-drift (no arrival burn) comes to the station over the checked
        /// coast, metres. Large = a missed burn safely misses the station. 0 when not evaluated.</summary>
        public double MinFreeDriftRangeM;
        /// <summary>True when the passive free-drift clears the required keep-out margin (or the check was
        /// disabled). False means this transfer would let a missed arrival burn breach the keep-out sphere.</summary>
        public bool PassiveAbortSafe;
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

            // The rotating-frame velocity the vehicle coasts on if the arrival burn is never made.
            r.Vx1 = w1x; r.Vy1 = w1y; r.Vz1 = w1z;
            r.PassiveAbortSafe = true;          // until a caller runs the free-drift check

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

        // ================= PASSIVE ABORT (offset targeting made a proven property) =================
        //
        // The real Crew Dragon rule (docs/REAL_CREW_DRAGON_MISSION.md): a departure burn must be aimed so
        // that a DISPERSED trajectory - one where the arrival (braking) burn never fires - still misses the
        // keep-out sphere. Aiming behind is the intent; this is the check that makes it a guarantee. After
        // the departure impulse the vehicle coasts on the rotating-frame velocity v0+ (CwSolution.Vx1..);
        // we propagate that free drift with the CW state-transition matrix and require the closest approach
        // to the station over a full orbit to stay outside the keep-out margin.

        /// <summary>
        /// CW free-drift position at time <paramref name="t"/> in the rotating LVLH frame, from a
        /// rotating-frame state (position r0, rotating-frame velocity v0). The position rows of the CW
        /// state-transition matrix - identical to the free-drift terms Solve uses, so a coast started from
        /// a solved v0+ reproduces the aim point at the transfer time and continues past it.
        /// </summary>
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

        /// <summary>
        /// The closest the passive free-drift comes to the station over [0, horizonS], sampling
        /// <paramref name="samples"/>+1 points. Starts from rotating-frame state (r0, v0). Used to score a
        /// transfer's passive-abort safety: large = a missed arrival burn safely misses the station.
        /// </summary>
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

        /// <summary>Default samples for the free-drift coast scan - fine enough to catch a grazing pass.</summary>
        public const int DefaultCoastSamples = 240;

        /// <summary>
        /// Pick the cheapest transfer time by sweeping. F9I sweeps rather than solving analytically
        /// because the cost is not monotonic in time of flight - there are singular times in the
        /// middle of the range, and the cheapest answer is often just to one side of one.
        ///
        /// (No passive-abort filter - the cheapest non-singular transfer, as before.)
        /// </summary>
        public static CwSolution Best(CwState s, double minTofS, double maxTofS, int steps,
                                      double aimBehindM, out double bestTofS)
        {
            return Best(s, minTofS, maxTofS, steps, aimBehindM, out bestTofS, 0.0, 0.0, 0);
        }

        /// <summary>
        /// Pick the best transfer time by sweeping, with a PASSIVE-ABORT preference: among the
        /// non-singular transfers, prefer the cheapest whose free drift (arrival burn never made) stays at
        /// least <paramref name="passiveSafeM"/> from the station over one orbit past arrival. If none is
        /// safe (the geometry forces a close pass), return the SAFEST transfer instead and flag it - the
        /// keep-out backstop then catches an actual breach, but we never knowingly pick a less-safe path.
        ///
        /// passiveSafeM &lt;= 0 disables the filter (identical to the plain Best). coastCheckPeriodS is one
        /// station orbital period; the coast is checked over [0, tof + coastCheckPeriodS].
        /// </summary>
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

            double bestSafeCost = double.MaxValue;     // cheapest among passively-safe transfers
            double bestSafeTof = 0.0; CwSolution bestSafe = new CwSolution(); bool haveSafe = false;
            double bestMargin = -1.0;                  // safest transfer, if none clears the margin
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
                // No transfer clears the passive-abort margin - take the safest and say so. Not a burn to
                // refuse (that would freeze the approach); the keep-out backstop guards an actual breach.
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
