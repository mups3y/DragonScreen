# Next-session resume prompt

Paste the block below at the start of the next session to pick up the autopilot build exactly where it left
off. (Rewritten 2026-08-26 to the approved plan: the autopilot is BUILT + INSTALLED but has never flown a
mission — 3 flights RUD'd at max-Q; the fix is the direct control stack. When something changes, update
"WHERE WE ARE" / "DO NEXT".)

---

```
Resume the RSS/RO Crew Dragon autopilot (DragonScreen). The autopilot is NOT named "CLAUDE" yet — the name is
EARNED by flying a full crew mission clean. It flies ANY crew mission (mission-as-data); Crew-2 is the
reference profile.

FIRST, read in full, in this order:
  1. the approved plan file  C:\Users\User\.claude\plans\snoopy-orbiting-hennessy.md  (§1 FRAME OF MIND +
     §2 GROUND-TRUTH AUTHORITY are the anti-regression guardrails — read every session; §4 is the build order)
  2. docs/AUTOPILOT_REBUILD_PLAN.md  (§0.0 folded master state; §5 Constants Register; §8b execution log)
  3. the memory dragonscreen-autopilot-rebuild-plan
Then continue the build.

WHERE WE ARE: the ENTIRE autopilot is BUILT + INSTALLED — pure L0–L7 (~900+ headless checks green) + all six
KSP glue seams (pad→orbit→booster→rendezvous→docking→return, DLL 237.5 KB). BUT it has never flown a mission:
the three in-game flights all RUD'd or lost the crew AT MAX-Q on ascent. Diagnosed causes: the glue still uses
stock SAS (too slow for FAR's transonic instability) + StageManager + action groups (hard-rule violation), and
earlier the launch clamp/erector wasn't released and chutes used stock ModuleParachute not RealChute. The deep
MechJeb + AtmosphereAutopilot harvest (docs/AUTOPILOT_HARVEST.md) supplied the concrete fixes.

THE FIX (approved plan Steps A–I, ASCENT-FIRST — get pad→orbit clean BEFORE any later phase):
  A. ✅ DONE + committed (fe837f0). Actuator (src/Actuator.cs) + pure role classifier (pure/Actuation.cs,
     tested in test/ActuationTest.cs) own all direct part control; every StageManager + actuation action-group
     call is gone (liftoff/MVac/SuperDraco ignition, MECO cutoff+interstage decouple, Dragon sep, RealChute,
     hold-downs, RCS, legs ported from MechJeb's ModuleWheelDeployment fsm-toggle, grid fins). ONLY the RCS
     *master* toggle remains (KSP requires it for FlightCtrlState translation — MechJeb-canonical), plus
     Steering SAS (Step B) + VesselData Light display (out of scope).
  B. ✅ DONE + committed (abb01eb). pure/AttitudeLoop.cs (MechJeb BetterController cascade + Pid2 port,
     13 headless checks incl. closed-loop convergence) + src/AttitudePilot.cs (frame glue: Euler(-90,0,0),
     error=(euler.x,euler.z,-euler.y), live MOI/angVel/ΣGetPotentialTorque, s.pitch/yaw + roll-damp via a new
     FlightDriver fly-by-wire channel). Steering.Point/PointNoRoll delegate to it; SAS behind UseGimbalLoop=false
     fallback only. Recorder att_*/act_*/ctrl_tq_* columns (ASCENT filler only so far — add PutAttitude to the
     booster/rendezvous/docking/return fillers as those proving flights come, Steps E-H). Self-reviewed +
     fixed (e6a5cdb): RCS torque gated by the master, roll channel released when dampRoll=false, attitude
     released on abort; MechJeb sign/axis port verified faithful. STILL TODO: AoA moderation (FAR safety net,
     §3.2) deferred to Step I; ⚠ Step C MUST reset the gimbal-loop PID integral while clamped (§3.4 windup).
  C. ✅ DONE + committed (06b9f86). pure/IgnitionGate.cs (clamp + ullage decisions, 13 checks) + src/Ullage.cs
     (RealFuels ullage via reflection). FlightDriver clamp gate: light octaweb, HOLD hold-downs until ≥99% of
     current-conditions thrust (maxFuelFlow*flowMultiplier*Isp*g0), reset gimbal integral while clamped, safe-
     abort a failed light (shut down, keep clamps, no SuperDraco). AscentControl S2 ullage settle: fire aft RCS
     (s.Z=-1) until settled, ignite past a 2 s min coast / 6 s backstop. Recorder ullage_stab/clamp_frac/clamp_held.
  D. PROVING FLIGHT: pad→orbit. Then E booster, F rendezvous, G docking, H return, I FDIR/self-cal hardening.

DO NEXT: Steps A+B+C DONE — the ascent stack is complete + INSTALLED. **Step D = the pad→orbit PROVING FLIGHT**
(the user flies it; I cannot fly in-game). Fly AUTO SEQUENCE pad→SECO→Dragon-sep, then read the CSV with
assess_flight.py. WATCH: clamp releases only at full thrust; vertical rise → one pitch kick → zero-AoA gravity
turn HELD BY THE GIMBAL LOOP through max-Q (att_point_deg small, no divergence — the RUD fix); MECO→interstage
sep→ullage settle (ullage_stab climbs to ≥0.996)→S2 light→UPFG→SECO→Dragon sep. First-cut items to tune from
the CSV (batch): UPFG Iy plane normal / SECO cutoff, ENU heading sign (SelfCal.SteerSign), the attitude sign
(if att_point_deg DIVERGES the actuation sign is flipped — set Steering.UseGimbalLoop=false to fall back to SAS
and confirm, or flip the sign). Do NOT proceed to Step E until ascent-to-orbit is clean. Keep build.py green.

AMENDED WORKFLOW RULES (2026-08-26 — these SUPERSEDE the older discipline):
- BATCH FIXES: apply as many well-reasoned fixes as a phase needs, then fly to verify the batch. Do NOT fly a
  separate flight per single constant. One disciplined root-cause pass may yield multiple fixes.
- YOU MAY COMMIT + INSTALL autonomously once build.py test is green and the change is reasoned (keeps a backup
  to revert to). The old "never commit/install without me" rule is lifted.
- INSTRUMENT everything but keep the FPS drop minimal (modest sample rate, no heavy per-tick work).

GROUND-TRUTH AUTHORITY: live ModuleManager.ConfigCache > flight CSV+KSP.log > the .md docs. Neutralized stale
numbers: octaweb ignitions = 1 PER MODE (3 total: liftoff/entry/landing, no mid-burn 3→1) NOT "4"; Merlin spool
INSTANT NOT "3–5 s"; engine thrust/Isp read live; capsule entry aero MEASURED live; AtmosphereAutopilot REMOVED.

NON-NEGOTIABLE RULES:
- ⛔ DO NOT read/reference/resurrect plugin/_deleted_autopilot/ — old unproven code. Build fresh from the
  research + primary sources (cite them). The SCREENS were kept.
- ⛔ DIRECT PART CONTROL ONLY — never StageManager, never Vessel.ActionGroups. Actuate by capability.
- ⛔ SAS lost control 3× — build the direct gimbal loop. SAS only if it demonstrably wins.
- FULL CONTROL AT ALL TIMES: guidance always outputs a definite attitude — never floating. No reaction wheels
  (16 Dracos share rotation+translation) → attitude-first-then-translate, never both at once. Open the nose
  shroud before any Draco burn. Draco = MMH+NTO.
- Crash-investigator method: read the CSV + KSP.log together, one disciplined root-cause pass. NO Python sims
  — validate with headless C# tests + the flight corpus (assess_flight.py). Full fidelity, never "safe".
```
