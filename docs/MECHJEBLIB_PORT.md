# Porting MechJebLib's FuelFlowSimulation — scope, settled, not yet started

**Status: fully scoped, blocking question answered, zero lines written.** Deliberately not rushed at
the end of a long session; it is ready to start cold.

## Why

**There is no staged Δv budget anywhere in this project.** `ReturnBudget` uses one hard-coded
`MonoIsp = 184` and a second-order rocket-equation expansion, and knows nothing about stages, engine
modes, or which engines are lit.

That gap caused this week's largest failure: the S2 was discarded with **32% of its propellant** at a
periapsis of −126 km while the capsule spent **114 of its 195 monopropellant units** doing the second
stage's job. A staged Δv budget would have said so on the pad. It took four flights and the
infinite-fuel cheat to find.

An outside review recommended KER for this. **KER is the wrong source for us** — not installed
(so it is a port either way), GPL-3.0, and Unity-coupled. MechJebLib's equivalent is already on disk
at `Desktop/mechjeb_src/MechJebLib/FuelFlowSimulation/`, has **zero KSP or Unity references**, is
licensed `LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+` (effectively
public domain), and CLAUDE.md already settled the choice: *"port MechJebLib, do not reference
MechJeb2 — 99 files, only 7 touch KSP."*

## The blocking question, ANSWERED — do not re-open

MechJebLib's simulator has **no multi-mode engine handling**; `SimVesselBuilder:171` builds a
`SimModuleEngines` for *every* `ModuleEngines` on a part. Our Tundra first stage is **one part with
three mutually exclusive `ModuleEnginesFX`** — the exact arrangement that once produced 5030 kN
against a real 2560 and flew a booster into the pad.

**Answered from the archive, no flight needed.** `b_availThrustKn` maxes at exactly **1706.0 kN** in
octaweb mode 1 and **763.8 kN** in mode 2, across seven independent flights — precisely the
`ThreeLanding` and `CenterOnly` config ratings, never the 5030 sum. **KSP already reports only the
active module**, so the inactive ones flag themselves inactive, and `SimVesselUpdater:156-157`
captures **both** `isEnabled` and `isOperational`. Whichever flag Tundra uses, it is covered.

**The port is safe as-is. No graft, no documented divergence.**

It also corrects a standing lesson: our original bug was that `Read()` summed `maxThrust` across
modules instead of asking KSP for `availableThrust`. The vehicle was always readable — we summed the
wrong thing. "The vehicle is not what I assumed" was the wrong generalisation.

## Scope

| take | lines | where |
|---|---|---|
| core sim, 15 files | ~2 230 | `src/pure` |
| `H1` + `HBase` interpolants | 358 | `src/pure` — **essential**, eight engine curves (`AtmosphereCurve`, `VelCurve`, `ThrustCurve`, `ThrottleIspCurve`…). Without them every Isp is a constant, which is the bug we already have |
| `Statics.Clamp` ×27, `EPS` ×5 | ~10 | `src/pure` |
| `ObjectPool` | 59 | `src/pure` |
| **`V3` — SKIP** | 481 saved | the sim uses only `V3.zero` ×6; skipping keeps "pure has no vector type" intact |
| our own vessel builder | ~250 | `src/` — against existing `VehicleParts`. **Do not port MechJeb's**: 700 lines, a third RealFuels reflection we do not need |

**The C# 5 tax is gone.** It was ~95 hand edits (45 expression-bodied, 43 interpolated strings, 3
`??`, 2 `?.`, 2 `nameof`, 1 `readonly struct`). `build.py` now uses Roslyn with `-langversion:latest`,
so the files can be taken as written. **That was the single largest risk in this port** — every hand
edit to flight-proven code is a chance to introduce a bug, and hand edits are where this project's
regressions come from.

## Order

1. `H1`/`HBase`, `Statics` helpers, `ObjectPool` into `src/pure` — no dependents, testable alone.
2. The 15 sim files, verbatim, licence header cited per file as with the MAS and MechJeb ports.
3. Headless tests against a hand-built `SimVessel` before any KSP glue exists.
4. Our own builder in `src/`, reusing `VehicleParts` for part identification.
5. Wire `ReturnBudget` to ask the sim instead of its fixed-Isp expansion.
6. Surface staged Δv on the VEHICLE page and in the recorder.

Step 5 is the one that pays for the whole thing.
