/*
 * DragonScreen - Approach
 *
 * PURE. The rendezvous ladder and the terminal approach, ported from `F9I/station_ops.ks` -
 * `StSpeedCap:603`, `StTerminal:1199`, and the ladder tuning block at :620-645.
 *
 * ---- ⛔ THE ONE RULE THAT COST A VEHICLE ----
 * `falcon-rendezvous-approach-law`: NEVER chase a co-orbital target. Pursuit steering de-orbited
 * flight 012 - it spent about 1.6 t of second stage and 38 units of monopropellant driving straight
 * at the station and put its own periapsis 15.6 km underground.
 *
 * ---- THE LADDER IS NOT A GUESS, AND F9I SAYS SO ----
 * station_ops.ks:620 - "These are not guesses. The whole ladder was simulated against flight 012's
 * actual geometry with an RK4 two-body propagator before it was written." Its table:
 *
 *      gap     phasing orbit (1 lap)      CW direct (0.20 period)
 *      51 km   17.7 m/s, peri untouched   289 m/s, peri -15.6 km
 *      30 km   10.4 m/s, peri untouched   171 m/s, peri  24.1 km
 *       5 km    1.8 m/s, peri untouched    29 m/s, peri  75.4 km
 *       2 km    0.7 m/s, peri untouched    12 m/s, peri  81.8 km
 *
 * Two things fall out of it. A PHASING ORBIT is 15-30x cheaper for any real gap and CANNOT drop
 * periapsis when we are ahead, because closing a forward gap means RAISING the orbit; it costs one
 * lap. CW is only affordable, and only periapsis-safe, once the gap is small.
 *
 * So the ladder is: PHASE the gap out, then CW the remainder, then RCS the last few hundred metres.
 */
namespace DragonScreen
{
    /// <summary>Which leg of the ladder the current geometry calls for.</summary>
    public enum ApproachLeg : byte
    {
        /// <summary>
        /// Not co-orbital yet. Circularise at apoapsis first - everything below assumes two orbits
        /// of the same size, and a CW solve against a different semi-major axis is nonsense.
        /// </summary>
        MatchOrbit = 0,
        /// <summary>Gap too large for CW at any sane cost. Use a phasing orbit.</summary>
        Phasing,
        /// <summary>Small enough for a two-impulse CW transfer.</summary>
        Clohessy,
        /// <summary>Inside CW's useful range. Straight-line RCS on a speed ladder.</summary>
        Terminal,
        /// <summary>
        /// Riding an intercept the orbits already give us, waiting on the velocity match.
        ///
        /// Distinct from <see cref="Clohessy"/> on purpose: nothing has been bought. F9I's leg 2 -
        /// "matching velocity there rather than buying a transfer" - is a warp and one burn, and a
        /// crew reading the leg name should be able to tell those apart.
        /// </summary>
        Intercept,
        /// <summary>At the aim point, station-keeping.</summary>
        Arrived
    }

    public struct TerminalCommand
    {
        /// <summary>Closing speed the ladder wants, m/s.</summary>
        public double WantClosingMps;
        /// <summary>Push forward along the commanded direction.</summary>
        public bool Thrust;
        /// <summary>Kill lateral drift rather than closing - it is outside its own deadband.</summary>
        public bool KillLateral;
        /// <summary>
        /// Nothing to do on any axis: stop steering AND stop pushing. F9I is explicit that this is
        /// worth having - the old loop re-locked steering every 0.1 s and "spent 38 units of mono on
        /// attitude alone". Coasting is free.
        /// </summary>
        public bool Coast;
        public string Note;
    }

    public static class Approach
    {
        // ---- LADDER THRESHOLDS. station_ops.ks:643-645. ----
        /// <summary>Above this along-track gap, use a phasing orbit rather than CW. `stPhaseMin`.</summary>
        public const double PhaseMinM = 3000.0;
        /// <summary>Laps to spread the phasing over. 2 halves the dv and doubles the wait.</summary>
        public const int PhaseOrbits = 1;
        /// <summary>Below this, stop planning nodes and fly it on RCS. `stCwTermRange`.</summary>
        public const double CwTermRangeM = 500.0;

        /// <summary>
        /// How far our orbital radius may differ from the station's before leg 0 fixes it, metres.
        ///
        /// Set from the simulated drift rate rather than picked. At the measured station orbit a
        /// circular orbit N metres off the station's radius drifts along-track by roughly 4.7 km per
        /// lap per kilometre of altitude error, so:
        ///
        ///      87 x 86 km   ->  +6.6 km per lap    the phasing loop absorbs this
        ///      90 x 86 km   ->  +34.9 km per lap   marginal
        ///     120 x 86 km   ->  +321 km per lap    diverges
        ///     182 x 81 km   ->  +933 km per lap    the 2026-08-11 flight; never converges
        ///
        /// 2 km buys about 9 km of drift per lap against a 3 km phasing exit threshold, so a lap can
        /// still close what the drift opens. Above that the loop is chasing its own tail.
        /// </summary>
        public const double CoOrbitalTolM = 2000.0;

        // ---- THE PHASING LOOP'S BOUND. `stPhaseMaxPass`, station_ops.ks:270. ----
        /// <summary>
        /// Phasing laps allowed before closing in from wherever we got to.
        ///
        /// ⛔ THE BOUND IS THE POINT. An unbounded ladder is what cost 28 orbit-match burns and
        /// 11.7 hours of requested warp on 2026-08-11, and F9I's own planner lost 60 days on flight
        /// 020, 89 on 015 and 13 on 014. Phasing is well-behaved in a way those were not - each lap
        /// closes a measured gap by a computed amount - but "well-behaved" is a reason to bound it at
        /// three, not a reason to leave it unbounded. F9I's fall-through is explicit and is the one
        /// we copy: log that the laps are used up and close in from here anyway.
        /// </summary>
        public const int PhaseMaxPass = 3;

        // ---- THE FREE INTERCEPT. station_ops.ks:247-253, 895-913. ----
        // ⚠ A TRANSFER YOU ALREADY HAVE IS CHEAPER THAN ONE YOU BUY. After the phasing laps the
        // orbits usually already cross close; F9I checks the existing closest approach BEFORE
        // planning anything, and if it is good enough it warps there and matches velocity instead.
        // Its own words: "riding the existing intercept ... Matching velocity there rather than
        // buying a transfer."
        /// <summary>Ride an existing intercept if it comes this close, metres. `stCaUseMax`.</summary>
        public const double CaUseMaxM = 27000.0;
        /// <summary>Search this many station periods ahead for it. `stCaSpan`.</summary>
        public const double CaSpanPeriods = 1.60;
        /// <summary>Coarse samples across that span. `stCaSteps`.</summary>
        public const int CaSteps = 60;
        /// <summary>Refinement passes, each buying a decimal place. `stCaRefine`.</summary>
        public const int CaRefine = 3;
        /// <summary>Closer than this and there is nothing left to match. `stMatchDist`.</summary>
        public const double MatchDistM = 200.0;
        /// <summary>Relative speed already low enough to call matched, m/s. `stMatchVel`.</summary>
        public const double MatchVelMps = 0.5;
        /// <summary>Do not bother warping a span shorter than this, seconds. `stWarpMinSpan`.</summary>
        public const double WarpMinSpanS = 60.0;
        /// <summary>Stop the warp this long before the match point, seconds. `stWarpMargin`.</summary>
        public const double WarpMarginS = 30.0;
        /// <summary>
        /// Plan a burn this far ahead so the vehicle can orient into it first. `stCwLead`.
        /// Same figure as <see cref="Phasing.LeadS"/>, and for the same reason.
        /// </summary>
        public const double LeadS = 60.0;
        /// <summary>
        /// Reject any approach burn bigger than this, m/s. `stCwMaxDv`.
        ///
        /// Shared with <see cref="Phasing.MaxDvMps"/>: a rendezvous burn of this size is a wrong
        /// number, not an expensive manoeuvre, whichever leg produced it.
        /// </summary>
        public const double MaxDvMps = 250.0;

        // ---- THE SPEED CAP. StSpeedCap:603, ordered NEAREST FIRST so the tightest band wins. ----
        // F9I's reason for the three bands rather than two, and it is a flight: "Flight 035 still
        // arrived too fast at the end, missed the port and bounced off the hull - 21.95 units of
        // monopropellant on the docking alone, more than the whole approach that delivered it there.
        // The step down to 1 m/s at 100 m matters most: below that range a miss is a COLLISION, not a
        // correction, and the capsule has to be slow enough for RCS to fix the alignment before
        // contact."
        public const double BandNearD = 100.0, BandNearV = 1.0;
        public const double BandMidD = 300.0, BandMidV = 5.0;
        public const double BandFarD = 600.0, BandFarV = 5.0;
        public const double BandOutD = 1500.0, BandOutV = 12.0;
        public const double BandMaxV = 25.0;

        // ---- TERMINAL LAW. ----
        // ⚠ CITED TO `StTerminal:1199` UNTIL 2026-08-12, AND `StTerminal` HAS NO CALLERS.
        // `check_live.py --audit` caught it. The constants are NOT wrong - `stCloseRate` is used by
        // the LIVE `StDirectApproach` at `station_ops.ks:695`:
        //     min(StSpeedCap(target:distance), max(stMatchVel, target:distance * stCloseRate))
        // - only the attribution was, and a citation pointing at dead code is how three flights got
        // spent porting `Flip2` and `Reentry1`.
        /// <summary>Commanded closing speed per metre of range, s^-1. `stCloseRate`.</summary>
        public const double CloseRate = 0.02;
        /// <summary>Never close faster than this, m/s. `stCloseMax`.</summary>
        public const double CloseMax = 120.0;
        /// <summary>Nor slower than this while still outside the goal, m/s. `stCloseMin`.</summary>
        public const double CloseMin = 1.5;
        /// <summary>Give up after this long, seconds. `stCloseTime`.</summary>
        public const double CloseTimeoutS = 1800.0;
        /// <summary>
        /// Deadband on the closing-rate error, m/s. Without it the controller corrects a few cm/s for
        /// ever and the monopropellant bill is the whole reason the approach fails later.
        /// </summary>
        public const double CloseDeadbandMps = 0.35;
        /// <summary>Only push when the nose is within this of the commanded direction, degrees.</summary>
        public const double ThrustConeDeg = 12.0;

        /// <summary>How far behind the station a CW leg aims. Never zero - see CwTargeting.</summary>
        public const double CwAimBehindM = 200.0;

        /// <summary>
        /// Hard closing-speed ceiling by range, m/s. NEAREST FIRST: the tightest band always wins.
        /// </summary>
        public static double SpeedCap(double rangeM)
        {
            if (rangeM <= BandNearD) return BandNearV;
            if (rangeM <= BandMidD) return BandMidV;
            if (rangeM <= BandFarD) return BandFarV;
            if (rangeM <= BandOutD) return BandOutV;
            return BandMaxV;
        }

        /// <summary>Which leg the geometry calls for. Range in metres, along-track gap in metres.</summary>
        public static ApproachLeg LegFor(double rangeM, double alongTrackGapM, double goalM,
                                         double ourSmaM, double stationSmaM)
        {
            if (rangeM <= goalM) return ApproachLeg.Arrived;
            if (rangeM <= CwTermRangeM) return ApproachLeg.Terminal;

            // ⚠ CO-ORBITAL FIRST. The CW solver assumes both vehicles are on orbits of the same
            // size - its whole frame is the station's LVLH - and the phasing solve reasons about a
            // PERIOD difference it is about to create. Neither means anything while the two orbits
            // are still different sizes, so the match comes before both.
            if (OrbitMatch.Needed(ourSmaM, stationSmaM)) return ApproachLeg.MatchOrbit;

            double gap = (alongTrackGapM < 0.0) ? -alongTrackGapM : alongTrackGapM;
            return (gap > PhaseMinM) ? ApproachLeg.Phasing : ApproachLeg.Clohessy;
        }

        /// <summary>
        /// The terminal law: RCS only, straight-line steering.
        ///
        /// ⚠ THE min() IS LAST, AND THE ORDER MATTERS. F9I:1218 - the range band must be a true
        /// ceiling. `CloseMin` is 1.5 m/s and would otherwise override the near band, and inside
        /// 100 m the band has to win outright or the capsule arrives too fast to fix its alignment.
        /// </summary>
        public static TerminalCommand Terminal(double rangeM, double closingMps, double lateralMps,
                                               double goalM, double elapsedS)
        {
            TerminalCommand c = new TerminalCommand();

            if (rangeM <= goalM) { c.Coast = true; c.Note = "AT AIM POINT"; return c; }
            if (elapsedS > CloseTimeoutS) { c.Coast = true; c.Note = "APPROACH TIMED OUT"; return c; }

            double ramp = rangeM * CloseRate;
            if (ramp > CloseMax) ramp = CloseMax;
            if (ramp < CloseMin) ramp = CloseMin;
            double want = SpeedCap(rangeM);
            if (ramp < want) want = ramp;          // min() LAST - the band is the ceiling
            c.WantClosingMps = want;

            double err = want - closingMps;
            double absErr = (err < 0.0) ? -err : err;
            double absLat = (lateralMps < 0.0) ? -lateralMps : lateralMps;

            if (absErr > CloseDeadbandMps)
            {
                // Too slow: push toward the station. Too fast: push away from it.
                c.Thrust = true;
                c.Note = (err > 0.0) ? "CLOSING" : "BRAKING";
            }
            else if (absLat > CloseDeadbandMps)
            {
                // Lateral drift has to be killed too, or we arrive ALONGSIDE the station rather
                // than at it.
                c.Thrust = true;
                c.KillLateral = true;
                c.Note = "KILLING DRIFT";
            }
            else
            {
                c.Coast = true;
                c.Note = "COASTING";
            }
            return c;
        }
    }
}
