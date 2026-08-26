# Mission Phase 3 — Rendezvous (phasing up to the ISS)

Real-world facts only, trusted sources. Scope: from **Dragon separation into the low parking orbit** to
**arrival at the approach corridor** (~7.5 km → 2 km behind & below the ISS) — the named-burn phasing
sequence. Proximity ops + docking (approach ellipsoid, keep-out sphere, waypoints, capture) are Phase 4.

Tags: **[P]** cited/authoritative · **[E]** established · **[D]** technique/principle · **[~]** approximate.

Reference sequence: Crew‑4 (2022) named-burn T+ times, cross-checked to two independent sources. [P]

---

## 1. Setup after insertion
- Dragon separates from the second stage ~**T+12 min** into a **low parking orbit** (~190–210 km class,
  **51.6°**), deliberately **below and behind** the ISS (~420 km). [E]
- **Nose cap opens on orbit** — exposes the docking adapter and the forward Draco thrusters used for the
  approach. [E]
- **All rendezvous maneuvers are on the 16 Draco thrusters** (NTO/MMH; SuperDracos are abort-only). [P]
- **Total rendezvous ~16 h** (up to ~19–24 h by profile) — a slow multi-orbit phasing, *not* a fast
  1-orbit rendezvous, because the ISS plane and the launch site rarely line up for the fast option. [P]

---

## 2. The named burn sequence

Standard co-elliptic rendezvous flown as a **named-burn sequence**, each burn with a defined job. Crew‑4
T+ times shown for scale (they vary by mission). [P]

| Burn | T+ (Crew‑4) | Duration | Job |
|---|---|---|---|
| **Phase** | ~00:48 | ~4 min | sets the catch-up (phasing) rate — the first major maneuver after sep |
| **Boost** | ~09:42 | up to ~500+ s | raises the orbit (run overnight, during the crew sleep period) |
| **Close** (co-elliptic) | ~10:27 | ~10 min | raises the orbit further toward the ISS |
| **Transfer** | ~12:03 | ~48 s | departs onto the transfer toward the approach corridor |
| **Co-elliptic** | ~12:50 | ~37 s | establishes the **co-elliptic orbit** — a stable path a fixed height below/behind the ISS |
| **Approach Initiation (AI)** | ~14:46 | — | begins proximity ops from **7.5 km behind & below**, **~96 min before docking** |
| **Approach Midcourse** | ~15:11 | — | mid-transfer correction, ~25 min after AI |
| *(Contact & capture)* | ~16:22 | — | 16:22 − 14:46 = **96 min**, corroborating the AI standoff |

- **The co-elliptic burn "flattens" Dragon's orbit to match the ISS's shape** so the two run concentric a
  fixed height apart — the stable slow catch-up platform the terminal transfers depart from. [P]
- **Approach Initiation** is the handover from phasing to proximity operations: **7.5 km behind and below**,
  **96 minutes** out. [P]

---

## 3. The technique (co-elliptic rendezvous)
- A **standard co-elliptic / Hohmann-family rendezvous with hold points**, NOT a proportional chase: Phase
  sets the rate, Boost/Close raise the orbit, Transfer + Co-elliptic establish a concentric orbit below the
  station, AI begins the terminal approach. [D]
- **Offset targeting is mandatory** — from outside the approach ellipsoid the intercept is aimed so a
  *dispersed or failed* burn still misses the keep-out sphere; a visiting vehicle never aims at the
  station. [P — the "never chase a co-orbital target" rule]

---

## 4. The math

### 4.1 The frame — LVLH (target-centred)
- Relative motion is expressed in the target's **Local-Vertical / Local-Horizontal (LVLH)** frame,
  coordinates **(x, y, z) = radial, along-track, cross-track**. [P]
- **R-bar** = the radial axis (toward/away from Earth — the "below/above" the station line). **V-bar** = the
  velocity/along-track axis (in front of / behind). Terminal approaches run along these. [E]

### 4.2 Relative motion — Clohessy–Wiltshire (Hill) equations
- **CW equations (Clohessy & Wiltshire, 1960)** linearise the relative motion of a chaser about a
  **circular-orbit target**, for designing rendezvous control. With mean motion `n = √(μ/a³)`, the
  along-track/radial/cross-track motion has the closed **state-transition matrix** used to solve a
  **two-impulse transfer** (the Δv now that puts the chaser at an aim point in time T, and the arrival Δv
  to null relative velocity). [P]
- **Relative ellipses centred on the station** are the geometric structure of the approach (Shuttle/visiting-
  vehicle heritage). [P]

### 4.3 Large moves — Hohmann phasing
- The big orbit raises (Phase/Boost/Close) are **Hohmann-family prograde apsis burns**; a modern scheme
  **combines Hohmann transfers (coarse phasing) with CW targeting (terminal)** for efficiency and
  precision — coarse orbit raises to a co-elliptic, then CW two-impulse transfers into the corridor. [P/D]
- **Phase-angle geometry** sets *when* to fire a raise: the chaser (lower, faster) closes the phase angle
  to the station; the raise is timed so the transfer arrives with the station still ahead by a safe
  margin. [D]

---

## 4b. Guidance-law equations (build-level)

### Clohessy–Wiltshire (Hill) relative motion — target circular, `n = √(μ/a³)`
LVLH state `(x,y,z)` = radial, along-track, cross-track: [P]
```
ẍ = 3n²x + 2nẏ        ÿ = −2nẋ        z̈ = −n²z
```
Closed-form **state-transition matrix** `[δr(t); δv(t)] = Φ(t)·[δr₀; δv₀]`, blocks `Φrr,Φrv,Φvr,Φvv`:
```
Φrr = [ 4−3c        0   0 ]      Φrv = [ s/n        2(1−c)/n     0   ]
      [ 6(s−nt)     1   0 ]            [ 2(c−1)/n   (4s−3nt)/n   0   ]
      [ 0           0   c ]            [ 0          0            s/n ]
Φvr = [ 3n·s        0   0 ]      Φvv = [ c          2s           0   ]
      [ 6n(c−1)     0   0 ]            [ −2s        4c−3         0   ]
      [ 0           0  −n·s]            [ 0          0            c   ]
      where  c = cos(nt),  s = sin(nt)
```

### Two-impulse CW transfer (the terminal-burn solver)
To fly from relative position `δr₀` to a target `δr_f` in time `t_f` (aim `δr_f` = an OFFSET point, never
the station): [P]
```
solve δv₀⁺ from   δr_f = Φrr(t_f)·δr₀ + Φrv(t_f)·δv₀⁺   ⇒   δv₀⁺ = Φrv(t_f)⁻¹ ( δr_f − Φrr(t_f)·δr₀ )
first burn:   Δv₁ = δv₀⁺ − δv₀⁻            (δv₀⁻ = current relative velocity)
arrival vel:  δv(t_f) = Φvr(t_f)·δr₀ + Φvv(t_f)·δv₀⁺
second burn:  Δv₂ = −δv(t_f)              (null relative velocity at arrival → station-keep)
```
Sweep `t_f` and pick the cheapest transfer whose **free-drift** (if Δv₂ is never made) stays outside the
KOS (passive-abort safety). [D]

### Hohmann orbit raises + phase-lead (the climb burns)
Raise from `r₁` to `r₂` (`a_t = (r₁+r₂)/2`): [E]
```
Δv₁ = √(μ/r₁)·( √(2r₂/(r₁+r₂)) − 1 )      (prograde at r₁)
Δv₂ = √(μ/r₂)·( 1 − √(2r₁/(r₁+r₂)) )      (prograde at r₂)
t_H = π·√(a_t³/μ)
```
**Phase-lead to fire the raise:** target angular rate `ω₂ = √(μ/r₂³)`; the target sweeps `ω₂·t_H` during
transfer, so fire when the target is **ahead by** `φ_lead = π − ω₂·t_H` (normalise to [0,2π)); the phase
drifts at the relative rate `ω₁ − ω₂`, so `wait = (φ_now − φ_lead)/(ω₁ − ω₂)`. Aim to arrive with the
station still **ahead** by a safety margin (never overshoot a lower, faster orbit past the target). [D]

### Co-elliptic offset
Set the co-elliptic orbit a fixed height `Δh` (real ~10 km) below the station: co-elliptic radius
`r_co = r_target − Δh`; BOOST raises to `r_co`, CLOSE circularises there. [D]

## 4c. Real named-burn schedule + magnitudes (Crew-1 rendezvous timeline PDF, 2026-08-26)  [P]
The exact real burn schedule was extracted → `data/crew_missions.json` (`standard_iss_crew.rendezvous_docking`):
**Phase (+0:45:42) → Boost (+15:53) → Close (+16:35) → Transfer (+22:18) → Coelliptic (+23:04) → 30 km
"rendezvous complete" (+1d00:32) → OOP burn (if needed) → GO for AI → AI-burn at 7.5 km (90 s, 0.72 m/s) →
AI-Midcourse → WP0 (400 m below) → WP1 (~220 m) → WP2 (20 m) → GO for Docking → Contact/Capture →
Docking complete (+1d03:45).** ~28 h profile; the AI burn magnitude (90 s / 0.72 m/s) and the two crew
GO/NO-GO gates are real, not estimated. This confirms the named-burn order and gives build-level values.

## 5. Phase summary
Insert low & behind → **open nose cap** → **Phase** (set catch-up) → **Boost/Close** (raise the orbit,
overnight) → **Transfer + Co-elliptic** (concentric orbit a fixed height below the ISS) → **Approach
Initiation** at **7.5 km behind & below, 96 min out** → **Midcourse** correction → arrive at the approach
corridor (~2 km) and hand to proximity operations (Phase 4). All on the Dracos, all with **offset
targeting** so a failed burn misses the station, over ~16 h. Math: **CW two-impulse transfers in the LVLH
frame** for the terminal legs, **Hohmann phasing** for the orbit raises.

**Sources:** [Crew Dragon burn sequences to the ISS — Teslarati](https://www.teslarati.com/spacex-crew-dragon-burn-sequences-intl-space-station/),
[Crew‑4 docks at ISS (named burns) — NASASpaceFlight](https://www.nasaspaceflight.com/2022/04/dragon-freedom-docks-crew-4/),
[Crew‑2 returns to station (rendezvous) — NASASpaceFlight](https://www.nasaspaceflight.com/2021/04/endeavour-returns-station-crew-2/),
[Crew‑4 mission timeline — Spaceflight Now](https://spaceflightnow.com/2022/04/27/crew-4-mission-timeline/),
[Clohessy–Wiltshire equations — Wikipedia (equations reference)](https://en.wikipedia.org/wiki/Clohessy%E2%80%93Wiltshire_equations),
[Relative motion / CW framework — Sustainaverse](https://www.sustainaverseweb.com/post/relative-motion-in-orbit-spacecraft-rendezvous-clohessy-wiltshire-equations),
[The delicate dance of orbital rendezvous (Carroll) — arXiv](https://arxiv.org/pdf/1908.02592).
</content>
