# TELEMETRY REGISTRY

> **RECONCILED 2026-09-02 (T1). Governed by `docs/BUILD_PLAN.md` (C7.1)** — `MASTER_BUILD_SPEC.md` and
> `SOURCE_OF_TRUTH.md` were both deleted in the 2026-09-01 pivot; the source-of-truth hierarchy they carried
> now lives in the plan as **§1.4** (verified-real → other users' recreations, marked → invention only by
> owner discussion). **What the plan overrides here:** every **Authority** cell naming `DockingControl` or
> `NavState3` names code that no longer exists — read those rows as Part B targets. `AuthorityManager`
> (display label only, `pure/ScreenModes.cs`), `MissionConductor` and `Fdir` (no-op stubs in
> `src/_AutopilotStub.cs`) still compile but own nothing. **`BOOSTER_STATUS` is dead**: booster recovery was
> deleted with the autopilot, Part B does not re-introduce it, and `MissionConductor.RecoveryBooster` is a
> stub returning null behind a screen toggle nothing acts on. Rows sourced directly from KSP
> (`vessel.orbit`, resources, `missionTime`, action groups) and from the pure display models
> (`CabinEnvironment`, `PropellantReadout`, `VehicleSystems`) are CURRENT. The rule the file exists for — one
> authoritative source per datum, `UNKNOWN — EVIDENCE REQUIRED` rather than invention — stands unchanged.

> Every important displayed datum and its single authoritative source. The screen may **format** a datum; it may not create a competing version (rule T6). A datum with no legitimate source is flagged `UNKNOWN — EVIDENCE REQUIRED`, not invented.

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
| GNC_MODE | **AuthorityManager** via `FlightDriver.MissionMode` ✅ (Phase 2/4) | who owns trans/attitude | AUTO/MANUAL/ABORT/RECOVERY | on change | NO DATA | NO |
| RCS_ENABLED | KSP `ActionGroups[RCS]` | vessel action group | bool | 5 Hz | NO DATA | NO |
| RCS_DEMAND_X/Y/Z, ROT_P/Y/R | KSP `FlightCtrlState` | commanded RCS (indicator wedges) | −1..1 | frame | 0 | NO |

## Core chrome / flight
| ID | Authority | Type/Units | Fallback |
|---|---|---|---|
| MISSION_PHASE | **CrewProcedureOps.ActivePhase** via `Mission.AuthoritativePhase` ✅ (Phase 3) | enum label | NO DATA |
| MET / UTC | KSP `Vessel.missionTime` / `Planetarium` | clock | `-` |
| ALT / VEL / AP / PE / INC | KSP `vessel.orbit`/`altitude`/`obt_speed` | m / m/s / deg | `-` (NaN→`-`) |
| ALERT_SEVERITY / ALERT_LIST | **Fdir** spine via `Alarms.SystemSeverity` + `Fdir.FaultName` ✅ (Phase 4) | severity + fault name | Nominal |
| PROPELLANT | KSP resources + `pure/PropellantReadout.cs` | %/kg | `-` |
| POWER (batt/solar/load/bus) | KSP resources | W / % | NO DATA |
| ECLSS (O2/CO2/press/temp) | TAC-LS via `pure/CabinEnvironment.cs` — **SIMULATION** | varies | NO DATA |
| BOOSTER_STATUS | **MissionConductor.RecoveryBooster** + `BoosterControl` FSM | enum + telemetry (tagged `VehicleId`) | N/A |

> ⚠ = still recomputed in the display world (to re-plumb); ✅ = now routed from the authoritative owner (Phases 2–4). NaN guarding on the display side (`VesselData.Clamp01`, formatting helpers) is preserved (rule N2).

## Reference-confirmed telemetry (from the real Crew Dragon / Figma pages → KSP/RO sources)
_The real/Figma pages (see `SCREEN_EVIDENCE_MATRIX.md`) display these; each is mapped to its authoritative source. Labels are the reference layout; **values come from real KSP/RO state** (E4), and anything with no real source is `UNKNOWN — EVIDENCE REQUIRED` or SIMULATION — never invented._

### NAV / mission page (Frame 67)
| Real label | ID | Authority / source | Units | Fallback |
|---|---|---|---|---|
| SPLASHDOWN TIME | SPLASHDOWN_ETA | `ReturnControl` deorbit/landing solution (predicted UT − now) | T−hh:mm:ss | N/A off-return |
| INERTIAL VELOCITY | ORB_VEL | KSP `vessel.obt_velocity.magnitude` | km/s | `-` |
| ALTITUDE | ALT | KSP `vessel.altitude` | m/km | `-` |
| APOGEE / PERIGEE | ORB_AP / ORB_PE | KSP `vessel.orbit.ApA` / `PeA` | km | `-` |
| INCLINATION | ORB_INC | KSP `vessel.orbit.inclination` | deg | `-` |
| TARGET LAT / LON | TGT_LAT / TGT_LON | deorbit/landing target (`ReturnControl`) | deg N / E | N/A |
| ground track / orbit line | GROUND_TRACK | KSP `vessel.orbit` propagated → conic on screen | — | NO DATA |
| POINTING MODE | POINTING_MODE | attitude controller / `Steering` target | enum (Sun+GEO/…) | `-` |
| CURRENT STATE | CURRENT_STATE | `CrewProcedureOps` step label | string | `-` |

### DOCKING / prox-ops attitude page (Frame 58) — additions to the docking table above
| Real label | ID | Authority / source | Units | Fallback |
|---|---|---|---|---|
| ROLL/PITCH/YAW rate | DOCK_*_RATE | KSP `vessel.angularVelocity` (body→control frame) | °/s | NO DATA |
| ACCELERATION | ACCEL_G | KSP `vessel.geeForce` | g | `-` |
| FRAME | DOCK_FRAME | fixed LVLH (`pure/Lvlh.cs`) | enum (LVLH) | LVLH |
| CAMERA | DOCK_CAMERA | `DockingCamRenderer` / camera selector | enum (Virtual/…) | NO DATA |

> Comm-link readouts (SPX / GND / TDRS / ISS) are **SIMULATION** unless a comms mod supplies them — degrade gracefully (S10). The systems big-number values (power/thermal/data) come from real KSP/RO + TAC-LS state, formatted like the reference.
