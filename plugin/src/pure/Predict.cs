// DragonScreen - Predict
// ---- PURE, WITH THE SAMPLING HANDED IN ----
// ---- THE TWO NON-OBVIOUS ONES ----
using System;

namespace DragonScreen
{
    public static class Predict
    {
        private const double TwoPi = 2.0 * Math.PI;

        // ------------------------------------------------------------------ timing

        public static double TimeBetweenTrueAnomalies(double ecc, double period,
                                                      double fromTrue, double toTrue)
        {
            if (period <= 0.0) return 0.0;
            double m1 = Orbital.TrueToMean(fromTrue, ecc);
            double m2 = Orbital.TrueToMean(toTrue, ecc);
            double frac = Orbital.Wrap(m2 - m1) / TwoPi;
            return period * frac;
        }

        public static double GroundTrackLongitude(double inertialLongitudeDeg,
                                                  double bodyRotationDegPerSec, double dt)
        {
            double lon = inertialLongitudeDeg - bodyRotationDegPerSec * dt;
            lon = lon % 360.0;
            if (lon < -180.0) lon += 360.0;
            if (lon > 180.0) lon -= 360.0;
            return lon;
        }

        // ------------------------------------------------------------------ impact

        public struct Impact
        {
            public bool Valid;
            public double TimeS;
            public double TerrainHeightM;
            public bool Converged;
            public int Iterations;
        }

        /// ---- WHY IT ITERATES, AND WHY IT MUST BE DAMPED ----
        public static Impact SolveImpact(double sma, double ecc, double bodyRadius,
                                         double periapsis, double apoapsis,
                                         double currentTrueAnomaly, double period,
                                         Func<double, double> terrainHeightAt,
                                         double toleranceM, int maxIterations)
        {
            Impact r = new Impact();
            if (terrainHeightAt == null || period <= 0.0) return r;

            double height = 0.0;
            for (int i = 0; i < maxIterations; i++)
            {
                r.Iterations = i + 1;

                double clamped = height;
                if (clamped > apoapsis - 1.0) clamped = apoapsis - 1.0;
                if (clamped < periapsis + 1.0) clamped = periapsis + 1.0;

                double up, down;
                Orbital.AltitudeToTrueAnomaly(sma, ecc, bodyRadius, clamped,
                                              periapsis, apoapsis, out up, out down);

                double t = TimeBetweenTrueAnomalies(ecc, period, currentTrueAnomaly, down);
                double sampled = terrainHeightAt(t);
                if (sampled < 0.0) sampled = 0.0;

                // ---- THE DAMPING. Averaging, not replacing. See the header. ----
                double next = (height + sampled) * 0.5;
                bool settled = Math.Abs(height - sampled) * 2.0 < toleranceM;

                height = next;
                r.TimeS = t;
                r.TerrainHeightM = height;

                if (settled) { r.Converged = true; break; }
            }

            r.Valid = periapsis < r.TerrainHeightM + toleranceM || r.Converged;
            return r;
        }

        // ------------------------------------------------------------------ closest approach

        public struct Approach
        {
            public bool Valid;
            public double TimeS;
            public double DistanceM;
        }

        public static Approach ClosestApproach(Func<double, double> distanceAt,
                                               double window, int steps, int refinements)
        {
            Approach r = new Approach();
            if (distanceAt == null || window <= 0.0 || steps < 2) return r;

            double stride = window / steps;
            double bestT = 0.0, bestD = distanceAt(0.0);

            for (int i = 1; i <= steps; i++)
            {
                double t = i * stride;
                double d = distanceAt(t);
                if (d < bestD) { bestD = d; bestT = t; }
            }

            for (int pass = 0; pass < refinements; pass++)
            {
                double lo = bestT - stride, hi = bestT + stride;
                if (lo < 0.0) lo = 0.0;
                if (hi > window) hi = window;
                stride /= 10.0;
                if (stride <= 0.0) break;

                for (double t = lo; t <= hi; t += stride)
                {
                    double d = distanceAt(t);
                    if (d < bestD) { bestD = d; bestT = t; }
                }
            }

            r.Valid = true;
            r.TimeS = bestT;
            r.DistanceM = bestD;
            return r;
        }

        public static double ClosingRate(Func<double, double> distanceAt, double dt)
        {
            if (distanceAt == null || dt <= 0.0) return 0.0;
            return (distanceAt(0.0) - distanceAt(dt)) / dt;
        }
    }
}
