# Falcon-9 booster guidance — the F9I method, extracted for §B16.5 option (a)

**Owner-directed research task, 2026-09-03. RESEARCH + THIS DOC ONLY — no code changed, no plan edited,
no register line closed.** Governed by `docs/BUILD_PLAN.md` **§B16** (booster recovery scope + architecture),
**§B16.5** (the guidance decision — this doc supplies the evidence for option (a) and does **not** take it),
**§1.4** (source-of-truth ladder) and **C7** (build inputs must be in the repo).

> 🟢 **C7 EXCEPTION — the OWNER's, granted in-chat 2026-09-03.** C7 lists external URLs as off-limits build
> sources. The owner named `https://github.com/mups3y/Falcon-9-Interface` and said *"go, use that repo"*.
> That is the whole of the authority used here; nothing else external was read, and the clone was made into
> the session scratchpad, **never into the repo tree** (C1.11).
>
> ⛔ **NO kOS CODE IS REPRODUCED HERE, BY DIRECTION.** This doc records the **method** — the algorithm, the
> steering and timing laws, and the constants that parameterise them — in terms implementable in C# against
> MechJeb's attitude and prediction modules. It does not transcribe script.

---

## 0. The finding in one paragraph

**F9I already flies RTLS and ASDS as ONE guidance with a target mode, not two implementations** — a single
`landProfile` selector (1 = RTLS, 2 = ASDS, 3/6 = expend) picks a small parameter set and two geometry
decisions, and every steering and timing law downstream is shared. That is the exact architecture §B16's
booster core wants, arrived at independently by a working, flight-proven script. The guidance itself is
simpler than expected: **one steering law does the entire descent** — add the predicted-impact error vector
to the surface-retrograde vector, clamp the result to a max angle of attack, and taper that angle to zero as
the ground approaches. Boostback, entry burn and landing burn are then just three throttle laws layered over
that one attitude law. **Two things in it must not be ported:** its engine-mode-cycling actuation layer is
the exact mechanism §B16.3 forbids, and every numeric constant is stock-Kerbin-scaled.

---

## 1. Provenance and tier (§1.4)

**TIER 2 — another user's method.** Marked as such everywhere it is used, per §1.4 and the style
`docs/MECHJEB_MISSION_TUNING.md` uses for MechJeb/BoosterGuidance and `assets/ASSET_PROVENANCE.md` uses for
the MaTte0 model.

| | |
|---|---|
| **Work** | Falcon 9 Interface (F9I) v1.1.0 — a kOS flight interface for the Tundra Exploration "Ghidorah 9" Falcon 9 / Falcon Heavy |
| **Author** | **mups3y** (the owner of this project) |
| **Source** | `https://github.com/mups3y/Falcon-9-Interface` |
| **Pinned at** | commit **`6f1486bdd4ff5ed8d3c4b6b948c6e9dcaaa59b69`**, 2026-08-04, *"@ F9I v1.1.0 - initial public release"* |
| **Licence** | **GNU GPL v3** — compatible with DragonScreen and with the pinned MechJeb embed (§B3/§B12.1) |
| **Upstream tier-2 chain** | F9I states it is *derived from the KSP Starship kOS Interface (Janus92 / Nubro), also GPL v3*. The method below is therefore **twice-derived**; attribution runs to both. |
| **Files the method lives in** | `Ships/Script/SPACEX/BOOSTER.ks` (the recovery core), `Ships/Script/SPACEX/PARAM.ks` (the profile parameter sets), `Ships/Script/COMMON/GNC.ks` (impact prediction, the spool/ullage helper) |
| **Owner's assessment** | the most accurate RTLS landing they have achieved, and a competent droneship performer — which is why it is the source rather than BoosterGuidance |

### 1.1 ⚠ THE SCALE CAVEAT — read before using a single number

**F9I flies stock Kerbin with Tundra Exploration parts. DragonScreen is RSS/RO.** The evidence is
unambiguous: the fallback landing zones are at latitude ≈ −0.13, longitude ≈ −74.55 (stock KSC), atmosphere
height is taken from the body's own `atm:height` (70 km on Kerbin, ~140 km on RSS Earth), and the entry-burn
gate is 32.5 km — 0.46 atmosphere-heights on Kerbin, a completely different regime on Earth.

> **The LAWS transfer. The CONSTANTS do not.** Every number in §4 is recorded as `[F9I]` — *the value F9I
> uses on Kerbin* — and each is a **starting point to re-converge in RSS/RO**, not a value to ship. This is
> the same discipline §B5/T22 already applies to the ascent tune: begin from a defensible baseline, then
> converge one parameter at a time against recorded flights.

### 1.2 Provenance confirmed in the other direction

`plugin/src/pure/VehicleParts.cs` already carries `GridFinPart = "Grid Fin M Titanium"`,
`EngineSwitchModule = "ModuleTundraEngineSwitch"`, `EngineIdThree = "Three"`, `EngineIdCentre = "Center"` and
the `".S1."` booster marker. **These match F9I's part identifiers exactly** — the octaweb model in our tree
was derived from this same vehicle. That is a useful confirmation that the two describe one craft, and it is
also why §7's warning matters: our constants encode F9I's *actuation* path as well as its part model.

---

## 2. The architecture: ONE guidance, a TARGET MODE

F9I's profile selector resolves to **two decisions and six numbers**, and nothing else in the flight software
branches on profile:

| Decision | RTLS (`landProfile` 1) | ASDS (`landProfile` 2) |
|---|---|---|
| **Where the target comes from** | a **static coordinate** — a configured landing-zone lat/lng, overridable from a coordinate config file and by an explicit RTLS-LZ parameter pair | a **live vessel lookup** — find the droneship vessel by name, take its ground position; falls back to a direct vessel-by-name lookup if it is not in the target list |
| **Boostback geometry** | flip to **180°**, then steer **at the target** and burn until the predicted impact passes it | flip to **170°**, then steer **retrograde with a 5° offset** and burn until the predicted impact passes a shifted aim point |

| Parameter `[F9I]` | RTLS | ASDS | What it drives |
|---|---|---|---|
| `maxPayload` | 7000 | 9000 | the payload-mass compensation term (§4.3) |
| `MECOangle` | 45° | 40° | staging pitch, and the flip's reference attitude |
| `tgtAlt` | 60 000 | 70 000 | ascent target apoapsis |
| `pitchGain` | 110 | 97 | ascent pitch program |
| `reentryHeight` | 32 500 m | 35 000 m | entry-burn **trigger altitude** |
| `reentryVelocity` | 550 m/s | 800 m/s | entry-burn **cutoff velocity** |

**Everything else is shared**: the flip law, the boostback throttle law, the entry-burn structure, the whole
aero-descent steering law, the landing-burn ignition solution, the throttle law, the 3→1 handover, the flare,
gear deployment, the ullage and spool discipline, and the impact-prediction primitive.

> ### ✅ Item 5's structural question, answered
> **One guidance with a target mode is correct, and it is what the accuracy source itself does.** The booster
> core should carry a `TargetMode { Rtls, Asds }` that resolves (a) an aim point — static coordinate vs live
> droneship vessel — and (b) a boostback geometry, then hand a single guidance chain a target. It should
> **not** carry two guidance implementations. Note also that the two profiles differ in the entry-burn
> *constants* (trigger 32.5 vs 35 km, cutoff 550 vs 800 m/s), not the entry-burn *law* — so those belong in
> the mode's parameter block, not in branching code.

**The repo already has the ASDS half of the hook**: `VehicleParts.IsDroneship()` / `DroneshipMarker =
"Droneship"` (`plugin/src/pure/VehicleParts.cs:77-78`) is exactly the live-vessel lookup this mode needs.
**It does not have the RTLS half** — see §8.3.

---

## 3. The phase sequence

F9I's top-level recovery flow, after separation is detected by watching the second-stage tank leave the
vessel:

```
  detect separation
    +- RTLS : flip to 180 deg  ->  boostback (steer at target)
       ASDS : flip to 170 deg  ->  boostback (retrograde + 5 deg, trimmed)
    +- atmospheric GNC   : reorient -> ENTRY BURN -> aero descent -> ignition point
    +- landing           : suicide burn -> 3-to-1 handover -> flare -> touchdown
```

A **flight-director GO/NO-GO** gates the whole thing: on NO-GO the booster skips boostback entirely and
deliberately ditches offshore. Worth carrying — it is the honest failure mode for a booster that cannot make
its target, and it maps cleanly onto the §B16 core refusing a phase whose ignition budget it cannot cover.

### 3.1 ⚠ What differs from §B16.2 — ASDS is *not* "no boostback"

`docs/BUILD_PLAN.md` §B16.2 and `docs/MECHJEB_MISSION_TUNING.md` §2.2 both state that the ASDS profile has
**no boostback burn**. **F9I does not agree.** Its ASDS path flips to 170° and runs the same boostback
routine, given a 5° rotation offset and a shifted aim point — a short retrograde *trim* burn rather than a
large return burn, but the same code, the same throttle law and the same overshoot bias.

This is a **conflict between a tier-2 method and the plan**, logged here and **not acted on** (C1.1/C7.1 —
THE PLAN WINS until the owner rules). It matters for the architecture: if ASDS trims rather than skipping,
then boostback is a *shared* phase with a mode-dependent target and magnitude, and the state machine has
**one** boostback state, not an optional one. See §8.1.

---

## 4. The guidance laws

Each of these is stated as a law plus its `[F9I]` constants, and then as what it becomes in C# against
MechJeb (§B16.5 option (a)).

### 4.1 The flip — a rate-limited slew, not a slew-to-target

**The method.** Do not command the final attitude and let the controller chase a 180° error. Instead carry a
**virtual commanded vector** that starts at the current facing and is advanced around the flip axis by a
fixed step each tick — and **advance it only while the vehicle's actual facing is within ~7.5° of it**. The
command leads the vehicle by a bounded angle, so the attitude error never leaves the controller's linear
range. Once within 25° of the final vector the gate is dropped and the command advances freely; at 15° it
snaps to the exact final vector.

`[F9I]`: step **0.333°/tick**, lead gate **7.5°**, free-run threshold **25°**, snap **15°**. The flip axis is
the cross product of the horizontal retrograde direction and the local vertical; roll is settled first
against that axis with a 10° tolerance and an 8-second timeout before the pitch-over begins.

**In C#/MechJeb.** This is a command *shaper*, not a controller, so it sits above
`MechJebModuleAttitudeController` (`BetterController`) rather than replacing it: hold the commanded
quaternion in the booster core, advance it per `FixedUpdate` under the lead-gate rule, and feed it to the
attitude controller each tick. The value of doing it this way is that it keeps a large-angle manoeuvre inside
the same PID that flies everything else — which is precisely the property that makes it portable to
MechJeb's controller unchanged.

> ⚠ RCS is on for the flip, and the steering gains are deliberately loosened (longer stopping time, roll
> torque factor raised) for the duration and reset afterwards. A port needs the same save/restore discipline
> around MechJeb's attitude tuning, or the flip's gains leak into the descent.

### 4.2 Boostback — proportional-error throttle, cut on predicted impact

**The steering.** RTLS points the thrust axis at the **horizontal bearing to the target** (the target's
position at the vehicle's current altitude, with the vertical component removed) — *not* retrograde. ASDS
holds **horizontal retrograde rotated by the mode's offset**.

**The throttle law — this is the accuracy mechanism.** Throttle is proportional to the **remaining downrange
error normalised by the error at burn start**:

```
    throttle = max(floor, currentImpactError / initialImpactError)
```

So the burn starts at full authority and **tapers smoothly to the floor as the predicted impact point walks
onto the target** — no bang-bang, no fixed Δv, no burn timer. The error is signed: the guidance compares the
angular position of the predicted impact against the target's, both measured from the body centre, and flips
the sign when the impact has gone *past* the target, so the law knows long from short.

`[F9I]`: throttle floor **0.25** (RTLS loop) / **0.125** (ASDS, and the RTLS pre-loop command).

**The cut — a deliberate overshoot bias.** The burn does **not** stop when the impact point reaches the
target. It stops when the impact point is **2700 m beyond** it. RTLS burns until predicted impact passes the
target by that margin; ASDS shifts its aim point 2700 m downrange, burns until the impact point is nearer the
pad than the shifted point, then restores the true target.

> **Why aim long.** Everything after boostback — the entry burn's off-retrograde steering, and a long aero
> descent under drag — removes range. The 2.7 km is the budget for that. It is the single most
> RSS/RO-sensitive number in the method: it is a *drag* budget, and RO's atmosphere is not Kerbin's.
> **Re-converge it first** (§10).

**In C#/MechJeb.** Needs a predicted impact point every tick (§5) and thrust-axis attitude commands — both
available. The law itself is a dozen lines of pure code and belongs in the headless-testable core: given
(predicted impact, target, initial error), return (throttle, commanded direction). That is exactly the
pure/glue split the project already enforces, and it means the boostback law can be regression-tested against
recorded flights without the game.

### 4.3 Entry burn — an altitude gate, a velocity cutoff, and a payload-mass correction

**The method.** Steer **pure surface-retrograde** for the burn (thrust dominates; steering to target here
wastes it), gate ignition on **altitude**, and cut on **speed** — never on duration or Δv.

F9I contains **two implementations**, and this is worth knowing before porting:

| | Live path (used by the atmospheric GNC routine) | Parameterised path (present, not on the flown path) |
|---|---|---|
| **Gate** | a hard-coded **32 500 m** | `reentryHeight + (maxPayload − payloadMass) / 7` |
| **Cutoff** | vertical speed risen to **−300 m/s** | airspeed below `reentryVelocity − (maxPayload − payloadMass) / 35` |
| **Engines** | step to 3-engine mode, full throttle **0.75 s**, step back down, then hold to cutoff | spool up, hold, spool down |

**Take the parameterised form.** Its **payload-mass compensation** is the better engineering and it is the
part worth porting: a lighter payload means a hotter, faster booster, so the gate rises and the cutoff
tightens, both linearly in the payload shortfall. The live path's hard-coded 32 500 m is the RTLS parameter
value inlined — it silently ignores the ASDS profile's own 35 000 m.

`[F9I]`: gate 32 500 m (RTLS) / 35 000 m (ASDS); cutoff 550 m/s (RTLS) / 800 m/s (ASDS); vertical-speed
cutoff −300 m/s on the live path; grid fins deploy at the gate.

**In C#/MechJeb.** `SmartASS SURFACE_RETROGRADE` covers the attitude exactly. The gate and cutoff are scalar
comparisons on vessel state. The engine sequencing is the part that **must be rebuilt** on per-engine control
rather than ported (§7).

### 4.4 Aero descent — the one steering law that flies the whole descent

**This is the heart of the method, and it is remarkably compact.**

```
    error    = predictedImpactPosition - targetPosition
    velocity = -surfaceVelocity                        ... i.e. the retrograde direction

    commanded = velocity + error                       ... clamped to max AoA:

    if angle(commanded, velocity) > AoA_max:
        commanded = normalize(velocity) + tan(AoA_max) * errorScale * normalize(error)

    where errorScale = min(1, |error| / deadband)
```

**Read it plainly:** *steer retrograde, then lean into the miss.* Adding the miss vector to the retrograde
vector tilts the vehicle so that lift and drag push the predicted impact point back onto the target. The
clamp is a true trigonometric limit — rebuilding the command as retrograde plus `tan(AoA_max)` of the error
direction produces exactly `AoA_max` of deflection, rather than merely rejecting an over-limit command.

The **deadband** scales the correction down inside a small miss distance, so the vehicle stops chattering
once it is on target.

`[F9I]`: deadband **5 m**; AoA limit **15°** through the descent.

**The AoA schedule — the authority taper.** The limit is not constant. Below 10 km it becomes
**`altitude / 100`** (metres → degrees): 15° at 1500 m, 10° at 1 km, 5° at 500 m, 1° at 100 m. **This is what
makes the landing vertical.** Steering authority is surrendered smoothly as the ground approaches, so the
vehicle stops trading attitude for accuracy exactly when it needs to stand up on its thrust axis. During the
landing burn itself the angle goes **negative** (§4.5).

A roll reference is held against a configured landing heading, with guards that fall back to a free roll
reference when the commanded direction comes within 15° of parallel or antiparallel to it — avoiding the
degenerate "look direction with up-vector" case.

**In C#/MechJeb.** Every term is available: the predicted impact from `MechJebModuleLandingPredictions`
(§5), the surface velocity from the vessel, and the commanded attitude straight into
`MechJebModuleAttitudeController`. The law is **pure** — vectors in, quaternion out — so it belongs in the
headless core with unit tests over synthetic miss geometries. This one function is the largest single win
available from this source.

> **Supporting PID structure.** F9I also carries lat/lng PID pairs driven by predicted-impact position
> against the target: an atmospheric set with output clamped to ±tan(10°), and a terminal/hover set clamped
> to ±tan(15°), the latter fed a **weighted blend of 20 % current position and 80 % predicted impact**.
> `[F9I]` gains: atmospheric **(45, 1.65, 3.25)**, hover **(300, 75, 205)**. Recorded for completeness — the
> vector law above is what actually steers on the flown path, and a port should start there rather than
> bringing four PIDs across untested.

### 4.5 Landing burn — the ignition solution and the throttle law

**The ignition solution.** Classic hoverslam, with two refinements that matter:

```
    trueAltitude = radarAltitude - boosterHeight    ... measure to the engine bell, not the CoM
    gravity      = mu / r^2                          ... computed from the body, not a constant
    maxDecel     = availableThrust / mass - gravity
    stopDistance = verticalSpeed^2 / (2 * maxDecel)

    IGNITE when  trueAltitude <= stopDistance + boosterHeight
```

`[F9I]`: booster height **31.02 m**, which serves **both** as the altitude correction *and* as the ignition
lead margin.

> ⚠ **This directly answers the RO trap S48 §2.5 records** — BoosterGuidance's warning that the landing-burn
> altitude is computed against a too-high predicted mass and arms too early. F9I sidesteps it by not
> pre-computing at all: the ignition test is **evaluated continuously against live mass and live thrust**, so
> it is always current. A port should keep that property. It is a better answer than a larger touchdown
> margin.

**The throttle law.** Once lit:

```
    throttle = stopDistance / trueAltitude  +  margin
```

**Self-correcting by construction.** Need more braking distance than you have altitude → the ratio exceeds 1
→ full throttle. Comfortably above the required distance → the ratio drops below 1 → throttle backs off. No
trajectory replan, no scheduled profile: one ratio, evaluated every tick, converging on touching down at zero
exactly at the pad.

`[F9I]` margins: **+0.06** bulk, **+0.06** after the single-engine handover, and **+0.34** for the flare
below **25 m** true altitude. The flare margin is large and deliberate — it is what converts the last of the
descent rate into a soft set-down.

**The 3→1 handover.** Hand from three engines to one when **both** hold:

1. descent rate has fallen below **40 m/s**, and
2. the **single-engine** stop distance, padded by **35 %**, still fits inside the remaining true altitude.

`[F9I]` single-engine thrust ratio **2.23** (not 3.0 — the effective ratio, allowing for throttle floors),
handover pad **1.35**. If touchdown happens before the handover condition is met, the burn simply completes
on three engines — the handover is an optimisation, not a required step.

**The terminal AoA schedule.** Through the landing burn the AoA limit is **negative** and tightening:
starting at −3°, then `−(altitude/100) − 0.25` clamped to the band **[−4°, −1°]**, and pinned to **−1°** below
300 m and **−0.25°** after the handover. The negative sign leans the vehicle the opposite way to the descent
correction, bleeding out the lateral rate it built up while steering, so it arrives vertical rather than
merely on target.

**Gear** deploys at **200 m** radar altitude.

**In C#/MechJeb.** All of it is arithmetic over live vessel state plus a thrust command — no MechJeb module
required beyond the attitude controller. **Do not use `MechJebModuleLandingAutopilot` for this phase**: this
law is strictly better suited to a limited-ignition, limited-throttle booster, which is the finding S48 §2.5
already reached from the other direction. MechJeb's contribution to the landing burn is attitude only.

---

## 5. The prediction primitive — what MechJeb has to supply

Every targeting law above is driven by **one primitive**: *where is this vehicle going to hit, right now?*
F9I exposes it in two forms — the predicted impact **ground position**, and the **downrange distance** from
that impact point to the target, computed as a great-circle arc from the angle the two subtend at the body
centre.

Its implementation is a **two-tier fallback**, which is the design lesson:

1. **Primary — the Trajectories mod**, which accounts for atmospheric drag. F9I sets the target into
   Trajectories and enables its descent modes so the prediction reflects the actual steered profile.
2. **Fallback — a Keplerian solver** that propagates the orbit to the impact radius, then **iterates on
   terrain height**: predict impact, sample the terrain height under that groundtrack, average it into the
   assumed impact altitude, repeat until the change falls under a convergence tolerance. Coarse, no drag,
   but never unavailable.

**In C#/MechJeb.** `MechJebModuleLandingPredictions` is the direct replacement for tier 1 — it is a
drag-aware descent prediction and is already named in §B16.5 as part of option (a). The **great-circle
downrange conversion and the signed long/short test are ours to write** and are pure. Keeping F9I's two-tier
structure is worth it: a booster whose guidance hard-fails when a prediction is momentarily unavailable is a
lost booster, and the Keplerian fallback costs little.

> ⚠ **Dependency note:** the primary path here is **Trajectories**, a third mod. §B3's packaging decision
> covers **MechJeb only**. Porting to `MechJebModuleLandingPredictions` avoids adding a dependency and is
> therefore the right call — but it is a **substitution of the prediction engine**, so the guidance gains and
> the 2700 m overshoot bias must be re-converged against MechJeb's predictor, not inherited (§10).

---

## 6. Ullage and spool — already correct, port as-is

F9I's engine-spool helper does exactly what S48 §2.6 gotcha 1 demands, and it is the piece our deleted
autopilot got wrong (`docs/FLIGHT_144114_SCREEN_AUDIT.md`: *booster ballistic, eng never lit → LOST*):

- **Ullage before ignition** — RCS on, forward translation at **0.75** for **2 s**, then neutralise.
- **Ignite at a trickle** — command **0.025** throttle first, hold briefly, and only then ramp.
- **Ramp, never step** — throttle is walked at **~1.333 units/second** (0 → 100 % in ~0.75 s) in both
  directions, respecting spool-up rather than commanding instant thrust.

**In C#/MechJeb.** Direct, and it should be a single shared routine in the booster core that *every* ignition
goes through — boostback, entry, landing. `GuidanceController.UllageLeadTime` (cfg 20 s) is the Dragon's
settle time; the booster needs its own, and this gives a defensible starting value.

---

## 7. ⛔ What must NOT be ported — the engine actuation layer

**F9I switches engine counts by cycling the Tundra engine-switch module's "next engine mode" action.** That
is, precisely and exactly, the mechanism the owner's directive of 2026-09-03 forbids (§B16.3 / S48 §2.3):

> *Do NOT cycle "next engine mode". RO's `ModuleEngineConfigs` mode-cycling causes engine **RE-IGNITIONS**
> and **lag**.*

**The source itself is evidence for the directive.** F9I's switch routine cannot trust the action to take
effect: it reads the mode field back after every cycle, retries up to three times, falls back to *assuming*
the mode advanced when the read fails, and ends with an explicit on-screen failure warning that the octaweb
would not reach the requested mode and *"landing may be wrong"*. That is a workaround built around an
unreliable actuator — on stock Kerbin, before RO's ignition costs are added.

**Therefore:** adopt §4's guidance laws, and rebuild the actuation beneath them on **per-engine control** —
`Activate()` / `Shutdown()` per engine plus `independentThrottle` + `independentThrottlePercentage`, exactly
as S48 §2.3 specifies. The mapping is clean, because the guidance only ever asks for *"three engines"* or
*"one engine"* at a given throttle; how that is delivered is entirely below the guidance layer.

**One habit worth keeping from it:** F9I *verifies* that its commanded engine state actually took effect and
says so loudly when it did not. Per-engine control should do the same — read back thrust and engine state,
and refuse or annunciate rather than assume.

> ⚠ **Consequence for our constants.** `VehicleParts.cs`'s `EngineSwitchModule` / `EngineSwitchAction` /
> `OctawebModeFor` describe this same forbidden path. S48 §10.3 finding 2 already records that the constants
> are a correct description of the part and only the *flight-software use* is forbidden. Nothing here changes
> that — but a session porting §4 will find those constants sitting right next to the part model it needs,
> and must not reach for them.

---

## 8. Conflicts and gaps this extraction surfaced — LOGGED, NOT ACTED ON (C1.1)

### 8.1 §B16.2's "ASDS = no boostback" is contradicted by the source
Detailed in §3.1. `docs/BUILD_PLAN.md` §B16.2 and `docs/MECHJEB_MISSION_TUNING.md` §2.2 both state the ASDS
profile has no boostback. F9I trims with a short offset boostback on the same code path. **Per C7.1 the plan
wins until the owner rules**; but the architecture consequence is real — boostback is a shared phase with a
mode-dependent magnitude, not an RTLS-only one. **Owner call.**

### 8.2 §B16.5's option (a) is now substantially de-risked
§B16.5 offers three routes: our own five-phase core on MechJeb's attitude + prediction modules; MechJeb's
landing autopilot as-is; or BoosterGuidance as a second dependency. **This extraction is a complete, working
specification for option (a)** — every law in §4 maps to `MechJebModuleAttitudeController` +
`MechJebModuleLandingPredictions` with no third dependency, and the pure/glue split falls out naturally
(§4.2, §4.4, §4.5 and §5's downrange maths are all pure and headless-testable). **The decision remains the
owner's** (C1.12); this doc only supplies the evidence.

### 8.3 There is still no RTLS aim point in the repo
`plugin/build/assess_flight.py:404` defines `PAD = (28.6084, −80.6043)` — that is **LC-39A, the launch pad**,
used only as the origin for a bearing/range readout — and `BARGE = (32.787551, −76.644507)`, the droneship
deck centre, which is the guidance aim. **LZ-1/LZ-2 (28.4858 N, 80.5444 W) exists only as a `[DOC]` note in
S48 §2.1, never as a constant.** F9I's structure shows what is needed: an RTLS landing-zone coordinate that
is *configurable* (it carries both a coordinate config file and an explicit RTLS-LZ parameter override).
**A `TargetMode.Rtls` has nothing to aim at today.**

### 8.4 The accuracy harness is ASDS-only
`plugin/build/assess_flight.py` scores a landing against the 50 m × 25 m deck
(`abs(along) <= 25 and abs(cross) <= 12.5`). **There is no RTLS pass/fail test.** Since the entire point of
this source is *LZ accuracy*, the harness is how we would ever know a port worked — an RTLS mode needs an
equivalent landing-circle criterion. F9I itself reports a scalar miss distance from the target at touchdown,
which is the natural shared measure.

### 8.5 📥 THE CRAFT FILES ARE IN THAT REPO — §B16.4 / O4 may be closable
The clone contains `Ships/VAB/`, including **`Ghidorah 9 - Crew Rodan.craft`**, `Ghidorah 9 - Crew Rodan
i4.craft`, the cargo and fairing variants, and **`DRONESHIP_MAIN.craft`** — the droneship F9I's ASDS mode
looks up by name.

**§B16.4 and S48 §2.4/O4 both state that the engine table cannot be filled because there is no craft dump in
the repo and C7 forbids reading the KSP install.** That input now demonstrably exists in a source the owner
has already pointed us at. **This does not self-authorise anything** — the owner's go covered *this*
extraction, and §B16.4's engine-table work is a different task (C1.1). But the blocker on O4 may be a
copy-in away. **Flagged for the owner.** Part names seen in passing while reading the guidance —
`TE.19.F9.S1.Engine`, `TE.19.F9.S2.Tank`, `KRE-FalconLegMk2-M` — are recorded as *observed*, not as an
engine table; filling that table is §B16.4's job against a dump placed in the repo.

---

## 9. Mission realism — the evidence for O5

Item 5 asks which profile is mission-realistic for a nominal Crew-Dragon launch versus which we default to,
*"so §B16.2 can state the choice rather than assume it."*

**Mission-realistic: ASDS.** `docs/MECHJEB_MISSION_TUNING.md` §2.1 records Crew-2 — the mission our tuned cfg
is built for — as a **droneship recovery with no boostback**, with its full timeline `[DOC]` (entry burn
T+7:27, landing burn T+9:03, landing T+9:30). Crew Falcon 9 boosters have generally flown downrange to the
droneship, because a crew profile's ascent leaves less margin for the ~10 % propellant cost of a return.

**The tension, stated plainly.** The profile the owner reports as **most accurate** is **RTLS**; the profile
that is **mission-realistic** for this vehicle is **ASDS**. That is not a problem to resolve — it is the
strongest argument for §2's conclusion. A single guidance with a target mode gives both: RTLS as the
accuracy-development and testing profile where the method is at its best, ASDS as the flown default for a
nominal Crew-Dragon mission.

**The default remains the owner's call** — it is already logged as **O5** (*RTLS or ASDS, and the aim point*)
in S48 §10.2, and §B16.2 cannot state a choice this chat is not permitted to make (C1.12). What this doc adds
is that **the choice is cheap**: under §2's architecture it is a mode selector and six numbers, not a
commitment to one implementation.

---

## 10. What this does NOT settle

1. **Every constant needs RSS/RO re-convergence** (§1.1). Priority order, most scale-sensitive first:
   **(a)** the 2700 m boostback overshoot bias — a drag budget, and RO's atmosphere is not Kerbin's;
   **(b)** the entry-burn gate and cutoff, both stated in absolute metres and m/s;
   **(c)** the AoA taper's `altitude/100`, which encodes an assumed terminal descent profile;
   **(d)** the landing-burn margins (0.06 / 0.34) and the 25 m flare altitude;
   **(e)** the 2.23 single-engine thrust ratio, which is vehicle- and throttle-floor-specific.
2. **The prediction engine is being substituted** (Trajectories → `MechJebModuleLandingPredictions`, §5).
   Gains and biases tuned against one predictor do not transfer unexamined to another.
3. **The engine actuation layer is a rebuild, not a port** (§7) — and its behaviour under RO ignition limits
   is untested by this source, which never faced them.
4. **Ignition budgeting is absent from the source.** F9I never counts ignitions, because stock engines
   relight freely. §B16.3's *"read `ignitions`, budget them, refuse a phase the budget cannot cover"* has
   **no counterpart here** and must be designed from scratch. The 3→1 handover in particular is an extra
   ignition event that F9I spends without thinking about it.
5. **`KerData.HasRecoveryReserve(stages, reserveMps)` still has no reserve value** (S48 §10.3 finding 6).
   Nothing in F9I supplies one — it does not compute a recovery Δv budget at all.

---

## 11. Sources

**Repo (authoritative, C7.1):** `docs/BUILD_PLAN.md` §B16 (§B16.1–§B16.6) · §1.4 · §14.4 · C1 / C7 / C7.1 ·
`docs/MECHJEB_MISSION_TUNING.md` PHASE 2 (§2.0–§2.6) and §10 (O4/O5, findings 2 and 6) ·
`plugin/src/pure/VehicleParts.cs` · `plugin/build/assess_flight.py:398-431` ·
`docs/FLIGHT_144114_SCREEN_AUDIT.md` (the ullage loss) · `assets/ASSET_PROVENANCE.md` (the F9I GPL-3.0
position, and the attribution style used above).

**Tier-2 external — read once, under the owner's explicit in-chat C7 exception of 2026-09-03:**

- **Falcon 9 Interface (F9I) v1.1.0**, mups3y — `https://github.com/mups3y/Falcon-9-Interface`, pinned at
  commit **`6f1486bdd4ff5ed8d3c4b6b948c6e9dcaaa59b69`** (2026-08-04). **GPL-3.0.** Method extracted from
  `Ships/Script/SPACEX/BOOSTER.ks`, `Ships/Script/SPACEX/PARAM.ks`, `Ships/Script/COMMON/GNC.ks`.
  Itself stated to be derived from the **KSP Starship kOS Interface (Janus92 / Nubro)**, GPL-3.0.
  **No code from it is reproduced in this document, by direction.**
