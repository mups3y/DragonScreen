# DragonScreen — MASTER INDEX (the referencing system)

> **The catalog of everything: every research doc, plan, data file, tool, external source, and the flight
> corpus.** Read this to FIND the right resource fast; grep it before concluding something doesn't exist.
> Organised by purpose. Freshness tag: **[ACTIVE]** = governing instruction (allowlist only) · **[CURRENT]** = live/authoritative · **[REF]** = stable reference/
> research · **[SCREENS]** = UI side · **[HIST]** = historical/F9I-era, background only (don't build from it) · **[SUPERSEDED]** = obsolete instruction plans, **deleted** from the repo (recoverable via git history; do not resurrect).
> Keep this in sync: add a line whenever a doc is created. Sibling index for ART assets: `ASSET_INDEX.md`.

---

## 0. START HERE — governing (the ACTIVE allowlist; nothing else is an instruction)
- **[MASTER_BUILD_SPEC.md](MASTER_BUILD_SPEC.md)** [ACTIVE] — ⭐ THE sole governing specification. Overrides every other doc automatically. Read first.
- **[SCREEN_SPEC.md](SCREEN_SPEC.md)** [ACTIVE] — the single screen spec (IA, components, page inventory, visual language); cannot override the master.
- Supporting ACTIVE docs: **[COMPLETION_MATRIX.md](COMPLETION_MATRIX.md)** · **[SOURCE_OF_TRUTH.md](SOURCE_OF_TRUTH.md)** · **[TELEMETRY_REGISTRY.md](TELEMETRY_REGISTRY.md)** · **[COMMAND_REGISTRY.md](COMMAND_REGISTRY.md)** · **[SCREEN_EVIDENCE_MATRIX.md](SCREEN_EVIDENCE_MATRIX.md)** · **[FLIGHT_VERIFICATION.md](FLIGHT_VERIFICATION.md)** · **[DEPENDENCY_MATRIX.md](DEPENDENCY_MATRIX.md)**.
- **INDEX.md** [CURRENT] — this file.
- Handoffs/assessments (HISTORICAL evidence, not instruction): `RESUME_PROMPT.md`, `SESSION_HANDOFF.md`, `GROK_ASSESSMENT_PROMPT.md`, `CHATGPT_ASSESSMENT.md`, `ASSESSMENT_VERIFICATION.md`.
- **SUPERSEDED (deleted from the repo — only the ACTIVE plan exists; in git history):** `ULTIMATE_PLAN`, `AUTOPILOT_REBUILD_PLAN`, `MASTER_FIX_PLAN`, `CAMPAIGN_PLAN`, `CAPABILITY_BUILD_BACKLOG`, `RETURN_FIX_PLAN`, `NOMINAL_END_TO_END_BUILD`.
- Memory: `~/.claude/.../memory/MEMORY.md` (index) + running log (read latest).

## 1. Architecture & method (how to build)
- **[TRUE_AUTOPILOT_ARCHITECTURE.md](TRUE_AUTOPILOT_ARCHITECTURE.md)** [REF] — how to build a true autopilot; the §13 completion criteria (the definition of done).
- [FLIGHT_SOFTWARE_PLAN.md](FLIGHT_SOFTWARE_PLAN.md) [REF] — why tailor-made vs referencing mods (the API rationale).
- [FLIGHT_SYSTEMS.md](FLIGHT_SYSTEMS.md) [REF] — reuse-not-reinvention survey.
- [ARCHITECTURE.md](ARCHITECTURE.md) [SCREENS/HIST] — F9I Dragon-screen architecture (screens-era).

## 2. Validation & workflow
- **[VALIDATION_AND_ROBUSTNESS.md](VALIDATION_AND_ROBUSTNESS.md)** [CURRENT] — the 4-tier V&V; Tier-2 dispersion harness is the buildable core; Tier-4 C# Monte-Carlo (corpus-calibrated first).
- **[PHASE_ACCEPTANCE_CRITERIA.md](PHASE_ACCEPTANCE_CRITERIA.md)** [CURRENT] — per-phase clean-flight signature + PASS gates + failure signatures (the fast in-flight check).
- [OPERATING_PROCEDURE.md](OPERATING_PROCEDURE.md) [REF] — operating procedure.

## 3. Autopilot harvest / mining (WHERE THE ALGORITHMS COME FROM — the mod-source gold)
- **[AUTOPILOT_HARVEST.md](AUTOPILOT_HARVEST.md)** [CURRENT] — round 1: MechJeb2 + AtmosphereAutopilot, §A–§O (attitude law, thrust/ullage/staging, rendezvous/docking/landing, AoA moderation, loss decomposition).
- **[MODS_HARVEST_2.md](MODS_HARVEST_2.md)** [CURRENT] — round 2: TCA (engine/RCS balancing solver), KerbalEngineer (ΔV/TWR), MFI (aero hook), FAR (Cl/Cd/Cm, M_α) + full install scan.
- **[AUTOPILOT_MINING_3.md](AUTOPILOT_MINING_3.md)** [CURRENT] — round 3: PEGAS (⭐ the inc-undershoot plane-normal fix; virtual stages/LJSQ), KSPTrajectories (reentry predictor + AoA schedule), GravityTurn (LaunchDB auto-tuner + loss metric).
- **[MECHJEB_CAPABILITY_INTEGRATION.md](MECHJEB_CAPABILITY_INTEGRATION.md)** [CURRENT] — the full ✅/⚠/❌ capability inventory sequenced P0–P3 into the build.
- [ATTITUDE_CONTROL_RESEARCH.md](ATTITUDE_CONTROL_RESEARCH.md) [REF] — the BetterController gimbal-loop port (frame, arrestable-rate, negative actuation).
- [MECHJEBLIB_PORT.md](MECHJEBLIB_PORT.md) [REF] — the FuelFlowSimulation port status (done → StageStats).
- [MECHJEB_WIKI_RESEARCH.md](MECHJEB_WIKI_RESEARCH.md) [REF] — MechJeb attitude PID numbers reconciled (source > stale wiki).

## 4. Guidance — ascent
- **[LAUNCH_AND_ASCENT_RESEARCH.md](LAUNCH_AND_ASCENT_RESEARCH.md)** [REF] — the ascent technique + profile (zero-AoA gravity turn, azimuth, window).
- [ASCENT_GUIDANCE_UPFG.md](ASCENT_GUIDANCE_UPFG.md) [REF] — UPFG/PEG, the real fix.
- **[ASCENT_GUIDANCE_DECISION.md](ASCENT_GUIDANCE_DECISION.md)** [CURRENT] — PVG-vs-UPFG decision: upgrade UPFG now (inc/LAN cutoff), PVG the committed strict-fidelity target.

## 5. Guidance — phases (booster → return)
- [PHASE_2_BOOSTER_RECOVERY_RESEARCH.md](PHASE_2_BOOSTER_RECOVERY_RESEARCH.md) + [BOOSTER_GUIDANCE_DESIGN.md](BOOSTER_GUIDANCE_DESIGN.md) [REF] — booster entry burn / grid-fin / hoverslam.
- **[BOOSTER_DUAL_FLIGHT_RESEARCH.md](BOOSTER_DUAL_FLIGHT_RESEARCH.md)** [CURRENT] — ⭐ the dual-flight: OnFlyByWire drives any UNPACKED non-active vessel (feasibility RESOLVED); "our PRE" (`src/RangeExtender.cs`, VesselRanges ported from PhysicsRangeExtender) + the PRE-on-before-sep / focus-booster / refocus / PRE-off design (`MissionConductor.TickBoosterRecovery`); the phantom-force risk + the H1 flight-verify list.
- [PHASE_3_RENDEZVOUS_RESEARCH.md](PHASE_3_RENDEZVOUS_RESEARCH.md) [REF] — named-burn rendezvous cascade + the AI standoff.
- [RENDEZVOUS_RESEARCH_2026-08-20.md](RENDEZVOUS_RESEARCH_2026-08-20.md) [HIST] — the flight_0820 tank-dry post-mortem (F9I-era lesson).
- [PHASE_4_DOCKING_RESEARCH.md](PHASE_4_DOCKING_RESEARCH.md) [REF] — L-approach corridor + capture.
- [PHASE_5_UNDOCKING_DEPARTURE_RESEARCH.md](PHASE_5_UNDOCKING_DEPARTURE_RESEARCH.md) [REF] — undock + departure burns.
- [PHASE_6_DEORBIT_ENTRY_SPLASHDOWN_RESEARCH.md](PHASE_6_DEORBIT_ENTRY_SPLASHDOWN_RESEARCH.md) [REF] — deorbit, bank-angle entry, chutes, splashdown.
- **[ABORT_PROCEDURES_RESEARCH.md](ABORT_PROCEDURES_RESEARCH.md)** [CURRENT] — every-regime abort + §G researched g-loads (the loved abort system).

## 6. GN&C / sensors / nav pipeline
- **[CREW_DRAGON_GNC_RESEARCH.md](CREW_DRAGON_GNC_RESEARCH.md)** [CURRENT] — real Dragon sensors/nav; §5 = the strict-fidelity NAV PIPELINE (L1.5 NavFilter) to build.

## 7. Mission data / telemetry (WHAT TO MATCH)
- [REAL_CREW_DRAGON_MISSION.md](REAL_CREW_DRAGON_MISSION.md) [REF] — the real mission end-to-end.
- [CREW2_REAL_MISSION_TECHNIQUES.md](CREW2_REAL_MISSION_TECHNIQUES.md) + [CREW2_RSS_RESEARCH.md](CREW2_RSS_RESEARCH.md) [REF] — Crew-2 techniques + the RSS/RO timeline to the second.
- [CREW_MISSION_TELEMETRY.md](CREW_MISSION_TELEMETRY.md) [REF] — the telemetry-DB research/reconstruction.
- **[MISSION_PROFILES_FREEFLYER.md](MISSION_PROFILES_FREEFLYER.md)** [CURRENT] — the 4 archetypes (ISS/high-circular/elliptical/polar) + free-flyer capability gaps.
- Data: **`data/crew_missions.json`** (the 20-mission DB) · `data/dm1_ascent_template.json` + `dm1_ascent_full.json` (DM-1 raw ascent telemetry).

## 8. Environment / mods / vehicle (GROUND TRUTH)
- **[INSTALLED_MODS_RESEARCH.md](INSTALLED_MODS_RESEARCH.md)** [CURRENT] — how the RSS/RO install behaves (no reaction wheels, FAR transonic, RealFuels, RealChute, TestFlight). §6a = the orchestration mods.
- **[MOD_INTEGRATION_RESEARCH.md](MOD_INTEGRATION_RESEARCH.md)** [CURRENT] — ⭐ installed mods as DATA SOURCES: KER's live-sim API (per-stage Δv/TWR/burn-time + suicide-burn + impact + q — the treasure chest) to SOFT-integrate, PRE, BetterTimeWarp (ref), KRE. Policy: no hard deps; read KER by reflection with our pure fallback.
- **[MOD_INVENTORY_RESEARCH.md](MOD_INVENTORY_RESEARCH.md)** [CURRENT] — the FULL triage of all 123 installed mods (useful / covered / infra / cosmetic). Useful+new: KSPCommunityFixes (validates our GetPotentialTorque authority), EngineGroupController, KSPCommunityPartModules, CustomPreLaunchChecks. So no mod is un-swept.
- [RO_RSS_ENVIRONMENT.md](RO_RSS_ENVIRONMENT.md) [REF] — real scale / atmosphere / aero / heating.
- [RO_MODS_MECHANICS.md](RO_MODS_MECHANICS.md) [REF] — what the guidance MUST obey.
- [RO_TESTFLIGHT_MECHANICS.md](RO_TESTFLIGHT_MECHANICS.md) [REF] — engine ignition/ullage/reliability mechanics.
- **[CRAFT_DUMP_VEHICLE_MAP.md](CRAFT_DUMP_VEHICLE_MAP.md)** [CURRENT] — every actuable module (control by capability). Data: **`data/craftdump.csv`**.
- **[VEHICLE_AUDIT.md](VEHICLE_AUDIT.md)** [CURRENT, ⭐HIGH PRIORITY] — the "perfect control" ledger: every RCS mode / thruster limit / engine mode / throttle / fuel type / tank / load, per phase (§B control matrix, VERIFIED from code) + the real-world Falcon 9/Crew Dragon accuracy audit (§C/§D), done one part at a time from a fresh dump (§A procedure, §E checklist).
- **[SEQUENCE_MAP.md](SEQUENCE_MAP.md)** [CURRENT, ⭐] — the mission phase sequence + **§1A the NOMINAL PARAMETER ENVELOPE per phase** (the real Crew Dragon numbers the autopilot must hold to be called nominal — the numeric form of `PHASE_ACCEPTANCE_CRITERIA`, incl. the IDSS soft-capture contact box) + all alternate paths + the ABORT DECISION MATRIX per phase (grounded in the real Crew Dragon 8-mode abort structure) + the STATE-AWARE RESUME map (how AUTO SEQUENCE works out where it is + what to do next — rendezvous vs deorbit, never re-launch). Drives `CrewProcedureOps.ResumeIndex`.

## 9. Screens / UI (the DragonScreen side)
- **[SCREENS_LOOK_AND_FUNCTION_RESEARCH.md](SCREENS_LOOK_AND_FUNCTION_RESEARCH.md)** [CURRENT] — ⭐ the comprehensive record of how the screens should LOOK + FUNCTION: all resources (3 Figma CC-BY files + Vue live-demo + iss-sim + more), the real page set + function, real→our page map, buildable functions (leak test), the hidden docking mini-game, licences.
- **[SCREENS_CONSOLE_PLAN.md](SCREENS_CONSOLE_PLAN.md)** [CURRENT] — the screens/console workstream: NAV bugs, button-wiring audit, per-page feature audit, error hunt. ⛔ keep the abort display.
- [REAL_DRAGON_SCREENS.md](REAL_DRAGON_SCREENS.md) [REF] — how the real Crew Dragon screens + console are arranged (the button transcript source).
- [REFERENCE_PAGES.md](REFERENCE_PAGES.md) [REF] — the 8 reference pages we own + the live-demo findings.
- [UI_AUDIT.md](UI_AUDIT.md) [REF] — the reference UI as its source describes it.
- [MAP_MFD_RESEARCH.md](MAP_MFD_RESEARCH.md) [REF] — the map/3D-globe nav-screen research + build plan.
- [IVA_TARGET.md](IVA_TARGET.md) [SCREENS] — the IVA target surface (Tundra props).
- [STATE_CONTRACT.md](STATE_CONTRACT.md) [SCREENS/HIST] — the F9I screen state contract v0.
- [PALETTE.md](PALETTE.md) [REF] — the verified colour palette. · [ASSET_INDEX.md](ASSET_INDEX.md) [CURRENT] — the generated art-asset catalog.

## 10. Tools, data & the flight corpus (working resources)
- **`plugin/build.py`** — `test` (headless certification, ~4000 checks) / `install` / `preview` (PNG screens without the game).
- **`plugin/tools/tuning_db.py`** — the control-tuning DB builder → `docs/tuning/TUNING_DB.{json,md}` + `exclude.txt`. Re-run after every flight. See [[dragonscreen-tuning-database]].
- **`assess_flight.py`** / the flight-data tooling — read a whole flight in one command. See [[dragonscreen-flight-data-tooling]].
- **`<KSP>/DragonScreen_capture/Crew-2_*.csv`** — the recorded FLIGHT CORPUS (ground truth for validation) + `KSP.log`.

## 11. External sources (NOT in the repo — read from disk / GitHub; standing method: DLL-only mod → read its GitHub)
- **`Desktop/mechjeb_src`** — full MechJeb2 + MechJebLib C# source (the primary KSP-guidance reference).
- GitHub (mined, `AUTOPILOT_MINING_3.md`/`MODS_HARVEST_2.md`): [allista/TCA](https://github.com/allista/ThrottleControlledAvionics) · [jrbudda/KerbalEngineer](https://github.com/jrbudda/KerbalEngineer) · [sarbian/MFI](https://github.com/sarbian/ModularFlightIntegrator) · [ferram4/FAR](https://github.com/ferram4/Ferram-Aerospace-Research) · [Noiredd/PEGAS](https://github.com/Noiredd/PEGAS) · [neuoy/KSPTrajectories](https://github.com/neuoy/KSPTrajectories) · [linuxgurugamer/GravityTurn](https://github.com/linuxgurugamer/GravityTurn).
- `Desktop/removed_autopilot_mods/` — the removed MechJeb2 + AtmosphereAutopilot (harvested, then removed so only ours flies).

## 12. F9I (PRIOR kOS project — background only, do NOT build from these)
- [F9I_BOOSTER_TARGETS.md](F9I_BOOSTER_TARGETS.md) · [F9I_PORT_MAP.md](F9I_PORT_MAP.md) [HIST] — the F9I kOS booster numbers + port map. Superseded by the C# rebuild; keep as reference only.
