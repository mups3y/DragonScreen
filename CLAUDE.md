# DragonScreen

**DragonScreen is the Crew Dragon IVA screens — the UI mod. It reads the vessel and draws it; it
flies nothing.** An autopilot that flew the vehicle from these screens was built here and **removed
2026-09-01** (owner directive: keep only the screens / the UI portion). All the flight-control,
guidance, rendezvous, docking, booster, entry and FDIR code — and its docs, plans, and flight
recordings — were deleted. If you find a reference to any of it, it is stale; remove it.

## What this repo is now

- Three live IVA touchscreens (VEHICLE / FLIGHT / NAV), each a RenderTexture + camera, drawn from
  live KSP state and driven by touch on the console colliders. See `docs/SCREEN_SPEC.md`.
- The command buttons that used to engage the autopilot are inert (`src/_AutopilotStub.cs` is the
  idle seam the screen code compiles against — status reads report "not engaged", flight commands
  no-op). The power / string / fire **systems** are real (pure `VehicleSystems`, display state only).

## The load-bearing rules (still true)

- **pure / glue split.** Everything decidable without the game lives in `plugin/src/pure/` and is
  headless-tested + PNG-previewable; `plugin/src/` is the thin KSP glue. Restarts are the scarce
  resource — judge layout/palette/legibility from `python plugin/build.py preview`, spend a restart
  only on what needs the capsule.
- **Build pages from the reference's own source, never a screenshot.** `docs/UI_AUDIT.md` is
  generated from the reference UI's CSS and gives exact positions. Screenshot/SVG-derived pages came
  out wrong every time.
- **Simulate, never fake.** Modelled signals (cabin PPO2, power strings, fire) move because the
  vessel moved — never a constant or a random number.

## Build / test

```bash
python plugin/build.py test      # compile (glue + pure) + run the headless display checks
python plugin/build.py preview   # render every page to PNG (no game)
python plugin/build.py install   # test, then copy the DLL + cfg into KSP  (needs KSP + CKAN closed, full restart)
```

## Start a session from

`docs/SCREEN_SPEC.md` (the screen spec) · `docs/UI_AUDIT.md` (exact layout source) ·
`docs/REAL_DRAGON_SCREENS.md` · `docs/PALETTE.md` · `docs/REFERENCE_PAGES.md`.
