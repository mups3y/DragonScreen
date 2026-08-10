# Porting F9I: the complete map

Built 2026-08-06, after three flights were lost to fixing whatever the last log happened to show.

**The problem this document exists to stop:** I kept grepping F9I for the one function I needed,
porting it, flying, and then grepping for the next thing that broke. That is not porting a flight
software system, it is patching symptoms with better constants. The user called it correctly —
"you are still cherry picking" — twice.

**F9I is 24 885 lines of flown, tuned, instrumented flight software.** Every number in it was paid
for with a flight. This is the inventory. Nothing gets built for the mission without checking here
first, and anything ported gets its row updated.

| file | lines | what it owns |
|---|---|---|
| `falcon9.ks` | 13 232 | the interface, the GUI, and the shared Falcon helpers |
| `F9I/station_ops.ks` | 2 835 | **the whole station mission** — rendezvous, dock, refuel, undock, return |
| `F9I/dragon_deorbit.ks` | 2 565 | **the whole return** — plane match, phasing, de-orbit, entry, terminal |
| `COMMON/GNC.ks` | 1 513 | the shared guidance toolbox everything else calls |
| `SPACEX/BOOSTER.ks` | 971 | **the whole booster recovery** — flip, boostback, entry, land |
| `F9I/F9_payload.ks` | 862 | the ascent mission sequence |
| `F9I/F9boosterTelemetry.ks` | 619 | booster telemetry |
| `SPACEX/PARAM.ks` | 131 | the per-profile ascent constants |

---

## Status key

- **DONE** — ported, with the source cited at the port site, and covered by a headless test.
- **PART** — some of it ported; the rest is not, and the gap is stated.
- **NO** — not ported. We either do without it or we have something weaker.

---

## 1. Ascent — `F9_payload.ks`

The real order, read end to end:

    Liftoff -> Ascent -> MECO -> ExtendRange -> FocusBooster
    -> BurnToApoapsis -> FalconSepS2 -> FalconCircularize -> ORBIT -> MatchSMA

| F9I | what it does | status |
|---|---|---|
| `Ascent` pitch law | `max(90(1−alt/(tgtAlt·gain/100)), MECOangle)` | **DONE** |
| stage-2 pitch law | `min(max(90(1−alt/tanAlt),0.1)+avoidFireDeath, MECOangle)` | **DONE** |
| `PARAM.ks` profiles | RTLS/ASDS/expendable constants | **DONE** (RTLS only) |
| `MECO` | throttle 0, hold attitude, wait 2.5, `SafeStage`, wait 3 | **DONE** (step 2) — a discrete phase, staged on COMMAND at the 60 km target. Starvation staging survives only as the fallback for an engine that quits |
| `FalconExtendRange` | `SetLoadDistances(ship, 1 500 km)` | **DONE** |
| `FalconFocusBooster` | wait for the booster to load, then `forceactive` | **DONE** |
| `BurnToApoapsis` | **RCS fore 0.75 at throt 0.075** — a trickle, not full thrust | **DONE** (step 2) — 6 s ullage, then the proportional deficit throttle |
| `FalconSepS2` | drop the S2 onto a re-entry arc BEFORE circularising | **NO** — this is why our circularisation had enough authority to reach escape velocity |
| `FalconCircBurnVecNow` | "circular HERE, NOW", the true fixed point | **DONE** |
| `FalconCircularize` | the burn loop, no-coast branch, overshoot guard | **PART** — law and cutoff ported; the no-coast branch and `TimeToAltitude` timing are not |
| `MatchSMA` | trim semi-major axis to the station's | **NO** |
| ascent-time learning | measures the ascent and saves it for the next launch's phase window | **NO** |

---

## 2. Booster recovery — `BOOSTER.ks`

| F9I | what it does | status |
|---|---|---|
| `WaitForSep` | hold until actually separated | **NO** |
| `Flip1` | the flip, with gains wound up and put back after | **NO** — we just point at the pad |
| `Boostback` | trim downrange velocity toward the LZ | **PART** — ours is a vacuum ballistic estimate, not F9I's |
| `AtmGNC` | coast, entry-burn gate, guided descent to the ignition point | **PART** — gate and cut ported; the guided descent is not |
| entry-burn soft start | centre engine 0.75 s, THEN the other two | **NO** |
| `LandBurnVars` | `TrueRadar` / `MaxDecel` / `StopDist` / `BurnThrottle` | **DONE** |
| `Land` | margins 6% / 34% flare, three-to-one handover | **DONE** |
| `LandingZoneGuidance` | lean off retrograde by `tan(AoA)` toward the error; **AoA sign FLIPS negative under thrust** | **DONE** — `LeanFraction` + `GuidanceAoaDeg`, with the sign flip and the height taper, both pinned by tests. The glue still has to build the vector and supply the impact point |
| `F9L_OneEngStopDist` | one-engine stopping distance, ratio 2.23 | **DONE** |
| `EngSwitch` / `OctaRead` | octaweb engine-mode switching | **PART** — we shut engines individually; F9I switches a part mode |
| `DeployGridFins` | grid fins out | **NO** |
| `DeployLegs` | legs at 200 m | **DONE** |

---

## 3. Station mission — `station_ops.ks` (43 functions)

**Ported 2026-08-11.** This row used to read "almost none of this is ported" and was the largest gap
on the project.

| F9I | what it does | status |
|---|---|---|
| `StFindStation` / `StLockStation` | find and hold the station target | **DONE** — `StationApproach.Find` |
| `StPhaseAtLaunch` / `StRequiredLead` / `StLaunchPhaseWait` | **launch on PHASE** — wait for the right phase angle | **DONE** — `pure/LaunchWindow.cs` |
| `StClosestPort` | choose which docking port to aim at | **DONE** — `pure/DockGeometry.cs` |
| `StNodeBasis` / `StProNode` / `StNodeSafe` | build burn nodes with a periapsis-floor guard | **DONE** — the floor lives inside `NodeExecutor` so no caller can forget it |
| `StCwSolve` / `StCwLeg` | **Clohessy-Wiltshire two-impulse**, at 0.20 of a period | **DONE** — `pure/CwTargeting.cs`, flown through the node executor |
| `StPhaseLeg` | the phasing-orbit leg | **DONE** — `pure/Phasing.cs`; cannot lower periapsis |
| `StTerminal` / `StCloseIn` | terminal approach and close-in | **DONE** — `src/StationApproach.cs` |
| `StMatchStationOrbit` | match the station's orbit | **DONE** — `OrbitMatch`, first rung of the ladder |
| `StRendezvousAndDock` | the whole approach | **DONE** — `src/DockingOps.cs` |
| `StCloseDockingShroud` | close the nose cone after docking | **DONE** — in `UndockOps`, before release |
| `StTopUpBeforeUndock` | **REFUELLING** | **DONE** — holds on PROGRESS, not on a full tank |
| `StUndock` / `StBackAway` | undock and back off at 1.5 m/s to 150 m | **DONE** — our port only, sign calibrated in flight, burst-then-coast |
| `StPhaseToDeorbitOrbit` | phase to the landing-calibrated 85.1 × 79.2 orbit | **DONE** — `pure/DeorbitOrbit.cs` + `src/PhaseDownOps.cs`; engines proved lit first |
| `StUndockAndLand` | the whole return | **DONE** as a chain rather than one function: `UndockOps` → DEORBIT NOW → `PhaseDownOps` → `DeorbitOps` → `EntryOps` |
| `StMonoForDeorbit` / `StMonoReport` | check the monoprop budget BEFORE committing | **DONE** — `pure/ReturnBudget.cs` |
| `StReturnAllowed` | the go/no-go for coming home | **DONE** — `DeorbitOps.Engage` refuses on it |
| ladder + periapsis floor | >3 km / 0.5–3 km / <0.5 km, floor 75 km | **DONE** |

---

## 4. The return — `dragon_deorbit.ks` (57 functions)

| F9I | what it does | status |
|---|---|---|
| `DgFindOverflight` / `DgSiteInertialAt` / `DgLandLag` / `DgTrackMissAt` | pick the pass whose ground track crosses the LZ | **DONE** — `pure/Overflight.cs`; site evaluated at TOUCHDOWN, lag calibrated not derived |
| `DgPlaneMatch` | measure how far out of plane the site will be at landing | **DONE** — `Overflight.OffPlaneDeg`, reported not burnt |
| `DgPlaneBurn` / `DgPlaneNodeBurn` / `DgRelNodeUt` | fly a plane change | **NOT PORTED, DELIBERATELY** — F9I hard-gates its own off (`dgPlaneChangeEnabled` false; flights 082–100 missed by 54–262 km), its geometry has an unresolved contradiction in its own source, and it needs MechJeb's node executor. See `docs/PORT_PLAN.md` E4 |
| `DgDeorbitBurn` | the burn, throttled on periapsis AND range error | **DONE** — `src/DeorbitOps.cs`, flown against the aim miss with periapsis as the depth limit |
| `DgAimPoint` / `DgAimMiss` / `DgImpactMiss` / `DgDownCross` | where we are actually going to land | **DONE** — `ImpactPredictor` (ours, drag measured) + `Orbital.DownCross` with the flight-053 lever-arm fix |
| `DgCapsuleTrim` / `DgPreEntryTrim` | take the long bias out during the dead coast | **DONE** — `EntryOps.Trim`, RCS translation only, landing reserve outranks the range |
| `DgRcsDeorbit` | de-orbit on RCS alone | **DONE** — `DeorbitOps` runs the burn on RCS when there is no thrust |
| `DgSepStack` / `DgTrunkAndEI` | separation sequencing into entry | **DONE** — `EntryOps.Separate`, one decouple in the retrograde attitude |
| `DgCoastToEI` | coast to entry interface, with warp | **DONE** minus the warp — warp automation is out of scope; every gate here is an altitude or a speed, not a clock |
| `DgEntryGuidance` | the AoA schedule with **shorten-only + lead** | **DONE** — `pure/EntryGuidance.cs` (all four traps) + `EntryOps.Fly` for the lift vector |
| `DgLongMargin` | the measured margin curve | **DONE** — `pure/EntryMargin.cs` |
| `DgTerminalParachute` | chutes | **DONE** — `pure/Terminal.cs` + `EntryOps` |
| `DgTerminalPropulsive` | SuperDraco landing | **DONE** — engines lit UNDER the drogues, chutes cut only once thrust is proven |
| `DgLandingReserve` / `DgUseS2Deorbit` | reserve and mode decisions | **DONE** for the reserve and the terminal mode. The S2 de-orbit branch stays unreachable: `DeorbitOps` refuses to run with a second stage attached |
| `DgDeployGear` | gear out of the heat shield | **DONE** — after touchdown, and never on a splashdown |

---

## 5. The shared toolbox — `GNC.ks` (48 functions)

The layer everything above stands on. **Tranche 1 ported 2026-08-06 → `pure/Orbital.cs`, 158 checks.**

| F9I | status |
|---|---|
| `AltToTA` | **DONE** — and the signature trap its own comment flags is CLOSED, see below |
| `TimeToAltitude` | **DONE** — mean-anomaly propagation, all three modes |
| `PhaseTime` | **DONE** as `PhaseWaitSeconds` — signed, never returns a missed window |
| `Hohmann` | **DONE** — both burns, signed, reversibility tested |
| vis-viva (`StVisViva`) | **DONE** — handles negative sma, so an escape trajectory does not return NaN |
| `FalconCircBurnVecNow` scalar form | **DONE** — the fixed point, tested in both directions |
| `DgGCDist` / `DgBearing` / `DgOffsetLatLng` | **DONE** — round-trip tested at eight bearings |
| synodic period | **DONE** — returns 0 for matched orbits rather than dividing by zero |
| `PhaseAngle` | **NO** — needs vectors, belongs in glue |
| `TimeTwoTA` | **DONE** (tranche 2) — `Predict.TimeBetweenTrueAnomalies` |
| `GroundTrack` | **DONE** (tranche 2) — the body-rotation longitude shift |
| `ImpactUT` | **DONE** (tranche 2) — with F9I's damping, and an iteration cap that reports non-convergence |
| `ClosestApproach` | **DONE** (tranche 2) — coarse-to-fine, sampling handed in as a delegate |
| `Impact` (the latlng form) | **NO** — needs the glue to supply geopositions |
| `ExecNode` throttle law | **DONE** (tranche 3) — `pure/BurnExec.cs`, and **one ordering defect fixed**, see below |
| `ExecNode` burn centring / coast gate / settle timer | **DONE** (tranche 3) |
| `VecToNode`, `CreateNode` | **NO** — need KSP node objects, glue |
| `DockGNC` + `RCSTranslate` | **DONE** (tranche 4) — `pure/DockControl.cs`, and **one saturation defect fixed**, see below |
| `PlaneMnv`, `OrbitBinormal`, `TargetBinormal`, `NodeAltitude` | **NO** |
| `IntegLand`, `LandThrottle` | **PART** — the landing throttle came via BOOSTER.ks instead |
| `HillClimb`/`Score`/`Improve`, `RSVPMnv` | **NO** — the optimiser |

**Two things were improved rather than copied, and both are recorded at the port site:**

- **`AltToTA`'s signature trap is closed.** GNC.ks takes `sma, ecc` but clamps against
  `ship:orbit:periapsis/apoapsis` — its own comment says "handing it another vessel's sma/ecc would
  mix two orbits and return nonsense... the signature invites the mistake." Both its callers happen
  to pass the ship's own figures so it has never been wrong in flight. **The rendezvous port has to
  ask about the TARGET's orbit**, so periapsis and apoapsis are parameters here.
- **`PhaseTime` no longer hides a missed window.** GNC.ks returns `abs(t)` and admits it "cannot tell
  you that you have JUST missed the window". Ours rolls forward to the next one, which is the honest
  version of the same answer.
- **`ExecNode`'s floor-lift is applied BEFORE the alignment term, not after.** GNC.ks:1085 computes
  `reqThrot = min(dv/accel,1) * angleMult` and then doubles it if under 0.5 — so the lift fires on
  MISPOINTING as well as on a small demand, and the throttle stops being monotonic in pointing error:
  1° off gives 0.80, 2° gives 0.60, **3° gives 0.80 again**. A vessel drifting off axis gets a
  throttle *kick* exactly as its pointing degrades, which is the opposite of what the alignment term
  is for. Their own comment says the lift is for "small demands … rather than dribbling" — the end of
  a burn — so lifting the time-based demand first keeps the intent and removes the artifact. Aligned
  burns are unchanged. **Found by a headless monotonicity sweep, not in flight**, where it would have
  shown up only as a burn that wanders.
- **`DockGNC`'s axis mixing ranks the OFFSETS, not the PID outputs.** GNC.ks:1259 compares
  `dockOutF/S/T` to decide which axis gets half authority — but with `P = 8` those saturate at ±1 for
  almost any real offset, so on an actual approach all three are pegged at 1.0, the comparison
  decides nothing, and the chain falls through to its last branch every time regardless of geometry.
  Caught here by a pure forward offset being reported VERTICAL. The offsets do not saturate, and
  "which axis has the most left to do" is what the mixing was always asking.

---

## ⛔ WHY OURS FLIES WORSE THAN F9I — ANSWERED 2026-08-06, AND IT IS ARCHITECTURAL

Two causes, one already fixed and one not.

### 1. The part names were wrong (FIXED)

I matched the booster on `"K1"`, taken from the **right-click menu title** "Ghidorah K1-180 Tank".
The actual `part.name` is `TE.19.F9.S1.Tank`. **`.S1.` and `.S2.` are the markers.**

A wrong name string cannot fail loudly — it just quietly means "no", and the damage was not the
obvious one:

    HasBooster() always FALSE -> SecondStage always TRUE
      -> the FIRST stage flew the SECOND stage's pitch law, which is capped at MECOangle
      -> at 400 m the vehicle slammed 90 deg -> 45 deg, in the thickest air of the ascent
      -> "very unstable flight"

...and separately, `FindBooster` never matched, so the recovery could not run whatever else was
fixed. All name matching now goes through `pure/VehicleParts.cs`, once.

### 2. ⛔ WE STEER WITH STOCK SAS. F9I DOES NOT. (NOT FIXED — this is the real gap.)

**F9I: `lock steering to lookdirup(dir, up)` driven by kOS's STEERING MANAGER**, and it tunes that
manager per phase — `rollts 20`, `rolltorquefactor 3`, `maxstoppingtime` 0.05 / 1 / 10,
`torqueepsilonmax/min`, `resettodefault()` between phases.

**Ours: `vessel.Autopilot.SAS.SetTargetOrientation(dir, false)`.** Three consequences:

- **No roll reference.** `lookdirup` commands a full attitude — where the nose points AND where the
  top faces. `SetTargetOrientation` takes a direction only, so roll is left to SAS. A launch vehicle
  with uncommanded roll is exactly the wandering that was flown.
- **Stock SAS is built to hold a fixed navball marker**, not to track a guidance vector that moves
  every frame. It has no feed-forward and no tunable damping.
- **Nothing is tuned per phase.** F9I uses a loose deadband in vacuum coast and a tight one for the
  landing burn, because those want opposite behaviour.

**The fix is not a gain tweak — it is our own attitude controller, plan step 4.** And it does not
need inventing: **`Desktop/mechjeb_src/MechJeb2/AttitudeControllers/` already contains kOS's
steering manager ported to C#**:

| file | lines | what it is |
|---|---|---|
| `KosAttitudeController.cs` | 197 | **kOS's steering manager, in C#** — the exact algorithm F9I flies |
| `KosPIDLoop.cs` | 173 | its PID, with the unwind behaviour |
| `TorquePI.cs` | 16 | the torque-to-actuation stage |
| `BetterController.cs` | 547 | MechJeb's own, newer |
| `DirectionTracker.cs` | 75 | direction + roll reference handling |

`KosAttitudeController` is a cascaded loop — attitude error → angular-rate setpoint (`TorquePI`) →
actuation (`KosPIDLoop`) — with `MaxStoppingTime` and `RollControlRange` as its tuning, which are the
same knobs F9I sets. **Porting those ~390 lines gives us F9I's steering exactly**, in C#, with no kOS
dependency, and it closes the one remaining structural difference.

It also unblocks something else: our own controller writes `FlightCtrlState` directly, so the
`ctlPitch/ctlYaw/ctlRoll` columns that are dead in the whole black-box corpus start recording, and
the system-identification pass in `FLIGHT_SOFTWARE_PLAN.md` becomes possible.

## Honest summary

Roughly **15% ported**, and the ported part is the part I happened to grep for. What is missing is
not detail — it is the **middle and end of the mission**: launch-on-phase, the CW rendezvous,
docking, refuelling, undock, the phasing to the calibrated de-orbit orbit, and the closed-loop entry.

**THE ORDER ABOVE IS SPENT — all five steps are done as of 2026-08-11.** It read: 1. `GNC.ks`
toolbox · 2. ascent architecture · 3. booster · 4. station · 5. return.

**⛔ WHAT REPLACES IT IS NOT MORE PORTING. IT IS FLYING.** Each step was supposed to end with a
flight and a log read, and that half did not happen — only §2 and §5 of the mission have flown. The
order of work is now the order of verification, and it is at the bottom of `docs/MISSION_PLAN.md`.

## Explicit gaps confirmed 2026-08-11 (external review + verification)

| item | F9I source | status | note |
|---|---|---|---|
| `Flip1` — rate-limited rotation to a commanded attitude before boostback | `BOOSTER.ks:286-380`, called at :226 (180°) and :231 (170°) | **NO** | `LandingSites.FlipDeg`/`FlipPower` carry the constants and are marked NOT WIRED. Our boostback points at the LZ and lights; there is no flip manoeuvre. |
| ASDS boostback target shift — LZ moved 2.7 km further downrange, burn flown against the shifted target, real LZ restored | `BOOSTER.ks:483-500` | **NO** | RTLS uses the signed-overshoot form, which is ported. The droneship branch is not. |
| Impact prediction with drag | `addons:tr` (Trajectories) | **PART** | Trajectories is installed but is a third-party dependency we do not take. `BoosterRecovery.PredictedMiss` is a vacuum ballistic solve, so it predicts LONG — the same direction as the deliberate 2.7 km overshoot. The two are a pair; do not "improve" one alone. |
| `TimeToAltitude` circularisation timing / no-coast branch | `F9_payload.ks` | **NO** | Current gate is `timeToAp <= 12 s` or periapsis already high. Flew correctly to 86 × 84 km on 2026-08-10. |
| Ullage delivered through `OnFlyByWire` | — | **PART** | `AutoPilot` writes `v.ctrlState.Z` from Update, which KSP rebuilds each FixedUpdate, so it is very likely a no-op. Harmless in stock (no ullage model); would matter under Real Fuels. Now that `AttitudeController` owns the callback the fix is a one-line move. |

## Flip1 + Boostback ported 2026-08-11 (rule 5: this row was written with the code)

| F9I source | ours | differences, and why |
|---|---|---|
| `BOOSTER.ks:256-283 WaitForSep` | `LandingPhase.Flip`, first `SepQuietS` = 2 s | Its closing `wait 2` is the whole reason it is here: "KSP is still resolving the vessel split and the physics of two bodies a metre apart. Steering into that produces a lurch and can push the booster back into the upper stage's plume." We detect separation by the booster appearing as a vessel rather than by the S2 tank leaving `ship:parts`; same event, different observer. |
| `BOOSTER.ks:295-381 Flip1(180, 0.333)` | `LandingPhase.Flip` + `BoosterRecovery.StepFlip` | **Structural difference, stated:** F9I blocks in two `until` loops; we have no `wait`, so the same schedule runs one step per tick. Coarse/fine thresholds, the 7.5° nose-catch, the snap at 15°, the rate of 0.333°/step and the axis construction are line-for-line. **NOT ported:** the roll-settle wait (10°/1 s/8 s) — our controller holds roll rather than commanding it, so there is nothing to settle. Gains: `maxstoppingtime 3` ported; the `pitchts/yawts ×1.5`, `ki ×1.5`, `rolltorquefactor 3` tweaks are kOS steering-manager knobs our cascade does not expose. |
| `EngSwitch(0, 1)` before the flip | `EnginesFor(Flip) = 3` | "Nine engines on a nearly empty stage is far more thrust than the boostback needs and the gimbal authority of the outer ring fights the rotation." Commanded during the flip so the mode switch has finished before the burn. |
| `BOOSTER.ks:394-518 Boostback` first half | `LandingPhase.BoostbackKill` | Flat retrograde until `vxcl(up, srfretrograde):mag <= 0.03`. **This half did not exist in our version at all** — we aimed at the pad from the first tick, which is why the landings went to the wrong place. |
| second half | `LandingPhase.Boostback` | `BBvec` horizon bearing, `max(0.25, impDist/intDist)` taper, −2700 m overshoot. |
| `EngSpl` throttle ramp | `Landing.Ramp`, 1.333/s | Ported as a ramp because F9I says a step to 1.0 on three Merlins shocks the stage mid-rotation. **NOT ported:** the ramp-DOWN at burn end (`EngSpl(0)`, ~0.75 s) — our Coast still cuts. |
| `steeringmanager` gains through the burn | `BoostbackStoppingTime = 15` | "The boostback wants a steady hold, not a fast one." We were using 1. `rollts 20` has no equivalent in our cascade. |
| `rcs off` for the burn | **NOT PORTED** | "Three Merlins gimballing have far more authority than the cold gas, and leaving RCS on just wastes the propellant the landing needs." We never command booster RCS either way, so nothing is wasted yet — but when RCS is added, this is the rule. |
| ASDS branch (`BOOSTER.ks:483-510`) | **NOT PORTED** | Target shifted 2.7 km downrange, `max(0.125, ...)` floor, no sign test. RTLS only for now. |

## Audit of the AtmGNC/Land port, 2026-08-11 — what the first pass still missed

Checked line by line after the port was claimed complete. Four more, one of them the largest
remaining landing-accuracy error:

| F9I | what we had | fixed |
|---|---|---|
| `LandingZoneGuidance:553` `errorVec is impactPos:position - LZ:position` | the stage's CURRENT horizontal offset from the pad | **YES** — now the predicted impact point, same ballistic solve the boostback uses |
| `Land:833` `set F9L_AOA to -0.25` after the handover | taper floored at −1, so −0.25 was unreachable and the stage kept steering to the ground | **YES** |
| `AtmGNC:664` `rcs on` / `Land:780` `rcs off` | booster RCS never commanded either way | **YES** — on for the coast, off for the landing burn |
| `AtmGNC:753` `wait until ship:altitude < 4000` before the ignition watch | no 4 km gate | **NO, deliberately** — checked the arithmetic: at 30 km and −300 m/s the stop distance is 2.3 km against 30 km of height, so the gate cannot fire early. Noted rather than added. |
| `LandingZoneGuidance:565` naive `velVec + errorVec` when it is gentler than the AoA ceiling | always the clamped form | **NO** — the clamped form is the conservative one and never leans further than F9I would |
| `AtmGNC:653` torque-epsilon deadbands | no equivalent | **NO** — kOS steering-manager knob our cascade does not expose |
