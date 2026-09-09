/*
 * S254 — THE CRAFT DUMP'S ROW RULES, DRIVEN FROM FIXTURES.
 *
 * 🟢 OWNER 2026-09-09: *"add literally everything the part dump could possibly capture for us. Look up
 * every single thing we could possibly capture with it and make sure we get it"*.
 *
 * ⛔⛔ WHY THIS SUITE EXISTS AND WHAT IT CAN AND CANNOT SEE. `src/CraftDump.cs` is glue: it needs KSP
 * to compile and `build.py test` compiles `src/pure` + `test` only, so nothing headless can ever call
 * it. That is exactly why S254 moved the row RULES into `pure/CraftDumpRows.cs` — what this suite
 * proves is that every row kind formats what it claims to, that the generic walk survives a throwing
 * member, and that a locked resource and an unhandled UI control both come out RIGHT rather than
 * merely coming out.
 *
 * ⛔ THE TWO SHARPEST CHECKS ARE THE ONES THE PROMPT NAMED, AND BOTH ARE DRIVEN, NOT OBSERVED:
 *   (a) ⚠ THE LAST DUMP HAD EVERY RESOURCE `flowing` (`docs/reference/craftdump.csv`: 21 RESOURCE
 *       rows, 21 of them `flowing`), so the `locked` path has NEVER been exercised on a real vessel.
 *       It is driven here with `flowState = false` rather than waited for.
 *   (b) ⛔ A THROWING PROPERTY MUST COST ONE ROW, NOT ONE PART. `ThrowingFixture` has a property that
 *       always throws, and the check is that the members either side of it still arrive.
 */
using DragonScreen;
using System;
using System.Collections.Generic;

public static class CraftDumpRowsTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    // =====================================================================================
    // THE FIXTURES — a stand-in `Part`, because the real one needs KSP
    // =====================================================================================

    /// <summary>⛔ THE THROWING-MEMBER FIXTURE. `Middle` throws every time it is read; `Before` and
    /// `After` are ordinary. The whole question is whether the other two survive it.</summary>
    public class ThrowingFixture
    {
        public int Before = 11;
        public int After = 22;
        public string Middle { get { throw new InvalidOperationException("not on the active vessel"); } }
        public double Plain { get { return 3.5; } }
    }

    /// <summary>The Unity-plumbing fixture: a member whose DECLARED type name is a Unity reference
    /// (so it must be named, not walked) beside one whose type is a Unity VALUE type (so it must be
    /// read). ⚠ The pure layer decides by type NAME, so a stand-in with the right name is a faithful
    /// test of the rule and needs no UnityEngine reference.</summary>
    public class ListFixture
    {
        public List<string> Three = new List<string> { "a", "b", "c" };
        public string[] Two = { "x", "y" };
        public string Nothing = null;
        public int Number = 7;
    }

    public static int Run()
    {
        Console.WriteLine("CraftDumpRowsTest (S254: every row kind the craft dump can emit, driven)");

        ShapeAndSize();
        TheGenericWalk();
        TheThermalVerdict();
        TheStructuredRows();
        TheResourceFlow();
        TheModuleAndFieldRows();
        TheEventActionAndVesselRows();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (the glue that reads a live `Part` is proved by the compiler, not here)");
        return failures == 0 ? 0 : 1;
    }

    // =====================================================================================
    // 1. THE CSV SHAPE AND ⛔ THE SIZE RULE
    // =====================================================================================
    static void ShapeAndSize()
    {
        Check("S254 the header names exactly the twelve columns the rows carry",
              CraftDumpRows.Header.Split(',').Length == CraftDumpRows.Columns,
              CraftDumpRows.Header.Split(',').Length + " vs " + CraftDumpRows.Columns);

        // ⛔ A ROW WITH THE WRONG CELL COUNT IS A CAUGHT ERROR, NOT A SHIFTED COLUMN. Silently
        // emitting eleven cells would put every later value under the wrong heading, and the file
        // would still open and still look plausible — which is the worst failure this format has.
        bool threw = false;
        try { CraftDumpRows.Line(new string[] { "a", "b" }); }
        catch (ArgumentException) { threw = true; }
        Check("S254 ⛔ a row with the wrong number of cells is refused, not written short", threw, "");

        string[] cells = new string[CraftDumpRows.Columns];
        for (int i = 0; i < cells.Length; i++) cells[i] = "c" + i;
        string line = CraftDumpRows.Line(cells);
        Check("S254 a complete row is comma-joined and newline-terminated",
              line.EndsWith("\n") && line.Split(',').Length == CraftDumpRows.Columns, line);

        // ⛔ THE SCRUB HAS TO BITE: a comma inside a cell would create a column out of nothing.
        Check("S254 a comma inside a value becomes a semicolon, never a new column",
              CraftDumpRows.Cell("a,b", 160) == "a;b", CraftDumpRows.Cell("a,b", 160));
        Check("S254 ...and so do newlines, which would otherwise create a whole new ROW",
              CraftDumpRows.Cell("a\nb\rc", 160) == "a b c", CraftDumpRows.Cell("a\nb\rc", 160));
        Check("S254 an empty cell is a dash, never nothing",
              CraftDumpRows.Cell("", 160) == "-" && CraftDumpRows.Cell(null, 160) == "-", "");

        // ⭐⭐ §2's SIZE DECISION, PINNED. `extra` is the ONLY wide column, and it is wide because
        // TREE's attach-node list, AERO's drag cubes and INFO's description are useless at 160 —
        // not merely because `GetInfo()` is long.
        Check("S254 ⭐ only the LAST column (`extra`) is wide; the other eleven stay at 160",
              CraftDumpRows.CapFor(CraftDumpRows.Columns - 1) == CraftDumpRows.WideCap
              && CraftDumpRows.CapFor(0) == CraftDumpRows.NarrowCap
              && CraftDumpRows.CapFor(9) == CraftDumpRows.NarrowCap, "");
        Check("S254 ...and the wide cap really is wider than the narrow one",
              CraftDumpRows.WideCap > CraftDumpRows.NarrowCap,
              CraftDumpRows.WideCap + " vs " + CraftDumpRows.NarrowCap);

        // ⛔ TRUNCATION IS MARKED. A silently shortened value that still looks well-formed is worse
        // than one that says it was cut — this is the check that a truncated cell ADMITS it.
        string longText = new string('x', 500);
        string cut = CraftDumpRows.Cell(longText, 160);
        Check("S254 ⛔ a truncated cell is exactly at the cap AND says it was truncated",
              cut.Length == 160 && cut.EndsWith("..."), cut.Length + " chars");
        string wide = CraftDumpRows.Cell(new string('y', 5000), CraftDumpRows.WideCap);
        Check("S254 ...and the wide column truncates at ITS cap, not the narrow one",
              wide.Length == CraftDumpRows.WideCap, wide.Length + " chars");
        Check("S254 a value under the cap is not touched at all",
              CraftDumpRows.Cell("short", 160) == "short", "");

        // ⛔ EVERY KIND IS NAMED ONCE. A kind the glue writes and `Kinds` does not name is a row
        // nobody decided about.
        Check("S254 the eighteen row kinds are all distinct",
              Distinct(CraftDumpRows.Kinds), "duplicate kind");
        Check("S254 ⭐ S254 ADDED kinds rather than renaming them — the six the dump already had survive",
              AllKnown(CraftDumpRows.KindsBeforeS254)
              && CraftDumpRows.Kinds.Length > CraftDumpRows.KindsBeforeS254.Length,
              CraftDumpRows.Kinds.Length + " kinds now, " + CraftDumpRows.KindsBeforeS254.Length + " before");
        Check("S254 ...and every kind §3 names is one the dump knows",
              CraftDumpRows.IsKnownKind("THERMAL") && CraftDumpRows.IsKnownKind("STRUCT")
              && CraftDumpRows.IsKnownKind("AERO") && CraftDumpRows.IsKnownKind("MASS")
              && CraftDumpRows.IsKnownKind("STAGING") && CraftDumpRows.IsKnownKind("TREE")
              && CraftDumpRows.IsKnownKind("IDS") && CraftDumpRows.IsKnownKind("CREW")
              && CraftDumpRows.IsKnownKind("INFO") && CraftDumpRows.IsKnownKind("VESSEL")
              && CraftDumpRows.IsKnownKind("PROP") && CraftDumpRows.IsKnownKind("MODINFO"), "");
        Check("S254 ...and an invented kind is NOT known, so the check is not vacuous",
              !CraftDumpRows.IsKnownKind("BANANA"), "");
    }

    static bool Distinct(string[] a)
    {
        for (int i = 0; i < a.Length; i++)
            for (int j = i + 1; j < a.Length; j++)
                if (a[i] == a[j]) return false;
        return true;
    }

    static bool AllKnown(string[] a)
    {
        for (int i = 0; i < a.Length; i++) if (!CraftDumpRows.IsKnownKind(a[i])) return false;
        return true;
    }

    // =====================================================================================
    // 2. ⭐⭐ THE GENERIC WALK — and the throwing member
    // =====================================================================================
    static void TheGenericWalk()
    {
        List<ReflectedMember> m = CraftDumpRows.ReflectPublic(new ThrowingFixture());

        // ⛔ THE PROMPT'S OWN CHECK: "one throw must cost one row, not one part."
        ReflectedMember before = Find(m, "Before"), after = Find(m, "After"),
                        middle = Find(m, "Middle"), plain = Find(m, "Plain");

        Check("S254 ⛔⛔ a THROWING property costs ONE ROW — the members either side still arrive",
              before.Name == "Before" && before.Value == "11"
              && after.Name == "After" && after.Value == "22"
              && plain.Name == "Plain", "before=" + before.Value + " after=" + after.Value);
        Check("S254 ...and the throwing member is STILL EMITTED, marked, not silently dropped",
              middle.Name == "Middle" && middle.Threw
              && middle.Value.StartsWith("<err:"), middle.Value);
        Check("S254 ...and the message survives, so the owner can see WHICH property is unreadable",
              middle.Value.Contains("not on the active vessel"), middle.Value);
        Check("S254 ...and the members that did NOT throw are not marked as if they had",
              !before.Threw && !after.Threw && !plain.Threw, "");

        // ⭐ THE DECLARED TYPE, which is what a writer needs. `Before` is an int; `Plain` is a double.
        Check("S254 ⭐ every walked member carries its DECLARED type — without it you cannot write it back",
              before.TypeName == "System.Int32" && plain.TypeName == "System.Double",
              before.TypeName + " / " + plain.TypeName);
        Check("S254 ...and fields and properties are told apart",
              before.Kind == "field" && plain.Kind == "property",
              before.Kind + " / " + plain.Kind);

        // ⛔ A null target must not throw — the walk is called inside a per-part guard, but a guard
        // that is never reached because the helper threw first is not a guard.
        Check("S254 a null target yields no rows rather than an exception",
              CraftDumpRows.ReflectPublic(null).Count == 0, "");

        // ⭐ A COLLECTION BECOMES ITS COUNT. `attachNodes`, `children` and `protoModuleCrew` each have
        // their own row kind, so expanding them here as well would say the same thing twice at length.
        List<ReflectedMember> lf = CraftDumpRows.ReflectPublic(new ListFixture());
        Check("S254 a list/array member is reported as its COUNT, not its contents",
              Find(lf, "Three").Value == "count=3" && Find(lf, "Two").Value == "count=2",
              Find(lf, "Three").Value + " / " + Find(lf, "Two").Value);
        Check("S254 a null member reads `null`, which is different from unreadable and from empty",
              Find(lf, "Nothing").Value == "null", Find(lf, "Nothing").Value);
        Check("S254 an ordinary value is just its value",
              Find(lf, "Number").Value == "7", Find(lf, "Number").Value);

        // =====================================================================================
        // ⛔⛔ THE UNITY-PLUMBING RULE, AND THE THREE MEMBERS IT MUST NOT EAT.
        // "Skip Unity plumbing — emit the NAME of such a reference, never walk into it." A blanket
        // "skip everything under UnityEngine" would throw away `CoMOffset` / `CoPOffset` / `CoLOffset`,
        // which are `UnityEngine.Vector3` and are three of the MASS row's own members.
        // =====================================================================================
        Check("S254 ⛔ UnityEngine plumbing is skipped: GameObject/Transform/Rigidbody/Collider",
              CraftDumpRows.SkipMemberType("UnityEngine.GameObject")
              && CraftDumpRows.SkipMemberType("UnityEngine.Transform")
              && CraftDumpRows.SkipMemberType("UnityEngine.Rigidbody")
              && CraftDumpRows.SkipMemberType("UnityEngine.Collider")
              && CraftDumpRows.SkipMemberType("UnityEngine.Renderer"), "");
        Check("S254 ⭐⭐ ...but a Unity VALUE type is NOT skipped — CoMOffset is a Vector3",
              !CraftDumpRows.SkipMemberType("UnityEngine.Vector3")
              && !CraftDumpRows.SkipMemberType("UnityEngine.Quaternion")
              && !CraftDumpRows.SkipMemberType("UnityEngine.Color"), "");
        Check("S254 ...and nothing outside UnityEngine is ever skipped by this rule",
              !CraftDumpRows.SkipMemberType("System.Double")
              && !CraftDumpRows.SkipMemberType("Part")
              && !CraftDumpRows.SkipMemberType("AttachNode")
              && !CraftDumpRows.SkipMemberType(null), "");
        Check("S254 a skipped reference is emitted BY NAME, so the owner knows it is there",
              CraftDumpRows.RefMarker("UnityEngine.Transform") == "<ref:UnityEngine.Transform>", "");
    }

    static ReflectedMember Find(List<ReflectedMember> m, string name)
    {
        for (int i = 0; i < m.Count; i++) if (m[i].Name == name) return m[i];
        ReflectedMember none = new ReflectedMember();
        none.Name = "<missing:" + name + ">"; none.Value = "<missing>";
        return none;
    }

    // =====================================================================================
    // 3. ⭐⭐ THERMAL — the one computed verdict, and the reason the whole task exists
    // =====================================================================================
    static void TheThermalVerdict()
    {
        // ⛔⛔ NTSB FOUND THE TRUNK'S **SKIN** CROSSING ITS LIMIT WHILE ITS INTERNAL TEMPERATURE WAS
        // FINE, and MechJeb's overheat limiter reads `part.temperature` only. So the row has to answer
        // "how close, and on WHICH of the two" — a row that only printed four numbers would leave the
        // owner doing that arithmetic 20 times per dump.
        Check("S254 a fraction is value/limit",
              Near(CraftDumpRows.UsedFraction(500.0, 1000.0), 0.5), "");
        Check("S254 ⛔ a non-positive limit is `n/a`, NOT zero — no limit is not infinite margin",
              CraftDumpRows.UsedFraction(500.0, 0.0) < 0.0
              && CraftDumpRows.UsedFraction(500.0, -1.0) < 0.0, "");
        Check("S254 ...and a NaN or infinite reading is `n/a` too, never a fake number",
              CraftDumpRows.UsedFraction(double.NaN, 1000.0) < 0.0
              && CraftDumpRows.UsedFraction(double.PositiveInfinity, 1000.0) < 0.0
              && CraftDumpRows.UsedFraction(500.0, double.NaN) < 0.0, "");

        // ⭐ THE VERDICT IS DRIVEN BOTH WAYS. Asserting only the skin case would pass against a
        // function that always answers SKIN.
        Check("S254 ⭐⭐ the SKIN being closer to its limit is reported as SKIN",
              CraftDumpRows.WorseOf(0.20, 0.95) == "SKIN", "");
        Check("S254 ...and the INTERNAL being closer is reported as INTERNAL — the verdict is not a constant",
              CraftDumpRows.WorseOf(0.95, 0.20) == "INTERNAL", "");
        Check("S254 ⛔ a TIE goes to SKIN, because skin is the one nothing throttles on",
              CraftDumpRows.WorseOf(0.5, 0.5) == "SKIN", "");
        Check("S254 one uncomputable side names the other; neither computable names nothing",
              CraftDumpRows.WorseOf(-1.0, 0.4) == "SKIN"
              && CraftDumpRows.WorseOf(0.4, -1.0) == "INTERNAL"
              && CraftDumpRows.WorseOf(-1.0, -1.0) == "-", "");

        // ⭐ THE TRUNK'S OWN SHAPE, as the row would print it: internal comfortable, skin nearly gone.
        string hot = CraftDumpRows.ThermalValue(300.0, 2000.0, 1900.0, 2000.0);
        Check("S254 ⭐⭐ the THERMAL row names both temperatures, both limits AND the worse one",
              hot.Contains("T=300") && hot.Contains("skinT=1900")
              && hot.Contains("15.0%") && hot.Contains("95.0%")
              && hot.Contains("worst=SKIN"), hot);
        string cool = CraftDumpRows.ThermalValue(1900.0, 2000.0, 300.0, 2000.0);
        Check("S254 ...and the same row on the OPPOSITE part says INTERNAL",
              cool.Contains("worst=INTERNAL"), cool);
        Check("S254 a part with no skin limit reports n/a rather than 0%",
              CraftDumpRows.ThermalValue(300.0, 2000.0, 300.0, 0.0).Contains("n/a"), "");

        string tx = CraftDumpRows.ThermalExtra(1.5, 0.25, 0.9, 12.0, 1.0, 4.0);
        Check("S254 THERMAL's extra names all six conduction/radiation terms",
              tx.Contains("thermalMass=1.5") && tx.Contains("skinThermalMass=0.25")
              && tx.Contains("emissiveConstant=0.9") && tx.Contains("heatConductivity=12")
              && tx.Contains("skinInternalConductionMult=1") && tx.Contains("radiatorHeadroom=4"), tx);
    }

    // =====================================================================================
    // 4. THE OTHER STRUCTURED ROWS — one driven check each
    // =====================================================================================
    static void TheStructuredRows()
    {
        string s = CraftDumpRows.StructValue(12f, 50f, 25f, 4000.0);
        Check("S254 STRUCT names what breaks and at what load",
              s.Contains("crashTolerance=12") && s.Contains("breakingForce=50")
              && s.Contains("breakingTorque=25") && s.Contains("maxPressure=4000"), s);

        string a = CraftDumpRows.AeroValue(1f, 0.5f, 0.2f, 0.1f, 2f, 0.1f, true);
        Check("S254 AERO names the drag scalars AND ShieldedFromAirstream",
              a.Contains("dragScalar=1") && a.Contains("maximum_drag=0.2")
              && a.Contains("ShieldedFromAirstream=True"), a);
        // ⚠ `ShieldedFromAirstream` is the ASYMMETRY behind BOB-58's pad hang, so a row that reported
        // it only when true would be no use at all.
        Check("S254 ...and reports it when FALSE too, not only when true",
              CraftDumpRows.AeroValue(1f, 0f, 0f, 0f, 0f, 0f, false).Contains("ShieldedFromAirstream=False"), "");
        Check("S254 ⛔ unreadable drag cubes SAY SO rather than reporting zeros as measurements",
              CraftDumpRows.DragCubeExtra(false, 0, 0, 0, 0, 0, 0, 0, 0) == "DragCubes=<unreadable>",
              CraftDumpRows.DragCubeExtra(false, 0, 0, 0, 0, 0, 0, 0, 0));
        Check("S254 ...and readable ones carry the areas and the coefficient",
              CraftDumpRows.DragCubeExtra(true, 2, 1.5f, 0.9f, 3f, 2.5f, 0.4f, 1.1f, 0.7f)
                  .Contains("cubes=2") &&
              CraftDumpRows.DragCubeExtra(true, 2, 1.5f, 0.9f, 3f, 2.5f, 0.4f, 1.1f, 0.7f)
                  .Contains("DragCoeff=0.4"), "");

        string m = CraftDumpRows.MassValue(2f, 1.5f, 8f, "FULL", 1f, 1f);
        Check("S254 MASS separates dry from resource mass AND gives the total",
              m.Contains("mass=2") && m.Contains("resourceMass=8") && m.Contains("total=10"), m);
        Check("S254 ...and the three offsets are carried as text (they are Vector3s)",
              CraftDumpRows.MassExtra("(0;0;1)", "(0;0;2)", "(0;0;3)").Contains("CoPOffset=(0;0;2)"), "");

        // ⛔ `Part.stagingEnabled` DOES NOT EXIST — the row carries Part's real staging members. If a
        // later task "restores" the prompt's name it will not compile, and this row says why.
        string st = CraftDumpRows.StagingValue(3, 3, 1, 0, true, 0, 0, 3, 1, false);
        Check("S254 STAGING carries the members StagingFloor.For reads",
              st.Contains("inverseStage=3") && st.Contains("separationIndex=1")
              && st.Contains("stagingOn=True") && st.Contains("originalStage=3"), st);
        Check("S254 ⛔ ...and it does NOT claim a `stagingEnabled` that Part does not have",
              !st.Contains("stagingEnabled"), st);

        // ---- TREE: the assembly graph -----------------------------------------------------------
        string t = CraftDumpRows.TreeValue(4, "TE.19.F9.S1.Tank", new int[] { 6, 7 }, "SRF_ATTACH");
        Check("S254 ⭐⭐ TREE names the parent by INDEX and by name, and lists the children",
              t.Contains("parent=#4:TE.19.F9.S1.Tank") && t.Contains("children=#6|#7")
              && t.Contains("attachMode=SRF_ATTACH"), t);
        Check("S254 ...and the ROOT says ROOT rather than #-1",
              CraftDumpRows.TreeValue(-1, null, new int[0], "STACK").Contains("parent=ROOT"), "");
        Check("S254 ...and a childless part says `none`, not an empty list",
              CraftDumpRows.TreeValue(0, "x", null, "STACK").Contains("children=none"), "");

        // ⭐ AN EMPTY ATTACH NODE IS THE INTERESTING CASE: it is where something COULD be attached
        // and is not. A row that only printed occupied nodes would answer half the question.
        Check("S254 ⭐ an attached node names what is on it",
              CraftDumpRows.AttachNodeText("top", 2, 9, "TE.19.F9.S2.Tank", "Stack", true)
                  .Contains("->#9:TE.19.F9.S2.Tank"), "");
        Check("S254 ⛔ ...and an EMPTY node says EMPTY, which is the case worth seeing",
              CraftDumpRows.AttachNodeText("bottom", 2, -1, null, "Stack", false).Contains("->EMPTY"), "");
        Check("S254 ...and each node carries size, type and crossfeed",
              CraftDumpRows.AttachNodeText("top", 3, -1, null, "Stack", true)
                  .Contains("size=3") &&
              CraftDumpRows.AttachNodeText("top", 3, -1, null, "Stack", true)
                  .Contains("xfeed=True"), "");
        Check("S254 a part with no attach nodes at all says `none`",
              CraftDumpRows.TreeExtra(new string[0], null).Contains("attachNodes=none")
              && CraftDumpRows.TreeExtra(new string[0], null).Contains("srfAttachNode=none"), "");

        // ⚠ `launchID` was the S147b counter ruling and has never been observed in a dump.
        string ids = CraftDumpRows.IdsValue(101u, 202u, 303u, 404u, 505u, "Full");
        Check("S254 IDS carries all five ids and the control level",
              ids.Contains("flightID=101") && ids.Contains("launchID=303")
              && ids.Contains("missionID=404") && ids.Contains("isControlSource=Full"), ids);

        // ⛔ `Part.crewCapacity` is spelled `CrewCapacity`; the row says so on its face.
        string c = CraftDumpRows.CrewValue(4, new string[] { "Jeb", "Bill" });
        Check("S254 CREW gives the capacity, the count and the names",
              c.Contains("CrewCapacity=4") && c.Contains("aboard=2") && c.Contains("Jeb|Bill"), c);
        Check("S254 ...and an empty seat set says `none`, not an empty string",
              CraftDumpRows.CrewValue(0, null).Contains("crew=none"), "");

        string inf = CraftDumpRows.InfoValue("Propulsion", "SpaceX", 1234.5, 900, "start", "size2");
        Check("S254 INFO carries the catalogue entry, TechRequired included",
              inf.Contains("category=Propulsion") && inf.Contains("cost=1234.5")
              && inf.Contains("entryCost=900") && inf.Contains("TechRequired=start"), inf);
    }

    // =====================================================================================
    // 5. ⭐⭐ RESOURCE — the owner's live question, and the path no flight has ever exercised
    // =====================================================================================
    static void TheResourceFlow()
    {
        // ⛔⛔ THE LAST DUMP HAD ALL 21 RESOURCES `flowing` (`docs/reference/craftdump.csv`, counted),
        // so `locked` has never once been written by a real vessel. It is DRIVEN here.
        Check("S254 ⭐ a flowing resource reads `flowing` — the word the owner's tooling already reads",
              CraftDumpRows.FlowWord(true) == "flowing", "");
        Check("S254 ⛔⛔ ...and a LOCKED one reads `locked` — the path no flight has ever exercised",
              CraftDumpRows.FlowWord(false) == "locked", "");

        // ⛔ `flowMode` IS THE FIELD THAT DECIDES WHETHER LOCKING ONE TANK ACTUALLY STOPS THE ENGINES
        // DRAWING, so it is the one that says whether the idea works at all.
        string e = CraftDumpRows.ResourceExtra("STACK_PRIORITY_SEARCH", true, false, true,
                                               1234, 0.005, 0.8, "Liquid Fuel", "LF", "DIRECT");
        Check("S254 ⭐⭐ RESOURCE's extra carries flowMode — the field that decides whether a lock works",
              e.Contains("flowMode=STACK_PRIORITY_SEARCH"), e);
        Check("S254 ...and the four PAW flags beside it",
              e.Contains("isTweakable=True") && e.Contains("hideFlow=False")
              && e.Contains("isVisible=True"), e);
        Check("S254 ...and the definition: id, density, unit cost, display name, abbreviation, transfer mode",
              e.Contains("info.id=1234") && e.Contains("density=0.005") && e.Contains("unitCost=0.8")
              && e.Contains("displayName=Liquid Fuel") && e.Contains("abbreviation=LF")
              && e.Contains("transferMode=DIRECT"), e);
        // ⛔ §3.2: `flowState` is ALREADY the ui_control column. Repeating it here would be two
        // answers to one question, and the later one would win silently in anyone's spreadsheet.
        Check("S254 ⛔ ...and flowState is NOT repeated in extra — it is the ui_control column already",
              !e.Contains("flowState"), e);
    }

    // =====================================================================================
    // 6. MODULE / FIELD — what makes a module addressable, and what a writer needs
    // =====================================================================================
    static void TheModuleAndFieldRows()
    {
        // ⭐⭐ THE MODULE INDEX. A ModuleManager `MODULE { }` node is addressed by INDEX within
        // `part.Modules`, and the dump has never emitted it. Every patch we write needs it.
        string m = CraftDumpRows.ModuleValue(7, true, true, false, true);
        Check("S254 ⭐⭐ MODULE carries the INDEX a ModuleManager patch addresses it by",
              m.Contains("index=7"), m);
        Check("S254 ...and all four enable flags, which are four different questions",
              m.Contains("enabled=True") && m.Contains("isEnabled=True")
              && m.Contains("moduleIsEnabled=False") && m.Contains("stagingEnabled=True"), m);
        Check("S254 MODULE's extra names the engine id and the applied upgrades",
              CraftDumpRows.ModuleExtra("main", new string[] { "up1" }).Contains("engineID=main")
              && CraftDumpRows.ModuleExtra("main", new string[] { "up1" }).Contains("upgradesApplied=1"), "");
        Check("S254 ...and a module with no engine id says `-`, not empty",
              CraftDumpRows.ModuleExtra(null, null).Contains("engineID=-"), "");

        // ⭐⭐ THE FIELD TYPE. Without it you cannot write the value back — the single most useful
        // addition on this row.
        string f = CraftDumpRows.FieldExtra("Single", true, "kN", "F1", "Thrust", true, false, "");
        Check("S254 ⭐⭐ FIELD carries the declared TYPE — without it you cannot write the value back",
              f.Contains("type=Single"), f);
        Check("S254 ...and isPersistant, which says whether a write would survive a save",
              f.Contains("isPersistant=True"), f);
        Check("S254 ...and the units, format and PAW group",
              f.Contains("guiUnits=kN") && f.Contains("guiFormat=F1") && f.Contains("group=Thrust"), f);
        Check("S254 an unknown field type is `?`, never blank",
              CraftDumpRows.FieldExtra(null, false, null, null, null, false, false, "").Contains("type=?"), "");

        // ⛔⛔ BOTH CONTROLS, NEVER ONE. The old code collapsed them to whichever was non-null first,
        // so a field editable in the VAB and locked in flight looked identical to one that was neither.
        Check("S254 ⛔⛔ a field editable in the VAB and locked in flight is DISTINGUISHABLE",
              CraftDumpRows.ControlCell(null, "UI_FloatRange") == "flight=none editor=UI_FloatRange",
              CraftDumpRows.ControlCell(null, "UI_FloatRange"));
        Check("S254 ...from one editable in flight and not in the VAB",
              CraftDumpRows.ControlCell("UI_FloatRange", null) == "flight=UI_FloatRange editor=none",
              CraftDumpRows.ControlCell("UI_FloatRange", null));
        Check("S254 ...and from one that is neither, which is the only `-`",
              CraftDumpRows.ControlCell(null, null) == "-", "");
        Check("S254 ...and a field with both is reported with both",
              CraftDumpRows.ControlCell("UI_Toggle", "UI_Toggle") == "flight=UI_Toggle editor=UI_Toggle", "");

        // ⛔⛔ AN UNHANDLED CONTROL TYPE EMITS ITS TYPE NAME, NEVER `-`. "There is a control here and
        // I do not know its shape" and "there is no control here" are different answers, and the old
        // `DescribeControl` gave the same one to both.
        Check("S254 ⛔⛔ an UNHANDLED control type still emits its NAME, so it is not mistaken for none",
              CraftDumpRows.ControlDetail("flightCtrl", "UI_SomeModAddedThis", "")
                  == "flightCtrl=UI_SomeModAddedThis",
              CraftDumpRows.ControlDetail("flightCtrl", "UI_SomeModAddedThis", ""));
        Check("S254 ...and a handled one carries its detail in brackets beside the name",
              CraftDumpRows.ControlDetail("flightCtrl", "UI_FloatRange", "min=0 max=1")
                  == "flightCtrl=UI_FloatRange[min=0 max=1]", "");
        Check("S254 ...and no control at all produces nothing, not an empty bracket",
              CraftDumpRows.ControlDetail("flightCtrl", null, "detail") == "", "");
    }

    // =====================================================================================
    // 7. EVENT / ACTION / VESSEL
    // =====================================================================================
    static void TheEventActionAndVesselRows()
    {
        string e = CraftDumpRows.EventExtra(true, false, true, true, false, true, "Fairing");
        Check("S254 EVENT says WHERE the button appears — flight, unfocused, uncommand AND editor",
              e.Contains("guiActive=True") && e.Contains("unfocused=False")
              && e.Contains("uncommand=True") && e.Contains("editor=True"), e);
        Check("S254 ...and whether it is EVA-only and whether it needs full control",
              e.Contains("externalToEVAOnly=False") && e.Contains("requireFullControl=True"), e);
        Check("S254 ...and its PAW group, `-` when it has none",
              e.Contains("group=Fairing")
              && CraftDumpRows.EventExtra(true, true, true, true, true, true, null).Contains("group=-"), e);

        // ⭐ WHICH ACTION GROUP FIRES IT — currently invisible. The conductor presses action groups;
        // without this nobody can say what a press would do.
        string a = CraftDumpRows.ActionExtra("Custom01", "None", true, false);
        Check("S254 ⭐ ACTION names the action group that fires it AND the module's default",
              a.Contains("actionGroup=Custom01") && a.Contains("defaultActionGroup=None"), a);
        Check("S254 ...and whether it is live in the editor and needs full control",
              a.Contains("activeEditor=True") && a.Contains("requireFullControl=False"), a);

        string v = CraftDumpRows.VesselValue("Crew-2", "Ship", "PRELAUNCH", 20, 549054.0, 4);
        Check("S254 ⭐ the VESSEL header row exists at all, and carries name/type/situation",
              v.Contains("name=Crew-2") && v.Contains("type=Ship") && v.Contains("situation=PRELAUNCH"), v);
        Check("S254 ...and the part count, total mass and crew count",
              v.Contains("parts=20") && v.Contains("crew=4") && v.Contains("totalMass=549054"), v);

        // ⚠ `Vessel.launchID` / `Vessel.missionID` DO NOT EXIST — they are PART fields, so the header
        // takes them off the ROOT PART and NAMES them that way rather than implying a vessel-level fact.
        string x = CraftDumpRows.VesselExtra(77u, 303u, 404u, 0, 5, 6);
        Check("S254 ⛔ the vessel row calls them rootLaunchID/rootMissionID — Vessel has neither field",
              x.Contains("rootLaunchID=303") && x.Contains("rootMissionID=404")
              && !x.Contains(" launchID=") && !x.Contains(" missionID="), x);
        Check("S254 ...and carries the root part index, the current stage and StageManager's count",
              x.Contains("rootPartIndex=0") && x.Contains("currentStage=5")
              && x.Contains("StageManager.StageCount=6"), x);
    }

    static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-9; }
}
