> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGH**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-28; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.
> ⚠ R1 §5.4: directly reusable for §B12.5's per-controller gates — but the gates themselves are the plan's to set, not this file's.

# Phase acceptance criteria — the expected signature of a clean flight, per phase

> **Why (2026-08-27):** most of the mission has never flown, so there's a large bug surface, and each first
> flight tends to expose "first-cut" glue bugs (frame signs, RCS axis signs, timing). This doc is the fast
> verification checklist: for each phase, the EXPECTED CSV/log signature, the PASS gates, the known first-cut
> knobs to check, and the FAILURE signatures that name the bug. Read the whole CSV + KSP.log
> ([[full-structured-flight-analysis]]); check these gates; a miss points straight at the fix.

Legend: **PASS** = must hold for the phase to be clean · **KNOB** = first-cut constant to verify/flip ·
**FAIL-SIG** = a failure signature and what it means.

---

## Ascent — pad → orbit  (PROVEN 214827)
- **PASS:** `[DragonScreen] ascent` log shows `inc=ACHIEVED/TARGET` with achieved **prograde** and within a few
  ° of target; UPFG cutoff on `tgo→0` (not depletion); SECO `pe > 0` (orbital, not suborbital); max-Q ≲ 35 kPa;
  pointing p95 < 0.5°; peak g ≤ crew limit (S1 3.5, S2 4.5) + small margin; RCS off through S2; single mass drop
  at separation (no g-spike).
- **KNOB:** azimuth sign (SelfCal.SteerSign) · `LaunchNodeSign` (flip if RAAN 180° off) · `FinalPitchDeg`/`TurnShape`
  (loft) · roll ref (radial-projected — verify roll rate bounded, no barrel-roll).
- **FAIL-SIG:** inc ~116° retrograde → frame bug (v.north/v.east) · fpa collapses then dives → loft/AoA-cap zeroed
  · thrust 0 with throttle 1 in S2 → ullage vapor-lock (throttle-0 reset) · g > limit at SECO → FullThrust100
  feeding a lagged thrust.

## Booster recovery  (NOT FLOWN)
- **PASS:** after sep, focus the booster; flip to retrograde on cold-gas; **entry burn on ThreeLanding (3
  engines)** bleeds speed to ~1300 m/s; grid-fin descent drives the predicted impact toward the droneship
  (down/cross error → 0); **landing burn on CenterOnly (1 engine, lit ONCE)**; touchdown ~0–2 m/s on legs.
- **KNOB:** BoosterTargeting BC measurement · rotation-correction sign · `CrossSign` · hoverslam ignition alt ·
  ⛔ engine-mode selected ABSOLUTELY while off (never NextEngineMode; 1 ignition per mode).
- **FAIL-SIG:** drifts at high AoA → grid-fin authority/sign · lands long/short → BC or ignition-alt · relights
  the center engine mid-burn → mode-cycle bug (must be one ignition) · tips over → touchdown vertical-speed/leg.

## Rendezvous / Phasing  (PHASING FIX INSTALLED, UNFLOWN)
- **PASS:** in PHASING, **pe RISES toward ~410 km (co-elliptic) or holds — NEVER falls**; `trans_z` not pinned
  at −1; `rcs_thrust_n` fires in raise bursts then coasts; range closes; hands to the AI standoff (~7.5 km) at
  the G9 gate. rv_burn_dv sane (≪ km/s), rv_range sane.
- **KNOB:** `ForwardSign` (−1; if pe FALLS during a "raise", it's flipped) · `SafePeFloorM` (150 km) ·
  `CwHandoffRangeM` (50 km) · `CoEllipticBelowM`.
- **FAIL-SIG:** ⛔ pe drops +178→−143 with trans_z pinned → the OLD self-deorbit (CW at far range) — must not
  recur · pe falls during raise → ForwardSign flipped (floor catches it at 150 km) · rv_burn_dv ~28 km/s →
  CW invoked far (guard failed).

## Docking  (NOT FLOWN)
- **PASS:** one leg per crew GO (WP0 400 m below → WP1 ~200 m V-bar → WP2 20 m → contact ~8 cm/s → capture);
  lateral miss nulled BEFORE along-axis closing; closing speed always ≤ MaxSpeedForDistance; nose shroud open;
  attitude-first-then-translate (never both); `DockedSide.Docked` true at capture.
- **KNOB:** RCS translation **signs** (`RcsRight/Up/FwdSign`) · servo gains · corridor/KOS tolerances.
- **FAIL-SIG:** drifts off-axis / overshoots the port → RCS sign or MaxSpeedForDistance · crosses the KOS →
  null-lateral-first not enforced (+ KOS auto-abort not wired — see GNC doc §5.3) · rotates while translating →
  attitude-first gate.

## Return — departure → deorbit → entry → splashdown  (NOT FLOWN)
- **PASS:** undock → departure CW burns out of the KOS → phasing → **trunk jettison → deorbit burn** closed-loop
  on measured Pe (stops AT target Pe, not past) → **CoM shifter Descent Mode engaged ONCE** → bank-angle entry
  holds predicted g within crew limit, banks for downrange + S-turn reversals → drogues ≤5.5 km / mains ≤1.8 km
  → splashdown ~5–8 m/s in/near the target zone.
- **KNOB:** deorbit `ForwardSign` + target Pe · `RollSign` / `EntrySteering.RollRefSign`/`CrossSign` + RollKp ·
  CoM `OffsetPercent` (L/D) · abort water-scan (SafeLandingSite RSS ocean detection).
- **FAIL-SIG:** deorbit over-burns → steep entry, high g · capsule tumbles on entry → CoM not engaged / roll loop
  fighting · mains too low → chute altitude gate · lands far from zone → entry footprint bias (needs the
  reentry-sim predictor, P2).

## Abort — any regime  (FLOWN, works)
- **PASS:** mode chosen from live state (LaunchEscape / AbortToOrbit / DeorbitReturn / KosRetreat /
  EmergencyUndock / RideItDown / SafeHold); **every path ends SAFE** (splash / safe orbit / standoff); trunk
  jettisoned before entry; shield-forward hold stops the tumble; chutes sequence-timed (pad) vs altitude-gated
  (high-energy); crew lands intact.
- **KNOB:** `SafeLandingSite` water-scan (RSS finds no ocean → 120 s timeout fallback) · `DeorbitGLimit` (3.5 g)
  · glide window · `EscapeBurnS`.
- **FAIL-SIG:** stranded (climbs, deorbit_phase=Idle) → commit gate/timeout · mains onto a tumbling capsule →
  reorient-before-chute · false abort at SECO → g threshold/debounce.

---

## The one-command gate (until the analyzers are wired into a single script)
After each flight: (1) grep the log for `[DragonScreen]` events; (2) run the full-CSV structured pass (whole
file, every column) — never spot-check; (3) `python tools/tuning_db.py` and read the flight-quality verdict;
(4) check the phase's PASS gates above; (5) any miss → the KNOB/FAIL-SIG names the fix. Batch the reasoned
fixes, re-fly. Cross-ref: [[full-structured-flight-analysis]] · [[dragonscreen-tuning-database]] ·
`docs/VALIDATION_AND_ROBUSTNESS.md` (these gates become the Tier-3 corpus regression asserts).
