// DragonScreen - Entry
// ---- ⛔ THE SCHEDULE IS MEASURED FLIGHT DATA. DO NOT DERIVE IT FROM THEORY. ----
// ---- ⛔ AND THE TRAP THAT COST A FLIGHT ----
// ---- WHAT THIS IS AND IS NOT ----
namespace DragonScreen
{
    public enum EntryBand : byte
    {
        Coast = 0,
        Interface,
        High,
        Low,
        Final,
        Drogues,
        Mains,
        Splashdown
    }

    public struct EntryInputs
    {
        public bool Valid;
        public double AltitudeM;
        public double SurfaceSpeed;
        public double VerticalSpeed;
        public bool Splashed;
        public bool DroguesOut, MainsOut;
        public bool TrunkAttached;
    }

    public struct EntryCommand
    {
        public EntryBand Band;
        public double AngleOfAttackDeg;
        public bool Retrograde;
        public bool DeployDrogues, DeployMains;
        public string Note;
    }

    public static class Entry
    {
        public const double TrimAngleDeg = 15.0;

        public static readonly double[] Fractions = { 0.00, 1.00, 0.55, 0.13 };

        public const double InterfaceTop = 70000.0;
        public const double HighTop = 50000.0;
        public const double LowTop = 25000.0;
        public const double FinalTop = 10000.0;

        public const double DrogueAltitude = 5486.0;
        public const double MainAltitude = 1830.0;

        public static double AngleFor(EntryBand b)
        {
            switch (b)
            {
                case EntryBand.Interface: return TrimAngleDeg * Fractions[0];
                case EntryBand.High:      return TrimAngleDeg * Fractions[1];
                case EntryBand.Low:       return TrimAngleDeg * Fractions[2];
                case EntryBand.Final:     return TrimAngleDeg * Fractions[3];
                default:                  return 0.0;
            }
        }

        public static EntryCommand Guide(EntryInputs s)
        {
            EntryCommand c = new EntryCommand();
            c.Retrograde = true;

            if (!s.Valid) { c.Note = "no vessel"; return c; }

            if (s.Splashed) { c.Band = EntryBand.Splashdown; c.Note = "SPLASHDOWN"; return c; }
            if (s.MainsOut) { c.Band = EntryBand.Mains; c.Note = "UNDER MAINS"; return c; }
            if (s.DroguesOut)
            {
                c.Band = EntryBand.Drogues;
                c.Note = "UNDER DROGUES";
                c.DeployMains = s.AltitudeM <= MainAltitude;
                return c;
            }

            if (s.AltitudeM <= DrogueAltitude)
            {
                c.Band = EntryBand.Final;
                c.AngleOfAttackDeg = AngleFor(EntryBand.Final);
                c.DeployDrogues = true;
                c.Note = "DROGUE DEPLOY";
                return c;
            }

            if (s.AltitudeM > InterfaceTop) { c.Band = EntryBand.Coast; c.Note = "COAST TO ENTRY"; }
            else if (s.AltitudeM > HighTop)  c.Band = EntryBand.Interface;
            else if (s.AltitudeM > LowTop)   c.Band = EntryBand.High;
            else if (s.AltitudeM > FinalTop) c.Band = EntryBand.Low;
            else                             c.Band = EntryBand.Final;

            c.AngleOfAttackDeg = AngleFor(c.Band);
            if (c.Note == null) c.Note = Name(c.Band);
            return c;
        }

        public static string Name(EntryBand b)
        {
            switch (b)
            {
                case EntryBand.Interface:  return "ENTRY INTERFACE";
                case EntryBand.High:       return "LIFTING ENTRY";
                case EntryBand.Low:        return "LIFT BLEED OFF";
                case EntryBand.Final:      return "TERMINAL DESCENT";
                case EntryBand.Drogues:    return "DROGUES";
                case EntryBand.Mains:      return "MAINS";
                case EntryBand.Splashdown: return "SPLASHDOWN";
                default:                   return "COAST TO ENTRY";
            }
        }
    }
}
