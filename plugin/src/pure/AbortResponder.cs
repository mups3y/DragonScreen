// DragonScreen — AbortResponder  (autopilot rebuild L5 FDIR: the phase-correct abort action)
// ============================================================================================
// When the mode manager / FDIR / crew commands ABORT, the RIGHT response depends on where in the mission
// it happens (docs/TRUE_AUTOPILOT_ARCHITECTURE.md §9/§11 — abort-to-home is the guaranteed floor):
//   • PAD / EARLY ASCENT → LAUNCH ESCAPE: fire the SuperDracos (the pod's abort config, craftdump:
//     TE.18.DRAGONV2.POD ModuleEngineConfigs configuration=SuperDraco; the stock Abort action group),
//     separate the capsule from the stack, coast, then the chutes (pure/Chutes.cs) bring it down. Requires
//     the launch-escape system ARMED (crew gate G5); without it the pad has no escape → safe-hold.
//   • ON-ORBIT PROX-OPS (phasing/approach) → KOS RETREAT: back out along the approach to a safe standoff
//     (the real "any unplanned keep-out breach commands an abort/retreat" rule; DockApproach flies it).
//     The SuperDracos are a LAUNCH escape system — an orbital prox-ops abort is a retreat, not an escape.
//   • DOCKED → SAFE-HOLD: hold attitude, station-keep, wait for the crew.
//   • ENTRY / UNDER CHUTES → RIDE IT DOWN: past the point of escape; ensure the chutes deploy (backstop).
//
// ⛔ FULL CONTROL: Respond() always yields a definite command; HoldAttitude tells L2 to hold the last safe
// attitude so nothing floats. PURE decision; the glue fires the action group / separates.
// ============================================================================================
namespace DragonScreen
{
    public enum AbortMode : byte { None, LaunchEscape, KosRetreat, SafeHold, RideItDown }

    public struct AbortInputs
    {
        public bool Triggered;         // an abort has been commanded
        public MissionPhase Phase;
        public bool LesArmed;          // launch-escape system armed (crew gate G5)
    }

    public struct AbortCommand
    {
        public AbortMode Mode;
        public bool FireSuperDracos;   // launch escape only
        public bool Separate;          // cut the capsule free of the stack
        public bool DeployChutes;      // ensure chutes (escape descent / ride-it-down backstop)
        public bool Retreat;           // back out of the corridor (prox-ops)
        public bool HoldSafe;          // station-keep / safe attitude hold
        public bool HoldAttitude;      // L2 holds the last safe attitude (never float)
        public string Note;
    }

    public static class AbortResponder
    {
        public static AbortCommand Respond(AbortInputs s)
        {
            AbortCommand c = new AbortCommand();
            c.HoldAttitude = true;                     // full control: never float, whatever the mode
            if (!s.Triggered) { c.Mode = AbortMode.None; return c; }

            switch (s.Phase)
            {
                case MissionPhase.Prelaunch:
                case MissionPhase.Ascent:
                    if (s.LesArmed)
                    {
                        c.Mode = AbortMode.LaunchEscape;
                        c.FireSuperDracos = true; c.Separate = true; c.DeployChutes = true;
                        c.Note = "LAUNCH ESCAPE — SuperDracos, separate, chutes";
                    }
                    else
                    {
                        c.Mode = AbortMode.SafeHold;        // no armed escape → hold safe
                        c.HoldSafe = true;
                        c.Note = "ABORT — LES not armed; safing";
                    }
                    break;

                case MissionPhase.Coast:
                case MissionPhase.Phasing:
                case MissionPhase.Approach:
                    c.Mode = AbortMode.KosRetreat;
                    c.Retreat = true; c.HoldSafe = true;
                    c.Note = "PROX-OPS ABORT — retreat out of the corridor to a safe standoff";
                    break;

                case MissionPhase.Docked:
                    c.Mode = AbortMode.SafeHold;
                    c.HoldSafe = true;
                    c.Note = "SAFE-HOLD — hold attitude, await crew";
                    break;

                case MissionPhase.Entry:
                case MissionPhase.Drogues:
                case MissionPhase.Mains:
                case MissionPhase.Splashdown:
                    c.Mode = AbortMode.RideItDown;
                    c.DeployChutes = true;                  // past escape: ensure the chutes
                    c.Note = "RIDE IT DOWN — past the escape point; chute backstop armed";
                    break;

                default:
                    c.Mode = AbortMode.SafeHold;
                    c.HoldSafe = true;
                    c.Note = "SAFE-HOLD";
                    break;
            }
            return c;
        }
    }
}
