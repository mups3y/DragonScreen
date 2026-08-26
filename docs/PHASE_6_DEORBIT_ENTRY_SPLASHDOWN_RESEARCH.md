# Mission Phase 6 — Return: Deorbit, Entry & Splashdown

Real-world facts only, trusted sources. Scope: from **trunk jettison / deorbit burn** through **entry, the
lifting bank-angle descent, parachutes and splashdown**. This is the final mission phase.

Tags: **[P]** cited/authoritative · **[E]** established · **[D]** technique/principle · **[~]** approximate.

---

## 1. Trunk jettison + deorbit burn
- **Trunk jettison happens shortly BEFORE the deorbit burn** on Crew Dragon (to shed mass / save
  propellant); the trunk has **no heat shield** and **burns up** on re-entry. (⚠ some cargo-Dragon
  descriptions place trunk sep after the burn; NASA Commercial Crew return material puts it *before* the
  deorbit burn for crew.) [P/E]
- **Deorbit burn on the Dracos, long and low-thrust: ~12–16.5 min** (Crew‑1 *Resilience*: **987 s ≈
  16.5 min**), targeting the entry corridor for the splashdown zone. [P]
- Dragon then orients **heat shield forward** for Entry Interface. [P]

---

## 2. Entry Interface (EI) & the heat shield
- **Entry Interface at ~120 km** — the first traces of atmosphere, at ~orbital velocity (~7.5–7.8 km/s). [P]
- **PICA‑X heat shield: 3.6 m across, the largest single-piece ablative heat shield ever flown** on an
  orbital spacecraft; protects against **>3,000 °F (surface ~1,850 °C at peak)**. [P]
- **Peak heating ~70–50 km:** a bow shock forms and **plasma envelops the capsule → communications
  blackout** (~6 min). [P]
- **Peak deceleration ~4–5 g** as the capsule slows from orbital to subsonic in minutes. [P]

---

## 3. The lifting entry (the steering technique)
Dragon is a **blunt lifting body**: an **offset (radially displaced) centre of mass** gives it a **natural
trim angle of attack (~12°) and lift-to-drag ratio L/D ≈ 0.18–0.27**. It steers by **bank-angle
modulation** — the Apollo/Orion/Shuttle method: [P/D]
- **Roll the lift vector about the velocity vector.** The **only control variable is the bank angle σ**;
  the vertical component of lift is **L·cos σ**, which controls descent rate / energy depletion and hence
  **downrange**. [P]
- **Crossrange is controlled by bank REVERSALS:** the guidance flips the sign of the bank angle whenever the
  predicted **crossrange error exceeds a velocity-dependent deadband**, producing a series of **S-turns**
  (as the Shuttle also did). [P]
- The vehicle **holds its trim AoA aerodynamically** throughout (no active AoA control needed); attitude
  control (RCS) points/rolls the lift vector. [D]

---

## 4. Parachutes & splashdown
- **2 drogue parachutes** deploy at **~18,000 ft (5,486 m)**, **~350 mph (~156 m/s)** — stabilise and slow
  the capsule through the transonic region. [P]
- **4 main parachutes** deploy at **~6,000 ft (1,830 m)**, **~119 mph (~53 m/s)**; each ~116 ft diameter;
  they slow Dragon to **~16–18 ft/s (~5–5.5 m/s)**. (Crew Dragon can land safely on 3 of 4 mains.) [P/E]
- **Splashdown at ~5–8 m/s** in the ocean (Atlantic off Florida, or the Gulf); Crew‑2 splashed off
  Pensacola at night. Recovery ship retrieves the capsule and crew. [P/E]

---

## 5. The math (entry guidance)
- **Apollo bank-angle entry guidance** (human-rated, flown every Apollo mission; MSL/Orion heritage): the
  bank-angle command is generated to **null the predicted downrange error**, with a **bank-reversal logic**
  on a velocity-dependent crossrange deadband. [P]
- **Predictor–corrector / reference-trajectory** entry guidance: predict where the current bank profile
  lands, adjust `cos σ` (lift up/down) to correct downrange, reverse the sign to correct crossrange. [P]
- **Vertical lift** = L·cos σ (range/energy); **horizontal lift** = L·sin σ (crossrange). The reversals
  keep crossrange inside the deadband while the magnitude flies the range. [P]
- Deorbit targeting: a retrograde Draco burn solved so the **ballistic + lifting trajectory** puts the
  entry footprint on the splashdown zone (accounting for the L/D range control available). [D]

---

## 5b. Guidance-law equations (build-level)

### Deorbit targeting (retrograde Δv to the entry corridor)
Lower periapsis from the circular orbit `r_c` to the entry-interface radius `r_p = R + h_EI` (h_EI ≈ 120 km);
transfer ellipse `a = (r_c + r_p)/2`: [E]
```
Δv_deorbit = √(μ/r_c) − √( 2μ·r_p / ( r_c·(r_c + r_p) ) )     (retrograde at r_c)
```
Solve the burn (magnitude + timing/direction on the Dracos) so the trajectory reaches EI at the target
**entry flight-path angle γ_EI** (the corridor: too shallow → skip out; too steep → over-g / over-heat) AND
so the footprint — after the lifting-entry range control below — falls on the splashdown zone. [D]

### Entry equations of motion (planar, per-unit-mass; bank angle σ is the only control)
`L = ½ρv²C_L A`, `D = ½ρv²C_D A`, `L/D = C_L/C_D ≈ 0.2`: [P]
```
ḣ = v·sin γ
ṡ = v·cos γ                                    (downrange)
v̇ = −D/m − g·sin γ
γ̇ = (1/v)·[ L·cos σ / m − g·cos γ + v²·cos γ/(R+h) ]
ψ̇ = (1/v)·[ L·sin σ / (m·cos γ) − (v/(R+h))·cos γ·tan(lat)·... ]   (heading → crossrange)
```
Vertical lift `L·cos σ` sets descent rate/energy → **downrange**; horizontal lift `L·sin σ` → **crossrange**.
[P]

### Bank-angle guidance law (Apollo/Orion, human-rated)
```
|σ_cmd| :  chosen (predictor–corrector or reference-drag tracking) to NULL the predicted downrange error
           to the target — larger |σ| tips lift sideways → shorter range; |σ|→0 → max lift → longer range.
sign(σ) :  reversed whenever  |crossrange error| > deadband(v)   (a velocity-dependent deadband)
           ⇒ a series of S-turns that keep crossrange bounded while |σ| flies the range.
```
The capsule holds its trim AoA **aerodynamically** (offset CoM); RCS only rolls/points the lift vector to
the commanded bank σ. [P]

### Parachute + touchdown triggers (state-based, not clock)
Drogues on descending through **~5.5 km** (Mach/`q` gate); mains on descending through **~1.8 km**; both
by measured altitude + descent rate, independent of any sequence (crew-safety backstop). Splashdown at
**~5–8 m/s** under 4 mains. [P]

## 6. Phase summary
**Trunk jettison** (burns up) → **~12–16 min Draco deorbit burn** to the entry corridor → orient **heat
shield forward** → **Entry Interface ~120 km**, **PICA‑X** shield to ~1,850 °C, **plasma blackout**, peak
**4–5 g** → **lifting bank-angle entry** (offset-CoM trim AoA, L/D ~0.2; roll the lift vector to fly
downrange, **bank-reversal S-turns** for crossrange — Apollo guidance) → **drogues ~5.5 km / 156 m/s** →
**4 mains ~1.8 km / 53 m/s** → **splashdown ~5–8 m/s** → recovery.

**Sources:** [Dragon Resilience return (deorbit 987 s, entry, chutes) — NASASpaceFlight](https://www.nasaspaceflight.com/2021/05/dragon-resilience-return-first-operational/),
[PICA‑X heat shield — Space Launches Live](https://spacelaunchlive.com/articles/pica-x/),
[Crew‑2 deorbit burn complete — NASA Commercial Crew](https://blogs.nasa.gov/commercialcrew/page/52),
[Deriving the MSL/Apollo entry guidance algorithm — Thomas Antony](https://www.thomasantony.com/posts/2021/msl-apollo-guidance/),
[Entry Guidance: A Unified Method — ResearchGate](https://www.researchgate.net/publication/262955727_Entry_Guidance_A_Unified_Method),
[Pterodactyl entry guidance/control — NASA NTRS](https://ntrs.nasa.gov/api/citations/20200000244/downloads/20200000244.pdf),
[Drogue/main altitudes & speeds, offset-CoM lifting entry — recorded in CREW2_REAL_MISSION_TECHNIQUES.md].
</content>
