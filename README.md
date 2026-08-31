# DragonScreen

A Kerbal Space Program mod that makes the Crew Dragon's three IVA touchscreens **real** — they draw
live vehicle data and respond to touch. Built against Tundra Exploration's Dragon V2 IVA.

The goal, in the author's words:

> The end result I want is for the screens to look and act like the real crew dragon screens. I want
> to be able to press the buttons on the page and they do what they say they will do.

> **Scope note (2026-09-01):** DragonScreen is now **screens only**. An autopilot that flew the
> vehicle from these screens was developed here and then **removed** — this repository is the IVA
> display mod: it *reads* the vessel and *draws* it, and it flies nothing. The craft flies on its
> stock RO/Tundra propulsion, hand-flown or by whatever autopilot you run separately.

---

## What it does

Three live touchscreens in the Crew Dragon IVA, each its own RenderTexture and camera:

- **VEHICLE** (left) — subsystems, consumables, power strings, and alerts, modelled from real vessel
  inputs (crew count, hull temperature, electric charge).
- **FLIGHT** (centre) — the telemetry strip and the Dragon illustration.
- **NAV** (right) — trajectory, ground track, orbit, and the 3D globe.

The pages read KSP directly and respond to touch on the console colliders. Any screen can show any
page, the same as the real capsule; the selection persists across saves.

---

## How it is put together

    plugin/src/pure/     no KSP, no Unity. Layout, display maths, orbital readouts. Headless-tested.
    plugin/src/          the thin glue that talks to KSP.
    plugin/test/         the headless display checks, run on every build.
    plugin/preview/      renders every page to PNG without launching the game.
    docs/                the screen spec, the UI audit, the palette, the reference-page research.

The **pure/glue split** is the load-bearing idea. Anything that can be decided without the game is
decided in `pure`, so it can be tested and previewed in half a second instead of a five-minute game
restart. Restarts are the scarce resource on this project and most of the tooling exists to avoid
spending one.

```bash
python plugin/build.py test      # compile + run every headless check
python plugin/build.py preview   # render every page to PNG
python plugin/build.py install   # test, then copy into KSP
```

`install` runs the tests first, on purpose: it used to copy whatever had just compiled.

---

## Two rules that explain most of the code

**Build pages from the reference's own source, never from a picture.** `docs/UI_AUDIT.md` is
generated from the reference UI's CSS and states every position exactly. Every page designed from a
screenshot or an SVG export came out wrong and cost a game restart.

**Simulate, never fake.** Stock KSP has no cabin PPO2, no power strings, no fire. Those are modelled
from real inputs — crew count, hull temperature, electric charge — so they move because the vessel
moved. A constant or a random number is forbidden: it is indistinguishable from a dead sensor.

---

## Licence

**GPL-3.0.** Some kept display maths (the per-stage Δv / burn-time readout) is ported from
[MechJeb2](https://github.com/MuMech/MechJeb2)'s FuelFlowSimulation, which is GPL-3.0, and copyleft
carries to derivatives. Each port site cites the exact source file and line.

Also drawn on, with attribution at each site:

- **[MechJeb2](https://github.com/MuMech/MechJeb2)** (GPL-3.0) — the fuel-flow / stage-Δv maths.
- **[Avionics Systems / MAS](https://github.com/MOARdV/AvionicsSystems)** (MIT) — the RenderTexture
  and GL glyph techniques, and the collider/touch mechanism.
- **[SpaceX-Dragon2-UI](https://github.com/Neel-Dandiwala/SpaceX-Dragon2-UI)** — the reference the
  page layouts are measured from.
- **Tundra Exploration** — the Dragon V2 parts and IVA this mod attaches to.

None of those are redistributed here; `.gitignore` keeps them out and the code cites them by path.

## Requirements

- **ModuleManager** and **Tundra Exploration** — required. The `.cfg` is a ModuleManager patch on
  Tundra's Crew Dragon IVA and pod; without either, nothing attaches.
