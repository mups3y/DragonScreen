// DragonScreen - Approach
// ---- ⛔ THE ONE RULE THAT COST A VEHICLE ----
// ---- THE LADDER IS NOT A GUESS, AND F9I SAYS SO ----
namespace DragonScreen
{
    public enum ApproachLeg : byte
    {
        MatchOrbit = 0,
        Phasing,
        Clohessy,
        Terminal,
        Intercept,
        Arrived
    }

    public struct TerminalCommand
    {
        public double WantClosingMps;
        public bool Thrust;
        public bool KillLateral;
        public bool Coast;
        public string Note;
    }

    public static class Approach
    {
        // ---- LADDER THRESHOLDS. station_ops.ks:643-645. ----
        public const double PhaseMinM = 3000.0;
        public const int PhaseOrbits = 1;
        public const double CwTermRangeM = 500.0;

        public const double CoOrbitalTolM = 2000.0;

        // ---- THE PHASING LOOP'S BOUND. `stPhaseMaxPass`, station_ops.ks:270. ----
        public const int PhaseMaxPass = 3;

        // ---- THE FREE INTERCEPT. station_ops.ks:247-253, 895-913. ----
        public const double CaUseMaxM = 27000.0;
        public const double CaSpanPeriods = 1.60;
        public const int CaSteps = 60;
        public const int CaRefine = 3;
        public const double MatchDistM = 200.0;
        public const double MatchVelMps = 0.5;
        public const double WarpMinSpanS = 60.0;
        public const double WarpMarginS = 30.0;
        public const double LeadS = 60.0;
        public const double MaxDvMps = 250.0;

        // ---- THE SPEED CAP. StSpeedCap:603, ordered NEAREST FIRST so the tightest band wins. ----
        public const double BandNearD = 100.0, BandNearV = 1.0;
        public const double BandMidD = 300.0, BandMidV = 5.0;
        public const double BandFarD = 600.0, BandFarV = 5.0;
        public const double BandOutD = 1500.0, BandOutV = 12.0;
        public const double BandMaxV = 25.0;

        // ---- TERMINAL LAW. `StDirectDv:1361`, which is LIVE - called at :1443 and :1481. ----
        public const double CloseRate = 0.02;
        public const double CloseMax = 120.0;
        public const double CloseMin = 1.5;
        public const double CloseTimeoutS = 1800.0;
        public const double CloseDeadbandMps = 0.35;
        public const double ThrustConeDeg = 12.0;

        public const double CwAimBehindM = 200.0;

        public static double SpeedCap(double rangeM)
        {
            if (rangeM <= BandNearD) return BandNearV;
            if (rangeM <= BandMidD) return BandMidV;
            if (rangeM <= BandFarD) return BandFarV;
            if (rangeM <= BandOutD) return BandOutV;
            return BandMaxV;
        }

        public static ApproachLeg LegFor(double rangeM, double alongTrackGapM, double goalM,
                                         double ourSmaM, double stationSmaM)
        {
            if (rangeM <= goalM) return ApproachLeg.Arrived;
            if (rangeM <= CwTermRangeM) return ApproachLeg.Terminal;

            if (OrbitMatch.Needed(ourSmaM, stationSmaM)) return ApproachLeg.MatchOrbit;

            double gap = (alongTrackGapM < 0.0) ? -alongTrackGapM : alongTrackGapM;
            return (gap > PhaseMinM) ? ApproachLeg.Phasing : ApproachLeg.Clohessy;
        }

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
            if (ramp < want) want = ramp;
            c.WantClosingMps = want;

            double err = want - closingMps;
            double absErr = (err < 0.0) ? -err : err;
            double absLat = (lateralMps < 0.0) ? -lateralMps : lateralMps;

            if (absErr > CloseDeadbandMps)
            {
                c.Thrust = true;
                c.Note = (err > 0.0) ? "CLOSING" : "BRAKING";
            }
            else if (absLat > CloseDeadbandMps)
            {
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
