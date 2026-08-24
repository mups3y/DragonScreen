/*
 * DragonScreen - AutoSequenceCore
 *
 * PURE. The mission conductor's decision logic: given the observable mission state, which sequence STEP
 * are we in, and is the sequence finished. No KSP, no engaging - the glue (AutoSequence.cs) reads the
 * vessel into SeqInputs, calls this, and carries out the step by engaging the matching controller.
 *
 * ---- WHY A CONDUCTOR, AND WHY THE LOGIC IS PURE ----
 * Every phase already flies itself (AutoPilot ascent, StationApproach->DockingOps, DockedRefuel,
 * DeorbitOps->EntryOps->ChuteGuard). What never existed is the thing that CHAINS them - pure/MissionPhase
 * says so in as many words: "the flight sequencer ... does not exist yet." This is it. Keeping the
 * transition MAP pure means a headless test can drive a whole mission through it with a struct and assert
 * every hand-off, which is the coverage a one-button mission needs before it ever flies.
 *
 * ---- THE TWO LEGS, ONE BUTTON ----
 * OUTBOUND: Ascent -> Rendezvous (hands to dock on its own) -> Refuel -> done, latching ReturnArmed.
 * RETURN  : Deorbit (hands to entry + chutes on its own) -> done.
 * Begin() picks the leg from state. ReturnArmed - set when an outbound run finishes - is what tells a
 * loose-in-orbit capsule "you have already flown out and docked, so this press is the ride home", so the
 * same button means Rendezvous the first time and Deorbit after the crew has undocked.
 *
 * ---- WHAT "DONE" MEANS, AND BAIL ----
 * A step ends either because its done-signal arrived (Docked, RefuelFull, Landed) or because the
 * controller it was waiting on is no longer engaged - the crew cancelled it, or an engage refused. The
 * second case is a BAIL: the conductor hands straight back to manual rather than sitting lit and idle.
 * The glue DEBOUNCES a bail (a controller hand-off, e.g. rendezvous->dock, can show neither engaged for a
 * frame), so this stays a pure, deterministic map and the timing lives in the glue.
 */
namespace DragonScreen
{
    /// <summary>The step the conductor dwells in. The step implies the leg; there is no separate mode.</summary>
    public enum SeqStep : byte
    {
        Idle = 0,
        // ---- OUTBOUND ----
        Ascent,        // AutoPilot flies launch -> insertion
        Rendezvous,    // StationApproach closes and hands to DockingOps
        Refuel,        // docked; DockedRefuel tops the capsule tank
        // ---- RETURN ----
        Deorbit        // DeorbitOps -> EntryOps -> chutes -> splashdown
    }

    /// <summary>Observable mission state. Plain flags - the glue fills it from KSP each tick.</summary>
    public struct SeqInputs
    {
        /// <summary>A stable orbit exists (periapsis above the atmosphere).</summary>
        public bool InStableOrbit;
        /// <summary>The second stage is gone - i.e. the capsule has separated to fly on its own.</summary>
        public bool S2Gone;
        /// <summary>Hard-docked to the station (merged-vessel aware - see DockedSide.Docked).</summary>
        public bool Docked;
        /// <summary>The CAPSULE tank is full - Dragon side only, not the station's (Refuel.Full).</summary>
        public bool RefuelFull;
        /// <summary>Refuel has stopped making progress (station empty), so waiting for full would hang.</summary>
        public bool RefuelStalled;
        /// <summary>On the surface or the water - the return is complete.</summary>
        public bool Landed;

        /// <summary>The ascent autopilot is engaged.</summary>
        public bool AscentEngaged;
        /// <summary>The rendezvous OR the docking controller is engaged (either owns the middle leg).</summary>
        public bool RendezvousEngaged;
        /// <summary>The deorbit OR the entry controller is engaged (either owns the return leg).</summary>
        public bool ReturnActive;

        /// <summary>Latched by the glue when an outbound run completes; selects the return leg on Begin.</summary>
        public bool ReturnArmed;
    }

    /// <summary>What the conductor should be doing now.</summary>
    public struct SeqResult
    {
        public SeqStep Step;
        /// <summary>The sequence has finished - the glue disengages the conductor.</summary>
        public bool Done;
        /// <summary>Finished because a phase dropped out, not by reaching its goal (glue debounces + logs).</summary>
        public bool Bail;
        /// <summary>Latch ReturnArmed - an outbound run just completed.</summary>
        public bool ArmReturn;
    }

    public static class AutoSequenceCore
    {
        /// <summary>Which step to start in when the conductor is first engaged, from the current state.</summary>
        public static SeqStep Begin(SeqInputs s)
        {
            // Ride home only when we have already flown out and are loose in orbit again.
            if (s.ReturnArmed && s.InStableOrbit && !s.Docked) return SeqStep.Deorbit;
            // Otherwise it is an outbound run: pick up wherever the vehicle already is.
            if (s.Docked) return SeqStep.Refuel;
            if (s.InStableOrbit && s.S2Gone) return SeqStep.Rendezvous;
            return SeqStep.Ascent;
        }

        /// <summary>
        /// Advance from the current step given the state. Returns the step we should now be in and whether
        /// the sequence is finished. A step whose controller is no longer engaged (and whose goal has not
        /// been reached) BAILS - hand back to manual.
        /// </summary>
        public static SeqResult Advance(SeqStep step, SeqInputs s)
        {
            switch (step)
            {
                case SeqStep.Ascent:
                    if (s.InStableOrbit && s.S2Gone) return At(SeqStep.Rendezvous);   // insertion done
                    if (!s.AscentEngaged) return Bail();                              // cancelled before orbit
                    return At(SeqStep.Ascent);

                case SeqStep.Rendezvous:
                    if (s.Docked) return At(SeqStep.Refuel);                          // berthed
                    if (!s.RendezvousEngaged) return Bail();                          // cancelled / refused
                    return At(SeqStep.Rendezvous);

                case SeqStep.Refuel:
                    if (!s.Docked) return Bail();                                     // fell off the port
                    // ⛔ NO ISS REFUEL (user 2026-08-24): real Crew-2 flies the whole mission on its launch
                    // propellant - the ISS carries no MMH/NTO and never transfers any to Dragon. So DOCKED
                    // = outbound complete; go idle at once for the crew, no propellant wait. The Dragon
                    // returns on what it launched with (conserved - see the SuperDraco/Draco fidelity work).
                    return DoneArm();

                case SeqStep.Deorbit:
                    if (s.Landed) return Done();                                      // home
                    if (!s.ReturnActive) return Bail();                              // cancelled before entry
                    return At(SeqStep.Deorbit);

                default:
                    return Done();
            }
        }

        /// <summary>Button label for a step. Fixed strings - read in the draw path.</summary>
        public static string StepName(SeqStep step)
        {
            switch (step)
            {
                case SeqStep.Ascent:     return "ASCENT";
                case SeqStep.Rendezvous: return "RENDEZVOUS";
                case SeqStep.Refuel:     return "REFUEL";
                case SeqStep.Deorbit:    return "RETURN";
                default:                 return "";
            }
        }

        // ---- result builders ----
        private static SeqResult At(SeqStep st) { SeqResult r; r.Step = st; r.Done = false; r.Bail = false; r.ArmReturn = false; return r; }
        private static SeqResult Bail()         { SeqResult r; r.Step = SeqStep.Idle; r.Done = true;  r.Bail = true;  r.ArmReturn = false; return r; }
        private static SeqResult Done()         { SeqResult r; r.Step = SeqStep.Idle; r.Done = true;  r.Bail = false; r.ArmReturn = false; return r; }
        private static SeqResult DoneArm()      { SeqResult r; r.Step = SeqStep.Idle; r.Done = true;  r.Bail = false; r.ArmReturn = true;  return r; }
    }
}
