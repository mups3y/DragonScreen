// DragonScreen — Actuation  (PURE: the capability→role decisions the Actuator glue acts on)
// ============================================================================================
// ⛔ DIRECT PART CONTROL, decided here so it is HEADLESS-TESTED against the REAL craft part names
// (data/craftdump.csv), never re-discovered live per flight. The glue (src/Actuator.cs) enumerates the
// live Part/PartModule objects and calls Activate/Shutdown/Decouple/etc.; WHICH part plays WHICH role is
// this pure classifier's job. Matching is by capability (part-name marker + engineID via VehicleParts),
// never a stage index or an action-group binding — see [[direct-part-control-hard-rule]].
//
// The reference Crew-2 stack (craft dump) resolves to:
//   engines    : TE.19.F9.S1.Engine  = octaweb (3 ModuleEngines, one per mode: All/Three/Centre, by engineID)
//                TE.19.F9.S2.Engine  = MVac  (second stage)
//                TE.18.DRAGONV2.POD  = SuperDraco abort motor (the pod ModuleEnginesRF, config "SuperDraco")
//   decouplers : TE.19.F9.S1.Interstage = stage sep     TE.19.C.Dragon.Decoupler = drop S2 (Dragon sep)
//                TE.18.DRAGONV2.TRUNK   = trunk jettison TE.Ghidorah.Erector      = pad erector/strongback
// ============================================================================================
namespace DragonScreen
{
    // What an engine on a part is FOR — the ignition command targets one role at a time.
    public enum EngineRole { None, OctawebAll, OctawebThree, OctawebCentre, SecondStage, PodAbort }

    // What a decoupler on a part SEPARATES — each mission event fires exactly one role.
    public enum DecouplerRole { None, StageSep, DragonSep, TrunkJettison, Erector }

    public static class Actuation
    {
        // Classify an engine by the part it sits on + its engineID. The octaweb (a booster part) carries
        // three ModuleEngines that differ only by engineID (AllEngines / ThreeLanding / CenterOnly); the
        // MVac sits on the second stage; the SuperDraco abort motor sits on the Dragon pod.
        public static EngineRole EngineRoleOf(string partName, string engineId)
        {
            if (VehicleParts.IsPod(partName)) return EngineRole.PodAbort;            // SuperDraco
            if (VehicleParts.IsSecondStage(partName)) return EngineRole.SecondStage; // MVac
            if (VehicleParts.IsBooster(partName))
            {
                // engineID picks the octaweb mode; a plain/blank id is the all-engines (liftoff) set.
                if (VehicleParts.EngineIdIsMode(engineId, VehicleParts.ModeCentreOnly)) return EngineRole.OctawebCentre;
                if (VehicleParts.EngineIdIsMode(engineId, VehicleParts.ModeThreeEngine)) return EngineRole.OctawebThree;
                return EngineRole.OctawebAll;
            }
            return EngineRole.None;
        }

        // Does this engine light for the given ignition command? (liftoff = OctawebAll; SES-1 = SecondStage;
        // launch escape = PodAbort). ⛔ REGRESSION GUARD (flight_0822_201219): liftoff must light ONLY the
        // all-engines mode — lighting Three/Centre too cooked the S1 tank. EngineRoleOf enforces that split.
        public static bool EngineLightsFor(string partName, string engineId, EngineRole want)
        {
            return want != EngineRole.None && EngineRoleOf(partName, engineId) == want;
        }

        // Classify a decoupler by the part it belongs to. Order matters only in that each real part matches
        // exactly one predicate here (verified in ActuationTest against the live part names).
        public static DecouplerRole DecouplerRoleOf(string partName)
        {
            if (VehicleParts.IsErector(partName)) return DecouplerRole.Erector;
            if (VehicleParts.IsDragonDecoupler(partName)) return DecouplerRole.DragonSep;
            if (VehicleParts.IsTrunk(partName)) return DecouplerRole.TrunkJettison;
            if (VehicleParts.IsInterstage(partName)) return DecouplerRole.StageSep;
            return DecouplerRole.None;
        }
    }
}
