# Booster-recovery architecture — the two-vessel problem and the guidance method

**What this is.** The **architecture** research §B16 deferred: (1) how KSP can fly two vessels at once at
all, (2) which of the established methods fits §B16 and the owner's intent, (3) what the installed
environment gives us, (4) what all this changes in §B16, and (5) **the guidance method** — the algorithm and
the steering/timing laws that actually produce landing-zone accuracy, for **both** the RTLS and the
ASDS/droneship profile, described so it is implementable in C# on MechJeb's attitude + prediction modules.

**What this is NOT.** Not a tuning derivation (that is `MECHJEB_MISSION_TUNING.md` PHASE 2 / S48, and it
stands unchanged), not a plan edit, and not authority to build anything.

**Status / authority.**
- **Owner-directed RESEARCH task, 2026-09-03.** Research + doc only. **No code. No plan edit** —
  `docs/BUILD_PLAN.md` is FROZEN; this is a NEW file, not a §B edit. **No gate is opened or widened here**
  (C1.12). §5 below states what §B16 would need amending to say; **only a governance task can amend it.**
- **Web research was explicitly authorised for this task by the owner's directive** ("Use Explore subagents +
  WebSearch/WebFetch"). That is a per-task authorisation and does **not** change C7 — external URLs remain
  off-limits as build sources; everything load-bearing below is either in the repo or cited in §8.
- **Everything quantitative below is a TARGET or an ESTIMATE, not a measured result.** Nothing here has been
  flown by this mod. Estimates are marked **[EST]**.

**Source tiers (§1.4).** **[REPO]** = this repository, including its own git history (authoritative, C7.1) ·
**[T2]** = another user's method, marked and attributed, never presented as verified-real · **[DOC]** =
public documentation of the real vehicle or of a KSP API/mod · **[EST]** = arithmetic done here.

---

# 1. The concurrency problem — what KSP actually does

## 1.1 The mechanism: LOADED / UNLOADED, PACKED / UNPACKED

Three states, not two, and the distinction is the whole answer.

| State | What it means | Physics? | Control? |
|---|---|---|---|
| **UNLOADED** | *"doesn't even have its parts in memory and is just a single dot in space with no dimensions"* **[DOC]** | none — on rails | none |
| **LOADED but PACKED** | *"close enough to be loaded, but still far enough away that its full capabilities aren't enabled… unable to have its parts interact"* **[DOC]** | partial | **no** |
| **LOADED + UNPACKED** | fully simulated | full (incl. aero/heating) | **yes** |

The load-bearing sentence, from the kOS documentation of KSP's own behaviour **[DOC]**:

> *"KSP limits some features (like throttle control) to only vessels that are unpacked."*

Stock unpack range is a few km, so **a separated booster unloads within seconds and is then deleted when its
on-rails trajectory enters the atmosphere** (S48 §2.6 gotcha 7 **[REPO]**). That is the failure the whole
question is about.

## 1.2 The API fact — RESOLVED, and it was already resolved in this repo

**A non-active vessel that is LOADED + UNPACKED can be flown**, by registering a control callback on that
vessel (`Vessel.OnFlyByWire`). This is not folklore; it is settled by three independent lines of evidence,
and the repo's own (deleted, 2026-09-01) research already recorded all three:

- **kOS** documents the constraint as an *unpack* constraint, not an *active-vessel* constraint **[DOC]**.
- **BDArmory** flies many non-active craft simultaneously by hooking each vessel's control callback, and
  **requires PhysicsRangeExtender as a hard dependency** precisely because craft otherwise freeze at
  distance **[DOC]** — living proof that the callback drives a non-active *unpacked* vessel.
- **MechJeb** registers the same callback on its own part's vessel — the callback *is* the control path, and
  whether it fires depends only on the vessel being unpacked **[REPO]**, `BOOSTER_DUAL_FLIGHT_RESEARCH.md`
  (deleted in `8b81816`, read from git history for this task).

> **⇒ "Both fly at once" is achievable in principle.** The constraint is not the API. It is **distance** —
> and distance is a *physics* problem, not a permissions problem (§1.4).

The tree is already shaped for this and has been since before the deletion:
`plugin/src/RangeExtender.cs` is a **source port of PhysicsRangeExtender's method** — no hard dependency on
the mod — applying, for a range `R`, `VesselRanges.Situation(load = R, unload = 1.05·R, pack = 1.10·R,
unpack = 0.99·R)` across all seven situations to every loaded vessel, and restoring
`PhysicsGlobals.VesselRangesDefault` on disable. **76 lines, correct, and with zero callers.** `REGISTER.md`
already protects it: *"do NOT retire it: §B16 claims it."*

## 1.3 The distance arithmetic — the numbers that decide everything

The recovery window, from the repo's own Crew-2 record (S48 §2.1) **[REPO]**: stage separation **T+2:39**,
booster touchdown **T+9:30** ⇒ **W = 411 s**.

The droneship's downrange distance, computed from the repo's own surviving coordinates
(`PAD = 28.6084, −80.6043`; `BARGE = 32.787551, −76.644507`, `assess_flight.py:404`) **[EST]**:
**≈ 599 km** great-circle. (Consistent with the ~560 km the deleted `BOOSTER_GUIDANCE_DESIGN.md` recorded
for Crew-2 **[REPO]**.)

Upper-stage travel during that 411 s window, from MECO at ~2.3 km/s / ~67 km **[REPO]** — **all [EST]**:

| Upper stage during recovery | S2 travel in 411 s | Separation at touchdown, **ASDS** | Separation at touchdown, **RTLS** |
|---|---|---|---|
| **COASTS** (unpowered, kept loaded) | ~820 km | **≈ 220 km** | **≈ 820 km** |
| **KEEPS FLYING** (MechJeb burns to orbit; SECO ≈ T+8:47) | ~2100 km | **≈ 1500 km** | **≈ 2100 km** |

Against that, PhysicsRangeExtender's own warning **[DOC]**:

> *"You might experience some of the following effects when the range is extended > 100 km: vessel shaking,
> lights flickering, phantom forces, landed vessels colliding with the ground, etc."*

## 1.4 The consequence nobody can engineer around: **the floating origin**

KSP re-centres its coordinate frame on the **active** vessel. So the active vessel is always computed near
the origin at full precision; **the far vessel is the one that shakes.** Phantom forces are a function of
*separation*, not of the range setting.

> **⇒ Whoever holds focus gets the precision.** A metre-class hoverslam cannot survive phantom forces
> (the header of `RangeExtender.cs` already says so: *"the real risk to a precision hoverslam"* **[REPO]**).
> A vacuum PVG burn, which re-plans continuously, can absorb far more jitter. **Therefore, in any concurrent
> scheme, the BOOSTER must be the focused vessel and the upper stage must be the one at range.** The reverse
> assignment fails the deliverable.

This also explains the owner's own sizing rule recorded in `RangeExtender.cs` — *"say it's 500 km → set
600 km"* **[REPO]**: it is the **coasting-S2, ASDS** row of the table above, and it is coherent.

---

# 2. The four candidate methods, honestly costed

## A — CONCURRENT DUAL-FLIGHT (both vessels under power, our PRE at ~1500–2100 km)
Focus the booster; our booster core flies it. MechJeb keeps driving the **non-active** upper stage through a
second control callback, so the ascent never stops.

- **Gives:** exactly the owner's stated intent — two autopilots, two vessels, concurrently, one continuous
  timeline, one recording, and the crew can watch the landing.
- **Costs:** needs a physics range of **~1500 km (ASDS)** — **15× beyond where PRE documents phantom
  forces**. Unverified. Also unverified: whether MechJeb's ascent modules behave correctly on a vessel that
  is not `FlightGlobals.ActiveVessel` (the callback will fire; whether every module respects it is a
  different question, and MechJeb was not written for this).
- **New dependency:** none — `RangeExtender.cs` is ours.

## B — FOCUS-FOLLOWS-BOOSTER, UPPER STAGE COASTS (our PRE at ~600 km)
The design the owner already directed on 2026-08-29 **[REPO]**. PRE on before separation → focus the
booster → fly it down while the upper stage **coasts, kept loaded** (it does not go on rails and is not
deleted) → return focus at touchdown → the ascent **resumes** → PRE off.

- **Gives:** a real, flown, precision landing at **≈ 220 km separation (ASDS)** — barely 2× PRE's warning
  threshold, and the booster holds focus so the hoverslam runs at the floating origin.
- **Costs:** **MechJeb does not fly the upper stage during the recovery window.** The Dragon's ascent is
  suspended for ~411 s and then resumed from a suborbital, decaying arc. Whether PVG can still close the
  target orbit after that coast is **the single make-or-break unknown** — the deleted research flagged it in
  those words: *"the biggest unknown — a long coast from a suborbital MECO may not recover"* **[REPO]**.
- **New dependency:** none.

## C — FMRS-STYLE SPLIT-FLIGHT
FMRS (*Flight Manager for Reusable Stages*) generates a save point at separation; you fly one vessel, jump
back to the separation point, fly the other, and the timelines are reconciled **[DOC]**. *"You can do
SpaceX's Falcon 9 style launches and fly your first stage back to the launch site, while your payload
continues to orbit."* It is the community's standard answer, and the public kOS Falcon-9 RTLS script studied
in §6 lists it as **"(Recommended)"** **[T2]**.

- **Gives:** both flights at **full fidelity, zero physics risk, no range problem at all**. MechJeb flies the
  entire ascent uninterrupted; our core flies the entire recovery uninterrupted.
- **Costs:** the two are **sequential in wall-clock**, not concurrent — "both fly at once" becomes "both
  really happened, at different times at the keyboard". FMRS is **player-GUI-driven**: the conductor cannot
  drive the jumps, so they become a manual step in the flight procedure. And it manipulates saves.
- **New dependency:** ⛔ **YES — an OVERRIDE-level decision.** §B3's packaging decision covers **MechJeb
  only**, and per §4 below FMRS is **not** in the last in-repo snapshot of the installed environment.

## D — StageRecovery-STYLE AUTO-RECOVER
Recovers a dropped stage by rule (chutes / thrust / probe core), awarding funds and parts. **No flight
happens.** Fails the §B16 deliverable ("a REAL landing") outright. Recorded for completeness; not a
candidate. Also not installed (§4).

## E — "fly only within the stock physics window" — NOT VIABLE
The booster lands 599 km downrange (ASDS) or flies ~800 km back (RTLS). The stock unpack window is a few km.
There is no version of this that reaches a landing. Dismissed.

## The comparison

| | **A** concurrent | **B** coast | **C** FMRS | **D** StageRecovery |
|---|---|---|---|---|
| A real, flown landing | ✅ | ✅ | ✅ | ❌ |
| MechJeb continues on the upper stage | ✅ | ❌ (suspended) | ✅ (other segment) | ✅ |
| Truly simultaneous | ✅ | ⚠ one flies, one coasts | ❌ | ✅ (nothing flies) |
| Range needed **[EST]** | ~1500 km ⚠⚠ | ~220 km ✅ | n/a ✅ | n/a |
| Hoverslam at the floating origin | ✅ | ✅ | ✅ | n/a |
| New mod dependency | none | none | ⛔ OVERRIDE | ⛔ OVERRIDE |
| Conductor can drive it | ✅ | ✅ | ❌ manual | ✅ |
| Proven | ❌ | ❌ | ✅ | ✅ |

---

# 3. RECOMMENDATION

> ## Build ONE architecture; fly it in TWO stages. **B first, then A.**
> **`RangeExtender.Enable(rangeM)` is already parameterised — B and A are the same code at two range
> settings, differing only in whether MechJeb is suspended on the upper stage during the window.**

**Stage 1 — prove the landing (method B, ~600 km, ASDS).** Focus follows the booster; the upper stage coasts
loaded. This is the owner's own 2026-08-29 design, needs **no new dependency and no new gate**, keeps the
separation at ~220 km where the physics is closest to PRE's proven regime, and puts the hoverslam at the
floating origin where it can actually be accurate. Everything hard about §B16 — the five-phase guidance, the
per-engine 3→1 schedule, the ignition solution, grid-fin steering, the ignition budget — is exercised here.

**Stage 2 — deliver the owner's intent (method A).** Once the landing is proven, stop suspending MechJeb on
the upper stage and widen the range. This is a **one-line change in the same architecture**, and it is the
only option that delivers "two autopilots, two vessels, concurrently" as stated.

**⚠ The limitation, stated plainly, not papered over.**

- **Stage 1 does not give the owner what they asked for.** MechJeb does *not* fly the upper stage during the
  recovery; it holds, and the ascent resumes afterwards. Whether the upper stage can still reach its target
  orbit after a ~411 s suborbital coast is **unknown and unverified**, and if it cannot, Stage 1 buys a
  landing at the price of the Dragon's orbit that flight.
- **Stage 2 may simply not work.** ~1500 km is 15× beyond PRE's own documented trouble threshold, and the
  floating-origin precision loss at that separation is arithmetic, not a bug to be fixed. **If Stage 2's
  phantom forces prove fatal to the upper stage, the fallback is method C (FMRS) — and that is an
  OVERRIDE-level new dependency the owner must grant** (§7, O-B1).
- **Neither stage is verified.** The five flight-verify items the deleted research listed (H1 a–e) are still
  open and are restated at §7.2.

**What the recommendation is NOT.** It is not a decision. §B16.5's guidance choice (O2), the focus question
(O3) and the profile/aim point (O5) remain **owner decisions** (C1.12). This section says which way the
evidence points and what each option costs.

---

# 4. What is installed that helps — and a C7 flag

**C7 forbids reading the KSP install**, so the only in-repo evidence is the historical inventories. The last
full snapshot is `docs/DEPENDENCY_MATRIX.md` (*"installed GameData/ at KSP root, 2026-08-31"*), deleted in
`8b81816` and read from git history for this task **[REPO]**.

**Present, and relevant — HARDWARE only, as the owner said:**

| Mod | Class | What it gives booster recovery |
|---|---|---|
| **KerbalReusabilityExpansion** | TARGET | **grid fins / legs / cold-gas** — the actuators phases 4–5 steer with |
| **Space_X_barge_lander-2.0** | TARGET | **the ASDS droneship** — the `BARGE` aim point; `VehicleParts.DroneshipMarker = "Droneship"` matches it |
| KerbalKonstructs · CanaveralPads · ModularLaunchPads · TundraSpaceCenter | TARGET | the pad and the **RTLS landing zones** |
| KSPWheel | INFRA | the landing legs |
| FerramAerospaceResearch (FAR) | ENVIRONMENT | the aero the descent steers in; supplies the grid-fin control surface |
| RealFuels / RO / SolverEngines / TestFlight | ENVIRONMENT | ignitions, ullage, throttle floors, spool — the constraints §B16.3 is about |
| KerbalJointReinforcement | INFRA | incidentally relevant to method A: it is what a far vessel's phantom forces have to survive |

**⛔ Nothing installed addresses concurrency.** Specifically, in that 2026-08-31 snapshot:

- **FMRS — absent.** Zero hits anywhere in the working tree *or* in git history. Method C is a **new
  dependency ⇒ OVERRIDE-level** (§B3 covers MechJeb only).
- **StageRecovery — absent.** Same. Method D would also be a new dependency, and it does not fly anyway.
- **PhysicsRangeExtender — ⚠ CONFLICTING RECORDS.** `MOD_INVENTORY_RESEARCH.md` (2026-08-28) names it;
  the later, exhaustive `DEPENDENCY_MATRIX.md` (2026-08-31) does **not** list it. **This does not block
  anything** — `RangeExtender.cs` is our own source port and needs no install — but the record is
  contradictory and only the owner can resolve it (C7 forbids looking). **Flagged, not resolved.**
- **Trajectories — ⚠ not in the 2026-08-31 snapshot** (it appears only in the 2026-08-04 F9I-era
  `ARCHITECTURE.md`). This matters for §6: the kOS methods studied there use Trajectories as their impact
  predictor. **We do not need it** — `MechJebModuleLandingPredictions` is the equivalent, and it comes with
  the MechJeb embed we are already committed to (§B2/§B3). Recorded so nobody adds a dependency for it.
- **kOS — absent** from this install (`plugin/build/check_live.py:33`: this game *"has NO Ships/Script at
  all"*). Consistent with the owner's ⛔ "no kOS code in this mod".

---

# 5. Impact on §B16 — what a governance task would need to amend

**§B16 does not have to be rewritten.** Its architecture holds. Four additions and one statement of choice:

## 5.1 §B16.1 (separate autopilot) — HOLDS, needs the concurrency mechanism added
The "separate-vessel autopilot, not another conductor phase" decision is **unchanged and correct** — if
anything §1 strengthens it, because the two vessels need *different* control callbacks, *different* attitude
authorities and *different* per-vessel-type cfgs. What §B16.1 does not yet say, and would need to:

1. **The mechanism** — `RangeExtender` (our PRE port) on before separation, off after focus returns, plus a
   per-vessel control callback. §B16.1 currently lists `RangeExtender.cs` only as an existing seam; it is in
   fact **the enabling mechanism**, and the range is the single most important number in the section.
2. **The focus-assignment RULE (§1.4):** whoever holds focus gets the precision, therefore **the booster
   holds focus during recovery**. This settles the direction of §B16.1's "open design question" (the
   `ForceSetActiveVessel(booster)` call) on *engineering* grounds — though whether the crew's view leaves the
   Dragon at all remains the owner's call (O3).
3. **⚠ A NEW REQUIREMENT §B16 does not currently carry — the screens must be focus-hardened.** Switching
   focus to the booster silently repoints the Dragon's own telemetry. Exactly one reader in the tree is
   hardened — `DockingCamRenderer.OurVessel()`, which finds the vessel carrying `DragonScreenState` rather
   than trusting focus, and whose comment already describes this failure: *"the Dragon's own docking camera
   renders a view out of the BOOSTER… showing the capsule's crew a picture from a stage three hundred
   kilometres away."* **Three readers are not hardened:** `VesselData.cs:55/776/1122`, `NavBallRenderer.cs:237`
   and `ScaledPlanetRenderer.cs:280` all read `FlightGlobals.ActiveVessel` directly. **Any focus-switching
   recovery repoints the entire Dragon telemetry snapshot, the navball and the planet renderer at the
   booster.** This is Part-A screen work that §B16 depends on, and it is not in any register line.

## 5.2 §B16.2 (five-phase profile) — HOLDS; add the DEFAULT and the target mode
- **The five phases are confirmed** by two further independent tier-2 sources (§6). No change.
- **§B16.2 should state the default profile rather than leave it assumed: ASDS / droneship.** Three reasons
  converge: it is what **Crew-2 flew** — the mission our tuned cfg is for (S48 §2.1); it is
  **mission-realistic** for a crewed Falcon 9 (Demo-2 and the early Crew missions all recovered on a
  droneship, the vehicle lacking the margin to return to LZ-1 on a crew profile; **RTLS on crew missions is a
  recent development** — Crew-9 and Crew-11 landed at LZ-1) **[DOC]**; and it is the **range-friendly** one
  (§1.3: ~220 km vs ~820 km). RTLS stays a supported mode, not the default.
- **ONE guidance with a TARGET MODE, not two implementations** (§6.2). The register/plan wording should say
  so explicitly so it is not built twice.

## 5.3 §B12.6 step (9) — POSITION UNCHANGED, prerequisites grow
Step (9) stays its own track, after (1)–(4), before-or-after (8) at the owner's call. Its prerequisites,
however, are now **three**, not two: the craft dump (§B16.4) and the §B16.5 guidance decision **plus** the
screen focus-hardening in §5.1(3). The range/focus behaviour itself is a **flight-verify** item and so sits
behind the separate `install` + glass-time gate.

## 5.4 §B16.5 (the guidance decision) — the evidence now clearly favours option (a)
§6 shows the five-phase method maps cleanly onto MechJeb's attitude controller + landing predictions, and
that the repo has already ported the hard part once (a numerical, drag-and-spool-aware ignition solver).
**BoosterGuidance as a second dependency buys less than it did** and costs an OVERRIDE. **The decision
remains the owner's (O2)** — this doc only supplies the evidence.

## 5.5 A new subsection would be needed
§B16 has no home for §1's material. A governance task would add **§B16.7 "Two-vessel concurrency: range,
focus and the flight-verify"** carrying §1–§3 and §5.1(3).

---

# 6. THE GUIDANCE METHOD — the laws that produce landing-zone accuracy

## 6.1 Provenance — ⚠ TIER-2, MARKED, and one C7 flag

Two **[T2]** sources of the same lineage. They are **different scripts by different authors** and are kept
distinct below; nothing from one is attributed to the other.

| | **F9I** — the owner's own kOS project | **surgical9's KSP Falcon-9 kOS RTLS script** |
|---|---|---|
| What | *Falcon 9 Interface*; ships **GPL-3.0** (`assets/ASSET_PROVENANCE.md:6`). DragonScreen began as its screen front-end; **this mod has no kOS dependency and no link to it** | a public kOS RTLS script for KSP 1.12.3 with an RO Merlin config; **MIT**, by GitHub user `surgical9` |
| Accuracy claimed | **lands 0.34–0.56 m from the pad**, across 8 near-identical black-box flights **[REPO]** | not stated |
| What we take | the **phase decomposition, the flown attitude/roll budget, the acceptance targets**, and the AtmGNC-level trigger rules | the **quantitative control laws** — error computation, the steering clamp, the AoA schedule, the ignition predicate and throttle law |
| ⚠ Source status | **NOT IN THE REPO.** The scripts live at an out-of-repo path two build tools reference. **C7: not a build source.** Everything F9I below is reconstructed from **the repo's own surviving records** — `assess_flight.py`'s roll targets, `audit_comments.py`'s citations, and the deleted `F9I_BOOSTER_TARGETS.md` / `BOOSTER_GUIDANCE_DESIGN.md` read from git history | public repository, read for this task under the owner's research authorisation |

**Attribution to record at release** (the style used for MechJeb / KER / the MaTte0 CC-BY model): *method
only, no code* — "booster-recovery guidance method informed by F9I (GPL-3.0) and by surgical9's KSP Falcon-9
kOS RTLS script (MIT); no script was copied or transcribed."

**⛔ Per the owner's directive, no kOS code is copied or transcribed anywhere in this document or into the
mod.** What follows is the *method*, restated as C#-implementable laws.

> ⚠ **The two sources AGREE on the shape** — five phases, a predicted-impact error signal, an AoA-clamped
> steering law, a `v²/2a` ignition predicate, a `stopDist/altitude` throttle law — and agree with
> BoosterGuidance and with §B16.2. **That convergence is the finding.** They differ on *numbers*, and every
> number below is a starting point for the §B5 one-at-a-time tune, not a value to trust.

## 6.2 ONE guidance, ONE target mode — what differs and what is shared

```
TargetMode { RTLS, ASDS }
   ├── selects the AIM POINT      RTLS → PAD  (28.6084, −80.6043)
   │                              ASDS → BARGE(32.787551, −76.644507) = the DECK CENTRE, not the group centre
   └── enables PHASE 1 BOOSTBACK  RTLS → yes (~10 % of total propellant)
                                  ASDS → no  (~6 %)
PHASES 2–5 (coast · entry burn · aero descent · landing burn) ARE IDENTICAL IN BOTH MODES.
```

That is the whole difference. **The booster core carries one guidance and a target mode**, not two
implementations. Everything in §6.3–§6.9 except §6.4 is shared; §6.4 is skipped in ASDS.

The two aim points are the only surviving coordinates in the repo, and `BARGE` is deliberately the **deck
centre** (group centre + the ~5.7 m model offset), because aiming at the group centre made an on-aim landing
read "dead centre" while it was ~5.7 m off a 25 m-wide deck **[REPO]**.

## 6.3 The two things every phase needs

**(a) The predictor.** Both kOS sources delegate impact prediction to the **Trajectories** mod and sample it
continuously. **Our equivalent is `MechJebModuleLandingPredictions`** — which we get with the embed and
which already integrates aero. Sample the latest published result each `FixedUpdate`; do not block on it.
(§4: do **not** add Trajectories as a dependency for this.)

**(b) The error signal.** `e = predicted impact − aim point`, in the surface frame, decomposed into
**along-track** (downrange) and **cross-track**. **The repo already contains exactly this decomposition** —
`assess_flight.py:405-419` computes bearing + haversine from the pad, then rotates the N/E offsets by the
pad→target bearing into along/cross. Lift it into the pure core as the error function and the whole guidance
and the whole assessor share one definition of "miss".

The on-target test is likewise already defined **[REPO]**: the deck is **50 m × 25 m**, so
`onDeck = |along| ≤ 25 m && |cross| ≤ 12.5 m`.

## 6.4 PHASE 1 — BOOSTBACK (RTLS only)

| Element | The law | Source |
|---|---|---|
| **Flip** | Rate-limited slew to burn attitude, **settled before throttle-up**. surgical9 walks pitch in 0.5 s steps (40→80°, then 80→0°) — that staging is a **workaround for kOS steering lag and must NOT be ported**; MechJeb's attitude controller slews properly. What survives is the *rule*: settle, then burn. | [T2] both |
| **Burn attitude** | Point opposite the **surface** velocity vector — `SmartASS SURFACE_RETROGRADE`. | [T2] surgical9 |
| **Lateral trim** | Cross-track error outside a **±20 m** deadband → apply a **±2°** heading offset. A deadband, not a continuous gain: this is what keeps roll travel low (§6.9). | [T2] surgical9 |
| **Throttle taper** | Full while the miss is large; **step down (0.4) at ~1000 m; cut at ~500 m** of predicted miss. Generalised: `throttle = clamp(k·|e|, floor, 1)` with a cut threshold — the taper is what converts a coarse burn into a 20 m solution. | [T2] surgical9 |
| **Cut-off** | **Three conditions, first to fire:** (i) `|e| < tol` (BoosterGuidance: ~20 m); (ii) **the error stops improving** — the guard against chasing a solution the vehicle cannot reach; (iii) a **kinematic guard** — surgical9 cuts when vertical speed reaches −300 m/s regardless of error. Implement all three. | [T2] both + [DOC] |
| **After cut** | Shut down, release steering, RCS on, coast; hand to the descent phase when vertical speed passes a threshold (surgical9: −5 m/s). | [T2] surgical9 |
| **Budget** | **39 s**, 138° of roll travel, mean 106.7° off retrograde (the flip dominates). | [T2] F9I |

## 6.5 PHASE 2 — COAST

- **Attitude is NOSE-UP, not retrograde**, until vertical speed passes a threshold (F9I: **−50 m/s**), on a
  **coarse** attitude deadband. Then switch to guided descent on a **tight** deadband. **[T2] F9I**
- ⚠ **This is a deliberate profile feature, not a fault.** F9I's own black box shows a mean **123.6°** off
  retrograde during this window; the repo's note is explicit: *"A large angle-to-retrograde in that window is
  the profile working."* An implementation that forces retrograde here is wrong. **[REPO]**
- **Grid fins deploy on VERTICAL SPEED, not on a timer.** **[T2] F9I**
- **Budget:** 33 s nose-up (52° roll) + 75 s guided (50° roll), mean 18.6° off retrograde in the guided part.

## 6.6 PHASE 3 — ENTRY BURN  *(shared by both profiles)*

| Element | The law | Source |
|---|---|---|
| **Trigger** | Altitude. surgical9: **65 km**. §B16.2 / S48: ~55–70 km. Real Crew-2: T+7:27. A tunable entry-burn altitude. | [T2] + [REPO] |
| **Engines** | **THREE** — centre + two opposed. Not one: one cannot bleed enough speed through the thickest, fastest part of entry in the window, and three cut the peak heating and dynamic pressure the airframe sees. | [REPO] |
| **Attitude** | Surface-retrograde, steered **slightly off** retrograde to null the target error — the same law as §6.7, at a small angle. F9I flies a mean **2.5°** off retrograde here. | [T2] both |
| **Throttle** | Full, **tapering at the end**. | [DOC] BoosterGuidance |
| **End** | A **velocity target** — surgical9 ends when vertical speed recovers above −310 m/s. | [T2] surgical9 |
| **Budget** | 14 s (F9I) / ~20 s (real Crew-2). 94° roll travel. | [T2] + [REPO] |
| ⚠ **Do not over-burn** | The entry burn **must reserve the landing burn's share**. The repo's own assessor already scores this: it watches the recovery-propellant fraction at DESCENT and prints `*** ENTRY BURN OVER-BURNED` below ~0.25, and records a flight where ours killed 1459 m/s against a real profile that is *"a lighter bleed, leaving more landing margin"*. | [REPO] |

## 6.7 PHASE 4 — AERO DESCENT — **the accuracy core**  *(shared)*

Engines **off**; grid fins do the work. *"Above ~100–200 m/s aero forces dominate… grid fins do the work,
engines do not"* (S48 §2.6) **[REPO]**.

**The steering law** — this is the single most transferable thing in either source **[T2] surgical9**:

```
d = −v_surface + K · e                     K = error gain (surgical9 uses 1)
if angle(d, −v_surface) > AoA_max:
    d = clamp d onto the cone of half-angle AoA_max about surface-retrograde
command d to the attitude controller
```

Retrograde, biased toward the target, **hard-clamped to an angle-of-attack cone**. The clamp is what keeps
it stable; the gain is what makes it converge.

**AoA_max is an ALTITUDE SCHEDULE, not a constant** — because dynamic pressure rises as it descends
**[T2] surgical9**:

| Radar altitude | AoA_max |
|---|---|
| above 12 km | **10°** |
| 7–12 km | **7.5°** |
| below 7 km | **5°** |

> ⚠ **The most important lesson in this document, and it is the repo's own** **[REPO]**: **AoA_max is a
> CEILING that should rarely bind.** F9I flies a **mean of 6.7°** off retrograde through the whole descent
> while carrying a 15° ceiling. *"A descent commanding a steady 15 degrees is commanding more than twice what
> F9I flies."* A guidance that sits on its limit is not steering, it is thrashing — and the repo has the
> flight to prove it (§6.9). **Tune the gain so the clamp is a safety net, not the controller.**

Other descent settings: **tight** attitude deadband; **hold roll still** (F9I sets a roll time-to-settle of
10 for the descent). Budget: **48 s**, 109° roll travel.

## 6.8 PHASE 5 — LANDING BURN — the ignition solution and the throttle law  *(shared)*

**Why it is a hoverslam at all** **[REPO]**: *"even ONE Merlin at its ~40 % minimum throttle has TWR > 1 on
the near-empty stage — it cannot hover."* There is no hang-and-settle. The vehicle must arrive at **zero
velocity exactly at zero altitude**.

**(a) The ignition predicate — the classic closed form** **[T2] both, [DOC] BoosterGuidance**

```
stopDist  = v_vertical² / (2 · a_net)          a_net = F_available/m − g
trueAlt   = radarAltitude − legOffset          (per-craft; surgical9's vehicle: 45.2 m)
IGNITE when  stopDist ≥ trueAlt
```

**(b) ⚠ But the closed form is not good enough, and this repo already knows why.** Its own history records
replacing it **[REPO]**: the closed form is **drag-blind** and **spool-blind**. What replaced it was
**MechJeb's method** — a **numerical descent integration + root-solve** for the ignition point — *extended*
with (i) the aero drag MechJeb's own version omits (a TODO in MechJeb's source) and (ii) the **~3.5 s Merlin
spool ramp**. Both matter more than the algebra. **That is the recommended solution**, and it is a strong
argument for §B16.5 option (a): the hard part has been solved here once already.

**(c) ⚠ The RO mass trap** **[DOC] BoosterGuidance**: the ignition altitude is computed *before* the
propellant the earlier phases will burn is consumed, so the predicted mass is too high and **the burn arms
too early — until after the entry burn.** Mitigations: **recompute continuously** (BoosterGuidance runs its
simulation ~10×/s), only trust the solution after the entry burn, and carry a **larger touchdown margin —
30 m in RO, against 10 m normally.**

**(d) Ignition LEAD.** Light **early** by the engine-startup lead time. In RO that lead covers the
**ignition delay + spool** (`throttleResponseRate`, *"about two seconds for an F-1 class engine"*) — and, per
the repo's own tuning, once the solver absorbs the spool the remaining lead covers only **the ullage-settle
dead-fall and retry room** (it fell 6.5 → 3.0 s when the solver took over). **[REPO] + [DOC]**

**(e) The throttle law** **[T2] both**:

```
throttle = stopDist / trueAlt          clamped to [engineFloor, 1]
```

⛔ **Never zero.** Zero throttle is an instant shutdown in RealFuels, and the relight costs an ignition the
vehicle may not have. Hold a floor above the engine minimum — BoosterGuidance states the same rule:
*"once the landing burn is enabled BoosterGuidance will not reduce thrust to zero since this kills the
engine."* **[DOC]**

**(f) The engine schedule — 3 → 1** (§B16.3, per-engine control only, never mode-cycling) **[REPO]**:
1. Light **three** (centre + two opposed) — or **centre only** if the ignition budget or the RO throttle
   floor forbids three — **early**, by (d).
2. Fly the high-thrust segment on three: three are needed to arrest the terminal speed.
3. **Shut the two outboards** when the solver's required thrust-acceleration falls to what one engine can
   hold. Three at the deck is far too much thrust to null the last few m/s precisely.
4. The **centre engine alone** flies the terminal segment on `independentThrottlePercentage` (starting around
   70 % and modulating), which is where the fine throttle authority lives.
5. ⚠ Settle propellant with RCS to "Very Stable" **before every relight** — ullage is the failure this
   project has already had (*"booster ballistic, eng never lit → LOST"*).

**(g) Terminal logic** **[T2] surgical9**: keep the §6.7 error-nulling law but at a small **negative AoA
(−3°)** so the thrust vector biases the correction; at **t_impact < 3.5 s switch to pure surface-retrograde
and stop correcting** — no aggressive manoeuvres at the deck. Deploy **legs at ~350 m** radar altitude
(MechJeb: `DeployGears = True`, `LimitGearsStage` per craft — ⚠ the opposite of the Dragon, which has no
legs). Cut at the touchdown speed (surgical9: −0.1 m/s; S48 §2.5 gives the booster ~0.5–2 m/s).
**Budget: 19 s, and 0° of roll — the landing burn should be dead still.**

## 6.9 The attitude budget — the acceptance targets

F9I's own black box, 8 near-identical flights of the vehicle that lands **0.34–0.56 m from the pad**
**[T2] F9I, via [REPO]**. The last column is what this project's own (now-deleted) guidance actually flew.

| Phase | secs | mean angle to retrograde | **roll travel** | ours, 2026-08-12 |
|---|---|---|---|---|
| flip + boostback | 39 | 106.7° | **138°** | 738° |
| coast, nose up | 33 | 123.6° | **52°** | — |
| coast, guided | 75 | 18.6° | **50°** | 240° (whole coast) |
| entry burn | 14 | 2.5° | **94°** | 17° |
| descent | 48 | **6.7°** | **109°** | 428° |
| landing burn | 19 | 0.4° | **0°** | 92° |
| **total** | | | **443°** | **1515°** |

> **These are the acceptance targets, and the repo's assessor already scores against them**
> (`assess_flight.py:48-54, 385-396`). A booster that rolls 1515° instead of 443° — **3.4× over**, worst at
> flip+boostback (5.4×) and descent (3.9×), and rolling 92° during a landing burn that should be dead still —
> is the signature of an over-aggressive steering law. **Use the corpus, not first principles.** The repo's
> own warning: *"Every time this project has argued from physics instead of reading the corpus it has cost a
> flight."*

## 6.10 Mapping onto MechJeb in C# — §B16.5 option (a)

| kOS idiom | The C# equivalent | Note |
|---|---|---|
| `lock steering to <direction>` (**cooked steering**) | `MechJebModuleAttitudeController` (`BetterController`) / `MechJebModuleSmartASS` — **command a DIRECTION, let the controller fly it** | ⛔ Do **not** write raw `FlightCtrlState`. Corollary from F9I's black box: `ctlRoll` is 0.000 in every phase *because* cooked steering bypasses `FlightCtrlState` — *"There is no command data in this corpus — only response. Do not look for it again."* **[REPO]** |
| Trajectories `IMPACTPOS` | `MechJebModuleLandingPredictions` | §6.3(a) |
| surface-retrograde hold | `SmartASS SURFACE_RETROGRADE` | coast + entry burn |
| explicit heading/pitch | `SmartASS SURFACE` | the flip |
| `torqueepsilon` deadbands | the attitude controller's tolerances — **coarse** for the ballistic hold, **tight** for guided descent | ⚠ **Do NOT port F9I's second pair** (max 0.0002 below min 0.001). The repo already flagged it as *"either a typo or a field whose meaning I have not established, and guessing at a controller deadband is how the roll got worse the first time."* **[REPO]** |
| the ignition solver | MechJeb's numerical descent-integration + root-solve, **plus drag and spool** (§6.8b) | the one piece worth porting wholesale |
| engine commands | per-engine `Activate()` / `Shutdown()` / `independentThrottle` + `independentThrottlePercentage` | §B16.3 — **never** `"next engine mode"` |
| staging | `MechJebModuleStagingController` **OFF** | it must not stage the booster |

**What we deliberately do NOT take:** any kOS code (owner directive); surgical9's staged-pitch flip
(a kOS-steering-lag workaround); F9I's torque-epsilon pair; and every **per-craft** constant — the 45.2 m leg
offset, the AoA schedule breakpoints, the entry-burn altitude — which come from the **owner-supplied craft
dump** (§B16.4) and the §B5 tune, not from another vehicle.

---

# 7. Open items

## 7.1 OWNER decisions (C1.12 — a build chat decides none of these)

| # | Decision | Note |
|---|---|---|
| **O2** *(open, S48 §10.2)* | **How the booster is flown** — our own core on MechJeb's modules · MechJeb's landing autopilot as-is · BoosterGuidance as a second dependency. | §5.4: the evidence now favours **option (a)**. Still the owner's call; the third is OVERRIDE-level. |
| **O3** *(open, S48 §10.2)* | **Vessel focus.** | §1.4 settles the *engineering* half — the booster must hold focus or the hoverslam cannot be accurate. Whether the crew's view leaves the Dragon at all is still the owner's. |
| **O5** *(open, S48 §10.2)* | **RTLS or ASDS, and the aim point.** | §5.2 recommends **ASDS as the default**, RTLS as a supported target mode. |
| **O-B1** ⭐ NEW | **The concurrency method: A (concurrent, ~1500 km) · B (coast, ~600 km) · C (FMRS) · staged B→A.** | §3 recommends **staged B→A**. **C is OVERRIDE-level** (a new mod dependency; §B3 covers MechJeb only). |
| **O-B2** ⭐ NEW | **Is PhysicsRangeExtender actually installed?** The two in-repo inventories disagree (§4) and C7 forbids checking. | Does **not** block anything — `RangeExtender.cs` is our own port. Record-hygiene only. |
| **O-B3** ⭐ NEW | **Does §B16 get amended** with §5's five items, and does booster recovery get its own register T-series? | §B16.6 already records that no register task exists and that this is the owner's call. A **governance task** would do the amending — not this one. |

## 7.2 Flight-verify — resolvable only in the capsule (behind the `install` + glass gate)

Restated from the deleted `BOOSTER_DUAL_FLIGHT_RESEARCH.md` **[REPO]**, still all open:
**(a)** our PRE keeps both craft loaded + unpacked through the recovery · **(b)** the focus switches fire ·
**(c)** ⭐ **the upper stage survives the coast and resumes its ascent to orbit** — the make-or-break unknown
for method B · **(d)** the booster lands on the deck despite phantom forces · **(e)** the range is sized from
the *measured* separation, not from §1.3's estimate.
Plus, new here: **(f)** whether MechJeb's ascent modules actually behave on a **non-active** vessel (method A).

⚠ **A measurement is owed instrumentation:** the flight recorder follows the **active vessel only**, so the
booster↔upper-stage separation *is not in any single recording*. A two-vessel range readout is needed before
(e) can be answered **[REPO]**.

## 7.3 Logged findings — NOT acted on (C1.1)

1. ⭐ **The screens are not focus-hardened.** `VesselData.cs:55/776/1122`, `NavBallRenderer.cs:237` and
   `ScaledPlanetRenderer.cs:280` read `FlightGlobals.ActiveVessel` directly; only
   `DockingCamRenderer.OurVessel()` is hardened. **Any focus-switching recovery repoints the Dragon's whole
   telemetry snapshot, navball and planet renderer at the booster.** Part-A work, no register line exists.
2. **The AUTO BOOSTER RECOVERY toggle is a live control with a dead effect** — `SettingsPage.cs:97/237/407` →
   `ScreenPainter.cs:724` flips `MissionConductor.AutoRecoverBooster` and logs behaviour that does not exist;
   nothing reads the flag. `SCREEN_LIVENESS_AUDIT.md` (S49) **has no booster row at all** and missed it.
3. **`RangeExtender.Enable`/`Disable` have zero callers** — already protected by `REGISTER.md` ("do NOT
   retire it: §B16 claims it"), noted here as the mechanism §B16.7 would name.
4. **Two dangling references to the deleted implementation:** `DockingCamRenderer.cs:439`
   (`BoosterRecovery:256`) and `assess_flight.py:399` (`BoosterRecovery.DroneshipEarthLatDeg/LonDeg`).
5. **`KerData.HasRecoveryReserve(stages, reserveMps)` still has no default reserve value**, and there is a
   second same-named function on different inputs in `StageStats.cs:101` — both callerless. §6 does not
   supply the number either; it must be net of RealFuels residuals (S48 §2.6 gotcha 5).
6. **`docs/INDEX.md` did not list `MECHJEB_MISSION_TUNING.md`** (S48 logged the same, §10.3 #7). This task
   added a line for **this** doc only (C1.11 — a task writes only its declared outputs).
7. **`plugin/build/check_live.py` and `audit_comments.py` hard-code an out-of-repo F9I path.** Both degrade
   gracefully when it is absent. Noted as the C7 boundary that made §6.1's reconstruction necessary.

---

# 8. Sources

**Repo (authoritative, C7.1).** `docs/BUILD_PLAN.md` §B12.6 · §B16.1–§B16.6 · §14.4 ·
`docs/MECHJEB_MISSION_TUNING.md` PHASE 2 (§2.0–§2.6) — the per-setting recipe this doc does not restate ·
`plugin/src/RangeExtender.cs` · `plugin/src/_AutopilotStub.cs` · `plugin/src/DockingCamRenderer.cs` ·
`plugin/src/HullCams.cs` · `plugin/src/pure/VehicleParts.cs` · `plugin/src/pure/KerData.cs` ·
`plugin/src/pure/StageStats.cs` · `plugin/build/assess_flight.py` · `plugin/build/check_live.py` ·
`docs/TELEMETRY_REGISTRY.md` · `docs/SCREEN_LIVENESS_AUDIT.md` · `docs/FLIGHT_144114_SCREEN_AUDIT.md` ·
`assets/ASSET_PROVENANCE.md`.

**Repo git history** (deleted in `8b81816`, read for this task; **not resurrected** — INDEX §7 stands):
`docs/BOOSTER_DUAL_FLIGHT_RESEARCH.md` (the unpack/OnFlyByWire resolution, the PRE port, the 2026-08-29
design, the H1 flight-verify list) · `docs/BOOSTER_GUIDANCE_DESIGN.md` (the real Crew-2 booster profile, why
3→1, the hoverslam solver history) · `docs/F9I_BOOSTER_TARGETS.md` (the F9I black-box phase/roll corpus) ·
`docs/MOD_INVENTORY_RESEARCH.md` + `docs/DEPENDENCY_MATRIX.md` (the installed environment).

**External, checked 2026-09-03** (read under this task's research authorisation; **not** build sources):

- Vessel Load Distance / The kOS CPU hardware — <https://ksp-kos.github.io/KOS/structures/misc/loaddistance.html>, <https://ksp-kos.github.io/KOS/general/cpu_hardware.html>
- PhysicsRangeExtender (jrodrigv, from BahamutoD's BDArmory code) — <https://github.com/jrodrigv/PhysicsRangeExtender>
- BDArmory — AI on non-active vessels; PRE a required dependency — <https://bdarmory.fandom.com/wiki/How_to_use_Competition_Mode>, <https://forum.kerbalspaceprogram.com/topic/217362-wip-making-bdarmory-ai-work-with-atmosphere-autopilot/>
- FMRS (Flight Manager for Reusable Stages), linuxgurugamer — <https://github.com/linuxgurugamer/FMRS>, <https://forum.kerbalspaceprogram.com/topic/157214-112x-flight-manager-for-reusable-stages-fmrs-now-with-recoverycontroller-integration/>
- BoosterGuidance (GPL-3.0) — <https://github.com/oyster-catcher/BoosterGuidance> and the maintained fork <https://github.com/linuxgurugamer/BoosterGuidance>
- **[T2]** surgical9, *KSP Falcon-9 kOS RTLS Script* (MIT) — <https://github.com/surgical9/KSP-Falcon-9-kOS-RTLS-Script> — **method only; no script copied or transcribed**
- KSP `Vessel` / `VesselRanges` API — <https://anatid.github.io/XML-Documentation-for-the-KSP-API/class_vessel.html>
- RealFuels readme (ignitions, ullage, throttle floors, `throttleResponseRate`, residuals) — <https://github.com/KSP-RO/RealFuels/blob/master/RealFuels/Readme_RF.txt>
- Crew-Dragon booster recovery mode by mission (ASDS for Demo-2 and the early Crew missions; LZ-1 RTLS from Crew-9 / Crew-11) — <https://en.wikipedia.org/wiki/Crew_Dragon_Demo-2>, <https://www.nasaspaceflight.com/2025/08/lz-1-final-falcon-landing-pad/>, <https://www.nasaspaceflight.com/2025/07/crew-11-launch/>
- Crew-2 timeline — <https://spaceflightnow.com/2021/04/22/crew-2-mission-timeline/>
