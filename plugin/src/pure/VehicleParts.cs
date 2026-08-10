/*
 * DragonScreen - VehicleParts
 *
 * PURE. The ONE place that knows how to recognise a part.
 *
 * ---- ⛔ MATCH `part.name`, NEVER THE RIGHT-CLICK TITLE ----
 * This file exists because of flight 17:34. I matched the booster on `"K1"`, which I had taken from
 * the PAW title "Ghidorah K1-180 Tank" seen in a screenshot. The actual `part.name` is
 * `TE.19.F9.S1.Tank`. The two are different strings and only one of them is in `part.name`.
 *
 * It matched nothing, silently, and the damage was not "the booster was not found":
 *
 *      HasBooster() was always FALSE
 *   -> SecondStage was always TRUE
 *   -> the FIRST stage flew the SECOND stage's pitch law, which is capped at MECOangle
 *   -> so at 400 m the vehicle slammed from 90 deg to 45 deg, in the thickest air of the ascent
 *   -> "very unstable flight"
 *
 * ...and separately, FindBooster never found a booster, so the recovery could never run.
 *
 * A wrong name string cannot fail loudly - it just quietly means "no". So the names live here, once,
 * taken from the craft file itself:
 *
 *      TE.19.F9.S1.Tank / .Engine / .Interstage      the booster
 *      TE.19.F9.S2.Tank / .Engine                    the second stage
 *      TE.18.DRAGONV2.POD / .TRUNK / .HEATSHIELD     the capsule
 *      TE.CD2.POD.DROGUES / .MAINS                   the chutes
 *      TE.19.C.Dragon.Decoupler                      S2 sep - NOT the trunk decoupler
 *
 * KSP appends an instance suffix (`TE.19.F9.S1.Tank_4293215820`), so these are CONTAINS tests, not
 * equality. That is also why they must be distinctive: `.S1.` and `.S2.` differ by one character and
 * getting them the wrong way round swaps the two stages.
 */
using System;

namespace DragonScreen
{
    public static class VehicleParts
    {
        /// <summary>Falcon 9 first stage. Tank, engine and interstage all carry it.</summary>
        public const string BoosterMarker = ".S1.";

        /// <summary>Falcon 9 second stage.</summary>
        public const string SecondStageMarker = ".S2.";

        /// <summary>The Dragon trunk - what FIRE PYRD drops.</summary>
        public const string TrunkMarker = "DRAGONV2.TRUNK";

        /// <summary>The capsule itself.</summary>
        public const string PodMarker = "DRAGONV2.POD";

        public const string DroguesMarker = "POD.DROGUES";
        public const string MainsMarker = "POD.MAINS";

        // ---- ⛔ TWO DECOUPLERS, AND ONLY ONE OF THEM IS EVER THE RIGHT ONE ----
        // From Ghidorah 9 - Crew Rodan.craft, top to bottom:
        //     TE.18.DRAGONV2.TRUNK       ModuleTundraDecoupler  drops the TRUNK *and everything
        //                                                       below it*, S2 included
        //     TE.19.C.Dragon.Decoupler   ModuleDecouple         drops the S2 ONLY - capsule and
        //                                                       trunk stay
        //     TE.19.F9.S2.Tank
        // `falcon-dragon-two-decouplers` was written because a comment in dragon_deorbit.ks claims
        // the opposite. Firing the trunk decoupler to "drop the S2" takes the trunk with it, and the
        // trunk is what the solar panels and radiators are on.
        public const string DragonDecouplerMarker = "C.Dragon.Decoupler";

        // ---- GRID FINS ----
        // NOT a Tundra part. Four `Grid Fin M Titanium` from Kerbal Reusability Expansion on every
        // Ghidorah 9 craft, and this one really does have spaces in `part.name` (config line 3;
        // the TITLE is the different-looking `T-222 "Nemesis" Grid Fin Medium`).
        //
        // ⚠ F9I records getting this exact pair wrong: its FalconDetect filled the GridFins global
        // from `TE2.19.F9.CGT`, which is the cold-gas RCS thruster, not a fin. Fixed there
        // 2026-08-04. The two are unrelated parts that both sit on the booster.
        //
        // The animation name is preferred over the part name for the actual deploy, the same way
        // FlightCommands finds the nose cone: B9PartSwitch reorders modules and a part rename is
        // survivable, but the animation clip name is what the module is actually keyed on.
        public const string GridFinPart = "Grid Fin M Titanium";
        public const string GridFinAnimation = "NewFinsDeploy";

        // ---- THE OCTAWEB ----
        // All nine Merlins are ONE part carrying THREE mutually exclusive ModuleEnginesFX, selected
        // by a Tundra module. Engine IDs and thrusts from TE_19_F9_S1_Engine.cfg:
        //
        //      AllEngines    2560 kN   mode 0   primaryEngineID
        //      ThreeLanding  1706 kN   mode 1   secondaryEngineID
        //      CenterOnly     764 kN   mode 2   tertiaryEngineID
        //
        // ⚠ Those are NOT multiples of one engine: 284 / 569 / 764 kN "per engine". Any code that
        // scales thrust linearly by an engine COUNT is wrong on this vehicle.
        public const string EngineSwitchModule = "ModuleTundraEngineSwitch";
        public const string EngineSwitchAction = "next engine mode";
        public const string EngineIdThree = "Three";
        public const string EngineIdCentre = "Center";

        /// <summary>Ghidorah NINE. The mode switch presents nine engines as one module.</summary>
        public const int OctawebEngineCount = 9;

        /// <summary>Octaweb modes, in the order the part's one-way "next engine mode" cycles them.</summary>
        public const int ModeAllEngines = 0, ModeThreeEngine = 1, ModeCentreOnly = 2;

        /// <summary>Which octaweb mode flies a given engine count.</summary>
        public static int OctawebModeFor(int engines)
        {
            if (engines <= 1) return ModeCentreOnly;
            if (engines <= 3) return ModeThreeEngine;
            return ModeAllEngines;
        }

        /// <summary>Does this engineID belong to the given octaweb mode?</summary>
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

        /// <summary>
        /// Is this part of the first stage?
        ///
        /// ⚠ Checked against the SECOND-stage marker too, and rejected if it matches: the two
        /// strings are one character apart and a part that somehow carried both would otherwise be
        /// counted as a booster and hold the ascent in first-stage guidance forever.
        /// </summary>
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

        public static bool IsTrunk(string partName) { return Has(partName, TrunkMarker); }
        public static bool IsDragonDecoupler(string partName)
        {
            return Has(partName, DragonDecouplerMarker);
        }
        public static bool IsPod(string partName) { return Has(partName, PodMarker); }
        public static bool IsDrogues(string partName) { return Has(partName, DroguesMarker); }
        public static bool IsMains(string partName) { return Has(partName, MainsMarker); }
    }
}
