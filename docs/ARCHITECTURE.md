# F9I Dragon screen — architecture

**Route confirmed by the user 2026-08-04: C# KSP plugin.** kOS keeps every flight decision; the plugin
owns rendering, input, layout and animation only. Crew Dragon only — Cargo and the fairing keep the
existing kOS GUI as a working fallback. The banner stays in kOS, untouched.

---

## MAS is a reference implementation we can borrow from, under MIT

`assets/reference/AvionicsSystems-master/` — MOARdV's Avionics Systems, the successor to
RasterPropMonitor. **Source, shaders, tools and DLL are MIT**, which is GPL-3.0 compatible, so code may
be lifted with attribution. (The *documentation* is All Rights Reserved — do not copy prose from it.)

**It is NOT installed into GameData and does not need to be.** We are using it as source, not as a
dependency. That keeps the mod list unchanged and the live install clean.

### The primitives it already implements, in KSP, working

`Camera · CompoundText · Ellipse · GroundTrack · Horizon · HorizontalBar · HorizontalStrip · Image ·
LineGraph · LineString · Menu · NavBall · OrbitDisplay · Polygon · RollingDigit · RpmModule · Text ·
VerticalBar · VerticalStrip · Viewport`

That list covers most of the Dragon screen. The ones that matter most to us:

| We need | MAS already has | Why it matters |
|---|---|---|
| Arc / ring gauges | `MASPageEllipse` (21 KB — the largest page renderer, does arcs with start/end angles) | This is the single element that made the kOS route impossible — it needed 143 pre-rendered PNGs |
| Odometer-style digits | `MASPageRollingDigit` | The Dragon telemetry readouts use exactly this |
| Vector shapes | `MASPageLineString`, `MASPagePolygon` | Feeds directly from the Figma SVG paths |
| Attitude / orbit | `MASPageHorizon`, `MASPageNavBall`, `MASPageOrbitDisplay`, `MASPageGroundTrack` | Whole features, free |
| Text | `MASPageText`, `MASPageCompoundText` | Font handling already solved |

## THE KEY FACT THAT MAKES REUSE POSSIBLE

MAS is usually described as IVA-only, and that is true of how it is *deployed* — a monitor targets a
prop screen inside a cockpit. But that is not how it *renders*.

`MASMonitor.cs`:

    private RenderTexture screen;
    screen = new RenderTexture(screenWidth, screenHeight, 24, RenderTextureFormat.ARGB32);
    screenCamera.targetTexture = screen;

**It draws pages with an orthographic camera into a RenderTexture.** Where that texture is then
displayed is a separate, later decision — MAS happens to put it on a prop material. Nothing in the
drawing stops it going somewhere else.

So the plan is: **borrow the page renderers, render into our own RenderTexture, and blit that into the
flight-scene panel** instead of onto a prop. The page renderers reference `InternalProp` only 2–3 times
each, which is parent-transform plumbing, not deep coupling — that is the part to check first when
porting, and it is small.

This is why the plugin route is still right even now that MAS exists: MAS's *deployment* target is
wrong for us, its *rendering* is exactly right.

## Consequence for the IVA question (deferred, not dead)

Because MAS is now on disk and MIT, the deferred "real screens inside the capsule" work gets much
cheaper if it is ever wanted. Tundra's `TE_CD2_IVA` already has a `TE_CD2_IVA_SCREEN` prop; it simply
has no monitor module. Adding one via ModuleManager is the standard MAS workflow. Not doing it now —
the user deferred IVA and the flight-scene panel is what gets used in external view, which is where
launches, booster landings and docking are actually watched.

---

## What still has to be written by us

1. **The state contract** — every value the screen displays, every command it can send. This is the
   first artefact and the expensive thing to get wrong. Needs no assets and no rendering.
2. **The kOS bridge** — a kOS addon, so scripts get `ADDONS:F9I:...`. This is the mechanism
   Trajectories, SCANsat and MechJeb all use, and all three are installed, so the pattern is proven
   on this install. Do NOT invent a file- or message-based channel.
3. **The panel host** — the flight-scene window, the RenderTexture blit, and input hit-testing.
4. **Page definitions** — the Dragon layout itself, measured from the Figma SVGs.

## Constraints carried forward

- Panel ~670 x 870 px, top right.
- **Three writers to window visibility must not collide.** Keep USER intent (`F9UserCollapsed`, written
  only by the tab) separate from MISSION intent (`F9OrbitStoodDown`). This exact bug was fixed on
  2026-08-04 when the `FalconLaunch` hand-back silently undid `F9StandDownAfterOrbit`.
- Everything reachable from the old kOS window must have a home on the screen, or Crew Dragon loses it:
  all 9 page stacks, LAUNCH, DE-ORBIT & LAND, the scale dialog, full Flight Settings, message1/2/3, and
  the EXECUTE/CANCEL bar that `confirm()` drives.
- **Stroke weight:** source frames are 3427 px wide with 2 px hairlines. Our panel is ~670 px. Scaled
  down that is sub-pixel — strokes must be 1 px and pixel-snapped or the whole thing turns to mush.
- `ALERTS` is a real capability gain: HUDTEXT warnings are currently thrown away.
