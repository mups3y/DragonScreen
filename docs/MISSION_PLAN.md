# The whole mission, end to end

Written 2026-08-11 because I kept porting fragments of the booster descent and calling it progress.
This is every phase of a Crew Dragon flight, the real procedure, the F9I function that already flies
it, and our honest status. **Nothing gets written until its row here says what it is porting.**

F9I is **7,233 lines across four files** and about 110 functions. It flies this whole mission today.

| file | lines | covers |
|---|---|---|
| `F9I/F9_payload.ks` | 862 | ascent, MECO, S2 separation, circularisation |
| `SPACEX/BOOSTER.ks` | 971 | the entire booster recovery |
| `F9I/station_ops.ks` | 2835 | launch window, rendezvous, docking, refuel, undock |
| `F9I/dragon_deorbit.ks` | 2565 | plane match, deorbit, entry, terminal descent |

Status key: **DONE** ported and flown · **PORTED** ported, unflown · **PART** some of it ·
**NO** not started

---

## 1 · Pre-launch and the launch window

**Real procedure.** Crew Dragon launches into the ISS's *plane*, and the launch time is set by when
the pad rotates under that plane — an instantaneous window. Phasing to the target is then handled by
the orbit, not by waiting on the ground.

**⚠ AND F9I DOES THE OPPOSITE, ON PURPOSE.** `falcon-station-ferry`: the Space X Station sits at
**inclination 0.133°**, so the plane window is *degenerate at the equator* — every moment is in
plane. F9I therefore launches on **PHASE ANGLE**, not plane. Copying the real procedure here would be
copying the wrong thing.

| step | F9I | ours |
|---|---|---|
| find the station | `StFindStation`, `StLockStation` | **PORTED** `StationApproach.Find` |
| phase angle now, and the lead required | `StPhaseAtLaunch`, `StRequiredLead` | **PORTED** `pure/LaunchWindow.cs` |
| hold until the phase is right | `StLaunchPhaseWait` | **PORTED** `LaunchWindow.SecondsToWindow` |
| pick the landing zone for the profile | `StLandingZone`, `StLandingZoneName` | **DONE** `LandingSites` |

---

## 2 · Ascent to MECO

**Real procedure.** Vertical rise, pitch/roll programme onto the launch azimuth, throttle-bucket
through max-Q, then MECO sized so the booster keeps the propellant its recovery needs.

| step | F9I | ours |
|---|---|---|
| liftoff, clear the pad | `Liftoff` | **DONE** `VerticalRise` |
| gravity turn, Q limit | `Ascent`, per-profile constants | **DONE** `Ascent.TurnPitch`, `QThrottle` |
| MECO at the profile's apoapsis | `Ascent` → `MECO` | **DONE** 60 km RTLS |

---

## 3 · Separation

| step | F9I | ours |
|---|---|---|
| throttle 0, engines RAMPED down | `MECO` → `EngSpl(0)` | **PART** we cut, F9I ramps over 0.75 s |
| RCS on, hold separation attitude | `MECO` | **PORTED** |
| `wait 2.5`, then stage | `MECO` | **PORTED** |
| booster: 2 s dead time for the vessel split | `WaitForSep` closing `wait 2` | **PORTED** |
| upper stage: `wait 3`, then MVac | `MECO` | **PORTED** |

---

## 4 · Booster recovery

**Real procedure.** Flip, boostback burn, unpowered coast, entry burn through the thick air, guided
descent on grid fins, single-engine hoverslam. Falcon 9 cannot hover — minimum thrust on one Merlin
exceeds landed weight — so it must reach zero velocity and zero altitude simultaneously.

| step | F9I | ours |
|---|---|---|
| stepped 180° turnaround, 3 engines selected first | `Flip1(180, 0.333)` | **PORTED** |
| kill downrange velocity, flat retrograde | `Boostback` first half | **PORTED** |
| aim at the pad, taper to −2.7 km overshoot | `Boostback` second half | **PORTED** |
| hold nose UP through the arc until −50 m/s | `AtmGNC:665` | **PORTED** |
| fins out ON the −50 m/s transition | `AtmGNC:667` | **PORTED** gate is now the descent, not a phase |
| continuous lean law from here to touchdown | `LandingZoneGuidance` | **PORTED** and it steers the PREDICTED IMPACT, not our position |
| AoA ceiling 15° high, then `alt/100` below 4 km | `AtmGNC:754` | **PORTED**, plus −0.25° after handover |
| entry burn: gate 32.5 km, soft start, cut at −300 m/s | `AtmGNC:707-730` | **PORTED** |
| ignition point on VERTICAL speed, 3-engine thrust, +31 m | `LandBurnVars`, `AtmGNC:757` | **PORTED** |
| landing burn = `StopDist/TrueRadar` + margin | `Land` | **PORTED** |
| 3→1 engine handover: −40 m/s AND `OneEngStopDist×1.35 < TrueRadar` | `Land:805` | **PORTED** |
| flare +34% inside 25 m, legs at 200 m | `Land` | **PORTED**, plus RCS off for the burn |

**All of §4 is now ported and audited line by line.** The two that mattered most, both found only by
reading the source rather than my summary of it: the ignition point solved on *surface* speed against
*one* engine's thrust instead of vertical speed against three, and the descent lean steering on our
CURRENT offset from the pad instead of the PREDICTED IMPACT POINT. Unflown.

---

## 5 · Upper stage to orbit

| step | F9I | ours |
|---|---|---|
| MVac burn to apoapsis, fairing/S2 gates | `BurnToApoapsis` | **DONE** |
| drop S2 at a 40 km periapsis so it re-enters | `FalconSepS2` | **PORTED** |
| circularise on the pod's SuperDracos | `FalconCircularize`, `FalconCircBurnVecNow` | **PORTED** |

---

## 6 · Rendezvous

**Real procedure.** Phasing orbit below and behind, height adjust, co-elliptic, then a
Clohessy–Wiltshire terminal approach along the R-bar or V-bar with hold points. Dragon 2 does this
autonomously with relative GPS and lidar.

**`falcon-rendezvous-approach-law`: NEVER chase a co-orbital target — pursuit steering de-orbited
flight 012.** Phasing above 3 km, CW from 0.5–3 km, RCS below, periapsis floor on every burn.

| step | F9I | ours |
|---|---|---|
| match the station's orbit | `StMatchStationOrbit` | **PORTED** `OrbitMatch`, first leg of the ladder |
| phasing leg | `StPhaseLeg`, `StAlongTrack` | **PORTED** `pure/Phasing.cs`, wired through the node executor |
| Clohessy–Wiltshire solve and leg | `StCwSolve`, `StCwLeg` | **PORTED** `pure/CwTargeting.cs`, wired with a periapsis floor — and now reachable ONLY from outside the 10 km gate, which is the regime it was designed for |
| **direct approach inside 10 km** | `StDirectApproach`, `StDirectDv` | **PORTED** `pure/DirectApproach.cs` + `src/DirectApproachOps.cs`. F9I's normal path and ours: the launch window puts arrivals within a few km, and flight 029 spent 1936 s flying CW legs across 2 km it was already co-moving with |
| terminal approach, closest port | `StTerminal`, `StClosestPort`, `StCloseIn` | **PORTED** ladder and port selection both |
| node execution | `StExecNode`, `StBurnNode`, `StVisViva` | **PORTED** `src/NodeExecutor.cs`, with the periapsis floor inside it |
| speed cap by range | `StSpeedCap` | **PORTED** `Approach.SpeedCap`, all four bands |

---

## 7 · Docking, refuel, undock

| step | F9I | ours |
|---|---|---|
| dock | `StRendezvousAndDock`, `StClosestPort` | **PORTED** `src/DockingOps.cs` - gate, hull skirt, axial run, flying the `pure/DockControl.cs` velocity servo (braking curve + authority mixing), not the bang-bang that stood there |
| top up propellant before release | `StTopUpBeforeUndock` | **PORTED** `src/Refuel.cs` actually moves it, reading the CAPSULE's tank through `DockedSide` - the merged-vessel read is a live bug in F9I and the reason our top-up could never see progress |
| close the docking shroud | `StCloseDockingShroud` | **PORTED** |
| undock and back away | `StUndock`, `StBackAway` | **PORTED** our port only, sign calibrated, burst-then-coast |
| is a return trajectory available | `StReturnAllowed`, `StMonoForDeorbit` | **PORTED** and now WIRED - DeorbitOps refuses on it and reports the budget |

---

## 8 · Return, entry, splashdown

**Real procedure.** Phase to put the landing site under the orbit, trunk jettison **before** the
deorbit burn, Draco deorbit, entry interface, a **lifting entry flown on bank angle** to control
range, drogues then mains, splashdown.

| step | F9I | ours |
|---|---|---|
| phase into the deorbit orbit | `StPhaseToDeorbitOrbit` | **PORTED** `pure/DeorbitOrbit.cs` + `src/PhaseDownOps.cs` - two Hohmann half-burns onto 85.1 x 79.2, engines proved lit first, skippable |
| find an overflight of the site | `DgFindOverflight`, `DgSiteInertialAt`, `DgLandLag`, `DgTrackMissAt` | **PORTED** `pure/Overflight.cs` - coarse/refine sweep, site evaluated at TOUCHDOWN not overflight |
| plane match | `DgPlaneMatch`, `DgPlaneBurn`, `DgPlaneDeltaV` | **MEASUREMENT ONLY, ON PURPOSE** - `Overflight.OffPlaneDeg`. F9I hard-gates its own burn off (`dgPlaneChangeEnabled` false; flights 082-100 missed by 54-262 km) and its geometry has an open contradiction. See `docs/PORT_PLAN.md` E4 |
| phasing | `DgPhasing` | **PORTED** as the phase-down above - the overflight search replaces the wait |
| deorbit burn to a target periapsis | `DgDeorbitBurn` | **PORTED** `src/DeorbitOps.cs` - flown against the aim miss, periapsis is the depth limit. DEORBIT NOW points at it, and it now phases down first |
| trunk jettison before the burn | `DgSepStack`, `DgTrunkAndEI` | **PORTED** `EntryOps.Separate` - one decouple, in the retrograde attitude |
| pre-entry trim | `DgPreEntryTrim`, `DgCapsuleTrim` | **PORTED** `EntryOps.Trim` - RCS translation only, attitude never leaves shield-forward, landing reserve outranks the range |
| **lifting entry on bank angle, long-margin schedule** | `DgEntryGuidance`, `DgLongMargin` | **PORTED** law in `pure/EntryMargin.cs` + `pure/EntryGuidance.cs`, glue in `EntryOps.Fly` - lift vector, scaled AoA, one-way drop latch |
| aim point, cross-range | `DgAimPoint`, `DgDownCross`, `DgImpactMiss` | **PORTED** `src/ImpactPredictor.MissTo` (integrated, drag measured) and `Orbital.DownCross` with the 053 lever-arm fix |
| drogues, mains | `DgTerminalParachute` | **PORTED** `pure/Terminal.cs` + `EntryOps` - speed-or-altitude drogue window, mains at 2 km |
| propulsive option | `DgTerminalPropulsive` | **PORTED** engines lit UNDER the drogues, chutes cut only once thrust is proven, hoverslam then hover |
| gear, and the final report | tail of `DgRecoveryMain` | **PORTED** gear after touchdown, never on a splashdown; miss distance logged |

### Known gaps in §8, deliberately

| gap | why |
|---|---|
| S2 de-orbit (`DgUseS2Deorbit`, `DgS2DeorbitToPeri`) | `DeorbitOps` refuses to run with a second stage attached, so only the Draco path is reachable. That makes `Deorbit.AimS2Crew` / `AimS2Cargo` unreachable constants today. Decided at E5; revisit only if a direct launch-to-landing is wanted |
| the plane-change burn | see the table above |
| warp automation | the recorder logs `warp` so a warped row is identifiable; taking the time controls off the crew is a separate decision |

---

## What this says

**All eight phases are ported AND REACHABLE as of 2026-08-11.** The second half of that sentence is
new and it was not true before: a dead-code sweep found the launch window, the rendezvous, the undock
and the docking servo all written, tested, marked DONE - and callable from nowhere. The FLIGHT page
now carries RENDEZVOUS, AUTO-DOCK and UNDOCK & LAND beneath AUTO SEQUENCE, and DEORBIT NOW runs the
whole return. See `docs/PORT_PLAN.md` rule 0.

**All eight phases are ported as of 2026-08-11.** The mission runs end to end: launch on phase,
ascend, recover the booster, insert, rendezvous, dock, refuel, undock, phase down, de-orbit, enter on
lift, and land.

**⛔ AND ALMOST NONE OF IT HAS FLOWN.** Only §2 and §5 are proven in the game - ascent and insertion,
86.0 x 83.8 km with the second stage away. Everything from §4 onward is transcription plus headless
tests: about 1 350 checks, which catch the arithmetic and cannot catch a wrong assumption about what
KSP does. Treat every **PORTED** in this file as "written from the source that flies it", not as
"works".

The order of work is now the order of *verification*, not of porting. The highest-value single test
is a plain RTLS launch reading `b_phase` and `b_predMissKm`: §4 has the most flight-derived constants
riding on it and has never landed. The return stack is downstream of an orbit we already reach
reliably, so it can wait its turn.
