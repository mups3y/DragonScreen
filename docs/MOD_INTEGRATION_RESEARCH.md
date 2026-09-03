> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGH (§B12.1 pins)**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-28; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# Mod-Integration Research — the installed mods as DATA SOURCES for the autopilot (2026-08-28)

> ⭐⭐ **MOD-DEPENDENCY POLICY (user 2026-08-28) — the deciding question is: is the mod ALREADY a hard dependency of
> the target RO/RSS install?**
> - **OPTIONAL mod** (may be absent — KER, BetterTimeWarp, PhysicsRangeExtender): NEVER add a hard dependency on
>   it. Either **(a) SOFT-integrate** — read it by reflection IF loaded, fall back to our own pure code if not
>   (e.g. `KerBridge`) — or **(b) BUILD the useful behaviour into DragonScreen** (e.g. `WarpPlan`/`MissionConductor`).
> - **HARD-DEPENDENCY-of-RO/RSS mod** (ALWAYS present in the target install — the KSP-RO ecosystem: ModuleManager,
>   `CustomPreLaunchChecks`, RealFuels/RO, FAR, KSPCommunityFixes, …): ⛔ **do NOT reinvent / port / absorb it —
>   that is "a hat on a hat".** It is guaranteed present, so our mod takes **FULL DIRECT ADVANTAGE / CONTROL** of
>   it — reference/read/drive it directly (a hard reference is fine; it adds no NEW dependency). 
> - ⭐ **AUDIT RULE:** if we have already ABSORBED (reimplemented) something a guaranteed-present hard-dep mod
>   provides, FIX it — use the mod directly. (Audit + findings in §6 below.)
> This doc is the reference for both paths: what each mod exposes, what the autopilot should consume, and how.

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

## 4b. ⭐ CustomPreLaunchChecks (CPLC) — a HARD-dep (KSP-RO ecosystem) → take direct control
`github.com/KSP-RO/CustomPreLaunchChecks` (v1.8.1.1, DLL). It is a KSP-RO project bundled with RP-1/RO, so in the
target install it is **effectively always present** → per policy we USE it directly, never reimplement it.
- **How it works:** at startup it detours (Harmony/`AsmUtils.Detour`) `EditorLogic.GetStockPreFlightCheck` →
  `CPLCFunctions.NewPreflightCheck`, injecting extra `PreFlightTests.IPreFlightTest` checks into KSP's STOCK
  pre-flight-check system. RP-1/RO populate the check list (`internal static List<…> allChecks`): launch-readiness
  like controllable avionics, crew, connection, etc. `CustomPreLaunchChecks.instance` is public; `allChecks` is
  internal.
- ⚠ **CORRECTION to `MOD_INVENTORY_RESEARCH §A4`:** CPLC gates the **VAB→pad LAUNCH** (the editor pre-flight
  check), NOT on-pad ignition. So it does **NOT** block our on-pad AUTO SEQUENCE — confirmed by the 2026-08-28
  flights (the vehicle was on the pad and DID ignite). My earlier "it may block our auto-launch" flag was wrong.
- ⭐ **How WE take direct advantage (the useful integration the user flagged, for the gated crew-screen GO/NO-GO):**
  the crew-screen countdown poll should SHOW real launch-readiness, and CPLC/stock already computes it. Two clean
  paths (both use what's guaranteed present, no reinvention): **(i)** run KSP's stock `PreFlightCheck` /
  `PreFlightTests.IPreFlightTest` set ourselves to read pass/fail for the display, or **(ii)** reflect
  `CustomPreLaunchChecks.instance` + `allChecks` to read the exact RO checks. Feed those into a crew-gate item
  (e.g. an autofilled "vehicle GO" line) so a NO-GO check shows RED on the glass instead of us re-deriving avionics/
  control readiness. ⛔ Do NOT build our own avionics/control/crew launch-readiness logic — that would be the
  hat-on-a-hat. (Integration task in §5.)

## 6. ⭐ AUDIT — have we "absorbed" (reimplemented) any hard-dep functionality? (hat-on-a-hat check)
Ran the audit per the policy. Findings:
- **Pre-launch readiness (CPLC / stock PreFlightCheck) vs our crew gates:** our `CrewProcedureOps`/`CrewGate` model
  the ASTRONAUT PROCEDURE (ingress, suit-leak, hatch, prop-load GO, ARM LES, crew GO poll) — a DIFFERENT thing from
  CPLC's technical launch-readiness (avionics/control). So it is **not a direct duplicate**, BUT the crew screen
  should DISPLAY CPLC/stock readiness rather than us adding our own technical checks → §4b integration. ⛒ minor.
- **StageStats (B1) vs KER:** KER is OPTIONAL (not an RO hard-dep), so our own `StageStats` is a JUSTIFIED fallback
  for when KER is absent, and KER is the soft cross-check when present (`KerBridge`). **Not** hat-on-a-hat. ✅ keep.
- **Aero (FAR):** we MEASURE the live aero force (`Trajectory.MeasureAero`) rather than reimplement FAR — correct
  (FAR is a hard-dep we USE via its effect, not a reimplementation). ✅
- **RealFuels ullage / engine state:** used directly (reflection in `Ullage.cs`), not reimplemented. ✅
- **GetPotentialTorque (KSPCommunityFixes):** we USE its fixed value, do not reimplement torque reporting. ✅
- **Conic/orbital math:** guidance-internal; no hard-dep mod provides the closed-loop UPFG/CW/Lambert we need. ✅
⇒ **No egregious hat-on-a-hat found.** The one action is CPLC display integration (§4b/§5), not a removal.

## 5. Plan implications
- ✅ **`src/KerBridge.cs`** — the soft KER reader (§1c) is BUILT (per-stage Δv/TWR/burn-time mirrored + the recorder
  KER-vs-ours cross-check). Remaining: have consumers TRUST KER over our fallback once the corpus proves agreement.
- ⭐ **NEW integration task (Part II screens / I-B): CPLC/stock launch-readiness → the crew-screen GO/NO-GO.** Read
  KSP's stock `PreFlightCheck` set (or reflect `CustomPreLaunchChecks.instance.allChecks`) and surface it as a
  crew-gate readiness line, so a NO-GO shows on the glass. ⛔ Do NOT reimplement avionics/control/crew checks — CPLC
  is a hard-dep, use it directly (§4b). This is the "take direct control of the hard-dep" pattern, not absorption.
- **Warp + focus orchestration:** already built (`WarpPlan` + `MissionConductor`), stock APIs, no deps.
- **PRE:** a *precondition to verify* for booster auto-recovery, not something we ship.
- **KER/KRE/PRE are environment facts** now recorded here + in `INSTALLED_MODS_RESEARCH.md §6a`; BTW is reference.
