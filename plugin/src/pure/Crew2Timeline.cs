/*
 * DragonScreen - Crew2Timeline
 *
 * PURE. The real Crew-2 mission clock: every launch-broadcast event at its exact T+ time, so our own
 * flight can be measured AGAINST the livestream. The end goal (user 2026-08-22) is for our events to
 * line up with the commentators - MECO when they call MECO, SECO when they call SECO. This module is
 * the reference they are compared to: given our MET, it names the event we should be at and the next
 * one, with the delta. A display or a log line turns that into "MECO T+2:36 - we are 4 s late".
 *
 * Times are the flown Crew-2 sequence (spacex.com/CREW-2, Spaceflight Now, NASA), converted to seconds.
 * The ascent + booster + Dragon-sep events are here - the stretch the launch broadcast actually covers;
 * the day-2 rendezvous burns (T+27-31 h) are in CREW2_RSS_RESEARCH.md, not on this clock.
 */
namespace DragonScreen
{
    /// <summary>One Crew-2 event: seconds after liftoff, and what the broadcast calls it.</summary>
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
            new Crew2Event( 62.0, "MAX Q"),          // T+1:02
            new Crew2Event( 69.0, "MACH 1"),         // T+1:09
            new Crew2Event(156.0, "MECO"),           // T+2:36
            new Crew2Event(159.0, "STAGE SEP"),      // T+2:39
            new Crew2Event(167.0, "SES-1 (M-VAC IGNITION)"),   // T+2:47
            new Crew2Event(447.0, "1ST STAGE ENTRY BURN"),     // T+7:27
            new Crew2Event(527.0, "SECO-1 (ORBIT INSERTION)"), // T+8:47
            new Crew2Event(543.0, "1ST STAGE LANDING BURN"),   // T+9:03
            new Crew2Event(570.0, "1ST STAGE LANDING"),        // T+9:30 (droneship)
            new Crew2Event(718.0, "DRAGON SEPARATION"),        // T+11:58
            new Crew2Event(782.0, "NOSECONE OPEN"),            // T+13:02
        };

        /// <summary>Index of the most recent event at or before this MET, or -1 before liftoff.</summary>
        public static int CurrentIndex(double metS)
        {
            int idx = -1;
            for (int i = 0; i < Events.Length; i++)
                if (metS >= Events[i].TPlusS) idx = i; else break;
            return idx;
        }

        /// <summary>The event we should be at (most recent). Name "-" and time 0 before liftoff.</summary>
        public static Crew2Event Current(double metS)
        {
            int i = CurrentIndex(metS);
            return (i >= 0) ? Events[i] : new Crew2Event(0.0, "-");
        }

        /// <summary>The next scheduled event, or false if the launch sequence is complete.</summary>
        public static bool Next(double metS, out Crew2Event next)
        {
            int i = CurrentIndex(metS);
            if (i + 1 < Events.Length) { next = Events[i + 1]; return true; }
            next = new Crew2Event(0.0, "-");
            return false;
        }

        /// <summary>Seconds until the next scheduled event; NaN if none remain.</summary>
        public static double TimeToNext(double metS)
        {
            Crew2Event n;
            if (!Next(metS, out n)) return double.NaN;
            return n.TPlusS - metS;
        }

        /// <summary>
        /// How far our flight is from the real clock at a NAMED event: our MET at which we reached it
        /// minus the real T+. Positive = we are LATE, negative = EARLY. NaN if the name is unknown.
        /// The tuning target for "match the livestream" is to drive this toward zero.
        /// </summary>
        public static double SyncErrorS(string eventName, double ourMetAtEventS)
        {
            for (int i = 0; i < Events.Length; i++)
                if (Events[i].Name == eventName) return ourMetAtEventS - Events[i].TPlusS;
            return double.NaN;
        }
    }
}
