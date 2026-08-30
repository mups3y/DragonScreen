# SCREEN EVIDENCE MATRIX (ACTIVE)

> Every screen feature carries an evidence class + source confidence, so reconstruction is never presented as confirmed SpaceX fact (rules E1/E2). Governed by `MASTER_BUILD_SPEC.md`. Where public evidence does not exist (some Dragon internals are confidential), the honest label is RECONSTRUCTED or SIMULATION with the confidence stated — never a false claim of fidelity.

**Classes:** CONFIRMED (direct primary evidence) · STRONGLY SUPPORTED (multiple credible sources) · RECONSTRUCTED (reasonable, public evidence incomplete) · SIMULATION (DragonScreen/KSP feature, not a SpaceX claim).
**Source confidence:** Very High · High · Medium · Low · Intentional (deliberate simulation).

**Primary references (RESEARCH, not instruction):** Shane Mielke — Crew Dragon Displays; Mielke — ISS Docking Simulator; iss-sim.spacex.com; Behance (SpaceX ISS Docking Simulator); mutantdragon reconstruction; repo `UI_AUDIT.md`, `REAL_DRAGON_SCREENS.md`, `REFERENCE_PAGES.md`, `PALETTE.md`.

## Docking page (highest-confidence screen — the reference implementation)
| Feature | Class | Source | Confidence |
|---|---|---|---|
| Translation cluster (UP/DOWN/LEFT/RIGHT/FWD/BACK) on one side | CONFIRMED | iss-sim / Mielke | Very High |
| Rotation cluster (ROLL/PITCH/YAW) on the other side | CONFIRMED | iss-sim / Mielke | Very High |
| Central target reticle + alignment graphic | CONFIRMED | iss-sim | Very High |
| Green central correction values | CONFIRMED | iss-sim | Very High |
| Closing RATE readout | CONFIRMED | iss-sim | Very High |
| Precision (fine/coarse) mode toggle | CONFIRMED | iss-sim | High |
| 0.2 / −0.2 m/s success thresholds | STRONGLY SUPPORTED (simulator-specific) | iss-sim | Medium — label "training guidance," not operational limit |
| AUTO/MANUAL indicator | RECONSTRUCTED | real Dragon docks autonomously; iss-sim is manual/training | High |
| Capture-envelope status (IDSS) | RECONSTRUCTED | IDSS spec + our `DockCapture` | Medium |

## Global chrome / other pages (seed)
| Feature | Class | Source | Confidence |
|---|---|---|---|
| Persistent nav/status bar (phase, status, comm/link, MET) | STRONGLY SUPPORTED | mutantdragon reconstruction + reference set | High |
| Visual language (dark blue/black bg, cyan primary, state colours) | STRONGLY SUPPORTED | `PALETTE.md` (measured) + references | High |
| D-DIN typography, 16 px base | STRONGLY SUPPORTED | measured from reference set | High |
| Three-display LEFT/CENTRE/RIGHT roles | RECONSTRUCTED | known three-screen layout; exact per-screen content varies | Medium |
| Systems/Overview subsystem pages | RECONSTRUCTED | mutantdragon + operational reasoning | Medium |
| Navigation 2D/3D map, ISS/ground-track/landing zone | RECONSTRUCTED | mutantdragon | Medium |
| TAC-LS / ECLSS readouts | SIMULATION | DragonScreen + KSP TAC-LS | Intentional (High as simulation) |
| Thermal / Power values | SIMULATION (real KSP/RO state) | KSP/RO | High as simulation |
| Comedic abort screen / Easter eggs | SIMULATION | DragonScreen | Intentional |

> Rule: any feature not backed by evidence above is `UNKNOWN — EVIDENCE REQUIRED` until researched; do not fill it with invention (rule E3). Update this matrix as each page is designed (before it is coded, rule S4).
