# Rendezvous research — why flight_0820 ran the tank dry

Started 2026-08-20 from flight_0820_203834 (full flight, stopped when mono hit 0 at 210 m from the
station). Grounds the diagnosis in the flight data, then checks it against the two authorities: the
real mission profile (`REAL_CREW_DRAGON_MISSION.md`) and MechJeb's proven autopilot
(`Desktop/mechjeb_src/MechJeb2/MechJebModuleRendezvousAutopilot.cs`).

## 1. What the tank actually paid for (from the data, not the labels)

Mono started the rendezvous at 185.6 and hit 0 at met 4848, **210 m from the station** — it very
nearly made it. The `m_rndz` label is worthless here: `ApproachLeg.MatchOrbit = 0` is the enum's
DEFAULT, so the recorder logs "MatchOrbit" whenever `Leg` is sitting at zero between assignments. Go
by the orbit and the mono instead:

| met | our orbit | range | closing | mono | what |
|---|---|---|---|---|---|
| 251–3006 | **133 × 120 (steady)** | 88 → 18 km | −13 → +72 → +1 | **177.9 (flat)** | **free coast, ZERO burns** |
| 3006–4257 | 133→**162**→136→**166**→145 | 18 → 23 → 15 km | swinging | 177.9 → **35.3** | **137 mono thrashing the orbit** |
| 4257–4848 | 145 → 119 × 89 | 15 → 0.2 km | +30 → +25 | 35.3 → **0** | DIRECT closing, ran dry at 210 m |

The story in one line: **the capsule coasted for free to ~18 km (correct, 0 mono), then burned 137
mono raising and lowering its own orbit — apoapsis bounced 133→162→136→166→145 km, AWAY from the
~120 km station — instead of matching velocity at the close pass the coast had already delivered.**
By the time DIRECT took over there were only 35 units left, and it emptied them getting to 210 m.

14 distinct burn pulses. The closest approach reached was 49 m — the geometry was never the problem.

## 2. Root cause — phasing is checked BEFORE the free intercept

`StationApproach.Tick` runs the legs in this order (`StationApproach.cs:337-364`):

```
LEG 0  MatchAltitude()   // latched (altMatchDone) — fires once, not the culprit
LEG 1  FlyPhasing()      // if !reachedGate && |AlongTrack| > PhaseMinM(3km) && phasePass < 3
LEG 2  RideIntercept()   // ride an existing close pass, match velocity there
```

With the along-track gap over 3 km and `reachedGate` still false, **LEG 1 wins every time** — so it
plans phasing laps (each a big orbit change: apoapsis to 162–166 km) and never reaches LEG 2, which
is the leg that would have matched velocity at the 18–22 km pass the coast handed it for free.

This directly contradicts the file's own stated design (`Approach.cs:113-118`): *"F9I checks the
existing closest approach BEFORE planning anything, and if it is good enough it warps there and
matches velocity instead."* The comment describes intercept-before-phasing; **the code does
phasing-before-intercept.** `CaUseMaxM` is 27 km and the closest approach was ~18–22 km — well inside
the ride-it threshold — but the phasing leg spent the tank before that check ever ran.

Secondary fault: the final approach ran with **two controllers contending** (`x_owner:
CONTENDED:node`, 1710 rows over met 4419–4933) — the DIRECT approach controller and `NodeExecutor`
both on the stick, burning the last 35 units.

## 3. What the authorities say the shape should be

**`REAL_CREW_DRAGON_MISSION.md`** — the real profile is a **named Hohmann-family burn sequence with
hold points**, explicitly *not* a range-classified proportional chase (its words, §Rendezvous). Each
burn has a job: Phase sets catch-up rate, Boost/Close/Co-elliptic establish a co-elliptic orbit a
fixed height below the station, Transfer starts the intercept, AI begins proximity ops. Proximity is
an **L** (R-bar up from below → V-bar in front → docking axis) with **holds** at WP0 (400 m below),
WP1 (220 m front), WP2 (20 m), and **offset targeting** so a misfire misses the keep-out sphere. The
doc's own gap table lists *all* of this as MISSING — we fly the ladder it says to replace.

**MechJeb's autopilot** — the practical algorithm, and its two anti-thrash rules are exactly what we
violate:
1. **Plan one node, execute it, THEN re-evaluate.** `MechJebModuleRendezvousAutopilot.cs:51-55`: it
   only classifies/plans when `maneuverNodes.Count == 0`. A live node is never re-planned against.
2. **Check the closest approach before buying a transfer.** Lines 72–95: if
   `NextClosestApproachDistance < SMA/25` (~29 km here) it **kills relative velocity at closest
   approach** — one burn — and only plots a Hohmann/phasing transfer when no close pass exists
   (line 111+). This is precisely the intercept-before-phasing our comment claims and our code skips.

## 4. Recommended fixes, in order

1. **Reorder: ride the free intercept before phasing.** Check `RideIntercept` (a close pass within
   `CaUseMaxM`) BEFORE `FlyPhasing`; phase only when no usable pass exists. This alone would have
   saved the 137 mono — the coast delivered an 18 km pass and the capsule should have matched
   velocity there, then closed. This is the smallest change that fixes *this* flight, and it aligns
   the code with both its own comment and MechJeb.
2. **Kill the final-approach contention.** One owner on the stick during DIRECT — hand `NodeExecutor`
   off cleanly when `DirectApproachOps` takes over.
3. **The real rework (bigger):** the named burn sequence + co-elliptic hold orbit + L-shaped R-bar→
   V-bar proximity with WP0/WP1/WP2 holds and offset targeting, per the mission doc. This is the
   correct long-term target; (1) and (2) make the current ladder survive until it's built.

## 5. Next step BEFORE writing any code

`falcon-rendezvous-approach-law`: **simulate in Python first.** Model this flight's orbit (133 × 120
against a ~125 km circular station) and confirm that riding the ~18–22 km closest approach + a small
intercept burn costs a fraction of the 137 mono the phasing legs spent — and that the reorder does
not strand a genuinely large along-track gap (where phasing IS required). Only then touch
`StationApproach.Tick`.

## Open question for the next session
The station is at ~120 km here, not the 86 km of the earlier ferry flights — so the real-mission
120 km altitude was adopted at some point. Confirm the station's exact current orbit before the
Python sim, because every threshold above (`CaUseMaxM`, `PhaseMinM`, `CoOrbitalTolM`) was fitted at
86 km and may need refitting at 120 km.

---

## 6. Python sim result (2026-08-20) — reorder is cheaper, but NOT sufficient alone

Reconstructed both orbits from flight_0820_203834 by fitting the station to the observed range curve
(rms 1.7 km): **capsule 133 × 120 km (e=0.009, period 2071 s); station ~circular 133 km (period
2099 s).** The 27.7 s period difference is a **61 km/orbit along-track drift** — the capsule was
catching up, which is why the coast closed 88 → 13 km for free.

- **Free drift closest approach: ~13 km. Relative velocity there: ~30 m/s (≈14 mono to match.)**
- **Phasing actually spent 281 m/s (137 mono) and diverged.**

So riding the pass is ~10× cheaper — the reorder's premise is confirmed.

### ⛔ But the reorder ALONE is a false fix — do not ship it in isolation
Tracing it through the state machine:
1. The DIRECT gate is `GateM = 10 km`; the free pass bottoms out at **13 km** — 3 km OUTSIDE the gate.
   So after matching velocity at 13 km the capsule is co-moving but still outside DIRECT.
2. `Leg.For`/dispatch then sees along-track 13 km > `PhaseMinM` 3 km → **falls straight back into
   phasing to close the last 3 km** — the same diverging phasing.
3. And that phasing diverges because `BuildState` feeds the solver the **INSTANTANEOUS** along-track
   (`alongTrack = s.Ry`, StationApproach.cs:631), which oscillates with the football. Each per-tick
   solve targets a different gap → plans a different burn → the orbit bounces 133→162→136→166 km.
4. The leg that SHOULD close a co-moving 13 km — an intercept-to-a-closer-point (Clohessy /
   MechJeb `DeltaVToInterceptAtTime`) — is DEAD (`StationApproach.cs:650`).

So a bare reorder trades one divergence for another. The correct fix is a coherent unit:
- (a) decide/phase on the **mean** along-track gap, not the instantaneous football sample;
- (b) reorder: ride a free intercept before buying a transfer;
- (c) revive the **intercept-to-closer** leg so a velocity match is followed by *closing*, not phasing —
  iterate {plot intercept → match at closest approach → close nearer} until inside the 10 km gate,
  one node at a time (MechJeb's structure, and the mission doc's named sequence);
- (d) single owner during DIRECT (kill the `CONTENDED:node`).

This is the **any-starting-orbit** structure. The current ladder cannot generalise: it is fitted to
one orbit, reads an oscillating gap, and its closing leg is dead.

---

## 7. General algorithm — SIM VALIDATED (2026-08-20). Sim in docs/rendezvous_sim/

Built a planar 2-body sim (`orbmech.py`: Kepler propagation + universal-variable Lambert, both
verified — Lambert recovers velocity to 2e-4 m/s) and ran the MechJeb-structured decision loop
(`algo.py`) from six very different starting orbits against a 133 km circular station. **All converge
to the 10 km gate (where DIRECT takes over), every one within the ~400 m/s tank:**

| start | separation | Δv to gate |
|---|---|---|
| **A — flight_0820 (133×120, catching up)** | 90 km | **22 m/s** |
| B — low circular 100 km, +0.6 rad | 425 km | 199 m/s |
| C — high circular 165 km, −1.5 rad | 1021 km | 243 m/s |
| D — eccentric 150×95, +2.5 rad | 1476 km | 303 m/s |
| E — co-orbital 133, 150° behind | 1416 km | 203 m/s |
| F — very low 80 km, +3.0 rad | 1409 km | 328 m/s |

The flight_0820 case is the headline: the ladder spent **281 m/s, diverged, and ran the tank dry at
210 m**; the general algorithm matches velocity at the close pass the coast delivers (inside the gate)
for **22 m/s** and hands to DIRECT.

### The validated decision loop (this is what the C# will implement)
At each decision point (no burn in flight), classify on the LIVE orbits and plan ONE burn:
1. `d < gate` and co-moving → **done** (DIRECT takes over).
2. `d < gate`, still moving → **match velocity now** (kill relv, hand off next tick).
3. `d < SMA/25` (~29 km) → if an imminent pass is sub-gate, **match at closest approach**; else
   **run in** — intercept a point one gate-radius short of the station, gently (Lambert, cap closing).
4. else, a close pass (< SMA/25) is coming → **coast to it and match velocity there** (the free
   intercept — this is the leg the current code skips).
5. else (far) → **phasing/Hohmann** to reduce the gap, transfers picked by **total** cost
   (burn-in + arrival match) so a ram-and-brake never looks cheaper than a gentle tangent arrival.

### What this maps to in the C# port
- Steps 1–4 are the CLOSE regime — the novel part. Step 3's "run in" is the intercept-to-closer leg
  that is currently DEAD; it must be revived. Step 4 is the intercept-before-phasing reorder.
- Step 5 (far) maps to the existing bounded `Phasing.SolveAdaptive`, once it is fed the **mean**
  along-track gap instead of the instantaneous football sample (`StationApproach.cs:631`), and once
  transfers are scored by total cost.
- Every burn is planned once and executed to completion before re-deciding (MechJeb's rule; kills the
  `CONTENDED` tangle).

NEXT: port to C# — revive the intercept-to-closer leg, reorder intercept-before-phasing, feed the mean
gap, single-owner during the run-in — with headless tests as the per-step check.

---

## 8. Port — STAGE 1 (the reorder) DONE, installed (DLL e875c2bb)

`StationApproach.Tick` now checks **RideIntercept before phasing** (`StationApproach.cs`, "LEG 2
BEFORE LEG 1"). Consequence for flight_0820: at 88 km it plans the velocity match at the 13 km pass
the coast delivers, instead of phasing an oscillating gap. That match also **collapses the football**,
so the phasing that closes the residual runs on a clean gap.

Why this is likely sufficient on its own: `FlyPhasing` already rides a pass mid-coast
(`StationApproach.cs:747`, added 2026-08-20). So the reorder gives the sim's iterate for free —
match at pass → phase a little → mid-coast ride the next pass → match → … → gate → DIRECT. All
headless suites pass.

### Remaining stages — deferred until a flight confirms Stage 1 (one validated change per flight)
- **Stage 2 — dedicated run-in (revive `FlyCw`, bounded).** Faster than phasing-to-gate, but reviving
  dead code with a clean gate handoff (the CW transfer crosses the 10 km gate; DIRECT must not engage
  mid-transfer) is real risk. Only worth it if Stage 1's phasing-to-gate proves too slow.
- **Stage 3 — mean gap + eccentricity in `MatchAltitude`.** `alongTrack = s.Ry` is the instantaneous
  football; and `MatchAltitude` circularises on SMA only, so an eccentric-but-SMA-matched arrival with
  no pass inside 27 km could still thrash. The reorder + bounded phasing contain it; a principled fix
  circularises to collapse the football before phasing.
- **Stage 4 — single owner in DIRECT.** The `CONTENDED:node` on final approach is inside
  `DirectApproachOps`, independent of the reorder.

NEXT: fly a rendezvous with e875c2bb; expect a velocity match at the first close pass and a cheap
convergence (~tens of m/s), NOT a phasing thrash. Read the mono ledger; then decide Stage 2/3/4.

---

## 9. Flight with Stage 1 (e875c2bb): DOCKED, but still thrashed. Stage 2 fix installed (22a6d8a6)

flight_0820_221434 **docked** (m_dock=Docked, closest 14 m) with mono to spare - first successful
dock. But the reorder did NOT give the cheap convergence the sim promised:
- Free coast closed 91 -> 15 km with mono FLAT at 180 (correct drift).
- Then it still bounced the orbit 133 -> 157 -> 152 -> 150 -> 142 km at ~13 km, burning **82 mono**,
  before DIRECT. DIRECT then spent **54 mono** over the last 10 km with **2768 CONTENDED ticks**.

Root cause (from the trace): during the ENTIRE coast `x_owner=node`, `Leg=MatchOrbit`, `warp=100`.
**MatchAltitude planned a circularise node ~2800 s ahead and the executor warped to it, holding the
tick the whole coast** - so RideIntercept (now before phasing, but AFTER MatchAltitude) never ran, and
the 15-25 km free passes were ignored. The reorder was correct but bypassed.

### Stage 2 (installed, 22a6d8a6)
1. **Node ownership** (`nodeOwner`): a node in flight is routed back to the leg that planned it, so no
   other leg grabs it and a warp to one leg's node cannot starve the others.
2. **RideIntercept BEFORE MatchAltitude**: ride a free pass the drift delivers before committing to a
   circularise warp. A velocity match already co-orbits us, so MatchAltitude only runs when no pass is
   on offer. This is what the sim actually does for a catching-up start (it never circularises first).

Expect next flight: a velocity match at the first close pass (~tens of m/s), NOT the 82-mono orbit
bounce. Still pending: DIRECT's 54-mono/2768-CONTENDED final approach (Stage 4, single owner).
