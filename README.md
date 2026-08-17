# DragonScreen

A Kerbal Space Program mod that makes the Crew Dragon's three IVA touchscreens **real** — they draw
live vehicle data, respond to touch, and fly the vehicle. Built against Tundra Exploration's Dragon
V2 and Falcon 9.

The goal, in the author's words:

> The end result I want is for the screens to look and act like the real crew dragon screens. I want
> to be able to press the buttons on the page and they do what they say they will do.

---

## ⚠ If you are here to help find bugs, read this first

This repository is **published specifically so other people and other AI can find what we have
missed**, so it is worth being blunt about where it stands.

**The pages work, and the flight software now flies — but not yet a clean mission end to end.**
Ascent, booster landing, orbital insertion and docking have all flown; rendezvous and the de-orbit
return have flown but not yet completed cleanly. Many test flights, many fixes, several of which
contained their own bugs. The most useful thing you can do is look for the next one.

**Start with these three files, in this order:**

| file | what it is |
|---|---|
| `CLAUDE.md` | the project's memory — every decision, every trap, and a blow-by-blow of each failed flight with its root cause |
| `docs/F9I_PORT_MAP.md` | the port inventory: what has been taken from the working kOS autopilot, what has not, and what is known to be wrong |
| `plugin/src/pure/` | all the guidance and layout logic, with no engine dependencies |

**Status as of 2026-08-17** (separating *flown* — in-game evidence in `docs/FLIGHT_*.md` — from
*ported* — code plus headless tests):

- **Flown and working:** the attitude controller (the `QuaternionD.Euler` crash was fixed), ascent to
  an 86 x 84 km orbit, S2 insertion, booster RTLS landing at 0.0 km / 1 m/s, and docking.
- **Flown but not yet clean:** rendezvous (launch timing left the capsule too far to close — re-tuned,
  unverified) and the de-orbit return (lands long — aim re-fit, unverified). No full end-to-end
  mission has completed cleanly.
- **Ported, not yet flight-verified:** refuel-while-docked, the automatic undock push, and the
  closed-loop lifting-entry steering. See `docs/MISSION_PLAN.md` and `docs/F9I_PORT_MAP.md`.

---

## How it is put together

    plugin/src/pure/     no KSP, no Unity. Layout, guidance, orbital maths. Headless-tested.
    plugin/src/          the thin glue that talks to KSP.
    plugin/test/         ~9 150 headless checks (14 suites), run on every build.
    plugin/preview/      renders every page to PNG without launching the game.
    docs/                research, the port map, the UI audit.

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

**GPL-3.0.** Not a preference — the flight software contains code ported from
[MechJeb2](https://github.com/MuMech/MechJeb2), which is GPL-3.0, and copyleft carries to
derivatives. Each port site cites the exact source file and line.

Also drawn on, with attribution at each site:

- **[MechJeb2](https://github.com/MuMech/MechJeb2)** (GPL-3.0) — attitude controller, landing speed
  policy, staging and vessel-switch APIs.
- **[Avionics Systems / MAS](https://github.com/MOARdV/AvionicsSystems)** (MIT) — the RenderTexture
  and GL glyph techniques, and the collider/touch mechanism.
- **[SpaceX-Dragon2-UI](https://github.com/Neel-Dandiwala/SpaceX-Dragon2-UI)** — the reference the
  page layouts are measured from.
- **Tundra Exploration** — the Dragon V2 and Falcon 9 parts and IVA this mod attaches to.

None of those are redistributed here; `.gitignore` keeps them out and the code cites them by path.

## Requirements

- **ModuleManager** and **Tundra Exploration** — required. The `.cfg` is a ModuleManager patch on
  Tundra's Crew Dragon IVA and pod; without either, nothing attaches.
- **PhysicsRangeExtender** — required for booster recovery. KSP clamps a vessel's physics range near
  300 km, so during the ~250 s the camera follows the booster down, the upper stage unloads and comes
  back a rebooted vessel — which disengages the ascent and loses the orbit. PRE lifts the clamp so the
  1500 km range the recovery already requests is honoured and the upper stage keeps flying itself.
  The mod raises the range only while focus is on the booster and restores it on handback
  (`BoosterRecovery.Extend`/`RestoreRanges`), so the reach is paid for only during recovery.
