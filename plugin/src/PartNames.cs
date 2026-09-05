// DragonScreen — PartNames  (GLUE: the ONE expression that turns a KSP `Part` into the name this
// project classifies on)
// ============================================================================================
// ---- WHY THIS FILE EXISTS: ONE CONTRACT THAT USED TO LIVE IN FIVE PLACES ----
// `OCT1` (2026-09-05) found that `Part.name` and `partInfo.name` are DIFFERENT STRINGS on a live
// vessel, and that reading the wrong one silently dropped every booster engine command for a whole
// descent. Its fix was correct and is unchanged; what it could not fix inside its own scope was that
// the expression then existed in five separate copies, two of which carry a comment saying — in so
// many words — that they must be edited together:
//
//   `OctawebEngines.PartName`   *"change it here and in `BoosterHost.Describe` together"*
//   `BoosterHost.Describe`      *"change this line and `OctawebEngines.PartName` together or the
//                                disagreement comes straight back"*
//   `CraftDump.DumpPart`        the dump the PURE LAYER'S TESTS are written against
//   `GeometryDump`              the other dump
//   `pure/OctawebBinding`       a comment REQUIRING the expression of whoever calls it
//
// ⛔ A RULE THAT SAYS "REMEMBER TO CHANGE THE OTHER ONE" IS NOT A CONTRACT, IT IS A HOPE — and this
// project has the receipt: the OCT1 outage WAS two classifiers disagreeing about one vessel. The same
// reasoning retired `Stroke` in S122, the second draw path in S100, and `ChromeBar.TopY`'s one-argument
// form in S120: two aligned copies can drift, one cannot. `OCT2` hoists it here, and every one of those
// five now calls this instead of restating it. Their comments STAY WHERE THEY ARE (C1.16 / G12) —
// the reasoning is the asset, the duplication was the defect.
//
// ---- AND IT WAS NOT ONLY THOSE FIVE ----
// `Actuator.cs` read a bare `Part.name` in seventeen places and `VesselData.cs` in three, every one of
// them feeding a SUBSTRING matcher (`VehicleParts.IsBooster` / `IsDrogues` / `IsMains` / `IsErector`,
// `Actuation.DecouplerRoleOf`, a `"Grid Fin"` search). ⚠ None of them was broken: a substring test
// survives the extra characters, which is exactly the luck `BoosterHost.Describe` was running on before
// OCT1 — `IsBooster`'s `.S1.` substring passed while the octaweb binder's whole-name EQUALITY failed, on
// the same part, in the same frame. **This removes the luck; it does not fix a live failure.** It stops
// mattering the first time any of those matchers is tightened, or the extras ever land mid-marker.
//
// ---- WHAT THE EXPRESSION IS, AND WHY IT IS THAT ----
// `partInfo.name` is the part's IDENTITY from the part database — the string the .craft file names, the
// string `docs/reference/craftdump.csv` records, and therefore the string every pure test asserts.
// `Part.name` is a live Unity `Object.name` on the scene instance and is NOT the contract; on
// 2026-09-05 it was not equal to `TE.19.F9.S1.Engine` and the difference was invisible in a log line.
// The fallback to `Part.name` covers a part with no `partInfo` at all, which should not happen and is
// not worth throwing over. `?? ""` so no caller has to null-check a classification input.
// ⚠ `partInfo.title` is the DISPLAY name and is a different thing again — `BlackBoxRecorder` and
// `HullCams` read it deliberately, for text a human reads, and neither belongs here.
// ============================================================================================

namespace DragonScreen
{
    public static class PartNames
    {
        /// <summary>The name this project classifies a part by. See this file's header for why it is
        /// `partInfo.name` and not `Part.name`, and for the outage that settled it (OCT1). Never null.
        /// </summary>
        public static string Of(Part p)
        {
            if (p == null) return "";
            // THE BUILD GUARD (`build.py part_name_source_check`) refuses a bare `Part.name`
            // anywhere in the glue, and it caught THIS line the first time it ran - correctly. The
            // one place the fallback may be written is the one place that defines it, so the marker
            // sits on the line itself rather than in a comment near it: an exemption you cannot see
            // at the offending line is how a guard quietly stops guarding.
            return (p.partInfo != null ? p.partInfo.name : p.name) ?? "";   // OCT2-ALLOW-RAW-NAME
        }
    }
}
