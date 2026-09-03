# NAV Map Rendering Research — the real globe, the native orbit line, and what map view actually is

**Owner-directed research task, 2026-09-03. RESEARCH + THIS DOC ONLY — no code changed, no plan edited,
no gate opened or implied.** Commissioned to settle the NAV-map ARCHITECTURE: can we put the *actual* Earth
globe that KSP's map view renders onto our NAV/MAP screen, with the *native* orbit line and markers?

Governed by `docs/BUILD_PLAN.md` §1.4 (source-of-truth ladder) and §14.4(e)/(f). Reading KSP's and the
visual mods' own rendering to learn the PLATFORM is the same kind of read as §B2's MechJeb study — it is
**not** a DragonScreen build input, so **C7 is intact**. Nothing here was taken from the KSP install as a
build source; the install was read only as evidence about the platform, and every such reading is cited.

> ⚠ **This doc supersedes `docs/MAP_MFD_RESEARCH.md` §1 and §2 where they disagree with it**, and it
> **corrects a settled verdict on `REGISTER.md`'s S42**. The disagreements are named explicitly in §0 and
> §5. Where they conflict, **this doc wins**: it was written from the actual installed binaries, the actual
> shipped configs, a real flight log, and KSP's own `Assembly-CSharp.dll` metadata — none of which the
> earlier passes consulted. `MAP_MFD_RESEARCH.md` §3–§4 (the capability checklist and the interactivity
> plan) are untouched and still stand.

---

## 0. Four findings that change the question

Read these first. Three of the four make the original question smaller, and one makes it larger.

### 0.1 ⛔ There is no RSS on this install. The planet pack is **Sol**.

`GameData\` contains **no `RealSolarSystem` folder**. The two RSS-named directories present are
`RSS-CanaveralHD` and `RSSDateTime` — a launch-site pack and a date formatter, not the planet pack. What
supplies the real-scale solar system is **`Sol-Configs` / `Sol-Textures` / `Sol-Visuals`** (ModuleManager
tag `FOR[SolSystem]`), on **Kopernicus 1.0.247** and **ParallaxContinued 1.0.0**.

RealismOverhaul, RP-1, RealFuels, FAR and friends *are* installed, so the register's shorthand "RSS" is
right about the **regime** — RO/RP-1 at 1:1 scale, Earth at 6371 km — and that is all the S43 hairline
reasoning ever depended on. It is wrong about the **planet pack and its shader**, and that mattered,
because every conclusion about `Custom/HapkeScaled` was attributed to a mod that is not here.

### 0.2 ⭐ `Custom/HapkeScaled` **does** carry a colour map, and we already read it. S42's verdict is wrong.

`REGISTER.md`'s S42 concluded, on the strength of the 21-slot list, that *"no slot on `Custom/HapkeScaled`
carries a colour map … the 'read a named, verified slot' branch is **ruled out**, not merely unproven."*

**That is not what happens.** From the current `KSP.log` (17.7 MB, one session, 2026-09-03 14:53–15:12) —
both lines read first-hand, verbatim, 1.74 s apart:

```
104405:[WRN 14:57:35.508] [DragonScreen] no usable scaled-space map for Earth on shader
  'Custom/HapkeScaled' - NAV draws the grid and track only. … _MainTex=4x4, … _ColorMap=null, …
104544:[LOG 14:57:37.251] [DragonScreen] body map Earth 16384x8192 from _ColorMap on 'Custom/HapkeScaled'
```

`ImageStore.BodyMap`'s **first** listed slot, `_ColorMap`, resolves a **16384×8192** Earth colour map. The
failure was a **race, not an absence**. The material is re-textured at runtime by ParallaxContinued from
`Sol-Configs/Configs/03_Earth-System/03_Earth/Earth-ParallaxTerrain.cfg`:

```
ParallaxScaledProperties { Material {
    customShaderName = Custom/HapkeScaled
    _ColorMap  = Sol-Textures/PluginData/03_Earth-System/03_Earth/Kopernicus/Earth_Color.dds
    _BumpMap   = …/Earth_Normal.dds
    _ScatteringTex = …/Earth_Scatter.dds
    _SurgeTex  = …/Earth_Surge.dds
```

and Parallax ships `loadTexturesImmediately = False` (`ParallaxContinued/Config/ParallaxGlobalSettings.cfg`),
while Kopernicus's `ScaledSpaceOnDemand` independently loads and unloads Earth's scaled textures on renderer
visibility. The log shows both cycling: `LoadTextures loading Earth` at 14:56:21 and 14:56:29,
`UnloadTextures unloading Earth` at 14:56:57 and 14:57:05, `Loaded Parallax Texture … Earth_Color.dds` at
14:56:34, 14:57:37 and 15:12:29. Our probe at 14:57:35.508 landed inside an unloaded window. The `4x4`
`_MainTex` is Kopernicus's deliberate placeholder — `Earth-Kopernicus.cfg`'s `ScaledVersion` material points
at `Earth_Dummy.dds` on purpose, because Parallax, not Kopernicus, owns Earth's appearance here.

**Our code already handles this correctly and did not need changing.** `ImageStore.BodyMap` never caches a
null (`ImageStore.cs:211` re-runs whenever `mapTexture` is null), and its cache guard is
`ReferenceEquals(b, mapBody) && mapTexture != null` — where `!=` is Unity's overload, which reports a
*destroyed* texture as null. So an on-demand unload silently invalidates the cache and the next frame
re-resolves. That is the right behaviour by accident of good habit, and it is why the success line exists.

What is left of S42 is therefore **not** an architecture problem. It is one cosmetic defect: for the ~1.7 s
before the texture streams in, the MAP view draws grid-and-track and emits a `LogWarning` phrased as a
permanent verdict (*"NAV draws the grid and track only"*) for what is really a **not-yet**. See §5.

### 0.3 ⛔ The scaled-space camera has **never run, once**, and cannot be reached by the shipping UI.

`ScreenPainter.cs:56` sets `private const bool FigmaMode = true;`. The **only** call to
`ScaledPlanetRenderer.Request(...)` is at `ScreenPainter.cs:958`, inside the `else` branch that `FigmaMode`
skips. `Pages.Build` — whose `case 2:` is the only route to `NavPage.Build` (`Pages.cs:588`) — is in that
same dead branch. `FigmaUI.Build`'s switch (`FigmaUI.cs:194–225`) has **no `UiPage` that reaches
`NavPage.Build`**; the globes it can reach are `CoverPage` (`CoverPage.cs:397`) and `ManualChuteDeployPage`
(`:197`), both of which call the 4-argument `NavPage.Planet`, i.e. **`live: false`**.

So under the shipping UI: `lastWanted` is never set → `ScaledPlanetRenderer.Texture()` returns null at its
claim gate (`ScaledPlanetRenderer.cs:143`) → the camera is never built, never enabled → `PlanetCamLive` is
never true → and the honest `LIVE 3D — NO SIGNAL` marking is never drawn either, because it only fires from
the `live: true` overload (`NavPage.cs:367`).

**The log confirms it independently.** A full flight session contains **zero** occurrences of the string
`scaled-planet`, while the sibling camera logged `[DragonScreen] docking cam ready` once. `Build()` and
`Texture()` both log on success; neither ever ran.

This reframes **G11** completely. The register records that the 38 glass frames never showed the NAV page
and treats that as a scheduling accident. It was not: **there is no NAV page to open.** G11 cannot be
answered by opening a gate — it needs a page route first. Logged as **`REGISTER.md` S62** (this research is
**S61**).

### 0.4 KSP's map view is a **four-camera rig plus a screen-space UI canvas**, not a camera.

Confirmed from `Assembly-CSharp.dll`'s own metadata (read with the `Mono.Cecil.dll` that ships in
`KSP_x64_Data\Managed\`). `MapView` carries `mapCamera` (a `PlanetariumCamera`), **`vectorCam`**,
**`mapFxCameraNear`**, **`mapFxCameraFar`**, plus `orbitLinesMaterial` and `max3DlineDrawDist`. The Ap/Pe/
AN/DN/target icons are **`KSP.UI.Screens.Mapview.MapNode`** — a *UI* type, on a canvas, not world geometry.

That single fact settles §3 below: "render the map view into a RenderTexture" is not one camera to redirect.

---

## 1. THE GLOBE — can a dedicated camera get the real one?

### 1.1 How KSP renders planets, and where the globe actually lives

KSP draws a frame with a **camera rig**, because bodies beyond a few km live in **scaled space** — the world
shrunk by `ScaledSpace.ScaleFactor` (6,000,000). MAS's `MASCamera.cs:322–347` (vendored in this repo at
`assets/reference/AvionicsSystems-master/`) is the best public write-up of that rig, and it names the
cameras and their culling masks:

```
GalaxyCamera        = 4'0000 -> SkySphere
Camera ScaledSpace  =    600 -> (layers 9 + 10)
Camera 01 / 00      = 8A'8013 -> Default | TransparentFX | Water | Local Scenery | … | ScaledSpaceSun
FXCamera            =  2'0001
```

MAS's *comment* labels `0x600` with Unity's default layer names, which is wrong for KSP. The **bitmask** is
the real datum, and the layer table read straight out of this build's `globalgamemanagers` resolves it:

| | | | |
|---|---|---|---|
| 0 Default | 9 **Atmosphere** | 18 SkySphere | 27 WheelColliders |
| 4 Water | 10 **Scaled Scenery** | 20 Internal Space | 29 DragRender |
| 5 UI | 12 UIVectors | 24 MapFX | 31 Vectors |
| 8 PartsList_Icons | 15 Local Scenery | 25 UIAdditional | |

So `0x600` = **layers 9 + 10 = Atmosphere + Scaled Scenery**. `MAP_MFD_RESEARCH.md` §2.1's guess of
"layer 10" was right about the planet and missed the atmosphere shell. The doc's advice not to hard-code the
mask stands and is now proven: the mask is two layers, not one, and it is version-dependent.

The load-bearing consequence: **the scaled bodies are live in the FLIGHT scene at all times.** The Earth you
see out the IVA window *is* the scaled-space render. Map view does not create the globe; it points a
different camera at the one that is already there.

### 1.2 What `CopyFrom(ScaledCamera.Instance.cam)` gets you — and what it does not

`ScaledPlanetRenderer.Build()` (`ScaledPlanetRenderer.cs:213–247`) does the right thing and its header
argues it well: copy the game's own scaled camera to inherit the exact culling mask, clip planes and
projection, then override only target, clear, depth and transform.

**`Camera.CopyFrom` copies camera *settings*.** It does not copy the **components attached to that camera's
GameObject**, nor its **CommandBuffers**. On this install, three separate mods hang work off the stock
scaled camera, and none of it comes across:

- **scatterer 0.0903** (`[Scatterer][Info] Running in unified camera mode`) attaches a `scaledCameraHook`
  and adds `ScaledScatteringContainer` / `ScaledScatteringScreenCopy` — symbols recovered from
  `Scatterer.dll`. Its scaled-space atmosphere is a screen-space composite pass on *that* camera.
- **ParallaxContinued** runs `Parallax.Scaled_System.RaymarchedShadowsRenderer` — scaled-space raymarched
  shadows, 48 steps (`ParallaxGlobalSettings.cfg`), again camera-attached.
- **Deferred 1.3.5** logs `[Deferred] Replacing shaders for deferred rendering`, and **TUFX 1.1.1** applies
  post-processing per camera.

**Verdict — §2's core prediction is CONFIRMED, with a named limit.** A cloned scaled camera renders whatever
material the body is wearing and **never asks for a texture slot**, so it gets the real Parallax
`Custom/HapkeScaled` Earth with its 16 K `_ColorMap` — the globe, the terrain shading, the ocean, the
day/night terminator, all real. What it will **not** have is scatterer's atmospheric halo, Parallax's
scaled shadows, or TUFX's grade. It will therefore look *crisper and flatter than map view*, like an
instrument rather than a photograph. For an MFD that is arguably the better picture, and it is an honest
render of real geometry — but it must be described that way and not as "what map view shows".

Two further real constraints, both already handled by the existing design:

- **Timing.** With `loadTexturesImmediately = False` plus Kopernicus on-demand cycling (§0.2), the material
  can be untextured for a second or two after scene entry. `Build()` is deliberately **not** latched
  (`ScaledPlanetRenderer.cs:202–210`, "a missing scaled camera is a NOT YET") and retries every frame, which
  is the correct shape for this too.
- **Our camera itself changes the on-demand state.** Kopernicus's `ScaledSpaceOnDemand` triggers on Unity's
  `OnBecameVisible` / `OnBecameInvisible` with no MapView or distance test. Unity documents `OnBecameVisible`
  as *"Called when the renderer becomes visible to any camera"* — including a camera rendering to a
  RenderTexture. So a DragonScreen scaled camera framing Earth **pins Earth's scaled textures loaded** while
  the page is up. That is a small, real VRAM cost (`Earth_Color.dds` is 178 MB on disk) and a free side
  benefit: it also makes the flat MAP view's `_ColorMap` lookup succeed more reliably.
- **Moving the camera is legitimate**, though MAS declines to. MAS pins its scaled clone to the stock
  camera's position (`MASCamera.cs:1157`, *"Until / unless I figure out how to locate my position in SS"*)
  because it wants a first-person view. We want a map-like view, `ScaledSpace.LocalToScaledSpace` gives a
  well-defined scaled position for any world point, and `PlanetGeom.Frame` already solves the placement.
  At ~2.2 body radii Earth sits ~1.06 scaled units across, comfortably inside the inherited clip planes.

---

## 2. THE ORBIT LINE + MARKERS — can any of KSP's own be reused?

### 2.1 The classes exist in this build — confirmed, not assumed

Read from `Assembly-CSharp.dll` (3377 types) via `Mono.Cecil`: `OrbitRenderer`, **`OrbitRendererBase`**,
`OrbitDriver`, `PatchedConicSolver`, `PatchedConicRenderer`, `MapObject`, `OrbitTargeter`, `MapViewFiltering`
and `KSP.UI.Screens.Mapview.MapNode` are all present. `OrbitRendererBase` carries `DrawOrbit`, `DrawNodes`,
`DrawSpline`, `CreateScaledSpaceNodes`, `DestroyScaledSpaceNodes`, `FindMapObject`, `RefreshMapObject`,
`IsRenderableOrbit`, `GetCurrentDrawMode`, **`layerMask`**, **`draw3dLines`**, `driver`, and the map-object
handles `objectMO` / `ApMO` / `PeMO` / `AscMO` / `DescMO`.

The working pattern for driving them is public and known ([forum 143101], the ESLD Beacons solution):
`AddComponent<OrbitDriver>` + `AddComponent<OrbitRenderer>` on a GameObject, set
`OrbitRendererBase.DrawMode` / `DrawIcons`, and `MapView.MapCamera.gameObject` is where the stock ones live.
`upperCamVsSmaRatio` / `lowerCamVsSmaRatio` gate visibility on **camera distance vs semi-major axis** — KSP's
own answer to the S43 problem, and evidence that a scale-band rule is the conventional fix.

### 2.2 Three reasons they cannot be captured onto our RenderTexture

**(a) They only run in map view.** Kopernicus's own `RuntimeUtility.ApplyOrbitVisibility` gates every
`body.orbitDriver.Renderer` touch on

```csharp
if (HighLogic.LoadedScene != GameScenes.TRACKSTATION &&
    (!HighLogic.LoadedSceneIsFlight || !MapView.MapIsEnabled)) return;
```

If the most-installed planet mod in the game must wait for `MapView.MapIsEnabled` to touch a renderer, so
must we. In IVA the lines are not merely invisible — they are not being generated.

**(b) The lines are Vectrosity, and Vectrosity's 3D camera is a single global.** KSP draws map lines with
Vectrosity (`MapView.vectorCam`, layers `Vectors`/`UIVectors`, `MapView.orbitLinesMaterial`,
`max3DlineDrawDist`, `OrbitRendererBase.draw3dLines`). `VectorLine.SetCamera3D(...)` is **static** — one 3D
camera for the whole library at a time — and KSP swaps it between `FlightCamera.fetch.cameras[0]` and
`PlanetariumCamera.Camera` on `GameEvents.OnCameraChange` ([gotmachine gist]). A second camera cannot be
given its own Vectrosity view; it can only steal the global one from the game.

**(c) The markers are UI, not geometry.** `MapNode` lives in `KSP.UI.Screens.Mapview`. Canvas UI does not
appear in a world-space camera's RenderTexture at all, whatever its culling mask.

**Honest verdict: the native line cannot be captured. We draw our own.** That is also what every shipping
IVA mod concluded — MAS's `MASPageOrbitDisplay` builds its **own** `LineRenderer`s on a private layer (29)
with a dedicated camera into its own RT, and Principia, which had every reason to reuse the stock renderer,
draws `GL.Vertex3` lines in `OnRenderObject()` through `PlanetariumCamera.Camera.WorldToScreenPoint`.

### 2.3 What *can* be reused, natively — the icons

This is the salvageable half of the ask, and it is cheap. `MapView` exposes, as **statics that do not
require map view to be enabled**: `OrbitIconsMaterial`, `OrbitIconsMap`, `DottedLinesMaterial`,
`OrbitLinesMaterial`, `PatchColors` and `OrbitIconsTextSkin`. MAS pulls the *newer* atlas by name rather
than through the stale `MapView.OrbitIconsMap` property (`MASLoader.cs:109–131`, credited to DMagic/SCANsat):

```csharp
foreach (Texture2D t in Resources.FindObjectsOfTypeAll<Texture2D>())
    if (t.name == "OrbitIcons") { orbitIconsAtlas = t; break; }
```

Sampling that atlas as an `ImageId` and drawing AP / PE / AN / DN / manoeuvre / target as textured quads
gives **genuinely native markers** — the same sprites the map draws — with our own placement and our own
occlusion. `PatchColors` gives the native patch palette for free. This is the one place "native" is
achievable, and it is a small, low-risk job.

---

## 3. THE WHOLE MAP VIEW — feasible or not?

**Not feasible. This is a firm no, and the mechanism is specific rather than a shrug.**

1. **It is a camera MODE, not a scene, and it is exclusive with IVA.** `CameraManager.CameraMode` has `Map`
   as a peer of `Flight`/`IVA`/`Internal`; `MapView.EnterMapView()` is documented as *"the game to switch to
   map view from the flight view"*. Entering it takes the crew's own view away — the exact thing the NAV
   screen exists to avoid.
2. **It is four cameras plus a canvas** (§0.4), not one target to redirect: `mapCamera`, `vectorCam`,
   `mapFxCameraNear`, `mapFxCameraFar`, and a screen-space UI canvas for every `MapNode` icon.
3. **Its line layer is globally owned** by Vectrosity's single `SetCamera3D` (§2.2b).
4. **Its renderers self-gate on `MapView.MapIsEnabled`** (§2.2a).

To "capture map view" one would have to stand up a parallel rig of three-to-four cameras, instantiate a
private set of `OrbitRenderer`/`PatchedConicRenderer` components, re-point a global Vectrosity camera every
frame and put it back, and re-implement the icon canvas in world space — while the game's own map is off.
That is not a capture; it is a re-implementation with a hostile dependency on internals that are neither
documented nor stable. `MAP_MFD_RESEARCH.md` §1 reached the same conclusion in 2026-08-27 by a different
route and it was right; this pass confirms it with the mechanism named.

---

## 4. RECOMMENDATION — the cleanest achievable architecture

**A hybrid, and it is the one the code is already 80% shaped for: KSP's real render underneath, our own
vector overlay on top, native icon art for the markers.**

### 4.1 The 3D PLANET view

1. **Globe: the cloned scaled camera** — `ScaledPlanetRenderer` as built. It is the only route that gets the
   real body, it is immune to the texture-slot problem by construction, and §1.2 confirms it works on this
   install. Document its one honest limit (no scatterer halo, no Parallax scaled shadows).
2. **Orbit line: ours, re-projected through the camera** — S37, unchanged and now *required*, not optional.
   `PlanetGeom.Project` and `PlanetGeom.Occluded` are already written and tested (66 headless checks) and
   are called by nothing; S37 is the wiring.
   ⚠ **S37 is bigger than it looks, and this is the sizing the register asked for.** The overlay's current
   samples are `PlanetOverlay`'s **rotation-corrected ground track** — `VesselData.GroundTrack` subtracts
   `360·dt/rot` from each longitude (`VesselData.cs:590`). That is exactly right for the flat map and
   **wrong for a 3D inertial view**: over one LEO orbit Earth turns ~22°, so re-using it would draw a
   visibly wrong curve. S37 needs its own **inertial scaled-space sample buffer**, not a re-projection of
   the existing one.
3. **Markers: the native `OrbitIcons` atlas** (§2.3), placed by our projection, culled by
   `PlanetGeom.Occluded`.

### 4.2 The flat MAP view

**Keep it exactly as it is.** §0.2 shows it already resolves a real 16384×8192 Earth colour map from
`_ColorMap`. The only change worth making is to the *warning*: treat "no usable map" as a **not-yet** for the
first few seconds (the same shape as `ScaledPlanetRenderer.Build`'s deliberate non-latching) and only
escalate to a warning if it is still failing after, say, 10 s or N attempts. As written it announces a
permanent defect for a transient one, and that mis-report is what sent S42 to the wrong conclusion.

### 4.3 The guard / soft-dependency story

The design is **already** soft-dependency-free, which is its main virtue and worth stating plainly:

- **Zero references** to `MapView`, `PlanetariumCamera`, `OrbitRenderer`, `OrbitDriver` or
  `PatchedConicRenderer` anywhere in the plugin (whole-tree grep). Nothing to guard, nothing to reflect over,
  no assembly to soft-depend on.
- The camera path touches only `ScaledCamera.Instance.cam`, `ScaledSpace.LocalToScaledSpace` /
  `InverseScaleFactor` and `CelestialBody.scaledBody` — **stock KSP, present since forever**, confirmed in
  this build's assembly metadata.
- **It degrades in the right direction at every step.** No `ScaledCamera` yet → `Build()` returns false and
  is asked again next frame. No vessel/body/orbit → `Aim()` false, camera off, one latched warning. No
  texture → the page keeps the textured disc and the projected orbit, both real, under the amber
  `LIVE 3D — NO SIGNAL` marking. No `_ColorMap` → grid and track. **Stock Kerbin, Sol, or a bare install all
  work**, because nothing in the path names a mod.
- The one new soft dependency the recommendation adds is the icon atlas, and it guards itself: if
  `Resources.FindObjectsOfTypeAll<Texture2D>()` finds no `OrbitIcons`, fall back to the box/cross glyphs
  `NavPage` already draws.

### 4.4 Order of work

**The blocker is not the camera — it is that nothing can reach it (§0.3).** Any glass time spent on G11
before a page routes to the 3D PLANET view is wasted. So: *route the page → then the camera answers G11 and
G12(3) on its own → then S37 → then the icons.*

---

## 5. What this means for S10b / S37 / G12(S42) / S43

| Line | Verdict | Why |
|---|---|---|
| **S10b** | **KEEP — and its blocker is re-diagnosed.** Still HELD, still needs an owner `install` + glass go. | The design is confirmed sound (§1.2): a cloned scaled camera does get the real globe and is immune to the slot problem. But **G11 is unanswerable as scheduled** — `FigmaMode` means no page can claim the camera, and the log proves it has never run (§0.3). A page route must exist first. |
| **S37** | **KEEP — promoted from "optional companion" to REQUIRED, and re-sized.** | With the camera confirmed as a *perspective* render from a *moved* eye, the orthographic overlay will not sit on it — the defect is certain, not suspected. New finding: the fix needs **new inertial sample buffers**, because the existing overlay samples are the rotation-corrected ground track (§4.1). |
| **G12 / S42** | ⚠ **SUPERSEDED IN PART — the "no colour map exists" verdict is factually wrong.** | `_ColorMap` resolves 16384×8192 on the very same shader, 1.7 s later, in the same log (§0.2). It was a load-timing race against ParallaxContinued + Kopernicus on-demand, not an absent slot. **G12(1) and G12(2) are answered off the log; G12(3) still rides S10b.** What remains is a warning-phrasing fix, not an architecture branch. ⛔ **This chat does not mark S42 DONE** — it is owner-gated, and re-verdicting a settled line is the owner's call (C1.8/C1.12). Put to the owner in the handoff prompt. |
| **S43** | **KEEP, unchanged and untouched by all of this.** | The hairline is the ORBIT view — a flat schematic that never involved the globe camera, the shader, or the projection. Its cause is the `max(a(1+e), R)` extent rule with **no zoom wired** (`NavPage.cs:591–597`; `Controls()` marks the cluster inactive for `NavMode.Orbit`, `:899`). Its own diagnosis — wire the existing zoom — stands. Supporting evidence: KSP solves the same problem with `upperCamVsSmaRatio`/`lowerCamVsSmaRatio` (§2.1), i.e. a scale band, confirming a scale rule is the conventional answer. |

**Net effect on the original question.** Using KSP's own map rendering does **not** make S37, G12 and S43
moot. It makes **G12 moot for a different reason** (there was never a missing colour map), leaves **S43
entirely untouched**, and makes **S37 mandatory**. The globe half of the ask is achievable and confirmed;
the native-orbit-line half is not, and the honest substitute is our own line plus KSP's own icon art.

---

## 6. Sources

**Platform, read first-hand on this machine** (evidence about KSP/mods, not build inputs — see the preamble):
`KSP.log` (2026-09-03, 17.7 MB, one session) · `KSP_x64_Data\Managed\Assembly-CSharp.dll` via the shipped
`Mono.Cecil.dll` · `KSP_x64_Data\globalgamemanagers` (layer table, Unity 2019.4.18f1, KSP 1.12.5.3190) ·
`GameData\AdvancedPQSTools\Patches\Parallax-ShaderBank.cfg` · `GameData\Sol-Configs\…\Earth-ParallaxTerrain.cfg`
and `…\Earth-Kopernicus.cfg` · `GameData\ParallaxContinued\Config\ParallaxGlobalSettings.cfg` ·
`GameData\Sol-Visuals\…\Earth-ScattererAtmosphere.cfg` · `GameData\Scatterer\Scatterer.dll` (symbols).

**In-repo reference:** `assets/reference/AvionicsSystems-master/Source/MASCamera.cs` (the camera rig and
culling masks), `…/MASPageOrbitDisplay.cs` (a shipping IVA orbit display), `…/MASLoader.cs` (the icon atlas).

**Public:**
- [KSP API — MapView](https://anatid.github.io/XML-Documentation-for-the-KSP-API/class_map_view.html) ·
  [PlanetariumCamera](https://anatid.github.io/XML-Documentation-for-the-KSP-API/class_planetarium_camera.html)
- [forum 143101 — Drawing orbits with no attached vessel](https://forum.kerbalspaceprogram.com/topic/143101-solved-drawing-orbits-with-no-attached-vessel/)
- [gotmachine — Enable Vectrosity in the KSP flight scene](https://gist.github.com/gotmachine/47f20ac412ec4c5d1630b3b772b6d271)
- [Kopernicus `RuntimeUtility.ApplyOrbitVisibility`](https://github.com/Kopernicus/Kopernicus/blob/master/src/Kopernicus/RuntimeUtility/RuntimeUtility.cs)
- [Kopernicus `ScaledSpaceOnDemand.cs`](https://github.com/Kopernicus/Kopernicus/tree/master/src/Kopernicus/OnDemand)
- [Unity — `MonoBehaviour.OnBecameVisible`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnBecameVisible.html)
- [Principia — map visualisation](https://deepwiki.com/mockingbirdnest/Principia/4.3-map-visualization)
