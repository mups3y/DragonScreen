# SOURCE OF TRUTH (ACTIVE)

> Declares the single authoritative owner of each spacecraft-state concept. Governed by `MASTER_BUILD_SPEC.md`. The display **consumes** these; it never creates a competing version (rule 0.4/T4). Where "current" ≠ "target," Phases 3–5 close the gap. This is the contract `TELEMETRY_REGISTRY.md` and `COMMAND_REGISTRY.md` build on.

Target data flow:
```
KSP physics → vehicle state → navigation/guidance/mission/FDIR → AuthorityManager → immutable display snapshot → screens
```

## State ownership
| State concept | Authoritative owner (TARGET) | Current reality | Gap → fix |
|---|---|---|---|
| Mission phase | `CrewProcedureOps.ActivePhase` (extended `pure/MissionPhase` enum) | TWO: FSM enum **and** display `Mission.Classify` string (can disagree) | Phase 3: FSM enum is sole source; classifier retained only as labelled fallback when disengaged |
| Relative navigation (range/rate/offsets/alignment) | filtered `NavState3` (`pure/NavFilter.cs`) owned by the active rel-nav consumer (`DockingControl`/`RendezvousControl`) | display recomputes from raw KSP in `VesselData.Docking()`, independent of the filter | Phase 4: publish filtered `NavState3` read-only into the snapshot; screen reads it |
| Orbital state (Ap/Pe/incl/vel/alt) | KSP `vessel.orbit` (authoritative physics) | display reads directly (legitimate) | keep; route through snapshot for consistency |
| Fault / severity / recovery | `Fdir` spine (`FaultKind`/`Recovery`, `FlightDriver.lastFdirReport`) | screen invents alerts in `pure/Alarms.cs` from `PageState` thresholds (unrelated) | Phase 4/11: FDIR feeds the snapshot; `Alarms` demoted to crew-environment layer |
| Control authority (who owns which axis) | **AuthorityManager** (new, Phase 2) | per-axis latches in `FlightDriver` (`transOwned/attitudeOwned/rollOwned/throttleOwned`); no manager | Phase 2: extract manager; screen reads AUTO/MANUAL/ABORT/RECOVERY per axis-group |
| Automation engaged / step | `CrewProcedureOps.Engaged` / step label | surfaced as strings only | Phase 3/4: expose via snapshot with the phase enum |
| Vehicle identity (Dragon vs Booster) | per-vessel; `MissionConductor.RecoveryBooster` | booster status passively reflected | Phase 4: explicit `VehicleId` on every telemetry item |
| Docking target / waypoint / capture envelope | `DockingControl` (`NextGateId` WP0/WP1/WP2, `DockCapture`/IDSS) | not exposed to screen | Phase 4/7: publish to snapshot; Docking page shows it |
| Propellant / resources | KSP resources + `pure/PropellantReadout.cs` | display reads directly | keep; route through snapshot |
| Life support (O2/CO2/press/temp) | TAC-LS resources via `pure/CabinEnvironment.cs` (simulated where KSP lacks a source) | simulated | keep; label SIMULATION in evidence matrix |
| Abort state | abort latch (always-live in `FlightDriver`) → AuthorityManager pre-emption | latched; not fully surfaced | Phase 2/11: expose latched abort state to screen |

## Hard rules
1. The **display snapshot is immutable and presentation-only** (rule T2): it caches/formats, never decides, never writes back into flight state — one-way flow, no feedback loop.
2. A datum with **no authoritative owner is not invented** in the display (rule T3): flag it here as `UNKNOWN — EVIDENCE REQUIRED` or add the smallest upstream publisher.
3. Presentation may convert units/format only; it may not recompute mission truth.
