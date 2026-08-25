/*
 * DragonScreen - MissionPhase
 *
 * PURE. The Crew Dragon mission phases, and a classifier that works out which one we are in from
 * observable vessel state.
 *
 * ---- THE PHASES ARE THE REAL VEHICLE'S, NOT KSP'S ----
 * ACTIVE PHASE used to show `Vessel.Situations` - ORBITING, PRELAUNCH, SUB_ORBITAL. Those are KSP
 * concepts. A screen meant to look and act like the real thing shows the MISSION phase, and the real
 * sequence is (docs/FLIGHT_SYSTEMS.md, from NASA and SpaceX sources):
 *
 *     LAUNCH -> ASCENT -> SECO -> S2 SEP -> NOSECONE OPEN -> PHASING -> APPROACH -> DOCKED
 *     -> UNDOCK -> DEPARTURE -> NOSECONE CLOSE -> TRUNK SEP -> DEORBIT BURN -> ENTRY
 *     -> DROGUES -> MAINS -> SPLASHDOWN
 *
 * ---- WHAT THIS CLASSIFIER CAN AND CANNOT KNOW ----
 * Some phases are visible in vessel state: on the pad, climbing, in space, docked, under chutes, wet.
 * Others are INTENTIONS - "departure" and "deorbit burn" are only distinguishable from "coast" by
 * knowing what the flight plan is doing. Those belong to the flight sequencer - the crew-in-the-loop
 * conductor (CrewProcedureOps + pure/CrewProcedure + pure/CrewGates). This classifier stays the
 * OBSERVABLE fallback for a vessel nobody is conducting; the conductor knows the intent.
 *
 * So this returns what is OBSERVABLE and returns `Coast` rather than guessing when it cannot tell.
 * Once the sequencer exists it will OVERRIDE this with the phase it is actually flying, and this stays
 * as the fallback for a vessel nobody is flying automatically. **Do not extend this with heuristics
 * that pretend to know intent** - a confidently wrong phase is the same class of bug as a confidently
 * wrong velocity.
 */
using System;

namespace DragonScreen
{
    public enum MissionPhase : byte
    {
        Unknown = 0,
        Prelaunch,
        Ascent,
        Coast,
        Phasing,
        Approach,
        Docked,
        Entry,
        Drogues,
        Mains,
        Splashdown,
        Landed
    }

    /// <summary>
    /// Observable state the classifier reads. Plain numbers - the glue fills it from KSP.
    /// </summary>
    public struct MissionInputs
    {
        public FlightRegime Regime;
        /// <summary>Radar altitude, metres. Height above the surface, not above sea level.</summary>
        public double RadarAltitude;
        /// <summary>Positive up, m/s.</summary>
        public double VerticalSpeed;
        public bool Docked;
        public bool Splashed;
        /// <summary>A target is selected - the only way to tell rendezvous from ordinary coast.</summary>
        public bool HasTarget;
        /// <summary>Metres to the target. Only meaningful when HasTarget.</summary>
        public double TargetRange;
        public bool DroguesOut;
        public bool MainsOut;
    }

    public static class Mission
    {
        // ---- REAL NUMBERS, from NASA/SpaceX sources. See docs/FLIGHT_SYSTEMS.md. ----
        /// <summary>Drogue deploy: 18 000 ft, at roughly 350 mph.</summary>
        public const double DrogueAltitude = 5486.0;
        /// <summary>Main deploy: 6 000 ft, at roughly 119 mph.</summary>
        public const double MainAltitude = 1830.0;

        /// <summary>
        /// Rendezvous / approach boundary, metres.
        ///
        /// 3 km is this project's own recorded approach law, not a round number picked here: outside
        /// it you fly a PHASING orbit, from 3 km to 0.5 km you fly Clohessy-Wiltshire, below that it is
        /// RCS. That law exists because pursuit steering - chasing a co-orbital target - de-orbited
        /// flight 012.
        /// </summary>
        public const double ApproachRange = 3000.0;

        public static MissionPhase Classify(MissionInputs s)
        {
            // Order matters: the most specific and most safety-critical states win. Chutes before
            // anything altitude-based, because a screen that says "ENTRY" while the mains are out is
            // worse than useless.
            if (s.Splashed) return MissionPhase.Splashdown;
            if (s.MainsOut) return MissionPhase.Mains;
            if (s.DroguesOut) return MissionPhase.Drogues;
            if (s.Docked) return MissionPhase.Docked;

            if (s.Regime == FlightRegime.Ground)
                return (s.VerticalSpeed > 1.0) ? MissionPhase.Ascent : MissionPhase.Prelaunch;

            if (s.Regime == FlightRegime.Atmosphere)
            {
                // Climbing is ascent; falling in atmosphere is entry. There is no third option for
                // this vehicle - it does not fly.
                if (s.VerticalSpeed > 0.0) return MissionPhase.Ascent;
                return MissionPhase.Entry;
            }

            // In space. A target is the only observable that separates rendezvous from coasting.
            if (s.HasTarget)
                return (s.TargetRange <= ApproachRange) ? MissionPhase.Approach : MissionPhase.Phasing;

            return MissionPhase.Coast;
        }

        /// <summary>
        /// Display name. Fixed strings, never built per frame - this is read in the draw path.
        /// </summary>
        public static string Name(MissionPhase p)
        {
            switch (p)
            {
                case MissionPhase.Prelaunch:  return "PRELAUNCH";
                case MissionPhase.Ascent:     return "ASCENT";
                case MissionPhase.Coast:      return "ORBIT COAST";
                case MissionPhase.Phasing:    return "PHASING";
                case MissionPhase.Approach:   return "APPROACH";
                case MissionPhase.Docked:     return "DOCKED";
                case MissionPhase.Entry:      return "ENTRY";
                case MissionPhase.Drogues:    return "DROGUES";
                case MissionPhase.Mains:      return "MAINS";
                case MissionPhase.Splashdown: return "SPLASHDOWN";
                case MissionPhase.Landed:     return "LANDED";
                default:                      return "-";
            }
        }
    }
}
