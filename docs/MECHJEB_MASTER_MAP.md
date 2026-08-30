# MECHJEB MASTER MAP — how MechJeb2 works, end-to-end, and how to build a KSP autopilot

> **Purpose:** the single durable map of MechJeb's architecture, read from the source at `Desktop/mechjeb_src`
> (MechJeb2 = KSP glue modules; MechJebLib = pure math/guidance). Built 2026-08-30 by reading the actual
> code, not skimming. This is the entry point; the deep-dive docs it indexes are listed at the bottom.
>
> ⚠ **VERSION NOTE (important):** the desktop `mechjeb_src` is an OLDER snapshot than the **installed** DLL
> (the RO/RP-1 build). The installed build has ascent fields the source lacks (`PitchStartVelocity`,
> `DynamicPressureTrigger`, `StagingTrigger`/`StagingTriggerFlag`, "Burnout Altitude" label instead of
> "Attach Alt"). So the source is authoritative for ARCHITECTURE and ALGORITHMS, but for the exact installed
> ascent-settings behaviour, cross-check the installed `Localization/en-us.cfg` and the live cfg. Port
> LOGIC from the source (per [[mechjeb-source-reference]]); never assume a cfg field means what an older
> source says.

---

## 0. THE ONE-PARAGRAPH MODEL

MechJeb is a bag of **ComputerModules** hanging off a **MechJebCore** `PartModule`. Every physics tick KSP
calls each module's `Drive(FlightCtrlState s)`, and the modules cooperatively write `s.pitch/yaw/roll`
(attitude), `s.mainThrottle` (thrust), and `s.X/Y/Z` (RCS translation). Modules are turned on by a
**refcounted UserPool** (a module is Enabled iff ≥1 "user" wants it), so an autopilot (e.g. Ascent) enables
the shared services it needs (Attitude, Thrust, Staging) and they auto-disable when it lets go. The two
pillars are **CONTROL** (attitude law + throttle-limiter stack, run every tick) and **GUIDANCE** (a plan —
PVG ascent solution, a maneuver node, a rendezvous decision tree — that sets targets for control). This is
exactly the autopilot architecture in [[TRUE_AUTOPILOT_ARCHITECTURE]]: nav → control → guidance → sequencer.

---

## 1. THE MODULE SYSTEM (`MechJebCore.cs`, `ComputerModule.cs`)

**`MechJebCore : PartModule`** — one per vessel (the AR202 part or a command pod). Holds strongly-typed
references to the singleton service modules and creates all ~79 modules. The mission-critical ones:

| Field | Module | Role | Priority |
|---|---|---|---|
| `Attitude` | MechJebModuleAttitudeController | point the vessel (writes pitch/yaw/roll) | 800 |
| `Thrust` | MechJebModuleThrustController | throttle + the limiter stack (writes mainThrottle) | 200 |
| `Staging` | MechJebModuleStagingController | autostage decision logic | 1000 |
| `Node` | MechJebModuleNodeExecutor | execute maneuver nodes (FSM) | — |
| `Guidance` | MechJebModuleGuidanceController | FLY a PVG solution | — |
| `Glueball` | MechJebModulePSGGlueBall | RUN the PVG optimizer, feed Guidance | — |
| `Target` | MechJebModuleTargetController | the selected target + relative geometry | — |
| `Warp` | MechJebModuleWarpController | time-warp control | — |
| `RCS` / `Rcsbal` | RCSController / RCSBalancer | RCS translation + thruster distribution | — |
| `StageStats` | MechJebModuleStageStats | per-stage ΔV/thrust/Isp via FuelFlowSim | — |
| `AscentSettings` | MechJebModuleAscentSettings | ascent config + which ascent autopilot | — |
| `Landing` | MechJebModuleLandingAutopilot | powered descent / precision landing | — |

**`ComputerModule` (the base class) — the pattern to copy:**
- `Drive(FlightCtrlState s)` — called every tick to write control outputs. `OnFixedUpdate()` /
  `OnUpdate()` — physics / frame updates. `Priority` (int) sorts drive order.
- **`UserPool Users`** — the key idea. `Users.Add(x)` enables the module; `Users.Remove(x)` disables it
  only when the LAST user leaves; `Users.Clear()` force-disables. So a consumer writes
  `Core.Attitude.Users.Add(this)` to "grab" attitude and `attitudeDeactivate()`/`Users.Remove(this)` to
  release. Multiple consumers can hold the same service. `Enabled` setter fires `OnModuleEnabled/Disabled`.
- **`Pass` persistence** (`LOCAL`=per-vessel-in-save, `TYPE`=per-vessel-type cfg, `GLOBAL`=global cfg). A
  `[Persistent(pass=…)]` field auto-saves to the matching `mechjeb_settings_*.cfg`. This is why editing the
  type cfg changes a specific vessel-type's ascent.
- `CascadeDisable(m)` — when THIS module disables, also disable `m` (dependency teardown).

**OUR equivalent:** our `MissionConductor` + the per-phase controllers are the "users"; `AttitudePilot` /
`FlightDriver` are the shared services. We don't have the refcounted UserPool — we gate by phase FSM instead,
which is fine for one vessel but is why booster+Dragon dual control needed the explicit
`BoosterRecoveryActive` guard (see [[campaign-plan-process]] C2 Step-2).

---

## 2. NAVIGATION — `VesselState.cs` (the derived-state layer)

Recomputed every tick from KSP truth. It is the autopilot's single source of nav truth — everything reads
`VesselState.*`, nothing recomputes orbital/aero quantities itself. Inventory (the useful ones):

- **Orbit:** `OrbitApA/PeA/SemiMajorAxis/Eccentricity/Inclination/LAN/ArgumentOfPeriapsis/Period/TimeToAp/TimeToPe`,
  `OrbitalPosition`, `OrbitalVelocity`, `SpeedOrbital/Surface/Vertical` + horizontal components.
- **Frames (unit vectors):** `Up`, `North`, `East`, `Forward`, `NormalPlus`, `RotationSurface` (a quaternion).
- **Attitude/rates:** `Pitch`, `Roll`, `Heading`, `AngularVelocity`, `AngularMomentum`, `MoI`,
  `TorqueAvailable` (reaction wheels+RCS+gimbal), `TorqueGimbal`, `TorqueDifferentialThrottle`,
  `TorqueResponseSpeed`, `ThrustForward`, `CoT` (center of thrust), `CoM`, `CoL`.
- **Aero:** `DynamicPressure`, `MaxDynamicPressure`, `Mach`, `AoA`, `AoS`, `AtmosphericDensity`,
  `PureDragVector`, `PureLiftVector`, `DragCoefficient`, `FreeMolecularAerothermalFlux`.
- **Propulsion:** `ThrustAvailable/Current/Minimum`, `MaxThrustAcceleration`, `MinThrustAcceleration`,
  `MaxEngineResponseTime` (spool), `RCSThrustAvailable` (per-direction), `LowestUllage` (RF ullage 0..1),
  `ThrottleLimit`, `ThrottleFixedLimit`.

**How torque/RCS availability is sourced** (the numbers our Campaign 6 needed): documented in
[[AUTOPILOT_HARVEST]] §A — MechJeb sums per-part reaction-wheel torque, engine gimbal torque (r×F about CoM),
and RCS torque, giving `TorqueAvailable`. This is what the attitude controller divides by MoI to get the
achievable angular acceleration.

**OUR equivalent:** `pure/Nav*` + the glue that fills it. We compute our own MoI/torque; Campaign 6's fix was
to use `max(reported ModuleRCS torque, geometric r×F)` because the stock per-module report flickers to ~2 N·m.

---

## 3. ATTITUDE CONTROL — the law that points the vehicle

### 3.1 The framework (`MechJebModuleAttitudeController.cs`, Priority 800)

- **13 reference frames** (`AttitudeReference`): INERTIAL, INERTIAL_COT (fixed for center-of-thrust offset),
  ORBIT (prograde/normal/radial), ORBIT_HORIZONTAL, SURFACE_NORTH(_COT), SURFACE_VELOCITY, TARGET,
  RELATIVE_VELOCITY, TARGET_ORIENTATION, MANEUVER_NODE(_COT), SUN, SURFACE_HORIZONTAL.
  `attitudeGetReferenceRotation(ref)` builds the quaternion for each. **INERTIAL_COT is the important one for
  burns** — it corrects for thrust being off the CoM axis so the vehicle holds an inertial burn vector.
- A consumer calls `attitudeTo(direction|quaternion|hdg/pitch/roll, reference, controller, killRoll)` — this
  adds the caller as a user and sets the target. `attitudeDeactivate()` releases.
- `OnFixedUpdate()` computes `steeringError`, the available `torque`, and **`inertia` = the "angular distance
  to stop"** = `0.5·sign(L)·L²/(torque·MoI)` — the angle it takes to null the current rate at max torque.
  This braking-distance term is the core idea under every good attitude law (also in [[AUTOPILOT_HARVEST]] D5).
- `Drive(s)` calls the active `Controller.DrivePre(s, out act, out deltaEuler)`, scales by
  ActuationControl/AxisControl, and writes `s.pitch/yaw/roll`. It auto-enables RCS when error > 3°
  (`RCS_auto`) and disables it inside 0.4°.
- **5 pluggable controllers** (`activeController`, default **3 = BetterController**):

### 3.2 The five controllers — what each ACTUALLY is (read from source)

| # | Controller | Structure | Notes |
|---|---|---|---|
| 0 | **MJAttitudeController** | the original MechJeb PID | legacy; known to wobble in RSS/RO |
| 1 | **KosAttitudeController** | port of the kOS `SteeringManager` PIDs | the kOS lineage |
| 2 | **HybridController** | cascade: `KosPIDLoop` rate PID → `TorquePI` torque PID; great-circle phi error; `inertia` feedforward | the predecessor to BetterController |
| 3 | **BetterController** (default) | cascade **position→velocity** PID using `PIDLoop2` (digital biquad, setpoint-weighted) + an analytic **slew-and-stop curve** for large errors | the RO/RP-1 default |
| 4 | **LQRController** | per-axis **Linear-Quadratic Regulator** (`LQRLoop1`), plant `M = MoI/controlTorque`, one cost knob `Grr=16`, saturates ±1 | newer/experimental |

**BetterController in detail (this is what we ported — see [[ATTITUDE_CONTROL_RESEARCH]]):**
- Per axis (pitch=0, roll=1, yaw=2). `maxAlpha = controlTorque/MoI` (max angular accel). `controlTorque` is a
  low-pass of `TorqueAvailable` (`SmoothTorque`=0.10 EMA) — this is the anti-noise filter.
- **Small error** (|err| ≤ 2·effLD, effLD = soften²·maxAlpha/(2·posKp²)): outer **position PID** (PosKp 2.03,
  PosTi 1.97) → targetOmega. `PIDLoop2` is a proper setpoint-weighted (B,C) biquad with filtered-derivative
  (N) and anti-windup (Clegg, deadband, SmoothIn/Out).
- **Large error:** analytic **stopping curve** `targetOmega = soften·√(2·maxAlpha·(|err|−effLD))·sign(err)`,
  clamped to `maxAlpha·MaxStoppingTime` (2 s) — slew at max rate, decelerate to arrive at the target with zero
  rate (no overshoot). *This is the key feature for big capsule flips (retrograde, shield-forward).*
- Inner **velocity PID** (VelKp 7.98) tracks targetOmega → targetAlpha (clamped ±maxAlpha) → targetTorque =
  MoI·targetAlpha → actuation = −targetTorque/controlTorque, in [−1,1].
- `RollControlRange` (5°): don't spend roll authority until pitch/yaw are within 5° (**point first, then roll**).
- `Soften` (0.5) reduces large-angle overshoot; `warpFactor` divides posKp so it doesn't wiggle at phys-warp.
- **Gain-scheduled automatically:** because it recomputes `maxAlpha = controlTorque/MoI` every tick, it adapts
  through fuel drain, staging, and the gimbal↔RCS authority change with no manual retune.

**LQRController in detail:** per axis, plant `M = MoI/controlTorque`, solves the LQR gain for the
double-integrator (attitude, rate) minimizing ∫(error² + Grr·control²) with `Grr=16`, output clamped ±1. One
tuning knob (the Q/R ratio Grr). Elegant and provably optimal *for the linear model*, but: (a) **no explicit
slew-and-stop** — big slews rely on the saturated optimal gain, which the research says settles slower/less
smoothly; (b) no deadband/anti-chatter tailoring; (c) least battle-tested in RSS/RO.

### 3.3 ⭐ WHICH CONTROLLER IS BEST FOR US — researched verdict (Chris asked, 2026-08-30)

**Not LQR, despite being the "fancy optimal" one. BetterController is right for us — for reasons of design,
not the name.** The evidence:

1. **Setpoint-weighted PID beats LQR on settling.** Peer-reviewed comparison: *"the transient behaviour of the
   LQR controller is not smooth and takes more time to settle."* BetterController IS a setpoint-weighted
   cascade PID (the B/C weights in PIDLoop2), so it has the better transient the research points to.
2. **Our real problem is on/off thrusters, not the controller choice.** We have **no reaction wheels** — coast/
   deorbit/entry attitude is **Draco RCS (bang-bang)**. Control theory: *"undamped motion within the deadband
   develops into a hard two-sided nutational limit cycle causing many unnecessary thruster actuations,
   undesirable for propellant economy and thruster reliability."* **That is exactly our Campaign-6 Draco
   chatter** (51% actuator saturation, sign-flipping every tick, MMH/NTO drained to 0). Swapping to LQR would
   *worsen* this — plain continuous LQR on on/off thrusters chatters; a study needed a special *on-off* LQR to
   cut that energy 36%.
3. **BetterController fits both our regimes:** the slew-and-stop curve is ideal for the big capsule flips with
   weak/quantized authority; the `SmoothTorque` filter + deadband + point-then-roll reduce RCS thrash; and it
   auto-gain-schedules through the gimbal→RCS authority change. It's the **RO/RP-1 community default** — chosen
   after MJAttitudeController wobbled in exactly our environment — and **we already ported it and flew it to
   pointing p95 0.08–0.36°** ([[ATTITUDE_CONTROL_RESEARCH]], [[mechjeb-wiki-research]]).

**⭐ THE ACTUAL LEVER for our propellant waste is not the attitude LAW — it's the RCS ACTUATION.** Real
spacecraft don't feed a continuous torque demand straight to on/off thrusters; they use:
- **Phase-plane control with a deadband + rate-limit** (the classic RCS attitude law): a deadzone in
  (attitude-error, rate-error) space — only fire when outside it — which stops the limit cycle. We have a
  small `ControlLaw.DeadbandRad ≈ 0.1°`; a proper phase-plane schedule is stronger.
- **PWPF (Pulse-Width Pulse-Frequency) modulation:** convert the controller's continuous torque command into
  thruster pulses whose *average* is near-linear — "less thruster activity, closer-to-linear actuation" than
  bang-bang. **⚠ CORRECTION (verified in source 2026-08-30):** MechJeb DOES ship a PWPF-family modulator —
  `MechJebLib/Control/DeltaSigmaThrottleModulator.cs` (delta-sigma: full-off/full-on pulsing that time-integrates
  to the commanded accel, with MinOn/MinOff dwell + anti-windup) — but it is wired **ONLY to the hoverslam
  ENGINE throttle** (`MechJebModuleHoverslamAutopilot._pwm`), for pulsing a min-throttle engine. **MechJeb does
  NOT apply any PWPF or phase-plane deadband to RCS.** Its RCS control is continuous PID → KSP bang-bang:
  attitude via the AttitudeController writing pitch/yaw/roll; translation via `MechJebModuleRCSController` (a
  velocity PID, Kp 0.125/Ki 0.07/Kd 0.53, → s.X/Y/Z). The `MechJebModuleRCSBalancer` (threaded `RCSSolver` QP)
  only distributes TRANSLATION across thrusters to minimize waste — **rotation balancing is explicitly
  unsupported** (source comment). So RCS-attitude PWPF + phase-plane is genuinely absent, and **we can beat
  MechJeb by adding it to the Draco path** — and better, `DeltaSigmaThrottleModulator` already exists to port
  and adapt. The PID primitive it/we use (`PIDLoop2`) already supports Integral/Output **deadbands** and
  back-calculation anti-windup, so the phase-plane deadband is a config away. This is the research-backed fix
  for the Campaign-6 chatter, and a genuine place we improve on the reference.

**Bottom line:** keep **BetterController** as the attitude law (it's the best of the five for us, and the
research backs setpoint-PID over LQR). Put the effort into **PWPF + phase-plane deadband on the Draco RCS
path**, plus the correct RCS authority estimate (Campaign 6). That combination — not LQR — is what kills the
propellant waste. Consider LQR only as an experiment on the *gimbal* (smooth-actuator) ascent phase, where its
weaknesses don't bite; even there, BetterController already flies our ascent well, so it's low priority.

---

## 4. THRUST CONTROL — `MechJebModuleThrustController.cs` (Priority 200, the throttle brain)

**Self-pinned always-on service:** `OnStart` adds itself as a permanent user so its limiters/ullage run every
tick even when no autopilot drives the throttle; *active* throttle control is gated on `Users.Count > 1`.

**The ordered limiter stack (THE architecture to copy — [[AUTOPILOT_HARVEST]] E1):** `ThrottleLimit` starts
at 1.0; each limiter can only LOWER it, in order:
1. **THROTTLE** (user GUI cap) → 2. **DYNAMIC_PRESSURE** (max-Q: full below MaxQ, then `1−15·(q/qmax−1)`) →
3. **TEMPERATURE** (throttle down within 5% of any part's maxTemp) → 4. **ACCELERATION** (g-cap:
   `(maxAccel−minAccel)/(maxThrustAccel−minThrustAccel)`) → 5. **ELECTRIC** → 6. **FLAMEOUT** (air-breathing) →
7. **MIN_THROTTLE** (floor; has authority over any non-zero limiter) → **ProcessUllage** →
8. **UNSTABLE_IGNITION** (hard zero if `LowestUllage < 0.996`).
Two outputs: `ThrottleLimit` (actual, includes transient zeros like ullage) and `ThrottleFixedLimit` (for
consumers like the node executor to compute burn time — excludes transient zeros, else burn time → ∞).

**Key methods to reuse:**
- **`ThrustForDv(dV, timeConstant)`** — feathered burn termination. Accounts for engine spool
  (`MaxEngineResponseTime`): `desiredAccel = (dV − spooldownDv)/timeConstant`, `throttle = accel/maxThrustAccel`.
  **Use this to end EVERY finite burn** (the node executor does).
- **`ProcessUllage(s)`** (RO) — auto-RCS ullage: sets `s.Z=−1` (fore RCS) and holds throttle at 0 until
  `LowestUllage ≥ 0.996` and thrust has come up; avoids wasting ignitions.
- **`ComputeDifferentialThrottle(torque)`** — an **alglib quadratic-program** that solves per-engine
  `thrustPercentage` to produce a demanded control torque (engine-out compensation / extra pitch-yaw authority
  from throttle asymmetry). Feeds off the attitude controller's demanded torque.
- `ApplySmoothThrottle` — rate-limits throttle change (`DeltaT/ThrottleSmoothingTime`).

**OUR equivalent:** `pure/ControlLaw.ThrottleLimit` (g-cap, with the RealFuels minThrottle-floor remap from
Campaign 5) + `AscentControl`. We have the g-cap and min-throttle; we do NOT yet have the full ordered stack,
max-Q bucket wired as a limiter, differential throttle, or auto-RCS ullage as a clean service (all are in the
BUILD BACKLOG per [[dragonscreen-autopilot-rebuild-plan]]).

---

## 5. STAGING — `MechJebModuleStagingController.cs` (Priority 1000)

**Autostage fires a stage only at DEPLETION / flame-out** — never early. The decision each tick (stage unless
any block returns):
- skip if `currentStage ≤ AutostageLimit`, within `AutostagePostDelay` of the last stage, or waiting for first
  manual stage;
- don't decouple an **active or idle** engine/tank (wait until the dropped stack is spent);
- **RF ullage guard:** if the next stage has unstable ullage and we have RCS, turn RCS on and wait;
- **always** stage if the current stage has no active engines;
- **HotStaging:** if current stage still has engines and the next stage has engines but no decoupler/clamp, and
  the burn time left > `HotStagingLeadTime`, wait (support hot-staging);
- don't fire a stage that deploys a **staying parachute**;
- **fairings:** stage only when `q < FairingMaxDynamicPressure` AND `alt > FairingMinAltitude` AND
  `flux < FairingMaxAerothermalFlux`;
- **launch clamps:** release only at ≥ `ClampAutoStageThrustPct` (99%) thrust with no failed engines;
- **DropSolids** (`ShouldDropSolids`): drop a booster stack early once its stack TWR falls to ≤ `DropSolidsTwrPct`
  (50%) of the whole-rocket TWR (spent SRB jettison) — the ONE early-drop path, and it's TWR-based, not fuel.

**⭐ CONSEQUENCE for our "drop the booster with landing fuel" (Chris 2026-08-30):** autostage runs the booster
to depletion, so left alone there is **no landing fuel** at sep. To sep early with a reserve you must MECO the
booster *before* flame-out — via a manual stage at the real MECO point, or the installed build's
`StagingTrigger`/`DynamicPressureTrigger` (verify against the installed DLL, not the old source), or a craft
whose booster is oversized. PVG's stage optimizer decides the *nominal* MECO for the payload, not a landing
reserve. See [[campaign-plan-process]] / the MechJeb-flight session notes.

**OUR equivalent:** direct `Actuator` part control (fire the specific decoupler by capability), NEVER
`StageManager` — the hard rule ([[direct-part-control-hard-rule]]). We port the *decision logic* (safe-to-sep,
fairing gate, drop-by-TWR) but actuate modules directly.

---

## 6. NODE EXECUTOR — `MechJebModuleNodeExecutor.cs` (burn execution FSM)

Executes maneuver nodes. **States:** `INITIAL_WARP → ALIGNING → WARPING → LEAD → BURN → IDLE`.
- `NextDirection()` keeps a **de-rotated inertial** burn vector (consistent across ticks/warp).
- `CalculateIgnitionUT` uses `BurnTime(dV)` — walks `StageStats.VacStats` stage-by-stage, converting ΔV to
  burn time with the rocket equation and adding spool-up — so ignition is centered on the node (stock) with
  half-burn lead.
- INITIAL_WARP warps to `ignitionUT − InitialWarpLeadTime` (600 s); WARPING warps to `−LeadTime` (3 s) while
  holding attitude; LEAD ullages (RCS) before ignition; **BURN uses `Core.Thrust.ThrustForDv(dvLeft, τ)`** with
  τ=0.5 (or 2 near the end) — feathered so it stops exactly at ΔV=0.
- Termination: stock ends when the angle to the node exceeds 90° (burn vector flips = done); removes the node,
  re-inits for the next (ALL_NODES) or aborts.
- **`RCSOnly`** mode: no main engine — pure RCS `s.Z=−1` translation burn (relevant to a Dragon on Dracos).
- Alignment gates: `AlignedToleranceDegrees` 1°, `WarpAlignedToleranceDegrees` 10°.

**OUR equivalent:** `pure/DeorbitGuidance` + `DeorbitBurn` (the shared Draco deorbit) and the rendezvous burns
use a similar align-then-burn + measured-ΔV pattern; we integrate delivered Δv from measured RCS thrust rather
than trusting a node.

---

## 7. GUIDANCE — the ASCENT stack (PVG / "PSG")

Two ascent paths (`AscentType`): **CLASSIC** (gravity-turn: TurnStart/End altitude, TurnShapeExponent,
AutoPath) and **PSG/PVG** (Powered eXplicit / Primer-Vector Guidance — optimal, for RSS/RO). PVG is three
cooperating pieces:

### 7.1 The SOLVER — `MechJebLib/PSG/Ascent.cs` + `Optimizer.cs` (pure math, runs async)
- `Ascent` is an **AsyncJob** (off the main thread) that builds a **multi-phase optimal-control problem**
  (`PhaseCollection` = the burn stages + optional coast) and runs `Optimizer` (an interior-point / Newton
  solver on the primer-vector costate).
- **Objective:** `MAX_ENERGY` for bootstrapping, then **`MIN_THRUST_ACCEL`** (minimize propellant) for the
  optimal solution. Bootstraps *without* q-alpha first, then adds it; uses `AscentGuesser` for the analytic
  initial guess. Warm-starts from the previous tick's solution.
- **Terminal constraints** = the target orbit as CONSTRAINTS, not a point: peR, apR, attach-R, inclination,
  LAN, FPA, ArgP (this is the linear-tangent/PEG idea in [[ASCENT_GUIDANCE_UPFG]] and [[AUTOPILOT_MINING_3]]).
- Output: a **`Solution`** (position/velocity/throttle/steering as functions of time, per phase).

### 7.2 The DRIVER — `MechJebModulePSGGlueBall.cs`
- Each tick `SetTarget(peR, apR, attR, inc, lan, fpa, attachAltFlag, lanflag)` rebuilds the problem from live
  `StageStats` (per-stage start/end mass, thrust, Isp, minThrottle), atmosphere constants (fits ρ0/h0, Cd,
  Aref, qAlphaMax, qMax), coast settings, and the old solution; starts the async job; on completion feeds
  `Core.Guidance.SetSolution`. Tracks converges/staleness/infeasibility. Pauses `OptimizerPauseTime` after a
  stage event; blocks near `PreStageTime`. `AttachAltFlag` clamps the attach ("Burnout") altitude between peR
  and apR — **attach = orbit alt gives a clean circular insertion; attach < peR = "periapsis insertion"
  (elliptical)**, which is the bug found in the Crew-2 cfg (110 km attach vs 210 km orbit → fixed to 210).

### 7.3 The EXECUTOR — `MechJebModuleGuidanceController.cs` (fly the solution)
- **PSGStatus FSM:** ENABLED → INITIALIZED → BURNING/COASTING → TERMINAL / TERMINAL_RCS / TERMINAL_STAGING →
  FINISHED.
- `UpdatePitchAndHeading`: `Solution.InertialGuidance(t)` → inertial thrust unit + throttle → pitch/heading via
  `Astro.ECIToPitchHeading`; feeds `Core.Attitude.attitudeTo(Inertial, …)`. Locks the inertial heading at
  `Tgo < 2 s` (through staging/burnout).
- `HandleThrottle`: coast vs burn per the solution; sets `Core.Thrust.TargetThrottle`; manages the autostage
  limit so it doesn't stage off the RCS-carrying insertion stage during a coast.
- `HandleTerminal`: **precise engine cutoff** — within 10 s of stage end, predicts state 1–2 ticks ahead and
  ends when `Solution.TerminalGuidanceSatisfied`; optional **TERMINAL_RCS** trims the last bit on RCS for
  accuracy; **TERMINAL_STAGING** for multi-stage precise shutdown.
- Ullage: RCS on `UllageLeadTime` (20 s) before a burn out of a coast.

### 7.4 The ASCENT AUTOPILOT — `MechJebModuleAscentPSGAutopilot.cs` (the vertical→pitch→guidance FSM)
- `VERTICAL_ASCENT` (thrust up until `PitchStartHeight`) → `PITCHPROGRAM` (open-loop pitch at `PitchRate` until
  it meets the guidance pitch and guidance is stable) → `GUIDANCE` (follow `Core.Guidance.Inertial`) → EXIT.
- `SetTarget` passes the target to the Glueball; when **LaunchingToPlane** it overrides inclination + LAN from
  `Core.Target.TargetOrbit` (the plane match). `LimitQaEnabled` is mandatory for PVG.

### 7.5 ⭐ OPERATIONAL — launch-to-plane / launch-for-rendezvous (`MechJebModuleAscentMenu.cs`)
The plane/LAN launch is a **runtime action, not a saved cfg** (`LaunchingToPlane/MatchLan/Lan` are
non-persisted). On the pad, with a target selected, the Ascent window shows buttons:
- **"Launch into plane of target"** — the rendezvous button. Calls `Astro.MinimumTimeToPlane(rotationPeriod,
  lat, lon, TargetOrbit.LAN − LaunchLANDifference, TargetOrbit.inclination)` → (timeToPlane, inclination),
  starts the countdown/autowarp to the plane crossing, and **sets DesiredInclination from the target**. Launch
  at that instant → correct RAAN → coplanar orbit → cheap rendezvous. `LaunchLANDifference` = 0 for the exact
  plane.
- "Launch to target LAN" / "Launch to LAN" — LAN-only variants.
- Requires the target in the same SoI. **This is why "set inclination by hand + engage" fails rendezvous**:
  right inclination, wrong RAAN, and the rendezvous autopilot then needs an unaffordable plane change.

---

## 8. RENDEZVOUS — `MechJebModuleRendezvousAutopilot.cs` (decision tree)

Params (GLOBAL): `desiredDistance` 100 m, `maxPhasingOrbits` 5, `maxClosingSpeed` 100 m/s. Each tick, in
order (first match wins), it PLACES A MANEUVER NODE and lets the NodeExecutor fly it:
1. node already exists → execute it;
2. within desiredDistance + relvel<1 → **done**;
3. within desiredDistance → **match velocities**;
4. distance < R/25 and on a closing course → match velocities at closest approach (adjust to stop at
   desiredDistance);
5. approx intercept course → match velocities at closest approach;
6. **coplanar + circular (relInc < 0.05°, ecc < 0.05)** → **Hohmann transfer** to intercept; if the intercept
   is > maxPhasingOrbits away, establish a **phasing orbit** (raise/lower to a phasing radius) to catch up;
7. coplanar but not circular → **circularize**;
8. **else (off-plane) → MATCH PLANES** (`DeltaVAndTimeToMatchPlanes…`).

**⭐ Why it "doesn't work" for us:** branch 8. Two 51.64° orbits with different RAAN have a large *relative*
inclination; the LEO plane-match burn is hundreds of m/s–km/s the Dragon can't afford → it churns / runs dry.
**Launch coplanar (§7.5) and it drops into branch 6 (cheap Hohmann phasing) instead.** The autopilot itself is
sound; the ascent plane timing is the fix. Also note it drives via **maneuver nodes + NodeExecutor**, so a
Dragon on Dracos executes on RCS (`RCSOnly`). See [[falcon-rendezvous-approach-law]] for our own (node-free)
approach law.

---

## 9. DOCKING — `MechJebModuleDockingAutopilot.cs` (RCS state machine)

Pure RCS translation. Computes `zSep` (along the target docking axis) and `lateralSep` (perpendicular).
**Steps:** INIT → (WRONG_SIDE_BACKING_UP → WRONG_SIDE_LATERAL → WRONG_SIDE_SWITCHSIDE if behind the port) →
BACKING_UP → MOVING_TO_START → DOCKING → OFF. Key ideas to reuse:
- **`MaxSpeedForDistance(d, axis)` = √(2·a·d)** where `a` = RCS accel available along that axis — the
  arrestable-approach-speed law (never approach faster than you can brake); clamped to `speedLimit` (1 m/s).
- Safe distance = vessel bbox + target bbox + 0.5 m; docking corridor radius 1 m; acquire range = half the
  port's `acquireRange`.
- Sets a target *world* velocity (target velocity + a lateral-null + z-approach adjustment) via
  `Core.RCS.SetTargetWorldVelocity`; attitude holds `TARGET_ORIENTATION` (or force-roll).
- Ends on `onPartCouple` (docked) or target loss.

**OUR equivalent:** the L3 docking work (`pure/Dock*`, corridor/capture) — same corridor + arrestable-speed
idea, with our IDSS envelope. See [[PHASE_4_DOCKING_RESEARCH]].

---

## 10. LANDING / POWERED DESCENT — `MechJebModuleLandingAutopilot.cs` (+ Predictions, Hoverslam)

The booster/powered-descent brain (934 lines — deep-read deferred; algorithm summary): a coast→decel-burn→
final-descent FSM driven by `MechJebModuleLandingPredictions` (an atmospheric trajectory integrator that
predicts the impact point, à la KSPTrajectories) and `MechJebLib/HoverslamSimulation` (the suicide-burn timing
— when to start the landing burn so v→0 at h→0). Deploys gear/chutes at gated stages; RCS-trims the approach.
This is the reference for our booster recovery + the capsule hoverslam ([[falcon-real-hoverslam-technique]],
[[AUTOPILOT_MINING_3]] §2, [[PHASE_2_BOOSTER_RECOVERY_RESEARCH]]). Full deep-read is a follow-up when we build
booster recovery.

---

## 11. THE MechJebLib TOOLBOX (pure math — port targets)

`MechJebLib/` is KSP-free and unit-tested — the ideal port source (some already ported, [[MECHJEBLIB_PORT]]):
- **`FuelFlowSimulation/`** — simulates the staging stack to get per-stage ΔV/thrust/Isp/mass/burn-time
  (`FuelStats`). Feeds StageStats, the node executor's BurnTime, and PVG. **Ported + proven** into
  `src/pure/mechjeblib/`.
- **`PSG/`** — the PVG ascent optimizer (Ascent, Optimizer, Phase, AscentGuesser, AscentBuilder).
- **`Maneuvers/`** — `Simple` (circularize, change apsis/inc/period), `TwoImpulseTransfer`, `ChangeOrbitalElement`,
  `ReturnFromMoon`, `InterplanetaryTransfer`, `FineTuneClosestApproach`.
- **`Lambert/`** — the Lambert two-point boundary solver (transfer between two positions in a set time —
  intercept/rendezvous targeting). Backlog for our Lambert rendezvous.
- **`Functions/`** — `Astro` (state↔elements, ECIToPitchHeading, TimeToPlane, MinimumTimeToPlane, nodes),
  `Angles`, `Maneuvers`, `SingleImpulseHyperbolicBurn`.
- **`ODE/`** — Runge-Kutta integrators (BS3, DP5, DP8, Tsit5) with event detection (for the trajectory/entry
  sims).
- **`Control/`** — `PIDLoop`/`PIDLoop2` (the digital biquad PIDs BetterController uses), `LQRLoop1`.
- **`TwoBody/`**, `Primitives/` (V3, M3, Q3, H3 vector/quaternion), `Rootfinding`, `Minimization`, `Lambert`,
  `HoverslamSimulation`, `Interpolants`.

---

## 12. TICK ORDER + how a full mission drives control

Per physics tick, modules `Drive(s)` in priority order (Thrust 200 → Attitude 800 → Staging 1000 among the
services), each writing part of `FlightCtrlState`. A mission works like:
1. An **autopilot** (Ascent / Rendezvous / Landing) is enabled by the user; it `Users.Add`s the services it
   needs (Attitude, Thrust, Staging, Guidance/Node).
2. **Guidance** decides the plan (PVG solution, or a maneuver node, or a rendezvous node) and sets the
   attitude target + throttle target.
3. **Attitude** turns the target into pitch/yaw/roll via BetterController; **Thrust** applies the throttle
   through the limiter stack; **Staging** sheds spent stages; **RCS/Balancer** distributes translation.
4. On completion the autopilot `Users.Remove`s itself and the services auto-disable.

**OUR architecture maps 1:1:** `MissionConductor` (sequencer) → per-phase controllers (guidance) →
`AttitudePilot`/`ControlLaw` (control) → `FlightDriver`/`Actuator` (the KSP write + direct part control). The
gaps vs MechJeb are catalogued in [[MECHJEB_CAPABILITY_INTEGRATION]] and the rebuild plan.

---

## 13. WHAT TO TAKE FROM MECHJEB FOR OUR AUTOPILOT (priority)

1. **Attitude law:** BetterController — already ported, keep it (§3.3). Add **PWPF + phase-plane deadband on the
   Draco RCS path** — the real fix for propellant chatter, which MechJeb does NOT apply to RCS (it only
   pulse-modulates the hoverslam throttle). Port/adapt `DeltaSigmaThrottleModulator` + use `PIDLoop2`'s
   deadbands.
2. **Thrust:** the ordered limiter stack + `ThrustForDv` feathering + auto-RCS-ullage as a clean always-on
   service (§4) — partially built.
3. **Guidance:** the PVG solver structure (target-as-constraints, min-thrust-accel objective, terminal precise
   cutoff, TERMINAL_RCS trim) — our UPFG is the interim; PVG is the target (§7, [[ASCENT_GUIDANCE_UPFG]]).
4. **Node/burn execution:** align→warp→lead→feathered-burn FSM (§6).
5. **Rendezvous:** the coplanar-first decision tree + phasing-orbit logic — but launch coplanar so we stay in
   the cheap branch (§8).
6. **Docking:** arrestable-speed corridor law (§9).
7. **FuelFlowSim / StageStats** — the ΔV/burn-time backbone everything else needs (ported).

---

## Index of the deep-dive docs this map ties together
- [[TRUE_AUTOPILOT_ARCHITECTURE]] — the 5-part autopilot design (nav/control/guidance/sequencer/FDIR).
- [[MECHJEB_CAPABILITY_INTEGRATION]] — the A–O capability inventory + build sequencing.
- [[AUTOPILOT_HARVEST]] — deep mining of the attitude controller, thrust limiter stack, staging, PID zoo.
- [[ATTITUDE_CONTROL_RESEARCH]] — the BetterController port, confirmed from source.
- [[mechjeb-wiki-research]] — the PID choice + per-vehicle tuning + SmoothTorque.
- [[MECHJEBLIB_PORT]] — the FuelFlowSimulation port (done).
- [[ASCENT_GUIDANCE_UPFG]] / [[AUTOPILOT_MINING_3]] — UPFG/PEG + PEGAS/Trajectories/GravityTurn mining.
- [[PHASE_3_RENDEZVOUS_RESEARCH]] / [[PHASE_4_DOCKING_RESEARCH]] — rendezvous + docking.
- Source of truth: `Desktop/mechjeb_src` (MechJeb2 = glue, MechJebLib = math). ⚠ older than the installed DLL.

## Sources (online research, controller verdict §3.3)
- Setpoint-weighted PID vs LQR settling: peer-reviewed control comparison (LQR transient "not smooth, takes
  more time to settle").
- Bang-bang thruster limit cycle / propellant economy + phase-plane deadband: NASA/DTIC on-orbit attitude
  control literature.
- On-off LQR saves 36% vs continuous LQR for thruster rendezvous (why plain LQR is wrong for on/off Dracos).
- PWPF modulation ("less thruster activity, closer-to-linear") as the bang-bang alternative.
- BetterController = the RO/RP-1 default (MechJeb2-RO branch; KSP forum RSS/RO threads).
