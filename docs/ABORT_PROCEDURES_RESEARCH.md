> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGH (§B12.7)**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-28; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.
> ⚠ **Named contradiction:** it designs the **hand-written** control loop that was deleted 2026-09-01. Part B builds a **pinned, privately-namespaced MechJeb embed + a pure conductor** (§B1–B16 / T15–T22) instead.

# ABORT & EMERGENCY PROCEDURES RESEARCH — the real Crew Dragon, every regime

**Why this doc exists.** In-game proving flight 2026-08-27 (`Crew-2_20260827_030705.csv`): the reshaped S1
guidance flew a clean ascent (80 km / 1894 m/s / fpa 50° at MECO), but the **second stage never ignited**
(thrust 0 through the whole "S2Burn"), so the crew had to abort at ~129 km, ascending, in near-vacuum. Our
abort **fired the SuperDracos and armed the chutes and did nothing else** — no trunk jettison, no Draco
re-orientation, no controlled entry. The capsule **tumbled** (AoA swinging 40–177°), arced to 223 km, fell
back into a **ballistic 13.6 g re-entry**, and the mains — deployed far too low onto a tumbling capsule —
could not hold it; it hit at ~118 m/s. **Dead crew, second abort in a row.**

The user's instruction: research SpaceX/NASA's real Crew Dragon abort procedures for **every** emergency
regime and record the reliable findings here. The real system has a **different, correct procedure for every
point in the mission**; ours has one. This doc is the reference for rebuilding `AbortResponder` +
`ReturnControl`'s abort/entry path to match. Numbers are from the sources listed at the bottom (NASA
Commercial Crew primary where possible; the 2015 Pad Abort Test and 2020 In-Flight Abort Test are the two
real validation flights).

> ⚠ Ground-truth rule still applies: these are the REAL-vehicle numbers/technique. Our RSS/RO vehicle's
> exact SuperDraco Δv, trunk-sep behaviour and RealChute deploy altitudes are read live / tuned from the
> recording. Use these as the *technique and the target*, then calibrate to our craft.

---

## 1. The Launch Escape System (LES) — what does the escaping

- **8 SuperDraco engines**, **~15,000–16,000 lbf each (~71 kN)**, **~120,000 lbf combined (~534 kN)**,
  hypergolic **MMH/NTO** (same propellant as the Dracos → shared tanks). Mounted in **4 pods ("nacelles")
  of 2**, integrated into the capsule wall (a **pusher** LAS, not a tractor tower).
- **Pusher advantage:** no tower to jettison → **continuous abort capability from ~40 min before liftoff all
  the way through orbital insertion.** There is **no ascent black-zone** (the Shuttle had regimes with no
  safe abort; Dragon does not). This is the single most important architectural fact.
- **Redundancy:** the abort closes with **only one engine per pod firing** (tolerates one-engine-out per
  pod). The 16 **Draco** thrusters (~90 N / ~400 N-class each in reality; ours are MMH/NTO too) do attitude
  + translation after the SuperDraco burn.
- **Burn:** the SuperDracos fire **to propellant depletion (~6 s on the pad case)**, full thrust, no
  throttling in the escape (SuperDracos *can* throttle 20–100 %, but an escape is max-thrust-to-depletion).
- **Trigger, two ways:**
  1. **Autonomous** — the Dragon flight computer continuously monitors the vehicle + Falcon 9 and will
     **instantly fire the SuperDracos on a detected major malfunction** (loss of thrust, loss of control,
     structural/pressure/attitude limits, F9 telemetry loss). No crew action needed.
  2. **Crew-manual** — the crew can command an abort from the cabin (our EJECT handle). Same sequence.

---

## 2. THE UNIVERSAL ABORT SEQUENCE — the ordered procedure (this is what we're missing)

Every abort, pad → orbit, runs the **same ordered sequence**; only the *energy* and therefore the *timing/
altitudes* change. This is the spine to rebuild ours around:

1. **ABORT TRIGGERED** (autonomous or crew) → **shut down / separate from Falcon 9** and **ignite the 8
   SuperDracos at full thrust.**
2. **SUPERDRACO ESCAPE BURN** — push the capsule away from the stack until propellant depletion (~6 s pad
   case; a few s in flight). Provides the separation Δv and the initial climb.
3. **SUPERDRACO CUTOFF → COAST** begins.
4. **⭐ TRUNK JETTISON.** The unpressurized trunk is **separated and discarded** (pad-abort case: at
   **~T+21 s**, shortly after burnout). *This is mandatory and we never do it.* The trunk must go so the
   heat shield is clear and the capsule's aero is stable.
5. **⭐ DRACO RE-ORIENTATION / STABILIZATION.** The 16 Dracos **reorient the capsule to a stable, heat-
   shield-forward (entry) attitude** and null the tumble **before any parachute is deployed.** In the pad
   case the capsule enters a slow, stable rotation heat-shield-down; in a high-energy/exo abort the Dracos
   must hold the shield into the direction of travel through re-entry. *We never do this either — hence the
   tumble.*
6. **COAST / CONTROLLED BALLISTIC ENTRY.** The capsule coasts to apogee and (if high-energy) re-enters
   **shield-forward under Draco attitude control**. Attitude determination in a real emergency entry can
   fall back to a **3-axis accelerometer** solution (heat-shield-forward is aerodynamically self-stable once
   the trunk is gone and the CoM is offset).
7. **DROGUE PARACHUTES (2)** — deployed **once stable and slowed**, to further stabilize + decelerate:
   - High-energy abort (IFA): **~6 mi / 9.6 km** (nominal drogue altitude is ~**18,000 ft / 5.5 km**;
     the IFA deployed higher because of the profile).
   - Low-energy abort (pad): **sequence-timed** — a **4–6 s window *after trunk separation*** (there is no
     altitude budget; apogee is only ~1.5 km).
8. **MAIN PARACHUTES (4)** — deployed **once the drogues have stabilized the vehicle**:
   - **~6,500 ft / ~1.6–2.0 km** altitude in the high-energy case;
   - immediately after drogue stabilization in the low-energy case.
   - **Chute redundancy: the capsule lands safely on 3 of 4 mains (possibly 2), and 1 of 2 drogues.**
9. **SPLASHDOWN** in water at ~**5–8 m/s** (nominal) at the nearest viable abort recovery zone.

**Pad Abort Test (2015-05-06) — the low-energy reference numbers:** 8 SuperDracos, ~6 s burn, coast ~15 s to
**apogee ~1,500 m** above the pad; **trunk jettison + heat-shield-down rotation at T+21 s**; **drogues in a
4–6 s window after trunk sep**, then **3 mains**; **splashdown ~2,200 m downrange, ~107 s after abort.**

**In-Flight Abort Test (2020-01-19) — the high-energy/max-Q reference numbers:** abort at **T+~84–90 s**,
**~19 km, ~Mach 1.8** (near max-Q), F9 engines cut to simulate failure → SuperDracos to **~Mach 2.2**;
**capsule separated, trunk discarded**, **apogee ~40–42 km (131,000 ft)**; **drogues at ~9.6 km (6 mi),
mains at ~1.6 km (1 mi)**; **splashdown ~10 min after liftoff** in the Atlantic. F9 broke up under max-Q
aero seconds after separation (expected).

---

## 3. THE ABORT MODES — a different procedure for every regime

The abort system has, formally, a **pad abort + seven in-flight abort modes**, each mapped to one of **~50
pre-surveyed abort splashdown locations** monitored for sea-state all the way along the ground track. The
regimes, by increasing energy/time:

| # | Regime | When | What the vehicle does | Splashes down |
|---|--------|------|----------------------|---------------|
| 0 | **Pad abort** | On pad / T-0 | SuperDraco to depletion → short climb (~1.5 km) → trunk jettison T+21 s → drogues (timed) → mains → splash | Atlantic a few mi E of the Cape (Det-3 Patrick AFB recovers, <200 nm) |
| 1a | **Low-altitude ascent** | Early Stage 1, low & slow | SuperDraco escape → trunk jettison → Draco stabilize → drogues+mains (sequence-timed, low apogee) | Just offshore / early downrange |
| 1b | **High-altitude / max-Q ascent** | ~Stage 1, transonic–supersonic (the IFA case) | SuperDraco escape through max-Q → sep → trunk jettison → Draco reorient shield-forward → coast to ~40 km → **controlled ballistic re-entry** → drogues ~9.6 km → mains ~1.6 km | Downrange Atlantic |
| 2a–2d | **Stage 2 ascent aborts** | After MECO/stage sep, second-stage burn | SuperDraco push-away (little/no atmosphere) → trunk jettison → Draco reorient → **long coast + controlled shield-forward re-entry from high altitude/energy** → drogues → mains | Downrange Atlantic toward Newfoundland / Ireland |
| 2e | **Abort-to-Orbit (ATO)** | Very late ascent, minor S2 underperformance | **Do NOT escape-and-splash.** Separate from S2 and **use the Dracos to complete the insertion** — reach the planned orbit, or settle for a **lower-but-safe orbit** and continue/curtail the mission | N/A — stays in orbit |

**The downrange geometry (operationally critical):**
- Abort splashdown coverage runs **Cape Canaveral → up the US east coast → Newfoundland → across the North
  Atlantic → Ireland.** ~50 monitored points, one per abort mode/segment.
- There is a **"downrange abort exclusion zone"** mid-North-Atlantic (rough seas, cold water). The flight
  computer is **programmed to avoid landing there** — it biases the abort **back toward Newfoundland
  (within ~200 nm)** or **forward toward the Irish coast**, never the exclusion zone.
- On a free-flight abort the capsule can **use Draco orbit-lowering/targeting maneuvers to line up its
  ground track on a chosen primary recovery site**, and **re-target an alternate site** if weather/sea-state
  at the primary goes out of limits.
- Recovery forces stage to the mode: <200 nm from the Cape → Patrick AFB (HC-130 + 2× HH-60 Pave Hawk, 6+
  pararescue, 5-min alert); farther → C-17 from Charleston with pararescue; ships/aircraft along the track.

**Key takeaway for us:** the abort is **not** "fire SuperDracos, wait for the ground." It is **mode-select
by current state (altitude, speed, whether past MECO, whether near/at orbit)** → run the matching procedure.
Below ~ the atmosphere it's escape+entry+chutes; very late it's **abort-to-orbit**; in orbit it's a **safe-
orbit / targeted deorbit**, never an escape burn.

---

## 4. ON-ORBIT & FREE-FLIGHT ABORTS (post-insertion, not docked)

Once inserted, an "abort" is **not** a SuperDraco escape — it is a **controlled return**:
- **Safe the vehicle**, hold attitude, assess.
- If return is required: **orbit-lowering + phasing on the Dracos to line the ground track onto a recovery
  zone**, **trunk jettison → deorbit burn → shield-forward entry → drogues → mains → splash** — i.e. the
  normal return sequence, just executed **as fast as the geometry allows** to the **nearest** viable zone
  rather than the nominal one.
- The **SuperDracos are held in reserve** as the contingency for a prox-ops/departure emergency (retreat),
  not used for an orbital "abort."

---

## 5. RENDEZVOUS / PROXIMITY-OPS & DOCKING ABORTS (near the ISS)

- **Approach abort / waveoff / retreat:** any breach of the **Keep-Out Sphere (KOS, ~200 m)** or approach-
  corridor, loss of nav/relative-state, or a station call → **stop closing and RETREAT**: a passive/active
  departure burn **away** from the station along a safe relative trajectory (the CW "free-drift passive
  abort" that naturally opens the range), then hold at a safe standoff. **An orbital prox-ops abort is a
  RETREAT, never a launch escape** ([[falcon-rendezvous-approach-law]]).
- **Go-around:** if the approach is off but safe, back out to a hold point and re-attempt.
- The autonomous FSW enforces the corridor + closing-rate limits and will command the retreat itself if the
  crew doesn't (matches our L5 `Fdir` keep-out-breach → `AbortResponder.KosRetreat`).

---

## 6. DOCKED EMERGENCIES — Dragon as ISS lifeboat

A docked Crew Dragon is the crew's **lifeboat** (each ISS crew has a seat in a docked vehicle). The
emergency classes and responses:
- **Cabin depressurization, fire, toxic release/ammonia, structural damage, medical emergency** → the crew
  **safe the station, don suits, board Dragon, seal the hatch, and emergency-undock.**
- **Safe haven:** during risky station ops (or a conjunction/debris threat) the crew **shelters inside the
  docked Dragon** ready to depart at once.
- **Emergency undock + return:** the vehicle undocks and **waits for the orbital geometry** that lines the
  ground track onto a viable splashdown zone (ISS orbit ~90 min → a return opportunity is not always
  immediate), then runs the **deorbit → entry → chute** return to the fastest safe landing. A **contingency
  deorbit** is faster/steeper than the leisurely nominal.
- Real precedent: Crew-11 (Jan 2026) executed an *early but controlled* return for an on-orbit medical
  issue — "controlled and planned," not an emergency deorbit. The distinction matters: **prefer a controlled
  expedited return over a ballistic emergency entry whenever the vehicle is healthy.**

---

## 7. ENTRY & DESCENT CONTINGENCIES

- **Trunk MUST be jettisoned before entry** (clears the heat shield, fixes the aero/CoM) — in every entry,
  nominal or abort. *(Our abort skips this — the #1 bug.)*
- **Attitude:** hold **heat-shield-forward**; the offset CoM makes shield-forward the stable trim once the
  trunk is gone, and the Dracos damp the rest. Emergency attitude can be derived from a 3-axis accelerometer
  if nav is degraded.
- **Chute redundancy:** **2 drogues** (deploy ~18k ft / 5.5 km nominal) → **4 mains** (~6,500 ft / ~2 km).
  Safe on **3 of 4 mains** (possibly 2) and **1 of 2 drogues.** Don't cut a chute you still need.
- **Water landing** at ~5–8 m/s; recovery forces pre-positioned by the abort mode.

---

## 8. GAP ANALYSIS — our abort vs the real procedure (the fix list)

Mapping the above onto our code (`pure/AbortResponder.cs`, `FlightDriver.UpdateAbort`, `ReturnControl`
entry/chutes, `Actuator`). **Diagnose-before-touch; this section is the spec, not yet the change.**

1. **⛔ NO TRUNK JETTISON on abort.** The real sequence jettisons the trunk right after the escape burn; ours
   never does. → On abort, after SuperDraco cutoff, `Actuator.FireDecoupler(trunk)` (the trunk decoupler is
   already identified in the craft dump — the same one `DeorbitGuidance` uses).
2. **⛔ NO DRACO RE-ORIENTATION / STABILIZATION.** Ours fires SuperDracos and immediately goes to chutes; the
   capsule tumbles (CSV AoA 40–177°). → After trunk jettison, run the **AttitudePilot holding heat-shield-
   forward (retrograde / entry attitude)** on the Dracos through the coast and entry, *before* and *during*
   chute deploy. Open the nose shroud first ([[dragon-nose-cone-rcs]]) so the Dracos aren't obstructed.
3. **⛔ CHUTES DEPLOY FAR TOO LOW + regime-blind.** Ours deployed mains at ~5 km onto a tumbling capsule that
   then re-accelerated to 118 m/s. → Deploy **drogues ~5.5 km (nominal) and mains ~2 km** in the high-energy
   case (altitude-gated), and only **sequence-time** them (trunk-sep → drogues → mains) in the low-energy
   pad case. Never deploy mains onto an unstabilized capsule.
4. **⛔ NO REGIME SELECTION.** Ours runs one procedure. → `AbortResponder` must pick the mode from live state:
   **exo/high-energy** (coast, reorient, controlled entry, then chutes) vs **low-energy/pad** (short climb,
   quick sequence-timed chutes) vs **very-late-ascent → ABORT-TO-ORBIT** (don't escape; Dracos to a safe
   orbit) vs **on-orbit → targeted deorbit return**. This is exactly the phase-map in §3.
5. **⛔ NO ABORT-TO-ORBIT.** A late-ascent minor underperformance should **not** splash the crew — it should
   fly to orbit on the Dracos. Add the 2e mode.
6. **↪ Recovery targeting (later).** The real vehicle lines its ground track onto a monitored zone and avoids
   the exclusion zone. For us this is the entry footprint targeting we already have (`EntrySteering` +
   `Trajectory` predictor) pointed at the **nearest** safe splashdown target rather than the nominal.

**Separate ascent bug found the same flight (NOT abort — record in the flight post-mortem):** the **MVac /
second stage produced zero thrust** through the entire S2Burn phase (`thrust_n = 0`, mass frozen, ballistic
deceleration). The S1 fix worked; S2 ignition is the next ascent root-cause to chase (ullage / MVac
detection / `Actuator.IgniteSecondStage`). This is why the abort was needed at all.

---

## 9. Sources

Primary (NASA) preferred; the 2015 Pad Abort Test and 2020 In-Flight Abort Test are the two real validation
flights; NASASpaceFlight/AmericaSpace/SpaceflightNow/CBS used for the mode-map + recovery-zone operational
detail. (Reliable secondary; primary numbers cross-checked against NASA where available.)

- [NASA Commercial Crew — SpaceX Demonstrates Astronaut Escape System (Pad Abort)](https://www.nasa.gov/news-release/spacex-demonstrates-astronaut-escape-system-for-crew-dragon-spacecraft/)
- [NASA Commercial Crew blog — Crew Dragon's Launch Escape System is Armed](https://blogs.nasa.gov/commercialcrew/2020/05/27/crew-dragons-launch-escape-system-is-armed)
- [NASA — SpaceX Crew Rescue and Recovery](https://www.nasa.gov/humans-in-space/nasas-spacex-crew-rescue-and-recovery/)
- [AmericaSpace — Pad Abort Test flight report (2015-05-06)](https://www.americaspace.com/2015/05/06/spacex-successfully-completes-rapid-pad-abort-test-from-cape-canaveral/)
- [AmericaSpace — In-Flight Abort Test flight report (2020-01-19)](https://www.americaspace.com/2020/01/19/spacex-flies-in-flight-abort-test-for-nasa-paves-way-to-crewed-flights-this-year/)
- [SpaceflightNow — Pad abort test preview (sequence detail)](https://spaceflightnow.com/2015/05/05/spacex-dragon-set-for-pad-abort-test/)
- [CBS News — Crew Dragon abort/rescue scenarios (modes, exclusion zone, recovery assets)](https://www.cbsnews.com/news/spacex-nasa-launch-abort-rescue-scenarios/)
- [NASASpaceFlight — Examining Crew Dragon's launch abort modes and splashdown locations](https://www.nasaspaceflight.com/2020/05/examining-crew-dragons-launch-abort-modes-and-splashdown-locations/)
- [NASASpaceFlight — Examining Crew-1 launch weather criteria and abort modes](https://www.nasaspaceflight.com/2020/11/examining-crew-dragon-abort-modes/)
- [SpaceNews — SpaceX to test Crew Dragon launch abort system (modes overview)](https://spacenews.com/spacex-to-test-crew-dragon-launch-abort-system/)
- [Everyday Astronaut — Crew Dragon In-Flight Abort Test](https://everydayastronaut.com/falcon-9-block-5-crew-dragon-in-flight-abort-test/)
- [NASA blog — SpaceX Completes Crew Dragon Static Fire Tests (SuperDraco specs)](https://blogs.nasa.gov/commercialcrew/2019/11/13/spacex-completes-crew-dragon-static-fire-tests)

---

## §G — G-LOADS PER PHASE + WHAT ACTUALLY TRIGGERS AN ABORT (researched 2026-08-27)

**Why this section exists:** our g-abort was a single fixed threshold with a hair-trigger window, so a
normal separation/staging spike (a brief >4 g jolt) false-aborted a good flight, while a real overload
could slip a too-short window. Real Crew Dragon does neither — its g-loads are phase-specific and it
aborts on *detected anomalies*, not on raw g. The corrected design mirrors that.

### Nominal g-loads by phase (primary/press sources)
| Phase | Nominal felt g | Notes |
|---|---|---|
| S1 ascent | **~3.2–3.3 g** peak just before MECO | drops to ~0 g at staging |
| S2 ascent | climbs to **~4.5 g** by SECO | astronaut accounts: "about four and a half Gs" |
| Coast / on-orbit | **~0 g** | any sustained high g is anomalous |
| SuperDraco abort escape | **~3.3 g** peak (IFA test, measured) | designed survivable, not a max-g slam |
| Lifting re-entry (nominal) | **~4 g** | Crew Dragon flies a lifting entry |
| Ballistic / contingency entry | **4–8 g sustained** | steeper = higher; Soyuz ballistic ~6–8 g reference |
| Drogues / mains / splashdown | brief jolts | chute-open + water impact spikes |

### What triggers a real abort (NOT raw g)
The flight computer autonomously commands the abort on a **detected malfunction** — loss of booster
thrust, loss of control (attitude/rate divergence), structural/aero limits exceeded, or loss of the
vehicle. SpaceX's own tests fire the abort at **max-Q** to prove the worst case. Normal g events —
staging, separation, chute deploy, re-entry deceleration — are **expected** and never trigger an abort.

### Our design (matches the above)
1. **Primary triggers = anomalies, not g:** loss-of-control (AoA runaway) + structural-Q abort live in
   `AscentControl`; thrust-shortfall / loss-of-control in FDIR. These are the real abort conditions.
2. **G-abort = a structural BACKSTOP, phase-aware** (`FlightDriver.StructuralAbortLimitG`):
   - Ascent / coast / prox-ops: `StructuralAbortG` = **6.0 g** (above the ~4.5 g nominal ceiling, with margin).
   - Re-entry & descent (Entry/Drogues/Mains/Splashdown/Landed): **DISABLED** — 4–8 g is nominal there and
     the crew is already coming home; an abort on entry g is meaningless.
   - An **active abort** is handled before the check, so the SuperDraco/entry g of an abort never reaches it.
3. **Wide window** (`StructuralAbortDwellS` = **0.5 s**): a real break-up holds high g; a separation /
   staging / chute jolt is a sub-half-second spike and must not abort a good flight.
4. **Operational throttle g-caps** (the bucket that HOLDS nominal g): S1 **3.5 g**, S2 **4.5 g** — matching
   the real profile.
5. **Contingency SuperDraco deorbit** (medical-emergency return): g-limited to **`DeorbitGLimit` 3.5 g** and
   stopped at the safe entry-corridor Pe — fast burn, survivable trajectory, not a full-thrust dive.

Sources: [Spaceflight Now — astronauts on Falcon 9 g-loads](https://spaceflightnow.com/2020/06/12/astronauts-say-riding-falcon-9-rocket-was-totally-different-from-the-space-shuttle/) ·
[Spaceflight Now — IFA preliminary results (~3.3 g peak abort)](https://spaceflightnow.com/2020/01/23/spacex-releases-preliminary-results-from-crew-dragon-abort-test/) ·
[NASA — SpaceX demonstrates astronaut escape system](https://www.nasa.gov/news-release/spacex-demonstrates-astronaut-escape-system-for-crew-dragon-spacecraft-2/) ·
[Spaceflight Now — Dragon astronauts describe return g-loads](https://spaceflightnow.com/2020/08/04/dragon-astronauts-describe-sounds-and-sensations-of-returning-to-earth/)
