# F9I Dragon UI — asset provenance and licence position

Downloaded 2026-08-04. **This folder is deliberately OUTSIDE the KSP install** — nothing here is on
the live game path until the plugin is proven.

F9I ships **GPL-3.0**. Every asset below is compatible with that. Anything added later must be checked
before it goes in, and recorded here at the same time — not afterwards.

---

## 1. `figma/` — THE DESIGN. All three Community files, exported as SVG 2026-08-04.

- **Licence:** **CC BY 4.0** (Figma Community free files). Reusable commercially and in a GPL project,
  **but attribution is required** — three credit lines in the release notes.
- **How:** exported from the user's own logged-in Figma session, all frames selected, format SVG.
  Not fetchable unattended; a Figma account is required to duplicate and export a Community file.

| Folder | Source file | Contents |
|---|---|---|
| `dashboard_ui/` | [Dashboard UI](https://www.figma.com/community/file/973348428067457336/) | 9 frames — 4 instrument pages + 5 seat/cabin settings pages |
| `flight_control_ui/` | [Flight Control UI](https://www.figma.com/community/file/855715967691534013/) | `Container.svg` is the screen; rest are cover/README annotation |
| `dragon_interface_docking/` | [Dragon Interface](https://www.figma.com/community/file/854001679240693384/) | the DOCKING page — translation/rotation control pads, 1920x1080 |

**1299 paths, 201 circles, 342 rects, 149 lines** across the set. Real geometry, measurable and
rebuildable — not screenshots.

**THE THREE FILES THAT MATTER, and why:**
- `dashboard_ui/Frame 58.svg` — **493 paths, 26 circles, 51 lines in 0.4 MB, ZERO rasters.** The
  richest instrument page and the cleanest file in the whole set. This is the primary reference.
- `dashboard_ui/Frame 59.svg` — 0.5 MB, also zero rasters. Second reference.
- `dragon_interface_docking/Space X Interface.svg` — the docking control pads. **20.9 MB of which
  almost all is a single embedded Mars photograph**; the actual UI is only 69 paths + 8 circles.
  Strip the raster before using it.

**File size is a poor guide to value here.** The five `A-Settings-*` pages are 4.7 MB each and nearly
identical to one another — they are the same seat-settings layout with a different seat highlighted,
and the bulk is an embedded seat photograph. `Cover.svg`, `README.svg` and the three arrow-named files
are annotation from the Figma authors, not interface.

## 2. `kenney_ui_scifi/` — CC0 fallback. DOWNGRADED: the visual language is WRONG for this.

- **Source:** https://kenney.nl/assets/ui-pack-sci-fi ("UI Pack: Sci-Fi", v2.0, 19-08-2024)
- **Licence:** **CC0 1.0**. No attribution, no obligations. Kenney asks for optional credit.
- **Contents:** 742 PNG, 370 SVG, 2 TTF, 6 colourways, all 9-slice sliceable.
- **Do not build the Dragon screen out of this.** It was downloaded first as the "safe CC0 chrome
  source" and that was a misjudgement: Kenney's pack is bevelled, glossy, chunky arcade-style UI. The
  Dragon screen is the exact opposite — flat fills, 1–2 px hairlines, no bevel, no gloss, no shadow.
  Using it would have produced something that looked nothing like the reference.
- **Keep it for:** scrollbars, cursors, and any utility widget with no Dragon equivalent, where CC0
  saves an attribution line. That is a small role, and it is the right one.

## 2. `d-din/` — typography. THE SINGLE BIGGEST VISUAL WIN.

- **Source:** https://github.com/amcchord/datto-d-din (Datto Inc., 2017)
- **Licence:** **SIL OFL 1.1**. Verified in `COPYING.txt`, not assumed from the repo description.
- **Reserved Font Names: "D-DIN", "D-DIN Condensed", "D-DIN Expanded".** This is the one real
  constraint: if the font is ever MODIFIED, the derivative may not keep those names. Bundling it
  unmodified — which is the plan — is unrestricted. Ship `OFL-1.1.txt` alongside it.
- **Note for the Tundra renaming:** renaming *our UI* to Tundra branding is unaffected by this. Do not
  rename the FONT's internal family name; that is what the reserved-name clause covers.

### INSTALLED PER-USER 2026-08-05 — how, and how to undo it

Unity's `Font.CreateDynamicFontFromOSFont` only sees fonts **installed in Windows**, so the bundled
files alone were not enough. Installed for THIS USER ONLY — no admin, no system directories touched:

    files      %LOCALAPPDATA%\Microsoft\Windows\Fonts\D-DIN*.ttf
    registry   HKCU\Software\Microsoft\Windows NT\CurrentVersion\Fonts   (one value per file)

**To uninstall:** delete those files and their registry values, or use Settings → Fonts → D-DIN →
Uninstall. Nothing else on the machine was changed.

**TTF only, not the OTF duplicates.** Both formats carry the same three families, and installing both
gives Windows two files claiming the same family name.

**Family names were READ OUT OF THE FILES, not assumed** — and they match the OFL reserved names:

    D-DIN.ttf, D-DIN-Bold.ttf, D-DIN-Italic.ttf          -> family "D-DIN"
    D-DINCondensed.ttf, D-DINCondensed-Bold.ttf          -> family "D-DIN Condensed"
    D-DINExp.ttf, D-DINExp-Bold.ttf, D-DINExp-Italic.ttf -> family "D-DIN Exp"

**This is a DEVELOPMENT convenience and cannot ship.** A released mod must not ask users to install a
font by hand. The release route is MAS's bitmap-font path — a texture plus `CharacterInfo`
(`MASLoader.cs:376-474`) — which needs neither an OS install nor an AssetBundle.
- **Contents:** 9 faces, OTF + TTF (Regular / Bold / Italic, plus Condensed and Expanded families).
  Web formats (woff/woff2) were deleted — Unity cannot use them.
- This is a DIN-family face, which is the typeface class the real capsule displays use. Most of the
  "it looks right" comes from this, not from the chrome.

## 3. `reference/dragon2-ui-vue/` — LAYOUT REFERENCE ONLY. NOT AN ASSET SOURCE.

- **Source:** https://github.com/Neel-Dandiwala/SpaceX-Dragon2-UI
- **Licence:** **Apache-2.0** (GPL-3.0 compatible).
- **Use it for:** what goes on each page and how the pages are arranged. It is a Vue 3 / WebGL app —
  its visuals are CSS and shaders, so there is nothing here to lift as a texture.
- **Pruned on download** from 134 MB to 2.2 MB: the two 42 MB ISS `.glb` models, the earth/camera
  photography and the built `docs/` output were all deleted. Kept: the 8 page components in
  `src/components/`, and `misc/` which holds screenshots of the real panels.

---

## Attribution owed in the release notes (CC BY 4.0, four lines)

- SpaceX Crew Dragon Dashboard UI — Figma Community, CC BY 4.0
- SpaceX Crew Dragon Flight Control UI — Figma Community, CC BY 4.0
- SpaceX Dragon Interface (Rodrigo do Carmo) — Figma Community, CC BY 4.0
- Crew Dragon Falcon 9 (MaTte0 / @matteomansion) — Sketchfab, CC BY 4.0 — **shipped**, as the
  rendered capsule turntable (§6 below). The only one of the four whose output is in the mod's own
  art folder rather than only informing the layout, so this line is the one that cannot be dropped.

## Not used

**MUTANTdragon** (https://mutantdragon.space/) is the most complete recreation found, but no licence
could be confirmed. **Look-reference only. Do not copy anything from it** until that is settled.

---

## Trademark, separately from licence

Using Tundra Exploration naming (the user's call, 2026-08-04) is what addresses this, and it is the
right call. Two things stay true regardless:

- Do not ship the SpaceX wordmark, logo, or the "Dragon"/"Crew Dragon" name as product branding.
  Trademark is a different regime from copyright and none of the licences above touch it.
- The *visual language* — dark panel, thin DIN type, cyan accents, ring gauges — is not protectable
  and is fine to reproduce.


---

## 5. MAS / MOARdVPlus — the interactive-IVA prior art. CHECKED 2026-08-05.

Question asked: **has anyone already built an interactive IVA for the Tundra vehicles?**
Answer: **no.** Recorded here so it is not re-researched.

| checked | found |
|---|---|
| `TundraExploration/Patches/Extra_MAS.cfg` | adds `MASFlightComputer` to the V2 pods, `:NEEDS[MOARdV/AvionicsSystems]`. **No props, no MASMonitor, no MAS_PAGE, no COLLIDER_EVENT.** A hook nobody took up. |
| MAS `GameData/MOARdV/MAS_ASET/` | 1647 cfgs and prop models — but **ASET/ALCOR geometry** (MFDs, Apollo switches), needing prop packs that are not installed. Not Tundra's buttons. |
| MOARdVPlus | **Apollo / FASA CM IVAs.** Nothing to do with Dragon. Abandoned April 2023 by its own README. |
| all of `GameData`, for the Dragon screen prop | one hit: **our own** `DragonScreen.cfg` |

### ⚠ LICENCE: MOARdVPlus is CC-BY-NC-SA 4.0 — **NonCommercial, INCOMPATIBLE with GPL-3.0**

**Nothing from MOARdVPlus may be copied into this project.** Not a config, not a snippet. The
NonCommercial clause cannot be reconciled with GPL-3.0, and ShareAlike would force its licence onto
anything built on it.

**It is deliberately NOT extracted into this tree.** The zip stays in `Downloads/`, outside the
project, precisely so it cannot be copied from by accident later. Reading it to understand a
technique is fine; every line that lands in this repo must come from somewhere else.

**MAS itself is MIT and IS compatible.** That is why the render path, the text glyph technique and
the collider-event pattern are all ported from `AvionicsSystems`, with the file and line noted at
each site. Keep doing that, and keep citing it.

### What this settles for the build

Nobody supplies colliders for Tundra's button meshes, because nobody has built on them at all. They
must be created — but the MECHANISM is ported from MIT-licensed MAS, not invented. Neither mod is
installed: MAS would render nothing (Tundra ships no MAS IVA) and the project already decided
against depending on it, and MOARdVPlus is an Apollo capsule.

---

## 6. `reference/models/` — the CREW DRAGON 3D MODEL. **SHIPPED as a render.** Added 2026-09-02.

The only entry on this page whose output goes into `plugin/GameData/`. Everything above is reference
that informs geometry we then draw ourselves; this one is somebody else's model, rendered, and the
render is in the mod. So the attribution is not optional and not a courtesy.

- **Work:** "Crew Dragon Falcon 9"
- **Author:** **MaTte0** (**@matteomansion**)
- **Source:** https://sketchfab.com/3d-models/crew-dragon-falcon-9-c30dd3d60bce45c593af7e25c69638d7
- **Licence:** **CC BY 4.0** — free to use commercially and in a GPL-3.0 project, **attribution
  required**. Verified on the model page; recorded in `docs/BUILD_PLAN.md` §5 (C1) as the vetted
  candidate before it was downloaded.
- **Used for:** the capsule turntable on the VEHICLE page's Capsule camera view —
  `plugin/GameData/DragonScreen/art/cover/dragon_turn_000.png … dragon_turn_035.png`, 36 frames at
  10° of azimuth, 512×1024 RGBA with a transparent background. These files ARE the derivative work
  the licence covers.
- **Rendered by:** `plugin/build/render_turntable.py` (Blender 5.1, Cycles CPU, headless). The script
  isolates the Dragon capsule + trunk and deletes the Falcon 9, legs and Merlins before the camera is
  placed — the mod ships the spacecraft, not the launch vehicle.
- **What was NOT taken:** no mesh, texture, material or model file from this work is in
  `plugin/GameData/`. The mod ships 36 PNGs.

### The model files themselves are NOT in git

`assets/reference/` is gitignored in full (see the top of `.gitignore`) — the same position taken for
every other third-party source in this tree: republishing someone else's work inside the repo is a
separate act from using it, and it is not ours to do. The files sit on disk only:

    crew_dragon_falcon_9 (1).glb    3.4 MB, 1k textures   <- the one rendered from
    crew_dragon_falcon_9.glb        32 MB, 4k textures    <- fallback; identical at sprite size
    crew-dragon-falcon-9.zip        40 MB, FBX + textures <- fallback

**Consequence to know about:** a fresh clone can rebuild the mod but cannot re-render the turntable —
the PNGs are the tracked artefact and the script is the record of how they were made. Re-download from
the Sketchfab link above to re-run it.

### Trademark

Unchanged by this: the render shows SpaceX markings that are part of the model's textures, and the
trademark position in the section above still applies — the *product* is not branded SpaceX, and the
"Dragon"/"Crew Dragon" name is not used as product branding.
