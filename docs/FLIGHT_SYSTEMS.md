# Flight systems — reuse, not reinvention

Direction set by the user 2026-08-05: build the flight systems from **MechJeb** and **Trajectories**
as bases, tailored to the Falcon / Crew Dragon missions, rather than writing new files for problems
that are already solved. This records the licence position, what is actually reusable, and the one
scope question that changes the size of the work by an order of magnitude.

---

## Licences — both GPL-3.0, and this project is GPL-3.0

| source | licence | verified |
|---|---|---|
| MechJeb2 | **GPL-3.0** | `Desktop/mechjeb_src/LICENSE.md` — "released under the GNU GPL version 3" |
| Trajectories | **GPL-3.0** | `GameData/Trajectories/LICENSE.md` |
| MAS (AvionicsSystems) | MIT | already the source of the render path, text and touch |
| **MOARdVPlus** | **CC-BY-NC-SA — INCOMPATIBLE** | never copy from it; see ASSET_PROVENANCE.md |

Porting from MechJeb or Trajectories is legally clean **with attribution**, and the result stays
GPL-3.0. Cite file and line at every ported site, as with MAS.

---

## Trajectories — CALL IT, DO NOT PORT IT. But calling it is not enough.

It is already installed and it ships a **public API**, found by reflecting the DLL:

    API.AlwaysUpdate                API.GetImpactPosition()      API.PlannedDirection
    API.HasTarget                   API.GetImpactVelocity()      API.CorrectedDirection
    API.SetTarget / ClearTarget     API.GetTimeTillImpact()      API.RawImpactPosition

Re-deriving an atmospheric integrator to get numbers a loaded DLL already computes would be the most
expensive mistake available. F9I already uses these from kOS.

### ⚠ THE PREDICTION IS ONLY TRUE IF THE DESCENT PROFILE MATCHES WHAT WE ACTUALLY FLY

**Raised by the user, and it is the whole point:** *"did you once set it correctly to -180 retrograde
and the correct aoa at the correct altitudes so we actually get an accurate prediction? also fixed
body set on so it counts the rotation."*

Answer: **no.** Nothing had been set. An unconfigured Trajectories predicts the descent of a vehicle
flying some other attitude, and returns a confident, precise, wrong impact point — which is strictly
worse than no prediction, and is **the exact failure this project already paid for** (see the memory
on the Dragon entry: the error maths was fiction and got steered on for many turns).

All of it is settable from code — verified in the DLL, not assumed:

    set_DescentProfileAngles     AoA per band: entry / high / low / final
    set_DescentProfileGrades     retrograde or prograde per band      <- the -180
    set_DescentProfileModes      AoA mode vs horizon mode per band
    set_BodyFixedMode            counts the planet's rotation during descent
    set_AlwaysUpdate             keeps predicting with the window closed
    set_MaxAoA, set_AoAResolution, set_RetrogradeEntry, set_Horizon

**Rules that follow, and none of them is optional:**

1. **Set the profile before reading any prediction.** A read from an unconfigured Trajectories is not
   a measurement, it is a guess wearing a decimal point.
2. **The profile must be driven by the SAME schedule our entry guidance commands.** If guidance flies
   one AoA schedule and Trajectories is told another, the prediction is fiction. They must come from
   one source, not two hand-maintained copies — the same rule that made `ChromeBar.LinkRect` single.
3. **BodyFixedMode ON.** A descent takes minutes; Kerbin turns underneath it. Without this the impact
   point is wrong by kilometres in a way that looks like guidance error.
4. **It is not one profile.** It differs with configuration — capsule + trunk versus capsule alone
   have different ballistic coefficients and different attitudes — and by phase.
5. **Verify the prediction a second way before trusting it.** Standing project rule: never call
   recorded data correct without an independent check. Compare predicted impact against the actual
   splashdown of a flown mission.

### THE EXACT SETTINGS — read out of F9I's flown code, 2026-08-05

Both are already solved and flying. **Do not re-derive these.** Cited so the next reader can check.

#### Crew Dragon — `F9I/dragon_deorbit.ks:688-720`, constants at :35 and :53-56

    descentmodes  = (true,  true,  true,  true)     // AoA mode ON in all four rows
    descentgrades = (true,  true,  true,  true)     // TRUE = RETROGRADE
    descentangles = (0.00, 15.00,  8.25,  1.95)     // Entry / High / Low / Final, degrees

Those angles are `dgAoA x (0.00, 1.00, 0.55, 0.13)` with `dgAoA = 15` (capsule trim off retrograde,
L/D ~0.27). Kept as FRACTIONS so changing the trim scales all four together.

**They were MEASURED, not chosen** — from the `aoaRetro` column of `bb_dragon_CrewDragon_072`,
binned by altitude:

    70-55 km   0.07 - 0.11 deg    pure retrograde, the shield-forward coast in   -> Entry 0.00
    50-30 km   14.6 - 15.0 deg    the lifting phase, full trim                   -> High  1.00
    25-15 km    8.5 -  4.6 deg    lift bleeding off                              -> Low   0.55
    10-0  km    0.2 -  1.8 deg    essentially retrograde again                   -> Final 0.13

#### Falcon 9 booster — `booster.ks:3341-3344`, in `SteeringCorrections`

    descentmodes  = (true, true, true, true)
    descentgrades = (true, true, true, true)
    descentangles = (180,  180,  180,  180)
    + ADDONS:TR:SETTARGET(landingzone) when not already targeted

Empirically right — the boosters land at 0.34-0.56 m. **One honest caveat:** a booster is very nearly
a symmetric cylinder, so 0 and 180 deg of AoA may produce the same drag and the value may not be doing
what it looks like it is doing. It works; the semantics are unverified. The capsule is NOT symmetric,
which is why its four angles matter and had to be measured.

#### The traps, every one paid for with a flight

1. **`descentgrades = TRUE` means RETROGRADE.** Setting it false cost CargoDragon_012: the guide
   vector sat **134.9 deg off the nose for the entire entry** with the correction pinned at its 45 deg
   limit, and both navball markers drawn on the PROGRADE side, where a heat-shield-first capsule never
   points. The comment in the source read the opposite way round until 2026-08-04.
2. **Write `descentmodes` as well.** `RESETDESCENTPROFILE(aoa)` writes only the ANGLES; with modes
   false Trajectories ignores them entirely and just follows the grade. Never rely on the default.
3. **Never inherit Starship's 78 deg.** It flies nearly broadside; a capsule trims ~15 deg and its
   markers belong clustered tight around retrograde. Inheriting 78 throws them most of a hemisphere.
4. **Never write one angle into all four rows.** "15 everywhere" tells the mod to predict lift where
   we generate none.
5. **Read the profile back and log it.** The Settings tab has *"Default to Retrograde descent profile
   on vessel launch"*, which can re-apply a reference underneath a write.
6. **Active-vessel only** — these suffixes throw or no-op otherwise. Guard on it.
7. **The aim constants absorb this prediction's error.** Change the four fractions and the `dgAim*`
   values must be re-fitted with them.

#### ⚠ BodyFixedMode is NOT set anywhere in F9I

Grepped: no `.ks` file touches it, and `Trajectories/PluginData/` holds only `Textures`. So it is
whatever the in-game Settings tab happens to hold — **every F9I prediction to date was made without
anyone knowing which way it was set.** The user is right that it must be ON so the impact point
counts the planet turning underneath a descent that lasts minutes.

Set it explicitly from code and log the readback, for exactly the reason trap 2 exists: an inherited
default is not a setting, it is a coincidence. It may also be absorbing part of the residual error
the `dgAim*` constants currently soak up — worth checking against a flown mission before assuming it
changes nothing.

**Where the real schedule comes from: F9I's flown entry solution, not invention.** It lands ~6.3 km
and its entry steers a long-margin schedule with shorten-only + lead; the black-box recordings hold
the AoA actually flown at each altitude. Read those numbers off flight data. **Do not derive an entry
profile from first principles when several flights of it already exist.**

Soft dependency: wrap the API in reflection so the mod still runs when Trajectories is absent. MAS's
`KACWrapper.cs` (MIT) is the template for exactly this pattern — port it, do not invent it.

---

## LOCKED 2026-08-05 — the screens FLY the mission, and the mission is the REAL one

User's decision, and it settles the question below: **"the screens display, interact and fly the
mission, and I want the missions they fly to be as realistically accurate to the real thing as
possible."** That is reading **B**. MechJeb porting is genuinely in scope — ascent, rendezvous,
docking, deorbit, entry, plus the per-configuration attitude controller.

### Realism is the standard, and stock KSP gets FLUFFED where it must

| | stock KSP (build this first) | RSS / RO (later) |
|---|---|---|
| station orbit | **86.8 x 85.8 km, inc 0.133 deg** — measured, near-equatorial, the easiest rendezvous from KSC | the real ISS orbit, ~420 km at **51.6 deg** |
| launch site | the **installed "Falcon 9" pad** — TundraSpaceCenter statics placed by KerbalKonstructs (`TLC_36-instances.cfg` "Falcon 9 Launch Pad", `TLC_41-instances.cfg` "Falcon 9") | the real **LC-39A**, 28.6084 N, 80.6043 W |
| profile | the real Crew Dragon sequence, fluffed only where Kerbin forces it | the real sequence outright |

**The launch site already exists — do not build one.** Checked: KerbalKonstructs ships
`Falcon 9`, `Falcon 9 Launch Pad` and `Starship` sites, plus `LandingZone2` / `Fossil_LZ2` for booster
returns. There is a **39B** (used for Starship) but **no 39A**, which is the real Crew Dragon pad;
authoring a 39A analogue is optional polish, not a prerequisite for anything.

**Why the ISS orbit cannot simply be scaled:** ISS sits at 420 km over a 6371 km Earth, 6.6% of a
radius. The same fraction of Kerbin's 600 km is 39.6 km — *inside the atmosphere*. So a "relative
position" is not a scaling; the practical stock analogue is the existing near-equatorial station, and
that is what 0.133 deg is for.

### The S2 question — ALREADY REALISTIC (corrected 2026-08-05 by the user)

An earlier draft of this file claimed F9I diverges from the real profile by carrying the second stage
to the station. **That is out of date.** F9I now **ditches the S2 while still on a sub-orbital
trajectory** and the capsule refuels at the Space X station — which is the real sequence, where Dragon
separates ~12 minutes after launch and the S2 disposes of itself.

The `S2 ATTACHED` deorbit mode and `dgAimS2Crew` are **legacy**, not the current profile. The live
path is Draco-only (`dgAimDracoCrew = 270700`), so realism costs nothing here.

**The lesson, since I got this wrong from reading code alone:** the scripts contain both a current and
a superseded path, and nothing in the source says which one is flown today. Ask, or check the recent
flight logs — do not infer the mission profile from the presence of a code branch.

### The nose cone is HINGED, not jettisoned

User, 2026-08-05. The real Dragon 2 nosecone opens on orbit for docking and **closes before entry** —
it is never thrown away. The console button reads `JETTISON NOSE CONE`, which is Tundra's label, not
the vehicle's behaviour. **Check what the Tundra part actually models before wiring that button**: if
it only supports jettison, the honest choice is to drive the real animation if one exists and leave
the button inert if it does not, rather than making the screen perform an action the vehicle would
never take.

### Mission phases come from the REAL profile, not from KSP

`ACTIVE PHASE` currently shows `Vessel.Situations` — a KSP concept. For a screen meant to look and act
like the real thing it must read the mission phase:

    LAUNCH -> ASCENT -> SECO -> S2 SEP -> NOSECONE OPEN -> PHASING -> APPROACH -> DOCKED
    -> UNDOCK -> DEPARTURE -> NOSECONE CLOSE -> TRUNK SEP -> DEORBIT BURN -> ENTRY
    -> DROGUES -> MAINS -> SPLASHDOWN

Real numbers to hold it to: **drogues 18 000 ft / 5 486 m at ~350 mph; mains 6 000 ft / 1 830 m at
~119 mph.** F9I currently arms drogues at 7 500 m radar — about 2 km high.

## MechJeb — the scope question, now ANSWERED (B). Kept for the reasoning.

**Settled elsewhere and it matters here:** F9I **stays in kOS, all of it, all variants** (project
memory, settled 2026-08-05). F9I already flies ascent, boostback, booster landing, rendezvous,
docking, deorbit and entry.

So there are two very different readings of "build the flight systems":

| reading | what it means | size |
|---|---|---|
| **A. The screens COMMAND and DISPLAY** | Buttons drive real KSP state directly — RCS, SAS modes, translation via `FlightCtrlState`, docking port target and undock, chutes, nose cone, abort, lights, cameras — plus **one ported attitude controller** so "hold retrograde" actually holds. F9I still flies the automated sequences. | modest, and already the stated architecture |
| **B. The screens FLY the mission** | Ascent guidance, boostback, landing autopilot, rendezvous and docking all re-implemented in C#. | duplicates F9I entirely, in a second language |

**Recommendation: A.** The one MechJeb piece genuinely worth porting is the **attitude controller** —
that is what the request for "PID values tuned for full stack, booster, S2, capsule with and without
trunk" is really about, and it is what makes a button that says HOLD RETROGRADE tell the truth. The
five configurations differ enormously in inertia and control authority, so one gain set cannot serve
them; the tuning is per-configuration and belongs in `src/pure`, headless testable, with the
configuration detected from the vessel.

CLAUDE.md already says this mod must work with kOS absent, which A satisfies: the screens fly the
capsule; F9I flies the rocket.

**This is not settled — it is a question for the user.** Do not start porting ascent or landing
guidance on the assumption of B.

---

## The mission profile to fly — from the user's reference

[orbitalradar.com/spacecraft/crew-dragon](https://orbitalradar.com/spacecraft/crew-dragon), phases in
order. This is the spine the ACTIVE PHASE readout and the page set should follow:

    Launch -> Orbit insertion -> SECO -> Rendezvous -> ISS dock (IDA, autonomous:
    GPS, lidar, infrared, cameras) -> docked ops (up to 210 days) -> De-orbit burn
    -> trunk separation -> Re-entry -> drogues -> mains -> Splashdown

Vehicle facts worth having on the VEHICLE page: 8 × SuperDraco abort engines at **71 kN each**,
pusher configuration, Inconel, 3D-printed; **PICA-X** heat shield; trunk is expendable and burns up
on re-entry; 4.0 m diameter, 8.1 m tall with trunk, ~12 519 kg, 9.3 m³ pressurised.

The page is thin on numbers — no propellant quantities, no RCS detail, no burn durations. Treat it as
the phase spine, not as a source of values.
