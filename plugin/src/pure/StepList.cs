/*
 * DragonScreen - StepList
 *
 * PURE. FLIGHT's step list: the countdown and ascent milestones, each either OBSERVED from vessel
 * state or ACKNOWLEDGED by the crew.
 *
 * ---- THERE IS NO LAUNCH STEP, AND THAT IS THE WHOLE POINT ----
 * Researched 2026-08-06. The countdown is run by the SpaceX LAUNCH DIRECTOR, who gives two explicit
 * gos - one to begin propellant load, one for liftoff. Once the hatch is shut the escape system
 * monitors the rocket autonomously, and during ascent SpaceX mission control calls the abort-mode
 * switches. The crew do checks beforehand and monitor throughout.
 *
 * So this list has no "LAUNCH" control on it. Putting one there would be inventing an authority the
 * crew does not have, on the one page whose job is to say what is actually happening.
 *
 * ---- TWO KINDS OF STEP, AND THE DIFFERENCE IS HONESTY ----
 * OBSERVED   driven by real vessel state. Crew aboard, propellant load, clamps, MECO, staging.
 * CREW       tapped to confirm. Comm check, seat rotation, suit leak check, hatch close.
 *
 * The CREW steps are the ones stock KSP cannot see - there is no hatch state, no suit, no intercom.
 * The project's rule is never to fake a reading, and a checklist item the crew ticks is not a faked
 * reading: on the real vehicle these ARE crew actions, performed and then reported. Making them
 * tappable is what they already are. Inferring them from something unrelated would have been the lie.
 *
 * ---- THE TIMES ARE A REFERENCE, NOT A SCHEDULE ----
 * T- figures are from NASA's Crew-10 launch-day milestones, converted against that mission's 7:03 pm
 * liftoff. They are shown so the list reads like the real one, and they are NOT used to advance
 * anything - a Kerbin mission will not match an ISS profile, and a step list driven by a clock rather
 * than by state would be confidently wrong the moment a countdown held.
 */
namespace DragonScreen
{
    public enum StepState : byte { Pending = 0, Active, Done, Skipped }

    /// <summary>Index into the crew-acknowledgement bitmask. Order must not be reshuffled.</summary>
    public enum StepId : byte
    {
        CrewAboard = 0,
        CommCheck,
        SeatRotation,
        SuitLeak,
        HatchClose,
        EscapeArmed,
        PropellantLoad,
        InternalPower,
        Liftoff,
        MaxQ,
        Meco,
        StageSep,
        Seco,
        DragonSep,
        NoseConeOpen,
        Count
    }

    public struct StepInputs
    {
        public bool Valid;
        public MissionPhase Phase;

        public int Crew;
        public bool Powered;
        /// <summary>Sitting on the pad, pre-release. The whole countdown group only applies here.</summary>
        public bool OnPad;
        /// <summary>Launch clamps still holding. Liftoff is their release, not a clock.</summary>
        public bool Clamped;
        public double Propellant01;

        public double RadarAltitude;
        public double VerticalSpeed;
        /// <summary>Latched by the glue once dynamic pressure has peaked and fallen away.</summary>
        public bool MaxQPassed;

        public bool BoosterAttached;
        public bool S2Attached;
        public bool BoosterLit;
        public bool S2Lit;
        public bool InSpace;
        public bool NoseConeOpen;

        /// <summary>Ours - the escape system arm state the panel owns.</summary>
        public bool EscapeArmed;

        /// <summary>Bit per StepId. Set by the crew tapping a CREW step.</summary>
        public int Acknowledged;
    }

    public struct StepRow
    {
        public StepId Id;
        public string Label;
        /// <summary>Reference time as the real timeline states it. Display only.</summary>
        public string TimeRef;
        public bool CrewStep;
        public StepState State;
    }

    public static class StepList
    {
        /// <summary>Every step, in order, with the real timeline's own reference times.</summary>
        private static readonly StepRow[] Template =
        {
            Row(StepId.CrewAboard,     "CREW ABOARD",           "T-2:35:00", false),
            Row(StepId.CommCheck,      "COMM CHECK",            "T-2:20:00", true),
            Row(StepId.SeatRotation,   "SEAT ROTATION",         "T-2:15:00", true),
            Row(StepId.SuitLeak,       "SUIT LEAK CHECK",       "T-2:14:00", true),
            Row(StepId.HatchClose,     "HATCH CLOSE",           "T-1:55:00", true),
            Row(StepId.EscapeArmed,    "ESCAPE SYSTEM ARMED",   "T-38:00",   false),
            Row(StepId.PropellantLoad, "PROPELLANT LOAD",       "T-35:00",   false),
            Row(StepId.InternalPower,  "DRAGON INTERNAL POWER", "T-1:00",    false),
            Row(StepId.Liftoff,        "LIFTOFF",               "T-0",       false),
            Row(StepId.MaxQ,           "MAX Q",                 "T+1:02",    false),
            Row(StepId.Meco,           "MECO",                  "T+2:33",    false),
            Row(StepId.StageSep,       "STAGE SEPARATION",      "T+2:36",    false),
            Row(StepId.Seco,           "SECO",                  "T+8:47",    false),
            Row(StepId.DragonSep,      "DRAGON SEPARATION",     "T+12:00",   false),
            Row(StepId.NoseConeOpen,   "NOSE CONE OPEN",        "T+13:00",   false),
        };

        private static StepRow Row(StepId id, string label, string t, bool crew)
        {
            StepRow r = new StepRow();
            r.Id = id; r.Label = label; r.TimeRef = t; r.CrewStep = crew;
            r.State = StepState.Pending;
            return r;
        }

        public static int Count { get { return Template.Length; } }

        public static bool IsAcknowledged(int mask, StepId id)
        {
            return (mask & (1 << (int)id)) != 0;
        }

        public static int Acknowledge(int mask, StepId id)
        {
            return mask | (1 << (int)id);
        }

        /// <summary>
        /// Fill <paramref name="into"/> with the current state of every step.
        ///
        /// Caller owns the array and reuses it - this is read in the draw path, so it allocates
        /// nothing. Returns the number of rows written.
        /// </summary>
        public static int Build(StepInputs s, StepRow[] into)
        {
            int n = Template.Length;
            if (into == null || into.Length < n) return 0;

            bool activeTaken = false;
            for (int i = 0; i < n; i++)
            {
                StepRow r = Template[i];
                bool done = Done(s, r.Id);

                if (done) r.State = StepState.Done;
                else if (!activeTaken) { r.State = StepState.Active; activeTaken = true; }
                else r.State = StepState.Pending;

                into[i] = r;
            }
            return n;
        }

        /// <summary>
        /// Has this step happened? OBSERVED steps read the vehicle; CREW steps read the tick.
        ///
        /// ---- ONCE FLYING, THE COUNTDOWN IS OVER ----
        /// Every prelaunch step reports done the moment the vehicle is off the pad, whether or not it
        /// was ticked. A checklist that still shows HATCH CLOSE outstanding at 40 km is not tracking
        /// the mission, and a crew member who skipped a tap should not be nagged for the rest of the
        /// flight by a list that has been overtaken by events.
        /// </summary>
        private static bool Done(StepInputs s, StepId id)
        {
            if (!s.Valid) return false;

            bool flying = !s.OnPad;

            switch (id)
            {
                // ---- countdown ----
                case StepId.CrewAboard:     return s.Crew > 0;
                case StepId.CommCheck:
                case StepId.SeatRotation:
                case StepId.SuitLeak:
                case StepId.HatchClose:
                    return flying || IsAcknowledged(s.Acknowledged, id);

                case StepId.EscapeArmed:    return flying || s.EscapeArmed;
                // Loaded is not "full" - a vehicle can fly a partial load, and ours did (97%).
                case StepId.PropellantLoad: return flying || s.Propellant01 > 0.95;
                case StepId.InternalPower:  return flying || s.Powered;

                // ---- ascent, all observed ----
                // Liftoff is the CLAMPS LETTING GO, not a clock and not an altitude. That is what
                // actually defines it, and it is the one moment a countdown can still be stopped.
                case StepId.Liftoff:        return flying || !s.Clamped;
                case StepId.MaxQ:           return s.MaxQPassed;
                case StepId.Meco:           return flying && s.BoosterAttached && !s.BoosterLit
                                                   && s.RadarAltitude > 1000.0
                                                   || !s.BoosterAttached;
                case StepId.StageSep:       return flying && !s.BoosterAttached;
                case StepId.Seco:           return s.InSpace && !s.S2Lit
                                                   && (!s.S2Attached || s.RadarAltitude > 100000.0);
                case StepId.DragonSep:      return flying && !s.S2Attached && !s.BoosterAttached;
                case StepId.NoseConeOpen:   return s.NoseConeOpen;
            }
            return false;
        }

        // ------------------------------------------------------------------ abort modes

        /// <summary>
        /// Which abort mode the vehicle is in.
        ///
        /// ⚠ THE STRUCTURE IS REAL, THE BOUNDARIES ARE OURS. Crew Dragon flies EIGHT abort modes -
        /// one on the pad and seven in flight - each with its own set of predetermined splashdown
        /// points, around fifty in total, and the ground calls the switches as the vehicle passes
        /// each boundary. That much is published.
        ///
        /// The actual boundary conditions are NOT public, and they are specific to a Florida launch
        /// and an ISS insertion in any case. So the count and the shape are the real vehicle's; where
        /// each one starts is ours, keyed to events this vehicle genuinely has. Do not cite these
        /// numbers as SpaceX's.
        /// </summary>
        public static string AbortMode(StepInputs s)
        {
            if (!s.Valid) return "-";
            if (!s.EscapeArmed) return "DISARMED";

            if (s.OnPad || s.Clamped) return "PAD ABORT";
            if (!s.BoosterAttached && !s.S2Attached) return "NONE - DRAGON FREE";

            if (s.BoosterAttached)
            {
                if (!s.MaxQPassed) return "MODE 1 - LOW ALT";
                if (s.BoosterLit) return "MODE 2 - HIGH ALT";
                return "MODE 3 - STAGING";
            }

            // Second stage. Split by how much of the job is left - below orbital energy an abort is a
            // downrange splashdown, above it the capsule can reach a low orbit on its own.
            if (s.RadarAltitude < 80000.0) return "MODE 4 - S2 EARLY";
            if (!s.InSpace) return "MODE 5 - S2 MID";
            if (s.S2Lit) return "MODE 6 - S2 LATE";
            return "MODE 7 - ORBIT CAPABLE";
        }
    }
}
