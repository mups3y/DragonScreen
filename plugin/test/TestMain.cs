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

        Console.WriteLine(bad == 0 ? "ALL SUITES PASSED" : bad + " SUITE(S) FAILED");
        return bad == 0 ? 0 : 1;
    }
}
