> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-28; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.
> ⚠ **Named contradiction:** it designs the **hand-written** control loop that was deleted 2026-09-01. Part B builds a **pinned, privately-namespaced MechJeb embed + a pure conductor** (§B1–B16 / T15–T22) instead.

# Ascent guidance — PVG vs UPFG, the honest decision

> **Why (2026-08-27):** the assessment called PVG "a heavy lift" and the ascent choice unresolved. This is the
> decision, made from reading MechJeb's PVG source (`MechJebLib/PSG/*`) against our working UPFG.

## What each is
- **UPFG (have, flies):** unified powered-flight guidance, linear-tangent single-stage, ported from PEGAS.
  Targets radius/velocity/FPA/plane at cutoff. Reached orbit with a clean `tgo→0` cutoff (flight 214827). Known
  gaps: inclination UNDERSHOOT (46.5 vs 51.6), single-stage, no coast-arc optimization, no explicit q·α limit.
- **PVG (MechJeb `MechJebLib/PSG`):** Pontryagin-optimal control. **Multi-stage**, **optimizes the coast-arc
  duration**, enforces a **q·α (aero-load) AND qMax constraint**, targets the **FULL element set**
  (peR/apR/inc/LAN/argp/fpa, each optionally constrained). It is the RO-grade ascent.

## The real cost of PVG
It is not one file. `PSG/Ascent` runs a **numerical shooting optimizer** (`Optimizer.cs` + `AscentGuesser`
bootstrapping + fixed/optimized burn-time modes) over an **ODE integration** of the trajectory, pulling in
`MechJebLib/ODE` (DP5/DP8/Tsit5), `Minimization`, `Rootfinding`, and the `Primitives` (V3/M3/Q3) + `TwoBody`.
Porting it faithfully means bringing a large, interdependent slice of MechJebLib, AND it is a shooting method
that can **fail to converge** without a good initial guess + a fallback path. High value, high risk, big build.

## The gaps that actually bit us — and which guidance is needed to close them
| Observed gap | Needs full PVG? | Cheaper fix |
|---|---|---|
| Inclination undershoot (plane) | **No** | add **full-plane (inc/LAN) targeting to UPFG's cutoff** — the single highest-value, low-risk upgrade |
| Aero load / q·α at max-Q (RUD risk) | **No** | this is a CONTROL concern → **AoA/q·α moderation (P0)**, not the guidance solver |
| Coast-arc optimization | Yes | but our MECO coast is ~2 s — negligible Δv; not worth the optimizer for LEO |
| Multi-stage optimal | Yes | we have one upper stage (MVac); single-stage UPFG is sufficient |
| Elliptical/high/polar insertion (free-flyers) | **No** | parameterized cutoff target (any peR/apR/inc) — data plumbing |

## DECISION — ✅ CONFIRMED BY THE USER (2026-08-27)
**Upgrade UPFG; do NOT port full PVG now.** (User chose "Upgrade UPFG (recommended)" over a full PVG port or a
committed-PVG-next path — PVG stays a conditional, earned upgrade.)
1. **P1 — UPFG full-element cutoff:** target inc/LAN via the plane normal in the cutoff conditions (closes the
   inc undershoot) + parameterized peR/apR (free-flyer orbits). Low risk, high value, keeps the working solver.
2. **P0 — AoA/q·α moderation** carries the aero-load safety (where it belongs — control, not guidance).
3. **PVG is the COMMITTED fidelity TARGET (updated 2026-08-27 — user chose STRICT IMPLEMENTATION FIDELITY).**
   The real Crew Dragon / Falcon ascent uses PVG-class powered-explicit guidance, so strict fidelity means we
   port it — UPFG-upgrade is the INTERIM that unblocks flights, not the end state. Sequence: ship UPFG-upgrade
   now; then port `MechJebLib/PSG` + its ODE/minimizer deps as its own headless-tested milestone (with the
   UPFG-upgrade as the proven fallback + the convergence guard). It is still sequenced AFTER ascent/booster are
   flying on UPFG-upgrade (don't take the port's convergence risk while the basics are unproven), but it is now
   a committed goal, not a conditional one.

**Honest note:** a literal "SpaceX-equivalent" ascent would use PVG-class optimal guidance. Choosing
UPFG-upgraded is a pragmatic risk call that closes every gap we've actually observed at a fraction of the cost;
it is not a fidelity compromise on the trajectory we fly, only on mathematical optimality of the Δv. If the
user wants the PVG port regardless, it's specified above and belongs after the dispersion harness exists (so we
can prove the port converges across the envelope before trusting it).

Cross-refs: `docs/ASCENT_GUIDANCE_UPFG.md` · `docs/MECHJEB_CAPABILITY_INTEGRATION.md` (P1) ·
`docs/VALIDATION_AND_ROBUSTNESS.md` · `docs/MISSION_PROFILES_FREEFLYER.md` (parameterized insertion).
