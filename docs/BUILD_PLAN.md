# DragonScreen — Crew Dragon Build Map & Roadmap

## 0. Plan map & status (consolidated 2026-09-02)
Two workstreams, both fully PLANNED, both on **BUILD-HOLD** (no mod code / no `install` / no glass time until
an explicit owner build-go):
- **PART A — Screens** (§1–13): the screens-only Crew-Dragon IVA UI. Research COMPLETE (§11/§13); build order
  §7; lower analog panel §4; capsule turntable §5; live-data/touch §6. ~18 pages built (owner-provisional).
- **PART B — MechJeb autopilot core** (B1–B15, incl. B6 risks): reintroduce flight software as an embedded,
  pinned, privately-namespaced MechJeb driven by a "conductor". Research COMPLETE (how-to-tune B7–B10 +
  flight-data targets B11); build architecture DESIGNED (B12); abort / crew-gate / FDIR researched (B13–B15);
  **coherence pass + source-tier map done (§14); all 4 tier-3 "invention" clusters RESOLVED with the owner
  (§14.4)** — the plan is DECISION-COMPLETE (build-hold still in force until an explicit build-go).
Execution is governed by **PART C** — the anti-drift harness (a rules→one-task→verify→register LOOP, run by a
`/next` skill + `CLAUDE.md`; Opus-for-hard / Sonnet-for-routine; one task per fresh chat). First task = T0
(scaffold the harness), then T1 (docs sync) onward. Build-hold until the owner's go; commit via GitHub Desktop ONLY.

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
(KSP + CKAN closed) + owner screenshots only for what needs the capsule. Commit/push via GitHub Desktop only.

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

### B0. Part B reading order & contents (numbers are labels, THIS is the order)
B1 Direction · B2 Grounding (MechJeb installed · Crew-2 cfg · GPLv3) · B3 Packaging (embed pinned/namespaced —
LOCKED) · B4 Conductor model · B5 Tuning methodology (knowledge-first → one-by-one empirical vs real data) ·
B7 Ascent tuning first-cut (mechanics) · B8 Ascent FULL guidance · B9 Full mission sequence (every phase → op
→ knobs) · B10 On-orbit modules FULL per-parameter guidance · B11 Flight-data TARGET reference ([DOC]/[EST]) ·
B12 Build architecture (the conductor: embed · pure core + glue driver · phase FSM · re-plan loop · screen
front-end · build order) · B13 Abort system · B14 Crew-gate procedures · B15 FDIR/fault detection · B6 Honest
risks. (Cross-cutting capstone: **§14 Coherence pass & source-tier map**, at the very end.)

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
cfg. Knowledge first (this task) → then the one-by-one empirical tune. The existing `Crew-2` cfg is the
starting profile to refine, not the finished answer.

## B7. Ascent tuning reference — FIRST CUT (MechJebModuleAscentSettings, source + Crew-2 cfg)
`AscentType` enum = **CLASSIC (0) / PVG (1)** → Crew-2 `AscentTypeInteger = 1` = **PVG** (RSS/RO-correct).
"How to set" mechanics: bool = cfg `Name = True/False`; scalar = `Name { ValConfig=<internal SI>, TextConfig=
<GUI display> }`. `EditableDoubleMult` fields keep internal SI in ValConfig + a scaled display in TextConfig
(e.g. `TurnStartAltitude` Val 500 m / Text 0.5 km; `DynamicPressureTrigger` Val 10000 Pa / Text 10 kPa) — set
via the module's Editable field (handles the scale) or write both cfg values. (WebFetch's "units" for Mult
fields were the DISPLAY unit; ValConfig is the authority.)

Ascent params grouped (name · type · stock default · role):
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
is optimal for PVG; autostaging MUST be on for its prediction; don't change staging after liftoff (Reset
Guidance instead).

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
  between stages; keep unless a specific coast is needed. Autostage ON.
- **Attach altitude / FPA:** AttachAltFlag + DesiredAttachAlt/FPA — force a specific insertion (burnout
  elevation). Crew-2 sets AttachAlt 210 km + FixedCoast — ⚠️ VERIFY this matches Dragon's real insertion vs
  letting PVG free-optimize; attach is mainly for shuttle-style 90×180 inserts.
- **Throttle:** RP-1 says PVG wants **bang-bang (full throttle), limiters OFF**. ⚠️ Crew-2 has
  `ThrustController.LimiterMinThrottle=True` — REVIEW: real F9 throttles down around max-Q + for a G-limit,
  which conflicts with PVG's bang-bang assumption. Decide whether to model F9's real throttle program or accept
  PVG bang-bang. (A genuine RSS-accuracy vs PVG-optimality tension to resolve empirically.)
- **Staging flags** (StagingController): ⚠️ Crew-2 `HotStaging=True` — real F9 does COLD stage sep, so review
  hot-staging lead-time semantics. FairingMaxDynamicPressure 5 kPa / FairingMinAltitude 50 km control fairing
  jettison — Dragon has no fairing (hinged nose cone stays), so confirm these don't mis-trigger.
- **Attitude (BetterController PID)** — Crew-2 PosKp 2.03 / PosTi 1.97 / VelKp 7.98 / RollControlRange 5 /
  MaxStoppingTime 2 / MinFlipTime 120 / Soften 0.5. The launch pointing controller; tune only if the stack
  oscillates or is sluggish on the gravity turn.

**Open ⚠️ flags to resolve empirically (RSS-accuracy vs PVG-optimality):** throttle limiter vs bang-bang;
hot vs cold staging; attach-altitude vs free-optimize; fairing logic on a fairingless Dragon.

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
IDA-2).** Two options the conductor can take: (a) **Docking Autopilot** — knob **speedLimit** (m/s; Crew-2 = 1;
real Dragon creeps in far slower, ~0.1–0.2 m/s at contact → tune speedLimit DOWN through the waypoints), plus
approach-distance/roll-alignment settings, on Draco RCS; or (b) **hand off to the Manual ISS Docking screen**
(already built) for crew-flown final approach, with **SmartASS TARGET/parallel** holding the pointing. CHOP =
last abort point (the panel BREAKOUT function, §4). ⚠️ Decide autopilot-docks vs screen-hands-off (owner call);
tune speedLimit ladder to the real keep-out/waypoint speeds.

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
descent prediction. Tune: entry attitude + any bank; Dragon is a low-L/D ballistic-ish entry, so mostly
attitude-hold, not active guidance. ⚠️ Confirm whether we model lifting-entry bank or pure ballistic.

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
16 Dracos), **Node Executor** (flies every planned node), **Staging** (ascent only), **Warp Helper** (skip the
long phasing coast), **Flight Recorder** (the Q/AoA/pitch graphs that drive the §B8 ascent tune). ⚠️ Open
per-phase decisions to resolve empirically: phasing-orbit altitude ladder (P2), transfer optimize vs simple
(P3), docking autopilot vs manual-screen hand-off + speedLimit ladder (P4), entry lifting vs ballistic (P8),
chute-altitude schedule (P9). Each of these gets the one-parameter-at-a-time flight-data tune (§B5) once built.

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
  the IDA-2 port; safe-distance ≈ the Keep-Out Sphere. ⚠️ Owner decision (P4): docking AP vs hand-off to the
  Manual ISS Docking screen; if hand-off, this module idles and SmartASS `parallel_plus` holds pointing.

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
**surface_retrograde** (heat-shield forward) + force_roll for any bank · docking (P4) = **target_plus /
parallel_plus** · departure (P6) = retrograde/target_minus. No cfg persistence — pure API per phase.

### B10.6 RCS control — the 16-Draco translation/rotation. Persisted: RCSController PID + RCSBalancer.
- **RCSController** (attitude-hold-on-RCS PID) — cfg **Tf 1 · Kp 0.125 · Ki 0.07 · Kd 0.53**. The RCS attitude
  gains; tune only if Dragon is jittery/sluggish holding attitude on Draco. API fields Tf/Kp/Ki/Kd.
- **RCSBalancer** — cfg smartTranslation **False**, overdrive 1 (100%), overdriveScale 0.9, tuning factors
  (torque 1 / translate 0.005 / waste 1). Balances thruster groups for pure translation (prox-ops). Target:
  enable **smartTranslation=True** for clean docking translation if cross-coupling shows up. **SmartRcs** (cfg
  EMPTY, live) = the translate-toward-target helper during prox-ops. ⚠️ Tune during the P3/P4 empirical pass.
- **ThrustController** — cfg LimiterMinThrottle **True**, MinThrottle 0, DifferentialThrottle False. (Ascent
  bang-bang flag lives here — see the §B8 throttle ⚠️.) On-orbit: leave default.

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
  entry attitude/bank target (P8), lifting-vs-ballistic ⚠️. Heat shield ~1927 °C **[DOC]**.
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
live** (§B12.4), then hand off at the Keep-Out Sphere to the Docking AP **or** the Manual ISS Docking screen
(§B10.3 owner ⚠️) · **Docked** → idle/KILL-ROT · **Entry** → deorbit via OperationPeriapsis beforehand, then
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

### B12.6 Single-core safety, packaging, testing
- **One commanding core:** detect a user's own MechJeb; ensure exactly ONE `MechJebCore` actually commands the
  Dragon (use ours; never double-drive). Belt-and-braces: our core is the private-namespace one.
- **Testing (mandatory, per the glue law):** headless tests for the pure conductor core (feed synthetic
  telemetry+phase, assert the `ConductorAction` + phase transitions + re-plan triggers) — the analogue of
  `FigmaUINavTest`. The glue driver stays thin enough to eyeball. `python plugin/build.py test` must stay green.
- **Build order (when build-go given):** (1) embed+namespace+headless-load one MechJebCore, prove it loads the
  Crew-Dragon cfg; (2) pure conductor core + tests (phases as pure decisions); (3) glue driver implements the
  stub surfaces read-only (report phase/engaged) — no commands yet; (4) wire Ascent (PVG) end-to-end + verify
  in-sim; (5) wire on-orbit ops + the re-plan loop; (6) docking hand-off; (7) deorbit/entry/chutes; (8) begin
  the §B5 one-parameter-at-a-time empirical tune against §B11 targets. Each step preview/test-gated; install +
  glass time only when a step needs the capsule; commit/push via GitHub Desktop only.

## B13. Abort system — research + conductor design
The Crew-Dragon Launch Abort System (LES) + on-orbit contingency aborts, and how the conductor implements them.
Sources: Wikipedia *Crew Dragon Launch Abort System*, CBS *rescue scenarios*, NASA escape-system release,
Space.com Demo-2 steps, the §4 panel research. **Abort is NOT a MechJeb module** (MechJeb has none) — it is
conductor-owned, composed from the KSP abort action-group + SmartASS + the chute logic, with the mode
PHASE-SELECTED exactly as the real vehicle autonomously selects it.

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
  2. **Ascent abort:** fire the KSP **Abort action group** (SuperDraco staged on the Dragon part) for pad/1a/1b;
     for 2a+ command S2 separation + Draco/SuperDraco shaping burns (SmartASS + RCS); 2e → resume on-orbit.
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

## B6. Honest risks
GPLv3 source-shipping obligation (public); MechJeb version pinning + private-namespace build tooling; **RSS/RO
ascent tuning (PVG) is the genuinely hard part**; the conductor's live re-plan state machine is the main new
code; ensuring a single MechJebCore (no double-run with a user's MechJeb).

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
- **(b) inferred panel semantics — RESOLVED 2026-09-02 (inert until verified):** keep POWER/STRING/RESET as
  display-state; **SWAP 1/2/3 + the inferred entry-mode toggles (ENTRY REBOOT / BACKUP ENTRY / NORMAL ENTRY) go
  INERT** (click, no function, unlit) until a real console-procedure source verifies them. Confirmed commands
  (ENABLE BACKUP PYROS, FIRE PYRO) stay. (⚠️ RESET 1/2 is in §4's inferred list but kept per the owner's choice.)
- **(d) Suit-Leak fail branch — RESOLVED 2026-09-02: KEEP, marked as a reconstruction.** A leak check must have
  a fail path (reconstruct-from-function); exact step wording (Failed Low / TROUBLESHOOT / step 2.5) is marked
  reconstructed, not verified-real. Drops to tier-2. Honors the earlier add-don't-take-away rule.

**All 4 tier-3 clusters RESOLVED (§14.4) → nothing in the plan now requires invention-discussion; every element
is tier-1, tier-2, or an owner-decided reconstruction. The plan is DECISION-COMPLETE and build-ready on the
owner's go** (first out-of-plan-mode action: mirror the new screens into `docs/SCREEN_INVENTORY.md` + the map
artifact, then build per §7 / §B12.6).

---

# PART C — Execution protocol, rules & task register (owner-approved 2026-09-02)
The anti-drift build harness. Owner decisions: **enforcement = a `/next` skill + `CLAUDE.md` (belt-and-braces)**;
**sessions = one task per fresh chat (manual)**; **models = Opus (hard) + Sonnet (routine)**. Build-hold is
still in force — this is the protocol to USE when the build begins, not itself a build-go.

## C1. Invariant rules (→ `CLAUDE.md`, auto-loaded every session; keep to ~1 page)
1. **ONE task at a time** — the single DOING item in `REGISTER.md`. No scope creep: if you notice other work,
   LOG it as a new register line, do NOT do it.
2. **Start every task** by reading these rules + the task's pointed-to plan/research section END-TO-END. If you
   cannot restate the current task + its done-criteria in one line, STOP and re-read.
3. **Never mark DONE** without: preview PNG inspected + `python plugin/build.py test` green + it matches the
   reference + §1.4 respected.
4. **Source-of-truth §1.4:** verified-real → other users' → invent ONLY by owner discussion. Never edit
   `PanelMap.cs` / label docs without a real-source confirmation.
5. **End every task** by updating `REGISTER.md` (DONE | NEEDS-WORK + one-line note) and committing via GitHub
   Desktop ONLY. Then STOP — new chat for the next task.
6. **Preview-first** (restarts are scarce); `install` / glass-time only when a task needs the capsule.
7. **Model:** Opus for [O] tasks, Sonnet for [S] (C3). If a task feels too big to finish before context
   compaction, SPLIT it in the register — never run a session to compaction mid-task.
8. **Decisions are FINAL unless the owner types `OVERRIDE`.** Every settled decision (the §14.4 log / the plan)
   stands. A chat instruction that conflicts with a settled decision or the plan is NOT acted on — quote it
   back and require an explicit `OVERRIDE` + a plan/register edit before changing course.
9. **Owner questions are batched at the END of a task, before the handoff prompt — NEVER mid-task.** When you
   need the owner, ask ONE structured question (with options), then emit the handoff prompt (C6).
10. **Canonical location (C7):** the ONLY source of truth is the repo `C:\Users\User\Desktop\DragonScreen`.
    Never read build inputs from `.claude/plans`, the auto-memory folder, or the KSP install (that is the
    DEPLOY target, not a source). If a needed input is not in the repo, STOP and flag it.
11. **A task writes ONLY its declared outputs.** Never write to the auto-memory folder, or create/modify any
    file outside the task's stated deliverables, as a side-effect. Memory/context updates are a SEPARATE,
    explicitly owner-requested action — never a task's own initiative. (Added 2026-09-02 after a T0 attempt
    silently edited memory; baked in at T0 so it is live from the first build session.)

## C2. The `/next` skill (the loop — identical every task)
Invoking `/next` runs: (1) read `CLAUDE.md`; (2) open `REGISTER.md`, take the first non-DONE item as THE task,
restate it + its done-criteria in one line; (3) read its pointed-to plan/research section end-to-end; (4) do
ONLY that task (log stray findings as new register lines); (5) verify (C1.3); (6) mark the register + commit;
(7) STOP. The skill refuses to touch a second task, and refuses DONE without the verification gate.

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
- **T21 Deorbit/entry/chutes + abort wiring** — read §B13/B10.4/B9 → DONE: return + splash in-sim, EJECT abort works.
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
- **Questions first (C1.9):** if the next task or a NEEDS-WORK result needs an owner call, ask ONE batched
  question BEFORE the prompt; never carry an open question into the prompt.
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
   copy into the repo as `docs/reference/mechjeb_settings_type_Crew-Dragon.cfg` (canonical starting profile;
   the KSP-install copy is a runtime artifact, NOT the source).
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
