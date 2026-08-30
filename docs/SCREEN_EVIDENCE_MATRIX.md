# SCREEN EVIDENCE MATRIX (ACTIVE)

> Every screen feature carries an evidence class + source confidence, so reconstruction is never presented as confirmed SpaceX fact (rules E1/E2). Governed by `MASTER_BUILD_SPEC.md`. Where public evidence does not exist (some Dragon internals are confidential), the honest label is RECONSTRUCTED or SIMULATION with the confidence stated — never a false claim of fidelity.

**Classes:** CONFIRMED (direct primary evidence) · STRONGLY SUPPORTED (multiple credible sources) · RECONSTRUCTED (reasonable, public evidence incomplete) · SIMULATION (DragonScreen/KSP feature, not a SpaceX claim).
**Source confidence:** Very High · High · Medium · Low · Intentional (deliberate simulation).

**Primary references (RESEARCH, not instruction):** Shane Mielke — Crew Dragon Displays; Mielke — ISS Docking Simulator; iss-sim.spacex.com; Behance (SpaceX ISS Docking Simulator); mutantdragon reconstruction; repo `UI_AUDIT.md`, `REAL_DRAGON_SCREENS.md`, `REFERENCE_PAGES.md`, `PALETTE.md`.

## Docking page (highest-confidence screen — the reference implementation)
**Reference studied directly 2026-08-31** at `iss-sim.spacex.com` (DOM/accessibility tree read from source, per rule E5 — not from screenshots). The simulator states it "will familiarize you with the controls used by NASA astronauts to manually pilot" Dragon, and links the *actual* interface video (youtube MdJDBHzJF8E) — a further primary source. iss-sim is a **training** representation, so success thresholds are simulator guidance, not certified operational limits.

| Feature (as observed in the live simulator) | Class | Source | Confidence |
|---|---|---|---|
| **Translation** cluster on the **LEFT**: Up / Down / Left / Right / Forward / Backward | CONFIRMED | iss-sim (studied) | Very High |
| **Rotation** cluster on the **RIGHT**: Roll / Pitch / Yaw | CONFIRMED | iss-sim (studied) | Very High |
| A **precision toggle at the centre of EACH cluster** (LARGE ↔ SMALL; default small/precise) | CONFIRMED | iss-sim (studied) | Very High |
| Centre: **HUD rings** + a **green diamond target** overlaid on the docking adapter; must be centred in the interface | CONFIRMED | iss-sim (studied) | Very High |
| Rotation readouts **ROLL / PITCH / YAW** (grouped "PYR"), each a value in degrees | CONFIRMED | iss-sim (studied) | Very High |
| **RANGE** readout (distance to ISS) and **RATE** readout (closing rate toward ISS, at the **bottom**) | CONFIRMED | iss-sim (studied) | Very High |
| **GREEN numbers = corrections** needed to reach the target (position + rotation) | CONFIRMED | iss-sim (studied) | Very High |
| **BLUE numbers = current rates** (speed you are translating / rotating) | CONFIRMED | iss-sim (studied) | Very High |
| Top controls: **Instructions · Reset Positions · Settings** | CONFIRMED | iss-sim (studied) | Very High |
| Dark starfield background; cyan/blue HUD accent; green target/corrections | CONFIRMED | iss-sim (studied) | High |
| Success = all green corrections **< 0.2** AND **RATE below −0.2 m/s** (keep RATE below −0.2 when range < 5 m) | STRONGLY SUPPORTED (simulator-specific) | iss-sim (studied) | Medium — label "training guidance," not a certified operational limit (plan §12) |
| **AUTO/MANUAL** indicator (our addition — real Dragon docks autonomously; iss-sim is manual-only) | RECONSTRUCTED / SIMULATION | our C6 requirement | High (as our requirement) |
| Capture-envelope status (IDSS soft-capture) | RECONSTRUCTED | IDSS spec + our `DockCapture` | Medium |
| Target waypoint (WP0/WP1/WP2) readout (our autopilot's leg) | SIMULATION | our `DockingControl.NextGateId` | Intentional |

**Key design takeaway (drives the rebuild):** each controlled axis has **two** numbers — a GREEN *correction* (drive to 0) and a BLUE *rate* (current speed). Our current `DockingPage` conflates these; the rebuild must show both. Left = translation, right = rotation, centre = green-diamond target in the rings, RANGE top-ish, RATE at the bottom, a precision toggle centred in each cluster.

### Real Crew Dragon interface — SpaceX "Crew Training | ISS Docking Simulator" (youtube MdJDBHzJF8E), studied 2026-08-31
The simulator's "actual interface" link. A short official clip of a real Dragon approach HUD (crew comms "dragon SpaceX we are go for the approach"). One representative frame (RANGE 72.30 m, RATE −0.30 m/s) documents the whole layout (it is a single continuous approach; only the numbers change).

| Feature (real HUD) | Class | Confidence | Note |
|---|---|---|---|
| ISS docking target inside **thin HUD rings** with a centre crosshair/reticle | CONFIRMED | Very High | matches iss-sim centre |
| **Rotation corrections** stacked to the LEFT of centre — 3 angle values (e.g. 3.1° / 2.3° / 0.5°) | CONFIRMED | Very High | roll/pitch/yaw errors |
| **Alignment** values to the RIGHT of centre — angle + angle-rate (e.g. 0.1° / −0.4°/m) | CONFIRMED | High | lateral alignment + its rate |
| **RANGE** bottom-left (72.30 m) with a **paired secondary** value (70.90 m) | CONFIRMED (paired value's meaning unclear) | High / meaning `UNKNOWN — EVIDENCE REQUIRED` | possibly filtered vs raw, or dual sensor — do not guess |
| **RANGE RATE** bottom-right (−0.30 m/s) with a paired secondary | CONFIRMED | High | closing rate, negative = approaching |
| Ultra-minimalist aesthetic — thin white rings, white/green numerals, black background, no chrome | CONFIRMED | Very High | validates spec §8 "restrained, not a sci-fi HUD" |

**Reconciliation (real vs training sim), drives our AUTO/MANUAL page:** the real primary interface is this **monitoring HUD** (target + corrections + RANGE + RANGE RATE), because Dragon docks **autonomously**; the iss-sim's large LEFT/RIGHT **button clusters** are the **manual-piloting/backup** layer. Our page should therefore unify both: **AUTO** shows the clean monitoring HUD; **MANUAL** reveals the translation/rotation control clusters over it (rule C6). This is a SIMULATION/RECONSTRUCTED unification of two CONFIRMED SpaceX layouts — label it as such, don't claim it's a verbatim SpaceX screen.

## Real Crew Dragon screens — reference-image study (user-provided images + community Figma, 2026-08-31)
**The highest-value evidence yet for the NON-docking screens.** Real Crew Dragon cockpit photographs and Demo-2 flight-footage frames, plus a community Figma recreation. What is *visible* in the photos is CONFIRMED (real hardware); text too small to read is `UNKNOWN — EVIDENCE REQUIRED` (never guess the strings).

### The three-display console — CONFIRMED (Very High)
- Three wide landscape touchscreens in one horizontal bezel strip, gently angled toward the two reclined seats, mounted **above** the round pressure hatch/dome.
- **Default page roles (from the pristine console photo):** LEFT and RIGHT show the **same / mirrored proximity-&-vehicle display** (the ISS target graphic); the **CENTRE** shows the **3D Earth globe / map**. Commander + pilot each get the primary vehicle display, sharing the map between them. Any page can move to any screen (matches our "one screen, N surfaces"). *Design note: our cfg default (LEFT=VEHICLE, CENTRE=FLIGHT, RIGHT=NAV) should be reconsidered toward LEFT/RIGHT = proximity, CENTRE = map.*
- Below the screens: a **physical control panel**; then the hatch. Two overhead light strips; panel/hatch downlights.

### Global chrome visible across pages — CONFIRMED
- **Top status/header bar** on each screen — a horizontal row of small readouts across the top (mode/time/state; exact labels not all legible).
- **Menu tabs down the far-RIGHT edge** — a vertical stack of ~4–6 small rounded-rectangle buttons (page/section nav; one highlighted = active).
- **Vertical scale bar on the far-LEFT edge** of the proximity page (a level/scale indicator).
- **Circular gauge dials** (thin blue rings) grouped top-left on several pages.
- Flat, thin-line, no bevels/gloss (validates spec §8 "restrained").

### Page types observed (each is a real page to build to)
| Page | What is visible | Class / Confidence |
|---|---|---|
| **Proximity / vehicle (ISS)** — LEFT/RIGHT default | Detailed white/cyan **line drawing of the ISS** (truss + solar arrays); a small cyan/green **target marker**; top-left circular gauges; right-hand **data columns** (label+value rows, headers like "102 A…"/"H0023…" — not fully legible); far-right **menu tabs**; far-left **scale bar** | CONFIRMED / Very High (layout); the values UNKNOWN |
| **3D globe / map** — CENTRE default | Full-bleed rendered **Earth** (green/tan land, blue sea, curved horizon), orbital-track context, minimal overlay | CONFIRMED / Very High |
| **Docking / nav plot** | **Concentric circles** (attitude / orbital / relative plot) with a central marker + radial ticks and a horizontal readout bar | CONFIRMED / High |
| **Vehicle / systems schematic** | Blue **line-art of the Dragon capsule** (side + front) surrounded by **large numeric readouts** (e.g. `0 kW`, `100`, a signed `+68`, `50`, a data figure) — a power/thermal/systems status page | CONFIRMED / High (values' meaning UNKNOWN) |
| **Procedure / checklist / status text** | CENTRE screen with **rows of text**, sometimes a **highlighted/selected line** or an alert/message block | CONFIRMED / High |
| **Ascent / launch** | A **Falcon 9 vertical schematic** + a telemetry list (from the montage frame) | CONFIRMED / Medium (one small frame) |
| **Camera feed** (likely docking cam) | A screen showing a camera view during a ground test | STRONGLY SUPPORTED / Medium |

### Physical control panel — CONFIRMED (High)
Horizontal black panel under the screens: **grouped push-buttons** (rows, symmetric left + right sections); a **central numeric readout `000000` with a `SEQ` label** (a sequence/counter, flanked by buttons); **two rotary knobs**; small **indicator lights**. Corroborates our `PanelButtons`/`PanelMap` model.

### Visual language — CONFIRMED (reinforces `DragonPalette`)
Background near-black deep navy · line-art/graphics **cyan/blue** · text **white/light-grey** · **green** = globe terrain + target marker + go states · occasional amber. Very high contrast, thin flat lines. Type: clean compact sans (consistent with D-DIN), small labels + larger values, wide letter-spacing on headers.

### Community Figma reconstruction — FULL PAGE DETAIL (exported PNG + SVG, studied 2026-08-31)
The community `figma.com/…/mbEy4s9XCQHssNvUa3mJA0` recreation, exported by the user as PNG + SVG frames and decomposed layers. **RECONSTRUCTED** — a designer's well-researched interpretation, **NOT** official SpaceX; cross-checked against the confirmed photos/iss-sim where possible, **never** treated as verbatim fact (rule E2). It is richly detailed and mission-realistic, so it is the best available **layout/label template** for our SIMULATION pages (which we drive from real KSP/RO state). Frames viewed at full resolution; decomposed layer *filenames* give the exact strings.

**Consistent chrome (all pages):**
- **Bottom status bar (every page):** left icon row (~5: nav/target/dragon/folder/abort); centre "CURRENT STATE — Far Field Pointing Deorbit"; "POINTING MODE — Sun + GEO"; right comm/link block "SPX ↕ GND · 22:33 · 0.00 TDRS · ISS ↕ · 79/1450122".
- **NAV page top strip:** ACTIVE PHASE · SPLASHDOWN TIME · INERTIAL VELOCITY · ALTITUDE · APOGEE · PERIGEE · INCLINATION.
- Deep navy bg · white lines/text · **green** correction/target values · cyan accents · realistic Earth globe (white land / dark sea).

**DOCKING / proximity-ops (Frame 58):** centre **attitude sphere** (navball-style rings) with cardinal labels ROLL (top) / YAW (bottom) / PITCH (right, vertical), each a **green angle + rate °/s** (ROLL 15.0°, YAW −10.0°, PITCH −20.0°; rates 0.0 °/s); centre **crosshair reticle** + a cyan **target marker**; faint degree ticks. Left bracket **X/Y/Z** 200.0 / 12.0 / 30.0 m; **RANGE** 202.6 m (green) + **RATE** −0.031 m/s inside the ring. Top-left **ACCELERATION 0.00g** ring gauge; top-centre "Local Pitch Mode"; top-right a **docking-port graphic** (rings + dot cluster). Right: "FLIGHT COMMANDS", "⊞ FAR FIELD POSITIONING", "ALERT ACTIVITY". Bottom-left mini **3D globe**; bottom-centre **FRAME: LVLH** + **CAMERA: Virtual**; bottom-right a **Dragon docking-mechanism graphic** + timer "0s" with **RESET / START**. Corner tick scales.

**NAV / mission (Frame 67):** top orbital-element strip (e.g. 7.69 km/s · 393.3 km · apo 416.2 · peri 379.4 · inc 51.62° · splashdown T−01:24:51). Left **phase sidebar** (⊙ each): Deport & burn · Coast to Trunk… (active) · Claw Separation · Procedure · Manual Chute… Centre **procedure panel**: ◀ ▶ + step name + "RUNNING ↻ 00:22:57"; "⊙ Crew Interrupt Conditions" dotted-leader rows (condition → action, e.g. "30° sustained altitude error … FAR FIELD POINTING"); "⊙ Crew Deorbit Preparation" rows (timeline → action, e.g. "NLT Deorbit Burn − 1 hr … Deorbit Burn Brief"); numbered steps with a "ENTRY ENABLED True/False" toggle. Right: **3D Earth globe** with orbital **ground track** (blue orbit + cyan current + yellow segment) + waypoint pins; **TARGET LATITUDE/LONGITUDE 26°15.00′ N**; **CAMERA: Auto − Earth IO**; a **SETTINGS** button.

**SETTINGS (Frame 66 "CABIN SETTINGS" + Audio/Video tabs):** bottom sub-tabs **Audio | Cabin | Video**. Cabin tab: a rendered **interior image** (4 seats + cupola + console) + a **LIGHTING** panel (columns CABIN / CABIN DISPLAYS ×3, each with DISPLAY 1/2/3 toggles, "Tap to disable display"). Audio tab (per-seat, layer set "SEAT 4 AUDIO"): channels **MAIN · INTERCOM · VOX · AUX1 · GROUND · ALERTS** each with a level slider + **+/−** and dB values (+9/0/12 dB, and 100/50/17) + a signal icon. *(Our mod SETTINGS already has CABIN/AUDIO/VIDEO/DISPLAY tabs — near-identical.)*

**PROCEDURE / checklist (Frame 59 + the "Test VRIO Health LEDs" layer set):** section header ("4.700 − Deorbit Preparation", "section 4: in progress"); numbered steps with sub-steps + **Command:** actions ("1 Thermal pre-chill · 2 Begin Fluid loading · 3 Store items · 4 test vrio health leds (4.1–4.5 Command…) · 5 complete fluid loading"), incl. "4.3 Verify functionality of VRIO health LEDs (left side of command panel)"; **Notes** ("Each VRIO LED is zero fault tolerant…"); controls **start/stop vrio led test · enter read-only · next · Acknowledge**.

**How this maps onto the mod (drives Phase 7+):**
- Frame 58 → our Docking page's **AUTO monitoring HUD** (attitude sphere + green corrections + RANGE/RATE + LVLH/CAMERA); add the iss-sim button clusters for **MANUAL**.
- Frame 67 → our NAV/FLIGHT/procedure: top strip → chrome/orbital; left phase sidebar → `CrewProcedureOps` FSM phases; procedure panel → step list/`GateCard`; globe → `NavBallRenderer`/NavPage; target lat/long → deorbit target.
- Frame 66 → our SETTINGS (already close): add per-seat AUDIO channels + cabin/display lighting toggles.
- Frame 59 → our procedure engine (spec §43/44): numbered steps, interrupt conditions, notes, acknowledge.
- **⚠ Every exact string/value is the community reconstruction** — use as layout/label inspiration; the mod's numbers come from real KSP/RO state; anything unconfirmed stays labelled SIMULATION.

**Reference on disk (licensed, PRE-EXISTING — use this, not a re-download):** the authoritative source is **`assets/figma/dashboard_ui/*.svg`** — the 9 frames as clean vector SVG (Frame 58/59/66/67 + A-Settings-*), plus **`assets/figma/dragon_interface_docking/Space X Interface.svg`** (the DOCKING translation/rotation control pads) and `assets/figma/flight_control_ui/Container.svg`. **Licence CC BY 4.0** (Figma Community) — attribution owed in the release notes (`ASSET_PROVENANCE.md` §1, exported + documented 2026-08-04). Build Phase 7 from these SVGs (rule E5 — real geometry, not screenshots); `Frame 58.svg` is the richest instrument page (493 paths, zero rasters). *(A PNG re-download gathered this session duplicated these and was removed; always check `ASSET_PROVENANCE.md` before re-gathering.)*

### ⚠ Mirroring caveat (user note, 2026-08-31)
Some circulated reference images are **horizontally flipped** (people mirror images to evade reuse detection). Treat LEFT/RIGHT orientation as authoritative **only** from the pristine console photo and from iss-sim's explicit "translation LEFT / rotation RIGHT". Verify orientation before trusting any single image; do not lock a left/right design decision on a possibly-mirrored copy.

### Still `UNKNOWN — EVIDENCE REQUIRED` (do not invent)
Exact text/labels/values in the data columns + headers · the menu-tab labels + full page list · exact colours (need pixel sampling) + pixel positions (Figma or higher-res shots would give these) · all interface/button/alert **sounds** (un-capturable via these tools; Phase 19).

---

## Global chrome / other pages (seed)
| Feature | Class | Source | Confidence |
|---|---|---|---|
| Persistent top status/header bar | **CONFIRMED** | real cockpit photos (see above) | Very High (labels not all legible) |
| Visual language (dark navy bg, cyan primary, white text, green states) | **CONFIRMED** | real photos + `PALETTE.md` | Very High |
| D-DIN-like compact sans, small-label/large-value | STRONGLY SUPPORTED | real photos + measured | High |
| Three-display roles: LEFT/RIGHT = proximity/vehicle, CENTRE = 3D globe | **CONFIRMED** | pristine console photo | High (any page movable to any screen) |
| Systems page (Dragon schematic + big-number readouts) | **CONFIRMED (layout)** / SIMULATION (our values) | real photos + Figma frames | High for layout; values are our KSP/TAC-LS sim |
| Navigation: 3D Earth globe + orbital track | **CONFIRMED** | real photos (CENTRE screen) + Figma Frame 67 | Very High |
| Far-right menu tabs + far-left scale bar (proximity page) | **CONFIRMED** | real photos | High |
| TAC-LS / ECLSS readouts | SIMULATION | DragonScreen + KSP TAC-LS | Intentional (High as simulation) |
| Thermal / Power values | SIMULATION (real KSP/RO state) | KSP/RO | High as simulation |
| Comedic abort screen / Easter eggs | SIMULATION | DragonScreen | Intentional |

> Rule: any feature not backed by evidence above is `UNKNOWN — EVIDENCE REQUIRED` until researched; do not fill it with invention (rule E3). Update this matrix as each page is designed (before it is coded, rule S4).

---

## Reference sources studied + research backlog (2026-08-31)
| Source | What it confirms | Yield |
|---|---|---|
| **iss-sim.spacex.com** (studied from DOM) | The manual DOCKING layout in full — the strongest public evidence. | High (docking → CONFIRMED) |
| **SpaceX "Crew Training" video** (MdJDBHzJF8E) | The real docking **monitoring HUD** (target rings, corrections, RANGE, RANGE RATE, minimalist aesthetic). | High (docking HUD → CONFIRMED) |
| **NASA "Tour from Space" — Behnken/Hurley** (5.6M views) | The **physical** setup only: three touchscreens on the console + a physical control panel below, operated from the reclined seats. Mostly astronaut-to-camera; UI pages are glimpsed in the periphery, not readable. | Low for UI pages; confirms physical layout |
| CNET "How a touchscreen controls SpaceX" · SpaceX "Crew Dragon Interior" | Not yet studied — likely re-show the iss-sim/docking demo. | Unstudied (low expected marginal yield beyond docking) |

**Honest limits (rule E3 — do not paper over):**
1. **Non-docking UI pages** (nav/systems/power/thermal/comms/ECLSS) are **not publicly documented in readable form.** Public footage centres on the docking HUD + general cabin. Therefore those pages stay **RECONSTRUCTED / SIMULATION** and must be labelled as such — we do **not** invent a "real SpaceX systems page" from imagination (rule E2/E3). This is expected and acceptable per the plan.
2. **Interface / button / alert SOUNDS cannot be captured or analysed via the browser tools** (they return visual frames + DOM text, not audio). Sound design is a separate evidence task — flagged for a dedicated audio pass (the user, or tooling that can process audio). Until then, screen/button/alert sounds are `UNKNOWN — EVIDENCE REQUIRED` and belong to Phase 19 (audio), after fidelity.

**Net:** the DOCKING page (the Phase-7 reference implementation) has **strong, source-based evidence**. Everything else is honestly reconstructed/simulated and labelled accordingly.
