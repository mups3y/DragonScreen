# Port plan for the rest of the mission

Written 2026-08-11, before any more code, because the waste on this project has not been the porting
— it has been **rewriting things I wrote before their dependency existed, or before I had read the
whole source function.** This plan exists to make both impossible.

Read `docs/MISSION_PLAN.md` for status. This is the *order and the contracts*.

---

## ⛔ RULE 0 OF ALL: A PHASE WITH NO CALLER IS NOT PORTED

Added 2026-08-11, after a dead-code sweep found **four** things written, tested, documented as DONE
in this file and in the port map - and referenced by nothing:

| written and unreachable | what it meant |
|---|---|
| `pure/LaunchWindow.cs` | §1 launch-on-phase could not be used. Arriving at the wrong phase is what made F9I's first ferry "spend 7.3 HOURS phasing" |
| `StationApproach.Engage` | §6 rendezvous had no caller anywhere in the plugin |
| `UndockOps.Engage` | §7 undock had no caller either - and its top-up moved no propellant, so the refuel had never once happened |
| `pure/DockControl.cs` | the ported `DockGNC` velocity servo, unused, while `DockingOps` flew bang-bang translation I had invented |

Every one of these looked finished from every angle except the one that matters. **Wire it the same
day you write it, or the row here stays open.** The sweep that found them is worth re-running:
list every public member of `src/pure` and `src/`, and flag any whose name appears in no other file.

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

## R1 · Phasing leg — **DONE 2026-08-11**

**Read:** `StPhaseLeg:932`, `StAlongTrack:894`, and the ladder tuning block at `:620-645` again for
`stPhaseOrbits`.

**Contract:** pure returns a target semi-major axis and the Δv at apoapsis/periapsis to reach it;
glue hands that to F1. **Must not be able to lower periapsis** — closing a forward gap means raising
the orbit, and the cost table shows a phasing orbit "cannot drop periapsis when we are ahead".

**Currently:** `StationApproach.FlyPhasing` reports the gap and holds. Replace that body only.

## R2 · Match station orbit — **DONE 2026-08-11**

**Decided:** `StMatchStationOrbit`. `MatchPlanes`/`MatchSMA` are marked DEAD in their own source
("do not wire this one back in by mistake because the name reads right") and drag in an unported
Starship toolchain. No plane match: the station is at 0.133° and the plane is degenerate.

---

## F2 · Docking port selection — **DONE 2026-08-11**

**Read:** `StClosestPort:369`, `StRelVel:737`, `StCloseIn:1689`, `StApproachTo:1597`.

**Contract:** given our ports and the station's, return the pair to use and the approach axis
(the port's outward normal). `pure/DockControl.cs` exists unwired — read it first.

**⚠ `falcon-station-ferry` and `falcon-blackbox-reading-docking`:** the station was **measured** at
86.8 × 85.8 km, inc 0.133°, and the berths sit at the tips of arms rather than on the hull, so the
keep-out sphere is centred on the station and the approach must slide round it rather than drive at
it. `falcon9.ks:10704-10760` has that geometry worked out — read it, do not re-derive it.

## D1 · Dock · D2 · Refuel · D3 · Undock — **ALL DONE 2026-08-11**

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

### E2 · Phase into the deorbit orbit — **DONE 2026-08-11**
**Read:** `StPhaseToDeorbitOrbit:2560`, constants at `station_ops.ks:78-80`. Needs F1.

`pure/DeorbitOrbit.cs` + `src/PhaseDownOps.cs`. Two Hohmann half-burns onto 85.1 × 79.2 km through
the node executor. **`DeorbitOps.Engage` now runs it first** — every aim constant in `pure/Deorbit.cs`
was fitted from that orbit, so de-orbiting from the station's 86.8 × 85.8 hands them an entry energy
they do not describe.

**⚠ The trap that is not the maths:** the first station return planned both burns with **nothing
lit**, so the executor fell back to RCS and pushed the wrong way — 120.3 × 119.5 became 159.1 × 138.0
and spent 34.5 units of monopropellant. `PodEngines.Available` is the check, and `PhaseDownOps` waits
on it rather than assuming ignition worked.

### E3 · Find an overflight — **DONE 2026-08-11**
**Read:** `DgFindOverflight:856`, `DgSiteInertialAt:805`, `DgLandLag:821`, `DgOffPlaneAt:839`,
`DgTrackMissAt:847`, `DgGCDist:566`, `DgBearing:573`. Pure, no burns — a search over time.

`pure/Overflight.cs`. Coarse 60 s sweep over 5 orbits, then refine 5 → 1 → 0.2 s.

**⛔ The site is evaluated at TOUCHDOWN, not at the overflight.** That single distinction is
`CargoDragon_070`'s 55 km. And the lag is **calibrated (0.358 of a period), not derived** — the
plausible derivation gives 366 s, flights 072/074 flew it, and the cross-track went from +53 km
straight through zero to −64 km.

### E4 · Plane match — **DECIDED 2026-08-11: PORT THE MEASUREMENT, NOT THE BURN**
**Read:** `DgPlaneMatch:889-1000` in full, plus the `dgPlaneChangeEnabled` declaration at `:33` and
the file header at `:26-33`.

**⛔ F9I DOES NOT FLY THIS BURN, AND THE PLAN WAS WRONG TO ASSUME IT DID.** Three findings, all from
the source itself:

1. **It is hard-gated off.** `global dgPlaneChangeEnabled is false`, with the reason in the header:
   flights **082–100 flew the plane change and missed by 54–262 km**. `DgPlaneMatch` solves the
   geometry, logs it, and returns before `DgPlaneNodeBurn`.
2. **Its own geometry is unresolved.** The source carries an open DIAG block: `dgTheta` (off-plane
   from the normal at the overflight) and `dgRotS` (from the normal now) are the same angle by
   construction and disagree by **3.6× to 26×** across four flights. Its author wrote *"DO NOT 'fix'
   this by making one call the other until that is known"*. Porting a burn driven by a number the
   source says may be fiction is exactly the failure this plan exists to prevent.
3. **`DgPlaneNodeBurn` hands the burn to MechJeb's node executor** after five hand-flown attempts
   failed. `mechjeb-kos-binding-limits` rules that out for us.

**And it does not cost us a landing.** Our station is at **inclination 0.133°** — the plane is
degenerate. The source records the equatorial case directly: off-plane reads up to 1.5°, the gate
fires, no burn happens, *and the capsule lands at 331 m anyway* because the entry lift walks the
cross-track off (flight 080 touched down at **−5 m** of cross).

**Ported:** `DgSiteInertialAt`, `DgLandLag`, `DgOffPlaneAt`, `DgTrackMissAt` — the measurement, in
`pure/Overflight.cs`, reported to the crew and the recorder. **Not ported:** the burn.
Re-open only with flight evidence that the residual cross-track is not being absorbed by the entry.

### E5 · Deorbit burn — **DONE 2026-08-11**
**Read:** `DgDeorbitBurn:1328`, `DgPhasing:1306`, `DgUseS2Deorbit:1901`, `DgS2DeorbitToPeri:1568`,
`DgRcsDeorbit:1754`. Needs F1.

**⚠** `FlightCommands.StartDeorbit` is currently a plain retrograde burn with no target periapsis
solve. It gets **replaced**, not extended.

### E6 · Trunk and entry interface — **DONE 2026-08-11**
**Read:** `DgSepStack:1859`, `DgPreEntryTrim:1910`, `DgCoastToEI:1970`, `DgTrunkAndEI:1995`,
`DgCapsuleTrim:1408`.

`src/EntryOps.cs` stages `Separating` → `CoastToInterface` → `Trimming`. One decouple, taken in the
**retrograde** attitude so the trunk goes prograde and the capsule is already pointed for what
follows. The trim runs on RCS **translation** with steering still locked shield-forward, and the
landing propellant reserve outranks the range every time.

**⚠ `falcon-dragon-two-decouplers`:** `TE.19.C.Dragon.Decoupler` drops the S2 alone; only the TRUNK
decoupler takes everything below it — and a comment in `dragon_deorbit.ks` itself says the opposite
and is wrong. We already have this right in `VehicleParts`; do not let the source's comment undo it.

### E7 · Lifting entry — the bank controller — **DONE 2026-08-11** (law and glue)
**Read:** `DgEntryGuidance:2109-2356`. 247 lines, the second-largest read. Also `DgSetProfileAngle:680`,
`DgSetProfile:733`, `DgImpactMiss:601`, `DgDownCross:605`, `DgAimPoint:586`, `DgAimMiss:590`.

**Consumes:** `pure/EntryMargin.cs` (done — the measured long-margin table).
**⚠ `falcon-dragon-entry-solution`:** the flown law is "shorten-only + lead". Check `x1` first on any
new recording. `pure/Entry.cs` was NOT replaced after all: it answers a different question - which way the heat shield points per altitude band, carrying the CargoDragon_012 lesson - and composes with the new range controller rather than duplicating it. Noted in both headers so nobody merges them.

### E8 · Terminal descent — **DONE 2026-08-11**
**Read:** `DgTerminal:2411`, `DgTerminalParachute:2356`, `DgTerminalPropulsive:2372`,
`DgRecoveryMain:2426`.

`pure/Terminal.cs` + the terminal stages of `src/EntryOps.cs`. Mode chosen on **capability**, and the
crew are told which of the three conditions failed. The propulsive path lights the SuperDracos
**under** the drogues and cuts only once thrust is proven — a headless check sweeps every stopping
distance to prove the burn gate can never be reached before the arm gate. Gear after touchdown, never
on a splashdown.

---

## Deliberately not porting

Recorded so nobody spends a session discovering why they cannot.

| thing | why |
|---|---|
| Trajectories impact prediction | third-party dependency. **Replaced 2026-08-11 by our own**: `pure/Trajectory.cs` is an RK4 integrator through the real atmosphere, and `src/ImpactPredictor.cs` MEASURES each vehicle's ballistic coefficient from its own telemetry rather than modelling drag. The drag-free fallback remains for vehicles that have not been measured yet, and says which it used. |
| kOS steering-manager knobs — `rollts`, `torqueepsilon*`, `pitchts`, per-axis `ki` | our cascade does not expose them; `MaxStoppingTime` is the one that carries across |
| `BBSet` / `BBMark` black-box scratch columns | our recorder has named columns instead — same purpose, better shape |
| warp management | the recorder now logs `warp` so a bad row is identifiable; automating warp is a separate question |
| ASDS boostback branch | RTLS only until RTLS lands |

---

## Order of work

1. ~~**F1** node executor~~ **DONE** — `pure/BurnExec.cs` rewritten as a port, `src/NodeExecutor.cs` is the glue, CW burns now fly through it
2. ~~**E1** return budget~~ **DONE** — `pure/ReturnBudget.cs`, 19 checks. Not yet on a page or a button
3. ~~**R1**, **R2**~~ **DONE** — §6 complete: match-orbit, phasing, CW, ladder, terminal, all wired
4. ~~**F2**, **D1**~~ **DONE** — `pure/DockGeometry.cs` + `src/DockingOps.cs`, 15 checks
5. ~~**D2**, **D3**~~ **DONE** — `src/UndockOps.cs`; §7 complete
6. ~~**E2**, **E3**, **E4**, **E5**~~ **DONE** — phase-down, overflight search, plane MEASUREMENT
   (not the burn — see E4), de-orbit burn
7. ~~**E6**, **E7**, **E8**~~ **DONE** — `src/EntryOps.cs` is the whole return sequence, and
   `DeorbitOps` hands to it automatically the way `DgRecoveryMain` does

**§8 is complete. The port is complete.** What remains is not porting — it is flying it. See
`docs/MISSION_PLAN.md` for what is proven versus merely written, and its "known gaps" table for the
three things left out on purpose.

**One test flight per numbered group, not per item.** Group 1–2 need no flight at all: they are pure
plus a headless check.
