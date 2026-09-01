# COMMAND REGISTRY

> **RECONCILED 2026-09-02 (T1). Governed by `docs/BUILD_PLAN.md` (C7.1)** — `MASTER_BUILD_SPEC.md` was
> deleted in the 2026-09-01 pivot. **What the plan overrides here, and it is most of the routing:**
> • **the command path below is a TARGET, not today's code.** `DockingControl` / `NavState3` and the real
>   `FlightDriver.SetTranslation/SetAttitude` actuation are **gone** (deleted 2026-09-01). What survives is an
>   idle seam: `MissionOps`, `Actuator`, `AbortControl`, `FlightDriver` are **no-op stubs** in
>   `src/_AutopilotStub.cs`, and **`AuthorityManager` survives only as a display label** in
>   `pure/ScreenModes.cs` (it names + colours the GNC lamp; it arbitrates nothing). Part B §B12.5 turns each
>   row real, one command at a time.
> • **refusal is silent, not red.** §14.4(a) settled the console behaviour: buttons light BRIGHT when
>   active/armed/fired, there is **NO red state**, and a refused or inert press = an audible **click**, no
>   light, no action. The "red dash with a reason" rule below is superseded.
> • **some controls are deliberately inert.** §14.4(b): SWAP 1/2/3 and the inferred entry-mode toggles
>   (ENABLE ENTRY REBOOT / BACKUP ENTRY / NORMAL ENTRY) stay inert until a real console-procedure source
>   verifies them. POWER 1/2, STRING 1A–2C, RESET 1/2, the fire/leak responses and the CONFIRMED entry
>   commands remain real display-state.
> The *shape* of this registry — one row per control, with its authority, target, interlock and feedback —
> is current and is what Part B fills in.

> Every interactive control and its complete command path. A control is finished only when it performs the real function end to end. No decorative controls.

Canonical command path:
```
USER INPUT (ScreenTouch / PanelButton)
  → PageAction / PanelMap
    → COMMAND (this registry)
      → AuthorityManager (grant/deny; abort pre-empts)
        → mission / guidance / control subsystem
          → actuator → KSP physics → telemetry → screen feedback
```
Contract columns: **ID · Input · Authority · Path/target · Manual? · AUTO? · Abort · Interlock · Feedback**.

## Existing (audited)
| ID | Input | Authority | Target | Manual | AUTO | Abort | Interlock | Feedback |
|---|---|---|---|---|---|---|---|---|
| TOGGLE_AUTO_SEQUENCE | FLIGHT page button | mission | `CrewProcedureOps.Toggle` (`ScreenPainter.Apply`) | — | engages FSM | n/a | crew GO gate (`GateCard`) | AUTO lamp / phase label |
| UNDOCK | FLIGHT page button | mission | `MissionOps.Undock → Actuator.Undock` | yes | — | n/a | phase = Docked | state change |
| ACK_STEP | modal `GateCard` | mission | advance gate | yes | — | n/a | gate active | step list update |
| NAV pan/zoom, lights, brightness, camera, set-screen-page | touch | display (SECONDARY) | `ScreenPainter.Apply` | yes | — | never authority | — | visual |
| PANEL buttons (~39) + ABORT handle | `PanelButtons`/`PanelMap` | flight / abort | `FlightCommands`, arm/EXECUTE `Interlock` | yes | — | ABORT pre-empts | arm→EXECUTE | indicator lamp |

## New for the Docking gold-standard page (Phase 7) — real commands via AuthorityManager
| ID | Input | Authority | Target | Manual | AUTO | Abort | Interlock | Feedback |
|---|---|---|---|---|---|---|---|---|
| TRANSLATE_UP/DOWN/LEFT/RIGHT/FWD/BACK | Docking left cluster | **AuthorityManager** (grant Manual on translation) | `FlightDriver.SetTranslation` → RcsPulse → Draco | yes | no (AUTO owns it) | ABORT pre-empts | GNC=MANUAL required | vessel motion → NavState3 readout |
| ROTATE_PITCH/YAW/ROLL ± | Docking right cluster | **AuthorityManager** (grant Manual on attitude) | `FlightDriver.SetAttitude` | yes | no | ABORT pre-empts | GNC=MANUAL required | attitude → align readout |
| PRECISION_TOGGLE | Docking centre | display→command | scale factor on trans/rot demand | yes | n/a | n/a | — | mode indicator |
| TAKE_MANUAL / RETURN_AUTO | Docking mode control | **AuthorityManager** | grant/release Manual; release on AUTO/abort | yes | yes | ABORT pre-empts both | valid phase | GNC AUTO/MANUAL indicator |

## Rules
1. **AuthorityManager is the single arbiter.** Camera focus is never authority (rule C1). Abort always pre-empts and latches (rule C2).
2. Manual grants are **scoped** (only while GNC=MANUAL) and **released** cleanly on AUTO re-engage or abort.
3. Every command that cannot run returns a reason (e.g. `DEORBIT — NOT AVAILABLE · REASON: not in deorbit configuration`), rendered by the FAULT/rejected button state.
4. A command with an unclear authority is a STOP condition (rule C-block) — do not implement until resolved.
