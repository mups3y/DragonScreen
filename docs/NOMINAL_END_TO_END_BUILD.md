# NOMINAL END-TO-END BUILD — the audit + the backlog to fly a perfect crew mission

> ⭐ **MANDATE (Chris 2026-08-29):** *"teach the autopilot the entire sequence map + the parameters for perfect
> nominal Dragon crew missions end to end, INCLUDING booster recovery. No guessing, no shortcuts, full
> understanding first."* This doc is the result of a full read of the ENTIRE flight-path code against the
> `SEQUENCE_MAP.md §1A` nominal envelope. It records, per phase: the **envelope target**, the **current code
> state** (verified by reading the source, not the .md docs), the **gap**, the **build action**, the **test**,
> and whether it is **build-gated** (I can build+headless-test now) or **flight-gated** (needs a flight to
> verify/tune — Chris's part; nothing is FACT until tick 3, [[three-tick-system]]).

## 0. THE HEADLINE FINDING — the sequence + parameters are ALREADY BUILT

The autopilot **already knows the sequence and flies every phase**. The conductor stack is complete and wired
end-to-end:

- **`pure/ModeManager.Plan`** encodes the full nominal sequence: countdown G1–G7 → Ascent → (Phasing → G9 →
  Approach×3 + G10/11/12 → Docked → G13 → Docked → G14 undock → Departure-Phasing) → G15 deorbit → Entry →
  Drogues. Free-flyer omits the rendezvous block.
- **`CrewProcedureOps`** walks it, satisfies each gate, and has **state-aware resume** (`ResumeIndex`: pad→
  countdown, ascending→ascent, orbit+targeted→rendezvous, docked→departure, orbit-otherwise→deorbit,
  entering→ride-it-down) — so AUTO SEQUENCE in orbit never re-launches.
- **`FlightDriver.DriveActivePhase`** dispatches each phase to its controller; **`MissionConductor`** adds
  never-overshoot auto-warp through the coasts + the booster focus hand-off.
- The **parameters already match §1A**: contact 0.08 m/s (∈ IDSS 0.05–0.10), S1 g 3.5 / S2 g 4.1, KOS 200 m,
  WP0 −400 / WP1 220 / WP2 20 m, AI hand-off 7.5 km, drogue 5486 m / main 1830 m, deorbit target Pe 50 km,
  trim L/D via CoM shifter, RCS signs −1 (MechJeb-derived), pe-floor 150 km, ullage-settle before every light.

**So "teach it the sequence + params" is substantially DONE.** What remains for *perfect* end-to-end completion
is a set of **behavioral gaps** + **flight verification** of the built-but-unproven phases. That is this backlog.

---

## 1. PER-PHASE AUDIT (envelope → code → gap → action)

Legend: **✅** built + matches envelope · **◑** built, refinement/fidelity gap · **⚠** built, FLIGHT-gated
(sign/tune to confirm) · **⛔** genuine build gap (not yet built).

### Countdown  — `CrewProcedureOps` + `CrewGates` + `FlightDriver` launch path
- ✅ Gates G1–G7 with the real MET spacing; LES-arm gate; ignite T-3 → spool clamped → release ≥99% thrust →
  pad safe-abort on a failed light. Ullage gate. **Matches §1A.**
- ◑ `AutoAdvanceGates = true` (hands-off test convenience) — set false to restore interactive crew GOs. Not a
  bug; a mode. No action needed for nominal.

### Ascent (pad → MECO → S2 → SECO → Dragon sep)  — `AscentControl` + `pure/Ascent` + `pure/Upfg`
- ✅ Zero-AoA gravity turn (q·α-moderated cap, floored so authority is never lost), max-Q bucket, S1 3.5 g /
  S2 4.1 g caps, UPFG closed-loop to a **circular target on the ISS plane normal**, SECO on v-go, cut-then-sep.
  **This phase is PROVEN (reaches orbit).** Matches §1A.
- ⚠ g still peaked ~4.5–4.65 in some flights (limiter lag) → `S2GLimitG` is the tune knob (F5). Flight-gated.

### Booster recovery (concurrent)  — `BoosterControl` + `MissionConductor` + `pure/BoosterDescent/Hoverslam/GridFin`
- ✅ The *descent logic* is built + correct: flip retrograde on cold-gas → entry burn on **ThreeLanding (3
  engines)** → grid-fin aero steer (aim-to-miss until nominal) → **hoverslam on CenterOnly (1 engine, one
  ignition, no mid-burn 3→1)** → legs → touchdown. Engine modes selected absolutely-while-off. Matches §1A.
- ⛔ **THE BIG GAP — DUAL FLIGHT.** `MissionConductor.AutoRecoverBooster` is OFF, and when ON it does
  `FlightGlobals.ForceSetActiveVessel(booster)` — which **sacrifices the Dragon to stock on-rails**. So booster
  recovery and Dragon-to-orbit are **mutually exclusive today.** The intended fix ("our PRE" via `VesselRanges`
  to keep both loaded + fly the non-active booster via `OnFlyByWire`) **rests on an UNVERIFIED KSP behavior** —
  see §2. This is the #1 named build gap and it is feasibility-gated.

### Rendezvous / phasing  — `RendezvousControl` + `pure/Phasing/Hohmann/Cw/Rendezvous`
- ✅ Far field = phase-timed **Hohmann prograde-only raise** to a co-elliptic ~10 km below (can't deorbit) +
  auto-warp; near field = **CW two-impulse** to offset aims, **pe-floor gated**, hands off at AI 7.5 km. Offset
  targeting + passive-abort safety. Matches the *physics* of §1A.
- ◑ **Fidelity:** the real **named-burn sequence** (Phase/Boost/Close/Transfer/Coelliptic/AI/Midcourse) is
  collapsed into a far-FSM + CW. The outcome is equivalent; strict-fidelity would model the discrete named
  burns. Low priority (physics-equivalent).
- ⚠ **F3 — did not converge to dock** last flight (reached the 100 km hand-off, ptErr 113°). Likely the F1
  attitude root (fixed, unflown). Flight-gated: re-fly, then tune the CW terminal.

### Proximity ops / docking  — `DockingControl` + `pure/DockControl/DockApproach`
- ✅ L-approach WP0→WP1→WP2→contact, glideslope servo, **contact 0.08 m/s ∈ IDSS**, RCS signs −1, attitude-
  first, nose shroud open. Matches §1A.
- ⛔ **KOS AUTO-ABORT NOT WIRED** (real Crew Dragon: any unplanned KOS breach / off-corridor → auto-retreat).
  `AbortResponder.KosRetreat` EXISTS but nothing detects a docking-corridor breach to trigger it — only the
  crew gate / g-abort do. **Build-gated** (needs a leg-aware corridor model so the nominal R-bar→V-bar move
  doesn't false-trigger) + pure test.
- ⛔ **IDSS contact envelope not enforced as a gate.** Contact speed is capped (0.08), but the lateral rate
  (≤0.04 m/s), lateral offset (≤0.10 m) and angular (≤4°) are not verified before declaring capture. Build-
  gated: add a "within capture box?" check that holds until aligned. Pure-testable.
- ◑ No explicit AI-burn / approach-ellipsoid (2 km) transition between 7.5 km and WP0 — the glideslope handles
  it. Fidelity, low priority.

### Undock / departure  — `ReturnControl.FlyDeparture` + `pure/Departure`
- ✅ Undock (hooks open) → CW departure burns to ~10 km below → phasing. Corridor-safe. Matches §1A.
- ⚠ Departure RCS `ForwardSign` — flight-gated sign confirm.

### Return: deorbit → entry → splashdown  — `ReturnControl` + `pure/DeorbitGuidance/Entry/Chutes`
- ✅ Trunk jettison BEFORE deorbit → retrograde Draco burn closed-loop on measured Pe (records Δv) → CoM
  shifter Descent Mode once → shield-forward lifting bank entry (footprint → bank σ, S-turn reversals) →
  drogues 5486 m / mains 1830 m → splash. Matches §1A.
- ⚠ Flight-gated signs: deorbit `ForwardSign`, `RollSign`/`RollRefSign`/`CrossSign`, and the entry-corridor Pe.

### Abort — every regime  — `AbortControl` + `pure/AbortResponder`
- ✅ Self-aware mode select from live state (LaunchEscape / AbortToOrbit / DeorbitReturn / KosRetreat /
  EmergencyUndock / RideItDown / SafeHold); universal sequence (escape → trunk jettison → shield-forward →
  chutes). **C1 chutes + C2 abort-attitude FLIGHT-PROVEN** (202127, 2 m/s splash). Matches §1A.
- ⛔ **F4 — RSS ocean detection.** `SafeSiteReachable` uses `body.TerrainAltitude(lat,lon) < 0` → reads 0/130
  over water in RSS, so WATER DEORBIT can't target water (DEORBIT NOW land-anywhere still works). Build-gated:
  find the correct RSS/Kopernicus ocean test. Needs the RSS body API — research before coding.

---

## 2. ⛔ BOOSTER DUAL-FLIGHT — the feasibility question (do NOT build on folklore)

The dual-flight design is: keep BOTH the Dragon and the separated booster **loaded** by widening
`Vessel.vesselRanges` ("our PRE"), keep the **Dragon active** (its autopilot flies S2→orbit), and fly the
**non-active booster** by registering `booster.OnFlyByWire += ...` (BoosterControl on the non-active vessel).

**The load-bearing assumption is UNVERIFIED:** that KSP invokes `OnFlyByWire` **and integrates throttle/steering
thrust** for a **non-active** loaded+unpacked vessel. Reading `mechjeb_src/MechJebCore.cs` shows MechJeb only
registers `OnFlyByWire` on **its own part's vessel** (each vessel carries its own MechJeb core) — it does **not**
prove KSP *calls* that callback, or simulates its thrust, when the vessel is not the active one. Our own code
comment calls it a "stock control-input limit." So this is **folklore either way** until measured.

**⇒ REQUIRED BEFORE THE BUILD: a one-flight feasibility probe.** Register `OnFlyByWire` on a non-active,
loaded, unpacked vessel, command `mainThrottle = 1`, and observe (CSV) whether it accelerates. Also test
whether widening `vesselRanges` (load/unload first, wait a tick, then pack/unpack — per the KSP API ordering
rule) keeps a 500 km-downrange booster loaded. If YES → build the dual-flight. If NO → the honest design is a
**focus-managed segment** (the current `ForceSetActiveVessel` path) accepting that the Dragon coasts on-rails
during the booster landing, OR a store-and-replay booster recovery. **No large build until this is answered.**
Sources: [KSP VesselRanges API](https://kerbalspaceprogram.com/api/class_vessel_ranges.html) ·
[kOS load/unpack ordering rule (#1128)](https://github.com/KSP-KOS/KOS/issues/1128) · `mechjeb_src/MechJebCore.cs:311–470`.

---

## 3. THE PRIORITISED BUILD ORDER

**Build-gated (I can build + headless-test now, no flight needed):**
1. ✅ **B-DOCK-KOS — DONE (2026-08-29, headless-green).** Docking KOS auto-abort: `pure/DockCorridor` (leg-aware
   V-bar corridor cone + KOS-breach test, +12 checks) wired into `DockingControl` on the terminal legs (toward
   WP2 + contact only — the R-bar climb + WP0→WP1 corner-cut arc through the boundary by design and are NOT
   gated) → `RequestAbort` → `KosRetreat`. ⚠ flight-pending (tick 3): confirm no false-trigger + the retreat.
2. ✅ **B-DOCK-CAP — DONE (2026-08-29, headless-green).** `pure/DockCapture` enforces the **IDSS IDD Rev E**
   capture box (closing ≤0.10, lateral rate ≤0.04, offset ≤0.10 m, angle ≤4°, rate ≤0.20°/s, +10 checks) before
   the geometric fallback declares soft capture; KSP's own docking magnetism still always completes a real dock,
   so it can't stall a good capture. ⚠ flight-pending: confirm the measured feeds track.
3. ✅ **B-F4-OCEAN — DONE (2026-08-29, green).** Root cause found (MechJeb KSP 1.12 source): `TerrainAltitude(lat,lon)`
   defaults `allowNegative=false` → **clamps ocean depth to 0**, so `<0` was never true → the RSS scan read 0/130.
   FIX: the 3-arg overload `TerrainAltitude(lat,lon,true)` returns the real (negative) seabed height. ⚠ flight-
   pending: fly WATER DEORBIT + read the site-scan log (expect many-of-130 water). `AbortControl.cs`.
4. ✅ **B-FDIR-FEEDS — DONE (2026-08-29, green).** FDIR spine wired observe-only (`FlightDriver.TickFdir`, ~4 Hz),
   acting behind `FdirActing=false`. Honest live feed = ResourceCritical from the Draco/RCS propellant margin
   (the return fuel: reads ~full through ascent since spent stages are separate vessels → no false trip, and is
   the true margin for rendezvous/deorbit). ⚠ flight-pending: confirm no false trips, then enable `FdirActing`.
4b. ✅ **T2b FDIR-RESIDUAL-FEEDS — DONE (2026-08-29, green, +16 headless checks → 731,271).** The thrust / control /
   stall monitors now fed HONESTLY (were nominal-until-fed), shaped by a new PURE `pure/FdirFeeds.cs` so every guard
   is headless-tested and an UNMEASURABLE moment reads nominal (never a false trip):
   • **ThrustDeliveredFrac** = Σ finalThrust / (throttle·Σ current-conditions full-max) over the COMMANDED main
     engines (a flamed-out-but-commanded engine still counts in expected but adds 0 to actual → its lost share drops
     the ratio = the engine-out signal). Suppressed while the hold-downs are clamped (thrust ramping to release);
     nominal on a coast or a Draco-only (ModuleRCS) burn — so it scopes itself to the crew-critical main-engine burns.
   • **ControlSolutionOk** = ¬(no-authority tumble): actively holding attitude + ~zero best-axis control torque +
     spinning past a tumble rate + pointing far off (the RCS-`GetPotentialTorque`-zero case). A healthy hard slew HAS
     authority → excluded; max-Q gimbal saturation is caught upstream (AscentControl q·α/AoA + g-abort).
   • **PlanProgressRate** = `RendezvousControl.NearClosingRateMps`, published only while it is actively closing
     (`NearClosingActive`) — an intended phasing coast / co-elliptic raise leaves it nominal.
   • **TrajErrorM** stays NOMINAL by design — no honest UNIFORM position-error residual without inventing one; ascent
     divergence is covered by q·α/AoA + g-abort, near-field drift by DockingControl's own corridor/KOS abort.
   FDIR remains OBSERVE-ONLY. ⚠ flight-pending: confirm no false trips in a flight, then enable `FdirActing`. Commit f48b5ae.
5. **B-RV-NAMED** *(low priority, fidelity)* — model the discrete named burns over the far-FSM for strict fidelity.
5b. **T13 CRAFTDUMP-REFRESH** *(open — Chris 2026-08-29)* — read the NEW `data/craftdump.csv` and update the artifacts'
   vehicle data with it (dashboard vehicle/audit rows + `docs/CRAFT_DUMP_VEHICLE_MAP.md` / `docs/VEHICLE_AUDIT.md`
   where the dump changed). Cross-check every part/module/action vs what the artifacts + code assume; flag any drift
   (new/removed parts, changed ignitions/thrust/modes). Ground truth = the live dump, not the `.md`.

**Booster dual-flight:**
6. ✅ **B-BOOST-DUAL — BUILT (2026-08-29, green).** Feasibility RESOLVED (OnFlyByWire drives any UNPACKED non-active
   vessel — BDArmory proof; no probe needed). `src/RangeExtender.cs` = "our PRE" (VesselRanges widened, PRE method
   ported). `MissionConductor.TickBoosterRecovery` = Chris's design: PRE on before sep → focus booster (S2 coasts,
   kept loaded) → land → return focus to S2 → PRE off; `PreRangeKm=600`. ⚠ flight-gated (H1): PRE keeps both
   loaded, focus switches fire, the S2 survives the coast + resumes, booster lands despite phantom forces (PRE's
   >100 km caveat), size `PreRangeKm` from measured separation. Full detail: `docs/BOOSTER_DUAL_FLIGHT_RESEARCH.md`.

**Flight-gated (wired behind default-OFF flags to RUN + collect data; SIGNS resolve from a flown CSV):**
- ✅ **T4 B2 aero-stiffness feed — WIRED (2026-08-29, green, commit b13113a).** `AscentControl.FeedAeroStiffness`
  feeds `SelfCal.AeroPitchStiffness` every powered tick: kAero = M_α/I from the isolated aero pitch angular-accel
  = (measured pitch ω̇) − (control ω̇ = `AttitudePilot.ActPitch·PitchAccelRadS2`), regressed on a SIGNED pitch-plane
  AoA (causal pairing). RUNS + RECORDS always (cols `cal_kaero`/`cal_kaero_p`/`qalpha_cap_deg`/`aero_ang_accel`/
  `aoa_signed_deg`); the q·α cap USES it only when `UseAeroStiffnessFeed` on AND ≥`AeroFeedMinSamples` excited
  samples absorbed (default OFF). ⭐ finding: gating on the RLS covariance P is WRONG — a zero-AoA ascent barely
  excites it, so P GROWS, not shrinks → an excited-sample COUNT is the honest trust signal. ⚠ flight-gated (sign):
  read `aero_ang_accel` vs `aoa_signed_deg` off a flown CSV → set `AeroFeedSign` → flip the flag on. Also fixed:
  `AscentControl.Reset` now clears the SelfCal estimators (stale RLS state must not carry to a new vehicle).
- ✅ **T5 B3 RCS balancer — ASSESSED + DIAGNOSTIC WIRED (2026-08-29, green, commit 9c89ac2).** ⚠ FINDING: the
  per-thruster solve is NOT stock-actuatable — the Dracos are 2 all-axis multi-thruster `ModuleRCS` with only a
  per-MODULE `thrustPercentage` (no per-thruster lever), and KSP's RCS solver + the concurrent `AttitudePilot` hold
  already close the loop on induced torque. Wired instead as a records-only diagnostic (`Actuator.RcsInducedTorque`
  runs the tested pure `RcsBalance`/`ThrustBalance` LIVE during prox-ops translation → cols `rcsbal_torque_naive`/
  `_resid`/`_force_frac`). True apply = direct force injection, deferred with this data. `docs/RCS_BALANCE_FINDING.md`.
- **Still owed:** B8 CourseCorrect targeting (booster/entry steering signs), deorbit/entry/departure RCS + bank
  signs, g-cap (F5). Wire behind default-OFF flags to RUN + collect data; their SIGNS are only resolvable from a
  flown CSV — wiring them "on" blind risks the nominal mission.

**Flight-gated (Chris flies; I analyse + tune — the F1/F2 fixes are installed, unflown):**
- Verify deorbit brings a vessel home (F1); AUTO SEQUENCE resumes the right phase (F2); rendezvous converges to
  dock (F3); resolve the best-guess SIGNS (deorbit fwd, entry bank/roll, departure fwd) from the CSV; tune g-cap
  (F5). Then the per-phase DB tune.

**The rule:** every build lands headless-green (`cd plugin && python build.py test`) + committed; nothing is
"working" until flown (tick 3). Update BOTH artifacts each response ([[update-artifacts-every-response]]).

Cross-refs: [SEQUENCE_MAP.md](SEQUENCE_MAP.md) (§1A envelope) · [ISSUE_REGISTER.md](ISSUE_REGISTER.md) (F1–F7) ·
[PHASE_ACCEPTANCE_CRITERIA.md](PHASE_ACCEPTANCE_CRITERIA.md) · [VEHICLE_AUDIT.md](VEHICLE_AUDIT.md).
