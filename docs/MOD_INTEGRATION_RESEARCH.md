# Mod-Integration Research — the installed mods as DATA SOURCES for the autopilot (2026-08-28)

> ⭐ **POLICY (user 2026-08-28): NO external-mod DEPENDENCIES.** We build the useful behaviour into DragonScreen.
> Where an installed mod already computes something hard and RealFuels/RO-accurate, we **SOFT-integrate** it (read
> it by reflection IF the assembly is loaded, fall back to our own pure code if not) — a *soft* dependency, never
> a hard one. This doc is the reference for that: what each mod exposes, what the autopilot should consume, and how.

Installed + version-checked this pass (KSP 1.12.5, RO/RSS): **Kerbal Engineer Redux 1.1.9.5** (jrbudda's RO fork),
**KerbalReusabilityExpansion**, **PhysicsRangeExtender** (partial — user), **KerbalJointReinforcement**,
**KerbalKonstructs**. **BetterTimeWarpContinued is NOT installed** (reference only).

---

## 1. ⭐⭐ Kerbal Engineer Redux (KER) — the live-data TREASURE CHEST
KER runs a full **RealFuels/RO-accurate fuel-flow simulation** and a live orbital/surface/rendezvous readout
engine. It computes, correctly and every ~150 ms, most of the hard numbers our guidance needs — battle-tested
against the *real* engine models. We should **read KER when present** (a soft bridge) and keep our own pure code
(`StageStats`, `Hoverslam`, `Trajectory`, `Conic`, `Lvlh`) as the FALLBACK + cross-check. If KER and we disagree,
that disagreement is itself a bug signal.

### 1a. The programmatic API — `KerbalEngineer.VesselSimulator.SimManager` (static)
Access by reflection into the loaded `KerbalEngineer.dll` (no compile-time reference → no hard dependency):
- `static Stage[] Stages` — per-stage results (index 0 = last/final stage burned … highest = current stage).
- `static Stage LastStage` — the final stage's cumulative result.
- `static bool ResultsReady()` — true when a run finished (`!bRunning`).
- `static void RequestSimulation()` — queue a run (KER coalesces; `minSimTime` = 150 ms floor).
- `static event ReadyEvent OnReady` — fires when a run completes.
- `static string failMessage` — populated on failure.

**`Stage` fields (all `double` unless noted)** — the vehicle model, for free:
- Δv: `deltaV` (vacuum, this stage), `totalDeltaV` (cumulative), `RCSdeltaVStart/End`.
- Thrust/TWR: `thrust` (max, vac), `actualThrust` (current), `thrustToWeight`, `actualThrustToWeight`,
  `maxThrustToWeight`, `RCSThrust`, `RCSTWRStart/End`, `maxThrustTorque`, `thrustOffsetAngle`, `float maxMach`.
- Isp: `isp` (vac), `RCSIsp`.
- Mass: `mass`, `totalMass`, `resourceMass` (fuel), `rcsMass`.
- Time: `time` (this stage's burn duration), `totalTime`, `RCSBurnTime`.
- Bookkeeping: `int number`, `partCount`, `cost`.

### 1b. The readout catalog (computed live; we mostly derive these ourselves, but KER is the cross-check)
- **Vessel (`Flight/Readouts/Vessel/`):** DeltaVCurrent, DeltaVCurrentTotal, DeltaVStaged, DeltaVTotal, RCSDeltaV,
  SpecificImpulse, Thrust, ThrustToWeight, **SurfaceThrustToWeight**, Acceleration, Mass, ThrustTorque,
  ThrustOffsetAngle, AngleOfAttack, AngleOfSideslip, Glideslope, Pitch/Roll/Heading (+rates), Throttle, LfOxRatio.
  ⭐ **SuicideBurn: SuicideBurnAltitude, SuicideBurnCountdown, SuicideBurnDeltaV, SuicideBurnDistance,
  SuicideBurnLength** — KER solves the hoverslam ignition point directly (a live cross-check for `pure/Hoverslam`).
- **Surface (`Flight/Readouts/Surface/`):** **ImpactTime, ImpactLatitude, ImpactLongitude, ImpactAltitude,
  ImpactBiome** (booster/entry targeting), **TerminalVelocity, MachNumber, DynamicPressure** (q — the max-Q
  bucket), **GeeForce, VerticalAcceleration, HorizontalAcceleration**, Slope, Situation, Altitude(SL/Terrain).
- **Orbital:** Ap/Pe height + speed, all elements (SMA, ecc, inc, LAN, AoP, anomalies), OrbitalPeriod,
  TimeToApoapsis/Periapsis, **TimeToAtmosphere**, AngleToPrograde/Retrograde, AngleTo/TimeTo Equatorial AN/DN.
- **Rendezvous:** Distance, **RelativeVelocity, RelativeSpeed, PhaseAngle, InterceptAngle, RelativeInclination**,
  BearingToTarget, AngleTo Relative AN/DN, target Ap/Pe/Period.
- **ManoeuvreNode:** NodeBurnTime, **NodeHalfBurnTime** (our `Maneuver.CenterOfBurnLeadS` twin), NodeTimeToManoeuvre,
  NodeTimeToHalfBurn, Node Prograde/Normal/Radial Δv, TotalΔv, PostBurn Ap/Pe/Inc/Ecc/Period.
- **Body:** Gravity, EscapeVelocity, GeostationaryHeight, atmosphere/space band heights, rotation/orbital period.

### 1c. ⭐ WHAT THE AUTOPILOT SHOULD CONSUME (the plan) — build `src/KerBridge.cs` (soft, reflection)
Highest value first (KER's number is proven, ours is the fallback + cross-check):
1. **Per-stage Δv / TWR / burn-time / Isp / mass / resourceMass** ← `SimManager.Stages/LastStage`. Feeds MECO
   recovery reserve (B1), the deorbit/abort Δv budgets, the UPFG vehicle model (`ExhaustVel`/`ThrustN`/`MassKg`
   and the multi-stage `UpfgStage[]`), and staging decisions — replacing fragile live part-scans with the sim.
2. **SuicideBurn altitude/countdown/Δv** ← Vessel readouts. Cross-check + backup for `pure/Hoverslam` ignition.
3. **Impact lat/lon/time** ← Surface readouts. Cross-check for `pure/Trajectory` booster + entry targeting.
4. **q / Mach / terminal velocity / g** ← Surface readouts. Cross-check for the max-Q bucket + g-limit.
5. **Phase angle / intercept angle / relative velocity** ← Rendezvous readouts. Cross-check for `Cw`/`Hohmann`.
⛔ **Discipline:** KER is a *soft* input — `KerBridge` returns `bool TryGet…(out …)`; every consumer keeps its pure
fallback and NEVER hard-refs `KerbalEngineer`. Call `RequestSimulation()` at our guidance rate; read on `OnReady`
or poll `ResultsReady()`. Instrument BOTH KER's value and ours in the recorder so the corpus shows any divergence.

---

## 2. PhysicsRangeExtender (PRE) — partially present (user)
Extends the vessel **load / unpack ranges** so a separated craft stays FULLY simulated (drag, thrust, control when
focused) instead of going on rails at the stock ~2.5 km unpack / ~22 km unload. Relevance: it is what makes a
**focus-managed booster recovery** possible — the separated S1 remains physically present at its landing altitude.
⛔ It does **not** remove the stock one-active-vessel limit (only the focused craft gets control input), so booster
recovery is still a SEGMENT, handled by `src/MissionConductor.cs`. Caveats from its docs: at ranges > 100 km,
expect shaking/flicker/phantom-forces and **landed vessels colliding with the ground** — so keep the extension
modest and verify the booster is still loaded at landing altitude before relying on auto-recovery. If our install's
PRE is only partial, treat "booster still loaded at touchdown" as a precondition to check, not an assumption.

---

## 3. BetterTimeWarpContinued (linuxgurugamer) — NOT installed, REFERENCE only
Adds customizable on-rails + **physics** warp rates and **lossless physics warp** (accurate physics/thrust at
accelerated time — "a 1-hour burn in 3 min at 20× physical warp"), with smoother rate transitions. Altitude limits:
0 m for 1–1000×, 100 km for 10 000×, 2 Mm for 100 000×. Known issue: physics warp below 0.1× or above 100× is
buggy. **We do not use or require it.** The USEFUL part — never overshooting a burn out of warp — is ours:
`pure/WarpPlan.cs` (safe-rate + lead drop-out, headless-proved) + `src/MissionConductor.cs` (applies it on stock
`TimeWarp.WarpTo`; forces 1× the instant thrust is live). Stock physics warp (≤ 4×) is available to us if we ever
want accelerated burns; no bundling.

---

## 4. KerbalReusabilityExpansion (KRE) — installed
The **Falcon-9 REUSE hardware** the booster flies with: grid fins (`Grid Fin M Titanium` + `NewFinsDeploy`
animation), landing legs (KSPWheel `ModuleWheelDeployment`), cold-gas RCS. Actuated directly by capability
(`Actuator.DeployGridFins`/`DeployLegs`, `GridFin` steering). It is the parts source for booster recovery, not a
control mod — no API to consume, just the modules our craft-dump map already targets.

---

## 5. Plan implications
- ⭐ **NEW integration task (I-B or a fast standalone): `src/KerBridge.cs`** — the soft KER reader (§1c). Wire the
  per-stage Δv/TWR/burn-time into the vehicle model + budgets first (highest value); suicide-burn / impact / q as
  cross-checks. Add recorder columns for KER-vs-ours so the corpus proves the agreement. NEVER a hard reference.
- **Warp + focus orchestration:** already built (`WarpPlan` + `MissionConductor`), stock APIs, no deps.
- **PRE:** a *precondition to verify* for booster auto-recovery, not something we ship.
- **KER/KRE/PRE are environment facts** now recorded here + in `INSTALLED_MODS_RESEARCH.md §6a`; BTW is reference.
