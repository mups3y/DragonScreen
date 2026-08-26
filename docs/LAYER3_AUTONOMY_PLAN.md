# Layer 3 — Autonomy: fault detection + re-plan + in-flight self-calibration

**The mandate (user, 2026-08-25):** a TRUE autopilot, not a very good tracker. It must *sense everything,
decide for itself, notice when a plan is failing, re-plan on its own, and never need a human tuning
constants between flights.* "If SpaceX/NASA wouldn't use it we don't want it. Every second, end to end."

Read this first after compaction. It is the complete plan and the concrete first steps.

## The three layers, and why Layer 3 is the work

1. **Control (closed-loop)** — hold a setpoint (attitude PID, g-limit throttle, hoverslam braking curve). DONE, solid.
2. **Guidance (compute commands live from state+goal)** — UPFG, CW re-solve, hoverslam ignition, deorbit aim, live engine-mode choice, live landing reserve. LARGELY DONE this session.
3. **Autonomy (decide / detect failure / re-plan / self-calibrate)** — the layer that makes it an autopilot. PARTIAL today (scattered ad-hoc guards). THIS IS THE FOCUS.

Real-world name for most of this: **FDIR — Fault Detection, Isolation, Recovery** — plus **onboard
parameter estimation** (self-calibration). Research these for grounding (NASA FDIR, adaptive guidance,
onboard system identification). No Python sims (banned) — validate with headless C# tests + the flight corpus.

## What already exists (scattered — Layer 3 is to UNIFY + EXTEND, not restart)

Inventory these first (they become the seeds of the framework):
- `pure/BurnExec.Runaway` — residual runs away (wrong-way burn) → abort.
- `NodeExecutor` — progress-based backstop (residual not falling → abort); RCS sign self-correct; ignition retry.
- `pure/Landing` — `NoSolution` (can't stop), `LandingReserveReached` (live fuel need), `EnginesFor`/`LandingEngines` (live thrust-need mode choice, now individual-engine gap-free).
- `pure/Ascent` — apoapsis-runaway backstop, circularisation-diverging abort, flameout MECO.
- `AbortResponder` — LES / KOS-retreat / safe-hold (phase-correct abort).
- `WaypointApproach` — KOS-breach auto-abort; `CwTargeting` passive-abort free-drift check.
- `BoosterRecovery` — `bl_liveThrustKn` (producing vs available), ignition retry, cold-gas/ullage.
- `LaunchWindowOps.MeasureAtInsertion` — measures ascent time/lon-gain and reports the correction (a
  self-calibration pattern already in the code — GENERALISE it).
- Life support — real time-to-crew-loss margins driving the gates.

These prove the discipline works; Layer 3 makes them a coherent framework instead of one-offs.

## Architecture (build pure-first, headless-tested, instrument EVERYTHING)

### A. `pure/HealthMonitor.cs` — the fault-detection primitive
A monitor is a pure function of sensed state producing a **residual** (expected − actual) and a **verdict**
(Nominal / Degraded / Failed), with **debounce** (N consecutive ticks before declaring, so noise can't
trip it) and **hysteresis** (don't flap). Struct + step function, carried state owned by the caller
(stateless-law discipline like Attitude.cs). Every monitor is a small, named, tested unit.

### B. `pure/FaultResponse.cs` — the decision layer
Given the set of monitor verdicts + the current phase, choose ONE response, in a strict priority order:
`Continue → Retry → Reconfigure → Replan → Downmode → Abort`. Pure, deterministic, tested (assert the
right response for each fault×phase). This is the "decide for itself" layer.

### C. `pure/Estimators.cs` — in-flight self-calibration
Live estimators that REPLACE tuned constants: mass/thrust from measured acceleration (F=ma), terminal
velocity from the descent, ballistic coefficient (already measured), ascent time/lon-gain. Each is a pure
filter (first-order, frame-rate-independent, like Trajectory.SmoothBc).

### D. `LearnedParams` (glue, `PluginData/learned.cfg`) — cross-flight memory
A tiny persisted store the autopilot writes at end-of-phase and reads at start: measured terminal v,
descent time, ascent time/lon, bc-by-Mach refinements, barge-miss residual. So the vehicle IMPROVES over
flights with ZERO human tuning — the self-calibration the user demands. (Seed from current constants;
each flight overwrites with the measured value, exactly as MeasureAtInsertion already does for the window.)

### E. Instrumentation — `fd_` recorder block
Every monitor's residual + verdict + the chosen FaultResponse + which estimator value is live vs seed.
Same rule as always: the recorder must show, in one glance, WHAT the autopilot sensed and WHY it decided.
Add the columns the same pass you build each monitor.

## Phased build order (each lands something tested; one concept per step)

- **P0 — Framework.** `HealthMonitor` + `FaultResponse` + `fd_` recorder + tests. Wire NOTHING yet; prove
  the primitives headless. Migrate `BurnExec.Runaway` + `NodeExecutor` progress-backstop into a
  `ThrustDeliveryMonitor` as the first concrete monitor (it's the most universal: expected Δv-rate vs
  delivered → engine dead/degraded). Corpus-check it against the no-burn rendezvous + lit-but-no-thrust flights.
- **P1 — Per-phase monitors.** Build + wire, one phase at a time (ascent → booster → rendezvous → deorbit
  → entry). Each phase gets: thrust-delivery, trajectory-track (guidance's own prediction vs actual),
  convergence (is the error shrinking?), resource-margin. Unify the existing ad-hoc backstops into these.
- **P2 — Re-plan responses (the heart).** For each detected fault, RE-PLAN instead of only abort:
  - Engine-out/underperformance → recompute guidance on the reduced thrust (UPFG already adapts; the
    booster landing now recomputes with fewer engines via the individual-engine control).
  - Booster can't reach the barge within authority → minimise miss / pick the reachable point / flag water.
  - Rendezvous not converging → re-plan phasing (explicit convergence monitor → re-solve).
  - Deorbit under-delivers → recompute the aim (already closed-loop; make it explicit + monitored).
  - Escalate to `AbortResponder` only when re-plan can't recover.
- **P3 — Self-calibration.** Stand up `Estimators` + `LearnedParams`; retire tuned constants one at a time,
  each behind the estimator with the constant as the seed/fallback: terminal v (booster reserve),
  Overflight descent-time (RSS cross-track), ascent time/lon (already), bc-by-Mach refinement, barge-miss
  residual → auto-trim the entry aim buffer. GOAL: zero constants a human must touch between flights.
- **P4 — Validation.** Headless tests for every monitor+response; **corpus replay** — feed past failure
  recordings through the monitors and assert they'd have caught them (free-fall, no-burn rendezvous, g-limit
  oscillation, entry-burn over-burn). Then in-game proving flights.

## The specific tuned constants to KILL (self-calibration targets)
`Landing.EntryBurnReserveFrac` (already superseded live; make terminal-v measured not estimated) ·
`Landing.EntryAimBufferM` (auto-trim from the measured barge miss) · `Overflight.DescentTimeS`/`LagArcFrac`
(measure the real descent) · `LaunchWindowOps.AscentTimeS`/`AscentLonDeg`/`AscentArcDeg` (already measured —
persist to LearnedParams) · `BoosterDrag` bc curve (refine from each flight's clean descent samples).

## STATUS

### P0 — DONE (2026-08-25). Built, headless-tested green, NOT installed, NOT committed.
Grounded in real FDIR (residual → threshold → verdict; local-recovery-before-safe-mode escalation) and
onboard parameter estimation (recursive-least-squares / forgetting-factor identification of mass+thrust) —
web-researched this session, cited in the file headers.
- **`pure/HealthMonitor.cs`** — the detect primitive: residual → verdict (Nominal/Degraded/Failed) with
  **TIME-based confirmation** (seconds, not tick counts — frame-rate-independent like `Trajectory.SmoothBc`,
  and warp-proof) + **hysteresis** (separate clear threshold). Caller-owned `MonitorState` struct, pure
  `Step(prev,cfg,residual,dt)`, `Fresh()` reset on phase change (the Attitude.cs staleness discipline).
- **`pure/FaultResponse.cs`** — the decide layer: `Recovery` ladder ordered by severity
  `Continue<Retry<Reconfigure<Replan<Downmode<Abort` + `Worst()` for concurrent faults; `FaultKind`
  taxonomy; `FaultDomain` regime (separate from MissionPhase — spans the booster, set by engaged
  controller). `Decide(kind,verdict,domain)` — the table **encodes the existing seeds' own choices**
  (each branch cites its seed), not invented policy.
- **`pure/ThrustDeliveryMonitor.cs`** — first concrete monitor, unifies BurnExec.Runaway +
  progress-backstop + lit-but-no-thrust + off-axis-no-burn into ONE along-axis delivery residual
  (0=full, 1=nothing, >1=wrong-way). Corpus-grounded unit tests replay all four real failures.
- **`test/HealthMonitorTest.cs`** — 52 checks (registered in TestMain), all green.
- **Observe-only wiring (P0's "prove it live before authority"):** `NodeExecutor` ticks the monitor every
  burning tick and exposes `DeliveryVerdict/Raw/Residual/Fault`; FlightRecorder writes the **`fd_` block**
  (`fd_thrVerdict,fd_thrRaw,fd_thrResid,fd_thrKind,fd_thrRecovery`) via `FaultDomainNow()`. It drives
  NOTHING — the existing guards still act — so it is safe to ship ahead of the engine test and turns the
  next flight into the monitor's real validation.

## First concrete steps for the next session (start here)
1. **IN-GAME (user):** `python build.py install`, restart KSP, fly. Confirm (a) the individual-engine
   rebuild — booster lights all 9, landing steps 9→3→1 gap-free (delete the patch if broken); (b) the new
   `fd_` columns move sensibly on any burn — a healthy burn reads `fd_thrVerdict=NOMINAL`, and the
   no-burn/reversed failures (if they recur) light up `FAILED` + the right `fd_thrKind`. That validates
   ThrustDeliveryMonitor against a live flight before it is given authority.
2. **P1 begins:** stand up the next concrete monitors as pure+tested, one regime at a time — a
   trajectory-track monitor (guidance's own prediction vs actual), a convergence monitor (is the error
   shrinking?), a resource-margin monitor — reusing HealthMonitor. Wire each observe-only behind more
   `fd_` columns first, exactly as ThrustDeliveryMonitor was, THEN give authority (P2).
3. **P2 (the heart):** let FaultResponse actually DRIVE — engine-out → UPFG re-solve; booster underperf →
   fewer-engine re-solve; rendezvous not converging → re-phase; escalate to AbortResponder only when
   re-plan cannot recover. Each behind a flight.
4. P3 self-calibration (Estimators + LearnedParams, kill the tuned constants), then P4 corpus-replay.

## State carried in from this session (context)
- All Layer-1/2 work is BUILT + INSTALLED (see `docs/GNC_AUDIT_2026-08-25.md`, `[[dragonscreen-gnc-audit-2026-08-25]]`):
  UPFG vthrust fix, RSS deorbit-orbit fix, both-stage g-limits, booster free-fall fix, thrust-need engine
  selection, live landing reserve, plane∩phase launch window, passive-abort.
- **Individual-engine rebuild is BUILT + INSTALLED, PENDING an IN-GAME TEST** (config patch
  `GameData/Crew2_Patches/F9_S1_IndividualEngines.cfg` + guidance). CONFIRM it in-game FIRST next session
  (booster lights all 9 at launch; landing steps 9→3→1 gap-free); if broken, delete the patch (auto-reverts).
- Nothing committed (standing rule). Build/test: `cd plugin && python build.py test|install` (KSP+CKAN
  closed, full restart). Diagnose: `python build/assess_flight.py "<newest capture>"`.

## Discipline (do not drop under the ambition)
Pure-first + headless-tested before glue · instrument the SAME pass · confirm against ground truth
(ConfigCache/CSV/KSP.log), not the .md · read files IN FULL · one root-cause pass then one fix · NO Python
sims — corpus + headless tests · detect by capability not part name · do NOT commit unless asked.
