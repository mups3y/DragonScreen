> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGH (§B10)**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-28; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# Validation & robustness — closing the "flawless is unprovable" hole

> **Why this exists (2026-08-27, honest-assessment follow-up).** The biggest hole in the plan is NOT missing
> features — it is that our validation proves *correctness of the logic on the nominal case* (headless unit
> tests) and *one clean flight*, but never *robustness across the envelope*. "Flawless" is unprovable without
> showing the autopilot survives dispersed conditions (winds, mass/CoM, engine underperformance, sensor noise,
> timing, failures). This doc is the method to earn a robustness claim **inside the project's hard rules.**
>
> ⛔ **HARD RULE INTERACTION — read first.** [[no-python-simulations]] bans Python physics/orbital sims for
> validation (they didn't work, and were deleted). Everything below is **headless C#** exercising the pure
> layer the user already accepts AS the certification, and every synthetic result is **cross-checked against
> the recorded flight corpus** before it is trusted ("verify the data in AND out"). Tier 3 (a C# trajectory
> Monte-Carlo) brushes closest to the ban — it is **flagged, not assumed**: build it only after the user
> confirms, because it is a forward-model, even though it is C# and corpus-calibrated.

---

## The four tiers of validation (each earns a different claim)

| Tier | Method | What it PROVES | Rule status |
|---|---|---|---|
| **1. Reference-accuracy tests** (have) | headless C#: each pure module vs an analytic/conservation/reference case | the MATH is correct on known cases | ✅ core discipline; this is MechJeb's own bar |
| **2. Property-based dispersion** (BUILD — primary) | headless C#: run the pure guidance/control over THOUSANDS of randomized-but-bounded initial states; assert invariants hold every time | the LOGIC is ROBUST, not just nominal-correct | ✅ headless C# unit testing of the pure layer — squarely allowed |
| **3. Flight-corpus regression** (partly have) | replay every recorded CSV through the analyzers + tuning DB; assert no regression, flag contamination | the code still explains REAL flights | ✅ analysis of recorded data (allowed) |
| **4. Trajectory Monte-Carlo** (⚠ FLAG — ask first) | a C# 3-DOF forward-model (reuse MechJeb's VERIFIED FuelFlowSim + ReentrySimulation), dispersed inputs, N runs → P95 miss / abort-safe rate | STATISTICAL robustness (the real "flawless-ish" number) | ⚠ a forward-model — close to the sim ban; C# + corpus-calibrated, but **confirm with the user before building** |

**Tier 2 is the one that closes the hole and is unambiguously allowed.** It is not a physics sim — it is the
existing headless test suite, fed dispersed inputs, asserting the invariants that MUST hold for the crew to be
safe. It is how you go from "it worked once" to "it cannot do the wrong thing."

---

## Tier 2 — property-based dispersion (the core new resource)

### The idea
For each pure controller, instead of one hand-picked test case, generate **N random cases within the real
envelope** and assert **invariants** — properties that must hold for EVERY input, not a specific output.
A single failing seed is a found bug. This is the standard way flight-critical logic is hardened.

### The invariants to assert per layer (the safety contract)
These are the properties whose violation loses the crew or the mission — assert them across the dispersion:

**Control (L2 / AttitudeLoop / ControlLaw)**
- Commanded rate is ALWAYS arrestable (`|ω_cmd| ≤ √(2·α·θ)` for the live α) — never commands a rate it can't stop.
- Actuation always in [−1, 1]; never NaN; zero-authority → zero command (no divide-by-zero kick).
- Pointing error is non-increasing in steady state (Lyapunov-style: the loop converges from any initial error).

**Ascent (L3 Ascent / UPFG / PVG)**
- AoA command never exceeds the moderation cap (q·α bound) at any q — the max-Q RUD invariant.
- UPFG/PVG converges (tgo→0, residual→0) from any dispersed MECO state within the vehicle's Δv; if it CAN'T
  reach the target, it reports infeasible (never silently flies a suborbital cut thinking it succeeded).
- Achieved orbit is prograde and within tolerance of the target for any launch-site latitude ≤ inclination.

**Rendezvous / Phasing (the self-deorbit class)**
- ⛔ **pe NEVER decreases below the safety floor** for ANY relative geometry — the invariant that would have
  caught flight 214827. Fuzz the relative state over ±13,000 km and assert no burn lowers pe past the floor.
- Far-field commands are prograde-or-coast only; CW is never invoked beyond its valid range.
- The solved transfer's free-drift reaches the aim (self-consistency) for any near-field state.

**Docking**
- Closing speed ≤ `MaxSpeedForDistance` at every range → always brakeable before contact.
- Lateral miss is nulled before along-axis closing inside the corridor → never cuts across the KOS.
- Any unplanned KOS breach → abort, for any approach geometry.

**Return / Entry**
- Deorbit burn stops at the target Pe (never over-burns into a steep unsafe entry).
- Bank command keeps predicted g within the crew limit across dispersed entry FPAs.
- Chutes deploy only in a safe q/Mach state and above the ground for any descent rate.

**FDIR (the whole point)**
- For EVERY injected single failure (engine out, thrust shortfall, loss of control, sensor dropout), the
  ladder terminates in a SAFE state (splash / safe orbit / standoff) within a bounded time. This is the
  "always brings the crew home" proof — run it over the failure×phase cross-product.

### The dispersion axes (the envelope to sample)
Mass ±5%, CoM offset, thrust ±3% per engine (+ 1 engine out), Isp ±2%, drag/lift ±20% (RSS/FAR uncertainty),
initial attitude/rate error, wind (FAR), sensor noise added to the relative state, actuator lag ±, ullage
state, launch-site latitude, target orbit (all four mission profiles). Seeded + reproducible; a failing seed
is saved as a permanent regression case.

### How it lands in the repo
- `plugin/test/dispersion/` — a small property-test harness (`Prng` seeded, `Envelope` sampler, `Invariants`
  asserted). Runs in `build.py test` (add a `--dispersion N` flag; default N small for CI, large on demand).
- Each found-and-fixed failing seed becomes a named regression test (like the existing `exclude.txt` bad
  flights, but synthetic). The corpus of seeds GROWS the safety proof over time.
- **Report a coverage number:** % of the invariant×envelope grid exercised, and the worst-case margins found
  (e.g. "min pe over 50k rendezvous seeds = 178 km, floor 150 km, margin +28 km").

---

## Tier 3 — flight-corpus regression (extend what exists)
`tools/tuning_db.py` already pools the corpus and flags contamination. Add a `tools/regress.py` that, after
every code change, re-runs the analyzers over EVERY recorded CSV and asserts: (a) each historical flight's
event timeline still parses, (b) the analyzer's derived quantities match the recorded orbit columns (in/out
check), (c) no previously-clean phase now reads broken. This catches a fix that silently breaks the reading
of a past flight. Cheap, pure analysis of recorded data — fully within the rules.

---

## Tier 4 — trajectory Monte-Carlo (✅ APPROVED 2026-08-27 — corpus-calibrated first)
The only way to a real *statistical* number (P95 splashdown miss, ascent-to-orbit success rate, abort-survival
rate) is a forward-model run N times over the dispersion. **User decision (2026-08-27): ALLOWED — a C# forward-
model is OK *provided it reproduces a known recorded flight within tolerance BEFORE any dispersion run* (the
"verify-in" gate). The [[no-python-simulations]] ban was about Python sims that didn't work; a corpus-validated
C# model is a distinct, accepted thing.** Reuse the two VERIFIED MechJeb C# models rather than writing physics
from scratch: `MechJebLib/FuelFlowSimulation` (staging Δv/TWR, exact) and `ReentrySimulation.cs` (BS34 adaptive
integrator, drag+lift+parachute, outcome classification). Also harvestable: **KerbalEngineer's `VesselSimulator`**
(ΔV/TWR per stage) and **ThrottleControlledAvionics** (engine-balancing / thrust-optimization) — see
`docs/MODS_HARVEST_2.md`.
⛔ **HARD PRECONDITION (the gate that keeps it inside the rules):** before it is trusted for ANY dispersion run,
the model MUST reproduce ≥2 recorded flights (e.g. the 214827 ascent + an entry) within a stated tolerance, and
that calibration check is a permanent headless test. A model that can't match the corpus is deleted, not tuned
to look right. "Verify in AND out" applies to the model itself, not just its outputs.

---

## What claim each tier lets us make (say exactly this, no more)
- Tiers 1+2+3 done → **"Robust across the exercised envelope: the logic cannot violate the crew-safety
  invariants for any sampled condition, and it still explains every real flight."** (Strong, honest, achievable.)
- Tier 4 done → add a **statistical** number (e.g. "P95 splashdown miss X km over 10k dispersed entries").
- Never claim "flawless" — claim **"robust with guaranteed abort-to-safe,"** which is what real flight
  software actually claims, and which our FDIR spine is built to deliver.

Cross-refs: [[no-python-simulations]] · [[full-structured-flight-analysis]] · [[dragonscreen-tuning-database]]
· `docs/PHASE_ACCEPTANCE_CRITERIA.md` (the per-flight gates that feed Tier 3).
