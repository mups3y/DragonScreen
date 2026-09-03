> ⚠️ **SUPERSEDED FIGURES (recovered W0, 2026-09-03).** This file's counts — **27 parts, 304 modules, 652
> events, 273 actions, 6770 fields** — describe the **Aug-26 dump**, taken before the owner rebuilt the craft
> and installed new mods. They do NOT describe the current vehicle. `docs/reference/craftdump.csv` was
> refreshed by W0; this file's per-part table and appendix are reference only until someone re-walks them
> against the fresh dump.

# Craft-dump vehicle map — the ground truth for direct control

The autopilot rebuild controls the vehicle **by capability, from the real craft** — not by part name, not
from spec sheets. This is the actuation surface of the reference Crew-2 stack, dumped live from the VAB/
flight by `CraftDump.cs` → `data/craftdump.csv` (**27 parts, 304 modules, 652 events, 273 actions, 6770
fields**). Re-dump per mission; the autopilot reads *this*, so any vehicle it is given is controlled from
its own real modules. [P — live craft dump]

Discipline: **detect + actuate by capability (module/event/action), never by part name** —
[[falcon-detect-by-capability]], [[falcon-port-dont-invent]].

---

## 1. The 27 parts and their control capabilities

| # | Part | Control modules (capability) |
|---|---|---|
| 0 | Crew Dragon pod | **ModuleEnginesRF** (Draco) + **ModuleEngineConfigs** + **ModuleRCSFX** (Draco = engine *and* RCS) + **ModuleAnimateGeneric** (nose cone) |
| 1 | Drogue parachutes | **RealChuteModule** |
| 2 | Main parachutes | **RealChuteModule** |
| 3 | PICA-X heatshield | ModuleAnimateGeneric |
| 4 | Trunk | **ModuleTundraDecoupler** (trunk jettison) + crossfeed |
| 5 | Trunk adapter/decoupler | **ModuleDecouple** (drops S2) + crossfeed |
| 6 | S2 tank | (fuel) |
| 7 | MVac (S2 engine) | **ModuleEnginesRF** + **ModuleEngineConfigs** + **ModuleGimbal** |
| 8–11 | S2 RCS ×4 | **ModuleRCSFX** |
| 12 | S1 interstage | **ModuleGimbal** + **ModuleTundraDecoupler** (stage sep) + crossfeed |
| 13 | S1 tank | (fuel) |
| 14 | Octaweb (9 Merlins) | **ModuleEnginesRF** + **ModuleEngineConfigs** (AllEngines/ThreeLanding/CenterOnly) + **ModuleGimbal** |
| 15 | Erector | (ground, decouples at launch) |
| 16–19 | Falcon landing legs ×4 | **ModuleWheelDeployment** |
| 20–21 | Cold-gas thrusters ×2 | **ModuleRCSFX** (the retrograde flip authority) |
| 22–25 | Grid fins ×4 (T-222 Titanium) | **SyncModuleControlSurface** + **ModuleAnimateGeneric** (deploy) |
| 26 | NASA Docking System | **ModuleDockingNode** + ModuleAnimateGeneric |

Also present per part: FAR aero (`FARAeroPartModule`/`FARPartModule`/`GeometryPartModule` — measure drag),
`ModuleFuelTanks` (RealFuels), **TestFlight** (`TestFlightCore`/`…Failure_IgnitionFail`/
`…Reliability_EngineCycle` — engine reliability < 1, ignition/ullage limits), `MechJebCore`,
`MuMechModuleHullCameraZoom` (cameras), antennas, solar, `ModuleColorChanger`/`B9PartSwitch` (livery/decals).

Pod resources (from the dump): **NTO 508.9 / MMH 655.3** (Draco propellant — [[dragon-return-propellant-mmh-nto]]),
Helium (pressurant), and TAC life support **Oxygen / Food / Water** ([[dragonscreen-tac-life-support]]).

---

## 2. The actuation verbs (events/actions) — how the control layer commands each capability

| Capability | Module | Actuation (actions/events + fields) |
|---|---|---|
| **Engines** (Merlin ×9, MVac, Draco) | ModuleEnginesRF | `ActivateAction` / `ShutdownAction` / `OnAction` / `ToggleThrottle`; field `thrustPercentage`; read `finalThrust` |
| **Engine mode** | ModuleEngineConfigs | select CONFIG (AllEngines / ThreeLanding / CenterOnly) — the octaweb 9→3→1 |
| **Gimbal** | ModuleGimbal | `LockAction`/`FreeAction`/`TogglePitch/Yaw/RollAction`; field `gimbalLimiter` |
| **RCS** (S2, Draco, cold-gas) | ModuleRCSFX | `ToggleAction`; field `thrustPercentage` (dial authority per task) |
| **Grid fins** (steer!) | SyncModuleControlSurface | `ActivateAllControls`/`DeactivateAllControls`, `Extend`/`Retract`, `PitchActive`/`YawActive`/`RollActive` (+Inactive/Toggle); field `authorityLimiter` |
| **Grid-fin deploy** | ModuleAnimateGeneric | `ToggleAction` (stow/deploy the fin) |
| **Decouple** (stage sep, trunk, S2 drop) | ModuleTundraDecoupler / ModuleDecouple | `DecoupleAction` |
| **Parachutes** | RealChuteModule | `ActionArm`/`ActionDeploy`/`ActionCut`/`ActionDisarm` |
| **Landing legs** | ModuleWheelDeployment | `ActionToggle` (deploy in the final seconds) |
| **Nose cone / shroud / heatshield** | ModuleAnimateGeneric | `ToggleAction` (open the nose to expose docking + forward Dracos) |
| **Docking** | ModuleDockingNode | `UndockAction`/`DecoupleAction`/`ServoEngageLock`/`ToggleXFeed` |
| **Crossfeed** | ModuleToggleCrossfeed | `EnableAction`/`DisableAction` |

**How the rebuild uses this (L1/L2 — nav/control):** the vehicle layer enumerates the live parts, matches
these **modules** to capabilities, and the control layer actuates through the **fields/actions** above —
never a part-name lookup, never a staging/action-group assumption. This file + `data/craftdump.csv` is the
direct-control ground truth; `ModuleManager.ConfigCache` supplies the numeric CONFIG values.

---

## 3. Capabilities beyond "just controls" — the ones easily MISSED (user 2026-08-26)

Filtering to obvious control modules hid capabilities the autopilot genuinely needs. From the **complete**
enumeration (65 modules, 150 events, 96 actions):

- **`ModuleTundraEngineSwitch`** — the REAL octaweb mode + individual-engine control, separate from
  `ModuleEngineConfigs`: `NextEngineModeAction` / `PreviousEngineModeAction` / `ToggleEngineModeAction`
  (AllEngines→ThreeLanding→CenterOnly, the **9→3→1**), `ActivateEngineAction` / `ShutdownEngineAction`,
  and **`ToggleIndependentThrottleAction`** (independent-engine throttle — the individual-engine landing
  control). *This is how the 9→3→1 landing steps are actually commanded.*
- **`AdjustableCoMShifter`** (`ToggleMode` / `Toggle`) — shifts the **centre of mass**: the offset CoM that
  gives Dragon its **trim AoA + L/D for the bank-angle lifting entry** (Phase 6). The entry guidance sets
  this, not just the bank angle.
- **Power / thermal / comms** (drive FDIR resource-margin + the ECLSS page, and real mission behaviour):
  `ModuleDeployableSolarPanel` (`ExtendAction`/`RetractAction` — trunk solar), `ModuleGenerator`
  (`ActivateAction`/`ShutdownAction`), `ModuleActiveRadiator` (`ActivateAction`/`ShutdownAction`/
  `ToggleRadiatorAction` — the coolant loops), **`ModuleRealAntenna`** (`StartTransmissionAction`/
  `PermanentShutdownAction` — comms; the entry **blackout** and the "return crew without comms" autonomy).
- **Livery / decals / flags** (the user's per-mission vehicle setup): **`ModuleColorChanger`**
  (`ToggleAction` — livery) and **`ModuleTundraSoot`** (`Flag1Selector`/`Flag2Selector`/`Flag3Selector` +
  `ToggleSootyAction` — mission flags/decals + reflown-booster soot).
- **`ModuleCommand`** — `ChangeControlPoint` / `MakeReference` / `MakeReferenceToggle` / `HibernateToggle`:
  set the **control point** (which end is "forward") — needed so docking/entry attitude is referenced to
  the right axis; `RenameVessel` (the VAB-name → mission selection reads this).
- **`ModuleDockingNode`** (full): `SetAsTarget`/`UnsetTarget`, `MakePrimary`, `MakeReferenceTransform`,
  `UndockSameVessel`, plus the Undock/Decouple/ServoLock/XFeed already listed.
- **`ModuleScienceContainer` / `ModuleScienceExperiment`** — `DeployExperiment`/`CollectAll`/`ReviewData`
  (science tasks; e.g. Fram2/research free-flyers).
- **`MuMechModuleHullCameraZoom`** — `ActivateCameraAction`/`Next`/`Previous`/`ZoomIn`/`ZoomOut` (the hull
  + docking cameras — a SCREEN feature, kept).
- **`ModuleToggleCrossfeed`** — `EnableAction`/`DisableAction` (propellant crossfeed between stages).
- **`MechJebCore` is on the craft** — exposes `OnAscentAPToggle`, `OnLandTarget`/`OnLandsomewhere`,
  `OnOrbitPrograde/Retrograde/Normal/Antinormal/RadialIn/RadialOut`, `OnKillRotation`, `OnPanic`,
  `OnTranslatron*` (vertical-speed hold) as actions. We drive our own guidance, but MechJeb's C# core is
  available ([[mechjeb-source-reference]]).
- **`ToggleStaging`** — present on nearly every module (the stock staging toggle); noted so it is not
  mistaken for a real capability.

**Nuance the rebuild must respect:** engine mode/individual throttle is `ModuleTundraEngineSwitch`, the CoM
for entry is `AdjustableCoMShifter`, comms/power/thermal have real actuation, and decals are
`ModuleColorChanger`/`ModuleTundraSoot` — none of which were in the first control-only pass.

### 3a. ⛔ Octaweb engine-mode actuation rule (the landing-critical one)

The S1 octaweb (part #14) is **ONE part with three engine MODES** under `ModuleTundraEngineSwitch`:
`selectedIndex` 0 = **AllEngines** (9), 1 = **ThreeLanding** (3), 2 = **CenterOnly** (1). Each mode is its
own `ModuleEnginesRF` (distinct `engineID`) and — on the LIVE vessel — **each mode has `ignitions = 1`**
(the CONFIG's `-1` is overridden per mode). Spool is instant (`throttleResponseRate = 1000000`). Therefore:

- **Exactly 3 ignitions, one per mode.** Budget them: **AllEngines = liftoff**, **ThreeLanding = entry
  burn (3 engines)**, **CenterOnly = landing burn (1 centre engine)**.
- **The landing burn is the single centre engine, lit ONCE, running continuously to the deck.** Do **NOT**
  step 3→1 *during* the landing burn — switching ThreeLanding→CenterOnly mid-burn RE-IGNITES CenterOnly and
  spools it, wrecking the hoverslam timing.
- **Select the mode ABSOLUTELY and only while the engine is OFF** (between burns): set `selectedIndex` to
  the target (or activate the target mode's `ModuleEnginesRF` by `engineID`). **NEVER use
  `NextEngineModeAction`** — it *cycles* relative to the current mode and can wrap to the wrong one.
- Igniting/shutting an engine mode: the mode's `ModuleEnginesRF` `Activate`/`Shutdown`; `thrustPercentage`
  for throttle. The mode switch (off) picks which engines; ignition lights them.

`pure/BoosterDescent.cs` encodes this: `EngineMode` = 3 for the entry burn, **1 for the landing burn**, and
the header spells out the "select absolutely while off, one ignition per mode, no mid-burn 3→1" rule for
the glue.

---

## 4. Appendix — COMPLETE events & actions per module (all 65)

Every distinct event/action in `data/craftdump.csv`. `ToggleStaging` (on almost all) omitted per row below
where it is the only event, to keep the list to real capabilities.

| Module | EVENTS | ACTIONS |
|---|---|---|
| AdjustableCoMShifter | ToggleMode, ToggleStaging | Toggle |
| EngineGroupModule | AssignGroupId, ClearGroupId | — |
| MechJebCore | — | OnAscentAPToggle, OnDeactivateSmartASS, OnKillRotation, OnLandTarget, OnLandsomewhere, OnOrbit{Prograde,Retrograde,Normal,Antinormal,RadialIn,RadialOut}, OnPanic, OnTranslatron{KeepVert,MinusOneSpeed,Off,PlusOneSpeed,ToggleHS,ZeroSpeed} |
| ModuleActiveRadiator | Activate, Shutdown | ActivateAction, ShutdownAction, ToggleRadiatorAction |
| ModuleAnimateGeneric | Toggle | ToggleAction |
| ModuleColorChanger | ToggleEvent | ToggleAction |
| ModuleCommand | ChangeControlPoint, MakeReference, RenameVessel, ToggleControlPointVisual | HibernateToggle, MakeReferenceToggle |
| ModuleDecouple | Decouple | DecoupleAction |
| ModuleDeployableSolarPanel | EventRepairExternal, Extend, Retract | ExtendAction, ExtendPanelsAction, RetractAction |
| ModuleDockingNode | Decouple, DisableXFeed, EnableXFeed, MakePrimary, MakeReferenceTransform, SetAsTarget, Undock, UndockSameVessel, UnsetTarget | DecoupleAction, DisableXFeedAction, EnableXFeedAction, MakeReferenceToggle, ServoEngageLockAction, ServoDisgageLockAction, ToggleLockedAction, ToggleXFeedAction, UndockAction |
| ModuleEnginesRF | Activate, Shutdown, ToggleIncludeinDV | ActivateAction, OnAction, ShutdownAction, ToggleThrottle |
| ModuleFuelTanks | ChooseTankDefinition, Empty, … (volume/resource callbacks) | — |
| ModuleGenerator | Activate, Shutdown | ActivateAction, ShutdownAction, ToggleAction |
| ModuleGimbal | ToggleToggles | FreeAction, LockAction, ToggleAction, TogglePitchAction, ToggleRollAction, ToggleYawAction |
| ModuleRCSFX | ToggleToggles | ToggleAction |
| ModuleRealAntenna | StartTransmission, StopTransmission, PermanentShutdownEvent, DebugAntenna, TransmitIncompleteToggle, Antenna{Planning,Target}GUI | StartTransmissionAction, PermanentShutdownAction |
| ModuleScienceContainer | CollectAll, CollectDataExternal, ReviewData, StoreDataExternal, TransferData | CollectAllAction |
| ModuleScienceExperiment | DeployExperiment(+External), ResetExperiment(+External), CleanUpExperimentExternal, ReviewData, TransferData | DeployAction, ResetAction |
| ModuleToggleCrossfeed | ToggleEvent | DisableAction, EnableAction, ToggleAction |
| ModuleTundraDecoupler | Decouple | DecoupleAction |
| ModuleTundraEngineSwitch | NextEngineModeEvent, PreviousEngineModeEvent | ActivateEngineAction, NextEngineModeAction, PreviousEngineModeAction, ShutdownEngineAction, ToggleEngineAction, ToggleEngineModeAction, ToggleIndependentThrottleAction |
| ModuleTundraSoot | CleanSootEvent, Flag1/2/3Selector, Flag1/2/3Toggle | ToggleSootyAction |
| ModuleWheelBase | EvtAutoFrictionToggle | ActAutoFrictionToggle |
| ModuleWheelDeployment | EventToggle | ActionToggle |
| MuMechModuleHullCameraZoom | ActivateCamera, EnableCamera | ActivateCameraAction, DeactivateCameraAction, NextCameraAction, PreviousCameraAction, ZoomInAction, ZoomOutAction |
| RealChuteModule | GUIArm, GUICut, GUIDeploy, GUIDisarm, GUIRepack, GUIToggleWindow | ActionArm, ActionCut, ActionDeploy, ActionDisarm |
| SyncModuleControlSurface | — | ActionExtend, ActionRetract, ActionToggle, ActivateAllControls, DeactivateAllControls, PitchActive/Inactive, YawActive/Inactive, RollActive/Inactive, TogglePitch/Yaw/Roll |
| *(events-only / passive)* | AdjustableCoMShifter*, DPSoundFX, DragonScreenState, FAR*, FXModule*, FlightDataRecorder_Engine, GeometryPartModule, LifeSupportModule, ModuleAblator, ModuleB9PartInfo/Switch, ModuleDeployableAero, ModuleDepthMask, ModuleDragModifier, ModuleEngineConfigs, ModuleInventoryPart, ModuleLiftingSurface, ModulePartVariants, ModuleProbeControlPoint, ModuleROSolarPanel, ModuleSAS, ModuleSurfaceFX, ModuleTripLogger, ModuleTundraRCSAnimation, ModuleWaterfallFX, ModuleWheelDamage/Lock/Suspension, TestFlight{Core,Interop,Reliability,Reliability_EngineCycle,Failure_*} | ToggleStaging only (+ module-specific fields; TestFlight fields = reliability/DU read for FDIR) |

