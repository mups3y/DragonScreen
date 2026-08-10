# Port plan for the rest of the mission

Written 2026-08-11, before any more code, because the waste on this project has not been the porting
— it has been **rewriting things I wrote before their dependency existed, or before I had read the
whole source function.** This plan exists to make both impossible.

Read `docs/MISSION_PLAN.md` for status. This is the *order and the contracts*.

---

## The three rules this plan enforces

1. **Read the whole function before writing a line of it.** Every item below names the exact
   functions and line numbers. If the read turns up a dependency not listed here, **stop and update
   this file first** — that is the signal that the plan was wrong, and discovering it in code is how
   rewrites start.
2. **Foundations before consumers.** The node executor is needed by five separate items. Writing any
   of them first means writing a burn mechanism twice and throwing one away. It goes first.
3. **The interface is decided here, not while writing.** Each item states what it consumes and what
   it exposes. A caller written against the contract below cannot need rewriting when the callee
   lands.

---

## Dependency graph

```
        F1 node executor ──┬── R1 phasing ──┐
                           │                ├── §6 rendezvous complete
        F2 port selection ─┼── R2 match orbit┘
                           │
                           ├── D1 dock ── D2 refuel ── D3 undock      §7
                           │
                           └── E2 phase ── E4 plane ── E5 deorbit ──┐
                                                                    ├── §8
              E3 overflight ── E6 trunk+EI ── E7 entry ── E8 chutes ┘
```

`E7` also consumes `pure/EntryMargin.cs`, which is already ported.

---

## F1 · Node executor — **DONE 2026-08-11**

Five items are blocked on it: R1, R2, D3, E4, E5. Every one of them needs "turn to a vector and burn
a given Δv accurately", and every one would otherwise grow its own.

**Read:** `station_ops.ks` `StExecNode:2437`, `StBurnNode:2469`, `StVisViva:2427`,
`StNodeBasis:741`, `StNodeSafe:906`, `StProNode:922`. About 150 lines total.

**Contract**

| | |
|---|---|
| pure | `pure/BurnExec.cs` — already exists and is unwired; **read it before extending it** |
| consumes | Δv vector, current mass, available thrust, Isp |
| exposes | `BurnPlan { StartUt, DurationS, Throttle(elapsed), Done }` and a settle/ullage flag |
| glue | `NodeExecutor.Execute(vessel, dvWorld, whenUt)`; steers via `AttitudeController`, throttles via its `Throttle` field |
| refuses | any burn whose result breaches the periapsis floor — `StNodeSafe` is that check, port it with the executor and not later |

**Watch for:** `StBurnNode` almost certainly handles the burn-time/half-burn-lead question (start
early by half the burn duration so the impulse straddles the node). If it does, that is the part
that must not be "simplified".

---

## R1 · Phasing leg

**Read:** `StPhaseLeg:932`, `StAlongTrack:894`, and the ladder tuning block at `:620-645` again for
`stPhaseOrbits`.

**Contract:** pure returns a target semi-major axis and the Δv at apoapsis/periapsis to reach it;
glue hands that to F1. **Must not be able to lower periapsis** — closing a forward gap means raising
the orbit, and the cost table shows a phasing orbit "cannot drop periapsis when we are ahead".

**Currently:** `StationApproach.FlyPhasing` reports the gap and holds. Replace that body only.

## R2 · Match station orbit

**Read:** `StMatchStationOrbit:1959`. Also `MatchPlanes:796` and `MatchSMA:816` in `F9_payload.ks` —
two implementations exist and the plan must pick one deliberately.

---

## F2 · Docking port selection

**Read:** `StClosestPort:369`, `StRelVel:737`, `StCloseIn:1689`, `StApproachTo:1597`.

**Contract:** given our ports and the station's, return the pair to use and the approach axis
(the port's outward normal). `pure/DockControl.cs` exists unwired — read it first.

**⚠ `falcon-station-ferry` and `falcon-blackbox-reading-docking`:** the station was **measured** at
86.8 × 85.8 km, inc 0.133°, and the berths sit at the tips of arms rather than on the hull, so the
keep-out sphere is centred on the station and the approach must slide round it rather than drive at
it. `falcon9.ks:10704-10760` has that geometry worked out — read it, do not re-derive it.

## D1 · Dock · D2 · Refuel · D3 · Undock

**Read:** `StRendezvousAndDock:1999`, `StCloseDockingShroud:2291`, `StUndock:2303`,
`StBackAway:2338`, `StTopUpBeforeUndock:2666`, `StMono:1254`.

**Contract:** D2 is a resource transfer and a report, no guidance. D3 needs F1 for the separation
burn and F2 for the axis to back away along.

---

## §8 · Return

### E1 · Is a return even available — **DONE 2026-08-11**
**Read:** `StReturnAllowed:2645`, `StMonoForDeorbit:839`, `StMonoReport:876`, `DgLandingReserve:1895`.
A budget check, no guidance. **Do this before E2–E8**: it is cheap, and it answers "should we undock
at all", which is the question the crew actually needs.

### E2 · Phase into the deorbit orbit
**Read:** `StPhaseToDeorbitOrbit:2560`. Needs F1.

### E3 · Find an overflight
**Read:** `DgFindOverflight:856`, `DgSiteInertialAt:805`, `DgLandLag:821`, `DgOffPlaneAt:839`,
`DgTrackMissAt:847`, `DgGCDist:566`, `DgBearing:573`. Pure, no burns — a search over time.

### E4 · Plane match
**Read:** `DgPlaneMatch:889`, `DgPlaneNodeBurn:1000`, `DgNodeBasis:1106`, `DgPlaneDeltaVAt:1121`,
`DgPlaneDeltaV:1133`, `DgRelNodeUt:1152`, `DgPlaneBurn:1190`. ~300 lines, the largest single read on
this list. Needs F1.

### E5 · Deorbit burn
**Read:** `DgDeorbitBurn:1328`, `DgPhasing:1306`, `DgUseS2Deorbit:1901`, `DgS2DeorbitToPeri:1568`,
`DgRcsDeorbit:1754`. Needs F1.

**⚠** `FlightCommands.StartDeorbit` is currently a plain retrograde burn with no target periapsis
solve. It gets **replaced**, not extended.

### E6 · Trunk and entry interface
**Read:** `DgSepStack:1859`, `DgPreEntryTrim:1910`, `DgCoastToEI:1970`, `DgTrunkAndEI:1995`,
`DgCapsuleTrim:1408`.

**⚠ `falcon-dragon-two-decouplers`:** `TE.19.C.Dragon.Decoupler` drops the S2 alone; only the TRUNK
decoupler takes everything below it — and a comment in `dragon_deorbit.ks` itself says the opposite
and is wrong. We already have this right in `VehicleParts`; do not let the source's comment undo it.

### E7 · Lifting entry — the bank controller
**Read:** `DgEntryGuidance:2109-2356`. 247 lines, the second-largest read. Also `DgSetProfileAngle:680`,
`DgSetProfile:733`, `DgImpactMiss:601`, `DgDownCross:605`, `DgAimPoint:586`, `DgAimMiss:590`.

**Consumes:** `pure/EntryMargin.cs` (done — the measured long-margin table).
**⚠ `falcon-dragon-entry-solution`:** the flown law is "shorten-only + lead". Check `x1` first on any
new recording. `pure/Entry.cs` is OURS and unflown — expect to **replace** it, not extend it.

### E8 · Terminal descent
**Read:** `DgTerminal:2411`, `DgTerminalParachute:2356`, `DgTerminalPropulsive:2372`,
`DgRecoveryMain:2426`.

---

## Deliberately not porting

Recorded so nobody spends a session discovering why they cannot.

| thing | why |
|---|---|
| Trajectories impact prediction | third-party dependency; ours is a drag-free ballistic solve and **predicts long**, which is stated at every call site and is why the boostback overshoot pairs with it |
| kOS steering-manager knobs — `rollts`, `torqueepsilon*`, `pitchts`, per-axis `ki` | our cascade does not expose them; `MaxStoppingTime` is the one that carries across |
| `BBSet` / `BBMark` black-box scratch columns | our recorder has named columns instead — same purpose, better shape |
| warp management | the recorder now logs `warp` so a bad row is identifiable; automating warp is a separate question |
| ASDS boostback branch | RTLS only until RTLS lands |

---

## Order of work

1. ~~**F1** node executor~~ **DONE** — `pure/BurnExec.cs` rewritten as a port, `src/NodeExecutor.cs` is the glue, CW burns now fly through it
2. ~~**E1** return budget~~ **DONE** — `pure/ReturnBudget.cs`, 19 checks. Not yet on a page or a button
3. **R1**, **R2** ← completes §6, makes the rendezvous actually close a gap
4. **F2**, **D1** ← docking, which is the point of the mission
5. **D2**, **D3**
6. **E2**, **E3**, **E4**, **E5**
7. **E6**, **E7**, **E8**

**One test flight per numbered group, not per item.** Group 1–2 need no flight at all: they are pure
plus a headless check.
