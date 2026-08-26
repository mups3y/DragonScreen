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
  B. AttitudePilot (NEW glue) — port MechJeb BetterController: currentAttitude=rot*Euler(-90,0,0), error=Euler
     of Inverse(current)*requested with yaw NEGATED (order pitch,roll,yaw), arrestable-rate ω=√(2αθ) with
     α=ΣGetPotentialTorque/MOI, actuation=−torque/controlTorque → s.pitch/roll/yaw; MOI-scaled gains; pitch≈yaw.
     Replaces SAS in Steering.cs. + AoA moderation (AA method) as a FAR safety net.
  C. Ullage before every light (RCS aft until LowestUllage≥0.996) + clamp/erector release gate (release only
     at ≥99% measured thrust + no failed engine; reset the gimbal integral while clamped).
  D. PROVING FLIGHT: pad→orbit. Then E booster, F rendezvous, G docking, H return, I FDIR/self-cal hardening.

DO NEXT: Step A is DONE. Build Step B (AttitudePilot — MechJeb BetterController direct gimbal loop, replaces
SAS in Steering.cs), then Step C (ullage-settle before every light + clamp/erector release gate at ≥99%
thrust). Headless-test the pure-testable decision logic; keep build.py test green; commit when green. Then
hand a pad→orbit proving flight (Step D).

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
