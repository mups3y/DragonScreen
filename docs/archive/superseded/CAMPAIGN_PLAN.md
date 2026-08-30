> **SUPERSEDED — historical reference only, NOT an active instruction.**
> The sole governing specification is `docs/MASTER_BUILD_SPEC.md`. Do not follow this document.
> Quarantined 2026-08-31 (Phase 1 document control). Kept for history; not deleted.

---

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

### ✅ C2 Step-1 — DUAL-FLIGHT PROOF: **GREEN (Tick-3 passed, flight 134620, 2026-08-29)**
Both criteria passed, log+CSV cross-checked: (1) KSP drove the non-active booster's `OnFlyByWire` (`cbEntered=2250`,
body-rate 7.9→36 °/s, loaded+unpacked throughout) → **control reaches it**; (2) with focus retained on the Dragon,
the S2 MVac **lit and burned to SECO → orbit (ap 384 × pe 154, inc 51.65°)** → **the C1 ullage-loss blocker is
fixed by not switching focus.** ⇒ Direction A validated; Step-2 approved-in-principle, **awaiting Grok review before code.**

### ✅ C2 Step-2 — recovery FSM on the non-active booster — *STRUCTURAL GOAL PROVEN (Tick-3, flight `Crew-2_20260829_144114`)*
Fly the separated booster on its **own `OnFlyByWire`** while the Dragon stays active. One change class (Phase-FSM).
Commits `0610609` (refactor: `AttitudeController` instance + `AttitudePilot` facade) + `5394358` (booster non-active FSM).
**TICK-3 RESULT (CSV + KSP.log + screenshots cross-checked):**
- ✅ **Dragon reached orbit — no regression:** SECO **200 × 197.7 km, inc 51.64°** (tgt 51.6, d=+0.04), S2 relit (`14:50:07 SECO`).
- ✅ **Dual control proven:** the non-active booster's OWN OnFlyByWire flew the FSM — **16,139 calls**, EntryBurn→LandingBurn,
  grid fins, engine mode switched **once** (the `FlightDriver:222` one-ignition guard held), attitude live (att.err 105°→6°).
  The static-state-collision blocker is fixed; two vehicles controlled at once.
- ❌ **Booster did NOT land:** `eng_ignited=0` the whole descent → ballistic → LOST @14 km. **Root = RealFuels ullage (the
  §8.6-predicted contingency)** → the NEXT campaign, NOT a defect here. See H1b.
- ⚠ PARKED (not Step-2): rendezvous/phasing thrash drained RCS (MMH/NTO→0) + pe self-deorbited to 149.9 km (C2a).

### ✅ C-INSTR-2 — RECORDER FIDELITY (Instrument class) — *BUILT (Tick-1 green + installed), UNFLOWN — PREREQ for C2 Step-3*
Holes found cross-checking flight 144114 screens/log vs the CSV (`docs/FLIGHT_144114_SCREEN_AUDIT.md`, register R1–R5).
Same Instrument class as C0. Commits `6974bfd` (R2/R3/R4) + `0438205` (R1).
- **R1** — the non-active booster gets its **own CSV** (`BoosterLog` + `BoosterControl.FillRecorderRow`), same schema
  → `assess_flight.py` reads it. Carries the H1b-critical columns: `eng_ignited` (did the octaweb light?),
  `ullage_stab` (settled ullage at the light?), throttle, the full attitude loop, reentry skin-temp.
- **R2** — `PutFdir` is now called every sample (it had NO call-site → `fdir_fault/recovery/abort` were dead while
  KSP.log logged 10+ faults). `FlightDriver.LastFdirReport` exposed.
- **R3** — `mmh_frac`/`nto_frac` columns (the return-propellant drain that ends a mission was CSV-invisible).
- **R4** — `skin_temp_frac` column + hottest-part log (the max-Q "Overheat!" was CSV-invisible).
**Tick-3 (fly): confirm the booster CSV appears with eng_ignited/ullage_stab, fdir_fault populates, mmh/nto track the
drain, skin_temp catches the overheat.** THEN C2 Step-3.

### ▶ C2 Step-3 — booster ignition — ⭐ **MEASUREMENT DONE (flight 161224): ROOT B confirmed — plan PIVOTS**
**The C-INSTR-2 flight answered the gate: it is NOT ullage (root A is DEAD).** The booster CSV shows `ullage_stab`
**~1.0 (settled)** through the whole entry burn with `throttle=1`, the octaweb Activated, and fuel present (KER 3,797 m/s
Δv, 14.66 TWR available) — yet `eng_ignited=0`. So **the settle-then-light plan (§8.4 below) is DISPROVEN — do NOT build
it.** The real root is **off-focus RF ignition** (register H1b): a non-active vessel's `ModuleEnginesRF` won't combust.
Plus **H1c**: the booster's cold-gas RCS is nearly empty (Δv ~1 m/s) → it tumbles (att_err 40–144°) with no engine gimbal.

⚠ **The deeper architectural conflict this exposes** (the honest blocker): the booster's entry burn is needed at ~T+4
(on the way down), but the Dragon's S2 insertion runs until SECO ~T+8:53 — so the booster's burn window OVERLAPS the
window where the Dragon must stay active. The booster needs FOCUS to fire its engine (root B), but switching focus
during insertion reintroduces the C1 ullage-loss on the Dragon's S2. **So "recover the booster while the Dragon inserts"
may not be solvable by ignition alone.** Options to research/decide BEFORE the next §8 (do not build yet):
- (i) research RealFuels: is ignition/thrust truly gated on `isActiveVessel`, or is there an off-focus path we're missing?
- (ii) sequence differently: can the booster's entry burn be DEFERRED until after the Dragon reaches orbit (accept a
  lower/late entry burn), so a post-SECO focus-switch to the booster is safe (S2 already done)?
- (iii) accept booster recovery is out-of-scope while crewed-insertion is active, and bank the Step-2 dual-CONTROL win.
This is a **design decision for Chris + research**, not a code change. The §8 below is kept for the record but its
root-A plan is void; a fresh §8 follows the decision.

---
_(superseded draft — root A, kept for the record)_
### C2 Step-3 (was) — booster ullage + ignition on the non-active vessel — §8 DRAFT (measurement-gated)

#### §8.1 State Confirmation (verified by reading, file:line, 2026-08-29)
- **The Dragon settles ullage before every S2 light, and it WORKS** (S2 relights reliably): `AscentControl.cs:173–214`
  runs an ignition CYCLE — settle@throttle-0 by firing the **aft RCS** `FlightDriver.SetTranslation(0,0,-1)` to seat the
  propellant (185), read `Ullage.Stability(Actuator.FindEngine(v, SecondStage))` (186), and after `S2SettleS` LIGHT via
  `Actuator.IgniteSecondStage` (208); if no SUSTAINED thrust it RESETS and re-settles (the 173–178 comment is the
  RealFuels "lit into vapor → 0 thrust" lesson).
- **The booster does NONE of this.** `BoosterControl.SelectEngineMode` just calls `e.Activate()` on a mode change — no
  settle, no ullage read, no re-light cycle. On flight 144114 the octaweb `eng_ignited=0` the whole descent → LOST (H1b).
- **The non-active booster's write path is its own `FlightCtrlState s`** (proven in Step-2): attitude `s.pitch/yaw/roll`
  + throttle `s.mainThrottle` reach it. Translation `s.X/s.Y/s.Z` is the same channel → an aft-settle for the booster is
  `s.Z` (sign TBD), NOT `FlightDriver.SetTranslation` (that targets the active Dragon).
- **C-INSTR-2 (R1) now records the booster** — `ullage_stab`, `eng_ignited`, `throttle`, `trans_*` per tick in its own
  CSV → the flight FINALLY shows the ullage state at the light attempt (the measurement this campaign is gated on).
- ⚠ **Open physics the flight must settle:** (a) does the booster's cold-gas RCS have TRANSLATION authority, or attitude
  only? (b) engine-first atmospheric descent decelerates the booster → the pseudo-force pushes propellant toward the NOSE
  (away from the feed) → ullage UN-settled unless actively pushed aft (so a settle is genuinely needed). (c) does a
  non-active vessel's `ModuleEnginesRF` even COMBUST once ullage is settled, or does RF gate ignition on active-focus?

#### §8.2 Diagnosis (two candidate roots — the R1 data disambiguates; do NOT guess before the flight)
`eng_ignited=0` = the octaweb was Activated but never combusted. Either **(A) Unsettled ullage** (most likely — matches
the Dragon needing a settle + the drag physics): `ullage_stab` reads LOW at the light → build the settle-then-light cycle;
or **(B) Off-focus RF ignition gate**: `ullage_stab` reads HIGH but `eng_ignited`=0 → RF won't combust a non-active
vessel's engine regardless of ullage → a DIFFERENT fix (§8.6), NOT the settle.

#### §8.3 Change class — **Actuator-ignition** (make the booster's octaweb light). One class. Do NOT retune the descent
FSM (BoosterDescent timing/AoA) until it lights — moot before ignition, and would mix classes.

#### §8.4 Edit plan (PRIMARY = root A; build ONLY after the flight confirms A)
1. **`BoosterControl` — add a settle-then-light cycle to the non-active octaweb ignition**, mirroring `AscentControl`'s
   proven S2 cycle but writing into the booster's own `s`: before lighting a new octaweb mode, run a settle phase —
   `s.Z = BoosterSettleDir` (aft push; sign a `[Tunable]` confirmed from the flight's `ullage_stab` response) at throttle
   0, read `Ullage.Stability` on the target-mode octaweb engine, and only when stable (the same threshold the Dragon uses)
   `e.Activate()` + raise `s.mainThrottle`; if no SUSTAINED thrust after a window, reset → re-settle. Kept inside
   `Fly(v,s)`/`DriveNonActive` so it stays on the booster's ctrlState.
2. Reuse the PURE `Ullage` + the same settle/light timing constants as AscentControl (don't re-derive) — a PORT of the
   Dragon's working cycle to the booster, not an invention.
3. Instrument already in place (R1) → the cycle is self-diagnosing.

#### §8.5 Verification
- **Tick-1:** `build.py test` green (glue cycle; pure `Ullage` already tested).
- **Tick-3 (exit):** on the booster CSV, `ullage_stab` RISES during the settle AND `eng_ignited ≥ 1` at the entry burn
  (then the landing burn). Re-run the scorecard; H1b closes when it lands within a stated bound.

#### §8.6 Contingency
- **Root B (settled but won't light off-focus):** `ullage_stab` high yet `eng_ignited`=0 → NOT a settle fix — candidates:
  a brief FORCE-FOCUS to the booster for the ignition instant then hand back, or an ignition path RF honours off-focus.
  RESEARCH the RealFuels source first (does `ModuleEnginesRF` gate combustion on `vessel.isActiveVessel`?). New §8; no guessing.
- **No booster translation RCS:** settle by holding the booster PROGRADE (engines-aft to the airflow) so drag seats the
  propellant at the feed, or enable a dedicated ullage thruster — decide from the craft dump.
- Any glue fault is caught by `DriveNonActive`'s try/catch → logs, carries on.

#### §8.1 State Confirmation (what the code actually is — verified by reading, file:line, 2026-08-29)
- `AttitudePilot` (`AttitudePilot.cs`) is a **`static` class**: the PID/smoothing/lag state — `posPid[]`/`velPid[]`
  (29–30), `smTx/smTy/smTz`+`smInit` (37–38), `actEst[]` (45), `rcsFallbackLogged` (261) — is **shared static**, and
  `Point()` writes the result through `FlightDriver.SetAttitude/SetAttitudeRoll` (151–153), which set static channels
  that `FlightDriver.OnFlyByWire` applies to the **bound = ACTIVE vessel only** (`FlightDriver.cs` Bind 96–101, apply
  150–159). So the whole write-path targets the Dragon; it cannot drive a second vessel.
- `BoosterControl` (`BoosterControl.cs`) already holds the **full recovery FSM** (flip→entry-burn→aero→hoverslam→legs)
  but is written for the **active** vessel: it steers via `Steering.Point` (130) → the static `AttitudePilot`, and sets
  throttle via `FlightDriver.SetThrottle` (149/165) — both active-only sinks.
- `MissionConductor.TickBoosterRecovery` (`MissionConductor.cs` 143–199) keeps the Dragon **active** and hooks the
  **non-active** booster's own `OnFlyByWire` with the Step-1 **proof** (`ProofFlyByWire` 265–269 = a constant pitch).
  Step-1 PROVED KSP drives that callback (`cbEntered=2250`, body-rate 7.9→36 °/s).
- ⚠ **Hazard found:** `FlightDriver.cs:219` calls `BoosterControl.Reset()` **every frame the Dragon is active** — which
  is exactly when a non-active recovery runs. Un-guarded, it wipes the FSM state each frame (re-igniting a mode every
  tick → violates one-ignition-per-mode). Must be guarded.
- All booster-side `Actuator` verbs act **directly on part modules** (`e.Activate()`, `wd.EventToggle()`,
  `r.rcsEnabled=true`, `SetGroup`) — non-active-safe. `EnableRcs` sets `ActionGroups[RCS]` (500), which is what
  `AttitudePilot.ControlTorque` reads (line 179) to count the cold-gas authority. Confirmed on the non-active booster
  in Step-1 (it developed a body-rate).

#### §8.2 Diagnosis
Two vessels need two independent attitude loops, and the booster's commands must land in the **booster's own
`FlightCtrlState`**, not the Dragon's active channels. The FSM logic already exists; what's missing is (a) per-vehicle
loop state and (b) a booster write-sink.

#### §8.3 Change class — **Phase-FSM** (fly the existing recovery FSM on the non-active booster). The enabling
attitude-loop refactor is **behaviour-preserving** (a facade delegating to identical logic), so it introduces no second
behavioural change — landed as its own commit with the Dragon path provably unchanged.

#### §8.4 Edit plan (concrete, file by file)
1. **NEW `src/AttitudeController.cs`** — the stateful loop extracted **verbatim** from `AttitudePilot` as an
   **instance** (per-vehicle `posPid/velPid/smTx.../actEst/rcsFallbackLogged` + diagnostics). `Compute(v,dir,dampRoll,
   rollUpRef)` returns an `AttitudeCmd{Pitch,Yaw,Roll,HasRoll}` instead of writing to `FlightDriver`; the `!dampRoll`
   roll-PID reset stays internal. Reuses the pure `AttitudeLoop.Axis` unchanged; reads the shared `[Tunable]`
   `AttitudePilot.UseLagComp`/`RcsTorqueFloorNm`.
2. **REWRITE `src/AttitudePilot.cs` → thin static facade** over one default `AttitudeController active` for the Dragon.
   Every existing static entry (`Point`, `Reset`, `ResetIntegrators`) + every diagnostic the callers read
   (`PitchAccelRadS2`, `ActPitch/Yaw/Roll`, `PointErrDeg`, `RateCmdRads/RateMeasRads`, `CtrlTorque*Nm`) forwards to
   `active`. `Point` does the same `FlightDriver.SetAttitude/…` writes it does today → **Dragon path byte-identical**.
   (Callers that must keep compiling: `Steering.cs:113/134`, `FlightDriver.cs:197/498/624-628/741`,
   `FlightLog.cs:99-122`, `AscentControl.cs:273/631`.)
3. **EDIT `src/BoosterControl.cs`** — add its own `AttitudeController att`. Split throttle **out** of `ApplyEngineMode`
   (mode-select only; the caller applies throttle to the right sink). Add `DriveNonActive(booster, s)`: same guidance
   (`BoosterDescent.Guide`) + engine-mode/fins/legs, but attitude via `att.Compute(...)` → `s.pitch/s.yaw/s.roll` and
   throttle via **`s.mainThrottle`** (this IS the literal C1 "throttle never raised" fix). Skip `FlightLog.Fill` on the
   non-active path (the Dragon owns the CSV); emit a rate-limited KSP.log line (phase/alt/vspeed/mode/throttle/lit).
4. **EDIT `src/MissionConductor.cs`** — replace the proof: `Armed` hooks `BoosterRecoveryDrive` (→
   `BoosterControl.DriveNonActive`) instead of `ProofFlyByWire`; `FlyingBooster` runs until the booster lands/splashes/
   is destroyed or a timeout, then unhooks + `RangeExtender.Disable()` + Done. Drop the proof-only `ProofPitchCmd`;
   keep a `RecoveryTimeoutS` backstop. Expose `BoosterRecoveryActive`.
5. **EDIT `src/FlightDriver.cs:219`** — guard the per-frame `BoosterControl.Reset()` with
   `if (!MissionConductor.BoosterRecoveryActive)` so the non-active FSM state survives the Dragon's frames.

#### §8.5 Verification
- **Tick-1:** `python build.py test` green — this compiles ALL of `src/` (glue included) against KSP + runs the ~900
  pure checks. The Dragon-path behaviour-preservation is **structural** (facade → identical logic), not a new headless
  test — stated honestly, proven at Tick-3 by an ascent identical to Step-1's (Dragon still reaches orbit).
- **Tick-3 exit conditions (both):** the Dragon **still reaches orbit** (S2 relights — the Step-1 win is not regressed)
  **AND** the booster's own `OnFlyByWire` flies the FSM: `eng_ignited ≥ 1` logged at the entry/landing burn and it
  descends toward the deck. Re-run `INTEGRATION_SCORECARD.md` afterward.

#### §8.6 Contingency
- If the booster's engine **won't light** on the non-active vessel (`eng_ignited`=0) → that's the next campaign
  (**Actuator-ignition class**: RealFuels ullage-settle before the light — NOT smuggled into this Phase-FSM change).
- If `v.radarAltitude` reads unreliably for the non-active booster → fall back to `alt = CoM-to-body` (watch item).
- Any glue fault is caught by the existing per-tick try/catch → logs + carries on, never taking the Dragon down.
- Revert point: the behaviour-preserving refactor is a separate commit; the wiring can be reverted without it.

### (old Step-1 plan, now superseded by the GREEN result above)
### ▶ (was) C2 — MECO booster-recovery hand-off — Step-1 DUAL-FLIGHT PROOF
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
