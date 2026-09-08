# The owner's original reference art — what landed, how it was keyed, and what it is safe to do with it

**Written by `S184` (2026-09-07).** This file is the **tracked** record of an **untracked** input, and that
is the whole reason it exists: the art lives under `assets/reference/nasa/`, which `.gitignore:12` excludes
(`assets/reference/`). A clone does NOT carry these files. Same argument as
`docs/reference/FIGMA_ELEMENT_EXPORTS.md`, and the same job — **the hashes below are the recovery contract.**

⚠ **`assets/` HAS NOW BEEN LOST TWICE.** `assets/kenney_ui_scifi` went empty in run 3, and 263 of 337
`assets/figma` files vanished between 2026-09-06 and 2026-09-07. The second loss was recovered *only*
because `FIGMA_ELEMENT_EXPORTS.md` recorded per-file hashes to verify the restore against. Nothing else
protected it. **Whether that stays the arrangement is `REGISTER.md` `S183`'s question — an owner decision,
not a build one — and it is not answered here.** What is done here is the part a build chat may do: land
nothing unhashed.

## Provenance — the owner's own words

**🟢 OWNER DIRECTIVE, 2026-09-07, verbatim (C1.12's evidentiary standard):**

> *"I also have the original nasa art we can use for pages. Put these in the resources stash"*

and, on extraction quality, verbatim:

> *"I do not mind having a halo"*

The four files were supplied in `C:\Users\User\Downloads\`. Their source md5s are recorded below so a later
chat can tell whether a file it is handed is the one this manifest describes.

## What landed

```
assets/reference/nasa/                (GITIGNORED via .gitignore:12 — REFERENCE, look-don't-ship, C7.1)
  crew_dragon_outline.png             keyed from crew_dragon_outline.jpg
  crew_dragon_profile.png             keyed from crew_dragon_profile.jpg
  interface_1630x941.png              alpha preserved from Interface.png
  interface_1950x1260.png             alpha preserved from Interface (1).png
```

⛔ **NOT in `plugin/GameData/DragonScreen/art/`, deliberately.** Nothing draws this art yet, and which art
ships is decided by the unit that uses it (C7.1: `assets/` is reference; the only shippable art lives under
`plugin/GameData/DragonScreen/art/`). `S184` wired none of it to anything.

## The manifest proper

`opaque %` is the share of pixels with `alpha > 0` — quoted **against the source frame** and **against the
landed crop**, because the two answer different questions: the first says how much of the supplied image was
ink, the second says how tight the crop is. `crop @` is the top-left of the landed file in the **source's**
pixel coordinates, so any geometry measured on the original is recoverable by adding it back.

| landed file | source | source md5 | source w×h | landed w×h | crop @ | opaque % (src) | opaque % (crop) | landed md5 | bytes |
|---|---|---|---:|---:|---|---:|---:|---|---:|
| `crew_dragon_outline.png` | `crew_dragon_outline.jpg` | `d3544c29` | 3238×1692 | 1890×1326 | (700, 167) | **4.350 %** | 9.510 % | `fa57b08c` | 258,010 |
| `crew_dragon_profile.png` | `crew_dragon_profile.jpg` | `2b8decff` | 3242×1696 | 2645×1326 | (279, 167) | **6.509 %** | 10.204 % | `7deffa3b` | 547,799 |
| `interface_1630x941.png` | `Interface.png` | `f44b978c` | 1630×941 | 1601×926 | (15, 15) | **94.708 %** | 97.985 % | `f3bd735b` | 1,208,414 |
| `interface_1950x1260.png` | `Interface (1).png` | `41964d87` | 1950×1260 | 1908×1219 | (21, 21) | **93.041 %** | 98.288 % | `579622dc` | 1,529,253 |

Full landed md5s: `fa57b08cd1ddbd9b767d8a4f5abadc37` · `7deffa3bb3ec4e66cc3f3cde9be3fee1` ·
`f3bd735b42f03c3453a06ba6e2e12143` · `579622dc5fd95d36fe0428154aeff21a`.
Full source md5s: `d3544c29329018f8785ba86e089c0450` · `2b8decff49a96bf6ec51d311aea08795` ·
`f44b978c7b9904a3d5ffb5473e7061db` · `41964d87d9ec190e034897d0fd2a9c5e`.

---

## ADDED 2026-09-07 by `S185` (UNIT 3) — the Vehicle Overview's 3D render, at 2.6x the shipped resolution

🟢 **OWNER, 2026-09-07, verbatim:** *"Vehicle overview page gets the 3d render"* — and, excluding the line
art from that page, *"they are for the other page with line art like those already on it, so we can show
the rcs port locations clearer etc"*.

**What this is.** `C:\Users\User\Downloads\crew_capsule.jpg` (2667x1500, 2026-09-06 16:39) is **the same
SpaceX render the page already draws** — white capsule + ribbed trunk, SPACEX / NASA / DRAGON / US-flag
markings, same pose, same framing. The shipped `plugin/GameData/DragonScreen/art/cover/dragon_crew.png` is a
**294x468** crop of it. The subject in the source measures **771x1232**, so this is **2.62x the linear
resolution** of what ships, against a slot that draws it at 388x506 device px at 2560x1406 — i.e. the
shipped art is currently UPSCALED and this one would not be.

⛔ **AND THE KEYING RECIPE `S185` WAS HANDED WAS WRONG FOR THIS FILE — THE SAME SHAPE AS `S184`'s OWN
FINDING, ONE FILE LATER.** The instruction was *"it is on pure black -> `alpha = max(r,g,b)`, threshold ~10,
crop to bbox"*. That is right for a white-on-black line drawing and **destructive for a full-colour render
with dark regions**. Measured, not argued:

- the shipped `dragon_crew.png` has **1,605 FULLY OPAQUE pixels below luminance 40**, down to luminance 0 —
  the capsule's skirt, the window recesses and the shadowed trunk flutes. It was never luminance-keyed;
- run as directed on `crew_capsule.jpg`, the recipe makes **3,346 subject pixels fully transparent** and
  **118,217 subject pixels less than 50 % opaque**. It punches holes through the vehicle.

**So the matte was taken by CONNECTIVITY, not by luminance**: a flood fill of `luminance <= 8` inward from
the four borders marks the background; everything the fill cannot reach is the subject and is opaque.
Luminance is then used **only** in the 2 px boundary band, where a JPEG genuinely does blend the subject
into the black behind it, and the colour there is un-premultiplied so the crop composites the same on any
ground.

⭐ **AND THE KEY IS PROVEN AGAINST GROUND TRUTH RATHER THAN ASSERTED.** The shipped 294x468 asset is an
existing, correct matte of the same subject, so it is a test. Downscaled to the shipped file's own ink crop
(287x460):

| measure | result |
|---|---|
| landed crop aspect vs shipped ink aspect | **0.6258 vs 0.6239** — 0.3 % apart, so the FRAMING is the same crop |
| alpha agreement | mean abs difference **1.6 / 255**, median **0**, **98.6 %** of pixels within 24 |
| RGB agreement where both are opaque (92,541 px) | mean abs difference **4.6 / 255**, **95.6 %** within 24 (JPEG artefacts account for most of it) |

**What landed**

| landed file | source | source md5 | source w x h | landed w x h | crop @ | opaque % (src) | opaque % (crop) | landed md5 | bytes |
|---|---|---|---:|---:|---|---:|---:|---|---:|
| `dragon_crew_hi_771x1232.png` | `crew_capsule.jpg` | `403ef17b` | 2667x1500 | 771x1232 | (946, 123) | **16.813 %** | 71.087 % | `bbaeb251` | 313,719 |

Full landed md5: `bbaeb251e1fcdf78306b1b8476ac356f`. Full source md5: `403ef17b5a14897afb697da73f93a627`.
⚠ The two `opaque %` columns here measure the **connectivity matte**, not a luminance key, and are therefore
not comparable with the four rows above — the same caveat `S184` recorded for the two `Interface` files.

⛔ **NOTHING WAS SWAPPED, AND THE REASON IS A FACT THIS UNIT DISCOVERED RATHER THAN A PREFERENCE.**
`dl.Asset("dragon_crew", ...)` is called from **`VehicleOverviewPage.cs:200` AND `VehicleSubsystemPage.cs:290`**,
and the second serves the six subsystem sub-tabs — so replacing the shipped PNG changes **seven page-views**,
six of them outside unit 3's scope, and `S185`'s own verification criterion is that `previewdiff` reports
*this page's views alone*. The swap is one file copy and it is proven safe; **whose call it is, is the
owner's** (`REGISTER.md` `S185` Q1). ⛔ It is also NOT a fix for the 22.2 % horizontal stretch, which is the
SLOT's aspect and is a separate open question — a sharper source is stretched by exactly the same factor.

⛔ **AND THE OTHER `Downloads` CANDIDATES WERE CHECKED AND REJECTED, WITH REASONS**, so a later chat does not
re-open the search: `dragon_threeview.png` (2645x1326, md5 `7c8361ac`) is the three-view LINE-ART sheet — same
pixel size as this manifest's own `crew_dragon_profile.png` but a different key — and the owner excluded line
art from this page; `crew dragon with trunk.jfif` (800x1303) is a white-on-black LINE-ART elevation of this
exact view and belongs to `PropSchematic`, whose header already calls for capsule + trunk line-art;
`crew_dragon_trunk.png` (213x347) and `crew_bottom_view.png` (132x147) are LOWER resolution than what ships;
`Crew Dragon Flight Control UI.png` (2352x1410) is a UI reference, not vehicle art.

None of the four source files was already anywhere in `assets/` or `plugin/GameData/` — checked by md5
across every `.png`/`.jpg` in both trees, 0 matches each. These are new inputs, not re-drops.

## Two files were keyed. Two were NOT, and that is the finding

### The two JPEGs — keyed, exactly as specified

`crew_dragon_outline.jpg` and `crew_dragon_profile.jpg` **are** white line art on pure black, no alpha
channel at all (`RGB` mode), all four corners `(0,0,0)`. They were keyed:

- `alpha = max(r, g, b)`;
- colour forced to **white** (`255,255,255`) everywhere, so the stroke has no residual colour cast;
- `alpha <= 6` → **fully transparent**, which is what removes the JPEG ringing around the strokes —
  **32,874 px on the outline and 65,470 px on the profile** were non-zero in the source and are zero here.
  That is the halo, gone. (The owner said he did not mind one; there is none to mind.)
- cropped to the opaque bounding box, so no black margin ships.

⭐ **This reproduces the task prompt's own verification exactly** — outline **4.35 % opaque**, bbox
**(700,167)–(2590,1493)**. The table above writes that bbox as inclusive-max `(700,167)–(2589,1492)` at
`1890×1326`; it is the same rectangle, quoted with an inclusive rather than exclusive far edge.

Only **0.231 %** (outline) and **0.162 %** (profile) of the landed ink is fully opaque — these are thin
antialiased strokes, almost entirely partial alpha. **Anything compositing them must respect alpha**;
thresholding them to 1-bit would erase most of the drawing.

### ⛔ The two `Interface` PNGs — NOT keyed, because the premise does not hold for them

**`S184`'s task prompt asserted all four files were white-on-pure-black and directed that all four be keyed.
For these two that is false, and keying them was destructive, so it was not done.** Measured:

| | `Interface.png` | `Interface (1).png` |
|---|---|---|
| mode | **RGBA** — it already has an alpha channel | **RGBA** |
| alpha `== 0` | 5.29 % of pixels | 6.96 % |
| alpha `== 255` | 66.62 % | 70.83 % |
| distinct alpha values | **103** | **60** |
| dominant colour under `alpha == 255` | **`(26,28,72)`** = `#1A1C48` | **`#1A1C48`** |
| max RGB under `alpha == 0` | **255** | **255** |

They are **full-colour renders of a VEHICLE OVERVIEW page** on a dark navy ground, with a rounded card and
a drop shadow — and the transparency *is* that card's corners and shadow. Their corners read `(0,0,0)` only
because they are **transparent** there; the "pure black corners" check was made on RGB after discarding
alpha, which reports black for every transparent pixel regardless of what the file means.

Keying them would have done three separate kinds of damage:
1. **thrown away their real alpha**, which is the only thing describing the card edge and shadow;
2. **flattened `#1A1C48` and every other colour to white**, i.e. deleted the artwork;
3. **resurrected colour from under the transparent pixels** — RGB there goes up to 255, so
   `alpha = max(r,g,b)` would have made fully-transparent regions opaque. Run as directed, the key gave
   **90.8 %** and **87.9 %** "opaque" — a near-solid white haze over the whole frame. That number is what
   exposed it.

So they were landed **preserving their own RGBA verbatim**, cropped only to their own alpha bounding box
(a margin that is fully transparent in the source, so nothing is lost), with RGB zeroed under
fully-transparent pixels so the crop composites identically on any ground. The `opaque %` quoted for them
in the table is therefore *their own alpha*, not a computed key — which is why it is ~94 % rather than ~5 %.
**A later chat comparing the two pairs of percentages should read them as measuring different things.**

⚠ **AND A NAMING NOTE, kept honest under §1.4.** The owner's word for the drop was *"the original nasa
art"*. The two **JPEGs** fit that plainly — labelled engineering cutaways (`FORWARD HATCH`, `WINDOWS`,
`SIDE HATCH`, `SUPER DRACOS`, `DRACOS`, `HEATSHIELD`, `TRUNK`, `LAUNCH ESCAPE FINS`) and a three-view sheet
(capsule, capsule-with-trunk, end-on ring). **The two `Interface` PNGs are UI mock-up renders of the
reference interface**, the same family as the `assets/figma` exports, not engineering art. The directory is
named for the owner's phrase and his phrase is quoted above; **no NASA authorship is asserted for any
individual file here**, and the two kinds are not the same tier of source. Establishing what the `Interface`
renders actually are is a question for the unit that first wants one, on the `FIGMA_ELEMENT_EXPORTS.md`
model — named as a question, not closed.

## What this input may be used for

Same rule as the figma exports: **an element present here is built FROM here** if a unit chooses to use it,
and **an element absent from here stays exactly as it is** — its absence is never grounds for changing or
removing anything. Shippable art still goes only to `plugin/GameData/DragonScreen/art/`.

⛔ **Nothing uses this art today.** `S184` landed it and recorded it; no page, no `pure/` file and no cfg
references it, and none was changed.

## How to re-key from source, if these are ever lost

```python
# the two JPEGs only — NOT the Interface PNGs, see above
from PIL import Image
import numpy as np
rgb   = np.asarray(Image.open(src).convert("RGB"))
alpha = rgb.max(axis=2).astype(np.uint8)
alpha[alpha <= 6] = 0                       # kills the JPEG ringing
ys, xs = np.nonzero(alpha)
x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
out = np.zeros((y1-y0+1, x1-x0+1, 4), np.uint8)
out[:, :, 0:3] = 255                        # colour forced to white
out[:, :, 3]   = alpha[y0:y1+1, x0:x1+1]
Image.fromarray(out, "RGBA").save(dst, optimize=True)
```

The `Interface` PNGs are restored by copying the source and cropping to `Image.getbbox()` — no key.

---

## ADDED 2026-09-07 by `S192` — a THIRD render of this page, and the eight tab icons harvested from it

🟢 **OWNER, 2026-09-07, verbatim**, on the first render of the rebuilt subsystem tab strip:

> *"that strip looks shit. Harvest the icons as asset from the example screen"*

**Why a third `Interface` file.** `S184` landed two renders of the Vehicle Overview page. There is a
**third** in `Downloads` and it is the largest: `Crew Dragon Flight Control UI.png`, 2352x1410, with **no
outer bezel**, so its usable card is **2352 px wide against `interface_1950x1260.png`'s 1708** — 1.38x the
resolution. That put the tab icons at **40-45 px instead of 30**, which is why the harvest was taken from
this file and not from the one already landed. Stored verbatim as RGB (its alpha channel is uniformly
opaque, so nothing is lost).

| landed file | source | source md5 | source w x h | landed w x h | landed md5 | bytes |
|---|---|---|---:|---:|---|---:|
| `interface_2352x1410.png` | `Crew Dragon Flight Control UI.png` | `0eeec498` | 2352x1410 | 2352x1410 | `52898b30` | 3,632,869 |

Full source md5: `0eeec498c1aa44c9bdf9c5791c87eb4b`. Full landed md5: `52898b3080fb3ec57a14a258a826713a`.

### The eight icons — and these SHIP, which none of the art above does

⛔ **THIS IS THE FIRST ENTRY IN THIS MANIFEST THAT GOES TO `plugin/GameData/DragonScreen/art/`.** `S184`
recorded the rule and the reason it did not apply to it: *"shipped art is chosen by the unit that uses
it"*. `S192` is that unit, on the owner's direct instruction, so these eight are shipped assets and are
tracked by git like every other file under `art/cover/`. The manifest row is here anyway, because the
SOURCE they were cut from is not tracked and without it nobody could re-cut them.

**How they were keyed.** Each icon sits on the tab panel's uniform ground `(26,28,72)`. Alpha is the
per-channel excess over that ground normalised to white, then **rescaled so each icon's own 99th
percentile reaches full alpha** — without that step the two icons the source draws in RED (Overview and
Life) would have keyed out at about 80 % and rendered visibly faded next to the white ones. RGB is then
forced to **white**, because `VehicleTabBar` tints every icon to its tab's own colour at draw time (T5
severity), exactly as `ic_check` is already drawn.

⚠ **SQUARE CANVASES, AND THAT IS QC `C-04`.** The source glyphs are not square — Power is 21x40, Avionics
45x44 — so each is centred on a square canvas of its own longer side and drawn with the same scalar on
both axes. A glyph-bearing asset that is only ever drawn square cannot be stretched.

⚠ ⛔ **SUPERSEDED IN PLACE 2026-09-09 (S243, owner ruling). THE PARAGRAPH BELOW IS KEPT VERBATIM BECAUSE
C1.16 FORBIDS DELETING IT, AND BOTH OF ITS CLAIMS ARE NOW WRONG.** It read:

> ⚠ **The source strip has NINE tabs and this build's has EIGHT.** Its Overview/Life/Comms map onto this
> build's All/Crew, so `ic_tab_all` is its rocket and `ic_tab_crew` its person; **its Comms wifi glyph was
> NOT harvested** — there is no Comms tab to put it on, and T9's eight tabs are confirmed-real and are not
> changed to suit an icon.

⛔ **WHAT IS WRONG WITH IT, ON BOTH COUNTS:**
1. **"T9's eight tabs are confirmed-real … and are not changed to suit an icon"** — the OWNER HAS
   OVERRIDDEN this. The rebuild's strip has **NINE** tabs, and the sheet's own tab row shows nine
   clusters with the ninth captioned "Comms". The eight-tab claim described this build, not the source,
   and was never a statement about reality.
2. **"its Comms wifi glyph was NOT harvested"** — ⭐ **it was.** The overseer cut it on 2026-09-08; it
   simply never reached `plugin/GameData/`, so the repo looked like the glyph did not exist. It is
   shipped by S243 and is the last row of the table below.

⚠ **The distinction is worth keeping**: this paragraph was TRUE ABOUT THE REPOSITORY when written and
FALSE AS A STATEMENT OF FACT, which is exactly the failure mode C1.16 exists to make visible rather than
tidy away.

| shipped file | our tab | source rect (x, y, w x h) | canvas | ink % | md5 | bytes |
|---|---|---|---:|---:|---|---:|
| `ic_tab_all.png` | All | (712, 1309) 34x41 | 47x47 | 43.3 % | `16012267` | 585 |
| `ic_tab_crew.png` | Crew | (833, 1309) 32x40 | 46x46 | 21.6 % | `f808a942` | 350 |
| `ic_tab_prop.png` | Prop | (1059, 1309) 30x39 | 45x45 | 28.1 % | `45608613` | 741 |
| `ic_tab_mech.png` | Mech | (1155, 1308) 40x40 | 46x46 | 31.7 % | `e1a8111b` | 812 |
| `ic_tab_power.png` | Power | (1272, 1309) 21x40 | 46x46 | 33.6 % | `56216a93` | 383 |
| `ic_tab_avionics.png` | Avionics | (1377, 1307) 45x44 | 51x51 | 36.1 % | `9ca12838` | 518 |
| `ic_tab_gnc.png` | GNC | (1491, 1307) 45x44 | 51x51 | 21.6 % | `d097eb45` | 731 |
| `ic_tab_thermal.png` | Thermal | (1606, 1308) 43x42 | 49x49 | 42.5 % | `f489929f` | 578 |
| `ic_tab_comms.png` | Comms | (940, 1309) 42x42 | 48x48 | 44.8 % | `493b93b1` | 802 |

⭐ **The ninth row was added by S243, 2026-09-09.** Cut by the same recipe as the other eight —
tab ground `(26,28,72)`; alpha = per-channel excess over ground / (255 − ground), max channel,
threshold **0.20** for the ink rect; rescaled so the icon's own 99th percentile hits full alpha;
RGB forced white; centred on a square canvas of **max(w,h) + 6**. ⭐ That recipe was PROVED by
re-cutting `mech`, `crew` and `thermal` **bit-exact** before it was trusted for a ninth.

⚠ **They are UPSCALED on the glass and the number is here rather than hidden:** drawn at 84 design units
on a 2112-unit frame, they render at **56 device px at the shipped 2560x1406** against a 45-51 px canvas —
about **1.15x**. That is the best available from any render of this page in the repository, and it is far
better than the 1.87x the smaller file would have given.

---

## ⭐⭐ THE TEN BASE-SCREEN BAR TILES (S245, 2026-09-09) — harvested, not cut here

⛔ **These are NOT the shipped `bar_*` set, and the difference is the whole reason they exist.**
`bar_nav_0..4`, `bar_label_current_state`, `bar_label_pointing_mode`, `bar_value_pointing_mode` and
`bar_comm_block` — nine of the eleven tiles the OLD bar draws — are **FULLY OPAQUE with `#111B52`
baked in**, 83,746 design px² of it, 10.4 % of that bar's box. ⛔ **A tint cannot repair one:** a tint
is a MULTIPLY in both renderers (`PreviewMain.TintedAsset`'s ColorMatrix and `ScreenPainter`'s
`GL.Color` modulation), so it can only DARKEN, and `#111B52` → `#1A1F35` needs the **red channel to
RISE**. ⭐ That measurement is this build's own, made in the discarded S244 working copy; the
base-screen spec §8.2 carries it and credits it, and the re-cuts below exist because of it.

**Provenance.** Cut by the overseer from the CLEAN `Component 48.png` export (⛔ **not** the shipped
copy, which carries S147's erase), crop rows 132..232, keyed off the `#111B52` ground by
**un-compositing with alpha recovery** — `alpha = (c − bg) / (255 − bg)` per channel, max channel —
**verified lossless, max error 1/255**. ⭐ They were supplied beside the spec in `Desktop/BOB/bar_assets/`
and **harvested byte-identical** into `art/cover/` by S245; the md5 column below is of the shipped copy
and was compared against the supplied file, all ten identical.

⭐ **VERIFIED ON HARVEST, EVERY FILE, AND THE STOP CONDITION WAS NOT MET.** The prompt's refusal
condition was *"if any of them carries a baked background, STOP"*. None does: **all four corners of all
ten files are `(0,0,0,0)`**, 45–86 % of each file is FULLY TRANSPARENT, and **not one opaque pixel in
any of the ten is within 30 (sum-of-channels) of `#111B52`**. They composite correctly on `#1A1F35`.

⚠ **ONE CORRECTION TO THE PROMPT'S WORDING, MEASURED RATHER THAN ASSUMED.** It said the tiles must be
*"alpha-varying with near-white opaque pixels"*. The first half holds exactly — the "alpha-varying"
share below is 56.4–89.5 %, which is §8.2's *"55–89 %"* under the reading "not fully opaque". ⛔ The
second half is **false for two of the ten**: `r_spx` and `r_iss` have opaque pixels averaging
rgb(31,237,243) and rgb(70,238,243) — **CYAN**, because the SPX/ISS badges and the countdown block are
cyan ink in the design itself. ⭐ Confirmed by eye on a composite over `#1A1F35` before it was accepted:
it is artwork, not residue. ⛔ It is NOT a baked background, so it is not the stop condition — recorded
here so nobody "fixes" the cyan to white later.

| shipped file | design left, top | design w x h | canvas | alpha-varying | opaque ink | md5 | bytes |
|---|---|---|---:|---:|---|---|---:|
| `b_nav0.png` | 13.4, 997.4 | 69.5 x 56.6 | 124x101 | 76.7 % | near-white | `7ba35873` | 7660 |
| `b_nav1.png` | 102.0, 997.4 | 35.9 x 56.6 | 64x101 | 83.5 % | near-white | `668af6c0` | 1661 |
| `b_nav2.png` | 176.5, 997.4 | 28.0 x 56.6 | 50x101 | 74.7 % | near-white | `9e471983` | 1372 |
| `b_nav3.png` | 243.2, 997.4 | 40.3 x 56.6 | 72x101 | 56.4 % | near-white | `3233ef39` | 1125 |
| `b_nav4.png` | 316.0, 997.4 | 38.1 x 56.6 | 68x101 | 79.0 % | near-white | `289a272c` | 2151 |
| `b_state.png` | 615.2, 997.4 | 192.7 x 56.6 | 344x101 | 89.5 % | near-white | `d9b8488a` | 6340 |
| `b_point.png` | 1103.1, 997.4 | 85.2 x 56.6 | 152x101 | 87.7 % | near-white | `918ebae9` | 3897 |
| `r_spx.png` | 1429.2, 998.5 | 191.2 x 35.9 | 272x51 | 81.0 % | ⚠ **cyan** | `0c5bea0f` | 5666 |
| `r_iss.png` | 1669.0, 998.5 | 94.2 x 35.9 | 134x51 | 83.5 % | ⚠ **cyan** | `e9d4b6b1` | 3381 |
| `r_count.png` | 1807.5, 998.5 | 99.1 x 35.9 | 141x51 | 86.3 % | near-white | `84cef3a8` | 2616 |

⭐ **THEY ARE DOWNSCALED ON THE GLASS, AND THE NUMBER IS HERE RATHER THAN HIDDEN** — the opposite of
the tab icons above, which are upscaled. `b_nav0` is 124 px wide and is drawn at 69.5 design units on a
1920 frame, which is **92.6 device px at the shipped 2560x1405** — about **0.75x**, so the artwork has
pixels to spare at every size the mod ships. `r_spx` is 272 px drawn at 254.9 device px, **0.94x**.
⚠ Each file's own aspect is asserted against the box §8.1 draws it into (`BasePageNoIconTest`, read out
of the PNG's IHDR): a transposed width and height would place the tile correctly and stretch the art,
which no position check can see.

⛔ **`b_nav0` CARRIES THE NAV SELECTION MARKER BAKED IN** — a solid `#FFFFFF` bar at design left 17.3,
width 61.7, top 1043.9, height 10.1, flush to the frame's bottom edge at 1054. ⭐ For the SHELL it is
STATIC, under nav 0, exactly as baked: the base screens have no navigation wired, so there is nothing
for it to follow. ⚠ **KNOWN FUTURE WORK:** when navigation is wired the marker must come OUT of the tile
and become a drawn `Rect` at that geometry, with the DRAW rectangle and the HIT rectangle from **one
expression**, so neither can move without the other.
