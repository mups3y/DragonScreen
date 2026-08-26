// DragonScreen - DockControl
// ---- IT IS A VELOCITY SERVO, NOT A POSITION ONE, AND THAT IS THE WHOLE DESIGN ----
// ---- THE SPEED LIMIT IS A BRAKING CURVE - THE SAME FAMILY AS THE HOVERSLAM ----
// ---- AND THE MIXING, WHICH IS ABOUT THRUSTER AUTHORITY ----
namespace DragonScreen
{
    public class Pid
    {
        public double P = 8.0, I = 0.02, D = 0.1;
        public double Setpoint;

        private double integral, lastError;
        private bool primed;

        public double IntegralLimit = 1.0;

        public void Reset() { integral = 0.0; lastError = 0.0; primed = false; }

        public double Update(double measured, double dt)
        {
            if (dt <= 0.0) return 0.0;
            double error = Setpoint - measured;

            integral += error * dt;
            if (integral > IntegralLimit) integral = IntegralLimit;
            if (integral < -IntegralLimit) integral = -IntegralLimit;

            double deriv = primed ? (error - lastError) / dt : 0.0;
            lastError = error;
            primed = true;

            double o = P * error + I * integral + D * deriv;
            if (o > 1.0) o = 1.0;
            if (o < -1.0) o = -1.0;
            return o;
        }
    }

    public struct DockState
    {
        public bool Valid;
        public double DistF, DistS, DistT;
        public double VelF, VelS, VelT;
        public double SpeedCap;
    }

    public struct DockCommand
    {
        public double Fore, Starboard, Top;
        public double RangeM;
        public bool Balanced;
        public string Note;
    }

    public static class DockControl
    {
        [Tunable] public static double RcsAccel = 0.15;

        [Tunable] public static double PidP = 8.0, PidI = 0.02, PidD = 0.1;

        public static double AxisSpeedLimit(double distance, double cap)
        {
            double d = (distance < 0.0) ? -distance : distance;
            double v = System.Math.Sqrt(2.0 * RcsAccel * d);
            if (v > cap) v = cap;
            return (distance < 0.0) ? -v : v;
        }

        public static double StandoffPerAxis(double distance)
        {
            return System.Math.Sqrt(distance * distance / 3.0);
        }

        public static double Range(DockState s)
        {
            return System.Math.Sqrt(s.DistF * s.DistF + s.DistS * s.DistS + s.DistT * s.DistT);
        }

        public static DockCommand Solve(DockState s, Pid pf, Pid ps, Pid pt, double dt)
        {
            DockCommand c = new DockCommand();
            if (!s.Valid || pf == null || ps == null || pt == null)
            {
                c.Note = "NO SOLUTION";
                return c;
            }

            c.RangeM = Range(s);

            pf.P = ps.P = pt.P = PidP;
            pf.I = ps.I = pt.I = PidI;
            pf.D = ps.D = pt.D = PidD;

            pf.Setpoint = AxisSpeedLimit(s.DistF, s.SpeedCap);
            ps.Setpoint = AxisSpeedLimit(s.DistS, s.SpeedCap);
            pt.Setpoint = AxisSpeedLimit(s.DistT, s.SpeedCap);

            double of = pf.Update(s.VelF, dt);
            double os = ps.Update(s.VelS, dt);
            double ot = pt.Update(s.VelT, dt);

            // ---- AUTHORITY MIXING ----
            // ---- ⚠ RANKED ON THE OFFSETS, NOT ON THE PID OUTPUTS. DELIBERATE CHANGE. ----
            // ---- ⛔ THE CLOSURE WAITS FOR THE LATERAL. `MechJebModuleDockingAutopilot.cs:203-213`. ----
            double lat = System.Math.Sqrt(s.DistS * s.DistS + s.DistT * s.DistT);
            double latSpeed = Abs(ps.Setpoint) + Abs(pt.Setpoint);
            double axSpeed = Abs(pf.Setpoint);
            if (lat > 1e-3 && latSpeed > 1e-6 && axSpeed > 1e-6)
            {
                double timeToAxis = lat / latSpeed;
                double timeToClose = Abs(s.DistF) / axSpeed;
                if (timeToAxis > 1e-6 && timeToClose > 1e-6
                    && (Abs(s.DistF) <= lat * 10.0 || timeToClose <= timeToAxis * 10.0))
                {
                    double scale = timeToClose / timeToAxis;
                    if (scale > 1.0) scale = 1.0;
                    pf.Setpoint = pf.Setpoint * scale;
                    ps.Setpoint = AxisSpeedLimit(s.DistS, s.SpeedCap * 2.0);
                    pt.Setpoint = AxisSpeedLimit(s.DistT, s.SpeedCap * 2.0);
                    of = pf.Update(s.VelF, dt);
                    os = ps.Update(s.VelS, dt);
                    ot = pt.Update(s.VelT, dt);
                    c.Balanced = scale < 0.999;
                }
            }

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

        /// ---- ⚠ THIS BAND IS OURS. F9I HAS NO LAW FOR IT, AND THAT IS NOT AN OVERSIGHT. ----
        public static double ContactCapMps(double rangeM)
        {
            if (rangeM >= Approach.BandNearD) return double.MaxValue;

            double t = rangeM / Approach.BandNearD;
            double v = ContactFloorMps + (Approach.BandNearV - ContactFloorMps) * t;
            return (v < ContactFloorMps) ? ContactFloorMps : v;
        }

        [Tunable] public static double ContactFloorMps = 0.15;

        /// ---- ⛔ THIS USED A SECOND, DIFFERENT LAW WHILE CLAIMING TO USE THE FIRST. ----
        public static double SpeedCapFor(double rangeM)
        {
            double ladder = Approach.SpeedCap(rangeM);
            double contact = ContactCapMps(rangeM);
            return (contact < ladder) ? contact : ladder;
        }
    }
}
