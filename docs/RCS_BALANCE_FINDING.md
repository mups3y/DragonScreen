> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-29; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# RCS-balance (B3 / task T5) — the finding: diagnostic-only, not stock-actuatable

**Date:** 2026-08-29 · **Status:** assessed, wired as a recorded diagnostic; per-thruster *application* deliberately deferred with evidence.

## The question
`pure/RcsBalance.cs` (B3) solves for the per-thruster activation set that produces a demanded **translation while
nulling net torque** — important on a no-reaction-wheel vehicle whose 16 Dracos do both attitude and translation,
so firing the wrong set to translate induces a rotation. Task T5 was to wire it for rendezvous/docking.

## The finding — it cannot be *applied* through stock KSP on this vehicle
Verified against the live craft dump (`data/craftdump.csv`, `TE.18.DRAGONV2.POD`):

1. **The Dracos are exposed as 2 `ModuleRCSFX` blocks**, each `thrusterPower = 2 kN`, each with **all axes enabled**
   (`enablePitch/Yaw/Roll/X/Y/Z = True`) and **multiple `thrusterTransforms`** per module. The only per-module lever
   is `thrustPercentage` (one scalar per block).
2. So there is **no per-thruster actuation lever** in stock KSP. `RcsBalance` computes a limit *per thruster*, but
   the vehicle can only scale a whole all-axis block — which cannot selectively null a translation-induced torque
   (the torque comes from *which direction* fires, chosen by KSP's own RCS solver from the command, not from us).
3. **KSP's RCS solver + the concurrent `AttitudePilot` attitude hold already close the loop** on the induced torque:
   during prox-ops the attitude loop actively holds attitude with the same Dracos, so a translation-induced rotation
   is corrected in closed loop every tick. The balancer's open-loop torque-nulling is largely feed-forward-redundant
   with that.

Applying the per-thruster solve would require **direct force injection** (`part.AddForceAtPosition`, bypassing stock
RCS) — invasive, fights KSP's own solver, and risky on a crewed vehicle. Not justified when the attitude loop already
holds the loop closed. **Deferred** as a separate, larger decision.

## What was wired instead — the evidence-collecting diagnostic (T5, commit-tracked)
`Actuator.RcsInducedTorque(v, demandCtrl, …)` runs the **tested pure `RcsBalance`/`ThrustBalance` live** during
prox-ops whenever an RCS translation is commanded, and records (via `FlightRecorder.PutRcsBalance`, columns
`rcsbal_torque_naive` / `rcsbal_torque_resid` / `rcsbal_force_frac`):
- **naive** — the net torque (N·m) the commanded translation induces with the naive full demand-serving set (what
  the attitude loop must fight);
- **resid** — what a torque-nulling per-thruster balance would leave;
- **force_frac** — the fraction of the translation that survives the balance (the cost of nulling).

It is **records-only** (no actuation, zero flight risk), gated to when translation is actually commanded, and reuses
cached effector arrays to stay light. This turns the deferral into a **data-backed** decision: a flown CSV will show
whether the induced torque is large enough (and cheap enough to null, `force_frac`) to justify the direct-injection
path — or confirm the attitude loop already handles it and no application is warranted.

## Decision
- ✅ Keep `pure/RcsBalance` + `pure/ThrustBalance` (headless-tested) as the ready solver.
- ✅ Diagnostic wired + recorded (this is T5's honest "complete as much as it can be").
- ⏸ Per-thruster **application** deferred to a direct-force-injection design **only if** the recorded data shows the
  induced torque materially degrades prox-ops beyond what the attitude loop absorbs.

Cross-refs: [NOMINAL_END_TO_END_BUILD.md](NOMINAL_END_TO_END_BUILD.md) · [CRAFT_DUMP_VEHICLE_MAP.md](CRAFT_DUMP_VEHICLE_MAP.md) · [direct-part-control-hard-rule].
