# Time-warp — proper KSP technique for a perfect-warp autopilot

**Goal (Chris 2026-08-29):** *never miss a manoeuvre*, and *be in position ready to burn before the burn time
passes.* This is the researched technique (primary source: MechJeb2 `MechJebModuleWarpController` +
`MechJebModuleNodeExecutor`, read in full at `Desktop/mechjeb_src`) vs. our current system, the gaps, and the plan.

---

## 1. KSP time-warp fundamentals (the mechanism)

- **Two modes.** `TimeWarp.WarpMode`:
  - **HIGH = on-rails.** Physics OFF; the vessel is propagated kinematically along its orbit. Rates 1/5/10/50/
    100/1000/… (`TimeWarp.fetch.warpRates`). **The vessel CANNOT rotate or actuate** — `OnFlyByWire`/FixedUpdate
    control does not run, so any `Steering.Point`/AttitudePilot command is **inert**. Attitude is frozen inertially.
  - **LOW = physics warp.** Physics ON at ≤4× (`physicsWarpRates`). Control still runs, so you *can* slew/burn — but
    everything is sped up and less precise.
- **Altitude gating.** On-rails warp is blocked below a per-body, per-rate altitude ceiling: `TimeWarp.fetch
  .GetAltitudeLimit(rateIndex, body)` and `GetMaxRateForAltitude(altitude, body)`. In atmosphere you get **no**
  on-rails warp; MechJeb falls back to **physics warp at ≤2×**. (Landed/PRELAUNCH is a special case that *does*
  allow high warp — that is why the pad launch-window hold can warp.)
- **Setting the rate.** `TimeWarp.SetRate(index, instant)`. `instant=true` snaps; `false` ramps over real seconds.
- **`TimeWarp.fetch.WarpTo(UT)`** — stock "warp to a time" that auto-selects + decelerates. Convenient but not
  finely controllable; MechJeb rolls its own instead (see §2).
- **Kraken guard.** Calling `SetRate(0)` when already at 0 can trigger "unexpected rapid separation" — always guard
  `if (CurrentRateIndex != 0)` before dropping to realtime. (We already do; MechJeb does too.)

## 2. The proven MechJeb technique

### 2a. The warp controller (`WarpToUT`, called every FixedUpdate while a target is set)
- **Rate ∝ time remaining:** `desiredRate = UT − (now + fixedDeltaTime·CurrentRateIndex)`, clamped `[1, maxRate]`.
  So the rate *is* the seconds-to-target minus a one-tick lead → it **self-decelerates** and can never overshoot by
  more than ~1 real second. As you approach, the rate ladders down to 1 automatically.
- **Altitude fallback:** below `GetAltitudeLimit(1, body)` → **physics warp at ≤2×**, else on-rails at `desiredRate`.
- **One step per call, with anti-thrash guards:** increase only if the next rate's altitude limit is met, the
  previous change has completed (`warpRates[idx] == CurrentRate`), and ≥2 s since the last increase. **`instantOn
  Decrease = true`, `instantOnIncrease = false`** — decreases snap (stop cleanly), increases ramp (smooth).

### 2b. The node executor state machine (this IS "in position before the burn")
States: **INITIAL_WARP → ALIGNING → WARPING → LEAD → BURN**.
- **Ignition is CENTERED on the node:** `ignitionUT = node.UT − halfBurnTime`, where `halfBurnTime` comes from the
  staged fuel-flow sim (our `StageStats`/`FuelFlowSim` equivalent, `BurnTime(dv, out halfBurnTime, out spool)`) and
  **includes spool-up**. You start burning *before* the node so the Δv impulse straddles it.
- **INITIAL_WARP** (`timeToBurn > 600 s`): coarse-warp to `ignition − 600 s`, thrust off, **no alignment needed**.
- **ALIGNING**: point the nose at the (inertial, de-rotated) burn vector at **1×/physics** (on-rails can't rotate).
- **WARPING**: once aligned within **`WarpAlignedToleranceDegrees` = 10°** *and settled*, warp toward `ignition −
  LeadTime` **while holding** that attitude. On rails the frozen inertial attitude = the burn vector, so it **stays
  aligned**; if it drifts beyond 10°, drop back to ALIGNING (minimum warp) and re-point.
- **LEAD** (last **`LeadTime` = 3 s**): minimum warp, hold attitude, and **ullage** (fire RCS to settle propellant)
  so it is stable at ignition.
- **BURN**: ignite only when `now ≥ ignitionUT` **and** aligned within **`AlignedToleranceDegrees` = 1°** and angular
  velocity < 0.001 rad/s (`AlignedAndSettled`). Burn with `ThrustForDv`; terminate when the remaining Δv is spent /
  the angle to the node exceeds 90°.
- **De-rotation:** the burn direction is stored as a *de-rotated* world vector (`inverse(Planetarium.rotation) ·
  burnVector`) and re-rotated each tick, so the target is a **consistent inertial direction** across the warp.

**The essence:** orient FIRST (far out, at physics rate), then warp *holding* the attitude, so warp-exit leaves you
already pointed — the only thing left in the final seconds is ullage. That is how it is *always in position before
the burn*, even with a slow-turning vehicle.

## 3. Our current system (`pure/WarpPlan` + `MissionConductor`)

- `WarpToEvent(UT)` sets a target; `Tick()` each frame: **universal burn-guard** (throttle>0.01 or Draco translation
  >0.001 → `Realtime()`, and it zeroes the target so we can never re-warp mid-burn) → `MustBeRealtime` window
  (`BurnLeadS + SettleMarginS` = 15 s) → else `ApplyRailWarp(DropOutUT = UT − BurnLeadS)`.
- `WarpPlan.SafeRate`: picks the highest rail rate with `LookaheadTicks`(=3) windows of headroom to the drop-out and
  **ratchets down monotonically**; `MaxWarpRateX` hard-caps it. `DropToRealtime` = instant `SetRate(0,true)` (guarded).
- On-rails only for coasts (drops LOW warp); relies on KSP to clamp the rail rate to the altitude limit.

**What's already right:** the burn-guard (never burn under warp), the monotone down-ladder, the hard cap, the
Kraken-guarded instant drop, the coarse coast ETA (`CoastEta`), and warping stops above the atmosphere on entry
(`EntryWarpMarginM`). The pure decision layer is headless-tested.

## 4. The gaps vs the two requirements

### G1 — NEVER MISS: the ladder steps down GRADUALLY (real bug)
`ApplyRailWarp` steps with `SetRate(idx, false)` (**gradual**) on the way down too. From a high rate, KSP's spin-down
takes ~1–2 **real** seconds, during which game-time races ahead (at 1000×, 1 real s = 1000 game-s). `LookaheadTicks
= 3` only guarantees `3·rate·tickS` game-seconds of headroom (≈0.06 **real** s at any rate) — far less than the
spin-down time → **it can blow past the drop-out from high rates.** MechJeb's answer is exactly `instantOnDecrease =
true`: on-rails is kinematic, so snapping the rate down is safe and stops cleanly. **→ Fix now (§5).**

### G2 — IN POSITION BEFORE THE BURN: no orient-during-warp, fixed 12 s lead, slow Dracos (the big one)
On-rails warp **freezes attitude** and our `Steering.Point` is inert during it, so we drop out `BurnLeadS = 12 s`
early at a **stale frozen** attitude (from whenever warp began — the burn vector, prograde/retrograde, has rotated
along the orbit since then, often tens of degrees). We then have 12 s to slew on a **reaction-wheel-less** vehicle
(16 Dracos do attitude *and* translation) — a large reorientation takes far longer than 12 s → we arrive **still
slewing** → the burn fires mis-pointed/late, or we miss the window. We do **not**: (a) orient to the burn vector
before/through the final warp, (b) gate the warp on alignment, (c) size the lead to the slew + ullage, or (d) center
the burn on the event (`ignition = event − halfBurnTime`). **→ The align-then-warp build (T14, §6).**

*Mitigating truth:* our guidance is **closed-loop + self-correcting** (the far-field transfer gates its burn on the
phase angle; the deorbit is closed-loop on measured Pe; the entry footprint controller absorbs deorbit-timing
error), so being *early/late* is recoverable rather than catastrophic. That is why G2 is a "be reliably ready"
improvement, not a flight-blocker — but it is exactly Chris's requirement, so it is the real target.

### G3 — two different warp mechanisms
The pad launch-window hold uses stock `TimeWarp.fetch.WarpTo` (FlightDriver); the coast conductor uses our
`SafeRate` ladder. Unifying on one path (our ladder, with G1 fixed) removes a class of inconsistency. Low priority.

## 5. Fix landed now — instant-on-decrease (closes G1)
`MissionConductor.ApplyRailWarp` now snaps DOWN and ramps UP: `SetRate(idx, idx < CurrentRateIndex)`. On-rails
kinematic warp makes an instant step-down safe, and it removes the spin-down overshoot from high rates — the
"never overshoot the drop-out" guarantee now actually holds at every rate. (The monotone down-ladder + `MaxWarpRateX`
+ the `MustBeRealtime` window + the burn-guard are unchanged.)

## 6. The align-then-warp build — T14 (the "in position before the burn" fix)
Port MechJeb's node-executor pipeline to our closed-loop conductor so we are provably pointed + settled before every
scheduled burn:
1. The coast controller publishes to the conductor, alongside `WarpToEvent`, the **burn direction** (inertial,
   de-rotated) and — via `StageStats`/`BurnTime` on the Draco Δv — the **halfBurnTime + ullage/orient lead**, so the
   drop-out becomes `ignition − orientLead − ullageLead` with `ignition = eventUT − halfBurnTime` (burn centered).
2. A conductor state machine **INITIAL_WARP → ALIGNING → WARPING → LEAD**: coarse-warp far out (no alignment); within
   the fine window **orient at 1×** to the burn vector; only warp on-rails once aligned within a warp-tolerance and
   **hold** it (frozen inertial = aligned); if it drifts, drop warp + re-align; final **LEAD** window ullages.
3. Ignite only when `now ≥ ignition` **and** aligned within a tight tolerance **and** settled (ω≈0) — reusing the
   `DockCapture`/attitude signals we already have. Everything headless-testable in the pure decision layer first.
Estimated: a new `pure/WarpPlan` extension (burn-centred drop-out + the align/warp/lead state predicate, tested) +
conductor glue + the coast controllers publishing the burn vector. Sign-safe (no new flight-tuned constants beyond
the tolerances, which are conservative). This is the task that makes warp "perfect" per the requirement.

---
**Sources:** MechJeb2 `MechJebModuleWarpController.cs`, `MechJebModuleNodeExecutor.cs` (`Desktop/mechjeb_src`); the
KSP `TimeWarp` API (`warpRates`/`physicsWarpRates`/`GetAltitudeLimit`/`GetMaxRateForAltitude`/`SetRate`/`WarpMode`).
Cross-refs: [pure/WarpPlan.cs], [MissionConductor.cs], [pure/CoastEta.cs], [NOMINAL_END_TO_END_BUILD.md].
