/*
 * Register W25 — tests for `pure/LandingTarget.cs`: the booster's aim point, resolved by craft name.
 *
 * ⛔ WHAT THESE TESTS ARE FOR. Two of them are ordinary correctness checks; the rest exist to make a
 * §1.4 violation IMPOSSIBLE TO COMMIT QUIETLY. This is the first file in the tree to carry a latitude
 * and a longitude, one previous task fabricated an owner ruling in order to invent two more, and the
 * struck coordinates are still sitting in `docs/reference/LZ_RECOVERY_TABLE.md` §2 where a later chat
 * can read them. So the suite pins, positively:
 *   • the exact digits of the TWO sourced coordinates, against their cited rows;
 *   • that JRTI and ASOG carry NO coordinate — asserted against the STRUCK values by name, so restoring
 *     either one turns this suite red on the spot;
 *   • that there are exactly TWO coordinate-bearing sites in the whole enum, so a THIRD cannot be added
 *     without a test failure;
 *   • that every land-anywhere answer says WHY, so a silent zero cannot come back.
 *
 * ⚠ WHAT IT DOES NOT COVER. `src/BoosterHost.cs`'s wiring — resolving at bind, the world-position
 * conversion, `PredictImpact` over the live vessel, the finite-difference error rates — is GLUE and is
 * compiled but NOT run by this suite (`build.py` builds the plugin from `sources('src')` and the tests
 * from `sources('src/pure','test')`). Only glass can confirm that half. Stated, not implied.
 */
using DragonScreen;
using System;

public static class LandingTargetTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    public static int Run()
    {
        Console.WriteLine("LandingTargetTest (W25: the booster aim point — sourced coordinates, land-anywhere)");

        bool saved = LandingTargets.ForceLandAnywhere;
        LandingTargets.ForceLandAnywhere = false;
        try
        {
            SourcedCoordinates();
            TheStruckOnesStayStruck();
            PerMissionTable();
            LandAnywhereIsAlwaysExplained();
            TheForceSwitch();
        }
        finally { LandingTargets.ForceLandAnywhere = saved; }

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // =============================================================================================
    // The two real coordinates, pinned to the digit against the rows they cite (§1.4).
    // =============================================================================================
    static void SourcedCoordinates()
    {
        // LZ_RECOVERY_TABLE.md §3: "Real coordinate — LZ-1 ... 28.48583, -80.54444".
        Check("LZ-1 latitude is exactly LZ_RECOVERY_TABLE.md §3's 28.48583",
              LandingTargets.Lz1LatDeg == 28.48583, LandingTargets.Lz1LatDeg.ToString("R"));
        Check("LZ-1 longitude is exactly LZ_RECOVERY_TABLE.md §3's -80.54444",
              LandingTargets.Lz1LonDeg == -80.54444, LandingTargets.Lz1LonDeg.ToString("R"));

        // BUILD_PLAN.md §B16.9: "Group 'Of Course I Still Love You', RefLatitude 32.7875,
        // RefLongitude -76.6445" — the KK GROUP CENTRE, which is what guidance targets.
        Check("OCISLY latitude is exactly §B16.9's group-centre 32.7875",
              LandingTargets.OcislyLatDeg == 32.7875, LandingTargets.OcislyLatDeg.ToString("R"));
        Check("OCISLY longitude is exactly §B16.9's group-centre -76.6445",
              LandingTargets.OcislyLonDeg == -76.6445, LandingTargets.OcislyLonDeg.ToString("R"));

        // A coordinate with no citation is a §1.4 violation, so the citation is part of the contract.
        LandingTarget lz = LandingTargets.Resolve("Crew-9");     // RTLS → LZ-1
        Check("an aimed target carries a non-empty citation", !string.IsNullOrEmpty(lz.Citation), "");
        Check("LZ-1's citation names its source document",
              Says(lz.Citation, "LZ_RECOVERY_TABLE.md §3"), lz.Citation ?? "(null)");
        LandingTarget oc = LandingTargets.Resolve("Crew-2");     // droneship → OCISLY
        Check("OCISLY's citation names §B16.9 and the GROUP CENTRE",
              oc.Citation != null && oc.Citation.Contains("§B16.9")
              && oc.Citation.ToUpperInvariant().Contains("GROUP CENTRE"), oc.Citation ?? "(null)");

        Check("Crew-9 resolves to LZ-1 with an aim point", lz.HasAimPoint && lz.Site == RecoverySite.LZ1, "");
        Check("Crew-9's aim point IS the LZ-1 constant pair",
              lz.LatDeg == LandingTargets.Lz1LatDeg && lz.LonDeg == LandingTargets.Lz1LonDeg, "");
        Check("Crew-2 resolves to OCISLY with an aim point",
              oc.HasAimPoint && oc.Site == RecoverySite.OCISLY, "");
        Check("Crew-2's aim point IS the OCISLY constant pair",
              oc.LatDeg == LandingTargets.OcislyLatDeg && oc.LonDeg == LandingTargets.OcislyLonDeg, "");
        Check("an aimed target has no land-anywhere reason", lz.LandAnywhereReason == null, "");
        Check("LandAnywhere is the exact inverse of HasAimPoint", lz.LandAnywhere == !lz.HasAimPoint, "");
    }

    // =============================================================================================
    // ⛔ THE TRIPWIRE. S89 (`8580c81`) struck two invented tier-3 coordinates; they must never return.
    // =============================================================================================
    static void TheStruckOnesStayStruck()
    {
        // The struck values, named here ON PURPOSE so that restoring either of them — from the table's
        // §2, from git history, or from a re-derivation of the same great-circle projection — fails.
        const double StruckJrtiLat = 30.51, StruckJrtiLon = -78.18;
        const double StruckAsogLat = 31.27, StruckAsogLon = -77.95;

        string[] jrtiMissions = { "Crew-1", "Crew-5", "Crew-6" };
        for (int i = 0; i < jrtiMissions.Length; i++)
        {
            LandingTarget t = LandingTargets.Resolve(jrtiMissions[i]);
            Check(jrtiMissions[i] + " knows its site is JRTI", t.Site == RecoverySite.JRTI, "");
            Check(jrtiMissions[i] + " has NO aim point (JRTI is not placed)", !t.HasAimPoint, "");
            Check(jrtiMissions[i] + " is not carrying the STRUCK JRTI coordinate",
                  !(t.LatDeg == StruckJrtiLat && t.LonDeg == StruckJrtiLon), "");
            Check(jrtiMissions[i] + " carries no coordinate at all (0/0, i.e. not a location)",
                  t.LatDeg == 0.0 && t.LonDeg == 0.0, "");
        }

        string[] asogMissions = { "Crew-3", "Crew-4", "Ax-1" };
        for (int i = 0; i < asogMissions.Length; i++)
        {
            LandingTarget t = LandingTargets.Resolve(asogMissions[i]);
            Check(asogMissions[i] + " knows its site is ASOG", t.Site == RecoverySite.ASOG, "");
            Check(asogMissions[i] + " has NO aim point (ASOG is not placed)", !t.HasAimPoint, "");
            Check(asogMissions[i] + " is not carrying the STRUCK ASOG coordinate",
                  !(t.LatDeg == StruckAsogLat && t.LonDeg == StruckAsogLon), "");
        }

        // ⛔ EXACTLY TWO SITES MAY CARRY A COORDINATE. Walk the whole enum rather than the missions, so a
        // third placed site cannot be added without this count moving.
        int aimed = 0;
        foreach (RecoverySite site in Enum.GetValues(typeof(RecoverySite)))
        {
            MissionProfile m = FirstMissionFor(site);
            if (!m.Valid) continue;
            if (LandingTargets.For(m).HasAimPoint) aimed++;
        }
        Check("EXACTLY TWO recovery sites carry a coordinate (LZ-1 and OCISLY), no third",
              aimed == 2, "aimed=" + aimed);

        // And the sites that DO NOT are still correctly NAMED — "we don't know where it is" and "we don't
        // know which ship it was" are different failures, and only the first one is true here.
        Check("JRTI is still named, not blanked",
              LandingTargets.NameOf(RecoverySite.JRTI) == "Just Read The Instructions", "");
        Check("ASOG is still named, not blanked",
              LandingTargets.NameOf(RecoverySite.ASOG) == "A Shortfall Of Gravitas", "");
        Check("land-anywhere is spelled out rather than left blank",
              LandingTargets.NameOf(RecoverySite.None) == "LAND ANYWHERE", "");
    }

    // =============================================================================================
    // The per-mission table against LZ_RECOVERY_TABLE.md §1's own summary line, row for row.
    // =============================================================================================
    static void PerMissionTable()
    {
        // §1: "8 real droneship recoveries, 8 real RTLS recoveries. Droneship split: OCISLY (DM-2,
        // Crew-2), JRTI (Crew-1, Crew-5, Crew-6), ASOG (Crew-3, Crew-4, Ax-1). All 8 RTLS missions
        // used LZ-1."
        Expect("DM-2", RecoverySite.OCISLY);
        Expect("Crew-1", RecoverySite.JRTI);
        Expect("Crew-2", RecoverySite.OCISLY);
        Expect("Crew-3", RecoverySite.ASOG);
        Expect("Ax-1", RecoverySite.ASOG);
        Expect("Crew-4", RecoverySite.ASOG);
        Expect("Crew-5", RecoverySite.JRTI);
        Expect("Crew-6", RecoverySite.JRTI);
        Expect("Ax-2", RecoverySite.LZ1);
        Expect("Crew-7", RecoverySite.LZ1);
        Expect("Ax-3", RecoverySite.LZ1);
        Expect("Crew-8", RecoverySite.LZ1);
        Expect("Crew-9", RecoverySite.LZ1);
        Expect("Crew-10", RecoverySite.LZ1);
        Expect("Ax-4", RecoverySite.LZ1);
        Expect("Crew-11", RecoverySite.LZ1);

        // The counted split — the arithmetic §1 states about itself.
        int ocisly = 0, jrti = 0, asog = 0, lz1 = 0, none = 0;
        for (int i = 0; i < Missions.Catalog.Length; i++)
        {
            switch (LandingTargets.SiteFor(Missions.Catalog[i]))
            {
                case RecoverySite.OCISLY: ocisly++; break;
                case RecoverySite.JRTI:   jrti++;   break;
                case RecoverySite.ASOG:   asog++;   break;
                case RecoverySite.LZ1:    lz1++;    break;
                default:                  none++;   break;
            }
        }
        Check("§1's droneship split: OCISLY 2", ocisly == 2, ocisly.ToString());
        Check("§1's droneship split: JRTI 3", jrti == 3, jrti.ToString());
        Check("§1's droneship split: ASOG 3", asog == 3, asog.ToString());
        Check("§1's RTLS count: 8 missions, all LZ-1", lz1 == 8, lz1.ToString());
        Check("8 droneship + 8 RTLS = §1's 16-mission roster", ocisly + jrti + asog + lz1 == 16, "");
        Check("the 3 free-flyers are NOT in §1's roster and get no target",
              none == 3, none.ToString());

        // ⚠ The site table must AGREE with `MissionProfile.Recovery`, which S66 corrected independently
        // against the same public record. Two tables sourced from the same document that disagree would
        // mean one of them was transcribed wrong.
        for (int i = 0; i < Missions.Catalog.Length; i++)
        {
            MissionProfile m = Missions.Catalog[i];
            RecoverySite site = LandingTargets.SiteFor(m);
            if (site == RecoverySite.None) continue;
            bool rtls = site == RecoverySite.LZ1;
            Check("site table agrees with MissionProfile.Recovery for " + m.Name,
                  rtls == (m.Recovery == RecoveryMode.RTLS), m.Name + " site=" + site + " rec=" + m.Recovery);
        }

        // Resolution goes through `Missions.Resolve`, so the descriptive VAB names it already handles
        // must reach the right site too — and Crew-1 vs Crew-11 must not collide.
        Check("a descriptive craft name still resolves to the right site",
              LandingTargets.Resolve("Falcon 9 - Crew-2 Real Size").Site == RecoverySite.OCISLY, "");
        Check("Crew-11 (RTLS/LZ-1) does not resolve as Crew-1 (JRTI)",
              LandingTargets.Resolve("Crew-11").Site == RecoverySite.LZ1, "");
    }

    // =============================================================================================
    // Every land-anywhere answer must SAY WHY. A silent zero is what W25 exists to remove.
    // =============================================================================================
    static void LandAnywhereIsAlwaysExplained()
    {
        string[] targetless = { "Crew-1", "Crew-3", "Inspiration4", "Polaris Dawn", "Fram2",
                                "Not A Real Mission", "", null };
        for (int i = 0; i < targetless.Length; i++)
        {
            LandingTarget t = LandingTargets.Resolve(targetless[i]);
            string who = "\"" + (targetless[i] ?? "(null)") + "\"";
            Check(who + " lands anywhere", t.LandAnywhere && !t.HasAimPoint, "");
            Check(who + " SAYS WHY (non-empty reason)",
                  !string.IsNullOrEmpty(t.LandAnywhereReason), "");
            Check(who + " has no citation, because it has no coordinate to cite",
                  t.Citation == null, t.Citation ?? "");
            Check(who + " never reports a coordinate", t.LatDeg == 0.0 && t.LonDeg == 0.0, "");
            Check(who + " always has a printable site name", !string.IsNullOrEmpty(t.SiteName), "");
        }

        // An unresolved craft name and a known-but-unplaced droneship are DIFFERENT situations and must
        // not be reported with the same sentence.
        string unresolved = LandingTargets.Resolve("Not A Real Mission").LandAnywhereReason;
        string unplaced = LandingTargets.Resolve("Crew-1").LandAnywhereReason;
        Check("an unresolved craft name and an unplaced droneship give DIFFERENT reasons",
              unresolved != unplaced, "");
        // ⚠ null-SAFE on purpose: these are tripwires, and a tripwire that throws instead of failing
        // hides every check after it. Found while mutation-proving this suite (restoring the struck
        // JRTI coordinate made the reason null and the run aborted mid-suite).
        Check("the unplaced-droneship reason names the ship",
              Says(unplaced, "Just Read The Instructions"), unplaced ?? "(null)");
        Check("the free-flyer reason names the roster it is missing from",
              Says(LandingTargets.Resolve("Fram2").LandAnywhereReason, "LZ_RECOVERY_TABLE"), "");
    }

    // =============================================================================================
    // The owner's switch: "you can use the land anywhere option to start with" (2026-09-05).
    // =============================================================================================
    static void TheForceSwitch()
    {
        Check("the switch's CODE DEFAULT is false, so placed sites do aim",
              LandingTargets.Resolve("Crew-9").HasAimPoint, "");

        LandingTargets.ForceLandAnywhere = true;
        try
        {
            string[] all = { "Crew-9", "Crew-2", "Crew-1", "Ax-1", "Fram2", "Not A Real Mission" };
            for (int i = 0; i < all.Length; i++)
            {
                LandingTarget t = LandingTargets.Resolve(all[i]);
                Check("forced: " + all[i] + " lands anywhere", !t.HasAimPoint, "");
                Check("forced: " + all[i] + " reports no coordinate",
                      t.LatDeg == 0.0 && t.LonDeg == 0.0, "");
                Check("forced: " + all[i] + " says the reason is the setting",
                      Says(t.LandAnywhereReason, "forced by setting"), "");
            }
            // Forcing land-anywhere must not ERASE what we know — the site is still correctly named, so
            // the recorder can say which target was skipped rather than losing the fact.
            Check("forced: the site is still identified (LZ-1 for an RTLS mission)",
                  LandingTargets.Resolve("Crew-9").Site == RecoverySite.LZ1, "");
            Check("forced: the reason names the site it is not aiming at",
                  Says(LandingTargets.Resolve("Crew-9").LandAnywhereReason, "LZ-1"), "");
        }
        finally { LandingTargets.ForceLandAnywhere = false; }

        Check("switching back restores the aim point",
              LandingTargets.Resolve("Crew-9").HasAimPoint, "");
    }

    // ---- helpers -------------------------------------------------------------------------------
    /// <summary>`s != null && s.Contains(needle)` — so a null reason FAILS the check instead of
    /// aborting the whole suite with a NullReferenceException.</summary>
    static bool Says(string s, string needle) { return s != null && s.Contains(needle); }

    static void Expect(string mission, RecoverySite site)
    {
        Check("LZ_RECOVERY_TABLE.md §1: " + mission + " -> " + site,
              LandingTargets.Resolve(mission).Site == site,
              LandingTargets.Resolve(mission).Site.ToString());
    }

    static MissionProfile FirstMissionFor(RecoverySite site)
    {
        for (int i = 0; i < Missions.Catalog.Length; i++)
            if (LandingTargets.SiteFor(Missions.Catalog[i]) == site) return Missions.Catalog[i];
        return Missions.Fallback;
    }
}
