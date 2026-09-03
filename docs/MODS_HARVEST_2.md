> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE (the TCA source behind B3)**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-28; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.
> ⚠ This is the document M1 was built without — it is why **ThrottleControlledAvionics** was missed from the mod register.

# Mods harvest, round 2 — TCA, KerbalEngineer, MFI, FAR (+ full install scan)

> **Why (2026-08-27):** the user added ThrottleControlledAvionics, KerbalEngineer, ModularFlightIntegrator and
> asked to harvest all installed mods for anything useful to the plan. These ship DLL-only (no local C#), so —
> **standing method (user, "always use this if you can't read the dll's"): find the mod's GitHub and read the
> source there.** Everything below is read from the actual repos. Actuate by DIRECT part control, always
> ([[direct-part-control-hard-rule]]); we take the math/decision logic, never the plumbing.

Repos: [allista/ThrottleControlledAvionics](https://github.com/allista/ThrottleControlledAvionics) ·
[jrbudda/KerbalEngineer](https://github.com/jrbudda/KerbalEngineer) ·
[sarbian/ModularFlightIntegrator](https://github.com/sarbian/ModularFlightIntegrator) ·
[ferram4/Ferram-Aerospace-Research](https://github.com/ferram4/Ferram-Aerospace-Research)

---

## 1. ThrottleControlledAvionics (TCA) — the P0 gold: engine + RCS thrust-limiter balancing
TCA's ENTIRE technique is *"change thrust limiters of engines and RCS thrusters in real time to control both
total thrust AND torque."* That is exactly our two P0 items (**RCS balancer** + **engine-out differential
octaweb throttle**). Both solvers (`Modules/EngineOptimizer.cs`, `Modules/RCSOptimizer.cs`) share one method.

### 1a. ⭐ The balancing solver (read from EngineOptimizer.cs / RCSOptimizer.cs)
An **iterative torque-nulling projected descent** over the per-effector thrust-limiters (NOT a closed-form QP):
```
target = needed_torque − cur_imbalance                         // residual torque to fix this pass
for each effector e (engine or RCS thruster), each pass:
    limit_tmp = −dot(e.currentTorque, target̂) / e.currentTorque_m · e.torqueRatio   // its projection on target
    limits_norm = clamp01(target_m / compensation_m)                                 // normalise by available comp
    e.limit   = clamp01( e.limit · (1 − limit_tmp · limits_norm) )                   // the update
fitness = torque_error (‖residual angular accel‖²) + AngleErrorWeight·angle(achieved, demanded)
keep best-so-far; converge when error < TorqueCutoff OR |Δerror| < OptimizationPrecision·last_error
```
- **Constants:** `MaxIterations = 50`/frame, `OptimizationPrecision = 0.01`, `OptimizationTorqueCutoff = 1.0`,
  `OptimizationAngleCutoff = 45°`, `AngleErrorWeight = 1.0`. Restores the best limit-set; flags failure if a
  nonzero demanded torque could not be met (→ our FDIR "insufficient authority" signal).
- **Roles drive the objective:** *Maneuver* effectors are MINIMISED (fire only on control input — our Dracos,
  and the octaweb's differential trim); *Thrust&Maneuver* are MAXIMISED (produce the main thrust — the octaweb
  liftoff). This is the clean way to say "keep full thrust, steal a little for torque."

### 1b. ⭐ RCS balancing = pure translation (our P0 RCS balancer)
For a demanded TRANSLATION with **zero net torque**: run the same solver with `needed_torque = 0` and the
translation force as the thrust objective → it finds the thruster-limiter set that translates while nulling
torque. TCA warns: a physically unbalanced layout can null RCS thrust almost entirely — so cap the correction.

### 1c. Two proven solver options for our P0 (pick per effector set)
| Approach | Source | Best for |
|---|---|---|
| **Iterative torque-nulling descent** (above) | TCA | general/asymmetric layouts, engine-out; robust, bounded 50 iters |
| **Bounded QP** (alglib minqp) | MechJeb `ComputeDifferentialThrottle` | when a true optimum is wanted |
| **Least-squares pseudo-inverse + clamp** | (ours to add) | our SYMMETRIC octaweb ring + Dracos — simplest, closed-form, headless-testable; fall back to the iterative descent if clamping bites |
⇒ **Build `pure/RcsBalance.cs` + `pure/DiffThrottle.cs`** on the pseudo-inverse for the symmetric case, with the
TCA iterative descent as the robust fallback and the FDIR "could-not-meet-torque" failure flag.

### 1d. Other TCA capabilities worth banking
- **T-SAS (`Modules/AttitudeControl.cs`):** controls the attitude of the **total thrust vector**, not the nose
  — i.e. point where the engines actually push (= MechJeb's INERTIAL_COT). Use for gimbal/differential burns so
  the THRUST axis, not the reference transform, tracks the guidance aim.
- **Vertical-Speed Control + Altitude + Radar (`Modules/Planet/VerticalSpeedControl.cs`):** VTOL soft-touchdown
  law + terrain-relative altitude via a built-in radar (= true ground clearance / `AltitudeBottom`). Directly
  useful for the booster hoverslam final descent + touchdown-height.
- **Horizontal-Speed Control:** null horizontal speed by tilting the thrust vector — booster lateral control.
- **Smart Engines (orbital):** group engines by thrust direction; choose the active group by **Rotation / Time /
  Efficiency** (minimise rotation, then fuel, then time). The Efficiency rule is a clean burn-orientation policy.

## 2. KerbalEngineer (KER) — the ΔV/TWR/burn-time reference (P1 StageStats + burn executor)
`KerbalEngineer/VesselSimulator/*` (SimManager/Simulation/PartSim/EngineSim) is the classic per-stage **ΔV /
TWR / burn-time** simulator (Padishar's improved algorithm) — a simpler, well-trusted alternative to MechJeb's
`FuelFlowSimulation` for our **StageStats (P1)**. Its maneuver readouts confirm our burn-executor design:
`NodeBurnTime.cs` + **`NodeHalfBurnTime.cs`** (center-of-burn timing) + `NodeTotalDeltaV.cs`. KER also computes
suicide-burn altitude, impact time/position, phase angle to target, and closest approach — the readout set our
recorder/screens should expose. ⇒ Cross-check our StageStats ΔV against KER's numbers (a second reference =
"verify out"); reuse the half-burn-time confirmation for the finite-burn executor.

## 3. ModularFlightIntegrator (MFI) — infrastructure, not a port, but important context
`ModularFlightIntegrator.cs` is the hook that lets FAR/RealHeat **override the stock aero + thermodynamic
integration** per vessel (drag, lift, convective/radiative heating). It is WHY our `Trajectory.MeasureAero`
reads real FAR forces, and it is the force model the **reentry-sim (Tier-4)** must integrate against — not a
stock drag-cube model. No code to port; the takeaway: our aero/thermal numbers already come from MFI's
integrated forces, so measure, don't model.

## 4. FerramAerospaceResearch (FAR) — computed aero we can USE, not just measure
FAR computes per-vessel **Cl / Cd / Cm and stability derivatives** (its static-analysis + the runtime aero
module). Two uses: (a) the **AoA/q·α moderation (P0)** needs the aero-moment slope `M_α` — FAR can give it
directly rather than only estimating it from response; (b) the **reentry-sim (Tier-4)** should pull FAR's
lift/drag for the capsule. We already tap the integrated forces via `MeasureAero`; FAR's coefficient interface
is the upgrade if the measured slope is too noisy. (Grid fins are `FARControllableSurface`, authority ∝ q.)

## 5. Full install scan — every other mod, harvest verdict
| Mod(s) | What it is | Harvest |
|---|---|---|
| **HotStaging** | RO hot-stage ring hardware | ⇒ the safe-sep guard MUST handle hot-stage lead (light upper before dropping lower) — §F3 |
| **CanaveralPads · TundraSpaceCenter · ModularLaunchPads · KerbalKonstructs · KSCSwitcher** | launch sites + **RTLS landing pads** | ⇒ launch-site lat/lon + the RTLS pad static positions for BoosterTargeting (droneship OR pad) |
| **TestFlight** | engine reliability / failures | ⇒ the FDIR reliability expectation + per-ignition failure probability (already used) |
| **SolverEngines** (+ RealFuels/RealHeat) | RF engine thrust vs pressure/throttle; ullage; shock heat | ⇒ engine numbers read live (ConfigCache); ullage 0.996; already core |
| **KSPCommunityFixes** | fixes stock `GetPotentialTorque` | ⇒ makes our gimbal-torque authority trustworthy (already relied on) |
| **KerbalJointReinforcement** | stiffer joints | ⇒ validates our rigid-body control assumption (less flex/pogo) |
| **RealAntennas** | comms range/pointing | ⇒ P3 comms/thermal management (antenna-point + link budget during coast) |
| **RCSBuildAid** | editor CoM/RCS-torque balance tool | ⇒ confirms the RCS-imbalance problem is real; not a runtime source |
| **EngineGroupController** | manual engine grouping | ⇒ N/A (we group by capability in Actuator) |
| **AJE** | jet engines | N/A (Falcon/Dragon) |
| **Kopernicus · Sol-Configs/Textures/Visuals · RSS-CanaveralHD** | the RSS-like "Sol" system bodies | ⇒ Earth R/μ/rotation + KSC latitude ground-truth (env research) |
| **ROLib/ROTanks/ROEngines/ROCapsules/ROHeatshields/ROSolar · ProceduralParts/Fairings · B9PartSwitch** | procedural RO parts | ⇒ parts only; actuate by capability |
| **AtmosphereAutopilot** | FAR-aware FBW autopilot | ⇒ already harvested (AoA moderation, §B of AUTOPILOT_HARVEST) — keep OFF |
| **KerbalReusabilityExpansion · IQStarshipLegs · StarshipGroundExtensions** | legs / grid-fins / landing hardware | ⇒ parts; capability-detected |
| **Waterfall/RealPlume/SmokeScreen · Scatterer/EVE/Parallax/TUFX/Deferred · ReStock · TextureReplacer** | visuals | N/A |
| **ContractConfigurator/RP-1/RONoCareer · KerbalChangelog/PatchManager/ClickThroughBlocker/ToolbarControl/Harmony** | career/UI/infra | N/A |

## 6. What this adds to the plan (folded in)
- **P0 RCS balancer + differential throttle:** now have a concrete, RSS/RO-proven solver (TCA iterative descent)
  + the MechJeb QP alternative + a pseudo-inverse recommendation for our symmetric layout, and the FDIR
  "insufficient authority" failure flag. Add **T-SAS thrust-vector aim** for gimbal/differential burns.
- **P0 AoA moderation:** get `M_α` from FAR's coefficients (cleaner than response-estimation).
- **P1 StageStats:** KER's VesselSimulator is the second ΔV/TWR reference to verify against; half-burn-time
  confirmed for the finite-burn executor.
- **Booster recovery:** TCA VSC/altitude/radar landing law + true ground clearance for the hoverslam touchdown;
  RTLS pad positions from the launch-site mods.
- **Safe-sep guards:** handle HotStaging hot-stage lead.
- **Tier-4 reentry-sim:** integrate against MFI/FAR forces (measure, don't model).

Standing method recorded: DLL-only mod → read its GitHub source. Cross-refs: `docs/AUTOPILOT_HARVEST.md`
(MechJeb) · `docs/MECHJEB_CAPABILITY_INTEGRATION.md` (P0/P1) · `docs/VALIDATION_AND_ROBUSTNESS.md` (Tier-4).
