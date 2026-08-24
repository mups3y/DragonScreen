# Plan — a Crew-2-grade guidance / autopilot system

Goal (user, 2026-08-24): a guidance/autopilot **as good as the real Crew-2 mission**. Every phase flies the
REAL technique ([[../CREW2_REAL_MISSION_TECHNIQUES]]) to the real numbers, closed-loop, on measured (not
guessed) vehicle data, verified flight-by-flight. This plan is a **fidelity-gap roadmap** — the architecture
already exists (conductor + per-phase modules); the work is bringing each phase to real-Crew-2 fidelity and
closing the specific gaps the flight data exposes.

## Design principles (the "sophistication" layer — apply to EVERY phase)

1. **Closed-loop to a real target, never open-loop or a fixed schedule.** Each phase measures its own error
   (predicted vs desired state) every tick and nulls it. Open-loop schedules are what fail when the estimate
   is wrong ([[falcon-flight-data-first]]).
2. **Measure the vehicle, don't model it** ([[falcon-detect-by-capability]]). Drag, thrust, TWR, ISP, spool,
   dead-time — all read off the live vehicle (ImpactPredictor already does this for drag). A hard-coded
   coefficient is wrong the first time the config changes.
3. **Account for the plumbing the real vehicle doesn't have to:** RealFuels ignition dead-time + Merlin
   spool ([[falcon-real-hoverslam-technique]]), the ~300 km physics-range unload ([[falcon-physics-range-clamp]]),
   no reaction wheels in RO (RCS is the only attitude authority).
4. **One change per test flight, root-caused from data first** ([[work-efficiency-no-second-guessing]]).
5. **Headless-testable pure core + thin glue** — the math lives in `pure/`, validated by the test suite; the
   KSP glue stays minimal. This is the split that has kept the maths honest.

## Current architecture (already built)

- **Conductor:** `AutoSequence` / `AutoSequenceCore` chains the whole mission from one button; `FlightDriver`
  ticks every controller each frame; `MissionPhase` names the phases.
- **Per-phase modules exist for every phase** (see the table). The gaps are fidelity/tuning + a few missing
  techniques, NOT missing scaffolding.

## Phase-by-phase gap analysis (real target → our module → gap → build)

| # | Phase | Real Crew-2 target | Our module(s) | Fidelity status & GAP |
|---|-------|--------------------|---------------|------------------------|
| 1 | **Ascent** | grav-turn ~0 AoA, max-Q + g-limit throttle, MECO ~T+2:36 / ~2.3 km/s | `pure/Ascent.cs`, `pure/Upfg.cs`, `AutoPilot` | **Good.** Loft tuned (staged 68.5 km ≈ real 67). GAP: confirm MECO velocity/FPA matches the flight-plan insertion target; verify UPFG hands S2 a clean state. |
| 2 | **Staging + S2 to orbit** | sep T+2:39, SES-1 T+2:47 (MVac), SECO-1 T+8:47, insert ~190–210 km, Dragon sep T+11:58 | `AutoPilot` (UPFG), sep/solar logic | **Good.** GAP: target the real LOW parking orbit (not a high circular), so the phasing-up is real. Verify SECO insertion state. |
| 3 | **Booster coast** | flip engines-first, cold-gas N2 in vacuum, coast to apogee, controlled + deliberate | `BoosterRecovery` (StepCoastAim) | **Fixed** (walked aim, roll 509→21). GAP: none major; keep it deliberate. |
| 4 | **Booster entry burn** | relight **3 engines** to shed re-entry speed/heat | `BoosterRecovery` + `pure/Landing.cs` EntryBurn | **Good** (3-engine, ullage settle, soft start). GAP: trim the over-sized entry burn so it leaves the right energy for the fins. |
| 5 | **Booster grid-fin guidance** ⭐ | 4 titanium fins steer the **hypersonic aero descent** to null downrange+cross-range BEFORE the landing burn — this is where the ~10 m precision comes from | `BoosterRecovery` GuidedLean + grid-fin deploy | **PARTIAL — the biggest precision lever.** We lean the body toward the pad; real precision is fin-driven *aerodynamic translation*. GAP: a proper fin/aero guidance law that drives the **predicted impact** (ImpactPredictor) onto the barge during DESCENT, so the landing burn is purely vertical. |
| 6 | **Booster landing burn** | hoverslam, 3→1, v=0 AT the deck, ~10 m | `pure/Landing.cs`, `pure/Hoverslam.cs` | **In test** (envelope handover + dead-time fix just applied). GAP: confirm from the next flight; resolve the over-powered-engine question (1-eng min TWR ~2.0 vs real ~1.2 — [[falcon-real-hoverslam-technique]]). |
| 7 | **Rendezvous** ⭐ | named co-elliptic sequence: Phase→Boost→Close→Transfer→Coelliptic→AI (AI at 7.5 km behind/below, ~96 min out) | `NamedRendezvous(Ops)`, `Rendezvous`, `StationApproach` | **PARTIAL.** Named-burn scaffolding exists but the approach has been an ad-hoc ladder ([[falcon-rendezvous-design]]). GAP: make it the real co-elliptic targeting (CW/Lambert), hand to prox-ops at the AI point 7.5 km out. |
| 8 | **Prox ops + docking** | approach ellipsoid 4×2 km → keep-out sphere 200 m → WP0 (400 m nadir) → 220 m on axis → WP2 (20 m) → soft then hard capture (12 hooks) | `WaypointApproach(Ops)`, `StationApproach`, `DockApproach`, `DockControl`, `DockingOps` | **PARTIAL.** Waypoint + docking control exist. GAP: encode the REAL corridor (ellipsoid → KOS → the exact waypoints) with go/no-go holds and cm/s axial closing; verify soft→hard capture. |
| 9 | **Undock + departure** | hooks retract, 2 sep burns, then 4 departure burns (0/1/2/3), then 1 phasing burn → ~10 km below ISS | `UndockOps`, `UndockPush`, `PhaseDownOps` | **PARTIAL.** Undock + push exist. GAP: the real **departure-burn sequence** (2 sep + 4 departure + phasing) rather than a single back-away; matches the approach in reverse. |
| 10 | **Deorbit** | trunk sep BEFORE deorbit, ~15 min Draco deorbit burn to the entry corridor | `DeorbitOps`, `pure/Deorbit*.cs`, `pure/DeorbitOrbit.cs` | **Good.** Draco deorbit + trunk logic exist. GAP: verify the long low-thrust burn targets the real entry corridor; confirm trunk-before-deorbit ordering. |
| 11 | **Re-entry** ⭐ | lifting entry, **12° trim AoA, L/D 0.18**, offset CoM, **bank-angle** range+cross-range control | `pure/EntryGuidance.cs`, `EntryOps`, `EntryMargin` | **Good technique, wrong constants.** We already bank the lift vector on downrange error. GAP: set TrimAoA / L/D to the REAL 12° / 0.18 (currently ~0.27), and add **cross-range** (bank-sign) control, not just downrange. |
| 12 | **Parachutes** | drogues ×2 @ ~18,000 ft / ~350 mph; mains ×4 @ ~6,000 ft / ~119 mph | `ChuteGuard` | **Good** (unconditional deploy guard). GAP: gate the deploys on the REAL altitude+speed triggers, not just altitude. |
| 13 | **Touchdown** | ~15 mph under mains, ocean splashdown at the recovery area | `ChuteGuard`, `EntryOps` | GAP: validate the splashdown lands in the targeted recovery zone (the entry guidance's job). |

⭐ = the three phases furthest from real and worth the most fidelity investment: **grid-fin descent guidance
(booster precision), the co-elliptic rendezvous, and the bank-angle capsule entry constants + cross-range.**

## Cross-cutting upgrades (shared machinery)

- **Unified closed-loop burn executor** (`NodeExecutor` + `pure/BurnExec.cs`) already leads for spool and
  RCS. Extend: every named burn (rendezvous, departure) runs through it with the real duration/Δv targets.
- **The measured-drag impact predictor** (`ImpactPredictor`) is the shared truth for BOTH the booster and the
  capsule (two profiles, now wired to the map+flight overlays). Grid-fin guidance (5) and entry guidance (11)
  both steer *their predicted impact onto the target* — same law, two vehicles.
- **Telemetry/verification:** `FlightRecorder` + `assess_flight.py` already diagnose a flight in one command.
  Add per-phase target-vs-actual columns so each phase is graded against its real number.

## Proposed build order (highest fidelity-return first)

1. **Finish the booster landing** (in test now) — confirm the envelope-handover + dead-time fix lands;
   settle the over-powered-engine question. *Closes phase 6.*
2. **Booster grid-fin descent guidance** (phase 5) — drive the predicted impact onto the barge during
   descent so the landing burn is vertical. This is where real precision lives; biggest single win.
3. **Capsule entry constants + cross-range** (phase 11) — set 12°/0.18, add bank-sign cross-range control;
   validate splashdown in the recovery zone. *Closes phases 11–13.*
4. **Co-elliptic rendezvous** (phase 7) — real Phase→…→AI sequence handing to prox-ops at 7.5 km.
5. **Docking corridor** (phase 8) — the real ellipsoid→KOS→waypoint corridor with holds + cm/s closing.
6. **Departure sequence** (phase 9) — 2 sep + 4 departure + phasing burns.
7. **Ascent/insertion + deorbit target verification** (phases 1–2, 10) — confirm the real target states.
8. **End-to-end**: one Crew-2 flight, launch→splashdown, driven only by AUTO SEQUENCE (×2) + UNDOCK, every
   phase hitting its real number.

## Verification

- **Headless** (`build.py test`): every new law gets pure-core tests (the `pure/` split). All suites stay green.
- **In-game, flight-data-first**: one change per flight; `assess_flight.py` grades each phase against its real
  target; root-cause from `FlightData` + black box before the next change.
- **The proving flight**: a full Crew-2, hands-off, every phase within its real tolerance (booster ~10 m,
  splashdown in the recovery zone, docking through the real corridor).

## Decisions (user, 2026-08-24)
- **Booster engine sizing** → investigated per the user's "check/fix the VAB" call. **RESULT: the vehicle is
  a faithful real F9 - do NOT change it.** flight_0824_210106: mass 25.6-28.4 t (real dry ~25.6 ✓), 3 engines
  2556 kN → 852 kN/engine SL (real Merlin 1D SL ~845 ✓). ConfigCache: every Merlin mode has
  **minThrust = 0.40·maxThrust** (real 40% floor). KSP maps command t to actual = min + t·(max-min), so
  command **0.4 → 64% thrust (TWR ~2.0)** but command **0 → 40% (TWR ~1.35 = real gentle flare)**. So
  `Landing.LandingMinThrottle = 0.40` is a DOUBLE floor - the guidance bug that stops the flare. **FIX (a
  one-line guidance change, next flight): lower LandingMinThrottle toward 0** - the engine self-limits to 40%
  and stays lit; the touchdown cut is via Shutdown, not throttle. See [[falcon-real-hoverslam-technique]].
- **Rendezvous cadence** → **REAL SLOW PHASING.** Fly the true ~23 h multi-orbit co-elliptic phasing
  (Phase→Boost→Close→Transfer→Coelliptic→AI), full fidelity, however long it takes / warps.
