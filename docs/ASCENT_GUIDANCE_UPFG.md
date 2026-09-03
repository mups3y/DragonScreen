> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGH**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-23; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.
> ⚠ **Named contradiction:** it designs the **hand-written** control loop that was deleted 2026-09-01. Part B builds a **pinned, privately-namespaced MechJeb embed + a pure conductor** (§B1–B16 / T15–T22) instead.

# Ascent guidance for RSS — the real fix (UPFG/PEG)

## ⛔ STATUS: UPFG CORE BUILT + HEADLESS-VALIDATED 2026-08-22. Glue (KSP wiring) is the remaining step.
- **`pure/Kepler.cs`** (16 tests) — universal-variable conic propagator (Stumpff), fresh C#. Verified vs
  circular quarter-orbit, full-period identity, energy/momentum conservation, reversibility.
- **`pure/Upfg.cs`** (10 tests) — the full Brand-Brown-Higgins UPFG, all 9 blocks, fresh C# (no kOS;
  equations cross-checked against the PEGAS-MATLAB reference). `Init` + `Step(r,v,mu,target,vehicle,ref state)`
  → thrust unit `iF` + `Tgo`. VALIDATED: from the measured MECO state (65 km, 2860 m/s inertial, ~18° FPA,
  M-Vac TWR 0.82) the predictor-corrector **converges** (<1% Tgo drift), its desired cutoff matches the
  200 km target radius, and `iF` comes out prograde WITH an upward loft component - the behaviour no fixed
  pitch produced. `V3` was extended with Cross/Dot/normalized (copied from MechJebLib, still a subset).
- **GLUE WIRED 2026-08-22 (installed, UNFLOWN)**: `AutoPilot.UpfgFlyS2` - once the M-Vac is lit on an RSS
  ascent (`RssBody` + thrust>1 kN + phase BurnToApoapsis/Coast/Circularise), UPFG OWNS the second stage:
  builds the target/vehicle from live state, `Upfg.Step`, `SteerTo(iF)`, full throttle, and cuts to SECO
  (SeparateSecondStage + disengage) at `Tgo<=0.2 s`. Frame = instantaneous world (R = CoM-body.pos,
  V = obt_velocity), consistent for R/V/iF within a tick. Logs `UPFG tgo Ns ap.. pe.. orb V/target`.
  Falls back to the loft if the solve returns invalid. Stock untouched. TEST IT (see below).
- **PRIOR (still true) = the thin glue** each RSS S2 tick build
  `UpfgTarget{ Iy=-unit(r×v), RadiusM=body.Radius+parkingAlt, SpeedMps=sqrt(mu/RadiusM), GammaRad=0 }` and
  `UpfgVehicle{ ExhaustVel=Isp·g0, ThrustN=Σ active thrust, MassKg=vessel mass }`; persist `UpfgState`; call
  `Upfg.Step`; `AttitudeController.SteerTo(iF)`; full throttle; cut to Done (SECO) when `Tgo<=~0.2 s`. This
  REPLACES the S2 loft + the apoapsis/coast/circularise phases for RSS (UPFG flies the whole insertion in
  one closed loop). RSS-gated on RssBody; stock keeps the gravity turn. Wire + test together next.

## Ascent guidance for RSS — the real fix (UPFG/PEG), researched 2026-08-22

The RSS ascent is the mission gate. This is the design for the guidance that actually closes it, grounded
in the real math Falcon 9 uses, not another hand-tuned heuristic. Read before touching `pure/Ascent.cs`
`Guide()` or the AutoPilot S2 loft again.

## 1. Why the interim gravity turn / loft cannot reliably close RSS orbit
MEASURED (flight_0822_045358): the RO M-Vac has **TWR ~0.82 at ignition** from a **65 km MECO**. A low-TWR
upper stage cannot hold altitude by pointing up (thrust < gravity), so a fixed-pitch or apoapsis-deficit
loft either lofts and bleeds (25° pitch → apoapsis 123 km then re-entry) or apexes inside the 140 km
atmosphere. The optimal is a **shallow, continuously-turning burn** that trades the small vertical velocity
and the rising centrifugal relief against gravity - a trajectory that only an optimal-control law flies
well. No single pitch heuristic reproduces it across the whole burn; that is exactly why real vehicles use
closed-loop optimal guidance.

## 2. The real math: linear tangent steering + predictor-corrector (PEG/UPFG)
Optimal ascent under flat-Earth/uniform-gravity/no-atmosphere has an ANALYTIC solution - the **linear
tangent law**: the thrust pitch obeys `tan(β) = A·t + B`, A and B constants solved from the terminal
constraints. **UPFG** (Unified Powered Flight Guidance - the Shuttle/PEG algorithm, and what RO's kOS
**PEGAS** mod flies) wraps this in a predictor-corrector so it works on a round, rotating Earth. Sources:
[NASA PEG (Mahajan 2025)](https://ntrs.nasa.gov/api/citations/20250011251/downloads/PEG_ASC25_Mahajan.pdf),
[PEGAS UPFG docs](https://github.com/Noiredd/PEGAS-MATLAB/blob/master/docs/upfg.md),
[Derivation of linear-tangent steering laws (Perkins)](https://www.semanticscholar.org/paper/DERIVATION-OF-LINEAR-TANGENT-STEERING-LAWS-Perkins/763a971b31073aa6338ff8ee1211af220d27b6eb).

### Inputs (target as CONSTRAINTS, not a point)
Earth-centered inertial `R`, `V`; target **plane normal `iY`** (from inc + LAN; ⚠ oriented OPPOSITE the
orbital angular-velocity vector - sign trap), target **radius**, **speed**, **flight-path angle**; per-stage
thrust, Isp, remaining propellant; time.

### The nine blocks (one call = one iteration; converges in 2-3, criterion ΔTgo < 1%)
1. **Init** - first `Rd`,`Vd` guess: rotate `R` forward in-plane, constrain to target plane + altitude.
2. **State update** - read `R`,`V`; reduce `Vgo` by the velocity gained since last call.
3. **Tgo** - time-to-cutoff from Tsiolkovsky over the remaining stage(s).
4. **Thrust integrals** (scalars over Tgo): `L = Σ Isp·g·ln(m0/mf)` (avail Δv); `S` (displacement from
   thrust); `J = ∫ t·(thrust accel) dt` (first time-moment); `Q` (second moment). Standard closed forms:
   `L = ve·ln(1/(1-Tgo/τ))`, `J = L·τ − ve·Tgo`, `S = L·Tgo − J`, `Q = S·τ − ve·Tgo²/2`, where
   `τ = ve/a0` (a0 = current thrust accel, ve = Isp·g).
5. **Turning rate + thrust direction** - `Rgo = Rd − R − Rgrav + Rbias`; solve steering unit vectors
   `λ` (from `Vgo`: `λ = Vgo/|Vgo|`) and `λ̇` (from `Rgo` and the moments), giving
   `iF(t) = unit(λ·cos + λ̇·sin)` — the linear-tangent direction. `Rbias`,`Vbias` carry prediction error.
6. (folded into 5 in most implementations.)
7. **Gravity** by **Conic State Extrapolation** (Kepler propagate `Rc1,Vc1` over Tgo → `Rc2,Vc2`;
   `Rgrav = Rc2−Rc1−Vc1·Tgo`, `Vgrav = Vc2−Vc1`) - no constant-g assumption. Needs a 2-body propagator.
8. **Vgo update** - predict cutoff `Rp = R + V·Tgo + Rgrav + Rthrust`, scale to target radius, build `Vd`
   satisfying (plane ⟂ iY, speed, FPA), set `Vgo = Vd − V − Vgrav + Vbias`.
9. Output **`iF`** (thrust unit vector to steer) and **`Tgo`**; on `Tgo → 0` command MECO/SECO.

## 3. ⛔ RECOMMENDATION: port UPFG, NOT PSG — a scope finding
The plan's step B is MechJebLib **PSG** (optimal-control collocation). MEASURED on disk 2026-08-22:
- **PSG = ~3,400 lines** (`Ascent/AscentProblem/Optimizer/Solution/Terminal…`) **+ ~9,800 lines of deps**
  (ODE 1585, TwoBody 646, Primitives 3496, Functions 1395, Utils 2150, Rootfinding 417, Minimization 166)
  ≈ **13,000 lines** of a nonlinear-programming solver with autodiff. The plan itself flags it
  "largest, highest-risk… fails to converge with no obvious reason why."
- **UPFG ≈ a few hundred lines**: the 9 blocks above + `V3` (ALREADY ported, `pure/mechjeblib/Primitives/V3.cs`)
  + a **conic (2-body) propagator** for block 7 (port MechJebLib `TwoBody/Shepperd`, ~200 lines, or write a
  universal-variable Kepler step). Stage Δv/Tgo reuse the **FuelFlowSimulation already ported**. It is the
  algorithm RO users actually fly (PEGAS), proven, and converges in 2-3 iterations with no optimizer.

⇒ **UPFG is the pragmatic path to closing the orbit**: ~20× smaller, proven, testable headless (does the
predictor-corrector converge; does `iF`/`Tgo` drive a point-mass to the target orbit — validate the MATH
without a KSP flight). PSG stays the eventual "optimal" upgrade if ever wanted, but it is overkill here.

## 4. Implementation plan (when we build it)
1. Port `TwoBody/Shepperd` (conic propagator) into `pure/mechjeblib/` (block 7 needs it). Headless-test
   against known conic cases.
2. New `pure/Upfg.cs`: the 9 blocks, taking (R, V, target constraints, stage list) → (iF, Tgo, converged).
   Uses V3 + Shepperd + the ported FuelFlowSim for stage Δv. Headless test: convergence + a point-mass
   integration reaching a 200 km / 51.6° orbit from the measured MECO state (65 km, 2589 m/s).
3. Glue: in the RSS BurnToApoapsis/Circularise branch, call Upfg each tick, steer `iF`, cut on `Tgo≤0`.
   Replaces the S2 loft heuristic (`S2LoftGainDeg`/`S2MaxLoftDeg`) and the two-phase apoapsis/circularise
   for RSS. Stock keeps the gravity turn. RSS-gated on RssBody as everything else.
4. First stage can STILL be the interim gravity turn (it works to ~MECO); UPFG owns the S2 to orbit - which
   is exactly where the interim fails.

## 5. Until then
The S2 loft ([Tunable] gains) is the stop-gap; if a couple of gain tunes from a flight don't close orbit,
build UPFG per §4 rather than tuning the heuristic further. This doc + the loft data is enough to start.
