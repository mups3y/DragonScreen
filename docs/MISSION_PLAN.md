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
| Clohessy–Wiltshire solve and leg | `StCwSolve`, `StCwLeg` | **PORTED** `pure/CwTargeting.cs`, wired with a periapsis floor |
| terminal approach, closest port | `StTerminal`, `StClosestPort`, `StCloseIn` | **PORTED** ladder and port selection both |
| node execution | `StExecNode`, `StBurnNode`, `StVisViva` | **PORTED** `src/NodeExecutor.cs`, with the periapsis floor inside it |
| speed cap by range | `StSpeedCap` | **PORTED** `Approach.SpeedCap`, all four bands |

---

## 7 · Docking, refuel, undock

| step | F9I | ours |
|---|---|---|
| dock | `StRendezvousAndDock`, `StClosestPort` | **PORTED** `src/DockingOps.cs` - gate, hull skirt, axial run |
| top up propellant before release | `StTopUpBeforeUndock` | **PORTED** holds on PROGRESS, not on full |
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
| phase into the deorbit orbit | `StPhaseToDeorbitOrbit` | **NO** |
| find an overflight of the site | `DgFindOverflight`, `DgSiteInertialAt` | **NO** |
| plane match | `DgPlaneMatch`, `DgPlaneBurn`, `DgPlaneDeltaV` | **NO** |
| phasing | `DgPhasing` | **NO** |
| deorbit burn to a target periapsis | `DgDeorbitBurn` | **PORTED** `src/DeorbitOps.cs` - flown against the aim miss, periapsis is the depth limit. DEORBIT NOW points at it. |
| trunk jettison before the burn | `DgSepStack`, `DgTrunkAndEI` | **PART** we fire it, but not on F9I's schedule |
| pre-entry trim | `DgPreEntryTrim`, `DgCapsuleTrim` | **NO** |
| **lifting entry on bank angle, long-margin schedule** | `DgEntryGuidance`, `DgLongMargin` | **PART** schedule AND controller ported (`pure/EntryMargin.cs`, `pure/EntryGuidance.cs`, 23 checks); the glue that drives bank angle from them is not written |
| aim point, cross-range | `DgAimPoint`, `DgDownCross`, `DgImpactMiss` | **PORTED** `src/ImpactPredictor.MissTo` - integrated, drag measured in flight |
| drogues, mains | `DgTerminalParachute` | **PART** buttons wired, no sequencer |
| propulsive option | `DgTerminalPropulsive` | **NO** |

---

## What this says

Of the eight phases, **two are done** (ascent to MECO, upper stage to orbit), **one is close**
(separation), **one is half-ported and wrong in four specific places** (booster recovery), and
**four have not been started** (launch window, rendezvous, docking/refuel, return and entry).

The order of work is the order of the flight. Fix §4's four NOs, because that is what is failing
now and every one of them is a line of `AtmGNC` or `Land` I had not read. Then §1, §6, §7, §8 in
sequence — each one starting by reading its F9I function end to end, and adding its row here first.
