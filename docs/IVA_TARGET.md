# The IVA is the target surface — what Tundra Exploration already gives us

Findings from `GameData/TundraExploration/`, 2026-08-05. **Read this before designing anything**: the
capsule interior is already modelled accurately, and everything we need is a *named transform*. We are
not building screen geometry, we are lighting up geometry that already exists.

## The screen prop

    GameData/TundraExploration/Props/TE_CD2_IVA_SCREEN.cfg   (+ .mu model)
    GameData/TundraExploration/Spaces/TE_CD2_IVA.cfg         places it, line ~89

The prop's config is three lines of nothing:

    MODULE { name = internalGeneric }

`internalGeneric` does exactly what it sounds like — **static art, zero functionality.** That is the
opportunity, not a problem: the hard part (accurate geometry, correct placement, matching textures)
is done, and the part nobody has done is the part we want to own.

## Named transforms inside the model

Extracted from the ASCII strings in `TE_CD2_IVA_SCREEN.mu`.

### Screens — THREE of them, exactly like the real capsule

    TT_CD2_IVA_SCREEN      base / frame
    TT_CD2_IVA_SCREEN1     display 1   LEFT
    TT_CD2_IVA_SCREEN2     display 2   CENTRE
    TT_CD2_IVA_SCREEN3     display 3   RIGHT

Left/centre/right confirmed in game 2026-08-05 by the proof pattern's bar count.

### CORRECTED 2026-08-05 from the running game — the .mu strings misled us

This section used to say "material `TE_CD2_PROP_SCREENS_TXT`, shader `KSP/Diffuse`". **Both were
wrong**, and they were read out of the model's ASCII strings rather than out of a live material.
What the game actually reports:

    material  TE_CD2_SCREEN_1 / _2 / _3   -- one material PER SCREEN, not one shared
    shader    KSP/Unlit                   -- NOT KSP/Diffuse
    slot      _MainTex                     -- this part was right

Two consequences, both good:

1. **`KSP/Unlit` means the displays are self-lit.** They do not dim with the cabin, which is exactly
   what a real screen does and what we would otherwise have had to fake.
2. **Separate materials per screen**, so the three were never going to fight over one texture. We
   still take `.material` (an instance) rather than `.sharedMaterial`, which is correct regardless.

**The lesson, and it is the project's own rule:** strings in a `.mu` name the ASSETS, not the live
material state. Mods rewrite shaders at load — Deferred and TexturesUnlimited are both installed
here. Read the material off the running game, which is why `DragonScreenMonitor` logs it at startup.

### Physical shape — MEASURED, not guessed

    screen 1  extents 0.2844 x 0.1561   aspect 1.8219
    screen 2  extents 0.2816 x 0.1561   aspect 1.8038
    screen 3  extents 0.2844 x 0.1561   aspect 1.8219

**The centre screen is ~1% narrower than the outer two.** Never assume the three displays are
pixel-identical; each gets its own render target sized from its own mesh. Note also that this is
**1.82:1, not the reference art's 1.62:1** — a page composed to the Figma frame will not fit.

**A RenderTexture assigns straight into `_MainTex`.** That is the whole integration: find the
transform, grab its renderer's material, swap the texture. This is what RasterPropMonitor and MAS both
do; we are doing the same thing with our own content.

### Buttons — the "analog buttons along the bottom", individually addressable

    TE_CD2_PROP_BUTTON_1 .. TE_CD2_PROP_BUTTON_8
    CD2_PROP_BUT1 .. CD2_PROP_BUT10, each with _2 _3 _4 _5 variants  (press states / rows - CONFIRM IN GAME)
    CD2_ABORT_HANDLE
    TT_CD2_IVA_BUTTONS      the panel itself

Material `CD2_BUTTONS`, shader **`KSP/Unlit`**, texture `TE_CD2_IVA_BUTTONS`.

Named transforms mean we can attach colliders and click handlers per button. The `_2.._5` variants are
**not yet understood** — could be press states, could be per-seat duplicates. Do not guess; look in
game before wiring them.

## What this means for the build

1. **Do not build screen geometry.** Build content, render it to a texture, assign it.
2. **Three surfaces, not one.** Design for three displays from the start — that is what the real
   capsule has and what this model has. A single-panel design would have to be torn up later.
3. **The floating window becomes a DEVELOPMENT VIEW, not the product.** Same RenderTexture, drawn with
   one `GUI.DrawTexture` in a window, so a page can be iterated without sitting in IVA. The IVA screen
   is the real target.

## THE ONE TECHNICAL CATCH — do not discover this the hard way

**`GUI.*` (IMGUI) calls render to the SCREEN and cannot be pointed at a RenderTexture.** The renderer
decision (IMGUI + GL, settled 2026-08-05 from MechJeb) still holds for *windows and input*, but page
CONTENT must be drawn with calls that respect `RenderTexture.active`:

    Graphics.DrawTexture(...)      textures / sprites
    GL.Begin(GL.TRIANGLES) ...     arcs, filled shapes  (MechJeb2/GLUtils.cs is the worked example)
    GL.Begin(GL.LINES)             hairlines

So: **page content = Graphics/GL into a RenderTexture. Chrome and input = IMGUI.** Getting this
backwards means building a page that can never leave the floating window.

## Also installed and worth knowing

- **RasterPropMonitor is present** (`GameData/JSI/RasterPropMonitor`). It could drive these screens
  today with cfg alone, by patching the prop's module — but RPM's look is retro green-on-black MFD,
  nothing like a Dragon touchscreen. Rejected for look, useful as a worked reference for the
  render-to-prop-material mechanism.
- The pod already carries `ModuleFreeIva` and `SCANRPMStorage`; **FreeIva is installed**, so moving
  around the cabin to reach the screens works.
- `TE_CD2_IVA_CUPOLA` is a second IVA space — check whether the tourist variant uses it.
