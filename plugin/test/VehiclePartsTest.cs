/*
 * Tests for the octaweb engine-mode logic (pure/VehicleParts.cs).
 *
 * REGRESSION: flight_0822_201219. `IgniteFirstStage` activated EVERY ModuleEngines on the octaweb -
 * all three modes (AllEngines / ThreeLanding / CenterOnly) - so the descent engines fired, plumed and
 * produced heat during ascent, and the stacked heatProduction cooked the S1 tank (300 -> 644 K while
 * dynamic pressure fell) until it exploded at boostback. The launch ignition must light ONLY the
 * all-engines mode; these checks pin `EngineIdIsMode`, the gate that now enforces that, against the
 * real Tundra RO engine IDs.
 */
using DragonScreen;
using System;

public static class VehiclePartsTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen octaweb engine-mode tests");

        // Real Tundra RO engine IDs (RO_TE_Falcon_9.cfg): AllEngines / ThreeLanding / CenterOnly.
        const string ALL = "AllEngines", THREE = "ThreeLanding", CENTRE = "CenterOnly";

        // ---- THE LAUNCH-IGNITION GATE: only AllEngines lights at ignition. ----
        Check("launch lights the all-engines module",
              VehicleParts.EngineIdIsMode(ALL, VehicleParts.ModeAllEngines), "");
        Check("launch does NOT light the three-engine (entry) module  [the bug]",
              !VehicleParts.EngineIdIsMode(THREE, VehicleParts.ModeAllEngines), "");
        Check("launch does NOT light the centre-engine (landing) module  [the bug]",
              !VehicleParts.EngineIdIsMode(CENTRE, VehicleParts.ModeAllEngines), "");

        // ---- individual-Merlin boosters (no octaweb, plain/empty engineID) STILL light at launch. ----
        Check("a plain engine id counts as all-engines (individual-engine boosters still light)",
              VehicleParts.EngineIdIsMode("Merlin1D", VehicleParts.ModeAllEngines), "");
        Check("an empty engine id counts as all-engines",
              VehicleParts.EngineIdIsMode("", VehicleParts.ModeAllEngines), "");
        Check("a null engine id does not throw and counts as all-engines",
              VehicleParts.EngineIdIsMode(null, VehicleParts.ModeAllEngines), "");

        // ---- the descent modes still match THEIR OWN mode, so BoosterRecovery can light them later. ----
        Check("three-engine module belongs to the three-engine mode",
              VehicleParts.EngineIdIsMode(THREE, VehicleParts.ModeThreeEngine), "");
        Check("centre-engine module belongs to the centre-only mode",
              VehicleParts.EngineIdIsMode(CENTRE, VehicleParts.ModeCentreOnly), "");
        Check("the all-engines module does NOT belong to the three-engine mode",
              !VehicleParts.EngineIdIsMode(ALL, VehicleParts.ModeThreeEngine), "");
        Check("three module is not the centre mode (Three and Center are distinct)",
              !VehicleParts.EngineIdIsMode(THREE, VehicleParts.ModeCentreOnly), "");

        // ---- the engine-count -> mode map the recovery uses. ----
        Check("9 engines -> all mode", VehicleParts.OctawebModeFor(9) == VehicleParts.ModeAllEngines, "");
        Check("3 engines -> three mode", VehicleParts.OctawebModeFor(3) == VehicleParts.ModeThreeEngine, "");
        Check("1 engine  -> centre mode", VehicleParts.OctawebModeFor(1) == VehicleParts.ModeCentreOnly, "");

        // ---- OCT3: "off" is its OWN value, never confusable with the ascent-only all-engines mode. ----
        Check("ModeOff is distinct from every real octaweb mode",
              VehicleParts.ModeOff != VehicleParts.ModeAllEngines
              && VehicleParts.ModeOff != VehicleParts.ModeThreeEngine
              && VehicleParts.ModeOff != VehicleParts.ModeCentreOnly, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
