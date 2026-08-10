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

        // ---- TERMINAL LAW. StTerminal:1199. ----
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
