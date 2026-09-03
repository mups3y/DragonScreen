> **RECOVERED 2026-09-04 by W26** from `8b81816^` (deleted by `8b81816`). R1 verdict: **RECOVER-REFERENCE**.
> **REFERENCE ONLY — `docs/BUILD_PLAN.md` WINS on any conflict (C7.1).** Written before 2026-08-28; the
> plan has since moved on. Read it for method, evidence and reasoning — never as current instruction.

# Crew Dragon GN&C — the real system, and how to represent it in KSP (RSS/RO)

> **Why (2026-08-27):** the honest assessment flagged docking + entry fidelity as weak and "sensor-limited."
> This research corrects that: it maps the REAL Crew Dragon guidance/navigation/control + sensor suite against
> what KSP gives us, and pins down where our fidelity gap actually is. **The surprising conclusion: our gap is
> NOT navigation — it is control authority.** Primary/secondary sources at the bottom.

---

## 1. The real sensor suite (what Dragon uses to know where it is)
- **Relative GPS (RGPS):** both Dragon and the ISS carry GPS; differencing their solutions gives a relative
  position/velocity good enough to approach a target moving at ~7.7 km/s. This is the far/mid-field nav.
- **DragonEye flash LIDAR** (128×128 pixel): ranging from hundreds of metres in to contact — the near-field
  relative range/bearing.
- **Visual cameras + machine vision:** track the docking target/retroreflectors on the ISS; the computer
  checks the vision solution against LIDAR for redundancy.
- **Thermal/IR cameras:** approach & alignment in eclipse.
- **IMU (accelerometers + gyros) and star trackers:** inertial attitude + acceleration (the absolute-attitude
  reference); the nav filter (SpaceX's "Dragonfly"-class filter, arXiv 2307.13513) fuses IMU + RGPS + LIDAR +
  vision into one relative state estimate.
- **Manual backup:** the crew can fly the terminal approach on the touchscreen if the autonomous system faults.

## 2. The real GN&C flow (nominal)
- **Ascent:** Falcon 9 flies its own closed-loop guidance to insertion; Dragon is a passenger, monitoring for
  abort. Dragon's SuperDracos are armed for launch escape (LES).
- **Phasing → rendezvous:** a sequence of named Draco burns (Phase, Boost, Coelliptic, Approach Initiation,
  Midcourse) raises Dragon from the ~200 km insertion toward the ISS (~420 km) over hours, closing the phase
  angle; RGPS drives it. The AI burn (~90 s, ~0.72 m/s) at ~7.5 km sets up the terminal approach.
- **Terminal approach & docking:** waypoint holds (WP0 ~400 m below on R-bar → WP1 ~200 m on V-bar → WP2 ~20 m)
  with crew GO gates; the vehicle keeps itself inside a corridor and outside the keep-out sphere; LIDAR+vision
  close the last metres to soft capture at ~0.1 m/s, then hard capture (hooks).
- **Return:** undock → departure burns → phasing → **trunk jettison → deorbit burn** (Dracos) → **bank-angle
  lifting entry** (offset CoM sets the trim AoA / L·D; roll the lift vector for downrange, bank reversals for
  crossrange — the Apollo/Orion guided-entry law) → drogues → mains → splashdown.

> ⚠ **UPDATED 2026-08-27 — the user chose STRICT IMPLEMENTATION FIDELITY over behavior-only fidelity, which
> REVERSES this doc's original "don't build sensor emulation" conclusion.** §3's analysis (our gap is CONTROL,
> not sensing) still holds — the RCS balancer is still the docking enabler — but strict fidelity ADDS a
> requirement ON TOP: replicate the real NAVIGATION pipeline (simulate the sensor suite from KSP ground truth
> with realistic noise, run a nav filter, fly the guidance on the ESTIMATE). This both matches the real vehicle
> AND proves robustness to real nav error. See the revised §5. [[crew2-full-fidelity-no-deviation]].

## 3. ⭐ THE KEY INSIGHT — our fidelity gap is INVERTED (control, not sensing) — but strict fidelity adds the nav pipeline
Real Dragon spends enormous engineering effort to **ESTIMATE** the relative state from noisy, dropout-prone
sensors (that is what the LIDAR, vision, RGPS, and the Dragonfly nav filter are FOR). **KSP hands us the
relative state EXACTLY** (`v.orbit`, `tgt.GetOrbit()`, positions/velocities are ground truth). So:
- **We do NOT need a navigation filter, LIDAR emulation, or machine vision.** Building one would be fidelity
  theatre — KSP already gives us a better relative state than Dragon's real sensors produce.
- **Our real problem is CONTROL:** the RO Dragon has **no reaction wheels** and 16 Dracos that share rotation
  and translation. Holding attitude AND translating precisely to soft-capture on a moving port, on thrusters
  alone, is the hard part — exactly what the **RCS balancer (P0)** and the attitude-first-then-translate
  discipline address. The docking hole is an AUTHORITY hole, not a SENSING hole.
- **Corollary for robustness:** because our nav is ground-truth-clean, the honest robustness test is to
  **inject sensor noise into the relative state** in the Tier-2 dispersion harness and confirm the approach
  still converges — i.e. prove we'd be fine EVEN with realistic sensor error, without simulating the sensors.

## 4. Fidelity map — real capability → our representation → gap
| Real Dragon | Our representation | Honest gap |
|---|---|---|
| RGPS relative nav | exact KSP relative orbit state | none (we're better; optionally add noise to test robustness) |
| DragonEye LIDAR / vision terminal nav | exact KSP relative position to the port | none for guidance; we don't render sensors |
| Star tracker / IMU attitude | KSP exact attitude + rates | none |
| Draco 6-DOF control (no RW) | AttitudePilot + RCS translation; **RCS balancer = P0** | ⚠ REAL — precise thruster-only soft-capture; the authority hole |
| Autonomous docking to a live ISS | our L-approach FSM + crew gates | ⚠ untested in flight; KOS auto-abort not wired |
| Bank-angle guided entry (Apollo law) | Entry/EntrySteering + CoM shifter | ⚠ works open-loop; precision splashdown needs the reentry-sim predictor (P2) |
| LES / SuperDraco abort | AbortControl (regime-aware, researched g-limits) | ✅ flown, works |
| Manual touchscreen backup | crew HOLD/ABORT gates + (future) manual-takeover at WP1/WP2 | ⚠ manual-takeover not wired |

## 5. What this adds to the plan (concrete) — REVISED for strict fidelity (2026-08-27)
1. **BUILD the real navigation pipeline (new capability, strict fidelity + robustness).** A `pure/NavFilter.cs`
   layer between nav and guidance: **simulate the sensor suite** from KSP ground truth — relative-GPS
   (position/velocity + noise), DragonEye **LIDAR** (range/bearing, valid <~hundreds of m, with dropouts),
   star-tracker/IMU (attitude + rates + bias/walk) — then **fuse them in an EKF** (Dragonfly-class) to produce
   the ESTIMATED relative state, and **fly the guidance on the ESTIMATE, not raw ground truth.** This matches
   the real vehicle AND directly answers "would it work with real sensors" (the estimate carries realistic
   error). Sensor models + the filter are headless-testable (assert the estimate tracks truth within a
   covariance bound). Slots as an **L1.5 nav layer**; the docking/rendezvous guidance then consumes the
   estimate. The CONTROL gap (RCS balancer, §4) is unchanged and still the docking enabler.
2. **Dispersion still injects sensor error** (Tier-2), now on the FILTER's inputs — assert the estimate stays
   bounded and the approach converges + never breaches the KOS across sensor noise/dropout seeds.
3. **Wire the KOS auto-abort** in docking (breach → AbortResponder KosRetreat) — currently only the crew gate
   path exists.
4. **Wire manual-takeover at WP1/WP2** (the real touchscreen mode) — the crew can assume translation control;
   the autopilot holds attitude. A crew-gate capability, not a nav one.
5. **Entry precision** is a predictor problem (reentry-sim, P2), not a sensor problem — confirmed.

## Sources
- [How SpaceX Uses AI — Dragon nav/vision/LIDAR](https://www.sentisight.ai/how-spacex-uses-ai-in-space-exploration/)
- [SpaceX Dragon uses LIDAR (DragonEye)](https://www.nextbigfuture.com/2020/11/spacex-dragon-uses-lidar.html)
- [Preliminary Design of the Dragonfly Navigation Filter (arXiv 2307.13513)](https://arxiv.org/pdf/2307.13513)
- [DragonEye sensor demo on Shuttle (Defense Daily)](https://www.defensedaily.com/spacexs-dragoneye-sensor-successfully-demonstrated-on-space-shuttle/space/)
- [Autonomous docking overview](https://chimniii.com/news/Science/Space/autonomous-docking-systems-how-spacexs-crew-dragon.html)

Cross-refs: `docs/PHASE_4_DOCKING_RESEARCH.md` · `docs/VALIDATION_AND_ROBUSTNESS.md` · [[dragon-nose-cone-rcs]]
· the RCS balancer P0 item in `docs/MECHJEB_CAPABILITY_INTEGRATION.md`.
