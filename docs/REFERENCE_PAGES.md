# The eight reference pages — what we already own

Rendered 2026-08-05 with `plugin/build/rasterise_all.py` into `plugin/build/refart/`. **Look at these
before designing any page.** The Figma export has no semantic layer names, so the only way to know
what is in a frame is to render it — and until this pass, four of the eight had never been looked at.

> Prompted by the user: *"dont we have assets we downloaded to create this?"* and *"you need to make
> sure you look at all them so you dont go making something we already have"*. Both were right, and
> the second caught a page I was about to invent.

---

## ⚠ THE DRAGON ILLUSTRATION EXISTS — I WAS WRONG, TWICE

An earlier version of this file said, in bold, that there is no Dragon illustration and that the real
pages do not have one. **Both claims were false.** The user corrected them with a photograph of the
real VEHICLE OVERVIEW screen, which has the capsule render dead centre.

    plugin/build/refart/embedded/container_3.png    1800 x 3010, transparent background
    the capsule + trunk, white, SPACEX branded - the exact render in the real screen photo

### How I got it wrong, so it is not repeated

**The Figma SVGs are mostly EMBEDDED RASTERS, and svglib silently drops them.** `ASSET_PROVENANCE.md`
already recorded this — the docking export is "20.9 MB of which almost all is a single embedded Mars
photograph" — and I did not join it to the blank centre in my own render of `Container.svg`. The
rasterised page came out with a hole exactly where the artwork is, and I read the hole as evidence of
absence.

**A rendering tool that fails silently produces evidence that looks like fact.** The rasterisation was
not wrong about the vectors; it was incomplete, and nothing said so. Before concluding an asset does
not exist, check whether the tool that would have shown it can render that kind of content at all.

`plugin/build/extract_embedded.py` pulls every embedded image out of the SVG set into
`build/refart/embedded/`. Run it alongside `rasterise_all.py` — the two together are the complete
picture; either alone is misleading.

### Licence position — CHECK BEFORE SHIPPING THIS ONE

The Figma Community file is CC BY 4.0, which covers the *file*. This particular layer is a
photorealistic render of a real spacecraft with SpaceX branding on it, and its ultimate provenance is
unknown — it may be SpaceX press imagery placed into the Community file by its author. **Using it as
a reference for our own drawing is unproblematic; bundling the PNG into a GPL-3.0 release is not
obviously safe.** Resolve that before it ships, not after.

---

## The pages

| render | what it is | maps to our page |
|---|---|---|
| `dashboard_ui_frame_58` | **manual flight / docking** | DOCKING |
| `dashboard_ui_frame_59` | **procedure**: "4.700 Deorbit Preparation" | a page TYPE we had not planned |
| `dashboard_ui_frame_66` | **cabin settings** — lighting | SETTINGS |
| `dashboard_ui_frame_67` | **deorbit**: telemetry strip + procedure + trajectory | FLIGHT / NAV |
| `dashboard_ui_a-settings-*` | **audio settings**, one per seat | SETTINGS |
| `flight_control_ui_container` | **VEHICLE OVERVIEW** | VEHICLE |
| `dragon_interface_docking_...` | **docking HUD** with translation/rotation pads | DOCKING |

### Frame 58 — manual flight / docking
Big pitch-roll-yaw ring, `RANGE` and `RATE` in green, X/Y/Z offsets, `ACCELERATION 0.00g` dial top
left, docking-port view top right, star field + clock bottom left, target view bottom right,
`FLIGHT COMMANDS` / `ALERT ACTIVITY` column on the right, `FRAME LVLH` and `CAMERA Virtual`
selectors, and a `RESET` / `START` timer.

### Frame 67 — deorbit
Top strip: `ACTIVE PHASE  SPLASHDOWN TIME  INERTIAL VELOCITY  ALTITUDE  APOGEE  PERIGEE  INCLINATION`.
Left: a phase sidebar (Deport & burn / Coast to Trunk / Claw Separation / Procedure / Manual Chute)
and a running step — "Coast to Trunk Jettison, RUNNING 00:22:57" — with *Crew Interrupt Conditions*
and a *Crew Deorbit Preparation* checklist. Right: the orbit plotted as an arc with markers and
`TARGET LATITUDE` / `TARGET LONGITUDE`.

### Container — VEHICLE OVERVIEW
Left: system checklist with coloured status dots (`THERMAL SHIELD / Applied`, `POWER COMPLETION /
Awaiting`). Centre: ring gauges — `PPO2`, `CABIN TEMP`, `CABIN PRESSURE`, `CO2`, `LOOP A`, `LOOP B`,
`NET PWR 1`, `NET PWR 2` — plus a `CONNECTIONS` list. Right: **horizontal bar gauges** for Inertial
Velocity, Altitude, Apogee, Perigee, Inclination, Range to ISS. Bottom: `SYSTEMS` / `CABIN` tabs and
the subsystem icon row.

---

## What this CORRECTS in what has already been built

1. **The bottom bar is not text page links.** The real one is, left to right:

       [5 icons]  |  CURRENT STATE / Far Field Pointing Deorbit  |  POINTING MODE / Sun + GEO
                  |  (o) SPX  22:33 GND / 0.00 TDRS   (o) ISS   |  79/1450122

   Our `ChromeBar` draws the five page names as words and shows `STATE / NOMINAL`, `COM1/TLM`, `MET`.
   The **structure** was right — page selector, state, comms, counter — but the page selector should
   be **icons**, and there is no MET down there. MET's slot is taken by `SPLASHDOWN TIME` in the TOP
   strip. It is identical on all four pages that have it, which confirms it is chrome.

2. **The telemetry strip is a TOP strip with seven fields**, and it uses **APOGEE / PERIGEE**, not
   APOAPSIS / PERIAPSIS — SpaceX uses the Earth terms. It also carries **SPLASHDOWN TIME** and
   **INCLINATION**, which we do not compute yet. Ours has six and is otherwise close.

3. **Gauge anatomy differs from ours.** Theirs: caption **above**, big value with a small unit
   beneath it, all inside a dark card, dial opening downward with small tick marks at the ends. Ours
   puts the caption below and has no card. Theirs is better and is what to match.

4. **Horizontal bar gauges are a distinct element we do not have** — a caption, a thin filled bar,
   and the value to its right. Used for anything with a range rather than a percentage.

5. **PROCEDURE is a page type we had not planned**, and it is where the "25-30 pages" number comes
   from. Numbered steps with tick states, inline command buttons, notes panels, `NEXT`, and a
   read-only mode. This is how the real vehicle is *flown*, so it matters for "fly the mission".

6. **The subview nav is an icon row**, not text, with the active one highlighted and alerts coloured — the
   red-when-non-nominal routing from the research.

   > ⚠ **CORRECTED 2026-09-02 (T1).** This entry read `Overview · Life · Comms · Prop · Mech · Power ·
   > Avionics · GNC · Thermal` — "nine subsystems". That set came from `Container.svg`, a community
   > reconstruction (§1.4 **tier 2**). The real tab bar was later read off the designer's own clean mockup
   > (`ui1.jpg`, tier 1) and is **eight**: **All · Crew · Prop · Mech · Power · Avionics · GNC · Thermal**.
   > That is what `VehicleTabBar` ships. Higher tier wins.

---

## How to use this

**Do not design a page that one of these already answers.** Match the reference, and where stock KSP
cannot supply a value (PPO2, CO2, cabin pressure, TDRS) either omit the element or leave it visibly
inert — never fill it with an invented number.


---

# THE LIVE DEMO — explored 2026-08-05

**https://neeldandiwala.com/SpaceX-Dragon2-UI/** — linked from line 7 of the Vue README, which I had
extracted and never read past the top. It is the *working* UI, and it shows behaviour no static SVG
can. Note: SPA routes 404 on direct load (GitHub Pages does not rewrite them) — open the root and
click the bottom icons.

## What the demo shows that the SVGs did not

1. **The right panel of the deorbit page is a LIVE, INTERACTIVE WORLD MAP** — equirectangular Earth
   with four pan arrows, a centre reset, `+` / `-` zoom, and a **`NEXT VIEW`** button that cycles to
   other camera views. That is the NAV page's content, and it is a *map with controls*, not a static
   plot. `CAMERA / Auto - Map IO` labels the current view.

2. **⚠ GAUGE COLOURS ARE PER-METRIC IDENTITY, NOT THRESHOLDS. — SETTLED 2026-08-06, user's call:
   match the real screen.** Implemented; `Gauge.LowIsBad`/`HighIsBad` are gone and `Alarms` replaced
   them.

   The colours were then MEASURED rather than eyeballed off the demo — they are the literal
   `style="stroke: #xxxxxx"` on each dial's fill circle in `Overview.vue`:

   | dial | line | hex |
   |---|---|---|
   | PPO2 | 59 | `#d7b733` mustard |
   | CABIN TEMP | 109 | `#d12c30` — **the same red as the alarm colour, deliberately** |
   | CABIN PRESSURE | 159 | `#fcd533` yellow |
   | CO2 | 209 | `#2983ed` blue |
   | LOOP A / LOOP B | 260, 310 | `#2886f6` |
   | NET PWR 1 / 2 | 360, 410 | `#2886f6` |
   | track (all dials) | 39 etc. | `#777777`, **dotted** — `stroke-dasharray="0 4"` |
   | bar track / fill | CSS | white 25% / `#35a9eb` |

   **My first note here said CABIN PRESSURE was grey. It is not — it is `#fcd533`.** That came from
   reading colours off a screenshot of the demo instead of out of its source, which was available the
   whole time. Same lesson as the Dragon illustration, in a milder form: read the source, not a
   render of it.

   The reference is coherent about the trade: the dials are identity-coloured, and **alarm is routed
   through the CHROME instead** — the left checklist dots and the subview nav going red. Alarm lives
   in one channel, not smeared across every dial. Ours now does the same via `Alarms.Mask` (page
   links) and the VEHICLE status row.

3. **The left checklist is the alarm channel.** Each row is a name with its STATE word beneath and a
   coloured dot: blue tick = Normal, **green** tick = `THERMAL SHIELD / Applied`, **orange** =
   `POWER COMPLETION / Awaiting`.

4. **The bar gauges are genuinely filled to a fraction** — they have ranges, they are not just a rule
   under a value. Ours draws a flat rule; theirs shows proportion.

5. **Subview tabs are CONTEXTUAL, not a fixed nine.** VEHICLE has `Overview / Mech`; SETTINGS has
   `Audio / Cabin / Video`. The nine-icon row in `Container.svg` is the fuller design, but the shipped
   demo varies the set per page.

6. **`SYSTEMS` / `CABIN` is a top-level toggle within VEHICLE**, with `MORE` at the right.

7. **Page content floats on a rounded dark card over a blue gradient** — not edge to edge. Our pages
   fill the whole render target.

8. The Dragon render in the demo is a **different asset** from `container_3.png` — US flag, more
   markings, and `CABIN MICS: RECORDING` in red beneath it.

## How to use it

It is a live, clickable specification for interaction — hover states, what is a button, what a
control actually does. **Open it before designing any interactive element**, the same way the
rendered SVGs are opened before designing a static one.
