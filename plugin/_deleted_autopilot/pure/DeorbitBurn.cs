// DragonScreen - DeorbitBurn
// ---- IT IS FLOWN AGAINST THE AIM POINT, NOT AGAINST A TARGET PERIAPSIS ----
// ---- ⛔ THE CUT-OUT NEEDS A LEAD, AND FLIGHT 035 MEASURED WHY ----
// ---- AND "GETTING WORSE" IS A STOP CONDITION ----
namespace DragonScreen
{
    public struct DeorbitState
    {
        public double AimMissM;
        public double PeriapsisM;
        public double PeriapsisRateMps;
        public double BestMissM;
        public int WorseCount;
        public double ElapsedS;
        public double S2FuelUnits;
        public bool UsingS2;
        public double MonoUnits;
    }

    public static class DeorbitBurn
    {
        // ---- F9I's CONSTANTS. dragon_deorbit.ks:96-265. ----
        public const double LzToleranceM = 15000.0;
        public const double OvershootM = 35000.0;
        public const double AimScanIntervalS = 0.50;
        public const double CutLeadS = 0.35;
        public const double PeriapsisTargetM = -30000.0;

        public const double DepthEaseM = 40000.0;
        public const double DepthThrottleMax = 0.40;
        public const double CrossBlend = 0.30;

        // ---- ⛔ VECTORED STEERING. The burn is not pure retrograde (user, 2026-08-20). ----
        public const double VectorScaleM = 60000.0;
        public const double VectorMaxGain = 0.5;

        public const double ThrottleMax = 0.60;
        public const double ThrottleMin = 0.01;
        public const double ThrottleBlind = 0.50;
        public const double ThrottleScaleM = 400000.0;

        public const double WorseMarginM = 500.0;
        public const int WorseLimit = 4;
        public const double CloseEnoughM = 30000.0;
        public const double MaxBurnS = 300.0;
        public const double S2FuelFloorUnits = 5.0;

        public const double MonoFloorUnits = 5.0;
        public const double AlignedDeg = 4.0;

        public static double Throttle(DeorbitState s)
        {
            if (s.AimMissM < 0.0) return ThrottleBlind;
            double t = System.Math.Sqrt(s.AimMissM / ThrottleScaleM);
            if (t > ThrottleMax) t = ThrottleMax;
            if (t < ThrottleMin) t = ThrottleMin;
            return t;
        }

        public static bool DepthLimitReached(double periapsisM, double periapsisRateMps)
        {
            return (periapsisM + periapsisRateMps * CutLeadS) < PeriapsisTargetM;
        }

        public static bool Complete(DeorbitState s, out string why)
        {
            if (DepthLimitReached(s.PeriapsisM, s.PeriapsisRateMps))
            {
                why = "depth limit reached";
                return true;
            }
            if (s.AimMissM >= 0.0 && s.AimMissM < LzToleranceM)
            {
                why = "impact inside the landing tolerance";
                return true;
            }
            if (s.AimMissM >= 0.0 && s.AimMissM < CloseEnoughM && s.WorseCount > WorseLimit)
            {
                why = "close, and the miss has stopped improving";
                return true;
            }
            if (s.UsingS2 && s.S2FuelUnits < S2FuelFloorUnits)
            {
                why = "S2 out of fuel";
                return true;
            }
            if (!s.UsingS2 && s.MonoUnits < MonoFloorUnits)
            {
                why = "ABORTED - out of monopropellant";
                return true;
            }
            if (s.ElapsedS > MaxBurnS)
            {
                why = "ABORTED - burn ran past its backstop";
                return true;
            }
            why = "";
            return false;
        }

        public static void Track(ref DeorbitState s)
        {
            if (s.AimMissM < 0.0) return;
            if (s.AimMissM <= s.BestMissM) { s.BestMissM = s.AimMissM; s.WorseCount = 0; }
            else if (s.AimMissM > s.BestMissM + WorseMarginM) s.WorseCount++;
        }
    }
}
