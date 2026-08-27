# Installed-Mods Research — how THIS environment actually behaves (and what it means for the autopilot)

Purpose: stop guessing at fixes. This is the ground truth of the 114-mod RSS/RO install the Crew-2 autopilot
must fly in, focused on everything that touches CONTROL, aerodynamics, propulsion, structure, and recovery.
Facts here are read from the install (ModuleManager.ConfigCache, mod settings, part configs, the flight
logs), not assumed. Tags: **[V]** verified from the install · **[K]** established mod behaviour.

---

## 0. THE ONE-PARAGRAPH PICTURE

This is a **Realism Overhaul + FAR** install. **Reaction wheels are gone** — the ONLY attitude authority on
ascent is the **first-stage engine gimbal, and it is only ±5°** [V]. The airframe is a long, thin Falcon 9
that **FAR makes aerodynamically UNSTABLE transonically** (the aero-centre shifts forward through Mach 1), so
any angle of attack in the max-Q region is *amplified* by the aerodynamics and must be actively nulled by the
gimbal, fast. Stock SAS's slow PID could not catch that divergence (flights 1-3 all lost attitude at max-Q
and either RUD'd or aborted). The gimbal has plenty of *torque* (≈10 MN·m vs ≈0.3 MN·m aero) — the problem is
the *control law*, not the authority. Everything else (RealFuels ullage/ignition limits, RealChute parachutes,
KJR joints, TestFlight reliability) is secondary but must be respected. **Staging and action groups are
unreliable here and are banned — actuate every part module directly.**

---

## 1. ATTITUDE CONTROL — the crux

### RealismOverhaul (v18.0.0) [V]
- **Removes reaction wheels** from crewed pods/probes (or cuts them to near-zero). ⇒ on ascent the vehicle
  has **NO body-torque source except the engine gimbal**; on orbit/entry the capsule has **only the RCS
  (Dracos)**. This is why "full control" cannot lean on reaction-wheel SAS. [K/V]
- Real masses, real CoM. The stack is long and back-heavy (engines aft) → statically marginal.

### The gimbal authority (measured) [V]
- **S1 `ModuleGimbal gimbalRange = 5`** (each octaweb mode; CenterOnly = 4). So pitch/yaw authority = **±5°
  of thrust vector**. `useGimbalResponseSpeed` not forcing slow — the gimbal can slew fast.
- Torque check: 9×Merlin ≈ 6,681 kN × sin5° ≈ 582 kN side force × ~18 m arm ≈ **~10 MN·m** available, vs a
  transonic aero moment on the order of **~0.3 MN·m**. **Authority is NOT the limit — the control loop is.**

### FerramAerospaceResearch / FAR (v0.16.1.2) [V]
- Replaces stock drag-cube aero with **real per-shape aerodynamics**: lift, drag, and **pitching/ yawing
  moments** computed from the actual body. A rocket with CoP ahead of CoM is **statically unstable** and will
  **diverge in AoA unless actively flown**. [K]
- **Transonic aero-centre shift:** through Mach ~0.8–1.2 the centre of pressure moves, and the vehicle that
  was stable subsonically becomes unstable — exactly where max-Q sits (~Mach 1, ~11 km). The flight CSVs show
  AoA <1° subsonically then a runaway 2°→40° right at max-Q: **this is the FAR transonic instability.** [V]
- **Zero-AoA is not optional here** — it is the only way to keep the destabilising aero moment ≈0 through the
  transonic/max-Q window. Load relief AND controllability both demand it.
- FAR provides `SyncModuleControlSurface` for the **grid fins** (booster) — real aero control surfaces.

### AtmosphereAutopilot (installed, **OFF by default**) [V]
- A FAR-aware fly-by-wire suite (Boris-Barboris): `Standard_Fly-By-Wire` with **`moderate_aoa`,
  `moderate_sideslip`, `moderate_g` = True**, and a **`Gravity_turn_Fly-By-Wire`** rocket-ascent mode. It
  builds an online model of the craft's aero and produces smooth, departure-safe control — precisely the
  problem we have. **master_switch_key = P; thread.log ends "Stopping" → it was NOT active in flights 1-3**
  (so it neither helped nor interfered). It is keyboard/user-driven, not obviously scriptable from our DLL.
- ⇒ **It proves the airframe IS controllable with a proper FAR-aware loop, and it is the reference for what
  our gimbal controller must do** (moderate AoA/G, react fast). If we cannot beat it, driving/duplicating its
  approach is the fallback.

### Implication for the autopilot
1. **Fly zero AoA through the transonic/max-Q band** (nose on the velocity vector) — already the plan.
2. **Replace the slow SAS inner loop with a FAST, direct gimbal loop** (pure `ControlLaw` + `Authority`
   commanding `FlightCtrlState.pitch/yaw`), aggressive enough to null the transonic divergence with the ±5°
   gimbal. SAS only if it demonstrably holds better (it did not).
3. On the capsule, attitude is **RCS-only** (Dracos) — attitude-first-then-translate stands.

---

## 2. PROPULSION — RealFuels / SolverEngines / ROEngines / AJE

- **RealFuels (v15.15):** engines are `ModuleEnginesRF`. **Ullage** = True — a settled propellant column is
  required before ignition, or the engine has a high chance of a bad/failed light. **Ignitions are limited**
  (config −1 but overridden **per octaweb mode to 1** on the live vessel — [[falcon-real-hoverslam-technique]]).
  `throttleResponseRate = 1000000` ⇒ **spool is instant** (no ramp).
  - �incidence: **S2 ignition after the MECO coast needs ullage** (RCS/ullage settling) or the MVac may not
    light — a real risk for our staging.
- **SolverEngines (v3.14) / AJE:** real thrust/Isp vs pressure & throttle; real **minimum throttle** (the
  Merlin cannot throttle below ~ its min, ~39–57%). The max-Q throttle bucket must stay above min throttle.
- **Direct control:** ignite = the specific mode's `ModuleEnginesRF.Activate()`; shut = `.Shutdown()`;
  throttle limit = `thrustPercentage`; octaweb mode = the `ModuleTundraEngineSwitch` mode's engines, selected
  absolutely while OFF (never NextEngineMode — [[falcon-real-hoverslam-technique]]).

## 3. STRUCTURE — KerbalJointReinforcement (v3.8.7) [V]
- Stiffens part joints (real rockets are stiffer than stock wobbly joints). Raises but does not remove the
  structural-failure threshold: the flight-2/3 breakups came from a **tumble at max-Q** (huge AoA → huge aero
  load) that exceeds even KJR-reinforced interstage joints. Keep AoA≈0 and the joints hold.

## 4. RECOVERY — RealChute (v1.4.9, RealChuteLite/FAR) [V]
- The Dragon's chutes are **RealChuteLite (FAR-integrated)**, NOT stock `ModuleParachute`. `ModuleParachute.
  Deploy()` does nothing on them — this silently failed EVERY descent (abort + splashdown; the parts "splashed
  at 139 m/s"). **Deploy by invoking the chute part's own deploy event/action** (already fixed in
  `FlightDriver.DeployChutes`, RealChute-aware). Drogue vs main = the `POD.DROGUES` / `POD.MAINS` parts.

## 5. RELIABILITY — TestFlight (v2.12) [V]
- Per-engine reliability with a small **base failure rate** (SuperDraco BFR ≈ 2.4e-5) → ignition failures,
  performance loss, shutdown, or (rare) explosion are POSSIBLE. **No failure events appear in the flight-1-3
  logs**, so the RUDs were control, not TestFlight. But FDIR must still watch for thrust-shortfall / no-light
  and abort — that path is real here.

## 6. ENVIRONMENT (context, not control)
- **RSS (Kopernicus + RSS-CanaveralHD + RealSolarSystem via RO):** Earth-sized, 51.6° ISS plane from ~28.5°N.
  Launch azimuth ~42°. RSSDateTime, KSCSwitcher, CanaveralPads/ModularLaunchPads (the **erector** =
  `TE_Ghidorah_Erector`, a `ModuleTundraDecoupler`, must be fired directly — [[dragonscreen-autopilot-rebuild-plan]]).
- **RealHeat:** real reentry heating (the PICA-X shield matters on entry).
- **Visual/other** (Parallax, Scatterer, EVE, TUFX, Waterfall, RealPlume, ReStock, TextureReplacer, etc.):
  no control effect.
- **MechJeb2 (installed):** a working RO ascent autopilot — the **reference/primary source** for the gimbal
  attitude law (port from `Desktop/mechjeb_src`, don't invent — [[mechjeb-source-reference]]).
- **ThunderAerospace (TAC-LS):** life support ([[dragonscreen-tac-life-support]]).
- **KSPWheel:** the landing legs (booster) — deploy the leg module directly, not the Gear action group.

### 6a. MISSION-ORCHESTRATION mods (time-warp, physics range, readouts) — 2026-08-28
⭐ **Policy (user 2026-08-28): NO external-mod DEPENDENCIES — we build the useful behaviour into DragonScreen.**
These are documented as REFERENCES + environment facts; the autopilot never requires them.
- **Kerbal Engineer Redux (KER) — INSTALLED** (`GameData/KerbalEngineer/KerbalEngineer.dll`). Live stage Δv/TWR,
  burn time, orbital + surface readouts, and the **NodeHalfBurnTime** centre-of-burn timing our `Maneuver.cs`
  already cites. Value to us: a **live cross-check** for `pure/StageStats` (B1) and the UPFG Tgo/Δv — if KER and
  our numbers disagree, one of us is wrong. We do NOT depend on it; our own FuelFlowSim-math port is the source.
- **KerbalReusabilityExpansion — INSTALLED.** The Falcon-9 REUSE hardware the booster flies with: grid fins
  (`Grid Fin M Titanium`), the landing legs, cold-gas — actuated directly by capability (`Actuator`/`GridFin`).
  It is the part source for booster recovery, not a control mod.
- **PhysicsRangeExtender (PRE) — PARTIALLY present (user: "partially already built").** PRE extends the vessel
  LOAD / unpack ranges so a separated craft stays FULLY SIMULATED instead of going on rails. Relevance: it lets a
  separated booster remain physically present (drag, thrust, control when focused) far past the stock ~2.5 km
  unpack / ~22 km unload, which is what makes a **focus-managed booster recovery** workable at all. ⛔ It does NOT
  remove the stock one-active-vessel limit — only the FOCUSED craft receives control input — so recovering the
  booster and flying the Dragon to orbit is still a SEGMENTED (focus-switched) affair, handled by
  `src/MissionConductor.cs`. If range extension is only partial, verify the booster is still loaded at its
  landing altitude before relying on auto-recovery.
- **BetterTimeWarpContinued (linuxgurugamer) — NOT installed; REFERENCE only.** Adds custom warp rates + **lossless
  physics warp** (accurate physics/thrust at accelerated time) and smoother warp-rate transitions. The USEFUL part
  — never overshooting a burn out of warp — we build ourselves: `pure/WarpPlan.cs` (the safe-rate + lead drop-out
  decision, headless-tested to never overshoot in one window) + `src/MissionConductor.cs` (applies it on stock
  `TimeWarp.WarpTo`, and forces real time the instant thrust is live). Stock physics warp (≤4×) is available to us
  if we ever want accelerated burns; we do not bundle or require the mod.

---

## 7. WHAT MUST CHANGE IN THE AUTOPILOT (the conclusion)
1. **DIRECT PART CONTROL, always** — no `StageManager`, no `ActionGroups`. Ignite/shut engines, fire
   decouplers, enable RCS + set thrust limits, deploy legs/fins/chutes, fire SuperDracos — all by module.
   ([[direct-part-control-hard-rule]])
2. **A fast direct GIMBAL attitude loop** (ControlLaw + Authority → `FlightCtrlState`), tuned to null the FAR
   transonic divergence with ±5° gimbal — the real fix for the max-Q loss of control. SAS only if it wins.
3. **Zero AoA through transonic/max-Q** (already implemented via the q-scaled AoA cap).
4. **Ullage before every ignition** (settle with RCS / ullage) — especially S2 after coast.
5. **Respect min throttle** in the max-Q bucket (SolverEngines).
6. **FDIR watches thrust delivery** (TestFlight can fail an engine) → abort; **abort deploys RealChutes**.
