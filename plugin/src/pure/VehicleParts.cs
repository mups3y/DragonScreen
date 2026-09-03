// DragonScreen - VehicleParts
// ---- ⛔ MATCH `part.name`, NEVER THE RIGHT-CLICK TITLE ----
using System;

namespace DragonScreen
{
    public static class VehicleParts
    {
        public const string BoosterMarker = ".S1.";

        public const string InterstageMarker = "S1.Interstage";

        public const string SecondStageMarker = ".S2.";

        public const string TrunkMarker = "DRAGONV2.TRUNK";

        public const string PodMarker = "DRAGONV2.POD";

        public const string DroguesMarker = "POD.DROGUES";
        public const string MainsMarker = "POD.MAINS";

        public const string HeatShieldMarker = "DRAGONV2.HEATSHIELD";

        // ---- ⛔ TWO DECOUPLERS, AND ONLY ONE OF THEM IS EVER THE RIGHT ONE ----
        public const string DragonDecouplerMarker = "C.Dragon.Decoupler";

        // ---- GRID FINS ----
        public const string GridFinPart = "Grid Fin M Titanium";
        public const string GridFinAnimation = "NewFinsDeploy";

        // ---- THE OCTAWEB ----
        public const string EngineSwitchModule = "ModuleTundraEngineSwitch";
        public const string EngineSwitchAction = "next engine mode";
        public const string EngineIdThree = "Three";
        public const string EngineIdCentre = "Center";

        // ⚠ NINE NOZZLES, NOT NINE PARTS (§B16.4; rider on W2, owner-confirmed G5a-Q3 option 2).
        // This is the vehicle's engine COUNT and must NEVER be read as an expected PART count. The real craft
        // (docs/reference/craftdump.csv) carries ONE octaweb part, TE.19.F9.S1.Engine, holding THREE
        // ModuleEnginesRF distinguished by engineID (AllEngines=9 / ThreeLanding=3 / CenterOnly=1). The old
        // "expect OctawebEngineCount = 9 engine parts, identify the centre by position" procedure was WRONG
        // for this craft and is deleted; bind BY THE engineID STRING and nothing else (§B16.4, OctawebBinding).
        public const int OctawebEngineCount = 9;

        public const int ModeAllEngines = 0, ModeThreeEngine = 1, ModeCentreOnly = 2;

        public static int OctawebModeFor(int engines)
        {
            if (engines <= 1) return ModeCentreOnly;
            if (engines <= 3) return ModeThreeEngine;
            return ModeAllEngines;
        }

        public static bool EngineIdIsMode(string engineId, int mode)
        {
            bool centre = Has(engineId, EngineIdCentre);
            bool three = Has(engineId, EngineIdThree);
            if (mode == ModeCentreOnly) return centre;
            if (mode == ModeThreeEngine) return three;
            return !centre && !three;
        }

        private static bool Has(string partName, string marker)
        {
            return partName != null
                && partName.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsBooster(string partName)
        {
            if (Has(partName, SecondStageMarker)) return false;
            return Has(partName, BoosterMarker);
        }

        public static bool IsSecondStage(string partName)
        {
            if (Has(partName, BoosterMarker)) return false;
            return Has(partName, SecondStageMarker);
        }

        public static bool IsInterstage(string partName) { return Has(partName, InterstageMarker); }

        public const string DroneshipMarker = "Droneship";
        public static bool IsDroneship(string partName) { return Has(partName, DroneshipMarker); }

        public const string ErectorMarker = "Erector";
        public static bool IsErector(string partName) { return Has(partName, ErectorMarker); }

        public static bool IsTrunk(string partName) { return Has(partName, TrunkMarker); }
        public static bool IsDragonDecoupler(string partName)
        {
            return Has(partName, DragonDecouplerMarker);
        }
        public static bool IsPod(string partName) { return Has(partName, PodMarker); }
        public static bool IsDrogues(string partName) { return Has(partName, DroguesMarker); }
        public static bool IsMains(string partName) { return Has(partName, MainsMarker); }
        public static bool IsHeatShield(string partName) { return Has(partName, HeatShieldMarker); }
    }
}
