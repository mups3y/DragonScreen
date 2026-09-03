# The booster steering law — the C1.15 mod-first search, and why W24 stopped here

> **[SPEC — evidence + open owner call].** Produced by **W24** (2026-09-04) as **STEP 1** of its own brief:
> *"Before writing any control law, do the documented mod-first search and record it in your deliverable…
> ⛔ If TCA (or anything else) can serve, STOP AND ASK."*
>
> **This document decides nothing.** It reports what the repo's own evidence says about
> **ThrottleControlledAvionics (TCA)** as a source for the booster's steering law, and it stops. Adopting or
> rejecting a second mod dependency is an **owner call** — `docs/BUILD_PLAN.md` §B16.5, verbatim:
> *"vendoring or depending on any second mod is an OWNER call, never a build-chat one."* C1.12 forbids this
> chat from making it either way.
>
> **Source-of-truth order:** `docs/BUILD_PLAN.md` wins on any conflict (C7.1).
> **Sources used:** in-repo only (C7) — `docs/reference/craftdump.csv`, `docs/MODS_HARVEST_2.md`,
> `docs/reference/INSTALLED_MODS.md`, `docs/FLIGHT_CORPUS_ASSESSMENT.md`, `docs/AUTOPILOT_RECOVERY_AUDIT.md`,
> `docs/ATTITUDE_CONTROL_RESEARCH.md`, `docs/BUILD_PLAN.md`, and the tree itself. **The TCA DLL, the KSP
> install and TCA's upstream repository were not read** — C7 puts all three off-limits as build sources.

---

## 0. Headline

| | |
|---|---|
| Is TCA installed? | **YES — and it is evidenced from inside the repo**, not assumed (§1) |
| Is `ModuleTCA` on this craft? | **YES, on two parts** — the Dragon pod **and** `TE.19.F9.S1.Interstage`, one shared TCA group (§1) |
| Does `docs/reference/INSTALLED_MODS.md` list it? | **NO.** M1's register omits TCA entirely. Confirmed, and logged as its own register line (§5) |
| Can TCA's **T-SAS** serve as the booster steering law? | **Cannot be ruled out — and cannot be ruled in from this repo.** It covers the powered phases in principle, covers neither engines-off phase, and its ability to command an **unfocused** vessel is unknown and unknowable under C7 (§2, §3) |
| Can TCA's **VSC + radar altimeter** serve the landing phases? | **NO, on in-repo evidence** — the radar altitude is not a gap (KSP supplies it and `BoosterHost` already reads it) and the VTOL hover law is the wrong instrument for a stage that cannot hover (§2b) |
| Can TCA's **Horizontal-Speed Control** serve? | **The one capability with no in-tree counterpart** — but it needs an aim point, which does not exist yet (**W25**) (§2c) |
| Does adopting TCA collide with a settled decision? | **YES, with three** — §B16.1, §B12.7 and the non-interference guarantee (§4) |
| **Outcome** | **W24 STOPS AT STEP 1.** STEP 2 (build the law) and STEP 3 (flip `BoosterHost.Actuate`) are **not** started. Two questions to the owner in `## Open questions for the owner` |

---

## 1. Is TCA installed, and is it on this vehicle? — YES, twice over, and the brief's citation needs one correction

**What was searched.** `docs/reference/INSTALLED_MODS.md` (the C1.15 register) first, as the rule requires;
then, because W24's own brief warns that register is known incomplete, the repo's tier-1 vehicle evidence:
`docs/reference/craftdump.csv` (W0's live part-module dump of the flying stack) and the 16 `.craft` files in
`docs/reference/`.

**Result — `INSTALLED_MODS.md` does not mention TCA.** Searched for `TCA`, `ThrottleControlled`,
`Avionics`, `T-SAS`: zero hits in that file. The brief's warning is correct.

**Result — the craft dump proves it anyway.** `docs/reference/craftdump.csv` carries **42 `ModuleTCA` rows**,
split evenly across **two parts**:

| Part | Rows | `GroupMaster` | `TCA_Active` | TCA group `GID` |
|---|---|---|---|---|
| `TE.18.DRAGONV2.POD` (the **Crew Dragon**) | 21 | **True** | False | `b90d60bb01ee4e448523168699cacccb` |
| `TE.19.F9.S1.Interstage` (a **booster** part) | 21 | False | False | `b90d60bb01ee4e448523168699cacccb` — **the same group** |

Both rows read `enabled=True isEnabled=True moduleIsEnabled=True`, with the full TCA surface present
(`ActivateTCA`, `ShowGroup`, `ToggleTCA`, `onActionUpdate` "Update TCA Profile"). A part-module dump taken
from the running game is §1.4 tier-1 evidence: **TCA is installed and its module is instantiated on this
stack.** `docs/MODS_HARVEST_2.md`'s own opening corroborates it independently — *"the user added
ThrottleControlledAvionics, KerbalEngineer, ModularFlightIntegrator"*, 2026-08-27.

**⚠ Correction 1 to W24's brief (citation only; the substance holds).** The brief cites *"`ModuleTCA` in
`New Crew-2.craft`"*. **There is no `New Crew-2.craft` in this repo**, and **none of the 16 `.craft` files
contains `ModuleTCA`** — searched all of them. The module is not saved into the craft file; it is applied at
load time (TCA patches command parts), which is exactly why it appears in the *dump* and not the *file*. The
claim is true; the citation is `docs/reference/craftdump.csv`, not a craft file.

**⚠ Correction 2, and this one is substantive.** The brief frames TCA as a booster question. **It is not.**
`ModuleTCA` sits on the **Dragon pod as the group master**, in the same TCA group as the booster-side
interstage. Whatever is decided about TCA is decided about **both vessels at once**, which is the whole point
of §4 below.

**And the booster-side part is not incidental.** `TE.19.F9.S1.Interstage` contains the `.S1.` marker
(`pure/VehicleParts.cs:9`), so `VehicleParts.IsBooster` returns true for it — it is one of the very parts
`pure/BoosterHostPlan.Select` uses to *identify* the booster vessel after separation. The TCA module is on
the identifying part of the vessel W24 exists to fly.

---

## 2. Candidate-by-candidate: can any TCA capability serve? — assessed against the six phases

`pure/BoosterDescent.Guide()` flies six states — **Flip · Boostback · Coast · EntryBurn · AeroDescent ·
LandingBurn** — and hands the host a unit `AimForward` in every one of them. Each TCA capability named in
`docs/MODS_HARVEST_2.md` §1d is assessed against that, phase by phase.

### 2a. T-SAS (`Modules/AttitudeControl.cs`) — the named candidate. **Right shape, half the coverage, one fatal unknown.**

**What it is** (`MODS_HARVEST_2.md` §1d, the only in-repo description): *"controls the attitude of the **total
thrust vector**, not the nose… Use for gimbal/differential burns so the THRUST axis, not the reference
transform, tracks the guidance aim."*

**✅ The concept is right, and the tree already agrees with it.** `pure/BoosterDescent.cs`'s own contract
defines `AimForward` as *"the direction THRUST PUSHES the vehicle"*, and `src/BoosterHost.cs` deliberately
feeds `ReferenceTransform.up` as the vehicle's facing *"the direction thrust pushes a bottom-engined stack —
the same convention `AimForward` uses."* The guidance is **already expressed in thrust-vector terms**. So
T-SAS's idea is not a new instrument; it is the convention this tree already committed to. What is missing is
not the concept — it is **the loop that closes on it**.

**⛔ It covers, at best, three phases of six.** T-SAS controls a thrust vector. **Two phases have no thrust
vector at all:**

| Phase | Engines | What steers it | T-SAS applicable? |
|---|---|---|---|
| **Flip** | OFF (post-MECO slew) | RCS, against a lead-gated rate command (`AdvanceFlip`) | **No thrust to vector** |
| Boostback | ON | gimbal + differential octaweb thrust | Yes, in principle |
| Coast | OFF (ballistic, retrograde) | attitude hold only | No thrust to vector |
| EntryBurn | ON (3 engines) | gimbal, steered slightly off retrograde | Yes, in principle |
| **AeroDescent** | **OFF** | **grid fins, at a capped, held AoA** (`pure/GridFin.cs`) | **No thrust to vector** |
| LandingBurn | ON (centre) | gimbal + a terminal AoA lean | Yes, in principle |

§B16.2 makes **AeroDescent** the phase where **grid fins are the primary descent authority**, and
`MODS_HARVEST_2.md` records **no grid-fin capability in TCA** anywhere. So even on the most generous reading,
T-SAS is the powered half of a law whose unpowered half still has to be written — and the unpowered half is
the one that carries the `|AoaDeg| <= AoaCapDeg` contract W24 is required to never violate.

**⛔ AND THE FATAL UNKNOWN: does TCA command an UNFOCUSED vessel?** §B16.7 is unconditional — *"FOCUS NEVER
LEAVES THE UPPER STAGE"*; the booster lands **unfocused**. W23 proved *our own* command path reaches a
loaded, unpacked, non-active vessel (two flights, `8fd533d` and `Crew-2_20260829_144114`). **Nothing in this
repo says whether TCA's own module does.** `MODS_HARVEST_2.md` does not address it; the DLL, the install and
TCA's upstream source are all off-limits (C7). **This is decisive and it is unresolvable here:** if TCA runs
only on the active vessel, T-SAS cannot fly this booster at all, and no amount of design work changes that.
Answering it needs glass time — a separate owner gate (§0).

**Verdict: NOT REJECTABLE, NOT ADOPTABLE.** → owner call, Q1.

### 2b. Vertical-Speed Control + radar altimeter — **REJECTED, on in-repo evidence, for two independent reasons**

`MODS_HARVEST_2.md` §1d offers these as *"VTOL soft-touchdown law + terrain-relative altitude via a built-in
radar… Directly useful for the booster hoverslam final descent + touchdown-height."* Assessed:

1. **The radar altimeter is not a gap at all.** True height above the deck comes from KSP's own
   `vessel.radarAltitude`, and `src/BoosterHost.cs` **already reads it** (`double alt = v.radarAltitude;`,
   fed to `BoosterInputs.AltitudeM` and to the hoverslam solve). Under §14.4(e) step (0) this is tier-1 real
   state, not a not-yet-modelled quantity — **no mod is owed for it.**
2. **The VTOL law is the wrong instrument for this plant, and the plan has already rejected its class.**
   `pure/Hoverslam.cs`'s own opening states the physics: *"The stage cannot hover — even one Merlin at ~40%
   min throttle out-thrusts the near-empty stage — so landing is a HOVERSLAM: one continuous max-thrust brake
   timed to reach v=0 exactly at h=0."* A vertical-speed **hold** law presumes throttle headroom this vehicle
   does not have, on an engine carrying `ignitions = 1`. This is the identical reasoning §B16.5 already used
   to reject MechJeb's landing autopilot — *"assumes a freely-throttleable lander with unlimited relights"*.
   **`pure/Hoverslam.cs` already solves the real problem and is in the tree.**

### 2c. Horizontal-Speed Control — **the one capability with no in-tree counterpart; and it is blocked on W25, not on W24**

*"Null horizontal speed by tilting the thrust vector — booster lateral control."* This is genuinely not in the
tree: `BoosterDescent`'s LandingBurn holds a **terminal AoA lean** (`TerminalAoaBiasDeg` / `TerminalAoaMinDeg`
/ `TerminalAoaMaxDeg`, all `[UN-CONVERGED]`) rather than closing a loop on a measured horizontal velocity.

**But it cannot be used yet regardless of the TCA decision.** Nulling horizontal speed *relative to a landing
target* needs a target, and `src/BoosterHost.cs` records that **there is no aim point in the tree** — every
target-derived input is supplied as zero and the FSM's own refusals fire honestly. That is **register W25**,
which is TODO. So this capability is not a W24 blocker in either direction; it is noted here so the finding is
not lost, and it belongs to W25's scope if the owner wants it pursued.

### 2d. Everything else in TCA — already taken, the right way, years of this project ago

`MODS_HARVEST_2.md` §1a/§1b's **engine + RCS thrust-limiter balancing solver** is TCA's headline technique.
**It is already in this tree** — as **our own pure code**, with the method credited and no dependency:

- `plugin/src/pure/ThrustBalance.cs` — header: *"The core both P0 balancers share (docs/MODS_HARVEST_2.md §1,
  distilled from TCA's EngineOptimizer.cs / RCSOptimizer.cs)"*, implemented as an iterative torque-nulling
  projected descent, pure and headless-tested.
- `plugin/src/pure/RcsBalance.cs`, `plugin/src/pure/DiffThrottle.cs` — its two consumers.

**This is the precedent that matters most to Q1.** `MODS_HARVEST_2.md`'s own standing method says it in one
line: ***"we take the math/decision logic, never the plumbing"***, and *"Actuate by DIRECT part control,
always"*. Taking T-SAS the same way — the idea that the loop closes on the thrust axis rather than the
reference transform — costs **no dependency, no licence question and no owner gate**, because it is
indistinguishable from writing our own law. That is option 1 of Q1.

### 2e. Any other installed mod?

Searched the whole of `docs/reference/INSTALLED_MODS.md` §1 for anything that could supply a booster attitude
control law: **MechJeb2** — ⛔ excluded twice over (§B16.1 forbids a `MechJebCore` on the booster; O2 forbids
using the MechJeb that flies the mission; §B16.5 restates both). **FAR** — an aero model, not a controller
(its coefficient interface is an *input* to a law, per `MODS_HARVEST_2.md` §4). **KER** — readouts only.
**TestFlight, RealFuels, TAC-LS, Kerbal Konstructs, TundraSpaceCenter, Fossil Industries, Kartoffelkuchen,
RealismOverhaul, TundraExploration, RasterPropMonitor, FreeIva, SCANRPMStorage, SpaceXSuits** — none is a
controller. **KSPCommunityFixes** is relevant but not as a candidate: `ATTITUDE_CONTROL_RESEARCH.md` §4
records that it *fixes* KSP's `GetPotentialTorque`, which is what makes a live authority estimate trustworthy
— an **input** any law we write would use, not a law. **No candidate but TCA.**

---

## 3. What the flight evidence says the law has to survive — recorded here so it is not lost when W24 resumes

From `docs/FLIGHT_CORPUS_ASSESSMENT.md` §3 (S76, 2026-09-04), reported not prescribed. This is the design
brief whichever way Q1 goes, and it corrects the inherited folklore:

- **The ascent failure was a DIVERGENCE, not a limit cycle.** Body rates reverse sign only **2–3 times**
  (0.05–0.10 /s) across the whole S2 burn while `rate_yaw_dps` climbs **monotonically to 90 dps** (§3.1).
- **`act_yaw` is the saturating axis** — repeatedly ±1.00, `act_sat` p95 = 1.00, duty > 0.95 for **19–20%**
  of the burn. **`act_roll` never exceeds 0.09.** Roll under-control was never the ascent problem.
- **The commanded rate is what ran away, not the actuation.** `rate_cmd_rads` reached **3.41 rad/s** on
  DS-ASC-001 and **7.61 rad/s** on DS-ASC-002 against a measured rate that never exceeded 1.01 / 1.18 —
  *"throughout the burn the loop asks for a rate the stage never achieves."*
- **The authority estimate the loop used was physically impossible** — `angacc_pitch_auth` 37–39 rad/s²
  where the same metric post-fix reads **0.43**. A law that computes `maxAlpha = torque / MOI` and trusts it
  is only as good as that torque figure. Any law W24 writes must **bound its own authority estimate** and
  degrade when it is not credible.
- **The limit cycle is real but lives in the terminal rendezvous, in the ACTUATION not the error** (§3.2) —
  81–89% duty, ~1 reversal/s, while the pointing error sits at a 3.0–3.8° median.
- **⚠ The booster is a different plant and the corpus barely covers it.** R1 §7.5 is explicit — the ascent
  failure is *"ascent + Dragon-RCS attitude hold… A recovered booster controller is not condemned by this
  failure — and it is not exonerated by it either."* The one substantial booster recording (the DS-ASC-008
  probe, 341 s, EntryBurn→LandingBurn) is **saturated for 99.8% of its recorded life**, `rate_yaw_dps`
  spanning −92 to +104 dps, and it **did not land**.

---

## 4. Why TCA adoption is an owner call and not a build-chat one — three collisions and a licence

1. **§B16.1 — the settled architecture names the steering law as OURS.** Owner decision, 2026-09-03, via the
   overseer: *"OUR OWN COMPILED CORE WITH ITS OWN STEERING LAW… it is NOT a second `MechJebCore`, and a build
   chat must not attach one to the booster"*, with the owner's stated intent *"a perfect flip and boostback
   with no roll and no wasted movement."* Handing the steering law to TCA's controller **contradicts a settled
   decision**, which under C1.8 needs an explicit `OVERRIDE` typed by the owner — a higher bar than a
   dependency go.
2. **§B12.7 — direct part control.** *"The autopilot NEVER stages and NEVER fires an action-group binding to
   actuate the vehicle. It reaches the live PART MODULES and calls them."* Engaging `ModuleTCA` and letting it
   fly the stage is the opposite shape. `src/BoosterHost.cs`'s own ban list already forbids the neighbouring
   moves for the same reason.
3. **The non-interference guarantee W23 made structural.** W23's whole design is that the Dragon is excluded
   **three independent ways** and that the two autopilots *"must NOT interfere with each other's flights."*
   `ModuleTCA` is on the **Dragon pod, as the group master**, sharing a TCA group with the booster-side
   interstage (§1). A runtime TCA dependency reintroduces, by construction, a controller present on both
   vessels — the exact hazard the owner asked W23 to prevent.
4. **Licence — a check is owed and it is not this chat's to run.** DragonScreen is **GPL-3.0** (`LICENSE` at
   the repo root) and `NOTICE` records how the one existing third-party derivation (Neel Dandiwala's
   SpaceX-Dragon2-UI, Apache-2.0) was cleared: source named, licence stated, changes stated. **TCA's licence
   is recorded nowhere in this repo**, and C7 puts both the install and the upstream repository off-limits.
   Any vendoring needs that check done first, at owner level, exactly as the Dandiwala one was.

⚠ **And note what O2 does *not* decide.** W24's brief is right that O2's ban was on *the MechJeb flying the
mission*; it says nothing about TCA. TCA is blocked by §B16.5 and §B16.1, not by O2.

---

## 5. Logged, not done (C1.1 — noticed here, out of this task's scope)

- **`docs/reference/INSTALLED_MODS.md` omits ThrottleControlledAvionics**, and the omission has already cost
  once: `docs/MODS_HARVEST_2.md`'s recovered banner records that it *"is why ThrottleControlledAvionics was
  missed from the mod register"*, and W26's own entry names **M1** as damage from the deleted research. The
  evidence to add it now exists in-repo and is tier-1 (`docs/reference/craftdump.csv`, 42 `ModuleTCA` rows on
  two named parts, §1 above). **Not fixed here** — M1's file says in its own words that it is updated *"as a
  separate task"*, and W24's declared outputs do not include it (C1.11). Logged as its own register line,
  **W29**. ⚠ That line records that TCA is *installed*; it decides nothing about *depending* on it — that is
  Q1 below, and it is the owner's.

---

## 6. What W24 did NOT do, stated rather than left to inference

- **STEP 2 — the steering law is NOT written.** No new control code, no gains, no thresholds. STEP 2's own
  precondition — *"ONLY IF NO MOD SERVES"* — is not met, because §2a could not establish that TCA does not
  serve, and C1.12 forbids this chat from rejecting a dependency on its own authority.
- **STEP 3 — `BoosterHost.Actuate` is STILL `false`, and must stay false.** It is untouched. Flipping it is
  W24's to do *after* a steering law exists and its tests are green; arming a thrust path with no attitude
  path is flight **194334** (`8225df7` finding A1 — *"attitude diverges 2→85 deg, LOST in ~10 s — and its
  0-km burn kicks the upper stage"*). Nothing in the tree sets it.
- **No byte of `AttitudePilot.cs` / `AttitudeController.cs` / `pure/AttitudeLoop.cs` entered the tree** — R1
  §3.2's ⛔ RECOVER-REFERENCE-ONLY verdict is intact, and nothing was restored, quoted into code, or adapted.
- **No file outside this document and `REGISTER.md` / `docs/INDEX.md` was modified.**

---

## Open questions for the owner

### Q1 — Does the booster's steering law come from TCA, or is it ours? **W24 cannot resume until this is answered.**

**The situation.** W24's brief required a documented mod-first search (C1.15) before any control law, and
named TCA's **T-SAS** as the candidate — *"controls the attitude of the TOTAL THRUST VECTOR, not the nose"*,
which is the right shape for a gimballed booster. The search is §1–§2 above. What it found:

- **TCA is installed and `ModuleTCA` is on this stack** — evidenced in-repo, on the **Dragon pod (group
  master)** *and* on `TE.19.F9.S1.Interstage`, in **one shared TCA group**. So this is not a booster-only
  decision.
- **T-SAS's concept is already the tree's convention** — `BoosterDescent`'s `AimForward` is defined as the
  direction thrust pushes the vehicle. What is missing is the loop, not the idea.
- **T-SAS covers at most 3 of the 6 phases.** Flip, Coast and AeroDescent are engines-off; AeroDescent is
  grid-fin-steered and §B16.2 makes the fins the *primary* authority there. TCA has no grid-fin capability
  recorded anywhere in this repo.
- **One unknown is decisive and unresolvable here: does TCA command an UNFOCUSED vessel?** §B16.7 has the
  booster landing unfocused. W23 proved *our* path reaches a non-active unpacked vessel; nothing says TCA's
  does. Only glass time answers it.
- **Adoption collides with three settled decisions** (§B16.1's "its own steering law", §B12.7's direct part
  control, and W23's non-interference guarantee) **and owes a licence check** (§4).

**The decision needed.** Whether the booster steering law is written as ours, or TCA becomes a second mod
dependency. **This chat has decided neither** (C1.12; §B16.5: *"depending on any second mod is an OWNER call,
never a build-chat one"*).

**Options:**

1. **⭐ OURS, with TCA's METHOD borrowed — no dependency (RECOMMENDED).** W24 resumes at STEP 2 and writes the
   law fresh in `plugin/src/pure/`, taking from T-SAS only the *idea* that the loop closes on the **thrust
   axis** rather than the reference transform. **This needs no gate and no `OVERRIDE`** — it is exactly what
   `pure/ThrustBalance.cs` already did with TCA's `EngineOptimizer`, under `MODS_HARVEST_2.md`'s own standing
   method: *"we take the math/decision logic, never the plumbing."*
   **Why recommended:** it is the only option that satisfies §B16.1, §B12.7, §B16.5 and W23's non-interference
   guarantee simultaneously; it needs no licence check; it does not depend on the unresolvable
   unfocused-vessel unknown; it covers all six phases including the two TCA cannot; and it is the one option
   that delivers the owner's own stated intent — *"a perfect flip and boostback with no roll and no wasted
   movement"* — as a purpose-built law for one known plant rather than a general-purpose autopilot's.
   **Cost, stated honestly:** it re-enters the territory of the component that already failed. §3 above is the
   measured brief that failure left, and the mitigations belong in the design, not in this choice.

2. **ADOPT TCA AS A RUNTIME DEPENDENCY** (drive `ModuleTCA`/T-SAS on the booster). Needs **four** things
   before a line is written: (a) a C1.8 **`OVERRIDE`** of §B16.1's *"its own steering law"*; (b) an owner go
   for a second mod dependency (§B16.5); (c) a licence check like the Dandiwala one (§4); (d) **glass time**
   to answer whether TCA commands an unfocused vessel — a separate owner gate. And even granted all four, a
   grid-fin law for AeroDescent still has to be written by us. **Also inherits the non-interference risk:
   `ModuleTCA` is on the Dragon.**

3. **VENDOR TCA'S SOURCE**, pinned and privately namespaced, as §B12.1 does for MechJeb. Removes the
   runtime-dependency and Dragon-group problems, keeps (a) and (c) above, and drops (d). **But it is a larger
   piece of work than the whole of W24**, and C7 forbids this chat from fetching the source.

4. **DEFER W24** until a glass session can answer the unfocused-vessel question. Costs a flight window and
   leaves `BoosterHost.Actuate` false — the booster keeps falling ballistically, which is the current state.

### Q2 — The phase-plane deadband: DS-ASC-008 measured it working, and `70dc239` stripped it. Does the new law get one?

**The situation.** W24's brief flags this and forbids this chat from acting on it alone. The numbers, from
`docs/FLIGHT_CORPUS_ASSESSMENT.md` §3.2 — **DS-ASC-008 (the deadband build) against DS-ASC-007 (pre-deadband),
segment `RV/ApproachInit`:**

| Metric | DS-ASC-007 | DS-ASC-008 | |
|---|---|---|---|
| `act_yaw` reversals /s | 0.37 | **0.07** | −81% |
| `act_roll` reversals /s | 0.53 | **0.08** | −85% |
| `act_sat` duty | 0.771 | **0.463** | −40% |
| yaw duty > 0.05 (terminal window) | 0.86 | **0.18** | −79% |
| **MMH at end of flight** | **0.000** | **0.584** | the **only** recording in the corpus that did not run its tank dry |

**The deadband measurably helped.** It is also one of the inventions the owner ordered stripped at `70dc239`
— *"Everything invented by reading flight data and chasing your tail needs to go"* — and R1 §7.3 lists *"the
phase-plane hold deadband and the 1.5× hold-authority scale"* among the items already removed from
`AttitudeLoop.Axis`'s parameter list.

**The decision needed.** Whether the booster steering law may carry a deadband, and in what form.

**Options:**

1. **⭐ BUILD THE LAW WITH A MARKED, `[UN-CONVERGED]`, DEFAULT-ZERO DEADBAND PARAMETER (RECOMMENDED).** The
   mechanism exists and is greppable; the **default is zero, i.e. behaviourally identical to the stripped
   state**, so nothing is reinstated and the owner can enable it from `PluginData/tuning.cfg` with no
   recompile (the same shape as `BoosterHost.Actuate`). **Why recommended:** the evidence above is real but it
   is **Dragon-regime evidence** — an ON/OFF 0.4 kN RCS set holding attitude in terminal rendezvous. R1 §7.5
   says plainly that the booster *"is a different plant"*: gimbal authority proportional to a high,
   throttleable thrust, with grid fins as the primary descent authority. Carrying a number across that
   boundary is precisely the mistake §B16.8 ruling 2 exists to prevent. A defaulted-off seam keeps the option
   without importing the number — and the booster's **Flip** phase *is* RCS-only, so the mechanism may well
   earn its place there once a recorded booster flight exists.
2. **AUTHORISE A LIVE DEADBAND** in the new law, seeded from DS-ASC-008. Needs an explicit **`OVERRIDE`** of
   the `70dc239` strip directive (C1.8), and the seed value would still be `[UN-CONVERGED]` for the booster
   regime under §B16.8 ruling 2.
3. **NO DEADBAND, no seam.** The strip directive stands untouched and the law is written without the
   mechanism. Simplest and most faithful to `70dc239`; if the booster does chatter, it costs a rebuild and a
   flight to find out.

⚠ **Q2 is needed whichever way Q1 goes** — under Q1 option 1 it shapes the law W24 writes; under options 2–3
it shapes the grid-fin/unpowered half that still has to be written by us regardless.

---

## Appendix — how to reproduce the §1 evidence

```bash
grep -c "ModuleTCA" docs/reference/craftdump.csv                 # 42
awk -F, '/ModuleTCA/ {print $2}' docs/reference/craftdump.csv | sort | uniq -c
grep -rn "ModuleTCA" docs/reference/*.craft                       # no hits — applied at load, not saved
grep -rin "TCA\|ThrottleControlled" docs/reference/INSTALLED_MODS.md   # no hits — the §5 gap
```
