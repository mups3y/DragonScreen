# VEHICLE AUDIT — "Perfect Control" ledger (every setting, every phase, real-world accurate)

> ⭐ **MANDATE (Chris 2026-08-28):** the correct **RCS mode, thruster limits, engine modes, throttle
> settings, fuel types, fuel tanks, fuel loads** must be set/used correctly **at all times, end to end,
> across the entire vessel** — AND every piece must be the **real Falcon 9 / Crew Dragon equivalent and
> accurate**. This is a *perfect control* subject and **high priority**. Method: **get a fresh part dump
> next flight test and go through the ENTIRE dump one part at a time** — correcting, researching, and
> logging every single piece. This file is that ledger; it is mirrored into both artifacts.

**Status:** FRAMEWORK + first pass built 2026-08-28 (this file). §B (control matrix) is VERIFIED from the
code now. §C/§D (real-world accuracy) is SEEDED from sourced real specs + the last dump; the definitive
per-part pass needs the **fresh dump** (§A). Nothing in §C is "corrected" until confirmed against the fresh
dump — I do not assert our numbers from memory (they may be stale).

**Authority order (unchanged):** live `ModuleManager.ConfigCache` > the flight CSV + `KSP.log` > the fresh
craft dump > this doc > older `.md`. Real-world column is sourced (primary/reputable) and cited inline.

---

## §A — The fresh-dump one-by-one procedure (next flight test)

1. **Dump on the pad, fully fuelled, all stages present** (`CraftDump.cs` → `data/craftdump.csv`). Re-dump
   any time the craft changes. Also snapshot the resolved `ModuleManager.ConfigCache` blocks per part.
2. **Walk the dump one PART at a time** (the §E checklist). For each part, for each module, record:
   the controllable fields (engine mode/`selectedIndex`, `thrustPercentage`/thrust limiter, `gimbalLimiter`,
   `rcsEnabled` + master, RCS `thrustPercentage`, `PROPELLANT{}` block, tank definition + volume + fill),
   what OUR autopilot commands it to per phase (§B), and the REAL F9/Dragon value (§C). Mark ✓ / ⚠ / ❓.
3. **Every discrepancy → §D** with the real value, the source, and the decision (fix cfg / fix code /
   accept as an RO modelling choice). ⛔ Do not "fix" a number until the fresh dump confirms it.
4. Re-run headless, re-install, re-fly; the emergent timeline + fuel closure are the falsifiable proof.

---

## §B — CONTROL MATRIX: what the autopilot commands, per phase (VERIFIED from the code, 2026-08-28)

Sources: `Actuator.cs`, `AscentControl.cs`, `BoosterControl.cs`, `RendezvousControl.cs`, `DockingControl.cs`,
`ReturnControl.cs`, `FlightDriver.cs`. Legend: ✓ = correct & intended · ⚠ = works but verify vs real · ❓ = needs the fresh dump.

### Booster + upper stage (ascent)
| Phase | Octaweb engines / mode | Throttle | Gimbal | RCS master | Notes |
|---|---|---|---|---|---|
| Pad / pre-launch | OFF | 0 | free | OFF | clamp gate: light **AllEngines**, hold hold-downs until ≥99% thrust + no failed engine, erector clear, then release ✓ |
| Liftoff | **AllEngines (9)** lit — `Actuator.IgniteOctawebLiftoff` (⛔ never 3/1) | 1.0 | gimbal steers (AttitudePilot) | OFF | one ignition on AllEngines ✓ |
| Ascent / gravity turn | AllEngines | **max-Q bucket + g-limit** (`GLimitG=3.5` on S1) | gimbal | OFF | `BalanceOctawebThrust` HOLDS FULL (n<2, single module) ✓ |
| MECO / stage sep | **cut octaweb** (`ShutdownBoosterEngines`) → fire interstage decoupler | 0 | — | OFF | `Actuator.Meco` ✓ |
| S2 ullage | MVac off | 0 | — | **ON** (aft-RCS settle, `s.Z`) | ullage settle before light ✓ |
| S2 burn (SES) | **MVac lit** (`IgniteSecondStage`) | UPFG + **g-limit `S2GLimitG=4.1`** | MVac gimbal steers | **OFF** (`DisableRcs` on ignite-confirm — gimbal steers, no non-stop Draco) ✓ |
| SECO | **MVac shut** (`ShutdownEngines(SecondStage)`) on `vgo<2` or `pe≥195` | 0 | — | OFF | clean cutoff ✓ |
| Dragon sep | fire Dragon decoupler (drops S2 alone, ⛔ NOT trunk) after thrust dies | — | — | — | `SeparateDragon` ✓ |

### Booster recovery (opt-in `AutoRecoverBooster`, focus-managed)
| Phase | Octaweb mode | Throttle | Attitude authority | Notes |
|---|---|---|---|---|
| Flip | OFF | 0 | **cold-gas RCS** (`ModuleRCSFX`) | hold retrograde ✓ |
| Entry burn | **ThreeLanding (3)** — select by activating the matching-`engineID` module WHILE OFF (⛔ never NextEngineMode) | metered | gimbal (lit) | 1 ignition on ThreeLanding ✓ |
| Aero descent | OFF | 0 | **grid fins** + cold-gas | fins deployed once ✓ |
| Landing burn | **CenterOnly (1)** — lit ONCE, continuous to deck (⛔ no mid-burn 3→1) | hoverslam feather | gimbal | 1 ignition on CenterOnly; legs deploy final ✓ |

### Crew Dragon (orbit → dock → return)
| Phase | Draco RCS (`ModuleRCSFX`) | Draco engine (`ModuleEnginesRF`) | RCS master | CoM shifter | Nose shroud | Notes |
|---|---|---|---|---|---|---|
| Coast / phasing | idle | idle | ON (attitude) | centred/off | closed | pe-floor gated; time-warp compresses coasts ✓ |
| Rendezvous burns | **translation `s.Z=ForwardSign(−1)`** on the nose-pointed axis | — | ON | off | **OPEN** (fwd Dracos shielded until open) ✓ |
| Near-field CW / dock | 3-axis servo `s.X/Y/Z` (**signs all −1**, MechJeb-derived) | — | ON | off | open | attitude-first-then-translate ✓ |
| Docked | idle | — | ON→idle | off | open | auto-gates G10–G14 ✓ |
| Undock / departure | translation `s.Z` | — | ON | off | open | CW departure hops ✓ |
| Deorbit | **retrograde translation `s.Z`** (trunk jettisoned first) | ⛔ **never throttled** (see §D-4) | ON | **engage Descent Mode ONCE** | closed | burn on measured pe ✓ |
| Entry | roll (bank σ) — RCS | — | ON | Descent Mode held | closed | `\|σ\|` downrange + S-turn reversals; shield-forward SAS ✓ |
| Chutes / splash | idle | — | idle | held | — | **RealChute GUIDeploy once** — drogues ≤5.5 km, mains ≤1.83 km ✓ FLIGHT-PROVEN (202127) |
| ABORT (any phase) | attitude (geometric-torque fallback) | — | ON | — | as-is | **SuperDraco** full-throttle + capsule sep + chutes ✓ FLIGHT-PROVEN |

**Thruster limits (`thrustPercentage`):** held at **100%** everywhere EXCEPT the engine-out differential-throttle
contingency (`BalanceOctawebThrust`, ≥2 independent modules only — our octaweb is 1 module → HOLD FULL). ✓
**RCS master:** ON for every capsule maneuver/attitude; **OFF during the S2 gimballed burn** (so fine attitude
doesn't fire the Dracos non-stop); OFF on the pad/ascent. ✓

---

## §C — REAL-WORLD ACCURACY ledger (real value sourced; OUR value to confirm vs the fresh dump)

⛔ The "OUR value" column is **left to fill from the fresh dump / live ConfigCache** — I will NOT assert it
from memory. The real column is sourced; the job is to compare and reconcile.

### Propulsion
| System | REAL Falcon 9 Block 5 / Crew Dragon | Source | OUR value (fresh dump) | Status |
|---|---|---|---|---|
| Merlin 1D (S1), sea level | **854 kN** thrust, Isp **282 s**, throttle to ~**40–70%** | Wevolver/Wikipedia F9 B5 | ❓ (`ModuleEnginesRF` maxThrust × config) | verify |
| Octaweb total (9× M1D) SL | **7,600 kN** max | Wikipedia F9 B5 | ❓ AllEngines CONFIG | verify (memory had ~6,682 — reconcile) |
| Merlin 1D-Vac (S2, MVac) | **934–981 kN** vac, Isp **311–348 s**, throttle to ~**70%** | Wevolver/Wikipedia | ❓ SecondStage CONFIG | verify (memory had ~805 — reconcile) |
| Merlin gimbal range | ~**±5°** (pitch/yaw); MVac gimbals | (spec sheets; confirm) | ❓ `ModuleGimbal.gimbalRange` | verify (patch `F9_S1_GimbalRange` currently DISABLED → stock 2° — likely too low) |
| Merlin ignitions | S1 relightable (entry+landing); each octaweb MODE = **1** | RO config (§3a) | 1 per mode | ✓ (verify per fresh dump) |
| SuperDraco (abort) | **8 × 71 kN** (16,400 lbf), MMH/NTO, Isp ~235 s | docs/FLIGHT_SYSTEMS | ❓ (ConfigCache showed a 680 kN CONFIG — per-engine vs total?) | verify |
| Draco (RCS + deorbit) | **16 × ~400 N** (90 lbf), MMH/NTO, Isp ~300 s | SpaceX | ❓ `ModuleRCSFX thrusterPower` (last seen **2 kN** = 5× real) | ⚠ §D-1 |

### Propellant / tanks / loads
| Item | REAL | Source | OUR value | Status |
|---|---|---|---|---|
| S1 propellant | RP-1 / LOX, ~**411 t** | Wikipedia F9 B5 | ❓ `ModuleFuelTanks` | verify |
| S2 propellant | RP-1 / LOX, ~**108 t** | Wikipedia F9 B5 | ❓ | verify |
| Dragon maneuvering prop | MMH / NTO (Draco RCS **and** deorbit) | ConfigCache | **MMH 655 / NTO 509** (ConfigCache); Draco `ModuleRCSFX` = MMH+NTO ✓ | ✓ RCS path confirmed |
| Dragon MonoPropellant 300 u | not a real resource (KSP artifact of the Draco `ModuleEnginesRF`) | ConfigCache | 300 u — unused by our autopilot (all burns are RCS) | ✓ dead weight, leave (M5) |
| Helium pressurant | real (both stages, Dragon) | ConfigCache | present | ✓ |

### Structures / recovery / other
| Item | REAL Crew Dragon / F9 | Source | OUR value | Status |
|---|---|---|---|---|
| Parachutes | **2 drogues + 4 mains** | SpaceX | ❓ (dump: "Drogue"/"Main" parts) | verify count |
| Chute deploy altitudes | drogues **5,486 m** (18 kft), mains **1,830 m** (6 kft) | docs/FLIGHT_SYSTEMS | drogues ≤5.5 km, mains ≤1.83 km | ✓ matches |
| Heatshield | **PICA-X** | SpaceX | PICA-X part | ✓ |
| Landing legs | **4** | SpaceX | 4 (`ModuleWheelDeployment`) | ✓ |
| Grid fins | **4 titanium** | SpaceX | 4 (`SyncModuleControlSurface`) | ✓ |
| Docking system | **NDS / IDA-compatible** (androgynous) | NASA | `ModuleDockingNode` | ✓ |
| Cold-gas thrusters (S1 flip) | nitrogen | SpaceX | `ModuleRCSFX` ×2 | ✓ (propellant: verify N₂ vs mono) |

---

## §D — OPEN DISCREPANCIES / DECISIONS (surface for Chris; fix only after the fresh dump confirms)

- **D-1 — Draco RCS thrust (`thrusterPower`).** Real Draco = ~400 N; last seen `thrusterPower = 2` kN (5× real).
  The OLD (deleted) autopilot deliberately ×5-boosted it because the free Dragon was ~0.03 m/s² vs real ~0.15.
  Tension: **real accuracy vs controllability.** Decision needed — match real 0.4 kN (slow, real-faithful) or
  keep a boost. ⭐ Note the real deorbit uses the Draco **engine** (bigger), which we don't fire (D-4).
- **D-2 — Octaweb SL thrust** (real 7,600 kN) and **D-3 — MVac thrust** (real 934–981 kN): memory had ~6,682 /
  ~805; **reconcile against the fresh ConfigCache** — RO configs are meant to be realistic, so the memory may be
  stale or a throttle/condition artifact. Do NOT change until confirmed.
- **D-4 — Deorbit propellant path.** Real Crew Dragon deorbits on the Dracos. We do the deorbit via **RCS
  translation** (`ModuleRCSFX`, MMH+NTO) and NEVER throttle the Draco `ModuleEnginesRF` (which burns the vestigial
  MonoProp). Real-faithful for the *propellant* (MMH+NTO ✓) but via the RCS module, not the throttled engine —
  acceptable, but note it. Fuel closure rides the 655/509 MMH/NTO budget.
- **D-5 — Merlin gimbal range** likely too low (patch disabled → stock 2° vs real ~±5°). Confirm + re-enable a
  realistic range if the fresh dump shows 2°.
- **D-6 — Chute count**: confirm 2 drogues + 4 mains (real) vs the dump's part grouping.

---

## §E — The one-by-one PART checklist (fill from the fresh dump)

Each row: confirm modules, controllable fields, our per-phase use (§B), real equivalent (§C), status. Tick when done.

| # | Part | Audited? | Notes |
|---|---|---|---|
| 0 | Crew Dragon pod (Draco engine + Draco RCS + SuperDraco + nose ModuleAnimateGeneric + CoM shifter) | ☐ | RCS=MMH+NTO ✓; D-1 Draco thrust; D-4 deorbit path |
| 1 | Drogue parachutes (RealChute) | ☐ | count + deploy alt (D-6) |
| 2 | Main parachutes (RealChute) | ☐ | count + deploy alt (D-6) |
| 3 | PICA-X heatshield | ☐ | ✓ |
| 4 | Trunk (decoupler + crossfeed + solar) | ☐ | jettison before entry ✓ |
| 5 | Trunk adapter / S2 decoupler | ☐ | drops S2 ✓ |
| 6 | S2 tank | ☐ | RP-1/LOX load (D-3) |
| 7 | MVac (S2 engine) | ☐ | thrust/Isp/gimbal (D-3, D-5) |
| 8–11 | S2 RCS ×4 | ☐ | propellant (N₂ cold-gas?) |
| 12 | S1 interstage (gimbal + decoupler) | ☐ | stage-sep ✓ |
| 13 | S1 tank | ☐ | RP-1/LOX load (D-2) |
| 14 | Octaweb (9 Merlins, 3 modes) | ☐ | thrust/Isp/gimbal/ignitions (D-2, D-5) |
| 15 | Erector | ☐ | retract before ignition ✓ |
| 16–19 | Landing legs ×4 | ☐ | ✓ |
| 20–21 | Cold-gas thrusters ×2 | ☐ | flip authority ✓ |
| 22–25 | Grid fins ×4 | ☐ | ✓ |
| 26 | NASA Docking System | ☐ | ✓ |

---

**Related:** [CRAFT_DUMP_VEHICLE_MAP.md](CRAFT_DUMP_VEHICLE_MAP.md) (capability map), `data/craftdump.csv`,
[ISSUE_REGISTER.md](ISSUE_REGISTER.md) (M5 MonoProp, S2/S4 RCS signs), [[dragon-return-propellant-mmh-nto]],
[[falcon-detect-by-capability]]. **Sources:** [Wikipedia Falcon 9 Block 5](https://en.wikipedia.org/wiki/Falcon_9_Block_5),
[Wevolver Merlin 1D](https://www.wevolver.com/specs/merlin-engine-merlin-1d-falcon-9-falcon-heavy), `docs/FLIGHT_SYSTEMS.md`.
