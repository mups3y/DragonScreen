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

### Supporting — community Figma recreation (opened view-only 2026-08-31)
`figma.com/design/mbEy4s9XCQHssNvUa3mJA0` "SpaceX Crew Dragon — Dashboard UI (Community)" — a **designed vector recreation** (community, **NOT** official SpaceX) → **RECONSTRUCTED** (an interpretation; cross-check against the real photos, never treat as fact — rule E2). It rendered view-only; I could read the **frame taxonomy**, but Figma's canvas ignored automated zoom, so exact fine text wasn't captured at crisp resolution. **Frames present (= a page taxonomy that corroborates the photos):**
- `Frame 67` → **NAV** (text list + 3D globe)
- `Frame 58` → **DOCKING** (concentric-circle nav/approach plot)
- `Frame 59` → **PROCEDURE / status text**
- `Frame 66` → **VEHICLE** (ISS / vehicle line-art schematic)
- `A-Settings-Seat1 / Seat2 / Seat3 / Seat4` + `A-Settings-Cabin` → **SETTINGS** (per-seat + cabin), each with a seat/vehicle schematic and a **systems big-number row** (community values, only partially legible: ~`12.5 · 0kW · 100 · +9kW · 50` — meanings unconfirmed).

**For exact Figma specs (px / colour / strings): export the frames as PNG/SVG and I'll ingest them** — the browser tools can't drive Figma's zoom.

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
