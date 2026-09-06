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

    // ---- ⛔ THE CrewProcedureOps AND FlightDriver STUBS ARE RETIRED (W10 / Wave D, 2026-09-05). ----
    // They used to be declared here as idle stand-ins: a conductor that was never engaged and a host with no
    // abort, no fault and idle authority. BOTH REAL FILES ARE BACK — `src/CrewProcedureOps.cs` and
    // `src/FlightDriver.cs`, restored from `8b81816^` per §B12.8 Wave D — and they carry every member the
    // screens compile against with identical signatures. This is the SAME swap W2 made for `Actuator` and
    // it follows the same rule: the class name the screens compile against did not change, only what stands
    // behind it. Do NOT re-add a stub for either; two declarations of the same type break the build
    // (§B12.8's two-generation rule).
    //
    // ⚠ WHY THEY HAD TO LAND TOGETHER, recorded so it is not undone. `CrewProcedureOps`'s whole state
    // machine advances inside `Tick(Vessel)`, and the ONLY caller of `Tick` in the entire pre-deletion tree
    // was `FlightDriver.cs:341`. The real conductor WITHOUT its host would have made the AUTO SEQUENCE
    // button (`ScreenPainter.cs:967`) engage something that can never tick: the gate card appears, its items
    // tick, and the crew's GO latches into `goPressed` for nobody to consume — a lit button and a dead GO,
    // strictly worse than the honest no-op §14.4(a) requires.
    //
    // ⚠ WHAT THE RESTORED PAIR DOES AND DOES NOT DO. The host is READ-ONLY (§B12.6 build-order step (3)):
    // it owns the flight-scene addon and `OnFlyByWire` (which writes no axis), ticks the conductor, and
    // reports phase/engaged. The conductor's GATES are genuinely live — items satisfy from vessel state,
    // the crew taps and presses GO, the plan advances — and its `AutoAdvanceGates` hands-off flag now
    // SHIPS FALSE, so the gates are interactive rather than decorative. NOTHING COMMANDS THE VEHICLE:
    // no ignition, no throttle, no attitude, no abort. `FlightDriver.HasControllerFor` is the empty phase
    // table that keeps the conductor from naming a phase nothing is flying, and each later increment grows
    // it (§B12.8 rider (c)). See both files' headers for the full account.

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

    // ---- ⛔ THE MissionConductor STUB IS RETIRED (W9, 2026-09-07). ----
    // It used to declare a no-op `MissionConductor` holding a display-only `AutoRecoverBooster` flag that
    // nothing read and a `RecoveryBooster` that was always null. The REAL one is back —
    // `src/MissionConductor.cs`, restored per §B12.8 Wave D — and it carries both members with identical
    // signatures, so this is the same facade swap W2 made for `Actuator`: the class name the screens
    // compile against did not change, only what stands behind it. Do NOT re-add a stub MissionConductor
    // here; two declarations of the same type break the build (§B12.8's two-generation rule).
    // The two blockers this stub recorded are both spent, and neither was resolved by relaxing a rule:
    //   1. `BoosterControl` STAYS DELETED — the restored file calls no member of it. §B16.1's fresh
    //      booster core exists (`src/BoosterHost.cs`, W23/W24) and does the flying; the conductor manages
    //      the physics ranges around it and never searches for, hooks or drives a booster itself.
    //   2. `FlightDriver.CmdTransX/Y/Z` is still NOT added — §B12.8(a) forbids half-wiring that facade,
    //      so the restored burn-guard reads the main throttle only and says so at its own definition.

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
    //  AutoPilot        → gen-2 `CrewProcedureOps.Engaged` (the AUTO SEQUENCE master), ticked by the real
    //                     `src/FlightDriver.cs`. LIVE since W10, 2026-09-05. The lamp is lit exactly when the
    //                     crew-gate conductor is engaged and being ticked — which is the honest meaning of
    //                     "AUTO SEQUENCE engaged" in a read-only build: the PROCEDURE is running, and nothing
    //                     is flying. **T18 onward** adds the controllers that make it fly (§B12.6 step (4+)).
    //  StationApproach  → the CONDUCTOR's §B9 Phase-3 approach: MechJeb Maneuver-Planner ops composed and
    //                     re-planned live (§B1/§B12.4), flown by the Node Executor. **LIVE since T19,
    //                     2026-09-07** — `MechConductor.ApproachEngaged`, true only while the core holds
    //                     drive authority on a far-field leg, dark inside the Keep-Out Sphere. (Not W20 —
    //                     W20 is a reference READ of the deleted hand-written `RendezvousControl.cs`,
    //                     mined to TUNE this phase; it lands no code.)
    //  DockingOps       → the MechJeb **Docking Autopilot**, the DEFAULT from the Keep-Out Sphere inward
    //                     (O6, owner 2026-09-03; §B10.3/§B12.3), with the manual button overriding to the
    //                     Manual ISS Docking screen. **LIVE since T20, 2026-09-07** —
    //                     `MechConductor.DockingEngaged`, true only on the CAPTURE leg with the core
    //                     driving; dark when berthed and dark under manual docking. ⚠ The manual-override
    //                     MECHANISM is live (`MechConductor.RequestManualDocking`); the screen BUTTON
    //                     that calls it is §B12.5's front-end and is NOT wired — see T20's register line.
    //                     (Not W21 — that is a reference READ, kept for the IDSS envelope + corridor
    //                     geometry MechJeb lacks.)
    //  UndockOps        → the conductor's §B9 Phase 6: SmartASS backout + the trunk jettison, out to
    //                     §B11's Approach Ellipsoid. **LIVE since T21 increment 1, 2026-09-07** —
    //                     `MechConductor.UndockEngaged`.
    //  DeorbitOps       → the conductor's §B9 Phase 7: `OperationPeriapsis` → Node Executor, then P8 entry
    //                     attitude hold (O8) and P9 chutes. **LIVE since T21 increment 2, 2026-09-07** —
    //                     `MechConductor.DeorbitEngaged`. ⚠ The two are disjoint by construction; §B12.5a
    //                     forbids them being lit together and the sets make that impossible.
    //  BoosterRecovery  → the SCRIPTED booster autopilot on its OWN vessel (§B16) — ours, not MechJeb's —
    //                     surfaced through gen-2 `MissionConductor.RecoveryBooster`'s §B16.7 range machine.
    //                     **LIVE since W9, 2026-09-07.** `Tracked` is the booster `src/BoosterHost.cs` is
    //                     flying, gated on the conductor's recovery stage. No `BoosterControl` byte is
    //                     back and no focus moves — it is the hull-camera follow, not a command.
    //
    // ⛔ ALL SIX ARE NOW LIVE, AND NONE OF THEM LIES. W10 (2026-09-05) `AutoPilot.Engaged`; W9
    // (2026-09-07) `BoosterRecovery.Tracked`; T19 `StationApproach`; T20 `DockingOps`; T21 `UndockOps`
    // then `DeorbitOps` — one property per increment, every time (§B12.5).
    // ⛔ AND THAT CHANGES NOTHING ABOUT §14.4(a) ON THE SCREENS. These six are STATUS READS. Every
    // flight COMMAND on every screen is still an honest no-op — `FlightCommands.Run` returns false for
    // everything that would fly, and this batch did not touch it. Click, no light, no action, no red.
    // ⚠ The lamps are lit by the CONDUCTOR, which the crew engage with AUTO SEQUENCE; they are not lit
    // by pressing a flight button, because pressing a flight button still does nothing.
    // `AutoPilot.Engaged` lighting means the CONDUCTOR is engaged, not that anything is flying: the host
    // behind it is read-only and commands nothing (§B12.6 step (3)). `BoosterRecovery.Tracked` going
    // non-null means a hull camera has something to look at on a SEPARATE vessel; the Dragon's own flight
    // is untouched by it.
    public static class AutoPilot { public static bool Engaged { get { return CrewProcedureOps.Engaged; } } }
    // ---- ⭐ LIVE SINCE T19 (2026-09-07) — THIS INCREMENT'S ONE FACADE FLIP (§B12.5: exactly one). ----
    // `MechConductor.ApproachEngaged` is true exactly while the conductor holds the vehicle on a
    // far-field rendezvous leg — MechJeb's Maneuver-Planner operations composed by us and flown by the
    // Node Executor, re-planned live (§B1 / §B12.4). ⛔ IT IS NEVER LIT AHEAD OF THE VEHICLE: it reads
    // `Flying`, which is the core's own drive authority, so a conductor that is idle, holding at a crew
    // gate, or standing down because it has no controller leaves the lamp DARK. Inside the Keep-Out
    // Sphere the far-field job is over and this goes dark too — that is `DockingOps`, T20's flip.
    public static class StationApproach
    {
        public static bool Engaged { get { return MechConductor.ApproachEngaged; } }
        public static string Note { get { return MechConductor.ApproachNote; } }
    }
    // ---- ⭐ LIVE SINCE T20 (2026-09-07) — THIS INCREMENT'S ONE FACADE FLIP (§B12.5: exactly one). ----
    // `MechConductor.DockingEngaged` is true exactly while MechJeb's Docking Autopilot — the DEFAULT
    // from the Keep-Out Sphere inward (O6, owner 2026-09-03; §B10.3/§B12.3) — holds the vehicle on the
    // CAPTURE leg. ⛔ IT IS DARK IN THREE STATES THAT MATTER: when the vehicle is BERTHED (that step is
    // SmartASS KILL-ROT, not a docking approach), when the crew have taken MANUAL docking (§B12.3: the
    // manual button shuts the autopilot down), and whenever the core is not driving at all — because it
    // reads `Flying`, which is the core's own drive authority.
    public static class DockingOps
    {
        public static bool Engaged { get { return MechConductor.DockingEngaged; } }
        public static string Note { get { return MechConductor.DockingNote; } }
    }
    // ---- ⭐ LIVE SINCE T21 (2026-09-07) — TWO PROPERTIES, TWO INCREMENTS, IN §B12.5a's OWN ORDER. ----
    // §B12.5a: "⚠ `UndockOps` and `DeorbitOps` are ONE task, TWO increments … T21 flips `UndockOps` on
    // the departure leg, then `DeorbitOps` on the return leg. Never both in one step." They cannot both
    // be lit: `UndockEngaged` covers only `Backout`/`TrunkJettison`, and `DeorbitEngaged` covers only
    // the steps `ReturnSequence.Beyond` names — the two sets are disjoint by construction, and both
    // read `Flying`, so neither can light ahead of the vehicle.
    public static class DeorbitOps
    {
        public static bool Engaged { get { return MechConductor.DeorbitEngaged; } }
        public static string Note { get { return MechConductor.ReturnNote; } }
    }
    public static class UndockOps
    {
        public static bool Engaged { get { return MechConductor.UndockEngaged; } }
        public static string Note { get { return MechConductor.ReturnNote; } }
    }

    // ---- ⭐ LIVE SINCE W9 (2026-09-07) — THIS INCREMENT'S ONE FACADE FLIP (§B12.5: exactly one). ----
    // `src/HullCams.cs:75` follows this vessel with the hull cameras. It is now the booster the §B16 host
    // is actually flying, gated on the conductor's own recovery stage — so it is non-null exactly while a
    // recovery is running and null otherwise. ⚠ IT IS A CAMERA FOLLOW, NOT A COMMAND: nothing about this
    // property flies anything, and no focus changes (§B16.7 — focus never leaves the upper stage).
    public static class BoosterRecovery { public static Vessel Tracked { get { return MissionConductor.RecoveryBooster; } } }
}
