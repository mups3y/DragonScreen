> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE (§B12.1 dependency policy)**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-31; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# DEPENDENCY MATRIX (ACTIVE)

> Classification of the installed environment. Governed by `MASTER_BUILD_SPEC.md`. Goal (rule S10): keep exactly what is necessary and useful while DragonScreen owns its mission-critical logic — **not** minimum mod count. Never remove a dependency until its graph is verified and any required capability is internalised + tested. Compile-time deps stay `EXTRA_REFS = []` (KSP/Unity only); optional integrations by reflection with pure fallback.

Snapshot source: installed `GameData/` at KSP root, 2026-08-31. Classes: **CORE** (DragonScreen cannot function without it) · **ENVIRONMENT** (provides the RSS/RO simulation world) · **TARGET** (mission objects: ISS, drone ship, pads) · **OPTIONAL** (reflection integration; must degrade gracefully) · **UNNECESSARY-for-DS** (present, but DragonScreen does not depend on it) · **VISUAL/INFRA** (support; not depended upon).

| Mod(s) | Class | DragonScreen use | Remove? |
|---|---|---|---|
| TundraExploration, TundraSpaceCenter, Crew2_Patches, Benjee10_sharedAssets, zzz_TundraRO_Fixes | CORE (asset) | Dragon/Falcon parts + the **IVA screen props** DragonScreen paints the RenderTexture onto (`DragonScreen.cfg` patches `TE_CD2_IVA_SCREEN`). | No. Foundation of the spacecraft. |
| ModuleManager, 000_Harmony, KSPCommunityFixes, ClickThroughBlocker, ToolbarControl, 000_AT_Utils, CommunityResourcePack | CORE (infra) | Patching / runtime foundation the whole install needs. | No. |
| RealismOverhaul, RealFuels, RealHeat, RealAntennas, RealChute, RealPlume, SolverEngines, AJE, ModularFlightIntegrator, FerramAerospaceResearch (FAR) | ENVIRONMENT | The RO physics DragonScreen must be tuned against (rule: never tune vs stock KSP). Aero/heating/fuel/ignition truth. | No (environment). |
| Kopernicus, RSS-CanaveralHD, KSCSwitcher, RSSDateTime, Sol-Configs/Sol-Textures/Sol-Visuals, RP-1-ExpressInstall, RONoCareer, AdvancedPQSTools, BurstPQS | ENVIRONMENT | Real-scale planetary environment (this install's RSS/"Sol" incarnation). | No (environment). |
| RO parts: ROEngines, ROTanks, ROSolar, ROCapsules, ROHeatshields, ROLib, ROUtils | ENVIRONMENT | RO-configured vehicle parts. | No. |
| HabTech2, HabTechProps | TARGET | The ISS rendezvous/docking target + IVA props. | No (docking campaign needs it). |
| KerbalReusabilityExpansion, Space_X_barge_lander-2.0 | TARGET | Grid fins / legs + the ASDS drone ship for booster recovery. | No (RTLS/ASDS campaigns). |
| ModularLaunchPads, CanaveralPads, KerbalKonstructs, TundraSpaceCenter | TARGET/ENVIRONMENT | Launch + RTLS landing infrastructure. | No. |
| ThunderAerospace (TAC-LS), REPOSoftTech | ENVIRONMENT | Life-support resources → ECLSS screen data (simulate, don't fake). | No while ECLSS pages are in scope. |
| TestFlight | ENVIRONMENT | Engine ignition/reliability failures → real FDIR/failure-injection campaigns. | No (failure testing). |
| KerbalEngineer | OPTIONAL | Read live per-stage Δv/TWR/burn-time by **reflection** with pure fallback (`KerData`). Never a hard dep. | Keep optional; DS works without it. |
| HullCameraVDS, NeptuneCamera | OPTIONAL | Camera surfaces for camera pages. Degrade gracefully. | Keep optional. |
| **MechJeb2** | **UNNECESSARY-for-DS** | **Not** a DragonScreen dependency: no compile-time ref (`EXTRA_REFS=[]`); concepts already reimplemented in `pure/`. NOTE: repo LICENSE is GPL-3.0 due to earlier ported MechJeb2 code. | User-optional. DS must remain fully functional without it; do not make it the runtime brain. |
| Scatterer, EnvironmentalVisualEnhancements, StockVolumetricClouds, ParallaxContinued + Parallax_*, TUFX + Blackrack_TUFX.cfg, Waterfall + WaterfallExtensions, zzz_Deferred, TextureReplacer, 000_TexturesUnlimited, Shabby, Shaddy, Firefly + FireflyAPI, IsaPlumes, SmokeScreen, DepthMask, Resurfaced, StockScattererConfigs, ScattererAtmosphereCache | VISUAL/INFRA | Not depended upon; the cockpit display must remain reliable when these change (rule S10). | User choice. |
| B9PartSwitch, B9_Aerospace_ProceduralWings, ProceduralParts, ProceduralFairings, ConformalDecals, KerbalJointReinforcement, KSPWheel, EngineGroupController, HotStaging, AnimatedDecouplers, StagedAnimation, RCSBuildAid, 999_Scale_Redist, htRobotics, KDSS, StarshipGroundExtensions, IQStarshipLegs, KSPCommunityPartModules, CustomPreLaunchChecks, PatchManager, KerbalChangelog, ContractConfigurator, 000_KSPBurst, CommunityCategoryKit, KSPTextureLoader, LocalFixes, OSSNTR, KerbalRenamer, BahaSP, PEKKAsMods, FLBWF, FlipNBurn, FShangarExtender, frost_mod, ReStock, ReStockPlus, RSS-CanaveralHD | INFRA/parts | Build/part/utility support; not DragonScreen deps. | User choice; verify graph before any removal. |

**Policy reminders:** (1) DragonScreen owns its mission logic; MechJeb stays learn→reimplement→remove-runtime-dep. (2) Optional integrations read by reflection with a pure fallback and must never throw when absent (show `UNAVAILABLE`). (3) This is a seed; verify per-mod usage before any removal.
