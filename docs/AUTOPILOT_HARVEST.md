> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGH (§B12.1)**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-26; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# Autopilot Harvest — EVERYTHING useful mined from the installed autopilots (before removing them)

The user's install had two flight-control autopilots that could interfere with ours: **MechJeb2** and
**AtmosphereAutopilot (AA)**. Both are being REMOVED from GameData so only CLAUDE controls the vehicle. This
doc records everything useful FIRST so nothing is lost (standing instruction: *"RECORD EVERYTHING YOU USEFUL
YOU FIND SO IT IS NOT LOST"*). MechJeb's full C# source is retained at `Desktop/mechjeb_src` (re-readable any
time); AA is DLL-only in-game so its design is captured from its GitHub README
(https://github.com/Boris-Barboris/AtmosphereAutopilot).

**This is the DEEP harvest** (rev 2). Every MechJeb module was read in full — not just the attitude
controller: the four PID implementations, the SmartASS attitude-reference set, the FlightRecorder, the
ThrustController (limiter stack / g-limit / max-Q bucket / ullage / ThrustForDv), the StagingController
decision logic, the NodeExecutor burn state machine + BurnTime integral, the Rendezvous cascade, the Docking
approach state machine, and the powered-descent/landing stack. Every mission phase is covered.

> ⚠ **HARD-RULE CAVEAT — read once, applies to ALL of §E–§J.** MechJeb actuates through `StageManager`
> (`StageManager.ActivateNextStage`), `Vessel.ActionGroups[RCS/SAS/...]`, and the stock `MechJebModuleAttitude
> Controller` (which is SAS-like at the seam). Our [[direct-part-control-hard-rule]] BANS all of that. So from
> MechJeb we take the **DECISION LOGIC, formulas, thresholds, and state machines** — NOT the actuation calls.
> Every "stage", "SetGroup(RCS,true)", "attitudeTo" below is re-implemented in CLAUDE as a **direct part
> action** (ignite/shutdown a specific engine module, fire a specific decoupler, enable a specific RCS block +
> set `thrustPercentage`, drive the gimbal via `FlightCtrlState`). The math is the treasure; the plumbing is
> ours.

Both mods independently converge on the SAME architecture we need, which is strong confirmation:
**guidance → angular-velocity setpoint (with AoA moderation) → angular-acceleration / torque inversion
(using live control effectiveness) → gimbal/RCS.**

---

## A. MechJeb2 — the gimbal attitude controller (CONFIRMED, read in full)
Full detail in **`docs/ATTITUDE_CONTROL_RESEARCH.md`**. Summary of the port (`BetterController`):
- Frame: `currentAttitude = ReferenceTransform.rotation * Euler(-90,0,0)`; `requested = LookRotation(dir,up)`.
- Error: Euler of `Inverse(current)*requested`, **order (pitch,roll,yaw), yaw NEGATED**, radians, clamp ±π.
- Per axis: arrestable-rate `ω=√(2·α·(|e|−effLD))·sign(e)` (α = controlTorque/MOI), vel-PID → α,
  `torque=MOI·α`, **`actuation=−torque/controlTorque`** → `s.pitch/roll/yaw`.
- Tunings: PosKp 2.03, PosTi 1.97, VelKp 7.98, MaxStoppingTime 2 s, Soften 0.5, RollControlRange 5°.

### MechJeb torque / MOI sourcing (VesselState.cs — CONFIRMED)
- `MOI = vessel.MOI`. `angularVelocity = vessel.angularVelocity`.
- **controlTorque per axis = Σ over `ITorqueProvider` modules of `GetPotentialTorque(out pos,out neg)`,
  taking `Vector3d.Max(|pos|,|neg|)`.** ⚠ KSPCommunityFixes (installed) fixes stock gimbal GetPotentialTorque.
- **RCS torque handled SEPARATELY** (RCS is on/off + position-dependent — don't lump into linear gimbal torque).
- `TorqueReactionSpeed` models actuator LAG (gimbal `gimbalRange/gimbalResponseSpeed`). Faster ⇒ command harder.

---

## B. AtmosphereAutopilot — how to fly a FAR-UNSTABLE craft (the design we must match)
Three-tier hierarchy = our corrected stack:
1. **High-level** (FBW / AoA-hold / cruise / director) → setpoints.
2. **Mid-level** (Director, AoA & sideslip controllers) → desired **angular velocities**, WITH moderation.
3. **Low-level** (Angular-ACCELERATION controllers) → **dynamics inversion**: desired α → gimbal command,
   using control-effectiveness prediction and accounting for actuator lag.

- **Online aero identification**: real-time linear regression → per-axis aero torque/force models; estimates
  MOI, CoM, control effectiveness (our L6 self-cal, extended to estimate CONTROL EFFECTIVENESS online).
- **Angular-velocity controller**: parabolic descent to setpoint (≈ our arrestable-rate curve); linear-P
  relaxation on small errors; precision mode ×0.33 sensitivity.
- ⛔ **AoA / G / SIDESLIP MODERATION (the key we were missing):** estimate MAX achievable AoA under current
  conditions; on an unstable craft **LIMIT commanded AoA to 0.6 × the controllability-region boundary** — a
  DYNAMIC AoA cap = f(control effectiveness vs aero moment). Through max-Q that fraction → ~0 automatically.
  Pitch moderation disabled the first **2 s after takeoff** (avoids pad over-moderation).
- **Rocket mode**: pitch & yaw treated IDENTICALLY (axisymmetric rocket) — our AttitudePilot should do the same.
- **Director**: velocity-vector control → AoA/sideslip/roll-rate setpoints (the clean way to express "fly the
  velocity vector to this pitch profile" as an AoA setpoint the moderation then caps).

### ⭐ AoA MODERATION — the CONCRETE method (read from `Modules/AngularVelAdaptiveController.cs`, 2026-08-26)
The README's "cap AoA at 0.6× the controllable boundary" is realised as a **controllability-region clamp
built on an online linear model** — the concrete algorithm to port for the FAR-unstable ascent:
- **Online linear model** `lin_model` (A/B/C matrices): pitch angular-acceleration as a linear function of
  (AoA, angular velocity, control), identified live by the mod's "trainers". Our L6 `SelfCal` already
  estimates control-effectiveness `1/I`; extend it to estimate the aero-moment slope `M_α`.
- **Equilibrium solve for the max-AoA rate:** solve `A·[aoa_max; v] + C = 0` for the angular velocity
  `max_aoa_v` that holds equilibrium AT `aoa_max` given current aero+control (`eq_b[0]=−(A[0,0]·rad_max_aoa
  + C[0,0]); eq_b[1]=−(A[1,0]·rad_max_aoa + C[1,0])` → `max_aoa_v = (A⁻¹·eq_b)[0]`, filtered). ⇒ **the
  controllable AoA is where available gimbal torque can still hold equilibrium** ≡ `aoa_max ≈ M_gimbal_max/M_α`.
- **`staticaly_stable` flag** (the FAR-transonic tell): the equilibrium solution's sign reveals static
  stability. On an UNSTABLE craft the transit is MORE conservative — `transit_max_aoa = min(rad_max_aoa,
  res_max_aoa)` used **directly** (a stable craft gets the relaxed `/3.0`). ⇒ through max-Q the cap tightens.
- **Clamp commanded AoA into `[res_min_aoa, res_max_aoa]`** = min of the guidance `max_input_aoa`, the simple
  `rad_max_aoa`, the equilibrium controllable AoA, and the **G-limit** AoA `max_g_aoa` (the `moderate_g`
  section). AoA / G / sideslip moderation all reduce to shrinking this one region.
- **Only above a dynamic-pressure cutoff** (`dyn_pressure > moder_cutoff_ias²`); base scale
  `max_v_construction = 0.7`; every estimate low-pass filtered.
⇒ **CLAUDE port** (after §A's direct gimbal loop holds zero-AoA cleanly): `pure/AoaModeration.cs` — feed it the
L6 `M_α` + live `Σ GetPotentialTorque` → `aoa_max = k·M_gimbal/M_α` (k≈0.6), clamp the guidance's AoA setpoint,
tighten when `SelfCal` reports static instability, gate on q. Self-limits to ~0 through transonic/max-Q.

---

## D. REUSABLE CONTROL PRIMITIVES (PID zoo + arrestable-rate) — port into `pure/`

MechJeb ships **four** distinct PID designs. Each solves a different problem; we want all four patterns
available in `pure/` (headless-tested), selected per loop. All are tiny and dependency-free.

### D1. `PIDController` (scalar, textbook) — `MechJeb2/PIDController.cs`
`action = Kp·e + Ki·∫e + Kd·(e−ePrev)/dt`, clamped [min,max], with **conditional-integration anti-windup**:
if the clamped output ≠ raw output, **back out** this tick's integral (`INTAccum -= e·dt`). Simplest correct
anti-windup. Use for: throttle/translation speed loops (MechJeb uses it for the thrust-controller speed PID,
gains `0.05, 1e-6, 0.05`).

### D2. `PIDControllerV2` (Vector3, derivative-on-measurement) — same file
- Derivative acts on **ω (measurement), not error** → no derivative kick on setpoint changes: `Dact = ω·Kd`.
- **Anti-windup by magnitude gate**: integrate only while `|Dact| < 0.6·max`, else bleed `INT *= 0.9`.
- Overload `Compute(error, ω, wlimit)`: clamps (P+I) to an **angular-velocity limit** `±wlimit·Kd` BEFORE
  adding D — i.e. the position loop can't demand more than a set rate. This is the two-stage cascade in one
  object. (V3 = same with per-axis Vector3 gains.) Use for: the gimbal attitude inner loop if we want a
  single-object cascade instead of the explicit BetterController two-PID split.

### D3. `TorquePI` + `KosPIDLoop` — `AttitudeControllers/TorquePI.cs`, `KosPIDLoop.cs`
- **`TorquePI`**: gains are set LIVE from inertia each tick — `Ki = Kp = 4·MOI` — then `KosPIDLoop.Update`.
  ⇒ **self-tuning to the vehicle's current MOI** (mass drops through flight). This is how MechJeb's alternate
  attitude law stays tuned as propellant burns off. **We should scale our gimbal gains by live MOI too.**
- **`KosPIDLoop`** (the kOS PILoop, ported): classic PI(D) with **back-calculation anti-windup** — on output
  saturation it recomputes `iTerm = clampedOutput − (pTerm+dTerm)` so the integral exactly accounts for the
  clamp (no lingering windup). Optional **ExtraUnwind**: doubles Ki while the error sign opposes the
  accumulated error (unwinds a wound-up integral faster). D acts on the **change rate of the input** (measured),
  not error. This is the most robust scalar PID of the four — **make it our default `pure/Pid.cs`.**

### D4. `PIDLoop` (digital biquad, filtered-derivative) — `MechJebLib/Control/PIDLoop.cs`
The modern MechJebLib loop. **Trapezoidal PID discretised as a transposed-direct-form-II biquad**, with:
- **Derivative filtering** (`N`, default 50) so D doesn't amplify noise (a real filtered derivative, not raw Δ).
- **Setpoint weighting** `B` (proportional) and `C` (derivative) — reduce setpoint-change kick.
- **Per-term deadbands** (P/I/D/output) and input/output **low-pass smoothing** (`SmoothIn/SmoothOut`).
- Handles NaN internal state reset cleanly. `Ts` = 0.02 (physics tick). Use for: any loop where sensor noise
  bites (entry bank-angle, docking translation trim). Overkill for the gimbal, ideal for noisy outer loops.

### D5. Arrestable-rate / braking curve (the one law under everything)
Appears in attitude (`ω=√(2αθ)`), docking translation (`v=√(2·a·d)`), and landing (`v=√(2(T−g)h)`). One
primitive: **max speed to still stop in remaining distance = `√(2·a·d)`**, scaled by a safety factor (0.8–0.95)
and clamped. Our `pure/ControlLaw.cs` already has the attitude form — **generalise it to a `BrakeCurve(a,d,k)`
helper used by attitude, RCS translation, AND the hoverslam.**

---

## E. THRUST MANAGEMENT — `MechJebModuleThrustController.cs` (huge; the ascent/burn throttle brain)

### E1. The ORDERED LIMITER STACK (architecture to copy exactly)
Start `throttleLimit = 1`; apply each active limiter; **each may only LOWER the limit** (`SetFixedLimit` takes
the min). Order matters:
1. `THROTTLE` (user/guidance cap) 2. `DYNAMIC_PRESSURE` (max-Q) 3. `TEMPERATURE` 4. `ACCELERATION` (g-limit)
5. `ELECTRIC` 6. `FLAMEOUT`. **Then** the min-throttle floor (`MIN_THROTTLE`, keeps a limited throttle above
the engine's real minimum). **Then, and only then**, zero-authority limiters that must win: `UNSTABLE_IGNITION`
and `AUTO_RCS_ULLAGE` force **0**. Two published values: `ThrottleLimit` (actual, includes transient zeros) and
`ThrottleFixedLimit` (steady, what burn-time consumers like the node executor read — never the transient zero,
or they'd compute infinite burn time). **CLAUDE gets one `ThrottleManager` with this exact precedence.**

### E2. Max-Q throttle bucket — `MaximumDynamicPressureThrottle`
`if q/qmax < 1 → 1.0` (full); else `1 − 15·(q/qmax − 1)` clamped [0,1]. FALLOFF=15 ⇒ throttles down HARD the
instant q exceeds the cap. `MaxDynamicPressure` default 20 kPa. **Our max-Q bucket = this, with qmax tuned to
the FAR transonic peak from the flight CSVs. Respect min-throttle (SolverEngines Merlin floor) underneath.**

### E3. g-limiter (acceleration cap) — `AccelerationLimitedThrottle`
`throttle = (aMax − aMinThrust)/(aMaxThrust − aMinThrust)`, clamped [0,1]. Correctly accounts for the engine's
min-thrust floor (throttle 0 still gives `aMinThrust`). **This is our crew g-limit** (Dragon ascent/abort/entry
comfort + structural) and the booster-landing accel cap. MaxAcceleration default 40 m/s² (~4 g).

### E4. `ThrustForDv(dV, timeConstant)` — feathered burn termination (USE FOR EVERY FINITE BURN)
`tc += MaxEngineResponseTime; spooldownDv = accel·responseTime; desiredAccel = (dV − spooldownDv)/tc;
throttle = clamp(desiredAccel/aMaxThrust, 0.01, 1)`. **Throttles DOWN smoothly as dV→0**, accounting for
engine spool, so the burn ends ON the target instead of overshooting. **Our NodeExecutor/deorbit/rendezvous
burns must use this**, not bang-bang. Adaptive tc (from NodeExecutor §G): `tc = (dvLeft>10 ||
aMin>0.25·aMax) ? 0.5 : 2` — long time-constant to feather the last few m/s on a deep-throttling engine.

### E5. Smooth-throttle rate limiter — `ApplySmoothThrottle`
`throttle = clamp(cmd, last − dt/τ, last + dt/τ)`, τ=`ThrottleSmoothingTime` (1 s). Bounds throttle SLEW to
protect against thrust transients / pogo. Optional; useful on the booster landing burn.

### E6. AUTO-RCS ULLAGE (the reference implementation — env research demanded this) — `ProcessUllage`
For RealFuels: before/at ignition, if `LowestUllage < 0.996` (any chance of a bad light), **hold throttle at 0
AND fire RCS aft (`s.Z = −1`) to settle propellant**; release once `ThrustCurrent > RCSThrustAvailable.Up` (real
thrust has come up). **Don't waste ignitions**: only force-zero if `LastThrottle ≤ 0` (not already burning).
Also `LimitToPreventUnstableIgnition`: kill throttle whenever `LowestUllage < 0.996`. **CLAUDE ullage =** settle
with a direct RCS-block aft command (not `ActionGroups[RCS]`) until ullage ≥ 0.996, THEN ignite the engine
module. The 0.996 threshold and "thrust-came-up" release are the port.

### E7. Differential throttle (engine-out / extra authority) — `ComputeDifferentialThrottle`
Solves a bounded **quadratic program** (alglib minqp) to distribute per-engine `ThrustRatio` so the octaweb
produces a demanded torque WITHOUT gimbal (torque + net-force objective, bounds [0, mainThrottle]). Needs ≥2
throttleable engines. For our 9-Merlin S1 this is **spare pitch/yaw/roll authority beyond the ±5° gimbal**, and
the natural **engine-out compensation** (a failed Merlin → rebalance the other 8). We don't need a QP solver
day-one, but record the technique: differential octaweb throttle is real added authority for FDIR.

### E8. Also here: `RequestActiveThrottle` (min-throttle enforcement), temperature throttle (throttle down as
`maxTempRatio → 1` within 5% margin), electric-throttle limiter, intake management (N/A for us).

---

## F. STAGING & SEQUENCING DECISION LOGIC — `MechJebModuleStagingController.cs`
(We DON'T call `StageManager`. We port the **conditions that decide WHEN** to fire our direct decouplers /
ignite the next engine mode / drop the fairing / release the clamp.)

### F1. ⭐ LAUNCH-CLAMP / HOLD-DOWN RELEASE GATE (the real fix for flights 1–3)
> *"only release launch clamps if we're at nearly full thrust and no failed engines"*
```
if (ThrustCurrent/ThrustAvailable < ClampAutoStageThrustPct(0.99)  ||  AnyFailedEngines):
    Attitude.Controller.Reset()   // keep resetting the gimbal PID while clamped → no integral windup
    return                        // DO NOT release yet
release hold-downs                // only now
```
**This is exactly what we kept getting wrong.** Ignite → wait until measured thrust ≥ 99% of available AND no
engine failed → THEN fire the erector/clamp decouplers directly. AND while still clamped, hold the gimbal loop
integral at zero (reset every tick) so it doesn't wind up against the clamp and kick at release.
`AnyFailedEngines` = an engine that's enabled, `allowShutdown`, but `!EngineIgnited` (lit-check via the module).

### F2. FAIRING / NOSE-CONE JETTISON GATE — `WaitingForFairing`
Deploy only when ALL hold: `q < FairingMaxDynamicPressure (5 kPa)` AND `altitude > FairingMinAltitude (50 km)`
AND `FreeMolecularAerothermalFlux < FairingMaxAerothermalFlux (1135 W/m²)`. **The Dragon nose-cone "open
shroud" should use these same three gates** (don't expose the Dracos to heating/dynamic pressure early —
[[dragon-nose-cone-rcs]]).

### F3. Safe-to-separate checks (port as pre-decouple guards)
- **Never decouple an active/idle engine or a tank still feeding a running engine** (`InverseStageDecouples
  ActiveOrIdleEngineOrTank`) — walk the decoupled subtree, check for fuelled/running engines or crossfeeding
  tanks. ⇒ before we fire S1/S2 sep, confirm the dropped stack has no lit/fuelled engine we still need.
- **Don't separate while ullage is unstable if we have RCS** — settle first (`InverseStageHasUnstableEngines`
  → hold, fire RCS). ⇒ our S2 ignition after MECO coast: ullage BEFORE the sep+light.
- **Always stage if the current stage has no active engines** (flameout/burnout → drop immediately).
- **Hot-staging**: keep the spent stage until `LastNonZeroDVStageBurnTime() > HotStagingLeadTime (2 s)` — i.e.
  light the upper engine before dropping the lower (RO hot-stage). Post-delay `StagingCooldownTimer` after any
  sep to avoid re-colliding with debris.
- **Don't fire a stage that deploys a staying (non-decoupled) chute** — would shred it.

### F4. Drop-spent-booster-by-TWR — `ShouldDropSolids`
Drop a strap-on/booster stack once its **own TWR falls to `DropSolidsTwrPct` (50%) of the whole rocket's
current accel** (`maxStackAccel ≤ CurrentThrustAccel·0.5`; recursively sum the detached subtree's thrust/mass).
Space-Shuttle SRB sep was ~37%. Not directly used (F9 has no strap-ons) but this is the general **"is this
stack still pulling its weight?"** decoupler trigger — reusable for any drop-mass decision by measured accel
ratio.

---

## G. NODE / BURN EXECUTION — `MechJebModuleNodeExecutor.cs` (every finite burn: circularize, rendezvous, deorbit)

### G1. The burn state machine
`INITIAL_WARP → ALIGNING → WARPING → LEAD → BURN → IDLE`. Transitions:
- Ignite (`BURN`) when `Time ≥ ignitionUT AND Aligned` (angle < `AlignedToleranceDegrees` 1°).
- `LEAD` = within `LeadTime` (3 s) of ignition — **start ullaging here** (RCS aft) so the engine lights clean.
- Warp only while within `WarpAlignedToleranceDegrees` (10°) AND settled (`|ω| < 0.001`).
- Store the burn direction as a **de-rotated inertial vector** (`Inverse(Planetarium.rotation)·burnVec`) so it's
  stable across ticks while the planet rotates; aim via inertial (COT-corrected) reference.

### G2. ⭐ Center-of-burn timing — `CalculateIgnitionUT`
Ignite at **`node.UT − halfBurnTime`** (stock: node UT is the burn CENTER; split the Δv half before / half
after the node). Far more accurate than igniting AT the node. halfBurnTime includes spool.

### G3. ⭐ `BurnTime(dv)` — the correct multi-stage burn-time integral (use this everywhere we predict burn time)
Per stage (from the fuel-flow stage stats), Δv ∝ ln(m0/m1). For a partial burn of `stageBurnDv`:
```
finalMass = startMass / exp( ln(startMass/endMass) · (stageBurnDv/stageDv) )
avgAccel  = thrust / ((startMass + finalMass)/2)      // current stage ×= ThrottleFixedLimit
burnTime += stageBurnDv / avgAccel ;  halfBurnTime tracked in parallel ;  add SpoolUpTime
```
Handles multi-stage burns, throttle limit on the current stage, and spool. **Replaces our `dv/accel` estimate**
in the node executor / deorbit planner — the ln-mass-ratio form is exact for a constant-thrust stage.

### G4. Ullage timing detail (single-tick race fix)
Always apply a minimum `MIN_RCS_TIME` (0.25 s) of ullage RIGHT BEFORE ignition, even if ullage momentarily
reads 1.0 — avoids the race where MJ sees 100% ullage but RF decrements it the same tick and the engine
flames. Require `LowestUllage ≥ 1.0` before releasing RCS.

### G5. Termination
Stock: terminate when `AngleFromNode > 90°` (the remaining-burn vector has flipped past the node = you're
done/overshooting). Then `ThrustForDv` (§E4) feathers the last m/s so you stop on target, not past it.
`DecrementDvLeft` (measured-dv fallback): `dV = (accel_immediate − graviticAccel)·burnDir · dt` — a measured
Δv accumulator that includes RCS and even decoupler impulse.

---

## H. RENDEZVOUS — `MechJebModuleRendezvousAutopilot.cs` (the far→near cascade; our named-burn logic)
Evaluated every tick; plots ONE maneuver node for the current situation, executes it (§G), re-evaluates.
Order (far to near) — this IS the phasing→transfer→approach sequence:
1. **Not coplanar** → match planes at AN or DN (whichever is sooner). (`DeltaVAndTimeToMatchPlanes*`)
2. **Coplanar, not circular** → circularize (at Pe or Ap, whichever is nearer the target SMA).
3. **Coplanar + circular, no intercept** → if the Hohmann window is `< maxPhasingOrbits` away, plot the
   **Hohmann transfer**; else establish a **phasing orbit** to close the phase angle faster. Phasing radius
   ratio: `axisRatio = (1 + 1.25/N)^(2/3)`, phasing SMA = target SMA · axisRatio (high) or / axisRatio (low),
   picking the interior orbit when we're below the target and it clears atmosphere+3 km.
4. **On approx intercept** (`closestApproach < SMA/25`) → **match velocities at closest approach**, with the
   burn time pulled EARLIER so we come to rest at `desiredDistance`, not at zero:
   `UT −= √(desiredDist² − approachDist²)/approachSpeed`; and **stop 1 s early if `approachSpeed > 10`**
   ("coming in hot"). This is the clean "arrive at the hold point at rest" law.
5. **Close** (`< R/25`) → intercept-at-time toward the target at a closing speed `dist/100` capped at
   `maxClosingSpeed` (100).
6. **Within desiredDistance** → `DeltaVToMatchVelocities` (null relvel).
7. **Within desiredDistance·1.05 and relvel < 1** → DONE.
- **Autowarp only while distance > 1000 m.** (Below that, physics on — real approach.)
⇒ Confirms our approach law ([[falcon-rendezvous-approach-law]]): never chase; phase, transfer, then null
relvel at closest approach arriving at rest at the hold distance. The `√(d²−approach²)/speed` offset and the
"stop early if hot" rule are directly portable to our AI/close/transfer burns.

---

## I. DOCKING — `MechJebModuleDockingAutopilot.cs` (the user's explicit ask; the prox-ops/corridor brain)

### I1. Approach state machine
`INIT → [WRONG_SIDE_BACKING_UP → WRONG_SIDE_LATERAL → WRONG_SIDE_SWITCHSIDE] → BACKING_UP → MOVING_TO_START →
DOCKING → OFF`. The "wrong side" branch handles being behind the target port: back up, move laterally clear,
switch to the correct side — **it never cuts across the target**, which is exactly the KOS / keep-out
discipline. INIT picks the entry step from geometry (behind → wrong-side/back-up; off-axis in front →
move-to-start; on-axis → dock).

### I2. ⭐ Geometry (port verbatim for our R-bar/V-bar corridor math)
```
zAxis   = target.DockingAxis.normalized
zSep    = −dot(separation, zAxis)     // + if we are IN FRONT of the target port, − if behind
lateralSep = Exclude(zAxis, separation)   // perpendicular miss vector
relativeZ  = dot(relVel, zAxis) ; relativeLateral = dot(lateralSep, relVel)
```
Only close along z (`zSep`) while `lateralSep.magnitude < dockingcorridorRadius (1 m)` — i.e. **null the
lateral miss FIRST, then approach on-axis**. In DOCKING, trade z-speed vs lateral-speed by time-to-close
(`timeToAxis` vs `timeToTargetSize`) so both converge together. Capture when `zSep < acquireRange`
(= docking node `acquireRange · 0.5`).

### I3. ⭐ `MaxSpeedForDistance(d, axis)` — RCS braking curve for translation
`v = √(2 · |d| · RCSThrustAvailable(axis)·rcsAccelFactor / mass)`, clamped to `speedLimit` (1 m/s). The linear
analogue of the attitude arrestable-rate — **approach speed that can still brake to rest before contact.** Our
DockControl approach-speed law.

### I4. Keep-out / safe-distance sizing
`targetSize = target bounding-box magnitude`; `safeDistance = ownBBox + targetSize + 0.5`. **Auto-sizes the
keep-out sphere from the actual vehicle sizes** — better than a hard-coded KOS radius. `dockingcorridorRadius`
1 m is the on-axis corridor.

### I5. Attitude + translation actuation
Point the docking port at the target port: `attitudeTo(back, TARGET_ORIENTATION)` (optionally `forceRol` for a
specific docking clock angle). Translate via `RCS.SetTargetWorldVelocity(targetVel + adjustment)` where
`adjustment = −lateralSep.normalized·latSpeed + zSpeed·zAxis` — i.e. **always match the moving target's velocity
first, then add the approach vector on top** (fly in the target's frame). For us: attitude-first-then-translate
([[dragon-nose-cone-rcs]]), commanding Draco blocks directly.

---

## J. POWERED DESCENT / BOOSTER RECOVERY — `MechJebModuleLandingAutopilot.cs` + `LandingAutopilot/*`

### J1. Course-correction by orbit perturbation — `ComputeCourseCorrection` (the targeting gem)
To steer the predicted impact point to a target lat/lon: numerically perturb the orbit by 1 m/s along
{prograde, radial+, normal+}, see where each moves the planet-intersection point (`deltas[i]` in
seconds-of-shift per m/s), then solve a **2×2 linear system** for the prograde+radial and normal combination
that produces the desired landing-point offset (accounting for planet rotation during descent via
`bodyRotationDuringFall`). This is a clean **finite-difference sensitivity + linear solve** for divert Δv —
directly usable for the booster's downrange/cross-range targeting to the droneship. (`allowPrograde=false`
during the entry/decel burn → steer with radial only.)

### J2. Descent-speed policies (the "how slow must I be at this altitude" law)
- `SafeDescentSpeedPolicy`: `vmax = 0.9·√(2(T−g)·altitude)` — the hoverslam braking curve (arrestable to rest
  at the surface), 0.9 safety.
- `PoweredCoastDescentSpeedPolicy`: time-of-flight form `toF = (v + √(v²+2g·h))/g`, `vmax = 0.8(T−g)·toF`.
- `GravityTurnDescentSpeedPolicy`: **binary-search** the max entry speed such that a simulated retrograde
  gravity-turn burn (10-step forward integration) stops before impact — most accurate; 0.95 safety.
⇒ Our hoverslam speed target at each altitude = `√(2(T−g)h)`; the gravity-turn/binary-search policy is the
high-fidelity upgrade if the closed-form overshoots.

### J3. `DecelerationBurn` (entry/braking burn) — retrograde + course-correction blend
Thrust vector = `−surfaceVel.normalized` blended with the course correction: `desiredThrust = (retro +
min(0.1, correctionMag/(2·aMax))·correction).normalized` — cap the correction lean at 0.1 rad so you never
sacrifice more than a little braking to steer. **Throttle by speed error** (not bang-bang):
```
speedError  = desiredSpeed(−MaxAllowedSpeed) − controlledSpeed
desiredAccel = speedError/τ(0.3) + (desiredSpeedAfterDt − desiredSpeed)/dt   // feed-forward the policy slope
throttle = clamp((desiredAccel − minAccel)/(maxAccel − minAccel), 0, 1)      // g-normalised, like §E3
```
Only burn when pointed within ~41° of retrograde (`dot(fwd,desiredThrust) ≥ 0.75`) and descending. Warp-to /
attitude-settle before the burn (`angle<5°, |ω|<0.001`).

### J4. `FinalDescent` (the hoverslam touchdown) — thrust-mode ladder
Uses the ThrustController's velocity-hold modes:
- TWR<1 special case → `KEEP_VERTICAL`, kill horizontal, target 0.
- above 300 m → point retrograde, `KEEP_SURFACE` at the gravity-turn policy speed (or point up / retrograde and
  follow min-throttle if ascending / badly-oriented).
- last 300 m → `desiredSpeed = −lerp(0, √((T−g)·2·300)·0.9, minalt/300)` (ramp the allowed speed down to the
  touchdown speed as you near the ground); when nearly vertical (`horizVel<5`) switch to `KEEP_VERTICAL` +
  `TransKillH`, target `min(−TouchdownSpeed(0.5), desiredSpeed)`.
⇒ Confirms our hoverslam ([[falcon-real-hoverslam-technique]]): hit v=0 at h=0 on ONE engine (CenterOnly), ramp
the speed target down with altitude, kill horizontal, touch at ~0.5 m/s. `KEEP_VERTICAL` law: aim
`−horizVel + up·max(|vSpd|, 20·g)` and PID throttle on vertical-speed error.

### J5. Gear / chute deploy gates (direct-actuate versions for us)
- **Gear** below 1000 m (`ModuleWheelDeployment.EventToggle` while retracted) — for us, the KSPWheel leg module
  directly ([[dragonscreen-autopilot-rebuild-plan]]).
- **Chutes**: deploy each when `deployAltitudeASL = deployAlt·multiplier + landingSiteASL > currentASL` AND
  stowed AND `deploymentSafeState == SAFE` (won't rip at current q/Mach). The **`ParachutePlan`** learns the
  best semi-deploy multiplier by **linear regression of overshoot vs multiplier** across repeated landing
  predictions (correlation-gated: give up controlling if corr>0 i.e. no useful relationship). Our RealChute
  deploy already checks a safe state — the regression-tuned deploy altitude is a possible upgrade for splashdown
  targeting. (`LinearRegression` class = a clean rolling-window least-squares we can reuse anywhere.)

### J6. Atmosphere-brake decision — `UseAtmosphereToBrake`
Compare the **drag length** `DragLength(alt, dragCoef, mass)` to the atmosphere height: if drag slows you to
terminal before impact, don't do a decel burn, just brake on the final descent. `DecelerationEndAltitude` =
200 m + siteASL (vacuum) or `1.1·dragLength + siteASL` (atmo). ⇒ For Earth entry, the capsule brakes
aerodynamically (chutes), the booster does a powered decel — this is the switch that decides which.

---

## K. ATTITUDE-REFERENCE SET — `MechJebModuleSmartASS.cs` (all the "point at X" targets we'll ever need)
The `AttitudeReference` frames + direction expressions to build any pointing command. Each = a reference
rotation × a body direction:
- **ORBIT**: prograde `+fwd`, retro `−fwd`, normal± `∓left/right`, radial± `±up/down`.
- **SURFACE_VELOCITY**: surface prograde/retrograde (with roll/pitch/yaw offsets for entry AoA).
- **SURFACE_NORTH**: heading/pitch/roll (the launch/vertical `VERTICAL_PLUS = up`).
- **TARGET / TARGET_ORIENTATION / RELATIVE_VELOCITY**: target±, parallel± (port-to-port), relative-velocity±
  — the docking/rendezvous pointing set.
- **MANEUVER_NODE**: `forward` in node frame (burn along the node).
- **KILLROT**: hold current inertial attitude (null rates).
- **INERTIAL / INERTIAL_COT**: a fixed world vector (COT = center-of-thrust corrected — use for burns so the
  thrust axis, not the nose, aligns).
- **Smooth control**: slew the target at `degreesPerSecond` (Slerp toward it) so big attitude changes don't
  command an instant snap — good for crew comfort on the capsule.
- **SetAxisControl(pitch,yaw,roll)** — enable/disable individual axes (e.g. free roll during the gravity turn).
⇒ CLAUDE's guidance should express every aim as (reference frame, direction) and let the AttitudePilot build
the world vector — exactly this table. We already need ORBIT prograde/retro (burns), SURFACE_VELOCITY (zero-AoA
ascent + entry), SURFACE_NORTH up (liftoff), TARGET_ORIENTATION (docking), MANEUVER_NODE/INERTIAL_COT (deorbit).

---

## L. FLIGHT RECORDER — `MechJebModuleFlightRecorder.cs` (enrich our FlightRecorder.cs)
Fields per sample: TimeSinceMark, CurrentStage, AltitudeASL/True, DownRange, SpeedSurface/Orbital,
Acceleration (geeForce), **Q (dynamic pressure)**, **AoA / AoS (sideslip) / AoD**, Pitch, Mass, and the
**loss decomposition** below. `Mark()` zeroes at liftoff; records every `Precision` (0.2 s); tracks per-field
min/max; `DumpCsv()` writes a headered CSV.

### ⭐ L1. Δv loss decomposition (add these to our recorder — they diagnose ascent quality directly)
Integrated each tick:
- **GravityLosses** += `dt · dot(−orbitalVel.normalized, gravityForce)` (Δv spent fighting gravity).
- **DragLosses** += `dt · dragAcceleration`.
- **SteeringLosses** += `dt · thrustAccel · (1 − dot(orbitalVel.normalized, forward))` (Δv wasted thrusting
  off-prograde — **this is our AoA/steering penalty, quantified**; near-zero if we fly zero-AoA).
- **DeltaVExpended** += `dt · thrustAccel`. **MaxDragGees** = max drag accel / 9.81.
⇒ Adding gravity/drag/**steering** losses to our CSV tells us, per flight, exactly how much the ascent steering
is costing — the single best number for tuning the pitch program and confirming zero-AoA. Also: **mark-relative
downrange** = `bodyRadius · angle(markSurfaceVec, vesselVec)`, and **phase angle from mark** for rendezvous
timing.

---

## M. TIME WARP — `MechJebModuleWarpController.cs` (for the coast phases; direct, no dependency)
`WarpToUT(UT)`: picks the highest safe rate to arrive at UT — **regular (on-rails) warp** above the atmosphere,
**physics warp ≤ ×2** below it; auto-switches modes; won't exceed the altitude rate limit; `MinimumWarp()` to
drop to ×1 (with the "never SetRate(0) when already 0" Kraken guard). Increase-rate guards: most recent change
complete, ≥2 s since last increase, altitude allows it. ⇒ Our coast-to-burn (rendezvous phasing, deorbit
alignment) can drive time warp with this exact safe-rate ladder. NOTE: warp is a global/UI action — respect the
crew-in-the-loop gates (don't warp through a decision point).

---

## N. MAPPING — MechJeb technique → CLAUDE file (the build TODO this harvest feeds)
| MechJeb source | Technique harvested | CLAUDE target |
|---|---|---|
| `BetterController` + `VesselState` | gimbal law, GetPotentialTorque authority | `AttitudePilot` (glue) + `pure/ControlLaw`/`Authority` |
| `KosPIDLoop`, `TorquePI`, `PIDLoop`, `PIDControllerV2` | 4 PID patterns; MOI-scaled gains; biquad D-filter | `pure/Pid.cs` (default=KosPIDLoop back-calc) |
| `ThrustController` | limiter stack, max-Q bucket, g-limit, ThrustForDv, ullage 0.996 | `ThrottleManager` (glue) + `pure/` throttle laws |
| `StagingController` | clamp-release@99%thrust gate, fairing gate, safe-sep guards, hot-stage | `Actuator`/sequencer direct-fire triggers |
| `NodeExecutor` | burn FSM, center-of-burn timing, `BurnTime` integral, LEAD-ullage | `NodeExecutor`/`DeorbitOps` (glue) + `pure/BurnTime` |
| `RendezvousAutopilot` | far→near cascade, phasing ratio, arrive-at-rest offset | `NamedRendezvous`/`StationApproach` + `pure/Rendezvous` |
| `DockingAutopilot` | corridor geometry, MaxSpeedForDistance, KOS auto-size, wrong-side | `DockControl`/`WaypointApproach` + `pure/DockGeometry` |
| `LandingAutopilot`/`DecelerationBurn`/`FinalDescent` | course-correction 2×2 solve, descent-speed policies, hoverslam ladder | `BoosterRecovery` + `pure/Hoverslam`/`Landing` |
| `SmartASS` | AttitudeReference target set, smooth-slew, per-axis enable | `AttitudePilot` reference builder |
| `FlightRecorder` | gravity/drag/**steering** loss decomposition, DumpCsv | `FlightRecorder.cs` (add loss columns) |
| `WarpController` | safe-rate WarpToUT (regular vs physics) | coast-phase warp helper |
| AA (README) | 3-tier, AoA moderation @0.6× controllable, rocket mode | `AttitudePilot` moderation + `pure/AoaModeration` |

---

## O. THE SYNTHESIS FOR CLAUDE (unchanged core, now fully sourced)
1. **Guidance** outputs a desired velocity-vector direction (zero-AoA gravity turn / UPFG / burn axis / dock aim).
2. **Convert to an AoA setpoint, then MODERATE it** — cap |AoA| at ~0.5–0.6 × max-controllable AoA
   = f(Σ GetPotentialTorque, MOI, aero moment at current q). Self-limits to ~0 through max-Q. (§B, Director.)
3. **Angular-velocity setpoint** from the pointing error via the arrestable-rate curve `ω=√(2αθ)`. (§D5.)
4. **Angular-acceleration / torque inversion**: α-PID → `torque=MOI·α` → `actuation=−torque/controlTorque` →
   `s.pitch/roll/yaw`, pitch & yaw symmetric (rocket mode). Scale gains by live MOI (§D3). (§A.)
5. **Throttle** through the ordered limiter stack (§E1): max-Q bucket, g-limit, min-throttle floor, ullage-zero;
   feather every finite burn with `ThrustForDv` (§E4). Ullage before every ignition (§E6/§G4).
6. **Sequence** by DIRECT part actuation on the §F gates (clamp release @≥99% thrust + no-fail; fairing @q/alt/
   flux; safe-sep guards); burns via the §G FSM with center-of-burn timing + `BurnTime`.
7. **Rendezvous** on the §H cascade (arrive at rest at the hold distance); **dock** on the §I corridor (null
   lateral first, brake with MaxSpeedForDistance, match target velocity + approach vector); **recover the
   booster** on §J (course-correct by 2×2 solve, hoverslam speed policy, one-engine touchdown).
8. **Instrument** everything into FlightRecorder with the §L loss decomposition; drive coasts with §M warp,
   respecting crew gates.
9. Actuate the GIMBAL and RCS DIRECTLY via `FlightCtrlState` / part modules — no SAS, no action groups, no
   staging. ([[direct-part-control-hard-rule]])

One coherent controller, confirmed by two independent proven autopilots. See
`docs/ATTITUDE_CONTROL_RESEARCH.md`, `docs/INSTALLED_MODS_RESEARCH.md`, [[direct-part-control-hard-rule]],
[[dragonscreen-autopilot-rebuild-plan]].
