# QC FINDINGS — the rendered screens, page by page

> **QC OFFICER deliverable — standing role, opened 2026-09-05.** This document inspects the **RENDERED
> PIXELS**, one page at a time, and records every defect with its evidence and a researched fix plan.
> **Nothing here is fixed by this document.** The QC role reads, renders and plans; the overseer assesses
> each finding and turns approved ones into build prompts. Subordinate to `docs/BUILD_PLAN.md` (C7.1) — on
> any conflict, **the plan wins**.
>
> **Why this is not a repeat of S49.** `docs/SCREEN_LIVENESS_AUDIT.md` (S49) audited liveness from SOURCE
> and says so in its own §7: *"No glass. Every finding is from source."* It never looked at a rendered
> pixel. And S75 (`5a003ce`) found that `plugin/preview/PreviewMain.cs` **ignored asset tint entirely**, so
> every state-tinted asset rendered white in every preview taken before 2026-09-04 — every prior visual
> inspection in this project was partially blind. **S49 owns SOURCE liveness; this document owns RENDERED
> reality.** Each page section reads S49's entry first and then confirms, contradicts or extends it.

## How to read a finding

**Severity uses the register's own five-tier triage** (`REGISTER.md:2783-2788`), which is a *kind*, not just
a magnitude. Stated explicitly so the overseer can sort without guessing:

| tier | meaning here |
|---|---|
| **TIER 1** | **correctness bug** — the screen states something untrue, a control misfires, or code is wrong in a way that will show |
| **TIER 2** | **hygiene / visible quality** — the right information, rendered wrongly (distortion, overrun, illegibility) |
| **TIER 3** | **owner decision pending** — a §1.4 source question, a reference deviation, or a gate (`install` / glass) blocks the fix |
| **TIER 4** | a deliberately-scheduled build, not a defect |
| **TIER 5** | held / owner-action / Part-B-bound |

Every finding carries: a short title · a tier · the **evidence** (which preview PNG, which `file:line`) ·
what is wrong · and a **fix plan** (what to change, why that is right, what it must not break, how to
verify). A finding without a fix plan is half a finding. Where the fix needs an owner call it says so and
gives options with a recommendation — it never assumes the answer (C1.14).

**Render provenance.** Every PNG cited was produced by `python plugin/build.py preview` on **2026-09-05**,
i.e. after S75's tint fix. Anything rendered before 2026-09-04 has the tint bug baked in and must not be
used as evidence. `python plugin/build.py test` was green at the same commit (**ALL SUITES PASSED**), so
nothing below is a broken build.

⚠ **READ H-01 BEFORE ACTING ON ANY LEGIBILITY FINDING IN THIS FILE.** The preview renders every Figma-era
page at **2560×1406** while the shipped `DragonScreen.cfg` sets `screenWidth = 1280` on all three screens.
Every "is this legible?" judgement taken from a preview PNG — including the ones in this document — is
therefore optimistic by a factor of two in each axis. H-01 has the measurements and the open question.

**Open questions raised so far** (full text at the end of each page's section):
**Q1** stray arrow placement (C-02) · **Q2** globe/map handedness, glass-gated (C-09) · **Q3** ENTRY ENABLED
class (C-08) · **Q4** where the CAMERA caption goes (C-13) · **Q5** which screen resolution is authoritative
(H-01) — *Q5 is the one that changes other findings' severity; answer it first.*

---

## PAGE INVENTORY — the worklist

The 35 `UiPage` values (`plugin/src/pure/FigmaUI.cs:21-80`, names from `Titles` at `:135-143`), plus the
Cover's seven phase views, plus the lower analog console panel. **The six Vehicle subsystem sub-tabs are
`UiPage` 20–25** and are listed in place rather than duplicated.

| # | UiPage | title | status | date |
|---|---|---|---|---|
| **0** | **Cover** | COVER | ✅ **DONE — 13 findings** *(C-12, C-13 added on owner review)* | 2026-09-05 |
| **1** | **Hud** | ATTITUDE HUD (Frame 58) | ✅ **DONE — 9 findings** | 2026-09-05 |
| 2 | Audio | AUDIO SETTINGS | NOT STARTED | — |
| 3 | Procedure | PROCEDURE (Frame 59) | NOT STARTED | — |
| 4 | Cabin | CABIN (Frame 66) | NOT STARTED | — |
| 5 | Menu | MENU | NOT STARTED | — |
| 6 | PhaseDeport | DEORBIT BURN | NOT STARTED — *unreachable enum value (S49 H9)* | — |
| 7 | PhaseCoast | COAST TO TRUNK JETTISON | NOT STARTED — *unreachable (S49 H9)* | — |
| 8 | PhaseClaw | CLAW SEPARATION | NOT STARTED — *unreachable (S49 H9)* | — |
| 9 | PhaseManual | MANUAL CHUTE | NOT STARTED — *unreachable (S49 H9)* | — |
| 10 | ActOnSpaceX | ON SPACEX — GO | NOT STARTED — *unreachable (S49 H9)* | — |
| 11 | ActDeorbitBrief | DEORBIT BURN BRIEF | NOT STARTED — *unreachable (S49 H9)* | — |
| 12 | ActReview | REVIEW REFERENCE | NOT STARTED — *unreachable (S49 H9)* | — |
| 13 | ActAcknowledge | ACKNOWLEDGE | NOT STARTED — *unreachable (S49 H9)* | — |
| 14 | Entry | ENTRY GO / NO-GO | NOT STARTED — *unreachable (S49 H9)* | — |
| 15 | Vehicle | VEHICLE OVERVIEW *(tab: All)* | NOT STARTED | — |
| 16 | SuitCheck | SUIT LEAK CHECK | NOT STARTED | — |
| 17 | VehicleMech | MECH PANEL *(tab: Mech)* | NOT STARTED | — |
| 18 | AudioVideo | VIDEO SETTINGS | NOT STARTED | — |
| 19 | VrioTest | TEST VRIO HEALTH LEDS | NOT STARTED | — |
| 20 | VehicleCrew | VEHICLE — CREW *(sub-tab)* | NOT STARTED | — |
| 21 | VehiclePropulsion | VEHICLE — PROP *(sub-tab)* | NOT STARTED | — |
| 22 | VehiclePower | VEHICLE — POWER *(sub-tab)* | NOT STARTED | — |
| 23 | VehicleAvionics | VEHICLE — AVIONICS *(sub-tab)* | NOT STARTED | — |
| 24 | VehicleGnc | VEHICLE — GNC *(sub-tab)* | NOT STARTED | — |
| 25 | VehicleThermal | VEHICLE — THERMAL *(sub-tab)* | NOT STARTED | — |
| 26 | ManualChute | MANUAL CHUTE DEPLOY | NOT STARTED | — |
| 27 | Docking | MANUAL DOCKING | NOT STARTED | — |
| 28 | Rendezvous | RENDEZVOUS | NOT STARTED | — |
| 29 | DeorbitBurnPrep | DEORBIT BURN PREP | NOT STARTED | — |
| 30 | EntryProcedure | ENTRY | NOT STARTED | — |
| 31 | SystemsTree | SYSTEMS TREE | NOT STARTED | — |
| 32 | SystemsPid | SYSTEMS P&ID | NOT STARTED | — |
| 33 | Ascent | ASCENT / LAUNCH | NOT STARTED | — |
| 34 | NavOrbitPlot | NAV / ORBIT PLOT | NOT STARTED | — |

### The Cover's seven phase views (`CoverPage.PhaseName`, `CoverPage.cs:87-89`)

| slot | phase | status | date |
|---|---|---|---|
| 0 | Deport & Burn | ⏳ **PART — body not separately rendered by the preview** | 2026-09-05 |
| 1 | Coast to Trunk Jettison | ✅ DONE *(`ui_cover.png`, the default)* | 2026-09-05 |
| 2 | Claw Separation Prep | ⏳ **PART — body not separately rendered** | 2026-09-05 |
| 3 | Procedure | ⏳ **PART — body not separately rendered** | 2026-09-05 |
| 4 | Procedure *(duplicate name)* | ⏳ **PART — body not separately rendered** | 2026-09-05 |
| 5 | Reference Content | ✅ DONE *(`ui_cover_phase5.png`)* | 2026-09-05 |
| 6 | Manual Chute Deploy | ✅ DONE *(`ui_cover_phase6.png`)* — see **C-07** | 2026-09-05 |

⚠ Slots 0, 2, 3 and 4 have **no preview render of their own** and S49 §2 records that all four draw the
Coast body byte-identically. They are marked PART rather than DONE: their *rendered* state is inferred from
`CoverPage.Build`'s single `refPhase` gate (`CoverPage.cs:346`, `:376`), not seen. A preview render per
slot is the cheapest way to close them — see **C-07**'s fix plan, which adds exactly that.

### The Cover's three camera views (`CoverPage.CoverCam`)

| view | status | render |
|---|---|---|
| Earth (live globe) | ✅ DONE | `ui_cover.png`, `ui_cover_phase5.png`, `ui_cover_phase6.png` |
| Map (flat, pannable) | ✅ DONE | `ui_cover_cam_map.png`, `ui_cover_cam_map_zoom.png` |
| Capsule (36-frame turntable) | ✅ DONE | `ui_cover_cam_capsule.png`, `ui_cover_turntable_*.png` |

### The lower analog console panel (38 buttons + the EJECT handle)

| surface | status | render | date |
|---|---|---|---|
| Console plate — rest / armed / fired / inert-swap | NOT STARTED | `panel_rest.png`, `panel_armed.png`, `panel_fired.png`, `panel_inert_swap.png` | — |

---

# PAGE 0 — COVER

**Renders inspected (all 2026-09-05):** `ui_cover.png` · `ui_cover_phase5.png` · `ui_cover_phase6.png` ·
`ui_cover_cam_map.png` · `ui_cover_cam_capsule.png`. All at 2560×1406, which is the shipped RenderTexture
width (`CoverPage.cs` renders at 2× the 1280 preview; the cfg's `screenWidth` is 2560).

**Source under inspection:** `plugin/src/pure/CoverPage.cs` (834 lines) · `plugin/src/pure/FigmaUI.cs`
(routing) · `plugin/src/ScreenPainter.cs:432-452` (touch dispatch) · `plugin/preview/PreviewMain.cs`
(the GDI+ renderer and the fixture).

**S49's entry, and what the glass says about it.** S49 §2 rates the Cover *"One live region … Everything
else is baked art or C# literals"* and files H1–H9. The renders **confirm H1, H2, H3, H4, H5, H8 and H9**,
**correct H6** (the row is not "neither lit" — see C-08), and add five defects S49 could not have seen from
source alone (C-02, C-03, C-04, C-05, C-06) plus one it did not reach (C-07).

---

## OWNER RULINGS ON RECORD (2026-09-05)

Recorded per **C1.12's evidentiary standard** — the owner's actual words, quoted, given directly in the QC
chat on review of the first pass. These are design decisions on this page's layout and they **supersede the
measured Frame 67 placement** for the elements they name (§1.4 / C7.1: the owner decides). They authorise
nothing else: `install` and glass time remain separate gates, and this document remains read-and-plan.

> **R-1 (→ C-12).** *"you missed the white smudge on the bottom left of the bottom bar. This was an attempt
> to remoove the baked in white bar but you can still see it."*

> **R-2 (→ C-13).** *"The coordinates bellow the map should be evenly spaced either side of the globe so
> they do not overrun the globe."*

> **R-3 (→ C-13).** *"next button should also be moved to look like it belongs."*

> **R-4 (→ C-13, standing).** *"I like well balanced layouts."*

> **R-5 (→ C-13).** *"That entire section just below the map just looks messy to me"*

⚠ **R-1 is also a correction to this document's first pass.** The smudge was **found by the owner, not by
QC.** The first pass did examine `component_48.png`, did see bright pixels under the first icon, and filed
them privately as the reference UI's baked "active tab" indicator — correct on the Cover by coincidence, so
not a defect. That reading was wrong: `FigmaUI.cs:274-276` states the marker *"has been erased there so it
can be drawn dynamically"*, and the erase was incomplete. The lesson for later pages: **when an artefact
looks like it belongs to the design, check whether the code says it was supposed to be gone.** C-12 is the
finding that should have been in the first pass.

## What was checked and found CLEAN

These were tested and are **not** defects. Recorded so they are not re-audited.

1. **Every referenced asset exists and is placed at its own native aspect ratio.** All 63 keys in
   `CoverPage.Keys` resolve to a file in `plugin/GameData/DragonScreen/art/cover/`; for all 63, the
   declared `Box` w/h matches the PNG's native w/h to within 2%. **No missing asset, no mis-scaled asset.**
2. **S75's tint fix is visibly working.** `gridicons_refresh` renders in `DragonPalette.Text6`, dimmer than
   the three white glyphs beside it (`ui_cover.png`, the RUNNING row) — exactly what S75 intended and what
   no pre-2026-09-04 preview could show.
3. **Wiring, hit-rect → painted control: complete.** All ten rows of `CoverPage.Hits` (`CoverPage.cs:723-734`)
   sit on top of a drawn control, verified box against box. No phantom rect (the S54 defect class).
4. **S54's phase gate holds.** On slot 5 the six `HiddenOnReferencePhase` rows are suppressed from both the
   draw and the hit test; `FigmaUI.HitTest`'s no-phase call cannot dispatch them because `MapCover`
   (`FigmaUI.cs:373-383`) resolves only `Menu` and `Settings`. Verified by reading both paths.
5. **Menu and Settings ARE wired** — via `MapCover`, which runs before the painter's Cover branch. S49 H5's
   list of silent no-ops is exactly right: the four `Act*` rows and the two `Entry*` rows, and nothing else.
6. **The globe is not clipped by the bars.** `CoverPage` allocates a 1204 px square whose top (y 95.9) and
   bottom (y 1300.2) do overhang the top bar (y 146.5) and the bottom bar (y 1249.6) — but `NavPage.Planet`
   insets the drawn disc to r ≈ 530 px (measured), so the disc spans y 168…1228 and touches neither.
7. **The flat map's ground-track gap is the known, deliberate seam, not a rendering dropout.** Measured in
   `ui_cover_cam_map.png`: the track breaks between longitude **−99.9°** and **−80.1°**. The preview fixture
   builds a track from −80.6° marching 360° east minus 22° of body rotation (`PreviewMain.cs:147-157`),
   ending at −102.6° — so the measured break *is* that ~22° rotation seam. `docs/ISSUE_REGISTER.md:197`
   (N5) already adjudicated this: closing it would draw a spurious chord across the seam; **left open,
   honestly.** ⚠ It carries **no marking** — see C-10's note.
8. **The map's d-pad cluster, the NEXT VIEW pill and the rail all hit where they are drawn.** `PadRect`,
   `NextViewRect` and the `SlotY`-derived rail band are each shared by the draw and the hit test, which is
   `PageAction`'s standing rule, and the renders agree with the computed rects to the pixel.

---

## C-01 — The whole top telemetry strip is baked art, and six of its eight readings contradict the state the same frame's globe was drawn from

**TIER 1** · confirms and hardens S49 **H1** + **H2**

**Evidence.** `ui_cover.png`, the strip across the top. The eight values on the glass are:

| readout | on the glass | what `PageState` carried in that same frame | agrees? |
|---|---|---|---|
| ACTIVE PHASE | `Deorbit Coast` | `ps.Phase = "ORBITING"` (`PreviewMain.cs:72`) | ❌ |
| SPLASHDOWN TIME | `T‑01:24:51` | *(fixture sets none)* | ❌ |
| INERTIAL VELOCITY | `7.69km/s` | `ps.Velocity = "2280 m/s"` (`:74`) | ❌ |
| ALTITUDE | `393.3km` | `ps.Altitude = "123.4 km"` (`:73`) | ❌ |
| APOGEE | `416.2km` | `ps.Apoapsis = "124.0 km"` (`:75`) | ❌ |
| PERIGEE | `379.4km` | `ps.Periapsis = "121.9 km"` (`:76`) | ❌ |
| INCLINATION | `51.62°` | `ps.InclinationDegText = "0.13°"` (`:129`) | ❌ *(coincidentally near the synthetic overlay's 51.6°, `:532`)* |
| RUNNING | `00:22:57` | *(no clock in `PageState`)* | ❌ |

All eight are PNGs placed by `CoverPage.Keys[12..18]` + `[21]` at `Box` rows 12–18, 21
(`CoverPage.cs:30-31`, `:52-53`). `CoverPage.cs` reads **none** of these fields — `PageState s` reaches
exactly one function in the whole file, `DrawCameraView`.

**What is wrong.** This is the crew's home page and its entire instrument strip is another flight's numbers.
S49 proved it from source; the render proves it *against live state on the same frame* — the globe beside
the strip is drawn from `ps`, and the strip is not. This is S22's rule broken at its most visible point: a
confident reading with no source behind it. §14.4(f) governs — these are READOUTS, every one exists live and
pre-formatted, and none needs a model.

**Fix plan.**
- Add the seven baked value keys to `SkipKeys` (`CoverPage.cs:120-124`) and draw `dl.Text` at each asset's
  own measured `Box`, exactly as `ManualChuteDeployPage` already does for the same seven values. The
  method is already in this file: `AttitudeSkipKeys` (`:143-146`) + `BoxOf` (`:167-171`) skip an asset and
  redraw it from its own measured rectangle, so the row cannot drift off its neighbours.
- Sources, all present and pre-formatted: `s.SplashdownText` + `s.SplashdownShown`, `s.Velocity`,
  `s.Altitude`, `s.Apoapsis` + `s.ApogeeShown`, `s.Periapsis` + `s.PerigeeShown`, `s.InclinationDegText`
  (which exists *specifically* to print the `51.64°` glyph form this art bakes), `s.Phase`.
- The `RUNNING` clock (H2) is the one item with no field: it wants a phase-entry timestamp held in the
  painter and formatted on the second, following the codebase's format-on-change rule (`Pages.cs:46-47`).
  Do it in the same pass — a stopwatch is the single element a viewer assumes is live.
- **Must not break:** the `!Valid` dash path. Each value must dash when the feed is invalid, or this trades
  a wrong number for a *confidently* wrong number. The `*Shown` flags exist for exactly this.
- **Verify:** re-render `ui_cover.png` and check all eight against the fixture; then set `ps.Valid = false`
  in a scratch fixture and confirm eight dashes.

---

## C-02 — A 16 px black arrow is placed outside the content panel and renders on the live camera slot

**TIER 1** *(with a §1.4 sub-question — see Q1)* · **NEW — S49 could not see this from source**

**Evidence.** `ui_cover.png` at panel **(1414, 698)**, dead centre of the globe over West Africa; also on
`ui_cover_cam_map.png` (on the map, beside the vessel marker) and `ui_cover_cam_capsule.png` (on bare
navy). Absent only on `ui_cover_phase5.png`. Pixel-verified: the 18×16 box at that position contains
4–6 near-black pixels on every Cover render except phase 5.

The asset is `bi_arrow_right_short`, `CoverPage.cs:42`, box `{1706,1048,16,16}` at `CoverPage.cs:58`.

**Two defects in one asset.**

1. **Placement.** Design x **1706** is **264 px right of the content panel's own right edge** (the panel is
   `rectangle_178` at `{218,216,1224,1779}` → x 218…1442) and **336 px right of the `deorbit_burn_brief`
   row it belongs to** (`{1093,1003,277,45}` → ends at x 1370). The fill-to-fit reflow then treats 1706 as
   right of `Split = 1500` and shifts it a further `extra` px (`CoverPage.cs:327`), landing it at panel
   x 1414 — inside the camera slot, whichever camera is up. It is grouped with `deorbit_burn_brief` in
   `ReferenceSkipKeys` (`CoverPage.cs:138`), which is why phase 5 is the one render without it, and which
   is direct evidence the measurer believed it belonged to that row.
   The placements were made by *"masked template match"* against `Frame 67.png` (`CoverPage.cs:3-5`).
   A 16×16 glyph with **12 opaque pixels** is the smallest, lowest-information target in the whole set —
   exactly the case where a template match returns a false peak.
2. **Ink colour.** `bi_arrow_right_short.png` is **pure black** — 12 opaque pixels, RGB (0,0,0) — while
   every comparable glyph on the page is pure white (`ic_sharp_arrow_back`, `eva_menu_fill`,
   `gridicons_refresh`, `ic_sharp_subtract`: all RGB 255,255,255). It is drawn with
   `DragonPalette.White`, and a white multiply leaves black black. So even at the *correct* position it
   would be invisible on `#020738`; it is visible today only because it landed on a photograph.

**Fix plan.**
- **Colour** (no source question, do it now): the arrow must be tinted like its row. It sits inside the
  `deorbit_burn_brief` action row, which **does** have a hit rect (`Hits`, `CoverPage.cs:729`), so by this
  page's own white-glyph-means-button idiom (`CoverPage.cs:196-214`) it is `DragonPalette.White` — but a
  white multiply cannot lift black ink. Two routes: (a) draw it as a primitive (a `dl.Rect` shaft plus the
  head) at the row's own metrics, the way `DrawAttitudeCriteria` already replaces two baked captions from
  their own measured boxes; or (b) add it to a new "tint-from-black" list and pre-multiply. **(a) is
  recommended** — it reuses a pattern already in this file, needs no new mechanism, and removes the
  dependence on a 12-pixel PNG.
- **Placement** — this needs a §1.4 call, **Q1** below. It must not be guessed: putting the arrow at "just
  after 1370 because that looks right" is inventing a layer bound, which C1.4 forbids.
- **Must not break:** `ReferenceSkipKeys` must keep suppressing it on phase 5, and the `Keys`/`Box` row
  must stay in lockstep if either array is edited (they are index-paired).
- **Verify:** re-render all five Cover states; the 18×16 probe box at (1414, 698) must contain **zero**
  non-background pixels on every one.

---

## C-03 — The NEXT VIEW label overruns its own pill; the pill's border strikes through the final "W"

**TIER 2** · **NEW**

**Evidence.** `ui_cover.png`, `ui_cover_cam_map.png`, `ui_cover_cam_capsule.png`, `ui_cover_phase5/6.png` —
every Cover state. Measured on `ui_cover.png`:

- pill rect (from `NextViewRect`, `CoverPage.cs:233` + `:255-259`): x **1277.2 … 1544.1**
- label glyph runs, scanned in the pill's text band: the last run ends at x **1551**

So the label overshoots the pill's right border by **≈7 px** and the border is drawn *through* the "W".
The label is `dl.Text("NEXT VIEW", px + Z(130f), py + Z(32f), Z(53f), …)` (`CoverPage.cs:515`): it starts
130 design px into a 401-wide pill and is set at **53 design px**.

**What is wrong.** The pill is built as SETTINGS' twin — *"same size, same dash-then-label interior"*
(`CoverPage.cs:230-232`) — but SETTINGS' label is a baked asset only **37 design px** tall
(`settings`, `{3124,1847,140,37}`) and **140 px wide**. NEXT VIEW is set 43% larger and needs ~184 px in
180 px of room. The two do not read as a pair, and one of them is broken out of its own border. This is
the owner's standing per-page QC line — *no clipping, every string inside its box* — failing on the
control the crew uses to change the camera.

⚠ **Two-renderer note.** The 7 px is measured in GDI+. `ScreenPainter.DrawText` (`:1242-1281`) rounds the
size to an int and sums integer `CharacterInfo.advance` values, so the in-game width will differ by a
percent or two either way. At a 4% overshoot the game is not reliably safe — this must be fixed by
geometry, not by hoping the game's metrics are narrower.

**Fix plan.**
- Set the label at the twin's own cap size and position instead of at 53/130. The `settings` asset is
  140×37 at `{3124,1847}` inside a pill at `{2994,1810,401,111}` — i.e. offset **(+130, +37)** with a
  **37 px** cap. Drawing NEXT VIEW at `Z(37f)` from the same +130 offset makes the two pills genuinely
  identical and drops the measured width to ~128 px, comfortably inside 180.
- Derive the offsets with `BoxOf("settings", …)` and `BoxOf("rectangle_174", …)` rather than re-typing the
  numbers, so the twin cannot drift if either box is re-measured — the same discipline `AttX`/`AttY1`
  already use (`CoverPage.cs:154-159`).
- **Must not break:** `NextViewRect` is shared by the draw *and* the hit test *and* the tests — do not move
  the pill, only the type inside it.
- **Verify:** re-render; re-run the glyph-run scan and require the last run to end at least 8 px inside
  x = 1544. Add a headless check that the drawn label's advance width fits the pill, so the next label
  change fails the build instead of the glass.

---

## C-04 — The bottom status bar is stretched 12.2% horizontally: its circular icons render as ellipses and all its baked type is distorted

**TIER 2** · **NEW**

**Evidence.** `ui_cover.png`, the bottom bar.

- `component_48.png` is **3427×235**. Its crosshair icon is exactly **square** in the asset: bounding box
  130×130, ratio **1.000**.
- The bar is drawn `dl.Asset("component_48", X(0), Y(1877), Wd(0, 3427), Z(235), …)` (`CoverPage.cs:338`).
  `Wd` (`CoverPage.cs:328`) **stretches any asset that straddles `Split`** — `0 + 3427 > 1500` — so the bar
  is drawn at width **2560** for a height scale of 0.6657. x-scale **0.7470**, y-scale **0.6657** →
  **+12.2% horizontal**.
- Measured on the render: the crosshair is **48 × 42 px**, ratio **1.143**.

**What is wrong.** Every glyph baked into that bar is 12% wider than tall: the five nav icons (a circle
drawn as an ellipse), `CURRENT STATE`, `Far Field Pointing Deorbit`, `POINTING MODE`, `Sun + GEO`, the
SPX / GND / TDRS / ISS block and `79/1450122`. `CoverPage`'s own header states the opposite —
*"Nothing is scaled non-uniformly, so the globe stays round and text/icons keep their exact size"*
(`CoverPage.cs:316-320`) — which is true of every asset except the two that straddle the split, and one of
those carries the page's entire persistent navigation.

**Scope.** `rectangle_173` (the top bar) also straddles and is also stretched, but it is a flat fill, so it
is harmless. `component_48` is drawn by **fifteen** pure pages; twelve of them pass a plain `w`
(`AscentPage.cs:147`, `DeorbitBurnPrepPage.cs:145`, `DockingSimPage.cs:194`, `EntryPage.cs:76`,
`FigmaFramePage.cs:28`, `Frame58Hud.cs:52`, `MenuPage.cs:83`, `NavOrbitPlotPage.cs:109`,
`PlaceholderPage.cs:38`, `RendezvousPage.cs:66` …), which stretches it the same way. **Only Cover and
ManualChute route it through `Wd`.** Either way the bar is stretched — this is a page-family defect first
seen here, and the fix belongs with whichever line takes the bar.

**Fix plan.**
- Draw `component_48` **uniformly** at `3427 * sc` and fill the horizontal remainder with the design's own
  bar background (`DragonPalette` background / the bar's own fill) so the bar still reaches both edges.
  The bar's content is left-and-right anchored, not centred, so a straight stretch is the wrong reflow for
  it: it should be **split** the way the page body already is — icons anchored left at design scale, the
  right-hand SPX/ISS block anchored right at design scale, and the slack in the middle, which is where the
  bar's own empty gap already is.
- The cleanest form reuses the machinery already in this file: draw the bar in **two** `dl.Asset` calls
  with a source-rect split at the design x where the bar is empty (between `Sun + GEO` and the SPX block),
  each half at uniform `sc`, left half anchored at 0 and right half anchored to the right edge. That needs
  `dl.Asset` to accept a source sub-rect, which it does not today — so the alternative is to extend
  `DisplayList.Asset` with a UV window (the `ImageUV` path already exists for `dl.Image`).
- **Must not break:** `FigmaUI.BottomBarHit` (`FigmaUI.cs:117-131`) maps a touch by
  `BarIconX[i] / RefW * w` — i.e. it *assumes the stretch*. If the draw stops stretching, the hit map must
  change with it, or every nav icon's touch target slides off its icon. **These two must be changed in one
  commit.** `FigmaUI.BottomBarMarker` (`:280-285`) has the same assumption.
- **Verify:** re-render; the crosshair's bounding box must come back square (ratio 1.00 ± 0.03). Add a
  headless check that the bar's drawn width ÷ 3427 equals its drawn height ÷ 235.

---

## C-05 — `FitRows` compares a DESIGN-space size against a PANEL-pixel legibility floor, so the floor never fires

**TIER 1** · **NEW**

**Evidence.** `ui_cover_phase5.png` — the Reference Content phase. Measured ink heights:

| card | rows | measured ink height | measured line pitch |
|---|---|---|---|
| **ENTRY TIMELINE** (7 rows, shortest slot) | 7 | **11–14 px** | **19 px** |
| CONTINGENCY (4 rows) | 4 | 16 px | 26.6 px |

The arithmetic behind it, from `FitRows` (`CoverPage.cs:591-607`) and its callers (`:634-670`):
card 1 has `top = 555`, `slotBottom = 760`, `count = 7`, `wantSize = 26`, `wantGap = 32` → `avail = 193`,
`need = 218`, `k = 0.885` → **size 23.0 design px**, which at `sc = 0.6657` renders at **15.3 panel px**.
Cards 2 and 3 both fit unscaled at 26 design px → 17.3 panel px.

**What is wrong.** `Typography.Min = 16f` is documented as a **measured** floor —
`plugin/src/pure/Typography.cs:2`: *"16 PX IS MEASURED, NOT CHOSEN"* — and every other caller uses
`Typography.*` as a **panel-pixel** size (`NavPage.cs:449`, `:481`, `:493`, `dl.Text(..., Typography.Caption, ...)`).
`FitRows` receives `top`, `slotBottom`, `wantSize` and `wantGap` in **design** units and returns a design
size, which the caller then multiplies by `Z()`. So line `:601`, `if (size < Typography.Min)`, compares
design px to panel px and **under-protects by the height scale**: at this panel it would permit type down
to 16 × 0.6657 = **10.6 panel px** before clamping. Today the ENTRY TIMELINE already sits at 15.3 px —
**below the project's own measured legibility floor** — and the clamp never noticed.

The second half of the symptom is the leading: 19 px of pitch for 14 px of ink is 5 px of gap, so the card
reads as one grey block, while cards 2 and 3 sit roughly 45% empty. `FitRows`' header says exactly why —
*"the densest list, the seven-step ENTRY TIMELINE, sits in the SHORTEST one"* (`CoverPage.cs:576-580`) —
but the remedy it chose trades overflow for illegibility instead of rebalancing.

**Fix plan.**
- **The unit bug first, on its own.** `FitRows` must compare in the space it is clamping. Either pass the
  height scale in and clamp at `Typography.Min / sc`, or move the clamp to the caller where `Z()` is in
  scope. **Passing `sc` in is recommended** — it keeps one function owning the fit, and the caller already
  has `sc`. Once fixed, card 1's 23.0 design px (15.3 panel px) *will* trip the clamp, which is the point.
- **Then the overflow the clamp will now expose.** With the floor honoured, seven rows at ≥16 panel px will
  not fit a 317-design-px card. Three options, and the choice is a layout decision, not a code one:
  (a) shorten the seven ENTRY TIMELINE strings so they fit at the floor size — they are §8 flight facts and
  their *wording* is ours, not a tier-1 quotation, so this is available;
  (b) move ENTRY TIMELINE into card 3 (550 px, currently ~45% empty) and CONTINGENCY into card 1 —
  the three card backgrounds are real Figma layout and would not move, only their contents;
  (c) let card 1 scroll (which is also what would give `rectangle_182` an honest job — see C-06).
  **(b) is recommended:** it needs no new mechanism, changes no baked art, keeps every string intact, and
  puts the densest list in the tallest slot, which is what the header says the problem is.
- **A smaller, separate nit in the same function:** `Card` draws its rows at `X(340)` while its title sits
  at `X(362)` (`CoverPage.cs:628-632`), so the body hangs 22 design px left of its own heading. The baked
  (non-Reference) body puts rows at **316** and titles at **362**. Whichever is right, the reference body
  and the reference-content body should share one left margin.
- **Must not break:** `FitRows` is `public` and its contract (returns the wanted size untouched when the
  block already fits) is relied on by cards 2 and 3, which must render byte-identically after the change.
- **Verify:** re-render `ui_cover_phase5.png`; every body row's measured ink height must be ≥ the ink
  height of the CONTINGENCY rows (16 px). Add a headless check that `FitRows` never returns a panel-space
  size below `Typography.Min`.

---

## C-06 — The panel's scrollbar thumb is painted in full white with no hit rect, no scroll model, and nothing to scroll

**TIER 2** · **NEW** · same defect class as S75

**Evidence.** `ui_cover.png` and `ui_cover_phase5.png`: a light-lavender vertical bar hard against the
content panel's inner right edge, running from the top of card 1 to partway down card 3. It is
`rectangle_182`, `{1427,438,15,920}` (`CoverPage.cs:29`, `:51`) — a 15×920 thumb inside a panel whose
inner edge is at x 1442.

**What is wrong.** A scrollbar thumb is an interactive idiom: its length says "there is more below" and its
position says "you are here". This one is fixed-length, fixed-position, has **no entry in `Hits`**, is not
in `InertKeys` (`CoverPage.cs:215`), and is drawn in full `DragonPalette.White` — so it reads as live.
Nothing on the page scrolls, and on the Reference Content phase (C-05) the content that *would* justify a
scrollbar is being crushed to fit instead.

This is precisely the case S75 named and fixed one glyph of: *"a painted control that resolves to nothing,
which is worse than a no-op because a no-op at least names an action"* (`CoverPage.cs:203-206`). S75 took
the inert-tint branch for `gridicons_refresh` and left this one untouched — S49 H18 files the same class
under the Vehicle page's `SHOW MARGINS TO`.

**Fix plan.** Two honest routes; they are not equivalent and the choice follows C-05.
- **(a) Make it inert, now.** Add `"rectangle_182"` to `InertKeys`. One line, uses the mechanism already in
  the file, and puts the thumb in the same "nothing live behind this" tint as the refresh glyph so it stops
  claiming an affordance. **Recommended as the immediate fix** — it is correct whatever C-05 decides.
- **(b) Make it real, later.** If C-05 lands option (c) (a scrolling card), the thumb becomes the scroll
  indicator: length = viewport ÷ content, position = offset, and it enters `Hits` as a drag target. Then it
  goes back to `White` **and** into `Hits` together — S75's own rule (`CoverPage.cs:212-214`).
- **Must not break:** the thumb must stay drawn. Removing the asset would leave the panel's right gutter
  empty and change the Figma layout; the defect is what it *claims*, not that it exists.
- **Verify:** re-render and confirm the thumb is dimmer than the panel's white hairlines and the three
  white glyphs, in the same relationship `gridicons_refresh` now has.

---

## C-07 — The ◄/► arrows can park the Cover on phase 6, printing "Manual Chute Deploy" over the Coast body while the real page sits one tap away

**TIER 1** · **NEW** — extends S49 **H4**

**Evidence.** `ui_cover_phase6.png`: the heading reads **"Manual Chute Deploy"**, the rail highlight is on
slot 6, and the body below is the **Coast to Trunk Jettison** content, byte-identical to `ui_cover.png` —
Crew Interrupt Conditions, Crew Deorbit Preparation, the three numbered steps and the ENTRY ENABLED row.

**Why it is reachable, which S49's table does not capture.** S49 §2 records slot 6 as the one rail item
that *"navigates"*, and it does: `MapCover` maps `PhaseManual → NavHit.Go(UiPage.ManualChute)`
(`FigmaUI.cs:380`), and `FigmaUI.HitTest` runs **before** the painter's Cover branch
(`ScreenPainter.cs:364-366`). So a tap on the rail never sets `coverPhase = 6`.

But the ◄/► arrows do, and they wrap over all seven:

```
else if (cb == CoverPage.CoverButton.Back)    coverPhase = (coverPhase + PhaseCount - 1) % PhaseCount;
else if (cb == CoverPage.CoverButton.Forward) coverPhase = (coverPhase + 1) % PhaseCount;
```
(`ScreenPainter.cs:441-444`)

Stepping ► from slot 5, or ◄ from slot 0, lands on **coverPhase = 6 while still on the Cover** — the state
the render shows. The heading names a page that exists (`UiPage.ManualChute`, a real screen with its own
live telemetry strip and globe) while the body underneath belongs to a different phase entirely.

**What is wrong.** Two separate faults, both reachable:
1. **A heading that lies about its body.** The one thing on the page that changes with the rail is the
   heading string (`dl.Text(PhaseName[sp], …)`, `CoverPage.cs:373`); the body is gated on slot 5 alone
   (`bool refPhase = (sp == ReferencePhase)`, `:346`). S49 H4 records this for slots 0–4. Slot 6 is worse
   than those, because its heading names a **real page whose real content the crew could be looking at**.
2. **Two navigation models for one rail item.** Tapping slot 6 navigates; arrowing to slot 6 does not.
   The same control reached two ways produces two different outcomes.

**Fix plan.**
- **The arrows must obey the rail's own model.** Slot 6 is a navigation target, so ► from slot 5 should
  either **navigate to `UiPage.ManualChute`** (consistent — the arrows walk the same seven items the rail
  does) or **wrap 5 → 0, skipping 6** (the arrows step only the in-page phases). **Navigating is
  recommended:** the arrows are drawn as the rail's stepper and a stepper that silently skips an item the
  rail shows is its own small lie. Implement by routing `Back`/`Forward` through the same
  `PhaseButton`/`MapCover` pair the rail uses, so there is one model, not two.
- **Must not break:** wrapping must stay total — from slot 0, ◄ must still reach slot 6 (i.e. navigate),
  and returning from `ManualChute` must land back on a sane `coverPhase`. `coverPhase` is per-screen and
  not persisted (`ScreenPainter.cs:84`), so no save-compat issue.
- **Slots 0, 2, 3 and 4 remain S49 H4's, and stay owner-gated where S49 says:** giving slots 0/1/2 their
  own bodies is not gated (the deorbit sequence they name is documented in §8); routing slots 3/4 is —
  **S27 already put it to the owner and the owner declined to assign the two generic "Procedure" slots.**
  This finding does not reopen that.
- **Verify:** add per-slot preview renders (`ui_cover_phase0.png` … `ui_cover_phase4.png`) so all seven
  bodies are on the gate instead of one. That also closes the four ⏳ PART rows in the inventory above,
  and it is four lines in `PreviewMain.cs` beside the two that already exist (`:918-934`).

---

## C-08 — ENTRY ENABLED is a baked verdict, permanently False — and S49's reading of it is wrong

**TIER 3** *(owner decision — this is S49 §8's Q2, still open)* · **corrects S49 H6**

**Evidence.** `ui_cover.png`, the ENTRY ENABLED row, magnified. S49 H6 states the row *"shows `True` **and**
`False` at once, neither lit"*. The render shows something different and more specific: **`True` is drawn
dim and light, `False` is drawn bright white and bold.** The baked art carries a *selected* state, and the
selection is **False**.

The three assets are `entry_enabled` `{351,1555,195,34}`, `true` `{783,1555,51,34}` and `false`
`{1132,1549,81,45}` (`CoverPage.cs:47-48`, `:59-60`). The `false` box is taller and wider than `true`'s
because the glyph is set heavier — the emphasis is in the PNG.

**What is wrong.** The Cover's whole body is the deorbit procedure, and step 2 of it —
*"After SpaceX GO for deorbit, verify entry is enabled"* — resolves, permanently and on every phase, to
**not enabled**. It is a safety verdict with no model behind it, which is the S31/S32 guardrail: a verdict
word must be computed, never declared. Both `EntryTrue` and `EntryFalse` have hit rects
(`CoverPage.cs:732-733`) and neither has a dispatcher case, so the crew cannot change it either.

**This needs the owner, and S49 already asked.** S49 §8 **Q2** is the same question and is **still open**:
if the row means *"the crew verified it"* it is a local latch and Part-A (A); if it means *"the vehicle has
armed entry"* it is an arming flag on the §14.4(a) side and Part-B (B). **No ruling on record.** This
finding adds one piece of evidence to that question and nothing else: the baked art *chooses* False, which
is what a vehicle state would look like before arming and is a strange default for a crew checkbox.

**Fix plan.** Held pending the owner's answer to Q2. Whichever way it goes:
- the **word must stop being a PNG** — the two value glyphs join `SkipKeys` and are drawn as text at their
  own measured boxes (C-01's method), with the lit/dim tint chosen by the model rather than by which file
  was exported;
- if (A), `EntryTrue`/`EntryFalse` become a latch in the painter beside `coverPhase`, and the two hit rects
  finally do something;
- if (B), both rows are drawn but **not** hit-testable until Part B wires the arming flag, and the row
  reads from whatever `PageState` field that flag lands in — §14.4(a), honest no-op, **no red**.
- **Must not break:** either way the row must dash, not read `False`, when there is no source — a
  confidently wrong "not enabled" is the defect being removed.

---

## C-09 — The globe and the flat map use opposite u→screen conventions, and the preview now shows them disagreeing on the same texture in the same session

**TIER 3** *(needs glass time — an owner gate)* · **NEW as rendered evidence; re-opens `ISSUE_REGISTER.md` N5**

**Evidence.** Two Cover renders from the same build, the same fixture, the same body texture:

- `ui_cover.png` — the **globe**, centred on longitude 0: South America on the **left**, Africa on the
  **right**. West-left, east-right. **Not mirrored.**
- `ui_cover_cam_map.png` — the **flat map**, centred on longitude 0: Asia and Australia on the **left**,
  Africa centre-right, the Americas on the **right**. East-left, west-right. **Mirrored.**

Both read `ImageId.BodyMap`, which in the preview is one stand-in equirectangular Earth
(`PreviewMain.cs:1510-1530`). One texture, one frame, two handednesses.

The source says the same thing plainly. The flat map's quad **swaps** u —
`dl.ImageUV(ImageId.BodyMap, …, q.UMax, q.UMin, …)` (`NavPage.cs:355`), *"to un-mirror KSP's `_ColorMap`"*
(`:348-350`). The globe's strips **do not** — *"⛔ NOT SWAPPED, unlike NavPage.Quad — and that is CORRECT,
confirmed in the PNG preview"* (`NavPage.cs:931-937`). And `PageTest.NavTexture` fences both in
(`plugin/test/PageTest.cs:242-313`), with a docstring asserting *"The two NAV textured views use OPPOSITE
u-conventions, and BOTH are correct."*

**What is wrong — carefully stated.** "Both correct" requires the two views to read the same way on the
same texture. The current preview shows that they do not. Look at how each half was proved:

| view | convention | what it was proved against |
|---|---|---|
| flat map | swap u | **the game** — *"user-confirmed in game, 2026-08-27"* (`PageTest.cs:245`), i.e. KSP's real `_ColorMap` |
| globe | no swap | **the preview** — *"the PNG preview shows the swap there puts India/east on the LEFT"* (`PageTest.cs:247`), i.e. the stand-in Earth |

Two different textures, two different verdicts, and the two textures cannot have the same handedness — the
flat map's swap exists *because* KSP's is mirrored, and the stand-in is a plain Earth map that is not. So
**at most one of the two views is right in game**, and the preview cannot tell which: it will always
flatter the globe and slander the map, because that is the texture it has.

`docs/ISSUE_REGISTER.md:197` (N5) closed this as *"BOTH FALSE ALARMS (Grok wrong, disproven in the PNG
preview, 2026-08-29)"*. Half of that disproof used the wrong instrument. ⚠ **I am not asserting the globe
is mirrored in game — I cannot see the game.** I am asserting that the evidence on record does not
establish that it is not, and that the preview visibly contradicts the test's own "read the same way".

**Fix plan.**
- **This is settled by one look at the glass and by nothing else.** `install` + glass time are separate
  owner gates (CLAUDE.md banner; C1.12), so a build chat stops here. **Q2** below poses it.
- **The cheap preparation that needs no gate:** make the preview able to answer the question by adding a
  **mirrored** stand-in alongside the normal one, and rendering the globe and the map against both. If the
  two views ever agree on either texture, the "opposite conventions" premise is wrong; if they never do,
  the glass check has to name which one is right. That is a preview-harness change only.
- **The confirming check, once the owner opens glass time:** on orbit, put the Cover on the Earth view over
  a recognisable coastline and step NEXT VIEW to the map. Same body, same longitude, two seconds apart.
- **Must not break:** whichever way it resolves, `PageTest.NavTexture` must be updated in the same commit —
  it currently pins the disagreement as correct, so a correct fix would fail the build.

---

## C-10 — The Cover's preview fixture is internally inconsistent, so the preview cannot judge marker-versus-track agreement on this page

**TIER 2** *(the QC instrument itself)* · **NEW**

**Evidence.** In the frame that produced `ui_cover.png`, three descriptions of the same vehicle disagree:

| quantity | fixture value | where |
|---|---|---|
| inclination (scalar readout) | **0.13°** | `PreviewMain.cs:124`, `:129` |
| inclination (orbit overlay) | **51.6°** | `PreviewMain.cs:532` |
| orbit radius (scalars) | 123.4 km over a 600 km body → ratio **1.206** | `:73`, `:124-126` |
| orbit radius (overlay) | ratio **1.06** | `:538` |
| vessel position (marker) | lat 0, lon 0 | `:546` |
| ground track (flat map) | built around lon **−80.6**, lat 51.6·sin θ | `:143-157` |

The visible consequence: on `ui_cover_cam_map.png` the green vessel cross sits at the map's centre while
its own ground track passes ~10° of latitude above it. On `ui_cover.png` the AP/PE markers sit at ratio
1.06 (just outside the disc) rather than the 1.206 the scalars imply.

**What is wrong.** Each of these was a reasonable local choice — the 51.6° overlay is explicitly *"a
synthetic inclined orbit so the projection + far-side occlusion are visible offline"* (`PreviewMain.cs:527`),
and the fixture header is honest that *"telemetry here is fine BECAUSE it is a design tool that nobody
flies"* (`:68-69`). But the Cover block reuses the NAV/PLANET block's overlay wholesale (`:566-567`) and
inherits `ps.Latitude = 0.0; ps.Longitude = 0.0` from it (`:546`), which silently overwrites the pad
coordinates the ground track was built around. The result is that **the one page-level question a preview
of this page could answer — do the markers, the track and the readouts agree? — cannot be asked.** CLAUDE.md
makes the preview the gate that saves restarts; a fixture that cannot fail is not a gate.

**Fix plan.**
- Derive the Cover fixture's overlay, ground track, vessel marker and scalar readouts **from one orbit**.
  A single small helper that takes (inclination, altitude, body radius, true anomaly) and fills
  `ov.OrbitLat/Lon/Ratio`, `ov.Ap/Pe/Vessel`, `ps.TrackLat/Lon`, and the six scalar strings would make every
  element of the page checkable against every other.
- Keep the 51.6° inclination — it is the right shape for catching a wrong projection, and it matches the
  real Dragon inclination the baked strip prints. Change the **scalars** to match it, not the other way.
- **Must not break:** `page2_nav_planet.png` and the NAV renders share this overlay; they must keep
  exercising the seam-straddle and far-side occlusion cases they were built for.
- **A related, unmarked-state note (no code change proposed):** the flat map's ground track has a real,
  deliberate ~22° gap (see CLEAN item 7). It is drawn with no marking, so at IVA distance it reads as a
  dropout rather than as "this track starts here". Worth a tick or a label if a future line touches
  `NavPage.Map`; not worth a line of its own.
- **Verify:** re-render; the vessel marker must sit **on** its own ground track on the map, and the AP/PE
  markers must sit at the radius the ALTITUDE readout implies.

---

## C-11 — The preview draws tinted assets at integer rectangles while the game draws them at float

**TIER 2** · **NEW** · two-renderer divergence, small but real

**Evidence.** `PreviewMain.DrawCoverAsset` has two paths (`plugin/preview/PreviewMain.cs:1640-1659`).
Opaque white takes `g.DrawImage(img, new RectangleF(c.A, c.B, c.C, c.D), …)` — **sub-pixel**. Any other
tint takes `g.DrawImage(img, new Rectangle((int)c.A, (int)c.B, (int)c.C, (int)c.D), …)` — **truncated to
integers**. `ScreenPainter.DrawImage` uses float vertices for both (`ScreenPainter.cs:1181`).

**What is wrong.** A tinted asset can sit up to 1 px higher and left, and be up to 1 px narrower and
shorter, in the preview than in the game — and than an untinted asset drawn beside it. On the Cover the only
tinted asset is `gridicons_refresh`, so the visible error is ≤1 px. But S75's whole lesson is that the two
renderers must not diverge silently, and this divergence was introduced *by* S75's fix: the tint path is new
code and it took a different rounding rule from the path it was added next to.

**Fix plan.**
- Use the `RectangleF` overload on the tinted path too. `Graphics.DrawImage` has a
  `(Image, RectangleF, RectangleF, GraphicsUnit, ImageAttributes)` form; if the exact overload is not
  available on the target framework, draw into a `GraphicsPath`/`TextureBrush` or pre-multiply the tint into
  a cached bitmap and reuse the float path. **Pre-multiplying into a cached tinted bitmap is recommended** —
  it removes the second path entirely, so the two can never drift again, and the cache is already there
  (`coverCache`).
- **Must not break:** opaque-white assets must keep rendering byte-identically, which was S75's own
  acceptance condition (`PreviewMain.cs:1639-1640`).
- **Verify:** render the Cover before and after; every asset except `gridicons_refresh` must be
  byte-identical, and `gridicons_refresh` must move by ≤1 px toward its float position.

---

## C-12 — The baked tab marker was erased from `component_48.png` but its glow was left behind: a white smudge sits at the bottom-left of every page

**TIER 1** · **NEW — owner-found (R-1); the first QC pass mis-read this as by-design**

**Evidence.** `ui_cabin.png`, bottom-left corner, magnified — chosen deliberately because the Cabin page's
active tab is **icon 4**, so the dynamic marker is nowhere near this corner and everything visible here is
leftover. Two artefacts under the first icon:

1. a **soft glow halo** spreading up and outward from the bar's bottom edge, and
2. a **pale residual bar** along the very bottom, clipped by the asset's own edge.

Measured in the asset (`component_48.png`, bar-local coordinates): elevated luminance across
**x ≈ 25…145, y ≈ 196…229**, peaking at **112** against a bar background of **42** — 2.7× the surround.
It falls back to background at y ≥ 231, which is the tell: the *hard* pill below y 231 was erased cleanly
and the *glow* above it was not.

Measured on the renders, over the residue box (panel x 14…112, y 1378…1400) against plain bar background
at the same rows:

| render | active tab | residue mean | bar background mean |
|---|---|---|---|
| `ui_cover.png` | icon 0 | 83.1 | 51.3 |
| `ui_ascent.png` | icon 0 | 83.1 | 51.3 |
| `ui_cabin.png` | **icon 4** | **73.1** | 51.3 |
| `ui_audiovideo.png` | **icon 4** | **73.1** | 51.3 |

The residue is present at 73.1 even where the marker is not — **it is not the marker.**

**What is wrong.** `FigmaUI.cs:274-276` records the intent: *"The marker was baked under the first icon in
`component_48.png`; it has been erased there so it can be drawn dynamically."* `BottomBarMarker`
(`FigmaUI.cs:280-286`) then draws a crisp 108×10 white bar under whichever tab is active. The erase removed
the pill and left its halo, so:

- on the Cover the crew sees the correct marker **plus** a halo bleeding out of it;
- on every other page the crew sees the correct marker under one icon **and a permanent ghost marker under
  icon 0**, which is exactly the state the dynamic marker exists to prevent — the bar says "Cover is active"
  on every page in the build.

`component_48` is drawn by fifteen pure pages, so this is on **every screen**, not just this one.

**Fix plan.**
- **Finish the erase in the asset.** Clear `component_48.png` to the bar's own background across the
  marker's full footprint including the glow — bar-local **x 20…150, y 190…235**, generously beyond the
  measured 25…145 / 196…229 so no fringe survives resampling. The bar background is a flat fill
  (luminance 42, sampled well clear of any glyph), so this is a rectangle fill, not a retouch.
- **Provenance:** `component_48.png` is a community-Figma export and this is the second edit to it (the
  first being the erase itself). Note the edit in `docs/ASSET_INDEX.md` so the shipped asset's divergence
  from the export stays recorded (C7.1) — the repo copy is authoritative, but the divergence must be
  written down.
- **Then verify the dynamic marker is doing its whole job**, which the residue has been masking: with the
  corner clean, `ui_cabin.png` and `ui_audiovideo.png` must show **one** marker, under icon 4.
- **Must not break:** the marker's own geometry. `MarkY = 1877 + 223`, `MarkH = 10`, `MarkW = 108`
  (`FigmaUI.cs:278`) are measured from the erased block; if the erase is widened, those constants stay as
  they are — the block being cleared is bigger than the marker being drawn, and that is correct.
- **Verify:** re-render; the residue probe (panel x 14…112, y 1378…1400) must read within 2 luminance units
  of the plain bar background on a page whose active tab is not icon 0. Add that as a headless pixel check
  if the preview harness can assert on rendered output, so an asset re-export cannot silently restore it.
- ⚠ **Take this with C-04 in one line.** Both are `component_48` defects, both are page-wide, and both are
  fixed by touching the same asset and its two draw sites.

---

## C-13 — The band below the globe is unbalanced: the coordinate readouts sit on the globe's foot, and NEXT VIEW is 296 px off its mirror position

**TIER 2** · **NEW — owner-directed (R-2, R-3, R-4, R-5)**

**Evidence.** `ui_cover.png`, the strip between the globe and the bottom bar. Measured geometry, all panel
px at 2560×1406:

- **camera slot:** x **960.0 … 2560**, centre **1760.0**; y 146.5 … 1249.6
- **globe:** centre (1760.0, 698.0), surface radius **530** (ray-measured, four directions: 528/530/529/531),
  so the disc spans x 1230 … 2290 and **bottom y = 1228.0**
- **clear band below the disc: 21.5 px** (1228.0 → 1249.6). Nothing can sit under the globe.

Element boxes in the band, and what each one does to the disc:

| element | x span | overruns the disc? |
|---|---|---|
| NEXT VIEW pill | 1277.2 … 1544.1 | clear — but only 61 px from the disc's foot |
| **TARGET LATITUDE** | 1619.4 … 1786.5 | **yes — 152 of its 167 px (91%) lie over the globe** |
| **TARGET LONGITUDE** | 1850.4 … 2036.8 | **yes — 35 px (19%)** |
| SETTINGS pill | 2271.4 … 2538.4 | clear |
| CAMERA caption | centred 2411.2, y 1145.7 | clear |

*(the disc's foot spans 1634.8 … 1885.2 at the readouts' own top row, y 1213, shrinking to nothing by 1228)*

And the spacing across the band, left to right:

```
slot edge →NEXT VIEW→ TARGET LAT →TARGET LON→        →SETTINGS→ slot edge
   317.2        75.3         63.9        234.6          21.6
```

**317 / 75 / 64 / 235 / 22.** Everything is crowded right of centre, the left third of the band is empty,
and the two readouts are pushed onto the globe's foot. That is the "messy" (R-5) measured.

**What is wrong.** The band's five elements are each placed at their own baked Frame 67 design-x and then
put through the page's fill-to-fit reflow (`CoverPage.cs:325-329`), which shifts everything at design
x ≥ 1500 right by `extra` — 278.6 px at this aspect. `NextX = 1500f` (`CoverPage.cs:233`) sits **exactly on
the Split**, so the pill takes the full shift and lands 296 px right of where it would balance SETTINGS.
The readouts take the same shift and land on the globe. Nothing in the band is positioned **relative to the
slot or to the globe**, so the layout is only correct at the one aspect ratio the design was drawn at.

**Fix plan — a band computed from the slot and the disc, not from baked design-x.**

Anchor every element to the slot rect and the globe's own geometry, both of which `CoverPage` already
computes (`ViewLeft`, `w`, and the `gcx`/`gs` used by `DrawCameraView`). Then the band is symmetric at any
panel aspect, which is the durable form of R-4.

1. **Mirror the pills.** SETTINGS' own right margin is **32 design px** (its box ends at 3395 in a 3427-wide
   frame) — take that as the band inset. Place NEXT VIEW at `slotLeft + inset` and SETTINGS at
   `slotRight − inset − width`, both keeping their existing 401×111 design size and row.
   → NEXT VIEW **981.3 … 1248.2** (moves left 295.9 px); SETTINGS **2271.8 … 2538.7** (unchanged — it is
   already at its mirror position, which is why it looks settled and NEXT VIEW does not). This is R-3.
2. **Put the readouts either side of the globe, symmetric about its centreline.** Centre each in the clear
   span between its pill and the disc's foot:
   - TARGET LATITUDE centred at `(1248.2 + 1634.8) / 2` = **1441.5**
   - TARGET LONGITUDE centred at `(1885.2 + 2271.8) / 2` = **2078.5**
   - and `(1441.5 + 2078.5) / 2` = **1760.0** — exactly the slot and globe centre. The pair comes out
     symmetric by construction, not by tuning. This is R-2.

   Resulting spacing: `21.3 / 109.7 / 109.6 / (globe) / 99.9 / 99.9 / 21.3`.
3. **The residual 10 px asymmetry** in step 2 is the two baked PNGs' different widths (167.1 vs 186.4 — the
   longer word). It disappears if the readouts are drawn as **text centred on ±318.5 from the slot centre**
   rather than as left-anchored PNGs — which is **C-01's method and should be the same build**: these two are
   baked art of someone else's coordinates (`26° 15.00° N` printed twice, S49 H3) and have to become live
   text regardless. Doing C-13 with PNGs still standing leaves the band 10 px off true.
4. **The CAMERA caption is the one element with no mirror**, and it is part of what reads as messy. Three
   options, and this is a design call — see **Q4**.

**Must not break.**
- `NextViewRect` (`CoverPage.cs:255-259`) is shared by the draw, the hit test **and** the tests, which is
  `PageAction`'s standing rule — change it in one place and the touch target follows the pill automatically.
  `FigmaUINavTest.cs:479-513` exercises the Cover's hit map and must be re-run, not re-pinned to old numbers.
- **The Map and Capsule views use the same band** with the readouts hidden (`EarthOnlyKeys`,
  `CoverPage.cs:195-196`). Both must be re-rendered: with NEXT VIEW moved left, the Map view's band becomes
  two mirrored pills, which is the arrangement it should have had all along.
- The **map d-pad cluster** is anchored to `MapRect`'s top-right (`PadRect`, `CoverPage.cs:261-279`) and is
  untouched by this.
- The globe itself must not move or resize — R-2 asks for the readouts to clear the globe, not for the
  globe to shrink. Its ±1.5 px overlap of the two bars (the atmosphere ring reaches r = 553, i.e. y 145 and
  1251, against bars at 146.5 and 1249.6) is cosmetically invisible and out of scope here.
- **C-03 still applies on top of this.** Moving the pill does not fix the label overrunning it; the label
  must also come down to its twin's 37 design px. Do both in one pass or the relocated pill still has a
  border through its "W".

**Verify.** Re-render all five Cover states. Then assert, as headless checks so the balance cannot drift:
the two pills' insets from their slot edges are equal; the two readout centres are equidistant from the slot
centre; and no readout box intersects the disc (centre + radius are both computable from the same constants
the draw uses).

---

## Open questions for the owner — Cover (Q1–Q4)

Per C1.14. Each is a paste-ready overseer prompt (C1.13). **The QC role decides none of these and proceeds
past none.**

### Q1 — `bi_arrow_right_short` is placed outside the content panel. Where does it actually belong? (C-02)

**Situation.** The Cover places a 16×16 arrow glyph at design `{1706,1048}`. That is 264 px right of the
content panel's own right edge and 336 px right of the "Deorbit Burn Brief" row it is grouped with in the
code. After the page's fill-to-fit reflow it lands on the live camera slot — a black speck on the globe, the
map and the capsule alike. Every placement on this page was made by masked template match against
`Frame 67.png`; this is the smallest target in the set (12 opaque pixels) and the likeliest false match.
The **colour** half of the defect (black ink on a navy page) can be fixed without a ruling. The
**position** cannot: choosing one would be inventing a layer bound, which C1.4 forbids.

**Options.**
1. **Re-measure from the Figma export's layer bounds rather than by template match**, and use whatever
   comes back. Needs the export; `assets/figma/` is gitignored, so this may require the owner to supply the
   node bounds. *(Recommended — it is the only option that answers the question with a source.)*
2. **Treat the glyph as part of the `deorbit_burn_brief` row** and draw it immediately after that row's
   right edge (design x 1370) at the row's own vertical centre. Coherent with the row it belongs to, but the
   exact offset is ours, so the row must be **MARKED** as our geometry (§14.4(e)).
3. **Drop the glyph.** Remove the key from `Keys`/`Box`; the action row it decorates already has a hit rect
   and a label, so nothing is lost functionally. Cheapest, and it removes a defect rather than relocating
   one — but it deletes a real element of the reference design.
4. **Leave it, fix only the colour.** Not recommended: a correctly-tinted arrow in the wrong place is a
   *more* visible defect than a black one.

**Recommendation: 1, falling back to 3 if the export cannot be produced.** Option 2 is acceptable but adds a
marked invention to a page that currently has none in its placements.

### Q2 — The globe and the flat map disagree about which way the body faces. Settling it needs glass time. (C-09)

**Situation.** In the same frame, from the same texture, the Cover's Earth view draws the planet west-left
and the Cover's Map view draws it east-left. The source pins both conventions and a regression test asserts
*"BOTH are correct"* — but the flat map's convention was confirmed **in game** (against KSP's `_ColorMap`)
and the globe's was confirmed **in the preview** (against a stand-in Earth). Those two textures cannot have
the same handedness, so at most one view is right in game, and the preview will never be able to say which.
`ISSUE_REGISTER.md` N5 closed this in 2026-08-29 on the preview alone for the globe half.
**`install` and glass time are separate owner gates (C1.12) — this chat stops here.**

**Options.**
1. **Open a glass-time gate for one look:** on orbit, Cover → Earth view over a recognisable coastline,
   then NEXT VIEW → Map. Two seconds, and it settles both views for good. Requires an explicit owner go for
   `install` + glass time. *(Recommended.)*
2. **Preview-only preparation first** (no gate): add a **mirrored** stand-in texture and render both views
   against both textures, so the next glass session has a precise prediction to confirm rather than an open
   question. Cheap, and it makes option 1 shorter.
3. **Accept N5's closure and do nothing.** Not recommended: the evidence on record does not support it, and
   a mirrored Earth on the crew's home page is exactly the kind of defect that survives forever because
   everyone assumes it was checked.

**Recommendation: 2 now, then 1 when the owner next opens glass time.** They compose — 2 costs nothing and
makes 1 conclusive.

### Q3 — ENTRY ENABLED: crew verification, or vehicle arming? (C-08) — *this is S49 §8's Q2, and there is no ruling on record*

**Situation.** The Cover's step 2 row reads `ENTRY ENABLED  True  False` with **False** baked as the
selected value — permanently, on every phase, with no model behind it and two hit rects that do nothing.
S49 raised this as its Q2 and it has not been answered. The classification decides who fixes it: if the row
records *the crew verified entry is enabled*, it is a local latch and Part A can do it now; if it records
*the vehicle has armed entry*, it is an arming flag and stays §14.4(a) honest-no-op until Part B.

**Options.**
1. **Crew verification (Part A).** A latch beside `coverPhase` in the painter; the two hit rects become a
   two-state selector; the value is drawn as text, lit from the latch. Buildable today. The baked step text
   beside it — *"After SpaceX GO for deorbit, verify entry is enabled"* — reads as crew verification, which
   argues for this.
2. **Vehicle arming (Part B).** Both values drawn, neither hit-testable, the row dashes until the conductor
   supplies the flag. Honest, and consistent with §14.4(a); the crew's home page then carries a dash where
   the real screen carries a word.
3. **Both:** the row shows the vehicle's arming state (B) and a separate crew-ack latch records the
   verification (A). Closest to how a real go/no-go works, and the most build.

**Recommendation: 1**, on the strength of the adjacent step text — but this is the owner's read of the
reference, not a build chat's, and §1.4 puts a real-source confirmation above a plausible reading. Whichever
option is chosen, the value must stop being a PNG and must dash when there is no source.

### Q4 — The CAMERA caption is the one element in the rebalanced band with no mirror. Where should it go? (C-13 step 4)

**Situation.** R-2/R-3 make the band symmetric: NEXT VIEW and SETTINGS mirror each other at the slot edges,
and the two coordinate readouts sit equidistant either side of the globe. `CAMERA` / `Auto - Earth IO` is
left floating above the SETTINGS pill at the bottom-right (centred x 2411.2, y 1145.7), with nothing above
NEXT VIEW to answer it — the last thing in the band that is off-balance. It cannot simply be centred under
the globe: the disc's foot occupies 1475…2045 at that row.

**Options.**
1. **Move it above NEXT VIEW** (centred x ≈ 1115, same rows). The caption names the camera *state*, and
   NEXT VIEW is the control that changes that state — label above its own button. It also fills the empty
   upper-left of the band, which is currently the biggest hole in it. *(Recommended.)*
2. **Leave it above SETTINGS.** Conservative — it is where the community Figma baked it (`camera_auto_earth_io`
   at `{3032,1718,346,59}`) — but it labels a state SETTINGS has nothing to do with, and it leaves the band
   heavy on the right, which is the imbalance R-5 is pointing at.
3. **Give it its own centred row above the whole band**, at the slot centre, raised clear of the disc
   (y ≈ 1100 or above). Balanced and unambiguous, but it pushes the caption into the globe's airspace and
   costs vertical room the band does not have much of.

**Recommendation: 1.** It is the only option that both balances the band and puts the label with the control
it describes. It is a deviation from the baked Figma placement — but so are R-2 and R-3, and the owner has
already ruled on those, so this is the same class of decision and belongs in the same answer.

---

*Page 0 (Cover) inspected 2026-09-05; C-12 and C-13 added the same day on owner review (R-1…R-5).*

*⚠ **C-12 and C-04 are both `component_48` and both page-wide** — the smudge and the 12% stretch are on
every screen in the build, so whichever line takes them fixes fifteen pages, not one. They should be
scheduled together and ahead of the per-page sweep.*

---

# PAGE 1 — HUD (FRAME 58)

**Renders inspected (2026-09-05, re-rendered at HEAD `97f4c78` after W10/T15d/G10/BB7/G11/W5/W34 landed):**
`frame58_hud.png` · `frame58_hud_noseopen.png` · `frame58.png`. All 2560×1406 — **which is not the shipped
size; see H-01.**

**Source under inspection:** `plugin/src/pure/Frame58Hud.cs` (**55 lines, the whole page**) ·
`plugin/src/pure/FigmaUI.cs:324-329` (the only touch route) · `plugin/src/ScreenPainter.cs:1121-1157,
1309, 1347-1354` · `plugin/GameData/DragonScreen/DragonScreen.cfg:60,76,87`.

**S49's entry, and what the glass says about it.** S49 §2 rates the HUD *"Two live things — the nose-cone
flag and the docking-cam disc it gates. Every readout is baked (§1.3)"*, and §1.3 proves from source that
`s.Steps.NoseConeOpen` is the file's only `PageState` read. **All of that is confirmed at HEAD.** The glass
adds seven defects source alone could not show, and one — H-01 — that is not about this page at all but was
found by asking why this page's renders are 2560 px wide.

## What was checked and found CLEAN

1. **The `ox > 40f` guard agrees between the draw and the hit test.** `Frame58Hud.cs:44` and
   `FigmaUI.cs:327` both gate the Manual Docking affordance on the same letterbox width, so the control
   cannot be hit when it is not drawn. *(Their **rectangles** do not agree — that is H-04. The guard does.)*
2. **The two renderers agree on the missing-texture case.** With the nose open and no docking-cam texture,
   `ScreenPainter.Execute` skips the whole command — `if (img == null || imageMat == null) continue;`
   (`ScreenPainter.cs:1309`), which takes the circle mask with it — and `PreviewMain.DrawImage` returns
   early on the same condition, *"Skipped, not substituted — same rule as the GL painter."* Both draw
   nothing. This is genuine agreement, not luck: **H-09 is preview blindness, not renderer divergence.**
3. **`Frame58Hud.Commands = 20` is a correct budget.** Worst case is 11 commands: background, frame asset,
   `ImageCircle` + `TargetReticle.Crosshair` (3, per `TargetReticle.cs:14`), the four margin-button draws,
   and `component_48`. No overflow warning in the preview log.
4. **The bowl geometry is sound.** `BowlCx/Cy/R = 1706/984/470` places the disc at panel (1275.0, 655.1)
   r 312.9 — concentric with the frame's own baked bowl, verified by the nose-open diff landing exactly on
   its centre.
5. **The bottom-left attitude sphere is the design's clean synthetic instrument**, not the mirrored
   photographic navball the file header was written to get away from. The header's complaint is satisfied.
   *(It is also completely static — that is part of H-02, not a separate defect.)*

## Cross-page confirmations

Both of the Cover's page-wide `component_48` defects are **present and clearly visible on this page**, which
is the second of the fifteen pages that draw that bar:

- **C-12** (the un-erased marker glow): the smudge sits in the HUD's **left letterbox**, outside the frame
  art entirely — `frame58_hud.png` bottom-left.
- **C-04** (the 12.2% horizontal stretch): the bottom bar's crosshair icon renders as a visible ellipse.

No new finding is logged for either; they are the same two defects and should be fixed once.

---

## H-01 — The preview renders every Figma page at 2× the width the mod actually ships, and says so on the strength of a cfg value the cfg contradicts

**TIER 1** · **NEW** · ⚠ **this finding changes the severity of other findings in this document, including C-05**

**Evidence.** Three files in this repo, at HEAD:

| source | says |
|---|---|
| `plugin/GameData/DragonScreen/DragonScreen.cfg:60`, `:76`, `:87` | `screenWidth = 1280` — on **all three** screens |
| `plugin/preview/PreviewMain.cs:43-46` | `ScreenSpec` = 1280×703 / 1280×710 / 1280×703 — agrees with the cfg |
| `plugin/preview/PreviewMain.cs:572` (and `:583`, `:595`, `:609`, `:626`) | `int CW = W * 2, CH = H * 2;` — every Figma page rendered at **2560×1406** |
| `plugin/preview/PreviewMain.cs:569-571`, the justification | *"Render at 2x the screen size: the Figma assets carry 2px hairline borders that fall to sub-pixel (~0.7px) at 1280 and drop inconsistently; 2x keeps them crisp (the in-game RenderTexture should match — **screenWidth 2560 in the cfg**)."* |

**The cfg says 1280, three times.** The preview therefore renders the Figma pages at **four times the pixel
count** the mod ships, on the stated basis of a cfg value that does not exist in the cfg.

**What is wrong.** Two things, and the second is the serious one.

1. **The claim is stale or was never true.** Whichever, the comment asserts a fact about a file sitting in
   the same repo, and that file disagrees. Under C7.1 the repo copy is authoritative.
2. **The preview is the project's legibility gate, and it is judging at double resolution.** CLAUDE.md
   makes this explicit — *"judge layout/palette/legibility from `python plugin/build.py preview`"* — and
   `PreviewMain` states the governing principle for the font in as many words: *"If this and
   `PreviewMain.FontFamily` ever disagree, the preview is lying about the real page"* (`:1470-1471`,
   paraphrasing the cfg's own note at `DragonScreen.cfg:48`). **Nobody wrote the same rule for resolution,
   and resolution is where it broke.**

   Worse, the justification records the symptom and treats it: *"2px hairline borders … fall to sub-pixel
   (~0.7px) at 1280 and drop inconsistently."* That is a statement that **at the shipped width the design's
   hairlines drop out**. The response was to render the preview larger, not to fix the page or raise the
   cfg — so the preview was made to look right at a resolution the game does not use.

**What this does to findings already in this document.** Every text size in `CoverPage` is a design-space
number multiplied by `sc = h / 2112`. At the preview's h = 1406, `sc = 0.6657`. At the shipped h ≈ 703,
`sc = 0.3329` — **exactly half**. So:

| element | preview (h 1406) | shipped (h 703) | `Typography.Min` = 16 |
|---|---|---|---|
| **C-05** ENTRY TIMELINE rows | 15.3 px *(already under the floor)* | **7.7 px** | ❌ less than half |
| Reference-content card titles `Z(34)` | 22.6 px | **11.3 px** | ❌ |
| Cover rail labels `Z(32)` | 21.3 px | **10.7 px** | ❌ |
| CONTINGENCY / PARACHUTES rows `Z(26)` | 17.3 px | **8.7 px** | ❌ |
| **C-03** NEXT VIEW label `Z(53)` | 35.3 px | 17.6 px | ✅ (but still overruns its pill) |

`Typography.Min = 16f` was measured against the **legacy** pages, which render at the real 1280×703 and pass
`Typography.Caption` straight through as a panel size. So the floor is a 1280-panel floor, and at 1280
**almost nothing `CoverPage` draws as text clears it.**

**Fix plan.** The engineering is easy; **which way to fix it is the owner's — see Q5.** Both directions are
buildable and they are not equivalent:

- **If 1280 is right:** the preview must render the Figma pages at 1280×703 like everything else (delete the
  `* 2`), and the pages must then be made legible and hairline-safe at that size. That is real work —
  C-05's rebalance, a type-scale pass, and hairlines that survive a 0.33 scale — but it is work against the
  screen the crew actually has.
- **If 2560 is right:** the cfg's three `screenWidth` lines go to 2560 and the preview is already correct.
  That is a one-line change per screen, but it is an in-game rendering change: three RenderTextures at 4×
  the pixels, and the cfg's own note explains the height is *derived from the mesh*, so the aspect follows
  automatically and only the cost changes. **Confirming the cost needs glass time — an owner gate (C1.12).**

- **Either way, and regardless of which is chosen:** correct the comment at `PreviewMain.cs:569-571` so it
  states the cfg value that is actually there, and **write down the resolution rule the font already has** —
  the preview's render size must be derived from the cfg, not asserted in a comment beside it. Deriving
  `CW`/`CH` from `ScreenSpec` (which already matches the cfg) makes the two unable to drift again.
- **Must not break:** the legacy `Pages.Build` renders (`page0_flight.png` … `page4_settings.png`) are
  already at the real 1280×703 and must stay there; only the Figma-era renders are doubled.
- **Verify:** whichever way it goes, the Cover and HUD renders must come back at the cfg's own width, and
  C-05's re-measure must be taken from *that* render, not this one.

---

## H-02 — Every readout on the docking HUD is a pixel in a PNG; 8 of the 12 numbers contradict live state in the same frame

**TIER 1** · confirms S49 **H10** with rendered proof

**Evidence.** `frame58_hud.png`. `Frame58Hud.Build` is six draw calls and reads exactly one `PageState`
field, `s.Steps.NoseConeOpen` (`Frame58Hud.cs:32`) — verified at HEAD. Everything below is baked into
`frame58.png`:

| readout | on the glass | live in `PageState` that same frame | agrees? |
|---|---|---|---|
| ROLL | `15.0°` | `RollDegText "15.0°"` (`PreviewMain.cs:87`) | ✓ *(art and fixture were matched to each other)* |
| ROLL rate | `0.0 °/s` | `RollRateText "0.0 deg/s"` (`:84`) | ✓ |
| PITCH | `-20.0°` | `PitchDegText "0.1°"` | ✗ |
| PITCH rate | `0.0 °/s` | `PitchRateText "0.0 deg/s"` | ✓ |
| YAW | `-10.0°` | `YawDegText "0.1°"` | ✗ |
| YAW rate | `0.0 °/s` | `YawRateText "0.1 deg/s"` | ✗ |
| X | `200.0 m` | `OffXText "22.7 m"` (`:82`) | ✗ |
| Y | `12.0 m` | `OffYText "0.1 m"` | ✗ |
| Z | `30.0 m` | `OffZText "0.0 m"` | ✗ |
| RANGE | `202.6 m` | `RangeText "202.6 m"` (`:81`) | ✓ |
| RATE | `-0.031 m/s` | `RateText "-0.25 m/s"` | ✗ |
| ACCELERATION | `0.00g` | `AccelPosText "1.42"` (`:174`) | ✗ |
| FRAME | `LVLH` | *(no field)* | — |
| CAMERA | `Virtual` | `CameraResText` / `HullCams.Labels()` | ✗ |
| timer | `0s` | *(no field)* | — |

**Eight of twelve numeric readouts disagree**, and the three that agree do so only because the fixture was
authored to match the art (`PreviewMain.cs:83`: *"the same three errors in the glyph form the MANUAL docking
page prints"*). On a real approach none would track. The bottom-left attitude sphere is baked too, so the
page's attitude instrument never moves.

**What is wrong.** This is S49's *"largest liveness gap in the build"*, and it is worse in kind than the
Cover's: the Cover's baked strip is context, whereas these are the numbers a crew member flies a manual
approach on. A docking HUD reading a frozen `RANGE 202.6 m` and `RATE -0.031 m/s` while the vehicle closes
is not a placeholder, it is a wrong instrument.

**Fix plan.** Identical in method to C-01, and the two should share a build line.
- Overdraw at the frame's measured coordinates. Every value exists live and pre-formatted:
  `RollDegText`/`PitchDegText`/`YawDegText`, `Roll/Pitch/YawRateText`, `OffX/Y/ZText`, `RangeText`,
  `RateText`, `AccelPosText`; `HullCams.Labels()` supplies a real camera name for CAMERA.
- **Research the coordinates from `docs/UI_AUDIT.md`, never from a screenshot** — CLAUDE.md's standing rule,
  and the reason screenshot-derived pages *"came out wrong every time."* `DockingSimPage` already draws the
  same fields correctly and is the working template.
- **Design the no-target and no-feed looks in the same pass.** `RangeText`/`RateText`/`OffX*` all dash with
  no target, and the page currently has no way to show that — a frozen number is exactly what the dash
  exists to prevent.
- **FRAME `LVLH`** has no source and must not be invented (C1.4). Leave it baked, or dash it, until a source
  names the frame — record which, do not guess.
- **Must not break:** the nose-cone gate and the bowl geometry (see CLEAN 4).
- **Verify:** re-render with the fixture's real values and check every readout against the table above.

---

## H-03 — Four painted controls in the page's own button idiom have no hit rect anywhere

**TIER 1** · S75's defect class · extends S49 **H11** (which named three of the four)

**Evidence.** `frame58_hud.png`, and `FigmaUI.HitTest` (`FigmaUI.cs:324-329`), whose entire `UiPage.Hud`
branch is the letterbox margin. There is no other Hud touch route: `ScreenPainter`'s Figma branch has **no
`cur == UiPage.Hud` case at all**. So on this page exactly one rectangle is touchable, and these are painted:

| control | where | idiom |
|---|---|---|
| **`Local Pitch Mode`** | top centre | filled pill with a centred label — *not named by S49* |
| **`FAR FIELD POSITIONING`** | right column, under FLIGHT COMMANDS | bordered box **with a leading icon** — unmistakably a button |
| **`RESET`** | bottom right, in the timer box | bordered box, centred label |
| **`START`** | bottom right, in the timer box | bordered box, centred label |

Measured: the FAR FIELD POSITIONING box occupies y 144…213 in the right column; RESET/START sit inside the
timer box at y 1092…1260.

**What is wrong.** The page paints four things that look exactly like the buttons on every other screen in
this build and none of them can be touched. That is the half of S75's defect that is *worse than a no-op* —
a no-op at least resolves to a named action — and it is the same call S75 made for `gridicons_refresh`.

**But these four do not all belong in one bucket, and that is the whole fix plan:**

- **`FAR FIELD POSITIONING` is a GNC MODE COMMAND.** It commands the vehicle's pointing. Under §14.4(a) it
  stays an honest no-op until Part B wires it — **(B)**, and S49 H11 classes it the same way. It must not
  get a working hit rect in Part A.
- **`RESET` / `START` are a stopwatch.** Their whole effect is screen state — **(A)**, buildable now, and
  the same shape as the Cover's `RUNNING` clock (C-01's H2 half). The two should share one timer model.
- **`Local Pitch Mode` is a READOUT, not a control.** It names the current pointing frame, and the bottom
  bar prints `POINTING MODE / Sun + GEO` twelve inches below it. Under §14.4(f) it is a readout that must be
  filled — **(A)** — and it needs no hit rect at all.

**Fix plan.**
- **Now, and cheaply:** tint the two that will never be touchable in Part A — `FAR FIELD POSITIONING` (a
  Part-B command) — with `DragonPalette.Text6`, the *"nothing live behind this"* tint S75 established
  (`CoverPage.cs:215-218`), so it stops riding the button idiom. `Frame58Hud` cannot do this today because
  it draws the whole page as **one** asset: `frame58.png` is a single flat PNG, so there is no per-element
  tint to apply. **That is the real blocker and it is structural** — see the note below.
- **The structural fix, which H-02 already requires:** the page must stop being one baked PNG. Once the
  readouts are overdrawn (H-02), the same pass can overdraw the four controls, and then each gets its own
  tint and, where it is (A), its own hit rect drawn from one shared rectangle (`PageAction`'s rule — and
  see H-04 for what happens when that rule is not followed here).
- **Must not break:** `FAR FIELD POSITIONING` must **not** become touchable. §14.4(a) is explicit that flight
  actuation stays an honest no-op, and §14.4(f)'s scope line excludes it.
- **Verify:** after the overdraw pass, every painted control on the page is either in a hit table or drawn
  in the inert tint — and the check runs both ways, as the charter requires.

---

## H-04 — The Manual Docking affordance is drawn from one rectangle and hit-tested from another, in two different files, and they do not match

**TIER 1** · **NEW** · `PageAction`'s standing rule, broken across a file boundary

**Evidence.** The same control, specified twice:

| | source | vertical extent |
|---|---|---|
| **drawn** | `Frame58Hud.cs:44` — `by = h * 0.44f, bh = h * 0.12f` | **0.44 h … 0.56 h** |
| **hit** | `FigmaUI.cs:327` — `py >= h * 0.40f && py < h * 0.60f` | **0.40 h … 0.60 h** |

Horizontally they agree (`12 … ox − 12` in both). Vertically the hit region is **20% of panel height against
a 12% painted box** — an invisible halo of **0.04 h above and 0.04 h below**, which at the preview's 1406 px
is **56.2 px each way**, and at the shipped 703 px is 28.1 px each way.

**What is wrong.** A tap in the empty letterbox up to 56 px above or below the visible button silently
navigates to Manual Docking. It is the S54 defect class — a rectangle that fires where nothing is painted —
and here the cause is plainer than S54's: **the rectangle is written out twice, in two files, in two
different modules, with different constants.** `PageAction`'s rule exists for exactly this — the Cover obeys
it with `NextViewRect`, `PadRect` and `CapsuleRect`, each shared by the draw, the hit test and the tests
(`CoverPage.cs:255-259`, `:261-279`, `:363-374`). The HUD's one control is the page that does not.

**Fix plan.**
- Give the affordance a single public rect function on `Frame58Hud` — `MarginRect(w, h, out x, out y, out
  rw, out rh)`, returning `false` when `ox <= 40f` — and have **both** `Frame58Hud.Build` and
  `FigmaUI.HitTest` call it. That is the Cover's own pattern, lifted directly.
- **Pick the drawn box, not the hit band**, as the shared truth: the crew can only aim at what they can see,
  and a control that fires outside its border is the defect. If the button is too small to hit comfortably
  at 1280 (H-01), the answer is to **draw it bigger**, not to keep a secret halo.
- ⚠ **The same construction is used twice more** and should be checked in the same pass: `UiPage.Docking`'s
  RENDEZVOUS affordance (`FigmaUI.cs:352-355`) uses the identical `py >= h*0.40f && py < h*0.60f` band, and
  `DockingSimPage` draws its own margin button. Those are pages 27 and 28 and will be inspected in turn, but
  if this is fixed generically it should be fixed for all three at once.
- **Must not break:** the `ox > 40f` guard must survive on both sides (CLEAN 1), and the Menu grid must
  remain a second route to Docking — see the note in H-04's sibling below.
- **Verify:** a headless check that the drawn rect and the hit rect are the same rect, for both the HUD and
  Docking margin affordances. That check is what would have caught this.

⚠ **Related, and worth recording rather than logging separately:** because both the draw and the hit are
gated on `ox > 40f`, a panel whose aspect leaves a letterbox of 40 px or less has **no route from the HUD to
the Docking page at all**. At the shipped 1280×703 the letterbox is 69.6 px, so it holds today — but with
only 29 px of margin, and the guard is a hard cliff, not a taper. The Menu grid remains a second route, so
this is a robustness note, not a defect.

---

## H-05 — The docking HUD has a titled ALERT ACTIVITY panel, 822 px tall and permanently empty, while the alarm channel is computed every frame and written to the black box

**TIER 1** · **NEW** · the constructive answer to S49 **H7**

**Evidence.** `frame58_hud.png`, right column (x 2105…2415). Bright-ink row bands, measured:

```
   y   84.. 98   FLIGHT COMMANDS   (heading)
   y  111        rule
   y  144..213   FAR FIELD POSITIONING  (the one command)
   y  241..256   ALERT ACTIVITY    (heading)
   y  269        rule
   ---- 822 px of nothing ----
   y 1092..1260  the 0s / RESET / START timer box
```

**822 px of empty column on a 1406 px page** — 58% of the page height — under a heading that promises alerts.

Meanwhile, at HEAD:

- `ScreenPainter.cs:1121` — `chrome.AlertMask = Alarms.Mask(ps);`
- `ScreenPainter.cs:1123` — `chrome.VehicleState = ps.Valid ? Alarms.Word(Alarms.SystemSeverity(ps)) : "NO DATA";`
- both computed **immediately before** `if (FigmaMode)` at `:1127`, and `ChromeBar.Build` — the only consumer
  that would put them on the glass — is at `:1194`, inside the `else`. **So they are still discarded from
  every screen**, exactly as S49 §1.1 found.
- **What has changed since S49:** `ScreenPainter.cs:652-653` now feeds `rec.AlarmMask = Alarms.Mask(ps)` and
  `rec.SevSystem` into the BlackBox recorder (BB1). So the alarm state is **computed, and recorded to disk,
  and never shown to the crew.**

**What is wrong.** S49 H7 says the Cover has no alarm surface and proposes building one there. This page
**already has one, designed, titled and sized** — and the fix has a home that needs no new layout invented
and no §1.4 source question, because the panel and its heading are in the reference design. `Alarms.cs`'s
own header is quoted in S49: *"THE ALERT ROUTING IS THE POINT, NOT THE DECORATION."*

**Fix plan.**
- Draw an alert **list** into the ALERT ACTIVITY panel from `Alarms.Mask(ps)` + `SystemsState`: one row per
  set bit, tinted by `Alarms.SystemSeverity`, with the severity word from `Alarms.Word`. `StatusIndicator`
  and `VehicleTabBar` already render severity, so the colour language exists.
- **Route the existing value rather than inventing a channel.** `chrome.AlertMask` is computed on the line
  before the Figma branch; it needs to reach `FigmaUI.Build`, not a second `Alarms` call — one state, one
  source, which is T14's rule and the reason the systems tree and the console plate share a dispatcher.
- **Scope, per §14.4(f):** an alert list built from `Alarms` + `Systems` (G-force, propellant, power, fire,
  leak, tripped strings, bus 0/3) is **(A)** and buildable now. An alert list that expects *real FDIR faults*
  is **(B)** — the stub pins `Fault`/`FaultResponse`/`FaultText` (S49 §1.2). Build the (A) half; leave a
  socket for the (B) half.
- ⚠ **`Alarms.Mask` bit 2 (NAV) is never set** — S49 H7 records this; bits 0/1/3 are. Harmless while the
  channel is discarded; the moment it reaches this panel, one of four categories is silently dead. **Fix
  that in the same pass or the new panel ships with a known hole.**
- **Empty state:** with no alarms the panel must say so — a `NOMINAL` row computed from
  `Alarms.SystemSeverity`, or an explicit "no active alerts". **Not a blank panel, and not a hardcoded green
  word** (S31/S32): the verdict is computed or it is not drawn.
- **Must not break:** nothing on this page is actuation, so §14.4(a) is not engaged. The BlackBox recorder's
  reads at `:652-653` must keep working unchanged.
- **Verify:** re-render with a fixture that trips one alarm of each category and check one row per bit,
  correctly tinted; then with a clean fixture and check the computed empty state.

---

## H-06 — The MANUAL DOCKING label overflows its own box on both sides

**TIER 2** · **NEW**

**Evidence.** `frame58_hud.png`, left letterbox, magnified. Measured at 2560×1406:

- box: **x 12.0 … 127.3** (`bx = 12f`, `bw = ox − 24f`, `ox = 139.29`)
- `MANUAL` (white): ink spans **x 14 … 140** — **overflows the right border by 12.7 px**
- `DOCKING` (accent): ink spans **x 7 … 132** — **overflows on BOTH sides**, 5 px each way

Both labels are set at `ts = h * 0.020f` = 28.1 px (`Frame58Hud.cs:45-48`) and centred on `bx + bw * 0.5`.

**What is wrong.** The label size is derived from the panel **height** while the box width is derived from
the **letterbox width** — two unrelated quantities — so the text has no relationship to the box it sits in.
`DOCKING` needs ~125 px and gets 115.3. This is the owner's standing per-page QC line (*every string inside
its box, no clipping*) failing on the page's **only** interactive control, and the aspect-dependence means
it gets worse on a narrower panel until `ox` drops to 40 and the button vanishes entirely (H-04's note).

**Fix plan.**
- Size the type from the **box**, not from the panel: pick `ts` such that the wider of the two labels fits
  `bw` with a margin — measure `DOCKING` against `bw` and scale down when it does not fit, the same shape as
  `CoverPage.FitRows` but for one axis. ⚠ **And apply the `Typography.Min` floor in PANEL space** — C-05 is
  the same bug in the other direction and both should learn from one helper.
- If the resulting type is below the floor, the box is too small and must be **widened or re-oriented** —
  stacking the two words is already the design; a third option is rotating the label to run up the margin,
  which is what the margin's shape actually suits.
- **Must not break:** H-04 makes the drawn rect the shared truth, so widening the box widens the hit target
  too — which is the point. Do H-04 first, then this, or the two will fight.
- **Verify:** re-render at **both** 1280 and 2560 (H-01) and require both labels' ink to sit strictly inside
  the box on each.

---

## H-07 — Two different fit strategies on one page: the frame art is letterboxed, the bar is stretched full width, so the frame's own border becomes a rule in the middle of the bar

**TIER 2** · **NEW** · borders over borders

**Evidence.** `frame58_hud.png`, bottom-left corner, magnified. Three border treatments collide:

1. a **hard vertical white line the full height of the page at x = 139.3** — the frame art's own left edge
   (`ox`), drawn as a visible stroke;
2. the frame's **rounded bottom-left corner**, starting at (141.6, 1265.6) and meeting a horizontal border
   at y ≈ 1317.8 — i.e. *inboard* of the page edge, with a triangular sliver of lighter navy trapped
   between it and the bar;
3. `component_48`'s own **rounded corner at the true page edge**, x 40…77, y 1312…1320.

Two rounded corners, ~100 px apart horizontally, in the same band.

**What is wrong.** `Frame58Hud.Build` uses **two incompatible fits in six lines**: the frame art is
fit-to-height and centred with a letterbox (`ox`, `Frame58Hud.cs:27,30`), and `component_48` is drawn at
`0f … w` — full panel width, ignoring `ox` (`Frame58Hud.cs:53`). In the design the bar sits flush under the
frame's own rounded panel; here it runs 139 px past it on each side, so the frame's left border stops being
an edge and becomes a rule crossing the bar's span, and the design's single corner becomes two.

`component_48.png`'s top 105 rows are transparent (measured: only 130 of its 235 rows are fully opaque), so
the frame's border shows *through* the bar's upper band rather than being covered by it — which is why the
collision is visible at all.

**Fix plan.** The Cover solved exactly this and its solution is the model, with one correction:
- **Draw the bar in the frame's own coordinate system**, at `ox … ox + RefW*sc`, so it is flush with the
  frame's rounded panel as designed, and fill the two letterbox strips with the page background. The frame's
  corner and the bar's corner then coincide, as one corner.
- ⚠ **Do NOT copy the Cover's `Wd` straddle-stretch** — that is C-04, and it is the reason the bar's icons
  are 12% wide. This page should take the *uniform* fix C-04 proposes, not the Cover's current behaviour.
- **Must not break:** `FigmaUI.BottomBarHit` (`FigmaUI.cs:117-131`) maps touches by
  `BarIconX[i] / RefW * w` — full panel width — so it currently matches this page's stretched draw. Moving
  the bar to `ox`-relative coordinates **moves every nav icon's touch target**, and the hit map must change
  in the same commit. This is the same coupling C-04 flags; **the two are one fix, not two.**
- **Verify:** re-render; the frame's bottom-left corner and the bar's must be the same corner, with no
  vertical rule at `ox` and no trapped sliver.

---

## H-08 — The frame art is exported at 0.6× design scale, so at the preview's resolution it is drawn upscaled and measurably soft

**TIER 2** · **NEW** · ⚠ **conditional on H-01 / Q5**

**Evidence.** `frame58.png` is **2048×1263** for a **3427×2112** design — a scale factor of **0.5976**. Every
Cover asset, by contrast, is exported at 1.0× design scale. Drawn at the preview's 2560×1406 the frame is
placed at 2281.4×1406, i.e. **upscaled 1.114×**.

Measured edge sharpness on the same render (mean gradient at edge pixels, normalised by local contrast —
higher is sharper):

| region | source asset | drawn scale | normalised sharpness |
|---|---|---|---|
| `FLIGHT COMMANDS` | frame58.png | **×1.114 (up)** | **0.314** |
| `FAR FIELD POSITIONING` | frame58.png | ×1.114 (up) | **0.315** |
| `CURRENT STATE` | component_48.png | ×0.747 (down) | 0.365 |
| `Far Field Pointing Deorbit` | component_48.png | ×0.747 (down) | 0.357 |

The frame art's edges are **≈14% softer** than the bar art on the same page, consistent with an 11% upscale.

**What is wrong — stated honestly.** At the **shipped** 1280×703 (H-01), the frame is drawn at 1140.7 px, a
*downscale* of 0.557, and this defect does not exist. **So H-08 is only a defect if Q5 resolves to 2560** —
and if it does, it is a defect on the one page that is *entirely* baked art, where softness has nowhere to
hide. `frame59.png` (2048×1262) and `frame66.png` (2048×1263) are the same export scale and the same pages
(`FigmaFramePage`), so this is a three-page finding, not one.

**Fix plan.** Do nothing until Q5 is answered. Then:
- **If 1280:** close this as not-a-defect and record why, so it is not re-logged.
- **If 2560:** re-export `frame58/59/66.png` at 1.0× design scale (3427×2112) from the community Figma. That
  needs the export — the same dependency as Q1 — and it costs disk: three PNGs at ~2.8× the pixels. Note the
  re-export in `docs/ASSET_INDEX.md`.
- **The durable fix underneath either answer** is H-02: a page assembled from elements rather than one flat
  PNG does not have a single global resolution to get wrong.
- **Verify:** re-measure normalised edge sharpness against the bar art on the same render; the two should
  land within ~5%.

---

## H-09 — The preview cannot render the page's only live feature

**TIER 2** · **NEW** · preview blindness, *not* renderer divergence (see CLEAN 2)

**Evidence.** Diffing `frame58_hud.png` against `frame58_hud_noseopen.png`:

```
differing pixels: 1109 of 3,599,360  (0.0308%)
bbox  x 1231..1319  y 612..699        (88 x 88 px — the crosshair, and nothing else)
```

The docking-cam disc is at **x 962…1588, y 342…968** — 626 px across. It is **entirely absent**, and so is
the `BowlBlue` corner mask that `Frame58Hud.cs:36` passes with it. So the page's one live behaviour renders
as *"a crosshair appeared."*

**Why.** `ImageId.DockingCamLive` is a runtime image and the preview gives a stand-in to only two —
`BodyMap` and the navball — for stated reasons (`PreviewMain.cs:1510-1530`). Everything else is skipped
because *"a preview that flatters us is worse than none."* That principle is right, and the same file
already records the exception that proves it: the body map gets a stand-in *"because the GAME always has
one … and only the PREVIEW cannot."*

**What is wrong.** The docking camera is in exactly that category — the game has a real feed
(`DockingCamRenderer`, claimed at `ScreenPainter.cs:1131`), only the preview cannot. So the page's single
live feature, the whole point of the nose-cone gate, **has never appeared on a preview render**, and the
preview is CLAUDE.md's stated instrument for judging the glass without spending a restart.

**Fix plan.**
- Give `ImageId.DockingCamLive` a stand-in, on the same footing and for the same stated reason as `BodyMap`:
  read from `assets/`, never shipped, and **visibly a stand-in** — a marked test pattern rather than a
  photograph, so no reader mistakes it for the real feed. A grid or bore-sight card also makes the circular
  clip and the `BowlBlue` corner mask checkable, which is the geometry the disc exists to exercise.
- **Must not break:** the missing-texture path must still render as it does now — that is the *in-game*
  no-feed look and it agrees with the GL painter today (CLEAN 2). Keep a render of both states.
- ⚠ **The no-feed look is itself undesigned.** With the nose open and no camera, the crew gets the baked
  bowl plus a crosshair and no indication the feed is missing — the same gap S49 H10 flags for the no-target
  case. `NavPage` already has the pattern: `PlanetGeom.NoSignalLabel` marks a disc that is not a live render
  (`NavPage.cs:498-503`). **Reuse it here rather than inventing a second marking.**
- **Verify:** three renders — nose closed, nose open with the stand-in, nose open with no feed — all three
  visibly distinct.

---

## Open questions for the owner — HUD (Q5)

### Q5 — The preview renders the Figma pages at 2560 wide; the shipped cfg says 1280. Which is authoritative? (H-01)

**Situation.** `DragonScreen.cfg` sets `screenWidth = 1280` on all three IVA screens (`:60`, `:76`, `:87`).
`PreviewMain`'s own `ScreenSpec` table agrees (1280×703 / 1280×710 / 1280×703). But every Figma-era page is
rendered at `W*2, H*2` = 2560×1406, justified in a comment that says *"the in-game RenderTexture should
match — screenWidth 2560 in the cfg."* It is 1280. So the preview — which CLAUDE.md names as the instrument
for judging layout and legibility without spending a restart — has been judging at four times the shipped
pixel count. The comment also records *why* the doubling was introduced: the design's 2 px hairlines *"fall
to sub-pixel (~0.7px) at 1280 and drop inconsistently"* — i.e. **at the shipped width the design's hairlines
drop out.** At 1280, `CoverPage`'s body text lands at 7.7–11.3 px against a measured 16 px legibility floor.

**This changes the severity of findings already filed** (C-05 above all), so it should be answered before
any legibility work is scheduled.

**Options.**
1. **1280 is authoritative — fix the preview and then fix the pages.** Delete the `* 2`, then do the real
   work the doubling was hiding: a type-scale pass on the Figma pages, C-05's rebalance, and hairlines that
   survive a 0.33 scale. Most work, but it is work against the screen the crew actually has, and it makes
   the preview honest immediately. *(Recommended — see below.)*
2. **2560 is authoritative — raise the cfg.** Three one-line changes; the preview is then already correct
   and the hairline problem disappears. But it is an in-game rendering change: three RenderTextures at 4× the
   pixels, in an IVA that already runs RSS-RO. The cfg derives height from the mesh, so aspect follows
   automatically and only cost changes. **Confirming the cost needs `install` + glass time — an owner gate
   (C1.12), which this chat cannot open.**
3. **Split the difference** — e.g. 1920 — trading some cost for some legibility. Cheap to try, but it picks
   a number no source names and leaves the same class of question open at the new value.
4. **Leave both as they are and annotate.** Not recommended: it keeps a gate that reports pass at a
   resolution the product does not ship, which is the failure `PreviewMain`'s own font rule was written to
   prevent.

**Recommendation: 1, with 2 as the fallback if glass time shows 1280 is genuinely unreadable in the seat.**
Reasoning: option 1 needs no gate and can start today, and it surfaces the real defect — pages designed at a
scale the screen does not have — rather than moving the screen to fit the pages. Option 2 is a legitimate
answer, but it cannot be *validated* without glass time, and buying legibility with a 4× RenderTexture cost
in an RSS-RO install is a trade only the owner can make.

**Either way, one part is not optional and needs no decision:** correct the stale comment at
`PreviewMain.cs:569-571`, and **derive** the preview's render size from `ScreenSpec` (which already tracks
the cfg) instead of asserting it in prose — so the preview and the cfg cannot silently disagree again. The
font already has this rule written down; resolution never did.

---

*Page 0 (Cover) inspected 2026-09-05; C-12 and C-13 added the same day on owner review (R-1…R-5).
Page 1 (Hud / Frame 58) inspected 2026-09-05 at HEAD `97f4c78`.*

*⚠ **Three findings are page-wide, not per-page, and should be scheduled ahead of the sweep:***
- ***H-01** — the preview's resolution. It decides how every later legibility finding is measured, so
  everything after this page is provisional until Q5 is answered.*
- ***C-12 + C-04** — `component_48`'s un-erased marker glow and its 12.2% horizontal stretch. Both confirmed
  on the HUD as well as the Cover; both are on all fifteen pages that draw the bar; **and H-07 is coupled to
  C-04 through `FigmaUI.BottomBarHit`, so all three touch one hit map and belong in one commit.***

*Next page: **UiPage 2 — Audio (settings)**, which S49 §2 records as display-only with its eight ± buttons
and two fan buttons **drawn with no HitTest in the file at all** — the same both-directions wiring question
this page just failed, on a page built entirely of controls.*
