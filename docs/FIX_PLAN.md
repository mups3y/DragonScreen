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
