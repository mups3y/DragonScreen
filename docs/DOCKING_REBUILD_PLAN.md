# Docking and rendezvous — the plan

Written 2026-08-13 after reading MechJeb's docking and rendezvous autopilots end to end. Nothing here
is written yet.

## The decision that shapes everything: keep the PROFILE, replace the INNER LOOP

MechJeb's docking autopilot is **generic KSP docking**. It has no keep-out sphere, no approach
corridor and no hold points. The real Crew Dragon has all three, and this project's north star is
that the screens "look and act like the real Crew Dragon". A straight MechJeb port would fly
correctly and stop being a Dragon.

So the split is:

| keep (ours / Crew Dragon) | replace (MechJeb's, because ours has never worked) |
|---|---|
| keep-out sphere, measured from the station | the inner control loop: **velocity**, not per-axis thrust |
| the gate on the port axis | commit tolerance: **1 m** of lateral, not 15 |
| the corridor run | time-balanced final approach |
| the monotone stage machine | explicit wrong-side handling |
| the speed ladder | sizes from bounding boxes and the port's own `acquireRange` |

## What is actually wrong, measured

**We commit to the axial run 15x too far off-axis.** `DockGeometry.GateToleranceM = 15` and
`StandoffToleranceM = 12`; MechJeb's `dockingcorridorRadius = 1`. Starting a final approach 15 m off
the axis and then trying to null it while closing is the whole failure.

**Nothing couples the closing rate to the lateral error.** MechJeb slows the approach until the
lateral can be nulled in time:

    timeToAxis       = |lateralSep| / latApproachSpeed
    timeToTargetSize = |zSep|       / zApproachSpeed
    if (zSep <= lateralSep*10 || timeToTargetSize <= timeToAxis*10)
        zApproachSpeed *= min(timeToTargetSize / timeToAxis, 1)   // slow the closure
        latApproachSpeed = FixSpeed(latApproachSpeed * 2)          // and push the lateral harder

Ours pushes fore regardless of lateral. That is how 97.8 units went into a stationary capsule.

**We command thrust; MechJeb commands velocity.** `SetTargetWorldVelocity(targetVel + adjustment)`
puts the station's own motion in the setpoint, so the controller is never fighting orbital drift —
it only ever solves for the *difference*. Ours re-discovers the drift as an error every tick.

**No wrong-side handling.** MechJeb has four states for "our port is behind theirs". We have none, so
being behind the port is indistinguishable from being in front of it.

## Order of work — and STOP when docking works

⛔ ONE CHANGE PER TEST FLIGHT. Phase 1 may fix docking outright and make 2 and 3 unnecessary. Do not
start a port because it is on this list.

### Phase 0 — fly what is already fixed. NO NEW CODE.

Two fixes are installed and unflown: the RCS one-way torque and the phasing periapsis floor. Until
they fly, nothing after them can be attributed.

*Proves:* booster `FLIP` roll well under 759 deg with `b_actR` off the rail; `rendezvous ENGAGED`
followed by a phasing lap instead of 4515 refusals.

### Phase 1 — the docking inner loop. ~150 lines, no new dependencies.

In order, each independently testable headless:

1. **Commit tolerance 15 m -> 1 m.** One constant, plus the standoff tolerance that pairs with it.
   The riskiest single line here, because a tight corridor with a loose controller never converges —
   so it lands WITH item 2, not before it.
2. **Time-balanced final approach**, transcribed from `MechJebModuleDockingAutopilot.cs:203-213`.
3. **Wrong-side states** (`:296-306` and `:353-370`), using our own port geometry.
4. **Sizes from the vehicle**: `targetSize` from the station's bounding box, `acquireRange` from the
   target `ModuleDockingNode.acquireRange * 0.5`, replacing the constant 25 m standoff.

*Proves:* `x_dkStage` reaches `Docked`; `x_dkDistS/T` under 1 m before `x_dkDistF` starts falling;
monopropellant spent on the docking under 20 units.

### Phase 2 — velocity control. Only if Phase 1 leaves it twitchy.

Port `MechJebModuleRCSController.SetTargetWorldVelocity` (269 lines, we need the tracking core, not
the throttle plumbing) and drive `DockControl` from a velocity setpoint that already contains the
station's motion.

*Proves:* `x_dkVelF/S/T` track their commanded values with the RCS duty cycle falling.

### Phase 3 — the rendezvous maneuver core. A real port. Do not start it casually.

MechJeb closes phase with a **Hohmann transfer whose departure time is solved for**, waiting up to 5
orbits for the window. We force the phase by lowering the orbit for a fixed lap count — which is
exactly what put periapsis under the atmosphere on 2026-08-13. Our fix (spend laps, not altitude)
reaches the same place from the other side and may be enough.

Scope, if it is ever needed:

| file | lines |
|---|---|
| `OrbitalManeuverCalculator` (the 3 functions the ladder uses) | ~120 of 523 |
| `Gooding` — the Lambert solver | 565 |
| `TwoImpulseTransfer` | 252 |
| `ChangeOrbitalElement` | 215 |
| `Shepperd` — two-body propagation | 365 |
| **`V3`** | 481 |

⚠ **This reverses a decision.** `docs/MECHJEBLIB_PORT.md` deliberately SKIPS `V3` to keep
"`src/pure` has no vector type". Every one of these files is built on `V3`, so Phase 3 spends that
decision. That is a reason to be sure Phases 1 and 2 were not enough before starting.

Plus KSP glue: `Orbit.RightHandedStateVectorsAtUT` and `V3ToWorld`, which are the coupling points.

## What this does NOT change

The rendezvous ladder classification (`>3 km phasing, 0.5-3 km CW, <0.5 km RCS`) is F9I's and is
flight-proven. The periapsis floor stays. `DockControl` — the ported `GNC.ks:1190 DockGNC` servo with
its braking curve and authority mixing — stays; Phase 2 changes what it is *given*, not what it is.
