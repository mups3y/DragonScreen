# Crew-2 mission assessment — how close is the flight software to the real thing?

Honest phase-by-phase audit of the DragonScreen RSS/RO Crew-2 flight computers against the REAL
SpaceX/NASA Crew-2 mission (Endeavour, 23 Apr 2021). For each phase: the real method, what our code
does, the verdict, RO-rules adherence, and whether the flight computer has **full control**.

Legend: ✅ matches real & flight-proven · 🟡 works but not the real technique / unvalidated · 🔴 gap.

---

## 1. Launch — pad, ignition, liftoff  ✅
- **Real:** LC-39A. 9 Merlin 1D ignite ~T-3 s (staggered), held down; computer checks thrust/health;
  clamps release at T0 only if nominal.
- **Ours:** `AutoPilot.RoLaunch` → `IgniteFirstStage` lights the S1 **by capability**, spools while
  clamped, checks TWR ≥ 1, then releases the erector/clamp by capability. Now lights **only the
  all-engines octaweb mode** (the 3-modes-at-once bug is fixed).
- **RO:** TEATEB ignition + ullage honoured; ground start settled by gravity.
- **Control:** FULL. **Verdict: ✅** — sequence and gating match the real pad flow.

## 2. Ascent to MECO — gravity turn, MaxQ  ✅ (plane just re-tuned)
- **Real:** pitch program / gravity turn, MaxQ ~T+1:00, throttle bucket, MECO ~T+2:30 at ~mach 8-10.
- **Ours:** `pure/Ascent.cs` gravity turn (ForBody, MEASURED constants) to MECO ~64 km / mach 8, on the
  51.6° azimuth from `LaunchAzimuth`. Inclination bias re-tuned 3.7→1.5° after the corrected ascent
  overshot to 53.78°.
- **RO:** FAR aero, RealFuels.
- **Control:** FULL. **Verdict: ✅** for the profile; ⚠ **RAAN/launch-window still to verify** — plane
  match needs the launch to be timed to the ISS's node, not just the inclination (see §Open).

## 3. Second-stage insertion — SES-1 → SECO  ✅ (this session)
- **Real:** SES-1 ~8 s after sep, single Merlin-Vac burn to SECO-1 ~T+8:47, ~190 km orbit.
- **Ours:** MVac lit **directly by capability** after an ullage-gated settle, then **UPFG** (MechJebLib
  primer-vector optimal guidance) flies the stage to SECO and cuts. Reached a real orbit (pe 206 km)
  once the fuel was right.
- **RO:** the big wins this session — **Cooled-propellant fuel match** (was the "No propellants" bug),
  **live ullage tracking** (`UllageProbe` reads `ullageSet.GetUllageStability`), TEATEB, ignition
  reliability. RCS-fore no longer fires through the burn (UPFG-return bug fixed).
- **Control:** FULL (UPFG owns thrust + steering). **Verdict: ✅** — this is genuinely the real method
  (optimal-guidance insertion), now working.

## 4. Booster recovery — boostback/entry/landing on the droneship  🔴
- **Real:** flip, boostback burn (for droneship: partial), entry burn ~T+7 at ~high altitude BEFORE the
  dense air, grid-fin guided descent, landing burn, touchdown on ASDS.
- **Ours:** `BoosterRecovery` flip → coast → entry → landing. Grid-fin control-while-stowed bug fixed.
- **Verdict: 🔴 GAP.** Last flight: entry/landing burn planned "ignition at 0.49 km" and hit an **RO
  Ignition Failure — high dynamic pressure** (lighting deep in the atmosphere), plus **not enough
  propellant** left for boostback+landing. The entry burn must light HIGH (thin air, low Q) as the real
  booster does, and the ascent must reserve booster propellant. **RO rule currently VIOLATED** (igniting
  at high Q). Control: PARTIAL.

## 5. Dragon separation from S2  ✅
- **Real:** Dragon separates from the second stage ~T+12 min; nosecone opens.
- **Ours:** `SeparateSecondStage` drops the S2 on the **Dragon decoupler** (trunk stays), arms the
  Dracos, nosecone handled by `FlightCommands`. **Control:** FULL. **Verdict: ✅.**

## 6. Rendezvous — phasing up to the ISS  🟡
- **Real:** Dragon inserts LOW and closes over ~1 day with the NASA/SpaceX **named-burn sequence** —
  NC (phasing/height), NH, NCC (corrective), coelliptic, then **TI (terminal initiation)** onto the
  R-bar. All ground/onboard-targeted, offset-aimed, never a straight chase.
- **Ours:** `StationApproach` — **now** raises the orbit to the station via a proper **Hohmann transfer**
  (fixed this session; was refusing), then bounded phasing + free-intercept (ride a close pass and match
  velocity) + CW terminal. Never chases the target (the `falcon-rendezvous-approach-law` rule).
- **Verdict: 🟡** — orbit-mechanically sound and safe, and the altitude/plane dead-end is fixed, **but it
  is an ad-hoc phasing ladder, not the real NC/NH/NCC/coelliptic/TI named-burn profile.** Control: FULL.

## 7. Proximity ops + approach — R-bar/V-bar  🟡
- **Real:** approach on the R-bar/V-bar through station-keeping **waypoints** (each a hold awaiting GO),
  200 m keep-out sphere, dock at **Harmony forward / IDA-2**.
- **Ours:** `WaypointApproachOps` (the real WP0 400 m-below → WP1 220 m-ahead → WP2 20 m L-approach with
  the fly-around arc) is **BUILT and tested but gated OFF** (`Enabled=false`); the live path is the
  straight-in `DirectApproachOps`.
- **Verdict: 🟡** — the real profile exists in code but has never flown; needs enabling + a validation
  flight. Control: FULL.

## 8. Docking  ✅
- **Real:** soft capture → hard dock at the forward port.
- **Ours:** `DockingOps` — picks the matching free port, keep-out-sphere rounding, two-move dock, roll
  aligned at the port. Flight-proven earlier. **Control:** FULL. **Verdict: ✅.**

## 9. Docked ops / "refuel"  🟡 (not a real Crew-2 step)
- **Real:** Crew Dragon **does not refuel** at the ISS — it stays docked ~6 months as a lifeboat. Our
  "refuel while docked" is a KERBIN-CHOICE mission compression, not a real technique.
- **Verdict: 🟡** — fine as a mission device; flag it as non-Crew-2 so we don't call it "real".

## 10. Undock + departure  🟡
- **Real:** hooks release, springs push, small departure burns, then the de-orbit sequence.
- **Ours:** `UndockOps` + `UndockPush`. Works mechanically; departure-burn fidelity unaudited.
- **Verdict: 🟡.** Control: FULL.

## 11. De-orbit  🟡 (targeted return exists; user hit the EMERGENCY button)
- **Real:** trunk jettison, a **targeted** de-orbit burn sized to put entry interface on a trajectory to
  the splashdown zone off Florida.
- **Ours — CORRECTED:** there are TWO paths. `DEORBIT NOW`/`WATER` are the **emergency** buttons, by
  design "deorbit immediately, parachute, land ANYWHERE the track falls" (the interim retrograde-to-Pe) -
  that is what the crew pressed, so it behaved as built. The **targeted return** (panel "STRING 1C") runs
  the full `DeorbitOps`, which aims at the **Crew-2 Gulf-of-Mexico splashdown** (`SplashdownEarthLat/Lon`,
  off Pensacola) and flies the AIM-MISS to it.
- **Verdict: 🟡** — the real targeted de-orbit EXISTS; it needs a validation flight for RSS splashdown
  accuracy (memory notes a ~92 km long bias and an adaptive-de-orbit-point fix). Control: FULL on the
  targeted path.

## 12. Entry + splashdown  🟡 (lifting-entry machinery exists)
- **Real:** lifting entry (bank-angle steering to the target), drogues ~5.5 km, mains ~1.8 km, parachute
  splashdown off Pensacola/Florida.
- **Ours — CORRECTED:** `EntryOps` DOES fly a **lifting entry with a bank controller**
  (`pure/EntryGuidance.cs`): coast shield-forward, trim the along/cross-track miss to the target, land on
  chutes at the **real Dragon altitudes** (drogues 5486 m, mains 1830 m). Range control via bank angle
  is present, not absent.
- **Verdict: 🟡** — real method is implemented; needs flight validation for Crew-2 splashdown accuracy.
  Control: FULL.

---

## Bottom line (corrected after auditing the code)

**"Do you have absolute control of the vessels at all times?"** — Ascent, insertion, Dragon sep,
docking, AND the targeted de-orbit + lifting entry: **yes** (the last needs a validation flight). The
one genuine control 🔴 is **booster droneship landing** (horizontal velocity not arrested → it splashed
at 230 m/s; ignition-altitude fix applied this session, but the ASDS-targeting descent needs flights).

**"Does it match the real Crew-2 methods/math?"**
- Genuinely real: launch-by-capability, gravity turn + **UPFG optimal insertion**, RealFuels
  ullage/ignition, Hohmann altitude match, keep-out-sphere docking, real chute altitudes.
- Real-but-not-enabled: the R-bar/V-bar **L-approach** (built, gated off).
- Not yet the real technique: the **rendezvous named-burn sequence** (NC/NH/NCC/coelliptic/TI), the
  **droneship boostback/entry** timing, the **targeted de-orbit + lifting entry** to a splashdown site.

**RO-rules adherence:** ✅ ascent/insertion (TEATEB, ullage now tracked live, Cooled fuel, FAR, TestFlight).
🔴 booster **entry burn ignites at high dynamic pressure** — an RO reliability rule we currently break.

## Priority order to close the gaps (proposed)
1. **Booster recovery** — light the entry burn high (thin air), reserve ascent propellant, so the ASDS
   landing works and stops failing RO's high-Q ignition rule.
2. **Targeted de-orbit + lifting entry** — size the de-orbit for a splashdown point and add bank-angle
   entry steering (the real EDL), so "de-orbit now" actually lands.
3. **Real rendezvous named-burn sequence** + enable/validate the **L-approach** — replace the ad-hoc
   ladder with NC/NH/NCC/coelliptic/TI and fly the R-bar/V-bar in.
