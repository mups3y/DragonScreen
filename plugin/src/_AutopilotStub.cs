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
    // ---- ⛔ FOUR CREW-GATE DISPLAY TYPES MOVED OUT (W4 / Wave D, 2026-09-04). ----
    // `ItemKind`, `ChecklistItem`, `Gate` and `ProcState` used to be declared here as idle stand-ins. They are
    // now the AUTHORITATIVE ones in `pure/CrewGate.cs`, restored from `8b81816^` — which is where they were
    // before the demolition, and which that file's own header says in as many words. Do NOT re-declare them
    // here; two declarations of the same type break the build (§B12.8's two-generation rule).
    // ⚠ The screens' reads are unchanged: `g.Title`, `g.Items[i].Label`, `g.Items[i].Kind == ItemKind.CrewAck`,
    // `pr.Phase`, `pr.Satisfied` (VesselData.cs:361-371) all exist on the restored types with the same shapes.
    // `Gate` gains an `Id` (`GateId`) and `ChecklistItem` gains `Crew()`/`Sys()` factories — additions only.
    // The one RENAME is `ItemKind.AutoCheck` → `ItemKind.Auto`; nothing in the tree read that member (grep,
    // 2026-09-04), so no screen file changed. `GatePhase` stays in `pure/MissionPhase.cs` where it already was.
    //
    // ---- idle stand-in types the screen code still names (the FDIR / abort data types) ----
    public struct FdirReport { public FaultKind Fault; public Recovery Response; }
    public enum AbortMode : byte { None, DeorbitReturn }

    // ---- the crew-in-the-loop conductor: never engaged, no gate ever active ----
    // ⚠ W4 (Wave D) RESTORED THE PURE HALF UNDER THIS STUB, AND DELIBERATELY LEFT THE STUB IN PLACE.
    // `pure/ModeManager.cs` (the plan), `pure/CrewGate.cs` (the gate machine), `pure/CrewGates.cs` (the G1..G15
    // catalog) and `pure/MissionProfile.cs` (mission-as-data) are all back and headless-tested. The GLUE that
    // drives them — `src/CrewProcedureOps.cs`, 20,016 B at `8b81816^` — is NOT restored, and the reason is
    // §14.4(a), not difficulty: its whole state machine advances inside `Tick(Vessel)`, and the ONLY caller of
    // `Tick` in the entire pre-deletion tree is `FlightDriver.cs:341` (checked, not assumed). FlightDriver is
    // R1 §5.2's "the Part-B host", it is in NO §B12.8 wave, and it is not in this tree. Landing the real
    // CrewProcedureOps without it would make the AUTO SEQUENCE button (`ScreenPainter.cs:739`) ENGAGE a
    // conductor that can never tick: a gate card would appear, its items would tick, and the crew's GO press
    // would latch into `goPressed` and be consumed by nobody — a lit button and a dead GO. That is strictly
    // WORSE than the honest no-op §14.4(a) requires (click, no light, no action), so it waits for its host.
    // ⇒ Register **W10** lands `src/CrewProcedureOps.cs` together with a `FlightDriver` that ticks it.
    // ⛔ Do NOT "fix" this by ticking CrewProcedureOps from a screen addon: its `AutoAdvanceGates` default is
    // `true`, so a tick with no flight software behind it auto-clears the whole countdown and parks the plan on
    // the Ascent Fly step, which nothing completes — the screens would then report a mission phase that is not
    // happening. A conductor with no controllers must not be ticked at all.
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

        // ---- W2 (Wave B) seam: the throttle-authority entry point the REAL Actuator calls ----
        // `Actuator.FireAbort` owns the throttle (SuperDracos fire at full), so restoring the actuation layer
        // needs this member to exist. The real FlightDriver (`8b81816^`) latches a commanded throttle and its
        // OnFlyByWire hook applies it; that hook is Wave D's, so here it stays an HONEST NO-OP — §14.4(a):
        // nothing is throttled, and nothing pretends to be. Do NOT make this write to a vessel: the facade
        // gets its real body when Wave D restores FlightDriver, not by half-wiring it from here (§B12.8(a)).
        public static void SetThrottle(double t) { }
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
    // ⚠ W4 (Wave D) RESTORED THIS ONE'S PURE HALF TOO, AND COULD NOT RESTORE THE GLUE. `pure/WarpPlan.cs`
    // (the never-overshoot warp rule) and `pure/CoastEta.cs` (a coast's ETA → the warp target UT) are back and
    // headless-tested. `src/MissionConductor.cs` (24,299 B at `8b81816^`) is NOT, because it does not COMPILE
    // in this tree, on two counts that are both settled decisions rather than missing effort:
    //   1. its booster-recovery FSM calls `BoosterControl.Reset()` / `.IsRecoverableBooster()` /
    //      `.DriveNonActive()` — and CLAUDE.md is explicit that *"the deleted `BoosterControl` implementation
    //      still stays deleted"* (R1 §5.2 files it RECOVER-REFERENCE; §B16.1 writes the booster core FRESH,
    //      on its own vessel). Restoring MissionConductor as-is would drag it back in through the side door.
    //   2. its burn-guard reads `FlightDriver.CmdTransX/Y/Z`, which the stub FlightDriver below does not have
    //      and must not gain — half-wiring a facade from here is what §B12.8(a) forbids.
    // Cutting the recovery half out to make it build is exactly the "quiet deletion inside another task's
    // diff" §B12.8 rider (b) bans, so it was not done. ⇒ Register **W9**.
    public static class MissionConductor
    {
        public static bool AutoRecoverBooster;         // a screen toggle only — nothing acts on it now
        public static Vessel RecoveryBooster { get { return null; } }
    }

    // ---- ⛔ THE Actuator STUB IS RETIRED (W2 / Wave B, 2026-09-04). ----
    // It used to declare a no-op `Actuator` with three refusing methods (ToggleNoseShroud / DeployChutes /
    // Undock). The REAL one is back — `src/Actuator.cs`, restored from `8b81816^` per §B12.8 Wave B — and it
    // carries all three with identical signatures, so this is the first genuine facade swap of §B12.5: the
    // class name the screens compile against did not change, only what stands behind it. Do NOT re-add a stub
    // Actuator here; two declarations of the same type break the build (§B12.8's two-generation rule).
    // ⚠ Nothing on any screen calls Actuator today (verified by grep, 2026-09-04), so no screen behaviour
    // changed with the swap — the callers arrive with Waves C/D.

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
                // S53 / H42: RETURN the model's answer, do not discard it. This case used to throw the
                // bool away and `return true`, so the lamp flashed "acted" even when the model refused
                // for want of a leak - §14.4(a)'s click-no-light-no-action, inverted. Its two
                // plate-siblings beside it always returned theirs; this one is now consistent with them.
                case PanelCommand.DepressResponse: return Systems.DepressResponse(ref State);

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

    // ================== THE GEN-1 DISPLAY FACADE (§B12.8(a) / §B12.5 / R1 Q4) ==================
    // These six names are the CONTRACT THE SCREENS COMPILE AGAINST. They are GEN-1 names; the gen-2
    // controllers that do those jobs are called something else, and §B12.8(a)'s resolution is that the gen-2
    // controllers REGISTER INTO THESE NAMES rather than the names moving to match a controller. So: never
    // rename one of these to match whatever lands behind it, never add a parallel surface beside it, and
    // never delete one — each increment flips exactly ONE of these from constant-false to live (§B12.5).
    //
    // ---- STATUS, ONE LINE EACH — the standing rule is "backed by a real controller OR still a no-op with a
    // ---- STATED REASON; never a silent gap". THE PROCESS THAT TURNS ONE OF THESE LIVE IS WRITTEN DOWN ONCE,
    // ---- IN §B12.5a — which increment owns each name, the five steps it follows, this comment convention,
    // ---- and the four things it must never do. Read that section before touching anything below.
    //
    // ---- ⚠ REWRITTEN BY G6, 2026-09-04 — the two previous answers here were BOTH wrong, and the second was
    // ---- wrong in a way only the register could reveal. W4 wrote "the backing controller is in NO §B12.8
    // ---- wave"; W11 then gave four of these names to the Wave E lines W18/W20/W21. The OWNER's
    // ---- upper-stage/booster decision of 2026-09-04 ("we use MechJeb for ALL UPPER STAGE MANOEUVRES as
    // ---- planned. BOOSTER SCRIPTED.") re-verdicted those three lines RECOVER-REFERENCE — they are READS
    // ---- now, they restore no code, and they will never flip a property. So each name below points at the
    // ---- T-series conductor increment that will ACTUALLY back it (§B12.8 rider (d), §B12.5a).
    //
    //  AutoPilot        → gen-2 `CrewProcedureOps.Engaged` (the AUTO SEQUENCE master). NO-OP: the glue has no
    //                     host yet and must not be ticked without one — see the block above. **W10** lands
    //                     the read-only host; **T17** then binds the pinned MechJeb core to it.
    //  StationApproach  → the CONDUCTOR's §B9 Phase-3 approach: MechJeb Maneuver-Planner ops composed and
    //                     re-planned live (§B1/§B12.4), flown by the Node Executor. NO-OP: that phase is not
    //                     built. **T19.** (Not W20 — W20 is a reference READ of the deleted hand-written
    //                     `RendezvousControl.cs`, mined to TUNE this phase; it lands no code.)
    //  DockingOps       → the MechJeb **Docking Autopilot**, the DEFAULT from the Keep-Out Sphere inward
    //                     (O6, owner 2026-09-03; §B10.3/§B12.3), with the manual button overriding to the
    //                     Manual ISS Docking screen. NO-OP: not built. **T20.** (Not W21 — that is a
    //                     reference READ, kept for the IDSS envelope + corridor geometry MechJeb lacks.)
    //  UndockOps        → the conductor's §B9 Phase 6: SmartASS backout + small departure burns → Node
    //                     Executor. NO-OP: not built. **T21, increment 1** (§B12.5a: one property per
    //                     increment, undock before deorbit).
    //  DeorbitOps       → the conductor's §B9 Phase 7: `OperationPeriapsis` → Node Executor, then P8 entry
    //                     attitude hold (O8) and P9 chutes. NO-OP: not built. **T21, increment 2.**
    //  BoosterRecovery  → the SCRIPTED booster autopilot on its OWN vessel (§B16) — ours, not MechJeb's —
    //                     surfaced through gen-2 `MissionConductor.RecoveryBooster`'s focus/PRE machine
    //                     (§B16.7). NO-OP: MissionConductor does not compile here, and the `BoosterControl`
    //                     under it STAYS DELETED — §B16.1 writes that core fresh. **W9**, then §B16.
    //
    // ⛔ NOTHING BELOW IS LIVE, AND NOTHING BELOW LIES. G6 changed the EXPLANATION, never the behaviour:
    // every one still returns false/null, so every lamp these feed is dark and every flight command is
    // §14.4(a)'s honest no-op — click, no light, no action, and no red.
    public static class AutoPilot { public static bool Engaged { get { return false; } } }
    public static class StationApproach { public static bool Engaged { get { return false; } } public static string Note { get { return null; } } }
    public static class DockingOps { public static bool Engaged { get { return false; } } public static string Note { get { return null; } } }
    public static class DeorbitOps { public static bool Engaged { get { return false; } } }
    public static class UndockOps { public static bool Engaged { get { return false; } } public static string Note { get { return null; } } }

    // ---- HullCams had followed the recovery booster; there is no recovery booster now ----
    public static class BoosterRecovery { public static Vessel Tracked { get { return null; } } }
}
