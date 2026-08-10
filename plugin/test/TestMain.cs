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
        bad += DockControlTest.Run();

        Console.WriteLine(bad == 0 ? "ALL SUITES PASSED" : bad + " SUITE(S) FAILED");
        return bad == 0 ? 0 : 1;
    }
}
