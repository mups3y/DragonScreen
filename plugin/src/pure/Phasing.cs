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

        /// <summary>Most laps <see cref="SolveAdaptive"/> will spread a gap over.</summary>
        public const int MaxLaps = 3;

        /// <summary>
        /// Solve, and if the answer is over the Δv cap, spread it over more laps and solve again.
        ///
        /// ---- WHY: ONE LAP CANNOT PAY FOR THE LARGEST GAPS, AND THE CAP IS RIGHT TO REFUSE ----
        /// The gap is a projection along the station's track, so it cannot exceed half a lap of its
        /// orbit - 2156 km at the measured 86.8 x 85.8 km. Simulated across that whole range against
        /// this exact law:
        ///
        ///      gap        1 lap                2 laps
        ///      1700 km    224.9 m/s  ok        -
        ///      2000 km    253.7 m/s  REJECTED  147.1 m/s  ok
        ///      2100 km    262.8 m/s  REJECTED  153.2 m/s  ok
        ///      2156 km    267.8 m/s  REJECTED  156.6 m/s  ok
        ///
        /// So a single lap leaves a band near the maximum that is refused for being expensive rather
        /// than for being wrong, and the fix costs nothing but time: two laps roughly halves the Δv
        /// and doubles the wait, 47 minutes to 79. The cap itself stays exactly where it is - it is
        /// what caught flight 014's 1353 m/s - and this only stops it rejecting arithmetic that was
        /// correct all along.
        ///
        /// ⚠ MORE LAPS IS NOT A RETRY. Each lap count is a DIFFERENT, complete solution, and the
        /// first one under the cap is flown. A solution rejected for direction or for a nonsensical
        /// period is not retried at all - those are wrong, not expensive.
        /// </summary>
        public static PhasingSolution SolveAdaptive(PhasingInputs p, out int lapsUsed)
        {
            lapsUsed = (p.Orbits > 0) ? p.Orbits : 1;
            PhasingSolution first = Solve(p);
            if (first.Ok) return first;

            // Only the Δv cap is worth spending laps on. Anything else is a bad number.
            if (first.Note == null || first.Note.IndexOf("exceeds the", System.StringComparison.Ordinal) < 0)
                return first;

            for (int laps = lapsUsed + 1; laps <= MaxLaps; laps++)
            {
                PhasingInputs q = p;
                q.Orbits = laps;
                PhasingSolution s = Solve(q);
                if (s.Ok) { lapsUsed = laps; return s; }
            }
            return first;
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

    /// <summary>
    /// Get co-orbital with the station before any of the approach ladder runs. Ported from
    /// `F9I/station_ops.ks:1959 StMatchStationOrbit`.
    ///
    /// ---- ⚠ THE OTHER IMPLEMENTATION IS DEAD CODE AND THE SOURCE SAYS SO ----
    /// `F9_payload.ks` also has `MatchPlanes` and `MatchSMA`, and the comment immediately below them
    /// reads: "DEAD SINCE 2026-08-04... The third orphan of the removed inline rendezvous... do not
    /// wire this one back in by mistake because the name reads right." They also drag in a whole
    /// Starship-inherited toolchain - Hohmann, PlaneMnv, VecToNode, TimeToNodeTarget - none of which
    /// is ported. StMatchStationOrbit is the live one and it is four lines of vis-viva.
    ///
    /// ---- AND THERE IS NO PLANE MATCH HERE, DELIBERATELY ----
    /// The station is at inclination 0.133 degrees. The plane is degenerate at the equator - the same
    /// reason the launch window is flown on phase angle rather than on plane - so a plane-match burn
    /// would be spending propellant to correct a difference that is inside the measurement noise.
    /// If the station is ever moved to a real inclination, THIS is where the plane match belongs and
    /// `DgPlaneMatch` is the thing to port into it.
    /// </summary>
    public static class OrbitMatch
    {
        /// <summary>
        /// Semi-major axes within this are already co-orbital, metres. `StMatchStationOrbit`'s 500.
        /// Below it the approach ladder can close everything that is left.
        /// </summary>
        public const double SmaToleranceM = 500.0;

        /// <summary>Do we need a matching burn at all?</summary>
        public static bool Needed(double ourSmaM, double stationSmaM)
        {
            double err = ourSmaM - stationSmaM;
            if (err < 0.0) err = -err;
            return err >= SmaToleranceM;
        }

        /// <summary>
        /// Prograde Δv to circularise at OUR APOAPSIS, m/s.
        ///
        /// Burning at apoapsis is what makes this one burn instead of two: the ascent already put our
        /// apoapsis within a few hundred metres of the station's radius, so circularising there both
        /// rounds the orbit off AND matches the altitude. Anywhere else and it would take a Hohmann.
        /// </summary>
        public static double CirculariseAtApoapsisDv(double apoapsisRadiusM, double ourSmaM,
                                                     double mu)
        {
            if (apoapsisRadiusM <= 0.0 || ourSmaM <= 0.0 || mu <= 0.0) return 0.0;
            double vNow = ReturnBudget.VisViva(apoapsisRadiusM, ourSmaM, mu);
            double vCirc = System.Math.Sqrt(mu / apoapsisRadiusM);
            return vCirc - vNow;
        }
    }
}
