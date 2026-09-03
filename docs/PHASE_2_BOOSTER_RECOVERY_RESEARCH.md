> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE — HIGH (§B16)**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-26; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.
> ⚠ **Named contradiction:** §B16 (owner, 2026-09-03) is the current form of booster recovery — a SEPARATE-VESSEL autopilot distinct from the conductor; the deleted `BoosterControl` implementation stays deleted.

# Mission Phase 2 — First-stage separation, descent & landing (booster recovery)

Real-world facts only, trusted sources. Scope: from **stage separation** to **booster touchdown on the
droneship**. This runs *concurrently* with the second-stage burn and Dragon insertion — both vehicles are
live at once. Crew Dragon boosters land **downrange on an Autonomous Spaceport Drone Ship (ASDS)** (Crew‑2:
*Of Course I Still Love You*), never RTLS, because a crew LEO launch sends the stage far downrange.

Tags: **[P]** cited primary/authoritative · **[E]** established · **[D]** design principle/technique ·
**[~]** approximate/varies.

Reference timing (Crew‑4, droneship): stage sep **T+2:39**, entry burn **~T+7:28**, landing burn
**~T+9:02**, landing **~T+9:30**. [P]

---

## 1. The four sub-phases

SpaceX's own breakdown of a booster recovery: **boostback burn → re-entry (entry burn) → landing burn →
touchdown.** For a **droneship** (downrange) mission there is **no boostback** — the stage is already going
where the droneship is; it only reorients, does the entry burn, glides, and lands. [P]

---

## 2. Stage separation

- **Pneumatic pusher separation** — after MECO the interstage releases and gas pushers shove the two stages
  apart (a low-energy push; the gap is opened mainly by the upper stage flying away). [E]
- The second stage ignites **~8 s after MECO**; the booster must be clear of the MVac plume. [P]

---

## 3. Reorientation (flip) — cold-gas thrusters

- The booster **flips to engines-first (retrograde)** for the entry burn. In near-vacuum the grid fins have
  no air to bite, so the turn is done with **cold-gas nitrogen (N₂) reaction-control thrusters** at the top
  of the stage. [P]
- (RTLS missions add a **boostback burn** here to reverse downrange velocity toward the launch site; a
  **droneship** crew mission omits it.) [E]

---

## 4. Entry burn — re-entry protection

- **Relights 3 of the 9 Merlins** to shed velocity before the stage hits the dense lower atmosphere,
  protecting it from **re-entry heating and aerodynamic loads**, and it also **aims the stage toward the
  landing target**. [P]
- Occurs high (~tens of km), around **T+7 min** on a crew droneship flight. [P]

---

## 5. Grid-fin aerodynamic descent — the steering phase

- **Four titanium grid fins**, hypersonic-capable, stowed on ascent, **deployed ~70 km** as the stage
  re-enters denser air and the flight computer has enough aerodynamic force to steer. They **constantly
  control the trajectory, directing the booster toward the ASDS.** [P]
- **X-wing arrangement; each fin rotates on one hinge.** Deflecting **"2-by-2 in the same direction gives
  pitch and yaw; all four together gives roll"** — full 3-axis aerodynamic control, up to **~20°** of
  vehicle reorientation. [P]
- **They steer by controlling the lift vector**, not a fixed pose: the stage flies at an **angle of attack**
  (body lift, magnitude → downrange) and **banks/rolls** the lift vector (direction → crossrange) — the
  same augmented-bank-angle-modulation family as a reentry vehicle. The fins deflect actively and
  continuously ("heavily moving"). [P/D]
- **Passive safety / offset targeting:** during descent the booster **initially aims to MISS the landing
  target**, and **only steers onto the target once all systems are confirmed nominal** — so a failure sends
  it into the water beside the droneship, not into it. [P]

---

## 6. Landing burn — the hoverslam

- Begins **~8 km altitude**; on most missions on the **single center engine** (some missions use 3 for the
  initial hard brake, then 1). [P]
- **It is a "hoverslam"/"suicide burn": the booster cannot hover.** One Merlin at its **minimum throttle
  (~40%)** still produces more thrust than the near-empty stage's weight, so it cannot hold altitude — it
  must **arrive at zero velocity at exactly zero altitude** in one continuous braking burn. [E]
- **Landing legs (4)** deploy in the **final seconds before touchdown**. [P]
- Touchdown is soft (~0–2 m/s) on the droneship's steel deck. [E]

---

## 7. The droneship (ASDS)

- An **Autonomous Spaceport Drone Ship** — a barge with **GPS, station-keeping thrusters, and a steel
  landing deck** — used when the trajectory carries the booster too far downrange for a land pad. Crew‑2's
  booster (B1061‑2) landed on **OCISLY** (*Of Course I Still Love You*), Atlantic, **~500 km downrange**. [P/E]

---

## 8. The math

### 8.1 The hoverslam (analytic braking curve)
- Cannot hover ⇒ a **single constant-max-thrust brake** timed to null velocity at the deck. With thrust
  acceleration `a` and gravity `g`, the fastest speed that can still be arrested from altitude `h` is the
  **braking curve** `v_max(h) = k·√(2·(a − g)·h)` (margin `k ≈ 0.9`), and the **ignition altitude** for
  current speed `v` is `h_ig = v² / (2·(a − g)·k²)`. Fire when the descent reaches `h_ig`; this hits
  `v = 0` at `h = 0`. [D — MechJeb hoverslam / standard]
- Engine mode is chosen **live** from the thrust needed (fewest engines that can still arrest from here),
  escalating 1 → 3 as required; the center engine gives the softest final touchdown. [D]

### 8.2 The real fuel-optimal method — convex optimization
- The landing guidance **continuously recomputes an optimal trajectory** to reach the deck within hardware
  limits; **convex optimization** guarantees the optimum is found fast enough to run onboard. [P]
- **Lossless convexification** (Açıkmeşe & Blackmore, *Automatica* 2011) reformulates the nonconvex
  fuel-optimal powered-descent problem — with **min/max thrust bounds and thrust-pointing constraints** —
  as a **Second-Order Cone Program (SOCP)** with a guaranteed global optimum. [P]
- **G-FOLD** (Guidance for Fuel-Optimal Large Diverts, Açıkmeşe/Casoliva/Carson/Blackmore) is the real-time
  implementation — **~6× the divert range** of the Curiosity landing software — and the same
  convexification techniques are applied to Falcon 9 (and later Starship). [P]
- The optimal thrust profile is **max-min-max ("bang-bang")**: brake hard, coast at min thrust, brake hard
  to touchdown — the fuel-optimal shape a suicide burn approximates. [D]

---

## 8b. Guidance-law equations (build-level)

### Grid-fin aerodynamic descent steering (impact-error → lift command)
The stage steers by pointing its **aerodynamic lift** toward the predicted-impact error (analogous to
entry bank-angle modulation). With `e_h = (predicted impact − target)` in the horizontal plane: [D]
```
predicted impact:  integrate the trajectory forward (RK4) under gravity + drag from the live state
lift command:      point the lift toward −e_h ;  |AoA| = clamp( k·|e_h| , AoA_max )   (AoA_max ≈ 15–20°)
                   AoA magnitude → lift magnitude → DOWNRANGE ;  bank/roll → lift direction → CROSSRANGE
grid fins:         actuate attitude to hold that AoA+bank (2-by-2 fins = pitch/yaw, all-4 = roll)
lead term:         steer on  e_h + τ·ė_h  to anticipate the aero lag and avoid overshoot
passive safety:    aim to MISS until all systems nominal, then converge e_h → 0
```

### Landing burn — analytic hoverslam (see §8.1) and the fuel-optimal convex problem
`v_max(h) = k·√(2(a−g)h)` (k≈0.9), ignite at `h_ig = v²/(2(a−g)k²)`; fewest engines that can still arrest. [D]

**Fuel-optimal powered descent as a convex program (G-FOLD / lossless convexification):** [P]
```
dynamics:   ṙ = v ,  v̇ = g + T/m ,  ṁ = −||T||/(Isp·g₀)
nonconvex:  0 < ρ₁ ≤ ||T|| ≤ ρ₂            (min-thrust lower bound is nonconvex),  thrust pointing
change of variables (Açıkmeşe–Blackmore):  u = T/m ,  σ = Γ/m ,  z = ln m
convex form: v̇ = g + u ,  ż = −α·σ  (α = 1/(Isp·g₀)) ,  ||u|| ≤ σ ,
             ρ₁·e^{−z} ≤ σ ≤ ρ₂·e^{−z}   (the exp bounds Taylor-linearised)
objective:   maximise final mass  ⇔  minimise ∫σ dt   → a SECOND-ORDER CONE PROGRAM (SOCP)
```
The relaxation is **lossless** (the optimum always has `||u|| = σ`), so the SOCP solves the true
fuel-optimal max-min-max ("bang-bang") thrust profile in real time onboard. [P]

## 9. Phase summary
Sep (pneumatic) → **flip retrograde on cold-gas N₂** → **entry burn (3 engines)** to shed velocity and
protect the stage → **deploy grid fins ~70 km**, steer the aerodynamic descent by **angle-of-attack + bank**
toward the droneship (**aim-to-miss until nominal**) → **landing burn ~8 km on the center engine
(hoverslam** — can't hover, null v at h=0, guided by **convex-optimization / G-FOLD**) → **legs deploy** →
soft touchdown on the ASDS ~500 km downrange, ~T+9:30.

**Sources:** [Up and down with a Falcon 9 booster — Spaceflight Now](https://spaceflightnow.com/2020/09/16/up-and-down-with-a-falcon-9-booster/),
[Grid Fins: how they steer a falling rocket — Space Launches Live](https://spacelaunchlive.com/articles/grid-fins/),
[Downrange Propulsive Landing infographic — ZLSA Design](https://zlsadesign.com/infographic/trajectory/spacex-falcon9-booster-dpl/),
[Crew‑4 timeline — Spaceflight Now](https://spaceflightnow.com/2022/04/27/crew-4-mission-timeline/),
[Lossless convexification of powered-descent guidance — Açıkmeşe & Blackmore](https://www.researchgate.net/publication/224253864_Lossless_convexification_of_Powered-Descent_Guidance_with_non-convex_thrust_bound_and_pointing_constraints),
[G-FOLD precision landing — NASA Armstrong](https://www.nasa.gov/centers-and-facilities/armstrong/mastens-xombie-tests-jpls-g-fold-precision-landing-software),
[Convex optimization for vehicle guidance — survey (arXiv)](https://arxiv.org/pdf/2311.05115).
</content>
