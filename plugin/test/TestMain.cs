/*
 * Headless test runner. Every suite returns a failure count; the process exit code is non-zero if any
 * of them failed, so build.py stops rather than cheerfully reporting "ok" over a broken build.
 *
 * PART B RECOVERY IS UNDER WAY (§B12.8). The autopilot deleted 2026-09-01 comes back in four
 * dependency-ordered waves, and each wave RE-REGISTERS the suites that prove it, below. Wave A (W1)
 * is the collision-free pure support layer; Waves B-D follow. A suite is registered here only once
 * the module it proves is actually in the tree - never ahead of it.
 */
using System;

public static class TestMain
{
    public static int Main()
    {
        // The SCREEN + shared-display-math suites. The autopilot suites removed on 2026-09-01 return
        // wave by wave underneath them (§B12.8); the ones still missing are the ones whose modules are.
        int bad = 0;
        bad += LayoutTest.Run();
        bad += LayoutSweepTest.Run();
        bad += PageTest.Run();
        bad += ComponentsTest.Run();       // Phase 6: pure display widgets (NumericReadout/StatusIndicator/TargetReticle)
        bad += PanelTest.Run();
        bad += GlobeProjectionTest.Run();  // screens: orthographic globe projection + occlusion (NAV 3D)
        bad += PlanetGeomTest.Run();       // screens: scaled-space camera framing/projection/occlusion (S10a)
        bad += OrbitalTest.Run();          // shared display math: orbit readouts
        bad += VehiclePartsTest.Run();     // screens: part classification for the systems display
        bad += MissionPhaseTest.Run();     // shared: the phase enum the screens label
        bad += StageStatsTest.Run();       // display: per-stage dV/TWR/burn-time readout (KER-mirrored)
        bad += KerDataTest.Run();          // KER soft-integration: per-stage selection over the mirrored KER sim data
        bad += FigmaUINavTest.Run();       // new Figma UI: bottom-bar nav + back chevron hit routing
        bad += TurntableTest.Run();        // screens: the capsule sprite turntable — naming, picker, drag (T11a, §5)
        bad += TouchWiringTest.Run();      // screens: the touch pass (T14) - chute actions, docking clusters, suit fail branch
        bad += LogGateTest.Run();          // diagnostics: the seen-set that stops a standing warning flooding KSP.log (S40)

        // ---- PART B RECOVERY, WAVE A (W1, §B12.8) - the collision-free pure support layer ----
        // Recovered from `8b81816^` with their modules. The fixtures are as they were: ConicTest and
        // LambertTest are RSS (mu = 3.986e14); TrajectoryTest and PredictTest are a STOCK Kerbin fixture
        // DELIBERATELY - they prove the integrator's ARITHMETIC against closed forms, and prove nothing
        // about RSS-RO tuning (R1 §3.5). Do not "fix" a fixture into RSS thinking it validates more.
        bad += AeroTest.Run();             // L1 derived aero: q, speed of sound, Mach, isothermal density
        bad += AuthorityTest.Run();        // L1 the vehicle's own control authority (torque / MOI)
        bad += ConicTest.Run();            // L3 support: Vec3 + universal-variable conic propagation
        bad += TrajectoryTest.Run();       // §B16 prediction engine: RK4 through-atmosphere, drag MEASURED
        bad += PredictTest.Run();          // where we will be / hit / pass closest - damped fixed point
        bad += LambertTest.Run();          // B7 Lambert two-point BVP, self-inverted against our propagator
        bad += RendezvousMathTest.Run();   // L3 rendezvous: the LVLH frame + Clohessy-Wiltshire targeting
        // S63's guard on the one irreplaceable RSS-RO dataset in the tree. `pure/BoosterDrag.cs` had no
        // suite and NEVER did (a grep of the whole pre-deletion tree at `8b81816^` finds its name in two
        // places: the file, and a prose sentence in `Aero.cs`). Its ten Mach-binned bc values came from
        // 18,080 samples over 48 RSS/RO flights whose raw CSVs were GITIGNORED AND NEVER COMMITTED
        // (R1 §3.5/§4.3, §B16.8 ruling 1) - so a changed digit could not be detected OR re-derived here,
        // and would surface as a landing miss rather than a red build. The suite transcribes the numbers
        // from R1 §3.5's verbatim quotation (a second surviving copy in the repo) rather than reading them
        // back out of the module, which is what makes it a guard and not a tautology.
        // ⚠ It pins the TABLE and the interpolator's SHAPE. It does not validate the curve - R1 is a
        // quotation of the same lost corpus, not an independent measurement - and R1 §3.5 records that the
        // data came from flights that mostly did NOT land, with no after-case for the miss it fixed. Only a
        // recorded RE-FLIGHT converges this (owner decision on R1 Q2), which needs glass time: an owner gate.
        bad += BoosterDragTest.Run();      // S63: the corpus bc-vs-Mach curve, pinned against R1 §3.5

        // ---- PART B RECOVERY, WAVE B (W2, §B12.8) - the actuation layer (§B12.7 direct part control) ----
        // ActuationTest proves the pure capability->role classifier the restored glue `src/Actuator.cs` acts
        // on, and carries W2's added §B16.4 HARD ASSERTION - read against the REAL `docs/reference/craftdump.csv`
        // on disk, so a wrong-vehicle bind (the Kartoffelkuchen Falcon 9, installed 2026-09-03) is caught
        // headless. ⚠ A MISSING DUMP FAILS this suite deliberately: the assertion is worthless without one.
        // ThrustBalanceTest proves the B3 balancer trio (ThrustBalance + DiffThrottle + RcsBalance), which
        // came back in this wave because `Actuator.BalanceOctawebThrust` / `RcsInducedTorque` will not compile
        // without them (R1 §3.1: "both should be recovered *with* the Actuator").
        // ⚠ Their CONSTANTS are UN-CONVERGED and UNATTRIBUTED (R1 §7.4) and engine-out was NEVER FLOWN
        // (R1 §5.1) - the suites prove the solver's ARITHMETIC, never that any of it is tuned. Each file
        // carries that marking in its own header; do not read a green suite as a validated number.
        bad += ActuationTest.Run();        // §B12.7 capability->role map + §B16.4's octaweb binding assertion
        bad += ThrustBalanceTest.Run();    // B3 TCA torque-nulling solver + its engine-out / RCS wrappers

        // ---- PART B RECOVERY, WAVE C (W3, §B12.8) - the booster set (§B16) ----
        // BoosterTest proves the three restored booster modules: the hoverslam ignition solver
        // (pure/Hoverslam.cs), the grid-fin steering law (pure/GridFin.cs) and the recovery FSM
        // (pure/BoosterDescent.cs). OctawebResolveTest proves W3's octaweb BINDER (pure/OctawebResolve.cs)
        // - guard first, then bind the three ModuleEnginesRF BY engineID into a named table, resolved
        // ONCE - and, like ActuationTest, reads the REAL `docs/reference/craftdump.csv` off disk, so a
        // MISSING DUMP FAILS IT DELIBERATELY.
        // ⚠ THE BOOSTER WAS NEVER RECOVERED IN FLIGHT (R1 §4.2) and every constant these suites touch is
        // UN-CONVERGED for RSS-RO with its regime recorded NOWHERE (R1 §7.4, §B16.8). BoosterTest's
        // fixture IS that defect - it carries the wave's only anchors, and they disagree with the only
        // other written set. These are PROPERTY checks: monotonicity, sign, unit-length, the AoA cap, the
        // FSM contract. Green here means the ARITHMETIC is right. It means NOTHING about tuning, and the
        // FSM under test is four phases where §B16.2 specifies five (no boostback state). Each file says
        // so in its own header; read one before trusting a number that came through it.
        bad += BoosterTest.Run();          // §B16 booster: hoverslam solver + grid-fin steering + the recovery FSM
        bad += OctawebResolveTest.Run();   // §B16.4 step 2: the octaweb binder, guard-first, against the real dump

        // ---- PART B RECOVERY, W23 (§B16) - the booster HOST: the thing that RUNS the script ----
        // W8 built the five-phase script and recorded that NOTHING CALLED IT. W23 built the caller:
        // pure/BoosterHostPlan.cs (the decisions) + src/BoosterHost.cs (the KSP glue). This suite proves
        // the decision half - WHICH VESSEL (and above all which NOT), WHEN TO STOP, WHETHER A COMMAND MAY
        // GO OUT, and WHICH ENGINE SET a command names. Its sharpest checks are NEGATIVE: the DRAGON is
        // exercised as a candidate from every angle and must never be selected, with each of the three
        // independent exclusions checked ALONE.
        // ⚠ NOTHING FLIES. `BoosterHost.Actuate` is FALSE by default - there is no steering law (register
        // W24), and a booster that lights an engine with an uncontrolled attitude is flight 194334
        // (`8225df7`: "fires thr=1.0 0.3 s after MECO at 'sep 0 km' ... LOST in ~10 s - and its 0-km burn
        // kicks the upper stage"). The suite pins that default. The two hold-off constants it exercises
        // are [UN-CONVERGED] (§B16.8): 194334 gives the FAILING point, never a converged safe value.
        bad += BoosterHostTest.Run();      // §B16 booster host: selection, stop, command gate, engine roles

        // ---- PART B RECOVERY, W24 (§B16) - the booster STEERING LAW -------------------------------
        // `docs/BOOSTER_STEERING_MOD_SEARCH.md` (C1.15) could neither rule TCA in nor out; the owner ruled
        // (via the overseer, 2026-09-04): OURS, TCA's METHOD borrowed, no dependency (Q1), and a marked,
        // [UN-CONVERGED], DEFAULT-ZERO deadband seam (Q2). `pure/BoosterSteer.cs` is that law. It is
        // written against the ACTUAL failure `docs/FLIGHT_CORPUS_ASSESSMENT.md` §3 found — a DIVERGENCE
        // (an unbounded commanded rate), not the limit cycle the inherited folklore blamed — by making the
        // outer angle-to-rate stage structurally incapable of demanding more than a fixed ceiling.
        // ⚠ NO BYTE of `AttitudePilot.cs`/`AttitudeController.cs`/`pure/AttitudeLoop.cs` is here (R1 §3.2:
        // RECOVER-REFERENCE ONLY, owner directive) — only the documented frame-conversion FORMULA is
        // reused, in the glue (`src/BoosterHost.cs`), per R1's own list of what those files are reference
        // FOR. Every gain is [UN-CONVERGED] (§B16.8 ruling 2) and the per-axis SIGN is UNVERIFIED — this
        // law has no recorded flight of its own. `BoosterHost.Actuate` flips to TRUE with this task, per
        // the owner's ruling on W23's Q1: the next flight is the first time this commands a real vessel.
        bad += BoosterSteerTest.Run();     // W24: the steering law - rate ceiling, deadband seam, bounds

        // ---- PART B RECOVERY, W6 (§B16, R1-tagged but in NO §B12.8 wave) - the B8 impact divert ----
        // pure/CourseCorrect.cs is the layer between the two above: it turns a predicted-impact ERROR
        // (BoosterDescent.ErrorTo, over pure/Trajectory.cs) into the control change that nulls it - a 2x2
        // finite-difference Jacobian for the booster's down/cross grid-fin steer, and a 1x1 Newton step for
        // the capsule entry range channel. R1 §5.1 gives it RECOVER-CODE - §B16 but no wave named it, so it
        // is its own register line (W6) rather than a quiet passenger in someone else's diff.
        // ⚠ NEVER FLOWN (R1 §5.1: "❌ NO"; e90a63f: "no lifting-entry flight in the corpus"). Its three
        // constants are UN-CONVERGED for RSS-RO (§B16.8 ruling 2) and this suite's fixtures are ANALYTIC -
        // a known linear impact model, chosen because it has an exact closed-form divert. Green proves the
        // linear algebra recovers that answer, the damping leaves exactly its residual, and the solve
        // REFUSES rather than diverting on noise when the Jacobian is unobservable or rank-deficient. It
        // proves nothing about a tuned number. The file's header says so.
        bad += CourseCorrectTest.Run();    // B8 impact-point divert: the 2x2 Jacobian solve + the 1x1 Newton step

        // ---- PART B RECOVERY, WAVE E-3 (W15, §B12.8 rider (c)) - the safe-water splashdown selector ----
        // pure/SafeLandingSite.cs picks WHICH point on the sampled ground track a returning capsule aims at:
        // the nearest OPEN WATER inside the reachable entry-glide window, else -1 so the glue coasts a step.
        // Its glue half (`src/LandingSiteScan.cs`, restored with it) does the body sampling and carries the
        // F4 water gate - `TerrainAltitude(lat, lon, true)`, the three-arg overload that returns the real
        // negative seabed height; the default two-arg call CLAMPS ocean depth to 0, so an RSS scan reads
        // ZERO water and never commits. That fix is the reason the file exists (R1 §5.2: regime RSS).
        // ⚠ NOTHING CALLS EITHER FILE YET. Both intended callers are absent - `AbortControl` is W19 and
        // `ReturnControl` (W18) is now RECOVER-REFERENCE and lands no code - so the module is restored
        // DORMANT, for them to consume. This suite is the only thing exercising it.
        // ⚠ NEVER FLOWN (R1 §5.1/§5.2: flown ❌ NO). The glide window it selects against is [UN-CONVERGED]
        // for RSS-RO (§B16.8 ruling 2), marked on `src/LandingSiteScan.cs` which supplies the band; the pure
        // selector defines no constant at all. Three of the checks are RECOVERED VERBATIM from the deleted
        // `test/FdirTest.cs:152-164` (the rest of that suite stays deleted - it is `AbortResponder`/`Fdir`
        // coverage and neither type is in the tree). The fixtures are ANALYTIC. Green proves the selector
        // picks the right sample; it proves nothing about the window being the right window.
        bad += SafeLandingSiteTest.Run();  // W15: nearest safe water inside the reachable glide window

        // ---- PART B RECOVERY, WAVE D (W4, §B12.8) - the PURE conductor set ----
        // The mission-conductor decision layer: ModeManager (the mission plan + the phase sequencer),
        // CrewGate (the crew-in-the-loop GATE state machine), CrewGates (the real G1..G15 catalog),
        // MissionProfile (mission-as-data, resolved from the VAB craft name), WarpPlan (the never-overshoot
        // time-warp rule) and CoastEta (a range-closing coast's ETA, so a long chase can be warped).
        // CrewGateTest is the one suite that covers CrewGate + CrewGates + ModeManager together.
        // ⚠ NOTHING HERE FLIES ANYTHING. Wave D restored the PURE half only: the two GLUE files that would
        // call it (`src/CrewProcedureOps.cs`, `src/MissionConductor.cs`) are NOT in the tree - they need a
        // host (`FlightDriver`) and a booster core that no wave owns yet (register W9/W10). So these suites
        // prove DECISIONS, not a flown mission; every flight command on every screen is still §14.4(a)'s
        // honest no-op. CrewGates' gate TITLES and CHECKLIST ITEMS are §1.4 source-of-truth material
        // (transcribed NASA/SpaceX callouts) - do not edit one to make a test pass.
        bad += MissionProfileTest.Run();   // L-S0b mission-as-data: the 19-mission catalog + craft-name resolve
        bad += CrewGateTest.Run();         // L4 crew gate machine + the real gate catalog + the phase sequencer
        bad += WarpPlanTest.Run();         // conductor: the on-rails rate that can never overshoot the drop-out
        bad += CoastEtaTest.Run();         // conductor: range-closing coast ETA -> the warp target UT

        // ---- PART B0 (BB1, §B0) - the BlackBox flight recorder core ----
        // ⭐ THE ONE LINE THAT MUST BE REMOVED IF THE RECORDER IS EXCISED FOR RELEASE. BB1 is
        // excisable by design (owner, 2026-09-03): delete `src/pure/blackbox/`, `src/BlackBoxRecorder.cs`,
        // `test/BlackBoxTest.cs`, and this line. Nothing else in the tree names it - the dependency
        // arrow points one way, BlackBox -> tree, and never back.
        // ⚠ This suite proves the PURE half only: the schema, RFC-4180 + invariant formatting, §4.6's
        // blank-never-zero validity, §2.0's rate ladder and warp floor, the R0 accumulators, the warp
        // void, the manifest, and the ghost-column coverage check S76's finding demanded. It proves
        // NOTHING about the glue reading the right KSP field into the right column - that is
        // `src/BlackBoxRecorder.cs`, it needs a Vessel, and it is confirmed on the glass by **BB4**.
        bad += BlackBoxTest.Run();       // BB1: recorder core - schema, validity, rates, manifest, coverage
        // ⚠ S85's suite is NOT part of the excision above. `pure/CrewControlIds.cs`, `pure/CrewPressLog.cs`
        // and this line stay when the recorder goes: the press buffer is SCREEN-side, the two choke
        // points write into it, and it is the reason `ScreenPainter.cs`/`PanelButtons.cs` still compile
        // with `pure/blackbox/` and `BlackBoxRecorder.cs` deleted. Verified by physical removal (S85).
        bad += CrewPressTest.Run();      // S85: the CVR press channel - the control_id namespace + buffer

        Console.WriteLine(bad == 0 ? "ALL SUITES PASSED" : bad + " SUITE(S) FAILED");
        return bad == 0 ? 0 : 1;
    }
}
