// DragonScreen - DockGeometry
// ---- ⛔ THE KEEP-OUT SPHERE IS CENTRED ON THE STATION, NOT ON THE PORT ----
// ---- ⛔ AND THE SPHERE TEST ONLY MEANS ANYTHING FROM OUTSIDE THE SPHERE ----
// ---- WHY THE SKIRT AIMS AT R + PAD AND NOT AT R ----
namespace DragonScreen
{
    public static class DockGeometry
    {
        [Tunable] public static double StandoffM = 25.0;

        [Tunable] public static double StandoffToleranceM = 12.0;

        [Tunable] public static double KeepOutPadM = 20.0;

        public static double GateDistanceM(double cDotU, double cSqrMag, double keepOutRadiusM)
        {
            double disc = cDotU * cDotU - cSqrMag + keepOutRadiusM * keepOutRadiusM;
            if (disc <= 0.0) return StandoffM;
            double exit = cDotU + System.Math.Sqrt(disc) + KeepOutPadM;
            return (exit > StandoffM) ? exit : StandoffM;
        }

        public static double ClosestApproachM(double cSqrMag, double cDotU, double segmentLenM)
        {
            double t = cDotU;
            if (t < 0.0) t = 0.0;
            if (t > segmentLenM) t = segmentLenM;
            double d2 = cSqrMag - 2.0 * t * cDotU + t * t;
            return (d2 > 0.0) ? System.Math.Sqrt(d2) : 0.0;
        }

        public static bool PathClear(double distanceToCentreM, double cSqrMag, double cDotU,
                                     double segmentLenM, double keepOutRadiusM)
        {
            if (keepOutRadiusM <= 0.0) return true;
            if (distanceToCentreM <= keepOutRadiusM) return true;
            if (segmentLenM < 0.001) return true;
            return ClosestApproachM(cSqrMag, cDotU, segmentLenM) > keepOutRadiusM;
        }

        public static double SkirtRadiusM(double keepOutRadiusM)
        {
            return keepOutRadiusM + KeepOutPadM;
        }

        // ---- ⛔ `AtStandoff` WAS DELETED 2026-08-17. It was the 13 m stall: "within 12 m of a point

        /// ---- ⛔ THE LEG THIS TEST EXISTS FOR WAS MISSING, AND IT DEADLOCKED THE DOCKING. ----
        public static bool AtGate(double distanceToGateM)
        {
            return distanceToGateM < GateToleranceM;
        }

        [Tunable] public static double GateToleranceM = 15.0;
    }
}
