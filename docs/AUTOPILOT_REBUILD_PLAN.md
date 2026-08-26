# CLAUDE — Crew Dragon autopilot: ground-up rebuild plan

> The autopilot we are building is named **CLAUDE** (user: "if you succeed we will call the autopilot
> CLAUDE"). It is the SpaceX/NASA autonomous flight software for the RSS/RO **Tundra Falcon 9 + Crew
> Dragon** stack, flown crew-in-the-loop, launch → splashdown, on Earth. This document is the single,
> self-contained plan for rebuilding it correctly from the ground up, with every starting value derived
> from the research corpus — not inherited from the current code. It is written to be **picked up in any
> session** and put you straight into the right mode.

---

## 0. RESUME HERE — the mode and frame of mind (read first, every session)

**You are rebuilding real spacecraft flight software.** Crew lives are the first priority; the vehicle
must always be able to bring them home. Fidelity to the *real* SpaceX/NASA technique is non-negotiable —
never a convenient analogue, never a "safe/simplified" alternative. When a choice arises, the answer is
**what the real Crew Dragon does** (the reference flight is **Crew-2**, on Earth in RSS/RO).

**Standing rules (from memory — do not drop these under the ambition):**
- **Full fidelity, no deviation** — copy the real flight exactly; never offer or ask "full or safe". Make
  hard real techniques *work*. [[crew2-full-fidelity-no-deviation]]
- **Confirm against GROUND TRUTH, never the `.md` docs** — the live `ModuleManager.ConfigCache`, the flight
  recording CSV, and `KSP.log`. The docs are records/hypotheses; one already sent an investigation the
  wrong way. Read files **in full**.
- **Pure-first, headless-tested** — pure math/decision/nav in `plugin/src/pure/`, tested by
  `python build.py test` before any KSP glue uses it. The pure layer **is** the model; the tests are the
  certification. [[no-python-simulations]] (NO Python physics sims — banned; validate with headless C#
  tests + the recorded flight corpus + `assess_flight.py`.)
- **Instrument EVERYTHING the same pass you build it** — every controller's decisions, gates, targets,
  triggers, planned+delivered Δv into `FlightRecorder.cs`; prefer more columns. Never fly uninstrumented.
  Between flights ALWAYS suggest the proper next step. [[instrument-everything]]
- **One root-cause pass then ONE fix; one change per test flight.** No "wait/actually" thrash. Terse chat.
  Take the user's clues literally. [[work-efficiency-no-second-guessing]] [[falcon-flight-data-first]]
- **Port, don't invent** — grep a function's identifiers before calling; copy the reason; never a
  "defensive" global; MechJeb source is at `Desktop/mechjeb_src` (port from it, don't decompile).
  [[falcon-port-dont-invent]] [[mechjeb-source-reference]]
- **Detect by capability, not part name.** [[falcon-detect-by-capability]]
- **Do NOT commit or install** without the user. Build: `cd plugin && python build.py test|install`
  (install needs KSP + CKAN closed and a full restart). `.cfg` changes need a restart only.

**The environment (RSS/RO, verified):** Earth R 6371 km, μ 3.986e14, atmosphere ~140 km, LEO ~7.8 km/s.
FAR voxel aero (**measure** drag, don't model it; grid fins are `FARControllableSurface`, authority ∝ q).
RealHeat shock heating. **No reaction wheels** (RO strips them) — attitude is **RCS + engine gimbal + grid
fins only**; the control law must turn RCS *on* when it is the sole authority. TestFlight gives engines
reliability < 1 (ullage + finite ignitions matter). The vehicle is a **real Falcon 9** (RO Merlin configs
AllEngines/ThreeLanding/CenterOnly, RP-1/LOX, ullage=true, ignitions=4) — so a real-timeline match is
achievable. Dragon maneuvers on **16 Draco (MMH+NTO)**; SuperDraco is abort-only; no ISS refuel.

**The acceptance principle (user):** *if the guidance is genuinely physics-based and flies the real
vehicle on the real trajectory, the real event times EMERGE* — MECO, entry burn, SECO, docking, splashdown
are never hard-coded. **A timeline that doesn't match the real callout MET is the tell that the guidance
(or the modelled vehicle) deviates.** The telemetry database (`data/crew_missions.json`) is the timeline
acceptance test.

---

## 1. WHAT "DONE" IS (the definition of the finished autopilot)

From the how-to-build guide's completion checklist (`TRUE_AUTOPILOT_ARCHITECTURE.md` §13) — all eight must
hold:

1. Every phase flown by a **closed-loop guidance law re-solved from live state** (never a fixed script).
2. **Control** bounds every commanded rate by what it can arrest (`ω_max=√(2αθ)`) — no per-phase gain tuning.
3. A **mode manager** sequences on **measured state + crew GO**, never a bare clock; owns HOLD/ABORT/takeover.
4. **FDIR that ACTS**: debounced monitors → recovery ladder → **guaranteed abort-to-home**.
5. **Onboard re-planning** on a detected failure — a normal event, not an exception.
6. **In-flight self-calibration (RLS)** estimates thrust/mass/drag/torque/lift live — **no hand-tuned
   constant** is trusted across flights.
7. Human retains **HOLD/ABORT/manual-takeover** at the real gates (the user IS the crew).
8. **Everything instrumented**, pure math **headless-tested**, glue **validated on the flight corpus**.

**Mission-agnostic — and that is what makes it a TRUE autopilot, not a script.** The autopilot flies *any*
real Crew Dragon mission; a specific mission is nothing but a **Mission Profile (data)**, **selected by the
VAB vehicle name** (§3A). The guidance/control never branches on "which mission" — only on physics. Crew-2
is the reference we validate against. The 20-mission database (`data/crew_missions.json`) is the profile
source.

**Acceptance:** one recorded RSS Crew-2 mission, launch → splashdown, where **the vehicle flew every metre
itself**, **the user did only what a real astronaut does** (the checks, polls, GOs, with HOLD/ABORT
available), and **the emergent timeline matches the real callout MET** within a sane band.

---

## 2. GROUND-TRUTH INDEX (what each layer is built FROM)

Every design decision and every constant traces to one of these (all on disk):

| Layer | Source of truth |
|---|---|
| Architecture / method | `TRUE_AUTOPILOT_ARCHITECTURE.md` (§0–13) |
| Ascent technique + math + numbers | `LAUNCH_AND_ASCENT_RESEARCH.md` + `ASCENT_GUIDANCE_UPFG.md` |
| Ascent pitch/throttle/g **profile** | `data/dm1_ascent_template.json` (real DM-1 telemetry) |
| Booster recovery | `PHASE_2_BOOSTER_RECOVERY_RESEARCH.md` + `BOOSTER_GUIDANCE_DESIGN.md` |
| Rendezvous (named burns + magnitudes) | `PHASE_3_RENDEZVOUS_RESEARCH.md` + `data/crew_missions.json` (real MET + AI burn) |
| Docking (L-approach, capture) | `PHASE_4_DOCKING_RESEARCH.md` + `data/crew_missions.json` (WP times) |
| Undock / departure | `PHASE_5_UNDOCKING_DEPARTURE_RESEARCH.md` + DB return sequence |
| Deorbit / entry / splashdown | `PHASE_6_DEORBIT_ENTRY_SPLASHDOWN_RESEARCH.md` + DB return spacing |
| Full-mission callout MET (all phases) | `data/crew_missions.json` + `docs/CREW_MISSION_TELEMETRY.md` |
| Autonomy (FDIR + self-cal) | `LAYER3_AUTONOMY_PLAN.md` |
| Crew-in-the-loop gates | `snoopy-orbiting-hennessy.md` (plan) + DB countdown MET |
| Vehicle numbers (Merlin/MVac/Draco) | **`ModuleManager.ConfigCache`** — read live, do NOT trust the .md spec figures |
| Direct-control actuation map (parts/modules/events/actions) | **`data/craftdump.csv`** + `docs/CRAFT_DUMP_VEHICLE_MAP.md` — the real vehicle's capabilities; control by capability |
| RSS/RO mechanics | `RO_RSS_ENVIRONMENT.md`, `RO_MODS_MECHANICS.md`, `RO_TESTFLIGHT_MECHANICS.md` |

---

## 3. ARCHITECTURE (the shape everything is built into)

The five-part pipeline, ticked every frame, plus the safety spine (`TRUE_AUTOPILOT_ARCHITECTURE.md` §1):

```
  MODE MANAGER  (master FSM + phase FSMs; owns crew GATES, HOLD/ABORT/takeover)
        │ engages phase controller               ▲ failsafe
        ▼                                         │
  GUIDANCE (plan, closed-loop, re-solve) ◀──────▶ FDIR (detect→isolate→recover ladder→abort-to-home)
        │ command                                 ▲ residuals
        ▼                                         │
  CONTROL (attitude quaternion-PD, throttle, RCS; fault-contained)
        │ actuate (gimbal / RCS / grid fins / chutes — by CAPABILITY)
        ▼                                         │
  NAVIGATION (KSP ground truth + DERIVED: q, impact point, β, authority)
        │
  RECORDER (every decision/target/residual/verdict, every tick)  +  SELF-CAL (RLS estimators → LearnedParams)
```

- **Multi-rate:** control every physics frame; guidance/re-plan less often; mode manager slow.
- **Pure/glue split is the architecture.** Pure = the model (headless-tested). Glue = KSP wiring
  (`OnFlyByWire` per vessel; SAS off; actuate part modules by capability; −90° nose-frame correction;
  drop out of time-warp before any burn; integrate real `dt`).
- **Fault containment:** wrap every controller so an exception detaches it and is logged; re-validate all
  state on scene/vessel change. A fault never crashes the flight.
- **Two vessels live at once** (booster + upper stage after sep) — a controller attached to each.

---

## 3A. MISSION PROFILE — fly ANY mission, selected by the VAB vehicle NAME

**This is the mechanism behind "true autopilot, not a one-track script."** The guidance, control and mode
manager (L2–L5) are **invariant** — they compute commands from live physics to reach a set of **targets
and constraints**. A specific mission is only **DATA**: which targets, which constraints, which vehicle
configuration. Change the data and the same code flies a different mission; the flying logic never
changes. A script would branch on "which mission"; CLAUDE branches only on physics.

**Selection by craft name (user 2026-08-26):** build **one vehicle per mission in the VAB, named exactly
as the mission** ("Crew-2", "Fram2", "Polaris Dawn", …). On the pad the autopilot reads `vessel.vesselName`,
**matches it to a mission in `data/crew_missions.json`**, and loads that profile. No match → fall back to a
generic ISS-crew profile **and raise a NO-GO note** (never fly the wrong plan silently).

**The Mission Profile (`pure/MissionProfile.cs`) = autopilot targets + per-mission vehicle setup:**

*Autopilot targets/constraints (drive the guidance, from the DB):*
- **Launch** — site lat/lon, target **plane (inclination)** + insertion altitude, **launch azimuth**,
  window mode (plane∩phase for ISS; the due-south **polar corridor** for Fram2).
- **Rendezvous** — target vessel (or **free-flyer flag** → omit rendezvous/dock/undock phases entirely),
  its orbit, docking port/approach axis, AE+KOS geometry, the named-burn schedule + AI burn.
- **Recovery** — booster mode (**ASDS vs RTLS**, chosen from energy — e.g. Ax-2/Ax-3 flew RTLS) + landing
  target; capsule **splashdown zone**.
- **Timeline seeds** — the callout MET, used **only** for the timeline acceptance check, never to drive a
  transition.

*Vehicle setup (per-mission, user-SETTABLE in the VAB; DB supplies the real default):*
- **Capsule** name/serial (Endeavour, Resilience, Grace…).
- **Booster flights flown** — a **settable option** (the real tail+flight, e.g. Crew-2 = **B1061 flight 2**).
  Feeds TestFlight wear/reliability → the FDIR's reliability expectation; a well-flown booster and a fresh
  one are genuinely different vehicles to the autopilot.
- **Fuel load** — propellant state at ignition (crew LEO loads full; the **booster-recovery reserve is an
  emergent consequence of the MECO energy**, not a scripted number).
- **Decals / livery / mission patch** — cosmetic mission identity shown on the DragonScreen; not
  autopilot-relevant.

The DB provides each as the real default; the user can **override any in the VAB**. Crucially, the
autopilot then reads the *resulting* vehicle state from **ground truth** (ConfigCache/parts/TestFlight) and
flies the physics — so even the setup fields are data it **measures**, never a branch it scripts on. This
keeps "fly any mission" and "true autopilot" the same property.

---

## 4. THE BUILD — bottom-up layers (each headless-tested before the next)

### L0 — Numerical core (VERIFY-THEN-REUSE, module by module — user 2026-08-26)
The how-to-build guide (§6) says build/verify the math toolbox first. Candidate modules already exist with
headless tests — Kepler (universal-variable), UPFG (Brand-Brown-Higgins), CW/LVLH, Hohmann, Hoverslam
(drag-aware), FuelFlowSimulation, V3 (ported from MechJebLib). **Reuse is EARNED by verification, not
assumed.** For each L0 module, at S0:
1. Read its existing tests and confirm they validate it against a **known-correct reference** — analytic
   cases, conservation laws (energy/momentum), reversibility/identity, or a cross-check vs the source
   (`Desktop/mechjeb_src`, PEGAS, the textbook equations). 
2. **Verified → reuse as-is.** **Not verifiable (weak/absent tests, or can't be checked) → rebuild or add
   the verifying tests until it IS proven** — then reuse. Nothing enters the build unverified.
The output of S0 is a per-module verdict (verified-reuse / needs-work). This is exactly "only reuse what
you can verify to be accurate."

### L1 — Navigation / derived quantities (pure functions of KSP state)
KSP hands exact position/velocity/orbit/attitude/rates (no filters needed). Build the derived quantities
the game does **not** hand you, each a tested pure function:
- **Dynamic pressure** `q = ½ρv²` — drives the max-Q throttle bucket and aero limits.
- **Predicted impact point** — RK4 forward-integrate under gravity + a **drag model** (not a vacuum
  parabola) — booster aim + entry footprint.
- **Ballistic coefficient** `β = m/(Cd·A)` — measured live from the actual deceleration.
- **Available torque / thrust per axis** — summed from the parts (gimbal + RCS + fins) so control knows its
  own authority as the stack sheds mass/stages.

### L2 — Control (attitude, throttle, RCS) — actuated BY CAPABILITY from the craft dump
- **Attitude = quaternion/vector PD**: error → rate command → torque → actuation, with the **arrestable-rate
  bound `ω_max = √(2·α·θ)`** (α = torque/inertia). This falls out of the vehicle's own inertia, so it is
  automatically right for a full stack, a spent booster, or a capsule — **no per-phase gain tuning**. Clamp
  actuation to [−1,1] and slew-limit; handle zero-authority (coast/no gimbal) by turning RCS on.
- **Throttle effector**: guidance sets the base; overlay a **max-Q bucket** (throttle down through measured
  peak q) and a **crew g-limit** (`throttle = g_limit·m/F_full`); take the more restrictive.
- **RCS translation** (`s.X/Y/Z`) for Draco orbital burns on the capsule (no main engine).
- **Actuate by CAPABILITY, from the real craft map** (`docs/CRAFT_DUMP_VEHICLE_MAP.md`, never by part name):
  engines `ModuleEnginesRF` (Activate/Shutdown/`thrustPercentage`, read `finalThrust`), **octaweb mode
  9→3→1 + individual-engine throttle = `ModuleTundraEngineSwitch`** (`NextEngineModeAction`/
  `ToggleIndependentThrottleAction`; `ModuleEngineConfigs` holds the CONFIG values), gimbal
  `ModuleGimbal.gimbalLimiter`, RCS/cold-gas/Draco `ModuleRCSFX.thrustPercentage`, **grid fins
  `SyncModuleControlSurface`** (Pitch/Yaw/RollActive + `authorityLimiter`, Extend/Retract), **entry CoM =
  `AdjustableCoMShifter`** (offset CoM → trim AoA/L·D), decouple `ModuleTundraDecoupler`/`ModuleDecouple`,
  chutes `RealChuteModule` (Arm/Deploy/Cut), legs `ModuleWheelDeployment`, nose/shroud/heatshield
  `ModuleAnimateGeneric`, control-point `ModuleCommand.ChangeControlPoint`, dock `ModuleDockingNode`,
  power/thermal/comms `ModuleDeployableSolarPanel`/`ModuleActiveRadiator`/`ModuleRealAntenna`, decals
  `ModuleColorChanger`/`ModuleTundraSoot`. TestFlight (`TestFlightCore`) read for the FDIR reliability
  expectation. Full map: `docs/CRAFT_DUMP_VEHICLE_MAP.md`.

### L3 — Guidance per phase (closed-loop, re-solved each cycle)
- **Ascent:** S1 = **pitch-programmed zero-AoA gravity turn** (open-loop, load relief) matching the DM-1
  pitch profile; **hand to closed-loop UPFG when the MVac lights** — UPFG flies the S2 insertion (linear-
  tangent `tan β = At+B` in a predictor-corrector; thrust integrals L/J/S/Q; conic-state gravity; cutoff on
  target radius/speed/FPA/plane). **Launch azimuth** `sin β = cos i/cos φ` ground-corrected; **launch
  window = plane ∩ phase**. Throttle bucket + g-limit from L2.
- **Booster recovery:** flip retrograde on cold-gas → **entry burn (3 engines)** cut on **physics** (bleed
  to a survivable reentry speed AND reserve exactly what the hoverslam solver needs — NOT a tuned frac) →
  **grid-fin aero descent** steering the **predicted impact point** to the deck by **AoA (magnitude→
  downrange) + bank (direction→crossrange)**, aim-to-MISS until nominal → **landing burn (hoverslam,
  3→1 engine)** ignited from the drag-aware solver, v=0 at h=0 → legs.
- **Rendezvous:** the **real named-burn schedule** (Phase→Boost→Close→Transfer→Coelliptic→AI→Midcourse),
  Hohmann phasing for the raises + **CW two-impulse** for the terminal legs, **offset targeting** (a failed
  burn misses the KOS), passive-abort free-drift check. Values from the telemetry DB (AI burn 90 s/0.72 m/s
  at 7.5 km).
- **Docking:** **R-bar → V-bar L-approach**, holds at **WP0 (400 m below) / WP1 (~220 m) / WP2 (20 m)**,
  **6-DOF glideslope** closing at ~8 cm/s, soft capture (ring+3 petals) → hard capture (12 hooks).
- **Return:** undock (hooks open, 2 sep burns) → **4 departure burns** (0–3) → **phasing burn (~6 min)** →
  **trunk jettison → deorbit burn (Draco, ~987 s, +5 min after jettison)** → **bank-angle lifting entry**
  (offset-CoM trim AoA, L/D~0.2; roll the lift vector for downrange, bank-reversal S-turns for crossrange —
  Apollo/Orion law) → drogues ~5.5 km / mains ~1.8 km → splashdown ~5–8 m/s.

### L4 — Mode manager + crew gates (the conductor)
- **Hierarchy of FSMs**: each phase an FSM under a master FSM; transitions fire on **crew command /
  autonomous measured trigger / FDIR failsafe** — never a bare clock. **Data-driven** from the Mission
  Profile.
- **Crew gates G1–G16** bracket the automated phases (countdown ingress/leak/hatch/**GO prop-load**/**LES
  ARM**/internal-power/**GO launch**; ascent monitor+abort; **GO for AI**, **WP0/WP1/WP2 holds**, **GO for
  docking**; **GO for undock**, **GO for deorbit**; entry monitor). Real countdown MET from the DB. The
  vehicle **HOLDS at each gate** and proceeds only on the crew's GO; HOLD and ABORT available throughout.
- Owns **manual-takeover** at WP1/WP2 (the real touchscreen docking mode).

### L5 — FDIR (the safety spine, from `LAYER3_AUTONOMY_PLAN.md`)
- **`HealthMonitor`** primitive: residual (expected−actual) → verdict (Nominal/Degraded/Failed) with
  **time-based debounce** (seconds, warp-proof) + **hysteresis**.
- **`FaultResponse`** ladder: `Continue<Retry<Reconfigure<Replan<Downmode<Abort`; `Worst()` for concurrent.
- **Monitors** (one per regime): thrust-delivery, trajectory-divergence, convergence-stall, resource-margin,
  no-control-solution, keep-out-breach. Re-planning is a **normal event** (re-seed the solver from live
  state); escalate to **`AbortResponder`** (LES / KOS-retreat / safe-hold) only when re-plan can't recover.
- **Abort-to-home is the guaranteed floor** — the crew always lands. Must **never mistake an intended crew
  GO-gate HOLD for a frozen-plan fault.**

### L6 — Self-calibration (kills the tuned constants)
- **`Estimators`** (RLS / first-order filters) live-estimate: thrust & Isp (accel×mass), mass, ballistic
  coefficient/drag, control authority, entry L/D & trim, steering sign/scale.
- **`LearnedParams`** (`PluginData/learned.cfg`): persist measured terminal-v, descent time, ascent
  time/lon-gain, bc-by-Mach, barge-miss residual → the vehicle **improves over flights with zero human
  tuning**. Seed each from the §5 research value; each flight overwrites with the measurement.

### L7 — Instrumentation (built the same pass as every layer above)
Extend `FlightRecorder.cs`: per-phase guidance internals (decisions/targets/triggers/planned+delivered Δv),
control residuals, the `fd_` FDIR block (every monitor residual+verdict+chosen response), which estimator
value is live vs seed, and the **emergent event times** (MECO/sep/entry-burn/SECO/docking/deorbit/splash)
for the timeline check against the DB. `assess_flight.py` reads it; the corpus is the validation set.

---

## 5. THE CONSTANTS REGISTER — starting values FROM RESEARCH (not the current code)

### 5.0 Live vehicle numbers — from `ModuleManager.ConfigCache` (verified S0c, 2026-08-26)
The authoritative engine numbers the guidance uses. Read from the live ConfigCache (RealFuels CONFIG
layer, **not** the base `ModuleEnginesRF` shell). These REPLACE the .md spec figures.

| Engine | Thrust (kN) | minThrust | Isp (vac/SL) | ullage | ignitions | spool | propellant |
|---|---|---|---|---|---|---|---|
| **S1 octaweb — Merlin1D (×9, AllEngines)** | **6681.6** | 2610 (~39%) | **311 / 282** | True | **−1 (unlimited)** | **instant** (`throttleResponseRate=1e6`) | RP-1 / LqdOxygen |
| S1 Merlin1D+ / 1D++ variants | 7425 / 8227 | 2970 | 311/282, 311/288.5 | True | −1 | instant | RP-1/LOX |
| **S1 ThreeLanding (×3)** | **2227.2** (=6681.6/3) | 870 | 311/282 | True | −1 | instant | RP-1/LOX |
| S1 CenterOnly (×1) | ~742 (per engine) | ~290 | 311/282 | True | −1 | instant | RP-1/LOX |
| **S2 MVac — Merlin1DVac** | **805** | 360 (~45%) | **345 / 216** | True | −1 | instant | RP-1/LOX |
| **Draco (pod RCS)** | thrusterPower **2 kN** | — | **240 / 100** | — | — | — | **MMH / NTO** |

**Corrections to prior notes (ConfigCache is ground truth):**
- **`ignitions = −1` (UNLIMITED relights)** on every S1/S2 CONFIG — NOT the "ignitions = 4" a memory
  claimed (0 occurrences of `4`, 9 of `−1`). The booster is not relight-limited in this install; TestFlight
  (`TestFlightFailure_IgnitionFail`) supplies per-ignition failure probability instead. FDIR uses the
  TestFlight reliability, not a hard ignition count.
- **Spool is INSTANT** (`throttleResponseRate = 1000000`, the instant-spool patch applied to all 9). So the
  landing solver's spool term ≈ **0 s** (the old `LandingSpoolS = 1.2` is stale) — the hoverslam dead-time
  is ullage-settle, not spool.
- **UPFG cross-check:** the S2 = 805 kN / Isp 345 s / ullage is exactly the UpfgTest scenario — the verified
  UPFG was tested on the real vehicle numbers. ✔
- Draco: a MonoPropellant/Isp-305/228 config is ALSO present in the pod alongside the MMH+NTO/Isp-240 RCS —
  confirm which drives the deorbit burn vs attitude (see [[dragon-return-propellant-mmh-nto]]).
- **RSS Isp uses g0 = 9.80665**; per-engine S1 thrust ≈ 742 kN (6681.6/9).



Every value below is derived from the ground-truth index (§2). Where the current code already equals the
research value, that is stated (its provenance is the research, not the code). **Values that CHANGE, are
KILLED (→ self-cal), or MOVE to the Mission Profile are flagged.** RO **vehicle numbers (Merlin/MVac/Draco
thrust/Isp) are read live from `ConfigCache`, never set here** — the .md spec figures are reference only.

### Ascent
| Constant | Research-derived start | Source | vs current |
|---|---|---|---|
| Target parking orbit | **200 km circular, 51.6°** | LAUNCH §5.4 (Demo-2 ~199×365) | = (`RssParkingAltitudeM`=200000) |
| Pitch kick | **T+10 s**, small azimuth-aligned | LAUNCH §4, DM-1 (+5.3°/s @ T+10) | derive fresh |
| S1 pitch program | FPA ~**79°** to T+45 → **46.6° at MECO**, zero-AoA turn | DM-1 template | new profile (was heuristic loft) |
| Max-Q throttle bucket | throttle down through **measured** q (~30–35 kPa); DM-1 dip ~**1.35 g** | LAUNCH §5.1, DM-1 | trigger on q, not clock |
| S1 g-limit | **3.5 g** cap (crew feels ~3.2 g; DM-1 peak 3.26 g) | LAUNCH §5.2, DM-1 | confirm value |
| S2 g-limit | **4.0 g** cap (crew ~4.1 g near SECO; DM-1 3.57 g) | LAUNCH §5.2, DM-1 | confirm value |
| MECO condition | **emergent** — staging energy leaving booster reserve | LAUNCH §4.6, BOOSTER | not a set value |
| UPFG SECO cutoff | Tgo ≤ **0.2 s** (solver tolerance) | ASCENT_GUIDANCE_UPFG | = (`UpfgSecoTgoS`) |
| Launch azimuth | `sinβ=cos i/cos φ`, ground-corrected | LAUNCH §6.3 | law, not constant |

### Booster recovery
| Constant | Research-derived start | Source | vs current |
|---|---|---|---|
| Entry burn engines | **3** (center + 2 opposed) | BOOSTER | = |
| Entry burn cut speed | bleed to ~**1300 m/s** (survivable reentry) | BOOSTER (2300→1300) | = (`EntryBurnTargetSpeedMps`=1300) |
| Entry burn reserve | **solver-computed** landing need + margin | LAYER3, BOOSTER | **KILL** `EntryBurnReserveFrac`=0.20 → self-cal |
| Landing burn engines | **3 → 1** hoverslam; handover ~**60 m/s** | BOOSTER | = (`HandoverVsMps`=60) |
| Grid-fin AoA cap | **20°** (real up-to-20° reorientation); restrict only near target | PHASE_2 §5 | **CHANGE** `AeroAoaDeg` 25 → 20 |
| Offset-to-miss aim | aim beside deck until all-nominal (~2 km) | PHASE_2 §5 | = (`EntryAimBufferM`=2000), auto-trim in L6 |
| Ullage settle dead-time | **6 s** (ullage-until-lit) | BOOSTER, RO ullage | = (`LandingDeadTimeS`=6); verify cold-gas budget |
| Merlin spool | **from `throttleResponseRate` in ConfigCache** | ConfigCache | verify (`LandingSpoolS`=1.2 may be stale) |
| Droneship target | Crew-2 **OCISLY on the achieved ground track** (~560 km downrange) | master plan | **MOVE to MissionProfile** (drop hard-coded lat/lon) |

### Rendezvous & docking (from the real telemetry — the biggest fidelity gain)
| Constant | Research-derived start | Source |
|---|---|---|
| Named-burn order | Phase→Boost→Close→Transfer→Coelliptic→AI→Midcourse | DB (Crew-1 rendezvous PDF) |
| **AI burn** | **90 s, 0.72 m/s at 7.5 km** | DB — exact real value |
| Co-elliptic offset | ~**10 km** below the ISS | PHASE_3 |
| WP0 / WP1 / WP2 | **400 m below / ~220 m axis / 20 m** | DB + PHASE_4 |
| Contact speed | ~**8 cm/s** | PHASE_4 |
| KOS / AE | **200 m radius** / **2000×1000 m** | PHASE_4 |
| Capsule slew rate | **10 °/s** (Draco authority) | Attitude (`CapsuleMaxRateDps`=10) |
| Standoff (WP2) | **20 m** | PHASE_4 | **CHANGE** `StandoffM` 25 → 20 |
| Draco | **16 × 400 N, Isp 300, MMH+NTO** — read live | LAUNCH §1.2 + ConfigCache |

### Return / entry
| Constant | Research-derived start | Source |
|---|---|---|
| Departure burns | **0,1,2,3** (DB0 +0, DB1 +5 m, DB2 +48 m, DB3 +1h39) | DB + PHASE_5 |
| Phasing burn | ~**6 min** | PHASE_5 |
| Trunk → deorbit | jettison, then deorbit burn **+5 min**, ~**987 s** burn | DB + PHASE_6 |
| Entry interface | ~**120 km** | PHASE_6 |
| Bank-angle entry | trim AoA ~**12°**, L/D ~**0.2**, β ~**440–510** | PHASE_6 | = (`CapsuleBcKgM2`=440) |
| Peak entry g | **4–5 g** | PHASE_6 |
| Drogues / mains | **5.5 km / 156 m/s**; **1.8 km / 53 m/s** | PHASE_6 |
| Splashdown | ~**5–8 m/s**; zone = **MissionProfile** | PHASE_6 | **MOVE** DeorbitOps lat/lon → profile |
| Entry footprint bias | RSS-calibrated (flight-tuned, self-cal) | GNC audit | `AlongBiasM`/`CrossBiasM` → L6 |

### Crew gates (countdown MET, from the DB)
| Gate | MET | Source |
|---|---|---|
| GO for propellant load (G4) | **T−45:00** | DB (Crew-1 PDF) |
| LES ARMED (G5) | **T−37:00** | DB |
| Dragon internal power (G6) | **T−5:00** | DB |
| GO for launch (G7) | **T−0:45** | DB |
| Ascent callouts (G8) | maxQ +0:58, MECO +2:37, sep +2:40, SES-1 +2:48, SECO +8:50, Dragon sep +12:03 | DB |

### Control (no tuning — laws, not constants)
| Constant | Value | Source |
|---|---|---|
| Arrestable rate bound | `ω_max=√(2αθ)` | TRUE_AUTOPILOT §4.1 |
| Coast / capsule / ascent max rate | 4 / 10 / 2 °/s | Attitude |

---

## 6. VALIDATION & ACCEPTANCE (how each layer is proven)

- **Headless (`build.py test`)** — every pure layer before its glue: L0 conic/UPFG/CW convergence; L1
  derived-quantity correctness; L2 arrestable-rate + throttle limiter; L3 each guidance solver reaching its
  target on a point-mass; L4 gate machine (advances only on GO, NO-GO holds, KOS-breach → abort, free-flyer
  omits rendezvous gates); L5 monitors (corpus replay: feed past failure recordings, assert they'd catch
  free-fall / no-burn rendezvous / g-oscillation / entry over-burn); L6 estimators converge.
- **Timeline-match** — the emergent event times (recorded, L7) compared to the DB callout MET; a `Crew2
  TimelineTest` headless guard + the recording. A mismatch means the physics/vehicle deviates.
- **In-game (`build.py install`, full restart)** — one change per flight, fully recorded; read the CSV with
  `assess_flight.py`; keep or revert on the data. RSS/RO is the only high-fidelity test of the glue.
- **Proving run** — one Crew-2 launch → splashdown, vehicle flying every metre, user working only the gates,
  HOLD/ABORT correct at every gate, timeline matching.

---

## 7. BUILD ORDER (sequenced, resumable; each step lands something tested)

Bottom-up so each layer rests on a tested one. **Do not install/commit without the user; one change per
flight.**

- [ ] **S0 — Baseline, L0 verification & register.** (a) `build.py test` green. (b) **L0 verify-then-reuse
      audit** — per module, confirm the tests prove accuracy vs a known-correct reference; classify
      verified-reuse vs needs-work; fix the unverified before reuse. (c) Read the live `ConfigCache` for the
      Merlin/MVac/Draco numbers and pin them into the register (replace the .md spec figures). (d) Verify
      booster geometry from a craft dump.
- [ ] **S0b — Mission Profile + name resolver** (`pure/MissionProfile.cs` + a `data/crew_missions.json`
      loader): resolve `vessel.vesselName` → mission → profile (targets + vehicle setup incl. settable
      booster-flights/fuel/decals); free-flyer flag; no-match fallback + NO-GO. Pure + tested; every later
      layer reads its targets from the active profile, never a literal. Build it early — L3/L4 depend on it.
- [ ] **S1 — L1 nav/derived** (q, impact predictor+drag, β live, per-axis authority) + tests.
- [ ] **S2 — L2 control** (quaternion-PD + arrestable-rate; throttle bucket+g-limit; RCS translation) +
      tests. First in-game check: attitude holds/slews on the stack, booster, and capsule with no wheels.
- [ ] **S3 — L3 ascent** (S1 pitch program to DM-1 profile → UPFG S2; azimuth; window) + timeline check →
      **flight**: MECO/SECO/Dragon-sep emerge near the real MET; zero-AoA loads.
- [ ] **S4 — L3 booster** (entry-burn physics cut; grid-fin impact-null AoA=20°; hoverslam 3→1) → **flight**:
      lands on deck, entry/landing-burn times emergent.
- [ ] **S5 — L3 rendezvous** (named burns + CW terminal; AI 90 s/0.72 m/s; offset) → **flight**: burns fire,
      arrive at 7.5 km / WP0.
- [ ] **S6 — L3 docking** (L-approach, WP holds, 6-DOF, capture) → **flight**: soft→hard capture.
- [ ] **S7 — L3 return** (departure burns, phasing, trunk→deorbit, bank-angle entry, chutes) → **flight**:
      splashdown in zone.
- [ ] **S8 — L4 mode manager + crew gates** wired across all phases (countdown MET from DB; GO gates;
      HOLD/ABORT/takeover). *(Can be built alongside S3–S7 as each phase comes online.)*
- [ ] **S9 — L5 FDIR** (HealthMonitor + FaultResponse + monitors, observe-only first, then authority;
      abort-to-home floor) + corpus replay.
- [ ] **S10 — L6 self-cal** (Estimators + LearnedParams; retire each tuned constant behind its estimator).
- [ ] **S11 — Proving run** — full Crew-2 mission; then user flies multiple missions; correct from data.

---

## 8. GAP ANALYSIS — research completeness (honest)

**The research is complete for the Crew-2 reference rebuild.** Every phase has its real technique, its
guidance math, and real numbers; the telemetry database closed the largest holes (exact named-burn
schedule, AI burn magnitude, countdown gate MET, DM-1 pitch/throttle/g profile). Remaining items are
**flight-calibrated, not researchable further** — they have a sane research-derived starting value and are
tuned from the recording (and later moved to L6 self-cal):

1. **Grid-fin AoA cap in FAR** — real "up to 20°"; start 20°, tune from descent authority (self-cal target).
2. **RSS entry-footprint bias** (`AlongBiasM`/`CrossBiasM`) — RSS aero differs from Earth; measured from the
   entry recording (Overflight descent-time), a known open RSS-cal (GNC audit).
3. **Booster geometry / Merlin spool** — read from craft dump + `ConfigCache` at S0, not researched.
4. **Free-flyer S2 burn plans** (Inspiration4 circular, Polaris Dawn elliptical, Fram2 polar azimuth) — not
   needed for the Crew-2 rebuild; added later as Mission-Profile data (DB has the orbits).
5. **UPFG vs the original NASA report** — verified against PEGAS-MATLAB, not line-by-line vs NTRS
   19740004402; a primary-source cross-check owed but not blocking (PEGAS is faithful).

None blocks the build.

---

## 8b. EXECUTION LOG

### S0 (a,b) — L0 verify-then-reuse audit — DONE 2026-08-26
`build.py test` **green** (all suites). Each L0 module checked for verification against a **known-correct
reference**; all pass → **all reuse-eligible, no L0 rebuild needed**:

| L0 module | Verified against | Verdict |
|---|---|---|
| **Kepler** (`pure/Kepler.cs`) | analytic quarter-orbit 90° rotation, full-period identity, elliptical energy+ang-momentum conserved to 1e-6, reverse-propagation identity | ✅ reuse |
| **UPFG** (`pure/Upfg.cs`) | predictor-corrector convergence (<1% tgo), converged desired-cutoff radius matches target to 1e-3, iF unit/in-plane/prograde/loft, point-mass closure flies the measured MECO state to ~200 km, ullage-poison guard | ✅ reuse |
| **LVLH frame** (`pure/Lvlh.cs`) | axis signs (radial/along/cross), eccentric-station radial-leak removal, OffsetToWorld↔Project round-trip, WP0 offset | ✅ reuse |
| **CW two-impulse** (`pure/CwTargeting.cs`) | exact CW state-transition matrix; **solved transfer's free-drift reproduces the aim point at tof** (self-consistency), t=0 identity, passive-abort min-range | ✅ reuse |
| **Hohmann** (`pure/Hohmann.cs`) | analytic circular speeds (7788/7659 m/s), raise/circularise dv signs+magnitudes, retrograde lowering, no-op zero | ✅ reuse |
| **Hoverslam** (`pure/Hoverslam.cs`) | property-based: arrests at deck; drag lowers, spool+dead-time raise ignition; monotonic in speed; cannot-stop guard; anchored to the real 0824 landing | ✅ reuse |
| **FuelFlow** (`MechJebLib/FuelFlowSimulation`) | port proven vs closed-form rocket eqn `Isp·g0·ln(m0/m1)` in vacuum AND sea-level (AtmosphereCurve by pressure) + mass/thrust accounting | ✅ reuse |

**S0b (craft data) — DONE 2026-08-26:** from the auto-saved Crew-2 template in save `test`, generated **19
per-mission `.craft` files** (`saves/test/Ships/VAB/<Mission>.craft`) — `ship = <mission name>` (the
autopilot's selector), a real-mission `description` (crew + booster tail/flight + capsule + orbit +
recovery), and the **booster flights baked in**: S1 `TestFlightCore.currentFlightData`/`initialFlightData`
set from `(flight_number−1)` (0 = new → ~10000 = mature) and `ModuleTundraSoot.toggleSoot` for the reflown
look — done for the **confirmed** boosters (DM-2 B1058.1, Crew-1 B1061.1, Crew-2 B1061.2, Crew-3 B1067.2);
the booster flights baked in. Only the S1 booster's TestFlight is edited (pod/S2 untouched).

**S0b (booster data + resolver) — DONE 2026-08-26:** all 19 crewed missions' **booster tail+flight
verified from primary sources** (OrbitCodex/NASASpaceflight/Spaceflight Now — no Wikipedia) and written to
`crew_missions.json`; every craft regenerated with the real flightData (new→0 … mature→10000) + soot.
**First rebuilt-autopilot code landed:** `pure/MissionProfile.cs` — the mission-as-data catalog (mirror of
the DB) + `Missions.Resolve(vessel.vesselName)` (exact match, then longest-substring so Crew-1≠Crew-11,
else a Valid=false fallback → NO-GO, never fly a guessed mission). `test/MissionProfileTest.cs` = 21 checks
green (collision, free-flyer, RTLS, round-trip, fallback). Booster table: DM-2 B1058.1, Crew-1 B1061.1,
Crew-2 B1061.2, Crew-3 B1067.2, Inspiration4 B1062.3, Ax-1 B1062.4, Crew-4 B1067.4, Crew-5 B1077.1, Crew-6
B1078.1, Ax-2 B1080.1, Crew-7 B1081.1, Ax-3 B1080.5, Crew-8 B1083.1, Polaris Dawn B1083.4, Crew-9 B1085.2,
Crew-10 B1090.2, Fram2 B1085.6, Ax-4 B1094.2, Crew-11 B1094.3.

**S1 (L1 navigation / derived) — DONE 2026-08-26.** Cross-checked the impact predictor against the ORIGINAL
`neuoy/KSPTrajectories` source (RK4, adaptive step, gravity+aero in the velocity frame — our method matches;
Trajectories samples the real Stock/FAR aero force at a descent AoA = drag **and** lift, which our port had
simplified to drag-only). Built:
- **`pure/Trajectory.cs`** (re-introduced, verified + ENHANCED) — RK4 impact integrator with **full drag +
  LIFT**. The lift is `(L/D)·drag` perpendicular to the surface-relative velocity, banked by σ (Apollo
  decomposition: vertical lift → range, horizontal → crossrange). **L/D and bank are MEASURED LIVE** from
  the vessel's aero acceleration (`MeasureAero`), so the prediction is tailored to whatever is flying —
  a grid-fin booster at AoA vs a bank-modulated capsule — never a drag-only assumption (L/D=0 is only the
  degenerate case). β measured the same way (`BallisticCoefficientFrom`), both first-order filtered
  (`SmoothBc`). Verified: vacuum vs analytic `√(2h/g)`/`√(2gh)`, drag properties, **lift up→farther /
  down→shorter / banked→crossrange**, and `MeasureAero` recovering L/D + bank (38 checks).
- **`pure/BoosterDrag.cs`** (re-introduced) — empirical bc(Mach) curve mined from the flight corpus.
- **`pure/Predict.cs`** (re-introduced) — impact (damped iteration) + closest-approach + timing helpers
  (354 checks).
- **`pure/Aero.cs`** (new) — dynamic pressure `q=½ρv²`, Mach, sound speed (P/ρ and T forms), isothermal
  density (13 checks).
- **`pure/Authority.cs`** (new) — per-axis control authority: `α=torque/inertia`, the arrestable-rate bound
  `ω_max=√(2αθ)` (the no-tuning control law L2 will cap on), torque summation (15 checks).
All headless-tested green; `DragonScreen.dll` builds.

**S2 (L2 control) — DONE 2026-08-26. Built FRESH from the research (TRUE_AUTOPILOT §4) + L1 `Authority` —
NOT from the deleted tree (user: the deleted code is unproven, use the research).** `pure/ControlLaw.cs`
(renamed from `Control` — the kept screen layer already owns that name):
- **Attitude (per body axis → FlightCtrlState pitch/yaw/roll):** `RateCommand` = the smaller of a linear
  terminal region (θ/τ, no chatter at zero) and the **braking curve `k·√(2αθ)`** from L1 Authority — so
  the commanded rate is always arrestable and there is **no per-phase gain tuning** (the same law flies the
  stack, a spent booster, a bare capsule). `Actuate` = torque-based (`I·Δω/τ`), clamped [−1,1], slew-limited,
  and **zero-authority → 0** (the glue turns RCS on where gimbal/RCS/fins give no torque). `AxisCommand`
  ties them end-to-end.
- **Throttle (`ThrottleLimit`):** the exact-physics **crew g-limit** (`throttle ≤ g·g0·m/F_full`, ~4 g) and
  the **max-Q bucket** (measured-q ramp from qSoft→qLimit down to a floor, then back up), MORE RESTRICTIVE
  wins. Autonomous (q measured), not scheduled.
- **RCS translation (`TranslateAxis`):** desired accel / available RCS accel → clamped s.X/Y/Z (Draco burns).
Verified (26 checks): rate proportional/braking/cap/sign/authority-scaling, actuate clamp+slew+zero-auth,
axis sign + on-target-quiet, g-limit + bucket + more-restrictive + base-clamp, translation clamp.
**L3 guidance per phase** (ascent pitch-program→UPFG; booster entry+grid-fin+hoverslam; rendezvous
named burns+CW; docking L-approach; return departure+deorbit+bank-angle entry), each closed-loop from the
research-derived targets in §5, using L1 nav + L2 control.

**S3a (L3 ASCENT) — DONE 2026-08-26. Built FRESH from the research + PEGAS primary source — the deleted
tree is off-limits (user).** All headless-green, `DragonScreen.dll` builds:
- **`pure/Vec3.cs`** — minimal 3-D vector (dot/cross/normalize/angle), fresh.
- **`pure/Conic.cs`** — universal-variable Kepler propagator (Stumpff), fresh from Bate-Mueller-White;
  verified vs analytic conics (quarter-orbit, period identity, energy+ang-momentum to 1e-8, reversibility).
- **`pure/LaunchAzimuth.cs`** — `sin β = cos i/cos φ` + Earth-spin ground correction (LAUNCH §6.3);
  verified (Cape 51.6°→~45° inertial pulled to ~43° ground; due-east at i=φ; i<φ unreachable; polar south).
- **`pure/Ascent.cs`** — S1 **pitch-programmed gravity turn following the REAL DM-1 pitch-vs-speed profile**
  + the phase FSM (VerticalRise→GravityTurn→MECO→Coast→S2Burn→SECO) advancing on measured state, throttle
  via L2 (max-Q bucket + g-limit), runaway backstop. Verified (129 checks incl. azimuth).
- **`pure/Upfg.cs`** — closed-loop S2 insertion, **ported faithfully from the PEGAS `unifiedPoweredFlight
  Guidance.m` primary source** (Brand-Brown-Higgins, single active stage): linear-tangent steering, thrust
  integrals L/J/S/Q/P/H, conic-state gravity (block 7 → `Conic`), Vgo corrector. Verified: converges to a
  fixed point, physical Tgo, iF unit/in-plane/prograde/**loft**, cutoff-radius match, and the decisive
  **point-mass closure — single-stepped UPFG flies the measured 62 km/2482 m/s MECO state to a 200 km orbit**
  (apo within 80 km, pe within 20 km). Uses the real §5.0 S2 numbers (805 kN, Isp 345). Caught + fixed one
  port bug (a bogus |Vgo|<ve cutoff that fired immediately).
**S3b (L3 BOOSTER RECOVERY) — DONE 2026-08-26. Built FRESH from the research (deleted tree off-limits).
Headline requirement (user): PERFECT CONTROL AT ALL TIMES — no drifting at weird AoA.** All green, DLL builds:
- **`pure/Hoverslam.cs`** — landing-burn ignition solver (fresh): numerically integrates the ullage-settle
  **dead-time free-fall** + spool + braking under thrust−gravity+**measured drag** to the altitude where a
  full brake nulls v at h=0; `EnginesFor` picks the fewest engines that can arrest (3→1). Verified: dead-time
  raises ignition >1 km, drag lowers it, spool raises it, faster→higher, cannot-stop→light-now.
- **`pure/GridFin.cs`** — aero steering (PHASE_2 §8b): a **small, capped (≤20°), DELIBERATE** AoA whose lift
  is pointed toward −impact-error (magnitude→downrange, direction→crossrange) with a lead term. Verified:
  overshoot→correct-back, crossrange tilt, AoA capped (no wild angle), lead anticipates.
- **`pure/BoosterDescent.cs`** — the recovery FSM (Flip→EntryBurn→AeroDescent→LandingBurn→Landed). **THE
  CONTRACT: `Guide()` ALWAYS returns a definite unit `AimForward`** — engines-first (retrograde: thrust
  opposes surface velocity) through flip/entry/descent, retrograde + the held steering AoA during aero
  descent, braking-vector (≈up, nulls horizontal drift) on landing; the stage is NEVER uncommanded, NEVER
  drifting. Transitions on measured state (entry burn descending through 70 km, cut at the §5.0 1300 m/s
  survivable speed, landing burn at the hoverslam ignition altitude). Verified (36 checks): **AimForward a
  unit vector in every phase incl. invalid input, AoA capped everywhere, aim = retro tilted by exactly the
  commanded AoA**, engines-first, all transitions, 3-engine entry + hoverslam.
L2 holds this attitude with all authority (cold-gas RCS in vacuum, grid fins + gimbal in air).
- **Engine-step correction (from the craft dump, user-flagged):** the octaweb is ONE part with 3 modes
  (AllEngines/ThreeLanding/CenterOnly), **each with `ignitions = 1`** on the live vessel. So there is **no
  3→1 step during the landing burn** — that would re-ignite + spool. Budget: liftoff=AllEngines, entry
  burn=**ThreeLanding (3)**, landing burn=**CenterOnly (1 centre engine, lit once, continuous to the deck)**.
  The glue selects the mode **absolutely** (`selectedIndex` / the mode's own `ModuleEnginesRF`) **while OFF**,
  **never `NextEngineMode`** (it cycles). `BoosterDescent.EngineMode` = 3 entry / 1 landing; rule in
  `docs/CRAFT_DUMP_VEHICLE_MAP.md` §3a.
**S3c (L3 RENDEZVOUS) — DONE 2026-08-26. Fresh (deleted tree off-limits). Full-control contract (user).**
All green, DLL builds:
- **`pure/Lvlh.cs`** — station LVLH frame (radial/along/cross) + the rotating-frame velocity transform
  (a co-orbiting body reads zero LVLH velocity — verified) + OffsetToWorld.
- **`pure/Cw.cs`** — Clohessy-Wiltshire STM + **two-impulse transfer** + free-drift + passive-abort
  min-range. Verified by self-consistency: **the solved transfer's free-drift reaches the aim point**;
  t=0 identity; sane Δv.
- **`pure/Hohmann.cs`** — vis-viva + Hohmann raise/lower Δv + transfer time + **phase-lead** timing.
  Verified vs analytic (7788/7659 m/s, ~60+60 m/s for 200→420 km, ~45 min, retrograde lowering).
- **`pure/Rendezvous.cs`** — the **named-burn FSM** (Phasing → CoElliptic → ApproachInit → Midcourse →
  Arrived) advancing on **measured range**, each terminal leg a CW two-impulse to an **OFFSET** aim (miss
  the KOS), feeding the DB AI standoff (7.5 km). **⛔ FULL CONTROL: `Guide()` ALWAYS returns a unit
  `AimLvlh`** — the capsule is never floating; the Dragon has no reaction wheels so all manoeuvres are on
  the 16 Dracos, which share rotation+translation, so the rule is **ATTITUDE-FIRST then translate**
  (`AttitudeReady` gates the burn) — never both at once (the old off-axis failure). Verified (18 checks):
  AimLvlh unit in every phase incl. invalid, phase progression, burn points along the burn, GO-gated.
**S3d (L3 DOCKING) — DONE 2026-08-26. Fresh. Full-control contract.** All green, DLL builds:
- **`pure/DockControl.cs`** — the 6-DOF glideslope servo: a per-axis position+velocity servo with a
  **closing-speed cap that tapers with range** (fast between waypoints → ~8 cm/s contact at the port),
  nulling lateral offset the same way. Verified: cap = contact speed at the port / far speed at the taper
  range, closes toward the target, never exceeds the cap.
- **`pure/DockApproach.cs`** — the **R-bar→V-bar L-approach FSM**: WP0 (400 m below) → GO → swing to WP1
  (~200 m front on the V-bar) → GO → WP2 (20 m) → GO for docking → Contact (~8 cm/s) → Captured. Each
  waypoint a **station-keeping HOLD released only by a crew GO**; **ANY unplanned KOS breach → automatic
  ABORT/retreat**. **⛔ FULL CONTROL: `Guide()` ALWAYS returns a unit `AimLvlh`** pointing the docking ring
  at the port (verified in every phase incl. invalid). Verified (26 checks): the whole GO-gated sequence,
  contact→captured, KOS abort (and no false abort on the planned corridor).
**S3e (L3 RETURN) — DONE 2026-08-26. Fresh (Phase 5+6 research). Full-control contract.** All green
(70 checks), DLL builds 187.5 KB:
- **`pure/Departure.cs`** — the Phase-5 FSM (mirror of the approach): on GO for undock, TWO tiny sep
  burns push AWAY from the port to a standoff → FOUR departure burns (CW two-impulse hops to OFFSET aims,
  up-and-over out of the 200 m KOS then down to the co-elliptic point ~10 km below / 20 km behind — every
  intermediate point outside the KOS, corridor-safe) → a departure **phasing** burn (Hohmann apsis-lower,
  retrograde −V-bar) to line the ground track up for the splashdown zone → Departed (coast). Reuses
  Cw/Hohmann/Lvlh. **⛔ FULL CONTROL:** `Guide()` always returns a unit `AimLvlh` (away from the station /
  along the burn), attitude-first-then-translate.
- **`pure/DeorbitGuidance.cs`** (named to avoid the kept `Deorbit` screen stub) — trunk jettison FIRST
  (mass save; burns up), brief settle, then the LONG low-thrust retrograde Draco burn, **closed-loop on
  the MEASURED periapsis** (cut when Pe ≤ target entry-interface radius), then orient heat-shield-forward.
  `DeorbitDvMps = |Hohmann.Dv1|` lowering Pe to R+h_EI. Full-control unit aim (retrograde burning /
  shield-forward after).
- **`pure/Entry.cs`** — the **lifting bank-angle entry** (Apollo/Orion): `|σ|` NULLS the predicted
  downrange error (predicted long → more bank → shorter), `sign(σ)` REVERSED on a velocity-dependent
  crossrange deadband with hysteresis (the S-turns); errors from the L1 lift-aware predictor (glue).
  **⛔ CoM SHIFTER USED CORRECTLY (user):** engage `AdjustableCoMShifter` Descent Mode ONCE before EI and
  leave it on (`EngageDescentMode`); `OffsetPercent` (0..1) set from the target L/D (~0.2 = full);
  **NEVER toggled to steer** — bank reversals are an RCS ROLL of the vehicle, the CoM shifter only sets
  the aerodynamic trim AoA the capsule holds by itself. Full-control unit aim (shield-forward + BankRad).
- **`pure/Chutes.cs`** — state-based crew-safety backstop: 2 drogues ≤5.5 km, 4 mains ≤1.8 km (on
  measured altitude + descent rate, not a clock), splashdown ~5–8 m/s.
- Test `test/ReturnTest.cs` (70 checks): full-control unit aim in every phase of all four FSMs; sep pushes
  away; CW hops burn; phasing retrograde; trunk-before-burn; closed-loop Pe cutoff; bank magnitude/sign
  laws + CoM-shifter contract; chute gates + splashdown.

**ALL L3 GUIDANCE COMPLETE.**

**S4 (L4 MODE MANAGER + CREW GATES) — DONE 2026-08-26. Fresh (architecture + telemetry-DB gate map).**
All green (38 checks), DLL builds. The real Crew Dragon operating concept: autonomous BETWEEN gates,
crew-authorised at each real decision point.
- **`pure/CrewGate.cs`** — the gate STATE MACHINE + the authoritative display types (ItemKind /
  ChecklistItem / Gate / ProcState, **moved here from the demolition stub** so there is one source; the
  screens read them unchanged). A gate = a titled checklist of CREW items (the user taps) + AUTO items
  (the system confirms) + the decision. `Step()` resolves the `GatePhase`: Holding→GoReady→Go, plus NoGo
  (hold) and Abort (absorbing); crew GO clears **only** a fully-satisfied checklist; GO resumes from NoGo.
- **`pure/CrewGates.cs`** — the concrete catalog G1..G15 built FROM a MissionProfile, grounded in the real
  callout timeline (`data/crew_missions.json`): countdown G1 ingress/comm · G2 suit leak · G3 hatch/cabin
  leak · G4 GO for prop load · **G5 ARM the launch-escape system** · G6 internal power · **G7 "Dragon crew
  — GO"**; prox-ops G9 GO-for-AI · G10/G11/G12 the WP0/WP1/WP2 holds · G13 docking complete; return G14 GO
  for undock · G15 GO for deorbit. A **free-flyer omits G9..G14** (no rendezvous/dock/undock).
- **`pure/ModeManager.cs`** — the conductor: the mission as an ordered list of STEPS (a GATE = hold for the
  crew's GO, or a FLY phase = an L3 controller flies until complete), built per profile. `Advance()` walks
  it — a phase advances only when its bracketing gate is `Go`; ABORT is absorbing. Never flies anything;
  decides WHICH phase is active and whether the autopilot may proceed. ISS crew = full timeline; free-flyer
  = countdown → ascent → free-flight → deorbit → return.
- Test `test/CrewGateTest.cs` (38 checks): the state machine (incomplete→Holding, complete→GoReady, GO
  clears, incomplete-GO does not, NoGo holds + resumes, ABORT absorbing); the catalog (7 countdown, 5
  prox, ISS-vs-free-flyer return, ById none for a free-flyer approach gate); the conductor (plan shape,
  holds without GO, GO/PhaseComplete advance, abort, and a full mission walk to Complete).

**S5 (L5 FDIR) — DONE 2026-08-26. Fresh (architecture §9/§11 + craft-dump abort).** All green (29
checks), DLL builds. The safety spine — Detect → Isolate → Recover, abort-to-home the guaranteed floor.
- **`pure/FaultMonitor.cs`** — the ONE detect+debounce primitive: a residual over the trip threshold must
  PERSIST for a confirmation time to trip, and clears on a separate LOWER hysteresis threshold held for a
  clear time (no flapping). Timing is ELAPSED REAL dt (seconds), never tick counts, so it means the same
  under time-warp. (Named FaultMonitor to avoid System.Threading.Monitor.)
- **`pure/Fdir.cs`** — the concrete monitors (thrust-shortfall, trajectory-divergence, convergence-stall,
  resource-critical, no-control-solution, keep-out-breach), the isolate-by-priority step, and the
  phase-aware fault→recovery table on the least-intervention LADDER `Continue → Retry → Reconfigure →
  Replan → Downmode → Abort/SafeMode`. **KOS breach → Abort; thrust shortfall on ascent → Abort (launch);
  resource at zero → SafeMode.** ⛔ the **convergence-stall monitor is SUPPRESSED during an intended crew
  GO-gate HOLD** (`GateHolding`) so FDIR never mistakes a hold for a frozen-plan fault (§11).
- **`pure/AbortResponder.cs`** — the phase-correct action: **pad/ascent + LES armed → LAUNCH ESCAPE**
  (fire SuperDracos — pod's abort config, craftdump; separate; chutes), **on-orbit prox-ops → KOS RETREAT**
  (back out; a SuperDraco escape is a LAUNCH system, an orbital abort is a retreat), **docked → SAFE-HOLD**,
  **entry/under chutes → RIDE IT DOWN** (chute backstop). ⛔ FULL CONTROL: always `HoldAttitude` so nothing
  floats. Pad abort without an armed LES → safe-hold (no escape available).
- Test `test/FdirTest.cs` (29 checks): the debounce timing (confirm/clear/deadband/dt≤0); every monitor +
  the priority order (KOS outranks thrust); the recovery table; the stall suppression during a hold; and
  every abort-responder phase branch incl. LES-armed vs not.

**S6 (L6 SELF-CAL) — DONE 2026-08-26. Fresh (architecture §10).** All green (16 checks), DLL builds. The
piece that KILLS tuned constants — the guidance's "constants" become in-flight MEASUREMENTS.
- **`pure/Rls.cs`** — Recursive Least Squares with a VARIABLE forgetting factor (the standard onboard
  parameter-ID method): scalar model y = φ·θ + noise; `λ = λ_max − (λ_max−λ_min)·min(1, e²/e_ref²)` — big
  innovation → forget faster → track; small → smooth. **λ_max held < 1** so P never collapses to zero and
  falls asleep (the classic RLS windup trap — found and fixed during the build). φ≡1 → an adaptive EWMA
  smoother. Verified: converges to a noisy mean + a clean regression parameter; VFF tracks a step faster
  than a slow filter; φ=0 leaves θ unchanged.
- **`pure/SelfCal.cs`** — the concurrent estimator bank (multiple compact RLS, §10): **thrust** F = a·m
  (VFF tracks throttle/stage/mode); **ballistic β** from dragAccel = q·(1/β); **control effectiveness**
  1/I from α = τ·(1/I) (+ `TorqueFor`); **entry L/D** smoothed from `Trajectory.MeasureAero`; and the
  **steering sign/scale** guard — response = g·command exposes the SIGN, so a flipped-frame steering error
  (which has cost real flights) is DETECTED (`SteerSign` → −1), not hand-tuned. Verified: every estimator
  recovers its known value; thrust tracks a throttle-down; the flip guard fires.
- Test `test/SelfCalTest.cs` (16 checks).

**S7 (L7 INSTRUMENTATION) — DONE 2026-08-26. Fresh (INSTRUMENT-EVERYTHING rule).** All green (17 checks),
DLL builds. Every controller's internals into a per-flight CSV, same pass, more columns not fewer.
- **`pure/FlightRecorder.cs`** — the pure recorder core: a 60-column ordered **SCHEMA** (single source of
  truth; the named indices are LOOKED UP from it so they can never drift), **invariant-culture formatting**
  (⛔ never a locale comma — a European locale would write "1,5" and shred the CSV), CSV escaping, blank
  cells for unset values, and a **`Put*` filler per controller that takes the controller's ACTUAL command
  struct** (PutMode/PutGate/PutNav/PutControl/PutAscent/PutBooster/PutRendezvous/PutDocking/PutReturn/
  PutDv/PutFdir/PutSelfCal) so recording is never ad-hoc. Columns span mode+gate, nav, control, UPFG,
  booster, rendezvous, docking, return (bank/CoM-descent/chutes), planned-vs-delivered Δv, the FDIR
  fault+recovery+abort, and the self-cal estimates. Glue samples each tick (NewRow → fillers → append
  Row) and owns the file I/O.
- Test `test/FlightRecorderTest.cs` (17 checks): schema width + drift-proof indices, invariant formatting,
  escaping, and every filler landing its values in the right columns (incl. bank rad→deg, β=1/InvBeta).

**✅ ALL GUIDANCE + AUTOPILOT LAYERS L0–L7 COMPLETE, fresh, headless-green (≈900+ checks across the
rebuilt suites).** What remains is the **KSP GLUE** (not headless-testable — needs the game): wire the
pure stack to the live vessel — replace the `_AutopilotStub`/`_DeorbitStub` members with real controllers
driven by `FlightDriver.Tick()`, feed each L3 controller the measured vessel state, actuate BY CAPABILITY
from the craft dump (engine modes, Dracos, nose shroud, CoM shifter, trunk/decouplers, abort action
group), run `CrewProcedureOps` off `ModeManager`+`CrewGate`+`CrewGates` with live auto-items, tick `Fdir`
+ `AbortResponder` + `SelfCal`, and write the `FlightRecorder` CSV — then the in-game proving flight
(Crew-2 launch → splashdown, crew working the gates). Then iterate one change per flight against the
recording, no Python sims.

### GLUE — built in SEAMS, each in-game-verifiable on its own (DLL green at every step)
The glue is not headless-testable, so it is built one verifiable seam at a time and handed over for an
in-game check before the next seam layers on it (the project rule: the glue is where bugs live; one change
per flight; instrument; never fly blind).

**GLUE SEAM 1 — host + conductor + log + countdown→launch — DONE 2026-08-26. DLL builds (212.5 KB).**
- **`src/FlightDriver.cs`** — the autopilot host, a **`[KSPAddon(Flight)]`** (flight-scene, so it survives
  the active-vessel switch a booster handover performs — the reason it must NOT live on the IVA screens,
  which are destroyed on handover). FixedUpdate: tick the conductor, perform the seam's single actuation
  (ignition on the launch GO via `StageManager.ActivateNextStage`), hand an ABORT to `AbortResponder` (→
  the stock Abort action group = SuperDraco escape), write the log. Only acts while AUTO SEQUENCE is
  engaged. Defensive (a glue fault logs + carries on).
- **`src/CrewProcedureOps.cs`** — the REAL conductor (stub removed): resolves the mission from the VAB
  craft name, walks `ModeManager.Plan`, satisfies each gate's AUTO items from live vessel state (crew taps
  the CrewAck items), runs `CrewGate.Step` → the `GatePhase` the screens render, advances on the crew's GO,
  latches ABORT + the launch intent. Screens read it unchanged (VesselData → GateCard); taps route in
  (ScreenPainter).
- **`src/FlightLog.cs`** — the CSV writer: one file per flight under `DragonScreen_capture/`, header from
  `FlightRecorder`, a row at 4 Hz (time + nav + gate/mode now; the flying controllers fill their columns as
  they land).
- **IN-GAME CHECK for seam 1** (the user): on the pad, press AUTO SEQUENCE → the countdown checklist runs
  (G1..G7), crew work the CrewAck items + give GO at each gate, NO-GO holds, and the launch GO ignites;
  a `<craft>_<stamp>.csv` appears in `DragonScreen_capture/`.

**GLUE SEAM 2 — ascent — DONE 2026-08-26. DLL builds (218.5 KB), installed.**
- **`src/Steering.cs`** — the inner attitude servo + world-direction math the flying controllers share.
  FIRST-CUT inner loop = **stock SAS target-hold** (`v.Autopilot.SAS.SetTargetOrientation`), which drives
  whatever authority the vehicle has (S1 gimbal, the Dracos) — so the first flights validate the GUIDANCE
  (ours) without a bespoke steering controller under it. `PitchHeadingDir` (ENU from the body), `Prograde`,
  `PointingErrorDeg`. The pure `ControlLaw`+`Authority` attitude loop is the clean later swap here.
- **`src/AscentControl.cs`** — flies launch→orbit with the PURE guidance: S1 pitch program on the
  `LaunchAzimuth` heading, max-Q bucket + g-limit throttle (`Ascent.Guide`), MECO→stage, S2 ignition,
  closed-loop **UPFG** thrust vector to a circular insertion (`Upfg.Step`), SECO on measured periapsis
  (‖ UPFG tgo→0), fires the **Dragon decoupler** (drops S2 alone — VehicleParts), then
  `CrewProcedureOps.PhaseComplete()` hands back to the conductor. Instrumented every tick (recorder ascent
  columns + SelfCal thrust from an INDEPENDENT felt-accel measurement).
- **`src/FlightDriver.cs`** — now owns `Vessel.OnFlyByWire` (throttle authority, follows handover) and
  dispatches the active phase (`DriveActivePhase` → AscentControl for Ascent); **`src/FlightLog.cs`** gained
  a `Fill` hook so the active controller adds its columns to each CSV row.
- **⚠ VALIDATE IN FLIGHT (first cut, not headless-testable):** S1 pitch program is the reliable part;
  the S2 UPFG **Iy plane normal** (currently −(r×v), in-plane assumption), the SECO cutoff, the ENU heading
  SIGN, and the staging order (MECO sep / S2 ignite / Dragon sep via `StageManager`) are the things to
  confirm against the recording and fix ONE change per flight. `SelfCal.SteerSign` is the guard for a
  flipped heading.
- **IN-GAME CHECK for seam 2:** AUTO SEQUENCE → countdown → launch; the vehicle flies the pitch-program
  gravity turn on the target-inclination heading, throttles through max-Q, stages at MECO, lights S2, flies
  UPFG to a ~circular orbit, cuts, and separates the Dragon; the CSV carries the ascent columns.
**GLUE SEAM 3 — booster recovery — DONE 2026-08-26. DLL builds (223.0 KB), installed.**
- **`src/BoosterControl.cs`** — flies the SEPARATED first stage with the pure guidance (`BoosterDescent`
  FSM + `Hoverslam` + `GridFin`). Runs when the active vessel `IsRecoverableBooster` (S1 parts, NO Dragon
  pod, airborne) — i.e. after the player focuses the booster post-sep (KSP only fully sims the focused
  craft; the FlightDriver KSPAddon persists across the switch). Flip → entry burn (ThreeLanding) → aero
  descent → hoverslam on CenterOnly. **⛔ engine modes selected ABSOLUTELY by Activating the matching-
  engineID `ModuleEngines` while off, shutting the rest, only on a MODE CHANGE** (so a lit mode is never
  re-ignited — one ignition per octaweb mode; NEVER NextEngineMode). Grid fins + legs deploy by capability.
  Instrumented (booster recorder columns). Attitude on the SAS inner loop (gimbal when lit; cold-gas/fins
  otherwise). FlightDriver throttle authority generalised (`SetThrottle`/`ReleaseThrottle`) so ascent +
  booster share it; a lone booster is dispatched before the mission conductor.
- **BOOSTER TARGETING refinement — DONE 2026-08-26** (user: droneship placed + Cape Canaveral RTLS pads).
  **`src/BoosterTargeting.cs`** runs the L1 pure impact predictor (`Trajectory.Solve` with a MEASURED
  ballistic coefficient — measured while coasting, engine off, from the felt drag decel + the body's live
  density) to a predicted touchdown, ROTATION-CORRECTS it (rotate the inertial impact back by the body
  rotation over the fall into the current body-fixed frame — the body turns tens of km under a multi-minute
  descent), and compares to the landing TARGET (the booster's explicit target if set — the crew can target
  the droneship or an RTLS pad — else auto-finds a droneship vessel by the `Droneship` part marker). The
  down/cross-range error feeds `GridFin`, so the fins STEER the predicted impact onto the deck; closed-loop
  (re-predicted each tick), and `AllNominal` aims at the deck only once a target is found (else retrograde
  hold). ⚠ validate in flight: the BC measurement, the rotation-correction sign, and the cross-range sign
  (`CrossSign` — a mirrored steer is a one-constant fix).
- **⚠ STILL VALIDATE IN FLIGHT (first cut):** the hoverslam ignition altitude, the engine-mode
  Activate/Shutdown behaviour on the real octaweb, and the booster's controllability (needs its own
  avionics/probe core).
- **IN-GAME CHECK for seam 3:** after MECO/sep, focus the booster (`[`/`]`); it flips engines-first, runs
  the entry burn on 3 engines, holds retrograde through the aero descent on the grid fins, and hoverslams
  to a stop on the centre engine with legs down; a booster CSV appears.
**GLUE SEAM 4 — rendezvous — DONE 2026-08-26. DLL builds (225.5 KB), installed.**
- **`src/RendezvousControl.cs`** — flies the Fly(Phasing) step (post-insertion orbit → ~7.5 km AI standoff)
  with the pure guidance: projects the chaser into the station's LVLH frame (`Lvlh.Project` off the
  targeted station's orbit), `Rendezvous.Guide` gives the named-burn Δv, executed on the **Dracos**.
  ⛔ ATTITUDE-FIRST-THEN-TRANSLATE (no reaction wheels): point the nose ALONG the burn (SAS), and only once
  `AttitudeReady` translate forward to deliver the residual Δv (closed-loop — the CW solve re-runs each
  tick). **Opens the nose shroud before any Draco burn** (exposes the forward Dracos + port) and turns RCS
  on. Hands back at the AI standoff → the G9 gate. FlightDriver gained RCS **translation authority**
  (`SetTranslation`/`ReleaseTranslation`) alongside the throttle, applied through OnFlyByWire.
- **⚠ VALIDATE IN FLIGHT (first cut):** the RCS translation axis/sign (`ForwardSign`, default −1 — a
  mirrored burn shows in the CSV, one-constant fix), and the named-burn execution across the long phasing
  coast (the crew time-warps between burns).
- **IN-GAME CHECK for seam 4:** on orbit, target the station; the capsule opens its nose shroud, points
  along each phasing burn and translates to deliver it on the Dracos, walking the range down toward
  ~7.5 km, where it hands to the G9 GO-for-AI gate.
**GLUE SEAM 5 — docking — DONE 2026-08-26. DLL builds (229.5 KB), installed.**
- **`src/DockingControl.cs`** — flies the L-approach to soft capture with the pure `DockControl` glideslope
  servo. The mode manager holds WP0/WP1/WP2 as the G10/G11/G12 CREW GATES, so this flies ONE leg at a time:
  it reads `CrewProcedureOps.NextGateId` (G10→WP0 400 m below, G11→WP1 ~220 m front, G12→WP2 20 m, else→
  contact), drives `DockControl.Translate` to that LVLH point on the **Dracos** (world demand decomposed
  into control-frame X/Y/Z), points the docking ring at the port (SAS), and `PhaseComplete`s at each
  waypoint so the crew's GO at the gate releases the next leg. Closing-speed cap tapers to ~8 cm/s at
  contact; capture detected via `DockedSide.Docked`. `CrewProcedureOps` gained `NextGateId`.
- **⚠ VALIDATE IN FLIGHT (first cut):** the RCS translation axis SIGNS (`RcsRight/Up/FwdSign`), the servo
  gains (`KPos`/`KVel`/`FarSpeed`), the arrival tolerances. KOS-breach auto-abort not wired yet (the crew's
  ABORT on the gate → the responder is the current safety path).
- **IN-GAME CHECK for seam 5:** after the G9 GO, the capsule flies the glideslope to WP0 (holds for the G10
  GO), then WP1 (G11), WP2 (G12), then closes to contact and soft-captures, each leg released by the crew's
  GO.
**GLUE SEAM 6 — return — DONE 2026-08-26. DLL builds (235.0 KB), installed. LAST SEAM.**
- **`src/ReturnControl.cs`** — the whole back half, dispatched by phase: return **Phasing** → FlyDeparture
  (undock the docking node, then `Departure` CW burns on the Dracos to the co-elliptic point below the
  station + the phasing burn); **Entry** → FlyDeorbitEntry (`DeorbitGuidance`: trunk jettison → close the
  nose shroud → retrograde Draco deorbit burn — point retrograde + forward-translate — cut on measured Pe;
  then the lifting entry: ⛔ **ENGAGE the CoM shifter Descent Mode ONCE via the AdjustableCoMShifter
  `ToggleMode` event** — the correct use, a mode not a steering actuator — and hold shield-forward);
  **Drogues/Mains/Splashdown** → FlyChutes (`Chutes` state-based drogue/main `ModuleParachute.Deploy` →
  splashdown → mission complete). `CrewProcedureOps` gained `IsReturn` (set when G14 clears) so the return
  Phasing routes to departure, not the outbound rendezvous.
- **BANK-ANGLE ENTRY STEERING — DONE 2026-08-26.** **`src/EntrySteering.cs`** provides the two live inputs
  the pure `Entry` guidance needs: (1) the predicted FOOTPRINT error — the L1 `Trajectory` predictor run
  WITH LIFT (L/D ~0.2, banked by the current σ) + a measured ballistic coefficient, rotation-corrected,
  vs the splashdown TARGET (the capsule's target — a recovery ship / waypoint the crew sets); and (2) the
  MEASURED BANK (the roll about the velocity axis vs lift-up). `Entry.Guide` sets |σ| to null downrange +
  reverses sign on the crossrange deadband (the S-turns). `FlightDriver` gained a **roll authority**
  (`SetRoll`/`ReleaseRoll` on `st.roll`); ReturnControl's entry runs the **roll loop** — SAS holds the
  nose retrograde (shield-forward), the loop banks to σ via `st.roll` (SAS's direction-only target leaves
  roll free). No target → nominal reference bank (still a stable lifting entry).
- **⚠ STILL VALIDATE IN FLIGHT (first cut):** the departure/deorbit RCS translation sign, the deorbit
  target Pe + cutoff, undock/trunk/CoM-shifter actuation, and the entry **bank/roll SIGNS** (`RollSign`,
  `EntrySteering.RollRefSign`/`CrossSign` — a mirrored bank/steer is a one-constant fix) + the roll gain
  (`RollKp`). Refinement beyond first cut: roll-ONLY entry control (let the capsule trim AoA aerodynamically
  rather than SAS holding exact retrograde).
- **IN-GAME CHECK for seam 6:** after G13/undock GO, the capsule backs away and runs the departure burns;
  after the G15 deorbit GO it jettisons the trunk, burns retrograde on the Dracos, engages the CoM shifter,
  enters shield-forward, and deploys drogues then mains to splashdown.

**✅ ALL GLUE SEAMS COMPLETE (1–6 + booster targeting) — the full Crew-2 mission is wired, pad →
splashdown, first-cut, installed.** What remains is entirely IN-GAME: fly it, read the FlightRecorder CSVs,
and tune the flagged first-cut items ONE change per flight (no Python sims). The known refinements queued:
bank-angle entry steering, the RCS translation signs, UPFG Iy/SECO, booster BC/rotation/cross signs, and
the pure ControlLaw+Authority attitude loop replacing the SAS inner loop.

**S0(c) — DONE 2026-08-26:** live `ModuleManager.ConfigCache` read (30 MB / 1.37 M lines; S1 @ L1135471,
S2 @ L1139519, Draco @ L1145419); real Merlin/MVac/Draco numbers pinned into §5.0 with two corrections
(ignitions = −1 not 4; spool instant not 1.2 s). **Remaining S0:** (d) booster geometry (`BoosterHeightM`)
+ per-engine confirm from a craft dump. Then S0b (Mission Profile + VAB-name resolver).

### DEMOLITION — DONE 2026-08-26 (user: delete the whole autopilot, keep the screens)
The entire autopilot was removed and the build is **green** (screens only):
- **116 files moved** to `plugin/_deleted_autopilot/{pure,src,test}` — all guidance/control/nav/mode/FDIR
  pure, all autopilot glue controllers, `FlightDriver`, the L0 math core, and the autopilot tests. In git
  history (reversible); the verified L0 suites re-enter from `_deleted_autopilot/test/` with their layer.
- **KEPT + compiling:** 29 screen/infra pure + 14 screen glue → `DragonScreen.dll` builds (50 files); screen
  test suites pass (layout 277 / sweep 2481 / page 545 / panel 207 / orbital 158 / octaweb 13).
- **Seam repairs (minimal, screens untouched):** restored the display helpers `ArcGeometry`, `PanelMap`,
  and the life-support trio `LifeSupport`/`LifeSupportBridge`/`DockedSide` (ECLSS display, no autopilot
  deps); moved the `GatePhase` display enum into `pure/MissionPhase.cs`; trimmed the moved-guidance test
  bodies out of `PanelTest`/`OrbitalTest` and re-pointed `TestMain` at the screen suites.
- **TWO TEMPORARY STUBS** hold the screen↔autopilot seam idle: `src/_AutopilotStub.cs` (FlightCommands,
  CrewProcedureOps + Gate/ProcState/ItemKind, MissionOps, AutoPilot/StationApproach/DockingOps/DeorbitOps/
  UndockOps `.Engaged`+`.Note`, BoosterRecovery.Tracked) and `src/pure/_DeorbitStub.cs`
  (Deorbit.LandingThrottle). **Delete each stub member as its real controller is rebuilt** and wire the
  screen to the real class. The stubs are the exact list of screen→autopilot dependencies to satisfy.

**Clean slate reached.** Next: S0(c/d) ground-truth numbers → S0b Mission Profile + name resolver → L1.

## 9. DECISION — resolved 2026-08-26 (superseded → full delete-and-rebuild)

**The user (2026-08-26): "nothing has worked correctly since we started building the RO version. Do a full
ground-up rebuild. Delete the whole autopilot and start from scratch. Keep the screen part."** So this is a
**from-scratch rebuild**, not verify-then-reuse-in-place:

- **DELETE the entire autopilot** — all guidance/control/nav-derived/mode-manager/FDIR pure modules, all
  autopilot glue controllers, `FlightDriver`, and the autopilot tests. Moved out of the build to
  `plugin/_deleted_autopilot/` (and it is in git history — reversible).
- **KEEP the screens** — all pages/rendering/UI (`ScreenPainter`, `ScreenTouch`, `DragonScreenMonitor/State`,
  the `pure/*Page*`, `Gauge`, `Card`, `DisplayList`, `Readouts`, cameras, `PanelButtons/Map`, palette,
  typography) + shared infra (`FlightRecorder`, `Tuning`, `CraftDump`). Screen↔autopilot seams are stubbed
  to idle until the rebuild reconnects them.
- **The verified L0 math is NOT thrown away** — it is in git; each primitive (Kepler/UPFG/CW/Hohmann/
  Hoverslam/FuelFlow/V3) is **re-introduced from history when its layer is reached, re-running its verified
  test** (§8b already proved each). "From scratch" = the autopilot tree starts empty and every piece is
  re-added deliberately, verified, per the build order — nothing inherited silently, no tuned constant
  carried over (§5 supplies fresh values).

**The build:** delete → screens compile green → then rebuild bottom-up S0b → L1 … per §7, actuating by
capability from the craft dump and steering to the research-derived constants.
