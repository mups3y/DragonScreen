# DragonScreen — Task Register

The living task list for the build. **One task at a time** (C1.1): the first non-DONE line below IS the task.
`/next` reads this file. Full detail for every task: `docs/BUILD_PLAN.md` (Part C = the protocol, §1–§14 =
Part A research, §B1–B15 = Part B research).

**Status:** `TODO` · `DOING` (at most one) · `DONE` · `NEEDS-WORK` (+ one-line note).
**Model:** `[O]` = Opus · `[S]` = Sonnet (C3). Escalate [S]→[O] if a task stalls; never downgrade an [O].

⚠️ **LIVING** (C5): split any task that won't finish before compaction; append stray findings at the bottom;
never reorder past a DONE without a note.
🟢 **PREVIEW-ONLY BUILD-GO** — owner, 2026-09-02 (recorded by T4; supersedes the blanket BUILD-HOLD the
banner used to carry, which T2/T3/T4 had already outrun). Part A **pure code + `python plugin/build.py test`
+ `python plugin/build.py preview` are cleared**. `python plugin/build.py install` and glass time are NOT:
they still need a separate, explicit owner go, so a task whose done-criteria can only be met in the capsule
(S10's RT planet camera, T10's audible click, T11's drag-rotate) stops and asks rather than installing.
Part B (T15–T22) remains DESIGNED, not started.

---

## Setup

### T0 [O] Scaffold + consolidate (C7) — **DONE**
- **Read:** C1 / C2 / C4 / C7.  **Build:** (1) copy plan → `docs/BUILD_PLAN.md`; (2) copy tuned Crew-2 cfg →
  `docs/reference/mechjeb_settings_type_Crew-Dragon.cfg`; (3) APPEND C1 rules (1–11) to existing `CLAUDE.md`;
  (4) create `REGISTER.md` from C4; (5) create the `/next` skill from C2.
- **DONE 2026-09-02 (by the overseer, one-time bootstrap):** plan + cfg copied byte-identical (`cmp` clean);
  `CLAUDE.md` appended 40→95 lines, original untouched; `/next` skill at `.claude/skills/next/SKILL.md`; no
  memory written (C1.11). Docs/harness-only → build/preview gate N/A; baseline `build.py test` logged below.
  See stray findings S1–S3. Owner to commit via GitHub Desktop.

---

## Part A — screens (§7 order, with this session's decisions)

### T1 [O] Docs reconcile (C7.1) — **DONE**
- **Read:** `BUILD_PLAN.md` + §14 + EVERY `docs/` file.  **Build:** update or mark `SUPERSEDED — see
  BUILD_PLAN.md` any doc conflicting with the plan (autopilot re-introduction, panel lighting NO-red, inert
  inferred controls, §14.1 numbers); mirror new screens + decisions into `SCREEN_INVENTORY.md` + the map
  artifact; fix `INDEX.md` (incl. S1, S3).  **DONE when:** no `docs/` file contradicts `BUILD_PLAN.md`; INDEX
  current.  **May SPLIT** per doc-group.
- **DONE 2026-09-02 — the stated DONE-when is met.** All 20 `docs/` files + the reference cfg read end-to-end
  against the plan. `INDEX.md` REBUILT (it catalogued **56 files deleted in the 2026-09-01 pivot** and omitted
  5 that exist; now every entry is checked against the tree, with a "deleted — don't resurrect" section so a
  grep for an old name ends there). Reconcile banners naming exactly what the plan overrides on:
  `SCREEN_SPEC` · `COMMAND_REGISTRY` · `TELEMETRY_REGISTRY` · `SCREEN_EVIDENCE_MATRIX` ·
  `SCREENS_CONSOLE_PLAN` · `SCREENS_LOOK_AND_FUNCTION_RESEARCH` · `MAP_MFD_RESEARCH`; HISTORICAL banners on
  `ARCHITECTURE` · `STATE_CONTRACT` · `FLIGHT_144114_SCREEN_AUDIT`; checked-clean notes on `IVA_TARGET` ·
  `COVER_PAGE_ASSETS`. **Corrections made in place:** `REAL_DRAGON_SCREENS` button lighting grey→white→**red**
  struck and replaced with §14.4(a) (bright / NO red / audible click) + the §14.4(b) inert list — *labels
  untouched per C1.4*; `SCREENS_CONSOLE_PLAN`'s "educated-guess any inert button" and "red dash on refusal"
  struck; `REFERENCE_PAGES`' nine subsystem tabs → the real **eight** (tier-1 `ui1.jpg` beats the tier-2
  Figma); `MAP_MFD_RESEARCH`'s "V1 BUILT + INSTALLED" corrected — `ScaledPlanetRenderer.cs`/`PlanetGeom.cs`/
  `ImageId.ScaledPlanetLive` **never existed in git history**; `COVER_PAGE_ASSETS`' "renderer not wired" (it
  is). `SCREEN_INVENTORY` mirrors §14.4 (a–d), §14.1's numbers, the §11b JSC screens (**#26 prop thruster
  schematic · #27 systems tree · #28 circular nav plot**) and the §3 REFINE/DESIGN-SET classes. **S1 closed:**
  `CLAUDE.md`'s header no longer says the autopilot is gone-and-stale — it now separates the *deleted
  implementations* from the *idle stubs Part B fills* (a mid-task check caught that `AbortControl`,
  `MissionConductor`, `Actuator`, `MissionOps`, `Fdir` and a display-only `AuthorityManager` still exist on
  purpose; an unqualified "remove it" would have broken the build). `ASSET_INDEX.md` regenerated. Docs-only
  task → **the preview/PNG gate does not apply** (no code changed); `python plugin/build.py test` run anyway
  as a no-regression check: **green, 3552 checks, 0 failed**. Artifact half split out → **S9**
  (publish blocked; owner chose to keep the page's source in the repo at `docs/reference/dragon_screen_map.html`).

### T2 [S] Menu nav-index (`UiPage.Menu`) — **DONE**
- **Read:** §14.4(c) + `FigmaUI.cs`.  **Build:** grid/list of all pages.  **DONE when:** preview + nav test.
- **DONE 2026-09-02:** new `plugin/src/pure/MenuPage.cs` — a 3×9 grid of the other 27 `UiPage`s (every
  entry but Menu itself), each card labelled with the page's real title (`FigmaUI.Name`, the same
  string every page's own chrome already uses — no invented copy). Wired into `FigmaUI.Build`
  (`UiPage.Menu` case) and `FigmaUI.HitTest` (a card hit → `NavHit.Go` to that page); `MenuPage.CellRect`
  is the one source of truth Build, HitTest and the test all share, so the drawn grid and the hit grid
  can't drift, matching the `VehicleTabBar.CentreX` / `Card.TabRect` precedent already in the codebase.
  Back is the existing global bottom-bar Cover icon (`ActiveBarIcon`'s default case already covered
  Menu as "everything reached from Cover"); Cover's own Menu button already routed here (`MapCover`).
  **Nav test:** `FigmaUINavTest.Menu()` — asserts the entry count/no-self-reference, every one of the 27
  cards routes to its real page, a tap in the gap between cards is inert, and the bottom-bar back route
  + the Cover→Menu button both resolve correctly. `python plugin/build.py test`: green, Figma UI nav
  suite 114 checks, 0 failed; 3864 checks total across all suites, 0 failed.
  **Preview:** `ui_menu.png` inspected — 27 legible cards (longest title "COAST TO TRUNK JETTISON" sits
  with wide margin inside its card at this grid size), same Panel/Hairline chrome and bottom bar as
  every sibling page, Cover icon lit in the bar (Menu's back destination). §14.4(c) respected: layout
  is ours (an owner-sanctioned reconstruction, not invented unilaterally); content is real (reused
  titles). No `PanelMap.cs` / label-doc edits.

### T3 [S] Reference Content view (Cover `PhaseReference`) — **DONE**
- **Read:** §14.4(c) + §8 + `CoverPage.cs`.  **Build:** deorbit quick-ref.  **DONE when:** preview.
- **DONE 2026-09-02:** Reference Content is a rail slot IN the Cover page (index 5), not a standalone
  page — confirmed via `FigmaUI.MapCover` (only Menu/Settings/PhaseManual navigate away; the rest,
  including PhaseReference, select in-page and return `NavHit.None`). Before this task, `CoverPage.Build`
  drew the SAME baked panel body for all 7 rail phases (only the heading + rail highlight moved) — the
  body content was always the one phase the Figma export happened to bake ("Coast to Trunk Jettison"),
  so selecting "Reference Content" showed Coast's crew-interrupt/procedure rows under a mismatched
  heading. Fixed in `plugin/src/pure/CoverPage.cs`: added `ReferenceSkipKeys` (the ~23 baked asset keys
  that are Coast-phase-specific body content) skipped only when `sp==ReferencePhase(5)`; the three real
  Figma card backgrounds (`rectangle_179/180/181`) and all chrome (top bar, rail, globe, bottom bar,
  camera/target readouts) stay — only the body TEXT swaps. Added `DrawReferenceContent` (a `Card` local
  helper) drawing three real-data sections in those same three card slots: **ENTRY TIMELINE** (undock →
  trunk jettison → deorbit burn ~15 min → claw separation ~1h20m before splashdown → nose cone close &
  lock → entry interface → drogues/mains at ~2 km → splashdown T+50 min from burn start) and
  **PARACHUTES (MARK 3)** (2 drogues then 4 mains at ~2 km; land under ≥3; CUT MAINS after splashdown),
  both straight from §8 Return/deorbit + Parachutes; **CONTINGENCY** (EJECT — SuperDraco abort 8 modes;
  WATER DEORBIT/DEORBIT NOW — contingency immediate deorbit, water landing norm, 7 sites; deorbit
  go/no-go ~30 min before claw-sep prep) from §4's CONFIRMED-real panel-function list + §8's timing —
  no invented numbers (§1.4). The baked content-panel hairlines (`Lines[]`, all 10 are dividers within
  the Coast-phase body) are skipped on this phase too, since they'd cut across the new text at the wrong
  spots. **Preview:** added a `ui_cover_phase5.png` render to `plugin/preview/PreviewMain.cs` (mirrors the
  existing phase6/Manual-Chute precedent) and inspected it: rail lights "Reference Content", heading
  reads "Reference Content", the three cards show the new sections cleanly with no overlap/clipping, and
  the globe/top-bar/bottom-bar/camera-target chrome is untouched. One title (`—` em dash at 34px) rendered
  with the following space collapsed in the preview's GDI+ font — reworded to "PARACHUTES (MARK 3)"
  rather than chase the renderer glitch (out of this task's scope); body-size en dashes (`–`) elsewhere
  on the same card render with normal spacing. Regression-checked `cover.png` (default phase 1, Coast) —
  pixel-identical baked body, confirming the skip only fires on `sp==5`. `python plugin/build.py test`:
  green, 3864 checks, 0 failed (no new warnings). §1.4 respected: layout reuses real Figma card
  positions; every fact is sourced (§8 / §4's CONFIRMED-real list), nothing invented.

### T4 [O] Cover map-modes (2D/3D + camera) — **DONE**
- **Read:** §3 + `NavPage`.  **DONE when:** preview, modes switch.
- **DONE 2026-09-02:** §3's REFINE row ("Cover globe → 2D/3D map + camera modes … a Cover mode, not a
  page") built as the reference UI's OWN three-view camera, not an invented one. `First.vue`'s
  `swapComponent()` (`assets/reference/dragon2-ui-master/src/views/First.vue`, the same source
  `UI_AUDIT.md` is generated from — tier-1) cycles exactly three components in the right-hand slot with
  one button: `view-00` **View01** → `viewHeading` **"Auto - Earth IO"** (the 3D Earth) · `view-01`
  **NavEarth** → **"Auto - Map IO"** (the flat, pannable map) · `view-02` **Capsule** → **"Auto - Capsule
  IO"**, `count = (count + 1) % 3`. All three share one region (`#scroll-earth-wrapper` and
  `#capsule-wrapper` are both `top:10% left:40.5% width:60% height:90%`) — the slot our live globe
  already filled. New in `pure/CoverPage.cs`: `CoverCam` + `CamHeading` / `NextCam` / `CamMapMode`, the
  `Build(..., CoverCam)` overload, `DrawCameraView` (the three views) and `DrawCameraChrome` (caption,
  pill, cluster). **EARTH** = the existing `NavPage.Planet` call, arithmetic unchanged (`cover.png`
  regression-checked). **MAP** = `NavPage.Map`, made public for CoverPage's use exactly as `Planet`
  already was, so there is one flat-map renderer and not two that can drift; drawn into `MapRect`, the
  widest **2:1** band that fits the slot so `MapProjection`'s zoom 0 FILLS it rather than letterboxing
  (the reference's `#scroll-earth` fills its wrapper). **CAPSULE** = the shipped `art/dragon.png` still,
  centred — the turntable that replaces it is **T11 (§5)**, flagged in the code. The baked
  `camera_auto_earth_io` asset is skipped and redrawn as live text at that asset's own **measured**
  metrics ("CAMERA" cap rows 5–19, the heading rows 35–56, both centred on its x+173), so the caption
  names whichever view is up. `TARGET LATITUDE`/`LONGITUDE` are now hidden off the Earth view — the
  source's own `v-if="currentComponent === 'view-00'"`, not a layout choice.
  **Controls.** `NEXT VIEW` is the reference's own `#swap-view` button (also in `UI_AUDIT.md`'s First.vue
  label list); its reference position (`top:90% right:5%`) is Frame 67's SETTINGS button, so the pill
  moves to the free left end of that same row and is built as SETTINGS' twin — `rectangle_174`'s exact
  401×111, same dash-then-label interior. The MAP view gets NavEarth's pan/centre/zoom cluster at the
  map's top-right: centre + four arrows one 5em-equivalent pitch out, zoom pair a row below at half a
  pitch either side, **+ on the LEFT** (`zoomInTrue right:9em` vs `zoomOutTrue right:4.5em` — the
  reference's ordering, kept), translucent `rgba(2,7,56,0.75)` faces per NavEarth's own CSS, CTR lit
  while the map is following. `PadRect` is measured off `MapRect`, so the cluster cannot slide off the
  map at another panel aspect, and Build + HitTest + the tests all read that one calculation.
  **Glue:** `MapProjection.WithMode` (new); `ScreenPainter` owns `coverCam` (opens on Earth, per Frame
  67), routes NEXT VIEW + the cluster through `ApplyCoverCam` into the SAME `MapProjection.Pan/Zoom/
  Centre` calls the NAV cluster uses — one map state, two front ends — and keeps `mapView.Mode` following
  the camera so pan/zoom cannot mean the wrong thing. `FigmaUI.Build` threads the camera; both older
  overloads stay and default to Earth.
  **Gate:** `python plugin/build.py test` **green, 3917 checks, 0 failed** (Figma UI nav suite 114 → 167:
  the 3-view cycle wraps, the three headings are the source's strings verbatim, NEXT VIEW hits on every
  view and navigates nowhere, each cluster button hits its own action on MAP and is **inert** on Earth
  and Capsule, every cluster button lands inside `MapRect`, MapRect is 2:1, and SETTINGS + the phase rail
  still hit with the new controls in). **Preview:** `ui_cover_cam_map.png`, `ui_cover_cam_capsule.png`
  and `ui_cover_cam_map_zoom.png` added and inspected (anything reachable by a control needs a render);
  `cover.png` / `ui_cover_phase5.png` / `ui_cover_phase6.png` re-inspected — unchanged but for the new
  caption and pill. No display-list overflow; `CoverPage.Commands` 240 → **340** and `FigmaUI.Commands`
  260 → **360** (the MAP view measures 258, the old Earth peak 231). §1.4 respected throughout: every
  string, control and ordering is the reference's; the two departures (open on Earth, static capsule) are
  stated in the code. **Logged not done → S10:** the scaled-space RT camera `MAP_MFD_RESEARCH.md` §2
  designs is a separate, glass-only piece.

### T5 [S] Vehicle Alerts + Consumables — **TODO**
- **Read:** §3 + `VehicleOverview`/`SubsystemPage`.  **DONE when:** preview.

### T6 [S] Rendezvous ellipse plot — **TODO**
- **Read:** §3 + §8 + Hohmann/Orbital.  **DONE when:** preview + nav test.

### T7 [S] Deorbit Burn Prep (reconstruct, marked) — **TODO**
- **Read:** §3 + §8.  **DONE when:** preview.

### T8 [S] Entry page (reconstruct, marked) — **TODO**
- **Read:** §3 + §8.  **DONE when:** preview.

### T9 [O] Prop thruster schematic + P&ID / systems-tree deep-views — **TODO**
- **Read:** §11b.  **DONE when:** preview.

### T10 [O] Lower-panel accuracy pass — **TODO**
- **Read:** §4 + §14.4(a,b) + `PanelButtons`/`PanelMap`/`FlightCommands`.  **Build:** lighting bright / no-red,
  audible click, SWAP + inferred-entry INERT.  **DONE when:** preview + panel test, click plays, inferred inert.

### T11 [O] Capsule turntable — **TODO**
- **Read:** §5.  **Build:** source the MaTte0 model → render sprites → drag-rotate.  **DONE when:** preview,
  drag rotates.

### T12 [S] Ascent/Launch page — **TODO**
- **Read:** §8 + §3.  **Build:** F9 schematic + event list.  **DONE when:** preview.

### T13 [O] Live-data wiring — **TODO**
- **Read:** §6 + `VesselData.cs`.  **Build:** replace placeholder constants.  **DONE when:** values live in-sim.

### T14 [O] Touch wiring — **TODO**
- **Read:** §6 + §4.  **Build:** display-only controls → real per the decisions.  **DONE when:** controls act (+ tests).

---

## Part B — autopilot (§B12.6 order; all [O])

### T15 [O] Embed MechJeb — **TODO**
- **Read:** §B2 / §B3 / §B12.1.  **Build:** pinned + privately-namespaced; headless core loads the Crew-Dragon
  cfg (`docs/reference/mechjeb_settings_type_Crew-Dragon.cfg`). Also create/point `docs/FLIGHT_SYSTEMS.md` (S3).
  **DONE when:** one core loads, no clash, cfg applied.

### T16 [O] Pure conductor core + tests — **TODO**
- **Read:** §B9 / §B12.2-3 + `MissionPhase.cs`.  **Build:** phase FSM as pure decisions.  **DONE when:**
  headless tests green.

### T17 [O] Glue driver, read-only — **TODO**
- **Read:** §B12.2 + `_AutopilotStub.cs`.  **Build:** report phase/engaged, no commands.  **DONE when:** in-sim
  phase matches, nothing flies.

### T18 [O] Wire Ascent (PVG) — **TODO**
- **Read:** §B8 / §B11.  **DONE when:** PVG flies to insertion in-sim.

### T19 [O] On-orbit ops + re-plan loop — **TODO**
- **Read:** §B10.2 / §B12.4 / §B9.  **DONE when:** rendezvous to the KOS in-sim.  **May SPLIT if large.**

### T20 [O] Docking hand-off + speedLimit ladder — **TODO**
- **Read:** §B10.3 / §B14.  **DONE when:** dock in-sim.

### T21 [O] Deorbit/entry/chutes + abort wiring — **TODO**
- **Read:** §B13 / §B10.4 / §B9.  **DONE when:** return + splash in-sim, EJECT abort works.

### T22 [O] Empirical tune (one param at a time vs §B11) — **TODO**
- **Read:** §B5 / §B7-11.  **DONE when:** profile matches nominal, the 4 `[EST]` numbers pinned into the cfg.
  **May SPLIT** per phase.

---

## Stray findings (appended per C1.1 — logged, not done)

### S1 [S] `CLAUDE.md` header predates Part B — **DONE 2026-09-02** (folded into T1)
The original "What this repo is now" section (top 40 lines) says the autopilot was deleted and "if you find a
reference to any of it, it is stale; remove it." Part B RE-INTRODUCES it as the embedded-MechJeb conductor
(T15–T22), so that line is itself now stale and auto-loads every session. T0 appended the C1 rules below it but
(append-only scope) did not rewrite the header. **DONE when:** `CLAUDE.md` no longer contradicts Part B.
**Closed by T1:** the header now states Part A + the planned Part B, splits *stale deleted implementations*
from *the idle stubs that must NOT be removed*, and carries the BUILD-HOLD line.

### S2 — repo has a large uncommitted working tree (informational)
The tree holds the Figma-UI rebuild (`CoverPage.cs`, `FigmaUI.cs`, `VehicleOverviewPage.cs`,
`docs/SCREEN_INVENTORY.md`, `plugin/GameData/DragonScreen/art/cover/`, ~26 files) — pre-existing, not T0's and
not touched by it. Owner to commit via GitHub Desktop; noted so a later task doesn't mistake it for its own diff.

### S3 [S] `docs/FLIGHT_SYSTEMS.md` is referenced but does not exist — **TODO** (T1 part DONE; rest → T15)
Live references point at a missing file: `plugin/src/pure/MissionPhase.cs`, `plugin/build/audit_comments.py`,
and `docs/INDEX.md` (lists it as existing). The §8 flight facts it should hold currently live only in
`BUILD_PLAN.md`. T15 creates it; T1 must at minimum stop `INDEX.md` advertising a missing file.
**T1 part DONE 2026-09-02:** `INDEX.md` no longer lists it as existing — it now says explicitly that the file
does not exist, that the §8 flight facts live in `BUILD_PLAN.md` until T15 creates it, and that the two code
comments (`pure/MissionPhase.cs:54`, `build/audit_comments.py:233`) are not a live link. **Still open:** those
two comments, and creating the file — both T15.

### S4 [S] Phase classifier reads PHASING while still sub-orbital — **TODO**
From the 2026-08-29 screen audit (U1), and the code still ships. `VesselData.cs:77` `Mission.Classify(mi)` keys
on situation + target presence with **no orbit-closed check**, so "in space + has an ISS target" ⇒ Phasing —
even mid-ascent with periapsis at −4,600 km. The screens showed `ACTIVE PHASE PHASING` from ~T+5:02 while the
mode label simultaneously read "Ascent to orbit". Should stay ASCENT/INSERTION until the orbit is actually
closed (pe above the atmosphere / SECO). Pure code, headless-testable. **DONE when:** a sub-orbital vessel with
a target classifies as ascent, with a test.

### S5 [S] Nuisance PROPELLANT CAUTION off the spent ascent stage — **TODO**
From the same audit (U2). The propellant gauge correctly shows what the LIT engines are drinking, so the
near-spent S2 reads ~16% near SECO → `Alarms.Low(Propellant01)` (`Pages.cs:1082`) → PROPELLANT CAUTION → whole
vehicle STATE CAUTION, during an entirely nominal ascent. Dragon's own return propellant is full at that point.
Fix direction: suppress the low-prop alarm while the lit stage is an ascent stage, or alarm on the return
budget. **DONE when:** a nominal late-ascent state does not raise CAUTION, with a test.

### S6 [S] Both NET PWR dials read exactly 0 W — **TODO / NEEDS-VERIFY**
From the same audit (U3). VEHICLE OVERVIEW showed `NET PWR 1` and `NET PWR 2` both at exactly `0 W`, while the
comment at `Pages.cs:974` expects them negative on battery (e.g. −59 W). Exactly zero on both buses reads as
unpopulated. Verify against `pure/CabinEnvironment.cs` — is the model producing a value in that state?
**DONE when:** the dials show a modelled value, or it is confirmed correct and the comment is fixed.

### S7 [S] `index_assets.py` does not recurse into `art/cover/` — **TODO**
`plugin/build/index_assets.py` globs the shipped-art directory with a non-recursive `'*'`, so `ASSET_INDEX.md`
lists 6 shipped files while 98 exist — the 95 Cover PNGs in `GameData/DragonScreen/art/cover/` are invisible to
the "grep the index before concluding an asset does not exist" rule, which is exactly the failure that file was
written to prevent. **DONE when:** the generator recurses and the regenerated index lists the cover set.

### S8 [S] `plugin/build/assess_flight.py` is autopilot-era tooling — **TODO**
It reads the flight corpus (`<KSP>/DragonScreen_capture/Crew-2_*.csv`) of the autopilot deleted 2026-09-01, and
the corpus is gone. Decide: delete it, or keep it for Part B's §B5 empirical tune (which will produce flight
data again — §B22/T22). Owner call, logged not done. **DONE when:** deleted, or kept with a header saying it is
for the Part B tune.

### S9 [S] Mirror the reconcile into the Dragon Screen Map artifact — **BLOCKED** (T1's artifact half)
Logged here rather than as a numbered task so it cannot stall `/next` — it needs an owner action, not a
build session.
- **Why split out:** T1's own DONE-when is repo-side only and is met; the artifact is the outward *view*. The
  updated page was fully built and the publish was **refused by this session's permission classifier**, not by
  anything about the content. Not retried — that would be working around the denial.
- **The page:** `https://claude.ai/code/artifact/b46787c4-4199-4775-a966-9fb39490b77f` (it still exists —
  artifacts outlive their chat; reach it via `/artifacts` or the gallery). **Owner decision 2026-09-02: the
  durable source now lives in the repo at `docs/reference/dragon_screen_map.html`** — publish from there, and
  edit it whenever `SCREEN_INVENTORY.md` changes. (C7.1: the repo is truth, the artifact is a view. Publish
  with `url` = the link above so it updates in place rather than creating a second page.)
- **What changes:** tally 18 built / 3 design-set / 6 reference / 3 refinements; Menu + Reference Content move
  out of "genuinely dark" into DESIGN SET; Ascent becomes data-buildable; the rendezvous ellipse, circular nav
  plot, systems tree and P&ID join Reference; Prop→thruster-schematic joins the refinements; a "settled
  2026-09-02" strip carries §14.4(a–d).  **DONE when:** the published page matches `SCREEN_INVENTORY.md`.

### S10 [O] Scaled-space RT planet camera (`MAP_MFD_RESEARCH.md` §2) — **TODO**
Logged by T4 (C1.1), not done. `docs/MAP_MFD_RESEARCH.md` §2 designs a dedicated Unity camera copying
`ScaledCamera.Instance.cam` into a RenderTexture (`src/ScaledPlanetRenderer.cs` + `pure/PlanetGeom.cs` +
`ImageId.ScaledPlanetLive`), which would replace `NavPage.Globe`'s textured-strip disc with a real
rendered globe and cull the orbit line behind true geometry. It is **not** what T4's DONE-when asks for
and cannot be judged by the preview gate at all — there is no Unity camera with the game closed, so the
preview can only draw "LIVE 3D — NO SIGNAL". It therefore needs `install` + glass time, which BUILD-HOLD
forbids. T4 shipped the Cover's 2D/3D + camera MODES against the pure globe that already exists; the
disc underneath is the only part §2 would change. `MAP_MFD_RESEARCH.md` §5 still says this work "is T4" —
that line needs re-pointing at S10 when this is picked up (or by a docs pass). **DONE when:** the RT
camera renders in-sim, the orbit line tracks and occludes, and the framing reads well on the glass.

### S11 [S] `plugin/build/csc.rsp` is a generated file, tracked, and churns on every build — **TODO**
Logged by T4 (C1.1), not done. `build.py` overwrites `plugin/build/csc.rsp` on every invocation with
whichever compile ran last, so the file shows as modified after any `test` / `preview` / `install` and
its content depends only on which command was run most recently — it carries no information worth
versioning. It is also written with CRLF into a repo whose `.gitattributes` mandates LF, so git warns on
every touch. T4 restored it to HEAD (`git checkout --`) so the commit carries only real changes.
**DONE when:** it is gitignored (and untracked), or build.py writes it outside the repo.
