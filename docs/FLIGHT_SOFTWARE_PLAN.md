> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-10; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.
> ⚠ Written 2026-08-06/10 under the original "MechJeb2 + Trajectories as a base" direction. §B12.1's pinned, privately-namespaced embed is the current form of that idea.

# Flight software — how we build it, and why this way

Research done 2026-08-06, in answer to: *use MechJeb2 and Trajectories as a base but tailor-made to
only what we need, for the whole flight including booster landings at selected LZs, so neither mod
needs to be installed, and as close to SpaceX's real methods as we can get.*

The short version: **that is not only possible, it is easier than referencing the mods**, because the
part of MechJeb worth having is already a separate, engine-free, permissively-licensed maths library.

---

## 1. The API question, answered

**Do not reference MechJeb2.dll.** It is GPL-3.0, it is welded to KSP and to its own GUI, and
referencing it makes it a hard install dependency — which contradicts the standing rule that this mod
works with kOS absent, and would apply equally to MechJeb.

**`MechJebLib` is the answer.** It is a separate project inside the same repo:

- **99 source files. Only 7 touch KSP or Unity**, and all 7 are `FuelFlowSimulation` (which needs
  part data by nature) plus one `Statics.cs`. The other 92 are pure maths.
- Its licence header is **not** the repo's GPL:

      SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+

  A "pick any of these" multi-licence — effectively public domain. Porting from it is unencumbered
  and does not even force GPL on us (we are GPL-3.0 anyway, which every one of those is compatible
  with). **Cite the file at each ported site regardless**, same discipline as the MAS ports.

**And it fits our architecture exactly.** Ninety-two engine-free maths files is precisely what
`src/pure` is for — they drop in and become headless-testable by the harness we already run on every
build. The pure/glue split we adopted for layout turns out to be the same split MechJeb's own author
arrived at for guidance.

### What we take

| MechJebLib | what it gives us |
|---|---|
| `ODE/` — Tsit5, DP5, DP8, BS3, **dense output, event detection** | our own trajectory predictor. **This is what replaces Trajectories.** |
| `PSG/` — `Problem`, `Optimizer`, `Phase`, `Terminal`, `AscentGuesser` | ascent guidance as a real optimal-control problem, not a pitch table |
| `HoverslamSimulation/` | the powered-descent / suicide-burn solve for booster landing |
| `TwoBody/`, `Lambert/`, `Maneuvers/` | phasing, transfers, rendezvous |
| `Rootfinding/`, `Minimization/`, `Interpolants/`, `Primitives/` | the numerical toolbox all of the above stands on |
| `FuelFlowSimulation/` | Δv and staging per configuration — needs a thin KSP adapter (one of the 7) |

**Trajectories is not ported and not referenced.** It is fundamentally "integrate the equations of
motion through the atmosphere and stop at the ground". We will have the integrator, the events and
the dense output; what we add is KSP's own atmosphere and drag, read from the game. Our own predictor
also removes the thing that made Trajectories awkward: its prediction is meaningless unless its
descent profile is set first, and ours will be driven by our own guidance rather than a UI setting.

---

## 2. What is genuinely SpaceX's method, and what is us being careful

Stated plainly so nobody later mistakes an analogy for a citation.

**Booster landing — this one is real and published.** Lars Blackmore and colleagues *at SpaceX*
published the convex-optimisation approach to fuel-optimal powered descent — "lossless
convexification" and G-FOLD (Guidance for Fuel-Optimal Large Diverts). It is the actual method family
behind Falcon 9's landings, and it is in the open literature. Two levels of fidelity are open to us:

1. **Hoverslam** — solve for the ignition point where a full-thrust burn brings velocity and altitude
   to zero together. This is what MechJebLib already has, and it is the correct *degenerate* case.
2. **Convexified powered descent** — the real thing: minimise fuel subject to thrust bounds, glide
   slope and a terminal position constraint, solved each cycle. Gives genuine **divert capability**,
   which is what "landing at a selected LZ" actually requires.

Start at (1), which lands. Move to (2) for LZ divert, because (1) cannot steer to a chosen pad — it
only stops you falling.

**Ascent** — Falcon 9 flies closed-loop guidance. PSG's primer-vector / optimal-control formulation
is the same family as the PEG-type guidance real launch vehicles use. Defensible as "the same
method", not as "their code".

**Entry** — Dragon 2 flies a *lifting* entry with an offset centre of gravity and steers by rolling
the lift vector: **bank-angle modulation**, the Apollo/Shuttle/Orion method. MechJeb has nothing like
this; it is ours to write, and it is the single most Dragon-specific piece of the whole system.

**Rendezvous** — Clohessy-Wiltshire relative motion for the close-in phase, which the F9I rules
already settled ("phasing orbit > 3 km, CW 0.5–3 km, RCS below").

---

## 3. What we write ourselves — the "tailor made" part

Porting maths is the easy half. These are what make it a Falcon 9 / Crew Dragon system rather than a
generic autopilot:

1. **A vehicle configuration table.** Full stack / booster alone / S2 / capsule+trunk / capsule. Each
   has its own mass, inertia, thrust, Isp and control authority, and therefore its own controller
   gains. This is the direct answer to "not a generic PID" — and it is already a known trap here:
   `falcon-booster-landing-twr` records that at 11% propellant the booster has TWR 0.81 on one engine
   and **cannot** land, so engine mode must be chosen from measured thrust before the landing solve.
2. **KSP atmosphere and drag**, read from the game, feeding the predictor.
3. **The mission sequencer.** The state machine that knows *Coast to Trunk Jettison* is the running
   step. Three separate things already wait on it: FLIGHT's step list, MECH's pyro timestamps, and
   any notion of the screens flying anything.
4. **Entry guidance** (bank-angle modulation) — see above.
5. **Boostback and LZ targeting** — choose a pad, compute the boostback burn, hand the terminal
   phase to the descent solver.

---

## 4. Validation: the corpus, measured rather than assumed

`quarantine/blackbox_flightdata` plus `Desktop/unread flight data` hold **554 recordings** —
**250 dragon, 160 upper stage, 144 booster** — at 5 Hz across **50 columns**, roughly **740,000
samples**, about 41 hours of flight. Split exactly along the three configurations we need to tune.

The columns are far richer than "36 raw values":

    met ut event status | alt radar lat lng vs gs air orbvel mach
    mass drymass availthrust maxthrust throttle | q pres pitch bank aoaSrf aoaRetro
    lf lfcap ox oxcap mono monocap | hasimpact implat implng tgtlat tgtlng
    apo peri inc | angvel ratePitch rateYaw rateRoll | ctlPitch ctlYaw ctlRoll rollErr

### ⚠ ONE COLUMN GROUP IS DEAD, AND IT IS THE ONE I NEARLY BUILT A CLAIM ON

**`ctlPitch`, `ctlYaw` and `ctlRoll` are zero in every row of every file checked** — twelve booster
flights, every sample. The reason is structural: F9I steers with kOS *cooked* steering
(`lock steering to …`), which drives KSP's own autopilot rather than writing `FlightCtrlState`, so
the recorder faithfully logged the raw control axes and they were never touched.

So **we do NOT have command/response pairs, and classical system identification is not available
from this corpus as recorded.** I was about to write that we could fit each configuration's
rotational transfer function directly from data. We cannot. Checked, not assumed.

### What the corpus IS strong for

1. **Fitting and validating the aero model.** `alt, mach, q, pres, aoaSrf, mass, throttle` against
   the resulting `vs, gs`. This is the input the predictor needs and there are hundreds of thousands
   of samples of it, across the whole speed and altitude range.
2. **Validating the predictor against two references at once.** `hasimpact/implat/implng` is
   *Trajectories' own prediction*, logged live — and the recording continues to the actual landing.
   So every flight gives us both "what the mod predicted" and "what really happened". About 83% of
   sampled rows carry a prediction; that is tens of thousands of scored comparisons, headless.
3. **Real numbers for the configuration table.** `mass, drymass, availthrust, maxthrust` per vehicle
   across 554 flights — including the case `falcon-booster-landing-twr` records, where the booster
   at 11% propellant has TWR 0.81 on one engine and cannot land.
4. **Bounding control authority.** We have `angvel` and the three rates but not the command, so we
   cannot identify a plant — but differentiating the rates gives achieved angular acceleration per
   configuration, which is enough to size gains sensibly rather than guess them.

### And the gap closes itself

Our controller will write `FlightCtrlState` directly, which is exactly what those three dead columns
record. **The moment our attitude controller flies, the corpus starts capturing command/response** —
and system identification becomes available for the tuning pass, on real flights, for free.

Ground truth to score against, from the F9I records: boosters landing **0.34–0.56 m** from the pad,
the Dragon entry solution at **331 m**, and the one that bites — **a Kerbin degree is 10,472 m, not
Earth's 111,320**.

This all runs headless. Same argument as the PNG preview, applied to guidance: **restarts are the
scarce resource**, so anything judgeable outside the game must be judged there.

---

## 5. Order of work, each step landing something visible

Sequenced so no stage is a long march with nothing to show.

| # | step | what appears on the screens |
|---|---|---|
| 1 | Port the numerical core — `ODE`, `Primitives`, `Rootfinding` — into `src/pure` | nothing yet; fully headless tested |
| 2 | Predictor = integrator + KSP atmosphere | **SPLASHDOWN TIME becomes real** (it is a crude interim today); NAV gains the projected track and impact point |
| 3 | **The sequencer** | FLIGHT's step list and Crew Deorbit Preparation checklist; MECH's pyro timestamps |
| 4 | Attitude controller, per-configuration gains | the first thing that actually flies; DOCKING's RCS rings show demand it is generating |
| 5 | Ascent (PSG) | launch under our own guidance |
| 6 | Boostback + powered descent + LZ select | booster landings; an LZ picker on NAV |
| 7 | Entry guidance (bank-angle) | the Dragon half of the mission |
| 8 | Rendezvous and docking | closes the loop with the DOCKING page |

**Step 2 is the one to do first after the core**, because it deletes a dependency, replaces an
interim model we have already flagged as dishonest, and shows up on two pages immediately.

---

## 6. Risks, named now

- **PSG is a solver, not a formula.** It has an optimizer, an initial-guess generator and convergence
  behaviour. Ported carelessly it will fail to converge and there will be no obvious reason why. Port
  it *with* `MechJebLibTest` — the repo ships tests, and they are the fastest way to know the port is
  faithful.
- **`FuelFlowSimulation` needs the KSP adapter** and is the one piece that is not clean maths.
- **Convex optimisation is a real dependency question.** G-FOLD needs an SOCP solver. MechJeb ships
  `alglib`; whether that covers it needs checking before promising divert capability.
- **Do not let this become a second UI.** The screens are the interface. Guidance is a library the
  sequencer drives and the pages display — the same pure/glue rule, or it will grow its own windows.
