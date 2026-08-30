# Grok assessment prompt — paste into Grok (game-building mode)

You are assessing a Kerbal Space Program mod, "DragonScreen", built in RSS/RO (Real Solar System + Realism
Overhaul — real scale, real physics, RealFuels, KSP 1.12.5). Assess the ENTIRE mod's architecture, approach, and
the DEVELOPMENT PROCESS. Tell us, in detail and specifically, what we are doing right and — more importantly —
wrong. Be brutally honest and technical; no generic praise.

## THE FINISHED PRODUCT (the goal — know this in detail)
DragonScreen is TWO things fused into one plugin:

1. **A user-interactive SIMULATION** that faithfully replicates the real SpaceX **Falcon 9 / Crew Dragon mission
   experience** — above all the Crew Dragon **touchscreen DISPLAYS** the astronauts use (NAV, telemetry, systems,
   rendezvous/docking, abort, life-support), interactive and matching the real screens, crew procedures and
   callouts of the real Crew-1/Crew-2 flights.

2. **A highly advanced AUTOPILOT** that flies Crew Dragon missions **FLAWLESSLY, end to end, on real physics:**
   - Launch → ascent (optimal PVG/UPFG guidance) → MECO → stage separation.
   - **Falcon 9 first-stage RECOVERY in BOTH modes:** **RTLS** (boostback + return to launch site) AND **drone
     ship / ASDS** (downrange landing) — entry burn, grid-fin steering, hoverslam to landing.
   - **SIMULTANEOUSLY** the upper stage (Crew Dragon + S2) continues to orbit — so the autopilot must **FLY TWO
     VESSELS AT ONCE** in the same flight: the booster recovery AND the ascent-to-orbit.
   - Orbit insertion (circular parking orbit) → rendezvous (phasing → co-elliptic → approach) → **docking with the
     ISS** → station-keeping → undock → deorbit → lifting entry (bank steering, CoM shift) → parachutes → splashdown.
   - Full fidelity: relative-nav filters (rel-GPS + IMU + LIDAR), abort modes, real propellants (MMH/NTO Draco
     RCS; RP-1/LOX booster), crew g-limits, no reaction wheels (RO strips them — attitude on gimbal + RCS only).

## CURRENT ARCHITECTURE
- One C# plugin, Roslyn-compiled. Repo: github.com/mups3y/DragonScreen.
- **pure/glue split:** `src/pure/*` = KSP-free logic (math, guidance, control), headless unit-tested (~731,000
  assertions). `src/*` = "glue" binding the pure logic to KSP's API. Build: `python build.py test` / `install`.
- **Mission spine:** `FlightDriver` dispatches phases (ascent → rendezvous → dock → deorbit → entry → chutes →
  abort) through a crew-gate state machine.
- **Control:** a ported MechJeb "BetterController" (cascade setpoint-PID + √-stopping slew curve); direct
  part-module actuation only (never stock staging/action groups); PWPF pulse-modulation on the RCS.
- **Two-vessel:** the Dragon stays the *active* vessel; the separated booster is flown *non-active* via its own
  OnFlyByWire, with Physics Range Extender keeping both loaded/unpacked.
- **Screens:** an in-game IMGUI/GL touchscreen UI driven by live telemetry.
- **Guidance:** ported/derived from MechJeb2 (UPFG/PEG ascent, Lambert, Hohmann, Clohessy-Wiltshire rendezvous,
  hoverslam, entry prediction) — a large "capability backlog" of MechJeb features being wired in piecemeal.

## CURRENT STATE (honest)
- Ascent reaches orbit with correct inclination (coplanar launch works). The screens render.
- Latest full-mission flight test exposed MAJOR problems: (a) at stage separation the booster's recovery engines
  fired and it **rammed the S2, pushing it off course**; (b) the second stage builds an **uncontrolled roll**
  (single-engine gimbal can't roll; RCS was disabled during the burn) reaching ~54°/s, which wrecks the
  rendezvous; (c) the **rendezvous never completes** — the capsule tumbles and drifts ~21 hours without docking;
  (d) **booster recovery does not work** (booster destroyed seconds after sep); (e) insertion is **eccentric/
  lofted**, not the intended circular parking orbit.
- **Docking, deorbit, entry, splashdown have NEVER been flown end-to-end. Booster recovery (RTLS or ASDS) has
  never succeeded.**
- The development has **thrashed**: many missions flown, little end-to-end progress; fixes are made and declared
  "done" without proper in-flight verification; root causes have been mis-attributed and re-analyzed repeatedly.

## ASSESS AND ADVISE ON
1. Is the overall **architecture and approach sound** for this goal? What is fundamentally right vs wrong?
2. **Two-vessel simultaneous flight** (active Dragon + non-active booster recovery): KSP's non-active vessel
   physics/control is limited — is fighting that a mistake? What is the correct way to recover a booster while
   flying the upper stage to orbit in KSP?
3. **Autopilot design** — is porting MechJeb piecemeal the right strategy, or should guidance/control be
   architected differently to achieve *flawless* end-to-end flight?
4. **Control** (gimbal + RCS, no reaction wheels, PWPF, the attitude cascade, roll authority on a single-engine
   stage) — sound? How should S2 roll actually be controlled?
5. **Separation/staging** — what is the correct real-Falcon-9 MECO sequence (engine shutdown, spool-down,
   decouple, ullage, S2 ignition ordering) and how should it be implemented so a thrusting booster never rams
   the upper stage?
6. **Rendezvous/docking** (phasing → co-elliptic → CW/Lambert → corridor) — right approach for RSS/RO?
7. **THE DEVELOPMENT PROCESS itself** — the biggest problem may be method, not code. What discipline makes this
   converge: test strategy, isolating one change per flight, verifying before declaring a fix, avoiding
   re-analysis churn, and knowing when a sub-system is truly done?
8. **Top 5 concrete things** to fix or change to actually reach a flawless end-to-end crew mission.

Give detailed, specific, technical advice.
