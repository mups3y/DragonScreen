# Crew-2 RSS/RO — full mission build status (2026-08-22, autonomous session)

Built to match SpaceX's own 6-step sequence + the real booster/return flight. Everything is RSS-gated
(stock keeps its previous methods) and **UNFLOWN** — nothing downstream can be tested until the ascent
closes orbit. Pure logic is headless-tested where it exists. Event/action names verified against the
Crew Rodan partdump (`quarantine/.../partdump_Ghidorah_9_-_Crew_Rodan.txt`).

## ⛔ THE GATE: the ascent does not yet reach orbit — but the REAL fix is now built
The RO M-Vac (TWR ~0.82 from a 65 km MECO) apexes inside Earth's 140 km atmosphere and re-enters. The
`S2 loft` heuristic (`[Tunable] S2LoftGainDeg/S2MaxLoftDeg`) is the stop-gap. The proper fix is now BUILT:
**`pure/Upfg.cs` + `pure/Kepler.cs`** — the full Brand-Brown-Higgins UPFG (linear-tangent predictor-
corrector, the guidance F9/Shuttle fly), fresh C#, no kOS. HEADLESS-VALIDATED (26 tests): from the exact
measured MECO state it CONVERGES to a 200 km orbit and commands a prograde+loft thrust vector. It replaces
the loft entirely. REMAINING = the thin KSP glue (feed state, steer iF, cut at Tgo≤0) - wire + test
together next. See **docs/ASCENT_GUIDANCE_UPFG.md**. (This is far smaller/safer than porting PSG's ~13k
lines, and it's the algorithm RO users actually fly.)

## Capability event names (from the partdump — use these, never staging)
| part | module | fire this |
|---|---|---|
| S1/S2 interstage, Dragon decoupler, trunk | `ModuleTundraDecoupler` | event/action **`decouple`** |
| S1 & S2 engines (Merlin) | ModuleEnginesRF | **`activate engine`** / `shutdown engine` (we call `.Activate()`) |
| nosecone | shroud | **`open shroud`** / `toggle shroud` (it OPENS and SHUTS — toggle, don't stage) |
| drogue chutes | ModuleParachute | **`deploy chute`** / `cut chute` |
| main chutes | ModuleParachute | **ARMED by default** — auto-deploy + auto-cut drogues; event is `disarm` |
| docking port | ModuleDockingNode | **`undock node`** / `decouple node`; `control from here` |
| landing legs | KRE | `deploy gear` / `toggle` |

## Per-stage build status (SpaceX's 6 steps + booster + return)

### 1. LIFTOFF → orbit  — **ASCENT, the gate**
- DONE: LaunchAzimuth into the 51.6° plane; ForBody (200 km parking, MaxQ 34, PitchRefAltM 40 km);
  SeparateBooster (interstage decouple by capability); IgniteSecondStage (ullage + 4-ignition retry);
  flameout-MECO; **S2 loft** (needs gain tuning). All RSS-gated. Insertion target ~190×205 km real.
- PENDING: reach orbit (loft tuning or PSG). Then confirm insertion 200 km / 51.6°.

### Booster → droneship  (parallel to the S2 burn)  — **BUILT**
- DONE: `Landing.Droneship` flag → **skips boostback** (Crew-2 is ASDS, not RTLS): flip → coast →
  entry burn → landing burn, matching real timing (entry 7:27 / landing burn 9:03 / land 9:30).
  Entry-burn gate now atmosphere-relative (Kerbin 32.5 km → Earth ~65 km, real F9 is ~55-70 km).
  Droneship targets the fixed OCISLY coordinate (KK static, 31.5599 N / 76.680 W). 5 new tests.
- PENDING: needs the ascent to leave S1 recovery propellant (currently stages ~dry). Tune the entry/
  landing gates from the first recovery flight so the clock matches the livestream.

### 2. ORBIT ACTIVATION  — **exists**
- Dragon separates from S2 on the Dragon decoupler (`SeparateSecondStage`, by capability), arms Dracos.
  Nosecone = shroud → open it here (step also needed for docking). Checkouts are cosmetic in KSP.

### 3. PHASING BURNS  — **RSS-wired**
- Phasing auto-adapts to the ISS orbit (reads target period/SMA), so 200→420 km needs no number.
- Periapsis floor made atmosphere-relative (Kerbin 75 km → Earth ~145 km; 75 km is INSIDE Earth's
  atmosphere and would re-enter). `Rendezvous.PeriapsisMarginM`.

### 4. APPROACH INITIATION (7.5 km)  — **spec ready, glue pending**
- Real: AI at 7.5 km behind+below, comm link, final raise burn, into the 2000×1000 m ellipsoid.
  Maps onto the L-approach engage (below).

### 5. PROXIMITY OPERATION (the L-approach)  — **PURE MODULE BUILT + TESTED; glue is the next task**
- `pure/WaypointApproach.cs` (17 tests): WP0 (400 m below, R-bar) → WP1 (220 m ahead, V-bar) →
  WP2 (20 m), each a HOLD with auto/crew GO, inside a 200 m keep-out sphere with a V-bar corridor;
  KOS breach → abort. The published Crew-2 waypoints, the L-geometry, the holds — all in the tested core.
- GLUE PENDING (deliberately not hand-written blind — untestable RCS-frame code): a `WaypointApproachOps`
  that (a) builds the station LVLH frame `up=(station.CoM-body.pos)`, `along=Exclude(up, station.obt_velocity)`,
  `cross=up×along`; (b) projects `ship.CoM-station.CoM` and `ship.obt_velocity-station.obt_velocity` into it
  → RadialM/AlongM/CrossM + rates; (c) runs `WaypointApproach.Guide`/`StepPhase`, tracking HoldElapsedS;
  (d) converts the commanded LVLH velocity to world, corrects `dv=cmdVelWorld-relVel`, and drives it via
  **DockingOps' RCS-translation path** (`AttitudeController.TranslateX/Y` + fore — reuse it, do NOT
  reinvent the vessel-frame transform); (e) hands to DockingOps at `WpPhase.Handover`. RSS-gate the
  StationApproach→approach handoff to pick this over DirectApproach. Verify in-flight at first rendezvous.

### 6. DOCKING & PRESSURIZATION  — **exists**
- `DockingOps`/`DockControl` fly contact & capture at IDA-2, Harmony forward (B10.IDA both sides).
  Pressurize/hatch are cosmetic.

### Refuel while docked  — **exists** (`x_refuelFrac`); user undocks when "refuelled and ready" (no 167-day warp).

### Undock → depart → de-orbit → entry → splashdown  — **exists + RSS-wired; exact SpaceX sequence below**
Real Crew Dragon return (researched 2026-08-22, [NASA earth_phasing.pdf](https://www.nasa.gov/wp-content/uploads/2020/08/earth_phasing.pdf),
[NSF Resilience return](https://www.nasaspaceflight.com/2021/05/dragon-resilience-return-first-operational/)):
1. **Undock** (docking node `undock node`).
2. **4 departure burns + Ground Axial Burn**, autonomous: **16 s** initial sep · **Depart-1 21 s** ·
   **Depart-2 44 s** · **Depart-3 61 s** · **Ground Axial 197 s** (lowers the orbit). ⛔ MISSING in our
   build - we fire ONE solved de-orbit node. A departure-burn sequencer (like the L-approach) is deferred.
3. **Trunk jettison BEFORE the de-orbit burn** (drops the solar array + radiator to cut mass for the burn).
   ⚠ OUR order differs: EntryOps jettisons the trunk at ENTRY in the retrograde attitude
   (`falcon-dragon-two-decouplers`), not pre-de-orbit. To match SpaceX, jettison before the de-orbit burn.
4. **De-orbit burn ~12 min** (Draco), onto the splashdown trajectory. Ours is one solved node → same effect,
   different method. Target now **Pensacola**: `DeorbitOps.SplashdownEarthLatDeg/LonDeg = 29.8 / -87.3`
   ([Tunable]; refine from where entry actually lands). Stock keeps Kerbin Splashdown.
5. **Nosecone CLOSES after the de-orbit burn** (covers the fwd thrusters/dock ring/sensors) - `toggle shroud`.
6. **Entry + chutes**: drogues **5486 m @ ~156 m/s**, mains **1830 m @ ~53 m/s** - already the REAL Dragon
   figures, correct in RSS (`pure/Entry.cs`). Mains auto-arm (deploy drogues, mains follow, drogues auto-cut).
7. **Splashdown**, Gulf of Mexico off Pensacola.

## Research + tooling added this session
- **`docs/ASCENT_GUIDANCE_UPFG.md`** — the real fix for the ascent gate. UPFG/PEG (linear-tangent
  predictor-corrector, the algorithm F9 flies and RO's PEGAS uses) is ~a few hundred lines vs PSG's
  ~13,000; RECOMMENDED over the plan's PSG. Full 9-block spec + implementation plan. Build it if a
  couple of loft tunes don't close orbit.
- **`pure/Crew2Timeline.cs`** (15 tests) — the flown Crew-2 launch clock (MECO T+2:36, SECO T+8:47,
  Dragon sep T+11:58 …). `Current/Next/TimeToNext/SyncErrorS(name, ourMet)` measures our flight against
  the broadcast (+late / -early) — the tuning target for "match the livestream". Ready to wire into a
  display/HUD or the phase-transition log (small glue, not yet done).
- Capability event names verified against the partdump (see the table above).

## What to do first when back
1. Fly the ascent (loft), send the CSV — tune `S2LoftGainDeg`/`S2MaxLoftDeg` until apoapsis climbs past
   140 km and closes a 200 km orbit. THIS unblocks everything else.
2. Then walk booster-recovery → phasing → L-approach(glue) → dock → return, tuning each from a flight.
3. Recorder now logs `a_aoaDeg` + `a_geeForce` on top of heat (`a_maxSkinK`) for the tuning.
