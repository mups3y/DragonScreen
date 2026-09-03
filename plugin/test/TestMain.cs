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

        Console.WriteLine(bad == 0 ? "ALL SUITES PASSED" : bad + " SUITE(S) FAILED");
        return bad == 0 ? 0 : 1;
    }
}
