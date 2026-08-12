# The real Crew Dragon mission, end to end

Researched 2026-08-13 from primary and press-kit sources. **Every number here has a source.** Where
a figure is our own choice for Kerbin it is labelled `KERBIN CHOICE` and the reasoning is given.

This is the specification the flight software is to be built against. It supersedes the ad-hoc
profile we have been flying.

---

## 1. The reference flight: Crew-4, 27 April 2022

Times are T+ from liftoff, derived from the published wall-clock timeline (liftoff 03:52:55 EDT).

### Ascent

| T+ | event |
|---|---|
| 00:00 | Liftoff |
| 00:53 | Stage 1 throttle bucket |
| 01:02 | Max-Q |
| 01:09 | Mach 1 |
| **02:36** | **MECO** |
| 02:39 | Stage separation |
| 02:47 | Second stage MVac ignition |
| 07:28 | Booster entry burn |
| **08:48** | **SECO-1 — orbit insertion** |
| 09:02 | Booster landing burn |
| 09:30 | Booster landing |
| **11:59** | **Dragon separates from the second stage** |

Two things this settles that we had wrong:

- **The second stage performs the orbital insertion and Dragon separates AFTER it, at T+12 min.**
  We only corrected this on 2026-08-12; it is confirmed here as the real profile, not a convenience.
- **MECO is at T+2:36 and the booster lands at T+9:30** — the whole recovery happens *during* the
  second stage burn and the coast. Both vehicles are live at once, which is what `FlightDriver`
  exists to support.

### Rendezvous — seven burns over ~16.4 hours

| T+ | burn |
|---|---|
| 00:48 | **Phase** |
| 09:42 | **Boost** |
| 10:27 | **Close** (co-elliptic) |
| 12:03 | **Transfer** |
| 12:50 | **Co-elliptic** |
| **14:46** | **Approach Initiation (AI)** |
| 15:11 | **Approach Midcourse** |
| **16:22** | **Contact and capture — IDA-3** |
| 16:35 | Docking sequence complete |

**Approach Initiation happens 7.5 km behind and below the station, 96 minutes before docking.**
16:22 − 14:46 = 96 min exactly, which corroborates the two independent sources.

⛔ **This is not our ladder.** We classify by range and fly phasing laps. The real profile is a
**named burn sequence** in which each burn has a job: Phase sets the catch-up rate, Boost raises the
orbit, Close/Co-elliptic establish a co-elliptic orbit a fixed height below the station, Transfer
starts the intercept, and AI begins proximity operations. It is a Hohmann-family solution with hold
points, not a proportional chase.

### Proximity operations — the zones

| zone | figure |
|---|---|
| **Approach Ellipsoid (AE)** | **2000 m** semi-axis along the orbital-velocity axis, **1000 m** orthogonal — the "4 x 2 km" of the press kit |
| **Keep Out Sphere (KOS)** | **200 m RADIUS** |

⚠ The popular press says the KOS is "200 metres wide". The technical sources say **200 m radius**.
Use the radius.

**Offset targeting is mandatory.** From outside the AE the approach must be aimed so that a
*dispersed* trajectory — one where the burn fails or misfires — still misses the KOS. A vehicle
never aims at the station. This is the formal version of `falcon-rendezvous-approach-law`'s "never
chase a co-orbital target", and it is a requirement, not a style.

**Any departure from the corridor, or any unplanned KOS penetration, commands an automatic abort.**

### Proximity operations — the waypoints

| waypoint | position |
|---|---|
| **WP0** | **400 m directly BELOW the ISS** — on the +R-bar |
| **WP1** | **220 m in FRONT of the ISS**, on the docking axis |
| **WP2** | **20 m from the docking port** |

The path: arrive 7.5 km behind and below → AI burn → into the AE → **WP0, 400 m below** → hold, GO
from the ground → **swing up and forward, entering the KOS** → **WP1 at 220 m on the axis** → hold,
GO → **WP2 at 20 m** → hold, GO → contact and capture.

⛔ **THE APPROACH IS AN L, NOT A LINE.** Dragon comes up the R-bar from below, turns onto the V-bar
in front, and runs in along the docking axis. Our software flies a straight line from wherever it is
to a gate on the port axis. That is the single largest divergence from the real vehicle, and it is
also why our capsule kept arriving off-axis.

⛔ **EVERY WAYPOINT IS A HOLD.** Dragon stops and station-keeps at WP0, WP1 and WP2 awaiting a GO.
Our approach never stops. Holds are what make an approach abortable at every stage, and they are the
reason the real vehicle does not need to null a large lateral error while closing.

### Return

| step | detail |
|---|---|
| Undock | two very small burns as the hooks retract |
| **4 departure burns** | move clear of the station |
| **1 departure phasing burn, ~6 minutes** | lines the orbit up with the splashdown zone |
| **Trunk jettison** | **BEFORE the deorbit burn**, to save propellant |
| **Deorbit burn, ~12 minutes** | |
| Entry | ~6 minutes of comms blackout |
| **Drogues** | **~18 000 ft (5 486 m)** at **~350 mph (156 m/s)** |
| **Mains** | **~6 000 ft (1 830 m)** at **~119 mph (53 m/s)** |
| Splashdown | |

Our drogue and main altitudes already match. The **12-minute deorbit burn** and the **four departure
burns plus a phasing burn** do not — we fire one de-orbit burn off a solved node.

---

## 2. Mapping to Kerbin — the decisions, with reasons

### Inclination: **51.6 degrees, exactly.** `PRIMARY CHANGE`

The ISS inclination is 51.6 deg and it transfers directly — it is an angle, not a distance. This is
the change that makes the mission real, and it has consequences we must accept deliberately:

⛔ **THE LAUNCH WINDOW BECOMES A PLANE PROBLEM AGAIN.** `falcon-station-ferry` records that we
launch on PHASE, not plane, "because the plane window is degenerate at the equator". At 51.6 deg it
is no longer degenerate: there are two windows per day and the launch azimuth is set by the target
plane. The plane-based window code exists in F9I history (`COMMON/TIME.ks`, deleted 2026-08-04
because it was superseded). **We need it back, in C#.**

⛔ **AND THE ASCENT AZIMUTH IS NO LONGER 90 DEG.** Our autopilot engages with "heading 90" hard-coded.
Launching from the Falcon 9 site into a 51.6 deg plane needs a real azimuth solve.

### Altitude: **120 km.** `KERBIN CHOICE`

A literal scale does not work. The ISS sits at ~420 km over a 6371 km Earth — 6.6% of a radius, and
about 4x the height of the atmosphere. On Kerbin, 6.6% of 600 km is 40 km, which is *inside* the
70 km atmosphere. So the ratio cannot be preserved; something has to be chosen.

**120 km is chosen for a measured reason.** The station currently sits at 86.8 x 85.8 km, which
leaves only **~16 km between the station and the atmosphere**. That is precisely what produced the
2026-08-13 deadlock: a one-lap phasing orbit needed a periapsis of 69 km and the floor is 75 km.
There was not enough room beneath the station to phase in. At 120 km there is **50 km** of room, the
phasing solutions are comfortable, and the co-elliptic legs of the real profile become expressible.

It also keeps flight time sane: period ~34 min against 31.7 min at 86 km.

### What stays as it is

- **Drogue 5 486 m / main 1 830 m** — already the real figures.
- **The keep-out sphere and approach corridor** as concepts — we have them; the geometry changes.
- **The 200 m KOS radius** replaces our measured-from-bounding-box keep-out for the station.

---

## 3. What we already have, and what is missing

| real element | our state |
|---|---|
| S2 insertion, Dragon sep at T+12 | **have**, fixed 2026-08-12 |
| Booster RTLS during the S2 burn | **have**, lands 0.0 km |
| Drogue/main altitudes | **have** |
| Keep-out sphere, corridor, gate | **have**, but the geometry is ours not theirs |
| Monotone stage machine, 1 m corridor commit | **have**, unflown |
| **51.6 deg inclination** | **MISSING** — station is at 0.133 deg |
| **Plane-based launch window + azimuth solve** | **MISSING** — we launch on phase, heading 90 |
| **Named burn sequence** (Phase/Boost/Close/Transfer/Co-elliptic/AI/Midcourse) | **MISSING** — we fly a range-classified ladder |
| **Co-elliptic orbit** a fixed height below the station | **MISSING** |
| **R-bar approach to WP0 at 400 m below** | **MISSING** — we go straight to the port axis |
| **The L-shaped transition R-bar to V-bar** | **MISSING** |
| **WP1 at 220 m, WP2 at 20 m** | **MISSING** — we have one standoff at 25 m |
| **Station-keeping holds with GO gates** | **MISSING** — we never stop |
| **Offset targeting / passive abort safety** | **MISSING** |
| **4 departure burns + 6 min phasing burn** | **MISSING** — one node burn |
| **Trunk jettison before deorbit** | **have** |
| **12 min deorbit burn** | different — ours is a solved node |

---

## Sources

- [Crew-4 mission timeline — Spaceflight Now](https://spaceflightnow.com/2022/04/27/crew-4-mission-timeline/)
- [Crew Dragon Successfully Docks to ISS — The Planetary Society](https://www.planetary.org/articles/crew-dragon-docks)
- [International Space Station approach zones — The Planetary Society](https://www.planetary.org/space-images/international-space-station)
- [International Rendezvous System Interoperability Standards (IRSIS), 2019](https://internationaldeepspacestandards.com/wp-content/uploads/2024/02/rendezvous_baseline_final_3-2019.pdf)
- [SSP 50235 ISS Vehicle Interface Definition Document](https://spacecraft.ssl.umd.edu/design_lib/SSP50235.ISSvehicleIDD.pdf)
- [How Do Spacecraft Dock With the ISS — New Space Economy](https://newspaceeconomy.ca/2024/09/05/how-do-spacecraft-dock-with-the-international-space-station/)
- [Top 10 Things to Know for NASA's SpaceX Demo-2 Return — NASA](https://www.nasa.gov/humans-in-space/top-10-things-to-know-for-nasas-spacex-demo-2-return/)
- [Crew Dragon return sequence infographic — NASA](https://www.nasa.gov/wp-content/uploads/2020/08/earth_phasing.pdf)
