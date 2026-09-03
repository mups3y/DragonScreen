> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-28; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# Autopilot mining, round 3 — PEGAS, Trajectories, GravityTurn (+ scan of the rest)

> **Why (2026-08-28, user: "mine the OTHER autopilot systems for every nugget of gold").** Beyond MechJeb / TCA
> / AtmosphereAutopilot / KerbalEngineer (already harvested), the highest-value external autopilot systems for
> our build. Read from source (standing method — [[mechjeb-source-reference]]). Each nugget is tagged with the
> MASTER BUILD SEQUENCE stage (`AUTOPILOT_REBUILD_PLAN.md §0.2`) it feeds.

Repos: [Noiredd/PEGAS](https://github.com/Noiredd/PEGAS) (kOS UPFG ascent) ·
[neuoy/KSPTrajectories](https://github.com/neuoy/KSPTrajectories) (entry prediction) ·
[linuxgurugamer/GravityTurn](https://github.com/linuxgurugamer/GravityTurn) (ascent optimizer).

---

## 1. PEGAS — the UPFG reference (Shuttle powered-explicit guidance in kOS) → Stage 0c + Stage 7
PEGAS is the faithful UPFG implementation our `pure/Upfg.cs` was ported against. The deep read gave the exact
thing that fixes our **inclination undershoot**, plus the multi-stage handling for the PVG port.

### 1a. ⭐⭐ THE PLANE-TARGETING FIX (Stage 0c — our inc undershoot, 46.5° vs 51.6°)
In PEGAS, UPFG does NOT lock the achieved plane — the **target plane normal `iy` is computed EXTERNALLY from
the target inclination + LAN and passed INTO UPFG as a cutoff target** (`LOCAL iy IS target["normal"]`), and the
guidance steers to HOLD that normal. The cutoff targets are **radius (rdval), velocity (vdval), flight-path
angle (gamma), AND the plane normal (iy)** — four conditions, the plane being one of them.
⟹ **Our bug (46.5° not 51.6°): we effectively targeted the ACHIEVED plane, not the target normal.** The fix,
straight from PEGAS: compute the target normal from (launch site, target inc, LAN) — the launch-window/azimuth
math we already have — and feed it into the UPFG cutoff so the guidance STEERS the plane to inc/LAN instead of
holding whatever it drifted into. This is the Stage-0c "UPFG inc/LAN cutoff" item, now with the exact method.

### 1b. Linear-tangent steering (confirms our `Upfg.cs`)
`iF = (lambda − lambdadot·J/L)` normalized, where `lambda = unit(vgo)`, `lambdadot = (rgo − S·lambda)/lambdade`.
The thrust direction leads the velocity-to-go by a rate set by the position error `rgo` — classic linear-tangent.

### 1c. ⭐ Virtual stages + thrust integrals L/J/S/Q (Stage 7 — the multi-stage PVG port)
UPFG reduces the whole multi-stage vehicle to **6 numbers**: split the vehicle into "virtual stages" at each
jettison/shutdown EVENT, pre-compute each, and accumulate the thrust integrals `L=∫a·dt`, `J=∫L·dt` (≈`0.5·L·tb`),
`S=∫J`, `Q=∫S·(tb/3+tgo)`; constant-acceleration vs exponential-throttle (log/exhaust-velocity) phases handled
separately. **No explicit iteration loop** — it converges by being CALLED each guidance cycle (predictor-
corrector), exactly as our per-cycle `Upfg.Step`. This is the multi-stage skeleton for the PVG upgrade.

## 2. KSPTrajectories — the reference ENTRY PREDICTOR → Stage 6 (reentry-sim) + Stage 2 (booster)
This IS the proven atmospheric-entry predictor (our `pure/Trajectory.cs` was cross-checked against it). The
deep read gives the method to copy for the Stage-6 reentry-sim + the Tier-4 forward-model.

### 2a. ⭐ The integrator (copy this for the reentry-sim)
**RK4 with a CONFIGURABLE step, PLUS a correction term that approximates KSP's own Euler-integration error** —
so the prediction matches what KSP physics actually does, not an idealised RK4. This "match the game's
integrator" trick is why Trajectories is accurate; our C# reentry-sim (Tier-4) must do the same to reproduce a
recorded flight (the corpus-calibration gate).

### 2b. Forces + rotation + impact
- Aero force from **AoA + air-relative velocity** via the aero model; gravity + aero combined each step.
- **Planet rotation:** `CalculateRotatedPosition` rotates inertial→body-fixed at each point (`angle = −(t−now)·|ω|`)
  — the rotation-correction we already do in BoosterTargeting/entry, confirmed correct.
- **Impact:** ray-cast between consecutive points, interpolate the terrain crossing.
- **Frame-chunked:** the integration `yield`s across frames with a back-buffer swap — how to run a LIVE
  predictor without stalling the game (for an on-screen entry footprint).

### 2c. ⭐ Scheduled entry AoA profile (Stage 6 — the capsule lifting entry)
The descent is a **4-band AoA schedule** vs atmosphere-depth ratio: AtmosEntry (50–100%), HighAltitude
(25–50%), LowAltitude (5–25%), FinalApproach (<5%); each band an AoA (relative to horizon OR velocity), **Lerp**
between bands. It represents **retrograde blunt-body (|AoA|>π/2) OR lifting capsule (|AoA|≤π/2, nonzero AoA vs
velocity)**. ⟹ our bank-angle entry / CoM-shifter trim should SCHEDULE the trim AoA by altitude band (Lerp),
not hold one fixed value — and the lifting-vs-blunt representation confirms the CoM-shifter L/D approach.

## 3. GravityTurn — the ascent AUTO-TUNER → Stage 1 (ascent tuning) + L6 self-cal
GravityTurn's whole value is a **persistent optimizer that improves the launch across flights** — directly the
"improve over flights, zero human tuning" goal for our ascent shape.

### 3a. ⭐ The LaunchDB auto-tuner (adopt for ascent-shape self-cal)
It stores each launch's `(turn params → resulting losses)` in a **LaunchDB**, retrieves the BEST prior settings,
and refines sequentially (`BestSettings` / `GuessSettings` → `RecordLaunch` → `Save`). It tunes **StartSpeed
(turn-start speed), TurnAngle (initial pitch), DestinationHeight**, minimizing:
```
TotalLoss = DragLoss + GravityDragLossAtAp + VectorLoss(steering)
```
⟹ this is the concrete self-improve loop for OUR gravity-turn shape (`Ascent.TurnStartVMps/TurnEndVMps/
FinalPitchDeg/TurnShape`): record each ascent's (shape → total loss) to a `PluginData/learned.cfg` (our L6
`LearnedParams`), pick the best, refine — the ascent tunes itself over flights. The **loss decomposition
(drag+gravity+steering)** is the SAME as MechJeb §L1 and our planned Δv-loss recorder columns — three sources
now agree on the metric.

### 3b. Throttle-to-timeToAp + prograde-hold (ascent law confirmation)
Throttle is modulated to hold `timeToAp` roughly constant (`APThrottle(timeToAp)`) → keeps the orbit curving up
smoothly; attitude holds **prograde-relative pitch** (vertical → pitch transition → prograde hold). Confirms our
zero-AoA gravity turn; the timeToAp-throttle is a clean complement to our max-Q bucket + g-limit for the coast-
to-orbit shaping.

## 4. Scanned, LOW marginal value (recorded so it's not re-mined)
- **kRPC.MechJeb / kRPC** — a binding layer to MechJeb's ascent autopilot; no new algorithm (external control
  pattern only). We're in-process.
- **Astrogator / Flight Plan / Maneuver Node Evolved** — maneuver-node planners; covered by our MechJeb maneuver
  library + Lambert (Stage 4).
- **kOS KSLib** — community kOS libs (Lambert, navball math); covered by MechJeb ports.
- **Principia** — n-body; not installed, overkill for 2-body LEO patched-conics.

## 5. Where each nugget lands (folded into the plan)
| Nugget | Stage | Action |
|---|---|---|
| ⭐ PEGAS plane normal → UPFG cutoff (inc/LAN) | 0c / 1 | pass the target normal into `Upfg` cutoff — FIX the inc undershoot |
| PEGAS virtual stages + L/J/S/Q | 7 | the multi-stage skeleton for the PVG port |
| ⭐ Trajectories RK4 + KSP-Euler correction | 6 / Tier-4 | the reentry-sim integrator (corpus-calibrated) |
| Trajectories 4-band AoA Lerp schedule | 6 | schedule the capsule entry trim-AoA by altitude band |
| ⭐ GravityTurn LaunchDB loss-minimizing auto-tuner | 1 / L6 | self-tune the gravity-turn shape over flights via `LearnedParams` |
| GravityTurn throttle-to-timeToAp | 1 | ascent throttle-shaping complement |
| loss = drag+gravity+steering (3 sources agree) | 1 / L7 | the Δv-loss recorder columns metric, confirmed |

Cross-refs: `docs/AUTOPILOT_HARVEST.md` (MechJeb) · `docs/MODS_HARVEST_2.md` (TCA/KER/MFI/FAR) ·
`docs/ASCENT_GUIDANCE_UPFG.md` · `docs/AUTOPILOT_REBUILD_PLAN.md §0.2`.
