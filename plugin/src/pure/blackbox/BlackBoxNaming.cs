// DragonScreen — BlackBox / NAMING  (register BB2; spec: docs/BLACKBOX_RESEARCH.md §4.4)
// ============================================================================================
// PURE. The file-naming rule, lifted out of the glue so it can be ASSERTED headlessly.
//
// ---- WHY THIS FILE EXISTS AT ALL ----
// §4.4 states the rule in one sentence — *"A second tracked vessel (the §B16 booster) opens
// `<MissionId>.<Vessel>.params.csv` — THE SAME MISSION ID. This is the fix for the paired
// `Crew-2_*.csv` / `Crew-2_Probe_*.csv` streams that could only be associated by their timestamps."*
// BB1 had one stream, so the rule lived as three lines inside `OpenFiles` and could not be tested
// without KSP. BB2 has two, and the ONE property the whole two-vessel design rests on — both streams
// carry the SAME mission id, and differ only by a vessel-qualified stem — is exactly the sort of
// property that silently rots. So it is pure, and `BlackBoxTest` asserts it.
//
// ---- THE STEM RULE ----
//   mission id            `<SanitizedVesselName>_<yyyyMMdd_HHmmss at FIRST open>`   e.g. Crew-2_20260904_101500
//   first stream          `<MissionId>.params.csv`        `.events.jsonl`   `.manifest.json`
//   every later stream    `<MissionId>.<Vessel>.params.csv`                 `<MissionId>.<Vessel>.manifest.json`
//   the event log         ⭐ ONE PER MISSION, shared. §4.1 says "per mission, three artefacts"; §4.4
//                         qualifies only the PARAMS file per vessel. Every event line already carries
//                         its own `vessel`, so one ordered narrative across both craft is what §4.10's
//                         new §10 section ("the whole events.jsonl as one ordered narrative") wants,
//                         and splitting it would force a reader to merge two files on a clock to get
//                         back what was never separate.
//   a revert              branches the MISSION, not one stream: `_r2`, `_r3` … on the mission id, so
//                         every vessel's stream re-opens under the new branch together (§4.4).
//
// ⛔ NO ROTATION BY FOCUS. §4.4: "Rotation: none by time, none by vessel focus, none by autopilot
// engage. One mission = one set." The stem is chosen ONCE, when a vessel's stream opens; which vessel
// holds the camera afterwards is a per-ROW fact (`focus`), never a file boundary.
// ============================================================================================
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DragonScreen.BlackBox
{
    public static class BlackBoxNaming
    {
        /// <summary>
        /// A vessel name reduced to path-safe characters. Composed from BB1's glue-private version,
        /// unchanged in behaviour: letters, digits, '-' and '_' survive; everything else becomes '_'.
        /// An empty name becomes "flight" rather than an empty stem, because a file called
        /// ".params.csv" is a hidden file on half the tools that would read it.
        /// </summary>
        public static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "flight";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
                sb.Append((char.IsLetterOrDigit(ch) || ch == '-' || ch == '_') ? ch : '_');
            return sb.ToString();
        }

        /// <summary>
        /// `<SanitizedVesselName>_<stamp>` (§4.4). The stamp is passed in rather than read here so the
        /// function is pure and testable; the glue passes `DateTime.Now.ToString("yyyyMMdd_HHmmss")`.
        /// Keeps `plugin/tools/assess_flight.py`'s existing `Crew-2*` glob working.
        /// </summary>
        public static string MissionId(string vesselName, string stamp)
        {
            return Sanitize(vesselName) + "_" + (stamp ?? "");
        }

        /// <summary>
        /// The per-stream suffix. The FIRST stream of a mission is unqualified — so a single-vessel
        /// mission produces exactly the BB1 file set and nothing downstream changes — and every later
        /// stream is qualified by its vessel: `.Booster`, `.Falcon_9_S1`, …
        /// </summary>
        public static string StreamSuffix(bool primary, string vesselName)
        {
            return primary ? "" : "." + Sanitize(vesselName);
        }

        /// <summary>`<MissionId><suffix>` — the stem the three per-stream extensions hang off.</summary>
        public static string Stem(string missionId, string suffix)
        {
            return (missionId ?? "") + (suffix ?? "");
        }

        /// <summary>
        /// Two vessels can carry the SAME name (KSP allows it, and a booster cloned from the same
        /// craft file routinely does). Two streams must never share a stem, or the second silently
        /// truncates the first — a failure mode worse than not recording, because the file that
        /// survives looks complete. The glue passes the stems already open in this mission; the
        /// disambiguator is `_2`, `_3`, …
        /// </summary>
        public static string UniqueSuffix(string missionId, string suffix, IList<string> takenStems)
        {
            if (takenStems == null || !Contains(takenStems, Stem(missionId, suffix))) return suffix;
            for (int n = 2; n < 100; n++)
            {
                string cand = suffix + "_" + n.ToString(CultureInfo.InvariantCulture);
                if (!Contains(takenStems, Stem(missionId, cand))) return cand;
            }
            return suffix;   // 99 same-named vessels in one mission is not a case worth a loop guard for
        }

        static bool Contains(IList<string> items, string want)
        {
            for (int i = 0; i < items.Count; i++) if (items[i] == want) return true;
            return false;
        }

        /// <summary>
        /// §4.4: a revert branches the MISSION id with `_r2`, `_r3`, … rather than starting a new
        /// mission. Composed verbatim from BB1's glue; hoisted here because BB2 applies it at mission
        /// level (all streams re-open under the new branch together) rather than per stream.
        /// </summary>
        public static string NextRevertSuffix(string missionId)
        {
            if (string.IsNullOrEmpty(missionId)) return "_r2";
            int at = missionId.LastIndexOf("_r", System.StringComparison.Ordinal);
            if (at > 0)
            {
                int n;
                if (int.TryParse(missionId.Substring(at + 2), NumberStyles.Integer,
                                 CultureInfo.InvariantCulture, out n))
                    return "_r" + (n + 1).ToString(CultureInfo.InvariantCulture);
            }
            return "_r2";
        }

        /// <summary>The branched mission id itself — `Crew-2_20260904_101500` → `…_r2` → `…_r3`.</summary>
        public static string BranchMissionId(string missionId)
        {
            if (string.IsNullOrEmpty(missionId)) return missionId;
            string next = NextRevertSuffix(missionId);
            int at = missionId.LastIndexOf("_r", System.StringComparison.Ordinal);
            if (at > 0)
            {
                int n;
                if (int.TryParse(missionId.Substring(at + 2), NumberStyles.Integer,
                                 CultureInfo.InvariantCulture, out n))
                    return missionId.Substring(0, at) + next;
            }
            return missionId + next;
        }
    }
}
