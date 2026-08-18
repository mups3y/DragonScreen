/*
 * DragonScreen - EntryMargin
 *
 * PURE. The lifting-entry range schedule, ported from `F9I/dragon_deorbit.ks:2075-2108`
 * (`DgLongMargin`, `DgLongMarginRaw`).
 *
 * ---- WHAT THIS IS ----
 * A Dragon flies a LIFTING entry: it is offset in the shield so it generates lift, and it controls
 * where it lands by BANKING that lift vector - lift up to stretch the trajectory, lift down to
 * shorten it. That is what the real capsule does and what F9I flies.
 *
 * The schedule below answers one question at every altitude: **how far PAST the landing site should
 * the predicted impact point be right now?** The guidance then banks to hold that margin. Wanting to
 * land long on the way down is the whole trick - drag only ever shortens a trajectory, so a capsule
 * aiming exactly at the target arrives short, and one aiming long can always give range away.
 *
 * ---- ⛔ EVERY NUMBER IS MEASURED, FROM A NAMED FLIGHT. DO NOT ROUND THEM. ----
 * These are not a curve fit to physics. They are the remaining drift actually observed on
 * CargoDragon_040 and _047 - down-error at altitude minus its final value - and the breakpoints moved
 * twice because of what those flights did:
 *
 *   * The original tail ran to zero at drogue altitude, which "left the profile asking for 6 km of
 *     overshoot at the moment the capsule was 1.7 km from the LZ - i.e. still aiming downrange while
 *     already overhead". Both 038 and 040 settled at a consistent 5 km long bias.
 *   * Converging at 12 km instead fixed that, and then _047 landed 3.2 km SHORT because the profile
 *     "stopped asking for overshoot early" while the capsule still had range to fly. Zero point moved
 *     again, 12 km -> 9 km.
 *
 * "Want long" must be ZERO when we are over the target: past that point there is no range left to
 * give away, only altitude.
 *
 * ---- AND IT IS ONLY VALID FOR THE ENERGY IT WAS MEASURED AT ----
 * `DgEntryGuidance` stamps the entry condition into the log on every flight for exactly this reason:
 * speed, altitude, periapsis, inclination, mass and the margin scale. A run from a different orbit is
 * then re-fittable instead of just "it missed again". Any port that drops that logging throws away
 * the thing that makes the table maintainable.
 */
namespace DragonScreen
{
    public static class EntryMargin
    {
        /// <summary>
        /// Global multiplier on the whole "aim long" schedule. `dgMarginScale`.
        ///
        /// ⛔ SET TO 0 (user directive, 2026-08-19): AIM THE PREDICTED IMPACT DIRECTLY AT THE LZ, and let
        /// the lift steering only null drift to stay on target. Our `ImpactPredictor` is DRAG-AWARE, so
        /// DownrangeErr = 0 already means "lands on the LZ." The F9I 341 km-at-71 km overshoot (measured
        /// on a DIFFERENT capsule) made the loop read the impact as hundreds of km "below profile,"
        /// command 0 (shorten-only cannot extend) and COAST OPEN-LOOP the whole descent - landed 107 km
        /// off (flight_0818_210822) after the pre-entry trim already had it at -381 m. With scale 0 the
        /// loop steers on the real miss all the way down.
        ///
        /// [Tunable]. If a flight then lands consistently SHORT (the predictor integrates drag but not
        /// the lift the capsule banks with), a small positive value is the tune - but try 0 FIRST.
        /// </summary>
        [Tunable] public static double MarginScale = 0.0;

        /// <summary>Altitude at which the schedule reaches zero. Re-measured twice; see the header.</summary>
        public const double ZeroAtM = 9000.0;

        /// <summary>
        /// How far PAST the landing site the predicted impact should be, metres, at altitude
        /// <paramref name="altitudeM"/>. Scaled by <see cref="MarginScale"/>.
        /// </summary>
        public static double WantLongM(double altitudeM)
        {
            return MarginScale * RawM(altitudeM);
        }

        /// <summary>
        /// The measured table, unscaled. Piecewise linear, breakpoints exactly as flown.
        ///
        /// Reading it as a curve: 215 km of wanted overshoot at 50 km altitude, decaying through
        /// 126 km at 36 km, 80 km at 31 km, 58 km at 28 km, 28 km at 23 km, 10 km at 18 km, 3.2 km at
        /// 14 km, 1.9 km at 12 km, and zero at 9 km.
        /// </summary>
        public static double RawM(double h)
        {
            if (h >= 50000.0) return 215000.0 + (h - 50000.0) * 6.0;
            if (h >= 36000.0) return 126000.0 + (h - 36000.0) * (89000.0 / 14000.0);
            if (h >= 31000.0) return 80000.0 + (h - 31000.0) * (46000.0 / 5000.0);
            // Tail re-measured on CargoDragon_040 (landed 2.62 km out).
            if (h >= 28000.0) return 58000.0 + (h - 28000.0) * (22000.0 / 3000.0);
            if (h >= 23000.0) return 28000.0 + (h - 23000.0) * (30000.0 / 5000.0);
            if (h >= 18000.0) return 10000.0 + (h - 18000.0) * (18000.0 / 5000.0);
            // Tail re-measured again on CargoDragon_047 (5.55 km out, cross-track 0.17 km).
            if (h >= 14000.0) return 3200.0 + (h - 14000.0) * (1500.0 / 4000.0);
            if (h >= 12000.0) return 1900.0 + (h - 12000.0) * (1300.0 / 2000.0);
            if (h >= ZeroAtM) return (h - ZeroAtM) * (1900.0 / 3000.0);
            return 0.0;
        }

        /// <summary>
        /// The schedule must never ask for more overshoot than there is ground left to give away.
        /// F9I clamps against the remaining along-track distance for the same reason the tail was
        /// re-measured twice: a capsule that is already overhead has no range to trade, only altitude.
        /// </summary>
        public static double WantLongClampedM(double altitudeM, double alongTrackRemainingM)
        {
            double want = WantLongM(altitudeM);
            if (alongTrackRemainingM <= 0.0) return 0.0;
            return (want > alongTrackRemainingM) ? alongTrackRemainingM : want;
        }

        // ShortenErrorM (predictedLong - WantLongClampedM) was removed 2026-08-18 (audit D4). It was
        // dead API - called by nothing - and, unlike the flown loop, it formed the error as
        // (predictedLong - clampedWant) rather than EntryGuidance.Update's (downrangeErr + clampedWant),
        // so it was NOT a drop-in for the live path and could have gone silently stale. WantLongClampedM
        // above is now called directly by Update, which is the one place the clamp is needed.
        //
        // ⚠ SHORTEN-ONLY IS THE FLOWN BEHAVIOUR. `falcon-dragon-entry-solution`: the working entry
        // "steers a long-margin schedule with shorten-only + lead" - aimed deliberately long, only ever
        // giving range away, because lift-up authority is far weaker than drag.
    }
}
