# DragonScreen — SCREEN INVENTORY & RESEARCH BASE (2026-09-01)

The consolidated catalogue of EVERY real Crew Dragon touchscreen page: what it is, what evidence we
have, and whether we've built it. Feeds the Figma-rebuild loop. Extends `SCREEN_EVIDENCE_MATRIX.md` /
`SCREENS_LOOK_AND_FUNCTION_RESEARCH.md` with the new real-photo + web evidence gathered this session.

## Headline fact
The real Dragon UI has **~25–30 individual pages** (DillonBaird recreation, from a SpaceX crew-displays
source; astronaut Doug Hurley quote). We now have **~18 built** (Cover, HUD, Vehicle Overview/All + Mech + 6 subsystem tabs, Suit Leak Check,
VRIO 4.700, Audio, Cabin, Video, **Manual Chute Deploy**, **Manual ISS Docking**), **strong real reference**
for the deorbit procedure pages **Deorbit Burn Prep** and **Entry** (🔴 not built) and the systems P&ID
schematic look, and **thin/no reference** for Ascent, standalone Nav/map, Reference Content, Menu, Alert/Fault.

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
Status: ✅ built · 🟡 reference only · 🔴 no reference (evidence of existence only).

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
| 12 | **Navigation (2D/3D map)** | Dedicated map page: vehicle pos, orbit track, ground stations, ISS, sun, predicted splashdown; 2D & 3D | DillonBaird + docs | 🔴 (our globe lives on the Cover; a standalone NAV page may be separate) |
| 13 | **Proximity / Vehicle (ISS line-drawing)** | ISS truss+arrays line art, target marker, circular gauges, right data columns, scale bar | photos (docs matrix) | 🔴 |
| 14 | **Ascent / Launch** | Falcon 9 vertical schematic + ascent telemetry list | montage frame (docs) | 🔴 |
| 15–22 | **Vehicle subsystem pages** — the Vehicle page's real sub-tab bar is **All · Crew · Prop · Mech · Power · Avionics · GNC · Thermal** (8 tabs, CONFIRMED from ui1.jpg mockup) | Each a subsystem overview + alerts | shanemielke ui1.jpg (clean mockup) + DillonBaird | ✅ ALL 8 BUILT (2026-09-01) — `VehicleTabBar` (shared 8-tab strip) + `VehicleSubsystemPage` (Crew/Prop/Power/Avionics/GNC/Thermal); All=`VehicleOverviewPage`, Mech=`VehicleMechPage`. Rep. data; real telemetry later |
| … | **More procedure pages** | The phase rail's real items — **Deport & Burn, Coast to Trunk Jettison, Claw Separation Prep, Procedure, (2nd) Procedure, Reference Content, Manual Chute Deploy** — each with numbered command steps (like 4.011, 4.700) | REAL PHOTOS (expanded rail) | 🔴 many |
| … | **Reference Content** | Documents / reference viewer (a phase-rail item) | photos | 🔴 |
| … | **Menu** | Top-left hamburger — page/menu drawer | photos | 🔴 (placeholder) |
| … | **Alert / Fault** | Subsystem fault detail (nav red-alert target) | DillonBaird | 🔴 |

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
    Deorbit Burn" (Roll/Pitch/Yaw + Maximum altitude rate + FC Slew) + numbered steps ("~3 min prior to
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
- **Vehicle systems P&ID schematic** (`crew1_3`, `crew3_1`, `demo1_3`): a distinct deep-view — the Dragon's
  fluid/electrical system as **line-art**: rectangular loops, ring/hex components (tanks/valves/pumps),
  inline valve symbols, small **green status dots** along the lines. NOT our radial Mech donut and NOT the
  rendered `dragon_crew`. Likely a subsystem detail (Prop/Thermal/ECLSS) or dedicated schematic. A build
  refinement for the subsystem pages (add a schematic view alongside the gauge grammar).
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

## TODO / residual research
- Build/refine reference for the **rendezvous orbital-ellipse plot** (BBC touchscreens centre screen) — pair
  with the "Proximity/ISS plot" gap; find a cleaner frame if possible.
- Scan the Figma **"Cover" page (12221:242)** for hidden frames (MCP rate-limited).
- Watch launch webcasts (Demo-2 / Crew-1 / Inspiration4) for a clear in-cabin ASCENT-screen frame — the one real gap with zero imagery.
- Capture the **systems P&ID schematic** text (which subsystem it belongs to) from a cleaner frame if one appears.
- Enumerate the **procedure sections** (which 2.x / 4.x / other numbered steps exist) as more frames surface.
- Ascent/Launch, standalone Nav/map, Reference Content, Menu, Alert/Fault still lack a clear public frame.
