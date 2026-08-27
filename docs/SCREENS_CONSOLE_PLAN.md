# DragonScreen screens + console — the "every feature and button working" workstream

> **Why (2026-08-28, user):** tie EVERYTHING into the DragonScreens + the physical button console — every
> single screen feature and every console button **assigned and working**, as high-fidelity as we can be.
> Any button without a documented use → make an educated guess at its function and wire it. Find any and all
> errors in the screens and fill any missing features. ⛔ **KEEP the abort feature + display exactly as-is —
> the user loves it (it's fun).** This is the SCREENS-side workstream; it runs alongside the autopilot
> flight-tuning (per the phase-order rule it's its own phase, and it doesn't gate pad→orbit). NOT being fixed
> yet — this is the plan.

---

## 1. Known bugs to fix (diagnosed 2026-08-28, `pure/NavPage.cs` + `pure/GlobeProjection.cs`)
| Bug | Where | Diagnosis (starting point) | Fix direction |
|---|---|---|---|
| **NAV 3D globe is MIRRORED** (flat map is correct) | `NavPage.Globe()` | The flat-map `Quad()` un-mirrors the texture by swapping U (`q.UMax, q.UMin`) — the KSP scaled-space `_ColorMap` is wound east-west opposite our lon→U. `Globe()` samples `uMin, uMax` in NORMAL order, so it keeps the mirror the flat map already fixed. | Swap U in `Globe()`'s `ImageUV` calls (non-split AND split sub-quads), exactly as `Quad()` does. The overlay (`GlobeProjection`, east→right) is already correct, so un-mirroring the texture makes them line up. |
| **Orbit lines don't close/connect** (globe AND flat map) | `NavPage.ProjPolyline()` + `NavPage.Map()` track loop | Both drawers loop `i=1..n-1` drawing segment `(i-1→i)` — neither draws the **closing segment `(n-1 → 0)`**. A closed orbit loop (the globe overlay is a full ellipse; `OrbitCount = TrackCount` sampled around 360°) therefore has a visible gap where it should meet. | Confirm the track/orbit arrays are meant to close (VesselData samples a full orbit), then draw the wrap segment `(n-1 → 0)` on the globe orbit + target orbit; on the flat map decide per data (a single-orbit GROUND track is an open sinusoid and legitimately doesn't close — but the ORBIT-view/globe orbit does). Keep the dateline-seam break + occlusion break. |

## 2. Console button audit — every plate, every button (from `pure/PanelMap.cs`, transcribed from the real model)
The ~39 buttons are already MAPPED to `PanelCommand`s; the task is to confirm each is **wired to a real function**
(carried out by `FlightCommands`/the executor, red dash on refusal) and to **educated-guess any that are inert**.

| Plate | Button | Command | Wired to (verify) / educated-guess if inert |
|---|---|---|---|
| **Emergency L+R** (shared interlock: ARM→EXECUTE, CANCEL clears) | CANCEL / WATER DEORBIT / DEORBIT NOW / BREAKOUT / EXECUTE | Cancel/WaterDeorbit/DeorbitNow/Breakout/Execute | WATER DEORBIT → `AbortControl` DeorbitReturn to nearest ocean; DEORBIT NOW → immediate deorbit burn; BREAKOUT → KOS-retreat / emergency-undock. Verify each fires the real `AbortControl`/`AbortResponder` path. |
| Emergency L+R (cabin) | DEPRESS RESPONSE / SURPRESS FIRE / FIRE RESPONSE | DepressResponse/SuppressFire/FireResponse | ⭐ tied to the ABORT DISPLAY (the loved feature): DEPRESS RESPONSE already silences the klaxon + red cabin lights (memory). Verify FIRE RESPONSE + SURPRESS FIRE do a sensible cabin-emergency action (VehicleSystems), red dash if N/A. **Do not change the abort FX.** |
| **Power** | POWER 1/2 · STRING 1A/1B/1C · STRING 2A/2B/2C · RESET 1/2 | Power1/2, String1A..2C, Reset1/2 | Educated guess: dual power buses (1/2) each feeding 3 avionics "strings" (A/B/C) — a redundancy/fault-tolerance panel. Wire to a VehicleSystems power/string model: POWER toggles the bus, STRING selects/isolates a flight-computer string, RESET clears a faulted string. Reflect state in the dash light (lit=on, red=faulted). |
| **Chutes/pyros** (immediate) | ENABLE BACKUP PYROS · JETTISON NOSE CONE · MAINS ONLY · DROGUES & MAINS · ENABLE ENTRY REBOOT · CUT MAINS · FIRE PYRD | as named | Wire to `Actuator`: JETTISON NOSE CONE→open/blow nose shroud; MAINS ONLY / DROGUES & MAINS→chute mode; CUT MAINS→`Actuator.CutChutes`; FIRE PYRD→fire the pyro/decoupler; ENABLE BACKUP PYROS / ENABLE ENTRY REBOOT→arm the backup pyro bus / reboot the entry computer (VehicleSystems flag). |
| **Entry mode** (immediate) | ENABLE BACKUP ENTRY · SWAP 1/2/3 · ENABLE NORMAL ENTRY | as named | Educated guess: primary vs backup entry-guidance computer, with SWAP 1/2/3 swapping the three avionics strings for entry. Wire to a VehicleSystems entry-mode flag the ReturnControl/EntrySteering reads (normal vs backup entry law). |
| **Abort handle** | pull + twist | Abort | Verify → `FlightDriver.RequestAbort` → regime-aware `AbortControl`. **The loved path — keep it.** |
Rule (from PanelMap): a control with genuinely nothing behind it must return `false` → **red dash with a reason**, never be silently inert. So "make every button work" = give each a real action OR an honest refusal.

## 3. Screen-feature audit — every page, every control
Audit each page against `docs/REFERENCE_PAGES.md`, `docs/REAL_DRAGON_SCREENS.md`, `docs/UI_AUDIT.md`, and the
live-demo findings; confirm every control does something real; fill missing features; keep high fidelity.
- **NAV** (`NavPage`, 3 modes: GROUND TRACK / ORBIT / 3D PLANET) — fix §1 bugs; verify pan/zoom/centre/NEXT-VIEW
  on all modes; the flat map + globe overlays; target orbit + AN/DN nodes (deferred V2 per memory — add).
- **MECH** (`MechPage`) — the mechanical/systems page; verify every gauge + readout is live.
- **DOCKING** (`DockingPage` + `DockingCamRenderer`) — the docking camera + approach cues; verify the cam,
  the range/rate/lateral cues, the WP gates, manual-takeover (from the GNC research).
- **NAVBALL** (`NavBallRenderer`) — verify right-side-up (memory: was fixed) + markers.
- **ABORT overlay** (`AbortOverlay`) — ⛔ KEEP; verify it still triggers + the DON'T-PANIC art + klaxon + red IVA.
- **SETTINGS** (`SettingsPage`) — verify every toggle maps to a real setting.
- **PROOF** (`ProofPage`) — verify it shows what it claims.
- **Crew-gate / procedure** (`CrewProcedureOps` + `GateCard`/`StepList`) — verify every gate GO/HOLD/ABORT works.
- **Touch + panel input** (`ScreenTouch`, `PanelButtons`) — verify every touch region + physical button routes.

## 4. Error hunt (systematic)
A full pass to find ANY screen error: run `build.py preview` for every page (PNG render without the game),
compare each against the reference art + the live-demo captures, and in-game verify each page + control. Log
every mismatch (mirrored/dark/misaligned/dead-control/missing-readout) like a flight error. Feed fixes phase-
style (batch per page, verify). Known-open from memory: AN/DN node markers (NAV V2), map-brightness, navball.

## 4b. Performance (60 fps benchmark — target rig: i5-14400F / GTX 1080 8GB / 16GB @ 1080p60)
From the KSP.log analysis (2026-08-28): no per-frame runtime spam (clean baseline), BUT some of OUR textures
(`art/hud_darken`, `art/navball`, `art/seat`) FAIL DXT compression (dims not a multiple of 4) → loaded
UNCOMPRESSED → ~4× VRAM on the 8 GB GTX 1080. **Fix: resize all shipped art to multiples of 4 (power-of-2
best).** Keep the screen render allocation-free + redraw-only-what-changed; throttle the RT cameras; keep our
own log volume modest (log-on-change). Our mod must NEVER be the reason FPS drops below 60. See
[[build-verify-no-shortcuts]] rule 12.

## 5. High-fidelity target + keep-list
- Match the real Crew Dragon glass-cockpit look + behavior (`REAL_DRAGON_SCREENS.md`, the live demo).
- ⛔ **KEEP unchanged:** the abort feature + display (DON'T-PANIC screen, klaxon, red-IVA strobe, the
  DEPRESS/SUPPRESS/FIRE response controls) — the user explicitly loves it.

## 5b. Missing pages + the hidden mini-game (from `SCREENS_LOOK_AND_FUNCTION_RESEARCH.md`)
Parity with the real Crew Dragon page set needs two pages we likely LACK, plus a fun easter-egg:
- ❌ **VEHICLE OVERVIEW page** — connections + life-support (PPO2/cabin temp/pressure/CO2/net power) + orbit +
  RANGE TO ISS + rendezvous-burn + thermal-shield + GO/NO-GO. Build it (live from TAC-LS + the autopilot phase).
- ❌ **SUIT LEAK CHECK page** (4.011) — the `SuitLeakG2` crew gate exists but has no page; build the real
  procedure (SUIT 1-4 delta pressure/status, PREPARE→EXECUTE→CLEAR/HALT) against the TAC-LS cabin model. ⭐ now
  buildable.
- ⭐ **HIDDEN DOCKING MINI-GAME** (user idea) — natively recreate the iss-sim docking experience (green-diamond
  target, roll/pitch/yaw null + XYZ translate, rates < 0.2 to dock) reusing `DockControl`+`NavBall`+`DockingPage`,
  triggered by a hidden gesture. ⛔ recreate the mechanics in OUR assets — never embed/ship iss-sim (proprietary).

## 6. How this fits the plan
Screens is its own phase (phase-order rule) and runs in PARALLEL with the autopilot flight-tuning — it's the
KEPT screens side and doesn't gate pad→orbit. Sequence within it: (1) the two NAV bugs; (2) the button-wiring
audit; (3) the per-page feature audit + error hunt; (4) fidelity polish. All screens code is pure + PNG-preview
testable, so most of it is headless-verifiable before an in-game check. Cross-refs: `docs/REAL_DRAGON_SCREENS.md`,
`docs/REFERENCE_PAGES.md`, `docs/UI_AUDIT.md`, `docs/AUTOPILOT_REBUILD_PLAN.md`.
