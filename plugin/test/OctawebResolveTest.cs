/*
 * Tests for the octaweb BINDER (pure/OctawebResolve.cs) — §B16.4 step 2, W3 / Wave C, 2026-09-04.
 *
 * W2 landed the GUARD (pure/OctawebBinding.cs: is this the right vehicle's octaweb at all?) with nothing
 * calling it. W3's register line makes the binder this wave's job: call the guard FIRST, refuse and
 * annunciate on anything but Ok, and only then bind the three ModuleEnginesRF BY engineID into a named
 * table — resolved ONCE at the phase boundary, never per frame, and NEVER by position, count or
 * persistent_id. This suite proves the decision half of that headless, against the REAL dump.
 *
 * ⚠ A MISSING DUMP FAILS THIS SUITE DELIBERATELY, exactly as in ActuationTest: the whole point is that
 * the table is checked against the actual craft, and an assertion that passes without one is worthless.
 *
 * ⚠ WHAT THIS DOES *NOT* PROVE. That anything flies. The bound table has no caller in a flight path —
 * §B16.1's booster core is written fresh and is not this wave — and this file tests the PURE resolver,
 * never the KSP glue (src/OctawebEngines.cs), which the headless build cannot compile at all. The glue
 * is the untested half by construction; keep it as thin as it is.
 */
using DragonScreen;
using System;
using System.Collections.Generic;

public static class OctawebResolveTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    // The real names (docs/reference/craftdump.csv, col 2) — the same constants ActuationTest pins.
    const string OCTAWEB = "TE.19.F9.S1.Engine";
    const string MVAC    = "TE.19.F9.S2.Engine";
    const string POD     = "TE.18.DRAGONV2.POD";
    const string TRUNK   = "TE.18.DRAGONV2.TRUNK";

    static OctawebEngineRef E(string part, string id)
    {
        return new OctawebEngineRef { PartName = part, EngineId = id };
    }

    // The nominal octaweb as the dump describes it: ONE part, THREE modules, distinguished by engineID.
    static OctawebEngineRef[] NominalEngines()
    {
        return new[] {
            E(OCTAWEB, OctawebBinding.EngineIdAll),
            E(OCTAWEB, OctawebBinding.EngineIdThreeLanding),
            E(OCTAWEB, OctawebBinding.EngineIdCenterOnly),
            E(MVAC, "MVac"),          // second stage — not the octaweb's, must be skipped
            E(POD, "SuperDraco"),     // abort motor — likewise
        };
    }

    static string[] NominalParts()
    {
        return new[] { POD, TRUNK, OCTAWEB, MVAC };
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen octaweb binder tests (§B16.4 step 2)");

        // ---- THE NOMINAL BIND ----
        OctawebTable t = OctawebResolve.Build(NominalParts(), NominalEngines());
        Check("nominal craft resolves Ok", t.Ok, "plan=" + t.Plan + " guard=" + t.Guard);
        Check("the guard ran and passed", t.Guard == OctawebBind.Ok, "guard=" + t.Guard);
        Check("it binds the Tundra octaweb by name", t.OctawebPart == OctawebBinding.TundraOctawebPart,
              "bound=" + (t.OctawebPart ?? "null"));
        Check("AllEngines is bound to its own module", t.AllIndex == 0, "idx=" + t.AllIndex);
        Check("ThreeLanding is bound to its own module", t.ThreeIndex == 1, "idx=" + t.ThreeIndex);
        Check("CenterOnly is bound to its own module", t.CentreIndex == 2, "idx=" + t.CentreIndex);
        Check("the three roles are DISTINCT modules (the 9-3-1 schedule needs three)",
              t.AllIndex != t.ThreeIndex && t.ThreeIndex != t.CentreIndex && t.AllIndex != t.CentreIndex, "");
        Check("IndexFor(OctawebAll) agrees with the table", t.IndexFor(EngineRole.OctawebAll) == t.AllIndex, "");
        Check("IndexFor(OctawebThree) agrees with the table", t.IndexFor(EngineRole.OctawebThree) == t.ThreeIndex, "");
        Check("IndexFor(OctawebCentre) agrees with the table", t.IndexFor(EngineRole.OctawebCentre) == t.CentreIndex, "");
        Check("IndexFor(SecondStage) is NOT in this table (the MVac is not the octaweb's)",
              t.IndexFor(EngineRole.SecondStage) == -1, "");
        Check("IndexFor(PodAbort) is NOT in this table",
              t.IndexFor(EngineRole.PodAbort) == -1, "");
        Check("IndexFor(None) is -1", t.IndexFor(EngineRole.None) == -1, "");
        Check("a successful bind is SILENT (no annunciation)", OctawebResolve.Annunciation(t) == null, "");

        // The MVac and the SuperDraco are on the vessel and are NOT bound — the classifier's split, not
        // an accident of ordering. (Regression guard's twin: liftoff must light only the all-engines mode.)
        Check("the MVac is not bound into any octaweb role",
              t.AllIndex != 3 && t.ThreeIndex != 3 && t.CentreIndex != 3, "");
        Check("the SuperDraco is not bound into any octaweb role",
              t.AllIndex != 4 && t.ThreeIndex != 4 && t.CentreIndex != 4, "");

        // Order is the CALLER'S, and the indices follow it — a controller maps them onto its own array.
        var shuffled = new[] {
            E(MVAC, "MVac"),
            E(OCTAWEB, OctawebBinding.EngineIdCenterOnly),
            E(OCTAWEB, OctawebBinding.EngineIdAll),
            E(OCTAWEB, OctawebBinding.EngineIdThreeLanding),
        };
        OctawebTable ts = OctawebResolve.Build(NominalParts(), shuffled);
        Check("a different module ORDER still resolves Ok", ts.Ok, "plan=" + ts.Plan);
        Check("...and the indices follow the caller's order, never a fixed position",
              ts.CentreIndex == 1 && ts.AllIndex == 2 && ts.ThreeIndex == 3,
              "all=" + ts.AllIndex + " three=" + ts.ThreeIndex + " centre=" + ts.CentreIndex);

        // ---- (1) THE GUARD RUNS FIRST — every §B16.4 refusal stops the resolve before any engineID ----
        OctawebTable noOcta = OctawebResolve.Build(new[] { POD, TRUNK, MVAC }, NominalEngines());
        Check("no octaweb part -> GuardRefused (NotFound), even with octaweb engineIDs offered",
              noOcta.Plan == OctawebPlan.GuardRefused && noOcta.Guard == OctawebBind.NotFound,
              "plan=" + noOcta.Plan + " guard=" + noOcta.Guard);

        OctawebTable two = OctawebResolve.Build(new[] { OCTAWEB, MVAC, OCTAWEB }, NominalEngines());
        Check("two octawebs -> GuardRefused (Ambiguous) — it must NOT pick one",
              two.Plan == OctawebPlan.GuardRefused && two.Guard == OctawebBind.Ambiguous,
              "guard=" + two.Guard);

        OctawebTable kk = OctawebResolve.Build(new[] { OCTAWEB, MVAC, "KK_SPX_F9_Octaweb" }, NominalEngines());
        Check("our octaweb + a Kartoffelkuchen part -> GuardRefused (ForeignVehicle)  [the mixed-craft trap]",
              kk.Plan == OctawebPlan.GuardRefused && kk.Guard == OctawebBind.ForeignVehicle,
              "guard=" + kk.Guard);

        Check("a null part list never throws — it refuses",
              OctawebResolve.Build(null, NominalEngines()).Plan == OctawebPlan.GuardRefused, "");

        // ---- (2) A MODE IS MISSING: all three, or none of it ----
        OctawebTable noCentre = OctawebResolve.Build(NominalParts(), new[] {
            E(OCTAWEB, OctawebBinding.EngineIdAll),
            E(OCTAWEB, OctawebBinding.EngineIdThreeLanding) });
        Check("a missing CenterOnly -> ModeMissing (no 3->1 handover is not flyable)",
              noCentre.Plan == OctawebPlan.ModeMissing, "plan=" + noCentre.Plan);
        Check("no engines at all -> ModeMissing",
              OctawebResolve.Build(NominalParts(), new OctawebEngineRef[0]).Plan == OctawebPlan.ModeMissing, "");
        Check("a null engine list -> ModeMissing (never a null-ref in a controller tick)",
              OctawebResolve.Build(NominalParts(), null).Plan == OctawebPlan.ModeMissing, "");

        // ---- (3) A MODE IS DUPLICATED: refuse, never pick ----
        OctawebTable dup = OctawebResolve.Build(NominalParts(), new[] {
            E(OCTAWEB, OctawebBinding.EngineIdAll),
            E(OCTAWEB, OctawebBinding.EngineIdThreeLanding),
            E(OCTAWEB, OctawebBinding.EngineIdCenterOnly),
            E(OCTAWEB, OctawebBinding.EngineIdCenterOnly) });
        Check("a duplicated CenterOnly -> ModeDuplicate", dup.Plan == OctawebPlan.ModeDuplicate, "plan=" + dup.Plan);

        // ---- (4) AN OCTAWEB ROLE ON A PART THE GUARD DID NOT BIND ----
        // A second booster part (".S1." marker) offering a landing mode. EngineRoleOf would classify it,
        // and taking it would silently discard the identity check the guard just performed.
        OctawebTable stray = OctawebResolve.Build(new[] { OCTAWEB, MVAC, "TE.19.F9.S1.Tank" }, new[] {
            E(OCTAWEB, OctawebBinding.EngineIdAll),
            E(OCTAWEB, OctawebBinding.EngineIdThreeLanding),
            E("TE.19.F9.S1.Tank", OctawebBinding.EngineIdCenterOnly) });
        Check("an octaweb role on another booster part -> ForeignPart",
              stray.Plan == OctawebPlan.ForeignPart, "plan=" + stray.Plan);

        // ---- EVERY refusal binds NOTHING and SAYS which one it was ----
        foreach (OctawebTable bad in new[] { noOcta, two, kk, noCentre, dup, stray })
        {
            Check("refusal " + bad.Plan + " binds no part", bad.OctawebPart == null, "");
            Check("refusal " + bad.Plan + " leaves every index unbound",
                  bad.AllIndex == -1 && bad.ThreeIndex == -1 && bad.CentreIndex == -1,
                  "all=" + bad.AllIndex + " three=" + bad.ThreeIndex + " centre=" + bad.CentreIndex);
            Check("refusal " + bad.Plan + " reports IndexFor == -1 for every octaweb role",
                  bad.IndexFor(EngineRole.OctawebAll) == -1 && bad.IndexFor(EngineRole.OctawebThree) == -1
                  && bad.IndexFor(EngineRole.OctawebCentre) == -1, "");
            Check("refusal " + bad.Plan + " ANNUNCIATES (refuse and annunciate, never refuse silently)",
                  !string.IsNullOrEmpty(OctawebResolve.Annunciation(bad)), "");
        }
        // A guard refusal defers to the guard's own wording, so the operator sees WHICH §B16.4 failure.
        Check("a guard refusal annunciates the GUARD's line, not a generic one",
              OctawebResolve.Annunciation(kk) == OctawebBinding.Annunciation(OctawebBind.ForeignVehicle),
              OctawebResolve.Annunciation(kk));

        RunDumpAssertions();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // Same locate-by-assembly idiom as ActuationTest/LayoutTest: ".." = plugin/, "../.." = the repo root.
    static string DumpPath()
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "..", "docs", "reference", "craftdump.csv"));
    }

    // THE BIND, AGAINST THE REAL CRAFT. Reads docs/reference/craftdump.csv exactly as the vessel would be
    // read at a phase boundary — every part name, every engine module as a (part, engineID) pair — and
    // asserts the binder produces one complete table off it.
    static void RunDumpAssertions()
    {
        string path = DumpPath();
        if (!System.IO.File.Exists(path))
        {
            Check("craft dump is present in the repo (docs/reference/craftdump.csv)", false, path);
            return;
        }

        string[] lines = System.IO.File.ReadAllLines(path);
        Check("dump has a header and rows", lines.Length > 1, "lines=" + lines.Length);
        // Splitting on ',' is safe BY CONSTRUCTION: CraftDump.C() replaces every comma in a value with
        // ';' before writing (plugin/src/CraftDump.cs:185). The column count is asserted as a guard.
        string[] head = lines[0].Split(',');
        int cols = head.Length;
        Check("dump column 1 is part_name", cols > 1 && head[1] == "part_name", lines[0]);
        Check("dump column 6 is kind", cols > 6 && head[6] == "kind", lines[0]);
        Check("dump column 7 is name", cols > 7 && head[7] == "name", lines[0]);
        Check("dump column 11 is extra", cols > 11 && head[11] == "extra", lines[0]);

        var partNames = new List<string>();
        var seen = new List<string>();
        var engines = new List<OctawebEngineRef>();
        int ragged = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length == 0) continue;
            string[] f = lines[i].Split(',');
            if (f.Length != cols) { ragged++; continue; }
            string name = f[1];
            if (!seen.Contains(name)) { seen.Add(name); partNames.Add(name); }
            if (f[6] != "MODULE" || (f[7] != "ModuleEnginesRF" && f[7] != "ModuleEngines")) continue;
            const string tag = "engineID=";
            int at = f[11].IndexOf(tag, StringComparison.Ordinal);
            engines.Add(new OctawebEngineRef {
                PartName = name,
                EngineId = at >= 0 ? f[11].Substring(at + tag.Length).Trim() : "" });
        }
        Check("every dump row has the header's column count", ragged == 0, "ragged=" + ragged);
        Check("the dump lists engine modules at all", engines.Count > 0, "engines=" + engines.Count);

        OctawebTable t = OctawebResolve.Build(partNames.ToArray(), engines.ToArray());
        Check("REAL DUMP: the octaweb binder resolves Ok",
              t.Ok, "plan=" + t.Plan + " guard=" + t.Guard + " over " + partNames.Count + " parts / "
                    + engines.Count + " engine modules");
        Check("REAL DUMP: it binds " + OctawebBinding.TundraOctawebPart,
              t.OctawebPart == OctawebBinding.TundraOctawebPart, "bound=" + (t.OctawebPart ?? "null"));
        Check("REAL DUMP: all three octaweb roles resolved",
              t.AllIndex >= 0 && t.ThreeIndex >= 0 && t.CentreIndex >= 0,
              "all=" + t.AllIndex + " three=" + t.ThreeIndex + " centre=" + t.CentreIndex);

        // ⛔ THE IDENTITY IS THE engineID STRING — assert the bound indices point at exactly the three
        // strings §B16.4 names, on the octaweb part, and at nothing else.
        if (t.Ok)
        {
            Check("REAL DUMP: AllIndex points at engineID '" + OctawebBinding.EngineIdAll + "'",
                  engines[t.AllIndex].EngineId == OctawebBinding.EngineIdAll, engines[t.AllIndex].EngineId);
            Check("REAL DUMP: ThreeIndex points at engineID '" + OctawebBinding.EngineIdThreeLanding + "'",
                  engines[t.ThreeIndex].EngineId == OctawebBinding.EngineIdThreeLanding, engines[t.ThreeIndex].EngineId);
            Check("REAL DUMP: CentreIndex points at engineID '" + OctawebBinding.EngineIdCenterOnly + "'",
                  engines[t.CentreIndex].EngineId == OctawebBinding.EngineIdCenterOnly, engines[t.CentreIndex].EngineId);
            Check("REAL DUMP: all three bound modules sit on the ONE octaweb part",
                  engines[t.AllIndex].PartName == t.OctawebPart
                  && engines[t.ThreeIndex].PartName == t.OctawebPart
                  && engines[t.CentreIndex].PartName == t.OctawebPart, "");
        }

        // ⛔ NEVER BY COUNT (§B16.4 / VehicleParts.cs:37). Prove the deleted "expect 9 engine parts"
        // procedure still fails on the real craft: three bound modes, not nine parts.
        int octawebModes = 0;
        for (int i = 0; i < engines.Count; i++)
            if (OctawebBinding.IsTundraOctaweb(engines[i].PartName)) octawebModes++;
        Check("REAL DUMP: the octaweb is ONE part with 3 modes, not "
              + VehicleParts.OctawebEngineCount + " engine parts",
              octawebModes == 3 && octawebModes != VehicleParts.OctawebEngineCount, "modes=" + octawebModes);
    }
}
