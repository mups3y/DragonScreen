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

_Last audit: 2026-08-29 (the 08:15–08:58 batch 081557 / 083358 / 085113 / Probe_085405 — rendezvous + deorbit + entry + booster; DLL 00:26 INCLUDES F1's per-axis r×F fix)._

## ⭐⭐ 2026-08-29 (08:15–08:58) EXHAUSTIVE AUDIT — the attitude loop is broken across the WHOLE mission
> Systematic per-flight per-phase pass (all actuators, all axes, fuel, pointing, rates, orbit). The headline: F1's
> "fix" did NOT work, and the SAME broken attitude loop shows up in rendezvous, deorbit-settle, entry-orient AND the
> booster entry burn — one systemic controller failure, many faces. No tunnel vision: every affected phase listed.

| # | Issue | Evidence (this batch) | Status |
|---|---|---|---|
| **G0** | **F1's RCS-torque fix NEVER ACTIVATES → the loop still thrashes.** `AttitudePilot.ControlTorque`'s geometric per-axis r×F fallback is gated `if (rcsOn && rcsReported < RcsTorqueFloorNm)` with **floor = 1.0 N·m**; the buggy stock `GetPotentialTorque` reports **~2 N·m** (> floor) so the fallback is SKIPPED and the loop uses 2 N·m. `AttitudeLoop.Axis`: `maxAlpha=τ/MOI≈0.05` and `actuation=−(MOI·α)/controlTorque` → dividing by ~2 N·m **saturates to ±1**, oscillates (sign-flips), fires opposing Dracos that cancel to ≈0 net torque, never arrests the tumble → burns RCS continuously to no effect. | `ctrl_tq_pitch` median **2.11 N·m** while `rcs_thrust_n` **15–18 kN**; MOI ~40; `act_pitch` sign-reverses **37%** of ticks; rate not arrested. | **OPEN — the ROOT.** Fix: never trust stock RCS GetPotentialTorque — ALWAYS use the geometric r×F (or max(reported, geometric)); MechJeb never trusts stock for RCS. |
| **G1** | **Rendezvous = thrusters pulse the whole phase, all directions** (Chris saw it in-game). = G0 on the capsule Dracos. | 081557 RV/Phasing: RCS fires **100%** of 4080 rows @18 kN, ptErr p50/p95/max **83/152/178°**, ~1.2 t RCS spent, never reached target. | **OPEN** (= G0) |
| **G2** | **Entry orientation tumbles — capsule cannot hold shield-forward.** = G0 on the capsule during OrientEntry. | 083358 DEO/OrientEntry (4302 rows): rate **pitch 63 °/s**, yaw act saturated **93%**, ptErr **178°**. A crewed entry tumbling at 63 °/s is fatal. | **OPEN** (= G0; crew-critical) |
| **G3** | **Booster entry burn tumbles at 113–156 °/s → burns ALL landing fuel mispointed.** The *gimbal* version of the attitude failure (aerodynamically unstable booster the loop can't hold). | Probe_085405 BOOST/EntryBurn (919 rows): rate **pitch 113 / yaw 156 °/s**, act saturated **100% all 3 axes**, ptErr **178°**, burned **83,144 kg** (nearly all fuel) pointing backwards. | **OPEN — crew-adjacent** (root of the booster loss) |
| **G4** | **Booster landing burn never ignites** → ballistic crash @118 m/s. Downstream of G3 (no fuel) + the hoverslam ignition never fires. | Probe_085405 LandingBurn: throttle **0.00** + thrust **0 N** for all 480 rows; `ignite_alt_m` just echoes current altitude (never computes a real ignition altitude); min alt −2.1 m @118 m/s. | **OPEN** |
| **G5** | **Rendezvous far-field strategy** — over-raises ap PAST the target + breaches the pe floor + 7.7-day phasing. | 081557: ap raised to **413 km** (target ~400) while pe dropped to **150 km** (floor breach); range 2570→100 km over ~665,000 s; the transfer burn was NOT clean prograde (pe shouldn't drop). | **OPEN** (F3 continued; needs the Hohmann/Lambert planner MechJeb uses) |
| **G6** | **S2 ascent yaw gimbal oscillation** — the gimbal (not RCS) also oscillates. | 081557 ASC/S2Burn: `act_yaw` sign-reverses **47%**, yaw ptErr max **43°** (pitch fine). Possibly shared control-law sensitivity to the yaw authority estimate. | **OPEN — DATA** (secondary; watch after G0) |
| **G7** | **Deorbit felt 9.75 g.** Above nominal entry loads; check the g-limiter / deorbit-burn profile. | 083358: max g **9.75**. | **OPEN — low** |

## ⭐ 2026-08-29 FLIGHT-TEST ISSUE LIST (the systematic CSV+log findings)
| # | Issue | Evidence (CSV+log) | Status |
|---|---|---|---|
| **F1** | **DEORBIT / abort can't hold retrograde → burns fire mis-pointed → DOES NOT deorbit → 4 vessels stranded** | 235430: DeorbitReturn burned **2300 kg ≈ 900 m/s** but pe only dropped ~3 km; ptErr **p95 134° / max 178°**, act_sat BROKEN. 232712/000624 same (128-178°). Log: the geometric RCS-torque estimate read **111185 / 4785 / 2393 / 2000 N·m** (23× swing). ROOT: `AttitudePilot.ControlTorque` summed `thrusterPower × \|arm\|` over ALL 16 Dracos on EVERY axis (~9× over) → the arrestable-rate law commanded ω too high → overshoot/oscillation around retrograde → burn fired mid-swing. | **REOPENED → see G0** (2026-08-29 08:15+ batch): the per-axis r×F fix is correct but **GATED behind `RcsTorqueFloorNm=1.0` which the buggy stock ~2 N·m clears, so it never runs** → the loop still thrashes. |
| **F2** | **AUTO SEQUENCE not state-aware** — pressing it in orbit restarts the launch / sits in "ASC/Done" instead of working out rendezvous vs deorbit | 235031: a stranded vessel idling in `ASC/Done` (orbit 407×172) — the autopilot didn't know what to do next. Chris's core ask. | **FIXED⚑** — `CrewProcedureOps.ResumeIndex` maps live state → the right step (pad→countdown; orbit+targeted→rendezvous; docked→departure; orbit-otherwise→deorbit; entering→ride-it-down). See `docs/SEQUENCE_MAP.md`. |
| **F3** | **Rendezvous reached 100 km (CW hand-off) but did NOT dock** — 34-hour phasing, then aborted | 232712: range 4653→481→100 km over met 0-123456 s; at 100 km ptErr p95 113°, burn_dv 48.7, act_sat BROKEN. The near-field CW handoff doesn't converge to the port. (May share F1's attitude root — verify after the torque fix.) | **OPEN — DATA** (re-fly with F1 fixed; then tune the CW terminal) |
| **F4** | **Deorbit water-site scan reads 0/130 over water** (RSS ocean detection) → WATER DEORBIT can't target water | log: "deorbit site scan: 0/130 ground-track samples over water; idx=-1" (×2). `body.TerrainAltitude(lat,lon) < 0` never true in RSS. | **OPEN** — DEORBIT NOW (land-anywhere) works regardless; needs the RSS ocean check fixed for water-targeting. |
| **F5** | max g **4.53 > 4.5** (crew cap) | 232712: max g 4.53. S2GLimitG=4.1 but the limiter still overshoots ~0.4 g at light mass. | **OPEN — tune** (lower S2GLimitG or fix the taper lead) |
| **F6** | early-ascent actuation saturation | VerticalRise act_sat 0.264, Coast 0.875 (roll axis) BROKEN | **OPEN — low** (transient; verify vs the F1 torque fix) |
| **F7** | PAD SAFE-ABORT on re-launch (0% thrust) | log 235032: octaweb 0% — a re-launch after a REVERT spent the octaweb's one RealFuels ignition | **not a code bug** — operational: full restart after a pad safe-abort revert (already documented). |

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

## ⭐ PERFECT CONTROL — vehicle real-world accuracy (HIGH priority; ledger = `docs/VEHICLE_AUDIT.md`)
| # | Item | Status | Ref |
|---|---|---|---|
| V0 | **The whole-vehicle audit** — correct RCS mode / thruster limits / engine modes / throttle / fuel types / tanks / loads at ALL times end-to-end, AND every part the real Falcon 9 / Crew Dragon equivalent | **FRAMEWORK BUILT** — §B control matrix VERIFIED from code; §C/§D accuracy audit SEEDED. ⛔ Needs a **fresh part dump next flight** to walk every part one-by-one (§A/§E). | VEHICLE_AUDIT.md |
| V1 | Draco RCS thrust `thrusterPower` (last seen 2 kN = 5× real ~400 N) | **DECISION OPEN** (real accuracy vs controllability) — VEHICLE_AUDIT §D-1 | fresh dump |
| V2 | Octaweb SL thrust (real 7,600 kN) + MVac (real 934–981 kN) vs our config | **VERIFY vs fresh ConfigCache** (don't change on memory) — §D-2/3 | fresh dump |
| V3 | Merlin gimbal range (patch disabled → stock 2° vs real ~±5°) | **VERIFY + re-enable if 2°** — §D-5 | fresh dump |
| V4 | Chute count (real 2 drogue + 4 main), cold-gas propellant, tank loads | **VERIFY** — §D-6/§E | fresh dump |

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
- **DEORBIT NOW / WATER DEORBIT rescue buttons — WIRED⚑** (were no-ops; commit 979959e). `FlightCommands.Run` → `FlightDriver.RequestDeorbit(landAnywhere)` → `AbortControl.ForceDeorbit` (DeorbitReturn engine, land/water flag, gear-after-land-touchdown). UNFLOWN (the rescue/data flight).
- **MissionOps screen buttons — RESOLVED⚑ (user 2026-08-28):** RENDEZVOUS + AUTODOCK **removed** (AUTO SEQUENCE flies them); UNDOCK+LAND → just **UNDOCK** (releases the hooks + `MarkDockedThisMission`). Post-dock, pressing AUTO SEQUENCE RESUMES at the departure step (`DepartureStepIndex`, `returnLeg=true`) — never re-docks; the pure Departure FSM eases carefully out of the 200 m ISS KOS (corridor-safe hops) → deorbit → entry → splash. UNFLOWN.
- FDIR live-wiring into FlightDriver (observe-first, then acting) — plan Step I. **OPEN**
- Screens: page router "NOT YET WIRED" (DragonScreenMonitor:79); screen-data placeholders (ScreenPainter:148); NAV globe-mirror + orbit-line-close bugs (S-A). **OPEN** (Part II)
- B9 recorder loss-columns; B2 isolated-aero estimator FEED; B3 RcsBalance glue; B8 targeting glue; B11 FDIR wiring. **OWED** (flight-gated)

## RESOLVED-VERIFIED (tick 3) — moved out when proven
- Ascent reaches orbit + inc 51.65 (flight-proven ×2). Everything else above is tick-1/2 or open.
