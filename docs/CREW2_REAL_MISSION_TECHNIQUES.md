# Real Crew-2 mission techniques — research record

Recorded 2026-08-24 from primary/authoritative sources for the FULL-FIDELITY autopilot. Crew-2 =
Falcon 9 B1061-2 + Crew Dragon C206 *Endeavour*, launch 2021-04-23 (LC-39A), crew Kimbrough / McArthur /
Hoshide / Pesquet, docked ISS Harmony forward (IDA-2), returned 2021-11-08 splashdown Gulf of Mexico off
Pensacola. Governing rule: copy this, no deviation ([[crew2-full-fidelity-no-deviation]]). Numbers are
real-world; RSS/RO reproduces the environment, so they are our targets. Sources listed per section.

Legend: **[P]** = number confirmed from the cited source; **[E]** = established Falcon 9/Dragon figure,
widely documented; **[D]** = design principle (technique), source cited.

---

## 1. ASCENT — liftoff to MECO

Real Crew-2 timeline (NASA Commercial Crew blog + Everyday Astronaut Crew-2):
- **Max Q ~T+00:01:02** [P]. Throttle-down through max-Q then back up (classic F9 profile) [E].
- **MECO T+00:02:36**, ~80 km, ~6,000 km/h (~1.7 km/s surface) [P]. (Our loft targets ~67 km / 2300 m/s
  cap — close; the 80 km figure is inertial-ish. Ascent loft already tuned, staged 68.5 km.)
- Liftoff thrust ~7,600 kN, 9× Merlin 1D ~845 kN SL each; ISP ~282 SL / 311 vac [E].
- Technique [D]: vertical rise → pitch kick → **gravity turn at ~zero angle of attack** (fly the velocity
  vector so aero load stays near zero), throttle to hold max-Q and a g-limit, MECO on a target
  velocity/flight-path-angle for the insertion the upper stage finishes.

## 2. STAGING + UPPER STAGE TO ORBIT

- **Stage separation T+00:02:39** (~3 s after MECO) [P].
- **Second stage ignition (SES-1) T+00:02:47** — single Merlin Vacuum (MVac), ~981 kN, ISP ~348 [P/E].
- **SECO-1 T+00:08:47** — Crew Dragon inserted [P].
- **Crew Dragon separation from S2 T+00:11:58** [P].
- Insertion is a LOW parking orbit (~190–210 km class); Dragon then phases UP to the ISS (~420 km) on its
  own Dracos over many orbits [E]. Crew-2 docked **~23 h after launch** [P] (a slow multi-orbit phasing,
  NOT the fast 1-orbit rendezvous).
- Technique [D]: S2 flies a closed-loop guidance (real F9 uses an explicit-guidance/PEG-class law) to hit
  the target insertion state; single burn to SECO-1 for LEO crew missions.

## 3. BOOSTER RECOVERY (droneship) — coast, entry burn, grid fins, landing

Crew-2 B1061-2 landed on the droneship *Of Course I Still Love You* (ASOG/OCISLY), Atlantic downrange.
- **No boostback** for a droneship LEO crew launch — the booster is going downrange and stays downrange.
  It flips to engines-first and coasts to apogee, then falls back [E].
- **Grid fins** (4, titanium, X-arrangement, ~21 ft² each) [P, BGR/spacelaunch]: deploy after sep; steer
  the **hypersonic aerodynamic descent** (Mach 10+) by rotating independently to make lift — roll/pitch/yaw
  during an *aerodynamic translation* toward the landing point [D]. **Cold-gas N2 thrusters** turn the
  stage in near-vacuum where fins have no air [P].
- **Entry burn** (~T+00:07:27 for Crew-2) [P]: relights **3 engines** to shed velocity and protect the
  stage from re-entry heating/loads before it hits the dense air [P/E].
- **Landing burn = hoverslam / suicide burn**: cannot hover (1 Merlin at min throttle > empty-stage
  weight), so it must **hit zero velocity at exactly zero altitude** — 3 engines for the hard brake then
  **1 centre engine** for the controlled touchdown; Merlin throttles to ~40% [P, Blackmore]. Precision
  ~10 m (droneship) from fin steering + a landing-burn divert; guidance is onboard **convex optimization
  (CVXGEN)** — fuel-optimal max-min-max throttle. Full detail + our failure analysis in
  [[falcon-real-hoverslam-technique]].

## 4. RENDEZVOUS — phasing orbit up to the ISS

Crew Dragon executes a NAMED burn sequence (NASASpaceFlight Crew-4/Crew-3 docking coverage):
1. **Phase Burn** (~4 min) — sets the catch-up rate [P].
2. **Boost Burn** (~7 min) — raises the orbit (run overnight while crew sleeps) [P].
3. **Close Burn** (~10 min) — raises the orbit further toward the ISS [P].
4. **Transfer Burn** (~48 s) — puts Dragon on the transfer to the approach corridor [P].
5. **Coelliptic Burn** (~37 s) — establishes a co-elliptic orbit just below/behind the ISS [P].
6. **Approach Initiation (AI) Burn** — begins ~**7.5 km behind and below** the ISS, ~**96 min before
   docking** [P].
- Technique [D]: standard co-elliptic rendezvous (phasing → transfer → coelliptic → AI), i.e. Clohessy-
  Wiltshire / Hohmann-class targeting, NOT a chase. Matches our rule [[falcon-rendezvous-approach-law]].

## 5. PROXIMITY OPS + DOCKING (autonomous)

Approach corridor (NASA + planetary.org + NSF):
- **Approach ellipsoid**: 4 km × 2 km egg-shaped zone around the ISS — entered on initial approach [P].
- **Keep-out sphere (KOS)**: 200 m radius safety zone; inside it Dragon stops at defined waypoints for
  go/no-go checks [P].
- **Waypoints** [P]: **WP0** ≈ 400 m directly below the station (nadir); Dragon then moves onto the
  docking axis to **~220 m** in front; **WP2 = 20 m** from the port. (Demo-1 also showed a retreat test:
  came to 150 m, backed to 180 m, then final approach from 20 m [P].)
- Sensors [P]: nose-cone opens to expose the docking ring; **LIDAR + cameras + thermal** guide fully
  automatic approach; crew monitors and can take over.
- **Soft capture** (initial contact), then **hard capture** ~10 min later via **12 hooks/latches** for the
  airtight seal [P].
- Final approach speed is slow (cm/s class) along the docking axis, holding the corridor [D].

## 6. UNDOCKING + DEPARTURE (autonomous)

Return day (NASA CCP; earth_phasing graphic; NASA Demo-2 return):
- Undock: hooks retract, then **2 very small separation burns** push Dragon off the port [P].
- **4 departure burns** [P]: **Burn 0** (~16 s, just after undock — up and around the station); **Burn 1**
  (~20 s, a few min later — to in-front-of and below); **Burn 2** (~44 s, ~50 min after Burn 1); **Burn 3**
  (~1 min) — leaving Dragon in a stable orbit **~10 km below the ISS** [P].
- **1 departure phasing burn** (~6 min per NASA Demo-2; ~9 min on other refs) — lowers the orbit onto the
  return path [P].
- Technique [D]: reverse of the approach — leave the KOS along a safe corridor, then phase down and away.

## 7. RETURN — deorbit, entry, parachutes, splashdown

Crew-2 return 2021-11-08 (and Demo-2 return figures, same vehicle class):
- **Trunk separation** shortly BEFORE the deorbit burn; trunk burns up [P]. (Draco is on the capsule; the
  trunk is unpowered.)
- **Deorbit burn** ~15–16 min on the Dracos [P] (long, low-thrust) — targets the entry corridor.
- **Entry interface** at orbital velocity ~**17,500 mph** (~7.8 km/s); peak heating shell ~**3,500 °F** [P].
- **Lifting re-entry** [P, GA Tech/Dragon aero]: **12° trim angle of attack, L/D ≈ 0.18**, via a **radially
  offset centre of mass**. Steered by **bank-angle modulation** — roll the lift vector about the velocity
  vector; vertical lift = L·cos(bank). This is Apollo-class bank-angle entry guidance (range control by
  banking, cross-range by bank sign), holding the trim AoA throughout [D].
- **Drogues (2)** deploy ~**18,000 ft**, ~**350 mph** (~156 m/s) [P].
- **Mains (4)** deploy ~**6,000 ft**, ~**119 mph** (~53 m/s) [P].
- **Splashdown** ~15–16 mph under the mains, ocean [E]; Crew-2 splashed off Pensacola at night [P].

## 8. VEHICLE PROPULSION NUMBERS

- **Draco**: NTO/MMH, **400 N (90 lbf)**, ISP **300 s**, **16 on Dragon** — all maneuvering + attitude +
  deorbit [P, SpaceX Draco]. (Matches our RO Dragon MMH+NTO — [[dragon-return-propellant-mmh-nto]].)
- **SuperDraco**: N2O4/MMH, **71 kN (16,000 lbf)**, ISP 235, 8 engines — LAUNCH ABORT ONLY, not used in a
  nominal flight [P]. So a nominal Crew-2 flies entirely on Dracos after S2 sep — our
  [[falcon-real-hoverslam-technique]] rule (Dracos, never SuperDraco, in nominal ops) is correct.

## SOURCES
- NASA Commercial Crew blog (Crew-2 timeline, MECO/sep/SES/entry/SECO/Dragon-sep, undock/deorbit/chutes):
  blogs.nasa.gov/commercialcrew/…, nasa.gov/humans-in-space/top-10-things-to-know-for-nasas-spacex-demo-2-return
- Lars Blackmore, *Autonomous Precision Landing of Space Rockets*, National Academy of Engineering
  (nae.edu / nationalacademies.org/read/23659/chapter/10) — hoverslam + precision.
- NASASpaceFlight Crew-3/Crew-4 docking articles — named rendezvous burns, approach corridor.
- planetary.org "Crew Dragon docks", NASA docking blogs — approach ellipsoid, KOS, waypoints, soft/hard capture.
- Everyday Astronaut Crew-2 page — flight profile timeline.
- Georgia Tech / SpaceX Dragon Re-Entry Vehicle Aerodynamics paper — 12° AoA, L/D 0.18, offset CoM.
- SpaceX Draco / SuperDraco / Dragon 2 specs; BGR / SpaceLaunchLive grid-fin articles.
- NASA earth_phasing graphic (departure/phasing/deorbit labels).
