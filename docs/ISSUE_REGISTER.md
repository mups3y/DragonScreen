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

_Last audit: 2026-08-29 (the 08:15–08:58 batch 081557 / 083358 / 085113 / Probe_085405), **CSV + KSP.log cross-checked** (per the revised Operating Instruction). ⚠ This section CORRECTS an earlier CSV-only pass whose conclusions the log disproved._

## ⭐⭐ 2026-08-29 — INSTRUMENT-VALIDATED audit (P0.0 first; earlier CSV-only claims RETRACTED)
> A first pass on the CSV alone produced a confident but WRONG story ("attitude loop thrashing across the whole
> mission — G0 systemic root, entry tumble, booster tumble, S2-yaw"). Cross-checking against KSP.log disproved it.
> The lesson IS the top finding: **the recorder is not trustworthy during warp** — everything below the P0-line is
> "confirmed by the log", not by the CSV alone. Retractions are listed explicitly so no future session re-inherits them.

**P0.0 — THE INSTRUMENT IS UNTRUSTWORTHY (the meta-root; fix before believing any warp-heavy flight).**
| # | Issue | Evidence | Status |
|---|---|---|---|
| **I1** | **Recorder FREEZES live-control columns during on-rails warp.** `rcs_thrust_n`, `rate_*`, `ctrl_tq_*`, `act_*` hold their last realtime value through warp. This *manufactured* the fake "94% RCS firing / thrash / tumble". | In 081557 rendezvous, **all 3,493 warped rows show RCS "firing", 0 idle** — physically impossible under on-rails warp (control is off). Realtime-only firing rows show **ptErr 0°, rate 0.2 °/s** = control is FINE when it actually fires. | **OPEN — P0.0(a).** Blank/zero those columns during warp, or stamp an `is_warping`/`warp_rate` column so warp rows can't be misread. |
| **I2** | **Booster delivered thrust/throttle is NOT instrumented** — `thrust_n = 0` for the *entire* booster file (max 0 N anywhere). Cannot tell from the CSV whether the booster ever fired. | Probe_085405: `thrust_n` max 0; the 83,144 kg "burn" was a **single-frame** mass drop (a decouple/unload artifact), not a gradual burn. | **OPEN — P0.0(b).** Record the booster vessel's real delivered thrust + throttle. |
| **I3** | **Ignition-retry SPAM** — `"S2 (MVac) ignition — 0 engine(s) lit"` logged **1,246×** (every tick) on failed flights. | KSP.log 08:58: spam while the vehicle is already falling (7 km, fpa −72°, no ullage). | **FIXED⚑ (C0, commit bd131f3)** — `Actuator.IgniteSecondStage` logs on change or ≥2 s. Tick-3 to confirm. |

**C0 TICK-3 RESULT (2026-08-29, flight `Crew-2_20260829_113314`, CSV+log):** **I1 ✅ FIXED+VERIFIED** (warp rows stamped `warp_rate`, all control columns blank there, nav/eng-state kept; realtime rows live). **I3 ✅ FIXED+VERIFIED** (one ignition log line, was 1,246×). **I2 ✅ column proven** on octaweb+MVac (`eng_ignited` 1→0→1→0 tracking liftoff/coast/S2/SECO; `thrust_n` non-zero when firing) — but the **booster focus-switch was NOT exercised** this flight, so booster-ignition-visibility is inferred (same vessel-agnostic `EngineState` code path), to be confirmed on the next booster flight (C1 exercises it). No physics-warp (LOW) segment flew → the "LOW rows stay live" gate is code-verified only. **VERDICT: C0 passes on I1/I3; the instrument is trustworthy for warp + main-engine ignition. Proceed to C1; confirm the booster-focus case incidentally there.**

**C0 status (2026-08-29):** I1/I2/I3 addressed (commit `bd131f3`), Grok-approved (Tick 2), Tick-3 flown — see result above. Deferred to a LATER instrument pass (NOT blockers — Grok): a **requested-vs-delivered** column split (cleaner than blank-on-warp long-term); **RealFuels ignition-count + ullage state** on the booster (only if the first Tick-3 flight still leaves ignition ambiguous). Old CSVs (pre-`warp_rate`) remain un-refixable — accept.

**RETRACTED (were asserted from the CSV, DISPROVEN by the log — do NOT re-raise without new evidence):**
- ~~G0/G1 "attitude loop thrashes, fires opposing Dracos to ~0 net torque, wastes fuel"~~ — the "firing" was I1's frozen-warp artifact; realtime firing points dead-on (ptErr 0°). The RCS authority estimate flicker (below) is real but **not shown to break control**.
- ~~G2 "entry tumbles at 63 °/s, crew-fatal"~~ — OrientEntry has good authority (`ctrl_tq` ~12,900 N·m), median rate 0.2–1 °/s, ptErr 36°. The 63°/s was a transient, mis-cited as typical.
- ~~G3 "booster tumbles at 156 °/s, burns all fuel mispointed"~~ — 156°/s was a max transient (median 0.2°/s); the "burned all fuel" was I2's single-frame artifact. Booster engine behaviour is **unknowable** from this data.
- ~~G5 "far-field over-raises ap PAST the target"~~ — the log shows the co-elliptic worked: `Phase→Transfer→Coast`, ap **200→405 km toward the 407 km park target** (correct), then the pe-floor guard held burns.
- ~~G6 "S2 yaw gimbal oscillation"~~ — ptErr median 0.0°, well-controlled; over-flagged sign-dither.

**CONFIRMED by the log (trustworthy):**
| # | Issue | Evidence (KSP.log / orbit telemetry) | Status |
|---|---|---|---|
| **L1** | **Ascent WORKS.** MECO → ullage READY → S2 lights → SECO → Dragon sep, reaches orbit. | 081557: `MECO — octaweb shut` → `Ullage reflection READY` → `S2 (MVac) ignition — 1 engine(s) lit` → `SECO — Dragon separated`; orbit 200×197, inc 51.64°. | **WORKING** (Tick-3 for ascent-to-orbit). |
| **L2** | **Far-field rendezvous progresses correctly**, then a real anomaly: **pe dropped 197→150 km** (floor guard held it). Why pe fell during a prograde-raise campaign is unexplained. | Log: `RV far-field: Phase → Transfer (ap 200 pe 197 park 407) → Coast (ap 405 pe 199)`; then `RV pe-floor: pe 150 ≤ floor 150 — burns HELD`. | **OPEN — DATA.** Real (orbit telemetry, not frozen). Investigate the pe drop after P0.0. |
| **L3** | **Booster engine never ignites during recovery** — SUPERSEDED BY H1b. The non-active-booster FSM now runs cleanly (fins, mode switch AllEngines→ThreeLanding, attitude) but `eng_ignited=0` the whole descent (flight 144114, log `booster recovery drive: ... engLit=0`). | KSP.log booster-recovery-drive lines. | **→ H1b (ullage, next campaign).** The FSM/mode-switch works; only ignition is open. |
| **L6** | **Booster↔upper-stage separation instrument reads 0 km** — `LogSeparation` logged `sep 0 km (max 0 km)` for the entire recovery (flight 144114) while both were clearly km+ apart and descending. | KSP.log `booster recovery: sep 0 km` ×many, both loaded+unpacked. | **OPEN — instrument bug (low).** `FindById(dragonId)`/CoM-diff is wrong; fix when the sep number is next needed (it sizes `PreRangeKm`). Do NOT chase now. |
| **L4** | **RCS authority estimate flickers** — stock `GetPotentialTorque` reports ~2 N·m for the Dracos; the geometric r×F fallback only fires when stock reads `<1.0` (logged **3×** total). So the loop usually runs on ~2 N·m. **Latent** — not proven to break control (real firing points fine), but a real fragility. | CSV `ctrl_tq` ~2 vs occasional geometric ~797; log "using per-axis geometric RCS torque" ×3. | **OPEN — latent.** Consider always-geometric or `max(reported,geometric)`; verify against a trustworthy flight. |
| **L5** | **RemoteTech duplicate-key exceptions when two vessels are loaded** (`Timing0 threw: same key … Falcon 9 Interstage / Dragon Trunk Adapter`), at every sep/recovery. Relevant to the PRE dual-flight. | KSP.log 08:18 / 08:24 / 08:54 / 08:57. | **OPEN — DATA.** A mod interaction, not our code directly; watch during PRE recovery. |
| **G7** | Deorbit felt 9.75 g (083358) — above nominal. Real (g is an accelerometer read, not frozen). | CSV `accel_g` max 9.75. | **OPEN — low.** |

## ⭐ 2026-08-29 FLIGHT-TEST ISSUE LIST (the systematic CSV+log findings)
| # | Issue | Evidence (CSV+log) | Status |
|---|---|---|---|
| **F1** | **DEORBIT / abort can't hold retrograde → burns fire mis-pointed → DOES NOT deorbit → 4 vessels stranded** | 235430: DeorbitReturn burned **2300 kg ≈ 900 m/s** but pe only dropped ~3 km; ptErr **p95 134° / max 178°**, act_sat BROKEN. 232712/000624 same (128-178°). Log: the geometric RCS-torque estimate read **111185 / 4785 / 2393 / 2000 N·m** (23× swing). ROOT: `AttitudePilot.ControlTorque` summed `thrusterPower × \|arm\|` over ALL 16 Dracos on EVERY axis (~9× over) → the arrestable-rate law commanded ω too high → overshoot/oscillation around retrograde → burn fired mid-swing. | **PARTIALLY SUPERSEDED → see L4** (2026-08-29 08:15+ batch, log-verified): the r×F fallback is real but rarely engages (stock reads ~2 N·m > the 1.0 floor). HOWEVER the "still thrashes" claim was a warp artifact (I1); realtime deorbit control is now UNVERIFIED, not proven-broken. Re-fly with a trustworthy recorder (P0.0) before concluding. |
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
| H1 | **Booster recovery — the DUAL-FLIGHT control problem** (fly the booster down while the Dragon reaches orbit) | ⭐ ROOT was the stock "only the active vessel gets control input" limit. Solved as designed: our-PRE keeps both loaded+unpacked, the Dragon stays ACTIVE (S2→orbit), the non-active booster is driven via its OWN `OnFlyByWire` (`BoosterControl.DriveNonActive`, its own `AttitudeController`). | **DONE✓ DUAL-FLIGHT CONTROL, Tick-3 flight `Crew-2_20260829_144114`:** Dragon → orbit **200×197.7, inc 51.64°** (SECO 14:50:07) **AND** the non-active booster's own OnFlyByWire flew the full FSM — **16,139 callback calls**, EntryBurn→LandingBurn, grid fins, engine mode AllEngines→ThreeLanding switched **exactly once** (the FlightDriver:222 one-ignition guard proven), attitude loop live (att.err 105°→6°). The stock limit is beaten. Commits `0610609`/`5394358`. **Remaining piece split to H1b.** | — |
| H1b | **Booster octaweb won't LIGHT on the non-active vessel — ROOT = OFF-FOCUS RF IGNITION, *not* ullage** (MEASURED, flight 161224). Booster CSV: `ullage_stab` **~1.0 (SETTLED)** the whole entry burn, `throttle=1`, `engine_mode=3` (Activated), fuel present (**KER: 3,797 m/s Δv, 14.66 TWR available**) — yet `eng_ignited=0`, `thrust_n=0`, and RF logs NO rejection either. So it is NOT unsettled ullage (§8.6 **root B**, not A). | A non-active vessel's `ModuleEnginesRF` does not combust from `Activate()`+`s.mainThrottle` even with settled ullage — the RF ignition path appears gated on active-focus. | **OPEN — plan PIVOTS to root B:** research the RealFuels source (does it gate ignition/thrust on `vessel.isActiveVessel`?); likely a focus strategy, not a settle. ⛔ the settle-then-light plan is DISPROVEN — don't build it. | — |
| H1c | **Booster cold-gas RCS nearly EMPTY (RCS Δv ~1 m/s → 0, KER) → can't hold attitude → tumbles** (att_err 40–144°, roll/pitch swinging). The Step-2 "control reaches it" win degrades within seconds once the RCS runs dry; no engine gimbal (H1b) = no fallback authority. | The separated booster carries almost no cold-gas propellant; attitude authority really depends on the (unlit) engine gimbal. | **OPEN (couples to H1b).** More booster RCS propellant and/or the engine gimbal (needs H1b). Now recorded per-tick (R1 `att_err`/`ctrl_tq`). | — |
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

## ⭐ 2026-08-29 — DRAGONSCREEN UI AUDIT (flight 144114 full screen tour) — see `docs/FLIGHT_144114_SCREEN_AUDIT.md`
> Chris captured every page + button; audited each screen, **source-checked every finding** (several first-glance
> "bugs" were intentional — logged as ruled-out in the audit doc so they aren't re-raised).
| # | Issue | Root / evidence | Status |
|---|---|---|---|
| U1 | **Phase classifier reads `PHASING` while still SUB-ORBITAL on the S2 insertion burn** (pe −4,600→−2,000 km, T+5–8). Contradicts the "AUTO: Ascent to orbit" label + SECO-pending checklist on the same screen. | `VesselData.cs:77` `Mission.Classify(mi)` keyed on situation(Regime)+target presence, NO orbit-closed gate → "in space + has target" ⇒ Phasing mid-ascent. | **FIXED⚑ (commit `98cf500`, Tick-1 green, installed, UNFLOWN)** — added `MissionInputs.OrbitClosed` (pe>atmo); outbound Phasing/Approach gated behind it, else ASCENT. Display-only. |
| U2 | **`STATE→CAUTION` during nominal late ascent** from a PROPELLANT low-alarm — the gauge (by-design) shows the near-spent S2 (~16%) near SECO → `Alarms.Low` → whole-vehicle CAUTION, while the RETURN propellant (MMH/NTO) is full. | 144921; `Pages.cs:1082`. | **OPEN (Low-Med).** Suppress low-prop alarm while the lit stage is an ascent stage, or alarm on the return budget. |
| U3 | **`NET PWR 1` & `NET PWR 2` both read exactly `0 W`** on OVERVIEW (comment expects negative on battery). | 144921; `Pages.cs:974` `CabinEnvironment`. | **OPEN (Low) — VERIFY** the sim output in this state. |
| U-F1 | **"Overheat!" part warning at max-Q ascent** — a part hits ~85–90% of its skin limit on the climb. | **IDENTIFIED (R4, flight 155116):** THERMAL log = **'Falcon 9/Heavy First Stage Tank' skin 501/588 K (85%)**; `skin_temp_frac` max 0.90. Booster grid fins ("T-222 Nemesis") also heat to 312/873 K on entry. | **OPEN (Low-Med)** — 85–90% is close but not failing; monitor / add margin if it climbs. Now instrumented (R4). |
| — | **Ruled out (intentional/misread, do NOT re-raise):** "SURPRESS FIRE" button (matches model art, `PanelMap.cs:19`); MECH "NEGATIVE" unit is g not deg/s (misread); POWER STRINGS "Ax Bx Cx" (intended status format); PROPELLANT 16% (intended lit-engine readout, captioned); PERIGEE blank when suborbital (`PerigeeMeaningful`); inclination 53.6° (osculating suborbital arc). | audit doc | n/a |

## ⭐ 2026-08-29 — RECORDER HOLES (found by cross-checking the screens/KSP.log vs the CSV, flight 144114)
> Things visible in the screenshots or the log that the FlightRecorder CSV did NOT capture. **Instrument class.**
> Each verified against `pure/FlightRecorder.cs` (89-col schema) + `FlightLog.cs`. These block clean diagnosis of
> the open campaigns (esp. H1b booster ullage/ignition and C2a rendezvous fuel-waste).
> **STATUS: R1–R4 ✅ DONE✓ FLIGHT-VALIDATED (Tick-3, flight 155116/155356/161224, 2026-08-29 ~16:00).**
> R1 ✓ booster CSV created (`Crew-2_Probe_*.csv`, 113 cols) with `ullage_stab`/`eng_ignited`/`skin_temp`/`boost_*`.
> R2 ✓ `fdir_fault`/`recovery`/`abort`/`abort_mode` all populated (distinct faults: NoControlSolution, ResourceCritical;
> responses Continue/Downmode/Abort/SafeMode). R3 ✓ `mmh_frac` 1.0→0.0, `nto_frac` 1.0→1e-6 (the drain recorded).
> R4 ✓ `skin_temp_frac` max 0.90 + the THERMAL log named the part (First Stage Tank 85%). R5 deferred (low).
> **The instrument work paid off immediately — it answered the Step-3 gate below.**
| # | Hole | Evidence | Fix (Instrument class) |
|---|---|---|---|
| **R1** | **The non-active booster gets NO CSV at all.** The recorder samples only the ACTIVE vessel; during C2 Step-2 the booster is non-active so nothing fills the `boost_*`/control/`eng_ignited`/`ullage_stab` columns — its whole recovery (16,139 ticks) lives only in sparse ~2 s KSP.log lines. assess flags `boost` block "idle". | only `Crew-2_144114.csv` (capsule) exists; `BoosterControl.DriveNonActive` deliberately skips `FlightLog.Fill`. | **A SECOND recorder stream for the non-active booster** (its own CSV): alt/vspeed/att_err/ctrl_tq/rates/eng_ignited/**ullage_stab**/throttle/mode/fins/legs per tick. Prereq for diagnosing **H1b**. |
| **R2** | **FDIR fault state is NEVER written to the CSV.** `fdir_fault`/`fdir_recovery`/`fdir_abort`/`abort_mode` columns exist but `PutFdir` (`FlightRecorder.cs:344`) is **never called** — `FlightLog.Sample` calls PutTime/Nav/Base/Gate/Command/Rates/RcsBalance/Authority/Ker/Instrument, NOT PutFdir. So assess reads "no FDIR fault" while KSP.log shows **10+** (NoControlSolution ×3, ResourceCritical→SafeMode). | grep: `PutFdir` defined, 0 call-sites. | **Call `PutFdir` every Sample** with the live `FdirReport` (expose the last report from `FlightDriver.TickFdir`). Dead columns → live. |
| **R3** | **No return-propellant (MMH/NTO) columns.** The RCS drain that ends the mission (MMH 655→0, NTO 509→0) is only visible in the resource panel screenshots; the CSV has no resource-quantity column (only `ullage_stab` fraction + the FDIR `ResourceMargin01` feed, which itself isn't recorded). | schema scan — no mmh/nto/prop_remain/margin col. | **Add `mmh_l`/`nto_l` (or `rcs_prop_frac`) columns** + ideally per-burn Δ. Prereq for diagnosing **C2a** fuel-waste. |
| **R4** | **No thermal instrumentation.** The max-Q "Overheat!" part warning (U-F1) is invisible in the CSV — no part-temperature/thermal-margin column. | schema scan — no temp/thermal/skin col. | **Add `max_skin_temp_frac` + hottest-part id** so a thermal event is diagnosable from the recording. |
| R5 | (minor) **Display-phase (`Mission.Classify`) not recorded** — CSV `mission_phase` is the guidance/mode phase, so display-phase bugs (U1) can't be caught from the CSV. | schema. | low-priority: add a `display_phase` col if useful. |

## RESOLVED-VERIFIED (tick 3) — moved out when proven
- Ascent reaches orbit + inc 51.65 (flight-proven ×2). Everything else above is tick-1/2 or open.
