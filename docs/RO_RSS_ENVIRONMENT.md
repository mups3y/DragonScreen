# RSS / RO physical environment — real scale, atmosphere, aero, heating

Compiled 2026-08-23 from the real configs in THIS install: the planet pack is **Sol** (a real-scale RSS-family
system, Kopernicus) — `Sol-Configs/Configs/03_Earth-System/03_Earth/Earth-Kopernicus.cfg` — with **FAR**
aerodynamics, **RealHeat** heating, and **Principia**-capable n-body init. Everything scale-dependent in the
guidance gates on `AutoPilot.RssBody(v)` (`mainBody.Radius > 1e6`). Companion to
[RO_MODS_MECHANICS.md](RO_MODS_MECHANICS.md) and [RO_TESTFLIGHT_MECHANICS.md](RO_TESTFLIGHT_MECHANICS.md).

## 1. Earth — the real body (measured, not Kerbin ×10.6)
| property | value | vs stock Kerbin |
|---|---|---|
| `radius` | **6 371 010 m** (6371 km) | Kerbin 600 km → **×10.6** |
| `gravParameter` μ | **3.9860044×10¹⁴ m³/s²** | Kerbin 3.53×10¹² → **×113** |
| surface gravity `geeASL` | **1.00138 g** (~9.82 m/s²) | ≈ same g, but far bigger well |
| `rotationPeriod` | **86 164.09 s** (sidereal day) | Kerbin 21 600 s → **×4** |
| `atmosphereDepth` | **140 000 m** | Kerbin 70 km → ×2 |
| `atmosphereMolarMass` | 0.0289644 kg/mol (real air) | — |
| `adiabaticIndex` γ | 1.4 (real air) | — |

The huge μ with the same surface g is the whole story: orbital SPEEDS and ENERGIES are ~3.4× Kerbin's, so ascent
Δv (~9.4 km/s to LEO) and every burn is far larger, and margins are unforgiving.

## 2. Orbital mechanics — the numbers the guidance must hit (derived from μ, R)
- **LEO circular speed** `v = √(μ/r)`:
  - 200 km parking (our target): `√(3.986e14 / 6 571 010)` = **7 788 m/s** ← matches the ascent UPFG "orb …/7788".
  - 420 km (the ISS): **7 661 m/s**.
- **Orbital period** `T = 2π√(r³/μ)`: 200 km → **~88.3 min** (5 300 s); 420 km → ~92.8 min.
- **Surface rotation** (free Δv eastward) `2πR/rotationPeriod`: equator **465 m/s**; **at LC-39A 28.6°N → 408 m/s**
  (× cos lat). This is why launching EAST is cheapest and why the plane/azimuth math must use the rotating frame.
- **Escape speed at surface** `√(2μ/R)` = **11 186 m/s**.
- **51.6° inclination from LC-39A (28.6°N)**: launch azimuth `sin(β)=cos(i)/cos(lat)` → **β ≈ 45°** inertial;
  the ground track bends from Earth's rotation (our booster track measured ~41.8°, see the barge placement).
- Frame reminders (KSP API): `getPositionAtUT` is world; `getOrbitalVelocityAtUT` is SWIZZLED (needs `.xzy`);
  MechJeb `SwappedOrbitNormal = -(GetOrbitNormal().xzy)`. These bit the plane-window before.

## 3. Atmosphere — real profile, not an exponential toy (Earth-Kopernicus pressureCurve/temperatureCurve)
- **Sea-level pressure 101.325 kPa** (1 atm). Falls ~exponentially with **scale height ≈ 8.5 km**:
  1 km 90.0, 5 km 54.7, 10 km 27.5, 20 km 5.61, 28 km 1.63 kPa. So dynamic pressure/max-Q is real: our MaxQ
  measured ~31 kPa at ~13 km, and `AscentTarget.ForBody` sets MaxQKpa 34 (do NOT throttle below that).
- **Temperature has real LAYERS** (not monotonic): 282.5 K surface → 240.5 K @8 km → **212 K tropopause @15 km**
  → warms to **268 K stratopause @50 km** → 209 K @75 km. Speed of sound (hence Mach) varies with it, so the
  transonic/max-Q region moves with altitude — matters for FAR drag and the throttle bucket.
- **Top at 140 km**, but air is effectively vacuum above ~80 km (q < 0.1 kPa) — which is why the booster's
  measured ballistic coefficient is junk up high and only meaningful low down (see RO_MODS_MECHANICS FAR row).
- `oxygen = True` (jets breathe); `inverseRotThresholdAltitude` 155 km.

## 4. FAR (Ferram Aerospace Research) — voxel aerodynamics, NOT stock drag cubes
- FAR **zeroes every stock drag field** (`maximum_drag/minimum_drag/dragCoeff/angularDrag = 0`) and computes
  lift/drag from the vessel's **voxelised geometry** each frame: real form drag, skin friction, wave drag
  (transonic/supersonic), induced drag, and body lift at angle of attack. A stock `Cd·A` or drag-cube number is
  meaningless here — which is exactly why our impact predictor MEASURES drag from the vehicle's deceleration
  instead of modelling it, and why the capsule-entry `AeroTable` (stock cubes) is wrong under FAR (open item).
- **Reynolds number** uses real air: `viscosityAtReferenceTemp = 1.7894e-5`, `referenceTemp = 288 K` (Earth).
- **Control surfaces** are FAR modules (`FARControllableSurface`, grid fins included), default `maxdeflect 15°`;
  authority scales with dynamic pressure — strong low/dense, useless high/thin. Booster attitude on descent is
  fins + RCS only (no reaction wheels, no gimbal until relight).
- **AoA matters**: a body at an angle of attack gets a side force — this is what the descent "guided lean" uses
  to walk the impact point, and why an over-large lean (past the AoA schedule) both stalls the steering and
  ruins the landing-burn vector.
- Stress: FAR can rip parts off at high q·AoA (`FARAeroStress`) — another reason the entry burn flies straight
  retrograde (no lean) through max dynamic pressure.

## 5. RealHeat — reentry/ascent heating (shock model)
- Replaces stock/Deadly-Reentry heat with a **shock-based convective model**: detached shock, oblique shock cone
  and cylinder, each with heat + coefficient multipliers (`detachedShockHeatMult`, `obliqueShockConeCoeffMult`,
  …), and real **gas composition** that DISSOCIATES at high temperature (O₂/N₂ → monatomic, endothermic) — so
  peak heating is speed³-ish and shield orientation matters.
- Implications: the booster ENTRY BURN exists to cut the peak heating (and downrange) at ~65 km before the
  thick air; the capsule must hold **heat-shield-forward** through entry; chute deploy stays within the real
  drogue/main envelope. Skin temperature is in the recorder (`b_maxSkinK`) — watch it on entry.

## 6. What this forces on the guidance (quick map)
| RSS/RO fact | guidance consequence |
|---|---|
| μ ×113, LEO 7.8 km/s | real staged Δv budget; ascent must be efficient; MECO velocity SETS booster downrange |
| real scale-height atmosphere | max-Q ~31 kPa @13 km, throttle ceiling not bucket; drag bleeds most entry speed |
| FAR voxel aero | MEASURE drag, don't model it; fins/RCS only on descent; straight-retrograde entry burn |
| RealHeat shock heating | entry burn for heat + downrange; shield-forward capsule; watch b_maxSkinK |
| 465 m/s equatorial spin | launch east; plane/azimuth in the rotating frame; 408 m/s free at LC-39A |
| 140 km atmosphere, vacuum >80 km | booster bc unmeasurable high up; PhysicsRangeExtender for the downrange booster |
