# MECHJEB CAPABILITY CHECKLIST — tick what we need for our autopilot

> **How to use:** change `- [ ]` to `- [x]` on every capability you want in the DragonScreen autopilot.
> I'll read your ticks straight back and turn them into the build plan. Enumerated from the FULL source
> (79 MechJeb2 modules + ~100 MechJebLib files + the sub-features inside the big modules) so nothing is
> missed. Items already ported/built are tagged **[HAVE]**; the two legacy attitude laws and the
> rover/aircraft/UI/infra sections at the end are listed for completeness even though they're almost
> certainly N/A for a Crew Dragon. ⚠ a few modules exist only in the installed RO DLL (marked *installed-only*).

---

## A. NAV / STATE ESTIMATION
- [ ] **VesselState** — the derived-truth layer: orbit (ap/pe/inc/LAN/argP/period/ecc/SMA/ttAp/ttPe), frames (up/north/east/forward/normal), rates (angular velocity/momentum, MoI), aero (q, maxQ, Mach, AoA/AoS, density, drag/lift vectors, aerothermal flux), propulsion (thrust avail/current/min, max/min thrust accel, engine response time, RCS thrust available per-direction, lowest ullage). **[HAVE]** (our own nav)
- [ ] **Torque availability** — sum reaction-wheel + engine-gimbal (r×F) + RCS torque per axis → `TorqueAvailable`. **[HAVE]** (Campaign-6 geometric estimate)
- [ ] **Center-of-thrust / center-of-mass / center-of-lift tracking** — live CoT/CoM/CoL for off-axis thrust correction.
- [ ] **Vector6** — the 6-direction (±x/±y/±z) RCS/force bookkeeping helper.
- [ ] **Orbit/Vessel/Part/CelestialBody extension math** — helpers: time-of-node, closest-approach, radius-at-time, SMA-from-apsides, etc. (`OrbitExtensions`, `VesselExtensions`, `CelestialBodyExtensions`, `PartExtensions`).

## B. ATTITUDE CONTROL
- [ ] **attitudeTo(target, frame)** — point the vehicle at a direction / quaternion / heading-pitch-roll. **[HAVE]**
- [ ] **13 reference frames** — INERTIAL, INERTIAL_COT (off-thrust corrected), ORBIT, ORBIT_HORIZONTAL, SURFACE_NORTH(_COT), SURFACE_VELOCITY, TARGET, RELATIVE_VELOCITY, TARGET_ORIENTATION, MANEUVER_NODE(_COT), SUN, SURFACE_HORIZONTAL. **[HAVE]** (subset)
- [ ] **BetterController** — cascade setpoint-PID + slew-and-stop curve (the default; our verdict = keep). **[HAVE]** (ported)
- [ ] **LQRController** — per-axis optimal regulator (verdict: not for our RCS; maybe gimbal experiment).
- [ ] **HybridController** — cascade + inertia feedforward (predecessor).
- [ ] **MJAttitudeController** — the original PID (legacy, wobbles in RSS/RO).
- [ ] **KosAttitudeController** — kOS SteeringManager PID port (legacy).
- [ ] **Kill-rotation / hold-current-attitude** — null all angular rates and hold.
- [ ] **RollControlRange (point-then-roll)** — don't spend roll authority until pitch/yaw are within N°. **[HAVE]**
- [ ] **Auto-enable RCS on large attitude error** — turn RCS on above 3° error, off inside 0.4°.
- [ ] **SmartASS** — one-click attitude presets: prograde/retrograde/normal±/radial±/target±/rel-velocity/kill-rot/surface heading-pitch-roll.
- [ ] **Attitude Adjustment (live PID tuning)** — in-flight controller gain tuning UI.
- [ ] **⭐ PWPF / phase-plane RCS modulation** — *NOT in MechJeb for RCS* (only its hoverslam throttle). Port `DeltaSigmaThrottleModulator` + `PIDLoop2` deadbands to the Draco path — the fix for Campaign-6 chatter. **(our improvement, build)**

## C. THRUST & THROTTLE
- [ ] **Throttle write / TargetThrottle** — the base throttle command to `mainThrottle`. **[HAVE]**
- [ ] **ThrustForDv(dV, τ)** — feathered burn cutoff accounting for engine spool (ends a finite burn smoothly).
- [ ] **Max-Q limiter** — throttle down above a dynamic-pressure ceiling. **[HAVE]** (q·α moderation, first-cut)
- [ ] **Acceleration (g) limiter** — cap felt g by throttle. **[HAVE]** (Campaign-5, min-throttle-floor aware)
- [ ] **Min-throttle floor** — keep a limited throttle above a floor (RealFuels min-throttle). **[HAVE]**
- [ ] **Unstable-ignition prevention** — kill throttle if RF ullage < 0.996 (don't light on bad ullage).
- [ ] **Auto-RCS ullaging** — fire fore-RCS to settle propellant before/at ignition (RealFuels).
- [ ] **Differential throttle** — per-engine `thrustPercentage` (alglib QP) for engine-out compensation / extra pitch-yaw authority.
- [ ] **Temperature limiter** — throttle down when a part nears maxTemp.
- [ ] **Electric-charge limiter** — throttle ion/electric engines by remaining EC.
- [ ] **Jet-flameout prevention + intake management** — air-breathing only (N/A for us).
- [ ] **Smooth throttle** — rate-limit throttle change.
- [ ] **User throttle cap** — hard max-throttle limit.
- [ ] **Translatron** — speed-hold modes: KEEP_ORBITAL / KEEP_SURFACE / KEEP_VERTICAL (+ kill-horizontal) throttle control.

## D. RCS CONTROL
- [ ] **RCS velocity controller** — drive RCS to a target/relative velocity via PID (station-keeping, docking approach). **[HAVE]** (our translation glue)
- [ ] **SetTargetWorldVelocity / VelocityError / TargetRelative** — the three RCS velocity command modes.
- [ ] **RCS Balancer (smart translation)** — threaded QP (`RCSSolver`) distributing translation across thrusters to minimize wasted/rotational side-effects. (rotation balancing *not* supported.)
- [ ] **RCSSolver** — the actual per-thruster force-distribution optimizer (cached, threaded).
- [ ] **Smart RCS** — RCS attitude-hold presets (like SmartASS but on RCS).
- [ ] **Conserve-fuel deadband** — skip RCS below a velocity-error threshold.

## E. STAGING & FUEL
- [ ] **Autostage** — fire the next stage at flame-out/depletion, with all the safety gates. **[partial]** (we do direct-decoupler)
- [ ] **Safe-to-separate gates** — don't drop active/idle engines or non-empty tanks; wait for spent.
- [ ] **Hot-staging support** — ignite the upper stage before dropping the lower (lead-time gated).
- [ ] **Fairing / shroud jettison gating** — drop only when q < X AND alt > Y AND aerothermal flux < Z. **[partial]** (nose-shroud logic)
- [ ] **Drop-solids-early by TWR** — jettison a spent booster stack when its TWR falls below a fraction of the whole.
- [ ] **Launch-clamp release gate** — release clamps only at ≥99% thrust, no failed engines.
- [ ] **Autostage limit / stop-at-stage** — never stage below stage N (protect the insertion stage).
- [ ] **⭐ Early MECO trigger (staging trigger / dynamic-pressure trigger)** — sep a stage before depletion *(installed-only)* — needed to **drop the booster with landing fuel**. **(verify installed build; likely build)**
- [ ] **StageStats (per-stage ΔV/thrust/Isp/burn-time)** — the fuel-flow-derived stage table everything reads. **[HAVE]** (FuelFlowSim ported)
- [ ] **FuelFlowSimulation** — simulate the whole stack draining to produce StageStats. **[HAVE]** (ported)

## F. ASCENT GUIDANCE
- [ ] **PVG / PSG optimal ascent** — multi-phase optimal-control solve (min-propellant), target orbit as constraints. **(target; UPFG is our interim)**
- [ ] **Classic gravity-turn ascent** — turn-start/end altitude, turn-shape exponent, auto-path.
- [ ] **UPFG / PEG linear-tangent guidance** — our interim optimal ascent. **[HAVE]** (core built)
- [ ] **Guidance executor (fly a PVG solution)** — pitch/heading + throttle + precise engine cutoff from a Solution.
- [ ] **Terminal precise cutoff** — predict 1–2 ticks ahead, cut at target orbit exactly.
- [ ] **Terminal RCS trim** — finish the last bit of insertion on RCS for accuracy.
- [ ] **⭐ Launch into plane of target** — time liftoff to the target's plane + set inclination from it (the rendezvous-launch). **(build — the fix for "rendezvous doesn't work")**
- [ ] **Launch to target LAN** — time liftoff to a LAN, manual inclination.
- [ ] **Launch to manual LAN** — time liftoff to a chosen LAN.
- [ ] **Auto-warp launch countdown** — warp to the timed launch instant.
- [ ] **Corrective steering** — feedback-correct the ascent trajectory (classic).
- [ ] **AoA limiter (ascent)** — cap angle of attack in atmosphere.
- [ ] **Q-alpha limiter (ascent)** — cap aero side-load (pressure × AoA). **[HAVE]** (first-cut)
- [ ] **Force roll / vertical roll / turn roll** — command a specific roll program on ascent.
- [ ] **Auto-deploy solar panels / antennas on ascent** — deploy once safely in vacuum.
- [ ] **Skip circularization** — cut guidance at insertion without a separate circularize burn.
- [ ] **Attach / burnout altitude** — where PVG attaches the target orbit (=orbit alt for clean circular).
- [ ] **Desired FPA insertion** — target a flight-path angle at cutoff (suborbital/transfer).
- [ ] **Coast phases (before / during / after) — burn-coast-burn** — optimal coast insertion.
- [ ] **Per-stage optimize / unguided / fixed stages** — PVG stage handling flags.
- [ ] **Spinup controller** — spin-stabilize a stage before ignition (spin-stabilized upper stages).
- [ ] **Vertical-ascent → pitch-program → guidance FSM** — the ascent mode sequence. **[HAVE]** (our ascent FSM)

## G. MANEUVER NODES & EXECUTION
- [ ] **Node Executor** — fly a maneuver node: warp → align → lead → feathered burn → next. **[partial]** (our burns)
- [ ] **Execute-one-node / execute-all-nodes** — single vs chained node execution.
- [ ] **RCS-only node execution** — burn a node on RCS translation (no main engine) — a Dragon on Dracos.
- [ ] **Auto-warp to node** — coarse + fine warp to ignition with alignment gating.
- [ ] **Node Editor** — create/tweak a maneuver node by ΔV components + time.
- [ ] **Principia support** — node execution under the Principia n-body integrator (N/A unless we run Principia).

## H. ORBITAL MANEUVER LIBRARY (the maneuver planner — `OrbitalManeuverCalculator` + `MechJebLib/Maneuvers`)
- [ ] **Circularize** — at apoapsis/periapsis/now. **[HAVE]** (Hohmann/pure)
- [ ] **Change apoapsis** — raise/lower Ap.
- [ ] **Change periapsis** — raise/lower Pe (deorbit target). **[HAVE]** (DeorbitGuidance)
- [ ] **Ellipticize (change both apsides)** — set Ap and Pe together.
- [ ] **Change inclination** — at a node.
- [ ] **Match planes with target (asc/desc node)** — null relative inclination. **[HAVE]** (rendezvous uses it)
- [ ] **Hohmann transfer to target** — the phasing/intercept transfer. **[HAVE]** (Hohmann)
- [ ] **Match velocities with target** — kill relative velocity at a point.
- [ ] **Intercept target at chosen time (course intercept)** — DeltaVToInterceptAtTime.
- [ ] **Bi-impulsive / Lambert transfer to target** — two-burn transfer solving Lambert (Gooding + Izzo). **(build — better rendezvous)**
- [ ] **Fine-tune closest approach** — small burn to tighten the approach distance.
- [ ] **Change SMA / period** — set orbital period (resonant orbits, ground-track).
- [ ] **Change surface longitude of apsis** — rotate the apsis to a ground longitude.
- [ ] **Kill relative velocity** — zero velocity vs target now.
- [ ] **Change generic orbital element** — set any single element (`ChangeOrbitalElement`).
- [ ] **Return-from-moon** — patched-conic return burn (N/A for LEO Dragon).
- [ ] **Interplanetary / advanced (porkchop) transfer** — planet-to-planet (N/A for Dragon).
- [ ] **Single-impulse hyperbolic burn** — ejection/escape targeting (N/A for Dragon).

## I. RENDEZVOUS
- [ ] **Rendezvous autopilot (full decision tree)** — plane-match → circularize → phasing orbit → Hohmann intercept → close → match velocities → done. **[partial]** (our CW+Hohmann)
- [ ] **Phasing-orbit establishment** — raise/lower to a phasing radius to catch the target within N orbits.
- [ ] **Max phasing orbits / max closing speed / desired distance** — the rendezvous tuning params.
- [ ] **Coplanar-first logic** — cheap Hohmann when in-plane; expensive plane-match only when off-plane (why coplanar launch matters).

## J. DOCKING
- [ ] **Docking autopilot (corridor FSM)** — align to the port axis, get on the correct side, approach the corridor, dock. **[partial]** (our L3 dock)
- [ ] **Arrestable approach speed — √(2·a·d)** — never approach faster than RCS can brake. **[HAVE]** (dock corridor)
- [ ] **Bounding-box safe distance / target size** — auto-size the keep-out + corridor from part bounding boxes.
- [ ] **Wrong-side avoidance** — back up / switch sides if behind the port.
- [ ] **Force-roll docking** — hold a commanded roll to the target port.
- [ ] **Speed limit / acquire range** — cap docking speed; detect capture range.

## K. LANDING / POWERED DESCENT / ENTRY
- [ ] **Landing autopilot — land at target (coords/KSC)** — precision powered landing to a chosen LZ. **(booster recovery)**
- [ ] **Landing autopilot — land somewhere** — nearest-safe powered landing.
- [ ] **Deceleration / suicide burn (hoverslam) timing** — start the landing burn so v→0 at h→0 (HoverslamSimulation). **[partial]** (our hoverslam constraint)
- [ ] **Hoverslam autopilot** — the dedicated suicide-burn controller (uses the delta-sigma throttle modulator).
- [ ] **Landing Predictions** — atmospheric trajectory integration to the impact point (à la Trajectories). **(build — entry predictor)**
- [ ] **ReentrySimulation** — the reentry integrator (drag/lift, rotating frame, to impact).
- [ ] **Auto-deploy gear on landing** — legs out at a gated altitude/stage.
- [ ] **Auto-deploy chutes on landing** — parachutes at a gated stage. **[partial]** (our chute sequence)
- [ ] **RCS approach adjustment** — RCS-trim the final descent.
- [ ] **Deorbit burn planning** — burn to a landing site / entry corridor. **[HAVE]** (DeorbitGuidance/DeorbitBurn)
- [ ] **⭐ CoM-shifter lifting entry + bank steering** — offset-CoM descent mode + bank-angle footprint steering (real Dragon). **[HAVE]** (first-cut; our own, not MechJeb)

## L. DEPLOYABLES
- [ ] **Solar panel controller** — auto-deploy/retract + track the sun.
- [ ] **Deployable antenna controller** — auto-deploy antennas (comms/RealAntennas).
- [ ] **Generic deployable controller** — base for animated-deployable parts.

## M. TIME WARP
- [ ] **Warp controller — WarpToUT / WarpToEvent** — warp to a time or an orbital event (ap/pe/node/SoI). **[HAVE]** (WarpPlan / CoastEta)
- [ ] **Min-warp / regulate physics warp** — drop to 1× for burns; hold phys-warp safely.
- [ ] **Warp Helper** — warp-to-phase-angle / manual warp presets.

## N. TARGETING
- [ ] **Target controller** — the selected target + relative position/velocity/distance, docking axis, target orbit. **[partial]** (we read v.targetObject)
- [ ] **Set target (vessel / body / position / docking port)** — programmatic target selection.

## O. INSTRUMENTATION
- [ ] **Flight Recorder** — record flight data to a time series. **[HAVE]** (our FlightRecorder, richer)
- [ ] **Flight Recorder Graph** — plot recorded channels in-game.
- [ ] **Info Items / Custom Info Windows** — the live readout catalog (ΔV, TWR, suicide-burn, node burn time, etc.).
- [ ] **Debug Arrows** — draw force/vector debug arrows in the scene.
- [ ] **Trajectory draw** — render the PVG/landing predicted path on the map.

## P. MechJebLib — MATH & CONTROL PRIMITIVES (the pure port library)
- [ ] **PIDLoop2** — 2-DOF PIDF: setpoint-weight (B/C), filtered-derivative (N), trapezoidal, deadbands, back-calc anti-windup, Clegg. **[HAVE]** (via ControlLaw)
- [ ] **PIDLoop / PIDController / PIDControllerV2** — simpler scalar/vector PIDs.
- [ ] **Biquad** — a biquad digital filter (smoothing).
- [ ] **LQRLoop1** — the per-axis LQR loop.
- [ ] **DeltaSigmaThrottleModulator** — PWPF-family pulse modulator (port to RCS — see §B). **(build — our RCS improvement)**
- [ ] **TorquePI / KosPIDLoop / DirectionTracker** — attitude-controller inner primitives.
- [ ] **Lambert solvers (Gooding + Izzo)** — two-point boundary transfer (rendezvous/intercept).
- [ ] **Two-body propagation (Farnocchia + Shepperd)** — analytic Kepler state advance.
- [ ] **PSG optimizer + terminals** — the PVG interior-point solver + terminal constraint sets (Kepler3/4/5, FlightPathAngle3/4/5-Energy).
- [ ] **Astro functions** — state↔elements, node times, MinimumTimeToPlane/TimeToPlane, ECIToPitchHeading, orbital energy.
- [ ] **Angles / Interpolants** — angle wrapping + interpolation helpers.
- [ ] **ODE integrators (BS3, DP5, DP8, Tsit5) + event detection** — Runge-Kutta for the trajectory/entry sims.
- [ ] **Root-finding (Bisection, Brent, Newton)** — scalar solvers.
- [ ] **Minimization (Brent)** — 1-D minimizer.
- [ ] **AutoDiff (Dual numbers)** — automatic differentiation for the optimizer gradients.
- [ ] **Primitives (V3, M3, Q3, H1/H3 quaternions, Vec)** — the vector/matrix/quaternion math types.
- [ ] **AsyncJob / ObjectPool / Statics** — the off-thread job runner + allocation-free helpers (infra for the solver).

## Q. PROBABLY N/A FOR A CREW DRAGON (listed so nothing is missed)
- [ ] **Rover autopilot** — drive-to-waypoint, traction/brake control, waypoint window (`RoverController`, `RoverWindow`, `WaypointWindow`).
- [ ] **Airplane autopilot** — level flight / heading / altitude hold *(installed-only)*.
- [ ] **Spaceplane autopilot + guidance** — runway approach + autoland *(installed-only)*.
- [ ] **Menu / windows / GUI framework** — `MechJebModuleMenu`, `DisplayModule`, `GuiUtils`, `GLUtils`, `CustomWindowEditor`, toolbar (we have our own screens).
- [ ] **Core / module system** — `MechJebCore`, `ComputerModule`/UserPool, `AutopilotModule`, `Settings` (we have MissionConductor).
- [ ] **Install / compatibility / bundles / localization** — `InstallChecker`, `CompatibilityChecker`, `MechjebBundlesManager`, `CachedLocalizer`, `ToolbarWrapper` (infra, not flight).
- [ ] **MechJeb part (AR202) + ModExtensionDemo** — the physical MechJeb part + the extension-API demo.

---
*Enumerated 2026-08-30 from `Desktop/mechjeb_src` (⚠ older than the installed RO DLL — the *installed-only*
items come from the live cfg). Companion to `docs/MECHJEB_MASTER_MAP.md`.*
