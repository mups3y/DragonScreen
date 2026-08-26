# Session handoff — 2026-08-26

Read this first next session. Everything below is BUILT + INSTALLED but **NOTHING is RO-flight-validated**
(user, verbatim: *"I would not count any part of the RO flights so far as proven anything"*). The whole
session's output is waiting on **one clean RO flight** to test it. Do not call any RO path "proven",
"working", or "tuned" — the Kerbin-measured constants are guesses for Earth.

## The one thing to do next: fly a validation flight
The infrastructure is sound; the guidance chain is what will stop an end-to-end flight, and the weakest
link is the **rendezvous closing** (the memory records a flight that fired ZERO burns — NamedRendezvous
NC/NSR/Ti triggers). The user's open question at session end:
- **Option A** — dig into the NamedRendezvous burn triggers so the rendezvous actually closes (the #1
  mission-killer), OR
- **Option B** — fly the return-focused build first (validate the entry control + chutes + the safety net),
  since a failed rendezvous now still returns useful data via ReturnFallback.
The user leaned toward getting one clean flight; ask which, or just have them fly and read the recording.

## What is installed and pending validation

### Config patches (in `KSP/GameData/Crew2_Patches/`, NOT in the source tree, `.cfg` = restart only)
- `F9_Engines_InstantSpool.cfg` — `throttleResponseRate = 1E6` (instant, RealFuels' own instant value) +
  `ignitions = -1` (unlimited) on every Tundra engine CONFIG. Kills the octaweb mode-switch free-fall
  (the spool, NOT ullage — proven from flight_0825_173123: engine lit 1608 m, crawled 0→25 kN over ~5 s,
  caught at 280 m too low). ⛔ ModuleManager = ONE statement per line.
- `F9_S1_GimbalRange.cfg` — octaweb ModuleGimbal 2°→5° (real Merlin; the corpus showed 10-20% gimbal
  railing). Attitude.cs uses it free (derives rates from torque).
- ⛔ The additive-engine patch (`F9_S1_IndividualEngines.cfg`) was **DELETED** — it broke launch
  (redefined AllEngines to 6 engines → TWR 0.99). Stock octaweb kept.

### DLL changes this session (all in `plugin/src/`, byte-installed)
- **Octaweb direct control** — `BoosterRecovery.SetOctawebMode` drives the 3 ModuleEnginesRF directly by
  engineID (Activate/Shutdown), NOT the ModuleTundraEngineSwitch cycle (the cycle re-inits = the free-fall).
- **`VehicleControl.cs`** (NEW) — unified direct-handle primitives: SetRcsThrust, SetGimbalLimit,
  SetGimbalLock, SetFinAuthority, SetRadiators, SetDescentMode, SetComOffset, Decouple, InvokeEvent,
  **FireByGuiName** (event-or-action by GUI label). CapsuleRcs routes through it. Radiators activated in
  orbit (AutoPilot). Booster asserts gimbal+fin authority each phase (BoosterRecovery, [Tunable]=100).
- **Passive-CoM lifting entry** — `EntryOps.PassiveComEntry = true` (ON; no proven RO entry to protect):
  Fly() enables the RO AdjustableCoMShifter "Descent Mode" + sets offsetPercent = LiftFraction. The CoM
  offset (0,0,0.196) trims the AoA aerodynamically.
- **⛔ BANK-TO-STEER entry (2026-08-26 flight feedback)** — the SteerTo up-hint in Fly() is now
  `lift.normalized` not `upC`: PITCH holds AoA, ROLL banks, NO yaw. User's manual entry proved only pitch
  (S) + roll (Q/E) were controllable; yaw induced instability (blunt capsule is aero-stable in yaw).
- **Reaction-wheel dependency removed** — `AttitudeController.EnsureRcsAuthority(v)` in Drive() turns RCS
  ON whenever steering a vessel with no wheel authority AND not powered (RO strips wheels; RCS is the only
  attitude authority). The Draco deorbit (ModuleRCS) still gets RCS; a lit main engine (gimbal) does not.
- **Nose cone timing** — DeorbitOps.Engage opens the shroud (Draco deorbit needs the forward Dracos);
  ChuteGuard closes it independently once `altitude < atmosphereDepth` on descent; EntryOps.Engage still
  closes at engage. Open for the orbital burn, shut for the atmosphere.
- **⛔ Parachutes = RealChuteModule (NOT stock ModuleParachute)** — ChuteGuard + FlightCommands were
  SILENT NO-OPS (cast to ModuleParachute, found nothing). EntryOps.Cut was broken ("cut chute" ≠ the real
  handle "Cut main chute"). All fixed to fire the RealChute handles ("Deploy Chute"/"Cut main chute") via
  VehicleControl.FireByGuiName. Deploy worked only by luck.
- **ReturnFallback.cs** (NEW) — if the rendezvous will not close (return propellant < 40% OR > 6 h without
  docking), abandon it and de-orbit to land ANYWHERE for the re-entry data — a Layer-3 downmode. Sets
  EntryOps.SteeringTest.
- **Re-entry steering-limits test** — `EntryOps.SteeringTest` + SteeringSweep(): sweeps pitch/yaw extremes
  (each 20 s) as it descends through the atmosphere layers, backs off at the heat limit
  (SweepBackoffTempFrac 0.92 / ablator 0.15). `EntryHeat.cs` (NEW) reads the PICA-X shield.
- **FDIR framework (Layer-3 P0)** — `pure/HealthMonitor.cs` + `pure/FaultResponse.cs` +
  `pure/ThrustDeliveryMonitor.cs` (52 tests green), wired OBSERVE-ONLY into NodeExecutor + the `fd_`
  recorder block. Drives nothing yet.

### Recorder columns added this session (validate them on the flight)
`fd_thrVerdict,fd_thrRaw,fd_thrResid,fd_thrKind,fd_thrRecovery` · `ctrl_gimbalPct` ·
`he_steerTest,he_steerSeg,he_atmDensity,he_ablatorFrac,he_charFrac,he_shieldK,he_shieldFluxKw`.
Per-layer entry steering readout = pair `he_*`+density with `a_altAsl/a_qKpa/a_mach` (layer), `a_aoaDeg`
(achieved AoA), `r_aoaCmd/r_vertCmd/r_latCmd` (command), `r_alongKm/r_crossM` (response).

## What to read from the validation flight
- **Engine step-down** — `bl_liveThrustKn` stays positive across 9→3→1 (the instant-spool + direct control).
- **Gimbal** — `b_actP/Y` railing reduced (the 5° range); `ctrl_gimbalPct`.
- **Rendezvous** — did the burns fire? (`rv_leg`, `rv_lastDv`, `nd_deliveredDv`). This is the weak link.
- **Entry** — bank-to-steer stable (no yaw thrash)? `he_shieldK`/`he_ablatorFrac` per layer; chutes deploy
  (`r_drogues`/`r_mains`); nose cone shut.
- **RCS** — `x_rcsOn` on through every coast/rendezvous maneuver.

## Infrastructure confirmed sound (do not re-audit)
- Physics range: built-in `BoosterRecovery.RangeMetres = 1500 km` (unpack 1485 km), covers the measured
  512 km droneship separation with 3× margin. NOT a separate PRE mod — built into the DLL.
- Impact prediction: built-in `ImpactPredictor` + MapTrajectory (feeds booster aim + entry).
- Conductor: `CrewProcedureOps` — gate-driven, engages the tested controllers, resumes ascent after the
  recovery focus-handback.

## Build / test / install / diagnose
- `cd plugin && python build.py test|install` (Roslyn, byte-deterministic; install needs KSP+CKAN closed +
  full restart). `.cfg` changes need a restart, no rebuild.
- Diagnose: `python build/assess_flight.py "<newest capture>"`; captures in `KSP/DragonScreen_capture/`.
- Craft dump: `craftdump.csv` auto-writes the first frame a craft is on the pad (CraftDump).

## Discipline (do not drop)
Pure-first + headless-tested before glue · instrument the same pass · confirm against ground truth
(ConfigCache/CSV/KSP.log/dump), not the .md · read files IN FULL · one root-cause pass then one fix ·
NO Python sims (corpus + headless tests) · detect by capability not part name · match GUI-name handles to
the DUMP's EXACT string · do NOT commit unless asked. Full session log: memory
[[dragonscreen-gnc-audit-2026-08-25]].
