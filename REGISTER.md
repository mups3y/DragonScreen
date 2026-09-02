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
the owner's call) and on **S10** (not verifiable until it is BUILT); each needs its own fresh owner go. **T2–T4 are covered retroactively by this go** — they are on-plan and
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

### S11 [S] `plugin/build/csc.rsp` is a generated file, tracked, and churns on every build — **TODO**
Logged by T4 (C1.1), not done. `build.py` overwrites `plugin/build/csc.rsp` on every invocation with
whichever compile ran last, so the file shows as modified after any `test` / `preview` / `install` and
its content depends only on which command was run most recently — it carries no information worth
versioning. It is also written with CRLF into a repo whose `.gitattributes` mandates LF, so git warns on
every touch. T4 restored it to HEAD (`git checkout --`) so the commit carries only real changes.
**DONE when:** it is gitignored (and untracked), or build.py writes it outside the repo.

### S12 [S] `VehicleMechPage`'s subsystem tab bar isn't severity-aware — **TODO**
Logged by T5 (C1.1), not done. T5 gave `VehicleTabBar` a `Severities(PageState)`-driven `Draw` overload
so a faulted subsystem's tab reads red from every vehicle page (`VehicleOverviewPage.cs`,
`VehicleSubsystemPage.cs`) — real signals per §14.4/§1.4: `Alarms.LifeSupport`/`Thermal`/`Low`/
`FdirSeverity`. `VehicleMechPage.cs` was out of T5's declared scope (register line names only
`VehicleOverview`/`SubsystemPage`) and still calls the old 4-arg `VehicleTabBar.Draw(dl,w,h,active)`, so
its own tab bar always reads nominal even when another subsystem is genuinely alerting. **DONE when:**
`VehicleMechPage.Build` passes `PageState` through and calls
`VehicleTabBar.Draw(dl,w,h,3,VehicleTabBar.Severities(s))`, with a preview showing it turn red to match.

### S13 [owner call] "altitude" vs "attitude" in Crew Interrupt Conditions / Slew for Deorbit Burn — **TODO**
Logged by T7 (C1.1), not done. Every source that names the deorbit-burn interrupt/slew criteria says
"altitude" — SCREEN_INVENTORY.md's photo transcription, SCREENS_LOOK_AND_FUNCTION_RESEARCH.md's read of
the community recreation's First.vue source, and the community Figma's own baked layer name
(`600deg_m_altitude_rate`, `CoverPage.cs` Keys). "30° sustained ALTITUDE error" and "600°/min ALTITUDE
rate" don't parse physically (altitude is a distance, not a rotational quantity), whereas "ATTITUDE
error/rate" would — paired with Roll/Pitch/Yaw and an autopilot SLEW-interrupt trigger, "attitude" is
what the criteria are almost certainly measuring. Not corrected unilaterally (C1.4: never edit a
real-sourced label without a real-source confirmation) — kept verbatim in `DeorbitBurnPrepPage.cs` and
its comments so nothing in the codebase disagrees with the already-shipped `CoverPage.cs` wording.
**DONE when:** the owner confirms which word the real screen actually shows (or that the community
Figma really typed "altitude"), and the docs + `CoverPage.cs` asset-key comment + `DeorbitBurnPrepPage.cs`
are updated together if it is "attitude".

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

### S15 [O] The circular nav / orbit plot (SCREEN_INVENTORY #28) is still unbuilt and unowned — **TODO**
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

### S16 [S] `SCREEN_INVENTORY.md` + §3 status marks are stale after T9 — **TODO**
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

### S18 [owner-gated] End-of-Part-A glass pass — **HELD** (deferred here by owner directive 2026-09-02; `/next` SKIPS it)
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

**Batch into this pass whatever else is glass-only by then** — the obvious candidate is **S10**'s RT planet
camera, but ONLY if S10 has actually been BUILT first (it is a TODO, not a verification; see its line). Any
T13/T14 criterion that turns out to need the capsule belongs here too rather than in its own visit — that
is what this line is for.

**DONE when:** both re-checks are confirmed on glass, or a NEEDS-WORK note says which way each is still off
and what the next step would cost.
