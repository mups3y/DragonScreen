# ART_SPEC_DRAGON — the Crew Dragon vehicle schematic, as code-drawn

*Written by **S65**, 2026-09-04. Deliverable of the "redraw the Dragon PROPULSION schematic" task.*
*Status: CURRENT. Governs `plugin/src/pure/PropSchematic.cs`. Subordinate to `docs/BUILD_PLAN.md` (C7.1).*

This file exists so no later task re-derives the vehicle drawing from scratch, and so the licence and
source-tier reasoning behind it survives the chat that produced it.

---

## 0. The licence line — read before touching this

The arrangement reference is a **commercial third-party SpaceX blueprint poster**.

**Owner decision, 2026-09-04, via the overseer: REDRAW — NEVER CROP, NEVER TRACE-COPY, NEVER COMMIT
THE IMAGE.**

- The image is **not in this repo and must never be added to it** — not under `docs/`, not under
  `assets/`, not "just as reference". DragonScreen is publicly distributed and becomes GPLv3 once
  MechJeb is embedded (§B2/§B3), and the repo is public, so committing the poster is redistribution.
- What was used is the **written element list and arrangement** — part names, counts, which part sits
  where. Those are facts, not protected expression.
- Every line in `PropSchematic.cs` is our own geometry, authored from that written spec. Nothing is
  cropped, traced, or derived pixel-wise.
- The file header carries the same statement; keep the two in step.

**A future task must not "improve accuracy" by fetching the poster.** C7 already puts external URLs
off-limits as build sources. If more arrangement detail is genuinely needed, it is an owner question
(C1.14), not a download.

---

## 1. The two sources, and exactly what each one is allowed to evidence

| | Source | Tier | What it evidences | What it does NOT evidence |
|---|---|---|---|---|
| **(a)** | Real SpaceX Prop page, JSC crew-training photo `jsc2026e404727` (Crew-13) | **TIER 1** | The page LAYOUT: the Dragon in **horizontal profile**, ringed by Draco thruster-cluster arc symbols with per-cluster firing/status, a per-thruster data band along the bottom, a left alert/sub-nav rail | Any on-screen string (§11b: "exact on-screen text is NOT transcribable"); any vehicle geometry |
| **(b)** | The blueprint poster (NOT in repo) | **TIER 2, marked** | The ELEMENT LIST and the ARRANGEMENT — which part sits at which axial station, and the radial clocking, from its three axial views (FRONT VIEW · FRONT VIEW WITHOUT NOSECONE · CAPSULE REAR VIEW) | **Any dimension.** Two versions of it disagree with each other on overall length and neither matches the vehicle |
| **(c)** | `docs/reference/craftdump.csv`, `docs/BUILD_PLAN.md` §8 / §B11 | **TIER 1 (repo)** | Part inventory and mission facts: NDS docking system, PICA-X heat shield, Dragon 2 trunk with lifting surface + 2 solar panels + active radiator, the claw umbilical, NTO/MMH/helium | Any linear dimension — see §4 |

**(a) wins on layout. It is horizontal, and it stays horizontal.** Orientation is settled; a future
chat that thinks a vertical elevation would be better raises it under §6, it does not switch.

---

## 2. Element inventory — the callouts, verbatim

Drawn on the glass, in the reference's own wording:

```
NOSECONE · 16x DRACO THRUSTERS · WINDOWS · ENGINE POD · 8x SUPERDRACO ENGINES · UMBILICAL · FIN
```

Seven callouts, seven leader lines. Nothing else gets a callout. The heat shield (the capsule's blunt
base), the trunk (the cylindrical body below the capsule, carrying the fins) and the docking mechanism
are **drawn but not labelled** — they are definitions in the task spec, not entries in the callout list,
and adding words the reference does not carry would be inventing labels (§1.4).

**Counts** — the poster's element list and `craftdump.csv` agree, and so does the Prop tab's own
existing checklist text, which the drawing now matches instead of contradicting:

- **16 Dracos, in 4 quads of 4.**
- **8 SuperDracos, in 4 pods of 2**, in raised sidewall fairings.

---

## 3. The geometry, as drawn (all in `PropSchematic`'s 3427 × 2112 reference frame)

Nose **LEFT**, trunk **RIGHT**, roll axis at `HullCY = 725`.

| Feature | Where | Notes |
|---|---|---|
| Nosecone | x 1225 → 1351, half-height 22 → 85 | Blunt hinged cover, 3 segments per side, capped at the tip. `ShoulderX` (1351) is the hinge line. |
| Docking mechanism | x 1308 → 1346 at ±58 | The NDS ring seen **edge-on under the closed cover** — drawn in `Hairline`/`Faint` as hidden detail, never as if visible from outside. Poster evidence: FRONT VIEW WITHOUT NOSECONE. |
| Capsule sidewall | x 1351 → 1480, half-height 85 → 129.5 | Straight cone, half-angle ≈ 19°. It stops where the fairing takes over the silhouette. |
| Draco quad station | x 1362 → 1432, at ±71 from the axis | **Forward shoulder**, just aft of the hinge — the accuracy fix. Two quads project above the axis and two below, both **inboard of the silhouette** (see §3.1). Four nozzles on each pod's outer face. |
| Windows | x 1445 → 1535, at ±68 | Four, on the near face, outlined. A window off the roll axis projects inboard of the silhouette — that is why they sit inside the outline. |
| Engine-pod fairings | x 1480 → 1584, standing 30 proud | **Over their span the FAIRING is the outline, not the bare cone** — drawing both produced a floating ridge in the first cut. Two canted nozzle stubs per fairing = the eight SuperDracos. |
| Near-side engine pod | x 1486 → 1594, ±34 | The az-0 pod seen face-on, projected onto the roll axis, drawn as the same hexagonal pod outline in `Faint`. The fourth pod is hidden behind the vehicle and is not drawn. |
| Heat shield | rim x 1603 at ±172, apex x 1633 on axis | Convex **aft**, three segments per side. Sagitta / diameter ≈ 0.087, which is a spherical cap of radius ≈ 1.3 × diameter. |
| Umbilical (claw) | box x 1610 → 1690, at +172 → +204 | Bridges the heat-shield plane and the trunk's forward ring. §8: the claw is the trunk↔capsule thermal / power / avionics link. |
| Trunk | x 1653 → 1971, ±172 | Panel joints above the split line = the solar-array half; two loop lines below it = the radiator half (§8). |
| Fins | x 1893 → 1971, flaring ±172 → ±204 | Two stand on the silhouette. **No fin count is stated on the glass** — the callout is "FIN", as in the reference. |

### 3.1 The radial clocking — why the pods sit where they do in profile

Azimuth is the existing instrument convention: **0° at twelve o'clock, increasing clockwise, viewed
LOOKING FORWARD.** The side view puts the observer at azimuth 90°, so a feature at azimuth *a* projects
onto the vertical at `R·cos a`:

- **Engine pods at 0 / 90 / 180 / 270** → one on the top silhouette, one on the bottom, one face-on
  (projecting onto the roll axis), one hidden.
- **Draco quads at 45 / 135 / 225 / 315** (`QAz`, unchanged) → two project at `+0.707R`, two at
  `−0.707R`. **None reaches the silhouette**, which is why the pods are drawn inboard of the outline
  rather than as bumps on it. The first cut drew them as bumps; that was wrong.

### 3.2 The axial key — the element the profile cannot supply

A profile can show the axial station and nothing about the clocking, and the clocking is what the owner
raised. So the drawing carries a **looking-forward section** below the vehicle: the hull circle, the
docking ring inside it, the four engine-pod marks at 0/90/180/270, and the four Draco quad marks at
45/135/225/315 with their letters.

The four QUAD A–D indicator rings are placed at the page's four corners **in clock order** — A
upper-right, B lower-right, C lower-left, D upper-left — so their position on the page *is* their
position around the hull, and the key confirms it against the circle. Each quad mark in the key is lit
by the **same `QuadDuty(s, q)`** that lights its ring, so the two can never disagree.

---

## 4. No dimension is asserted, and that is deliberate

⚠ **No number in this drawing comes from the poster, because the poster is not allowed to evidence
one** (§1): two versions of it disagree on overall length (~66 M vs 60.0 M) and neither matches the
real vehicle.

The three authorities the task names — §8 flight facts, §B11 flight-data targets, and
`docs/reference/craftdump.csv` — were searched, and **none of them carries a linear dimension for
Dragon.** §8 gives the vehicle's *structure* (pressurised + service + nose-cone sections, trunk = half
array + half radiator, 8 SuperDracos) but no metres. §B11 is ascent / approach / entry / chute numbers.
`craftdump.csv` is parts, resources and module fields — masses and resource quantities, no geometry.

So:

- The profile is drawn to **proportion only**: capsule : trunk : diameter in the vehicle's own ratio to
  each other, sized to fit the panel.
- **No metre figure is drawn on the glass, and none is claimed in the code.** Every number on the Prop
  page comes from `PageState`.
- The previous file header claimed "4 m across, a 4.4 m capsule and a 3.7 m trunk". Those figures have
  no authority anywhere in this repo, so S65 removed the claim rather than propagate an unsourced
  number. The *ratio* they express is what the drawing keeps.

---

## 5. What must not regress

- **`ThrusterDuty()` / `QuadDuty()` / `MaxDuty()` are the live model and were not touched by S65.**
  Every lit segment is the live RCS demand (`PageState.TransX/Y/Z`, `RotPitch/Yaw/Roll`, off
  `FlightCtrlState` in `VesselData`) gated by the real RCS action group (`PageState.RcsOn`), resolved
  onto the pod that would answer it. `VesselData` derives the "Draco Duty" readout from `MaxDuty` — the
  same function — so the number and the rings are one signal. **§14.4(f): a live thing never regresses
  to static art.**
- **Vector, and rendered twice.** Everything goes through `pure/DisplayList.cs` and must draw
  identically in `ScreenPainter` (GL, in-game) and `preview/PreviewMain` (GDI+, PNG). **No bitmaps.**
- **Command budget.** The Prop page renders at **286 commands** in the firing state. The ceiling is
  `FigmaUI.Commands` = 360 in-game and `VehicleSubsystemPage.Commands + 60` = 360 in the preview and the
  tests. `PropSchematic.Commands` is declared 300. A future edit that adds detail must re-check the
  preview's printed command count and the `OVERFLOWED` warning.
- **The eight Vehicle sub-tabs, `PanelMap.cs` and the label docs are not this drawing's business**
  (C1.4). Neither is the 3D-render / turntable workstream — owner: *"3D renders are to be left alone."*

---

## 6. Open questions for the owner

### Q1 — Is the 45° clocking between the Draco quads and the engine pods correct?

**Situation.** The drawing places the four SuperDraco engine pods at azimuth 0/90/180/270 and the four
Draco quads at 45/135/225/315 — i.e. the quads bisect the pods. That interleave is what
`PropSchematic`'s existing `QAz` array already assumed, and it is the arrangement the S65 task spec
describes from the poster's axial views. **But the build chat could not read the poster** — it is not in
the repo and must not be added (§0, C7) — so the clocking is asserted, not verified. It is marked
tier-2 in the file header and on the glass ("QUADS CLOCKED / 45 DEG FROM PODS").

**Options.**
1. **Confirm 45° and leave it.** *(Recommended.)* Four 4-fold-symmetric pod sets on one hull almost
   always interleave, the existing code already assumed it, and nothing downstream depends on the exact
   number — `ThrusterDuty` resolves demand onto whatever azimuths `QAz` holds.
2. **Owner reads the real clocking off the poster and states it here**, and a follow-up register line
   edits `QAz` + the axial key to match. Cheap: one array and one comment.
3. **Drop the clocking claim** — remove the on-glass note and draw the quad marks without asserting a
   relationship to the pods. Loses the thing the owner actually asked for (where the quads are).

**Recommendation: 1**, with 2 held open as a cheap correction if the owner sees otherwise on the poster.

### Q2 — Should the fin COUNT be stated?

**Situation.** The callout is "FIN", exactly as the reference has it, and the profile draws the two
fins that stand on the silhouette. The real number of trunk fins is not evidenced by anything in the
repo (`craftdump.csv` shows the trunk carries a `ModuleLiftingSurface`, not how many fins the model
has), and the poster is a dimension-and-count source we are only allowed to read arrangement from.

**Options.**
1. **Leave it as "FIN", no count.** *(Recommended.)* Honest, matches the reference wording, and the
   page already carries counts for the two things that *are* evidenced (Draco ×16, SuperDraco ×8).
2. Owner supplies the count; a follow-up line adds "n× FIN" and draws them at the right clocking.
3. Guess a count from the render/model. **Rejected** — that is inventing a §1.4 fact.

**Recommendation: 1.**

### Q3 — Are the callout labels big enough on the glass?

**Situation.** The seven callouts are drawn at reference size 22, the same size as the existing
per-thruster data band below them (shipped since T9). At 2560 px that is ~15 px. It reads cleanly in
the preview PNG, but the preview is not the capsule, and S38 has already shown once that this console is
viewed obliquely and that judgements made on a PNG can be wrong on the glass.

**Options.**
1. **Ship at 22 and judge it on the next glass pass.** *(Recommended.)* It matches the page's existing
   smallest text, so if 22 is too small the fix is a page-wide one, not a schematic-only one.
2. Bump the callouts to 26 now. Costs horizontal room — "8× SUPERDRACO ENGINES" is already the widest
   label in its row — and would force a re-layout of the bottom callout row.
3. Cut callouts to fewer, larger words. Loses reference wording (§2).

**Recommendation: 1**, and note it on whatever glass-time task comes next. `install` + glass time are
separate owner gates (C1.12) and S65 did not touch them.

---

## 7. Verification S65 ran

- `python plugin/build.py test` — **ALL SUITES PASSED**, including
  `FigmaUINavTest.PropSchematicDuty()`, which is the guard on the live duty model (§5).
- `python plugin/build.py preview` — no `OVERFLOWED` warning on any Prop render.
- PNGs inspected at full page and at 2× crop: `ui_vehiclepropulsion.png` (idle / RCS DISABLED),
  `ui_vehiclepropulsion_firing.png`, `ui_vehiclepropulsion_kerabsent.png`,
  `ui_vehiclepropulsion_alerts.png` (unchanged — the ALERTS view does not use this drawing).
- Per-page QC: every string inside its box, no overlap, no clipping, no leader crossing another leader
  or running inside a solid, and the axial key's A/B/C/D letters agree with the corner rings' placement.
