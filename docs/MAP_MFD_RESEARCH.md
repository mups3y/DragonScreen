# Map-MFD on the DragonScreen — research + build plan (live 3D scaled-planet view)

**Goal (user 2026-08-27):** all the capability of the KSP map screen on the DragonScreen round-earth NAV
screen, so the crew never has to leave IVA — *with a live 3D scaled-planet view* as the chosen centrepiece.

> ⚠ **§1 and §2 are PARTLY SUPERSEDED by [`NAV_MAP_RENDERING_RESEARCH.md`](NAV_MAP_RENDERING_RESEARCH.md)**
> (owner-directed research, 2026-09-03) — read that first, and where the two disagree **it wins**; it was
> written from the installed binaries, the shipped configs, a real flight log and KSP's own assembly
> metadata. What it changes here: §1's "you cannot pipe the map view" is **confirmed**, with the mechanism
> named (four cameras + a UI canvas, map-only renderers, a single global Vectrosity camera). §2's core
> prediction — a cloned `ScaledCamera` renders the real body and never asks for a texture slot — is
> **confirmed**, with one named limit (no scatterer halo, no Parallax scaled shadows, no TUFX grade). But
> §2.1's culling mask is **layers 9 + 10** (Atmosphere + Scaled Scenery), not layer 10 alone; §2.2's plan to
> re-project the *existing* overlay samples is **wrong** (they are the rotation-corrected ground track, not
> an inertial orbit); and the markers have a better source than drawing our own — KSP's own `OrbitIcons`
> atlas. §3 and §4 below are untouched and still stand.

---

## ⚠ BUILD STATUS — CORRECTED 2026-09-02 (T1): the scaled-space camera is NOT in this repo

**What is actually here:** NAV's **NEXT VIEW** does cycle **GROUND TRACK → ORBIT → 3D PLANET**, and the
3D-planet view is drawn by `pure/NavPage.Planet` — a textured globe disc (`NavPage.Globe`, from the
`BodyMap` texture) with the tested `pure/PlanetOverlay.cs` orbit/marker overlay drawn over it. `CoverPage`
reuses the same call, so the Cover globe and the NAV globe cannot drift apart.

**What is NOT here** — the T1 correction, now **half-answered by S10a (2026-09-02)**. T1 found that
`src/ScaledPlanetRenderer.cs`, `pure/PlanetGeom.cs`, `test/PlanetGeomTest.cs` and `ImageId.ScaledPlanetLive`
did not exist in this repo and never had (checked against the full git history), despite the paragraph below
reading as though they had shipped. Where that stands now:

- **BUILT by S10a:** `pure/PlanetGeom.cs` (the camera placement, its projection and its ray/sphere occlusion),
  `test/PlanetGeomTest.cs`, `ImageId.ScaledPlanetLive` + `PageState.PlanetCamLive` +
  `ImageStore.ScaledPlanetLive()` (the seam), and `NavPage.Planet`'s live path with the honest
  `LIVE 3D — NO SIGNAL` marking for the state where no camera is rendering — which is every state today.
- **STILL NOT HERE — S10b:** `src/ScaledPlanetRenderer.cs`. There is no scaled-space RT camera, no
  `CopyFrom(ScaledCamera.Instance.cam)` and nothing occlusion-culled behind a really rendered globe. It cannot
  be exercised with the game closed, so it is behind a separate owner `install` + glass go (§5).

Read the following block as the **design** for that camera — which is still the right design, and which
`PlanetGeom` now implements the arithmetic of — not as a status report. The original text:

- `src/ScaledPlanetRenderer.cs` (glue) — the RT camera. `CopyFrom(ScaledCamera.Instance.cam)` to inherit
  the exact culling mask/projection the map draws the planets with, then override target/clip/clear and
  the transform. Aimed each frame by the pure `PlanetGeom.Frame` in scaled space; re-aimed in OnPreCull
  (never a frame stale) and switched off by `Idle()` when unwatched — the DockingCam lifetime pattern.
  Exposed as `ImageId.ScaledPlanetLive`.
- Overlay: the vessel orbit, target orbit and Ap/Pe/vessel/target markers, sampled in world, converted to
  scaled space and projected to **viewport fractions with the camera's own `WorldToViewportPoint`** (so
  the line can't drift off the render), **occlusion-culled behind the globe** by the tested
  `PlanetGeom.Occluded`. Carried in pure `PlanetOverlay` (`PageState.Planet`), placed by `NavPage.Planet`.
- Controls: pan swings/tilts the camera about the orbit normal (`MapView.PlanetRotDeg/PitchDeg`), zoom is
  distance, CTR resets to the default 3/4 view.
- Pure + tested: `pure/PlanetGeom.cs` (`test/PlanetGeomTest.cs`, 22 checks), `pure/PlanetOverlay.cs`; the
  page-capacity sweep now covers all three NAV modes. Preview draws the honest "LIVE 3D — NO SIGNAL" state
  (there is no Unity camera with the game closed).

⛔ **NOT YET FLOWN** — verify in-game that the globe renders, the orbit line tracks and disappears behind
the planet, and the framing/zoom read well. **Deferred to V2 (see §4): interactivity** — maneuver-node
create/nudge, target-pick from the view, warp-to; and AN/DN node markers.

---

## 1. The core finding — you cannot pipe the real KSP map view

The KSP map view is **not** a camera feed you can redirect into a RenderTexture. It is a **modal scene mode**
(`MapView.EnterMapView()` / `MapView.MapIsEnabled`) built on the `PlanetariumCamera` + `ScaledSpace`, and its
overlays — the orbit lines of every object, the maneuver-node gizmos, the clickable vessel/planet icons — are
drawn by the map subsystem **only while map view is active**. So there is no way to render "the map, with all
its layers" to a texture while staying in the flight/IVA scene, and its interactivity (drag a node, pick a
target, warp) is mouse-picking bound to that scene. No mod has ever piped the literal map view onto an IVA
screen; it isn't a render-to-texture job, it would be a re-implementation.

**The proven approach is to BUILD the map as our own instrument** from the live orbit data — exactly what the
two big IVA frameworks do:
- **MAS (MOARdV's Avionics System) "Orbit Display"** draws the vessel orbit, the target orbit, the resulting
  orbit of a planned maneuver node, and the body — and is "more advanced than the RPM module."
- **RasterPropMonitor `JSIOrbitDisplay`** — the older, simpler version of the same idea.
- **External-camera-to-IVA-monitor** mods (HullCam, KerbalView, and *our own* NavBall + DockingCam renderers)
  prove that rendering a **dedicated camera into a RenderTexture the IVA screen draws** is routine.

So: a full "map MFD" that never leaves IVA is feasible and standard — as a custom page reading the same data
and driving the same plugin APIs. The literal-map mirror is the only part that is not.

Sources: [MAS MASMonitor / Orbit Display](https://github.com/FirstPersonKSP/AvionicsSystems/wiki/MASMonitor) ·
[RasterPropMonitor](https://github.com/FirstPersonKSP/RasterPropMonitor) ·
[external cameras on IVA monitors — the RT technique](https://forum.kerbalspaceprogram.com/topic/96082-external-camera-views-on-iva-monitors-howto/).

---

## 2. THE LIVE 3D SCALED-PLANET VIEW (the chosen build)

A real 3D globe of the body, rendered by a dedicated camera into a RenderTexture, with our orbit lines drawn
over it. This is the same machinery we already ship for the navball and the docking cam — just pointed at
**scaled space** instead of the local scene.

### 2.1 The camera (mirror `src/DockingCamRenderer.cs`)
Our `DockingCamRenderer` is the template: create a `Camera` GameObject, `cam.enabled = false` (render on
demand only), `cam.targetTexture = RenderTexture(ARGB32)`, set the culling mask, and expose it through
`ImageStore` as a live `ImageId` (like `DockingCamLive` / `NavBallLive`). Idle it off after N frames of not
being drawn; validate-not-remember across scene loads (the same fake-null trap the other renderers document).

The ONE difference is what it renders — **scaled space (the planets), not the local scene:**
- **Culling mask:** scaled scenery is KSP **layer 10 ("Scaled Scenery")**; the sky/galaxy is its own layer.
  Don't hard-code a bitmask (KSP's layers drift between versions, as DockingCamRenderer notes) — instead
  **`cam.CopyFrom(ScaledCamera.Instance.cam)`** (the game's own scaled-space camera) to inherit its exact
  culling mask + clip planes + projection, then override `targetTexture`, `enabled`, and the transform. That
  guarantees we render precisely what the map's planet render does.
- **Coordinates:** scaled space is the world shrunk by **`ScaledSpace.ScaleFactor` (6,000,000)**. Convert any
  world position with **`ScaledSpace.LocalToScaledSpace(worldPos)`**. The scaled body sits at its
  `body.scaledBody.transform.position`; the vessel's scaled position is `LocalToScaledSpace(v.transform.position)`.
- **Viewpoint:** place the camera in scaled space looking at the scaled body centre. Options (make it a
  tunable / a page control):
  - *Orbital chase*: behind/above the vessel's scaled position, framing body + vessel.
  - *Fixed north-up*: over the pole or the orbital-plane normal, so the orbit reads like a map.
  - Let the crew rotate/zoom via the touchscreen (see §4).

### 2.2 The orbit + map overlays (drawn by us, over the RT — the MAS/RPM part)
The native map lines will NOT render into our RT (they're map-mode overlays), so we draw them ourselves as
`DisplayList` polylines, projecting scaled-space points to screen through the camera:
1. **Vessel orbit:** sample `v.orbit.getPositionAtUT(t)` over one period (or the escape span), `LocalToScaledSpace`
   each, `cam.WorldToViewportPoint` → screen, connect. Mark **Ap/Pe/AN/DN**.
2. **Target orbit + marker:** same from `v.targetObject.GetOrbit()`.
3. **Maneuver-node result orbit:** each `v.patchedConicSolver.maneuverNodes[i].nextPatch` is an `Orbit` — draw
   it dashed. (This is also how we'll show a plan the crew builds — §4.)
4. **The body + its atmosphere ring, terminator/day-night** come free from the scaled render.
5. Reuse what the round-earth NAV screen already computes (ground track, lat/lon, target lat/lon).

### 2.3 Where it lives
A new `src/ScaledPlanetRenderer.cs` (the camera + RT, patterned on DockingCamRenderer) + `ImageId.ScaledPlanetLive`
in `pure/Images.cs`, drawn by a NAV page variant in `pure/NavPage.cs`. Keep the existing flat-map + orbit-side
views as selectable modes on the same page (the round-earth view becomes one of several map modes).

---

## 3. Capability parity checklist (what "all the map's capabilities" means, and how)
| Map capability | How on the DragonScreen |
|---|---|
| See the planet in 3D | ✅ §2 scaled-planet RT |
| Own orbit + Ap/Pe/nodes | ✅ draw from `v.orbit` |
| Target orbit + relative geometry | ✅ draw from `targetObject.GetOrbit()` (+ our LVLH) |
| Plan a maneuver node | build via `patchedConicSolver.AddManeuverNode(UT)` + set `node.DeltaV` (pro/normal/radial) |
| See the maneuver result orbit | ✅ `node.nextPatch` |
| Pick / change the target | `FlightGlobals.fetch.SetVesselTarget(...)` (we already do this for the ISS) |
| Time-warp to a node/Ap/Pe | `TimeWarp.fetch.WarpTo(UT)` |
| Focus / zoom / rotate | our camera transform + touch (§4) |
| Other vessels / bodies as icons | draw from `FlightGlobals.Vessels` / `FlightGlobals.Bodies` scaled positions |

Everything above is plain plugin API — no map-mode required.

---

## 4. Interactivity (later, on top of the view)
We already have touchscreen input (`src/ScreenTouch.cs`) and the RT pipeline, so the plumbing exists. The MFD
controls to add: node create/nudge (prograde/retrograde/normal/radial ± and slide the node time), target
select, warp-to, and view rotate/zoom. Each is an API call listed in §3 — a normal UI build, not research.

---

## 5. Scope + priority
This is a **screens-side feature**, not autopilot. ~~Per the governing plan, finish pad→orbit (the S2 ignition
fix) and the proving flights first~~ — **SUPERSEDED 2026-09-02 (T1):** the autopilot that sentence sequenced
against was deleted on 2026-09-01, and the live order is `REGISTER.md`.

~~This work is **T4 — Cover map-modes (2D/3D + camera)**, the first item of the §7 build order, and it is
BUILD-HELD until the owner's go.~~ — **RE-POINTED 2026-09-02 (S10a), and this was the stale line S10's
register entry flagged.** T4 was a different task and is DONE: it shipped the Cover's 2D/3D map MODES against
the pure globe that already existed, never the camera. §2's scaled-space camera is **S10**, which SPLITS the
way T11a/T11b did, because its RenderTexture renders nothing with the game closed and the standing go is
preview-only:

- **S10a — DONE 2026-09-02, preview-only.** The pure geometry (`pure/PlanetGeom.cs` + `test/PlanetGeomTest.cs`),
  the seam (`ImageId.ScaledPlanetLive`, `PageState.PlanetCamLive`, `ImageStore.ScaledPlanetLive()`), and
  `NavPage.Planet`'s live path — which draws the RT when one exists and otherwise keeps the textured disc and
  the projected orbit under an honest `LIVE 3D — NO SIGNAL` marking (§14.4(e)). The NAV sub-heading no longer
  claims `LIVE CAMERA` when there is no camera.
- **S10b — HELD, needs a separate owner `install` + glass go.** `src/ScaledPlanetRenderer.cs` itself (the
  camera, the RT, the `CopyFrom(ScaledCamera.Instance.cam)` and the lifetime), plus the three judgements only
  the capsule can settle: does the globe render, does the orbit line track and occlude behind true geometry,
  does the framing read at cabin distance. Carried on `REGISTER.md`'s **S18** glass checklist as **G11**.

It reuses infrastructure we already have (the RT camera pattern from `DockingCamRenderer`/`NavBallRenderer`,
touch, the NAV page). What is left for S10b: `ScaledPlanetRenderer` (camera+RT, ~a DockingCamRenderer-sized
file) aimed by `PlanetGeom.Frame`, one line in `ImageStore.ScaledPlanetTexture()` to hand the texture over,
then the node/target/warp controls of §4.
