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
| C1 | Abort chutes never deploy → 127 m/s splash | Only "armed" the RealChute (deferred to its envelope, never triggered) → 0 deploy activity. | **DONE✓ FLIGHT-PROVEN** — flight 202127 (max-Q LaunchEscape): log shows `RealChute DROGUES/MAINS activated`, main inflated 4.9 km → decelerated 121→8 m/s → **splash 2 m/s** (target 5–8). Crew survived. | ad3c85b |
| C2 | Abort attitude authority = 0 → capsule tumbles | stock `ModuleRCS.GetPotentialTorque`=0 on the separated capsule (Dracos fire 21 kN; normal flight reads ~735). | **DONE✓ FLIGHT-PROVEN** — flight 202127: fallback fired (log: "geometric RCS-torque estimate 4310 N·m/axis"), `ctrl_tq` 0→**1995 N·m**, roll settled to −8°/s (was 17). | this pass |

## HIGH — mission-blocking / functional bugs
| # | Issue | Root cause | Status | Ref |
|---|---|---|---|---|
| H1 | **Booster recovery fails — neither the upper stage reaches orbit NOR the booster lands** | ⭐ ROOT (Chris, flights 195316/200639/fragments): **PRE is NOT wired in** → a NON-ACTIVE vessel goes ON-RAILS (no thrust/control). So when focus is on the booster, the Dragon's S2 doesn't fire (drifts) → no orbit; and the booster wouldn't flip/land even when focused (a separate BoosterControl bug). (The focus-switch PLACEMENT bug 9201aa9 IS fixed; the ping-pong I "saw" was Chris manually switching to investigate.) The code's own comment says you can't fly both (stock control-input limit) — but MechJeb flies non-active loaded vessels via `vessel.OnFlyByWire`, so **dual-flight IS possible**: (1) extend the physics range ("our PRE" — set `VesselRanges` ourselves, no mod) to keep BOTH loaded; (2) keep the Dragon ACTIVE (S2→orbit) and drive the BOOSTER via `OnFlyByWire` (BoosterControl on the non-active vessel) — until their separation exceeds ~100 km (PRE's phantom-force limit), by which the booster should have landed. Plus fix BoosterControl's flip/entry/land. | **OPEN — the next major task** (dual-flight redesign) | — |
| H2 | **Rendezvous strands** — never reaches CW hand-off | ROOT (data): the low-thrust CIRCULARIZE took ~27 near-apoapsis orbits, drifting the chaser 246→6,000 km. AND the transfer reaches only ~80 km at apoapsis (131412: 86, 165302: 79) while the CW hand-off was 50 km → never caught → fly-past. | **FIXED⚑** — removed the slow circularize; `CwHandoffRangeM` 50→100 km so CW takes over on approach + does the terminal rendezvous (its valid regime). ⚠ verify CW converges from ~100 km next flight. | this pass |
| H3 | ~~SECO eccentric 200×160~~ **NOT a bug — was an assess-tool misread** | The real settled insertion is **200×197 (near-circular)** on all 3 post-fix flights (up from 200×181 pre-fix). The "200×160" was `assess_flight.py` reading the last S2Burn-phase row — ~0.3 s BEFORE cutoff, pe still rising 159→196. The vgo/inOrbit(pe≥195) cutoff works. | **DONE✓** (flight-confirmed) + tool bug fixed | this pass |

## MEDIUM — tuning / fidelity
| # | Issue | Notes | Status |
|---|---|---|---|
| M1 | Peak g 4.65 > 4.5 (crew) | limiter lags ~0.35 g at light mass; `S2GLimitG` 4.5→4.3→**4.1**. | **FIXED⚑** (verify ≤4.5) |
| M2 | Gravity-turn AoA ~8° (should be ~0 zero-AoA) | open-loop pitch program leads prograde; B9 LaunchTuner is the knob (needs recorder loss-columns + flights). | **DATA** |
| M3 | Phasing PHASE wait ~1.9 synodic (58 h) | appears to miss the 1st alignment; WarpPlan drops out 12 s early so shouldn't overshoot. | **DATA** (far-phase transition log added — pins it next flight) |
| M4 | Deorbit burn recorded **no Δv** | `PutDv` was never called by any glue; also `di.DvAppliedMps` (backstop cutoff) was unset. | **FIXED⚑** | d2de379 |
| M5 | 300 u MonoPropellant — consumed by nothing our autopilot fires | ⭐ RESOLVED from the LIVE **ConfigCache** (authoritative; the craft-dump `resourceName=MonoPropellant` FIELD is a legacy field a `PROPELLANT{}` block OVERRIDES — a first-clue trap I nearly fell for). The pod's Draco **`ModuleRCSFX`** (attitude + translation RCS) has `PROPELLANT{MMH 0.5629 / NTO 0.4371 / Helium}` → it burns **MMH+NTO**. A separate Draco **`ModuleEnginesRF`** (228 kN) has `PROPELLANT{MonoPropellant}` → the ONLY MonoProp consumer. But our autopilot does EVERY burn (rendezvous/departure/deorbit) via RCS translation (`ModuleRCSFX`, MMH+NTO — ReturnControl:15/110), and NEVER throttles the Draco `ModuleEnginesRF`. So the 300 u MonoProp is effectively dead weight (~1.2 t) FOR OUR PROFILE, and the RCS path is confirmed MMH+NTO (so propellant closure rides the 655/509 MMH/NTO budget, NOT MonoProp). | **RESOLVED — leave it** (removal is a low-value cfg change with a CoM/entry-trim risk on the unflown entry; harmless as-is). ⚠ was going to be removed on a WRONG premise — corrected. |
| M6 | 10 g steep entry in the DeorbitReturn | the abort entry is ballistic (no lifting entry) — but the research says high-energy aborts DO a controlled ballistic re-entry (up to ~13.6 g), so this is within the abort envelope, not a defect. | **OPEN-low** (verify vs research g-band) |

## RCS TRANSLATION SIGNS — ⭐ DERIVED (no longer guess-and-flip)
The Draco translation sign map is **derived from MechJeb's proven `MechJebModuleRCSController.Drive`** (it expresses
the world velocity error in the control-transform frame and writes `s.X←right, s.Y←forward, s.Z←up` — the y/z swap we
replicate — uniformly). Our demand is the desired accel `A = −error`, so every axis is `s = −Dot(A, axis)` → **all −1**.
Flight-anchored: rendezvous `ForwardSign = −1` (`s.Z=−1`) raised apoapsis correctly with the nose (`ct.up`) prograde
(flight 131412), proving `s.Z=−Dot(A,up)`; the same uniform mechanism gives −1 on right + forward.
| # | Item | Status |
|---|---|---|
| S4 | Rendezvous `ForwardSign = −1` | **DERIVED + FLIGHT-CONFIRMED** (the anchor) |
| S2 | Docking `RcsRight/Up/Fwd = −1/−1/−1` (was +1/+1/−1 — right/up were unreasoned defaults that INVERTED those axes → docking would diverge off the corridor) | **DERIVED — FIXED⚑** (verify docking converges next flight). Servo gains/tolerances stay first-cut tunables. |

## GENUINELY FLIGHT-RESOLVED — a sane value + a SAFE failure, resolved deterministically from ONE recording (NOT a guess to randomly flip)
| # | Item | Why it can't be derived to certainty | Safe? | Where |
|---|---|---|---|---|
| S1 | Booster: BC, rotation-correction sign, CrossSign, hoverslam ignition alt | booster geometry + never flown | opt-in segment | BoosterTargeting/BoosterControl |
| S3 | Entry `RollSign`/`RollRefSign` (roll-loop feedback), `CrossSign` (crossrange steer) | the KSP roll convention × the capsule CoM-lift geometry × the ad-hoc `MeasuredBankRad` atan2 frame — no analytic certainty without the KSP internals or a flown entry | **YES** — downrange uses `\|σ\|` (sign-independent) + crossrange self-reverses on the deadband (Apollo/Orion lateral logic, verified in `Entry.cs`); a wrong roll sign → the capsule spins about the velocity axis → near-ballistic survivable entry (SAS holds shield-forward), NOT a tumble | ReturnControl/EntrySteering |
| — | Deorbit target pe (SafePeFloor / DeorbitGuidance) | targets a pe band; tuned from the entry footprint | pe-floor gated | ReturnControl |

## COMPLETION-BLOCKERS fixed proactively (so a mission can actually reach the end)
| # | Issue | Fix | Status |
|---|---|---|---|
| B1 | **Near-field CW / docking read the UNLOADED ISS transform** = a stale placeholder → the terminal approach never converges (the likely docking stall, oc-nearfield) | Use the ORBIT position (`getPositionAtUT`) until the target is actually `.loaded` (within physics range); the transform only takes over for the final precision metres. Both RendezvousControl.FlyNearFieldCw + DockingControl. | **FIXED⚑** — this pass |
| B2 | Mission could stall waiting for a crew GO at each gate | `AutoAdvanceGates=true` — gates auto-progress; departure hands off cleanly. Verified in code (not a bug). | ✓ in place |

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
