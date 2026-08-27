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
strict IMPLEMENTATION fidelity (build the real nav pipeline + PVG, not just the behavior). ⭐ **Build correctly
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
- **B5 — PVG / virtual-stages optimal ascent** ○ **(LAST, redefined 2026-08-28)** — real primer-vector / PEG optimal ascent (PEGAS/UPFG lineage): analytic primer-vector steering + a small Newton BVP shooting solve for costates/burn-times/optimal-coast. ⛔ NOT the MechJeb PSG/ALGLIB port (rejected — see the course-change box above). Near-no-op for single-burn Crew-2.
- **B6 — NavFilter (strict-fidelity nav)** ○ — `pure/NavFilter.cs` L1.5: simulate the sensor suite + EKF, fly guidance on the ESTIMATE (`CREW_DRAGON_GNC_RESEARCH.md §5`).
- **B7 — Lambert + maneuver-node library + finite-burn executor** ○ — beyond the current CW+Hohmann+named-burns.
- **B8 — Entry predictor upgrade** ✅ pure — `pure/CourseCorrect.cs` (finite-difference impact-divert: 2×2 booster / 1×1 entry, 15 checks) + `Trajectory.EntryLdBand` 4-band L/D schedule (predictor prior; ⛔ NOT active CoM steering — respects the engage-once hard rule). **Owed I-B (validation-gated):** wiring CourseCorrect into BoosterTargeting/EntrySteering (replaces a working heuristic → flight-validate, keep heuristic fallback) + the **KSP-Euler correction** (the doc gates it on reproducing a recorded flight — no entry corpus yet).
- **B9 — GravityTurn LaunchDB auto-tuner** ✅ pure — `pure/AscentLoss.cs` (gravity + drag + steering Δv-loss decomposition = the objective) + `pure/LaunchTuner.cs` (LaunchDB coordinate-descent that walks the gravity-turn shape to min-loss over launches; converges on a synthetic-loss replay). 17 checks. **I-B:** integrate the loss into the recorder + run the tuner across flights → `learned.cfg` (retires the hand-set pitch constants).
- **B10 — V&V completion** ½→ **Tier-2 dispersion** more families (docking/return/FDIR — today control+rendezvous only); **Tier-3** corpus regression tool; **Tier-4** Monte-Carlo (corpus-calibrated FuelFlowSim + ReentrySim, gate first).
- **B11 — FDIR full authority + free-flyer profiles** ✅ pure — added the FDIR **escalation ladder** (`Fdir.Escalate`, +6 checks): a fault a recovery rung doesn't clear within RungGraceS climbs Retry→Reconfigure→Replan→Downmode→**Abort**, so a persistent fault is guaranteed to reach abort rather than retry forever; resets when it clears. Free-flyer profiles built + verified (catalog has Inspiration4/Polaris Dawn/Fram2 as `MissionKind.FreeFlyer`, `HasRendezvous=false`; CrewGates omits G9–G14). **I-B (the plan's Step I):** wire FDIR live into FlightDriver — observe-only first (record fdir_*), then acting.

#### Build + Tuned status matrix (updated 2026-08-28)
Two axes per item, so we always know both whether it is BUILT and whether its tunables are at the best data-backed
starting defaults. **Build:** ○ not built · ½ first-cut · ✅ built. **Tuned:** **—** no tunables (pure math/physics) ·
**○** researched defaults, the DB has no data for this phase yet (can't DB-seed until I-B flies it) · **◐** DB-seeded
(initial values set/validated from the corpus) · **✅** flight-tuned (I-B). ⚠ The tuning DB corpus today covers only
**ascent control** (VerticalRise/GravityTurn/S2Burn/Coast) + **abort** — NO booster/rendezvous/docking/entry/chute
data — so only ascent-coupled tunables are DB-seedable now; the rest stay ○ until their I-B flight.

| Item | Build | Tuned | Tuning source / why |
|---|---|---|---|
| B1 StageStats | ✅ | — | physics only (G0, rocket eq); no tunables |
| B2 q·α moderation | ✅+glue | ○ | aero-stiffness seed is researched; the estimator needs the isolated-aero FEED (owed I-B) before the DB can seed it |
| B3 thrust/RCS balancer | ✅+glue | ○ | StepFactor/deadband researched; no engine-out or rendezvous flight in the corpus |
| B4 actuator-lag | ✅+glue | ◐ | uses the LIVE measured gimbal `responseSpeed` → self-seeding, no static value to tune |
| B5 primer-vector PVG | ○ | — | not built (last) |
| B6 NavFilter | ✅ | ○ | IMU/RGPS noise tunables; no sensor-truth flight yet |
| B7 Lambert + Maneuver | ✅ | — | universal-variable math; no tunables |
| B8 entry predictor | ✅ pure | ○ | CourseCorrect + EntryLdBand built; band L/D + KSP-Euler pending an entry-flight corpus to calibrate; targeting glue owed I-B |
| B9 GravityTurn auto-tuner | ✅ pure | ○ | AscentLoss + LaunchTuner built; the tuner seeds from recorded ascent LOSSES → needs recorder loss-columns + flights (I-B). It IS the ascent-shape tuner |
| B10 V&V | ½ | — | test tooling; no tunables |
| B11 FDIR authority | ✅ pure | ○ | escalation ladder + free-flyer profiles built; debounce/threshold tunables need data (ascent+abort only); live-wiring + acting = I-B Step I (observe-first) |
| Ascent control (L2/L3) | ✅ | ◐ | DB-VALIDATED: GravityTurn/S2Burn pe_p95 < 0.4°, sat_duty ≈ 0 across the corpus → current defaults are already good |

**I-A tuning goal:** move every item to **◐** where the corpus supports it; the ones stuck at **○** are the honest
list of tunables that can only be seeded once their phase flies in I-B. Regenerate with `tools/tuning_db.py` after any
new flight; re-seed here from `assess_flight.py` + the DB.

### Movement I-B — PROVE IT (flight-test + tune, phase-order: ascent→booster→rendezvous→docking→return)
Only AFTER I-A. Tune each phase to its REAL coupled targets against the FlightRecorder CSVs (the flights flown
2026-08-28 were skeleton reconnaissance — they verified the phasing self-deorbit fix and surfaced the ascent
defects, several of which are really MISSING I-A methods). Then:
- **Stage 8 — CLAUDE PROVING RUN.** One Crew-2 mission pad→splashdown, crew on the gates, timeline matching.
  ⭐ **The name is earned here.**

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
