# DRAGONSCREEN — MASTER BUILD SPEC (the sole governing specification)

> **ACTIVE · AUTHORITY.** This is the one governing build specification for DragonScreen. If any other document — old plan, roadmap, TODO, architecture doc, session handoff, AI/Claude-generated plan, stale status block, or code comment — conflicts with it, **this file wins automatically**. Do not reconcile, merge, or choose between competing plans; follow this spec. Never resolve uncertainty by creating a new master plan; if the roadmap must change, edit *this file* in place and record the change (§ change log at the bottom).
>
> Established 2026-08-31 from the *Master Build Control Directive*, reconciled against the repository at HEAD `9073429`. Supersedes and replaces **all** prior plans — the obsolete plan docs were **deleted** from the repo so only this governing plan exists anywhere (recoverable via git history).

---

## 0. Governing objective
Create the most accurate practical interactive simulation of the **Crew Dragon crew interface** possible within KSP/RSS/RO — viewed from the astronaut's seat — backed by a **genuinely functional spacecraft/mission automation system** that flies the intended Dragon/Falcon missions, not merely convincing graphics. The project is simultaneously a systems simulation, a flight-control system, a mission-automation system, a Crew-Dragon HMI, an IVA interaction system, a dual-vessel control system, a fault/abort system, and a verification project. **Make it actually finished, not look finished.**

## 1. Absolute Authority Rule & document classification
Every repository document is exactly one of:
- **ACTIVE (instructions — the only docs that say what to do).** Allowlist, one name each:
  `MASTER_BUILD_SPEC.md` (whole project) · `SCREEN_SPEC.md` (screen detail; cannot override this file) · `COMPLETION_MATRIX.md` · `SOURCE_OF_TRUTH.md` · `TELEMETRY_REGISTRY.md` · `COMMAND_REGISTRY.md` · `SCREEN_EVIDENCE_MATRIX.md` · `FLIGHT_VERIFICATION.md` · `DEPENDENCY_MATRIX.md`. **Never** a second/competing UI-screen spec.
- **RESEARCH (evidence, not instruction)** — answers "what do we know?", never "what next?".
- **HISTORICAL** — why something exists (flight logs, past sessions); kept, never followed as instruction.
- **SUPERSEDED** — the obsolete *instruction* plans (`ULTIMATE_PLAN`, `AUTOPILOT_REBUILD_PLAN`, `MASTER_FIX_PLAN`, `CAMPAIGN_PLAN`, `CAPABILITY_BUILD_BACKLOG`, `RETURN_FIX_PLAN`, `NOMINAL_END_TO_END_BUILD`) were **deleted from the repo** so only this governing plan exists anywhere; git history retains them if ever needed. Do not resurrect them.
No plan-nesting. Research/evidence/flight logs are never deleted.

## 2. Definition of DONE (five verification levels)
Nothing is "complete" until it satisfies the relevant levels; a green unit-test report is **L1 only**.
- **L1** software/mathematical correctness (pure deterministic tests) · **L2** KSP integration correctness · **L3** single-vessel flight verification · **L4** multi-vessel (Dragon + booster, no authority loss) · **L5** end-to-end mission.
Status terms (never upgraded without evidence): **IMPLEMENTED · TESTED · FLIGHT-PROVEN · PARTIALLY PROVEN · OPEN**. Uncertainty tokens: `UNKNOWN — EVIDENCE REQUIRED`, `CONFLICT`.

## 3. The complete ruleset (enforceable; each citeable)
**Meta:** 0.1 don't trust previous plans · 0.2 no blind rewrite · 0.3 never fake functionality · 0.4 never create a second source of truth.
**STOP condition:** contradictory evidence · missing authoritative state · absent required system · dep behaves differently under RO/RSS · flight result contradicts model · UI value has no trustworthy source · ambiguous command authority · change risks a proven system · architecture would duplicate state → **STOP → DOCUMENT → INVESTIGATE → RESOLVE → IMPLEMENT**, never GUESS → PATCH → MOVE ON.

**A. Truth & state** — T1 present reality never invent · T2 read-only presentation-only snapshot (no state, no decisions, one-way flow) · T3 missing datum → smallest upstream publisher, never reconstruct in display · T4 one authoritative navigation/mission/authority state · T5 never debug a display discrepancy display-first (trace physics→subsystem→NavState3→snapshot→renderer) · T6 every datum declares its source-of-truth contract.
**B. Control & authority** — C1 one AuthorityManager; camera focus is never authority · C2 abort latch + pre-emption · C3 per-vehicle, fault-isolated · C4 operational fidelity is global (control→command→controller→physics→movement→nav→telemetry) · C5 safety interlocks; a blocked command explains why · C6 automation must be visible (AUTO/MANUAL).
**C. Evidence & honesty** — E1 classify everything CONFIRMED/STRONGLY SUPPORTED/RECONSTRUCTED/SIMULATION + SOURCE CONFIDENCE · E2 never present reconstructed as confirmed SpaceX · E3 do not guess; say when evidence is confidential/unavailable · E4 simulate don't fake don't omit (else NO DATA/INVALID/N/A) · E5 build pages from reference source (`UI_AUDIT.md`) not screenshots · E6 provenance labels PORT/MEASURED/MY INVENTION.
**D. Numerical safety** — N1 sanitize all mission-critical maths · N2 never render NaN/Inf to the astronaut except as intentional diagnostic; eliminate the `Clamp1` passthrough.
**E. Change discipline** — P1 no mass rewrites (fix the 20%, keep the 80%) · P2 protect proven systems + load-bearing display assets · P3 bounded upstream flight changes (only to expose needed authoritative state or fix a mission-blocker; each gated) · P4 smallest safe change · P5 handle obsolete/duplicate code carefully; no competing brains · P6 no architecture problems hidden in tuning.
**F. Completion & verification** — V1 code exists ≠ works; headless green ≠ flight proven · V2 no fake completion · V3 one change → one verification cycle · V4 touching proven code invalidates its proof until re-flown · V5 evidence via FlightRecorder + flight IDs; don't overfit one flight.
**G. Process, scope & UX** — S1 Phase 0 first · S2 contracts before code · S3 one authoritative doc set, no nesting · S4 complete page inventory before coding pages · S5 fidelity before fun · S6 mission-phase-adaptive UI · S7 astronaut-first, uncluttered · S8 root cause not symptoms · S9 performance (no per-frame rebuilds/allocs/reflection; dirty-state; split high/low-rate) · S10 graceful degradation, minimal deps, not a MechJeb skin · S11 warp is mission-aware.

## 4. The core architectural defect this build eliminates
Two sources of truth today: the **display world** (`plugin/src/VesselData.cs` → `PageState` in `pure/Pages.cs:53`, querying KSP directly and computing its own numbers) versus the **control world** (mission FSM `pure/ModeManager.cs`+`CrewProcedureOps.cs`, controllers, `Authority`, `Fdir`, the private rel-nav filter `NavState3` in `pure/NavFilter.cs:125`). Target flow (rule 0.4/T-block):
```
KSP physics → vehicle state → navigation/guidance/mission/FDIR → AuthorityManager → immutable display snapshot → three Dragon screens
```
The display is a **consumer of truth**, never a second spacecraft brain.

## 5. Authoritative build order (Phase 0–21)
```
0  Baseline/freeze  ·  1  Documents + source-of-truth contracts  ·  2  AuthorityManager (+Clamp1 NaN fix)  ·
3  Authoritative mission/vehicle STATE (extend MissionPhase enum)  ·  4  Display snapshot (re-plumb PageState to READ authoritative state)  ·
5  Telemetry + command registries  ·  6  Screen component system  ·  7  DOCKING gold-standard screen (AUTO/MANUAL + real commands)
8  Three-screen integration  ·  9  Navigation/GNC  ·  10 Mission/Systems  ·  11 FDIR/Alerts/Abort  ·
12 Rendezvous  ·  13 Docking flight verification  ·  14 Deorbit/Entry/Splashdown  ·  15 Booster RTLS  ·  16 Booster ASDS  ·
17 Full end-to-end mission  ·  18 Visual fidelity (proper D-DIN bitmap font)  ·  19 Audio/animation/immersion  ·  20 Easter eggs/comedy  ·  21 Final regression → release
```
**Phase-order rule:** this sequence is authoritative. Propose reordering only with evidence of a technical dependency or unnecessary risk; then stop, document, and get approval before a change that materially alters architecture.
**Current active increment: Phase 0–7** (foundation + the Docking gold-standard reference page). The AuthorityManager is a **behavior-preserving extraction** (Autopilot/Abort identical; add Manual/Recovery; per-vehicle) gated by re-flying ascent + max-Q abort + dual-vessel booster; the `Clamp1` NaN fix and `MissionPhase` enum extension ride the same gate.

## 6. Protected assets (do not rewrite without a demonstrated defect)
Flight-proven: ascent-to-orbit; max-Q launch escape; dual-vessel booster control (`MissionConductor` hooks `booster.OnFlyByWire`, PARTIALLY PROVEN — controlled, not landed). Proven infrastructure: torque attitude loop, FDIR ladder, mission FSMs, `[Tunable]`, dispersion suite, FlightRecorder, RangeExtender isolation. Load-bearing display assets: pure/glue split, `DisplayList` render seam (GL + offline PNG preview), three-display "one screen four surfaces", touch/hitbox "one rect per control", `Gauge`/`Card`/`GateCard`/`Control.Button`/`ChromeBar` widgets + `DragonPalette`/`Typography` tokens. Key code assets to inspect-not-replace: `FlightDriver`, `MissionConductor`, `VesselData`, `ScreenPainter`, `DragonScreenMonitor`, `DragonScreenState`, `ScreenTouch`, `PanelButtons`, `AscentControl`, `RendezvousControl`, `DockingControl`, `AbortControl`, `ReturnControl`, `BoosterControl`, `Fdir`, `FlightRecorder`, `RangeExtender`.

## 7. Verification workflow
`code → pure tests (build.py test) → preview PNG (build.py preview) → KSP integration (build.py install) → flight (recorded, flight-ID) → document`. Restarts are the scarce resource: judge layout from PNGs before spending one. Named campaigns: A Ascent · B Launch escape · C Booster/RTLS/ASDS · D Rendezvous · E Docking · F Return — each with failure-injection cases (TestFlight). One change → one verification cycle. Modifying proven code invalidates its proof until re-flown (V4).

## 8. Companion ACTIVE documents
`SCREEN_SPEC.md` (screen IA, component contract, page inventory, visual language) · `COMPLETION_MATRIX.md` (every subsystem × L1–L5 × status) · `SOURCE_OF_TRUTH.md` (which subsystem owns each state) · `TELEMETRY_REGISTRY.md` (every displayed datum's authoritative source) · `COMMAND_REGISTRY.md` (every interactive command's path + authority) · `SCREEN_EVIDENCE_MATRIX.md` (evidence class + confidence per screen feature) · `FLIGHT_VERIFICATION.md` (flight-ID log + baseline) · `DEPENDENCY_MATRIX.md` (required/environment/optional/unnecessary).

---

## Baseline snapshot (Phase 0, 2026-08-31)
- Repo/branch: `DragonScreen` @ `master`; HEAD `9073429` ("AI plan assessment.", 2026-08-30); working tree clean; tag `Falcon_crew_dragon_auto_pilot_software`.
- Baseline `build.py test`: **PASS** (exit 0) — 26 unit suites (~515 checks) + Tier-2 dispersion (731,239 checks / 100k property cases), 0 failed. L1 only (rule V1). Recorded in `FLIGHT_VERIFICATION.md`.
- Environment: KSP + RSS/RO/RP-1 + TundraExploration (see `DEPENDENCY_MATRIX.md`).

## Change log
- 2026-08-31 — Created from the Master Build Control Directive; reconciled against HEAD `9073429`. Active increment set to Phase 0–7.
