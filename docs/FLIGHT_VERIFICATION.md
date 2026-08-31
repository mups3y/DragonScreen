# FLIGHT VERIFICATION — evidence log (ACTIVE)

> The record of what has actually been verified, at which level, with evidence. Governed by `MASTER_BUILD_SPEC.md`. A capability is only as proven as its highest **flight** evidence — headless green is L1, never flight proof (rule V1). Every significant flight gets an ID and a row here.

## Verification levels
L1 software/math (pure tests) · L2 KSP integration · L3 single-vessel flight · L4 multi-vessel flight · L5 end-to-end mission.

## Flight-ID scheme
`DS-<CAMPAIGN>-<NNN>` — campaigns: `ASC` ascent · `ABORT` launch escape · `RTLS`/`ASDS` booster · `RV` rendezvous · `DOCK` docking · `RET` return · `E2E` full mission · `DOCKUI` docking-screen UI. Record: build/commit, config, mods, objective, start conditions, result, failure point, suspected root cause, evidence (recorder path).

## Baseline
| Date | Commit | Item | Result |
|---|---|---|---|
| 2026-08-31 | `9073429` | `python plugin/build.py test` (headless L1) | **PASS**, exit 0. 26 unit suites (~515 checks) + Tier-2 dispersion 731,239 checks (100k property cases: control/rendezvous/docking/return/fdir). 0 failed. |

## Protected flight-proof registry (invalidated by touching the listed code, per rule V4)
| Capability | Level | Status | Evidence | Invalidated by changes to |
|---|---|---|---|---|
| ~~Ascent to orbit~~ **ASCENT — S1/MECO OK; S2 phase BROKEN (upper-stage attitude divergence)** | L3→**regressed** | ⛔ **NOT PROVEN post-Phase-2** (was FLIGHT-PROVEN in a *prior* audit before the Phase-2 change) | `DS-ASC-001`+`DS-ASC-002` (2026-08-31): S2 att_err→125–154°, tumble, no SECO | `AscentControl`, `FlightDriver`, staging/sep, `MissionConductor` — **investigate the Phase-2 change first** |
| Max-Q launch escape (attitude authority, geometric RCS fallback, chutes, splashdown, crew survival) | L3 | FLIGHT-PROVEN | prior audit | `AbortControl`, `FlightDriver`, abort latch, RCS authority |
| Dual-vessel booster control (Dragon active + non-active booster on own `OnFlyByWire`) | L4 | PARTIALLY PROVEN (controlled, not landed) | prior audit (~16k OnFlyByWire callbacks) | `MissionConductor`, `BoosterControl`, `RangeExtender`, `FlightDriver` |

> **Re-fly requirement (ACTIVE — the three rows above are currently INVALIDATED, rule V4):** Phase-2 landed as CODE — UNFLIGHTED on 2026-08-31 (commit pending): the AuthorityManager extraction + the `Clamp1`/`Clamp1f`/`Clamp01f` NaN fix touch `FlightDriver` and `BoosterControl`. The extraction is **behavior-preserving by construction** (the proven `OnFlyByWire` actuation path is unchanged; only additive read-only authority publishing + NaN guards were added), so the flights are expected to reproduce the prior results — but they are not re-proven until flown.
>
> **Gate before Phase 3 — status 2026-08-31 (PARTIAL, see flight log `DS-ASC-001`):**
> - `DS-RTLS/dual` — **re-confirmed** (controlled, not landed): Dragon active + non-active booster on its own `OnFlyByWire`, both held loaded/unpacked, no authority loss. ✓ *as flown*
> - `DS-ASC` (ascent to orbit, ~200×197 km / ~51.6°) — **PARTIAL:** ascent nominal & fault-free through S2 ignition, but the flight was **reverted mid-S2 (no SECO)**, so orbit insertion is **not re-proven**. A to-orbit ascent is still owed to fully restore the ascent L3 proof.
> - `DS-ABORT` (max-Q launch escape → chutes → splashdown, crew survive) — **STILL OWED** (not exercised; `fdir_abort`=0). The abort proof remains INVALIDATED.
>
> Install with `python plugin/build.py install` (close KSP **and CKAN** first; a DLL change needs a full game restart).

## Open (no flight proof yet)
Rendezvous (reaches region, no dock) · Docking (no end-to-end) · Return: deorbit→entry→splashdown (**0 autonomous returns to date**) · Booster RTLS landing · Booster ASDS landing · Booster recovery ignition reliability · RCS-depletion handling · RSS entry targeting · water-site scan.

## Return-history reconciliation (Phase 1 — DONE)
User signal "12 killed / 17 rescued by hand / 0 autonomously returned" is now **CORROBORATED by two independent repo sources**:
- `RETURN_FIX_PLAN.md` §Why (doc removed 2026-08-31; preserved in git history) — the KSP-log ledger from Chris's 2026-08-30 return-test session: **12 killed (respawned), 17 rescued by hand, 0 brought home by the autopilot.**
- `dashboard/klm_data.json` — partial audit (since 2026-08-28, 10 flights via `dashboard/audit_kerbals.py`): `home: 0`, `rescued: 0`, `died: 8`, `stranded: 8`, `returning: 16`. Independent confirmation of **0 autonomous returns**.

**Firm fact:** RETURN = OPEN, **0 autonomous returns to date** (two sources). Exact kill/rescue tallies vary by accounting window (KSP-log session ledger vs recorder-CSV scoreboard); the operative signal — the autopilot cannot yet bring the crew home — is confirmed. Return is Phase 14; not touched this session.

## Flight log

### ⛔ DS-ASC-001 + DS-ASC-002 — ascent FAILS at S2 (upper-stage attitude divergence) — 2026-08-31
> **CRITICAL, REPRODUCED IN BOTH POST-PHASE-2 RE-FLIES. This supersedes an earlier mis-assessment that called the ascent "nominal" — it is not.** First-stage ascent to MECO is clean, but **at S2 (MVac) ignition the upper-stage attitude control diverges and the S2+Dragon tumbles in pitch/yaw**, the S2 thrust is misdirected, apoapsis stalls and the vehicle **never reaches orbit**. Ascent-to-orbit is currently **BROKEN**.

- **Flights / recorders:**
  - `DS-ASC-001` — `Crew-2_20260831_094132.csv` (1763 rows) + booster `Crew-2_Probe_20260831_094424.csv`. DLL at HEAD `e1f5763` + uncommitted docking rebuild.
  - `DS-ASC-002` — `Crew-2_20260831_102133.csv` (1688 rows) + booster `Crew-2_Probe_20260831_102425.csv`. DLL = commit `f79402a` (docking rebuild + angular-rates publish; **no flight-code change vs 001**).
  - Config both: RSS/RO Earth, Cape, crewed (4), target ISS, **AUTO BOOSTER RECOVERY armed**. **Zero DragonScreen exceptions** either flight.
- **THE FAILURE (evidence — recorder `att_err_deg`, `rate_pitch/yaw_dps`, matched frame-by-frame to the separation screenshots):**
  - Ascent to MECO **and through the coast**: `att_err` ≈ 0–2°, body rates <1°/s, roll ~0° — **rock-solid** (S1 control path is fine; `102417`/`102426`/`102427` show Roll ≈0.0° at MECO).
  - **ONSET IS AT S2 IGNITION, NOT SEPARATION.** MECO (met 140) + decouple are clean and controlled; the vehicle coasts controlled (met 141–142, att_err 2°). **The instant the MVac lights (met 142–143) a pitch rate of ~−12°/s appears and is never arrested** — att_err ramps nearly linearly 3°→62° over ~5 s (`102429`/`102430`: HDG 043°→023°, Pitch 29°→55°, Roll 0°→−17° right as thrust comes on), then becomes a **divergent limit-cycle**: att_err oscillates ~24°↔124°, pitch rate ±40–68°/s, yaw rate building to −86°/s, HDG swinging wildly (043→003→314→019→107→231→143°) to the revert (met 163). **Roll stays ~0–2° (the roll-trim RCS holds it); the runaway is in pitch/yaw.**
  - **The BOOSTER diverges at the SAME TIME** (att_err 2°→141° over t0–15 s, engines correctly unlit). Both the active Dragon/S2 and the non-active booster lose attitude control simultaneously at separation/ignition → **points at a shared attitude-control-path cause, not an S2-gimbal-specific one.**
  - **Apoapsis frozen at ~120 km, velocity bleeding off** (S2 thrust misdirected); **perigee stays ≈ −6168 km (suborbital)** → **no SECO, no orbit.** User reverted at met 163 because it was visibly failing.
  - **FDIR did NOT detect it:** `fdir_fault`=None / `fdir_abort`=0 every row despite 100°+ error → fault spine **blind to attitude divergence**.
  - **Screens FUNCTIONED CORRECTLY throughout** (see `DS-DOCKUI`): all pages matched KER; the DOCKING angular-rates fix showed live values. The screens are NOT implicated in the failure.
- **The reverts were forced by this failure, not chosen** — both flights were reverted mid-S2 once the tumble made orbit impossible. Earlier note "ended by revert, deliberate, not a crash" was wrong.
- **✅ ROOT CAUSE — CONFIRMED headlessly (2026-08-31), evidence chain below. It is a control-authority OVER-READ of ~137×, no disturbance.** (This corrects two earlier wrong turns in this same investigation: it is NOT Phase-2/`Clamp1`, and it is NOT the ~5× over-read I first sized — the real authority is far lower than I assumed.)
  - **Evidence 1 — flight regression (pure data, no model).** Regressing net torque (`MOI·Δω/Δt`) against commanded actuation over the S2 window gives, per axis: **slope K (real per-unit control torque) ≈ 451 (pitch) / 406 (yaw)**, and **intercept (disturbance torque) ≈ 0**. So (a) there is **NO external disturbance**, and (b) the **real** S2 pitch/yaw control authority is only **~451** — i.e. the gimbal alone; the RCS (loop estimate ~62,000) delivers **≈0 effective torque** despite its geometric estimate. (So the recorder's ~480 gimbal reading is the REAL authority, not an under-report — my earlier "artifact" note used an inconsistent unit comparison and is retracted.)
  - **Evidence 2 — the loop over-reads by ~137×.** The loop uses `controlTorque ≈ 62,000` (dominated by the RCS geometric estimate) while the real is ~451 → `maxAlpha = ct/MOI` is ~137× too high (S2 ≈ 37 vs a real ≈ 0.27 rad/s²). Origin: `c42ed20` (Campaign-6) credits the RCS geometric authority whenever the RCS master is ON; `8225df7` (roll-trim + ullage) arms RCS in S2 — breaking the `AttitudeController.ControlTorque` L195-198 assumption *"during gimbal-only ascent RCS is off."* First flown 2026-08-31.
  - **Evidence 3 — headless sim reproduces the tumble, no disturbance (`AttitudeLoopTest`, faithful port of `AttitudeLoop.Axis`+`Pid2`).** With MOI 1650, real authority 451, estimate 62,000, **D=0**: the loop **LIMIT-CYCLES** (matches the flight: starts small → grows → swings past 200°; commanded rate peaks ~7.4 rad/s vs the flight's ~7.6). It is a **threshold instability**: converges for over-reads ≲4×, limit-cycles past ~10× (the flight is 137×). A correct estimate (est = real) **CONVERGES** = fix preview. This is why my earlier stand-in (real≈11846, only 5× over-read) wrongly looked stable.
  - **Mechanism:** the bang-bang loop sizes its rate command off the inflated `maxAlpha`, commanding rates ~15× what the tiny real authority can achieve; it overshoots the target and swings back → divergent limit cycle. Same loop on both vessels → both tumble. Roll stays put because the roll-trim owns it.
  - **THE FIX (root cause; still to implement + review + re-fly, rule V4):** make the loop's `controlTorque` reflect the **achievable** authority during powered gimbal ascent — do NOT credit the RCS geometric estimate for pitch/yaw when the main engine is thrusting (RCS is at best a roll trim there). The sim confirms this converges. Pair it with a **`maxAlpha`/`targetOmega` robustness clamp** (defense-in-depth: the loop must never command a physically-absurd rate from any authority estimate). Touches `AttitudeController.ControlTorque` / `AttitudeLoop` — used by the proven max-Q ascent + abort + booster, so it invalidates those proofs until re-flown.
  - **INVESTIGATION TRAIL (kept so the dead ends are not re-walked):** ruled out — `Clamp1` NaN (commands finite); a disturbance torque (regression intercept ≈ 0); a ~5× over-read (stable — I'd wrongly sized the real authority at the S1-gimbal ~11846); B4 lead-comp vs an instant RCS (converges). The gimbal is fine (`gimbalRange=2°`, active); the low ~451 IS its real authority, not an artifact. What actually holds up: the ~137× authority over-read above.
- **What DID work (verified):** liftoff/clamp release; **PRE holds booster + S2 loaded/unpacked** (dual-vessel physics); ascent-to-MECO guidance (plane-locked, max-Q 31.8 kPa, g-capped 3.500); **MECO shut-then-decouple collision guard** → collision-free interstage separation; **C2 dual-vessel booster recovery** (Dragon active + booster on own `OnFlyByWire`, both alive to 3 km) → L4 control still functions; booster retrograde flip reached ~2° (but **underdamped**, oscillating 20–60°, and never lit its entry burn / not landed — reverted while still ascending). Screens rendered correctly incl. DOCKING (`DS-DOCKUI-001`).
- **Net verification impact:** **ascent-to-orbit is BROKEN (S2 tumble), reproduced twice — TOP blocker.** The Phase-2 re-fly did NOT clear the ascent proof; it **revealed a regression**. Protected "Ascent to orbit" row must be downgraded until root-caused and re-flown to SECO/orbit. Max-Q abort still un-exercised. Dual-vessel *control* re-confirmed (not landed).

### DS-DOCKUI-001/002 — DOCKING screen + all pages, in-game (L2) — 2026-08-31
- **All screen pages FUNCTION CORRECTLY** (verified across `DS-ASC-002`'s 75 in-flight screenshots, cross-checked to KerbalEngineer in-frame): FLIGHT (phase/abort-mode/gauges — abort mode escalates PAD→MODE 1-LOW ALT→MODE 2 correctly), VEHICLE OVERVIEW+MECH (ECLSS/net-power sign/accel decomposition), NAV GROUND-TRACK + 3D-PLANET (**perigee/time-to-pe correctly dashed on the suborbital arc** — the OrbitReadout guard works), DOCKING, SETTINGS CABIN/AUDIO/VIDEO(live interstage cam)/DISPLAY. Altitude/velocity/apogee/inclination match KER at every sampled point. **Zero DragonScreen exceptions.**
- **Rebuilt DOCKING** (real-HUD reticle-in-rings, **no navball**): owner verdict **"fine for now"**. Reads authoritative rel-nav; RANGE = KER target distance to the metre; X/Y/Z vector-sum = RANGE.
- **✅ Angular-rates fix CONFIRMED in-game (`DS-ASC-002`, commit `f79402a`):** the blue per-axis rate line now reads real values (e.g. `0.0 / 0.5 / 0.0 deg/s`) instead of "—".
- **Screen GAPS found:**
  - **No screen surfaced the S2 attitude-control failure.** Through the 100°+ tumble the chrome **STATE stayed NOMINAL** (FDIR blind, `fdir_fault`=None) and no page shows a GNC/attitude-error or guidance-tracking health readout. A catastrophic control divergence produced **zero crew alert**. (Ties to the FDIR finding in the DS-ASC entry; argues for a GNC/attitude-health element + FDIR attitude-divergence detection.)
  - Minor: FLIGHT reads PHASE **ASCENT while still clamped/​warping on the pad** pre-liftoff (AUTO arms the phase early). Phase-labeling nuance.
  - Still owed (known): target diamond on boresight (2-D bearing not yet published).

_(Older placeholder note removed — these are the first real entries this program.)_
