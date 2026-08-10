/*
 * DragonScreen - Phasing
 *
 * PURE. Close an along-track gap with a phasing orbit. Ported from
 * `F9I/station_ops.ks:932 StPhaseLeg` and `:894 StAlongTrack`.
 *
 * ---- WHY A PHASING ORBIT AND NOT A BURN AT THE TARGET ----
 * F9I: "You cannot close a gap along your own orbit by pushing along it; you change your PERIOD and
 * let the geometry do the work." Ahead of the station means a longer period, i.e. a HIGHER orbit -
 * which is why this **can never drop us into the atmosphere** the way flight 012's pursuit
 * controller did.
 *
 * Two burns, both scalar, both prograde/retrograde at the same point - so that point becomes an
 * apsis of the phasing orbit and we return to exactly where we left. No basis, no handedness.
 *
 * ---- ⛔ TWO NUMERICAL TRAPS, BOTH PAID FOR WITH A FLIGHT. DO NOT "TIDY" EITHER. ----
 *
 * 1. NO FRACTIONAL POWERS. The semi-major axis was `(mu * (Tp/2pi)^2) ^ (1/3)`. On flight 014 the
 *    period was CORRECT - the HUD printed "32 min to close it", exactly right for a 52.9 km lead -
 *    and that line still produced 373,106 m instead of 691,957 m, which made the node a 1353 m/s
 *    RETROGRADE burn leaving periapsis at -539.9 km. Kepler linearised instead: T² ∝ a³, so
 *    dT/T = (3/2)(da/a), giving `a_phase = a_station * (1 + (2/3)(Tp - Ts)/Ts)`. Over a 1.3% period
 *    change the linearisation error is a few tens of METRES on a 687 km axis, and the gap is
 *    re-measured every pass anyway.
 *
 * 2. VIS-VIVA IS INLINE HERE, NOT A HELPER CALL. In kOS the helper's parameter shared a name with a
 *    local and flight 015 got -1300.69 m/s where the correct answer was +11.5 m/s. C# scoping makes
 *    that particular accident impossible, but the inline form is kept because it is what was flown
 *    and because the guard below is not a substitute for computing the right number:
 *    "a guard catching a bad number is not the same as computing a good one."
 */
namespace DragonScreen
{
    public struct PhasingInputs
    {
        /// <summary>Signed along-track gap, metres. POSITIVE means we are AHEAD of the station.</summary>
        public double GapM;
        /// <summary>Our orbital radius at the burn point, metres.</summary>
        public double RadiusM;
        /// <summary>Our orbital speed at the burn point, m/s.</summary>
        public double SpeedMps;
        /// <summary>Station orbital period, seconds.</summary>
        public double StationPeriodS;
        /// <summary>Station semi-major axis, metres.</summary>
        public double StationSmaM;
        /// <summary>Body gravitational parameter.</summary>
        public double Mu;
        /// <summary>Laps to spread the phasing over. More laps, less Δv, longer wait.</summary>
        public int Orbits;
    }

    public struct PhasingSolution
    {
        public bool Ok;
        /// <summary>Prograde Δv to enter the phasing orbit, m/s. Negative is retrograde.</summary>
        public double EntryDvMps;
        /// <summary>The phasing orbit's period, seconds.</summary>
        public double PhasePeriodS;
        /// <summary>Its semi-major axis, metres.</summary>
        public double PhaseSmaM;
        /// <summary>How long the phasing coast lasts, seconds.</summary>
        public double CoastS;
        /// <summary>True when we are ahead and must RAISE the orbit to fall back.</summary>
        public bool Ahead;
        public string Note;
    }

    public static class Phasing
    {
        /// <summary>Plan the burn this far ahead so the vehicle can orient first. `stCwLead`.</summary>
        public const double LeadS = 60.0;

        /// <summary>
        /// Reject a solution asking for more than this, m/s. `stCwMaxDv`. TUNABLE.
        ///
        /// A phasing burn to close a co-orbital gap is single-digit to low-tens of m/s. Anything near
        /// this cap is not a phasing burn, it is a wrong number - which is exactly how flight 014's
        /// 1353 m/s was caught before it flew.
        /// </summary>
        public const double MaxDvMps = 250.0;

        /// <summary>A period below this is nonsense, not a solution.</summary>
        public const double MinPeriodS = 60.0;

        /// <summary>
        /// Solve the phasing orbit.
        ///
        /// The angle we must give back is `gap / radius`; the period that gives it back in
        /// <see cref="PhasingInputs.Orbits"/> laps follows directly.
        /// </summary>
        public static PhasingSolution Solve(PhasingInputs p)
        {
            PhasingSolution s = new PhasingSolution();
            s.Ok = false;
            s.Ahead = p.GapM > 0.0;

            int laps = (p.Orbits > 0) ? p.Orbits : 1;
            if (p.RadiusM <= 0.0 || p.StationPeriodS <= 0.0 || p.StationSmaM <= 0.0 || p.Mu <= 0.0)
            {
                s.Note = "phasing: no usable orbit";
                return s;
            }

            double dTheta = p.GapM / p.RadiusM;
            double tp = p.StationPeriodS * (1.0 + dTheta / (2.0 * System.Math.PI * laps));
            if (tp < MinPeriodS)
            {
                s.Note = "phasing period came out nonsensical (" + tp.ToString("F0") + " s)";
                return s;
            }
            s.PhasePeriodS = tp;
            s.CoastS = tp * laps;

            // Kepler LINEARISED. See trap 1 in the header - do not restore the cube root.
            s.PhaseSmaM = p.StationSmaM
                        * (1.0 + (2.0 / 3.0) * (tp - p.StationPeriodS) / p.StationPeriodS);

            // Vis-viva, inline. See trap 2.
            double term = p.Mu * ((2.0 / p.RadiusM) - (1.0 / s.PhaseSmaM));
            if (term <= 0.0)
            {
                s.Note = "phasing: that semi-major axis is not reachable from here";
                return s;
            }
            double vPhase = System.Math.Sqrt(term);
            s.EntryDvMps = vPhase - p.SpeedMps;

            double mag = (s.EntryDvMps < 0.0) ? -s.EntryDvMps : s.EntryDvMps;
            if (mag > MaxDvMps)
            {
                s.Note = "phasing dv " + s.EntryDvMps.ToString("F1") + " m/s exceeds the "
                       + MaxDvMps.ToString("F0") + " m/s cap - the solution is wrong, not flying it";
                return s;
            }

            s.Ok = true;
            s.Note = (mag).ToString("F2") + " m/s each way, " + laps + " lap(s), period "
                   + p.StationPeriodS.ToString("F0") + " -> " + tp.ToString("F0") + " s";
            return s;
        }

        /// <summary>
        /// Δv to circularise back into the station's orbit at the end of the coast.
        ///
        /// A separate, trivially checkable scalar rather than something carried from the entry solve:
        /// the radius on return is measured then, not predicted now.
        /// </summary>
        public static double ExitDvMps(double radiusM, double speedMps, double mu)
        {
            if (radiusM <= 0.0 || mu <= 0.0) return 0.0;
            return System.Math.Sqrt(mu / radiusM) - speedMps;
        }

        /// <summary>
        /// ⚠ SANITY, NOT DECORATION. Closing a FORWARD gap means RAISING the orbit - the phasing
        /// orbit's semi-major axis must be larger than the station's when we are ahead, and smaller
        /// when we are behind. That property is the whole reason this cannot drop periapsis into the
        /// atmosphere, so it is worth asserting rather than assuming.
        /// </summary>
        public static bool DirectionSane(PhasingInputs p, PhasingSolution s)
        {
            if (!s.Ok) return true;
            if (p.GapM > 0.0) return s.PhaseSmaM > p.StationSmaM;
            if (p.GapM < 0.0) return s.PhaseSmaM < p.StationSmaM;
            return true;
        }
    }
}
