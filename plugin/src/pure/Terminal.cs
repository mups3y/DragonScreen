// DragonScreen - Terminal
// ---- ⛔ THE MODE IS CHOSEN ON WHAT IS ABOARD, NOT ON WHAT WAS PLANNED ----
// ---- ⛔ THE CHUTES ARE CUT ONLY AFTER THE ENGINES ARE PROVEN LIT ----
// ---- ⚠ THESE ALTITUDES ARE KERBIN'S, NOT THE REAL DRAGON'S ----
namespace DragonScreen
{
    public enum LandingMethod : byte
    {
        Parachute = 0,
        Propulsive = 1
    }

    public static class Terminal
    {
        // ---- F9I's CONSTANTS. dragon_deorbit.ks:475, 505-507, 2372-2410. ----

        public const double HandoverAltM = 7500.0;
        public const double DrogueFloorChuteM = 4500.0;
        public const double DrogueFloorPropulsiveM = 5000.0;
        public const double DrogueMaxSpeedMps = 560.0;
        public const double MainAltM = 2000.0;
        public const double MonoGateUnits = 120.0;

        public const double GearDeployAltM = 40.0;

        public const double HeightOffsetM = 4.0;
        public const double ArmFactor = 2.2;
        public const double ArmFloorM = 120.0;
        public const double BurnFactor = 1.6;
        public const double HoverMargin = 1.05;
        public const double HoverHandoverMps = -5.0;
        public const double TouchdownTimeoutS = 45.0;

        public static LandingMethod Choose(bool wantPropulsive, bool hasLandingEngines,
                                           double monoUnits, out string why)
        {
            why = "";
            if (!wantPropulsive) { why = "parachute landing selected"; return LandingMethod.Parachute; }
            if (!hasLandingEngines)
            {
                why = "no pod engines";
                return LandingMethod.Parachute;
            }
            if (monoUnits < MonoGateUnits)
            {
                why = "mono " + monoUnits.ToString("F0") + " < " + MonoGateUnits.ToString("F0");
                return LandingMethod.Parachute;
            }
            return LandingMethod.Propulsive;
        }

        public static bool DrogueReady(double airspeedMps, double radarAltM, LandingMethod m)
        {
            double floor = (m == LandingMethod.Propulsive)
                         ? DrogueFloorPropulsiveM : DrogueFloorChuteM;
            return airspeedMps < DrogueMaxSpeedMps || radarAltM < floor;
        }

        public static bool MainsReady(double radarAltM) { return radarAltM < MainAltM; }

        public static double TrueRadarM(double radarAltM) { return radarAltM - HeightOffsetM; }

        public static double MaxDecelMps2(double availableThrustKn, double massT, double gravityMps2)
        {
            if (massT <= 0.0) return 0.001;
            double a = (availableThrustKn / massT) - gravityMps2;
            return (a > 0.001) ? a : 0.001;
        }

        public static double StopDistanceM(double verticalSpeedMps, double maxDecelMps2)
        {
            if (maxDecelMps2 <= 0.0) return double.MaxValue;
            return (verticalSpeedMps * verticalSpeedMps) / (2.0 * maxDecelMps2);
        }

        public static bool ArmGate(double trueRadarM, double stopDistM)
        {
            double gate = stopDistM * ArmFactor;
            if (gate < ArmFloorM) gate = ArmFloorM;
            return trueRadarM <= gate;
        }

        public static bool BurnGate(double trueRadarM, double stopDistM)
        {
            return trueRadarM <= (stopDistM * BurnFactor) + HeightOffsetM;
        }

        public static double LandingThrottle(double trueRadarM, double stopDistM)
        {
            return Deorbit.LandingThrottle(trueRadarM, stopDistM);
        }

        public static bool HoverHandover(double verticalSpeedMps)
        {
            return verticalSpeedMps > HoverHandoverMps;
        }

        public static double HoverThrottle(double massT, double gravityMps2, double availableThrustKn)
        {
            double t = (availableThrustKn > 0.001) ? availableThrustKn : 0.001;
            double h = HoverMargin * ((massT * gravityMps2) / t);
            return (h > 1.0) ? 1.0 : h;
        }
    }
}
