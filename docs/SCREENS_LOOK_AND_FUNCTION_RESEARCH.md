# DragonScreen — how the screens should LOOK and FUNCTION (comprehensive research)

> **Why (2026-08-28):** before the ULTIMATE PLAN, close the hole — a single comprehensive record of every
> resource we have + can find on the real Crew Dragon displays, the full page set + function, how it maps to
> OUR pages, what's now buildable, and the hidden docking mini-game idea. Consolidates the scattered screen
> research (`UI_AUDIT.md`, `REAL_DRAGON_SCREENS.md`, `REFERENCE_PAGES.md`, `MAP_MFD_RESEARCH.md`) + new finds.

---

## 1. Resource inventory — what we HAVE + what we can USE (with licences)
| Resource | Where | Licence / use | Value |
|---|---|---|---|
| **Figma: Dashboard UI** (Frames 58/59/66/67 + A-Settings-Cabin/Seat1-4) | `assets/figma/dashboard_ui/` | **CC BY 4.0** (attribute) — USE | ⭐ the richest instrument-page geometry (Frame 58 = 493 paths, 0 rasters) |
| **Figma: Flight Control UI** (Container.svg) | `assets/figma/flight_control_ui/` | CC BY 4.0 — USE | the flight-control screen layout |
| **Figma: Dragon Interface Docking** (Space X Interface.svg) | `assets/figma/dragon_interface_docking/` | CC BY 4.0 — USE (strip the 20 MB Mars raster) | the docking translation/rotation pads |
| **Vue "live demo"** (dragon2-ui, Neel Dandiwala) — App/views/components + assets | `assets/reference/dragon2-ui-master/` + `dragon2-ui-assets/` | **Apache 2.0** (attribution + keep NOTICE) — **USE the code AND assets** | ⭐⭐ the FUNCTIONAL spec — exact layout + every label, audited in `UI_AUDIT.md` |
| Kenney UI Sci-Fi | `assets/reference/kenney_ui_scifi/` | CC0 | fallback chrome only — WRONG visual language, don't build from it |
| AvionicsSystems (MAS) reference | `assets/reference/AvionicsSystems-master/` | reference | MAS MFD/touchscreen patterns (how RPM/MAS build IVA screens) |
| **iss-sim.spacex.com** (official SpaceX ISS Docking Simulator) | web (SpaceX) | ⛔ proprietary — REFERENCE ONLY, recreate, don't lift assets | ⭐ the AUTHORITATIVE manual-docking screen (NASA STEM toolkit; built on the real Crew Dragon UI/UX by Shane Mielke) |
| **NEW Figma: "Spaceship UI: Mission Mars I"** | [figma community 1449003371236258569](https://www.figma.com/community/file/1449003371236258569/) | check licence | inspired by Dragon docking — lower priority (generic) |
| **MUTANTdragon** (Dillon Baird) | [dillonbaird.io](https://dillonbaird.io/articles/mutantdragon/) + GitHub | reference | a full HW+UI recreation writeup — extra page detail if needed |
| **Design writeups** | [Ulises Siriczman (UX Collective)](https://uxdesign.cc/how-i-recreated-crew-dragons-ui-15877eddf3ed) · [Neel Dandiwala (Medium)](https://bootcamp.uxdesign.cc/recreating-the-ui-of-spacex-dragon-2-fb326cf9de8d) | reference | how the real UI is structured (look + function) |
| Real webcast footage (Demo-2 / Crew-1) | NASA/SpaceX webcasts | reference | the actual in-flight screens (confirm specific pages) |
⭐ **The real Crew Dragon UI is HTML/JS rendered by Chromium** (SpaceX's own JS component library) — confirmed;
so faithful HTML/vector recreations (Figma + the Vue demo) ARE close to the real thing.

## 2. The real Crew Dragon page set + FUNCTION (from the recreation, `UI_AUDIT.md`, + iss-sim)
The faithful recreation (and the real UI) is a small set of full-screen pages, cycled by a NEXT-VIEW control:
1. **Deorbit / NAV** (`First.vue`) — ACTIVE PHASE (e.g. "Deorbit Coast"), SPLASHDOWN TIME, INERTIAL VELOCITY,
   ALTITUDE/APOGEE/PERIGEE/INCLINATION, the **deorbit procedure list** (Depart & burn → Coast to Trunk → Claw
   Separation → Manual Chute), **Crew Interrupt Conditions** (30° sustained altitude error / FAR-FIELD POINTING
   / 600°/min altitude rate), the **Crew Deorbit Preparation timeline** (Deorbit burn −3 hr … −1 hr … −30 min,
   Go/No-Go, Acknowledge), ENTRY ENABLED true/false, TARGET LAT/LON, a 3D Earth. NEXT VIEW.
2. **Manual Flight / DOCKING** (`Second.vue`) — the **navball** + HUD ring, **YAW / PITCH / ROLL** numbers,
   **RANGE / RATE / ACCELERATION**, XYZ translation, FLIGHT COMMANDS (WASD move, R\|F up/down, Q\|E roll,
   arrows pitch/yaw). ⭐ this IS the iss-sim manual-docking interface (see §5).
3. **VEHICLE OVERVIEW** (`Overview.vue`) — CONNECTIONS (manual rings / airlock / wing / connected), **PPO2,
   CABIN TEMP, CABIN PRESSURE, CO2, LOOP A, NET PWR 1/2**, INERTIAL VELOCITY/ALTITUDE/APOGEE/PERIGEE/INCL,
   **RANGE TO ISS**, RENDEZVOUS BURN, THERMAL SHIELD, BURN GO/NO-GO, STATION DECK CHECK, ALL SYSTEMS CHECK.
4. **MECH PANEL** (`Mech.vue`) — ACCELERATION (positive/negative/angular/centripetal/antigravity), **PRESSURE,
   LQ OXYGEN, LQ NITROGEN, CO2 CANISTERS**, TRUNKS/DROGUES/MAINS FIRED, WATER UPRIGHTING, BALLAST/BILGE PUMPS,
   SEAT 1-4 TACH, ALL SYSTEMS CHECK.
5. **SUIT LEAK CHECK** (`Fourth.vue` + `SuitLeak.vue`) — ⭐ **4.011 Suit Leak Check**: PREPARE → EXECUTE, **SUIT
   1-4 DELTA PRESSURE + STATUS**, START/HALT, "CLEAR / PROCEDURE COMPLETE", "Crew can open visors but must not
   open zippers or disconnect umbilical." (Now buildable — §4.)
6. **SETTINGS** (`Fifth.vue`) — **Audio** (Seat 1-4 / Cabin / dB / Vox / Intercom / Alerts / Main/Aux), **Cabin**
   (LIGHTING: back/left/right/up/down/front/outside), **Video** (camera: front/rear/left/right, resolution).
7. **START** (`Start.vue`) — the boot/nebula screen. Plus **Capsule** (3D capsule) + **NavEarth** (scroll globe).

## 3. Map: real pages → OUR DragonScreen pages (what each should show/do)
| Real page | Our page (`src/pure/*Page.cs`) | Status / gap |
|---|---|---|
| Deorbit/NAV + procedure | `NavPage` (3 modes) + crew-gate procedure (`CrewGate`/`GateCard`/`StepList`) | ⚠ NAV bugs (§SCREENS_CONSOLE_PLAN); the deorbit procedure timeline + crew-interrupt conditions should be surfaced |
| Manual Flight / DOCKING | `DockingPage` + `NavBallRenderer` + `DockingCamRenderer` | ⚠ verify RANGE/RATE/ACCEL + the docking cues match iss-sim; wire manual-takeover |
| VEHICLE OVERVIEW | (⚠ **no dedicated Overview page yet**) | ❌ likely MISSING — build an Overview page (connections + life-support + orbit + GO/NO-GO) |
| MECH PANEL | `MechPage` | ✅ exists — verify every readout is live (LQ O2/N2, CO2, pressure from TAC-LS) |
| SUIT LEAK CHECK | (crew-gate `SuitLeakG2` exists; ⚠ **no leak-test PAGE**) | ❌ build the leak-test page/procedure — now buildable (§4) |
| SETTINGS (Audio/Cabin/Video) | `SettingsPage` (+ `DockingCam` for Video) | ⚠ verify audio/cabin/video controls exist + do something |
| START/boot | the monitor boot | — |
| Abort | `AbortOverlay` | ⛔ KEEP (loved) |

## 4. Functions now BUILDABLE (the cabin/life-support model unlocks them)
- ⭐ **Suit Leak Check (4.011)** — with the TAC-LS cabin model ([[dragonscreen-tac-life-support]]) + `VehicleSystems`,
  a REAL leak test: pressurise the suit loop, watch SUIT 1-4 DELTA PRESSURE, CLEAR/HANDLED status, HALT. The
  `SuitLeakG2` crew gate already exists; give it a real page + a real cabin-pressure procedure behind it.
- **Cabin/life-support readouts** (Overview + Mech: PPO2, CABIN TEMP/PRESSURE, CO2, LQ O2/N2) — live from TAC-LS.
- **DEPRESS/FIRE RESPONSE console buttons** — already tied to the abort/cabin FX; a real depress event now models.
- **GO/NO-GO gates, thermal shield, rendezvous-burn** — driven by the autopilot phase/FDIR state.

## 5. ⭐ HIDDEN DOCKING MINI-GAME (user idea 2026-08-28) — the iss-sim, natively recreated
Embed the SpaceX ISS-docking experience as a HIDDEN easter-egg mini-game on the screen (the reference UI itself
hints at "Easter Eggs hidden in space!"). ⛔ **We do NOT embed iss-sim** — it's proprietary + a web app KSP
can't host. We **natively RECREATE the mini-game** (mechanics aren't copyrightable; only SpaceX's assets are —
like the many open-source recreations):
- **Gameplay (from iss-sim, public):** a simulated ISS docking port with a **green-diamond target**; the player
  uses **roll/pitch/yaw** to null rotation (center the diamond) and **XYZ translation** to close; **blue numbers
  = rates**; **dock when all rates < 0.2**. Range/rate/acceleration readouts; the same navball/HUD as our
  manual-flight page.
- **How it fits us:** reuse our `DockControl` geometry + `NavBallRenderer` + the `DockingPage` cues + touch
  input; render the target/HUD with our `DisplayList`. It doubles as **manual-docking practice** (and the real
  manual-takeover mode). Trigger: a hidden gesture/easter-egg (e.g. a Konami-style panel sequence, or a hidden
  touch region), separate from the real flight so it can't interfere. Build reference: study iss-sim firsthand
  (in-app browser) for the exact HUD before recreating; keep OUR palette (`PALETTE.md`) + assets.
- **Licence:** build it from our LICENSED assets (Figma CC-BY + Vue Apache-2.0 + our own art) — those are free
  to use; recreate iss-sim's mechanics; the only thing off-limits is ripping SpaceX's OWN iss-sim files (which we
  don't have). Attribute the CC-BY/Apache sources in the release notes.

## 6. Licence summary — what we can ship (verified 2026-08-28: USE ALL our assets)
All the assets we HOLD are openly licensed and GPL-3.0-compatible — **use all of them**, they exist to be used:
- ✅ **Figma Community files** (Dashboard/Flight-Control/Docking) — **CC BY 4.0**: use/modify/ship, attribute (3 credit lines in the release notes). These are the authors' own recreations, publicly released — the reason none were "sued".
- ✅ **Vue recreation (dragon2-ui + dragon2-ui-assets)** — **Apache 2.0**: use the code AND assets; keep the LICENSE + NOTICE and attribute.
- ✅ **Kenney UI Sci-Fi** — **CC0**: public domain, no conditions (still a fallback visual language, not the Dragon look).
- ✅ **Our own recreation** (DisplayList-drawn pages) — ours.
- ⚠ **iss-sim.spacex.com — the ONE exception, and it doesn't cost us anything.** It's SpaceX's OWN site, so it's
  proprietary (publicly viewable ≠ licensed to copy). We don't have its files and don't need them: the docking
  mini-game recreates its **mechanics** (not copyrightable) using the CC-BY + Apache-2.0 + our own assets. Fan
  mods commonly use Dragon imagery and SpaceX is permissive in practice — but recreate-with-licensed-art is the
  clean path and looks just as good. So: **use everything we have; just don't rip iss-sim's own files (which we
  don't have anyway).**

## 7. Gaps / to verify (the residual research)
- Build an **Overview page** (missing) + a **Suit-Leak-Check page** (missing) to complete parity with the real set.
- Confirm the exact iss-sim HUD firsthand (in-app browser) before the mini-game build.
- Confirm licences on the 4th Figma file + the Vue/MUTANTdragon repos before using code.
- Real webcast frames (Demo-2/Crew-1) to confirm any page the recreation left ambiguous.

Cross-refs (the detail lives here — don't duplicate): `UI_AUDIT.md` (exact layouts + all labels),
`REAL_DRAGON_SCREENS.md` (hardware + the console buttons), `REFERENCE_PAGES.md` (the 8 reference pages),
`MAP_MFD_RESEARCH.md` (the NAV globe), `SCREENS_CONSOLE_PLAN.md` (the build workstream), `PALETTE.md`,
`ASSET_INDEX.md`, `assets/ASSET_PROVENANCE.md` (licences).
