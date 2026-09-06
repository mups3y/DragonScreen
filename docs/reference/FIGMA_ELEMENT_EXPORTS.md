# The per-element Figma exports — what landed, where, and what it is safe to do with it

**Written by `S175` (2026-09-06), step 0 of the owner's per-page rebuild programme.** This file is the
**tracked** record of an **untracked** input, and that is the whole reason it exists: the exports live under
`assets/figma/`, which `.gitignore` excludes as *"exports of the REFERENCE UI's design, not ours to
republish"*. A clone therefore does NOT carry them. Without this manifest the tree would hold 319 files that
no committed artefact names, and the next chat could neither confirm the set was complete nor tell which
element belongs to which page. C1.16's argument applied to an input rather than a document.

## Provenance — the owner's own words

**🟢 OWNER DIRECTIVE, 2026-09-06, verbatim (C1.12's evidentiary standard):** *"I want a prompt to completely
rebuild each page correctly one at a time. Build it then show me preview I will either approve it or as for
more edits. Only then do we move onto the next page."*

The owner supplied seven zips on 2026-09-06 — six in `Downloads`, one on the `Desktop`. **The Desktop zip is
a bundle, not a seventh export:** it contains the six Downloads zips verbatim plus the nine frame SVGs and
five whole-frame PNGs. The six Downloads zips are **four distinct element sets, each exported twice** — the
duplicate pairs differ only in which navigation tab `Component 48.png` shows as active.

| set | source zips | files | what the set is |
|---|---|---:|---|
| `cover` | `(2).zip`, `.zip` | 75 | **Frame 67, the Cover.** Hard attribution: **68 of its 72 top-level names, snake-cased, are already shipped** in `plugin/GameData/DragonScreen/art/cover/`. This is the export the Cover art was cut from. |
| `procedure_vrio` | `(1).zip`, `(5).zip` | 51 | The VRIO-LED / fluid-loading procedure steps (`1. Thermal pre-chill` … `4.700 - Deorbit Preparation`). |
| `hud_numerals` | `(3).zip` | 145 | Short numeric readouts and `Union` / `Vector` path fragments — an instrument page's parts. |
| `settings_displays` | `(4).zip` | 48 | `Cabin Displays`, `- Display 1..3` and the seat/cabin settings elements. |

⚠ **ONLY `cover` HAS HARD ATTRIBUTION TO A NAMED FRAME.** The other three set names are read off their own
contents, not proved against a frame. `hud_numerals` is *probably* Frame 58 and `procedure_vrio` *probably*
Frame 66, but the frame SVGs render all text as paths, so no string match can confirm it and **none was
manufactured**. The unit that first needs one of those three sets should establish the mapping and record it
here. Named as a question, not closed: see `REGISTER.md` S175-Q1.

## Layout in the tree

```
assets/figma/                       (GITIGNORED — reference, look-don't-ship, C7.1)
  dashboard_ui/                     the nine frame SVGs (pre-existing, untouched)
  elements/cover/                   75 per-element PNGs
  elements/procedure_vrio/          51
  elements/hud_numerals/           145
  elements/settings_displays/       48
  frames/                           Frame 59, Frame 59 (1), Frame 66, Frame 67 — whole-frame rasters
  component_48_variants/            all five distinct Component 48 exports, tagged by source zip
```

**319 element PNGs, 300 unique basenames** — a name repeats across sets when the same component appears on
more than one page (`Component 48`, `Union`, `Rectangle 177`…). **The sets are kept apart for that reason:
flattening them into one directory would silently collide 19 differently-drawn elements.**

Two shape notes, both from the exporter rather than from us:
- **Four elements are named with a `/`**, which the zip encoded as a directory (`600°/m altitude rate`,
  `Deorbit, entry and landing Go/No-Go`, `inertial velocity 7.69km/s`, `-0.031 m/s`). They are landed with
  the slash written ` ~ `, e.g. `inertial velocity 7.69km ~ s.png`.
- **One name is too long for a Windows path** (220 chars, the VRIO LED note in `procedure_vrio/`) and is
  truncated at the tail. Its full original name is
  `Note_ LED operation begins at the start of entry sequence. Any light flashing indicates automated chute deployment is available - VRIO 1 LED - FC connected - VRIO 2 LED - Ready for automated backup if FC disconnected.png`.

## What this input may be used for

§14.2a (`docs/BUILD_PLAN.md`, written by `G13`) governs. In short: **an element present here is built FROM
here** — sliced from its own PNG or drawn from its own path, and layered; **an element absent from here stays
exactly as it is**, and its absence is never grounds for changing or removing it. Shippable art still goes
only to `plugin/GameData/DragonScreen/art/`, which is where the Figma-derived art already lives.

## `component_48.png` — the swap `S175` made, and the one cut that must survive any re-export

The bar was the one asset the owner named for immediate replacement. **Five distinct `Component 48.png`
exports exist; they are the same art with the active-tab marker under a different icon**, and exactly one has
no marker at all. Measured, not asserted — non-ground pixels in the marker band (`y 207..232`), per icon:

| variant | icon 0 | icon 1 | icon 2 | icon 3 | icon 4 | |
|---|---:|---:|---:|---:|---:|---|
| `[z1]` (= `(1).zip`, `(5).zip`) | 0 | 0 | 0 | 0 | 0 | ⭐ **marker-free — the one shipped** |
| `[z2]` (= `(2).zip`) | 3475 | 431 | 0 | 0 | 0 | marker under icon 0 |
| `[z3]` (= `(3).zip`) | 1639 | 3464 | 425 | 0 | 0 | marker under icon 1 |
| `[z6]` (= `.zip`) | 3475 | 431 | 0 | 0 | 0 | marker under icon 0 |
| `[z7]` (bundle) | 0 | 0 | 0 | 1644 | 3475 | marker under icon 4 |

⚠ **THE PROMPT'S FIGURES DID NOT REPRODUCE, AND THE MEASURED ONES ARE USED INSTEAD.** The task prompt said
*"three of the seven fresh exports have 31"* residual glow pixels against 466 shipped. **Measured here: there
is ONE clean variant, not three, and it has 0, not 31; the file it replaced carried 707, not 466.** Both
numbers are counts of pixels differing from the bar's ground `#111B52` at tolerance 2 — the shipped figure is
*pixels that are ink in the old file and ground in the clean export*, i.e. the smudge itself. The direction of
the finding is unchanged and the swap is the same swap; only the arithmetic is this file's own.

⛔ **THE SHIPPED PNG IS NOT THE RAW EXPORT AND MUST NOT BECOME IT.** The raw export still carries the baked
sentence **"Far Field Pointing Deorbit"** in the CURRENT STATE value box — a frozen literal that 21 pages
showed whatever the vehicle was doing, and which `S147` cut out so `BottomBar.Draw` could type the live phase
over it. The shipped file is **the clean export with that one box re-cut**, at `S147`'s own measured
coordinates **x 1098..1461, y 170..208**, filled with the bar's ground colour.

Verified after the swap, and these three checks are the definition of it being correct:
- **731 pixels changed against the old shipped file, all within `x 36..136, y 188..199`** — under icon 0,
  and nowhere else. **707 of them went from ink to ground**: the smudge, gone.
- **The only difference from the raw export is inside the `S147` box** — 3666 pixels, all of them the baked
  sentence. Nothing else in the export was altered.
- The caption band (`y 143..158`, 1248 px of ink) and the vertical rule (`x 1462..1469`, 150 px) are
  untouched, as `S147` required.

---

# Full element index

The tables below are the manifest proper: every landed file, its pixel size, and a short md5 so a later chat
can tell whether the copy in its tree is the one this file describes.

### `cover/` — 75 elements

| element PNG | w | h | md5 |
|---|---:|---:|---|
| `1 Monitor slow to free-flight altitude (Sun+GEO pointing).png` | 1061 | 40 | `7221406a` |
| `2 After SpaceX GO for deorbit, verify entry is enabled_.png` | 1009 | 39 | `f007a41d` |
| `3 After entry is enabled, Dragon transitions to Claw.png` | 968 | 39 | `e4f7836e` |
| `30° sustained altitude error.png` | 411 | 24 | `62b43a96` |
| `600° ~ m altitude rate.png` | 315 | 24 | `6a28a986` |
| `Acknowledge.png` | 211 | 31 | `8db70d59` |
| `Claw Separati....png` | 132 | 61 | `4d9fe479` |
| `Coast to Trunk Jettison.png` | 561 | 39 | `a3e98043` |
| `Coast to Trunk....png` | 112 | 55 | `3edcdfc0` |
| `Component 48.png` | 3427 | 235 | `9216b384` |
| `Crew Deorbit Preparation.png` | 484 | 38 | `c662b612` |
| `Crew Interrupt Conditions.png` | 494 | 38 | `430b13de` |
| `Deorbit Burn Brief.png` | 275 | 24 | `5217296e` |
| `Deorbit burn - -3 hrs.png` | 305 | 24 | `1d8ded7c` |
| `Deorbit, entry and landing Go ~ No-Go.png` | 561 | 31 | `e9614989` |
| `Deport & burn.png` | 120 | 55 | `f69c4eae` |
| `ENTRY ENABLED.png` | 192 | 18 | `612ce3a1` |
| `FAR FIELD POINTING-1.png` | 313 | 24 | `3640b292` |
| `FAR FIELD POINTING.png` | 313 | 24 | `3640b292` |
| `False.png` | 78 | 24 | `e1da4dc5` |
| `Group 65.png` | 1600 | 1600 | `79ab4829` |
| `Line 85.png` | 1052 | 2 | `93295ce5` |
| `Line 86.png` | 324 | 2 | `d87e9b2d` |
| `Line 87.png` | 420 | 2 | `ce0039c3` |
| `Line 88.png` | 1052 | 2 | `93295ce5` |
| `Line 89.png` | 155 | 2 | `fe3a797f` |
| `Line 90.png` | 434 | 2 | `54bcfc3f` |
| `Line 91.png` | 254 | 2 | `9ed4db90` |
| `Line 92.png` | 282 | 2 | `a3fbd821` |
| `Line 93.png` | 1076 | 2 | `c57c57b5` |
| `Line 94.png` | 1076 | 2 | `c57c57b5` |
| `Manual Chute....png` | 101 | 56 | `9ca22ff0` |
| `NLT Deorbit Burn - 1 hr.png` | 340 | 24 | `5606b013` |
| `NLT Deorbit Burn - 30 min.png` | 390 | 24 | `f9b5d197` |
| `On SpaceX, On, begin procedure 4.700.png` | 589 | 31 | `92e3aa3c` |
| `Procedure.png` | 140 | 22 | `b7b64e02` |
| `Rectangle 169.png` | 154 | 154 | `9f38b2c8` |
| `Rectangle 173.png` | 3427 | 219 | `9127d34b` |
| `Rectangle 174.png` | 401 | 111 | `f8c18845` |
| `Rectangle 176.png` | 110 | 110 | `2ac48de9` |
| `Rectangle 177.png` | 110 | 110 | `aca634c4` |
| `Rectangle 178.png` | 1224 | 1779 | `042de737` |
| `Rectangle 179.png` | 1187 | 317 | `83082fd1` |
| `Rectangle 180.png` | 1187 | 449 | `2a93f828` |
| `Rectangle 181.png` | 1187 | 550 | `a382aa34` |
| `Rectangle 182.png` | 15 | 920 | `50962e44` |
| `Rectangle 183.png` | 178 | 150 | `ec9656ec` |
| `Rectangle 95.png` | 162 | 8 | `0c743b3f` |
| `Review Reference Content.png` | 403 | 24 | `1656d7ad` |
| `SETTINGS.png` | 138 | 21 | `530f306a` |
| `True.png` | 50 | 18 | `faafa592` |
| `Union-1.png` | 24 | 24 | `70944016` |
| `Union-2.png` | 24 | 24 | `70944016` |
| `Union-3.png` | 24 | 24 | `70944016` |
| `Union-4.png` | 24 | 24 | `70944016` |
| `Union-5.png` | 24 | 24 | `70944016` |
| `Union-6.png` | 36 | 36 | `988c63ed` |
| `Union.png` | 36 | 36 | `62aabc6c` |
| `active phase Deorbit Coast.png` | 263 | 71 | `8308e72c` |
| `altitude 393.3km.png` | 208 | 91 | `d9d212eb` |
| `apogee 416.2km.png` | 198 | 91 | `e8ff48ad` |
| `bi_arrow-right-short.png` | 16 | 16 | `2feb89dd` |
| `camera Auto - Earth IO.png` | 199 | 51 | `74c08457` |
| `eva_menu-fill.png` | 56 | 56 | `36e912f9` |
| `gridicons_refresh.png` | 55 | 55 | `559a2c9b` |
| `ic_sharp-arrow-back-1.png` | 48 | 48 | `5cfca5f8` |
| `ic_sharp-arrow-back.png` | 48 | 48 | `5ae4fbe6` |
| `ic_sharp-subtract.png` | 56 | 56 | `2fb4cfc1` |
| `inclination 51.62°.png` | 178 | 91 | `88e461c0` |
| `inertial velocity 7.69km ~ s.png` | 264 | 91 | `621bd662` |
| `perigee 379.4km.png` | 198 | 91 | `913c69f6` |
| `running 00_22_57.png` | 149 | 63 | `669099ca` |
| `splashdown time T-01_24_51.png` | 301 | 91 | `86045aab` |
| `target latitude 26° 15.00° N.png` | 250 | 71 | `30f0b658` |
| `target longitude 26° 15.00° N.png` | 279 | 71 | `e00c83a1` |

### `procedure_vrio/` — 51 elements

| element PNG | w | h | md5 |
|---|---:|---:|---|
| `1. Thermal pre-chill.png` | 266 | 18 | `0fa0461d` |
| `2. Begin Fluid loading.png` | 285 | 18 | `4ba98d23` |
| `3. Store items.png` | 179 | 18 | `8d0e3949` |
| `4. test vrio health leds.png` | 309 | 18 | `7240a97d` |
| `4.1 Command_.png` | 267 | 30 | `ea3d591b` |
| `4.2 Command_.png` | 272 | 30 | `54475ba0` |
| `4.3 Verify functionality of VRIO health LEDs (left side of command panel).png` | 1373 | 40 | `5f5e7a73` |
| `4.4 Contact SpaceX to report LED status.png` | 760 | 38 | `ef2cef48` |
| `4.5 Command_.png` | 270 | 30 | `dc76d0c9` |
| `4.700 - Deorbit Preparation.png` | 462 | 147 | `5a9edb4b` |
| `5. complete fluid loading.png` | 339 | 18 | `7288abd3` |
| `Component 48.png` | 3427 | 235 | `5868d544` |
| `Ellipse 110.png` | 42 | 42 | `ae27d37e` |
| `Line 100.png` | 1672 | 2 | `6044ba46` |
| `Line 101.png` | 1672 | 2 | `6044ba46` |
| `Line 93.png` | 605 | 2 | `a4ab4ea3` |
| `Line 94.png` | 605 | 2 | `a4ab4ea3` |
| `Line 95.png` | 605 | 2 | `a4ab4ea3` |
| `Line 96.png` | 605 | 2 | `a4ab4ea3` |
| `Line 97.png` | 605 | 2 | `a4ab4ea3` |
| `Line 98.png` | 605 | 2 | `a4ab4ea3` |
| `Line 99.png` | 605 | 2 | `a4ab4ea3` |
| `Note_ Each VRIO LED is zero fault tolerant. This test ensures prior awareness of a malfunction..png` | 553 | 111 | `4e42bde1` |
| `Note_ LED operation begins at the start of entry sequence. Any light flashing indicates automated chute deployment is available - VRIO 1 LED - FC connected - VRIO 2 LED - Rea.png` | 553 | 252 | `2eb580bb` |
| `Rectangle 138.png` | 447 | 106 | `b4998fd1` |
| `Rectangle 144.png` | 157 | 51 | `a9e4ef3f` |
| `Rectangle 177.png` | 110 | 110 | `e79e1c35` |
| `Rectangle 178.png` | 827 | 1929 | `7bbd9db5` |
| `Rectangle 179.png` | 2516 | 1929 | `8c7f155c` |
| `Rectangle 185.png` | 625 | 202 | `50cf4935` |
| `Rectangle 186.png` | 20 | 867 | `1818eec4` |
| `Rectangle 187.png` | 447 | 106 | `b4998fd1` |
| `Rectangle 188.png` | 433 | 106 | `b5a70593` |
| `Rectangle 189.png` | 274 | 106 | `9e1ad90f` |
| `Rectangle 190.png` | 625 | 353 | `fcc0317b` |
| `Test VRIO Health LEDs.png` | 766 | 54 | `6032bfd1` |
| `Vector-1.png` | 42 | 42 | `c6522bbf` |
| `Vector-2.png` | 42 | 42 | `c6522bbf` |
| `Vector-3.png` | 42 | 42 | `c6522bbf` |
| `Vector.png` | 42 | 42 | `c6522bbf` |
| `bytesize_eye.png` | 48 | 48 | `9c3670b2` |
| `deorbit.png` | 136 | 24 | `6a123899` |
| `enter read-only.png` | 220 | 18 | `64fcae1e` |
| `heroicons-solid_view-grid-1.png` | 28 | 28 | `042aaff7` |
| `heroicons-solid_view-grid-2.png` | 28 | 28 | `042aaff7` |
| `heroicons-solid_view-grid.png` | 28 | 28 | `042aaff7` |
| `next.png` | 72 | 21 | `882b17ce` |
| `section 4_ in progress.png` | 389 | 24 | `5fd2232b` |
| `start vrio 1 led test.png` | 317 | 21 | `8cb8f7be` |
| `start vrio 2 led test.png` | 320 | 21 | `10a4de00` |
| `stop vrio 2 led test.png` | 305 | 21 | `9d4db834` |

### `hud_numerals/` — 145 elements

| element PNG | w | h | md5 |
|---|---:|---:|---|
| `-0.031 m ~ s.png` | 111 | 17 | `e20162c4` |
| `0-1.png` | 21 | 20 | `f1160e6b` |
| `0.png` | 21 | 20 | `f1160e6b` |
| `0s.png` | 73 | 47 | `85b2b75f` |
| `104.png` | 47 | 51 | `5d025dfd` |
| `116.png` | 44 | 47 | `9b72e26c` |
| `118.png` | 45 | 46 | `e17e26db` |
| `12-1.png` | 26 | 27 | `774871d5` |
| `12.0 m.png` | 69 | 17 | `326fbd24` |
| `12.png` | 26 | 27 | `774871d5` |
| `14.png` | 35 | 35 | `88aaa6dd` |
| `15-1.png` | 26 | 23 | `18ffe3bf` |
| `15.png` | 26 | 23 | `18ffe3bf` |
| `16.png` | 36 | 36 | `2487f149` |
| `18-1.png` | 22 | 16 | `003f9a2f` |
| `18.png` | 19 | 14 | `4d77795b` |
| `200.0 m.png` | 83 | 17 | `ef4a7ffa` |
| `202.6 m.png` | 79 | 17 | `80e9dece` |
| `28.png` | 38 | 38 | `7b881fb2` |
| `3-1.png` | 18 | 14 | `86848f58` |
| `3.png` | 18 | 14 | `86848f58` |
| `30.0 m.png` | 73 | 17 | `29605256` |
| `36.png` | 38 | 38 | `f69e544c` |
| `44.png` | 37 | 37 | `eed13a2e` |
| `48.png` | 38 | 38 | `06d7acec` |
| `6-1.png` | 18 | 14 | `45c127a9` |
| `6.png` | 18 | 14 | `45c127a9` |
| `84.png` | 37 | 37 | `53835a19` |
| `88.png` | 38 | 38 | `c37ea566` |
| `9-1.png` | 19 | 16 | `0b55b64f` |
| `9.png` | 19 | 18 | `e58902de` |
| `96.png` | 37 | 39 | `8f8389cd` |
| `Component 48.png` | 3427 | 235 | `5ea2c53e` |
| `Ellipse 41.png` | 96 | 96 | `881159d4` |
| `Ellipse 44.png` | 463 | 463 | `4ca75362` |
| `Ellipse 52.png` | 463 | 463 | `4ca75362` |
| `Ellipse 53.png` | 463 | 463 | `4ca75362` |
| `Ellipse 54.png` | 463 | 463 | `4ca75362` |
| `Ellipse 55.png` | 280 | 280 | `d1f41e1a` |
| `Ellipse 56.png` | 389 | 389 | `d0b243e3` |
| `Ellipse 58.png` | 307 | 333 | `9ee795d0` |
| `Ellipse 6.png` | 1681 | 1681 | `35c5f9f1` |
| `Ellipse 70.png` | 15 | 15 | `93bd3136` |
| `FRAME lvlh.png` | 67 | 47 | `c17376be` |
| `Group 13.png` | 63 | 28 | `d95f6beb` |
| `Group 14.png` | 63 | 28 | `84aa5402` |
| `Group 15.png` | 63 | 28 | `84aa5402` |
| `Group 16.png` | 79 | 41 | `2bf7aaba` |
| `Group 17.png` | 94 | 41 | `e0f357e1` |
| `Group 18.png` | 94 | 41 | `71f23d0a` |
| `Group 19.png` | 71 | 68 | `b885b635` |
| `Group 20.png` | 1298 | 1335 | `028241f5` |
| `Group 21.png` | 962 | 370 | `cfb058c1` |
| `Group 22.png` | 962 | 368 | `9ef8e2fb` |
| `Group 23.png` | 672 | 1536 | `92f8077e` |
| `Group 24.png` | 672 | 1536 | `64ed6bfb` |
| `Group 25.png` | 80 | 80 | `b348f587` |
| `Group 37.png` | 257 | 140 | `b20c9f0d` |
| `Group 43.png` | 245 | 218 | `ecbc5fb9` |
| `Group 44.png` | 1756 | 1736 | `4b7e7280` |
| `Group 7.png` | 340 | 85 | `b22ff5d8` |
| `Intersect-1.png` | 91 | 624 | `b31084f7` |
| `Intersect-2.png` | 624 | 91 | `12f3d06c` |
| `Intersect-3.png` | 91 | 624 | `dd476955` |
| `Intersect.png` | 624 | 91 | `1c6f38ce` |
| `Line 28.png` | 466 | 2 | `3ac1c8f7` |
| `Line 29.png` | 466 | 2 | `3ac1c8f7` |
| `Line 32.png` | 1 | 19 | `ca0a469f` |
| `Line 33.png` | 1 | 19 | `ca0a469f` |
| `Line 34.png` | 19 | 1 | `3194d4d9` |
| `Line 35.png` | 19 | 1 | `3194d4d9` |
| `Line 36.png` | 15 | 15 | `7c4e2249` |
| `Line 37.png` | 15 | 15 | `7c4e2249` |
| `Line 38.png` | 15 | 15 | `588cd1bf` |
| `Line 39.png` | 15 | 15 | `588cd1bf` |
| `Line 40.png` | 18 | 9 | `81004b56` |
| `Line 41.png` | 18 | 9 | `81004b56` |
| `Line 42.png` | 9 | 18 | `8753eca6` |
| `Line 43.png` | 9 | 18 | `8753eca6` |
| `Line 44.png` | 9 | 18 | `e1022172` |
| `Line 45.png` | 9 | 18 | `e1022172` |
| `Line 46.png` | 18 | 9 | `2d31d33e` |
| `Line 47.png` | 18 | 9 | `2d31d33e` |
| `Line 48.png` | 1 | 18 | `daa8274e` |
| `Line 49.png` | 1 | 18 | `daa8274e` |
| `Line 50.png` | 18 | 1 | `31f4b577` |
| `Line 51.png` | 18 | 1 | `31f4b577` |
| `Line 52.png` | 14 | 14 | `e90324d6` |
| `Line 53.png` | 14 | 14 | `e90324d6` |
| `Line 54.png` | 14 | 14 | `8038b0fd` |
| `Line 55.png` | 14 | 14 | `8038b0fd` |
| `Line 56.png` | 10 | 17 | `23cc679b` |
| `Line 57.png` | 10 | 17 | `23cc679b` |
| `Line 58.png` | 17 | 10 | `d669fce5` |
| `Line 59.png` | 17 | 10 | `d669fce5` |
| `Line 60.png` | 18 | 6 | `cf512bf2` |
| `Line 61.png` | 18 | 6 | `cf512bf2` |
| `Line 62.png` | 6 | 18 | `e8cd24a9` |
| `Line 63.png` | 6 | 18 | `e8cd24a9` |
| `Line 64.png` | 6 | 18 | `5cc7a043` |
| `Line 65.png` | 6 | 18 | `5cc7a043` |
| `Line 66.png` | 18 | 6 | `9503b7ef` |
| `Line 67.png` | 18 | 6 | `9503b7ef` |
| `Line 68.png` | 17 | 10 | `45eef284` |
| `Line 69.png` | 17 | 10 | `45eef284` |
| `Line 70.png` | 10 | 17 | `79b57abb` |
| `Line 71.png` | 10 | 17 | `79b57abb` |
| `Line 74.png` | 15 | 21 | `4ba3f29b` |
| `P I T C H.png` | 15 | 113 | `5fb9e200` |
| `RANGE.png` | 62 | 15 | `bafd2444` |
| `RATE.png` | 45 | 15 | `dde8ec7b` |
| `RESET.png` | 100 | 25 | `5003e2e7` |
| `Rectangle 131.png` | 483 | 255 | `9b9f5399` |
| `Rectangle 132.png` | 183 | 97 | `803aedac` |
| `Rectangle 133.png` | 183 | 97 | `803aedac` |
| `Rectangle 138.png` | 444 | 106 | `dfffec3c` |
| `Rectangle 89.png` | 2451 | 1822 | `9a742a52` |
| `Rectangle 92.png` | 279 | 85 | `09a7ae30` |
| `Rectangle 93.png` | 279 | 85 | `09a7ae30` |
| `START.png` | 103 | 25 | `50d46389` |
| `Subtract-1.png` | 192 | 635 | `446d70e3` |
| `Subtract-2.png` | 192 | 635 | `14c39f98` |
| `Subtract-3.png` | 170 | 170 | `821fe480` |
| `Subtract.png` | 1162 | 1162 | `b74461fd` |
| `Union-1.png` | 107 | 115 | `cc225be8` |
| `Union-2.png` | 109 | 118 | `18e85b2e` |
| `Union-3.png` | 109 | 118 | `18e85b2e` |
| `Union-4.png` | 114 | 122 | `b51346b4` |
| `Union-5.png` | 114 | 122 | `a6acd2b9` |
| `Union-6.png` | 114 | 122 | `b67113e7` |
| `Union-7.png` | 114 | 122 | `da044010` |
| `Union-8.png` | 343 | 341 | `194debe1` |
| `Union-9.png` | 384 | 384 | `b8f124dd` |
| `Union.png` | 107 | 115 | `d0f60847` |
| `Vector.png` | 389 | 386 | `c6022583` |
| `X.png` | 15 | 17 | `c58db397` |
| `Y.png` | 15 | 17 | `b49e8519` |
| `YAW.png` | 50 | 17 | `313b78fb` |
| `Z.png` | 13 | 17 | `c0fc6a64` |
| `alert activity.png` | 260 | 24 | `4c84c231` |
| `camera Virtual.png` | 92 | 47 | `e67db8d0` |
| `far field positioning.png` | 325 | 21 | `58d655de` |
| `flight commands.png` | 320 | 25 | `e99c2115` |
| `heroicons-solid_view-grid.png` | 28 | 28 | `042aaff7` |
| `roll.png` | 57 | 17 | `f6a041a0` |

### `settings_displays/` — 48 elements

| element PNG | w | h | md5 |
|---|---:|---:|---|
| `- Display 1-1.png` | 175 | 24 | `181c3be5` |
| `- Display 1-2.png` | 175 | 24 | `181c3be5` |
| `- Display 1-3.png` | 175 | 24 | `181c3be5` |
| `- Display 1.png` | 175 | 24 | `181c3be5` |
| `- Display 2-1.png` | 177 | 24 | `bbbd030f` |
| `- Display 2-2.png` | 177 | 24 | `bbbd030f` |
| `- Display 2-3.png` | 177 | 24 | `bbbd030f` |
| `- Display 2.png` | 177 | 24 | `bbbd030f` |
| `- Display 3-1.png` | 176 | 24 | `75836dbe` |
| `- Display 3-2.png` | 176 | 24 | `75836dbe` |
| `- Display 3-3.png` | 176 | 24 | `75836dbe` |
| `- Display 3-4.png` | 176 | 24 | `75836dbe` |
| `- Display 3-5.png` | 176 | 24 | `75836dbe` |
| `- Display 3-6.png` | 176 | 24 | `75836dbe` |
| `- Display 3.png` | 176 | 24 | `75836dbe` |
| `CABIN SETTINGS.png` | 327 | 29 | `8dd5f820` |
| `Cabin displays-1.png` | 222 | 21 | `a7fb56d9` |
| `Cabin displays-2.png` | 222 | 21 | `a7fb56d9` |
| `Cabin displays.png` | 222 | 21 | `a7fb56d9` |
| `Frame 61.png` | 3381 | 1935 | `c0f1abcb` |
| `Group 57.png` | 3058 | 2002 | `7628bc25` |
| `Group 58.png` | 78 | 81 | `458f94a6` |
| `Group 59.png` | 77 | 82 | `6e712bc9` |
| `Group 60.png` | 79 | 82 | `35a91349` |
| `LIGHTING.png` | 150 | 24 | `458b63cd` |
| `Line 102.png` | 270 | 2 | `7306ab58` |
| `Line 103.png` | 270 | 2 | `7306ab58` |
| `Line 104.png` | 270 | 2 | `7306ab58` |
| `Line 105.png` | 270 | 2 | `7306ab58` |
| `Line 110.png` | 270 | 2 | `7306ab58` |
| `Line 111.png` | 270 | 2 | `7306ab58` |
| `Line 112.png` | 270 | 2 | `7306ab58` |
| `Line 113.png` | 270 | 2 | `7306ab58` |
| `Line 114.png` | 270 | 2 | `7306ab58` |
| `Line 115.png` | 270 | 2 | `7306ab58` |
| `Line 116.png` | 270 | 2 | `7306ab58` |
| `Polygon 45.png` | 580 | 243 | `dc013a93` |
| `Rectangle 158.png` | 144 | 32 | `c34351ab` |
| `Rectangle 184.png` | 1412 | 659 | `6d55c4ec` |
| `Rectangle 191.png` | 311 | 395 | `80953f3b` |
| `Rectangle 192.png` | 311 | 521 | `e2047e48` |
| `Rectangle 193.png` | 311 | 521 | `e2047e48` |
| `Rectangle 194.png` | 311 | 521 | `e2047e48` |
| `Subtract.png` | 3383 | 1936 | `c9c3f2a7` |
| `Tap to disable display or.png` | 247 | 46 | `231fe705` |
| `Trunk Jettison and Deorbit Burn Enabled.png` | 360 | 59 | `db41531c` |
| `Union.png` | 1412 | 740 | `be688c4a` |
| `cabin.png` | 83 | 21 | `b3c43b8e` |
