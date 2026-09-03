# KER Data Research — Kerbal Engineer Redux as a live data source for the screens

**Owner-directed research task, 2026-09-03. RESEARCH + THIS DOC ONLY — no code changed, no plan edited.**
Governed by `docs/BUILD_PLAN.md` §6 (live data / parameterize), §1.4 (source-of-truth ladder) and
**§14.4(e) step (1)** — *"read it from an existing installed mod if one provides it (tier-2, MARKED)"*.
This is the mod-first step for the §6 live-data workstream. **It wires nothing.**

> ⚠ **This doc supersedes and corrects the KER section of `docs/MOD_INTEGRATION_RESEARCH.md`**, which was
> written 2026-08-28 *for the autopilot* and deleted 2026-09-01 with it (commit `8b81816`; still readable via
> `git show 8b81816^:docs/MOD_INTEGRATION_RESEARCH.md`). Two live files still cite that dead path —
> `plugin/src/KerBridge.cs:8` and `plugin/src/pure/KerData.cs:1`. Where this doc and the old §1 disagree, the
> disagreements are named explicitly in §1.5 and **this doc wins**: it was written from KER's actual source
> and the actual installed binary, neither of which the 2026-08-28 pass consulted.

---

## 0. The situation in one paragraph

**KER is already half-integrated, and the half that exists is wired to nothing.** `plugin/src/KerBridge.cs`
(reflection reader) and `plugin/src/pure/KerData.cs` (SI mirror + stage selection) exist, compile, ship in the
DLL and are headless-tested (`plugin/test/KerDataTest.cs`, 11 checks, registered at `plugin/test/TestMain.cs:31`).
But a whole-tree grep for `KerBridge` / `KerData` / `KerStage` finds **no call site** in `VesselData.cs`,
`ScreenPainter.cs`, `DragonScreenMonitor.cs`, `PageState`, or any page. The consumer that used to read it was
the guidance layer, deleted 2026-09-01. So `KerBridge.cs` is dead code in the shipped assembly — and it carries
a latent defect that would have stopped it returning data anyway (§1.4). Meanwhile the screens carry ~40
permanently-dashed readouts, and **no Δv, TWR, thrust, Isp, burn-time or stage-mass value appears anywhere on
the glass** — the single largest category of thing KER is actually good at.

---

# 1. ACCESS METHOD — the gating question, answered first

## 1.1 The verdict

**There is a clean, supported way to read KER's live values at runtime, and it is pure reflection — no
compile-time reference, no hard dependency.** But it is **not** "read the static, get a number". KER's values
are computed by *processor* objects that run only when something asks them to, and the asking is normally done
by KER's own visible readouts. **A plugin that only reads the statics gets stale or zero data.** The plugin must
drive the processors itself. That is the whole finding, and it is what the previous research pass got wrong.

## 1.2 What is actually installed (verified, not assumed)

Read from the install directly. Permitted for this task as *researching a dependency* — the same allowance §B2
used for MechJeb. The KSP tree remains off-limits as a **build input** (C7); nothing here is a build input.

| Fact | Value | Evidence |
|---|---|---|
| Mod | Kerbal Engineer Redux **1.1.9.5** (jrbudda's RO fork; original author CYBUTEK) | `GameData/KerbalEngineer/KerbalEngineer.version` |
| Assemblies | `KerbalEngineer.dll` (325 632 B), `KerbalEngineer.Unity.dll` (16 896 B) | file listing |
| Declared KSP range | 1.8.0 – 1.12.9, built for 1.12.5 | `.version` |
| Host KSP | **1.12.5**, build id 03190 | `buildID64.txt` |
| Confirmed loading | `Executing: KerbalEngineer - 1.1.9.5` | `GameData/KerbalEngineer/KerbalEngineer.log` |
| Upstream source | `https://github.com/jrbudda/KerbalEngineer` | `.version` `URL` / `DOWNLOAD` |

**Namespaces present in the installed DLL** (enumerated from the assembly's `#Strings` metadata heap):
`KerbalEngineer.VesselSimulator`, `KerbalEngineer.Flight`, `KerbalEngineer.Flight.Readouts` and its
`.Body` / `.Miscellaneous` / `.Orbital` / `.Orbital.ManoeuvreNode` / `.Rendezvous` / `.Surface` / `.Thermal` /
`.Vessel` children, plus `.Sections`, `.Helpers`, `.Extensions`, `.Editor`, `.TrackingStation`, `.Unity`.

**Every access target named in this document was checked against that binary**, not merely against upstream
`master`. All present: types `SimManager`, `Stage`, `SimulationProcessor`, `RendezvousProcessor`,
`ImpactProcessor`, `AtmosphericProcessor`, `ManoeuvreProcessor`, `ThermalProcessor`, `AttitudeProcessor`,
`FlightEngineerCore`, `IUpdatable`, `IUpdateRequest`; methods `RequestSimulation`, `TryStartSimulation`,
`ResultsReady`, `RequestUpdate`, `AddUpdatable`; properties `Stages`, `LastStage`, `ShowDetails`,
`UpdateRequested`, `Instance`, `Atmosphere`, `Gravity`, `Mach`, `TerminalVelocity`, `DynamicPressure`,
`SuicideAltitude`, `SuicideCountdown`, `SuicideDeltaV`, `PhaseAngle`, `InterceptAngle`, `RelativeInclination`,
`RelativeVelocity`, `TimeTilEncounter`, `SeparationAtEncounter`, `SpeedAtEncounter`; and all 26 `Stage` fields
`KerBridge` binds. *(Two of those, `isp` and `time`, appear in the heap only as shared suffixes of `currentisp`
and `burntime` — ordinary .NET string-heap suffix folding, not an absence.)*

## 1.3 The mechanism — why reading alone does not compute

KER has **two** data planes and they behave differently.

### Plane A — `KerbalEngineer.VesselSimulator.SimManager` (the fuel-flow simulation)

A static class. Runs a RealFuels/RO-aware fuel-flow solve of the whole part tree **on a background thread**
(`ThreadPool.QueueUserWorkItem(RunSimulation, simulation)`). Public surface:

- `static Stage[] Stages` · `static Stage LastStage` — the results.
- `static void RequestSimulation()` · `static void TryStartSimulation()` · `static bool ResultsReady()`
- `static TimeSpan minSimTime` — the throttle floor, **150 ms**.
- `static double Atmosphere` / `Gravity` / `Mach` — **inputs you are expected to set.**
- `static string failMessage`, `static bool hasInstalledRealFuels`, `hasInstalledKIDS`, `vectoredThrust`.

**The trap**, verbatim from KER's source:

```csharp
public static void RequestSimulation()
{
    if (!hasCheckedForMods) { CheckForMods(); }
    lock (locker) { bRequested = true; if (!timer.IsRunning) { timer.Start(); } }
}

public static void TryStartSimulation()
{
    lock (locker)
    {
        if (!bRequested || bRunning || (timer.Elapsed < delayBetweenSims && timer.Elapsed >= TimeSpan.Zero) ||
            (!HighLogic.LoadedSceneIsEditor && FlightGlobals.ActiveVessel == null))
        { return; }
        bRequested = false; timer.Reset();
    }
    StartSimulation();
}
```

`RequestSimulation()` sets a flag and returns. **`TryStartSimulation()` is what actually kicks the run.** In
the flight scene its only caller is `SimulationProcessor.Update()`.

Note also `ResultsReady()` is `return !bRunning;` — it means **"no run in flight right now"**, *not* "results
exist". It returns `true` before the first simulation has ever run, so it is not a validity gate (§1.6).

### Plane B — the readout **Processors** (everything that is not per-stage Δv)

Each readout category has a processor holding its computed values as public statics:

| Processor | Namespace (`KerbalEngineer.Flight.Readouts.…`) | Supplies |
|---|---|---|
| `SimulationProcessor` | `Vessel` | `Stages`, `LastStage`, `ShowDetails`; drives Plane A |
| `ImpactProcessor` | `Surface` | impact point/time + **all five suicide-burn values** |
| `AtmosphericProcessor` | `Surface` | terminal velocity, efficiency, deceleration, pressures |
| `RendezvousProcessor` | `Rendezvous` | ~35 target-relative values |
| `ManoeuvreProcessor` | `Orbital.ManoeuvreNode` | node burn split + post-burn elements |
| `ThermalProcessor` | `Thermal` | hottest/coolest part + skin temps, fluxes |
| `AttitudeProcessor` | `Vessel` | pitch/roll/heading + rates, AoA, sideslip |

All implement `IUpdatable, IUpdateRequest`. They are driven by `FlightEngineerCore`, a `MonoBehaviour` marked
`[KSPAddon(KSPAddon.Startup.FlightAndKSC, false)]` — **so it exists in every flight scene whether or not KER's
window is open.** Its loop, verbatim:

```csharp
private void UpdateModules() {
    foreach (var updatable in this.UpdatableModules) {
        if (updatable is IUpdateRequest) {
            var request = updatable as IUpdateRequest;
            if (request.UpdateRequested) { updatable.Update(); request.UpdateRequested = false; }
        } else { updatable.Update(); }
    }
}
```

**Two gates, both of which we must satisfy ourselves:**

1. **Registration.** A processor only ticks if it is in `UpdatableModules`. Entry is via
   `public void AddUpdatable(IUpdatable updatable)` on `public static FlightEngineerCore Instance { get; private set; }`.
   KER calls it from each readout's `Reset()` — i.e. **only for readouts the user has added to a section.**
2. **Request.** Even when registered, `Update()` runs only for the frame after `UpdateRequested` is set, and the
   flag is cleared immediately. `UpdateRequested` is set by `<Processor>.RequestUpdate()`, which KER's readouts
   call from their own `Update()` — i.e. **only while that readout is being drawn.**

This is KER's own idiom, visible in every readout. `Surface/DynamicPressure.cs`, in shape:

```csharp
public override void Update()  { AtmosphericProcessor.RequestUpdate(); }
public override void Draw(...) { if (AtmosphericProcessor.ShowDetails) { DrawLine(AtmosphericProcessor.DynamicPressure.ToPressure(...), section); } }
```

**⇒ Answer to the gating question: KER does NOT compute continuously, and reading a static does NOT force a
compute.** If the user has never enabled the corresponding readout, the processor is unregistered, never
updated, and its statics hold their initial zeros for the entire flight. DragonScreen must therefore do **both**
steps itself, every flight scene. This is good news, not bad: it means we control the cost, and we can leave
KER's own UI closed.

## 1.4 ⚠ Consequence: `KerBridge.RequestSimulation()` as written can never produce data

`plugin/src/KerBridge.cs:70-77` binds and calls **only** `SimManager.RequestSimulation()`. Per §1.3 that sets
`bRequested` and returns. Nothing in our code calls `TryStartSimulation()`, and `SimulationProcessor` — the only
thing in the flight scene that would — is neither registered nor requested by us. So unless the user happens to
have a Δv readout open in KER's own window, `SimManager.Stages` stays empty and `KerBridge.TryGetStages()`
returns `false` for ever.

This has never been noticed because **`KerBridge` has no callers** (§0). It is a latent defect, not a live one.
**Logged here, not fixed** (C1.1) — see §6.3.

## 1.5 Corrections to the deleted `MOD_INTEGRATION_RESEARCH.md` §1a

Recorded so the two documents cannot be read as agreeing when they do not:

| The old §1a said | Actually |
|---|---|
| *"Call `RequestSimulation()` at our guidance rate; read on `OnReady` or poll `ResultsReady()`."* | `RequestSimulation()` alone never runs anything (§1.4). Drive `SimulationProcessor`, which does the whole housekeeping including `TryStartSimulation()` and the `Gravity`/`Mach` inputs. |
| *"`static bool ResultsReady()` — true when a run finished."* | It is `!bRunning` — **true before the first run too**. Gate on `SimulationProcessor.ShowDetails` (set only once `Stages` *and* `LastStage` exist) or on a non-empty `Stages`. |
| *"`Stages` — index 0 = last/final stage burned … highest = current stage."* | True of the **`number` field**, which is what `pure/KerData.cs` sorts on — so `KerData.Current`/`Final` are correct as written and should stay. The *array index* ordering is not part of KER's contract; do not start relying on it. |
| Listed `static event ReadyEvent OnReady`. | Present, but a cross-assembly reflective event subscription is fragile and would run our delegate **on KER's background thread**. Poll; do not subscribe. |
| No mention of `SimManager.Atmosphere` / `Gravity` / `Mach`. | These are **inputs**. `SimulationProcessor.Update()` sets `SimManager.Gravity` from `mainBody.gravParameter` and `SimManager.Mach` from `vessel.mach`. Drive the processor and they are set for us; bypass it and the results are computed against stale inputs. |
| Stated no licence position. | §5.4. |

Everything else in the old §1 — the `Stage` field list, the readout catalogue, the soft-dependency discipline,
and the §6 audit finding that *"our own `StageStats` is a JUSTIFIED fallback … Not hat-on-a-hat. ✅ keep"* —
is confirmed and carried forward.

## 1.6 The recommended access recipe

Drive **`SimulationProcessor`** (and the other processors), not `SimManager` directly, mirroring KER's own idiom.

Once per flight scene:

```
FlightEngineerCore.Instance.AddUpdatable( <Processor>.Instance )     // for each processor we use
```

Then on our existing 5 Hz tick, for each processor whose values a **currently-visible** page needs:

```
<Processor>.RequestUpdate()        // sets UpdateRequested; FlightEngineerCore runs Update() that frame
```

and read the statics on the **following** tick, gated on `<Processor>.ShowDetails`. Per-stage data continues to
come from `SimManager.Stages` exactly as `KerBridge` already reads it (`SimulationProcessor.Stages` mirrors the
same array).

- **Cadence.** `minSimTime` is a **150 ms** floor; `VesselData.Refresh()` already self-throttles to
  `RefreshInterval = 0.2f` → **5 Hz / 200 ms** (`plugin/src/VesselData.cs:13,52`). These match almost exactly.
  One `RequestUpdate()` per `Refresh()` is the natural cadence and needs no new timer.
- **Only request what is on screen.** The fuel-flow solve walks the whole part tree. KER's own discipline is
  that a readout requests only while drawn; gate on the active page for the same reason.
- **Threading.** The solve runs on a ThreadPool thread. Read `Stages` from the main thread only, after
  `ShowDetails` / a non-empty array. Do not subscribe to `OnReady`.
- **One-frame lag** is inherent (request this frame, value next). At 5 Hz against a 150 ms sim floor it is
  invisible on the glass, and it is what KER's own readouts live with.
- **Independent corroboration, already in our tree.** MOARdV's MAS does exactly this:
  `assets/reference/AvionicsSystems-master/Source/MASIKerbalEngineer.cs` binds
  `SimulationProcessor.RequestUpdate` / `LastStage` / `ShowDetails` and `ImpactProcessor.RequestUpdate` /
  `ShowDetails` / `Latitude` / `Longitude` / `Altitude` / `Time` by reflection, and binds the `Stage` fields
  with generated delegates. (Reference only — look, don't ship, C7.1.)
- ⚠ **The one thing source cannot settle.** MAS is **not** seen calling `AddUpdatable`; it may quietly rely on
  the user having KER's readouts enabled. Whether `AddUpdatable` is required, or whether touching `Instance`
  self-registers, **must be confirmed in the capsule** — see §6.2 V1. Everything else in this section is settled
  from source and binary.

## 1.7 Reflection shape

`KerBridge` already has the right skeleton and it should be extended, not replaced: probe once behind a lazy
`Available` property, cache the handles, reject partial matches, catch everything, expose `bool TryGet…(out …)`.
Extending it to the processors is more of the same — walk `AppDomain.CurrentDomain.GetAssemblies()` for
`KerbalEngineer.Flight.Readouts.<Cat>.<Processor>`, bind the static property getters and `RequestUpdate`, and
treat any null handle as "KER absent". For values read at 5 Hz, prefer `Delegate.CreateDelegate` over repeated
`PropertyInfo.GetValue` (MAS's approach): the same one-off reflection cost, near-direct call cost thereafter.

---

# 2. COMPLETE INVENTORY — every live value KER exposes

KER's readouts are one class per value under `KerbalEngineer/Flight/Readouts/<Category>/`. The list below is
that directory tree in full, for the installed 1.1.9.5. Non-value entries (`Separator`, `ClearSeparator`,
`Crosshair`, `GuiSizeAdjustor`, `LogSimToggle`, `VectoredThrustToggle`, `SystemTime*`, `SystemDateTime`,
`SimulationDelay`, `TargetSelector`, `ImpactMarker`, `ReadoutCategory`, `ReadoutLibrary`, `ReadoutModule`,
`ReadoutModuleConfigNode`) are UI furniture, not data, and are excluded.

**Unit convention.** KER stores SI-ish raw values and formats at draw time through extension methods:
`ToDistance()` → m, `ToSpeed()` → m/s, `ToAngle()` → deg, `ToRate()` → per second, `ToMass()` → t,
`ToForce()` → kN, `ToPressure()` → kPa, `ToAcceleration()` → m/s², `TimeFormatter.ConvertToString()` → a
duration in seconds. Units below are read off those formatters; ones **not** independently confirmed are
marked *(inferred)* and are on the §6.2 verification list.

**Access-path shorthand.** `SIM` = `SimManager.Stages[i].<field>` (via `KerBridge`). `SP` / `IP` / `AP` / `RP` /
`MP` / `TP` / `AtP` = a public static property on `SimulationProcessor` / `ImpactProcessor` /
`AtmosphericProcessor` / `RendezvousProcessor` / `ManoeuvreProcessor` / `ThermalProcessor` / `AttitudeProcessor`.
`KSP` = the readout is a thin wrapper over a stock field and KER adds nothing (§4).

## 2.1 Vessel / Δv / TWR — `Flight/Readouts/Vessel/` (the fuel-flow simulation)

This is the one group where KER does heavy lifting no one else does. All of it comes from `Stage`, whose 26
public fields are listed here once; the readout classes are presentation over these.

| `Stage` field | Units | Meaning | Accuracy / source |
|---|---|---|---|
| `number` | int | KSP stage number; counts **up** toward the currently-burning stage, 0 = final | exact |
| `deltaV` | m/s | Δv of **this** stage | full fuel-flow sim, RealFuels/RO aware |
| `totalDeltaV` | m/s | cumulative Δv from this stage down (= **remaining**, for the current stage) | same |
| `inverseTotalDeltaV` | m/s | cumulative the other way | same |
| `thrust` | **kN** | max thrust at the sim's `Atmosphere`/`Mach` inputs | same |
| `actualThrust` | **kN** | current thrust at current throttle | same |
| `thrustToWeight` | — | TWR at the reference gravity | same |
| `actualThrustToWeight` | — | TWR at current throttle | same |
| `maxThrustToWeight` | — | TWR at burnout (lowest mass) | same |
| `isp` | s | Isp at the sim's inputs | same |
| `mass` | **t** | this stage's start mass | same |
| `totalMass` | **t** | cumulative vessel mass | same |
| `resourceMass` | **t** | propellant mass in this stage | same |
| `time` | s | this stage's burn duration | same |
| `totalTime` | s | cumulative burn duration | same |
| `rcsMass` | **t** | RCS propellant mass | same |
| `RCSIsp` / `RCSThrust` | s / **kN** | RCS-only Isp and thrust | same |
| `RCSdeltaVStart` / `RCSdeltaVEnd` | m/s | RCS Δv at stage start / end mass | same |
| `RCSTWRStart` / `RCSTWREnd` | — | RCS TWR at start / end | same |
| `RCSBurnTime` | s | RCS burn duration | same |
| `maxThrustTorque` | kN·m *(inferred)* | worst-case thrust-induced torque | sim |
| `thrustOffsetAngle` | deg | angle between thrust axis and vessel axis | sim |
| `maxMach` | float | max Mach the stage's engines are rated for | sim |
| `partCount` / `totalPartCount` | int | part counts | exact |
| `cost` / `totalCost` | funds | — | exact |

**`KerBridge` already mirrors 13 of these into `KerStage` in SI** (`plugin/src/pure/KerData.cs:12-26`),
converting **kN → N** and **t → kg** at the boundary (`KerBridge.cs:115,121`). The 13 not yet mirrored — the
whole RCS family, `maxThrustTorque`, `thrustOffsetAngle`, `maxMach`, `totalTime`, `rcsMass`, `partCount`,
`cost`, `inverseTotalDeltaV` — are a one-line-each addition if ever wanted.

Readout classes over the above: `DeltaVCurrent`, `DeltaVCurrentTotal`, `DeltaVStaged`, `DeltaVTotal`,
`RCSDeltaV`, `RCSIsp`, `RCSTWR`, `RCSThrust`, `SpecificImpulse`, `Thrust`, `ThrustToWeight`,
`SurfaceThrustToWeight`, `ThrustTorque`, `ThrustOffsetAngle`, `Mass`, `PartCount`, `Acceleration`, `Throttle`,
`LfOxRatio`, `Gravity`, `Glideslope`, plus the intake-air family (`IntakeAirDemand`, `IntakeAirSupply`,
`IntakeAirDemandSupply`, `IntakeAirUsage` — jet-engine bookkeeping, irrelevant to Dragon).

**Access:** `SIM`, gated on `SP.ShowDetails`, driven per §1.6.

### The suicide-burn set — lives on `ImpactProcessor`, needs **both** processors

| Value | Units | Meaning | Access |
|---|---|---|---|
| `SuicideAltitude` | m | altitude at which the landing burn must start | `IP.SuicideAltitude` |
| `SuicideCountdown` | s | time until that ignition point | `IP.SuicideCountdown` |
| `SuicideDeltaV` | m/s | Δv the burn will consume | `IP.SuicideDeltaV` |
| `SuicideDistance` | m | slant distance to the ignition point | `IP.SuicideDistance` |
| `SuicideLength` | s *(inferred)* | burn duration | `IP.SuicideLength` |

`SuicideBurnAltitude.Draw` gates on **`SimulationProcessor.ShowDetails && ImpactProcessor.ShowDetails`**, and
`ImpactProcessor.RequestUpdate()` itself calls `SimulationProcessor.RequestUpdate()` — the suicide solve needs
the vehicle's thrust/mass model. Drive both.

### Attitude — `AttitudeProcessor`

`Pitch`, `Roll`, `Heading` (deg) and `PitchRate`, `RollRate`, `HeadingRate` (°/s); `AngleOfAttack`,
`AngleOfSideslip`, `AngleOfDisplacement` (deg). **Access:** `AtP.*`.

## 2.2 Surface — `Flight/Readouts/Surface/`

| Value | Units | Meaning | KER's source / accuracy | Access |
|---|---|---|---|---|
| `TerminalVelocity` | m/s | terminal velocity in the current atmosphere | ⭐ **FAR-aware**: when FAR is installed KER reflects into it (`farTerminalVelocity.Invoke`); otherwise `√(2mg / ρ·A·Cd)` | `AP.TerminalVelocity` |
| `AtmosphericEfficiency` | — | `srfSpeed / terminalVelocity` | derived from the above | `AP.Efficiency` |
| *(deceleration)* | m/s² *(inferred)* | aero deceleration | derived | `AP.Deceleration` |
| `DynamicPressure` | kPa | q | **`FlightGlobals.ActiveVessel.dynamicPressurekPa` verbatim — `KSP`** | `AP.DynamicPressure` |
| `AtmosphericPressure` | kPa | static pressure | **`vessel.staticPressurekPa` verbatim — `KSP`** | `AP.StaticPressure` |
| `MachNumber` | — | Mach | **`FlightGlobals.ActiveVessel.mach` verbatim — `KSP`** | `KSP` |
| `GeeForce` | g | current + max-so-far g | **`FlightGlobals.ship_geeForce` verbatim — `KSP`** | `KSP` |
| `ImpactTime` | s from now *(inferred; formatted as a duration)* | time to impact | KER's own ballistic/terrain trace | `IP.Time` |
| `ImpactLatitude` / `ImpactLongitude` | deg | predicted impact point | same trace | `IP.Latitude` / `IP.Longitude` |
| `ImpactAltitude` | m | terrain altitude at the impact point | same | `IP.Altitude` |
| `ImpactBiome` | string | biome at the impact point | same | `IP.Biome` |
| `AltitudeSeaLevel` / `AltitudeTerrain` | m | ASL / AGL | `KSP` (`altitude` / `radarAltitude`) | `KSP` |
| `VerticalSpeed` / `HorizontalSpeed` | m/s | — | `KSP` | `KSP` |
| `VerticalAcceleration` / `HorizontalAcceleration` | m/s² | — | KER differentiates the speeds | readout-local |
| `Latitude` / `Longitude` | deg | vessel position | `KSP` | `KSP` |
| `Biome` | string | current biome | `KSP` | `KSP` |
| `Situation` | string | landed/flying/orbiting… | `KSP` (`vessel.situation`) | `KSP` |
| `Slope` | deg | terrain slope beneath | KER raycast | readout-local |
| `BearingToWaypoint` / `SurfaceDistanceToWaypoint` | deg / m | to the active waypoint | KER great-circle | readout-local |

## 2.3 Orbital — `Flight/Readouts/Orbital/`

**Every one of these is a formatting wrapper over `vessel.orbit`.** Listed for completeness; §4 says take them
from KSP, not KER.

`ApoapsisHeight` (m) · `PeriapsisHeight` (m) · `TimeToApoapsis` / `TimeToPeriapsis` (s) · `SpeedAtApoapsis` /
`SpeedAtPeriapsis` (m/s) · `OrbitalSpeed` (m/s) · `OrbitalPeriod` (s) · `Inclination` (deg) · `Eccentricity` (—) ·
`SemiMajorAxis` / `SemiMinorAxis` (m) · `LongitudeOfAscendingNode` (deg) · `LongitudeOfPeriapsis` (deg) ·
`ArgumentOfPeriapsis` (deg) · `TrueAnomaly` / `MeanAnomaly` / `MeanAnomalyAtEpoc` / `EccentricAnomaly` (deg) ·
`AngleToPrograde` / `AngleToRetrograde` (deg) · `AngleToEquatorialAscendingNode` /
`AngleToEquatorialDescendingNode` (deg) · `TimeToEquatorialAscendingNode` / `TimeToEquatorialDescendingNode` (s) ·
**`TimeToAtmosphere` (s)** — the one in this group with real derivation work behind it.

## 2.4 Rendezvous — `Flight/Readouts/Rendezvous/` (`RendezvousProcessor`)

The richest group after the fuel-flow sim, and the one we currently approximate by hand.

| Property | Units | Meaning |
|---|---|---|
| `Distance` | m | range to target |
| `RelativeSpeed` | m/s | scalar closing/opening rate |
| `RelativeVelocity` | m/s | relative velocity magnitude |
| `RelativeInclination` | deg | plane difference between the two orbits |
| **`PhaseAngle`** | deg | true phase angle about the common body |
| **`InterceptAngle`** | deg | phase angle at which a transfer burn intercepts |
| `TransferAngle` | deg | the Hohmann transfer phase angle |
| `TimeToTransferAngle` | s | wait until phase = transfer angle |
| **`TimeTilEncounter`** | s | time to closest approach |
| **`SeparationAtEncounter`** | m | miss distance at closest approach |
| **`SpeedAtEncounter`** | m/s | relative speed at closest approach |
| `AngleToAscendingNode` / `AngleToDescendingNode` | deg | to the *relative* nodes |
| `TimeToAscendingNode` / `TimeToDescendingNode` | s | to the relative nodes |
| `AltitudeSeaLevel`, `ApoapsisHeight`, `PeriapsisHeight`, `SemiMajorAxis`, `SemiMinorAxis`, `OrbitalPeriod`, `TimeToApoapsis`, `TimeToPeriapsis` | m / s | the **target's** own elements |
| `TargetLatitude` / `TargetLongitude` | deg | target ground position |
| `BearingToTarget` / `SurfaceDistanceToTarget` | deg / m | surface-relative |
| `TimeToPlane[]` / `AngleToPlane[]` / `TimeToPlaneisAsc` | s / deg / bool | launch-window solver for a landed vessel |
| `isLanded`, `landedSamePlanet`, `bodyRotationPeriod`, `targetDisplay`, `sourceDisplay` | — | context |

**Access:** `RP.*`, gated on `RP.ShowDetails`.

## 2.5 Manoeuvre node — `Flight/Readouts/Orbital/ManoeuvreNode/` (`ManoeuvreProcessor`)

| Property | Units | Meaning |
|---|---|---|
| `TotalDeltaV` | m/s | node Δv magnitude |
| `ProgradeDeltaV` / `NormalDeltaV` / `RadialDeltaV` | m/s | the three components |
| `BurnTime` / `HalfBurnTime` | s | full and half burn duration |
| `UniversalTime` | s (UT) | node epoch |
| `AvailableDeltaV` | m/s | Δv remaining in the burning stage |
| `HasDeltaV` | bool | whether the node is achievable |
| `FinalStage` | int | which stage performs the burn |
| `PostBurnAp` / `PostBurnPe` | m | resulting apses |
| `PostBurnEcc` / `PostBurnInclination` / `PostBurnPeriod` | — / deg / s | resulting elements |
| `PostBurnRelativeInclination` | deg | vs the target's plane |
| `AngleToPrograde` / `AngleToRetrograde` | deg | node position |
| `TripDeltaV` | m/s | sum over all nodes |

`ManoeuvreProcessor.RequestUpdate()` also requests `SimulationProcessor` — burn time needs the thrust model.
**Access:** `MP.*`.

## 2.6 Thermal — `Flight/Readouts/Thermal/` (`ThermalProcessor`)

`HottestPart` / `CoolestPart` (part name) · `HottestTemperature` / `CoolestTemperature` (K) ·
`HottestSkinTemperature` / `CoolestSkinTemperature` (K) · `CriticalPart`, `CriticalTemperature`,
`CriticalSkinTemperature`, `CriticalThermalPercentage` (fraction of limit) · `ConvectionFlux`,
`RadiationFlux`, `InternalFlux` (kW *(inferred)*). All are a part-tree scan over stock thermal fields.

## 2.7 Body — `Flight/Readouts/Body/`

`BodyName`, `CurrentSoi`, `BodyRadius` (m), `Mass` (kg), `Gravity` (m/s²), `EscapeVelocity` (m/s),
`RotationPeriod` (s), `OrbitalPeriod` (s), `HasAtmosphere` / `HasOxygen` (bool), `HighAtmosphereHeight`,
`LowSpaceHeight`, `HighSpaceHeight`, `MinOrbitHeight`, `GeostationaryHeight` (m). All are
`CelestialBody` fields or one-line derivations — `KSP`.

---

# 3. MAPPING + WIRING PATH TO OUR SCREENS

## 3.1 The wiring path — five hops, none of them new

The chain already exists and is proven by the TAC-LS water row, which is the repo's existing tier-2
mod-sourced datum. A KER value follows exactly the same path:

| # | Hop | Where | Precedent (`WaterText`) |
|---|---|---|---|
| 1 | **Mod read (reflection, guarded)** | `plugin/src/KerBridge.cs` — extend with the processors | `plugin/src/LifeSupportBridge.cs` |
| 2 | **SI mirror + pure logic** | `plugin/src/pure/KerData.cs` — `KerStage` + selection, headless-tested | `LsState` + `pure/LifeSupport.cs` |
| 3 | **Glue read → state field** | a new `Propulsion(v);` sibling in the one-liner block at `plugin/src/VesselData.cs:206-211`, inside the existing 5 Hz `Refresh()` and its `try/catch` | `VesselData.cs:180-187` |
| 4 | **`PageState` field** | `plugin/src/pure/Pages.cs` — `XxxText` (pre-formatted, unit-carrying for a row, bare for a gauge; **two** fields if both), plus `Xxx01`/raw double if geometry needs it; doc-comment naming source, units and what `null` means | `pure/Pages.cs:213-214` |
| 5 | **Page readout** | the page's `T(...)`/`F(...)` guards turn `null` into the `Dash` const | `pure/VehicleSubsystemPage.cs:63-64, 270-274` |

Two rules from the existing contract that a KER block must honour:

- **`null` means dash.** There is no per-field `HasX` bool for strings. `VesselData` writes `null` when KER is
  absent or has not produced a result, and the page dashes (`pure/Pages.cs:174-175`).
- **`KerBridge.RequestSimulation()` goes in `VesselData.Refresh()`, never in `ScreenPainter.Update()`** —
  that method runs **once per screen, three times a frame** (`ScreenPainter.cs:781-782`). `Refresh()` is
  frame-guarded and 5 Hz-throttled, which is the cadence §1.6 wants.

⚠ **The docked caveat, and it is a real one.** KER simulates the **whole `Vessel`**, and KSP merges both craft
into one `Vessel` when docked. So a Δv or TWR read from KER while berthed to the ISS is the *stack's*, not the
Dragon's. `plugin/src/DockedSide.cs` exists precisely for this class of bug (its header: *"⛔ WHEN DOCKED, KSP
MERGES BOTH CRAFT INTO ONE `Vessel`. … THIS IS A LIVE BUG IN F9I AND IT MUST NOT BE PORTED"*) — but it cannot
help here, because the merge happens *inside KER's simulation*, before we see the number. **Any KER-sourced
Δv/TWR/mass row must dash (or be labelled) while `PageState.Docked` is true.**

## 3.2 The mapping — every currently-dashed or approximated value vs. KER

`docs/TELEMETRY_REGISTRY.md` is well behind the code (it covers the Docking page + core chrome + NAV; the six
Vehicle sub-tabs are documented only in `pure/Pages.cs:172-274`). So this table cross-references **both** the
registry and the actual dash sites found in the pages.

### ✅ KER feeds it — genuine wins

| Screen readout | Today | KER value | Path |
|---|---|---|---|
| **`Thrust Avail`** — PROPULSION rows | dash (`pure/VehicleSubsystemPage.cs:314`) | `Stage.thrust` / `actualThrust` | `SIM` → `KerStage.ThrustN`/`ActualThrustN` (already mirrored) |
| **SPLASHDOWN TIME** (`SPLASHDOWN_ETA`, registry `:56`) | `radarAltitude / -verticalSpeed` — a naive constant-rate divide (`VesselData.cs:128`); registry names the deleted `ReturnControl` as authority | `IP.Time` (+ `IP.Latitude`/`Longitude`) | `ImpactProcessor` → new `PageState` field, replacing the divide |
| **TARGET LAT / LON** (`TGT_LAT`/`TGT_LON`, registry `:61`) | dash; authority is deleted code | `IP.Latitude` / `IP.Longitude` for the *predicted* point | `ImpactProcessor` |
| **Rendezvous phase angle** | ours, and a **stated approximation** — *"the target is projected into OUR orbital plane"* (`VesselData.cs:525`) | `RP.PhaseAngle` (true), `RP.RelativeInclination` (the error the approximation hides) | `RendezvousProcessor` |
| **Closest approach** | **not computed at all** | `RP.TimeTilEncounter`, `RP.SeparationAtEncounter`, `RP.SpeedAtEncounter` | `RendezvousProcessor` |
| **Δv / TWR / burn time / Isp / stage mass** | **nothing on the glass anywhere** | the whole `Stage` set | `SIM` — `KerStage` already carries 13 fields in SI |
| **Terminal velocity** | not computed | `AP.TerminalVelocity` — **FAR-aware**, and FAR *is* installed | `AtmosphericProcessor` |
| **Manoeuvre-node burn time / half-burn / post-burn elements** | nothing | `MP.*` | `ManoeuvreProcessor` |
| **Suicide-burn set** | nothing — and `pure/Hoverslam` **does not exist** despite two file headers claiming it as the fallback | `IP.SuicideAltitude/Countdown/DeltaV/Distance/Length` | `ImpactProcessor` + `SimulationProcessor` |

### 🔶 KER could feed it, but a KSP-direct or existing read is better (§4)

| Readout | Today | Verdict |
|---|---|---|
| MAX Q marker (`pure/StepList.cs:112` shows a **hard-coded `"T+1:02"`**) | `q` is read at `VesselData.cs:398` and **thrown away** after latching `maxQPassed` | Publish the number we already read. KER's `DynamicPressure` *is* `vessel.dynamicPressurekPa`. **Do not route through KER.** |
| Mach | `pure/AscentPage.cs:72` shows a hard-coded `"T+1:09 — MACH 1"` | `vessel.mach` direct. KER adds nothing. |
| g-force | live (`v.geeForce`, `VesselData.cs:160`) | already correct |
| Ap/Pe/Inc/period/time-to-apsis | live from `v.orbit` | already correct |
| Hull / TPS temperature | ours, max-over-parts (`VesselData.cs:286-304`) | equivalent to `ThermalProcessor`; keep ours, no dependency earned |

### ❌ KER cannot help — these are modelling gaps, not fuel-flow gaps

`Humidity` · `HELIUM`, `PROP TEMP`, `Chamber Press`, `SuperDraco Temp` · `BUS A`/`BUS B` volts, `Bus Load`,
`Battery Temp` · `FC LOAD`, `BUS TRAFFIC`, `LINK MARGIN`, `STORAGE`, `FC1/2/3`, `GPS Sats`, `Data Rate` ·
`RADIATOR`, `Loop A/B Flow`, `Heat Reject`, `Cabin HX` · the four `Orbit n Subtank` rows and the whole
`MARGIN` column (`pure/VehicleOverviewPage.cs:163-164, 201`) · `WATER UPRIGHTING`
(`pure/VehicleMechPage.cs:67,146`) · `SEAT n TACH` · the two `PowerUnit*Text` rows (KSP has one
`ElectricCharge` pool) · `chrome.LinkName`/`LinkTimer`, frozen at `"COM1/TLM"` / `"00:00:00"`
(`ScreenPainter.cs:210-236`).
These remain §14.4(e) step (2) — coherent marked simulation — or step (3) honest dash.

### ⛔ Explicitly NOT a KER target

The four **SLEW** rows on `pure/DeorbitBurnPrepPage.cs:129-132` (`ROLL`, `PITCH`, `YAW`,
`MAXIMUM ATTITUDE RATE`) are **commanded** attitudes, which only Part B's flight software will produce.
§14.4(e) step (3) names them by example as dash-until-Part-B. `AttitudeProcessor` supplies *measured*
attitude, which is a different quantity — substituting it would be exactly the fabrication §14.4(e)'s guardrail
forbids. **Leave them dashed.**

## 3.3 The biggest wins, ranked

1. **Per-stage and remaining Δv, TWR, burn time, Isp, stage mass.** Nothing of this kind exists on the glass.
   The bridge, the SI mirror and the tested selection logic are all already written — this is the shortest path
   from "KER is installed" to "a real number on a screen".
2. **Closest approach (time / separation / speed).** Cannot be shown at all today, and it is the number that
   makes a rendezvous page a rendezvous page.
3. **True phase angle + relative inclination.** Replaces an approximation our own code already confesses to.
4. **Splashdown / impact time and point.** Replaces a naive divide with a real trace, and revives a registry
   row (`SPLASHDOWN_ETA`) whose declared authority was deleted.
5. **FAR-aware terminal velocity.** We would otherwise have to write FAR reflection ourselves; KER has it.

---

# 4. KER vs SELF-COMPUTE vs KSP-DIRECT

**The discipline: take a KER dependency only where KER genuinely earns it.** KER's readout catalogue is
seductive because it is long, but a large fraction of it is presentation over stock fields. Routing those
through KER would add a dependency, a reflection hop and a frame of lag for **zero** accuracy gain — and would
violate the registry's one-authoritative-source-per-datum rule (`docs/TELEMETRY_REGISTRY.md:16`) by creating a
second path to a number we already have.

| Quantity | Source to use | Why |
|---|---|---|
| Per-stage / total / remaining **Δv** | **KER** | Full fuel-flow sim with RealFuels/RO engine models, crossfeed, per-engine grouping. Our `pure/StageStats.cs` is a *single-segment closed-form* Tsiolkovsky solve with no crossfeed, no atmospheric Isp curve, no residuals, no ullage — and its glue was never written, so it has never run on a real vessel. |
| **TWR** (start / current / max / surface) | **KER** | Falls out of the same sim, at the correct per-stage masses. |
| **Burn time**, **Isp**, **stage / propellant mass** | **KER** | Same sim; ours needs the same never-written glue. |
| **Suicide burn** (5 values) | **KER** | We have **no** hoverslam code at all — `pure/Hoverslam` was deleted and two file headers still wrongly promise it. |
| **Impact time / lat / lon / altitude / biome** | **KER** | A real terrain-aware trace. Ours is `radarAltitude / -verticalSpeed`. |
| **Closest approach** (time / separation / speed) | **KER** | We do not compute it. |
| **Phase angle**, **intercept angle**, **relative inclination** | **KER** | Ours is a stated in-plane approximation (`VesselData.cs:525`). |
| **Terminal velocity** | **KER** | Only because it is FAR-aware and FAR is installed. Without FAR this would be self-compute. |
| **Manoeuvre-node burn split + post-burn elements** | **KER** | Needs the thrust model; we have no node maths. |
| **Dynamic pressure (q)**, **static pressure** | **KSP-direct** | KER's values are literally `vessel.dynamicPressurekPa` / `staticPressurekPa`. We already read q at `VesselData.cs:398` and discard it — publish it, don't proxy it. |
| **Mach** | **KSP-direct** | KER's readout is `FlightGlobals.ActiveVessel.mach`. |
| **g-force** | **KSP-direct** | KER's readout is `FlightGlobals.ship_geeForce`. Already live at `VesselData.cs:160`. |
| **Ap / Pe / inclination / period / time-to-apsis / all orbital elements** | **KSP-direct** | Every KER orbital readout wraps `vessel.orbit`. Already live. |
| **All Body values** (radius, gravity, escape velocity, SOI, atmosphere bands) | **KSP-direct** | `CelestialBody` fields or one-line derivations. |
| **Altitude ASL/AGL, lat/lon, vertical/horizontal speed, situation, biome, throttle, part count** | **KSP-direct** | Stock fields. |
| **Hull / skin temperature, thermal margin** | **self-compute (already done)** | `VesselData.HottestPart` (`:286-304`) does what `ThermalProcessor` does. No dependency earned. |
| **Pitch / roll / heading and their rates** | **self-compute (already done)** | `VesselData.Rates` (`:1013-1029`) from `v.angularVelocity`. `AttitudeProcessor` adds AoA / sideslip, which no page asks for. |
| **Range / closing rate to target** | **self-compute (already done)** | `VesselData.Docking` (`:456-467`). Correct, and frame-accurate. |
| **Solar array, batteries, ElectricCharge, propellant fractions, crew, resources** | **KSP-direct (already done)** | KER has nothing to add. |

**Net:** KER earns a dependency for **one simulation** (fuel flow → Δv/TWR/Isp/burn time/mass, and the suicide
burn built on it) and **three solvers** (impact trace, rendezvous encounter geometry, manoeuvre-node maths),
plus **one FAR shim** (terminal velocity). Everything else stays where it is.

---

# 5. DEPENDENCY, GUARDING AND LICENCE

## 5.1 KER is a SOFT dependency — and that is a standing owner policy

From the deleted `MOD_INTEGRATION_RESEARCH.md` preamble (owner, 2026-08-28), which remains the governing rule
even though the doc is gone:

> **OPTIONAL mod** (may be absent — KER, BetterTimeWarp, PhysicsRangeExtender): NEVER add a hard dependency on
> it. Either **(a) SOFT-integrate** — read it by reflection IF loaded, fall back to our own pure code if not
> (e.g. `KerBridge`) — or **(b) BUILD the useful behaviour into DragonScreen**.

`KerBridge.cs:8` carries the same line: *"⛔ NEVER make this a hard dependency (user policy 2026-08-28)."*
KER is **not** a hard dependency of the RO/RSS ecosystem (unlike ModuleManager, RealFuels, FAR,
KSPCommunityFixes), so the optional branch applies. **No compile-time reference to `KerbalEngineer.dll`, ever.**

## 5.2 The guarding pattern — three levels, all already in the codebase

**Level 1 — mod present?** `KerBridge.Available`: a lazy one-shot assembly walk behind a `_probed` latch, using
the no-throw `asm.GetType(name, false)`, with **partial-match rejection** (`_available` requires three specific
handles, so a KER version that renames a field degrades to the fallback rather than throwing NREs), the whole
probe in a `try/catch`, and one log line stating which branch was taken. Already written; extend it.

**Level 2 — has this datum been produced for *this* vessel?** Distinct from level 1, and this is the nuance
`LifeSupportBridge` models explicitly:

```csharp
/// <summary>… HasWater is separate from Present because a life-support mod can be installed and this
/// vehicle still carry no water tank - the Crew tab dashes the row in that case rather than printing a
/// confident zero. …</summary>
public bool HasWater;
```

KER needs the same shape: `Available` (assembly found) vs. `ShowDetails` / non-empty `Stages` (a result exists
for the active vessel). `KerBridge.TryGetStages` already returns `false` for the second case.

**Level 3 — the page dashes.** `null` string → `pure/VehicleSubsystemPage.cs:272`'s
`T(string live) => (valid && !string.IsNullOrEmpty(live)) ? live : Dash;`. Same as the CommNet rows (S24), which
are the closest existing template for *"a source that can be switched off"*:

```csharp
CommNet.CommNetVessel conn = CommNet.CommNetScenario.CommNetEnabled ? v.Connection : null;
state.SBandText = (conn != null) ? (linked ? "Linked" : "No Signal") : null;
```

**Plus the docked guard from §3.1** — dash any KER Δv/TWR/mass row while `PageState.Docked`, because KER
simulates the merged stack.

## 5.3 Marking — KER-sourced values are tier-2 under §14.4(e)

§14.4(e) step (1) is exactly this case: *"read it from an existing installed mod if one provides it (tier-2,
MARKED — cabin O2/CO2/water already come from TAC-LS via `LifeSupportBridge`)"*. KER would be the **second**
instance of that pattern.

There is **no per-field runtime provenance on `PageState`** today — nothing in the struct says "this came from a
mod". The codebase supports three marking mechanisms, in ascending cost:

1. **Doc-comment + `docs/TELEMETRY_REGISTRY.md` row.** What `WaterText` does: marked in code and doc, unmarked
   on glass. Cheapest, and consistent with the existing tier-2 precedent.
2. **A `bool` on `PageState` + a pure label const.** What `PlanetCamLive` / `PlanetGeom.NoSignalLabel`
   (`"LIVE 3D — NO SIGNAL"`) does — the only *visible* provenance marker in the build. Marks on glass.
3. **Two `PageState` fields — KER's value and our pure fallback side by side.** This is what
   `KerBridge.cs:10-13` explicitly calls for during validation: the units and stage-index semantics are to be
   *"flight-verified against our own numbers via the recorder cross-check … before any consumer TRUSTS KER over
   its own pure fallback."* ⚠ **That cross-check has never run**, because no consumer exists — so the kN→N and
   t→kg conversions at `KerBridge.cs:115-124` and the `Number` ordering assumption at `KerData.cs:30-31` are
   unvalidated against a live game.

Which of the three to adopt is an **owner call** (§6.1), because option 2 changes what the crew sees.

Whichever is chosen, three things are not optional: a `docs/TELEMETRY_REGISTRY.md` row per datum naming KER as
the authority (the registry's own rule, `:16`); a `PageState` doc-comment naming source, units and what `null`
means; and a REAL/MODELLED-style header note in the bridge, as `pure/CabinEnvironment.cs:17-20` does for TAC-LS.

## 5.4 Licence

**KER is GPL-3.0.** Confirmed from the release metadata for the exact installed build — the 1.1.9.5 listing
records the licence as `http://www.gnu.org/licenses/gpl-3.0.en.html`, creators CYBUTEK and jrbudda, source at
`github.com/jrbudda/KerbalEngineer`. (The repository README credits CYBUTEK as original developer but carries
no licence statement of its own; the release metadata is the authority here.)

**DragonScreen is already GPL-3.0** (`README.md:65-82`), because it already ports MechJeb2's fuel-flow maths.
So the licence question is, in practice, **moot** — but the position should be stated in the same style §B2/§B3
stated MechJeb's, and the distinction matters if the project's licence is ever revisited:

| | MechJeb (§B12.1, planned) | **KER (this doc)** |
|---|---|---|
| What we do | **Vendor pinned source** into our build under a private namespace | **Runtime reflection** against an independently-installed assembly |
| Code copied | Yes — the whole mod | **None** |
| Compile-time reference | Yes | **None** |
| Redistributed | Yes — shipped inside DragonScreen | **No** — the user installs KER themselves; `.gitignore` keeps it out of the repo |
| Copyleft consequence | DragonScreen becomes a GPLv3 combined work; must ship source under GPLv3 | Materially weaker linkage: no derivative work is distributed. **And DragonScreen is GPL-3.0 already**, so no new obligation arises either way. |

**Position:** reading KER's public statics by reflection at runtime, copying no code and redistributing nothing,
creates no distribution obligation beyond the GPL-3.0 DragonScreen already carries. What it *does* require is
**attribution**, in the same house style `README.md` already uses for MechJeb2, MAS and SpaceX-Dragon2-UI —
KER named, linked, licence stated, and cited at each read site. That attribution does not exist yet.

⚠ Two things this is *not*: it is not legal advice, and it is not a licence to start vendoring KER source. If
KER code were ever copied into the tree (rather than reflected against), the MechJeb column applies instead.

---

# 6. WHAT THIS RESEARCH LEAVES OPEN

## 6.1 Owner decisions (batched per C1.9; posed to the overseer per C1.13)

- **(a) Scope.** Which of the §3.3 wins to build, and in what order — or none yet.
- **(b) Marking.** Which of §5.3's three mechanisms a KER-sourced number gets. Option 2 changes what the crew
  sees on the glass, so it is not a build-chat call.
- **(c) Where the Δv/TWR family would live.** There is no propulsion-performance readout anywhere on the three
  screens today, so this is a *new page region*, not a dash to fill — and §1.4's source-of-truth ladder applies
  to its layout before any of it is drawn.

## 6.2 Must be verified in the capsule (needs an owner glass go — C1.12)

- **V1.** Whether `FlightEngineerCore.Instance.AddUpdatable(<Processor>.Instance)` is required, or whether
  touching `Instance` self-registers. §1.6's recipe is correct either way, but the necessity of the
  `AddUpdatable` call cannot be settled from source. **This is the one open item in the access method.**
- **V2.** The `KerBridge.cs:10-13` cross-check that has never run: kN→N, t→kg, and the `Number` ordering.
- **V3.** The units marked *(inferred)* in §2 — `IP.Time` (countdown vs UT), `IP.SuicideLength`,
  `AP.Deceleration`, `maxThrustTorque`, the thermal fluxes.
- **V4.** KER's behaviour while docked (§3.1) — confirm the merged-stack reading empirically before any row
  goes live.

## 6.3 Logged, not done (C1.1 — noticed during this task, out of its scope)

1. **`KerBridge.RequestSimulation()` cannot cause a simulation to run** (§1.4). Latent, since nothing calls it.
2. **Two dangling doc references** — `plugin/src/KerBridge.cs:8` and `plugin/src/pure/KerData.cs:1` cite
   `docs/MOD_INTEGRATION_RESEARCH.md`, deleted 2026-09-01. They should point at this file.
3. **Two stale fallback promises** — the same two headers claim a `pure/Hoverslam` fallback. **No such file
   exists**; it was deleted with the autopilot. The comments promise a safety net that is not there.
4. **`KerBridge.cs` ships as dead code** in every built DLL (`build.py:187` globs all of `src/`).
5. **`docs/INDEX.md` does not list this file.** Not updated here — a task writes only its declared outputs
   (C1.11), and this task's declared output is this doc alone.
6. **`docs/TELEMETRY_REGISTRY.md` is well behind the code** — it has no row for any of the six Vehicle
   sub-tabs, and its `SPLASHDOWN_ETA` / `TGT_LAT` / `TGT_LON` rows still name deleted code as authority.

---

## Sources

**Primary — KER's own source** (`github.com/jrbudda/KerbalEngineer`, master): `VesselSimulator/SimManager.cs`,
`VesselSimulator/Stage.cs`, `Flight/FlightEngineerCore.cs`, `Flight/Readouts/Vessel/SimulationProcessor.cs`,
`Flight/Readouts/Surface/{ImpactProcessor,AtmosphericProcessor,DynamicPressure,MachNumber,GeeForce,ImpactTime}.cs`,
`Flight/Readouts/Rendezvous/{RendezvousProcessor,PhaseAngle}.cs`,
`Flight/Readouts/Orbital/ManoeuvreNode/ManoeuvreProcessor.cs`, `Flight/Readouts/Vessel/{DeltaVStaged,SuicideBurnAltitude}.cs`,
and the full directory listings of all seven readout categories.

**The installed binary** — `GameData/KerbalEngineer/{KerbalEngineer.dll, KerbalEngineer.version,
KerbalEngineer.log}`, 1.1.9.5 on KSP 1.12.5; type/member presence confirmed by metadata scan.

**Repo** — `plugin/src/{KerBridge,LifeSupportBridge,VesselData,ScreenPainter,DockedSide,HullCams}.cs`,
`plugin/src/pure/{KerData,Pages,StageStats,CabinEnvironment,VehicleSubsystemPage,VehicleOverviewPage,
DeorbitBurnPrepPage,StepList,AscentPage}.cs`, `plugin/test/{KerDataTest,TestMain}.cs`, `plugin/build.py`,
`docs/{BUILD_PLAN,TELEMETRY_REGISTRY,STATE_CONTRACT}.md`, `README.md`, and
`git show 8b81816^:docs/MOD_INTEGRATION_RESEARCH.md` (prior research, deleted).

**Reference only (look, don't ship — C7.1)** — `assets/reference/AvionicsSystems-master/Source/MASIKerbalEngineer.cs`.
