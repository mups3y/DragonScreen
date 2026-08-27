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
The whole autopilot is BUILT + installed; this is the order to make it GREAT. Stages, dependency-ordered
(full detail: `AUTOPILOT_REBUILD_PLAN.md §0.2`):

- **Stage 0 — Foundations.** Verify the installed phasing + attitude fixes in flight; build the **Tier-2
  dispersion harness** (headless C#, crew-safety invariants — `VALIDATION_AND_ROBUSTNESS.md`); build the
  ascent/booster **P0 capabilities**: engine-out differential octaweb throttle, AoA/q·α moderation, **UPFG
  inc/LAN cutoff** (⭐ PEGAS's target-plane-normal method — the inc-undershoot fix, `AUTOPILOT_MINING_3.md`),
  StageStats (MECO reserve), actuator-lag.
- **Stage 1 — Ascent.** Tune to the REAL orbit (alt/inc, coplanar) AND a booster-recoverable MECO, together.
- **Stage 2 — Booster return.** Course-correction 2×2, TCA landing law, StageStats reserve → droneship.
- **Stage 3 — Navigation pipeline (strict fidelity).** `pure/NavFilter.cs` L1.5: simulate the sensor suite +
  EKF, fly guidance on the ESTIMATE (`CREW_DRAGON_GNC_RESEARCH.md §5`).
- **Stage 4 — Rendezvous.** RCS balancer (TCA solver), cascade completeness, Lambert + maneuver library,
  finite-burn executor → the AI standoff.
- **Stage 5 — Docking.** Corridor upgrade, KOS auto-abort, manual-takeover → capture.
- **Stage 6 — Return/entry/splashdown.** ReentrySimulation predictor (⭐ Trajectories' RK4 + KSP-Euler
  correction) + course-correction + the 4-band entry-AoA schedule; tune to splashdown.
- **Stage 7 — Hardening + fidelity.** FDIR full authority; retire tuned constants behind self-cal (⭐
  GravityTurn's LaunchDB loss-minimizing auto-tuner for the ascent shape); **PVG optimal-ascent port**
  (PEGAS virtual-stages skeleton); free-flyer profiles; **Tier-4 Monte-Carlo** (corpus-calibrated first).
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
