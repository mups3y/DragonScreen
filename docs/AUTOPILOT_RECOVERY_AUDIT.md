# Autopilot Recovery Audit — R1

> **RESEARCH + AUDIT ONLY.** No code was restored into the working tree. This document is the complete
> inventory and triage of the flight software deleted on 2026-09-01 by commit `8b81816`
> *"Delete the autopilot entirely — DragonScreen is screens-only (owner directive)"*.
>
> **Authority:** owner, 2026-09-03, via the overseer — *the deleted autopilot is to be RECOVERED AND
> AUDITED IN FULL before Part B proceeds. Full audit of the whole pre-deletion tree, not a targeted
> extraction.* Recorded here as the owner's decision (C1.12).
>
> **Source boundary (C7):** this repo's own git history only. Precedent: `docs/BLACKBOX_RESEARCH.md`
> already read deleted files with `git show`. No other repo, no KSP install, no external URL was read.
>
> **Status:** COMPLETE — R1 was not split. Every file in the pre-deletion `plugin/` and `docs/` trees
> has a row and a verdict.

---

## 0. How to read this document

### 0.1 The regime rule — it governs every verdict below

Two bodies of prior work exist and they are evidence of **different things**:

| | **Our deleted C# autopilot** | **F9I (kOS)** |
|---|---|---|
| Regime | **RSS/RO** (Sol planet pack, RealFuels, TestFlight) | **STOCK KSP ONLY** (owner, 2026-09-03) |
| Mission | **Never completed end-to-end** | **Completed end-to-end** |
| Flight-validated | Ascent control + abort ONLY (`e90a63f`: *"ascent control is DB-validated (pe_p95 < 0.4 deg)"*) | Its own stock missions |
| What transfers | **Its numbers** — this is our only source of RSS-RO constants | Its **method** and its **recording schema**. **NO number**: not an altitude, velocity, timing, drag figure, gain or margin. |

Every `RECOVER-*` row states **which regime the file's constants came from** and **whether they were
ever flown**. A constant with no stated regime is recorded as a **defect** and named in §7.

### 0.2 The two-generation trap

Two generations of flight software share class names. Taking both produces duplicate types.

- **GEN 1** — newest at `158eb2a^`. Superseded wholesale by `158eb2a` (2026-08-26)
  *"Ground-up autopilot rebuild (CLAUDE): pure L0–L7 + all KSP glue seams"*. **Not in the audit set**
  (the audit set is the `8b81816^` tree); treated separately in **§6**. Default classification
  **OBSOLETE**, with three specific exceptions found and named there.
- **GEN 2** — newest at `8b81816^`. **This is the recovery target** and the whole of §2–§5.

### 0.3 The verdicts

| Verdict | Meaning |
|---|---|
| **RECOVER-CODE** | Restore the file itself into the Part-B build (subject to its stated collisions and regime caveats). |
| **RECOVER-REFERENCE** | Read it, mine it, quote it — **never make it live**. |
| **SUPERSEDED** | A file in today's tree already does this job; today's file is named. |
| **OBSOLETE** | Superseded by the gen-2 rebuild, or explicitly removed as a defect, or a build artefact. |

### 0.4 The audit set, derived

```
git ls-tree -r 8b81816^ --name-only   ->  371 paths, of which 332 are under plugin/ or docs/
git ls-tree -r HEAD     --name-only   ->  332 paths
```

| | Count |
|---|---|
| Audit set (`plugin/` + `docs/` in the pre-deletion tree) | **332** |
| Deleted at `8b81816` (in PRE, not in HEAD) | **213** |
| Still present today (fast-path, §5) | **119** |

Of the 213 deleted: 126 C# files (62 `pure/`, 23 `src/`, 41 `test/`) — plus `plugin/src/=`, a 0-byte
junk file, and `plugin/build/csc.rsp`, a gitignored build artefact, 60 markdown docs, 22 flight
recordings/screenshots, and the 3-file tuning DB.

Last-touching commits were resolved in a single `git log --name-only` pass over `8b81816^`, not one
`git log` per file.

---

## 1. Executive summary — what actually matters

**Nine findings, in order of consequence for Part B.**

1. **`plugin/src/Actuator.cs` (868 lines, 37 public methods) already implements BUILD_PLAN's
   no-staging / no-action-groups rule, and it is essentially complete.** It is the single highest-value
   recoverable artefact in the tree. §3.1 confirms the overseer's finding in detail. It has one
   dependency that did NOT survive the deletion — `pure/Actuation.cs`, the capability→role classifier —
   while its other dependency, `pure/VehicleParts.cs`, **did** survive and is live today.

2. **The custom attitude loop is the piece that failed, and by the time of the deletion it had already
   been reduced to a faithful MechJeb port.** The last ~15 commits are that failure. `70dc239` names the
   inventions that were removed (`RcsPulse` PWPF, `ActuatorLag` B4 lead compensation, `RvCoast`
   hysteresis, plus the phase-plane deadband and the 1.5x hold-authority scale). Those are marked
   **OBSOLETE** here — the owner directive to strip them is already executed in the pre-deletion tree.
   `AttitudePilot` / `AttitudeController` / `pure/AttitudeLoop.cs` are **RECOVER-REFERENCE ONLY**
   (§3.2). §7 lists every recovered file implicated in the failure so nothing is made live blind.

3. **The one genuine RSS-RO empirical dataset that survives is `pure/BoosterDrag.cs`** — a Mach-binned
   ballistic-coefficient curve mined from *"18,080 clean unpowered in-atmosphere descent samples across
   48 recorded RSS/RO flights"*. It is 32 lines of table and interpolation and it is exactly what §B16
   needs (§3.5). **The raw CSVs behind it are gone** — they were gitignored and never committed.

4. **`pure/Trajectory.cs` is a genuine, body-agnostic RK4 predictor whose drag term is MEASURED, not
   modelled**, and `pure/BoosterDrag.cs` feeds it a Mach curve. Together they are the intended prediction
   engine for the booster core, and the design is regime-portable by construction (mu, body radius, body
   omega, and the atmosphere model are all passed in as callbacks). §3.5.

5. **Only two subsystems were EVER flight-validated: ascent control and abort.** `e90a63f`'s
   Build-vs-Tuned matrix is quoted verbatim in §4.1. Nine of the eleven backlog items sit at
   *"researched defaults, the DB has no data for this phase yet"*. **The booster was never recovered** —
   `docs/FLIGHT_144114_SCREEN_AUDIT.md:35` (in today's tree) records *"Booster ballistic (eng never lit)
   → LOST"*, root-caused to ullage.

6. **The BlackBox is being built fresh (owner), and `docs/BLACKBOX_RESEARCH.md` §3.1–§3.2 has already
   done most of R1's work on the recorder.** This audit **confirms** its figures against the source and
   adds two corrections (§3.3). The deleted recorder is **RECOVER-REFERENCE ONLY**.

7. **All eight items in the overseer's finding-4 list exist. One of them is not deleted at all** —
   `pure/StageStats.cs` survived `8b81816` **byte-identical** and is live in today's tree with its test.
   §3.4 corrects this.

8. **`plugin/src/pure/Terminal.cs` carries an explicit self-declared regime defect** —
   *"⚠ THESE ALTITUDES ARE KERBIN'S, NOT THE REAL DRAGON'S"* — and is an F9I port. It is the clearest
   single illustration of the regime rule in the tree. **RECOVER-REFERENCE** (§7.3).

9. **Today's `_AutopilotStub.cs` compiles the screens against GEN-1 class names**
   (`AutoPilot`, `StationApproach`, `DockingOps`, `DeorbitOps`, `UndockOps`, `BoosterRecovery`) as well
   as gen-2 ones. The display contract's vocabulary is partly gen-1. Any recovery must map gen-2 modules
   onto those gen-1 names or change the screen call sites — a collision the plan does not currently
   name. §5.2 and Open Question **Q4**.

---

## 2. The deletion, and the arc that led to it

```
fff21b7  2026-08-31  Validate + harden RcsAccounting recorder before the focused flight
a8ac818  2026-08-31  Investigate SAS/MechJeb attitude-hold: phase-plane deadband is the fix
ddd0ef3  2026-08-31  Deeper control research: phase-adaptive RCS architecture synthesis
23efb74  2026-08-31  DS-ASC-007: acc_* flight resolves RCS loss = ~97% attitude (52% + 45% simultaneous)
a6eb15f  2026-08-31  Implement RCS attitude-hold phase-plane deadband (DS-ASC-007 fix)
2fe1cef  2026-09-01  DS-ASC-008 analysis + roll under-control root cause; archive recordings
7057328  2026-09-01  RCS over-thrust root cause: Draco x5 boost, per-phase scale-down removed
f38005f  2026-09-01  Revert Draco x5 thrust hack to stock 0.4 kN         <- revert 1
3ef6e3d  2026-09-01  SAS test mode: hand all nominal attitude to stock SAS + stock RCS
1603998  2026-09-01  Use stock KSP SAS for attitude, custom loop off; autopilot still steers
0624278  2026-09-01  Our SAS 1.5x hold authority + S1/S2 gimbal dampening (owner exact spec)
0ab61ab  2026-09-01  Remove lag-comp runaway loop + lower S2 insertion target  <- revert 2
1c24697  2026-09-01  Revert 3 destabilizing tuning changes that grew the attitude limit cycle  <- revert 3
fc74863  2026-09-01  PID research fix (remove non-MechJeb lag comp) + RCS-on for S2 + separation hold-lock
9f37e73  2026-09-01  Booster prograde-hold at separation + record predicted impact at sep+5s
70dc239  2026-09-01  Strip Claude-invented control loops -> faithful MechJeb port (owner directive)
8b81816  2026-09-01  Delete the autopilot entirely — DragonScreen is screens-only (owner directive)
```

### 2.1 What `70dc239` actually removed, in the owner's words

> Owner: *"Everything invented by reading flight data and chasing your tail needs to go"*; RCS
> *"pulsing like crazy"* in orbit; audit for double-ported methods.
>
> Audit result: **NO double-porting.** One PID class (`Pid2`), one `AttitudeController`; the only
> attitude writers are the Dragon (`AttitudePilot`), the separate booster vessel (`BoosterControl`), and
> the entry-bank roll — never the same channel on the same vessel at once. **The interference was
> invented loops layered on the MechJeb port, not duplicates.**
>
> Removed (all Claude inventions, none in MechJeb's `BetterController`):
> - `RcsPulse` / PWPF: `UseRcsPulse=false`. MechJeb writes the CONTINUOUS RCS command; the 0.06 s pulse
>   chopping **WAS** the *"pulsing like crazy"* in orbit.
> - `RvCoast` coast-release hysteresis (`RendezvousControl`).
> - B4 lag compensation (`AttitudeController` + the `actEst`/`lastGimbalNm`/`GimbalPresentNm`/
>   `GimbalResponseSpeed` fields it needed).
> - Phase-plane hold deadband + 1.5x hold-authority scale (`AttitudeLoop.Axis` params + `AttitudePilot`
>   tunables); `Axis` is back to plain MechJeb.
> - Gimbal dampener was already reverted (stock `responseSpeed`).
>
> `AttitudeLoop` is now a faithful MechJeb `BetterController` port: **PosKp 2.03 / VelKp 7.98 / P-only
> velocity, arrestable-rate curve, SmoothTorque 0.10.** Dead classes (`RcsPulse`/`ActuatorLag`/`RvCoast`)
> remain unused, deletable later. L1 green (731,239 checks).

**Consequence for the recovery.** The invented loops were *already gone* at `8b81816^`. Recovering the
pre-deletion tree does **not** re-import them as live code — `RcsPulse.cs`, `ActuatorLag.cs` and
`RvCoast.cs` sit in the tree dead and unreferenced. They are classified **OBSOLETE** below and must not
be restored.

### 2.2 The root cause that was found and NOT fixed

`2fe1cef` (docs-only) root-caused **roll under-control** and the fix was deliberately withheld:

> ROLL UNDER-CONTROL root-caused (owner-reported: S2 shakes violently, stops when RCS on; uncontrolled
> roll in manoeuvres). **One cause, three symptoms:** single MVac has no roll authority
> (`ctrl_tq_roll=0` for **79% of S2**), the S2 roll-trim hysteresis (`AscentControl.cs:397-414`)
> sawtooths roll to **27.5 dps** + toggles RCS **17x** + 2 Hz gimbal chatter = the shake; rendezvous
> releases the attitude channel on coast + never angle-holds roll (`Steering.cs:102`) = the mission roll
> drift. Same cause as the separation tumble + the 17% detumble fuel. **Fix is a continuous roll-only
> deadband everywhere -- touches PROVEN ascent, held for owner review + a V4 re-fly.**

So: **two named, located, unfixed defects** are inside files this audit marks RECOVER —
`AscentControl.cs:397-414` and `Steering.cs:102`. Both are carried into §7.

### 2.3 The regime qualifier on the failure

Everything above is **ascent + Dragon-RCS on-orbit attitude hold**. That is a different control regime
from **booster landing**:

| | Ascent / Dragon attitude hold (where it failed) | Booster landing (§B16) |
|---|---|---|
| Actuator | Draco RCS (ON/OFF, 0.4 kN), single-MVac gimbal with **no roll authority** | Octaweb gimbal, high thrust, deep throttle range |
| Aerodynamics | Coast (none) or max-Q on a slender stack | Dominant — grid fins are the primary descent authority |
| Torque budget | Marginal to absent on roll (`ctrl_tq_roll=0`, 79% of S2) | Large; gimbal authority scales with thrust |
| Timescale | Minutes of hold | Seconds of terminal solve |

**A booster-landing loop failing is not predicted by this failure, and a booster-landing loop working is
not evidence that this failure is fixed.** The failure evidence constrains §B12.5's ascent/attitude
controllers; it does not constrain §B16.

---
## 3. The five overseer findings — confirmed or corrected

### 3.1 `fe837f0` "Step A: Actuator (direct part control) — rip out staging + action groups" — **CONFIRMED, and it is more complete than the finding suggests**

**Commit:** `fe837f0` · 2026-08-26 · exists.
**File at `8b81816^`:** `plugin/src/Actuator.cs`, **48,171 bytes / 868 lines / 37 public methods.**

Its own header states the rule the plan depends on:

> ⛔ **HARD RULE** (`[[direct-part-control-hard-rule]]`): the autopilot **NEVER stages** and **NEVER
> fires an action-group binding** to actuate the vehicle. It reaches the live **PART MODULES** and calls
> them: `ModuleEngines.Activate/Shutdown`, `ModuleDecouple`/custom-decoupler `.Decouple()`,
> `ModuleRCS.rcsEnabled`, RealChute deploy, leg/fin deploy, the SuperDraco abort motor. WHICH part plays
> WHICH role is decided by the pure classifier (`pure/Actuation.cs` + `pure/VehicleParts.cs`), so the
> capability→actuation mapping is headless-tested against the real craft (`test/ActuationTest.cs`),
> never re-discovered live.

**The one documented exception**, and its justification (verbatim):

> The one deliberate exception is the RCS *master* toggle (`KSPActionGroup.RCS`): in KSP a thruster only
> answers `FlightCtrlState.X/Y/Z` translation while the vessel-level RCS flag is set, so `EnableRcs` sets
> both the per-thruster `rcsEnabled` **and** the master — exactly as our porting reference MechJeb does in
> every controller that translates on RCS (`NodeExecutor`, `ThrustController`, `RCSController`). That
> master is a **stock vessel enable, not a VAB-dependent AG binding**, which is the class of thing the
> rule forbids. Everything else here is pure direct module actuation.

**Coverage — what the Actuator can do (37 public methods):**

| Group | Methods |
|---|---|
| Engines | `ActivateEngines` · `ShutdownEngines` · `MaxThrustN` · `FindEngine` · `EngineThrust` · `TotalActiveThrustN` · `ShutdownBoosterEngines` · `IgniteOctawebLiftoff` · `IgniteSecondStage` |
| Octaweb / engine-out | `BalanceOctawebThrust(v, role, demandedTorqueNm)` — ~100 lines, the TCA torque-nulling application |
| Separation | `SeparateBooster` · `FireDecoupler(role)` · `SeparateDragon` · `FirePartDecoupler(part)` · `JettisonTrunk` · `Undock` |
| Pad | `ReleaseHoldDowns` · `OpenErector` · `ErectorClear` |
| Abort | `FireAbort` |
| Shroud | `OpenNoseShroud` · `CloseNoseShroud` · `ToggleNoseShroud` |
| RCS | `EnableRcs` · `DisableRcs` · `IsRcsOn` · `RcsInducedTorque(v, demandCtrl, …)` (~78 lines) · `RcsThrustN` |
| Deployables | `DeployLegs` · `DeployGridFins` · `DeploySolarPanels` · `RetractSolarPanels` · `DeployAntennas` |
| Chutes | `DeployChutes(drogue)` · `CutChutes(drogue)` |

**How complete is it?** For the mission the plan describes, effectively complete: every actuation the
Falcon-9 + Crew-Dragon stack needs from pad-clamp release to chute cut is present, defensively wrapped
(*"a failed actuation logs and returns; it never throws into the control tick"*), and it is the only
file that touches parts. Nothing obviously missing was found. **It is glue, not guidance — it contains
no guidance constants and therefore no regime exposure.**

| | |
|---|---|
| **Regime** | **n/a** — pure KSP part-module glue; the only numbers are indices and defensive guards. |
| **Ever flown?** | **YES** — it was the actuation path on every DS-ASC-00x RSS/RO flight and every abort in the corpus. |
| **Verdict** | **RECOVER-CODE — HIGHEST PRIORITY.** Serves **§B12.7** and every §B12.5 controller. |
| **Collision** | Today's `_AutopilotStub.cs:79` declares `public static class Actuator` with a no-op surface. Restoring the real one **replaces** that stub class — check every screen call site against the stub's signatures. |
| **Missing dependency** | **`pure/Actuation.cs` was deleted** (the `EngineRoleOf` / `DecouplerRole` classifier). `pure/VehicleParts.cs` **survives today** and is live. `test/ActuationTest.cs` was deleted. Recovering `Actuator.cs` requires recovering `pure/Actuation.cs` + `test/ActuationTest.cs` with it. |

**One caveat worth stating.** `RcsInducedTorque` and `BalanceOctawebThrust` are not plain actuation —
they compute. `BalanceOctawebThrust` applies the `pure/ThrustBalance.cs` TCA solve; `RcsInducedTorque`
is part of the attitude path implicated in §7. Both should be recovered *with* the Actuator but treated
as §7-implicated rather than as inert glue.

---

### 3.2 `abb01eb` "Step B: AttitudePilot — direct gimbal/RCS loop, replaces SAS" — **CONFIRMED. RECOVER-REFERENCE ONLY, per owner directive. Not resurrected.**

**Commit:** `abb01eb` · 2026-08-26 · exists.

By the time of the deletion this had been **refactored into two files** — the finding names one, the
tree has two:

| File at `8b81816^` | Bytes / lines | What it is |
|---|---|---|
| `plugin/src/AttitudePilot.cs` | 5,310 / 76 | A **thin static facade** holding one default `AttitudeController` for the ACTIVE vessel, forwarding the static API + diagnostics, and doing the `FlightDriver.SetAttitude` writes. |
| `plugin/src/AttitudeController.cs` | 20,434 / 304 | The **stateful loop**, made instantiable at C2 Step-2 so the non-active booster gets its own state. Frame conversion + `Pid2` banks + `SmoothTorque` low-pass. |
| `plugin/src/pure/AttitudeLoop.cs` | 8,026 | The **pure per-axis law** — *"ported from MechJeb"*, `AttitudeLoop.Axis(...)`. |

`AttitudeController`'s header documents the frame conversion it inherited from MechJeb's
`BetterController` + `DirectionTracker` (`docs/ATTITUDE_CONTROL_RESEARCH.md` §1-2):

```
current   = ReferenceTransform.rotation · Euler(-90,0,0)      // nose -> +Z, LookRotation convention
requested = LookRotation(dir, up)                             // up = current roll ref, roll error ~ 0
delta     = Inverse(current)·requested;  euler = delta.eulerAngles (deg)
error     = ( ClampPi(euler.x), ClampPi(euler.z), −ClampPi(euler.y) )   // (pitch, roll, yaw); yaw NEGATED
```

**Classification.**

| | |
|---|---|
| **Generation** | GEN 2 |
| **Regime** | Gains are **MechJeb's own defaults**, not ours and not RSS-RO-derived: PosKp 2.03 / VelKp 7.98 / SmoothTorque 0.10 (`70dc239`). The one remaining local knob, `RcsTorqueFloorNm = 1.0`, is a **diagnostic threshold**, not a control gain — it detects that KSP's own RCS-torque report *"flickers ~2 N·m 91% of RCS-on ticks"*. |
| **Ever flown?** | **YES — and it is the piece that failed.** DS-ASC-005/006/007/008 all flew it; DS-ASC-007 resolved *"RCS loss = ~97% attitude"*; `1603998` switched attitude to stock SAS entirely. |
| **Verdict** | ⛔ **RECOVER-REFERENCE ONLY — never live code (owner directive).** All three files: `AttitudePilot.cs`, `AttitudeController.cs`, `pure/AttitudeLoop.cs`. |
| **What it is reference FOR** | (a) the **frame-conversion block above** — that is the hard-won part and it is independent of the gains; (b) the **two-vessel instantiation pattern** (§B16 needs exactly this: two independent loops, two `FlightCtrlState` sinks); (c) the `max(stock-reported, geometric)` RCS-torque workaround; (d) a worked record of which MechJeb `BetterController` fields matter. |
| **Serves** | §B16 (the two-vessel pattern) and §B12.5 as a **negative** reference — what a hand-written attitude loop cost. |
| **Collides with** | Nothing in today's tree (no attitude code exists). The pinned MechJeb embed (§B12.1) **supplies the real replacement** — that is the whole point of the embed. |

**Nothing in this audit plans to resurrect it.**

---

### 3.3 `967b07a` "Recorder reliability fix: always-on snapshot" — **CONFIRMED. Assessed as REFERENCE ONLY against `docs/BLACKBOX_RESEARCH.md`. Not revived.**

**Commit:** `967b07a` · 2026-08-26 · exists. 6 files, +87/−4.

**What it fixed** (verbatim from the commit):

> Verifying the 231747 proving-flight CSV against the screenshots + KSP.log showed the recorder captured
> nav + attitude/control correctly but **FROZE on the ascent filler**: `mission_phase` empty (`PutMode`
> never called), `ascent_phase` stuck at `GravityTurn` through the whole abort + chute descent, and
> `abort_mode`/`fdir_abort`/`chute_phase`/`drogue`/`main` all empty. **Root architecture flaw: only the
> active controller's `Fill` wrote columns, so anything with no controller (abort, coast, cutout) was
> lost.**
>
> Fix, borrowing MechJeb's `MechJebModuleFlightRecorder`: `FlightRecorder.PutBase`, an **ALWAYS-ON base
> written every sample regardless of phase**; new columns `srf_speed_mps` (both surface + orbital, ending
> the frame ambiguity), `accel_g`, `thrust_n` (= `Actuator.TotalActiveThrustN`, so an uncommanded cutout
> — the MET-121 failure — is DIRECTLY visible); `CrewProcedureOps.CurrentMode`; the abort path swaps the
> stale ascent `Fill` for `AbortFillRow`.

**What the recorder captured, at what rate, in what format** (measured from `8b81816^`):

| | Measured |
|---|---|
| Pure schema | `plugin/src/pure/FlightRecorder.cs` — 33,660 bytes / 507 lines; **135 column names** in the `Schema[]` array |
| Glue | `plugin/src/FlightLog.cs` — 17,464 bytes / 304 lines |
| **Rate** | **4 Hz** — `[Tunable] public static double SampleIntervalS = 0.25;` *"4 Hz — plenty for post-flight analysis"*. This is the **only** value it ever had (single-commit history for that line). |
| Format | One CSV per flight, `<KSP>/DragonScreen_capture/<VesselName>_yyyyMMdd_HHmmss.csv`, header written once, invariant-culture formatting (*"never locale-formatted — a European locale would write `1,5` and shred the CSV"*), CSV escaping in the pure half |
| Architecture | Pure owns schema + formatting + the `Put*` fillers; glue owns the file and the sampling clock. Glue sets `Action<string[]> Fill` per active controller. |
| Measured header of the last flown file | **136 columns** (`Crew-2_20260901_004929.csv`) |
| Column groups | time/mode/gate · nav (both speed frames, q, mach, downrange, mass, felt g, measured thrust) · control · attitude-loop internals · body rates · **control authority** (`ctrl_tq_*`, `moi_*`, `rcs_thrust_n`) · orbit state · ignition gates · … |

**Against `docs/BLACKBOX_RESEARCH.md`'s spec — the deleted recorder falls short on four axes:**

| BlackBox spec | Deleted recorder | Gap |
|---|---|---|
| **R0** physics-rate (50 Hz) **accumulators**, never sampled | Only `pure/RcsAccounting.cs` did this, added late (`3a47567`, 2026-08-31), and only for RCS | The spec generalises R0 to *anything that pulses*. The deleted recorder aliased everything else. |
| **R1 = 10 Hz** dynamic block | **4 Hz**, flat | 2.5x too slow; the spec says a 0.06 s RCS dwell is invisible at snapshot rates |
| **R2 = 2 Hz / R3 = 0.1 Hz** tiered, blank-not-forward-filled, declared in a manifest | **Single flat rate, all columns every row** | No rate ladder, no `*.manifest.json`, no per-column period/provenance |
| Three streams + a discrete **event log** (EVR analogue) | One CSV | Events are quantised into sampled columns; an *exact* event time is not recoverable |
| Validity = **blank, never a plausible number** | Mixed — `double.NaN` in places, empty in others | The `PutBase` fix exists precisely because blanks were being *left* rather than *meant* |

**Two corrections to `docs/BLACKBOX_RESEARCH.md` §3.1** (both minor; logged, not fixed — C1.1):

1. §3.1's table says the newer Recorder B had **"136 columns in the final flown corpus"** — that is the
   measured **CSV header**, and it is right. But §2.0's rate rationale says 10 Hz is *"2x the deleted
   recorder's best (5 Hz)"*. **5 Hz was Recorder A's rate; Recorder B's was 4 Hz**, which §3.1's own
   table states correctly. The 10 Hz choice is 2.5x Recorder B, 2x Recorder A — the conclusion is
   unaffected.
2. The `Schema[]` array in `pure/FlightRecorder.cs` declares **135** names; the flown header has **136**.
   The difference is one column emitted by the glue outside the array. Not chased — recorded so a fresh
   build does not treat 135 and 136 as the same number.

| | |
|---|---|
| **Regime** | **n/a** — a schema and a clock, no physics constants. |
| **Ever flown?** | **YES** — it produced the entire 55-flight RSS/RO corpus and every DS-ASC-00x recording. |
| **Verdict** | **RECOVER-REFERENCE ONLY.** The BlackBox is being built fresh (owner). |
| **Serves** | BlackBox §3.2(2), which already says *"the deleted recorder is recoverable, and its hard-won details are the specification"*. This audit adds: recover the **`PutBase` always-on principle** and the **control-authority column group** (`ctrl_tq_*` / `moi_*` / `rcs_thrust_n`) — those made `tuning_db.py`'s `angacc_*_auth` metric possible and they are what proved the roll defect. |
| **Collides with** | Nothing today. `plugin/tools/assess_flight.py` and `plugin/tools/tuning_db.py`, which **read this schema**, both survive in the working tree. A fresh BlackBox schema breaks them unless the column names are carried over or the analysers are ported. **This is Open Question Q3.** |

---

### 3.4 The eight items of finding 4 — **all confirmed to exist; one correction**

| # | Commit | Date | Subject | Files at `8b81816^` | Classification |
|---|---|---|---|---|---|
| 1 | `b477c11` | 08-28 | B5 (LAST): multi-stage UPFG (PEGAS virtual stages) — the primer-vector PVG | `pure/Upfg.cs` 14,428 B; `test/UpfgTest.cs` 10,290 B | **RECOVER-CODE** — closed-loop S2 guidance, §B12.5 ascent. **Regime: RSS** (`UpfgTest.cs` uses μ = 3.986e14, R = 6371 km, and names RSS). **Flown?** The ascent it guided was DB-validated at the S2Burn phase (pe_p95 < 0.4°) — but `b477c11` landed **after** `e90a63f`'s matrix, which still showed **B5 "○ not built"**. **So the multi-stage PEGAS version was built on 08-28 and its flight status is not separately recorded.** Treat as BUILT + unit-tested, ascent-phase-flown in its predecessor form, PEGAS form unproven. |
| 2 | `18a690b` | 08-28 | B7: Lambert solver + maneuver/finite-burn library (pure) | `pure/Lambert.cs` 4,228 B; `pure/Maneuver.cs` 2,298 B; `test/LambertTest.cs` 4,636 B | **RECOVER-CODE** — universal-variable math. **Regime: n/a** (`e90a63f`: Tuned **"—"**, *"universal-variable math; no tunables"*). Test validates by **self-inversion**. **Flown?** No — never exercised in a completed rendezvous. |
| 3 | `106a885` | 08-28 | B6: NavFilter (pure) — strict-fidelity per-axis Kalman | `pure/NavFilter.cs` 8,827 B; `test/NavFilterTest.cs` 6,943 B | **RECOVER-CODE** — 3-state per-axis (pos, vel, bias). **Regime: unstated in the file — DEFECT (§7.3).** `e90a63f`: Tuned **"○ — IMU/RGPS noise tunables; no sensor-truth flight yet"**. **Flown?** **NO.** |
| 4 | `5584c7a` | 08-28 | B3: thrust/RCS balancer (pure) — TCA torque-nulling + engine-out + translation | `pure/ThrustBalance.cs` 5,723 B; `pure/RcsBalance.cs` 2,514 B; `pure/DiffThrottle.cs` 1,778 B; `test/ThrustBalanceTest.cs` 6,226 B | **RECOVER-CODE** — directly serves §B16 (octaweb, engine-out). Applied by `Actuator.BalanceOctawebThrust`. **Regime: method is TCA's (mod-derived), constants are "researched defaults"** — `e90a63f`: Tuned **"○ — StepFactor/deadband researched; no engine-out or rendezvous flight in the corpus"**. **Flown?** The solver ran; **no engine-out event was ever flown.** See also `docs/RCS_BALANCE_FINDING.md` (deleted): *"wired as a recorded diagnostic; per-thruster application deliberately deferred with evidence."* |
| 5 | `a266420` | 08-28 | B8 (pure): CourseCorrect impact-divert solver + 4-band entry L/D prior | `pure/CourseCorrect.cs` 6,857 B; `test/CourseCorrectTest.cs` 6,581 B; also extended `pure/Trajectory.cs` with `UseLdBand` | **RECOVER-CODE** — finite-difference divert on top of `Trajectory`. Serves §B16 + entry. **Regime: the L/D bands are an explicitly-marked PRIOR, not measured** — the file says so: *"a predictor prior for the bands not yet measured. Predictor-only; it does NOT command the CoM shifter."* `e90a63f`: **"○ — no lifting-entry flight in the corpus"**. **Flown?** **NO.** |
| 6 | `9223ff9` | 08-27 | Add structural q-limit + g-limit aborts (mission-spec safety) | `pure/QAlpha.cs` 5,550 B (B2 q·α cap) + the abort thresholds in `pure/Fdir.cs` 12,034 B / `pure/AbortResponder.cs` | **RECOVER-CODE.** **Regime: RSS-RO researched, never DB-seeded** — `e90a63f`: B2 Tuned **"○ — aero-stiffness seed is researched; the estimator needs the isolated-aero FEED (owed I-B) before the DB can seed it"**. **Flown?** The **abort path is one of only two flight-validated subsystems** (the corpus has `ABORT/LaunchEscape` and `ABORT/SafeHold` phases); the **q/g thresholds themselves were never tripped in a recorded flight.** |
| 7 | `06b9f86` | 08-26 | Step C: ullage settle + clamp-release gate | `pure/IgnitionGate.cs` 2,502 B; `plugin/src/Ullage.cs` 3,917 B; `test/IgnitionGateTest.cs` 2,944 B | **RECOVER-CODE — HIGH PRIORITY for §B16.** `Ullage.cs` reads RealFuels propellant-settling state by reflection — **RO-specific and irreplaceable**. **Regime: RSS-RO** (RealFuels is an RO mod; there is no stock equivalent). **Flown?** YES, and **it is implicated in the booster loss**: `docs/FLIGHT_144114_SCREEN_AUDIT.md:35` records *"Booster ballistic (eng never lit) → LOST (= register H1b, ullage)"*. **Recover it and treat the ullage gate as an open defect, not a working part.** |
| 8 | `15738e8` | 08-28 | B1: StageStats — per-stage dV/TWR/burn-time + MECO recovery reserve (pure) | `plugin/src/pure/StageStats.cs` | ⚠ **CORRECTION — this file was NOT deleted.** It survives `8b81816` **byte-identical** (5,765 bytes at both `8b81816^` and `HEAD`) and is **live in today's tree** with `plugin/test/StageStatsTest.cs`. **Verdict: no recovery action — already present.** `e90a63f`: Build ✅, Tuned **"— physics only (G0, rocket eq); no tunables"**. |

---

### 3.5 `pure/Trajectory.cs` + `pure/BoosterDrag.cs` — **CONFIRMED. This is the strongest asset in the tree and the only RSS-RO empirical dataset that survives.**

#### The predictor — `plugin/src/pure/Trajectory.cs` (16,891 bytes / 346 lines)

Last touched `a266420` (2026-08-28). First written `2708961` (2026-08-11)
*"Our own trajectory predictor - drag MEASURED from the vehicle, not modelled"*.

⚠ **Its explanatory header was STRIPPED by `158eb2a`.** The last version with full commentary is
`0d6423d` (2026-08-26, literally *"BEFORE STRIPPING COMMENTS"*). Recover the commentary from there —
at `8b81816^` only the four banner lines survive and the file reads as unexplained. Verbatim from
`0d6423d`:

> **WHY WE HAVE OUR OWN INSTEAD OF TAKING TRAJECTORIES.** F9I gets its impact point from the Trajectories
> add-on. We do not take that dependency, and the vacuum solve we had instead is not good enough for
> everything: it is fine for a boostback that deliberately overshoots by 2.7 km, and useless for a
> de-orbit burn whose stop condition is a 50 m tolerance. Drag only ever shortens a trajectory, so a
> vacuum answer is always LONG, and by tens of kilometres on an entry.
>
> ⛔ **THE DRAG TERM IS MEASURED, NOT MODELLED.** This is the part that makes it worth having. KSP's drag
> comes from per-part drag cubes, occlusion and orientation; computing it analytically means
> reimplementing the game's aerodynamics and being wrong in a new way. Instead the glue **MEASURES** the
> vessel's actual drag acceleration in flight — total acceleration minus gravity minus thrust — and
> back-solves the ballistic coefficient:
>
>       a_drag = 0.5 * rho * v² / BC        ->        BC = 0.5 * rho * v² / a_drag
>
> `BC = m/(Cd·A)` carries mass, shape, orientation and occlusion in one number the vehicle tells us about
> itself. It is re-measured continuously, so a capsule that jettisons a trunk, deploys fins or turns
> broadside updates its own prediction without anyone writing down a coefficient. […] Measure the
> vehicle, do not describe it.
>
> **INTEGRATION.** RK4 in an inertial frame centred on the body. Gravity is Newtonian; drag acts along the
> **SURFACE-relative** velocity, because that is what the air sees. The body's rotation is carried
> separately so the impact point comes out as a **ground** position rather than an inertial one. […] The
> atmosphere is supplied as a **callback** so this file stays free of KSP. The glue passes
> `body.GetDensity(body.GetPressure(alt), body.GetTemperature(alt))`, which is the game's own model
> rather than an approximation of it.

**Why this is regime-portable by construction.** Everything body-specific is an *input*, not a constant:
`TrajectoryInputs` carries `Mu`, `BodyRadiusM`, `BodyOmega`, `AtmosphereDepthM`, `BallisticCoefficient`,
`ImpactAltitudeM`, and three delegates (`DensityAt`, `SpeedOfSoundAt`, `DragFactorAt`). **There is no
hardcoded planet in this file.** The only Kerbin reference anywhere in the pair is in prose (*"a 600 km
body turning once per six hours"*, written 2026-08-11) and in the test fixture.

`a266420` later added the lift model on top: `LiftToDrag`, `BankRad` (roll of the lift vector about the
velocity vector), and `UseLdBand` (the 4-band entry L/D **prior**). The file marks the prior honestly.

#### The drag curve — `plugin/src/pure/BoosterDrag.cs` (1,089 bytes / 32 lines)

Verbatim from `0d6423d`:

> PURE. The Falcon 9 booster's ballistic coefficient as a function of Mach — the empirical drag curve,
> **MINED FROM THE RECORDED CORPUS** (user 2026-08-25: *"use the flights to build the perfect trajectory
> predictions"*). **18,080 clean unpowered in-atmosphere descent samples across 48 recorded RSS/RO
> flights**, binned by Mach (median bc per 0.5-Mach bin):
>
>       Mach  0.5   1.0   1.5   2.0   2.5   3.0   3.5   4.0   4.5   5.0
>       bc    2582  1485  1796  1075  1331  1321  1481  1580  1582  1439   kg/m2
>
> ⛔ **WHY A CURVE, NOT A SCALAR (the bug this fixes).** The booster's bc is NOT constant — it drops ~2600
> (subsonic, low Cd) to ~1075 through the transonic drag rise (Mach 2), then ~1400-1580 hypersonic.
> Feeding the trajectory integrator ONE scalar bc (the last live measurement) mis-predicts wherever the
> Mach along the fall differs from where it was measured; worse, the live bc is HELD at a garbage
> near-vacuum value (~37000) through the entry burn — exactly when the burn needs to aim — so the
> predicted impact was tens of km wrong (`flight_0825_184857`: entry burn left the impact **25 km long**,
> drag then over-shortened it to **16 km short** of the barge). […] Reynolds is unused — the corpus is
> binned on Mach alone, which is what dominates the Falcon booster's Cd here.

#### Evidence of correctness

| Evidence | What it shows | What it does NOT show |
|---|---|---|
| `plugin/test/TrajectoryTest.cs` — **12,790 bytes**, the third-largest pure-module test in the tree (after `DispersionTest` 26,667 and `AttitudeLoopTest` 14,211) | Vacuum limit vs. analytic conic; drag monotonicity; body-rotation ground-shift; RK4 step convergence. Its own header: *"Kerbin's atmosphere, close enough for a test: sea level 1.225 kg/m³, scale height ~5600 m."* | **The fixture is STOCK Kerbin.** The test proves the integrator is arithmetically right; it proves **nothing about RSS-RO tuning**. That is fine — the integrator has no regime — but it is not RSS-RO validation. |
| `test/PredictTest.cs` — *"On a CIRCULAR orbit, time between anomalies is just the fraction of a period. Exact."* | The companion predictor (`pure/Predict.cs`) is checked against exact analytic answers | Also a Kerbin fixture |
| The `BoosterDrag` curve itself | **Real RSS-RO measured data** — 18,080 samples, 48 flights, median-binned | It is the booster's **drag**, measured on flights that mostly did **not** land. It validates the drag model; it does not validate any landing. |
| `flight_0825_184857` | The named failure the curve fixed (25 km long → 16 km short) — a real, quantified before-case | **No after-case is recorded in the repo.** No commit states the curve was re-flown and the miss measured. |

**The correctness claim, stated honestly:** the integrator is unit-proven against analytic answers in a
stock fixture; the drag curve is empirically derived from real RSS-RO flight data; **the combination was
never demonstrated to land a booster.** That is consistent with §4 — the booster was never recovered.

#### Verdict

| File | Regime | Ever flown? | Verdict |
|---|---|---|---|
| `plugin/src/pure/Trajectory.cs` | **n/a — body-agnostic by construction** (all body constants are inputs). Only the *prose* and the test fixture mention Kerbin. | Yes, as a live predictor on RSS-RO flights; its *accuracy* was measured once (the 0825 miss) | **RECOVER-CODE — HIGH PRIORITY. §B16 prediction engine.** Recover the `0d6423d` header commentary with it. |
| `plugin/src/pure/BoosterDrag.cs` | **RSS-RO — the only genuine RSS-RO empirical constant set in the tree.** | The samples ARE flight data | **RECOVER-CODE — HIGH PRIORITY. §B16.** ⚠ It is a **Falcon-9 booster** curve; it is not valid for the Dragon capsule or the S2. |
| `plugin/src/pure/Predict.cs` | Method ported from **F9I `COMMON/GNC.ks`** (stock) — but the ported thing is a *fixed-point damping scheme*, not a number. No constants. | Yes | **RECOVER-CODE.** §B16 + NAV impact readouts. |
| `plugin/test/TrajectoryTest.cs` | STOCK Kerbin fixture (deliberate, as an arithmetic check) | n/a | **RECOVER-CODE** with the module. |

⚠ **The raw evidence behind the curve is GONE.** `flight_0825_*.csv` and the other 47 RSS/RO booster
flights were **gitignored and never committed** (only the DS-ASC-00x and Crew-2 recordings were
force-added, from `a029fec` onward). The curve in `BoosterDrag.cs` and the aggregate in
`docs/tuning/TUNING_DB.json` are the **only surviving distillates**. If the curve is ever doubted it
cannot be re-derived from this repo. **Open Question Q2.**

---
## 4. What was EVER flight-validated

### 4.1 The Build-vs-Tuned status matrix — `e90a63f`, recovered verbatim

`e90a63f` (2026-08-28 05:32, *"plan: add Build + Tuned two-axis status matrix for B1-B11"*) added 27
lines to `docs/ULTIMATE_PLAN.md`. **That file is not in the audit set** — it was deleted earlier, at
`3b269b0` (2026-08-31, *"current build and plan"*). The matrix therefore survives **only inside the
commit**. Recovered here in full.

The commit message states the honest position:

> Per user request: track not just whether each backlog item is BUILT but whether its tunables are at the
> best DB-backed starting defaults. **Honest current state: the corpus covers only ascent control +
> abort**, so most items stay at researched defaults (marked defaults) until their I-B flight; **ascent
> control is DB-validated (pe_p95 < 0.4 deg)**.

And the matrix, verbatim:

> #### Build + Tuned status matrix (updated 2026-08-28)
>
> Two axes per item, so we always know both whether it is BUILT and whether its tunables are at the best
> data-backed starting defaults. **Build:** ○ not built · ½ first-cut · ✅ built. **Tuned:** **—** no
> tunables (pure math/physics) · **○** researched defaults, the DB has no data for this phase yet (can't
> DB-seed until I-B flies it) · **◐** DB-seeded (initial values set/validated from the corpus) · **✅**
> flight-tuned (I-B). ⚠ The tuning DB corpus today covers only **ascent control**
> (VerticalRise/GravityTurn/S2Burn/Coast) + **abort** — NO booster/rendezvous/docking/entry/chute data —
> so only ascent-coupled tunables are DB-seedable now; the rest stay ○ until their I-B flight.
>
> | Item | Build | Tuned | Tuning source / why |
> |---|---|---|---|
> | B1 StageStats | ✅ | — | physics only (G0, rocket eq); no tunables |
> | B2 q·α moderation | ✅+glue | ○ | aero-stiffness seed is researched; the estimator needs the isolated-aero FEED (owed I-B) before the DB can seed it |
> | B3 thrust/RCS balancer | ✅+glue | ○ | StepFactor/deadband researched; no engine-out or rendezvous flight in the corpus |
> | B4 actuator-lag | ✅+glue | ◐ | uses the LIVE measured gimbal `responseSpeed` → self-seeding, no static value to tune |
> | B5 primer-vector PVG | ○ | — | not built (last) |
> | B6 NavFilter | ✅ | ○ | IMU/RGPS noise tunables; no sensor-truth flight yet |
> | B7 Lambert + Maneuver | ✅ | — | universal-variable math; no tunables |
> | B8 entry predictor | ½→ | ○ | AoA-band schedule + step sizes; no lifting-entry flight in the corpus |
> | B9 GravityTurn auto-tuner | ○ | — | it IS the tuner — its output is the tuned ascent shape |
> | B10 V&V | ½ | — | test tooling; no tunables |
> | B11 FDIR authority | ○ | ○ | debounce/threshold tunables; only ascent+abort phases have data |
> | Ascent control (L2/L3) | ✅ | ◐ | **DB-VALIDATED: GravityTurn/S2Burn pe_p95 < 0.4°, sat_duty ≈ 0 across the corpus → current defaults are already good** |
>
> **I-A tuning goal:** move every item to **◐** where the corpus supports it; the ones stuck at **○** are
> the honest list of tunables that can only be seeded once their phase flies in I-B. Regenerate with
> `tools/tuning_db.py` after any new flight; re-seed here from `assess_flight.py` + the DB.

⚠ **Two things date this matrix.** It was written 2026-08-28 05:32; `b477c11` **built B5** later that
same day, and `15738e8` (B1) also lands 08-28. The **Build** column moved after this snapshot; the
**Tuned** column did not — no further flight moved anything off ○ except ascent, which was already ◐.

### 4.2 Per subsystem: FLOWN vs merely BUILT

| Subsystem | Built? | **FLOWN?** | Evidence |
|---|---|---|---|
| **Ascent control (L2/L3)** | ✅ | ✅ **YES — the only DB-VALIDATED subsystem.** `pe_p95 < 0.4°`, `sat_duty ≈ 0` for GravityTurn + S2Burn across the corpus | `e90a63f`; `docs/tuning/TUNING_DB.md` per-flight table |
| **Abort** | ✅ | ✅ **YES — flown, phases present in the corpus** (`ABORT/LaunchEscape`, `ABORT/SafeHold`), but no per-phase pointing statistic (`pe_*` = None on every abort row — the abort path did not populate the attitude columns) | `TUNING_DB.md` flight table |
| Actuation (`Actuator`) | ✅ | ✅ **YES** — every flight actuated through it | Implicit in every DS-ASC flight |
| Ullage / clamp gate | ✅ | ✅ flown — **and it FAILED**: booster engine never lit | `docs/FLIGHT_144114_SCREEN_AUDIT.md:35`, register H1b |
| **Booster recovery** | ✅ (gen-2 `BoosterControl` + `BoosterDescent` + `Hoverslam` + `GridFin`) | ❌ **NO. The booster was LOST.** *"Booster ballistic (eng never lit) → LOST"*. The best result recorded is `9f37e73` — a **prograde hold at separation**, i.e. attitude only, no recovery burn | `FLIGHT_144114_SCREEN_AUDIT.md:35`; `2fe1cef` (*"booster hold (V4)"*) |
| Rendezvous (far-field) | ✅ | ◐ **PARTIAL** — DS-ASC-008 reached **109 km**, *"9 km short of the 100 km near-field hand-off"* | `2fe1cef` |
| **Rendezvous (terminal) + docking** | ✅ | ❌ **NO — "terminal approach + dock STILL UNPROVEN"** | `2fe1cef` |
| Deorbit / entry / chutes | ✅ / ½ | ❌ **NO lifting-entry flight in the corpus.** One deorbit units-bug flight, DS-DEO-001 | `e90a63f` B8 row; `5681d18` |
| NavFilter (B6) | ✅ | ❌ **NO** — *"no sensor-truth flight yet"* | `e90a63f` |
| Thrust/RCS balancer (B3) | ✅ | ❌ engine-out **never flown**; wired as a **recorded diagnostic only** | `e90a63f`; `docs/RCS_BALANCE_FINDING.md` |
| Lambert / Maneuver (B7) | ✅ | ❌ never exercised end-to-end | `e90a63f` |
| UPFG / PEGAS (B5) | ✅ (after the matrix) | ❌ the multi-stage PEGAS form is unproven | `b477c11` vs `e90a63f` |
| FDIR full authority (B11) | ○ | ❌ observe-only, never acting | `e90a63f` |
| Attitude loop | ✅ | ✅ flown — **and it is the failure** (§2) | §2 |

**One-line summary of the flight record: the vehicle reached orbit reliably and aborted correctly. It
never recovered a booster, never completed a rendezvous, never docked, and never flew a lifting entry.**

### 4.3 Where the RSS-RO numbers actually live

| Artefact | Status | What it holds |
|---|---|---|
| `docs/tuning/TUNING_DB.json` + `.md` (deleted at `8b81816`, recoverable) | **RECOVER-REFERENCE — the RSS-RO tuning memory** | Per-phase statistics over a **55-flight** RSS/RO corpus (2026-08-26 → 08-29), with the derived authority metrics `act_sat` and `angacc_*_auth = ctrl_tq/moi`. 5 flights excluded as contaminated (`exclude.txt`). |
| `pure/BoosterDrag.cs` | **RECOVER-CODE** | The Mach-binned bc curve from an **earlier, separate 48-flight** RSS/RO corpus (to 2026-08-25) |
| `docs/reference/mechjeb_settings_type_Crew-Dragon.cfg` (**in today's tree**, named by CLAUDE.md C7) | Already the sanctioned input | The tuned MechJeb cfg |
| The raw CSVs behind both corpora | **GONE — gitignored, never committed** | — |
| `docs/flights/*.csv` (deleted, recoverable) | **RECOVER-REFERENCE** | The 12 force-added DS-ASC/DS-DEO/Crew-2 recordings — the only raw RSS-RO flight data that exists anywhere in this repo |

---

## 5. The file inventory — one row per file

Columns: `path` | `bytes` (at `8b81816^`) | `gen` | `last-touching commit + subject` | `PURPOSE`
| `REGIME` | `FLOWN?` | `VERDICT`.

Regime values: **RSS-RO** (constants derived in / for RSS+RO) · **stock** (constants from F9I/kOS or a
Kerbin fixture) · **n/a** (no physical constants — pure math, glue, schema, or docs).

### 5.1 `plugin/src/pure/` — deleted (62 files)

| path | bytes | gen | last commit | purpose | regime | flown? | verdict |
|---|---|---|---|---|---|---|---|
| `AbortResponder.cs` | 8,635 | 2 | `9223ff9` q/g-limit aborts | L5 FDIR: the self-aware, regime-correct abort action (7 abort regimes) | RSS-RO (thresholds researched) | **YES** (abort is flight-validated) | **RECOVER-CODE** — §B12.7 abort. Collides with stub `AbortControl`/`AbortMode` in `_AutopilotStub.cs:26,66` |
| `Actuation.cs` | 4,100 | 2 | `fe837f0` Step A: Actuator | PURE: the capability→role decisions the `Actuator` glue acts on (`EngineRoleOf`, `DecouplerRole`) | n/a | YES | **RECOVER-CODE — HIGH.** Hard dependency of `Actuator.cs` (§3.1). Companion `pure/VehicleParts.cs` survives today |
| `ActuatorLag.cs` | 2,975 | 2 | `70dc239` strip invented loops | B4 actuator-lag model + lead compensation | n/a | flown, then **removed as a defect** | ⛔ **OBSOLETE** — explicitly stripped by `70dc239` as a Claude invention; left dead in the tree. Do not restore |
| `Aero.cs` | 2,560 | 2 | `158eb2a` rebuild | L1 derived aerodynamic quantities (q, AoA, mach helpers) | n/a | YES | **RECOVER-CODE** |
| `Ascent.cs` | 8,447 | 2 | `9f37e73` booster prograde-hold | L3 first-stage ascent guidance + the ascent FSM | **RSS-RO — DB-VALIDATED** | **YES — the strongest RSS-RO asset** | **RECOVER-CODE — HIGH.** §B12.5 ascent |
| `AscentLoss.cs` | 2,941 | 2 | `158eb2a` rebuild | B9 ascent Δv-loss decomposition = the tuner objective | n/a (closed-form) | no | **RECOVER-CODE** |
| `AttitudeLoop.cs` | 8,026 | 2 | `70dc239` strip invented loops | L2 per-axis gimbal/RCS attitude law, ported from MechJeb `BetterController` | gains are **MechJeb's**, not ours | **YES — the failure** | ⛔ **RECOVER-REFERENCE ONLY** (§3.2). §B12.1's MechJeb embed replaces it |
| `Authority.cs` | 3,294 | 2 | `158eb2a` rebuild | L1 the vehicle's own control authority (torque/MOI) | n/a | YES | **RECOVER-CODE** |
| `AuthorityManager.cs` | 7,674 | 2 | `158eb2a` rebuild | Phase-2 control-authority arbitration layer | n/a | YES | **SUPERSEDED** by `plugin/src/pure/ScreenModes.cs:28` — today's `AuthorityManager` is a **display label only** (CLAUDE.md). Recovering the arbitration logic collides head-on |
| `BoosterDescent.cs` | 8,382 | 2 | `9f37e73` booster prograde-hold | L3 booster recovery FSM (boostback → entry burn → landing burn) | RSS-RO researched, **never DB-seeded** | ❌ **NO — booster LOST** | **RECOVER-CODE — §B16.** ⚠ unproven |
| `BoosterDrag.cs` | 1,089 | 2 | `158eb2a` rebuild | Falcon-9 booster bc-vs-Mach curve, mined from 48 RSS/RO flights | **RSS-RO — measured** | samples ARE flight data | **RECOVER-CODE — HIGH. §B16** (§3.5) |
| `Chutes.cs` | 7,535 | 2 | `9223ff9` q/g aborts | L3 return: drogue / main / splashdown sequence | RSS-RO (RealChute) | partially — chute descent recorded in aborts | **RECOVER-CODE** — §B12.7 return |
| `CoastEta.cs` | 3,077 | 2 | `158eb2a` rebuild | Mission-conductor: how long a coast lasts, so warp-to-manoeuvre has a target UT | n/a | YES | **RECOVER-CODE** — T15–T22 conductor |
| `Conic.cs` | 4,107 | 2 | `158eb2a` rebuild | L3 support: universal-variable two-body conic propagation | n/a | YES | **RECOVER-CODE** |
| `ControlLaw.cs` | 8,125 | 2 | `70dc239` strip invented loops | L2 attitude / throttle / translation laws | RealFuels-aware; gains MechJeb's | **YES — attitude half implicated** | **RECOVER-REFERENCE** for the attitude law; the **throttle limiter** is separable and recoverable as code (§7.2) |
| `CourseCorrect.cs` | 6,857 | 2 | `a266420` B8 impact-divert | B8 finite-difference impact-point divert solve | prior is **explicitly marked unmeasured** | ❌ NO | **RECOVER-CODE** — §B16 |
| `CrewGate.cs` | 4,765 | 2 | `158eb2a` rebuild | L4 crew-in-the-loop GATE state machine | n/a | YES | **RECOVER-CODE.** Collides with stub `Gate`/`ProcState`/`GatePhase` (`_AutopilotStub.cs:24-25`) and the live `pure/GateCard.cs` / `pure/StepList.cs` |
| `CrewGates.cs` | 6,094 | 2 | `06b9f86` Step C ullage | L4 the real Crew Dragon gate catalog, mission-as-data | n/a (procedure data) | YES | **RECOVER-CODE** — §1.4 source-of-truth material; do NOT edit without a real-source confirmation |
| `Cw.cs` | 5,557 | 2 | `158eb2a` rebuild | L3 rendezvous: Clohessy-Wiltshire terminal targeting | n/a | ❌ terminal never flown | **RECOVER-CODE** |
| `DeorbitGuidance.cs` | 6,169 | 2 | `e2f74aa` RSS ocean fix | L3 return: trunk jettison + the deorbit burn | RSS-RO (ocean detection fixed for RSS) | one units-bug flight (DS-DEO-001) | **RECOVER-CODE** |
| `Departure.cs` | 9,847 | 2 | `158eb2a` rebuild | L3 return: undock + departure-burn FSM (Phase 5) | n/a | ❌ NO | **RECOVER-CODE** |
| `DiffThrottle.cs` | 1,778 | 2 | `5584c7a` B3 balancer | B3 engine-out differential octaweb throttle | researched defaults | ❌ engine-out never flown | **RECOVER-CODE** — §B16 |
| `DockApproach.cs` | 5,803 | 2 | `158eb2a` rebuild | L3 docking: the R-bar→V-bar L-approach FSM | n/a (geometry) | ❌ NO | **RECOVER-CODE** |
| `DockCapture.cs` | 2,822 | 2 | `158eb2a` rebuild | L3 docking: IDSS soft-capture envelope gate | **real-world IDSS IDD Rev E Table 3.3.1.1-2** | ❌ NO | **RECOVER-CODE** — §1.4 verified-real source |
| `DockControl.cs` | 3,276 | 2 | `158eb2a` rebuild | L3 docking: the 6-DOF glideslope servo | n/a | ❌ NO | **RECOVER-CODE** |
| `DockCorridor.cs` | 2,712 | 2 | `158eb2a` rebuild | L3 docking: approach-corridor / KOS-breach test | real-world corridor geometry | ❌ NO | **RECOVER-CODE** |
| `Entry.cs` | 10,109 | 2 | `a266420` B8 | L3 return: lifting bank-angle entry guidance | L/D bands = **marked prior** | ❌ NO lifting entry flown | **RECOVER-CODE** |
| `FaultMonitor.cs` | 2,637 | 2 | `158eb2a` rebuild | L5 FDIR: the shared detect+debounce primitive | n/a | YES (observe-only) | **RECOVER-CODE** |
| `Fdir.cs` | 12,034 | 2 | `9223ff9` q/g aborts | L5 Fault Detection, Isolation, Recovery — the safety spine | RSS-RO thresholds, only ascent+abort have data | observe-only; **never acting** | **RECOVER-CODE.** Collides with stub `Fdir` (`_AutopilotStub.cs:60`) + `FdirReport`/`FaultKind`/`Recovery` |
| `FdirFeeds.cs` | 4,782 | 2 | `158eb2a` rebuild | L5 FDIR: honest residual shaping for the monitor feeds | n/a | YES | **RECOVER-CODE** |
| `FlightRecorder.cs` | 33,660 | 2 | `2fe1cef` DS-ASC-008 analysis | L7 the CSV schema (135 names) + invariant formatting + `Put*` fillers | n/a | **YES — produced the whole corpus** | ⛔ **RECOVER-REFERENCE ONLY** — BlackBox built fresh (§3.3) |
| `GridFin.cs` | 2,962 | 2 | `158eb2a` rebuild | L3 booster: grid-fin aero descent steering | RSS-RO researched, **never DB-seeded** | ❌ NO | **RECOVER-CODE** — §B16 |
| `Hoverslam.cs` | 4,684 | 2 | `158eb2a` rebuild | L3 booster: the drag-aware landing-burn ignition solver (√(2ad)) | ⚠ **anchors unstated in gen-2; gen-1 `HoverslamTest` cites "the real 0824 landing (v_term 244, 31 t, 1925 kN, spool 3.5 s)" — regime of that landing is NOT recorded** (§7.3) | ❌ **NO — never landed** | **RECOVER-CODE — §B16**, with the anchor regime treated as an open defect |
| `IgnitionGate.cs` | 2,502 | 2 | `06b9f86` Step C ullage | PURE: the clamp-release + ullage-settle decisions | **RSS-RO (RealFuels)** | **YES — and it FAILED** (booster engine never lit) | **RECOVER-CODE — HIGH, §B16.** Treat the ullage gate as an **open defect** (register H1b) |
| `Lambert.cs` | 4,228 | 2 | `18a690b` B7 | B7 Lambert two-point boundary-value solver | n/a | ❌ NO | **RECOVER-CODE** |
| `LaunchAzimuth.cs` | 2,866 | 2 | `158eb2a` rebuild | L3 ascent: the plane problem | RSS Earth geometry | YES | **RECOVER-CODE** |
| `LaunchTuner.cs` | 4,972 | 2 | `158eb2a` rebuild | B9 GravityTurn LaunchDB ascent-shape auto-tuner | RSS-RO corpus-driven | ❌ never run as a tuner | **RECOVER-CODE** |
| `LaunchWindow.cs` | 3,938 | 2 | `158eb2a` rebuild | L3 launch-to-rendezvous PLANE window / RAAN | RSS Earth | YES (ascent planned from it) | **RECOVER-CODE** |
| `Lvlh.cs` | 2,857 | 2 | `158eb2a` rebuild | L3 rendezvous: the target's local frame | n/a | YES (far-field) | **RECOVER-CODE** |
| `Maneuver.cs` | 2,298 | 2 | `18a690b` B7 | B7 maneuver-node / finite-burn library | n/a | ❌ NO | **RECOVER-CODE** |
| `MissionProfile.cs` | 7,178 | 2 | `158eb2a` rebuild | L-S0b mission-as-data resolver (4 free-flyer profiles) | n/a | partially | **RECOVER-CODE.** Collides with the live `pure/MissionPhase.cs` |
| `ModeManager.cs` | 7,647 | 2 | `158eb2a` rebuild | L4 the mission conductor / phase sequencer | n/a | YES | **RECOVER-CODE — T15–T22.** This is the closest existing thing to the planned "conductor" core |
| `NavFilter.cs` | 8,827 | 2 | `106a885` B6 | B6 per-axis 3-state Kalman (pos, vel, bias) translational nav filter | ⚠ **UNSTATED — defect (§7.3).** Noise tunables are researched, not measured | ❌ **NO** | **RECOVER-CODE** with the regime question open |
| `Phasing.cs` | 7,369 | 2 | `cfe3b5c` phasing self-deorbit fix | L3 rendezvous: FAR-FIELD phase-timed transfer + crew safety floor | RSS Earth (μ=3.986e14, R=6371 km in its test) | ◐ far-field flown to 109 km | **RECOVER-CODE** |
| `Predict.cs` | 5,032 | 2 (gen-1 origin) | `158eb2a` rebuild | Where we will be / hit / pass closest — `TimeTwoTA`, `GroundTrack`, `ImpactUT`, `ClosestApproach` | method from **F9I `COMMON/GNC.ks` (stock)**; **no constants** | YES | **RECOVER-CODE.** Recover the `0d6423d` commentary — the damped fixed-point rationale is the whole file |
| `QAlpha.cs` | 5,550 | 2 | `158eb2a` rebuild | B2 q·α moderation — the controllability-region AoA cap | RSS-RO **researched, not DB-seeded** | flown; never limiting | **RECOVER-CODE** |
| `RcsAccounting.cs` | 5,048 | 2 | `fff21b7` harden recorder | PHYSICS-RATE (50 Hz) RCS actuation accounting, sampled + reset per recorder interval | n/a | **YES — it settled the duty-cycle question** | **RECOVER-REFERENCE** — BlackBox §2.0's **R0 accumulator** is the generalisation of exactly this file. Read it before building R0 |
| `RcsBalance.cs` | 2,514 | 2 | `5584c7a` B3 | B3 RCS translation balancer — pure translation | researched defaults | diagnostic only | **RECOVER-CODE** |
| `RcsPulse.cs` | 4,381 | 2 | `70dc239` strip invented loops | PWPF / delta-sigma pulse modulation for an ON/OFF RCS axis | n/a | flown, then **removed as the root cause of "pulsing like crazy"** | ⛔ **OBSOLETE** — do not restore |
| `Rendezvous.cs` | 7,072 | 2 | `70dc239` strip invented loops | L3 the named-burn rendezvous FSM | RSS-RO | ◐ far-field only | **RECOVER-CODE** — ⚠ see `docs/RENDEZVOUS_REBUILD_PLAN.md` (deleted): *"a 2026-08-31 review found real DEFECTS"* |
| `Rls.cs` | 4,537 | 2 | `158eb2a` rebuild | L6 self-cal: Recursive Least Squares with variable forgetting | n/a | YES (estimating only) | **RECOVER-CODE** |
| `RvCoast.cs` | 1,429 | 2 | `70dc239` strip invented loops | Far-field rendezvous attitude gate (coast-release hysteresis) | n/a | flown, then **removed as a Claude invention** | ⛔ **OBSOLETE** — do not restore |
| `RvIntercept.cs` | 6,462 | 2 | `158eb2a` rebuild | Lambert two-impulse rendezvous INTERCEPT planner (tof scan + pe floor) | RSS Earth (test uses 3.986e14 / 6371) | ❌ NO | **RECOVER-CODE** |
| `SafeLandingSite.cs` | 3,023 | 2 | `e2f74aa` RSS ocean fix | Pick a SAFE splashdown along the ground track for a deorbit abort | **RSS** (the ocean-detection fix is RSS-specific) | ❌ NO | **RECOVER-CODE** |
| `SelfCal.cs` | 6,293 | 2 | `158eb2a` rebuild | L6 in-flight self-calibration — the concurrent RLS bank | RSS-RO (estimates the live vehicle) | YES (estimating) | **RECOVER-CODE** |
| `Terminal.cs` | 4,000 | 2 (gen-1 origin) | `158eb2a` rebuild | The last 7.5 km: chute vs propulsive landing mode, chute timing, SuperDraco cut | ⚠ **STOCK — the file itself says "⚠ THESE ALTITUDES ARE KERBIN'S, NOT THE REAL DRAGON'S"**; ported from F9I `dragon_deorbit.ks` | ❌ NO | ⛔ **RECOVER-REFERENCE.** The **ordering** (drogues out → engines proven lit → then cut) is a safety argument worth keeping; **every altitude must be re-derived for RSS** |
| `ThrustBalance.cs` | 5,723 | 2 | `5584c7a` B3 | B3 shared thrust-limiter balancing solver (TCA method) | method from TCA; StepFactor/deadband **researched** | diagnostic only | **RECOVER-CODE** — §B16 octaweb |
| `Trajectory.cs` | 16,891 | 2 (gen-1 origin) | `a266420` B8 | RK4 through-atmosphere ballistic predictor; drag term **measured**, not modelled; + lift/bank/L-D-band | **n/a — body-agnostic** (all constants are inputs) | YES as a predictor | **RECOVER-CODE — HIGH, §B16** (§3.5). Recover the `0d6423d` header |
| `Upfg.cs` | 14,428 | 2 | `b477c11` B5 multi-stage UPFG | L3 ascent: closed-loop second-stage guidance (PEGAS virtual stages) | **RSS** (named in-file) | predecessor flown; PEGAS form ❌ | **RECOVER-CODE** |
| `Vec3.cs` | 2,850 | 2 | `158eb2a` rebuild | L3 support: a minimal 3-D vector | n/a | YES | **RECOVER-CODE** |
| `WarpPlan.cs` | 4,068 | 2 | `158eb2a` rebuild | Mission-conductor: safe time-warp decisions so the autopilot never overshoots a burn | n/a | YES | **RECOVER-CODE — T15–T22 conductor.** Collides with stub `MissionConductor` (`_AutopilotStub.cs:72`) |
| `_DeorbitStub.cs` | 814 | 2 | `158eb2a` rebuild | TEMPORARY autopilot stub (pure side) | n/a | n/a | **SUPERSEDED** by `plugin/src/_AutopilotStub.cs` |

### 5.2 `plugin/src/` — deleted (24 files, incl. one 0-byte artefact)

| path | bytes | gen | last commit | purpose | regime | flown? | verdict |
|---|---|---|---|---|---|---|---|
| `=` | 0 | — | `158eb2a` rebuild | **0-byte junk file** — a shell redirection accident (`> =`) | n/a | n/a | ⛔ **OBSOLETE** — never restore |
| `AbortControl.cs` | 23,536 | 2 | `9223ff9` q/g aborts | KSP glue: the SELF-AWARE abort executor — 7 regimes, mode **LATCHED** at the first tick | RSS-RO | **YES — flight-validated** | **RECOVER-CODE — HIGH, §B12.7.** Collides with stub `AbortControl` (`_AutopilotStub.cs:66`) |
| `Actuator.cs` | 48,171 | 2 | `70dc239` strip invented loops | **DIRECT part-module control — the ONLY place that touches parts** (§3.1) | n/a | **YES** | **RECOVER-CODE — HIGHEST PRIORITY, §B12.7.** Collides with stub `Actuator` (`_AutopilotStub.cs:79`) |
| `AscentControl.cs` | 55,054 | 2 | `70dc239` strip invented loops | KSP glue seam 2: the ascent phase controller | **RSS-RO — DB-VALIDATED** | **YES** | **RECOVER-CODE — HIGH** ⛔ **carries the unfixed S2 roll-trim hysteresis defect at `:397-414`** (§7.1) |
| `AttitudeController.cs` | 20,434 | 2 | `70dc239` strip invented loops | The per-vehicle instantiable direct gimbal/RCS attitude loop | MechJeb gains | **YES — the failure** | ⛔ **RECOVER-REFERENCE ONLY** (§3.2) |
| `AttitudePilot.cs` | 5,310 | 2 | `70dc239` strip invented loops | Static facade over the active vessel's `AttitudeController` | MechJeb gains | **YES — the failure** | ⛔ **RECOVER-REFERENCE ONLY** (§3.2) |
| `BoosterControl.cs` | 23,306 | 2 | `9f37e73` booster prograde-hold | KSP glue seam 3: the first-stage recovery controller (non-active vessel, own `OnFlyByWire`) | RSS-RO | ❌ **NO — booster LOST** | **RECOVER-REFERENCE.** ⚠ CLAUDE.md: *"the deleted `BoosterControl` implementation still stays deleted"* — §B16 is a **separate-vessel autopilot** built fresh. Mine it for the **two-vessel focus/`FlightCtrlState` handover**, do not restore it. Collides with stub `BoosterRecovery` (`_AutopilotStub.cs:150`) |
| `BoosterLog.cs` | 4,161 | 2 | `9f37e73` booster prograde-hold | Per-recovery CSV writer for the **NON-ACTIVE** booster | n/a | YES | **RECOVER-REFERENCE** — BlackBox: how a second vessel gets its own recording stream |
| `BoosterTargeting.cs` | 7,595 | 2 | `9f37e73` booster prograde-hold | Land the booster ON the droneship / RTLS pad | ⚠ **UNSTATED — defect (§7.3).** Gen-1's `LandingSites.cs` states its coordinates are **F9I's (stock)**; gen-2's provenance is not recorded | ❌ NO | **RECOVER-REFERENCE** — §B16. **Every site coordinate must be re-surveyed in RSS before use** |
| `CraftDump.cs` | 7,737 | 2 | `158eb2a` rebuild | Dump the live craft's parts/modules to CSV (feeds `test/ActuationTest.cs`) | n/a | YES | **RECOVER-CODE** — read-only diagnostic; how the capability map stays real (§1.4) |
| `CrewProcedureOps.cs` | 20,016 | 2 | `967b07a` recorder fix | KSP glue: the crew-in-the-loop mission conductor | n/a | YES | **RECOVER-CODE — T15–T22.** Collides with stub `CrewProcedureOps` (`_AutopilotStub.cs:30`) |
| `DeorbitBurn.cs` | 6,733 | 2 | `e2f74aa` RSS ocean fix | The ONE Draco retrograde deorbit burn, shared by both callers | RSS-RO | one units-bug flight | **RECOVER-CODE** |
| `DeployablesControl.cs` | 2,828 | 2 | `158eb2a` rebuild | On-orbit solar/antenna deploy, pre-return retract | n/a | YES | **RECOVER-CODE** |
| `DockingControl.cs` | 15,447 | 2 | `70dc239` strip invented loops | KSP glue seam 5: terminal approach + docking | n/a | ❌ **NO — dock UNPROVEN** | **RECOVER-CODE.** Collides with stub `DockingOps` (`_AutopilotStub.cs:145`) — a **gen-1 name** (§5.4) |
| `EntrySteering.cs` | 9,687 | 2 | `a266420` B8 | The lifting bank-angle entry — footprint + bank measurement | L/D prior | ❌ NO | **RECOVER-CODE** |
| `FlightDriver.cs` | 59,523 | 2 | `70dc239` strip invented loops | The autopilot host: a flight-scene `KSPAddon`, owns `OnFlyByWire` | n/a | YES | **RECOVER-CODE — the Part-B host.** Collides with stub `FlightDriver` (`_AutopilotStub.cs:48`) |
| `FlightLog.cs` | 17,464 | 2 | `2fe1cef` DS-ASC-008 analysis | Per-flight CSV writer; owns the file + the 4 Hz sampling clock | n/a | YES | ⛔ **RECOVER-REFERENCE ONLY** — BlackBox fresh (§3.3) |
| `GeometryDump.cs` | 8,122 | 2 | `158eb2a` rebuild | READ-ONLY diagnostic; touches nothing in the control path | n/a | YES | **RECOVER-CODE** — safe, zero control risk |
| `LandingSiteScan.cs` | 4,364 | 2 | `e2f74aa` RSS ocean fix | Shared ground-track → safe-water splashdown site scan | **RSS** (the ocean fix is RSS-specific) | ❌ NO | **RECOVER-CODE** |
| `MissionConductor.cs` | 24,299 | 2 | `158eb2a` rebuild | Mission-level orchestration — time-warp + vessel focus | n/a | YES | **RECOVER-CODE — T15–T22.** Collides with stub `MissionConductor` (`_AutopilotStub.cs:72`). **Vessel focus is the §B16 two-vessel problem** |
| `RendezvousControl.cs` | 40,628 | 2 | `70dc239` strip invented loops | KSP glue seam 4: the coarse rendezvous controller | RSS-RO | ◐ far-field to 109 km | **RECOVER-CODE** ⚠ defects flagged in `RENDEZVOUS_REBUILD_PLAN.md`. Collides with stub `StationApproach` (gen-1 name) |
| `ReturnControl.cs` | 22,827 | 2 | `158eb2a` rebuild | KSP glue seam 6: the return — undock → splashdown | RSS-RO | ❌ NO | **RECOVER-CODE.** Collides with stubs `UndockOps` / `DeorbitOps` (gen-1 names) |
| `Steering.cs` | 8,741 | 2 | `70dc239` strip invented loops | Point the vehicle where the guidance says; holds the `UseGimbalLoop` custom-loop/stock-SAS switch | n/a | **YES — implicated** | **RECOVER-REFERENCE** ⛔ **carries the unfixed roll-release defect at `:102`** (§7.1); `UseGimbalLoop=false` is the last state — attitude was handed to stock SAS |
| `Ullage.cs` | 3,917 | 2 | `06b9f86` Step C ullage | Read RealFuels propellant-settling state via reflection | **RSS-RO (RealFuels) — irreplaceable, no stock analogue** | **YES — implicated in the booster loss** | **RECOVER-CODE — HIGH, §B16** (register H1b) |
### 5.3 `plugin/test/` — deleted (41 files)

Every test is GEN 2. A test's verdict follows its module: **RECOVER-CODE** with the module it proves,
**RECOVER-REFERENCE** if the module is reference-only, **OBSOLETE** if the module is obsolete.
Regime is **n/a** for the harness itself; the fixture regime is noted where it matters.

| path | bytes | last commit | purpose | fixture regime | verdict |
|---|---|---|---|---|---|
| `ActuationTest.cs` | 6,448 | `fe837f0` Step A | Capability→role map checked against **the real craft** (`data/craftdump.csv`, col 2) | real craft (RSS-RO install) | **RECOVER-CODE — HIGH** (with `Actuator.cs` + `pure/Actuation.cs`) |
| `ActuatorLagTest.cs` | 3,303 | `158eb2a` rebuild | B4 first-order actuator model + lead compensation | n/a | ⛔ **OBSOLETE** (module stripped by `70dc239`) |
| `AeroTest.cs` | 2,327 | `158eb2a` rebuild | Derived aero quantities vs textbook arithmetic | n/a | **RECOVER-CODE** |
| `AscentTest.cs` | 6,547 | `158eb2a` rebuild | Launch azimuth + the S1 pitch program + FSM | **RSS** (uses 6371 km) | **RECOVER-CODE** |
| `AttitudeLoopTest.cs` | 14,211 | `70dc239` strip | A representative axis: MOI ~3e5 kg·m², gimbal torque ~1e7 N·m (from `ATTITUDE_CONTROL_RESEARCH`) | **RSS-RO vehicle scale** | ⛔ **RECOVER-REFERENCE** — the *plant model* (MOI/torque scale) is genuinely useful for §B12.1 verification; the loop it tests is not |
| `AuthorityManagerTest.cs` | 5,767 | `158eb2a` rebuild | Claim/grant arbitration basics | n/a | **SUPERSEDED** (see `pure/AuthorityManager.cs`) |
| `AuthorityTest.cs` | 2,818 | `158eb2a` rebuild | The vehicle knowing its own limits | n/a | **RECOVER-CODE** |
| `BoosterTest.cs` | 8,991 | `158eb2a` rebuild | L3 booster recovery: hoverslam ignition solver + grid fins | ⚠ anchors' regime unstated (§7.3) | **RECOVER-CODE — §B16** |
| `CoastEtaTest.cs` | 3,488 | `158eb2a` rebuild | Coast-length estimate → warp target UT | n/a | **RECOVER-CODE** |
| `ConicTest.cs` | 3,695 | `158eb2a` rebuild | `Vec3` + universal-variable conic propagator | **RSS** (μ = 3.986e14) | **RECOVER-CODE** |
| `ControlTest.cs` | 6,924 | `70dc239` strip | L2 control laws — attitude rate/actuation, throttle limiter, RCS | RealFuels-aware | **RECOVER-CODE** for the throttle-limiter half; **REFERENCE** for the attitude half (§7.2) |
| `CourseCorrectTest.cs` | 6,581 | `a266420` B8 | Divert solve against a **KNOWN LINEAR impact model** — the decisive check | n/a (analytic) | **RECOVER-CODE** |
| `CrewGateTest.cs` | 8,695 | `06b9f86` Step C | Crew-gate state machine + the real gate catalog | n/a | **RECOVER-CODE** |
| `DispersionTest.cs` | 26,667 | `158eb2a` rebuild | **Tier-2 property-based dispersion** (`VALIDATION_AND_ROBUSTNESS.md` §Tier 2) — the largest test in the tree | RealFuels-aware | **RECOVER-CODE — HIGH.** This is §B10's V&V machinery and there is no replacement for it today |
| `DockCaptureTest.cs` | 2,178 | `158eb2a` rebuild | IDSS soft-capture envelope, **IDSS IDD Rev E Table 3.3.1.1-2** | real-world spec | **RECOVER-CODE** — §1.4 verified-real |
| `DockCorridorTest.cs` | 3,104 | `158eb2a` rebuild | Docking corridor / KOS-breach geometry | real-world | **RECOVER-CODE** |
| `DockingTest.cs` | 4,966 | `158eb2a` rebuild | Glideslope servo + L-approach FSM | n/a | **RECOVER-CODE** |
| `FdirAlertTest.cs` | 2,613 | `158eb2a` rebuild | Fault → alert mapping (no fault → nominal) | n/a | **RECOVER-CODE** |
| `FdirFeedsTest.cs` | 3,573 | `158eb2a` rebuild | Honest normalisation + the guards on FDIR feeds (task T2b) | n/a | **RECOVER-CODE** |
| `FdirTest.cs` | 13,737 | `9223ff9` q/g aborts | Debounced monitor primitive + the fault-detection spine | RSS-RO thresholds | **RECOVER-CODE** |
| `FlightRecorderTest.cs` | 9,258 | `2fe1cef` DS-ASC-008 | *"the schema is the single source of truth"* — index-drift + formatting guards | n/a | ⛔ **RECOVER-REFERENCE** — its **guard set** (index-never-drifts, invariant culture, CSV escaping) is directly reusable by a fresh BlackBox |
| `IgnitionGateTest.cs` | 2,944 | `06b9f86` Step C | Clamp release + ullage settle | RealFuels | **RECOVER-CODE — HIGH** (§B16 / H1b) |
| `LambertTest.cs` | 4,636 | `18a690b` B7 | **Self-inversion** against our own propagator — the decisive Lambert check | **RSS** (μ = 3.986e14) | **RECOVER-CODE** |
| `LaunchTunerTest.cs` | 5,213 | `158eb2a` rebuild | `AscentLoss` vs the closed-form value + the tuner | n/a | **RECOVER-CODE** |
| `LaunchWindowTest.cs` | 3,643 | `158eb2a` rebuild | Launch-to-rendezvous plane-crossing / RAAN window | RSS | **RECOVER-CODE** |
| `MissionProfileTest.cs` | 4,251 | `158eb2a` rebuild | Mission-as-data resolver | n/a | **RECOVER-CODE** |
| `NavFilterTest.cs` | 6,943 | `106a885` B6 | Per-axis 3-state Kalman (pos, vel, bias) | ⚠ unstated (§7.3) | **RECOVER-CODE** |
| `PhasingTest.cs` | 5,680 | `cfe3b5c` phasing fix | Far-field phase-timed Hohmann + the crew-safety floor | **RSS** (3.986e14, 6371) | **RECOVER-CODE** |
| `PredictTest.cs` | 10,205 | `158eb2a` rebuild | *"On a CIRCULAR orbit, time between anomalies is just the fraction of a period. Exact."* | **STOCK Kerbin fixture** (arithmetic check only) | **RECOVER-CODE** |
| `QAlphaTest.cs` | 5,238 | `158eb2a` rebuild | q·α cap + the `SelfCal.AeroPitchStiffness` online estimator | RSS-RO | **RECOVER-CODE** |
| `RcsAccountingTest.cs` | 3,197 | `fff21b7` harden recorder | Physics-rate attitude/translation split at known thrusts | n/a | **RECOVER-REFERENCE** — the R0 accumulator's proof (BlackBox §2.0) |
| `RcsPulseTest.cs` | 6,110 | `158eb2a` rebuild | PWPF / delta-sigma pulse modulation | n/a | ⛔ **OBSOLETE** (module stripped by `70dc239`) |
| `RendezvousMathTest.cs` | 4,392 | `158eb2a` rebuild | LVLH frame + Clohessy-Wiltshire two-impulse targeting | **RSS** | **RECOVER-CODE** |
| `RendezvousTest.cs` | 4,360 | `70dc239` strip | Named-burn FSM: phase progression on measured range | n/a | **RECOVER-CODE** |
| `ReturnTest.cs` | 14,562 | `e2f74aa` RSS ocean fix | Departure FSM + deorbit targeting + burn | **RSS** (3.986e14, 6.371e6) | **RECOVER-CODE** |
| `RvInterceptTest.cs` | 6,615 | `158eb2a` rebuild | Lambert intercept planner (tof scan + pe-floor) | **RSS** | **RECOVER-CODE** |
| `SelfCalTest.cs` | 5,403 | `158eb2a` rebuild | RLS-with-variable-forgetting + the self-cal bank | n/a | **RECOVER-CODE** |
| `ThrustBalanceTest.cs` | 6,226 | `5584c7a` B3 | The shared thrust-limiter balancing solver (TCA method) | n/a | **RECOVER-CODE — §B16** |
| `TrajectoryTest.cs` | 12,790 | `158eb2a` rebuild | Vacuum limit, drag monotonicity, body-rotation ground shift, RK4 convergence | **STOCK Kerbin fixture** — deliberate; proves arithmetic, not tuning | **RECOVER-CODE — HIGH, §B16** (§3.5) |
| `UpfgTest.cs` | 10,290 | `b477c11` B5 | *"A predictor-corrector's real validation is that it CONVERGES"* | **RSS** (3.986e14, 6371, names RSS) | **RECOVER-CODE** |
| `WarpPlanTest.cs` | 3,232 | `158eb2a` rebuild | The chosen on-rails warp rate can always be unwound before the burn | n/a | **RECOVER-CODE — T15–T22** |

### 5.4 `docs/` — deleted (60 markdown + 22 recordings + 3 tuning-DB files)

All GEN 2 unless noted. Docs have **no runtime regime**; the column records the regime of the *evidence
they contain*. None of these collide with anything in today's tree — they were all deleted outright.

**Governance / status docs — SUPERSEDED by `docs/BUILD_PLAN.md` + `REGISTER.md` (C7.1: THE PLAN WINS):**

| path | bytes | last commit | purpose | verdict |
|---|---|---|---|---|
| `MASTER_BUILD_SPEC.md` | 12,933 | `2fe1cef` DS-ASC-008 | *"ACTIVE · AUTHORITY. The one governing build specification"* | **SUPERSEDED** by `docs/BUILD_PLAN.md`. ⚠ Its **subsystem acceptance content** is not all reproduced there — mine before discarding |
| `RESUME_PROMPT.md` | 34,974 | `2fe1cef` | Session-start prompt; already self-marked *"⛔ SUPERSEDED FOR GOVERNANCE"* | **SUPERSEDED** by `CLAUDE.md` + `/next` |
| `SESSION_HANDOFF.md` | 5,125 | `7057328` RCS root cause | *"NEXT SESSION, DO THIS FIRST"* — a point-in-time handoff | **OBSOLETE** |
| `ISSUE_REGISTER.md` | 48,508 | `2fe1cef` | *"THE ENFORCEMENT MECHANISM for `[[fix-everything-means-exhaustive]]`"* | **SUPERSEDED** by `REGISTER.md`. ⚠ Mine for open defects before discarding — **register H1b (ullage) traces here** |
| `COMPLETION_MATRIX.md` | 10,263 | `2fe1cef` | One status grid per subsystem (FLIGHT-PROVEN / PARTIALLY / TESTED) | **SUPERSEDED** by §4.2 of this document + `BUILD_PLAN.md` |
| `FLIGHT_VERIFICATION.md` | 59,447 | `2fe1cef` | *"the record of what has actually been verified, at which level, with evidence"* | **RECOVER-REFERENCE — HIGH.** RSS-RO. This is the **primary evidence ledger** behind §4.2 and holds the DS-ASC-008 roll root-cause in full |
| `INTEGRATION_SCORECARD.md` | 4,813 | `2fe1cef` | C1 evidence artifact — the longest clean recorded prefix | **RECOVER-REFERENCE** (RSS-RO) |
| `PHASE_ACCEPTANCE_CRITERIA.md` | 6,209 | `cfe3b5c` | Per-phase first-flight acceptance criteria | **RECOVER-REFERENCE — HIGH.** Directly reusable for §B12.5's per-controller gates |
| `SOURCE_OF_TRUTH.md` | 3,417 | `158eb2a` | Declares the single authoritative owner of each spacecraft-state concept | **SUPERSEDED** by `docs/STATE_CONTRACT.md` (in today's tree) — ⚠ verify coverage before discarding |
| `DEPENDENCY_MATRIX.md` | 4,995 | `6ce70fd` mod-dep policy | Classification of the installed environment | **RECOVER-REFERENCE** — §B12.1 dependency policy |
| `OPERATING_PROCEDURE.md` | 8,490 | `2fe1cef` | What to press, when, and what the vehicle will do on its own | **RECOVER-REFERENCE** — the crew-facing contract the screens implement |
| `SEQUENCE_MAP.md` | 21,368 | `cfe3b5c` | *"what to do and when, with all the alternate paths per phase"* (Chris, 08-29) | **RECOVER-REFERENCE — HIGH.** The conductor's (T15–T22) sequence spec |
| `AI_REVIEW_HANDOFF.md` | 12,244 | `7057328` | Self-marked *"HISTORICAL REVIEW MATERIAL — a point-in-time snapshot"* | **OBSOLETE** |

**Research corpus — RECOVER-REFERENCE (this is the mined knowledge Part B is meant to stand on):**

| path | bytes | last commit | purpose | regime of evidence | verdict |
|---|---|---|---|---|---|
| `ABORT_PROCEDURES_RESEARCH.md` | 21,955 | `cfe3b5c` | The 7 abort regimes, from a real proving flight | **RSS-RO** | **RECOVER-REFERENCE — HIGH** (§B12.7) |
| `ASCENT_GUIDANCE_DECISION.md` | 4,235 | `cfe3b5c` | Why PVG/UPFG over the alternatives | RSS-RO | **RECOVER-REFERENCE** |
| `ASCENT_GUIDANCE_UPFG.md` | 8,508 | `b477c11` | *"The RSS ascent is the mission gate"* — the UPFG design | **RSS** | **RECOVER-REFERENCE — HIGH** |
| `ATTITUDE_CONTROL_RESEARCH.md` | 5,413 | `a8ac818` | *"a CONTROL-LAW problem, not an authority problem (S1 gimbal ±5°…)"* | RSS-RO | **RECOVER-REFERENCE — HIGH.** The frame-conversion source for §3.2; **also the record of the reasoning that failed** — read with §7 |
| `AUTOPILOT_HARVEST.md` | 37,484 | `158eb2a` | MechJeb2 + the other installed autopilot, and how they interfere | n/a | **RECOVER-REFERENCE — HIGH** (§B12.1) |
| `AUTOPILOT_MINING_3.md` | 8,152 | `cfe3b5c` | *"mine the OTHER autopilot systems for every nugget of gold"* | n/a | **RECOVER-REFERENCE** |
| `BOOSTER_DUAL_FLIGHT_RESEARCH.md` | 5,300 | `cfe3b5c` | *"the #1 named build gap — fly the booster to a landing WITHOUT sacrificing the [capsule]"* | RSS-RO | **RECOVER-REFERENCE — HIGHEST for §B16.** This is the two-vessel problem, researched. Cross-reference today's `docs/BOOSTER_RECOVERY_ARCHITECTURE.md` |
| `BOOSTER_GUIDANCE_DESIGN.md` | 7,256 | `cfe3b5c` | *"how did the real Crew-2 booster fly its atmosphere [phase]"* | real-world + RSS-RO | **RECOVER-REFERENCE — HIGHEST for §B16.** Cross-reference today's `docs/BOOSTER_GUIDANCE_METHOD.md` (untracked at session start) |
| `PHASE_2_BOOSTER_RECOVERY_RESEARCH.md` | 9,737 | `cfe3b5c` | Real-world facts, separation → droneship touchdown | real-world | **RECOVER-REFERENCE — HIGH (§B16)** |
| `PHASE_3_RENDEZVOUS_RESEARCH.md` | 9,821 | `cfe3b5c` | Real-world: separation → approach corridor | real-world | **RECOVER-REFERENCE** |
| `PHASE_4_DOCKING_RESEARCH.md` | 8,285 | `cfe3b5c` | Real-world: corridor → capture | real-world | **RECOVER-REFERENCE** |
| `PHASE_5_UNDOCKING_DEPARTURE_RESEARCH.md` | 5,279 | `cfe3b5c` | Real-world: hatch close → departure burn | real-world | **RECOVER-REFERENCE** |
| `PHASE_6_DEORBIT_ENTRY_SPLASHDOWN_RESEARCH.md` | 7,616 | `cfe3b5c` | Real-world: trunk jettison → splashdown | real-world | **RECOVER-REFERENCE** |
| `LAUNCH_AND_ASCENT_RESEARCH.md` | 20,082 | `cfe3b5c` | Source-backed reference for how SpaceX/NASA fly a crewed F9 | real-world | **RECOVER-REFERENCE** |
| `CREW2_REAL_MISSION_TECHNIQUES.md` | 8,907 | `158eb2a` | Crew-2 primary sources — the fidelity target | real-world | **RECOVER-REFERENCE** — §1.4 verified-real |
| `CREW2_RSS_RESEARCH.md` | 17,687 | `158eb2a` | *"Primary sources only… the fidelity target and the physics the guidance must obey"* | **RSS** | **RECOVER-REFERENCE — HIGH** |
| `CREW_DRAGON_GNC_RESEARCH.md` | 8,315 | `cfe3b5c` | Docking + entry fidelity, sensor limits | real-world | **RECOVER-REFERENCE** |
| `CREW_MISSION_TELEMETRY.md` | 17,739 | `158eb2a` | Telemetry from every crewed Crew Dragon mission | real-world | **RECOVER-REFERENCE** |
| `REAL_CREW_DRAGON_MISSION.md` | 9,752 | `158eb2a` | *"Every number here has a source"* | real-world | **RECOVER-REFERENCE** — §1.4 verified-real |
| `MISSION_PROFILES_FREEFLYER.md` | 5,340 | `cfe3b5c` | The 4 free-flyer mission profiles | n/a | **RECOVER-REFERENCE** |
| `RO_RSS_ENVIRONMENT.md` | 6,568 | `158eb2a` | *"the planet pack is **Sol** (a real-scale RSS-family…)"* — from the real configs | **RSS-RO — the regime definition itself** | **RECOVER-REFERENCE — HIGHEST.** This is the document that says what "RSS-RO" means in this install |
| `RO_MODS_MECHANICS.md` | 5,753 | `158eb2a` | RO mechanics, from the actual `GameData/` | **RSS-RO** | **RECOVER-REFERENCE — HIGH.** ⚠ C7 forbids re-reading the KSP install; **this doc is the sanctioned in-repo capture of it** |
| `RO_TESTFLIGHT_MECHANICS.md` | 7,367 | `158eb2a` | TestFlight ignition/reliability, from the real configs | **RSS-RO** | **RECOVER-REFERENCE — HIGH.** Bears directly on the ullage/ignition failure |
| `INSTALLED_MODS_RESEARCH.md` | 11,563 | `158eb2a` | Ground truth of the 114-mod RSS/RO install | **RSS-RO** | **RECOVER-REFERENCE** |
| `MOD_INVENTORY_RESEARCH.md` | 8,254 | `158eb2a` | Complete sweep of everything installed | RSS-RO | **RECOVER-REFERENCE** |
| `MOD_INTEGRATION_RESEARCH.md` | 13,265 | `6ce70fd` | ⭐⭐ the **mod-dependency policy** (optional vs RO/RSS hard-dep) | n/a | **RECOVER-REFERENCE — HIGH** (§B12.1 pins) |
| `MODS_HARVEST_2.md` | 10,570 | `cfe3b5c` | TCA / KerbalEngineer / ModularFlightIntegrator harvest | n/a | **RECOVER-REFERENCE** — the TCA source behind B3 |
| `MECHJEB_MASTER_MAP.md` | 34,827 | `cfe3b5c` | *"the single durable map of MechJeb's architecture, read from the source"* | n/a | **RECOVER-REFERENCE — HIGHEST for §B12.1** |
| `MECHJEB_WIKI_RESEARCH.md` | 12,308 | `967b07a` recorder fix | All 15 wiki pages, incl. **Attitude Adjustment (PIDs)** and the FlightRecorder §8 note | n/a | **RECOVER-REFERENCE — HIGH** |
| `MECHJEB_CAPABILITY_CHECKLIST.md` | 18,284 | `cfe3b5c` | Every MechJeb capability, as a tick-list for the owner | n/a | **RECOVER-REFERENCE — HIGH** (§B12.5 scope) |
| `MECHJEB_CAPABILITY_INTEGRATION.md` | 20,355 | `cfe3b5c` | *"list EVERY MechJeb capability useful to our build in ANY way"* | n/a | **RECOVER-REFERENCE — HIGH** |
| `MECHJEBLIB_PORT.md` | 6,719 | `158eb2a` | *"Status 2026-08-21: steps 1-3 COMPLETE and headless-validated"* — the MechJebLib port record | n/a | **RECOVER-REFERENCE — HIGHEST for §B12.1.** It documents the **gen-1 vendored `pure/mechjeblib/` tree** (§6.2) — a prior attempt at exactly what §B12.1 plans |
| `TRUE_AUTOPILOT_ARCHITECTURE.md` | 22,678 | `158eb2a` | *"a complete, standalone build guide for a genuine autonomous spacecraft autopilot"* | n/a | **RECOVER-REFERENCE — HIGH.** The architecture the conductor (T15–T22) is a descendant of |
| `FLIGHT_SOFTWARE_PLAN.md` | 11,565 | `14b8c2a` | 2026-08-06: *"use MechJeb2 and Trajectories as a base but tailor-made"* | n/a | **RECOVER-REFERENCE** — the original direction, and it is §B12.1's |
| `FLIGHT_SYSTEMS.md` | 16,912 | `14b8c2a` | Owner direction 08-05: build from MechJeb + Trajectories | n/a | **RECOVER-REFERENCE** |
| `VALIDATION_AND_ROBUSTNESS.md` | 9,413 | `cfe3b5c` | The Tier-1…Tier-4 V&V ladder (`DispersionTest` implements Tier 2) | n/a | **RECOVER-REFERENCE — HIGH (§B10)** |
| `TIME_WARP_RESEARCH.md` | 10,402 | `cfe3b5c` | *"never miss a manoeuvre"* (Chris, 08-29) | n/a | **RECOVER-REFERENCE — T15–T22** |
| `CRAFT_DUMP_VEHICLE_MAP.md` | 14,122 | `158eb2a` | *"controls the vehicle by capability, from the real craft — not by part name"* | **RSS-RO craft** | **RECOVER-REFERENCE — HIGH.** §1.4 source for `pure/Actuation.cs` |
| `VEHICLE_AUDIT.md` | 12,963 | `cfe3b5c` | ⭐ RCS modes, thruster limits, engine modes, throttle (Chris, 08-28) | **RSS-RO craft** | **RECOVER-REFERENCE — HIGH** |
| `RCS_BALANCE_FINDING.md` | 3,745 | `158eb2a` | *"assessed, wired as a recorded diagnostic; per-thruster application deliberately deferred with evidence"* | RSS-RO | **RECOVER-REFERENCE** |
| `RENDEZVOUS_REBUILD_PLAN.md` | 10,583 | `2fe1cef` | *"⛔ UNDER VERIFICATION — NOT AN INSTRUCTION, NOT APPROVED. A 2026-08-31 review found real DEFECTS"* | RSS-RO | **RECOVER-REFERENCE** — ⛔ **never treat as an instruction** (its own banner) |
| `RENDEZVOUS_RESEARCH_2026-08-20.md` | 24,878 | `158eb2a` | From `flight_0820_203834` (mono hit 0 at 210 m) | RSS-RO | **RECOVER-REFERENCE** |
| `F9I_PORT_MAP.md` | 23,795 | `158eb2a` | The F9I → C# port map, built after three flights were lost | ⚠ **STOCK (F9I)** | **RECOVER-REFERENCE** — ⛔ **method only; no number in it transfers** |
| `F9I_BOOSTER_TARGETS.md` | 2,972 | `158eb2a` | Measured from `bb_booster_001..008` in F9I's own black box | ⚠ **STOCK (F9I)** | **RECOVER-REFERENCE** — ⛔ **the numbers are F9I's stock landings. Do NOT seed §B16 from them.** Its *recording schema* is what transfers (owner) |
| `ASSESSMENT_VERIFICATION.md` | 21,859 | `2fe1cef` | Verification of the external assessment | n/a | **RECOVER-REFERENCE** |
| `CHATGPT_ASSESSMENT.md` | 41,633 | `158eb2a` | Verbatim external assessment | n/a | **RECOVER-REFERENCE** |
| `GROK_ASSESSMENT_PROMPT.md` | 5,830 | `158eb2a` | The prompt used for an external assessment | n/a | **OBSOLETE** — a prompt, not a finding |

**Flight recordings + tuning DB:**

| path | bytes | last commit | purpose | regime | verdict |
|---|---|---|---|---|---|
| `docs/flights/README.md` | 19,289 | `2fe1cef` | The flight table (DS-ASC-001…008, DS-DEO-001), the `act_*`/`app_*`/`acc_*` semantic warning, the format section, the column groups, the geometry-dump schema, and **four runnable stdlib-only Python reproductions** | **RSS-RO** | **RECOVER-REFERENCE — HIGHEST for BlackBox.** `BLACKBOX_RESEARCH.md` §3.2: *"already 90% of a recording-format spec… Salvage it before writing anything."* |
| `docs/flights/Crew-2_*.csv` (10 files) | part of 20.9 MB total under `docs/flights/` | `2fe1cef` / `23efb74` / `a029fec` | The force-added DS-ASC-003…008 + DS-DEO-001 recordings, incl. `Crew-2_20260901_004929.csv` (**the last flown file, 136 columns**) | **RSS-RO — the only raw flight data in the repo** | **RECOVER-REFERENCE — HIGHEST.** Irreplaceable |
| `docs/flights/Crew-2_Probe_*.csv` (3) | — | `2fe1cef` | The probe/target vessel's parallel recordings | RSS-RO | **RECOVER-REFERENCE** |
| `docs/flights/*_KSPlog_excerpt.txt` (2) | — | `a029fec` | KSP.log excerpts pinned to two flights | RSS-RO | **RECOVER-REFERENCE** |
| `docs/flights/Crew-2_deorbit_geometry_dump_manual_2500s.csv` | — | `5681d18` | Geometry dump for the deorbit units bug | RSS-RO | **RECOVER-REFERENCE** |
| `docs/flights/DS-ASC-008_geometry_dump_{manual_0s,pad}.csv` | — | `2fe1cef` | Pad + manual geometry dumps | RSS-RO | **RECOVER-REFERENCE** |
| `docs/flights/DS-ASC-008_screen{1,2,3}.png` | — | `2fe1cef` | Screenshots cross-checked against the CSV | RSS-RO | **RECOVER-REFERENCE** — the screen-vs-telemetry cross-check evidence |
| `docs/tuning/TUNING_DB.json` | 143,540 | `2fe1cef` | The machine-readable 55-flight per-phase control statistics | **RSS-RO** | **RECOVER-REFERENCE — HIGHEST.** §4.3 |
| `docs/tuning/TUNING_DB.md` | 60,713 | `2fe1cef` | The human-readable DB + the per-flight `pe_p95`/`sat_duty` verdict table | **RSS-RO** | **RECOVER-REFERENCE — HIGHEST** |
| `docs/tuning/exclude.txt` | 316 | `2fe1cef` | The 5 contaminated flights excluded from the pooled stats | RSS-RO | **RECOVER-REFERENCE** — the exclusion judgement is itself evidence |

**Build artefact:**

| path | bytes | last commit | purpose | verdict |
|---|---|---|---|---|
| `plugin/build/csc.rsp` | 10,629 | `158eb2a` rebuild | Compiler response file listing the source set | ⛔ **OBSOLETE** — it is **generated by `build.py` and gitignored today** (`.gitignore:27`); a copy exists in the working tree right now. Never restore the committed version |

### 5.5 Fast-path — the 119 files that still exist today

These were in the pre-deletion tree and survive in `HEAD`. **No recovery action.** They are listed as
groups because a per-file row adds nothing: none of them is autopilot code.

| group | count | changed since `8b81816^`? | note |
|---|---|---|---|
| `plugin/src/pure/` screen + model code (`Pages.cs`, `NavPage.cs`, `PanelMap.cs`, `VehicleSystems.cs`, `Orbital.cs`, `StageStats.cs`, `VehicleParts.cs`, …) | 42 | 11 changed (`Alarms`, `AttitudeHud`, `DisplayList`, `Images`, `KerData`, `MapProjection`, `MechPage`, `NavPage`, `PageAction`, `Pages`, `PanelMap`) | Part A. **`StageStats.cs` and `VehicleParts.cs` are the two that matter to Part B** — both unchanged, both live |
| `plugin/src/` KSP glue (`ScreenPainter`, `VesselData`, `KerBridge`, `PanelButtons`, `_AutopilotStub`, …) | 18 | 9 changed, incl. `_AutopilotStub.cs` | Part A + the idle seams |
| `plugin/test/` | 12 | 5 changed | Part A tests + `StageStatsTest`, `VehiclePartsTest` |
| `plugin/build/` + `plugin/tools/` Python | 11 | 2 changed | Includes **`tools/assess_flight.py` and `tools/tuning_db.py`, which read the DELETED recorder's schema** — see Q3 |
| `plugin/GameData/DragonScreen/` cfg + art | 9 | 1 changed | Shippable art |
| `plugin/reference_f9i/` | 6 tracked (a 7th file, `F9IScreen.dll`, is on disk but gitignored via `.gitignore:32 *.dll`) | 0 | §8 |
| `plugin/build.py`, `plugin/rasterise.py`, `plugin/preview/PreviewMain.cs`, one `__pycache__` blob | 4 | 1 changed | Build entry points |
| `docs/` Part-A docs (`SCREEN_SPEC`, `UI_AUDIT`, `PALETTE`, `INDEX`, `ARCHITECTURE`, `STATE_CONTRACT`, …) | 17 | 15 changed | Part A |
| **total** | **119** | 44 changed | |

---

## 6. GEN 1 — the pre-`158eb2a` generation

**Not in the audit set** (it is not in the `8b81816^` tree). `158eb2a` (2026-08-26) replaced 110 files
under `plugin/` with the L0–L7 rebuild. **Default classification: OBSOLETE.** Three exceptions were
found and are named below; everything else is superseded by its gen-2 equivalent.

### 6.1 The six files named in the brief

| gen-1 path (at `158eb2a^`) | bytes | purpose | regime | verdict |
|---|---|---|---|---|
| `plugin/src/pure/Landing.cs` | **104,499** | *"PURE. Booster recovery: boostback, entry burn, and the landing burn."* Its landing law is MechJeb's `ConstantThrustDescentSpeedPolicy` (`MechJebModuleLandingAutopilot.cs:563-567`), read from source | mixed — MechJeb law + F9I-derived constants | **OBSOLETE as code** (superseded by gen-2 `pure/BoosterDescent.cs` + `Hoverslam.cs` + `GridFin.cs`). ⚠ **EXCEPTION 1:** at 104 KB it is **8× the size of its gen-2 replacement set** — it contains descent logic the rebuild did not carry over. **Worth a targeted read for §B16 before it is written off** — see Q5 |
| `plugin/src/pure/LandingSites.cs` | 7,455 | Where a booster is allowed to come down, and which ascent flies it there | ⛔ **STOCK — the file says so: *"EVERY NUMBER HERE IS F9I'S, COPIED NOT DERIVED… `SPACEX/PARAM.ks`. The coordinates are surveyed against the actual pads in this install and the ascent constants are the ones those landings were flown with."*** | **OBSOLETE.** ⛔ Per the regime rule, **none of its numbers transfer.** Its *structure* (site table + per-site ascent binding) is the only transferable part |
| `plugin/src/BoosterRecovery.cs` | **141,148** | *"GLUE. Flies `pure/Landing.cs` on the BOOSTER after separation, hands focus over to it, and hands focus back to the upper stage when it is down."* The handover is *"copied from our own kOS interface, which already solved this — `Ships/Script/falcon9.ks:4723` FalconExtendRange and FalconFocusBooster"* | stock (F9I handover) | **OBSOLETE as code.** ⚠ **EXCEPTION 2:** the **focus-handover mechanism** is the §B16 two-vessel problem, already solved once here and once in gen-2's `BoosterControl.cs`. **RECOVER-REFERENCE for that mechanism only.** Note its name is still live in today's stub (`_AutopilotStub.cs:150`) |
| `plugin/src/FlightTrajectory.cs` | 7,786 | Draws the predicted descent trajectory, impact point and target over the **FLIGHT** view (owner, 08-24: *"green x on the centre of the barge so we can visually see how far off target we are"*) | n/a | **OBSOLETE as autopilot code** — ⚠ but it is a **screen** feature, and today's tree has no equivalent. Logged, not actioned (C1.1) |
| `plugin/src/MapTrajectory.cs` | 12,783 | Draws the predicted re-entry trajectory + impact point in the **MAP** view. *"⛔ THIS IS THE ADD-ON'S OWN MAP RENDERER, PORTED — the line/crosshair meshes, the screen-facing 'ribbon' edge and the ScaledSpace conversions"* | n/a | **OBSOLETE as autopilot code.** ⚠ **EXCEPTION 3:** it is a **working `ScaledSpace` map renderer**, and `docs/NAV_MAP_RENDERING_RESEARCH.md` (S61, in today's tree) is research into exactly this. **RECOVER-REFERENCE for the NAV map** — see Q5 |
| `plugin/test/HoverslamTest.cs` | 4,836 | *"Property-based: the ignition altitude must arrest the stage at the deck… Anchors mirror `scratchpad/hoverslam_val.py` against the real 0824 landing (v_term 244, 31 t, 1925 kN, spool 3.5 s)."* | ⚠ **UNSTATED — the regime of "the real 0824 landing" is not recorded anywhere** (§7.3) | **OBSOLETE** (gen-2 `BoosterTest.cs` supersedes). ⚠ It is the **only place any hoverslam anchor number is written down**, and its regime is unknown |

### 6.2 The one gen-1 asset the brief does not name — and it is the biggest

`158eb2a^` contains **`plugin/src/pure/mechjeblib/` — a 24-file vendored port of MechJebLib**:
`FuelFlowSimulation/` (14 files: `FuelFlowSimulation`, `DecouplingAnalyzer`, `FuelStats`, `SimVessel`,
`SimPart`, `SimPropellant`, `SimResource`, and 7 `PartModules/Sim*` shims), `Functions/Interpolants.cs`,
`Primitives/{H1,HBase,V3}.cs`, `Utils/{AsyncJob,DictOfLists,ObjectPool,Statics}.cs`, plus
`plugin/test/{FuelFlowTest,MechJebLibTest}.cs`.

`docs/MECHJEBLIB_PORT.md` records its status: *"2026-08-21: steps 1-3 COMPLETE and headless-validated.
Steps 4-6 (the KSP builder, wiring…)"* — i.e. **it was abandoned two-thirds done.**

| | |
|---|---|
| Generation | 1 |
| Regime | **n/a** — it is MechJeb's own simulation code, ported |
| Ever flown? | ❌ **NO** — never wired (steps 4-6 incomplete) |
| **Verdict** | **RECOVER-REFERENCE — HIGH, §B12.1.** §B12.1 plans to *"vendor PINNED source into the repo"*. **This repo has already attempted that once**, got two-thirds of the way, and wrote down why it stopped. Read `MECHJEBLIB_PORT.md` + this tree before starting §B12.1. **It is not a substitute for the pinned embed** — it is a partial hand-port of a different scope, and taking it would reintroduce the exact hand-port risk §B12.1 exists to avoid. |

### 6.3 Everything else in gen 1

The remaining ~80 files (`AutoPilot.cs`, `DockingOps.cs`, `EntryOps.cs`, `NodeExecutor.cs`,
`pure/Kepler.cs`, `pure/Attitude.cs`, `pure/Deorbit*.cs`, `pure/EntryGuidance.cs`, the gen-1 test set,
etc.) are **OBSOLETE** — each has a gen-2 successor of the same job, and `158eb2a` replaced them
deliberately. **No reason to recover any of them was found.** ⛔ Taking any gen-1 file alongside its
gen-2 counterpart produces duplicate types in the same `DragonScreen` namespace and will not compile.

---

## 7. The failure — which recovered files are implicated

**Read this before making any recovered file live.**

### 7.1 Directly implicated — a named, located, UNFIXED defect

| File | Defect | Status |
|---|---|---|
| `plugin/src/AscentControl.cs` **`:397-414`** | The S2 roll-trim hysteresis. *"sawtooths roll to 27.5 dps + toggles RCS 17x + 2 Hz gimbal chatter = the shake"* | ⛔ **UNFIXED.** The fix (*"a continuous roll-only deadband everywhere"*) was **deliberately withheld** because it *"touches PROVEN ascent"* and needed owner review + a V4 re-fly that never happened (`2fe1cef`) |
| `plugin/src/Steering.cs` **`:102`** | Rendezvous releases the attitude channel on coast and **never angle-holds roll** = the mission roll drift, the separation tumble, and the 17% detumble fuel | ⛔ **UNFIXED**, same reason |
| `plugin/src/pure/IgnitionGate.cs` + `plugin/src/Ullage.cs` | The ullage/ignition gate that let the **booster engine never light** → booster LOST | ⛔ **UNFIXED** — register **H1b** |

### 7.2 Implicated as the failed control loop — RECOVER-REFERENCE only

| File | Why |
|---|---|
| `plugin/src/AttitudePilot.cs` | ⛔ Owner directive: reference only |
| `plugin/src/AttitudeController.cs` | The stateful loop; same directive |
| `plugin/src/pure/AttitudeLoop.cs` | The pure law the two above wrap |
| `plugin/src/pure/ControlLaw.cs` | Its **attitude** half is the same lineage. Its **throttle limiter** and **translation** law are separable and are not implicated — split the file rather than recovering or discarding it whole |
| `plugin/src/Steering.cs` | The custom-loop/stock-SAS switch (`UseGimbalLoop`) and the roll-release defect. **Its last committed state is `UseGimbalLoop = false` — attitude handed to stock SAS.** Recovering it as-is recovers that state |
| `plugin/src/Actuator.cs` → `RcsInducedTorque` | Part of the attitude path; the rest of the file is not |

### 7.3 Removed as defects — OBSOLETE, never restore

`plugin/src/pure/RcsPulse.cs` · `plugin/src/pure/ActuatorLag.cs` · `plugin/src/pure/RvCoast.cs`
· `plugin/test/RcsPulseTest.cs` · `plugin/test/ActuatorLagTest.cs`
— plus the phase-plane hold deadband and the 1.5× hold-authority scale, which `70dc239` already removed
from `AttitudeLoop.Axis`'s parameter list. All five files sit dead in the pre-deletion tree.

### 7.4 Constants with NO STATED REGIME — defects, per §0.1

Each of these is a `RECOVER-*` file whose numbers cannot be attributed to a regime from anything in this
repo. **Every one must have its regime established before the number is used.**

| File | The unattributed constants |
|---|---|
| `plugin/src/pure/NavFilter.cs` | The IMU / RGPS process- and measurement-noise tunables. `e90a63f` says *"researched"* — the research source is not named in the file |
| `plugin/src/pure/Hoverslam.cs` + `plugin/test/BoosterTest.cs` | The ignition-solve anchors. The only written anchor set is gen-1 `HoverslamTest.cs`'s *"the real 0824 landing (v_term 244, 31 t, 1925 kN, spool 3.5 s)"* — **and whether that landing was ours (RSS-RO) or F9I's (stock) is recorded nowhere** |
| `plugin/src/BoosterTargeting.cs` | Droneship / RTLS site coordinates and approach geometry. Gen-1's `LandingSites.cs` states plainly that its equivalents are **F9I's, stock**; gen-2 does not state its provenance at all |
| `plugin/src/pure/ThrustBalance.cs` / `RcsBalance.cs` / `DiffThrottle.cs` | `StepFactor` and the deadbands — *"researched"* (`e90a63f`), source not named in-file |
| `plugin/src/pure/QAlpha.cs` | The aero-stiffness seed — *"researched"*, source not named in-file |
| `plugin/src/pure/Entry.cs` / `CourseCorrect.cs` | The 4-band L/D schedule — **honestly self-marked as a prior**, so this is a *disclosed* gap rather than a hidden one, but it is still an unmeasured number |

### 7.5 The qualifier — this failure does not predict §B16

Restating §2.3 because it governs how §7.2 is read: everything in §7.1–7.2 is **ascent + Dragon-RCS
attitude hold** — an ON/OFF 0.4 kN thruster set and a single-MVac stage with **zero roll authority for
79% of S2**. Booster landing is a different plant: gimbal authority proportional to a high and deeply
throttleable thrust, grid fins as the primary descent authority, dominant aerodynamics, and a terminal
solve measured in seconds. **A recovered booster controller is not condemned by this failure — and it is
not exonerated by it either.** It has its own, entirely separate, never-flown status (§4.2).

---

## 8. `plugin/reference_f9i/` — recorded, not audited (that is R3)

**It still exists in the working tree**, in no `docs/INDEX.md` entry and on no `REGISTER.md` line. Seven
files (6 tracked; the compiled `.dll` is on disk but gitignored), all last touched by `14b8c2a` (2026-08-10) — the repo's second commit — and **never modified
since**: `BridgeProtocol.cs` (8,288 B), `BridgeTest.cs` (11,758 B), `F9IBridgeModule.cs` (3,924 B),
`F9IScreenAddon.cs` (24,858 B), and `GameData_F9IScreen/` holding `F9IScreen.cfg`, a compiled
`F9IScreen.dll` (15,872 B) and `dragon_page_58.png` (254 KB).

It appears to be **a complete, self-contained earlier mod — "F9IScreen" — that put a Crew-Dragon panel on
screen as a `KSPAddon(Flight)` GUI window driven by kOS.** `BridgeProtocol.cs` is a pure, headless-tested
wire format between kOS and the panel: thirteen fields packed into one `~|~`-separated string in a single
`guiActive` PartModule field, because *"kOS reads and writes a PartModule field with `GETFIELD`/`SETFIELD`
and it REQUIRES `guiActive = true`… Thirteen fields here would be thirteen rows"*, and because kOS cost
*"is the entire reason this port exists"*. `F9IScreenAddon.cs` is its glue and carries the rule this repo
still runs on — *"THE BUG IS IN THE GLUE, AND THE GLUE IS THE PART WITH NO TESTS."* It also documents why
the screen is a `KSPAddon(Flight)` rather than a `PartModule`, and that click-through is handled by the
already-installed `ClickThroughBlocker` rather than hand-rolled. Note `dragon_page_58.png` — today's tree
has `pure/Frame58Hud.cs` and `pure/FigmaFramePage.cs`, so this asset is plausibly the ancestor of a live
DragonScreen page.

Per the regime rule it is **F9I lineage and therefore stock-only** for anything numeric; what it holds is
**method** — the bridge protocol design, the tolerant-parse discipline, the glue/pure split — and one
possible art provenance. **A shipped compiled DLL sitting untracked-by-docs in the tree is itself worth a
decision** (Q6). Full audit deferred to **R3**.

---
## 9. Verdict roll-up

Counted from the tables in §5, not estimated.

| Verdict | `pure/` | `src/` | `test/` | `docs/` + build | **total** |
|---|---|---|---|---|---|
| **RECOVER-CODE** | 52 | 16 | 35 | 0 | **103** |
| **RECOVER-REFERENCE** | 5 | 7 | 3 | 62 | **77** |
| **SUPERSEDED** | 2 | 0 | 1 | 5 | **8** |
| **OBSOLETE** | 3 | 1 (`=`) | 2 | 3 + `csc.rsp` | **10** |
| **rows** | 62 | 24 | 41 | 71 | **198** |
| **files covered** | 62 | 24 | 41 | 86 | **213** |
| **Fast-path (§5.5, already present)** | 42 | 18 | 12 | 47 | **119** |
| | | | | | **332** |

The `docs/` + build column has fewer rows (71) than files (86) because the 22 flight recordings and
`csc.rsp` are grouped: `Crew-2_*.csv` (10), `Crew-2_Probe_*.csv` (3), the KSP-log excerpts (2), the
DS-ASC-008 geometry dumps (2) and the DS-ASC-008 screenshots (3) each get one row. `ControlLaw.cs` /
`ControlTest.cs` are counted once, under RECOVER-CODE, with the split noted in §7.2.

**The recovery, in one sentence:** restore 103 files of pure guidance, glue and tests — led by
`Actuator.cs` + `pure/Actuation.cs`, `pure/Trajectory.cs` + `pure/BoosterDrag.cs`, `pure/Ascent.cs`, and
`pure/IgnitionGate.cs` + `Ullage.cs` — read 77 more as evidence without making them live, leave the
attitude loop dead, and never restore the five files `70dc239` already condemned.

---

## 10. Verification (C1.3)

- ✅ Every file in the pre-deletion `plugin/` and `docs/` trees has a row and a verdict: **332 = 213
  deleted (§5.1–5.4) + 119 fast-path (§5.5)**, derived from `git ls-tree` and cross-checked with `comm`.
- ✅ `python plugin/build.py test` — **GREEN** (no code was changed; run recorded in the session).
- ✅ `git diff` is **empty**; `git status` shows **one new file from this task**
  (`docs/AUTOPILOT_RECOVERY_AUDIT.md`) — plus `docs/BOOSTER_GUIDANCE_METHOD.md`, which was **already
  untracked before this task began** and is deliberately left uncommitted. No file was restored.
  `REGISTER.md`, `CLAUDE.md`, `BUILD_PLAN.md`, `PanelMap.cs` and the label docs were **not touched**.
- ✅ Sources: this repo's git history only (C7). No other repo, no KSP install, no external URL.
- ✅ R1 completed without splitting; R1a/R1b/R1c were not needed.

---

## Open questions for the owner

*(C1.14 format — every question is in this document, with numbered options and a recommendation.
This audit decides none of them and proceeds past none of them. C1.12: a build chat never
self-authorises a gate, an `OVERRIDE`, or a plan change.)*

---

### Q1 — What is the ORDER of recovery, and does R1's inventory become register lines now?

**Situation.** 103 files are marked RECOVER-CODE. They are not independent: `Actuator.cs` needs
`pure/Actuation.cs` + `test/ActuationTest.cs`; the booster set needs `Trajectory` + `BoosterDrag` +
`Predict` + `Vec3` + `Conic`; six recovered files collide with `_AutopilotStub.cs` classes the screens
compile against. Recovering them in the wrong order gives a tree that does not build, and the
preview-only gate means a broken tree is the only feedback available.

**Options.**
1. **Dependency-ordered restore in four waves, one register task per wave, each ending green.**
   Wave A = `pure/` support with no collisions (`Vec3`, `Conic`, `Trajectory`, `BoosterDrag`, `Predict`,
   `Aero`, `Authority`, `Lambert`, `Maneuver`, `Lvlh`, `Cw`) + their tests. Wave B = `Actuation` +
   `Actuator` + `ActuationTest` (retires the `Actuator` stub). Wave C = the booster set (§B16).
   Wave D = the conductor set (`ModeManager`, `WarpPlan`, `CoastEta`, `MissionConductor`,
   `CrewProcedureOps`) — this is where most stub collisions land.
2. **Restore everything RECOVER-CODE in one commit, then fix the build.** Fastest to a complete tree;
   worst feedback loop.
3. **Restore nothing yet.** Treat this audit as the map and let §B12 pull each file in as its own task
   needs it.
4. **Restore only what §B16 needs**, defer the rest until after the first booster flight.

**My recommendation: option 1.** Each wave is a task that can end green under the existing
preview-only go, the dependency order is already known from §5, and the stub collisions get retired one
at a time in the order §B12.5 wants ("one controller at a time"). Option 3 sounds disciplined but
repeats the audit work inside every later task; option 2 puts the tree in a state no preview can judge.
**Needs an owner decision, plus new `REGISTER.md` lines — which this chat has not written (C1.11: R1's
declared outputs are one doc and one commit).**

---

### Q2 — The RSS-RO flight corpus is gone. Do we re-fly to rebuild it, or trust the distillates?

**Situation.** Two RSS-RO corpora produced every number we have: a 48-flight booster-descent corpus (to
2026-08-25) behind `BoosterDrag.cs`'s bc curve, and a 55-flight corpus (08-26 → 08-29) behind
`docs/tuning/TUNING_DB.json`. **Neither corpus's raw CSVs are in this repo** — they were gitignored and
never committed. Only 12 later recordings were force-added. So the two most valuable RSS-RO artefacts
are **distillates that cannot be re-derived or re-checked from anything in the repo**, and the one
quantified accuracy datum for the drag curve is a *before* figure with no *after*.

**Options.**
1. **Trust both distillates as the RSS-RO seed and re-validate on the first recorded flight.** Recover
   `BoosterDrag.cs` and `TUNING_DB.*` as-is, mark both `PROVENANCE: distillate, raw data lost`, and
   treat the first §B16 flight as the check.
2. **Re-fly to rebuild the corpus before §B16 trusts any of it.** Costs glass time (a separate gate) and
   a working recorder (the BlackBox, not yet built).
3. **Use the curve only as a shape, not a magnitude** — recover the Mach-dependence but re-scale it from
   the vehicle's own live measured bc each flight (which `Trajectory.cs` already measures continuously).
4. **Discard `BoosterDrag.cs` and predict on live-measured bc alone** — i.e. revert the exact bug the
   file was written to fix.

**My recommendation: option 3, falling back to option 1.** The file's own header says the failure mode
was a *scalar* bc held at a garbage value through the entry burn; the *shape* of the curve is the fix and
the shape is far more robust to a changed vehicle than the absolute values. Blending the live measurement
with the corpus shape keeps the fix and removes the dependence on a dataset we can no longer inspect.
Option 4 knowingly reintroduces a diagnosed 25 km error. **Owner call — it decides what §B16 is allowed
to seed from.**

---

### Q3 — `tools/assess_flight.py` + `tools/tuning_db.py` survive and read the DELETED recorder's schema. What happens to them when the BlackBox is built fresh?

**Situation.** Both analysers are live in today's tree and are, per `BLACKBOX_RESEARCH.md` §3.2, *"the
report generator"* — nine sections, hard-won `is_warp()` filtering, vis-viva self-checks, and the
authority metrics `act_sat` / `angacc_*_auth` that produced the roll root-cause. They glob `Crew-2*.csv`
and index the deleted 136-column schema by name. A fresh BlackBox schema (`BLACKBOX_RESEARCH.md` §4.1's three streams + manifest
+ blank-not-guess) breaks both on day one, and `docs/tuning/TUNING_DB.json` becomes unreproducible.

**Options.**
1. **Carry the old column names forward into the new schema wherever the quantity is the same**, so both
   analysers keep working and the old corpus stays comparable to the new.
2. **Port both analysers to the new schema as part of the BlackBox task**, and treat the old corpus as
   read-only history.
3. **Write a shim** that projects a new-format recording down to the old 136-column CSV, so the analysers
   are untouched.
4. **Let them break**; write a new report generator from the manifest.

**My recommendation: option 1 plus option 2** — name-compatibility where the quantity is genuinely the
same (it costs nothing and preserves cross-corpus comparison), and an explicit port of both analysers
inside the BlackBox task rather than after it. Option 3 adds a format nobody reads; option 4 discards the
single best piece of tooling that survived the deletion. **This is an input to the BlackBox task's scope,
so the owner should settle it before that task starts.**

---

### Q4 — The screens compile against GEN-1 class names. Which vocabulary wins?

**Situation.** `_AutopilotStub.cs` keeps `AutoPilot`, `StationApproach`, `DockingOps`, `DeorbitOps`,
`UndockOps` and `BoosterRecovery` — all **gen-1** names — alongside the gen-2 ones (`FlightDriver`,
`CrewProcedureOps`, `Fdir`, `Actuator`, `MissionConductor`, `AbortControl`, `FlightCommands`,
`MissionOps`). The gen-2 modules that actually do those jobs are named differently
(`RendezvousControl`, `DockingControl`, `ReturnControl`, `BoosterControl`). CLAUDE.md states the contract
the screens compile against **does not change** — so something has to bridge, and the plan does not
currently say what.

**Options.**
1. **Keep the stub names as the display-facing façade**; recovered gen-2 controllers register into them.
   The screens never change; `_AutopilotStub.cs` becomes a thin adapter instead of a no-op.
2. **Rename the recovered gen-2 controllers to the gen-1 names** the screens already use.
3. **Change the screen call sites** to the gen-2 names and delete the gen-1 aliases.
4. **Decide per name** as each controller lands under §B12.5.

**My recommendation: option 1.** It is the only one that honours CLAUDE.md's "the contract does not
change" literally, it keeps Part A untouched (protecting the scarce restarts), and it matches §B12.5's
one-controller-at-a-time shape — each controller flips one façade property from `false` to live. Option 3
touches `PanelMap.cs`-adjacent screen code for no display benefit. **Owner call, because it is a change
to how §B12.5 is understood to work.**

---

### Q5 — Two gen-1 files are exceptions to "gen 1 is obsolete". Do we open them?

**Situation.** §6.1 found three gen-1 exceptions, two of which are actionable now.
(a) `pure/Landing.cs` is **104 KB** — roughly 8× its combined gen-2 replacements
(`BoosterDescent` 8.4 KB + `Hoverslam` 4.7 KB + `GridFin` 3.0 KB). The rebuild did not carry most of it
over, and §B16 is about to build booster recovery again from a much thinner base.
(b) `MapTrajectory.cs` is a **working `ScaledSpace` map renderer**, ported from the Trajectories add-on,
and `docs/NAV_MAP_RENDERING_RESEARCH.md` (S61, last session) is research into exactly that problem.
⛔ Both are gen 1; taking either as *code* alongside gen-2 gives duplicate types.

**Options.**
1. **Open both as READ-ONLY reference in a scoped follow-up task** (R2?), producing a written extract of
   what gen-2 dropped and what the map renderer does — no code restored.
2. **Open `Landing.cs` only** (§B16 is imminent; NAV map is not).
3. **Open `MapTrajectory.cs` only** (it is the smaller, better-defined win, and S61 has a live question).
4. **Open neither.** Accept gen 2 as the baseline and build §B16 from it.

**My recommendation: option 1, as one small read-only task before §B16 starts coding.** A 104 KB file
that a rebuild replaced with 16 KB either contains 88 KB of things that were wrong, or 88 KB of things
that were hard — and §B16 should know which before it re-derives them. The same task can settle the NAV
map question S61 left open. **Needs an owner decision and a register line; R1's declared outputs do not
include creating one.**

---

### Q6 — `plugin/reference_f9i/` is undocumented and ships a compiled DLL. Where does it belong?

**Situation.** Seven files, untouched since 2026-08-10, in no `docs/INDEX.md` entry and on no register
line — including a **compiled `F9IScreen.dll` (15,872 B)** and a 254 KB PNG. It is F9I lineage, so
**stock-only** for anything numeric (§0.1), but its bridge-protocol design and glue/pure discipline are
genuinely instructive. R3 is meant to audit it.

**Options.**
1. **Leave it, and add an `INDEX.md` entry + a register line now** so it stops being invisible; R3 audits
   it properly later.
2. **Move it under `assets/`**, which CLAUDE.md C7.1 already defines as *"REFERENCE — look, don't ship"*.
3. **Delete the compiled `.dll` only**, keep the source as reference.
4. **Leave it entirely alone until R3.**

**My recommendation: option 1.** The problem today is not where it lives, it is that nothing in the docs
admits it exists — and C7.1's "look, don't ship" rule cannot be applied to a directory no index mentions.
An index entry costs one line and removes the ambiguity; moving or pruning it is R3's call with the full
picture. ⚠ Options 2 and 3 modify files outside R1's declared outputs and **this chat has not done
either**.

---

### Q7 — Two defects are known, located, and were deliberately left unfixed. Do they get register lines now?

**Situation.** `AscentControl.cs:397-414` (the S2 roll-trim hysteresis) and `Steering.cs:102` (roll never
angle-held on coast) were root-caused on 2026-09-01 and the fix — *"a continuous roll-only deadband
everywhere"* — was withheld because it *"touches PROVEN ascent"* and needed owner review plus a V4
re-fly. That re-fly never happened; the autopilot was deleted the same day. The third, the ullage gate
that lost the booster, is already register **H1b**. If these files are recovered as-is, the defects come
back with them.

**Options.**
1. **Log all three as register lines when the files are recovered**, and gate the roll fix behind the
   V4 re-fly it was always waiting for (which needs a separate glass-time go).
2. **Fix the roll defect during recovery**, before anything flies.
3. **Recover the files as-is and say nothing further** — the defects are documented here.
4. **Do not recover `Steering.cs` at all** (§7.2 already marks it RECOVER-REFERENCE); write its
   replacement fresh against the pinned MechJeb, and recover `AscentControl.cs` with the roll-trim block
   removed rather than fixed.

**My recommendation: option 4 for `Steering.cs`, option 1 for the rest.** `Steering.cs`'s last committed
state hands attitude to stock SAS (`UseGimbalLoop = false`) — recovering it live would silently import
that decision, and §B12.1's whole point is that MechJeb owns attitude now. For `AscentControl.cs`,
deleting the roll-trim block is safer than fixing it, because the fix was never flown and the block is
the identified cause. Everything else becomes a tracked line rather than a surprise. **Owner call: it
touches the one subsystem that is flight-validated, which is exactly why the fix was held in the first
place.**

---

## Logged, not done (C1.1 — noticed during this task, out of its scope)

These were observed while auditing and are **not** acted on here. They are recorded so they are not lost;
turning any of them into work needs a `REGISTER.md` line, which R1's declared outputs do not include.

1. `docs/BLACKBOX_RESEARCH.md` §2.0 says 10 Hz is *"2× the deleted recorder's best (5 Hz)"*; that is
   Recorder A's rate. Recorder B was **4 Hz** (§3.3). One-word fix, no consequence to the conclusion.
2. The `Schema[]` array declares **135** names; the last flown CSV header has **136** columns. The extra
   column is emitted by the glue outside the array. Worth pinning down before a fresh schema is written.
3. `plugin/src/=` — a 0-byte file created by a shell redirection accident — was committed at `158eb2a`
   and lived in the tree for a week. Nothing to do now (it is gone), but it is evidence that stray files
   survive review here.
4. Gen-1 `plugin/src/FlightTrajectory.cs` drew the impact point and target over the **flight view**
   (owner, 2026-08-24: *"green x on the centre of the barge"*). Today's tree has no equivalent, and it
   is a screen feature, not autopilot code.
5. `docs/INDEX.md` has no entry for `plugin/reference_f9i/` (Q6).
6. `docs/MASTER_BUILD_SPEC.md` and `docs/ISSUE_REGISTER.md` are marked SUPERSEDED here, but neither's
   content has been verified as fully carried into `BUILD_PLAN.md` / `REGISTER.md`. **Mine before
   discarding** — register H1b traces to `ISSUE_REGISTER.md`.

---

*End of R1. No code restored. Next action is the owner's decision on Q1–Q7.*
