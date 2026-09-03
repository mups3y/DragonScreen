> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGHEST for §B16**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-25; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.
> ⚠ **Named contradiction:** §B16 (owner, 2026-09-03) is the current form of booster recovery — a SEPARATE-VESSEL autopilot distinct from the conductor; the deleted `BoosterControl` implementation stays deleted. Cross-reference today's `docs/BOOSTER_GUIDANCE_METHOD.md`.

# Falcon 9 booster guidance — real Crew-2 profile + MechJeb-based design

Answering the mission question (user 2026-08-24): how did the real Crew-2 booster fly its atmosphere
(entry) burn and its landing burn — when, how many engines, why, on what trajectory relative to the
droneship, at what height, and with what math — and how to build our guidance on MechJeb as a base.
Sources: NASA Crew-2 timeline; [Spaceflight Now — "Up and down with a Falcon 9 booster"](https://spaceflightnow.com/2020/09/16/up-and-down-with-a-falcon-9-booster/);
[Everyday Astronaut](https://everydayastronaut.com/). Primary numbers, not Wikipedia.

## The real Crew-2 booster (B1061-2, droneship OCISLY, Atlantic)

| Event | MET | Engines | What / why |
|---|---|---|---|
| MECO | T+2:36 | 9→0 | ~2.3 km/s, ~67 km |
| Stage sep | T+2:39 | — | booster flips retrograde, grid fins active |
| *(no boostback)* | — | 0 | **droneship = high energy: NO boostback**, the booster keeps flying downrange to the barge (~560 km) |
| **Entry (atmosphere) burn** | **T+7:27** | **3** (center + 2 opposed) | lit high (~55–70 km) through the **supersonic** upper air to bleed speed so reentry heating/max-Q is survivable AND to aim the ballistic arc at the barge; ~20 s |
| **Landing burn** | **T+9:03** | **3 → 1** | starts on 3 to arrest terminal velocity, **shuts down 2, finishes on the single center engine** |
| Landing | T+9:30 | 1 | on the droneship |

### Why those engine counts (the crux)
- **Entry burn = 3 engines** because one can't bleed enough speed through the thickest, fastest part of
  reentry in the short window; three cut the peak heating/dynamic pressure the airframe sees.
- **Landing burn = 3 → 1** because **even ONE Merlin at its ~40 % minimum throttle has TWR > 1 on the
  near-empty stage — it cannot hover.** So landing is a **hoverslam** (a.k.a. suicide burn): you can't
  hang in the air and settle, you must arrive at **zero velocity exactly at zero altitude**. Three
  engines are needed to arrest the high terminal speed, but three at the deck is far too much thrust to
  null the last few m/s precisely, so two shut down and the center engine flies the touchdown, starting
  ~70 % throttle and modulating for "coming in too fast or too slow."

### The trajectory relative to the droneship
The booster is on a **ballistic reentry arc** whose un-powered impact point is placed near the barge by
the MECO velocity (that sets downrange) and the entry burn (fine bleed + aim). **Grid fins** then fly
the descent aerodynamically to null the remaining cross-track/downrange error onto the deck. The barge
sits **on the booster's ground track** at the downrange the staging velocity produces (~560 km for
Crew-2). It is a **hit-what-you-aim** trajectory, not a hover-and-search.

### The math
1. **Entry burn sizing** — a Δv chosen so the *predicted* peak reentry heating / dynamic pressure stays
   under the airframe limit. Set by entry velocity, altitude, flight-path angle.
2. **Landing burn = the suicide-burn / hoverslam** — ignite when `altitude ≈ StopDist + margin`, with
   the closed form `StopDist = v² / (2·(a − g))` (a = thrust acceleration on the chosen engines). Ignite
   **as late as possible** so aerodynamic drag does maximum free braking and minimum propellant is
   spent. Target: `v = 0` at `h = 0`.
3. **Steering** — grid fins + gimbal null the impact-point error to the barge during descent.

## What we do now vs. the target

**Now (`pure/Landing.cs`):** the closed-form hoverslam is right in spirit — `BurnThrottle = StopDist/h`,
a 3→1 handover (`HandoverReady`), grid-fin/gimbal aim. This session's fixes got the engine to **light
and brake 246→~45 m/s** (the TestFlight q-ignition penalty was the blocker), and just fixed the
**flameout** (it commanded 0.22 on 3 engines, below the Merlin's ~0.4 floor; now clamped + handover
relaxed so it drops to one engine before starving — `07dd8848`).

**Gaps to the real profile / MechJeb quality:**
- ~~**Ignition point is closed-form, drag-blind.**~~ **DONE 2026-08-24** — `pure/Hoverslam.cs`
  (`HoverslamSolver`) replaces the closed form: MechJeb's numerical descent-integration + root-solve
  *method*, plus the aero drag MechJeb's own version omits (a TODO in its source) and the ~3.5 s Merlin
  spool, both of which the closed form ignored. Wired into `Landing.Guide`'s ignition-altitude with the
  3-engine thrust recovered from `Mass × AccelThreeEngine`; the drag reference is the stage's own
  terminal fall (drag == gravity there). The `LandingIgnitionLeadS` lead shed the 3.5 s spool it used to
  carry (6.5→3.0 s) and now covers only the ullage-settle dead-fall + retry room, on top of the solver.
  7 property tests pass; validated against the 0824 landing (ignition ~925 m base, arrests at the deck).
- **Accuracy** — 71 km barge miss this session, because the trajectory shifted with the new
  throttle/plane and the aim/barge is off the current ground track.
- **Entry burn over-sized** — ours kills 1459 m/s (2280→821); real is a lighter bleed, leaving more
  landing margin.

## The MechJeb base (what to port)
MechJeb lands with a proper state machine — `CoastToDeceleration → DecelerationBurn → FinalDescent` —
driven by **`MechJebLib/HoverslamSimulation`**: a **numerical descent integration** (ODE with thrust,
gravity AND aero drag) that solves for the exact `IgnitionUT`, `IgnitionAttitude`, `FinalThrustAccel`
and `Dv` — instead of our drag-blind closed form. This is the "perfect" ignition-point solver.

### Plan — build the guidance on MechJeb, tuned to Falcon 9 / RO
1. ~~**Port `HoverslamSimulation`**~~ **DONE 2026-08-24 (`pure/Hoverslam.cs`, wired into `Landing.Guide`).**
   Built as MechJeb's *method* — numerical descent-integration + bisection root-solve — tailored to the
   1D vertical Falcon landing: a lightweight integrator (dt 0.05, no ODE/Tsit5/Astro stack) over
   altitude/speed/mass under gravity + full-throttle thrust with the ~3.5 s spool ramp + v² drag. Drag
   reference is the MEASURED terminal fall (drag == gravity), no atmosphere model. The `Mass` field added
   to `LandingInputs` recovers thrust from the accels. Next: feed it the sampled FAR ballistic coefficient
   directly instead of the terminal-fall proxy if a flight shows the proxy drifting.
2. **Keep the Falcon engine schedule from the real profile:** 3-engine entry burn sized to a reentry-q
   limit (trim the over-burn), 3→1 landing handover (the fix from today, now driven by the sim's
   FinalThrustAccel so it hands to one engine at the right moment).
3. **Steering to the barge** — port MechJeb's `CourseCorrection` idea: null the *predicted* impact point
   to the droneship with grid fins (aero) then gimbal, instead of a fixed aim. This fixes the 71 km miss
   and lets the barge sit exactly on the achieved ground track.
4. **Validate the sim headless** (like the launch-window and PSG ports) before flying: the predicted
   ignition altitude and touchdown velocity must match a recorded descent, then one flight.

Same discipline as the rest of the mission: real Crew-2 method + MechJeb's proven math + tuned to our
measured vehicle. This doc is the target; each step is one tested change.
