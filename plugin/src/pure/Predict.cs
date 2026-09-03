/*
 * DragonScreen - Predict
 *
 * PURE. Tranche 2 of the GNC port: where we will be, where we will hit, and when we pass closest to
 * something else. `COMMON/GNC.ks` - `TimeTwoTA`, `GroundTrack`, `ImpactUT`, `ClosestApproach`.
 *
 * ---- PURE, WITH THE SAMPLING HANDED IN ----
 * A predictor needs to ask the world questions - "how high is the terrain there", "where is the
 * target then". Those answers live in KSP. Rather than drag KSP in here and lose the headless tests,
 * the SEARCHES take a delegate and the glue supplies it. The search logic - which is the part with
 * the bugs in it - stays testable against analytic functions whose answers are known exactly.
 *
 * ---- THE TWO NON-OBVIOUS ONES ----
 * `ImpactUT` looks like it should be a one-shot solve and is not: the time you hit the ground depends
 * on the terrain height where you hit, which depends on where you hit, which depends on the time. So
 * it is a FIXED-POINT ITERATION, and F9I damps it by averaging the old and new heights
 * (`(impactHeight + newImpactHeight) / 2`) rather than taking the new one. Undamped, it oscillates
 * between a mountain and the valley behind it and never converges.
 *
 * `GroundTrack` is the one that catches people: a future position in INERTIAL space is over a
 * different LONGITUDE than it looks, because the body rotates underneath it. Predicting an impact
 * point without that shift puts it degrees out - and on Kerbin a degree is 10 472 m.
 *
 * ⚠ PROVENANCE (W1, 2026-09-04). Recovered from the tree deleted 2026-09-01. The CODE is
 * byte-identical between `0d6423d` (the last pre-comment-strip commit) and `8b81816^` (the recovery
 * target) - verified by a comment-stripped diff of the two - so this file is the `0d6423d` copy,
 * taken solely to recover the commentary `158eb2a` stripped, exactly as R1 §5.1 directs ("the damped
 * fixed-point rationale is the whole file"). No gen-1 LOGIC is imported by that choice.
 * The METHOD is ported from F9I `COMMON/GNC.ks`, which is STOCK-regime - but the ported thing is a
 * damped fixed-point scheme, not a number, and this file carries no physical constant (R1 §5.1).
 */
using System;

namespace DragonScreen
{
    public static class Predict
    {
        private const double TwoPi = 2.0 * Math.PI;

        // ------------------------------------------------------------------ timing

        /// <summary>
        /// Seconds to travel from one true anomaly to another on the same orbit. `TimeTwoTA`.
        ///
        /// Via MEAN anomaly, because that is the only one that advances linearly with time. Always
        /// returns a POSITIVE time within one period - going "backwards" means going the long way
        /// round, which is what an orbit actually does.
        /// </summary>
        public static double TimeBetweenTrueAnomalies(double ecc, double period,
                                                      double fromTrue, double toTrue)
        {
            if (period <= 0.0) return 0.0;
            double m1 = Orbital.TrueToMean(fromTrue, ecc);
            double m2 = Orbital.TrueToMean(toTrue, ecc);
            double frac = Orbital.Wrap(m2 - m1) / TwoPi;
            return period * frac;
        }

        /// <summary>
        /// Longitude of a point that will be reached <paramref name="dt"/> seconds from now, given
        /// the longitude its inertial position maps to today. `GroundTrack`.
        ///
        /// The body turns under the orbit, so the ground point drifts WEST relative to the inertial
        /// position at the body's rotation rate. Skip this and every predicted impact is wrong by
        /// (rotation rate x time of flight) - for a ten-minute Kerbin descent that is about 25
        /// degrees, or 260 km.
        /// </summary>
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

        /// <summary>Result of an impact solve.</summary>
        public struct Impact
        {
            public bool Valid;
            /// <summary>Seconds from now.</summary>
            public double TimeS;
            /// <summary>Terrain height the solution settled on, metres.</summary>
            public double TerrainHeightM;
            public bool Converged;
            public int Iterations;
        }

        /// <summary>
        /// When and where the orbit meets the ground. `ImpactUT`, ported with its damping.
        ///
        /// <paramref name="terrainHeightAt"/> is given a time-from-now and returns the terrain height
        /// under the vessel at that time. The glue supplies it; here it can be a test function.
        ///
        /// ---- WHY IT ITERATES, AND WHY IT MUST BE DAMPED ----
        /// The impact TIME depends on the terrain HEIGHT at the impact point, which depends on WHERE
        /// the impact is, which depends on the time. F9I closes that loop by averaging the previous
        /// height with the newly sampled one. Taking the new height outright makes the search
        /// oscillate between a peak and the valley behind it; the average halves the step each pass
        /// and it settles.
        ///
        /// Returns Converged=false rather than looping forever over terrain that will not settle -
        /// a caller flying a landing needs to know the prediction is untrustworthy, not wait for it.
        /// </summary>
        public static Impact SolveImpact(double sma, double ecc, double bodyRadius,
                                         double periapsis, double apoapsis,
                                         double currentTrueAnomaly, double period,
                                         Func<double, double> terrainHeightAt,
                                         double toleranceM, int maxIterations)
        {
            Impact r = new Impact();
            if (terrainHeightAt == null || period <= 0.0) return r;

            // An orbit whose periapsis clears the terrain never comes down. Say so instead of
            // returning a confident answer from a clamp.
            double height = 0.0;
            for (int i = 0; i < maxIterations; i++)
            {
                r.Iterations = i + 1;

                // Where does the orbit cross that height, coming DOWN? Falling root.
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
            /// <summary>Seconds from now.</summary>
            public double TimeS;
            public double DistanceM;
        }

        /// <summary>
        /// Time of closest approach between now and <paramref name="window"/>. `ClosestApproach`.
        ///
        /// Coarse-to-fine, exactly as F9I does it: scan the window at a stride, keep the best sample,
        /// then rescan a stride either side of it at a tenth of the stride, and repeat. Each pass
        /// buys one decimal place, and it costs `steps + 10*refinements` samples rather than the
        /// `steps * 10^refinements` a flat scan of the same resolution would.
        ///
        /// ⚠ IT FINDS A LOCAL MINIMUM, AND SO DOES F9I'S. On a rendezvous with several close passes
        /// in the window it returns whichever one the coarse scan happened to land in. That is fine
        /// for the approach ladder, which only ever asks about the NEXT pass - but a caller that
        /// needs the global best over many orbits must narrow the window itself.
        /// </summary>
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

        /// <summary>
        /// Is the vessel closing on the target or opening away from it? Sampled rather than
        /// differentiated, because the caller already has a distance function and a numerical
        /// derivative of a noisy one is worse than two samples.
        ///
        /// POSITIVE means closing, matching the sign convention on the DOCKING page and in
        /// `Rendezvous.ApproachInputs` - the one place a sign error would be read as good news.
        /// </summary>
        public static double ClosingRate(Func<double, double> distanceAt, double dt)
        {
            if (distanceAt == null || dt <= 0.0) return 0.0;
            return (distanceAt(0.0) - distanceAt(dt)) / dt;
        }
    }
}
