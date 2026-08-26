# Mission Phase 5 — Undocking & Departure

Real-world facts only, trusted sources. Scope: from **hatch close and undock** through the **departure
burns and departure phasing burn** that put Crew Dragon on the orbital path for the return. Deorbit, entry
and splashdown are Phase 6.

Tags: **[P]** cited/authoritative · **[E]** established · **[D]** technique/principle · **[~]** approximate.

---

## 1. Prep to undock (reverse of the arrival)
- Crew **suit up, ingress, close & lock the hatch, run the vestibule/cabin leak check**, and get the
  **GO for undock** — the reverse of the docking-day ingress. [E]
- Everything is on the **Dracos** (autonomous), crew monitoring with manual-takeover authority. [E]

---

## 2. Undocking (release)
- On **GO for undock**: **umbilicals retract, the hard-capture hooks open (retract)**, freeing Dragon from
  the IDA. [P]
- **Two very small separation burns** fire immediately after the hooks retract to push Dragon off the port
  (a gentle spring-plus-Draco push). [P]

---

## 3. Departure burns (out of the corridor)
- Once free, Dragon **autonomously executes four departure burns** to move clear of the station and out of
  the **approach ellipsoid** (the 4 × 2 km zone that governs all vehicles arriving/departing the ISS). [P]
- The named/numbered sequence (Demo‑2 / NASA return figures): [P/E]
  - **Burn 0** (~16 s, just after undock) — up and around the station.
  - **Burn 1** (~20 s, a few minutes later) — to in-front-of and below.
  - **Burn 2** (~44 s, ~50 min after Burn 1).
  - **Burn 3** (~1 min) — leaving Dragon in a stable orbit **~10 km below the ISS**.
- The departure is the **reverse of the approach**: leave the KOS/corridor along a safe path, then move to a
  stable orbit below and behind. [D]

---

## 4. Departure phasing burn
- After the four departure burns, Dragon fires a **departure phasing burn (~6–9 min; ~9 min 18 s on
  reference profiles)** that lowers/lines up the orbit to **sync with the target splashdown zone** for the
  chosen deorbit opportunity. [P]
- (Undock-to-splashdown is typically several hours to allow the orbit to phase to the recovery zone.) [E]

---

## 5. The math
- **Departure = CW two-impulse maneuvers in the station's LVLH frame** (the mirror of the approach) to walk
  Dragon safely out of the corridor to a co-orbital point below/behind the ISS. [D]
- **The departure phasing burn is a Hohmann-class orbit adjustment** that sets the ground track / orbital
  timing so the subsequent **deorbit burn** puts the entry footprint on the splashdown zone. [D]
- **Offset/safe-trajectory targeting still applies** while inside the corridor — the departure path is
  chosen so a missed burn does not re-enter the KOS. [D]

---

## 5b. Guidance-law equations (build-level)

### Departure burns — CW two-impulse, mirror of the approach (full STM in Phase 3)
Each departure hop is a CW solve in the station LVLH frame (`n = √(μ/a³)`), aiming an offset point that
walks Dragon safely OUT of the corridor and down to a co-orbital point below/behind: [D]
```
δv₀⁺ = Φrv(t_f)⁻¹ ( δr_target − Φrr(t_f)·δr₀ )        (each of Burn 0..3)
```
Burn 0 lifts up-and-over; Burns 1–3 walk to a stable point **~10 km below** the ISS; the whole path is
chosen so a missed burn does not re-enter the 200 m KOS. No arrival-null needed until the final co-orbital
hold. [D]

### Departure phasing burn — set the ground track for splashdown
A Hohmann-class in-plane change that lowers/tunes the orbit so the ground track lines up with the
splashdown zone at the chosen deorbit opportunity. Lower from `r₁` to `r₂`:
```
Δv = √(μ/r₁)·( 1 − √(2r₂/(r₁+r₂)) )      (retrograde at r₁; ~6–9 min low-thrust Draco)
```
The target is chosen so that, after the subsequent deorbit burn (Phase 6) and the lifting-entry range, the
footprint lands in the recovery zone — i.e. it phases the orbit to the right longitude/time for de-orbit.
[D]

## 6. Phase summary
Suit up, hatch close, leak check, **GO for undock** → **hooks open, umbilicals retract, 2 tiny separation
burns** → **4 autonomous departure burns** (Burn 0–3) out of the approach ellipsoid to a stable orbit
**~10 km below** the ISS → a **~6–9 min departure phasing burn** to line the orbit up with the splashdown
zone → coast to the deorbit opportunity (Phase 6). All on the Dracos, reverse-of-approach, corridor-safe.

**Sources:** [Crew‑11 undocking — umbilicals retract, hooks open, Dragon backs away — KHOU/NASA](https://www.khou.com/video/news/local/nasa-breaks-down-crew-11-undocking-umbilicals-retract-hooks-open-dragon-backs-away/285-c94b96e3-46e4-4495-a6d8-a8d94aaf81ec),
[Crew Dragon return sequence infographic (Departure / Phasing / Deorbit) — NASA](https://www.nasa.gov/wp-content/uploads/2020/08/earth_phasing.pdf),
[Top 10 Things to Know for the Return (undock + departure burns) — NASA](https://nasa.tumblr.com/post/625191676032516096/top-10-things-to-know-for-the-return-of-our-launch),
[How Crew Dragon returns home — Everyday Astronaut](https://everydayastronaut.com/spacexs-crew-dragon-how-to-get-back-from-earth-orbit/),
[Crew‑2 return techniques (departure burns 0–3) — recorded in CREW2_REAL_MISSION_TECHNIQUES.md].
</content>
