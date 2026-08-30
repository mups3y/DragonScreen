> **SUPERSEDED — historical reference only, NOT an active instruction.**
> The sole governing specification is `docs/MASTER_BUILD_SPEC.md`. Do not follow this document.
> Quarantined 2026-08-31 (Phase 1 document control). Kept for history; not deleted.

---

# THE ULTIMATE ~~QUESTION~~ PLAN — the whole DragonScreen mod, end to end

> *"There is a theory which states that if ever anyone discovers exactly what the Universe is for and why it
> is here, it will instantly disappear and be replaced by something even more bizarre and inexplicable. There
> is another theory which states that this has already happened."* — build the autopilot anyway.
>
> **This is the single top-level governing plan for the ENTIRE DragonScreen mod.** It supersedes nothing's
> content — the detailed docs remain its backing (find any of them in **`docs/INDEX.md`**). Order of the whole
> mod: **build CLAUDE (the autopilot flight system) FIRST and prove it, THEN finish the entire mod** (screens,
> console, the missing pages, the hidden mini-game, audio, the lot). Authoritative sub-plans it cites:
> `AUTOPILOT_REBUILD_PLAN.md` (autopilot detail + §0.2 stage sequence), `SCREENS_CONSOLE_PLAN.md` +
> `SCREENS_LOOK_AND_FUNCTION_RESEARCH.md` (screens), `VALIDATION_AND_ROBUSTNESS.md`, the harvest/mining docs.

---

## The answer is 42; the work is everything else
DragonScreen is a full RSS/RO Crew Dragon + Falcon 9 experience flown from the IVA glass cockpit: a true
SpaceX/NASA-grade **autonomous autopilot (CLAUDE)** that flies any crew mission pad→splashdown, presented on
**faithful Crew Dragon touchscreens + the physical button console**, crew-in-the-loop. ⭐ **The autopilot is
"the autopilot" until it EARNS the name CLAUDE** — a clean, full-fidelity crew mission, launch→splashdown.

**The frame of mind (the guardrails — full detail `AUTOPILOT_REBUILD_PLAN.md §0`):** strict full fidelity, crew
always comes home; DIRECT part control only; the direct gimbal/RCS loop is the inner loop (SAS fallback-only);
tune ONE PHASE at a time to REAL coupled targets; don't invent + trust sims — use proven methods + the recorded
corpus; instrument everything; batch reasoned fixes then fly; commit+install autonomously, push on request;
strict IMPLEMENTATION fidelity (build the real nav pipeline + PVG, not just the behavior). ⭐ **MOD-DEPENDENCY
POLICY** ([[mod-dependency-policy]]): an OPTIONAL mod (may be absent — KER, BetterTimeWarp) → soft-integrate
(reflection + our own fallback) or build the behaviour in; a HARD-dep of RO/RSS (always present — CustomPreLaunchChecks,
ModuleManager, RealFuels, FAR, KSPCommunityFixes…) → ⛔ don't reinvent it ("a hat on a hat"), take DIRECT control.
⭐ **Build correctly
+ thoroughly the FIRST time — NO shortcuts:** verify before reuse; verify EVERY comment against the code and
fix wrong ones (they rot); keep comments precise BUT concise; skim-reading/shortcutting to save tokens only
costs more rebuilding later — token-efficiency is terse CHAT + not-rebuilding, never a skimmed build
([[build-verify-no-shortcuts]]). ⛔ **NO KOS CODE** — DragonScreen is C#; port logic from any kOS reference
(PEGAS) into C#, never `.ks` / kOS idioms. **Also (full 12 in [[build-verify-no-shortcuts]]):** units + frames
always explicit (our #1 bug class); fail LOUD, never silent; pure/glue discipline; no magic literals (named
`[Tunable]` + source); no dead code left as live; guard every KSP API call. ⭐ **PERFORMANCE — 60 FPS is the
benchmark** (target rig: i5-14400F / GTX 1080 8GB / 16 GB @ 1080p60 — mid-range; the GTX 1080 + RAM are the
ceilings). Our mod must NEVER be the reason FPS drops below 60: allocation-free hot paths (per-frame GC =
stutter), cached part/module lookups, efficient screen rendering + RT cameras.

---

## PART I — BUILD CLAUDE (the autopilot) FIRST
⚠ **STATE (corrected 2026-08-28, user):** a **first-cut SKELETON** is built + flies (pure L0–L7 + glue seams,
pad→orbit→phasing). It is **NOT the advanced autopilot** — the huge array of RESEARCHED methods (from the
MechJeb/TCA/PEGAS/Trajectories/GravityTurn mining + the GNC research) is largely **a BUILD BACKLOG, not
implemented**. ⭐ **DECISION (user 2026-08-28): BUILD THE FULL FEATURE SET FIRST, THEN TEST.** So Part I has
two movements: **I-A BUILD the backlog** (headless-first, no flights) → **I-B PROVE it** (flight-test + tune
phase-by-phase — the phase-order rule lives HERE, not in the build). Full detail: `AUTOPILOT_REBUILD_PLAN.md
§0.2`, `MECHJEB_CAPABILITY_INTEGRATION.md`, `MODS_HARVEST_2.md`, `AUTOPILOT_MINING_3.md`, `CREW_DRAGON_GNC_RESEARCH.md`.

### Movement I-A — BUILD THE RESEARCHED-METHOD BACKLOG (pure-first + headless-tested; NO flights yet)
Dependency-ordered. Each = a pure module + its tests, THEN the thin glue. Ported from the PROVEN sources
(MechJeb `mechjeb_src`, PEGAS, TCA, Trajectories) — never a self-invented model. ✅=built, ½=first-cut only, ○=not built.

> ✅✅ **MOVEMENT I-A COMPLETE (2026-08-28).** All 11 backlog items built + headless-validated (the suite is now
> ~725k checks/build), plus the FATAL abort splashdown fixed. Pure modules + the safe glue are in; the
> behavior-changing / corpus-gated glue is explicitly deferred to I-B (listed per item below + in
> `RESUME_PROMPT.md`). **NEXT = Movement I-B: flight-tune phase-by-phase.**

> ⭐ **APPROVED COURSE CHANGE (2026-08-28, user OK'd the pivot).** Two governing constraints now: **(1) NO NEW flight
> tests until the ENTIRE autopilot is built** — stay build-only through all of I-A; **(2) do INITIAL tuning from the
> UP-TO-DATE flight recordings + the tuning DB** (`quarantine\dragonscreen_flightdata` + `DragonScreen_capture`, via
> `assess_flight.py` / `tuning_db.py`) as you build — set data-backed start values now, never invent; FINAL tuning
> (fresh flights, phase-by-phase) is I-B. **Priority within I-A is crew-survival-first:** the FATAL
> abort defect, then B8 (entry) + B11 (FDIR), then B9/B10, and **B5 (PVG) LAST**. **B5 is REDEFINED:** a faithful
> port of MechJeb `PSG` is REJECTED — PSG is a Hermite-Simpson collocation transcription solved by **ALGLIB's
> `minnlc` SQP** (`alglib.minnlcoptimize`, 4000 iters, 120 s timeout, off-thread), i.e. vendoring **~200k lines of
> ALGLIB** into the `-nostdlib` allocation-free build — which breaks the 60-FPS / minimal-build rules and is NOT how
> bounded-time flight avionics are built. Instead B5 = **real primer-vector / PEG optimal ascent (PEGAS/UPFG
> lineage): analytic primer-vector steering + a small Newton BVP shooting solve** for costates/burn-times/optimal-
> coast — same solution class, deterministic, allocation-light, no NLP library. It is ~a no-op for the single-burn
> Crew-2 profile (UPFG already reaches orbit clean), hence LAST. ⛔ **ALGLIB is permanently off the table.**
>
> **FATAL ABORT (crew-survival item #1):** launch-escape aborts splash at ~122 m/s (mains under-decelerate). FIRST
> classify from the EXISTING quarantine recordings (no new flight): if it is a **logic/structural** defect (chutes
> not arming / wrong deploy sequence / wrong module) → fix NOW as a build item; if it is purely **tuning** (deploy
> altitudes, chute sizing) → flag for I-B and move on.
- **B1 — StageStats / FuelFlowSim** ○ — Δv/TWR per stage + MECO recovery reserve (port MechJeb `FuelFlowSimulation`). Foundational (feeds budgets + Tier-4).
- **B2 — q·α moderation** ½→ the AtmosphereAutopilot online-model controllability region (today only a q-scaled AoA cone cap).
- **B3 — Thrust/RCS balancer** ○ — the TCA `EngineOptimizer`/`RCSOptimizer` torque-nulling solver → **engine-out differential octaweb throttle** AND the RCS translation balancer (one solver, both users).
- **B4 — Actuator-lag model** ○ — command→response lag compensation in the control loop.
- **B5 — PVG / virtual-stages optimal ascent** ✅ **(LAST, done 2026-08-28)** — multi-stage UPFG (PEGAS virtual stages): `Upfg.Step(UpfgStage[])` accumulates the thrust integrals across burn stages with the `tgoi1` cross-stage shift, ported VERBATIM from `Noiredd/PEGAS-MATLAB/unifiedPoweredFlightGuidance.m` (fetched this session). Both const-thrust + const-accel modes; blocks 5–8 refactored into a shared `Steer()`; single-stage Step is the unchanged n=1 case. Validated: n=1 reproduces single-stage exactly + a 2-stage point-mass CLOSURE reaches ~200×200 km through a staging jettison. ⛔ NOT the MechJeb PSG/ALGLIB port (rejected). Near-no-op for our single-upper-stage vehicles → single-stage Step stays the live path; multi-stage built + validated, ready if needed.
- **B6 — NavFilter (strict-fidelity nav)** ○ — `pure/NavFilter.cs` L1.5: simulate the sensor suite + EKF, fly guidance on the ESTIMATE (`CREW_DRAGON_GNC_RESEARCH.md §5`).
- **B7 — Lambert + maneuver-node library + finite-burn executor** ○ — beyond the current CW+Hohmann+named-burns.
- **B8 — Entry predictor upgrade** ✅ pure — `pure/CourseCorrect.cs` (finite-difference impact-divert: 2×2 booster / 1×1 entry, 15 checks) + `Trajectory.EntryLdBand` 4-band L/D schedule (predictor prior; ⛔ NOT active CoM steering — respects the engage-once hard rule). **Owed I-B (validation-gated):** wiring CourseCorrect into BoosterTargeting/EntrySteering (replaces a working heuristic → flight-validate, keep heuristic fallback) + the **KSP-Euler correction** (the doc gates it on reproducing a recorded flight — no entry corpus yet).
- **B9 — GravityTurn LaunchDB auto-tuner** ✅ pure — `pure/AscentLoss.cs` (gravity + drag + steering Δv-loss decomposition = the objective) + `pure/LaunchTuner.cs` (LaunchDB coordinate-descent that walks the gravity-turn shape to min-loss over launches; converges on a synthetic-loss replay). 17 checks. **I-B:** integrate the loss into the recorder + run the tuner across flights → `learned.cfg` (retires the hand-set pitch constants).
- **B10 — V&V completion** ✅ Tier-2 — **Tier-2 dispersion** now covers 5 families: control, rendezvous, **docking, return, FDIR** (property-based invariants over the pure layer, **724,791 checks/build**). **I-B (corpus-gated):** Tier-3 corpus-regression tool + Tier-4 Monte-Carlo (corpus-calibrated FuelFlowSim + ReentrySim — must reproduce ≥2 recorded flights first, `VALIDATION_AND_ROBUSTNESS.md`).
- **B11 — FDIR full authority + free-flyer profiles** ✅ pure — added the FDIR **escalation ladder** (`Fdir.Escalate`, +6 checks): a fault a recovery rung doesn't clear within RungGraceS climbs Retry→Reconfigure→Replan→Downmode→**Abort**, so a persistent fault is guaranteed to reach abort rather than retry forever; resets when it clears. Free-flyer profiles built + verified (catalog has Inspiration4/Polaris Dawn/Fram2 as `MissionKind.FreeFlyer`, `HasRendezvous=false`; CrewGates omits G9–G14). **I-B (the plan's Step I):** wire FDIR live into FlightDriver — observe-only first (record fdir_*), then acting.

#### Build + Tuned status matrix (updated 2026-08-28)
Two axes per item, so we always know both whether it is BUILT and whether its tunables are at the best data-backed
starting defaults. **Build:** ○ not built · ½ first-cut · ✅ built. **Tuned:** **—** no tunables (pure math/physics) ·
**○ = guess** best educated-guess default set, the DB has no data for this phase yet (never "unset") · **◐ = DB**
tuned/validated from the corpus · **live** self-tunes from live telemetry · **✅** flight-tuned (I-B). **⭐ Every
tunable (122, none unset) already carries a best educated-guess default** (user directive 2026-08-28): where there
is no corpus data, use the best researched/educated guess — ○/guess means "best-guess set, awaiting corpus/flight
confirmation", NOT "to do". ⚠ The tuning DB corpus today covers only
**ascent control** (VerticalRise/GravityTurn/S2Burn/Coast) + **abort** — NO booster/rendezvous/docking/entry/chute
data — so only ascent-coupled tunables are DB-seedable now; the rest stay ○ until their I-B flight.

| Item | Build | Tuned | Tuning source / why |
|---|---|---|---|
| B1 StageStats | ✅ | — | physics only (G0, rocket eq); no tunables |
| B2 q·α moderation | ✅+glue | ○ | aero-stiffness seed is researched; the estimator needs the isolated-aero FEED (owed I-B) before the DB can seed it |
| B3 thrust/RCS balancer | ✅+glue | ○ | StepFactor/deadband researched; no engine-out or rendezvous flight in the corpus |
| B4 actuator-lag | ✅+glue | ◐ | uses the LIVE measured gimbal `responseSpeed` → self-seeding, no static value to tune |
| B5 primer-vector PVG | ✅ | — | multi-stage UPFG (PEGAS virtual stages) built + point-mass-closure validated; no tunables |
| B6 NavFilter | ✅ | ○ | IMU/RGPS noise tunables; no sensor-truth flight yet |
| B7 Lambert + Maneuver | ✅ | — | universal-variable math; no tunables |
| B8 entry predictor | ✅ pure | ○ | CourseCorrect + EntryLdBand built; band L/D + KSP-Euler pending an entry-flight corpus to calibrate; targeting glue owed I-B |
| B9 GravityTurn auto-tuner | ✅ pure | ○ | AscentLoss + LaunchTuner built; the tuner seeds from recorded ascent LOSSES → needs recorder loss-columns + flights (I-B). It IS the ascent-shape tuner |
| B10 V&V | ✅ Tier-2 | — | 5 dispersion families, 724,791 checks; Tier-3/4 corpus-gated → I-B; no tunables |
| B11 FDIR authority | ✅ pure | ○ | escalation ladder + free-flyer profiles built; debounce/threshold tunables need data (ascent+abort only); live-wiring + acting = I-B Step I (observe-first) |
| Ascent control (L2/L3) | ✅ | ◐ | DB-VALIDATED: GravityTurn/S2Burn pe_p95 < 0.4°, sat_duty ≈ 0 across the corpus → current defaults are already good |

**I-A tuning goal:** move every item to **◐** where the corpus supports it; the ones stuck at **○** are the honest
list of tunables that can only be seeded once their phase flies in I-B. Regenerate with `tools/tuning_db.py` after any
new flight; re-seed here from `assess_flight.py` + the DB.

### Movement I-B — GET IT FLYING END-TO-END, THEN THE TUNE LOOP (rinse & repeat until perfect)
⭐ **This is the real rhythm of the project (user 2026-08-28).** I-A built the advanced methods; I-B makes the whole
mission RUN, then tunes it to perfection from real flight data, over many iterations.

- **I-B.0 — GET THE MISSION RUNNING END-TO-END (the milestone that unlocks testing).** Wire/fix so a single AUTO run
  COMPLETES pad→splashdown **including booster recovery**, FAST. The bar is completion + clean phase hand-offs, NOT
  perfection (signs/params stay best-guess; the loop tunes them). Concrete: **(a)** booster-recovery focus
  orchestration (`MissionConductor.AutoRecoverBooster` — focus→land→return; partial-PRE keeps it loaded); **(b)**
  ⭐ **proactive coast auto-warp = warp-to-maneuvers** (wire each coast controller's next-event UT into
  `MissionConductor.WarpToEvent` — launch window done, phasing + return/deorbit coasts to add) so there are NO long
  real-time waits; **(c)** every phase transition completes + hands to the next (fix any stall); **(d)** plausible
  best-guess signs/params so it RUNS; **(e)** confirm the octaweb + abort fixes hold in a real run.
  ⭐⭐ **THE MATCHING TIMELINE MUST EMERGE — NEVER FORCE IT (user 2026-08-28).** "Get it running end-to-end" is
  about the physics FLOWING through every phase to completion, NOT about hitting the real Crew-2 callout MET. ⛔ Do
  NOT hardcode/script an event time, insert a fixed wait, or fake a hand-off to make a phase "complete" on schedule.
  Every event time (MECO/SECO/sep/AI/dock/deorbit/splash) must be an EMERGENT output of physics-based guidance
  flying the real trajectory — a timeline that matches the real MET is the *tell* that the guidance is right, not a
  target to steer to. If the emergent timeline is off, that is DATA for the tune loop (I-B.1), never a cue to force
  the clock. This is the acceptance principle applied to the get-it-running phase: warp-to-maneuvers only compresses
  the ballistic *coasts* (it never advances a burn or a gate); the burns and hand-offs stay physics-driven.
- **I-B.1 — THE TUNE LOOP → RINSE AND REPEAT.** User flies MULTIPLE end-to-end tests → I run the FULL structured
  analysis on each recording + FEED the tuning DB → we DB-tune **ONE PHASE AT A TIME in mission order**
  (ascent→booster→rendezvous→dock→return) with the DB tuner (the B9 `LaunchTuner` coordinate-descent generalised
  per phase, driven by the recorded per-phase loss/deviation) → user flies more → analyze → feed DB → tune → …
  **REPEAT until the entire flight is perfectly tuned end-to-end.** ⭐ Warp-to-maneuvers throughout to maximise
  flight throughput; one phase per pass, in order (batch reasoned fixes within a phase; revert on the data).
- **Stage 8 — CLAUDE PROVING RUN.** Once every phase is tuned: one Crew-2 mission pad→splashdown, crew on the gates,
  timeline matching. ⭐ **The name is earned here.**

---

## PART II — FINISH THE ENTIRE MOD (the screens, console & the fun)
Runs mostly in PARALLEL with Part I (screens are pure + PNG-preview testable, don't gate the autopilot), but
completed as its own program. Detail: `SCREENS_CONSOLE_PLAN.md` + `SCREENS_LOOK_AND_FUNCTION_RESEARCH.md`.

- **S-A — Fix the known NAV bugs.** The globe MIRROR (swap U in `Globe()` like `Quad()`) + the orbit lines not
  closing (draw the `n-1→0` segment). Use ALL our licensed assets (Figma CC-BY, Vue Apache-2.0, Kenney CC0).
- **S-B — Wire every console button.** All ~39 (`PanelMap`) verified to a real function or an honest red-dash
  refusal; educated-guess the inert ones (power/strings, entry-swap). ⭐ Add **per-button colliders** (today the
  console is one collider — no button is individually hittable). ⛔ KEEP the abort feature/display (loved).
- **S-C — Complete the page set (parity with the real Crew Dragon UI).** Existing: NAV, MECH, Docking, Settings,
  navball, docking cam, crew gates. **Build the MISSING pages:** ❌ **Vehicle Overview** (connections +
  life-support PPO2/temp/pressure/CO2 + orbit + range-to-ISS + GO/NO-GO) and ❌ **Suit Leak Check 4.011** (the
  `SuitLeakG2` gate exists but has no page) — both now buildable against the TAC-LS cabin model.
  ⭐ **Wire the launch GO/NO-GO to the REAL readiness** — read KSP's stock `PreFlightCheck` / RO's **CustomPreLaunchChecks**
  (a hard RO-dep → USE it directly, don't reinvent avionics/control/crew checks) and surface each as a crew-gate
  line, so a NO-GO shows RED on the glass (`MOD_INTEGRATION_RESEARCH.md §4b`).
- **S-D — Audio.** ❌ (residual hole) decide + add sound: the abort klaxon, alert tones, crew callouts, and the
  Audio settings page — the real Dragon is mostly quiet + alerts, so keep it minimal + tasteful.
- **S-E — The hidden docking MINI-GAME.** ⭐ Natively recreate the iss-sim docking experience (green-diamond
  target, roll/pitch/yaw null + XYZ translate, dock at rates < 0.2) reusing `DockControl` + navball + the docking
  page, hidden-gesture trigger. Built from our LICENSED art; recreate mechanics (never rip SpaceX's own files).
- **S-F — Persistence + polish.** ❌ (residual hole) confirm the mission/autopilot state survives a mid-mission
  save→reload; verify `ProofPage`'s purpose; the systematic per-page ERROR HUNT (PNG previews vs the reference);
  high-fidelity pass; performance budget (keep the FPS hit minimal).

---

## VALIDATION (continuous, across both parts)
Every change → headless C# tests (~4000 checks = the certification) + Tier-2 dispersion of the pure layer +
Tier-3 corpus regression. Screens → PNG previews vs the reference art. Never trust a self-invented model.
The bar is **ROBUST with guaranteed abort-to-safe**, not "flawless" (`VALIDATION_AND_ROBUSTNESS.md`).

## DEFINITION OF DONE — Life, the Universe, and Everything
1. CLAUDE flies one Crew-2 mission pad→splashdown clean, full-fidelity, crew only on the gates, timeline matching
   (the 8 `TRUE_AUTOPILOT_ARCHITECTURE.md §13` criteria) — the name is earned.
2. Every DragonScreen page + every console button is assigned and working (or an honest refusal).
3. The missing pages (Overview, Suit-Leak), audio, and the hidden mini-game are done.
4. Robust across the exercised dispersion envelope; every abort path ends safe.
5. Then, and only then, is the mod complete. Don't Panic.

**Everything is findable in `docs/INDEX.md`. This is the top; that is the map.**
