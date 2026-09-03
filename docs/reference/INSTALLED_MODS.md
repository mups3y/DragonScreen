# Installed mods — the evidence-gated mod-first register (C1.15)

**M1, 2026-09-04. Docs-only — no code changed, no plan edited.** Governed by `CLAUDE.md` C1.15 and
`docs/BUILD_PLAN.md` §14.4(e)/(f). Per C7, `GameData/` itself is off-limits as a build source — this file is
built from **owner/overseer evidence already written down in the repo** (mostly `docs/BUILD_PLAN.md` §B16.9,
`docs/KER_DATA_RESEARCH.md` §1.2, `docs/IVA_TARGET.md`, `REGISTER.md`'s M1/LZ1 lines, and
`docs/AUTOPILOT_RECOVERY_AUDIT.md`) plus public research (mod docs/source, cited). No task needs to go looking
in the install for any fact recorded here — that has already been done, by the owner, and written down.

This is the list `C1.15` asks every future not-yet-modelled-quantity search to check **before** writing a new
simulation. Update it (as a separate task) if the owner reports a mod added or removed.

## 1. The installed-mod list, as evidenced in this repo

| Mod | Supplies | Evidence |
|---|---|---|
| **MechJeb2** (Sarbian/MuMech release, PVG-capable) | Guidance/autopilot core Part B embeds | `docs/BUILD_PLAN.md:353`, `:804-808` |
| **RealismOverhaul** (+ `RO_SuggestedMods`) | The RSS-RO ruleset/config layer everything else sits under | `docs/BUILD_PLAN.md:1422-1424` |
| **RealFuels** | Procedural/realistic propellant tanks, **propellant-settling (ullage) state** — read by reflection in recovered `Ullage.cs` | `docs/AUTOPILOT_RECOVERY_AUDIT.md:27,617,674`; `REGISTER.md:1831-1834` |
| **TestFlight** | Part failure/reliability model — sits on `TE.19.F9.S1.Engine` with `TestFlightFailure_IgnitionFail/_ShutdownEngine/_ReducedMaxThrust/_EnginePerformanceLoss/_Explode`, `TestFlightReliability_EngineCycle` | `docs/BUILD_PLAN.md:1291-1297` |
| **Kerbal Konstructs** | Static-placement framework — `Space_X_barge_lander-2.0` (`SpaceXbarge2`), group-centre placement schema | `docs/BUILD_PLAN.md:1428, 1445-1449` |
| **TundraSpaceCenter** | A second barge static (`TSC_Barge`/`TE_Barge`) | `docs/BUILD_PLAN.md:1430-1431` |
| **Fossil Industries "SpaceX Landing Pads"** | RTLS statics — `Fossil_LZ1/LZ2/LZ4`, `Fossil_StarbasePad` | `docs/BUILD_PLAN.md:1432-1435` |
| **Kartoffelkuchen "Launchers Pack"** | A second Falcon 9 (`KK_SPX_*`/`KK_F9demo_*`, own octaweb) — must be REJECTED by the booster binding, not used; also ships `KK_SPX_ASDS`/`KK_SPX_LandingZone1` PARTS (unused — we place KK statics instead) | `docs/BUILD_PLAN.md:1279-1289, 1436-1440` |
| **Kerbal Engineer Redux** (jrbudda RO fork, 1.1.9.5) | `Stage` Δv/TWR/burn-time/Isp, terminal velocity (FAR-aware), rendezvous processor, etc. — read by runtime reflection, no vendored code | `docs/KER_DATA_RESEARCH.md:41-53, 605-643` |
| **FAR (Ferram Aerospace Research)** | Aerodynamic model — KER reflects into it for terminal velocity when present | `docs/KER_DATA_RESEARCH.md:331, 473, 537` |
| **TAC Life Support (TAC-LS)** | Cabin O2/CO2/water — already wired via `LifeSupportBridge` into `pure/CabinEnvironment.cs` | `docs/KER_DATA_RESEARCH.md:606`; `plugin/src/pure/CabinEnvironment.cs`, `plugin/src/pure/LifeSupport.cs` |
| **TundraExploration (TE)** | The Crew Dragon IVA/part pack itself — `TE_CD2_IVA*`, `TE.19.F9.S1.Engine`, etc. | `docs/IVA_TARGET.md:8-15` |
| **RasterPropMonitor (JSI)** | Present, unused (rejected for look — retro MFD, not a Dragon touchscreen); worked reference for render-to-prop-material | `docs/IVA_TARGET.md:110-113` |
| **FreeIva** | Lets the kerbal move around the IVA cabin to reach the screens | `docs/IVA_TARGET.md:114-115` |
| **SCANRPMStorage** | Carried on the pod alongside `ModuleFreeIva` | `docs/IVA_TARGET.md:114` |
| **SpaceXSuits** (TextureReplacer suit combo) | SpaceX-look kerbal suit **texture only** — cosmetic, no suit-loop pressure telemetry | `REGISTER.md:1834-1836` (confirmed installed 2026-09-03) |
| *(stock KSP CommNet — not a mod)* | Real S-Band signal strength, gated on the CommNet difficulty toggle | `plugin/src/VesselData.cs:968-987` |

## 2. Candidate-by-candidate search (the five M1 names, at minimum)

### (a) Life support / suit pressure — **RESOLVED, pre-existing (task text item (a)+(b))**
- **Cabin O2/CO2/water:** already supplied by **TAC-LS**, already wired (`LifeSupportBridge` →
  `pure/CabinEnvironment.cs`). No action needed — this is the pattern C1.15 generalizes from.
- **Suit-loop pressure specifically:** searched the full list in §1 above (TAC-LS, RealFuels, TestFlight, FAR,
  KER, RasterPropMonitor, FreeIva, SCANRPMStorage, MechJeb2, Kerbal Konstructs, TundraSpaceCenter, Fossil
  Industries, Kartoffelkuchen, RealismOverhaul, SpaceXSuits) — **none model a pressure suit.** SpaceXSuits is
  confirmed (per its own evidence line) a TextureReplacer suit-texture combo only: cosmetic, no pressure
  telemetry. **Conclusion: `pure/SuitLeakSim.cs`'s simulation stands, confirmed correct under C1.15** — it
  already says "no mod models a pressure suit" in its own header; this sweep is the formal check that
  statement owes.
- Public research (below) surfaced **Kerbalism** as a mod that *does* model suit/EVA environment resources in
  general — but it is not in the installed-evidence list, so it is a found-but-not-installed candidate, not an
  action. See Open Questions §3.

### (b) Engine reliability / FDIR — **RESOLVED, pre-existing (task text item (a))**
**TestFlight** sits on the exact booster engine part (`TE.19.F9.S1.Engine`) with a full failure/reliability
module set (`TestFlightFailure_IgnitionFail`, `_ShutdownEngine`, `_ReducedMaxThrust`,
`_EnginePerformanceLoss`, `_Explode`, `TestFlightReliability_EngineCycle`). **§B15's FDIR should read
TestFlight's model, not invent its own thresholds** — the same reflection pattern already proven for
RealFuels/`Ullage.cs`. Public research (below) confirms TestFlight's reliability design (FailureRate/MTBF that
improves with flight data, dynamic-pressure-gated ignition-failure chance) is a real, non-trivial model worth
reading rather than reproducing.

### (c) Aero (Q/AoA/drag)
- **Dynamic pressure (Q) / static pressure:** already **KSP-direct** — `vessel.dynamicPressurekPa` /
  `staticPressurekPa`, read at `VesselData.cs:398` per `docs/KER_DATA_RESEARCH.md:539`. Real, not a
  not-yet-modelled quantity; no mod needed.
- **FAR (Ferram Aerospace Research)** is installed and is the aero authority KER itself defers to
  (`docs/KER_DATA_RESEARCH.md:331,473,537` — "FAR-aware... FAR is installed"). Any Part-B aero coefficient not
  already covered by our own recovered `pure/BoosterDrag.cs` (the 48-flight Mach-binned bc curve, marked
  reference-with-provenance, §B16.9's sibling section) should check FAR before inventing a new one.
- **Drag (booster descent):** already **OUR OWN recovered pure code** (`pure/BoosterDrag.cs`), explicitly
  marked as a reference distillate, not sourced from any mod — no action; this is the correct tier already.
- **Conclusion:** no gap. Q is real, drag has a marked in-repo source, FAR is the fallback for anything else
  aero that Part B reaches for.

### (d) Heat
- **Cabin/hull temperature:** already REAL — `HullTempC` comes from stock KSP part temperature (read in
  `VesselData.cs`), and `pure/CabinEnvironment.cs`'s `CabinTempC` is a coherent simulation blended from that
  real input (`CabinEnvironment.cs:152`). This is not a not-yet-modelled quantity needing a mod: it is tier-1
  real state already, per §14.4(e) step (0).
- **Per-part quantities with no source today** (chamber pressure, SuperDraco temperature, per-bus load,
  battery temperature — `VehicleSubsystemPage.cs:304-312,358-362`): searched the installed list — none of
  TestFlight, FAR, TAC-LS or KER model per-engine chamber pressure or a per-battery thermal figure. These stay
  genuinely-absent dashes per §14.4(e); no mod closes them. **Not this task's scope to change** (M1 records
  search results, it does not re-open dashed rows) — noted here only because "heat" was one of the five named
  candidates.

### (e) Comms link budget
Not a not-yet-modelled quantity at all: **stock KSP's own CommNet** already supplies real S-Band signal
strength, wired end-to-end in `VesselData.cs:968-987` (S24) and gated on the game's CommNet difficulty toggle.
No third-party comms mod (RemoteTech, AntennaRange, etc.) appears anywhere in the installed-evidence list, and
none is needed — CommNet has no separate uplink/downlink budget, so both readouts are correctly the one real
signal. No gap, no simulation, no mod search owed beyond confirming CommNet's presence (it is base game, not a
mod, so §14.5's mod-first ladder does not even apply here).

## 3. Public research consulted

- KSP-RO/TestFlight source + wiki (`TestFlightFailure_IgnitionFail.cs`, `TestFlightReliability_EngineCycle.cs`,
  the reliability-redesign proposal) — confirms the FailureRate/MTBF model and the dynamic-pressure-gated
  ignition-failure mechanic cited in §2(b) above.
  [TestFlightFailure_IgnitionFail.cs](https://github.com/KSP-RO/TestFlight/blob/master/TestFlightFailure_IgnitionFail.cs) ·
  [TestFlightReliability_EngineCycle.cs](https://github.com/KSP-RO/TestFlight/blob/master/TestFlightReliability_EngineCycle.cs) ·
  [Reliability/Failure System Redesign](https://github.com/KSP-RO/TestFlight/wiki/Proposal-For-Reliability-and-Failure-System-Redesign)
- Search for a KSP mod modelling EVA/pressure-suit telemetry: the one credible hit is **Kerbalism**
  (`kerbalism.readthedocs.io`) — a full crew/component/resource/environment overhaul with its own
  telemetry/monitoring UI. It is **not** in this repo's installed-evidence list (§1) and is a large overhaul
  that typically replaces, not layers with, TAC-LS. Not actioned — see Open Questions below.
  [Kerbalism docs](https://kerbalism.readthedocs.io/en/stable/)

## Open questions for the owner

**Situation.** This sweep (M1) found no installed mod that models pressure-suit-loop telemetry — confirming
`pure/SuitLeakSim.cs`'s existing simulation is correctly the source of truth today. Public research turned up
one mod, **Kerbalism**, that does model suit/EVA environment resources in general, but it is not in the
installed-evidence list and was not searched for in the install (C7 forbids that; it is raised here purely as
a found-but-not-installed candidate per C1.15).

**The decision needed.** Whether Kerbalism is worth an owner-level look as a future life-support/suit data
source, given it would very likely **replace TAC-LS** (both are full life-support overhauls; they are not
designed to run together) rather than sit alongside it — a much bigger swap than adding a single-purpose mod.

**Options:**
1. **No action (recommended).** `SuitLeakSim.cs`'s marked simulation is coherent, driven by real cabin
   pressure, and this sweep confirms no lighter-weight alternative exists. Revisit only if the owner
   independently decides to evaluate a TAC-LS → Kerbalism swap for reasons beyond suit pressure.
2. **Owner evaluates Kerbalism as a TAC-LS replacement** (own research, own install) — out of scope for a
   build chat under C7 regardless of the answer, since it would change the installed mod set.

Neither option needs a build-go or `OVERRIDE` — this is a "no action needed" finding, not a blocked task; it
is recorded per C1.14 because C1.15 generates it as a natural side-finding of the sweep.
