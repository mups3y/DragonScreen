# COMMAND REGISTRY (ACTIVE)

> Every interactive control and its complete command path. Governed by `MASTER_BUILD_SPEC.md`. Rule C4: a control is finished only when it performs the real function end to end. No decorative controls (rule 0.3). A blocked command explains why (rule C5), never silently no-ops.

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
