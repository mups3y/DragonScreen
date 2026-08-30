/*
 * Headless test runner. Every suite returns a failure count; the process exit code is non-zero if any
 * of them failed, so build.py stops rather than cheerfully reporting "ok" over a broken build.
 *
 * ⛔ THE AUTOPILOT WAS DELETED FOR A GROUND-UP REBUILD (docs/AUTOPILOT_REBUILD_PLAN.md, 2026-08-26).
 * Only the SCREEN suites remain here. As each rebuilt autopilot layer lands, re-register its (verified)
 * test — the L0 math suites (Kepler/UPFG/CW/Hohmann/Hoverslam/FuelFlow) are in _deleted_autopilot/test/,
 * ready to return with their layer.
 */
using System;

public static class TestMain
{
    public static int Main()
    {
        int bad = 0;
        bad += LayoutTest.Run();
        bad += LayoutSweepTest.Run();
        bad += PageTest.Run();
        bad += ComponentsTest.Run();       // Phase 6: pure display widgets (NumericReadout/StatusIndicator/TargetReticle)
        bad += PanelTest.Run();
        bad += GlobeProjectionTest.Run();  // screens: orthographic globe projection + occlusion (NAV 3D)
        bad += OrbitalTest.Run();
        bad += VehiclePartsTest.Run();
        bad += ActuationTest.Run();        // glue: direct-control role classifier (Actuator ← pure/Actuation)
        bad += AttitudeLoopTest.Run();     // L2 control: MechJeb BetterController gimbal loop (AttitudePilot ← pure)
        bad += IgnitionGateTest.Run();     // glue gates: clamp release (≥99% thrust) + ullage settle (≥0.996)
        bad += MissionProfileTest.Run();   // rebuilt autopilot: S0b mission-as-data resolver
        bad += MissionPhaseTest.Run();     // Phase 3: authoritative-phase resolver (FSM beats the classifier shadow)
        bad += TrajectoryTest.Run();       // L1 nav: RK4 impact predictor + measured drag/lift/beta
        bad += PredictTest.Run();          // L1 nav: impact / closest-approach helpers
        bad += AeroTest.Run();             // L1 nav: dynamic pressure / Mach / sound speed
        bad += AuthorityTest.Run();        // L1 nav: per-axis control authority + arrestable rate
        bad += AuthorityManagerTest.Run(); // Phase 2: control-authority arbitration (Auto/Manual/Recovery/Abort, per-vehicle)
        bad += ControlTest.Run();          // L2 control: attitude PD + throttle bucket/g-limit + RCS
        bad += RcsPulseTest.Run();         // Tier-2: PWPF/delta-sigma RCS pulse modulation (Draco chatter fix)
        bad += ConicTest.Run();            // L3 support: Vec3 + conic propagator (UPFG gravity)
        bad += AscentTest.Run();           // L3 ascent: launch azimuth + S1 pitch program + FSM
        bad += LaunchWindowTest.Run();     // L3 ascent: launch-to-rendezvous plane-crossing / RAAN window
        bad += UpfgTest.Run();             // L3 ascent: closed-loop UPFG S2 insertion (PEGAS port)
        bad += StageStatsTest.Run();       // B1: per-stage dV/TWR/burn-time + MECO recovery reserve (MechJeb FuelFlowSim math)
        bad += QAlphaTest.Run();           // B2: q·α moderation controllability cap + SelfCal aero-stiffness estimator
        bad += ThrustBalanceTest.Run();    // B3: thrust-limiter balancing solver (TCA torque-nulling descent)
        bad += ActuatorLagTest.Run();      // B4: first-order actuator-lag model + lead compensation
        bad += NavFilterTest.Run();        // B6: strict-fidelity nav filter (per-axis 3-state Kalman: pos/vel/bias)
        bad += BoosterTest.Run();          // L3 booster: hoverslam + grid-fin steering + descent FSM
        bad += RendezvousMathTest.Run();   // L3 rendezvous: LVLH + CW two-impulse + Hohmann
        bad += LambertTest.Run();          // B7: universal-variable Lambert solver + maneuver/finite-burn library
        bad += RvInterceptTest.Run();      // Lambert rendezvous planner: tof scan + pe-floor gate + cost cap (intercept)
        bad += CourseCorrectTest.Run();    // B8: finite-difference impact-point divert solve (2×2 booster / 1×1 entry)
        bad += LaunchTunerTest.Run();      // B9: ascent Δv-loss decomposition + GravityTurn LaunchDB shape auto-tuner
        bad += WarpPlanTest.Run();         // mission-conductor: safe time-warp decisions (never overshoot a burn)
        bad += CoastEtaTest.Run();         // mission-conductor: coast-length ETA for warp-to-maneuvers (range close)
        bad += KerDataTest.Run();          // KER soft-integration: per-stage selection over the mirrored KER sim data
        bad += RendezvousTest.Run();       // L3 rendezvous: named-burn FSM + full-control contract
        bad += PhasingTest.Run();          // L3 rendezvous: far-field co-elliptic raise + crew-safety pe floor + CW guard
        bad += DockingTest.Run();          // L3 docking: glideslope servo + L-approach FSM
        bad += DockCorridorTest.Run();     // L3 docking: approach-corridor / KOS-breach geometry (auto-abort)
        bad += DockCaptureTest.Run();      // L3 docking: IDSS soft-capture envelope gate (IDD Rev E)
        bad += ReturnTest.Run();           // L3 return: departure + deorbit + lifting entry (CoM shifter) + chutes
        bad += CrewGateTest.Run();         // L4 conductor: crew-gate state machine + gate catalog + mode manager
        bad += FdirTest.Run();             // L5 FDIR: debounced monitors + recovery ladder + phase-correct abort
        bad += FdirFeedsTest.Run();        // L5 FDIR: honest feed-shaping (thrust frac / tumble / closing progress) — T2b
        bad += FdirAlertTest.Run();        // Phase 4: FDIR fault spine → crew alert channel (severity + fault name)
        bad += SelfCalTest.Run();          // L6 self-cal: RLS w/ variable forgetting + the live-estimate bank
        bad += FlightRecorderTest.Run();   // L7 instrumentation: the per-flight CSV schema + fillers
        bad += DispersionTest.Run();       // Tier-2 robustness: property-based dispersion of the pure layer

        Console.WriteLine(bad == 0 ? "ALL SCREEN SUITES PASSED" : bad + " SUITE(S) FAILED");
        return bad == 0 ? 0 : 1;
    }
}
