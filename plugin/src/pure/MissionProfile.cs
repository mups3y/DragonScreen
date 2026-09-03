// DragonScreen — MissionProfile  (autopilot rebuild L-S0b, docs/AUTOPILOT_REBUILD_PLAN.md §3A)
// ============================================================================================
// THE FIRST PIECE OF THE REBUILT AUTOPILOT. A mission is DATA, selected by the VAB craft name.
// This is what makes CLAUDE a true autopilot and not a script: the guidance/control is invariant
// and flies physics to the TARGETS in the resolved profile; changing the mission changes only the
// data. Build one .craft per mission named exactly as the mission (saves/test/Ships/VAB/<name>.craft,
// generated from data/crew_missions.json). On the pad, Resolve(vessel.vesselName) picks the profile.
//
// Pure + headless-tested. The catalog is the compiled-in mirror of data/crew_missions.json (the DB is
// the source of truth; regen this table from it when the DB changes). No file I/O in the pure layer.
//
// ---- RESTORED BY W4 (Wave D, §B12.8), 2026-09-04, from `8b81816^` — 7,178 B, byte-for-byte R1 §5.1's row.
// ⚠ ITS MIRROR SOURCE IS NOT IN THE TREE. The catalog below says it mirrors `data/crew_missions.json`; that
// file was deleted 2026-09-01 (R1 §5.4) and survives only in git history at `8b81816^`, so "regen this table
// from it when the DB changes" cannot be done from the working tree today.
// ✅ THE CATALOG WAS RE-CONFIRMED AGAINST AN IN-REPO SOURCE (W4, §1.4) FOR CAPSULE/TAIL/FLIGHT. The 16
// owner-supplied `docs/reference/<mission>.craft` files each carry a description naming that flight's
// capsule, booster tail and booster flight number, and ALL 16 AGREE WITH THIS TABLE ON THOSE THREE FIELDS
// (DM-2 · Crew-1..Crew-11 · Ax-1..Ax-4). The catalog is 19 entries; the three with no `.craft` in the repo
// are the free-flyers Inspiration4, Polaris Dawn and Fram2, whose rows stay uncorroborated in-repo.
// ⚠ THAT CHECK DID NOT COVER `RecoveryMode`. The `.craft` descriptions only ever say generic "droneship" —
// never a specific ship or RTLS — so W4's cross-check could not and did not verify `RecoveryMode` against
// anything but itself. Register **S66** (2026-09-04) sourced `RecoveryMode` independently against public
// flight records and found six rows wrong (Crew-7, Crew-8, Crew-9, Crew-10, Ax-4, Crew-11 — coded Droneship,
// really RTLS); corrected here. Full per-mission citations: `docs/reference/LZ_RECOVERY_TABLE.md` §1/Sources.
// ℹ The `.craft` descriptions also name WHICH droneship (OCISLY / JRTI / ASOG) where `RecoveryMode` can only
// say `Droneship`. That per-mission recovery-target detail is register **LZ1**'s deliverable, not this
// enum's — do not widen `RecoveryMode` here to hold it.
// ⚠ NO CALLER. `Missions.Resolve(vesselName)` was called from `src/CrewProcedureOps.Engage()`, which is not
// in the tree (register **W10**). Nothing resolves a mission today.
// ============================================================================================
using System;

namespace DragonScreen
{
    public enum MissionKind : byte { IssCrew, FreeFlyer }
    public enum RecoveryMode : byte { Droneship, RTLS }

    public struct MissionProfile
    {
        public string Name;          // the VAB craft name that selects this mission
        public string Date;
        public double IncDeg;        // target orbital plane
        public double PeriKm, ApoKm; // 0/0 = the standard ~200 km circular ISS insertion
        public MissionKind Kind;
        public RecoveryMode Recovery;
        public string Capsule;
        public string BoosterTail;
        public int BoosterFlight;    // 1 = new booster
        public bool HasRendezvous;   // ISS crew = true; a free-flyer omits rendezvous/dock/undock
        public bool Valid;           // false = no craft-name match; caller must NO-GO, not fly blind

        public bool FreeFlyer { get { return Kind == MissionKind.FreeFlyer; } }
    }

    public static class Missions
    {
        // ---- helpers to keep the table terse ----
        static MissionProfile Iss(string name, string date, string capsule, string tail, int flight,
                                   RecoveryMode rec)
        {
            return new MissionProfile {
                Name = name, Date = date, IncDeg = 51.6, PeriKm = 0, ApoKm = 0,
                Kind = MissionKind.IssCrew, Recovery = rec, Capsule = capsule,
                BoosterTail = tail, BoosterFlight = flight, HasRendezvous = true, Valid = true };
        }
        static MissionProfile Free(string name, string date, string capsule, string tail, int flight,
                                   double inc, double peri, double apo)
        {
            return new MissionProfile {
                Name = name, Date = date, IncDeg = inc, PeriKm = peri, ApoKm = apo,
                Kind = MissionKind.FreeFlyer, Recovery = RecoveryMode.Droneship, Capsule = capsule,
                BoosterTail = tail, BoosterFlight = flight, HasRendezvous = false, Valid = true };
        }

        // Mirror of data/crew_missions.json (booster tail/flight verified from primary sources 2026-08-26).
        public static readonly MissionProfile[] Catalog = new MissionProfile[]
        {
            Iss ("DM-2",         "2020-05-30", "Endeavour",  "B1058", 1, RecoveryMode.Droneship),
            Iss ("Crew-1",       "2020-11-16", "Resilience", "B1061", 1, RecoveryMode.Droneship),
            Iss ("Crew-2",       "2021-04-23", "Endeavour",  "B1061", 2, RecoveryMode.Droneship),
            Free("Inspiration4", "2021-09-16", "Resilience", "B1062", 3, 51.6, 575, 585),
            Iss ("Crew-3",       "2021-11-11", "Endurance",  "B1067", 2, RecoveryMode.Droneship),
            Iss ("Ax-1",         "2022-04-08", "Endeavour",  "B1062", 4, RecoveryMode.Droneship),
            Iss ("Crew-4",       "2022-04-27", "Freedom",    "B1067", 4, RecoveryMode.Droneship),
            Iss ("Crew-5",       "2022-10-05", "Endurance",  "B1077", 1, RecoveryMode.Droneship),
            Iss ("Crew-6",       "2023-03-02", "Endeavour",  "B1078", 1, RecoveryMode.Droneship),
            Iss ("Ax-2",         "2023-05-21", "Freedom",    "B1080", 1, RecoveryMode.RTLS),
            Iss ("Crew-7",       "2023-08-26", "Endurance",  "B1081", 1, RecoveryMode.RTLS),
            Iss ("Ax-3",         "2024-01-18", "Freedom",    "B1080", 5, RecoveryMode.RTLS),
            Iss ("Crew-8",       "2024-03-04", "Endeavour",  "B1083", 1, RecoveryMode.RTLS),
            Free("Polaris Dawn", "2024-09-10", "Resilience", "B1083", 4, 51.7, 190, 1400),
            Iss ("Crew-9",       "2024-09-28", "Freedom",    "B1085", 2, RecoveryMode.RTLS),
            Iss ("Crew-10",      "2025-03-14", "Endurance",  "B1090", 2, RecoveryMode.RTLS),
            Free("Fram2",        "2025-03-31", "Resilience", "B1085", 6, 90.01, 202, 413),
            Iss ("Ax-4",         "2025-06-25", "Grace",      "B1094", 2, RecoveryMode.RTLS),
            Iss ("Crew-11",      "2025-07-31", "Endeavour",  "B1094", 3, RecoveryMode.RTLS),
        };

        // No craft-name match: a generic ISS-crew plane so nothing is undefined, but Valid=false so the
        // conductor raises a NO-GO — never fly a guessed mission silently (docs plan §3A).
        public static MissionProfile Fallback = new MissionProfile {
            Name = "(unrecognised — generic ISS crew)", Date = "", IncDeg = 51.6, PeriKm = 0, ApoKm = 0,
            Kind = MissionKind.IssCrew, Recovery = RecoveryMode.Droneship, Capsule = "",
            BoosterTail = "", BoosterFlight = 1, HasRendezvous = true, Valid = false };

        // Lowercase; keep only [a-z0-9], so "Falcon 9 - Crew-2 Real Size" -> "falcon9crew2realsize"
        // and "Crew-2" -> "crew2". Exact-name match is the primary path (the generated craft use the
        // bare mission name); the substring pass is a bounded fallback for descriptive craft names.
        static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = char.ToLowerInvariant(s[i]);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
            }
            return sb.ToString();
        }

        public static MissionProfile Resolve(string vesselName)
        {
            string key = Norm(vesselName);
            if (key.Length == 0) return Fallback;

            // 1) exact normalized match (the generated craft names hit this).
            for (int i = 0; i < Catalog.Length; i++)
                if (Norm(Catalog[i].Name) == key) return Catalog[i];

            // 2) substring, LONGEST catalog name first so "crew11" wins over "crew1" (Crew-1 vs Crew-11).
            int bestLen = -1, best = -1;
            for (int i = 0; i < Catalog.Length; i++)
            {
                string cn = Norm(Catalog[i].Name);
                if (cn.Length > bestLen && key.Contains(cn)) { bestLen = cn.Length; best = i; }
            }
            if (best >= 0) return Catalog[best];

            return Fallback;
        }
    }
}
