# RETURN FIX PLAN — the autopilot cannot bring the crew home (council-verified)

> **NEXT-SESSION START HERE.** This is the active build plan. Read it + `docs/ISSUE_REGISTER.md` +
> the memory. Then build **R1**. Process is unchanged: §8 output before code, ONE change class per
> campaign, pure-first + headless where the logic is pure, 3-tick (nothing "done" until FLOWN),
> instrument the same pass, verify every claim against live code before editing (line numbers below
> are as of 2026-08-30 HEAD — re-verify).

## Why (Chris's 2026-08-30 return-test session)
Chris flew the return; the autopilot **stranded and nearly killed five+ crews** — he hand-flew them home
by eyeball (no maneuver nodes). Ledger from the KSP log: **12 killed** (respawned), **17 rescued by
hand** ("available again"), **0 brought home by the autopilot**. Only the ISS remains in space. A human
out-flying the autopilot means the return is genuinely broken.

## ⚠ Method lesson (why this plan is v2)
My FIRST return plan led with "no lifting entry" and mis-ranked the fixes. A 3-AI council red-team +
**reading the actual code** overturned it. Do the same discipline here: the roots below are VERIFIED in
code/CSV/log, but the DEORBIT-BURN-broken and CHUTE-root each still want confirmation on ≥2 flights and
a check that the installed DLL matches source (the rescue path was rewired recently, commit 979959e).

## THE ROOT (verified): the autopilot deorbits on an EMPTY engine
- `AbortControl.FlyDeorbitReturn` (~line 327-335) fires the **SuperDraco** (`EngineRole.PodAbort`,
  `Actuator.ActivateEngines`/`MaxThrustN(PodAbort)` → `FlightDriver.SetThrottle`). The SuperDraco is the
  ABORT engine and is **empty on a return** (screenshots: "Ignitions Remaining: 0 · Flame-Out! No
  propellants"). **Proof:** flight 024400 deorbit `Burn` phase — throttle commanded **0.36**,
  `thrust_n = 0`, `trans_z = 0`, pe 196.9→196.1 km. Zero thrust → no deorbit → crew stranded.
- The code comment itself admits it: *"Real Dragon reserves the SuperDracos + deorbits on the Dracos …
  for an emergency we trade that for speed."* The trade fails: the SuperDraco has nothing to trade.

## The verified chain of return failures
1. **Deorbit fires an empty engine** (above). Root of the stranding.
2. **Return Draco Δv collapses:** MMH+NTO went **99% → 0%** by flight 024400 (`mmh_frac`/`nto_frac`) —
   even the CORRECT engine (Dracos) may lack the Δv. Load too low, or orient/rendezvous over-spends it.
3. **Deorbit TARGET likely wrong for RSS:** `DeorbitTargetPeM = 50 km` (both `AbortControl` +
   `ReturnControl`). A fixed 50 km pe is a steep entry; the real corridor is an entry FPA (~−1.5° class).
4. **Ballistic entries → 7.4–8.4 g + chute overheat:** flown on Chris's MANUAL pe≈−5000 km setups
   (because the autopilot couldn't deorbit). From that steepness, shield-forward ballistic pulls 8 g and
   the **Dragon 2 Main Parachute skin hits 507/506 K (100% of limit) → crew death** (log 03:30). ⚠
   CONFOUND: these were human-forced steep trajectories — the entry can't be graded until the deorbit
   (1–3) works. Whether a ballistic entry from a CORRECT corridor is survivable is UNPROVEN.
5. **Trunk jettison broken (confirmed):** the trunk decoupler is a **`ModuleTundraDecoupler`** (craft
   dump: `TE.18.DRAGONV2.TRUNK`); `ReturnControl.JettisonTrunk` (~line 316) + `Actuator` look for
   `ModuleDecouple`/`ModuleAnchoredDecoupler` → "no trunk decoupler found" every rescue. Chris
   hand-decoupled (mass 10,020 kg at sep → 6,386 kg at entry).
6. **Nose-shroud spam:** ~14,000 "OPENED" logs in ~20 min — the return/rescue re-opens every tick.

## Verified-OK (do not re-litigate)
- Ascent to orbit + 4.5 g crew cap (flight 023443, gmax 4.50). Campaign 6 RCS authority (geometric
  12890/10690/9500 N·m firing, log-confirmed). **CoM shifter API is correct** —
  `AdjustableCoMShifter.ToggleMode` exists (`DescentModeCoM=(0,0,0.2)`), matching `EngageCoMShifter`.
  Chute deploy gates at 5.5 km (`SequenceAbort`) — the earlier "deploy at 91 km" claim was a
  phase-column artifact, DROPPED.

## THE FIXES — build in this order (R1 first)

### R1 — deorbit on the DRACOS, not the empty SuperDraco  ← BUILD THIS FIRST
- **Fix:** unify BOTH the nominal (`ReturnControl.FlyDeorbitEntry`) and rescue
  (`AbortControl.FlyDeorbitReturn`) deorbit onto ONE **Draco** burn — retrograde-point + Draco
  translation, **closed-loop on measured pe** to the entry corridor (R2), delete the SuperDraco-deorbit
  path. The SuperDraco g-limit throttle formula (`GLimit·g0·m/sdMaxN`) does NOT port to the ~6.4 kN Draco
  (it clamps to full) — rewrite as a low-thrust translation burn that stops at the target.
- **Δv budget:** verify the Draco MMH+NTO load ≥ deorbit need (~100 m/s real). It drained to 0 — find
  whether the load is too low or orient/rendezvous over-spends, reconcile to fidelity. Instrument
  planned vs delivered deorbit Δv.
- **Files:** `plugin/src/AbortControl.cs` (FlyDeorbitReturn), `plugin/src/ReturnControl.cs`
  (FlyDeorbitEntry deorbit stage), `plugin/src/pure/DeorbitGuidance.cs`; `data/craftdump.csv` (Draco load).
- **Acceptance (Tick-3):** a flight lowers pe to the corridor on the Draco propellant it actually has,
  logs delivered Δv > 0. §8 before code.

### R2 — deorbit target = entry FPA corridor, not a fixed 50 km pe
- Target an entry flight-path angle appropriate to a lifting capsule in RSS; keep it `[Tunable]`.
- **Files:** `plugin/src/pure/DeorbitGuidance.cs` / `Entry.cs`; `DeorbitTargetPeM` in both controllers.

### R3 — trunk jettison (confirmed bug)
- Detect `ModuleTundraDecoupler` and invoke its `Decouple` event (the TRUNK part, not the Dragon-drop/S2
  decoupler), in BOTH `ReturnControl.JettisonTrunk` and `Actuator`/`AbortControl`. Acceptance: "TRUNK
  JETTISON" logged, mass drops ~3.6 t.

### R4 — entry survivability: test ballistic-from-corridor, add lift only if needed
- After R1+R2 give a shallow deorbit, FLY it and measure the shield-forward ballistic entry. If peak g ≤
  ~5 and chute skin < ~85% → the emergency stays simple (as designed). If NOT → engage CoM descent mode
  (API verified) for a lifting entry to cap g + protect the 506 K chutes. Full LZ-precision belongs in
  nominal `ReturnControl`, not the emergency.

### R5 — chute survival
- Root = the chute part's **506 K skin limit** + steep-entry heat. Flows from R2 (cooler) + R4 (lift). If
  still hot from a correct entry, reconcile the chute maxTemp — **Chris fidelity call**.

### R6 — shroud latch + nominal LZ bank steering
- **Shroud:** latch open/close (kill the 14k spam); keep OPEN through the Draco deorbit burn (RCS needs
  it — the nose cone shields the Dracos), CLOSE only after the burn.
- **LZ steering (nominal only):** tune the coded `EntrySteering` footprint → `Entry.Guide` σ → bank loop
  to Chris's confirmed roll-steering (rolling moved the predicted impact — direction confirmed, magnitude
  unisolated). The autopilot must fly it RECORDED to get the tuning data (Chris's manual runs had no CSV).
  Resolve the SIGNS (RollSign/RollRefSign/CrossSign) from that recording. Flight-tuned (I-B).

## Council-mandated open checks (fold into the R-steps)
- Confirm installed DLL == current return/abort source (rescue rewired 979959e).
- Confirm the deorbit-burn failure on ≥2 flights (not just 024400).
- Confirm whether rendezvous/dock actually completes — NO evidence either way (all flights ended in
  PHASING or manual deorbit). Separate owed item; don't assume it works.

## Current repo/artifact state
- No code changed this session (analysis + planning). HEAD `394e566` (return-failure docs) — needs a
  GitHub Desktop push with the earlier `4de7a74…` chain.
- Dashboard "I Smell What You're Stepping In" v27 (return analysis + KLM CORRECTED: memorial now names
  the real dead kerbals, 12 dead / 17 rescued; + the black-granite wall + Marvin's family letters).
- Public mod site published (new artifact "DragonScreen") — read-only, shareable.
- Installed DLL still has Campaigns 1a–6 (ascent + 4.5 g + RCS authority) — return is UNTOUCHED.
