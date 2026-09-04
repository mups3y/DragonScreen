# EXTRACT — `DockingControl.cs` (deleted gen-2) → §B9 Phase 4's geometry gates and `speedLimit` ladder

> **[FROM DELETED GEN-2 `DockingControl.cs` — DOCK NEVER FLOWN — REFERENCE ONLY, NEVER LIVE]**
>
> Produced by register task **W21** (Wave E-9), 2026-09-04, under the owner's G6 re-verdict of 2026-09-04
> (*"docking … ALL MechJeb"*): `W21` was re-verdicted **RECOVER-CODE → RECOVER-REFERENCE**. **No `.cs` file
> was created, restored or edited by this task.** Everything below was read out of `8b81816^` with `git show`.
>
> ⛔⛔ **THE DOCK WAS NEVER FLOWN. NOT ONCE.** R1 §5.2 records `DockingControl.cs` **UNPROVEN**, and all four
> of its pure halves are `❌ NO`. `FLIGHT_CORPUS_ASSESSMENT.md`: *"Flights that completed a docking: **0**"*
> and *"No `dock_phase` value is ever set."* **This is a design record, not a validated one.** Every constant
> here carries §B16.8 ruling 2's **UN-CONVERGED** marking — **except** the two §1.4 verified-real geometry
> sources in **§1**, which carry their real citation instead.
>
> ⛔ **This document is EVIDENCE, not an instruction.** `docs/BUILD_PLAN.md` wins on any conflict (C7.1).
> **A settled owner decision governs this phase and this extract does not touch it (C1.8/C1.12): O6 (owner,
> 2026-09-03, via the overseer; §B12.3 + §B10.3) makes the MechJeb Docking Autopilot the DEFAULT from the
> Keep-Out Sphere inward**, with the crew's manual docking button overriding to the Manual ISS Docking screen
> and shutting the Docking AP down. `pure/DockControl.cs`'s 6-DOF glideslope servo is **precisely** the job
> that decision hands to MechJeb, and §4 records it as history, not as a proposal.

**Read with:** `docs/BUILD_PLAN.md` §B9 Phase 4, §B10.3 (Docking Autopilot), §B10.6 (RCS), §B11, §B12.3,
§B12.5a, §B12.8, §B14 · `docs/MECHJEB_MISSION_TUNING.md` §4.1–§4.3 ·
`docs/AUTOPILOT_RECOVERY_AUDIT.md` (R1) §5.1–§5.2 · `docs/PHASE_4_DOCKING_RESEARCH.md`.

---

## 0. What was read, and what it was

| File (at `8b81816^`) | Size | R1 verdict | Flight status |
|---|---|---|---|
| `plugin/src/DockingControl.cs` | 15,447 B | RECOVER-CODE | ❌ **NO — dock UNPROVEN** |
| `plugin/src/pure/DockApproach.cs` | 5,803 B | RECOVER-CODE | ❌ NO |
| `plugin/src/pure/DockControl.cs` | 3,276 B | RECOVER-CODE | ❌ NO |
| `plugin/src/pure/DockCorridor.cs` | 2,712 B | RECOVER-CODE — **§1.4 verified-real geometry** | ❌ NO |
| `plugin/src/pure/DockCapture.cs` | 2,822 B | RECOVER-CODE — **§1.4 VERIFIED-REAL source** | ❌ NO |

**Architecture in one line.** A waypoint FSM (`DockApproach`) whose holds *are* the §B14 crew gates, a 6-DOF
glideslope servo (`DockControl`) flying one leg at a time between them, a corridor predicate
(`DockCorridor`) that auto-aborts an off-corridor KOS penetration, and a capture predicate (`DockCapture`)
that refuses to call a fast or skewed contact a clean capture.

---

## 1. ⭐ The two VERIFIED-REAL sources — recorded unchanged from their real sources, and cited

⚠ **§1.4 applies to this section and to nothing else in this document.** These are **measurement, not
invention**. They are transcribed here from the deleted files' own text — **not from memory** — and they must
not be edited without a real-source confirmation. **MechJeb's Docking Autopilot does not supply either of
them**: it flies an approach; it does not know the IDSS envelope it has to arrive inside, nor where the
corridor boundary a CHOP/BREAKOUT verdict is judged against lies.

### 1.1 The IDSS soft-capture envelope — **IDSS IDD Rev E, Table 3.3.1.1-2**

**Citation, as the deleted file states it:** *"The envelope is the primary standard — **IDSS IDD Rev E, Table
3.3.1.1-2** (`SEQUENCE_MAP` §1A)"*, and the accessor is named `DockCapture.Idss()`. R1 §5.1 names this a
**verified-real source**.

| Initial-contact condition | Limit | Field in `CaptureLimits` |
|---|---|---|
| Closing (**axial**) rate | **0.05 – 0.10 m/s** | `MaxClosingMps = 0.10` |
| Lateral (**radial**) rate | **≤ 0.04 m/s** | `MaxLateralRateMps = 0.04` |
| Lateral **misalignment** | **≤ 0.10 m** | `MaxLateralOffsetM = 0.10` |
| **Pitch/yaw** misalignment (vector sum) **+ roll** | **≤ 4.0°** each | `MaxAngleDeg = 4.0` |
| Pitch/yaw **+ roll rate** | **≤ 0.20 °/s** | `MaxAngRateDegS = 0.20` |

⚠ **One transcription note, recorded rather than smoothed over.** The table gives closing rate as a **band,
0.05–0.10 m/s**; the code encodes only the **upper** bound (`closingMps <= 0.10`) and deliberately admits a
receding capsule — *"a slightly-receding capsule (closingMps < 0) still passes the closing bound (it is not
too FAST); the range/contact test in the glue decides whether it is actually at the port."* **The 0.05 m/s
lower bound is part of the real standard and is not enforced anywhere in the deleted code.** Anyone using
this envelope as an acceptance test should know that.

**How it was used:** KSP's own docking magnetism (`DockedSide.Docked`) stayed the **authoritative** capture
signal; the envelope was an *additional* gate on the geometric fallback, *"so a fast or skewed fly-through at
the contact tolerance does NOT count as a clean capture."* The measured quantities were: closing rate =
axial rate toward the origin; lateral rate = the perpendicular component of relative speed; lateral offset =
√(Rx² + Rz²) off the V-bar axis; angle = the docking-ring pointing error; angular rate = the vessel body rate.

**Why this survives the move to MechJeb.** The Docking Autopilot's `speedLimit` is a *clamp on commanded
speed*; it is not a statement about the geometry at first contact. **The IDSS envelope is the acceptance
criterion the §B9 Phase 4 tune converges to** — it is what "docked cleanly" means — and it is also what the
§B14 gates and the screens' verdicts are judged against.

### 1.2 The approach corridor and the Keep-Out-Sphere breach test

**Citation, as the deleted file states it:** *"Real Crew Dragon rule (`PHASE_4_DOCKING_RESEARCH` / **IRSIS** /
`SEQUENCE_MAP` §1A): inside the 200 m KEEP-OUT SPHERE the vehicle must stay within the docking-axis APPROACH
CORRIDOR; any unplanned KOS penetration OFF that corridor commands an automatic **RETREAT** (`KosRetreat`),
**never a launch escape**."*

**The geometry, exactly as encoded:**

- The corridor is a **cone about the +along-track (V-bar) axis** toward the port at the LVLH origin.
- **Outside the KOS the corridor is not enforced** — *"the R-bar climb + the WP0→WP1 swing legitimately arc
  outside it."*
- Inside the KOS: with `along = |Ry|` (distance from the port along the axis) and
  `lateral = √(Rx² + Rz²)` (offset from the axis),

  ```
  halfWidth = max( along · tan(coneHalfAngle) , minHalfWidth )
  onCorridor  ⇔  lateral ≤ halfWidth
  breached    ⇔  range < kosRadius  AND  NOT onCorridor
  ```

  — i.e. a cone with a **minimum half-width floor near the port**, so the corridor does not pinch to zero at
  contact.

| Parameter | Value | Status |
|---|---|---|
| **Keep-Out Sphere radius** | **200 m** | ✅ **REAL** — §B11 **[DOC]**: *"Keep-Out Sphere ≈ 200 m"* |
| Corridor cone half-angle | **10°** | ⚠ **NOT verified-real, and the file says so**: *"the exact SpaceX corridor half-angle is **NOT public** (`SEQUENCE_MAP` §1A honesty log); the glue passes a researched ~10° cone as a `[Tunable]` to confirm from a flown approach."* **UN-CONVERGED, never flown.** |
| Corridor minimum half-width | **5.0 m** | ⚠ **UN-CONVERGED, never flown** — no stated derivation |

⚠ **The rule that a KOS breach commands a RETREAT and never a launch escape is the verified-real part.** The
cone that decides *whether* it has been breached is a researched guess with a public-source gap, marked as
such in the original and marked as such here. The file's own mitigation: *"the nominal guidance holds the
axis (lateral → 0), so any sane cone never false-triggers the nominal path."*

**Where it was enforced, and where it deliberately was not** — this is a design subtlety worth keeping:
the breach test ran **only on the V-bar terminal legs** (toward WP2 and contact). *"NOT enforced on the
R-bar / WP0→WP1 legs (they arc through the boundary by design) — a blind check would false-abort the
corner-cut."* **A corridor monitor that is armed everywhere inside the KOS will abort the nominal approach.**

---

## 2. ⭐ The R-bar → V-bar L-approach — the waypoint ordering and the speed on each leg

**This is the direct input to §B9 Phase 4's `speedLimit` ladder** (§B10.3: *"the single most important
docking knob"*).

### 2.1 The ordering

| # | Phase | Waypoint (station LVLH: x radial + up, y along-track + ahead) | Leaves when |
|---|---|---|---|
| 1 | **WP0Hold** | **(−400 m, 0, 0)** — 400 m **directly BELOW** the station (up the R-bar) | crew **GO** at gate **G10** |
| 2 | **ToWP1** | swing onto the V-bar toward **(0, +220 m, 0)** — ahead, on the docking axis | within `ArriveTolM` of WP1 |
| 3 | **WP1Hold** | hold at WP1 | crew **GO** at gate **G11** |
| 4 | **ToWP2** | close to **(0, +20 m, 0)** | within `ArriveTolM` of WP2 |
| 5 | **WP2Hold** | hold at WP2 | crew **GO** at gate **G12** |
| 6 | **Contact** | close to the port at **(0, 0, 0)** at contact speed | range ≤ contact range → **Captured** |
| — | **Abort** | ⛔ retreat back out to **WP0** — entered from *any* phase on an unplanned off-corridor KOS breach | — |

**Four properties of the ordering, which outlive the servo that flew them:**

1. **R-bar first, then V-bar.** Approach from **below**, hold, then swing **ahead** onto the docking axis.
   This matches §B11's *"Dragon approaches from behind and below, then loops to ahead"* **[DOC]**.
2. **Every waypoint is a station-keeping HOLD released only by a crew GO** — *"which is what makes the
   approach abortable at every step."* Those three holds **are** the §B14 crew gates G10 / G11 / G12; the
   controller flew **one leg at a time** and called `PhaseComplete()` on arrival so the gate could release
   the next. ⚠ This coupling is why §B9 P4 is a *gated* phase, not a single autopilot run.
3. **The capsule points its docking ring at the port the whole time** (`AimLvlh` is always the unit vector
   toward the station), attitude first, then translate.
4. **Abort = retreat to WP0**, back out along the approach — never an escape.

⚠ **A discrepancy inside the deleted code itself, recorded as found.** `pure/DockApproach.cs`'s field
comment says WP1 is *"~200 m in FRONT"*, while the glue's `WaypointFor` passes **220 m**. **§B11 [DOC] says
220 m**, so the glue is right and the pure file's comment is stale. The plan's 220 m wins (C7.1).

### 2.2 The speeds — the `speedLimit` ladder input

The deleted design had no per-leg speed table. It had a **single range-tapered clamp**, applied to the total
demand:

```
speedCap(range) = contactSpeed + (farSpeed − contactSpeed) · clamp(range / taperRange, 0, 1)
```

| Parameter | Value | Status |
|---|---|---|
| `ContactSpeedMps` | **0.08 m/s** (8 cm/s) | ⚠ **UN-CONVERGED, never flown** — and see the warning below |
| `FarSpeedMps` | **20.0 m/s** | ⚠ **UN-CONVERGED, never flown** |
| `TaperRangeM` | **400 m** | ⚠ **UN-CONVERGED, never flown** — note it equals the WP0 distance |

**Resolved against the waypoints** (linear taper from 400 m):

| At | Range | Cap from this formula | §B10.3 / §B11's ladder |
|---|---|---|---|
| WP0 | 400 m | **20.0 m/s** | ~1 m/s (far / KOS approach) |
| WP1 | 220 m | **~11.0 m/s** | ~0.3–0.5 m/s (waypoints) |
| WP2 | 20 m | **~1.1 m/s** | ~0.3–0.5 m/s |
| 5 m | 5 m | **~0.33 m/s** | ⛔ §B11 **[DOC]**: rate must stay **< 0.2 m/s inside 5 m** |
| contact | 0 m | **0.08 m/s** | ~0.1–0.2 m/s |

⛔ **This is the single most important finding in §2, and it is a warning, not a template. The deleted
taper is roughly an order of magnitude too fast everywhere except at contact, and it violates §B11's
documented `< 0.2 m/s inside 5 m` rule at 5 m.** It was never flown, so nothing caught it. **Do not port the
taper.** What it usefully supplies to the tune is the *shape* of the argument — a monotone clamp that falls
with range so the approach stays slow and abortable — and the observation that a **linear** taper anchored
at the far value spends almost the whole corridor near the far speed. §B10.3's **stepped ladder** (~1 →
0.3–0.5 → 0.1–0.2 m/s, set at each gate) is the better instrument, and this is the evidence for why.

⚠ **The 0.08 m/s contact speed is very likely a cargo-Dragon BERTHING figure.** §B11 marks the 7.6 cm/s →
5 cm/s numbers as **cargo-Dragon berthing** and says explicitly they are **not to be used as crew-docking
targets**; crew Dragon's documented final contact is **~0.1 m/s**. 8 cm/s sits squarely in the berthing band.
**Recorded as a suspected provenance error in the deleted code** — the plan's 0.1–0.2 m/s wins (C7.1).

### 2.3 The other approach tunables

| Name | Value | Status |
|---|---|---|
| `ArriveTolM` | **10.0 m** | ⚠ UN-CONVERGED, never flown — "reached this waypoint" |
| `ContactTolM` | **0.4 m** (`const`, glue) | ⚠ UN-CONVERGED, never flown |
| `DockApproach.ContactRangeM` | **0.3 m** (`const`, pure) | ⚠ UN-CONVERGED, never flown. **Note it disagrees with the glue's 0.4 m** — the pure FSM and the glue used *different* contact ranges; recorded as found |
| `KosRadiusM` | **200.0 m** | ✅ real (§1.2) |
| `CorridorConeDeg` / `CorridorMinHalfWidthM` | **10.0° / 5.0 m** | ⚠ researched / underived (§1.2) |

---

## 3. The KOS auto-abort path, in one place

Because it is a safety behaviour and the geometry above is meaningless without it:

1. The breach test runs **only on the V-bar terminal legs** — when the next gate is `WP2DockGoG12`, or on the
   contact leg (§1.2).
2. On a breach: log, **release translation**, and `FlightDriver.RequestAbort()` — which routes through the
   abort responder to **`KosRetreat`**, *"since we are near the station"*. ⛔ **Never a launch escape.**
3. It is **latched once** (`kosAbortRaised`) so it cannot re-fire.
4. In the pure FSM the same rule appears independently: any phase other than `Abort`/`Captured`, inside the
   KOS with `CorridorOk == false`, transitions to **`Abort`** with the target set back to **WP0**.

⚠ **Under O6 the Docking Autopilot flies the approach, so this monitor becomes a *supervisor*, not a
controller** — and §4.1 of `MECHJEB_MISSION_TUNING.md` names the reason it is still needed: the Docking
Autopilot's own `WRONG_SIDE_*` recovery steps *"will fly the vehicle **around** the target, which in RSS/RO
near the ISS is exactly the manoeuvre the Keep-Out Sphere forbids."* **A KOS monitor with the corridor
geometry above is the thing that catches that.**

---

## 4. The 6-DOF glideslope servo — history, not a proposal

⛔ **UN-CONVERGED and NEVER FLOWN, in full.** `pure/DockControl.cs` is a per-axis position+velocity servo in
the station LVLH frame: for each of radial / along / cross it commands a velocity toward the target
(`vCmd = −KPos · err`), clamps that to the range-tapered speed cap of §2.2, and closes the velocity error
(`accel = KVel · (vCmd − rate)`). The gains are **`KPos = 0.1`, `KVel = 1.0`** — first-cut tunables the file
itself marks *"validated from the CSV"*, against a CSV that was never recorded. The world demand was mapped
to Draco translation through a sign map **`RcsRightSign = RcsUpSign = RcsFwdSign = −1.0`**, which is the one
part of this file that is genuinely *derived* rather than guessed: it replicates
`MechJebModuleRCSController.Drive`'s frame convention (`s.X = local.x`, `s.Y = local.z`, `s.Z = local.y` — the
y/z swap), taken uniformly negative off the acceleration demand, and anchored to the flight-131412
confirmation of `s.Z = −Dot(A, up)`; the file records that the previous `+1/+1` on right/up were *"unreasoned
defaults that INVERT those axes → the servo pushes lateral error the wrong way → docking diverges off the
corridor."* **All of this is exactly the job O6 hands to MechJeb** (§B10.3 / §B12.3), whose RCS controller is
where the sign convention came from in the first place — so it is recorded as a design record and nothing
here is a proposal to fly it.

**The one durable lesson:** a lateral-axis sign error in a translation servo does not fail loudly — it
**diverges off the corridor**, which is precisely what §1.2's monitor exists to catch. If a MechJeb docking
approach ever walks sideways out of the corridor, suspect the frame convention before the gains.

---

## 5. `NavFilter` in the terminal — never exercised, and its constants are regime-unstated

`DockingControl` wired the same `pure/NavFilter.cs` the rendezvous used, but with the measurement 1σ
**range-scheduled** through `NavFilter.TerminalSensorNoiseM(range)` — rel-GPS far, **DragonEye LIDAR/optical
near**, blended linearly across the band — *"so the noise collapses to cm-class in close and never wrecks the
dock."* The servo then flew on the **estimate**, not on truth, matching the real pipeline.

⛔ **Read those constants in the order `EXTRACT_RENDEZVOUS_CONTROL.md` §1.5 records them, and do not
re-derive them here.** Every `NavFilter` noise tunable is an **R1 §7.4 REGIME-UNSTATED defect** — attributed
in the file only to *"typical spec (`docs/CREW_DRAGON_GNC_RESEARCH` §1)"*, with **no named source and no
regime** — and §0.1 requires a regime before a number is used. The ones this path would have used:
`RgpsNoiseM` **5.0 m**, `LidarNoiseM` **0.02 m**, handoff band `LidarHandoffFarM` **1,000 m** /
`LidarHandoffNearM` **200 m** — **each regime UNSTATED, source not named**. The filter was **never exercised
in flight on this path or any other**.

The *design shape* is what is worth keeping: a **range-scheduled sensor handoff** is the right model for a
vehicle that docks on LIDAR and navigates on rel-GPS, and the handoff band happens to coincide with the KOS
(200 m) and the Go/No-Go hold (~1 km) — which is a coincidence worth noticing rather than a derivation.

---

## 6. What this line records for other tasks

### 6.1 ⚠ `DockingOps` is **T20's** to flip — this line flips nothing

§B12.5a gives **`DockingOps.Engaged` / `.Note`** to **T20** (the docking hand-off + the `speedLimit`
ladder): the Docking Autopilot as DEFAULT per **O6** / §B10.3, plus the manual-docking-button override that
shuts it down. **W21 changed no facade property and touched no `.cs` file.**

⛔ **And when `DockingOps` does go live, it must not be read as "docking works."** The dock has never been
flown, by this code or any other. A live facade means *a controller is behind the lamp* — nothing more.

### 6.2 Cross-references

- **W20** (`EXTRACT_RENDEZVOUS_CONTROL.md`) — the shared `pure/NavFilter.cs` regime marking (§5), and the
  rendezvous half that hands off at the KOS. W20's §2.4 records that the hand-off point itself
  (`AiRangeM` 7,500 m vs §B11's ~1 km Go/No-Go hold) is **asserted, not derived**.
- **§B14** owns gates G10 / G11 / G12; this extract records only that the three waypoint holds *are* those
  gates.
- ⛔ `git status` shows **no `.cs` file touched** by this task.

---

## Open questions for the owner

### Q1 — This extract went to `docs/EXTRACT_DOCKING_CONTROL.md`, not to `MECHJEB_MISSION_TUNING.md` §4.1–§4.3

**Situation.** W21's register line says *"Where it goes: `docs/MECHJEB_MISSION_TUNING.md` §4.1–§4.3 … with
the two verified-real sources in their own clearly-cited block."* The owner-authorised batch instruction of
**2026-09-04** instead directed one self-contained `docs/EXTRACT_<name>.md` per task, explicitly so a batch
stopping mid-way leaves no half-written shared document. This session followed the batch instruction and
**did not edit `MECHJEB_MISSION_TUNING.md`** (C1.11). §1 above is the clearly-cited verified-real block the
line asked for, kept separate from every un-converged number. The same question stands on W20 and W18.

**Options.**
1. **Leave it as it stands** — self-contained, reachable through `INDEX.md`.
2. **Open one small [S] line covering the whole batch**, to fold each extract's tuning-relevant sections into
   `MECHJEB_MISSION_TUNING.md` once all five exist. *(This chat's recommendation.)*
3. **Move each extract wholesale into the tuning doc**, leaving pointer stubs (nothing deleted, C1.16).

**Recommendation: option 2**, with one addition specific to this line: **§1's IDSS envelope arguably belongs
in the tuning doc regardless of what happens to the rest**, because it is the acceptance criterion §B9 Phase
4 converges to and a tuning session will look for it there.

### Q2 — The deleted contact speed (0.08 m/s) looks like a cargo-**berthing** number. Is that worth a correction note?

**Situation.** §2.2: the deleted servo's contact speed is **0.08 m/s**. §B11 marks **7.6 cm/s → 5 cm/s** as
**cargo-Dragon BERTHING** figures, explicitly *"not to be used as crew-docking targets"*, and gives crew
Dragon's final contact as **~0.1 m/s** with a hard **< 0.2 m/s inside 5 m**. 0.08 m/s sits in the berthing
band, and the deleted code cites `PHASE_4_DOCKING_RESEARCH` without distinguishing the two. It never flew, so
nothing caught it. **This extract records it as a suspected provenance error and applies C7.1** (the plan
wins); it changed nothing.

**Options.**
1. **Record-only**, as now — the plan already states the right number and no live code uses the wrong one.
   *(This chat's recommendation.)*
2. **Open an [S] line** to add an explicit "berthing vs docking" warning note into
   `MECHJEB_MISSION_TUNING.md` §4.1 next to the `speedLimit` ladder, so the confusion cannot recur when the
   ladder is tuned.
3. **Re-check `PHASE_4_DOCKING_RESEARCH.md`** to establish whether the deleted code misread it or whether the
   research itself conflates the two — a research-integrity question, not a tuning one.

**Recommendation: option 1, with option 2 if the owner wants belt-and-braces.** The trap is a real one — the
same conflation reached working code once already — but §B11 already carries the warning in the authoritative
document, which is where a tuning session should be reading from.

### Q3 — The IDSS closing-rate **lower** bound (0.05 m/s) is in the standard but was never enforced

**Situation.** §1.1: IDSS IDD Rev E Table 3.3.1.1-2 gives axial closing rate as a **band, 0.05–0.10 m/s**.
The deleted `DockCapture.WithinEnvelope` enforces only the upper bound and deliberately admits a *receding*
capsule, on the reasoning that "too slow" is not a capture hazard the way "too fast" is, and that the range
test decides whether contact happened at all. Whether that reasoning is right depends on the soft-capture
mechanism: below some closing rate the ring and latches may not actuate.

**Options.**
1. **Keep the upper-bound-only test** and record the lower bound as documentation, as §1.1 now does.
2. **Treat 0.05 m/s as a real lower gate** in whatever eventually judges capture, so an approach that stalls
   short is flagged rather than silently held.
3. **Leave it open until a dock is actually flown** — the first real capture will show whether a slow
   approach captures or bounces.

**Recommendation: option 3, with §1.1's documentation standing in the meantime.** The dock has never been
flown; enforcing a bound whose consequence nobody has observed would be inventing a behaviour. This is a
§B5/T22 empirical item and an owner call, not a build-chat one.
