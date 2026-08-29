# DragonScreen — MASTER FIX PLAN (fix every open issue; Grok-reviewed)

> Built at Chris's request: a rock-solid, verified plan to fix **every** current open issue in `ISSUE_REGISTER.md`,
> **without requiring a flight to build the fixes.** Principle: build + headless-verify everything that is code-fixable
> or diagnosable from the EXISTING flight data (155116 / 155356 / 161224); flight-CONFIRMATION is deferred and batched
> into the next flight whenever Chris chooses. Issues that genuinely cannot be diagnosed without NEW flight data or a
> fresh part dump are marked **DATA-GATED** — honestly, they can't be fixed blind.
>
> Process: ONE change class per campaign, ordered, §8 before code, headless-green + install per campaign. Each root is
> either **VERIFIED** (traced to file:line / existing CSV) or **HYPOTHESIS** (marked, to confirm). Grok verifies each
> against the repo before I build.

## Legend
- **[V]** root verified from code / existing data · **[H]** hypothesis (needs confirmation) · **[DG]** data-gated (needs a NEW flight / fresh dump).

---

## Campaign A — BOOSTER IGNITION + SURVIVAL  *(class: Actuator-ignition)*  — fixes H1b, H1c/H1c'
- **H1b mode-mismatch — [V] FIXED⚑ (built, installed, commit `12a2e7f`).** `BoosterDescent.Guide` emitted the bare
  `3`(entry)/`1`(landing); `VehicleParts.EngineIdIsMode` decodes `1=three,2=centre,else=all`. So entry (3→else) lit the
  AllEngines/outer set = the engines that spent their 1 ignition at liftoff → `eng_ignited=0`. Now emits
  `ModeThreeEngine`/`ModeCentreOnly`.
- **Contingency chain if the flight still shows `eng_ignited=0`** (do NOT pre-build — measure first):
  1. **[H] Ignition-count** — does the `ThreeEngine` octaweb mode actually have an UNUSED RealFuels ignition, or does the
     octaweb share a budget? *Verify from ConfigCache / the .craft* (no flight needed) — if shared, the mode-fix alone
     won't light it. Add an `ign_remaining` recorder column if ambiguous.
  2. **[H] Off-focus RF** — only if ullage settled + right mode + ignitions remaining and it STILL won't light. Then research
     RealFuels source for an `isActiveVessel` gate; last resort a brief force-focus for the ignition instant.
- **H1c/H1c' (booster tumbles→burns up) — [V] couples to H1b.** With the entry burn LIT, the 3-engine **gimbal** gives
  strong attitude authority to hold retrograde during the burns (the broadside-heating cause). Between burns the cold-gas
  RCS (Δv ~1 m/s) is thin. **Plan: confirm H1b lights first; THEN measure whether coast attitude still needs more
  cold-gas** (a config load bump) — don't add propellant blind.
- **Verification:** mode-fix is Tick-1 green; the chain is measured on the next flight (deferred). Pre-emptive: read the
  octaweb ignition config now.

## Campaign B — RENDEZVOUS RCS EFFICIENCY  *(class: Phase-FSM / control)*  — fixes C2a-RCS, L2, F3
- **[V] Root (data + code):** `RendezvousControl` calls `Steering.Point(v, aimWorld)` **every tick** during phasing
  (`RendezvousControl.cs:184, 272`) — it holds a TIGHT attitude (`AttitudeReadyDeg=5°`) to the prograde/burn vector
  **continuously, including during the long coasts between burns**. On a no-reaction-wheel vehicle every degree of hold =
  RCS pulses → the CSV's **63% attitude-only firing** (1094/1728). The **~104° swings** (att_err p95 103.6°) are hard
  slews when the aim target jumps (phase change / prograde↔burn-vector). This drains MMH/NTO 1.0→0.
- **[H] L2 pe-drop (197→150):** a pure prograde raise can't lower pe → the unbalanced attitude-RCS produces a net
  radial/retrograde Δv leak (the parked C2a-later). Confirm by decomposing each RCS pulse's Δv in the orbital frame.
- **Fix (one cause at a time, from the existing data — NO new flight to BUILD):**
  1. **Release the tight attitude during COAST** — don't hold 5° pointing between burns; re-point only within a lead time
     before the next burn. (The biggest saver; the RCS stops pulsing during the multi-hour coasts.)
  2. Widen the coast attitude deadband so sub-threshold error doesn't chatter the Dracos.
  3. Diagnose the 100° aim jumps (why the target flips) — likely a per-phase re-point; smooth or lead it.
  4. Instrument the per-pulse orbital-frame Δv (radial/along/normal) to pin the pe leak (L2), then null it.
- **Verification:** the coast-release + deadband are headless-testable (pure attitude-hold logic); the RCS-budget saving is
  flight-confirmed later. F3 (CW terminal convergence) re-checks once the budget survives to the near field.

## Campaign C — SMALL UI / GLUE BATCH  *(class: UI/actuator glue, all code-verifiable now)*  — fixes UI-shroud, shroud-spam, U2, U3, L6, F4
- **UI-shroud [V]:** relabel the dash button `JETTISON NOSE CONE` → **`TOGGLE SHROUD`** and change the command to toggle
  open/close by the shroud's `Progress` (call `Actuator.OpenNoseShroud` if closed, `CloseNoseShroud` if open).
  `PanelMap.cs` (label + command enum) + the command handler.
- **shroud-spam [V]:** `nose shroud OPENED` logged 1674× — make `Actuator.OpenNoseShroud`/`CloseNoseShroud` **idempotent**:
  read the actual animation state and only `Toggle()` + log on a real state change (same pattern as the I3 ignition-log
  fix). Callers keep their latches; this removes the spam + any self-fighting toggle.
- **U2 [V]:** `Pages.cs:1082` trips CAUTION from `Alarms.Low(Propellant01)` while the gauge (by design) shows the near-spent
  ASCENT stage near SECO. Fix: suppress the low-prop alarm while the lit stage is an ascent stage (or alarm on the RETURN
  MMH/NTO budget, which R3 now exposes).
- **U3 [V]:** `CabinEnvironment.cs:184-185` sets `NetPwr1W = watts*0.55`, `NetPwr2W = watts*0.45`; the flight showed 0 W →
  `watts` resolves to 0 in that state. Fix: make `watts` a real generation-minus-draw net (negative on battery), or, if
  it can't be modelled honestly, relabel the dials. Low priority.
- **L6 [V]:** `MissionConductor.LogSeparation` reads `sep 0 km` — the `FindById(dragonId)` / CoM-diff is wrong. Fix the id
  lookup (the upper stage's `persistentId` may change at sep) or compute sep from both loaded vessels' CoM directly.
- **F4 [V]:** deorbit water-site scan reads 0/130 because `body.TerrainAltitude(lat,lon) < 0` is never true in RSS (ocean
  is at 0, not negative). Fix: detect ocean by `body.ocean && altitude-at-point ≤ sea level` per the RSS convention.
- **Verification:** all headless / code-review verifiable — no flight needed. (DEORBIT-NOW/land-anywhere already works.)

## Campaign D — ASCENT TUNING  *(class: tuning)*  — fixes F5/M1; DATA-GATED: M2, F6
- **F5/M1 g-cap [V root, tune fix]:** `S2GLimitG=4.1` still peaks **4.53 > 4.5** (flight 155116) — the throttle taper LAGS
  the g rise at light S2 mass. Fix options: (a) **lower `S2GLimitG` to ~3.9** (0.6 g margin covers the ~0.4 g overshoot —
  the sanctioned knob per the code comment `AscentControl.cs:161`); (b) better: a **predictive taper** that anticipates
  the g rise from `d(m)/dt` so the limiter leads instead of lagging. Start with (a); (b) if it still clips.
- **M2 [DG]** gravity-turn AoA ~8° (should be ~0) — needs the recorder loss-columns + a flight for the B9 LaunchTuner.
- **F6 [DG]** early-ascent roll saturation — verify vs the control after B.
- **Verification:** the g-limit setpoint change is headless-safe (ControlLaw.ThrottleLimit is tested); the ≤4.5 peak is
  flight-confirmed later.

## Campaign E — ROBUSTNESS / LATENT  *(class: contingency)*  — fixes L4, L5, G7/M6
- **L4 [V]:** the attitude loop usually runs on stock `GetPotentialTorque` ~2 N·m for the Dracos; the geometric r×F
  fallback only engages below 1 N·m. Fix: use **`max(reported, geometric)` with hysteresis** so the loop always has a
  sane authority estimate. (`AttitudeController.ControlTorque`.)
- **L5 [H/DG]:** RemoteTech duplicate-key exceptions with two vessels loaded — a mod interaction, not our code. Watch;
  if it destabilises the dual-flight, guard/suppress the specific call. Low.
- **G7/M6 [DG-low]:** deorbit 9.75 g / 10 g steep entry — verify vs the researched abort g-band (≤13.6 g) before treating
  as a defect; likely within envelope.

## Campaign F — VEHICLE FIDELITY AUDIT  *(class: reference/baseline)*  — V0–V4
- **[DG]** needs a **fresh part dump**. Chris doesn't want a flight — I can instead read the **live ConfigCache / the .craft
  file** directly to walk every part (RCS thrust V1, octaweb/MVac thrust V2, gimbal range V3, chute count + cold-gas V4).
  This is the one place a fresh dump substitutes for a flight. Do LAST (fidelity, not blocking).

---

## Ordering + honest flight-dependency
1. **A** (booster ignition) — mode-fix built; the contingency is a **config read now** + a flight later.
2. **C** (small UI/glue batch) — fully code-fixable now, unblocks the dash + logs. Can run in parallel with B.
3. **B** (rendezvous RCS) — buildable now from the existing 155116 data; the biggest mission-impact fix.
4. **D** (g-cap) — small tune, buildable now.
5. **E** (robustness) — buildable now (L4), watch (L5).
6. **F** (fidelity) — config read.

**Everything except the flight-CONFIRMATION and the [DG] items (M2/F6/M3/L5/F1/F3) can be BUILT + headless-verified now.**
The [DG] items are honestly blocked on a future flight — I will not guess their fixes.

## What I need Grok to verify (against the repo)
For each **[V]/[H]**: confirm the file:line I cited actually says what I claim, and refute any root that's overstated.
Specifically: (B) is `Steering.Point` really called every coast tick with no coast-release? (C) are the shroud latches /
`OpenNoseShroud` idempotency as described? (D) is `S2GLimitG` the only g knob and lowering it safe? (A) is the ignition
config per-mode or shared? — and the overall **campaign ORDER**.
