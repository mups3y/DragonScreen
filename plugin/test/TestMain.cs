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
        // ⛔ THE AUTOPILOT WAS DELETED (2026-09-01, owner directive: keep ONLY the Dragon screens / UI).
        // Only the SCREEN + shared-display-math suites remain. The autopilot suites (control / guidance /
        // rendezvous / docking / booster / FDIR / self-cal) were removed with the code they tested.
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

        Console.WriteLine(bad == 0 ? "ALL SCREEN SUITES PASSED" : bad + " SUITE(S) FAILED");
        return bad == 0 ? 0 : 1;
    }
}
