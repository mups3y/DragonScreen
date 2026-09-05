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

> ## ⟳ H-01 IS FIXED, AND THE ORIGINAL SWEEP WAS CONDUCTED ON THE BROKEN INSTRUMENT
>
> **S100 (`7957d4d`, 2026-09-05)** made the preview derive its size from `DragonScreen.cfg` and rendered it
> incapable of any other. Every PNG is now **1280×703**, and `build/preview/MANIFEST.txt` records each one
> with its size, written fresh each run into an emptied folder.
>
> **Everything in this file above this line was measured at 2560×1406 — twice the shipped width in each
> axis.** That does not touch the correctness findings: a readout that contradicts live state does so at any
> size, as do dead hit rects, duplicate pages, hardcoded colours and coordinate-system mismatches. It does
> touch every absolute pixel figure and every legibility judgement.
>
> **Fourteen scale-dependent findings were re-validated on the honest preview on 2026-09-05** — C-03, C-04,
> C-05, C-13, H-06, H-07, H-08, A-03, A-04, A-06, AS-02, DB-01, NO-01, MP-03. Each now carries a dated
> **⟳ RE-VALIDATED** block giving the old figure beside the new one and a verdict of STANDS / CHANGED /
> VANISHED. **The original text of every finding is preserved unedited** — a reader must be able to see what
> was measured on the broken instrument and what changed when it was fixed. The batch summary is at the end
> of this file, and the pass added one new finding, **R-01**.
>
> ⚠ **Read a pre-2026-09-05 pixel figure in this file as "twice the shipped value" unless a ⟳ block says
> otherwise.** Ratios and proportions were unaffected and are stated as such.

**Open questions raised** (full text at the end of each page's section):
**Q1** stray arrow placement (C-02) · **Q2** globe/map handedness, glass-gated (C-09) · **Q3** ENTRY ENABLED
class (C-08) · **Q4** where the CAMERA caption goes (C-13) · **Q5** which screen resolution is authoritative
(H-01) · **Q6** do the Figma audio faders survive the 2026-08-06 no-volume-sliders decision (A-02)
— *Q5 is the one that changes other findings' severity; answer it first.*

⚠ **Q1, A-05 and A-06 all wait on the same thing: the community Figma export** (`assets/figma/` is
gitignored, so it is not in the repo, C7). One retrieval unblocks three findings across two pages.

---

## PAGE INVENTORY — the worklist

The 35 `UiPage` values (`plugin/src/pure/FigmaUI.cs:21-80`, names from `Titles` at `:135-143`), plus the
Cover's seven phase views, plus the lower analog console panel. **The six Vehicle subsystem sub-tabs are
`UiPage` 20–25** and are listed in place rather than duplicated.

| # | UiPage | title | status | date |
|---|---|---|---|---|
| **0** | **Cover** | COVER | ✅ **DONE — 13 findings** *(C-12, C-13 added on owner review)* | 2026-09-05 |
| **1** | **Hud** | ATTITUDE HUD (Frame 58) | ✅ **DONE — 9 findings** | 2026-09-05 |
| **2** | **Audio** | AUDIO SETTINGS | ✅ **DONE — 6 findings** | 2026-09-05 |
| **3** | **Procedure** | PROCEDURE (Frame 59) | ✅ **DONE — 5 findings** *(shared section with page 4)* | 2026-09-05 |
| **4** | **Cabin** | CABIN (Frame 66) | ✅ **DONE** *(same section — one source file)* | 2026-09-05 |
| **5** | **Menu** | MENU | ✅ **DONE — 2 findings** | 2026-09-05 |
| 6 | PhaseDeport | DEORBIT BURN | ✅ **DONE** *(placeholder — see M-02)* | 2026-09-05 |
| 7 | PhaseCoast | COAST TO TRUNK JETTISON | ✅ **DONE** *(placeholder — see M-02)* | 2026-09-05 |
| 8 | PhaseClaw | CLAW SEPARATION | ✅ **DONE** *(placeholder — see M-02)* | 2026-09-05 |
| 9 | PhaseManual | MANUAL CHUTE | ✅ **DONE** *(placeholder — see M-02)* | 2026-09-05 |
| 10 | ActOnSpaceX | ON SPACEX — GO | ✅ **DONE** *(placeholder — see M-02)* | 2026-09-05 |
| 11 | ActDeorbitBrief | DEORBIT BURN BRIEF | ✅ **DONE** *(placeholder — see M-02)* | 2026-09-05 |
| 12 | ActReview | REVIEW REFERENCE | ✅ **DONE** *(placeholder — see M-02)* | 2026-09-05 |
| 13 | ActAcknowledge | ACKNOWLEDGE | ✅ **DONE** *(placeholder — see M-02)* | 2026-09-05 |
| 14 | Entry | ENTRY GO / NO-GO | ✅ **DONE** *(placeholder — see M-02)* | 2026-09-05 |
| **15** | **Vehicle** | VEHICLE OVERVIEW *(tab: All)* | ✅ **DONE — 3 findings** | 2026-09-05 |
| **16** | **SuitCheck** | SUIT LEAK CHECK | ✅ **DONE — 2 findings** *(the build's best page)* | 2026-09-05 |
| **17** | **VehicleMech** | MECH PANEL *(tab: Mech)* | ✅ **DONE — 3 findings** | 2026-09-05 |
| **18** | **AudioVideo** | VIDEO SETTINGS | ✅ **DONE — 2 findings** *(shared section with 19)* | 2026-09-05 |
| **19** | **VrioTest** | TEST VRIO HEALTH LEDS | ✅ **DONE — 1 finding** *(+ F-01)* | 2026-09-05 |
| **20** | **VehicleCrew** | VEHICLE — CREW *(sub-tab)* | ✅ **DONE — 4 findings** *(shared section, 20–25)* | 2026-09-05 |
| **21** | **VehiclePropulsion** | VEHICLE — PROP *(sub-tab)* | ✅ **DONE — 4 findings** *(shared section, 20–25)* | 2026-09-05 |
| **22** | **VehiclePower** | VEHICLE — POWER *(sub-tab)* | ✅ **DONE — 4 findings** *(shared section, 20–25)* | 2026-09-05 |
| **23** | **VehicleAvionics** | VEHICLE — AVIONICS *(sub-tab)* | ✅ **DONE — 4 findings** *(shared section, 20–25)* | 2026-09-05 |
| **24** | **VehicleGnc** | VEHICLE — GNC *(sub-tab)* | ✅ **DONE — 4 findings** *(shared section, 20–25)* | 2026-09-05 |
| **25** | **VehicleThermal** | VEHICLE — THERMAL *(sub-tab)* | ✅ **DONE — 4 findings** *(shared section, 20–25)* | 2026-09-05 |
| **26** | **ManualChute** | MANUAL CHUTE DEPLOY | ✅ **DONE — 2 findings** | 2026-09-05 |
| **27** | **Docking** | MANUAL DOCKING | ✅ **DONE — 3 findings** *(shared section with 28)* | 2026-09-05 |
| **28** | **Rendezvous** | RENDEZVOUS | ✅ **DONE — 1 finding** | 2026-09-05 |
| **29** | **DeorbitBurnPrep** | DEORBIT BURN PREP | ✅ **DONE — 3 findings** *(shared section with 30)* | 2026-09-05 |
| **30** | **EntryProcedure** | ENTRY | ✅ **DONE** *(same section)* | 2026-09-05 |
| **31** | **SystemsTree** | SYSTEMS TREE | ✅ **DONE — 2 findings** *(shared section with 32)* | 2026-09-05 |
| **32** | **SystemsPid** | SYSTEMS P&ID | ✅ **DONE** *(same section)* | 2026-09-05 |
| **33** | **Ascent** | ASCENT / LAUNCH | ✅ **DONE — 2 findings** *(shared section with 34)* | 2026-09-05 |
| **34** | **NavOrbitPlot** | NAV / ORBIT PLOT | ✅ **DONE — 2 findings** | 2026-09-05 |

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
| Console plate — rest / armed / fired / inert-swap | ✅ **DONE — 0 findings** | `panel_rest.png`, `panel_armed.png`, `panel_fired.png`, `panel_inert_swap.png` | 2026-09-05 |

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


### ✅ FIXED 2026-09-05 — S105 (QC batch 3)

**Fixed for seven of the eight.** New `CoverPage.DrawTopStrip` draws `ACTIVE PHASE`, `SPLASHDOWN TIME`,
`INERTIAL VELOCITY`, `ALTITUDE`, `APOGEE`, `PERIGEE` and `INCLINATION` live, as text at the baked assets'
own measured boxes (read via `BoxOf`, so the strip cannot drift if a placement is re-measured). The seven
keys joined `SkipKeys`. Metrics measured off the PNGs replaced: all left-aligned, caption cap rows 7..26,
value cap rows 56..97 (47..77 for the 89-tall `active_phase`).

The dash rules are `ManualChuteDeployPage`'s, reused so the two pages cannot disagree — and on the current
fixture `SPLASHDOWN TIME` correctly renders a dimmed **—** rather than a number.

**On the glass:** `ACTIVE PHASE ORBITING · INERTIAL VELOCITY 2280 m/s · ALTITUDE 123.6 km · APOGEE 124.0 km
· PERIGEE 121.9 km · INCLINATION 51.60°` — every one matching the `PageState` the globe beside it is drawn
from. The six-of-seven contradiction is gone.

⛔ **`running_00_22_57` stays baked, and that is H-2, not an oversight.** The label reads as *time in the
current phase*; nothing keeps a phase-entry timestamp, and `VesselData.Met` — the only clock that exists,
and one that does not reach `PageState` — is a different quantity. Drawing MET there would replace a frozen
wrong number with a **live** wrong one, which is worse because it would look right.
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


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `ui_cover.png`, 1280×703 (`MANIFEST.txt`).  **Verdict: CHANGED — worse in the way that
matters.**

| | filed (2560×1406) | now (1280×703) |
|---|---|---|
| pill | x 1277.2…1544.1, w 266.9 | **x 638.6…772.1, w 133.5** |
| label ink | 1367…1551 = 184 px | **683…779 = 96 px** |
| room (label start → border) | 180.4 px | **90.2 px** |
| overrun | **7.0 px = 3.9% of the label** | **6.9 px = 7.2% of the label** |

The room halved exactly; **the label did not** — GDI+ at half the size returns a proportionally wider
string (96 px where a linear halving gives 92). So the absolute overrun is unchanged at ~7 px while
everything around it halved: **in relative terms the defect roughly doubled.**

⚠ **The border is present and correctly placed — this is NOT S101's hairline dropout.** Luminance profile
across the pill's right cap at y=620 (page ground 21, pill fill 42):

```
x   760-763  764-765  766-769  770-772  773-776  777-778  779+
lum   42       255      42       255      21       255      21
```

The white at **770-772 is the border**. The white at **764-765 and 777-778 are the two stems of the final
"W"** — one inside the pill, one outside it on the page ground. The glyph straddles a border that is where
it should be. *(The pill's left cap does show S101's symptom separately: a single pixel at x=639 rendering
at luminance 221 rather than 255, because `Stroke(sc,2)` = 0.67 px clamps to 1. That is S101's, not C-03's.)*

⚠ **The filed fix no longer works and must not be built as written.** It proposed setting the label at the
SETTINGS twin's `Z(37)`. At the shipped width that is **12.3 px** — below `Typography.Min` = 16, so it
trades an overrun for an unreadable label. The pill must get wider, or the label shorter, or both; see
**R-01**, which is the general form of this problem.

### ✅ FIXED 2026-09-05 — S105 (QC batch 3)

**Fixed — and the filed fix plan was wrong, which the re-validation had already half-caught.**

The plan said to set the label at the SETTINGS twin's own `Z(37)`. At the shipped width that is **12.3 panel
px, below `Typography.Min` = 16** — it would have traded an overrun for an unreadable label. The lever it
missed is the **inset**: 130 is SETTINGS' own margin, and SETTINGS' label is 140 design px wide where this
one is ~288.

The cluster moved inward instead — dash `Z(36)`→`Z(30)`, label `Z(130)`→`Z(102)`, size `Z(53)`→`Z(50)`.
Measured on the render: runs at 501–515 (dash), 526–615 (label), 620–623 (the border) — the label ends
**5 px clear** of the border, and `Z(50)` is **16.6 panel px, above the floor**.

⚠ **First fix in the sweep to bump into R-01.** It was solved without shrinking anything, but the next label
that does not fit will not have that escape.
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


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `ui_cover.png`, `frame58_hud.png`, `ui_menu.png`, 1280×703.  **Verdict: STANDS, exactly.**

The stretch is a ratio of two scales that both halved, so it is unchanged to four decimal places:

| | filed | now |
|---|---|---|
| x-scale (`w / RefW`) | 0.7470 | **0.3735** |
| y-scale (`h / RefH`) | 0.6657 | **0.3329** |
| **horizontal stretch** | **12.2%** | **12.2%** |
| rendered crosshair | 48 × 42 px, ratio 1.143 | **23 × 21 px, ratio 1.095** |

The asset's crosshair is still exactly square (130 × 130). The measured render ratio moved from 1.143 to
1.095 only because the icon is now 21 px tall and a ±1 px quantisation is worth 5% at that size; the
arithmetic (1.122) is unchanged.

⚠ **One thing the honest render adds:** the bar's icons are now **21 px tall**, so the 12% distortion is
harder to *see* — but the icons are correspondingly harder to read at all. The fix is unaffected; the
coupling to `FigmaUI.BottomBarHit` still holds.

### ✅ FIXED 2026-09-05 — S103 (QC batch 1)

**Fixed by giving the bar one geometry.** New `plugin/src/pure/BottomBar.cs` owns the bar's rectangle, draw,
hit map and marker; all **21 draw sites** now call `BottomBar.Draw(dl, w, h)`, and `FigmaUI.BottomBarHit` /
`BottomBarMarker` delegate to the same file. The bar is drawn undistorted in the design frame's own box,
`ox … ox + RefW·sc`.

| | before | **after** |
|---|---|---|
| bar x-scale vs y-scale | 0.3735 vs 0.3329 — **12.2% stretch** | **equal, by construction** |
| rendered crosshair (asset is 130×130) | 23 × 21, ratio 1.095 | **21 × 21, ratio 1.000** |

⚠ **The hit map moved with the draw, and the tests caught a third copy of the old mapping.**
`FigmaUINavTest`'s own "menu bottom-bar → Cover (back)" probe computed the icon centre as
`(46f + 40f) / RefW * W` and silently stopped landing on the bar once the draw was un-stretched. Both it and
the main bar probe now derive from `BottomBar.Rect`, so they prove the hit map agrees with the **draw**
rather than with a copy of itself.

**New fence:** `FigmaUINavTest.BottomBarUndistorted` asserts, at four panel sizes, that the bar's x-scale
equals its y-scale and that every icon's **drawn** centre is a hit on its own index.
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


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `ui_cover_phase5.png`, 1280×703.  **Verdict: CHANGED — much worse, and it is no longer one
card's problem.** S100's re-assessment said "worse than filed"; here is the measurement.

`FitRows`' arithmetic is design-space and unchanged (card 1: avail 193, need 218, k 0.8853 → design 23.02).
What changed is what that design size becomes on the shipped panel:

| card | design size | filed (panel px @2560) | **now (panel px @1280)** | vs the 16 px floor |
|---|---|---|---|---|
| 1 — ENTRY TIMELINE | 23.02 | 15.32 | **7.66** | **48%** |
| 2 — PARACHUTES | 26.00 | 17.31 | **8.65** | **54%** |
| 3 — CONTINGENCY | 26.00 | 17.31 | **8.65** | **54%** |

**Measured ink heights on the render:** card 1 rows **5–7 px** with a 9.4 px line pitch; card 3 rows **9 px**.
At 4× magnification the card-1 rows are visibly mush — strokes merge and "Deorbit burn" reads as "bum".

**The finding's character changes.** As filed it was *"card 1 is marginally under the floor while cards 2 and
3 clear it"* — cards 2 and 3 measured 17.3 px, **above** 16. At the shipped width **all three cards are at
roughly half the floor**, and the difference between them is no longer the point.

⚠ **The unit bug is correspondingly worse.** `if (size < Typography.Min)` compares a design size to a
panel-pixel constant, so the clamp would permit a design size of 16 — which at the shipped width renders at
**5.33 px, one third of the real floor.** Filed, that latent floor was 10.65 px.

⚠ **And it is not confined to this page.** See **R-01**: every sampled text element on every Figma-era page
is below the floor at the shipped width. C-05's fix (compare in panel space) is still right and still
necessary, but on its own it now exposes an overflow the layout cannot absorb — its option (b), moving
ENTRY TIMELINE into the taller card, does not create enough room. **Sequence C-05 behind R-01.**
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


### ✅ FIXED 2026-09-05 — S103 (QC batch 1)

**Fixed in the asset.** `component_48.png` cleared to its own flat background `#111B52` (17, 27, 82) in
three boxes shaped **around** icon 0 rather than through it — `y 200..232, x 10..160` (fully below the
icon's last ink row, 198), plus `y 190..199` at `x 10..49` and `x 122..160` either side of it — stopping
short of the bar's own bottom border at y 233..234.

**Verified against the pre-edit asset:** icon 0 (x 54..117, y 134..198) **byte-identical**; the bottom
border (y 233..234) **byte-identical**; 4350 pixels changed, **none outside the box**; the glow region's
peak luminance 113.7 → **42.0, the bar background exactly**.

**On the glass**, the residue probe on the pages whose active tab is *not* icon 0:

| render | active tab | before | **after** |
|---|---|---|---|
| `ui_cabin.png` | icon 4 | +27.4 above plain bar | **+0.8** |
| `ui_audiovideo.png` | icon 4 | +27.4 | **+0.8** |
| `ui_cover.png` | icon 0 | +56.5 | +35.1 — *the real dynamic marker, correctly there* |

Both edits to this asset are now recorded in `docs/COVER_PAGE_ASSETS.md`, with the instruction that a
re-export must re-apply both.
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


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `ui_cover.png`, 1280×703.  **Verdict: STANDS.** Every proportion is identical; every absolute
figure is exactly half.

| | filed (2560×1406) | now (1280×703) |
|---|---|---|
| camera slot | x 960…2560, centre 1760 | **x 480…1280, centre 880.0** |
| globe centre / radius | (1760, 698) r 530 | **(880.0, 349.0) r 264.5** |
| clear band below the disc | 21.5 px | **11.3 px** |
| TARGET LATITUDE over the disc | 91% | **88%** |
| TARGET LONGITUDE over the disc | 19% | **17%** |
| band spacing, left→right | 317 / 75 / 64 / 235 / 22 | **158.6 / 37.6 / 32.0 / 117.5 / 10.7** |

*(The 91%→88% and 19%→17% differences are precision on the ray-measured disc radius, not a real change.)*

**The fix geometry re-derives cleanly and its key property survives.** At the shipped width the design's own
32-design-px margin is 10.7 px; mirroring the pills puts NEXT VIEW at **490.6…624.1**, so it moves **left by
147.9 px** (filed: 295.9 — exactly half). The proposed readout centres become **721.8** and **2078.5 → 1038.2**,
whose midpoint is **880.0 — the slot centre, exactly.** The construction was not an artifact of the scale.

### ✅ FIXED 2026-09-05 — S105 (QC batch 3)

**Fixed, to the owner's note (R-2, R-3, R-4).** Both halves are derived now rather than placed:

- `NextViewRect` returns `(ViewLeft + 32) * sc` — SETTINGS' own 32-design-px margin, mirrored about the
  other end of the camera slot. `NextX = 1500f` was **exactly the reflow `Split`**, which is why the pill
  took the full slack.
- The two TARGET readouts are drawn by `DrawCameraChrome`, centred on the camera slot's own centre
  ± `ReadoutHalfGap` (475 design px) — the same centre the globe is drawn about.

**Measured after, at 1280×703:**

| | before | **after** |
|---|---|---|
| pill insets from their slot ends | 158.6 vs 10.7 | **10.7 vs 10.7** |
| readout centres | — | 721.9 and 1038.1, **midpoint 880.0 = the slot centre exactly** |
| TARGET LATITUDE over the disc | 88% | **0%** |
| TARGET LONGITUDE over the disc | 17% | **0%** |

⚠ **Q4 (the CAMERA caption) is untouched** — it is an open owner question and this fix does not pre-empt it.
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


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `frame58_hud.png`, 1280×703.  **Verdict: CHANGED — worse, and it is now two defects rather
than one.**

| | filed (2560×1406) | now (1280×703) |
|---|---|---|
| letterbox `ox` | 139.29 | **69.64** *(still > 40, so the affordance is drawn)* |
| box | x 12…127.3, w 115.3 | **x 12…57.6, w 45.6** |
| type `h * 0.020` | 28.1 px | **14.06 px** |
| `MANUAL` ink | 14…140 = 126 px = **109%** of the box | **8…63 = 56 px = 123% of the box** |
| `DOCKING` ink (cyan, incl. borders at 12 / 58) | 7…132 | **5…65** |

`MANUAL` now starts **4 px left of the left border** and ends **5.4 px right of the right one**; `DOCKING`
clears both borders on both sides. The overrun grew from 109% to **123% of the box width**, because the box
is derived from the letterbox width and the type from the panel height — two quantities that halved
together, while the string's rendered width did not (56 px where a linear halving gives 52).

⚠ **NEW, and independent of the overflow: the label is now below the legibility floor.** 14.06 px against
`Typography.Min` = 16 — **88% of the floor.** So the page's only interactive control is both illegible and
overflowing. The filed fix (size the type from the box) is still correct but must now also clear the floor,
which a 45.6 px box cannot do for the word "DOCKING" — **the box has to grow, or the label has to change.**
See **R-01**.
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


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `frame58_hud.png`, 1280×703.  **Verdict: STANDS** — structurally identical, at half the scale.

| | filed | now |
|---|---|---|
| frame art (letterboxed) | x 139.3…2420.7 | **x 69.64…1210.36** |
| `component_48` (full width) | 0…2560 | **0…1280** |
| the frame's left edge as a rule inside the bar's span | 139.3 px | **69.6 px** |
| the two rounded corners' separation | ~100 px | **~50 px** |

Verified on the render: the vertical white rule at **x = 70** runs from **y = 631** to the page bottom; the
frame's own rounded bottom-left corner starts just right of it; `component_48`'s corner sits at the true page
edge; and the triangular sliver of lighter navy is still trapped between the frame's curve and the bar's top.
Two corners, one seam, unchanged.

⚠ **Distinguished from S101, as the brief asks.** The hairlines involved are now visibly greyer and thinner —
`Stroke(sc, 2)` is 0.67 px and clamps to 1 — so the borders *look* fainter. **That is S101 and it is not this
finding.** H-07 is about the borders being in two different *places*, and they are: 69.6 px apart, exactly as
filed. Fixing S101 would make this defect more visible, not less.

### ✅ FIXED 2026-09-05 — S103 (QC batch 1)

**Fixed by the same change as C-04, and this is why the two were one job.** `component_48` carries the design
frame's **own** bottom border and left/right edges, so drawing it `0…w` while the page art was drawn at `ox`
put a second page border 69.6 px inside the first. Drawing the bar in the design frame — `ox … ox + RefW·sc`
— makes the bar's edge and the frame's edge the **same edge**.

**On the glass** (`frame58_hud.png`, bottom-left, 6× crop): the vertical white rule at x = 70 is gone as a
*separate* line, there is **one** rounded corner instead of two ~50 px apart, and the triangular sliver of
lighter navy trapped between them has gone with it.

⚠ **The ten pages that spread x across the full width now show the bar inset ~70 px from each edge**, with
its own rounded corners on page ground. Inspected on `ui_vehicle.png`: it reads as a framed bar and is
better than the stretched version it replaces. **The strips are deliberately left unfilled** — filling them
would put the asset's own left/right border in the middle of a filled bar, which is this defect one step to
the right. Recorded in `BottomBar.cs`'s header.
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


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `frame58_hud.png`, 1280×703.  **Verdict: ⛔ VANISHED — struck. This was an artifact of the 2×
render and was never true of the shipped build.**

| | filed (2560×1406) | now (1280×703) |
|---|---|---|
| `frame58.png` native | 2048 × 1263 | 2048 × 1263 |
| drawn at | 2281.4 × 1406 | **1140.7 × 703** |
| scale factor | **1.1140 — an UPSCALE** | **0.5570 — a DOWNSCALE** |

**And the sharpness measurement inverts.** Normalised mean edge gradient (higher = sharper), same method:

| region | source asset | filed | **now** |
|---|---|---|---|
| `FLIGHT COMMANDS` | frame58.png | 0.314 | **0.411** |
| `FAR FIELD POSITIONING` | frame58.png | 0.315 | **0.395** |
| `CURRENT STATE` | component_48.png | 0.365 | **0.311** |
| `Far Field Pointing Deorbit` | component_48.png | 0.357 | **0.357** |

Filed, the frame art measured ~14% **softer** than the bar art. At the shipped width it is the **sharper** of
the two. The premise — *"exported at 0.6× design scale, so at the preview's resolution it is drawn upscaled
and measurably soft"* — is false, and *"the preview's resolution"* is precisely what was wrong.

⚠ **The finding predicted its own death and should be given credit for it**: it was filed conditional on Q5
(*"H-08 is only a defect if Q5 resolves to 2560 … at the shipped 1280×703 the frame is drawn at 1140.7 px, a
downscale of 0.557, and this defect does not exist"*). S100 resolved the instrument to 1280 and the
prediction is confirmed to three decimal places. **Nothing to fix. Do not re-export `frame58/59/66.png`.**
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

---

# PAGE 2 — AUDIO SETTINGS

**Render inspected:** `settings_audio.png` (2560×1406 — again not the shipped size, H-01), 2026-09-05.

**Source under inspection:** `plugin/src/pure/SettingsAudioPage.cs` (127 lines) · `FigmaUI.cs:197` (the
build call) · `FigmaUI.cs:310-320` (the tab hit bands) · `plugin/src/pure/SettingsPage.cs:27-29` (the
owner decision that governs this page).

**S49's entry, and what the glass says about it.** S49 §2 rates this page *"Display-only. `sel` is the
literal `2`; channel values … are a literal array; the eight ± buttons and two fan buttons are drawn art
with no HitTest in the file at all"* (H12). **Every clause of that is confirmed at HEAD.** The glass adds
four defects and turns one of S49's classifications into an owner question — because a **dated owner
decision on record says these controls should not be drawn at all**, and S49 read that decision as covering
only the values.

## What was checked and found CLEAN

1. **The three tab hit bands sit on the three drawn tabs.** Drawn at design x 1584 / 1714 / 1843
   (`SettingsAudioPage.cs:117-119`); hit bands 1520–1650 / 1652–1780 / 1782–1910 (`FigmaUI.cs:314-317`),
   centres 1585 / 1716 / 1846. Within 3 design px on all three. The tabs are the one wired thing here and
   they are wired correctly.
2. **No collision between the tab band and the bottom bar.** `BottomBarHit` runs first and its icons sit at
   design x 46…638; the tabs are at 1520…1910. The two y-bands overlap by 9 design px and it does not
   matter, because they share no x.
3. **The tabs survive being drawn under the bar.** `component_48` is drawn last (`:126`) over the tab rows,
   but its top ~105 rows are transparent, so the labels and the accent underline read correctly. ⚠ This is
   luck, not design: the page depends on an asset's alpha for its own navigation to be visible. If the bar
   is ever re-exported opaque, the tab strip disappears with no code change.
4. **The accent underline matches its tab cell, not its label.** 120 design px wide against a 130-px hit
   cell and a ~65-px label — correct for a tab indicator, and it is centred on the label's own centre.
5. **No asset is upscaled.** The seat PNGs are 1161×1748 drawn at 580×874 design (×0.333 at this panel);
   the cabin PNG is 1216×1888 drawn at 608×944. All downscales. H-08's problem does not exist here.
6. **The seat labels are baked into the seat PNGs**, not drawn — so a live `sel` would move the highlight
   without disturbing the labels. Not a defect, recorded so it is not mistaken for one.

## Cross-page confirmations

**C-04** (the 12.2% horizontal stretch) and **C-12** (the un-erased marker glow) are present again —
`SettingsAudioPage.cs:126` draws `component_48` at `3427 * sx` for sizes scaled by `sy`, the identical
straddle-stretch. **Third page, same two defects.** No new finding.

---

## A-01 — The page has five layouts and can render exactly one of them, forever

**TIER 1** · confirms S49 **H12**

**Evidence.** `FigmaUI.cs:197` — `case UiPage.Audio: SettingsAudioPage.Build(dl, w, h, 2); break;`

`Build`'s `sel` parameter selects which of the five audio scopes is shown: `sel == 2` is CABIN, `0/1/3/4`
are SEAT 1–4. It drives the highlight box (`:78-84`), the panel heading (`:97`) and nothing else — the five
seat illustrations are always all drawn. The call site passes the literal `2`. There is no `PageState`
parameter on this page at all, and `sel` is never anything else in the build or in the preview
(`PreviewMain.cs:585` also passes `2`).

**What is wrong.** `CABIN AUDIO` is the only heading this page can ever show. The four `SEAT n AUDIO`
layouts are written, correct, and unreachable — the same class as S49 H9's nine dead `UiPage` values, but
inside a page that ships. And because the highlight is the *only* thing that moves, the page presents five
selectable-looking seats of which none is selectable.

**Fix plan.**
- The seat selection is **pure screen state** — it changes a heading and a highlight and commands nothing —
  so it is (A) and buildable now. It wants (i) a `sel` held per screen in the painter beside `coverPhase`
  and `coverCam` (`ScreenPainter.cs:84-100`), and (ii) a hit test over the five seat boxes, which
  `SeatBox` already defines and which the draw already uses.
- **Do it with one shared rect function**, `SeatRect(i, w, h, …)`, called by the draw and the hit test —
  `PageAction`'s rule, and the exact thing H-04 shows going wrong when it is not followed.
- ⚠ **What the four seat layouts should then SHOW is the open question, not the switching.** Today
  `ChValue` is one literal array shared by all five scopes, so selecting SEAT 2 would change a heading and
  nothing else — five headings over one set of numbers, which is worse than one honest heading. **A-02 and
  Q6 govern what the numbers are allowed to be**, so land that first.
- **Must not break:** the tab strip, which is the page's working navigation.
- **Verify:** five preview renders, one per `sel`, each with its own highlight and heading.

---

## A-02 — The page paints ten controls that a dated owner decision says should not exist, and none of them can be touched

**TIER 1** · extends S49 **H12** · ⚠ **partly re-classifies it — see Q6**

**Evidence.** Ten controls are drawn (`SettingsAudioPage.cs:104-116`): eight ± buttons (GROUND, AUX,
INTERCOM, ALERTS × minus/plus) and two signal buttons (GROUND, AUX). Each is a filled square with a
`St(3)` white border and a centred glyph — this build's button idiom everywhere.

**There is no hit test.** Verified three ways: no `HitTest` in `SettingsAudioPage.cs`; no `SettingsAudioPage.`
reference anywhere in `plugin/src/` or `plugin/test/` that mentions hit-testing; and `FigmaUI.HitTest`'s
settings branch (`:310-320`) resolves **only** the three tabs. Ten painted buttons, zero rectangles.

**And the decision is on record.** `plugin/src/pure/SettingsPage.cs:27-29`, quoted in full:

> *"---- NO VOLUME SLIDERS ---- (user's call, 2026-08-06). Audio shows per-seat ROLE and occupancy, and
> the intercom/alert state. KSP has no cabin audio, so a fader would be a control bound to nothing.
> **Simulate a reading, never simulate a control.**"*

Two lines above it, the same comment states the principle for the lighting zones: *"Drawing eight buttons
where seven do nothing is **the dead-control failure this project refuses**."*

**What is wrong.** The Figma rebuild re-introduced, as ten dead buttons, precisely the faders a dated owner
decision removed — and the decision's stated reason ("a control bound to nothing") is exactly what they are.
S49 filed the audio faders as **(C) deliberate** on the strength of this same comment, but the comment
supports only half of that: *the values staying display-state* is deliberate and correct; *drawing the
controls* is the thing it forbids. The distinction matters because (C) means "recorded, do not re-log", and
ten dead buttons on a reachable page is not a thing to stop logging.

⚠ **I am not asserting the owner's 2026-08-06 decision still stands unchanged.** It predates the Figma
rebuild, and the Figma design is itself a source (§1.4). That is exactly why this is **Q6** and not a fix.

**Fix plan.** Held pending Q6. The three shapes it can take:
- **Remove the ten buttons.** Truest to the 2026-08-06 decision and to the no-dead-controls principle. The
  channel values stay as readings. Costs fidelity to the Figma frame.
- **Tint them inert.** `DragonPalette.Text6`, S75's *"nothing live behind this"* tint, so they stop riding
  the button idiom while the design's layout survives. Cheapest, and it is the branch S75 chose for
  `gridicons_refresh` when the action had no source.
- **Make them live as screen state.** A per-channel level held in the painter, adjusted by the ± buttons,
  with the values drawn from it. It commands nothing and breaks no gate — but it is the *simulated control*
  the 2026-08-06 decision names, so it needs the owner to reverse that decision explicitly.
- **Must not break, in every case:** the two signal buttons are not faders and may be a different question —
  they read as a squelch/signal indicator, and no source in the repo names their action.

---

## A-03 — The dividers define five equal cells; AUX's value and two of the four button clusters do not sit in them

**TIER 2** · **NEW**

**Evidence.** All arithmetic from `SettingsAudioPage.cs:33-38`, confirmed against `settings_audio.png`.

The four dividers (`DivX = {966, 1464, 1962, 2460}`) and the panel edges (468, 2957) cut the panel into
**five cells of 498 design px** — a perfectly regular grid. Against that grid:

| channel | cell | cell centre | value drawn at | button cluster centred at |
|---|---|---|---|---|
| GROUND | 468–966 | 717 | **717** ✓ | **717** ✓ |
| AUX | 966–1464 | 1215 | **1257** ✗ **+42** | 1219 ✓ *(+4)* |
| MAIN | 1464–1962 | 1713 | **1713** ✓ | *(none — see A-05)* |
| INTERCOM | 1962–2460 | 2211 | **2211** ✓ | **2257** ✗ **+46** |
| ALERTS | 2460–2957 | 2708.5 | **2709** ✓ | **2754** ✗ **+45.5** |

**Three of thirteen positions are off a grid that the other ten land on exactly.** They are two independent
errors, not one systematic offset:

1. **AUX's label and value** are 42 design px right of their cell — its *buttons* are correct.
2. **INTERCOM's and ALERTS' button pairs** are ~45 px right of their cells — their *values* are correct.

Visible on the render: AUX's three buttons sit left of the `0dB` above them; INTERCOM's and ALERTS' pairs
sit right of `+9dB` and `50`. ≈33 panel px at 2560, ≈16 px at the shipped 1280.

⚠ **The dividers themselves are exactly regular**, and they prove it: with AUX's value at its cell centre
1215, the midpoints between neighbouring values become 966 and 1464 — the divider positions, to the pixel.
So the grid is the design's intent and these three are outliers against it.

**Fix plan.**
- Stop writing thirteen absolute positions and **derive them from the grid the dividers already define**: a
  cell width, a cell index, and offsets within a cell. Then a value and its buttons cannot drift apart,
  and the next channel added cannot be placed wrong.
- Concretely: `CellCx(i) = 468 + 498 * (i + 0.5)`; the value and label centre on it; a 3-button row spans
  `CellCx ± 152` with the middle button on the centre; a 2-button row spans `CellCx ± 76`.
- ⚠ **§1.4 note, small but real.** These numbers came from *"the exact layer geometry from the Figma MCP"*
  (`SettingsAudioPage.cs:3-4`), so snapping them to a computed grid edits measured source geometry. Ten of
  thirteen landing exactly on that grid is strong evidence the outliers are transcription slips rather than
  design — and the owner has already ruled for balance on the Cover (**R-2/R-4**, *"I like well balanced
  layouts"*) — but the change should be **recorded as ours** rather than presented as re-measurement.
- **Must not break:** the dividers, which are already right, and the MAIN cell, which has no buttons.
- **Verify:** re-render and require each value's ink centre and its button cluster's centre to fall within
  2 px of the same cell centre, as a headless check.


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `settings_audio.png`, 1280×703.  **Verdict: STANDS.**

The grid is stated in design space and is untouched by the render size: five cells of **498 design px**,
dividers exact, ten of thirteen positions on the grid and three off it. Only the panel-pixel expression of
the three outliers changes, and it halves:

| outlier | design offset | filed (panel px) | **now (panel px)** | as a share of the 186 px cell |
|---|---|---|---|---|
| AUX label + value | +42 | +31.4 | **+15.7** | **8.4%** |
| INTERCOM button pair | +46 | +34.4 | **+17.2** | **9.2%** |
| ALERTS button pair | +45.5 | +34.0 | **+17.0** | **9.1%** |

*(cell width 498 design = **186.0 panel px**, filed 372.0.)* Confirmed visually on the honest render: AUX's
three buttons sit left of the `0dB` above them; INTERCOM's and ALERTS' pairs sit right of `+9dB` and `50`.
The share of the cell is unchanged, so the misalignment reads exactly as it did.
---

## A-04 — The signal glyph is drawn below its own button and far too small to read as one

**TIER 2** · **NEW**

**Evidence.** `SettingsAudioPage.cs:112-116`. The button box is `Btn(SignalX[i], 1598, 140, …)` — design y
**1598…1738**, centre **1668**. The glyph inside is
`dl.ArcBand(SignalX[i] * sx, PY(1690), SZ(6), SZ(20), -55, 55, White)` plus a filled dot of r5.

Two problems, both measurable:
- **It is centred on design y 1690, not 1668** — **22 design px low** (14.6 panel px at 2560) in its own
  140-px box. Its neighbours are correct: the − and + are drawn at `PY(1668)`, dead centre.
- **It is a 40-px-wide mark in a 140-px box.** The − spans 56 design px and the + 56×56; the signal fan's
  outer radius is 20, so it occupies under a third of the width its siblings use.

On the render it reads as a small solid mushroom blob sitting low in an empty square — not a signal fan, and
not obviously the same class of control as the two buttons beside it.

**Fix plan.**
- Centre it on the box: `PY(1668)`, the same constant the − and + already use. Better, derive all three
  from the `Btn` call's own y and size so a future move cannot desynchronise them — the same one-rectangle
  discipline as A-01 and H-04.
- Scale it to its siblings: outer radius ~28 design px (matching the ±'s 56-px span), inner radius scaled
  with it, so the three glyphs read as one set.
- ⚠ **It also does not read as a signal fan.** A 14-px-thick band over ±55° is a solid wedge at this size.
  Three thin concentric arcs over ±55° with a filled dot is the conventional form and is what the shape is
  reaching for. **This is our geometry either way** — the Figma export was not used for this glyph — so no
  §1.4 question, but mark it as ours.
- **Must not break:** A-02/Q6 may delete these buttons entirely. **Do Q6 first**; this fix is wasted if the
  answer is "remove them".
- **Verify:** re-render; all three glyphs in a channel's row share a centre line and a comparable extent.


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `settings_audio.png`, 1280×703.  **Verdict: CHANGED — the geometry halves as expected, but the
legibility half of the finding is worse than described.**

| | filed | now |
|---|---|---|
| glyph centre below its box centre | 22 design px = 14.6 panel px | 22 design px = **7.3 panel px** |
| button box | 140 design = 93.2 px | 140 design = **46.6 px** |
| glyph outer radius | 20 design = 13.3 px | 20 design = **6.7 px** |

The mis-centring is unchanged as a fraction of the box (15.7%), so that half **stands**. What changed is what
the glyph reads as: filed, I described *"a small solid mushroom blob"* — a 27-px-diameter mark. At the shipped
width it is a **~13 px mark inside a 47 px box**, and on the render it reads as a **speck**, not as a signal
fan and barely as a mark at all.

⚠ So the filed fix (centre it, and scale it to its ± siblings) is still right but is no longer cosmetic —
at this size the glyph must grow substantially or it conveys nothing. ⚠ **Q6 still gates this**: if the ten
controls are removed, the fix is moot.
---

## A-05 — MAIN's VOX readout has no box, though the file's own comment says it should

**TIER 3** *(fidelity — needs the Figma export)* · **NEW**

**Evidence.** `settings_audio.png`, the MAIN column: `VOX` and `17` float as bare text in the row where the
four other channels each carry a bordered button row. Nothing is drawn around them.

`SettingsAudioPage.cs:36` says otherwise, in the code's own words: *"-/+ button centres (design x) per
side; **MAIN has the VOX box instead**."* The draw (`:118-119`) is two `CTxt` calls and no box.

**What is wrong.** A discrepancy inside our own file: the comment describes a box that the code does not
draw. Either the comment is loose wording for "MAIN has VOX there instead of buttons", or the box exists in
the Figma frame and was dropped in transcription. On the glass the row reads as a gap in an otherwise
regular strip, which is the owner's balance concern again (**R-4**).

**Fix plan.** This needs the source, not a guess (C1.4) — see **Q6**'s note, which carries the same
dependency.
- **If the Figma frame has a box:** draw it at the measured bounds, in the same idiom as the ± boxes but
  visibly not a button (no border-as-affordance, or the inert tint) — VOX is a *reading*, not a control,
  and this page's whole problem is controls that are not controls.
- **If it does not:** correct the comment, which is the actual defect in that case.
- **Must not break:** the VOX value stays a readout under either answer.
- **Verify:** re-render and compare the MAIN cell against the export.

---

## A-06 — The centre panel — the one the page is actually showing — is an empty box between four illustrated seats

**TIER 2** · **NEW** · §1.4 note

**Evidence.** `settings_cabin_seat.png` is **1216×1888**. Its entire bright content is **one 213×58 region
at x 502…715, y 90…148** — the word "Cabin". Measured: 5,298 pixels above luminance 120, in a single row
band, out of 2.3 million. The rest is a flat dark panel: mean luminance **22.3**, and **0.23%** of pixels
brighter than 120, against **4.7–4.9%** for each of the four seat PNGs.

On the render the effect is a hole: four illustrated seats, and in the middle — inside the cyan selection
highlight, on the scope the page is actually reporting — a dark rectangle containing two small rings drawn
by our own code (`SettingsAudioPage.cs:88-93`) and nothing else.

**What is wrong.** The selected panel is the emptiest thing on the page. Whether the community Figma's cabin
frame was itself an empty plate, or whether the export lost a layer, cannot be told from the repo — the
Figma exports are gitignored (`assets/figma/`). What *can* be said is that the asset carries one word and a
fill, which is a strange thing to ship as a 1216×1888 PNG, and that the result is visibly unbalanced in the
way the owner has already objected to on the Cover.

**Fix plan.**
- **First establish what the frame contains** — the same export dependency as Q1 and A-05. If a layer was
  lost, re-export; that is the whole fix and it needs no design decision.
- **If the frame really is an empty plate**, the panel needs content of its own or it should stop being a
  panel. The two speaker rings we already draw are the honest seed: a cabin-audio scope could show what the
  cabin actually has — speaker count, intercom state, alert routing — all of which `CabinEnvironment` and
  `Alarms` can supply, and none of which commands anything (§14.4(f) READOUTS, (A)).
- ⚠ **Do not fill it with a picture.** A drawn cabin interior would be a tier-3 invention (§1.4) with no
  source, on a page that already has one asset of unknown provenance.
- **Must not break:** the seat highlight geometry, which is computed from `SeatBox[sel]` and is correct.
- **Verify:** re-render; the selected panel should carry at least as much information as the four
  unselected ones.


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `settings_audio.png`, 1280×703.  **Verdict: STANDS, unchanged.**

The finding is a property of the asset, not of the render, and the asset has not changed:

| asset | bright (> lum 120) |
|---|---|
| `settings_cabin_seat.png` (1216 × 1888) | **0.23%** |
| `settings_seat1..4.png` | 4.74% / 4.86% / 4.86% / 4.85% |

Its entire bright content is still one 213 × 58 region — the word "Cabin". It is now drawn at **202 × 314
panel px** (filed 405 × 629), and on the honest render the centre panel reads as the same dark hole between
four illustrated seats — if anything more so, because the two speaker rings our code draws over it are now
~10 px across and contribute even less. Nothing in the fix plan changes.
---

## Open questions for the owner — Audio (Q6)

### Q6 — A dated decision says this page should have no volume controls. It has ten. Does the Figma design change that call? (A-02, and it gates A-04/A-05)

**Situation.** `SettingsPage.cs:27-29` records, in the code, dated and attributed: *"---- NO VOLUME SLIDERS
---- (user's call, 2026-08-06) … KSP has no cabin audio, so a fader would be a control bound to nothing.
Simulate a reading, never simulate a control."* Two lines above, the same comment calls drawing buttons that
do nothing *"the dead-control failure this project refuses."* The Figma rebuild — `SettingsAudioPage`, which
replaced the page that decision was written for — draws **eight ± buttons and two signal buttons**, with no
hit test anywhere in the build. So the page now shows exactly the controls the decision removed, inert.

The decision predates the Figma rebuild, and the Figma frame is itself a source (§1.4). So this is not
simply "the code drifted from a ruling" — it is two sources disagreeing, and only the owner can say which
governs. **A-04 and A-05 are both downstream of the answer** and should not be built before it.

**Options.**
1. **The 2026-08-06 decision still governs — remove the ten buttons.** The channel values stay as readings,
   which is what that decision explicitly allows. Truest to the ruling and to the no-dead-controls
   principle; costs fidelity to the Figma frame. *(Recommended — see below.)*
2. **Keep the layout, tint the controls inert.** `DragonPalette.Text6`, S75's established "nothing live
   behind this" tint. The design survives, the affordance stops lying. Cheapest, and it is the branch S75
   itself took when a glyph's action had no source.
3. **Reverse the decision — make them live screen state.** A per-channel level in the painter, adjusted by
   the ± buttons, values drawn from it. Nothing is commanded and no gate is touched, but it is precisely
   the *simulated control* the ruling forbids, so it needs the owner to say so explicitly (C1.8 — a settled
   decision stands unless the owner types `OVERRIDE`).
4. **Leave as-is.** Not recommended: ten dead buttons on a reachable page is the failure the project's own
   comment names, and it is now on the glass in a preview.

**Recommendation: 1, with 2 as the fallback if the Figma frame is judged worth preserving intact.**
Reasoning: option 1 is what the ruling on record actually says, and the values-as-readings half of the page
is untouched by it — the page still shows GROUND / AUX / MAIN / INTERCOM / ALERTS and their levels, which is
the information the crew needs. Option 3 is defensible but it needs an `OVERRIDE`, and this chat cannot
infer one from the design's existence.

⚠ **Whichever way it goes, A-01 comes first**: making the seat selection live is independent of this
question, and until it lands the page can only ever show CABIN.

---

---

# PAGES 3 + 4 — PROCEDURE (FRAME 59) and CABIN (FRAME 66)

**Inspected together because they are one source file.** `FigmaUI.cs:198-199` routes both to
`FigmaFramePage.Build(dl, w, h, "frame59" | "frame66")` — the same 26-line function, differing only in
which PNG it places. One section, one commit; the inventory marks both DONE.

**Renders inspected:** `frame59.png` (Procedure) · `ui_cabin.png` = `frame66.png` (Cabin, the same render
under two names) · cross-checked against `ui_vriotest.png`. All 2026-09-05, 2560×1406 (H-01).

**S49's entry.** §2 rates both as *"One PNG. `FigmaFramePage.Build`, `Commands = 8`, no `PageState`, no
HitTest branch — a dead end escapable only by the bottom bar"* (H13), and calls Procedure *"a generic
placeholder template"*. **The first half is exactly right. The second half is wrong** — Frame 59 is a
fully-specified real procedure screen, and the build already contains a second, element-level rendering of
it (F-01).

## What was checked and found CLEAN

1. **`Commands = 8` is a correct budget** — the function makes three draw calls.
2. **The frame art is drawn undistorted.** Fit-to-height, centred, so the illustration keeps its aspect.
   That much of the file's stated intent holds.
3. **No asset is upscaled at the shipped width.** `frame59.png`/`frame66.png` are the same 2048-wide export
   as `frame58.png`; H-08's upscale exists only at the preview's 2560 and is the same finding, not a new one.

## Cross-page confirmations

**C-04** (12.2% bar stretch), **C-12** (the un-erased marker glow) and **H-07** (frame letterboxed at `ox`
while the bar is drawn `0…w`, so the frame's left border becomes a rule crossing the bar and two rounded
corners collide) all recur here — `FigmaFramePage.cs:23-26` is the same construction as `Frame58Hud`, line
for line. **Pages four and five for C-04/C-12; second and third for H-07.** No new findings.

---

## F-01 — `UiPage.Procedure` and `UiPage.VrioTest` are the same real screen, shipped twice, both reachable from the Menu

**TIER 1** · **NEW** · corrects S49 **H13**'s "generic placeholder template"

**Evidence.** Put `frame59.png` and `ui_vriotest.png` side by side. They are the same screen:

| element | Frame 59 (page 3) | VrioTest (page 19) |
|---|---|---|
| title | `4.700 - Deorbit Preparation` | `4.700 - Deorbit Preparation` |
| section | `DEORBIT` | `DEORBIT` |
| checklist | 5 items, 4 ticked, `5. COMPLETE FLUID LOADING` open | identical, same 4/1 state |
| heading | `SECTION 4: IN PROGRESS` · `Test VRIO Health LEDs` | identical |
| steps | 4.1 … 4.5, same wording | identical |
| commands | START VRIO 1 / START VRIO 2 / STOP VRIO 2 | identical |
| footer | `NEXT` · `ENTER READ-ONLY` | identical |
| notes | the same two note cards, same wording | identical |

So the build carries **two `UiPage` values for one screen** — page 3 as a flat baked PNG, page 19 as an
element-level rebuild — and `MenuPage` lists both, as `PROCEDURE` and `TEST VRIO HEALTH LEDS`. A crew member
opening the Menu sees two cards that lead to the same procedure, drawn differently.

**And the two renderings disagree with each other**, which is the sharper half of the defect:
- the frame's checklist ticks are **white outline** circles; the rebuild's are **filled green**;
- the rebuild adds a refresh glyph beside `SECTION 4: IN PROGRESS` that the frame does not have;
- the note cards sit at different x and use different type treatments (the rebuild has a hanging indent
  after `Note:` and dims its bullet lines);
- the rebuild's `ENTER READ-ONLY` glyph is a filled rounded rect; the frame's is an eye.

Two surfaces stating the same procedure, disagreeing on its appearance — C7.1's own failure mode, and the
same class as the S13 residual the Cover fixed (`CoverPage.cs:126-141`).

**Fix plan.**
- **This is a routing decision, not a drawing one.** `UiPage.VrioTest` is the real rebuild and the one S49
  H21 already schedules work against; `UiPage.Procedure` is its baked predecessor. The clean resolution is
  to **point `UiPage.Procedure` at `VrioTestPage`** — one screen, one renderer — and drop `frame59` from the
  draw path. The enum value stays (UiPage's own rule: never renumber), so no save breaks.
- ⚠ **`MenuPage` must then stop listing both**, or the grid shows one screen twice under two names. `S14`
  already established the pattern for pruning the Menu (`FigmaUI.IsPlaceholder` decides grid membership).
- **Alternative, if the owner wants Frame 59 kept as the reference look:** keep it, but rename its Menu card
  so the duplication is legible rather than confusing, and record in `SCREEN_INVENTORY.md` that the two are
  one screen. **Not recommended** — it keeps two renderings that already disagree.
- **Must not break:** `frame59.png` stays on disk (C1.16's spirit — the asset is evidence of the reference
  look even if it stops being drawn), and the enum value is not renumbered.
- **Verify:** the Menu grid lists this procedure once; `ui_vriotest.png` is the only render of it.

---

## F-02 — Both pages are a single PNG with no state and no touch, and the Cabin page's data is already live one file away

**TIER 1** · confirms S49 **H13**

**Evidence.** `FigmaFramePage.Build(DisplayList dl, int w, int h, string frameKey)` — **no `PageState`
parameter**, structurally provable. Three draw calls. No `HitTest` in the file, and `FigmaUI.HitTest` has no
branch for either page beyond the settings tab strip (which only `Cabin` shares).

Painted controls with no rectangle behind them:
- **Frame 59:** `START VRIO 1 LED TEST`, `START VRIO 2 LED TEST`, `STOP VRIO 2 LED TEST`, `NEXT`,
  `ENTER READ-ONLY`, five tappable-looking checklist rows, and a baked scrollbar thumb — **11**.
- **Frame 66:** fifteen `- DISPLAY n` lighting rows, three tab icons, and a caption that says
  **`Tap to disable display`** — **19**, one of which instructs the crew to tap.

**And the Cabin page's data is all present and already drawn live elsewhere.** `PageState` carries
`Ppo2Text`, `Co2Text`, `PressText`, `CabinTempText`, `LoopAText`, `LoopBText`, `CrewText`
(`Pages.cs:104-106`), and `Pages.cs:1185-1197` already draws them as banded dials, as does
`SettingsPage.cs:293`. `Alarms.LifeSupport` / `Thermal` already band them. So the page shows a photograph
of a cabin while the real cabin's numbers are computed every frame two files away.

**Fix plan.**
- This is the element-by-element rebuild `FigmaFramePage.cs:9-11` says is the plan, and for **Cabin it needs
  no research at all**: the values, the formatters and the bands all exist. Overdraw at the frame's measured
  positions, exactly as C-01/H-02 propose for the Cover strip and the HUD.
- **For Procedure, F-01 dissolves the work** — the rebuild already exists as `VrioTestPage`.
- ⚠ **The lighting rows are not a drawing problem, they are F-03's.** Do not wire them.
- **Must not break:** the illustration. Frame 66's cabin render is the page's whole visual identity and
  should stay as the background it is.
- **Verify:** a `!Valid` render must dash every overdrawn value rather than showing the baked one.

---

## F-03 — Frame 66's LIGHTING panel is broken in the baked art, and it draws fifteen controls where a recorded finding says exactly one is bindable

**TIER 1** · **NEW**

**Evidence.** `ui_cabin.png`, the LIGHTING panel, magnified. Four faults, all in the PNG:

1. **Three of the four column headings are identical** — `CABIN` · `CABIN DISPLAYS` · `CABIN DISPLAYS` ·
   `CABIN DISPLAYS`.
2. **`- DISPLAY 3` appears twice** in each of columns 1, 2 and 3, and there is **no `DISPLAY 4`**. The rows
   read 1, 2, 3, 3.
3. **Column 4 has only three rows** and its box ends higher than the other three, so the four columns do not
   align along the bottom.
4. **Its caption ends mid-clause:** `Tap to disable display` / `or` — and stops.

**And the controls should not be there at all.** `SettingsPage.cs:20-25` records the finding, in the code:

> *"Checked in `TundraExploration/Parts/RodanV2/TE_CD2_POD.cfg`: the pod carries exactly ONE
> ModuleColorChanger, on the Light action group. There is no Back light, no Tip light, no per-zone anything
> to bind to. **Drawing eight buttons where seven do nothing is the dead-control failure this project
> refuses**, so the panel shows the master toggle plus whatever light modules are ACTUALLY found on the
> vessel, by name."*

That finding is about **eight** buttons. This page draws **fifteen** lighting rows, plus an instruction to
tap them, on a page with no hit test — the same failure the same file refuses, at nearly twice the scale.
This is A-02's shape a second time: the Figma rebuild re-introduced controls a recorded decision had removed.

**Fix plan.**
- **The art faults (1–4) cannot be fixed in code** — they are pixels in `frame66.png`, a community export.
  Either the frame is re-exported (needs the Figma, the same dependency as **Q1 / A-05 / A-06**), or the
  panel is **rebuilt as elements** and the baked one skipped, which is F-02's fix and the only route that
  does not depend on an export.
- **The controls question is A-02's, one page over, and should be answered once for both** — see **Q6**,
  whose options (remove / tint inert / make live under an `OVERRIDE`) apply here unchanged. ⚠ **The
  difference is that here the answer is already partly determined by evidence**, not just by preference:
  `TE_CD2_POD.cfg` has one `ModuleColorChanger`, so fifteen per-zone rows cannot be made live even if the
  owner wanted them. The honest rebuild shows **the master toggle plus whatever light modules the vessel
  actually has**, which is precisely what `SettingsPage` already does.
- **`Tap to disable display` must go or become true.** An instruction to tap, on a page where nothing is
  tappable, is the strongest form of the dead-control defect: it does not merely look interactive, it
  *says* it is.
- **Must not break:** if the panel is rebuilt, the cabin illustration behind it stays.
- **Verify:** re-render; no duplicate row label, four columns of equal height, no truncated caption, and no
  instruction the page cannot honour.

---

## F-04 — The settings tab strip exists in two incompatible forms, and the shared hit bands are computed in a coordinate system only one of them draws in

**TIER 2** · **NEW**

**Evidence.** Three pages share one tab strip — `Audio`, `Cabin`, `AudioVideo` — hit-tested by one block,
`FigmaUI.cs:310-320`, which maps a touch with `dx = px * RefW / w` (a **full-width stretch**).

But the three pages draw that strip in two different ways:
- **Audio** draws it as live text with `PX(x) = x * sx`, `sx = w / RefW` — the **same** full-width mapping
  the hit test uses. Exact agreement.
- **Cabin** does not draw it at all: the strip is **baked into `frame66.png`** with icons, and
  `FigmaFramePage` places that PNG **letterboxed** — `ox + x * sc`, a different mapping.

So on the Cabin page a tab painted at design x *d* is hit-tested as if it were at
`(ox + d·sc) · RefW / w`. At this panel that puts the three tabs at effective design x **1598 / 1714 / 1829**
against bands centred on **1585 / 1716 / 1846** — inside all three, with ~50 px of margin, but off by up to
17 px, and the error grows with `ox`, i.e. with panel width.

**And the two strips do not look alike:** Audio's is plain text with an accent underline; Cabin's is baked
icons above labels. Two sibling settings pages, two different navigation chromes.

**Fix plan.**
- **Draw the tab strip once, in code, for all three settings pages**, and skip the baked one on Cabin —
  the same "swap the baked element for a drawn one" move `CoverPage` already makes with `SkipKeys`.
  `FigmaFramePage` would need a skip mechanism, which is a reason to prefer F-02's element rebuild.
- **Then derive the hit bands from the same function that draws them** — the standing rule that H-04 and
  A-01 both invoke. One tab geometry, three pages.
- **Must not break:** the bands currently work at the shipped aspect; any change must keep all three tabs
  hittable on Audio, Cabin and AudioVideo, which is three renders to check, not one.
- **Verify:** a headless check that each drawn tab's centre maps back inside its own hit band, on each of
  the three pages, at two panel aspects.

---

## F-05 — The preview folder keeps stale renders, and one of them is a full Cover page from before the tint fix, named exactly like current output

**TIER 2** · **NEW** · QC-instrument finding

**Evidence.** `plugin/build/preview/` holds **118 PNGs**. Nineteen are older than S75's tint fix
(2026-09-04) and are therefore drawn by the renderer that **ignored asset tint entirely**:

```
2026-08-05  screen1/2/3.png
2026-08-21  navball_*.png, _stock_*.png, _heading_*.png, _verify_hn_he.png   (11 files)
2026-08-29  globe_left.png, globe_right.png
2026-09-01  arrow_zoom.png, seat_thumb.png
2026-09-01  ui_cover_phase4.png        ← 1.75 MB, a full Cover render
```

`ui_cover_phase4.png` is the dangerous one. It is named exactly like the current `ui_cover_phase5.png` and
`ui_cover_phase6.png`, it is a full-size Cover render, and **`PreviewMain` no longer produces it** — grep
finds no reference. It is an orphan from a deleted render block, and nothing about the file says so.

**What is wrong.** The preview directory is never cleaned, so it accumulates output from render blocks that
have since been removed, and those orphans are indistinguishable by name from current output. This role
nearly used that file as evidence for the Cover's phase-4 body — which would have been a finding written
against a tint-blind render of a two-week-old build. The directory is gitignored *and* documented as
*"output, not input — they are how the pages are checked, and they change every build"* (`.gitignore`), but
they do not all change every build, and that is the gap.

⚠ **The Cover section's inventory marks slots 0, 2, 3 and 4 as ⏳ PART for exactly this reason** — the
tempting `ui_cover_phase4.png` was not usable, and C-07's fix plan proposes rendering all seven slots.

**Fix plan.**
- **Clear the output directory at the start of each `build.py preview` run**, so the folder contains exactly
  what this build produced and nothing else. It is gitignored build output, so nothing is lost, and it makes
  a stale render impossible rather than merely detectable.
- ⚠ **Check the four non-`ui_` families before deleting them wholesale**: `navball_*`, `_heading_*`,
  `_stock_*` and `globe_*` look like one-off investigation renders from named campaigns (2026-08-21 and
  the Campaign 4 globe work). If any is still cited as evidence in `docs/`, it belongs in `docs/reference/`
  as a tracked input, not in gitignored build output — **that is C1.16's territory and must be checked, not
  assumed.** A grep of `docs/` for those filenames answers it.
- **Must not break:** the preview's own console output already lists every file it writes; that listing
  becomes the manifest of what should exist.
- **Verify:** two consecutive `preview` runs produce byte-identical directory listings.

---

---

# PAGE 5 — MENU, and the nine placeholder pages (6–14)

**Renders inspected:** `ui_menu.png` · `ui_phasedeport.png` (representative of all nine placeholders — they
differ only in the title string). 2026-09-05.

**Source:** `plugin/src/pure/MenuPage.cs` (108 lines) · `plugin/src/pure/PlaceholderPage.cs` (41 lines) ·
`FigmaUI.IsPlaceholder`.

**S49's entry.** §2 rates Menu as *"Nav index; grid membership is compile-time. `Build` takes no
`PageState`"* — correct, and correctly classed **(C)**, not a defect. H9 records the nine dead enum values
as **(C) — record, don't build**. Both hold. The two findings below are things S49 could not see without a
render and a grep.

## What was checked and found CLEAN — and this page is the model the others should copy

1. ⭐ **`MenuPage.CellRect` is the shared-rectangle discipline done right.** Its own docstring: *"The one
   source of truth Build, HitTest and the headless nav test all share, so the drawn grid and the hit grid
   can never drift apart."* `Build` (`:76`) and `HitTest` (`:97`) both call it. **This is the exact pattern
   H-04 shows failing on the HUD and A-01 needs on the Audio page** — it already exists, in this file, and
   should be cited as the precedent when those are fixed.
2. **The hit mapping matches the draw mapping.** `HitTest` uses `px * RefW / w`; `Build` uses `PX(x) = x * sx`
   with `sx = w / RefW`. Exact inverses — unlike F-04's letterbox mismatch.
3. **Grid membership is correct per S14.** 25 cards for 35 enum values: `Menu` itself and the nine
   `IsPlaceholder` values are excluded, so no look-alike dead card reaches the grid.
4. **The heading and the card rects agree on centre.** `dl.Text("MENU", w * 0.5f, …)` uses the panel centre
   while the cards use `PX()`; at design 3427 the two coincide exactly (1713.5 → 1280 at this panel).
5. **The placeholder card is honestly built** — it names its destination, says plainly it is not built, and
   the bottom bar is a real way out. ⚠ Except for one line of its copy — M-02.

## Cross-page confirmations

**C-04** and **C-12** recur on both (`MenuPage.cs:85`, `PlaceholderPage.cs:37` — `component_48` at full
panel width again). Pages six and seven.

⭐ **F-01's evidence is visible here:** the grid carries **`PROCEDURE`** and **`TEST VRIO HEALTH LEDS`** as
two separate cards, and they open the same screen.

---

## M-01 — The Menu's row count is a hand-maintained constant, and the 31st page added will be drawn under the bottom bar and be untappable

**TIER 2** · **NEW** · latent

**Evidence.** `MenuPage.cs:29` — `const int Cols = 3, Rows = 10;` — with the file's own comment recording
that it has already been bumped by hand once:

> *"Rows bumped 9->10 (T6, Rendezvous appended): grid cells must cover `FigmaUI.PageCount-1` entries, and
> the count keeps growing every time a page is appended — see BuildEntries."*

`Rows` is **not derived from `Entries.Length`.** Today: 25 entries in 30 cells → **five dead cells** and a
visibly empty band across the bottom of the grid (`ui_menu.png`, rows 9 and 10).

**The latent half is the one that matters.** With `Top = 210`, `Bottom = 1830`, `Gap = 24`, `Rows = 10`, a
cell is 140.4 design px tall and the row pitch is 164.4. So entry **30** (the 31st) lands at:

```
CellRect(30) -> row 10, y = 210 + 10 x 164.4 = 1854 ... 1994.4
bottom status bar begins at design y 1877
HitTest guard: `if (dy0 < Top || dy0 > Bottom) return -1`   (Bottom = 1830)
```

So the 31st card would be **drawn**, mostly underneath the bottom bar, and **rejected by the hit test
entirely** — a visible card that cannot be tapped. The build is five appends away from that, and the enum
has grown by nine values since the Figma rebuild began.

**Fix plan.**
- **Derive the grid from the data:** `Rows = ceil(Entries.Length / (float)Cols)`, computed in the static
  initialiser beside `Entries`. Then the grid can never under- or over-provision, and the hand-maintenance
  the comment describes stops being needed.
- ⚠ **Deriving `Rows` alone is not enough** — with the row *pitch* fixed by `(Bottom - Top)`, more rows
  means shorter cells, and eventually cells too short for a 32-px label. Add the same guard `FitRows`
  needs (**C-05**): below a legible cell height the grid must **paginate**, not shrink. That is real work
  and should be scheduled, not bolted on.
- **Recommended now, as the cheap safe step:** derive `Rows`, and add a headless check that
  `CellRect(Entries.Length - 1)` ends above `Bottom` **and** above the bottom bar's design y (1877). That
  check turns a future silent breakage into a build failure — which is the same move C-07 and H-04's fix
  plans both propose.
- **Must not break:** `CellRect` is shared by `Build`, `HitTest` and `FigmaUINavTest`. Changing `Rows`
  changes all three together, which is exactly why the shared function is right.
- **Verify:** re-render `ui_menu.png` — 25 entries should fill 9 rows with two spare cells, not 8 rows with
  five; and the headless bound check must pass with a synthetic 31-entry list.

---

## M-02 — The placeholder page tells the crew "this button is wired", and no button is wired to it

**TIER 2** · **NEW** · extends S49 **H9**

**Evidence.** `ui_phasedeport.png` renders three lines:

```
DEORBIT BURN
PAGE NOT YET BUILT
this button is wired; the destination is coming
```

The third is a literal at `PlaceholderPage.cs:35-36`. **It is false.** Verified by grep across
`plugin/src/`: no `NavHit` anywhere targets `UiPage` 6–14 — the only matches for those enum values are
two comments explaining that `UiPage.Entry` (14) was *not* reused for `EntryPage`. And `MenuPage.BuildEntries`
excludes every `IsPlaceholder` value from the grid. So the nine placeholder pages are reachable only from a
**stale persisted page int** — a save written by an older build.

**What is wrong.** The copy was true when it was written: the Figma nav *did* wire every button to a
destination, which is what `PlaceholderPage.cs:3-5` describes. **S14 then removed those values from the
Menu grid** — correctly, per the owner's decision, so a dead card would not read as a real page — and left
the caption behind. The page now makes a claim about the build that the build contradicts, on the one
screen whose entire purpose is to be honest about not being built.

**Fix plan.**
- **Correct the line to what is now true.** Something with no claim in it — *"no page is built for this
  destination yet"* — or, better, say how the crew got here, because that is genuinely useful: this page
  can now only appear from a stale saved selection, and *"this screen was remembered from an older save"*
  tells them something actionable (the bottom bar takes them anywhere).
- ⚠ **Do not delete the page or the enum values.** `UiPage`'s own rule is that the int persists per screen
  and values are never renumbered; the placeholder is precisely the graceful landing for an int that no
  longer resolves. S49 H9 classes this **(C) — record, don't build**, and that stands: the *page* is
  correct, one *sentence* is not.
- **Optional, and worth considering with it:** `ScreenPainter` could clamp a persisted page int that
  resolves to `IsPlaceholder` back to `UiPage.Cover` on load, so a stale save opens on the hub instead of
  on a "not built" card. That is a behaviour change, so it is the owner's call — but it would make the
  placeholder genuinely unreachable, at which point the caption question disappears.
- **Must not break:** the back route. The bottom bar is drawn on this page and is the way out.
- **Verify:** re-render any placeholder; the caption must not assert a wiring that does not exist.

---

*Page 0 (Cover) inspected 2026-09-05; C-12 and C-13 added the same day on owner review (R-1…R-5).
Page 1 (Hud / Frame 58) inspected 2026-09-05 at HEAD `97f4c78`.
Page 2 (Audio settings) inspected 2026-09-05.
Pages 3 + 4 (Procedure / Cabin) inspected 2026-09-05.
Page 5 (Menu) + pages 6–14 (the nine placeholders) inspected 2026-09-05.
Page 15 (Vehicle Overview) inspected 2026-09-05.*

---

# PAGE 15 — VEHICLE OVERVIEW (tab: All)

**Renders inspected:** `ui_vehicle.png` · `ui_vehicle_alarm.png` · `ui_vehicle_nofeed.png`. 2026-09-05.

**Source:** `plugin/src/pure/VehicleOverviewPage.cs` (220 lines) · `Alarms.cs:106` (`Band`) ·
`Alarms.cs:148-153` (`CabinLimits`) · `VehicleTabBar.cs` · `VehicleDeepViewLinks.cs`.

**S49's entry.** §2 rates this *"Mixed — the best-wired family. Four cabin gauges live/micro-sim;
CONSUMABLES 4 of 8 live; tab severities live"*, with H14, H16, H17, H18. **That is right about the numbers
and wrong about the colours**, which is V-01. Two of S49's holes have since been closed and one of its
supporting claims has gone out of date — recorded below so they are not re-logged.

## What was checked and found CLEAN — including two S49 holes now closed

1. ⭐ **S22's `!valid` guard has landed on this page.** `VehicleOverviewPage.cs:104-118, 141-152` route the
   seven checklist states, the four `Connected` rows and `RECORDING` through `T()`, which dashes and dims
   them on a dead feed. Confirmed on `ui_vehicle_nofeed.png`. **S49 H14 tier (i) is DONE for the Overview**
   — its remaining half (computing those words rather than dimming them) is unchanged.
2. ⭐ **S75's `SHOW MARGINS TO` fix has landed and is visible.** It is drawn in `Dim`, not `Accent`, so it
   no longer rides the idiom of the two links beside it that *are* touchable. S49 H18's "painted button"
   half is closed; its MARGIN-column half is V-03.
3. **Both touchable clusters have hit tests**: `VehicleTabBar.HitTest` and `VehicleDeepViewLinks.HitTest`,
   both routed at `FigmaUI.cs:299-307`. The eight tabs and the two deep-view links are genuinely wired.
4. **The tab severities are live.** `VehicleTabBar.Severities(s)` colours `Power` amber on this render while
   `All` carries the selection underline — a real subsystem severity reaching a tab, exactly as designed.
5. **The gauge value and the ring LENGTH come from one readout**, as the file claims — `F(s.Cabin.*01)` and
   `T(s.*Text)` are the same model. ⚠ The claim covers length only; the colour is V-01.
6. **The four dashed CONSUMABLES rows are a correct §14.4 dash, with the reason recorded**: the Orbit 1/2
   subtanks *"have no KSP counterpart, and guessing which litres belong to which subtank would be inventing
   the number"* (`VehicleOverviewPage.cs:212-214`). Genuinely-absent state → dash. Not a defect.

---

## V-01 — Every gauge ring on this page is a fixed colour, so CABIN TEMP is permanently RED — and the P&ID computes the opposite verdict for the same value in the same frame

**TIER 1** · **NEW** · S31/S32's guardrail, and C7.1's two-surfaces failure

**Evidence.** `VehicleOverviewPage.cs:120-136` — the colour argument to every `Gauge` call is a **constant**:

```csharp
Gauge(1170, 430, 175, F(s.Cabin.Ppo201),      Gold,   "PPO2",           …);
Gauge(1620, 430, 175, F(s.Cabin.CabinTemp01), Red,    "CABIN TEMP",     …);
Gauge(2070, 430, 175, F(s.Cabin.Press01),     Yellow, "CABIN PRESSURE", …);
Gauge(2520, 430, 175, F(s.Cabin.Co201),       Blue,   "CO2",            …);
…
Gauge(1230,  900, 120, F(s.Cabin.LoopA01),  Blue,   "LOOP A",   …);
Gauge(1230, 1200, 120, F(s.Cabin.LoopB01),  Blue,   "LOOP B",   …);
Gauge(2410,  900, 120, F(NetPwr01(…)),      Accent, "NET PWR1", …);
Gauge(2410, 1200, 120, F(NetPwr01(…)),      Accent, "NET PWR2", …);
```

The arc's **length** is live. Its **colour** is decoration — all eight of them, at every value.

On `ui_vehicle.png` the consequence is stark: **CABIN TEMP reads 21.8 °C and its ring is drawn red.**
The thresholds are in this repo: `Alarms.cs:153` — `CabinTempCaution = 30.0, CabinTempAlarm = 35.0`. So
`Alarms.Band(21.8, 30, 35)` is **Nominal**. Three of the four top gauges show a non-nominal colour on a
nominal reading (PPO2 gold at 2.86 psia, PRESSURE yellow at 14.72 psia, TEMP red at 21.8 °C).

**And the build already computes this correctly, twice, elsewhere:**

| page | line | what it does with the same quantity |
|---|---|---|
| `SystemsPidPage` | `:249` | `Alarms.Colour(Alarms.Band(s.Cabin.CabinTempC, CabinLimits.CabinTempCaution, CabinLimits.CabinTempAlarm))` |
| `SystemsPidPage` | `:208-210` | the same for Loop A and Loop B |
| `VehicleSubsystemPage` | `:519-521` | the same for Loop A and Loop B |

So in one frame, on one vessel, the Vehicle Overview says CABIN TEMP is in alarm and the Systems P&ID says
it is nominal. That is C7.1's failure mode on the glass, and S49 H15's class — *"status words contradict
live state already on the same screen"* — one level up, because a red ring outranks a word.

⚠ **Red is the strongest signal this UI has**, and §14.4(a) is explicit: **no red for something that is not
a fault.** A permanently-red cabin-temperature ring also destroys the signal — when the cabin really does
pass 30 °C, nothing on this page changes.

**Fix plan.**
- Replace each constant with the computed severity colour: `Alarms.Colour(Alarms.Band(value, caution,
  alarm))`, taking the raw quantity (`s.Cabin.CabinTempC`, `Co2MmHg`, `LoopAC`, `LoopBC`, …) and the
  `CabinLimits` constants — **exactly the call `SystemsPidPage.cs:249` already makes.** Do not write a
  second banding rule; route through the existing one so the two surfaces cannot disagree again (T14's rule,
  and the reason `SystemsTreePage` and the console plate share a dispatcher).
- **PPO2 and CABIN PRESSURE need their thresholds checked, not assumed.** `Alarms.LifeSupport(s.Cabin)`
  already folds PPO2 and pressure into a severity (`Alarms.cs:140` area); if a per-gauge band is wanted,
  the constants must come from `CabinLimits`, and any that does not exist there is a **§1.4 question, not a
  build-chat number**. ⚠ `CabinLimits` is also mirrored into Python for the BlackBox report generator
  (`REGISTER.md` BB3-Q1, still open) — a new constant has to land in both or the report drifts.
- **NET PWR1/2 are signed and are a different case.** Their ring already carries magnitude against a stated
  full scale and the sign lives in the number (`NetPwr01`, `:190-196`). A severity colour there means
  "discharging faster than X", which needs a threshold nothing currently defines — leave `Accent` and say
  so, rather than inventing one.
- **Must not break:** the `!valid` path. On a dead feed the ring is already empty and the value dashed;
  the colour must not become a confident nominal green on no data.
- **Verify:** `ui_vehicle.png` with the current fixture must show four nominal rings; add a fixture at
  32 °C and check the ring goes caution on **both** this page and the P&ID, from the same call.


### ✅ FIXED 2026-09-05 — S104 (QC batch 2)

**Fixed.** All eight rings now take `Alarms.GaugeColour(Alarms.Band(raw, caution, alarm), valid)` —
PPO2, CABIN TEMP, CABIN PRESSURE, CO2, LOOP A and LOOP B — the same call `SystemsPidPage.cs:249` already
made. **NET PWR 1/2 deliberately keep `Accent`**: nothing in the model bands net power, and inventing a
threshold to justify a colour is this finding, not its fix.

⚠ **The filed fix plan's §1.4 caution was wrong and is withdrawn.** It said PPO2's and pressure's bands
"must come from `CabinLimits`, and any that does not exist there is a §1.4 question". **They both exist**
(`Ppo2Caution 2.5 / Ppo2Alarm 2.0`, `PressCaution 13.0 / PressAlarm 11.0`) and `Alarms.Band` already handles
the low side. There was no gate.

**On the glass:** CABIN TEMP at 21.8 °C renders **green** on `ui_vehicle.png`, agreeing with the P&ID.
An alarm-red pixel sweep of the page returns **0**.
---

## V-02 — `CABIN MICS: RECORDING` is drawn in alarm red for a state that is not a fault

**TIER 2** · **NEW**

**Evidence.** `VehicleOverviewPage.cs:152` —
`dl.Text(T("RECORDING"), …, valid ? Red : Dim);` — and on `ui_vehicle.png` the word reads red beside four
green `Connected` rows.

**What is wrong.** Recording is a normal operating state, not a failure. §14.4(a)'s rule is quoted in
CLAUDE.md itself: *"no red"* for something that is not a fault. The colour is also a literal: the word is
reference copy (the file says so at `:141-143`), so this is a hardcoded verdict *and* a hardcoded severity
on top of it — the same defect class as V-01, in text.

⚠ **It is `!valid`-guarded** (S22's fix reaches it), so on a dead feed it dims correctly. The defect is only
in the live branch's colour choice.

**Fix plan.**
- Draw it in `White` or `Go` — a recording indicator is a state, and if it is to carry a colour at all,
  green/on is the honest one. Red should be reserved for a *failed* recorder, which nothing models today.
- **If the reference render shows it red**, that is a §1.4 deviation to record rather than silently change —
  but note the reference is a static mockup and §14.4(a) is a standing decision about *this* build's colour
  language. Recommend following §14.4(a) and recording the deviation.
- **Must not break:** the `T()` dash-on-dead-feed behaviour.
- **Verify:** re-render; no red on a page with no fault.


### ✅ FIXED 2026-09-05 — S104 (QC batch 2)

**Fixed.** `RECORDING` is drawn `Go`, not `Red`. It matches the four `Connected` rows immediately above it
in the same block — the same kind of thing, a state that is currently true — and a *failed* recorder, which
nothing models, would be the red case. The `!valid` dash path is unchanged.
---

## V-03 — The MARGIN column is eight dashes while the margins are computed every frame and written to the black box

**TIER 2** · updates S49 **H18**, whose supporting claim is now out of date

**Evidence.** `ui_vehicle.png`, right column: `MARGIN` is a header over **eight `—`**
(`VehicleOverviewPage.cs:161` draws `R(Dash, 3360, y, 25, Dim)` unconditionally, for every row).

**S49 H18 says `LifeSupport.Margins` "has no caller anywhere."** That was true when S49 was written and is
not true now — BB1/BB2 wired it:

- `plugin/src/LifeSupportBridge.cs:52-60` — `Margins(Vessel v)` off real TAC-LS food/water/oxygen
- `plugin/src/BlackBoxRecorder.cs:1149-1150` — `ls = LifeSupportBridge.Margins(v)`
- `plugin/src/pure/blackbox/BlackBoxSchema.cs:458-459` — `ls_present`, `ls_o2_days` … recorded per flight

So the margins are **computed every frame and written to disk, and the crew's own MARGIN column shows a
dash.** ⚠ **This is the second instance of that exact pattern** — H-05 found the alarm mask computed,
recorded to the BlackBox at `ScreenPainter.cs:652`, and discarded from the glass. Two channels now go to the
recorder and not to the screen.

**Fix plan.**
- Draw the MARGIN column from `LsMargins`. The bridge already returns days-remaining per consumable, which
  is the shape the column wants; the rows that have no margin (the subtanks) keep their dash, correctly.
- **Route it the same way the recorder does** — through `LifeSupportBridge`, not a second computation —
  so the glass and the black box can never report different margins for the same flight.
- ⚠ **`SHOW MARGINS TO` stays inert until this lands.** S75's comment states the condition precisely:
  *"When the MARGIN column reads modelled margins and a target set is settled, this goes back to Accent AND
  gains a rect — the two happen together or not at all."* Filling the column is half of that; the target set
  is still a §1.4 question (S76), so the control does **not** become touchable in the same pass.
- **Must not break:** rows 4–7 must keep dashing. A margin for a quantity with no quantity is worse than
  no margin.
- **Verify:** re-render; four rows with margins, four with dashes, and the same numbers as a BlackBox
  recording of the same frame.

---

---

# PAGES 20–25 — THE SIX VEHICLE SUBSYSTEM SUB-TABS

**One source file, one section.** `FigmaUI.cs:210-215` routes all six to
`VehicleSubsystemPage.Build(…, Sub.Crew | Propulsion | Power | Avionics | Gnc | Thermal, …)` — 580 lines
with a per-subsystem descriptor. Six pages, one layout, one set of defects.

**Renders inspected:** `ui_vehiclecrew.png` · `ui_vehiclecrew_alerts.png` · `ui_vehiclecrew_nofeed.png` ·
`ui_vehiclepropulsion.png` (+ `_alerts`, `_firing`, `_kerabsent`) · `ui_vehiclepower.png` (+ `_alarm`,
`_alerts`) · `ui_vehicleavionics.png` (+ `_commoff`) · `ui_vehiclegnc.png` · `ui_vehiclethermal.png`.

**S49's entry.** §2: *"Gauges and detail rows substantially live; **31 status words are literals and, unlike
the Overview, are not even `!Valid`-guarded**"* (H14, H16, H17). ⚠ **The guard half is now WRONG — S51 fixed
it** (see CLEAN 1). The literal half is right but has improved and is now measurable: **S-03**.

## What was checked and found CLEAN — S51 has closed two of S49's holes here

1. ⭐ **S49 H14's headline claim is out of date.** `VehicleSubsystemPage.cs:123-144` now carries the guard,
   with the file's own note: *"S51 / audit H14: THIS COLUMN NEVER GOT S22'S GUARD … The guard is now the
   overview's, verbatim."* `ckValid` / `CT()` dim the whole row on a dead feed. Confirmed on
   `ui_vehiclecrew_nofeed.png`. **Do not re-log H14 tier (i) against these pages.**
2. ⭐ **S49 H15's contradictions are fixed, including the one it led with.** `SMOKE DETECT` now reads the
   live fire model — `smoke ? "Detected" : "Clear"` off `st.Systems.Fire` (`:335-337`), the same source the
   P&ID prints — **word and colour together**, which was the trap worth checking. Loops A/B, the heat
   shield, S-band, RCS authority, the buses and the batteries are all computed too.
3. **The FUNCTIONS | ALERTS toggle obeys the shared-rectangle rule.** *"The two words are hit-tested from
   the SAME TabX/TabW below that place them"* (`:213-215`) — the discipline H-04 breaks and `MenuPage`
   models.
4. **The Prop tab's sixth row is an honest `Dash`**, not a literal — a genuinely-absent state, correctly
   dashed.
5. **The ALERTS view dashes on a dead feed.** S49 H16's *"prints a green NOMINAL on a dead feed beside its
   own honest NO DATA"* is fixed — `:190-192` now dims the word and prints `NO DATA`. Confirmed.

---

## S-01 — V-01's real scope: all 24 sub-tab gauge colours are constants too, so CABIN TEMP is permanently red on two different pages

**TIER 1** · **NEW** · the same defect and the same fix as **V-01**

**Evidence.** Six `GCol` arrays, all literal (`VehicleSubsystemPage.cs`):

```
:345 Crew      { Gold, Red, Yellow, Blue }      <- byte-identical to the Overview's
:376 Prop      { Gold, Gold, Blue, Red }
:424 Power     { Accent, Accent, Accent, Yellow }
:459 Avionics  { Accent, Accent, Go, Blue }
:490 GNC       { Accent, Accent, Accent, Gold }
:533 Thermal   { Blue, Blue, Accent, Red }
```

On `ui_vehiclecrew.png`, **CABIN TEMP reads 21.8 °C in a red ring** — the same nominal value, the same false
alarm, on a second page. Prop and Thermal each carry a permanently-red gauge of their own.

**So V-01 is not a one-page defect: it is 32 gauges across 7 pages** — 8 on the Overview, 24 here — every one
of them a fixed colour over a live length.

**Fix plan.** **One fix, seven pages.** `Gauge()` has the same signature in both files; changing the colour
argument to `Alarms.Colour(Alarms.Band(raw, caution, alarm))` at the descriptor sites resolves all 32.
V-01's plan applies unchanged, including its two cautions:
- thresholds come from `CabinLimits`, and anything not already there is a **§1.4 question**, not a
  build-chat number;
- **`Accent` on a gauge with no defined threshold should stay `Accent`** — 12 of the 24 are `Accent`
  already, which is the honest "this is a reading, not a verdict" colour. Do not invent a band to justify
  colouring them.
- ⚠ **`Go` at `:459` (Avionics gauge 3) is the opposite failure**: a permanently *green* ring is a
  hardcoded all-clear, which is S31/S32's guardrail read the other way. It is the one that most needs a
  model behind it or a demotion to `Accent`.


### ✅ FIXED 2026-09-05 — S104 (QC batch 2)

**Fixed with V-01, one rule across both files.** All 24 sub-tab rings are computed where the model bands the
quantity and `Accent` where it does not:

| tab | computed | left `Accent`, and why |
|---|---|---|
| Crew | all four `CabinLimits` quantities | — |
| Prop | OX, FUEL via `Alarms.Low` — the same 0..1 band `PropellantSeverity` applies | HELIUM, PROP TEMP — **dashes** |
| Power | `Alarms.Low(Power01)`, as `VehicleSeverity` reads it | BUS A/B (dashes), ARRAY kW (no band) |
| Avionics | — | **all four are dashes** |
| GNC | `Alarms.Low(DragonProp01)` | three body rates (no band) |
| Thermal | LOOP A, LOOP B | RADIATOR (dash), SHIELD (see below) |

⚠ **This finding's own warning was heeded twice.** Avionics' `Go` was a hardcoded **green all-clear on a
gauge with no reading** — S31/S32 inverted — and is now neutral. And SHIELD stayed `Accent` rather than
gaining a band, because what `HullTemp01` is normalised against is not established here; a band invented to
justify a colour is the defect.
---

## S-02 — "ALERT ACTIVITY" resolves to one word, and the FDIR bar beside it is a fake three-position gauge

**TIER 2** · confirms S49 **H16** · ⚠ links to **H-05**

**Evidence.** `ui_vehiclepower_alerts.png`. The ALERTS view fills a whole screen with:

- the heading `ALERT ACTIVITY`, a rule, and **one word — `CAUTION`** — in 110-design-px amber;
- an `FDIR` label, the word `NOMINAL`, and a bar filled about 15%.

The bar's fill is `VehicleSubsystemPage.cs:199-201`:

```csharp
float fdirFrac = Alarms.FdirSeverity(s) == Severity.Nominal ? 0.15f
               : Alarms.FdirSeverity(s) == Severity.Caution ? 0.6f : 1f;
```

**A bar whose fill is a lookup from a three-valued enum is not a gauge.** It is drawn in the same idiom as
the four real detail bars on the FUNCTIONS view — same width, same track, same `Accent`-family fill — so it
reads as a continuous measurement of something. It measures nothing.

**And the word is the whole alert surface.** No enumerated list, no timestamps, no which-subsystem, no
acknowledgement. The crew learns *that* something is in caution and nothing about *what* — on the page whose
title is ALERT ACTIVITY. Everything else on the screen is the capsule illustration and empty space.

⚠ **This is the same heading as the HUD's, and the HUD's is empty (H-05).** Two surfaces titled ALERT
ACTIVITY: one shows a single computed word, the other shows nothing at all, and neither enumerates an alert.
**They should be built once and shared**, not twice.

**Fix plan.**
- Build the enumerated list H-05's plan describes — one row per set bit of `Alarms.Mask(ps)` plus
  `SystemsState`'s discrete conditions (fire, leak, tripped strings, bus 0/3) — and **use it on both
  surfaces**. The severity word stays as the summary above the list.
- **Retire the FDIR bar or make it real.** Two honest options: (a) drop the bar and keep the word, which
  loses nothing — the word already carries the state; (b) if a bar is wanted, it must measure something
  continuous, and nothing in `Fdir` currently is. **(a) is recommended**; a decorative bar in the same
  idiom as four real ones is the defect.
- ⚠ **Scope, per §14.4(f):** the list built from `Alarms` + `Systems` is **(A)** and buildable now. The FDIR
  *channel* is Part B's — the stub pins `Fault`/`FaultResponse`/`FaultText` (S49 §1.2) — so the FDIR row
  stays an honest no-op until then.
- **Must not break:** `LiveSeverity` drives both the ALERTS content and the tab colour, *"so the toggle
  content and the red-nav can never say different things"* (`:183-185`). Any list must read the same source.

---

## S-03 — 23 of the 36 subsystem state words are still literals

**TIER 2** · S49 **H14** tier (ii), now counted

**Evidence.** Six tabs × six rows = 36 words. Counted from the six `CkState` arrays:

| tab | line | literal | computed | dash |
|---|---|---|---|---|
| Crew | `:336` | 5 | 1 *(smoke)* | — |
| Prop | `:367` | 4 | 1 *(rcsUp)* | 1 |
| Power | `:413` | 2 | 4 *(2 × `BusWord`, battery, solar)* | — |
| Avionics | `:442` | 5 | 1 *(S-band)* | — |
| GNC | `:479` | 4 | 2 *(RCS authority, mode)* | — |
| Thermal | `:524` | 3 | 3 *(loop A, loop B, shield)* | — |
| **total** | | **23** | **12** | **1** |

S49 said 31 literals and no guard; S51 brought it to **23 literals with the guard in place**. The remaining
23 include `"16 / 16"`, `"3 / 3"`, `"2 / 2"`, `"Lock"` ×2, `"Armed"` ×2, `"Open"`, `"Deployed"`, `"Auto"`,
`"Valid"`, `"Active"`, `"Standby"` and nine `"Nominal"`.

**What is wrong.** Under §14.4(f) a status word is a READOUT and must be filled from a live source or a
marked model — a nominal word with nothing behind it is a defect, not a placeholder. These are dimmed on a
dead feed (S51), which removes the *contradiction*, but a live feed still prints twelve confident verdicts
that nothing computed.

**Fix plan.**
- **Sort them before building any.** Three groups, and only the first is straightforward:
  1. **Countable** — `"16 / 16"`, `"3 / 3"`, `"2 / 2"`: these are *n of m* counts of real things (Draco
     thrusters, flight computers, batteries). Where the vessel has the parts, count them; `BusWord`'s
     pattern applies directly.
  2. **State words with a live source that is simply not read yet** — `"Open"`, `"Armed"`, `"Deployed"`,
     `"Lock"`. Each needs one field identified; some may already exist in `SystemsState`.
  3. **The nine `"Nominal"`s and `"Auto"`/`"Valid"`/`"Active"`/`"Standby"`** — these are *verdicts*, and a
     verdict needs a model. Under S31/S32 they must be computed from something or become dashes. ⚠ **This
     is a policy call, not a build: it is S49's own Q3** (*"a large surface and a policy question, not one
     build"*), and it is still open. **Do not invent bands for them.**
- **Recommended sequencing:** group 1, then group 2 one field at a time, and hold group 3 behind Q3.
- **Must not break:** the `CT()` guard and the `CkKey` colour must move together with each word — S51's
  lesson, and the reason `SMOKE DETECT` is now correct in both.
- **Verify:** per tab, a live render and a `_nofeed` render; every computed word must change between them.

---

## S-04 — The dash surface is S49 H17's, unchanged, and it is a standing policy question rather than a per-page defect

**TIER 3** *(owner policy — S49 §8's Q3, still open)* · recorded, not re-litigated

**Evidence.** `ui_vehiclecrew.png` right column: **`Humidity` is a dash with an empty bar**, beside three
live rows (O2 Tank 86%, N2 Tank 93%, Potable Water 108 L) and a live `Crew Aboard 3 / 4`. S49 H17 lists
~27 such rows across these six tabs — Humidity, Chamber Press, SuperDraco Temp, HELIUM, PROP TEMP, bus
voltages, Bus Load, Battery Temp, FC LOAD, BUS TRAFFIC, LINK MARGIN, STORAGE, FC1-3, GPS Sats, Data Rate,
RADIATOR, loop flows, Heat Reject, Cabin HX, the TPS rows.

**What is wrong — and why this is not a new finding.** Before §14.4(f) these dashes were correct. After it,
a dash survives only for a *genuinely-absent* state, and every one of these is a physically-real Dragon
quantity. But S49 already put this to the owner as **Q3** — *"a large surface and a policy question, not one
build"* — and **there is no ruling on record.** This QC pass confirms the surface is unchanged at HEAD and
adds nothing to the question.

**Fix plan.** None proposed; the question is open and is the owner's. What this pass can add:
- The Avionics tab's own comment (`:444`) already states the honest position — *"MOST OF THIS TAB STILL
  DASHES, AND THAT IS THE ANSWER"* — which is the right posture until Q3 is answered.
- ⚠ **C1.15 applies to whatever is built.** Before any of these is simulated, the task's deliverable must
  record a documented search against `docs/reference/INSTALLED_MODS.md` — what was searched for, what
  candidates exist, why each was accepted or rejected. Several of these quantities plausibly have real
  sources already installed (TAC-LS for humidity-adjacent state, RealFuels for propellant temperature,
  TestFlight for component reliability), and C1.15 exists precisely because a screens pass began inventing
  simulations for adjacent quantities without checking.

---

---

# PAGE 17 — MECH PANEL

**Renders inspected:** `ui_vehiclemech.png` · `ui_vehiclemech_alarm.png` · `ui_vehiclemech_nofeed.png`.

**Source:** `plugin/src/pure/VehicleMechPage.cs` (166 lines).

**S49's entry.** §2: *"Three of five donut nodes live off real acceleration; SEAT n TACH ×4 dashed;
`Awaiting` static"* (H14, H15). Confirmed exactly. The render adds one thing S49 could not see from a single
file: what `Awaiting` looks like **beside the other page that names the same check**.

## What was checked and found CLEAN

1. **Three donuts are genuinely live** off real acceleration — `ACCELERATION 1.42 g`, `CENTRIPETAL 0.881 g`,
   `RESISTANCE 0.00 g` — and `PRESSURE 14.72 psia` is live too (four of five).
2. **The dead-feed guard is present** — `s.Valid ? … : Dash` on the status word (`:135`), and the dashed
   rows dash rather than zeroing.
3. **The donut colour here is `Accent`, which is the honest choice** — a reading, not a verdict. ⚠ It is
   also the reason MP-02 is a contradiction rather than a second instance of V-01.
4. **No HitTest, and none owed** — every touchable thing on the page (the eight tabs, the two deep-view
   links) belongs to `VehicleTabBar` / `VehicleDeepViewLinks`, which have their own, and are routed at
   `FigmaUI.cs:299-307`.

---

## MP-01 — `ALL SYSTEMS CHECK` reads "Normal" on the Vehicle Overview and "Awaiting" in caution amber on the Mech Panel, in the same frame

**TIER 1** · **NEW** · S49 **H15**'s class, across two pages

**Evidence.** The same named check, on two pages the crew reaches with one tap of the same tab strip:

| page | source | word | colour |
|---|---|---|---|
| Vehicle Overview | `VehicleOverviewPage.cs:61` — `ChkState[0] = "Normal"`, `ChkKey[0] = 0` | **Normal** | White |
| Mech Panel | `VehicleMechPage.cs:135` — `C(s.Valid ? "Awaiting" : Dash, …, Amber)` | **Awaiting** | **Amber (caution)** |

Both are hardcoded. Neither is computed from anything. On `ui_vehicle.png` and `ui_vehiclemech.png` — the
same fixture, the same frame — the crew is told the all-systems check is complete and normal, and that it is
still awaiting, one tab apart.

**What is wrong.** S49 H15 catalogues status words that contradict *live state on the same screen*. This is
the harder version: two hardcoded words contradicting **each other** across screens, with no model behind
either, so there is no fact of the matter to appeal to. And the Mech Panel's version is amber — a standing
caution the crew can never clear, on a check that the neighbouring page says passed.

⚠ **Note the asymmetry that makes this worse than V-02:** the Overview's `ALL SYSTEMS CHECK` is drawn in
*White* (`ChkKey = 0`, neutral), so it does not even claim to be a verdict. The Mech Panel's is amber, which
does. The two disagree on the *word*, the *colour* and the *claim*.

**Fix plan.**
- **One check, one source.** Whatever `ALL SYSTEMS CHECK` means, both pages must read it from the same
  place — the rule T14 established for the systems tree and the console plate, and the rule V-01 needs for
  the cabin bands.
- **The obvious source already exists:** `Alarms.SystemSeverity(ps)` is the build's authoritative
  vehicle-wide verdict — it folds the FDIR spine, propellant, power and crew environment, and
  `Alarms.Word()` already turns it into a word. `ScreenPainter.cs:1123` computes it every frame. An
  `ALL SYSTEMS CHECK` driven by it would be live on both pages, and would say the same thing on both.
- ⚠ **"Awaiting" is not a severity**, so it cannot come from `Alarms.Word` — it is a *procedural* state
  (a check not yet run). If the row is meant to be procedural rather than a health verdict, it needs a
  step model, and there is none on either page. **That is the decision to make: is `ALL SYSTEMS CHECK` a
  health verdict (A, buildable now off `SystemSeverity`) or a procedure step (needs a model, and the only
  real step machine in the tree — `StepList` — is stranded per S49 §1.1)?** Recommend the health-verdict
  reading: it is what the Overview's neutral White already implies, and it is buildable today.
- **Must not break:** the `!valid` dash on both pages.
- **Verify:** render both pages from one fixture and require the same word and the same colour; then trip a
  subsystem and require both to change together.


### ✅ FIXED 2026-09-05 — S104 (QC batch 2)

**PARTLY fixed — and confirming before fixing changed what "fixed" means here.**

⚠ **The words are reference copy and are not ours to change.** `VehicleMechPage.cs:26-27` states it: *"§6
scopes this task to the VALUES, so the reference COPY — the node names, 'SEAT n TACH', and the 'ALL SYSTEMS
CHECK / Awaiting' line under the seats — is reproduced untouched."* The Overview's "Normal" for the same
named row is reference copy too, from a different mockup. **So the contradiction is between two reference
sources, not between two of our choices**, and §1.4 reproduces each faithfully. The filed fix — drive both
from `Alarms.SystemSeverity` — would have overwritten reference copy on both pages.

**What WAS ours, and is fixed:** the **caution amber**. That severity was painted on top of a reproduced
word by this build, and §14.4(a) forbids spending a fault colour on a non-fault. The row now reads **White**
— deliberately not green, because a hardcoded all-clear is the same defect inverted. `ui_vehiclemech.png`
returns **0 alarm-red pixels** and carries no false caution.

**STILL OPEN, and it is an owner question:** two reference mockups give one named check two different
states. Options unchanged from the fix plan above — health verdict (buildable off `SystemSeverity`) or
procedure step (needs the stranded `StepList`) — but **whichever is chosen, it overrides reference copy on
at least one page, so it is §1.4's and the owner's, not a build chat's.**
---

## MP-02 — The same 14.72 psia is called PRESSURE here and CABIN PRESSURE / CABIN PRESS elsewhere, and drawn in three different colours

**TIER 2** · **NEW** · the third surface of **V-01 / S-01**

**Evidence.** One quantity, three pages, in the same frame:

| page | label | value | ring colour |
|---|---|---|---|
| Vehicle Overview | `CABIN PRESSURE` | 14.72 psia | **Yellow** (constant) |
| Vehicle — Crew | `CABIN PRESS` | 14.72 psia | **Yellow** (constant) |
| Mech Panel | `PRESSURE` | 14.72 psia | **Accent** (constant) |

**Three names and two colours for one reading.** The Mech Panel's `Accent` is the *honest* one — a reading,
not a verdict — which is exactly why the disagreement matters: the two Vehicle pages are asserting a caution
that this page does not, about the same number, at the same instant.

**Fix plan.**
- **Colour** is V-01/S-01's fix and needs nothing extra here: once the two Vehicle pages compute their band,
  all three agree, and the Mech Panel's `Accent` either stays (no threshold defined) or joins them.
- **Naming is a separate, cheap consistency fix.** `CABIN PRESSURE` / `CABIN PRESS` / `PRESSURE` for one
  quantity across three sibling pages is the kind of drift `docs/REFERENCE_PAGES.md` exists to prevent.
  ⚠ **Check the reference before renaming** (§1.4): if the real screens label them differently on different
  pages, that is a real deviation to preserve, not a bug to fix. If they do not, pick one and record it.
- **Must not break:** the Mech Panel's `PRESSURE` label may be the reference's own word for a *structural*
  pressure rather than cabin pressure — in which case the defect is not the label but the fact that it is
  wired to `Press01`. **Confirm which quantity the reference means before changing either.**
- **Verify:** one label per quantity across the Vehicle family, and one colour rule.

---

## MP-03 — Five of the page's nine readouts are dashes, and the empty centre circle is the visible consequence

**TIER 3** *(S49 H17 / Q3's surface — recorded, not re-litigated)*

**Evidence.** `ui_vehiclemech.png`: the four `SEAT n TACH` rows are `—`, and the `WATER UPRIGHTING` donut
shows a dash with an empty ring. Four live readouts (ACCELERATION, CENTRIPETAL, RESISTANCE, PRESSURE), five
dead.

**The layout consequence is visible and is the owner's standing concern (R-4).** The page's dominant
element is a ~440-px-diameter circle whose entire content is a heading, four dashes and a hardcoded status
word — roughly a third of the screen's area carrying one live fact between them (and MP-01 says that fact is
wrong). The circle is not badly laid out; **it is correctly laid out around content that is not there.**

**Fix plan.** No new question — this is S49 H17's surface and **Q3 governs it**, unanswered. What this pass
adds:
- **`WATER UPRIGHTING` is the one worth separating from the rest.** It is a real Dragon system (the
  uprighting bags) with a real binary state, not a continuous quantity — so it is likelier to have an
  honest live or micro-sim source than the four tachs, and it is drawn as a *donut*, which is the wrong
  instrument for a binary. Worth its own look when Q3 is answered.
- ⚠ **`SEAT n TACH` should not be simulated on a guess.** No source in the repo says what a seat tachometer
  measures on this vehicle; inventing one would be a §1.4 tier-3 invention. **If Q3 resolves toward
  filling, these four need a source first** — and C1.15 requires the `INSTALLED_MODS` search to be
  documented before any simulation is written.
- **Do not restyle the circle to hide the dashes.** The emptiness is honest; it is the readouts that are
  missing, and shrinking the circle would remove the evidence.


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `ui_vehiclemech.png`, 1280×703.  **Verdict: STANDS on its substance — but the figure in the
filed text was wrong at BOTH scales and is corrected here.**

The counts are render-independent and unchanged: **five of nine readouts are dashes** (four `SEAT n TACH`
plus `WATER UPRIGHTING`), four live (ACCELERATION, CENTRIPETAL, RESISTANCE, PRESSURE).

The hub circle, measured from the source constants (`ccx 1713, ccy 1040, ring 440`) and confirmed on the
render:

| | filed prose | **measured (identical at both scales)** |
|---|---|---|
| radius | *"~440 px diameter"* | `SZ(440)` = **146.5 px**, diameter **293 px** |
| share of the body | *"roughly a third of the screen's area"* | **8.4% of the area** · **47% of the height** |

⚠ **That was a description error, not a scale artifact** — 8.4% / 47% is what it measures at 2560 as well. I
was describing the circle's *height* share and called it *area*. Corrected.

The substance is untouched: a 293-px circle taking nearly half the body's height, containing a heading, four
dashes and one hardcoded word (**MP-01**). ⚠ **New at the shipped width:** those `SEAT n TACH` rows are drawn
at 26 design px = **8.7 panel px, 54% of the legibility floor** — so even when they are filled they will not
read. See **R-01**.
---

---

# PAGE 16 — SUIT LEAK CHECK

**Renders inspected:** `ui_suitcheck.png` · `ui_suitcheck_leak.png` · `ui_suitcheck_popup.png` ·
`ui_suitcheck_leak_popup.png`.

**Source:** `plugin/src/pure/SuitCheckPage.cs` (326 lines) · `SuitLeak` · `ScreenPainter.cs:368-401`.

**S49's entry.** §2: *"The exemplar MICRO-SIM (S31/S32) — four ΔP rows and the STATUS verdict computed from
`SuitLeakSim`, never hardcoded. But no step is tracked: both left ticks draw checked at page-open, and
'SECTION 2: IN PROGRESS' never advances"* (H19, H20). **Both halves confirmed exactly.**

## What was checked and found CLEAN — this is the page the rest of the build should be measured against

1. ⭐ **The verdict is computed, and the render proves it.** `ui_suitcheck_leak.png`: `SUIT 3 DELTA
   PRESSURE 0.01psi` → `SUIT 3 STATUS Failed Low` in amber with an amber marker, while suits 1, 2 and 4
   read 0.28 / 0.26 / 0.27 psi → `Nominal` in green. **The status word follows the number.** That is
   S31/S32 satisfied, and so far it is the only page inspected where a safety verdict has a model behind
   it. Every hardcoded-verdict finding in this document — C-08, V-01, V-02, MP-01, S-01, S-03 — describes a
   page that does not do what this one does.
2. **The failure branch is gated on the model, not on the press.** `Available(SuitAct.Troubleshoot, suits)`
   returns `FailBranchLive && suits.AnyFailed` (`:297`), and the control is tinted from the same predicate
   (`:213-215`) — *"a dimmed TROUBLESHOOT cannot act, and a live one cannot look unavailable"*
   (`ScreenPainter.cs:376-378`). Draw state and act state from one source.
3. **Six controls are hit-tested from the rectangles that draw them** — INITIATE, HALT, FINISH,
   TROUBLESHOOT, TRY ADDITIONAL TIMER and the popup Close (`:301-318`), dispatched at
   `ScreenPainter.cs:379-400`.
4. **The dead-feed path is honest:** `suits.Valid ? "ic_refresh" : "ic_dash"` (`:156`), and the status
   markers dash rather than holding a stale verdict.
5. **The run seed is owned by the painter and re-rolled per run**, so a second timed check genuinely
   re-rolls rather than repeating the first — `StartSuitRun()` on both START and TRY ADDITIONAL TIMER.

---

## SC-01 — The two-step procedure is drawn complete before it starts, and its section header never advances

**TIER 2** · confirms S49 **H20**

**Evidence.** `SuitCheckPage.cs:94-95`:

```csharp
Ico("ic_check", 120, 452, 38, White); L("1. PREPARE SUITS FOR LEAK CHECK", 176, 458, 26, White);
Ico("ic_check", 120, 560, 38, White); L("2. EXECUTE SUIT LEAK CHECK", 176, 566, 26, White);
```

`ic_check` is passed **unconditionally**, in White, for both rows — no state, no `suits`, no countdown. On
`ui_suitcheck.png`, before any run, both steps already carry a tick. And `:109` prints
`SECTION 2: IN PROGRESS` as a literal, so the header says IN PROGRESS in every state the page has —
including after FINISH has raised the result popup.

**What is wrong.** The page tracks the *measurement* beautifully and the *procedure* not at all. The left
column says both steps are done; the header says section 2 is in progress; the run may not have started.
The three statements cannot all be true and none of them is computed.

⚠ **This is the page-level instance of S49's central conclusion** — *"no procedure page in the build is
step-tracked"* — on the one page that already holds the state needed to do it.

**Fix plan.**
- **The state exists in the painter already:** `suitStart`, `suitCountdown`, `suitPopup`, `suitSeed`
  (`ScreenPainter.cs:368-400`) fully determine *not started / running / finished*, and `Build` is already
  handed `suitCountdown` and `suitPopup`. Step 1 ticks once a run has been started; step 2 ticks once one
  has completed; the header reads `NOT STARTED / IN PROGRESS / COMPLETE` off the same three-way.
- **No new model, no new source, no §1.4 question** — this is routing state the page is already given.
- ⚠ **`suits.AnyFailed` must not tick step 2 as a pass.** A completed check with a failed suit is
  *complete*, not *nominal*: the tick means "done", the STATUS column means "passed". Keep them separate or
  the page starts declaring a verdict in two places — the defect this page otherwise avoids.
- **Must not break:** the popup flow and the `!Valid` dash path.
- **Verify:** four renders — before start, running, finished-clean, finished-with-failure — each with a
  different tick/header combination.

---

## SC-02 — The two owner-ruled-inert read-only plates are still painted as live buttons

**TIER 2** · **NEW** · S29 settled the behaviour; S75 later settled the appearance and was never applied here

**Evidence.** `SuitCheckPage.cs:96-104`, with the ruling quoted in the code:

> *"read-only controls (bottom) — **S29 (owner, via the overseer, 2026-09-02): both plates stay INERT,
> drawn only, no HitTest entry.** One caption, two plates: the reference does not say which of
> `ic_grid`/`ic_eye` arms read-only mode or what the other one does, so §1.4 (inert until verified) applies
> rather than inventing a real-only-console function for either."*

The decision is right and is not in question. **How they are drawn is:**

```csharp
Pl(210, 1600, 130, 130, White); Ico("ic_grid", 245, 1635, 60, White);
Pl(430, 1600, 130, 130, White); Ico("ic_eye",  465, 1635, 60, White);
```

Full plate, full border, full-white glyph — **identical in idiom to `INITIATE SUIT LEAK CHECK` and
`TROUBLESHOOT` on the same screen**, both of which act. Visible on every SuitCheck render.

**What is wrong.** S29 (2026-09-02) decided these do nothing. **S75 (2026-09-04) decided what a control that
does nothing must look like** — `DragonPalette.Text6`, the *"nothing live behind this"* tint — and applied
it to the Cover's `gridicons_refresh`, stating the rule: *"If a real source for the action ever appears, it
goes back to White AND enters Hits — together."* S29 predates S75 by two days, so the appearance half was
never applied here. The page honours the ruling in behaviour and contradicts it in paint.

⚠ **Same class, same page:** the `ic_refresh` glyphs — one beside `SECTION 2` in `Accent` (`:108`) and one
per table row in White (`:132`, `:156`) — are also drawn in live tints with no hit rect. The Cover's
identical glyph was tinted inert by S75; these were not.

**Fix plan.**
- Draw both read-only plates, and every un-hit-testable `ic_refresh`, in `DragonPalette.Text6` — exactly
  what `CoverPage.InertTint` already is (`CoverPage.cs:215-218`).
- ⚠ **Do NOT give them hit rects.** S29 is a settled owner decision and C1.8 applies: it stands unless the
  owner types `OVERRIDE`. This fix changes the tint only, which is S75's territory.
- **Consider hoisting the idiom.** Three pages now need the same inert tint for the same reason — Cover,
  SuitCheck, and A-02/F-03's controls if **Q6** resolves that way. A shared `DragonPalette.Inert` or a
  `Control.DrawInert` helper stops the next page relearning it.
- **Must not break:** the six live controls stay White/Red. The whole point is that the two groups become
  distinguishable at a glance.
- **Verify:** on any SuitCheck render, every White control resolves to an action and every inert one is
  visibly dimmer.

---

---

# PAGES 18 + 19 — VIDEO SETTINGS and TEST VRIO HEALTH LEDS

**Renders inspected:** `ui_audiovideo.png` · `ui_vriotest.png` (also used as F-01's evidence).

**Source:** `plugin/src/pure/SettingsVideoPage.cs` (83 lines) · `plugin/src/pure/VrioTestPage.cs`.

**S49's entries.** §2 rates Video as *"Camera list is **LIVE read-only** off a real
`MuMechModuleHullCameraZoom` scan; **tapping a camera row does nothing** — its only writer is in the dead
`Apply`"* (H12), and VrioTest as *"Fully static. `Build(dl,w,h)` — **no `PageState` parameter**; checklist
state is `bool[] Done = {true,true,true,true,false}`; **no HitTest in the file and no glue branch** —
START / STOP / NEXT are pixels"* (H21). **Both confirmed verbatim at HEAD.**

## What was checked and found CLEAN

1. **The Video page's empty states are honest and well made.** With no cameras it says `no cameras on
   vehicle` in the list and `NO SIGNAL` in the viewport — not a fake feed, not a blank panel. It also has a
   third, distinct state: `FORWARD VIEW IN USE BY DOCKING` when the docking renderer holds the camera
   (`:66-67`). Three states, three honest messages.
2. ⭐ **The Video page's tab strip maps correctly, unlike the Cabin page's.** It draws with `PX(x) = x * sx`
   (`:74-78`), the exact inverse of `FigmaUI.HitTest`'s `px * RefW / w`. **F-04 is a Cabin-only defect** —
   Audio and Video both agree with the hit bands.
3. **`VrioTestPage` is the element-level rebuild F-01 recommends routing to.** Its content is complete and
   correct; what it lacks is state and touch, which is VT-01.

---

## VV-01 — The Video page prints a camera resolution beside its own "no cameras" and "NO SIGNAL"

**TIER 2** · **NEW**

**Evidence.** `ui_audiovideo.png` states three things at once:

- left column: **`no cameras on vehicle`**
- viewport: **`NO SIGNAL`**
- below the viewport: **`RESOLUTION   640 x 360`**

The third is `SettingsVideoPage.cs:72` — `R(s.CameraResText ?? "—", …)` — printed **unconditionally**. The
two honest states are branch-guarded (`:66-70` gate on `cams.Length == 0`); the resolution is not.

**What is wrong.** A resolution is a property of a camera. With no camera there is no resolution, and the
page's own `?? "—"` fallback shows the author intended a dash for exactly this case — it just never fires,
because the *field* is populated even when the *camera list* is empty. It is a small defect but a pure one:
one page, three statements, one of them impossible.

**Fix plan.**
- Gate the value on the same condition the viewport already uses:
  `R(cams.Length == 0 ? Dash : (s.CameraResText ?? Dash), …)`. One line, no new state.
- ⚠ **Check `CameraHeldByDocking` too** — when the forward view is held by docking, *this* page has no feed
  but the resolution may still be meaningful. Decide deliberately rather than by omission; recommend
  showing it in that case, since a camera exists.
- **Must not break:** the three honest empty states, which are this page's strength.
- **Verify:** three renders — no cameras, cameras present, camera held by docking — with the resolution row
  correct in each.

---

## VV-02 — The camera list is live and read-only, its writer is stranded, and the preview has never rendered the populated state

**TIER 2** · confirms S49 **H12** · ⚠ same class as **H-09**

**Evidence.** `SettingsVideoPage.cs:44-56` reads `s.CamLabels` and highlights `s.CameraView` — a genuinely
live list off a real vessel scan. There is **no `HitTest` in the file**, and `FigmaUI.HitTest`'s settings
branch (`:310-320`) resolves only the three tabs. So a camera row draws a selection the crew cannot change.
S49 records where the writer went: the only code that sets `CameraView` is in the stranded legacy `Apply`
path, unreachable under `FigmaMode` (S49 §1.1).

**And the preview cannot show any of it.** `PreviewMain.cs:180` sets `ps.CameraView = 0;
ps.CameraResText = "640 x 360"; ps.CameraHeldByDocking = false;` — and **never sets `CamLabels`**. So the
one render of this page is its empty state, and the live list, the selection highlight and the
`FORWARD VIEW IN USE BY DOCKING` branch have **never been rendered**. That is H-09's defect on a second
page: the preview gate cannot see the page's live half.

**Fix plan.**
- **Two independent fixes; do the fixture one first**, because it is free and it makes the other reviewable:
  1. **Fixture:** give `ps.CamLabels` two or three plausible names in `PreviewMain`, and add renders for
     the populated and `CameraHeldByDocking` states. Then the page's live half is on the gate.
  2. **Wiring:** re-home the stranded writer. Selecting a camera is **pure screen state** — it changes
     which feed this page shows and commands nothing — so it is (A) and buildable now. It needs a
     `SettingsVideoPage.HitTest` over the same row rects the draw uses, and a painter branch beside the
     SuitCheck one.
- ⚠ **S49 H12 groups this with the other stranded settings handlers** (lights, brightness, seat view,
  page-per-display in `SettingsPage.cs`). They are one job — *"re-home the stranded handlers onto a
  reachable Figma settings page"* — and should be scheduled together rather than one row at a time.
- **Must not break:** the three empty states, and `CameraHeldByDocking`'s precedence over a selection.
- **Verify:** the populated render shows a highlighted row; tapping a different row moves the highlight and
  the feed.

---

## VT-01 — The VRIO page takes no state, ships a literal checklist, and has no touch anywhere

**TIER 1** *(folded into **F-01**'s fix — see below)* · confirms S49 **H21**

**Evidence, all three structural and verified at HEAD:**

- `VrioTestPage.cs:36` — `public static void Build(DisplayList dl, int w, int h)` — **no `PageState`
  parameter.** The page cannot read the vehicle even in principle.
- `:34` — `static readonly bool[] Done = { true, true, true, true, false };` — the five-step DEORBIT
  checklist state is a compile-time literal. `ui_vriotest.png` shows four green ticks and one grey,
  permanently.
- **No `HitTest` in the file, and no `cur == UiPage.VrioTest` branch in `ScreenPainter`.** `START VRIO 1 LED
  TEST`, `START VRIO 2 LED TEST`, `STOP VRIO 2 LED TEST`, `NEXT` and `ENTER READ-ONLY` are all painted in
  full button idiom and none is touchable — seven controls, zero rectangles.

**What is wrong.** This is the most complete procedure screen in the build and none of it is connected. It
is also **the second rendering of the same procedure** — see **F-01** — so the build ships two versions of
one screen, neither of which tracks a step.

**Fix plan.** **Sequence matters here, and F-01 comes first.**
1. **F-01** decides which of the two renderings survives. Building state into `VrioTestPage` before that is
   decided risks doing it twice or doing it to the copy that gets dropped.
2. **Then the classification, which is not uniform** — and this is the part that must not be rushed:
   - `NEXT` and `ENTER READ-ONLY` are **navigation / screen state** → **(A)**, buildable now.
   - The five checklist ticks are **step TRACKING**, a readout of real vehicle state → **(A)** under
     §14.4(f), and S49's own distinction: *"a procedure's step TRACKING is a readout … and is therefore
     (A); the same step's ACTION BUTTON fires a pyro and is (B)."*
   - `START VRIO 1 / 2 LED TEST` and `STOP VRIO 2` **command the vehicle's health LEDs** → **(B)**, and
     they stay §14.4(a) honest-no-op until Part B. **They must not be given working rectangles in Part A.**
3. **Meanwhile, S75's tint applies to all seven now** — see **SC-02**. A control that cannot act must not be
   painted as one, whichever class it lands in.
- ⚠ **There is no step model to read from.** S49 §1.1 records that the one real step machine in the tree,
  `pure/StepList.cs`, is stranded behind `FigmaMode`. Tracking these five steps means routing it, which is
  H34's job and a build of its own — not a line item on this page.
- **Verify:** after F-01, one render of this procedure; after step tracking, the ticks change with vehicle
  state; after the tint pass, the two (B) buttons are visibly inert.

---

---

# PAGE 26 — MANUAL CHUTE DEPLOY

**Renders inspected:** `ui_manualchute.png` · `ui_manualchute_armed.png` · `ui_manualchute_descent.png` ·
`ui_manualchute_nofeed.png`.

**Source:** `plugin/src/pure/ManualChuteDeployPage.cs` (268 lines) · `ScreenPainter.ChuteAction`.

**S49's entry.** §2: *"Telemetry strip fully live (T13c) plus the live globe. Step rows are static strings;
8 of 12 action buttons are no-ops; the rail index is the literal 6"* (H22, H23). The live half is confirmed
and is **more important than S49 could show**; the "no-ops" phrasing needs correcting (CLEAN 2).

## What was checked and found CLEAN

1. ⭐⭐ **This page is C-01's fix, already built and working.** Its top strip is drawn, not baked, and every
   value matches the fixture exactly in the same frame:

   | readout | on the glass | fixture |
   |---|---|---|
   | ACTIVE PHASE | `ORBITING` | `ps.Phase = "ORBITING"` |
   | INERTIAL VELOCITY | `2280 m/s` | `ps.Velocity = "2280 m/s"` |
   | ALTITUDE | `123.4 km` | `ps.Altitude = "123.4 km"` |
   | APOGEE | `124.0 km` | `ps.Apoapsis = "124.0 km"` |
   | PERIGEE | `121.9 km` | `ps.Periapsis = "121.9 km"` |
   | INCLINATION | `0.13°` | `ps.InclinationDegText = "0.13°"` |

   **The Cover's C-01 is the same seven values on a page that shares this page's own rail** — baked there,
   live here. S49 H1 predicted this method would work; the render proves it does. **Cite this page when
   C-01 is built.** ⚠ It is also visibly *more legible* than the Cover's baked strip: drawn text on the
   page ground, no boxes, no PNG resampling.
2. ⭐ **S49 H23's "8 of 12 action buttons are no-ops" understates what is there.** `ChuteAction`
   (`ScreenPainter.cs`) routes every button through `FlightCommands.Run` + `PanelPolicy.ResolveImmediate` —
   **the same dispatcher and the same policy the physical console plate uses** — and logs the resolved
   outcome. A refusal is an *honest refusal through the shared policy*, which is §14.4(a) exactly, and
   T14's rule that two surfaces cannot come to different answers. `Monitor altitude` is handled separately
   and says so: *"a row that commands nothing did nothing, and saying so is different from saying it was
   refused."* **This is correct behaviour, not a hole.**
3. **All 12 buttons have hit rects, from the rectangle that draws them** — `HitTest` iterates `Actions` and
   calls `ActionRect` (`:162-171`), the same function `Build` uses. `PageAction`'s rule followed.
4. **The rail is the Cover's own `DrawRail`**, so the two rails are pixel-identical, and tapping any other
   rail row returns to the Cover (`FigmaUI.cs:339-345`). The literal `6` S49 flags is *correct* here — this
   page **is** phase 6.
5. **The altitude gates carry `(TBC)` markers** — `10.6 km (TBC)`, `2.5 km (TBC)` … — an honest marking that
   the numbers are provisional.

---

## MC-01 — Both section markers are hardcoded alarm red, permanently

**TIER 2** · **NEW** · §14.4(a)'s "no red" rule, third instance

**Evidence.** `ManualChuteDeployPage.cs:241`:

```csharp
dl.ArcBand(X(300), Y(titleY + 14), Z(5), Z(15), 0, 360, Alarm);   // red section marker
```

`Alarm` is `DragonPalette.Alarm` (`:47`), and `Section(…)` is called twice (`:264`), so **both**
`High Altitude Chute Deploy` and `Standard Altitude Chute Deploy` carry a filled alarm-red dot in every
state, on every render, with nothing faulted.

**What is wrong.** Red is this UI's fault colour and §14.4(a) is explicit: no red for something that is not
a fault. Two permanent red markers on the chute page are worse than decorative — this is the screen a crew
reads while descending under parachutes, and a red marker there means something. It also destroys the
signal: if a chute section ever *should* go red, nothing changes.

⚠ **Third instance of this class in the sweep** — V-02 (`CABIN MICS: RECORDING` in red), MP-01 (`Awaiting`
in caution amber), and now this. All three are hardcoded severities on non-fault states.

**Fix plan.**
- **Decide what the marker means, then compute it.** Two honest readings: (a) it is a *section bullet* —
  then it should be `Accent`, matching the Cover's reference-content bullets, and carry no severity; (b) it
  is a *section state* — then it must be computed, and the obvious source is whether that section's gate
  altitude has been passed (`s.Steps.RadarAltitude` is live and is what MC-02 needs anyway).
- **(a) is recommended as the immediate fix** — it is a one-constant change, removes a false alarm today,
  and does not pre-empt MC-02's design. (b) becomes available once MC-02 lands.
- **Must not break:** the marker's geometry, which is measured.
- **Verify:** re-render all four states; no red anywhere on a page with no fault.


### ✅ FIXED 2026-09-05 — S104 (QC batch 2)

**Fixed.** Both section markers are `DragonPalette.Accent` — the accent section bullet the Cover's
reference-content cards already use — instead of `Alarm`. `ui_manualchute.png` and
`ui_manualchute_descent.png` now return **0 alarm-red pixels**.

The finding's option (b) is preserved as a note in the code: if a section marker is ever meant to carry
state, the source is `s.Steps.RadarAltitude` against the section's gate altitudes — which is **MC-02** — and
it must be computed then, not re-hardcoded.
---

## MC-02 — Six live altitude gates, and nothing says which one is next

**TIER 2** · confirms S49 **H22**

**Evidence.** `ui_manualchute_descent.png` lists six gates across two sections —
`10.6 km (TBC)`, `10.0 km`, `2.5 km`, `2.2 km` (High Altitude) and `5.5 km`, `1.6 km` (Standard) — each
with its own action rows. **Nothing indicates which gate is next, which have been passed, or where the
vehicle is against them.** The rows are static strings (S49 H22), and the page's own name is *descent*.

Meanwhile `s.Steps.RadarAltitude` is live and is already in the fixture (`PreviewMain.cs:110` sets
`96000.0` for the ascent case). The one number that would order these six gates is present and unread.

**What is wrong.** This is S49's central procedure finding on the page where it costs most: under
parachutes, "which gate am I at" is the question the screen exists to answer, and the crew has to answer it
by reading the altitude off the top strip and comparing it to six numbers by eye.

⚠ **Step TRACKING is (A), the buttons are (B), and the distinction is the whole point** — S49 states it:
*"a procedure's step TRACKING is a readout of real vehicle state and is therefore (A); the same step's
ACTION BUTTON fires a pyro and is (B). The Manual Chute page is the clearest case — its altitude gates can
go live now, its `DEPLOY DROGUES` button cannot."*

**Fix plan.**
- **Compare `s.Steps.RadarAltitude` against each gate** and mark each row passed / current / pending. No
  new model, no new source, no §1.4 question — one live number against six printed ones.
- **The tint language already exists on this page**: `Dim` for pending, `White` for current, and the
  existing `(TBC)` treatment stays. A current-row marker can reuse `Accent`, which MC-01 frees up.
- ⚠ **Do not let the tracking imply the action happened.** A passed *altitude* is not a deployed *chute* —
  the gate is a readout, the deployment is Part B's. Mark the gate, not the outcome.
- ⚠ **`(TBC)` must survive.** The gate altitudes are provisional and marked as such; step tracking against a
  provisional number must keep the marking, or the page starts asserting precision it does not have.
- **Must not break:** the `!Valid` path (`ui_manualchute_nofeed.png`) — with no radar altitude, no row is
  current and all six stay pending, rather than defaulting to the first.
- **Verify:** three renders at descending altitudes with the current row moving down the list.

---

---

# PAGES 27 + 28 — MANUAL DOCKING and RENDEZVOUS

**Inspected together — they are the prox-ops pair**, reached from one another's letterbox margins, and S49
covers them as one family (H24–H28).

**Renders:** `ui_docking.png` · `ui_docking_corrected.png` · `ui_docking_notarget.png` ·
`ui_docking_precise.png` · `ui_rendezvous.png` · `ui_rendezvous_notarget.png`.

**Source:** `plugin/src/pure/DockingSimPage.cs` · `plugin/src/pure/RendezvousPage.cs` ·
`ScreenPainter.DockAction`.

## What was checked and found CLEAN

1. ⭐ **The docking bearings are genuinely live and correctly tinted.** `ui_docking.png`: ROLL 15.0°,
   PITCH 0.1° and YAW 0.1° in **green** (within `CorrectedToleranceDeg`), PYR 0.0 / 0.1 / 0.0 deg/s,
   RANGE 202.6 m, RATE −0.25 m/s — all matching the fixture. `RingTint` (`:150`) computes the
   green-when-corrected state from the value. **S26's work, and the second computed verdict in the sweep
   after SuitCheck.** On `ui_docking_notarget.png` everything dashes.
2. ⭐ **S85 turned the no-op into data.** `DockAction` sets `rec.Acted = false` for every direction pad —
   *"§14.4(a)'s honest no-op stated as data rather than only as a log line, and it is the record that lets
   a flight prove a direction pad was pressed and flew nothing."* That is better than an honest refusal; it
   is an auditable one.
3. **The two magnitude toggles act, and they are the only things on the page that do** — screen state, not
   the vehicle, correctly classed. `ui_docking_precise.png` shows the toggled state.
4. ⚠ **The Rendezvous plot's DOTTED orbit is deliberate, not a gap.** `RendezvousPage.cs:37` budgets *"the
   dotted ellipse (72 steps)"*, and `:63` calls `NavPage.Orbit(…, true)` — the dotted flag being *"the one
   addition NavPage.Orbit gained for this page"*. **Recording this explicitly because it looks exactly like
   the broken-orbit-line defect class the charter asks about** (and like `ISSUE_REGISTER` N5's ground-track
   seam). It is not one.
5. **S83's AP/PE label offset is visible and working** — on `ui_rendezvous.png` the two labels sit outboard
   of their markers along the local normal, not straight down.
6. **The Rendezvous plot is the real conic**, shared with `NavPage.Orbit` rather than re-implemented —
   *"NOT a second orbit renderer"* (`:13`).
7. **`Instructions` and `Reset Positions` are inert for two different recorded reasons (S29, quoted in
   `DockingSimPage.cs:44-53`)** — `Instructions` has no body in any source; `Reset Positions` is actuation
   whose target (vehicle or view) the reference does not state, so §1.4 keeps it classified conservatively.
   **Both correct.** How they are *drawn* is DK-02.

---

## DK-01 — Four of the six translation pads are labelled in the secondary tint and two in the primary, though all six do the same thing

**TIER 2** · **NEW**

**Evidence.** `DockingSimPage.Cluster` draws each pad's `a` label in `DragonPalette.White` and its `b` label
in `DragonPalette.Text6` (`:209-210`). The two clusters pass different slots:

```csharp
Cluster(…, "ROTATION",    …, "ROLL","ROLL", "PITCH","PITCH","YAW","YAW",  "▲","▼","◄","►");
Cluster(…, "TRANSLATION", …, "FWD","BACK",  "UP","DOWN","LEFT","RIGHT",   "", "", "", "");
```

- **ROTATION** puts the arrows in the `a` slot (White) and the axis words in `b` (Dim) — so each pad has a
  bright glyph and a dim caption. Coherent.
- **TRANSLATION** has no arrows: `aTop…aRgt` are empty, so `UP`, `DOWN`, `LEFT`, `RIGHT` land in the `b`
  slot and are drawn **Dim** — while `FWD` and `BACK`, in the corner `a` slots, are drawn **White**.

On `ui_docking.png` the result is plain: FWD and BACK read bright; UP, DOWN, LEFT and RIGHT read faint,
inside identically-bright borders. **All six are the same control with the same behaviour.**

**What is wrong.** `Text6` is this build's *"no live source behind this"* tint (S75) and this page's caption
tint (ROLL/YAW/PITCH labels, RANGE/RATE). Using it for four of six sibling pads says something the page does
not mean. The cause is structural: `Cluster` was written around the rotation cluster's glyph-plus-caption
shape, and translation reuses it with the glyph slot empty.

**Fix plan.**
- Give `Cluster` an explicit label tint, or fall the label back to `White` when the `a` slot is empty —
  a one-line change in `Btn`: if `a` is empty, draw `b` in White at the `a` size and position.
- ⚠ **Decide it once for both clusters.** If the intended reading is "axis captions are secondary", then
  ROTATION is right and TRANSLATION needs arrows, not a tint change — that is a design call, and the
  reference (`iss-sim`, named in the file header) should settle it before either is changed. **Recommend
  the tint fix as the minimal correct step**, because six identical controls reading in two weights is
  wrong under either interpretation.
- **Must not break:** the hit rects, which are per-pad and unaffected.
- **Verify:** all six translation pads read at one weight.

---

## DK-02 — Thirteen correctly-inert controls, all painted as live buttons

**TIER 2** · S75's appearance rule, **third page** (after **SC-02** and **A-02**)

**Evidence.** Twelve direction pads plus `Reset Positions` are §14.4(a) no-ops, and `Instructions` has no
content — four separate, recorded, correct decisions (S29 + T14 + S85). Every one is drawn in the live
idiom: `dl.Box(…, DragonPalette.Hairline)` plus a `DragonPalette.White` label (`:207-209`), and the three
bottom buttons at `:191` are `White` on a bordered box. Nothing distinguishes them from the two magnitude
toggles, which are the only controls on the page that act — and those are drawn in **`Accent`**, so the page
does have a distinguishing tint available and uses it for the wrong half.

**What is wrong.** Identical to **SC-02**: the *behaviour* is settled and right; the *appearance* rule that
S75 established on 2026-09-04 was never applied here. A crew member cannot tell, by looking, which of the
sixteen controls on this page does anything.

⚠ **This is now the third page with the same gap** (Cover fixed by S75; SuitCheck, Audio and Docking not).
**It should be one build line, not three** — see SC-02's recommendation to hoist a shared inert tint.

**Fix plan.**
- Draw the twelve pads, `Reset Positions` and `Instructions` in `DragonPalette.Text6`; leave the two
  magnitude toggles and `Settings` in their live tints.
- ⚠ **Do not remove their hit rects.** S85's `rec.Acted = false` record depends on the press being
  *received* and logged — that is the audit trail, and it is a feature. Inert here means "drawn as not
  acting", not "not hit-tested".
- ⚠ **`Reset Positions` may become live** if a source settles that it resets the view rather than the
  vehicle (S29 left it open). It then goes back to White **and** stays hit-tested — S75's "together" rule.
- **Verify:** on `ui_docking.png`, exactly three controls read as live.

---

## DK-03 — There is no camera behind the docking rings

**TIER 2** · confirms S49 **H26**

**Evidence.** `ui_docking.png`: the two alignment rings enclose bare page background. `DockingSimPage` draws
no `ImageId.DockingCamLive` anywhere — grep confirms the key does not appear in the file.

**What is wrong.** This is the *manual docking* page. The rings and the target diamond are an overlay for a
view the page does not show, so the crew aligns against an empty circle. S49 records that the build already
knows how: the stranded pre-Figma `pure/DockingPage.cs:73` draws the **full-bleed docking camera** the live
HUD does not, and `Frame58Hud` already claims the camera and clips a feed to a circle
(`Frame58Hud.cs:34-37`) — so both the claim path and the circular-clip draw exist.

**Fix plan.**
- Draw `ImageId.DockingCamLive` behind the rings, clipped to the outer ring, using `dl.ImageCircle` exactly
  as `Frame58Hud` does — same call, same mask colour idiom.
- **The camera must be claimed for this page.** `ScreenPainter.cs:1129-1131` requests the docking view only
  for `FigmaUI.WantsDockingCam(up, ps)`, which is `p == UiPage.Hud && s.Steps.NoseConeOpen`. This page needs
  adding to that predicate — and then `CameraHeldByDocking` becomes true, which the **Video settings page
  already handles** with its `FORWARD VIEW IN USE BY DOCKING` message (VV-01's CLEAN 1). The three pages
  are already designed to cooperate; only this one is not wired in.
- ⚠ **Design the no-feed look in the same pass**, and reuse `PlanetGeom.NoSignalLabel`'s marking pattern
  rather than inventing a second one — the same note H-09 makes.
- ⚠ **The preview cannot show the result** (H-09): `DockingCamLive` has no stand-in. **Do H-09's stand-in
  first** or this fix ships unreviewed.
- **Verify:** three renders — no target, target with feed, target with camera unavailable.

---

## RZ-01 — The Hold-Capture card reads NOT ENGAGED forever, and neither its arrows nor the icon rail can be touched

**TIER 2** · confirms S49 **H27** + **H28**

**Evidence.** `ui_rendezvous.png`: the `HOLD CAPTURE` card shows **`NOT ENGAGED`**, with a `◄` and a `►`
below it and a ring glyph top-right. To its left, a rail of **four identical empty ring boxes**. The whole
left third of the page is those five elements and empty space.

`NOT ENGAGED` is stub-pinned: S49 §1.2 records `RendezvousEngaged` / `RendezvousNote` as fed by
`StationApproach` in `_AutopilotStub.cs:144`, whose value *"can only ever be"* `false` / `null`. The arrows
and the four rail icons have no hit rect (S49 H28); the rail is drawn at `RendezvousPage.cs:81-85` as four
`dl.Rect` + ring, with nothing behind them.

**What is wrong — and the split matters.**
- The **engagement state** is genuinely Part B's. `RendezvousEngaged` is one of the nineteen stub-pinned
  fields; §14.4(a) says it stays an honest no-op until the conductor exists. **`NOT ENGAGED` is not a
  defect** — it is a true statement about this build.
- **What the card does with the rest of its space is (A), and that is the defect.** S49 H27 makes the point
  precisely: *"a card could read an actual approach off `HasTarget` / `RangeM` / `Closing` instead of a stub
  that says NOT ENGAGED for the whole mission."* Those three fields are live on this very page's sibling —
  `ui_docking.png` prints RANGE 202.6 m and RATE −0.25 m/s from them in the same frame.
- The **four rail icons** are a different problem: they are drawn with no labels, no state and no action —
  four identical rings. Nothing in the repo says what they are.

**Fix plan.**
- **Fill the card with the approach that is actually happening**, beside the honest engagement line: range,
  range-rate, closing/opening, target name — all live in `PageState` today, all readouts, all (A) under
  §14.4(f). Keep `NOT ENGAGED` as the *engagement* row; it is true.
- ⚠ **Do not wire the ◄/► arrows on a guess.** No source says what they step through. Either they select
  something the card can show (then they are (A) screen state and need a rect), or they are Part B's
  approach-mode selector (then they are (B)). **§1.4 question — no source, so no rectangle.** Meanwhile
  S75's tint applies: draw them inert (DK-02's fix, same page family).
- ⚠ **The four rail icons are a §1.4 question of their own** and should not be built or removed on a build
  chat's judgement. Recommend tinting them inert now and recording the question; the Figma export
  (**Q1 / A-05 / A-06**'s dependency) may name them.
- **Must not break:** the plot, which is the page's live half and is correct.
- **Verify:** with a target, the card reads a real approach; with none, it dashes — `ui_rendezvous_notarget.png`
  already exists as the second case.

---

---

# PAGES 29 + 30 — DEORBIT BURN PREP and ENTRY

**Inspected together — the two reconstructed-from-photo screens**, both Menu-only, both built on thin
tier-1 evidence (a blurry frame for 29, a *partial* frame for 30 with only one section title legible).

**Renders:** `ui_deorbitburnprep.png` · `ui_entryprocedure.png`.

**Source:** `plugin/src/pure/DeorbitBurnPrepPage.cs` · `plugin/src/pure/EntryPage.cs`.

**S49's entries.** §2: 29 is *"Crew Interrupt Conditions are static text; the four SLEW rows are literal
dashes; FC SLEW is correctly wired to a pinned stub. No touch at all"* (H29, H30); 30 is *"Nothing live.
`Build(dl,w,h)` takes no `PageState` — structurally provable"* (H31). **Both confirmed at HEAD** —
`EntryPage.cs:39` is `Build(DisplayList dl, int w, int h)`, no state parameter.

## What was checked and found CLEAN

1. ⭐ **Page 29's interrupt criteria are the SHARED source the Cover reads — C7.1 done right.** The Cover
   skips its own two baked captions and redraws them *"from DeorbitBurnPrepPage's own S13-corrected
   strings, so the two surfaces that state this criterion now read identically"* (`CoverPage.cs:126-141`).
   On the glass both pages say `30° sustained attitude error` and `600°/min attitude rate`, identically.
   **This is the exact pattern MP-01 needs and does not have** — one fact, one source, two surfaces.
2. **The four SLEW dashes are correct §14.4 dashes with a recorded reason.** `DeorbitBurnPrepPage.cs:46-47`:
   *"ROLL / PITCH / YAW under 'SLEW FOR DEORBIT BURN' are the attitude the vehicle is being TOLD to hold
   … and `docs/TELEMETRY_REGISTRY.md` carries no row for any of them — no SLEW_* datum, no authority."*
   A commanded attitude with no commander is genuinely absent. Dash is right.
3. **`FC SLEW` is honestly stub-wired.** `slewing ? "ENGAGED" : "NOT ENGAGED"` with `Go` / `Text6`
   (`:134`) — reads a pinned Part-B field and says `NOT ENGAGED`, which is true of this build. Compare
   RZ-01: the same construction, correctly used.
4. **Page 30's `(TBC)` markers survive** into its prose lines — `5.5 km (TBC)`, `1.6 km (TBC)`.

---

## DB-01 — Both pages use a corner of the screen and leave the rest empty

**TIER 2** · **NEW** · the owner's standing layout concern (**R-4**)

**Evidence.** Measured from the renders, both 2560×1406:

| page | content bounding box | share of the page |
|---|---|---|
| DEORBIT BURN PREP | x ≈ 360…870, y ≈ 180…680 | **≈ 7%** of the area; the content column is 20% of the width |
| ENTRY | x ≈ 360…810, y ≈ 175…365 | **≈ 2%** of the area |

Everything on both pages is stacked in a single left column starting at the same x, and the remaining
75–95% is empty background. `ui_entryprocedure.png` is a title, one section heading and six short lines on
an otherwise blank screen — reachable from the Menu grid as a full page called `ENTRY`.

**What is wrong.** Both pages are *honest* — they carry what their sources support and no more, and S49 is
right that the alternative would be invention. But a page that fills 2% of the glass is not a finished
screen, and the emptiness is not neutral: at IVA distance a near-blank screen reads as a fault or a page
that failed to load.

**Fix plan — and the honest options are limited, which is the point.**
1. **Lay the existing content out for the screen it is on** — two or three columns instead of one, at a
   larger type size, using the width. Costs nothing, invents nothing, and makes what little there is
   legible at distance. *(Recommended as the immediate step for both.)*
2. **Fill page 29 from what is live.** Its own subject — the deorbit burn — has real quantities the build
   already computes: burn ΔV, time-to-burn, propellant, attitude error against the slew target. These are
   readouts and (A) under §14.4(f). ⚠ **But `TELEMETRY_REGISTRY` has no SLEW row** (CLEAN 2), so the
   *target* attitude stays dashed; only the *current* attitude and the burn parameters can fill.
3. **Merge page 30 into the Manual Chute page** — see DB-02, which argues its content is already there.
4. **Leave them.** Defensible under §1.4 and the least work, but it ships two near-blank screens on the
   Menu grid.

**Recommend 1 for both, plus 3 for page 30 if the owner accepts DB-02's reading.** Option 2 is real work
and should be its own line.


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `ui_deorbitburnprep.png`, `ui_entryprocedure.png`, 1280×703.  **Verdict: STANDS.** Proportions
confirmed by measurement rather than by eye.

Content bounding box below the centred page title, above the bottom bar:

| page | filed | **now (measured)** |
|---|---|---|
| DEORBIT BURN PREP | *"≈7% of the area; column 20% of the width"* | **20% of the width, 9.4% of the body area** |
| ENTRY | *"≈2% of the area"* | **18% of the width, 3.1% of the body area** |

⚠ The filed area figures were estimated from a 2× render with the centred title folded into the box; measured
cleanly they are slightly larger but the same order. **Both pages still use under a tenth of their body area,
in a single left column under a fifth of the width, and 80% of the screen is empty background.**

The fix plan is unaffected — and option 1 (lay the existing content out for the screen it is on) is now
**more** urgent, because at the shipped width that content is also below the legibility floor (**R-01**).
---

## DB-02 — Page 30's entire content is a third copy of material already on two other pages

**TIER 2** · **NEW** · content duplication, the sibling of **F-01**

**Evidence.** `ui_entryprocedure.png` carries exactly six lines. Every one already exists elsewhere:

| ENTRY (page 30) | already on |
|---|---|
| `5.5 km (TBC): monitor altitude, arm and verify backup pyros` | **Manual Chute**, Standard Altitude — `5.5 km (TBC) · 6 nm · drogues` + `Monitor altitude` + `ENABLE BACKUP PYROS / Arm and verify` |
| `Deploy drogues – latch` | **Manual Chute** — `DEPLOY DROGUES / Latch` |
| `1.6 km (TBC): fire pyro, arm and verify backup pyros` | **Manual Chute** — `1.6 km (TBC) · 6 nm · mains` + `FIRE PYRO / Execute` |
| `Deploy mains – execute` | **Manual Chute** — `DEPLOY MAINS / Execute` |
| `Land under ≥ 3 mains` | **Cover**, Reference Content → PARACHUTES (MARK 3) |
| `CUT MAINS after splashdown` | **Cover**, Reference Content → PARACHUTES (MARK 3) |

**Three surfaces, one set of facts, three formats** — a live action list with buttons (Manual Chute), a
reference card (Cover), and prose (Entry). And unlike page 29's shared strings (CLEAN 1), **these are three
independent copies**: change a gate altitude and three files need editing.

**What is wrong.** Same class as F-01 but at content level rather than page level, and with the same risk —
`CoverPage`'s own header records what happened last time two surfaces stated one criterion independently
(the S13 residual: *"the two surfaces that state the same criterion disagreed on glass (C7.1)"*).

**Fix plan.**
- **Make one of them the source.** The Manual Chute page holds the richest version (gates + actions +
  `(TBC)`), so its strings should be the source and the other two should read them — exactly the move
  `CoverPage` already makes for the interrupt criteria (CLEAN 1). That fix pattern is in the codebase and
  needs no new mechanism.
- **Then ask whether page 30 still has a reason to exist.** If its content is the Manual Chute page's, it
  is a second view of one procedure. ⚠ **That is the owner's call, not a build chat's** — the page is
  reconstructed from a real partial photo frame (a §1.4 tier-1 source), so *deleting* it discards a real
  screen. **Recommend: keep the page, make it read the shared strings, and give it the layout DB-01 asks
  for** — then it is a legitimate reference view rather than a third transcription.
- **Must not break:** the `(TBC)` markers, on every surface.
- **Verify:** grep for each gate altitude; each should appear in exactly one source file.

---

## DB-03 — Neither page has any touch, and page 30 cannot read the vehicle at all

**TIER 3** *(recorded — the §1.4 evidence is genuinely thin)* · confirms S49 **H29/H30/H31**

**Evidence.** Neither file contains a `HitTest`, and neither has a `ScreenPainter` branch. `EntryPage.Build`
takes **no `PageState`**, so the page cannot read vehicle state even in principle. Page 29 does take state
but reads exactly one field (`slewing`, for FC SLEW).

**What is wrong — carefully.** Nothing on either page is drawn *as* a control, so this is **not** the
S75 defect the other pages have: there is no false affordance here. What there is, is two screens that
cannot participate in the mission they describe.

⚠ **The constraint is real and is recorded.** Page 30's source is a partial photo frame with only
*"Parachute Deployment Altitude"* legible (`FigmaUI.cs:57-61`); page 29's is *"a blurry photo frame"*. There
is no tier-1 evidence for controls on either. **Inventing them would be a §1.4 tier-3 invention.**

**Fix plan.**
- **Page 29 has one clear (A) opportunity that needs no new source**: it names attitude criteria
  (`30° sustained attitude error`, `600°/min attitude rate`) and the vehicle's *current* attitude and rate
  are live (`RollDegText` … `YawRateText`, drawn on Docking in the same frame). Showing current-against-
  criterion turns a static list into a live interrupt monitor — a readout, (A), no invention.
- **Page 30 stays as it is** until DB-02 is decided.
- ⚠ **Do not add navigation affordances to either.** T14 owns entry points, and S27 already records the
  owner declining to assign these pages a Cover rail slot — *"no source names what belongs there"*.
- **Verify:** page 29 with a live attitude, showing each criterion's current margin.

---

---

# PAGES 31 + 32 — SYSTEMS TREE and SYSTEMS P&ID

**Inspected together — the two Vehicle deep-views**, reachable from every Vehicle-family page via
`VehicleDeepViewLinks` (S27).

**Renders:** `ui_systemstree.png` · `ui_systemstree_live.png` · `ui_systemspid.png`.

**S49's entries.** §2: 31 is *"Genuinely live-coloured … **Read-only — no HitTest anywhere**"* (H32); 32 is
*"every valve but one is a fixed-colour glyph; PUMP A/B `RUNNING` is a literal not even `Valid`-guarded;
CABIN HX A/B are **empty boxes**"* (H33). ⚠ **S56 has closed nearly all of this** — see CLEAN 1–3. Do not
re-log H32, and re-log H33 only for what remains.

## What was checked and found CLEAN — S56 closed two of S49's largest holes

1. ⭐ **H32 is CLOSED.** `SystemsTreePage.HitTest` exists (`:150`), returns a `PanelCommand`, and is
   dispatched through the **same `FlightCommands.Run` / `PanelPolicy` the physical console plate uses**
   (`ScreenPainter.SystemsAction`) — *"there is no second policy here and there must never be one."*
   `ui_systemstree_live.png` shows the result: STRING 1C **TRIP** in red, STRING 2B **ISOL** in amber,
   the rest ON in green, MAIN POWER **CAUTION** with a live bar, POWER 1/2 at **2 / 3 ONLINE**, and the
   **connector lines coloured from node state**.
2. ⭐ **The tree's affordance caption is TRUE**: *"TOUCH A POWER OR STRING NODE TO SWITCH IT — THE SAME
   COMMAND AS THE CONSOLE PLATE."* ⚠ **Contrast F-03**: the Cabin page says `Tap to disable display` and
   nothing on it is tappable. Same instruction pattern, opposite truth, two pages apart.
3. ⭐ **H33 is substantially CLOSED.** `SystemsPidPage.cs:34-37` records the fix in its own words —
   CABIN FAN, PUMP A and PUMP B now read `SystemsState.FanOn` / `.PumpAOn` / `.PumpBOn` with a live guard,
   and CABIN HX A/B carry `—` instead of `""`. On `ui_systemspid.png` all three read **OFF** in amber with
   amber node markers, and the HX boxes carry a dash. **And the valves are state-coloured too** —
   `Valve(…, o2Line)`, `Valve(…, n2Line)`, `Valve(…, airPipe)`, `Valve(…, leaking ? ventCol : Pipe)`
   (`:156-203`) — so H33's *"every inline valve but one is a fixed-colour glyph"* is also out of date.
4. **The tree has a legend** — ON / ISOLATED / TRIPPED / UNPOWERED, each in its own colour. The only page
   in the sweep that explains its own colour language.

## ⭐ And here is V-01's contradiction, rendered

`ui_systemspid.png` prints **`CABIN TEMP 21.8 °C` in GREEN** in its READOUTS column, computed by
`Alarms.Colour(Alarms.Band(s.Cabin.CabinTempC, CabinLimits.CabinTempCaution, CabinLimits.CabinTempAlarm))`
(`SystemsPidPage.cs:249`).

`ui_vehicle.png` and `ui_vehiclecrew.png` print **the same 21.8 °C inside a RED ring**, from a hardcoded
constant.

**Same value, same fixture, same frame, opposite verdicts.** This is the single clearest piece of evidence
for **V-01 / S-01**, and it also shows the fix already working one page over: the P&ID needs no change, the
Vehicle pages need the P&ID's call.

---

## SP-01 — The P&ID has exactly one render, in an all-nominal state, so none of the live colouring S56 built is on the gate

**TIER 2** · **NEW** · third instance of the preview-blindness class (**H-09**, **VV-02**)

**Evidence.** The preview writes **one** P&ID render, `ui_systemspid.png`, from the default fixture — in
which nothing is faulted. Its sibling gets two: `ui_systemstree.png` **and** `ui_systemstree_live.png`, the
second showing a tripped string, an isolated string and a caution bus.

So everything S56 built into the P&ID's colouring is invisible on the gate: the valve tints
(`o2Line`, `n2Line`, `airPipe`, `leaking ? ventCol : Pipe`), the pipe states, the fire and leak words, the
`OVERBOARD / ISOLATION` state, and the per-loop severity bands at `:208-210`. On the one render they all
resolve to the same nominal colour, which is exactly why H33's *"fixed-colour glyph"* reading looked right
from a screenshot **and was wrong** — the QC pass nearly re-logged a closed hole because the render could
not distinguish "fixed" from "nominal".

**What is wrong.** The preview is the gate, and for this page it can only ever return "nominal renders
nominally". A page whose entire value is live colouring needs at least one non-nominal render, or the gate
proves nothing about it.

**Fix plan.**
- Add P&ID renders for the states the page distinguishes: **a leak** (`Systems.Leaking` → vent path and
  `CABIN LEAK`), **a fire** (`Systems.Fire` → `FIRE`), **a pump/fan off vs on**, and **a loop over
  `CabinLimits.LoopCaution`**. Four renders, all from fixture edits, no source change.
- ⚠ **The same gap exists in the general case and is worth stating once:** the sweep has now found three
  pages whose live half has never been rendered — the HUD's docking-cam disc (**H-09**), the Video page's
  camera list (**VV-02**), and this. **A page's non-nominal states belong in the preview set**, and that is
  a harness policy rather than three separate fixes.
- **Must not break:** the existing nominal render, which is the baseline any comparison needs.
- **Verify:** each new render differs from the nominal one in the elements it is meant to exercise.

---

## SP-02 — The FLIGHT COMPUTER STRINGS node is the one node on the tree with no state, and its caption asserts a count nothing models

**TIER 3** · **NEW**

**Evidence.** `ui_systemstree_live.png`: every node on the page carries a live state and a state colour —
except the footer node, which reads

```
FLIGHT COMPUTER STRINGS
TRIPLE-REDUNDANT · 18 UNITS · 54 VOTING PROCESSORS
```

drawn as two static strings in White and Dim (`SystemsTreePage.cs:286-287`), with a dim border, on a page
whose other ten nodes are green / amber / red from `SystemsState`.

**What is wrong.** Two things, of different weight:
- **It reads as unpowered.** The tree's own legend gives `UNPOWERED` a dim grey, and this node is dim grey.
  A crew member applying the page's legend to the page's own footer concludes the flight computers are
  unpowered.
- **`18 UNITS · 54 VOTING PROCESSORS` is a confident count with nothing behind it.** ⚠ It is reference copy
  (the file classes these boxes as *"readouts of …"*, `:112`), and the Avionics sub-tab separately prints
  `3 / 3` for flight computers — the two are probably describing different things (strings vs computers),
  **but nothing on either page says so**, and a reader comparing them has no way to reconcile 18 with 3.

**Fix plan.**
- **Tint it out of the legend.** Whatever else happens, a node that carries no state must not be drawn in
  the colour the legend assigns to `UNPOWERED`. Use the page's caption tint, or give the node no border.
  **Cheapest and correct regardless of the rest.**
- **Then decide whether it can be live.** `SystemsState` models power strings, not flight computers, so
  `18 UNITS` has no source in the model today. Under §14.4(f) this is a readout that should be filled — but
  filling it means modelling flight-computer health, which is Part-B-adjacent (the FDIR spine) and is not a
  screens-pass job. **Recommend: tint now, and record the liveness question against the same policy
  question S49's Q3 already holds.**
- ⚠ **Do not reconcile `18` with the Avionics tab's `3 / 3` by editing either** without a source — both are
  reference-derived, and §1.4 governs. If the reference does distinguish strings from computers, the fix is
  a clarifying label, not a changed number.
- **Verify:** the footer node no longer matches any legend colour.

---

---

# PAGES 33 + 34 — ASCENT / LAUNCH and NAV / ORBIT PLOT

**The last two `UiPage` values.** Both Menu-only.

**Renders:** `ui_ascent.png` · `ui_navorbitplot.png` · `ui_navorbitplot_notarget.png`.

**S49's entries.** §2: 33 is *"One live element — ACTIVE PHASE. All 11 ascent events are a static string
array, while `StepList` computes live equivalents of six of them and is never read"* (H34); 34 is *"Plot
LIVE … G-FORCE / RATE / RANGE live; **the range rings carry no scale**; no touch controls"* (H35, H36).
**Confirmed, with one correction: H36's missing scale is a recorded §1.4 decision** (CLEAN 2).

## What was checked and found CLEAN

1. **Page 34's plot is live and correct** — the real conic via `NavPage.Orbit` (*"NOT a second orbit
   renderer"*), a legend (`VEHICLE` cyan / `SPACE X STATION` amber), live `G-FORCE 0.2 g`,
   `RATE −0.25 m/s`, `RANGE 202.6 m`, the approach chord, and S83's AP/PE label offset.
2. ⚠ **H36's "no scale" is answered, not open.** `NavOrbitPlotPage.cs:22` states it: *"the concentric range
   rings (ring count and spacing — **no scale is legible in either source, §1.4**)"*. The rings are marked
   as ours and unscaled deliberately, because inventing a scale would be a tier-3 invention. **Do not
   re-log this.** ⭐ What *is* wrong with them is NO-01, which is a different thing entirely.
3. **Page 33's `ACTIVE PHASE — ORBITING` is live** and is the page's one live element, as S49 says.

---

## NO-01 — Three of the four range rings are drawn and then painted over by the globe

**TIER 2** · **NEW** · measured

**Evidence.** `NavOrbitPlotPage.cs:58-66` draws four concentric rings **before** calling `NavPage.Orbit`
(`:69`), which draws the body disc over them. Measured on `ui_navorbitplot.png`:

```
plot centre  (1280.0, 676.0)
rmax = Z(min(PlotW,PlotH)) x 0.46 = 511.4 panel px
rings at      127.9   255.7   383.6   511.4
globe limb, scanned from the centre leftwards:  radius 388.0
```

**Rings 1, 2 and 3 all fall inside the globe's 388-px radius and are covered.** Only the outer ring is
visible — which is exactly what the render shows: one faint circle outside the globe and nothing inside it.
Ring 3 misses by 4.4 px.

**What is wrong.** The page draws four rings, states in its own header that the ring count and spacing are a
deliberate design choice of ours, and then renders one. Three quarters of a deliberately-designed element is
invisible, and the one that survives reads as a lone decorative circle rather than as the outermost of a
scale. It also costs draw calls the page's `Commands` budget is paying for.

⚠ **This is not the same as H36.** H36 is "the rings carry no scale", which is a settled §1.4 decision.
NO-01 is "three of the rings are not on the screen at all", which is a rendering defect.

**Fix plan.**
- **Draw the rings AFTER the globe**, not before. They are a range overlay; an overlay belongs on top.
  One statement moved.
- ⚠ **Then they will cross the globe**, which is the point of a range ring — but check the tint: at
  `DragonPalette.Hairline` over a photographic disc they may vanish into the texture. If so, the honest fix
  is a slightly brighter or dashed ring over the disc, **not** moving them back under it.
- **Alternative, if rings over the body are judged wrong:** size `rmax` so all four sit *outside* the
  globe — i.e. from the limb outward rather than from the centre. That keeps them clear of the texture and
  makes all four visible, but it changes what they measure, so it is a design decision rather than a fix.
  **Recommend moving the draw order first** and looking at the result before choosing.
- **Must not break:** the §1.4 marking. However they are drawn, the rings stay unscaled and stay marked as
  ours.
- **Verify:** count the visible rings on the render — four, not one.


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `ui_navorbitplot.png`, 1280×703.  **Verdict: STANDS, exactly — this is a pure ratio and it
survived untouched.**

| | filed (2560×1406) | now (1280×703) |
|---|---|---|
| plot centre | (1280, 676) | **(640.0, 337.9)** |
| `rmax` | 511.4 | **255.7** |
| rings | 127.9 / 255.7 / 383.6 / 511.4 | **63.9 / 127.9 / 191.8 / 255.7** |
| globe limb (ray-measured) | 388.0 | **194.0** |
| **rings hidden by the globe** | **3 of 4** | **3 of 4** |
| ring 3's margin | hidden by 4.4 px | **hidden by 2.2 px** |

Ring radii as a fraction of the globe radius: **0.330 / 0.659 / 0.989 / 1.318** — identical at both scales,
which is the whole reason the finding survives. Draw order is the cause and draw order does not scale.

⚠ **One new consequence at the shipped width, which belongs to S101 and not here:** the one ring that *is*
visible is drawn at `St(2)` = 1 px and now renders as a faint grey hairline rather than a clear circle. Fixing
NO-01 (moving the rings above the globe) will put four such hairlines over a photographic disc — **the fix
plan's existing caution about the tint is now the load-bearing part of it**, not an afterthought.
---

## NO-02 — S43 built zoom and pan for the orbit plot, and the standalone orbit plot cannot use them

**TIER 2** · **NEW** · extends S49 **H36**

**Evidence.** `NavOrbitPlotPage.Build(DisplayList dl, int w, int h, PageState s)` — **no `MapView`
parameter** — and `FigmaUI.cs:224` calls it without one. `NavPage.Orbit` has three overloads (`:599`,
`:607`, `:640`); this page calls the one that takes no viewport.

Meanwhile `REGISTER.md` records **S43: *"the ORBIT plot gets ZOOM + PAN, owner-ruled, default render
untouched"*** (`01aef68`), and the preview carries six renders proving it works —
`page2_nav_orbit_leo_x1/x4/x8`, `_x4_pan`, `_kerbin_x4`, `_suborbital_x4`.

**So the page whose entire subject is the orbit plot is the one place the orbit plot's zoom cannot be
reached.** The zoom exists, is owner-ruled, is tested and rendered — on the NAV page, which S49 §1.1 records
as *stranded* behind `FigmaMode` and unreachable in the shipped build.

⚠ **Stated precisely: S43's zoom is currently reachable from nowhere in the shipped UI.** It lives on
`NavPage.Build`'s cluster, and there is no `UiPage.Nav`.

**Fix plan.**
- Give `NavOrbitPlotPage` a `MapView` and pass it to the viewport overload of `NavPage.Orbit`, then draw
  the zoom cluster. **The cluster already exists twice over** — `NavPage`'s own (stranded) and
  `CoverPage.PadRect`/`PadButton`, which is reachable, tested, and drawn from shared rectangles.
  **Reuse the Cover's**, don't re-implement.
- **The painter already holds the state.** `mapView` is a field on `ScreenPainter` and is already passed to
  Cover and ManualChute; this page needs adding to that list. ⚠ **Decide whether it shares the Cover's
  `mapView` or gets its own** — sharing means zooming here also zooms the Cover's globe, which may or may
  not be wanted. Recommend a separate view: the two plots show different things at different scales.
- ⚠ **S43 is owner-ruled and its ruling included "default render untouched".** Any change here must keep
  the zero-zoom render byte-identical, which is what that ruling protects.
- **Must not break:** `ui_navorbitplot_notarget.png`, and the dotted-ellipse rendering.
- **Verify:** the six existing NAV zoom renders have equivalents on this page.

---

## AS-01 — Eleven ascent events, none of them tracked, while the step machine that computes six of them runs unread

**TIER 2** · confirms S49 **H34**

**Evidence.** `ui_ascent.png` lists eleven T+ events down a Falcon 9 / Dragon line drawing — `LIFTOFF`,
`T+0:10 PITCH KICK`, `T+1:00 MAX-Q`, `T+1:09 MACH 1`, `T+1:14 STAGE-1B ABORT MODE`, `T+2:30-2:35 MECO`,
`T+2:35-2:39 STAGE SEPARATION`, `T+2:36-2:47 S2 IGNITION`, `T+4:20-8:43 SECO-1 / ORBIT INSERTION`,
`T+9:00-12:02 DRAGON SEPARATION`, `T+12:48-13:23 NOSE-CONE OPEN` — **all in one tint, none marked passed,
current or pending.**

And the state to mark them with is in the fixture, on this frame: `ps.Steps.MaxQPassed = true`,
`ps.Steps.BoosterAttached = false`, `ps.Steps.S2Attached = true`, `ps.Steps.S2Lit = true`,
`ps.Steps.Phase = MissionPhase.Ascent` (`PreviewMain.cs:105-113`). By those five fields alone, **six of the
eleven events have demonstrably occurred** and the page marks none.

S49 §1.1 records why: `pure/StepList.cs` is *"a 15-row live ascent/prelaunch state machine … every row
resolved off real KSP state"*, stranded behind `FigmaMode`. **The events on this page and the rows in that
file are the same events.** S49's own words: *"H34's fix is largely 'route what already exists'."*

**Fix plan.**
- **Route `StepList`, do not rewrite it.** It already resolves liftoff, Max-Q (a latched peak detector),
  MECO, stage sep, SECO, Dragon sep and nose-cone open off `VesselData`. This page needs the six or seven
  it covers, tinted passed / current / pending.
- ⚠ **The eleven T+ *times* are reference copy and must not be recomputed.** `REGISTER.md:941` records them
  as *"Real, from §8's mission-timeline pass (tier-1): all 11 T+ events transcribed verbatim."* Step
  tracking marks which have happened; it does not change when they are printed to happen.
- ⚠ **`StepList` is stranded, not missing.** Routing it is a real piece of work (it means reaching a pure
  file the Figma path does not currently call) and it serves this page, the SuitCheck steps (**SC-01**),
  the Manual Chute gates (**MC-02**) and the VRIO checklist (**VT-01**). **Four pages want the same
  routing.** It should be one line, scheduled once — which is what S49 H34 says.
- **Verify:** three fixtures — pre-launch, mid-ascent, post-insertion — with the marked set growing.

---

## AS-02 — The Ascent page uses the left 40% of the screen

**TIER 2** · **NEW** · same class as **DB-01**

**Evidence.** On `ui_ascent.png` the vehicle drawing and its eleven callouts occupy x ≈ 420…1010 of 2560;
the only thing right of that is `ACTIVE PHASE — ORBITING` in the top corner. **Roughly 60% of the width is
empty background.**

**What is wrong.** The layout is a vertical stack on a wide screen — the callout labels all run rightwards
from the rocket and stop less than halfway across. It is the same shape as DB-01 (pages 29 and 30) and the
same owner concern (**R-4**): a screen that reads as unfinished at IVA distance.

⚠ **Unlike DB-01, this page has content to put there.** AS-01's step state, a mission clock, the live
ascent telemetry (`ps.Steps.RadarAltitude`, `VerticalSpeed`, `Propellant01`) — all live, all readouts, all
(A). The empty 60% is not a layout problem looking for filler; it is the space AS-01's fix should occupy.

**Fix plan.**
- **Do AS-01 first, then lay out for the result.** Marking the eleven events is the content; a second
  column of live ascent telemetry beside the vehicle is the natural use of the space.
- **If AS-01 is deferred**, widen the callout column and increase the type so the existing content at least
  reads at distance — the same interim step DB-01 recommends.
- **Verify:** the page's content bounding box covers a substantial majority of the plot area.


### ⟳ RE-VALIDATED 2026-09-05, on the honest 1280×703 preview (S100 / `7957d4d`)

**Rendered:** `ui_ascent.png`, 1280×703.  **Verdict: STANDS as a proportion — but the figure in the filed
text was an eyeball, not a measurement, and is corrected here.**

Measured bounding box of the rocket and its eleven callouts, below the `ACTIVE PHASE` line and excluding the
centred page title:

| | filed | **now (measured)** |
|---|---|---|
| content column | *"the left 40%"* | **x 114…501 = 30% of the width** |
| body area used | *"roughly 60% of the width is empty"* | **28.6%** used → **~70% of the width empty** |

⚠ **The filed figure was never measured at either scale.** My own 2× numbers in the finding's evidence
(*"x ≈ 420…1010 of 2560"*) work out to 23% of the width, while the prose beside them said 40%. The measured
30% now supersedes both. **The direction is unchanged and the defect is slightly worse than stated.**

The fix plan is unaffected — and its argument gets stronger: AS-01's step state plus live ascent telemetry
has **70%** of the width to occupy, not 60%.
---

---

# THE LOWER ANALOG CONSOLE PANEL — 0 findings

**Renders:** `panel_rest.png` · `panel_armed.png` · `panel_fired.png` · `panel_inert_swap.png`
(3600×540 each).

**Source:** `plugin/src/pure/PanelMap.cs` · `PanelBehaviour.cs` · `PanelBoardPage.cs` ·
`PreviewMain.cs:1302-1355` · `docs/REAL_DRAGON_SCREENS.md:44-48, 155-175`.

**⚠ Scope, and it changes what QC can legitimately check here.** This is **not a screen we draw.** The
preview says so in its own header: *"NOT a screen … The console's buttons are meshes on Tundra's IVA prop
and its indicators are Tundra's dashes; this draws them only so the LIGHTING can be judged with the game
closed, which is otherwise the one part of the panel that costs a restart to look at."* The render is a
**diagnostic**, and it labels itself one on the glass: *"PREVIEW-ONLY DIAGNOSTIC, NOT A SCREEN … Neither
mark exists on the real console."*

So layout, labels and plate geometry are **Tundra's model, not ours**, and C1.4 forbids editing `PanelMap`
or the label docs without a real-source confirmation. What QC can check is the **lighting model**, the
**§14.4(a)/(b) behaviour**, and whether the diagnostic's own claims are true. All three check out.

## Verified

1. ⭐ **The no-red invariant holds, measured.** The preview states the rule the four scenes exist to test —
   *"What must be true in all four is that no dash is any colour but bright or as-modelled"* — and §14.4(a)
   requires no red for a non-fault. Pixel-scanned across **all four renders, 1,944,000 px each**:

   | render | red px | amber px | green px |
   |---|---|---|---|
   | `panel_rest` | **0** | **0** | **0** |
   | `panel_armed` | **0** | **0** | **0** |
   | `panel_fired` | **0** | **0** | **0** |
   | `panel_inert_swap` | **0** | **0** | **0** |

   ⚠ **This is the sharpest result in the whole sweep, and it cuts the other way from the screens.** The
   surface that actually arms and fires — pyros, abort, deorbit — obeys §14.4(a) exactly. Meanwhile three
   *screens* draw permanent non-fault red or amber: **V-02** (`CABIN MICS: RECORDING` red), **MP-01**
   (`Awaiting` in caution amber), **MC-01** (two alarm-red chute markers). **The console is the standard
   the screens are failing.**
2. **The four scenes are the right four**, and each states what it proves: at rest, armed-and-holding,
   fired-from-the-other-seat (with a real display command lit alongside), and an inert control pressed.
   `panel_fired` deliberately combines two cases so the armed lamp going out can be seen against a lamp
   that holds.
3. ⭐ **The inert scene self-checks, and passes.** Its caption is generated as
   `"Clicked" + (inert.LastClicked ? " (audible)" : " (SILENT - WRONG)")` — the diagnostic **reports its own
   defect** if the click is silent. The render reads **`INERT - SWAP 2 PRESSED (Inert)` / "Clicked
   (audible), did nothing, lit nothing (§14.4b)"**. Press received, audible, acts on nothing, lights
   nothing — §14.4(b) exactly. **A self-reporting diagnostic is a pattern worth copying.**

## Two things that look like defects and are not — recorded so they are not re-logged

1. **`SURPRESS FIRE` is not a typo of ours.** It appears on both emergency plates, and
   `docs/REAL_DRAGON_SCREENS.md:46-48` rules on it: *"`SURPRESS FIRE` is spelled that way **in the model** —
   the real capsule reads SUPPRESS. **Do not "correct" it in our own labels** if we ever redraw that
   texture; matching the installed art matters more, and it is Tundra's to fix."* `PanelMap.cs:19-20`
   carries the same note. A deliberate, sourced transcription. ⚠ **C1.4 forbids changing it anyway.**
2. **The two empty bays are real blank plates, not unmapped controls.** `PanelMap` names six plates —
   `TE_CD2_PROP_BUTTON_1/2/3/4/6/8` — and the diagnostic renders eight positions, so `_5` and `_7` come out
   empty. `docs/REAL_DRAGON_SCREENS.md:171,174` records both from a real transform dump as **"blank
   filler … no children"**, count **0**, and `PanelMap.cs:9-11` confirms the mapping *"matched every count,
   including the two blank filler plates and FIRE PYRD sitting apart from its row."* The gaps are the
   model's. (`FIRE PYRD`, likewise, is the model's own label and placement.)

**Conclusion: no findings.** Recorded deliberately rather than padded — this surface is correct, and its
correctness is the argument for fixing V-02, MP-01, MC-01 and the S75 tint gap (**SC-02 / DK-02 / A-02**) on
the screens.

---

# THE SWEEP IS COMPLETE — all 35 `UiPage` values, the 7 Cover phases, the 3 camera views and the console

**65 findings in 16 sections**, covering all 35 `UiPage` values, the seven Cover phases, the three Cover
camera views and the lower console. Every page in the inventory is marked DONE. What follows is not new
analysis; it is the cross-cutting shape of what the sweep found, for scheduling.

⚠ **Four Cover phase slots (0, 2, 3, 4) remain ⏳ PART** — they have no preview render of their own and
their bodies were read from source rather than seen. **C-07**'s fix plan adds the four renders; until then
those four are the one part of this inventory that is inferred rather than inspected.

## The five things that are one fix, not many

| # | theme | findings | why one line |
|---|---|---|---|
| 1 | **`component_48`** — the un-erased marker glow and the 12.2% horizontal stretch | **C-12**, **C-04**, and **H-07**'s seam | One asset, one draw idiom, **fifteen pages**. H-07 is coupled to C-04 through `FigmaUI.BottomBarHit`, so all three move one hit map. |
| 2 | **Hardcoded gauge colour** | **V-01**, **S-01** | **32 gauges across 7 pages**, one `Gauge()` colour argument each. The correct call already exists at `SystemsPidPage.cs:249`, and **SP-01's section shows the two verdicts side by side.** |
| 3 | **S75's inert tint, never applied outside the Cover** | **SC-02**, **DK-02**, **A-02**, **F-03**, **RZ-01** | Five pages, one shared tint. SC-02 recommends hoisting `DragonPalette.Inert` rather than relearning it a sixth time. |
| 4 | **Step tracking** | **SC-01**, **MC-02**, **VT-01**, **AS-01** | Four pages want the same routing of the stranded `pure/StepList.cs` (S49 §1.1 / H34). One line, scheduled once. |
| 5 | **The preview cannot see a page's live half** | **H-09**, **VV-02**, **SP-01**, and **C-10**'s fixture | Three pages plus one fixture. **A harness policy** — non-nominal states belong in the preview set — not four separate fixes. |

## What must be decided before building

**H-01 / Q5 first.** The preview renders Figma pages at 2560 while the cfg ships 1280, so **every legibility
finding in this document is provisional** until that is settled — C-05 above all, which at the shipped width
is 7.7 px against a 16 px floor.

Then **Q6** (the audio faders — it gates A-04 and A-05), **Q1** (the Figma export — it also unblocks A-05 and
A-06), **Q2** (glass time for the globe/map handedness), **Q3** (ENTRY ENABLED), **Q4** (the CAMERA caption).

## What the sweep found working, and should be copied

- **`SuitCheckPage`** — the only page where a safety verdict is computed from a model (S31/S32 satisfied).
- **`MenuPage.CellRect`** — the shared-rectangle discipline stated and followed; the answer to **H-04**.
- **`SystemsTreePage`** — live colour, a legend, a shared dispatcher, and an affordance caption that is true.
- **`ManualChuteDeployPage`'s top strip** — **C-01's fix, already built**, on a page that shares the Cover's rail.
- **`DeorbitBurnPrepPage` ↔ `CoverPage`** — one fact, one source, two surfaces; the answer to **MP-01**.
- **The lower console** — §14.4(a)/(b) obeyed exactly, verified at 0 red pixels across four scenes.

*⚠ **Three findings are page-wide, not per-page, and should be scheduled ahead of the sweep:***
- ***H-01** — the preview's resolution. It decides how every later legibility finding is measured, so
  everything after this page is provisional until Q5 is answered.*
- ***C-12 + C-04** — `component_48`'s un-erased marker glow and its 12.2% horizontal stretch. Both confirmed
  on the HUD as well as the Cover; both are on all fifteen pages that draw the bar; **and H-07 is coupled to
  C-04 through `FigmaUI.BottomBarHit`, so all three touch one hit map and belong in one commit.***

*Next: **pages 3 + 4 — Procedure (Frame 59) and Cabin (Frame 66)**, inspected together below because
`FigmaUI` routes both to the same 26-line `FigmaFramePage.Build`.*


---

# ⟳ RE-VALIDATION PASS — 2026-09-05, on the honest 1280×703 preview

**Why.** QC's own **H-01** was right and has been fixed by **S100** (`7957d4d`): the preview was rendering
every Figma-era page at 2560×1406 — twice the shipped width in each axis — so **the entire original sweep
was conducted on a lying instrument.** The preview now derives its size from `DragonScreen.cfg`, cannot
render at any other, and writes `MANIFEST.txt` recording every PNG with its size into a folder emptied each
run. This pass re-measured the fourteen scale-dependent findings against that.

**Scope.** Only the fourteen. The correctness findings are untouched and stand as filed — a readout that
contradicts live state does so at any size, as do dead hit rects, duplicate pages, hardcoded colours,
coordinate-system mismatches and un-erased art. **No original text was edited**; each of the fourteen carries
a dated **⟳ RE-VALIDATED** block with the old figure beside the new one.

**Rendered for this pass:** all 104 PNGs, `python plugin/build.py preview`, 2026-09-05 20:13:40, every one
**1280×703** per `build/preview/MANIFEST.txt`. `build.py test` green.

## Verdicts

| finding | verdict | the number that changed |
|---|---|---|
| **C-03** NEXT VIEW overruns its pill | **CHANGED — worse** | overrun 7.0 px = **3.9%** of the label → 6.9 px = **7.2%**. Absolute unchanged, everything around it halved. |
| **C-04** bottom bar stretched horizontally | **STANDS, exactly** | **12.2% → 12.2%.** A ratio of two scales that both halved. |
| **C-05** `FitRows` floor compared in the wrong space | **CHANGED — much worse** | card 1 15.32 px → **7.66 px (48% of the floor)**; cards 2 and 3 were **above** the floor at 17.31 and are now **8.65 px (54%)**. All three now fail. |
| **C-13** the band below the globe | **STANDS** | every proportion identical; absolutes exactly halved (clear band 21.5 → **11.3 px**). |
| **H-06** MANUAL / DOCKING overruns its box | **CHANGED — worse, now two defects** | 109% → **123% of the box**, and the type is now **14.06 px, below the 16 px floor**. |
| **H-07** two fit strategies, two rounded corners | **STANDS** | the frame's edge is a rule **69.6 px** inside the bar's span (was 139.3); corners ~50 px apart (was ~100). |
| **H-08** frame art upscaled and soft | **⛔ VANISHED — struck** | scale **1.1140 (up) → 0.5570 (down)**; sharpness inverts, frame art **0.411** vs bar art **0.311**. Never true of the shipped build. |
| **A-03** three positions off the 498-px cell grid | **STANDS** | design-space grid untouched; offsets +31.4/+34.4/+34.0 → **+15.7/+17.2/+17.0 panel px**, unchanged as a share of the cell. |
| **A-04** signal glyph low and undersized | **CHANGED — worse** | geometry halves as expected (7.3 px low in a 46.6 px box); **the glyph is now a ~13 px speck**, not the "blob" filed. |
| **A-06** the cabin asset is an empty box | **STANDS, unchanged** | an asset property: **0.23% bright** against 4.74–4.86% for the four seats. Render-independent. |
| **AS-02** Ascent uses a fraction of the width | **STANDS — figure corrected** | filed *"left 40%"* was never measured; **measured 30% of the width, 28.6% of the body area** → ~70% empty. |
| **DB-01** two pages use a corner | **STANDS** | DEORBIT BURN PREP **20% width / 9.4% area**; ENTRY **18% width / 3.1% area**. |
| **NO-01** three of four rings hidden by the globe | **STANDS, exactly** | ring radii as a fraction of the globe radius **0.330 / 0.659 / 0.989 / 1.318** at both scales. **3 of 4** hidden either way. |
| **MP-03** five of nine readouts dashed | **STANDS — figure corrected** | *"roughly a third of the screen's area"* was wrong at **both** scales; measured **8.4% of the area, 47% of the height**. |

## The count, and whether the batch is bigger or smaller than it looked

**STANDS 9** *(C-04, C-13, H-07, A-03, A-06, AS-02, DB-01, NO-01, MP-03)* — three of them with a **corrected
figure** (AS-02 and MP-03 were mis-stated; DB-01's area was estimated).
**CHANGED 4** *(C-03, C-05, H-06, A-04)* — **every one of them worse, none better.**
**VANISHED 1** *(H-08)*.

**The batch is BIGGER than it looked, not smaller.** Thirteen of fourteen survive; the one that died had
already predicted its own death in writing and cost nothing. Four grew, and two of those four — **C-05** and
**H-06** — grew from "a marginal case" into "below the legibility floor", which is a different class of
defect. And the pass exposed **R-01**, which is larger than any of the fourteen.

⚠ **What this says about the original sweep, plainly.** Every *ratio* and *proportion* I recorded survived
intact — several to three decimal places — and every *absolute pixel* figure was exactly double. The
instrument distorted magnitudes, not relationships. The two figures that were actually wrong (**AS-02**'s
"40%" and **MP-03**'s "a third of the area") were wrong at *both* scales: they were eyeball estimates I wrote
as if they were measurements. That is a lesson about my prose, not about the preview.

---

## R-01 — At the shipped width, every sampled text element on every Figma-era page is below the measured legibility floor

**TIER 1** · **NEW — exposed by this re-validation pass, 2026-09-05** · page-wide

**Evidence.** `Typography.Min = 16f` is documented as a **measured** floor — `Typography.cs:2`, *"16 PX IS
MEASURED, NOT CHOSEN"* — and every legacy page uses `Typography.*` directly as a panel-pixel size at the real
1280×703. The Figma-era pages instead write design-space sizes and multiply by `sc = h / 2112` = **0.3329**.
Seventeen elements sampled across nine pages, all from the source constants:

| page | element | source | panel px | vs the 16 px floor |
|---|---|---|---|---|
| CoverPage | rail labels | `Z(32)` | 10.7 | 67% |
| CoverPage | reference card titles | `Z(34)` | 11.3 | 71% |
| CoverPage | reference rows, cards 2/3 | `Z(26)` | 8.7 | 54% |
| CoverPage | reference rows, card 1 | `Z(23.02)` | **7.7** | **48%** |
| CoverPage | attitude-criteria captions | `Z(32)` | 10.7 | 67% |
| CoverPage | map d-pad button labels | `PadLabel 26` | 8.7 | 54% |
| CoverPage | CAMERA caption | `Z(21)` | **7.0** | **44%** |
| SettingsAudioPage | channel labels | `SZ(30)` | 10.0 | 62% |
| SettingsAudioPage | VOX | `SZ(30)` | 10.0 | 62% |
| SettingsAudioPage | **tab strip** Audio/Cabin/Video | `SZ(28)` | 9.3 | 58% |
| VehicleOverviewPage | checklist state words | `SZ(26)` | 8.7 | 54% |
| VehicleOverviewPage | CONSUMABLES rows | `SZ(23)` | 7.7 | 48% |
| VehicleMechPage | SEAT n TACH rows | `SZ(26)` | 8.7 | 54% |
| MenuPage | card labels | `SZ(32)` | 10.7 | 67% |
| DockingSimPage | pad captions | `Z(22)` | **7.3** | **46%** |
| SuitCheckPage | checklist rows | `26` | 8.7 | 54% |
| Frame58Hud | MANUAL / DOCKING | `h * 0.020` | 14.1 | 88% |

**Seventeen samples, seventeen failures, 44% to 88% of the floor.** Confirmed on the render: at 4×
magnification the Cover's ENTRY TIMELINE rows are mush — strokes merge and *"Deorbit burn"* reads as *"bum"*.

**What is wrong.** This is not a per-page layout problem. **The Figma pages were designed at a scale the
shipped screen does not have**, and the 2× preview hid it for two weeks — the same mechanism, and the same
two weeks, as S101's hairline dropout. `Typography.Min` is the project's own measured answer to "can the crew
read this", and the Figma-era pages never consult it.

⚠ **This subsumes several findings and re-sequences others:**
- **C-05** is the same disease in one function. Its fix (compare in panel space) is necessary and still
  right, but on its own it now exposes an overflow the layout cannot absorb — **schedule C-05 behind R-01.**
- **C-03**'s and **H-06**'s filed fixes both propose *shrinking* a label to fit its box. At the shipped width
  both results are below the floor. **Neither fix is safe until R-01 is answered.**
- **MP-03**, **DB-01** and **AS-02** all end in "and the content is too small to read at distance" — that is
  this finding.
- **S101** (the 2px hairline dropout, `REGISTER.md`) is the same statement about strokes rather than type,
  and its own line already says *"Hairlines and type sizes are both 'the design assumes a scale the screen
  does not have'"*. **R-01 and S101 are one job.**

**Fix plan — and this is a design decision, not a mechanical one.**
1. **Raise the type across the Figma pages to clear the floor**, in design space: a design size of **≥ 48**
   is needed for 16 panel px at the shipped scale, against the 21–34 in use. That is a **1.4×–2.3× increase**
   and it will not fit the existing layouts — several pages would need re-laying, and **DB-01 / AS-02 /
   MP-03's empty space is where that room comes from.** Most work, and it is work against the screen the
   crew has. *(Recommended.)*
2. **Raise `screenWidth` in the cfg** so the existing design scale clears the floor. `screenWidth = 2560`
   would restore exactly the sizes the sweep was measured at — but note that **even at 2560 five of the
   seventeen samples are still under 16 px**, so this does not fully solve it either. ⚠ It is also **Q5
   option 2**, which needs `install` + glass time — **an owner gate, C1.12**, which this chat cannot open.
3. **A hybrid**: a modest cfg increase plus a type pass. Picks a number no source names.
4. **Re-measure the floor.** `Typography.Min = 16` was measured for the legacy pages; if it is wrong for
   these, the honest move is to re-derive it — **in the capsule, at IVA distance**, which is glass time again.

**Recommendation: 1, and do not start it until Q5 is formally answered.** S100 fixed the instrument, which
settles what the *preview* does; it does not settle what the *cfg* should say, and option 2 remains open on
the owner's authority. But option 1 needs no gate, can start today, and is the only option that treats the
cause rather than the symptom.

**Must not break:** the legacy `Pages.Build` screens, which already use `Typography.*` correctly at the real
size and must render byte-identically.
**Verify:** re-render every Figma page and assert that no drawn text size falls below `Typography.Min` in
**panel** pixels — a headless check, which is what would have caught this at the start.


---

# BATCH 1 — SPECIFICATION: THE BOTTOM BAR

> **Owner-directed, 2026-09-05.** The owner said, verbatim: **"give me batch 1"**. Batch 0 was **S100**
> (`7957d4d`), an INSTRUMENT task that fixed the gate itself. This is the batch that follows it.
>
> ⛔ **This section is a SPECIFICATION, not a build.** The QC role is read-and-plan and writes only this
> file; nothing here has been implemented. It is written so a build chat can execute it without re-deriving
> anything. **No owner approval of its contents is claimed** — the owner asked for a batch; this is QC's
> proposal of which one, with the research done.

## What batch 1 is, and why this one

**Three findings, one asset, one hit map, and every page in the build: C-12 + C-04 + H-07.**

| finding | tier | what it is |
|---|---|---|
| **C-12** | 1 | `component_48.png`'s baked tab marker was erased so the marker could be drawn dynamically — **the erase left the glow behind**, so every page carries a permanent ghost marker under icon 0 |
| **C-04** | 2 | the bar is drawn at full panel width against a height-derived scale, so it is **stretched 12.2% horizontally** — circular icons render as ellipses and all its baked type is distorted |
| **H-07** | 2 | on the letterboxed pages the bar is drawn `0…w` while the page art is drawn `ox…ox+RefW·sc`, so **the frame's own border becomes a rule crossing the bar** and two rounded corners collide |

**Why this batch and not another.** Four tests, and it is the only candidate that passes all four:

1. **No owner gate and no open question.** Q1–Q6 do not touch it; `install` and glass time are not needed;
   the whole thing is verifiable from `build.py preview`. *(This rules out **R-01**, whose option 2 is Q5's
   and needs glass; and **A-02 / F-03**, which wait on Q6.)*
2. **One fix, many surfaces.** 21 draw sites covering **all 35 pages** — not a page-at-a-time sweep.
3. **They are coupled and must move together.** The draw, the hit map (`FigmaUI.BottomBarHit`) and the
   marker (`BottomBarMarker`) all encode the same stretched mapping. Fixing C-04 without the other two
   slides every nav icon's touch target off its icon. **Doing these separately means touching one hit map
   three times.**
4. **It follows batch 0 in kind.** S100 made the gate honest; this is the first defect that gate can now
   prove, on every page at once.

⚠ **It is also the batch that S100 makes newly verifiable.** C-12's proof — the ghost showing on a page
whose marker is elsewhere — was measured at 2× and is **re-confirmed at the shipped width below**.

## Research a build chat would otherwise have to redo

### The 21 draw sites, and the three families they fall into

`dl.Asset("component_48", …)` appears in **21 files**. Every one draws it **full panel width × `235·sc`**,
so the 12.2% stretch is universal. But the *pages* fall into three families by how they map x, and **the
right fix differs by family** — this is the single most important thing in this spec:

| family | x mapping | sites | files |
|---|---|---|---|
| **A — LETTERBOXED** | `x·sc + ox` | **11** | `AscentPage` `DeorbitBurnPrepPage` `DockingSimPage` `EntryPage` `FigmaFramePage` `Frame58Hud` `NavOrbitPlotPage` `PlaceholderPage` `RendezvousPage` `SystemsPidPage` `SystemsTreePage` |
| **B — FULL-WIDTH STRETCH** | `x·sx` | **8** | `MenuPage` `SettingsAudioPage` `SettingsVideoPage` `SuitCheckPage` `VehicleMechPage` `VehicleOverviewPage` `VehicleSubsystemPage` `VrioTestPage` |
| **C — FILL-TO-FIT REFLOW** | `x·sc + (x ≥ Split ? extra : 0)` | **2** | `CoverPage` `ManualChuteDeployPage` |

*(11 + 8 + 2 = 21 sites → 35 pages: `FigmaFramePage` serves 2, `PlaceholderPage` 9, `VehicleSubsystemPage` 6,
the other 18 files one each.)*

⭐ **For family A, drawing the bar at `ox … ox + 3427·sc` fixes C-04 and H-07 in one move** — the bar becomes
uniform *and* flush with the page's own frame, and the colliding-corner seam disappears because there is only
one frame edge left. For families B and C there is no letterbox, so a uniform bar leaves 69.6 px of *page
background* at each end, which reads as a gap rather than a frame.

### Where the bar can be split without showing

Measured on `component_48.png` (3427 × 235), interior rows 118–232, excluding its top border:
three empty spans wider than 80 design px —

```
x  632..1097   (466 wide)
x 1466..1942   (477 wide)
x 2121..2666   (546 wide, centre 2393)   <- the widest, between "Sun + GEO" and the SPX block
```

At the shipped width 546 design px is **182 panel px**, and the horizontal slack to absorb is **69.6 px**.
So a split at design **x ≈ 2393** hides the slack inside a span two and a half times its width.

⚠ **An overlap trick was tested and does not work.** Drawing the asset twice at uniform scale — once anchored
left, once anchored right — leaves the right copy visible only from design x ≥ 3008.5, which lands **inside**
the ink group at 3008–3058 and clips a glyph. Rejected; recorded so it is not re-tried.

### The residue, re-measured at the shipped width

Region: bar-local **x 25…145, y 196…229** in the asset, peaking at luminance **112** against a bar background
of **42**, falling back to background at y ≥ 231 — the hard pill was erased, the glow was not. On the honest
1280×703 renders:

| render | active tab | residue mean | plain bar | excess |
|---|---|---|---|---|
| `ui_cover.png` | icon 0 | 97.4 | 40.9 | +56.5 |
| `ui_ascent.png` | icon 0 | 97.4 | 40.9 | +56.5 |
| **`ui_cabin.png`** | **icon 4** | **68.3** | 40.9 | **+27.4** |
| **`ui_audiovideo.png`** | **icon 4** | **68.3** | 40.9 | **+27.4** |

**The last two are the proof**: their marker is under icon 4, and the residue under icon 0 is still there at
+27.4 above background. It is not the marker. At the shipped width it lands at panel **x 9.3…54.2,
y 690.0…701.0**.

## The build

### 1 — C-12: finish the erase *(no decision needed)*

Clear `component_48.png` to its own flat background `#111B52` (sampled RGB **17, 27, 82**, alpha 255) across
**bar-local x 20…150, y 190…235** — deliberately wider than the measured 25…145 / 196…229 so no fringe
survives resampling. It is a rectangle fill on a flat region, not a retouch.

- ⚠ **Record the edit in `docs/ASSET_INDEX.md`.** This is the second edit to a community-Figma export (the
  first being the original erase); the repo copy's divergence must stay written down (C7.1).
- ⚠ **Do not touch `MarkY` / `MarkH` / `MarkW`** (`FigmaUI.cs:278`). They are measured from the erased block,
  and the block being cleared is deliberately larger than the marker being drawn.

### 2 — C-04 + H-07: one uniform bar *(a decision is needed — see below)*

**Family A (11 sites) is unambiguous.** Draw the bar at `ox … ox + 3427·sc`, uniform. C-04 and H-07 both
close. This is 11 of the 21 sites and the higher-value half.

**Families B and C (10 sites) need a choice.** Three shapes, and QC does not decide it:

| # | option | cost | result |
|---|---|---|---|
| **1** | **Add `DisplayList.AssetUV`** — a sub-rect asset draw mirroring the existing `ImageUV` — and split the bar at design 2393, left half anchored left, right half anchored right | a new primitive in **both renderers** (`ScreenPainter.DrawImage` + `PreviewMain.DrawCoverAsset`) | exact: uniform scale, content flush to both edges, slack invisible |
| **2** | **Draw uniformly at `ox` on every page**, and fill the two end strips with the bar's own `#111B52` plus a 1 px top-border continuation | no new primitive | uniform everywhere; the bar's content insets from 10.5 px to **78.9 px** from the left edge on families B and C |
| **3** | **Fix family A only**, leave B and C stretched | least work, least risk | 11 of 21 sites correct, 10 still 12.2% wide — an inconsistency that would need recording |

⚠ **Option 1 is the correct result and the riskiest route.** A new draw primitive implemented twice is
exactly the class of divergence **S75** found — where `PreviewMain` ignored asset tint for months and the
preview and the capsule silently disagreed. If it is taken, the two implementations must land in one commit
with a headless test that pins them together, the way `ScreenSizeTest` now pins the render size.

**QC's recommendation: option 2.** It fixes all 21 sites, needs no new primitive, and therefore cannot
introduce a two-renderer divergence. Its only cost is a 68 px content inset on families B and C — and on
family A that inset **is** the frame, which is the point. If the inset is judged wrong once it is on the
glass, option 1 remains available and the family-A work is not wasted.

### 3 — The hit map moves with the draw *(mandatory, whichever option)*

`FigmaUI.BottomBarHit` (`:121-131`) maps a touch by `BarIconX[i] / RefW * w` and `BottomBarMarker`
(`:281-286`) places the marker by the same rule — **both assume the current stretch.** They must change in
the same commit as the draw or every nav icon's touch target slides off its icon, on all 35 pages.

⚠ **The bar is the one control the crew can always rely on** — `FigmaUI.HitTest` tests it first, before any
page control, for exactly that reason. A silent break here is the worst possible outcome of this batch.
**Derive all three (draw, hit, marker) from one function**, as `MenuPage.CellRect` already does for the Menu
grid — *"the one source of truth Build, HitTest and the headless nav test all share."*

## Must not break

- **`ScreenSizeTest`** — S100's fence. Nothing here may re-enlarge the preview.
- **The tab strip on `UiPage.Cabin`.** `F-04` records that it is baked into `frame66.png` and survives only
  because `component_48`'s top ~105 rows are transparent. Changing the bar's geometry on a family-A page
  moves what shows through. **Re-render `ui_cabin.png` and check the three tabs are still legible and still
  inside their hit bands.**
- **`FigmaUINavTest`** — it exercises the bar's routing and must be re-run, not re-pinned to old numbers.
- **The `_nofeed` / `_alarm` / `_alerts` render variants** — the bar is on all of them.

## Verify

1. `component_48`'s crosshair icon renders **square** (bbox ratio 1.00 ± 0.05) on `ui_cover.png`. It is
   exactly 130 × 130 in the asset; today it renders 23 × 21.
2. The residue probe — panel **x 9.3…54.2, y 690.0…701.0** — reads within 2 luminance units of the plain bar
   background on a page whose active tab is **not** icon 0 (`ui_cabin.png`, `ui_audiovideo.png`).
3. On a family-A page (`frame58_hud.png`), **no vertical rule at x = 70** and **one** rounded bottom-left
   corner, not two.
4. A headless check that the bar's drawn width ÷ 3427 equals its drawn height ÷ 235, and that each icon's
   drawn centre maps back inside its own `BottomBarHit` band — at two panel aspects.
5. `build.py test` green; all 104 preview PNGs re-rendered and the manifest checked.

## What batch 1 deliberately does NOT include

- **R-01** (page-wide sub-floor type) and **S101** (the hairline dropout) — one job between them, TIER 1, and
  **the strongest candidate for batch 2** — but its option 2 is **Q5's** and needs `install` + glass, an owner
  gate (C1.12). ⚠ It also **blocks the filed fixes for C-03 and H-06**, so those cannot precede it either.
- **V-01 + S-01** (32 hardcoded gauge colours across 7 pages) — unblocked for cabin temp, CO2 and the loops,
  where `SystemsPidPage.cs:249` already makes the right call; but PPO2's and pressure's bands are a §1.4
  question and `CabinLimits` is mirrored into Python for the BlackBox report (**BB3-Q1**, still open). **A
  good batch 3.**
- **The S75 inert-tint hoist** (SC-02, DK-02, RZ-01, and A-02/F-03 behind Q6) — five pages, one shared tint,
  no gate on three of them. **A good batch 4**, and small.
- Everything gated on **Q1** (the Figma export), **Q2** (glass), **Q3**, **Q4**, **Q6**.
