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
> **Gate before Phase 3:** re-fly and log here — `DS-ASC-###` (ascent to orbit, confirm ~200×197 km / ~51.6°), `DS-ABORT-###` (max-Q launch escape → chutes → splashdown, crew survive), `DS-RTLS/dual-###` (Dragon + non-active booster controlled simultaneously). Install with `python plugin/build.py install` (close KSP **and CKAN** first; a DLL change needs a full game restart).

## Open (no flight proof yet)
Rendezvous (reaches region, no dock) · Docking (no end-to-end) · Return: deorbit→entry→splashdown (**0 autonomous returns to date**) · Booster RTLS landing · Booster ASDS landing · Booster recovery ignition reliability · RCS-depletion handling · RSS entry targeting · water-site scan.

## Return-history reconciliation (Phase 1 — DONE)
User signal "12 killed / 17 rescued by hand / 0 autonomously returned" is now **CORROBORATED by two independent repo sources**:
- `docs/archive/superseded/RETURN_FIX_PLAN.md` §Why — the KSP-log ledger from Chris's 2026-08-30 return-test session: **12 killed (respawned), 17 rescued by hand, 0 brought home by the autopilot.**
- `dashboard/klm_data.json` — partial audit (since 2026-08-28, 10 flights via `dashboard/audit_kerbals.py`): `home: 0`, `rescued: 0`, `died: 8`, `stranded: 8`, `returning: 16`. Independent confirmation of **0 autonomous returns**.

**Firm fact:** RETURN = OPEN, **0 autonomous returns to date** (two sources). Exact kill/rescue tallies vary by accounting window (KSP-log session ledger vs recorder-CSV scoreboard); the operative signal — the autopilot cannot yet bring the crew home — is confirmed. Return is Phase 14; not touched this session.

## Flight log
_(none yet this program — first entries will be the Phase-2 regression re-flies: `DS-ASC-###`, `DS-ABORT-###`, `DS-RTLS-###`/dual-vessel, then `DS-DOCKUI-001`.)_
