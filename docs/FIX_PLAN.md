# Fix plan — researched against F9I, 2026-08-11

Companion to `docs/AUDIT_2026-08-11.md`. The audit says what is broken; this says how to fix it, with
the F9I source checked **before** the fix was chosen rather than after.

**Four of the fixes I sketched in the audit were wrong.** Researching them changed the answer in every
case, and in three of them the change is from "write something" to "delete something". That is
recorded below rather than quietly corrected, because the sketch is what I would have built.

---

## RESEARCH FINDING 1 — the rendezvous fix is a deletion, not a rewrite

**Audit said:** give the ladder memory; make `OrbitMatch` target the station's SMA with a two-burn
Hohmann; add an attempt/Δv budget.

**F9I says:** none of that. Three things settle it.

`station_ops.ks:1951`, immediately above the function:

> **`---- DEAD BY DECISION: NOTHING CALLS StMatchStationOrbit, AND NOTHING SHOULD. ----`**

`StRendezvousAndDock:2027-2036`:

> `---- NO CIRCULARISATION, NO NODES, NO PHASING. JUST GO. ----`
> *"Every node-based step has hurt rather than helped. StMatchStationOrbit's 'circularise at apoapsis'
> burn **overshot on flight 011**: SMA 683.06 → 691.61 km against a station at 686.75… The launch
> already puts us in the station's orbit; what is left is a RELATIVE MOTION problem, so fly it
> directly."*

And on the phasing fallback (`:2100`):

> *"A failed approach is a thing to report and re-try deliberately, not to hand to a planner that
> answers with a two-month wait. (flight 020: 60 days; 015: 89 days; 014: 13 days)"*

**F9I hit our exact divergence, on flight 011, and deleted the step.** Our `CirculariseAtApoapsisDv`
is a faithful port of a function F9I marks dead. The bug is not the law — it is that we put a
one-shot procedural step inside a per-tick classifier that re-fires it forever.

**The fix.** `StationApproach.Tick` becomes F9I's live path:

```
range <= GoalRangeM      -> Arrived  (hand to DockingOps)
range <= DirectApproach.GateM (10 km) -> DirectApproachOps   ← distance, and nothing else
otherwise                -> STOP. Report range and relative velocity. Hold attitude.
                            Do not burn. Say "press RENDEZVOUS again to retry from here."
```

`MatchOrbit`, `Phasing` and `Clohessy` become unreachable from the live path and are marked
**DEAD BY DECISION**, quoting F9I, in the same words F9I uses. This closes **C1, C2, C3 and C8 with
one change**, and it removes the need for the budget in C3 because nothing loops any more.

⚠ `LaunchWindowOps` is what makes this safe — it is the thing that puts us inside 10 km. It went in
this morning and has never worked (C4), so **step 1 must land before step 2 is meaningful.**

---

## RESEARCH FINDING 2 — the booster RCS map is not "on for the flip"

**Audit said:** set `Rcs = true` for Flip, BoostbackKill and Boostback.

**F9I's actual sequence** — every `rcs` statement in `BOOSTER.ks`:

| line | where | state |
|---|---|---|
| 303 | `Flip1`, with `maxstoppingtime 3` | **on** |
| 408 | `Boostback`, first line, with `clearscreen` | **off** |
| 429 | `Boostback`, on `EngSpl(1)` — throttle spool-up | **on** |
| 659 | `AtmGNC` — coast, entry burn, guided descent | **on** |
| 781 | `Land` — the landing burn | **off** |
| 871 | end of recovery | off |

So: on for the flip; **off while the boostback establishes its aim, back on when the engines spool**;
on through the whole descent; off for the landing burn.

**The fix.** In `Landing.Guide`:

* `Flip` → `Rcs = true`
* `BoostbackKill` / `Boostback` → `Rcs = Ramp(s.PhaseElapsedS) > 0.0` — off until the throttle
  starts spooling, on from there. That reproduces 408/429 exactly instead of approximating it.
* `Coast`, `EntryBurn`, `Descent` → `Rcs = true` (Coast already is)
* `LandingBurn` → `Rcs = false` (already correct)

---

## RESEARCH FINDING 3 — `pure/Entry.cs` should be DELETED, not wired

**Audit said:** wire it into `EntryOps` or delete it.

**F9I says delete.** The band fractions `0.00 / 1.00 / 0.55 / 0.13` appear in exactly one place
(`dragon_deorbit.ks:719`):

```
set addons:tr:descentangles to list(dgA*dgTrEntry, dgA*dgTrHigh, dgA*dgTrLow, dgA*dgTrFinal).
```

That is **configuration handed to the Trajectories mod** so *its* predictor and *its* navball markers
assume the right attitude. It is not an attitude anyone flies.

What F9I actually flies is one trim angle, scaled by the lift command (`:2317`):

```
set dgAoACmd to dgAoA * min(1, dgMag).     // dgAoA = 15, one constant
```

— which is exactly what `EntryGuidance.AoaCommandDeg` already does and what `EntryOps.Fly` already
applies. **There is no second law to wire.** `pure/Entry.cs` ports a mod's configuration block, and we
do not use that mod.

**The fix.** Delete `pure/Entry.cs` and its tests. Move the CargoDragon_012 lesson it carries (grades
= true means RETROGRADE; an entry flown 134.9° off the nose) into `docs/F9I_PORT_MAP.md` under
"deliberately not porting", because it is a lesson about *Trajectories*, not about our vehicle.
Correct the PORT_PLAN E7 claim that it "composes with the new range controller" — it never did.

---

## RESEARCH FINDING 4 — H5 is not a sign bug

**Audit said:** exercise `PredictedMiss`'s sign; it may be wrong.

**It is right.** F9I's boostback (`BOOSTER.ks:465-472`) negates the distance once the impact is beyond
the pad and terminates on `impDist < -2700` — so **negative means past the pad**. Ours returns
`Dot(err, -toLz) > 0 ? +miss : -miss`, which is positive when the impact falls between us and the pad
and negative when it is beyond. Same convention.

The reason it never fired is **C10 + the 180° flip**: the booster was 67–82 km short for the whole
flight and never approached the pad, so a correct test never had cause to trip.

**The fix.** No code change. Add a headless test pinning the convention in both directions so it
cannot rot, and re-read it after the first flight where boostback actually works.

---

## The plan

Each step ends in something provable. Step 3 is a flight, deliberately placed before the structural
work rather than after it.

### Step 1 — Undo today's damage · C4, C7, H7, H8, H1
Nothing here needs research; it is all restoring what a change broke this morning.

1. Move the pad hold from `AutoPilot.Engage` into `Tick`, after the packed check. `Engage` must reach
   `PrepareForSeparation` on every path.
2. Set `liftoffUt` when the vehicle actually leaves the pad, not in `Engage`.
3. `FlightCommands`: `DeorbitNow` → `DeorbitOps.Toggle()`.
4. `FlightDriver` owns the recorder's lifetime alone; `AutoPilot.Disengage` stops stopping it.
5. `Pages.MissionRect` measured **up from `ChromeBar.TopY`**.
6. **New permanent test** — the rect sweep written during the audit: every control on every page, at
   1280×703 / 1280×710 / 1280×600, must sit inside the drawable area and round-trip to its own action.
   It fails today on the mission buttons and on the step list at 600 high.

**Proves:** the booster is recoverable again and the crew can abort.

### Step 2 — Give the booster its authority back · C10, H2, H3, H6
1. The RCS map from research finding 2.
2. Implement the roll-settle gate that `FlipRollToleranceDeg/MinS/MaxS` already describe — the three
   constants exist and nothing reads them.
3. Delete the invented `max()` in `GuidanceAoaDeg`; `BOOSTER.ks:755` is a plain `alt:radar / 100`.
4. Move `BoosterRecovery`'s packed check above `Landing.Guide` so a stage on rails cannot advance its
   own phase machine.
5. Test: `PredictedMiss` sign both ways (research finding 4).

**Proves:** the flip completes in a sane time and the descent steers as F9I's does.

### Step 3 — Fly it
Launch → insertion → booster landing. Read `b_phase`, `b_predMissKm`, `b_torqueX`, `b_omegaP`.

Two open questions this flight settles and nothing else can:
* whether the flip is now quick, which tests the whole of step 2;
* **the unexplained torque gap** — reported authority against achieved angular acceleration. With RCS
  actually on, `b_torqueX` and `b_omegaP` together answer it. Until then it stays an open measurement,
  not a conclusion. See the audit's discarded impression #1.

### Step 4 — Make the rendezvous F9I's rendezvous · C1, C2, C3, C8, C9, H4, M4
1. `StationApproach.Tick` reduced to the live path in research finding 1.
2. `MatchOrbit`/`Phasing`/`Clohessy` marked DEAD BY DECISION with F9I's own words and flight numbers.
3. `DockControl.SpeedCapFor` → `Approach.SpeedCap`. `Rendezvous.CorridorRate` deleted: `DockGNC`'s own
   header says *"Do not wire the function itself back in"*, and `StCloseIn`/`StDirectApproach` — the
   paths F9I actually flies — both use `StSpeedCap`.
4. Warp capped at one orbital period per request; never inside the direct gate.
5. Test from this flight's geometry (4.4 km, 750 m SMA error): must reach the goal without a node.

**Proves:** the failure that ended the last flight cannot recur.

### Step 5 — Fly the rendezvous
Launch on the window, arrive inside 10 km, direct approach, dock, refuel, undock. Read the `m_` block.

### Step 6 — One owner per actuator · C5, C6
Only now, because steps 1–5 remove most of the contention and a flight will have shown what remains.
1. `NodeExecutor.Begin` takes an owner token; `Active`/`Phase`/`Note` readable only by the owner.
2. One mission-phase arbiter — ascent, rendezvous, docking, undock, return mutually exclusive.

### Step 7 — Wire or delete, and the rest · C11, H9, H10, M1–M9, L1–L9
Delete `pure/Entry.cs` (research finding 3). Collapse the duplicated landing orbit, the two `0.333`
and the three drogue altitudes to one home each. Retire `StartDeorbit` and point WATER DEORBIT at
`DeorbitOps`. Fix the stale NOT-WIRED block on `FlipDeg`. Then the mediums and lows.

---

## What the research changed

| audit sketch | after reading F9I |
|---|---|
| Rewrite `OrbitMatch` as a Hohmann; add ladder memory and a Δv budget | **Delete the orbit-match path.** F9I marks it dead by decision after flight 011 |
| `Rcs = true` for the flip and boostback | Six-state map; boostback is **off until the throttle spools** |
| Wire `pure/Entry.cs` into `EntryOps` | **Delete it.** It is Trajectories configuration, not a flight law |
| Fix `PredictedMiss`'s sign | **The sign is right.** Add a test; the symptom is downstream of the flip |

Three of four are deletions. The pattern is the one this project keeps re-learning: **the expensive
mistake is not failing to write something, it is writing something F9I already tried and removed.**


---

## Flight 2026-08-11 13:44 — seven more, all fixed

See [FLIGHT_2026-08-11_1344.md](FLIGHT_2026-08-11_1344.md) for the log evidence and the F9I
citations. Summary, because four of these are the same shape as the audit's own worst pattern:

| # | fault | shape |
|---|---|---|
| F1 | "no free docking port on the station" — the station was **unloaded**, so `parts` was empty | a true sentence about an empty list, a false one about the world |
| F2 | docking approach steered with no roll reference; KDSS needs `captureMinRollDot = 0.5` | same omission as the booster flip |
| F3 | the 300 s burn backstop was measured from before a 3663 s warp | a backstop on the wait, not the burn |
| F4 | no monopropellant floor on the Dracos, though the S2 has one | asymmetry between two branches of one guard |
| F5 | short-of-reserve and short-of-burn shared one warning, and it flew anyway | two failures, one message |
| F6 | an aborted de-orbit was handed to the entry at Pe 80.1 km | trusted the abort string instead of the orbit |
| F7 | `trunk jettison commanded (0 decoupler(s))` logged as success; the error then blamed attitude | a guard that fires silently, and a diagnostic pointing away from the cause |

F1, F3 and F7 were each settled by a source outside my own reasoning — the partdumps and F9I — and
in two cases that source **overturned my first diagnosis**. F7 in particular: I had already written
"the trunk only has the action" into a comment before the partdump showed it has both.


---

## CORRECTION — the rendezvous was not "deletion not rewrite"

The research table above says *"Rewrite `OrbitMatch` as a Hohmann → **Delete the orbit-match path.**
F9I marks it dead by decision after flight 011."* The deletion is right. **The conclusion I drew
from it was not**, and it went into `StationApproach.cs` as a banner claiming F9I's live rendezvous
is "arrived, or inside the gate and flying it directly, or STOPPED. There is no fourth branch."

F9I's live rendezvous is **`StCloseIn` (station_ops.ks:855)** and it has three legs:

1. `StPhaseLeg` in a bounded loop — `until gap <= stPhaseMin`, capped at `stPhaseMaxPass = 3`
2. ride an existing intercept — closest approach inside `stCaUseMax = 27000`, warp there, `StMatchVelAt`
3. `StDirectApproach` inside `stDirectMax = 10000`

I had conflated `StMatchStationOrbit` (dead, and its source says so in capitals at :1951) with
`StPhaseLeg` (live, called from :882). **What cost a flight was the circularise-at-apoapsis burn,
not phasing.**

The cost of my error was concrete: on 2026-08-11 13:44 the capsule sat 10.2 km out receding at
116 m/s and the only answer the code had was STOPPED. Nothing was wrong with the vehicle.

### What went in

| leg | what | source |
|---|---|---|
| 0 | circularise **at the station's radius crossing** | new — see below |
| 1 | phasing laps, bounded at 3, adaptive lap count | `StPhaseLeg` + simulation |
| 2 | ride an existing intercept and match velocity there | `StCloseIn:1786-1841` |
| 3 | direct approach | unchanged |

**Leg 0 is the only thing here that is not a port, and it exists because the simulation said the
port alone would not converge.** `Phasing.ExitDvMps` circularises at whatever radius the coast
returns to; from the 182 × 81 km orbit that flight was actually in, that leaves a 411 s period error
and **933 km of fresh drift per lap**. Three bounded laps could never close it.

It is *not* the burn flight 011 deleted, and the distinction is the whole justification:

- **`StMatchStationOrbit`** circularised at **our apoapsis** — not the station's radius, so it lands
  in the wrong orbit by construction. It went to SMA 691.61 km against a station at 686.75.
- **Leg 0** circularises where our radius **equals the station's**. There `sqrt(mu/r)` *is* the
  station's speed, so a correct burn cannot land anywhere else. It cannot overshoot the way flight
  011 did because the target speed is measured at the target radius.

It refuses rather than guesses when our orbit never reaches the station's radius — that needs a
transfer, and inventing one there is how the deleted burn came to exist in the first place.

### Simulated before written, per `falcon-rendezvous-approach-law`

Sweeping the phasing law across every gap it can physically see (a projection along the track, so it
cannot exceed half a lap — 2156 km at the measured station orbit):

| gap | a_phase | periapsis | Δv | one lap? |
|---|---|---|---|---|
| 10 km | 687 km | 86.3 km | 1.75 m/s | yes |
| 100 km | 697 km | 86.3 km | 17.20 m/s | yes |
| 800 km | 771 km | 86.3 km | 121.58 m/s | yes |
| 1700 km | 867 km | 86.3 km | 224.91 m/s | yes |
| 2100 km | — | — | 262.77 m/s | **rejected — over the 250 cap** |
| 2100 km | — | — | 153.23 m/s | yes, over **two** laps |

**Periapsis does not move at any gap** — the burn point stays an apsis, which is the structural
reason phasing cannot do what flight 012 did. And the band near the maximum was being refused for
being *expensive* rather than *wrong*, so `Phasing.SolveAdaptive` spreads it over more laps. The
250 m/s cap is untouched: it is what caught flight 014's 1353 m/s.

Leg 0's cost, simulated: 69.0 m/s / ~89 units of monopropellant from 182 × 81, 2.8 m/s / 3.7 units
from 90 × 86, nothing when already co-orbital. Expensive from a bad orbit, paid once, not per lap.

### The real boundary, stated plainly

"Any distance" now holds. **"Any orbit" does not, and cannot be made to hold here.** If our orbit
never crosses the station's radius, leg 0 refuses and says so. That is a transfer, and it is the
one thing in this area that has already cost a flight.


---

## Hull cameras on the VIDEO tab — 2026-08-11

User: *"the tundra parts should have cameras attached by default ... I would like these cameras to be
the camera views we can pick on the dragonscreen."*

**`MuMechModuleHullCameraZoom` is HullCameraVDS, not MechJeb.** The MuMech prefix is historical and
it misleads — MechJeb is not installed in this game at all. HullCameraVDS is, at
`GameData/HullCameraVDS/Plugins/HullcamVDSContinued.dll`, and it is what defines the module.

### What the vehicle actually carries

`TundraExploration/Patches/Extra_Hullcam.cfg` adds cameras to the **interstage (two), the S2 tank,
the FH nosecone, the fairing and the fairing adapter** — not, as expected, to the capsule, trunk or
booster tank. The 13:34 partdump shows more than that on the flown craft (a HazCam pair, two booster
guidance units, one on the K2-81 tank), which are separate HullCameraVDS parts bolted on in the VAB.

So the list cannot be written down. **It is enumerated off the vessel at runtime.**

### How

`src/HullCams.cs`, by reflection on the type NAME — no compile-time reference to
`HullcamVDSContinued.dll`, because a hard reference would stop the plugin loading for anyone without
the mod. `build.py`'s MODS list stays empty. The three cases all degrade to the truth:

| situation | result |
|---|---|
| mod installed, cameras on the craft | they appear |
| mod installed, no cameras | nothing appears |
| mod not installed | nothing appears, no error |

Views 0–3 stay the four hull-swept directions (real, derived from the control point and a live hull
sweep). Real cameras are appended from index 4 up, so a saved selection keeps meaning what it meant.

### Three things that needed care

1. **A zero `cameraForward` is not a direction, it is "use the transform".** Every Tundra camera
   ships `0, 0, 0` and relies on `cameraTransformName`; HullCameraVDS's own docking-port patch does
   the opposite. Both forms are live in this install, so a zero vector is treated as absent —
   normalising it would aim every Tundra camera at nothing.
2. **A camera can be jettisoned mid-flight.** The interstage cameras leave with the first stage.
   The transform is re-validated every frame, and `ValidateCameraView` drops back to FRONT rather
   than showing a black rectangle the crew has to diagnose. Booster cameras keep working after
   separation because `BoosterRecovery` already holds the booster loaded.
3. **The column is not infinite.** Four buttons fitted by inspection; a craft with six more would
   run the list out through the tab bar — the same failure a cockpit photo showed for AUTO DOCK.
   `CamSlots` derives the count from the space and anything that does not fit is not drawn.

The layout sweep now sweeps the **full** column rather than the four it used to, and passes the same
count to the hit test. That immediately caught the hit path still answering for four buttons while
eleven were painted — a button bound to nothing wearing the shape of one that works. 2,212 checks
went to 2,481.
