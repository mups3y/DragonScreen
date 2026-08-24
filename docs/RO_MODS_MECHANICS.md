# RO / RSS / mods vs stock KSP — what the guidance MUST obey

Compiled 2026-08-23 from the ACTUAL install (`GameData/` enumerated, key claims verified against the
mod's own config). This exists because the guidance keeps inheriting stock-KSP assumptions that these
mods have deleted — the reaction-wheel one (below) cost a manual RCS intervention on the 2026-08-23
rendezvous. Every stock crutch is suspect; verify against the config, not memory.

Legend: ✅ handled · ⚠️ partial / flagged · ❌ not handled (open)

| mod (verified in GameData) | what it CHANGES from stock | guidance rule | status |
|---|---|---|---|
| **RealismOverhaul** `RO_ReactionWheels.cfg` | **Reaction wheels STRIPPED** — 392 `!MODULE[ModuleReactionWheel]` removed, survivors cut to ~0.1 N·m CMGs. | Attitude authority = **RCS + engine gimbal ONLY**. Whenever we steer with no lit engine, RCS MUST be on or nothing turns. Detect by capability (`HaveWheelAuthority`), not RSS flag. | ✅ NodeExecutor align/hold/burn (2026-08-23). ✅ docking/prox-ops audited (2026-08-23): DirectApproach/Waypoint/StationApproach/DockingOps.FlyTo all enable RCS; fixed the ONE gap — DockingOps capture-hold now guarantees its own RCS. Stale StationApproach "orients on wheels" comments swept 2026-08-23. |
| **FerramAerospaceResearch** (+`Custom_FARAeroData.cfg`) | FAR **replaces stock drag cubes** entirely. Real AoA, Mach, control-surface (grid-fin) forces. | Never model drag from stock DragCubes. **Measure** it from the vessel's own deceleration (FAR-agnostic). | ⚠️ booster: measured bc, now FAR-clean gated (2026-08-23). ❌ capsule entry `AeroTable`/`BuildAeroTable` still builds from STOCK drag cubes — wrong under FAR; fix for the de-orbit/return leg |
| **RealFuels** (+`SolverEngines`) | **Ullage** (settled prop to light), **limited ignitions** (TEATEB: S1 4, MVac 4), boiloff, real Isp/thrust, pressure-fed vs pump. | Ullage-settle before EVERY light; never spam staging (murders the ignition budget); track ignitions; light MVac promptly at sep while still settled. | ✅ UllageProbe + ignition gating on ascent. ⚠️ booster relights (entry+landing) each need an ullage settle + can fail — verify |
| **TestFlight** | Engines have **reliability < 1** — an ignition can just FAIL (`ignitionReliabilityStart` S1 0.986 / MVac 0.967), and in-flight failures. | Detect a failed light (no thrust ~1-2 s after ignite) and RETRY within the ignition budget, rather than staging the stack apart. | ❌ not handled — no failed-ignition detect/retry yet. OPEN |
| **RealAntennas** | Comms range/gain is REAL — control can drop with distance/aspect (RA replaces CommNet ranges). | The booster flies to ~500 km downrange while Dragon coasts; verify BOTH keep a control link, or the recovery goes unresponsive. | ❌ unverified — check link at booster's downrange in a flight |
| **RealHeat** | Real reentry heating on booster + capsule. | The entry burn exists to cut heating (and downrange); the capsule needs its heat-shield-forward attitude on entry. | ⚠️ booster entry burn present; capsule EDL attitude TBD on return |
| **RealChute** | Real parachute stages (drogues then mains) at real altitudes/speeds. | Dragon splashdown: drogues ~5486 m, mains ~1830 m (already the real figures). | ✅ figures correct; UNTESTED in RSS |
| **Kopernicus + Sol/RSS** (`RSS-CanaveralHD`, `Sol-Configs`, `RP-1`) | Earth-scale: R 6371 km, atmosphere ~140 km, LEO ~7.8 km/s, LC-39A 28.6 N, 51.6° to ISS. | All scale-dependent guidance gates on `RssBody(v)` (body radius > 1e6). | ✅ RssBody split throughout |
| **KerbalKonstructs** | Launch sites + the droneship as a STATIC (not a vessel). | Booster aims at the fixed `DroneshipEarthLat/Lon` (KK can't be found as a vessel). | ✅ handled |
| **TundraExploration** + `zzz_TundraRO_Fixes` + `ROCapsules/ROEngines/ROTanks` | The Falcon/Dragon parts, RO-rescaled to real mass/fuel/thrust by part name. | `VehicleParts` binds by part name/capability; must survive the RO rescale. | ✅ detection works |
| **KerbalJointReinforcement** | Stiffer joints; staging/sep shocks differ from stock. | Sep/flip sequencing already uses distance not fixed time; watch for sep transients. | ✅ mostly |
| **KerbalReusabilityExpansion** | Grid fins + landing legs (KRE parts, `ModuleControlSurface`). | Grid-fin actuation gated on deploy; fins are control surfaces under FAR. | ✅ grid fins handled |
| **AtmosphereAutopilot** | An aero-assist autopilot that can take control inputs. | ⚠️ must NOT fight our AttitudeController — verify it is inert/off for our vessels. | ❌ unverified — check it is not active |

## The recurring trap (why this file exists)
Every one of these deletes or replaces a stock affordance the guidance was written against:
- stock **reaction wheels** turn a coasting ship for free → **gone** (RO): steer needs RCS.
- stock **drag cubes** give a cheap drag number → **replaced** (FAR): drag must be measured.
- stock engines **always light** → **not guaranteed** (TestFlight): must detect + retry.
- stock **infinite ignitions** → **finite** (RealFuels): must budget and settle.

Before writing ANY guidance that assumes the vehicle will turn, light, hold, or drag a certain way,
check which of these owns that behaviour in THIS install and confirm against the mod's config.

## Verified this session
- Reaction wheels: `RealismOverhaul/RO_ReactionWheels.cfg` (392 removed, survivors ~0.1 N·m). FIX SHIPPED.
- FAR: `GameData/FerramAerospaceResearch/` present. Drag-sampling made FAR-clean.
- TestFlight, RealAntennas, RealHeat, RealChute, AtmosphereAutopilot: folders present — effects above are
  RO-standard; the ❌ items need an in-game/flight check before I encode anything.
