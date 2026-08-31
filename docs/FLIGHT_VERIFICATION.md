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
| Ascent to orbit (MECO→S2→SECO→Dragon sep; ~200×197 km, ~51.6°) | L3 | FLIGHT-PROVEN | prior audit (archived flights) | `AscentControl`, `FlightDriver`, staging/sep, `MissionConductor` |
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

### DS-ASC-001 / DS-RTLS-dual-001 — ascent + dual-vessel booster recovery (PARTIAL) — 2026-08-31
- **Build/commit:** DLL from working tree at HEAD `e1f5763` + the uncommitted Phase-7 docking rebuild (display-only; no flight-code change). Recorder: `DragonScreen_capture/Crew-2_20260831_094132.csv` (Dragon, 1763 rows, MET 0→181.4 s) + `Crew-2_Probe_20260831_094424.csv` (booster, 158 rows, t 0→40.8 s). Log: `KSP.log` (mission-conductor narrative intact).
- **Config:** RSS/RO Earth, Cape (28.62°N/80.6°W), crewed (4), target ISS. **AUTO BOOSTER RECOVERY = ARMED** (SETTINGS/DISPLAY; "sacrifices the Dragon orbit that flight").
- **Objective:** first post-Phase-2 re-fly (AuthorityManager extraction + `Clamp1` NaN fix + authoritative phase/mode/FDIR) and first in-game look at the rebuilt docking screen.
- **Result — SUCCESSES (verified):**
  - Crew gate `SuitLeakG2` → liftoff clean; octaweb ignition, hold-down/clamp release at full thrust.
  - **PRE (RangeExtender) widened to 600 km; booster + S2 held loaded+unpacked** the whole flight (H1 dual-vessel check passing).
  - **Ascent guidance NOMINAL:** VerticalRise→GravityTurn, **plane LOCKED**, fpa 90°→32° smooth, inclination steered 28.62°→47.4° toward 51.64°, ap climbed smoothly to 120 km. **Max-Q 31.8 kPa. accel g-limited to exactly 3.500 g** (crew limit active). **`fdir_fault`=None, `fdir_abort`=0, no NaN, no abort** for all 1763 rows.
  - **MECO shut-then-decouple collision guard** ("octaweb SHUT; holding decouple until its thrust dies — no ram-into-S2") → clean interstage separation. *(Re-confirms the flight-194334 separation-collision fix.)*
  - **C2 dual-vessel booster recovery engaged:** Dragon stays ACTIVE, booster flown on its own `OnFlyByWire`; both vessels driven simultaneously, sep 0→3 km, both alive → **L4 dual-vessel control re-confirmed post-Phase-2**.
  - **S2 (MVac) ignition + S2 roll-trim RCS** (gimbal-less roll hold) worked.
  - **Booster retrograde flip executed:** att_err 162°→**1.96°** at best (engines correctly unlit while ascending).
  - **Screens:** all pages rendered correctly in IVA incl. the **rebuilt DOCKING page** (reticle-in-rings, no navball; owner verdict "fine for now"). **Zero DragonScreen exceptions** in the log; docking telemetry authoritative + internally consistent (X/Y/Z vector-sum = RANGE; ALIGN drives the sweep). See `DS-DOCKUI-001`.
- **Result — INCOMPLETE / FAILURES / CONCERNS:**
  - **Flight ENDED BY REVERT at MET 181 s** (booster t≈41 s) — deliberate, not a crash. Therefore **NOT proven this flight:** Dragon orbit insertion (no SECO; suborbital ap 118 km), booster entry burn / landing burn / **touchdown** (booster still coasting to apex, `eng_ignited`=0), and everything after (rendezvous/dock/deorbit/entry/splashdown).
  - **Booster attitude hold is underdamped** — the flip reached ~2° but oscillated 20–60° (log). Fidelity concern for entry-burn accuracy; not a fault.
  - **Max-Q ABORT NOT exercised** (`fdir_abort`=0 all rows) → the Phase-2 abort proof is **still not re-closed**.
  - Config note: AUTO BOOSTER RECOVERY "sacrifices the Dragon orbit," so ascent-to-orbit and booster-landing cannot both be proven in one flight of this mode.
- **Net verification impact:** Phase-2 changes re-exercised on the **ascent + separation + dual-vessel** path with **no regression and no NaN** — strong supporting evidence, but **NOT a full re-proof**: (a) ascent-to-orbit (SECO) not reached, (b) **max-Q abort still owed**. Protected rows below stay INVALIDATED for the abort; the ascent/dual-vessel rows are re-confirmed *as flown* (nominal to S2 / controlled, not landed) but a to-orbit flight is still owed to fully restore the ascent L3 proof.

### DS-DOCKUI-001 — rebuilt DOCKING screen, in-game (L2) — 2026-08-31
- Rebuilt `DockingPage` (real-HUD reticle-in-rings, **no navball**) rendered on the live IVA screen through the whole prelaunch/ascent above. **Renders correctly, zero exceptions**, reads authoritative rel-nav from `VesselData` (RANGE / RATE / X-Y-Z / ALIGN / ROLL-PITCH-YAW / Mode). Owner verdict: **"fine for now"** (look accepted).
- **Gap found in-game:** the blue per-axis **rate line reads "—"** — `VesselData` does not publish angular rates yet (RollRate/PitchRate/YawRateText null → honest placeholder, rule E4). Next increment: publish them (rule T3). The green target diamond sits on boresight (magnitude-only `Align01`; 2-D bearing still owed).

_(Older placeholder note removed — these are the first real entries this program.)_
