// DragonScreen - DirectApproach
// ---- WHY THIS EXISTS WHEN WE ALREADY HAVE A CW LADDER ----
// ---- ⛔ THE RANGE GATE IS LOAD-BEARING. IT IS NOT A TUNING PARAMETER. ----
// ---- ⛔ FOUR THINGS HERE ARE COUNTER-INTUITIVE AND EVERY ONE IS A FLIGHT ----
namespace DragonScreen
{
    public enum DirectPhase : byte
    {
        Idle = 0,
        Vectoring,
        Accelerating,
        Closing,
        Matching,
        Done,
        Refused
    }

    public static class DirectApproach
    {
        // ---- F9I's CONSTANTS. station_ops.ks:496-569, 673-680, 726. ----

        public const double GateM = 10000.0;

        public const double CloseRate = 0.02;
        public const double GoalM = 200.0;
        public const double MatchVelMps = 0.5;

        // ---- ⛔ KEEP-OUT: A HARD BACKSTOP AGAINST RAMMING THE STATION ----
        public const double KeepOutFloorM = 60.0;
        public const double HardAbortM = 25.0;

        public const double DvToleranceMps = 0.30;
        public const double AimToleranceDeg = 2.0;
        public const double AimAlignDeg = 1.5;
        public const double BrakeAlignDeg = 10.0;

        public const double AccelMaxS = 60.0;
        public const double CloseTimeoutS = 1800.0;

        public const double BurnAccel = 1.5;
        public const double BurnTaperS = 4.0;
        public const double ThrottleMin = 0.01;
        public const double GoalTolerance = 1.30;

        public static bool InsideGate(double rangeM) { return rangeM <= GateM; }

        public static double WantSpeedMps(double rangeM)
        {
            double taper = ((rangeM - GoalM) * CloseRate) + MatchVelMps;
            if (taper < MatchVelMps) taper = MatchVelMps;
            double cap = Approach.SpeedCap(rangeM);
            return (taper < cap) ? taper : cap;
        }

        public static double Throttle(double dvMps, double massT, double thrustKn)
        {
            double want = dvMps / BurnTaperS;
            if (want > BurnAccel) want = BurnAccel;
            double t = want * massT / ((thrustKn > 1.0) ? thrustKn : 1.0);
            if (t < ThrottleMin) t = ThrottleMin;
            if (t > 1.0) t = 1.0;
            return t;
        }

        public static bool Burn(double dvMps, double aimErrorDeg, double closingMps, double rangeM)
        {
            return Burn(dvMps, aimErrorDeg, closingMps, rangeM, true);
        }

        /// ---- ⛔ THE SPEED CAP BELONGS TO THE ACCELERATE LOOP ONLY. F9I HAS THREE LOOPS. ----
        public static bool Burn(double dvMps, double aimErrorDeg, double closingMps, double rangeM,
                                bool accelerating)
        {
            if (dvMps <= DvToleranceMps) return false;
            if (aimErrorDeg >= AimToleranceDeg) return false;
            if (!accelerating) return true;
            return closingMps < Approach.SpeedCap(rangeM);
        }

        public static bool Arrived(double rangeM, double relSpeedMps)
        {
            return rangeM <= GoalM * GoalTolerance && relSpeedMps <= MatchVelMps;
        }
    }
}
