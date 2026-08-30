# TELEMETRY REGISTRY (ACTIVE)

> Every important displayed datum and its single authoritative source. Governed by `MASTER_BUILD_SPEC.md` + `SOURCE_OF_TRUTH.md`. The screen may **format** a datum; it may not create a competing version (rule T6). A datum with no legitimate source is flagged `UNKNOWN — EVIDENCE REQUIRED`, not invented.

Contract columns: **ID · Authority (owning subsystem) · Physical source · Type/Units · Update rate · Fallback · Screen may modify**. Fallbacks: `NO DATA` (unavailable), `INVALID` (NaN/out-of-range), `N/A` (not applicable this phase).

Seeded for the Phase-7 Docking page + core chrome. Expanded per page as pages are built (rule S4). "Authority" values in **bold** are the TARGET after Phases 3–4; today several are recomputed in `VesselData` (flagged ⚠, to be re-plumbed).

## Docking page
| ID | Authority | Physical source | Type/Units | Rate | Fallback | Modify |
|---|---|---|---|---|---|---|
| DOCK_RANGE | **DockingControl.NavState3** ⚠ (today: `VesselData.Docking()`) | relative vessel transform, filtered | distance, m | 10 Hz | NO DATA | NO |
| DOCK_RATE | **DockingControl.NavState3** ⚠ | relative velocity along LOS, filtered | speed, m/s (− = closing) | 10 Hz | NO DATA | NO |
| DOCK_OFF_X/Y/Z | **DockingControl.NavState3** ⚠ | LVLH offsets (`pure/Lvlh.cs`) | distance, m | 10 Hz | NO DATA | NO |
| DOCK_ALIGN | **DockingControl.NavState3** ⚠ | port-axis misalignment angle | deg (+ `Align01` 0..1) | 10 Hz | NO DATA | NO |
| DOCK_PITCH/YAW/ROLL_ERR | **DockingControl** ⚠ | relative attitude error | deg | 10 Hz | NO DATA | NO |
| DOCK_TARGET_WP | **DockingControl.NextGateId** (new to snapshot) | active leg WP0/WP1/WP2/contact | enum | on change | N/A | NO |
| DOCK_CAPTURE | **DockingControl** (`DockCapture`/IDSS) | capture-envelope status | enum | on change | N/A | NO |
| GNC_MODE | **AuthorityManager** (new) | who owns trans/attitude | AUTO/MANUAL/ABORT/RECOVERY | on change | NO DATA | NO |
| RCS_ENABLED | KSP `ActionGroups[RCS]` | vessel action group | bool | 5 Hz | NO DATA | NO |
| RCS_DEMAND_X/Y/Z, ROT_P/Y/R | KSP `FlightCtrlState` | commanded RCS (indicator wedges) | −1..1 | frame | 0 | NO |

## Core chrome / flight
| ID | Authority | Type/Units | Fallback |
|---|---|---|---|
| MISSION_PHASE | **CrewProcedureOps.ActivePhase** (extended enum) ⚠ (today: `Mission.Classify` string) | enum label | NO DATA |
| MET / UTC | KSP `Vessel.missionTime` / `Planetarium` | clock | `-` |
| ALT / VEL / AP / PE / INC | KSP `vessel.orbit`/`altitude`/`obt_speed` | m / m/s / deg | `-` (NaN→`-`) |
| ALERT_SEVERITY / ALERT_LIST | **Fdir** spine (`FaultKind`) ⚠ (today: `pure/Alarms.cs`) | severity + list | Nominal |
| PROPELLANT | KSP resources + `pure/PropellantReadout.cs` | %/kg | `-` |
| POWER (batt/solar/load/bus) | KSP resources | W / % | NO DATA |
| ECLSS (O2/CO2/press/temp) | TAC-LS via `pure/CabinEnvironment.cs` — **SIMULATION** | varies | NO DATA |
| BOOSTER_STATUS | **MissionConductor.RecoveryBooster** + `BoosterControl` FSM | enum + telemetry (tagged `VehicleId`) | N/A |

> ⚠ = currently recomputed/invented in the display world; Phases 3–4 route it from the authoritative owner. NaN guarding already exists on the display side (`VesselData.Clamp01`, formatting helpers) and must be preserved (rule N2).
