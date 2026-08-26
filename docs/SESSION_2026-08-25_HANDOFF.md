# Session handoff — 2026-08-25 (booster landing + Dragon rendezvous execution)

Read this first after compaction. It is the complete current state and the exact next steps. Everything
below is VERIFIED from the flight data / configs, not the `.md` docs.

## The mission

Build the full SpaceX-equivalent guidance for the Tundra (real Falcon 9 + Crew Dragon) in RSS/RO,
mission-agnostic, validated by the real timeline. Order: booster → upper stage → rest. **Master plan:
`docs/SPACEX_GUIDANCE_MASTER_PLAN.md`.**

## ⛔ DISCIPLINE (the user was furious about these — do not repeat)

- **Read the flight the USER ACTUALLY SAW**, by mtime, not an old one. I diagnosed flight_0825_163535 when
  the user had flown flight_0825_184857 — different failure. Cost trust.
- **INSTRUMENT EVERYTHING**, same pass you build. The user has said this many times.
- **USE THE CORPUS** (124 recorded flights in `DragonScreen_capture/`) to build EMPIRICAL models, not
  physics guessing. Mining CSVs with Python is data analysis, NOT a banned sim.
- **Confirm against ground truth** — the live `ModuleManager.ConfigCache`, the flight CSV, `KSP.log` — never
  the `.md`. Read configs/blocks IN FULL (I wrongly called the booster engine "stock" from a partial read;
  it IS a real Merlin — ConfigCache 1,134,891+).
- **Find ALL issues, no tunnel vision.** One glance-and-fix is how flights get wasted.
- Between flights ALWAYS suggest the proper next step. Never fly blind.

## BOOSTER — the landing is SOLVED; the AIM fix is installed but UNFLOWN

**Landing (SOLVED, flight_0825_184857):** the engine was starving-then-catching ~6 s after ignition (cold
gas empty → unsettled propellant at terminal velocity), but the ignition budgeted only 3.7 s → it caught
too low and crashed. FIX: **`Landing.LandingDeadTimeS 2.5 → 6.0`** (measured command→full-thrust). Result:
caught at ~1122 m, braked −246→−13 m/s, landed SOFT. Proof the catch tracks ignition altitude (105 m → 280 m
→ soft). Done.

**Aim (INSTALLED, UNFLOWN):** last flight landed 16 km short because the impact prediction was garbage. Root:
the ballistic coefficient is **Mach-dependent, not a scalar**, and `ImpactPredictor` fed the integrator one
live-measured scalar bc that is HELD at a near-vacuum ~37000 under thrust — exactly when the entry burn must
aim. Our `pure/Trajectory.cs` integrator ALREADY had a Mach-dependent drag path (`DragFactorAt`, ported from
the Trajectories add-on) that was never wired up ("not set up correctly", user). FIXES:
- **`pure/BoosterDrag.cs` (NEW)** — empirical `bc(Mach)` MINED from the corpus (18,080 clean descent samples:
  Mach 0.5→2582, 1→1485, 2→1075, 3→1321, 4-5→~1500 kg/m²).
- **`ImpactPredictor.PredictBooster`** — integrates with that curve (DragFactor + sqrt(1.4·P/ρ) sound speed).
  `BoosterRecovery.ImpactPointHoriz` now calls it.
- **The entry burn TARGETS THE BARGE** — `Landing.EntryAimReached` cuts when the drag-modelled predicted
  impact has bled to within `EntryAimBufferM=2000 m` of the barge (floor `EntryBurnMinSpeedMps=700`),
  replacing the blind velocity cut. `PredictedMissValid` (threaded through `PredictedMiss`/`ImpactPointHoriz`)
  gates it to drag-modelled predictions.

**NEXT booster flight:** does it land NEAR the barge now? Read `b_predMissKm` through the entry burn — should
be STABLE (was garbage/sign-flipping) and shrink toward 0. If still off: tune `EntryAimBufferM`, or the
corpus curve above Mach 5, or (deeper) the ascent MECO energy is too high and the barge at ~599 km is
over-flown — softening MECO is the real lever for a light entry burn (couples booster↔ascent). Watch
`b_maxSkinK` (heating; last flight hit 674 K, under the 773 K skin limit).

## DRAGON RENDEZVOUS — root FOUND + FIXED, UNFLOWN

**Symptom (flight_0825_184857, the one the user watched):** capsule floated, no thrust, orbit frozen
216/200 through PHASE/BOOST/CLOSE. **PROOF:** of **1414 "Burning" rows only 1 had `nd_pointErrDeg` < 3°.**
`NodeExecutor` only fired the Draco fore-translation (`UllageFore`) when `onAxis` = pErr < **3°** — a
main-engine gate the Crew Dragon (NO reaction wheels; holds attitude on the SAME Dracos it translates with)
can never hold, it oscillates 3-40°. So it translated 1 tick of 1414 → delivered nothing → drifted.

**Second failure (flight_0825_163535):** when it DID briefly reach pErr 2°, the translation was INVERTED vs
the booster (x_ctlZ −1.00, nose prograde, yet accelerated retrograde → peri driven to −18 km, nd_deliveredDv
−1941). The Dragon's forward translation sign is opposite the booster's.

**FIXES (installed, UNFLOWN):**
- `NodeExecutor.RcsTranslateGateDeg = 25°` — RCS burns translate at a LOOSE gate the Dragon can hold
  (cos25=91%, cross-component averages out); tight 3° stays for main-engine burns. The backstop uses the
  loose gate for RCS.
- `NodeExecutor.rcsTransSign` — SELF-CORRECTING: after `RcsSignCheckS=0.6 s` of translation, if the Δv
  delivered ALONG the intended direction is negative (`RcsSignFlipDvMps=-0.10`), flip the sign once, discard
  the small wrong-way perturbation, reseed the accounting. Vehicle-agnostic (no hard-coded Dragon sign).
- `pure/BurnExec.cs` runaway abort — `Runaway()` (residual > `InitialDvMps*1.5 + 3`) aborts a wrong-way burn
  before it wrecks the orbit; the overshoot test provably cannot catch wrong-way. Has a regression test.

**NEXT rendezvous flight:** does the orbit RAISE on BOOST (`nd_deliveredDv` > 0, `a_apoKm` climbs from 216
toward the co-elliptic ~409, not frozen)? Watch the log for **"RCS translation was INVERTED - flipped"**. If
the burns deliver, the sequence (Phase→Boost→Close→Drift→Transfer→Co-elliptic→AI→Midcourse→L-approach) can
finally progress. The rendezvous LOGIC (pure/NamedRendezvous.cs + NamedRendezvousOps.cs, CW terminal) is the
2026-08-25 rebuild and is sound; the blocker was pure burn EXECUTION.

## Instrumentation added this session (in FlightRecorder, appended at END)

- `rv_*` — rendezvous internals (leg, gap-to-lead, alongKm, radialKm, lastBurn, deliveredDv via nd_, warp).
- `bl_*` — booster landing (`bl_liveThrustKn` = PRODUCING thrust not available, `bl_coldGasFrac`,
  `bl_ullageOn`, `bl_igniteAttempts`).
- `nd_*` — every NodeExecutor burn (planned vs delivered Δv, pointErr, rcs, tIgn) — added earlier this day.
- `x_ctlZ`/`x_fore` (the translation reaching the vessel) already existed — that's what proved the inversion.

## State of the tree

ALL of this is **UNCOMMITTED on master** in `Desktop\DragonScreen`. Build green, INSTALLED (needs a full KSP
restart). Do NOT commit or push unless the user asks. Files changed: `pure/Landing.cs`, `pure/BoosterDrag.cs`
(new), `ImpactPredictor.cs`, `BoosterRecovery.cs`, `pure/BurnExec.cs`, `NodeExecutor.cs`, `FlightRecorder.cs`,
`test/BurnExecTest.cs`, plus the earlier `pure/NamedRendezvous.cs` + `NamedRendezvousOps.cs` rebuild and
`docs/SPACEX_GUIDANCE_MASTER_PLAN.md`.

## Commands

```
cd C:\Users\User\Desktop\DragonScreen\plugin
python build.py test            # build + headless tests
python build.py install         # build+test then copy to GameData (KSP + CKAN CLOSED; full restart)
python build/assess_flight.py   # newest capture, or pass a path
```
Captures: `C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\DragonScreen_capture\` (124 flights).
Corpus mining example (bc-by-Mach) is how BoosterDrag was built — re-run over `b_mach`/`r_bcBooster` filtered
to `b_phase==DESCENT`, unpowered, q>0.2.

Memory: [[dragonscreen-spacex-guidance]], [[dragonscreen-rendezvous-rebuild]], [[falcon-real-hoverslam-technique]],
[[falcon-booster-landing-twr]], [[instrument-everything]], [[no-python-simulations]], [[dragon-nose-cone-rcs]].
```
