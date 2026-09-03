> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGHEST for §B12.1**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-21; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.
> ⚠ It records the **GEN-1 vendored `pure/mechjeblib/` tree** (R1 §6.2) — a PRIOR attempt at what §B12.1 plans, not the current one.

# Porting MechJebLib's FuelFlowSimulation — the pure sim is DONE and PROVEN

**Status 2026-08-21: steps 1-3 COMPLETE and headless-validated. Steps 4-6 (the KSP builder, wiring
ReturnBudget, surfacing the budget) remain — they are KSP-coupled and can only be validated in-game,
so they wait for the RSS/RO install gate.** This work was started as step 2 of the approved RSS/RO
Crew-1 plan (`.claude/plans/snoopy-orbiting-hennessy.md`): a real staged Δv budget is the prerequisite
for tuning any RSS phase honestly.

## ✅ DONE 2026-08-21 — the pure simulation, ported verbatim into `src/pure/mechjeblib/`

- **Foundation** (`Utils/ObjectPool`, `Utils/Statics` [subset], `Utils/DictOfLists`, `Utils/AsyncJob`,
  `Functions/Interpolants` [double only], `Primitives/HBase`, `Primitives/H1`) and the **15 sim files**
  (`FuelFlowSimulation/*` + `PartModules/*`), all copied verbatim with SPDX + provenance headers.
- **`test/MechJebLibTest.cs` (16 checks)** guards H1/Statics; **`test/FuelFlowTest.cs` (8 checks)**
  hand-builds a SimVessel and proves the sim reproduces **dv = Isp·g0·ln(m0/m1)** at both vacuum and
  sea level (so the H1 AtmosphereCurve is being evaluated by pressure), plus start/end mass, thrust,
  Isp. Build green, all suites pass.

### ⚠ ONE DELIBERATE DEVIATION from the plan below: a **minimal V3**, not "skip V3"

The "V3 — SKIP ... only V3.zero x6" line was an under-count. `SimModuleEngines` carries
`List<V3> ThrustDirectionVectors` + `V3 ThrustCurrent/Max/Min` and does real vector arithmetic to sum
canted thrust before `.magnitude` reduces it to a scalar; `SimVessel` has `V3 R,V,U`. The full V3.cs
(481 lines) drags in M3 (Outer) + Q3 (Slerp) + more Statics — the cascade this plan rightly deferred.
Resolution: keep all 15 sim files **verbatim** and provide a **minimal V3** (`src/pure/mechjeblib/
Primitives/V3.cs`) with exactly the members the sim uses. It is a strict subset of MechJebLib's V3, so
the PSG port (which needs the full V3 + M3 + Q3) **replaces that file additively** — nothing here is
undone. The approved plan's PSG step makes a real vector type in `src/pure` correct, superseding the
old "pure has no vector type" note.

### What remains (steps 4-6, KSP-coupled — need the game to validate)

4. **Our own SimVessel builder** in `src/` (not `src/pure`) against `VehicleParts` — reads the KSP
   Vessel's parts/engines/resources/FloatCurves into a SimVessel. Only exercisable against a real
   craft, so it waits for the RSS/RO install + Tundra-RO Falcon 9.
5. **Wire a staged Δv budget** (ReturnBudget, or a new consumer) to ask the sim.
6. **Surface staged Δv** on the VEHICLE page + recorder.

---

## (original scope, kept for reference)

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
