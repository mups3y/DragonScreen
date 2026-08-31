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

> **Post-Phase-2 re-fly status (2026-08-31, flight `DS-ASC-001` — see `FLIGHT_VERIFICATION.md`):** ascent+separation+dual-vessel re-flown **nominal & fault-free, no NaN** (AuthorityManager/`Clamp1` changes show no regression) — but the flight was **reverted mid-S2**, so **orbit insertion is not re-proven** and **max-Q abort was not exercised** (still owed). Booster flip worked (att_err→1.96°) but was **underdamped (20–60° oscillation)** and **never lit its entry burn / landed** (reverted while still ascending).

## Architecture / seam (this program, Phase 0–7)
| Item | Status | Phase | Notes |
|---|---|---|---|
| Baseline freeze + test | DONE (L1) | 0 | 731,239+ checks pass; tag pending |
| Nine ACTIVE governing docs | DONE | 1 | created |
| Old plans deleted | DONE | 1 | 7 obsolete plans removed from the repo (git history retains them) |
| AuthorityManager (behavior-preserving) | **CODE — UNFLIGHTED** (L1 pass) | 2 | new `pure/AuthorityManager.cs` (30 tests); additive authority publish in `FlightDriver` (OnFlyByWire untouched); **regression re-fly pending** |
| `Clamp1` / `Clamp1f` / `Clamp01f` NaN fix (N2) | **CODE — UNFLIGHTED** | 2 | `FlightDriver.cs`, `BoosterControl.cs` — NaN→0; rides the same re-fly |
| Authoritative mission phase | **CODE — UNFLIGHTED** (L1+L2) | 3 | `Mission.AuthoritativePhase` — display shows the FSM's phase while flying, classifier only as fallback (6 tests); preview renders clean. **Display-only, no flight dynamics → no flight needed.** |
| `MissionPhase` enum extension | **DEFERRED** (§42 scoping) | 3→later | Extending with members the FSM doesn't yet produce is speculative (rule P4/P1). Do it when the FSM genuinely distinguishes finer phases (that change is flight-gated). Reasoning logged here per the phase-order rule. |
| Display snapshot re-plumb (`PageState`) | **CODE — UNFLIGHTED** (L1+L2) | 4 | General authoritative seam done: phase authoritative (T4); AuthorityManager AUTO/MANUAL mode (C6); **FDIR fault spine → crew alert severity + fault name (§4.2 fix)** — chrome STATE now folds in `Fdir` via `Alarms.SystemSeverity`. Display-only, no flight. Docking-specific filtered rel-nav (`NavState3` + waypoint + capture) deferred to Phase 7, built with the docking page. |
| FDIR → screen alerts (§4.2) | **CODE — UNFLIGHTED** (L1+L2) | 4/11 | `Alarms.FdirSeverity`/`SystemSeverity` + `Fdir.FaultName`; the real spine reaches the alert channel (11 tests). A dedicated FDIR/Alerts page is Phase 11. |
| TELEMETRY_REGISTRY populated | **DONE (docs)** | 5 | reference-confirmed telemetry (nav mission page + docking attitude + chrome) mapped to authoritative KSP/RO sources; Phase 2–4 items marked routed |
| COMMAND_REGISTRY populated | **OPEN** | 5 | seeded; command **paths/gating** await the review decision (does the AuthorityManager gate the actuation path) |
| Screen component system | **IN PROGRESS (L1+preview)** | 6 | `NumericReadout` (green-correction/blue-rate + `—` honesty placeholder), `StatusIndicator` (AUTO/MANUAL/ABORT + severity, C6), `TargetReticle`, and **`AttitudeHud`** — the docking attitude display built on the **LIVE game navball** (`ImageId.NavBallLive`), not a synthetic sphere. Pure, headless-tested + rendered in a new **component gallery** (`build.py preview` → `page_gallery.png`). Expanded button/FAULT states + final docking layout build **with** the Docking page (Phase 7, §62). |
| Docking gold-standard page (AUTO view) | **LOOK ACCEPTED · L2 in-game verified (display-only)** | 7 | Rebuilt 2026-08-31 to the **real-HUD monitoring layout** (evidence: CONFIRMED real-HUD video + iss-sim outrank the RECONSTRUCTED Figma navball). `DockingPage.Build` = **centred reticle in thin concentric rings + green-diamond target over the live docking camera — NO navball**: ROLL/PITCH/YAW corrections (green)+rates (blue) left, X/Y/Z/ALIGN right, RANGE bottom-left, RATE bottom-right, AUTO/MANUAL badge (C6). **Owner verdict 2026-08-31: "fine for now" (look accepted).** **L2 (`DS-DOCKUI-001`):** rendered on the live IVA screen through a full ascent, **zero exceptions**, reads authoritative rel-nav (X/Y/Z vector-sum=RANGE, ALIGN drives the sweep). L1: `PageTest.DockingLayout` (never-regress invariant "draws no navball"). Display-only (rules T1/T2/E4 — commands nothing yet). **Open (display half):** blue per-axis **rates read "—" in-game** (VesselData doesn't publish angular rates — next increment, rule T3); target diamond on boresight (2-D bearing owed). **Open (operational half, review-gated):** MANUAL translation/rotation clusters + **real** RCS/attitude commands via AuthorityManager (COMMAND_REGISTRY). ⛔ Rejected navball prototypes superseded — do not resurrect. |

## Screens (current pages)
| Page | Status | Notes |
|---|---|---|
| FLIGHT / VEHICLE / NAV / DOCKING / SETTINGS | IMPLEMENTED | render; but consume the display "second world" (to be re-plumbed Phase 4) |
| Font (D-DIN) | OPEN (defect) | silent Arial fallback; proper bitmap font Phase 18 |

## Known "fixed" items to re-verify (directive §56 — treat as unproven until flown)
ascent separation · S2 roll trim · rendezvous detumble timeout · unified deorbit burn · trunk jettison · shroud timing · booster ignition mode · RCS authority (geometric fallback) · rendezvous strand fix · AUTO-sequence state awareness · max-g correction · recorder warp correction · ignition-spam correction · separation-collision correction. Each is `CODE FIXED — UNFLIGHTED` unless a flight ID in `FLIGHT_VERIFICATION.md` proves it.
