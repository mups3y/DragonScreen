// DragonScreen - LaunchAzimuth
// ---- WHY THIS EXISTS: 'heading 90' IS A KERBIN ARTEFACT ----
// ---- THE FORMULA, AND WHY THERE ARE TWO OF THEM ----
using System;

namespace DragonScreen
{
    public static class LaunchAzimuth
    {
        private const double Deg2Rad = Math.PI / 180.0;
        private const double Rad2Deg = 180.0 / Math.PI;

        public static double InertialHeadingDeg(double inclinationDeg, double latitudeDeg)
        {
            double cosLat = Math.Cos(latitudeDeg * Deg2Rad);
            if (Math.Abs(cosLat) < 1e-9) return 90.0;

            double s = Math.Cos(inclinationDeg * Deg2Rad) / cosLat;
            if (s > 1.0) s = 1.0;
            if (s < -1.0) s = -1.0;
            return Math.Asin(s) * Rad2Deg;
        }

        public static double SurfaceEastwardSpeedMps(double bodyRadiusM, double bodyRotationPeriodS,
                                                     double latitudeDeg)
        {
            if (bodyRotationPeriodS <= 0.0) return 0.0;
            return 2.0 * Math.PI * bodyRadiusM * Math.Cos(latitudeDeg * Deg2Rad) / bodyRotationPeriodS;
        }

        public static double GroundHeadingDeg(double inclinationDeg, double latitudeDeg,
                                              double orbitalSpeedMps, double surfaceEastwardSpeedMps)
        {
            double betaI = InertialHeadingDeg(inclinationDeg, latitudeDeg) * Deg2Rad;
            double east = orbitalSpeedMps * Math.Sin(betaI) - surfaceEastwardSpeedMps;
            double north = orbitalSpeedMps * Math.Cos(betaI);
            double h = Math.Atan2(east, north) * Rad2Deg;
            return (h < 0.0) ? h + 360.0 : h;
        }
    }
}
