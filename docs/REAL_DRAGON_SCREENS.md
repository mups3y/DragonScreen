# How the real Crew Dragon screens are arranged and how they work

Researched 2026-08-05. Sources at the bottom, all public. **Read the confidence marker on every
claim** — some of this is SpaceX-adjacent first-hand, some is a careful reconstruction from launch
footage, and one important thing turns out not to be publicly documented at all.

---

## 1. The hardware — CONFIRMED, and Tundra already modelled it

Three large touchscreen displays in a row, facing the commander (left seat) and pilot (right seat),
with roughly **30 physical buttons** below them and a twist ABORT handle in the centre. The
touchscreens work through the pressurised-suit gloves.

**The physical buttons are not a research problem — they are already in the model**, and the labels
in the Tundra IVA match the real capsule.

### The complete panel — TRANSCRIBED FROM IN-GAME CLOSE-UPS, 2026-08-05

Left to right along the console. Every label below was read off the rendered model, not inferred.

**1 + 6. Emergency panel — A MIRRORED PAIR, one at each end.** Both copies are identical. Each seat
gets its own, because the crew member who needs it may not be the one who can reach the middle.

    box border, labelled VEHICLE EMERGENCIES along the top and CABIN EMERGENCIES along the bottom
      row 1   CANCEL (⊘)   WATER DEORBIT   DEORBIT NOW   BREAKOUT   EXECUTE (◉)
      row 2   DEPRESS RESPONSE   SURPRESS FIRE   FIRE RESPONSE

`SURPRESS FIRE` is spelled that way **in the model** — the real capsule reads SUPPRESS. Do not
"correct" it in our own labels if we ever redraw that texture; matching the installed art matters
more, and it is Tundra's to fix.

CANCEL and EXECUTE bracketing the row is the real interlock: an emergency action is armed, then
executed. Copy that behaviour, not just the buttons.

**2. Power and strings.** Captioned `PRESS AND HOLD TO SELECT STRING FOR RESET`, above and below.

    row 1   POWER 1 (⏻)   STRING 1A   STRING 1B   STRING 1C   RESET 1 (↻)
    row 2   POWER 2 (⏻)   STRING 2A   STRING 2B   STRING 2C   RESET 2 (↻)

**3. Chutes and pyros.**

    row 1   ENABLE BACKUP PYROS   JETTISON NOSE CONE   MAINS ONLY   DROGUES & MAINS
    row 2   ENABLE ENTRY REBOOT   CUT MAINS  (← USE ONLY AFTER LANDING)     FIRE PYRD

**4. Centre — the ABORT handle.** A black cylindrical twist handle in a red-outlined box, marked
`EJECT` above and below. Twist, not press.

**5. Entry mode.**

    ENABLE BACKUP ENTRY   [three unlabelled buttons, ⇄ swap/cycle icons]   ENABLE NORMAL ENTRY

The three middle buttons carry **no text at all**, only a swap/cycle arrow pair. Bracketed by BACKUP
and NORMAL entry, the obvious reading is one swap per flight-computer string — which would pair them
with `STRING 1A/1B/1C` and `2A/2B/2C` next door. **That is a reading, not a fact.** Icon-only is
consistent with the real capsule's deliberate lack of text labels, so there may be no more to learn
from the art; treat their function as undecided.

### ⚠ HOW THE REAL BUTTONS LIGHT UP IS **NOT PUBLICLY DOCUMENTED** — searched 2026-08-06

Asked directly and searched four ways: illumination/armed/executed indicators, backlighting, the
arm/execute interlock, and the two detailed public reconstructions. **No source describes what the
buttons do visually when pressed.** Baird's article covers only the touchscreen UI and says outright
that he left unknown screens blank rather than guess; the Medium UX piece is paywalled and enumerates
the five panels we already transcribed better ourselves.

**So the colour scheme is OUR DECISION, not a reconstruction. Never cite it as fact.**
Decided with the user 2026-08-06: unlit grey as modelled → **white** when pressed or armed → **red**
when refused. What we do *not* do is add a glow, halo or outline of our own — only the dash Tundra
already drew is driven.

Three things the search DID establish, all new:

- **38 manual buttons in two rows**, described as back-up control. Our transcription maps 38 plus the
  handle, which is a match.
- **Many buttons sit beneath clear guards**, and are "intended to never be used, because they are
  often the third option after the touch screens and ground control." That is an argument for the
  interlock being strict rather than convenient.
- **The EJECT handle must be PULLED, then twisted** — this page previously said "twist, not press",
  which was incomplete.

Sources: [Slashdot on SpaceX revealing the controls](https://science.slashdot.org/story/18/08/15/0455239/spacex-reveals-the-controls-of-its-dragon-spacecraft-for-the-first-time),
[Space.com](https://www.space.com/spacex-crew-dragon-touchscreen-astronaut-thoughts.html),
[TechCrunch](https://techcrunch.com/2020/05/04/this-is-certainly-different-astronauts-on-controlling-the-dragon-spacecraft-via-touchscreen/),
[Dillon Baird](https://dillonbaird.io/articles/mutantdragon/).

### ⚠ EVERY BUTTON HAS A STATE INDICATOR, AND IT IS A DASH

Confirmed across all the close-ups: **every single button carries a small horizontal `—` above its
label.** Not decoration — that is the indicator, and it is the panel's entire visual language for
"is this armed / active / available".

This matters more than it looks:

1. It means an interactive panel does **not** need invented highlight styling. The affordance is
   already modelled and it is one mark per button.
2. It gives the `_2.._5` transform variants a second, better-supported hypothesis: **per-button
   visual states** (dark / armed / active / …), rather than the grouping arithmetic below.
3. Whichever it is, **do not add a glow or an outline of our own design.** Drive what Tundra already
   drew, or the panel stops matching itself.

Whether the dash is separate geometry (drivable per button) or baked into `TE_CD2_IVA_BUTTONS.dds`
(needs a material or UV trick) is exactly what the transform dump answers. Do not commit to a
lighting approach before reading it.

### What each one can actually do in KSP

Separating these now prevents building a panel that promises what it cannot deliver.

| buttons | wirable in KSP? |
|---|---|
| JETTISON NOSE CONE, MAINS ONLY, DROGUES & MAINS, CUT MAINS, ENABLE BACKUP PYROS, FIRE PYRD | **Yes, directly.** The Tundra Dragon has a nose cone and real chute modules. |
| ABORT / EJECT handle | **Yes** — KSP's abort action group. |
| DEORBIT NOW, WATER DEORBIT, BREAKOUT, CANCEL, EXECUTE | **Yes, as sequences** we implement. These are the arm/execute interlock. |
| ENABLE NORMAL / BACKUP ENTRY | **Yes**, as our own entry-mode state. |
| POWER 1/2, STRING 1A–1C / 2A–2C, RESET 1/2 | **Partly.** No KSP equivalent to power strings; can drive real electric-charge state and be honest, or stay indicators. **Do not fake a system that does nothing.** |
| DEPRESS RESPONSE, SURPRESS FIRE, FIRE RESPONSE | **No KSP equivalent.** Cabin emergencies do not exist in stock. Leave inert rather than invent. |

### THE BUTTON MAP — SETTLED IN GAME 2026-08-05. Both hypotheses were wrong.

The dump (renderer bounds in prop space) says the naming means something duller than either guess:

- **`TE_CD2_PROP_BUTTON_1..8` are not buttons. They are the eight PANEL PLATES**, children of the
  prop root, each 0.0975 wide except plate 7 which is 0.0396.
- **`CD2_PROP_BUT*` are the individual buttons**, 0.0177 x 0.0150 each, and are **children of a
  plate**.
- **`_2 .. _5` is just Unity's copy suffix** for the same button mesh reused in another plate.
  Nothing to do with press states, nothing to do with grouping.

**Naming rule, within any plate:** `BUT1..BUT5` are the TOP row left to right, `BUT6..BUT10` the
BOTTOM row left to right. Top row sits at z = -0.1185, bottom at z = -0.1368 (more negative z is
further from the screens, i.e. lower on the console).

| plate | prop-space x | what it is | buttons |
|---|---|---|---|
| `TE_CD2_PROP_BUTTON_1` | -0.376 | **LEFT emergencies** | 8 |
| `TE_CD2_PROP_BUTTON_2` | -0.267 | power + strings | 10 |
| `TE_CD2_PROP_BUTTON_3` | -0.157 | chutes + pyros | 7 |
| `TE_CD2_PROP_BUTTON_7` | -0.077 | **blank filler**, narrow, no children | 0 |
| `TE_CD2_PROP_BUTTON_8` | 0.000 | **ABORT** — holds `CD2_ABORT_HANDLE` | 1 |
| `TE_CD2_PROP_BUTTON_4` | +0.153 | entry mode | 5 |
| `TE_CD2_PROP_BUTTON_5` | +0.262 | **blank filler**, no children | 0 |
| `TE_CD2_PROP_BUTTON_6` | +0.373 | **RIGHT emergencies** (mirror of plate 1) | 8 |

Full mapping, transform to label:

    PLATE 1 / 6  -- emergencies, LEFT and RIGHT copies are identical
      top     BUT1 CANCEL   BUT2 WATER DEORBIT   BUT3 DEORBIT NOW   BUT4 BREAKOUT   BUT5 EXECUTE
      bottom       BUT7 DEPRESS RESPONSE   BUT8 SURPRESS FIRE   BUT9 FIRE RESPONSE
                   (three only, inset under positions 2-4)

    PLATE 2  -- power and strings
      top     BUT1 POWER 1   BUT2 STRING 1A   BUT3 STRING 1B   BUT4 STRING 1C   BUT5 RESET 1
      bottom  BUT6 POWER 2   BUT7 STRING 2A   BUT8 STRING 2B   BUT9 STRING 2C   BUT10 RESET 2

    PLATE 3  -- chutes and pyros
      top     BUT1 ENABLE BACKUP PYROS   BUT2 JETTISON NOSE CONE   BUT3 MAINS ONLY
              BUT4 DROGUES & MAINS
      bottom  BUT6 ENABLE ENTRY REBOOT   BUT7 CUT MAINS   BUT10 FIRE PYRD (offset right, as drawn)

    PLATE 4  -- entry mode, a SINGLE row sitting at the bottom-row z
      BUT6 ENABLE BACKUP ENTRY   BUT7/8/9 the three swap toggles   BUT10 ENABLE NORMAL ENTRY

Every count matches the photographs, including the two blank plates and FIRE PYRD sitting apart from
its row — which is why this is a map and not another hypothesis.

### ⚠ THERE ARE NO PER-BUTTON COLLIDERS. WE MUST ADD THEM.

The whole prop carries **one** collider:

    TundraExploration/Props/TE_CD2_IVA_SCREEN(Clone)   [Collider]   size 1.0372 x 0.0887 x 0.4073

One box over the entire console — screens and buttons together. No button has a collider, **and
neither do the screens**, so touch input needs them too.

### CHECKED FIRST: nothing we have supplies them. Verified 2026-08-05, not assumed.

The project rule is look before building, and this is what looking found:

| checked | result |
|---|---|
| every file in `GameData` mentioning `TE_CD2_IVA_SCREEN` / `CD2_PROP_BUT` / `TE_CD2_PROP_BUTTON` | exactly one hit — **our own** `DragonScreen.cfg`. No mod patches this prop. |
| **MAS** `MASComponentColliderEvent.cs:199-247` | `FindModelTransform` then `AddComponent<ButtonObject>` — it attaches behaviour to a collider that **already exists** |
| every MAS source file, for `AddComponent<BoxCollider>` / `<MeshCollider>` | **no hits.** MAS never creates a collider. Neither does RPM. |
| installed prop packs | RPM only. No ASET, no ALCOR. Their props ship colliders baked in; **Tundra's do not.** |

**So the colliders must be created — but the MECHANISM is ported, not invented.** `OnMouseDown` on a
MonoBehaviour attached to the collider's GameObject is exactly what MAS does and is proven to work
under KSP's internal camera. That was the part worth not guessing at.

### The route — DECIDED: a collider per button and per screen

Sized from the bounds already in the dump, which we have for every one of them.

Rejected: raycasting the single existing console box and deriving which control was hit from the
local hit point. It needs no new objects, but it **re-derives geometry that would then have to be
kept in step with the model by hand** — the exact failure mode this project keeps paying for. The
existing box is also a coarse axis-aligned volume covering the screens *and* the buttons, so a ray
strikes it well before reaching any control's actual face.

**Touch coordinates come free and need no `MeshCollider`.** `RaycastHit.textureCoord` would require
a MeshCollider with a read/write-enabled mesh; we do not need it. Converting the world hit point into
the screen transform's local space and dividing by the mesh bounds gives normalised screen
coordinates — **using the very numbers already measured for the aspect ratio**. One measurement, two
uses, no duplicated geometry.

### SETTLED IN GAME 2026-08-05 — the probe experiment

Two probes, one load. Probe A on Tundra's console collider, probe B on `CD2_PROP_BUT1` (left panel
CANCEL) with a `BoxCollider` added:

    probe A  console  type=MeshCollider  isTrigger=False  enabled=True  layer=16 (kerbals)
    probe B  CD2_PROP_BUT1  BoxCollider added, size (0.0177, 0.0024, 0.0148), layer=16

    ENTER  BUTTON:CD2_PROP_BUT1
    DOWN   BUTTON:CD2_PROP_BUT1  (1)
    DOWN   BUTTON:CD2_PROP_BUT1  (2)

**Probe B fired. Probe A never did. A nested collider WINS the hit.**

And the reason is now visible: **the console collider is a `MeshCollider`, not a box.** The
1.04 x 0.09 x 0.41 measured earlier is its *bounds*, not a solid volume — it follows the console's
actual surface, so the buttons are not buried inside it. The concern was real; the geometry answered
it.

Three further facts, confirmed rather than trusted:

- `AddComponent<BoxCollider>` **auto-fits the mesh** — the button's box came out
  0.0177 x 0.0024 x 0.0148, matching its geometry. No hand-derived sizes.
- The collider **inherits layer 16** from the button's GameObject. No layer decision to make, as long
  as the collider goes on the EXISTING GameObject rather than a new child.
- **FreeIva is not affected**, and that needed no test: every collider we add sits strictly inside
  the console collider's volume, which the player already cannot enter.

**⚠ THE ZERO-THICKNESS TRAP, for the screens only.** The buttons have real depth (0.0024), but the
screen mesh measures `0.2844 x 0.0000 x 0.1561` — exactly flat. An auto-fitted box would be zero
thick, which makes `1/size` infinite and can make the raycast miss entirely. `ScreenTouch` gives any
flat axis a 4 mm minimum. A silently dead touchscreen would be a long evening.

### Independent confirmation of the screen order

    TT_CD2_IVA_SCREEN1   x = -0.2953   size 0.2844
    TT_CD2_IVA_SCREEN2   x =  0.0000   size 0.2816   <- narrower, again
    TT_CD2_IVA_SCREEN3   x = +0.2953   size 0.2844

Left / centre / right, now by measured position rather than by counting bars on a test pattern.

---

## 2. The software architecture — CONFIRMED in shape

From Dillon Baird's 60-day reconstruction, which was built from launch footage frame by frame and
whose source is public. This is the most detailed public description of the display software.

**~25–30 individual pages** exist in the real system (Doug Hurley's number). Two persistent chrome
elements frame all of them:

**Global nav bar** — always present, along the bottom, four sections:

1. navigation links (page selector)
2. vehicle state / status
3. connection link status, with timers
4. **MET** — mission elapsed time

**Subview nav bar** — moves between the sub-pages of the current section, and **turns red when that
subview holds an alert**, so a non-nominal subsystem can be reached in one touch from anywhere.

That alert behaviour is the single most important interaction detail on this page. It is what makes
the interface flyable rather than merely pretty, and it is cheap to implement.

**The page groups:**

| group | contents |
|---|---|
| **Navigation** | 2D and 3D map. Vehicle position, orbit path, ground stations, ISS position, sun position, planned splashdown zone. 2D uses a d-pad; 3D is touch-and-drag. |
| **Docking** | approach and docking controls — the page everyone has seen, because SpaceX published it as the ISS docking simulator |
| **Vehicle** | per-subsystem pages: overview, functions, alerts. One page per system. |
| **Settings** | audio, lighting, video transmission |

The manual-control mode puts an **attitude control view** on the screens — translation controls
bottom left, rotation controls bottom right, deliberately icon-only with no text labels because the
crew is trained on them.

---

## 3. Which page goes on which screen — NOT PUBLICLY DOCUMENTED, and that is the answer

Searched for it directly and it is not in the public record: not in NASA's material, not in the press
coverage, not in either detailed reconstruction. The reason is that **the assignment is not fixed**.
The three displays are general-purpose and crew-selectable — the global nav bar is on every screen,
and the crew puts what they want where they want it.

So "the correct art on the correct screen" is a question about **convention, not specification**. The
convention has to come from mission imagery, and the honest source for it is launch and docking
footage, not a document.

**DECIDED 2026-08-05: all three screens are the same screen.** One page set, one renderer, one nav
bar, three instances; the crew picks what each shows. See CLAUDE.md, "ONE SCREEN, FOUR SURFACES".
The table below is therefore a **starting selection**, not a design.

### The page set

Five pages, matching the reference recreation's five views and the real vehicle's four groups. Named
from the real capsule's vocabulary rather than the Vue file names, which are `First`..`Fifth`.

| page | contents | reference view |
|---|---|---|
| `FLIGHT` | telemetry strip — active phase, splashdown time, inertial velocity, altitude, apogee, perigee — with the **Dragon illustration** and Earth | `First.vue` (`Capsule.vue` is a THREE.js `spaceDragon.glb`) |

> **"Telemetry strip" here means DRAGON'S, not F9I's.** It is the header row across the top of the
> `FLIGHT` page in the reference recreation, and it is part of the Crew Dragon design. It has nothing
> to do with the Falcon 9 Interface's kOS panel — **this mod has no kOS dependency and no link to
> F9I at all.** Every value on these pages is read directly in C# from the vessel and its orbit. The
> F9I bridge `PartModule` and its protocol are in `plugin/reference_f9i/` and are not compiled.

| `VEHICLE` | subsystems, consumables, alerts. The dial gauges live here | `Third.vue` → `Overview.vue` + `Mech.vue` |
| `NAV` | trajectory, ground track, orbit, manual attitude view | `Second.vue` |
| `DOCKING` | approach and docking controls | SpaceX's published ISS docking sim; `assets/figma/dragon_interface_docking/` |
| `SETTINGS` | cabin, audio, lighting, cameras | `Fifth.vue` |

`Fourth.vue` is a **Suit Leak Check** procedure page — a checklist, not an instrument. Worth keeping
in mind as the shape a procedure page takes, but not one of the five.

### Defaults — DECIDED 2026-08-05

| our transform | position | default page | why this one |
|---|---|---|---|
| `TT_CD2_IVA_SCREEN1` | **left** | `VEHICLE` | Commander's side. Alert routing is the interface's spine, so one screen should always be the one that tells you something is wrong. |
| `TT_CD2_IVA_SCREEN2` | **centre** | `FLIGHT` | Shared by both seats, and the most head-on screen from the IVA camera. It is also the signature page — the illustration is what makes this read as a Crew Dragon at a glance. |
| `TT_CD2_IVA_SCREEN3` | **right** | `NAV` | Pilot's side. Trajectory is the constant across every phase of a mission; `DOCKING` is episodic, so it is a page you select, not one you start on. |

**Any screen must be able to show any page.** That is what the real vehicle does, it is what the
global nav bar is for, and building fixed single-purpose screens would be a decision to be less
accurate, not more. The table above is a starting selection, and it costs nothing extra because the
page selector is required anyway.

**A PHASE CHANGE MUST NEVER OVERRIDE A PAGE THE CREW CHOSE.** It is tempting to auto-swap to
`DOCKING` on approach or `FLIGHT` on entry, and it is wrong: the one thing worse than the wrong page
is the right page vanishing because the software decided it knew better. Auto-selection is
acceptable only on a screen the crew has not touched this flight, and even then it should be
announced by the nav bar rather than done silently.

---

## 4. What this means for our build

1. **The global nav bar and the subview nav bar are the first UI to build**, before any page content.
   They are on every screen, in every phase, and their red-alert behaviour is the interface's spine.
2. **Design the page router first, one page per screen selectable.** Three fixed screens would have
   to be torn up.
3. MET, link status and vehicle state are always visible — they are chrome, not a page.
4. Icon-only controls are correct for translation/rotation. Do not add text labels the real one does
   not have.

---

## Sources

- [Recreating the SpaceX Crew Dragon UI in 60 Days — Dillon Baird](https://dillonbaird.io/articles/mutantdragon/)
  and the [Medium version](https://dillonbaird.medium.com/spacex-crew-dragon-ui-in-60-days-afc53095a990)
  — nav bar structure, page groups, the ~25–30 page count. Source published on GitHub.
- [Navigating the Future — Kristen Moores](https://medium.com/@kristenmoores/navigating-the-future-b950772acc0a)
  — three touchscreens plus ~30 buttons; translation bottom-left, rotation bottom-right, icon-only by
  intent.
- [Crew Dragon Displays and Crew Spacesuits Ready for Mission to Space Station — NASA](https://blogs.nasa.gov/commercialcrew/2020/05/12/crew-dragon-displays-and-crew-spacesuits-ready-for-mission-to-space-station)
- [The touchscreen controls of SpaceX's Crew Dragon — Space.com](https://www.space.com/spacex-crew-dragon-touchscreen-astronaut-thoughts.html)
  — glove compatibility, manual attitude-control view.
- [Astronauts on controlling the Dragon spacecraft via touchscreen — TechCrunch](https://techcrunch.com/2020/05/04/this-is-certainly-different-astronauts-on-controlling-the-dragon-spacecraft-via-touchscreen/)
- [SpaceX Crew Dragon Displays UI/UX — Shane Mielke](https://www.shanemielke.com/work/spacex/crew-dragon-displays/)
  — principal UI/UX designer on the Crew Displays team 2018–2020. Portfolio imagery only, no specs,
  but it is the closest thing to a first-party visual reference.

**Already on disk and worth more than any of the above for actual pixels:**
`assets/reference/dragon2-ui-vue/` is [Neel Dandiwala's SpaceX-Dragon2-UI](https://github.com/Neel-Dandiwala/SpaceX-Dragon2-UI)
— note that it is a **single-screen, five-page** recreation with a bottom page selector, so it models
the page CONTENT and the interaction, not the three-screen arrangement.
