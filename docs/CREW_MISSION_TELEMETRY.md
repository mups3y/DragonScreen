# Crew Dragon mission telemetry — research & reconstruction database

**Goal (user 2026-08-26):** get telemetry from *every* Crew Dragon crewed mission, and where raw
telemetry is missing, **build a database that reconstructs the missing ascent facts** (pitch, throttle/g,
event times) from what we *can* source. This doc is the human-readable recording; the machine-readable
database is `data/crew_missions.json` (+ `data/dm1_ascent_template.json`, the reconstruction basis).

Tags: **[P]** cited primary/authoritative · **[E]** established · **[D]** derived/reconstructed · **[~]** approximate.

---

## 0. The hard truth about availability (fact-checked 2026-08-26)

**Only ONE Crew Dragon mission has cleanly-extracted raw time-series telemetry: Demo-1 (DM-1).** [P]

- `shahar603/Telemetry-Data` (the canonical extracted-webcast dataset) was **directory-listed in full**:
  its newest SpaceX entries are SAOCOM-1A / DM-1 (late 2018 – early 2019). It contains **DM-1** and **no**
  DM-2, Crew-1…Crew-11, Ax-x, Inspiration4, Polaris Dawn or Fram2. [P — GitHub API contents listing]
- `shahar603/Launch-Dashboard-API` (the REST successor that *would* hold newer launches) — the host
  `api.launchdashboard.space` (157.245.84.42) **resolves but the server does not respond** (connect
  timeout / HTTP 000). The API is effectively dead. [P — direct probe 2026-08-26]
- Every crewed mission's webcast *shows* on-screen velocity/altitude/time, but extracting it needs OCR on
  the video (`shahar603/SpaceXtract`) — not something we can run here on 20+ hours of stream.

**Therefore the plan is reconstruction, not download:** DM-1 is the same Falcon 9 Block 5 + Crew Dragon
flying the same NASA-imposed crew ascent (same g-limits, same trajectory class). Its *derived* pitch and
throttle/g profile is the **canonical template**; each other mission's **published event timeline** (MECO,
sep, SES-1, SECO-1, Dragon sep) and **target orbit** key that template to the specific flight. That is the
database.

---

## 1. The one real dataset — DM-1 canonical ascent template  [P → D]

Source: `shahar603/Telemetry-Data/DM-1` (Crew Dragon Demo-1, **2019-03-02**), webcast OCR. Downloaded and
re-derived 2026-08-26. Raw channels: time, velocity (m/s, ground-relative), altitude (km), and derived
velocity_x/y, acceleration, flight-path angle, downrange, q. Saved as `data/dm1_ascent_template.json`.

**Events (s):** throttle-down 44 → max-Q **59** → throttle-up 71 → **MECO 158** → SES-1 **169** →
SECO-1 ~**527** (data ends 538). [P]

**S1 pitch (flight-path angle) + acceleration vs fraction of MECO (158 s):** [D from P]

| frac | t (s) | alt (km) | v (m/s) | pitch° | accel (g) |
|---|---|---|---|---|---|
| 0.1 | 16 | 0.0 | 61 | 76.2 | 1.46 |
| 0.2 | 32 | 0.2 | 149 | 78.8 | 1.61 |
| 0.3 | 47 | 1.6 | 235 | 79.2 | **1.35** ← throttle bucket |
| 0.4 | 63 | 8.6 | 301 | 74.9 | 1.45 |
| 0.5 | 79 | 16 | 429 | 72.6 | 2.09 |
| 0.6 | 95 | — | 628 | 68.4 | 2.35 |
| 0.7 | 111 | — | 880 | 62.8 | 2.66 |
| 0.8 | 126 | — | 1178 | 57.3 | 3.07 |
| 0.9 | 142 | — | 1531 | 50.9 | 2.95 |
| 1.0 | 158 | — | 1881 | **46.6** | 1.10 (MECO throttle-down) |

- **max-Q (T+59 s):** alt 7.6 km, v 282 m/s, pitch **74.1°**. [D]
- **Throttle bucket:** acceleration dips to **1.35 g** through max-Q (44–71 s). [D]
- **S1 g-limit:** pre-MECO peak **3.26 g** at T+154 s. [D]

**S2 acceleration vs fraction of SES-1→SECO-1 (169→527 s):** [D from P]

| frac | t (s) | v (m/s) | pitch° | accel (g) |
|---|---|---|---|---|
| 0.00 | 169 | 1821 | 45.0 | 0.61 |
| 0.30 | 276 | 2267 | 13.2 | 1.08 |
| 0.60 | 384 | 3589 | -1.1 | 1.60 |
| 0.90 | 491 | 5854 | -1.6 | 2.85 |
| 1.00 | 527 | 6991 | -0.5 | 3.05 |

- **S2 g-limit:** pre-SECO peak **3.57 g** at T+518 s. [D]
- **Insertion (data end ~SECO-1):** alt ~198 km, v ~6991 m/s ground-relative (≈7.5 km/s inertial). [D]

**Autopilot takeaways (the pitch + throttle law the vehicle should fly):**
1. Pitch program: near-vertical clear of tower → hold ~**79°** to ~T+45 s → gravity-turn pitch-over to
   ~**47°** at MECO → S2 continues the pitch-down through **0°** (horizontal) to orbit.
2. Throttle law: full → **bucket to ~1.35 g** across max-Q (T+44–71 s) → full → **cap S1 at ~3.3 g**
   toward MECO → S2 ramps to a **~3.5–3.6 g** cap near SECO (NASA crew g-limit, well under cargo's).

---

## 2. Standard ISS-crew ascent timeline (DM-2, Crew-1…Crew-11, Ax-1…Ax-4)  [P]

Every ISS crew-rotation & Axiom flight is the **same trajectory** — Falcon 9 Block 5 to a **~51.6°**,
~200 km insertion, droneship recovery (ASOG/JRTI/OCISLY downrange in the Atlantic), Dragon phasing to the
ISS. The published event times are near-identical mission to mission (spread of only a few seconds):

| Event | MET (T+) | Notes |
|---|---|---|
| Max-Q | ~0:58–1:00 | max aerodynamic pressure; throttle bucket |
| **MECO** | **~2:34–2:36** | first-stage cutoff |
| Stage sep | ~2:38–2:39 | pneumatic pushers, ~3 s after MECO |
| **SES-1** | ~2:44–2:47 | MVac ignition, ~8 s after sep |
| Entry burn (booster) | ~6:30–7:30 | concurrent, droneship |
| **SECO-1** | **~8:47** | orbit insertion (single S2 burn for ISS) |
| Booster landing | ~8:30–9:30 | droneship downrange |
| **Dragon sep** | **~11:57–12:00** | from S2; nosecone already open; Draco checkout |

**Anchor missions (verified wall-clock → MET):** [P]
- **Crew-2** (2021-04-23, liftoff 05:49:02 EDT): MECO 05:51:38 (**T+2:36**), sep 05:51:41 (**T+2:39**),
  SECO-1 05:57:49 (**T+8:47**), Dragon sep 06:01:00 (**T+11:58**). [P — Spaceflight Now / NASA timeline]
- **Crew-6** (2023-03-02, liftoff 00:34:14 EST): MECO 00:36:48 (**T+2:34**), sep 00:36:52 (**T+2:38**),
  SECO-1 00:43:01 (**T+8:47**), Dragon sep 00:46:11 (**T+11:57**). [P — Spaceflight Now]

→ The ISS-crew reconstruction uses the DM-1 template scaled so **MECO=155 s** (mean ~2:35) and
**SECO-1=527 s**; per-mission overrides applied where an exact timeline is published.

---

## 3. The three free-flyers — different trajectories (record separately)  [P]

These are NOT ISS missions; their ascent/insertion differs and the reconstruction must NOT reuse the ISS
timeline blindly:

- **Inspiration4** (2021-09-16, *Resilience*): **~575×585 km circular, 51.6°** — the highest *circular*
  human orbit since Hubble servicing. Higher-energy insertion than ISS → **S2 does a longer / two-burn
  raise** (coast + circularization) vs the ISS single burn. [P]
- **Polaris Dawn** (2024-09-10, *Resilience*): **~190 km × up to 1,400 km, 51.7°** — highly **elliptical**;
  reached the **highest Earth apogee of any crewed flight (1,408 km)**. S2/Dragon raise apogee enormously;
  perigee later raised for the EVA phase. [P]
- **Fram2** (2025-03-31/04-01, *Resilience*): **~202×413 km, 90.01° (slightly retrograde) TRUE POLAR** —
  first crewed polar orbit. Launched **due south** from KSC down a coastal polar corridor → **launch
  azimuth ≈ 180–190°**, not the ~44° ISS azimuth; different plane-window logic entirely. [P]

Insertion geometry differs, but the **S1 pitch + throttle-bucket + g-limit template from DM-1 still
applies** (same vehicle, same aero, same crew g-limit); what changes is the **S2 burn plan** (target
apogee/perigee/plane) and, for Fram2, the **launch azimuth**.

---

## 4. Telemetry-availability matrix (fact-checked)

| Mission | Date | Capsule | Orbit (km / °) | Raw telemetry? | Timeline? | Reconstruction |
|---|---|---|---|---|---|---|
| **DM-1** | 2019-03-02 | C204 | ISS 51.6 | **YES** (shahar603) | yes | *is the template* |
| DM-2 | 2020-05-30 | Endeavour | ISS 51.6 | no | [P] | DM-1 tpl @ ISS timeline |
| Crew-1 | 2020-11-16 | Resilience | ISS 51.6 | no | [P] NASA PDF | ” |
| **Crew-2** | 2021-04-23 | Endeavour | ISS 51.6 | no | **[P] exact** | ” (anchor) |
| Inspiration4 | 2021-09-16 | Resilience | 575×585 / 51.6 | no | [P] | tpl + high-circular S2 |
| Crew-3 | 2021-11-11 | Endurance | ISS 51.6 | no | [P] | DM-1 tpl @ ISS timeline |
| Ax-1 | 2022-04-08 | Endeavour | ISS 51.6 | no | [P] | ” |
| Crew-4 | 2022-04-27 | Freedom | ISS 51.6 | no | [P] | ” |
| Crew-5 | 2022-10-05 | Endurance | ISS 51.6 | no | [P] | ” |
| **Crew-6** | 2023-03-02 | Endeavour | ISS 51.6 | no | **[P] exact** | ” (anchor) |
| Ax-2 | 2023-05-21 | Freedom | ISS 51.6 | no | [P] | ” |
| Crew-7 | 2023-08-26 | Endurance | ISS 51.6 | no | [P] | ” |
| Ax-3 | 2024-01-18 | Freedom | ISS 51.6 | no | [P] | ” |
| Crew-8 | 2024-03-04 | Endeavour | ISS 51.6 | no | [P] | ” |
| Polaris Dawn | 2024-09-10 | Resilience | 190×1400 / 51.7 | no | [P] | tpl + elliptical S2 |
| Crew-9 | 2024-09-28 | Freedom | ISS 51.6 | no | [P] | DM-1 tpl @ ISS timeline |
| Fram2 | 2025-03-31 | Resilience | 202×413 / 90.0 | no | [P] | tpl + polar azimuth |
| Ax-4 | 2025-06 | *new (Grace)* | ISS 51.6 | no | [P] | DM-1 tpl @ ISS timeline |
| Crew-10 | 2025-03-14 | Endurance | ISS 51.6 | no | [P] | ” |
| Crew-11 | 2025-08-01 | Endeavour | ISS 51.6 | no | [P] | ” |

*(dates/capsules being fact-checked mission-by-mission below; matrix updated as verified.)*

---

## 5. The callout / mission-control announcement stream — a per-mission source  [P]

The user's insight (2026-08-26): the webcasts' **live callouts** ("vehicle is supersonic … max-Q …
MECO … stage sep … SECO … Dragon sep") and **commentary** ("Dragon is pitching over now") are matched to
the on-screen MET clock, and can be mined per mission. Two practical, fetchable forms of that stream:

- **NASA mission-timeline PDFs** — the authoritative *transcription* of the countdown + ascent callouts to
  exact MET. **Readable here** via the Read tool (WebFetch can't decode the PDF, but Read does). This is
  the highest-fidelity callout source and exists for most NASA crew flights.
- **NASA Commercial Crew live blog** — live-posts each callout as it's spoken ("Crew-N: Max Q, Main Engine
  Cutoff, Stage Separation"), in prose (event order, coarse timing). Exists for **every** crew launch,
  including the newest.

**Radio/mission-control loops:** SpaceX does not publish the raw CapCom↔Dragon loop as a file; the crew
comm is audible on the webcast and NASA has released flight-director loop audio for a few flights. We
can't transcribe audio directly, but the *events those loops call* are captured by the two text sources
above — so the callout information is recoverable without the audio.

### Crew-1 full callout timeline (NASA PDF, 2020-11-15 launch) — the ISS-crew reference  [P]

**Countdown (crew procedure → maps to autopilot gates G1–G7):**

| MET | Event | Gate |
|---|---|---|
| −4:59:59 | Dragon IMU align & configure for launch | — |
| −4:30:00 | Dragon prop pressurization | — |
| −4:00:00 | Suit donning & checkouts | — |
| −2:35:00 | Crew ingress | G1 |
| −2:20:00 | Communication check | G1 |
| −2:14:00 | Suit leak checks | G2 |
| −1:55:00 | Hatch close | G3 |
| −1:10:00 | ISS state upload to Dragon | — |
| **−0:45:00** | **SpaceX Launch Director verifies GO for propellant load** | **G4** |
| −0:42:00 | Crew access arm retracts | — |
| **−0:37:00** | **Dragon launch escape system is ARMED** | **G5** |
| −0:35:00 | RP-1 + 1st-stage LOX load begins | — |
| −0:16:00 | 2nd-stage LOX load begins | — |
| −0:07:00 | Falcon 9 engine chill | — |
| **−0:05:00** | **Dragon transitions to internal power** | **G6** |
| −0:01:00 | Final prelaunch checks; tanks to flight pressure | — |
| **−0:00:45** | **SpaceX Launch Director verifies GO for launch** | **G7** |
| −0:00:03 | Engine controller commands ignition sequence start | — |

**Ascent (callout MET):**

| MET | Event |
|---|---|
| 0:00:00 | Liftoff |
| **0:00:58** | Max-Q |
| **0:02:37** | MECO |
| 0:02:40 | Stage separation (MECO + 3 s) |
| **0:02:48** | 2nd-stage engine start SES-1 (sep + 8 s) |
| 0:07:29 | 1st-stage entry burn |
| **0:08:50** | SECO-1 (orbit insertion) |
| 0:08:59 | 1st-stage landing burn |
| 0:09:29 | 1st-stage landing |
| **0:12:03** | Crew Dragon separates from 2nd stage |
| 0:12:48 | Dragon nosecone open sequence begins |

These countdown times are the **real values for the autopilot's countdown gates** (matches the plan's
G1–G7). Cross-check vs Crew-2 (MECO 2:36, SECO 8:47, sep 11:58) and Crew-6 (MECO 2:34, SECO 8:47, sep
11:57): the ISS-crew ascent is stable to a few seconds. → database `standard_iss_crew` block.

## 6. FULL-MISSION callout timeline (user 2026-08-26: "callout + telemetry for entire mission")  [P]

The callout stream for the *whole* mission is spread across NASA's per-phase timeline documents (launch
day / rendezvous day / return day), all readable via the Read tool. Assembled below from the **Crew-1**
timelines (the reference), with return timing cross-checked vs DM-2 & Crew-2 blogs.

### 6a. Rendezvous & docking — the real named-burn schedule (Crew-1 rendezvous timeline PDF)  [P]

**This is exactly the sequence the rendezvous rebuild targets** — the real named burns, in order, with MET
and the two crew GO/NO-GO gates:

| MET (d/hh:mm:ss) | Event | Autopilot |
|---|---|---|
| +0/00:45:42 | **Phase burn** | rendezvous start |
| +0/15:53:39 | **Boost burn** (next day) | |
| +0/16:35:25 | **Close burn** | |
| +0/22:18:05 | **Transfer burn** | |
| +0/23:04:33 | **Coelliptic burn** | co-elliptic hold |
| +1/00:32:42 | **30 km — rendezvous complete** | end far-field |
| +1/01:02:52 | Approach OOP burn (if needed) | out-of-plane trim |
| +1/01:37:42 | **GO/NO-GO for AI burn** | **G9** |
| +1/02:02:42 | **AI-burn (7.5 km) — 90 s, 0.72 m/s** | Approach Initiation |
| +1/02:27:42 | **AI-Midcourse burn** | midcourse |
| +1/02:47:42 | **Waypoint Zero (400 m below)** | **G10** hold |
| +1/03:11:42 | **Docking-axis arrival — WP1 (~220 m)** | **G11** hold |
| +1/03:22:42 | **Waypoint 2 arrive (20 m)** | **G12** hold |
| +1/03:23:42 | **GO/NO-GO for Docking** | **G12** gate |
| +1/03:27:42 | WP2 depart (20 m) | begin final |
| +1/03:32:42 | **Contact / Capture** | soft capture |
| +1/03:45:42 | **Docking complete** | hard capture / hooks |

Extracted facts the autopilot can use directly: the **AI burn is 90 s / 0.72 m/s at 7.5 km**; WP0=400 m
below, WP1≈220 m on axis, WP2=20 m; rendezvous is a **~28-hour** profile (Crew-1); the named-burn ORDER
(Phase→Boost→Close→Transfer→Coelliptic→AI→Midcourse) is confirmed real, matching the rebuild plan. [P]

### 6b. Return — undock → splashdown (NASA blogs, DM-2 / Crew-1 / Crew-2)  [P]

The return callouts (times are relative; the *sequence + spacing* is what the autopilot needs):

| Phase | Event | Timing |
|---|---|---|
| Depart | Fly-around burns ×4 (zenith→aft→nadir→forward→zenith) | Crew-2: ~22 min apart |
| Depart | **Departure burns 0,1,2,3** | Crew-2: DB0 +0, DB1 +5 min, DB2 +48 min, DB3 +1h39 |
| Phasing | Departure **phasing burn (~6 min)** | sets ground track for splashdown |
| — | coast to deorbit opportunity | several hours |
| Deorbit | **Trunk jettison** | — |
| Deorbit | **Deorbit burn start** | **trunk + ~5 min** (DM-2, Crew-1) |
| Entry | **Splashdown** | **deorbit-burn-start + ~52–54 min** (DM-2 52, Crew-1 54) |

Cross-check: DM-2 trunk 1:51 → deorbit 1:56 (+5) → splash 2:48 (+52); Crew-1 trunk 1:58 → deorbit 2:03
(+5) → splash 2:57 (+54). The deorbit burn itself is ~12–16 min (Crew-1 Resilience 987 s); EI ~120 km;
drogues ~5.5 km / 156 m/s; mains ~1.8 km / 53 m/s; splashdown ~5–8 m/s (Phase 6 doc). [P]

### 6c. Telemetry availability, honestly, per phase  [P/D]

| Phase | Callout data | Actual time-series telemetry |
|---|---|---|
| Countdown | **YES** — NASA PDF MET | n/a (procedure) |
| Ascent | **YES** — NASA PDF MET | **DM-1 raw** (v/alt/accel/pitch); others reconstructed |
| Rendezvous | **YES** — NASA rendezvous PDF MET + burn specs | none public (relative nav is onboard-only) |
| Docking | **YES** — WP holds, capture times | LIDAR range/range-rate shown on webcast (not archived) |
| Return/undock | **YES** — NASA blogs, burn sequence | none public |
| Entry/splashdown | **YES** — drogue/main/splash callouts | webcast shows v/alt during some entry coverage (not archived) |

**Bottom line:** the **callout/event timeline is recoverable for the entire mission, every mission**, from
NASA's timeline PDFs + live blogs. **Continuous numeric telemetry is only archived for DM-1 ascent** — so
the database pairs the real per-mission callout timeline with the DM-1-derived numeric profile, which is
the faithful reconstruction. → `data/crew_missions.json` `standard_iss_crew` now carries all phases.

## Sources (running)
- [shahar603/Telemetry-Data — repo contents (DM-1 is newest crew mission)](https://github.com/shahar603/Telemetry-Data)
- [shahar603/Launch-Dashboard-API (host dead 2026-08-26)](https://github.com/shahar603/Launch-Dashboard-API)
- [Crew-2 mission timeline — Spaceflight Now](https://spaceflightnow.com/2021/04/22/crew-2-mission-timeline/)
- [Crew-6 mission timeline — Spaceflight Now](https://spaceflightnow.com/2023/03/01/crew-6-mission-timeline/)
- [Crew-2 NASA timeline PDF](https://www.nasa.gov/wp-content/uploads/2021/11/crew-2_timeline_-_april_23_0.pdf)
- [Crew-1 NASA timeline PDF](https://www.nasa.gov/wp-content/uploads/2021/05/crew-1_timeline_nov15.pdf)
- [Inspiration4 — Wikipedia (orbit 575×585, 51.6°)](https://en.wikipedia.org/wiki/Inspiration4)
- [Polaris Dawn — Wikipedia (apogee 1408 km, 51.7°)](https://en.wikipedia.org/wiki/Polaris_Dawn)
- [Fram2 polar orbit (90.01°) — Spaceflight Now](https://spaceflightnow.com/2025/03/31/live-coverage-spacex-to-launch-fram2-astronauts-to-polar-orbit-on-falcon-9-rocket-from-the-kennedy-space-center/)
</content>
</invoke>
