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
