# Next-session resume prompt — DragonScreen autopilot

Paste the block below at the start of the next session. It puts you in the right frame of mind and points you
at the one thing that matters next.

---

## ⭐⭐ RESUME HERE (2026-08-29, post-compaction #7) — DEORBIT ROOT CAUSE FIXED (4 stranded); AUTO SEQUENCE NOW STATE-AWARE

**⭐ WHAT JUST HAPPENED (2026-08-29 flight tests: 232712 / 235031 / 235430 / 000624 — rendezvous + deorbit + abort +
entry; 4 vessels stranded). Full CSV+log analysis done → the deorbit ROOT CAUSE found + fixed. Commits 5d02f84
(fixes) + 556b603 (artifacts), installed, UNFLOWN.** The full issue list is `docs/ISSUE_REGISTER.md` §"2026-08-29
FLIGHT-TEST ISSUE LIST" (F1–F7). The headline three:
- **F1 — THE 4-STRANDED ROOT CAUSE, FIXED⚑.** The deorbit/abort could not HOLD RETROGRADE (ptErr p95 128–178°, act
  saturated) → the burns fired mis-pointed: flight 235430 burned **~900 m/s but pe dropped ~3 km** → did not deorbit.
  ROOT: `AttitudePilot.ControlTorque`'s geometric RCS-torque fallback summed `thrusterPower × |arm|` over ALL 16
  Dracos on EVERY axis (~9× over — logged **111185 N·m**) → the arrestable-rate law α=τ/MOI over-estimated → it
  commanded ω=√(2αθ) far too high → the capsule OVERSHOT retrograde and oscillated → the burn fired mid-swing. FIX:
  a **per-axis `r×F` estimate** ported from MechJeb `VesselState.RCSTorqueAvailable` ((pos−CoM)×(thrustDir·power),
  projected to the control frame, Max(Σ+,Σ−) per axis). ⚠ #1 to verify: does a deorbit now actually bring a vessel down?
- **F2 — AUTO SEQUENCE NOW STATE-AWARE, FIXED⚑** (Chris's core ask: "know where it is and what to do next; don't
  re-launch in orbit"). `CrewProcedureOps.ResumeIndex` maps the LIVE vessel state → the right plan step: pad→countdown,
  ascending→ascent, **orbit+not-docked+station-targeted→rendezvous**, docked/post-dock→departure, **orbit-otherwise→
  deorbit**, entering→ride-it-down. So a stranded vessel + AUTO SEQUENCE resumes the correct phase. ⭐ Documented +
  driven by the NEW **`docs/SEQUENCE_MAP.md`** — the phase sequence + all alternate paths + the ABORT DECISION MATRIX
  per phase (grounded in the real Crew Dragon 8-mode abort structure; research-sourced).
- **F3 — rendezvous reached the 100 km CW hand-off after ~34 h but did NOT dock** (ptErr 113°, may share F1's attitude
  root). OPEN — re-fly with F1 fixed, then tune the CW terminal.
- Also open: **F4** deorbit water-scan reads 0/130 over water in RSS (WATER DEORBIT can't target water; DEORBIT NOW
  land-anywhere works regardless); **F5** g 4.53 > 4.5; **F6** early-ascent act-sat transient; **F7** pad safe-abort on
  a re-launch (spent RealFuels ignition — operational, full restart after a revert).

> **Carried from #6 (all installed, unflown unless noted):** docking RCS signs DERIVED all −1 (MechJeb); M5 resolved —
> Draco RCS = MMH+NTO, 300u MonoProp is unused dead weight LEAVE it; entry roll = a flight-resolved sign with a SAFE
> failure; TIME WARP tunable (`MissionConductor.AutoWarpEnabled` + `MaxWarpRateX` via the `SafeRate` ladder); the
> **PERFECT-CONTROL VEHICLE AUDIT** (`docs/VEHICLE_AUDIT.md`, register V0–V4, Vehicle dashboard tab) — one part at a
> time from a FRESH DUMP, ⛔ don't change engine/tank numbers on memory; the **UNDOCK button** (RENDEZVOUS + AUTO-DOCK
> removed) + the **DEORBIT NOW / WATER DEORBIT rescue buttons** (wired to `AbortControl.ForceDeorbit`); abort C1/C2
> FLIGHT-PROVEN (202127, 2 m/s splash). ⚠ **The attitude tunables (AttitudePilot softening/gains) are DB-tunable** —
> F1 fixed the wrong torque FORMULA (a bug, not a tuning value); now the real attitude gains can be tuned on a correct base.

**⭐ NEXT:** re-fly — (1) verify a **deorbit brings a vessel home** (F1); (2) press **AUTO SEQUENCE on a stranded
vessel** → confirm it resumes rendezvous/deorbit not launch (F2); (3) verify **rendezvous converges to dock** (F3);
then tune per phase to the DB. THEN the booster dual-flight (H1) + the vehicle audit fresh dump.

**⭐⭐ THE PROJECT RHYTHM (user, said with force): WIRE THE FULL MISSION SO EVERY PHASE RUNS AND COLLECTS DATA — failed
phases INCLUDED.** ⭐ **THE POST-FLIGHT LOOP, EVERY TIME (do ALL of it):** (1) full event-by-event pass on EACH new CSV
(`plugin/tools/assess_flight.py` + read KSP.log TOGETHER — never spot-check); (2) **feed the tuning DB** (`python
plugin/tools/tuning_db.py`); (3) **re-run the KLM audit** (`python dashboard/audit_kerbals.py`); (4) **update + PUBLISH
the dashboard** (verify it RENDERS first — serve locally + browser-console check); (5) update THIS file + the running
log; (6) diagnose → fix the REAL root (research × data × code; ⛔ NEVER ASSUME — verify every claim against the data;
guesses are LAST resort). ⭐ **`docs/ISSUE_REGISTER.md` = the exhaustive-audit ledger** ([[fix-everything-means-exhaustive]]):
every issue lives there with a status, removed only when FIXED **and** flight-verified. Keep auditing until it's empty.

**⭐⭐ THE WIN (flight-proven this session, Crew-2_20260828_202127 — a max-Q LaunchEscape abort): FIRST CREW TO SURVIVE.**
- **C1 chutes DONE✓:** the RealChute canopies NEVER deployed before (0 deploy activity → 127 m/s = dead). `Actuator.DeployChutePart`
  now fires the canopy's real "Deploy Chute" (GUIDeploy) event ONCE (was arm-only). Log confirms `DROGUES/MAINS activated`;
  the main inflated at 4.9 km → decelerated 121→8 m/s → **splash 2 m/s**. Crew alive.
- **C2 abort attitude DONE✓:** the separated capsule had ZERO control authority (`ctrl_tq≈0` → 17°/s tumble) because stock
  `ModuleRCS.GetPotentialTorque`=0 on a freshly-separated vessel (Dracos fire 21 kN; normal flight reads ~735). `AttitudePilot.ControlTorque`
  now falls back to a MechJeb-style geometric RCS-torque estimate when the reported RCS torque is ~0. Log shows it fired (4310 N·m/axis),
  `ctrl_tq` 0→1995, roll settled −8°/s. (Refinement owed: ~250°/s tumble at SuperDraco sep before the fallback recovers it.)

**⭐ ASCENT — reaches orbit, PROVEN. `att_err_deg`=ANGLE OF ATTACK, `att_point_deg`=POINTING error (att_point≈0 everywhere =
control is near-perfect; don't misread AoA/UPFG-lead as a control failure).** Fixes built + FLIGHT-DATA-confirmed:
- ✅ **SECO is 200×197 (near-circular)** — the "eccentric 200×160" was an `assess_flight.py` BUG reading the last S2Burn row
  ~0.3 s before cutoff (pe still rising 159→196); tool fixed. The vgo/inOrbit(pe≥195) cutoff works. inc 51.65 vs 51.6.
- ⚠ g still hit 4.65 (>4.5) with S2GLimitG=4.3 → now **4.1** (unflown; lag ~0.35). gravity-turn AoA ~8° = B9-tuner item.

**⭐ RENDEZVOUS→DOCK CHAIN — completion-blockers fixed (ALL UNFLOWN), so a full mission should now progress much further:**
- ✅ **H2 far-field:** removed the slow low-thrust CIRCULARIZE (it drifted the chaser 246→6000 km over ~27 orbits) — the far
  field now just DELIVERS to CW's regime (`FarPhase` Phase→Transfer→Coast) and **`CwHandoffRangeM` 50→100 km** so CW catches
  the transfer's ~80 km closest approach + does the terminal rendezvous (research §3). ⚠ verify CW converges from ~100 km.
- ✅ **B1 near-field CW / DockingControl** read the ISS **transform** position — a stale placeholder while the ISS is UNLOADED
  (from ~100 km down to ~2.5 km). Now use the **orbit position** (`getPositionAtUT`) until the target is `.loaded`; transform
  only for the final metres. This was the likely docking stall.
- ✅ `AutoAdvanceGates=true` verified — the mission auto-progresses gates G10–G14 (dock→undock→return), no crew-GO stall.
- ✅ deorbit burn now records Δv (`PutDv` was never called; also feeds `di.DvAppliedMps`, the guidance backstop, which was unset).
- Doc-rot fixed: Steering header said inner loop = SAS (it's AttitudePilot, live); ReturnControl said bank-entry not wired (it is).

**⛔⛔ THE NEXT BIG TASK — BOOSTER RECOVERY / "OUR PRE" (H1 in the register; Chris's clarification — do NOT re-misread this):**
- Chris ARMED booster recovery (195316/200639). At MECO the focus-switch to the booster fires correctly (my earlier
  BurnCommanded-gate fix 9201aa9 works). BUT: **PhysicsRangeExtender is NOT actually installed/wired** (no DLL, no log line),
  so a **NON-ACTIVE vessel goes ON-RAILS** — the upper stage only flew (and self-corrected its drift) when Chris manually
  FOCUSED it; the booster wouldn't flip/land even when focused. Result: **upper stage failed to reach orbit AND booster failed
  to land.** (⚠ the "ping-pong" in the fragment CSVs was CHRIS manually switching to investigate — NOT a bug. I earlier
  mis-guessed it as auto-focus and started a wrong "commit to booster" edit — REVERTED.)
- **THE FIX (dual-flight, next session):** the code comment says "can't fly both (stock control-input limit)" — but **MechJeb
  flies non-active LOADED vessels via `vessel.OnFlyByWire`** (mechjeb_src MechJebCore.cs:319/468/1055), so dual-flight IS
  possible: (1) **"our PRE" — extend the load/unpack ranges ourselves** via `VesselRanges` (no mod dep; PRE docs cap ~100 km
  for phantom-force reasons) to keep BOTH the Dragon + booster loaded/simulated; (2) keep the **Dragon ACTIVE** (its autopilot
  flies S2→orbit) and drive the **BOOSTER via `OnFlyByWire`** (BoosterControl on the non-active vessel) until they separate
  past the range (by which the booster should have landed downrange). (3) Fix **BoosterControl's flip/entry/land** (it did
  nothing even when focused). ⚠ Research the KSP `VesselRanges` API + OnFlyByWire before coding (guard every call).
- MonoPropellant 300 u = vestigial dead weight (base Tundra `ModuleRCSFX`; real Dracos are `ModuleEnginesRF` MMH+NTO) —
  confirm the RCS-translation path before removing it. [[dragon-return-propellant-mmh-nto]]

**⭐ KLM (Claude-owned scorecard, `dashboard/audit_kerbals.py`, counting from `20260828_103303`):** died 8 · stranded 8 ·
**abort-safe 4 (the first survivors)** · returning 8 · 0 home. Audit impact-detection fixed twice (was reading the ascent
climb / the main-deploy transient as the "impact"; now = touchdown speed at the lowest descending altitude). ⚠ owed: detect
REVERTS (195316/200639 ended at MECO = booster-armed setup, not real strandings — they slightly inflate stranded/survived).
The **KERBAL LIVES MATTER** banner is on the dashboard header. Live: https://claude.ai/code/artifact/9873fc17-efd8-4902-a029-67df25d3d783

**⭐ NEXT = FLY, then the booster redesign:**
1. **Full Dragon mission (toggle OFF)** — does the rendezvous now hand to CW → dock → undock → deorbit → entry → chutes → splash?
   (H2 + near-field + auto-gates should carry it much further; surface the best-guess SIGNS to flip — docking RCS, entry bank.)
2. **Then implement BOOSTER DUAL-FLIGHT** (our PRE + OnFlyByWire, above) + fix BoosterControl's land.
⛔ Getting it to RUN must NOT force the timeline — every MET emerges from physics (ULTIMATE_PLAN I-B.0).
⚠ After a PAD SAFE-ABORT, REVERT to launch (RealFuels spends the octaweb's one ignition; a re-engage shows 0% — not a bug).

**⭐ THE SHARED DASHBOARD (source of truth) — "I Smell What You're Stepping In"**
https://claude.ai/code/artifact/9873fc17-efd8-4902-a029-67df25d3d783 (source `dashboard/ismell.html`).
- **3-TICK verification ([[three-tick-system]]):** nothing is FACT until **CL** (Claude built, headless-green) +
  **YOU** (Chris approves) + **FLT** (proven in-game). Pure/math tops out at CL+YOU. Chris edits it in-browser; each
  edit self-republishes and PINGS this session → I re-read + investigate + confirm/dispute. ⛔ Concurrent edits cause
  publish conflicts — merge onto the live version (Artifact action:read), and force-publish ONLY on Chris's explicit OK.
- **KLM SCORECARD (Claude-owned, audited by `dashboard/audit_kerbals.py`) — CLEARED, counting from NOW** (user
  2026-08-28: "clear both, count from now on"). Baseline `dashboard/klm_since.txt` = `20260828_103303`; the audit
  takes a SINCE cutoff and counts only STRICTLY-newer flights → **currently 0/0/0/0/0** (the messy dev flights are
  excluded). Re-run `audit_kerbals.py` after new flights → it updates only from the reset forward. Walls = ONE big
  carved marble slab each (memorial / heroes), names DEDUPED (each kerbal once) with a **×tally** (times lost /
  missions completed); crew = Kimbrough/McArthur/Hoshide/Pesquet. 0 home climbing + an empty memorial = the autopilot
  getting good; the counter sits in the header as my scorecard.
- The user is **Chris** (nickname Muppet); "Seth" (the account email) is his SON — never call him Seth. [[user-chris]]

**⭐ OWED FLIGHT-GATED GLUE (do as the phase flies):** B8 targeting glue (CourseCorrect→BoosterTargeting/EntrySteering),
B9 AscentLoss→recorder + LaunchTuner→learned.cfg, B11 FDIR live-wiring, B2 estimator feed, B3 RcsBalance glue, KER
consumers (trust once cross-check agrees — it now does), the near-field CW transform-read (placeholder if ISS unloaded
past physics range — the likely near-field stall; watch the CSV), departure drift-leg warp.

Top plan `docs/ULTIMATE_PLAN.md` (Part I → Movement I-B = the tune loop); running log [[dragonscreen-autopilot-rebuild-plan]].
Live plan artifact: https://claude.ai/code/artifact/943d3d08-1124-432b-b474-1c33b5c29774

**⭐ APPROVED PIVOT (2026-08-28, user OK'd; user delegates the engineering calls but requires explicit permission
before changing the RULES or the PLAN'S COURSE).** Constraints: **(1) NO NEW flight tests until the whole autopilot
is built** (build-only through I-A). **(2) INITIAL tuning uses the UP-TO-DATE recordings + tuning DB now** (data-
backed start values, never invent); FINAL tuning is I-B. **Order = crew-survival first:** FATAL abort fix → B8
(entry) → B11 (FDIR) → B9/B10 → **B5 (PVG) LAST**.

**⛔ B5 REDEFINED — ALGLIB PERMANENTLY OFF THE TABLE.** A faithful MechJeb `PSG` port was REJECTED after reading the
source: PSG is a Hermite-Simpson collocation transcription solved by **ALGLIB `minnlc` SQP** (`alglib.minnlcoptimize`,
4000 iters, 120 s timeout, off-thread) — i.e. vendoring **~200k lines of ALGLIB** into the `-nostdlib` allocation-free
build, breaking the 60-FPS/minimal-build rules and unlike any bounded-time flight avionics. B5 instead = **real
primer-vector / PEG optimal ascent (PEGAS/UPFG lineage): analytic primer-vector steering + a small Newton BVP shooting
solve** (costates/burn-times/optimal-coast) — same solution class, deterministic, allocation-light, no NLP library,
the natural multi-stage upgrade of our UPFG. ~No-op for single-burn Crew-2 → sequenced LAST. (May need PEGAS-MATLAB
fetched; kOS PEGAS off-limits per NO-KOS.)

**✅ CREW-SURVIVAL ITEM #1 = the FATAL abort — FIXED (build), commit a9d2600.** Classified from the existing
recordings (NO new flight): both launch-escape aborts splashed ~122 m/s because the capsule slowed to ~42 m/s
under chute drag then ACCELERATED back to ~122 m/s (bare terminal) — the mains never inflated. ROOT (CSV+KSP.log,
one pass): the glue invoked RealChute's one-shot "Deploy Chute" EVERY tick (log: "…MAINS was activated in stage 1"
~50×/s at splash), restarting inflation so the canopy never finished. FIX = ARM the RealChute canopy once
(idempotent) and let RealChute stage it by altitude (`Actuator.DeployChutePart`); abort no longer CUTS the drogues
(kept as a backstop under the mains — `Chutes.SequenceAbort`); latch each arm (`AbortControl`/`ReturnControl`).
Green headless; ⚠ NOT flight-verified (no-new-flights constraint) — #1 to confirm in I-B (and re-enable the
fidelity drogue-cut once main deploy is confirmable).

**BUILT + committed earlier this run (headless-green ~490+ checks; DLL install DEFERRED to end of I-A — nothing
flies until the build is done, so one install before I-B, not per-item):** B1 StageStats, B2 q·α moderation
(+glue), B3 thrust/RCS balancer (+engine-out glue), B4 actuator-lag (+glue), B6 NavFilter, B7 Lambert + Maneuver,
+ the fatal-abort fix, + **B8 pure** (CourseCorrect 2×2/1×1 divert solve + Trajectory.EntryLdBand 4-band L/D
prior; 15+7 checks), + **B11 pure** (FDIR escalation ladder + free-flyer profiles verified; +6 checks).
✅ **B9 pure** (AscentLoss + LaunchTuner, 17 checks) + ✅ **B10 Tier-2** (dispersion now 5 families:
control/rendezvous/docking/return/FDIR) + ✅ **B5** (multi-stage UPFG / PEGAS virtual stages, ported verbatim
from PEGAS-MATLAB, validated by n=1-equivalence + a 2-stage point-mass closure). **✅✅ MOVEMENT I-A COMPLETE —
all 11 backlog items built + the fatal abort + the octaweb pad-abort fixed + mission-conductor (never-overshoot warp
+ booster focus) + KerBridge, ~725k headless checks/build, DLL installed. NEXT = the WORKFLOW above: get the mission
RUNNING end-to-end (first task), then the per-phase DB-tune LOOP, rinse-and-repeat until perfect.**
**⭐ TUNING RULE (user 2026-08-28):** every tunable (122, none unset) must carry a best educated-guess default;
where no corpus data exists, use the best researched/educated guess. The artifact tuned-tracker DISTINGUISHES
**DB-tuned** (corpus) from **best-guess** from **live** (self-tuning, e.g. B4). Only ascent control is DB-tuned so
far. **Then Movement I-B: flight-tune.** Owed in I-B (corpus-gated): B10 Tier-3 corpus-regression + Tier-4 MC;
**B9 glue** (integrate AscentLoss into the recorder + run LaunchTuner across flights → learned.cfg) + the B2 estimator FEED
(isolated aero angular-accel, sign-sensitive) + the B3 RcsBalance glue (rendezvous/docking) + **B8 targeting glue**
(wire CourseCorrect into BoosterTargeting/EntrySteering, keep the heuristic as fallback) + **B8 KSP-Euler
correction** (corpus-gated) + **B11 FDIR live-wiring** (observe-first into FlightDriver, then acting — plan Step I)
+ **the mission-conductor proactive coast auto-warp** (wire each coast controller's next-event UT into
`MissionConductor.WarpToEvent`; the framework + universal burn-guard are already in, commit d72ea53).

**⭐ NEW: mods as DATA SOURCES (`docs/MOD_INTEGRATION_RESEARCH.md`).** KER 1.1.9.5 is a live-data TREASURE CHEST
(per-stage Δv/TWR/burn-time/Isp/mass via `SimManager.Stages/LastStage` + suicide-burn + impact lat/lon/time + q/
Mach/terminal-v + phase/intercept angle). **Build `src/KerBridge.cs`** — a SOFT (reflection, optional, our pure
code as fallback) reader that feeds KER's proven RealFuels-accurate numbers into the vehicle model + Δv budgets
(highest value) and cross-checks Hoverslam/Trajectory/max-Q. ⛔ NO hard dependency (user policy). Mods now
researched: KER (integrate), PhysicsRangeExtender (partial — precondition for booster auto-recovery), KRE (reuse
hardware), BetterTimeWarpContinued (reference only — the never-overshoot warp is ours: `WarpPlan`+`MissionConductor`).

**⚠ From the 2026-08-28 flight analysis (7 flights, in `quarantine\dragonscreen_flightdata`, in the DB):**
phasing self-deorbit is FIXED (verified, pe held 177–179 km). Ascent defects: inc −5.1° (A1 UPFG-plane fix
installed, unflown), g 4.72 (min-throttle floor, safe), eccentric SECO 200×140 (re-assess post-A1). **⛔⛔ ABORT
recovery is FATAL — launch-escape aborts splash at ~122 m/s (mains under-decelerate); the abort TRIGGERS
perfectly in every regime but the crew doesn't survive.** High-priority I-B (return/chute phase). Flight tooling:
`plugin/tools/assess_flight.py` (CURRENT 89-col schema) + `tuning_db.py` (both read capture + the quarantine archive).

The stale "Stage 0 / PHASING SELF-DEORBIT is THE blocker" content below is SUPERSEDED — kept only for the rules
+ research references. The frame-of-mind + build-quality rules below all still apply.

---

```
Resume the RSS/RO Crew Dragon autopilot (DragonScreen, C:\Users\User\Desktop\DragonScreen). It is NOT named
"CLAUDE" yet — that name is EARNED by flying a full Crew-2 mission clean, pad→splashdown. It flies ANY crew
mission (mission-as-data by VAB craft name); Crew-2 is the reference profile.

════════ FRAME OF MIND (read every session — these are the anti-regression guardrails) ════════
• Real spacecraft flight software. Crew lives first; the vehicle must always be able to bring them home.
  FULL FIDELITY, no "safe/simplified" analogue, never ask "full or safe" (the answer is always full).
• ⛔ GROUND TRUTH beats the .md docs. Authority order: live ModuleManager.ConfigCache > the flight CSV +
  KSP.log > the research docs. Several .md numbers are stale. READ FILES IN FULL.
• ⛔ FLIGHT DATA FIRST. Read the recording (CSV) + KSP.log TOGETHER before proposing any cause. One
  disciplined root-cause pass; do NOT latch onto the first theory. Absence of an expected log line is
  evidence. This session alone, three "obvious" S2 theories (ullage / TEATEB / relight-count) were ALL wrong
  — the CSV (ullage=1, throttle=1, propellant present, 19 relights, 0 thrust) killed each one and the RealFuels
  readme gave the real answer. Let the data, not the hunch, drive the fix.
• ⛔ RESEARCH THE ENVIRONMENT before coding a mechanic. When a game/mod behaviour fights you, find the current
  authoritative source (mod README/source on GitHub, KSP version, installed mod versions) — you may be using
  an outdated process. Build: KSP 1.12.5, RealFuels 15.15.0, RO 18.0, TestFlight 2.12, FAR, Kopernicus, the
  "Sol" RSS-like system (Sol-Configs/Sol-Textures), MechJeb REMOVED, reaction wheels removed.
• ⛔ DIRECT PART CONTROL ONLY (hard rule): actuate live part modules by capability; NEVER StageManager or
  action groups. The ONE sanctioned exception is the RCS master toggle. Everything goes through src/Actuator.cs.
• ⛔ INSTRUMENT EVERYTHING the same pass you build it (FlightRecorder columns), but keep the FPS hit minimal.
  Never fly uninstrumented.
• ✅ BATCH reasoned fixes then fly to verify (NOT one change per flight) — but each flight must be fully
  recorded and read. Revert on the data if a batch regresses.
• ✅ You MAY commit + install autonomously once `cd plugin && python build.py test` is green and the change is
  reasoned (keeps a backup). A cfg or DLL change needs a FULL KSP restart (KSP + CKAN closed) to take effect.
• ⛔ Any new persistent glue state MUST reset on scene load (FlightDriver.Start→ResetAll / the idle branch).
• ⭐ BUILD-QUALITY RULES (2026-08-28, full 12 in [[build-verify-no-shortcuts]]): verify before reuse; verify +
  fix EVERY comment (they rot); comments precise BUT concise; build right the FIRST time — NO skim/shortcut
  (token-efficiency = terse CHAT + not-rebuilding, never a skimmed build); ⛔ NO KOS CODE (C# only; port kOS-ref
  logic into C#); units + frames ALWAYS explicit (our #1 bug class); fail LOUD never silent; pure/glue
  discipline; no magic literals (named [Tunable]+source); no dead code as live; guard every KSP API call.
• ⭐ 60 FPS BENCHMARK (rig: i5-14400F / GTX 1080 8GB / 16GB @ 1080p60): our mod must NEVER drop FPS <60 —
  allocation-free hot paths, cached lookups, efficient render + RT cameras, DXT-compressible textures (×4 dims).
• ⭐ TUNE ONE PHASE AT A TIME, in order, to REAL coupled targets (ascent+booster couple at MECO) — NOT an
  abstract orbit. STRICT IMPLEMENTATION fidelity (build the real nav pipeline + PVG). Don't invent+trust sims —
  proven methods + the recorded corpus (a corpus-calibrated C# forward-model is OK).
• ⛔ The deleted autopilot tree is OFF-LIMITS (empty locally; on GitHub only) unless the user says so.
• ⭐ TOP PLAN: docs/ULTIMATE_PLAN.md (whole mod: Part I autopilot first, Part II finish the mod). Backing:
  docs/AUTOPILOT_REBUILD_PLAN.md (§0.2 = the ordered build sequence). FIND ANYTHING via docs/INDEX.md.
  Memory: [[dragonscreen-autopilot-rebuild-plan]] = the running log (chronological; read latest).

════════ WHERE WE ARE (pad→orbit is SOLVED + prograde; the ONE blocker is PHASING self-deorbiting) ════════
✅ WORKS + PROVEN in flight (flight 214827, 2026-08-27, full-CSV+log pass, data verified IN and OUT):
  • Launch sequence + real erector procedure (move away → clear → ignite octaweb → clamp gate ≥99% → release).
    Gates AUTO-ADVANCED (CrewProcedureOps.AutoAdvanceGates=true, temporary test-fly convenience; set false to
    restore interactive gates).
  • S2 (MVac) IGNITION — SOLVED (throttle-0 vapor-lock reset). Lights ~2 s after MECO, burns to orbit.
  • ⭐ REACHES ORBIT, PROGRADE. inc 46.5° PROGRADE (was 116° retrograde — the v.north/v.east frame fix is
    CONFIRMED). Clean UPFG cutoff (tgo→0 at 0.21 s, NOT depletion): SECO at pe +178 / ap 200 km, inc 46.5.
    S2 burned 370 s. "plane LOCKED", az 43°.
  • S1 + attitude control NEAR-PERFECT: GravityTurn pointing p95 0.36°, S2Burn p95 0.078°, sat_duty ~0,
    max-Q 31.9 kPa, roll held (no barrel-roll). (Verified via the tuning DB.)
  • RCS OFF the entire S2 burn (Actuator.DisableRcs on ignition-confirm) — the gimbal steers S2. CONFIRMED.
  • CLEAN single separation: cut the MVac, wait for thrust to die, THEN decouple → no g spike. CONFIRMED.
  • Launch-window RAAN HOLD: holds + TimeWarp.WarpTo's to the plane-crossing window, then ignites in-plane.
  • ABORT SYSTEM — research-hardened to match real Crew Dragon (docs/ABORT_PROCEDURES_RESEARCH.md §G):
    7 modes chosen from live physical state, every path ends SAFE (splash / safe orbit / standoff). g-abort is
    a phase-aware BACKSTOP (6 g ascent/coast, DISABLED on entry where 4–8 g is nominal, 0.5 s window so a
    separation/staging jolt can't false-trigger). SuperDraco deorbit = FAST but g-limited (DeorbitGLimit 3.5 g)
    to a safe Pe. AbortToOrbit now completes the return home. DeorbitReturn commits within 120 s (never strands).
  • TUNING DATABASE built (tools/tuning_db.py) + recorder captures every control/authority signal. This flight's
    clean prograde ascent data is now in the ASCENT buckets (gold). Data verified IN and OUT. See [[dragonscreen-tuning-database]].
  • NAV 3D-globe map screen done; UI fixes (navball, flat map) done.

⛔ THE ONE BLOCKER — PHASING SELF-DEORBITS the capsule. After the good 178×200 km orbit, the rendezvous Phasing
  controller produced a GARBAGE solve (rv_range 13,322 km, rv_burn_dv 28,179 m/s — impossible) and pinned
  `trans_z = −1` (continuous retrograde Draco burn) with NO periapsis floor → pe dropped +178 → −143 km
  (suborbital) until the Dracos ran dry (met 843), then the crew aborted (DeorbitReturn). ROOT (data-confirmed,
  not guessed): the Phasing FSM applies a terminal/near-field solve to a far-field (13,000 km) geometry so dv
  blows up, AND there is no orbit-floor guard to refuse a burn that lowers pe below a safe floor. Same class as
  the earlier unloaded-ISS placeholder bugs — check how the target's state is read at 13,000 km separation.

⚠ SECONDARY (verified, lower priority):
  • inc UNDERSHOOT 46.5° vs 51.6° (~5° low) — the plane locked EARLY in the gravity turn at the achieved plane
    and never steered up to target. Fix the plane reference to the TARGET normal, or delay/raise the lock.
  • g-limit 4.73 g vs 4.5 target — 0.2 g overshoot at the final light-mass second of S2. Minor.
  • Abort water-scan finds 0/130 ground-track samples over water (idx=−1) → SafeLandingSite can't find ocean in
    RSS (TerrainAltitude / ground-track rotation). Falls back to the 120 s timeout. First-cut, not yet exercised
    to completion (CSV ended 26 s into the abort).
  • Target-RAAN readout logged as 0.0 (achieved 354.9) — verify the ISS RAAN is actually being read for the
    coplanar check.

════════ NEW RESOURCES + DECISIONS (2026-08-27 — the plan is now execution-ready) ════════
• Honest assessment done → the bar is "ROBUST with guaranteed abort-to-safe," NOT "flawless." Every research
  hole now has a resource (`AUTOPILOT_REBUILD_PLAN.md §0.1`): `VALIDATION_AND_ROBUSTNESS.md` (4-tier V&V, the
  buildable core is Tier-2 property-based dispersion of the pure layer), `CREW_DRAGON_GNC_RESEARCH.md` (docking
  gap is CONTROL not sensing — KSP gives exact relative state), `MISSION_PROFILES_FREEFLYER.md` (4 archetypes),
  `PHASE_ACCEPTANCE_CRITERIA.md` (per-phase pass gates), `ASCENT_GUIDANCE_DECISION.md`.
• MechJeb + mod capability integration: `MECHJEB_CAPABILITY_INTEGRATION.md` (P0–P3) + `MODS_HARVEST_2.md`
  (TCA `EngineOptimizer`/`RCSOptimizer` = the P0 RCS-balancer + differential-throttle solver; FAR = M_α; KER =
  ΔV/TWR). Standing method: DLL-only mod → read its GitHub source.
• USER DECISIONS: (a) Tier-4 C# Monte-Carlo ALLOWED — must reproduce ≥2 recorded flights first (proven-not-
  invented; [[no-python-simulations]] sharpened to that). (b) Ascent = UPGRADE UPFG (full inc/LAN cutoff), NOT
  a full PVG port. (c) Recommended next build = the P0 set (RCS balancer + differential octaweb throttle).

════════ DO NEXT (in order) ════════
⭐ THE TOP PLAN is `docs/ULTIMATE_PLAN.md` (whole mod: Part I autopilot first, Part II finish the mod); this
handoff + `AUTOPILOT_REBUILD_PLAN.md §0.2` are its autopilot backing.
⭐ FIND ANYTHING via `docs/INDEX.md` — the master catalog of every research doc, data file, tool, external
source + the flight corpus (organised by purpose, freshness-tagged). Grep it before assuming something's missing.
⭐ THE ORDERED BUILD PATH is `AUTOPILOT_REBUILD_PLAN.md §0.2 MASTER BUILD SEQUENCE` (Stage 0 foundations →
ascent → booster → nav-pipeline → rendezvous → docking → return → hardening → proving run; SCREENS/CONSOLE in
parallel per `SCREENS_CONSOLE_PLAN.md`).

⭐⭐ START THE BUILD HERE — STAGE 0 (foundations), three parts:
  • 0a — VERIFY THE INSTALLED FIXES in flight (details in step 1 below): fly pad→orbit→phasing; confirm inc
    prograde, phasing pe RISES/holds (never falls), attitude roll bounded. Full structured CSV+log pass +
    `python tools/tuning_db.py`. This is the gate before new capability work.
  • 0b — BUILD THE TIER-2 DISPERSION HARNESS (`VALIDATION_AND_ROBUSTNESS.md`): headless-C# property-based
    dispersion of the pure layer, asserting the crew-safety invariants (e.g. pe never < floor for ANY rendezvous
    geometry; FDIR always ends safe). Build it early — it catches bugs before expensive flights.
  • 0c — BUILD THE ASCENT/BOOSTER P0 CAPABILITIES (what the first-tuned phases need): UPFG inc/LAN cutoff (PEGAS
    target-plane-normal method — fixes the inc undershoot, `AUTOPILOT_MINING_3.md §1a`); engine-out differential
    octaweb throttle + RCS balancer (TCA solvers, `MODS_HARVEST_2.md §1`); AoA/q·α moderation; StageStats
    (MECO reserve); actuator-lag. Then Stage 1 (ascent to REAL orbit + booster-recoverable MECO, together).
  Build order for everything after: ULTIMATE_PLAN Part I Stages 1→8, Part II screens in parallel.

1. ✅ PHASING SELF-DEORBIT FIXED + INSTALLED (unflown) — FLY to verify (this IS Stage 0a). Root cause was CW (linearised, valid
   only to tens of km) run at the 13,000 km far field → 28 km/s garbage → pinned retrograde Draco burn → self-
   deorbit. FIX (headless-green): (a) NEW pure/Phasing.cs — far field is a PROGRADE-ONLY co-elliptic raise
   (prograde raises the orbit → it can NEVER deorbit) until ~10 km below the station, then coast; (b) far/near
   split in src/RendezvousControl.cs at CwHandoffRangeM=50 km (far = prograde raise, near = CW as before);
   (c) robust range from the target ORBIT not its unloaded-transform placeholder (that placeholder is what read
   13,000 km); (d) a HARD PE FLOOR (Phasing.PeSafe, SafePeFloorM=150 km) gating EVERY burn — a wrong ForwardSign
   or a garbage solve is caught at 150 km, not at re-entry; (e) a CW-validity guard in pure/Rendezvous.cs
   (CwMaxRangeM=200 km) so the solver itself never emits a far-field burn. ⚠ VERIFY in the CSV: in PHASING, pe
   RISES toward ~410 km (co-elliptic) or at worst holds — it must NEVER fall; trans_z is not pinned; if pe FALLS,
   ForwardSign (−1) is flipped. Also ATTITUDE: the crew now ride BACKS-TO-SKY — ascent roll ref changed from the
   sideways cross-track (up×aim) to RADIAL-OUT projected ⊥ nose (src/AscentControl.cs); verify roll stays bounded
   (no barrel-roll) and inc still ~46.5 prograde — one-line revert to Vector3d.Cross(up,aim) if roll misbehaves.
2. THEN the inc undershoot (46.5 vs 51.6, ~5° low): plane locked EARLY at the achieved plane; fix the plane
   reference → the TARGET normal, or lock later. Check RAAN vs the ISS; if ~180° off, flip FlightDriver.LaunchNodeSign.
3. Once PHASING holds/raises a stable orbit + reaches the AI standoff: continue per docs/AUTOPILOT_REBUILD_PLAN.md
   §4 — docking → deorbit/return/entry/chutes, each flown for the first time and tuned from its recording + the DB.
4. Tuning is DATA-DRIVEN via the DB. ASCENT control data is clean+prograde (gold). DB GAP still open: the broken-
   flight heuristic catches broken POINTING but NOT a pinned TRANSLATION self-deorbit if pointing stays clean —
   add a trans-sat / pe-floor contamination check to tuning_db.py. ALWAYS verify data (recorder IN + tool OUT).

════════ HOW TO WORK ════════
• Build/test: `cd plugin && python build.py test` (headless C# suites ARE the certification; ~4000 checks).
  Install: `python build.py install` (needs KSP + CKAN closed; full restart to load). Preview screens without
  the game: `python build.py preview`.
• Config patches live in GameData/Crew2_Patches (standalone, NOT in the repo): F9_S1_reliability (TestFlight
  ignition), F9_S1_TEATEB (ignitor fluid, now 100 units S1+S2), F9_Engines_InstantSpool (throttleResponseRate
  1e6 + ignitions=-1 = RF unlimited default). F9_S1_GimbalRange is DISABLED (reverted to stock 2°).
• The pure layer (src/pure) is KSP-free + headless-tested — the model. The glue (src/*.cs) is where bugs live;
  fully record every glue flight. NO Python physics sims — validate with the C# tests + the flight corpus.
• Between flights, ALWAYS propose the proper next step from the data; never fly blind.
```

---

*Keep this file current: when the S2 fix is verified (or the next blocker is found), rewrite "WHERE WE ARE"
and "DO NEXT". The memory file [[dragonscreen-autopilot-rebuild-plan]] holds the full flight-by-flight log.*
