# DragonScreen — SCREEN INVENTORY & RESEARCH BASE (updated 2026-09-02)

The consolidated catalogue of EVERY real Crew Dragon touchscreen page: what it is, what evidence we
have, and whether we've built it. Feeds the Figma-rebuild loop. Extends `SCREEN_EVIDENCE_MATRIX.md` /
`SCREENS_LOOK_AND_FUNCTION_RESEARCH.md` with the new real-photo + web evidence gathered this session.
**Subordinate to `docs/BUILD_PLAN.md` (C7.1)** — the plan's §3 map and §14.4 decision log win on any
conflict. The published *Dragon Screen Map* artifact is a **view of this file**, mirrored outward; this
file is the source, never the other way round.

## Decisions mirrored in from the plan (§14.4, owner, 2026-09-02) — these are FINAL

| Cluster | Decision | Effect on this inventory |
|---|---|---|
| **(c) Menu** | **RESOLVED — reconstruct-from-function.** `UiPage.Menu`, opened by the Cover's Menu button, is a **navigation index**: a grid/list of every built page, tap to jump. Content is real pages; only the layout is ours. Drops tier-3 → tier-2. | **Menu leaves 🔴 no-reference** — it is now DESIGN SET and buildable (**T2**). |
| **(c) Reference Content** | **RESOLVED — reconstruct-from-function.** NOT a standalone page: it is the Cover phase-rail slot `PhaseReference`, showing a **deorbit quick-reference** built from the §8 real flight data (entry timeline, altitudes, abort/contingency notes). | **Reference Content leaves 🔴 no-reference** — DESIGN SET, buildable as a Cover view (**T3**). |
| **(a) Panel lighting** | Buttons light **BRIGHT** when active/armed/fired; **NO red** (no evidence of a red button — the old red-on-refusal is removed); rest unlit; **audible CLICK** on every mechanical press. A refused/inert press = click, no light, no action. | Console behaviour only; supersedes the grey→white→red scheme in `REAL_DRAGON_SCREENS.md` (**T10**). |
| **(b) Inferred panel semantics** | POWER 1/2 · STRING 1A–2C · RESET 1/2 · fire/leak response · the CONFIRMED entry commands stay **real display-state**. **SWAP 1/2/3 and the inferred entry-mode toggles go INERT** until a real console-procedure source verifies them. | Console behaviour only (**T10**). |
| **(d) Suit-Leak fail branch** | **KEEP, marked as a reconstruction.** A leak check must have a fail path; the exact wording (Failed Low / TROUBLESHOOT / step 2.5) is marked reconstructed, not verified-real. Drops to tier-2. | Confirms screen **#5** below as-built — do not delete the branch for lack of a frame. |

**§14.1 numbers, so nothing “fixes” them the wrong way:** WP1 = **220 m nominal** (the ~150 m Crew-4 figure
is mission-dependent — mark the range) · Keep-Out Sphere ≈ **200 m**, sources ambiguous radius-vs-diameter,
kept marked · **the chute altitudes are INTENTIONALLY two different things** — the FSM trigger constants
(5486 m drogues / 1830 m mains in `MissionPhase.cs`) are the real numbers, while the Manual Chute page's
“(TBC)” altitudes are SpaceX's own placeholder text kept verbatim. **Neither should be edited to match the
other.**

**S13 divergence note (owner, 2026-09-02, via the overseer):** screen **#24** below, "Deorbit Burn Prep" —
the Crew Interrupt Conditions / Slew for Deorbit Burn criteria — read **ATTITUDE**, not altitude. Every
source that names them (this file's own blurry-photo transcription, `SCREENS_LOOK_AND_FUNCTION_RESEARCH.md`'s
read of the community First.vue source, and the community Figma's own baked asset filename
`600deg_m_altitude_rate`) literally says "altitude", but that doesn't parse physically for a rotational,
degrees/degrees-per-minute slew criterion paired with Roll/Pitch/Yaw — and the tier-1 photo transcription is
the likely origin, with the tier-2 sources sharing one lineage rather than confirming it independently, so
§1.4 "verified-real" is weak here and physics is decisive (C1.4/C7.1). Corrected below and in
`plugin/src/pure/DeorbitBurnPrepPage.cs` (full writeup), `CoverPage.cs`'s asset-key comment, and
`SCREENS_LOOK_AND_FUNCTION_RESEARCH.md` together. The literal community assets still read "altitude".

## Headline fact
The real Dragon UI has **~25–30 individual pages** (DillonBaird recreation, from a SpaceX crew-displays
source; astronaut Doug Hurley quote). We now have **~18 built** (Cover, HUD, Vehicle Overview/All + Mech + 6 subsystem tabs, Suit Leak Check,
VRIO 4.700, Audio, Cabin, Video, **Manual Chute Deploy**, **Manual ISS Docking**), **strong real reference**
for the deorbit procedure pages **Deorbit Burn Prep** and **Entry** (🔴 not built) and the systems P&ID
schematic look, and **thin/no reference** for Ascent and Alert/Fault. *(Reference Content and Menu were in this
sentence until 2026-09-02 — §14.4(c) resolved both; see the decisions table above. Ascent has no frame
but its DATA is known from §8, so it is layout-reconstructed rather than dark. Standalone Nav/map is a
Cover globe mode, not a page — see #12.)*

## Sources (strongest first)
- **REAL_SPACEX_SCREENSHOTS/** — the actual capsule displays. Now includes **hi-res `discovery*.jpg`
  (4128×2322)** + `crew*/inspiration4*/ui1 (1).jpg` (2048–2880 wide) added 2026-09-01: FAR more legible
  than the earlier ~300–500px shanemielke thumbnails. Crop the centre monitor at full res + upscale ~1.6×
  to read procedure text (see `discovery2`=suit-check, `discovery14/15`=manual chute, etc.). Highest fidelity.
- **Shane Mielke** — the Principal UI/UX designer of the real Crew Dragon flight displays (2018-2020);
  showcase at shanemielke.com/work/spacex/crew-dragon-displays/ (30+ images, unnamed) and the
  **ISS Docking Simulator** (iss-sim.spacex.com) built from the real UI.
- **DillonBaird "Recreating the Crew Dragon UI in 60 Days"** (dillonbaird.io/articles/mutantdragon) —
  structural page breakdown; states the "25 to 30 individual pages" count.
- **Community Figma** (fileKey k1X6Ytu9rEVcveKrS1Re5s): 9 page frames on Page 1 (Cover=Frame67, HUD=Frame58,
  Procedure=Frame59, Cabin=Frame66, A-Settings ×5); the second "Cover" page (12221:242) is UNSCANNED (MCP
  rate-limited) and may hold more.
- **neel-dandiwala demo** (github SpaceX-Dragon2-UI) — 5-panel reference (behaviour only).
- Prior docs: `SCREEN_EVIDENCE_MATRIX.md`, `SCREENS_LOOK_AND_FUNCTION_RESEARCH.md`, `UI_AUDIT.md`.

## Navigation model (confirmed)
- **Global nav bar** — pinned bottom (component_48): 5 page icons + a sliding white marker under the
  active one, CURRENT STATE, POINTING MODE, SPX/ISS link, MET. Always visible.
- **Sub-tabs** within a page (Overview/Mech; Audio/Cabin/Video), and a **left phase/procedure rail** on
  the deorbit dashboard.
- Red alerts surface on the nav for a subsystem with a fault.

## SCREEN INVENTORY
Status (legend updated 2026-09-02 to match the plan's §3 classes): **✅ built** · **🟦 DESIGN SET** —
§14.4 resolved it as reconstruct-from-function, buildable now · **🟠 REF, not built** — real reference in
hand, needs building · **🟡 REFINE** — folds into a page we already ship, not a new page · **🔴 no reference**
— evidence of existence only.

| # | Screen | Function | Best evidence | Status |
|---|--------|----------|---------------|--------|
| 1 | **Deorbit dashboard / Cover** (Frame 67) | Deorbit home: LEFT phase rail, centre procedure card + phase heading, live globe/map (3 view modes via NEXT VIEW), top orbit telemetry | Figma Frame67 + photos | ✅ (rail now the real **7 items**, 2026-09-01) |
| 2 | **Attitude / Docking HUD** (Frame 58) | Synthetic attitude bowl + graticule, ROLL/PITCH/YAW (green corr / blue rate), X/Y/Z, RANGE/RATE, 4 corner rings, accel gauge; nose-open → docking cam in bowl | Figma Frame58 + photos | ✅ (values not yet live) |
| 3 | **Vehicle Overview** | Cabin atmosphere gauges (PPO2/temp/press/CO2), coolant loops, net power, connections, cabin mics, capsule diagram; SYSTEMS/CABIN + Overview/Mech tabs, MORE→Power/Engine/Comms | demo Panel 3 + docs | ✅ (rep. data; MORE subpages 🔴) |
| 4 | **Mech Panel** | Radial mechanical schematic: central SEATS tachs + nodes (ACCELERATION/CENTRIPETAL/PRESSURE/RESISTANCE/WATER UPRIGHTING) | demo Mech | ✅ (rep. data) |
| 5 | **Suit Leak Check (4.011 · ECLSS)** | Procedure: INITIATE/HALT, per-suit DELTA PRESSURE + STATUS(Nominal), TIME REMAINING, step 2.5, TROUBLESHOOT fail branch, completion popup | **hi-res photos (discovery2/3)** + demo | ✅ UPGRADED 2026-09-01 to real wording (ECLSS subtitle, "SECTION 2: IN PROGRESS", "INITIATE SUIT LEAK CHECK", 2.4 "…nominal after time remaining is 0", TIME REMAINING row, STATUS=Nominal, real caution "critical…final 15 seconds…values") + reconstructed below-fold failure branch (Failed Low / TROUBLESHOOT / TRY ADDITIONAL TIMER / 2.5 Contact SpaceX). Popup = title/ECLSS/CLEAR/PROCEDURE COMPLETE/visor text |
| 6 | **Test VRIO Health LEDs (4.700)** | Procedure: deorbit-prep checklist; START/STOP VRIO 1&2 LED tests, verify, report; engineering notes | REAL PHOTOS (this session) | ✅ BUILT this session (`VrioTestPage`) |
| 7 | **Audio Settings** | Per-seat audio (Seat1/2/Cabin/3/4): GROUND/AUX/MAIN(Vox)/INTERCOM/ALERTS | Figma A-Settings + photos | ✅ |
| 8 | **Cabin Settings** (Frame 66) | Cabin hero image + LIGHTING (4 display columns of toggles) | Figma Frame66 + photos | ✅ (frame render) |
| 9 | **Video Settings** | Vehicle's real cameras (docking/hull feed) + resolution | our build (themed) | ✅ |
| 10 | **Procedure (generic, Frame 59)** | A procedure/checklist template page | Figma Frame59 | 🟡 static frame render |
| 11 | **ISS Docking (manual)** ✅ BUILT 2026-09-01 (`DockingSimPage`, `UiPage.Docking`, reached from the HUD margin) | Manual prox-ops. FULL SPEC from iss-sim.spacex.com DOM (2026-09-01): two concentric **HUD Rings** + centre reticle over the docking-adapter cam; **ROLL / PITCH / YAW** labels + **PYR** with three axis values ("180.0" ×3, green when corrected); **RANGE** + **RATE** readouts (RATE < 0.2 m/s required below 5 m); a **rotation control cluster** (Roll/Pitch/Yaw ±) and a **translation cluster** (Up/Down/Left/Right/Fwd/Back), EACH with a centre **precision toggle** (LARGE ↔ small / "Toggle Translation Precision"); bottom controls **Instructions · Reset Positions · Settings**; **SUCCESS / FAIL** end states. Blue numbers = rates, green diamond = target. | **iss-sim.spacex.com (live DOM, definitive)** + the YouTube "actual interface" video (`MdJDBHzJF8E`) which the sim links as its real-UI reference | 🔴 distinct from #2; owner idea = hidden mini-game |
| 12 | **Navigation (2D/3D map)** | **NOT a separate page** — the Cover's live globe gains 2D/3D map modes + a camera-mode toggle (vehicle pos, orbit track, ground stations, ISS, sun, predicted splashdown). DillonBaird's alt-text: 2D navigated by d-pad, 3D by touch-and-drag | DillonBaird nav render + alt-text | 🟡 REFINE of the Cover globe (**T4**) |
| 13 | **Proximity / Vehicle (ISS line-drawing)** | ISS truss+arrays line art, target marker, circular gauges, right data columns, scale bar | photos (docs matrix) | 🔴 — but see **#23/#28**: the rendezvous ellipse + circular nav plot probably ARE this screen |
| 14 | **Ascent / Launch** | Falcon 9 vertical schematic + the real ascent-event list (liftoff · pitch kick · max-Q · MECO · sep · SECO-1 · Dragon sep · nose-cone open) | **DATA known** (§8 mission timelines); layout inferred — no public in-cabin frame exists | 🟦 DATA-BUILDABLE, layout reconstructed + MARKED (**T12**) |
| 15–22 | **Vehicle subsystem pages** — the Vehicle page's real sub-tab bar is **All · Crew · Prop · Mech · Power · Avionics · GNC · Thermal** (8 tabs, CONFIRMED from ui1.jpg mockup) | Each a subsystem overview + alerts | shanemielke ui1.jpg (clean mockup) + DillonBaird | ✅ ALL 8 BUILT (2026-09-01) — `VehicleTabBar` (shared 8-tab strip) + `VehicleSubsystemPage` (Crew/Prop/Power/Avionics/GNC/Thermal); All=`VehicleOverviewPage`, Mech=`VehicleMechPage`. Rep. data; real telemetry later |
| … | **More procedure pages** | The phase rail's real items — **Deport & Burn, Coast to Trunk Jettison, Claw Separation Prep, Procedure, (2nd) Procedure, Reference Content, Manual Chute Deploy** — each with numbered command steps (like 4.011, 4.700) | REAL PHOTOS (expanded rail) | 🔴 many |
| … | **Reference Content** | **Deorbit quick-reference** — entry timeline + altitudes + abort/contingency notes, built from the §8 real flight data. A Cover phase-rail VIEW (`PhaseReference`), not a standalone page | §14.4(c) + §8 real data | 🟦 DESIGN SET (**T3**) |
| … | **Menu** | **Navigation index** — a grid/list of every built page, tap to jump (`UiPage.Menu`, opened by the Cover Menu button). Fills the ~25–30-page need the 5-icon bar can't | §14.4(c); content = real pages, layout ours | 🟦 DESIGN SET (**T2**) |
| … | **Alert / Fault** | **NOT a page** — a FUNCTIONS/ALERTS toggle on every Vehicle subsystem page + red sub-nav routing | DillonBaird Vehicle render | 🟡 REFINE of the subsystem pages (**T5**) |
| 23 | **Rendezvous orbital-ellipse plot** | Left icon sub-nav rail + a “Hold Capture” procedure card + a large **2D orbital-ellipse plot** with the vehicle position and an approach chord. The CENTRE situational-awareness display's rendezvous view | BBC `touchscreens.png` (all three screens, Hold-Capture phase) | 🟠 REF, not built (**T6**) — likely also covers the Proximity/ISS plot gap (#13) |
| 24 | **Deorbit Burn Prep** | “Crew Interrupt Conditions” + “Slew for Deorbit Burn” (Roll/Pitch/Yaw, max attitude rate — see S13 divergence note above, FC Slew) + numbered settle-burn steps (~3 min prior, 1 s pulses at 30 s intervals, 8 min duration) | `discovery` deorbit-burn frames (blurry) | 🟠 REF, not built — reconstruct + MARK (**T7**) |
| 25 | **Entry** | “Parachute Deployment Altitude” section + steps — the entry/descent procedure page | `discovery` “Entry” frame (partial) | 🟠 REF, not built — reconstruct + MARK (**T8**) |
| 26 | **Prop / RCS thruster schematic** | The AUTHENTIC look of Vehicle·Prop: the Dragon in **horizontal profile** (capsule + trunk line-art) ringed by **Draco thruster-quad arc symbols** with per-cluster firing/status, per-thruster data along the bottom, a LEFT alert + sub-nav rail | **JSC `jsc2026e404727`** (Crew-13 training) | ✅ BUILT 2026-09-02 (**T9**, `PropSchematic.cs`) — Prop's FUNCTIONS view now draws this schematic, per-thruster firing LIVE off real RCS demand, replacing the generic gauge template |
| 27 | **Systems / electrical TREE** | A **hierarchical box-and-connector diagram** (labelled boxes joined by connector lines) — a power-distribution / systems tree. A subsystem deep-view, distinct from the P&ID plumbing view | **JSC `jsc2024e064449`** (sim rig, LEFT screen) | ✅ BUILT 2026-09-02 (**T9**, `SystemsTreePage.cs`, `UiPage.SystemsTree`) — boxes/connectors live-coloured off `PageState.Systems` |
| 28 | **Nav / orbit plot (circular)** | Concentric rings + coloured target markers (yellow + cyan) + orbit arcs + a g/rate readout — the circular situational plot. Pairs with #23's ellipse view | **JSC `jsc2024e064449`** (sim rig, RIGHT screen) + BBC | 🟠 REF, not built — owned by **S15**, not T9 (T6 is the rendezvous *ellipse*; T9 is the other two JSC screens; neither register line covers this one) |

## NEW findings this session (record)
- **4.700 "Test VRIO Health LEDs" is REAL** — reconstructed from photos + built (`VrioTestPage`). VRIO =
  redundant I/O for the flight-computer / automated-chute backup path; LEDs are zero-fault-tolerant health
  lamps the crew tests before entry. Shares the Suit-Leak-Check procedure template (left checklist / centre
  numbered commands / right notes).
- **The deorbit phase rail has 7 items**, not our 5: Deport & Burn · Coast to Trunk Jettison · Claw
  Separation Prep · Procedure · Procedure · Reference Content · Manual Chute Deploy. **DONE (2026-09-01):**
  the community Figma baked only 5 rail rows, so the rail is now redrawn as 7 primitive rows (ring marker
  + 2-line label) with the baked 5 labels/dots skipped; the baked highlight box + cyan underline + big
  heading track the selected phase across all 7. `CoverPage.PhaseCount=7`, `PhaseButton[]`/`PhaseOf`,
  SlotY 7 slots; painter's ◄/► wrap over 7; 75 nav checks pass.
- **Suit Leak Check UPGRADED to the real page (2026-09-01)** from full-res frames (discovery2 = in-progress,
  discovery3 = completion popup): ECLSS subtitle, "SECTION 2: IN PROGRESS", "INITIATE SUIT LEAK CHECK",
  2.4 "…statuses are nominal after time remaining is 0", table row "TIME REMAINING IN LEAK CHECK", STATUS =
  "Nominal" (green), real caution ("critical…final 15 seconds…accurate…values"), popup = title/ECLSS/CLEAR/
  PROCEDURE COMPLETE/visor text. The main table shows "Scroll to continue" + FINISH → there is MORE
  procedure below the fold than any single frame captures. Per the owner: **absence in a photo ≠ absence,
  and research only ADDS, never removes** — so the fail branch is reconstructed (not deleted for lack of a
  frame): STATUS can read "Failed Low", a right-column "Did any suit fail the leak check? → TROUBLESHOOT /
  TRY ADDITIONAL TIMER" block, and step "2.5 Contact SpaceX to report results." Marked in-code as
  reconstructed/below-fold. TROUBLESHOOT/TIMER display-only until the touch pass.
- Procedure pages are **numbered by section** (2.x on the suit check, 4.x on VRIO / deorbit prep) with a
  NEXT to advance — a whole procedure-sequence system, likely the bulk of the ~25–30 pages.
- **NEW SCREENS exposed by the hi-res `discovery*.jpg` frames (2026-09-01) — 🔴 not built, strong reference now:**
  - **Manual Chute Deploy** (`discovery14/15`, and a "Complete (FC Failed)" variant): two sections —
    **High Altitude Chute Deploy** and **Standard Altitude Chute Deploy** — each a command list with
    altitude gates (e.g. "10.0 km (TBC) 6 nm drogues", "2.2 km (TBC) 6 nm mains") and steps ENABLE BACKUP
    PYROS / DEPLOY DROGUES / FIRE PYRO / DEPLOY MAINS, each with an Arm-and-verify / Execute / Latch /
    Check-latched action. This is the Cover phase-rail item #7 (Manual Chute Deploy). Rich — its own build.
  - **Deorbit Burn Prep** (`discovery` deorbit-burn frames): "Crew Interrupt Conditions" + "Slew for
    Deorbit Burn" (Roll/Pitch/Yaw + Maximum attitude rate [S13, 2026-09-02: corrected from "altitude" —
    see the divergence note above] + FC Slew) + numbered steps ("~3 min prior to
    burn Dragon performs single-pulse burns to settle propellant; 8 min duration; 1 sec pulses at 30 sec
    intervals; state oscillates between Deorbit Burn Prep and Deorbit Burn Settle"). A deorbit procedure page.
  - **Entry** (`discovery` "Entry" frame): "Parachute Deployment Altitude" section + steps — the entry/
    landing procedure page (rail item area).
  - **Physical console button panel** (`discovery9`, hardware not a screen): STRING 1A/1B/1C · STRING
    2A/2B/2C · RESET 1/2 · POWER 1/2 · ENABLE BACKUP PYROS · JETTISON NOSE CONE · MAINS ONLY · DROGUES &
    MAINS · ENABLE ENTRY REBOOT · CUT MAINS · ENABLE BACKUP ENTRY · FIRE PYRO. Confirms our command names.
  - Hi-res also CONFIRMS already-built pages: Vehicle Overview (`discovery` capsule+gauges, subsystem tab
    bar Prop·Mech·Power·Avionics·GNC·Thermal), Audio Settings (`discovery5`: Seat 2 COMMANDER / Seat 3 PILOT,
    SEAT 2 AUDIO GROUND +12dB / REG 0dB / MAIN 100 / INTERCOM +9dB / ALERTS 50 / VOX 17), HUD (`discovery16`),
    Cover/Coast-to-Trunk (`discovery13`).
- **The Vehicle page has 8 subsystem sub-tabs** (from the clean ui1.jpg mockup): **All · Crew · Prop ·
  Mech · Power · Avionics · GNC · Thermal**. Our "Overview/Mech" was wrong — Overview ≈ All. **DONE
  (2026-09-01):** the two-tab strip is replaced by the shared 8-tab bar (`VehicleTabBar`, drawn by every
  vehicle page, sliding accent underline under the active tab), and the six missing subsystem pages are
  built from one template (`VehicleSubsystemPage`, `enum Sub`) in the Vehicle-Overview grammar — LEFT
  subsystem checklist · CENTRE capsule (`dragon_crew`) + four headline gauges · RIGHT detail readouts.
  Power's readouts reuse the real photo's "+68 W" / "0 kW". `FigmaUI` routes each tab to its sibling
  page (UiPage 20-25, PageCount 26); 57 nav checks pass. Values representative; real telemetry later.
- shanemielke.com gallery = ui1 (Vehicle/All, clean render — best subsystem reference), ui2 (Video crew-cam
  promo), iss_docking.mp4 (the manual docking screen in motion), + Discovery/mission photos (same 5 screens
  as REAL_SPACEX_SCREENSHOTS). iss-sim.spacex.com = the live manual docking screen (#11).

## RESEARCH PASS 2026-09-01 (hi-res photos + iss-sim + web) — complete map so far
Owner directive: build a complete map of every publicly-visible Crew Dragon screen; research now, build later
with approval. Sources this pass: hi-res `discovery*.jpg`/`crew*`/`inspiration4*`, **iss-sim.spacex.com**
(live DOM), the YouTube **"Crew Training | ISS Docking Simulator"** (`MdJDBHzJF8E`, SpaceX, unlisted — it is
the sim's linked "actual interface", i.e. the real manual-docking screen, no new pages beyond #11),
dillonbaird.io (~25–30 pages, 6 categories), space.com/rocketstem (3 panels, ~30 hardware buttons, 5-section nav).

- **Manual Chute Deploy — ✅ BUILT 2026-09-01** (`ManualChuteDeployPage`, `UiPage.ManualChute`; reached from the
  Cover's "Manual Chute" rail item, which now navigates while the other rail items still select in-page). Reuses
  `CoverPage.DrawRail` (shared rail) + the live globe. Both sections + per-step action pills + FC-failed status
  concept drawn; actions display-only until the touch pass. FULL SPEC (rail item #7; `discovery5` = "…(Complete FC
  Failed)" variant, `discovery15` = nominal). Layout: LEFT 7-item phase rail; CENTRE a two-section command list; RIGHT the live globe/map
  (shares the Cover's globe); header "Manual Chute Deploy" + ◄/► + MANUAL + splashdown/orbit telemetry.
  - **Section 1 — High Altitude Chute Deploy** (red section icon; "(Complete FC Failed)" state adds status
    rows Flight Computer / Dracos / VRIO2 health LED / Altitude, e.g. "Verify failed pyro string…"). Steps
    are altitude-gated: "10.6 km (TBC) 6 nm drogues" → ENABLE BACKUP PYROS → DEPLOY DROGUES → "10.0 km (TBC)
    6 nm drogues" → FIRE PYRO → "2.5 km (TBC) 6 nm mains" → ENABLE BACKUP PYROS → DEPLOY MAINS → "2.2 km (TBC)
    6 nm mains" → FIRE PYRO.
  - **Section 2 — Standard Altitude Chute Deploy** (status: Altitude / VRIO health LEDs (front)): "5.5 km
    (TBC) 6 nm drogues" → ENABLE BACKUP PYROS → DEPLOY DROGUES → "1.6 km (TBC) 6 nm mains" → FIRE PYRO →
    "1.4 km (TBC) 6 nm mains" → ENABLE BACKUP PYROS → DEPLOY MAINS → FIRE PYRO.
  - **Right-side ACTION per step** (the touch target): Check latched · Arm and verify · Execute · Monitor
    altitude · Halt · Latch. Values marked "(TBC)" on-screen (SpaceX's own to-be-confirmed placeholders).
- **Vehicle systems P&ID schematic** (`crew1_3`, `crew3_1`, `demo1_3`) — ✅ BUILT 2026-09-02 (**T9**,
  `SystemsPidPage.cs`, `UiPage.SystemsPid`): a distinct deep-view — the Dragon's fluid/electrical system as
  **line-art**: rectangular loops, ring/hex components (tanks/valves/pumps), inline valve symbols, small
  **green status dots** along the lines. NOT our radial Mech donut and NOT the rendered `dragon_crew`. Built
  as the **ECLSS + coolant loops** — the inventory named the subsystem ambiguously ("likely
  Prop/Thermal/ECLSS"); ECLSS/coolant are the fluid systems this build actually models (every component
  gets a live state), and Prop's own plumbing would have duplicated the #26 schematic above.
- **Circular proximity/nav plot** (`crew2_1/2_2` right screen): concentric-ring plot with centre marker —
  the docking/attitude/relative plot (overlaps HUD #2 and the prox screen #13).
- Manual ISS Docking (#11) fully specced from the live sim DOM — see the table row above.

## IMAGERY HUNT 2026-09-01 (thin pages) — findings
Owner directive: pause building, hunt imagery for the thin pages. Sources worked: shanemielke.com gallery
(enumerated the designer's FULL public image set), dillonbaird.io article (recreation renders + authoritative
alt-text), iss-sim, web writeups. mutantdragon.space (the live recreation) was DOWN this pass.

- **The designer's public gallery has NO clean render of any thin page.** Full set = `ui1` (Vehicle/All),
  `ui2` (Video crew-cam), `demo1_1/1_2/1_3` (HUD/docking design views), `discovery2–16` + `crew*` +
  `inspiration4*` + `bob_doug1/2` + `training1` (all mission/training PHOTOS of the same ~10 screens we
  already catalogued). So Ascent, standalone Nav, Reference Content, Menu have **no real public frame** —
  confirmed, not just unfound. `bob_doug1` desktop monitors show the attitude/docking HUD in a design tool
  (nothing new).
- **Navigation / Map is the globe with view modes, not a wholly separate page.** DillonBaird's "Navigation
  Screen" render + alt-text: *"2D & 3D map views — current vehicle position, orbit path, ground stations,
  ISS position, sun position, planned splashdown zone; 2D navigated by d-pad, 3D by touch & drag,"* with a
  camera-mode label ("Auto - Earth 3D") + SETTINGS. This is exactly the Cover's right-side globe with a
  2D/3D + camera toggle — so our standalone-NAV gap is really "add 2D-map + camera modes to the Cover globe"
  (the owner already flagged NEXT VIEW → astronaut views). Downgrade NAV from "needs info" → reference.
- **Alert / Fault is NOT a standalone page — it is a FUNCTIONS / ALERTS toggle on every Vehicle subsystem
  page.** DillonBaird's Vehicle render shows FUNCTIONS|ALERTS tabs (bottom-left) + the subsystem icon bar,
  and the "Subview Nav Bar … displays red when alerts exist in that subview." So the FDIR/alert surface =
  the ALERTS tab per subsystem + red nav routing. Build later as a mode of the subsystem pages, not a page.
- **Vehicle Overview right column should be CONSUMABLES, not orbit telemetry** (DillonBaird render, plausible
  real values): CONSUMABLE / QTY / MARGIN — Power Unit 1 Energy 100%, Power Unit 2 Energy 100%, Usable
  Deorbit Fuel 791.1 kg, Usable Deorbit Oxidizer 1308 kg, Orbit 1 Subtank Fuel 67.76 kg / Oxidizer 111.3 kg,
  Orbit 2 Subtank Fuel 67.76 kg / Oxidizer 111.3 kg, + a "SHOW MARGINS TO" toggle. Refinement for our Overview.
- **Two nav templates** (alt-text): "one with bottom sub-nav, and the other with a sidebar sub-nav w/ attached
  sub-view" — matches our bottom-bar + the Cover/ManualChute rail. The **3-display split** (space.com):
  LEFT = vehicle systems, CENTRE = situational awareness (trajectory/rendezvous/attitude), RIGHT = crew
  inputs / nav planning.
- **Still genuinely dark (no public imagery, recreators left blank too):** Ascent/Launch, Reference Content,
  Menu drawer, and most deep subsystem detail. Building these would be a guess — hold until imagery appears.

### Follow-up 2026-09-01 (retry mutantdragon + ascent dig)
- **mutantdragon.space is DEAD** (DNS `ENOTFOUND`, no longer resolves) and the GitHub repo
  **`DillonBaird/MUTANTdragon-UI-Demo` is EMPTY** — the live recreation is gone for good. Only Baird's
  article-embedded renders survive (already captured: nav / vehicle / templates). Drop it as a lead.
- **Ascent / Launch screen: no public frame exists** — not in the designer's gallery (all its capsule
  photos are deorbit-phase: splashdown-time / T-01:xx), not in the dead recreation, not in web writeups
  (which only *describe* it: during ascent the CENTRE "situational awareness" display shows the ascent
  trajectory + spacecraft orientation + telemetry; a low-quality composite "montage frame" in older docs is
  the only visual). Building Ascent now would be a guess — HOLD until a launch-cabin frame surfaces.

### BBC explainer page 2026-09-01 (bbc.com/news/science-environment-52840482)
Owner pointed here. Useful assets (BBC ichef, upscalable via the `/news/<width>/` path segment, e.g. 2048):
- **`_112570366_touchscreens.png`** ("NASA Touchscreen controls") — a flight screengrab of ALL THREE cockpit
  screens during the **"Hold Capture" rendezvous** phase. LEFT = the attitude/docking HUD (reticle +
  concentric rings + corner status rings + bottom gauge — CONFIRMS our HUD/DockingSimPage design). RIGHT =
  a scrolling **event-log / checklist list** page. CENTRE = **a screen we don't have: a rendezvous page** —
  a left vertical ICON sub-nav rail (the "sidebar sub-nav" template), a "Hold Capture" procedure/checklist
  card (◄/► + RUNNING + status text + a circular mission-patch icon), and a large **2D ORBITAL-ELLIPSE plot**
  on the right (the orbit drawn as an ellipse with the vehicle position + an approach chord). This is the
  CENTRE "situational-awareness" display's rendezvous/orbital view — distinct from the Earth globe (Cover)
  and the docking HUD. 🔴 NOT built; now has real reference. Likely overlaps the "Proximity/ISS plot" gap.
- `_112425252_maxresdefault.jpg` interior photo = 4-seat cabin with the display arm stowed (no screen content).
- `_112424554_..._capsule_inf640` = the front/rear capsule cutaway the owner pasted (nose cone / pressurised
  crew section / Draco engines / trunk / solar panels). `_112424556_..._return_to_earth_inf640` = a reentry/
  recovery diagram (not a screen). Exterior views are usable as front/rear reference for a capsule render.

## RESEARCH PASS 2026-09-02 — the JSC crew-training vein (§11b of the plan)

The **JSC crew-training photo series** on images.nasa.gov (`jscYYYYeNNNNNN`, Crew-1…13) turned out to be a
rich, well-lit, high-res imagery vein not previously tapped — crew posed in the Dragon mockup with the
screens and console clearly lit, far better than the dark Discovery frames. **44 of 74 frames pulled and
contact-sheeted; the screen-facing ones re-pulled at 3240–3840 px.** Result:

- **Three new real screen LOOKS captured** — rows **#26 Prop/RCS thruster schematic**, **#27 systems tree**
  and **#28 circular nav/orbit plot** above. The left screens in the same set (`jsc2024e079789`,
  `jsc2025e064540`, `jsc2022e068644`) show the attitude/nav HUD and **corroborate our built HUD (#2)**.
  **Update 2026-09-02 (T9):** #26 and #27, plus the P&ID entry below, are now ✅ BUILT; #28 is still
  🟠 REF, not built — owned by **S15**, not T9.
- **Layouts yes, text no.** The mockup screens were shot at steep angles with glare, so exact on-screen
  text was **not transcribable** at any resolution available. The two built pages (plus the P&ID) are
  **layout-real / labels-reconstructed** — the same honest status as our other from-photo pages; #28 will
  be the same when it's built.
- **The console is still not label-legible** in ANY JSC frame — every shot is upward-at-crew.
  `jsc2024e064449` gives the **best-lit console band yet** (button-group plates, a labelled rotary, a red
  guarded control) and it is still not readable at that angle. So the §4 panel-label verification remains
  blocked on a console-down close-up that does not exist in public imagery.
- Also swept and closed out: the **SpaceX Flickr photostream** (1,486 photos) is launch/exterior work — no
  Crew Dragon interior or console shots. Dead end, do not re-work it.

## TODO / residual research

> **Note (T1, 2026-09-02):** the list below is the *research* backlog. The *build* backlog is `REGISTER.md`
> — do not start work from this list. Research was declared **COMPLETE** by the owner (§11/§13 of the plan):
> what remains is label-level, gated on non-public (ITAR/SpaceX) sources or owner-supplied imagery, and is
> carried as a caveat, not a blocker.
- Build/refine reference for the **rendezvous orbital-ellipse plot** (BBC touchscreens centre screen) — pair
  with the "Proximity/ISS plot" gap; find a cleaner frame if possible.
- Scan the Figma **"Cover" page (12221:242)** for hidden frames (MCP rate-limited).
- Watch launch webcasts (Demo-2 / Crew-1 / Inspiration4) for a clear in-cabin ASCENT-screen frame — the one real gap with zero imagery.
- Capture the **systems P&ID schematic** text (which subsystem it belongs to) from a cleaner frame if one appears.
- Enumerate the **procedure sections** (which 2.x / 4.x / other numbered steps exist) as more frames surface.
- Ascent/Launch, standalone Nav/map, Reference Content, Menu, Alert/Fault still lack a clear public frame.
