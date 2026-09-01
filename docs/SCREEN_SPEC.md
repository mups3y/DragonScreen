# SCREEN SPEC — the single screen specification

> **RECONCILED 2026-09-02 (T1). `docs/BUILD_PLAN.md` is the authoritative spec (C7.1); this file is
> subordinate to it and cannot override it.** The old governing doc named here, `MASTER_BUILD_SPEC.md`, was
> deleted in the 2026-09-01 screens-only pivot — read `BUILD_PLAN.md` in its place, and read the numbered
> "rules" cited below (E5, S4, C6, T1…) as this file's own house rules, not as clauses of a live document.
> **What the plan overrides here:** the command paths in §4/§6 route through a `DockingControl` /
> `NavState3` / `FlightDriver.Set*` actuation layer that was deleted (`AuthorityManager` survives only as the
> GNC lamp's display label in `pure/ScreenModes.cs`) — flight commands are an honest no-op today
> (§14.4(a): click, no light, no action, **no red**), and Part B re-introduces the real paths one at a time
> (§B12.5). The renderer contract (§2), visual language (§3), component library (§4) and the per-page
> completion gate (§7) are CURRENT and load-bearing. Page-set truth lives in `SCREEN_INVENTORY.md` + §3 of
> the plan; "Phase N" numbers below are from the old schedule — the live order is `REGISTER.md`.

> Detailed screen requirements. This is the ONLY screen spec — no `DRAGONSCREEN_UI_SPEC.md` / `UI_MASTER_SPEC.md`. Build pages from reference *source* (`UI_AUDIT.md`, `REAL_DRAGON_SCREENS.md`), never from screenshots (rule E5). Every feature is classified in `SCREEN_EVIDENCE_MATRIX.md`; every value sourced in `TELEMETRY_REGISTRY.md`; every control pathed in `COMMAND_REGISTRY.md`.

## 1. The screen is a window into the spacecraft
Presents authoritative state; never invents it (rule T1/T2). Consumes the immutable display snapshot fed by physics → vehicle → nav/mission/FDIR → AuthorityManager. Two fidelities, both required on every page: **visual/interaction** and **operational** (`control → command → controller → physics → movement → nav → telemetry`).

## 2. Framework as-built (PROTECT — do not rewrite the renderer)
- **Render:** Unity `RenderTexture` + GL immediate-mode (not IMGUI/uGUI). Pure page code emits a `pure/DisplayList.cs` command buffer; walked in-game by `ScreenPainter.cs` (`OnPostRender`, 3 passes solid/text/image) and offline by `preview/PreviewMain.cs` (System.Drawing → PNG). Shared geometry `pure/ArcGeometry.cs`. Host: `DragonScreenMonitor.cs` per IVA prop.
- **Three displays, "one screen four surfaces":** `screenIndex` is identity not role; any page → any display; persisted in `DragonScreenState.cs` (`pure/PageSelection.cs`). All three read the SAME authoritative snapshot (never different truth). **Real default (CONFIRMED from cockpit photos, `SCREEN_EVIDENCE_MATRIX.md`): LEFT + RIGHT = the mirrored proximity/vehicle display, CENTRE = the 3D Earth globe/map.** Reconsider our cfg default (currently LEFT=VEHICLE/CENTRE=FLIGHT/RIGHT=NAV) toward this.
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

### Docking (the gold standard — Phase 7). Layout from a direct study of iss-sim (2026-08-31); see `SCREEN_EVIDENCE_MATRIX.md`.
- Purpose: manual + automatic proximity/docking operations; the design system for all other pages.
- **Layout (reference-confirmed):**
  - **LEFT cluster — TRANSLATION:** Up / Down / Left / Right / Forward / Backward, with a **precision toggle centred in the cluster** (LARGE ↔ SMALL; default small/precise).
  - **RIGHT cluster — ROTATION:** Roll / Pitch / Yaw, with its own **centred precision toggle**.
  - **CENTRE:** HUD rings + a **green-diamond target** overlaid on the docking adapter; must be centred. Rotation readouts ROLL/PITCH/YAW near it.
  - **RANGE** readout (distance) upper area; **RATE** (closing rate) at the **bottom**.
  - Top controls: Instructions · Reset · Settings.
- **Two numbers per axis (critical, from the reference):** a **GREEN correction** (drive to 0) *and* a **BLUE rate** (current speed). Success = all green < 0.2 and RATE > −0.2 m/s (keep < −0.2 below 5 m). Label these as **simulator training guidance**, not certified limits (plan §12). Our current page conflates correction and rate — the rebuild must show both.
- **AUTO/MANUAL aware** (our addition, C6 — real Dragon docks autonomously). Design from studying BOTH SpaceX layouts (see `SCREEN_EVIDENCE_MATRIX.md`): **AUTO shows the real monitoring HUD** (target in rings + rotation corrections + RANGE + RANGE RATE, the clean look from the training video) while the FSM flies; **MANUAL reveals the LEFT/RIGHT control clusters** over it, and the crew's translation/rotation buttons issue **real** RCS/attitude commands via the AuthorityManager path (see `COMMAND_REGISTRY.md`). This unification is our SIMULATION/RECONSTRUCTION of two confirmed layouts — not a verbatim SpaceX screen.
- Telemetry: RANGE / RATE / offsets / ROLL-PITCH-YAW error / alignment / target-waypoint / capture from the filtered `NavState3` (see `TELEMETRY_REGISTRY.md`) — never recomputed on-screen (rule T5).
- Available phases: RENDEZVOUS / APPROACH / PROXIMITY / DOCKED. Required systems: target vessel, rel-nav, GNC, RCS.
- Failure modes: NO DATA (no target/nav), INVALID (bad filter), corridor breach → abort, RCS depletion. Completion per §7.

## 7. "Screen complete" gate (per page)
IA ✓ · evidence class + confidence recorded ✓ · source-of-truth contract ✓ · visual review vs reference ✓ · typography ✓ · components ✓ · glove touch targets ✓ · real telemetry ✓ · real commands ✓ · phase-aware ✓ · alert-integrated ✓ · failure/disabled states ✓ · IVA-tested ✓ · performance ✓ · **flight-tested where applicable ✓** · docs updated ✓.
