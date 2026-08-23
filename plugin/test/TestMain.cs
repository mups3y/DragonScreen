/*
 * Headless test runner. Every suite returns a failure count; the process exit code is non-zero if any
 * of them failed, so build.py stops rather than cheerfully reporting "ok" over a broken build.
 */
using System;

public static class TestMain
{
    public static int Main()
    {
        int bad = 0;
        bad += LayoutTest.Run();
        bad += PageTest.Run();
        bad += PanelTest.Run();
        bad += FlightTest.Run();
        bad += OrbitalTest.Run();
        bad += PredictTest.Run();
        bad += BurnExecTest.Run();
        bad += ReturnBudgetTest.Run();
        bad += PhasingTest.Run();
        bad += DockGeometryTest.Run();
        bad += DeorbitBurnTest.Run();
        bad += DeorbitPointTest.Run();
        bad += TrajectoryTest.Run();
        bad += EntryGuidanceTest.Run();
        bad += DockControlTest.Run();
        bad += DockApproachTest.Run_();
        bad += ReturnPathTest.Run();
        bad += LayoutSweepTest.Run();
        bad += MechJebLibTest.Run();
        bad += FuelFlowTest.Run();
        bad += LaunchAzimuthTest.Run();
        bad += WaypointApproachTest.Run();
        bad += LvlhTest.Run();
        bad += VehiclePartsTest.Run();
        bad += HohmannTest.Run();
        bad += PlaneWindowTest.Run();
        bad += Crew2TimelineTest.Run();
        bad += KeplerTest.Run();
        bad += UpfgTest.Run();

        Console.WriteLine(bad == 0 ? "ALL SUITES PASSED" : bad + " SUITE(S) FAILED");
        return bad == 0 ? 0 : 1;
    }
}
