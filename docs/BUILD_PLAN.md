# DragonScreen — Crew Dragon Build Map & Roadmap

## 0. Plan map & status (consolidated 2026-09-02; gate updated 2026-09-03)
Two workstreams, both fully PLANNED. **Gate state — PREVIEW-ONLY BUILD-GO (owner, 2026-09-02, granted via the
overseer; supersedes the blanket BUILD-HOLD this banner carried until then), EXTENDED TO PART B (owner,
2026-09-03, granted via the overseer and confirmed in-chat):** pure code + `build.py test` + `build.py preview`
are cleared for **Part A AND Part B**. **The Part-B build gate is OPEN** — **T15 onward** (the pinned,
privately-namespaced MechJeb embed, GPLv3 per §B2/§B3/§B12.1, and the conductor) is **GO**, to be built at
**RSS-RO DEFAULT settings as the baseline to tune from** (§B5's "begin from RO defaults"); the
one-parameter-at-a-time **fine tune is DEFERRED until after the first recorded flight**. `build.py install`
and glass time are **NOT** covered by either go: they remain **SEPARATE owner gates, granted per session**, so
a task whose done-criteria can only be met in the capsule (the Part-B "in-sim" criteria included) **stops and
asks** rather than installing. `REGISTER.md`'s banner is the live copy of this rule, and **only the owner opens
or widens it (C1.12)** — a build chat never self-authorizes one.
- **PART A — Screens** (§1–13): the screens-only Crew-Dragon IVA UI. Research COMPLETE (§11/§13); build order
  §7; lower analog panel §4; capsule turntable §5; live-data/touch §6. ~18 pages built (owner-provisional).
- **PART B — MechJeb autopilot core** (B1–B16, incl. B6 risks): reintroduce flight software as an embedded,
  pinned, privately-namespaced MechJeb driven by a "conductor". Research COMPLETE (how-to-tune B7–B10 +
  flight-data targets B11); build architecture DESIGNED (B12); abort / crew-gate / FDIR researched (B13–B15);
  **coherence pass + source-tier map done (§14); all 4 tier-3 "invention" clusters RESOLVED with the owner
  (§14.4)** — the plan is DECISION-COMPLETE, and **Part B is now GO** (owner, 2026-09-03 — see the banner
  above), built from RO defaults. **§B16 (owner scope addition, 2026-09-03) folds in Falcon-9 booster
  recovery** — a SEPARATE-VESSEL autopilot, distinct from the Dragon conductor.
  **Amended 2026-09-03 by G5a** with the owner's settled decisions on booster recovery and direct control:
  **§B12.7** (direct part control — no staging, no action groups, binding on ALL of Part B), **§B12.8**
  (Part B starts from RECOVERY — four dependency-ordered waves over the 103 RECOVER-CODE files R1 inventoried),
  **§B12.1a** (T15 = a full MechJeb port, headless, full settings authority), **§B8** (autostage OFF —
  prediction stays, actuation is the conductor's), **§B13.4** (direct SuperDraco commanding), and
  **§B16.1/§B16.4–§B16.9** (our own booster core and steering law · the craft dump is in the repo and the
  engines bind by `engineID` · the guidance decision SETTLED · the focus protocol · un-converged constants ·
  Kerbal-Konstructs landing zones). **Further amended 2026-09-03 by G5b** with the two-profile split (§B5),
  the MechJeb-repository + ASDS-boostback resolutions (closing G5a-Q1/Q2), and three more settled Dragon-
  mission decisions: **§B10.3/§B12.3/§B9 P4** (auto-dock default, O6), **§B9 P8/§B10.5** (entry attitude-hold
  baseline, no bank, O8), **§B8/§B10.6** (MechJeb auto-throttle over PVG bang-bang, O9).
  **Amended 2026-09-04 by W11 then G6** — §B12.8's recovery shape. W11 added **Wave E** (rider (c): nine
  register lines for the glue files no wave owned). **G6** then applied the owner's **upper-stage / booster
  split** of 2026-09-04 (*"we use MechJeb for ALL UPPER STAGE MANOEUVRES as planned. BOOSTER SCRIPTED."*) as
  **§B12.8 rider (d)** — which re-verdicts **five** Wave E lines RECOVER-CODE → **RECOVER-REFERENCE** (read
  and mined, never made live), leaving Wave E at **four code + five reference** — and wrote **§B12.5a**, the
  one account of how a facade property goes from constant-false to live and which increment owns each. That
  split **RESTORES** §B1/§B9's original division of labour rather than changing it.
  Open items are at the end of this document under **"Open questions for the owner"**.
Execution is governed by **PART C** — the anti-drift harness (a rules→one-task→verify→register LOOP, run by a
`/next` skill + `CLAUDE.md`; Opus-for-hard / Sonnet-for-routine; one task per fresh chat). First task = T0
(scaffold the harness), then T1 (docs sync) onward. Gate per the banner above; each task commits LOCALLY with
`git commit` and never `git push` (C1.5) — the owner pushes from GitHub Desktop.

## 1. Context & status
DragonScreen is a screens-only KSP mod recreating the Crew Dragon IVA touchscreens + lower analog console.
This is the consolidated **build map** and execution roadmap, built from a deep research pass (designer
portfolio, Discovery capsule frames, iss-sim, DillonBaird recreation + full article, BBC explainer, NASA
press kit + mission timelines). Full screen detail: `docs/SCREEN_INVENTORY.md`; visual status board: the
published "Dragon Screen Map" artifact.

**PLAN ACCEPTED by owner (2026-09-01) — NOT READY TO BUILD YET.** Research gate is CLEARED (public sources
exhausted; the map below is the agreed build base; remaining gaps are label-level only per §13, carried as
caveats). The owner has accepted this plan but explicitly is **not ready to start building**. So we HOLD in
planning: no mod code, no `install`, no glass time, and no documentation sync to `SCREEN_INVENTORY.md`/the
artifact — nothing executes until the owner gives an explicit build-go (and names the first §7 item). The
~18 already-built pages are the baseline; label-level details stay reconstructed.
**Superseded 2026-09-02 (owner, via the overseer):** the hold above is lifted to a **PREVIEW-ONLY BUILD-GO**
per the §0 banner — pure code + `test` + `preview` are cleared (and the docs/inventory sync was done by T1);
`install` + glass time remain gated on a separate explicit owner go.

**Owner decisions locked:**
1. Capsule render = **turntable drag-rotate** (§5), not a front/rear toggle.
2. Build order = **most-complete-reference-first**, working down to inferred pages (§7).
3. **No install / no glass time** until research is called complete.
4. **Source-of-truth hierarchy (owner, 2026-09-02) — governs EVERY element, Part A AND Part B, screens AND
   flight behaviour:** (1) VERIFIED-REAL Crew-Dragon design/layout/functionality/assets are used FIRST;
   (2) where an element cannot be COMPLETELY verified, fall back to OTHER USERS' recreations/designs/assets/
   elements (DillonBaird, iss-sim, Tundra's IVA model, community Figma, the JSC imagery — each MARKED as such);
   (3) ONLY where there is no real evidence AND no existing asset for an element is invention permitted — and
   invention is a **JOINT owner discussion, never unilateral**. (Supersedes the older "infer as a marked last
   resort" wording; the same three tiers apply to the coherence pass §14.)
   **EXTENDED 2026-09-02 by §14.4(e)** (simulation-for-immersion): a not-yet-modelled real quantity goes to an
   installed mod's value, else a COHERENT MARKED simulation — a dash ONLY where the quantity truly does not exist.
   **AMENDED 2026-09-03 by §14.4(f)** (completeness + simulate-to-fill): for READOUTS the dash-last-resort above
   is SUPERSEDED — every real-screen feature is included and filled, live source first, else a coherent MARKED
   simulation that behaves live; a dash only for a genuinely-absent state. Actuation is unchanged (§14.4(a)).

## 2. How we build (principles — all confirmed adoptable for our pipeline)
Our renderer is pure C# `DisplayList` draw-commands → two must-agree renderers: `ScreenPainter` (in-game GL)
and `preview/PreviewMain` (GDI+ PNG). Author in the 3427×2112 Figma frame. These principles (from the real
SpaceX stack + DillonBaird's article) match that pipeline:
- **Stateless UI over a "truth" state.** The real touch UI is stateless — it *commands + monitors*; the
  triple-redundant flight computer is the truth. So our pages must **render live vessel state, never own or
  invent it**. Today's representative gauge constants are placeholders, not design → see §6 live-data.
- **Parameterized, reusable components; no static data/image paths; no UI libraries.** Confirmed at source:
  the flown screens are **Chromium + a custom in-house JS component library** (SpaceX's Sofian Hnaide),
  flight software in C++. (Baird's Vue/THREE/LIT/WASM/Electron was *his* recreation, not SpaceX's — ignore.)
  Our equivalent = pure primitives + named PNG assets + shared helpers (`CoverPage.DrawRail`, `VehicleTabBar`,
  `NavPage.Planet`, `ImageStore`). Keep it.
- **Test that "data shown == data provided."** Our analogue: the headless suites (esp. `FigmaUINavTest`) +
  preview-PNG inspection. Extend both per new page.

## 3. The screen map
Real UI ≈ 25–30 pages (Doug Hurley). Status of every page we know of:

| Screen | Status | Best reference | Notes / what it needs |
|---|---|---|---|
| Cover / Deorbit Dashboard | BUILT* | Figma Frame 67 + photos | 7-item phase rail, live globe, orbit telemetry |
| Attitude / Docking HUD (Frame 58) | BUILT* | Figma Frame 58 + photos | values not yet live |
| Vehicle Overview / All | BUILT* | demo + ui1 photo | right column should become CONSUMABLES (§ refine) |
| Vehicle · Mech | BUILT* | demo | radial schematic |
| Vehicle · Crew/Prop/Power/Avionics/GNC/Thermal | BUILT* | ui1 tab bar + template | 6 subsystem tabs; representative data |
| Suit Leak Check (4.011) | BUILT* | hi-res photos | + reconstructed fail branch |
| Test VRIO Health LEDs (4.700) | BUILT* | photos | procedure page |
| Audio / Cabin / Video Settings | BUILT* | Figma + photos | Cabin = frame render |
| Manual Chute Deploy | BUILT* | discovery5/15 | actions display-only |
| Manual ISS Docking | BUILT* | iss-sim live DOM | controls display-only |
| Procedure (generic, Frame 59) | BUILT* (static) | Figma Frame 59 | placeholder template |
| **Cover globe → 2D/3D map + camera modes** | REFINE | iss-sim + Baird nav render | = the "standalone Nav"; a Cover mode, not a page |
| **Vehicle → FUNCTIONS/ALERTS + red-nav; Overview CONSUMABLES col** | REFINE | Baird Vehicle render | = the Alert/Fault surface (per-subsystem, not a page) |
| Rendezvous orbital-ellipse plot | REF, not built | BBC touchscreens centre | left icon sub-nav + Hold-Capture card + 2D orbit ellipse; likely = the Proximity/ISS plot too |
| Deorbit Burn Prep | REF, not built | discovery10 (blurry) | Crew Interrupt Conditions + Slew for Deorbit Burn + settle-burn steps |
| Entry | REF, not built | discovery (partial) | "Parachute Deployment Altitude" procedure |
| Vehicle · Prop — real look = **thruster/RCS schematic** | REFINE | JSC `jsc2026e404727` | built as a gauge template; real = Dragon profile + Draco-quad arcs + alert rail (§11b) |
| Vehicle systems deep-views (P&ID plumbing · systems/electrical tree) | REF, not built | crew/demo + JSC `064449` | two distinct line-art deep-views (plumbing loops/valves; hierarchical box-tree); layout known, labels not transcribable |
| Nav / orbit plot (circular) | REF, not built | JSC `064449` + BBC | concentric rings + target markers + orbit arcs + g/rate; pairs with the Rendezvous ellipse |
| Ascent / Launch | DATA known, layout inferred | mission timelines (§8) | F9 schematic + real ascent-event list; no screen photo |
| Menu (nav index) | DESIGN SET §14.4 | function-reconstruct | grid/list of all pages; layout ours, content real |
| Reference Content (Cover deorbit-phase view) | DESIGN SET §14.4 | §8 real data | deorbit quick-ref: entry timeline/altitudes/abort notes |

`*BUILT = preview-verified, 82 nav checks passing; PROVISIONAL per owner.` REFINE = fold into an existing
built page. Lower analog console panel = its own workstream (§4).

## 4. Lower analog button panel (already coded — accuracy pass needed)
Built in code: `PanelButtons.cs` (colliders, click, arm/EXECUTE interlock, indicator-dash lighting) +
`pure/PanelMap.cs` + the `FlightCommands` stub. `PanelMap` was transcribed from an in-game transform dump of
**Tundra's fan IVA model** + that model's labels — accurate *to the model*, NOT independently verified vs the
real cockpit. Press coverage corroborates the shape: **two rows of ~38 buttons, many under clear guards**
(third-line backup) + the pull-twist EJECT handle ≈ our 38 + EJECT.

**Modelled inventory (6 plates):** Emergency ×2 (identical, guarded, arm→EXECUTE): CANCEL · WATER DEORBIT ·
DEORBIT NOW · BREAKOUT · EXECUTE / DEPRESS RESPONSE · SURPRESS FIRE[sic] · FIRE RESPONSE. Power/Strings:
POWER 1 · STRING 1A/1B/1C · RESET 1 / POWER 2 · STRING 2A/2B/2C · RESET 2. Chutes/Pyros: ENABLE BACKUP PYROS ·
JETTISON NOSE CONE · MAINS ONLY · DROGUES & MAINS · ENABLE ENTRY REBOOT · CUT MAINS · FIRE PYRD[sic]. Entry:
ENABLE BACKUP ENTRY · SWAP 1/2/3 · ENABLE NORMAL ENTRY. Handle: EJECT.

**True function research (public sources exhausted — real function first, inference marked):**
- CONFIRMED real: EJECT (SuperDraco abort, 8 modes) · POWER 1/2 (main buses) · DROGUES & MAINS (2 drogues →
  4 mains) · MAINS ONLY · CUT MAINS (release after splashdown) · ENABLE BACKUP PYROS / FIRE PYRO · WATER
  DEORBIT + DEORBIT NOW (contingency/immediate deorbit; water-landing norm, 7 sites) · DEPRESS/FIRE RESPONSE
  (leak + fire suppression) · the flight-computer strings (triple-redundant: 18 units / 54 voting processors).
- INFERRED (architecture-consistent, marked): exact STRING 1A–2C semantics · SWAP 1/2/3 (swap a failed
  string) · RESET 1/2 · entry-mode specifics.
- ⚠️ LIKELY-INACCURATE label (3 sources): **JETTISON NOSE CONE** — the real nose cone is HINGED (opens on
  orbit, closes+locks for reentry, stays attached; jettison was Cargo Dragon 1). Real control ≈ **OPEN/CLOSE
  NOSE CONE**. ⚠️ **BREAKOUT** — undocumented name, but the FUNCTION is real (crew abort-the-approach / back
  away, valid until the Crew Hands-Off Point).
- UNVERIFIABLE from public sources: the exact emergency-plate labels/order + visual confirmation of the
  nose-cone/breakout button text. The console band is dark/angled in every flown-console photo; the NASA
  "instrument panel" image is the 2015 prototype (dead end). Definitive source = SpaceX/NASA crew procedures
  (ITAR, non-public). Untried leads: museum/facility replica close-up, the Tundra modeller's cited references,
  deep NASASpaceFlight-forum threads.

**Behaviour (owner-decided 2026-09-02, §14.4 a+b):** REAL display-state = POWER 1/2 · STRING 1A–2C · RESET 1/2 ·
fire/leak response · the CONFIRMED entry commands (ENABLE BACKUP PYROS, FIRE PYRO). **INERT until verified**
(clicks, does nothing, stays unlit) = the inferred-only controls SWAP 1/2/3 + the inferred entry-mode toggles
(ENABLE ENTRY REBOOT / ENABLE BACKUP ENTRY / ENABLE NORMAL ENTRY). Flight/actuation (EJECT, deorbit, chutes,
nose cone, cut mains, undock) = honest no-op in the screens-only build until Part B wires them. **Lighting:**
buttons LIGHT UP BRIGHT when active/armed/fired (clearly crew-visible, matching the real console); **NO red
state** — no evidence of a red button, so the old red-refused is REMOVED; rest = unlit. **Audible CLICK on
every mechanical switch press** (new audio asset). A refused/inert press = click + no light + no action.
(⚠️ §4's INFERRED list also flags RESET 1/2; kept as display-state per the owner's option-B choice — revisit if
a real source contradicts.)
Never edit `PanelMap.cs`/`REAL_DRAGON_SCREENS.md` labels except after a real-source confirmation.

## 5. Capsule turntable render (owner-chosen)
A true 2-photo 3D reconstruction is impossible and our renderer is 2D, so this is a **pre-rendered sprite
turntable that reads as 3D**:
- **C1 — 3D model (prerequisite; VETTED CANDIDATE FOUND):** ✅ **MaTte0 "Crew Dragon Falcon 9"** (Sketchfab
  `c30dd3d…`, @matteomansion) — **CC-BY (Attribution), free-downloadable** (verified on the model page),
  includes Dragon + trunk + F9 (isolate the Dragon+trunk for the render). Usable WITH attribution to MaTte0.
  Backups: Sketchfab `dannzjs`/`hiyougami`, CGTrader free low-poly (Blender/OBJ), GrabCAD CAD (verify each
  licence). NASA 3D Resources has none. Actually downloading + rendering is a build-phase action (not plan
  mode) — C1 is now "a licence-clean, trunk-inclusive model is confirmed available."
- **C2 — offline turntable render:** N frames (start 36 @ 10°, 72 if needed), capsule-with-trunk, sized to
  the vehicle-page slot; horizontal spin first.
- **C3 — ship as a named sequence:** `art/cover/dragon_turn_000.png…` via `ImageStore.ResolveAsset` + a
  frame-picker helper.
- **C4 — drag-to-rotate:** horizontal drag delta → frame index (wrap), rendered on the vehicle page; a
  reset/"front" tap. Front vs rear = two indices of the same sequence. Verify per-frame in preview; drag
  needs on-glass validation.

## 6. Cross-cutting workstreams
- **Live data / parameterize:** replace representative constants in `VehicleSubsystemPage`,
  `VehicleOverviewPage`, `SuitCheckPage`, etc. with `PageState`/`VesselData` values (the stateless-UI
  principle). Chrome, globe, nose-cone state are already live; the numeric VALUES are the placeholders.
- **Simulation-for-immersion (§14.4(e)):** a placeholder with no source is not defaulted to a dash — take the
  value from an installed mod, else SIMULATE it coherently off real state and MARK it; dash only if absent.
- **Completeness + simulate-to-fill (§14.4(f), 2026-09-03):** every feature the real screens have is INCLUDED
  and FILLED — live source first, else a coherent MARKED simulation that BEHAVES live (safety verdicts computed
  from the model, never hardcoded); a dash only for a genuinely-absent state. READOUTS only — the touch-wiring
  bullet below (actuation) is UNCHANGED and stays §14.4(a) honest-no-op until Part B.
- **Touch wiring:** the display-only controls (Manual Chute per-step actions, Docking clusters, Suit Leak
  TROUBLESHOOT/timer, and the console panel per §4) → real state/actions once behaviour is defined.

## 7. Execution order (most-complete-reference-first)
1. Refinements with clean reference — Cover map-modes; Vehicle Alerts+Consumables.
2. Rendezvous orbital-ellipse plot (one clear frame).
3. Deorbit Burn Prep (blurry frame → reconstruct marked).
4. Entry (partial frame).
5. Vehicle systems P&ID schematic (needs a cleaner frame).
6. Lower analog panel accuracy pass (§4) — high reference; behaviour decision is owner-gated.
7. Capsule turntable (§5) — after a model is sourced.
8. Ascent/Launch — data-buildable, layout reconstructed.
9. Live-data + touch wiring (§6) — cross-cutting.
10. Menu (nav index) + Reference Content (deorbit quick-ref) — DESIGN SET (§14.4); build with Part A.

## 8. Crew Dragon flight-facts data reference (drives screen DATA)
Real numbers/names for the phase rail, procedure pages, Ascent/Rendezvous screens, and live-data wiring
(DM-2 + Crew-2 timelines, NASA CCP press kit + rescue/recovery, NASASpaceFlight; values vary per mission).
- **Ascent (T+ from liftoff):** Liftoff · Pitch kick ~0:10 · Max-Q ~1:00 · Mach 1 ~1:09 · Stage-1b abort-mode
  ~1:14 · MECO ~2:30–2:35 · stage sep ~2:35–2:39 · S2 ignition ~2:36–2:47 · SECO-1/orbit insertion ~4:20–8:43
  · Dragon sep ~9:00–12:02 · nose-cone open ~12:48–13:23. (Falcon 9 schematic + this list = the Ascent page.)
- **Rendezvous burns (real names):** Phase (~T+47–50 min) → Boost → Close → Transfer → Coelliptic →
  Out-of-plane. Approach: 4 km Approach Ellipsoid → Keep-Out Sphere → Waypoint 1 → Waypoint 0 → Crew
  Hands-Off Point (CHOP, crew abort ends) → Contact & capture at IDA-2. Dock ~19 h after launch. LIDAR + cams.
- **Return/deorbit:** undock → distance → trunk jettison → deorbit burn (~15 min) → nose-cone close+lock →
  entry → drogues → mains → splashdown. Claw (trunk↔capsule thermal/power/avionics link) separates ~1 h 20 m
  before splashdown; deorbit decision ~30 min before claw-sep prep; splashdown ~50 min after burn start.
- **Parachutes (Mark 3):** 2 drogues first, then 4 mains at ~2 km (matches our chute page); land under ≥3;
  CUT MAINS after splashdown.
- **Vehicle structure:** capsule = pressurized + service + nose-cone sections; trunk = half solar array +
  half radiators, jettisoned shortly before reentry; nose cone stowed (not jettisoned) pre-reentry; 8
  SuperDraco abort engines. Flight computers triple-redundant (18 units / 54 voting processors).
- **Realism refs for data wiring:** ECLSS + Active Thermal Control (ATCS) papers (Crew/Thermal values), ECEF
  coords (orbit/globe math).
- **Press-kit timeline pass (done):** `pdftotext -layout` on the CCP kit yields the Crew Dragon ascent event
  table (liftoff · Max Q · MECO · 1st/2nd stage sep · S2 start · 1st-stage entry burns · SECO-1 · Dragon sep ·
  nosecone-open) — CONFIRMS the sequence, but the extracted T+/clock columns misalign (and the kit interleaves
  the Starliner Atlas-V timeline), so its numbers are NOT reliable. Use the mission-timeline T+ values above as
  authoritative. No separate clean Dragon on-orbit/return timeline table in the kit (return is prose, captured).

## 9. Architecture & files the build will touch
- **Nav:** `plugin/src/pure/FigmaUI.cs` — `UiPage` enum (append-only), `Build` dispatch, `HitTest`,
  `ActiveBarIcon`, `PageCount`. New page = a `UiPage` + `Build` case + preview-loop entry + nav-test checks.
- **Draw:** `plugin/src/pure/DisplayList.cs`; renderers `plugin/src/ScreenPainter.cs` (GL) + `plugin/preview/
  PreviewMain.cs` (GDI+) must agree.
- **Reuse:** `CoverPage.DrawRail`, `VehicleTabBar`, `NavPage.Planet`, `component_48` bar, `ImageStore.
  ResolveAsset` (`GameData/DragonScreen/art/cover/`). New Cover-chrome pages mirror `ManualChuteDeployPage.cs`;
  procedure pages mirror `SuitCheckPage.cs` / `VrioTestPage.cs`. Console panel: `PanelMap.cs` + `PanelButtons.cs`.
- **Tests:** `plugin/test/FigmaUINavTest.cs`.

## 10. Verification (per item)
`python plugin/build.py preview` → inspect PNG in `plugin/build/preview/` (preview-first; restarts are scarce).
`python plugin/build.py test` → all headless suites incl. the Figma UI nav suite stay green. `build.py install`
(KSP + CKAN closed) + owner screenshots only for what needs the capsule, and only on a separate owner go.
Commit LOCALLY with `git commit`; never `git push` (C1.5) — the owner pushes from GitHub Desktop.

## 11. Research status — COMPLETE
The owner-set research passes are all DONE: (a) map consolidated [this document] · (b) press-kit T+ timeline
pulled (corroborated the ascent events; times unreliable → §8 mission-timeline values are authoritative) ·
(c) a vetted CC-BY trunk-inclusive 3D model found (§5) + the JSC render vein worked (§11b) · (d) broad mine
run (NASA library → 3 new screen designs; SpaceX Flickr → dead end). Research declared complete by the owner.
Label-level gaps that would need ITAR/SpaceX manuals or owner-supplied imagery (carried as caveats, NOT
blockers): exact on-screen text for the newest screens; emergency-plate labels + a console-down close-up
(§4); cleaner Deorbit Burn Prep / Entry / Reference Content / Menu frames; the unscanned Figma "Cover" page
(12221:242). First action out of plan mode: mirror the new screens + refinements into
`docs/SCREEN_INVENTORY.md` + the map artifact, then build per the §7 order on the owner's go.

## 11b. Broad-sweep finds — NASA image library (session 2, 2026-09-01)
The **JSC crew-training photo series** on images.nasa.gov (`jscYYYYeNNNNNN`, Crew-1…13) is a RICH, well-lit,
high-res imagery vein not previously tapped — crew posed in the Dragon (mockup) with the screens + console
clearly lit, far better than the dark Discovery frames. High value for verifying screens AND the console.
- **NEW real screen — Propulsion / RCS thruster-status page** (`jsc2026e404727`, Crew-13): the Dragon drawn
  in **horizontal profile** (capsule + trunk line-art) ringed by the **Draco thruster-cluster arc symbols**
  with per-cluster firing/status indicators, small per-thruster data labels, and a left subview-icon rail.
  This is the AUTHENTIC look for our **Vehicle · Prop** page — which we built as a generic gauge template, so
  Prop should be REFINED to this thruster schematic. Distinct from the P&ID plumbing schematic (a 2nd deep-view).
- Left screens in the same set (`jsc2024e079789`, `jsc2025e064540`, `jsc2022e068644`) show the attitude/nav
  HUD — corroborates our HUD. Console band is better-lit here than Discovery → the next console-verification
  attempt (panel §4) should mine this JSC series for a legible emergency-plate close-up.
- **More new screen looks (`jsc2024e064449`, sim rig — two readable screens):** LEFT = a **hierarchical
  systems/electrical TREE diagram** (labelled boxes joined by connector lines) — a distinct look, likely a
  Power-distribution / systems tree (a 3rd subsystem deep-view alongside the P&ID plumbing + the thruster
  schematic); RIGHT = a **circular nav/orbit plot** (concentric rings + coloured target markers + orbit
  arcs + a g/rate readout) — corroborates the Rendezvous/nav plot. Also the **best-lit console band yet**
  (button-group plates + a labelled rotary + a red guarded control) but still not label-legible at the angle.
- **Deep-dive result (44 of 74 JSC frames pulled + contact-sheeted):** the series is GOLD for screen *looks*
  (prop thruster schematic, systems tree, nav/orbit plot, attitude HUD) but the shots are upward-at-crew, so
  none gives a label-legible top-down console/emergency-plate close-up. The panel-label verification (§4)
  still needs a dedicated console close-up we have not found in public imagery.
- **Full-res transcription pass (done):** pulled high-res (3240–3840px) of the screen-facing frames
  (`064523/064540/404725/066390/079790`) + zoomed the sim-rig (`064449`) and prop screen (`404727`). Result:
  the **LAYOUTS of the new screens are captured** — enough to build their visual grammar — but the mockup
  screens are shot at steep angles + glare, so **exact on-screen text is NOT transcribable** at these
  resolutions. So these pages, when built, are **layout-real / labels-reconstructed** (same status as our
  other from-photo pages). The three new designs, characterised:
  - **Prop / RCS thruster schematic:** Dragon in HORIZONTAL profile, Draco thruster-quad arc symbols arrayed
    around the hull with per-cluster bars/status, a LEFT alert+sub-nav rail, per-thruster data along the bottom.
  - **Systems tree:** a HIERARCHICAL box-and-connector diagram (a power/electrical or systems distribution tree).
  - **Nav / orbit plot:** concentric rings + coloured target markers (yellow + cyan) + orbit arcs + a g/rate
    readout — the circular situational plot (pairs with the Rendezvous ellipse view).
- **Console still not label-legible** in any JSC frame (all upward-at-crew). Panel §4 verification remains
  blocked on a console-down close-up not found in public imagery.

## 12. Sources & assets appendix
- **Screen imagery:** REAL_SPACEX_SCREENSHOTS/ (hi-res `discovery*.jpg`), shanemielke.com (designer gallery —
  fully enumerated, no thin-page renders), iss-sim.spacex.com (docking DOM), BBC explainer (`touchscreens.png`
  = rendezvous frame), neel-dandiwala demo, community Figma (k1X6Ytu9rEVcveKrS1Re5s).
- **Recreations (structure only):** dillonbaird.io/articles/mutantdragon (renders + authoritative alt-text;
  live demo mutantdragon.space is DEAD, repo empty), uxdesign.cc (Ulises — 5 physical panels), os-system.com
  (real stack).
- **Flight data:** DM-2/Crew-2 timelines (Everyday Astronaut, Spaceflight Now), NASA CCP press kit + rescue/
  recovery, NASASpaceFlight, Wikipedia LAS.
- **3D models / renders:** Sketchfab (MaTte0 CC-BY, §5) / CGTrader / GrabCAD (models); NASA image library
  (the JSC training series, §11b — best screen imagery). AJ Fitzpatrick portfolio (ITAR-confidential).
- **SpaceX Flickr — CHECKED, dead end for our need:** the photostream (1,486 photos) is launch/exterior
  photography (Starship/Transporter/Intelsat/SDA…); user-scoped search does not surface Crew Dragon interior/
  console shots. No console-down angle or new-screen imagery there.

## 13. Research conclusion (2026-09-01)
Public sources are now EXHAUSTED across the accessible veins (designer gallery, NASA image library incl. the
JSC training series, iss-sim, DillonBaird + article, BBC, press kit, mission timelines, SpaceX Flickr,
recreations). We have: every page's LAYOUT/visual grammar; the flight-facts data (§8); the panel functions
(confirmed/inferred/invented, §4); and 3 newly-characterised screen designs. Remaining gaps are LABEL-level
only and gated on non-public sources (ITAR/SpaceX manuals) or owner-supplied imagery: exact on-screen text
for the newest screens, the emergency-plate labels + a console-down close-up, and cleaner Deorbit Burn Prep /
Entry / Reference Content / Menu frames. The map (§3) + this document are the consolidated build base; when
out of plan mode, mirror the new screens into `docs/SCREEN_INVENTORY.md` + the map artifact.

---

# PART B — MechJeb Autopilot Core (NEW workstream, 2026-09-01)

🟢 **PART-B BUILD GATE OPEN — the OWNER's decision, 2026-09-03, granted via the overseer and confirmed in-chat
(recorded as the owner's per C1.12).** The standing preview-only build-go (§0) is **EXTENDED to cover Part-B
code**: **T15 onward** — the pinned, privately-namespaced **MechJeb embed (GPLv3, §B2/§B3/§B12.1)** and the
conductor — is **GO**, built at **RSS-RO DEFAULT settings as the baseline to tune from** (§B5's "begin from RO
defaults"), with the one-parameter-at-a-time **fine tune DEFERRED until after the first recorded flight**
(§B5 / T22). The same limits apply as to Part A: **pure code + `build.py test` + `build.py preview` only** —
**`build.py install` and glass time REMAIN SEPARATE owner gates, granted per session** (C1.12 unchanged), so a
Part-B step whose done-criteria are "in-sim" (T17 onward) **stops and asks** rather than installing. **Only the
owner opens or widens this gate**; a build chat never self-authorizes one.

### B0. Part B reading order & contents (numbers are labels, THIS is the order)
B1 Direction · B2 Grounding (MechJeb installed · Crew-2 cfg · GPLv3) · B3 Packaging (embed pinned/namespaced —
LOCKED) · B4 Conductor model · B5 Tuning methodology (knowledge-first → one-by-one empirical vs real data) ·
B7 Ascent tuning first-cut (mechanics) · B8 Ascent FULL guidance · B9 Full mission sequence (every phase → op
→ knobs) · B10 On-orbit modules FULL per-parameter guidance · B11 Flight-data TARGET reference ([DOC]/[EST]) ·
B12 Build architecture (the conductor: embed · pure core + glue driver · phase FSM · re-plan loop · screen
front-end · build order · **B12.7 direct part control** · **B12.8 recovery-first build order**) · B13 Abort
system · B14 Crew-gate procedures · B15 FDIR/fault detection · **B16 Falcon-9 booster recovery** (owner scope
addition 2026-09-03 — a SEPARATE-VESSEL autopilot, §B16.1–§B16.9; per-setting recipe in
`docs/MECHJEB_MISSION_TUNING.md` §2) · B6 Honest risks. (Cross-cutting capstone: **§14 Coherence pass &
source-tier map**, at the very end.)

## B1. Direction (owner)
Reintroduce flight software (the autopilot was deleted for the screens-only pivot) as a **MechJeb-driven
core**. A **"conductor"** — code that behaves like an expert MechJeb operator — drives MechJeb phase-by-phase
in lockstep with the screens, using **permanently-locked, Crew-Dragon / RSS-RO-tuned parameters**. MechJeb's
rendezvous *autopilot* is unreliable in RSS/RO, so the conductor instead composes the **Maneuver/Rendezvous
planners + Node Executor**, re-planning burns live to hold a nominal real-Dragon profile. The screens' command
buttons (currently honest-refuse via `_AutopilotStub`) become the front-end to this core.

## B2. Grounding found (2026-09-01)
- **MechJeb2 is installed** (`GameData/MechJeb2/Plugins/MechJeb2.dll` + `MechJebLib.dll` + alglib) — modern
  build with **PVG** (Primer Vector Guidance) ascent, the RSS/RO-correct guidance.
- **A tuned `Crew-2` profile already exists**: `…/PluginData/MechJeb2/mechjeb_settings_type_Crew-2.cfg`. This
  IS MechJeb's permanent per-vessel-type parameter store AND our current tuning research. It holds a real
  ascent tune (PVG guidance active — GuidanceController + PVGGlueBall configured): `DesiredInclination
  -51.6316` (ISS), `Desired Apoapsis/OrbitAltitude 210 km`, `PitchStartVelocity 70`, `TurnStartAltitude 500m`,
  `LimitQa 2000`, `MaxAoA 5`, `CorrectiveSteeringGain 3`, hot-staging + fairing-drop rules, tuned attitude PID.
  In this cfg: a tickbox = a `bool` (`CorrectiveSteering = True`), an editable field = a `{ValConfig (internal),
  TextConfig (GUI text)}` pair. So "every user-editable parameter + how to set it permanently" = enumerate each
  module ↔ these keys.
- **MechJeb2 is GPLv3** (`MechJeb2/LICENSE.md`). Combined with **public distribution** (owner) → DragonScreen
  becomes a GPLv3 combined work and must ship source under GPLv3-compatible terms — true for BOTH port and link.

## B3. Packaging decision (perf-neutral; self-containment vs maintenance)
Porting vs linking has **no performance difference** (same CIL/runtime/math); perf comes from running MechJeb
**headless** + enabling only needed modules + controlling heavy-sim cadence. Naive DLL-bundling is unsafe
(RO/RSS users often already run MechJeb → duplicate-assembly conflict). **Recommended: embed MechJeb as a
pinned, privately-namespaced assembly built inside DragonScreen** (source kept INTACT, not rewritten; renamed
namespace/assembly to avoid any clash; drive headless via its module API). A true rewrite-port only pays off
if we must modify MechJeb internals. **DECISION LOCKED (owner, 2026-09-01): embed a pinned, privately-
namespaced MechJeb built inside DragonScreen, driven headless via its API.** (Distribution is public → ship
DragonScreen + the embedded MechJeb source under GPLv3; pin the exact tuned version.)

**SCOPE AMENDED 2026-09-03 (owner, via the overseer) — the port is FULL AND COMPLETE.** T15 vendors a
**complete port of upstream `MuMech/MechJeb2`, newest commit at port time, from GitHub — everything, dead code
included** — not a subset of the modules the conductor happens to call. (The repository is named outright
here, not left a blank to fill in — see §B12.1a's RESOLVED G5a-Q1 for the research behind the choice.) And
the conductor must be able to **edit and set ALL user-editable settings**, acting as an expert human would at
the UI. The full detail, and the three tensions this scope creates, are in **§B12.1**; nothing here narrows it.

## B4. Conductor model (how it will work)
Per mission phase, the conductor engages the right MechJeb module with the locked params — exactly as an
expert user would: **Ascent** = PVG ascent autopilot (Crew-2 profile); **Rendezvous** = Maneuver Planner
`Operation*` classes (transfer → match-velocities → fine course-correction) → **Node Executor**, re-planned
live; **Docking** = docking autopilot or hand-off to the manual docking screen; **Deorbit/Entry** = maneuver
planner + node executor + landing guidance. Modules reached via `MechJebCore.GetComputerModule<T>()`.

## B5. First research task (owner-set): "exactly how to tune correctly" — the MechJeb tuning reference
FIRST deliverable = the KNOWLEDGE of how to tune, before touching any value. For every user-editable
parameter/option/tickbox per MechJeb module, document: **what it does · units · valid range · effect on the
flight profile · the RO default · how to set it (the `mechjeb_settings_type_*.cfg` key AND the API field)**.
Start with **launch/ascent (PVG + ascent settings + staging + attitude/Q limits)**, then per mission phase.
Sources: the `Crew-2` settings cfg (persisted keys), **MechJeb2 source on GitHub** (the `[Persistent]`
`EditableDouble/EditableInt` fields, the `AscentType`/PVG enums, GUI tooltips) + the MechJeb wiki, and the
module list from `MechJeb2/Icons/`. Build as a new reference doc added to our MechJeb research.

**Tuning METHODOLOGY (owner):** begin from **RO's default settings** (not a blank slate), then tune **one
parameter at a time, validated against real Crew-Dragon flight data** (the §8 flight-facts + real telemetry),
converging each knob until the profile matches nominal real missions — then lock it into the per-vessel-type
cfg. Knowledge first (this task) → then the one-by-one empirical tune.

**The two-profile split — what flight 1 actually loads (owner, 2026-09-03, via the overseer).** "Stock" means
**RSS-RO DEFAULTS** (never the bare word — see CLAUDE.md's C1 rule); `docs/reference/mechjeb_settings_type_
Crew-Dragon.cfg` (the tuned Crew-2 profile T0 copied in) is **NOT what flies first.**
- **Flight 1 loads RO's OWN shipped MechJeb defaults** for every ascent-shaping / attitude / throttle /
  staging KNOB — the values §B7/§B8 label "RO default". **NOT** the Crew-2 cfg's already-researched values
  (Pitch Rate 0.75, Pitch Start Velocity 70, etc.) — those are **DEMOTED to the §B5 tune's TARGET**, alongside
  §B11's [DOC]/[EST] numbers, both converged toward empirically in **build-order step (8)** (§B12.6) using
  flight-1 BlackBox data, per the standing "fine tune deferred until after the first recorded flight" gate
  (§0).
- **Target-orbit values are the one exception — and here is why.** `DesiredInclination`/`DesiredOrbitAltitude`
  etc. are not ascent-shaping knobs at all — they are **MISSION FACTS** (the real Crew-2 orbit, ~210 km ×
  51.63°, §8). A destination is not something you tune toward; it is data you already have. These load
  correctly from flight 1 regardless of which profile (RO-default or Crew-2-tuned) is active.
- **The booster is a different case, stated here so the two are never conflated.** It has **no MechJeb
  baseline at all** — it isn't a MechJeb vehicle (§B16.1/§B16.5). Its flight-1 constants are the **recovered,
  explicitly un-converged values** of §B16.8 (the 48-flight drag curve, the 55-flight tuning DB), not an
  RO-default/Crew-2 split. This is unrelated to the Dragon two-profile policy above.

## B7. Ascent tuning reference — FIRST CUT (MechJebModuleAscentSettings, source + Crew-2 cfg)
`AscentType` enum = **CLASSIC (0) / PVG (1)** → Crew-2 `AscentTypeInteger = 1` = **PVG** (RSS/RO-correct).
"How to set" mechanics: bool = cfg `Name = True/False`; scalar = `Name { ValConfig=<internal SI>, TextConfig=
<GUI display> }`. `EditableDoubleMult` fields keep internal SI in ValConfig + a scaled display in TextConfig
(e.g. `TurnStartAltitude` Val 500 m / Text 0.5 km; `DynamicPressureTrigger` Val 10000 Pa / Text 10 kPa) — set
via the module's Editable field (handles the scale) or write both cfg values. (WebFetch's "units" for Mult
fields were the DISPLAY unit; ValConfig is the authority.)

Ascent params grouped (name · type · RO default · role). **Per §B5's two-profile split: every "Crew-2: <value>"
annotation below is a TARGET for the §B5 tune, not what flight 1 loads — flight 1 loads the RO default named
in each bullet, and the Crew-2 value is what later flights converge toward.**
- **Target orbit:** DesiredOrbitAltitude (Mult, m) · DesiredApoapsis (Mult, km disp) · DesiredInclination
  (deg) · DesiredLan (deg) · RelativeLAN (bool). Crew-2: 210 km / incl -51.6316 (ISS).
- **PVG-specific:** AttachAltFlag (bool) + DesiredAttachAlt/Fixed (Mult, m) · DesiredFPA (rad) · DesiredArgP
  (rad)+DesiredArgPFlag · MinDeltaV (40 m/s) · MaxCoast (450 s)/MinCoast (0)/FixedCoast · Optimize/Coast/
  Spinup stage flags + *StageInternal indices · SpinupLeadTime (50 s)/SpinupAngularVelocity (τ/6 rad/s) ·
  Cd (0.5)/Aref (m²) drag model. GuidanceController.UllageLeadTime (20 s). These are the live PVG knobs.
- **Classic gravity-turn (mostly inactive under PVG):** TurnStartAltitude (500 m)/TurnStartVelocity (50 m/s) ·
  TurnEndAltitude (60 km)/TurnEndAngle (0°) · TurnShapeExponent (0.4) · AutoPath (bool)+AutoTurnPerc (0.05)/
  AutoTurnSpdFactor (18.5) · PitchStartHeight (100 m)/PitchStartVelocity (Crew-2 70)/PitchRate.
- **Aero/attitude limits:** LimitQaEnabled (bool)+LimitQa (2000 Pa) · LimitAoA (bool)+MaxAoA (5°)+
  AOALimitFadeoutPressure (2500 Pa) · CorrectiveSteering (bool)+CorrectiveSteeringGain (3) · ForceRoll (bool)+
  VerticalRoll/TurnRoll/RollAltitude (50 m).
- **Auto/misc:** Autostage (bool) · AutoDeploySolarPanels/Antennas (bool) · SkipCircularization (bool) ·
  WarpCountDown (11 s).
- **Coupled modules that shape ascent (in the same cfg):** MechJebModuleStagingController (HotStaging,
  DropSolids, HotStagingLeadTime 1s, DropSolidsLeadTime 1s, FairingMaxDynamicPressure 5 kPa/FairingMinAltitude
  50 km/FairingMaxAerothermalFlux 1135, AutostagePre/PostDelay, ClampAutoStageThrustPct 0.99) ·
  MechJebModuleThrustController (LimiterMinThrottle, MinThrottle, DifferentialThrottle) ·
  MechJebModuleAttitudeController.BetterController (PosKp/PosTi, VelKp, RollControlRange, MaxStoppingTime,
  MinFlipTime, Soften, SmoothTorque — the launch pointing PID).
- **TODO (next research increments):** the PVG autopilot/guidance module semantics (how attach-alt + FPA +
  stage-optimize actually drive the trajectory), then per-phase params — Maneuver-Planner `Operation*` classes
  (transfer/circularize/match-velocities/course-correction) + NodeExecutor (tolerance, lead time, RCS/ullage),
  Docking autopilot, Landing autopilot, SmartASS/RCS. Document each: what · units · range · effect · RO
  default · how-to-set, per §B5.

## B8. Ascent (PVG) — FULL tuning guidance (RO default → Crew-Dragon target → why/how)
Sources: RP-1 wiki *TroubleshootingMechJebPVG* (authoritative RO PVG guide), MechJeb source (B7), the Crew-2
cfg (current tune), §8 flight facts. **Core principle: PVG is a VACUUM-only optimizer. The ascent has two
regimes — (1) an OPEN-LOOP pitch program the human tunes for the aerodynamic climb, handed off at the Max-Q
trigger to (2) CLOSED-LOOP PVG that optimizes the vacuum arc to the target orbit.** Bang-bang throttle (full)
is optimal for PVG; don't change staging after liftoff (Reset Guidance instead).

⛔ **AUTOSTAGE IS OFF — GUIDANCE-PREDICTION IS SEPARATED FROM ACTUATION (owner directive, 2026-09-03, via the
overseer).** An earlier version of this line read *"autostaging MUST be on for its prediction"*. That
conflated two different things and it is **superseded**. The resolution the owner directed:
- **PVG KEEPS ITS FULL STAGE MODEL** — the stage list, `OptimizeStageInternal`, `MinDeltaV`, the coast-arc
  settings and the burnout prediction are all unchanged. PVG must still *know* the staging schedule to
  optimize the arc; that is what the stage model is for.
- **`MechJebModuleStagingController.Autostage` = FALSE.** MechJeb never actuates a separation or an ignition.
- **The conductor performs every separation and every ignition DIRECTLY, at the times PVG predicts**, through
  the named-part path of **§B12.7** (`ModuleDecouple.Decouple()`, `ModuleEngines.Activate()` per engine) —
  never staging, never an action group.
- **A T18 chat must not read the old sentence and switch autostage back on.** If PVG's arc looks wrong, the
  fault is in the stage MODEL or in the conductor's separation timing — fix those. Turning autostage on hands
  actuation back to MechJeb and breaks §B12.7, which is binding on all of Part B.

Per-parameter (what · RO default · Crew-Dragon target · why / how-to-tune):
- **AscentType** — CLASSIC(0)/PVG(1). Target **PVG(1)**. Why: RSS/RO needs the vacuum optimizer; Crew-2 ✓.
- **Pitch Rate** (deg/s) — THE primary knob. Default 50 m/s pitch-start's companion; range 0.1–2+. Crew-2
  0.75. Tune to sea-level TWR (F9 liftoff TWR ~1.2–1.3 = low → low rate). **Method:** fly, read the flight-
  recorder Q/AoA/pitch graph — AoA spike AT max-Q = overlofted → raise rate; AoA deviating BEFORE max-Q =
  overpitched → lower rate 0.1 at a time; optimum = flat AoA line through max-Q. This is the #1 empirical tune.
- **Pitch Start velocity** (m/s) — default 50; raise for low TWR (75–100 for very low). Crew-2 70. Why: delays
  pitchover until aerodynamically safe on a low-TWR F9. Paired with Pitch Rate.
- **Turn Start Altitude / Velocity** — 500 m / 50 m/s (Crew-2 ✓). When the open-loop pitch program engages.
- **DynamicPressureTrigger (Max-Q switch)** (kPa) — LEAVE default **10 kPa** (Crew-2 ✓). Marks open-loop→PVG
  hand-off. Don't tune unless the vehicle is still deep in atmo at 10 kPa.
- **LimitQaEnabled + LimitQa** (Pa·rad) — default 2000 (~2° AoA at 30 kPa max-Q); real-rocket range 1000–4000.
  Crew-2 2000. Lower for a floppy/unstable stack; raise for a stiff one. Keep enabled through max-Q.
- **LimitAoA + MaxAoA** (°) — Crew-2 5°. Aero-load cap during the pitch program.
- **Target orbit:** DesiredInclination **51.6316°** (ISS — Crew-2 ✓; can't be < launch-site latitude 28.6°).
  DesiredOrbitAltitude/Apoapsis **210 km** (Crew-2 ✓) — Dragon inserts low then phases up to ISS (~420 km).
  SkipCircularization = True (Crew-2) — PVG inserts directly to the target; verify against real insertion.
- **PVG stage/coast:** OptimizeStageInternal (Crew-2 = 8) = the stage PVG optimizes burnout on (= last powered
  stage). MinDeltaV 40 m/s (exclude ullage/RCS stages). MaxCoast 450 s / MinCoast 0 / FixedCoast — coast arc
  between stages; keep unless a specific coast is needed. **Autostage OFF** — the stage model stays, the
  actuation is the conductor's (the §B8 autostage rule above; §B12.7).
- **Attach altitude / FPA:** AttachAltFlag + DesiredAttachAlt/FPA — force a specific insertion (burnout
  elevation). Crew-2 sets AttachAlt 210 km + FixedCoast — ⚠️ VERIFY this matches Dragon's real insertion vs
  letting PVG free-optimize; attach is mainly for shuttle-style 90×180 inserts.
- **Throttle — SETTLED (O9, owner, 2026-09-03, via the overseer): MechJeb's own auto-throttle controls, NOT
  PVG bang-bang.** RP-1 says PVG wants bang-bang (full throttle, limiters OFF); the owner **explicitly reverses
  that recommendation** — MechJeb's auto-throttle (max-Q throttle-down etc.) is preferred for precision, "more
  precise especially for fine manoeuvres that need precise low-throttle control." Two distinct cases: **ascent**
  max-Q throttle-down costs PVG some optimality (an accepted trade, expect more T22 tuning to compensate);
  **on-orbit** low-throttle burns (§B10.2 ops, fine rendezvous corrections) involve no PVG at all and are a pure
  win with no trade-off. `ThrustController.LimiterMinThrottle=True` (Crew-2's persisted value) is therefore the
  RIGHT setting, not a review item — it stays **True**. Only the exact throttle curve/floor is still an
  empirical T22 tune (a different, still-open thing from the mechanism decided here).
- **Staging flags** (StagingController): with **Autostage OFF** these become inert — the StagingController
  never fires, so `HotStaging`, the lead times and the fairing rules cannot mis-trigger. They stay recorded
  because the BEHAVIOUR they describe is now the conductor's to reproduce: real F9 does a **COLD** stage sep
  (so the conductor separates, then ignites, with its own lead time — it does not hot-stage), and Dragon has
  **no fairing** (the hinged nose cone stays), so there is no fairing event to schedule at all.
  Crew-2's persisted values (`HotStaging=True`, FairingMaxDynamicPressure 5 kPa, FairingMinAltitude 50 km) are
  left as-is in the cfg and simply not acted on.
- **Attitude (BetterController PID)** — Crew-2 PosKp 2.03 / PosTi 1.97 / VelKp 7.98 / RollControlRange 5 /
  MaxStoppingTime 2 / MinFlipTime 120 / Soften 0.5. The launch pointing controller; tune only if the stack
  oscillates or is sluggish on the gravity turn.

**Open ⚠️ flags to resolve empirically (RSS-accuracy vs PVG-optimality):** hot vs cold staging (moot —
Autostage is OFF, see above); attach-altitude vs free-optimize; fairing logic on a fairingless Dragon
(also moot under Autostage OFF). **Throttle limiter vs bang-bang is SETTLED (O9): MechJeb auto-throttle wins**
— only the exact throttle curve remains an empirical T22 tune.

## B9. Full mission sequence — the conductor's phase-by-phase MechJeb use & tune
The complete Crew-Dragon flight, step by step. Each phase: **real events (§8) → the MechJeb module/operation
the conductor drives → how it's engaged → the tuning knobs (what · default · Crew-Dragon target · why) → ⚠️
flags.** Rendezvous uses the **Maneuver Planner `Operation*` classes + Node Executor** (never the rendezvous
*autopilot*), each op producing node(s) via `make_nodes()` that the Node Executor then flies — re-planned live.
Modules reached by `MechJebCore.GetComputerModule<T>()`. (Op params below are from the kRPC.MechJeb API;
Node-Executor/SmartASS/Docking/Landing cfg keys are in the `Crew-2` store — exact key names ⚠️ to verify vs
the live cfg as each is implemented.)

**Phase 0 — Prelaunch / pad.** Conductor: select the Crew-Dragon per-vessel-type profile (loads all locked
params), set the **target = ISS**, arm PVG Ascent Guidance. No burn. Tune: none live — this just loads B8.

**Phase 1 — Ascent to insertion (T0 → SECO-1).** → **PVG Ascent Autopilot** (full guidance in **§B8**).
Ends at the ~210 km × 51.63° insertion orbit; Dragon separates, nose cone opens (§8). Real events: Max-Q,
MECO, stage sep, S2, SECO-1. Tune: see §B8 (Pitch Rate is the #1 knob).

**Phase 2 — Insertion trim & phasing setup (post-sep).** Real: Dragon on its low insertion orbit, must set
up the catch-up (phasing) orbit below ISS. → **Maneuver Planner `OperationCircularize`** (clean up insertion)
and/or **`OperationPeriapsis`/`OperationApoapsis`/`OperationEllipticize`** (`new_periapsis`/`new_apoapsis`, m)
to establish the phasing orbit → **Node Executor**. Tune: target apsides set so the phase angle to ISS closes
at the real rate (Dragon docks ~19 h after launch → a slow catch-up from ~210 km toward ~420 km). Knob = the
phasing-orbit altitude (lower = faster catch-up). ⚠️ Real Dragon does a scripted burn series; we approximate
with apsis targets tuned so total time-to-dock ≈ nominal.

**Phase 3 — Rendezvous (real burns: Phase → Boost → Close → Transfer → Coelliptic → Out-of-plane).** The core
planner-composition phase. Conductor sequences these ops, each → `make_nodes()` → **Node Executor**, RE-PLANNED
after every burn (residuals + drift):
- **Out-of-plane** → **`OperationPlane`** (match planes with target; no params; TimeSelector = relative
  ascending/descending node). Kill the plane error early and cheap.
- **Phase / Boost / Transfer** → **`OperationTransfer`** (bi-impulsive Hohmann to target). Params:
  `intercept_only` (bool), `simple_transfer` (bool — false = let it optimize the intercept), `period_offset`.
  TimeSelector `computed` (optimum) for the transfer to intercept. This is the big catch-up burn to an
  intercept trajectory.
- **Close / fine approach** → **`OperationCourseCorrection`** (fine-tune closest approach). Params:
  `intercept_distance` (m — set to the **4 km Approach-Ellipsoid** entry, then tighter on the next pass),
  `course_correct_final_pe_a`. Run 1–2× to walk the closest approach down.
- **Coelliptic / arrive** → **`OperationKillRelVel`** (match velocities with target; no params; TimeSelector
  `closest_approach`) — arrives + nulls relative velocity at the station-keeping point.
- Node Executor tune: **Tolerance** (m/s residual before cutoff; RO ~0.1 — smaller=more precise, too small=
  chases noise), **Lead time** (attitude-settle before ignition), **Autowarp** on, **RCS/ullage** before
  ignition (ties to GuidanceController.UllageLeadTime 20 s from §B8). ⚠️ Verify NodeExecutor cfg key names.

**Phase 4 — Proximity ops & docking (Approach Ellipsoid → Keep-Out Sphere → WP1 → WP0 → CHOP → capture at
IDA-2).** **Auto-dock is the DEFAULT (O6, owner, 2026-09-03, via the overseer):** the **Docking Autopilot**
flies the approach — knob **speedLimit** (m/s; Crew-2 = 1; real Dragon creeps in far slower, ~0.1–0.2 m/s at
contact → tune speedLimit DOWN through the waypoints), plus approach-distance/roll-alignment settings, on Draco
RCS. Pressing the manual docking button **switches to the Manual ISS Docking screen and shuts down the Docking
Autopilot** (couples to S28, which T20 makes live) for crew-flown final approach, with **SmartASS TARGET/
parallel** holding the pointing until the crew retakes it. CHOP = last abort point (the panel BREAKOUT
function, §4). Tune speedLimit ladder to the real keep-out/waypoint speeds.

**Phase 5 — Docked ops.** MechJeb essentially idle; **SmartASS OFF / KILL-ROT** or station attitude hold.
No planner burns. (Reboosts, if modelled, = `OperationPeriapsis`/`Apoapsis` + Node Executor.)

**Phase 6 — Undock & departure.** Real: undock → separation → departure burns → back away. → **SmartASS**
(retrograde/target) for the backout, then small **`OperationApoapsis`/`Ellipticize`** departure burns +
Node Executor to drop onto the pre-deorbit orbit. Tune: departure Δv small; keep clear of the Keep-Out Sphere.

**Phase 7 — Deorbit (trunk jettison → deorbit burn ~15 min → nose-cone close+lock).** Real: claw/trunk sep
~1 h 20 m before splashdown; deorbit burn ~50 min before splashdown (§8). → **`OperationPeriapsis`** (`new_
periapsis` = a negative/low entry-corridor Pe so the trajectory intersects atmosphere at the right angle) →
**Node Executor**; OR **Landing Guidance** targeted at the splashdown lat/lon for a site-accurate deorbit.
Tune: entry-corridor Pe / flight-path angle (too shallow = skip, too steep = over-g); the target splashdown
coordinates (one of 7 real sites). ⚠️ Model the trunk-jettison + nose-cone-close as sequenced vessel actions,
not MechJeb burns.

**Phase 8 — Entry.** Real: nose cone closed+locked, entry interface, heat-shield-forward. → **SmartASS
SURFACE/RETROGRADE** (heat-shield forward) attitude hold through peak heating/g; **Landing Guidance** runs the
descent prediction. **Lifting bank vs pure ballistic — RESOLVED (O8, owner, 2026-09-03, via the overseer):
attitude hold, NO active steering, is the BASELINE.** The nominal ballistic entry+landing is captured with
plain heat-shield-forward attitude hold and no commanded bank; active steering is added ONLY LATER, for
off-target cases (a future increment, not flight 1). Tune: entry attitude only — no bank at baseline.

**Phase 9 — Descent & chutes.** Real: 2 drogues → 4 mains at ~2 km (§8; matches the Manual Chute Deploy page).
→ **Landing Autopilot** (`DeployChutes` = True, **TouchdownSpeed** Crew-2 0.5) for the prediction/timeline, and
the **Manual Chute Deploy screen** for the crew-commanded drogue/main steps. Tune: chute-deploy altitudes
(the page's "(TBC)" gates) to the real Mark-3 schedule; TouchdownSpeed is a landing-detect threshold, not a
powered target (Dragon splashes ballistic under chutes). ⚠️ No powered landing — Landing Autopilot here is for
prediction + chute triggering, not a rocket-landing burn.

**Phase 10 — Splashdown & recovery.** Real: land under ≥3 mains → CUT MAINS after splashdown (§4 panel). MechJeb
done; conductor releases control. Tune: none.

**Cross-phase modules the conductor leans on:** **SmartASS** (attitude modes: SURFACE/ORBITAL/TARGET, pro/
retrograde, kill-rot — coast, entry, docking pointing), **SmartRCS + RCS Balancer** (translation/ullage on the
16 Dracos), **Node Executor** (flies every planned node), **Staging** (⚠ **prediction only — `Autostage` is
OFF and the StagingController never actuates**; the conductor separates and ignites directly, §B8/§B12.7),
**Warp Helper** (skip the
long phasing coast), **Flight Recorder** (the Q/AoA/pitch graphs that drive the §B8 ascent tune). ⚠️ Open
per-phase decisions to resolve empirically: phasing-orbit altitude ladder (P2), transfer optimize vs simple
(P3), the speedLimit ladder (P4 — auto-dock-default vs manual-override is settled, O6; only the ladder values
are still an empirical tune), chute-altitude schedule (P9). Each of these gets the one-parameter-at-a-time
flight-data tune (§B5) once built.

## B10. On-orbit modules — FULL per-parameter tuning guidance (mirrors §B8 depth)
Read against the LIVE Crew-2 cfg. **Two kinds of param:** *persisted* (in the cfg = the permanent store) vs
*live-driven* (cfg block EMPTY → the conductor sets it per phase via API each invocation; Maneuver-Planner
operations are ALWAYS per-invocation, never persisted). Each: what · units/range · default · Crew-Dragon
target · why · how-to-set (cfg key ⟂ API field).

### B10.1 Node Executor (`MechJebModuleNodeExecutor`) — flies every planned burn. cfg block EMPTY → defaults.
- **tolerance** (m/s) — cutoff: stop the burn when remaining node Δv < tolerance. MechJeb stock default **0.1**.
  Target: **~0.1** for big burns, tighten toward **0.05** for the fine rendezvous corrections (smaller = more
  precise, too small = chases engine/RCS noise and won't converge). How-to-set: API `NodeExecutor.tolerance`,
  or persist `tolerance{ValConfig..}` in the cfg block.
- **lead_time** (s) — starts the burn `lead_time` before the node's *half-burn* point so the impulse straddles
  the node; also the attitude-settle window. Stock default **3**. Target: **3–5 s** (Dragon points on RCS/
  Draco — allow settle). How-to-set: API `NodeExecutor.lead_time` / cfg.
- **autowarp** (bool) — time-warp to the burn. Default/target **True** (skip coasts). API `NodeExecutor.
  autowarp`. Ullage before ignition is handled by GuidanceController.UllageLeadTime (§B10.7), not here.
- Drive via `ExecuteOneNode()` (one burn, re-plan after) — the conductor's default so it can re-plan between
  rendezvous burns — vs `ExecuteAllNodes()`.

### B10.2 Maneuver Planner `Operation*` classes — per-invocation (never persisted); set fields then `MakeNodes()`.
- **OperationCircularize** — no params; TimeSelector (apoapsis/periapsis/altitude/computed). Insertion clean-up.
- **OperationPeriapsis / OperationApoapsis** — `new_periapsis` / `new_apoapsis` (m). Phasing-orbit shaping (P2)
  and deorbit-Pe (P7). Target: phasing Pe/Ap tuned so time-to-dock ≈ nominal; deorbit `new_periapsis` = a low/
  negative value putting entry FPA in-corridor.
- **OperationEllipticize** — `new_apoapsis`+`new_periapsis` (m). Set both apsides in one burn.
- **OperationSemiMajor** — `new_semi_major_axis` (m). Precise period/phasing control.
- **OperationInclination** (`new_inclination`, °) / **OperationLan** (`new_lan`) / **OperationPlane** (no params,
  TimeSelector = AN/DN) — plane control. Dragon: **OperationPlane** at the node = the real "Out-of-plane" burn.
- **OperationTransfer** — bi-impulsive Hohmann to target. `intercept_only` (bool — arrive, don't circularize),
  `simple_transfer` (bool — **False** to let it optimize the intercept vs a naive coplanar Hohmann),
  `period_offset`. TimeSelector `computed`. = the Phase/Transfer catch-up burn (P3).
- **OperationCourseCorrection** — fine-tune closest approach. `intercept_distance` (m — target closest-approach;
  set to the **4 km Approach-Ellipsoid** then walk down on later passes), `course_correct_final_pe_a`. Run 1–2×.
- **OperationKillRelVel** — no params; TimeSelector `closest_approach`. Null relative velocity at arrival (the
  Coelliptic/station-keep point). How-to-set (all): instantiate op, set fields, `MakeNodes(orbit, UT, target)`
  → Node Executor. ⚠️ Verify exact C# class names vs the pinned MechJeb source when embedding.

### B10.3 Docking Autopilot (`MechJebModuleDockingAutopilot`) — persisted: `speedLimit` only.
- **speedLimit** (m/s) — max approach speed cap. cfg **1**. Target: a **ladder DOWN through the corridor** —
  keep-out approach ~1, waypoints ~0.3–0.5, contact ~**0.1–0.2** (real Dragon closes very slowly). The single
  most important docking knob. How-to-set: cfg `speedLimit` ⟂ API `DockingAutopilot.speed_limit`.
- Non-persisted (running defaults, settable): **overrideSafeDistance/safeDistance** (m — keep-out radius),
  **overrideTargetSize/targetSize**, **forceRol/rol** (roll-align to the port). Target: enable roll-align to
  the IDA-2 port; safe-distance ≈ the Keep-Out Sphere. **Docking AP vs hand-off — RESOLVED (O6, owner,
  2026-09-03, via the overseer): the Docking Autopilot is the DEFAULT** for the Approach phase (§B12.3).
  Pressing the manual docking button switches to the **Manual ISS Docking screen** and **shuts down the
  Docking Autopilot** (couples to S28 screen behaviour, which T20 makes live) — a crew override, not a
  standing choice the conductor makes.

### B10.4 Landing Autopilot (`MechJebModuleLandingAutopilot`) — persisted; used for deorbit-targeting + chutes.
- **DeployChutes** (bool) — cfg **True**. Arm chute auto-deploy. Keep True (Dragon lands under chutes). API
  `LandingAutopilot.deploy_chutes`.
- **LimitChutesStage** (int, stage#) — cfg **0**. Don't deploy chutes before this stage. Target = the chute
  stage. **DeployGears / LimitGearsStage** — cfg True/0 but ⚠️ **Dragon has no landing gear (water splash)** →
  set DeployGears **False** or leave inert. **RCSAdjustment** (bool) — cfg True — RCS trims the descent.
- **TouchdownSpeed** (m/s) — cfg **0.5**. A landing-DETECT / final-settle threshold, **not** a powered target
  (Dragon splashes ballistic under mains — no propulsive landing). Leave ~0.5. API `landing_autopilot`.
- Landing SITE = a target lat/lon (via LandingGuidance/TargetController), not a field here → set the splashdown
  coordinates (one of 7 real sites) as the deorbit target. ⚠️ Landing AP here = deorbit-to-corridor + chute
  triggering + prediction, NOT a rocket-landing burn.

### B10.5 SmartASS (`MechJebModuleSmartASS`) — live-driven (cfg EMPTY); the conductor's attitude tool.
Modes (set `SmartASS.autopilot_mode`): OFF · KILL_ROT · NODE · ORBIT {prograde/retrograde/normal±/radial±} ·
SURFACE {surface_prograde/retrograde, horizontal±, vertical_plus, surface(custom heading/pitch/roll)} · TARGET
{target_plus/minus, relative±, **parallel_plus** = docking-axis aligned/minus}. Controls: **force_pitch/yaw/
roll** (bool), **surface_heading/pitch/roll** (° — pitch 0=horizon,90=up), **surface_vel_*** (° trims).
Phase map: coast = OFF or KILL_ROT · pre-burn pointing when not using Node Executor = NODE · entry (P8) =
**surface_retrograde** (heat-shield forward), attitude-hold baseline — **`force_roll` is NOT engaged at
baseline** (O8: pure ballistic, no commanded bank; `force_roll` is reserved for the later off-target-steering
increment, §B9 P8) · docking (P4) = **target_plus / parallel_plus** · departure (P6) =
retrograde/target_minus. No cfg persistence — pure API per phase.

### B10.6 RCS control — the 16-Draco translation/rotation. Persisted: RCSController PID + RCSBalancer.
- **RCSController** (attitude-hold-on-RCS PID) — cfg **Tf 1 · Kp 0.125 · Ki 0.07 · Kd 0.53**. The RCS attitude
  gains; tune only if Dragon is jittery/sluggish holding attitude on Draco. API fields Tf/Kp/Ki/Kd.
- **RCSBalancer** — cfg smartTranslation **False**, overdrive 1 (100%), overdriveScale 0.9, tuning factors
  (torque 1 / translate 0.005 / waste 1). Balances thruster groups for pure translation (prox-ops). Target:
  enable **smartTranslation=True** for clean docking translation if cross-coupling shows up. **SmartRcs** (cfg
  EMPTY, live) = the translate-toward-target helper during prox-ops. ⚠️ Tune during the P3/P4 empirical pass.
- **ThrustController** — cfg LimiterMinThrottle **True**, MinThrottle 0, DifferentialThrottle False. This is
  the setting O9 settles: MechJeb auto-throttle over PVG bang-bang, so `LimiterMinThrottle=True` is correct
  as-is, not a review item (§B8's throttle bullet). On-orbit: leave default.

### B10.7 Coast/warp/ullage helpers.
- **WarpHelper** (`phaseAngle` cfg 0) — auto-warp helper to skip the long phasing coast (P2). Set the lead/
  phase to stop warp before the next burn. API `warp_helper`.
- **GuidanceController.UllageLeadTime** (s) — cfg **20**. RCS-ullage/settle time before a main ignition (esp.
  after a coast, pressure-fed Dracos). Feeds every Node-Executor burn. Keep ~20 s; tune to real settle. Also
  ShouldDrawTrajectory True (display only).
- **FlightRecorder** — no tuning; it produces the Q/AoA/pitch graphs that drive the §B8 ascent tune and the
  rendezvous residual checks. The conductor reads it; nothing to set.

**Net:** the *persisted* store already carries a real docking/landing/RCS/attitude tune; the *live-driven*
knobs (Node Executor tolerance/lead, SmartASS modes, the Operation params, WarpHelper) are what the conductor
sets per phase. All ⚠️ targets converge in the §B5 one-parameter-at-a-time flight-data pass, per phase.

## B11. Flight-data TARGET reference — the numbers the §B5 one-by-one tune converges to
The real Crew-Dragon / Falcon-9 figures each locked parameter must reproduce. Tags: **[DOC]** = publicly
documented · **[EST]** = engineering estimate to VALIDATE in-sim (public sources don't publish it). Each line
maps target → the knob it constrains (§B8/§B10). Sources: NASA Commercial Crew blogs (Crew-12 ascent
milestones), Planetary Society / Space.com / NASASpaceFlight (approach geometry), Everyday Astronaut / The
Conversation (entry), CBS / Spaceflight Now (approach rates), the §8 mission timelines, the Crew-2 cfg.

**Ascent (Falcon 9 + Dragon) — constrains §B8:**
- Max-Q **[DOC]**: ~T+1:12, dynamic pressure **≈30–35 kPa at ~12 km**; F9 THROTTLES DOWN through max-Q. →
  the AoA-flat-through-max-Q pitch-rate tune + the throttle ⚠️ (F9's real throttle-down vs PVG bang-bang).
- MECO **[DOC]**: ~T+2:17, **alt ~80 km, vel ~Mach 10**. Stage sep ~2:21 · S2 ignition ~2:28 · **SECO-1 ~8:33**
  · in orbit ~9 min. → the staging/optimize-stage + coast timing.
- Insertion orbit **[DOC/cfg]**: **~190–210 km × 51.63°** (Crew-2 = 210/-51.6316). → DesiredOrbitAltitude/Incl.
- Peak axial accel **[EST]**: ~**4 g** near MECO and again near SECO. → IF we model an F9 g-limit, cap ≈ 4 g
  (else bang-bang). Validate against a flown accel trace.

**Rendezvous / approach — constrains §B10.2 (ops) + §B10.3 (speedLimit ladder):**
- **[DOC]** Approach Ellipsoid = **4 × 2 km** egg zone. Dragon halts at **~1 km** for the Go/No-Go before the
  Keep-Out Sphere. Keep-Out Sphere ≈ **200 m**. Waypoints: **WP0 = 400 m below**, **WP1 (docking axis) = 220 m
  ahead**, **WP2 = 20 m** from the port. → OperationCourseCorrection `intercept_distance` ladder (4 km → 1 km →
  220 m → 20 m) and the waypoint sequence.
- Approach-speed ladder **[DOC]**: enters KOS at **< 7.6 cm/s**, slows to **~5 cm/s** (these are cargo-Dragon
  BERTHING figures — ⚠️ crew DOCKING differs); Crew Dragon **final contact ~0.1 m/s**, and rate must stay
  **< 0.2 m/s inside 5 m** range. → the DockingAutopilot.speedLimit ladder (≈1 far → 0.3–0.5 mid → **0.1–0.2
  m/s** contact). Dock **~19 h** after launch; approaches **from behind & below**, loops to ahead. **[DOC]**

**Deorbit / entry — constrains §B10.4 + P7/P8:**
- Deorbit burn **[DOC]** ~**15 min** long-duration; Δv **~100 m/s [EST]** (validate). → OperationPeriapsis
  `new_periapsis` sized to the corridor.
- Entry interface **[DOC]**: **122 km (400,000 ft)** at **~7.8 km/s**. → the entry-FPA target.
- Entry flight-path angle **[EST]**: ~**−1.4° to −1.6°** inertial (corridor: too shallow skips, too steep
  over-heats/over-g). → the deorbit-Pe tune; validate the FPA in-sim.
- Peak entry decel **[DOC/EST]**: generic crew capsule ~**7–8 g** worst-case; **Dragon nominal ~4–4.5 g** →
  the entry attitude target (P8; pure ballistic, no commanded bank at baseline — O8). Heat shield ~1927 °C
  **[DOC]**.
- Trunk jettison before entry; **claw sep ~1 h 20 m** before splashdown; **deorbit ~50 min** before splashdown
  **[§8]**. → the P7 sequencing.

**Chutes (Mark 3) — constrains P9 + the Manual Chute Deploy page gates:**
- **[DOC]** 2 drogues first, then **4 mains at ~2 km**; land under **≥3** mains; **CUT MAINS** after splash.
  Drogue-deploy altitude is mission/energy-dependent (the page's High vs Standard schedules, "(TBC)"). → the
  chute-altitude gates + Landing AP `DeployChutes`/`LimitChutesStage`.

**Confidence summary:** ascent event times/altitudes, approach GEOMETRY, approach SPEED ladder, entry
interface, chute count/altitude = **[DOC]** (solid tune targets). Peak-g, entry FPA, deorbit Δv, drogue
altitude = **[EST]** — the four numbers to pin empirically in-sim against a flown telemetry trace during §B5.

## B12. Build architecture — the conductor (plan-only design; no code until build-go)
Grounded in the real seams: `_AutopilotStub.cs` (the static surfaces the screens compile against),
`pure/MissionPhase.cs` (the phase FSM + `AuthoritativePhase` resolver + real altitude/range constants),
`DragonScreenMonitor.cs` (the pure/glue law), `FlightCommands.Run(PanelCommand)` (the panel dispatcher),
`VesselData.cs` (the live-telemetry surface). **The autopilot is re-introduced by replacing the idle stubs
one controller at a time — the screen-facing contract already exists and does not change.**

### B12.1 Embedded MechJeb (pinned, privately-namespaced — per §B3, LOCKED)
- Vendor MechJeb2 **source at a pinned commit** into the DragonScreen build, compiled under a **private
  namespace + assembly** (e.g. `DragonScreen.Mech`) so it can NEVER clash with a user's installed
  `MechJeb2.dll`. Source kept intact (not rewritten) — rename shell only.
- Drive **headless**: attach/find ONE `MechJebCore` on the Dragon part, **no GUI**, enable only the modules
  the conductor uses. Reach modules via `MechJebCore.GetComputerModule<T>()`.
- The permanent tune = ship the `mechjeb_settings_type_Crew-Dragon.cfg` (the B7–B11 locked values) inside the
  mod; the conductor loads it, never the user.
- GPLv3 (§B2): public distribution ⇒ ship DragonScreen + the embedded MechJeb source under GPLv3; pin+record
  the exact upstream commit.

#### B12.1a T15's scope — FULL PORT, FULL SETTINGS AUTHORITY (owner, 2026-09-03, via the overseer)
Two directives, both binding on T15:
1. **A FULL AND COMPLETE MechJeb2 port from the most up-to-date GitHub source — everything, dead code
   included.** Do NOT vendor only the modules the conductor calls, and do NOT prune. A complete tree is what
   makes the pin meaningful and the GPLv3 source-shipping obligation clean, and it means a later conductor
   increment can reach a module nobody anticipated without a second port.
2. **The conductor edits and sets ALL user-editable settings**, behaving as an expert human at the MechJeb UI.
   Concretely: it drives the **Maneuver Planner** to build a multi-burn ISS rendezvous — **NOT the rendezvous
   autopilot**, which §B1 already rules out for RSS/RO. §B10.1–§B10.7 is the parameter surface it must be able
   to reach; the point of the full port is that nothing on that surface is out of reach.

**Three tensions this scope creates. Two are resolved here; one is an owner call.**
- **"Most up to date" vs "pinned" — RESOLVED: take the NEWEST source at port time, then PIN it and RECORD the
  commit** (hash + date + branch) in this section and in the shipped source header. The two words are not in
  conflict: "most up to date" governs *what you fetch*, "pinned" governs *what happens after*. There is no
  standing obligation to track upstream; a later re-pin is its own task.
- **WHICH repository — RESOLVED (owner, 2026-09-03, via the overseer; see G5a-Q1 in "Open questions for the
  owner" for the full research finding).** **T15 vendors upstream `MuMech/MechJeb2`, newest commit at port
  time, then pins and records it (§B12.1a's own "newest-then-pin" resolution above).** Researched, not
  assumed: no current or endorsed RO fork exists — `lamont-granquist/PrimerVectorMechJeb` (where PVG guidance
  was originally developed) has been **archived since 2021-07**, an experimental dev branch, not a maintained
  release; the RP-1 wiki's own `TroubleshootingMechJebPVG` page (already cited by §B8) points RSS/RO players
  at the **standard Sarbian/MuMech release** (`ksp.sarbian.com/jenkins/job/MechJeb2-Release`, CKAN 2.15+), not
  a fork. The one hard datum that motivated the question stands and is satisfied: the user's INSTALLED build
  has **PVG** (§B2, verified from `MechJeb2/Plugins/` + the Crew-2 cfg's `AscentTypeInteger = 1`), and upstream
  carries PVG natively — no fork was needed to get it, and C7 still forbids reading the install as a source.
- **HEADLESS IS MANDATORY EVEN THOUGH THE UI IS PORTED.** The full port brings MechJeb's whole GUI with it. It
  must be vendored but **never registered/shown**: a user who already runs MechJeb would otherwise get **two
  MechJeb UIs**, one of which they cannot configure and must not touch. The private namespace (§B3) prevents
  the assembly clash; suppressing the GUI is a separate, equally mandatory job. Ported ≠ enabled.

### B12.2 The conductor — two layers (honors the pure/glue split)
- **Pure core** `plugin/src/pure/Conductor*.cs` (NO Unity/MechJeb refs → headless-testable): the phase state
  machine + the re-plan decision logic. Input = a plain telemetry snapshot (extend `MissionInputs`/read
  `VesselData`) + current phase; output = a **ConductorAction** value (which module to engage, which
  Operation to build with which params, when to advance/hold/re-plan). All *decisions* live here.
- **Glue driver** `plugin/src/ConductorDriver.cs` (KSP/MechJeb-facing, thin, like the render path): each tick,
  read `VesselData` → ask the pure core for the `ConductorAction` → execute it against the embedded
  `MechJebCore` modules. It also **implements the `_AutopilotStub` surfaces** (`FlightDriver.MissionMode/
  RequestDeorbit/RequestAbort`, `DockingOps/DeorbitOps/UndockOps/StationApproach.Engaged+Note`, `AutoPilot.
  Engaged`) by reporting the core's state, and supplies `engaged`+`ActivePhase` to `Mission.AuthoritativePhase`
  so screen and autopilot can never disagree (rule T4).

### B12.3 Phase state machine (drives B9 ops with B7–B11 locked params)
Over `MissionPhase` (the enum already exists): **Prelaunch** → load profile + target ISS + arm PVG · **Ascent**
→ PVG ascent (§B8) · **Coast/Phasing** → Maneuver-Planner circularize/apsis ops + Node Executor (§B10.2, P2) ·
**Approach** → the rendezvous op chain Plane→Transfer→CourseCorrection→KillRelVel + Node Executor, **re-planned
live** (§B12.4), then hand off at the Keep-Out Sphere to the **Docking AP — the DEFAULT** (O6, §B10.3); the
crew's manual docking button overrides to the Manual ISS Docking screen and shuts the Docking AP down ·
**Docked** → idle/KILL-ROT · **Entry** → deorbit via OperationPeriapsis beforehand, then
SmartASS heat-shield-forward (§B10.5) · **Drogues/Mains** → chute triggering (the real 5486/1830 m constants
already in `MissionPhase.cs`) + the Manual Chute page · **Splashdown** → release control. Transitions are
gated by telemetry (the existing `Classify()` inputs) + crew Go/No-Go where the real mission holds.

### B12.4 The live re-plan loop (the main NEW logic — pure + tested)
During Approach, MechJeb's rendezvous *autopilot* is unreliable in RSS/RO (§B1), so the conductor composes
planner ops itself and **re-plans**: after each executed node, or when closest-approach error / node residual
exceeds a threshold, re-run the relevant Operation to regenerate the node → Node Executor. Pure decision rule
(testable): `if closestApproachErr > εd OR residual > tol OR drift → rebuild Operation k`. This is the
"expert user re-planning burns live" behaviour, expressed as deterministic pure logic.

### B12.5 Screen command front-end (replace the honest-refuse no-ops, one at a time)
`FlightCommands.Run` currently returns `false` (an honest no-op: click, no light, no action — §4, NO red) for
everything that would fly. The conductor turns
these into real dispatch, **one command per increment** (the stub's own rule): `DeorbitNow`/`WaterDeorbit` →
`FlightDriver.RequestDeorbit` → conductor enters the deorbit phase; `Abort`/`Breakout` → conductor abort/back-
off; chute/shroud/undock → real `Actuator`/`MissionOps`. The crew-gate buttons (`CrewProcedureOps` Go/No-Go/
Abort, `GatePhase`) advance the conductor's holds (1 km Go/No-Go, KOS entry, CHOP — §B11 waypoints). The
power/string/fire/entry-lamp commands STAY as-is (already real display state). Never wire a command whose real
function is still inferred/invented (§4) without an owner call.

**How an increment lands — the stub is a FACADE, not a placeholder (owner, 2026-09-03; §B12.8(a)).**
`_AutopilotStub.cs:143-150` declares the lamp surfaces the screens compile against — `AutoPilot`,
`StationApproach`, `DockingOps`, `DeorbitOps`, `UndockOps`, `BoosterRecovery` — each currently returning
`false` / `null`. These names STAY. A recovered or newly-written controller **registers INTO them**: the stub
class stops being a no-op and becomes a **thin adapter** that reports the live controller's state, so
**each increment flips exactly ONE facade property from constant-false to live** and **no screen file changes
at all**. That is CLAUDE.md's *"the contract the screens compile against does not change"*, taken literally —
the contract is these property signatures, and they are never renamed to match whatever the controller
underneath is called (§B12.8's two-generation rule).

### B12.5a HOW A FACADE GOES LIVE — THE ONE PROCESS (written once, followed everywhere)
*Added by **G6**, 2026-09-04, at the owner's request — "do it in the most logical way for success, maybe have
one chat write the whole process so it is streamlined" — rather than leaving each landing line to invent its
own idiom. **This is the single account.** Every later increment points here instead of re-explaining itself,
and `plugin/src/_AutopilotStub.cs`'s facade comment block points here too.*

**(i) THE SIX NAMES AND THEIR REAL FUTURE OWNER.** These are the properties at `_AutopilotStub.cs:143-150`.
The owner column is the increment that will actually flip each one — **not** a recovery line that will never
produce code. That distinction is the whole reason this table exists: W4's comments named *"no §B12.8 wave"*,
W11 re-pointed four of them at W18/W20/W21, and the owner's 2026-09-04 upper-stage decision (rider (d)) made
**both** answers wrong — those three lines are reference reads now and will never flip anything.

| Facade property | What it reports on the glass | The increment that flips it | What that increment must satisfy FIRST |
|---|---|---|---|
| `AutoPilot.Engaged` | AUTO SEQUENCE master lamp | **W10** (the read-only host + `CrewProcedureOps`), then **T17** binds the pinned core to it | a conductor that genuinely ticks and advances its gates; GO consumed on the frame it is pressed (W10's own rule: *a conductor with no controllers must not be ticked at all*) |
| `StationApproach.Engaged` / `.Note` | far-field → Keep-Out-Sphere approach | **T19** (on-orbit ops + re-plan loop) | §B9 Phase 3's planner chain composing (`OperationPlane` → `OperationTransfer` → `OperationCourseCorrection` → `OperationKillRelVel`), Node Executor flying the nodes, the §B12.4 re-plan loop live |
| `DockingOps.Engaged` / `.Note` | KOS-inward docking | **T20** (docking hand-off + `speedLimit` ladder) | the Docking Autopilot as DEFAULT per **O6** / §B10.3, and the manual-docking-button override that shuts it down |
| `UndockOps.Engaged` / `.Note` | undock → departure | **T21**, increment **1** | §B9 Phase 6: SmartASS backout + the small `OperationApoapsis`/`Ellipticize` departure burns → Node Executor, clear of the KOS |
| `DeorbitOps.Engaged` | deorbit → splashdown | **T21**, increment **2** | §B9 Phase 7: `OperationPeriapsis` (entry-corridor Pe) → Node Executor; then P8 attitude hold (**O8**) and P9 chutes |
| `BoosterRecovery.Tracked` | the HullCam's booster follow | **W9** (warp + focus glue), then §B16 | §B16.1's fresh booster core existing **on its own vessel**, and §B16.7's focus protocol (range extended, lands unfocused, +10 s, auto-recover, range restored) |

⚠ **`UndockOps` and `DeorbitOps` are ONE task, TWO increments.** §B12.5's one-property-per-increment rule is
about increments, not register lines: T21 flips `UndockOps` on the departure leg, then `DeorbitOps` on the
return leg. Never both in one step. (T21's title used to name only "Deorbit/entry/chutes" while its
DONE-when spanned §B9 Phases 6–10 — fixed per the register's **S73**, DONE 2026-09-04.)

**(ii) THE FIVE STEPS, IN ORDER.** An increment that cannot complete step 1 does not start step 2; it STOPS
and says so (C1.12).
1. **The controller exists and ticks.** Something real is behind the name and something real calls it every
   frame — W10's host for the Dragon, the booster's own script for §B16. A property may not go live because a
   controller merely compiles.
2. **The stub class becomes a THIN ADAPTER, not a rewrite.** The constant-`false`/`null` body is replaced by a
   read of the live controller's state, and **nothing else about the class changes**: same name, same property
   signatures, same file. This is §B12.8(a) — gen-2 controllers register INTO the gen-1 names.
3. **Exactly ONE property flips per increment** (§B12.5). A file that backs two names flips them in two
   sequenced increments.
4. **The comment for that name is rewritten IN THE SAME DIFF** — never as a later tidy-up. A facade comment
   and the behaviour it describes must not be able to drift apart; they have already drifted twice (W4, then
   W11), which is why this is a numbered step rather than an aside.
5. **`python plugin/build.py test` green, and the lamp is HONEST.** Engaged controller → lit lamp; idle or
   absent controller → dark lamp; **never lit ahead of the vehicle** (§14.4(a): click, no light, no action,
   and no red). If a preview page shows the lamp, re-render it and look at the PNG.

**(iii) THE COMMENT CONVENTION — one block, one entry per name, two forms only.**
```
//  <Name>  → <what really backs it> (<§ / R1 / task ref>). LIVE since <task id>, <date>.
//  <Name>  → <the increment that will back it>. NO-OP: <one sentence, the honest reason>.
```
The dark form's reason must name **an increment that will actually produce code** and say why the name is not
live yet in one sentence — not a wave label, not a recovery line that is only a read. Keep the block in
`_AutopilotStub.cs` directly above the class declarations, alphabetically stable, and keep the ⛔ footer that
states nothing below it is live.

**(iv) FOUR THINGS AN INCREMENT MUST NEVER DO.**
- **Never rename a facade property** to match whatever the controller behind it is called (§B12.8(a)).
- **Never add a parallel surface beside one** — a second "real" status class next to the facade is the same
  drift by another route.
- **Never half-wire one**: a property that reports engaged while the vehicle is not being commanded is worse
  than the honest no-op it replaced (§B12.8(a), §14.4(a)).
- **Never point a comment at a task that will never produce code.** That is the exact defect G6 fixed, and it
  is only detectable by reading the register — so check the owning line's verdict, not just its number.

### B12.6 Single-core safety, packaging, testing
- **One commanding core — THE RULE IS PER-VESSEL, AND THE VESSEL IS THE DRAGON.** Detect a user's own MechJeb;
  ensure exactly ONE `MechJebCore` actually commands **the capsule** (use ours; never double-drive).
  Belt-and-braces: our core is the private-namespace one. **Clarified 2026-09-03:** this governs the DRAGON
  only. It is **not** a ban on a second controller on a **different vessel** — §B16's booster flies on its own
  vessel with **its own compiled core and its own steering law** (§B16.1), which is not a MechJebCore at all,
  so it cannot be a violation of this rule. Read literally as "one core in the game", §B12.6 would forbid §B16
  outright; that is not what it means and never was.
- **Testing (mandatory, per the glue law):** headless tests for the pure conductor core (feed synthetic
  telemetry+phase, assert the `ConductorAction` + phase transitions + re-plan triggers) — the analogue of
  `FigmaUINavTest`. The glue driver stays thin enough to eyeball. `python plugin/build.py test` must stay green.
- **Build order (when build-go given):** (1) embed+namespace+headless-load one MechJebCore, prove it loads the
  Crew-Dragon cfg; (2) pure conductor core + tests (phases as pure decisions); (3) glue driver implements the
  stub surfaces read-only (report phase/engaged) — no commands yet; (4) wire Ascent (PVG) end-to-end + verify
  in-sim; (5) wire on-orbit ops + the re-plan loop; (6) docking hand-off; (7) deorbit/entry/chutes; (8) begin
  the §B5 one-parameter-at-a-time empirical tune against §B11 targets — which, per the 2026-09-03 gate opening
  (§0), is **DEFERRED until after the first recorded flight**: steps (1)–(7) are built at **RO defaults**.
  **(9) BOOSTER RECOVERY — §B16**, the owner's 2026-09-03 scope addition: a SEPARATE-VESSEL autopilot, so it is
  its own track, NOT a phase of (1)–(7). It cannot start before (1)–(4) (the embed + a flown ascent); it may be
  built before or after (8) — the owner's call. **Its two former prerequisites are both now closed:** the craft
  dump is **in the repo** (§B16.4) and the **guidance decision is SETTLED** (§B16.5).
  ⚠ **And (1)–(9) are all preceded by the RECOVERY WAVES of §B12.8** — W0 (done) then Waves A–D. Part B's first
  code is a recovery, not a green field; §B16 is **Wave C**. Each step preview/test-gated; install + glass time only when a step needs the capsule
  (and only on a separate owner go); commit LOCALLY with `git commit`, never `git push` (C1.5).

### B12.7 Direct part control — NO staging, NO action groups (owner directive, 2026-09-03)
> **The autopilot NEVER stages and NEVER fires an action-group binding to actuate the vehicle. It reaches the
> live PART MODULES and calls them.**

Binding on **all** of Part B — the conductor, the abort path (§B13.4), the chutes, the nose cone, and §B16's
booster. It is the general rule that §B16.3's engine-mode ban is one instance of.

**This is not new work — it already exists, and it is recovery, not invention.** `docs/AUTOPILOT_RECOVERY_AUDIT.md`
§3.1 confirms `plugin/src/Actuator.cs` at `8b81816^` — **868 lines, 37 public methods**, verdict
**RECOVER-CODE, highest priority** — does exactly this, and carries the rule as its own header hard rule. Its
coverage is effectively complete for this stack: engines (`ActivateEngines`/`ShutdownEngines`/`FindEngine`/
`IgniteOctawebLiftoff`/`ShutdownBoosterEngines`), separation (`SeparateBooster`/`FireDecoupler(role)`/
`SeparateDragon`/`JettisonTrunk`/`Undock`), pad (`ReleaseHoldDowns`/`OpenErector`), abort (`FireAbort`), shroud
(`Open/Close/ToggleNoseShroud`), RCS, deployables (legs, **grid fins**, panels, antennas) and chutes
(`DeployChutes`/`CutChutes`). It was the actuation path on every recorded RSS-RO flight.
- **Its missing dependency:** `pure/Actuation.cs` — the capability→role classifier (`EngineRoleOf`,
  `DecouplerRole`) — was **deleted with it**, as was `test/ActuationTest.cs`. All three come back together
  (§B12.8 Wave B) or none of them do.
- **Its surviving dependency:** `pure/VehicleParts.cs` **was not deleted** and is live in today's tree.
- **Its collision:** today's `_AutopilotStub.cs` declares a no-op `Actuator`; recovering the real one retires
  that stub class (§B12.8(a)).
- **The ONE documented exception, and it stays:** the RCS *master* toggle (`KSPActionGroup.RCS`). A thruster
  only answers translation input while the vessel-level RCS flag is set, so `EnableRcs` sets both the
  per-thruster `rcsEnabled` **and** the master — a stock vessel enable, not a VAB-dependent AG binding, which
  is the class of thing the rule forbids. MechJeb does the same in every controller that translates on RCS.

**THE BINDING RULE — the dump is the SPECIFICATION, the runtime lookup is the BINDING.**
- `docs/reference/craftdump.csv` tells you **what the vehicle is**: which parts exist, which modules they
  carry, which events/actions/fields those modules expose. That is what you design against.
- The flight software **finds its parts on the LIVE vessel at runtime**, by **module type and identity** —
  `ModuleEngines`/`ModuleEnginesRF` (+ `engineID`), `ModuleDecouple` / custom decouplers, `RealChuteModule`,
  `ModuleAnimateGeneric`, part **name** where the name is the identity. Resolve ONCE at the phase boundary
  into a named table; never re-search per frame.
- ⛔ **NEVER hardcode a `persistent_id` (or any other dump-local index) as the binding key.** Those change
  between craft revisions — the 23:21 2026-09-03 dump is already a different vessel revision from the 26 Aug
  one — so a hardcoded id **breaks silently the first time the craft is edited in the VAB**, which is the worst
  available failure mode: no compile error, no exception, just an actuation that quietly addresses nothing.

**⚠ The escalation, stated plainly: because control is direct, the craft dump gates essentially ALL Part-B
actuation** — ascent separation and ignition (§B8's autostage-off rule), trunk jettison, the nose cone, the
chutes, the abort motor — **not just the booster**. §B16.4 previously carried this as a booster-only
dependency; it is not.

**Where the dump now is (C7 satisfied).** It is **IN the repo**: `docs/reference/craftdump.csv`, regenerated by
**W0** (2026-09-03) via the recovered `plugin/src/CraftDump.cs`. §B16.4's older *"the owner supplies it / STOP
and ask"* wording is amended there accordingly.
⚠ **The craft is a WORK IN PROGRESS (owner, 2026-09-03) and a RE-DUMP IS PENDING.** The current dump holds
**20 parts** and is **missing the drogues, the mains, the trunk adapter/decoupler and all four S2 RCS
thrusters**. So: design the actuation against the METHOD and the parts that are visibly there, and **make no
claim about which chute, decoupler or RCS parts exist** — those bindings are resolved at runtime against the
current dump, and the sections that need them say so rather than naming parts that may not survive the
re-dump (§1.4: invent no part names).
ℹ **A partial cross-check exists, and it is evidence, not a part table.** The 16 owner-supplied
`docs/reference/<mission>.loadmeta` files each carry a `partNames` + `partModules` manifest for their
`.craft` (e.g. `Crew-2.loadmeta`: 27 parts, 9 stages) — and those manifests **do** list parts the 20-part dump
lacks. Use them to know **what the vehicle is meant to contain**; do **not** promote a name from them into a
binding. The dump is the specification and the runtime lookup is the binding (above), and the **re-dump** is
what makes the dump authoritative again.

### B12.8 Part B starts from RECOVERY, not from scratch (owner, 2026-09-03, via the overseer)
`docs/AUTOPILOT_RECOVERY_AUDIT.md` (R1) inventoried the flight software deleted on 2026-09-01 and found
**103 files classified RECOVER-CODE** — pure guidance, glue and tests — plus 77 more to read as evidence
without making them live. **Part B's first code is therefore a RECOVERY, in DEPENDENCY-ORDERED WAVES**, each
ending green under the preview-only gate (§0). G5c writes the register lines; this section fixes the shape.
⚠ **Amended by W11, 2026-09-04:** the original shape was *four* waves, *one register task each*. Waves A–D are
still one task each, but cross-checking R1 §5.2 row by row showed **ten RECOVER-CODE glue files in no wave at
all** — so `FlightDriver.cs` went to **W10** and the other nine became **Wave E, one register line per file**
(rider **(c)**), on the owner's decision of 2026-09-04 via the overseer.
⚠ **Amended again by G6, later on 2026-09-04:** on the owner's upper-stage/booster decision of that date,
**five of Wave E's nine lines were re-verdicted RECOVER-CODE → RECOVER-REFERENCE** — they are read and mined,
never made live. Wave E is now **four code lines + five reference lines**. The decision, the reasoning and the
supersession of R1's five rows are rider **(d)**.

**W0 (`plugin/src/CraftDump.cs` + a fresh dump) is already DONE** and sits ahead of Wave A — §B12.7's binding
rule has nothing to design against without it.

| Wave | Contents | Why here |
|---|---|---|
| **A** | Collision-free `pure/` support: `Vec3`, `Conic`, `Trajectory`, `BoosterDrag`, `Predict`, `Aero`, `Authority`, `Lambert`, `Maneuver`, `Lvlh`, `Cw` — **plus their tests** | Nothing depends on these and they collide with nothing in today's tree, so the wave is pure gain and provably green. `Trajectory` + `BoosterDrag` are the prediction engine §B16.5 now commits to (§B16.8). |
| **B** | `pure/Actuation.cs` + `Actuator.cs` + `test/ActuationTest.cs` | §B12.7's actuation layer. Retires the `Actuator` stub — the first real facade swap. Everything that flies needs it. |
| **C** | The booster set — `pure/BoosterDescent.cs`, `pure/Hoverslam.cs`, `pure/GridFin.cs`, `test/BoosterTest.cs` (and §B16.8's provenance marking) | §B16. Depends on A (prediction) and B (per-engine actuation). Never flown (§B16.8) — recovered as a **starting point**, not as working code. |
| **D** | The conductor set — `ModeManager`, `WarpPlan`, `CoastEta`, `MissionConductor`, `CrewProcedureOps` | **This is where most stub collisions land**, so it goes last, when A–C have already proven the pattern. |
| **E** — **NINE register lines, one file each** (not one task): **FOUR code + FIVE reference** | The remaining `plugin/src/` glue R1 §5.2 verdicts RECOVER-CODE. **RECOVER-CODE (4):** `GeometryDump` (W13, done) · `DeployablesControl` (W14) · `LandingSiteScan` (W15) · `AbortControl` (W19) — each with its own §5.1 pure half and its own test. **RECOVER-REFERENCE (5), re-verdicted by G6:** `EntrySteering` (W16) · `DeorbitBurn` (W17) · `ReturnControl` (W18) · `RendezvousControl` (W20) · `DockingControl` (W21) — read, mined and quoted into `docs/MECHJEB_MISSION_TUNING.md`; **no `.cs` file lands.** | Added by **W11**, 2026-09-04: waves A–D never contained these, so **four facade names had no owner at all**. They are too heterogeneous for one task (a 40 KB rendezvous controller beside an 8 KB read-only diagnostic) and one lumped line is the C1.7 compaction failure mode — so **one register line per file**, each flipping at most ONE facade property. Order + dependencies: rider **(c)**. ⚠ **Amended by G6, 2026-09-04:** the owner's upper-stage/booster split gave every *manoeuvre* in Wave E to MechJeb, so five lines became **reference reads** and **no Wave E line flips a facade property any more** — the facades moved to T19/T20/T21 (§B12.5a). Rider **(d)**. |

**(a) THE STUB NAMES ARE THE DISPLAY-FACING FACADE — keep them.** `_AutopilotStub.cs:143-150` holds the
**gen-1** names the screens compile against (`AutoPilot`, `StationApproach`, `DockingOps`, `DeorbitOps`,
`UndockOps`, `BoosterRecovery`); the recovered controllers are **gen-2** and are named differently. The
resolution: **gen-2 controllers register INTO the gen-1 facade.** The stub becomes a thin adapter rather than a
no-op, each controller flips one facade property live (§B12.5), and **the screens never change**. Do not rename
a facade property to match a controller, and do not add a parallel surface beside it.

**(b) TWO FILES ARE NOT RECOVERED AS-IS — and this is the whole reason to state it up front.**
- ⛔ **`Steering.cs` is NEVER recovered.** Its replacement is **written fresh** against the pinned MechJeb.
  Reason (R1 §7.2): its **last committed state is `UseGimbalLoop = false` — attitude handed to stock SAS**.
  Recovering the file silently re-imports that decision, which is precisely the arrangement Part B replaces
  with MechJeb's `BetterController`. It is read as reference; it never becomes live.
- ⚠ **`AscentControl.cs` is recovered WITH THE ROLL-TRIM BLOCK (`:397-414`) REMOVED — removed, not fixed.**
  That block is R1 §7.1's named, located, **unfixed** defect (*"sawtooths roll to 27.5 dps + toggles RCS 17× +
  2 Hz gimbal chatter = the shake"*); the fix was deliberately withheld at the time because it *"touches PROVEN
  ascent"*. **Flag, loudly:** ascent control is the **ONLY flight-validated subsystem we have** (R1 §4.2 —
  DB-validated, `pe_p95 < 0.4°`). Cutting code out of it is therefore not a tidy-up. It gets **its own register
  line and its own test**, and it is **never a quiet deletion inside another task's diff**.

**(c) WAVE E — THE NINE REMAINING GLUE FILES, AND WHY THEY LAND IN THIS ORDER (W11, 2026-09-04).**
R1 §5.2 verdicts **sixteen** `plugin/src/` files RECOVER-CODE. Six already have an owner — `Actuator` (Wave B,
done), `CraftDump` (W0, done), `AscentControl` (**W7**), `Ullage` (**W5**), `MissionConductor` (Wave D /
**W9**), `CrewProcedureOps` (Wave D / **W10**). **`FlightDriver.cs` (59,523 B, R1's "the Part-B host") is
owned OUTRIGHT by W10** — the host is not a Wave E file, and no Wave E line restores it; each Wave E line
instead **grows W10's read-only host** by exactly the dispatch its own controller needs (§B12.6 step (3) →
§B12.5, one increment at a time). The nine that remain are Wave E, and they are ordered by **verified
dependency**, not by mission phase:

| # | Line | File (bytes) | Its §5.1 pure half | **Verdict (G6, 2026-09-04)** | Facade flipped |
|---|---|---|---|---|---|
| 1 | **W13** | `GeometryDump.cs` (8,122) | — (`pure/Authority.cs` already in tree) | **RECOVER-CODE** (DONE) | none |
| 2 | **W14** | `DeployablesControl.cs` (2,828) | — (drives the live `Actuator`) | **RECOVER-CODE** — actuation, not a manoeuvre | none |
| 3 | **W15** | `LandingSiteScan.cs` (4,364) | `SafeLandingSite.cs` | **RECOVER-CODE** — splashdown-site data both return paths need | none |
| 4 | **W16** | `EntrySteering.cs` (9,687) | `Entry.cs` | 🔁 **RECOVER-REFERENCE** — entry is §B9 P8 SmartASS hold (**O8**); mined for the 4-band L/D prior | none — never had one |
| 5 | **W17** | `DeorbitBurn.cs` (6,733) | `DeorbitGuidance.cs` | 🔁 **RECOVER-REFERENCE** — §B9 P7 `OperationPeriapsis` + Node Executor; mined for the DS-DEO-001 units bug + the entry-corridor Pe | none — never had one |
| 6 | **W18** | `ReturnControl.cs` (22,827) | `Departure.cs` + `Chutes.cs` | 🔁 **RECOVER-REFERENCE** — §B9 P6–10 are MechJeb's; mined for the return ORDER + the chute schedule | **moved to T21** (undock inc. 1, deorbit inc. 2) |
| 7 | **W19** | `AbortControl.cs` (23,536) | `AbortResponder.cs` | **RECOVER-CODE** — abort is a conductor state, not a MechJeb call (§B13); flight-validated (R1 §4.2). **Now also lands `DeorbitGuidance.cs` + `Chutes.cs`**, orphaned by W17/W18 | retires the `AbortControl` + `AbortMode` **stub** |
| 8 | **W20** | `RendezvousControl.cs` (40,628) | `Rendezvous.cs` + `Phasing.cs` + `RvIntercept.cs` + `NavFilter.cs` | 🔁 **RECOVER-REFERENCE** — §B9 P3 planner composition (§B1); ⭐ mined for the **only real RSS-RO rendezvous experience in the project** (flown far-field to 109 km) | **moved to T19** |
| 9 | **W21** | `DockingControl.cs` (15,447) | `DockApproach.cs` + `DockCapture.cs` + `DockControl.cs` + `DockCorridor.cs` | 🔁 **RECOVER-REFERENCE** — Docking AP is DEFAULT (**O6**, §B10.3); mined for the two §1.4 VERIFIED-REAL gates (IDSS envelope, KOS corridor) | **moved to T20** |

**The spine of the order is `Steering.cs`, and it is a measured split, not a preference.** Rider (b) says
`Steering.cs` is **NEVER recovered**; its replacement is written fresh against the pinned MechJeb (**T15**).
Reading the nine files at `8b81816^`, **six of them call it** — `ReturnControl` (13 distinct members),
`AbortControl` (`Point`×6, `Up`×3, `Prograde`×2, `PointingErrorDeg`, `PointNoRoll`), `RendezvousControl`
(`Point`×5, `PointingErrorDeg`×4, `Prograde`×2, `Release`), `DeorbitBurn` (`Point`, `PointingErrorDeg`×2,
`Up`), `DockingControl` (`Point`, `PointingErrorDeg`, `Release`) and `EntrySteering` (`Up`×2 — **geometry
only**). So:
- **Lines 1–4 need NO attitude channel** and can land before T15: W13 is isolated (its own `[KSPAddon]`,
  **zero** references to `Steering`/`Actuator`/`FlightDriver` — it needs no host at all); W14's only calls are
  `Actuator.DeploySolarPanels` / `.DeployAntennas` / `.RetractSolarPanels`, **all three present in today's
  `src/Actuator.cs` with matching signatures**; W15 calls only `pure/SafeLandingSite.cs`; W16 calls only
  `Steering.Up`, a body-up unit vector that is geometry, not a command.
- **Lines 5–9 all command attitude** and are therefore **gated on T15's fresh steering layer**. A Wave E line
  must not close that gap by reviving `Steering.cs`, and must not half-wire the facade (rider (a)).
  ⚠ **MOOT FOR FOUR OF THE FIVE since G6, 2026-09-04.** W17, W18, W20 and W21 are **reference reads** now —
  they restore nothing, so **nothing of theirs will ever command attitude** and the T15 gate simply does not
  apply to them. It survives on exactly **one** line: **W19**, the only remaining Wave E code line that points
  the vehicle. The dependency analysis below is left standing as the record of *why* the order was built that
  way; it is no longer a schedule constraint for the four.
- ⚠ **`Steering.cs` is also ReturnControl's ENTRY STATE BUS, not just its attitude channel.** It calls
  `Steering.PredictDownErrAtBank`, `MeasuredBankRad`, `MeasureBc`, `LastSigmaRad`, `FootprintError`,
  `EntryLoverD` and `SetSplashTarget` — the bank/footprint measurement R1 §5.2 names as **EntrySteering's own
  job**, parked in the one file that never comes back. **W18 must say where that state lives instead**; this is
  a design decision inside the line, not a rename.

**Three more rules Wave E inherits, stated so a later chat cannot lose them:**
1. **A line carries its own pure half and its own test.** The §5.1 files above are RECOVER-CODE and are in no
   wave either (the same gap **W6** found for `pure/CourseCorrect.cs`); rather than a second pure wave, **the
   first Wave E line that needs a pure file lands it**, and later lines consume it. Wave A's `Cw`, `Lvlh`,
   `Lambert`, `Maneuver`, `Trajectory`, `Authority` are already in the tree and are not re-landed.
2. **One facade property per increment (§B12.5) survives a file that backs two.** `ReturnControl.cs` backs
   **both** `UndockOps` and `DeorbitOps`. It lands on ONE register line — one file, one restore — but flips the
   two properties as **two sequenced increments**, undock/departure first, deorbit/return second. §B12.5's rule
   is about increments, not about register lines.
3. **Every recovered constant is UN-CONVERGED for RSS-RO unless R1 records it as flown** (§B16.8 ruling 2's
   marking, applied outside the booster too). In Wave E that binds `pure/Entry.cs`'s 4-band L/D schedule
   (R1 §7.4 — honestly self-marked, still unmeasured) and `pure/NavFilter.cs`'s noise tunables (R1 §7.4 —
   regime **unstated**, a defect). `pure/Terminal.cs` stays **RECOVER-REFERENCE**: its own text says its
   altitudes are Kerbin's.

✅ **W11's open question 1 — how much of `RendezvousControl` / `DockingControl` is actually recovered — is
ANSWERED: neither, as code.** W11 scoped both lines to what held under either answer and placed them last so
the answer would arrive first; it did, on 2026-09-04 (rider (d)). §B12.3 + §B10.3 (**O6**, owner 2026-09-03)
make the **Docking AP the DEFAULT** from the Keep-Out Sphere inward, and §B12.4 has the conductor compose
MechJeb **planner ops** for the approach because MechJeb's rendezvous *autopilot* is unreliable in RSS/RO —
and the owner's upper-stage decision confirms both. So `DockingControl.cs` (**never flown**) and
`RendezvousControl.cs` (flown far-field to 109 km only) are **RECOVER-REFERENCE**: not even the facade adapter
is restored, because `StationApproach` and `DockingOps` moved to **T19** and **T20** (§B12.5a). What survives
is the **extraction** — `DockCapture`'s IDSS envelope and `DockCorridor`'s KOS-breach geometry (both §1.4
verified-real, and MechJeb's Docking AP does not supply them), the waypoint/speed ladder that tunes
`speedLimit`, and ⭐ the far-field RSS-RO rendezvous experience that tunes the §B9 P3 planner chain.

**(d) THE UPPER-STAGE / BOOSTER SPLIT — FIVE WAVE E LINES ARE RECOVER-REFERENCE (owner, 2026-09-04, via the
overseer; applied by G6).** Stated twice and confirmed:

> **"We use MechJeb for ALL UPPER STAGE MANOEUVRES as planned. BOOSTER SCRIPTED."**

Explicitly: **launch → rendezvous → docking → undocking → re-entry orbit** (Manoeuvre Planner, then execute
next node) **→ re-entry → landing are ALL MechJeb.** The booster's return and recovery is **our own scripted
`.dll`**, running at separation. **Two systems that must not interfere with each other's flights.** ⚠ The owner
briefly floated our own `.dll` doing rendezvous and then **RETRACTED** it — **the retraction is the decision**;
the retracted version is not implemented, and a later chat reading only the first half of that exchange must
not revive it.

**This RESTORES the plan rather than changing it.** §B1, §B9 Phases 2–10, §B10.2, §B10.3 (O6), §B12.3 and
§B12.4 always had MechJeb flying the Dragon mission; §B16 always had the booster as a separate scripted vessel.
What it corrects is the *recovery* plan built on top: **five of Wave E's nine lines were pointed at manoeuvres
MechJeb owns.** Their verdicts move **RECOVER-CODE → RECOVER-REFERENCE** — read, mined, quoted, **never made
live** — with the MechJeb owner of each job named beside it:

| Line | File | The MechJeb path that owns the job |
|---|---|---|
| **W16** | `EntrySteering.cs` | §B9 P8 — SmartASS SURFACE/RETROGRADE attitude hold, **no** active steering (**O8** baseline) |
| **W17** | `DeorbitBurn.cs` | §B9 P7 — `OperationPeriapsis` → Node Executor (literally *"Manoeuvre Planner, then execute next node"*) |
| **W18** | `ReturnControl.cs` | §B9 P6–10 — departure burns, deorbit, entry hold, Landing Autopilot for chute prediction |
| **W20** | `RendezvousControl.cs` | §B9 P3 / §B1 — Maneuver Planner op composition + Node Executor, re-planned live (§B12.4) |
| **W21** | `DockingControl.cs` | §B9 P4 / §B10.3 — the Docking Autopilot as DEFAULT (**O6**) |

**⚠ A RE-VERDICT IS NOT A DELETION, AND THE REFERENCE VALUE IS THE POINT.** Each of the five register lines
must say what its file is still good **FOR**, not merely that it is not restored:
- ⭐ **`RendezvousControl.cs` was flown far-field to 109 km in RSS-RO** (R1 §5.2, *"the coarse rendezvous
  controller"*). **That is the only real RSS-RO rendezvous experience that exists anywhere in this project** —
  every other number in §B9 Phase 3 is a target or an estimate — so it is directly useful for **tuning** the
  planner composition against §B9 P3 / §B11 targets. Its terminal leg was never flown; that boundary is stated,
  not blurred.
- **`DeorbitBurn.cs`'s units bug** (DS-DEO-001: the SuperDraco throttled, *"196.9→196.1 km unchanged) → the crew
  stranded"*) and **`EntrySteering.cs` / `pure/Entry.cs`'s 4-band L/D prior** (R1 §7.4, honestly self-marked and
  still unmeasured) are **lessons that are cheaper to read than to rediscover in flight**.
- **`DockingControl.cs` carries two §1.4 VERIFIED-REAL sources** MechJeb does not supply: `pure/DockCapture.cs`'s
  **IDSS IDD Rev E Table 3.3.1.1-2** soft-capture envelope and `pure/DockCorridor.cs`'s corridor / keep-out
  geometry. The Docking AP flies an approach; it does not know the envelope it must arrive inside.
- **`ReturnControl.cs` holds the only end-to-end ORDERING of the return** — the spec T21's sequencer is built
  against — and `pure/Chutes.cs` is the one return leg with real recorded data (chute descent, in the aborts).

**Three consequences, stated so nothing is lost in the change:**
1. **No Wave E line flips a facade property any more.** `UndockOps` + `DeorbitOps` → **T21**, `StationApproach`
   → **T19**, `DockingOps` → **T20** (§B12.5a). W11's per-line "replace that name's W4 comment as it flips"
   clauses are superseded; G6 rewrote all six comments once instead.
2. **W18's entry-state-bus problem is RETIRED, not deferred.** With nothing restored there is no
   bank/footprint state to re-home off the never-recovered `Steering.cs`: the measurement is MechJeb's Landing
   Guidance prediction (§B10.4), plus `pure/Entry.cs`'s method as *recorded* by W16 if an off-target bank
   increment is ever built.
3. **W19 inherits two orphaned pure files.** `pure/DeorbitGuidance.cs` (W17) and `pure/Chutes.cs` (W18) are no
   longer landed by anyone, so rider (c) rule 1 — *"the first Wave E line that needs a pure file lands it"* —
   makes **W19** land both with its own tests. **W19 itself stays RECOVER-CODE**: abort is not in the owner's
   manoeuvre list, §B13 states outright that *"abort is a conductor state, not a MechJeb call"* actuated by
   §B12.7 direct part control, and it is one of only two flight-validated subsystems we have (R1 §4.2).

**⛔ R1 IS SUPERSEDED ON THESE FIVE ROWS, NOT FALSIFIED — and it is NOT rewritten.**
`docs/AUTOPILOT_RECOVERY_AUDIT.md` inventoried deleted code and was never told which jobs MechJeb would take;
its RECOVER-CODE verdicts were **correct on the evidence it had**. This rider supersedes the five rows above.
**No byte of R1 was edited**, here or in the register — a historical audit stays a historical record, and the
supersession lives where the current plan lives.

**Where the extractions go.** Each of the five lines writes its findings into
`docs/MECHJEB_MISSION_TUNING.md`'s matching phase section — §3 (rendezvous), §4 (docking), §6 (undock/
departure), §7.1–§7.4 (deorbit / entry / chutes / splashdown) — as a block headed
`[FROM DELETED GEN-2 <file> — REFERENCE ONLY, NEVER LIVE]`, with `8b81816^` provenance and §B16.8 ruling 2's
UN-CONVERGED marking on every constant that never flew. That doc is the per-phase MechJeb recipe (§B4's
"expert operator's flight book"), which is exactly where a tuning input belongs. **The plan still wins on any
conflict of numbers** (C7.1).

**The two-generation rule (R1 §0.2) — it prevents a build break, not a style problem.** Two generations of
flight software **share class names**. **GEN 2** (newest at `8b81816^`) is the recovery target and the whole of
the table above. **GEN 1** (newest at `158eb2a^`) is **never restored as code** — taking both produces
duplicate types and the build fails. Gen 1's only role is as reference, and the gen-1 *names* survive solely
as the display facade in (a).

## B13. Abort system — research + conductor design
The Crew-Dragon Launch Abort System (LES) + on-orbit contingency aborts, and how the conductor implements them.
Sources: Wikipedia *Crew Dragon Launch Abort System*, CBS *rescue scenarios*, NASA escape-system release,
Space.com Demo-2 steps, the §4 panel research. **Abort is NOT a MechJeb module** (MechJeb has none) — it is
conductor-owned, composed from the KSP abort action-group + SmartASS + the chute logic, with the mode
PHASE-SELECTED exactly as the real vehicle autonomously selects it. **Corrected 2026-09-03:** an earlier
version of this line said abort was composed from the *KSP abort action group*; it is not. Abort actuates
through **§B12.7 direct part control** — the SuperDraco engine modules and the decouplers by name — plus
SmartASS and the chute logic. No action group, no staging.

### B13.1 Hardware & trigger
- **8 SuperDraco engines, 4 pods of 2**, side-mounted, **71 kN each** (~16,000 lbf), hypergolic NTO/MMH,
  pressure-fed (He), throttleable, 3D-printed chambers. **Pusher, RETAINED** (never jettisoned — unlike tower
  systems); the **trunk stays attached during abort** for aero stability. Burn: ~6–9 s nominal push (up to
  ~25 s capability — ⚠️ source spread; treat as "a short high-g push").
- **Full-envelope:** ~T−40 min (pad) through orbital insertion.
- **Trigger:** AUTONOMOUS (flight computer detects booster malfunction → auto abort) **OR** MANUAL via the
  **pull-and-twist handle between the seats = the panel EJECT handle** (§4, CONFIRMED-real). Draco thrusters
  reorient/stabilize the capsule after the SuperDraco push, then drogues → 3+ mains → water landing.

### B13.2 The 8 abort modes (phase-selected; = §4 "EJECT, 8 modes")
| Mode | T+ window | Capsule action | Splashdown |
|---|---|---|---|
| Pad Abort | T−~37 min | SuperDraco ignition, push off pad | few mi E of Cape Canaveral |
| Stage 1a | 0 → ~1:15 | SuperDraco push-away from live F9 | FL → N. Carolina |
| Stage 1b | ~1:15 → 2:32 | SuperDraco push-away | Virginia coast |
| Stage 2a | ~2:32 → 8:05 | benign SEP from S2 + post-sep burn | NE U.S. coast |
| Stage 2b | ~8:05 → 8:28 | sep + retrograde burn | Nova Scotia (back-fly ≤200 nm) |
| Stage 2c | ~8:28 → 8:38 | sep + prograde burn | W. Ireland (fly forward) |
| Stage 2d | ~8:38 → 8:44 | sep + retrograde burn | W. Ireland |
| Stage 2e | T+8:44+ | SuperDraco + Draco | **ABORT TO ORBIT** |
Transition: pad/1a/1b = SuperDraco push off a live/failing booster; **2a onward = benign SEPARATION from a shut
second stage + Dragon's own Draco/SuperDraco burns** to shape a survivable trajectory (avoiding the N-Atlantic
exclusion zone by flying back to Newfoundland or forward to Ireland); **2e ≈ enough energy to reach orbit** →
hand back to the normal on-orbit conductor. Tests: Pad Abort May 2015; **IFA Jan 19 2020** (sep at T+1:26 at
max-Q, 42 km apogee, Atlantic); C204 was a **ground-test anomaly** Apr 2019 (not the pad-abort flight).

### B13.3 On-orbit / proximity contingency aborts (distinct from the LES)
- **DEORBIT NOW / WATER DEORBIT** (§4 panel, CONFIRMED-real) = immediate/contingency return from orbit — NOT
  SuperDraco; the conductor runs the deorbit phase (OperationPeriapsis → Node Executor → entry → chutes)
  targeting a contingency splashdown site (one of 7).
- **BREAKOUT** (§4; function real, name unverified) = abort-the-APPROACH during prox-ops — back away from the
  ISS, valid until the Crew Hands-Off Point (CHOP). Conductor: SmartASS target_minus/retrograde + RCS to exit
  the Keep-Out Sphere and hold at a safe waypoint.

### B13.4 Conductor implementation (extends §B12; abort is a conductor state, not a MechJeb call)
- **Abort authority = `FlightDriver.RequestAbort` / `AbortControl` / `AbortMode`** (the existing stub surfaces
  — B12 replaces them). On trigger (manual EJECT via `FlightCommands.Run(Abort)`, or an autonomous FDIR check
  the pure core runs), the conductor:
  1. **Selects the mode** from `MissionPhase` + T+/energy (pad/1a/1b/2a…2e) — pure, testable decision.
  2. **Ascent abort — DIRECT SuperDraco commanding (owner directive, 2026-09-03: NEVER staging, NEVER action
     groups; §B12.7).** For pad/1a/1b: **command the SuperDraco engine modules directly** — resolve them on the
     live vessel at the abort boundary and `Activate()` them (the recovered `Actuator.FireAbort` is exactly
     this path, R1 §3.1), then fire the capsule↔trunk/booster decoupler by role through `FireDecoupler` —
     never `KSPActionGroup.Abort`, never a staging call. An action-group binding is authored in the VAB and can
     be absent, re-ordered or wired to the wrong part; on the one control path that must never silently fail,
     that is unacceptable. For 2a+: command S2 separation the same way, then Draco/SuperDraco shaping burns
     (SmartASS + RCS); 2e → resume on-orbit.
     ⚠ **Which parts:** the SuperDraco set and the separation decouplers are resolved **at runtime against the
     current craft dump** (§B12.7), not named here — the craft is a work in progress and a re-dump is pending,
     so this section states the method and names no part.
  3. **Stabilize:** SmartASS `surface_retrograde`/KILL-ROT (heat-shield/blunt-forward), trunk retained.
  4. **Recover:** hand to the chute logic (drogues at 5486 m, mains at 1830 m — the existing `MissionPhase`
     constants) → splashdown.
- **On-orbit contingency:** `FlightDriver.RequestDeorbit(propulsive)` (existing stub) → the deorbit phase.
- **Panel front-end (§B12.5, one command at a time):** EJECT → RequestAbort(phase-selected) · DEORBIT NOW /
  WATER DEORBIT → RequestDeorbit · BREAKOUT → prox-ops back-out. These are CONFIRMED-real functions (except
  BREAKOUT's name), so they are wireable without the §4 owner-gate that inferred/invented labels need.
- ⚠️ **Sim-fidelity flags:** the downrange back-fly-to-Ireland/Newfoundland targeting is likely only
  APPROXIMATED in KSP/RO (aim for a survivable splash, not the exact zone); SuperDraco burn duration/thrust to
  be matched to the Dragon part's KSP engine config; autonomous FDIR fault-detection is optional (manual EJECT
  is the guaranteed path). All abort logic is pure + headless-tested (mode selection + phase gating).

## B14. Crew-gate procedures — research + conductor/screen mapping
The crew-in-the-loop layer OVER the conductor: the real per-phase Go/No-Go holds, mapped to the existing seam
(`CrewProcedureOps` + `GatePhase` in `pure/MissionPhase.cs` + the `GateCard` screen). Sources: NASA/Spaceflight
Now Crew-1/2 timelines, Planetary Society, NASASpaceFlight docking write-ups. **Source-of-truth (§1.4):** the
gate STRUCTURE + poll points below are VERIFIED-REAL; the exact on-screen checklist WORDING is not public →
tier-2 reconstruction, marked, until owner-verified.

### B14.1 The gate model (from the seam — do not redesign)
`GatePhase` = Holding → GoReady (all items satisfied) → Go (crew pressed) | NoGo (hold/recycle) | Abort.
A `Gate` = { Title, ChecklistItem[] }; `ChecklistItem` = { Label, Kind: **CrewAck** (crew ticks) | **AutoCheck**
(system auto-satisfies) }. `CrewProcedureOps` exposes Engaged, **IsReturn** (outbound vs return leg), ActivePhase
/PhaseName, Proc (ProcState = {Phase, Satisfied[]}), CrewActionNeeded, CurrentGate, Toggle/ToggleItem(i),
PressGo/PressNoGo/PressAbort, MarkDockedThisMission. The conductor's phase FSM (§B12.3) advances a phase ONLY
through its gate: hold until GoReady, then the crew's PressGo releases the next phase (PressNoGo holds,
PressAbort → §B13). `GateCard` renders CurrentGate; `AuthoritativePhase` keeps it consistent with the autopilot.

### B14.2 The real gate sequence (each = a MissionPhase transition)
**Outbound (IsReturn = false):**
- **Prelaunch — crew ingress:** hatch close · **Suit Leak Check** (→ the built SuitCheckPage!) · comms — CrewAck.
- **Prelaunch — launch poll:** T-47 GO-for-prop-load poll · T-42 crew-access-arm retract · **T-38 LAS ARMED** ·
  T-35 prop load · T-5 Dragon internal power · T-1 tank press · **T-45s LD GO-for-launch** — mostly AutoCheck +
  a crew GO.
- **Ascent:** AUTOMATED — no gate; abort available (§B13). Conductor flies PVG; crew monitors.
- **Post-insertion:** Dragon sep + nose-cone open (AutoCheck) → **GO for phasing**.
- **Approach Initiation:** ~96 min before dock, from 7.5 km behind/below — **Go/No-Go poll for the Approach
  Initiation Burn**.
- **1 km hold:** Dragon STOPS at 1 km — **Go/No-Go to enter the Keep-Out Sphere** (200 m) & proceed to WP1.
- **WP1 → port:** GO to swing up to WP1 (~150–220 m off Harmony fwd port, mission-dependent) · WP0 400 m below ·
  continue to contact.
- **Docking:** contact & capture → hard-dock (hooks) → leak/pressure check → hatch open — AutoCheck + crew-ack.
  `MarkDockedThisMission`.
**Return (IsReturn = true):**
- **Undock:** GO for undock → autonomous undock from Harmony → maneuver away.
- **Deorbit:** GO for the deorbit burn (~15 min, begins after undock) · trunk sep · nose-cone close+lock.
- **Entry/descent:** monitored; the **Manual Chute Deploy** page gates (drogues 5486 m, mains 1830 m) → splash.
Contingency: the 140 m crew-commanded back-away (Crew-4/DM-2 demo) = the **BREAKOUT** gate/abort (§B13.3),
valid until CHOP.

### B14.3 Conductor wiring
Gate content authored per phase as pure data (headless-testable: assert Holding→GoReady only when Satisfied[]
all true, PressGo advances, PressNoGo/PressAbort behave). The gates are the ONLY place the autopilot pauses for
a human — everything between gates is autonomous. Prelaunch gates reuse the existing SuitCheck/procedure pages;
the docking gates pair with the Manual ISS Docking screen; the chute gates with Manual Chute Deploy. ⚠️ Exact
checklist wording = tier-2 until a real procedure source (ITAR-limited) or owner input firms it.

## B15. FDIR / autonomous fault detection — research + conductor design
The autonomous half of the abort trigger (§B13.1) and the fault-response surface. Sources: Aviation Week
(Dragon radiation-tolerant/redundant design), Space Launches Live (Dragon flight computer), NASA HWHAP.
**Source-of-truth (§1.4):** the AVIONICS ARCHITECTURE is VERIFIED-REAL (tier-1); the exact fault-trigger
thresholds / abort commit criteria are ITAR-private → tier-2 inference, marked.

### B15.1 Verified-real avionics architecture (tier-1)
- **Triple-redundant flight computers:** 3 units, each a PAIR of computers that cross-check → "6 computers in
  3 pairs." ~**18 triply-redundant processing units / ~54 processors** spread through Dragon (matches §8).
- **Commodity off-the-shelf dual-core** processors (NOT rad-hardened chips) — reliability from REDUNDANCY +
  **ECC memory** (detect/repair corruption) + **voting** (even one unit offline, two pairs still vote).
  **Linux-based flight software, C/C++.** Autonomous systems detect abnormal conditions and alert crew + ground.
- **The "strings"** = these redundant computer strings → the §4 panel's **STRING 1A/1B/1C + 2A/2B/2C** map to
  string units; **SWAP 1/2/3** swaps a failed string; **POWER 1/2** = the two main buses; **RESET 1/2**.

### B15.2 The conductor's FDIR (pure monitor; auto-abort is optional, manual EJECT is guaranteed)
- A **pure, headless-tested monitor** over the telemetry snapshot watching for out-of-family conditions:
  booster thrust/attitude/pressure anomaly (ascent → auto-abort §B13), cabin depress (leak), fire, loss of a
  string/bus, attitude-control divergence. On a fault it raises a `FdirReport` (Fault + Recovery — the existing
  stub type) → either auto-abort (ascent) or a safe-shutdown/response.
- **Fault-response surface (already REAL display-state in `FlightCommands.Run`, keep):** `DepressResponse`
  (isolate a cabin leak), `SuppressFire`/`FireResponse`, the STRING/POWER/RESET toggles (`Systems.*`). The
  conductor's FDIR DRIVES the lamps these buttons already read; the crew can also act manually.
- ⚠️ **Tier-2 / sim-fidelity:** the exact fault thresholds + abort commit criteria are not public → reconstruct
  conservatively and mark; in-sim, **autonomous FDIR is an OPTIONAL layer — the guaranteed abort path is the
  manual EJECT handle** (§B13). String/bus voting is modelled as display-state (as today), not a real KSP
  compute-failure sim. `Fdir.FaultName` (stub) already renders fault text.

## B16. Falcon-9 booster recovery — SEPARATE-VESSEL autopilot (owner scope addition, 2026-09-03)
**Authority.** Owner directive, 2026-09-03, granted via the overseer and confirmed in-chat (recorded as the
owner's per C1.12). **§B1–§B15 cover the DRAGON CAPSULE's flight only** — §B9's phase list runs Prelaunch →
Ascent → Phasing → Rendezvous → Docking → Docked → Undock → Deorbit → Entry → Chutes → Splashdown, and §B12's
conductor design assumes ONE controlled vessel. Falcon-9 first-stage recovery appears nowhere in it. This
section adds it, in the same gate as the rest of Part B (the §0 banner).

**AMENDED 2026-09-03 by G5a** with the owner's settled decisions, relayed through the overseer: §B16.1's
architecture (our own compiled core with its own steering law — **not** a second `MechJebCore`), §B16.4 (the
craft dump is IN the repo; the three engines bind by `engineID`), §B16.5 (**the guidance decision is settled**
— our own five-phase core on our own integrator), §B16.6 (it is **Wave C** of §B12.8), and three new
subsections: **§B16.7** the focus protocol, **§B16.8** the un-converged constants, **§B16.9** the
Kerbal-Konstructs landing zones.

**This section is a SCOPE + ARCHITECTURE statement, not a tuning derivation.** The per-setting flight book
already exists: **`docs/MECHJEB_MISSION_TUNING.md` (S48) — PHASE 2 (§2.0–§2.6)**, whose SCOPE FLAG anticipated
exactly this fold-in. Read it for every value, knob and gotcha; §B16 does not restate them and must not drift
from them (C7.1 — on any number, THE PLAN WINS, and where the plan is silent S48 is the recipe).

### B16.1 Why it is a separate autopilot, not another conductor phase
- **It is a SEPARATE VESSEL.** At stage separation KSP splits the stack into two `Vessel` objects: the Dragon
  conductor follows the capsule, the booster becomes a second, independently-flown craft needing its own
  autopilot. Nothing in §B12 — one `MechJebCore`, one phase FSM, one screen front-end — addresses a second
  vessel, and KSP gives only ONE vessel focus at a time.
- **It is a different flight regime** from everything else in Part B: a powered, atmospheric, target-accurate
  landing under **limited ignitions and limited throttle**, not orbital or entry guidance.
- **Architecture consequence — OUR OWN COMPILED CORE WITH ITS OWN STEERING LAW (owner decision, 2026-09-03,
  via the overseer; supersedes this bullet's earlier "its own `MechJebCore`" wording).** A `BoosterRecovery*`
  track that MIRRORS §B12's split — **pure decision core + thin glue driver, headless-tested** — and owns:
  **its own vessel**, **its own parameter store / cfg** (the booster is a different vehicle from the Dragon,
  S48 §2.5), and **its OWN STEERING LAW**. ⛔ **It is NOT a second `MechJebCore`, and a build chat must not
  attach one to the booster.** The earlier wording would have sent a chat to do exactly that.
  The owner's statement of intent, verbatim in substance: *a `.dll` version of our kOS-style landings with even
  better attitude and manoeuvre precision* — a **perfect flip and boostback with no roll and no wasted
  movement**, and a **9-3-1 engine schedule with no relight lag**. That is a purpose-built controller for one
  known plant, which is why it is ours rather than a general-purpose autopilot's: the steering law is
  §B16.5/§B16.2's, the attitude command shaper is the booster core's own, and it is **independent of the
  MechJeb instance flying the Dragon mission** — the two never share a core, a controller or a settings store.
  The Dragon conductor does not become a two-vessel machine; the two tracks coexist and share only the seams
  already in the tree (`MissionConductor.AutoRecoverBooster`, `BoosterRecovery.Tracked`, `RangeExtender.cs`,
  `pure/VehicleParts.cs`'s octaweb model — inventoried in S48 §2.0). Single-core safety is unaffected: see
  **§B12.6**, which governs the DRAGON.
- **Vessel focus — SETTLED. See §B16.7.** This bullet previously carried `ForceSetActiveVessel(booster)` as an
  open design question (S48 §2.6 gotcha 8). The owner settled it on 2026-09-03: **focus never leaves the upper
  stage.** The protocol, and the accepted risk it bounds, are **§B16.7**.

### B16.2 The recovery profile — boostback / entry / landing burns
The five-phase decomposition the RSS/RO community converged on (S48 §2.2 carries the exit conditions and the
full parameter table): **1 BOOSTBACK** → **2 COAST** (ballistic, retrograde) → **3 ENTRY BURN** (three
engines, steer slightly off retrograde) → **4 AERO DESCENT** (engines off, grid fins steer) → **5 LANDING
BURN** (ignite EARLY to cover the RO ignition delay; decelerate to ~zero). Profiles: **RTLS** = full boostback
+ 3-engine entry + 1-engine landing, ~10 % of total propellant; **ASDS/droneship** = a **zero-magnitude
boostback trim** (below) + 3-engine entry + 1-engine (or 3-then-1) landing, ~6 %. Crew-2 — the mission our
cfg is tuned for — was an **ASDS** recovery (S48 §2.1 has its timeline and both aim points).

✅ **RESOLVED — boostback is ONE ALWAYS-ENTERED state for both profiles (owner, 2026-09-03, via the overseer;
closes G5a-Q2, recorded in full under that entry in "Open questions for the owner"; this IS the C1.8 `OVERRIDE`
of the line below, and the chat that raised the question flagged exactly this edit as the mechanism it would
need).** An earlier version of this section gave RTLS a boostback phase and ASDS none. That is superseded.
**Boostback is now a single state entered on every recovery**, with its **magnitude and aim-point offset
parameterized by target mode**: RTLS runs the full flip-and-null-target-error return burn; **ASDS defaults to
a ZERO-MAGNITUDE trim** until a recorded flight says otherwise. The zero-magnitude default is not a guess —
`docs/BOOSTER_GUIDANCE_METHOD.md` §3.1/§8.1's tier-2 source runs a **170° flip / 5° retrograde offset / 2700 m
downrange aim** on ASDS, same code as RTLS boostback, just sized as a trim rather than a return burn; ASDS
starts at zero magnitude (matching the old "no boostback" behaviour exactly) and converges toward that tier-2
shape empirically, the same way every other un-converged booster constant does (§B16.8). A build chat must
implement the state as **always-entered, mode-parameterized magnitude/aim-offset**, never an RTLS-only
optional state.

### B16.3 ⛔ RO engine handling — the owner's operational direction (2026-09-03)
> **Do NOT cycle "next engine mode".** RO's `ModuleEngineConfigs` mode-cycling causes engine **RE-IGNITIONS**
> and **lag**. Instead **read the CRAFT FILE's engine list** to identify the **THREE landing engines**, and
> **control them SEPARATELY** — the 3-engine → 1-engine landing-burn throttle profile.

This is binding on the flight software. Consequences, in full in S48 §2.3:
- **The forbidden path is already in our tree and must not be called by the recovery guidance:**
  `pure/VehicleParts.cs`'s `EngineSwitchModule = "ModuleTundraEngineSwitch"` / `EngineSwitchAction =
  "next engine mode"` / `OctawebModeFor(int)`. The constants stay (they correctly describe the part); what
  changes is what the autopilot calls.
- **The method instead is per-engine control** through the stock `ModuleEngines` API that RO's
  `ModuleEnginesRF` derives from: `Activate()` / `Shutdown()` per engine, and **`independentThrottle` +
  `independentThrottlePercentage`** to throttle a named engine independently of the vessel throttle — the
  field that makes a 3-engine → 1-engine profile possible without touching engine modes.
- **RO ignitions are a finite per-engine resource** — read `ignitions`, budget them, and refuse a phase the
  budget cannot cover. **Never command zero throttle mid-landing-burn** (that is an instant shutdown; the
  relight costs an ignition): hold a floor above the engine minimum.
- **Ullage is the failure we have already had** (`docs/FLIGHT_144114_SCREEN_AUDIT.md`: *booster ballistic, eng
  never lit → LOST*): settle propellant with RCS before EVERY relight (S48 §2.6).

### B16.4 The craft dump — IN THE REPO, and how the three engines are actually bound
**C7 status: SATISFIED.** This section previously said *"the OWNER supplies the craft dump… T15-onward work
must STOP and ask if it needs the dump and the dump is not in the repo."* **The dump is now in the repo** —
`docs/reference/craftdump.csv`, regenerated by **W0** (2026-09-03) with the recovered
`plugin/src/CraftDump.cs`, plus 15 owner-supplied `.craft`/`.loadmeta` pairs under `docs/reference/`. No task
needs to stop and ask for it. §B12.7 records the escalation that follows: because all actuation is direct, the
dump gates **essentially all** Part-B actuation, not only the booster.
⚠ **Work in progress — a RE-DUMP IS PENDING** (owner, 2026-09-03). The current dump has **20 parts** and is
**missing the drogues, the mains, the trunk adapter/decoupler and all four S2 RCS thrusters**. Design against
the method and the parts that are there; name no chute, decoupler or S2-RCS part.

#### ⛔ The old engine-resolution procedure was WRONG for this craft — do not follow it
It said: list the parts carrying `ModuleEngines`/`ModuleEnginesRF`, **expect `OctawebEngineCount = 9`**, and
identify the centre engine and its two burn partners **by position**. **Verified twice against
`docs/reference/craftdump.csv`, including W0's fresh 23:21 dump of 2026-09-03: there are NOT nine engine
parts.** The `expect OctawebEngineCount = 9` expectation is **deleted**; position-based identification is
**deleted**. What the dump actually shows:

- **The octaweb is ONE part** — `TE.19.F9.S1.Engine`, *"Falcon 9/Heavy Full Thrust Octoweb"* — carrying
  **THREE `ModuleEnginesRF` modules**, distinguished by `engineID`:

  | `engineID` | Engines | Role |
  |---|---|---|
  | `AllEngines` | **9** | liftoff / ascent |
  | `ThreeLanding` | **3** | boostback, entry burn, landing-burn start |
  | `CenterOnly` | **1** | the 3→1 handover, terminal landing burn |

- **These three ARE "the 3 individual engine control modes from the craft dump"** in the owner's 2026-09-03
  directive, and they **ARE the 9-3-1 schedule** §B16.2 describes. There is no fourth thing to find.
- Each also appears as a matching `ModuleEngineConfigs` block (configuration `Merlin1D` / `Merlin1D++`).

#### The binding procedure — BY `engineID` STRING, and nothing else
1. **Find the booster part** with `VehicleParts.IsBooster(part.name)` — the `".S1."` marker (`VehicleParts.cs:9`).
2. **Bind the three `ModuleEnginesRF` instances on it BY THEIR `engineID` STRING** — `AllEngines`,
   `ThreeLanding`, `CenterOnly` — resolved ONCE at the phase boundary into a named table, never re-searched
   per frame. Throttle a bound instance with `independentThrottle` + `independentThrottlePercentage` (both
   confirmed present on this part) and start/stop it with `Activate()`/`Shutdown()`, per §B16.3.
   *(Note: `VehicleParts.cs:34-35` already carries `EngineIdThree = "Three"` / `EngineIdCentre = "Center"`,
   which do match `ThreeLanding` / `CenterOnly` as substrings — but the dump strings above are the identity;
   bind to them.)*
3. ⛔ **NEVER by position. NEVER by engine-part count. NEVER by `persistent_id`** — ids change between craft
   revisions (the 23:21 dump is already a different vessel revision from the 26 Aug one), so a hardcoded id
   breaks silently on the next VAB edit (§B12.7).

#### ⛔ The ban, with the forbidden members NAMED
§B16.3 forbids mode-cycling; on this part the forbidden members are, exactly:
`ModuleTundraEngineSwitch.NextEngineModeEvent` · `.NextEngineModeAction` · `.ToggleEngineModeAction` (and by
the same rule `PreviousEngineModeEvent`/`.PreviousEngineModeAction`), and **`ModuleEngineConfigs`** as a
switching mechanism. All are present on `TE.19.F9.S1.Engine` and none may be called by the flight software.
`ModuleTundraEngineSwitch`'s read-only fields (`currentEngineDisplay`, `primaryEngineID`, `secondaryEngineID`,
`tertiaryEngineID`, `selectedIndex`) may be **read** for annunciation. `ToggleIndependentThrottleAction` also
sits on this part — an **action**, therefore out (set the `independentThrottle` field directly instead).

#### ⚠ HARD ASSERTION — a SECOND Falcon 9 is now installed, and the binding must refuse it
The owner installed **Kartoffelkuchen "Launchers Pack"** on 2026-09-03. Its parts are prefixed **`KK_SPX_`** /
**`KK_F9demo_`** and it ships **its own octaweb, `KK_SPX_F9_Octaweb`**. So the booster binding **MUST**:
- **assert that EXACTLY ONE octaweb is found**, and that it is **the Tundra one** (`TE.19.F9.S1.Engine`);
- **reject any part whose name contains `"KK_SPX"` or `"KK_F9demo"`**;
- **refuse and annunciate** on either failure rather than picking one — a booster controller that binds the
  wrong vehicle's engines is a lost booster with no error message.
- **This assertion must be GUARDED BY A TEST** (the recovered `test/ActuationTest.cs` pattern: assert the
  capability map against the real dump, headless).
Verified 2026-09-03: **no Kartoffelkuchen part name contains `".S1."`**, so `IsBooster` still discriminates
today. The assertion exists so that it cannot silently bind the wrong vehicle **if that ever changes**.

#### ⚠ TestFlight sits on this exact part
`TE.19.F9.S1.Engine` also carries **`TestFlightFailure_IgnitionFail`** (alongside
`TestFlightFailure_ShutdownEngine`, `_ReducedMaxThrust`, `_EnginePerformanceLoss`, `_Explode`,
`TestFlightReliability_EngineCycle`, and `ModuleGimbal`). **Ignition failure is the failure class that lost the
booster** (`docs/FLIGHT_144114_SCREEN_AUDIT.md`: *"booster ballistic, eng never lit → LOST"*) and the reason
register line **H1b** exists. The ullage/ignition discipline of §B16.3 and R1 §7.1 is therefore not defensive
padding on this vehicle — it is the known failure mode, present on the part, with dice attached.

### B16.5 The guidance decision — SETTLED (owner, 2026-09-03, via the overseer)
This section used to be *"the guidance decision the owner still owes"*, offering three options. **It is no
longer an open question.** The answer is **its own option 1: the §B16.2 five-phase method, implemented inside
OUR OWN booster core** (§B16.1) — with **one correction from R1: the PREDICTION comes from OUR OWN INTEGRATOR.**

**What is chosen**
- **The method:** §B16.2's five phases (boostback → coast → entry burn → aero descent → landing burn), with
  the laws and their tier-2 attribution in `docs/BOOSTER_GUIDANCE_METHOD.md` §4 — one guidance, a target mode
  (`Rtls`/`Asds`), one steering law for the whole descent, three throttle laws layered on it.
- **The prediction engine: `plugin/src/pure/Trajectory.cs` + `plugin/src/pure/BoosterDrag.cs`, ours.**
  R1 §3.5 establishes why: `Trajectory` is a **body-agnostic RK4 predictor** — `Mu`, `BodyRadiusM`,
  `BodyOmega`, `AtmosphereDepthM`, `BallisticCoefficient` and the `DensityAt` / `SpeedOfSoundAt` /
  `DragFactorAt` delegates are all **inputs**, so there is **no hardcoded planet in the file** — and it is
  **unit-proven against analytic conics** (`test/TrajectoryTest.cs`, 12.8 KB). `BoosterDrag` feeds it a
  **Mach-binned ballistic-coefficient curve**. Both are **RECOVER-CODE** (R1 §3.5) and land in **§B12.8 Wave A**.
  The design note that motivated them is still the argument: *drag only ever shortens a trajectory, so a
  vacuum answer is always LONG* — by tens of km on an entry — and *the drag term is MEASURED, not modelled*.

**What is NOT chosen — all three exclusions are load-bearing**
- ⛔ **NOT a second `MechJebCore`.** §B16.1: the booster gets our own compiled core with its own steering law.
  A build chat must not attach a `MechJebCore` to the booster vessel.
- ⛔ **NOT MechJeb's landing autopilot.** `MechJebModuleLandingAutopilot` assumes a freely-throttleable lander
  with unlimited relights, and has **no boostback phase, no entry-burn phase, no grid-fin steering and no
  phase-by-phase engine count**. On a limited-ignition, throttle-floored booster it is the wrong instrument —
  S48 §2.5 and `BOOSTER_GUIDANCE_METHOD.md` §4.5 reach that from opposite directions.
- ⛔ **NOT `BoosterGuidance` as a dependency.** Not needed now. **The general rule stands: §B3's packaging
  decision covers MechJeb ONLY — vendoring or depending on any second mod is an OWNER call, never a
  build-chat one.** That includes **Trajectories**, which the tier-2 source uses for its impact prediction and
  which our own integrator exists precisely to avoid.

**Consequence for `docs/BOOSTER_GUIDANCE_METHOD.md` (that doc is unedited; the plan governs).** Its §4 maps
each law onto `MechJebModuleAttitudeController` + `MechJebModuleLandingPredictions`. Those mappings are
**superseded by §B16.1 + this section**: with no `MechJebCore` on the booster there is no MechJeb attitude
controller or prediction module on that vessel either. **The LAWS transfer unchanged** — read every
*"in C#/MechJeb"* note as *"in C#, in our own booster core"*, with attitude commanded by the booster core's own
controller and impact supplied by `pure/Trajectory.cs`. Its §5 two-tier prediction fallback (a coarse Keplerian
answer when the good one is unavailable) is worth keeping on the same reasoning it gives.
⚠ **And its §10 still applies in full: the constants do not transfer** — see **§B16.8**.

### B16.6 Register status — the SHAPE is settled; the lines are G5c's to write
G4 posed this as an owner call. **The owner settled it on 2026-09-03 (via the overseer): the recovery enters
`REGISTER.md` as FOUR DEPENDENCY-ORDERED WAVES, ONE REGISTER TASK EACH**, each ending green under the
preview-only gate. The waves, their contents and their ordering are **§B12.8** (A = collision-free `pure/`
support · B = actuation · **C = the booster set** · D = the conductor set), with **W0 already DONE** ahead of
Wave A. So §B16 is no longer a track with no register position: **it is Wave C.**

**The register lines are written by G5c, not here.** G5c owns every `REGISTER.md` line (this task wrote none);
§B12.6 step (9) still records the build-order position, and the gate is the §0 banner, the same as the rest of
Part B.

### B16.7 The focus protocol, and the risk it bounds (owner decision O-B1 REVISED, 2026-09-03)
> ⛔ **FOCUS NEVER LEAVES THE UPPER STAGE.**

The sequence, in order:
1. **PhysicsRangeExtender expands the range** so the booster stays loaded and physically simulated at
   separation distance (`RangeExtender.cs` is already a seam in the tree, S48 §2.0).
2. **The booster lands UNFOCUSED** — flown by its own core (§B16.1) on the non-active vessel.
3. **+10 s settle** after touchdown, so the landed state is stable before anything is asked of it.
4. **Auto-recover** the booster.
5. **PhysicsRangeExtender OFF; the default range restored.**

**Why this, and not `ForceSetActiveVessel(booster)`:**
- **It preserves IVA immersion.** This is a Crew-Dragon IVA mod. Yanking the camera out of the capsule
  mid-ascent to watch a booster is the one thing the whole of Part A exists to prevent.
- **It sidesteps the S60 `ActiveVessel` defect entirely.** S60 (`docs/BOOSTER_RECOVERY_ARCHITECTURE.md`) found
  a focus-switch defect in the two-vessel handover. **With no focus switch, it never bites.** Still **fix it
  defensively** — a latent defect on a path nobody takes is cheap to fix and expensive to rediscover — but the
  fix is **off the critical path** and blocks nothing.

**⚠ THE ACCEPTED RISK, stated plainly rather than buried.** An **unfocused** booster at roughly **1500 km**
separation is **well past PhysicsRangeExtender's own >100 km caution**, and KSP's floating-origin rule is
*"whoever holds focus gets the precision"* (S60) — so the vessel we are landing accurately is the one running
at the coarser end of the physics. This is a **knowingly accepted risk, not an oversight**: the protocol above
**bounds** it (loaded and unpacked only for as long as the landing needs, then recovered and the range
restored), and **the BlackBox's two-vessel recording is what will actually answer it** —
`docs/BLACKBOX_RESEARCH.md`, recording the booster as its own stream (R1 names `BoosterLog.cs` as the
reference for how a non-active vessel gets its own recording stream). Until a recorded flight says otherwise,
the risk is open and documented; nobody should be surprised by it later.

### B16.8 Provenance of the booster constants — they are UN-CONVERGED
> **Owner decision, 2026-09-03 (R1 open question Q2): RE-FLY.**

**Two RSS-RO distillates survive the deletion, and only two:**
| Artefact | What it is |
|---|---|
| `plugin/src/pure/BoosterDrag.cs` | the Falcon-9 booster's Mach-binned ballistic-coefficient curve — **18,080 clean unpowered in-atmosphere descent samples across 48 recorded RSS/RO flights**, median bc per 0.5-Mach bin (R1 §3.5, citing commit `0d6423d`) |
| `docs/tuning/TUNING_DB.json` (+ `.md`) | per-phase statistics over a **55-flight** RSS/RO corpus, 2026-08-26 → 08-29 (R1 §4.3) |

⛔ **THE RAW CSVs BEHIND THESE TWO DISTILLATES ARE GONE — and that is now a narrower claim than it was.**
Both corpora were **gitignored and never committed** (R1 §3.5, §4.3) — the same `.gitignore` mechanism this
task closes — so **neither the 48-flight `BoosterDrag` bc corpus nor the 55-flight TUNING_DB corpus is in
this repo, and neither distillate can be re-derived, re-binned or re-checked from it.** If the curve is
ever doubted, there is nothing here to doubt it against. **That remains true of those two corpora.**

✅ **What is NO LONGER true is the general statement.** **W26** (2026-09-04, the owner's decision) recovered
**16 CSVs — 13 `FlightRecorder` recordings + 3 geometry dumps — into `docs/flights/`** (DS-ASC-001…008 and
DS-DEO-001, 2026-08-31 → 09-01, 21 MB with the log excerpts and screenshots). Raw RSS-RO flight data **does**
exist in this repo again, and **S76** re-pointed `plugin/tools/assess_flight.py` at it and read all 13
— see **`docs/FLIGHT_CORPUS_ASSESSMENT.md`** for what they contain, which are usable and which are junk.
⚠ **They are NOT the two corpora above:** they are 13 flights, not 48 or 55; they are a later window; and
they carry no atmospheric density, drag acceleration or unpowered-phase marking, so they cannot re-derive a
ballistic coefficient. **Ruling 2 below is UNCHANGED** — every recovered constant stays UN-CONVERGED until a
task actually re-derives it, and no constant is re-verdicted by this correction of fact (C1.12).

**Therefore, three rulings:**
1. **Both are REFERENCE WITH STATED PROVENANCE, not seed truth.** Recover them, keep them, and **mark them in
   place**: *derived from a 48-flight (bc curve) / 55-flight (tuning DB) RSS-RO corpus whose raw data is lost;
   distillate only, not re-derivable.* A number you cannot re-derive is still the best number you have — it is
   simply not evidence any more.
2. **Every booster constant is marked UN-CONVERGED for RSS-RO.** That includes both the recovered ones and
   every `[F9I]` value in `docs/BOOSTER_GUIDANCE_METHOD.md` §4 — whose §1.1 already says the laws transfer and
   the constants do not (F9I is **stock Kerbin**), and whose §10 gives the re-convergence priority order:
   the 2700 m boostback overshoot bias first (it is a **drag budget**, and RO's atmosphere is not Kerbin's),
   then the entry-burn gate/cutoff, the `altitude/100` AoA taper, the landing-burn margins and flare
   altitude, and the single-engine thrust ratio. Add to that R1 §7.4's regime-unstated defects —
   `pure/Hoverslam.cs`'s ignition anchors and `BoosterTargeting.cs`'s coordinates, whose regime **is recorded
   nowhere** and must be established before the number is used.
3. **The corpus is rebuilt by RECORDED RE-FLIGHTS.** That needs the **BlackBox**
   (`docs/BLACKBOX_RESEARCH.md`) and it needs **glass time — a SEPARATE owner gate, per session** (§0). A
   booster task cannot converge a constant under the preview-only gate; it can only build the thing that
   would.

**⚠ What the BlackBox MUST capture, or the bc curve cannot be re-derived at all.** The back-solve is
`BC = 0.5 · rho · v² / a_drag`, so the recorder must log, per sample: **atmospheric density**, **Mach**,
**drag acceleration** (or the terms it is computed from — total acceleration, gravity, thrust), **mass**, and
an **explicit marking of the unpowered phase** (the 18,080 samples were *clean unpowered in-atmosphere
descent* — without that flag the powered samples poison the bins). This is a **requirement on the BlackBox
spec**, recorded here so it is not discovered after the first re-flight.

### B16.9 The landing zones are KERBAL KONSTRUCTS STATICS, not craft (owner directive, 2026-09-03)
**The decision.** In RSS-RO **the seas are too rough for the droneship `.craft`** — flying a barge as a vessel
is ruled out. We use **the SAME droneship in Kerbal-Konstructs STATIC form**, placed at **real coordinates**
under the **real droneship name**, and the **per-mission choice is resolved by craft name in the VAB** (O5).
Guidance targets a **KK group centre's lat/lon**.

**⚠ NAMING TRAP — state it so no later chat repeats it.** In `RealismOverhaul/RO_SuggestedMods/`, the folder
**"KK Launchers"** means **KARTOFFELKUCHEN**, *not* Kerbal Konstructs. Its siblings are `RO_KK_AtlasV_*`,
`RO_KK_DeltaIV_*`, `RO_KK_Vulcan_*`, `RO_KK_MinotaurV`. Two different mods answer to "KK" in this install.

**The verified install state, recorded so LZ1 does not re-derive it** (owner/overseer evidence of 2026-09-03;
C7 — this is written down here as evidence, and no task needs to go looking in the install for it):
- **Kerbal Konstructs is installed.** `Space_X_barge_lander-2.0` supplies the barge static
  (`pointername = SpaceXbarge2`); `zzz_TundraRO_Fixes/Droneship_ROFix.cfg` patches it for RO.
  **TundraSpaceCenter** also ships a second barge static (`TSC_Barge` / `TE_Barge`, *"Funny, It Worked Last
  Time… Droneship"*).
- **NEW 2026-09-03 — the RTLS gap is CLOSED with real assets.** **Fossil Industries "SpaceX Landing Pads"**
  supplies the RTLS statics: **`Fossil_LZ1`, `Fossil_LZ2`, `Fossil_LZ4`, `Fossil_StarbasePad`** (*"SpaceX
  Landing Zone 1 / 2 / 4"*, *"SpaceX Starbase Landing Zone"*). **No invention is required** for RTLS — which
  closes `BOOSTER_GUIDANCE_METHOD.md` §8.3's *"a `TargetMode.Rtls` has nothing to aim at today"*.
- **ALSO new 2026-09-03:** Kartoffelkuchen's Launchers Pack means **`KK_SPX_ASDS`** and
  **`KK_SPX_LandingZone1`** now **exist**, so RO's `RO_KK_Falcon9_LandingZones.cfg` finally applies to them.
  **These are PARTS, not statics.** ⛔ **DECISION TO RECORD: we place the KK STATICS and leave those parts
  unused.** Guidance targets a KK group centre's lat/lon; flying a barge as a vessel is what the owner ruled
  out. (Same pack is why §B16.4 carries a hard assertion against `KK_SPX` / `KK_F9demo` parts.)
- **EXACTLY ONE barge is placed today:** Group **"Of Course I Still Love You"**, `RefLatitude` **32.7875**,
  `RefLongitude` **−76.6445**, `Heading` **13.320014** — which **IS** the `BARGE (32.787551, −76.644507)` aim
  point already in our code (`plugin/build/assess_flight.py`). **The KK GROUP CENTRE's lat/lon is therefore
  what guidance targets** — not a vessel position, not a static's own offset.
- **Placement takes two files** (recorded so the LZ1 task does not have to rediscover the schema):
  `KerbalKonstructs/NewInstances/KK_GroupCenter_Earth_<Group>.cfg` — `Group`, `CelestialBody = Earth`,
  `RefLatitude`, `RefLongitude`, `Heading`, `RadiusOffset`, `SeaLevelAsReference` — and
  `<static>-instances.cfg` — `STATIC { pointername, Instances { UUID, RelativePosition, Orientation, Group,
  LaunchSite { LaunchSiteName = <Group>_<static>_0, … } } }`.
- **GAP: "Just Read The Instructions" and "A Shortfall Of Gravitas" are NOT placed — and need NO download.**
  The droneship **NAME is the KK Group name**; the **MODEL is the static we already have**. Two more
  group-centre + instance entries place them.

**What §B16.9 does NOT do.** The **per-mission craft-name → droneship/LZ table is REAL FLIGHT DATA**: it must
be **SOURCED and MARKED per §1.4**, never invented. Sourcing that table, placing the two missing droneships and
placing `Fossil_LZ1` is the **LZ1** task, which the overseer issues separately. **This section states the
requirement; it does not carry the table.**
ℹ **One source LZ1 already has in the repo:** the 16 owner-supplied `docs/reference/<mission>.craft` files
(committed by G5a) each carry a mission description naming that flight's **recovery mode** — e.g. `Crew-2`
*"Recovery: OCISLY droneship"*, `Ax-2` *"RARE crew RTLS (boostback to land, not droneship)"*, `Ax-3`
*"crew RTLS"*. That is a per-mission, in-repo, owner-supplied datum keyed by exactly the craft name the VAB
selection resolves on. LZ1 still has to source and mark it properly (and reconcile it against a real flight
record), but the table does not start from nothing.

**O5/O7 cross-reference (G5b, no duplicate table).** Both are already resolved and recorded **in full at
§B16.9 above** — **O5** (per-vehicle-name LZ resolution, "resolved by craft name in the VAB") and **O7**
(mod-first RTLS/droneship sourcing — Fossil Industries "SpaceX Landing Pads" for RTLS, Kerbal Konstructs
statics for ASDS, no invention required for either). The per-mission **craft-name → LZ table** itself is the
**LZ1** task's deliverable, not this one's — §B16.9's closing paragraph already says so; this note just closes
the O5/O7 loop without restating that content here.

## B6. Honest risks
GPLv3 source-shipping obligation (public); MechJeb version pinning + private-namespace build tooling; **RSS/RO
ascent tuning (PVG) is the genuinely hard part**; the conductor's live re-plan state machine is the main new
code; ensuring a single MechJebCore commands the Dragon (no double-run with a user's MechJeb — §B12.6).
**Added 2026-09-03, with §B16 and §B12.8 folded in:** a **full** MechJeb port means porting the GUI too and
then keeping it suppressed forever (§B12.1a); **direct part control** moves every actuation failure from
"MechJeb didn't stage" to "we addressed the wrong part", which is why the dump binds at runtime and the
booster binding is test-guarded (§B12.7/§B16.4); the **unfocused booster at ~1500 km** is a knowingly accepted
precision risk (§B16.7); **every booster constant is un-converged and its raw evidence is lost**, so the first
numbers will be wrong and only recorded re-flights can fix them (§B16.8); and the recovery waves reintroduce
**103 files that mostly never flew** — recovered ≠ working (§B12.8, R1 §4.2).

---

## 14. Coherence pass & source-of-truth tier map (2026-09-02)
Full read-through of Parts A + B against the §1.4 hierarchy. Result: **internally coherent** — a few
label-level numbers to reconcile (below), every element classified tier-1/2/3, and the tier-3 (invention)
items flagged for the JOINT owner discussion §1.4 requires. Nothing is invented unilaterally.

### 14.1 Minor inconsistencies to reconcile (all label-level, none blocking)
- **WP1 distance:** 220 m (§8/B9/B11) vs ~150 m (Crew-4, B14) — mission-dependent; use **220 m nominal**, mark range.
- **Keep-Out Sphere "200 m":** sources ambiguous radius vs diameter; keep ≈200 m, marked.
- **Chute altitudes:** the FSM trigger constants (5486 m drogues / 1830 m mains, `MissionPhase.cs`) are the REAL
  numbers; the Manual Chute page's "(TBC)" altitudes are SpaceX's own placeholder text kept verbatim — the two
  are INTENTIONALLY different (page = illustrative/TBC, FSM = trigger). Neither should be "fixed" to match.
- **SuperDraco burn:** 6–9 s (nominal push) vs 25 s (capability) — treat as a short high-g push (B13).
- **Part B labels** run B1–B5, B7–B15, then B6 (risks) last — intentional; §B0 is the reading order.

### 14.2 Source-tier map (per §1.4)
- **TIER-1 (verified-real — used FIRST):** all captured screen LAYOUTS (Cover/HUD/Suit-Check/VRIO/Manual-Chute/
  Manual-Docking + the prop-thruster / systems-tree / nav-plot looks); flight-facts §8; abort modes+hardware
  B13; avionics architecture B15; crew Go/No-Go poll points B14; MechJeb semantics + the tuned Crew-2 cfg
  B7–B10; the [DOC] flight-data targets B11; the §4 CONFIRMED panel functions.
- **TIER-2 (other users' recreations/assets — where unverifiable, MARKED):** exact on-screen TEXT for the newest
  screens; the capsule turntable model (MaTte0 CC-BY §5); iss-sim docking DOM; `PanelMap` from Tundra's IVA
  model §4; DillonBaird structural cues; crew-gate checklist WORDING B14; FDIR thresholds B15.
- **TIER-3 (NO evidence AND no asset → invention, JOINT discussion required):** (a) the panel LIGHTING scheme
  (grey/white/red — §4 "our choice"); (b) the INFERRED panel semantics (STRING 1A–2C / SWAP 1/2/3 / RESET /
  entry-mode specifics — §4 INFERRED list); (c) the **Reference Content + Menu** pages (DARK — no imagery AND
  no data structure, §3/§7 HOLD); (d) the reconstructed Suit-Leak FAIL branch. (The [EST] B11 numbers are
  MEASURED-in-sim, not invented — a separate class, resolved by the §B5 tune, not by discussion.)

### 14.3 Tier-3 items needing the joint owner decision (per §1.4, before any build)
Clusters (a)–(d) above are the ONLY elements with no real and no other-user basis. Per §1.4 each needs an owner
discussion on HOW to handle — leave inert/blank, reconstruct-and-mark, or co-design — before it is built. This
is the standing owner-decision list; **no unilateral invention.**

### 14.4 Owner invention decisions (running log)
- **(c) Menu + Reference Content — RESOLVED 2026-09-02 (reconstruct-from-function; drops tier-3 → tier-2):**
  · **Menu** (`UiPage.Menu`, opened by the Cover Menu button) = a **navigation index** — a grid/list of all
    built pages, tap to jump. Fills the real 25–30-page need the 5-icon bar can't; content = real pages, only
    the layout is ours.
  · **Reference Content** (the Cover deorbit phase-rail slot `PhaseReference`, NOT a standalone page) = a
    **deorbit quick-reference** built from the §8 real flight data (entry timeline + altitudes + abort/
    contingency notes). Real data, just presented for the deorbit phase.
  Build with Part A (§7 item 10).
- **(a) panel lighting — RESOLVED 2026-09-02 (toward the real look):** buttons light BRIGHT when active/armed/
  fired (crew-visible, real-console look); **NO red** (no evidence — red-refused removed); rest unlit; **audible
  CLICK** on every mechanical press (new audio asset). Screens-only refuse = click + no light + no action.
  **UNCHANGED by §14.4(f) (2026-09-03) — the scope boundary:** (f) governs READOUTS only; flight ACTUATION
  stays this honest no-op until Part B wires it (§B12.5), never a simulated flight path.
- **(b) inferred panel semantics — RESOLVED 2026-09-02 (inert until verified):** keep POWER/STRING/RESET as
  display-state; **SWAP 1/2/3 + the inferred entry-mode toggles (ENTRY REBOOT / BACKUP ENTRY / NORMAL ENTRY) go
  INERT** (click, no function, unlit) until a real console-procedure source verifies them. Confirmed commands
  (ENABLE BACKUP PYROS, FIRE PYRO) stay. (⚠️ RESET 1/2 is in §4's inferred list but kept per the owner's choice.)
- **(d) Suit-Leak fail branch — RESOLVED 2026-09-02: KEEP, marked as a reconstruction.** A leak check must have
  a fail path (reconstruct-from-function); exact step wording (Failed Low / TROUBLESHOOT / step 2.5) is marked
  reconstructed, not verified-real. Drops to tier-2. Honors the earlier add-don't-take-away rule.
- **(e) Simulation-for-immersion policy — RESOLVED 2026-09-02.** Where a physically-real vehicle quantity is
  not yet modelled, do **NOT** default to an honest dash when that dash costs immersion. In order: (1) **read
  it from an existing installed mod** if one provides it (tier-2, MARKED — cabin O2/CO2/water already come
  from TAC-LS via `LifeSupportBridge`); (2) failing a mod, **SIMULATE** it, but only as a **COHERENT model
  driven off real vessel/cabin state** (never a static constant), **MARKED as simulated in code** (tier-3
  invention, jointly decided per §1.4); (3) keep an **honest dash ONLY where the quantity genuinely does not
  exist in that state** (no target → no docking error; return leg → no splashdown-relative; a value only
  Part B's flight software will command, e.g. the deorbit SLEW rows → dash until Part B).
  **GUARDRAIL:** a simulated value must **never fabricate a safety VERDICT the sim cannot justify** — a
  verdict (e.g. a suit-leak "Nominal") follows the simulation honestly, never hardcoded.
  This **EXTENDS §1.4** (real → other-users'/mod → simulate-marked → dash-for-absent); it does **not** license
  unmarked invention. First application: the suit leak check (S31).
  **EXTENDED 2026-09-03 into a completeness mandate by (f)** — for READOUTS, (2)'s coherent marked simulation
  is now the DEFAULT fill and (3)'s dash narrows to a genuinely-absent state; see §14.4(f).
- **(f) Completeness + simulate-to-fill — RESOLVED 2026-09-03 (owner, via the overseer).** Every feature the
  real Dragon screens have is **INCLUDED** — nothing is dropped for lack of a source. Fill each feature's
  values **LIVE from a real source** (KSP / installed mods / computed) wherever one exists — the default, the
  extensive active display. Where no live source exists for a physically-real quantity, **SIMULATE** it: this
  **REPLACES the honest-dash fallback** (supersedes §14.4(e)(2)'s dash-emphasis and §1.4's dash-last-resort)
  **FOR READOUTS** — a coherent marked simulation is now the default fill, not a dash. A simulation MUST:
  (i) **BEHAVE live** — a coherent model driven off real vessel/cabin state, moving and responding, never a
  static constant dressed as live; (ii) compute any **SAFETY VERDICT** (leak / fire / abort / go-no-go) from
  its own model, **never hardcoded** (the S31/S32 guardrail); (iii) be **MARKED as simulated in code**
  (provenance), while reading live to the in-game crew. A **DASH** still stands only for a genuinely-absent
  state within an included feature (no target → no docking error) — which is how a real live readout reads
  with no input. **FRAMING** (subordinate to the guardrails above): it is a game, so all display is
  simulation; a simulated readout that behaves like a real live value IS the feature.
  ⛔ **SCOPE: this governs READOUTS / DISPLAYS only.** Flight **ACTUATION** — controls that fly the vehicle
  (docking clusters, the deorbit / abort / chute / EJECT panel) — is **UNCHANGED**: it stays §14.4(a)
  honest-no-op until Part B wires it (or a specific owner `OVERRIDE`), because simulating actuation forks a
  screens-only flight path Part B must reconcile. §1.4's source hierarchy (verified-real → other-users'/mod
  → simulate-marked) still governs WHICH source; (f) only changes the LAST RESORT from dash to
  coherent-marked-sim.

**All 4 tier-3 clusters RESOLVED (§14.4) → nothing in the plan now requires invention-discussion; every element
is tier-1, tier-2, or an owner-decided reconstruction. The plan is DECISION-COMPLETE and build-ready on the
owner's go** (first out-of-plan-mode action: mirror the new screens into `docs/SCREEN_INVENTORY.md` + the map
artifact, then build per §7 / §B12.6).

---

# PART C — Execution protocol, rules & task register (owner-approved 2026-09-02)
The anti-drift build harness. Owner decisions: **enforcement = a `/next` skill + `CLAUDE.md` (belt-and-braces)**;
**sessions = one task per fresh chat (manual)**; **models = Opus (hard) + Sonnet (routine)**. This is the
protocol to USE while building — it is not itself a build-go. The live gate is the §0 banner / `REGISTER.md`:
a **PREVIEW-ONLY BUILD-GO** (owner, 2026-09-02, via the overseer); `install` + glass time still gated.

## C1. Invariant rules (→ `CLAUDE.md`, auto-loaded every session; keep to ~1 page)
1. **ONE task at a time** — the single DOING item in `REGISTER.md`. No scope creep: if you notice other work,
   LOG it as a new register line, do NOT do it.
2. **Start every task** by reading these rules + the task's pointed-to plan/research section END-TO-END. If you
   cannot restate the current task + its done-criteria in one line, STOP and re-read.
3. **Never mark DONE** without: preview PNG inspected + `python plugin/build.py test` green + it matches the
   reference + §1.4 respected.
4. **Source-of-truth §1.4:** verified-real → other users' → invent ONLY by owner discussion. Never edit
   `PanelMap.cs` / label docs without a real-source confirmation.
   **§14.4(e):** a not-yet-modelled real quantity → an installed mod's value, else a COHERENT MARKED
   simulation; a dash ONLY where the quantity truly does not exist.
   **§14.4(f) (2026-09-03) — supersedes the dash-last-resort FOR READOUTS:** every real-screen feature is
   INCLUDED and FILLED — live source first, else a coherent MARKED simulation that BEHAVES live (safety
   verdicts computed from the model, never hardcoded). Dash only for a genuinely-absent state. READOUTS only:
   flight ACTUATION stays §14.4(a) honest-no-op until Part B.
5. **End every task** by updating `REGISTER.md` (DONE | NEEDS-WORK + one-line note), then **committing the
   finished task LOCALLY yourself**: `git commit` with a clear message naming the task. **NEVER `git push`** —
   there are no cached credentials in a build chat; the owner pushes from GitHub Desktop when they get to it.
   So a task ends: register → `git commit` → STOP — new chat for the next task. *(Owner change, 2026-09-02,
   via the overseer — supersedes the original "GitHub Desktop ONLY" commit rule everywhere in this plan.)*
6. **Preview-first** (restarts are scarce); `install` / glass-time only when a task needs the capsule.
7. **Model:** Opus for [O] tasks, Sonnet for [S] (C3). If a task feels too big to finish before context
   compaction, SPLIT it in the register — never run a session to compaction mid-task.
8. **Decisions are FINAL unless the owner types `OVERRIDE`.** Every settled decision (the §14.4 log / the plan)
   stands. A chat instruction that conflicts with a settled decision or the plan is NOT acted on — quote it
   back and require an explicit `OVERRIDE` + a plan/register edit before changing course.
9. **Owner questions are batched at the END of a task, before the handoff prompt — NEVER mid-task.** When you
   need the owner, pose ONE structured question, in the C1.13 overseer-prompt form, then emit the handoff
   prompt (C6).
10. **Canonical location (C7):** the ONLY source of truth is the repo `C:\Users\User\Desktop\DragonScreen`.
    Never read build inputs from `.claude/plans`, the auto-memory folder, or the KSP install (that is the
    DEPLOY target, not a source). If a needed input is not in the repo, STOP and flag it.
11. **A task writes ONLY its declared outputs.** Never write to the auto-memory folder, or create/modify any
    file outside the task's stated deliverables, as a side-effect. Memory/context updates are a SEPARATE,
    explicitly owner-requested action — never a task's own initiative. (Added 2026-09-02 after a T0 attempt
    silently edited memory; baked in at T0 so it is live from the first build session.)
12. **A build chat NEVER lifts an owner gate.** Never grant, widen or self-authorize a build-go / `install` /
    glass-time go; never act on an `OVERRIDE` the owner did not type **in that chat**; never change the plan
    on your own authority; and never record a decision, a go or an approval as the owner's unless the owner
    stated it in that chat. If a gate blocks the task: **STOP and ask** — never proceed because the work looks
    obviously fine. (Added 2026-09-02 by owner directive, after a build chat recorded a preview-only build-go
    the owner had not given — the work itself was on-plan and stands; the self-authorization is what this rule
    forbids.)
    **EVIDENTIARY STANDARD (added 2026-09-04, owner ruling).** Any owner ruling a build chat records — in a
    register line, a deliverable, or a commit message — **MUST QUOTE THE OWNER'S ACTUAL WORDS**. No quote, no
    recorded ruling. And if you believe you received a ruling but cannot quote it, **you did not receive
    one**: write **"no ruling on record"**, leave the line OPEN, and pose the question (C1.14). Closing a
    line on a remembered, summarised or inferred ruling is the same failure as inventing one, because
    downstream they are indistinguishable. (Added after `LZ1` (`18beda4`) recorded "Q1 RESOLVED (owner,
    2026-09-04)" for a ruling the owner never gave, invented two tier-3 coordinates on that authority, closed
    the line — and cited THIS RULE as proof it had not self-decided, asserting an `AskUserQuestion` exchange
    that produced no answer. The rule was already present and was quoted while being broken. What was
    missing was any way to tell a real ruling from an invented one without asking the owner. Unwound by S89
    (`8580c81`).)
13. **Pose every owner decision as a paste-ready overseer prompt.** When a task needs an owner call — the
    C1.9 batched question at the END of a task, OR a mid-task stop-and-ask when a gate / source / authority
    (C1.12 / C7) blocks the work — do NOT leave it as a bare inline question. Phrase it as a SELF-CONTAINED
    prompt addressed to the overseer: state the situation and what was already done, name the exact decision
    needed, list the discrete options, and flag which options need an owner gate-open or `OVERRIDE` (C1.12).
    The owner (Chris) pastes it to the overseer so the two can discuss and decide together; the build chat
    then acts only on the returned decision. This governs the FORM of asking only — it does NOT let a build
    chat decide a gated item itself (C1.12 still stands), and questions are still batched at the end (C1.9).
    (Added 2026-09-02 by owner directive.)
14. **Every research or build chat MUST write its open questions into its deliverable file**, under
    `## Open questions for the owner`. Each: the situation, 2-4 numbered options, and the chat's
    recommendation with reasoning. Chat-only questions do not count as asked. The overseer puts every one to
    the owner as multiple choice with a recommendation. **The owner decides. Always.** A build chat decides
    none and proceeds past none.
15. **Evidence-gated mod-first (extends §14.4(e)/(f)).** Before writing ANY new simulation for a
    not-yet-modelled real quantity, the task's OWN deliverable must record a documented search against
    `docs/reference/INSTALLED_MODS.md`: what was searched for, what candidates exist in that list, and why
    each was accepted or rejected. A candidate found but NOT installed is a proposal to the owner (C1.14),
    never a build-chat install — C7 forbids reading or modifying the KSP install directly regardless. Until
    `docs/reference/INSTALLED_MODS.md` exists, a task facing this situation STOPS and flags it (C1.12) rather
    than searching ad hoc or simulating unchecked. This exists because this session found real, already-
    installed sources (RealFuels propellant-settling state, already read by reflection in the recovered
    `Ullage.cs`; TestFlight's failure/reliability model) sitting unused while a screens-only pass had begun
    inventing simulations for adjacent quantities instead of checking first.
16. **RESEARCH IS NEVER DELETED.** Code may be deleted, rewritten or superseded at any time — it can be
    rebuilt from research. Research cannot: it has to be re-earned, and re-earning it costs more than
    keeping it. No task may delete a file under `docs/` as part of removing code. If a document is wrong,
    mark it `SUPERSEDED` per C7.1; if it is obsolete, say so in it. Deleting it is not an option a build
    chat has. (Added 2026-09-04 after `8b81816` removed ~60 research documents alongside the autopilot,
    and six later tasks — M1, W8, S60, W23, LZ1, W11 — were built without research that already existed.)

## C2. The `/next` skill (the loop — identical every task)
Invoking `/next` runs: (1) read `CLAUDE.md`; (2) open `REGISTER.md` and take THE task —

Take the first line marked DOING — a previous session stopped mid-task, pick it up rather than skip it.
If there is none, take the first line marked TODO or NEEDS-WORK. Skip DONE, SPLIT, HELD, and any line
whose status says it is blocked. If you skip a blocked line, LIST it and its blocker in your report so
blockers cannot accumulate unseen. If every remaining line is blocked, STOP and say so — never reach
past a block to find work.

— then restate it + its done-criteria in one line; (3) read its pointed-to plan/research section end-to-end;
(4) do ONLY that task (log stray findings as new register lines); (5) verify (C1.3); (6) mark the register +
`git commit` LOCALLY (never `git push` — C1.5); (7) STOP. The skill refuses to touch a second task, refuses
DONE without the verification gate, and refuses to lift an owner gate (C1.12).

## C3. Model policy
**[O] = Opus 5 (or 4.8)** — architecture, RSS/RO tuning, embed/namespace, the conductor, hard visuals
(schematics, turntable, map-modes), live-data/touch wiring. **[S] = Sonnet** — building a page from an existing
spec, nav/panel tests, mechanical edits, docs sync, research fan-out (Explore subagents). Escalate [S]→[O] if a
task stalls; never downgrade an [O] task to save cost. Higher reasoning-effort for [O], standard for [S].

## C4. Task register (ordered, atomic; copy to `REGISTER.md`. Each: [model] Task — read → build → DONE-when. All TODO.)
**Setup**
- **T0 [O] Scaffold + consolidate (C7)** — read C1/C2/C4/C7 → (1) copy the plan to `docs/BUILD_PLAN.md`;
  (2) copy the tuned Crew-2 cfg into `docs/reference/mechjeb_settings_type_Crew-Dragon.cfg`; (3) APPEND the C1
  rules to the existing `CLAUDE.md` (don't overwrite); (4) create `REGISTER.md` from C4; (5) create the `/next`
  skill from C2 → DONE: all in-repo, `/next` reads the register, nothing needed lives outside the repo.
**Part A — screens (§7 order, with this session's decisions)**
- **T1 [O] Docs reconcile (C7.1)** — read `BUILD_PLAN.md` + §14 + EVERY `docs/` file → update or mark
  `SUPERSEDED` any doc that conflicts with the plan (autopilot re-introduction, panel lighting NO-red, inert
  inferred controls, §14.1 numbers); mirror new screens + decisions into `SCREEN_INVENTORY.md` + the map
  artifact; confirm `INDEX.md` lists the authoritative set → DONE: no `docs/` file contradicts `BUILD_PLAN.md`,
  INDEX current. (SPLIT per doc-group if large.)
- **T2 [S] Menu nav-index** (`UiPage.Menu`) — read §14.4(c) + FigmaUI.cs → grid/list of all pages → DONE:
  preview + nav test (each entry routes, back works).
- **T3 [S] Reference Content view** (Cover `PhaseReference`) — read §14.4(c) + §8 + CoverPage.cs → deorbit
  quick-ref → DONE: preview.
- **T4 [O] Cover map-modes** (2D/3D + camera) — read §3 + NavPage → DONE: preview, modes switch.
- **T5 [S] Vehicle Alerts + Consumables** — read §3 + VehicleOverview/SubsystemPage → DONE: preview.
- **T6 [S] Rendezvous ellipse plot** — read §3 + §8 + Hohmann/Orbital → DONE: preview + nav test.
- **T7 [S] Deorbit Burn Prep** (reconstruct, marked) — read §3 + §8 → DONE: preview.
- **T8 [S] Entry page** (reconstruct, marked) — read §3 + §8 → DONE: preview.
- **T9 [O] Prop thruster schematic + P&ID/systems-tree deep-views** — read §11b → DONE: preview.
- **T10 [O] Lower-panel accuracy pass** — read §4 + §14.4(a,b) + PanelButtons/PanelMap/FlightCommands → lighting
  bright/no-red, audible click, SWAP + inferred-entry INERT → DONE: preview + panel test, click plays, inferred inert.
- **T11 [O] Capsule turntable** — read §5 → source the MaTte0 model → render sprites → drag-rotate → DONE:
  preview, drag rotates.
- **T12 [S] Ascent/Launch page** — read §8 + §3 → F9 schematic + event list → DONE: preview.
- **T13 [O] Live-data wiring** — read §6 + VesselData.cs → replace placeholder constants → DONE: values live in-sim.
- **T14 [O] Touch wiring** — read §6 + §4 → display-only controls → real per the decisions → DONE: controls act (+ tests).
**Part B — autopilot (§B12.6 order; all [O])**
- **T15 Embed MechJeb** pinned/namespaced; headless core loads the Crew-Dragon cfg — read §B2/B3/B12.1 → DONE:
  one core loads, no clash, cfg applied.
- **T16 Pure conductor core + tests** — read §B9/B12.2-3 + MissionPhase.cs → phase FSM as pure decisions →
  DONE: headless tests green.
- **T17 Glue driver, read-only** — read §B12.2 + _AutopilotStub.cs → report phase/engaged, no commands → DONE:
  in-sim phase matches, nothing flies.
- **T18 Wire Ascent (PVG)** — read §B8/B11 → DONE: PVG flies to insertion in-sim.
- **T19 On-orbit ops + re-plan loop** — read §B10.2/B12.4/B9 → DONE: rendezvous to the KOS in-sim. (SPLIT if large.)
- **T20 Docking hand-off + speedLimit ladder** — read §B10.3/B14 → DONE: dock in-sim.
- **T21 Undock/departure/deorbit/entry/chutes + abort wiring (§B9 Phases 6–10)** — read §B13/B10.4/B9 → DONE:
  undock clear of the KOS, return + splash in-sim, EJECT abort works.
- **T22 Empirical tune** (one param at a time vs §B11) — read §B5/B7-11 → DONE: profile matches nominal, the 4
  [EST] numbers pinned into the cfg. (SPLIT per phase.)

## C5. Setup note (do in the FRESH chat, NOT plan mode)
The plan + `docs/` are the RESEARCH the tasks read — they don't move. Create three NEW files: `CLAUDE.md` (from
C1), `REGISTER.md` (from C4), and the `/next` skill (from C2). The auto-memory already carries the durable
facts. Then run the loop: `/next` → one task → STOP → new chat → `/next`. T0 (scaffold) is first, then T1…
⚠️ Register is LIVING: split any task that won't finish before compaction; append stray findings as new lines;
never reorder past a DONE without a note.

## C6. Handoff protocol — the fresh-chat prompt (controls owner-variance)
One task per fresh chat (C1.7). At the END of every task, after updating `REGISTER.md`, emit the next chat's
**handoff prompt**. Governed by:
- **Questions first (C1.9):** if the next task or a NEEDS-WORK result needs an owner call, pose ONE batched
  question BEFORE the prompt, **in the C1.13 overseer-prompt form** — self-contained and addressed to the
  overseer (situation + what was done, the exact decision needed, the discrete options, and which options need
  an owner gate-open or `OVERRIDE`), so the owner can paste it straight through. Never carry an open question
  into the prompt.
- **Fixed + minimal prompt** — the only thing the owner types to start a build chat (less typed = less drift):
  line 1 `Read CLAUDE.md end-to-end, then run /next.` · line 2 `Next: T<n> [O|S] — open on <Opus|Sonnet>.`
- **OVERRIDE (C1.8)** protects settled decisions from casual chat.
- **Bootstrap exception (T0 ONLY):** CLAUDE.md/register/skill don't exist yet, so T0's prompt points at the plan
  (Part C) instead of `/next`, and T0 also COPIES this plan into the repo as `docs/BUILD_PLAN.md` (durable,
  version-controlled). After T0, all tasks read the repo copy — not the `.claude/plans` path.
The owner's job each chat shrinks to: open the right model, paste the prompt, answer at most one question, STOP.

## C7. Canonical location & off-limits rule (owner-directed 2026-09-02)
**THE single source of truth for the build is the repo `C:\Users\User\Desktop\DragonScreen`.** It already holds:
code (`plugin/`), research/specs (`docs/` — 19 files incl. INDEX.md, SCREEN_INVENTORY.md, SCREEN_SPEC.md,
ARCHITECTURE.md, PALETTE.md, UI_AUDIT.md, REAL_DRAGON_SCREENS.md, COMMAND_REGISTRY.md, TELEMETRY_REGISTRY.md, …),
and reference / other-users' assets (`assets/` — DillonBaird `dragon2-ui-assets`, Kenney UI, MAS
`AvionicsSystems`). **Only what is in this repo may be used to build.**

**Consolidation gaps found by the audit — pulled IN by T0 so nothing is lost:**
1. **The whole plan** (Parts A/B/C + §14) currently lives ONLY at `C:\Users\User\.claude\plans\fluttering-
   prancing-parnas.md` (ephemeral). → copy to `docs/BUILD_PLAN.md`.
2. **The tuned Crew-2 MechJeb cfg** (permanent tuning store, B2/B7–B11) lives ONLY in the KSP install
   `…\Kerbal Space Program\GameData\MechJeb2\Plugins\PluginData\MechJeb2\mechjeb_settings_type_Crew-2.cfg`. →
   copy into the repo as `docs/reference/mechjeb_settings_type_Crew-Dragon.cfg` (the §B5 tune's
   **target/reference profile**, NOT the flight-1 config — see §B5's two-profile split; the KSP-install copy
   is a runtime artifact, NOT the source).
3. **`docs/FLIGHT_SYSTEMS.md` is referenced by `MissionPhase.cs` but does NOT exist** — the §8 flight facts have
   no repo home. `BUILD_PLAN.md` carries them; create/point `FLIGHT_SYSTEMS.md` when Part B starts.

**OFF-LIMITS as build sources (never read build inputs from these):**
- `C:\Users\User\.claude\plans\` — ephemeral; superseded by `docs/BUILD_PLAN.md` after T0.
- the auto-memory folder — background recall only, not a source.
- the KSP install `…\Kerbal Space Program\GameData\` — the DEPLOY/INSTALL TARGET + runtime (where the live cfg
  is written during the tune); the only input needed from it (the tuned cfg) is copied in per gap #2. Otherwise
  write-only.
- the user's installed `MechJeb2` — the embed vendors PINNED MechJeb SOURCE into the repo (B12.1), never the
  user's install.
- external URLs / the claude.ai artifact — research is complete and captured in `docs/`; the artifact is a view.
**If a build input isn't in the repo, STOP and flag it — do not go hunting in an off-limits location.**

### C7.1 Only-the-correct-stuff (no stale / duplicate / conflicting content)
The repo must contain ONLY current, correct content — anything a build task might read must be right.
- **Single authoritative spec:** `docs/BUILD_PLAN.md` + the §14.4 decision log. On ANY conflict between an
  older `docs/` file and the plan, THE PLAN WINS; the older file is updated or marked at its top
  **`SUPERSEDED — see BUILD_PLAN.md`**, never left silently contradicting.
- **Known stale-risk points to reconcile (T1):** the AUTOPILOT — memory/docs say "deleted 2026-09-01", but
  Part B RE-INTRODUCES it (as the embedded-MechJeb conductor); PANEL LIGHTING — any "grey/white/red" or
  "red-refused" text is superseded by §14.4(a) (bright, NO red, audible click); the INFERRED panel controls are
  now INERT per §14.4(b); pre-decision numbers (WP1 220 vs 150; chute page-TBC vs FSM-trigger) per §14.1.
- **Reference vs shippable:** `assets/` (DillonBaird, Kenney, MAS `AvionicsSystems`) is REFERENCE — look,
  don't ship; the only shippable art lives in `plugin/GameData/DragonScreen/art/`. A build task never ships a
  file out of `assets/reference/`.
- **No duplicate truth:** where a copy exists twice (tuned cfg: repo vs KSP install; plan: repo vs
  `.claude/plans`), the REPO copy is authoritative; the other is a runtime/ephemeral artifact.
- ⚠️ I have NOT read all 19 `docs/` files this session — their line-level correctness is UNVERIFIED; **T1
  reconciles them before any page is built on them.**

---

## Open questions for the owner

Raised by **G5a** (2026-09-03), which baked the owner's settled booster + direct-control decisions into this
plan. **Nothing below was decided by the build chat** (C1.12); each is posed in the C1.13 overseer-prompt form.

### G5a-Q1 — WHICH MechJeb repository does T15 vendor?
**Situation.** §B3/§B12.1a now record the owner's scope: a **full and complete MechJeb2 port from the most
up-to-date GitHub source**, everything included, then **pinned** at the commit taken. "Most up to date" vs
"pinned" resolved cleanly (fetch newest, then pin and record the hash). **Which repository was never named**,
and it is not a detail: upstream and an RO fork are different trees and we inherit different ascent guidance
from each. The one hard datum is that the user's INSTALLED build has **PVG** (§B2), so whatever is vendored
must carry PVG — and C7 forbids reading that install as a source, so we cannot settle it by inspection.

**Options.**
1. **Upstream `MuMech/MechJeb2`, newest commit at port time** — the canonical tree, PVG present upstream,
   cleanest provenance and the easiest to re-pin later.
2. **An RO-oriented fork** (owner to name it) — may carry RO-specific fixes we would otherwise re-discover
   empirically, at the cost of a less canonical, possibly stale base.
3. **Owner confirms it is whatever their installed build came from**, identified by name/version so T15
   fetches the matching source from GitHub rather than the install.

**Recommendation: option 1**, unless the owner knows of a specific RO fork they rely on. Upstream carries PVG,
is the tree the RP-1 PVG guidance in §B8 is written against, and keeps the GPLv3 provenance simple; if a fork
turns out to be needed, re-pinning is its own bounded task. **A C7 exception is NOT needed** (GitHub source is
fetched as the port, exactly as §B12.1 already provides for) — but the owner must **name the repository**
before T15 fetches anything.

**RESOLVED (owner, 2026-09-03, via the overseer).** Option 1 — **T15 vendors upstream `MuMech/MechJeb2`**,
newest commit at port time, then pinned and recorded, per §B12.1a's existing "newest-then-pin" resolution.
Owner's original framing: *"if there is a RO version/fork of MechJeb2 then we want that."* Researched, not
assumed: **no current or endorsed RO fork exists.** `lamont-granquist/PrimerVectorMechJeb` — the repo where
PVG guidance was originally developed — has been **archived since 2021-07**, an experimental dev branch, not
a maintained release. Decisively: the RP-1 wiki's own `TroubleshootingMechJebPVG` page (already cited by this
plan's §B8) points RSS/RO players at the **standard Sarbian/MuMech release**
(`ksp.sarbian.com/jenkins/job/MechJeb2-Release`, CKAN 2.15+) — not a fork. Baked into §B3 and §B12.1a above.

### G5a-Q2 — Does the ASDS profile run a trim boostback, or none at all?
**Situation.** §B16.2 states the ASDS profile has **no boostback burn**. `docs/BOOSTER_GUIDANCE_METHOD.md`
§3.1/§8.1 records that the tier-2 accuracy source **does** run one on ASDS — flipped to 170°, retrograde with
a 5° offset against an aim point shifted 2700 m downrange — i.e. the **same code and the same throttle law**,
sized as a short trim rather than a return burn. Per C7.1 **the plan wins and this chat changed nothing**;
§B16.2 now carries the conflict as a flag. It matters architecturally: if ASDS trims, boostback is **one
shared state always entered** with a mode-dependent magnitude, rather than an RTLS-only optional state — and
that shape decision is cheaper to make before Wave C than after.

**Options.**
1. **Keep §B16.2 as written** — ASDS truly skips boostback; the state machine makes it optional.
2. **`OVERRIDE` §B16.2 to the source's behaviour** — one always-entered boostback state, magnitude and aim
   offset from the target mode's parameter block.
3. **Build the state as always-entered but allow a zero-magnitude configuration** — the shape of (2), the
   flown behaviour of (1) until a recorded flight rules.

**Recommendation: option 3.** It costs nothing now, cannot be wrong about the flown profile (a zero-magnitude
trim is exactly "no boostback"), and avoids a state-machine refactor mid-Wave-C if the trim turns out to
matter in RO. ⚠ Options 2 and 3 both amend a written plan section and therefore need an explicit owner
**`OVERRIDE`** plus this edit (C1.8/C1.12).

**RESOLVED (owner, 2026-09-03, via the overseer) — option 3, the chat's own recommendation, agreed.**
§B16.2 is amended (recorded there in full): **boostback becomes ONE ALWAYS-ENTERED state** for both RTLS and
ASDS profiles, with magnitude and aim-point offset parameterized by target mode; **ASDS defaults to a
ZERO-MAGNITUDE trim** until a recorded flight says otherwise — per `docs/BOOSTER_GUIDANCE_METHOD.md` §3.1/§8.1's
tier-2 source, which runs a 170° flip / 5° retrograde offset / 2700 m downrange aim on ASDS (the same code as
RTLS boostback, sized as a trim rather than a return burn). This owner statement, relayed via the overseer, IS
the C1.8 `OVERRIDE` the chat itself flagged as required to change §B16.2's written text.

### G5a-Q3 — Three docs now contradict the amended plan, and G5a was not allowed to touch them
**Situation.** G5a's declared outputs were `BUILD_PLAN.md`, `.gitignore`, `INDEX.md` and the untracked
reference/doc files (C1.11), so three files that the amendments contradict were **deliberately left alone**:
- `docs/MECHJEB_MISSION_TUNING.md` **§2.2** — *"ASDS = no boostback"* (the Q2 conflict) and **§2.4 / O4** —
  *"the craft dump cannot be filled; there are no `.craft` files in the repo"*, which is **no longer true**
  (§B16.4). Its §2.4 also carries the deleted `expect OctawebEngineCount = 9` / by-position procedure.
- `docs/BOOSTER_RECOVERY_ARCHITECTURE.md` (S60) — its banner says *"§B16 is unamended"* (now false), and its
  staged focus recommendation is superseded by the settled **§B16.7** protocol.
- `plugin/src/pure/VehicleParts.cs:37` — `OctawebEngineCount = 9`. It correctly describes the **vehicle**
  (nine nozzles) but must never be used as an expected **part count**; §B16.4 now says so. It is a `.cs` file
  and explicitly out of G5a's scope.
Per C7.1 the plan wins on every one of these, so nothing is ambiguous *for a chat that reads the plan* — but a
chat that opens S48 §2.4 first will follow a deleted procedure.

**Options.**
1. **One doc-sync register line** (G5c writes it) that marks all three: banner S48 and S60
   `PARTLY SUPERSEDED — see BUILD_PLAN.md §B12.7/§B16.4/§B16.7`, and adds the in-file caveat comment on
   `VehicleParts.cs:37`.
2. **Split it** — a docs-only line now (S48 + S60, no code) and fold the `VehicleParts.cs` comment into
   Wave B, which touches that file's neighbourhood anyway.
3. **Leave all three** and rely on C7.1's "the plan wins".

**Recommendation: option 2.** The two stale docs are the real hazard and a docs-only line closes them under
the preview-only gate with no code risk; the one-line comment on `VehicleParts.cs` is safest inside a task
that is already compiling and testing that area. Option 3 is the one to avoid — S48 §2.4 is *exactly* the
section a booster chat will open first. No gate-open or `OVERRIDE` is needed for any option.
