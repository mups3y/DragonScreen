# Next-session resume prompt — DragonScreen autopilot

Paste the block below at the start of the next session. It puts you in the right frame of mind and points you
at the one thing that matters next.

---

## ⭐⭐ RESUME HERE (2026-08-28, post-compaction) — MODE IS BUILD

**The autopilot is NOT "build complete" — a first-cut SKELETON flies (pad→orbit→phasing, verified), but the
researched-method BACKLOG is what makes it advanced. USER DECISION: build the full feature set first (ULTIMATE_PLAN
Movement I-A, dependency-ordered B1–B11, pure-first + headless + committed + installed), THEN flight-tune
phase-by-phase (I-B).** Top plan `docs/ULTIMATE_PLAN.md`; detail `AUTOPILOT_REBUILD_PLAN.md §0.2`; running log
[[dragonscreen-autopilot-rebuild-plan]]. Live plan artifact: https://claude.ai/code/artifact/943d3d08-1124-432b-b474-1c33b5c29774

**⭐ FIRST TASK THIS SESSION (user directive): B5 — PVG / virtual-stages.** It is the MechJebLib `PSG`
Pontryagin/primer-vector OPTIMAL-CONTROL ascent solver (`Desktop/mechjeb_src/MechJebLib/PSG/*`, ~2900 lines
across 13 files + deps: `MechJebLib.ODE` integrator, `Optimizer.cs` 513-line BVP solver, `AscentProblem.cs`
556-line costate dynamics, `Primitives`/`Functions`/`Interpolants`). The LARGEST backlog item — a MULTI-PASS
port: ODE integrator → phase/vehicle model → costate dynamics → optimizer → verify vs MechJeb `AscentTests`,
committing each pass. It's an optimization of the already-working UPFG ascent (a no-op for single-stage Crew-2),
so it was sequenced last; do it FIRST this session per the user.

**BUILT + committed + installed this run (headless-green ~490+ checks, DLL live):** B1 StageStats, B2 q·α
moderation (+glue), B3 thrust/RCS balancer (+engine-out glue), B4 actuator-lag (+glue), B6 NavFilter, B7 Lambert
+ Maneuver. **REMAINING after B5:** B8 entry-predictor upgrade (Trajectories KSP-Euler correction + 4-band AoA +
course-correction 2×2), B9 GravityTurn LaunchDB auto-tuner, B10 V&V (Tier-2 more families + Tier-3 regress +
Tier-4 MC), B11 FDIR full authority + free-flyer profiles. **Then Movement I-B: flight-tune.** Owed in I-B: the
B2 estimator FEED (isolated aero angular-accel, sign-sensitive) + the B3 RcsBalance glue (rendezvous/docking).

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
