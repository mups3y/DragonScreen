# CAPABILITY BUILD BACKLOG — Chris's ticked MechJeb set, dependency-ordered

> **Source of truth:** `docs/MECHJEB_CAPABILITY_CHECKLIST.md` (Chris ticked it 2026-08-30 — build ALL ticked
> capabilities). This is the execution order. Rules unchanged: **pure-first + headless**, **ONE change class
> per campaign**, §8 output before code, 3-tick (nothing "done" until flown for the flight-gated ones), verify
> each claim against live code before editing. Legend: ✅ HAVE · 🟡 PARTIAL (extend) · 🔨 BUILD (new) · ⭐ our
> improvement over MechJeb.
>
> **Why tiers:** each tier only depends on the ones above it. We do NOT start a tier until its dependencies are
> headless-green. Most of Tiers 1–3 are PURE (headless-provable, no flights); the flight-gated glue is flagged.

---

## ⚠ REALITY CHECK — audited `plugin/src/pure/` (2026-08-30) before building anything
We already have **~90 pure modules, headless-proven (731k checks pass)**. Most of the ticked set EXISTS in
pure form — the gap is **completion + WIRING into the mission + tuning**, NOT building from scratch. Do NOT
rebuild these; VERIFY each does what the capability needs, then WIRE + tune.

**EXISTS as tested pure modules (complete or first-cut):** Vec3 · Conic · Orbital · Predict · Lvlh · ArcGeometry ·
LaunchAzimuth · **Lambert** · Maneuver · Hohmann · Cw · Phasing · Departure · DeorbitGuidance · ControlLaw ·
AttitudeLoop · **DiffThrottle** · **RcsBalance** · Authority · QAlpha · ThrustBalance · ActuatorLag · AscentLoss ·
StageStats · IgnitionGate · Actuation · VehicleParts · Ascent · Upfg · LaunchWindow · LaunchTuner · CourseCorrect ·
Rendezvous · RvCoast · DockApproach · DockCapture · DockControl · DockCorridor · Hoverslam · BoosterDescent ·
BoosterDrag · Entry · Chutes · Trajectory · SafeLandingSite · GridFin · Rls · NavFilter · Fdir · FaultMonitor ·
SelfCal · WarpPlan · CoastEta · Aero · LifeSupport · CabinEnvironment · Alarms · Predict · MissionPhase/Profile · ModeManager.

**GENUINELY MISSING (no pure module — real BUILD):** ⭐ **DeltaSigma/PWPF + phase-plane RCS** · **PVG optimizer**
(Ascent/Optimizer/Phase/Terminals — we have UPFG interim) · **SmartASS / SmartRCS** presets · **Translatron** ·
**Deployables** (solar/antenna) · **LQRLoop1** · standalone **ODE integrators** / **root-find** / **minimizer** /
**AutoDiff** (may be inlined in Predict/Trajectory — verify) · **ReentrySimulation** as a first-class predictor ·
**Landing autopilot** (land-at-target / land-somewhere glue) · **Warp-to-phase-angle helper** · **early-MECO trigger**.

**So the true work, in order:** (1) VERIFY the existing pure modules match the capability (many are "first-cut");
(2) **WIRE them into MissionConductor + the phase controllers** (the biggest gap — proven brains, not connected);
(3) BUILD the ~12 missing pieces; (4) tune phase-by-phase in flight. The tiers below fold this in: 🟡 = exists,
complete+wire; 🔨 = genuinely build.

## ⚙ WIRING AUDIT (2026-08-30) — which built capabilities are actually USED in flight
Chris: "make sure all capabilities are USED in ALL the places they are useful." Grepped the glue for each
built pure module. **Wired NOWHERE (built + headless-proven, but dead gold):**
- ✅ **NavFilter** (strict-fidelity Kalman rel-nav) → WIRED into `RendezvousControl.FlyNearFieldCw` (`e99ed21`)
  AND `DockingControl` (terminal): both simulate the sensor from truth, fuse through NavFilter, fly the
  guidance/servo on the ESTIMATE. Docking adds the **terminal sensor handoff** (`NavFilter.TerminalSensorNoiseM`:
  rel-GPS→LIDAR range-scheduled 1σ, cm-class in close so the sub-metre dock survives) — the follow-up NavFilter's
  header flagged. Instrumented, tunable `UseNavFilter`. ⏳ Tick-3 flight to read the est-vs-truth logs.
- ✅ **Lambert** (two-impulse intercept) → WIRED via `pure/RvIntercept.cs` (tof scan + **transfer-periapsis floor
  gate** + cost cap over the tested `Maneuver.InterceptDv`) + `RendezvousControl.TryLambertIntercept` (closed-loop
  on a latched arrival UT: re-solve residual Δv → point → translate → coast to the CW hand-off). Headless
  `RvInterceptTest` 10 checks (self-inversion, pe-safe guarantee, floor+cost refusal). Tunable `UseLambertIntercept`
  **default OFF** (CW/Hohmann stay default until a flight tunes it on). ⏳ Tick-3 flight to enable + tune.
- ✅ **Authority** (per-axis control authority + arrestable rate) → VERIFIED SUPERSEDED (2026-08-30, do NOT wire
  a redundant path). The capability — "never command a rate you cannot arrest" — IS live: BetterController
  (`pure/AttitudeLoop.cs`, the committed inner loop) computes `maxAlpha = controlTorque/MOI` (line 103) and the
  identical `√(2·maxAlpha·(|e|−effLD))` arrestable-rate braking curve (line 128) itself. `pure/Authority` +
  `ControlLaw.RateCommand/AxisCommand` (which consume it) are a PARALLEL attitude PD that the glue never flies
  (only `ControlTest` calls them; the live loop is AttitudePilot→AttitudeLoop). Adding an Authority-based path =
  "hat on a hat" ([[mod-dependency-policy]]). Left as-is; `ControlLaw.ThrottleLimit` (the throttle limiter, a
  different function) stays live in AscentControl.

**Per-phase usage (what each phase controller pulls in today):**
- Ascent: AttitudePilot, ControlLaw, QAlpha, SelfCal, Ullage, Upfg. (DiffThrottle/ThrustBalance/RcsBalance/
  ActuatorLag are wired via Actuator + AttitudeController, so ascent gets them indirectly. NavFilter absent.)
- Rendezvous: Cw, Hohmann, Phasing, Rendezvous FSM. (Lambert + NavFilter absent.)
- Return: DeorbitGuidance, DeorbitBurn, Entry, EntrySteering, Chutes, CourseCorrect, Predict, **SafeLandingSite**
  (Trajectory is wired via EntrySteering). ✅ **SafeLandingSite → nominal return LZ** now WIRED: shared
  `LandingSiteScan` (one copy of the F4 water-gate fix, used by BOTH abort + return) selects the nearest reachable
  open-water splashdown; `EntrySteering.SetSplashTarget` steers the lifting-entry footprint at it. Tunable
  `UseSafeLandingSite`. ⏳ Tick-3 flight to tune the bank-steering signs (R6) to the recorded footprint.
- ⭐ **RcsPulse (PWPF)** → NEW, wired at `FlightDriver.OnFlyByWire` (all RCS phases). ✅ `1d0f613`.

**Next wiring campaigns (each its own §8 pass, build+wire+instrument, then flight-tune):** NavFilter→rel-nav ·
Lambert→rendezvous intercept · SafeLandingSite→return LZ · the genuinely-missing §K/§L glue (landing autopilot,
deployables) · then the Settings-page TUNING tab so these tunables are live-adjustable in flight (Chris's ask).

## TIER 0 — already built (verify, don't rebuild)
✅ VesselState + torque availability (geometric RCS) · attitudeTo + frame subset · BetterController + RollControlRange ·
throttle write · max-Q (first-cut) · g-cap (Campaign-5) · min-throttle floor · q·α (first-cut) · ascent FSM ·
FuelFlowSim + StageStats · PIDLoop2 (ControlLaw) · Hohmann/circularize/change-pe (DeorbitGuidance) · match-planes ·
CW+Hohmann rendezvous (partial) · dock corridor + arrestable speed (partial) · deorbit burn (DeorbitBurn, R1) ·
CoM lifting entry (first-cut) · chute sequence (partial) · WarpPlan/CoastEta · FlightRecorder.

## TIER 1 — PURE MATH ROOTS  (headless, no flights — unblocks everything)
- 🔨 **Primitives** — V3/M3/Q3/H1/H3 quaternions, Vec (port `MechJebLib/Primitives`; confirm what our pure/ already has)
- 🔨 **AutoDiff (Dual numbers)** — for optimizer gradients (`Primitives/Dual`, `Utils/AutoDiff`)
- 🔨 **ODE integrators** — BS3/DP5/DP8/Tsit5 + event detection (`MechJebLib/ODE`)
- 🔨 **Root-finding + Minimization** — Bisection/Brent/Newton + BrentMin (`Rootfinding`, `Minimization`)
- 🔨 **Two-body propagation** — Farnocchia + Shepperd analytic Kepler advance (`TwoBody`)
- 🔨 **Astro + Angles** — state↔elements, node times, Min/TimeToPlane, ECIToPitchHeading, energy (`Functions/Astro`,`Angles`)
- 🔨 **Lambert solvers** — Gooding + Izzo two-point transfer (`Lambert`) ⭐ enables real intercept/rendezvous
- 🔨 **Maneuver library** — Simple (circularize/change apsis/inc/ellipticize), ChangeOrbitalElement, TwoImpulse, FineTuneClosestApproach, SingleImpulseHyperbolic (`Maneuvers`) — **the whole §H set**

## TIER 2 — CONTROL PRIMITIVES + ⭐ RCS ACTUATION  (headless where pure)
- 🔨 **PID zoo** — PIDLoop, PIDController, PIDControllerV2, Biquad, TorquePI, KosPIDLoop, DirectionTracker (`Control` + attitude inner)
- 🔨 **LQRLoop1** — the LQR loop (for the optional gimbal experiment; not the RCS default)
- ⭐✅ **DeltaSigma/PWPF + deadband** — DONE (`1d0f613`): `pure/RcsPulse.cs` + wired at `FlightDriver.OnFlyByWire` (translation always; attitude when engine off). Headless 11 checks green, installed. **Campaign-6 chatter fix.** ⏳ Tick-3 flight to confirm reduced thrash. (Follow-up: a full phase-plane (error,rate) deadband in the attitude law itself.)
- 🟡 **Differential throttle** — per-engine `thrustPercentage` QP (engine-out / extra authority) — port the alglib QP

## TIER 3 — THRUST LIMITER STACK (complete it) + TRANSLATRON  (glue, compile-green)
- 🟡 **Ordered limiter stack** — make our ControlLaw the same cascade: user-cap → maxQ → temp → g → electric → min-floor → ullage → unstable-ignition, with ThrottleLimit vs ThrottleFixedLimit split
- 🔨 **ThrustForDv(dV,τ)** — feathered burn cutoff w/ spool comp (use for every finite burn)
- 🔨 **Temperature limiter · Electric limiter · Smooth-throttle · User throttle cap**
- 🔨 **Auto-RCS ullaging** — as a clean always-on service (RF)
- 🔨 **Unstable-ignition prevention** — ullage<0.996 → kill throttle
- 🔨 **Translatron** — KEEP_ORBITAL/SURFACE/VERTICAL speed-hold + kill-horizontal

## TIER 4 — STAGING (complete) + EARLY MECO  (glue; direct-part actuation, never StageManager)
- 🟡 **Autostage decision logic** — port the gates (safe-to-sep, hot-staging lead, fairing q/alt/flux, launch-clamp 99%, autostage-limit/stop-at-stage) onto our direct-decoupler Actuator
- ⭐🔨 **Early-MECO trigger** — sep the booster before depletion (velocity/q trigger) → **drop the booster with landing fuel** (verify installed-DLL StagingTrigger first)

## TIER 5 — ASCENT (PVG) + LAUNCH-TO-PLANE  (guidance; flight-gated)
- 🔨 **PVG/PSG optimal ascent** — port the solver (Ascent/Optimizer/Phase/Terminals/Guesser/Builder), driver (Glueball), executor (GuidanceController): multi-phase min-thrust-accel, terminal precise cutoff, terminal-RCS trim, coast phases, per-stage optimize/unguided/fixed. (Our UPFG is the interim.)
- ⭐🔨 **Launch into plane of target / to LAN** — timed launch + inclination-from-target (**the rendezvous-launch fix**) + auto-warp countdown
- 🔨 **Classic gravity-turn** (fallback path) · **corrective steering** · **AoA limiter** · **force/vertical/turn roll** · **attach/burnout alt** · **desired-FPA** · **spinup controller** · **auto-deploy panels/antennas on ascent**
- 🟡 **RETURN R2–R6** (corridor-FPA target, entry survivability, trunk-jettison [done R3], chute survival, shroud latch + LZ bank steering) — slots here; R1 built awaiting flight

## TIER 6 — NODES + MANEUVER PLANNER  (wires Tier-1 maths to the flight)
- 🟡 **Node Executor (complete)** — warp→align→lead→feathered-burn FSM, execute-one/all, RCS-only, auto-warp-to-node
- 🔨 **Node Editor / maneuver-planner actions** — expose the §H maneuver library as commandable burns

## TIER 7 — RENDEZVOUS (full tree)  (flight-gated)
- 🟡 **Full rendezvous decision tree** — plane-match → circularize → phasing-orbit → Hohmann intercept → close → match-velocities → done, with max-phasing/closing/desired-distance params
- ⭐🔨 **Lambert transfer intercept** (uses Tier-1 Lambert) · **coplanar-first logic** (cheap when in-plane)

## TIER 8 — DOCKING (full)  (flight-gated)
- 🟡 **Corridor FSM (complete)** — align→correct-side→corridor→dock, bounding-box safe-distance/target-size, wrong-side avoidance, force-roll, speed-limit/acquire-range (arrestable speed already HAVE)

## TIER 9 — LANDING / ENTRY  (flight-gated; the booster + capsule recovery brain)
- 🔨 **Landing autopilot** — land-at-target (coords/KSC) + land-somewhere (nearest-safe)
- 🔨 **Landing Predictions + ReentrySimulation** — atmospheric integrate-to-impact (uses Tier-1 ODE) ⭐ entry predictor
- 🟡 **Hoverslam** — suicide-burn timing (HoverslamSimulation) + hoverslam autopilot (uses the delta-sigma modulator)
- 🟡 **Gear/chute auto-deploy at gated stages · RCS approach trim · deorbit planning** (deorbit HAVE)

## TIER 10 — SUPPORT SERVICES  (glue)
- 🔨 **SmartASS** — attitude presets (prograde/retro/normal/radial/target/kill-rot/hdg-pitch-roll) · **Smart RCS** presets
- 🔨 **Attitude Adjustment** — live PID-gain tuning surface
- 🔨 **Deployables** — solar panel (deploy+sun-track), antenna, generic deployable controller
- 🔨 **Warp Helper** — warp-to-phase-angle + presets (WarpToUT/Event HAVE)
- 🟡 **Target controller** — relative pos/vel/distance, docking axis, set-target (vessel/body/position/port)
- 🔨 **Instrumentation** — recorder graph, info-item catalog (ΔV/TWR/suicide-burn/node-time), debug arrows, trajectory draw

## TIER 11 — INFRA PATTERNS  (Chris: "use to strengthen our build where you can")
- 🔨 **UserPool refcount pattern** — adopt for our shared services (MissionConductor grants), replacing ad-hoc phase gating where it helps
- 🔨 **Module priority / tick order** — a clean Drive() ordering (thrust→attitude→staging) in the conductor
- 🔨 **Settings persistence (LOCAL/TYPE/GLOBAL)** — per-vessel-type tunable persistence like MechJeb's cfg passes
- 🔨 **Install/compat guards + localization scaffolding** — where it hardens our load path

---
### Build discipline (so this doesn't repeat the chaos)
1. **Do a tier top-to-bottom; don't skip dependencies.** Tiers 1–3 are mostly PURE → headless-provable with no
   flights; bank those first (fast, safe, high-leverage).
2. **One change class per campaign** — a campaign is one coherent capability (or a tight cluster), built + its
   headless tests, in one pass. §8 output before code.
3. **Flight-gated tiers (5,7,8,9)** are tuned phase-by-phase in mission order once their pure deps are green.
4. **Track here** — flip 🔨→✅ as each lands headless-green + (where applicable) flown.

*Companion to `docs/MECHJEB_MASTER_MAP.md` (how each works) + `docs/ULTIMATE_PLAN.md` / `AUTOPILOT_REBUILD_PLAN.md`
(the prior B1–B11 backlog this consolidates + extends) + `docs/RETURN_FIX_PLAN.md` (R1–R6).*
