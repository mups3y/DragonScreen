> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGH**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-25; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# RO + TestFlight engine mechanics — how they actually work

Compiled 2026-08-23 by reading the real configs in this install (RealismOverhaul/TestFlight_Generic_Engines.cfg,
the RO engine templates, and the ModuleManager.ConfigCache). This is the reference for anything touching
booster/upper-stage IGNITION, RELIABILITY, ULLAGE, or FUEL. It exists because I wasted flights guessing at
symptoms (ullage, ignition lead) when the real cause was TestFlight reliability + an MM pass-order mistake.
Companion to [RO_MODS_MECHANICS.md](RO_MODS_MECHANICS.md) (reaction wheels, FAR, the whole mod list).

## 1. The booster octaweb is ONE part, THREE modes, and a CLUSTER
`TE_19_F9_S1_Engine` carries THREE `ModuleEngineConfigs` / `ModuleEnginesRF`, selected by the Tundra engine
switch: **AllEngines** (9), **ThreeLanding** (3), **CenterOnly** (1). Each has three thrust CONFIGs —
`Merlin1D`, `Merlin1D+`, `Merlin1D++` — and the craft flies **Merlin1D++**. Each config has its OWN:
- `ignitions = 4` — hard cap on relights **per config**. `ignitions = 0` seen elsewhere means UNLIMITED,
  but the booster Merlin is **4**.
- `TESTFLIGHT { … }` node with the reliability inputs (below).
Because it is a cluster, RO multiplies the ignition/cycle FAILURE chance by the engine count
(`clusterMultiplier`): ThreeLanding ×3, AllEngines ×9. A cluster is intrinsically less reliable than one engine.

## 2. TestFlight ignition reliability — the pipeline (RO/TestFlight_Generic_Engines.cfg)
Ignition reliability is NOT a single number the engine reads at runtime. RO builds a **curve** at MM-patch time:

1. **Source** (in each CONFIG): `TESTFLIGHT { ignitionReliabilityStart (fresh, e.g. 0.999348),
   ignitionReliabilityEnd (proven), cycleReliabilityStart/End }`.
2. **`:BEFORE[zTestFlight]`** — RO MOVES the whole `TESTFLIGHT` node OUT of `MODULE[ModuleEngineConfigs]/CONFIG`
   up to the PART, and copies `clusterMultiplier` into it.
3. **`:FOR[zTestFlight]`** — RO:
   - transforms Start/End by the cluster: `failChance = (1 − reliability) × clusterMultiplier`, back to reliability;
   - builds **`baseIgnitionChance`** = an FloatCurve over **data units (du), x = 0…10000** → reliability.
     At **0 du (fresh)** reliability = `ignitionReliabilityStart`; at 10000 du = `…End`; with a "kink"
     (reliabilityMidH/MidV) — you gain most reliability early. **Sandbox has no career du persistence, so an
     engine starts each flight near 0 du = the Start value**, accruing du (burn time) during the flight.
   - builds the failure MODULEs (`TestFlightFailure_IgnitionFail`, `_ShutdownEngine`, `_Explode`, …).

**Ignition failure chance at a light** ≈ `(1 − baseIgnitionChance(du)) × ignitionDynPresFailMultiplier(q)`
`+ additionalFailureChance`, then weighted among the failure types. So it depends on: heritage (du), the
cluster penalty, and DYNAMIC PRESSURE.

### Dynamic-pressure curve (helps low, hurts high)
`ignitionDynPresFailMultiplier` pressureCurve: `q=0→1, 5 kPa→1, 15 kPa→0.85, 30 kPa→0.4, 50 kPa→0.15`.
So a HIGH-q light (dense air, the landing burn ~30 kPa) is MORE reliable (×0.4); a near-vacuum light
(the entry burn ~1.5 kPa) is the baseline (×1). Dyn pressure was never the landing problem.

### `TestFlightFailure_IgnitionFail` recovery — why retries don't save it
From the config: **`restoreIgnitionCharge = False`** (a failed light CONSUMES an ignition charge and does
NOT give it back), **`oneShot = True`**, `duFail = 900`, `severity = major`. Consequences:
- A failed ignition is **not repaired in flight** — re-`Activate()` just attempts the NEXT charge.
- With `ignitions = 4` and the entry burn already using one, only ~3 landing attempts exist; a retry storm
  (our `IgniteWithRetry` fired 7–9×) BURNS THE BUDGET and then every further attempt is a no-op.
- So the real defense is **not the retry — it is a reliability high enough that the first light succeeds.**
  The retry is only a rare backstop while charges remain.

## 3. ⛔ HOW TO PATCH RELIABILITY CORRECTLY (the lesson that cost flights)
RO consumes `ignitionReliabilityStart` at `:BEFORE`/`:FOR[zTestFlight]` (moves the node, then builds the
curve). **A `:FINAL` patch is TOO LATE** — the node has been moved out of the CONFIG (so `@CONFIG{@TESTFLIGHT}`
matches nothing) and the curve is already built. Patch must:
- run **`:BEFORE[zTestFlight]`** (before RO moves the node and before it builds the curve), and
- edit the `TESTFLIGHT` node **while it is still inside the CONFIG**: `@MODULE[Module*EngineConfigs],* {
  @CONFIG[<name>],* { @TESTFLIGHT { @ignitionReliabilityStart = … } } }`.
- Ordering within the pass is by GameData path (alphabetical): **Crew2_Patches < RealismOverhaul**, so our
  edit lands before RO's move. Set the RAW value; RO still applies the cluster penalty and pressure curve on
  top. This is exactly what `GameData/Crew2_Patches/F9_S1_reliability.cfg` now does (0.99995 raw → ~0.9999
  effective for the landing). Verify by flying: IgnitionFail should essentially disappear from KSP.log.
Reliability improves with du in career; in sandbox it does not persist, so the Start value IS the flight value.

## 4. RealFuels ignition — ULLAGE + IGNITOR RESOURCE
A `ModuleEnginesRF` with `ullage = True`, `pressureFed = False` (both Merlins) needs:
- **SETTLED propellant.** After a coast/glide with the engine off, propellant floats; the Merlin will NOT
  light on unsettled propellant even with a good TestFlight roll. We settle by firing RCS forward (UllageFore)
  for ~2 s before the light — measured: entry burn eng=0 to +2 s (settle), eng=1 at +2.2 s, thrust +3 s.
  ⚠ THE LANDING BURN NEEDS THIS TOO (drag does NOT settle it — added 2026-08-23).
- **IGNITOR_RESOURCE.** Each config lists `TEATEB 1.0 + ElectricCharge 0.5` per light. TEATEB is `NO_FLOW`
  (drawn only from the engine part), and RO's `RO_TE_Falcon_9.cfg` strips onboard resources, so we add it
  back in `Crew2_Patches/F9_S1_TEATEB.cfg` (8 units). Out of TEATEB OR out of `ignitions` = no light, no fault.
- **Spool is SLOW.** availThr ramps over seconds after ignition, so a relight's command→useful-thrust is
  ~3–5 s total (settle + light + spool). Land-burn ignition lead must cover ALL of it (LandingIgnitionLeadS).

## 5. Propellants, mass, "reserve"
- Booster propellant is **CooledRP-1 / CooledLqdOxygen** (NOT stock LiquidFuel/Oxidizer) — so the recorder's
  `b_lfFrac`/`b_oxFrac` read ~0 and are USELESS for the booster; use `b_massT`.
- RealFuels leaves only a tiny unusable residual; 12 t at landing is plentiful. When an engine "won't light
  with fuel in the tank," suspect **ullage / ignition**, not a shortage (confirmed 2026-08-23).
- Slower staging (MECO velocity cap) leaves the booster HEAVIER at separation (more recovery propellant).

## 6. Quick map: symptom → real cause
| symptom | look here first |
|---|---|
| `TestFlightFailure_IgnitionFail` in log | reliability curve (du/cluster/pressure); is the patch in the RIGHT pass? |
| engine commanded, `eng=0`/`availThr=0`, no TF fault | ULLAGE not settled, or out of TEATEB/`ignitions` |
| light takes seconds to make thrust | RealFuels spool — size the ignition lead for it |
| booster fuel fractions read 0 | wrong resource (Cooled*), use mass |
| ignition budget exhausted | `restoreIgnitionCharge=False` + retry storm burned the 4 charges |
