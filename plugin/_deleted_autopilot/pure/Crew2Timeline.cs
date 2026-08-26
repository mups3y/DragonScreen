// DragonScreen - Crew2Timeline
namespace DragonScreen
{
    public struct Crew2Event
    {
        public double TPlusS;
        public string Name;
        public Crew2Event(double t, string name) { TPlusS = t; Name = name; }
    }

    public static class Crew2Timeline
    {
        // ---- THE FLOWN CREW-2 LAUNCH SEQUENCE, T+ seconds ----
        public static readonly Crew2Event[] Events = new Crew2Event[]
        {
            new Crew2Event(  0.0, "LIFTOFF"),
            new Crew2Event( 62.0, "MAX Q"),
            new Crew2Event( 69.0, "MACH 1"),
            new Crew2Event(156.0, "MECO"),
            new Crew2Event(159.0, "STAGE SEP"),
            new Crew2Event(167.0, "SES-1 (M-VAC IGNITION)"),
            new Crew2Event(447.0, "1ST STAGE ENTRY BURN"),
            new Crew2Event(527.0, "SECO-1 (ORBIT INSERTION)"),
            new Crew2Event(543.0, "1ST STAGE LANDING BURN"),
            new Crew2Event(570.0, "1ST STAGE LANDING"),
            new Crew2Event(718.0, "DRAGON SEPARATION"),
            new Crew2Event(782.0, "NOSECONE OPEN"),
        };

        public static int CurrentIndex(double metS)
        {
            int idx = -1;
            for (int i = 0; i < Events.Length; i++)
                if (metS >= Events[i].TPlusS) idx = i; else break;
            return idx;
        }

        public static Crew2Event Current(double metS)
        {
            int i = CurrentIndex(metS);
            return (i >= 0) ? Events[i] : new Crew2Event(0.0, "-");
        }

        public static bool Next(double metS, out Crew2Event next)
        {
            int i = CurrentIndex(metS);
            if (i + 1 < Events.Length) { next = Events[i + 1]; return true; }
            next = new Crew2Event(0.0, "-");
            return false;
        }

        public static double TimeToNext(double metS)
        {
            Crew2Event n;
            if (!Next(metS, out n)) return double.NaN;
            return n.TPlusS - metS;
        }

        public static double SyncErrorS(string eventName, double ourMetAtEventS)
        {
            for (int i = 0; i < Events.Length; i++)
                if (Events[i].Name == eventName) return ourMetAtEventS - Events[i].TPlusS;
            return double.NaN;
        }
    }
}
