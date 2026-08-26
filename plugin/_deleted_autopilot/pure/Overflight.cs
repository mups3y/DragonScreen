// DragonScreen - Overflight
// ---- ⛔ THE ONE IDEA THIS FILE EXISTS FOR: THE SITE MOVES WHILE WE FALL ----
// ---- ⚠ AND ON AN EQUATORIAL ORBIT THE BUG IS INVISIBLE ----
// ---- ⛔ THERE IS NO PLANE-CHANGE BURN HERE, AND THAT IS DELIBERATE ----
namespace DragonScreen
{
    public struct OverflightResult
    {
        public bool Ok;
        public double Ut;
        public double TrackMissM;
        public double InS;
        public string Note;
    }

    public delegate double TrackMissAtUt(double ut);

    public static class Overflight
    {
        // ---- F9I's CONSTANTS. dragon_deorbit.ks:110, 455-459. ----

        public const double DescentTimeS = 845.0;

        public const double LagArcFrac = 0.358;

        public const double OrbitCap = 5.0;

        /// ---- ⛔ WHY A GLOBAL MINIMUM IS A COIN-FLIP OVER AN EQUATORIAL SITE ----
        public const double TieMarginM = 3000.0;

        /// ---- ⛔ THIS IS NOT `LagArcFrac`, AND CONFUSING THE TWO COST THREE FLIGHTS ----
        public const double PhaseArcFrac = 0.255;

        public const double GoTimeMarginS = 60.0;

        public static double GoTimeUt(double overflightUt, double nowUt, double orbitPeriodS)
        {
            double go = overflightUt - (orbitPeriodS * PhaseArcFrac);
            if (orbitPeriodS <= 0.0) return go;
            while (go <= nowUt + GoTimeMarginS) go += orbitPeriodS;
            return go;
        }

        public const double PlaneToleranceDeg = 0.05;

        // ---- the coarse/fine sweep. dgFindOverflight. ----
        public const double CoarseStepS = 60.0;
        public const double RefineStartStepS = 5.0;
        public const double RefineFloorS = 0.1;
        public const int RefineHalfWidthSteps = 12;
        public const double RefineDivisor = 5.0;

        public static double LandLagS(double orbitPeriodS)
        {
            return DescentTimeS - (LagArcFrac * orbitPeriodS);
        }

        public static double SiteLonAtDeg(double siteLonDeg, double elapsedS, double rotationPeriodS)
        {
            if (rotationPeriodS <= 0.0) return siteLonDeg;
            return WrapLonDeg(siteLonDeg + (360.0 * elapsedS / rotationPeriodS));
        }

        public static double WrapLonDeg(double lonDeg)
        {
            double l = (lonDeg + 180.0) % 360.0;
            if (l < 0.0) l += 360.0;
            return l - 180.0;
        }

        public static double TrackMissM(double bodyRadiusM, double rotationPeriodS,
                                        double subLatDeg, double subLonDeg, double elapsedS,
                                        double siteLatDeg, double siteLonDeg)
        {
            double lon = subLonDeg;
            if (rotationPeriodS > 0.0)
                lon = WrapLonDeg(subLonDeg - (360.0 * elapsedS / rotationPeriodS));
            return Orbital.GroundRange(bodyRadiusM, subLatDeg, lon, siteLatDeg, siteLonDeg);
        }

        public static double OffPlaneDeg(double normalLatDeg, double normalLonDeg,
                                         double siteLatDeg, double siteLonDeg)
        {
            const double rad = System.Math.PI / 180.0;
            double c = System.Math.Sin(normalLatDeg * rad) * System.Math.Sin(siteLatDeg * rad)
                     + System.Math.Cos(normalLatDeg * rad) * System.Math.Cos(siteLatDeg * rad)
                     * System.Math.Cos((normalLonDeg - siteLonDeg) * rad);
            if (c > 1.0) c = 1.0; else if (c < -1.0) c = -1.0;
            return 90.0 - (System.Math.Acos(c) / rad);
        }

        public static double CrossTrackFromOffPlaneM(double offPlaneDeg, double bodyRadiusM)
        {
            return offPlaneDeg * System.Math.PI / 180.0 * bodyRadiusM;
        }

        /// ---- ⚠ WHY IT SEARCHES A SHORT WINDOW RATHER THAN WAITING FOR A GOOD ONE ----
        public static OverflightResult Search(double nowUt, double orbitPeriodS, TrackMissAtUt f)
        {
            OverflightResult r = new OverflightResult();
            r.Ut = nowUt;
            r.TrackMissM = 9.9e12;

            if (f == null || orbitPeriodS <= 0.0)
            {
                r.Note = "no orbit to search";
                return r;
            }

            double end = orbitPeriodS * OrbitCap;
            for (double t = 0.0; t <= end; t += CoarseStepS)
            {
                double m = f(nowUt + t);
                if (m < r.TrackMissM - TieMarginM) { r.TrackMissM = m; r.Ut = nowUt + t; }
            }

            double step = RefineStartStepS;
            while (step >= RefineFloorS)
            {
                double lo = r.Ut - (step * RefineHalfWidthSteps);
                double hi = r.Ut + (step * RefineHalfWidthSteps);
                if (lo < nowUt) lo = nowUt;
                for (double u = lo; u <= hi; u += step)
                {
                    double m = f(u);
                    if (m < r.TrackMissM) { r.TrackMissM = m; r.Ut = u; }
                }
                step = step / RefineDivisor;
            }

            r.Ok = true;
            r.InS = r.Ut - nowUt;
            r.Note = "pass in " + r.InS.ToString("F0") + " s, track miss "
                   + (r.TrackMissM / 1000.0).ToString("F1") + " km";
            return r;
        }
    }
}
