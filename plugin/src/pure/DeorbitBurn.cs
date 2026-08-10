/*
 * DragonScreen - DeorbitBurn
 *
 * PURE. The closed-loop de-orbit burn law, ported from `F9I/dragon_deorbit.ks:1328 DgDeorbitBurn`.
 *
 * ---- IT IS FLOWN AGAINST THE AIM POINT, NOT AGAINST A TARGET PERIAPSIS ----
 * The throttle is a function of how far the predicted impact still misses the landing zone by. The
 * periapsis target is not the objective - it is the DEPTH LIMIT, a floor the burn must not punch
 * through while chasing the aim point. Both conditions can end the burn and they mean different
 * things, which is why they are separate tests here rather than one.
 *
 * ---- ⛔ THE CUT-OUT NEEDS A LEAD, AND FLIGHT 035 MEASURED WHY ----
 * The depth test used to sit in the outer loop, one line after the trajectory scan. Reading periapsis
 * is free; scanning the trajectory is not. F9I's measurement: "periapsis crossed the -31,800 m target
 * at t=289.53 s and the throttle stayed at 0.0642 until t=292.77 s - 3.24 s, one loop iteration -
 * ending at -39,699 m. That 7.9 km of excess depth is a steeper entry than planned, and the capsule
 * trim then spends the whole descent hauling the impact point back."
 *
 * So the depth test is polled at physics rate, and it projects periapsis FORWARD by `CutLeadS`. The
 * rate is negative while burning, so the projection trips EARLY, never late. On the Dracos periapsis
 * falls at about 2,500 m/s, which makes every tenth of a second of latency 250 m of entry depth.
 *
 * ---- AND "GETTING WORSE" IS A STOP CONDITION ----
 * If the miss has been climbing for long enough while already close, the burn is no longer helping.
 * Continuing past that point is how a capsule trades a small miss for a steep entry.
 */
namespace DragonScreen
{
    public struct DeorbitState
    {
        /// <summary>
        /// Predicted impact miss from the landing zone, metres. NEGATIVE means the predictor has no
        /// answer yet - a distinct case, not a zero miss.
        /// </summary>
        public double AimMissM;
        /// <summary>Current periapsis, metres. Negative is below the surface, which is the point.</summary>
        public double PeriapsisM;
        /// <summary>Rate of change of periapsis, m/s. Negative while burning.</summary>
        public double PeriapsisRateMps;
        /// <summary>Best miss seen so far this burn, metres.</summary>
        public double BestMissM;
        /// <summary>Consecutive scans on which the miss was worse than the best by the margin.</summary>
        public int WorseCount;
        /// <summary>Seconds since ignition.</summary>
        public double ElapsedS;
        /// <summary>Liquid fuel remaining if burning the S2, units. Ignored on Dracos.</summary>
        public double S2FuelUnits;
        public bool UsingS2;
    }

    public static class DeorbitBurn
    {
        // ---- F9I's CONSTANTS. dragon_deorbit.ks:96-265. ----
        /// <summary>Closed-loop cutoff: impact within this of the target, metres. `dgLzTol`.</summary>
        public const double LzToleranceM = 50.0;
        /// <summary>The S2 de-orbit aims the impact this far PAST the LZ, metres. `dgOvershoot`.</summary>
        public const double OvershootM = 35000.0;
        /// <summary>Seconds between aim scans during the burn. `dgAimEvery`.</summary>
        public const double AimScanIntervalS = 0.50;
        /// <summary>Seconds of LEAD on the cutoff. Covers the loop tick. `dgCutLead`.</summary>
        public const double CutLeadS = 0.35;
        /// <summary>
        /// Periapsis the burn drives to, metres. The DEPTH LIMIT, not the objective.
        /// `dgPeriTgtDraco` - no trim authority on Dracos, so aim the entry directly.
        /// </summary>
        public const double PeriapsisTargetM = -31800.0;

        /// <summary>Throttle ceiling. The burn is long and shallow, not a slam. `min(0.60, ...)`.</summary>
        public const double ThrottleMax = 0.60;
        /// <summary>Floor while still burning.</summary>
        public const double ThrottleMin = 0.01;
        /// <summary>Throttle used while the predictor has no answer yet.</summary>
        public const double ThrottleBlind = 0.50;
        /// <summary>Scale inside the sqrt. A 400 km miss is full throttle; 4 km is 10%.</summary>
        public const double ThrottleScaleM = 400000.0;

        /// <summary>A miss worse than the best by this counts toward the diverging test, metres.</summary>
        public const double WorseMarginM = 500.0;
        /// <summary>Consecutive worse scans that end the burn, once already close.</summary>
        public const int WorseLimit = 25;
        /// <summary>...and "already close" means inside this, metres.</summary>
        public const double CloseEnoughM = 30000.0;
        /// <summary>Runaway backstop, seconds.</summary>
        public const double MaxBurnS = 300.0;
        /// <summary>Below this the S2 is out of fuel and the burn ends.</summary>
        public const double S2FuelFloorUnits = 5.0;
        /// <summary>Aligned enough to ignite, degrees.</summary>
        public const double AlignedDeg = 4.0;

        /// <summary>
        /// Throttle from the aim miss. `sqrt(miss / 400000)`, clamped.
        ///
        /// Square root rather than linear: it holds meaningful thrust well after the miss has come
        /// down, so the last kilometres are still being flown rather than drifted. A linear law is
        /// almost off by the time it matters.
        /// </summary>
        public static double Throttle(DeorbitState s)
        {
            if (s.AimMissM < 0.0) return ThrottleBlind;         // predictor has no answer yet
            double t = System.Math.Sqrt(s.AimMissM / ThrottleScaleM);
            if (t > ThrottleMax) t = ThrottleMax;
            if (t < ThrottleMin) t = ThrottleMin;
            return t;
        }

        /// <summary>
        /// ⛔ THE DEPTH LIMIT, PROJECTED FORWARD. Poll this at physics rate between aim scans - it is
        /// free, and the scan is not. See the header for the 7.9 km flight 035 paid for the latency.
        /// </summary>
        public static bool DepthLimitReached(double periapsisM, double periapsisRateMps)
        {
            return (periapsisM + periapsisRateMps * CutLeadS) < PeriapsisTargetM;
        }

        /// <summary>Is the burn finished, and why?</summary>
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
            if (s.ElapsedS > MaxBurnS)
            {
                why = "ABORTED - burn ran past its backstop";
                return true;
            }
            why = "";
            return false;
        }

        /// <summary>
        /// Update the best-miss tracking. Kept here so the diverging test cannot be reimplemented
        /// slightly differently by a caller.
        /// </summary>
        public static void Track(ref DeorbitState s)
        {
            if (s.AimMissM < 0.0) return;
            if (s.AimMissM <= s.BestMissM) { s.BestMissM = s.AimMissM; s.WorseCount = 0; }
            else if (s.AimMissM > s.BestMissM + WorseMarginM) s.WorseCount++;
        }
    }
}
