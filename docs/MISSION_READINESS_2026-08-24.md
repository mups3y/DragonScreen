# Crew-2 mission readiness — every feature, build/wire/record/test/flown

Audit done 2026-08-24 (overnight) in answer to "build the missing de-orbit and land for the Dragon
capsule; make sure every feature is built and ready for testing." **Finding: nothing is missing to
BUILD — every phase is built, wired, recorded, and headless-tested. What remains is VALIDATION (test
flights) and two known tuning residuals.** Evidence below, phase by phase.

## The de-orbit + land IS built (the specific ask)

It is NOT missing. The full return chain exists and is wired three ways in:

| Piece | File | State |
|---|---|---|
| Targeted de-orbit burn | `DeorbitOps.cs` (788 ln) + `pure/DeorbitBurn.cs`, `Deorbit.cs`, `DeorbitOrbit.cs` | phase-down → pass-find (`Overflight.GoTimeUt`) → warp → closed-loop retrograde burn to the entry periapsis, cross-track nulled, depth-floored. Defensive gates: fuel budget, S2-still-attached, sole-owner-of-vehicle, periapsis-actually-below-atmosphere before handoff. |
| Entry + landing | `EntryOps.cs` (1089 ln) + `pure/Entry.cs`, `EntryGuidance.cs`, `EntryMargin.cs`, `Terminal.cs` | trunk jettison (retrograde, one decouple, Dragon-decoupler fallback) → shield-forward coast → course-trim → lifting entry → **drogues → mains (parachute, the Crew-2 default)** OR SuperDraco propulsive with engine-failure abort back to chutes → touchdown (splash vs land, gear only on land). Clean termination (releases control). |
| Chute watchdog | `ChuteGuard.cs` | ticked every frame. |

**Wiring (all three present):**
1. AUTO SEQUENCE return leg — `AutoSequence`/`AutoSequenceCore` `SeqStep.Deorbit` engages `DeorbitOps`,
   which hands to `EntryOps`; the leg completes on `Landed`/`Splashed`. Reached when `ReturnArmed`
   (set when an outbound run finishes) and undocked in a stable orbit.
2. Manual — the **DEORBIT** string button (`FlightCommands.TouristDeorbit → DeorbitOps.Toggle`) starts
   the same targeted return from orbit **without** needing the outbound first. **This is how to
   test the return in isolation.**
3. Emergency — `PanelCommand.DeorbitNow` / `WaterDeorbit` (crude parachute-anywhere, no LZ aim).

**Recorded:** `FlightRecorder.Return()` writes `r_*` (entry stage, note, along/cross/miss, deorbit
miss/throttle, node phase). The first return flight WILL be assessable by `assess_flight.py §5`.

**Headless-tested + GREEN:** `DeorbitBurnTest`, `DeorbitPointTest`, `EntryGuidanceTest`,
`ReturnBudgetTest`, `ReturnPathTest`, plus `AutoSequenceTest` "Deorbit holds while the return
controllers run". `python build.py test` = ALL SUITES PASSED.

**Never flown** — no `r_`-stage activity in the last 40 captures. That is the whole gap: it is built and
unit-proven, not flight-proven. Historically (memory) it lands **~92 km long**; that is an accuracy
residual to diagnose from the first flight's data, not a missing feature.

## Full mission readiness matrix

| Phase | Controller(s) | Wired | Recorded | Headless test | Flown / status |
|---|---|---|---|---|---|
| Ascent → orbit | `AutoPilot` | conductor Ascent + AUTO | `a_*` | UPFG, azimuth, plane-window, Crew-2 timeline | ✅ 7/7 to orbit (UPFG off) |
| Booster recovery | `BoosterRecovery` + `pure/Landing`,`Hoverslam` | auto on booster vessel | `b_*`,`d_recov*` | octaweb, hoverslam, trajectory | ⚠ landing fix (1-engine) UNFLOWN; 19 km-long (energy); roll 1.7× (physical) |
| Rendezvous | `StationApproach`→`DirectApproachOps`→`DockingOps` | conductor Rendezvous + RENDEZVOUS/AUTO-DOCK | `x_*` | named-burn, waypoint, dock-approach/-control/-geometry, phasing, Hohmann | ⚠ not flown end-to-end |
| Dock | `DockingOps` | auto from approach gate | `x_dk*` | dock-control/-geometry | ⚠ unverified in flight |
| Refuel (berthed) | `DockedRefuel` + `Refuel.Full` (Dragon-side only) | conductor Refuel | via `x_refuelFrac` | fuel-flow | ⚠ unverified in flight |
| Undock | `UndockOps` + `UndockPush` | UNDOCK button | `x_ud*` | (geometry via dock tests) | ⚠ unverified in flight |
| **Return: de-orbit** | `DeorbitOps`→`PhaseDownOps` | conductor Deorbit + DEORBIT button | `r_*` | deorbit-burn/-point, return-budget | ⚠ never flown; ~92 km long |
| **Return: entry+land** | `EntryOps`→`ChuteGuard` | auto from deorbit handoff | `r_*` | entry-guidance, return-path | ⚠ never flown |

Every controller is ticked by `FlightDriver.Tick()` (or by its parent — `DirectApproachOps` by
`StationApproach`). No orphaned or unticked controller. No `TODO`/stub in any live path.

## What needs a TEST FLIGHT (not building)

1. **Booster 1-engine landing** — the crash fix (commit to the centre engine, no mid-air octaweb swap)
   is installed + headless-validated but unflown. Prove a soft touchdown.
2. **Rendezvous → dock → refuel → undock** — never flown as a chain; the conductor now reaches it.
3. **Return** — never flown. Use the **DEORBIT string button** from orbit to test it standalone
   (no outbound needed). Watch `assess_flight.py §5` for the ~92 km-long residual.

## Known residuals (tuning, need flight data — NOT missing features)

- **Booster 19 km-long**: vehicle energy budget — our natural impact ~561 km vs the real barge 542 km;
  the entry burn already brakes maximally to the 0.35 reserve, grid fins trim ~3 km, no fuel to null the
  rest without starving the landing. Real fix is ascent-side (stage at the velocity whose natural impact
  = 542 km); coupled to the working S2 insertion, so it must be flown. See SESSION_2026-08-23.md.
- **De-orbit ~92 km-long**: the fixed `PhaseArcFrac` lead is F9I's lighter capsule's. Diagnose from the
  first return flight's `r_*` data.
- **Booster roll 1.7×**: physical roll authority ~10× below pitch/yaw; lands on-track; settled, no
  mission impact.

## Known cleanup (deferred, low priority, documented not to lose it)

`DeorbitOps.AdaptiveIgnition` + `IgnitionMissAt`/`IgnitionFrameOk`/`Swizzle`/`ignAimRp`/
`IgnitionAimPeriapsisM`/`IgnitionWindowFrac`, and `pure/DeorbitPoint.cs` (+ its test), are a **reverted
failed experiment** — an "adaptive ignition search" that landed ~1100 km worse than the live
`Overflight.GoTimeUt` path. Fully contained (0 refs outside DeorbitOps; no recorder columns). The live
comment already flags it loudly. Left in place tonight: the pure math (`DeorbitPoint`) is correct/tested
and may inform the ~92 km diagnosis; the dead glue is harmless and clearly marked. Delete in a careful
daytime pass if desired.

## ⛔ CRITICAL / UNRESOLVED — refuel propellant: MonoPropellant vs MMH+NTO (user asked 2026-08-24)

User: "does our ISS have the fuel we need to refuel?" and "are you sure that is what RO Dragon uses?"
Verified against the game files (not assumed). There is a **contradiction** that only the user can settle:

- **Current RO config (resolved `GameData/ModuleManager.ConfigCache`, part `TE_18_DRAGONV2_POD`):** the
  Crew Dragon's tanks are **MMH 693.03 + NTO 538.15 + Helium**, and its Draco RCS `ModuleRCSFX` burn
  `PROPELLANT { MMH 0.5629 } { NTO 0.4371 } { Helium 103.35 }`. `resourceName = MonoPropellant` is only a
  vestigial legacy field — the actual propellant consumed is MMH+NTO. **There is no MonoPropellant tank.**
  So on a **freshly built** Dragon, `DockedSide.Mono("MonoPropellant")` reads ~0 and the whole refuel +
  return budget target the wrong resource.
- **But the live saves (`Space X Simulation`, `DragonScreen`) and the flight captures show only
  MonoPropellant** — the station carries ~6300 u MonoPropellant (2100×3 farms) and the flown capsule read
  "300 MonoPropellant", with **no MMH/NTO anywhere in the saves.** Those saved vessels predate the RO
  patch, so they are a self-consistent MonoPropellant world: MonoPropellant Dragon ← MonoPropellant
  station ← MonoPropellant plugin. **The refuel works today for those vehicles.**

**Answer to the ISS-fuel question:** the station has plenty (~6300 u) **of MonoPropellant**, which is
correct **only if the Dragon actually uses MonoPropellant**. If the mission Dragon is rebuilt from the
current config it uses **MMH+NTO**, and then (a) the plugin's MonoPropellant refuel/budget break, AND
(b) the station has **zero** MMH/NTO to give — the refuel cannot work at all.

**NEEDS THE USER TO CONFIRM (one check in the VAB — right-click the Dragon pod, read its tank resource):**
- If **MonoPropellant** → no change; station is well stocked; done.
- If **MMH+NTO** → a real fix is required: (1) `DockedSide`/`Refuel`/`ReturnBudget` must measure and
  transfer **MMH and NTO** (limiting one by the 0.563/0.437 ratio), not MonoPropellant; (2) the **station
  needs MMH+NTO farms added** (it currently has none). Do NOT guess this overnight — it changes the
  linchpin of the whole return.

## Persistence note (`ReturnArmed`)

`AutoSequence.ReturnArmed` is a volatile static (no `ScenarioModule`). It survives a play session — the
outbound→transfer→undock→return flow works within one sitting — but not a **save-and-reload** between
docking and undock (it would re-rendezvous instead of returning). Not a test blocker: the DEORBIT string
button returns regardless. A save-persisted latch would need a ScenarioModule + a save with the crew
mid-mission to validate; deferred (can't fly/save headless).
