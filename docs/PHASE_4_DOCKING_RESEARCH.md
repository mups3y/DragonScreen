> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-26; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# Mission Phase 4 — Proximity operations & Docking

Real-world facts only, trusted sources. Scope: from **arrival at the approach corridor** (~2 km, after
Approach Initiation) through **the L-approach, waypoint holds, soft capture and hard capture** at the ISS
docking port. Crew Dragon is **fully autonomous** here from the start; the crew monitors and can take over.

Tags: **[P]** cited/authoritative · **[E]** established · **[D]** technique/principle · **[~]** approximate.

---

## 1. The approach zones (safety geometry)
- **Approach Ellipsoid (AE):** an egg-shaped zone around the ISS — **~2000 m** semi-axis along the
  orbital-velocity (V-bar) axis, **~1000 m** orthogonal (the press-kit "4 × 2 km"). Entered on initial
  approach. [P]
- **Keep-Out Sphere (KOS):** **200 m RADIUS** safety zone around the station (⚠ the technical sources say
  *radius*, not "200 m wide"). [P]
- **Any unplanned KOS penetration, or any departure from the approach corridor, commands an automatic
  abort.** [P]
- **Offset targeting is mandatory** — the approach is aimed so a *dispersed/failed* burn misses the KOS; a
  visiting vehicle never aims at the station. [P]

---

## 2. The L-approach (R-bar → V-bar) and the waypoints
The approach is an **"L", not a straight line**: come up the **R-bar** from below, turn onto the **V-bar**
in front, and run in along the docking axis. Each waypoint is a **station-keeping HOLD with a GO gate**
(ground/crew GO before proceeding) — holds are what make the approach abortable at every step and remove
the need to null a large lateral error while closing. [P/D]

| Waypoint | Position | Note |
|---|---|---|
| **AI arrival** | ~7.5 km behind & below | end of Phase 3 |
| **AE entry** | ~2 km behind | approach-ellipsoid entry; L-approach guidance takes over |
| **WP0** | **~400 m directly BELOW** the ISS (+R-bar) | hold, GO → enter the KOS |
| **WP1** | **~150–220 m in FRONT** on the docking axis (V-bar) | hold, GO; manual-takeover available |
| **WP2** | **~20 m** from the port | hold, GO for docking |
| *(contact)* | docking port | soft capture |

- **Demo‑1 also flew a retreat test** — approached to ~150 m, backed off to ~180 m to demonstrate departure,
  then closed again — proving the abort/retreat capability. [P]
- **Final approach speed at contact: ~8 cm/s** along the docking axis. [P]

---

## 3. Sensors & navigation (relative)
- Dragon's **hinged nose cap opens** to expose the docking ring and a suite of **LIDAR + cameras + thermal**
  sensors for fully automated approach. [P]
- **Relative navigation** provides full **6-DOF relative pose** (position + attitude) of Dragon vs the port
  during approach and docking. **Active LIDAR** does the primary range / range-rate work; **cameras** are
  secondary (and increasingly capable). [P]
- Crew Dragon was **designed for fully autonomous docking** from the outset; the crew monitors and can take
  manual control. [P]

---

## 4. Soft capture → hard capture (the mechanism)
Docking is two phases against the **International Docking Adapter (IDA)** on the ISS (the **NASA Docking
System / International Docking System Standard**, IDSS): [P]

1. **Soft capture:** electric actuators **extend the soft-capture ring** ahead of the main structure.
   **Three guide petals** on the ring mesh with the passive port's petals, **funnelling the rings into
   alignment and correcting small angle/offset errors** from navigation/control. When the rings touch,
   **latches engage = soft capture**, decelerating Dragon from the ~**8 cm/s** contact speed. The soft-
   capture system **absorbs the misalignments and residual motion**. [P]
2. **Hard capture:** the soft-capture ring **retracts**, pulling the vehicles together; then **12 hard-
   capture hooks** (NDS hook design) engage the corresponding passive hooks on the IDA to make the
   **structural, airtight seal** and connect **power + data**. [P]
- After hard capture: vestibule leak checks, then hatch open for crew transfer. [E]

---

## 5. The math
- **Terminal guidance = Clohessy–Wiltshire two-impulse transfers in the station's LVLH frame** between the
  waypoints (radial/along-track/cross-track), with **station-keeping holds** at WP0/WP1/WP2. [D]
- **6-DOF relative pose control** for the final approach and docking — the docking controller is a
  velocity/position servo on the LIDAR-measured relative state, holding the docking axis and closing at the
  commanded slow rate (cm/s), nulling lateral offset and attitude. [D]
- **Abort logic:** the corridor + KOS define hard constraints; a breach triggers the automatic abort/retreat
  (a departure burn back out of the corridor). [P]

---

## 5b. Guidance-law equations (build-level)

### Terminal transfer between waypoints — CW two-impulse (see Phase 3 for the full STM)
Each hop WP→WP is a CW two-impulse solve in the station LVLH frame with `n = √(μ/a³)`:
```
δv₀⁺ = Φrv(t_f)⁻¹ ( δr_wp − Φrr(t_f)·δr₀ )      (depart burn to the next waypoint)
Δv_arrive = −( Φvr(t_f)·δr₀ + Φvv(t_f)·δv₀⁺ )   (null relative velocity → station-keep hold)
```
aim WP positions as OFFSET points (R-bar/V-bar), so a missed burn drifts clear of the KOS. [D]

### Terminal 6-DOF relative control (close from WP2 to contact)
Two coupled loops on the LIDAR-measured relative state `(δr, δv)` and relative attitude: [D]
- **Translation — glideslope velocity servo.** Command a closing speed that tapers with range and caps at
  the corridor limit (~cm/s at contact):
  ```
  v_cmd = −min( k_r·|δr_axis| , v_max ) · û_axis   −   k_lat·δr_lateral
  a_cmd = k_v·( v_cmd − δv )                          → Draco RCS translation (±X/±Y/±Z body)
  ```
  `û_axis` = docking-axis unit; `δr_lateral` = off-axis offset (nulled proportionally); `v_max ≈ 0.08 m/s`
  at contact. Slow, monotone closure keeps the approach abortable.
- **Attitude — align the ports.** Command the vehicle attitude so the docking ring axis points at the
  target port and the roll clocks the ring; standard quaternion/vector PD (error → rate → torque → RCS),
  rate bounded by `ω_max = √(2·α·θ)` (`α` = RCS torque / inertia). [D]
- **Abort / keep-out:** the corridor and the **200 m KOS** are hard constraints; a predicted or actual
  breach triggers the retreat burn `Δv_retreat = −k·δr_axis` back out along the approach axis. [P]

### Soft-capture ring compliance (mechanical, not guidance)
Contact at ~8 cm/s; the soft-capture ring's actuators + 3 petals absorb residual lateral/angular
misalignment (navigation/control error) and correct it as the rings mate — the guidance only has to
deliver Dragon inside the ring's capture envelope, not to zero error. [P]

## 6. Phase summary
Arrive at the AE (~2 km) → **L-approach**: up the **R-bar** to **WP0 (~400 m below)** — hold, GO → into the
**KOS**, onto the **V-bar** to **WP1 (~150–220 m in front)** — hold, GO → **WP2 (~20 m)** — hold, GO for
docking → close at **~8 cm/s** on **LIDAR/camera** 6-DOF relative nav → **soft capture** (ring + 3 petals +
latches, absorbing misalignment) → **hard capture** (ring retract, **12 hooks** to the IDA) → leak checks,
hatch open. Fully autonomous, **offset-targeted**, **abort on any KOS breach**, crew monitors with
manual-takeover.

**Sources:** [Design, Development, Testing & Flight of the Crew Dragon Docking System — ESMATS (Matthews)](https://esmats.eu/amspapers/pastpapers/pdfs/2020/matthews.pdf),
[NASA Docking System (NDS) — Space Launches Live](https://spacelaunchlive.com/spacecraft-systems/nasa-docking-system-nds/),
[Crew Dragon docks with ISS — SpaceNews](https://spacenews.com/crew-dragon-docks-with-iss/),
[Crew Dragon successfully docks (Demo‑1, retreat test) — Forbes](https://www.forbes.com/sites/jonathanocallaghan/2019/03/03/spacexs-crew-dragon-spacecraft-just-docked-with-the-international-space-station/),
[Crew Dragon uses LIDAR — NextBigFuture](https://www.nextbigfuture.com/2020/11/spacex-dragon-uses-lidar.html),
[AR&D sensors — NASA SBIR](https://sbir.gsfc.nasa.gov/content/autonomous-rendezvous-and-docking-sensors),
[Approach ellipsoid / KOS / waypoints — Planetary Society & IRSIS (recorded in REAL_CREW_DRAGON_MISSION.md)].
</content>
