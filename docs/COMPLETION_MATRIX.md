# COMPLETION MATRIX (ACTIVE)

> One authoritative status grid for every major subsystem. Governed by `MASTER_BUILD_SPEC.md`. Statuses: **FLIGHT-PROVEN · PARTIALLY PROVEN · TESTED (L1/L2, not flown) · IMPLEMENTED (code exists) · OPEN (not started/broken)**. Never upgrade a status without evidence (rule V1/V2). Levels: L1 pure · L2 integration · L3 single-vessel flight · L4 multi-vessel · L5 end-to-end.

## Mission / flight systems
| System | L1 | L2 | L3 | L4 | L5 | Status | Notes / owning code |
|---|---|---|---|---|---|---|---|
| Ascent to orbit | ✓ | ✓ | ✓ | ✓ | — | FLIGHT-PROVEN | `AscentControl`; protected |
| Max-Q launch escape | ✓ | ✓ | ✓ | — | — | FLIGHT-PROVEN | `AbortControl`; protected |
| Dual-vessel booster control | ✓ | ✓ | ✓ | ✓ | — | PARTIALLY PROVEN | controlled, not landed; `MissionConductor`/`BoosterControl` |
| Booster RTLS landing | ✓ | ✓ | — | — | — | TESTED | `BoosterControl` |
| Booster ASDS landing | ✓ | ✓ | — | — | — | TESTED | `BoosterControl` |
| Rendezvous | ✓ | ✓ | — | — | — | TESTED | reaches region, no dock; `RendezvousControl` |
| Docking (autonomous) | ✓ | ✓ | — | — | — | TESTED | no end-to-end; `DockingControl` |
| Deorbit / Entry / Splashdown | ✓ | ? | — | — | — | OPEN | 0 autonomous returns; `ReturnControl` |
| FDIR spine | ✓ | ✓ | — | — | — | TESTED | `Fdir`/`FaultMonitor`; observe-only (`FdirActing` off) |
| Abort latch/authority | ✓ | ✓ | ✓ | — | — | PARTIALLY PROVEN | proven in max-Q; broaden coverage |

## Architecture / seam (this program, Phase 0–7)
| Item | Status | Phase | Notes |
|---|---|---|---|
| Baseline freeze + test | DONE (L1) | 0 | 731,239+ checks pass; tag pending |
| Nine ACTIVE governing docs | IN PROGRESS | 1 | this pass |
| Superseded-plan quarantine | IN PROGRESS | 1 | → `docs/archive/superseded/` |
| AuthorityManager (behavior-preserving) | **CODE — UNFLIGHTED** (L1 pass) | 2 | new `pure/AuthorityManager.cs` (30 tests); additive authority publish in `FlightDriver` (OnFlyByWire untouched); **regression re-fly pending** |
| `Clamp1` / `Clamp1f` / `Clamp01f` NaN fix (N2) | **CODE — UNFLIGHTED** | 2 | `FlightDriver.cs`, `BoosterControl.cs` — NaN→0; rides the same re-fly |
| `MissionPhase` enum extension | OPEN | 3 | single authoritative enum |
| Authoritative mission/vehicle state | OPEN | 3 | eliminate `Mission.Classify` shadow-phase |
| Display snapshot re-plumb (`PageState`) | OPEN | 4 | read authoritative state, one-way |
| TELEMETRY_REGISTRY / COMMAND_REGISTRY populated | OPEN | 5 | every datum + command has a source |
| Screen component system (Docking set) | OPEN | 6 | NumericReadout/StatusIndicator/TargetReticle/… |
| Docking gold-standard page | OPEN | 7 | AUTO/MANUAL + real commands; flight-tested |

## Screens (current pages)
| Page | Status | Notes |
|---|---|---|
| FLIGHT / VEHICLE / NAV / DOCKING / SETTINGS | IMPLEMENTED | render; but consume the display "second world" (to be re-plumbed Phase 4) |
| Font (D-DIN) | OPEN (defect) | silent Arial fallback; proper bitmap font Phase 18 |

## Known "fixed" items to re-verify (directive §56 — treat as unproven until flown)
ascent separation · S2 roll trim · rendezvous detumble timeout · unified deorbit burn · trunk jettison · shroud timing · booster ignition mode · RCS authority (geometric fallback) · rendezvous strand fix · AUTO-sequence state awareness · max-g correction · recorder warp correction · ignition-spam correction · separation-collision correction. Each is `CODE FIXED — UNFLIGHTED` unless a flight ID in `FLIGHT_VERIFICATION.md` proves it.
