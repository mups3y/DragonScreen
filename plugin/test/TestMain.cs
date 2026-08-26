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
        bad += PanelTest.Run();
        bad += OrbitalTest.Run();
        bad += VehiclePartsTest.Run();
        bad += ActuationTest.Run();        // glue: direct-control role classifier (Actuator ← pure/Actuation)
        bad += MissionProfileTest.Run();   // rebuilt autopilot: S0b mission-as-data resolver
        bad += TrajectoryTest.Run();       // L1 nav: RK4 impact predictor + measured drag/lift/beta
        bad += PredictTest.Run();          // L1 nav: impact / closest-approach helpers
        bad += AeroTest.Run();             // L1 nav: dynamic pressure / Mach / sound speed
        bad += AuthorityTest.Run();        // L1 nav: per-axis control authority + arrestable rate
        bad += ControlTest.Run();          // L2 control: attitude PD + throttle bucket/g-limit + RCS
        bad += ConicTest.Run();            // L3 support: Vec3 + conic propagator (UPFG gravity)
        bad += AscentTest.Run();           // L3 ascent: launch azimuth + S1 pitch program + FSM
        bad += UpfgTest.Run();             // L3 ascent: closed-loop UPFG S2 insertion (PEGAS port)
        bad += BoosterTest.Run();          // L3 booster: hoverslam + grid-fin steering + descent FSM
        bad += RendezvousMathTest.Run();   // L3 rendezvous: LVLH + CW two-impulse + Hohmann
        bad += RendezvousTest.Run();       // L3 rendezvous: named-burn FSM + full-control contract
        bad += DockingTest.Run();          // L3 docking: glideslope servo + L-approach FSM
        bad += ReturnTest.Run();           // L3 return: departure + deorbit + lifting entry (CoM shifter) + chutes
        bad += CrewGateTest.Run();         // L4 conductor: crew-gate state machine + gate catalog + mode manager
        bad += FdirTest.Run();             // L5 FDIR: debounced monitors + recovery ladder + phase-correct abort
        bad += SelfCalTest.Run();          // L6 self-cal: RLS w/ variable forgetting + the live-estimate bank
        bad += FlightRecorderTest.Run();   // L7 instrumentation: the per-flight CSV schema + fillers

        Console.WriteLine(bad == 0 ? "ALL SCREEN SUITES PASSED" : bad + " SUITE(S) FAILED");
        return bad == 0 ? 0 : 1;
    }
}
