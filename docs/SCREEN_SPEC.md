# SCREEN SPEC (ACTIVE — the single screen specification)

> Detailed screen requirements. Subordinate to `MASTER_BUILD_SPEC.md` (**cannot override it**). This is the ONLY screen spec — no `DRAGONSCREEN_UI_SPEC.md` / `UI_MASTER_SPEC.md`. Build pages from reference *source* (`UI_AUDIT.md`, `REAL_DRAGON_SCREENS.md`), never from screenshots (rule E5). Every feature is classified in `SCREEN_EVIDENCE_MATRIX.md`; every value sourced in `TELEMETRY_REGISTRY.md`; every control pathed in `COMMAND_REGISTRY.md`.

## 1. The screen is a window into the spacecraft
Presents authoritative state; never invents it (rule T1/T2). Consumes the immutable display snapshot fed by physics → vehicle → nav/mission/FDIR → AuthorityManager. Two fidelities, both required on every page: **visual/interaction** and **operational** (`control → command → controller → physics → movement → nav → telemetry`).

## 2. Framework as-built (PROTECT — do not rewrite the renderer)
- **Render:** Unity `RenderTexture` + GL immediate-mode (not IMGUI/uGUI). Pure page code emits a `pure/DisplayList.cs` command buffer; walked in-game by `ScreenPainter.cs` (`OnPostRender`, 3 passes solid/text/image) and offline by `preview/PreviewMain.cs` (System.Drawing → PNG). Shared geometry `pure/ArcGeometry.cs`. Host: `DragonScreenMonitor.cs` per IVA prop.
- **Three displays, "one screen four surfaces":** `screenIndex` is identity not role; any page → any display; persisted in `DragonScreenState.cs` (`pure/PageSelection.cs`). All three read the SAME authoritative snapshot (never different truth).
- **Touch/input:** `ScreenTouch.cs` (collider raycast → page pixels) → `Pages.HitTest` → `PageHit{PageAct,Arg}` → `ScreenPainter.Apply`. **One rect function per control** (draw + hit share the same `*Rect`) — keep this invariant. Physical console: `PanelButtons.cs`/`pure/PanelMap.cs`.
- **Sub-surface RTs:** `NavBallRenderer.cs`, `DockingCamRenderer.cs`, `ImageStore.cs`.

## 3. Visual language & tokens
- Colours: `pure/DragonPalette.cs` (measured) — bg `#020738`, panel `#111B52`, accent/cyan `#20FBFD`, Go/Caution/Alarm, text ramp `Text0..8`. State semantics: green=go, amber=caution, orange=warning, red=critical, muted=disabled.
- Type: `pure/Typography.cs` — Dense 12 / Caption 16 (measured) / Body 20 / Value 28 / Hero 40. Font D-DIN (**fix silent Arial fallback**, Phase 18 — bitmap-font-from-texture, no OS install; `PreviewMain.FontFamily` must match cfg).
- Avoid generic sci-fi HUD styling, gratuitous glow/neon, decorative animation (spec §8 of directive). Restrained, modern, information-dense but uncluttered (rule S7).

## 4. Component library contract (build only what the Docking page needs first; reuse thereafter)
Existing (PROTECT/extend): `Gauge` (Ring/Labelled/Bar), `Control.Button`, `Card`, `GateCard`, `ChromeBar`, `Readouts`, `StepList`.
New for Docking (Phase 6), each with NORMAL/PRESSED/ACTIVE/DISABLED/**FAULT**/CONFIRMATION states + **glove-friendly hitbox ≥ visual bounds** (rule; iss-sim glove requirement):
- `NumericReadout` — value+unit, NaN→`—`, `NO DATA`/`INVALID` states.
- `StatusIndicator` — NORMAL/CAUTION/WARNING/CRITICAL/AUTO/MANUAL/ABORT.
- `TargetReticle` + `Crosshair` + alignment/closing-rate error tape.
- Button command-rejected reason surface (rule C5).

## 5. Mission-phase-adaptive UI (rule S6)
Controls irrelevant/unsafe in the current phase are hidden / disabled / inhibited, never permanently live. Automation state always visible: AUTO / MANUAL / ABORT / INHIBITED / UNAVAILABLE. Each page declares its available phases (below).

## 6. Page inventory (complete before coding pages, rule S4)
Currently implemented: FLIGHT, VEHICLE, NAV, DOCKING, SETTINGS (`ChromeBar.PageNames`). Full intended set below; each page must, before it is coded, declare: **evidence class + confidence · purpose · inputs · outputs · controls · available phases · required systems · telemetry sources · manual/auto interaction · failure modes · completion status.** Names are evidence-based (`SCREEN_EVIDENCE_MATRIX.md`), not assumed.

Navigation · **Docking (reference)** · Mission · Vehicle/Overview · Systems · Alerts(FDIR) · Cameras · Propulsion · RCS · Power · Thermal · Communications · Environment/ECLSS · Crew · GNC/Attitude · Orbital · Guidance · Maneuvers · Rendezvous · Approach · Deorbit · Entry · Landing/Recovery · Procedures/Checklists · Timeline · Automation · Manual · Abort · Recovery · Settings · Training/Simulation.

### Docking (the gold standard — Phase 7)
- Purpose: manual + automatic proximity/docking operations; the design system for all other pages.
- Layout: left translation cluster · right rotation cluster · centre target reticle + alignment · precision toggle · central green correction values · closing RATE.
- AUTO/MANUAL aware; on MANUAL, controls issue **real** RCS/attitude commands via AuthorityManager (see `COMMAND_REGISTRY.md`). Shows AUTO while the FSM flies it.
- Telemetry: DOCK_RANGE/RATE/OFF_*/ALIGN/*_ERR/TARGET_WP/CAPTURE from filtered `NavState3` (see `TELEMETRY_REGISTRY.md`) — never recomputed on-screen.
- Available phases: RENDEZVOUS/APPROACH/PROXIMITY/DOCKED. Required systems: target vessel, rel-nav, GNC, RCS.
- Failure modes: NO DATA (no target/nav), INVALID (bad filter), corridor breach → abort, RCS depletion. Completion per §8.

## 7. "Screen complete" gate (per page)
IA ✓ · evidence class + confidence recorded ✓ · source-of-truth contract ✓ · visual review vs reference ✓ · typography ✓ · components ✓ · glove touch targets ✓ · real telemetry ✓ · real commands ✓ · phase-aware ✓ · alert-integrated ✓ · failure/disabled states ✓ · IVA-tested ✓ · performance ✓ · **flight-tested where applicable ✓** · docs updated ✓.
