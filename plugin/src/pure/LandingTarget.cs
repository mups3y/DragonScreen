// DragonScreen — LandingTarget  (PURE: the booster's AIM POINT, resolved by craft name)
// ============================================================================================
// Register **W25**, 2026-09-05. `src/BoosterHost.cs` supplied `TargetBearing`, `DownrangeErrM`,
// `InitialDownrangeErrM` and the whole `GridFinInputs` error set as **ZERO**, because no aim point
// existed anywhere in code — W23 put no latitude or longitude in any file (§1.4) and LZ1's sourced
// per-mission table is a DOCUMENT. This file is the seam that turns that document into a target, and
// `BoosterDescent.TargetModeFor` is the mode half it sits beside.
//
// ============================================================================================
// ⛔ §1.4 — THIS IS THE FIRST FILE IN THE TREE TO CARRY A LATITUDE AND A LONGITUDE
// ============================================================================================
// Every coordinate below cites the row it came from, in place, and **anything unsourced is ABSENT** —
// not estimated, not offset, not carried forward from a neighbour. There are exactly TWO coordinates
// here and there is no third. Both are read from `docs/reference/LZ_RECOVERY_TABLE.md` and
// `docs/BUILD_PLAN.md` §B16.9, per C7 — **never from the KSP install's Kerbal-Konstructs cfgs**, even
// though the same numbers are sitting in them.
//
// ⛔ **JRTI AND ASOG CARRY NO COORDINATE HERE, AND MUST NEVER BE GIVEN ONE BY A BUILD CHAT.** `LZ1`
// (`18beda4`) invented two tier-3 coordinates for them on a **fabricated owner ruling** and closed its
// line on that authority; `S89` (`8580c81`) unwound it and the two numbers are struck — kept visible in
// the table's §2 rather than deleted, so the failure stays legible. Whatever their status in that
// document, they are **not §1.4-sourced positions** and they do not enter code. A mission recovering to
// JRTI or ASOG resolves to LAND-ANYWHERE below, which is an honest answer; an invented coordinate is not.
//
// ============================================================================================
// ⭐ LAND-ANYWHERE — THE OWNER'S DIRECTION, 2026-09-05, IN THIS TASK'S OWN CHAT
// ============================================================================================
// Quoted verbatim (C1.12's evidentiary standard): **"you can use the land anywhere option to start
// with"**. That is why `RecoverySite.None` is a NAMED, DELIBERATE resolution here rather than the
// absence of one — before this file, a missing target was an accident of zeros; now it is a decision the
// FSM can be told about and the screens and the recorder can show.
//
// It is also exactly what the owner's EARLIER recorded ruling asks for. `LZ_RECOVERY_TABLE.md` §2, owner,
// 2026-09-04, via the overseer, verbatim: *"The droneships are placed at ROUGH, EXPLICITLY PROVISIONAL
// coordinates. The first booster is flown to wherever it NATURALLY lands for a clean nominal descent —
// the trajectory is not fought to reach a target — and THEN the droneship is moved to that exact measured
// position."* Under that ruling **the target moves to the booster, not the booster to the target**, so a
// clean un-fought descent is the CORRECT flight for a site that is not placed — not a degraded one.
//
// ⚠ **HOW W25 READ THE SCOPE OF "to start with", and what it did NOT assume.** The instruction says
// land-anywhere MAY be used; it does not say the sourced aim points must go unused, and W25's own
// done-criteria require them (*"an RTLS mission's boostback stops annunciating 'no target bearing' for
// the missions whose target is actually placed"*). So both are built:
//   • **placed sites AIM** — `Fossil_LZ1` (RTLS) and OCISLY (ASDS) resolve to real coordinates;
//   • **unplaced sites LAND ANYWHERE** — JRTI, ASOG, and any craft name the catalog does not resolve.
// `ForceLandAnywhere` below is the switch that makes it ALL of them, if that is what was meant — it is
// `[Tunable]`, so the owner can set it from `PluginData/tuning.cfg` with no recompile. **Its code default
// is `false`** (aim where a real coordinate exists), because defaulting it true would make the sourced
// half of this task dead code on the strength of one chat line read one particular way. The question is
// posed back to the owner in W25's register line; ⛔ this chat did not decide it, it deferred it to a
// switch and asked (C1.12/C1.14).
//
// ============================================================================================
// WHAT LAND-ANYWHERE ACTUALLY DOES TO THE FLIGHT — nothing is faked and nothing is aimed
// ============================================================================================
// The host supplies zeros for every target-derived input, exactly as it did before W25, and the FSM's own
// stated behaviours follow — they are the RIGHT behaviours for a free descent, which is why no new phase
// or law is needed:
//   • RTLS boostback refuses and annunciates (*"boostback refused: RTLS has no target bearing to aim
//     at"*) — correct: there is nothing to boost back TO;
//   • ASDS boostback is inert at magnitude 0 (§B16.2's C1.8 OVERRIDE) — correct;
//   • the grid-fin law steers toward zero error, i.e. holds retrograde — correct: a clean, un-fought
//     descent is the whole point;
//   • the entry burn and the hoverslam are UNAFFECTED. Both are vertical/speed solves over live state and
//     need no target at all, so the stage still flies a real, controlled, engine-lit landing — it simply
//     lands where the trajectory was already going. **That is the measurement the owner's ruling wants.**
// ⛔ Land-anywhere is NOT "guidance off" and NOT a licence to invent a target later in the pipeline.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>The recovery sites this project can name. `None` is LAND-ANYWHERE — a real resolution,
    /// not a failure — see the file header.</summary>
    public enum RecoverySite : byte { None, LZ1, OCISLY, JRTI, ASOG }

    /// <summary>A resolved aim point, or an honest statement that there is not one.</summary>
    public struct LandingTarget
    {
        /// <summary>The site this mission recovers to, as REAL FLIGHT HISTORY records it. Known even when
        /// no coordinate exists — JRTI and ASOG are correctly named here and still carry no position.</summary>
        public RecoverySite Site;

        /// <summary>True only when `LatDeg`/`LonDeg` hold a real, cited coordinate and guidance may aim
        /// at it. False = LAND ANYWHERE; every target-derived input stays zero.</summary>
        public bool HasAimPoint;

        /// <summary>Degrees. **Meaningless unless `HasAimPoint`** — they are 0/0, not a location.</summary>
        public double LatDeg, LonDeg;

        /// <summary>The site's real name, for annunciation and the recorder. Never null.</summary>
        public string SiteName;

        /// <summary>Why there is no aim point, in one line, for the crew-facing channel. Null when
        /// `HasAimPoint` is true.</summary>
        public string LandAnywhereReason;

        /// <summary>Where the coordinate came from, quoted tight enough to re-check (§1.4). Null when
        /// there is no coordinate to cite.</summary>
        public string Citation;

        /// <summary>Convenience: the inverse of <see cref="HasAimPoint"/>, named the way the flight
        /// reads it.</summary>
        public bool LandAnywhere { get { return !HasAimPoint; } }
    }

    public static class LandingTargets
    {
        // =========================================================================================
        // ⭐ THE OWNER'S SWITCH — see the file header for the verbatim quote it implements.
        // =========================================================================================
        // `true` = every mission flies LAND-ANYWHERE, including the two whose sites are really placed.
        // Code default `false`; `[Tunable]`, so it is an owner setting rather than a rebuild.
        [Tunable] public static bool ForceLandAnywhere = false;

        // =========================================================================================
        // THE TWO REAL COORDINATES. There is no third, and a build chat may not add one (§1.4/C1.12).
        // =========================================================================================

        /// <summary>LZ-1, Cape Canaveral Space Force Station — a fixed, surveyed ground pad, so a single
        /// real coordinate genuinely exists. **SOURCE: `docs/reference/LZ_RECOVERY_TABLE.md` §3** —
        /// *"Real coordinate — LZ-1, Cape Canaveral Space Force Station: `28.48583, -80.54444`
        /// (28°29′09″N 80°32′40″W; Wikipedia "Landing Zones 1 and 2" / Wikidata Q22078213)"*, placed as
        /// the Fossil Industries static `Fossil_LZ1` (§B16.9: the RTLS gap *"is CLOSED with real
        /// assets"*). This is the pad all 8 RTLS missions in §1 targeted.</summary>
        public const double Lz1LatDeg = 28.48583;
        public const double Lz1LonDeg = -80.54444;
        public const string Lz1Citation =
            "LZ_RECOVERY_TABLE.md §3 (Fossil_LZ1; Wikipedia/Wikidata Q22078213) — surveyed ground pad";

        /// <summary>"Of Course I Still Love You" — the ONE droneship actually placed today, and the
        /// coordinate guidance must use is the **KK GROUP CENTRE**, which §B16.9 is explicit about:
        /// *"The KK GROUP CENTRE's lat/lon is therefore what guidance targets — not a vessel position,
        /// not a static's own offset."* **SOURCE: `docs/BUILD_PLAN.md` §B16.9** — *"EXACTLY ONE barge is
        /// placed today: Group "Of Course I Still Love You", `RefLatitude` **32.7875**, `RefLongitude`
        /// **-76.6445**"*.
        /// ⚠ Do NOT re-source this from `plugin/build/assess_flight.py`'s `BARGE` constant: LZ1's own
        /// §4 item 7 records that that file's value is the **deck centre**, deliberately not the group
        /// centre, and that citing it as the group centre is a mis-citation. The number happens to agree;
        /// the provenance does not. §B16.9 is the row this constant cites.</summary>
        public const double OcislyLatDeg = 32.7875;
        public const double OcislyLonDeg = -76.6445;
        public const string OcislyCitation =
            "BUILD_PLAN.md §B16.9 — OCISLY's PLACED Kerbal-Konstructs GROUP CENTRE (not a deck/vessel position)";

        // =========================================================================================
        // THE PER-MISSION SITE TABLE — `docs/reference/LZ_RECOVERY_TABLE.md` §1, row for row
        // =========================================================================================
        // §1 is *"verified/extended against public flight records (Wikipedia mission/launch-list
        // infoboxes, NASASpaceflight, Spaceflight Now)"* and its summary states the split this table
        // reproduces exactly: *"8 real droneship recoveries, 8 real RTLS recoveries. Droneship split:
        // OCISLY (DM-2, Crew-2), JRTI (Crew-1, Crew-5, Crew-6), ASOG (Crew-3, Crew-4, Ax-1). All 8 RTLS
        // missions used LZ-1."*
        //
        // ⚠ **THIS IS A FINER FACT THAN `MissionProfile.Recovery` CAN HOLD, WHICH IS WHY IT LIVES HERE.**
        // `RecoveryMode` is `Droneship | RTLS` and `MissionProfile.cs` says in place: *"That per-mission
        // recovery-target detail is register LZ1's deliverable, not this enum's — do not widen
        // `RecoveryMode` here to hold it."* W25 obeys that: the enum is untouched and the detail is a
        // separate table, keyed by the same craft name.
        // ⚠ **THE THREE FREE-FLYERS ARE ABSENT ON PURPOSE.** Inspiration4, Polaris Dawn and Fram2 are in
        // the mission catalog but NOT in §1's 16-mission roster, so this repo has no sourced recovery
        // target for them. They resolve to LAND-ANYWHERE — which is the honest answer, and is what §1.4
        // requires when a real quantity has no in-repo source.
        struct Row { public string Mission; public RecoverySite Site; }
        static Row R(string m, RecoverySite s) { Row r; r.Mission = m; r.Site = s; return r; }

        static readonly Row[] Table = new Row[]
        {
            R("DM-2",    RecoverySite.OCISLY),   // §1: OCISLY
            R("Crew-1",  RecoverySite.JRTI),     // §1: JRTI
            R("Crew-2",  RecoverySite.OCISLY),   // §1: OCISLY
            R("Crew-3",  RecoverySite.ASOG),     // §1: ASOG
            R("Ax-1",    RecoverySite.ASOG),     // §1: ASOG
            R("Crew-4",  RecoverySite.ASOG),     // §1: ASOG
            R("Crew-5",  RecoverySite.JRTI),     // §1: JRTI   (resolved — the .craft said only "droneship")
            R("Crew-6",  RecoverySite.JRTI),     // §1: JRTI   (resolved — the .craft said only "droneship")
            R("Ax-2",    RecoverySite.LZ1),      // §1: RTLS → LZ-1
            R("Crew-7",  RecoverySite.LZ1),      // §1: RTLS → LZ-1
            R("Ax-3",    RecoverySite.LZ1),      // §1: RTLS → LZ-1
            R("Crew-8",  RecoverySite.LZ1),      // §1: RTLS → LZ-1
            R("Crew-9",  RecoverySite.LZ1),      // §1: RTLS → LZ-1
            R("Crew-10", RecoverySite.LZ1),      // §1: RTLS → LZ-1
            R("Ax-4",    RecoverySite.LZ1),      // §1: RTLS → LZ-1 (§1 CORRECTS the .craft's "droneship")
            R("Crew-11", RecoverySite.LZ1),      // §1: RTLS → LZ-1 (the final LZ-1 landing, Aug 2025)
        };

        /// <summary>The site a mission really recovered to, or `None` when this repo has no sourced
        /// answer. Keyed by the CATALOG name, so the craft-name normalisation lives in exactly one place
        /// (<see cref="Missions.Resolve"/>) and cannot drift between the two tables.</summary>
        public static RecoverySite SiteFor(MissionProfile mission)
        {
            if (!mission.Valid || string.IsNullOrEmpty(mission.Name)) return RecoverySite.None;
            for (int i = 0; i < Table.Length; i++)
                if (Table[i].Mission == mission.Name) return Table[i].Site;
            return RecoverySite.None;
        }

        /// <summary>Resolve a live VAB craft name straight to an aim point (or to land-anywhere). This is
        /// the one call the glue makes, once, at bind.</summary>
        public static LandingTarget Resolve(string craftName)
        {
            return For(Missions.Resolve(craftName));
        }

        /// <summary>The resolution proper. ⛔ Every branch that does NOT return a coordinate says WHY in
        /// `LandAnywhereReason` — there is no silent zero anywhere in this function.</summary>
        public static LandingTarget For(MissionProfile mission)
        {
            LandingTarget t = new LandingTarget();
            RecoverySite site = SiteFor(mission);
            t.Site = site;
            t.SiteName = NameOf(site);

            if (ForceLandAnywhere)
            {
                t.HasAimPoint = false;
                t.LandAnywhereReason = "LAND ANYWHERE — forced by setting (owner, 2026-09-05: \"you can "
                                     + "use the land anywhere option to start with\")"
                                     + (site == RecoverySite.None ? "" : "; site " + t.SiteName + " not aimed at");
                return t;
            }

            switch (site)
            {
                case RecoverySite.LZ1:
                    t.HasAimPoint = true;
                    t.LatDeg = Lz1LatDeg; t.LonDeg = Lz1LonDeg;
                    t.Citation = Lz1Citation;
                    return t;

                case RecoverySite.OCISLY:
                    t.HasAimPoint = true;
                    t.LatDeg = OcislyLatDeg; t.LonDeg = OcislyLonDeg;
                    t.Citation = OcislyCitation;
                    return t;

                case RecoverySite.JRTI:
                case RecoverySite.ASOG:
                    // ⛔ NOT A BUG AND NOT A GAP TO CLOSE IN CODE. No citable coordinate exists for either
                    // (LZ_RECOVERY_TABLE.md §2: a droneship's recovery position is mission-variable by
                    // design and the search produced *"only a range, never a single citable point"*), and
                    // the two numbers a previous task did put there were invented on a fabricated owner
                    // ruling and struck by S89. Under the owner's 2026-09-04 ruling the fix is not a
                    // coordinate at all — it is a measured landing, after which the droneship is MOVED.
                    t.HasAimPoint = false;
                    t.LandAnywhereReason = "LAND ANYWHERE — " + t.SiteName + " is not placed and has no "
                                         + "sourced coordinate (LZ_RECOVERY_TABLE.md §2); the droneship "
                                         + "moves to the measured touchdown, per the owner's 2026-09-04 ruling";
                    return t;

                default:
                    t.HasAimPoint = false;
                    t.LandAnywhereReason = mission.Valid
                        ? "LAND ANYWHERE — mission \"" + mission.Name + "\" has no recovery target in "
                          + "LZ_RECOVERY_TABLE.md §1 (its 16-mission roster excludes the free-flyers)"
                        : "LAND ANYWHERE — craft name did not resolve to a mission, so there is no "
                          + "recovery target to look up";
                    return t;
            }
        }

        /// <summary>The site's REAL NAME — what the crew and the recorder should see. `None` is spelled
        /// out rather than left blank, because "land anywhere" is a decision, not an empty field.</summary>
        public static string NameOf(RecoverySite site)
        {
            switch (site)
            {
                case RecoverySite.LZ1:    return "LZ-1";
                case RecoverySite.OCISLY: return "Of Course I Still Love You";
                case RecoverySite.JRTI:   return "Just Read The Instructions";
                case RecoverySite.ASOG:   return "A Shortfall Of Gravitas";
                default:                  return "LAND ANYWHERE";
            }
        }
    }
}
