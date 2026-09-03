# MechJeb mission tuning — the operational, per-setting recipe for a nominal Crew-Dragon flight

**What this is.** An **operational** tuning recipe: for every phase of a nominal Crew-Dragon mission, the
MechJeb module(s) used, the exact user-editable settings and the values to set, **how** to set each (the
`mechjeb_settings_type_*.cfg` key ⟂ the C#/API field ⟂ the GUI control), and the phase's gotchas. It is the
"expert MechJeb operator's" flight book that the Part-B conductor (§B4) will execute in code.

**What this is NOT.** It is **not new derivation**. §B5–§B11 of `docs/BUILD_PLAN.md` already did the research;
this doc **CONSOLIDATES** it into a per-phase, per-setting recipe and adds only what §B5–§B11 does not cover:
(a) the explicit ascent-curve / max-Q / throttle enumeration §B8 carried in prose, (b) the **Falcon-9
booster-recovery** method (⚠ NEW — see the SCOPE FLAG), and (c) a **name-drift check** of §B10's kRPC-derived
identifiers against MechJeb's current C# source. Where this doc and §B5–§B11 disagree on a *number*, **the
plan wins** (C7.1) and the disagreement is flagged here, not silently resolved.

**Status / authority.**
- Research doc. **No code. No plan edit** — `docs/BUILD_PLAN.md` is FROZEN; this is a NEW file, not a §B edit.
- **It does NOT open the Part-B build gate (T15).** Like §B5–§B11 it is *preparatory research*: nothing here
  is authority to write conductor code, embed MechJeb, or touch the game. Part B starts when the **owner**
  opens that gate (C1.12).
- Every value below is a **target to converge**, not a measured result. The §B5 methodology stands: start
  from the RO default, change **one parameter at a time**, validate against real flight data (§8 / §B11),
  then lock it into the per-vessel-type cfg. **Nothing here has been flown.**

---

## ⚠ SCOPE FLAG — booster recovery is an EXTENSION of Part B, not part of it

> ✅ **FOLDED IN 2026-09-03 (owner directive, via the overseer — G4).** The owner has added booster recovery
> to the plan as **`docs/BUILD_PLAN.md` §B16**, and opened the Part-B build gate. This flag is kept as the
> record of why the extension exists; **§B16 is now the scope + architecture statement and this PHASE 2 is its
> per-setting recipe** (the plan wins on any conflict, C7.1). §B16.3 carries the engine-control directive
> below verbatim in substance; §2.4's craft-dump prerequisite and §2.5's guidance decision remain OPEN owner
> items. Building it still requires a register task — none exists yet (§B16.6).

**The current Part-B plan (§B1–§B15) covers the DRAGON CAPSULE's flight only.** §B9's phase list runs
Prelaunch → Ascent → Phasing → Rendezvous → Docking → Docked → Undock → Deorbit → Entry → Chutes →
Splashdown. **Falcon-9 booster recovery appears nowhere in it**, and §B12's conductor design assumes a single
controlled vessel.

Booster recovery is **structurally different from every other phase in this doc**:

- **It is a SEPARATE VESSEL.** At stage separation KSP splits the stack into two `Vessel` objects. The Dragon
  conductor follows the capsule; the booster becomes a second, independently-flown vessel. Nothing in §B12's
  conductor — one `MechJebCore`, one phase FSM, one screen front-end — addresses a second vessel, and KSP
  gives only *one* vessel focus at a time.
- **It is a different flight regime.** Everything else in this doc is orbital or entry guidance. This is a
  powered, atmospheric, target-accurate landing with **limited ignitions and limited throttle**.
- **It is not on the Dragon screens.** `docs/TELEMETRY_REGISTRY.md:9` records `BOOSTER_STATUS` as **dead** —
  the booster-recovery code went with the hand-written autopilot on 2026-09-01.

**Recorded as: an owner-directed ADDITION to fold into the plan when Part B starts** — a new §B section and
its own register task(s), decided by the owner, alongside T15–T22. It is **not** a silent widening of Part B,
and **phase 2 below is the research for that addition, not a licence to build it.** The conductor-level
questions it raises (which vessel the conductor follows, whether a second MechJeb instance is needed, whether
the booster is flown at all) are **owner decisions**, collected in §10.

---

## 0. Conventions — how to read every "how to set" line

### 0.1 The three places a setting lives

| Where | What it is | When it applies |
|---|---|---|
| **cfg** — `docs/reference/mechjeb_settings_type_Crew-Dragon.cfg` | the **permanent per-vessel-type store**; written on scene change, read on load | *persisted* settings — the locked tune |
| **C# field** on the module, reached by `MechJebCore.GetComputerModule<T>()` | what the conductor actually sets at runtime | everything; the only way to set *live-driven* settings |
| **GUI** control in the MechJeb window | what a human operator clicks | manual tuning + verification passes |

**cfg syntax** (§B7, re-confirmed against the live file):

- a tickbox is a bare bool — `CorrectiveSteering = True`
- an editable field is a **pair** — `LimitQa { ValConfig = 2000  TextConfig = 2000 }`
- an `EditableDoubleMult` keeps **internal SI in `ValConfig`** and a **scaled display in `TextConfig`**:
  `TurnStartAltitude { 500 / 0.5 }` (m ⟂ km) · `DynamicPressureTrigger { 10000 / 10 }` (Pa ⟂ kPa) ·
  `TurnShapeExponent { 0.4 / 40 }` (fraction ⟂ %) ·
  `SpinupAngularVelocity { 1.0471975511965976 / 10 }` (π/3 rad·s⁻¹ = 60 °/s ⟂ 10 RPM).
  **`ValConfig` is the authority.** Setting the module's Editable field handles the scale for you; writing the
  cfg by hand means writing **both** values consistently.

**persisted vs live-driven** (§B10): a module whose cfg block is **empty** — `MechJebModuleNodeExecutor { }`,
`MechJebModuleSmartASS { }`, `MechJebModuleSmartRcs { }`, `MechJebModuleTargetController { }`,
`MechJebModuleLandingGuidance { }`, `MechJebModuleManeuverPlanner { }`, `MechJebModulePVGGlueBall { }` — is
running **stock defaults** and is meant to be driven **per invocation from code**. Maneuver-Planner
`Operation*` objects are **never** persisted: instantiate, set fields, call `MakeNodesImpl(...)`, discard.

### 0.2 Tags

`[DOC]` publicly documented real figure · `[EST]` engineering estimate to validate in-sim · `[cfg]` the value
in our tuned Crew-Dragon cfg today · `[RO]` the RO/RP-1 recommended default · ⚠ open item (empirical or owner).

### 0.3 ⚠ NAME DRIFT — §B10's identifiers vs MechJeb's current C# source

§B10.1/§B10.2 took several names from the **kRPC MechJeb API**, which wraps an **older** MechJeb. Checked
against MechJeb2 `dev` source (checked 2026-09-03), the current C# differs. **This is not a change to the
plan's intent — the operations and the knobs are the same — it is a warning that every identifier must be
re-verified against the PINNED version we embed (§B12.1) before any code is written.**

| §B10 says (kRPC) | Current C# source | Note |
|---|---|---|
| `OperationTransfer` with `intercept_only`, `simple_transfer`, `period_offset` | `Maneuver/OperationTransfer.cs` defines class **`OperationGeneric`**: `Capture` (bool, `true`), `PlanCapture` (bool, `true`), `MatchOrbit` (bool), `LagTime` (EditableDouble, 0 s), `MinDepartureUT`/`MaxDepartureUT` (EditableTime), `Coplanar` (bool) | the *semantics* map (`intercept_only` ≈ `Capture=false`, `simple_transfer` ≈ `Coplanar`); the names do not |
| `OperationCourseCorrection.intercept_distance`, `course_correct_final_pe_a` | **`InterceptDistance`** (EditableDoubleMult, default **200 m**), **`Periapsis`** (EditableDoubleMult, 200 000 km), `Inclination` (90°), `InclinationFlag` (bool) | `Periapsis`/`Inclination` are for *body* encounters; the vessel-rendezvous knob is `InterceptDistance` |
| `NodeExecutor.tolerance` (m/s; "stop when remaining Δv < tolerance") | **no such field.** Persisted: `Autowarp` (bool, `true`), `LeadTime` (EditableDouble, **3 s**), `InitialWarpLeadTime` (600 s), `AlignedToleranceDegrees` (**1°**), `WarpAlignedToleranceDegrees` (10°), `KillRollRotation` (bool, `true`) | cutoff is **not** a Δv tolerance: stock mode ends the burn when the vessel has rotated past 90° from the node direction (`AngleFromNode() < 0.5*PI` ceases to hold); Principia mode ends when `_dvLeft <= 0`. **§B9's "Tolerance ~0.1, tighten to 0.05" has nothing to set.** The precision knob that *does* exist is `AlignedToleranceDegrees`. |
| `NodeExecutor.lead_time` | **`LeadTime`** — ends warp, starts RCS ullage, and always applies a minimum ullage pulse immediately before ignition | §B10.1's 3–5 s target stands; the field is real |
| `OperationPlane` "TimeSelector = AN/DN" | confirmed — `REL_HIGHEST_AD`, `REL_NEAREST_AD`, `REL_ASCENDING`, `REL_DESCENDING`; **no user fields** | as planned |
| `landing_autopilot` fields | confirmed exactly — `TouchdownSpeed` (0.5), `DeployGears` (true), `LimitGearsStage` (0), `DeployChutes` (true), `LimitChutesStage` (0), `RCSAdjustment` (true); entry points `LandAtPositionTarget(controller)`, `LandUntargeted(controller)`, `StopLanding()` | matches our cfg block 1:1 |
| `DockingAutopilot.speed_limit` | **`speedLimit`** (EditableDouble, 1) plus `forceRol`/`rol`, `overrideSafeDistance`/**`overridenSafeDistance`**, `overrideTargetSize`/**`overridenTargetSize`** | note the `overriden…` spelling of the value fields |

Also in current source and **not** in §B10.2 — worth knowing before the rendezvous phase is built:
`OperationLambert` (fixed-time-of-flight intercept), `OperationAdvancedTransfer` (porkchop),
`OperationLongitude`, `OperationResonantOrbit`, `OperationEccentricity`, `OperationStationary`,
`OperationMoonReturn`.

Full `TimeSelector` reference set: `COMPUTED`, `PERIAPSIS`, `APOAPSIS`, `X_FROM_NOW`, `ALTITUDE`,
`EQ_DESCENDING`, `EQ_ASCENDING`, `REL_NEAREST_AD`, `REL_ASCENDING`, `REL_DESCENDING`, `CLOSEST_APPROACH`.

### 0.4 Phase numbering

This doc uses the **owner's seven-phase numbering**. Mapping to §B9's phases:

| here | §B9 |
|---|---|
| **1** Launch → insertion → phasing | Phase 0 (pad) + Phase 1 (ascent) + Phase 2 (insertion trim & phasing) |
| **2** Falcon-9 booster recovery | ⚠ **not in §B9** — the scope-flagged extension |
| **3** Rendezvous | Phase 3 |
| **4** Docking | Phase 4 |
| **5** Undocking + departure | Phase 5 (docked) + Phase 6 (undock/departure) |
| **6** Re-entry starting orbit | ⚠ implicit in §B9 between Phase 6 and Phase 7 — made explicit here |
| **7** Deorbit + entry to a designated LZ | Phase 7 (deorbit) + Phase 8 (entry) + Phase 9 (chutes) + Phase 10 (splashdown) |

---

# PHASE 1 — LAUNCH → INSERTION → PHASING → rendezvous setup

**Real events (§8):** Liftoff · pitch kick ~0:10 · **Max-Q ~1:00–1:12** · Mach 1 ~1:09 · stage-1b abort mode
~1:14 · **MECO ~2:30–2:36** · stage sep ~2:35–2:39 · S2 ignition ~2:36–2:47 · **SECO-1 ~4:20–8:47** ·
Dragon sep ~9:00–12:02 · nose-cone open ~12:48–13:23. Crew-2's own numbers **[DOC]**: Max-Q T+1:02,
MECO T+2:36, sep T+2:39, S2 start T+2:47, SECO-1 T+8:47, Dragon sep T+11:58.

**Modules:** `MechJebModuleAscentSettings` (the store) + `MechJebModuleAscentPVGAutopilot` (the flier) +
`MechJebModulePVGGlueBall` + `MechJebModuleGuidanceController` + `MechJebModuleStagingController` +
`MechJebModuleThrustController` + `MechJebModuleAttitudeController.BetterController`. Then, post-sep:
`MechJebModuleManeuverPlanner` `Operation*` → `MechJebModuleNodeExecutor`.

---

## 1A. Pad / prelaunch (§B9 Phase 0)

| Do | How |
|---|---|
| load the Crew-Dragon per-vessel-type profile | it loads itself: MechJeb reads `mechjeb_settings_type_<type>.cfg` for the vessel type. **Verify the vessel type string matches** or a fresh default profile is written instead — ⚠ our repo copy is named `…_Crew-Dragon.cfg`, `BUILD_PLAN.md:315` calls it `…_Crew-2.cfg`. Same file, two names (`docs/INDEX.md:78`); **do not invent a third.** |
| set the target = ISS | `MechJebModuleTargetController` — cfg block EMPTY, so it is set live |
| arm PVG ascent guidance | `AscentTypeInteger = 1` is already persisted; the autopilot is engaged by enabling `MechJebModuleAscentPVGAutopilot` |
| ⚠ **KSP-side prerequisites, not MechJeb settings** | (1) default throttle **100 %** in KSP settings, and (2) MechJeb Settings → **"Module disabling does not kill throttle (RSS/RO)"** ON. Without both, throttle cuts to zero the moment PVG is disengaged **[RO]**. (3) The vessel must be **rooted to the payload**, not to a decoupler or tank. (4) Sufficient avionics on **every** stage, or guidance drops out mid-ascent and the rocket burns straight down **[RO]**. |

No burn, no live tuning — this phase only loads §B8.

---

## 1B. THE ASCENT PROFILE — every knob, enumerated

**The governing principle (§B8, unchanged): PVG is a VACUUM-only optimiser.** The ascent is two regimes:

1. an **OPEN-LOOP pitch program** you tune by hand for the aerodynamic climb — vertical rise, pitch kick,
   zero-AoA gravity turn;
2. handed off at the **max-Q trigger** to **CLOSED-LOOP PVG**, which optimises the vacuum arc to the target
   orbit and no longer cares what you set the pitch program to.

Every knob below belongs to exactly one of those two regimes, plus the limits that protect the vehicle across
the hand-off. **Tune regime 1. Regime 2 is target-setting, not tuning.**

### 1B.i The ascent-curve knobs (regime 1 — the open-loop pitch program)

| Setting | Units | cfg key (ValConfig / TextConfig) | Crew-Dragon **[cfg]** | RO default **[RO]** | What it does · how to tune |
|---|---|---|---|---|---|
| **`AscentTypeInteger`** | enum | bare int | **1 = PVG** | 1 for RSS/RO | CLASSIC(0) / PVG(1). **Leave at 1.** Classic's whole gravity-turn block below goes inert under PVG. |
| **`PitchStartVelocity`** | m/s | `70 / 70` | **70** | **50** | Surface speed at which the vertical rise ends and the pitch program starts. Raise for low sea-level TWR — F9 lifts off at TWR ≈ 1.2–1.3, which is low, hence 70. RO guidance: **75–100 for very low SLT**. Paired with `PitchRate`; change one at a time. |
| **`PitchRate`** | °/s | `0.75 / 0.75` | **0.75** | start **1.0**; range **0.1–2.0** | **THE primary ascent knob.** How fast the vehicle pitches over once `PitchStartVelocity` is reached. Higher SLT → higher rate. Tuned by the flight-recorder method in **1B.v**, in **±0.1 °/s steps**. |
| **`TurnStartAltitude`** | m | `500 / 0.5` | **500 m** | 500 m | Altitude gate for the pitch program (whichever of altitude/velocity is met). Leave. |
| **`TurnStartVelocity`** | m/s | `50 / 50` | **50** | 50 | Velocity gate for the same. Leave — note it is **not** the same knob as `PitchStartVelocity` (70); the pitch *program* starts at the turn gates, the pitch *kick* at `PitchStartVelocity`. ⚠ Verify this distinction against the pinned source before wiring. |
| `TurnEndAltitude` | m | `60000 / 60` | 60 km | 60 km | **CLASSIC only — inert under PVG.** Leave at stock. |
| `TurnEndAngle` | ° | `0 / 0` | 0 | 0 | CLASSIC only. |
| `TurnShapeExponent` | frac / % | `0.4 / 40` | 0.4 (40 %) | 0.4 | CLASSIC only — the gravity-turn curve shape. |
| `AutoPath` | bool | `AutoPath = True` | True | — | CLASSIC only; with `AutoTurnPerc 0.05`, `AutoTurnSpdFactor 18.5`. Harmless under PVG. |
| **`ForceRoll` + `VerticalRoll` / `TurnRoll` / `RollAltitude`** | bool + ° + m | `True`; `0 / 0 / 50` | **True**, 0°, 0°, 50 m | — | Commands a fixed roll (heads-down / heads-up). Both roll angles are **0** — i.e. roll is forced but to zero. ⚠ Confirm 0/0 is intended for a crewed F9 (real F9 flies a specific roll program); this is a cheap, cosmetic-but-visible detail. |

### 1B.ii The max-Q / aero-load knobs (the hand-off and the protections)

| Setting | Units | cfg key | Crew-Dragon **[cfg]** | RO default **[RO]** | What it does · how to tune |
|---|---|---|---|---|---|
| **`DynamicPressureTrigger`** | Pa (disp. kPa) | `10000 / 10` | **10 kPa** | **10 kPa — "almost always leave this"** | **THE open-loop → PVG hand-off.** PVG takes over when q falls back through this value on the way up. Only touch it if the vehicle is still deep in atmosphere at 10 kPa. |
| **`LimitQaEnabled`** | bool | `= True` | **True** | True | Master switch for the Q·α limiter. **Keep enabled through max-Q.** |
| **`LimitQa`** | Pa·rad | `2000 / 2000` | **2000** | **2000**; real-rocket range **1000–4000** | The aerodynamic-load cap: q × AoA. 1000 Pa·rad ≈ **2° of AoA at a 30 kPa max-Q**. Lower it if the stack flips or bends; raise it for a stiff vehicle. A **correctly-tuned pitch rate barely touches this limiter** — heavy limiter activity is the symptom, not the fix. |
| **`LimitAoA`** | bool | `= True` | True | True | Hard AoA cap during the pitch program. |
| **`MaxAoA`** | ° | `5 / 5` | **5°** | 5° | The cap itself. |
| **`AOALimitFadeoutPressure`** | Pa | `2500 / 2500` | 2500 | 2500 | Above this q the AoA limit is enforced; it fades out below, so the limiter stops fighting guidance in thin air. |
| **`CorrectiveSteering`** + **`CorrectiveSteeringGain`** | bool + gain | `True`; `3 / 3` | **True**, **3** | 3 | Feedback trim that steers back onto the commanded path. Raise if the profile drifts; lower if it hunts. |
| `LimitingAoA` | bool | `= False` | False | — | **Runtime state, not a setting.** Do not "tune" it; it reports whether the limiter is currently active. |
| **Max-Q target [DOC]** | — | — | — | — | **~30–35 kPa at ~12 km, T+1:02–1:12.** This is the number the pitch-rate tune must reproduce, from §B11. |

### 1B.iii The throttle / speed knobs

| Setting | Units | cfg key | Crew-Dragon **[cfg]** | RO guidance **[RO]** | Notes |
|---|---|---|---|---|---|
| **`ThrustController.LimiterMinThrottle`** | bool | `= True` | **True** | RP-1: *"don't use any of the throttle limiters — all the throttle limiters are useless"*; fly **bang-bang** (0 % or 100 %) | ⚠ **THE STANDING §B8 CONFLICT, unresolved.** PVG's optimality assumes full throttle; the real F9 **throttles down through max-Q and again for a g-limit**. See 1B.vi. |
| `ThrustController.MinThrottle` | frac | `0 / 0` | 0 | 0 | The floor `LimiterMinThrottle` clamps to. At 0 the limiter is effectively inert — ⚠ which means the flag being True may cost nothing today. **Verify in-sim before "fixing" it.** |
| `ThrustController.DifferentialThrottle` | bool | `= False` | False | False | Per-engine differential throttle for asymmetric thrust. Off. |
| **g-limiter** | g | — | — | **disable before launch** | RO explicitly says turn it off for PVG. **[EST]** real F9 peak axial accel ≈ **4 g** near MECO and again near SECO (§B11) — the number to cap at *if* we ever model a g-limit. |
| `ClampAutoStageThrustPct` | frac | `0.99 / 0.99` | 0.99 | 0.99 | Autostage fires when thrust drops below 99 % — i.e. on burnout. Leave. |

### 1B.iv Target orbit, PVG stage/coast, staging, attitude

| Setting | cfg key | Crew-Dragon **[cfg]** | Notes |
|---|---|---|---|
| **`DesiredOrbitAltitude`** / **`DesiredApoapsis`** | `210000 / 210` | **210 km** | Dragon inserts LOW and phases up to the ISS (~420 km) — this is correct, not a mistake. §B11 **[DOC]**: real insertion **~190–210 km**. |
| **`DesiredInclination`** | `-51.6316` | **−51.6316°** | ISS. **Negative = the descending-node solution** (launch azimuth south-east-ish); cannot be less than the launch-site latitude 28.6°. |
| `DesiredLan` / `RelativeLAN` / `LaunchPhaseAngle` / `LaunchLANDifference` | `0` | 0 | Not used — we are not launch-window-phasing with PVG. ⚠ If Part B ever wants a real ISS launch window it lives here. |
| **`SkipCircularization`** | `= True` | **True** | PVG inserts directly to the target; no separate circularisation burn. |
| **`AttachAltFlag`** + `DesiredAttachAlt` / `DesiredAttachAltFixed` | `True`; `210000 / 210` | **True, 210 km** | ⚠ **OPEN (§B8).** Forces burnout at a specific altitude instead of letting PVG free-optimise. Attach-alt is mainly a shuttle-style 90×180 tool. **Verify against Dragon's real insertion**; the alternative is `AttachAltFlag = False`. |
| `DesiredFPA` | `0 / 0` | 0 rad | Flight-path angle at insertion; 0 = horizontal. Consistent with a circular insert. |
| **`FixedCoast`** + `FixedCoastLength` / `MaxCoast` / `MinCoast` | `True`; `0` / `450` / `0` | **FixedCoast True, length 0** | ⚠ `FixedCoast = True` with `FixedCoastLength = 0` means **no coast arc**. `MaxCoast 450` s / `MinCoast 0` are then unused. For a two-burn insertion this is right; **confirm** against the real SECO-1 profile. |
| **`OptimizeStageFlag`** + **`OptimizeStageInternal`** | `True`; `8 / 8` | **True, stage 8** | The stage PVG optimises burnout on = the last powered stage. ⚠ **8 is craft-specific** — it must match the actual staging of the craft the owner supplies, or the optimiser targets the wrong stage. RO guidance: same as "Last Stage" unless the last stage is a solid. |
| `MinDeltaV` | `40 / 40` | 40 m/s | Stages below this Δv (ullage motors, sep motors) are excluded from the optimiser. |
| `CoastStageFlag` / `CoastStageInternal` | `False`; `-1` | off | No coast stage. |
| `SpinupStageFlag` / `SpinupStageInternal` / `SpinupLeadTime` / `SpinupAngularVelocity` | `False`; `-1`; `50`; `1.0472 rad/s (10 RPM)` | off | Spin-stabilised upper stages only. Not F9. |
| `UnguidedStagesFlag` / `UnguidedStagesInternal` | `False`; empty | off | For solid stages PVG must not steer. Not F9. |
| `StagingTriggerFlag` / `StagingTrigger` | `False`; `1` | off | Manual staging trigger — off, because **autostage owns staging**. |
| `PreStageTime` / `OptimizerPauseTime` | `10` / `5` | 10 s / 5 s | The optimiser stops re-solving `PreStageTime` before a stage event and resumes `OptimizerPauseTime` after. Leave. |
| **`_autostage`** | `= True` | **True** | **MUST be on** — PVG's stage prediction depends on it. Never stage by hand mid-ascent. |
| **Staging: `HotStaging` + `HotStagingLeadTime`** | `True`; `1 / 1` | **True, 1 s** | ⚠ **OPEN (§B8).** The real F9 does a **cold** stage separation. Review whether hot-staging semantics mis-fire on this stack. |
| `DropSolids` / `DropSolidsLeadTime` | `True`; `1` | on, 1 s | No solids on F9. Inert. |
| `AutostagePreDelay` / `AutostagePostDelay` / `AutostageLimit` | `0` / `0.5` / `0` | 0 / 0.5 s / 0 | Leave. |
| **Fairing: `FairingMaxDynamicPressure` / `FairingMinAltitude` / `FairingMaxAerothermalFlux`** | `5000 / 5`, `50000 / 50`, `1135` | 5 kPa, 50 km, 1135 | ⚠ **Dragon has NO fairing** (the nose cone hinges and stays). Confirm these cannot mis-trigger on a hinged nose cone or on the trunk. RO also warns MechJeb cannot tell an interstage fairing from a payload fairing. |
| `AutodeploySolarPanels` / `AutoDeployAntennas` | `True` / `True` | on | ⚠ Dragon's arrays are **body-mounted on the trunk and do not deploy**. Harmless, but it is a Dragon-inaccurate flag; confirm it does nothing on this craft. |
| **Attitude `BetterController`** | `PosKp 2.03` · `PosTi 1.97` · `PosTd 0` · `VelKp 7.98` · `VelTi 0` · `VelTd 0` · `RollControlRange 5` · `MaxStoppingTime 2` · `MinFlipTime 120` · `Soften 0.5` · `SmoothTorque 0.1` · `PosDeadband 0` · `VelDeadband 0` · `UseControlRange/UseFlipTime/UseStoppingTime True` · `Version 16` | as listed | The launch pointing PID. **Touch only if the stack oscillates (lower `PosKp`, raise `Soften`) or is sluggish (raise `PosKp`).** This is a *last* resort — a pitch-rate problem looks like a PID problem and is not one. |

### 1B.v ⭐ The ascent tuning METHOD (the one empirical loop that matters)

This is `PitchRate`, and only `PitchRate`, tuned against the **Flight Recorder** graph. Set the recorder to
**Time** on the x-axis (not Downrange) and display **Q, Angle-of-Attack, Pitch**. Fly to max-Q and read:

| What the AoA trace does | Diagnosis | Fix |
|---|---|---|
| AoA stays **flat at zero** through max-Q, then opens into a smooth parabola after the Q·α hand-off; one small downward bump at pitch-start; a smooth knee in the pitch trace | **OPTIMAL** | lock it |
| AoA **spikes sharply AT max-Q** (~90–95 % of the way there), pitch command dives, Q·α limiter takes over | **OVERLOFTED** — pitch rate too LOW | **raise `PitchRate` by 0.1** |
| AoA **deviates well BEFORE max-Q**; guidance meets the vacuum prediction early and sits on the Q·α constraint | **OVERPITCHED** — pitch rate too HIGH; *aerodynamic-breakup risk* | **lower `PitchRate` by 0.1**, floor 0.1; if it is still wrong at the floor, raise `PitchStartVelocity` to 75–100 and reset `PitchRate` to 0.5 |

**One parameter at a time (§B5).** The order to converge them: `PitchRate` → (only if it bottoms/tops out)
`PitchStartVelocity` → `LimitQa` → the throttle question (1B.vi) → everything else. Validate against §B11's
**[DOC]** ascent targets: max-Q 30–35 kPa at ~12 km / T+1:02; MECO ~T+2:36 at **~80 km, ~Mach 10**;
SECO-1 ~T+8:47; insertion 190–210 km × 51.63°.

**The ANALYSIS tooling for this already exists in the repo** and does not need writing: `plugin/tools/assess_flight.py`
(per-phase flight assessment — ascent, booster, rendezvous/phasing, deorbit/entry/chute, abort/FDIR, control
authority) and `plugin/tools/tuning_db.py` (per-phase statistics of every control signal — rates, actuation,
pointing error, throttle, AoA, q, g — written to `docs/tuning/TUNING_DB.json`). **The RECORDER that produces
the flight corpus for them to read does NOT exist** — it was deleted with the rest of the autopilot (2026-09-01)
and must be rebuilt fresh as BlackBox (`docs/BLACKBOX_RESEARCH.md`, S59); the analysers themselves survive
only as historical-corpus readers until then (Open Question Q3, `docs/AUTOPILOT_RECOVERY_AUDIT.md`). Both
need a **flight corpus**, which only exists once Part B flies BlackBox. `plugin/build/assess_flight.py` is
the older-schema copy, retained deliberately for exactly this tune (T22).

### 1B.vi ⚠ The four open ascent questions (§B8, still open — none is a build-chat call)

1. **Throttle: real F9 program vs PVG bang-bang.** RO says limiters off; the real vehicle throttles down
   through max-Q and holds ~4 g. Accepting bang-bang costs realism at max-Q; modelling the throttle program
   costs PVG optimality. **Empirical + owner.**
2. **Hot vs cold staging.** `HotStaging = True` vs the real F9's cold sep.
3. **Attach-altitude vs free-optimise.** `AttachAltFlag = True` at 210 km vs letting PVG choose burnout.
4. **Fairing logic on a fairingless vehicle.** Confirm the three fairing keys cannot fire on the nose cone.

---

## 1C. Insertion trim & the phasing ladder (§B9 Phase 2)

Dragon separates onto ~210 km and must set up a **catch-up orbit below the ISS**, closing the phase angle so
that **docking falls ~19 h after launch [DOC]**. Lower phasing orbit = faster catch-up.

| Step | Module / operation | Fields to set | Target |
|---|---|---|---|
| clean up insertion | `OperationCircularize` | none; `TimeSelector` = `APOAPSIS` | only if PVG's insert is off; with `SkipCircularization = True` it usually is not needed |
| shape the phasing orbit | `OperationEllipticize` (`NewApoapsis` + `NewPeriapsis`, m) or `OperationPeriapsis`/`OperationApoapsis` | apsides, in **metres** | the ladder that makes time-to-dock ≈ 19 h |
| precise period control | `OperationSemiMajor` (`NewSemiMajorAxis`, m) | SMA in metres | the fine phasing knob — period, not shape |
| fly each node | `MechJebModuleNodeExecutor.ExecuteOneNode(controller)` | `LeadTime` 3–5 s, `Autowarp` true | **one node at a time**, re-plan after each |
| skip the coast | `MechJebModuleWarpHelper` (`phaseAngle`, cfg 0) | lead/phase to stop warp before the next burn | ⚠ set a lead so warp does not overshoot the burn |

**⚠ The phasing floor.** The repo already fixes a safety floor: `plugin/tools/assess_flight.py:26`
`PE_FLOOR_KM = 150.0  # the rendezvous/phasing safety floor`. **No phasing periapsis below 150 km.**

**⚠ The known failure mode, from our own flight record.** `docs/FLIGHT_144114_SCREEN_AUDIT.md` (the flight the
*deleted* autopilot flew): *rendezvous/phasing self-deorbits, drains the return propellant, and never docks.*
Whatever the phasing ladder ends up being, the two acceptance tests are: **periapsis never goes below the
floor**, and **the return-propellant budget is still intact at docking**. `plugin/src/VesselData.cs:886-928`
already keeps a Dragon-only deorbit fuel/ox ledger — that is the gauge to read.

**⚠ Real vs approximated.** Real Dragon flies a scripted named burn series (Phase → Boost → Close → Transfer →
Coelliptic → Out-of-plane, §8). We approximate it with apsis targets tuned so total time-to-dock ≈ nominal.
The *names* on the screens should still be the real ones.

---

# PHASE 2 — FALCON-9 BOOSTER RECOVERY  ⚠ NEW SCOPE

> **Read the SCOPE FLAG at the top of this doc first.** This phase is **not** in §B1–§B15. It is an
> owner-directed **extension** of Part B, on a **separate vessel**, to be folded into the plan as its own
> §B section and register task(s) **when Part B starts**. Nothing here is authority to build it.
>
> ⚠ **It also contradicts a live doc line that will need updating when the owner folds this in:**
> `docs/TELEMETRY_REGISTRY.md:9` currently states *"`BOOSTER_STATUS` is dead: booster recovery was deleted
> with the autopilot, **Part B does not re-introduce it**."* That sentence was true when written and is what
> this extension changes. **Logged, not edited** (C1.1).

## 2.0 What already exists in the tree (do not re-derive it)

The booster-recovery *implementation* was deleted 2026-09-01, but its **seams and its vehicle model survived**:

| File | What it still holds |
|---|---|
| **`plugin/src/pure/VehicleParts.cs`** | **the Falcon-9 octaweb model** — `BoosterMarker ".S1."`, `OctawebEngineCount = 9`, `ModeAllEngines = 0 / ModeThreeEngine = 1 / ModeCentreOnly = 2`, `OctawebModeFor(int engines)`, `GridFinPart "Grid Fin M Titanium"`, `GridFinAnimation "NewFinsDeploy"`, `DroneshipMarker "Droneship"`, `EngineSwitchModule "ModuleTundraEngineSwitch"`, `EngineSwitchAction "next engine mode"`, `EngineIdThree "Three"`, `EngineIdCentre "Center"`. Matches on `part.name`, **never** the right-click title. |
| `plugin/build/assess_flight.py:398-431` | **the only recovery coordinates in the repo** (below), the 50 m × 25 m deck geometry, and the on-deck test |
| `plugin/src/pure/KerData.cs:64-68` | `HasRecoveryReserve(stages, reserveMps)` — "a MECO recovery reserve holds when the remaining Δv still exceeds what the booster needs to fly home". **No default reserve value is defined anywhere in the repo** — that number is a knob this phase must supply. |
| `plugin/src/RangeExtender.cs` | the physics-range extender: ON before booster separation, OFF after focus returns post-recovery; `MissionConductor` drives it; sized as max booster↔upper-stage separation + margin |
| `plugin/src/_AutopilotStub.cs:72-76, :150` | `MissionConductor.AutoRecoverBooster` (a live screen toggle nothing acts on) and `BoosterRecovery.Tracked` → null |
| `plugin/src/HullCams.cs`, `plugin/src/DockingCamRenderer.cs:438-441` | cameras follow `BoosterRecovery.Tracked`; the note that recovery used `ForceSetActiveVessel(booster)` "so the crew can watch the landing, and MechJeb does the same thing for the same reason" |

**⚠ `VehicleParts.cs` encodes the exact mechanism the owner's directive forbids** (`EngineSwitchAction =
"next engine mode"` + `OctawebModeFor`). See **2.3**. Logged as a finding; **not changed by this task**.

## 2.1 The real profile — what we are reproducing

**Crew-2's own booster, [DOC]** (the mission our cfg is tuned for — an **ASDS / droneship** recovery, so
**no boostback burn**):

| Event | T+ | Note |
|---|---|---|
| MECO | 2:36 | |
| stage separation | 2:39 | |
| **1st-stage entry burn** | **7:27** | |
| **1st-stage landing burn** | **9:03** | |
| **1st-stage landing on droneship** | **9:30** | ~27 s of landing burn |

**The two profiles [DOC]:**

| | **RTLS** (return to launch site) | **ASDS** (droneship, downrange) |
|---|---|---|
| boostback burn | **yes** — booster flips ~180°, **3 engines, a few seconds** | **none** (Crew-2 had none) |
| entry burn | **3 engines**, from ~55–70 km | **3 engines** |
| landing burn | **1 engine** (centre) | 1 engine (**or 3-then-1** — see 2.3) |
| recovery propellant cost | **~10 %** of total | **~6 %** of total |
| target | LZ-1 / LZ-2 | droneship, several hundred km downrange |

**Targets, from the repo's own record** (`plugin/build/assess_flight.py:404`, the only lat/lon pair that
survived the deletion):

```python
PAD, BARGE = (28.6084, -80.6043), (32.787551, -76.644507)
```
`PAD` = **LC-39A**. `BARGE` = the droneship **deck centre** (group centre + the ~5.7 m model offset), which is
the aim point — *not* the group centre or a waypoint. Deck is **50 m × 25 m**; the on-deck test is
`abs(along) <= 25 m and abs(cross) <= 12.5 m`. **[DOC]** for reference: LZ-1/LZ-2 sit at **28.4858 N,
80.5444 W**; LZ-1 retired Aug 2025 after 54 landings, LZ-2 remains active.

## 2.2 The established RSS/RO method — five phases

⚠ **PARTLY SUPERSEDED 2026-09-03 (G5a-Q3) — see `BUILD_PLAN.md` §B12.7/§B16.2/§B16.4/§B16.7.** The phase
table below still shows row 1 as **"BOOSTBACK (RTLS only)"** — that is the pre-amendment split. §B16.2 was
amended (owner, 2026-09-03): **boostback is now ONE ALWAYS-ENTERED state for BOTH profiles**, magnitude and
aim-point parameterized by target mode, with ASDS defaulting to a ZERO-MAGNITUDE trim until a recorded
flight says otherwise. On any conflict THE PLAN WINS (C7.1); this section's method/parameter names still
stand, only the "RTLS only" gating on boostback does not.

This is the decomposition the RSS/RO community converged on (it is what **BoosterGuidance** implements —
*"has arisen out of the HopperGuidance mod… made to work in Realism Overhaul where limited throttleable
engines and limited ignitions mean you can achieve successful recoveries"*, GPL-3.0). **That the mod exists
at all is the finding: MechJeb's own landing autopilot was not built for a limited-ignition, limited-throttle
booster.** See 2.5.

| # | Phase | What the vehicle does | Exit condition |
|---|---|---|---|
| 1 | **BOOSTBACK** (RTLS only) | rotate to null the target error using RCS + thrust vectoring, then throttle up | target error < ~20 m, or it stops improving |
| 2 | **COAST** | ballistic arc, steering **retrograde**; grid fins stowed or trimming | reaching the entry-burn altitude |
| 3 | **ENTRY BURN** | engines at full throttle (tapering at the end); steer **slightly off retrograde** to null target error | velocity drops below the entry-burn target velocity |
| 4 | **AERO DESCENT** | engines **off**; **grid fins** steer to the target; drag does the braking | reaching the landing-burn altitude |
| 5 | **LANDING BURN** | decelerate to ~zero at the pad; **ignite EARLY to cover RO ignition delay** | touchdown |

**The parameters this method exposes** (names as BoosterGuidance states them — they are the *knobs the
conductor would need*, whoever implements the guidance):

| Parameter | Units | Meaning | Value |
|---|---|---|---|
| target latitude / longitude / altitude | ° / ° / m | the aim point | `PAD` or `BARGE` above |
| **gain** (re-entry / aero / landing — one each) | ratio | steering aggressiveness per phase | tune per phase |
| **maximum angle-of-attack** | ° | steering deflection limit | **20** |
| **touchdown margin** | m | height above target where deceleration must be complete | **10 m normally, 30 m for RO** |
| **touchdown speed** | m/s | the gentle final velocity | set |
| **deploy gear height** | m | ⚠ **F9 legs**, not Dragon — Dragon has none | set for the booster only |
| **no-steer height** | m | below this, stop targeting and just land | set |
| **engine startup** | s | ignition lead time — **critical in RO** | set from the engine's real start transient |
| **entry-burn altitude / target velocity** | m / m/s | when the entry burn starts and what it slows to | tune |

## 2.3 ⛔ ENGINE CONTROL — the owner's operational direction

**Captured verbatim in substance, owner directive 2026-09-03:**

> **Do NOT cycle "next engine mode".** RO's `ModuleEngineConfigs` mode-cycling causes engine
> **RE-IGNITIONS** and **lag**. Instead, **read the CRAFT FILE's engine list** to identify the **THREE
> landing engines**, and **control them SEPARATELY** — the 3-engine → 1-engine landing-burn throttle profile.

**Why this matters, mechanically:**

- In RO, **`ignitions` is a finite, per-engine resource** (`ignitions: the number of ignitions the engine has.
  Defaults to -1 (unlimited)`; limited-ignition engines consume `IGNITOR_RESOURCE` nodes and *"will fail to
  ignite (but still use up an ignition) if they are not available"*). A Merlin has a small, countable budget
  covering boostback + entry + landing. **Anything that re-ignites an engine spends one.**
- A mode/config switch is a **vehicle-level reconfiguration**, not a throttle command. RealFuels' own
  `ChangeEngineType` path has a history of instability (a logged CTD issue), and the switch is applied to the
  part, not to a single engine — so it cannot express *"these three, not those six"*.
- **The forbidden path is already in our tree:** `VehicleParts.cs` holds
  `EngineSwitchModule = "ModuleTundraEngineSwitch"`, `EngineSwitchAction = "next engine mode"`,
  `EngineIdThree = "Three"`, `EngineIdCentre = "Center"` and `OctawebModeFor(int engines)`. That is the
  mode-cycling API. **Under this directive the recovery guidance must not use it.** (The constants can stay —
  they are a correct description of the part; the directive is about what the *flight software* calls.)

**The method instead — per-engine control.** Every KSP engine is a `ModuleEngines` (RO's is
`ModuleEnginesRF`, which derives from it), and the stock API already exposes exactly what is needed:

| Need | Field / call on `ModuleEngines` | Notes |
|---|---|---|
| light **this** engine | `Activate()` | consumes one RO ignition |
| shut **this** engine down | `Shutdown()` | RealFuels: *"when setting 0 throttle, the engine instantly shuts off"* — so a **throttle floor is not a shutdown**, and vice-versa |
| throttle **this** engine independently of the vessel throttle | **`independentThrottle`** (bool) + **`independentThrottlePercentage`** | RealFuels' own `ModuleEnginesRF` computes `controlThrottle` from `independentThrottlePercentage * 0.01f`; KSP-Interstellar uses the same trio. **This is the field that makes a 3-engine→1-engine profile possible without touching engine modes.** |
| cap **this** engine's thrust | `thrustPercentage` (0–100, the thrust limiter) | the coarse alternative to independent throttle |
| how many ignitions are left | RealFuels `ignitions` on the engine's `ModuleEnginesRF`/config | ⚠ read it, budget it, display it — never assume |

**The 3 → 1 landing-burn throttle profile** the directive names, expressed in these terms:

1. **Entry burn:** `Activate()` the **three** identified engines; full throttle; taper at the end.
2. **Aero descent:** `Shutdown()` all three. (⚠ Each restart later costs an ignition — this is the decision
   point between a 3-engine and a 1-engine landing burn.)
3. **Landing burn, high-thrust segment:** light the **three** engines (or the centre one alone, if the
   ignition budget or the RO throttle floor forbids three) — **early**, by the engine-startup lead time.
4. **Landing burn, terminal segment:** shut the **two outboard** engines down; the **centre** engine alone
   flies the last segment on `independentThrottlePercentage`, which is where the real vehicle's fine
   throttle authority lives.
5. **Never command zero throttle mid-landing-burn** — that is an instant shutdown, and relighting costs an
   ignition the vehicle may not have. Hold a floor above the engine's minimum instead.

## 2.4 ⚠ The craft dump — owner-supplied, and the ONLY source for the engine list

⚠ **PARTLY SUPERSEDED 2026-09-03 (G5a-Q3) — see `BUILD_PLAN.md` §B12.7/§B16.2/§B16.4/§B16.7.** Two claims
below are now false/wrong. **(1)** *"there are no `.craft` files in this repo"* — **16 mission `.craft` +
`.loadmeta` pairs were committed by G5a** (`docs/reference/`, `INDEX.md` §3); the craft-dump prerequisite
this section calls OPEN is resolved. **(2)** the by-position engine procedure (*"Expect
`OctawebEngineCount = 9`"*, step 3 below) is **superseded by §B16.4**: the octaweb is confirmed **one part**
carrying **three** `ModuleEnginesRF` modules (`AllEngines`/`ThreeLanding`/`CenterOnly`), not nine engine
parts — binding is by module role against `reference/craftdump.csv`, never by position-counting nine parts.
On any conflict THE PLAN WINS (C7.1).

**C7 forbids reading the KSP install**, and **there are no `.craft` files in this repo** (verified: no
`*.craft`, no `Ships/`, no `.sfs`; `CRAFT_DUMP_VEHICLE_MAP` was deleted 2026-09-01). So:

> **The exact 3-engine configuration is per-craft. The OWNER supplies the craft dump. Until then this section
> documents the METHOD against an owner-supplied engine list, and invents no part names.**

**The procedure, once the dump exists:**

1. A `.craft` is a KSP `ConfigNode` text file of `PART { … }` nodes; each part node carries its part name, its
   position/rotation, and nested `MODULE { name = … }` nodes. ⚠ **The exact key spellings must be read off the
   owner's dump, not assumed** — publicly there is no authoritative `.craft` format spec, and §1.4 forbids
   inventing them.
2. Filter to the **booster**: `VehicleParts.IsBooster(part.name)` — the `".S1."` marker.
3. Within it, list every part carrying a `ModuleEngines`/`ModuleEnginesRF` module. Expect
   `OctawebEngineCount = 9`.
4. Identify the **centre** engine and the **two** it burns with, by **position** — the centre engine sits on
   the booster's thrust axis; the other eight are the outer ring. ⚠ Confirm against the dump's own position
   field; do not guess which ring members SpaceX uses (the real vehicle uses the centre plus two opposed
   outboards for a 3-engine burn).
5. Record the result as a **fixed, named engine table** in the conductor — resolved once, by part id, at
   staging — never re-searched per frame.

**The table to fill from the owner's dump** (empty on purpose — filling it now would be invention):

| Role | part name | part id / uid | position | ignitions | min throttle | notes |
|---|---|---|---|---|---|---|
| centre / landing | — | — | — | — | — | flies the terminal segment |
| outboard A | — | — | — | — | — | 3-engine burns |
| outboard B | — | — | — | — | — | 3-engine burns |
| ring ×6 | — | — | — | — | — | ascent only |

## 2.5 MechJeb's part in this — what it can and cannot do

**What MechJeb gives you for a booster:**

- `MechJebModuleLandingAutopilot.LandAtPositionTarget(controller)` with a lat/lon set through
  `MechJebModuleLandingGuidance` ("Enter target coordinates" / "Pick target on map" / the landing-site
  dropdown, which reads **RSS site configs** as well as KSP launch sites and Kerbal-Konstructs) — this is how
  the **LZ / droneship coordinates** get in.
- `MechJebModuleLandingPredictions` — the descent prediction and the trajectory overlay.
- `MechJebModuleSmartASS` — `SURFACE_RETROGRADE` for the coast and entry attitude, `SURFACE` with an explicit
  heading/pitch for a steered entry.
- `MechJebModuleAttitudeController.BetterController` — the same pointing PID as ascent, which does the
  thrust-vector steering.
- `MechJebModuleStagingController` — **off** for the booster; it must not stage anything.

**Landing-autopilot settings for the booster** (cfg values shown are the **Dragon's** — the booster is a
different vessel type and would get **its own** `mechjeb_settings_type_*.cfg`):

| Setting | Dragon **[cfg]** | **Booster** target | Why |
|---|---|---|---|
| `TouchdownSpeed` | 0.5 | **~0.5–2** m/s ⚠ | on the booster this is a real powered target, not a splash-detect threshold as it is for Dragon |
| `DeployGears` | True | **True** | ⚠ opposite of Dragon: the **booster HAS legs**, Dragon has none |
| `LimitGearsStage` | 0 | the leg-deploy stage | must match the craft |
| `DeployChutes` | True | **False** | no chutes on a propulsive booster |
| `LimitChutesStage` | 0 | n/a | |
| `RCSAdjustment` | True | **True** | RCS trims the descent; the cold-gas thrusters do the flip |

**What MechJeb does NOT give you, and why BoosterGuidance exists:**

- No **boostback** phase, no **entry-burn** phase, no **grid-fin** steering, and no notion of a
  **phase-by-phase engine count**. Its landing autopilot plans a deorbit + course correction + a suicide burn
  for a *lander*, assuming freely-throttleable engines and unlimited relights.
- ⚠ **The known RO trap, stated by BoosterGuidance and worth quoting into any implementation:** the
  landing-burn start altitude gets computed **before** the propellant that earlier phases will burn is
  actually consumed, so the predicted mass is too high and the burn is armed **too early** — until after the
  entry burn. The stated mitigation is a **larger touchdown margin (30 m in RO)**.

**⚠ THE OWNER DECISION this forces** (§10): implement the five-phase method **inside our conductor** on top of
MechJeb's attitude + prediction modules; or accept MechJeb's landing autopilot as-is and its limits; or add
**BoosterGuidance** as a second mod dependency. BoosterGuidance is **GPL-3.0**, i.e. licence-compatible with
DragonScreen and with the pinned MechJeb embed — but **§B3's packaging decision covers MechJeb only**, and
adding or vendoring a second mod is not a build-chat call.

## 2.6 RO gotchas for this phase — the ones that actually kill boosters

1. **⚠ ULLAGE. This is the failure we have already had.** `docs/FLIGHT_144114_SCREEN_AUDIT.md` records the
   deleted autopilot's flight: ***"booster ballistic (eng never lit) → LOST (ullage)."*** RealFuels: *"if
   ullage is enabled for the engine, and your propellant stability is not 'Very Stable', there is a chance
   that vapor can get in the feed lines and the engine will flame out… set the throttle to 0 to reset, then
   stabilize your propellants."* **Settle the propellant with RCS to "Very Stable" BEFORE every booster
   relight** — boostback, entry, and landing. `GuidanceController.UllageLeadTime` (cfg **20 s**) is the
   Dragon's settle time; the booster needs its own, and RCS/solids are themselves immune to ullage.
2. **Limited ignitions.** Budget them explicitly: boostback (RTLS) + entry + landing = **3 relights minimum**,
   more if a 3-engine landing burn is restarted as 1. Do not "toggle guidance on and off" — each cycle risks
   an ignition. **Read `ignitions` and refuse a phase the budget cannot cover.**
3. **Limited throttling.** Realistic engines throttle only to **40–50 %**. A single Merlin at its floor on a
   nearly-empty booster still out-thrusts its weight — which is exactly why the real vehicle flies a
   **hoverslam** and why "hold a throttle floor, never zero" (2.3, step 5) is the rule.
4. **Throttle response is not instant.** `throttleResponseRate` on `ModuleEnginesRF` models spool-up
   (*"about two seconds for an F-1 class engine"*). This is the **engine-startup lead time** parameter — light
   early or land short.
5. **Residuals.** Engines cannot burn every drop; RealFuels exposes `predictedMaximumResiduals` as a
   multiplier on total propellant. **The recovery Δv budget must be net of residuals** — this is the number
   `KerData.HasRecoveryReserve(stages, reserveMps)` wants and does not yet have.
6. **Steering authority is aerodynamic until it is not.** Above ~100–200 m/s, aero forces dominate and the
   steering solution is unreliable; grid fins do the work, engines do not.
7. **Physics range.** `plugin/src/RangeExtender.cs` must be ON before separation or the booster is unloaded
   and deleted. Size it to max separation + margin.
8. **Vessel focus.** KSP flies one vessel at a time. Recovery historically used `ForceSetActiveVessel(booster)`
   (`DockingCamRenderer.cs:438-441`). ⚠ **That switches focus away from the Dragon mid-ascent** — a conductor
   design question, not a tuning one (§10).

---

# PHASE 3 — RENDEZVOUS  (§B9 Phase 3 / §B10.2)

**Real burn names (§8):** Phase (~T+47–50 min) → Boost → Close → Transfer → Coelliptic → Out-of-plane.
**Approach geometry [DOC] (§B11):** 4 × 2 km **Approach Ellipsoid** → halt at **~1 km** for the Go/No-Go →
**Keep-Out Sphere ≈ 200 m** → **WP1 = 220 m** on the docking axis → **WP0 = 400 m below** → **WP2 = 20 m** →
CHOP → contact. Dragon approaches **from behind and below**, then loops to ahead. Dock **~19 h** after launch.

**The rule (§B1, non-negotiable): never the rendezvous *autopilot*.** It is unreliable in RSS/RO. The
conductor composes **Maneuver-Planner `Operation*` → Node Executor**, and **re-plans after every burn**.

## 3.1 The operation chain

| Real burn | Operation | Fields to set | TimeSelector | Notes |
|---|---|---|---|---|
| **Out-of-plane** | `OperationPlane` | **none** | `REL_HIGHEST_AD` (cheapest) or `REL_NEAREST_AD` | **Do this FIRST and EARLY** — plane error is cheapest to kill far out and it corrupts every later solution |
| **Phase / Boost / Transfer** | `OperationGeneric` (⚠ the file is `OperationTransfer.cs`; the class is `OperationGeneric` — §0.3) | `Capture` (two-burn capture vs bare intercept), `PlanCapture`, `MatchOrbit`, `LagTime` (s), `Coplanar`, `MinDepartureUT`/`MaxDepartureUT` | `COMPUTED` | the big catch-up onto an intercept trajectory. §B10.2's intent: **let it optimise** rather than fly a naive coplanar Hohmann → leave `Coplanar` **off** |
| **Close / fine approach** | `OperationCourseCorrection` | **`InterceptDistance`** (m, default 200) | `COMPUTED` | run **1–2×**, walking closest approach down the ladder below |
| **Coelliptic / arrive** | `OperationKillRelVel` | **none** | `CLOSEST_APPROACH` | nulls relative velocity at the station-keeping point |
| — | fallback for a fixed-time intercept | `OperationLambert` | ⚠ not in §B10.2; available in current source | keep in reserve |

**The `InterceptDistance` ladder** (from the §B11 **[DOC]** geometry): **4000 m** (Approach Ellipsoid entry)
→ **1000 m** (the Go/No-Go hold) → **220 m** (WP1, the docking axis) → **20 m** (WP2). Below ~200 m the
Keep-Out Sphere rules apply and phase 4 takes over.

## 3.2 Node Executor settings for this phase

| Setting | Value | Why |
|---|---|---|
| `LeadTime` | **3–5 s** | ends warp, starts RCS ullage, guarantees a settle pulse at ignition. Dragon points and settles on Draco RCS — give it the time |
| `Autowarp` | **true** | skip the coasts |
| `InitialWarpLeadTime` | 600 s (default) | coarse warp threshold |
| `AlignedToleranceDegrees` | **1° (default)** | ⚠ **this is the precision knob that actually exists** — §B9's "Tolerance 0.1 → 0.05 m/s" refers to a `tolerance` field that is **not in the current source** (§0.3). Tighten *this* for the fine corrections instead |
| `WarpAlignedToleranceDegrees` | 10° (default) | |
| `KillRollRotation` | true (default) | |
| **drive method** | **`ExecuteOneNode(controller)`** — never `ExecuteAllNodes` | one burn, then **re-plan**. This is the whole §B12.4 re-plan loop |
| ullage | `GuidanceController.UllageLeadTime` **20 s [cfg]** | applies to every Node-Executor burn, not just this phase |

## 3.3 Gotchas

- **Re-plan after every burn**, from the *actual* post-burn orbit — residuals and drift make the next node
  stale immediately. Never queue the whole chain.
- **Plane first, always.** A transfer computed against an un-matched plane wastes Δv and produces an
  intercept that then needs a plane change to fix.
- ⚠ **Propellant ledger.** Same acceptance test as 1C: the phasing/rendezvous chain must **not** eat the
  return propellant. `VesselData.cs:886-928` keeps a Dragon-only deorbit fuel/ox ledger — check it at every
  re-plan, and refuse a burn that would breach the deorbit reserve.
- ⚠ **`ApproachRange` in our own code is 3000 m** (`pure/MissionPhase.cs:58`), which is the phase-FSM's
  Phasing→Approach boundary — close to, but not the same as, the 4 km ellipsoid. **Do not "fix" one to match
  the other**; they answer different questions (display phase vs guidance target).

---

# PHASE 4 — DOCKING  (§B9 Phase 4 / §B10.3)

**Real corridor [DOC]:** Approach Ellipsoid → Keep-Out Sphere (~200 m) → WP1 (220 m) → WP0 (400 m below) →
WP2 (20 m) → **CHOP** (Crew Hands-Off Point — the last abort point; the panel's BREAKOUT, §4) → contact and
capture at **IDA-2**.

**Approach-rate targets [DOC] (§B11):** Crew Dragon **final contact ≈ 0.1 m/s**, and rate must stay
**< 0.2 m/s inside 5 m**. (The 7.6 cm/s → 5 cm/s figures in §B11 are **cargo-Dragon BERTHING** numbers —
⚠ marked there and not to be used as crew-docking targets.)

## 4.1 Option (a) — Docking Autopilot

`MechJebModuleDockingAutopilot`, the only module with a persisted docking value in our cfg.

| Setting | cfg / field | **[cfg]** | Target | Notes |
|---|---|---|---|---|
| **`speedLimit`** | `speedLimit { 1 / 1 }` ⟂ `speedLimit` (EditableDouble) | **1 m/s** | **a LADDER, stepped down through the corridor** | far / KOS approach **~1** → waypoints **~0.3–0.5** → contact **0.1–0.2**. **The single most important docking knob.** It is applied as a hard clamp in `FixSpeed()` to both the axial (`zApproachSpeed`) and lateral (`latApproachSpeed`) components |
| `forceRol` + `rol` | not persisted | false / 0 | **forceRol = true**, `rol` set to the IDA-2 port alignment | roll-align to the port |
| `overrideSafeDistance` + **`overridenSafeDistance`** | not persisted | false / 5 | enable, set ≈ the **Keep-Out Sphere** | ⚠ note the `overriden…` spelling |
| `overrideTargetSize` + **`overridenTargetSize`** | not persisted | false / 10 | set if the auto bounding-box is wrong for the ISS | |

The autopilot's internal steps — `INIT`, `WRONG_SIDE_BACKING_UP`, `WRONG_SIDE_LATERAL`,
`WRONG_SIDE_SWITCHSIDE`, `BACKING_UP`, `MOVING_TO_START`, `DOCKING` — are **not settings**; they are what it
does. ⚠ The "wrong side" recovery steps will fly the vehicle **around** the target, which in RSS/RO near the
ISS is exactly the manoeuvre the Keep-Out Sphere forbids. **Arrive on the correct side (phase 3's job) so
those states never trigger.**

## 4.2 Option (b) — hand off to the Manual ISS Docking screen

Already built (Part A). MechJeb's role shrinks to **`SmartASS` in `TARGET` / `parallel_plus`** holding the
docking-axis pointing while the crew flies the translation. The docking autopilot idles.

**⚠ This is an OWNER decision (§B9 P4, still open, §10).** It is also the more *Dragon-accurate* one: the real
vehicle docks autonomously, but our screens exist to be flown from. Note the existing seam:
`ScreenPainter.cs` records that *"rendezvous + docking are flown by AUTO SEQUENCE (`CrewProcedureOps`), not
by a button"* — the front-end already assumes an automatic sequence.

## 4.3 RCS settings that matter here

| Setting | **[cfg]** | Target |
|---|---|---|
| `RCSController` PID — `Tf` / `Kp` / `Ki` / `Kd` | **1 / 0.125 / 0.07 / 0.53** | leave; retune only if Dragon is jittery or sluggish holding attitude on Draco |
| `RCSBalancer.smartTranslation` | **False** | ⚠ **turn True** if cross-coupling shows up in translation — the classic prox-ops symptom |
| `RCSBalancer.overdrive` / `overdriveScale` | 1 (100 %) / 0.9 | leave |
| `RCSBalancer` tuning factors — torque / translate / waste | 1 / 0.005 / 1 | leave |
| `MechJebModuleSmartRcs` | cfg EMPTY | live-driven: the translate-toward-target helper for prox ops |

⚠ **Dragon has 16 Dracos in fixed pods** — its translation authority is not uniform in all axes. If the
speedLimit ladder produces a crawl in one axis and lurches in another, that is thruster geometry, not a PID
problem. Fix it with `smartTranslation`, not with `RCSController` gains.

---

# PHASE 5 — UNDOCKING + DEPARTURE  (§B9 Phases 5–6)

**Docked (Phase 5):** MechJeb is **idle**. `SmartASS` = `OFF` or `KILL_ROT` — the station holds attitude, not
us. No planner burns. (If reboosts are ever modelled: `OperationPeriapsis`/`OperationApoapsis` + Node
Executor, exactly as phase 1C.)

**Undock + departure (Phase 6):**

| Step | Module | Settings |
|---|---|---|
| release + back straight out along the docking axis | `Actuator.Undock(v)` (our seam) then **RCS translation** | `SmartASS` = `TARGET` → **`target_minus`** or `parallel_minus` to hold the axis while backing away. Keep the rate **at or below the contact ladder's slowest step (~0.1–0.2 m/s)** until clear of the port |
| clear the Keep-Out Sphere | RCS only | ⚠ **no main-engine burn inside the KOS.** Depart on Dracos |
| departure burns | `OperationApoapsis` / `OperationEllipticize` (small) → `NodeExecutor.ExecuteOneNode` | Δv small; `LeadTime` 3–5 s; `Autowarp` true |
| coast attitude | `SmartASS` = `KILL_ROT` or `ORBIT` retrograde | |

**Gotchas:** the departure must open range **monotonically** — a departure burn that lowers periapsis into a
re-encounter is the classic error. And note the real sequencing (§8): **trunk jettison and nose-cone close
are vessel actions, not MechJeb burns**, and they belong to phase 7, not here.

---

# PHASE 6 — THE RE-ENTRY STARTING ORBIT

⚠ **§B9 does not name this phase** — it goes straight from departure (P6) to the deorbit burn (P7). It is
made explicit here because the owner asked for it, and because *"the orbit deorbit begins from"* is the state
that decides whether a designated-LZ entry is even reachable.

## 6.1 What the orbit has to be

| Property | Value | Why |
|---|---|---|
| shape | **near-circular** | a circular starting orbit makes the deorbit Δv and the entry FPA predictable; an eccentric one makes the corridor a function of where you burn |
| altitude | **ISS-like, ~400–420 km** | real Dragon does **not** substantially lower its orbit before deorbit — it departs, free-flies, and deorbits from near station altitude |
| inclination | **51.6°**, unchanged | never spend Δv on plane after undock |
| periapsis floor | **≥ 150 km** until the deorbit burn | the repo's own floor (`assess_flight.py:26  PE_FLOOR_KM = 150.0`) |
| **ground track** | **phased so the post-deorbit track crosses the designated splashdown site** | ⭐ **this is the real content of this phase.** Deorbit timing is a *ground-track* problem: you wait for the revolution whose entry track passes the LZ |
| propellant | deorbit Δv **~100 m/s [EST]** still in the tank, net of residuals | the ledger check from 1C/3.3, cashed here |

## 6.2 How to establish and hold it

| Need | Module | Settings |
|---|---|---|
| trim the post-departure orbit to circular at the target altitude | `OperationCircularize` (TimeSelector `APOAPSIS`) → `NodeExecutor.ExecuteOneNode` | `LeadTime` 3–5 s |
| set apsides exactly | `OperationEllipticize` (`NewApoapsis` + `NewPeriapsis`, m) | |
| fine-tune the period so the ground track walks onto the LZ | **`OperationSemiMajor`** (`NewSemiMajorAxis`, m) | ⭐ the correct tool: period, not shape, is what moves the ground track between revolutions |
| ⚠ candidate, unverified | `OperationLongitude` | present in current source, not in §B10.2 — **verify what it targets** before relying on it |
| hold attitude through the wait | `SmartASS` = **`KILL_ROT`** (or `ORBIT` prograde for a stable thermal attitude) | live-driven; nothing persisted |
| skip the wait | `MechJebModuleWarpHelper` (`phaseAngle`, cfg **0**) | ⚠ set the lead so warp stops **before** the deorbit burn, not on top of it |

## 6.3 Gotchas

- **Do not lower the orbit "to save deorbit Δv".** It costs the same Δv either way and it breaks the timeline
  the screens display (§8: deorbit ~50 min before splashdown; claw sep ~1 h 20 m before).
- **The trunk is still attached here.** It jettisons in phase 7, before entry — so this phase's mass, drag and
  RCS authority are the *trunk-attached* values.
- ⚠ **Nothing in the repo currently owns the target coordinates.** `docs/TELEMETRY_REGISTRY.md:61` gives
  `TGT_LAT`/`TGT_LON` authority to `ReturnControl`, which was deleted; `VesselData.cs:450-451` fills them from
  the KSP *target's* ground position, which is not a splashdown site. The LZ has to come from somewhere —
  see §10.

---

# PHASE 7 — DEORBIT + ENTRY TO A DESIGNATED LZ  (§B9 P7–P9 / §B10.4 / §B11)

**Real sequence (§8):** trunk jettison → **deorbit burn ~15 min** → nose-cone close + lock → entry interface
→ drogues → mains → splashdown. **Claw** (the trunk↔capsule thermal/power/avionics link) separates
**~1 h 20 m** before splashdown; the deorbit decision is ~30 min before claw-sep prep; **splashdown ~50 min
after burn start**.

**Entry targets [DOC/EST] (§B11):** entry interface **122 km (400 000 ft) at ~7.8 km/s [DOC]** · entry FPA
**≈ −1.4° to −1.6° inertial [EST]** · deorbit Δv **~100 m/s [EST]** · peak entry decel **~4–4.5 g nominal
[DOC/EST]** (generic crew-capsule worst case 7–8 g) · heat shield ~1927 °C **[DOC]**.

## 7.1 Two ways to fly the deorbit — pick one, they are not complementary

**(a) Corridor-controlled: `OperationPeriapsis` → Node Executor.** You choose the entry corridor directly.

| Setting | Value | Notes |
|---|---|---|
| `OperationPeriapsis.NewPeriapsis` | **metres**, a low or negative value that puts the entry FPA in corridor | ⚠ the FPA is the *real* target; periapsis is only the handle. Too shallow → skip; too steep → over-g and over-heat |
| TimeSelector | the point on the orbit whose entry track crosses the LZ (phase 6) | this is where the ground-track phasing is cashed in |
| `NodeExecutor.LeadTime` | 3–5 s | |
| `GuidanceController.UllageLeadTime` | **20 s [cfg]** | a long coast precedes this burn on pressure-fed Dracos — **do not shorten it** |

**(b) Site-targeted: Landing Guidance → `LandAtPositionTarget(controller)`.** You give it the LZ and it plans
the deorbit itself — it *"selects between course correction, plane change, or standard deorbit burn depending
on orbit periapsis and atmospheric density"*.

| Setting | cfg / field | **[cfg]** | Target |
|---|---|---|---|
| the site | `MechJebModuleLandingGuidance` — "Enter target coordinates" / the site dropdown (reads **RSS site configs**) ⟂ `MechJebModuleTargetController` | — | the designated splashdown lat/lon |
| `DeployChutes` | `DeployChutes = True` | **True** | keep — Dragon lands under chutes |
| `LimitChutesStage` | `{ 0 / 0 }` | 0 | ⚠ set to the **actual chute stage** on the owner's craft |
| `DeployGears` / `LimitGearsStage` | `True` / `{ 0 / 0 }` | True / 0 | ⚠ **Dragon has no landing gear (water splash)** — set `DeployGears = False` or confirm it is inert |
| `RCSAdjustment` | `True` | **True** | RCS trims the descent |
| `TouchdownSpeed` | `{ 0.5 / 0.5 }` | **0.5** | a landing-**detect**/settle threshold, **not** a powered target — Dragon splashes ballistic under mains. Leave |
| Auto-warp | GUI toggle | on | |

⚠ **(b) is the site-accurate option and the one the owner's brief asks for ("Landing Guidance targeted at the
LZ lat/lon"). Its risk is that it will happily plan a *plane change* or a *course correction* it thinks it
needs** — Δv Dragon does not have, on a vehicle with no landing engine. **Cap it with the propellant ledger,
and treat (a) as the fallback if its plan exceeds the reserve.**

## 7.2 Entry (§B9 P8)

| Need | Module | Setting |
|---|---|---|
| heat-shield-forward attitude hold | **`SmartASS`** | mode **`SURFACE_RETROGRADE`**; `force_pitch`/`force_yaw` on |
| any bank / lifting entry | `SmartASS` `SURFACE` with explicit `surface_heading` / `surface_pitch` / `surface_roll`, + `force_roll` | ⚠ **OPEN (§B9 P8): lifting-entry bank vs pure ballistic.** Dragon is low-L/D; §B9's own reading is "mostly attitude-hold, not active guidance" |
| the descent prediction | `MechJebModuleLandingPredictions` | display + the timeline the screens read |
| attitude gains | `AttitudeController.BetterController` | same PID as ascent; `RollControlRange 5`, `MaxStoppingTime 2`, `Soften 0.5` |

**Sequenced vessel actions, NOT MechJeb burns** (⚠ §B9 P7 says this explicitly): **trunk jettison**,
**nose-cone close + lock**, claw separation. Our seam for these is `Actuator` / `FlightCommands`
(`_AutopilotStub.cs:79-84`), not any MechJeb module.

## 7.3 Chutes (§B9 P9)

| Gate | Value | Source |
|---|---|---|
| drogues | **5486.0 m** | `pure/MissionPhase.cs:56  DrogueAltitude` — 18 000 ft |
| mains | **1830.0 m** | `pure/MissionPhase.cs:57  MainAltitude` — 6 000 ft |
| count | 2 drogues, then **4 mains**; land under **≥ 3** | §8 **[DOC]** |
| after splash | **CUT MAINS** | §4 panel |

⚠ **Do not reconcile these with the Manual-Chute page's "(TBC)" altitudes** — §14.1 records that the two are
**intentionally different** (the page carries SpaceX's own placeholder text verbatim; the FSM constants are
the real trigger numbers). Neither is to be "fixed" to match the other.

⚠ **No powered landing.** The Landing Autopilot here is for **prediction + chute triggering**, never a
landing burn. `TouchdownSpeed` is a detection threshold.

## 7.4 Splashdown (§B9 P10)

MechJeb is done; the conductor releases control. Nothing to tune.

⚠ **The seven splashdown sites** (Pensacola · Tampa · Tallahassee · Panama City · Cape Canaveral · Daytona ·
Jacksonville — all off Florida, **22–175 nautical miles offshore [DOC]**) are named in six places in the repo
with **no coordinates anywhere** (`CoverPage.cs:640`, `BUILD_PLAN.md:114/500/581/770`, `REGISTER.md:183`).
**NASA does not publish per-site coordinates**, so a targeted deorbit needs either an owner-supplied set or an
explicitly-marked reconstruction (§1.4 / §14.4(e)). **Do not invent them.** See §10.

---

# 8. Cross-phase settings — set once, they apply everywhere

| Module | Setting | **[cfg]** | Target | Applies to |
|---|---|---|---|---|
| `MechJebModuleGuidanceController` | **`UllageLeadTime`** `{ 20 / 20 }` | **20 s** | keep ~20; tune to the real Draco settle | **every** Node-Executor burn, and every relight after a coast |
| | `ShouldDrawTrajectory` | True | display only | — |
| `MechJebModuleAttitudeController` → `BetterController` | `PosKp 2.03` · `PosTi 1.97` · `VelKp 7.98` · `RollControlRange 5` · `MaxStoppingTime 2` · `MinFlipTime 120` · `Soften 0.5` · `SmoothTorque 0.1` | as listed | leave | ascent pointing, node pointing, entry attitude — **one PID for the whole mission** |
| `MechJebModuleRCSController` | `Tf 1` · `Kp 0.125` · `Ki 0.07` · `Kd 0.53` | as listed | leave | every RCS attitude hold |
| `MechJebModuleRCSBalancer` | `smartTranslation False`, `overdrive 1`, `overdriveScale 0.9` | as listed | ⚠ `smartTranslation → True` if prox-ops cross-coupling appears | docking, departure |
| `MechJebModuleThrustController` | `LimiterMinThrottle True` · `MinThrottle 0` · `DifferentialThrottle False` | as listed | ⚠ the ascent bang-bang conflict lives here (1B.iii/1B.vi); on-orbit leave default | ascent mainly |
| `MechJebModuleWarpHelper` | `phaseAngle { 0 / 0 }` | 0 | set a lead per use | phasing coast (1C), pre-deorbit wait (6.2) |
| `MechJebModuleSmartASS` | cfg **EMPTY** | live-driven only | see the phase map below | everywhere |
| `MechJebModuleFlightRecorder` | nothing to set | — | **read it** — it is the instrument the whole §B5 tune depends on | ascent tune, rendezvous residuals |

**SmartASS phase map** (set `autopilot_mode`; no persistence, pure API per phase):

| Phase | Mode |
|---|---|
| coast / docked | `OFF` or `KILL_ROT` |
| pre-burn pointing when *not* using the Node Executor | `NODE` |
| docking (4) | `TARGET` → **`target_plus`** / **`parallel_plus`** (docking-axis aligned) |
| departure (5) | `TARGET` → `target_minus`, then `ORBIT` retrograde |
| entry (7) | **`SURFACE_RETROGRADE`** (heat-shield forward), + `force_roll` for any bank |
| booster coast/entry (2) | `SURFACE_RETROGRADE`; `SURFACE` with explicit heading/pitch for steered entry |

Full mode set: `OFF` · `KILL_ROT` · `NODE` · `ORBIT {prograde, retrograde, normal±, radial±}` ·
`SURFACE {surface_prograde/retrograde, horizontal±, vertical_plus, surface(heading/pitch/roll)}` ·
`TARGET {target_plus/minus, relative±, parallel_plus/minus}`. Controls: `force_pitch` / `force_yaw` /
`force_roll` (bool), `surface_heading` / `surface_pitch` (0 = horizon, 90 = up) / `surface_roll`.

---

# 9. The tune order — what to converge, in what sequence

**§B5's methodology, unchanged: start from the RO default, change ONE parameter, fly, compare against real
data, lock it.** The order below is by *leverage*: each step's residual error is what the next step is
tuning against, so doing them out of order means re-doing them.

| # | Knob | Validate against | Instrument |
|---|---|---|---|
| 1 | **`PitchRate`** (±0.1 °/s) | flat AoA through max-Q; max-Q **30–35 kPa at ~12 km, T+1:02** **[DOC]** | Flight Recorder Q/AoA/Pitch |
| 2 | `PitchStartVelocity` (only if 1 saturates) | same | same |
| 3 | `LimitQa` | limiter barely active on a good profile | same |
| 4 | **the throttle question** (1B.vi #1) | peak axial accel ≈ **4 g [EST]**; MECO **T+2:36, ~80 km, ~Mach 10 [DOC]** | `tools/tuning_db.py` throttle + g segments |
| 5 | `OptimizeStageInternal`, staging flags | SECO-1 **T+8:47**; insertion **190–210 km × 51.63° [DOC]** | `tools/assess_flight.py` ascent section |
| 6 | the **phasing ladder** (apsides / SMA) | **dock ~19 h after launch [DOC]**; Pe never < **150 km**; return propellant intact | `assess_flight.py` rendezvous/phasing |
| 7 | `AlignedToleranceDegrees`, `LeadTime` | burn residuals small enough that a re-plan is a trim, not a redo | Flight Recorder |
| 8 | the **`InterceptDistance` ladder** | 4 km → 1 km → 220 m → 20 m **[DOC]** | closest-approach readout |
| 9 | the **`speedLimit` ladder** | contact **≈0.1 m/s**; **< 0.2 m/s inside 5 m [DOC]** | docking rate |
| 10 | the **deorbit Pe / FPA** | entry interface **122 km at 7.8 km/s [DOC]**; FPA **−1.4° to −1.6° [EST]**; peak decel **4–4.5 g [EST]** | `assess_flight.py` deorbit/entry |
| 11 | chute gates | 5486 m / 1830 m; ≥3 mains | phase FSM |
| 12 | *(extension)* booster: entry-burn altitude/target-velocity, landing-burn lead, gains, touchdown margin | on-deck test **±25 m along / ±12.5 m cross** | `build/assess_flight.py` booster section |

**The four `[EST]` numbers T22 exists to pin** (§B11): **peak ascent g · entry FPA · deorbit Δv · drogue
altitude**. Everything else in the target list is `[DOC]`.

**⚠ Two failure modes are already on record** (`docs/FLIGHT_144114_SCREEN_AUDIT.md`, the deleted autopilot's
flight — historical, "do not open a task from it", but they are what the tune must not reproduce):
**(1)** rendezvous/phasing **self-deorbited**, drained the return propellant, and never docked;
**(2)** the booster went **ballistic — engines never lit — LOST, on ullage**.

---

# 10. Open ⚠ items — consolidated

## 10.1 Empirical (resolved by flying, not by deciding) — the §B5/T22 pass

| # | Item | Phase |
|---|---|---|
| E1 | `PitchRate` / `PitchStartVelocity` convergence | 1 |
| E2 | whether `LimiterMinThrottle = True` with `MinThrottle = 0` costs anything in practice | 1 |
| E3 | hot vs cold staging behaviour on this stack | 1 |
| E4 | `AttachAltFlag` at 210 km vs free-optimise | 1 |
| E5 | whether the three fairing keys can mis-trigger on a fairingless Dragon | 1 |
| E6 | the phasing-orbit ladder that lands docking at ~19 h | 1C |
| E7 | `OperationGeneric` optimised vs `Coplanar` transfer | 3 |
| E8 | `AlignedToleranceDegrees` / `LeadTime` for clean burns | 3 |
| E9 | whether `RCSBalancer.smartTranslation` is needed | 4 |
| E10 | the entry-corridor Pe that yields FPA −1.4° to −1.6° | 7 |
| E11 | the four `[EST]` numbers (§B11) | all |

## 10.2 OWNER decisions (a build chat decides none of these — C1.12)

| # | Decision | Why it is the owner's |
|---|---|---|
| **O1** | **Fold booster recovery into Part B at all?** If yes: a new §B section + register task(s), and `docs/TELEMETRY_REGISTRY.md:9` ("Part B does not re-introduce it") needs updating. | a scope change to a frozen plan |
| **O2** | **How is the booster flown** — the five-phase method implemented in our conductor on MechJeb's attitude/prediction modules · MechJeb's landing autopilot as-is with its limits · or **BoosterGuidance** (GPL-3.0) as a second mod dependency? §B3's packaging decision covers **MechJeb only**. | packaging + dependency |
| **O3** | **Vessel focus / second conductor.** §B12 assumes one vessel. Recovery historically called `ForceSetActiveVessel(booster)` — which takes the crew's view off the Dragon mid-ascent. Does the conductor follow the capsule, the booster, or run two? | conductor architecture |
| **O4** | **The craft dump.** The 3-engine landing config is per-craft, C7 forbids reading the KSP install, and there are no `.craft` files in the repo. **The engine table in 2.4 cannot be filled without it.** | a build input that is not in the repo (C7 → STOP and flag) |
| **O5** | **RTLS or ASDS**, and the aim point. The repo's only surviving coordinates are `PAD (28.6084, −80.6043)` and `BARGE (32.787551, −76.644507)`. | mission definition |
| **O6** | **Docking: autopilot or hand-off** to the Manual ISS Docking screen (§B9 P4, open since the plan was written). | it changes what the crew does |
| **O7** | **The seven splashdown sites have no coordinates anywhere**, and NASA does not publish them. Owner-supplied set, an explicitly-marked reconstruction (§14.4(e) step 2), or a single nominal site? | §1.4 source-of-truth |
| **O8** | **Entry: lifting bank or pure ballistic** (§B9 P8). | flight-model realism |
| **O9** | **Ascent throttle: model F9's real throttle-down or accept PVG bang-bang** (§B8, open). Empirical *evidence*, but a realism-vs-optimality *call*. | a stated realism trade |

## 10.3 Logged findings — NOT acted on (C1.1)

1. **`docs/TELEMETRY_REGISTRY.md:9`** states Part B does **not** re-introduce booster recovery — contradicted
   by this task's directive. Needs updating **if and when** O1 is decided.
2. **`plugin/src/pure/VehicleParts.cs:32-46`** encodes the engine-mode-cycling path the owner's directive
   forbids (`EngineSwitchModule`, `EngineSwitchAction "next engine mode"`, `OctawebModeFor`). The constants
   themselves are a correct description of the part; only the *flight-software use* is forbidden.
3. **§B10.1's `NodeExecutor.tolerance`** does not exist in current MechJeb source (§0.3). §B9's
   "Tolerance 0.1 → 0.05 m/s" tune has nothing to set.
4. **§B10.2's `OperationTransfer` field names** (`intercept_only`, `simple_transfer`, `period_offset`) are
   kRPC names; the current class is `OperationGeneric` with different fields (§0.3).
5. **`pure/MissionPhase.cs:56-57`** — `DrogueAltitude` / `MainAltitude` are declared but **never read by
   `Classify()`** (it branches on the `DroguesOut`/`MainsOut` booleans). Live dead-constant.
6. **`KerData.HasRecoveryReserve(stages, reserveMps)`** has **no default reserve value** defined anywhere in
   the repo. Phase 2 needs that number.
7. **`docs/INDEX.md` does not list this doc** — deliberately not updated (C1.11: a task writes ONLY its
   declared outputs).
8. **`docs/FLIGHT_SYSTEMS.md` is still a dangling reference** (`pure/MissionPhase.cs:54`,
   `build/audit_comments.py`). T15 owns creating it; this doc may be part of what it should point at.

---

# 11. Sources

**Repo (authoritative, C7.1):** `docs/BUILD_PLAN.md` §8 (flight facts) · §B5 (methodology) · §B7 (cfg
mechanics) · §B8 (ascent) · §B9 (mission sequence) · §B10 (on-orbit modules) · §B11 (flight-data targets) ·
§14.1 / §14.4 (decisions) · `docs/reference/mechjeb_settings_type_Crew-Dragon.cfg` (the tuned profile — every
`[cfg]` value in this doc is quoted from it) · `plugin/src/pure/VehicleParts.cs` · `plugin/src/pure/MissionPhase.cs` ·
`plugin/build/assess_flight.py` · `plugin/tools/assess_flight.py` · `plugin/tools/tuning_db.py` ·
`plugin/src/RangeExtender.cs` · `plugin/src/_AutopilotStub.cs` · `docs/TELEMETRY_REGISTRY.md` ·
`docs/FLIGHT_144114_SCREEN_AUDIT.md` (historical).

**External (checked 2026-09-03):**

- RP-1 wiki, *Troubleshooting MechJeb PVG* — https://github.com/KSP-RO/RP-1/wiki/TroubleshootingMechJebPVG
- MechJeb2 source (`dev`) — `MechJebModuleNodeExecutor.cs`, `MechJebModuleDockingAutopilot.cs`,
  `MechJebModuleLandingAutopilot.cs`, `MechJebModuleLandingGuidance.cs`, `Maneuver/` —
  https://github.com/MuMech/MechJeb2
- BoosterGuidance (the established RSS/RO Falcon-9 recovery method; GPL-3.0) —
  https://github.com/oyster-catcher/BoosterGuidance and the maintained fork
  https://github.com/linuxgurugamer/BoosterGuidance
- RealFuels readme (ignitions, ullage, `ModuleEngineConfigs`, `ModuleEnginesRF`, throttle limits, residuals) —
  https://github.com/KSP-RO/RealFuels/blob/master/RealFuels/Readme_RF.txt
- KSP `ModuleEngines` API reference — https://anatid.github.io/XML-Documentation-for-the-KSP-API/class_module_engines.html
- Crew-2 mission timeline (the booster events quoted in 2.1) — https://spaceflightnow.com/2021/04/22/crew-2-mission-timeline/
  and https://everydayastronaut.com/crew-2/
- Falcon 9 recovery burn overview — https://www.thespacetechie.com/re-entry-burns-of-falcon-9/
- Landing Zones 1 and 2 (28.4858 N, 80.5444 W) — https://en.wikipedia.org/wiki/Landing_Zones_1_and_2
- NASA, *SpaceX Crew Rescue and Recovery* (the seven splashdown sites) —
  https://www.nasa.gov/humans-in-space/nasas-spacex-crew-rescue-and-recovery/
- NASASpaceflight, *Examining Crew Dragon's launch abort modes and splashdown locations* —
  https://www.nasaspaceflight.com/2020/05/examining-crew-dragons-launch-abort-modes-and-splashdown-locations/
