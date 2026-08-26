// DragonScreen - Rendezvous
// ---- ⛔ NEVER POINT AT A CO-ORBITAL TARGET AND THRUST ----
// ---- THE LADDER, AND ITS MEASURED COSTS ----
// ---- TWO THINGS THE SIMULATION CAUGHT THAT WOULD OTHERWISE HAVE FLOWN ----
namespace DragonScreen
{
    public enum ApproachRung : byte
    {
        Idle = 0,
        Phasing,
        Clohessy,
        Rcs,
        Final,
        Docked
    }

    public struct ApproachInputs
    {
        public bool Valid;
        public bool HasTarget;
        public double RangeM;
        public double ClosingMps;
        public double PeriapsisM;
        public double PeriodS, TargetPeriodS;
        public double AlignDeg;
        public bool Docked;
    }

    public struct ApproachCommand
    {
        public ApproachRung Rung;
        public double TargetClosingMps;
        public double ClosingErrorMps;
        public bool FloorViolated;
        public string Note;
    }

    public static class Rendezvous
    {
        public const double PhasingRange = 3000.0;
        public const double CwRange = 500.0;

        public const double FinalRange = 50.0;

        public const double PeriapsisFloorM = 75000.0;

        public const double PeriapsisMarginM = 5000.0;

        public const double CwPeriodFraction = 0.20;

        public static ApproachRung Classify(ApproachInputs s)
        {
            if (!s.Valid || !s.HasTarget) return ApproachRung.Idle;
            if (s.Docked) return ApproachRung.Docked;
            if (s.RangeM > PhasingRange) return ApproachRung.Phasing;
            if (s.RangeM > CwRange) return ApproachRung.Clohessy;
            if (s.RangeM > FinalRange) return ApproachRung.Rcs;
            return ApproachRung.Final;
        }

        public static double CorridorRate(double rangeM)
        {
            if (rangeM <= 0.0) return 0.0;
            double v = rangeM * 0.025;
            if (v > 12.0) v = 12.0;
            if (rangeM < 10.0) { v = 0.15; }
            else if (v < 0.3) v = 0.3;
            return v;
        }

        public static bool FloorOk(double resultingPeriapsisM)
        {
            return resultingPeriapsisM >= PeriapsisFloorM;
        }

        public static double PhasingPeriodChange(ApproachInputs s, bool targetAhead)
        {
            if (s.TargetPeriodS <= 0.0) return 0.0;
            double circumference = s.TargetPeriodS * 7800.0;
            if (circumference <= 0.0) return 0.0;
            double frac = s.RangeM / circumference;
            double dT = s.TargetPeriodS * frac;
            return targetAhead ? -dT : dT;
        }

        public static ApproachCommand Guide(ApproachInputs s)
        {
            ApproachCommand c = new ApproachCommand();
            c.Rung = Classify(s);

            if (c.Rung == ApproachRung.Idle) { c.Note = "NO TARGET"; return c; }
            if (c.Rung == ApproachRung.Docked) { c.Note = "DOCKED"; return c; }

            c.TargetClosingMps = CorridorRate(s.RangeM);
            c.ClosingErrorMps = c.TargetClosingMps - s.ClosingMps;

            // ---- THE FLOOR IS CHECKED ON EVERY RUNG, NOT JUST BEFORE A BURN ----
            c.FloorViolated = !FloorOk(s.PeriapsisM);
            if (c.FloorViolated)
            {
                c.TargetClosingMps = 0.0;
                c.ClosingErrorMps = 0.0;
                c.Note = "PERIAPSIS FLOOR - APPROACH HELD";
                return c;
            }

            switch (c.Rung)
            {
                case ApproachRung.Phasing:   c.Note = "PHASING"; break;
                case ApproachRung.Clohessy:  c.Note = "CW TRANSFER"; break;
                case ApproachRung.Rcs:       c.Note = "RCS APPROACH"; break;
                default:                     c.Note = "FINAL APPROACH"; break;
            }
            return c;
        }

        public static string Name(ApproachRung r)
        {
            switch (r)
            {
                case ApproachRung.Phasing:  return "PHASING";
                case ApproachRung.Clohessy: return "CW TRANSFER";
                case ApproachRung.Rcs:      return "RCS APPROACH";
                case ApproachRung.Final:    return "FINAL APPROACH";
                case ApproachRung.Docked:   return "DOCKED";
                default:                    return "STANDBY";
            }
        }
    }
}
