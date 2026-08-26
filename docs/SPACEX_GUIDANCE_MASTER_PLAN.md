# SpaceX-equivalent guidance system — master plan

**Goal (user, 2026-08-25):** build *"the most sophisticated auto-pilot system Kerbal Space Program has
ever seen"* — a **true SpaceX flight-software stack** for the Tundra Exploration (SpaceX) vehicles in this
RSS/RO install. Not one mission's script: a **mission-agnostic** guidance system that flies *any* Falcon /
Dragon mission from the same code, with the mission expressed as **data**.

**The validation principle (user):** *"If we use the exact same guidance system built on the same
techniques then the timelines for all Falcon missions should line up."* A genuinely physics-based,
measured guidance flying a real vehicle on a real trajectory will make the real event times **emerge** —
MECO, entry burn, landing burn, SECO, docking — never hard-coded. **A timeline that doesn't match is the
tell that the guidance (or the modelled vehicle) deviates.** So the timeline is our acceptance test.

**Discipline (user, repeatedly):** *always choose the most realistic option*; **confirm every fact against
primary sources — the actual GameData configs, the flight recording, and KSP.log — never the `.md` docs**
(they are records and hypotheses, and one of them already sent this investigation down a wrong path). Read
things in full. Instrument everything. No Python sims. Between flights, always suggest the proper next step.

---

## 0. What is VERIFIED (ground truth, not `.md`)

Each of these was read from the live source named, this session:

- **The vehicle is a real Falcon 9.** `ModuleManager.ConfigCache` lines 1,134,891–1,135,800: `TE_19_F9_S1_
  Engine` carries three `ModuleEngineConfigs` (AllEngines / ThreeLanding / CenterOnly) with real Merlin
  CONFIGs — **Merlin1D 6681.6 kN / Merlin1D+ 7425 / Merlin1D++ 8227 kN**, **RP-1 + LqdOxygen**, **ullage =
  True**, **ignitions = 4**, ISP 311 vac / 282 SL, TEATEB ignitor. (The `ModuleEnginesRF` base shell reads
  2560 kN / LiquidFuel — that is the pre-config layer RealFuels overlays; do NOT read the base shell alone.)
  Applied by `RO_TE_Falcon_9.cfg:68` (MM log line 14707). **So the timeline-match goal is achievable.**
- **Environment** (`RO_RSS_ENVIRONMENT.md` claims, spot-checked against Earth-Kopernicus): Earth R 6371 km,
  μ 3.986e14, atmosphere 140 km, LEO ~7.8 km/s. **MECO velocity sets booster downrange.** FAR voxel aero
  (measure drag, don't model it; grid fins are `FARControllableSurface`, authority ∝ q). RealHeat shock
  heating (entry burn cuts peak heat + downrange). **No reaction wheels** (RO strips them) — attitude is
  **RCS + engine gimbal + grid fins only**. TestFlight gives engines reliability < 1.
- **The current flight (`flight_0825_110240`)**, read from the CSV:
  - Entry burn: mass **53.4 → 28.3 t (25 t spent)**, velocity **2306 → 507 m/s**. A gross over-burn.
  - Landing burn: commanded 1358 m, engines "lit" (`b_enginesLit`=1) at ~776 m, **but mass fell only
    0.06 t and speed only 239 → 233 m/s** — the engines lit and made **≈no thrust**; the stage free-fell
    onto the deck at ~230 m/s, **271 m short of centre**.

> ⚠ Open item to CONFIRM in-flight: the exact landing no-thrust cause (unsettled ullage at ~0 g terminal
> vs a TestFlight ignition failure) could NOT be confirmed — KSP.log had been overwritten by a later
> session. Phase-1 step 0 is to instrument it so the next flight answers it definitively. Do not "fix" it
> from theory first.

---

## 1. Architecture — mission as data, guidance as library

The stack already follows the pure/glue split and a `MissionProfile` exists on the Dragon side. Extend the
**same** idea to the whole vehicle:

- **`MissionProfile` (data)** — launch site + target plane/altitude, recovery mode (RTLS vs ASDS) + landing
  site, splashdown zone, payload. The booster's droneship coordinate, the ascent inclination, the reserve
  policy — all read from the active profile, never hard-coded in the guidance. (Today `BoosterRecovery`
  hard-codes the droneship lat/lon; that moves into the profile.)
- **Guidance controllers (library)** — each phase flies the real technique from **measured** vehicle state:
  ascent PEG/UPFG, booster entry+hoverslam, grid-fin aero steering, Dragon named-burn rendezvous, bank-angle
  entry. Nothing branches on "which mission"; it branches on physics.
- **RTLS vs ASDS is chosen from energy vs target**, not a per-mission flag baked into code, so the same
  booster guidance flies a light RTLS pad-return and a heavy downrange droneship landing.

---

## 2. PHASE 1 — the booster (droneship recovery, crew missions) ← START HERE

### 2.1 The real sequence (confirmed: BOOSTER_GUIDANCE_DESIGN.md + web primary sources)

`MECO → sep → flip (engines-first, grid fins deploy; NO boostback for ASDS) → entry burn (3 engines, ~55 km,
bleeds ~2300 → ~1300 m/s to survive reentry heating/loads and set the descent) → aero descent (grid fins fly
to the deck, terminal ~300 m/s) → landing burn (hoverslam, 3 engines brake → 1 centre engine touchdown, to
~2 m/s) → land.` Recovery spends ~6–10 % of total fuel. It is *hit-what-you-aim*, not hover-and-search.
Crew-4 clock: entry burn T+7:27, landing burn T+9:03, land T+9:30 — these are the **emergent** times to match.

### 2.2 Current state (read in full: `pure/Landing.cs` 1554 ln, `BoosterRecovery.cs` 2096 ln, `Hoverslam.cs`)

The architecture is already sophisticated and largely correct: the four-phase sequence, a **drag-aware
numerical hoverslam solver** (MechJeb's method + aero + spool + dead-time), a **two-phase 3→1 ignition
sizing**, grid-fin/gimbal aero steering on a **predicted impact point** with lead compensation, single-axis
roll-free slews, ullage-until-lit relight logic, octaweb step-and-verify with finite-budget ignition retry.
This is not a rewrite — it is three targeted, physics-grounded fixes plus instrumentation.

### 2.3 The fixes (each measured + mission-agnostic; one change per test flight)

**Step 0 — INSTRUMENT the landing burn (before touching it).** Add to the recorder, for the booster:
per-engine `finalThrust`, the octaweb mode read-back, the ullage/RCS state and the **cold-gas propellant
remaining**, the net axial acceleration (the settling state — ~0 at terminal = unsettled), ignition
attempts, and any TestFlight failure. This is the one that tells us *why* the engines lit but made no
thrust (ullage starvation vs failed relight) instead of guessing. Fly once, read it back.

**Step 1 — Entry burn cut on PHYSICS, not tuned constants.** Today it cuts on `EntryBurnCutVs = −300 m/s`
(an RTLS number that over-bleeds a droneship) OR a hand-tuned `EntryBurnReserveFrac = 0.20`. Replace with
the tighter of two computed conditions, valid for any vehicle:
- a **reentry-velocity / heating ceiling** — bleed only to a survivable speed (~1300 m/s emerges; watch
  `b_maxSkinK` against the RealHeat limit), leaving the downrange the barge sits on; and
- a **landing reserve the hoverslam solver computes** — reserve exactly the propellant `HoverslamSolver`
  predicts the landing burn needs from the predicted terminal state, + margin. Self-sizes to the vehicle.
This stops the 25 t over-burn, keeps the downrange, and guarantees a fed landing burn — on any Falcon.

**Step 2 — Landing ignition (driven by Step 0's data).** The engines lit but starved. Likely fixes, chosen
after the data: guarantee the ullage settle actually settles (verify cold-gas is not depleted; if it is,
budget it or light where drag still decelerates the stage so propellant is settled), raise the
lit-threshold above a starving trickle, and keep the finite-ignition retry honest. The hoverslam solver
already budgets the dead-time; the missing piece is making the *first* light produce real thrust.

**Step 3 — Aim from the profile + null the last ~271 m.** Read the droneship target from `MissionProfile`,
not the hard-coded `DroneshipEarthLatDeg/LonDeg`. The grid-fin/gimbal steering already cut 5 km → 0.27 km;
tighten the impact-point null (the residual is partly staging energy — couple it to the S2 MECO target).

**Step 4 — Timeline instrumentation.** Record the event times (MECO / sep / flip / entry-burn on+off /
landing-burn on / touchdown) and compare to the Crew-4 clock. `Crew2TimelineTest` becomes the headless
guard; a recorded flight is the in-game guard.

### 2.4 Booster acceptance

One recorded ASDS flight where the booster lands **on the deck, upright, intact**, and the **entry-burn and
landing-burn times fall within a sane band of T+7:27 / T+9:03** as an *output* of the physics.

---

## 3. PHASE 2 — the upper stage (ascent to orbit, crew missions)

Real: closed-loop **explicit guidance (PEG/UPFG class)** — the S2 MVac flies to a target insertion state
(velocity, flight-path angle, altitude) rather than a fixed pitch program; **MECO velocity/FPA is what sets
the booster's downrange**, so the two are coupled. Current: `pure/Upfg.cs`, `pure/Ascent.cs`,
`pure/LaunchAzimuth.cs`, `pure/PlaneWindow.cs`, `AutoPilot` exist and fly. Phase-2 work: **audit these in
full against the real technique and the RO Merlin/MVac numbers**, tune the loft so MECO emerges at ~T+2:36 /
~67 km / ~2.3 km/s and SECO-1 at ~T+8:47 (the Crew-4 clock), and confirm the ascent is at ~zero angle of
attack (FAR loads). The ascent already reaches orbit; this makes it match the real profile and feeds the
booster the right staging energy (which fixes the residual downrange in Phase-1 Step 3).

---

## 4. PHASE 3 — the rest (Dragon: rendezvous, dock, return)

Largely built and (rendezvous) just rebuilt to the real named-burn + CW terminal profile. Refinements once
the launch/recovery is solid: the return's **4 departure burns + phasing burn + ~12-min deorbit burn** (real
sequence, currently one node burn), and the bank-angle lifting entry validation. Same discipline throughout.

---

## 5. Cross-cutting: instrumentation & validation

- **Instrument everything, every controller, the same pass it is built** — the booster landing failure was
  diagnosable only because the recorder had `b_*` columns; the gaps (ullage/cold-gas/per-engine thrust) are
  Step 0. No flight flies uninstrumented.
- **Timelines as the acceptance test** — a per-flight comparison of emergent event times to the real Falcon
  clock, headless where possible (`Crew2TimelineTest`) and in the recording.
- **No Python sims** — validate with headless C# tests + the recorded corpus + `assess_flight.py`.
- **Confirm against configs/data/log, never the `.md`.**

---

## 6. Build order (each step one tested change, then one flight)

1. **P1.0** Instrument the booster landing (ullage/cold-gas/per-engine thrust/ignition/TestFlight) → fly, read.
2. **P1.1** Entry-burn physics cut (reentry-velocity + solver-computed reserve) → fly.
3. **P1.2** Landing-ignition fix from P1.0 data → fly.
4. **P1.3** Aim from profile + impact-null tightening → fly. **Booster acceptance.**
5. **P1.4** Event-time instrumentation + timeline check.
6. **P2** Ascent PEG/UPFG audit + loft tune to the Crew-4 clock (couples to P1 downrange).
7. **P3** Return multi-burn + entry validation.

Nothing here is committed or installed without the user; each flight is one clean, fully-recorded run.
