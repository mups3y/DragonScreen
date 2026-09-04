# EXTRACT — `RendezvousControl.cs` (deleted gen-2) → MechJeb §B9 Phase 3 tuning input

> **[FROM DELETED GEN-2 `RendezvousControl.cs` — FLOWN FAR-FIELD TO 109 km IN RSS-RO — REFERENCE ONLY,
> NEVER LIVE]**
>
> Produced by register task **W20** (Wave E-8), 2026-09-04, under the owner's G6 re-verdict of
> 2026-09-04 (*"MechJeb flies all upper-stage manoeuvres, only the booster is scripted"*): `W20` was
> re-verdicted **RECOVER-CODE → RECOVER-REFERENCE**. **No `.cs` file was created, restored or edited by
> this task.** Everything below was read out of `8b81816^` with `git show` and written down.
>
> ⛔ **This document is EVIDENCE, not an instruction.** It records what a deleted hand-written controller
> did and what happened when it flew. `docs/BUILD_PLAN.md` wins on any conflict (C7.1). The
> `StationApproach` facade this file used to back is **T19's** to flip (§B12.5a) — not this document's.
>
> ⛔ **The flown half is the far field only.** The terminal (near-field CW) leg was **NEVER FLOWN**; the
> dock was never flown. §5 draws that boundary explicitly. Do not read a number from §2/§3 as if it were
> terminal evidence.

**Read with:** `docs/BUILD_PLAN.md` §B1, §B9 Phase 3, §B10.1–§B10.2, §B11, §B12.4, §B12.5a, §B12.8 ·
`docs/MECHJEB_MISSION_TUNING.md` §3.1–§3.3 (the op chain this maps onto) ·
`docs/AUTOPILOT_RECOVERY_AUDIT.md` (R1) §5.1, §5.2, §7.4 · `docs/FLIGHT_VERIFICATION.md` (DS-ASC-008) ·
`docs/FLIGHT_CORPUS_ASSESSMENT.md` §5 · `docs/RENDEZVOUS_REBUILD_PLAN.md` (**evidence only** — §4 below).

---

## 0. What was read, and what it was

| File (at `8b81816^`) | Size / lines | R1 verdict | Regime | Flight status |
|---|---|---|---|---|
| `plugin/src/RendezvousControl.cs` | 40,628 B / 634 lines | RECOVER-CODE ⚠ defects flagged | RSS-RO | ◐ **far-field only, to 109 km** |
| `plugin/src/pure/Phasing.cs` | 7,369 B / 112 lines | RECOVER-CODE | RSS Earth (its test) | ◐ far-field flown |
| `plugin/src/pure/Rendezvous.cs` | 7,072 B / 125 lines | RECOVER-CODE ⚠ defect-flagged | — | ❌ **near-field never flown** |
| `plugin/src/pure/RvIntercept.cs` | 6,462 B / 106 lines | RECOVER-CODE | — | ❌ **never flown — default OFF** |
| `plugin/src/pure/NavFilter.cs` | 8,827 B / 146 lines | RECOVER-CODE, **R1 §7.4 regime-unstated** | — | ❌ near-field only ⇒ never flown |

`Cw`, `Lvlh`, `Lambert`, `Maneuver`, `Hohmann`, `Conic` were already in the tree (Wave A) and were not
re-read here beyond their call sites.

**Architecture in one line.** A far/near split at a single range threshold: **far field** = a phase-timed
Hohmann transfer flown as a *continuous* prograde Draco burn; **near field** = Clohessy-Wiltshire
two-impulse legs to offset aim points. A disabled-by-default Lambert mid-field intercept could pre-empt the
far field. A hard periapsis floor gated every burn in both regimes.

---

## 1. The tunables, transcribed — every one with its regime and its evidence

⚠ **Read the caveat with the number.** Where the "Source / evidence" cell says a number's regime or
provenance is unstated, that caveat is part of the number and travels with it (R1 §0.1). Nothing in this
table is a converged RSS-RO value unless the cell says a flight produced it.

### 1.1 `RendezvousControl` — the glue seam

| Name | Value | Source / evidence — **and its limits** |
|---|---|---|
| `ForwardSign` | **−1.0** | ⭐ **DERIVED + FLIGHT-CONFIRMED (131412).** Forward burn = `s.Z = −Dot(A, ct.up)`; nose prograde ⇒ `s.Z=−1` raised apoapsis **200 → 419 km** on flight 131412. A KSP-frame convention, not a physical constant — **it does not transfer to MechJeb**, which drives its own throttle/attitude. |
| `RvReturnReserveFrac` | **0.20** | ⭐ Deliberately **MODEST and UNSIZED** — a catastrophe-preventer against the DS-ASC-003 total drain (0% ⇒ no attitude control ⇒ dead crew), *not* a return guarantee. Its own comment says **"SIZE IT to the measured deorbit cost on re-fly"** — that re-fly never happened, so **0.20 is a guess, not a measurement**. |
| `AttitudeReadyDeg` | **5.0°** | The burn gate: translate only once pointed within this. ⚠ Flight **194334** shows the failure mode — a tumbling capsule never met `perr ≤ 5°`, so `trans_z = 0` for the entire flight. Provenance of the 5° itself is **not stated in the file**. |
| `CoastReacquireDeg` | **3.0°** | Far-field coast prograde re-acquire hysteresis. ⛔ **The loop it drove was REMOVED by owner directive 2026-09-01** (*"remove Claude-invented loops"*) — the field survived, the behaviour did not. Treat as dead. |
| `BurnDoneDvMps` | **0.02 m/s** | Near-field CW burn-complete threshold. **Never flown** (near field never entered). Provenance unstated. |
| `CwHandoffRangeM` | **100,000 m** | ⭐ **FLIGHT-DERIVED, and the single most instructive number here.** The original **50 km** split *never caught* the transfer's ~80 km closest approach ⇒ fly-past (131412: **86 km**; 165302: **79 km**). Raised to 100 km. ⚠ **The fix was never validated**: DS-ASC-008 stopped at **109 km**, 9 km short of it. |
| `CoEllipticBelowM` | **10,000 m** | Co-elliptic parking height below the station. Provenance: the research profile (~10 km below/behind), **not** a measured RSS-RO value. |
| `RaiseTolM` | **2,000 m** | "Apoapsis reached target" tolerance. Provenance **unstated**. |
| `SafePeFloorM` | **150,000 m** | ⛔ The crew-safety periapsis floor. **Its existence is flight-earned** (214827 self-deorbit, §3.1); **the 150 km value itself has no stated derivation** — it is a round number above the RSS-RO atmosphere, not a computed decay bound. |
| `CoastWarp` | **true** | Warp through the co-elliptic coast. |
| `CoastWarpFallbackHorizonS` | **5,400 s** | Bounded look-ahead when the target period is unusable. A guard value; provenance unstated. |
| `CoastWarpMinRangeM` | **120,000 m** | Never warp inside this of the station. Chosen as a **buffer above the hand-off** — the comment says "above the 50 km CW hand-off", which is **stale** against the 100 km `CwHandoffRangeM` it shipped with. An internal inconsistency, recorded as found. |
| `SettleRateDps` / `SettleMaxS` | **2.0 dps / 90 s** | ⛔ Detumble-at-entry gate, added after flight **194334**. The *mechanism* is flight-root-caused (§3.5); the **2 dps / 90 s values are not measured** — the comment states no derivation. |
| `UseNavFilter` | **true** | Fly the near-field CW on a filtered rel-nav estimate rather than truth. **Never exercised in flight** — the near field was never entered. |
| `UseLambertIntercept` | **false** | ⭐ **DEFAULT OFF, explicitly flight-gated**: *"CW/Hohmann stay the default until a flight tunes this on."* That flight never happened. **Zero flight evidence exists for the Lambert path.** |
| `LambertMinRangeM` / `LambertMaxRangeM` | **30,000 / 300,000 m** | Band in which a single-rev Lambert was considered reliable. **Asserted, never flown.** |
| `LambertMinTofS` | **45 s** | Abandon a plan arriving inside this. Unflown. |
| `LambertBurnDoneDvMps` | **0.05 m/s** | Residual departure Δv that ends the intercept burn. Unflown. |

### 1.2 `pure/Phasing.cs`

| Name | Value | Source / evidence — **and its limits** |
|---|---|---|
| `PhaseAlignTolS` | **15.0 s** | Within this of the aligned phase, stop waiting and start the transfer. Provenance **unstated**. |

`FarGuide` itself carries no other constants — every altitude comes in through `FarInputs` from the glue.

### 1.3 `pure/Rendezvous.cs` — ❌ **the near-field FSM, NEVER FLOWN**

| Name | Value | Source / evidence — **and its limits** |
|---|---|---|
| `RendezvousCompleteM` | **30,000 m** | Phasing → CoElliptic transition. From the research profile ("30 km rendezvous-complete"); **never reached in flight**. |
| `TerminalTofFrac` | **0.25** | CW leg time-of-flight as a fraction of the target period (floored at 60 s). Provenance **unstated**; never flown. |
| `CwMaxRangeM` | **200,000 m** | ⛔ **Defence-in-depth guard, flight-earned** — beyond it `Guide` refuses to run the CW two-impulse solve at all. Directly the 214827 lesson (§3.1). The **200 km value** is stated as "well above the near-field regime", i.e. **chosen, not derived**. |
| `CoEllipticBelowM` / `CoEllipticBehindM` | **10,000 / 20,000 m** | The Phasing/CoElliptic offset aim point (10 km below, 20 km behind). Research profile; never flown. |
| `AiRangeM` | **7,500 m** | Approach-Initiation standoff; also the hand-back point to `CrewProcedureOps.PhaseComplete()`. Research profile (the burn DB gives AI as 90 s / 0.72 m/s). ⚠ `RENDEZVOUS_REBUILD_PLAN.md` defect (3) says the *"100 km CW invalid / 7.5 km correct"* hand-off is **asserted, not derived from a CW-vs-truth bound**. |
| `CorridorRangeM` | **2,000 m** | Midcourse → Arrived, hand to docking. Research profile; never flown. |
| Aim offsets | Phasing/CoElliptic `(−10 km, −20 km)`; ApproachInit `(−0.3·7500, −7500)`; Midcourse `(−0.2·2000, −2000)` | Offset targeting so a missed burn drifts clear of the Keep-Out Sphere. The **fractions 0.3 and 0.2 have no stated derivation.** |

### 1.4 `pure/RvIntercept.cs` — ❌ **never flown (its caller defaulted OFF)**

| Name | Value | Source / evidence — **and its limits** |
|---|---|---|
| `MaxDvMps` | **300.0** | Cost cap rejecting bad (near-180°) geometry. Provenance unstated. |
| `TofSamples` | **24** | Candidates scanned across the band. Unstated. |
| `TofMinFrac` / `TofMaxFrac` | **0.15 / 1.10** | Time-of-flight search band as a fraction of the target period. Unstated. |

### 1.5 ⛔ `pure/NavFilter.cs` — **R1 §7.4 REGIME-UNSTATED DEFECT**

The file's own comment attributes these to *"typical spec (`docs/CREW_DRAGON_GNC_RESEARCH` §1)"* — a
**pointer to a document section, not a named source, and no regime**. §0.1 requires a regime before a
number is used. **Every value below is therefore recorded here as REGIME-UNSTATED, and that marking is part
of the value.** None of them was ever exercised in flight: `NavFilter` was wired only into the near-field
CW path, which was never entered.

| Name | Value | **Regime: UNSTATED. Provenance: "typical spec", source not named in the file.** |
|---|---|---|
| `ImuAccelNoiseMps2` | **0.01 m/s²** | Accelerometer white noise 1σ — regime UNSTATED, source not named. |
| `BiasWalkMps2` | **1.0e-4 m/s²** | Accel-bias random walk per tick — regime UNSTATED, source not named. |
| `RgpsNoiseM` | **5.0 m** | Relative-GPS position 1σ — regime UNSTATED, source not named. |
| `LidarNoiseM` | **0.02 m** | Terminal LIDAR/optical rel-nav 1σ — regime UNSTATED, source not named. |
| `LidarHandoffFarM` | **1,000 m** | Pure rel-GPS beyond this range — regime UNSTATED, source not named. |
| `LidarHandoffNearM` | **200 m** | Full LIDAR within this range — regime UNSTATED, source not named. |
| `InitPosVar` / `InitVelVar` / `InitBiasVar` | **100 / 25 / 1** | Initial covariance seeds — regime UNSTATED, source not named. |

⛔ **Do not lift any of the seven rows above into a MechJeb tune, a display, or a simulation without first
establishing its regime.** The design *shape* is worth keeping — three decoupled per-axis 3-state
`[position, velocity, accel-bias]` Kalman filters, propagated on the IMU accelerometer and corrected on
rel-GPS position, with a **range-scheduled measurement 1σ** blending rel-GPS → LIDAR linearly across
[near, far]. That shape carries no regime risk. The numbers do.

---

## 2. ⭐ The flown far-field experience, mapped op-by-op onto §B9 Phase 3's planner chain

This is what W20 exists for. Each row states **what the deleted controller actually did in RSS-RO**, and
**what that means for the named MechJeb operation** §B9 P3 / `MECHJEB_MISSION_TUNING.md` §3.1 puts in its
place. The op chain is `OperationPlane` → `OperationGeneric` (file `OperationTransfer.cs`) →
`OperationCourseCorrection` → `OperationKillRelVel`, each driven by `ExecuteOneNode` and re-planned.

### 2.1 `OperationPlane` — **the absence is the finding**

The deleted controller has **no plane-change logic at all**. `FlyFarField` computes a signed phase angle
about the chaser's own orbit normal and never measures, plans or burns a relative inclination or RAAN
error; `AscentControl.TargetPlaneNormal` set the plane at launch and nothing maintained it afterwards.

**Tuning input:** the one flown rendezvous therefore contains **zero evidence about plane error in RSS-RO**
— neither that it was small, nor that it was manageable. §3.1's *"Do this FIRST and EARLY"* stands
unchallenged by this file, and the first MechJeb rendezvous flight is the **first** measurement of plane
error this project will ever have. Log the relative inclination at insertion and after the plane burn; there
is no prior to compare it to.

### 2.2 `OperationGeneric` (Phase / Boost / Transfer) — **the half that flew**

**What flew.** The far-field FSM (`Phasing.FarGuide`) is a three-state phase-timed Hohmann transfer:

1. **PHASE** — stay on the low, fast insertion orbit; coast (warp-compressed) until the phase angle reaches
   `Hohmann.PhaseLeadRad(r1, r2, μ)`; begin the transfer when the wait falls within **`PhaseAlignTolS` = 15 s**.
2. **TRANSFER** — burn **prograde only**, near periapsis, raising **apoapsis** to
   `TargetAltM = (r_station − R_body) − CoEllipticBelowM`, i.e. **10 km below the station's altitude**, and
   **stop** when apoapsis is within **`RaiseTolM` = 2 km** of it.
3. **COAST** — coast up to apoapsis, where the phase timing puts the chaser near the station and the range
   drops into the near-field regime.

**Flown numbers — DS-ASC-008, `Crew-2_20260901_004929.csv`, 2026-09-01, RSS-RO, the deadband build:**

| Quantity | Value | Note |
|---|---|---|
| Insertion orbit | **367 × 336 km / 51.6°** | the phasing orbit the transfer started from |
| Range at start of phasing | **7,328 km** | |
| Range at end of flight | **109 km** | ⛔ **the flight stopped here** — 9 km short of the 100 km hand-off |
| Duration of the close | **15.6 orbits**, ~70,000 s of coast | |
| Propellant across the coast | `mmh_frac` **0.826 → 0.822** | **near-zero** — the far-field coast is essentially free |
| Propellant at end of flight | `mmh_frac` **0.584** (58% remaining) | vs DS-ASC-007's **0.000** (ran dry) |
| `rv_burn_dv`, `dv_planned`, `dv_delivered`, `trans_*` | **all 0 in the tail** | no burn in the recorded tail — it was pure coast |
| Roll behaviour | **46% of the rendezvous was full attitude drift**; roll commanded only **21%**; drifted **~54°** | the coast released all attitude axes |
| Transfer apoapsis closest approach (earlier builds) | **86 km** (131412), **79 km** (165302) | why the 50 km hand-off never caught it |
| Near-field entry, earlier build | **91 km** (DS-ASC-007) | reached the near field — and **ran dry there** |

**Tuning input for `OperationGeneric`:**

- **A phasing orbit ~30–40 km below the target closed 7,328 → 109 km in 15.6 orbits (~19.4 h) for
  essentially zero propellant.** That is the one real RSS-RO phasing datum this project owns, and it is a
  *lower bound on how cheap the far field can be*, not a target: the closing came from the altitude
  difference, not from burns. It is consistent with the real ~19 h launch-to-dock (§B11) — a MechJeb
  transfer that wants to close much faster is buying time with Δv the tank may not have (§2.5).
- **The bounded-raise discipline is the transferable lesson, and MechJeb already has it.** The deleted
  code's own "stop at target ± 2 km" exists *only* because an earlier continuous raise over-shot (§3.2).
  `OperationGeneric` plans a node with a finite Δv and the Node Executor stops on it — the over-raise class
  of failure does not exist in a node-based executor. **Do not port `RaiseTolM`**; it is a workaround for a
  continuous-thrust controller.
- **Leave `Coplanar` off and let `OperationGeneric` optimise** (§3.1's intent). The deleted controller's
  naive coplanar phase-timed Hohmann is exactly the thing §3.1 says not to reproduce, and its own flight
  record (a fly-past at 79–86 km, twice) is the argument against it.
- **Target the intercept, not a parking altitude.** The deleted design aimed at *"10 km below the station"*
  and then relied on the coast to bring the range down. `OperationGeneric` with `Capture`/`PlanCapture`
  targets the station itself. The 10 km figure survives only as the **§B11 approach-corridor geometry**,
  not as a transfer target.

### 2.3 `OperationCourseCorrection` — **the `InterceptDistance` ladder has no flight evidence**

**What flew.** Nothing. There is no course-correction stage in the deleted design at all: a single
`CwHandoffRangeM` threshold switched the whole controller from far-field Hohmann to near-field CW, and the
near field was never entered. The nearest analogue is the **Lambert mid-field intercept**
(`TryLambertIntercept` + `pure/RvIntercept.cs`), which scans 24 time-of-flight candidates across
[0.15, 1.10] × target period on **both** transfer-angle branches, latches the cheapest pe-safe plan under a
**300 m/s** cap, and then **re-solves to the latched arrival UT every tick** so the residual departure Δv
shrinks as the burn is delivered. **It shipped `UseLambertIntercept = false` and was never flown.**

**Tuning input:**

- **The `InterceptDistance` ladder (4000 → 1000 → 220 → 20 m, §3.1) is entirely un-flown by this project.**
  Nothing here confirms or contradicts it. Say so when the first flight is planned.
- The Lambert executor's **re-solve-to-a-latched-arrival** pattern is the deleted code's closest structural
  match to §3.1's *"run `OperationCourseCorrection` 1–2×, walking closest approach down"*, and to §3.3's
  *"re-plan after every burn"*. It was arrived at independently, which is weak corroboration that the
  re-plan cadence is the right shape — **but corroboration from unflown code, which is worth very little.**
- The **300 m/s cost cap** and the **near-180°-transfer blow-up** it guards are real properties of Lambert
  geometry, not of this codebase. If a `OperationGeneric` solve ever returns an absurd Δv in RSS-RO, that is
  the mechanism to suspect first.

### 2.4 `OperationKillRelVel` — **never flown; the hand-off point is the only usable datum**

**What flew.** Nothing. `Rendezvous.Guide` ends at `RvPhase.Arrived` when range ≤ `CorridorRangeM` = 2,000 m
and holds V-bar attitude; separately, `FlyNearFieldCw` calls `CrewProcedureOps.PhaseComplete()` when range
≤ `AiRangeM` = **7,500 m** — the G9 GO-for-AI gate. Neither path ever executed.

**Tuning input:** `OperationKillRelVel` with `TimeSelector = CLOSEST_APPROACH` (§3.1) nulls relative
velocity at the station-keeping point; the deleted design's **7.5 km AI standoff** is where its crew gate
sat, and §B11's geometry puts the Go/No-Go hold at **~1 km**. These are different points and the deleted
code is **not evidence for either** — its 7.5 km comes from the research profile, and
`RENDEZVOUS_REBUILD_PLAN.md` defect (3) explicitly calls the hand-off range **asserted, not derived**.

### 2.5 Node Executor + the re-plan cadence (§3.2 / §3.3) — **the propellant ledger is the real finding**

The deleted controller was **continuous-thrust**, not node-based, so it has no `LeadTime`, no
`AlignedToleranceDegrees` and no `ExecuteOneNode` analogue to tune from. What it *does* carry is the
**propellant arithmetic that makes §3.3's ledger gotcha load-bearing**:

- Draco **Isp 240**; RCS translation **~21% efficient** (`RCS_BALANCE_FINDING.md`); **a full tank ≈ 66 m/s
  useful**. ⚠ Regime: measured against the deleted controller's own RCS-translation path in this install —
  **it is not a MechJeb number**, and a Node-Executor burn that points and holds may realise a different
  efficiency.
- The real Dragon rendezvous is **~100–200 m/s** spread over ~28 h of discrete burns.
- ⇒ **The profile does not fit the tank at 21% efficiency.** This is `RENDEZVOUS_REBUILD_PLAN.md`'s defect
  (4), recorded there as **UNRESOLVED and "EVIDENCE REQUIRED"**.
- The deleted code's answer was `RvReturnReserveFrac = 0.20`: a hard inhibit on every rendezvous
  translation once return propellant fell to 20%, which **holds short of the dock rather than spending the
  reserve**. Its own comment concedes *"a full dock AND a full return do not both fit the tank"*
  (`docs/FLIGHT_VERIFICATION.md`).

**Tuning input:** §3.3's *"the phasing/rendezvous chain must not eat the return propellant"* is not a
precaution here — it is the **binding constraint**, and the deleted project never resolved it. The
Node-Executor tune must be measured against the deorbit ledger (`VesselData.cs:886-928`) from the first
flight, and the **first thing the first MechJeb rendezvous flight should measure is the realised Δv
efficiency of a Node-Executor Draco burn**, because 66 m/s vs 100–200 m/s is the difference between the
mission being possible and not.

---

## 3. The failure record — five flights, five mechanisms

These are the reasons the deleted design looks the way it does. They are **failure lessons**: cheaper to
read than to rediscover.

### 3.1 Flight **214827** — CW at long range self-deorbited the capsule
The near-field CW two-impulse solve was run at a **13,000 km** separation (a bogus range: the ISS was
**unloaded**, so its *transform* position was a stale placeholder). CW is a linearisation about the target
and its two-impulse inverse **demanded ~28 km/s**; the glue faithfully fired the Dracos **retrograde** until
the capsule **self-deorbited — periapsis +178 → −143 km**.
**Three separate defences were added, and all three are worth keeping as principles:** (a) read the target
position from `getPositionAtUT` (the orbit), never the transform, unless the target is `loaded`; (b) a
`CwMaxRangeM = 200 km` refusal inside the pure solver; (c) the hard `SafePeFloorM = 150 km` periapsis floor
that gates *every* burn regardless of what guidance asked for.
**For MechJeb:** (a) and (b) are internal to a solver MechJeb replaces. **(c) is not** — a
guidance-independent periapsis floor is a crew-safety property no planner gives you for free, and this is
the flight that earned it.

### 3.2 Flight **103303** — the continuous co-elliptic raise over-raised apoapsis 200 → 772 km
The original far-field law burned prograde **continuously** to "walk both apses up". A continuous prograde
burn near periapsis only pumps **apoapsis**: it went **200 → 772 km** against a ~409 km target while
periapsis crawled, never coasted (so warp never armed) and never closed the phase gap.
**For MechJeb:** this failure mode is structural to continuous-thrust guidance and **cannot occur** with
`OperationGeneric` + `ExecuteOneNode`, which burn a finite planned Δv and stop.

### 3.3 Flight **165302** — the low-thrust apoapsis circularize took ~27 orbits and drifted 246 → 6,000 km
An "establish a circular co-elliptic orbit" step was attempted with the low-thrust Dracos. It needed **~27
near-apoapsis orbits**, during which the chaser drifted **246 → 6,000 km** away. It never completed and
never handed off; the step was deleted.
**For MechJeb:** the constraint is the **thrust-to-time budget of the Dracos in RSS-RO**, and it does not go
away when a better planner does the planning. Any §B9 P3 chain that asks for a circularization at these
altitudes on RCS alone will hit the same wall. Prefer transfers that arrive at an intercept over transfers
that establish a parking orbit.

### 3.4 The 50 km hand-off never caught the ~80 km closest approach (131412: **86 km**; 165302: **79 km**)
The phase-timed transfer brought the chaser to ~80 km at apoapsis, but the far→near threshold was 50 km, so
the controller stayed in far-field mode through closest approach and **flew past**. Fixed by raising
`CwHandoffRangeM` **50 → 100 km**. ⚠ **The fix was never validated** — DS-ASC-008 then stopped at **109 km**.
**For MechJeb:** a *single-threshold* far/near mode switch is fragile against a closest approach that the
planner itself determines. §3.1's ladder replaces it with a sequence of targeted closest approaches, which
is the structurally better answer — this is the flight evidence for preferring it.

### 3.5 Flight **194334** — an ascent-induced tumble made the rendezvous impossible
The capsule arrived from ascent with an **uncontrolled roll rate**: the single-engine S2 has **zero roll
authority** (`ctrl_tq_roll = 0` for 79% of S2) and RCS attitude was disabled while the engine was lit, so
roll wound up monotonically to **~54 dps by SECO**. A spinning capsule can never hold prograde — the nose
traced a cone, pitch/yaw thrashed chasing it (`att_err` **50–177°**, **±90 dps**), the burn gate
`perr ≤ 5°` was never met so the far field **never translated** (`trans_z = 0` the whole flight), and the
thrash burned **23% of the Draco MMH/NTO**.
The fix was a one-shot detumble at rendezvous entry: hold the *current* attitude
(`Steering.Point(v, v.ReferenceTransform.up)` ⇒ zero pitch/yaw error by construction) so the shared Dracos
are free for the roll-rate loop, then proceed.
**For MechJeb:** the *fix* belongs to a steering layer MechJeb owns. **The precondition does not:** the
vehicle can be handed to Phase 3 in a state where no attitude-gated burn will ever fire. DS-ASC-008
confirms the mechanism persists — S2 peak roll **27.5 dps**, and the capsule drifted **~54° in roll** during
the rendezvous with roll actively commanded only **21%** of the time. **The conductor must not enter Phase 3
without a rate gate.**

### 3.6 The attitude-duty drain (RC1) — 94% duty, 85–94% of terminal fuel
`RENDEZVOUS_REBUILD_PLAN.md` §1 measured the pre-deadband builds: during burn/phasing segments the attitude
loop held prograde **continuously** — **94% applied attitude duty**, with **85–94% of terminal fuel going to
attitude** and only ~8% to translation. Authority fixes (units, then trusting the game's own estimate) moved
applied duty **59% → 51%** and range **90 → 58 km** — real, but marginal.
DS-ASC-008's deadband build fixed this **in the far field only**, and left a **residual terminal limit
cycle**: even holding a far-field aim in realtime, pitch chattered **±2.6 dps** and cost **~7% of the tank
over ~200 s** (`mmh` 0.654 → 0.584). The file's own note: the rate band may be **too tight for a *moving*
aim point**.
**For MechJeb:** attitude-hold duty against a *moving* aim is a real RSS-RO cost on this vehicle, and it is
the one place where §3.2's `AlignedToleranceDegrees` tune has a flown prior — **a tighter band is not
free here.** ⚠ Regime caveat: every duty percentage above was measured against the **deleted custom steering
loop**, not against MechJeb's attitude controller, so the numbers bound the *problem*, not MechJeb's
*solution*.

---

## 4. The enumerated defects — `RENDEZVOUS_REBUILD_PLAN.md`, read as evidence only

⛔ **`docs/RENDEZVOUS_REBUILD_PLAN.md` carries its own banner: *"UNDER VERIFICATION — NOT AN INSTRUCTION,
NOT APPROVED"*, and R1 §5.4 verdicts it RECOVER-REFERENCE with *"⛔ never treat as an instruction"*. It is
cited here as the record of a 2026-08-31 review that found real defects. Nothing in it is adopted, and its
proposed rebuild (§4–§9 of that file) is NOT a plan this project holds.** The same review flagged
`pure/Rendezvous.cs` (R1 §5.1) in the same words.

**The six defects that review found — each stated as "this is what went wrong":**

1. **A lower circular orbit was wrongly called a "stable co-elliptic" orbit.** It is a **phasing** orbit
   that closes along-track — ~17 m/s at 10 km below. Calling it stable led to a design that expected free
   station-keeping where there is drift.
2. **The removed circularization was not reproduced before proposing to restore it.** The proposal to bring
   back the step that failed in flight 165302 was made without re-demonstrating the failure.
3. **The "100 km CW invalid / 7.5 km correct" hand-off was asserted, not derived** from a CW-versus-truth
   error bound. Both numbers in `CwHandoffRangeM` and `AiRangeM` inherit this.
4. **The Δv budget contradiction was UNRESOLVED — "EVIDENCE REQUIRED".** ~66 m/s of useful tank against a
   100–200 m/s profile, hinging on a **discrete-burn efficiency that is UNKNOWN**. ⭐ **This is the defect
   that still binds** (§2.5).
5. **Fuel attribution has a sampling caveat** — the attitude-vs-translation split came from sampled
   columns, so the 94% / 85–94% / ~8% figures in §3.6 carry that caveat.
6. **The named-burn / 7.5 km / waypoint values need source-confidence labels** they did not have. Every
   research-profile number in §1.3 above inherits this.

That review also recorded five **open risks**, none of which was ever closed: whether a discrete apsis-burn
co-elliptic fits the time and the budget on Dracos; whether the full profile fits 66 m/s (*"the single
biggest risk"*); how loose attitude may go during co-elliptic drift; whether WP0 400 m / WP1 200 m / WP2
20 m hold for RSS-RO; and whether to keep Hohmann coarse + CW terminal. **They are open questions in the
historical record, not tasks.**

---

## 5. ⛔ The evidence boundary — exactly where flown stops and written begins

| Segment | Status | Evidence |
|---|---|---|
| Insertion → phasing orbit | ✅ **FLOWN** | DS-ASC-008: 367 × 336 km / 51.6° |
| Far-field PHASE (coast to the Hohmann lead, warp-compressed) | ✅ **FLOWN** | 7,328 → 109 km over 15.6 orbits, `mmh_frac` 0.826 → 0.822 |
| Far-field TRANSFER (bounded prograde ap-raise) | ✅ **FLOWN** — in earlier builds | 131412 (ap 200 → 419 km); reached 86 km / 79 km apoapsis approach |
| Far-field COAST to the hand-off | ◐ **PARTIAL** | reached **109 km** and **stopped there** — 9 km short of the 100 km threshold |
| ⛔ **Crossing the far→near hand-off** | ❌ **NEVER FLOWN on the current build** | DS-ASC-007 (pre-deadband) reached **91 km** and **ran dry** there |
| ⛔ **Near-field CW terminal legs** (`Rendezvous.Guide`, `pure/Cw.cs`) | ❌ **NEVER FLOWN** | `pure/Cw.cs` is explicitly marked *"❌ terminal never flown"*; DS-ASC-008's tail has `rv_burn_dv`/`dv_planned`/`dv_delivered`/`trans_*` all **0** |
| ⛔ **Approach Initiation / Midcourse / Arrived** | ❌ **NEVER FLOWN** | `rv_phase` only ever `Phasing` / `ApproachInit`, and **the CSV `ApproachInit` label means the far-field COAST**, not the real AI — a label collision, recorded in `FLIGHT_VERIFICATION.md` |
| ⛔ **`NavFilter` rel-nav in the loop** | ❌ **NEVER FLOWN** | wired only into the near-field path |
| ⛔ **Lambert mid-field intercept** | ❌ **NEVER FLOWN** | shipped `UseLambertIntercept = false`, explicitly *"flight-gated"* |
| ⛔ **Plane change** | ❌ **NEVER EXISTED** | no plane logic in the file (§2.1) |
| ⛔ **Docking** | ❌ **NEVER FLOWN** | `FLIGHT_CORPUS_ASSESSMENT.md` §5: *"Nothing about docking. No `dock_phase` value is ever set."* |

⚠ **One flight, far-field only.** `FLIGHT_VERIFICATION.md`'s own instruction on DS-ASC-008 is
*"This flight is ONE data point, far-field only — do not overfit"* (rule V5). Every number in §2.2 carries
that.

---

## 6. What this line records for other tasks

- **`StationApproach` is T19's to flip** (§B12.5a). W20 lands no code and does not touch the facade.
- **This is a reference extraction (TIER 4).** `git status` shows **no `.cs` file touched** by this task.
- **W21** (`DockingControl.cs`) shares `pure/NavFilter.cs` with this line — §1.5's regime marking is the
  order it should read those constants in, and it must not re-derive them.
- The **Δv-budget contradiction (§2.5 / §4 defect 4)** is the largest un-closed question this file leaves
  behind. It is not W20's to answer.

---

## Open questions for the owner

### Q1 — This extract went to `docs/EXTRACT_RENDEZVOUS_CONTROL.md`, not to `MECHJEB_MISSION_TUNING.md` §3

**Situation.** W20's register line says *"Where it goes: `docs/MECHJEB_MISSION_TUNING.md` §3.1–§3.3"*. The
owner-authorised batch instruction of **2026-09-04** (the same instruction that permitted five Wave E lines
in one session, deviating from C1.1/C1.7) instead directed: *"EACH TASK PRODUCES ONE DOCUMENT —
`docs/EXTRACT_<name>.md` … Do NOT write a single combined document: if this batch stops at 80% context, a
combined doc is left half-written and useless."* This session followed the batch instruction, because it is
the more recent owner direction and its stated reason (a combined document left half-written) applies
directly to `MECHJEB_MISSION_TUNING.md` §3, which is exactly such a shared document. **`MECHJEB_MISSION_TUNING.md`
was NOT edited** — C1.11 limits a task to its declared outputs, and the batch declared these three.

**Options.**
1. **Leave it as it stands** — the extract lives in `EXTRACT_RENDEZVOUS_CONTROL.md`; `MECHJEB_MISSION_TUNING.md`
   §3 keeps pointing at the plan, and a later reader finds this file through `INDEX.md`.
2. **Open a small [S] register line** to fold §2 of this extract into `MECHJEB_MISSION_TUNING.md` §3.1–§3.3
   as tuning inputs (with the flown/never-flown markings intact), leaving the failure record and the defect
   enumeration here. *(This chat's recommendation — §3 is where a tuning session will actually look, and the
   split keeps the tuning doc short.)*
3. **Move the whole extract into `MECHJEB_MISSION_TUNING.md` §3** and delete nothing (C1.16), leaving
   `EXTRACT_RENDEZVOUS_CONTROL.md` as a stub pointer.

**Recommendation: option 2.** It honours both directions — one self-contained document per task now, and one
merge into the tuning doc later, as its own line, when the batch is finished and the risk of a half-written
combined document is gone.

### Q2 — The Δv-budget contradiction is still open, and it decides whether Phase 3 is flyable

**Situation.** §2.5 / §4 defect (4): a full Draco tank is **≈66 m/s useful** at the measured ~21% RCS
translation efficiency, against a real rendezvous profile of **~100–200 m/s**. The 2026-08-31 review left
this **UNRESOLVED — "EVIDENCE REQUIRED"**, because it hinges on a discrete-burn efficiency that was never
measured. The deleted code's response was to inhibit rendezvous burns at a 20% return-propellant reserve —
i.e. to **hold short of the dock** rather than strand the crew. Moving to MechJeb changes *how* the burns are
flown (Node Executor, point-and-burn) but not the tank.

**Options.**
1. **Make it the first measurement of the first MechJeb rendezvous flight** — instrument the realised Δv per
   Node-Executor burn against propellant consumed, and decide the profile afterwards.
2. **Resolve it analytically first**, from the Draco Isp/thrust and the RO propellant configs, before any
   flight is planned.
3. **Treat it as settled against the profile** and plan Phase 3 around a reduced-Δv approach from the outset.

**Recommendation: option 1.** §B12's whole approach is RO-defaults-first-then-tune, the number is an
*empirical* efficiency that an analytic pass cannot honestly produce, and holding short is already the safe
failure mode. This is a §B5/T22 empirical item, not a build-chat call.
