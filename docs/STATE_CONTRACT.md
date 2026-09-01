# F9I Dragon screen — state contract v0

> 📜 **HISTORICAL — do NOT build from this file. SUPERSEDED, see `docs/BUILD_PLAN.md` (C7.1).**
> This is the 2026-08-04 F9I/kOS-era contract. **DragonScreen has no kOS dependency, no F9I bridge, and none
> of the ten globals or the `message1/2/3` lines below exist in the mod.** The live contracts are
> `TELEMETRY_REGISTRY.md` (every datum → its one authoritative source) and `COMMAND_REGISTRY.md` (every
> control → its command path), both governed by the plan. Layer 1's advice — don't hand-write an accessor
> for a vessel/target number someone has already debugged — is the one idea here that outlived the design.

The first artefact, and the expensive one to get wrong. **Almost none of it needs to be invented** —
both halves already exist and were extracted rather than designed.

---

## Layer 1 — generic vessel data: DO NOT WRITE THIS. MAS ALREADY HAS IT.

`assets/reference/AvionicsSystems-master/Source/MASFlightComputerProxy{,2,3}.cs` expose
**787 public variable methods**, MIT licensed, working in KSP 1.12:

    Proxy.cs   290    Proxy2.cs  226    Proxy3.cs  271

Altitude, velocity, apsides, resources, engine state, attitude, target relative geometry, docking
alignment, manoeuvre nodes, closest approach — all named, all implemented, all debugged over years.
A sample of what is already there on the docking side alone:

    DockConnected · DockReady · Docked · DockedObjectName · HasDock · GetTargetDockIndex
    PitchDockingAlignment · RollDockingAlignment · PitchTarget · PitchTargetPrograde
    ManeuverNodeTargetClosestApproachDistance / Speed / Time · ManeuverNodeRelativeInclination

**Rule: if the screen needs a number about the vessel or the target, look here FIRST.** Writing our own
accessor for anything in that list is wasted work and a second thing to keep correct.

---

## Layer 2 — F9I mission state: EXTRACTED FROM THE kOS, NOT DESIGNED

This is the half MAS cannot know about, because it is our own flight logic. It was measured out of the
live scripts rather than imagined, so it describes what the interface actually publishes today.

### 2a. Mission-state globals — the whole set is ten names

| Global | Meaning | Owner |
|---|---|---|
| `runningprogram` | which program is executing ("None" when idle) | falcon9.ks |
| `shownPage` | which of the 9 page stacks is visible | falcon9.ks |
| `dgPhase` | de-orbit/entry phase string | dragon_deorbit.ks |
| `stPhase` | station-ops phase string | station_ops.ks |
| `dockingmode` | GATE / INTMD / APPR / DOCK | falcon9.ks |
| `fdGO` | flight director GO/NO-GO on booster recovery | F9boosterTelemetry.ks |
| `fdLandProfile` | 1 RTLS / 2 ASDS / 3,6 expendable | BOOSTER.ks |
| `ShipType`, `ShipSubType` | variant | falcon_detect.ks |
| `missionTimer`, `MissionName` | mission clock and label | falcon9.ks |

### 2b. The three message lines ARE the contract, and they dominate everything else

    message1   383 references
    message2   349
    message3   363
    launchlabel 79 · landlabel 61 · statuslabel 12
    DgStatus() 29 calls · DgStatusLabels() 19 calls

**~1095 writes to three text lines.** That is not incidental — it is how every program in this project
already talks to the user, and it is the single most load-bearing element to carry across. The Dragon
screen must have a home for these three lines that is at least as readable as the current window, or
the port is a regression no matter how good the gauges look.

`DgStatus` / `DgStatusLabels` are the structured version of the same idea (title / action / detail),
already used 48 times on the de-orbit and station paths. **That three-field shape is the contract** —
adopt it rather than inventing a new one, and route `message1/2/3` into it.

### 2c. Values with no home in the current GUI — the capability gain

Recorded because the screen can show them and the kOS window cannot:
- **ALERTS.** HUDTEXT warnings are currently thrown away after their timeout. Nothing keeps a history.
- Black-box slots `x1..x4`, which change meaning per phase and are currently only readable post-flight.
- The flight director's reasoning on a NO-GO (it logs, it does not display).

---

## Layer 3 — commands the screen can send

Everything reachable from the old window, or Crew Dragon loses it:

- 9 page stacks: flight, settings, cargo, attitude, status, orbit, engine, crew, maneuver
- LAUNCH · DE-ORBIT & LAND · the EXECUTE/CANCEL bar that `confirm()` drives
- Full Flight Settings (hide-GUI, black box, Dragon landing mode, telemetry scale, log data)
- The scale dialog

---

## The bridge

A **kOS addon**, so scripts read `ADDONS:F9I:...`. This is the mechanism Trajectories, SCANsat and
MechJeb all use, and **all three are installed on this rig**, so the pattern is proven here. Do not
invent a file- or message-based channel.

Direction matters and is asymmetric:
- **Plugin → kOS:** which button was pressed. Small, event-shaped.
- **kOS → plugin:** the ten globals plus three message lines, at repaint rate. Everything else the
  screen needs, it should read from MAS/KSP directly rather than round-tripping through kOS — that is
  the whole point of layer 1, and it keeps the bridge narrow.

---

## Open, needs a decision

1. **Does the screen read vessel data directly (MAS-style), or does kOS push everything?** Direct is
   faster and keeps the bridge small, but it means two sources of truth for anything kOS also computes.
   Recommendation: direct for raw vessel data, kOS-pushed for anything kOS *derives* (aim, phase,
   GO/NO-GO, predicted miss). Never both for the same value.
2. **Repaint rate.** The kOS GUI cost 68–77% of real time; the whole point of moving is to stop that.
   Pick a rate and hold it.
3. Whether `message1/2/3` keep their exact current semantics or get re-scoped to the DgStatus triple.
