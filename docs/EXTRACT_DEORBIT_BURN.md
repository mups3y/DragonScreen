# EXTRACT — `DeorbitBurn.cs` (deleted gen-2) → §B9 Phase 7's targets, and the failure that stranded the crew

> **[FROM DELETED GEN-2 `DeorbitBurn.cs` — REFERENCE ONLY, NEVER LIVE]**
>
> Produced by register task **W17** (Wave E-5), 2026-09-04, under the owner's G6 re-verdict of 2026-09-04
> (*"…re-entry orbit (Manoeuvre Planner, then execute next node)…"*): `W17` was re-verdicted
> **RECOVER-CODE → RECOVER-REFERENCE**. The deorbit burn is MechJeb's, **by name** — §B9 Phase 7 plans it
> with **`OperationPeriapsis`** (`new_periapsis` = the entry-corridor Pe) and flies it with the **Node
> Executor**, which is word for word the *"Manoeuvre Planner, then execute next node"* the owner described.
> **No `.cs` file was created, restored or edited by this task, and the bug below is NOT "fixed" — there is
> nothing live to fix.**
>
> ⛔ **`attitudeReadyDeg` and every burn margin are UN-CONVERGED** (§B16.8 ruling 2) until a recorded flight
> converges them. **This document is EVIDENCE, not an instruction**; `docs/BUILD_PLAN.md` wins on any
> conflict (C7.1).

**Read with:** `docs/BUILD_PLAN.md` §B9 Phase 7, §B10.1 (Node Executor), §B10.2 (`OperationPeriapsis`), §B11,
§B12.5a, §B12.8 · `docs/MECHJEB_MISSION_TUNING.md` §7.1 · `docs/AUTOPILOT_RECOVERY_AUDIT.md` (R1) §4.2,
§5.1–§5.2 · `docs/FLIGHT_VERIFICATION.md` · `docs/FLIGHT_CORPUS_ASSESSMENT.md` ·
`docs/ISSUE_REGISTER.md` RET-1/RET-2 · `docs/EXTRACT_RETURN_CONTROL.md` (W18 — where this burn sits in the
return order).

---

## 0. What was read, and what it was

| File (at `8b81816^`) | Size | R1 verdict | Regime | Flight status |
|---|---|---|---|---|
| `plugin/src/DeorbitBurn.cs` | 6,733 B | RECOVER-CODE | RSS-RO | ◐ **one units-bug flight** |
| `plugin/src/pure/DeorbitGuidance.cs` | 6,169 B | RECOVER-CODE | RSS-RO (ocean detection fixed for RSS) | ◐ **one units-bug flight (DS-DEO-001)** |

⚠ **R1 §4.2 validates ascent and abort, and nothing else.** So the *surviving* nominal path
(`ReturnControl.FlyDeorbitEntry`) was never proven either. **MechJeb inherits an unproven phase, not a solved
one** — and that is the single most useful sentence in this extract.

---

## 1. ⛔ THE FAILURE — verbatim from the file's own header

`DeorbitBurn.cs` exists *because of* a failure, and its header is the account written by the people it
stranded. Transcribed **verbatim**:

> *"Real Crew Dragon deorbits on the **DRACOS** (MMH+NTO); the **SuperDracos are ABORT-reserved and are EMPTY
> on a return**. The retired rescue path (`AbortControl.RunDeorbitBurn`) throttled the **SuperDraco**
> (`EngineRole.PodAbort`) → an **empty engine** → **ZERO thrust** (flight 024400: throttle 0.36, thrust_n 0,
> pe **196.9→196.1 km unchanged**) → **the crew stranded**. The nominal path (`ReturnControl.FlyDeorbitEntry`)
> already used the Dracos correctly, so the two diverged — one right, one wrong. This is the SINGLE
> implementation both now call, so they can never diverge again."*

`ISSUE_REGISTER.md` **RET-1** records the same event as *"THE ROOT — the autopilot cannot deorbit"*, and its
fix (`cb45cb3`) as unifying both callers onto this one file and **deleting the SuperDraco path**.

### 1.1 ⚠ THERE ARE TWO DISTINCT ZERO-DELIVERY DEORBITS, NOT ONE — and the register line conflates them

This matters, because they fail for **different reasons** and only one of them is fixed.

| | **Flight 024400** — the *engine-role* bug (RET-1) | **DS-DEO-001** — the *units* bug |
|---|---|---|
| Recording | ⚠ **not among the 13 surviving CSVs** | `Crew-2_20260831_141924.csv`, 833 rows, MET 31–247 s |
| Date | before 2026-08-31 | **2026-08-31** |
| Phase reached | the rescue path's burn | `TrunkJettison → Settle → **Burn**`, `abort_mode = DeorbitReturn` |
| Orbit | pe **196.9 → 196.1 km** (unchanged) | ap **342** / pe **313 km** |
| Δv | throttle 0.36, **`thrust_n` 0** | `dv_planned` **78.04**, `dv_delivered` **0.00**, residual **78.04** |
| Mechanism | **wrong engine** — it commanded the SuperDraco, which is abort-reserved and **empty on a return** | ⭐ **a ×1000 units bug in `AttitudeController.ControlTorque`** — the capsule **spun**, so the burn's attitude gate never opened |
| Status | ✅ **FIXED** (`cb45cb3`) — the SuperDraco path deleted, both callers unified onto `DeorbitBurn` | ✅ **the units bug was FIXED** (`f1a0cbb`), but ⛔ **no deorbit has been flown since** |

**The units bug, precisely** (`FLIGHT_VERIFICATION.md`, resolved 2026-08-31 against the capsule geometry dump
`geometry_dump_manual_2500s.csv` + this very flight): `ControlTorque` computed each thruster's power as
`thrusterPower * 1000` — i.e. in **N·m** — while stock `GetPotentialTorque`, the gimbal report and the MOI
divisor are all in **kN·m / t·m²**. `MAX(stock, geometric)` therefore *always* picked a number **1000× too
large**, and `maxAlpha = ct / MOI` inherited it. On the Dragon capsule alone (**6.8 t**, 16 Dracos, no
gimbal): stock RCS **2**, the N·m-bugged geometric **12,870** (→ **12.9 kN·m** once fixed), flight-**delivered
7 kN·m**; `maxAlpha` read **919 rad/s²** against a real **0.5**. *"→ the capsule spun under autopilot exactly
as observed."*

What that looked like in the recording (`FLIGHT_CORPUS_ASSESSMENT.md` — *"the extreme case of the authority
over-read"*): `angacc_*_auth` p50 **640–948 rad/s²** on a 7 t capsule · `rate_cmd_rads` p50 **18.6**, max
**28.4 rad/s (1,627 °/s)** against a measured `att_rate_meas` max of **6.15 rad/s** · `act_sat` p50 =
**1.000** with **52% duty above 0.95** · `rate_pitch_dps` reaching **352 dps** · and `dv_delivered` **0.00**.

### 1.2 ⭐ The three lessons, and what each one means for MechJeb

**(a) The engine-role lesson — a standing warning about which engine a deorbit uses.**
Dragon deorbits on the **Dracos** (MMH/NTO). The **SuperDracos are abort-reserved and empty on a return**.
Under MechJeb this becomes a **staging/engine-selection** question, not a throttle question: §B9/§B12.7 record
that **`Autostage` is OFF and the StagingController never actuates** — the conductor separates and ignites
directly. **Whatever the conductor points at the deorbit node must be the Dracos.** The failure was not
subtle: throttle 0.36 against `thrust_n = 0`, for the whole burn, and nothing noticed.

**(b) The units lesson — a control-authority over-read defeats a burn without ever touching the burn code.**
Nothing in `DeorbitBurn.cs` or `DeorbitGuidance.cs` was wrong on DS-DEO-001. The guidance correctly asked for
retrograde and gated the throttle on `AttitudeReady`; the attitude layer could not deliver it, so the gate
never opened and **zero of 78.04 m/s was delivered**. ⭐ **This is the failure mode §B10.1's Node Executor
settings must be checked against** — the Node Executor has exactly the same structure: `LeadTime` starts the
burn, `AlignedToleranceDegrees` (1° default) decides whether it is pointed *enough* to fire, and a vehicle
that cannot hold the aim will sit at the node and burn nothing. **On the first return, check
`dv_delivered` against `dv_planned` before checking anything else** — a zero there is this bug's signature,
whatever caused it.

**(c) The nose-shroud lesson — recorded in the same header, and easy to lose.**
The shroud is kept **OPEN through the whole burn**, because *"the forward Dracos are the attitude authority
with reaction wheels stripped"*, and closes **only once the burn completes**, to protect the docking adapter
on entry. ⛔ *"The old `ReturnControl` closed it AT trunk jettison, **before** the burn — obstructing the very
thrusters holding retrograde."* **This is a sequencing trap that survives the move to MechJeb intact**, since
the shroud is a sequenced vessel action (`Actuator`), not a MechJeb burn. It is recorded again in
`EXTRACT_RETURN_CONTROL.md` §1 step 8, in the return's phase order.

---

## 2. ⭐ The entry-corridor targets — what `OperationPeriapsis.NewPeriapsis` has to be given

### 2.1 What the guidance aimed at

| Quantity | Value in the deleted code | Status |
|---|---|---|
| **Target periapsis** (the cutoff) | `DeorbitInputs.EntryInterfaceAltM` — the pure file documents it as *"target periapsis = entry interface (**~120 000**)"* | ⚠ **UN-CONVERGED** — and ⛔ **the glue did not pass 120 km.** See §2.2 |
| **Planned Δv formula** | `Δv = √(μ/r_c) − √( 2μ·r_p / ( r_c·(r_c + r_p) ) )` — the retrograde **Hohmann first-burn** magnitude lowering the apsis to `r_p = R + h_EI`; `== \|Hohmann.Dv1(r_c, r_p, μ)\|` | ✅ **exact, not a tuning value** — a closed-form measured-state formula, recomputed **every tick** from the live orbit |
| **Cutoff law** | ⭐ **CLOSED-LOOP on MEASURED periapsis** — cut when `PeA ≤ target`, *"with the planned Δv as a backstop — **not** an open-loop clock"* | the design property worth keeping |
| **Burn duration (real)** | *"a LONG low-thrust retrograde Draco burn (**~12–16.5 min**; **Crew-1 Resilience 987 s**)"* | ✅ **REAL** — a documented real-mission figure, and it agrees with §B11's **[DOC]** *"deorbit burn ~15 min"* |
| **Throttle** | **1.0 (full open)** while `AttitudeReady && AllNominal`, else 0 | a long low-thrust burn is flown wide open; not a tunable |
| Δv actually planned in flight | **78.04 m/s** (DS-DEO-001, from a 342 × 313 km orbit) | ⚠ vs §B11's **~100 m/s [EST]** — the two are consistent once the different starting orbits are allowed for; **neither is measured**, since 0.00 was delivered |

### 2.2 ⛔ The 50 km / 120 km disagreement — the pure file and the glue targeted different things

`pure/DeorbitGuidance.cs` names its target field **`EntryInterfaceAltM`** and documents it as **~120,000 m**
— i.e. *lower periapsis to the entry interface*. But `ReturnControl` passed
**`DeorbitTargetPeM = 50,000 m`** into that field (`EXTRACT_RETURN_CONTROL.md` §2.1). **The two files
disagreed by 70 km on what the deorbit burn was aiming at**, and since no deorbit ever delivered a metre of
Δv, nothing ever exposed it.

**What the plan says, and it wins (C7.1):**

- §B9 P7 / `MECHJEB_MISSION_TUNING.md` §7.1(a): `OperationPeriapsis.NewPeriapsis` should be **a low or
  negative value** — *"⚠ the **FPA is the real target**; periapsis is only the handle. Too shallow → skip;
  too steep → over-g and over-heat."*
- §B11 **[EST]**: entry flight-path angle ≈ **−1.4° to −1.6° inertial**.
- §B11 **[DOC]**: entry interface **122 km (400,000 ft)** at **~7.8 km/s**.

⇒ **Neither 50 km nor 120 km is an entry-corridor periapsis.** 120 km is the *interface altitude* — targeting
periapsis *at* the interface produces a grazing, skip-prone trajectory, not a corridor entry; 50 km is a
guess with no stated derivation. ⭐ **The number `OperationPeriapsis` actually wants is the periapsis whose
resulting FPA at the 122 km interface lands in the −1.4° to −1.6° band, and this project has never computed
it and never flown it.** That is the honest state of §B9 Phase 7's most important setting.

### 2.3 The sequence the burn ran, for the record

`TrunkJettison` → `Settle` → `Burn` → `OrientEntry`/`Complete`:

1. **TrunkJettison** — trunk goes **first** (no heat shield, burns up; mass saving), pointed retrograde. Fired
   as a **direct part actuation**: `Actuator.JettisonTrunk` invokes the `ModuleTundraDecoupler` "Decouple"
   event **by name** — *"never staging/action groups."*
2. **Settle** — a brief dwell (`settleS`) to let the trunk clear and the propellants settle before ignition,
   attitude held.
3. **Burn** — retrograde, throttle wide open, gated on `AttitudeReady`; cut on **measured** periapsis.
4. **OrientEntry** — swing to **heat-shield-forward** (shield normal **along** the velocity vector, into the
   flow) and hold for the entry interface; `Complete` set.

Instrumentation worth noting: **planned and delivered Δv were recorded every tick** — planned from the
measured orbit, delivered as `∫ (measured RCS thrust / mass) dt` while actually firing — *"so the return
propellant budget is falsifiable in the recording."* ⭐ **That instrumentation is exactly what caught the
0.00-of-78.04**, and §B16.8's recorder requirement should keep it.

### 2.4 The tunables passed in — ⛔ all UN-CONVERGED

The glue took no magic literals: each caller passed its own `[Tunable]` values. From `ReturnControl`:

| Parameter | Value | Status |
|---|---|---|
| `attitudeReadyDeg` | **5.0°** | ⛔ **UN-CONVERGED** (§B16.8 ruling 2) — and ⭐ **this is the gate DS-DEO-001 never opened.** Its MechJeb counterpart is `NodeExecutor.AlignedToleranceDegrees` (1° default, §B10.1) |
| `settleS` | **3.0 s** | ⛔ UN-CONVERGED. Its MechJeb counterpart is `GuidanceController.UllageLeadTime` — **20 s [cfg]**, which §7.1(a) says **not to shorten** because a long coast precedes this burn on pressure-fed Dracos |
| `targetPeM` | **50,000 m** | ⛔ UN-CONVERGED, and disputed — §2.2 |
| `forwardSign` | **−1.0** | KSP translation-frame convention; MechJeb drives its own |

⚠ **`DeorbitBurn` has no constants of its own.** Every number above belongs to a caller, and every one is
un-converged. There is nothing here that a tune can start from except the **formula** (§2.1) and the
**closed-loop-on-measured-periapsis** discipline.

---

## 3. What this line records for other tasks

- ⭐ **The deorbit is planned by `OperationPeriapsis` and flown by the Node Executor, per §B9 P7** — with
  Landing Guidance's `LandAtPositionTarget` as the site-accurate alternative (§7.1(b)), capped by the
  propellant ledger. Nothing from this file becomes live.
- **`DeorbitOps` is T21's to flip**, increment 2 (§B12.5a) — recorded in full by **W18**.
- **W18** (`EXTRACT_RETURN_CONTROL.md`) places this burn at step 8 of the return order and carries the same
  shroud-sequencing and 50 km notes from the caller's side.
- **W19** (`AbortControl.cs`) is the other caller of this file; the SuperDraco path it once used is deleted.
- ⛔ `git status` shows **no `.cs` file touched** by this task, and **the bug is not "fixed"** — there is
  nothing live to fix.

---

## Open questions for the owner

### Q1 — This extract went to `docs/EXTRACT_DEORBIT_BURN.md`, not to `MECHJEB_MISSION_TUNING.md` §7.1

**Situation.** W17's register line says *"Where it goes: `docs/MECHJEB_MISSION_TUNING.md` §7.1 … with the
units bug as a ⚠ callout, not a footnote."* The owner-authorised batch instruction of **2026-09-04** instead
directed one self-contained `docs/EXTRACT_<name>.md` per task, explicitly so a batch stopping mid-way leaves
no half-written shared document. This session followed the batch instruction and **did not edit
`MECHJEB_MISSION_TUNING.md`** (C1.11). The units bug is §1 of this document — the first thing in it, not a
footnote. The same question stands on W20, W18 and W21.

**Options.**
1. **Leave it as it stands** — self-contained, reachable through `INDEX.md`.
2. **Open one small [S] line covering the whole batch**, to fold each extract's tuning-relevant sections into
   `MECHJEB_MISSION_TUNING.md` once all five exist. *(This chat's recommendation.)*
3. **Move each extract wholesale into the tuning doc**, leaving pointer stubs (nothing deleted, C1.16).

**Recommendation: option 2**, with the note that §1.2(b)'s Node-Executor warning is the piece most worth
landing in §7.1 specifically, because that is where someone will set `AlignedToleranceDegrees`.

### Q2 — ⭐ Nobody has ever computed the entry-corridor periapsis. What should `NewPeriapsis` be given?

**Situation.** §2.2. The deleted pure guidance documented its target as the **122/120 km entry interface**;
the glue passed **50 km**; §B9 P7 and §7.1(a) say the value should be **low or negative** and chosen from the
**flight-path angle**, which §B11 puts at **≈ −1.4° to −1.6° [EST]** at a **122 km [DOC]** interface. **No
number in this project has ever been derived from that FPA, and no deorbit has ever delivered a metre of Δv**
(024400 fired an empty engine; DS-DEO-001 never opened its attitude gate). So §B9 Phase 7's single most
important setting is currently unsourced.

**Options.**
1. **Derive it analytically now** — from the departure orbit (§6.1: near-circular, ~400–420 km), solve the
   periapsis that yields −1.4° to −1.6° inertial FPA at 122 km, and record it **[EST]** with its working, to
   be validated in flight.
2. **Use option (b) instead** — Landing Guidance's `LandAtPositionTarget`, which plans the deorbit itself
   from the LZ, and treat `OperationPeriapsis` purely as the fallback (§7.1's own framing). ⚠ §7.1 warns it
   *"will happily plan a plane change or a course correction it thinks it needs"* — Δv Dragon does not have.
3. **Defer to the first recorded return** and fly RSS-RO defaults, per §B12's tune-after-flight baseline.

**Recommendation: option 1 followed by option 3.** The FPA→periapsis map is closed-form geometry, not a
tuning judgement, so a derived **[EST]** costs one task and removes a guessed number from the most
consequential burn of the mission — but it should still be *validated*, not trusted, on the first return.
Option 2's risk is real and §7.1 already flags it. **Deriving it is a task the owner would need to open; a
build chat cannot decide the entry corridor.**

### Q3 — The register line calls DS-DEO-001 "the units-bug flight" and quotes flight 024400's numbers. Should the record be split?

**Situation.** §1.1. These are **two different flights with two different mechanisms**: 024400 threw the
SuperDraco (engine-role, fixed by `cb45cb3`, recording **not in the surviving corpus**), and DS-DEO-001
(`Crew-2_20260831_141924.csv`, the recording that **does** survive) delivered 0.00 of 78.04 m/s because the
×1000 `ControlTorque` units bug spun the capsule so the attitude gate never opened. R1 §5.1's row on
`DeorbitGuidance.cs` says *"one units-bug flight (DS-DEO-001)"* — correct — while `DeorbitBurn.cs`'s header
describes 024400. Reading either alone gives a wrong picture of what failed, and the second failure is the
one whose *class* (a burn that never fires because the vehicle cannot point) can recur under MechJeb.

**Options.**
1. **Keep both, distinguished, as §1.1 now does** — and treat that table as the canonical account.
   *(This chat's recommendation.)*
2. **Also correct the register/R1 wording** so "the units-bug flight" is never read as covering 024400 — an
   [S] line, since C1.16 forbids deleting and C7.1 governs how a doc is amended.
3. **Leave the older wording alone** — both source documents are individually accurate and the conflation
   only appears when they are read together.

**Recommendation: option 1, with option 2 if the owner wants the register itself corrected.** The distinction
is load-bearing: only one of the two mechanisms is fully retired, and the surviving one is the failure class
the Node Executor can reproduce.
