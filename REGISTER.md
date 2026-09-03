# DragonScreen — Task Register

The living task list for the build. **One task at a time** (C1.1): the first non-DONE line below IS the task.
`/next` reads this file. Full detail for every task: `docs/BUILD_PLAN.md` (Part C = the protocol, §1–§14 =
Part A research, §B1–B15 = Part B research).

**Status:** `TODO` · `DOING` (at most one) · `DONE` · `NEEDS-WORK` (+ one-line note) · `HELD` (blocked on an
owner action or an owner gate — **`/next` SKIPS it** and takes the first `TODO` below; only the owner
unblocks it) · `SPLIT` (a line closed in favour of the sub-tasks it names — also skipped).
**Model:** `[O]` = Opus · `[S]` = Sonnet (C3). Escalate [S]→[O] if a task stalls; never downgrade an [O].

⚠️ **LIVING** (C5): split any task that won't finish before compaction; append stray findings at the bottom;
never reorder past a DONE without a note.
🟢 **PREVIEW-ONLY BUILD-GO — the OWNER's decision, 2026-09-02, granted via the overseer.** Part A **pure
code + `python plugin/build.py test` + `python plugin/build.py preview` are cleared**. `python
plugin/build.py install` and glass time are NOT: they need a SEPARATE, explicit owner go, so a task whose
done-criteria can only be met in the capsule stops and asks rather than installing. ⚠ **A one-session
gate-open is not a standing one:** the owner opened `install` + glass for ONE session on 2026-09-02,
scoped to T10 + T11b. **S17 used it and is DONE.** That gate **closed with that session** — the standing
state is preview-only again. What still wants glass is collected on **S18** (an end-of-Part-A pass, by
the owner's call) and on **S10b** (S10 SPLIT 2026-09-02: **S10a** is built and preview-verified; **S10b** —
the Unity camera itself — cannot even be BUILT without the capsule to exercise it); each needs its own fresh
owner go. **T2–T4 are covered retroactively by this go** — they are on-plan and
preview-only, so nothing is reverted. Part B (T15–T22) remains DESIGNED, not started.
⛔ **Only the OWNER opens or widens this gate.** A build chat never grants one, never lifts one, and never
records a decision as the owner's unless the owner stated it in that chat (C1.12).

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

### G0 [O] Governance fix — real build-go, local-commit flow, C1.12 — **DONE**
- **Owner directive (`OVERRIDE`), 2026-09-02, via the overseer.** A harness/docs task, exempt from the gate.
  **Build:** (1) rewrite this file's build-go banner as the OWNER's decision — delete the "recorded by T4" /
  "T2–T4 outran the hold" wording, cover T2–T4 retroactively; (2) change C1.5 (commit flow) in `CLAUDE.md`,
  `docs/BUILD_PLAN.md` and the `/next` skill (step 6 + refusals); (3) add rule C1.12 to `CLAUDE.md` +
  `docs/BUILD_PLAN.md`.
- **DONE 2026-09-02.** The owner did NOT authorize the go that T4 recorded here; they had said HOLD pending
  the overseer. The work T2/T3/T4 shipped is on-plan and preview-only, so **nothing was reverted** — only the
  record was fixed. (1) **Banner** now reads as the owner's own decision, dated, granted via the overseer, with
  T2–T4 retroactively covered and a "only the OWNER opens or widens this gate" line; `CLAUDE.md`'s copy matches.
  (2) **C1.5 replaced everywhere:** a build chat now AUTO-COMMITS each finished task LOCALLY (`git commit`, clear
  message) and **NEVER `git push`** — no cached credentials; the owner pushes from GitHub Desktop. Task ends:
  register → `git commit` → STOP. Updated in `CLAUDE.md` (C1.5 + the loop line), `docs/BUILD_PLAN.md` (C1.5, C2
  step 6, §0, §10, §B12 build order) and `.claude/skills/next/SKILL.md` (step 6 + the refusal list).
  (3) **C1.12 added** to `CLAUDE.md` + `docs/BUILD_PLAN.md`, and mirrored into the skill's step 4 + refusals: a
  build chat never grants, widens or self-authorizes a build-go / `install` / glass-time go / `OVERRIDE` / plan
  change, and never records a decision as the owner's unless the owner stated it IN THAT CHAT — if a gate
  blocks, STOP and ask. **Consistency edits in the same files** (C7.1 — the plan must not contradict itself):
  `BUILD_PLAN.md` §0's blanket BUILD-HOLD banner now states the live gate (old wording kept, labelled
  pre-2026-09-02), §1's "NOT READY TO BUILD YET" paragraph carries a dated superseded line, Part C's header and
  the Part B bullet re-point at the real gate, and S10's blocker below is renamed from "BUILD-HOLD forbids" to
  "needs a separate owner go" (its substance is unchanged). Historical DONE notes were NOT rewritten.
  Harness/docs-only, no code change → **the preview/PNG gate does not apply** (C1.3); `python plugin/build.py
  test` run as a no-regression check: **green, 3917 checks, 0 failed**. Committed locally per the new C1.5.

### G1 [O] Governance — C1.13 "pose every owner decision as a paste-ready overseer prompt" — **DONE**
- **Owner directive, 2026-09-02, decided by the owner (Chris) via the overseer** (recorded as the owner's per
  C1.12). A harness/docs task, exempt from the preview/PNG gate. **Build:** add invariant rule **C1.13** — when
  a task needs an owner call (the C1.9 batched question at the end, or a mid-task stop-and-ask when a gate /
  source / authority blocks the work), phrase it as a SELF-CONTAINED prompt addressed to the overseer
  (situation + what was done, the exact decision, the discrete options, and which options need an owner
  gate-open or `OVERRIDE`) instead of a bare inline question — then apply it consistently across the harness.
- **DONE 2026-09-02.** Rule 13 added verbatim-in-substance to the C1 list in **`CLAUDE.md`** and to **C1** in
  **`docs/BUILD_PLAN.md`** (identical text); no other rule renumbered or altered. Consistency edits (C7.1):
  `BUILD_PLAN.md` **C1.9** now reads "pose ONE structured question, in the C1.13 overseer-prompt form", and
  **C6**'s "Questions first" bullet spells out the same form so the owner can paste it straight through;
  **`.claude/skills/next/SKILL.md`** step 7 now names C1.13 and lists the four things the prompt must carry,
  step 4's gate-blocked "STOP and ask" points at the same form, and step 1's rule count reads 1–13. C1.13
  governs the FORM of asking only — C1.12 (never decide a gated item) and C1.9 (batch at the end) both stand.
  No code change → **the preview/PNG gate does not apply** (C1.3); `python plugin/build.py test` run as a
  no-regression check: **green, 11 suites / 5292 checks, 0 failed**. Committed locally (C1.5); NOT pushed.

### G2 [O] Governance — §14.4(e) simulation-for-immersion policy — **DONE**
- **Owner directive, 2026-09-02, decided by the owner (Chris) via the overseer** (recorded as the owner's per
  C1.12). An authorized plan change — it intentionally changes the settled "dash when no source" idiom, so it
  is an owner decision, not a build-chat one (C1.8). A docs-only task, exempt from the preview/PNG gate.
  **Build:** add **§14.4(e)** to the Owner-invention-decisions running log, substance verbatim; cross-reference
  it at **§1.4** and **§6** so the plan does not contradict itself (C7.1).
- **DONE 2026-09-02.** **§14.4(e) added** to `docs/BUILD_PLAN.md`'s §14.4 log, after (d), substance verbatim:
  where a physically-real vehicle quantity is not yet modelled, do NOT default to an honest dash when that dash
  costs immersion — (1) read it from an existing installed mod if one provides it (tier-2, MARKED — cabin
  O2/CO2/water already come from TAC-LS via `LifeSupportBridge`); (2) failing a mod, SIMULATE it, but only as a
  COHERENT model driven off real vessel/cabin state (never a static constant), MARKED as simulated in code
  (tier-3 invention, jointly decided per §1.4); (3) keep an honest dash ONLY where the quantity genuinely does
  not exist in that state (no target → no docking error; return leg → no splashdown-relative; a value only
  Part B's flight software will command, e.g. the deorbit SLEW rows → dash until Part B). GUARDRAIL: a simulated
  value must never fabricate a safety VERDICT the sim cannot justify — a verdict (e.g. a suit-leak "Nominal")
  follows the simulation honestly, never hardcoded. It EXTENDS §1.4 (real → other-users'/mod → simulate-marked
  → dash-for-absent) and does NOT license unmarked invention. First application: **S31** (suit leak check).
  **Cross-references added (C7.1):** §1.4's owner-decision 4 now carries an "EXTENDED 2026-09-02 by §14.4(e)"
  line, and §6 (cross-cutting / live-data) gained a "Simulation-for-immersion (§14.4(e))" bullet beside the
  live-data one, so neither reads as "dash when no source" any more. **Optional pointer taken:** the one-line
  §14.4(e) clause is appended to **C1.4** in `CLAUDE.md` (auto-loaded every session, which is where the old
  idiom would otherwise be applied) and mirrored identically into **C1.4 in `docs/BUILD_PLAN.md`** so the two
  copies of the rule stay word-identical (C7.1). No other rule renumbered or altered; `LifeSupportBridge` +
  the TAC-LS path confirmed present in the tree before citing them. This was the ONLY plan edit of the session.
  No code change → **the preview/PNG gate does not apply** (C1.3); `python plugin/build.py test` run as a
  no-regression check: **green, 11 suites / 7998 checks, 0 failed**. Committed locally (C1.5); NOT pushed.
- ⚠ **Naming collision, logged not acted on (C1.1):** the GLASS-CHECKLIST table further down this file also
  numbers its rows G1–G9. Those are glass-time gaps, unrelated to the G0/G1/G2 governance tasks up here. A
  future chat asking for "G2" should say which. Left as-is — renaming either scheme is out of this task's scope.

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

### T5 [S] Vehicle Alerts + Consumables — **DONE**
- **Read:** §3 + `VehicleOverview`/`SubsystemPage`.  **DONE when:** preview.
- **DONE 2026-09-02.** §3's two REFINE rows built from DillonBaird's Vehicle render + alt-text
  (`SCREEN_INVENTORY.md` "IMAGERY HUNT 2026-09-01", tier-2 marked per §1.4).
  **CONSUMABLES** (`VehicleOverviewPage.cs`): the RIGHT column was orbit telemetry duplicating the
  FLIGHT page's own strip — replaced with the real CONSUMABLE/QTY/MARGIN table (Power Unit 1/2 Energy,
  Usable Deorbit Fuel/Oxidizer, Orbit 1/2 Subtank Fuel/Oxidizer, + SHOW MARGINS TO). MARGIN itself isn't
  in the captured alt-text, so it draws as "—" (the existing no-source-yet idiom, `STATE_CONTRACT.md`)
  rather than inventing a number.
  **FUNCTIONS/ALERTS + red sub-nav** (`VehicleSubsystemPage.cs` + `VehicleTabBar.cs`): a real confirmed
  fact (`REAL_DRAGON_SCREENS.md` §2, tier-1) is that the real subview nav bar "turns red when that
  subview holds an alert" — built as `VehicleTabBar.Severities(PageState)`, computed from the SAME live
  signals already used elsewhere in the codebase (`Alarms.LifeSupport`/`Thermal` on the cabin,
  `Alarms.Low` on propellant/power, `Alarms.FdirSeverity` on the fault spine — Avionics+GNC share the
  one real fault channel this build has), never a new fake number. `VehicleOverviewPage` and
  `VehicleSubsystemPage` both now thread `PageState` through so a faulted subsystem's tab reads red from
  every vehicle page, not just its own (the real "reached in one touch from anywhere" behaviour) —
  `VehicleMechPage` is untouched (out of this task's declared scope) and keeps the old no-severity
  overload, logged as **S12**. The ALERTS tab itself ("ALERT ACTIVITY" — a real label, Frame 58's own
  attitude HUD, `REFERENCE_PAGES.md`) shows the same live severity as a big status word plus the FDIR
  row, so the tab colour and the page content can never disagree. Toggle pixel geometry is OURS (not
  measurable from the source render) — same footing as Menu/Reference Content's §14.4(c) layout — and is
  left **inert**, matching this task's DONE-when (preview only); wiring the tap is **T14**'s job
  (display-only → real controls).
  **Preview:** `ui_vehicle.png` (CONSUMABLES column) and all six `ui_vehicle<sub>.png` renders inspected;
  added `ui_vehiclepower_alerts.png` / `ui_vehiclecrew_alerts.png` (the ALERTS tab content), and
  `ui_vehiclepower_alarm.png` / `ui_vehicle_alarm.png` (Power01 pushed to the ALARM band to prove the
  sub-nav genuinely turns red, not just amber, and that it shows from the Overview page too) — all
  inspected, all clean, no overlap/clipping, no `DisplayList` overflow warnings. `python plugin/build.py
  test`: green, 3917 checks, 0 failed (unchanged — T5's DONE-when is preview only, no nav-test required).
  §1.4 respected throughout.

### T6 [S] Rendezvous ellipse plot — **DONE**
- **Read:** §3 + §8 + Hohmann/Orbital.  **DONE when:** preview + nav test.
- **DONE 2026-09-02.** §3 row 83 / SCREEN_INVENTORY #23+#87 built as a new page, `UiPage.Rendezvous`
  (`plugin/src/pure/RendezvousPage.cs`). Source is a REAL flight screengrab (tier-1, not a
  recreation): the BBC explainer's `_112570366_touchscreens.png`, all three cockpit screens during an
  actual "Hold Capture" rendezvous — LEFT confirmed our existing HUD/DockingSimPage design, RIGHT was
  a checklist page (out of scope), CENTRE (this page) showed a left icon sub-nav rail, a "Hold
  Capture" procedure card (◄/► + RUNNING + status text + a circular mission-patch icon), and a large
  2D orbital-ellipse plot with the vehicle position + an approach chord.
  **The ellipse plot is not a second orbit renderer:** `NavPage.Orbit` (the plain NAV page's existing
  real conic — apogee/perigee → ellipse, current radius → true anomaly) was made public with one
  addition — an optional approach chord, drawn vehicle→periapsis, shown only when `s.HasTarget` — so
  there is one orbit calculation, not two that could drift (same rule T4 followed for NavPage.Map).
  The plain NAV page's own call is the untouched 6-arg overload (chord always off); its rendering is
  unchanged. **The Hold Capture card's RUNNING/NOT ENGAGED state is real**, not decoration: it reads
  `PageState.RendezvousEngaged`/`.RendezvousNote`, which the glue already threads from
  `StationApproach.Engaged`/`.Note` (`_AutopilotStub.cs`) — "NOT ENGAGED" until Part B wires it, the
  same honest-stub idiom CLAUDE.md requires everywhere else. **What's ours, stated in the code:** the
  left rail's icons are not label-legible in the source photo (SCREEN_INVENTORY residual research),
  so it draws as inert chrome with no invented destinations; the circular "mission-patch" icon is a
  plain roundel (shape confirmed, no artwork invented); the ◄/► step controls are display-only
  (T14 wires touch, same footing as T5's ALERTS toggle); the approach chord runs to periapsis rather
  than a true target-relative point, since target orbital elements aren't in PageState yet (T13).
  **Reachability:** a letterbox-margin affordance on the Manual Docking page (mirroring the HUD's own
  Docking affordance) opens Rendezvous — the two are the HUD/plot pairing the same photo shows
  together — plus the universal Menu grid and bottom bar (Hud/Docking/Rendezvous share bar icon 1).
  **Required side-effect, not scope creep:** `MenuPage`'s grid auto-discovers every `UiPage`
  (`FigmaUI.PageCount`), so adding a 29th page overflowed its fixed 27-cell (3×9) grid; bumped to
  3×10 (30 cells) — mechanical upkeep of an existing DONE page's own design, not new scope.
  **Nav test:** `FigmaUINavTest.Rendezvous()` — Docking margin → Rendezvous, bottom bar → Cover,
  Menu lists the new page (also proven end-to-end by `Menu()`'s existing all-entries loop), and the
  rail/card are confirmed inert. `python plugin/build.py test`: green, Figma UI nav suite 167 → 173,
  3917 → **3923 checks total, 0 failed**.
  **Preview:** `ui_rendezvous.png` added and inspected — title, 4-slot rail, Hold Capture card (patch
  roundel, "NOT ENGAGED" in dim text, ◄/► boxes), and the orbital plot (live globe, dotted ellipse,
  AP/PE markers, vehicle marker, orange approach chord to PE) all render cleanly, no overlap/clipping,
  no `DisplayList` overflow (205 of 320 commands). `ui_menu.png` re-inspected — 28 cards including the
  new "RENDEZVOUS" entry, still legible, no overlap. §1.4 respected throughout: every real fact is
  sourced (the photo, or existing live PageState fields); every invented detail is named as such in
  the code and above.

### T7 [S] Deorbit Burn Prep (reconstruct, marked) — **DONE**
- **Read:** §3 + §8.  **DONE when:** preview.
- **DONE 2026-09-02.** SCREEN_INVENTORY.md #24 / §3 row "Deorbit Burn Prep", sourced only from a
  **blurry** `discovery` deorbit-burn photo frame (tier-1, real capsule, no legible layout) —
  "reconstruct + MARK" per §7 item 3, same footing as VrioTestPage (also photo-only, no Figma/demo
  ref) and the Suit-Leak fail branch (§14.4d). Built as a new standalone page, `UiPage.DeorbitBurnPrep`
  (`plugin/src/pure/DeorbitBurnPrepPage.cs`) — NOT a Cover phase-rail slot: `FigmaUI.cs`'s own
  pre-existing comment on `VrioTest` states a "Procedure" rail item's real nav entry point is wired in
  **the touch pass (T14)**, so re-pointing the Cover rail's generic "Procedure" slots (real label, real
  photos — SCREEN_INVENTORY's residual research — not invented) at specific content is deliberately
  left to T14/live-data wiring, not this task; reached for now via the Menu grid (auto-discovered,
  `FigmaUI.PageCount` 29→30) same as VrioTest.
  **Real content, two independent sources agreeing on field NAMES (never layout, §1.4):**
  SCREEN_INVENTORY's photo transcription ("Crew Interrupt Conditions" + "Slew for Deorbit Burn"
  [Roll/Pitch/Yaw, Maximum altitude rate, FC Slew] + the numbered settle-burn steps) and
  SCREENS_LOOK_AND_FUNCTION_RESEARCH's read of the community recreation's own First.vue source (the
  three Crew Interrupt Conditions criteria verbatim). Roll/Pitch/Yaw/Maximum-altitude-rate show "—"
  (T5's no-source-yet idiom — this is a free-flight slew, not the docking-relative
  PageState.RollText/.. the HUD uses, and T13 is where a real number could appear); **FC SLEW is real,
  not decoration** — new `PageState.DeorbitEngaged` threads `DeorbitOps.Engaged`
  (`_AutopilotStub.cs`, already the live STRING1C source per `PanelButtons.cs`) through
  `VesselData.cs`, the same honest "NOT ENGAGED" idiom RendezvousPage's Hold Capture card uses (T6).
  Card-style chrome (accent dot + title + lines) reuses CoverPage.DrawReferenceContent's (T3) visual
  convention for reconstructed real content; page layout itself is ours (no photo layout to measure).
  **Flagged for the owner, not silently changed (see S13):** both real sources, and the community
  Figma's own baked layer name (`600deg_m_altitude_rate` in `CoverPage.cs`), say "altitude" where
  "ATTITUDE" would make physical sense (an attitude-error/attitude-rate interrupt for a SLEW maneuver)
  — kept verbatim per C1.4 rather than unilaterally corrected.
  **Nav test:** new `FigmaUINavTest.DeorbitBurnPrep()` — bottom bar → Cover, Menu lists the new page
  (also proven end-to-end by `Menu()`'s existing all-entries loop, whose dynamic per-entry check count
  rose with it), and the content is confirmed inert (no invented destinations). `python
  plugin/build.py test`: green, Figma UI nav suite 173 → **177**, 3923 → **3927 checks total, 0
  failed**. **Preview:** `ui_deorbitburnprep.png` inspected — title, three cards (Crew Interrupt
  Conditions / Slew for Deorbit Burn / Deorbit Burn Settle), no overlap/clipping, no `DisplayList`
  overflow (27 of 80 commands); `ui_menu.png` re-inspected — 29 cards including "DEORBIT BURN PREP",
  still legible, no overlap. §1.4 respected throughout: every real fact is sourced; the one ambiguity
  found is flagged, not silently resolved; nothing in `PanelMap.cs` / label docs touched.

### T8 [S] Entry page (reconstruct, marked) — **DONE**
- **Read:** §3 + §8.  **DONE when:** preview.
- **DONE 2026-09-02.** SCREEN_INVENTORY.md #25 / §3 row "Entry" built as a new standalone page —
  new `plugin/src/pure/EntryPage.cs`. Source is a **PARTIAL** `discovery` "Entry" photo frame
  (tier-1, real capsule, thinner than T7's already-blurry Deorbit Burn Prep source): the ONLY
  legible fact is a "Parachute Deployment Altitude" section title + "steps" — no step TEXT was
  transcribable. Rather than invent step text, the one card reuses the SAME real, already-shipped
  drogue/main altitude + action figures from `ManualChuteDeployPage.cs`'s Standard schedule (one
  real source for one real physical event, not a second independently-invented number that could
  disagree with it — same discipline T6 applied to `NavPage.Orbit` and T7 applied to reusing
  `PageState` fields); "(TBC)" kept verbatim as SpaceX's own to-be-confirmed marker. Page chrome
  (title + one accent-dot card) is ours, in `DeorbitBurnPrepPage`'s own established card style.
  **Important correction caught before building:** `UiPage.Entry` (14) already existed but was
  **NOT** repurposed for this page — its `FigmaUI.Titles` entry is "ENTRY GO / NO-GO", one of
  S14's confirmed-dead leftover phase-rail ACTION ints (unrelated to this screen despite the name
  collision). A new value, `UiPage.EntryProcedure` (30), was appended instead (`FigmaUI.cs`:
  enum + `PageCount` 30→31 + `Titles` "ENTRY" + `Build`'s switch + `IsPlaceholder`), so the Menu
  card's label actually matches the page it opens. Reachability: Menu grid only for now (same
  footing as T7 — a real phase-rail entry point is T14's job); `PreviewMain.cs`'s UI render loop
  and `FigmaUINavTest.EntryProcedure()` (bottom-bar → Cover, Menu lists it, body inert) added to
  match T7's precedent exactly. `MenuPage`'s grid auto-discovers via `IsPlaceholder` — no resize
  needed (20 → 21 real cards, well under the existing 3×10 capacity).
  **Nav test + gate:** `python plugin/build.py test` green, Figma UI nav suite 228 → **234**
  checks, 3978 → **3984** total, 0 failed, no new warnings. **Preview:** `ui_entryprocedure.png`
  inspected — title "ENTRY", one clean card (6 lines), no overlap/clipping, standard bottom bar;
  `ui_menu.png` re-inspected — 21 cards including "ENTRY" as the new last card, still legible, no
  overlap. §1.4 respected: the one real fact (section title) is sourced and marked; every other
  number reuses an existing real source rather than inventing a new one; no `PanelMap.cs` /
  label-doc edits; `plugin/build/csc.rsp` churn reverted before commit (S11).

### T9 [O] Prop thruster schematic + P&ID / systems-tree deep-views — **DONE**
- **Read:** §11b.  **DONE when:** preview.
- **DONE 2026-09-02.** All three §11b screens built. Every one is **layout-real / labels-reconstructed
  + MARKED** — §11b's own verdict on this vein ("the LAYOUTS of the new screens are captured … exact
  on-screen text is NOT transcribable at these resolutions"), the same footing T7 and T8 established.
  **1 · Prop thruster schematic** (`plugin/src/pure/PropSchematic.cs`, new; §3's REFINE row /
  SCREEN_INVENTORY #26, source JSC `jsc2026e404727`). `VehicleSubsystemPage`'s FUNCTIONS view for
  `Sub.Propulsion` now delegates its centre+right zone to the schematic and skips the upright
  `dragon_crew` render (which would be a second, contradictory vehicle); its title, left checklist,
  FUNCTIONS/ALERTS toggle, ALERTS view and tab bar are the shared template, untouched — the five
  sibling tabs render pixel-identically (`ui_vehiclepower.png` re-inspected). Drawn per §11b: Dragon in
  **horizontal profile** (line-art at the real 4 m × 4.4 m capsule + 3.7 m trunk proportions, blunt nose
  cone, heat shield, trunk ribs, SuperDraco sidewall pods), **four Draco quad arc symbols** as callouts
  around it whose leaders converge on ONE axial pod station (the real arrangement), and a
  **per-thruster data band** along the bottom (16 rows in 4 quad columns).
  **The firing indicators are SIMULATED, never faked:** each thruster's duty is the LIVE RCS demand
  (`PageState.TransX/Y/Z`, `RotPitch/Yaw/Roll` — straight off `FlightCtrlState` in `VesselData.cs`, the
  same signal the DOCKING page's corner rings already draw) resolved onto that pod's azimuth, gated by
  the real RCS action group (`PageState.RcsOn`) — nothing moves unless the vehicle's controls moved.
  **Ours, stated in the code:** the quad names A–D, the per-thruster designators and their four roles
  (FWD/AFT/LAT/ROLL), and the four-pods-90°-apart-each-with-four-roles geometry — SpaceX's real thruster
  naming and control allocation are not public. **Nothing was removed:** the template's four
  headline-gauge values and five detail readouts move intact into the schematic's right column (still
  representative, still T13's to make live).
  **2 · Systems tree** (`SystemsTreePage.cs`, new `UiPage.SystemsTree` = 31; SCREEN_INVENTORY #27,
  source JSC `jsc2024e064449` LEFT screen). The hierarchical box-and-connector diagram §11b describes,
  drawn as the electrical distribution: SOLAR ARRAY + BATTERIES ×4 → MAIN POWER → **POWER 1 / POWER 2**
  → **STRING 1A–1C / 2A–2C** → the flight-computer-strings foot. **The box labels are not invented:**
  POWER 1/2 and STRING 1A–2C are the REAL console's own button legends (`pure/PanelMap.cs`, transcribed
  — untouched here per C1.4) and §4 confirms them as the main buses + the triple-redundant FC strings
  (18 units / 54 voting processors, the foot caption); SOLAR ARRAY / BATTERIES ×4 are
  `VehicleSubsystemPage`'s own Power checklist strings reused verbatim. **Live, not painted:** every box
  and connector is coloured from `PageState.Systems` (`pure/VehicleSystems.cs` — bus on/off,
  `Systems.Get`/`StateWord` per string ON/ISOL/TRIP, `Systems.OnlineCount`) plus `Power01` for the SoC
  bar. Only "DEPLOYED" on the array has no live source (as on the Power page already; T13).
  **3 · Systems P&ID** (`SystemsPidPage.cs`, new `UiPage.SystemsPid` = 32; SCREEN_INVENTORY's "Vehicle
  systems P&ID schematic", sources `crew1_3`/`crew3_1`/`demo1_3`, §7 item 5). The inventory's own
  description built literally — line-art rectangular loops, boxed components, inline valve symbols, and
  small green status dots on the lines. **Ours, and stated:** WHICH subsystem it plumbs. The inventory
  says only "likely Prop/Thermal/ECLSS"; we drew the ECLSS + coolant loops because those are the fluid
  systems this build actually MODELS, so every component has a live state rather than a painted one
  (`Systems.Oxygen/Nitrogen/CanisterUsed/Suppressant/Fire/Leaking/Isolating` + `Cabin` pressure/ppO2/
  CO2/temp/loops, banded by the SAME `Alarms`/`CabinLimits` thresholds the gauges use, so a component
  can never disagree with the gauge for the same quantity). Propulsion's plumbing would have duplicated
  the schematic above. All numbers come from `PageState`'s pre-formatted text — the draw path formats
  nothing; quantities with no pre-formatted text draw as bars instead of inventing one.
  **Reachability:** Menu grid for both new pages (auto-discovered; 21 → 23 cards, well inside the
  existing 3×10 grid), plus the global bottom bar, whose marker names Vehicle as their parent.
  Deliberately **not** ninth/tenth `VehicleTabBar` tabs — that strip's eight tabs are confirmed-real
  from the clean designer mockup, so adding one would be editing a real-sourced label set (C1.4). A real
  in-page entry point is **T14**'s job, exactly as for T7 and T8.
  **Gate:** `python plugin/build.py test` **green, Figma UI nav suite 234 → 256 checks, 3984 → 4006
  total, 0 failed**, no new warnings. New checks: `SystemsDeepViews()` (bottom bar → Cover, Menu lists
  each, body inert, no phantom tab strip, both are real pages not placeholders) and
  `PropSchematicDuty()`, which pins the firing model's real properties rather than a screenshot — RCS
  off fires nothing; a roll demand works every pod's tangential thruster and only that one; a +Z demand
  works one axial thruster per pod and leaves its opposite idle; a lateral demand lights some pods and
  not all four (a thruster pushes one way only). **Preview:** `ui_vehiclepropulsion.png` (idle),
  `ui_vehiclepropulsion_firing.png` (new — live RCS demand, so the quads and per-thruster bars are
  proven to light), `ui_vehiclepropulsion_alerts.png` (new — the ALERTS pairing, still the untouched
  template), `ui_systemstree.png` (buses unpowered, the fixture's honest starting state),
  `ui_systemstree_live.png` (new — buses on with one string TRIPPED and one ISOLATED, proving the live
  colouring), `ui_systemspid.png` and `ui_menu.png` all rendered and inspected: no overlap, no clipping,
  no `DisplayList` overflow (heaviest 186 of 360). Sibling vehicle pages re-inspected, unchanged.
  §1.4 respected throughout; no `PanelMap.cs` / label-doc edits; `plugin/build/csc.rsp` churn reverted
  before commit (S11). **Logged not done → S15** (the circular nav/orbit plot, #28) and **S16** (the
  now-stale status marks in `SCREEN_INVENTORY.md` / §3).

### T10 [O] Lower-panel accuracy pass — **DONE**
- **Read:** §4 + §14.4(a,b) + `PanelButtons`/`PanelMap`/`FlightCommands`.  **Build:** lighting bright / no-red,
  audible click, SWAP + inferred-entry INERT.  **DONE when:** preview + panel test, click plays, inferred inert.
- **SPLIT by owner decision (via the overseer), 2026-09-02, per C1.7:** three of the four done-criteria are
  preview-verifiable under the standing preview-only build-go; the fourth (the click actually being HEARD) can
  only be met in the capsule. The click was **BUILT** this session — asset + glue — and only its **on-glass
  verification** was deferred to the new held **S17** line. No `install`, no glass time, no gate touched (C1.12).
- **DONE 2026-09-02.** (a) **Lighting — bright, no red.** The policy moved out of the MonoBehaviour into a new
  pure `pure/PanelBehaviour.cs` (`PanelPolicy` + `PanelBoard`), so the one part of the panel that is a decision
  is now headless-testable and PNG-previewable. `PanelLight.Failed` **deleted** from `PanelMap.cs` and
  `FailColour` **deleted** from `PanelButtons.cs` — removed from the enum rather than left unused, since an
  unused red state is one edit from returning. A press that cannot act is now `PanelPressKind.Nothing`: click,
  no light, no action, and it does **not** disturb a lamp driven from live state (pressing an unpowered STRING
  no longer blacks out the row lamp). (b) **Inert:** SWAP 1/2/3 + ENABLE ENTRY REBOOT / BACKUP ENTRY / NORMAL
  ENTRY gated in `PanelPolicy.IsInert` — the dispatcher is **never called** for them, so no Part B wiring can
  make one act by accident; their confirmed plate-mates (ENABLE BACKUP PYROS, FIRE PYRD) are untouched.
  (c) **Click:** `build/make_click.py` synthesises `GameData/DragonScreen/sounds/panel_click.wav` (60 ms, mono
  16-bit 44.1 kHz, deterministic — two runs are byte-identical); authored here, nothing downloaded (C7), no
  attribution to keep. `src/PanelAudio.cs` plays it on **every** press including inert and unbacked ones — with
  the red dash gone the click is their only feedback. Deliberately `spatialBlend = 0`: 3D falloff cannot be
  judged with the game closed and its failure mode is silence, which is indistinguishable from "never played".
  (d) **Test:** `test/PanelTest.cs` gained `Lighting()` / `Inert()` / `Board()` — the panel suite went 118 →
  **1773 checks**, including a sweep that presses all 38 buttons both ways and asserts no lamp is ever anything
  but bright or as-modelled. `python plugin/build.py test` **green, 5572 checks, 0 failed**.
- **Gate (C1.3) met:** four preview PNGs rendered from the new preview-only `pure/PanelBoardPage.cs` and
  **inspected** — `panel_rest` (0 of 38 lit), `panel_armed` (DEORBIT NOW held bright on the left plate, 1 lit),
  `panel_fired` (EXECUTE from the RIGHT seat bright, POWER 1 holding, the left plate's armed lamp correctly
  **out**, 2 lit) and `panel_inert_swap` (SWAP 2 pressed → clicked, board entirely dark, 0 lit). No red in any
  of them; no `DisplayList` overflow. Plate order/spacing come from the prop-space transform dump in
  `REAL_DRAGON_SCREENS.md`, not from an image. **`PanelBoardPage` is a diagnostic, NOT a screen** — no `UiPage`,
  no Menu card, `ScreenPainter` never builds it.
- **§1.4 / C1.4 respected:** no label, plate, button or command row in `PanelMap.cs` was touched (verified by
  count) and no label doc was edited. The only `PanelMap.cs` changes are the behaviour enum and its comments,
  which §14.4(a) directs. `plugin/build/csc.rsp` churn reverted before commit (S11).
- **Bugs found and fixed on the way:** the first `PanelBoard` forced a lamp dark on a do-nothing press, which
  the glue does not — caught by the new test, model corrected to match the shipped code; and a fired arming was
  not clearing its lamp in the pure model (`PanelPolicy.ClearsArmedLamps` now owns both cases for glue and
  board alike).

### T11 [O] Capsule turntable — **SPLIT** into T11a (DONE) + T11b (render DONE, glue/glass HELD) — this line is closed, do not take it
- **Read:** §5.  **Build:** source the MaTte0 model → render sprites → drag-rotate.  **DONE when:** preview,
  drag rotates.
- **SPLIT 2026-09-02 (owner decision via the overseer; C1.7/C5 — living-register action only, `BUILD_PLAN`
  §5 is NOT edited and no `OVERRIDE` was asked for or given).** §5's C1 prerequisite — the MaTte0 CC-BY
  model — **is not in the repo**, and C7 bars going to look for it; the drag itself needs glass. So the
  model-independent half was built now and the rest is held.

### T11a [O] Capsule turntable — model-independent half (loader / frame-picker / drag maths) — **DONE**
- **Read:** §5 (C2–C4) + `CoverPage.cs` (the Capsule camera view).  **Build:** the sequence naming +
  frame picker + drag-delta→frame-index maths **with wrap**, all in `pure/`, driven against clearly
  marked PLACEHOLDER frames.  **DONE when:** `build.py test` green (incl. wrap), preview PNG inspected,
  §1.4 respected.
- **DONE 2026-09-02 — the stated DONE-when is met.**
  - **New `plugin/src/pure/Turntable.cs`** — §5's C3 naming (`art/cover/dragon_turn_NNN.png`, 36 frames @
    10°, resolved by the EXISTING loader: `ImageStore.ResolveAsset` in game, `PreviewMain.DrawCoverAsset`
    out of it), the frame picker (nearest-frame, wraps at both ends, non-finite resolves to the front so
    the page is never handed a NaN), and C4's drag maths. The state is a **continuous** `Turn`, not an int
    frame, so a drag smaller than one frame is not thrown away — 600 × 1 px lands exactly where 1 × 600 px
    does, and there is a test for it. Gearing is **one full sweep of the vehicle = one revolution**,
    expressed as a fraction of the slot so the gesture is the same physical sweep at 1280 / 2560 / the 2×
    cover render. Sign convention (drag right → the near face follows the finger) is documented and
    isolated in one constant; whether it reads right against the real render is a **glass** question → T11b.
  - **`CoverPage`** — the Capsule camera view no longer draws the `dragon.png` still; it draws one frame of
    the sequence, chosen by a new `Build(..., TurntableState)` overload (the older overloads open on the
    front). New public `CapsuleRect` is the ONE rect for the draw and for T11b's gesture region
    (PageAction's rule). `dragon.png` is **not** orphaned — `Pages.cs:981` still uses it.
  - **`plugin/build/make_turntable.py`** + the 36 shipped frames — a deterministic, dependency-light
    generator (the `make_click.py` precedent) writing **deliberately schematic** wireframes that carry
    the words PLACEHOLDER / NOT A RENDER - T11b plus their own frame number and azimuth. Rotation is made
    legible by four cues (near-side ribs, one accent index rib, an orbiting-and-foreshortening hatch, a
    top-down compass) because a body of revolution has the same silhouette from every angle and 36
    identical frames would prove nothing. §1.4: the marking is drawn by the same code that draws the
    sprite, `Turntable.Placeholder` gates both, and a **test** asserts the label exists and names T11b —
    so the stand-in cannot quietly outlive itself.
  - **Preview:** `ui_cover_turntable_0..3.png` — four quarter-sweeps applied through the REAL
    `Turntable.Drag` (not a frame index typed into the harness), giving frames 0 → 9 → 18 → 27 → back to 0;
    and `ui_turntable_sheet.png`, the only render that touches all 36 keys, so a missing frame shows up
    there rather than on the glass. Inspected: the sequence steps cleanly, no MISSING-asset line, no
    display-list overflow.
  - **`build.py test`: green, every suite 0 failed** — new `TurntableTest` suite, 224 checks (naming +
    wrap both ends, nearest-frame rounding over the seam, sub-frame accumulation, zero-width/NaN drags,
    the four-quarter loop, the page emitting exactly the picked frame and no frame on the other two camera
    views, the command budget). Two CS0162 warnings this work introduced were cleared, so the build is no
    noisier than it was found.
  - **NOT done here, on purpose (C1.1):** the real render, the glue drag plumbing, and the front-reset tap
    — all T11b, below.

### T11b [O] Capsule turntable — real render + drag plumbing — **DONE** (both halves) — this line is closed, do not take it
- ⛔ **`/next` skips this line.** Nothing is left in it that a build chat can take: the render half landed
  2026-09-02 and the glue half landed 2026-09-02 (both below). The one T11b question a PNG cannot answer —
  **how the drag FEELS on glass**, i.e. the sign and the gearing against the real sprites — is tracked by
  **S17**, batched with the T10 click and S10's RT camera; it is NOT a held T11b item and there is no
  NEEDS-WORK here. The first takeable task is **T12**.
- **Held on (resolved 2026-09-02 for the render half only):** §5's C1 model was not in the repo and C7 barred fetching it. **The
  owner placed the model** — `assets/reference/models/crew_dragon_falcon_9 (1).glb` (+ a 4k twin and the
  FBX zip as fallbacks) — and directed this pickup, so the render half ran as an **owner-directed** task
  (C1.12: the gate was opened by the owner, not by the chat; the work is preview-only and covered by the
  standing preview-only build-go).
- **Build:** (1) render §5's C2 turntable — 36 frames @ 10° (72 if 36 reads steppy), capsule **with trunk**,
  written at `Turntable.FrameW`×`FrameH` (512×1024) over `art/cover/dragon_turn_NNN.png`; (2) clear
  `Turntable.Placeholder`, which removes the on-screen marking and the label strip in one move;
  (3) confirm the render's rotation direction against `Turntable`'s documented sign — if it turns the
  other way, one constant flips; (4) the **glue drag plumbing** — `ScreenTouch` is `OnMouseDown`-only
  today, so press/drag/release → `Turntable.Drag` per frame, with the turntable state held beside
  `coverCam` in `ScreenPainter` and threaded through `FigmaUI.Build`; (5) §5 C4's **reset/"front" tap**
  (deliberately not built in T11a: it would have been a control that moves nothing); (6) consider warming
  the sequence in `ImageStore` — 36 first-touch `File.ReadAllBytes` + `LoadImage` calls one per new frame
  is a hitch per frame through the first revolution.

- **RENDER HALF DONE 2026-09-02 — items (1), (2) and (3) above.**
  - **New `plugin/build/render_turntable.py`** (the `make_turntable.py` precedent) — a headless Blender
    5.1 / Cycles-CPU script that re-bakes the sequence from the untracked model, so the frames are
    reproducible rather than a one-off. It **isolates Dragon + trunk and deletes the Falcon 9**, legs and
    Merlins, splitting on **material** name rather than object name (an artist's object naming survives a
    re-export less well than the material assignment does) and **refusing to render** if either the Dragon
    or the booster set is missing — a swapped model fails loudly instead of quietly putting a launch
    vehicle on the vehicle page. Camera is **orthographic**, so the silhouette cannot breathe between
    frames; camera + all three lights hang off one rig empty that turns about the vehicle axis, which
    fixes the lighting **relative to the camera** and stops the sequence strobing.
  - **The 36 frames** — `art/cover/dragon_turn_000..035.png`, 512×1024 RGBA, transparent film, 10° apart,
    the placeholder set overwritten in place. Framing is fitted to the **rotation-invariant** radius (the
    largest distance of any vertex from the axis) at 0.90 of the sprite width, so no frame in the sequence
    can clip: measured alpha extent is x∈[25,486], y∈[114,910] of 512×1024, identical top and bottom
    margins. Width, not height, is the binding constraint — the vehicle is 1.73 tall per 1 across and the
    sprite is 1:2.
  - **(3) The rotation direction was CHECKED, not assumed, and needed no flip.** The frames were measured:
    the trunk's solar array enters at the **left** limb on frame 3 and leaves at the **right** on frame 33,
    its centroid moving monotonically right the whole way — i.e. the near face follows a rightward drag,
    which is exactly `Turntable`'s documented sign. Brightness is even across the sweep (p95 luminance
    184–193 on every frame), which is the measurement that says the fixed-to-camera lighting worked.
  - **(2) `Turntable.Placeholder` cleared** — the on-screen PLACEHOLDER label and the 96 px strip
    `CoverPage` reserved for it both go in the one move, so the sprite now gets the whole slot. The
    marking **mechanism** is kept (§5 leaves a 72-frame variant open; a future stand-in must still be able
    to mark itself), and `TurntableTest.Marking()` was **turned over** rather than deleted: it now asserts
    the sequence is real, that the capsule view prints **no** placeholder/T11b text (read back off the
    display list, so a label drawn from anywhere else is caught too), and that the un-stripped sprite
    still fits the slot.
  - **Provenance (§1.4 / C1.4):** `assets/ASSET_PROVENANCE.md` gained §6 — "Crew Dragon Falcon 9" by
    **MaTte0 (@matteomansion)**, Sketchfab, **CC-BY 4.0**, rendered to `art/cover/dragon_turn_*.png` — plus
    a fourth line in the release-notes attribution list, flagged as the one whose output actually **ships**.
    The model files stay out of git (`assets/reference/` is gitignored in full), which is noted there along
    with the consequence: a fresh clone can build the mod but must re-download to re-render.
  - **One extra edit, declared:** `make_turntable.py` gained a `SUPERSEDED` stamp at the top — it now
    warns that running it overwrites the real sprites, and marks its "the model is not in the repo /
    T11b is held" framing as the position before the owner placed the model. Removing that false claim
    is part of clearing the placeholder, not separate work.
  - **Gate (C1.3) met:** `python plugin/build.py test` **green — every suite 0 failed, 5797 checks across
    the run**, the turntable suite 224 → 225. Preview re-rendered and **inspected** —
    `ui_cover_turntable_0..3.png` walk 0°/90°/180°/270°
    through the REAL `Turntable.Drag` and close the loop back on frame 0, and `ui_turntable_sheet.png`
    shows all 36: real capsule + trunk, clean step frame to frame, **no placeholder marks**, no
    MISSING-asset line, no display-list overflow. No new compiler warnings.
- **GLUE HALF DONE 2026-09-02 — items (4), (5) and (6). Owner-directed pickup (C1.12: the owner opened
  this, the chat did not); preview-only throughout, so it is covered by the standing preview-only build-go
  and NOTHING was installed.**
  - **(4) Press / drag / release.** `ScreenTouch` was `OnMouseDown`-only; it now also takes `OnMouseDrag`
    and `OnMouseUp` — the same Unity mechanism on the same component, so the MAS-ported raycast is
    unchanged and simply factored into one `PagePoint()` both paths call. The painter gained
    `TouchDown` / `TouchDrag` / `TouchUp` (the old `Touch` renamed for what it now is), holds a
    `TurntableState` + `TurntableTouch` per screen, and threads the state through a new
    `FigmaUI.Build` overload into `CoverPage.Build`. A press is offered to the capsule **last**, only on
    what `CoverPage.HitTest` returned `None` for, so the NEXT VIEW pill drawn over the slot still wins.
  - **(5) The reset is a TAP on the vehicle** (§5 C4), not a new button — nothing is added to a page whose
    layout is measured from the reference. What separates a tap from a drag is measured in **frames of
    rotation, not pixels** (`Turntable.TapSlopFrames` = half a frame, through the same gearing), so it is
    the same gesture at 1280, at 2560 and at the 2× cover render; and travel is the **path**, not the
    displacement, so a wiggle that ends where it began is a drag and does not snap to the front.
  - **(6) Resident texture is now BOUNDED.** 36 frames × 512×1024 RGBA = **72 MB** if a full revolution
    keeps everything it touched. The policy is a **window** — `WarmRadius` 2, so five frames — around what
    each screen is showing, plus the front pinned while anyone is looking: **one screen holds 5–6 frames
    (10–12 MB)**, three screens diverged hold 15–16 (30–32 MB), and when no screen is on the capsule view
    the sequence is released entirely. Residency is the **union over screens** on purpose: one shared
    window would have three screens evicting each other's frames and re-reading them from disk every
    frame, which is worse than the hitch it exists to remove. Warming loads **at most one frame per draw**,
    nearest-first, so arriving at the view costs a little on each of five frames instead of one hitch.
  - **The split that makes it testable:** all of the above is decided in **pure `Turntable`**
    (`Press`/`Move`/`Release`/`IsTap`, `InWindow`/`IsResident`/`ResidentCount`/`WarmOffset`/`Distance`)
    plus `CoverPage.CapsuleHit` (the press region — the SAME `CapsuleRect` the sprite is drawn from,
    PageAction's one-rect rule). `ScreenTouch` and the painter's three entry points hold **no decisions at
    all**, which is the most a headless test can be denied. `ImageStore` gained only a load, a `Destroy`
    and a sweep, acting on the pure policy's answers.
  - **One extra edit, declared:** `Turntable.Key` now reads a table built once at type init instead of
    building a string per call. The evict sweep asks for all 36 keys whenever the centre moves, and the
    draw asks for one every frame; the old version allocated on both paths, against DisplayList's
    no-allocation rule. Same keys, same wrap — the naming tests are untouched and still pass.
  - **Gate (C1.3) met — all preview/headless, no `install`, no glass:** `python plugin/build.py test`
    **green, every suite 0 failed — 10630 checks across the run**; the turntable suite 225 → **5058 checks** with three new sections
    (`Gesture` plays whole press→move→release chains exactly as the glue calls them, including the tap
    that resets and the wiggle that must not; `Region` pins the press region to the drawn rect and proves
    the capsule does not shadow the page's controls; `Residency` sweeps every centre and 864 three-screen
    combinations and proves the resident set is never the whole sequence). **No new compiler warnings**
    (11 before, 11 after — all pre-existing). Five new previews rendered and **inspected**:
    `ui_cover_turntable_drag_0..3.png`, driven through the real gesture from a press at the middle of
    `CapsuleRect`, step 0 → 8 → 16 → 24 with the vehicle turning the same way throughout; and
    `ui_cover_turntable_reset.png`, a press with no travel from frame 32, back on the **authored front**
    (frame 000, flag and meatball square-on) — identical to the press frame. The preview log also prints
    the residency numbers, from the same pure policy `ImageStore` acts on.
  - **§1.4 / C1.4:** N/A — no new source content, no art, no label; `PanelMap.cs` and the label docs are
    untouched. **FRONT unchanged:** the model's authored frame 0 is still the front (the owner's A1
    decision); nothing was re-baked and `art/cover/` is byte-identical.
- **DONE when:** ~~the drag is plumbed and confirmed on glass~~ → **the drag is plumbed** (met above).
  Confirming how it FEELS in the capsule is **S17's**, not this line's.

### T12 [S] Ascent/Launch page — **DONE**
- **Read:** §8 + §3.  **Build:** F9 schematic + event list.  **DONE when:** preview.
- **DONE 2026-09-02.** SCREEN_INVENTORY.md #14 / §3 row "Ascent / Launch" built as a new standalone page,
  `UiPage.Ascent` (`plugin/src/pure/AscentPage.cs`) — the one screen with **no public in-cabin frame at
  all** (confirmed absent, not just unfound), so §3 marks it "DATA-BUILDABLE, layout reconstructed +
  MARKED", one step past DeorbitBurnPrep (T7) / EntryProcedure (T8): those had a blurry/partial photo to
  anchor a layout guess against; this has none, so the whole page CHROME is ours, not just the spacing.
  **Real, from §8's mission-timeline pass (tier-1):** all 11 T+ events transcribed verbatim — Liftoff ·
  Pitch kick ~0:10 · Max-Q ~1:00 · Mach 1 ~1:09 · Stage-1b abort mode ~1:14 · MECO ~2:30–2:35 · stage sep
  ~2:35–2:39 · S2 ignition ~2:36–2:47 · SECO-1/orbit insertion ~4:20–8:43 · Dragon separation ~9:00–12:02 ·
  nose-cone open ~12:48–13:23. **Real, public Falcon 9 facts** (well-documented by SpaceX, not
  Dragon-interior-specific, same footing as PropSchematic's "16 Dracos in 4 quads" — no owner discussion
  needed): 9 Merlin 1D on stage 1, 1 Merlin Vacuum on stage 2. **"ACTIVE PHASE" is live, not decoration:**
  it reads `PageState.Phase`, the SAME field `DockingPage`/`DockingPageCentral` already display (threaded
  live by `VesselData.cs` from `Mission.Classify`) — reused, not new wiring (T13 still does that job),
  the same discipline T6/T7/T9 followed reusing their own live signals.
  **Ours, stated in the code:** the F9 + Dragon line-art profile (nose/capsule/trunk/interstage/stage
  proportions, grid-fin/leg/engine marks) and where each real event is called out against it — there is no
  photo to measure a layout from, so placement is a narrative ordering (ground → orbit reads bottom → top,
  matching the physical stack) rather than a scaled timeline or altitude plot.
  **Reachability:** Menu grid only for now (auto-discovered via `FigmaUI.IsPlaceholder`, 23 → 24 real
  cards, well inside the existing 3×10 grid capacity) plus the universal bottom bar (parents to Cover, same
  footing as DeorbitBurnPrep/EntryProcedure); a real phase-rail entry point is **T14**'s job.
  **Nav test:** new `FigmaUINavTest.Ascent()` — bottom bar → Cover, Menu lists it, body inert, confirmed a
  real page not a placeholder. `python plugin/build.py test`: green, Figma UI nav suite 256 → **263**
  checks, 0 failed. **Preview:** `ui_ascent.png` inspected — title, live "ACTIVE PHASE — ORBITING" line,
  the F9/Dragon stack (nose cone, capsule/trunk with ribs, payload/core step, interstage, grid fins,
  landing legs, engine ticks) with all 11 real event callouts legible and non-overlapping, no
  `DisplayList` overflow (54 of 100 commands); `ui_menu.png` re-inspected — 24 cards including
  "ASCENT / LAUNCH" as the new last card, still legible, no overlap. §1.4 respected throughout: every real
  fact is sourced (§8, or the existing live `PageState.Phase`); the reconstructed layout is named as such
  in the code header; no `PanelMap.cs` / label-doc edits. `plugin/build/csc.rsp` churn reverted before
  commit (S11).

### T13 [O] Live-data wiring — **SPLIT** into T13a + T13b + T13c — this line is closed, do not take it
- **Read:** §6 + `VesselData.cs`.  **Build:** replace placeholder constants.  **DONE when:** values live in-sim.
- **SPLIT 2026-09-02 (C1.7 / C5), no scope change.** The end-to-end read of §6 + `VesselData.cs` +
  `docs/TELEMETRY_REGISTRY.md` found the placeholder surface is ~140 readouts across nine pages — far more
  than one session, so it is cut into three lines that each finish. §6's own scoping is kept verbatim
  (*"the numeric VALUES are the placeholders"*): reference COPY — checklist wording, state words, the
  reference's own duplicate `LOOP A` label — is NOT touched by any of the three (see S19/S20 below).
  The idiom every line follows is the one **`SystemsPidPage` already ships** (T9): a value with a live or
  simulated-from-real source is drawn from `PageState`'s pre-formatted text, a value with no authoritative
  source is `—`, and nothing is invented (`docs/TELEMETRY_REGISTRY.md`: *"anything with no real source is
  `UNKNOWN — EVIDENCE REQUIRED` or SIMULATION — never invented"*).
- ⚠ **The old DONE-when ("values live in-sim") is NOT a preview-gate criterion** and no sub-line claims it.
  Each sub-line is DONE on `build.py test` green + preview inspected + the wiring provably reading
  `PageState`; **confirming the numbers on the glass belongs to `S18`**, which already names T13 for exactly
  this (*"Any T13/T14 criterion that turns out to need the capsule belongs here"*). No gate is lifted here.

### T13a [O] Live-data wiring — the VEHICLE page family — **DONE**
- **Read:** §6 + `docs/TELEMETRY_REGISTRY.md` + `VesselData.cs` + `pure/SystemsPidPage.cs` (the idiom).
- **Build:** `VehicleOverviewPage` (the eight cabin/loop/net-power gauges + the CONSUMABLES QTY column),
  `VehicleMechPage` (the five outer node values + the four seat tachs — the page did not even take a
  `PageState`), `SystemsTreePage` (the solar array's `DEPLOYED` word + the batteries' `4 / 4`, the two
  the file header itself flags as T13's), plus whatever `PageState` / `VesselData` fields those need.
- **DONE 2026-09-02.** Every number on the three pages now comes from `PageState`, in the idiom
  `SystemsPidPage` (T9) already ships — `valid ? s.SomeText : "—"` — and **nothing was invented** where a
  source is missing (`docs/TELEMETRY_REGISTRY.md`).
  - **`VehicleOverviewPage`:** the four cabin gauges → `s.Ppo2Text / CabinTempText / PressText / Co2Text`,
    the two loop gauges → `LoopAText` / **`LoopBText`** (two different loops under the reference's own
    duplicate `LOOP A` label — see **S20**, the owner's call), the two net-power gauges → `NetPwr1Text /
    NetPwr2Text`. **Every ring is drawn from the same `CabinReadout` that produced the number inside it**,
    so a needle can never disagree with its own readout. CONSUMABLES: `Power Unit 1/2 Energy` → the real
    state of charge, `Usable Deorbit Fuel / Oxidizer` → the Dragon's OWN tanks in kg (the parts that are
    neither booster nor second stage — the Dracos those tanks feed are what flies the deorbit burn).
  - **`VehicleMechPage`** (now takes a `PageState`; the `FigmaUI` call site updated): ACCELERATION →
    `AccelPosText`, RESISTANCE → `AccelNegText`, CENTRIPETAL → `AccelCentText`, PRESSURE → `PressText`.
    Those four `Accel*Text` fields have been computed in `VesselData` **for this page** since they were
    written and **nothing drew them** — the field's own comment says "broken out the way the reference's
    MECH PANEL does". **One extra edit, declared:** each node's ring was a FIXED 240° sweep, i.e.
    decoration beside a number that now moves; it is now the same 300°-arc-with-a-60°-gap every other
    vehicle gauge uses, filled from the reading, which needed three raw fractions (`AccelPos01 /
    AccelNeg01 / AccelCent01`, full scales **stated** in `VesselData.Acceleration`: 5 g axial — the scale
    the G dial already uses — and 2 g centripetal). The SEAT block now draws **one row per real seat**
    (`s.SeatCount`), the "draw what exists" rule `Pages.cs` states for `LightCount`.
  - **`SystemsTreePage`:** SOLAR ARRAY → the real `ModuleDeployableSolarPanel` state
    (DEPLOYED / STOWED / "n / m" / NONE), BATTERIES → the real count of parts holding charge over the
    parts that can. Both exceptions its own header listed are now closed.
  - **What stays dashed, and why it is right:** WATER UPRIGHTING and the four SEAT n TACH readings (no
    uprighting or per-seat-tachometer model exists anywhere in this build) and the four `Orbit n Subtank`
    rows + MARGIN (the real vehicle's tank split has no KSP counterpart, so choosing which litres are
    "Orbit 2 Subtank Oxidizer" would be inventing the number the label asks for). Each is stated at its
    own site.
  - **Gate (C1.3) met — preview + headless only, no `install`, no glass.** `python plugin/build.py test`
    **green, 0 failed**; the Figma UI nav suite 263 → **342 checks** with a new `VehicleLiveValues`
    section that builds each page with **two different fixtures** and asserts every value moved — a
    constant cannot pass that, whatever its value — plus no-feed builds, ring-fill counts, and a
    regression guard naming all 22 of the strings these pages used to hard-code. **The suite was proved
    non-vacuous:** re-hardcoding PPO2 to its old `"2.69"` fails it (2 checks), and it passes again on
    revert. **No new compiler warnings** (11 before, 11 after — all pre-existing).
  - **Previews rendered and inspected:** `ui_vehicle.png` (PPO2 2.86 / TEMP 21.8 / PRESS 14.72 / CO2 1.64
    with rings that match, LOOP 26.4 + 20.1, NET PWR −59 / −49, CONSUMABLES 18 % · 791.1 kg · 1308.0 kg
    and four honest dashes), `ui_vehiclemech.png` (1.42 g / 0.881 g / 14.72 psia / 0.00 g, rings moving
    with them, WATER UPRIGHTING and the seat tachs dashed), `ui_systemstree.png` +
    `ui_systemstree_live.png` (live sources over both the unpowered and powered trees). **Two NEW
    previews for the failure mode** — `ui_vehicle_nofeed.png`, `ui_vehiclemech_nofeed.png` — because
    every value being live makes "no feed" a look of its own, and both confirm dashes and empty rings
    rather than a plausible zero (`Pages.cs`: a screen confidently reading 0.0 is indistinguishable from
    a dead one). `plugin/build/csc.rsp` churn reverted before commit (S11).
  - **§1.4 / C1.4:** respected. No label, no `PanelMap.cs`, no label doc touched; §6's own scoping
    ("the numeric VALUES are the placeholders") kept, so the reference COPY is reproduced untouched —
    including two things that are **wrong but not this task's to change**: see **S19** (two mis-transcribed
    checklist strings), **S20** (the duplicate `LOOP A` label), **S22** and **S23**.
- ⚠ **Not claimed:** that the numbers are right ON THE GLASS. That needs the capsule and the gate is
  preview-only, so it stays with **S18**, which already names T13 for exactly this.

### T13b [O] Live-data wiring — the six subsystem sub-tabs + the Prop data band — **DONE**
- **Read:** §6 + `docs/TELEMETRY_REGISTRY.md` + `pure/VehicleSubsystemPage.cs` + `pure/VehicleSystems.cs`.
- **Build:** `VehicleSubsystemPage.DefOf`'s six subsystems (Crew · Prop · Power · Avionics · GNC · Thermal),
  4 headline gauges + 5 detail readouts each = 54 values, and the same numbers where `PropSchematic` re-draws
  them in its bottom data band (they are passed through, so wiring the source fixes both).
- **DONE 2026-09-02.** All 54 now come from `PageState` in the idiom `SystemsPidPage` (T9) and T13a already
  ship — `T(s.SomeText)` with each ring's fraction taken from the SAME source that produced its number — or
  are an honest dash. **30 live, 24 dashed**, and nothing invented (`docs/TELEMETRY_REGISTRY.md`). `DefOf`
  now takes the `PageState`; every wiring decision is stated at its own site.
  - **CREW (8 live / 1 dash):** the four cabin gauges are the overview's own `CabinReadout`; O2 / N2 tanks →
    the simulated stores in `VehicleSystems` (they fall with real crew, real power, a real leak); Potable
    Water → **TAC's own `Water` resource on our side of a dock** (new `LsState.HasWater/WaterLitres/Water01`
    — no mod or no tank ⇒ dash); Crew Aboard → `s.CrewText` + a new `Crew01` for its bar. Humidity dashes:
    nothing models it.
  - **PROP (4 live / 5 dash):** OX (NTO) and FUEL (MMH) → the **Dragon's own tanks as a fraction BY MASS**,
    accumulated in the SAME single parts pass that already prints their kilograms, so the percentage and the
    kg row beside it are two views of one number. Prop Remaining → both tanks together; Draco Duty → the live
    RCS demand through the new `PropSchematic.MaxDuty`, the same function that lights the schematic's own
    segments. Helium, prop temp, chamber pressure, SuperDraco temp and thrust-available dash — no resource
    and no model answers any of them. **One fix carried in passing:** `NTO`/`MMH` are now in the propellant
    buckets, so T13a's `Usable Deorbit Fuel / Oxidizer` rows also stop reading `—` under RealFuels.
  - **POWER (5 live / 4 dash):** BATTERY SOC → `PowerText`/`Power01`; ARRAY + Array Output → the panels' real
    `flowRate` (ring = flow / their own `chargeRate`, a real fraction, not a chosen full scale); Net Power +
    Charge Rate → the two `NetPwr*W` added up, in W and in kW. Bus A / Bus B volts, Bus Load and Battery Temp
    dash: KSP charge has no voltage and there is no per-bus-load or battery-thermal model.
  - **AVIONICS (2 live / 7 dash) — nearly the honest answer, not a gap.** This build models no computer
    load, bus traffic, link budget, storage or GPS state, and no KSP quantity stands in for FC LOAD, BUS
    TRAFFIC, LINK MARGIN, STORAGE, FC1/2/3, GPS Sats or Data Rate, so those seven dash. **S24** (owner
    call, resolved 2026-09-02 to option (b)) wires Uplink + Downlink — and the S-BAND COMMS checklist row
    — to stock KSP's own CommNet (`Vessel.Connection`), the one honest source this subsystem has; GPS
    stays untouched (a comm link is not a GPS source). The tab's other live signal is its FDIR severity,
    which already colours the tab and fills the ALERTS view.
  - **GNC (9 live / 0 dash with a target):** roll/pitch/yaw rate → `vessel.angularVelocity`, **hoisted out of
    `Docking()`** into a new `VesselData.Rates()` — they are not target-dependent and inside that block they
    were simply stale on this tab with no target; RCS FUEL → the Prop tab's own combined tank fraction (the
    Dracos ARE the RCS, so it is one number on two pages); Attitude Err → `AlignText`, dashed with no target
    (an error needs something to be an error against); Body Rate, Altitude, Velocity (through the shared
    `OrbitReadout`, so this page cannot read orbital speed on the pad while FLIGHT reads surface speed) and
    Pointing → the live authority word.
  - **THERMAL (4 live / 5 dash):** LOOP A / LOOP B → the coolant model; SHIELD + TPS Max → the hottest
    structure in °C, ringed by that part's fraction of its OWN maximum (margin to limit, no invented scale).
    Radiator, both loop flows, heat reject and cabin HX dash — the loops are modelled as temperatures, not
    as flows.
  - **Two formats for one datum, stated:** a gauge prints its unit on its own line so its text is bare
    ("2.60"), a row prints one string so its carries the unit ("2.60 kW"). Where the template shows the same
    quantity as both, `VesselData` formats the pair side by side from one value, so they cannot drift.
  - **Gate (C1.3) met — preview + headless only, no `install`, no glass.** `python plugin/build.py test`
    **green, 0 failed**; the Figma UI nav suite 342 → **534 checks** with a new `SubsystemLiveValues` section
    that builds all six tabs with two different fixtures and asserts every wired value moved, that each drops
    to a dash with no feed, that the 50 old hard-coded constants never return, that the rings fill one per
    sourced gauge and empty on a dead feed, that the Prop data band carries the same live values, that GNC
    keeps its rates with NO target and follows `OrbitReadout` on the ground — and, inverted, that **AVIONICS
    invents nothing**: not one fixture value may appear on it, on either fixture. **Proved non-vacuous:**
    re-hardcoding PPO2 to `"2.69"` and Array Output to `"3.4 kW"` fails it (4 checks), and it passes again on
    revert. **No new compiler warnings** (11 before, 11 after — all pre-existing).
  - **Previews rendered and inspected:** `ui_vehiclecrew` (2.86 / 21.8 / 14.72 / 1.64 with matching rings,
    86 % · 93 % · 108 L · 3 / 4, Humidity dashed), `ui_vehiclepropulsion` (OX 87 / FUEL 83, Prop Remaining
    86 %, Draco Duty 0 % at rest), `ui_vehiclepropulsion_firing` (**Draco Duty 67 %** with the quads lit —
    the number and the segments move together), `ui_vehiclepower` (SOC 18, ARRAY 2.60 kW, Net Power −108 W,
    Charge Rate −0.11 kW, four dashes), `ui_vehicleavionics` (nine dashes and four empty rings — the look of
    an unmodelled subsystem, and the strongest argument in the set), `ui_vehiclegnc` (−0.05 / 0.12 / 0.31
    °/s, RCS FUEL 86, 5.4 deg, 123.4 km, 2280 m/s, AUTO), `ui_vehiclethermal` (26.4 / 20.1 / — / 312 °C),
    `ui_vehiclepower_alarm` re-inspected. **One NEW preview** — `ui_vehiclecrew_nofeed.png` — because the
    template is shared by all six tabs and "no feed" is now a look of its own: every value dashes, every
    ring empties. Two fixture bugs the wiring exposed were fixed in the same pass: the FIRING render's duty
    and the ALARM render's SOC are now derived the way `VesselData` derives them, so neither shows a number
    that disagrees with the ring beside it. `plugin/build/csc.rsp` churn reverted before commit (S11).
- **§1.4 / C1.4:** respected. No label, no unit, no `PanelMap.cs`, no label doc touched; §6's own scoping
  ("the numeric VALUES are the placeholders") kept, so the left checklist and every caption are reproduced
  untouched — including the Power tab's static `4 / 4` and `Deployed` beside now-live sources (**S25**) and
  the status words that stay confident on a dead feed (**S22**).
- ⚠ **Not claimed:** that the numbers are right ON THE GLASS. That needs the capsule and the gate is
  preview-only, so it stays with **S18**, which already names T13 for exactly this.

### T13c [O] Live-data wiring — the procedure & prox-ops pages — **DONE**
- **Read:** §6 + `docs/TELEMETRY_REGISTRY.md` + `pure/ManualChuteDeployPage.cs` / `pure/DockingSimPage.cs` /
  `pure/SuitCheckPage.cs` / `pure/DeorbitBurnPrepPage.cs` / `pure/NavPage.cs`.
- **Build:** `ManualChuteDeployPage`'s hard-coded top telemetry strip (ACTIVE PHASE · SPLASHDOWN TIME ·
  INERTIAL VELOCITY / ALTITUDE / APOGEE / PERIGEE / INCLINATION — every one of them already live in
  `PageState`); `DockingSimPage`'s ROLL/PITCH/YAW, PYR, RANGE and RATE (the page takes no `PageState`);
  `SuitCheckPage`'s four SUIT n DELTA PRESSURE rows; `DeorbitBurnPrepPage`'s four dashed SLEW rows (decide
  live-or-stay-dashed against the registry — it is a commanded inertial slew, which may be Part B's, not
  T13's); and `NavPage`'s approach chord, which wants the target orbital elements `PageState` does not carry
  yet (the gap T6 logged at line ~269).
- **DONE when:** the same criteria as T13a.
- **DONE 2026-09-02.** The five pages the line names are wired, in the idiom `SystemsPidPage` (T9),
  T13a and T13b already ship — a value with a source comes from `PageState`, a value without one is an
  honest dash, and nothing is invented (`docs/TELEMETRY_REGISTRY.md`). **13 readouts made live, 8 kept
  dashed on purpose, 1 chord made real.** Every decision is stated at its own site.
  - **`ManualChuteDeployPage` — the top telemetry strip (7 live).** ACTIVE PHASE → `s.Phase`,
    SPLASHDOWN TIME → `s.SplashdownText` **gated on `SplashdownShown`** (the registry's "N/A
    off-return" for SPLASHDOWN_ETA), INERTIAL VELOCITY → `s.Velocity`, ALTITUDE → `s.Altitude`,
    APOGEE / PERIGEE → `s.Apoapsis`/`s.Periapsis` **gated on `ApogeeShown`/`PerigeeShown`**, the same
    flags every other page's apsides follow, INCLINATION → a new `s.InclinationDegText`. The seven
    strings replaced were the reference export's own baked values ("7.67 km/s", "T-01:08:36", …) —
    §6's "the numeric VALUES are the placeholders". The PROCEDURE COPY below the strip (altitudes,
    step names, actions) is reference text and is untouched.
  - **`DockingSimPage` — ROLL / PITCH / YAW, PYR, RANGE, RATE (5 live, drawn in 8 places).** The page
    did not take a `PageState`; it does now (`FigmaUI` call site updated). The ring readouts and the PYR
    block are **the same group, not two** — `docs/SCREEN_EVIDENCE_MATRIX.md` has them as "Rotation
    readouts ROLL / PITCH / YAW (grouped 'PYR'), each a value in degrees" — so both now read the SAME
    strings. The placeholder era had them **disagreeing** (`0.0°` around the rings, `180.0` in the
    block), which is exactly the "a needle that disagrees with its own readout" failure T13a's wiring
    exists to make impossible. RANGE / RATE are the HUD's own `RangeText`/`RateText`, so there is one
    range and one closing rate in the build. **No target ⇒ all eight dash**: there is nothing to be
    misaligned with, and a confident `0.0°` of error against nothing is the worst reading on the page.
  - **`SuitCheckPage` — the four SUIT n DELTA PRESSURE rows: DASHED, and that is the finding.** Nothing
    in this build models a suit (not `VehicleSystems`, not `CabinEnvironment`, and KSP has no per-crew
    pressure resource), so `"0.01psi"` had no source at all — and a constant sitting in a LEAK CHECK is
    the worst place in the build for one: four suits reading a confident 0.01 psi is how a screen says
    "no leak" when it knows nothing. TIME REMAINING keeps the real procedure countdown; the STATUS
    words are reference copy (**S22**), untouched.
  - **`DeorbitBurnPrepPage` — the four SLEW rows: STAY DASHED, decided (this line asked for the call).**
    ROLL / PITCH / YAW / MAXIMUM ALTITUDE RATE under "SLEW FOR DEORBIT BURN" are a **commanded** inertial
    slew, and `docs/TELEMETRY_REGISTRY.md` carries no row for any of them — no SLEW_* datum, no
    authority, no source. The near-misses were considered and rejected **as inventions of MEANING, not
    of a number**: the docking-relative errors are an error against a docking TARGET; the body rates
    `Rates()` publishes are how fast we are turning, not where we are being told to turn to; a latched
    peak rate under "MAXIMUM ALTITUDE RATE" would be a plausible number under a label this build cannot
    confirm the meaning of. Their real source is the thing that will COMMAND the slew — **Part B, T21**.
    Written into the page header, and **guarded by a test** that no fixture value may appear on those
    rows. FC SLEW beside them stays live on the same Part B seam.
  - **`NavPage`'s approach chord — now target-relative (the T6 gap at line ~269 is closed).** T6 ran the
    chord to PERIAPSIS and said why: the target's orbital state was not in `PageState`. It is now —
    `HasTargetOrbit` / `TargetRadiusM` / `TargetPhaseRad`, filled by a new `VesselData.TargetPlot()` —
    so the chord runs to **where the target actually is**, with a small diamond marking the endpoint (a
    line has to end somewhere identifiable; **no label is invented** for it). The angle is a **phase
    angle measured from US**, not from periapsis, so the far end inherits the same guarantee the near
    end has: the plot places our marker from our own radius, and an angle measured off that marker
    cannot disagree with it. **Stated approximation:** the target is projected into our orbital plane
    (a 2D plot has nowhere else to put it; on a real rendezvous the planes are all but identical).
    A target with no orbit around our body draws **no chord at all** rather than reverting to a line it
    had to invent.
  - **Gate (C1.3) met — preview + headless only, no `install`, no glass.** `python plugin/build.py test`
    **green, 0 failed**; the Figma UI nav suite 534 → **633 checks** with a new `ProcedureLiveValues`
    section that builds each page with **two different fixtures** and asserts every wired value moved,
    that each drops to a dash with no feed / no target, that the 11 old hard-coded constants never
    return, that the three docking axes are each drawn **exactly twice from one string**, that the
    chord's ENDPOINT moves with the phase angle (the "not a constant" proof for a line rather than a
    string), that there is **no chord** with no target or no comparable orbit, that the plain NAV view
    never grows one — and, inverted, that **the deorbit SLEW rows invent nothing**: not one fixture
    value may appear on them. **Proved non-vacuous:** re-hardcoding the ROLL readout to `"0.0°"` and
    INERTIAL VELOCITY to `"7.67 km/s"` fails it (6 checks), and it passes again on revert. **No new
    compiler warnings** (11 before, 11 after — all pre-existing).
  - **Previews rendered and inspected:** `ui_manualchute.png` (ORBITING · 2280 m/s · 123.4 km · 124.0
    km · 121.9 km · 0.13°, SPLASHDOWN TIME correctly dashed **off** a return), `ui_docking.png`
    (ROLL 15.0° / PITCH 0.1° / YAW 0.1° with the PYR block reading the identical 0.1 / 0.1 / 15.0,
    RANGE 202.6 m, RATE −0.25 m/s), `ui_rendezvous.png` (the chord now runs from the vehicle marker to
    a diamond OUTSIDE the ellipse — a target above and ahead, which is what an approach from a lower
    phasing orbit looks like), `ui_suitcheck.png` (four dashed ΔP rows with dimmed dash icons),
    `ui_deorbitburnprep.png` re-inspected (four dashes + NOT ENGAGED, unchanged). **Four NEW previews
    for the states the new values create** — `ui_manualchute_descent.png` (the strip fully live, T−
    01:08:36), `ui_manualchute_nofeed.png` (all seven dashed and dim), `ui_docking_notarget.png` (all
    eight dashed), `ui_rendezvous_notarget.png` (no chord, no diamond, plot otherwise intact) — because
    every value being live makes "no feed" and "no target" looks of their own.
    `plugin/build/csc.rsp` churn reverted before commit (S11).
- **§1.4 / C1.4:** respected. No label, no unit, no `PanelMap.cs`, no label doc touched; §6's scoping
  ("the numeric VALUES are the placeholders") kept, so all the procedure copy — the chute steps and
  actions, the suit checklist and its STATUS words (**S22**), the Crew Interrupt Conditions' "altitude"
  wording (**S13**) — is reproduced untouched. One rendering note, not a change: SPLASHDOWN TIME prints
  the shared formatter's `"T- 01:08:36"` where the reference export baked `"T-01:08:36"`; the space
  comes from `VesselData`'s one splashdown formatter and bending it for a single page would be the
  drift this wiring exists to prevent.
- ⚠ **Not claimed:** that the numbers are right ON THE GLASS. That needs the capsule and the gate is
  preview-only, so it stays with **S18**, which already names T13 for exactly this. **One thing S18
  should look at specifically:** the approach chord's endpoint under a real ISS target, which is the
  one value here derived from geometry rather than read straight out of an existing field.

### T14 [O] Touch wiring — **DONE**
- **Read:** §6 + §4.  **Build:** display-only controls → real per the decisions.  **DONE when:** controls act (+ tests).
- **DONE 2026-09-02.** §6's touch-wiring bullet names four groups of display-only controls; all four are
  now settled, three by building and one by verifying it was already built.
  **What acts, and on whose authority — the whole task is an application of §14.4(a)+(b), never a new
  decision:**
  - **Manual Chute Deploy — the 12 per-step ACTION buttons** (`pure/ManualChuteDeployPage.cs`). Four of
    the five step labels name a control the LOWER CONSOLE PLATE also carries, so they were mapped to the
    SAME `PanelCommand` by reading the step's own label against §4's modelled inventory — ENABLE BACKUP
    PYROS→`EnableBackupPyros`, DEPLOY DROGUES→`DroguesAndMains` (§4's confirmed "2 drogues → 4 mains"),
    DEPLOY MAINS→`MainsOnly`, FIRE PYRO→`FirePyro` — and dispatched through the SAME `FlightCommands.Run`
    the plate uses, with the outcome read by the SAME `PanelPolicy` (which is where §14.4(a)+(b) live).
    **No second policy was written and none may be**: pressing DEPLOY DROGUES on the glass and DROGUES &
    MAINS on the plate cannot come to different answers. Today that means the four ENABLE BACKUP PYROS
    rows ARM and light BRIGHT, and the rest click into silence — §14.4(a) flight actuation with no flight
    software yet; Part B (§B12.5) lights them with no edit here. The lamp is read from
    `PageState.BackupPyrosArmed` ← `FlightCommands.BackupPyros`, i.e. the flag the console dash reads,
    never a latch of the page's own — the one-state-two-surfaces rule. "Monitor altitude" is the one
    action naming no command (the crew watching the live ALTITUDE the strip above already draws), so it
    stays dark, and a test pins that it is the ONLY one.
  - **Manual Docking — the clusters** (`pure/DockingSimPage.cs`). The two centre **LARGE↔PRECISE**
    magnitude toggles are REAL (both states are in the iss-sim spec the page was built from; selecting one
    is screen state and flies nothing) and flip per screen. **Settings** opens the settings page — the
    destination the Cover's own Settings button already has, so no second destination was invented. The
    twelve direction pads, plus Reset Positions, would MOVE the vehicle: §14.4(a) makes them an honest
    no-op, so they resolve to a named act, log, and do nothing — no light, no action, **no red**. They are
    a seam, not a dead rect (`IsActuation` names the set; Part B replaces the dispatch without touching
    the geometry). ⚠ Whether they should instead fly the capsule by hand is an OPEN OWNER CALL — **S28**.
  - **Suit Leak Check — the fail branch + timer** (`pure/SuitCheckPage.cs`). **TRY ADDITIONAL TIMER**
    re-runs the countdown (a complete instruction; the page already owns the timer) and **FINISH** ends at
    step 2.5 and raises the completion popup. **TROUBLESHOOT resolves but is UNAVAILABLE, and is drawn
    dimmed to say so** — it responds to a suit reading "Failed Low" and this build models no suit at all,
    which is the same fact that dashed the four DELTA PRESSURE rows in T13c. `FailBranchLive` is the one
    constant to change the day a suit is modelled. Dimming an unavailable control is the existing
    no-source-so-do-not-pretend idiom applied to a control instead of a value; §14.4(d) keeps the branch
    drawn, and nothing here removes it.
  - **The console panel (§4) — VERIFIED ALREADY LIVE, not rebuilt.** T10 shipped it: `PanelButtons.cs`
    holds the colliders and dispatches through `FlightCommands.Run`, and `pure/PanelBehaviour.cs` is
    §14.4(a)+(b) in code (two lamp states, no red; SWAP 1/2/3 + the three entry-mode toggles inert). T14
    adds no code there and asserts its answers instead (`ConsolePanelUnchanged`), because the chute page
    now BORROWS that policy — a change to it must not silently un-wire a second surface.
  - **Bonus, and in scope by T5's own note:** the **FUNCTIONS | ALERTS** toggle on the six subsystem
    sub-tabs. T5 drew it and left it inert, saying in as many words that *"wiring the tap is T14's job"*.
    It is a pure screen-state flip, so it is now one. Its geometry moved into `TabX`/`TabW`, read by BOTH
    the draw and the new `ToggleHit`, and the render is pixel-identical (`ui_vehiclepower_alerts.png`
    re-inspected).
- **Where the state lives:** a new pure `PageControls` (`pure/PageAction.cs`) carries the three bits a
  touch flips — the ALERTS tab and the two cluster magnitudes — held per screen by `ScreenPainter`, on the
  same footing as the Cover camera and the turntable (per screen, not persisted, reset on a page change).
  Deliberately NOT in `PageState`: that is what the VEHICLE is doing, this is what the crew member at THIS
  screen is looking at, and putting it there would make `VesselData` invent a value every tick for state
  it does not own. Threaded through one new `FigmaUI.Build` overload; every existing caller is unchanged.
- **The one-rect rule (PageAction's own) enforced everywhere it was newly needed:** the chute page's row
  ladder is now walked ONCE at class-init (`HighY`/`StdY`/`Actions`) and read by both `Build` and
  `ActionRect`, where `Build` used to accumulate its own `y`; the docking clusters and bottom controls
  gained `ClusterRect`/`BottomRect`, drawn from and hit through the same call. Both refactors are
  pixel-neutral — `ui_manualchute.png` and `ui_docking.png` re-rendered and compared.
- **Gate (C1.3):** `python plugin/build.py test` **green — new `TouchWiringTest` suite, 260 checks, 0
  failed; 11,010 → 11,270 total, 0 failed, no new warnings.** The new suite aims at the centre of every
  rect the pages publish, at three screen sizes, and its most important checks are the NEGATIVE ones:
  the twelve pads + Reset Positions are actuation and the magnitude toggles are not; the three chute
  actuation commands can never light in either flag state; the modal popup swallows HALT and FINISH;
  adjacent chute rows do not touch (the 14px dead band is real, so a fat press cannot fire the step
  below); every cluster button is a distinct act; TROUBLESHOOT is unavailable while the timer control is
  not. **Preview:** two NEW renders for the states the new controls create — `ui_manualchute_armed.png`
  (exactly four bright "Arm and verify" plates, label knocked out on accent, every other action
  unchanged; 279 → 283 commands, no overflow) and `ui_docking_precise.png` (both centre toggles reading
  PRECISE, cluster geometry unmoved) — plus `ui_manualchute.png`, `ui_docking.png`, `ui_suitcheck.png`
  (TROUBLESHOOT dimmed, TRY ADDITIONAL TIMER bright, FINISH unmoved) and `ui_vehiclepower_alerts.png`
  re-inspected. All clean: no overlap, no clipping, no `DisplayList` overflow.
- **§1.4 / C1.4 respected.** No label, no unit, no `PanelMap.cs`, no label doc touched. Every step→command
  pairing is read off the step's OWN label against §4's inventory and nothing else — a test re-derives the
  same map from the labels so the two must keep agreeing. One header CORRECTION, not a content change:
  `SuitCheckPage`'s header claimed the CLEAR line / Failed-Low / TROUBLESHOOT / "2.5 Contact SpaceX"
  content was "deliberately not drawn" — the code has always drawn all of it, and §14.4(d) is the owner's
  decision to KEEP it as a marked reconstruction. The comment predated §14.4(d) and would have talked
  someone into deleting owner-decided content, so it now says what the code does and why.
- ⚠ **Deliberately NOT done (logged, not built — C1.1):** the in-page / phase-rail entry points that T7,
  T8, T9 and T12 each parked on T14. Those tasks assign the JOB here, but WHICH content sits behind the
  Cover rail's two generic "Procedure" slots is not in any source — `SCREEN_INVENTORY.md` line 83 lists
  the rail's real per-item content as 🔴 unbuilt — and there are three candidate pages for two slots. That
  is a §1.4 tier-3 invention, so it is the owner's, not a build chat's (C1.4/C1.12). **Nothing is
  unreachable meanwhile**: all six pages are on the Menu grid and under the global bottom bar. Logged as
  **S27** and posed as an overseer prompt.
- ⚠ **Not claimed:** that any of this is usable ON THE GLASS — whether a finger can hit one chute row,
  whether an inert docking pad reads as deliberate, whether the dimmed TROUBLESHOOT reads as unavailable.
  The gate is preview-only, so those go to **S18**, appended to its glass-checklist as **G5–G9** at the
  moment each arose, per the owner's 2026-09-02 directive.

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

## Stray triage (owner, via the overseer, 2026-09-02)
Open strays are triaged into 5 tiers. Recommended pre-Part-B order: **Tier 1** (correctness bugs)
then **Tier 2** (hygiene) — S4 → S5 → S22 → S6 → S19, then the hygiene items. The trivial hygiene
items **S7 + S11 + S21 + S30** MAY be run as one scoped "hygiene sweep" chat. **Tier 3** are
owner-decisions pending. **Tier 4** are deliberately-scheduled builds — S15 is a real unbuilt Part A
screen; build it or consciously cut it before Part B. **Tier 5** stay held / owner-action. Starting
T15 / Part B is the owner's separate gate call — the standing build-go is scoped to "Part A pure
code" only. **Applied 2026-09-02.**

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

### S3 [S] `docs/FLIGHT_SYSTEMS.md` is referenced but does not exist — **TODO** (T1 part DONE; rest → T15) — [TIER 5: held / owner-action / Part-B-bound]
Live references point at a missing file: `plugin/src/pure/MissionPhase.cs`, `plugin/build/audit_comments.py`,
and `docs/INDEX.md` (lists it as existing). The §8 flight facts it should hold currently live only in
`BUILD_PLAN.md`. T15 creates it; T1 must at minimum stop `INDEX.md` advertising a missing file.
**T1 part DONE 2026-09-02:** `INDEX.md` no longer lists it as existing — it now says explicitly that the file
does not exist, that the §8 flight facts live in `BUILD_PLAN.md` until T15 creates it, and that the two code
comments (`pure/MissionPhase.cs:54`, `build/audit_comments.py:233`) are not a live link. **Still open:** those
two comments, and creating the file — both T15.

### S4 [S] Phase classifier reads PHASING while still sub-orbital — **DONE 2026-09-02**
From the 2026-08-29 screen audit (U1). **Turned out already fixed** — the SAME-DAY audit-response commit
`98cf500` ("U1: phase classifier stays ASCENT until the orbit is closed") had already added
`MissionInputs.OrbitClosed` (`pure/MissionPhase.cs`), populated it in `VesselData.cs:78` as
`v.orbit.PeA > v.mainBody.atmosphereDepth`, gated `Mission.Classify`'s Phasing/Approach branch behind it
(`pure/MissionPhase.cs:76-84`), and added the regression check `"targeted but orbit not closed is still
ascent (U1)"` in `plugin/test/LayoutTest.cs:525`. This S4 register line was logged from the same audit finding
without noticing the fix commit had already landed — a duplicate, not a live bug.
**This session:** read both files end-to-end, confirmed the orbit-closed gate and test are present and
committed (`git log -S"OrbitClosed"` → `98cf500`, ancestor of HEAD), confirmed no other code path re-reads
PHASING without the gate. `python plugin/build.py test`: green, ALL SCREEN SUITES PASSED (MissionPhase suite
6/6, including the U1 check). No code changed this session → no label changed → preview re-render N/A (C1.3).

### S5 [S] Nuisance PROPELLANT CAUTION off the spent ascent stage — **DONE 2026-09-02**
- **Owner-directed task (not the `/next` default, C1.1).** From the same audit (U2). The propellant gauge
  correctly shows what the LIT engines are drinking, so the near-spent S2 reads ~16% near SECO →
  `Alarms.Low(Propellant01)` → PROPELLANT CAUTION → whole vehicle STATE CAUTION, during an entirely nominal
  ascent. Dragon's own return propellant is full at that point.
- **Chosen fix: alarm on Dragon's own return-propellant budget, not "suppress while an ascent stage is lit."**
  `VesselData.VehicleSources` already computes `PageState.DragonProp01` — the Dragon-only tanks (parts that
  are neither booster nor second stage) as a fraction by mass, the exact number the PROP/GNC pages already
  show as "Prop Remaining" / "RCS FUEL". That field is a direct, already-tested answer to "does the crew have
  enough propellant", independent of which stage happens to be firing — it does not need a new "is this an
  ascent stage" classifier (which would only patch the ascent case and would need re-deriving for every other
  phase a non-Dragon stage might be lit), and it can never itself go wrong when the wrong engine is lit,
  because Dragon's tanks do not move when the booster's or second stage's do.
- **Build (`plugin/src/pure/Alarms.cs`):** new `Alarms.PropellantSeverity(PageState s)` = `Low(s.DragonProp01)`
  — ONE function (this file's own "ONE FUNCTION, BOTH CALLERS" rule), documented with the rationale above, so
  every propellant-alarm consumer routes through it and can never disagree. `Alarms.Mask`'s FLIGHT bit and
  `Alarms.VehicleSeverity` (→ `SystemSeverity` → `ScreenPainter`'s STATE CAUTION banner) now call it instead of
  `Low(s.Propellant01)` directly. Three call sites outside `Alarms.cs` switched the same way: the FLIGHT page's
  "PROPELLANT" status dot (`pure/Pages.cs`'s `Status`), the Vehicle page's PROP sub-tab colour
  (`VehicleTabBar.Severities`), and the Propulsion subsystem banner (`VehicleSubsystemPage.LiveSeverity`).
  **Left unchanged, on purpose:** `s.Propellant01` itself (still correctly "what the lit engines are
  drinking" for the FLIGHT page's own dial, `Pages.cs:716`, captioned with the stage it reads) and
  `StepInputs.Propellant01` (`StepList.cs`'s `PropellantLoad` pre-launch fuelling-complete check, a different
  question about the currently-active stage's own load) — neither is an alarm read, so neither needed to move.
- **Test (`plugin/test/PageTest.cs`, `AlarmRouting`):** `Healthy()` now also sets `DragonProp01 = 0.8` (it only
  set `Propellant01` before, which would have silently defaulted `DragonProp01` to 0.0 — an accidental ALARM
  — once the routing changed); the existing "low propellant lights FLIGHT/VEHICLE" regression now drives
  `DragonProp01` (what actually lights it post-fix) instead of `Propellant01`. New **S5 regression**: a
  `lateAscent` state with `Propellant01 = 0.16` (near-spent S2, matching the audit's ~16%) and
  `DragonProp01 = 0.97` (Dragon's own tanks untouched) — asserts the raw stage reading alone WOULD have
  alarmed (`Alarms.Low` on it is non-nominal, proving the test is not vacuous), then asserts
  `PropellantSeverity`/`VehicleSeverity`/`SystemSeverity`/`Mask` are all nominal/zero for that same state —
  covering the FLIGHT-tab bit, the vehicle-wide severity, and the STATE CAUTION banner in one fixture.
- **Verified no other fixture regressed:** checked every `PageState`/`DragonProp01` construction site across
  `plugin/test/` and `plugin/preview/` — `FigmaUINavTest.VehicleFixture` already sets `DragonProp01 = 0.85`
  (unaffected), `PreviewMain`'s shared base fixture already sets a nominal `DragonProp01 ≈ 0.858` from the
  same CONSUMABLES kilograms it prints (unaffected — `build.py preview` renders identically), and no other
  severity/alarm assertion in the suite depends on `Propellant01` driving the alarm.
- **Gate (C1.3):** pure logic fix, no layout/new page — no new preview PNG is meaningful here (the existing
  base fixture's propellant values were already nominal under both old and new routing, so nothing visibly
  moves); `python plugin/build.py test` **green, 15 suites, 0 failed** (Figma UI nav 654, page tests 695
  including the 5 new S5 checks, no new warnings). §1.4 respected: no `PanelMap.cs` / label-doc edits.
- **Left uncommitted, not mine to touch (C1.1):** the working tree already carried unrelated, unstaged edits
  to `VesselData.cs` / `VehicleMechPage.cs` / `VehicleOverviewPage.cs` (S22's own payload — S22's REGISTER
  entry reads DONE and is already committed, but that commit evidently did not include its code) and a
  REGISTER.md addition logging **S31**, from work already in progress before this session started. Neither is
  S5's concern; both are left exactly as found, staged out of this task's commit. **Flagged for the owner,
  not decided here:** S22 shows DONE + committed in `REGISTER.md`/git log, but its code changes are sitting
  uncommitted in the working tree — worth a look before that work is lost to a `git clean`/reset elsewhere.

### S6 [S] Both NET PWR dials read exactly 0 W — **DONE**
- **Verified against `pure/CabinEnvironment.cs`: the model is correct, the bug was in the glue.**
  `Cabin.Compute` turns `CabinInputs.PowerFlow` into signed watts with a plain `* 120.0` scale + 0.55/0.45
  split (`CabinEnvironment.cs:183-185`) — given a nonzero `PowerFlow` it produces exactly the negative
  reading the `Pages.cs` comment names: the preview's own fixture (`PreviewMain.cs:183`,
  `pci.PowerFlow = -0.9`) renders NET PWR1 **−59 W** / NET PWR2 **−49 W**, bit-for-bit the comment's example.
  So the model was never the problem, and the `Pages.cs` comment was not stale — it correctly describes
  what the model does whenever it is fed a nonzero flow.
- **Root cause, in `VesselData.cs` (glue):** the flow derivative clocked itself off
  `Time.realtimeSinceStartup` — wall-clock time, which keeps advancing while KSP is paused.
  `ScreenPainter.OnPostRender` (which drives `VesselData.Refresh()`) keeps firing every paused frame too
  (the IVA cameras don't stop rendering on pause), so during any pause — exactly what a deliberate
  "screenshot every button" tour does — `ElectricCharge` was frozen (`amt` unchanged) while the wall-clock
  denominator kept growing, so `(amt - lastCharge) / (now - lastChargeAt)` evaluated to **exactly** 0.0
  every paused frame. Both dials read exactly 0 W together because they are the same underlying
  `PowerFlow` split 55/45 — consistent with a bit-exact-zero upstream value, not independent rounding.
- **Fix:** clock the derivative off `v.missionTime` (simulation time — frozen while paused, already read
  elsewhere in the same method) instead of `Time.realtimeSinceStartup`. While paused, `now` no longer
  advances past `lastChargeAt`, so the existing `now > lastChargeAt` guard now does what it always meant
  to: hold the last real reading instead of overwriting it with a pause artifact.
  `plugin/src/VesselData.cs:20-22,144-153` (glue only — `pure/CabinEnvironment.cs` untouched).
- Glue-only change; the preview path feeds `CabinInputs.PowerFlow` from a hardcoded fixture and never
  exercises this real-time derivative, so `ui_vehicle.png` is unaffected by design — re-rendered anyway to
  confirm: NET PWR1/2 still **−59 W / −49 W**, unchanged.
- `python plugin/build.py test`: **green, ALL SCREEN SUITES PASSED**. No headless test added — the bug and
  fix live entirely in KSP-facing glue (`Vessel.missionTime`, `OnPostRender` pause behaviour), which the
  headless harness has no vessel to exercise; verified by code inspection + the preview fixture cross-check
  above instead.
- **DONE when** met: the dials show a modelled value once wired correctly (fixed), not a stale-comment case.

### S7 [S] `index_assets.py` does not recurse into `art/cover/` — **DONE**
`plugin/build/index_assets.py` globs the shipped-art directory with a non-recursive `'*'`, so `ASSET_INDEX.md`
lists 6 shipped files while 98 exist — the 95 Cover PNGs in `GameData/DragonScreen/art/cover/` are invisible to
the "grep the index before concluding an asset does not exist" rule, which is exactly the failure that file was
written to prevent. **DONE when:** the generator recurses and the regenerated index lists the cover set.
- **DONE 2026-09-02 (hygiene sweep, owner-directed).** The `SECTIONS` table gained a per-entry `recursive`
  flag; only the SHIPPED entry sets it `True` (the other six sections have no subdirectories to miss, so
  their behaviour is unchanged). When set, the glob becomes `glob.glob(os.path.join(d, '**', pat),
  recursive=True)`, which also matches files directly in `d` (Python's `**` matches zero directories too).
  Regenerated `docs/ASSET_INDEX.md`: SHIPPED now lists **134** files (6 top-level + 127 in `cover/` + 1
  directory-count quirk resolved — every `cover/*.png` now has its own `cover/<name>.png` line with size),
  up from 6. No behaviour/visual change (a docs-generator + regenerated doc) → preview/PNG gate N/A (C1.3).
  `python plugin/build.py test`: green, all suites, 0 failed. Committed locally (C1.5); not pushed.

### S8 [S] `plugin/build/assess_flight.py` is autopilot-era tooling — **DONE**
- **Owner decision (via the overseer, 2026-09-02):** KEEP the file — retained for Part B's §B5 empirical tune
  (T22), which will regenerate a flight corpus; the old corpus is gone, so it will not run until then.
- **DONE 2026-09-02.** Added a header block stating the retention rationale (kept, not deleted; for T22/§B5;
  won't run until T22 produces new data) above the existing OLD-SCHEMA note, which is unchanged. Comment-only,
  no code change → preview/PNG gate N/A (C1.3). `python plugin/build.py test` run as a no-regression check:
  **green, ALL SCREEN SUITES PASSED** (14 suites, 0 failed). Committed locally (C1.5); not pushed.

### S9 [S] Mirror the reconcile into the Dragon Screen Map artifact — **BLOCKED** (T1's artifact half) — [TIER 5: held / owner-action / Part-B-bound]
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

### S10 [O] Scaled-space RT planet camera (`MAP_MFD_RESEARCH.md` §2) — **SPLIT** into S10a (DONE) + S10b (HELD) — this line is closed, do not take it
**SPLIT 2026-09-02, on the owner's directive to BUILD it** (via the overseer). The split is T11a/T11b's, for
T11a/T11b's reason: the part that can be judged with the game closed was built under the standing preview-only
go, and the part that can only be judged in the capsule waits for a gate of its own. **S10a** is the pure
geometry, the seam and the honest no-signal state — **DONE**, below. **S10b** is the Unity camera itself plus
the three in-sim judgements — **HELD**, below, and it needs a separate owner `install` + glass go (C1.12).
The original line follows.

Logged by T4 (C1.1), not done. `docs/MAP_MFD_RESEARCH.md` §2 designs a dedicated Unity camera copying
`ScaledCamera.Instance.cam` into a RenderTexture (`src/ScaledPlanetRenderer.cs` + `pure/PlanetGeom.cs` +
`ImageId.ScaledPlanetLive`), which would replace `NavPage.Globe`'s textured-strip disc with a real
rendered globe and cull the orbit line behind true geometry. It is **not** what T4's DONE-when asks for
and cannot be judged by the preview gate at all — there is no Unity camera with the game closed, so the
preview can only draw "LIVE 3D — NO SIGNAL". It therefore needs `install` + glass time, which the
preview-only build-go does NOT cover — a separate, explicit owner go first. T4 shipped the Cover's 2D/3D + camera MODES against the pure globe that already exists; the
disc underneath is the only part §2 would change. `MAP_MFD_RESEARCH.md` §5 still says this work "is T4" —
that line needs re-pointing at S10 when this is picked up (or by a docs pass).
**⛔ REMOVED FROM S17's BATCH, 2026-09-02** (owner directive, in the session that opened the T10 + T11b
gate). S17 had batched this as a third capsule-only item, which was a category error: S17 VERIFIES things
that are built, and this is **not built**. So the order is fixed — **build S10 first (preview-only, under
the standing build-go, as far as it can go), and only then does its glass check need a go of its own.**
That go is the OWNER's (C1.12) and the T10 + T11b gate did not cover it. **DONE when:** the RT
camera renders in-sim, the orbit line tracks and occludes, and the framing reads well on the glass.
*(That DONE-when is now **S10b's**; S10a's is on its own line.)*

### S10a [O] Scaled-space RT planet camera — the half the preview gate can judge — **DONE 2026-09-02**
Owner-directed BUILD (via the overseer, 2026-09-02), taken as THE task of its session rather than the `/next`
default. Everything in `MAP_MFD_RESEARCH.md` §2 that is decidable with the game closed, under the standing
preview-only go. **The line was drawn at "can it be EXERCISED", not "can it be compiled":** `build.py`
compiles all of `src/` — glue included — on every `test`, so a Unity camera file would have compiled here;
but nothing in `test` or `preview` can RUN a `Camera`, so the renderer would have shipped unexercised. It
therefore went to S10b, and everything that can be run and seen was built here.
- **`plugin/src/pure/PlanetGeom.cs` (new).** The scaled-space camera arithmetic: a `ScaledVec` (pure code
  cannot reference `UnityEngine.Vector3`), `Frame()` — the default 3/4 orbital-chase placement, built on the
  ORBIT's basis so `CTR` returns to it exactly — `Distance()`/`Fill()`/`ApparentFill()`, a `Project()` twin of
  `Camera.WorldToViewportPoint`, `ViewportToPanel()`, and `Occluded()`, the true-geometry ray/sphere twin of
  `GlobeProjection`'s orthographic test.
- **⭐ ONE NUMBER TIES THE TWO GLOBES TOGETHER.** The camera distance is not a tuned constant: it is SOLVED
  from `PlanetGeom.DiscFillOfHalfHeight` (0.88) — the fraction of the well's half-height `NavPage.Globe`'s
  textured disc already fills — and the camera's own vertical FOV. So the rendered limb lands exactly on the
  disc's limb, the view cannot jump when a camera appears behind it, and the orbit overlay is projected
  against one radius either way. `NavPage`'s old free-standing `0.44f` now derives from that constant, and
  both globes zoom by the same 1.25. A test round-trips the solve and lands the projected limb on it — at the
  TANGENT point, not the equator point, which is a real 5%-of-a-radius difference under perspective.
- **The seam.** `ImageId.ScaledPlanetLive` (+ `Images.IsRuntime`), `PageState.PlanetCamLive`, and
  `ImageStore.ScaledPlanetTexture()` — which returns null, honestly, with the comment saying S10b replaces its
  body with `return ScaledPlanetRenderer.Texture();`. `VesselData` reads it into `PlanetCamLive` (and clears
  it on the no-vessel and exception paths), so the PAGE never asks about textures and the GLUE never decides
  what a page says. The preview's `LoadStandIn` returns null for it too — the shared stand-in IS an
  equirectangular Earth, so falling through would have let a PNG claim a render that does not exist.
- **The honest state (§14.4(e)).** `NavPage.Planet` gained a `live` flag. NAV's 3D PLANET view passes true and
  draws the RT when one exists; with none it keeps the textured disc and the projected orbit — both real — and
  prints `LIVE 3D — NO SIGNAL` + a second line naming S10b in the well's top-left, Caution amber, not Alarm
  red (nothing has failed). The marking is drawn OUTSIDE the body so no early return can skip it. The
  sub-heading, which said `LIVE CAMERA` unconditionally and could not back the claim, now says `GLOBE + ORBIT`
  until a camera is actually rendering. Same marking mechanism as T11a's placeholder sequence, label naming
  the task that clears it, asserted by a test.
- **⚠ NOT CHANGED, deliberately:** the Cover globe and the Manual Chute globe call the same `NavPage.Planet`
  and pass `live: false` — they are finished pages with small decorative body slots, not camera views, and a
  feed plus a no-signal notice in them would have been a regression for nothing. Asserted by test, and
  confirmed against both previews.
- **Gate (C1.3):** `python plugin/build.py test` **green**, all suites, 0 failed — 66 new `PlanetGeomTest`
  checks + a new `PageTest.PlanetLiveSeam` (both states of the seam, the two untouched globes, and the
  well-is-wider-than-tall assumption the shared fill constant rests on, at both screen heights).
  `python plugin/build.py preview` re-rendered; `page2_nav_planet.png` inspected — the marking reads, the
  globe and orbit are unchanged beneath it; `cover.png` and `ui_manualchute.png` unchanged. No `install`, no
  glass (C1.12). Committed locally (C1.5); not pushed.
- **Docs:** `MAP_MFD_RESEARCH.md` §5's stale "this work is T4" re-pointed at S10a/S10b, and the §BUILD STATUS
  block's "`PlanetGeom.cs` does not exist and never has" corrected to say which half now does.

### S10b [owner-gated] Scaled-space RT planet camera — the Unity camera + the in-sim verify — **HELD** (`/next` SKIPS it) — [TIER 5: held / owner-action / Part-B-bound]
The half S10a could not touch. **⛔ NEEDS A SEPARATE, EXPLICIT OWNER `install` + GLASS GO** — the standing go
is preview-only, and this line neither grants nor inherits one (C1.12).
**What it builds:** `src/ScaledPlanetRenderer.cs` — the camera + RenderTexture on the `DockingCamRenderer`
pattern (`cam.CopyFrom(ScaledCamera.Instance.cam)` to inherit the exact culling mask/clip/projection the map
draws planets with, then override target/enabled/transform; re-aimed in `OnPreCull` so it is never a frame
stale; `Idle()` off when unwatched; validate-not-remember across scene loads). It is aimed by
`PlanetGeom.Frame`, which is built and tested. **Hook-up is one line:** `ImageStore.ScaledPlanetTexture()`
returns the texture instead of null, and `PlanetCamLive`, the page, the sub-heading and the marking all follow
on their own. Then §2.2's overlay re-projection through the camera's own `WorldToViewportPoint` +
`PlanetGeom.Occluded` — note S10a's overlay is still the ORTHOGRAPHIC `GlobeProjection` one, which is right
for the disc and approximate over a perspective render at ~2.2 body radii.
**What it verifies (the three a PNG cannot answer):** does the globe render in-sim; does the orbit line track
and disappear behind TRUE geometry; does the framing/zoom read well on the glass — including whether
`PlanetGeom.DefaultAzimuthDeg`/`DefaultPitchDeg` (-55 / +30, CHOSEN not measured, and marked as such in the
source) are the right 3/4 view. **Batched onto `S18`'s glass checklist as G11, tagged S10.**
**DONE when:** the RT camera renders in-sim, the orbit line tracks and occludes, and the framing reads well
on the glass.
- **CODE WRITTEN AND COMMITTED 2026-09-03 (owner directive, that chat) — the line STAYS HELD.** The renderer
  had been left uncommitted in the working tree; the owner asked for it to be committed, so it now is
  (`src/ScaledPlanetRenderer.cs` + the `ImageStore` hook-up + the painter's claim/idle + the `PlanetGeom`
  marking reword + its test). **This does NOT make S10b DONE and does not open any gate.** Its three
  done-criteria are exactly the three things a PNG cannot answer, so they are all still open, and the
  standing state remains preview-only (C1.12). What IS verified: `python plugin/build.py test` green
  (11476 checks, 0 failed) and `page2_nav_planet.png` inspected — the view still draws the marked
  `LIVE 3D — NO SIGNAL` state over the real orthographic globe + orbit, which is correct, because the PNG
  preview never links the glue and so can never have a Unity camera behind it.
- ✅ **THE GLASS SESSION DID HAPPEN — the register had simply missed it.** The committed code carried two
  comments asserting an owner install + glass go (`ImageStore.cs`: *"S18's install + glass go built it"*;
  `ScaledPlanetRenderer.cs`: *"the camera waited for install + glass time, which is S18's gate. This is
  that camera."*). Nothing in the register recorded such a go, so the committing chat rewrote both to claim
  only what it could verify, and flagged it. **The owner then confirmed it, and the KSP screenshots prove
  it** — 38 frames, 12:02:48–12:06:13 on 2026-09-03, showing a build current through S26. So the original
  claim was substantially TRUE and the rewrite was over-cautious; both comments have been corrected again
  to say what actually happened, and the gate opening is recorded here where it belongs. **The correction
  was still the right call at the time** — C1.12 forbids a build chat recording a go as the owner's on the
  strength of a code comment, and the fix for that is exactly what happened: state the verifiable version,
  flag it, let the owner settle it. ⛔ **THE CAMERA WAS NOT EXERCISED — ALL 38 FRAMES CHECKED (2026-09-03).** The owner asked for the
  rest of the screenshots to be swept, so all 38 were, by contact sheet. **Not one shows the NAV page**, in
  any of its three views. The session covered the VEHICLE family end to end (Overview, Crew, Prop, Mech,
  Power, Avionics, GNC, Thermal, Systems Tree ×3, Systems P&ID), the Cover's deorbit rail through every
  phase and all three camera views, DOCKING and MANUAL DOCKING, all three Settings tabs, and the Suit Leak
  Check — but the NAV page was never opened, so `ScaledPlanetRenderer` was never claimed and never rendered
  a frame. **S10b's three in-sim criteria are therefore untouched and this line stays HELD** — the gate was
  opened and spent on other pages, which is a perfectly reasonable use of a restart, but it is not S10b.
  ⚠ And when it IS opened, note the state: the vessel was `Landed` for the whole session
  ("ON SURFACE - NO ORBIT"), which is the DEGENERATE case for `PlanetGeom`'s orbit-plane framing — the
  normal falls back to the body's north axis. Answering "does the orbit line track and occlude" needs the
  vehicle **in orbit**, so whoever plans that visit should put it there first or the check cannot be made.
- **Still not built, unchanged:** §2.2's overlay RE-PROJECTION. The orbit line over this view is still
  S10a's ORTHOGRAPHIC `GlobeProjection`, which is right over the textured disc and NOT right over a
  perspective render from a 3/4 angle. The committed file's own header says so. Carried on **S37**.
- ⚠ **NEEDS-WORK ADDED 2026-09-03 by the flight-surfaced screen-bugs pass — the RSS COMPAT GAP (now S42).**
  The owner's 2026-09-03 in-orbit flight produced no DragonScreen exception at all, but it did produce one
  standing warning ~450 times, once per frame:
  `[DragonScreen] no usable scaled-space map for Earth on shader 'Custom/HapkeScaled' - NAV draws the grid
  and track only. Texture slots: _MainTex=4x4, _BumpMap=null, ..., _Skybox=4096x4096, ...`
  Under RSS the planet wears a **custom Hapke scaled-space shader whose texture slots are not the stock
  ones**, so `ImageStore.BodyMap`'s `_ColorMap` / `_MainTex` / `mainTexture` lookup finds nothing usable
  (`_MainTex` is a 4×4 stub, correctly rejected by `MinMapPixels`) and the NAV **MAP** view — and the
  strip-textured globe under the **3D PLANET** view — fall back to grid + track with no planet on them.
  **This is a real compat gap and it is NOT S10b's three criteria**; it is logged as **S42** and its glass
  check as **G12**. What it does mean for THIS line: when G11 is finally run, G12 rides the same visit,
  because the two questions share one screen. Note the §2 prediction worth testing there — the camera
  renders whatever shader the body is wearing and never asks for a texture slot, so **G11 may well pass
  under RSS while the flat map still fails**. The flood itself is fixed (**S40**): that warning now says
  itself once per body+shader.

### S37 [O] The 3D PLANET overlay is still orthographic over a perspective render (§2.2) — **HELD** (rides S10b's gate) — [TIER 5: held / owner-action / Part-B-bound]
Logged by the S10b commit, 2026-09-03. `ScaledPlanetRenderer` renders the globe in PERSPECTIVE from a 3/4
chase angle, but the orbit line, the AP/PE markers and the vessel tick drawn over it still come from S10a's
ORTHOGRAPHIC `GlobeProjection` — the two look at the globe from different places, so once the feed is live
the overlay will not sit on the globe underneath it. `docs/MAP_MFD_RESEARCH.md` §2.2 is the plan: re-project
through the camera's own `WorldToViewportPoint` plus `PlanetGeom.Occluded` for the behind-the-limb half.
**Why it is HELD, not TODO:** the defect it fixes cannot be SEEN, and the fix cannot be judged, until the
camera actually renders — which is S10b's in-capsule gate. It is deliberately its own line rather than
smuggled into S10b (the committed file's header says exactly that), so whoever opens that gate can decide
whether to spend the same visit on both.

### S11 [S] `plugin/build/csc.rsp` is a generated file, tracked, and churns on every build — **DONE**
Logged by T4 (C1.1), not done. `build.py` overwrites `plugin/build/csc.rsp` on every invocation with
whichever compile ran last, so the file shows as modified after any `test` / `preview` / `install` and
its content depends only on which command was run most recently — it carries no information worth
versioning. It is also written with CRLF into a repo whose `.gitattributes` mandates LF, so git warns on
every touch. T4 restored it to HEAD (`git checkout --`) so the commit carries only real changes.
**DONE when:** it is gitignored (and untracked), or build.py writes it outside the repo.
- **DONE 2026-09-02 (hygiene sweep, owner-directed).** Added `plugin/build/csc.rsp` to `.gitignore`
  (under the existing "BUILD ARTEFACTS" section, beside `preview/`/`tmp/`/`refart/`) and `git rm --cached`
  it — the file stays on disk (build.py still writes it) but git no longer tracks it, so it can't churn
  the working tree again. Working tree was already clean at session start (no dirty csc.rsp to clear this
  time). No behaviour/visual change → preview/PNG gate N/A (C1.3). `python plugin/build.py test`: green,
  all suites, 0 failed. Committed locally (C1.5); not pushed.

### S12 [S] `VehicleMechPage`'s subsystem tab bar isn't severity-aware — **DONE**
Logged by T5 (C1.1), not done. T5 gave `VehicleTabBar` a `Severities(PageState)`-driven `Draw` overload
so a faulted subsystem's tab reads red from every vehicle page (`VehicleOverviewPage.cs`,
`VehicleSubsystemPage.cs`) — real signals per §14.4/§1.4: `Alarms.LifeSupport`/`Thermal`/`Low`/
`FdirSeverity`. `VehicleMechPage.cs` was out of T5's declared scope (register line names only
`VehicleOverview`/`SubsystemPage`) and still calls the old 4-arg `VehicleTabBar.Draw(dl,w,h,active)`, so
its own tab bar always reads nominal even when another subsystem is genuinely alerting. **DONE when:**
`VehicleMechPage.Build` passes `PageState` through and calls
`VehicleTabBar.Draw(dl,w,h,3,VehicleTabBar.Severities(s))`, with a preview showing it turn red to match.
- **DONE 2026-09-02 (owner-directed).** `VehicleMechPage.Build` already took `PageState s` as a
  parameter (used throughout for the node readings), so the only change needed was the tab-bar call
  itself: `VehicleTabBar.Draw(dl, w, h, 3)` → `VehicleTabBar.Draw(dl, w, h, 3, VehicleTabBar.Severities(s))`
  — the exact overload T5 built for `VehicleOverviewPage`/`VehicleSubsystemPage`, no new signals invented.
  Added an `ui_vehiclemech_alarm.png` render to `PreviewMain.cs` alongside the existing
  `ui_vehiclepower_alarm`/`ui_vehicle_alarm` block (same forced-into-alarm-band `Power01`), so the Mech
  page's sub-nav is proven to turn red the same way Overview's does, not just compile. Previews inspected:
  `ui_vehiclemech.png` (baseline — All/Power amber per the fixture's own Caution-band Power01, Mech tab
  itself white/active, matches pre-change look) and `ui_vehiclemech_alarm.png` (All/Power now **red**,
  Mech tab still white/active since nothing alerts on Mech itself — matches `ui_vehicle_alarm.png`'s
  behaviour). S22's "ALL SYSTEMS CHECK" dash-and-dim (`Dash`/`Dim` on `s.Valid` false) confirmed intact
  and unchanged in both renders. `python plugin/build.py test`: green, all suites, 0 failed.

### S13 [owner call] "altitude" vs "attitude" in Crew Interrupt Conditions / Slew for Deorbit Burn — **DONE**
Logged by T7 (C1.1). Every source that names the deorbit-burn interrupt/slew criteria says "altitude" —
SCREEN_INVENTORY.md's photo transcription, SCREENS_LOOK_AND_FUNCTION_RESEARCH.md's read of the community
recreation's First.vue source, and the community Figma's own baked layer name (`600deg_m_altitude_rate`,
`CoverPage.cs` Keys). "30° sustained ALTITUDE error" and "600°/min ALTITUDE rate" don't parse physically
(altitude is a distance, not a rotational quantity), whereas "ATTITUDE error/rate" would — paired with
Roll/Pitch/Yaw and an autopilot SLEW-interrupt trigger, "attitude" is what the criteria are almost
certainly measuring.
- **DONE 2026-09-02 (owner-directed session, decided by the owner via the overseer — recorded as such per
  C1.12).** **Decision: ATTITUDE.** The deorbit interrupt/slew criteria are rotational (degrees,
  degrees/min) and pair with Roll/Pitch/Yaw + a SLEW interrupt, so the real quantity is attitude, not
  altitude; the only tier-1 source is a blurry photo whose transcription is the likely error, and the
  tier-2 community sources share one lineage (not independent), so §1.4 "verified-real" is weak and
  physics is decisive (C1.4/C7.1). Applied to the human-facing label + comments only — the baked Figma
  asset key (`600deg_m_altitude_rate`, real community-asset filename) is **NOT** renamed. Updated
  together so nothing in the tree disagrees: `DeorbitBurnPrepPage.cs` (on-screen strings "30° sustained
  attitude error" / "600°/min attitude rate" / "MAXIMUM ATTITUDE RATE", plus a full 2026-09-02 divergence
  note replacing the old "flagged for owner" note), `CoverPage.cs` (a comment beside the two baked Keys
  entries recording the divergence — key strings themselves untouched), `docs/SCREEN_INVENTORY.md` (a
  divergence-note paragraph + both inline "altitude rate" mentions for screen #24), and
  `docs/SCREENS_LOOK_AND_FUNCTION_RESEARCH.md` (a bullet in the top RECONCILED note + the §2 item 1 inline
  mention). Preview `ui_deorbitburnprep.png` inspected — shows "attitude" throughout; `ui_cover.png`
  inspected — the baked Cover asset still reads "altitude" verbatim, as intended (literal community
  asset, not relabelled). `python plugin/build.py test` green (all suites passed).

### S14 [S] Three dead `UiPage` entries surface confusing Menu cards — **DONE**
Logged by T7 (C1.1), not done. `UiPage.PhaseDeport`/`PhaseCoast`/`PhaseClaw` (values 6/7/8) predate the
Cover rail's redraw as a real 7-item IN-PAGE selector (`CoverPage.MapCover` only routes `PhaseManual`
away; the rest select in-page, `NavHit.None`) — `FigmaUI.Build`'s switch has no case for them, so they
fall to the honest `PlaceholderPage`, but they still surface as real-looking Menu cards ("DEORBIT BURN",
"COAST TO TRUNK JETTISON", "CLAW SEPARATION") since `MenuPage` auto-discovers every `UiPage`. "DEORBIT
BURN" (index 6) now sits one row away from T7's genuine "DEORBIT BURN PREP" card, which reads as two
different pages about the same thing to anyone browsing the Menu. Owner call: delete the three (their
explicit int values 6/7/8 would just go unused — nothing else is numbered relative to them) once nothing
persists a screen against them, or keep them and give Menu a way to hide placeholder-only pages.
**DONE when:** decided + implemented.
- **DONE 2026-09-02 (owner-directed stray-finding pickup, decided by the owner via the overseer — recorded
  as such per C1.12; not self-authorized).** **Decision: KEEP, don't delete/renumber.** `UiPage` is a
  per-screen persisted int (`ScreenPainter.selectedPage` ↔ `DragonScreenState.GetPage`/`SetPage`) and the
  enum's own comment already forbids renumbering — deleting/renumbering `PhaseDeport`/`PhaseCoast`/`PhaseClaw`
  was ruled out on that basis, not attempted. Instead, added one shared predicate,
  `FigmaUI.IsPlaceholder(UiPage)` (`FigmaUI.cs`, next to `Build`'s switch) — true for any page with no real
  case in `Build`'s switch (so a visit draws the honest `PlaceholderPage` card). `MenuPage.BuildEntries()`
  now skips both `Menu` itself and any `IsPlaceholder` page, so the grid lists only real pages — no
  hardcoded names (per the trap warning: the two enums `UiPage.PhaseDeport/Coast/Claw` and
  `CoverPage.CoverButton.PhaseDeport/Coast/Claw` share names but are unrelated; only `UiPage`/`MenuPage`/
  `FigmaUI` were touched, `CoverPage`'s live phase rail is untouched).
  **Correction to this finding's own count:** the general predicate (not a 3-item hardcode) turned out to
  hide **nine** cards, not three — `PhaseDeport`(6)/`PhaseCoast`(7)/`PhaseClaw`(8) as logged here, plus
  `PhaseManual`(9)/`ActOnSpaceX`(10)/`ActDeorbitBrief`(11)/`ActReview`(12)/`ActAcknowledge`(13)/`Entry`(14) —
  grepped and confirmed none of those six have a real `Build` case or any live `NavHit.Go` target anywhere
  in the tree either (they are leftover ints from the old phase-rail numbering, same footing as the three
  named here). Menu now shows the real 20 pages (`ui_menu.png`: 124 commands vs. the prior 29-card grid);
  the grid (3×10, unchanged — T2's sizing) has empty trailing rows now, expected to refill as later tasks
  (T8+) add real `Build` cases, per this task's own "self-populates" framing.
  **Drift safety:** rather than trust the hand-written predicate blindly, `FigmaUINavTest.MenuHidesPlaceholders()`
  actually calls `FigmaUI.Build` for all 30 pages and checks the emitted `DisplayList` for `PlaceholderPage`'s
  own marker text ("PAGE NOT YET BUILT"), asserting it agrees with `IsPlaceholder` — and separately asserts
  `MenuPage.Entries` contains exactly the non-Menu, non-placeholder set. `Menu()`'s pre-existing entry-count
  check was updated (`FigmaUI.PageCount - 1` → computed via the same predicate) since it no longer holds.
  **Gate:** `python plugin/build.py test` green, Figma UI nav suite 177 → **228** checks, 3927 → **3978**
  total, 0 failed. `ui_menu.png` re-rendered and inspected: 20 cards (Cover, Attitude HUD, Audio Settings,
  Procedure, Cabin, Vehicle Overview, Suit Leak Check, Mech Panel, Video Settings, Test Vrio Health LEDs,
  Vehicle—Crew/Prop/Power/Avionics/GNC/Thermal, Manual Chute Deploy, Manual Docking, Rendezvous, Deorbit
  Burn Prep), all legible, no overlap/clipping, bottom bar intact; the 9 placeholder titles absent. Declared
  outputs only: `MenuPage.cs`, `FigmaUI.cs`, `plugin/test/FigmaUINavTest.cs`, `REGISTER.md` — no
  `PanelMap.cs`/label-doc edits, no memory writes.

### S15 [O] The circular nav / orbit plot (SCREEN_INVENTORY #28) is still unbuilt and unowned — **DONE 2026-09-02**
Logged by T9 (C1.1), not done. `SCREEN_INVENTORY.md` #28 marks the circular nav/orbit plot as
"🟠 REF, not built (**T6**/**T9**)", but neither task's register line covers it: T6's line is the
rendezvous *ellipse* plot (built, `RendezvousPage.cs`) and T9's is "Prop thruster schematic + P&ID /
systems-tree deep-views". So the third of §11b's three newly-characterised screens has no owner. The
reference is good — JSC `jsc2024e064449`'s RIGHT screen plus the BBC frame: concentric rings, coloured
target markers (yellow + cyan), orbit arcs and a g/rate readout; §3 says it "pairs with the Rendezvous
ellipse". Most of what it needs already exists (`NavPage.Orbit`'s real conic, `PageState`'s target/range
fields), so it is likely a small [S] page rather than an [O] one. **DONE when:** the owner decides
whether it becomes its own register task (and where in the §7 order), and it is built + previewed —
or it is explicitly deferred and #28's "(T6/T9)" mark corrected.
- **Owner directive (via the overseer), 2026-09-02:** BUILD it now, as its own task, run on **[S]** (the
  register's own guess above — most of what it needs already exists). Reference confirmed: JSC
  `jsc2024e064449`'s RIGHT screen + the BBC frame, same footing as T6/T9 (layout-real /
  labels-reconstructed + MARKED).
- **DONE 2026-09-02.** New `plugin/src/pure/NavOrbitPlotPage.cs`, `UiPage.NavOrbitPlot` (34), and
  `FigmaUI.PageCount` 34→**35**. **Not a second orbit renderer** (§1.4, the same rule T6 followed for
  its ellipse): it calls `NavPage.Orbit(dl, s, ..., true)` — the SAME real conic (apogee/perigee →
  ellipse, current radius → true anomaly, target phase → approach chord) T6's rendezvous plot already
  trusts, so there is still one orbit calculation in the codebase, not a third. The AP/PE markers, the
  vehicle cross and the target-chord diamond it draws are its own real markers, untouched.
  **The g/rate readout is real, not a new field:** `PageState.GForceText`/`.GForce01` (the same field
  the Vehicle Overview's G-FORCE dial reads) and `.RateText`/`.RangeText`/`.TargetName` (the same
  docking-approach fields the Rendezvous chord and the Docking page already read) — no new `PageState`
  field was added for this task, exactly the reuse the task called for.
  **Ours, stated in the code (§1.4):** the concentric range rings (ring count/spacing — no scale is
  legible in either source, so none is printed) and the small "VEHICLE" (cyan) / target-name (yellow)
  colour-key chips — the JSC frame shows two coloured markers but not their exact glyphs, so the key
  names the colour convention (matching the reference's yellow+cyan) rather than inventing an unreadable
  icon shape, the same "shape confirmed, artwork not invented" call T6 made for its mission-patch
  roundel.
  **Reachability:** Menu grid only (auto-discovered via `FigmaUI.IsPlaceholder`, no MenuPage resize
  needed — 25 real cards, well inside the existing 3×10 grid) + the bottom bar (present + correctly
  routing on every page automatically) — the same footing T12's Ascent established, not T6's
  letterbox-margin pairing (the task's own reachability line named Menu + bottom bar, not a new
  Rendezvous↔plot link, so none was added — no scope creep).
  **Nav test:** new `FigmaUINavTest.NavOrbitPlot()` — bottom bar → Cover, Menu lists the new page, the
  body is inert (no invented destinations), and it is a real page, not a placeholder. `python
  plugin/build.py test`: **green, Figma UI nav suite 0 failed, all 14 suites 0 failed** (no new
  warnings; the pre-existing CS0162/CS0219 warnings in `ScreenPainter.cs`/`Pages.cs` are untouched by
  this task).
  **Preview:** `ui_navorbitplot.png` inspected — concentric rings, the live globe + dotted ellipse +
  AP/PE + vehicle cross, the amber approach chord to "SPACE X STATION" with its diamond endpoint, the
  cyan/amber colour key, and the G-FORCE/RATE/RANGE readout all render cleanly in the plot well, no
  overlap/clipping, no `DisplayList` overflow (179 of 340 commands). Added
  `ui_navorbitplot_notarget.png` (mirroring the existing Docking/Rendezvous no-target renders, from the
  SAME target-off fixture toggle) — the chord and diamond correctly vanish, the key reads "NO TARGET",
  RATE/RANGE correctly dash, G-FORCE (not target-gated) still reads; `ui_menu.png` re-inspected — 25
  cards including the new "NAV / ORBIT PLOT" as the last card, still legible, no overlap.
  **Docs:** `SCREEN_INVENTORY.md` #28 flipped 🟠 REF, not built → **✅ BUILT**, naming the real source
  file/`UiPage` and what's real vs. ours; the "RESEARCH PASS 2026-09-02" JSC-vein summary paragraph
  updated in place (it read "#28 is still … not built — owned by S15" — now says built, all three
  JSC-sourced looks done). `docs/BUILD_PLAN.md` left **frozen** per the task's own instruction (its
  stale §3 REF/REFINE marks are the known, already-logged **S16**-adjacent gap — status lives in the
  living inventory, not the spec, C7.1). §1.4 respected throughout; no `PanelMap.cs` / label-doc edits.

### S16 [S] `SCREEN_INVENTORY.md` + §3 status marks are stale after T9 — **DONE 2026-09-02** (owner-directed, scoped to `SCREEN_INVENTORY.md` only — C1.1)
Logged by T9 (C1.1), not done — a docs pass, and T9's declared outputs are code + preview only (C1.11),
so the marks were not edited here. Now inaccurate: **#26 Prop/RCS thruster schematic** still reads
"🟡 REFINE — we built Prop as a generic gauge template" (it is now the schematic); **#27 Systems /
electrical TREE** and the **"Vehicle systems P&ID schematic"** entry still read "🟠 REF, not built"
(both are built pages, `UiPage.SystemsTree` / `UiPage.SystemsPid`); `BUILD_PLAN.md` §3's rows
"Vehicle · Prop — real look" (REFINE) and "Vehicle systems deep-views" (REF, not built) say the same;
§7's item 5 ("Vehicle systems P&ID schematic (needs a cleaner frame)") is done to the extent a cleaner
frame allows. §11b itself is research and stays as written. Fold into the next docs pass together with
**S9** (the map artifact), which needs the same tally update. **DONE when:** no `docs/` status mark
contradicts the tree.
- **DONE 2026-09-02, owner-directed this session — explicitly narrowed to `SCREEN_INVENTORY.md`.**
  `BUILD_PLAN.md` §3 is deliberately **left frozen** on the owner's instruction (unchanged since G1;
  status now lives in this living inventory, not the spec, per C7.1) — its stale REFINE/REF marks are
  **known and intentionally not touched** here, not missed.
- **`docs/SCREEN_INVENTORY.md` edits:** table rows **#26** and **#27** flipped to ✅ BUILT (T9), each
  naming its real source file (`PropSchematic.cs`; `SystemsTreePage.cs`/`UiPage.SystemsTree`) and what
  makes it live, not painted. Row **#28** stays 🟠 REF, not built, but its stale `(T6/T9)` owner tag is
  corrected to **`S15`** — T6 built the rendezvous *ellipse*, T9 built the other two JSC screens, neither
  register line actually covers #28 (this also satisfies the mark-correction half of S15's own
  DONE-when; S15 itself stays TODO — the build-or-defer call hasn't been made). The prose
  "Vehicle systems P&ID schematic" research bullet gets the same ✅ BUILT 2026-09-02 (T9,
  `SystemsPidPage.cs`) tag, plus one line on why it landed as ECLSS + coolant loops rather than Prop
  (Prop's plumbing would have duplicated #26). The "RESEARCH PASS 2026-09-02" summary paragraph is
  updated in place so it no longer reads as if all three JSC screens are still un-built.
- **Not touched, on purpose (C1.1, no scope creep):** the file's older "~18 built" headline count/page
  list (stale since well before T9 — T2/T3/T6/T7/T8/T9 all post-date it) and `docs/BUILD_PLAN.md` §3.
  Both are already the stated job of **S9** (BLOCKED on an owner publish action) once it runs, so no new
  stray line was opened for them.
- Docs-only, no code change → the preview/PNG gate does not apply (C1.3); `python plugin/build.py test`
  run as a no-regression check: **green, 12 suites, all passed, 0 failed** (unchanged from before this
  edit — a docs-only diff, as expected). §1.4 respected: every status flip cites the real file/type it
  points at; no `PanelMap.cs` / label-doc edits.

### S17 [owner-gated] Glass verification — T10 click + lamps, T11b drag feel — **DONE 2026-09-02** (all six answered on glass: 4 confirmed, 2 wrong and fixed; the two re-checks carried to **S18**)
Logged by T10 (C1.1/C1.7), 2026-09-02. This line is the ONE capsule session that settles the handful of
criteria a PNG cannot reach. It covers exactly **two** tasks — **T10** (the click and the lamps) and
**T11b** (the drag feel). Both are BUILT, SHIPPED and headless-green; what is left in each is a judgement
that needs eyes, ears and a hand on the glass.

**⛔ S10 IS NOT BATCHED HERE ANY MORE (unbatched 2026-09-02, by owner directive in the session that opened
the gate).** S17 batched it because it is another capsule-only item, but that was a category error: S10 is
**not built** — it is a TODO that would ADD `src/ScaledPlanetRenderer.cs` + `pure/PlanetGeom.cs` +
`ImageId.ScaledPlanetLive` — and there is nothing to verify until it exists. **S10 stays HELD on its own
line, gated on S10 being BUILT first**, and it needs its own owner go for `install` + glass when it is; the
gate opened for this session explicitly did **not** cover it and was not widened (C1.12).

**Gate (C1.12) — recorded as the owner stated it:** the owner (Chris) opened the `install` + glass-time
gate for **THIS SESSION ONLY, scoped to T10 + T11b**, explicitly excluding S10 and anything else. A build
chat neither granted nor widened it. The standing state remains preview-only; this go does not survive the
session.

**Install performed 2026-09-02 under that gate.** KSP and CKAN confirmed **closed** first (no matching
processes). `python plugin/build.py install` ran clean — tests green, **10630 checks, every suite 0
failed** — and wrote the DLL, the cfg, the art and `sounds/panel_click.wav` to
`…/Kerbal Space Program/GameData/DragonScreen`. A second run reported **every file unchanged**, which is
the check that the install is complete and self-consistent rather than half-written. **KSP needs a FULL
RESTART** to pick up the DLL. `plugin/build/csc.rsp` churn reverted (S11).

**THE SIX CHECKS — ALL SIX ANSWERED ON GLASS by the owner, 2026-09-02.** Four confirmed as built; two
were wrong and were each one constant, so both were fixed, rebuilt and reinstalled in the same session,
per the task's own rule that only a fix bigger than a constant becomes a follow-up line.

| # | Check | The owner's answer | Outcome |
|---|---|---|---|
| (a) | **T11b direction** — does dragging right read as grabbing the vehicle? | *"yes perfect"* | ✅ **RESOLVED, no change.** The sign was right. `render_turntable.py` derived it, the frames were measured, and the capsule agrees — all three now say the same thing. |
| (b) | **T11b gearing** — is one sweep = one revolution right? | *"too slow, does 3/4 of revolution dragging from left to right"* | 🔧 **FIXED — awaiting re-check.** See below. |
| (c) | **T11b reset threshold** — does a slow deliberate turn ever snap to the front? | *"stays; tap snaps to front"* | ✅ **RESOLVED, no change.** `TapSlopFrames` = 0.5 separates the two gestures correctly: a deliberate turn holds, a tap resets. |
| (d) | **T10 click level** — audible over cabin ambience? | *"too quiet"* | 🔧 **FIXED — awaiting re-check.** See below. |
| (e) | **T10 spatialisation** — does 2D read as flat? | *"sounds good, like it's coming from the button"* | ✅ **RESOLVED, no change — and this CLOSES the 3D branch.** S17 had said "if it reads as flat, move it to 3D with measured numbers". It does not read as flat, so the 3D version is **not built**: it would cost a source per button, a rolloff and a set of measurements to replace something that already reads correctly. `PanelAudio`'s header records that the fail-safe choice turned out to be the right-sounding one. |
| (f) | **T10 lamp brightness** — do the dashes light BRIGHT through the installed shader? | *"correct"* | ✅ **RESOLVED, no change.** The over-1 `LitColour` (2.2) does reach an HDR-capable colour property in the installed material — the case that would have made this a NEEDS-WORK (an over-1 clamped on `_Color`) did not happen. §14.4(a)'s bright/no-red language is now confirmed end to end. |

**(b) THE GEARING — the one real finding of the session, and it was a geometry mistake, not a maths one.**
T11a chose "one full sweep of the sprite's rect = one revolution", explicitly flagged as *chosen, not
measured*, and left it for the capsule. The capsule disagreed: a natural left-to-right drag turned the
vehicle about **three quarters** of the way round. The reason is that the gesture is measured against
`CoverPage.CapsuleRect` — the rect the sprite is drawn in, which is right, because it is what a crew member
is grabbing — but that rect is **1:2 in a much wider slot: only ~474 px across on a 2560 px panel**. A
comfortable sweep never traverses all of it, so "one whole rect" was a gesture nobody was going to make.
**Fix — one constant, as predicted:** a new `Turntable.UsableSweepFraction = 0.75f` records the measurement,
and `FramesPerSlot` is now `Count / UsableSweepFraction` = **48** (was 36). The measured sweep is now
exactly one revolution. The fraction is kept as a named constant rather than folded into 48 so the glass
measurement stays legible, and so the harness can ask for "a quarter turn" without arithmetic.

**(d) THE CLICK LEVEL.** `PanelAudio.Volume` **0.55 → 0.85**, still under 1 (a switch under your hands
should not be an event) and still a multiplier on `SHIP_VOLUME`, so a crew member who has turned the ship
down still gets a quieter panel. The **sample is unchanged** — the level is applied at `PlayOneShot`, not
baked in — so only the DLL moved.

**One test-design fix the new gearing exposed, declared.** `600 x 1px == 1 x 600px` compared two raw
`Turn` values. Under the new gearing 600 px is *exactly* one revolution, so the accumulated path lands a
hair under 36 while the single drag lands exactly on it — the same point on the circle, 36 apart as raw
numbers. The old gearing put this test comfortably mid-circle and hid that. The assertion always meant "the
same place", so it now says so, via a seam-aware `Apart()` helper — and it is now asking about the seam,
which is the interesting case. **Not a code bug: the shipped `Wrap` was correct throughout.**

**Verification of the fixes (C1.3).** `python plugin/build.py test` **green — every suite 0 failed**, the
turntable suite 5058 → **5061 checks** (the gearing assertions turned over from "a rect is a revolution" to
"a usable sweep is a revolution", with the resulting 48 pinned beside it so a silent change to either
number fails). **No new compiler warnings** (11 before, 11 after). Previews re-rendered: every frame lands
on **exactly** the index it did before — `ui_cover_turntable_0..3` on 0/9/18/27 closing the loop,
`ui_cover_turntable_drag_0..3` on 0/8/16/24, `ui_cover_turntable_reset` on the authored front — because
every harness drag distance is now expressed in the measured sweep. The renders are therefore identical to
the ones already inspected; what changed is the **travel**, which fell from 104 px to **78 px** for the same
rotation. That number is the gearing change made visible. **Reinstalled** with KSP and CKAN confirmed
closed; a second install run wrote **nothing**, which is the self-consistency check.

**DONE 2026-09-02 — the stated DONE-when is met: all six were heard, seen and felt in the capsule.** Four
came back confirmed and are **settled — they are not revisited**. Two came back wrong, were one constant
each exactly as this line predicted, and were fixed, tested, previewed and reinstalled inside the same
session. That is the whole of what a verification line can do; what is left is not verification but a
**re-check of two fixes**, which is a different job.

**⛔ THE GATE THIS LINE USED IS CLOSED.** It was opened by the owner for ONE session, scoped to T10 + T11b.
It did not survive the session, was never widened, and nothing here grants a new one (C1.12).

**→ The two re-checks are carried to `S18`** (end-of-Part-A glass pass) **by owner directive, 2026-09-02:**
two constants do not justify their own capsule visit when restarts are the scarce resource (C1.6), so they
wait and go in with whatever else has accumulated by the time the screens are built. The **installed build
already carries both fixes**, so nothing is lost by waiting.

### S18 [owner-gated] End-of-Part-A glass pass — **HELD** (deferred here by owner directive 2026-09-02; `/next` SKIPS it) — [TIER 5: held / owner-action / Part-B-bound]
The one capsule visit to make **once the screen build is complete** — Part A's remaining tasks are
**T12** (Ascent/Launch), **T13** (live-data wiring) and **T14** (touch wiring). Opened because S17 finished
its six checks and produced two fixes that want a second look, and **the owner's call was not to spend a
restart on two constants**: restarts are the scarce resource (C1.6), so these wait and go in as one pass
with whatever else has accumulated. Nothing is lost by waiting — **the installed build already carries both
fixes**, so when the gate opens this may need only a restart rather than a reinstall (it WILL need a
reinstall if the DLL has moved on by then, which T12–T14 will do).

**⛔ NEEDS A FRESH OWNER GO.** The 2026-09-02 gate was for one session, scoped to T10 + T11b, and closed
with it. This line does not grant, inherit or widen anything (C1.12) — `install` + glass time are the
owner's to open, at the time, for this pass.

**Carries — the two S17 fixes, each needing one look:**
1. **T11b (b) — the drag gearing.** `Turntable.FramesPerSlot` is now **48** (`Count / UsableSweepFraction`,
   the fraction being 0.75 as measured on glass), so a natural left-to-right sweep across the vehicle should
   now be **exactly one revolution** where it previously managed three quarters. **Check:** does it come
   round once? Over- or under-shooting, and roughly by how much? **If still off:** one constant
   (`UsableSweepFraction`, `pure/Turntable.cs`) — and the number it wants is readable straight off the
   answer, since the fraction IS the measurement.
2. **T10 (d) — the click level.** `PanelAudio.Volume` is now **0.85** (was 0.55, heard as too quiet).
   **Check:** right, still quiet, or too far? **If still quiet:** 1.0 is the LAST step available as a pure
   constant — beyond it the sample itself has to be regenerated hotter in `build/make_click.py`, which is
   more than a constant and becomes its own line rather than being done in the pass.

**⛔ SETTLED AT S17 — DO NOT RE-LITIGATE:** (a) the drag direction reads as grabbing the vehicle;
(c) a deliberate turn holds while a tap resets to the front; (e) the 2D click reads as coming from the
button, so **3D audio is not built**; (f) the dashes light BRIGHT through the installed shader. Four owner
answers, each recorded verbatim on S17.

**GLASS-CHECKLIST — the accumulated T13 / T14 wants.** *Seeded at the START of T14 by owner directive
(2026-09-02, via the overseer): the decision on WHEN to glass T13's numbers stays the default — this one
end-of-Part-A pass — but the pass's scope is written down EXPLICITLY here as it accrues, rather than being
reconstructed from task notes when the gate finally opens. Every item is tagged to the task that raised it.
T14 APPENDS to this list, at the moment and site each want arises. This seeding is a living-register action
(C1.7 / C5): it opens no gate, and the standing preview-only gate (C1.12) is untouched.*

| # | From | What to look at on the glass | Why it could not be settled in preview |
|---|------|------------------------------|-----------------------------------------|
| G1 | **T13c** | **The approach chord's endpoint under a real ISS target** (`RendezvousPage`) — with a target actually acquired, does the chord run from the vehicle marker to a plausible target position, and does the diamond sit where the ISS is? | The one T13 value DERIVED from geometry rather than read straight out of an existing field. The preview's target is synthetic, so it can only prove the chord is drawn, never that it points at the right place. T13c singled this out by name. |
| G2 | **T13a** | **Spot-check the VEHICLE-family readouts against a live vessel** — the overview gauges, the alerts/consumables block, the systems tree's live counts. | Preview proves the wiring reads `PageState`; only a live vessel proves the number it reads is the right one. T13a's "⚠ Not claimed" note. |
| G3 | **T13b** | **Spot-check the six subsystem sub-tabs + the Prop data band** against a live vessel — including the CommNet-fed S-BAND COMMS / Uplink / Downlink (S24) and the Power tab's live source/count rows (S23/S25). | Same reason as G2; CommNet in particular has no headless stand-in, so its three rows have never been seen with a real link. T13b's "⚠ Not claimed" note. |
| G4 | **T13c** | **Spot-check the procedure & prox-ops readouts** — Manual Chute's live top strip (SPLASHDOWN TIME, apsides), the docking ROLL/PITCH/YAW + PYR pair, RANGE / RATE — against a real approach. | Same reason as G2. Also the first chance to see the "no feed" / "no target" looks arise for real rather than from a forced preview state. T13c's "⚠ Not claimed" note. |
| G5 | **T14** | **Can a finger hit ONE chute action row?** The Manual Chute page now has 12 tappable action plates, 280×46 design-px, pitched 60 apart — a 14px dead band between neighbours. Press each of the four ENABLE BACKUP PYROS plates and confirm the one you aimed at is the one that lit. | The headless test proves the drawn rect and the hit rect are the same rect at three sizes (`TouchWiringTest`). It cannot prove that rect is big enough for a gloved finger at IVA distance — and the failure mode is firing the row BELOW the one aimed at, which on this page is a chute step. |
| G6 | **T14** | **One flag, two surfaces.** Tap ENABLE BACKUP PYROS on the Manual Chute glass, then look at the LOWER CONSOLE PLATE: its dash should be lit too, and vice-versa. | The whole design claim of the chute wiring is that the page and the plate cannot disagree because they read one flag. Preview renders the page; only the capsule has both surfaces in view at once. |
| G7 | **T14** | **Does a docking pad that does nothing read as deliberate, or as broken?** §14.4(a) makes the twelve direction pads an honest no-op, and a SCREEN touch has no click behind it the way a console press does — so pressing FWD produces literally no feedback. Watch someone press one and see whether they press it again. | This is the §14.4(a) "click + no light + no action" rule meeting a surface that cannot click. If it reads as a dead screen it is a NEEDS-WORK, and the fix is a decision (an inert-press affordance) rather than a constant. |
| G8 | **T14** | **The FUNCTIONS \| ALERTS toggle** — is the hit band usable? Its geometry is OURS (T5 said so explicitly, it is not measurable from the reference), 28px words with a ±20px design-space margin. Tap between the two words and confirm nothing flips. | Same reason as G5, and worse: the target is text rather than a plate, so there is no drawn edge telling the crew where to aim. |
| G9 | **T14** | **Legibility spot-check:** the docking clusters' **PRECISE** label, which is drawn at 22px against LARGE's 26px so the longer word fits its plate — does it read clearly at cabin distance? | This is a judgement about what a crew member reads at cabin distance, which is the one thing the PNG explicitly cannot settle (CLAUDE.md: "a screenshot is still the only way to judge how it LOOKS on the glass"). |
| G10 | **S31/S32** | **Suit Leak Check, three checks in one visit:** (1) does the **lit** TROUBLESHOOT (white, "act now") read distinctly from its dim resting state at IVA distance? (2) run the repair→rerun flow end-to-end on the collider — CLOSE the leak result box, press TROUBLESHOOT, watch the countdown restart from 5; (3) is the ΔP magnitude legible at cabin distance — a nominal **~0.28 psi** and a bled-down leaking suit's **~0.01 psi**? | Same reason as G9: a legibility/affordance judgement the PNG cannot settle. S31 ratified its constants as built but deferred the ΔP magnitude's glass eyeball to this pass; S32 added the lit-vs-dim state change and the repair-and-rerun flow, which preview can prove is WIRED but not that it reads right or feels discoverable on glass. |
| G11 | **S10b** | **The live 3D planet camera, three checks in one visit** — requires `src/ScaledPlanetRenderer.cs` to have been BUILT first (S10b), which needs this same go: (1) does the scaled-space RT actually render the globe on the NAV 3D PLANET view, and does `LIVE 3D — NO SIGNAL` clear itself and the sub-heading go back to `LIVE CAMERA` when it does? (2) does the orbit line track the vehicle and DISAPPEAR behind the real rendered planet (`PlanetGeom.Occluded` against true geometry, not the orthographic approximation)? (3) does the framing read at cabin distance — is `PlanetGeom.DefaultAzimuthDeg`/`DefaultPitchDeg` (-55 / +30) the right 3/4 view, and does the limb sit where the textured disc's did so the view does not jump on the switch? | There is no Unity camera with the game closed, so the preview can only ever draw the no-signal state — which is exactly what S10a built and what `page2_nav_planet.png` shows. S10a proved the arithmetic headlessly (66 checks) and the seam by test; nothing offline can prove a render. Framing is also a judgement at cabin distance, the one thing the PNG explicitly cannot settle (G9's reason). |
| G12 | **S42** | **Does the NAV globe have a picture under RSS at all?** Two answers in one visit: (1) on the **MAP** and **3D PLANET** views, is there a body texture, or only the graticule and the track? The 2026-09-03 log says the flat map had none — `ImageStore.BodyMap` found no usable slot on `Custom/HapkeScaled`. (2) With the log now saying it **once** (S40), read the single `no usable scaled-space map ... Texture slots:` line out of KSP.log and **write the full slot list into S42** — that list is the missing input the fix needs and it exists nowhere else. Then (3) does the S10b **camera** view show a real globe regardless, as §2 predicts it should, since a camera renders whatever shader the body wears and never asks for a texture slot? | The shader is RSS's; the preview build has no RSS, no Kopernicus and no Unity material, so nothing offline can enumerate its slots or render it. C7 forbids reading the KSP install for the answer, which leaves the flight log — and one visit yields both the slot list and the verdict on whether the camera route sidesteps the problem entirely. |

**Batch into this pass whatever else is glass-only by then** — the obvious candidate WAS **S10**'s RT planet
camera, and it is now written down as **G11** above. ⚠ S10 SPLIT on 2026-09-02: **S10a** (the pure geometry,
the seam and the honest no-signal state) is **DONE** and preview-verified, but **S10b** — `ScaledPlanetRenderer`
itself — is still **HELD** and unbuilt, and it needs THIS pass's `install` go to be built at all, not merely to
be checked. So G11 is a build-then-verify item, not a pure verification, and it should be done FIRST in the
visit if it is done at all. Any
T13/T14 criterion that turns out to need the capsule belongs here too rather than in its own visit — that
is what this line is for.

**DONE when:** both re-checks are confirmed on glass, or a NEEDS-WORK note says which way each is still off
and what the next step would cost.

### S19 [S] `VehicleOverviewPage`'s checklist copy mis-transcribes the reference — **DONE 2026-09-02**
Found by T13a while reading the reference source for the live-data wiring (not fixed — C1.1, and it is
label copy, not a value). `plugin/src/pure/VehicleOverviewPage.cs`'s `ChkLabel` read
**"RENDEZVOUS BURN BLOW"** and **"BURN GOING-GO"**. The page's own stated source —
`assets/reference/dragon2-ui-master/src/components/Overview.vue`, lines 527 and 575 — says
**"RENDEZVOUS BURN SLOW"** and **"BURN GO/NO-GO"**, and `docs/UI_AUDIT.md` (line 310, generated from that
same CSS/DOM) agrees with the .vue on both. So these were two transcription slips against a source the
repo holds, not a deliberate deviation. Tier-2 source (a recreation), quoted verbatim as the page's header
says it is — no owner call needed, a straight correction to the stated source.

**DONE 2026-09-02.** `ChkLabel`'s two entries changed to `"RENDEZVOUS BURN SLOW"` / `"BURN GO/NO-GO"`,
verified against `Overview.vue` lines 527/575 and `UI_AUDIT.md` line 310 (both re-checked, both agree).
**Verify (C1.3):** `python plugin/build.py test` green, same suites/counts, no new warnings; `ui_vehicle.png`
re-rendered and inspected — both checklist rows now read correctly, nothing else reflowed, and S20's
`LOOP A` / `LOOP B` gauge labels (26.4°C / 20.1°C) are still intact.

### S20 [owner call] The reference labels BOTH coolant gauges `LOOP A` — **DONE 2026-09-02** (resolved to (b): label the second gauge `LOOP B`)
Also found by T13a. `VehicleOverviewPage` draws two coolant gauges and labels them both `LOOP A`, which is
**faithful**: `Overview.vue` lines 222 and 272 both say `LOOP A`, and `docs/UI_AUDIT.md`'s label list carries
`LOOP A` once. But two of our own docs (`docs/REFERENCE_PAGES.md` lines 75 and 156) document the pair as
`LOOP A / LOOP B`, our model computes two distinct loops (`Cabin.LoopAC` / `LoopBC`), and T13a has now wired
the second gauge to **Loop B's** live value — so the page shows two different temperatures under one label.
The choice is between reproducing a reference bug and correcting it. **Options:** (a) leave both `LOOP A`
(reference-faithful, reads as a contradiction on the glass); (b) label the second `LOOP B` (matches
REFERENCE_PAGES, our model and the value actually drawn; deviates from the tier-2 source). **This is a label
change against a real-sourced page, so it is the owner's (C1.4) — a build chat does not decide it.**

**Decision (owner, via the overseer, 2026-09-02) = (b).** Rationale on record: the real Dragon has two
coolant loops A and B (tier-1), our model draws two distinct loops (`Cabin.LoopAC` / `LoopBC`) and T13a
wired the second gauge to Loop B's live value, and `Overview.vue`'s `LOOP A`/`LOOP A` is a recreation
copy-paste error (tier-2), not a deliberate reference choice — leaving it shows two different temperatures
under one label.

**DONE 2026-09-02.** `plugin/src/pure/VehicleOverviewPage.cs`'s second coolant `Gauge` call: label
`"LOOP A"` → **`"LOOP B"`** (the value/fraction args, `s.Cabin.LoopB01` / `s.LoopBText`, were already
correct since T13a — only the label string changes). A dated divergence note replaces the old "reproduced,
not a typo" comment at the call site, and the page header's own summary of what T13a left untouched is
updated to point at S20 instead of asserting the duplicate label is untouched. **Verify (C1.3):**
`python plugin/build.py test` green (11003 checks across all suites, 0 failed, no new warnings);
`ui_vehicle.png` re-rendered and inspected — gauges now read `LOOP A 26.4°C` / `LOOP B 20.1°C`, nothing else
reflowed. No other page/test hard-codes a duplicate `LOOP A` (`SystemsPidPage`, `VehicleSubsystemPage`,
`SettingsPage`, `Pages.cs` already used `LOOP A`/`LOOP B` correctly), so no other site needed a change.

### S21 [S] A zero-byte file named `=` is tracked in `plugin/src/` — **DONE**
Noticed by T13a while listing the glue sources. `plugin/src/=` is 0 bytes, dated 2026-08-10, and is tracked
(it came in with the initial import, `14b8c2a`) — the classic leftover of a `... = ...` shell redirect. It is
not referenced by `plugin/build.py`, compiles to nothing, and only clutters the source listing. **Fix:**
`git rm plugin/src/=` and confirm `build.py test` is still green. Trivial, but not T13's to do (C1.1).
- **DONE 2026-09-02 (hygiene sweep, owner-directed).** `git rm plugin/src/=`. No behaviour/visual change →
  preview/PNG gate N/A (C1.3). `python plugin/build.py test`: green, all suites, 0 failed. Committed
  locally (C1.5); not pushed.

### S22 [S] The reference's static status words read confidently on a dead feed — **DONE 2026-09-02**
Logged by T13a, deliberately not done (§6 scopes T13 to the numeric VALUES, and this is reference COPY —
changing it is a different kind of edit). Now that every NUMBER on the vehicle pages dashes when
`PageState.Valid` is false, the words beside them are the only things left claiming to know something:
`VehicleOverviewPage`'s seven checklist states (`Normal` / `Applied` / `Awaiting`, in green and amber),
its `CONNECTIONS → Connected` rows and `CABIN MICS: RECORDING`, and `VehicleMechPage`'s
`ALL SYSTEMS CHECK / Awaiting`. `ui_vehicle_nofeed.png` and `ui_vehiclemech_nofeed.png` show it plainly:
every gauge dashes, and a green "Normal" sits next to them. That is the failure `Pages.cs` warns about
("a screen confidently reading 0.0 is indistinguishable from a dead feed") in word form. **Fix:** decide
one rule for the whole category and apply it in one pass — most likely dash-and-dim every static status
word when the feed is invalid, leaving the label. Cheap; it is grouped here rather than split across pages
so the pages cannot end up disagreeing.

**DONE 2026-09-02.** One rule, applied in one pass to every static status word named above: on
`!PageState.Valid` the word becomes the same no-source dash (`"—"`) the numbers already use, coloured
`Dim` (`DragonPalette.Text6`) — the exact colour the codebase already uses for a dash everywhere else
(the CONSUMABLES table, `VehicleMechPage`'s own SEAT TACH rows and donut nodes). The LABEL beside each
word is untouched. `VehicleOverviewPage.cs`: the seven checklist rows' state word AND their `ic_check`
icon now dim together (the icon carries the same Go/Amber/White status the word does, so leaving it
lit while the word dashed would be its own version of the same lie); `CONNECTIONS`'s four `Connected`
rows and `CABIN MICS: RECORDING` dash the same way, reusing the page's own `T()` no-source helper
(hoisted above the checklist loop so it's in scope there too) rather than a second implementation.
`VehicleMechPage.cs`: `Awaiting` dashes/dims the same way; `ALL SYSTEMS CHECK` (already `Dim` as a
label) is untouched. Nothing here touches a live-wired row — `SystemsTreePage`/`VehicleSubsystemPage`'s
own Go/Amber/neutral checklist logic (S25) is a different, already-live category and was not touched.
**Verify (C1.3):** `python plugin/build.py test` green (same suite set, 0 failed, no new warnings).
Preview: `ui_vehicle_nofeed.png` and `ui_vehiclemech_nofeed.png` re-rendered and inspected — every
checklist icon+word, `CONNECTIONS` row and `CABIN MICS` now reads a dim `—` beside the dashed gauges,
no stale green/amber/red word remains; `ui_vehicle.png` and `ui_vehiclemech.png` (live feed) re-rendered
and inspected — `Normal`/`Applied`/`Awaiting`, `Connected` and `RECORDING` still read in their original
colours, confirming the dash-and-dim path only engages on `!Valid`.

### S23 [owner call] `BATTERIES ×4` names four batteries; the live count beneath it says otherwise — **DONE 2026-09-02** (resolved to (b): drop the `×4`, on both the systems tree and the Power subsystem page)
Also T13a. The systems-tree battery node keeps the label `BATTERIES ×4` — the REAL Crew Dragon's own
battery count, reused verbatim from the Power checklist — while the state line under it is now this
vessel's LIVE count of parts holding charge, which on the KSP craft is whatever it is (`2 / 2` in the
preview). Both halves are correct and they read as a contradiction. **Options:** (a) leave it — the label
is a vehicle fact, the value is this vessel's state; (b) drop the `×4` so the label makes no count claim;
(c) make the label itself live. (b) and (c) change a label that came from a real-sourced set, so this is
the owner's (C1.4). The identical `SOLAR ARRAY` / `BATTERIES ×4` pair on the **Power subsystem page** is
still fully representative — that page is **T13b**'s, and whatever is decided here should land there too.

**Decision (owner, via the overseer, 2026-09-02) = (b).** Drop the `×4` count-claim; the label reads plain
`BATTERIES`. Rationale on record: a static "×4" over a live count misleads (reads as "N of 4 present") on
any craft that isn't 4 batteries. Explicitly done together with **S25** (below), in one pass, across both
pages, so neither page could be left one edit ahead of the other.

**DONE 2026-09-02 (with S25, one pass, both pages).** `SystemsTreePage.cs`'s battery `NodeBox` label
"BATTERIES ×4" → **"BATTERIES"**; `VehicleSubsystemPage.cs`'s Power `CkLabel` "BATTERIES x4" →
**"BATTERIES"**. **Label-doc handling (C1.4/C7.1):** no separate `docs/` file was found to record this
transcription (checked `UI_AUDIT.md`, `REFERENCE_PAGES.md`, `REAL_DRAGON_SCREENS.md`,
`SCREEN_INVENTORY.md`, `SCREEN_EVIDENCE_MATRIX.md`, `BUILD_PLAN.md` — none mention it; the checklist
content came in with the initial 2026-09-02 import, `864c2e4`, with no separate doc behind it) — the only
places that recorded "BATTERIES ×4" as real were `SystemsTreePage.cs`'s own header/inline comments and
this register entry. Both are KEPT (the real transcription — "the real screen shows BATTERIES ×4" — is
still stated) with a dated note added alongside explaining the drop is this owner decision, not a
re-transcription (see `SystemsTreePage.cs`'s file header and its battery `NodeBox` comment). Nothing
falsified: the record says what the real screen shows AND what this build now displays and why they
differ. **Gate + test:** see S25's DONE note — one gate covers both.

### S24 [owner call] The AVIONICS tab reads nine dashes — is CommNet a legitimate source for part of it? — **DONE 2026-09-02** (resolved to (b): S-BAND COMMS / Uplink / Downlink wired to stock CommNet, everything else stays dashed)
Found by T13b, which wired the other five subsystem tabs and left this one entirely dashed because that is
what `docs/TELEMETRY_REGISTRY.md` requires: nothing in this build models flight-computer load, data-bus
traffic, storage, GPS lock or a link budget, and no KSP quantity stands in for them. `ui_vehicleavionics.png`
shows the result — nine dashes and four empty rings. It is *correct*, and it is also the only tab with no
live number on it. **What might change that:** stock KSP's own CommNet carries real state
(`vessel.Connection.IsConnected` / `SignalStrength`, and the control level), which could honestly answer
`Uplink`, `Downlink` and `GPS Sats`-adjacent rows, and `S-BAND COMMS` in the checklist. It could NOT answer
`LINK MARGIN` in **dB** without inventing a conversion from a 0..1 strength, and it says nothing about
`FC LOAD`, `BUS TRAFFIC` or `STORAGE`. The registry's own note that comm-link readouts are "SIMULATION
unless a comms mod supplies them" points the same way. **Options:** (a) leave the tab dashed — no new source,
no new dependency; (b) adopt CommNet for the link rows only, dashing `LINK MARGIN` and the three computer
gauges; (c) adopt CommNet and additionally define a stated dB mapping. **Adopting a new authoritative source
is a §1.4 decision, so it is the owner's** — a build chat does not add one on its own. (b) is the smallest
honest step if the answer is "yes".

**Decision (owner, via the overseer, 2026-09-02) = (b).** Populate Uplink / Downlink / S-BAND COMMS from
stock CommNet; dash LINK MARGIN (no dB conversion from a 0..1 strength — inventing one violates §1.4), FC
LOAD / BUS TRAFFIC / STORAGE (no KSP source) and GPS (a comm link is not a GPS source; do not borrow
connection state for it — left exactly as it was, untouched).

**DONE 2026-09-02.** Source: `Vessel.Connection` → `CommNet.CommNetVessel` (confirmed present in KSP's own
`Assembly-CSharp.dll` by reflecting the installed assembly before writing any code — `IsConnected` (bool),
`SignalStrength` (double, 0..1) — plus the static `CommNet.CommNetScenario.CommNetEnabled` difficulty flag).
Real stock state, same footing as every other `vessel.*` read in `VesselData.cs` — nothing invented (§1.4).
- New glue: `VesselData.Avionics(Vessel)` (`plugin/src/VesselData.cs`), called from `Refresh()` alongside
  `VehicleSources`. Gated on `CommNetEnabled`; `conn == null` (CommNet off, or this vessel carries no
  `CommNetVessel`) sets everything null/false so the page dashes exactly like any other unsourced row —
  never a stale "Linked". Uplink and Downlink are the SAME real signal strength (CommNet has no separate
  up/down budget) — two fields for one number, the same reasoning already used for
  `PowerUnit1Text`/`PowerUnit2Text`.
- New `PageState` fields (`plugin/src/pure/Pages.cs`): `SBandText`, `UplinkText`, `DownlinkText`,
  `SBandLinked`, `CommSignal01`.
- `VehicleSubsystemPage.DefOf` (`Sub.Avionics`): S-BAND COMMS checklist row now reads `T(st.SBandText)`
  with a live checkmark colour (green linked / amber no-signal / white-neutral dashed); Uplink/Downlink
  rows read `T(st.UplinkText)`/`T(st.DownlinkText)` with `st.CommSignal01` driving both bars, formatted as
  a percentage (`Pct()`), never a fabricated unit. FC1/2/3, GPS Sats, Data Rate, and all four headline
  gauges (FC LOAD/BUS TRAFFIC/LINK MARGIN/STORAGE) are untouched. GPS checklist row untouched.
- **Test (`plugin/test/FigmaUINavTest.cs`):** `VehicleFixture` grows the three new fields (varying between
  the A/B fixtures); `SubsystemLiveValues`' Avionics entry in `live[]` now asserts S-BAND/Uplink/Downlink
  move with the fixture and dash with no feed, same as every other tab's wired values; the cross-tab
  "avionics invents no value" loop now skips Avionics' own index (else it would assert the opposite of
  what this wires in); a new dedicated block builds an OTHERWISE-VALID fixture with the three CommNet
  fields nulled out (simulating CommNet off/absent while the vessel itself is fine) and asserts all three
  dash gracefully. `python plugin/build.py test`: **green, 0 failed** (Figma UI nav suite 547 checks).
- **Preview:** `ui_vehicleavionics.png` (linked: S-BAND "Linked" in green, Uplink/Downlink "82 %" with a
  filled bar) and a new `ui_vehicleavionics_commoff.png` (CommNet off: S-BAND "—" white-neutral,
  Uplink/Downlink "—" with an empty bar) both inspected — the four gauges and the other three readouts
  stay dashed in both, GPS is unaffected either way. §1.4 respected: no `PanelMap.cs` / label-doc edit:
  the checklist LABELS (`CkLabel`) were already real-sourced copy and are untouched, only the live VALUE
  and its colour changed.

### S25 [S] The Power tab's checklist still reads `4 / 4` and `Deployed` beside the live sources for both — **DONE 2026-09-02**
Found by T13b, deliberately not done: §6 scopes T13 to the numeric VALUES and the register line scoped T13b
to the 54 gauge + readout values, so the left checklist was left untouched. `VehicleSubsystemPage`'s Power
checklist carries `BATTERIES x4 → "4 / 4"` and `SOLAR ARRAY → "Deployed"` as static strings, while
`PageState.BatteryText` and `PageState.SolarArrayText` — the REAL counts and the REAL
`ModuleDeployableSolarPanel` state, wired by T13a — are already in hand and are what the systems tree draws
two pages away. So the same vessel can read `2 / 2` on one page and `4 / 4` on the other. **Fix:** point both
checklist states at those two fields, the way the systems tree does. ⚠ **Do this WITH S23, not before it:**
S23 is the owner's call on the `BATTERIES ×4` LABEL and says explicitly that whatever is decided there should
land here too — wiring the value first would leave the label question half-answered on two pages instead of
one. Also related: **S22** (the static status words that stay confident on a dead feed) covers this
checklist's other five rows.

**Done together with S23, one pass (owner decision, via the overseer, 2026-09-02 — recorded as the
owner's per C1.12).** `VehicleSubsystemPage.cs`'s Power case (`DefOf`): `CkState[2]`/`CkState[3]` now read
`T(st.BatteryText)` / `T(st.SolarArrayText)` — the SAME two `PageState` fields `SystemsTreePage.cs` draws
(T13a) — instead of the static `"4 / 4"` / `"Deployed"`, so a dead feed dashes them like every other live
row on the tab rather than holding a stale value. `CkKey[2]`/`CkKey[3]` (the checkmark + state-text
colour) now mirror the systems tree's own live-colour logic exactly rather than staying a flat green:
BATTERIES is Go whenever the vessel carries any charge-holding parts (`BatteryText` not empty/`"NONE"`),
neutral only if it carries none or the feed is dead; SOLAR ARRAY is Go only when fully `DEPLOYED`, amber
for any other real state (`STOWED` / mid-deploy / `NONE`), neutral with no feed — the tree's own
Go/Caution/Faint three-way, mapped onto the checklist's Go/Amber/neutral vocabulary (no separate "Faint"
colour exists there). The other five checklist rows are untouched (S22's territory, not this task — no
scope creep). Consequence: the `2/2`-vs-`4/4` contradiction the finding named is gone — both pages now
read the identical live text for the identical vessel.
- **Test (`plugin/test/FigmaUINavTest.cs`):** `SubsystemLiveValues`'s POWER entry in `live[]` grows
  `a.BatteryText`/`a.SolarArrayText` (asserts both move with the fixture and dash with no feed, the same
  loop every other tab's wired values go through); POWER's `gone[]` grows `"4 / 4"`/`"Deployed"` (the two
  retired hard-codes must never come back). `VehicleFixture`'s `BatteryText` changed from `"4 / 4"` (which
  would have made that new `gone[]` guard vacuous — equal to the very constant it replaced, the same
  problem `LoopAText`'s existing comment flags) to `"2 / 5"` / `"1 / 5"`, picked to avoid colliding with
  AVIONICS' own static `"3 / 3"` and GNC's static `"2 / 2"` (both would have false-failed the cross-tab
  "avionics/other tabs invent no value" checks by coincidence — caught by a first failing run, fixed).
  New dedicated checks: the Power checklist draws exactly `"BATTERIES"` and never `"BATTERIES ×4"` /
  `"BATTERIES x4"` (S23's own regression guard, on either page — the systems tree's matching old assertion
  was updated the same way). `python plugin/build.py test`: **green, 0 failed** — Figma UI nav suite 547 →
  **560 checks** (all other suites unchanged).
- **Preview:** `ui_vehiclepower.png` inspected — `BATTERIES` / `2 / 2` and `SOLAR ARRAY` / `DEPLOYED`, both
  green, matching `ui_systemstree.png`'s `BATTERIES` / `2 / 2` and `SOLAR ARRAY` / `DEPLOYED` on the SAME
  fixture — the contradiction is gone. `ui_systemstree_live.png` re-inspected — unaffected (label only,
  the state colouring/values there were already live since T13a). `ui_vehiclepower_alerts.png`
  re-inspected — the checklist (drawn outside the FUNCTIONS/ALERTS toggle) shows the same live values on
  the ALERTS view too, no overlap/clipping. No new `DisplayList` overflow; no new compiler warnings (same
  6× CS0162 / 2× CS0219 / 1× CS0649 baseline as before this task). `plugin/build/csc.rsp` churn reverted
  before commit (S11 precedent).
- **§1.4 / C1.4 respected:** the checklist LABELS were real-sourced copy and only the one owner-authorized
  edit (S23) touched a label; no `PanelMap.cs` edit; no other label doc touched.

### S26 [S] Manual docking: the target diamond is fixed, and the axis group is drawn twice — **DONE 2026-09-02** — [TIER 4: scheduled build/polish]
Found by T13c, deliberately not done (C1.1). `DockingSimPage` now prints live ROLL / PITCH / YAW, RANGE and
RATE, but the green target diamond is still drawn at a FIXED offset from the reticle (`tx = HCX + 70f,
ty = HCY - 48f`) — so it sits off-centre while the page reads 0.1° of error, and `ui_docking_notarget.png`
shows it still hovering there with **no target at all**. Same class of thing T13a fixed on the MECH panel's
fixed 240° rings: decoration beside a number that now moves.
Not fixed here for two reasons. It is a LAYOUT/geometry change, and §6 scopes T13 to the numeric VALUES;
and unlike T13a's rings the fractions are not already in hand — placing the diamond honestly needs the
pitch/yaw bearings as raw doubles in `PageState` (only their text exists today) plus a decision about how
many degrees of error equal the ring radius, which no source in the repo states.
**When it is taken:** add the raw bearings beside `PitchDegText`/`YawDegText`, place the diamond from them,
hide it entirely with no target, and state the ring's degrees-full-scale at its own site the way
`VehicleSubsystemPage` states its 2 °/s rate dial. Note the two things the iss-sim reference DOES confirm
(SCREEN_INVENTORY #11): the diamond is the target, and the readouts go green when corrected — the second is
a colour rule this build does not implement either, and belongs with the same task.

**Second, related layout question on the same page** (the other thing `DockingSimPage`'s header points
here for). The page draws the ROLL / PITCH / YAW group TWICE — once around the rings, once as the PYR
block — and `docs/SCREEN_EVIDENCE_MATRIX.md` describes ONE group ("Rotation readouts ROLL / PITCH / YAW
(grouped 'PYR'), each a value in degrees"). T13c wired both to one source so they can no longer
disagree, which is the safe outcome and as far as a VALUES task may go; whether one of the two is
redundant chrome that should be dropped, or given the second confirmed quantity (the reference's blue
RATES — `PitchRateText`/`YawRateText`/`RollRateText` are already in `PageState`), is a layout call
against the iss-sim reference. Take it with the diamond above, in one pass over this page.

**Built, owner-directed (2026-09-02, via the overseer).** One pass over `DockingSimPage`:
- Added `RollDeg`/`PitchDeg`/`YawDeg` (raw doubles) to `PageState` beside the existing `*DegText`
  strings ([Pages.cs](plugin/src/pure/Pages.cs)); `VesselData` now sets both from the same value
  ([VesselData.cs](plugin/src/VesselData.cs)).
- The diamond now places itself from `YawDeg`/`PitchDeg` against a new STATED constant
  `DockingSimPage.RingFullScaleDeg = 8f` (pegs at the inner ring past 8°, documented in code — no
  source states a real number, same footing as `VehicleSubsystemPage.RateFullScaleDps`), and is
  **hidden entirely** with no target (was drawn unconditionally at a fixed offset).
- Ring axis readouts (ROLL/PITCH/YAW) now go **GREEN when corrected** — within a new STATED
  `CorrectedToleranceDeg = 0.5f` of zero (iss-sim: SCREEN_INVENTORY #11) — and **WHITE** otherwise,
  replacing the old "green whenever a target exists regardless of error" tint.
- **The duplicate-PYR decision:** gave the PYR block the reference's other confirmed quantity, BLUE
  per-axis RATE (`PitchRateText`/`YawRateText`/`RollRateText`, T13b body rates), instead of dropping
  it. Chosen over dropping because `DockingPage.cs`'s own header already names iss-sim's "GREEN
  correction / BLUE rate, two numbers per axis" scheme as its key design takeaway from that same
  reference — this page had the correction drawn twice and the rate nowhere, so it now matches that
  existing precedent (one axis, two colours, one place each) rather than inventing a third scheme.
  Not a genuine owner call in the end: the reference argument was concrete enough to decide in code
  (stated in `DockingSimPage.cs`'s own header), so C1.9/C1.13 was not invoked.
- §1.4/C1.4: no `PanelMap.cs` edit, no label-doc edit; both new constants are STATED/marked in code as
  ours, not sourced.
- **Gate:** `python plugin/build.py test` green, 0 failed (Figma UI nav suite 724 checks, up from 560;
  no new compiler warnings — same 6× CS0162 / 2× CS0219 / 1× CS0649 baseline). New checks: the diamond
  is hidden with no target (0 green `Line` commands), drawn with a target (4 green `Line` commands),
  and moves between two fixtures with different `YawDeg`/`PitchDeg`; each axis correction now draws
  ONCE (not twice) and each PYR rate draws once; an axis reads GREEN when within tolerance and WHITE
  when not.
- **Preview:** `ui_docking.png` (mixed state: PITCH/YAW corrected → green, ROLL not → white, PYR shows
  live rates), `ui_docking_notarget.png` (diamond and all readouts gone, dashes only — re-inspected,
  unaffected) and a new `ui_docking_corrected.png` (all three axes within tolerance → all green, diamond
  centred) all inspected and match the intended behaviour.

### S27 [owner call] The reconstructed pages still have no in-page entry point — four DONE tasks parked it on T14 — **DONE 2026-09-02** (resolved to (b): no Cover-rail assignment; built a Vehicle-page affordance to the two systems deep-views instead)
Raised by **T14**, which is the task those four named. `T7` (Deorbit Burn Prep), `T8` (Entry), `T9`
(Systems Tree + Systems P&ID) and `T12` (Ascent / Launch) each end with a line saying a real phase-rail or
in-page entry point "is **T14**'s job", and `FigmaUI.cs`'s own `VrioTest` comment says the same of a
phase-rail "Procedure" item. T14 did not do it, deliberately, and this is why:
- The Cover rail's seven items are **real** (`REAL_SPACEX_SCREENSHOTS` / `SCREEN_INVENTORY`), and two of
  them are labelled just **"Procedure"**. What CONTENT sits behind each is **not in any source** —
  `SCREEN_INVENTORY.md` line 83 lists the rail's real per-item content ("each with numbered command
  steps") as 🔴 **unbuilt, many**. Pointing slot 3 at a page we happen to have built would be a claim
  about the real screen that nothing supports: a §1.4 tier-3 invention, which is the owner's call and
  never a build chat's (C1.4 / C1.12).
- There are **three** candidate procedure pages (VrioTest 4.700, Deorbit Burn Prep, Entry) for **two**
  slots, and **two more** (Systems Tree, Systems P&ID) that are vehicle deep-views and do not belong on a
  deorbit rail at all — T9 already ruled out giving them `VehicleTabBar` tabs for the same C1.4 reason.
- **Nothing is unreachable in the meantime.** All six are on the Menu grid (auto-discovered) and under the
  global bottom bar, which is how they have been reached since they were built.
**Options for the owner:** (a) assign the two "Procedure" slots explicitly (which page behind each, owner
says); (b) leave the rail alone and give the deep-views an affordance elsewhere (e.g. from the Vehicle
pages, our geometry, marked as ours like the FUNCTIONS|ALERTS toggle); (c) leave all six on the Menu grid
and close this. **DONE when:** the owner's choice is recorded here and built, or (c) is chosen and this
line is closed.

**Decision (owner, via the overseer, 2026-09-02) = (b).** The Cover phase rail's two "Procedure" slots are
left unassigned — no source names what belongs there, and assigning one of VrioTest / Deorbit Burn Prep /
Entry would be a §1.4 tier-3 claim on a real screen (C1.4). All three stay reachable via the Menu grid +
bottom bar exactly as before. `SystemsTree` / `SystemsPid` get an affordance FROM the Vehicle pages
instead — the (b) branch, scoped to just those two (T7/T8's procedure pages are a separate, still-open
question the rail-slot options above cover; this task does not touch them).

**DONE 2026-09-02.** New `plugin/src/pure/VehicleDeepViewLinks.cs` — two links, "SYSTEMS TREE" /
"SYSTEMS P&ID", drawn on every Vehicle-family page (`VehicleOverviewPage`, `VehicleSubsystemPage`'s six
sub-tabs including both the FUNCTIONS and ALERTS views, `VehicleMechPage` — `FigmaUI.IsVehiclePage`'s own
set), same footing as T5's FUNCTIONS|ALERTS toggle and T6's Docking→Rendezvous affordance: our own
geometry, marked as ours in the file's header comment, one rect shared by `Draw` and `HitTest` (PageAction's
rule) so drawing and hit-testing can never drift apart. Deliberately NOT a ninth/tenth `VehicleTabBar` tab
(T9 already ruled that out — C1.4); placed to the right of the tab strip's own hit region, past its
rightmost real tab (`VehicleTabBar.CentreX(7)`'s hit edge at design-x 2533.5), in the row's own unused
space, so a drawn or hit-tested link can never collide with a real tab. Wired in `FigmaUI.HitTest` inside
the existing `IsVehiclePage(page)` branch, right after the tab-strip check; `UiPage.SystemsTree`/`SystemsPid`'s
enum comments in `FigmaUI.cs` updated to point at the new path instead of the stale "T14's job" line.
**Nav test:** new `FigmaUINavTest.VehicleDeepViewLinksTest()` — both links route to the right page from
all eight Vehicle-family pages (16 routes checked), the real Thermal tab still resolves correctly (no
overlap with the link geometry), the gap between the two links is inert, the links are inert on a
non-vehicle page (Hud), and both destinations are real pages, not placeholders. `python plugin/build.py
test`: **green, Figma UI nav suite 654 checks, 0 failed**, no new warnings. **Preview:** `ui_vehicle.png`,
`ui_vehiclemech.png`, `ui_vehiclepropulsion.png` (the tightest sibling — Prop's own thruster data band
sits well clear, ends ~y1652 design-space vs. the links' 1778+) and `ui_vehiclepower_alerts.png` (the
ALERTS view) all inspected — the two accent-coloured links sit cleanly to the right of the tab strip on
every one, no overlap/clipping with any existing content, `ui_systemstree.png`/`ui_systemspid.png`
re-inspected unchanged (the destinations draw no tab strip and no links of their own, as before). §1.4
respected: no `PanelMap.cs` / label-doc edit; the two new label strings are stated as ours, not
transcribed from any source.

### S28 [owner call] Should the manual-docking clusters actually fly the capsule? — **decided-(a), recorded 2026-09-02 — not built (Part B's job)**
Raised by **T14** and flagged in `DockingSimPage`'s own header long before it (*"wiring them to RCS (the
owner's 'hidden mini-game' idea) is a later decision"*). T14 applied the decision that IS settled —
§14.4(a): flight actuation is an honest no-op until Part B — so the twelve direction pads and Reset
Positions resolve to a named act, log, and do nothing. That is correct-by-the-plan and it is also the
least interesting possible answer for a page whose whole subject is flying by hand.
**Options:** (a) leave them as the §14.4(a) no-op and let **Part B** wire them with everything else
(§B12.5 / §B10.6 already own the 16-Draco RCS tuning); (b) wire them to KSP's RCS translation/rotation
inputs NOW as a screens-only exception, which needs an explicit §14.4 entry because it contradicts the
standing "screens fly nothing" rule; (c) keep them inert permanently and say so on the page.
**(b) needs an owner `OVERRIDE` + a §14.4 log entry** — it is a change to a settled decision (C1.8).
The seam is already in place either way: `DockingSimPage.IsActuation` names the set, and the dispatch is
one method in `ScreenPainter` (`DockAction`), so none of the three options costs geometry or drawing work.

**Decision (owner, via the overseer, 2026-09-02) = (a).** The twelve direction pads and Reset Positions
stay the §14.4(a) honest no-op — click, log, no action — and **Part B** (§B12.5 / §B10.6, which already own
the 16-Draco RCS tuning) wires them when the conductor lands. No `OVERRIDE` was given, so (b) is not in
play and §14.4 is not amended. **Recorded 2026-09-02 — bookkeeping only, no build (C1.1):** the seam
`DockingSimPage.IsActuation` / `ScreenPainter.DockAction` already carries this exactly as it should; there
is no code change for (a) to make. This line is closed as decided.

### S29 [S] Four display-only controls remain, all outside §6's list — **DONE, 2026-09-02**
Noticed by **T14** while wiring the four groups §6 names (logged, not done — C1.1). None of these is in
that list, and each wanted a decision rather than a wire.

**Decision (owner, via the overseer, 2026-09-02):** the DONE-when's "recorded reason for having none"
branch for all four — do not invent a real control's function without a source (§1.4, the
inert-until-verified discipline of §14.4b). No behaviour or geometry change; recorded at each control's
site in the code:
- `SuitCheckPage`'s two left-panel plates under the caption **"ENTER READ-ONLY"** (`ic_grid` and
  `ic_eye`) — stay inert, comment added at the draw site ([SuitCheckPage.cs](plugin/src/pure/SuitCheckPage.cs)):
  the reference does not say which plate arms read-only or what the other does, and neither was in T14's
  §6 wiring scope.
- `DockingSimPage`'s **"Instructions"** — stays inert, header comment expanded
  ([DockingSimPage.cs](plugin/src/pure/DockingSimPage.cs)): this build has no instructions content, so
  there is nothing to be actuation or screen-state.
- `DockingSimPage`'s **"Reset Positions"** — keeps T14's §14.4(a) no-op, same header comment: the
  reference does not confirm whether it resets the vehicle (actuation) or only the page view (screen
  state), so it stays the conservative classification until confirmed.

`python plugin/build.py test`: green, 260 T14 touch-wiring checks + all other suites unchanged (no new
checks needed — no behaviour changed). Preview N/A (no render change). §1.4 respected: no invented
function, no `PanelMap.cs` / label-doc edit.

### S30 [S] `_AutopilotStub.cs` still describes the deleted RED refuse state — **DONE**
Noticed by **T14** while routing the chute page through `FlightCommands.Run` (comment-only; not fixed,
C1.1). The file's header says the command buttons "are no-ops that honestly refuse (**a red flash**)" and
`Run`'s own comments say "true = white flash (actioned), false = **red flash** (honestly cannot)" and
"honest **red flash**". §14.4(a) **removed the red state** on 2026-09-02 — `PanelLight` has two values,
`PanelBehaviour` enforces bright-or-dark, and `PanelMap`'s own header records the removal. So this is the
one file still telling a reader the panel has a colour it does not have, and it is the file every new
command surface reads first (T14 read it). **Fix:** three comment edits, no code. **DONE when:** the stub
describes click-no-light-no-action, and `build.py test` is still green.
- **DONE 2026-09-02 (hygiene sweep, owner-directed).** Four comment edits in `plugin/src/_AutopilotStub.cs`
  (the header's "a red flash", the dispatcher banner's "(red flash)", `Run`'s "white flash (actioned) /
  red flash (honestly cannot)", and the FLY/actuate section banner's "honest red flash") all now read
  click-no-light-no-action / "honestly cannot" — no code changed, comments only. Confirmed no remaining
  "red flash"/"white flash" text in the file. No behaviour/visual change → preview/PNG gate N/A (C1.3).
  `python plugin/build.py test`: green, all suites, 0 failed. Committed locally (C1.5); not pushed.

### S31 [O] `SuitCheckPage`'s four SUIT n STATUS rows still read a confident "Nominal" — not covered by S22 — **DONE 2026-09-02** (resolved by §14.4(e): SIMULATE the suit, verdict follows the sim)
Noticed by **S22** while fixing the static-status-word category on the vehicle pages (not fixed here — C1.1,
different file, different page). `SuitCheckPage.cs`'s own header (line 20) and its `Row` call (line 127,
`Row(5 + i, Suit[i] + " STATUS", "Nominal", "ic_check", Go, Go)`) both cite S22 as the reason the four
SUIT n STATUS words are reproduced as static reference copy — but they are drawn unconditionally, green,
every time, and S22's fix never touched this file. The reason is structural, not an oversight in S22's
pass: `SuitCheckPage.Build` (line 56) takes `(DisplayList dl, int w, int h, int countdown, bool showPopup)`
— no `PageState`, no `s.Valid` — so there is no feed-validity signal on this page for a dash-and-dim rule
to key off of at all; S22's fix (dash-and-dim on `!PageState.Valid`) has nothing to attach to here. The
four SUIT n DELTA PRESSURE rows beside them already dash permanently (T13c: no suit is modelled, so there
is no source ever, feed-valid or not) — STATUS sits right next to a permanent dash reading a permanent
"Nominal", which is the same "screen confidently reading a state it cannot know" shape S22 just fixed
elsewhere, just not feed-gated since this page has no feed concept. **Options for whoever takes this:**
(a) leave it — this page's checklist rows are load-bearing UI, not telemetry, and the procedure countdown
IS this page's real live state (already honestly wired); (b) since nothing models a suit at all (T13c),
dash STATUS permanently too, the same way DELTA PRESSURE already does — consistent with "no source = dash"
rather than "no source = a static green word"; (c) thread `PageState`/`Valid` into this page so STATUS can
follow the same dash-and-dim rule as everywhere else, wiring in the vessel feed just to gate a checklist
word this page didn't otherwise need. (b) looks like the smallest honest fix (matches the row right next
to it, no new dependency) but (a)/(b) is a §1.4-adjacent call about what "reference COPY" means on a
procedure page vs a telemetry page, so this is flagged rather than picked. Also correct the two stale
"(S22)" comments (`SuitCheckPage.cs:20` and `:127`'s neighbourhood) once this is resolved — they now point
at a task that does not cover this file.

**⚠ SUPERSEDING POLICY (G2, 2026-09-02): read §14.4(e) BEFORE choosing.** The owner's
simulation-for-immersion policy landed after this line was written and it governs this task — a real
quantity that is simply not modelled yet goes to an installed mod's value, else a COHERENT MARKED
simulation off real cabin state; a dash is for quantities that genuinely do not exist. So option (b)
"dash STATUS permanently" is no longer the default it reads as here, and the GUARDRAIL applies
directly: the STATUS verdict must FOLLOW whatever the sim says, never be a hardcoded "Nominal".
The option list above is left as written for the record; §14.4(e) is what decides between them.

- **DONE 2026-09-02 — owner-directed, run OUT of `/next` order after G2, and decided by the owner (Chris)
  via the overseer: SUPERSEDE the option list above and SIMULATE the suit life-support state per
  §14.4(e).** Run on Opus as an [O] (the line was written [S] before the policy turned it from a
  three-line comment fix into a model + two page states + a test suite).
  **Built:**
  · **New `plugin/src/pure/SuitLeakSim.cs`** — the model, with its header stating what is REAL (cabin
    pressure, from `PageState.Cabin` / `CabinEnvironment`, itself TAC-LS-driven through
    `LifeSupportBridge`), what is SIMULATED (one regulated suit-loop pressure `SuitLoopPsia` = 15.00
    psia, small stated per-suit fit offsets so four suits are four readings, and a leaking suit's
    bleed-down) and what is ROLLED. `SuitCheckState` carries the four differentials; `Failed(i)` is a
    threshold on them, so it is the VERDICT and there is no path that writes one in by hand.
  · **The four SUIT n DELTA PRESSURE rows are live** — ΔP = suit loop − REAL cabin pressure, so all four
    move when the cabin moves (they were "0.01psi", a constant, then a permanent dash after T13c). Nominal
    ~0.28 psi, printed `0.28psi` in the reference's own format (`docs/UI_AUDIT.md`: `0.01psi`).
  · **The four SUIT n STATUS words are a verdict on that sim** — "Nominal" only while the differential is
    holding above `PassPsi` (0.10), "Failed Low" (amber, `ic_stop`) once a suit has bled below it. §14.4(e)'s
    GUARDRAIL satisfied: the S22-class hardcoded green word is gone. **No feed = the whole table dashes**,
    the honest case §14.4(e) keeps a dash for.
  · **The 5% leak roll is SEEDABLE, not a loose RNG** — `SuitLeak.LeakingSuit(seed)` is a pure function of
    the run seed, so a verdict is stable for a whole run, two screens agree, and both branches are
    reachable from a test (`SeedForLeak`). `ScreenPainter` mints one seed per run from the real clock at
    INITIATE / **TRY ADDITIONAL TIMER (so re-running re-rolls)**; HALT drops it; FINISH reports the run it
    already has; a page change resets it.
  · **The leak outcome raises the SAME box** — same scrim, panel, title/ECLSS/status-word/headline/body and
    the same close control as the photographed completion popup, carrying `FAILED LOW` /
    `SUIT LEAK DETECTED` / "Suit n did not hold pressure." / **"Repair suit and rerun suit check."** Our own
    copy for our own simulated feature, marked as such in the file.
  · **Threading:** `SuitCheckPage.Build` now takes `SuitCheckState` as its own input (it took no vessel
    state at all, which is exactly why S22's fix could not reach it); `FigmaUI.Build` gained a `uint
    suitSeed` and assembles the state from the `PageState` it already had. **The real procedure countdown
    is untouched.**
  · **The two stale "(S22)" comments are corrected**, plus the FLOW paragraph and the fail-branch note that
    both asserted "no suit is modelled". `PanelMap.cs` and the label docs were NOT touched (§1.4 / C1.4).
  **Gate (C1.3):** `python plugin/build.py test` **green — 11 suites / 11333 checks, 0 failed**, with new
  coverage in `FigmaUINavTest.SuitLeakSimulation()` (the roll is deterministic in its seed, lands within
  4–6% over 40 000 seeds, every suit reachable; a clean run draws "Nominal" exactly 4× and the completion
  box; a forced-leak run draws it 3× + one "Failed Low" and the repair-and-rerun box; a leaking suit bleeds
  DOWN through the countdown; no feed and a half-built feed both yield no verdict) and in
  `ProcedureLiveValues` (all four ΔP strings move between two cabin fixtures; dead feed dashes the table).
  `TouchWiringTest`'s "no suit can fail this check yet" check was restated — that reason is now false.
  **Preview inspected:** `ui_suitcheck.png` (four distinct live ΔP, four green Nominal) and the new
  `ui_suitcheck_leak.png` (the repair-and-rerun box); the underlying leak table was rendered once during
  the pass to confirm the amber `0.01psi` / `Failed Low` row, then the render restored to the popup state.
  Committed locally (C1.5); NOT pushed.
- ✅ **Constants RATIFIED as built — owner (Chris), via the overseer, 2026-09-02 (Q1 = (a), no change):**
  `SuitLoopPsia` 15.00 · `PassPsi` 0.10 · `LeakFallPsi` 0.28 · the four `Fit` offsets · `LeakChance` 0.05
  stand exactly as S31 built them; the ΔP magnitude's legibility on the real glass is to be eyeballed at
  the **S18** glass pass (recorded here by S32's commit — a decision logged, not a second task).
- ⚠ **Logged, not done (C1.1):** see **S32** — TROUBLESHOOT is still inert, and its stated reason changed.

### S32 [owner call] The Suit Leak Check's TROUBLESHOOT is still inert, and its reason has changed — **DONE 2026-09-02** (owner chose (b): a MARKED reconstructed-from-function action — repair + rerun)
Found by **S31** (logged, not done — C1.1, different question). Until S31, `SuitCheckPage.FailBranchLive`
was `false` for a reason that made the control moot: **no suit could fail this check**, so the fail
branch's own question ("Did any suit fail the leak check?") answered itself and TROUBLESHOOT had nothing
to respond to. S31's marked simulation (§14.4(e)) removed that: a suit CAN now read "Failed Low", the
crew CAN now be looking at the branch's question with a real answer in front of them, and the one control
that responds to it is still drawn dimmed and does nothing. S31 restated the constant's doc-comment, the
draw-site note and `TouchWiringTest`'s check so none of them still claims "no suit is modelled" — but it
did NOT flip the constant, because what is missing now is different: **no reference frame says what
pressing TROUBLESHOOT does.** §1.4 keeps an unverified control inert rather than inventing a function for
it, and §14.4(d) only decided that the fail BRANCH is kept as a marked reconstruction, not what its button
does. **Options:** (a) leave it dimmed — honest, and the page already says "unavailable" rather than
swallowing the press; (b) give it a reconstructed-from-function action under §14.4(d)'s own precedent
(e.g. it re-opens the branch text / marks the suit for the ground), owner-decided and marked; (c) find a
real source first (a 4.011 continuation frame past the "Scroll to continue" fold) and only then wire it.
`FailBranchLive` is still the single edit whichever way this goes. Needs an owner call, not a build-chat
one (§1.4 tier-3).

- **DONE 2026-09-02 — owner-directed (run OUT of `/next` order), decided by the owner (Chris) via the
  overseer: option (b).** TROUBLESHOOT gets a **reconstructed-from-function** action under
  **§14.4(d)+(e)**, MARKED in code and NOT claimed as real — option (c) is closed, not deferred: there is
  no 4.011 continuation frame to find (the real table scrolls past its "Scroll to continue" fold and that
  content is ITAR-class), so waiting for a source would leave the control dead forever. The function is
  read off the page's own instruction to the crew, "Repair suit and rerun suit check."
  **Built:**
  · **`SuitCheckPage.FailBranchLive` flipped to `true`**, its doc-comment rewritten to say what the action
    is, where it came from and why no source is coming; the header's reconstruction block, the FLOW
    paragraph and the fail-branch draw-site note all restated to match (nothing left claiming it is inert).
  · **`Available` now takes the suit state** (`Available(SuitAct, SuitCheckState)`): TROUBLESHOOT is live
    **only while the model is actually reading a suit below `PassPsi`** — `SuitCheckState.AnyFailed`, a new
    one-line property that is the fail branch's own printed question answered from the sim. Clean run, a
    run that has not bled yet, or no feed → dimmed and inert exactly as before. **Build lights the plate
    from the same call the glue gates the press on**, so a dimmed control cannot act and a live one cannot
    look unavailable — one verdict, one place.
  · **The press routes through S31's existing re-run path.** `ScreenPainter.StartSuitRun()` is now the ONE
    place a run begins (INITIATE / TRY ADDITIONAL TIMER / the TROUBLESHOOT repair all call it): countdown
    back to the top, no result yet, and a **fresh seed**, so the repair's re-run is **rolled** like any
    other rather than declared clean by the press.
  · **The countdown now PERSISTS at 0 once a run ends** (a painter field, not a local that sprang back to 5
    whenever the timer went idle). Without this the feature is unreachable on glass: the result box is
    modal, so TROUBLESHOOT can only be pressed after CLOSE — and on CLOSE the old code un-bled the leaking
    suit, so the table snapped back to four green "Nominal" and the control went dim again. HALT, a new
    run, or a page change put it back to 5; FINISH parks it at 0 for the same reason (the table has to keep
    agreeing with the verdict the crew was just shown).
  · `PanelMap.cs` and the label docs were **NOT** touched (§1.4 / C1.4).
  **Gate (C1.3):** `python plugin/build.py test` **green — 11347 checks, 0 failed**, with new coverage in
  `TouchWiringTest.SuitControls()` (the branch has an action; a failed suit makes it available; a clean run,
  an unbled run and a dead feed all leave it inert) and in `FigmaUINavTest.SuitLeakSimulation()` (the same
  three, plus: the failed table draws TROUBLESHOOT in `White` and a clean page in `Text6` — a new `ColourOf`
  helper, since "dimmed" is a claim only a colour can settle; the failed table still reads its verdict with
  the box closed; a repair mints a different run, the repaired suit is holding again at the top of it and
  the control goes back to inert; a later run that finds a leak lights it again, so the recovery repeats).
  **Preview inspected:** `ui_suitcheck.png` — resting, 5s, four live ΔP, four green Nominal, TROUBLESHOOT
  **dim**; `ui_suitcheck_leak.png` — **re-aimed by this task** at the state the crew actually acts in (leak
  found, result box closed): `0s`, SUIT 3 `0.01psi` + `Failed Low` in amber, three Nominal, and TROUBLESHOOT
  **lit white, matching TRY ADDITIONAL TIMER**. The leak RESULT BOX that file used to hold is preserved as
  the new **`ui_suitcheck_leak_popup.png`** (beside the clean `ui_suitcheck_popup.png`) — it was moved
  because an 82%-opaque scrim sits over the fail branch, so the one thing S32 changed cannot be judged
  through it. Committed locally (C1.5); NOT pushed.
- ⚠ **Logged, not done (C1.1):** see **S33** — `docs/SCREEN_INVENTORY.md`'s row for screen #5 predates S31
  and S32.

### S33 [S] `docs/SCREEN_INVENTORY.md`'s Suit Leak Check row predates S31/S32 — **DONE 2026-09-02** — [TIER 4: docs hygiene]
Noticed by **S32** while checking the docs for claims the code had just falsified (not fixed here — C1.1,
and S32's declared outputs were code + register only). Line 83 still describes screen #5 as
"per-suit DELTA PRESSURE + STATUS(**Nominal**)", which was true when the four status words were static
reference copy and has not been since **S31** made them a verdict computed off a marked simulation
(§14.4(e)). Line 125's "TROUBLESHOOT/TIMER display-only until the touch pass" is likewise now two steps
stale: T14 wired the timer and **S32** gave TROUBLESHOOT its owner-decided reconstructed action, live only
on a failed suit. Neither line is *wrong about the reference* — they describe what was built at the time —
but C7.1 says the older doc gets updated when the plan/code moves past it. **DONE when:** #5's row and the
#5 note read the way the page now behaves (STATUS = a computed verdict, ΔP live off real cabin pressure,
TROUBLESHOOT = live-on-failure repair-and-rerun, both marked reconstructions), with no other row touched
and no code change (so the build/preview gate is N/A per C1.3).

- **DONE 2026-09-02 — owner directive (via the overseer), widened to cover both suit-check doc surfaces
  S31/S32 left stale (S33's own scope was `SCREEN_INVENTORY.md` only).**
  1. **`docs/SCREEN_INVENTORY.md`** — row #5's Function/Status cells and the "NEW findings" bullet
     (the old "STATUS(Nominal)" / "TROUBLESHOOT/TIMER display-only until the touch pass" wording) both
     rewritten to state: ΔP + STATUS are a MARKED §14.4(e) simulation (ΔP = suit loop − real cabin
     pressure, four live readings; STATUS a computed verdict, never a hardcoded word), and TROUBLESHOOT
     is S32's live-on-failure repair-and-rerun, not display-only. No other row touched; `BUILD_PLAN.md`
     NOT edited (frozen per this task's scope).
  2. **`REGISTER.md`'s S18 glass checklist** (S18 itself stays **HELD** — only its checklist content
     changed, no gate opened, C1.12): (a) **added G10**, tagged S31/S32, covering the S32 TROUBLESHOOT
     lit-vs-dim affordance at IVA distance, the repair→rerun touch-flow end-to-end on the collider
     (CLOSE → TROUBLESHOOT → countdown restarts), and the S31 ΔP-magnitude legibility (~0.28 psi nominal,
     ~0.01 psi bled-down); (b) **trimmed G9** — dropped its now-stale "does the dimmed TROUBLESHOOT read
     as unavailable" sub-point (TROUBLESHOOT is no longer always dim; G10 now covers it), kept G9's
     docking-cluster PRECISE-label half intact.
  **Docs/register only, no code change → the preview/PNG gate is N/A (C1.3).** `python plugin/build.py
  test` run as a no-regression check: **green, 11347 checks, 0 failed** (unchanged from S32 — no code
  touched). Committed locally (C1.5); NOT pushed.

### S34 [O] QC-AUDIT sweep of the 2026-09-03 glass findings — **DONE 2026-09-03** — [TIER 2: real defect]
Owner-directed sweep (that chat), six findings brought back from the capsule. Audited each against the
code first, fixed the three that were real defects, and closed two as NOT defects with the source that
settles them. The sixth is systemic and is a `§1.4` source question, so it is logged as **S35** rather
than decided here (C1.12). PREVIEW-ONLY throughout — no `install`, no glass.

- **(3) SYSTEMS TREE vs ELECTRICAL POWER disagreed on the bus state — REAL, FIXED.** The tree read
  `MAIN BUS A/B` off the live `SystemsState` and said `BUS OFF`; the ELECTRICAL POWER tab hard-coded a
  green `"Nominal"`. Two surfaces, one truth, disagreeing (C7.1) — and `VehicleSystems.Fresh()` starts
  both buses **off**, so the tab was wrong the moment the page opened. `VehicleSubsystemPage.cs`'s two
  bus rows now read `SystemsState` through new `BusWord`/`BusKey` helpers that mirror `SystemsTreePage`'s
  own rule exactly: unpowered → `Off` (neutral), 3/3 → `Nominal` (go), 1–2/3 → `n / 3 Online` (caution),
  a powered bus with every string down → `0 / 3 Online` (alarm). That last case needed a fourth checklist
  colour key (`3` → `DragonPalette.Alarm`); keys 0–2 are untouched. Words are pre-built (`BusOnline3`),
  so the draw path still formats no strings.
  ✅ **CORROBORATED ON GLASS 2026-09-03**, in two frames twelve seconds apart: `20260903120456_1.jpg`
  (ELECTRICAL POWER) shows `MAIN BUS A — Nominal` and `MAIN BUS B — Nominal`, both green, while
  `20260903120508_1.jpg` (SYSTEMS TREE) shows `POWER 2 — BUS OFF` in grey with all three of its strings
  dashed. The same vessel, the same bus, the same minute, two screens, opposite answers. This is the
  finding exactly as reported, and the fix removes it.
- **(4) The Cover still read "altitude" while DeorbitBurnPrep read "attitude" — REAL, FIXED.** S13's
  residual. The two Crew Interrupt Conditions captions are baked community PNGs whose pixels say
  "altitude", so S13 could only correct `DeorbitBurnPrepPage`; the Cover kept disagreeing on glass.
  `CoverPage.cs` now **skips** those two asset keys (`AttitudeSkipKeys`) and redraws the rows as
  primitives carrying `DeorbitBurnPrepPage`'s own S13 strings — `30° sustained attitude error` /
  `600°/min attitude rate`. The asset KEYS and the PNG files are still untouched, per S13's rule; they
  are simply not placed. Geometry is read out of `Keys`/`Box` rather than re-typed, so the rows stay on
  their hairlines: measured in the render at 211→466 px with the leader starting at 485 (row 1) and
  211→411 with the leader at 421 (row 2) — no overlap, and the type matches the still-baked
  `FAR FIELD POINTING` beside it. **No asset re-render was needed, so no follow-up line is opened.**
- **(6) TEXT OVERFLOWING ITS CONTAINER — REAL, FIXED.** On the Cover's Reference Content phase the
  seven-step `ENTRY TIMELINE` overhung its card by 13 design units and rendered half on the card, half
  on the page ground. Cause: the three baked card slots are 317 / 449 / 550 tall and the densest list is
  in the **shortest** one. Fixed structurally, not by retyping one string: `CoverPage.FitRows` scales the
  row size and pitch by the same factor when a block does not fit its own slot, floored at
  `Typography.Min` and never letting rows overlap; the three cards now pass their measured slot bottoms
  (read from `Box`, one source of truth). Cards 2 and 3 already fitted and are byte-identical.
- **(2) SYSTEMS P&ID READOUTS "mis-wired" — NOT REPRODUCED, no defect found.** Traced end-to-end:
  `VesselData.cs:191-194` assigns each text field from its own `CabinReadout` member, and
  `SystemsPidPage.cs`'s READOUTS rows pair each label with that same field. `ui_systemspid.png` re-read
  after this task: LOOP A 26.4 °C · LOOP B 20.1 °C · CABIN TEMP 21.8 °C · CABIN PRESS 14.72 psia ·
  PPO2 2.86 psia · CO2 1.64 mmHg — correct, and every row nominal green. Nothing was changed.
  ⛔ **CORRECTED 2026-09-03, same day, after the owner supplied the glass screenshots.** The verdict
  above (no wiring defect) is right; **the reason this task gave for it was wrong.** It guessed the
  finding came from a stale DLL. It did not — see **S36**, now closed — and the real cause is worse:
  the readouts genuinely DO read mis-paired on the glass, because the value column sits ~920 design
  units right of its labels and the console is viewed obliquely, so the whole column reads about one
  row HIGH. The crew was reading the panel correctly; the panel is misleading at the IVA angle. That
  is a real legibility defect and is now **S38**. This task should have asked for the screenshots
  instead of theorising about the build.
- **(5) The "Changelog" CONNECTIONS row — NOT a mis-transcription, no change.** Unlike S19 this one is
  faithful: `docs/UI_AUDIT.md:310` (generated from the reference's own source) lists `Changelog` among
  the page's labels, and **both** reference copies agree —
  `assets/reference/dragon2-ui-vue/src/components/Overview.vue:9` and `…dragon2-ui-master/…:9`. Changing
  it would need a verified-real source we do not have (§1.4), so it stands.

**Gate (C1.3):** `python plugin/build.py test` **green — 11364 checks, 0 failed**, including 18 new
regression checks in `plugin/test/LayoutTest.cs` (`FitRows` fit/floor/no-overlap/proportion, an
end-to-end "no text crosses a card slot's bottom edge" guard over the built display list, the Cover's
attitude strings + the absence of the two baked captions, and a four-state tree-vs-tab bus agreement
check that compares what the crew SEES on each page). `python plugin/build.py preview` re-rendered and
**inspected**: `ui_cover.png` (both rows read attitude), `ui_cover_phase5.png` (all seven ENTRY TIMELINE
rows inside the card), `ui_vehiclepower.png` (`MAIN BUS A/B` → `Off`), `ui_systemstree.png` (`BUS OFF` —
the two now agree), plus `ui_systemspid.png`, `ui_vehiclecrew.png`, `ui_vehicle.png`, `ui_vehiclethermal.png`,
`ui_vehiclegnc.png` and `ui_deorbitburnprep.png` swept for further overflow — none found.
⛔ Per this task's scope, **no** flight-actuation no-op (§14.4(a)) or inert inferred control (§14.4(b))
was wired, and no `PanelMap`/label-doc was edited.
**Note on the working tree:** the S10b session's work was still uncommitted when this ran. This task
touched only `plugin/src/pure/CoverPage.cs`, `plugin/src/pure/VehicleSubsystemPage.cs` and
`plugin/test/LayoutTest.cs` — **no overlap** with S10b's six files — and the commit names those three
paths explicitly, so S10b's tree is left exactly as it was found.

### S35 [owner call] Gauge identity colours make a NOMINAL reading look like an alarm — **TODO** — [TIER 3: owner decision]
Logged by S34 (C1.1), not done — it needs an owner call, so S34 stopped and asked rather than repainting
reference-sourced elements on its own authority (C1.4 / C1.12).

**The finding was real, the diagnosis in it was not.** Glass reported "gauge colour-banding: nominal
values render in caution/alarm colours". There is **no threshold bug**. `CabinLimits` is correct and
physically sensible (`Ppo2Caution 2.5 / Alarm 2.0` descending · `Co2 4.0 / 6.0` · `Press 13.0 / 11.0`
descending · `CabinTemp 30 / 35` · `Loop 45 / 55`) and `Alarms.Band` handles both directions correctly —
`SystemsPidPage`, the one surface that *does* colour by severity, renders every one of those quantities
**green** at the same values that look red on the gauges.

**The actual cause is that the gauge arcs never consult severity at all — they are fixed identity
colours, and the reference's own choices collide with our state palette:**
- `assets/reference/dragon2-ui-vue/src/components/Overview.vue` sets `stroke: #d12c30` on the CABIN TEMP
  arc, `#d7b733` on PPO2, `#fcd533` on CABIN PRESSURE, `#2983ed`/`#2886f6` on CO2 and the loops. Ours are
  a faithful transcription (`DragonPalette.Gauge*`, `VehicleOverviewPage.cs:120`).
- `#d12c30` **is** `DragonPalette.Alarm`, byte for byte, and `#d7b733`/`#fcd533` read as
  `DragonPalette.Caution`. Everywhere else in the mod red means alarm and amber means caution, so the
  crew reads an alarm off a healthy cabin.
- ✅ **Seen on glass 2026-09-03, so this is not a preview artefact:** `20260903120439_1.jpg` (VEHICLE
  OVERVIEW) and `20260903120447_1.jpg` (CREW) both show PPO2 `3.00` gold, CABIN TEMP `22.4` **red**, CABIN
  PRESS `14.70` gold, CO2 `1.00` blue — a completely healthy cabin with a red gauge in the middle of it.
  `20260903120502_1.jpg` (GNC) shows `RCS FUEL 100` in gold, and `20260903120523_1.jpg` (THERMAL) shows
  `SHIELD 27 °C` in red.
- `VehicleSubsystemPage.cs` extends the same idiom to the reconstructed tabs, which is why THERMAL's
  `SHIELD` is red and GNC's `RCS FUEL` is gold at 100 % (`s.GCol` is a literal array on every tab —
  lines ~253 / 272 / 306 / 341 / 363 / 391 — never a severity).

**Paste-ready overseer prompt (C1.13):**
> DragonScreen, S35. A glass pass found that healthy cabin readings look like alarms: CABIN TEMP ~22 °C
> draws a RED arc on Crew and Vehicle Overview, PPO2 and CABIN PRESS draw amber, THERMAL's SHIELD draws
> red and GNC's RCS FUEL draws gold at 100 %. The build chat audited it and found the alarm thresholds
> are NOT wrong — the P&ID page, which is the one surface that colours by severity, shows all the same
> quantities green. The gauge arcs simply never look at severity: they use fixed per-gauge identity
> colours transcribed faithfully from the reference UI's own CSS, and the reference happens to have
> picked `#d12c30` for CABIN TEMP — the exact hex this mod uses for ALARM — plus two yellows that read
> as CAUTION. So the collision is between the reference's decorative palette and our state palette.
> Nothing has been changed; the chat stopped here because fixing it means deviating from the reference,
> which is a C1.4 §1.4 source decision only the owner makes. The options:
> **(a) Leave it.** The reference is the source of truth and the colours are its own. Cheapest, but the
> crew keeps reading a red gauge on a healthy cabin, and it contradicts S22's "don't read confidently
> when you shouldn't" direction.
> **(b) Severity wins.** Drive every gauge arc from `Alarms.Band` — green nominal, amber caution, red
> alarm — and retire the identity colours. Most legible and internally consistent; the biggest visual
> departure from the reference, and it loses the at-a-glance "which gauge is this" cue.
> **(c) Keep identity colours, but re-pick the clashing ones.** Move CABIN TEMP off `#d12c30` and the two
> yellows off the caution amber, onto hues that are not in the state palette (the reference's own blues,
> say), so red and amber mean only one thing anywhere on the glass. Keeps the reference's *idea* — a
> per-gauge colour — while removing the false alarm. Needs the owner to pick the replacement hues.
> **(d) Identity colour normally, severity colour on exceedance.** The arc keeps its reference hue while
> the value is nominal and switches to amber/red only when `Alarms.Band` says so. Preserves both cues;
> the most code, and a red CABIN TEMP would then mean two different things depending on the value —
> which is the confusion this is trying to remove.
> Options (b), (c) and (d) all change reference-sourced pages, so each needs an explicit `OVERRIDE` plus
> a plan/register edit before a build chat may act (C1.12). Which one, and if (c), which hues?

*(The "was glass even running a current build?" half of this — S34's other open question — was split out
to **S36** by owner directive, 2026-09-03, so the two can be decided separately.)*

### S36 [owner call] The 2026-09-03 glass pass ran on the S17 DLL — re-baseline before the next findings? — **CLOSED 2026-09-03: THE PREMISE WAS FALSE** — [TIER 3: owner decision]
Logged by S34, split out of S35's prompt by owner directive. **Closed the same day, unasked, because the
owner supplied the glass screenshots and they disprove it. No owner decision is needed.**

**What this line claimed:** that the glass session was running the S17 DLL (2026-09-02), which predates
S19–S33, and that this explained why one of the six findings did not reproduce.

**What the screenshots show** (`Steam\userdata\...\220200\screenshots`, 38 frames, 12:02:48–12:06:13 on
2026-09-03 — the owner pointed here; they are evidence handed over, not a build source, so C7 is intact):
- **THERMAL CONTROL** labels its two coolant gauges `LOOP A` and `LOOP B`. That is **S20** (2026-09-02),
  which landed AFTER S17. The reference labels both `LOOP A`; only the post-S20 build says `LOOP B`.
- **MANUAL DOCKING** draws the PYR block as per-axis RATE with no fixed target diamond. That is **S26**
  (`d8e718b`) — the HEAD commit immediately before the QC-audit session.
- **The Suit Leak Check** shows S31/S32's popup, verdict word and TROUBLESHOOT affordance.

So the glass was running a build **current through at least S26**, i.e. everything S17 through S33. The
"stale DLL" theory was invented to explain a finding that did not reproduce in preview, and it was wrong.
**The correct explanation is S38.** Nothing about the install cadence needs changing, and the option (c)
"install-then-look as a standing rule" this line proposed is unnecessary — the owner already did exactly
that.

**The lesson, which is the part worth keeping:** a build chat that cannot reproduce a glass finding should
**ask for the screenshots**, not theorise about which DLL was running. The evidence existed the whole time.


### S38 [O] Label→value rows read ONE ROW OFF on the glass — the console is viewed obliquely — **DONE 2026-09-03** — [TIER 2: real defect]
Logged 2026-09-03 from the owner's glass screenshots. **This is what QC finding 2 actually was.** S34
closed that finding as "not reproduced" against the preview PNG and was right about the CODE and wrong
about the CREW: the pages are correctly wired, and they are still misread in the capsule.

**The defect.** `SystemsPidPage`'s READOUTS column draws each label at `rx = 2150` and its value
right-aligned at `3070` — **920 design units apart**, with nothing joining them. On the glass the console
is a tilted quad, so a row that is horizontal in the RenderTexture is a sloping line in the crew's view,
and the value column lifts relative to its labels. Measured on `20260903120540_1.jpg`: row pitch ~25 px,
value column displaced ~24 px upward — almost exactly one row. The crew reads:
`CABIN TEMP 14.70 psia` (the pressure) · `CABIN PRESS 3.00 psia` (the ppO2) · `PPO2 1.00 mmHg` (the CO2)
· `CO2` blank, with the first value stranded on the `READOUTS` heading.
**The units travel with the values, which is the proof it is not a wiring bug:** the code draws each unit
at its LABEL's y and each value 4 units above, so a data mis-pairing could not carry `psia` up onto the
CABIN TEMP line. A rigid displacement of the whole right-hand column can, and does. Cross-checked against
the same frame's own diagram boxes — CABIN 14.70, SUIT LOOP 3.00, CO2 SCRUBBER 1.00, RADIATOR A 27.1,
RADIATOR B 20.4 — every one agrees with the TRUE pairing and none with the apparent one.

**It is not just the P&ID.** The Cover's `Crew Interrupt Conditions` shows the same thing at a gentler
angle (`20260903120255_1.jpg`): both `FAR FIELD POINTING` values sit ~14 px above their labels on a 21 px
pitch, so the first value floats above the first label. Anything built as "label left, value far right,
nothing in between" is exposed. **This is a legibility class, not one page** — audit for it, do not fix
one instance.

**Why preview never caught it, and what that means for the harness:** `build.py preview` renders the
panel flat and square-on, so the rows align perfectly and always will. **No PNG check can find this
defect.** That is the interesting part: it is the first class of defect in this project that is invisible
to the preview gate by construction, and the C1.3 gate should probably say so.

**Fix direction (not decided — whoever takes this picks, and it is a design call):** bind label and value
into one visual row rather than trusting horizontal alignment across a wide gap — a leader line that
actually spans the gap (the Cover has short ones and still misreads, so they must reach), a banded or
boxed row, or simply moving the value column in beside its label. The last is cheapest and most robust
and costs nothing but a layout change to a page that is ours, not the reference's.

- **DONE 2026-09-03. The owner chose "move the value column in beside its label"**, and that is what was
  built. First the CLASS was surveyed rather than the one page fixed (this line's own instruction): a
  headless pass over all 35 `UiPage`s plus the three live screens, pairing each Left-aligned text with a
  Right-aligned one emitted within three commands on the same row, and reporting the worst span as a
  multiple of the row's type size. **The survey is what set the scope** — it showed the pattern is real
  and widespread, and it separated the genuine multi-row blocks from caption/badge pairs that happen to
  sit on one line.
- **The insight that scoped it:** the defect only bites where rows are **STACKED**. A lone caption pair
  (`RESOLUTION — 640 x 360` under the video box) has no neighbouring row to be confused with, however
  wide its gap. Three stacked blocks were fixed, worst span before → after:
  - **`SystemsPidPage` READOUTS** — the block the glass actually caught — **38× → 11×** (920 design units
    → 280). Values stay RIGHT-aligned so the digits still line up and the column is still scannable.
  - **`DeorbitBurnPrepPage`'s five SLEW rows** — the widest span in the build — **105× → 17×** (2747 →
    460). The label sat at the far left of the card and the value at the far right of a 3427-wide page.
  - **`VehicleSubsystemPage`'s detail rows** — **24× → 16×** (600 → 400). One helper, so this lands on all
    six subsystem tabs at once.
- **Pinned by 6 new checks in `LayoutTest.cs`.** They assert the worst label→value span per page as a
  multiple of the type size, so the guard scales with the panel and catches a regression on any of the six
  tabs, not just the one that was edited. ⛔ **The comment on those checks says the important part: no PNG
  check can ever find this defect** — `build.py preview` renders the panel flat and square-on, so these
  rows align perfectly there and always will. It is the first defect class in this build that is invisible
  to the preview gate by construction.
- **Gate:** `python plugin/build.py test` green (11483 checks, 0 failed — 306 in the layout suite, up from
  300). `preview` re-rendered and the three fixed blocks INSPECTED: `ui_systemspid.png` now reads as a
  tight two-column list, `ui_deorbitburnprep.png`'s SLEW block is compact instead of spanning the card,
  `ui_vehiclepower.png`'s detail values sit in over their own bars.
- ⚠ **HONEST RESIDUAL, carried to S39.** Moving the column in cannot fully cure a block with RAGGED label
  lengths: `DeorbitBurnPrep`'s column is set by `MAXIMUM ATTITUDE RATE`, so short labels (`ROLL`, `PITCH`,
  `YAW`) still have ~300 design units of empty space to their values — and that block's row pitch is only
  40 units, the tightest in the build, so the residual displacement is still a real fraction of a row.
  Distance alone does not finish this one; it wants the leader line this line listed as its other option.
  That, and the blocks the survey flagged but this task did not touch, are **S39**.

### S39 [O] Finish the S38 sweep — the blocks distance alone does not fix — **TODO** — [TIER 3: scheduled polish]
Logged by S38, 2026-09-03. S38 fixed the three worst stacked label→value blocks by moving the value column
in, which was the owner's chosen remedy, and pinned them. Its own survey named what is left. **Numbers below
are the worst span on that page as a multiple of the row's type size, measured after S38 landed** — rerun the
survey rather than trusting them if the pages move.

**Genuine remaining candidates:**
- **`Pages.SideRow`, the FLIGHT screen — 44×, and NINETEEN stacked rows**, the largest count anywhere. It
  already draws a 1 px rule under each row, which is a real connector and is why it was not top of the list,
  but 19 tightly-stacked rows at 44× is the biggest remaining exposure.
- **`VehicleOverviewPage`'s CONSUMABLES table — 28×.** Three columns (CONSUMABLE / QTY / MARGIN), so it is
  the one case where the value genuinely cannot move all the way in; it wants column rules or banding.
- **`NavOrbitPlotPage` — 22×** and **`VehicleMechPage` — 18×.** Both mild, both cheap.
- **`DeorbitBurnPrepPage`'s residual** — see S38: ragged labels plus a 40-unit row pitch. This is the one
  that actually needs the LEADER LINE rather than more distance.

**Not defects, do not "fix" them** (the survey flags them, they are not stacked label→value rows):
`SettingsVideoPage`'s `RESOLUTION` caption under the video box (80×, a lone row with nothing to confuse it
with) · `VehiclePropulsion`'s section caption + `RCS DISABLED` page badge on one line (92×) · the NAV
screen's `GROUND TRACK` / `TRACKING VEHICLE` pair (110×) · `ManualChute`'s heading + badge (16×).

**Also worth deciding here:** whether the C1.3 gate wording should say out loud that a PNG preview cannot
see this class, so a future task does not read "preview inspected" as "legible in the capsule". S38's test
comment says it; the protocol does not.

### S40 [S] The RSS "no usable scaled-space map" warning floods KSP.log ~450×/flight — **DONE 2026-09-03** — [TIER 2: real defect]
Logged and fixed by the owner-directed flight-surfaced screen-bugs pass, 2026-09-03 (finding **A**). Evidence:
the owner's KSP.log from that day's in-orbit flight carries
`[DragonScreen] no usable scaled-space map for Earth on shader 'Custom/HapkeScaled' - NAV draws the grid and
track only. Texture slots: _MainTex=4x4, _BumpMap=null, ..., _Skybox=4096x4096, ...` about **450 times, once
per frame, for the whole flight**.
- **The mechanism, and why the obvious fix would have been wrong.** `ImageStore.BodyMap` caches the map on
  `ReferenceEquals(b, mapBody) && mapTexture != null` — so a body whose map does NOT resolve is never cached
  and the whole lookup re-runs every frame. **That retry is deliberate and correct** (scaled space can be
  built late by Kopernicus; the body changes on an SOI transition), so it is untouched. What was wrong is
  that the retry also **re-said its diagnosis**, and re-built the string to say it with — a per-frame
  `GetTexturePropertyNames()` plus concatenation in the draw path.
- **The fix.** New pure `plugin/src/pure/LogGate.cs`: a seen-set of string keys, `First(key)` true once.
  `BodyMap`'s warning is now `else if (LogGate.First(MapFailKey(b.bodyName, shader)))`, so it is said **once
  per body+shader** — a new body, or the same body after a shader swap, is a new diagnosis and IS heard. The
  slot-enumeration is built INSIDE the gate, so silent frames also allocate nothing. The `catch` branch's
  `body map lookup failed` was flooding by the same mechanism and is gated the same way, keyed on
  body+exception type.
- **Why the gate is pure.** `ImageStore` is glue and cannot run headless — it needs `FlightGlobals` and a
  `Material`. The RULE ("same key once, different key again") is decidable with the game closed, so it lives
  in `pure/` and is tested there; the glue keeps the `Debug.LogWarning` and the gate never knows what a log is.
  A single bool, the `PanelButtons.PickColourProperty` pattern, would have silenced the SECOND body for the
  wrong reason — `LogGateTest` checks exactly that case.
- **Gate (C1.3):** `python plugin/build.py test` **green**, all suites, 0 failed — new `LogGateTest` suite,
  14 checks, including the 450-frames-one-line case as it actually happened. No visual change → the PNG gate
  is N/A for this finding (the previews for S41 were re-rendered and inspected in the same session). No
  `install`, no glass (C1.12). ⚠ **It cannot be confirmed on the glass until the next flight** — the proof is
  that the next KSP.log carries that line ONCE. Batched onto **G12**, which needs to read that single line
  anyway.
- **Files:** `plugin/src/pure/LogGate.cs` (new) · `plugin/src/ImageStore.cs` · `plugin/test/LogGateTest.cs`
  (new) · `plugin/test/TestMain.cs` (registration).

### S41 [O] The ORBIT plot forced a closed ellipse through a sub-orbital trajectory — **DONE 2026-09-03** — [TIER 2: real defect]
Logged and fixed by the same pass (finding **B**). The owner's 2026-09-03 flight was **sub-orbital for the
first several minutes of ascent** — negative periapsis, the trajectory not yet closed — which is what every
ascent looks like until circularisation, and the ORBIT view had never been previewed in that state.
- **CONFIRMED OURS, not KSP's map.** The brief asked for this to be established first. `NavPage.Orbit` is
  pure and deterministic, so it was settled by building the fixture and rendering it BEFORE touching
  anything. The pre-fix PNG is unambiguous and worse than predicted: the ellipse is drawn **entirely inside
  the globe**, the `PE` box sits near the planet's core, and the vehicle marker — 148 km ABOVE the ground —
  is painted underneath its own planet. Nothing about that comes from KSP or MechJeb; it is this function
  assuming the conic closes. (The weird in-game map ascent trajectory the owner flagged as MechJeb PVG
  re-planning was **not** chased, per the brief.)
- **The rule.** The plot now draws only the part of the conic **at or above the surface**. With
  `p = a(1-e²)`, `r(ν) ≥ R` exactly when `cos ν ≤ (p/R - 1)/e`; that one threshold decides everything —
  `≥ +1` the periapsis clears the surface and the orbit closes (draw all 360, unchanged); in `(-1, +1)` an
  **open arc through apoapsis**, from `+νSurf` round to `-νSurf`, both ends included so it visibly touches
  the limb; `≤ -1` nothing clears the surface, so no arc is drawn. The globe is drawn from the SAME radius
  at the SAME focus, so the arc's ends meet the drawn limb exactly rather than approximately.
- **`PE` is drawn only where PE exists**, and that is the deliberate part. On an open trajectory the
  periapsis is underground, and a box labelled PE inside the planet is the same lie in miniature — it is
  dropped, not moved and not relabelled. The readout column already dashes PERIGEE in this state for its own
  reason (`OrbitReadout` rejects a periapsis that far below the surface as the body-radius artefact), so the
  page says one thing twice rather than two different things. **On a deorbit the two rules deliberately
  differ**: `PeA = -30 km` is a target worth printing, and the point is still underground, so the number
  shows and the marker does not. The plot's rule is geometric; the readout's is editorial.
- **The cut is captioned** — `TRAJECTORY INTERSECTS SURFACE`, in the same slot and weight as
  `ON SURFACE - NO ORBIT`, and in wording that is true of an ascent and a deorbit alike so no phase is
  claimed; `NO TRAJECTORY ABOVE SURFACE` for the degenerate near-vertical case, where the vehicle tick is
  also skipped rather than placed wherever a clamped `acos` fell.
- **Gate (C1.3):** `python plugin/build.py test` **green**, all suites, 0 failed. **The new test was proved
  to catch the bug**: `NavPage.cs` was reverted alone and `PageTest` went to **11 failures** — the ascent
  arc's nearest dot was **0.074 body radii** from the planet's centre, the deorbit arc's **0.95** — then
  restored to green. The closed-orbit checks passed against BOTH versions, which is the evidence that the
  fix costs the case that already worked nothing. `python plugin/build.py preview` re-rendered and inspected:
  `page2_nav_orbit.png` (closed) unchanged; new `page2_nav_orbit_suborbital.png` is the flown RSS case; new
  `page2_nav_orbit_suborbital_kerbin.png` shows the arc rising out of the limb, over apoapsis, and back into
  the planet, AP on the arc and no PE. No `install`, no glass (C1.12).
- ⚠ **One thing the RSS PNG shows that is NOT this defect:** at RSS scale a 210 km trajectory over a 6371 km
  radius is a **hairline on the limb** — honest, and nearly unreadable. That is a property of the plot's
  fit-the-whole-orbit-plus-the-body scale rule, and the CLOSED orbit the flight ended in draws the same
  hairline ring. Logged as **S43**, not fixed here (C1.1).
- **Files:** `plugin/src/pure/NavPage.cs` · `plugin/test/PageTest.cs` (`OpenTrajectory`, +23 checks) ·
  `plugin/preview/PreviewMain.cs` (two scenes).

### S42 [owner-gated] The RSS scaled-space globe: `Custom/HapkeScaled` defeats the body-map lookup — **HELD** (`/next` SKIPS it; build-then-verify-on-glass) — [TIER 5: held / owner-action / Part-B-bound]
Logged by the same pass (finding **C**), and **deliberately NOT claimed as fixed** — the brief said escalate,
and the escalation is right. Evidence is S40's log line: under RSS the planet wears
**`Custom/HapkeScaled`**, whose texture slots are not the stock ones, so `ImageStore.BodyMap`'s
`_ColorMap` / `_MainTex` / `mainTexture` list finds nothing usable (`_MainTex` is a 4×4 stub, correctly
rejected by `MinMapPixels = 64`) and the NAV **MAP** view — plus the strip-textured globe under **3D PLANET**
— fall back to grid + track with no planet on them.
- ⛔ **WHY NOTHING WAS BUILT HERE, AND THIS IS THE POINT.** The brief allowed a best-effort source-and-fallback
  *only if it could be done without guessing*. It cannot, from inside the repo:
  - The one slot name that would settle it is in the elided middle of the owner's log excerpt
    (`..., _BumpMap=null, ..., _Skybox=4096x4096, ...`). The **full** line has it; this chat does not.
  - The obvious generic fallback — "no known slot matched, so take the biggest usable texture on the
    material" — is a **guess, and the log proves it is a dangerous one**: the biggest texture on that
    material is `_Skybox` at 4096×4096, so a size-ranked fallback would paint the NAV map with the
    **skybox**. That is worse than the honest grid, and it would look plausible enough to ship.
  - C7 forbids the two places the answer lives outside the repo (the KSP install; external URLs), so the
    remaining legitimate source is **the flight log itself** — which is exactly why G12 asks for it.
- **What §2 predicts, and it may make most of this moot.** A scaled-space **camera** renders whatever shader
  the body is wearing and never asks for a texture slot at all. `src/ScaledPlanetRenderer.cs` is already
  written (S10b) and does exactly that. So the **3D PLANET** view may already be immune to this under RSS,
  while the flat **MAP** quad — which genuinely needs a bitmap — is not. Testing that is one look at one
  screen, and it is folded into **G12**.
- **DONE when:** the full `Texture slots:` list from a real RSS flight is written into this line; and either
  a named, verified slot is read from it (a source, not a guess) and wired with the same `Usable` guard, or
  it is established that no slot on that shader carries a colour map — in which case the honest answer is the
  MAP view saying so, and the 3D PLANET camera being the route that works.
- ⛔ **Needs a separate, explicit owner `install` + glass go**; the standing go is preview-only and this line
  neither grants nor inherits one (C1.12). **Batched onto S18's glass checklist as G12**, and it should ride
  the same visit as **G11** because both are answered by opening the NAV page once, in orbit.

### S43 [S] The ORBIT plot is a hairline when the orbit is small against the body (RSS LEO) — **TODO** — [TIER 3: scheduled polish]
Logged by S41, 2026-09-03, from its own preview. `NavPage.Orbit` fits **the whole orbit AND the body** into
the panel — deliberately, and for a good reason recorded in the source: leaving the body out of the extent
once blew a 790 px globe into a 520 px panel on the pad. The cost shows up at RSS scale: a 200 km orbit over
a **6371 km** radius is 3% of the globe, so the ring (or, sub-orbital, the arc) is a **hairline hugging the
limb**, with the AP box, its label and the vehicle tick all piled on top of each other. Compare
`page2_nav_orbit_suborbital.png` (RSS — correct and nearly unreadable) with
`page2_nav_orbit_suborbital_kerbin.png` (same geometry, 600 km radius — perfectly clear).
**Not a correctness bug** — the plot is telling the truth, and it is the truth that is thin — so it is polish,
not TIER 2. **Worth noting it is the ZOOM control's natural job:** the NAV page already has `ZOOM ×1` with
`-` / `+` beside this view, and `MapView` already carries a zoom step the MAP view uses; the ORBIT view
currently ignores it. Wiring the existing control, rather than inventing a new scale rule, is the cheap
option and probably the right one.
**DONE when:** an RSS-scale LEO orbit is legible on this plot — the ring/arc separated from the limb and the
apsis markers not overlapping it — with the closed Kerbin-scale case unchanged, judged on both preview PNGs.
