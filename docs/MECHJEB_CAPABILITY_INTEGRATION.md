> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGH**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-28; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# MechJeb capability integration — the "highly advanced" build plan

> **Purpose (user 2026-08-27, emphatic): "list EVERY MechJeb capability useful to our build in ANY way, roll
> them into the plan. Our autopilot MUST be highly advanced."** This is the exhaustive inventory + the
> sequenced integration. It sits ON TOP of `docs/AUTOPILOT_HARVEST.md` (which holds the decision-logic /
> formulas / thresholds already mined) and the governing `docs/AUTOPILOT_REBUILD_PLAN.md` (§4 build order).
> Source of truth: the full MechJeb C# at `Desktop/mechjeb_src` (MechJeb2/ + MechJebLib/), read in full.
>
> ⛔ Every item is ported as **decision logic / math**, actuated by DIRECT part control ([[direct-part-control
> -hard-rule]]) — never MechJeb's StageManager / ActionGroups / SAS seam.
>
> Status key: ✅ HAVE (equivalent built) · ⚠ PARTIAL (have some, missing pieces) · ❌ MISSING.
> Priority: **P0** authority/safety-critical for OUR vehicle · **P1** guidance quality · **P2** precision/
> robustness · **P3** nice-to-have/conditional · **N/A** not our vehicle.

---

## PART 1 — THE COMPLETE CAPABILITY INVENTORY

### A. Attitude & control law
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| BetterController gimbal law (arrestable-rate ω=√(2αθ), −torque/controlTorque, yaw-negated frame) | ✅ | our AttitudeLoop/AttitudePilot | `AttitudeControllers/BetterController` |
| ΣGetPotentialTorque authority + `MOI=vessel.MOI` | ✅ | our ControlTorque | `VesselState` |
| MOI-scaled gains (TorquePI: `Ki=Kp=4·MOI`, self-tunes as mass drops) | ⚠ | keeps the loop tuned all the way to a light capsule; we scale by MOI in the law but don't retune gains live | `AttitudeControllers/TorquePI` |
| **Actuator-LAG model** (`TorqueReactionSpeed`/`ResponseSpeed` from gimbalRange/responseSpeed) — command HARDER when the gimbal/RCS is slow | ❌ P0 | a faster, non-oscillating loop through max-Q; the missing half of "why is it sluggish" | `VesselState` |
| **AoA / q·α / G moderation** (cap commanded AoA at ~0.6× the online-computed max-controllable AoA; tighten when statically unstable) | ⚠ P0 | the FAR max-Q RUD safety net — self-limits to ~0 through transonic | AA + harvest §B |
| Selectable **LQR** attitude controller (optimal, `MechJebLib/Control/LQRLoop1`) | ❌ P3 | an alternative optimal law to A/B-test vs BetterController | `AttitudeControllers/LQRController` |
| Biquad **filtered-derivative PID** (`PIDLoop`, N=50, setpoint weighting, deadbands) | ❌ P3 | noise-robust loops for entry bank + docking trim (raw D amplifies sensor noise) | `MechJebLib/Control/PIDLoop` |
| SmartASS **attitude-reference set** (ORBIT pro/retro/normal/radial, SURFACE_VEL, TARGET/PARALLEL/REL_VEL, NODE, INERTIAL_COT, KILLROT) + smooth-slew + per-axis enable | ⚠ P2 | express EVERY aim as (frame, direction); COT-corrected burns; crew-comfort slew | `MechJebModuleSmartASS` |

### B. Throttle / thrust management (`MechJebModuleThrustController`)
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| Ordered limiter STACK (each may only LOWER; then min-throttle floor; then zero-authority ullage/unstable-ignition force 0). Publishes `ThrottleLimit` (actual) vs `ThrottleFixedLimit` (steady, for burn-time) | ⚠ P1 | one correct precedence engine; the two-value split stops burn-time math seeing a transient zero | §E1 |
| Max-Q bucket `1−15(q/qmax−1)` | ✅ | ControlLaw | §E2 |
| g-limit `(aMax−aMin)/(aMaxThr−aMin)` | ✅ | ControlLaw | §E3 |
| **ThrustForDv(dv, τ)** feathered burn-end (throttles down as dv→0, accounts for spool) + adaptive τ | ❌ P1 | every finite burn (deorbit/rendezvous/departure) ENDS on target, no overshoot | §E4 |
| Smooth-throttle slew limiter (τ=1 s) | ❌ P2 | protects against pogo/thrust transients on the landing burn | §E5 |
| **Differential octaweb throttle** (bounded QP: per-engine thrust ratio → torque without gimbal; engine-out rebalance) | ❌ P0 | spare pitch/yaw/roll authority beyond ±5° gimbal AND automatic engine-out compensation on the 9-Merlin S1 | §E7 |
| Terminal-velocity limiter + temperature throttle + electric throttle | ❌ P2 | keep dynamic pressure / heat safe on ascent + entry | §E8 |
| **AUTO-RCS ULLAGE** (`LowestUllage<0.996` → throttle 0 + aft RCS until real thrust; don't waste ignitions) | ✅ | our Ullage.cs (we solved it as the throttle-0 vapor-lock reset) | §E6 |

### C. Ascent guidance
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| Classic gravity-turn path (TurnStart/End/Shape) | ✅ | pure/Ascent (parameterized turn) | `AscentClassicAutopilot` |
| Launch-inclination azimuth + launch-window/RAAN | ✅ | LaunchAzimuth + LaunchWindow | `Maneuvers/Simple.HeadingForLaunchInclination` |
| **PVG optimal ascent** (`MechJebLib/PSG`): Pontryagin-optimal, **multi-stage**, **coast-arc duration OPTIMIZED**, targets FULL elements (peR/apR/**inc/LAN/argp/fpa**), enforces a **q·α (aero-load) constraint** and qMax, warm-starts from the last solution each tick | ❌ P1 | the RO-grade ascent: minimum-Δv, respects the aero-load limit that keeps RUD'ing us, hits the ISS plane+RAAN directly, optimizes the MECO coast — everything UPFG approximates | `MechJebLib/PSG/Ascent`,`AscentBuilder`,`AscentProblem` |
| Corrective steering (closed-loop heading trim) | ⚠ P3 | small ascent heading correction; our plane-lock covers most of it | `AscentSettings.CorrectiveSteering` |

### D. Staging & sequencing (`MechJebModuleStagingController`)
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| **Clamp-release gate** (≥99% thrust AND no failed engine; reset gimbal integral while clamped) | ✅ | IgnitionGate | §F1 |
| **Fairing / nose-shroud jettison gate** (q<5 kPa AND alt>50 km AND aerothermal flux<1135 W/m²) | ⚠ P1 | open the Dragon nose shroud on the SAME three physical gates — don't expose Dracos to heat/q early | §F2 |
| Safe-sep guards: never drop a lit/fuelled/crossfeeding stage; ullage before sep; hot-stage lead (2 s); post-sep cooldown; never fire a staying-chute stage | ⚠ P0 | belt-and-suspenders around every decouple (we cut+wait+decouple but don't check the full guard set) | §F3 |
| Drop-mass-by-TWR (`maxStackAccel ≤ 0.5·currentAccel`) | N/A | strap-on trigger (F9 has none) — keep as the general "still pulling its weight?" rule | §F4 |

### E. Burn execution (`MechJebModuleNodeExecutor`)
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| Burn FSM (INITIAL_WARP→ALIGN→WARP→LEAD→BURN) + de-rotated inertial burn vector | ⚠ P1 | a clean, warp-aware finite-burn executor for deorbit/rendezvous/departure | §G1 |
| **Center-of-burn timing** (ignite at `node.UT − halfBurnTime`) | ❌ P1 | burns centered on the node = far more accurate than igniting AT it | §G2 |
| **BurnTime(dv)** exact multi-stage ln-mass-ratio integral (+ spool) | ❌ P1 | correct burn-time/half-burn everywhere we predict a burn (replaces dv/accel) | §G3 |
| LEAD-ullage (min 0.25 s before every light) + terminate at AngleFromNode>90° then ThrustForDv | ⚠ P1 | clean lights + stop-on-target | §G4/G5 |

### F. Rendezvous (`MechJebModuleRendezvousAutopilot`)
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| Far→near cascade: plane-match → circularize → Hohmann/phasing → match-at-closest-approach → close → null-relvel | ⚠ P1 | our Rendezvous now has the co-elliptic far-field raise + CW terminal, but NOT the explicit **plane-match**, **phasing-ratio `(1+1.25/N)^(2/3)`**, or **arrive-at-rest** offset | §H |
| **Arrive-at-rest-at-hold** offset (`UT −= √(d²−approach²)/speed`) + "stop 1 s early if closing >10 m/s" | ❌ P1 | terminal legs that come to REST at the hold point, not overshoot into the KOS | §H4 |
| Autowarp only while range>1000 m | ⚠ P2 | fast phasing coast, physics-on for the real approach | §H |

### G. Docking (`MechJebModuleDockingAutopilot`)
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| Approach FSM incl. **wrong-side go-around** (never cut across the target) | ⚠ P1 | our DockApproach does WP holds; add the wrong-side/behind-the-port recovery | §I1 |
| Corridor geometry (`zSep`, `lateralSep`=Exclude, null lateral FIRST inside 1 m corridor) | ⚠ P1 | verbatim R-bar/V-bar corridor math; null lateral before closing | §I2 |
| **MaxSpeedForDistance** RCS braking curve `v=√(2·d·a/m)` capped 1 m/s | ⚠ P1 | approach speed that can always brake to contact | §I3 |
| **KOS auto-size** from bounding boxes (`ownBBox+targetSize+0.5`) | ❌ P2 | keep-out sized to the real vehicles, not a guessed radius | §I4 |
| Match target velocity FIRST, then add approach vector (fly in the target frame) | ⚠ P1 | correct for a moving/rotating station | §I5 |

### H. Booster recovery / powered descent (`MechJebModuleLandingAutopilot`)
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| **Course-correction by 1 m/s orbit perturbation → 2×2 linear solve** for divert Δv (planet-rotation-aware) | ⚠ P1 | precise droneship targeting: finite-difference sensitivity + solve, cleaner than our down/cross heuristic | §J1 |
| Descent-speed policies (`0.9√(2(T−g)h)`; ToF; gravity-turn binary-search) | ✅ | Hoverslam | §J2 |
| DecelerationBurn: retro+correction blend (cap lean 0.1 rad), throttle by speed-error | ⚠ P1 | entry burn that steers while braking | §J3 |
| FinalDescent hoverslam ladder + KEEP_VERTICAL kill-horizontal touchdown | ✅ | Hoverslam/BoosterDescent | §J4 |
| Atmosphere-brake decision (`DragLength` vs atmosphere → burn or coast) | ⚠ P2 | decide powered-decel vs aero-brake per vehicle | §J6 |

### I. Entry / precision landing
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| Bank-angle lifting entry (roll the lift vector, S-turns) | ✅ | Entry/EntrySteering | our build |
| **ReentrySimulation**: forward-integrate the whole reentry (drag+lift+chutes), returns Outcome (LANDED/AEROBRAKED/…), **EndASL/EndPosition/EndOrbit** | ❌ P2 | PRECISION splashdown targeting + a real entry footprint (not just an RK4 impact point) — C#, so NOT the banned Python sim | `ReentrySimulation.cs` + `LandingPredictions` |
| **Parachute-deploy regression** (learn the semi-deploy multiplier by least-squares of overshoot vs multiplier) | ❌ P2 | tune chute deploy for splashdown accuracy over flights | §J5 |
| Chute safe-state gate (won't rip at current q/Mach) | ✅ | Chutes | §J5 |

### J. Maneuver library / node planning (`ManeuverPlanner` + `MechJebLib/Maneuvers`)
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| **ChangeOrbitalElement** (single-burn change Pe/Ap/Apsis/**SMA/ECC**, root-found) | ❌ P1 | any circularize/raise/lower/trim as an exact Δv | `Maneuvers/ChangeOrbitalElement` |
| **Simple** (circularize, ellipticize, circularize-after-time, launch-inclination) | ⚠ P1 | exact circularize/ellipticize (we have Hohmann only) | `Maneuvers/Simple` |
| **Plane-match / inclination / LAN** change | ❌ P1 | fix the ISS coplanarity (our 46.5 vs 51.6 undershoot) with a real node | `OperationInclination/Lan/Plane` |
| **TwoImpulseTransfer** (Lambert-based, minimized over departure/arrival) + **Lambert (Gooding + Izzo)** | ❌ P1 | exact transfers for terminal rendezvous / any two-point intercept | `Maneuvers/TwoImpulseTransfer`,`MechJebLib/Lambert` |
| Course-correction fine-tune (closest approach to target) | ❌ P2 | trim the rendezvous intercept | `Maneuvers/FineTuneClosestApproach*` |
| Resonant orbit / moon return / interplanetary / advanced porkchop | N/A | not a LEO Crew-2 need (add later as Mission-Profile data) | `Operation*` |

### K. RCS control
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| **RCS BALANCER** (threaded solver: given a translation direction, per-thruster throttles for PURE translation with ~0 net torque, CoM-shift aware, cached) | ❌ **P0** | THE Dragon capability we lack — 16 Dracos share rotation+translation and no reaction wheels; balanced translation stops the coupling/waste that forces our attitude-first-then-translate stutter | `MechJebModuleRCSController`/`RCSBalancer` |
| SmartRcs (RCS-driven SmartASS) + RCS thrust-available per axis | ⚠ P1 | RCS attitude when the gimbal is off; per-axis RCS authority (we have RcsThrustN) | `MechJebModuleSmartRcs` |

### L. Derived vehicle state (`VesselState` — the nav layer)
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| q, Mach, orbit elements, torque-available, MOI, ullage, thrust min/cur/max, accel | ✅ | our L1 nav + recorder | `VesselState` |
| **AoS (sideslip)** + AoD (angle of drag) | ❌ P2 | full aero attitude — sideslip matters on the unstable ascent | `VesselState.AoS/AoD` |
| **CoL / CoT / DoT** (centre of lift/thrust, direction of thrust) | ❌ P2 | static-margin awareness + thrust-axis (COT) alignment for burns | `VesselState` |
| **FreeMolecularAerothermalFlux** | ❌ P1 | the nose-shroud/fairing jettison gate quantity (§F2) | `VesselState` |
| **TerminalVelocity()** | ❌ P2 | the atmosphere-brake decision (§J6) | `VesselState` |
| **AltitudeBottom** (lowest-part ground clearance, not CoM alt) | ❌ P1 | correct hoverslam touchdown height (legs, not CoM) | `VesselState` |
| Torque response-lag models (see A) | ❌ P0 | faster loop | `VesselState` |

### M. Time warp (`MechJebModuleWarpController`)
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| Safe-rate `WarpToUT` (regular above atmo, physics ≤×2 below; Kraken guards) | ⚠ P2 | drive the rendezvous/deorbit-alignment coasts (we only warp to the launch window) — respect crew gates | §M |

### N. Instrumentation (`MechJebModuleFlightRecorder`)
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| Rich per-sample record (q, AoA/AoS, mass, accel, downrange, phase-angle) | ✅ | our FlightRecorder (richer for our purpose) | §L |
| **Δv LOSS DECOMPOSITION** (gravity / drag / **steering** loss = `dt·thrustAccel·(1−dot(v̂,fwd))`) | ❌ P1 | the single best ascent-quality number — quantifies the AoA/steering cost, confirms zero-AoA | §L1 |

### O. Misc
| Cap | Status | What it buys us | Source |
|---|---|---|---|
| **StageStats** (live per-stage ΔV / TWR / burn-time, atmo+vac, via FuelFlowSimulation) | ❌ P1 | real fuel budgeting: booster-recovery reserve as a NUMBER, abort-to-orbit feasibility, MECO energy | `MechJebModuleStageStats` + `MechJebLib/FuelFlowSimulation` |
| High-order adaptive ODE integrators (DP5/DP8/Tsit5, dense output, events) | ❌ P3 | more accurate predictors than fixed-step RK4 | `MechJebLib/ODE` |
| TargetController (target mgmt/selection) | ⚠ P3 | richer than v.targetObject | `MechJebModuleTargetController` |
| SpinupController (spin-stabilize before a burn on a control-less stage) | ❌ P3 (conditional) | fallback if a coasting stage loses attitude authority | `MechJebModuleSpinupController` |
| Deployable / Solar / Antenna auto-management | ❌ P3 | auto power/thermal/comms during coast | `Deployable*Controller` |
| Spaceplane guidance / FlyingSim / Rover / interplanetary | N/A | not Falcon/Dragon LEO | — |

---

## PART 2 — SEQUENCED INTO THE BUILD (rolled into `AUTOPILOT_REBUILD_PLAN.md` §4)

Ordered by priority; each names the target file(s). ✅-items are done; below are the ADDS.

### P0 — authority & safety for OUR exact vehicle (do these next, they gate "highly advanced")
1. **RCS Balancer** — `pure/RcsBalance.cs` (given a body-frame translation demand + the thruster geometry, solve per-thruster throttles for pure translation, min torque; port the RCSSolver least-squares, cache by direction) + `Actuator` per-thruster `thrustPercentage`. Kills the attitude-first-then-translate stutter on the Dragon. Headless-testable on a thruster layout.
2. **Engine-out differential throttle** — extend the throttle law: `pure/DiffThrottle.cs` (distribute per-engine thrust to make a demanded torque without gimbal; bounds [0, main]) → `Actuator` per-engine `thrustPercentage`. Octaweb authority + automatic engine-out compensation (FDIR ties in).
3. **AoA / q·α moderation** — `pure/AoaModeration.cs` (harvest §B): cap commanded AoA at `k·M_gimbal/M_α` from L6 SelfCal's `M_α` + live ΣGetPotentialTorque; tighten when statically unstable; gate on q. The max-Q RUD net.
4. **Actuator-lag term** — feed `TorqueReactionSpeed` (gimbal/RCS response) into AttitudeLoop so it commands harder when the actuator is slow (faster, non-oscillating loop).
5. **Full safe-sep guard set + fairing/flux gate** — `pure/StageGuards.cs` (never drop a lit/fuelled/crossfeeding stage; ullage-before-sep; nose-shroud on q<5 kPa & alt>50 km & flux<1135) using new `VesselState`-style derived quantities (`FreeMolecularAerothermalFlux`, `AltitudeBottom`).

### P1 — guidance quality (the leap to "advanced")
6. **PVG optimal ascent** — the big one. Either port `MechJebLib/PSG` (multi-stage, coast-optimized, q·α-constrained, full-element target) into `pure/Pvg.cs`, or upgrade UPFG with a q·α constraint + coast-arc + inc/LAN targeting. Replaces `Upfg` as the S2 (and ideally whole-ascent) guidance. Headless-test against the point-mass closure + a known optimal case.
7. **Finite-burn executor** — `pure/BurnTime.cs` (exact ln-mass multi-stage integral) + `pure/ThrustForDv` feather + center-of-burn timing, wired into deorbit/rendezvous/departure so every burn ends ON target.
8. **StageStats** — `pure/StageStats.cs` over `FuelFlowSimulation` (already referenced): live ΔV/TWR/burn-time per stage → booster reserve, abort-to-orbit feasibility, MECO energy as real numbers (retires guessed fractions).
9. **Maneuver library** — `pure/Maneuvers.cs` (ChangeOrbitalElement Pe/Ap/SMA/ECC; circularize/ellipticize; **plane-match/inc/LAN**) + `pure/Lambert.cs` (Izzo) + `pure/TwoImpulseTransfer.cs`. Fixes the ISS coplanarity (real plane-match node) and gives exact terminal-rendezvous transfers.
10. **Rendezvous cascade completeness** — extend `pure/Rendezvous.cs`: explicit plane-match step, phasing-ratio `(1+1.25/N)^(2/3)`, arrive-at-rest offset `UT−=√(d²−approach²)/speed`, stop-if-hot.
11. **Docking corridor upgrade** — `pure/DockControl.cs`: MaxSpeedForDistance brake, null-lateral-first inside a 1 m corridor, match-target-velocity-then-approach, KOS auto-size, wrong-side go-around.
12. **Δv loss columns** — `FlightRecorder`: gravity / drag / **steering** loss. The ascent-quality readout.

### P2 — precision & robustness
13. **ReentrySimulation predictor** — `pure/ReentryPredict.cs` (C# forward-integrate drag+lift+chutes → EndASL/Outcome) for splashdown targeting + the entry footprint; feeds `EntrySteering`. (C#, allowed; not a Python physics sim.)
14. **Course-correction 2×2 solve** — `pure/CourseCorrect.cs` (1 m/s perturbation sensitivity → 2×2 divert) for the booster droneship + capsule splashdown.
15. **Parachute-deploy regression** — extend `SelfCal` (least-squares overshoot-vs-multiplier) for chute-deploy altitude.
16. **Throttle extras** — smooth-throttle slew + terminal-velocity + temperature limiters into the throttle stack.
17. **WarpController** — `WarpHelper` safe-rate ladder for the rendezvous/deorbit coasts (gate-aware).
18. **SmartASS reference builder** — express every guidance aim as (frame, direction) incl. INERTIAL_COT for burns; smooth-slew for crew comfort.
19. **Derived-state completeness** — add AoS, CoL/CoT/DoT, terminal velocity to L1 nav + recorder.

### P3 — nice-to-have / conditional
20. Biquad filtered-derivative PID for entry-bank + dock-trim (noise).
21. Selectable LQR attitude controller (A/B vs BetterController).
22. High-order adaptive ODE integrators for the predictors.
23. SpinupController (only if a control-less coast stage ever appears).
24. Deployable/solar/antenna auto-management (power/thermal during coast).

### N/A (record, don't build)
Spaceplane/aircraft autopilot, rover, interplanetary transfer, moon return, resonant orbit, advanced porkchop — not a Crew-2 LEO need; add later as Mission-Profile data if a future mission wants them.

---

**Cross-refs:** formulas/thresholds in `docs/AUTOPILOT_HARVEST.md` (§A–§O); build order in `docs/AUTOPILOT_REBUILD_PLAN.md` §4; attitude law in `docs/ATTITUDE_CONTROL_RESEARCH.md`; env in `docs/INSTALLED_MODS_RESEARCH.md`.
