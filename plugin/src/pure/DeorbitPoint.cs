/*
 * DragonScreen - DeorbitPoint
 *
 * PURE. WHEN to light the de-orbit burn, chosen so the burn LANDS ON THE TARGET instead of running
 * out of depth budget hundreds of km short of it.
 *
 * ---- ⛔ WHY THIS EXISTS: THE FIXED LEAD WAS F9I'S, NOT OURS ----
 * `Overflight` picks the pass for minimum CROSS-track, then places ignition a FIXED fraction of an
 * orbit before it (`PhaseArcFrac = 0.255`, `DescentTimeS = 845`) - both measured on F9I's lighter
 * capsule. On flight_0818 that fixed lead put the de-orbit burn 455 km long: the closed loop drove
 * periapsis to the -31.8 km depth floor still 455 km from target and stopped, and the entry (which can
 * only ever GIVE range away) clawed it back to a 92.6 km splashdown miss. The drag-aware predictor
 * called that miss before the burn (101.7 km vs 92.6 km actual - EntryOps.cs:194 validated it), which
 * is the green light to let it choose the ignition point instead of a constant.
 *
 * ---- WHAT IT DOES ----
 * The retrograde burn cannot move the orbit PLANE and barely moves the ground TRACK, so cross-track is
 * still Overflight's job and is left untouched. Along-track - where on that track the capsule comes
 * down - is decided almost entirely by WHEN the engine lights. This searches the ignition time within
 * the lap for the one that lands on the target, modelling the burn as an impulsive retrograde to the
 * aim periapsis and integrating the real drag descent (`Trajectory`, via the glue's delegate).
 *
 * Two-body throughout: a retrograde burn scales the velocity vector, preserving its direction, and the
 * new orbit's periapsis follows from energy and angular momentum. That is exact for the instant of the
 * burn, which is all the ignition search needs - the closed loop in `DeorbitBurn` flies the rest.
 */
using System;

namespace DragonScreen
{
    /// <summary>What a candidate ignition time produces, filled by the glue's delegate.</summary>
    public struct IgnitionMiss
    {
        /// <summary>True when the modelled burn came down inside the flight-time cap.</summary>
        public bool Ok;
        /// <summary>Ground distance from the modelled landing to the target, metres.</summary>
        public double MissM;
        /// <summary>Periapsis the modelled burn drove to, metres above sea level (negative = subsurface).</summary>
        public double PeriapsisM;
        /// <summary>Retrograde dv the model applied, m/s.</summary>
        public double DvMps;
    }

    /// <summary>The landing miss for a candidate ignition universal time. Supplied by the glue.</summary>
    public delegate IgnitionMiss IgnitionMissAtUt(double ut);

    public static class DeorbitPoint
    {
        /// <summary>
        /// Periapsis radius (distance from the body centre, metres) of the two-body orbit through the
        /// state (p, v). Works for bound and unbound orbits: with e >= 1 the semi-major axis is
        /// negative and a(1-e) is still the periapsis.
        /// </summary>
        public static double PeriapsisRadius(double px, double py, double pz,
                                             double vx, double vy, double vz, double mu)
        {
            double r = Math.Sqrt(px * px + py * py + pz * pz);
            if (r < 1.0 || mu <= 0.0) return r;
            double v2 = vx * vx + vy * vy + vz * vz;
            double energy = v2 / 2.0 - mu / r;

            // h = |r x v|
            double hx = py * vz - pz * vy;
            double hy = pz * vx - px * vz;
            double hz = px * vy - py * vx;
            double h2 = hx * hx + hy * hy + hz * hz;

            if (Math.Abs(energy) < 1e-12)                       // parabolic: rp = h^2 / (2 mu)
                return h2 / (2.0 * mu);

            double a = -mu / (2.0 * energy);
            double eArg = 1.0 + 2.0 * energy * h2 / (mu * mu);
            double e = Math.Sqrt(eArg > 0.0 ? eArg : 0.0);
            return a * (1.0 - e);
        }

        /// <summary>
        /// The retrograde dv (m/s) at the state (p, v) that lowers the geometric periapsis to
        /// <paramref name="targetRpM"/> (a RADIUS from the body centre). The burn is purely retrograde,
        /// so it scales the velocity magnitude down while preserving direction. Periapsis falls
        /// monotonically with dv, so a bisection is exact and cannot pick the wrong root.
        ///
        /// Returns 0 when the orbit is already at or below the target - a burn never has to ADD energy
        /// to come home.
        /// </summary>
        public static double DvForPeriapsis(double px, double py, double pz,
                                            double vx, double vy, double vz,
                                            double mu, double targetRpM)
        {
            double vmag = Math.Sqrt(vx * vx + vy * vy + vz * vz);
            if (vmag < 1e-6 || mu <= 0.0) return 0.0;

            double rp0 = PeriapsisRadius(px, py, pz, vx, vy, vz, mu);
            if (rp0 <= targetRpM) return 0.0;                   // already steep enough

            double lo = 0.0, hi = vmag * 0.999;                 // dv = vmag is a dead stop, radial fall
            for (int i = 0; i < 60; i++)
            {
                double mid = 0.5 * (lo + hi);
                double s = 1.0 - mid / vmag;
                double rp = PeriapsisRadius(px, py, pz, vx * s, vy * s, vz * s, mu);
                if (rp > targetRpM) lo = mid;                   // not enough dv yet
                else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        // ---- the ignition-time search. Same coarse-then-refine shape as Overflight.Search. ----

        /// <summary>Coarse sweep step of the ignition search, seconds.</summary>
        public const double CoarseStepS = 20.0;
        /// <summary>First refining step, seconds.</summary>
        public const double RefineStartStepS = 5.0;
        /// <summary>Refining stops below this step, seconds.</summary>
        public const double RefineFloorS = 0.25;
        /// <summary>Each refining pass searches ±(step × this) around the best so far.</summary>
        public const int RefineHalfWidthSteps = 10;
        /// <summary>...and then divides the step by this.</summary>
        public const double RefineDivisor = 5.0;

        /// <summary>
        /// Search the ignition universal time in [loUt, hiUt] that lands closest to the target. The
        /// window must be shorter than one orbit so the along-track miss has a single minimum in it -
        /// the glue passes the lap that leads up to Overflight's chosen pass.
        ///
        /// Returns loUt with Ok = false if the delegate never produced a landing (an orbit the model
        /// could not bring down inside the window), so the glue can fall back rather than trust a
        /// meaningless time.
        /// </summary>
        public static IgnitionMiss Search(double loUt, double hiUt, IgnitionMissAtUt f,
                                          out double bestUt)
        {
            IgnitionMiss best = new IgnitionMiss();
            best.MissM = double.MaxValue;
            bestUt = loUt;

            if (f == null || hiUt <= loUt) { best.Ok = false; return best; }

            for (double t = loUt; t <= hiUt; t += CoarseStepS)
            {
                IgnitionMiss m = f(t);
                if (m.Ok && m.MissM < best.MissM) { best = m; bestUt = t; }
            }
            if (!best.Ok) return best;                          // nothing came down; caller falls back

            double step = RefineStartStepS;
            while (step >= RefineFloorS)
            {
                double lo = bestUt - step * RefineHalfWidthSteps;
                double hi = bestUt + step * RefineHalfWidthSteps;
                if (lo < loUt) lo = loUt;
                if (hi > hiUt) hi = hiUt;
                for (double u = lo; u <= hi; u += step)
                {
                    IgnitionMiss m = f(u);
                    if (m.Ok && m.MissM < best.MissM) { best = m; bestUt = u; }
                }
                step /= RefineDivisor;
            }
            return best;
        }
    }
}
