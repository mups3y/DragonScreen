# Operating procedure

What to press, when, and what the vehicle will do on its own. Everything here is checked against the
code — where a control is refused or a phase is skipped, the reason is the actual guard.

---

## 0. Before a test flight

**Restart KSP fully if the DLL changed.** `build.py install` prints `KSP needs a FULL RESTART` when
it replaced the binary. A scene reload is not enough; you will be flying the old code and the log
will look inexplicable.

The flight recorder starts by itself when the autopilot engages and writes
`DragonScreen_capture/flight_<MMDD_HHMMSS>.csv` at 5 Hz. Nothing to switch on.

---

## 1. Launch

| # | Do this | What should happen |
|---|---|---|
| 1 | Roll out to the pad and stop. Vessel state `PRELAUNCH` | — |
| 2 | Enter IVA (`C`) | The three screens light up |
| 3 | Chrome bar → **FLIGHT** (leftmost of FLIGHT / VEHICLE / NAV / DOCKING / SETTINGS) | — |
| 4 | Press **AUTO SEQUENCE**, the wide button low and centred on the page | Label changes to `AUTO VERTICAL RISE` |

That is the whole launch procedure. One button.

**From here you may leave IVA.** The autopilot, the booster recovery and the recorder run from
`FlightDriver`, a flight-scene addon, so external and map view are both safe. This was *not* true
before 2026-08-10 — everything used to tick from the IVA screens.

### What it flies, unattended

```
VERTICAL RISE → GRAVITY TURN → MECO → BURN TO APOAPSIS → COAST → CIRCULARISE → disengage
```

MECO issues the stage command itself at a 60 km apoapsis, with propellant still in the booster.
Target orbit is 86 km on heading 90 — the Space X Station's orbit. On the 2026-08-10 21:01 flight
this produced **86.0 × 83.8 km at 0.134° inclination** and disengaged on its own.

### While it is flying

- **Do not touch pitch, yaw or roll.** Any axis past 0.2 disengages the autopilot with
  `manual input`. That is deliberate — it hands the vehicle back rather than fighting you — but it
  means a bumped stick ends the ascent.
- **Do not touch the throttle.** It is written every frame.
- **Leave SAS alone.** The attitude controller turns it off on purpose; the two would fight over the
  same three axes.
- Time warp is untested with the autopilot engaged. Don't, yet.

### If you need to take over

Nudge the stick. It disengages cleanly, zeroes the throttle and gives the axes back.

---

## 2. After insertion

The autopilot disengages itself and logs `insertion complete`. If it stopped for any other reason
the log line is the actual reason, not that phrase — that distinction cost two flights to learn.

**Do not press AUTO SEQUENCE again.** It is an *ascent* autopilot. Pressing it in orbit used to run
the whole state machine from the beginning: on 2026-08-10 that happened four times in four minutes
and produced attitude errors of 45°, 99°, 112° and 134°, plus a stage command on an orbiting
capsule. It is now refused, with a log line, whenever periapsis is already above the atmosphere —
but there is nothing useful behind the refusal either.

---

## 3. The lower console

Every command is **arm, then execute**:

1. Press the command — `DEORBIT NOW`, `FIRE PYRD`, `MAINS ONLY`, …
2. Press **EXECUTE**

`CANCEL` clears an armed command. `EXECUTE` with nothing armed lights a red refusal; `CANCEL` with
nothing armed is a silent no-op, because that is the safe thing a crew member does when unsure.

Wired and working: nose cone, chutes (mains / drogues+mains / cut), trunk pyros, both power buses
and all six strings, the fault responses, `DEORBIT NOW`, `WATER DEORBIT`, `BREAKOUT`, `ABORT`.

`DEORBIT NOW` and `WATER DEORBIT` refuse on the ground and refuse if periapsis is already at or
below the target — they will say which in the log.

---

## 4. What is NOT built

Do not wait for these; nothing will happen.

- **Rendezvous, docking, refuelling** — the guidance exists in `pure/`, unwired and unflown.
- **Closed-loop entry guidance** — see `docs/F9I_PORT_MAP.md`.

## 5. Booster recovery — both vehicles fly at once

**Recovery is taken as soon as the booster exists**, seconds after MECO, which is what gives
boostback its window. The camera follows the booster down; **the upper stage keeps flying itself the
whole time** and continues to orbit without you.

This is F9I's architecture, not a workaround. KSP simulates every *loaded* vessel and calls each
one's own control callback whether or not the camera is on it — F9I runs `BOOSTER.ks` and
`F9_payload.ks` as two CPUs and says so plainly: *"Focus → Booster for landing. The upper stage
circularizes on its own."* We now do the same with one controller instance per vehicle.

Expected sequence after MECO:

```
booster:      BOOSTBACK → COAST → ENTRY BURN → DESCENT → LANDING BURN → TOUCHDOWN
upper stage:  BURN TO APOAPSIS → COAST → CIRCULARISE → insertion       (concurrently)
```

When the booster is down, focus returns to the upper stage. That is a camera move — it never
stopped flying.

**Press `[` or `]` to look at the other vehicle at any time.** It changes nothing about what either
one is doing.

### The one limit that is real

The physics range still clamps. `falcon-physics-range-clamp` measured 297–341 km against the
1500 km requested, on four F9I flights, because PhysicsRangeExtender is not installed. Past that the
far vehicle goes **on rails** and neither we nor F9I can command it — F9I sees exactly this as its
interface CPU rebooting mid-circularisation on every flight.

For an RTLS profile the two vehicles stay well inside 300 km, so this should not bite. If the log
says `upper stage has gone on rails`, that is what happened, and PhysicsRangeExtender is the fix.

### Still unflown

The entry burn, the soft start, the grid fins, the hoverslam and the engine-mode switching **have
never executed once**. Expect the first recovery to find something.
