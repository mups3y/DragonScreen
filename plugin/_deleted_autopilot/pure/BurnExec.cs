// DragonScreen - BurnExec
// ---- ⛔ THIS FILE WAS MINE AND IS NOW A PORT. THE THROTTLE LAW CHANGED. ----
// ---- WHY THE THROTTLE IS PROPORTIONAL AND NOT LOCKED AT 1 ----
// ---- AND WHY THE HALF-BURN LEAD IS SIZED ON THE ACCELERATION WE WILL USE ----
// ---- THE STOP CONDITION IS AN OVERSHOOT TEST, NOT A COUNTDOWN ----
namespace DragonScreen
{
    public struct BurnState
    {
        public double RemainingDvMps;
        public double InitialDvMps;
        public double MassT;
        public double AvailableThrustKn;
        public double PointingErrorDeg;
        public bool Overshot;
        public double ElapsedS;
    }

    public static class BurnExec
    {
        // ---- F9I's CONSTANTS. station_ops.ks:597-681. ----
        public const double CruiseAccel = 1.5;
        public const double TaperS = 4.0;
        public const double ThrottleMin = 0.01;
        public const double StopDvMps = 0.05;
        public const double AlignPadS = 2.0;
        public const double AlignRcsBelowS = 45.0;
        public const double AlignedDeg = 3.0;
        public const double LooseAlignDeg = 15.0;
        public const double MaxBurnDurationS = 300.0;

        public const double RunawayFactor = 1.5;
        public const double RunawayBufferMps = 3.0;

        public static double BurnAccel(double massT, double availableThrustKn)
        {
            double have = availableThrustKn / (massT > 0.1 ? massT : 0.1);
            double a = (CruiseAccel < have) ? CruiseAccel : have;
            return (a < 0.05) ? 0.05 : a;
        }

        public static double HalfBurnS(double dvMps, double massT, double availableThrustKn, double spoolS)
        {
            return dvMps / (2.0 * BurnAccel(massT, availableThrustKn)) + 0.5 * (spoolS > 0.0 ? spoolS : 0.0);
        }

        public static double HalfBurnS(double dvMps, double massT, double availableThrustKn)
        {
            return HalfBurnS(dvMps, massT, availableThrustKn, 0.0);
        }

        public static double AlignDeadlineS(double secondsToIgnition)
        {
            return secondsToIgnition - AlignPadS;
        }

        public static bool Aligned(double pointingErrorDeg)
        {
            return pointingErrorDeg < AlignedDeg;
        }

        /// ---- ⛔ ...BUT THAT ASSUMES REACTION WHEELS EXIST, AND IN RO THEY DO NOT ----
        public static bool NeedRcsToAlign(double secondsToIgnition, double pointingErrorDeg,
                                          bool haveWheelAuthority)
        {
            if (Aligned(pointingErrorDeg)) return false;
            if (!haveWheelAuthority) return true;
            return secondsToIgnition < AlignRcsBelowS;
        }

        public static double Throttle(BurnState s)
        {
            if (s.AvailableThrustKn <= 0.0 || s.MassT <= 0.0) return 0.0;
            if (Complete(s)) return 0.0;

            double wantA = s.RemainingDvMps / TaperS;
            if (CruiseAccel < wantA) wantA = CruiseAccel;

            double th = wantA * s.MassT / (s.AvailableThrustKn > 1.0 ? s.AvailableThrustKn : 1.0);
            if (th > 1.0) th = 1.0;
            if (th < ThrottleMin) th = ThrottleMin;
            return th;
        }

        public static bool Complete(BurnState s)
        {
            if (s.Overshot) return true;
            if (s.RemainingDvMps < StopDvMps) return true;
            if (Runaway(s)) return true;
            return s.ElapsedS > MaxBurnDurationS;
        }

        public static bool Runaway(BurnState s)
        {
            return s.InitialDvMps > 0.0
                && s.RemainingDvMps > s.InitialDvMps * RunawayFactor + RunawayBufferMps;
        }

        public static string CompletionNote(BurnState s)
        {
            if (s.Overshot) return "burned past the node";
            if (s.RemainingDvMps < StopDvMps) return "residual inside the stop threshold";
            if (Runaway(s)) return "ABORTED - residual ran away (thrusting off-axis / wrong-way)";
            if (s.ElapsedS > MaxBurnDurationS) return "ABORTED - burn ran past its backstop";
            return "";
        }
    }
}
