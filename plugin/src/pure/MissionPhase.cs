// DragonScreen - MissionPhase
// ---- THE PHASES ARE THE REAL VEHICLE'S, NOT KSP'S ----
// ---- WHAT THIS CLASSIFIER CAN AND CANNOT KNOW ----
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

    // ---- Crew-gate display state. The screen (GateCard) renders it; the rebuilt crew-procedure
    // ---- autopilot will drive it. Lives in the screen layer so the screens compile independently.
    public enum GatePhase : byte
    {
        Holding,
        GoReady,
        Go,
        NoGo,
        Abort
    }

    public struct MissionInputs
    {
        public FlightRegime Regime;
        public double RadarAltitude;
        public double VerticalSpeed;
        public bool Docked;
        public bool Splashed;
        public bool HasTarget;
        public double TargetRange;
        public bool DroguesOut;
        public bool MainsOut;
        // ⭐ U1: is the orbit CLOSED (periapsis above the atmosphere = self-sustaining)? During the S2 insertion
        // burn the vehicle is already in the Space regime with the ISS targeted, but pe is still below the
        // atmosphere (sub-orbital) — that is ASCENT, not phasing. Only a closed orbit begins the outbound phasing.
        public bool OrbitClosed;
    }

    public static class Mission
    {
        // ---- REAL NUMBERS, from NASA/SpaceX sources. See docs/BUILD_PLAN.md §8. ----
        public const double DrogueAltitude = 5486.0;
        public const double MainAltitude = 1830.0;

        public const double ApproachRange = 3000.0;

        public static MissionPhase Classify(MissionInputs s)
        {
            if (s.Splashed) return MissionPhase.Splashdown;
            if (s.MainsOut) return MissionPhase.Mains;
            if (s.DroguesOut) return MissionPhase.Drogues;
            if (s.Docked) return MissionPhase.Docked;

            if (s.Regime == FlightRegime.Ground)
                return (s.VerticalSpeed > 1.0) ? MissionPhase.Ascent : MissionPhase.Prelaunch;

            if (s.Regime == FlightRegime.Atmosphere)
            {
                if (s.VerticalSpeed > 0.0) return MissionPhase.Ascent;
                return MissionPhase.Entry;
            }

            if (s.HasTarget)
            {
                // ⭐ U1: a set target in space is NOT phasing until the orbit is CLOSED — during the S2 insertion
                // the vehicle is above the atmosphere with the ISS targeted but pe is still sub-orbital (SECO not
                // reached). Show ASCENT until orbit is achieved, then phasing/approach by range. (The return leg
                // is undocked → HasTarget is false → this gate never mislabels a deorbit descent.)
                if (!s.OrbitClosed) return MissionPhase.Ascent;
                return (s.TargetRange <= ApproachRange) ? MissionPhase.Approach : MissionPhase.Phasing;
            }

            return MissionPhase.Coast;
        }

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

        // ⛔ ONE AUTHORITATIVE PHASE (rule T4). While the autopilot is ENGAGED and flying a KNOWN phase, the
        // mission FSM's ActivePhase IS the phase — the display shows it, never the independent Classify() shadow,
        // so the screen and the autopilot can never disagree about where the mission is. When disengaged
        // (manual/idle) or between phases (at a gate, ActivePhase == Unknown), the live classifier is the honest
        // fallback. This is the single-source-of-truth resolver the display consumes (VesselData).
        public static MissionPhase AuthoritativePhase(bool engaged, MissionPhase active, MissionPhase classified)
        {
            return (engaged && active != MissionPhase.Unknown) ? active : classified;
        }
    }
}
