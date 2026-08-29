// DragonScreen — TEMPORARY autopilot stub (glue side)
// ============================================================================================
// The autopilot was DELETED for a ground-up rebuild (docs/AUTOPILOT_REBUILD_PLAN.md, 2026-08-26).
// The SCREENS are kept; they read autopilot state and trigger commands through the surface below.
// Every member here is an IDLE stand-in (reads report "not engaged", commands are no-ops) so the
// screen build stays green. As each real controller is rebuilt, DELETE its stub here and wire the
// screen to the real class. Nothing in this file flies anything.
// ============================================================================================
namespace DragonScreen
{
    // ---- crew-gate display types (ItemKind/ChecklistItem/Gate/ProcState) + the gate state machine, the
    // ---- gate catalog (CrewGates) and the conductor (ModeManager) are now REBUILT in pure/CrewGate.cs,
    // ---- pure/CrewGates.cs, pure/ModeManager.cs (L4). CrewProcedureOps below stays an idle glue stub
    // ---- until the KSP glue wires those pure pieces to live vessel state + the phase controllers. ----

    // ---- command dispatcher + systems flags the panel drives ----
    public static class FlightCommands
    {
        public static bool BackupPyros, EntryReboot, BackupEntry;
        public static bool EscapeArmed = true;
        public static SystemsState State = SystemsState.Fresh();
        public static double Charge01;
        public static bool CancelAllSequences() { return CrewProcedureOps.Engaged; }

        // ⭐ Campaign 3 (N1): the panel dispatcher — was a 4-command stub, so most dash buttons REFUSED-flashed even
        // though VehicleSystems already implements the power/string/fire handlers. Now wired. Each returns true =
        // white flash (actioned), false = red flash (honestly cannot). Ambiguous/dangerous commands with no verified
        // action (CutMains, FirePyro, SwapString, Execute) still return false ON PURPOSE — a red flash beats firing
        // the wrong pyro. State mutations go to FlightCommands.State, which the display reads (VesselData:288-289).
        public static bool Run(PanelCommand c)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            switch (c)
            {
                // ---- abort / rescue (already real) ----
                case PanelCommand.Abort:        FlightDriver.RequestAbort(); return true;
                case PanelCommand.Breakout:     FlightDriver.RequestAbort(); return true;   // prox-ops retreat — the responder is regime-aware
                // "DEPRESS RESPONSE": suppress the abort FX (klaxon/red lights → centre screen), AND isolate a leak if one is active.
                case PanelCommand.DepressResponse: FlightDriver.SuppressAbortFx(); Systems.DepressResponse(ref State); return true;
                case PanelCommand.DeorbitNow:   FlightDriver.RequestDeorbit(true);  return true;
                case PanelCommand.WaterDeorbit: FlightDriver.RequestDeorbit(false); return true;

                // ---- power buses / strings (real VehicleSystems handlers; State is what the screen reads) ----
                case PanelCommand.Power1: Systems.ToggleBus(ref State, 1); return true;
                case PanelCommand.Power2: Systems.ToggleBus(ref State, 2); return true;
                case PanelCommand.String1A: return Systems.ToggleString(ref State, 1, 0);
                case PanelCommand.String1B: return Systems.ToggleString(ref State, 1, 1);
                case PanelCommand.String1C: return Systems.ToggleString(ref State, 1, 2);
                case PanelCommand.String2A: return Systems.ToggleString(ref State, 2, 0);
                case PanelCommand.String2B: return Systems.ToggleString(ref State, 2, 1);
                case PanelCommand.String2C: return Systems.ToggleString(ref State, 2, 2);
                case PanelCommand.Reset1: return Systems.ResetBus(ref State, 1, Charge01);
                case PanelCommand.Reset2: return Systems.ResetBus(ref State, 2, Charge01);

                // ---- fire response (real; red if there is no fire / no suppressant to use) ----
                case PanelCommand.SuppressFire:  return Systems.SuppressFire(ref State);
                case PanelCommand.FireResponse:  return Systems.FireResponse(ref State);

                // ---- entry-mode arming flags (the lamps already read these — PanelButtons:314-317) ----
                case PanelCommand.EnableBackupPyros: BackupPyros = true;  return true;
                case PanelCommand.EnableEntryReboot: EntryReboot = true;  return true;
                case PanelCommand.EnableBackupEntry: BackupEntry = true;  return true;
                case PanelCommand.EnableNormalEntry: BackupEntry = false; return true;

                // ---- chutes + shroud (direct actuation; the abort/return path uses the same helpers) ----
                case PanelCommand.JettisonNoseCone: return Actuator.ToggleNoseShroud(v);   // relabelled TOGGLE SHROUD
                case PanelCommand.MainsOnly:        Actuator.DeployChutes(v, false); return true;
                case PanelCommand.DroguesAndMains:  Actuator.DeployChutes(v, true); Actuator.DeployChutes(v, false); return true;

                // ---- cancel the running AUTO SEQUENCE ----
                case PanelCommand.Cancel: if (CrewProcedureOps.Engaged) { CrewProcedureOps.Toggle(); return true; } return false;

                // ---- no VERIFIED action yet → honest red flash (do NOT guess a pyro/string) ----
                // CutMains (needs a RealChute-cut helper), FirePyro (which decoupler?), SwapString1/2/3 (bus unknown),
                // Execute (managed by the emergency interlock in PanelButtons).
                default: return false;
            }
        }
    }

    // CrewProcedureOps (the crew-in-the-loop gate conductor) is now REAL — see src/CrewProcedureOps.cs,
    // driving the pure ModeManager + CrewGate + CrewGates against the live vessel, hosted by
    // src/FlightDriver.cs. The stub is gone.

    // ---- mission command entry points (screen buttons) ----
    // Only UNDOCK is a manual control now (user 2026-08-28 — RENDEZVOUS + AUTO-DOCK removed; AUTO SEQUENCE
    // flies rendezvous + docking). UNDOCK just RELEASES THE HOOKS when the crew is ready, and marks that we
    // have docked this mission so pressing AUTO SEQUENCE again RESUMES at departure (careful KOS-safe backaway
    // → deorbit → entry → splashdown) instead of trying to dock again.
    public static class MissionOps
    {
        public static void Undock()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            bool released = Actuator.Undock(v);          // release the docking hooks (idempotent — no-op if not docked)
            CrewProcedureOps.MarkDockedThisMission();     // AUTO SEQUENCE now resumes at departure, not rendezvous
            UnityEngine.Debug.Log("[DragonScreen] UNDOCK pressed — hooks " + (released ? "released" : "already free")
                                  + "; press AUTO SEQUENCE to fly the return (it resumes at departure).");
        }
    }

    // ---- phase controllers: the screens read only "engaged" + a status note ----
    // ⭐ Campaign 3 (N2): were `return false` stubs → the STRING/mode lamps never lit. Now read the live conductor.
    public static class AutoPilot { public static bool Engaged { get { return CrewProcedureOps.Engaged; } } }

    public static class StationApproach
    {
        // outbound rendezvous = the Phasing fly-phase that is NOT the return (departure) leg.
        public static bool Engaged { get { return CrewProcedureOps.Engaged
            && CrewProcedureOps.ActivePhase == MissionPhase.Phasing && !CrewProcedureOps.IsReturn; } }
        public static string Note { get { return null; } }
    }

    public static class DockingOps
    {
        public static bool Engaged { get { return CrewProcedureOps.Engaged
            && (CrewProcedureOps.ActivePhase == MissionPhase.Approach
                || CrewProcedureOps.ActivePhase == MissionPhase.Docked); } }
        public static string Note { get { return null; } }
    }

    // The "DEORBIT" mode lamp lights while a deorbit is actually running — the DEORBIT NOW / WATER DEORBIT
    // rescue (or a fault deorbit-return abort). Real state from the abort executor, so the crew sees it engage.
    public static class DeorbitOps
    {
        public static bool Engaged { get { return FlightDriver.Aborting && AbortControl.Mode == AbortMode.DeorbitReturn; } }
    }

    public static class UndockOps
    {
        public static bool Engaged { get { return false; } }
        public static string Note { get { return null; } }
    }

    // ---- HullCams follows the booster during recovery ----  ⭐ Campaign 3 (N2): now the live recovery booster.
    public static class BoosterRecovery { public static Vessel Tracked { get { return MissionConductor.RecoveryBooster; } } }
}
