# Session handoff — 2026-08-25 (rendezvous rebuilt + installed; next step = FLY IT)

Read this first after any compaction. It is the complete state and the exact next step.

## Where we are

The interactive crew-in-the-loop autopilot was built, installed, and FLOWN (flight_0825_081808):
**countdown → launch → ascent → orbit all work**; the rendezvous fired zero burns. The user directed a
full rebuild of the rendezvous to the **real SpaceX/NASA named-burn procedure**, as a 3-step order:
(1) instrument everything, (2) de-risk the burn path, (3) build the real sequence. **ALL THREE ARE NOW
DONE and the build is INSTALLED (2026-08-25).** The next action is the **proving flight** — restart KSP,
fly a Crew-2 launch→rendezvous→dock via AUTO SEQUENCE, then read it back with `assess_flight.py`.

## The rebuilt rendezvous (Step 3 — DONE, installed)

`pure/NamedRendezvous.cs` + `NamedRendezvousOps.cs` fully rewritten; `NamedRendezvousTest.cs` rewritten
(21 checks green). Same public surface, so `StationApproach` / `CrewProcedureOps` are unchanged.
- **CLIMB (pure, phase-angle-triggered Hohmann):** PHASE (circularise the insertion dispersions) → BOOST
  (Hohmann raise to the co-elliptic, 10 km below the station, fired when the phase gap has closed to ≤0.15°
  of the lead) → CLOSE (circularise at apoapsis onto the co-elliptic).
- **TERMINAL (glue, Clohessy-Wiltshire two-impulse, RANGE-triggered, offset-aimed):** DRIFT (warp the slow
  co-elliptic catch-up to ≤20 km behind) → TRANSFER (`CwTargeting` intercept to the real 7.5 km AI point) →
  CO-ELLIPTIC (null the relative velocity = the AI station-keeping hold) → AI (CW intercept to the 2 km
  approach-ellipsoid entry) → MIDCOURSE (CW re-solve at mid-transfer) → hand to the L-approach at ≤2.5 km,
  which flies WP0/WP1/WP2 as crew-GO holds (unchanged).
- **Why this shape (the robustness fix):** phase error amplifies (~118 km/° at 419 km), so ONLY BOOST is
  phase-triggered — aimed to arrive 1.5° BEHIND for margin — and everything past the co-elliptic is measured
  RANGE fed to the already-tested `CwTargeting`, which re-solves from the live relative state and aims at an
  offset point behind the station, never at it. **No 0.6° knife-edge window; no `warpArmed` latch** —
  `WarpToUt()` is re-armable (guarded only on live `TimeWarp.CurrentRateIndex`) and a climb raise fires even
  when a warp nudges the phase slightly PAST the lead (the exact 2026-08-25 zero-burn regression, now a test).
- **Instrumented in the same pass:** `FlightRecorder` gained the `rv_` block (rv_leg, rv_rangeKm, rv_phaseDeg,
  rv_alongKm, rv_radialKm, rv_elevDeg, rv_leadDeg, **rv_gapDeg** = phase-still-to-close-to-the-lead,
  rv_coAltKm, **rv_lastBurn**, **rv_lastDv**, rv_arrRelMps, rv_warp), 13 columns appended at the END.

## Reading the proving flight back

`python build/assess_flight.py "<newest capture>"`. The burns fire iff `rv_lastBurn` cycles
PHASE→BOOST→CLOSE→TRANSFER→CO-ELLIPTIC→AI→MIDCOURSE and, for each, `nd_deliveredDv` approaches `nd_initDv`
(planned vs delivered — the burn-path proof). `rv_gapDeg` should march to ~0 before BOOST (frozen-far-from-0
would be a no-fire bug); `rv_along`/`rv_radial` should shrink through the terminal legs; `rv_warp` should
not stick. Then between-flight, suggest the next fidelity step (never fly blind).

## The flown flight — what the data showed (flight_0825_081808.csv)

- ✅ Gates, countdown, launch, ascent all worked. Reached orbit (pe 198.5 km).
- ❌ Rendezvous fired **zero burns** — orbit frozen at ap 217 / pe 199 km the whole time; range only
  drifted (13363↔216 km). `NamedRendezvous` (co-elliptic NC→NSR→Ti) engaged (`m_rndz=Phasing`) but NC
  never triggered, so it never climbed toward the ISS or reached the L-approach.
- Booster over-burned the entry burn → landed dry, 393 m off deck (separate known landing issue).

## The no-burn ROOT CAUSE (from the CODE, not a guess)

`pure/NamedRendezvous.cs` fires NC only when the phase angle lands in a **0.6°-wide window** of a computed
lead angle, and getting there relies on a **single warp-to-lead** in `NamedRendezvousOps.cs` that
(a) bails if any timewarp is already active (`CurrentRateIndex != 0`), and (b) latches `warpArmed` and
never resets it except on a burn — so one overshoot of the 0.6° window means it must close a full
synodic period (~30 h) in 1×, which never happens. A knife-edge trigger with a one-shot escape hatch.

## Mission geometry (grounded from saves/test/persistent.sfs)

- ISS "ISS USOS Real Size": **SMA 6,790,000 m = 419 km circular, INC 51.64°, ECC ~0.0003**, body = Earth
  (REF=1, R 6,371,000). Earth μ = **3.986004418e14**.
- Chaser insertion ~208 km circular, 51.6°. Real Crew-2 geometry.

## Step 1 — DONE: instrumented all sequences (build green, NOT yet installed)

Added to `FlightRecorder.cs` (appended at the END so no column shifted; width self-checks in-game):
- **`nd_`** (NodeExecutor / the burn): `nd_phase, nd_initDv, nd_remainDv, nd_deliveredDv, nd_pointErrDeg,
  nd_throttle, nd_rcs, nd_tIgnS`. **planned vs delivered Δv is the burn-path proof.**
- **`g_`** (crew gates): `g_gate, g_gatePhase, g_actionNeeded, g_returnArmed, g_releasedHold`.
- **`ab_`** (abort): `ab_lesArmed, ab_aborting, ab_mode`.
- **`ls_`** (TAC life support, Dragon side): `ls_present, ls_o2Frac, ls_co2Frac, ls_o2Days, ls_foodDays,
  ls_waterDays` (via new `LifeSupportBridge.Sample`, one part-walk; days clamped to 9999 for no-crew).
- Exposed on `NodeExecutor`: `DeliveredDvMps, RcsBurn, TimeToIgnitionS`.
- **The rendezvous `rv_` block is NOT added yet — add it WITH the Step-3 rebuild** (instrument-everything
  rule: instrument in the same pass you build).

## Step 2 — DONE: burn path de-risked (real evidence)

`flight_0824_004018.csv`: a `NodeExecutor` burn delivered **387.3 → 5.4 m/s** (ran to completion). The
executor mechanism works. The rendezvous failure is the TRIGGER, not the burn. The RCS burn path
(`AccountByVelocity`) measures actually-delivered Δv and only pushes on-axis — sound by construction.

## Step 3 — DONE (see "The rebuilt rendezvous" at the top). The original spec, for the record:

**User requirement: FULL fidelity — reproduce what real SpaceX/NASA do for Crew Dragon.** The real
sequence (Crew-4 timeline, `docs/REAL_CREW_DRAGON_MISSION.md`):
**Phase → Boost → Close (co-elliptic) → Transfer → Co-elliptic → Approach Initiation (AI, 7.5 km behind+
below) → Approach Midcourse** → hand to the R-bar/V-bar L-approach (WP0 400 m / WP1 220 m / WP2 20 m,
each a crew-GO hold — ALREADY BUILT & WIRED in `WaypointApproachOps`, do not rebuild).

Rewrite `pure/NamedRendezvous.cs` + `NamedRendezvousOps.cs`:
- The full named sequence, each burn a named event, tuned to 208 km → ISS 419 km at 51.6°.
- **Robust triggers** — NOT a 0.6° window + one-shot warp. Plan one burn, execute to completion (via
  `NodeExecutor`, `useRcs:true` — Draco), re-decide. Warp must be re-armable and must not bail forever.
- Burns built with `pure/Hohmann.cs` primitives (`RaiseOppositeApsisDv`, `CirculariseDv`, `TransferSma`,
  `SpeedAt`, `ApsisBurnDv`) + `pure/Kepler.cs` / `pure/Lvlh.cs`. Use `Desktop/mechjeb_src` as the
  reference algorithm (port, don't invent).
- **Add `rv_` instrumentation** in the same pass: leg, phase angle, lead angle, gap-to-lead, co-elliptic
  target radius, elevation angle, each named burn's planned+delivered Δv, warp state, note.
- Headless tests per burn in `plugin/test/NamedRendezvousTest.cs` (already registered in TestMain).
- Then `python build.py install` (needs KSP+CKAN closed) and fly ONE fully-recorded flight.

`StationApproach` is just a wrapper that delegates to `NamedRendezvousOps` (do not rebuild it). The gates
(`CrewProcedureOps`), L-approach holds, ascent, booster, return are all done and working — leave them.

## RULES RE-AFFIRMED THIS SESSION (all in memory)

- **NO Python simulations — BANNED** ([[no-python-simulations]]). Deleted `docs/rendezvous_sim/`. Validate
  with headless C# tests + the recorded corpus (`assess_flight.py`). Don't cite sim results.
- **INSTRUMENT + RECORD EVERYTHING** ([[instrument-everything]]) — every controller's internals, same pass.
- **ALWAYS FULL FIDELITY, never ask full-vs-safe** ([[crew2-full-fidelity-no-deviation]]).
- **Between test flights, ALWAYS suggest the proper next step** (de-risk/instrument/verify), never fly blind.
- Don't dress a self-adopted habit up as an established rule; if a practice keeps failing, flag it and ask.

## Tooling / config fixed this session

- `plugin/build/assess_flight.py` — the physics self-check was hard-coded to KERBIN gravity and false-
  alarmed on Earth flights; now `detect_body()` picks Earth vs Kerbin from `a_orbSpeed`. Verified.
- User settings `~/.claude/settings.json` — added a safe read-only allowlist (git status/diff/log/show,
  `build.py test`/`preview`, `assess_flight.py`, `ls/head/tail/awk`); NOT `install`, git push/reset, rm.

## Build / assess commands

```
cd C:\Users\User\Desktop\DragonScreen\plugin
python build.py test        # build DLL + headless tests (no KSP)
python build.py install     # build+test then copy to GameData (KSP + CKAN must be CLOSED; full restart)
python build/assess_flight.py "<capture.csv>"   # or no arg for newest
```
Captures: `C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\DragonScreen_capture\`.

## State of the tree

All this session's work (crew-ops autopilot + TAC LS + instrumentation + the rendezvous rebuild) is
**UNCOMMITTED** in `Desktop\DragonScreen` (master). Build is green and **INSTALLED** to the game
(2026-08-25) — KSP needs a FULL RESTART to load the new DLL. The next flight is therefore one clean,
fully-recorded run of the whole rebuilt stack. Do NOT commit or push unless the user asks.

Full context also in memory: [[dragonscreen-rendezvous-rebuild]], [[dragonscreen-crew-autopilot-direction]],
[[dragonscreen-tac-life-support]], [[instrument-everything]], [[no-python-simulations]].
