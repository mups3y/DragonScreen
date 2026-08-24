# MechJeb phase-by-phase audit — what to take, tuned to Crew Dragon / RO

Goal (user, 2026-08-23): a *true thinking, perfectly-tuned autopilot* — correct decisions at every
phase, able to adjust when something goes wrong. Method: go through MechJeb module by module against
what we do now, verify the math, take what genuinely does better, tune it to Falcon 9 / Crew Dragon and
to RO's rules (no reaction wheels, RealFuels ullage, TestFlight ignition, FAR aero).

Source read: `Desktop/mechjeb_src` (MechJeb2 + MechJebLib). Ours: `DragonScreen/plugin/src`.

**Discipline reminder:** one change per test flight; validate headless before flying. The
launch-into-plane fix from earlier today (`68cafd46`) is still UNFLOWN — confirm it coplanar before
stacking the bigger ports below.

---

## Scorecard by phase

| Phase | Ours now | MechJeb has | Verdict |
|---|---|---|---|
| Launch window | ✅ ported MechJeb `Astro.TimeToPlane` today | same | **DONE** (confirm in flight) |
| Attitude control | bang-bang `sqrt(2αθ)` → time-const P; no integral | **BetterController**: cascade PID, integral near target, `Soften`, torque LPF | **PORT — biggest win** |
| Ascent guidance | UPFG (works) | PSG (RSS/RO) + UPFG | keep ours; steal RO knobs |
| Staging/MECO/SECO | capability-based, works | autostage | keep ours |
| Booster landing | hoverslam, ignition struggles | **HoverslamSimulation** with RealFuels **RCS-ullage burn** | **PORT ullage handling** |
| Rendezvous | hand-rolled co-elliptic | state machine over **OrbitalManeuverCalculator** | **PORT calc + SM** |
| Docking | ours works-ish | DockingAutopilot (RCS translation PID) | review after rendezvous |
| Deorbit/entry | ours (F9I heritage) | landing predictions | keep ours; compare later |
| RO settings | mostly matched independently | a few extras | minor top-ups |

---

## 1. Attitude control — the headline win (`BetterController`)

**Ours** (`AttitudeController.DriveInner`, `pure/Attitude.cs`): per-axis error → target rate via
`RateCommand` = `sqrt(2·α·θ)` bounded by `MaxRateDps` → torque via `TorqueCommand` =
`(ωtarget−ω)·MOI / TimeConstant` → actuate. A pure bang-bang-to-P law. **No integral term.**

**MechJeb `BetterController`** (verified correct — it's a textbook cascade):
- **Position loop**: far from target (|err| > 2·effLD) uses the SAME `sqrt(2·α·θ)` bang-bang we do;
  near target it switches to a **PID** (Kp 2.03, Ti 1.97) → smooth settle with **integral action that
  kills steady-state pointing error**. `effLD = soften²·α/(2·Kp²)` is the crossover.
- **`Soften` = 0.5** scales the target rate on large slews → less overshoot (our booster-flip / 180°
  reorientation roll history is exactly this failure).
- **Velocity loop**: tuned P (Kp 7.98) clamped to ±maxAlpha → `targetTorque = MOI·targetAlpha`.
- **Torque low-pass** (`SmoothTorque` 0.10) + `warpFactor` scaling → no wiggle under phys-warp.

**Why it matters for us:** tighter pointing everywhere (gravity-turn tracking, boostback aim, deorbit
aim, docking alignment) AND controlled large slews. It directly answers "adjust if anything goes
wrong": the integral trims persistent errors (aero torque, thrust offset) our P law leaves standing.

**Tailoring:** gains 2.03/1.97/7.98 are MechJeb's universal defaults, validated against MOI/torque
which we already sum from the parts (gimbal+RCS, RO reaction wheels stripped). They should transfer, but
**tune on our vehicle**: the booster flip (low roll authority) and the capsule (RCS-only) are the two
stress cases to check. Keep our `LockRoll` and roll-authority handling on top.

**Risk:** attitude is where flights died (per memory). Port into `pure/`, headless-test the step
response (rise time, overshoot, steady-state) against the current law, then ONE flight.

---

## 2. Orbital maneuvers — `OrbitalManeuverCalculator` (rendezvous foundation)

**Ours** (`pure/Orbital.cs`, `pure/Phasing.cs`): `Hohmann`, `CircularSpeed`, `PhaseWaitSeconds`,
`CirculariseAtApoapsisDv`. Enough for a co-elliptic ladder, missing the closers.

**MechJeb** — a complete, rigorous suite (all pure-portable): `DeltaVToCircularize`,
`DeltaVToChange Apoapsis/Periapsis/Eccentricity`, `DeltaVForSemiMajorAxis`, `DeltaVToChangeInclination`,
**`DeltaVAndTimeToMatchPlanesAscending/Descending`** (residual plane trim — we have none),
**`DeltaVToInterceptAtTime`** (Lambert intercept — the real terminal-phase burn), `DeltaVToMatchVelocities`
(the terminal Δv match), `DeltaVAndTimeForHohmannTransfer`, `DeltaVToResonantOrbit` (phasing).

**Verdict:** port `OrbitalManeuverCalculator` into `pure/` (+ its Lambert solver from
`MechJebLib/Lambert`). It's the math foundation for a correct rendezvous and gives us a proper plane
trim for any residual the launch window leaves.

---

## 3. Rendezvous — state machine over the calculator

**Ours:** `NamedRendezvousOps` hand-rolled co-elliptic ladder.
**MechJeb `MechJebModuleRendezvousAutopilot`:** clean state machine — match planes if off →
Hohmann/coelliptic to a phasing orbit → intercept-at-time (Lambert) → match velocities → hand to
close-approach. Uses the calculator above for every burn.

**Tailoring:** keep our hand-off to the R-bar/V-bar L-approach at close range; drive attitude/RCS the
RO way (no reaction wheels). Port the state machine after the calculator lands.

---

## 4. Booster landing — steal the RealFuels ullage handling

**Ours:** `pure/Landing.cs` hoverslam + the spool/ignition fixes fought all session.
**MechJeb `MechJebModuleHoverslamSimulation`:** simulates the landing burn AND, under RealFuels, plans
an **RCS ullage burn** before ignition (`AutoRCSUllaging`, `needsRCSUllage = IsLoadedRealFuels &&
LowestUllage < 1.0`, `RcsUllageTime`). This is precisely the ignition problem we kept hitting — MechJeb
schedules the settle as part of the plan instead of reacting to a failed light.

**Note:** its sim has `TODO: add aero model for landing on Earth` — so its predictor is *worse* than our
FAR-measured drag for the descent. **Take the ullage-burn scheduling, keep our aero.**

---

## 5. RO/RSS settings — the gaps are small (we got most independently)

MechJeb auto-detects RO (`IsLoadedRealismOverhaul`) and applies (`ApplyRODefaults`):

| Setting | MechJeb RO | Ours | Action |
|---|---|---|---|
| Ullage lead | fixed 20 s | 2 s settle **+ UllageProbe stability gate** | ours is smarter — keep |
| Min throttle | 0.05 | 0.05 | ✅ match |
| Auto RCS ullaging | on | manual per-phase | fold into hoverslam port (§4) |
| Parking altitude | 145 km (min stable) | 200 km (realistic, below ISS) | ✅ ours better |
| MaxQ | limit OFF (guidance handles) | no explicit throttle-down | consider a Falcon maxQ throttle |
| Unstable-ignition limit | off | n/a | n/a |

Only genuine gap: a real **maxQ throttle-down** (Falcon throttles the center through ~max-Q). Minor for
reaching orbit; nice for fidelity.

---

## Recommended sequence (one change per flight)

0. **Confirm** the launch-into-plane fix flies coplanar (`INSERTION PLANE rel-inc ~0`). *(staged)*
1. **Attitude: port `BetterController`** cascade PID into `pure/`, headless step-response test vs
   current law, tune on booster-flip + capsule, one flight. *(biggest control win)*
2. **`OrbitalManeuverCalculator` + Lambert** into `pure/`, headless-tested against known transfers.
3. **Rendezvous state machine** over it; keep L-approach hand-off. Fly it.
4. **Hoverslam RCS-ullage** scheduling into our landing (keep our aero).
5. Docking PID + maxQ throttle as polish.
