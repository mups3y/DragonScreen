# DragonScreen — ISSUE REGISTER (the exhaustive-audit ledger)

> ⭐ **THE ENFORCEMENT MECHANISM for [[fix-everything-means-exhaustive]].** "Fix everything" = keep auditing until
> this register finds nothing, fixing research-backed (guesses last resort). EVERY known issue lives here with a
> status; nothing is silently dropped. An issue is REMOVED only when **FIXED and flight-verified (tick 3)**.
> Re-run the audit every pass — cross-check **research × flight-data × code**; grep the code's own uncertainty
> markers (`best-guess`, `first-cut`, `unflown`, `TODO`, `placeholder`, `owed`, `⚠`, `assume`). Mirror the live
> ones into the dashboard "Open Claims" tab.

**Status key:** `FIXED⚑` = fix built, headless-green, installed, **UNFLOWN** (tick-1 only) · `OPEN` = confirmed, not
yet fixed · `DATA` = root needs an instrumented flight to pin (do NOT guess) · `SIGN` = a best-guess sign/param that
only a flown phase resolves (guessing is last-resort → wait for data) · `DOC` = stale comment / doc-rot · `DONE✓` =
fixed + flight-verified.

_Last audit: 2026-08-28 (post the 5-flight batch 165302–180029)._

## CRITICAL — crew survival
| # | Issue | Root cause (evidence) | Status | Ref |
|---|---|---|---|---|
| C1 | Abort chutes never deploy → **127 m/s splash, crew dead** | RealChute canopies never deployed (0 deploy-log activity; splashed with chutes attached). The code only "armed" (deferred to RealChute's envelope, which never triggered). | **FIXED⚑** — `DeployChutePart` now fires the real "Deploy Chute" (GUIDeploy) once. | ad3c85b |
| C2 | Abort attitude authority = **0 → capsule tumbles ~17°/s, no control** | `AttitudePilot.ControlTorque` returned ~0 on the SEPARATED capsule: stock `ModuleRCS.GetPotentialTorque` = 0 even with the master ON + Dracos able to fire 21 kN (a known stock bug; normal flight reports ~735 N·m). | **FIXED⚑** — MechJeb-style geometric RCS-torque FALLBACK when the reported RCS torque is ~0. ⚠ magnitude to confirm on an instrumented abort. | this pass |

## HIGH — mission-blocking / functional bugs
| # | Issue | Root cause | Status | Ref |
|---|---|---|---|---|
| H1 | **Booster-recovery toggle did NOTHING** when ARMED | `TryFocusBooster` sat behind the `BurnCommanded` early-return → from MECO (ullage RCS + S2 = continuous "burn") the focus-switch was blocked every frame; the booster unloaded before it could be focused. | **FIXED⚑** — focus-switch now runs before the burn gate. | 9201aa9 |
| H2 | **Rendezvous strands** — CoElliptic reached but CoElliptic→CW→dock hand-off never completes | Near-field CW reads `tgt.GetTransform().position`, a placeholder when the ISS is unloaded past physics range (RendezvousControl.cs:95). Likely the near-field stall. | **OPEN** (needs a flight that reaches the hand-off + PRE range check) | — |
| H3 | **SECO eccentric 200×160**, not circular | Only `ai.TargetApoapsisM=200` is set — UPFG's terminal target is eccentric (vgo→0 at pe~160). My vgo-cutoff is correct; the TARGET isn't circular. | **OPEN** (UPFG terminal-condition fix; careful — proven ascent) | — |

## MEDIUM — tuning / fidelity
| # | Issue | Notes | Status |
|---|---|---|---|
| M1 | Peak g 4.65 > 4.5 (crew) | limiter lags ~0.35 g at light mass; `S2GLimitG` 4.5→4.3→**4.1**. | **FIXED⚑** (verify ≤4.5) |
| M2 | Gravity-turn AoA ~8° (should be ~0 zero-AoA) | open-loop pitch program leads prograde; B9 LaunchTuner is the knob (needs recorder loss-columns + flights). | **DATA** |
| M3 | Phasing PHASE wait ~1.9 synodic (58 h) | appears to miss the 1st alignment; WarpPlan drops out 12 s early so shouldn't overshoot. | **DATA** (far-phase transition log added — pins it next flight) |
| M4 | Deorbit burn recorded **no Δv** | `PutDv` was never called by any glue; also `di.DvAppliedMps` (backstop cutoff) was unset. | **FIXED⚑** | d2de379 |
| M5 | 300 units MonoPropellant = **dead weight** | vestigial stock `ModuleRCSFX` RO didn't strip; real Dracos are `ModuleEnginesRF` MMH+NTO. | **OPEN** — remove via cfg AFTER confirming the RCS path (tied to C2). |
| M6 | 10 g steep entry in the DeorbitReturn | the abort entry is ballistic (no lifting entry) — but the research says high-energy aborts DO a controlled ballistic re-entry (up to ~13.6 g), so this is within the abort envelope, not a defect. | **OPEN-low** (verify vs research g-band) |

## BEST-GUESS SIGNS/PARAMS — resolved only by a flown phase (guessing is last-resort → WAIT for the data)
| # | Item | Where |
|---|---|---|
| S1 | Booster: BC, rotation-correction sign, CrossSign, hoverslam ignition alt | BoosterTargeting/BoosterControl (never flown) |
| S2 | Docking: RcsRight/Up/FwdSign, servo gains, tolerances | DockingControl (never reached) |
| S3 | Entry: RollSign, RollRefSign/CrossSign, deorbit target pe | ReturnControl/EntrySteering (never completed nominally) |
| S4 | Rendezvous: ForwardSign (−1 held so far) | RendezvousControl |

## DOC-ROT — comments contradicting the code (fixed this pass)
| # | Was | Reality | Status |
|---|---|---|---|
| D1 | Steering.cs:7 "FIRST-CUT INNER LOOP = stock SAS" | AttitudePilot (direct gimbal/RCS loop) is live (`UseGimbalLoop=true`); SAS is fallback-only. | **DOC-fixed** |
| D2 | ReturnControl.cs:17 "BANK-ANGLE ENTRY STEERING IS NOT YET WIRED" | It IS wired (Entry.Guide + RollSign bank loop + CoM shifter). | **DOC-fixed** |

## UNWIRED / OWED (tracked, not yet due)
- FDIR live-wiring into FlightDriver (observe-first, then acting) — plan Step I. **OPEN**
- Screens: page router "NOT YET WIRED" (DragonScreenMonitor:79); screen-data placeholders (ScreenPainter:148); NAV globe-mirror + orbit-line-close bugs (S-A). **OPEN** (Part II)
- B9 recorder loss-columns; B2 isolated-aero estimator FEED; B3 RcsBalance glue; B8 targeting glue; B11 FDIR wiring. **OWED** (flight-gated)

## RESOLVED-VERIFIED (tick 3) — moved out when proven
- Ascent reaches orbit + inc 51.65 (flight-proven ×2). Everything else above is tick-1/2 or open.
