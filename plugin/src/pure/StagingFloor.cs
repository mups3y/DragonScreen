// DragonScreen — THE STAGING FLOOR  (register S228 / NTSB-2026-002 R-03)
// ============================================================================================
// PURE. ⭐ **DEFENCE IN DEPTH. THIS FILE ASSUMES JOBS 1 AND 2 ARE WRONG.**
//
// ---- WHAT HAPPENED, BECAUSE THE NUMBER BELOW IS NOT AN OPINION ----
// `MechJebCore.FixedUpdate:552-566`, on its first frame as master-and-focus, threw away every module
// and reloaded from cfg — reverting `_autostage` to `True`, which is §B8's ONE sanctioned deviation and
// exists precisely to stop what followed. Contributing factor F-102: `LastStage` went back to −1 and
// `AutostageLimit` sat at its default **0**, so NOTHING BOUNDED HOW FAR THE CASCADE COULD RUN:
//
//     MET 124.08   stage.staged   from: 6 -> to: 1     mass 572,285 kg -> 7,470 kg
//     MET 411.50   flight.splashdown   ~134 m/s, NO PARACHUTES
//
// Five stages in one frame at 33.9 km and Mach 5.6, with 11,456 m/s of Δv still owed.
//
// ---- ⛔ AND THIS IS WHY THERE WERE NO PARACHUTES, WHICH IS THE PART THAT MATTERS HERE ----
// `docs/reference/Crew-2.craft` (tier-1, in-repo) puts the drogues at **istg 2** and the mains at
// **istg 1**. The cascade ran 6 → 1, so it fired the S1/S2 separation, the S2 engine, the S2 tank and
// the Dragon decoupler, the trunk, **and the drogues** — at 33.9 km. The recovery system was expended
// on the way up. ⭐ A floor that stops the cascade at its FIRST step saves everything below it, which
// is the entire argument for this file existing separately from the two fixes that should have
// prevented the cascade at all.
//
// ---- THE LADDER, READ OUT OF THE CRAFT FILE, NOT ASSUMED ----
//     istg 8  TE.19.F9.S1.Engine .................. octaweb ignition
//     istg 7  TE.Ghidorah.Erector .................. hold-down release — LIFTOFF
//     istg 6  S1 Interstage + S1 Tank + 4 grid fins + 4 legs + 2 clamps ... ⭐ S1/S2 SEPARATION
//     istg 5  TE.19.F9.S2.Engine ................... SES-1
//     istg 4  Dragon Decoupler + S2 Tank + 4 S2 RCS
//     istg 3  TE.18.DRAGONV2.TRUNK
//     istg 2  TE.CD2.POD.DROGUES
//     istg 1  TE.CD2.POD.MAINS
//     istg 0  NDS + heatshield + pod
//
// ---- HOW THE FLOOR WORKS, IN MECHJEB'S OWN TERMS ----
// `MechJebModuleStagingController.cs:284` refuses to stage while
// `Vessel.currentStage <= AutostageLimit`. So setting the limit to the S1/S2 separation stage means
// MechJeb may still fire 8 (ignition) and 7 (liftoff) — the two the conductor's own T-0 path performs,
// so the vehicle is never stranded on the pad — and **cannot fire 6 or anything below it**. The launch
// vehicle, the second stage, the trunk and both parachute stages are out of its reach.
//
// ---- ⛔ WHY IT IS DERIVED AND NOT TYPED IN AS "6" ----
// The number is a property of the CRAFT, and this repo holds sixteen `.craft` files. A literal 6 would
// be right for `Crew-2` and silently wrong for the next vehicle — and "silently wrong safety limit" is
// the same defect class as the one that produced this line. The interstage is located by
// `VehicleParts.IsInterstage`, which is the SOURCED predicate `pure/Actuation.cs` already routes the
// stage-sep decoupler by (§1.4), so the floor moves with the craft.
//
// ---- ⛔ AND WHAT HAPPENS WHEN IT CANNOT BE DERIVED: IT CLAMPS SHUT, IT DOES NOT OPEN ----
// No interstage found means we cannot identify the launch vehicle, and the honest response to "I do not
// know which stage is safe" is not "then anything goes". `Unknown` returns a floor ABOVE the top stage,
// which forbids autostaging entirely. That costs nothing in the design as flown — §B8 sets
// `Autostage = false` and §B12.7 gives direct part control to `Actuator`, so MechJeb autostaging is
// never how this vehicle is supposed to stage. The floor only ever bites when something has already
// gone wrong.
// ============================================================================================

namespace DragonScreen
{
    /// <summary>One part, reduced to the two facts the floor needs. Built by the glue from the live
    /// vessel; no KSP type reaches this file.</summary>
    public struct StagePart
    {
        /// <summary>`Part.inverseStage` — the stage that ACTIVATES this part.</summary>
        public int Stage;
        /// <summary>`PartNames.Of(p)` — the OCT2 contract, the same string `craftdump.csv` records.</summary>
        public string Name;

        public static StagePart Of(int stage, string name)
        {
            StagePart p; p.Stage = stage; p.Name = name; return p;
        }
    }

    public static class StagingFloor
    {
        /// <summary>
        /// ⛔ The floor used when the launch vehicle cannot be identified. Far above any real KSP stage
        /// number, so `currentStage &lt;= AutostageLimit` is true on every tick and MechJeb never stages.
        /// A number, not a sentinel, because it is written straight into `AutostageLimit.Val`.
        /// </summary>
        public const int ForbidAll = 99;

        /// <summary>
        /// The stage at or below which MechJeb must not autostage. ⭐ Returns the stage carrying the
        /// INTERSTAGE — the S1/S2 separation — so ignition and liftoff stay reachable and everything
        /// from stage separation downward does not.
        ///
        /// ⛔ Returns <see cref="ForbidAll"/> when no interstage is present, which forbids autostaging
        /// outright. See the header: "I do not know which stage is safe" must never resolve to
        /// "then anything goes".
        /// </summary>
        public static int For(StagePart[] parts)
        {
            if (parts == null || parts.Length == 0) return ForbidAll;

            bool found = false;
            int sep = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Name == null) continue;
                if (!VehicleParts.IsInterstage(parts[i].Name)) continue;
                // ⚠ HIGHEST, not first. Symmetry counterparts and a re-flown booster can both put more
                // than one matching part in the list; the SAFEST reading of several is the one that
                // stops the cascade earliest.
                if (!found || parts[i].Stage > sep) { sep = parts[i].Stage; found = true; }
            }
            if (!found) return ForbidAll;

            // ⛔ A negative stage cannot bound anything — `currentStage` is never negative, so a floor
            // below zero is the same as no floor at all. Treat it as underivable rather than as a limit.
            return sep < 0 ? ForbidAll : sep;
        }

        /// <summary>
        /// Would this floor still permit the stage that is about to fire? ⭐ Mirrors
        /// `MechJebModuleStagingController.cs:284`'s own comparison (`currentStage &lt;= AutostageLimit`
        /// returns without staging) so the test can assert against MechJeb's rule rather than a
        /// paraphrase of it.
        /// </summary>
        public static bool WouldStage(int currentStage, int floor)
        {
            return currentStage > floor;
        }
    }
}
