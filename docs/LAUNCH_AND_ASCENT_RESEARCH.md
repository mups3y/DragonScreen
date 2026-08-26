# Falcon 9 / Crew Dragon — Launch & Ascent research record

Purpose: a single, complete, source-backed reference for how SpaceX and NASA fly a crewed Falcon 9
from the pad into the parking/insertion orbit. **Real-world facts only, from trusted sources.** This
is NOT about our code — it is the ground truth the autopilot is measured against. Scope is strictly
**launch and ascent** (pad → SECO → Dragon separation into the parking orbit); rendezvous, docking and
return are recorded elsewhere.

Confidence tags: **[P]** = confirmed from a cited primary/authoritative source · **[E]** = established,
widely-documented Falcon 9/Dragon figure · **[D]** = design principle / technique, source cited ·
**[~]** = approximate / varies by mission, range given.

Reference flights: **Crew-2** (Falcon 9 B1061‑2 + Dragon *Endeavour*, LC‑39A, 2021‑04‑23) and **Crew‑4**
(2022‑04‑27) — same vehicle class, droneship booster recovery, ISS 51.6° plane.

---

## 1. The vehicle (the physics that sets everything)

### 1.1 Falcon 9 Block 5 — two stages, RP‑1 / LOX both stages
- **First stage:** 9 × Merlin 1D in an octaweb. Per engine **~845–854 kN sea level, ~981 kN vacuum**;
  **liftoff thrust ~7,600 kN** (SL, all nine). **Isp ~282 s SL / ~311 s vac.** **Throttle 70–100%.**
  Restartable — it relights for the entry burn and the landing burn. [E]
- **Second stage:** 1 × **Merlin 1D Vacuum (MVac)**, extended/optimised nozzle, **~934–981 kN vacuum**,
  **Isp ~348 s vac**, throttle ~70–100%, single in-space restart capability. [E]
- Liftoff thrust-to-weight **~1.4–1.6**. [E]

### 1.2 Crew Dragon (the payload)
- ~12–13 t (capsule + trunk) at launch. [E]
- **16 × Draco** thrusters — **~400 N each, Isp ~300 s, NTO/MMH** — do **all** nominal orbital
  maneuvering (phasing, rendezvous, attitude, deorbit). [P — SpaceX Draco specs]
- **8 × SuperDraco** — **~71 kN each (16,000 lbf), Isp ~235 s, NTO/MMH** — **launch escape only**, never
  used on a nominal flight. [P]
- **No payload fairing.** Crew Dragon's own **nose cap** is the ascent aero surface; it is **closed
  during ascent** and opened on orbit to expose the docking adapter and forward Dracos. [E]

### 1.3 Redundancy
- The engine controllers/flight computer are **triple-redundant**; the vehicle continuously monitors
  thousands of parameters and can **auto-abort before liftoff** (standard auto-abort: shut down intact
  before clamp release if any final power-check parameter is out of envelope). [P — CBS/SFN abort coverage]

Sources: [Falcon 9 Block 5 / Merlin specs — Wevolver](https://www.wevolver.com/specs/falcon-9-v12-or-full-thrust-block-5),
[Space Launch Report Falcon 9 data sheet — NASA SMA](https://sma.nasa.gov/LaunchVehicle/assets/spacex-falcon-9-data-sheet.pdf),
[SpaceX Draco/SuperDraco — Crew Dragon Launch Abort System](https://en.wikipedia.org/wiki/Crew_Dragon_Launch_Abort_System).

---

## 2. Countdown, ignition, liftoff (the last minutes)

- **"Load-and-go" crew fuelling:** crew board first; **RP‑1 and sub-cooled LOX load begins ~T‑35 min**,
  after the crew are strapped in and the escape system is armed. [E]
- **Launch Escape System armed ~T‑37 min** — the SuperDracos are live from here through orbit. [P]
- **Dragon to internal power / flight configuration ~T‑5 min**; tanks brought to flight pressure. [E]
- **Engine ignition ~T‑3 s:** the 9 Merlins light and ramp up while hold-down clamps restrain the
  vehicle; the flight computer verifies all nine at full thrust and nominal before **clamp release at
  T‑0**. Any out-of-bounds reading triggers **auto-abort on the pad** (engines shut down, vehicle
  intact). [P]
- **Liftoff at T‑0.** [P]

Sources: [Crew launch weather / abort coverage — Spaceflight Now](https://spaceflightnow.com/2020/05/26/spacex-crew-launch-comes-with-new-weather-constraints-for-downrange-aborts/),
[Falcon 9 auto-abort behaviour — CBS News](https://www.cbsnews.com/news/spacex-falcon-9-rocket-safely-aborts-takeoff-after-engine-ignition-and-shutdown/).

---

## 3. The ascent timeline (canonical, droneship crew mission)

T+ from liftoff. Crew‑2 and Crew‑4 agree to a few seconds; this is the profile to fly to.

| T+ | Event | Notes |
|---|---|---|
| 00:00 | **Liftoff** | TWR ~1.4–1.6 [P] |
| ~00:10–00:12 | **Pitch kick** | small attitude change that starts the gravity turn into the launch azimuth [P] |
| ~00:53–00:55 | **Stage‑1 throttle bucket** | throttle DOWN for max‑Q [P] |
| ~01:02–01:04 | **Max‑Q** | peak dynamic pressure [P] |
| ~01:09–01:11 | **Mach 1** | [P] |
| ~01:15 | **Abort mode 1a → 1b** | [P] |
| **02:36** | **MECO** (main engine cutoff) | ~65–80 km, ~1.7 km/s surface [P] |
| **02:39** | **Stage separation** | ~3 s after MECO [P] |
| **02:47** | **SES‑1** (MVac ignition) | ~8 s after MECO; begins the ~6‑min insertion burn [P] |
| ~07:28 | Booster **entry burn** | 3 engines (recovery, runs during the S2 burn) [P] |
| **08:47–08:48** | **SECO‑1** (2nd‑stage cutoff) | **orbit insertion** [P] |
| ~09:30 | Booster **landing** on droneship | [P] |
| **11:58–11:59** | **Dragon separates** from the 2nd stage | into a stable parking orbit [P] |

Two settled facts: **the second stage performs the orbital insertion, and Dragon separates from it at
~T+12 min** (not earlier); and **the booster lands ~T+9:30, during the S2 burn / coast — both vehicles
are live at once.** [P]

Sources: [Crew‑4 timeline — Spaceflight Now](https://spaceflightnow.com/2022/04/27/crew-4-mission-timeline/),
[Crew‑2 timeline — Spaceflight Now](https://spaceflightnow.com/2021/04/22/crew-2-mission-timeline/),
[Crew‑6 timeline — Spaceflight Now](https://spaceflightnow.com/2023/03/01/crew-6-mission-timeline/),
[Everyday Astronaut — Crew‑2](https://everydayastronaut.com/crew-2/).

---

## 4. Ascent techniques (what produces those times)

1. **Vertical rise** off the pad to clear the tower. [D]
2. **Pitch kick (~T+10 s):** one small attitude change to begin the turn in the launch azimuth. [D]
3. **Gravity-turn at ~zero angle of attack:** after the kick the vehicle holds the nose aligned with
   the **velocity vector**, so **aerodynamic side loads stay near zero** (*load relief*) — this is what
   lets the airframe (and crew) survive the dense atmosphere, and it minimises steering/gravity losses.
   Real vehicles fly a **pitch-programmed** (guided) gravity turn, the guidance following a planned
   pitch-vs-time/velocity profile, not a purely passive turn. [D]
4. **Throttle bucket at max‑Q:** the Merlins are **throttled down through peak dynamic pressure**, then
   ramped back up once through it — limiting aero loads at the worst instant. [P/E]
5. **G‑limit throttle:** as the first stage lightens, it is **throttled down near MECO to cap axial
   acceleration** for the crew (~3–3.5 g). The second stage likewise throttles down near SECO (crew
   reaches ~4 g there). [E]
6. **MECO** on the target staging velocity/energy — for a droneship mission, leaving the reserve the
   booster needs for entry + landing. [D]
7. **Stage separation** (pneumatic pushers), then an **~8 s coast** before **SES‑1** so the MVac plume
   does not hit the booster. [D]
8. **Second stage flies closed-loop guidance** (§6) in one burn to the insertion target, then **SECO**. [D]
9. **Dragon separates** into the parking orbit; nose cap opens on orbit. [E]

Sources: [Gravity turn / load relief — Headed For Space](https://headedforspace.com/gravity-turn/),
[Falcon 9 passing Max‑Q — NASA blog](https://blogs.nasa.gov/spacex/2016/04/08/falcon-9-passing-max-q/),
[Crew launch g / throttle — SFN & analysis](https://sjamthe.github.io/spacex-launch-analysis/).

---

## 5. The key numbers

### 5.1 Aerodynamics / max‑Q
- **Max‑Q ≈ 30–35 kPa**, at **~Mach 1.5**, **~11–13 km altitude**, **~T+60 s**. [E]
- Zero‑AoA gravity turn ⇒ near‑zero aerodynamic **side** loads (the reason for the technique). [D]
- No fairing; the closed nose cap is the aero surface on ascent. [E]

### 5.2 Acceleration (g) on a crew ascent
- Crew feel **~2 g early**, rising to **~3.2 g just before MECO** (first stage), and **~4.1 g near SECO**
  (second stage — highest, because the stage is light and the single MVac is still strong). [P/E]
- Falcon 9 **design flight-limit load factors ~6.0 / 4.0 / 3.5 g** at points in the envelope. [E]
- The crew profile deliberately **throttles down to keep g gentle** rather than flying full thrust. [D]

### 5.3 Speeds & altitudes at the milestones
- **MECO:** ~**65–80 km** altitude, ~**1.7 km/s surface-relative** (~2.3 km/s inertial-class). MVac
  ignites into a **~0.8 TWR** (low-thrust upper stage — cannot hold altitude by pointing up; must fly a
  shallow continuously-turning burn). [P/E]
- **SECO / insertion:** ~**199 km altitude**, ~**7,267 m/s (16,256 mph)** inertial. [P]

### 5.4 The insertion / parking orbit (the goal that ends the S2 burn)
- A **deliberately LOW insertion**, so Dragon phases up to the ISS on its own Dracos. [E]
- **Demo‑2 insertion: ~199 × 365 km, 51.6°.** Typical crew insertions are in the **~190–210 km perigee**
  class, elliptical to a few hundred km apogee. [P — n2yo/NORAD; SFN Demo‑2]
- **Inclination is always 51.6°** (the ISS plane), set by the launch azimuth out of KSC/CCSFS
  (latitude ~28.5°N). [E]
- Dragon reaches the ISS on a **slow multi-orbit phasing (~24 h+)**, not a fast 1‑orbit rendezvous. [P]

Sources: [Max‑Q value — Orbital Radar glossary](https://orbitalradar.com/glossary/max-q),
[Crew launch g — analysis](https://sjamthe.github.io/spacex-launch-analysis/),
[Demo‑2 orbit — NORAD 45623 / n2yo](https://www.n2yo.com/satellite/?s=45623),
[Demo‑2 reaches orbit — NASA](https://blogs.nasa.gov/commercialcrew/2020/05/30/spacex-demo-2-crew-dragon-reaches-orbit-news-conference-at-630-p-m-edt/).

---

## 6. The guidance math (the real algorithms)

### 6.1 First stage — OPEN-LOOP pitch program (gravity turn)
- The first stage flies a **pre-planned pitch-vs-time/velocity profile** (an open-loop program) that
  holds ~zero AoA — the *pitch-programmed gravity turn*. It is not continuously re-solved from terminal
  conditions; the plan is designed pre-flight to give the right staging state and load relief. [D]
- 3‑DOF planar equations of motion (the model this profile is designed against): state = downrange,
  altitude, velocity **v**, flight-path angle **γ**, mass **m**; forces = **thrust − drag − gravity**;
  at zero AoA **gravity provides the turning** (dγ/dt driven by the −g·cosγ/v term). [D]

### 6.2 Second stage — CLOSED-LOOP explicit guidance (PEG / UPFG class)
Real launch vehicles switch to **closed-loop optimal guidance** once out of the high‑q region, where
aerodynamic forces are negligible. Falcon 9's upper stage flies an **explicit-guidance / PEG-class law**
(the Shuttle's *Powered Explicit Guidance*, and the *Unified Powered Flight Guidance* of Brand, Brown &
Higgins — the algorithm RO's PEGAS mod also flies). [D]

**The idea (linear-tangent steering):** under flat‑Earth / uniform‑gravity / no‑atmosphere, the optimal
(minimum-propellant) ascent has an **analytic** solution — the thrust pitch obeys
`tan(β) = A·t + B`, with A, B solved from the terminal constraints. PEG/UPFG wraps this in a
**predictor–corrector** so it works on a round, rotating Earth. [P — Shuttle PEG / Brand‑Brown‑Higgins]

**Target = CONSTRAINTS, not a point:** Earth-centred **inertial** state **R, V**; a target orbital
**plane normal** (from inclination + longitude of ascending node), a **cutoff radius**, a **cutoff
speed**, and a **flight-path angle**; plus per-stage thrust, Isp and mass. [P]

**The cyclic solve (one call ≈ one iteration; converges in 2–3, criterion ΔTgo < ~1%):** [P — PEG docs]
1. **Time-to-go** `Tgo` from the remaining Δv via Tsiolkovsky, with `τ = ve / a₀` (a₀ = current thrust
   accel, ve = Isp·g₀).
2. **Thrust integrals** (scalars over Tgo), standard closed forms:
   `L = ve·ln(1 / (1 − Tgo/τ))` (available Δv), `J = L·τ − ve·Tgo`, `S = L·Tgo − J`,
   `Q = S·τ − ve·Tgo²/2` (and the higher moments P, H).
3. **Steering:** `λ = Vgo/|Vgo|`; a turning rate `λ̇` from `Rgo` and the moments; the commanded thrust
   unit vector is the **linear-tangent** direction `iF = unit(λ·cos + λ̇·sin)`.
4. **Gravity** by **conic state extrapolation** (Kepler-propagate the state over Tgo — no constant-g
   assumption): `Rgrav`, `Vgrav`.
5. **Predict cutoff**, rebuild the desired cutoff state on the target plane at the target radius/speed/
   FPA, and **update `Vgo`**. Near cutoff the FPA constraint is released and the burn flies purely on
   remaining Δv to the target inertial velocity, then commands **SECO**. [P]

### 6.3 Launch azimuth (the plane problem)
- **Inertial azimuth** (clockwise from north) for a target inclination *i* from launch latitude *φ*:
  **`sin(β_inertial) = cos(i) / cos(φ)`**. [P]
- **Ground azimuth correction for Earth's spin:** the pad already moves east at
  **`V_rot = 2π·R·cos(φ) / T_day`** (~465 m/s·cos φ; ~408 m/s at the Cape's ~28.5°N). The heading
  actually flown is the inertial velocity **minus** the pad's eastward velocity, re-taken as a heading:
  `V_east = V_orbit·sin(β_inertial) − V_rot`, `V_north = V_orbit·cos(β_inertial)`,
  **`β_ground = atan2(V_east, V_north)`**. From the Cape into the ISS plane this pulls the heading a few
  degrees. [P]
- **Constraint:** a direct ascent can only reach **`i ≥ |φ|`** (else `cos i / cos φ > 1`, no solution) —
  from the Cape (~28.5°N) the 51.6° ISS plane is reachable (NE azimuth), ascending node. [P]

### 6.4 Ascent Δv budget (order of magnitude)
- Total Δv to LEO ≈ **~9.3–9.4 km/s** = orbital velocity (~7.8 km/s) **+ gravity losses** (~1.5–2 km/s)
  **+ drag losses** (~0.1–0.3 km/s) **+ steering losses**, minus the **Earth-rotation assist** (~0.4 km/s
  eastward from the Cape). The gravity turn exists to **minimise the gravity + steering losses**. [E]

Sources: [Powered Explicit Guidance — OrbiterWiki](https://www.orbiterwiki.org/wiki/Powered_Explicit_Guidance),
[Shuttle PEG — FlightGear wiki](https://wiki.flightgear.org/Shuttle_guidance_-_Ascent_guidance_Powered_Explicit_Guidance_(PEG)),
[UPFG (Brand‑Brown‑Higgins) original — NASA NTRS 19740004402](https://ntrs.nasa.gov/citations/19740004402),
[PEGAS UPFG explainer](https://github.com/Noiredd/PEGAS-MATLAB/blob/master/docs/upfg.md),
[Closed-loop nominal & abort ascent guidance (Dukeman, GaTech PhD)](https://smartech.gatech.edu/bitstream/handle/1853/6820/dukeman_greg_a_200505_phd.pdf),
[Launch Azimuth — OrbiterWiki](https://www.orbiterwiki.org/wiki/Launch_Azimuth).

---

## 7. Launch abort — the safety system (part of launch)

- **Launch Escape System:** **8 SuperDraco** engines (~71 kN each), **armed pre-launch (~T‑37 min)
  through orbit**. **Automatic** trigger — the flight computer fires the SuperDracos the instant any
  monitored parameter goes out of envelope — plus a **manual pull-and-twist handle** between the crew. [P]
- **8 abort modes** (pad + 7 ascent), each with its own splashdown zone: [P]
  - **Pad abort** — SuperDraco push off the pad, splashdown near shore.
  - **1a** (liftoff → ~early first stage) — SuperDraco abort, splashdown **Florida → North Carolina**.
  - **1b** (~T+1:15 → MECO) — splashdown **off Virginia**.
  - **2a** (~T+2:32 → ~T+8:05) — splashdown **Delaware → Canadian Maritimes**.
  - **2b** — pop off the second stage, **SuperDraco retrograde burn** to a point **past Nova Scotia**.
  - **2c** — abort **across the Atlantic to off the west coast of Ireland**.
  - **Abort-to-orbit** (late in flight) — use the abort system / Dracos to reach a **lower-than-planned
    orbit** rather than aborting to splashdown.
- **Downrange Abort Exclusion Zone ("black zone"):** the cold, rough **North Atlantic** mid-ocean region
  is actively avoided; Dragon biases the abort toward **Newfoundland** or on toward **Ireland**. [P]
- **After any SuperDraco abort:** the capsule **coasts to apogee**, **Draco reorients**, then **drogues →
  mains → splashdown** (the same descent as a nominal return). [P]

Sources: [Crew Dragon abort modes & splashdown zones — NASASpaceFlight](https://www.nasaspaceflight.com/2020/05/examining-crew-dragons-launch-abort-modes-and-splashdown-locations/),
[Examining Crew‑1 abort modes — NASASpaceFlight](https://www.nasaspaceflight.com/2020/11/examining-crew-dragon-abort-modes/),
[Crew Dragon abort test — Spaceflight Now](https://spaceflightnow.com/2020/01/23/spacex-releases-preliminary-results-from-crew-dragon-abort-test/),
[Abort system for crew safety — CBS News](https://www.cbsnews.com/news/spacex-nasa-launch-abort-rescue-scenarios/).

---

## 8. One-line summary (the standard to fly to)

Light 9 Merlins (~7,600 kN) → vertical rise → pitch kick (~T+10 s) → **zero‑AoA gravity turn** →
**throttle bucket through max‑Q** (~30–35 kPa, Mach ~1.5, ~T+60 s) → **g‑limit throttle** (~3.2 g) →
**MECO ~T+2:36** (~1.7 km/s surface) → sep → ~8 s coast → **MVac SES‑1** flying **closed-loop PEG** to a
**51.6° / ~200 km‑perigee** target (~4 g near cutoff) → **SECO ~T+8:47** (~7.27 km/s at ~199 km) →
**Dragon sep ~T+12:00** into the parking orbit. The Launch Escape System (8 SuperDraco) is armed
pad‑through‑orbit with automatic + manual triggers. Fly the physics and the clock takes care of itself.

---

## Open items to verify further (honesty log)
- Our UPFG has been cross-checked against the **PEGAS reference**, not line-by-line against the original
  **NASA report (NTRS 19740004402)** — a primary-source verification still owed.
- Exact SpaceX flight-software details (the precise guidance law, the exact throttle/pitch schedules,
  the exact abort-mode T+ boundaries) are **not public**; figures here are from the best available
  authoritative secondary coverage and are tagged accordingly.
</content>
