# Full Installed-Mod Inventory — the 123 GameData folders, triaged (2026-08-28)

> Purpose: a COMPLETE sweep of everything installed, so nothing useful is missed. Each mod is placed in one of
> four buckets: **[COVERED]** already researched elsewhere (linked), **[USEFUL]** control/data/hardware relevance
> → researched here, **[INFRA]** framework/dependency (no autopilot logic), **[COSMETIC]** visual/audio (no control
> effect). Policy stands: NO hard mod dependencies — soft-integrate data (KER), build behaviour in.

---

## A. [USEFUL] — control / data / hardware relevance (researched this pass)

### A1. KSPCommunityFixes (KCF) — ⭐ control-critical, VALIDATES our attitude loop
A large stock-bug-fix mod (Harmony patches). The ones that matter to us:
- ⭐ **`GetPotentialTorque` fixes (gimbals + wheels: `WheelsPotentialTorque`).** Stock `ITorqueProvider.
  GetPotentialTorque` reports garbage for gimbals/wheels; KCF corrects it. Our `AttitudePilot` sums
  `GetPotentialTorque` for the control-authority `controlTorque` (the arrestable-rate law, actuation =
  −torque/controlTorque). **KCF is WHY we can trust that number** — without it the gimbal torque authority would
  be wrong and the loop would mis-scale. (Already flagged in `ATTITUDE_CONTROL_RESEARCH.md`; this records the full
  dependency.) ⇒ keep KCF; if it were ever removed, our torque authority must fall back to a measured estimate.
- **`RoboticsDrift` / `RoboticPartLock*`** — fixes Breaking-Ground robotic-part drift + lock states. The Dragon's
  `AdjustableCoMShifter` is a moving/robotic-style part; this keeps its offset stable (no creep) — relevant to the
  entry L/D trim we set once and hold.
- **Physics / `PhysicsDT` / `PhysicsDTPerFrame`** — physics-timestep fixes; they make the sim (and thus our
  measured accel / control) more deterministic. No action, just soundness.
⇒ **No integration to build** — KCF is an environment guarantee our control layer already relies on. Document it.

### A2. EngineGroupController (EGC) — present on every engine; we bypass it
`AddModule.cfg` bolts `EngineGroupModule` onto EVERY liquid engine (`@PART[*]:HAS[@MODULE[ModuleEngine*]...]`). It
lets engines be assigned to action-group "groups" for independent throttle/shutdown. ⛔ **We do NOT use it** — the
octaweb + differential throttle are actuated DIRECTLY per-engine (`Actuator`, `ModuleEnginesRF` by engineID,
`ModuleTundraEngineSwitch` for the mode). EGC's module sits inert alongside ours (no conflict). Worth knowing it is
on every engine so a future part scan isn't surprised by it. No integration.

### A3. KSPCommunityPartModules (KCPM) — `ModuleAutoCutDrogue`, INERT here
Adds community part modules; the relevant one is `ModuleAutoCutDrogue` ("Auto-Cut Drogue Chute(s)") on the Dragon
drogues — BUT it is gated `MODULE:NEEDS[KSPCommunityPartModules&!RealChute]`, and **RealChute IS installed**, so
KCPM's auto-cut is stripped and RealChute owns the drogues (see the abort-chute fix). ⇒ no effect on our chute
logic; noted so the `!RealChute` gating is understood.

### A4. CustomPreLaunchChecks (CPLC) — ⭐ a HARD RO-dep → take DIRECT control (do NOT reinvent)
A **KSP-RO** project (`github.com/KSP-RO/CustomPreLaunchChecks`), bundled with RP-1/RO → effectively always present.
It detours `EditorLogic.GetStockPreFlightCheck` to inject RO/RP-1 launch-readiness `IPreFlightTest`s (avionics/
control/crew) into KSP's STOCK pre-flight system. ✅ **CORRECTION to my earlier flag:** it gates the **VAB→pad
LAUNCH**, NOT on-pad ignition — so it does NOT block our AUTO SEQUENCE (the 2026-08-28 vehicle was on the pad and
DID ignite). ⭐ **Per the mod-dependency policy it is a HARD dep → we USE it directly**, feeding the stock/CPLC
launch-readiness into the **crew-screen GO/NO-GO** display rather than reimplementing avionics checks. Full detail +
the integration approach in **`MOD_INTEGRATION_RESEARCH.md §4b`**; the hat-on-a-hat audit is §6 there.

### A5. Launch site / targeting statics (booster + entry targets) — [also COVERED in part]
`KerbalKonstructs` + `TundraSpaceCenter` + `CanaveralPads` + `ModularLaunchPads` (the erector
`TE_Ghidorah_Erector`) + **`Space_X_barge_lander` (the DRONESHIP)** provide the launch pad, the RTLS pads, and the
droneship our `BoosterTargeting` aims at (marker "Droneship"). ⇒ already used; recorded together here as the
targeting-static set. `HabTech2`/`HabTechProps` = the ISS station parts (the rendezvous/docking TARGET) +
`htRobotics` = Canadarm2 station hardware (not our vehicle). No integration beyond finding the target vessel.

### A6. ROHeatshields / RealHeat — entry (already partly covered)
`ROHeatshields` (PICA-X ablative shield) + `RealHeat` (real reentry heating). Relevance: the entry phase must keep
the heat shield into the flow (our `Entry`/`ReturnControl` hold shield-forward) and the shield ablates — the L/D +
bank-angle entry is what keeps heating survivable. Covered by `INSTALLED_MODS_RESEARCH` + `PHASE_6_*`; noted for
completeness. `ProceduralFairings` = the fairing jettison (q<5 kPa gate) — actuated by decoupler capability.

---

## B. [COVERED] — already researched (see the linked doc)
- **RealismOverhaul, FerramAerospaceResearch (FAR), RealFuels, SolverEngines, ROEngines, AJE, RealChute, TestFlight,
  KerbalJointReinforcement, RealHeat, ModularFlightIntegrator** → `INSTALLED_MODS_RESEARCH.md`.
- **Kopernicus + RSS (`Sol-Configs`, `RSS-CanaveralHD`, `KSCSwitcher`, `RSSDateTime`, `AdvancedPQSTools`)** → the
  RSS environment (`INSTALLED_MODS_RESEARCH.md §6`, `RO_RSS_ENVIRONMENT.md`).
- **TundraExploration + `zzz_TundraRO_Fixes` + `ROCapsules`/`ROTanks`/`ROSolar` + `Crew2_Patches`** → the vehicle
  (`CRAFT_DUMP_VEHICLE_MAP.md`, `crew_missions.json`).
- **ThunderAerospace (TAC-LS)** → life support (`dragonscreen-tac-life-support`).
- **KSPWheel** → the landing legs; **KerbalReusabilityExpansion** → grid fins/legs/cold-gas; **KerbalEngineer**,
  **PhysicsRangeExtender** → `MOD_INTEGRATION_RESEARCH.md`. **AtmosphereAutopilot / MechJeb2** → removed/reference
  (`AUTOPILOT_HARVEST.md`, `mechjeb-source-reference`).

## C. [INFRA] — frameworks / dependencies (no autopilot logic)
`000_AT_Utils, 000_ClickThroughBlocker, 000_Harmony, 000_KSPBurst, 001_ToolbarControl, 999_Scale_Redist,
ModuleManager*, CommunityResourcePack, CommunityCategoryKit, PatchManager, KerbalChangelog, LocalFixes,
PEKKAsMods, REPOSoftTech, ROUtils, ROLib, RONoCareer, RP-1-ExpressInstall, B9PartSwitch, ProceduralParts,
StagedAnimation, AnimatedDecouplers, DepthMask, KSPTextureLoader, Shabby, Shaddy, RealAntennas (comms — signal
present; not used by our control), RCSBuildAid (EDITOR torque/CoM analysis, no flight API), HotStaging (hot-stage
ring parts — the Falcon cold-stages, unused), FlipNBurn / StarshipGroundExtensions / IQStarshipLegs (Starship
hardware, not the Falcon/Dragon), OSSNTR, BahaSP, Benjee10_sharedAssets, HabTechProps, EngineGroupController*
(*module present, we bypass — see A2), KDSS, frost_mod, htRobotics (see A5).`

## D. [COSMETIC] — visual / audio (verified no control effect)
`TUFX, Scatterer (+AtmosphereCache, StockScattererConfigs, Blackrack_TUFX), EnvironmentalVisualEnhancements,
StockVolumetricClouds, ParallaxContinued + Parallax_Stock*, 000_TexturesUnlimited, ReStock/ReStockPlus,
TextureReplacer, RealPlume, Waterfall/WaterfallExtensions, SmokeScreen, IsaPlumes, Firefly/FireflyAPI,
ConformalDecals, NeptuneCamera, HullCameraVDS (feeds our HullCams display only), Resurfaced, Sol-Textures,
Sol-Visuals, zzz_Deferred, B9_Aerospace_ProceduralWings, ConformalDecals, KDSS.`

---

## E. Actions for the plan
1. **KCF is a control-layer GUARANTEE** — record that our `AttitudePilot` GetPotentialTorque authority depends on
   it (done, A1). No build.
2. **CustomPreLaunchChecks (A4)** — flight-verify at the first I-B ascent that it does not block our auto-launch;
   if it does, satisfy/exempt. Add to the I-B ascent checklist.
3. **EGC / KCPM (A2/A3)** — no action; documented so a part scan isn't surprised.
4. Everything else is COVERED / INFRA / COSMETIC — no further research warranted. This inventory is the record so a
   future session need not re-triage all 123.
