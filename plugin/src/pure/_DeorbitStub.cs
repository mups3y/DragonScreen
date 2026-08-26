// DragonScreen — TEMPORARY autopilot stub (pure side)
// ============================================================================================
// The autopilot was DELETED for a ground-up rebuild (docs/AUTOPILOT_REBUILD_PLAN.md, 2026-08-26).
// This provides the idle stand-ins the KEPT SCREEN code still references, so the screens compile
// while the autopilot is rebuilt from scratch. DELETE each member here as the real class returns.
// ============================================================================================
namespace DragonScreen
{
    // Terminal.cs (a screen readout) asks the guidance for a landing throttle. Idle until rebuilt.
    public static class Deorbit
    {
        public static double LandingThrottle(double trueRadarM, double stopDistM) { return 0.0; }
    }
}
