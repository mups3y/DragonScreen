// DragonScreen — autopilot-surface stub (glue side)
// ============================================================================================
// THE AUTOPILOT WAS DELETED 2026-09-01 (owner directive: keep ONLY the Dragon screens / the UI
// portion of the mod). The SCREENS are kept: they render, respond to touch, and display live vehicle
// telemetry read straight from KSP. What they can no longer do is FLY the vehicle — there is no
// flight software behind the command buttons any more.
//
// This file is the seam the screens compile against. Every controller/type the screen code still
// references is provided here as an IDLE stand-in: status reads report "not engaged / no fault", and
// the command buttons are no-ops that honestly refuse (click, no light, no action) rather than pretending to act.
// The genuinely display-only systems surface (power buses / strings / fire response — pure
// VehicleSystems, which only mutates on-screen state and flies nothing) stays REAL.
//
// Nothing in this file flies anything. If flight software is ever wanted again it replaces these
// stubs one controller at a time; until then the screens stand alone.
// ============================================================================================
using UnityEngine;

namespace DragonScreen
{
    // ---- idle stand-in types the screen code still names (were the crew-gate / FDIR data types) ----
    public enum ItemKind : byte { CrewAck, AutoCheck }
    public struct ChecklistItem { public string Label; public ItemKind Kind; }
    public struct Gate { public string Title; public ChecklistItem[] Items; }
    public struct ProcState { public GatePhase Phase; public bool[] Satisfied; }
    public struct FdirReport { public FaultKind Fault; public Recovery Response; }
    public enum AbortMode : byte { None, DeorbitReturn }

    // ---- the crew-in-the-loop conductor: never engaged, no gate ever active ----
    public static class CrewProcedureOps
    {
        public static bool Engaged { get { return false; } }
        public static bool IsReturn { get { return false; } }
        public static MissionPhase ActivePhase { get { return default(MissionPhase); } }
        public static string PhaseName { get { return null; } }
        public static ProcState Proc { get { return default(ProcState); } }
        public static bool CrewActionNeeded() { return false; }
        public static Gate CurrentGate() { return default(Gate); }
        public static void Toggle() { }
        public static void ToggleItem(int i) { }
        public static void PressGo() { }
        public static void PressNoGo() { }
        public static void PressAbort() { }
        public static void MarkDockedThisMission() { }
    }

    // ---- the (deleted) autopilot host: no abort in progress, no fault, idle authority ----
    public static class FlightDriver
    {
        public static bool Aborting { get { return false; } }
        public static bool AbortFxSuppressed { get { return false; } }
        public static ControlMode MissionMode { get { return ControlMode.Idle; } }
        public static FdirReport LastFdirReport { get { return default(FdirReport); } }
        public static void RequestAbort() { }
        public static void SuppressAbortFx() { }
        public static void RequestDeorbit(bool propulsive) { }
    }

    // ---- FDIR fault-name helper (display text). No autopilot → always nominal. ----
    public static class Fdir
    {
        public static string FaultName(FaultKind f) { return f == FaultKind.None ? "NOMINAL" : f.ToString(); }
    }

    // ---- abort executor: never in an abort mode ----
    public static class AbortControl
    {
        public static AbortMode Mode { get { return AbortMode.None; } }
    }

    // ---- mission orchestration: a display-only booster-recovery arm flag, no recovery booster ----
    public static class MissionConductor
    {
        public static bool AutoRecoverBooster;         // a screen toggle only — nothing acts on it now
        public static Vessel RecoveryBooster { get { return null; } }
    }

    // ---- direct part actuation is gone with the autopilot: the manual command buttons refuse honestly ----
    public static class Actuator
    {
        public static bool ToggleNoseShroud(Vessel v) { return false; }
        public static void DeployChutes(Vessel v, bool drogues) { }
        public static bool Undock(Vessel v) { return false; }
    }

    // ---- command dispatcher the panel drives. Flight commands are no-ops (click, no light, no action);
    // ---- the power/string/fire systems are REAL (pure VehicleSystems, display state only). ----
    public static class FlightCommands
    {
        public static bool BackupPyros, EntryReboot, BackupEntry;
        public static bool EscapeArmed = true;
        public static SystemsState State = SystemsState.Fresh();
        public static double Charge01;
        public static bool CancelAllSequences() { return false; }

        // true = actioned, false = honestly cannot (click, no light, no action). With no flight software,
        // everything that would COMMAND the vehicle returns false; only the display-state systems act.
        public static bool Run(PanelCommand c)
        {
            switch (c)
            {
                // ---- power buses / strings (REAL — VehicleSystems mutates display state, flies nothing) ----
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

                // ---- fire response (REAL — display state) ----
                case PanelCommand.SuppressFire:  return Systems.SuppressFire(ref State);
                case PanelCommand.FireResponse:  return Systems.FireResponse(ref State);
                // ---- suppress the abort FX + isolate a leak (the leak isolate is real display state) ----
                case PanelCommand.DepressResponse: Systems.DepressResponse(ref State); return true;

                // ---- entry-mode arming lamps (display flags the lamps read) ----
                case PanelCommand.EnableBackupPyros: BackupPyros = true;  return true;
                case PanelCommand.EnableEntryReboot: EntryReboot = true;  return true;
                case PanelCommand.EnableBackupEntry: BackupEntry = true;  return true;
                case PanelCommand.EnableNormalEntry: BackupEntry = false; return true;

                // ---- everything that would FLY / actuate the vehicle: no flight software → click, no light, no action ----
                // Abort, Breakout, DeorbitNow, WaterDeorbit, chutes/shroud, Cancel, and the unverified commands.
                default: return false;
            }
        }
    }

    // ---- UNDOCK button: the actuation layer is gone, so it can no longer release the hooks ----
    public static class MissionOps
    {
        public static void Undock()
        {
            Debug.Log("[DragonScreen] UNDOCK pressed — no flight/actuation software installed (screens-only build).");
        }
    }

    // ---- phase / mode lamps the screens read: nothing is ever engaged ----
    public static class AutoPilot { public static bool Engaged { get { return false; } } }
    public static class StationApproach { public static bool Engaged { get { return false; } } public static string Note { get { return null; } } }
    public static class DockingOps { public static bool Engaged { get { return false; } } public static string Note { get { return null; } } }
    public static class DeorbitOps { public static bool Engaged { get { return false; } } }
    public static class UndockOps { public static bool Engaged { get { return false; } } public static string Note { get { return null; } } }

    // ---- HullCams had followed the recovery booster; there is no recovery booster now ----
    public static class BoosterRecovery { public static Vessel Tracked { get { return null; } } }
}
