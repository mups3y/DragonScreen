# Crew-2 Mission Flight Plan — DragonScreen RO/RSS

End-to-end profile for flying SpaceX **Crew-2 (Endeavour)** under DragonScreen's own guidance — no MechJeb,
no kOS — in Realism Overhaul at real 1× scale. Every phase carries the real mission's parameters, the method
and math, the RO constraints it must obey, and an honest **validated-vs-open** status. Published form:
Artifact "Crew-2 Flight Plan". Companion refs: [RO_RSS_ENVIRONMENT.md](RO_RSS_ENVIRONMENT.md),
[RO_TESTFLIGHT_MECHANICS.md](RO_TESTFLIGHT_MECHANICS.md), [RO_MODS_MECHANICS.md](RO_MODS_MECHANICS.md),
[CREW2_RSS_RESEARCH.md](CREW2_RSS_RESEARCH.md).

**Vehicle** Falcon 9 Block 5 · Crew Dragon · **Pad/Inc** LC-39A · 51.6° · **Target** ISS Harmony fwd/IDA-2 ·
**Recovery** droneship, Atlantic.

## Flight-readiness statement
The ENVIRONMENT is understood and documented from the installed configs. The FLIGHT is not yet certified:
ascent reaches orbit and booster recovery is in active debug; the rendezvous is being rebuilt to the real
named-burn sequence; the R-bar/V-bar approach is built but disabled; the return pipeline is built but unflown
in RO. This plan is the target and the gate — a phase is not "done" until its named number is met on a
recorded flight.

**Status key:** GO = validated on a recorded RO flight · CAUTION = built & tested, RO-unvalidated · OPEN = not
yet flown / not to spec.

## Flight environment — the numbers that govern every phase
- **Real Earth** R 6 371 km, μ 3.986e14, sidereal day 86 164 s. LEO ≈ 7.79 km/s; ascent Δv ≈ 9.4 km/s.
- **Atmosphere** SL 101.3 kPa, scale height ~8.5 km, top 140 km. Max-Q ~31 kPa @ 13 km — a throttle ceiling.
- **FAR** stock drag zeroed; lift/drag/wave-drag from voxel geometry. Drag is MEASURED, not modelled.
- **No reaction wheels** attitude = RCS + gimbal only; any unpowered re-orient needs RCS on or nothing turns.
- **RealFuels** ullage settle before every relight; TEATEB + 4 ignitions/mode; failed light not refunded; slow spool.
- **TestFlight + RealHeat** per-ignition reliability (heritage-tuned to proven B1061-2); shock reentry heating.

## Real Crew-2 telemetry — the numbers to match (authoritative)
Launch **2021-04-23 09:49:02 UTC** (05:49:02 EDT), **LC-39A**. Booster **B1061-2** (2nd flight, reused from
Crew-1), landed on droneship **"Of Course I Still Love You"**, Atlantic. Crew Dragon **Endeavour**. Dock
**Harmony forward / IDA-2**. MET below is the **official NASA Crew-2 timeline** (times approximate).

### Ascent (NASA MET — the launch numbers to match)
| MET | Event | Telemetry to match |
|---|---|---|
| **T+0:00:00** | Liftoff | LC-39A; ~549 t stack; 9 Merlin 1D; TWR ~1.4 |
| **T+0:01:02** | **Max Q** | **throttle BUCKET (ease off), ~31 kPa peak, ~13 km, ~Mach 1.5** |
| **T+0:02:36** | **MECO** | **~2.3 km/s surface, ~67 km; g-limited ~4 g** |
| **T+0:02:39** | Stage separation | MECO + 3 s (interstage decoupler) |
| **T+0:02:47** | SES-1 (M-Vac ignites) | MECO + 11 s; ullage settle then light |
| **T+0:07:27** | Booster **entry burn** | 3 engines, ~20 s |
| **T+0:08:47** | **SECO-1** | parking orbit **~190 × 210 km × 51.6°** |
| **T+0:09:03** | Booster **landing burn** | 3→1-engine hoverslam |
| **T+0:09:30** | Booster **landing** | droneship, **~500 km downrange**, Atlantic |
| **T+0:11:58** | Dragon separates from S2 | into the parking orbit |
| **T+0:13:02** | Dragon nosecone open | |

### Orbit → rendezvous → dock (the phase numbers to match)
| MET | Event | Target |
|---|---|---|
| T+~0:50 → ~T+23 h | Phasing / co-elliptic burns | Hohmann-family climb ~190 km → ISS ~419 km |
| **T+23:19:00** | **Docking (contact & capture)** | **Harmony fwd / IDA-2**, closing on V-bar, ~7.66 km/s |
| **T+23:34** | Hooks closed (docking complete) | ~15 min after capture |
| **T+25:26** | Hatch open | leak checks T+23:46 → T+25:11 |

### ISS target orbit — set in the save to the real April-2021 values
- **Altitude ~419 km** (417 × 421 km), **inclination 51.64°**, ecc ~0.0003, period ~92.9 min, ~7.66 km/s.
  Now written into `saves/test/persistent.sfs` (was 420 km / 51.6°). Launch is **on-plane** (RAAN-matched),
  so the rendezvous closes the phase; the ISS RAAN sets *when* the plane window opens.

### Launch window — 2-day warp lead
- The pad crosses the ISS plane (north-going) once per sidereal day. `LaunchWindowOps.PlaneWindowMinLeadS`
  (**2 days**) pushes the window ≥ 2 days out so there is time to warp to it without missing it — same
  on-plane geometry, just a later crossing. Set to 0 to launch on the next crossing.

Sources: [NASA Crew-2 mission timeline (PDF)](https://www.nasa.gov/wp-content/uploads/2021/11/crew-2_timeline_-_april_23_0.pdf) ·
[NASA — Crew Dragon docks day after launch](https://blogs.nasa.gov/crew-2/2021/04/24/crew-dragon-docks-to-station-day-after-launch/) ·
[Spaceflight Now — Crew-2 timeline](https://spaceflightnow.com/2021/04/22/crew-2-mission-timeline/).

## Timeline — phase by phase

### Phase 0 — Countdown & pad start (T-35m→T0) · CAUTION
- **Real** S1 chill T-7:00, M-Vac TEATEB purge T-4:00, pressurize T-2:00, ignition T-3s, hold-downs release AFTER thrust good.
- **Method** ignite clamped, spool ~3s, confirm thrust + no failed light, THEN release. Failed light = pad abort.
- **RO** pad ullage settled (tanks full); onboard TEATEB for relights; TestFlight can fail a light.
- **Status** pad start flies; spool-then-clamp-by-capability still to harden.

### Phase 1 — Ascent to orbit (T0→T+12m) · CAUTION
- **Guidance** vertical rise → gravity turn → MECO capped at real staging velocity → interstage-decoupler sep →
  ullage-settled M-Vac (UPFG owns S2) → SECO at parking orbit → Dragon separates.
- **Real T+** 0:53 throttle bucket · 1:02 Max-Q · **2:36 MECO** (~2.3 km/s srf) · 2:39 sep · 2:47 SES-1 ·
  **8:47 SECO-1** (~190×210 km × 51.6°) · **11:58 Dragon sep**.
- **RO** FAR max-Q is a throttle CEILING; M-Vac needs ullage settle + reliable relight; MECO VELOCITY sets booster downrange.
- **Status** reaches orbit in RO. Open: confirm 51.6°±0.1° and S2 still closes 200 km after slower staging.

### Phase 1R — Booster recovery, droneship (parallel) · OPEN
- **Real** B1061-2 reused, NO boostback: entry burn T+7:27, landing burn T+9:03, touchdown ~T+9:30, ~500 km downrange.
- **Method** flip to retrograde on RCS, coast, 3-engine entry burn, grid-fin+RCS guided descent, 3→1 hoverslam.
- **RO** ullage settle before each relight; ignition reliability; RealHeat entry burn; barge on the true ground track at 500 km (31.906, −77.089).
- **Status** ACTIVE DEBUG. Fixed: staging cap, lean clamp, flip no-ignite, landing ullage settle + ignition lead, TestFlight reliability. Remaining: confirm the landing lights & lands; trim ~62 km residual.

### Phase 2 — Orbit activation (T+12m→T+50m) · CAUTION
- **Real** Dragon sep T+11:58, checkouts, nosecone open T+13:02; parking orbit held.
- **RO** Draco RCS only; finite cold-gas/mono budget; RealAntennas comms.
- **Status** built (stock); validate RCS-only attitude hold + checkout at RSS scale.

### Phase 3 — Phasing burns (T+50m→T+27h) · CAUTION (named-burn built 2026-08-23)
- **Real** Phase burn T+49:23, Transfer T+27:06:46, Co-elliptic T+27:53:12, Out-of-plane T+28:48:56 — Hohmann-family climb ~190 km → ISS ~420 km.
- **Method** REBUILT to the real co-elliptic named-burn profile: **NC** (phasing raise at a computed lead angle) → **NSR** (circularise co-elliptic 15 km below the ISS) → **Ti** (terminal initiation at the 27.5° target elevation angle) → hand to the L-approach. Pure math in `pure/NamedRendezvous.cs` (verified vs `scratchpad/rdv_sim.py`), flown by `NamedRendezvousOps` (RSS-gated); the ad-hoc ladder is retired on Earth.
- **RO** RCS-only Draco burns; orient on RCS before each (no reaction wheels); the long phasing/co-elliptic waits warp themselves and drop out ~2 min short; periapsis floor guards every burn.
- **Status** built + tested headless; **RO-unvalidated in flight.** Open: NPC out-of-plane trim (residual is small when the launch matches the ISS plane) not yet added.

### Phase 4 — Approach initiation (T+29:40) · OPEN
- **Real** AI at range 7.5 km behind & below; midcourse T+30:05; enter Approach Ellipsoid (2000 × 1000 m).
- **Method** OFFSET targeting mandatory; corridor/keep-out breach = auto-abort.
- **Status** set AI range 7.5 km; wire ellipsoid + keep-out to the real geometry.

### Phase 5 — Proximity operations (T+30→T+31m) · CAUTION (enabled 2026-08-23)
- **Real** holds at WP0 400 m below (+R-bar) T+30:25 → WP1 220 m on axis (V-bar) T+30:49 → WP2 20 m T+31:00.
- **Method** an L not a line — LVLH guidance on Draco, arc from below onto the port axis, hold the target-port attitude throughout.
- **Status** WP0/WP1/WP2 + arc built; **now enabled** — the named-burn Ti hands to `WaypointApproachOps` at the approach box. Validate the LVLH holds + station-keeping in RO.

### Phase 6 — Docking & pressurization (T+31→T+32m) · CAUTION
- **Real** contact & capture T+31:10 at IDA-2 Harmony forward; hooks closed T+31:23; hatch T+32:15.
- **RO** Draco trim to contact (no lateral deadband); hold port axis AND roll reference to latch.
- **Status** docking control built; capture-hold RCS corrected for no-reaction-wheels; validate the latch in RO.

### Phase 7 — Return & splashdown (on command) · OPEN
- **Profile** skip the 199-day stay: undock on command → departure burns clear keep-out → phasing → trunk jettison → ~12-min de-orbit aimed at the box → lifting entry shield-forward → drogues ~5 486 m/~156 m/s → mains ~1 830 m/~53 m/s → Gulf splashdown.
- **RO** de-orbit closed-loop on drag-aware predicted impact; RealHeat shield-forward; RealChute figures already real.
- **Status** built, UNVALIDATED in RO. **Capsule-entry drag made FAR-consistent 2026-08-23**: on Earth the predictor drops the stock drag-cube table and flies the vehicle's MEASURED ballistic coefficient (the known override in vacuum before entry), matching how the booster already predicts.

## Risk register
| Item | Phase | Risk | Disposition |
|---|---|---|---|
| Booster landing not closed | 1R | HIGH | active debug; land on the barge |
| Rendezvous named-burn RO-unvalidated | 3 | MED | built + tested; validate NC/NSR/Ti in flight |
| L-approach RO-unvalidated | 5 | MED | enabled; validate LVLH holds |
| Return unflown in RO | 7 | MED | validate accuracy end-to-end (entry now FAR-consistent) |
| NPC out-of-plane trim not added | 3 | LOW | small when launch matches the plane; add if a flight shows residual |
| Avionics/EC/comms budgets unverified | 2–7 | LOW | likely non-blocking; verify |

**Bottom line.** Environment understood; profile matches Crew-2 phase for phase. Path to a certified flight is
the phase sequence above, each gated on its named number on a recorded RO flight.
