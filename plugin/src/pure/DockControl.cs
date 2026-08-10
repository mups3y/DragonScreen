/*
 * DragonScreen - DockControl
 *
 * PURE. The docking translation controller. `COMMON/GNC.ks:1190` DockGNC + RCSTranslate.
 * Tranche 4 of docs/F9I_PORT_MAP.md.
 *
 * ---- IT IS A VELOCITY SERVO, NOT A POSITION ONE, AND THAT IS THE WHOLE DESIGN ----
 * The obvious way to write this is a PID on POSITION error. F9I does not: each axis computes a
 * TARGET SPEED from the distance remaining, and the PID then servos the relative VELOCITY onto that
 * speed. Two consequences that matter:
 *
 *   - the approach speed is bounded BY CONSTRUCTION at every range, instead of emerging from gains
 *   - the PID only ever sees a velocity error, so it cannot wind up over a long transit
 *
 * ---- THE SPEED LIMIT IS A BRAKING CURVE - THE SAME FAMILY AS THE HOVERSLAM ----
 *
 *      speed = clamp( sqrt(2 * rcsAccel * |distance|), -cap, +cap )
 *
 * v^2 = 2ad again: the fastest you may close and still stop in the distance you have left, with
 * `rcsAccel` = 0.15 m/s^2 standing for the RCS authority. It is the landing law pointed sideways,
 * and it is why the approach slows itself down without anyone scheduling it.
 *
 * ---- AND THE MIXING, WHICH IS ABOUT THRUSTER AUTHORITY ----
 * All three axes share one set of thrusters, so commanding all three at full is how a capsule ends up
 * translating diagonally at half the rate on every axis. F9I gives the DOMINANT axis half authority
 * and the other two a quarter each. It looks arbitrary; it is a priority scheme, and it is why the
 * approach converges one axis at a time instead of crabbing.
 */
namespace DragonScreen
{
    /// <summary>
    /// The PID F9I uses per axis. P 8, I 0.02, D 0.1 - a stiff proportional term with just enough
    /// integral to kill a standing offset and light damping.
    /// </summary>
    public class Pid
    {
        public double P = 8.0, I = 0.02, D = 0.1;
        public double Setpoint;

        private double integral, lastError;
        private bool primed;

        /// <summary>Clamp on the integral. Without one, a long hold at an unreachable setpoint
        /// stores authority the controller then dumps the instant the error flips.</summary>
        public double IntegralLimit = 1.0;

        public void Reset() { integral = 0.0; lastError = 0.0; primed = false; }

        public double Update(double measured, double dt)
        {
            if (dt <= 0.0) return 0.0;
            double error = Setpoint - measured;

            integral += error * dt;
            if (integral > IntegralLimit) integral = IntegralLimit;
            if (integral < -IntegralLimit) integral = -IntegralLimit;

            // First call has no previous error, so a derivative would be a spike off zero.
            double deriv = primed ? (error - lastError) / dt : 0.0;
            lastError = error;
            primed = true;

            double o = P * error + I * integral + D * deriv;
            if (o > 1.0) o = 1.0;
            if (o < -1.0) o = -1.0;
            return o;
        }
    }

    /// <summary>Relative state on the vessel's own three axes, metres and m/s.</summary>
    public struct DockState
    {
        public bool Valid;
        /// <summary>Offset to the aim point along forward / starboard / top.</summary>
        public double DistF, DistS, DistT;
        /// <summary>Relative velocity (ours minus theirs) on the same three axes.</summary>
        public double VelF, VelS, VelT;
        /// <summary>Speed cap for this phase, m/s.</summary>
        public double SpeedCap;
    }

    /// <summary>RCS translation command, each -1..1.</summary>
    public struct DockCommand
    {
        public double Fore, Starboard, Top;
        public double RangeM;
        public string Note;
    }

    public static class DockControl
    {
        /// <summary>
        /// Assumed RCS translation authority, m/s^2. `rcsAcc` in GNC.ks. It does not have to be
        /// exact - it sets how conservative the braking curve is, and 0.15 is deliberately low so
        /// the approach under-commits rather than arriving unable to stop.
        /// </summary>
        public const double RcsAccel = 0.15;

        /// <summary>
        /// The braking curve: fastest we may close on this axis and still stop in the distance left.
        /// Signed - the sign says which way to go, the magnitude how fast.
        /// </summary>
        public static double AxisSpeedLimit(double distance, double cap)
        {
            double d = (distance < 0.0) ? -distance : distance;
            double v = System.Math.Sqrt(2.0 * RcsAccel * d);
            if (v > cap) v = cap;
            return (distance < 0.0) ? -v : v;
        }

        /// <summary>
        /// Split a standoff distance equally across three axes, the way GNC.ks builds its aim point
        /// with `sqrt(tDist^2 / 3)` on each. Three of those in quadrature is exactly tDist, which is
        /// the property that makes it a standoff SPHERE rather than a cube.
        /// </summary>
        public static double StandoffPerAxis(double distance)
        {
            return System.Math.Sqrt(distance * distance / 3.0);
        }

        /// <summary>Range from the three axis offsets.</summary>
        public static double Range(DockState s)
        {
            return System.Math.Sqrt(s.DistF * s.DistF + s.DistS * s.DistS + s.DistT * s.DistT);
        }

        /// <summary>
        /// One control step. The three PIDs are owned by the caller so their state survives between
        /// ticks - a PID recreated every frame is just a P controller with extra steps.
        /// </summary>
        public static DockCommand Solve(DockState s, Pid pf, Pid ps, Pid pt, double dt)
        {
            DockCommand c = new DockCommand();
            if (!s.Valid || pf == null || ps == null || pt == null)
            {
                c.Note = "NO SOLUTION";
                return c;
            }

            c.RangeM = Range(s);

            // Each axis: a target CLOSING SPEED from the braking curve, servoed by the PID against
            // the relative velocity we actually have.
            pf.Setpoint = AxisSpeedLimit(s.DistF, s.SpeedCap);
            ps.Setpoint = AxisSpeedLimit(s.DistS, s.SpeedCap);
            pt.Setpoint = AxisSpeedLimit(s.DistT, s.SpeedCap);

            double of = pf.Update(s.VelF, dt);
            double os = ps.Update(s.VelS, dt);
            double ot = pt.Update(s.VelT, dt);

            // ---- AUTHORITY MIXING ----
            // The dominant axis gets half, the other two a quarter each. See the header: they share
            // one set of thrusters, and commanding all three flat out is how a capsule crabs.
            //
            // ---- ⚠ RANKED ON THE OFFSETS, NOT ON THE PID OUTPUTS. DELIBERATE CHANGE. ----
            // GNC.ks:1259 compares `dockOutF/S/T` - the PID outputs. With P = 8 those SATURATE at
            // +/-1 for any offset over about an eighth of a metre per second of speed error, so on a
            // real approach all three are pegged at 1.0 and the comparison decides nothing: the
            // chain falls through to its last branch and the same axis wins every time regardless
            // of geometry. Caught here by a forward-only offset being reported VERTICAL.
            //
            // The offsets do not saturate, and "which axis has the most left to do" is what the
            // mixing was always trying to ask.
            double af = Abs(s.DistF), as_ = Abs(s.DistS), at = Abs(s.DistT);
            if (as_ + at < af)
            {
                c.Fore = of * 0.5; c.Starboard = os * 0.25; c.Top = ot * 0.25;
                c.Note = "AXIAL";
            }
            else if (as_ > at)
            {
                c.Fore = of * 0.25; c.Starboard = os * 0.5; c.Top = ot * 0.25;
                c.Note = "LATERAL";
            }
            else
            {
                c.Fore = of * 0.25; c.Starboard = os * 0.25; c.Top = ot * 0.5;
                c.Note = "VERTICAL";
            }
            return c;
        }

        private static double Abs(double v) { return (v < 0.0) ? -v : v; }

        /// <summary>
        /// Speed cap for a range, tying the docking controller to the recorded approach ladder so
        /// the two cannot disagree about how fast is safe. See Rendezvous.CorridorRate - the same
        /// numbers, deliberately, because a controller with its own idea of a safe closing speed is
        /// how you get a capsule that thinks it is being careful while the ladder thinks otherwise.
        /// </summary>
        public static double SpeedCapFor(double rangeM)
        {
            return Rendezvous.CorridorRate(rangeM);
        }
    }
}
