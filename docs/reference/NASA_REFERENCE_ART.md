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
