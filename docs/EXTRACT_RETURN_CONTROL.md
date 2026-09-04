# EXTRACT — `ReturnControl.cs` (deleted gen-2) → the §B9 Phases 6–10 sequence T21 must reproduce

> **[FROM DELETED GEN-2 `ReturnControl.cs` — REFERENCE ONLY, NEVER LIVE]**
>
> Produced by register task **W18** (Wave E-6), 2026-09-04, under the owner's G6 re-verdict of 2026-09-04
> (*"undocking → re-entry orbit → re-entry → landing are ALL MechJeb"*): `W18` was re-verdicted
> **RECOVER-CODE → RECOVER-REFERENCE**. **No `.cs` file was created, restored or edited by this task, and
> no facade property changed.** Everything below was read out of `8b81816^` with `git show`.
>
> ⛔ **EVERY CONSTANT IN THIS DOCUMENT IS UN-CONVERGED** (§B16.8 ruling 2). `ReturnControl.cs` and
> `pure/Departure.cs` **never flew — not once**. `pure/Chutes.cs` is the single partial exception, and §4.4
> shows that exception is thinner than it looks. Nothing here is a tuned value; it is a *starting shape*.
>
> ⛔ **This document is EVIDENCE, not an instruction.** `docs/BUILD_PLAN.md` wins on any conflict (C7.1).

**Read with:** `docs/BUILD_PLAN.md` §B9 Phases 6–10, §B10.4 (Landing Autopilot), §B10.5 (SmartASS),
§B11, §B12.5a, §B12.8 · `docs/MECHJEB_MISSION_TUNING.md` §6, §7.1–§7.4 ·
`docs/AUTOPILOT_RECOVERY_AUDIT.md` (R1) §5.1–§5.2 · `docs/FLIGHT_CORPUS_ASSESSMENT.md` §5 ·
`docs/PHASE_5_UNDOCKING_DEPARTURE_RESEARCH.md` · `docs/PHASE_6_DEORBIT_ENTRY_SPLASHDOWN_RESEARCH.md`.

---

## 0. What was read, and what it was

| File (at `8b81816^`) | Size | R1 verdict | Regime | Flight status |
|---|---|---|---|---|
| `plugin/src/ReturnControl.cs` | 22,827 B | RECOVER-CODE | RSS-RO | ❌ **NEVER FLOWN** |
| `plugin/src/pure/Departure.cs` | 9,847 B | RECOVER-CODE | n/a | ❌ **NEVER FLOWN** |
| `plugin/src/pure/Chutes.cs` | 7,535 B | RECOVER-CODE | RSS-RO (RealChute) | ◐ *"partially — chute descent recorded in aborts"* ⚠ **see §4.4 — that recording is not in the repo** |

`pure/Terminal.cs` was **not** read into this extract beyond R1's row: R1 §5.1 verdicts it
RECOVER-REFERENCE because its own text says *"⚠ THESE ALTITUDES ARE KERBIN'S, NOT THE REAL DRAGON'S"*. Its
**ordering argument** — drogues out → engines proven lit → *then* cut — is worth keeping and is recorded in
§4.5; **every altitude in it must be re-derived for RSS** and none is repeated here.

**Architecture in one line.** One glue file with three independently-dispatched entry points —
`TickDeparture`, `TickDeorbitEntry`, `TickChutes` — each driving a pure FSM (`Departure.Guide`,
`Entry.Guide` + the shared `DeorbitBurn`, `Chutes.Sequence`). The phase-to-phase hand-offs are not internal:
each leg ends by calling `CrewProcedureOps.PhaseComplete()` and the conductor dispatches the next.

---

## 1. ⭐ The return as an ordered phase list — the spec T21's sequencer is built against

**This is what W18 exists for.** The order below is the only end-to-end ordering of the return this project
has, and **T21's sequencer has to reproduce it whatever flies it**. Each row states the state, the trigger
that leaves it, and which §B9 phase / MechJeb module now owns it.

| # | State | Left when | §B9 phase → what owns it now |
|---|---|---|---|
| 1 | **Undock** — `ModuleDockingNode.Undock()` on the first node with an `otherNode`; then **two tiny separation pushes** straight off the port, along `+` relative position (away from the station) | relative range ≥ **`SepStandoffM` = 40 m** | **P6** — sequenced vessel action (`Actuator`/`FlightCommands`), then SmartASS backout |
| 2 | **Depart0** — "up-and-over": a CW two-impulse hop to an offset aim **above and behind** that clears the Keep-Out Sphere | range to the aim ≤ `max(0.15 × ‖aim‖, 5 m)` | **P6** — `OperationApoapsis`/`Ellipticize` + Node Executor |
| 3 | **Depart1** — CW hop, starting down and behind | same arrival test | **P6** |
| 4 | **Depart2** — CW hop, half way out | same arrival test | **P6** |
| 5 | **Depart3** — CW hop to the **stable co-elliptic point, 10 km below / 20 km behind** | same arrival test | **P6** |
| 6 | **Phasing** — a Hohmann-class **apsis-lower** (retrograde, `−` along-track), magnitude `\|Hohmann.Dv1(r1, r1 − PhasingLowerM, μ)\|`, to line the ground track up with the splashdown zone | Δv ≤ 0.01 m/s ⇒ **Departed** | ⚠ **§B9 does not name this phase** — `MECHJEB_MISSION_TUNING.md` §6 makes it explicit; `OperationSemiMajor` is the correct tool (period moves the ground track, not shape) |
| 7 | **Departed** — hold retrograde-ish and coast to the deorbit opportunity | conductor dispatch | **§6** (the pre-deorbit orbit) |
| 8 | **Trunk jettison** → **settle** (`SettleS` = 3 s) → **retrograde Draco deorbit burn**, closed-loop on **measured** periapsis, target `DeorbitTargetPeM` = **50 km**; the **nose shroud stays OPEN through the burn** (forward Dracos = attitude authority) and is **closed on completion** | measured Pe ≤ target | **P7** — `OperationPeriapsis` → Node Executor, **or** Landing Guidance targeted at the LZ (`MECHJEB_MISSION_TUNING.md` §7.1 (a) vs (b)). ⚠ The burn itself is **W17's** extract |
| 9 | **LZ select (once, latched)** — `LandingSiteScan.FindWaterSite` scans the descending ground track for the nearest reachable **open water** and hands it to the entry steering. Retries until found, then latches: *"you don't re-pick an LZ mid-entry"* | a site is found | **P7/P8** — `MechJebModuleLandingGuidance` + `TargetController`. ⚠ `MECHJEB_MISSION_TUNING.md` §7.4: the seven real splashdown sites have **no coordinates anywhere in the repo** and must not be invented |
| 10 | **Coast to the entry interface** — warp toward the interface crossing on an altitude ETA, dropping to 1× at `EntryInterfaceAltM + EntryWarpMarginM` | altitude ≤ interface + margin | **P7→P8** — Warp Helper; ⚠ §6.2's warning applies: set the lead so warp stops **before** the burn, not on top of it |
| 11 | **Engage the CoM shifter Descent Mode — ONCE** (`AdjustableCoMShifter`, `ToggleMode` event) | one-shot latch | **P8** — ⛔ **a sequenced vessel action, not a MechJeb burn.** See §3.1 |
| 12 | **Entry** — hold the heat shield into the flow (**nose surface-retrograde, roll excluded**) and, if lifting, bank to σ with a roll loop | `Entry.Guide` reports `HandToChutes` | **P8** — SmartASS `SURFACE_RETROGRADE`, `force_pitch`/`force_yaw`. ⛔ **O8 (owner, 2026-09-03) settled this: attitude hold, NO active steering, is the BASELINE.** The bank loop below is therefore **not** flight 1's design |
| 13 | **Drogues** — 2 drogues at **5,486 m** (18,000 ft) with a positive descent rate | main-deploy gate passes | **P9** — Landing Autopilot for prediction + triggering; Manual Chute Deploy screen for the crew steps |
| 14 | **Mains** — 4 mains at **1,830 m** (6,000 ft) with a positive descent rate | altitude ≤ sea level | **P9** |
| 15 | **Splashdown** — `Splashed`, or KSP reports `SPLASHED`/`LANDED` ⇒ mission complete | — | **P10** — MechJeb done, conductor releases control. Panel action: **CUT MAINS** |

**Six ordering properties worth keeping — they are the reason the order is what it is:**

1. **Undock, then clear the KOS, then descend.** The four departure hops are *the mirror of the approach*
   and every intermediate aim point sits **outside the 200 m Keep-Out Sphere** (§3.2). Order matters: the
   first hop goes **up and over**, not straight down.
2. **Ground-track phasing happens BEFORE the deorbit burn, not during it.** Step 6 exists so step 8 has a
   revolution whose entry track crosses the LZ. `MECHJEB_MISSION_TUNING.md` §6 calls this *"the real content
   of this phase"*.
3. **Trunk jettison precedes the deorbit burn** — so the burn, the coast and the entry all run at
   *trunk-detached* mass, drag and RCS authority.
4. **The nose shroud closes AFTER the deorbit burn, never at trunk sep.** The forward Dracos are the
   capsule's attitude authority during a retrograde burn; closing the shroud early removes them.
   ⚠ **This is a real sequencing trap and it is not obvious from the phase names.**
5. **The CoM shifter is a MODE, engaged once — never a steering actuator.** Banking is done by an RCS roll,
   not by toggling the shifter (§3.1).
6. **The chute sequence is STATE-BASED, not clock-based**, and each canopy is independently gated on
   measured altitude + descent rate, so a missed upstream step still deploys them (§4.1).

---

## 2. The constants, transcribed — ⛔ every one UN-CONVERGED

⚠ **§B16.8 ruling 2.** `ReturnControl.cs` and `pure/Departure.cs` never flew. Every number below is a
**best-guess starting value that no flight has ever confirmed**, and that marking is part of the value. The
file's own header says so: *"⚠ FIRST CUT (validate in flight): the departure/deorbit RCS translation sign
(`ForwardSign`), the deorbit target periapsis + cutoff, and the trunk/undock actuation are best-guess"*, and
*"the SIGNS (`RollSign`, `RollRefSign`/`CrossSign`) are the best-guess part that a flown entry confirms."*

### 2.1 `ReturnControl` — the glue seam

| Name | Value | **UN-CONVERGED — what is known about it** |
|---|---|---|
| `ForwardSign` | **−1.0** | KSP translation-frame convention (H = Z −1). The file marks it *first cut*; the **same value was flight-confirmed for `RendezvousControl` on flight 131412**, so it is *probably* right — but it was never exercised on this path. Does not transfer to MechJeb, which drives its own translation. |
| `AttitudeReadyDeg` | **5.0°** | Burn gate, copied from the rendezvous seam. Never flown here. ⚠ Flight 194334 (see `EXTRACT_RENDEZVOUS_CONTROL.md` §3.5) shows the failure mode of an attitude-gated burn on an unsettled vehicle. |
| `DeorbitTargetPeM` | **50,000 m** | The entry-corridor handle. ⚠ **Conflicts with the plan**: §B9 P7 and `MECHJEB_MISSION_TUNING.md` §7.1(a) call for *"a low or negative"* new periapsis, and §7.1's own note is that **flight-path angle is the real target and periapsis is only the handle** (§B11: FPA ≈ −1.4° to −1.6° **[EST]**). 50 km is neither derived from an FPA nor flown. |
| `SettleS` | **3.0 s** | Post-jettison settle before the burn. Never flown. |
| `RollKp` | **0.6** | Bank loop gain: `st.roll` per radian of bank error. Never flown. |
| `RollSign` | **+1.0** | ⚠ *"flip if the capsule banks the wrong way"* — **an explicitly unverified sign**, and the whole bank loop is off-baseline under O8 anyway. |
| `EntryInterfaceAltM` | **120,000 m** | ⚠ **Disagrees with §B11's documented value of 122 km (400,000 ft) [DOC]** by 2 km. Recorded as found; the plan's number wins (C7.1). |
| `CoastWarp` | **true** | Warp through the post-deorbit ballistic coast (up to ~half an orbit of dead time). |
| `CoastWarpFallbackHorizonS` | **5,400 s** | Bounded look-ahead when the orbital period is unusable. A guard, unstated provenance. |
| `EntryWarpMarginM` | **5,000 m** | Stop warping this far above the interface, so the entry is flown at 1×. Unstated provenance. |
| `UseSafeLandingSite` | **true** | Scan for open water rather than steering at `v.targetObject` (which is a station, not a splashdown site). |
| `LzGroundSamples` / `LzGroundStepS` | **130 / 45.0 s** | The ground-track scan window ≈ **97.5 min** ahead — about one orbit. Unstated provenance. |
| `LzMinGlideM` / `LzMaxGlideM` | **1.0e6 / 12.0e6 m** | The reachable-glide band (1,000–12,000 km downrange). ⚠ `LandingSiteScan`/`pure/SafeLandingSite.cs` is **W15's** line; this file only passes the band in. |
| `UseCourseCorrectEntry` | **false** | ⭐ **Records-first, default OFF.** The B8/T6 Newton bank correction *runs and records* the step + the sensitivity slope so a flown CSV can reveal the sign convention, and only *applies* it when this is on. **That CSV was never flown.** |
| `CcEntryPerturbDeg` / `CcEntryMaxStepDeg` / `CcEntryMaxBankDeg` / `CcEntryTickS` | **5.0° / 15.0° / 80.0° / 0.25 s** | Finite-difference perturbation, per-step clamp, absolute bank clamp, ~4 Hz recompute cadence. All unflown; the slope **sign** is explicitly *"a convention to read off the CSV first"*. |

### 2.2 `pure/Departure.cs`

| Name | Value | **UN-CONVERGED — what is known about it** |
|---|---|---|
| `SepStandoffM` | **40.0 m** | Back off to here before the first CW hop. From the research profile, never flown. |
| `SepDvMps` | **0.2 m/s** | Each of the two separation pushes ("spring-plus-Draco"). Never flown. |
| `TofFrac` | **0.25** | Hop transfer time as a fraction of the orbital period, floored at **60 s**. Same value as `Rendezvous.TerminalTofFrac`; unstated provenance in both. |
| `ArriveTolFrac` | **0.15** (`const`) | "Reached the aim" = within `max(0.15 × ‖aim‖, 5 m)`. Unstated provenance. |

Supplied by the glue into `DepartureInputs`, all **un-converged**: `KosRadiusM` = **200 m**,
`CoEllipticBelowM` = **10,000 m**, `CoEllipticBehindM` = **20,000 m**, `PhasingLowerM` = **10,000 m**.

### 2.3 `pure/Chutes.cs`

| Name | Value | Status |
|---|---|---|
| `DrogueAltM` | **5,486 m** (18,000 ft) | ✅ **Still live in the tree** — `pure/MissionPhase.cs:56 DrogueAltitude`; `MECHJEB_MISSION_TUNING.md` §7.3 already carries it. |
| `MainAltM` | **1,830 m** (6,000 ft) | ✅ **Still live** — `pure/MissionPhase.cs:57 MainAltitude`. |
| `DrogueSpeedMps` | **156.0** | Reference deploy speed — **informational only**, never used as a gate. |
| `MainSpeedMps` | **53.0** | Same — informational. |
| `TouchdownMaxMps` | **8.0** | Nominal splashdown 5–8 m/s. Never measured here. |
| `MinDescentMps` | **0.5** | Must actually be descending to deploy. A sanity gate, not a tuned value. |
| `AbortDrogueDwellSec` | **2.5 s** | Abort path: drogues ride this long to stabilise before the mains are armed. |
| `AbortMainFloorM` | **600.0 m** | Abort path: …or arm the mains immediately below this. |

⚠ **`MECHJEB_MISSION_TUNING.md` §7.3 already records the standing rule and it is not this document's to
change:** do **not** reconcile these with the Manual-Chute page's "(TBC)" altitudes — §14.1 records the two
as **intentionally different**, and neither is to be "fixed" to match the other.

---

## 3. The departure leg — the Δv and the Keep-Out-Sphere clearance rule

### 3.1 What actually flies the departure, and what does not

Two things in this leg are **sequenced vessel actions, not manoeuvres**, and MechJeb owns neither:

- **The undock itself** — walk `v.parts`, find the first `ModuleDockingNode` with a non-null `otherNode`,
  call `Undock()`.
- **The CoM shifter** — ⛔ the file's loudest warning, and an owner instruction: *"USE THE CoM SHIFTER
  CORRECTLY (user): the `AdjustableCoMShifter` Descent Mode is engaged **ONCE** before entry (`ToggleMode`
  event) — **a mode, not a steering actuator**; the shield is held into the flow and (as a refinement)
  banked by an RCS roll, **never by toggling the shifter**."* This is a §1.4 owner-sourced constraint on how
  the part is used, and it survives the move to MechJeb intact: `Actuator`/`FlightCommands` fire it, SmartASS
  holds the attitude, and nothing toggles the shifter to steer.

### 3.2 ⭐ The Keep-Out-Sphere clearance rule

**The rule, stated as the file states it:** every departure hop is a **CW two-impulse solve to an OFFSET aim
point**, chosen so that **a missed burn drifts CLEAR of the 200 m Keep-Out Sphere** rather than into it. This
is the *passive-abort* / corridor-safe discipline — the same rule the approach uses in reverse. *"Every
intermediate point sits OUTSIDE the KOS."*

**The four aim points**, in the station LVLH frame (x = radial, + up; y = along-track, + ahead), with
`below` = 10,000 m, `behind` = 20,000 m, `KosRadiusM` = 200 m:

| Hop | Formula | Resolves to | What it does |
|---|---|---|---|
| **Depart0** | `x = +0.05·below`, `y = −max(0.05·behind, 2·KosRadius)` | **(+500 m, −1,000 m)** | ⭐ **up-and-over**: rise first, clear the KOS *behind*. The `2 × KOS` term is the explicit floor — the first aim is **never** closer than twice the sphere radius. |
| **Depart1** | `x = −0.15·below`, `y = −0.20·behind` | **(−1,500 m, −4,000 m)** | start dropping below and behind |
| **Depart2** | `x = −0.50·below`, `y = −0.55·behind` | **(−5,000 m, −11,000 m)** | half way out |
| **Depart3** | `x = −below`, `y = −behind` | **(−10,000 m, −20,000 m)** | the stable co-elliptic point |

⚠ **The fractions 0.05 / 0.15 / 0.20 / 0.50 / 0.55 have no stated derivation** — they are a hand-chosen
ladder. The **property** they implement (monotonically outward, first hop up, every point outside 2×KOS) is
the transferable content; the fractions are not.

### 3.3 The departure Δv

| Burn | Magnitude | Source |
|---|---|---|
| Separation pushes (×2) | **0.2 m/s each** | `SepDvMps` — un-converged |
| Depart0–3 hops (×4) | **computed per tick** by `Cw.TwoImpulse` to the aim above, fired only while `BurnDvMps > 0.01` and `AllNominal` | no fixed magnitude exists in the file — it is whatever CW returns |
| Departure phasing burn | **`\|Hohmann.Dv1(r1, r1 − 10,000, μ)\|`** — a retrograde apsis-lower of 10 km | `PhasingLowerM` — un-converged |

⇒ **The file records no total departure Δv budget.** The only fixed numbers are the two 0.2 m/s pushes and
the 10 km phasing drop; everything else is solver output. §B9 P6's tuning note — *"departure Δv small; keep
clear of the Keep-Out Sphere"* — is consistent with that, and this file adds **no measured figure** to it.

⚠ **`MECHJEB_MISSION_TUNING.md` §6.1 disagrees with the phasing drop in principle**: it says real Dragon
*does not* substantially lower its orbit before deorbit, and §6.3's gotcha is *"do not lower the orbit to
save deorbit Δv."* The deleted design's 10 km lower is a **ground-track** move, not a Δv saving — but the
plan's tool for that job is **`OperationSemiMajor`** (period), not an apsis-lower. **The plan wins (C7.1);
recorded here only so the disagreement is visible.**

---

## 4. The chute schedule — and the one leg with recorded data

### 4.1 The nominal sequence

**State-based, not clock-based** — *"a crew-safety backstop that fires on the MEASURED altitude + descent
rate regardless of what the rest of the sequence thinks."*

| Gate | Condition | Result |
|---|---|---|
| **Drogues** | `alt ≤ 5,486 m` **and** `descentRate > 0.5 m/s` | deploy **2 drogues** — stabilise + slow through transonic. Reference speed ~156 m/s (informational) |
| **Drogue → Main state** | the main gate passes | advance the FSM |
| **Mains** | `alt ≤ 1,830 m` **and** `descentRate > 0.5 m/s` | deploy **4 mains** — slow to ~5–5.5 m/s, safe on **3 of 4**. Reference speed ~53 m/s (informational) |
| **Splashdown** | `alt ≤ 0` | `Splashed`, touchdown speed reported; nominal **5–8 m/s** |

Mains only advance **after** drogues (a sequence guard), but **each canopy is independently gated on
measured state, so a missed upstream step still deploys it.** That is the safety property, and it is worth
keeping whatever triggers the chutes.

### 4.2 The RSS-RO / RealChute specifics

- **Arm each canopy exactly ONCE.** ⛔ *"RealChute arming is idempotent, but re-invoking deploy every tick
  **reset its inflation** (the abort 122 m/s bug)"* — so the controller latches `rDroguesArmed` /
  `rMainsArmed` in addition to whatever `Actuator.DeployChutePart` does. **This is a real, observed RealChute
  behaviour and the single most important RO-specific note in the file.**
- **RealChute holds each canopy to its own altitude envelope.** Arming early is therefore safe: the part
  auto-predeploys/deploys within its own band (drogues high, mains low), so the sequencer *"doesn't have to
  wait for the nominal main altitude"* itself.
- **Drogues and mains are independent parts** — no in-sim entanglement, so mains can deploy underneath
  drogues that are still out.

### 4.3 The abort sequence — a compressed schedule, and why

⛔ **In an abort — especially a pad or low ascent abort — there is not the altitude budget for the nominal
18,000 ft → 6,000 ft drogue-then-main gap: waiting for `MainAltM` meant the capsule hit the ground first
(the original bug).** The abort path instead arms the drogues on the descent, then arms the mains after a
brief stabilise dwell (**`AbortDrogueDwellSec` = 2.5 s**) *or* immediately below **`AbortMainFloorM` = 600 m**.

⛔ **The drogues are NOT cut.** *"A mains-failed abort that had cut the drogues became a 122 m/s free-fall"*,
so the design keeps the drogues out as a backstop and lets RealChute deploy the mains underneath them. The
real Dragon **does** release its drogues; the file defers re-enabling that until in-flight main deployment
can be confirmed. **That confirmation never happened.**

⚠ **This abort schedule is `AbortControl`'s territory (W19), not T21's.** It is recorded here because it
lives in the same pure file and because the 122 m/s lesson is the reason the nominal path latches its arms.

### 4.4 ⚠ *"The one leg with real recorded data"* — a correction of fact

W18's register line says `pure/Chutes.cs` *"carries the one leg with **real recorded data**: chute descent
was recorded in the aborts (R1 §5.1, 'partially'), which is more than the rest of the return can say."*
**R1's row is accurate as history** — it reads *"partially — chute descent recorded in aborts"* against
commit `9223ff9` (the q/g aborts). **But the recording is not in the repo.**

`docs/FLIGHT_CORPUS_ASSESSMENT.md` examined all 13 surviving recorder CSVs and reports, plainly:

- *"No recording contains … an entry, or a chute deployment. Every `dock_*`, `entry_*` and `chute_*` column
  is blank in every file."*
- §5.2: *"Nothing about entry, chutes or splashdown. No `entry_phase`, `chute_phase`, `drogue` or `main`
  transition anywhere in the corpus."*
- Flights that completed a return / entry / splashdown: **0**.

⇒ **The chute descent was recorded, and the recording is gone** — lost with the RSS-RO flight corpus that
§B16.8 says has to be rebuilt from recorded re-flights. **What survives of it is the two lessons its
failures produced** (§4.2's inflation-reset latch and §4.3's 122 m/s no-cut rule), which are written into the
code's comments and are therefore preserved by this extract. **No chute *number* in §2.3 is backed by data
this project can still read.** Treat the whole return, chutes included, as **un-flown evidence** for tuning
purposes — the register line's *"more than the rest of the return can say"* is true of the *lessons*, not of
any recoverable measurement.

### 4.5 `pure/Terminal.cs` — the ordering argument, kept; the altitudes, not

R1 §5.1 keeps `Terminal.cs` as RECOVER-REFERENCE for **one** reason: its ordering argument — **drogues out
→ engines proven lit → then cut**. That is a safety sequence (never give up the working decelerator until
the replacement is demonstrably working), and it is the same argument §4.3's no-cut rule reaches from the
other direction. ⛔ **Every altitude in that file is Kerbin's, by its own admission, and must be re-derived
for RSS.** None is reproduced here. It stays RECOVER-REFERENCE and is **not** made live to satisfy this line.

---

## 5. What this line records for other tasks

### 5.1 ⚠ `UndockOps` and `DeorbitOps` are **T21's** to flip — this line flips nothing

W11 gave both facade names to W18 as two sequenced increments. **§B12.5a now gives both to `T21`**, the
conductor increment that flies §B9 Phases 6–10. §B12.5's one-property-per-increment rule rides along:

- **T21 increment 1** — the departure leg → **`UndockOps.Engaged` / `.Note`** goes live (§B9 Phase 6:
  SmartASS backout + the small `OperationApoapsis`/`Ellipticize` departure burns → Node Executor, clear of
  the KOS).
- **T21 increment 2** — the return leg → **`DeorbitOps.Engaged`** goes live (§B9 Phase 7:
  `OperationPeriapsis` → Node Executor; then P8 attitude hold under **O8**, and P9 chutes).

**Never both in one step.** G6 has already re-pointed both W4 reason comments in `_AutopilotStub.cs`; **W18
did not touch them**, and no facade property changed.

### 5.2 ✅ The entry-state-bus design decision is **RETIRED, not deferred**

W11's hardest Wave E finding was that `Steering.cs` was this file's **entry state bus** — 13 distinct
members, seven of which are not an attitude channel at all but **bank/footprint measurement**:
`PredictDownErrAtBank`, `MeasuredBankRad`, `MeasureBc`, `LastSigmaRad`, `FootprintError`, `EntryLoverD`,
`SetSplashTarget`. R1 §5.2 calls that measurement `EntrySteering`'s own job, parked in the one file §B12.8
rider (b) says **never comes back**, and W11 required W18 to decide where it lives.

**With nothing restored there is no state to re-home, so the decision is retired here in the open.** Where
the measurement lives now:

| Deleted `Steering` member | Where the job goes |
|---|---|
| footprint prediction, `PredictDownErrAtBank`, `FootprintError` | **MechJeb Landing Guidance / `MechJebModuleLandingPredictions`** (§B10.4) — it *is* the descent predictor |
| `MeasureBc` (ballistic coefficient) | Landing Guidance's own model; §B16.8 separately requires the recorder to capture density, Mach, drag accel, mass and an unpowered-phase marking for any re-derivation |
| `MeasuredBankRad`, `LastSigmaRad`, `EntryLoverD` | ⛔ **nowhere at baseline** — O8 settled that flight 1 is attitude hold with **no commanded bank**, so there is no bank to measure. If an off-target bank increment is ever built, the method is `pure/Entry.cs`'s, as recorded by **W16** |
| `SetSplashTarget` | `MechJebModuleTargetController` / Landing Guidance's site input. ⚠ §7.4: the seven splashdown sites have **no coordinates in the repo** and must not be invented |

**This is a design decision, stated, not a quiet edit.** It is W18's declared output and it changes no file
outside this one.

### 5.3 Cross-references

- **W17** (`DeorbitBurn.cs`) owns step 8's burn — this extract records only *where it sits in the order*.
- **W16** (`EntrySteering.cs`) owns the L/D prior and `pure/Entry.cs`'s method — the fallback home named in
  §5.2 above.
- **W15** (`LandingSiteScan` / `pure/SafeLandingSite.cs`) owns the open-water scan of step 9.
- **W19** (`AbortControl.cs`) owns the abort chute schedule of §4.3.
- ⛔ `git status` shows **no `.cs` file touched** by this task.

---

## Open questions for the owner

### Q1 — This extract went to `docs/EXTRACT_RETURN_CONTROL.md`, not to `MECHJEB_MISSION_TUNING.md` §6/§7.3–§7.4

**Situation.** W18's register line says *"Where it goes: `docs/MECHJEB_MISSION_TUNING.md` §6 (undock/departure)
and §7.3–§7.4 (chutes/splashdown)."* The owner-authorised batch instruction of **2026-09-04** instead directed
one self-contained `docs/EXTRACT_<name>.md` per task, explicitly so that a batch stopping mid-way does not
leave a shared document half-written. This session followed the batch instruction and **did not edit
`MECHJEB_MISSION_TUNING.md`** (C1.11 — declared outputs only). The same question was raised by **W20**; it is
repeated here so each line carries its own record.

**Options.**
1. **Leave it as it stands** — the extract is self-contained and reachable through `INDEX.md`.
2. **Open one small [S] register line covering the whole batch**, to fold each extract's tuning-relevant
   sections into `MECHJEB_MISSION_TUNING.md` (§3 from W20, §6/§7.3–§7.4 from W18, §7.1 from W17, §7.2 from
   W16), markings intact, once all five extracts exist. *(This chat's recommendation.)*
3. **Move each extract wholesale into `MECHJEB_MISSION_TUNING.md`**, leaving pointer stubs (nothing deleted,
   C1.16).

**Recommendation: option 2** — one merge line for the batch rather than five, done after the batch is
complete, when the half-written risk has passed.

### Q2 — Three of this file's numbers disagree with the plan. Recorded, not reconciled — should they be?

**Situation.** Reading the deleted file against `BUILD_PLAN.md` / `MECHJEB_MISSION_TUNING.md` surfaced three
disagreements. This extract **recorded** each and applied C7.1 (the plan wins) without changing anything:

1. **Entry interface**: the file says **120 km**; §B11 says **122 km / 400,000 ft [DOC]**.
2. **Deorbit target periapsis**: the file targets **+50 km**; §B9 P7 / §7.1(a) call for a **low or negative**
   periapsis chosen from the entry **FPA** (§B11: ≈ −1.4° to −1.6° **[EST]**).
3. **The departure phasing burn**: the file lowers the orbit **10 km** to phase the ground track; §6.1 says
   Dragon does **not** substantially lower before deorbit and §6.3 says do not lower it, with
   **`OperationSemiMajor`** (period) named as the right tool for ground-track walking.

**Options.**
1. **Leave all three recorded-only**, as now — they are properties of a deleted file, and the plan already
   states the correct values. *(This chat's recommendation.)*
2. **Open an [S] line to add explicit "the deleted code used X, the plan says Y" notes** into
   `MECHJEB_MISSION_TUNING.md` §6/§7, so a tuning session cannot pick up the wrong number from this extract.
3. **Treat (1) as a plan erratum** and check whether 120 km is right and §B11's 122 km is the round-number
   restatement — an owner call, since §B11 is **[DOC]**-marked.

**Recommendation: option 1.** All three are already resolved by C7.1, and the deleted file has no authority
against the plan. Option 3 is only worth doing if the owner has reason to doubt §B11's `[DOC]` marking —
which a build chat cannot assess.

### Q3 — `pure/Chutes.cs`'s recorded chute descent no longer exists. Does the return count as fully un-flown?

**Situation.** §4.4: R1 §5.1 marks `Chutes.cs` *"partially — chute descent recorded in aborts"*, and W18's
own register line calls it *"the one leg with real recorded data."* Both are accurate about history, but
`FLIGHT_CORPUS_ASSESSMENT.md` establishes that **no surviving CSV contains a single chute transition** — the
recording went with the lost RSS-RO corpus (§B16.8). What survives is the *lessons* the failures produced
(the RealChute inflation-reset latch; the 122 m/s no-cut rule), not any measurement.

**Options.**
1. **Treat the whole return — chutes included — as UN-FLOWN for tuning purposes**, and say so wherever the
   "partially flown" marking appears. *(This chat's recommendation, and what §2.3 / §4.4 above already do.)*
2. **Keep the "partially" marking** on the strength of R1's row, on the basis that the code was corrected by
   real flights even though the data is gone.
3. **Add the missing chute descent to §B16.8's re-fly list** as an explicitly named recording to recapture.

**Recommendation: option 1, and option 3 alongside it** — they are compatible. A marking that says "flown"
against data nobody can read is exactly the trap C1.16 and §B16.8 were written to prevent; and a named
re-fly item costs nothing and closes the gap. Both are owner calls, not this chat's.
