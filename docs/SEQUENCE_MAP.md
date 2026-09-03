> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGH (the conductor's sequence spec)**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-29; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# SEQUENCE MAP — mission phases, state-aware resume, and the abort decision matrix

> ⭐ **MANDATE (Chris 2026-08-29):** a clear map of **what to do and when**, with **all the alternate paths per
> phase** and **the abort choices the autopilot can pick at each point**. The autopilot must **know where it is
> and what to do next** — pressing AUTO SEQUENCE in orbit must work out *rendezvous vs deorbit*, never restart
> the launch.

**This exists for real Crew Dragon.** SpaceX/NASA fly to *flight rules* + an *abort mode matrix*: the mission is
a fixed phase sequence, and at every instant an **abort mode** is armed and called out (e.g. "Dragon, you are GO
for Mode 2b"). Crew-1 had **8 abort modes — 1 pad + 7 in-flight**, each with its own splash zone, switching as
the vehicle gains energy, ending in **Abort-To-Orbit** (mode 2e) in the last seconds of ascent. Sources:
[NASASpaceFlight — Crew Dragon launch abort modes](https://www.nasaspaceflight.com/2020/05/examining-crew-dragons-launch-abort-modes-and-splashdown-locations/),
[Wikipedia — Crew Dragon Launch Abort System](https://en.wikipedia.org/wiki/Crew_Dragon_Launch_Abort_System),
[everydayastronaut IFA](https://everydayastronaut.com/falcon-9-block-5-crew-dragon-in-flight-abort-test/).
Our `pure/AbortResponder` + `AbortControl` already implement this concept; this doc is the authoritative map.

---

## §1 — The nominal phase sequence (the `ModeManager.Plan`)

GATE = hold for the crew's GO; FLY = an L3 controller flies until it reports complete. ISS-crew profile:

| # | Step | Kind | Real Crew-2 analogue |
|---|---|---|---|
| G1–G7 | Ingress · suit leak · hatch · GO prop-load · **ARM LES** · internal power · **GO launch** | Gate | T-4h → T-45m countdown polls |
| Ascent | Ascent to orbit (max-Q → MECO → S2 → SECO → Dragon sep) | Fly | T-0 → T+12 min |
| Phasing | Phasing to Approach Initiation (Hohmann + co-elliptic) | Fly | hours of orbit-raising |
| G9 | GO for Approach Initiation | Gate | AI go/no-go |
| Approach ×3 | WP0 (400 m below) → WP1 (~220 m front) → WP2 (20 m) | Fly + G10/G11/G12 gates | the R-bar/V-bar L-approach |
| Docked | soft → hard capture | Fly | contact + capture |
| G13 | Docking complete — vestibule | Gate | leak checks, hatch |
| Docked | crew aboard (station ops) | Fly | days–months |
| **UNDOCK** (button) | crew releases the hooks when ready | — | manual undock command |
| G14 | GO for undock | Gate | undock go/no-go |
| Phasing (return) | **Departure** & phasing (careful KOS backaway → co-elliptic → phase) | Fly | departure burns |
| G15 | GO for deorbit burn | Gate | deorbit go/no-go |
| Entry | deorbit → lifting bank entry | Fly | deorbit → EI → entry |
| Drogues | drogues → mains → splashdown | Fly | 18 kft / 6 kft → splash |

Free-flyer profile (no ISS): countdown → ascent → **Coast (free-flight dwell)** → G15 deorbit → entry → chutes.

---

## §1A — THE NOMINAL PARAMETER ENVELOPE (what "nominal" MEANS, per phase)

> ⭐ **MANDATE (Chris 2026-08-29):** the parameters the autopilot must follow to be called **nominal**. "Nominal"
> = the vehicle stays inside this envelope for the WHOLE mission; a deviation past a bound is off-nominal and is
> either a **tune target** (a knob to correct) or an **abort trigger** (§3). These are the **real Crew Dragon**
> figures — RSS/RO reproduces the environment so they are used DIRECTLY (no Kerbin scaling). They are the
> numeric form of the `PHASE_ACCEPTANCE_CRITERIA.md` PASS gates and become the Tier-3 corpus-regression asserts.
> Confidence tags: **[P]** cited primary/authoritative · **[E]** established F9/Dragon figure · **[D]** design
> technique · **[~]** approximate / flight-calibrated. "Our knob" = the tunable or gate that enforces it.
> Full derivations + sources: the `LAUNCH_AND_ASCENT`, `CREW2_RSS_RESEARCH`, `PHASE_2..6_*`, `ABORT_PROCEDURES`,
> `CREW_DRAGON_GNC` research docs + `data/crew_missions.json`.

### Countdown (T-) — the launch gates + the pad-start rule
| Parameter | Nominal | Our gate/knob |
|---|---|---|
| Gate MET (Crew-1 PDF) | G1 ingress **T‑2:35:00** · G2 suit leak **T‑2:14:00** · G3 hatch **T‑1:55:00** · G4 GO prop-load **T‑45:00** · **G5 LES ARMED T‑37:00** · G6 internal power **T‑5:00** · G7 GO launch **T‑45 s** · ignition seq **T‑3 s** [P] | `CrewGates` G1–G7 |
| Pad start | ignite S1 at **T‑3 s** → spool while clamped → **release clamps only at thrust ≥99% AND no failed engine** (T‑0); any out-of-envelope reading → **pad safe-abort** (shut down intact) [P] | clamp-release gate; `SafeHold` |
| Ullage before every light | RCS-settle to **LowestUllage ≥ 0.996** then ignite [P] | ullage gate |

### Ascent — first stage (pad → MECO)
| Parameter | Nominal | Our knob |
|---|---|---|
| Liftoff TWR | **1.4–1.6** [E] | — |
| Pitch kick | ~**T+10 s**, small (DM-1 onset ~**+5.3°/s**) → hold ~**79°** to ~T+45 [P/D] | `FinalPitchDeg`/`TurnShape` |
| Gravity turn | **zero-AoA** (fly the velocity vector → aero side load ≈ 0); mean pitch rate ~**−0.287°/s** → ~**46.6° at MECO** [P/D] | pitch program, roll ref |
| Max-Q | **30–35 kPa** @ Mach ~1.5, **11–13 km**, ~**T+58–62 s**; throttle **bucket down** through it [E] | max-Q throttle |
| Mach 1 | ~**T+69 s** [P] | — |
| S1 g-cap | **≤ ~3.2–3.5 g** (throttle down near MECO) [P] | `S1GLimitG` = 3.5 |
| MECO | ~**T+2:36 (156–158 s)**, ~65–80 km, ~**1.7 km/s** surface [P] | staging energy |
| Frame checks | inc **prograde** & → target; pointing p95 **< 0.5°**; no barrel-roll | `SteerSign`, `LaunchNodeSign` |

### Staging + second stage (MECO → SECO → Dragon sep)
| Parameter | Nominal | Our knob |
|---|---|---|
| Stage sep | **MECO + 3 s** (~T+2:39), pneumatic, **direct decoupler** (never staging) [P] | `SeparateBooster` |
| S2 ignition (SES-1) | **MECO + 8–11 s** (~T+2:47), after ~8 s **plume-clearance coast**, ullage-settled [P] | `IgniteSecondStage`, retry within budget |
| S2 g-cap | **≤ ~4.5 g** near SECO (throttle down) [P] | `S2GLimitG` = 4.1 |
| SECO-1 | ~**T+8:47 (527–530 s)**; insertion **~190–210 × ~300 km @ 51.6°** (interim target ~200 km circular) [P] | UPFG cutoff, target plane normal |
| RCS during S2 | **OFF** the whole burn (gimbal steers) [P] | `DisableRcs` on ignite-confirm |
| Dragon sep | ~**T+11:58 (718–723 s)**; **nose cap opens** ~T+13:02 [P] | `SeparateSecondStage`, nose shroud |
| Ignition budget | S1 & S2 = **4 each**; booster recovery spends 3 of S1's (ascent/entry/landing) [P] | ignition accounting |

### Booster recovery (runs CONCURRENTLY, droneship downrange)
| Parameter | Nominal | Our knob |
|---|---|---|
| No boostback | droneship ~**500 km** downrange; flip **retrograde on cold-gas N₂** [P/E] | — |
| Entry burn | ~**T+7:27**, **3 engines (ThreeLanding)** → ~**1300 m/s** [P] | mode absolute-while-off |
| Grid fins | deploy ~**70 km**; AoA cap **~15–20°**; **aim-to-MISS until all nominal** [P] | AoA cap, `CrossSign` |
| Landing burn | ~**8 km**, **CenterOnly (1 engine, ONE ignition — no mid-burn 3→1)**, hoverslam **v=0 @ h=0**, min throttle ~40% [E] | ignition alt, `k`≈0.9 |
| Touchdown | legs deploy in the final seconds, **~0–2 m/s**; ~T+9:30. RSS droneship at **31.5599°N, −76.6800°E** [P] | `DroneshipEarthLat/LonDeg` |

### Rendezvous / phasing (16 Dracos only — SuperDraco NEVER nominal)
| Parameter | Nominal | Our knob |
|---|---|---|
| Prop | 16 × **Draco 400 N, Isp 300, MMH+NTO** [P] | — |
| Named-burn order | **Phase (~T+46 m) → Boost (~T+15:53, overnight) → Close → Transfer (~48 s) → Coelliptic (~37 s) → 30 km rendezvous-complete (~T+1d00:32) → OOP (if needed) → G9 GO for AI → AI-burn @ 7.5 km (90 s, 0.72 m/s) → Midcourse** [P] | phasing FSM |
| Total duration | **~24–28 h** (slow multi-orbit; NOT a 1-orbit rendezvous) [P] | — |
| Co-elliptic offset | ~**10 km below** the ISS [D] | `CoEllipticBelowM` |
| AI standoff | **7.5 km behind & below**, **~96 min** before dock [P] | AI trigger range |
| Safety invariants | **offset targeting mandatory** (never aim at the station); free-drift passive-abort misses the KOS; **Pe floor ≥ 150 km gates EVERY burn** [P/D] | `ForwardSign`=−1, `SafePeFloorM`=150 km |
| CW validity | terminal only (< ~tens km); far field = **Hohmann prograde raise** (can't deorbit); hand to CW at ~**100 km** [D] | `CwHandoffRangeM`, `CwMaxRangeM`=200 km |

### Proximity operations & docking (autonomous; crew monitors)
| Parameter | Nominal | Our knob |
|---|---|---|
| Approach Ellipsoid | **2000 m** (V-bar semi-axis) × **1000 m** orthogonal = the "4×2 km" [P] | AE gate |
| Keep-Out Sphere | **200 m RADIUS**; any breach/off-corridor → **auto-abort/retreat with a positive opening rate** [P] | KOS auto-abort → `KosRetreat` |
| Approach shape | an **L, not a line**: up the **R-bar** → onto the **V-bar** → in along the axis; every WP a **station-keeping HOLD + GO** [P] | L-approach FSM |
| WP0 | **400 m directly BELOW** (R-bar) → **G10** [P] | WP0 |
| WP1 | **~150–220 m in FRONT** (V-bar) → **G11** [P] | WP1 |
| WP2 | **20 m** from the port → **G12** [P] | WP2 |
| Closing rate | **glideslope taper** — cap decreases with range, monotone; ≤ IDSS at contact [D] | `MaxSpeedForDistance` |
| Discipline | **null lateral BEFORE closing**; **attitude-first-then-translate** (no reaction wheels); **nose shroud open** first [P/D] | `RcsRight/Up/FwdSign` |
| **IDSS contact envelope** (IDD Rev E, Table 3.3.1.1‑2) — the box the guidance must deliver Dragon INTO at first contact [P]: | closing (axial) rate **0.05–0.10 m/s** · lateral (radial) rate **≤ 0.04 m/s** · pitch/yaw rate **≤ 0.20 °/s** (vector sum) · roll rate **≤ 0.20 °/s** · lateral misalignment **≤ 0.10 m** · pitch/yaw misalignment **≤ 4.0°** (vector sum) · roll **≤ 4.0°** | terminal 6-DOF servo tolerances |
| Capture | **soft capture** (ring + 3 petals + latches absorb the residual) → **hard capture** (12 hooks) ~10 min later → **G13** [P] | `DockedSide.Docked` |

### Undocking & departure (reverse of the approach)
| Parameter | Nominal | Our knob |
|---|---|---|
| Undock | GO for undock → **hooks open, umbilicals retract, 2 tiny separation burns** [P] | `Undock` button → `MarkDockedThisMission` |
| 4 departure burns | **Burn 0** (~16 s, up & around) → **Burn 1** (~20 s) → **Burn 2** (~44 s, ~50 min after B1) → **Burn 3** (~1 min) → stable **~10 km below** the ISS [P] | Departure FSM |
| Departure phasing | **~6–9 min** low-thrust Draco → sets the **ground track for the splash zone** [P] | phasing burn |
| Safety | corridor-safe — a missed burn does NOT re-enter the 200 m KOS [D] | offset targeting |

### Return — deorbit → entry → splashdown
| Parameter | Nominal | Our knob |
|---|---|---|
| Trunk jettison | **BEFORE** the deorbit burn (sheds mass, clears the shield) [P] | trunk decoupler |
| Deorbit burn | Dracos, **~12–16.5 min** (Crew-1: **987 s**), **closed-loop on measured Pe** (stop AT the target Pe, not past) [P] | deorbit `ForwardSign` + target Pe |
| Entry corridor | target **EI flight-path angle ~−1.5° class** at EI **~120 km** (LEO-capsule shallow corridor: too shallow → skip-out, too steep → over-g/over-heat) — **flight-calibrated to our vehicle; exact SpaceX value not public** [~] | deorbit Pe / γ_EI |
| Attitude | **heat-shield forward**; **CoM shifter Descent Mode engaged ONCE** [P] | CoM `OffsetPercent` |
| Lifting entry | offset-CoM trim AoA **~12°**, **L/D ≈ 0.18–0.27**; bank σ: vertical lift **L·cos σ = downrange**; **bank reversals (S-turns)** on a velocity-dependent crossrange deadband [P] | `RollSign`/`RollRefSign`/`CrossSign`, `RollKp` |
| Peak entry g | **~4–5 g** nominal; comms blackout ~6 min [P] | g-abort DISABLED on entry |
| Drogues (2) | **~5486 m (18 kft)**, **~156 m/s (350 mph)** [P] | drogue alt gate |
| Mains (4) | **~1830 m (6 kft)**, **~53 m/s (119 mph)**; safe on **3 of 4** [P] | main alt gate |
| Splashdown | **~5–8 m/s**; one of **7 Florida zones** — Gulf: Pensacola/Panama City/Tampa/Tallahassee · Atlantic: Cape Canaveral/Daytona/Jacksonville. Crew-2 = **Gulf off Pensacola** (~30.2°N, 87.2°W approx) [P/~] | water-scan / target zone |

### Abort — g-loads & mode boundaries (the backstop, NOT the trigger — full matrix §3)
| Parameter | Nominal | Our knob |
|---|---|---|
| Felt g by phase | S1 **3.2–3.3 g** · S2 **≤4.5 g** · coast **~0 g** · SuperDraco escape **~3.3 g** · lifting entry **~4 g** · ballistic/contingency **4–8 g** [P] | — |
| Real abort trigger | a **DETECTED anomaly** (loss of thrust/control, structural/aero-limit), **NOT raw g** — SpaceX proves it at max-Q [P] | AoA-runaway + structural-Q in `AscentControl`; FDIR |
| Structural backstop | **6.0 g, 0.5 s dwell**; **DISABLED on entry** (4–8 g is nominal there) | `StructuralAbortG`, `StructuralAbortDwellS` |
| Contingency SuperDraco deorbit | g-limited **3.5 g**, stops at the safe entry-corridor Pe | `DeorbitGLimit`=3.5 |
| Mode boundaries | pad/ascent = **LaunchEscape** · **late-ascent near-orbital = Abort-To-Orbit (Dracos to a safe orbit, do NOT splash)** · on-orbit = **DeorbitReturn** · prox-ops = **KosRetreat** · entry = **RideItDown** [P] | `AbortResponder` |
| Universal sequence | escape → **TRUNK JETTISON → Draco reorient shield-forward** → (coast) → drogues → mains → splash [P] | `AbortControl` |

### ⭐ Holes FILLED this pass (online research, 2026-08-29)
- **IDSS soft-capture contact envelope** — the research had the *concept* (deliver Dragon inside the capture
  envelope at ~8 cm/s) but **no numeric box**. Now pinned to the primary standard, **IDSS IDD Rev E,
  Table 3.3.1.1‑2** (closing 0.05–0.10 m/s, lateral ≤0.04 m/s, rates ≤0.20 °/s, lateral ≤0.10 m, angular ≤4°).
  Our ~8 cm/s contact target sits inside it. → the docking terminal-servo tolerances now have a real acceptance box.
- **Splashdown zones** — was "need the exact Pensacola lat/lon." Now: **7 pre-surveyed Florida zones** (Gulf +
  Atlantic), primary/alternate chosen ~2 days before return; Crew-2 = **Gulf off Pensacola**. Exact offshore
  coordinates are not published (they're weather-selected recovery boxes) → the WATER-DEORBIT target is a zone,
  approximated ~30.2°N/87.2°W, tuned to our RSS ocean.
- **Entry-corridor FPA** — bounded to the **LEO-capsule shallow class (~−1.5°)** vs the lunar-return −5.86°
  (Orion). The exact SpaceX EI FPA is **not public** — flight-calibrated to our vehicle's L/D.

### ⚠ Honesty log — genuinely NOT-public (flight-calibrated, not researchable further)
The exact SpaceX **guidance law**, **throttle/pitch schedules**, **abort-mode T+ boundaries**, the **approach-
corridor cone half-angle**, and the **exact splash coordinates** are not published. The values above are the best
authoritative figures; the residual precision is tuned from our own recordings (the DB), not invented.

**Sources (this pass):** [IDSS IDD Rev E — NASA/NTRS 20170001546](https://ntrs.nasa.gov/api/citations/20170001546/downloads/20170001546.pdf) (Table 3.3.1.1‑2 initial contact conditions) ·
[NASA — SpaceX Crew Rescue & Recovery (7 splash zones)](https://www.nasa.gov/humans-in-space/nasas-spacex-crew-rescue-and-recovery/) ·
[NASA names Gulf off Pensacola splash site](https://weartv.com/news/local/nasa-names-gulf-off-pensacola-as-potential-splashdown-site-for-crew-dragon) ·
[Orion PredGuid entry from LEO (EI FPA / corridor)](https://ntrs.nasa.gov/api/citations/20110013203/downloads/20110013203.pdf) ·
[The Planetary Society — ISS approach zones (AE/KOS)](https://www.planetary.org/space-images/international-space-station).

---

## §2 — STATE-AWARE RESUME (the "know where I am" map) — `CrewProcedureOps.ResumeIndex`

When AUTO SEQUENCE is pressed, the LIVE vessel state picks the resume step. **It never restarts the launch when
the vehicle is already in space.** (This is the real flight-rules idea: the phase is a function of where the
vehicle physically is.)

| Live state (Vessel.situation + orbit) | Resume at | Why |
|---|---|---|
| PRELAUNCH / LANDED / SPLASHED (at rest) | **countdown (0)** | on the pad → fly the whole mission |
| In atmosphere, descending, srf-speed > 300 | **Entry** (ride it down) | already re-entering |
| In orbit, pe < atmosphere, descending | **Entry** | committed to entry |
| In orbit, **docked** or docked earlier this mission | **Departure** (returnLeg) | station business done → careful KOS backaway → return |
| In orbit, NOT docked, ISS crew, station targeted | **Rendezvous** (outbound Phasing) | ⛔ the fix — go rendezvous, do NOT re-launch |
| In orbit, otherwise (free-flyer done / no target) | **Deorbit gate (G15)** | come home |
| Sub-orbital / FLYING, ascending | **Ascent** | still climbing |
| Sub-orbital / FLYING, descending | **Entry** | falling |

`dockedThisMission` (set on the UNDOCK press + live-dock detection, reset per scene) is the memory that
distinguishes "arriving to dock" from "leaving after dock." `returnLeg` is set whenever the resume is at/after
the undock gate, so the shared Phasing phase flies as departure and the return controllers own it.

⭐ **Fixes the "4 stranded" symptom:** a vessel left in orbit + AUTO SEQUENCE now resumes rendezvous or deorbit
by state, instead of sitting in "ASC/Done" or trying to launch.

---

## §3 — THE ABORT DECISION MATRIX (which abort the autopilot picks, by phase) — `pure/AbortResponder`

The abort mode is **latched from the live physical state at the instant of the abort** (it does not flip
mid-descent). Every path ends SAFE (splash / safe orbit / standoff). Real-mode parallel in the last column.

| Phase / state | Our abort mode | What it does | Real Crew Dragon mode |
|---|---|---|---|
| Pad / early ascent, **LES armed** | **LaunchEscape** | SuperDraco escape → drop stack → trunk jettison → open nose → Dracos hold shield-forward → compressed chutes → splash | Pad abort · Mode 1a/1b |
| Ascent, max-Q (~M 1.8, ~19 km) | **LaunchEscape** | the design-driver abort — SuperDracos at full | Max-Q abort |
| Ascent, first/second stage | **LaunchEscape** | SuperDraco → Draco reorient → chutes → downrange Atlantic splash | Mode 1c / 2a–2d |
| **Late ascent, near orbital energy** | **AbortToOrbit** | point prograde, thrust to a safe periapsis — do NOT splash | **Mode 2e (Abort-To-Orbit)** |
| On orbit (crew commands abort) | **DeorbitReturn** | trunk jettison → g-limited retrograde burn timed for the nearest safe splash → shield-forward entry → chutes | contingency deorbit |
| Docked (emergency) | **EmergencyUndock** | release hooks, back off, then DeorbitReturn | emergency undock + return |
| Prox-ops (inside the KOS) | **KosRetreat** | back out of the corridor to a safe standoff (an orbital abort is a RETREAT, not a launch escape) | breakout / retreat |
| Already entering | **RideItDown** | jettison trunk if attached → hold shield-forward → chutes | (entry has no abort — survive it) |
| Pad, LES not armed / nothing to escape with | **SafeHold** | safe the vehicle (shut down intact) | pad safe / scrub |

**The rescue buttons** (`DEORBIT NOW` / `WATER DEORBIT`) are the crew-commanded form of **DeorbitReturn** (a
controlled deorbit-and-land, no klaxon): DEORBIT NOW lands anywhere safe (gear after a land touchdown), WATER
DEORBIT targets the nearest open water (splashdown, no gear). The g-abort structural backstop (6 g, 0.5 s dwell)
is disabled during entry where 4–8 g is nominal.

---

## §4 — Alternate paths per phase (branch table)

- **Countdown gate NO-GO** → hold at the gate; the crew resolves and re-GOes, or scrubs (SafeHold).
- **Pad ignition fails ≥99% thrust** → **pad safe-abort** (octaweb shut, clamps held, no SuperDraco). ⚠ after a
  safe-abort REVERT, RealFuels has spent the octaweb's one ignition → a re-launch shows 0% until a full restart.
- **Ascent anomaly (loss of thrust / control, q·α, structural g)** → abort per §3 (LaunchEscape / AbortToOrbit).
- **Rendezvous can't converge / times out** → hold co-elliptic (never chase, never self-deorbit — the pe floor
  gates every burn); crew can DeorbitReturn.
- **Prox-ops KOS breach** → KosRetreat to a safe standoff.
- **Docking fault** → back to WP hold / KosRetreat / EmergencyUndock.
- **Return: deorbit can't point / burn fails** → hold, retry the slew; the pe floor prevents a wrong-way walk-down.
- **Entry** → RideItDown (no abort; the vehicle is built to survive entry) → chutes → splash.

---

## §5 — Open issues this map exposes (see `docs/ISSUE_REGISTER.md`)

- The DeorbitReturn attitude must reliably point retrograde (the geometric-torque over-estimate that caused the
  oscillation is fixed; verify the deorbit now completes).
- The water-site scan reads 0/130 over water in RSS (ocean detection) → WATER DEORBIT can't target water yet.
- The rendezvous must converge from the 100 km CW hand-off to docking (it reached 100 km but didn't dock).

**Related:** [ISSUE_REGISTER.md](ISSUE_REGISTER.md), [ABORT_PROCEDURES_RESEARCH.md](ABORT_PROCEDURES_RESEARCH.md),
`pure/AbortResponder.cs`, `pure/ModeManager.cs`, `CrewProcedureOps.ResumeIndex`, [[crew-mission-telemetry-database]].
