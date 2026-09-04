# EXTRACT — `EntrySteering.cs` / `pure/Entry.cs` (deleted gen-2) → the lifting-entry L/D prior and the bank/footprint method

> **[FROM DELETED GEN-2 `EntrySteering.cs` / `pure/Entry.cs` — REFERENCE ONLY, NEVER LIVE]**
>
> Produced by register task **W16** (Wave E-4), 2026-09-04, under the owner's G6 re-verdict of 2026-09-04
> (*"we use MechJeb for ALL UPPER STAGE MANOEUVRES as planned"*): `W16` was re-verdicted
> **RECOVER-CODE → RECOVER-REFERENCE**. **No `.cs` file was created, restored or edited by this task, and
> this line parks no state anywhere.**
>
> ⛔ **Entry is MechJeb's, and this file is the thing the owner already declined for flight 1.** §B9 Phase 8
> under **O8** (owner, 2026-09-03, via the overseer) is **SmartASS `SURFACE_RETROGRADE` attitude hold, NO
> active steering** — plain ballistic, heat-shield forward, as the **baseline**; active steering is added
> **ONLY LATER**, for off-target cases, as a future increment. `EntrySteering.cs` + `pure/Entry.cs` **are**
> that active steering. Nothing here becomes live.
>
> ⛔ **NO LIFTING ENTRY HAS EVER BEEN FLOWN** (R1 §5.1). `EntrySteering.cs` is flown **❌ NO**;
> `FLIGHT_CORPUS_ASSESSMENT.md` §5: *"Nothing about entry, chutes or splashdown. No `entry_phase` … anywhere
> in the corpus."* **Every number below is UN-CONVERGED for RSS-RO** (§B16.8 ruling 2) and stays so until a
> recorded flight says otherwise — which needs glass time, a **SEPARATE owner gate** this task does not have
> and cannot open (C1.12).
>
> ⛔ **This document is EVIDENCE, not an instruction.** `docs/BUILD_PLAN.md` wins on any conflict (C7.1).

**Read with:** `docs/BUILD_PLAN.md` §B9 Phase 8 + **O8**, §B10.4 (Landing Guidance), §B10.5 (SmartASS),
§B11, §B12.5a, §B12.8, §B16.8 ruling 2 · `docs/MECHJEB_MISSION_TUNING.md` §7.2 ·
`docs/AUTOPILOT_RECOVERY_AUDIT.md` (R1) §5.1–§5.2, §7.4 · `plugin/src/pure/Trajectory.cs:360-395` (in the
tree — the 4-band schedule and W22's marking) · `docs/EXTRACT_RETURN_CONTROL.md` (W18 — where the bank loop
sat, and where this state was retired to).

---

## 0. What was read, and what it was

| File (at `8b81816^`) | Size | R1 verdict | Regime | Flight status |
|---|---|---|---|---|
| `plugin/src/EntrySteering.cs` | 9,687 B | RECOVER-CODE | **L/D prior** | ❌ **NO** |
| `plugin/src/pure/Entry.cs` | 10,109 B | RECOVER-CODE — *"L/D bands = marked prior"* | — | ❌ **NO — no lifting entry ever flown** |

**Architecture in one line.** `pure/Entry.cs` is the pure bank-angle guidance law (|σ| flies downrange,
sign(σ) reverses on a crossrange deadband — the Apollo/Orion/Shuttle method); `EntrySteering.cs` is the glue
that supplies its two live inputs — the **predicted footprint error** from the lift-aware impact predictor,
and the **measured bank angle** — plus a measured ballistic coefficient.

---

## 1. ⭐ The L/D prior — where each piece actually lives

⚠ **Read this section heading carefully; the register line has already been corrected once on this point.**
The prior is in **three** places, and only one of them is a 4-band schedule.

| Piece | Where it lives | Status |
|---|---|---|
| The **4-band L/D schedule** (`LdAtmosEntry` … `LdFinalApproach`) | ✅ **in the tree**, `plugin/src/pure/Trajectory.cs:377-380` — **not** in `pure/Entry.cs` | ⛔ marked `[UN-CONVERGED]` in place by **W22**, 2026-09-04 (`4715942`) |
| The **L/D envelope + full-offset anchor** | ⛔ deleted, `pure/Entry.cs` | ⛔ **UN-CONVERGED, and it never carried a marking** — §1.2 |
| The **predictor's fixed L/D tunable** | ⛔ deleted, `EntrySteering.EntryLoverD = 0.2` | ⛔ **UN-CONVERGED**, self-marked *"FIRST CUT (validate in flight)"* |

### 1.1 The 4-band schedule, verbatim, band boundaries included

⚠ **This lives in the working tree, so it is quoted here for the record, not recovered.** Source:
`plugin/src/pure/Trajectory.cs:377-395`, derived from `AUTOPILOT_MINING_3` §2c (the four bands the
Trajectories mod uses), keyed on the **atmosphere-depth ratio** `alt / atmosphereDepth`:

| Band | Depth ratio | Value | Band centre used for the Lerp |
|---|---|---|---|
| `LdAtmosEntry` | **50–100%** (thin air, hypersonic) | **0.18** `[UN-CONVERGED]` | **0.75** |
| `LdHighAltitude` | **25–50%** | **0.20** `[UN-CONVERGED]` | **0.375** |
| `LdLowAltitude` | **5–25%** (dense, near peak L/D) | **0.26** `[UN-CONVERGED]` | **0.15** |
| `LdFinalApproach` | **< 5%** (subsonic terminal) | **0.24** `[UN-CONVERGED]` | **0.025** |

`EntryLdBand(altRatio)` clamps the ratio to [0, 1] and **linearly interpolates between band centres**:
`r ≥ 0.75` → `LdAtmosEntry`; `r ≥ 0.375` → Lerp(0.375→0.75, HighAltitude→AtmosEntry); `r ≥ 0.15` →
Lerp(0.15→0.375, LowAltitude→HighAltitude); `r ≥ 0.025` → Lerp(0.025→0.15, FinalApproach→LowAltitude);
below that, `LdFinalApproach`.

⛔ **Its status, in W22's words and R1 §7.4's:** *"UN-CONVERGED FOR RSS-RO (§B16.8 ruling 2). R1 §5.1: no
lifting entry has ever been flown, so a value inside the 'Dragon L/D 0.18–0.27 envelope' is **a prior, not a
measurement** — honestly self-marked in the prose … Re-converge from a recorded RSS-RO lifting-entry flight
before trusting the schedule for a commanded divert (ruling 3 — needs glass time, a SEPARATE owner gate)."*
It is also **predictor-model only**: it does **not** command the CoM shifter, and a live measured L/D
(`MeasureAero`) overrides it where available. Today nothing live consumes it —
`BoosterDescent.cs:463-464` sets `UseLdBand = false` — so it is a marking, not a live wrong number.

### 1.2 ⛔ `pure/Entry.cs`'s own copy — a prior that never carried a marking

The deleted `pure/Entry.cs` holds a **different** statement of the same prior, and **it has no
`[UN-CONVERGED]` tag anywhere**. Transcribed with its caveat attached:

- **The aerodynamic claim, from the header:** *"Crew Dragon is a **BLUNT LIFTING BODY**. An OFFSET (radially
  displaced) centre of mass gives it a natural **trim angle of attack ~12°** and **lift-to-drag L/D ≈
  0.18–0.27**"*, cited to `PHASE_6_DEORBIT_ENTRY_SPLASHDOWN_RESEARCH` §3/§5b. ⛔ **The ~12° AoA and the
  0.18–0.27 envelope are UN-CONVERGED for RSS-RO — no lifting entry has been flown, so neither has been
  measured in this install.**
- **The full-offset anchor, in code:** `OffsetPercentFor()` declares `const double fullLoverD = 0.20;` —
  *"L/D at `offsetPercent` = 1.0 (`DescentModeCoM`)"* — and scales the CoM offset linearly,
  `p = targetL/D ÷ 0.20`, clamped to [0, 1], defaulting to **full offset**. ⛔ **This 0.20 is
  UN-CONVERGED**, and the **linearity of L/D in `offsetPercent` is itself an unverified modelling
  assumption**, not a measurement.
- **The glue's predictor value:** `EntrySteering.EntryLoverD = 0.2` — *"offset-CoM lift-to-drag for the
  predictor"*. ⛔ **UN-CONVERGED**; the file's own header lists it first among *"⚠ FIRST CUT (validate in
  flight): the ballistic-coefficient + L/D used…"*.

⚠ **Consistency note, recorded rather than resolved:** `Entry.cs`'s single 0.20 anchor and
`Trajectory.cs`'s four bands (0.18 / 0.20 / 0.26 / 0.24) are two different models of the same quantity, and
`EntrySteering` fed the predictor a **fixed** `LiftToDrag = 0.2` rather than setting `UseLdBand`. So the
deleted stack **never actually used the 4-band schedule** — the schedule was a prior for bands not yet flown,
and the live path used one flat number. Nothing here reconciles them; the plan does not depend on either.

### 1.3 The marking idiom, for whoever converges this

Match `pure/CourseCorrect.cs`'s idiom exactly — a **header block** stating the un-converged status and its
reason, plus **in-place `[UN-CONVERGED]` comments** on each `[Tunable]` line — which is what
`pure/Trajectory.cs:367-380` already does (it follows `pure/GridFin.cs`). **Do not invent a second idiom.**
⛔ **A build chat cannot converge any of these numbers**: convergence needs a recorded RSS-RO lifting-entry
flight, which needs glass time — a **separate owner gate** (C1.12).

---

## 2. The footprint and bank-measurement method — one paragraph each

Enough that a later off-target increment can rebuild it without re-reading 9,687 B of glue.

### 2.1 The predicted footprint error

`EntrySteering` measures a **ballistic coefficient** live from the felt drag — sample only inside the
atmosphere above 50 m/s, take `ρ = body.GetDensity(GetPressure(alt), GetTemperature(alt))` and drag
acceleration `= v.geeForce × 9.80665`, feed `Trajectory.BallisticCoefficientFrom(ρ, speed, dragAccel)`, and
low-pass it with `Trajectory.SmoothBc(…, BcFilterTauS)` — then runs the **lift-aware L1 impact predictor**
(`Trajectory.Solve`) from the current position and orbital velocity with that smoothed Bc, `LiftToDrag =
EntryLoverD` and **`BankRad` = the bank being assumed** (normally `LastSigmaRad`, the previous tick's
command, since *"the predictor assumes the COMMANDED bank next tick"*). The inertial impact point is then
**rotation-corrected into the current body-fixed frame** — rotate by `−BodyRotationRad` about the body's spin
axis, because the planet turns measurably under a long entry — and the miss `(impact − target)` is decomposed
in the **ground-track frame**: `downHat` = the horizontal component of surface velocity, normalised;
`crossHat = up × downHat`; `downErr = err · downHat` (+ = LONG/overshoot) and `crossErr = CrossSign ×
(err · crossHat)`. The target is either the selected open-water splashdown site
(`SetSplashTarget(lat, lon)` → `GetWorldSurfacePosition(lat, lon, 0)`, handed in by the return controller's
`LandingSiteScan`) or, failing that, the KSP target object's transform. ⭐ **The prediction core is
side-effect free**, which is what let the same routine be re-run at an *arbitrary* bank
(`PredictDownErrAtBank`) to get a finite-difference `d(downrange)/d|σ|` for the B8/T6 Newton correction
without clobbering the live footprint state.

### 2.2 The measured bank angle

Bank is the vehicle's roll about the **velocity** axis, measured against a **lift-up reference**, using the
same *0 = lift-up* convention as the guidance's σ. Build an orthonormal frame at the vehicle:
`velHat` = surface velocity normalised (bail below 1 m/s); `liftUp` = the local radial-up with its
along-velocity component removed, normalised; `liftRight = velHat × liftUp`. Take the body's roll reference
as `RollRefSign × ct.forward` (the control transform's forward axis), project out its along-velocity
component and normalise to `refPerp`. Then **`bank = atan2(refPerp · liftRight, refPerp · liftUp)`**. The
roll loop (in `ReturnControl`, `EXTRACT_RETURN_CONTROL.md` §1 step 12) drove `RollSign × RollKp ×
wrap(σ_cmd − bank)`.

⚠ **Four signs in this stack were never validated**, and the file says so: *"the rotation-correction sign,
the down/cross-range signs into `Entry`, and the bank-measurement sign / body roll-reference axis (a constant
roll offset shows as a steady-state bank error — a one-constant fix)"*, plus `RollSign` on the loop itself.
⛔ **A sign error here does not fail loudly** — it steers the footprint the wrong way, exactly as the docking
servo's lateral sign did (`EXTRACT_DOCKING_CONTROL.md` §4). Any rebuild reads them off a recorded flight
before trusting them. And: *"No target → nominal reference bank (still a stable lifting entry, no precise
footprint control)"* — a safe degradation worth keeping.

### 2.3 The bank-angle guidance law, and its constants — ⛔ all UN-CONVERGED

| Name | Value | Status |
|---|---|---|
| `RefBankDeg` | **60.0°** | ⛔ UN-CONVERGED — nominal entry bank; `cos 60° = 0.5` vertical lift, leaving authority to lengthen or shorten |
| `MinBankDeg` | **15.0°** | ⛔ UN-CONVERGED — *"never fully lift-up (keeps roll authority)"* |
| `MaxBankDeg` | **105.0°** | ⛔ UN-CONVERGED — *"past 90° = lift-down (max range reduction)"* |
| `BankGainDegPerKm` | **4.0 °/km** | ⛔ UN-CONVERGED — |σ| change per km of predicted downrange error |
| `CrossDeadbandBaseM` | **5,000 m** | ⛔ UN-CONVERGED — floor of the crossrange deadband |
| `CrossDeadbandPerMps` | **3.0 m per m/s** | ⛔ UN-CONVERGED — the deadband widens with speed |
| `CrossSign` / `RollRefSign` | **+1.0 / +1.0** | ⛔ UN-CONVERGED **and explicitly unverified** — *"flip if …"* |

**The law:** `|σ| = clamp(RefBankDeg + BankGainDegPerKm × downErr_km, MinBankDeg, MaxBankDeg)` — predicted
**LONG** → more bank → shorter; predicted **SHORT** → less bank → longer. `sign(σ)` holds its previous value
inside the deadband `CrossDeadbandBaseM + CrossDeadbandPerMps × speed` (hysteresis, no chatter) and flips to
oppose the error when it breaches — **the S-turn bank reversals**. Phases: `PreEntry` (above the interface —
shield forward, CoM shifter engaged, σ = 0) → `Entry` (active banking) → `Descent` (at/below the drogue
altitude — stop banking, hand to the chutes).

---

## 3. ⛔ The CoM shifter — the one owner-sourced hard rule in this file

Recorded verbatim, because it is a §1.4 owner constraint on how a real part is used and it survives the move
to MechJeb unchanged:

> *"**USE THE CoM SHIFTER CORRECTLY (user, explicit).** The offset CoM is the `AdjustableCoMShifter` part
> (craftdump: EVENT `"ToggleMode"`/`"Turn Descent Mode On"`, ACTION `"Toggle"`, `DescentModeCoM=(0,0,0.2)`,
> `offsetPercent` 0..1 sets the magnitude → the L/D). The correct use:
> 1. Engage **DESCENT MODE ONCE**, before Entry Interface (heat-shield-forward). It is a **MODE, not a
>    steering actuator** — turned ON for entry and left on. It is **OFF** for launch/orbit/rendezvous/docking
>    so the CoM stays centred/symmetric.
> 2. `offsetPercent` sets the trim AoA / L/D — pick it for the target L/D (~1.0 = full offset ≈ L/D 0.2).
> 3. **NEVER toggle it to steer.** Bank REVERSALS are an **RCS ROLL** of the whole vehicle about the velocity
>    vector; the CoM shifter only establishes the aerodynamic trim the vehicle holds by itself. The capsule
>    keeps its trim AoA aerodynamically — **RCS does not fly AoA, only the roll (bank σ)."***

⚠ Under O8's baseline there is **no commanded bank at all**, so only points 1 and 2 apply to flight 1: the
shifter is engaged once before the interface and left alone. Point 3 becomes live only if the off-target
increment is ever built. The same rule is recorded from the caller's side in
`EXTRACT_RETURN_CONTROL.md` §3.1 and step 11 of its phase list.

---

## 4. What this line records for other tasks

- ⭐ **Entry is flown by MechJeb, per §B9 P8 / O8** — SmartASS `SURFACE_RETROGRADE`, `force_pitch`/`force_yaw`,
  attitude hold with **no commanded bank at baseline**; Landing Guidance
  (`MechJebModuleLandingPredictions`) runs the descent prediction; attitude gains are the one mission-wide
  `AttitudeController.BetterController` PID. Nothing from these two files becomes live.
- ✅ **The state-bus worry is moot for this line, and already retired by W18.** `EntrySteering`'s
  measurement members (`MeasuredBankRad`, `MeasureBc`, `LastSigmaRad`, `FootprintError`, `EntryLoverD`,
  `PredictDownErrAtBank`, `SetSplashTarget`) were read off `Steering.cs`, which is **never recovered**
  (§B12.8 rider (b)). `EXTRACT_RETURN_CONTROL.md` §5.2 re-homes every one of them in the open: footprint and
  Bc → MechJeb Landing Guidance; bank and L/D → **nowhere at baseline**, since O8 settled there is no bank to
  measure; splash target → `TargetController`. ⛔ **This line parks no state anywhere.**
- **W22** owns `pure/Trajectory.cs`'s copy of the schedule and has already marked it (`4715942`); this
  extract quotes it rather than touching it.
- **W18** carried the deleted bank loop (`RollKp` 0.6, `RollSign` +1.0) and the B8/T6 CourseCorrect entry
  channel (`UseCourseCorrectEntry` **false**, records-first, never flown) — see its §2.1.
- ⚠ **`docs/reference/INSTALLED_MODS.md` search (C1.15): not applicable.** This task wrote **no** simulation
  and modelled **no** quantity — it transcribed existing values out of `8b81816^` and marked them. C1.15 is
  recorded as considered and not triggered.
- ⛔ `git status` shows **no `.cs` file touched** by this task.

---

## Open questions for the owner

### Q1 — This extract went to `docs/EXTRACT_ENTRY_STEERING.md`, not to `MECHJEB_MISSION_TUNING.md` §7.2

**Situation.** W16's register line says *"Where it goes: `docs/MECHJEB_MISSION_TUNING.md` §7.2 (Entry), as a
block headed `[FROM DELETED GEN-2 …]`."* The owner-authorised batch instruction of **2026-09-04** instead
directed one self-contained `docs/EXTRACT_<name>.md` per task, explicitly so a batch stopping mid-way leaves
no half-written shared document. This session followed the batch instruction and **did not edit
`MECHJEB_MISSION_TUNING.md`** (C1.11). The same question stands on W20, W18, W21 and W17 — **all five
extracts in this batch**.

**Options.**
1. **Leave it as it stands** — each extract is self-contained and reachable through `INDEX.md`.
2. **Open one small [S] register line** to fold each extract's tuning-relevant sections into
   `MECHJEB_MISSION_TUNING.md` (§3 from W20; §6/§7.3–§7.4 from W18; §4.1–§4.3 from W21; §7.1 from W17;
   §7.2 from W16), markings intact, now that all five exist. *(This chat's recommendation.)*
3. **Move each extract wholesale into the tuning doc**, leaving pointer stubs (nothing deleted, C1.16).

**Recommendation: option 2** — one merge line for the batch rather than five, run after the batch, when the
half-written risk has passed. **This is the last of the five, so that line can be opened now.**

### Q2 — Two incompatible statements of the same L/D prior are in the record. Reconcile, or leave both?

**Situation.** §1.2. `pure/Trajectory.cs` (in the tree, W22-marked) models entry L/D as a **4-band schedule**
— 0.18 / 0.20 / 0.26 / 0.24 by atmosphere-depth ratio — while the deleted `pure/Entry.cs` anchors on a
**single 0.20** at full CoM offset and assumes L/D is **linear in `offsetPercent`**, and the deleted
`EntrySteering` fed the predictor a flat `LiftToDrag = 0.2` and **never set `UseLdBand`**. So the deleted
stack never used the schedule at all. Neither model has ever been measured in RSS-RO; both sit inside the
same 0.18–0.27 envelope, itself a research figure rather than a measurement.

**Options.**
1. **Leave both recorded and reconcile nothing** — no lifting entry is planned for flight 1 (O8), the
   schedule is already marked in the tree, and the deleted model is not coming back. *(This chat's
   recommendation.)*
2. **Open an [S] line to add a cross-reference note** to `pure/Trajectory.cs`'s header saying the deleted
   entry stack used a flat 0.2 and never enabled the bands, so a future consumer knows the schedule has never
   driven anything.
3. **Treat the 4-band schedule as the single model going forward** and record the flat-0.2 approach as
   superseded.

**Recommendation: option 1.** Reconciling two unmeasured priors produces a third unmeasured prior. The
question is settled by a recorded lifting-entry flight or not at all — and that flight is not planned, since
O8 made flight 1 ballistic.

### Q3 — Four unvalidated signs sit in the entry stack. Should a future increment be required to read them off a flight first?

**Situation.** §2.2. The deleted stack self-declares four unverified sign conventions: the rotation-correction
sign, the downrange/crossrange signs into `Entry`, the bank-measurement sign / roll-reference axis
(`RollRefSign`), and `CrossSign` — plus `ReturnControl`'s own `RollSign` on the roll loop. A wrong sign here
**steers the footprint the wrong way silently**; the same class of defect is recorded in
`EXTRACT_DOCKING_CONTROL.md` §4 (a lateral sign error that *"pushes lateral error the wrong way → docking
diverges off the corridor"*) and, from the other direction, in `EXTRACT_DEORBIT_BURN.md` §1.2(b).

**Options.**
1. **Require any off-target entry increment to derive each sign from a recorded ballistic entry first** —
   record the predicted-vs-actual footprint on the O8 baseline flight, which fixes the frame conventions
   before any bank is ever commanded. *(This chat's recommendation.)*
2. **Leave it to the increment** — it is a design detail of work that may never be built.
3. **Add it to §B16.8's recorder requirements** so the baseline ballistic entry captures what a later sign
   derivation would need (predicted impact, actual impact, measured bank, body rotation).

**Recommendation: option 1 together with option 3.** They are the same measurement seen from two ends, and
both are free on a flight that is already going to happen: the O8 ballistic entry can validate every sign in
this stack **without commanding a single degree of bank**. Both are owner calls — option 3 touches §B16.8,
and any flight needs the glass-time gate.
