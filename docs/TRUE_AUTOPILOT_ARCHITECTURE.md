# How to build a true autopilot in KSP — a Crew-Dragon-equivalent, complete

Purpose: a **complete, standalone build guide** for a genuine autonomous spacecraft autopilot — the
equivalent of the flight software SpaceX/NASA fly on Crew Dragon — **implementable in a live KSP (RSS/RO)
build**. It records, from trusted sources, *what* to build and *how* each piece is actually done in KSP.
It is written to stand on its own: read it and you know how to build the system. It deliberately does
**not** describe any existing implementation.

Tags: **[P]** cited primary/authoritative · **[E]** established/widely-documented · **[D]** design
principle · **[K]** KSP implementation path (engineering method, not a sourced fact).

---

## 0. The definition — AUTONOMY, not automation

- **Automation** executes a pre-planned sequence and cannot handle the unforeseen.
- **Autonomy** senses the real state, **decides for itself, notices a failing plan, re-plans or safes
  itself** — no human/ground in the loop. Orion's requirement to *return crew safely without comms*
  "drove much more autonomy and automation than just operational navigation." [P]
- **A true autopilot owns the decision, not just the execution.** Every section below serves that. [D]

Sources: [Orion GN&C for Increased Automation & Autonomy — AIAA 2008‑7291](https://arc.aiaa.org/doi/10.2514/6.2008-7291),
[Retrospective on Orion GN&C Design — NASA NTRS](https://ntrs.nasa.gov/api/citations/20240001457/downloads/AAS_24_171%20Retrospective%20on%20Orion%20GNC%20Design%20v1.pdf).

---

## 1. The architecture — five parts, ticked every frame

Real flight software is a pipeline of separable functions plus a manager and a safety spine:

```
  MISSION / MODE MANAGER  (master FSM + phase FSMs; owns HOLD/ABORT/takeover)
        │ engages phase controller            ▲ failsafe from FDIR
        ▼                                      │
  GUIDANCE (plan, closed-loop, re-plan) ◀────▶ FDIR (detect→isolate→recover ladder)
        │ command                              ▲ residuals
        ▼                                      │
  CONTROL (fast, closed-loop, fault-contained) │
        │ actuate                              │
        ▼                                      │
  NAVIGATION (state) ── in KSP = ground truth ─┘
        │
  RECORDER (every decision/target/residual/verdict, every tick)
```

- **Multi-rate:** control runs every physics frame (~tens of Hz); guidance/replanning less often;
  the mode manager on a slow cadence. "The breadth of algorithm types drives a multi-rate architecture."
  [P]
- Real vehicles run this on **triple-redundant, vote-based flight computers** (Crew Dragon: ordinary
  computers checking each other, self-rebooting on radiation — proven CRS‑1 2012; software in **C++**). [P]
- **[K] In KSP** you have one deterministic process and perfect state, so redundancy/voting is not needed.
  The principle that transfers: **a fault must never crash the flight** — wrap every controller so an
  exception detaches it and is logged, and re-validate all state on scene/vessel change.

Sources: [Orion GN&C Flight Software — Tech Briefs](https://www.techbriefs.com/component/content/article/28025-msc-25615-1),
[Dragon Flight Computer — Space Launches Live](https://spacelaunchlive.com/avionics/dragon-flight-computer/),
[SpaceX flight software — Stack Overflow](https://stackoverflow.blog/2021/12/27/dont-push-that-button-exploring-the-software-that-flies-spacex-starships/).

---

## 2. The KSP control interface — how you actually command a vessel

This is the foundation everything actuates through. [P — KSP API]

- **`FlightCtrlState`** is the object that controls a vessel; writing it **overwrites user input** (that
  IS autopiloting). Fields: `pitch`, `yaw`, `roll` (rotation, −1..1), `mainThrottle` (0..1),
  `X`, `Y`, `Z` (RCS translation, −1..1), `wheelThrottle`/`wheelSteer`.
- **`Vessel.OnFlyByWire += callback(FlightCtrlState s)`** is the **only per-vessel write point** —
  register a delegate and set the axes there every FixedUpdate. This is per-vessel, so **you can fly two
  vessels at once** (e.g. booster + upper stage) by attaching a controller to each. [K]
- **Turn stock SAS off** on any vessel you drive (`v.ActionGroups.SetGroup(KSPActionGroup.SAS, false)`),
  or two controllers fight for the same axes. [K]
- **Actuators are part modules**, driven directly by capability (not by staging/action groups) for
  precise control:
  - Engines: `ModuleEngines(RF)` — `Activate()`/`Shutdown()`, `thrustPercentage`, read `finalThrust`.
  - Gimbal: `ModuleGimbal.gimbalLimiter` (0..100), `gimbalLock`.
  - RCS: `ModuleRCS.thrustPercentage` (dial authority per task), `thrusterPower`.
  - Control surfaces / grid fins: the `authorityLimiter` field.
  - Decouplers/chutes/etc.: fire the part's own `BaseEvent`/`BaseAction` by name.
- **Frames & the swizzle:** KSP is left-handed and uses a `xzy` swizzle between world and orbit frames;
  a command part's transform has **+Y out of the nose**, so a control law working in
  forward/top/starboard (+Z forward) needs a fixed **−90° about X** correction. Get the frame wrong and
  every axis permutes silently. [K]
- **Time warp:** on-rails warp freezes physics — guidance that must burn/steer has to drop to real time
  first; anything time-based must integrate real `dt`, not tick counts. [K]

Sources: [API:FlightCtrlState — KSP Wiki](https://wiki.kerbalspaceprogram.com/wiki/API:FlightCtrlState),
[VesselAutopilot class — KSP API](https://kerbalspaceprogram.com/api/class_vessel_autopilot.html),
[kOS autopilot mod](https://github.com/KSP-KOS/KOS),
[AtmosphereAutopilot plugin](https://github.com/Boris-Barboris/AtmosphereAutopilot).

---

## 3. NAVIGATION (state) — trivial truth + derived quantities

- **In KSP navigation is free:** the game hands exact position, velocity, orbit, attitude and rates. No
  Kalman/relative-nav filters are needed (a real vehicle fuses IMU/GPS/star-tracker/LIDAR to *estimate*
  what KSP gives you directly). [K]
- **But a real autopilot still needs DERIVED quantities the game does not hand you:**
  - **Dynamic pressure** `q = ½·ρ·v²` (for max-Q throttle and aero limits).
  - **Predicted impact point** — integrate the trajectory forward under gravity + a **drag model**
    (RK4), not a vacuum parabola, or the prediction swings tens of metres per m/s. This drives booster
    aim and entry footprint.
  - **Ballistic coefficient** `β = m/(Cd·A)` — measure it live from the actual deceleration so the impact
    predictor tracks the real vehicle.
  - **Available torque / thrust per axis** — sum from the parts (gimbal, RCS, fins) so the control law
    knows its own authority as the vehicle stages.
- **[K] Build these as pure functions** of the KSP state, tested headless, so the guidance always steers
  on the *same* number it reports.

Sources: [Survey of LIDAR for spacecraft relative navigation (what KSP replaces) — NASA NTRS](https://ntrs.nasa.gov/archive/nasa/casi.ntrs.nasa.gov/20140000616.pdf).

---

## 4. CONTROL — the attitude & effector laws

### 4.1 Attitude control (point the vehicle)
- **Standard real-world law: quaternion/vector-feedback PD.** error → rate command → torque command →
  actuation. Quaternion-feedback PD regulators "robustly track trajectories in real time, enabling
  efficient onboard implementation." [P]
- **The one idea that matters — never command a rate you cannot arrest.** Bound the commanded rate by the
  **braking curve** `ω_max = √(2·α·θ)` (α = torque/inertia, θ = angle error) — the same law a hoverslam
  uses. It falls out of the vehicle's *own* torque and inertia, so it is automatically right for a full
  stack, a spent booster or a capsule, with **no per-phase gain tuning**. [D]
- **Actuator saturation is a real design concern** — clamp actuation to [−1,1] and slew-limit its rate so
  a step command cannot slam; handle the case where authority is momentarily zero (coast, no gimbal). [P]
- **[K] In KSP** the actuation fractions are written to `s.pitch/yaw/roll`; authority is measured by
  summing gimbal + RCS + control-surface torque from the parts. **RO strips reaction wheels**, so RCS/
  gimbal are the only authority — the control law must turn RCS *on* when it is the sole authority.

### 4.2 Effector laws
- **Throttle:** the guidance sets it; overlay a **max-Q bucket** (throttle down through peak q) and a
  **g-limit** (throttle = g_limit·m / F_full) for crew; take the more restrictive.
- **Gimbal / RCS / fins** are commanded through the same rotational actuation; **RCS translation** is a
  separate `s.X/Y/Z` for orbital burns on a capsule with no main engine.

Sources: [Quaternion-feedback PD attitude control — IEEE](https://ieeexplore.ieee.org/document/8002994/),
[Attitude control with actuator saturation — ScienceDirect](https://www.sciencedirect.com/science/article/abs/pii/S0967066102002162).

---

## 5. GUIDANCE — the plan for each phase (closed-loop, re-solved)

Guidance is a **predictor–corrector that re-solves each cycle from the measured state to the target
constraints** — it never trusts a stored answer. [D] The per-phase laws:

- **Ascent → orbit:** first stage = **open-loop pitch program / gravity turn** (zero-AoA load relief);
  second stage = **closed-loop PEG/UPFG** (linear-tangent steering `tan β = A·t + B` wrapped in a
  predictor-corrector; thrust integrals L/J/S/Q; conic-state gravity; cutoff on target velocity/radius/
  flight-path-angle/plane). *Full math in `LAUNCH_AND_ASCENT_RESEARCH.md`.* [P]
- **Booster recovery:** flip to retrograde → **entry burn** (shed velocity, protect from heating) →
  **aerodynamic descent** steered by grid fins (angle-of-attack + bank to walk the impact point) →
  **landing burn**. The landing burn is a **fuel-optimal powered descent**: either the analytic
  **hoverslam** (constant-max-thrust brake sized to hit v=0 at h=0, `t_brake` from the braking curve), or
  the real fuel-optimal method — **convex optimization via lossless convexification** (Açıkmeşe &
  Blackmore, *Automatica* 2011: reformulate the nonconvex min/max-thrust + pointing problem as a
  **Second-Order Cone Program**), realised as **G-FOLD** (Fuel Optimal Large Divert), flight-tested for
  pinpoint landing. [P]
- **Rendezvous:** relative motion near a target is the **Clohessy–Wiltshire (Hill) equations** in the
  target's LVLH frame; a **two-impulse CW transfer** (solve the state-transition matrix for the Δv that
  puts you at the aim point in time T) plus **Hohmann/Lambert** phasing for the large orbit changes.
  Aim at an **offset point** so a dispersed/failed burn misses the keep-out sphere (offset targeting is
  mandatory). [D/E]
- **Entry:** hold the trim **angle of attack** (offset CoM gives lift, L/D ~0.2) and steer by
  **bank-angle modulation** — roll the lift vector; vertical lift = L·cos(bank) controls range, bank sign
  controls cross-range (Apollo/Orion method). [P — recorded in the mission-techniques research]
- **Deorbit:** a targeted retrograde burn solved to put periapsis in the entry corridor for the
  splashdown footprint.
- **Constraint enforcement is part of guidance:** every candidate plan is checked against propellant,
  keep-out zones, g/heat limits and a **periapsis floor** *before* it is committed. [D]

Sources: [Lossless convexification of powered-descent guidance — Açıkmeşe & Blackmore](https://www.researchgate.net/publication/224253864_Lossless_convexification_of_Powered-Descent_Guidance_with_non-convex_thrust_bound_and_pointing_constraints),
[Convex optimization for vehicle guidance & control — survey (arXiv)](https://arxiv.org/pdf/2311.05115),
[Powered Explicit Guidance — OrbiterWiki](https://www.orbiterwiki.org/wiki/Powered_Explicit_Guidance).

---

## 6. The math toolbox (build/verify these first)

A true autopilot rests on a small, correct numerical core — build and **headless-test each before it
flies**: [D]

- **Kepler / conic propagation** (universal-variable, Stumpff functions) — future state on an orbit
  without integrating; used by PEG gravity and warp targeting.
- **Vis-viva** `v² = μ(2/r − 1/a)` — orbital speeds, circularise/insertion Δv.
- **Lambert solver** — the two-point boundary transfer (rendezvous/return).
- **Clohessy–Wiltshire state transition** — relative-motion targeting near a station.
- **RK4 numerical integration** with a drag model — impact/entry prediction.
- **Quaternion / vector algebra** and the KSP frame transforms (§2).
- **Rocket equation** `Δv = ve·ln(m0/mf)` and a fuel-flow model — Δv budgets, burn times, staging.

---

## 7. MISSION / MODE MANAGER — the sequencer that owns the flight

- **A hierarchy of finite-state machines:** each flight phase (ascent, orbit, rendezvous, entry) is its
  own FSM, coordinated by a **high-level master FSM**. [P]
- **Data-driven** — the mission (targets, gates) is *data*, not new code, so one engine flies any
  mission profile and the sequence stays flexible. [P]
- **Transitions fire on three sources only:** an **operator/crew command**, an **autonomous trigger** (a
  condition the vehicle measures), or a **failsafe event** (from FDIR). Never a bare clock. [P]
- **[K] Build it** as one master conductor that engages each phase's guidance/control, advancing on
  *measured state* + *crew GO gates* + *FDIR*, and owning **HOLD / ABORT / manual-takeover**.

Sources: [State-Machine Fault-Protection Architecture for GN&C — AIAA JAIS](https://arc.aiaa.org/doi/abs/10.2514/1.I010673),
[Orion data-driven FSW for automated sequencing & fault recovery — IEEE](https://ieeexplore.ieee.org/document/5446722/).

---

## 8. ONBOARD RE-PLANNING — the heart of autonomy

- **Plan onboard from live state, and recompute when reality diverges.** NASA's **CASPER** (Continuous
  Activity Scheduling, Planning, Execution and **Re-planning**) plans, schedules, enforces vehicle
  constraints, and **re-plans continuously**. Orion's **Burn Plan Manager + Two-Level Targeter** generate
  targets onboard for orbit guidance — the most sophisticated autonomous onboard targeting/burn execution
  built. [P]
- **[K] Build re-planning as a normal event, not an exception:** when a monitor reports a stalled/diverged
  plan, abort the stuck node and **re-solve the leg from the measured state**; if it still cannot close,
  **downmode** to a reachable objective (come home). The guidance solvers already re-solve every cycle,
  so re-planning = re-seeding them from the current reality.

Sources: [CASPER onboard re-planning — JPL/AIAA](https://www.jpl.nasa.gov/nmp/st6/TECHNOLOGY/AIAA-12923-137.pdf),
[Orion Two-Level Targeter / Burn Plan Manager — NTRS](https://ntrs.nasa.gov/api/citations/20240001457/downloads/AAS_24_171%20Retrospective%20on%20Orion%20GNC%20Design%20v1.pdf).

---

## 9. FDIR — Fault Detection, Isolation, Recovery (the safety spine)

- **Three steps, always:** **Detect** (a residual = expected − actual crosses a threshold) → **Isolate**
  (which system, what fault kind) → **Recover** (fire the right response); classic form = **event-action
  tables** (ESA/NASA standard). [P]
- **Debounce, or it flaps:** require the fault to persist for a **confirmation time** and clear on a
  separate, lower **hysteresis** threshold — and use **elapsed real time (dt), not tick counts**, so it
  means the same thing under warp/at any frame rate. [D]
- **Recover on a least-intervention LADDER** (model-based-FDIR / NASA-ESA hierarchy):
  `Continue → Retry → Reconfigure → Replan → Downmode → Abort/Safe-mode`. Try the cheap local fix first;
  fall to safe mode only when nothing local can hold the mission. [D]
- **Safe mode** = a known-safe hold (power-positive, thermally safe, comms) as the last resort. **Redundancy
  management** switches away from a failed sensor/string. Modern autonomy resolves FDIR **onboard, in a
  scalable hierarchical fashion.** [P]
- **[K] Build:** one shared **detect+debounce monitor** primitive, a **fault→recovery decision table**,
  and concrete monitors — **thrust delivery** (expected vs delivered Δv-rate), **trajectory divergence**
  (error growing), **convergence stall** (a plan not progressing), **resource critical** (propellant/heat
  margin), **no-control-solution**, **keep-out breach**. Make **abort-to-home the guaranteed floor** so
  the crew always lands.

Sources: [Survey on FDIR Strategies in the Space Domain — AIAA JAIS](https://arc.aiaa.org/doi/10.2514/1.I010307),
[State-Machine Fault-Protection Architecture — AIAA JAIS](https://arc.aiaa.org/doi/abs/10.2514/1.I010673).

---

## 10. IN-FLIGHT SELF-CALIBRATION — the piece that kills tuned constants

This is what makes it *never need a human between flights*, the last step from "good autopilot" to "true
autopilot":

- **Estimate the vehicle's own parameters in flight instead of hand-tuning them.** The standard method is
  **Recursive Least Squares (RLS) with a variable forgetting factor** — proven onboard for **spacecraft
  mass-property identification** and **disturbance-torque estimation** (converges in <2 min for higher
  forgetting factors). **Multiple concurrent RLS** segments unknown parameters into groups solvable by
  fast, compact linear algorithms. **Adaptive attitude control with inertia-matrix identification** tracks
  the true dynamics as they change. [P]
- **[K] What to estimate live** (replacing tuned constants): actual **thrust & Isp** (from measured
  accel × mass), **mass**, **ballistic coefficient / drag**, **control effectiveness / available torque**,
  **entry lift & trim (L/D)**, and steering **sign/scale** — feed the estimate back into the guidance so
  the constants become *measurements*, not guesses.

Sources: [Online spacecraft mass-property identification via concurrent RLS — NASA NTRS](https://ntrs.nasa.gov/citations/20080008654),
[Adaptive attitude tracking with inertia identification — AIAA JGCD](https://arc.aiaa.org/doi/10.2514/2.4310),
[Disturbance-torque estimation via RLS with variable forgetting factor](https://www.academia.edu/142978533/Adaptive_Disturbance_Torque_Estimation_for_Orbiting_Spacecraft_Using_Recursive_Least_Squares_Methods).

---

## 11. Autonomous Rendezvous & Docking + crewed autonomy

- **AR&D structure** (the guidance/control stays even though KSP hands you the exact relative state):
  range-layered phases, a **relative-navigation** step (real vehicles fuse long-range RF/IR/star-tracker →
  **flash LIDAR (DragonEye: range/velocity/bearing)** → terminal vision), **offset targeting**, **waypoint
  holds with GO gates**, and **automatic abort on any keep-out breach**. [P]
- **Crewed autonomy:** the vehicle flies itself between decision points; the crew **monitors, polls, gives
  GO** at the real gates and retains **HOLD/ABORT/manual-takeover**. Automation on a manned vehicle is
  deliberately share-able and lets operators build trust; FDIR must **never mistake an intended GO-gate
  hold for a frozen-plan fault.** [P]

Sources: [AR&D sensors — NASA SBIR](https://sbir.gsfc.nasa.gov/content/autonomous-rendezvous-and-docking-sensors),
[Orion automation/autonomy & crew override — AIAA 2008‑7291](https://arc.aiaa.org/doi/10.2514/6.2008-7291).

---

## 12. How to build it CORRECTLY — engineering method

- **Model-based, tested-first.** NASA/Orion build GN&C in Simulink/Stateflow and **auto-generate C**;
  even so, the generated code is **fully tested and certified** (generators are unqualified). Use
  **formal V&V — model checking and static analysis** — for high assurance and early error detection. [P]
- **[K] The KSP-practical equivalent of MBD:**
  1. **Separate pure math (guidance/decision/nav) from KSP glue**; the pure layer IS the model — **test it
     headless** (unit + closed-loop point-mass sims) before it ever flies. This is your certification.
  2. **Instrument everything** — every controller's decisions, targets, residuals and verdicts into a
     flight recorder the same pass you build them; **validate against the recorded flight corpus**, not a
     parallel simulation.
  3. **One change per flight, grounded in the data** — RSS/RO is the only high-fidelity test of the glue,
     so change one thing, fly, read the recording, keep or revert.
  4. **Defensive, deterministic code:** range-check every input, fault-contain every controller,
     constraint-check every plan before commit, and give every failure a defined, tested response.

Sources: [Model-Based Design & Testing for Orion — NASA](https://www.nasa.gov/wp-content/uploads/2016/10/01-03_orion_cre_exploration_vehicle_model_0.pdf),
[Certifying Auto-Generated Flight Code (AUTOCERT) — NASA NTRS](https://ntrs.nasa.gov/citations/20080022231),
[V&V of Autonomy Software at NASA — NTRS](https://ntrs.nasa.gov/archive/nasa/casi.ntrs.nasa.gov/20010000882.pdf).

---

## 13. Completion checklist — "is it a TRUE autopilot?"

1. Every phase flown by a **closed-loop guidance law** re-solved from live state (never a fixed script).
2. **Control** law bounds every rate by what it can arrest — no per-phase gain tuning.
3. A **mode manager** sequences on **measured state + crew GO**, never a clock; owns HOLD/ABORT/takeover.
4. **FDIR that ACTS**: debounced monitors → the recovery ladder → **guaranteed abort-to-home**.
5. **Onboard re-planning** on a detected failure — a normal event, not an exception.
6. **In-flight self-calibration (RLS)** estimates thrust/mass/drag/torque/lift live, so **no hand-tuned
   constant** is trusted across flights.
7. Human retains **HOLD/ABORT/manual-takeover** at the real gates.
8. **Everything instrumented**, pure math **headless-tested**, glue **validated on the flight corpus**.

If all eight hold, the autopilot is autonomous — it senses, decides, notices failure, re-plans, self-
calibrates, and always brings the crew home — the genuine equivalent of the SpaceX/NASA flight software.

---

## Honesty log
- SpaceX's actual Crew Dragon flight-software internals are **not public**; the architecture here is
  reconstructed from NASA Orion / ISS-visiting-vehicle / spacecraft-autonomy literature plus the confirmed
  Crew Dragon facts (triple-redundant voting computers, C++, autonomous docking). Tagged [P]/[E]/[D].
- Algorithm choices (quaternion-PD, lossless-convexification/G-FOLD, PEG/UPFG, CW, RLS) are the
  established real-world methods for each function, cited above; the [K] items are the engineering path to
  realise them in KSP, not sourced facts.