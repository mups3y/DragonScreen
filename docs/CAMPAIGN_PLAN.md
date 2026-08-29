# DragonScreen — Campaign Plan (the anti-drift track)

> The ordered execution of everything agreed with Grok under the revised **Operating Instruction**. This is the
> persistent handoff (§9). **Read this + `docs/ISSUE_REGISTER.md` at the start of every session.** One campaign =
> one change class. Do NOT reorder. Do NOT start a campaign before the one above it passes Tick 3 (flown), except
> where a campaign is explicitly "build-only, Tick-3 deferred".

## Standing rules (every campaign, no exceptions)
- **§8 output BEFORE code:** State Confirmation → Diagnosis → change class → edit plan → verification → contingency.
- **One change class per campaign** (Instrument / Prediction / Reference-trajectory / Phase-FSM / Actuator-ignition-staging / Contingency). Mixing = forbidden.
- **Evidence:** until C0 lands, CSV + KSP.log are **co-equal**, cite both. Every "fixed" cites columns + flight file + log lines.
- **3-tick:** Tick1 build+headless-green · Tick2 Chris-approved · Tick3 proven in-game. Nothing "done" before Tick3.
- **Anti-tunnel (§9) before every edit;** breadth (conductor + phases before/after) before depth.
- Update THIS file + `ISSUE_REGISTER.md` + the persistent handoff note at the end of every campaign.

## Ranked campaigns

### ✅ C0 — INSTRUMENT FIDELITY (P0.0) — *PASSED (I1/I3 verified in-flight; I2 column proven, booster-focus deferred to C1)*
**Goal:** make it impossible to diagnose a nonexistent thrash from warped rows, and provable whether an engine ignited. **Class: Instrument.**
- [x] `pure/FlightRecorder.cs`: `warp_rate`, `eng_ignited`, `eng_flameout` cols + `PutInstrument` + pure `ZeroControlColumnsForWarp`.
- [x] `FlightLog.cs`: stamp `warp_rate`; on on-rails warp (`WarpMode==HIGH && CurrentRateIndex>0`) blank the delivered/measured control columns; record main-engine `EngineIgnited`/`flameout` counts.
- [x] `Actuator.cs`: rate-limit the S2 ignition log (was 1,246× spam).
- [x] `tools/assess_flight.py`: `is_warp()` helper; §8 control-authority stats exclude `warp_rate>1` rows.
- [x] **Tick1 DONE** — `build.py test` green (731,271 + new warp-zero/instrument checks). Committed `bd131f3`.
- [x] **Tick2 DONE** — Grok reviewed + signed off (blank/keep lists correct, warp gate correct, eng-state sufficient). Fly as designed.
- [ ] **Tick3 — Chris flies. Required profile (Grok-refined):**
      1. Ascent to orbit (realtime rows + clean MECO/S2/SECO).
      2. **An intentional on-rails warp interval ≥30–60 s** in orbit/coast (so the blanking + `warp_rate` are inspectable). A short physics-warp (LOW) segment too is a bonus (proves those rows stay LIVE).
      3. **One booster focus-switch that includes an ignition attempt** (or the normal recovery that should attempt it).
      No full landing / long rendezvous needed for C0.
- **First analysis step after the flight (do this BEFORE anything else):** `warp_rate` histogram + a side-by-side of one realtime row vs one warped row for the blanked columns. Then check `eng_ignited`/`eng_flameout`/`thrust_n`, then confirm the ignition log didn't spam.
- **DoD:** warp rows unmistakable + control columns blank there; booster ignition state visible; log quiet. Record the Tick-3 result (pass/fail on I1–I3) in `ISSUE_REGISTER.md` + the handoff note **before** any C1 work.

### ✅ C1 — INTEGRATION SCORECARD (P0) — *DONE (2026-08-29, Grok-approved plan)*
Evidence-only; `is_warp` fixed to key on blanked-control (physics-warp rows kept). Scorecard: `docs/INTEGRATION_SCORECARD.md`
(stitched by wall-clock+log with provenance; the MECO focus-switch scored as an explicit row).
- **Earliest failed exit condition = the MECO → booster-recovery hand-off (12:06:40).** It breaks BOTH branches:
  the S2 MVac won't relight after the ~6-min booster-recovery coast (`0 engine(s) lit` + FDIR NoControlSolution → no
  insertion), and the booster's own engines never ignite (`eng_ignited`=0 through entry+landing → crash @119 m/s).
- Mission B (`121530`) is a SEPARATE deorbit test (MET discontinuity) — deorbit didn't complete; parked.

### ▶ C2 — MECO booster-recovery hand-off — Step-1 DUAL-FLIGHT PROOF (Tick-1 done, commit de6d487; awaiting Tick-3)
**Grok-reviewed, attitude-first.** No focus switch: Dragon stays active (S2 gets its continuous insertion); the
non-active booster is driven via its own `OnFlyByWire` with a constant pitch command; log `cbEntered` + body-rate.
**Go/no-go (both required):** (1) booster shows a clear commanded body-rate response (> ~2 °/s, > noise) AND
`cbEntered`>0; (2) Dragon stays active + S2 lights + does a meaningful insertion burn. GREEN → Step-2 (full FSM on
the non-active booster). `cbEntered`=0 → RED (KSP doesn't drive it under this PRE). Report log evidence, STOP for review.

### (superseded scope note) — MECO booster-recovery hand-off ignition failure
**Scope (the only thing C2 may touch):** why neither the S2 MVac (after the coast) nor the booster octaweb ignites at/
after the MECO hand-off. **Diagnose before choosing the class** — likely candidates: RealFuels **ullage** not settled
before the light on either vehicle (Actuator/ignition class), OR the **orchestration ordering** (recover the booster
*after* S2 insertion, not at MECO — Phase-FSM class). Measure first (do the booster + S2 have ullage at the light
attempt? is the octaweb even commanded on?), THEN one single-class fix. Old C2a–C2d (prediction) move down behind this.

### C2 — PREDICTION FIDELITY (P1)
**Goal:** the predictor prevents errors the online law is currently asked to correct. **Class: Prediction model** (one sub-target per campaign — do NOT batch these):
- **C2a — pe-drop burn-vector instrument + fix:** log every RCS/engine pulse's Δv in the orbital frame (radial/along/normal) + anomaly + pe/ap before/after (Grok: a pure prograde burn cannot lower pe → there is a radial/retrograde leak or premature CW activation). Measure first, then fix the single cause.
- **C2b — booster impact prediction + grid-fin divert** (historic multi-10 km miss).
- **C2c — entry-burn Δv sizing** (OPEN assumption — measure vs real Crew-2 bleed).
- **C2d — launch-window / rendezvous timing residual.**

### C3 — FROZEN BASELINE REFRESH (P2)
**Goal:** a **flight-state** craft dump (current one is editor-state, engine-thrust reads 0). Reconcile every pure constant + SelfCal input to it. **Class: Reference/baseline.** (This is T13.)

### C4 — OFFLINE REFERENCE GENERATION (P3)
Ascent pitch program + booster entry/landing targets validated against offline trajectories respecting max-Q/g-limit/fuel. **Class: Reference-trajectory.**

### C5 — CONTINGENCY SURFACE (P4)
Map every abort/FDIR branch to recorder columns; ensure each leaves a clear signature and is exercised. **Class: Contingency.**

### C6 — LOCAL POLISH (P5)  — *only after all above*
Small gain / logic fixes. Includes the deferred, low-priority items:
- **RCS authority flicker** → `max(reported, geometric)` with hysteresis (Grok-endorsed; latent, realtime control is fine).
- **RemoteTech duplicate-key watch** during PRE dual-flight (mod interaction — watch, don't chase).
- **Align-then-warp (T14)** in-position-before-burn (Phase-FSM class — schedule as its own campaign when reached).

## Parking lot (do NOT start; tracked so they aren't lost)
Booster dual-flight PRE robustness (H1) · docking KOS/IDSS flight-verify · deorbit/entry/departure sign resolution · g-cap (F5/G7) · the flag-gated wires already built (T2b/T4/T5/T6) awaiting their phase's flight.
