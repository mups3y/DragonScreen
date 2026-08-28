# Booster recovery dual-flight — research + the "our PRE" design

> **Why (2026-08-29):** the #1 named build gap was flying the booster to a landing WITHOUT sacrificing the
> Dragon-to-orbit. The old `MissionConductor` used `ForceSetActiveVessel(booster)` and accepted the Dragon
> going on-rails (mutually exclusive). This doc records the feasibility research + the design Chris directed.

## 1. The feasibility question — RESOLVED (it is an UNPACK question, not folklore)

KSP fully simulates AND accepts control input (throttle + steering, via `Vessel.OnFlyByWire` / `ctrlState`)
for any vessel that is **UNPACKED** — within the *unpack* range of the active vessel. Beyond it a vessel goes
**ON-RAILS** (no physics, no control). This is confirmed:
- **kOS** documents that "KSP limits some features (like throttle control) to only vessels that are UNPACKED."
  [Vessel Load Distance — kOS](https://ksp-kos.github.io/KOS/structures/misc/loaddistance.html)
- **BDArmory** flies MANY non-active craft simultaneously by hooking each vessel's `OnFlyByWire` — living proof
  that OnFlyByWire drives a non-active *unpacked* vessel. [BDArmory AI / OnFlyByWire](https://forum.kerbalspaceprogram.com/topic/217362-wip-making-bdarmory-ai-work-with-atmosphere-autopilot/)
- **MechJeb** registers `OnFlyByWire` on its own part's vessel (`MechJebCore.cs:311–470`) — the callback is the
  control path; whether it *fires* depends only on the vessel being unpacked.

**⇒ So the booster CAN be flown while the Dragon flies, IF the booster stays UNPACKED — i.e. within the widened
physics range.** That is exactly what PhysicsRangeExtender does. The remaining risk is PHYSICS, not API (§4).

## 2. PhysicsRangeExtender — the method we port ("our PRE")

PRE (jrodrigv, from BahamutoD's BDArmory code) widens every vessel's `VesselRanges`. Ported verbatim from its
source (`PhysicsRangeExtender.ApplyRangesToVessels`):
```csharp
// for range R metres, every one of the 7 situations gets:
new VesselRanges.Situation(load = R, unload = 1.05·R, pack = 1.10·R, unpack = 0.99·R);
// then: vessel.vesselRanges = new VesselRanges(base);   // for every loaded vessel
```
Within ~R of the active vessel a booster stays **loaded + unpacked** (physics + OnFlyByWire-controllable).
Restore stock ranges from `PhysicsGlobals.Instance.VesselRangesDefault`. Built as `src/RangeExtender.cs` — a
soft port, NO hard dependency on the PRE mod. Source: [PhysicsRangeExtender](https://github.com/jrodrigv/PhysicsRangeExtender),
[KSP VesselRanges API](https://kerbalspaceprogram.com/api/class_vessel_ranges.html).

## 3. The design Chris directed (2026-08-29) — `MissionConductor.TickBoosterRecovery`

1. **PRE ON before booster separation** (arm while the pod-carrying stack is airborne in ascent).
2. **Focus the separated booster** → `BoosterControl` flies it down (flip → entry burn → grid-fins → hoverslam
   → legs). The upper stage is now NON-active → it **COASTS**, kept LOADED by PRE (it does not vanish/on-rails).
3. **Return focus to the upper stage** once the booster is Landed/Splashed → its ascent RESUMES.
4. **PRE OFF** (restore stock ranges).

**Why the separation stays small (~hundreds of km, not thousands):** because the upper stage COASTS during the
recovery (it is not accelerating to orbit while non-active), it stays near the booster's region. The flight
corpus shows the S2/Dragon reaches **6500 km** downrange when it burns to orbit — so keeping both unpacked to
the booster's landing is only feasible *because* the S2 coasts. `PreRangeKm` = max booster↔upper-stage
separation during the recovery window + margin (Chris: "say it's 500 km → set 600 km"). Default **600 km**.

## 4. ⚠ The real risk — phantom forces (physics), and the open flight-verify items (H1)

PRE's own README warns: extending the range beyond ~100 km causes **"vessel shaking, lights flickering, phantom
forces, landed vessels colliding with the ground."** Our booster lands ~500 km downrange, so the recovery runs
deep in that regime — a threat to a **precision hoverslam**. Mitigations if a flight shows it: raise
`FloatingOrigin.threshold` (PRE also does this — a v2 refinement, currently omitted to keep the change minimal),
and/or keep `PreRangeKm` only as wide as measured.

**Flight-verify (tick-3, H1):** (a) PRE keeps both craft loaded+unpacked through the recovery; (b) the focus
switches fire (booster after sep, back to the upper stage after landing); (c) the **upper stage survives the
coast + resumes its ascent** to orbit after refocus (the biggest unknown — a long coast from a suborbital MECO
may not recover; fallback = drive the non-active S2 via a second `OnFlyByWire` hook so BOTH fly); (d) the
booster lands on the droneship despite phantom forces; (e) size `PreRangeKm` from the measured max separation.

## 5. Data note
The FlightRecorder CSV follows the ACTIVE vessel only (one vessel per file, switching on focus change), so the
booster↔upper-stage separation is NOT directly in a single recording. The max downrange the S2 reaches (6500 km,
`Crew-2_20260828_020539.csv`) is recorded; the pairwise separation must be measured in-flight (a two-vessel
range readout is owed instrumentation). Until then `PreRangeKm=600` is the design assumption to confirm.
