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

**The pages work. The flight software does not, yet.** Five test flights, five different failures,
and several of the fixes contained their own bugs. The most useful thing you can do is look for the
next one.

**Start with these three files, in this order:**

| file | what it is |
|---|---|
| `CLAUDE.md` | the project's memory — every decision, every trap, and a blow-by-blow of each failed flight with its root cause |
| `docs/F9I_PORT_MAP.md` | the port inventory: what has been taken from the working kOS autopilot, what has not, and what is known to be wrong |
| `plugin/src/pure/` | all the guidance and layout logic, with no engine dependencies |

**Known broken or unverified right now:**

- The **attitude controller has never successfully executed**. It threw `MissingMethodException` on
  every physics tick of its first flight (KSP's `QuaternionD.Euler` is broken; MechJeb ships its own
  for that reason). Fixed but unflown.
- **Booster recovery has never run once.** Every constant in `BoosterRecovery.cs` and
  `pure/Landing.cs` is untested in flight.
- **No orbit has been achieved.** The best flight reached a 73 km apoapsis and fell back.
- Rendezvous, docking, refuelling and closed-loop entry guidance are **not built** — see the port map.

---

## How it is put together

    plugin/src/pure/     no KSP, no Unity. Layout, guidance, orbital maths. Headless-tested.
    plugin/src/          the thin glue that talks to KSP.
    plugin/test/         ~7 400 headless checks, run on every build.
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
