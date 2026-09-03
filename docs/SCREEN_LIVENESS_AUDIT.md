# SCREEN LIVENESS & GAP AUDIT

> **RESEARCH DOC — owner-directed, 2026-09-03.** A systematic walk of every screen, every page and every
> Cover phase, recording what is LIVE, what is STATIC, what is a NO-OP and what is a MICRO-SIM — then
> classifying every remaining hole so the build knows what to research and fill *before* it starts filling.
> **No code was changed, no plan was edited, no gate was touched.** Subordinate to `docs/BUILD_PLAN.md`
> (C7.1) — on any conflict, **the plan wins**.

**Method.** Five parallel read-only walks over `plugin/src/pure/` + `plugin/src/`, one per page family,
cross-read against `docs/BUILD_PLAN.md` §3 / §6 / §14.4 / §B12.5 / §B14, `docs/SCREEN_INVENTORY.md`,
`docs/TELEMETRY_REGISTRY.md`, `docs/COMMAND_REGISTRY.md`, `docs/SCREEN_SPEC.md` and the open register
strays. Every claim carries a `file:line`.

**Verification status — read this before acting on a line.** The structural claims in §1 were re-verified
directly against the source by the audit author. The per-element tables are the walks' own findings at the
cited lines, spot-checked rather than exhaustively re-derived: **treat a `file:line` as a pointer to check,
not as a warrant.** Line numbers drift; re-read the site before editing it.

---

## 0. THE GOVERNING RULE — §14.4(f), and why it *is* the A/B axis

§14.4(f) landed **today** (`BUILD_PLAN.md:949-967`, commit `a913270`) and it draws this audit's
classification line for us, so the line is the owner's, not this document's:

- **READOUTS / DISPLAYS** — *"every feature the real Dragon screens have is INCLUDED … live from a real
  source wherever one exists … where no live source exists for a physically-real quantity, SIMULATE it:
  this REPLACES the honest-dash fallback."* A simulation must **behave live**, must **compute any safety
  verdict from its own model** (the S31/S32 guardrail), and must be **marked in code**. A dash survives
  only for a **genuinely-absent state within an included feature** (no target → no docking error).
- **ACTUATION** — *"⛔ SCOPE: this governs READOUTS / DISPLAYS only. Flight ACTUATION … is UNCHANGED: it
  stays §14.4(a) honest-no-op until Part B wires it"* (`BUILD_PLAN.md:962-965`; the same boundary is
  restated on §14.4(a) itself at `:926-927`).

The audit's classes map straight onto that boundary:

| class | meaning | basis |
|---|---|---|
| **(A) PART-A-ACHIEVABLE-NOW** | a READOUT, or a control whose whole effect is screen state. Fill it: live source first, else a coherent marked sim. | §14.4(f) READOUTS — the completeness mandate |
| **(B) PART-B-GATED** | commands the vehicle, or reads an engagement / authority / fault state only the conductor can produce. | §14.4(f)'s ⛔ scope line + §14.4(a) + §B12.5 / §B14 |
| **(C) DISPLAY-ONLY / REFERENCE** | correctly static: real procedure copy, a diagram that is not an instrument, a legend, or a control deliberately inert per §14.4(b) pending a source. | §1.4 / §14.4(b) |

**The distinction that does the most work in this document:** a procedure's **step TRACKING** is a readout
of real vehicle state and is therefore **(A)**; the same step's **ACTION BUTTON** fires a pyro and is
**(B)**. The Manual Chute page is the clearest case — its altitude gates can go live now, its
`DEPLOY DROGUES` button cannot. The two sit on the same row and belong in different classes.

---

## 1. THREE STRUCTURAL FACTS THAT REFRAME EVERYTHING BELOW

These are not holes in a page — they are properties of the build that decide what "live" can even mean.
All three were re-verified directly.

### 1.1 `FigmaMode = true` makes an entire second UI unreachable

`plugin/src/ScreenPainter.cs:56` — `private const bool FigmaMode = true;`

It gates **both** paths: the draw path (`ScreenPainter.cs:899`, with `Pages.Build` stranded in the `else`
at `:961`) and the touch path (`ScreenPainter.cs:361`, whose Figma branch `return`s at `:442` before
`Apply(Pages.HitTest(...))` at `:475` is ever reached).

Everything below is **compiled, tested, previewable — and unreachable in the shipped build**:

| stranded | what it is | why it matters |
|---|---|---|
| `pure/StepList.cs` | a **15-row live ascent/prelaunch state machine** — crew aboard, escape armed, prop load, liftoff, Max-Q (a latched peak detector), MECO, stage sep, SECO, Dragon sep, nose-cone open — every row resolved off real KSP state (`StepList.cs:175-211`, fed from `VesselData.cs:386-428`), plus an 8-mode `AbortMode()` (`:228-249`) | **the only real procedure state machine in the tree.** H34's fix is largely "route what already exists" |
| `pure/GateCard.cs` + `PageAct.AckStep` | the crew-gate card and the crew's tap on a step | the §B14 crew-gate UI. Doubly dead — see §1.2 |
| `pure/SettingsPage.cs` (478 lines) | LIGHTS (really calls `ActionGroups.SetGroup`, `VesselData.cs:1120-1128`), seat-view (`CameraManager.SetCameraIVA`), brightness (really tints the render, `ScreenPainter.cs:1192-1198`), CAPTURE, the per-display page grid | **eight working handlers behind an unopenable door.** "The settings page toggles the lights" is true of the code and false of the game |
| `pure/NavPage.Build` — the NAV **page** | its two readout columns plus a complete, wired pan / zoom / **NEXT VIEW** cluster (`NavPage.cs:893-929` draw, `:114-131` hit, `ScreenPainter.cs:685-697` dispatch) | there is **no `UiPage.Nav`** in the enum (`FigmaUI.cs:21-80` — verified, zero matches). The renderers `NavPage.Map/Orbit/Planet/Globe` **are** live and reused by Cover / ManualChute / Rendezvous / NavOrbitPlot; only the *page* and its controls are orphaned |
| `pure/ChromeBar.cs` | the 5-tab bar and its per-tab **alert routing** | `ScreenPainter.cs:893` computes `chrome.AlertMask = Alarms.Mask(ps)` and `:895` `chrome.VehicleState` **every frame, before** the `FigmaMode` branch — then discards both |
| `pure/DockingPage.cs`, `DockingPageCentral.cs`, `AttitudeHud.cs`, `MechPage.cs` | the pre-Figma docking / attitude pages | instructive: `DockingPage.cs:73` draws the **full-bleed docking camera** the live HUD does not |

**Consequence:** several "missing" features are not missing, they are **stranded**. That changes their cost
from *build* to *route*, and it is why the recommended order in §6 opens where it does.

⚠ **This audit does not propose flipping `FigmaMode`.** The Figma rebuild is the current design
(`SCREEN_INVENTORY.md`, §3 of the plan); the legacy path is kept "so the build stays valid and the change
is reversible" (`ScreenPainter.cs:50-55`). The stranded assets should be **harvested into Figma pages**,
not switched back on wholesale. Which of them are worth harvesting is Q1 in §8.

### 1.2 The idle stub pins nineteen `PageState` fields to a constant

`plugin/src/_AutopilotStub.cs` is the deliberate seam (CLAUDE.md: **NOT stale — leave it alone**). Its
effect on the glass is worth stating precisely, because these are **not** Part-A holes — they are Part B's
sockets, already wired correctly at the display end:

| field(s) | stub source | value it can only ever have |
|---|---|---|
| `AutoEngaged` / `AutoPhase` | `CrewProcedureOps.Engaged` `:32` | `false` / `null` |
| `Mode` / `ModeText` | `FlightDriver.MissionMode` `:52` | `Idle` / `"IDLE"` |
| `Fault` / `FaultResponse` / `FaultText` | `FlightDriver.LastFdirReport` `:53` | `None` / `Continue` / `"NOMINAL"` |
| `GateActive` / `GateTitle` / `GateItems` / `GateStage` | `CrewProcedureOps.CrewActionNeeded()` `:37` | `false` / `null` / `null` / never written |
| `RendezvousEngaged` / `RendezvousNote` | `StationApproach` `:144` | `false` / `null` |
| `DockEngaged` / `DockNote` | `DockingOps` `:145` | `false` / `null` |
| `UndockEngaged` / `UndockNote` | `UndockOps` `:147` | `false` / `null` |
| `DeorbitEngaged` | `DeorbitOps` `:146` | `false` |

One stub read **degrades gracefully and stays live**: `Mission.AuthoritativePhase` falls through to the
real classifier because `Engaged` is false (`MissionPhase.cs:113-116`), so `s.Phase` is genuinely live.
**That is the pattern the rest should copy** — see H27, where a card could read an actual approach off
`HasTarget` / `RangeM` / `Closing` instead of a stub that says `NOT ENGAGED` for the whole mission.

### 1.3 On three surfaces the readouts are pixels in a PNG

Not "a constant in C#" — **burned into the reference art**, so no amount of `PageState` wiring reaches them
until the art is edited or the values are overdrawn.

- **The Cover's entire top telemetry strip.** Verified: `CoverPage.cs:30-31` lists the asset keys and
  `:52-53` their boxes; the files exist on disk (`art/cover/altitude_393_3km.png`, `apogee_416_2km.png`,
  `perigee_379_4km.png`, `inertial_velocity_7_69km_s.png`, `splashdown_time_t_01_24_51.png`,
  `inclination_51_62deg.png`, `active_phase_deorbit_coast.png`, `running_00_22_57.png`). Verified by grep:
  **`CoverPage.cs` contains no read of `s.Altitude`, `s.Velocity`, `s.Apoapsis`, `s.Periapsis`, `s.Phase`,
  `s.SplashdownText` or `s.InclinationDegText`.** `PageState s` reaches exactly one function in the file —
  `DrawCameraView`.
- **The whole Frame 58 HUD.** Verified: `Frame58Hud.Build` is **five draw calls** — background, one
  `dl.Asset("frame58", …)` (`:30`), the nose-cone-gated camera disc, the MANUAL/DOCKING margin button, the
  bottom bar. Verified by grep: the **only** `PageState` read in the file is `s.Steps.NoseConeOpen` at
  `:33`. `ROLL 15.0°`, `PITCH -20.0°`, `YAW -10.0°`, the three `0.0 °/s`, `X 200.0 / Y 12.0 / Z 30.0`,
  `RANGE 202.6 m`, `RATE -0.031 m/s`, `ACCELERATION 0.00g`, `FRAME LVLH`, `CAMERA Virtual` are all pixels.
  The file's own header says so (`:10-11`).
- **The bottom status bar, on every page in the build.** `component_48.png` bakes `CURRENT STATE`,
  `POINTING MODE`, the `SPX / GND / TDRS / ISS` link block and a counter (~30 call sites). Only the sliding
  active-tab marker is dynamic (`FigmaUI.cs:281-286`) — and **the precedent for the fix is in that same
  file** (`:276-277`): the marker *was* baked into `component_48.png` and **was erased from the PNG** so it
  could be drawn live.

**The irony running through all three:** `PageState` already carries live, pre-formatted values for nearly
every one of those numbers, and `docs/TELEMETRY_REGISTRY.md` already registers most of them with an
authority. The data is not missing. It is **not drawn**.

---

## 2. PAGE-BY-PAGE LIVENESS — the whole `UiPage` set + the Cover phases

`UiPage` has 35 values (`FigmaUI.cs:21-80`). Reachability is from `FigmaUI.Build` (`:190-232`),
`FigmaUI.HitTest` (`:288-365`) and `MenuPage.BuildEntries` (`:37-47`).

| # | UiPage | reachable via | liveness verdict | holes |
|---|---|---|---|---|
| 0 | **Cover** | bottom bar, Menu | **One live region.** The camera slot (globe / flat map / capsule) is fully LIVE; rail highlight + CTR lamp are local UI state; the turntable is a real 36-frame interactive sequence. Everything else is baked art or C# literals | H1–H9 |
| 1 | **Hud** (Frame 58) | bottom bar, Menu | **Two live things** — the nose-cone flag and the docking-cam disc it gates. Every readout is baked (§1.3) | H10, H11 |
| 2 | **Audio** settings | bottom bar, Cover, Docking | **Display-only.** `sel` is the literal `2` (`FigmaUI.cs:197`); channel values `12dB/0dB/100/+9dB/50` are a literal array (`SettingsAudioPage.cs:34`); the eight ± buttons and two fan buttons are **drawn art with no HitTest in the file at all** | H12 |
| 3 | **Procedure** (Frame 59) | Menu only | **One PNG.** `FigmaFramePage.Build`, `Commands = 8`, no `PageState`, no HitTest branch — a dead end escapable only by the bottom bar | H13 |
| 4 | **Cabin** (Frame 66) | settings tabs, Menu | **One PNG** (1975 KB of baked cabin state) while `CabinEnvironment` runs live beside it | H13 |
| 5 | **Menu** | Cover menu button | Nav index; grid membership is compile-time. `Build` takes **no `PageState`** | — (C) |
| 6–9 | PhaseDeport / PhaseCoast / PhaseClaw / PhaseManual | **UNREACHABLE** | dead enum values — excluded from Menu, never a `NavHit` target | H9 |
| 10–13 | ActOnSpaceX / ActDeorbitBrief / ActReview / ActAcknowledge | **UNREACHABLE** | dead enum values (the Cover *buttons* of these names are a separate thing — H5) | H9 |
| 14 | Entry ("ENTRY GO / NO-GO") | **UNREACHABLE** | a dead enum value carrying a real screen name | H9 |
| 15 | **Vehicle** (All) | bottom bar, tabs, Menu | **Mixed — the best-wired family.** Four cabin gauges live/micro-sim; CONSUMABLES 4 of 8 live; tab severities live | H14, H16, H17, H18 |
| 16 | **SuitCheck** | bottom bar, Menu | **The exemplar MICRO-SIM** (S31/S32) — four ΔP rows and the STATUS verdict computed from `SuitLeakSim`, never hardcoded. But **no step is tracked**: both left ticks draw checked at page-open, and "SECTION 2: IN PROGRESS" never advances | H19, H20 |
| 17 | **VehicleMech** | tabs, Menu | Three of five donut nodes live off real acceleration; SEAT n TACH ×4 dashed; `Awaiting` static | H14, H15 |
| 18 | **AudioVideo** | settings tabs, Menu | Camera list is **LIVE read-only** off a real `MuMechModuleHullCameraZoom` scan; **tapping a camera row does nothing** — its only writer is in the dead `Apply` | H12 |
| 19 | **VrioTest** | Menu only | **Fully static.** `Build(dl,w,h)` — **no `PageState` parameter**; checklist state is `bool[] Done = {true,true,true,true,false}` (`:34`); **no HitTest in the file and no glue branch** — START / STOP / NEXT are pixels | H21 |
| 20–25 | **VehicleCrew / Propulsion / Power / Avionics / Gnc / Thermal** | tabs, Menu | Gauges and detail rows substantially live; **31 status words are literals and, unlike the Overview, are not even `!Valid`-guarded** | H14, H16, H17 |
| 26 | **ManualChute** | Cover rail, Menu | **Telemetry strip fully live** (T13c) plus the live globe. **Step rows are static strings**; 8 of 12 action buttons are no-ops; the rail index is the literal `6` | H22, H23 |
| 27 | **Docking** | HUD margin, Menu | **Bearings genuinely LIVE** (S26) — diamond placed off `YawDeg`/`PitchDeg`, green-when-corrected, PYR rates, RANGE/RATE, all dashing with no target. **12 pads + Reset Positions are no-ops.** No camera behind the rings | H24, H25, H26 |
| 28 | **Rendezvous** | Docking margin, Menu | **Plot fully LIVE** — real conic, real target chord from real UT state vectors. **Hold-Capture card reads `NOT ENGAGED` forever**; ◄/► have no hit rect; the icon rail is inert | H27, H28 |
| 29 | **DeorbitBurnPrep** | Menu only | Crew Interrupt Conditions are static text; the four SLEW rows are **literal dashes**; FC SLEW is correctly wired to a pinned stub. No touch at all | H29, H30 |
| 30 | **EntryProcedure** | Menu only | **Nothing live. `Build(dl,w,h)` takes no `PageState`** — structurally provable | H31 |
| 31 | **SystemsTree** | Vehicle deep-view links, Menu | **Genuinely live-coloured** off `SystemsState` plus real solar/battery counts. **Read-only** — no HitTest anywhere | H32 |
| 32 | **SystemsPid** | Vehicle deep-view links, Menu | Loops and atmosphere live-coloured; **every valve but one is a fixed-colour glyph**; PUMP A/B `"RUNNING"` is a literal not even `Valid`-guarded; CABIN HX A/B are **empty boxes** | H32, H33 |
| 33 | **Ascent** | Menu only | **One live element** — `ACTIVE PHASE`. All 11 ascent events are a static string array, while `StepList` computes live equivalents of six of them and is never read | H34 |
| 34 | **NavOrbitPlot** | Menu only | Plot LIVE (shares `NavPage.Orbit`); G-FORCE / RATE / RANGE live; **the range rings carry no scale**; no touch controls | H35, H36 |

### The Cover phases (the 7-item rail)

| slot | name | body drawn | navigates |
|---|---|---|---|
| 0 | Deport & Burn | **the Coast body** | no |
| 1 | Coast to Trunk | the Coast body | no |
| 2 | Claw Sep. Prep | **the Coast body** | no |
| 3 | Procedure | **the Coast body** | no |
| 4 | Procedure *(duplicate name)* | **the Coast body** | no |
| 5 | Reference Content | its own real §8 content (T3) | no |
| 6 | Manual Chute | — | **yes** → `UiPage.ManualChute` |

Verified: the body swap is gated on index 5 alone — `bool refPhase = (sp == ReferencePhase)`
(`CoverPage.cs:323`; `const int ReferencePhase = 5` at `:133`), applied at `:337`. **Five of seven rail
items render byte-identical content under different headings.** `REGISTER.md:171` records that this was
true of all seven before T3, which fixed exactly one.

### The lower analog console panel (38 buttons + the EJECT handle)

| group | count | class |
|---|---|---|
| POWER 1/2, STRING 1A–2C, RESET 1/2 | 10 | **MICRO-SIM** — real `VehicleSystems` display state, ticked off real EC / g / heat |
| DEPRESS RESPONSE, SUPPRESS FIRE, FIRE RESPONSE (two plates) | 6 | **MICRO-SIM** |
| ENABLE BACKUP PYROS | 1 | **LIVE display flag** — latches, and the Manual Chute page reads the same flag (one state, two surfaces) |
| SWAP 1/2/3, ENTRY REBOOT, BACKUP ENTRY, NORMAL ENTRY | 6 | **INERT by §14.4(b)** — **(C)**, pending a real console-procedure source |
| JETTISON NOSE CONE, MAINS ONLY, DROGUES & MAINS, CUT MAINS, FIRE PYRO, EJECT handle | 6 | **NO-OP actuation** — **(B)** |
| WATER DEORBIT, DEORBIT NOW, BREAKOUT (two plates) | 6 | arm is real, fire is a no-op — **(B)** |
| CANCEL, EXECUTE (two plates) | 4 | interlock real; dispatch always refuses — **(B)** |

⚠ **Correction to a premise this audit was briefed with:** RESET 1/2 are **not** inert.
`PanelPolicy.IsInert` (`PanelBehaviour.cs:78-86`) lists exactly six commands and RESET is not among them;
`BUILD_PLAN.md:136-137` records the owner's choice keeping RESET as real display-state.

---

## 3. THE HOLES

Each hole: what it is, what is missing, and its class. **(A)** carries a sketch of the model and what to
research; **(B)** is named as Part-B's and no Part-A build is proposed for it; **(C)** is recorded so it is
not re-logged as a defect.

### Cover

| id | hole | class | note |
|---|---|---|---|
| **H1** | **The entire top telemetry strip is baked art from someone else's flight** — SPLASHDOWN TIME, INERTIAL VELOCITY, ALTITUDE, APOGEE, PERIGEE, INCLINATION, ACTIVE PHASE (§1.3) | **(A)** | **Nothing to research — nothing to model.** All seven exist live and pre-formatted: `s.SplashdownText`+`SplashdownShown`, `s.Velocity`, `s.Altitude`, `s.Apoapsis`+`ApogeeShown`, `s.Periapsis`+`PerigeeShown`, `s.InclinationDegText` (which exists *specifically* to print the `51.64°` glyph form this art bakes), `s.Phase`. Build = add the keys to `SkipKeys` and draw `dl.Text` at the measured boxes, exactly as `ManualChuteDeployPage` already does for the same seven values. **The single highest value-per-line item in the build.** |
| **H2** | **`RUNNING 00:22:57` is a frozen clock** (`CoverPage.cs:32`) | **(A)** | A stopwatch is the one element a viewer assumes is live. Model: a phase-entry timestamp in the painter, formatted on the second (the codebase's format-on-change rule, `Pages.cs:46-47`). Research: none. |
| **H3** | **TARGET LATITUDE / LONGITUDE are baked, and the baked longitude repeats the latitude's value** (`26° 15.00° N` on both) | **(A)** | Two options, and the choice is the research: (i) wire `s.TargetLat`/`s.TargetLon`/`s.HasTargetGround` — correct, live, already used by `NavPage.cs:268-271`, but it is the *nav target*, not a splashdown site; (ii) a splashdown-point predictor — `s.SplashdownText`/`SplashdownShown` prove a descent model already exists in `VesselData`. ⚠ §B11's **O7** records that the seven real splashdown sites have **no published coordinates**, so (ii) cannot be sourced, only modelled. |
| **H4** | **Five of seven rail phases draw identical content** (§2) | **(A)** | Four content sets missing. Two candidates already exist as built pages reachable only from the Menu grid — `DeorbitBurnPrep` and `EntryProcedure`. Research: which of the seven rail slots those two belong behind. ⚠ **S27 already put this to the owner and the owner declined to assign the two generic "Procedure" slots** (no source names their content). So H4 splits: routing slots 3/4 is **owner-gated**, but giving slots 0/1/2 their own bodies is not — the deorbit sequence they name (Deport & Burn / Coast to Trunk / Claw Sep Prep) is documented in §8 exactly as slot 5's content was. |
| **H5** | **All four `Act*` buttons are silent no-ops** — named hit rects, no dispatcher case, and not even the honest-refuse log the chute page emits | **(A)**, with one caveat | `ActReview` ("Review Reference Content") should select rail index 5 — **its own label says so, the destination exists, one line.** `ActAcknowledge` is a crew-ack latch — pure local state. `ActDeorbitBrief` should route to `DeorbitBurnPrep` — routing only. `ActOnSpaceX` ("begin procedure 4.700") is a *ground-authorisation* item, not a vehicle command — a local latch. **None commands the vehicle**, so all four are (A). ⚠ Fix **H8 first**. |
| **H6** | **ENTRY ENABLED shows `True` **and** `False` at once**, neither lit, and `EntryTrue`/`EntryFalse` resolve to nothing | **(A)** *if* crew-verification; **(B)** *if* vehicle-arming | The ambiguity is the research. If the row means *"the crew verified it"* → local latch, (A). If it means *"the vehicle has armed entry"* → it is an arming flag on the §14.4(a) side, (B). The baked step text beside it (`CoverPage.cs:44-45`) reads as crew verification, which argues (A) — but this needs the owner's read of the reference, so it is **Q2 in §8**. |
| **H7** | **The Cover has no alarm surface at all.** `Alarms.Mask` folds in G-force, propellant, power and the full FDIR spine every frame (`ScreenPainter.cs:893`) and is discarded (§1.1) | **(A)** | The crew's home page cannot show a caution. `Alarms.cs:2-3`'s own header: *"THE ALERT ROUTING IS THE POINT, NOT THE DECORATION."* Model: one status field tinted by `Alarms.SystemSeverity(ps)`; `StatusIndicator` and `VehicleTabBar` already render severity. ⚠ **`Alarms.Mask` bit 2 (NAV) is never set** — bits 0/1/3 are; harmless today, a silent gap the moment the channel is reconnected. |
| **H8** | **Phantom hit rects on the Reference Content phase.** `CoverPage.HitTest` takes `cam` but **not** the phase, and `Hits` is unconditional — on slot 5 the Act*/Entry labels are suppressed but their rectangles still fire over the reference text | **(A)** — and a **prerequisite** | Harmless today only because H5's targets are no-ops. **The moment H5 is fixed, tapping the ENTRY TIMELINE triggers deorbit actions.** Fix = pass `sp` into `HitTest` and gate the six rows. **Do this before H5.** |
| **H9** | **Nine `UiPage` values (6–14) are wired to nothing**, and `PlaceholderPage` is therefore unreachable except from a stale persisted page int | **(C)** — record, don't build | The numbering is already reserved for exactly H4/H5's destinations. `UiPage.Entry` (14) carries the title "ENTRY GO / NO-GO" — a real screen name pointing at nothing. S14 already cleaned the Menu of these; nothing further is owed unless H4/H5 land. |

### HUD (Frame 58)

| id | hole | class | note |
|---|---|---|---|
| **H10** | **Every HUD readout is pixels** (§1.3) — ROLL/PITCH/YAW correction and rate, X/Y/Z, RANGE, RATE, ACCELERATION | **(A)** | **The largest liveness gap in the build.** Every value exists live in `PageState` — `RollDegText`, `PitchDegText`, `YawDegText`, `Roll/Pitch/YawRateText`, `OffX/Y/ZText`, `RangeText`, `RateText`, `AccelPosText` — and `DockingSimPage` already draws the same fields correctly. Build = overdraw at the frame's measured coordinates (H1's method). Research: the coordinates, from `docs/UI_AUDIT.md` (the plan's rule: **from the reference's own source, never a screenshot**). ⚠ The no-target and no-feed looks must be designed too — the page currently cannot show either. |
| **H11** | **`FRAME LVLH` / `CAMERA Virtual` are baked labels**, and `FAR FIELD POSITIONING` + the `0s / RESET / START` timer are baked with no hit rects | mixed | FRAME/CAMERA as *readouts* → **(A)** (`HullCams.Labels()` already supplies real camera names). The timer → **(A)** (local). `FAR FIELD POSITIONING` is a **GNC mode command** → **(B)**. |

### Settings & frame pages

| id | hole | class | note |
|---|---|---|---|
| **H12** | **No reachable settings control does anything.** Audio's ± buttons and fan buttons have **no HitTest in the file**; the video page's camera rows draw a live selection whose only writer is stranded (§1.1); the working settings page is unreachable | **(A)** | All screen-state or vessel-display: lights, brightness, seat view, camera selection, page-per-display. Build = re-home the stranded handlers onto a reachable Figma settings page. ⚠ Audio faders are **(C)** — `SettingsPage.cs:27-29` records the deliberate decision that stock KSP has no cabin audio, so the values stay display-state. |
| **H13** | **`UiPage.Procedure` and `UiPage.Cabin` are flat images** — 8 draw calls for a whole screen, no `PageState` | **(A)** | This is the element-by-element rebuild `FigmaFramePage.cs:9-11` says is the plan. For Cabin the data is all sitting there: `s.Cabin`, `Ppo2Text`, `Co2Text`, `PressText`, `CabinTempText`, `LoopAText`/`LoopBText`, `CrewText`, `Crew01`, and `Alarms.LifeSupport`/`Thermal` already band them. ⚠ Procedure (Frame 59) is a *generic template* — §3 calls it a placeholder template, so rebuilding it needs a decision about what procedure it holds. |

### Vehicle family

| id | hole | class | note |
|---|---|---|---|
| **H14** | **~40 status words are literals.** The six subsystem tabs are **not even `!Valid`-guarded**: `VehicleOverviewPage.cs:113` gates its checklist on `!valid`, `VehicleSubsystemPage.cs:130-131` does not — verified. On a dead feed all four gauges dash while the left column still reads green `Nominal / Active / Clear / 16 / 16 / Open / Ready / None / Lock / Enabled / Valid / Auto / Deployed` | **(A)** | **This is S22's own failure, one page over** — see §4. Two tiers: **(i)** the `!Valid` guard, a mechanical fix routing the literals through the existing `T()` and gating `sc` on `valid`; **(ii)** the deeper one — under §14.4(f) these words should be *computed*, not merely dimmed. |
| **H15** | **Eight status words contradict live state already on the same screen** | **(A)** — the cheapest wins in the audit | `SMOKE DETECT "Clear"` vs live `s.Systems.Fire` (the P&ID prints DETECTED off it) · `MANIFOLD LEAK "None"` vs live `s.Systems.Leaking` · `OMS/RCS "Ready"` and `RCS AUTHORITY "Enabled"` vs live `s.RcsOn` (which `PropSchematic` prints on the same tab) · `ATT CONTROL "Auto"` vs live `st.ModeText` (drawn four rows below) · `COOLANT LOOP A/B "Nominal"` vs live `Alarms.Band(Cabin.LoopAC, …)` · `HEAT SHIELD "Nominal"` vs live `s.HullTemp01` (the SHIELD gauge two lines down). **No new model, no new source — read the field, drop the literal.** The template already exists: QC-AUDIT finding 3 did exactly this for MAIN BUS A/B. |
| **H16** | **The ALERTS view is a one-word summary, and it prints a green `NOMINAL` on a dead feed** beside its own honest `NO DATA` | **(A)** | No enumerated list, no timestamps, no acknowledgement; the FDIR bar's `0.15/0.6/1` fill is a fake three-position gauge under a real word. `Alarms.Mask` + `SystemsState` (fire, leak, tripped strings, bus 0/3) already produce discrete events to list. ⚠ Per §1.2 the **FDIR channel itself** is Part B's — an alert *list* built from `Alarms`/`Systems` is (A); an alert list that expects real faults is (B). |
| **H17** | **~27 honest dashes across the subsystem tabs** — Humidity, Chamber Press, SuperDraco Temp, HELIUM, PROP TEMP, BUS A/B voltage, Bus Load, Battery Temp, FC LOAD, BUS TRAFFIC, LINK MARGIN, STORAGE, FC1-3, GPS Sats, Data Rate, RADIATOR, Loop A/B Flow, Heat Reject, Cabin HX, TPS rows | **(A)** — **this is the category §14.4(f) was written for** | Before (f) these were correct. After (f) a dash survives only for a *genuinely-absent* state, and every one of these is a physically-real Dragon quantity. Each wants a coherent marked micro-sim keyed on something real: humidity off crew + power; helium/chamber pressure off Draco duty; bus voltage off SOC droop; FC load / bus traffic off a modelled computer; loop flow off the existing loop temperatures; radiator outlet off hull temp. ⚠ **This is a large surface and a policy question, not one build** — see Q3 in §8. |
| **H18** | **`SHOW MARGINS TO` is a painted button with no hit rect**, and the whole MARGIN column is a hardcoded dash | **(A)** | Worse than a no-op: a no-op at least resolves to a named action. Margin = remaining ÷ rate, and `LifeSupport.Margins` (`LifeSupport.cs:36-47`) already computes exactly this shape of answer off real TAC-LS rates — **and has no caller anywhere.** ⚠ Same class: the Cover's `gridicons_refresh` glyph is drawn with no hit rect. |
| **H32** | **The systems tree and the P&ID are read-only** — neither is in `IsVehiclePage`, neither has a HitTest, so `POWER 1/2` and the six `STRING nX` nodes are untouchable — while `Systems.ToggleBus`/`ToggleString`/`ResetBus` exist, work, and are wired **only** to the physical IVA plate | **(A)** | **The highest-value gap in the Vehicle family.** The target model is entirely local (`SystemsState`), the dispatcher already exists, and the tree already renders the state a touch would change. A crew member can see BUS OFF and cannot do anything about it from the glass. Build = a hit test + a `ScreenPainter` branch, routed through the **same** `FlightCommands.Run`/`PanelPolicy` the plate uses so the two surfaces cannot disagree (T14's rule). **Nothing here flies the vehicle.** |
| **H33** | **The P&ID's plumbing is a static drawing with live tenants.** Every inline valve but the overboard one is a fixed-colour glyph; every atmosphere pipe is fixed; `CABIN FAN "RUNNING"` is a literal with a hardcoded `Severity.Nominal`; `PUMP A/B "RUNNING"` is a literal **not even `live`-guarded**; `CABIN HX A/B` are empty strings | **(A)** | A pump/fan is exactly what `VehicleSystems` should own: `FanOn`/`PumpAOn`/`PumpBOn` tripping with `Bus1On`/`Bus2On` would make the word *and* the pipe colour honest, off buses the crew can already switch. Supply valves likewise follow bus power and the leak path. ⚠ `CABIN HX` has genuinely no quantity — it should carry `—`, not `""`, so it reads as "no source" rather than "nothing here". |

### Procedures & checklists — the question the brief asked most directly

**Answer: no procedure page in the build is step-tracked.** There is no step index, no cursor and no
advance on any of the five procedure screens. The one real step state machine, `StepList`, is stranded
(§1.1). What the pages have is a 5→0 timer (Suit Leak only), the micro-sim hanging off it, twelve command
buttons of which 8 are no-ops, and otherwise baked copy.

| id | hole | class | note |
|---|---|---|---|
| **H19** | **Suit Leak Check: the sim is live, the *procedure* is not.** Both left ticks draw checked at page-open, before the crew touches anything; "SECTION 2: IN PROGRESS" never advances; steps 2.3/2.4/2.5 are literals | **(A)** | The verdict half is already exemplary (S31/S32) — this is the *step-flow* half. Model: a step index advanced by the existing INITIATE / timer / FINISH events the page already owns. Research: none — the transitions are already in `ScreenPainter`'s suit state. |
| **H20** | **`SuitLeakSim`'s provenance comment does not match the code.** Its header claims the ΔP is measured against cabin pressure *"driven by real TAC Life Support state"*; the actual line is `r.PressPsia = PressNominal + slower * 0.06` (`CabinEnvironment.cs:145`) — verified — where `slower = sin(MissionTime/113)`. TAC drives **only** ppO2 and CO2 | **(A)** — and a **documentation defect** | The rows do move off a real clock, so the "never a constant" test passes — but they do **not** respond to life support, to a cabin leak, or to `Systems.DepressResponse`. Two things are owed: **fix the comment** (it currently misleads the next reader about provenance), and **feed `s.Systems.LeakRate` into `CabinInputs`** so pressure falls. See H37 — this is the same root cause. |
| **H21** | **VrioTest is inert end to end.** No `PageState` parameter, no HitTest, no glue branch; the five checklist ticks read a literal `bool[]`; **there are no health LEDs on the page** — the "VRIO 1/2 LED" items are dim note text | **(A)** | The file admits it (`:12`): the touch pass never landed here. Under §14.4(f) the LED *test* is a readout of a modelled avionics health state. Model: a VRIO health micro-sim (two units, pass/fail, a test that takes time), verdict computed not hardcoded — the S31 pattern exactly. Research: `SCREEN_INVENTORY.md` #6 already has the real procedure structure; **what a VRIO health lamp actually reports is not public**, so the model is a marked reconstruction. |
| **H22** | **Manual Chute steps do not track descent altitude.** The gates (`10.6 km`, `5.5 km`, `1.6 km`…) are literals in a `Step[]`; the row tint is a function of a compile-time `Gate` flag, not of `s.Altitude`. Nothing compares the two | **(A)** | **The clearest (A) in the audit** and the clearest split from (B): tracking is a readout of `s.Altitude`, which the strip above already draws live. Model: compare `s.AltitudeM` to each gate, mark passed / current / pending. ⚠ **Do not "fix" the numbers to match**: `SCREEN_INVENTORY.md` records that the FSM constants (`5486`/`1830` in `MissionPhase.cs`) and the page's "(TBC)" altitudes are **intentionally two different things** — SpaceX's own placeholder text kept verbatim. Tracking must therefore be against the *page's* stated gates, and the discrepancy noted below is a **reporting** matter, not a licence to edit. ⚠ Noted for the record: the page prints `1.6 km` for mains against `MainAltitude = 1830 m`; `EntryPage` copies the same string. |
| **H23** | **8 of 12 chute action buttons are no-ops** (DEPLOY DROGUES ×2, DEPLOY MAINS ×2, FIRE PYRO ×3, plus "Monitor altitude" which names no command) | **(B)** | Flight actuation — §14.4(a), unchanged by §14.4(f), wired by §B12.5. **No Part-A build proposed.** Two observations for whoever wires it: the four ENABLE BACKUP PYROS rows **all light together** off one shared flag with no per-row latch, and `FlightCommands.BackupPyros` is only ever written `true` — `CancelAllSequences()` returns false and clears nothing. Both are Part B's to resolve. |
| **H29** | **Deorbit Burn Prep: the four SLEW rows are literal dashes**, printed identically with a full live vessel | **(B)** — *but see Q4* | §14.4(e)(3) names these explicitly as a legitimate dash ("a value only Part B's flight software will command, e.g. the deorbit SLEW rows"), the page header records the three rejected near-misses, and a test guards that no fixture value appears there. §14.4(f) narrows the dash to "a genuinely-absent state" — and with no conductor there *is* no commanded slew, which keeps (e)(3) intact. **This audit reads them as still (B)**, and flags the tension as **Q4 in §8** rather than deciding it. |
| **H30** | **Crew Interrupt Conditions are static text** — "30° sustained attitude error", "600°/min attitude rate", "Far-field pointing" are printed as copy; no threshold is compared, nothing turns amber | **(A)** for the *evaluation*; **(C)** for the *wording* | The criteria are real reference copy (S13 settled the attitude/altitude wording) and must not be edited. But *evaluating* them is a readout: attitude error and body rate are both live in `PageState` (`s.AlignText`, `BodyRateText`, `Body*Dps`). A criterion that lights amber when the live value exceeds the stated limit is (A) and needs no conductor. ⚠ "Sustained" needs a dwell timer — that is the model to design. |
| **H31** | **Entry page: nothing live at all**, structurally — `Build(dl,w,h)` takes no `PageState` | **(A)** | The page prints parachute-deployment altitudes; `s.Altitude`, `s.Steps.DroguesFired`/`MainsFired` and the phase are all live. Same model as H22. |

### Docking, rendezvous & nav

| id | hole | class | note |
|---|---|---|---|
| **H24** | **The 12 direction pads + Reset Positions do nothing** | **(B)** — **decided, not open** | **S28 is closed decided-(a)** (owner, via the overseer, 2026-09-02): the pads stay the §14.4(a) honest no-op and **Part B** (§B12.5 / §B10.6) wires them. `DockingSimPage.IsActuation` / `ScreenPainter.DockAction` already carry the seam correctly. **No Part-A build is proposed, and option (b) — a screens-only RCS exception — would need an owner `OVERRIDE` plus a §14.4 entry.** Recorded here only so the walk is complete. |
| **H25** | **The manual docking page has no camera.** `Build` fills the screen with `Background`; `WantsDockingCam` grants the live feed **only** to `UiPage.Hud`. The reference (and iss-sim) shows the docking-adapter view behind the rings | **(A)** | Pure display. `DockingCamRenderer` exists and is genuinely live; the stranded `DockingPage.cs:73` proves the pattern with a full-bleed `dl.Image(ImageId.DockingCamLive, …)`. Build = one image call plus a `WantsDockingCam` clause. |
| **H26** | **`Instructions` and `Reset Positions` are inert for two different reasons** | **(C)** / **(A)** | **S29 settled both**: `Instructions` has no content in this build → (C) until content exists; `Reset Positions` is conservatively classified as actuation because the reference does not say whether it resets the vehicle or the view. **If it resets the view it is (A) and costs three lines** — that disambiguation is the research, and it is the same open question S29 recorded. |
| **H27** | **The Hold-Capture card reads `NOT ENGAGED` for the entire mission** — its only variable is a stub (§1.2) | **(A)**, with a (B) ceiling | The card is honest but useless. **Follow the `AuthoritativePhase` pattern** (§1.2): derive an approach state from what is already live — `s.HasTarget && s.RangeM < X && s.Closing` — so the card reflects an actual approach without commanding anything. The *conductor-engaged* reading stays (B). ⚠ This is the audit's clearest example of a stub that could degrade gracefully and does not. |
| **H28** | **Rendezvous ◄/► have no hit rect at all**, and the four-slot icon rail is inert | **(A)** / **(C)** | The arrows are a procedure stepper — (A), and `StepList` exists. The icon rail is **(C)**: `RendezvousPage.cs:17-20` records that the icons are not label-legible in the source photo, so naming destinations would be a §1.4 invention. |
| **H35** | **NavOrbitPlot's four concentric range rings carry no scale** — `rmax·i/4`, no units printed | **(A)** | The file records why (no scale legible in the JSC source). But an unlabelled ring is a readout that says nothing. The live plot scale is already computed at `NavPage.cs:596`, and `s.RangeM` + `BarScale.Range` exist. Labelling the rings off our own computed scale is ours-and-marked, same footing as `RingFullScaleDeg`. |
| **H36** | **NavOrbitPlot has no touch controls**, and the **complete NAV pan/zoom/NEXT-VIEW cluster is orphaned** (§1.1) | **(A)** | `MapProjection.Zoom`/`Pan` are implemented, wired and unreachable. ⚠ **This is also S43's cheap fix** — S43 notes the ORBIT view ignores the existing zoom control, and that wiring it "is the cheap option and probably the right one". H36 and S43 are the same work. |
| **H37** | **Cabin pressure is a clock, and the pressure alarm can never fire.** `14.7 + sin(t/113)·0.06` — range ±0.06 psi against `PressCaution = 13.0`. Meanwhile `SystemsState.LeakRate` is live and unconnected, so the P&ID can print `CABIN LEAK: DETECTED` beside a rock-steady `14.70 psia` | **(A)** — high value | Drawn as a headline gauge in **four** places and it drives `Alarms.LifeSupport`, which colours the Crew tab and the P&ID cabin outline. Model: feed `s.Systems.LeakRate` into `CabinInputs` and let pressure fall; `Systems.DepressResponse`/`Isolating` already model the recovery. **Makes the leak, the isolation valve, the alert word, the suit ΔP (H20) and the gauge one story.** Same root cause as H20. |
| **H38** | **Duplicated signals presented as independent instruments** — `PowerUnit1Text == PowerUnit2Text` (one string written to both), NET PWR1/PWR2 (one real flow split 0.55/0.45 by a hardcoded constant), `Downlink = Uplink` literally, Charge Rate = Net Power, TPS Max = the SHIELD gauge | **(A)** | Each is documented in-code as deliberate and honest given one KSP source. But the screen still asserts two instruments where one signal exists. `SystemsState` **already models two independent buses**, so splitting the power pair is local work. ⚠ The others may be correct to leave — this is a §14.4(f) judgement about whether a duplicated readout is "filled". |
| **H39** | **`Orbital.cs`, `Hohmann.cs` and `LifeSupport.Margins` are orphaned** — full vis-viva / anomaly / Hohmann / phase-lead maths and real TAC-LS margin computation, with **zero callers from any screen** | **(A)** | *Displaying* a rendezvous plan (Δv to phase, transfer time, wait time) or a days-remaining margin requires **no flight control whatsoever** — the numbers are already computable from `s.TargetRadiusM` / `s.TargetPhaseRad` / `s.BodyRadiusM`. This is the natural live replacement for H27's dead card and the natural filling for H18's MARGIN column. |

### Cross-cutting

| id | hole | class | note |
|---|---|---|---|
| **H40** | **The bottom status bar's live text is baked on every page** (§1.3) — CURRENT STATE, POINTING MODE, the SPX/GND/TDRS/ISS block, a counter | **(A)** | `TELEMETRY_REGISTRY` already registers `CURRENT_STATE` (→ `s.Phase`, live) and `POINTING_MODE` with sources; MET is trivial. **The precedent is in `FigmaUI.cs:276-277`** — the tab marker was erased from this very PNG so it could be drawn live. ⚠ The comm block is marked SIMULATION in the registry absent a comms mod; under §14.4(f) that is now a *fill*, not an omission — but `s.SBandText`/`CommSignal01` from stock CommNet (S24) are a real partial source. |
| **H41** | **STRING 1A/1B/1C console lamps can never light.** Their sim state changes correctly, but `PanelPolicy.IsLiveMode` routes them to `ModeIsOn`, which reads `AutoPilot.Engaged` / `StationApproach.Engaged` / `DockingOps.Engaged` — three hard-`false` stubs — and `PanelButton.Update` re-darkens them every tick. Their siblings 2A/2B/2C are not live-mode and flash correctly | **(A)** — **a real defect** | A lamp that lies by omission, on a control whose underlying state is genuinely modelled. The fix is local: read `Systems.Get(State, 1, i)` as the 2A/2B/2C path effectively does. **Not a Part-B item** — the string model is Part A's own micro-sim. |
| **H42** | **`DepressResponse` discards its return value** — `Systems.DepressResponse(ref State); return true;` reports "acted" and flashes the lamp even when the model refused because there is no leak. Its two plate-siblings return the bool correctly | **(A)** — **a real defect** | One-line inconsistency, but it is exactly the "click, no light, no action" honesty §14.4(a) exists to enforce, inverted. |
| **H43** | **Three of the five `[Tunable]` knobs feed a camera nothing turns on.** S44 wired `Tuning` correctly; `DockingCamRenderer.PortStandoffM`/`PortFovDeg` are genuinely live, but `ScaledPlanetRenderer.FovDeg`/`AzimuthTrimDeg`/`PitchTrimDeg` reach a renderer whose only `Request` call site is in the dead branch (§1.1) | **(C)** — record | Not a defect: **S10b/S37/S42 already own the scaled-space camera** and are HELD on an owner gate. Recorded so a future tuning session does not waste a glass visit on three knobs that cannot move anything. |
| **H44** | **`RangeExtender.cs` is 76 lines with no caller**, left by the 2026-09-01 autopilot deletion; `defaultPage` in `DragonScreen.cfg` is documented, parsed, warned about, then discarded by `FigmaMode ? 0 : defaultPageIndex` | **(C)** — hygiene | Neither affects the glass. Logged so they are not mistaken for live wiring. |
| **H45** | **A dashed value is drawn in the same weight as a live one** on the vehicle gauges and detail rows (`White`), while the CONSUMABLES table correctly dims its dash | **(A)** — cosmetic but on-theme | The same "can't tell dead from live" failure S22 was opened for, in a third form. ⚠ Also latent: the codebase has **two dash glyphs** — `—` in the vehicle family, ASCII `-` in the unused `Gauge`/`StatusIndicator` widgets. |

---

## 4. CROSS-REFERENCE TO THE OPEN STRAYS

**What this audit absorbs** (already logged; do not re-log):

| stray | status | how this audit relates |
|---|---|---|
| **S22** — static status words read confidently on a dead feed | DONE 2026-09-02 | **Absorbed and EXTENDED.** S22 applied one rule to `VehicleOverviewPage` + `VehicleMechPage`. **H14 is the same failure on the six subsystem tabs, which S22 did not touch** — verified: the Overview gates on `!valid`, the subsystem page does not. H14 also raises the tier S22 could not: under §14.4(f) these words should be *computed*, not merely dimmed (H15). H45 is a third form of the same idea. |
| **S28** — should the docking clusters fly the capsule? | decided-(a) | **Absorbed.** H24 records the decision and proposes no Part-A build. |
| **S29** — four display-only controls outside §6's list | DONE | **Absorbed.** H26 carries `Instructions` (C) and `Reset Positions` (A-if-view), including S29's own unresolved disambiguation. The two SuitCheck "ENTER READ-ONLY" plates stay (C). |
| **S31 / S32** — suit sim + TROUBLESHOOT | DONE | **Absorbed as the exemplar.** H19 adds the *step-flow* half S31/S32 did not cover; **H20 is new** — the sim's provenance comment does not match `CabinEnvironment.cs:145`. |
| **S26** — docking diamond fixed / axis drawn twice | DONE | **Confirmed live.** The audit verifies the diamond now places off real bearings, tints green when corrected, and hides with no target. |
| **S15** — the circular nav plot | DONE | Built; H35/H36 are its remaining scale and touch gaps. |
| **S44** — `Tuning` was never invoked | wired | **H43 refines it**: three of five knobs feed a camera nothing turns on. |
| **S43** — ORBIT plot is a hairline at RSS scale | TODO | **H36 is the same work.** S43 already identifies wiring the existing zoom control as the right fix; H36 supplies the reason it is unreachable. **Do them together.** |
| **S35** — gauge identity colours read as alarms | TODO, owner call | **Intersects H14/H15.** S35 is about *arc colour vs severity*; H15 is about *words vs live state*. If the owner picks S35 option (b) or (d) (severity drives colour), H15's word fixes should land in the same pass so colour and word cannot disagree. |
| **S39** — stacked label→value rows | TODO | Legibility, not liveness. Noted only because H1/H10 **add** live rows to two of the pages S39 lists; the new rows should adopt S38's remedy rather than inherit the defect. |
| **S3, S9, S10b, S18, S37, S42, S47** | HELD / blocked | Untouched. H43 notes S10b/S37/S42 own the scaled-space camera. Several (A) items below want glass eventually, which is **S18's** business, not this audit's. |

**What is genuinely new here** and is not covered by any existing stray — proposed as new register lines,
detail in this document:

*(This audit is register line **S49**; the findings below are logged as S50–S57.)*

- **S50** — the Cover top strip and the whole Frame 58 HUD are baked art while `PageState` carries every value live (H1, H2, H10). *TIER 2 / the biggest immersion win.*
- **S51** — the six subsystem tabs never got S22's guard (H14) + the eight self-contradicting status words (H15). *TIER 2: real defect.*
- **S52** — `SuitLeakSim`'s provenance comment contradicts `CabinEnvironment.cs:145`, and cabin pressure ignores the live `LeakRate` (H20, H37). *TIER 2: real defect + doc defect.*
- **S53** — STRING 1A/1B/1C lamps can never light; `DepressResponse` discards its refusal (H41, H42). *TIER 2: real defect.*
- **S54** — phantom Act*/Entry hit rects fire over the Reference Content text; must be fixed **before** H5 (H8). *TIER 2: latent defect.*
- **S55** — no procedure page is step-tracked; `StepList` is stranded by `FigmaMode` (H19, H21, H22, H31, H34). *TIER 3: scheduled build.*
- **S56** — the systems tree / P&ID are read-only while their toggle model exists and works (H32, H33). *TIER 3: scheduled build.*
- **S57** — orphaned live code with no caller: `Orbital`, `Hohmann`, `LifeSupport.Margins`, the NAV control cluster, `RangeExtender` (H36, H39, H44). *TIER 4: hygiene + harvest.*

---

## 5. WHAT IS CORRECTLY STATIC — class (C), so it is not re-audited

- **Real procedure copy.** The chute steps and actions, the suit checklist wording, the VRIO engineering
  notes, the Crew Interrupt Conditions *text*, the Reference Content cards, the deorbit settle-burn notes.
  §6 scopes the live-data work to **values**; this is reference copy and §1.4/C1.4 protect it.
- **Labels, legends and tab names.** The 8-tab `VehicleTabBar` strip (confirmed-real from the clean
  mockup), the systems-tree legend, `PanelMap`'s button legends.
- **Line art that is a diagram, not an instrument.** The F9 stack outline, the Dragon hull profile, the
  P&ID's topology, the systems tree's box-and-connector skeleton. Their *state colouring* is a readout
  (H33); their geometry is a drawing.
- **Deliberately inert controls pending a source (§14.4(b)).** SWAP 1/2/3, ENTRY REBOOT, BACKUP ENTRY,
  NORMAL ENTRY; the two SuitCheck "ENTER READ-ONLY" plates (S29); the Rendezvous icon rail; docking
  `Instructions`.
- **Honest dashes for genuinely-absent state.** No target → no docking error; off the return leg → no
  splashdown time; docked → KER's stack figure is unknowable. §14.4(f) explicitly preserves these.
- **Audio faders.** Stock KSP has no cabin audio; `SettingsPage.cs:27-29` records the decision.
- **Preview/test-only surfaces.** `PanelBoardPage`, `ProofPage`.

---

## 6. RECOMMENDED ORDER FOR THE (A) HOLES

Biggest immersion win first, as the suit sim was. **Cheap-and-visible before deep-and-modelled** — the top
four need no new model at all, only drawing and reading.

| # | work | holes | why here |
|---|---|---|---|
| **1** | **Draw the Cover top strip live** | H1, H2 | Seven values already live and pre-formatted, on the page the crew opens on. Highest value-per-line in the build. Add H2's clock in the same pass. |
| **2** | **Draw the Frame 58 HUD readouts live** | H10 | The largest single liveness gap. Same method as #1, same zero new data. Design the no-target/no-feed looks while there. |
| **3** | **Make the status words honest** | H15, then H14 | H15 first — eight words contradicted by live state **on the same screen**, no new model. Then H14's `!Valid` guard, mechanically. ⚠ Coordinate with **S35** if the owner has ruled on gauge colour. |
| **4** | **Fix the lamps and rects that lie** | H8, H41, H42, H45, H18 | Small, cheap, and each is the exact "can't tell dead from live" failure class the build already cares about. **H8 must precede H5.** |
| **5** | **One cabin-pressure story** | H37, H20 | Feed `LeakRate` into cabin pressure; correct the provenance comment. Unlocks the pressure alarm and makes leak / valve / suit ΔP / gauge agree. Real modelling, but small and high-payoff. |
| **6** | **Live procedure step-tracking** | H22, H31, H19, H34, H21 | The brief's headline question. Start with **Manual Chute** (H22) — the cleanest (A), tracking an altitude the same page already draws. Then Entry, Suit Leak's step flow, Ascent (harvest `StepList`), VrioTest (needs a new marked micro-sim, so last). |
| **7** | **Make the Cover's rail and alarms mean something** | H7, H5, H4, H3 | Alarm surface first (H7 — the home page cannot show a caution). Then the Act* buttons (H5, after H8). H4's slots 0/1/2 next; **slots 3/4 stay owner-gated per S27**. |
| **8** | **Reach the systems the crew can already see** | H32, H33 | Touch on the tree/P&ID, then pumps/fans/valves in `VehicleSystems`. Route through the existing dispatcher so plate and glass cannot disagree. |
| **9** | **Harvest the stranded and the orphaned** | H36+S43, H39, H12, H25, H27, H28, H35 | NAV controls (also fixes S43), the orphaned maths as real readouts, settings handlers re-homed, the docking camera, the Hold-Capture card following `AuthoritativePhase`. |
| **10** | **The dashed-quantity sweep** | H17, H38, H13, H16, H40, H11 | The largest surface and the one most in need of a policy call first (**Q3**). Do it last, in themed batches, not as one task. |

**Sequencing rules that fall out of the above:** H8 **before** H5 · H36 **with** S43 · H15 **with** S35's
outcome · H20 **with** H37 · every one of these is preview-gated Part-A work, and none needs `install` or
glass to *build* — though several will want S18's eyes eventually.

---

## 7. WHAT THIS AUDIT DID NOT DO

- **No code was read into a fix.** Every hole is a description, not a patch.
- **No plan or decision was changed.** §14.4(f) is quoted as it stands; S27/S28/S29's settled answers are
  recorded, not reopened.
- **No glass.** Every finding is from source. Whether a live HUD readout is *legible* at the console is
  S18/S39's question, not this document's.
- **Not exhaustively re-verified.** §1 was; the per-element tables are the walks' findings, spot-checked.
- **Line numbers will drift.** Two commits landed during this audit (`40fe9c1` S48, `a913270` G3).
  Re-read before editing.

---

## 8. OWNER QUESTIONS (C1.9 / C1.13) — paste-ready

> **DragonScreen — SCREEN_LIVENESS_AUDIT.** A read-only audit walked all 35 `UiPage` pages, the 7 Cover
> phases and the 38-button console, classified every element LIVE / STATIC / NO-OP / MICRO-SIM, and sorted
> every hole into (A) Part-A-achievable, (B) Part-B-gated, (C) correctly static. `docs/SCREEN_LIVENESS_AUDIT.md`
> is committed; no code, plan or gate was touched. Four decisions are needed before the (A) work starts.
>
> **Q1 — The stranded UI.** `ScreenPainter.cs:56` pins `FigmaMode = true`, which makes an entire second UI
> unreachable: a 15-row **live** ascent state machine (`StepList`), the crew-gate card, a 478-line settings
> page with eight **working** handlers (lights, brightness, seat-view, capture), and a complete, wired
> NAV pan/zoom/NEXT-VIEW cluster with no `UiPage` to host it. All compiled, all tested, none reachable.
> The audit does **not** propose flipping the flag — the Figma rebuild is the current design. The question
> is what to do with the assets: **(a)** harvest them into Figma pages one at a time as the (A) work
> reaches each (recommended — it is how `StepList` fills the Ascent page and how the NAV controls fix S43);
> **(b)** harvest only `StepList` + the NAV cluster and formally retire the rest as dead code;
> **(c)** leave everything stranded and rebuild fresh where needed. Which?
>
> **Q2 — Cover `ENTRY ENABLED` (H6).** The row draws `True` **and** `False` simultaneously, neither lit,
> and both hit rects resolve to nothing. Its class depends on what the row means, which the build cannot
> read off the reference: **(a)** *"the crew verified entry is enabled"* → a local latch, Part-A-achievable
> now (the baked step text beside it reads this way); **(b)** *"the vehicle has armed entry"* → an arming
> flag on the §14.4(a) actuation side, Part-B. Which reading?
>
> **Q3 — The scope of §14.4(f)'s sweep (H17, H38).** (f) says a dash now survives only for a
> *genuinely-absent* state, which reclassifies ~27 currently-honest dashes across the subsystem tabs —
> humidity, chamber pressure, helium, bus voltage, FC load, bus traffic, link margin, storage, loop flow,
> heat reject, radiator outlet — as things to fill with coherent marked micro-sims. That is a large new
> modelling surface (a modelled flight computer, a modelled pressurisation system, a modelled coolant
> flow), and it is the single biggest expansion in the audit. Options: **(a)** fill all of them, in themed
> batches, as its own register epic; **(b)** fill only those with a plausible physical driver already in
> the build (helium off Draco duty, bus voltage off SOC, loop flow off loop temperature, humidity off crew
> + power) and leave the avionics/computer group dashed as genuinely-absent; **(c)** defer the whole sweep
> until the cheap (A) items (#1–#8 in the audit's order) are done. The audit recommends **(b)** then
> **(c)**, but the scope of (f) is the owner's to set.
>
> **Q4 — Do the Deorbit SLEW rows stay dashed (H29)?** §14.4(e)(3) names them explicitly as a legitimate
> dash ("a value only Part B's flight software will command, e.g. the deorbit SLEW rows"), and a test
> currently guards that no value appears there. §14.4(f) narrows a dash to "a genuinely-absent state" — and
> with no conductor there genuinely is no commanded slew, so the audit read them as **still (B)** and
> changed nothing. Confirm that reading, or say whether (f) is meant to reach them too. **Same question
> applies to the other stub-pinned readouts** (`GNC IDLE`, `ALERT ACTIVITY — none`, the Hold-Capture card's
> `NOT ENGAGED`): the audit treats them as Part-B sockets, except H27, where it recommends the card follow
> the `AuthoritativePhase` pattern and derive an approach state from live target range/closing instead.
