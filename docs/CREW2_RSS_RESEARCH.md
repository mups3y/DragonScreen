> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGH**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-23; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# Crew-2 in RSS/RO — research (timeline to the second + RO mechanics)

Primary sources only (no Wikipedia). This is the fidelity target and the physics the guidance must obey.
Compiled 2026-08-22.

## 1. The Crew-2 timeline, to the second
Sources: [Spaceflight Now Crew-2 timeline](https://spaceflightnow.com/2021/04/22/crew-2-mission-timeline/),
[NASA Crew-2 timeline PDF](https://www.nasa.gov/wp-content/uploads/2021/11/crew-2_timeline_-_april_23_0.pdf),
[spacex.com/CREW-2](https://spacex.com). Launch 2021-04-23 **05:49:02 EDT** from **LC-39A**.

### Countdown (drives the RO launch technique)
| T- | event |
|---|---|
| 0:35:00 | RP-1 load (both stages) + S1 LOX load begins |
| 0:16:00 | S2 LOX load begins |
| **0:07:00** | **1st stage engine chill begins** (turbopump/line thermal conditioning) |
| 0:05:30 | Dragon → internal power |
| 0:05:20 | Strongback retraction begins |
| **0:04:00** | **M-Vac engine igniter purge** (TEATEB ignitor line — see RO ignitions below) |
| 0:03:00 | S2 LOX tank full |
| 0:02:00 | F9 prop tanks **pressurize for flight** |
| 0:00:45 | GO for launch |
| **0:00:03** | **Engine controller commands ignition sequence** — 9 Merlins light, spool up |
| **0:00:00** | **LIFTOFF** — hold-downs release AFTER thrust is confirmed good |

⇒ The real pad sequence is **ignite (T-3s) → spool up while clamped → confirm thrust → release clamps (T-0)**.
Never release before thrust is up; if an engine fails to light, you abort on the pad.

### Ascent + booster recovery (droneship, downrange — NOT RTLS)
| T+ | event |
|---|---|
| 0:53 | Stage-1 throttle bucket (throttle DOWN for max Q) |
| **1:02** | **Max Q** |
| 1:09 | Mach 1 |
| **2:36** | **MECO** (9 Merlins cut) |
| **2:39** | **Stage separation** (MECO + 3 s) |
| **2:47** | **M-Vac ignites** (SES-1; MECO + 11 s, sep + 8 s) |
| **7:27** | **1st stage entry burn** |
| **8:47** | **SECO-1 / orbit insertion** (M-Vac cutoff) |
| **9:03** | 1st stage landing burn |
| **9:30** | **1st stage lands on droneship** (downrange, Atlantic) |
| **11:58** | **Dragon separates from S2** + Draco checkouts |
| 13:02 | Dragon nosecone open sequence |
| 49:23 | Phase burn (Draco) |

### Rendezvous (day 2) — the named-burn L-approach (matches REAL_CREW_DRAGON_MISSION.md)
| T+ | event |
|---|---|
| 27:06:46 | Transfer burn (Draco) |
| 27:53:12 | Coelliptic burn |
| 28:48:56 | Out-of-plane burn |
| 29:40 | **Approach initiation** — range **7.5 km** from ISS |
| 30:05 | Approach midcourse burn |
| 30:25 | **Waypoint 0** arrival |
| 30:49 | Docking axis / **Waypoint 1** arrival |
| 31:00 | **Waypoint 2** arrival + hold |
| **31:10** | **Contact & capture — IDA-2, Harmony forward port** |
| 31:23 | Docking complete, hooks closed |
| 32:15 | Hatch open |

Return (we SKIP the 199-day stay; user undocks on command): splashdown **off Pensacola, FL** (Gulf),
under 4 parachutes. Need the exact Pensacola lat/lon for the de-orbit aim when we build the return.

## 2. RO engine mechanics — GROUND TRUTH from this install's `ModuleManager.ConfigCache`
The real constraints the guidance must respect. Both stages are RealFuels `ModuleEnginesRF`.

### S2 — Merlin 1D Vacuum (`Merlin1DVac`, cache line 746112)
- `ullage = True`, `pressureFed = False` → **needs settled propellant to light** (turbopump; RCS ullage first).
- **`ignitions = 4`** — only 4 starts. Each consumes `IGNITOR_RESOURCE`: **1.0 TEATEB + 0.5 EC**. Finite TEATEB.
- **`ignitionReliabilityStart = 0.967`** → ~3.3% chance a light just FAILS (→ 0.995 with flight heritage).
- `minThrust 360 / maxThrust 805 kN` (`Merlin1DVac+` Block-5 = 934). Isp `345 vac / 216 SL`. ratedBurnTime 375 s.

### S1 — Merlin 1D (`Merlin1D`, cache line 746794)
- `ullage = True`, **`ignitions = 4`**, `ignitionReliabilityStart = 0.986` (~1.4% fail).
- `minThrust 290 / maxThrust 742.4 kN` (per engine; ×9). ratedBurnTime 180 s.
- ⛔ Booster recovery spends **3 of the 4**: ascent (#1) + entry burn (#2) + landing burn (#3). Each relight
  needs an **ullage settle** (RCS) AND can fail. One wasted ignition and there is no landing burn.

### What this forces on the guidance (the RO techniques to build)
1. **Pad start**: ignite S1, let it spool ~3 s, CONFIRM thrust (and no failed light) BEFORE releasing clamps.
   Don't stage the clamp blindly.
2. **Ullage before every light** (MVac SES-1, and every booster relight): RCS fore to settle, then ignite.
   Our ascent already ullages 6 s before the MVac; the booster relights need the same.
3. **Never spam ignition** — 4 total per engine, gated by TEATEB. The MechJeb autostage burst (8 stages in
   90 ms) is exactly what murders the ignition budget. Command engines by part, once, with a settle.
4. **Detect a failed ignition and RETRY within budget**: after ullage + ignite, if no thrust in ~1-2 s,
   re-command the light (costs another ignition/TEATEB) rather than staging the stack apart.
5. **PhysicsRangeExtender**: droneship landing is ~hundreds of km downrange while the Dragon flies on. PRE
   must keep BOTH loaded (stock unloads at ~300 km — see falcon-physics-range-clamp). User says PRE is
   installed; VERIFY its range is set large enough for the real downrange distance before flying recovery.

## ⛔ THE CREW-2 FLIGHT — SpaceX's OWN 6 STEPS TO DOCK (spacex.com/CREW-2, verbatim)
The mission is built to follow these six steps. Each maps onto an existing DragonScreen phase/system;
"build the flight" = fly this sequence end-to-end, RSS-gated. Column 3 = current RSS status.

| # | SpaceX step (their words) | our phase / system | RSS status |
|---|---|---|---|
| 1 | **LIFTOFF** — F9 first stage lofts Dragon to orbit; stages separate; second stage accelerates Dragon to orbital velocity | `Ascent` (Ascent.cs + AutoPilot): VerticalRise→GravityTurn→MECO→SeparateBooster→MVac→**UPFG**→SECO, target 200 km | **← ACTIVE GATE**: UPFG flies S2 (replaces the loft). Ullage-poisoned-Init loft bug FIXED 2026-08-22 (`UpfgMinThrustKn` gate) — proven in scratchpad/sim, awaiting reflight. Booster→droneship is a parallel recovery branch (BoosterRecovery) |
| 2 | **ORBIT ACTIVATION** — Dragon separates from F9 second stage; initial orbit activation + checkouts of propulsion, life support, thermal | `SeparateSecondStage` (drops S2 on the Dragon decoupler, arms Dracos) → `Coast` | built (stock); RSS just needs orbit first |
| 3 | **PHASING BURNS** — Dragon performs Δv orbit-raising to catch up with the ISS | `Phasing` (Phasing.cs / StationApproach) — raise 200→~408 km toward the ISS | built, Kerbin-tuned; needs RSS ISS altitude (420 km) check |
| 4 | **APPROACH INITIATION** — Dragon links comms with ISS and performs its final orbit-raising Δv burn | `Approach` init (Rendezvous) — real Crew-2 does this at **range 7.5 km** (our `ApproachRange`=3 km is a Kerbin law) | built; RSS approach-initiation range = 7.5 km per timeline |
| 5 | **PROXIMITY OPERATION** — Dragon establishes relative nav, arrives on the docking axis, begins autonomous approach | `DirectApproach`/`DockApproach` — WP0(−400 m R-bar)→WP1(220 m)→WP2(20 m) | built |
| 6 | **DOCKING & PRESSURIZATION** — final approach + dock; pressurize, hatch, ingress | `DockingOps`/`DockControl` — IDA-2, **Harmony forward** (B10.IDA) | built |
(Return is separate & we skip the 199-day stay: undock on command → de-orbit → Pensacola splashdown, 4 chutes.)
The phase skeleton is complete end-to-end (built for stock); the ascent is the only phase that does not yet
close in RSS. Once it reaches orbit, walk 2→6 in order, tuning each from a flight.

### EXACT details per step — the RSS/Crew-2 reproduction spec
⛔ In RSS these are the REAL numbers, used DIRECTLY (the whole point of the RSS track - no Kerbin scaling).
Sources: NASA/SpaceX 6-step (spacex.com, nasa.gov/dragon_arrival.pdf), [Spaceflight Now Crew-2 timeline](https://spaceflightnow.com/2021/04/22/crew-2-mission-timeline/),
[Space.com Demo-2 profile](https://www.space.com/spacex-crew-dragon-demo-2-step-by-step.html), REAL_CREW_DRAGON_MISSION.md.

**1. LIFTOFF** — LC-39A, 2021-04-23 05:49:02 EDT. Azimuth into **51.6°** inclination (~43° ground from 28.6 N).
- T+1:02 Max Q (~31 kPa measured) · T+2:36 MECO · T+2:39 stage sep · T+2:47 SES-1 (M-Vac) · **T+8:47 SECO-1**.
- **Insertion orbit ≈ 190 × 205 km @ 51.6°** (our RSS parking target — round to ~200 km for the interim).
- Booster (parallel branch): entry burn T+7:27 · landing burn T+9:03 · **droneship OCISLY T+9:30**, ~500 km downrange.

**2. ORBIT ACTIVATION** — **Dragon separates from S2 at T+11:58**, Draco checkouts begin. Nosecone open T+13:02.
Checkouts: propulsion, life support, thermal. (No orbit change - just the ~190×205 km parking orbit.)

**3. PHASING BURNS** — a series of **~5 phasing / orbit-raising Draco burns**, first at **T+~48 min**, raising Dragon
from ~190 km toward the **ISS at ~400 km (420 for us)**. Named legs (Crew-2 timeline): Phase burn T+49:23 ·
Transfer T+27:06:46 · Co-elliptic T+27:53:12 · Out-of-plane T+28:48:56. Hohmann-family; ~25-27 h to catch up.

**4. APPROACH INITIATION (AI)** — **T+29:40, range 7.5 km behind + BELOW the ISS.** Comm link established, final
orbit-raising Δv burn, midcourse T+30:05. Enters the **Approach Ellipsoid: 2000 m along-track × 1000 m** ("4×2 km").
Offset targeting mandatory (never aim at the station); corridor/keep-out-sphere breach = auto-abort.

**5. PROXIMITY OPERATION** — relative nav, arrive on the docking axis. The approach is an **L, not a line** - up the
R-bar from below, turn onto the V-bar, in along the axis. EVERY waypoint is a HOLD + station-keep awaiting GO:
- **WP0: 400 m directly BELOW the ISS (+R-bar)** — arrive T+30:25, hold, GO.
- **WP1: 220 m in FRONT on the docking axis (V-bar)** — arrive T+30:49, hold, GO.
- **WP2: 20 m from the port** — arrive T+31:00, hold, GO.

**6. DOCKING & PRESSURIZATION** — final approach WP2 → **contact & capture T+31:10 at IDA-2, Harmony FORWARD**.
Hooks closed / docking complete T+31:23. Vestibule pressurization, **hatch open T+32:15**, ingress.

(Return, we skip the 199-day stay: 4 departure burns + ~6-min phasing burn → ~12-min de-orbit → entry → drogues
**5486 m @ ~156 m/s** → mains **1830 m @ ~53 m/s** → **Pensacola** splashdown.)

## ⛔ STOCK vs RSS — ONE codebase, two methods (user rule, 2026-08-22)
Stock KSP keeps the PREVIOUS, tested ways; RSS/RO gets the new methods. Everything divergent gates on
**`AutoPilot.RssBody(v)`** / `BoosterRecovery.EarthRadiusThresholdM` (body radius > 1e6 = Earth):
| concern | STOCK (Kerbin) | RSS/RO (Earth) |
|---|---|---|
| booster sep | `StageManager.ActivateNextStage()` | `SeparateBooster` (interstage decoupler by capability) |
| MVac ignition | lit via MECO staging | `IgniteSecondStage` (ullage settle + retry, 4-ignition budget) |
| starvation `Stage()` | recovers a dead stage in ANY phase | pad start (VerticalRise) ONLY |
| ascent constants | `AscentTarget.Station` (120 km, MaxQ 20) | `ForBody` (200 km, PitchRefAltM 40 km, MaxQ 34) |
| launch azimuth | `LaunchAzimuth` returns ~90 (transparent) | ~43 into the ISS plane |
| station find | "Space X Station" by name | `vesselType == Station` fallback |
| droneship | vessel `DRONESHIP_MAIN` (live position) | fixed OCISLY coordinate (KK static) |
Shared + transparent to stock: flameout-MECO (`Ascent.FirstStageSpent`) never triggers on the tuned
stock ascent. Keep this table honest as methods are added (e.g. the RO spool-then-clamp launch).

## 3. RSS/RO vs the stock build (what changes)
- Earth: R 6371 km, atmosphere ~140 km, LEO ~7.8 km/s, LC-39A 28.6 N. 51.6 deg to the ISS.
- FAR aero (not stock drag); RealFuels (ullage/boiloff/limited ignitions); real Isp/thrust.
- Measured ascent (flight_0822_011349): liftoff ~527 t, TWR ~1.6, **max Q 31 kPa @ 13 km**, MECO ~2587 m/s
  srf at 70 km / apo 121 km, inc → 51.6. Turn pitches over FAST (see SESSION_2026-08-22 ForBody constants).
- Guidance already scale-aware: `LaunchAzimuth` (into the plane), `AscentTarget.ForBody` (Earth pitch/Q/parking).

## 4. Build order (each testable)
1. **RO launch sequence** — ignite S1 clamped, spool+confirm thrust, THEN release clamps by capability
   (not stage the clamp). [ascent glue]
2. **Separation + ignition by capability** — fire the S1/interstage decoupler directly (NOT
   `StageManager.ActivateNextStage`), then ullage + ignite the MVac directly, with failed-light retry.
   This is the CURRENT BLOCKER (S2 exploded on a staging burst). [ascent glue + VehicleParts + Ascent]
3. **Station targeting** — `StationApproach.Find` must match the RSS ISS so LaunchAzimuth aims into its plane
   and the rendezvous has a target. (Flew without targeting the station.) [StationApproach]
4. **Booster droneship recovery** in RSS + PRE range; 3-ignition budget, ullage each relight. [BoosterRecovery]
   - Droneship mod: `Space_X_barge_lander-2.0` (part `SpaceXDroneship`, ~50 m deck at rescaleFactor 1 ≈
     real ASDS, fine for the RO booster). RO patch `zzz_TundraRO_Fixes/Droneship_ROFix.cfg` tags it
     `RSSROConfig=True` so HardRemoveNonRO doesn't delete it. NO rescale needed.
   - ⛔ PLACED AS A KERBALKONSTRUCTS STATIC (2026-08-22) - the floating vessel was too unstable. A KK
     static is NOT a vessel, so `FindDroneship` can't see it. So on Earth the booster aims at the FIXED
     coordinate the static sits at: `BoosterRecovery.DroneshipEarthLatDeg/LonDeg` = **31.559906,
     -76.679988** ([Tunable], = the KK `RefLatitude/RefLongitude`; move the static → update these).
     KK instance: `KerbalKonstructs/NewInstances/KK_GroupCenter_Earth_Of Course I Still Love You.cfg`.
     (Vessel path still supported: name a vessel "Of Course I Still Love You" or carry the droneship part.)
   - Crew-2 landed on **OCISLY** in the Atlantic (~T+9:30). Exact coords never published. Geometric
     placement from LC-39A (28.608 N, 80.604 W), bearing ~48° NE (the crew ground track), downrange:
     500 km → **31.56 N, 76.68 W**; 560 km → **31.91 N, 76.20 W**. Real "exact" spot = where the
     booster's ground track actually comes down; measure from a recovery-fuel flight, place there,
     landing burn nulls the residual. ⚠ Booster currently stages DRY (no recovery propellant) - must
     tune the ascent (lower StageAltM / earlier MECO) to leave S1 fuel before any landing is possible.
5. **Named-burn rendezvous** (Phase→Transfer→Coelliptic→OutOfPlane→AI@7.5km→WP0→WP1→WP2→capture). 
6. **Return** — de-orbit to the Pensacola splashdown, 4 chutes.

## Second-stage restart / ullage — how the REAL vehicle settles propellant (researched 2026-08-22)

Prompted by flight_0822_211853: MVac "Flame-Out! Cause: No propellants / Prop Requirement Met 0.00%"
with the tank FULL and the ullage-hold fix working (x_fore held 0.75 for 121 s, x_ctlZ = −0.75 applied,
RCS on) yet ZERO acceleration — the RCS ullage produces no thrust and never settles the tank.

**Real Falcon 9 / Crew Dragon method (sources below):**
- **SES-1 lights ~8–11 s after stage separation** (MECO+11 s / sep+8 s — matches our Crew-2 timeline,
  T+2:47). It is a SHORT coast: the propellant is still settled at the aft of the tank from the
  first-stage boost, which was under thrust right up to MECO. The engine's own thrust then completes the
  settling. Real vehicles "light while still settled", they do NOT float a full tank and re-settle it.
- The second stage's **GN2 cold-gas nitrogen ACS is primarily attitude/roll control**, a very small
  acceleration. It is NOT a powerful ullage system and cannot rapidly settle a FULL, heavy stage.
- For LONG-coast restarts (minutes later) SpaceX does a dedicated settling maneuver with the GN2 — but
  by then the stage is nearly EMPTY (light), so the same tiny thrust gives useful acceleration. (SpaceX
  once traced a restart failure to FROZEN igniter-fluid lines, not settling — a different failure mode.)

**Why our sim fails:** we coast ~8 s and then try to RE-SETTLE a full ~110 t stack with the Tundra GN2
RCS, which is `thrusterPower = 0.5` kN × 4 ≈ 2 kN → ~0.018 m/s² even if it fired axially — far too weak.
And in the data NO RCS fires at all for the fore command (neither the S2 Nitrogen nor the Dragon's
MonoPropellant — mono flat at 195): the stack's RCS blocks look to have no effective FORE/AFT
(axial) translation authority, so the −Z command fires nothing. Fuel LEVELS/config are all correct
(S2 tank full RP-1/LqdOxygen, S2 RCS Nitrogen 10000 flowing, enableZ=True) — this is a settling-physics
/ thruster-geometry gap, not a fuel-level bug.

**Fix direction (real-behaviour-grounded):** don't rely on re-settling a full stage with weak cold gas.
Light the MVac PROMPTLY at separation while the propellant is still settled from boost (the F9 method),
at low throttle, and let the engine's own thrust take over — minimise the float time. The booster-plume
clearance is the competing constraint (falcon-open-issues #1), so the light-time is a balance, not zero.
If the RealFuels model still floats it to 0 % at MECO, the ullage must be made to actually produce axial
thrust (Dragon Draco translation, or a craft-side ullage provision) — verify in-game whether ANY RCS
thruster fires on the fore command first.

Sources: [Space Launch Report — Falcon 9 v1.2 data sheet (NASA-hosted)](https://sma.nasa.gov/LaunchVehicle/assets/spacex-falcon-9-v1.2-data-sheet.pdf) ·
[Microgravity restart of liquid rocket engines (Romero-Calvo et al.)](https://hanspeterschaub.info/Papers/RomeroCalvo2022d.pdf) ·
[Falcon 9 Block 5 spec (Wevolver)](https://www.wevolver.com/specs/falcon-9-v12-or-full-thrust-block-5)
