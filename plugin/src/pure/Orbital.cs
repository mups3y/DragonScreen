// DragonScreen - Orbital
// ---- THIS IS THE LAYER EVERYTHING ELSE STANDS ON, AND IT WAS THE LAST THING I BUILT ----
// ---- kOS TRIGONOMETRY IS IN DEGREES. THIS IS IN RADIANS INTERNALLY. ----
namespace DragonScreen
{
    public static class Orbital
    {
        private const double Deg = 180.0 / System.Math.PI;
        private const double Rad = System.Math.PI / 180.0;
        private const double TwoPi = 2.0 * System.Math.PI;

        public static double Wrap(double r)
        {
            r = r % TwoPi;
            return (r < 0.0) ? r + TwoPi : r;
        }

        // ------------------------------------------------------------------ vis-viva

        public static double VisViva(double mu, double r, double sma)
        {
            if (r <= 0.0) return 0.0;
            double t = mu * (2.0 / r - 1.0 / sma);
            return (t > 0.0) ? System.Math.Sqrt(t) : 0.0;
        }

        public static double CircularSpeed(double mu, double r)
        {
            if (r <= 0.0) return 0.0;
            return System.Math.Sqrt(mu / r);
        }

        public static double Period(double mu, double sma)
        {
            if (sma <= 0.0 || mu <= 0.0) return 0.0;
            return TwoPi * System.Math.Sqrt(sma * sma * sma / mu);
        }

        // ------------------------------------------------------------------ anomalies

        /// ---- ⚠ THE SIGNATURE TRAP F9I FLAGGED IN ITS OWN SOURCE, FIXED HERE ----
        public static void AltitudeToTrueAnomaly(double sma, double ecc, double bodyRadius,
                                                 double altitude, double periapsis, double apoapsis,
                                                 out double climbing, out double falling)
        {
            double alt = altitude;
            if (alt < periapsis) alt = periapsis;
            if (alt > apoapsis) alt = apoapsis;
            double r = alt + bodyRadius;

            double c = (-sma * ecc * ecc + sma - r) / (ecc * r);
            if (c > 1.0) c = 1.0; else if (c < -1.0) c = -1.0;
            climbing = System.Math.Acos(c);
            falling = TwoPi - climbing;
        }

        public static double TrueToEccentric(double trueAnomaly, double ecc)
        {
            double e2 = 1.0 - ecc * ecc;
            if (e2 < 0.0) e2 = 0.0;
            return Wrap(System.Math.Atan2(System.Math.Sqrt(e2) * System.Math.Sin(trueAnomaly),
                                          ecc + System.Math.Cos(trueAnomaly)));
        }

        public static double EccentricToMean(double eccentricAnomaly, double ecc)
        {
            return eccentricAnomaly - ecc * System.Math.Sin(eccentricAnomaly);
        }

        public static double TrueToMean(double trueAnomaly, double ecc)
        {
            return EccentricToMean(TrueToEccentric(trueAnomaly, ecc), ecc);
        }

        public static double TimeToAltitude(double mu, double sma, double ecc, double bodyRadius,
                                            double periapsis, double apoapsis,
                                            double currentTrueAnomaly, double targetAltitude,
                                            int mode)
        {
            if (sma <= 0.0 || mu <= 0.0) return 0.0;

            double up, down;
            AltitudeToTrueAnomaly(sma, ecc, bodyRadius, targetAltitude, periapsis, apoapsis,
                                  out up, out down);

            double n = System.Math.Sqrt(mu / (sma * sma * sma));
            if (n <= 0.0) return 0.0;

            double m0 = TrueToMean(currentTrueAnomaly, ecc);
            double tUp = Wrap(TrueToMean(up, ecc) - m0) / n;
            double tDown = Wrap(TrueToMean(down, ecc) - m0) / n;

            if (mode == 1) return tDown;
            if (mode == 2) return tUp;
            return (tUp < tDown) ? tUp : tDown;
        }

        // ------------------------------------------------------------------ phasing

        public static double PhaseWaitSeconds(double mu, double ourSma, double targetSma,
                                              double currentPhaseDeg)
        {
            double ourPeriod = Period(mu, ourSma);
            double tgtPeriod = Period(mu, targetSma);
            if (ourPeriod <= 0.0 || tgtPeriod <= 0.0) return 0.0;

            double transferSma = (ourSma + targetSma) * 0.5;
            double transferTime = Period(mu, transferSma) * 0.5;
            double targetTravelDeg = 360.0 * transferTime / tgtPeriod;
            double wantPhaseDeg = 180.0 - targetTravelDeg;

            double diff = currentPhaseDeg - wantPhaseDeg;
            double rate = 360.0 / ourPeriod - 360.0 / tgtPeriod;
            if (System.Math.Abs(rate) < 1e-12) return 0.0;

            double t = diff / rate;
            double synodic = System.Math.Abs(360.0 / rate);
            while (t < 0.0) t += synodic;
            return t;
        }

        public static double SynodicPeriod(double mu, double ourSma, double targetSma)
        {
            double a = Period(mu, ourSma), b = Period(mu, targetSma);
            if (a <= 0.0 || b <= 0.0) return 0.0;
            double d = System.Math.Abs(1.0 / a - 1.0 / b);
            if (d < 1e-15) return 0.0;
            return 1.0 / d;
        }

        // ------------------------------------------------------------------ Hohmann

        public static void Hohmann(double mu, double r1, double r2, out double dv1, out double dv2)
        {
            dv1 = 0.0; dv2 = 0.0;
            if (mu <= 0.0 || r1 <= 0.0 || r2 <= 0.0) return;
            double a = (r1 + r2) * 0.5;
            dv1 = VisViva(mu, r1, a) - CircularSpeed(mu, r1);
            dv2 = CircularSpeed(mu, r2) - VisViva(mu, r2, a);
        }

        public static double CircularisationDv(double mu, double r,
                                               double horizontalSpeed, double verticalSpeed)
        {
            double want = CircularSpeed(mu, r);
            double dh = want - horizontalSpeed;
            return System.Math.Sqrt(dh * dh + verticalSpeed * verticalSpeed);
        }

        // ------------------------------------------------------------------ ground

        public static double GroundRange(double bodyRadius, double lat1, double lon1,
                                         double lat2, double lon2)
        {
            double p1 = lat1 * Rad, p2 = lat2 * Rad;
            double dp = (lat2 - lat1) * Rad;
            double dl = (lon2 - lon1) * Rad;
            double a = System.Math.Sin(dp / 2) * System.Math.Sin(dp / 2)
                     + System.Math.Cos(p1) * System.Math.Cos(p2)
                     * System.Math.Sin(dl / 2) * System.Math.Sin(dl / 2);
            return 2.0 * bodyRadius * System.Math.Atan2(System.Math.Sqrt(a),
                                                        System.Math.Sqrt(1.0 - a));
        }

        public static double Bearing(double lat1, double lon1, double lat2, double lon2)
        {
            double p1 = lat1 * Rad, p2 = lat2 * Rad;
            double dl = (lon2 - lon1) * Rad;
            double y = System.Math.Sin(dl) * System.Math.Cos(p2);
            double x = System.Math.Cos(p1) * System.Math.Sin(p2)
                     - System.Math.Sin(p1) * System.Math.Cos(p2) * System.Math.Cos(dl);
            double b = System.Math.Atan2(y, x) * Deg;
            return (b < 0.0) ? b + 360.0 : b;
        }

        public static void OffsetLatLon(double bodyRadius, double lat, double lon,
                                        double bearingDeg, double distanceM,
                                        out double outLat, out double outLon)
        {
            double d = distanceM / bodyRadius;
            double p1 = lat * Rad, l1 = lon * Rad, b = bearingDeg * Rad;
            double sp = System.Math.Sin(p1) * System.Math.Cos(d)
                      + System.Math.Cos(p1) * System.Math.Sin(d) * System.Math.Cos(b);
            if (sp > 1.0) sp = 1.0; else if (sp < -1.0) sp = -1.0;
            double p2 = System.Math.Asin(sp);
            double l2 = l1 + System.Math.Atan2(
                System.Math.Sin(b) * System.Math.Sin(d) * System.Math.Cos(p1),
                System.Math.Cos(d) - System.Math.Sin(p1) * sp);
            outLat = p2 * Deg;
            outLon = ((l2 * Deg + 540.0) % 360.0) - 180.0;
        }

        /// ---- ⛔ THE CROSS TERM IS NOT `miss · sin(Δbearing)`, AND THE OBVIOUS VERSION IS BIASED ----
        public static void DownCross(double bodyRadius,
                                     double shipLat, double shipLon,
                                     double impactLat, double impactLon,
                                     double tgtLat, double tgtLon,
                                     out double alongM, out double crossM, out double missM)
        {
            missM = GroundRange(bodyRadius, impactLat, impactLon, tgtLat, tgtLon);
            double track = Bearing(shipLat, shipLon, impactLat, impactLon);
            double toTgt = Bearing(impactLat, impactLon, tgtLat, tgtLon);
            alongM = missM * System.Math.Cos((toTgt - track) * Rad);

            double tgtD = GroundRange(bodyRadius, shipLat, shipLon, tgtLat, tgtLon);
            double tgtB = Bearing(shipLat, shipLon, tgtLat, tgtLon);
            double arc = tgtD / bodyRadius;
            double s = System.Math.Sin(arc) * System.Math.Sin((tgtB - track) * Rad);
            if (s > 1.0) s = 1.0; else if (s < -1.0) s = -1.0;
            crossM = System.Math.Asin(s) * bodyRadius;
        }
    }
}
