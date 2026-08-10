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
        public static bool IsPod(string partName) { return Has(partName, PodMarker); }
        public static bool IsDrogues(string partName) { return Has(partName, DroguesMarker); }
        public static bool IsMains(string partName) { return Has(partName, MainsMarker); }
    }
}
