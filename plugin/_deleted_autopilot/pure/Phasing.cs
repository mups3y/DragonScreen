// DragonScreen - Phasing
// ---- WHY A PHASING ORBIT AND NOT A BURN AT THE TARGET ----
// ---- ⛔ TWO NUMERICAL TRAPS, BOTH PAID FOR WITH A FLIGHT. DO NOT "TIDY" EITHER. ----
namespace DragonScreen
{
    public struct PhasingInputs
    {
        public double GapM;
        public double RadiusM;
        public double SpeedMps;
        public double StationPeriodS;
        public double StationSmaM;
        public double Mu;
        public int Orbits;

        public double PeriFloorRadiusM;
    }

    public struct PhasingSolution
    {
        public bool Ok;
        public double EntryDvMps;
        public double PhasePeriodS;
        public double PhaseSmaM;
        public double CoastS;
        public bool Ahead;
        public string Note;
    }

    public static class Phasing
    {
        public const double LeadS = 60.0;

        public const double MaxDvMps = 250.0;

        public const double MinPeriodS = 60.0;

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

            s.PhaseSmaM = p.StationSmaM
                        * (1.0 + (2.0 / 3.0) * (tp - p.StationPeriodS) / p.StationPeriodS);

            double term = p.Mu * ((2.0 / p.RadiusM) - (1.0 / s.PhaseSmaM));
            if (term <= 0.0)
            {
                s.Note = "phasing: that semi-major axis is not reachable from here";
                return s;
            }
            double vPhase = System.Math.Sqrt(term);
            s.EntryDvMps = vPhase - p.SpeedMps;

            // ---- WOULD THIS PHASING ORBIT DIP TOO LOW? ----
            if (p.PeriFloorRadiusM > 0.0)
            {
                double other = 2.0 * s.PhaseSmaM - p.RadiusM;
                double peri = (other < p.RadiusM) ? other : p.RadiusM;
                if (peri < p.PeriFloorRadiusM)
                {
                    s.Note = "phasing periapsis would be "
                           + ((peri - p.PeriFloorRadiusM) / 1000.0).ToString("F1")
                           + " km too low";
                    return s;
                }
            }

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

        public const int MaxLaps = 5;

        /// ---- WHY: ONE LAP CANNOT PAY FOR THE LARGEST GAPS, AND THE CAP IS RIGHT TO REFUSE ----
        public static PhasingSolution SolveAdaptive(PhasingInputs p, out int lapsUsed)
        {
            lapsUsed = (p.Orbits > 0) ? p.Orbits : 1;
            PhasingSolution first = Solve(p);
            if (first.Ok) return first;

            // ---- WHAT IS WORTH SPENDING A LAP ON ----
            if (first.Note == null
                || (first.Note.IndexOf("exceeds the", System.StringComparison.Ordinal) < 0
                    && first.Note.IndexOf("too low", System.StringComparison.Ordinal) < 0))
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

        public static double ExitDvMps(double radiusM, double speedMps, double mu)
        {
            if (radiusM <= 0.0 || mu <= 0.0) return 0.0;
            return System.Math.Sqrt(mu / radiusM) - speedMps;
        }

        public static bool DirectionSane(PhasingInputs p, PhasingSolution s)
        {
            if (!s.Ok) return true;
            if (p.GapM > 0.0) return s.PhaseSmaM > p.StationSmaM;
            if (p.GapM < 0.0) return s.PhaseSmaM < p.StationSmaM;
            return true;
        }
    }

    /// ---- ⚠ THE OTHER IMPLEMENTATION IS DEAD CODE AND THE SOURCE SAYS SO ----
    /// ---- AND THERE IS NO PLANE MATCH HERE, DELIBERATELY ----
    public static class OrbitMatch
    {
        public const double SmaToleranceM = 500.0;

        public static bool Needed(double ourSmaM, double stationSmaM)
        {
            double err = ourSmaM - stationSmaM;
            if (err < 0.0) err = -err;
            return err >= SmaToleranceM;
        }

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
