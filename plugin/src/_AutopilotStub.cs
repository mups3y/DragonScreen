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
        public static bool CancelAllSequences() { return false; }
        public static bool Run(PanelCommand c)
        {
            // The physical EJECT/abort handle is wired to the real abort (SuperDraco + chutes + the DON'T
            // PANIC alert). The other panel commands are still idle stubs until their controllers are rebuilt.
            if (c == PanelCommand.Abort) { FlightDriver.RequestAbort(); return true; }
            // "DEPRESS RESPONSE" is the abort-alarm SUPPRESS RESPONSE: silences the klaxon + red cabin
            // lights and drops the alert to the centre screen only. It does NOT cancel the abort.
            if (c == PanelCommand.DepressResponse) { FlightDriver.SuppressAbortFx(); return true; }
            return false;
        }
    }

    // CrewProcedureOps (the crew-in-the-loop gate conductor) is now REAL — see src/CrewProcedureOps.cs,
    // driving the pure ModeManager + CrewGate + CrewGates against the live vessel, hosted by
    // src/FlightDriver.cs. The stub is gone.

    // ---- mission command entry points (screen buttons) ----
    public static class MissionOps
    {
        public static void Rendezvous() { }
        public static void AutoDock() { }
        public static void UndockAndLand() { }
    }

    // ---- phase controllers: the screens read only "engaged" + a status note ----
    public static class AutoPilot { public static bool Engaged { get { return false; } } }

    public static class StationApproach
    {
        public static bool Engaged { get { return false; } }
        public static string Note { get { return null; } }
    }

    public static class DockingOps
    {
        public static bool Engaged { get { return false; } }
        public static string Note { get { return null; } }
    }

    public static class DeorbitOps { public static bool Engaged { get { return false; } } }

    public static class UndockOps
    {
        public static bool Engaged { get { return false; } }
        public static string Note { get { return null; } }
    }

    // ---- HullCams follows the booster during recovery; nothing tracked while the autopilot is idle ----
    public static class BoosterRecovery { public static Vessel Tracked { get { return null; } } }
}
