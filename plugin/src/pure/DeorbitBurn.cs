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
        /// <summary>Monopropellant remaining, units. What the Dracos burn.</summary>
        public double MonoUnits;
    }

    public static class DeorbitBurn
    {
        // ---- F9I's CONSTANTS. dragon_deorbit.ks:96-265. ----
        /// <summary>
        /// Closed-loop cutoff: STOP once the predicted ballistic impact first comes within this of the
        /// target, metres. `dgLzTol` was 50 m - UNREACHABLE, and that is the whole bug (flight_0820).
        ///
        /// ⛔ THE BURN WAS OVERSHOOTING A GOOD SOLUTION. As periapsis is driven down the ballistic miss
        /// falls to a MINIMUM and then climbs again - past the minimum is a steeper, SHORTER entry.
        /// flight_0820_070846: the miss bottomed at 5.5 km (Pe -47 km) and the burn kept going to Pe
        /// -67 km, where the miss had grown back to 45.7 km; flight_0820_054631 did the same, 9.9 km ->
        /// 70 km. The 50 m cutoff can never fire (the achievable minimum is ~5-10 km, predictor- and
        /// step-limited), and the "close and no longer improving" backstop needs 25 worsening scans while
        /// the burn floors out in ~8 - so nothing stopped it at the minimum and the depth floor took it,
        /// tens of km SHORT, which a shorten-only entry cannot recover.
        ///
        /// Set to 15 km: the miss falls MONOTONICALLY to the minimum (verified on both flights, no
        /// blips), so the FIRST scan under 15 km is on the way DOWN - the SHALLOW, LONG side of the
        /// minimum - which is exactly where a shorten-only lifting entry wants to start. The entry then
        /// bleeds that ~10-15 km onto the LZ. Kept below the 20 km the worsening test exercises so that
        /// test stays about worsening, not tolerance.
        /// </summary>
        public const double LzToleranceM = 15000.0;
        /// <summary>The S2 de-orbit aims the impact this far PAST the LZ, metres. `dgOvershoot`.</summary>
        public const double OvershootM = 35000.0;
        /// <summary>Seconds between aim scans during the burn. `dgAimEvery`.</summary>
        public const double AimScanIntervalS = 0.50;
        /// <summary>Seconds of LEAD on the cutoff. Covers the loop tick. `dgCutLead`.</summary>
        public const double CutLeadS = 0.35;
        /// <summary>
        /// THE ENTRY PERIAPSIS THE BURN AIMS FOR, metres ASL (negative = subsurface). ⛔ 2026-08-20,
        /// AFTER RESEARCH: the burn NO LONGER aims the impact by deepening - that is one-way (retrograde
        /// only shortens) and near the grazing entry it is so sensitive that full thrust threw the impact
        /// 37 km past the target between two aim scans and slammed to the old -70 km floor
        /// (flight_0820_112928), unrecoverable. MechJeb's landing autopilot burns to a PERIAPSIS and then
        /// does a two-way course correction; so this is now just a sane entry depth. -30 km is close to
        /// F9I's proven `dgPeriTgtDraco` (-31.8 km): steep enough to re-enter cleanly, shallow enough that
        /// the impact lands near the target, and `EntryOps.Trim` (two-way RCS) does the aiming.
        /// </summary>
        public const double PeriapsisTargetM = -30000.0;

        /// <summary>How far above the target periapsis the deepen-throttle is still at full; it eases to
        /// zero as periapsis approaches the target so the burn does not slam past it. Metres.</summary>
        public const double DepthEaseM = 40000.0;
        /// <summary>Throttle ceiling while deepening to the entry periapsis. Gentle - this is not a slam.</summary>
        public const double DepthThrottleMax = 0.40;
        /// <summary>How much sideways (cross-null) push is blended into the retrograde burn: `normalize(
        /// retro + this * normal)`. A normal push cannot change periapsis, so it takes cross-track out
        /// while deepening without ever overshooting the depth. Small so the burn stays mostly retro.</summary>
        public const double CrossBlend = 0.30;

        // ---- ⛔ VECTORED STEERING. The burn is not pure retrograde (user, 2026-08-20). ----
        /// <summary>
        /// Miss at which the burn's steering reaches its full vector-gain cap, metres. Below it the
        /// blend toward the LZ tapers with the miss, so the thrust returns to pure retrograde as the
        /// impact arrives on target.
        /// </summary>
        public const double VectorScaleM = 60000.0;
        /// <summary>
        /// The most of the toward-LZ correction that is blended into the retrograde thrust direction,
        /// as `normalize(retro + gain * towardLZ)`. Kept BELOW 1.0 on purpose: with the correction
        /// weaker than retrograde the resultant can never point prograde, so periapsis keeps falling and
        /// the burn cannot lift itself back out of a re-entry. A horizontal toward-LZ vector that is
        /// anti-parallel to retrograde is absorbed by the normalise (it only shortens the vector, not
        /// turns it), so this naturally steers CROSS-track - the axis a retrograde burn cannot reach -
        /// and leaves along-track to the depth loop.
        /// </summary>
        public const double VectorMaxGain = 0.5;

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
        /// <summary>
        /// Consecutive worse scans that end the burn, once already close. Was 25 - too slow: past the
        /// miss minimum the burn deepens ~2.5 km/scan and floors out in ~8 scans, so 25 never accrued
        /// and the depth floor stopped it tens of km short. 4 is the BACKSTOP for an orbit whose
        /// achievable minimum sits ABOVE <see cref="LzToleranceM"/> (so the way-down cutoff never fires);
        /// it then stops within a few scans of the minimum instead of at the floor. On the flown orbits
        /// LzToleranceM fires first, on the way down, and this never accrues.
        /// </summary>
        public const int WorseLimit = 4;
        /// <summary>...and "already close" means inside this, metres.</summary>
        public const double CloseEnoughM = 30000.0;
        /// <summary>Runaway backstop, seconds.</summary>
        public const double MaxBurnS = 300.0;
        /// <summary>Below this the S2 is out of fuel and the burn ends.</summary>
        public const double S2FuelFloorUnits = 5.0;

        /// <summary>
        /// Below this the Dracos are out of monopropellant and the burn ends, units.
        ///
        /// ⛔ THE SAME GUARD THE S2 ALREADY HAD, ON THE PROPELLANT WE ACTUALLY BURN. Its absence
        /// is why the 2026-08-11 phase-down held the throttle open for 301 seconds on a dry tank and
        /// then reported "ABORTED - burn ran past its backstop" - 74.25 m/s commanded, 32.45 m/s
        /// residual, and a backstop named as the cause when the cause was an empty tank. A burn that
        /// cannot push must say so in its own words.
        ///
        /// Same 5-unit figure as the S2 floor for the same reason: the last of a tank is unusable
        /// ullage, and a "remaining" that is not zero is not the same as thrust.
        /// </summary>
        public const double MonoFloorUnits = 5.0;
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
