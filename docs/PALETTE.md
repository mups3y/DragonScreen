# Dragon screen palette — VERIFIED, not eyeballed

Extracted 2026-08-04 by counting `fill=`/`stroke=` attributes across the nine exported Figma SVGs,
then **cross-checked against a completely independent source**: the hex literals in the Apache-2.0
Vue recreation (`assets/reference/dragon2-ui-vue`). The two agree exactly on every structural colour.
That agreement is why these numbers can be trusted — one source could have been a designer's
approximation; two arriving at `#020738` and `#20FBFD` independently cannot.

| Role | Hex | SVG uses | Confirmed by code recreation |
|---|---|---|---|
| Panel / card fill | `#111B52` | 412 | yes |
| Primary accent (cyan) | `#20FBFD` | 172 | yes |
| Screen background | `#020738` | 74 | yes |
| Hairline / divider | `#313D7B` | 3 | yes |
| Caution amber | `#FFB74B` | 3 | yes |
| Go / nominal green | `#1FE327` | 7 | (`#2AFF00` in code — near, not identical) |
| Secondary cyan | — | — | `#24D2FD` (code only, used for links/secondary) |
| Alarm red | — | — | `#FF0000` / `#D12C30` (code only) |

## Text greys, brightest to dimmest

`#F3F3F3` → `#E8EBFF` → `#DAE7FA` → `#C1C3DF` → `#B2B8DE` → `#A6ABC9` → `#8489A3` → `#585D7C` →
`#515670`

That ladder is the thing to copy most carefully. The Dragon look is not "cyan on navy" — it is a
**tightly graded set of blue-greys on near-black navy, with cyan used sparingly for the one value that
matters right now.** Overusing `#20FBFD` is the fastest way to make it look wrong.

## Near-black variants

`#10102C`, `#0E0D2C`, `#0D0D29` — used for insets and wells, all slightly darker and less blue than the
`#020738` ground. Worth reproducing; they are what gives the panels depth without any bevel or shadow.

## Notes for the Unity implementation

- **No gradients, no bevels, no drop shadows** in the source. Every surface is flat fill plus a 1–2 px
  hairline stroke. This is why the Kenney sci-fi pack was the wrong starting point — its whole visual
  language is gloss and bevel.
- Stroke weight in the source frames is **2 px at 3427 px wide**, i.e. hairlines. At our panel width
  (~670 px) that is sub-pixel — strokes must be 1 px and snapped, or the whole thing turns to mush.
- Text is **outlined to paths** in the SVG export, so strings cannot be extracted from it. That does not
  matter: we set type in D-DIN (`assets/d-din/`), which is the same DIN family the source uses.
