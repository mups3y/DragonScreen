# Free-flyer mission profiles — closing the "any mission" generality hole

> **Why (2026-08-27):** the honest assessment flagged "any Crew Dragon mission" as an architecture claim, not a
> demonstrated one. The mission DATA is already in `data/crew_missions.json` (verified below against primary
> sources). The hole is the autopilot CAPABILITY to fly a non-Crew-2 profile, plus validation. This doc pins
> the four archetypes, what each needs, and what is already there vs to build.

---

## The four mission archetypes (Crew-2 is one of four, not the only shape)
| Archetype | Real mission | Orbit (verified) | Rendezvous/dock? | The new demand |
|---|---|---|---|---|
| **ISS crew (reference)** | Crew-2 | ~420 km circular, **51.6°**, docks | YES | the full pipeline (done/in-progress) |
| **High circular free-flyer** | Inspiration4 | **575×585 km, 51.6°**, no dock (cupola) | NO | insert to a HIGH circular parking orbit (not 200 km) |
| **High elliptical + on-orbit reshape** | Polaris Dawn | insert ~**190×1200 km → raise apo to ~1400 → lower apo to ~700**, **51.7°**, EVA (whole-cabin depress, no airlock) | NO | elliptical insertion + **on-orbit apogee raise/lower burns** + an **EVA hold phase** |
| **True polar free-flyer** | Fram2 | **202×413 km, 90.01° (retrograde polar)**, no dock | NO | **polar launch corridor / dogleg** + 90° azimuth |

All free-flyers end the same way as Crew-2: deorbit → bank-angle entry → drogues/mains → splashdown.

## What the autopilot needs to fly each (capability map)
| Capability | Status | Notes |
|---|---|---|
| Mission-as-data by VAB craft name → profile | ✅ | `pure/MissionProfile.cs` resolves all 20; free-flyer data present + verified |
| Omit rendezvous/dock/undock for free-flyers | ✅ | free-flyer flag in the mode manager (crew gates G9–G14 omitted) |
| **Parameterized insertion target (any peri/apo/inc), not hard-coded 200 km** | ⚠ | ascent targets a parking orbit; must read `orbit.peri_km/apo_km/inc_deg` from the profile — trivial data plumbing, verify UPFG/PVG hits a 585 km / elliptical target |
| **On-orbit apogee raise/lower burns** (Polaris 1200→1400→700) | ❌ | needs the **maneuver library (P1)**: `ChangeApoapsis` prograde burn at periapsis + circularize/lower — the same Draco finite-burn executor |
| **Polar launch corridor / dogleg** (Fram2 90°) | ⚠ | `LaunchAzimuth.GroundRad` handles inc=90°; the real Fram2 flew a **dogleg south** from KSC to avoid overflight — add a corridor option (azimuth bias early, correct later) if the straight polar ascent is disallowed by geography; for KSP it can fly straight polar |
| **EVA hold phase** (Polaris: cabin depress, attitude hold, life support) | ❌ | a crew-ops gate phase: hold a stable attitude, keep RCS quiet, TAC life-support monitored, crew does the EVA on the screens; no guidance, just a HOLD + gate |
| Elliptical/high-orbit deorbit + entry from a higher/faster orbit | ⚠ | deorbit Δv scales with the orbit; the closed-loop deorbit-to-Pe handles it, but entry from 585 km / after a high-apogee is faster — verify the bank-entry g-limit holds (Tier-2 dispersion covers this) |

## Honest status of "any mission"
- **Data:** ✅ present + verified against primary sources.
- **Architecture:** ✅ real (guidance flies physics to the profile's targets; only the DATA changes).
- **Capability:** ⚠ Crew-2 (ISS-dock) is the only shape being flown; the free-flyer shapes need (1) insertion-
  target parameterization, (2) the maneuver library for on-orbit raise/lower, (3) a polar corridor option,
  (4) an EVA hold phase. Items (1) and (2) are already on the P1 list; (3)/(4) are small additions.
- **Validation:** ❌ none flown for a free-flyer. The Tier-2 dispersion harness should sample ALL FOUR target
  orbits so the guidance is proven to reach any of them before we ever fly one.

## The build additions this creates (folded into the plan)
- **P1+ (with the maneuver library):** insertion-target parameterization (read peri/apo/inc from the profile);
  on-orbit `ChangeApoapsis`/`ChangePeriapsis` burns for Polaris-class reshaping.
- **P2:** EVA hold phase (crew-ops gate: attitude-hold + life-support + GO/END gates); polar-corridor azimuth
  option for Fram2.
- **Validation:** the dispersion envelope includes {ISS 420×51.6, high-circular 585×51.6, elliptical
  190×1400×51.7, polar 413×90} as target cases — prove ascent + deorbit reach all four before flying.

## Sources (primary/near-primary)
- Polaris Dawn: [polarisprogram.com/dawn](https://polarisprogram.com/dawn/) · [Spaceflight Now — spacewalk](https://spaceflightnow.com/2024/09/12/polaris-dawn-crew-gears-up-for-thursday-spacewalk/) (200×1400→700 km, 51.7°, EVA ~740 km)
- Fram2: [NASASpaceFlight — Fram2 launch](https://www.nasaspaceflight.com/2025/03/fram2-launch/) · [NSS](https://nss.org/spacexs-historic-fram2-mission-a-breakthrough-in-polar-orbit-flight/) (202×413 km, 90.01° retrograde polar)
- Inspiration4: [eoPortal](https://www.eoportal.org/satellite-missions/inspiration-4) · [space.com](https://www.space.com/inspiration4-spacex.html) (~585 km, 51.6°, cupola/no dock)

Cross-refs: `data/crew_missions.json` · [[crew-mission-telemetry-database]] · `docs/MECHJEB_CAPABILITY_INTEGRATION.md` (maneuver library P1) · `docs/VALIDATION_AND_ROBUSTNESS.md`.
