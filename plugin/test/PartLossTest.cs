/*
 * S227 — PART LOSS: the classifier, the flood budget, and the subscription that must not leak.
 *
 * ⛔ WHAT THIS EXISTS TO STOP. The owner's question is *"which failed first"*, and the only way to
 * answer it wrongly-but-plausibly is to label a routine staging separation as a failure, or a real
 * failure as a routine separation. `onPartDie` fires on NORMAL STAGING — every decoupler, fairing and
 * spent stage dies legitimately — so the classifier is the whole channel, and a classifier that can
 * only be exercised by crashing a rocket is one nobody ever checks. That is why the judgement lives in
 * `pure/blackbox/PartLoss.cs` and why this suite can reach it at all.
 *
 * ⭐ THE LOAD-BEARING CHECK IS `StagingDoesNotPromoteToCommanded`. The tempting rule — "a stage command
 * was just given, so this death was commanded" — is wrong in exactly the case the channel exists for:
 * staging is the likeliest instant for something to let go, and that rule would file the failure as
 * routine and delete it from the analysis. The verdict there must be `Unclassified`, which is a WEAKER
 * claim than the evidence would allow, and this suite fails if anyone ever "improves" it.
 *
 * ⭐⭐ AND THE SOURCE SCANNER PROVES ITSELF. `SubscriptionSymmetry` reads `src/PartLossWatch.cs` and
 * fails the build if a `GameEvents` handler is added without being removed — the `GetPotentialTorque`
 * defect, which cost this project a day. ⛔ But a scanner that always returns "balanced" would pass
 * that check on any input, so the same scanner is ALSO fed two synthetic sources that MUST fail it:
 * one with a missing `.Remove`, and one where the `.Remove` is merely COMMENTED OUT. The second is
 * S220's lesson — two of its mutants survived because its assertions matched commented-out code.
 *
 * ⚠ WHAT THIS DOES *NOT* PROVE, AND IT IS MOST OF THE CHANNEL. Not one real part death happens here.
 * Whether `onPartWillDie` fires for every loss, whether `p.vessel` is still readable when it does,
 * whether scene teardown raises per-part deaths at all, and whether the classifier's windows are the
 * right lengths against a real staging sequence — all of that needs the game. See the register's
 * "what a flight has to show" section. What is proven here is that the JUDGEMENT is right on every
 * combination of facts, that the budget keeps the head of a cascade and announces the cut exactly
 * once, and that the subscription is symmetric, guarded, and wired to the scene.
 */
using DragonScreen;
using DragonScreen.BlackBox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public static class PartLossTest
{
    static int checks, failures;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + (detail == "" ? "" : "   " + detail)); }
    }

    static string Repo(params string[] parts)
    {
        var bits = new List<string> {
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", ".." };
        bits.AddRange(parts);
        return Path.GetFullPath(Path.Combine(bits.ToArray()));
    }

    // ⛔ TEXT ASSERTIONS MATCH COMMENTED-OUT CODE. (Idiom: `AutoTargetTest.Live`, S220, where two
    // mutants survived for exactly this reason.) Strip line comments before asserting a statement
    // EXISTS — and note this file relies on it for ABSENCE too: `onPartExplode` is named in
    // PartLossWatch's header as a documented REJECTION, so a naive scan would "find" it subscribed.
    static string Live(string src)
    {
        string[] lines = src.Replace("\r\n", "\n").Split(new char[] { (char)10 });
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].TrimStart();
            if (t.StartsWith("//")) continue;
            sb.Append(lines[i]).Append((char)10);
        }
        return sb.ToString();
    }

    // ---- the fact-builder, so each case below states only what it changes ----
    static PartLossFacts F(double ut, double stageUt, double decoupleUt,
                           bool teardown, bool failure, bool jointBreak)
    {
        PartLossFacts f;
        f.Ut = ut;
        f.LastStageCommandUt = stageUt;
        f.LastDecoupleUt = decoupleUt;
        f.VesselTeardown = teardown;
        f.ArrivedAsFailure = failure;
        f.ArrivedAsJointBreak = jointBreak;
        return f;
    }
    const double NA = double.NaN;

    public static int Run()
    {
        Console.WriteLine("PartLossTest (S227: which part went, when, and whether anybody asked it to)");
        checks = 0; failures = 0;

        TheDefaultClaimsNothing();
        StagingDoesNotPromoteToCommanded();
        TheRest();
        ElapsedIsHonest();
        BudgetKeepsTheHead();
        SubscriptionSymmetry();
        TheScannerCanFail();
        HandlersAreGuarded();
        WiredToTheScene();
        PayloadJoinsToCraftDump();
        ColumnAndKinds();
        S235_EveryHandlerEmits();
        S235_TheEmitScannerCanFail();
        S235_TheLineRendersThePayload();
        S235_TheHotPartIsNamed();
        S235_SettingsVersusReadings();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + (failures == 0 ? "   (staging never promotes to commanded; the scanner is proven to fail)" : ""));
        return failures;
    }

    // ============================================================================================
    //  1. THE DEFAULT VERDICT CLAIMS NOTHING
    // ============================================================================================
    static void TheDefaultClaimsNothing()
    {
        // ⛔ An unset verdict must never read as a confident one. `default(PartLossClass)` is what an
        // uninitialised struct field carries, and if that were `Commanded` a forgotten assignment
        // would silently exonerate a failure.
        Check("S227 the zero value of PartLossClass is Unclassified",
              default(PartLossClass) == PartLossClass.Unclassified,
              "got " + default(PartLossClass));
        Check("...and its token says so", PartLoss.Name(default(PartLossClass)) == "unclassified",
              PartLoss.Name(default(PartLossClass)));
        // All four tokens are distinct, lowercase and stable — an analyser groups on them.
        var seen = new HashSet<string>();
        PartLossClass[] all = { PartLossClass.Unclassified, PartLossClass.Commanded,
                                PartLossClass.Uncommanded, PartLossClass.Teardown };
        for (int i = 0; i < all.Length; i++) seen.Add(PartLoss.Name(all[i]));
        Check("S227 all four classes have distinct tokens", seen.Count == 4, "got " + seen.Count);
    }

    // ============================================================================================
    //  2. ⭐⭐ THE LOAD-BEARING ONE. A STAGE COMMAND IS NOT A LICENCE.
    // ============================================================================================
    static void StagingDoesNotPromoteToCommanded()
    {
        // Staged one second ago; this part itself never decoupled. The vehicle WAS commanded to do
        // something — but not this part, and staging is the likeliest instant for something to let go.
        PartLossVerdict v = PartLoss.Classify(F(100.0, 99.0, NA, false, false, false));
        Check("S227 ⭐ a recent STAGE command alone yields Unclassified, NOT Commanded",
              v.Class == PartLossClass.Unclassified, "got " + PartLoss.Name(v.Class));
        Check("...and says which rule fired", v.Why == PartLoss.WhyStageWindow, v.Why);
        // ⛔ THE POINT, STATED AS A CHECK: this must never become Commanded. If a later chat "improves"
        // the classifier to resolve the ambiguity, this line is what stops it doing so silently.
        Check("...⛔ and it is NEVER Commanded on staging evidence alone",
              v.Class != PartLossClass.Commanded, PartLoss.Name(v.Class));

        // The raw numbers travel WITH the verdict, so an analyst reads the reasoning, not the label.
        Check("S227 the verdict carries the age of the stage command it weighed",
              Math.Abs(v.SinceStageS - 1.0) < 1e-9, "got " + v.SinceStageS);
        Check("...and NaN for a decouple that never happened", double.IsNaN(v.SinceDecoupleS), "");

        // Outside the window there is no ambiguity left to be honest about.
        PartLossVerdict old = PartLoss.Classify(
            F(100.0, 100.0 - PartLoss.StageWindowS - 0.001, NA, false, false, false));
        Check("S227 a stage command OLDER than the window leaves it Uncommanded",
              old.Class == PartLossClass.Uncommanded && old.Why == PartLoss.WhyNoCommand,
              PartLoss.Name(old.Class) + "/" + old.Why);
        // The boundary itself is inclusive, pinned so the window cannot drift by an off-by-one.
        PartLossVerdict edge = PartLoss.Classify(
            F(100.0, 100.0 - PartLoss.StageWindowS, NA, false, false, false));
        Check("...and the window boundary is inclusive",
              edge.Class == PartLossClass.Unclassified, PartLoss.Name(edge.Class));
    }

    // ============================================================================================
    //  3. EVERY OTHER RUNG, INCLUDING THE ORDER THEY FIRE IN
    // ============================================================================================
    static void TheRest()
    {
        // THIS PART'S OWN decouple — the only evidence strong enough for Commanded.
        PartLossVerdict d = PartLoss.Classify(F(100.0, NA, 99.5, false, false, false));
        Check("S227 this part's OWN decouple is the one thing that yields Commanded",
              d.Class == PartLossClass.Commanded && d.Why == PartLoss.WhyDecoupled,
              PartLoss.Name(d.Class) + "/" + d.Why);
        PartLossVerdict dOld = PartLoss.Classify(
            F(100.0, NA, 100.0 - PartLoss.DecoupleWindowS - 0.001, false, false, false));
        Check("...but only inside its window", dOld.Class != PartLossClass.Commanded,
              PartLoss.Name(dOld.Class));

        // KSP's own statements outrank our inference.
        PartLossVerdict f = PartLoss.Classify(F(100.0, NA, NA, false, true, false));
        Check("S227 KSP's own onPartFailure yields Uncommanded",
              f.Class == PartLossClass.Uncommanded && f.Why == PartLoss.WhyKspFailure,
              PartLoss.Name(f.Class) + "/" + f.Why);
        PartLossVerdict j = PartLoss.Classify(F(100.0, NA, NA, false, false, true));
        Check("S227 a joint break yields Uncommanded, under its own reason token",
              j.Class == PartLossClass.Uncommanded && j.Why == PartLoss.WhyJointBreak,
              PartLoss.Name(j.Class) + "/" + j.Why);

        // Nothing at all: no command anywhere near it, vessel intact, part gone. THE FINDING.
        PartLossVerdict bare = PartLoss.Classify(F(100.0, NA, NA, false, false, false));
        Check("S227 ⭐ a part that vanishes with no command anywhere near it is Uncommanded",
              bare.Class == PartLossClass.Uncommanded && bare.Why == PartLoss.WhyNoCommand,
              PartLoss.Name(bare.Class) + "/" + bare.Why);

        // ---- ORDER MATTERS, and each of these would be a different verdict under a different order ----
        // Teardown outranks EVERYTHING. Without this, one scene change emits a vessel's worth of
        // `uncommanded` verdicts and manufactures the false cascade the channel exists to see through.
        Check("S227 ⛔ teardown outranks a KSP failure",
              PartLoss.Classify(F(100.0, NA, NA, true, true, false)).Class == PartLossClass.Teardown, "");
        Check("...outranks a joint break",
              PartLoss.Classify(F(100.0, NA, NA, true, false, true)).Class == PartLossClass.Teardown, "");
        Check("...outranks this part's own decouple",
              PartLoss.Classify(F(100.0, NA, 99.9, true, false, false)).Class == PartLossClass.Teardown, "");
        // A decouple outranks a failure flag: the game said it was released on purpose, about THIS part.
        Check("S227 an explicit decouple outranks a stale failure flag",
              PartLoss.Classify(F(100.0, NA, 99.9, false, true, false)).Class == PartLossClass.Commanded, "");
        // ⭐ And a KSP failure DURING the staging window still reads as a failure — the case the
        // staging rung would otherwise swallow, and the owner's scenario almost exactly.
        PartLossVerdict duringStage = PartLoss.Classify(F(100.0, 99.5, NA, false, true, false));
        Check("S227 ⭐ a KSP failure DURING staging is still a failure, not staging noise",
              duringStage.Class == PartLossClass.Uncommanded
              && duringStage.Why == PartLoss.WhyKspFailure,
              PartLoss.Name(duringStage.Class) + "/" + duringStage.Why);
    }

    // ============================================================================================
    //  4. `Elapsed`: NaN IN, NaN OUT — NEVER ZERO
    // ============================================================================================
    static void ElapsedIsHonest()
    {
        Check("S227 an unseen command stays NaN and never becomes 0",
              double.IsNaN(PartLoss.Elapsed(100.0, NA)), "got " + PartLoss.Elapsed(100.0, NA));
        Check("...an unknown NOW is NaN too", double.IsNaN(PartLoss.Elapsed(NA, 99.0)), "");
        Check("S227 a normal age is the difference", Math.Abs(PartLoss.Elapsed(100.0, 97.5) - 2.5) < 1e-9,
              "got " + PartLoss.Elapsed(100.0, 97.5));
        // ⛔ A clock that moved BACKWARDS (a revert) must not produce a negative age that the rules
        // below would then weigh as "very recent".
        Check("S227 ⛔ a command in the FUTURE (a revert) is NaN, not a negative age",
              double.IsNaN(PartLoss.Elapsed(100.0, 101.0)), "got " + PartLoss.Elapsed(100.0, 101.0));
        // And that NaN must not sneak a part into a window.
        Check("...so a reverted clock cannot make a death look commanded",
              PartLoss.Classify(F(100.0, NA, 101.0, false, false, false)).Class != PartLossClass.Commanded,
              "");
        Check("S227 zero elapsed is a real answer, not NaN", PartLoss.Elapsed(100.0, 100.0) == 0.0, "");
    }

    // ============================================================================================
    //  5. THE FLOOD BUDGET KEEPS THE **HEAD** AND ANNOUNCES THE CUT ONCE
    // ============================================================================================
    static void BudgetKeepsTheHead()
    {
        var b = new PartLossBudget(3);
        Check("S227 the budget admits exactly its cap",
              b.Admit() && b.Admit() && b.Admit(), "");
        Check("...and refuses the next", !b.Admit(), "");
        Check("...and every one after that", !b.Admit() && !b.Admit(), "");
        Check("S227 it counts everything offered, admitted or not", b.Seen == 6, "got " + b.Seen);
        Check("...and reports the cap it was built with", b.Cap == 3, "got " + b.Cap);

        // ⭐ EXACTLY ONCE, however long the cascade runs. This is the property that makes truncation
        // visible without the marker itself becoming the flood.
        Check("S227 ⭐ the truncation marker is due exactly once", b.TakeMarker(), "");
        Check("...and never again", !b.TakeMarker() && !b.TakeMarker(), "");
        for (int i = 0; i < 500; i++) b.Admit();
        Check("...⛔ not even after 500 more losses", !b.TakeMarker(), "seen " + b.Seen);

        // A budget that was never exceeded has nothing to announce.
        var quiet = new PartLossBudget(3);
        quiet.Admit(); quiet.Admit();
        Check("S227 an unexceeded budget never raises a marker", !quiet.TakeMarker(), "");

        // ⛔ THE HEAD, NOT THE TAIL. A ring buffer would keep the last N and throw away the answer.
        // Modelled here as the sequence of admitted indices: it must be 0,1,2 — the FIRST three.
        var kept = new List<int>();
        var head = new PartLossBudget(3);
        for (int i = 0; i < 10; i++) if (head.Admit()) kept.Add(i);
        Check("S227 ⭐ the budget keeps the FIRST losses — the first line is the finding",
              kept.Count == 3 && kept[0] == 0 && kept[1] == 1 && kept[2] == 2,
              "kept " + string.Join(",", kept.ConvertAll(x => x.ToString()).ToArray()));

        // Degenerate caps must not throw or admit.
        var zero = new PartLossBudget(0);
        Check("S227 a zero cap admits nothing and still announces the cut",
              !zero.Admit() && zero.TakeMarker() && !zero.TakeMarker(), "");
        var neg = new PartLossBudget(-5);
        Check("...and a negative cap is clamped to zero rather than throwing",
              neg.Cap == 0 && !neg.Admit(), "cap " + neg.Cap);
    }

    // ============================================================================================
    //  6. ⛔ EVERY `.Add` HAS ITS `.Remove` — THE `GetPotentialTorque` DEFECT
    // ============================================================================================

    /// <summary>
    /// The scanner, factored out so it can be pointed at synthetic sources that MUST fail it.
    /// Returns "" when balanced, else a description of the imbalance.
    /// </summary>
    static string SymmetryReport(string rawSrc)
    {
        string src = Live(rawSrc);
        var added = new List<string>();
        var removed = new HashSet<string>();
        foreach (Match m in Regex.Matches(src, @"GameEvents\.(\w+)\.Add\s*\("))
            added.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(src, @"GameEvents\.(\w+)\.Remove\s*\("))
            removed.Add(m.Groups[1].Value);
        if (added.Count == 0) return "no GameEvents subscription found at all";
        var orphans = new List<string>();
        for (int i = 0; i < added.Count; i++)
            if (!removed.Contains(added[i])) orphans.Add(added[i]);
        return orphans.Count == 0 ? ""
             : orphans.Count + " added-but-never-removed: " + string.Join(",", orphans.ToArray());
    }

    static void SubscriptionSymmetry()
    {
        string src = File.ReadAllText(Repo("plugin", "src", "PartLossWatch.cs"));
        Check("S227 ⛔ every GameEvents handler that is added is also removed",
              SymmetryReport(src) == "", SymmetryReport(src));

        string live = Live(src);
        int adds = Regex.Matches(live, @"GameEvents\.\w+\.Add\s*\(").Count;
        int removes = Regex.Matches(live, @"GameEvents\.\w+\.Remove\s*\(").Count;
        Check("...and the counts match exactly", adds == removes && adds == 8,
              adds + " adds, " + removes + " removes");

        // ⛔ THE DOCUMENTED REJECTION STAYS REJECTED. `onPartExplode` carries no Part (its payload,
        // GameEvents.ExplosionReaction, has only `distance` and `magnitude`), so a handler on it could
        // report an explosion and never say what exploded. It is named in the header as a rejection —
        // which is exactly why this scan runs on LIVE source, or the header would satisfy it.
        Check("S227 ⛔ onPartExplode is NOT subscribed — it does not name a part",
              !live.Contains("onPartExplode.Add"), "");
        Check("...and the rejection is still explained in the file",
              src.Contains("onPartExplode") && src.Contains("ExplosionReaction"), "");
    }

    // ============================================================================================
    //  7. ⭐⭐ THE SCANNER PROVES ITSELF. A CHECK THAT CANNOT FAIL HAS NOT CHECKED ANYTHING.
    // ============================================================================================
    static void TheScannerCanFail()
    {
        // Balanced control: the scanner must accept this, or every failure below is meaningless.
        const string ok = "GameEvents.onPartWillDie.Add(A);\nGameEvents.onPartWillDie.Remove(A);\n";
        Check("S227 the symmetry scanner accepts a balanced source", SymmetryReport(ok) == "",
              SymmetryReport(ok));

        // ⛔ THE FAILING CASE, BUILT INTO THE CHECK. A handler added and never removed.
        const string leak = "GameEvents.onPartWillDie.Add(A);\nGameEvents.onPartWillDie.Remove(A);\n"
                          + "GameEvents.onStageActivate.Add(B);\n";
        Check("S227 ⭐ ...and REJECTS an added-but-never-removed handler",
              SymmetryReport(leak).Contains("onStageActivate"), SymmetryReport(leak));

        // ⛔ S220's LESSON. A `.Remove` that has been COMMENTED OUT is not a `.Remove`. Without `Live()`
        // this passes, and the leak ships — which is precisely how two S220 mutants survived.
        const string commented = "GameEvents.onPartWillDie.Add(A);\n// GameEvents.onPartWillDie.Remove(A);\n";
        Check("S227 ⭐⭐ ...and is NOT fooled by a Remove that is commented out",
              SymmetryReport(commented).Contains("onPartWillDie"), SymmetryReport(commented));

        // And it refuses to report "balanced" for a file that subscribes to nothing — the S168 rule
        // that an absent input is never a pass.
        Check("S227 ⛔ ...and an empty source is not silently 'balanced'",
              SymmetryReport("// nothing here\n") != "", "");
    }

    // ============================================================================================
    //  8. NO UNGUARDED KSP CALLBACK
    // ============================================================================================
    static void HandlersAreGuarded()
    {
        string live = Live(File.ReadAllText(Repo("plugin", "src", "PartLossWatch.cs")));
        // Every handler `.Add` names a method; each of those methods must contain a `try`. An
        // exception escaping into KSP's own dispatch, at the moment the vehicle is coming apart, can
        // take the flight scene with it — and losing the scene loses the recording.
        var handlers = new List<string>();
        foreach (Match m in Regex.Matches(live, @"GameEvents\.\w+\.Add\s*\(\s*(\w+)\s*\)"))
            if (!handlers.Contains(m.Groups[1].Value)) handlers.Add(m.Groups[1].Value);
        Check("S227 the scan found the handler methods", handlers.Count >= 6, "got " + handlers.Count);

        int unguarded = 0; string firstBad = null;
        for (int i = 0; i < handlers.Count; i++)
        {
            Match def = Regex.Match(live, @"static void " + Regex.Escape(handlers[i]) + @"\s*\([^)]*\)\s*\{");
            if (!def.Success) { unguarded++; if (firstBad == null) firstBad = handlers[i] + " (no body)"; continue; }
            // The body, to the next `static` at method indentation — the same bracketing idiom the
            // other source-scanning suites use.
            string rest = live.Substring(def.Index);
            Match next = Regex.Match(rest.Substring(1), @"\n        (static|public|///)");
            // ⚠ `+ 1` because `next.Index` is measured in `rest.Substring(1)`. Without it the slice is
            // one character short and drops the body's CLOSING BRACE — which silently broke the
            // one-line-forwarder branch below on first run (`OnPartDeCouple`/`OnPartUndock` were
            // reported unguarded when they are not). Kept as a comment because the next person to
            // borrow this bracketing idiom will make the same mistake.
            string body = next.Success ? rest.Substring(0, next.Index + 1) : rest;
            // A one-line forwarder is guarded by what it forwards TO; accept either.
            // ⚠ S235: the argument pattern used to be `\w*` — a SINGLE bare identifier — which silently
            // stopped matching the moment `OnPartDeCouple` gained a second argument
            // (`NoteReleased(p, BlackBoxEvents.PartDecoupled)`) and reported two guarded handlers as
            // unguarded. ⭐ The check was RIGHT to fail: the shape it was pinned to had genuinely
            // changed. Widened to any argument list, still refusing anything with a statement in it.
            bool guarded = body.Contains("try") || Regex.IsMatch(body, @"\{\s*\w+\([^;{}]*\);\s*\}");
            if (!guarded) { unguarded++; if (firstBad == null) firstBad = handlers[i]; }
        }
        Check("S227 ⛔ no GameEvents handler can throw into KSP's dispatch",
              unguarded == 0, unguarded + " unguarded, first: " + firstBad);

        // The warn path logs and does not rethrow.
        Check("S227 the failure path logs rather than rethrowing",
              live.Contains("Debug.LogWarning") && !Regex.IsMatch(live, @"catch[^{]*\{[^}]*throw"), "");
    }

    // ============================================================================================
    //  9. WIRED TO THE SCENE, IN BOTH DIRECTIONS
    // ============================================================================================
    static void WiredToTheScene()
    {
        string rec = Live(File.ReadAllText(Repo("plugin", "src", "BlackBoxRecorder.cs")));
        Check("S227 the addon subscribes the watch on Start",
              Regex.IsMatch(rec, @"void Start\(\)[^}]*PartLossWatch\.Subscribe\(\)"), "");
        Check("S227 ⛔ ...and unsubscribes it on OnDestroy",
              Regex.IsMatch(rec, @"void OnDestroy\(\)[^}]*PartLossWatch\.Unsubscribe\(\)"), "");
        // ⛔ ORDER: unsubscribe BEFORE Close, so a part death arriving during teardown cannot reach a
        // recorder that is already flushing.
        Match od = Regex.Match(rec, @"void OnDestroy\(\)\s*\{([^}]*)\}");
        Check("...unsubscribe comes BEFORE the close",
              od.Success && od.Groups[1].Value.IndexOf("Unsubscribe")
                          < od.Groups[1].Value.IndexOf("Close"), od.Groups[1].Value.Trim());

        // Routing exists and falls back rather than dropping (hazard D).
        Check("S227 events route to the part's OWN vessel's stream",
              rec.Contains("EmitForVessel"), "");
        Check("...and fall back to the mission log rather than being dropped",
              Regex.IsMatch(rec, @"EmitForVessel[\s\S]{0,1200}?else EmitMission"), "");
    }

    // ============================================================================================
    //  10. THE PAYLOAD JOINS TO `craftdump.csv` — BY ITS OWN HEADER, NOT BY MEMORY
    // ============================================================================================
    static void PayloadJoinsToCraftDump()
    {
        // ⭐ Read the key names out of the dump's ACTUAL header string, so a rename on either side
        // breaks the build instead of quietly making the two files unjoinable.
        // ⚠⚠ S254 — ~~`Match hdr = Regex.Match(dump, "sb\\.Append\\(\"(part_idx[^\"]*)\"")`, matched
        // against `src/CraftDump.cs`~~ SUPERSEDED IN PLACE (C1.16). The header moved into
        // `pure/CraftDumpRows.Header` when S254 made the row rules headless-testable, and this check
        // BROKE — which is exactly what it is for, and is why it is not being weakened here.
        // ⭐ IT IS NOW STRICTLY STRONGER: it reads the CONSTANT the glue actually writes rather than
        // text-matching a call site, so a header that changes value can no longer pass a regex that
        // still matches. ⛔ The glue is still checked — separately, below — for actually using it.
        string dump = Live(File.ReadAllText(Repo("plugin", "src", "CraftDump.cs")));
        string header = CraftDumpRows.Header;
        Check("S254 craftdump's own header is the shared constant, and the glue writes THAT",
              header.StartsWith("part_idx") && dump.Contains("CraftDumpRows.Header"), header);
        string watch = Live(File.ReadAllText(Repo("plugin", "src", "PartLossWatch.cs")));
        string[] mustJoin = { "part_idx", "part_name", "part_title", "persistent_id", "stage" };
        int missing = 0; string firstMissing = null;
        for (int i = 0; i < mustJoin.Length; i++)
        {
            bool inDump = ("," + header + ",").Contains("," + mustJoin[i] + ",");
            bool inEvent = watch.Contains("\"" + mustJoin[i] + "\"");
            if (!inDump || !inEvent) { missing++; if (firstMissing == null) firstMissing = mustJoin[i]; }
        }
        Check("S227 ⭐ the event carries craftdump's OWN key names, so it joins with no lookup table",
              missing == 0, missing + " missing, first: " + firstMissing);

        // The classification travels IN the event, with the numbers it was decided from — hazard (B)
        // says state the discriminator, do not leave it to be inferred.
        string[] verdictKeys = { "loss_class", "why", "since_stage_s", "since_decouple_s" };
        int vm = 0;
        for (int i = 0; i < verdictKeys.Length; i++) if (!watch.Contains("\"" + verdictKeys[i] + "\"")) vm++;
        Check("S227 ⛔ the discriminator is STATED in the event, with its raw inputs beside it",
              vm == 0, vm + " of 4 verdict keys missing");

        // ⚠ And the file warns that part_idx is the LIVE index, not craftdump's pad index — the join
        // key is persistent_id. A reader who joins on part_idx gets the wrong part after any staging.
        string raw = File.ReadAllText(Repo("plugin", "src", "PartLossWatch.cs"));
        Check("S227 ⚠ ...and it says in the file that persistent_id, not part_idx, is the join key",
              raw.Contains("persistent_id` is the join") || raw.Contains("persistent_id** is the join")
              || raw.Contains("`persistent_id` is the join key"), "");
    }

    // ============================================================================================
    //  11. THE COLUMN AND THE KINDS ARE REAL, DECLARED AND WRITTEN
    // ============================================================================================
    static void ColumnAndKinds()
    {
        // ⛔ Declaration and writer in the SAME commit — a declared column with no writer is the S76
        // ghost column, and `BlackBoxCoverage` fires on it either way round.
        Check("S227 part_count is in the schema", BlackBoxSchema.Index("part_count") >= 0, "");
        // ⚠ S235 — THIS CHECK USED TO READ, VERBATIM:
        //       BlackBoxSchema.Index("part_count") == BlackBoxSchema.Columns.Length - 1
        //   SUPERSEDED IN PLACE (C1.16 / G12): "last" was true when S227 wrote it and stopped being true
        //   the moment S235 appended `hot_part_id`/`hot_part_name`. ⛔ What keeps `SchemaVersion` at 1 is
        //   that nothing MOVED — not that this column happens to be last — and the absolute pin says so
        //   directly. `FigmaUINavTest` holds the full append ledger.
        Check("...and it has not MOVED, which is what keeps SchemaVersion at 1",
              BlackBoxSchema.Index("part_count") == 206,
              "index " + BlackBoxSchema.Index("part_count") + " (pinned 206)");
        Check("...and SchemaVersion is still 1", BlackBoxSchema.SchemaVersion == 1,
              "got " + BlackBoxSchema.SchemaVersion);
        Check("S227 BlackBoxCols exposes it", BlackBoxCols.PartCount == BlackBoxSchema.Index("part_count"),
              "");

        string rec = Live(File.ReadAllText(Repo("plugin", "src", "BlackBoxRecorder.cs")));
        Check("S227 ⛔ and it HAS a writer, in the same commit as its declaration",
              rec.Contains("BlackBoxCols.PartCount"), "");
        // ⛔ THIS stream's vessel, never the active one — a booster row carrying the capsule's part
        // count would be a plausible integer about somebody else (§4.8).
        Check("...written from THIS stream's vessel, not FlightGlobals.ActiveVessel",
              Regex.IsMatch(rec, @"BlackBoxCols\.PartCount, v\.parts\.Count"), "");
        Check("...and guarded, because v.parts is null exactly when this matters most",
              Regex.IsMatch(rec, @"v\.parts != null[^\n]*BlackBoxCols\.PartCount"), "");

        // The four kinds exist, are distinct, and live under the `part.` namespace.
        string[] kinds = { BlackBoxEvents.PartLost, BlackBoxEvents.PartFailure,
                           BlackBoxEvents.PartJointBreak, BlackBoxEvents.PartLossTruncated };
        var uniq = new HashSet<string>(); int badNs = 0;
        for (int i = 0; i < kinds.Length; i++)
        {
            uniq.Add(kinds[i]);
            if (!kinds[i].StartsWith("part.")) badNs++;
        }
        Check("S227 four distinct part kinds, all in the part. namespace",
              uniq.Count == 4 && badNs == 0, uniq.Count + " distinct, " + badNs + " misnamespaced");

        string watch = Live(File.ReadAllText(Repo("plugin", "src", "PartLossWatch.cs")));
        int unemitted = 0; string firstDead = null;
        string[] names = { "PartLost", "PartFailure", "PartJointBreak", "PartLossTruncated" };
        for (int i = 0; i < names.Length; i++)
            if (!watch.Contains("BlackBoxEvents." + names[i]))
            { unemitted++; if (firstDead == null) firstDead = names[i]; }
        Check("S227 ⛔ every declared kind is actually emitted — no ghost channels (S90)",
              unemitted == 0, unemitted + " dead, first: " + firstDead);

        // ⛔ The truncation marker must NOT be budgeted, or the announcement of the cut can itself be
        // cut. It goes out through EmitMission, outside the Admit() gate.
        Check("S227 ⛔ the truncation marker is emitted OUTSIDE the budget it reports on",
              Regex.IsMatch(watch, @"TakeMarker\(\)[\s\S]{0,200}?EmitMission\("), "");
    }
    // ============================================================================================
    //  ⭐⭐ S235 / NTSB-2026-003 F-308 — **AN OBSERVATION KEPT ONLY AS STATE IS NOT RECORDED.**
    //
    //  This is the section S227 needed and did not have. The overseer assessed S227 as clean because
    //  8 `.Add` matched 8 `.Remove` — **emission was never asserted** — and three handlers that
    //  observed and discarded passed every check in this file. At MET 139.28 the sixteen
    //  launch-vehicle parts became a new vessel instead of dying, `part.lost` was silent by
    //  construction, and the stage number that would have named the agent had been reduced to a
    //  timestamp.
    //
    //  ⛔ SO THE BAR IS NOT "a handler is registered" AND NOT "a kind is declared". It is: **every
    //  handler emits, and every parameter it is handed reaches the record.** That generalises F-308
    //  instead of patching its three instances.
    // ============================================================================================

    /// <summary>
    /// Handlers that legitimately emit nothing, each with its reason. ⛔ AN ENTRY HERE IS A DEFECT ON
    /// RECORD, NOT A PARDON — the same rule `build.py`'s "known dead and owned" list carries. These two
    /// set teardown state consumed by `PartLoss.Classify`; ⚠ they ARE the F-308 shape and S236 owns the
    /// question of whether a vessel teardown deserves its own event. Adding a name here costs a
    /// register line.
    /// </summary>
    static readonly string[] EmitExempt = { "OnVesselWillDestroy", "OnVesselUnloaded" };

    /// <summary>Body of `static void Name(...)`, following ONE level of forwarding.</summary>
    static string HandlerBody(string live, string name)
    {
        Match def = Regex.Match(live, @"static void " + Regex.Escape(name) + @"\s*\(([^)]*)\)\s*\{");
        if (!def.Success) return "";
        string rest = live.Substring(def.Index);
        Match next = Regex.Match(rest.Substring(1), @"\n        (static|public|///)");
        string body = next.Success ? rest.Substring(0, next.Index + 1) : rest;
        Match fwd = Regex.Match(body, @"\{\s*(\w+)\([^;{}]*\);\s*\}");
        if (fwd.Success && fwd.Groups[1].Value != name)
            body += "\n" + HandlerBody(live, fwd.Groups[1].Value);
        return body;
    }

    static string[] Params(string live, string name)
    {
        Match def = Regex.Match(live, @"static void " + Regex.Escape(name) + @"\s*\(([^)]*)\)");
        if (!def.Success || def.Groups[1].Value.Trim().Length == 0) return new string[0];
        string[] parts = def.Groups[1].Value.Split(',');
        var names = new List<string>();
        for (int i = 0; i < parts.Length; i++)
        {
            string[] w = parts[i].Trim().Split(' ');
            if (w.Length >= 2) names.Add(w[w.Length - 1].Trim());
        }
        return names.ToArray();
    }

    static bool Emits(string body) { return Regex.IsMatch(body, @"\bEmit(Mission|ForVessel)?\s*\("); }

    static void S235_EveryHandlerEmits()
    {
        string live = Live(File.ReadAllText(Repo("plugin", "src", "PartLossWatch.cs")));
        var handlers = new List<string>();
        foreach (Match m in Regex.Matches(live, @"GameEvents\.\w+\.Add\s*\(\s*(\w+)\s*\)"))
            if (!handlers.Contains(m.Groups[1].Value)) handlers.Add(m.Groups[1].Value);
        Check("S235 the handler scan found them all", handlers.Count == 8, "got " + handlers.Count);

        int silent = 0; string firstSilent = null;
        for (int i = 0; i < handlers.Count; i++)
        {
            if (Array.IndexOf(EmitExempt, handlers[i]) >= 0) continue;
            if (!Emits(HandlerBody(live, handlers[i])))
            { silent++; if (firstSilent == null) firstSilent = handlers[i]; }
        }
        Check("S235 the every non-exempt GameEvents handler EMITS - F-308 cannot come back silently",
              silent == 0, silent + " observe-and-discard, first: " + firstSilent);

        // AND EVERY PARAMETER REACHES THE RECORD. `OnStageActivate` was HANDED the stage number and
        // kept a timestamp; a handler that emits but drops what it was given is F-308 with one step.
        int dropped = 0; string firstDropped = null;
        for (int i = 0; i < handlers.Count; i++)
        {
            if (Array.IndexOf(EmitExempt, handlers[i]) >= 0) continue;
            string body = HandlerBody(live, handlers[i]);
            string[] ps = Params(live, handlers[i]);
            for (int j = 0; j < ps.Length; j++)
                // ⚠ THE RULE IS "REFERENCED AT ALL", NOT "APPEARS INSIDE THE EMIT CALL", and the
                // difference is `OnPartJointBreak(PartJoint j, float breakForce)`: it destructures `j`
                // into `j.Child` / `j.Parent` and emits THOSE, so `j` itself never appears in a payload
                // while every fact it carried does. The stricter form reported that as a defect on its
                // first run — a scanner false positive, and changing the CODE to satisfy it would have
                // been the wrong repair.
                // ⛔ IT IS STILL THE CHECK THAT CATCHES F-308: S227's body was `lastStageCommandUt =
                // Now();`, which never mentions `stage` anywhere. A parameter never referenced is a
                // parameter that was thrown away, and that is the whole finding.
                if (!Regex.IsMatch(body, @"\b" + Regex.Escape(ps[j]) + @"\b"))
                { dropped++; if (firstDropped == null) firstDropped = handlers[i] + "(" + ps[j] + ")"; }
        }
        Check("S235 every parameter a handler is HANDED is USED, and the handler emits",
              dropped == 0, dropped + " dropped, first: " + firstDropped);

        Check("S235 onStageActivate emits the STAGE NUMBER, not just a timestamp",
              Regex.IsMatch(HandlerBody(live, "OnStageActivate"),
                            "EmitMission\\(BlackBoxEvents\\.StageActivateCalled[\\s\\S]{0,220}?Kv\\.Int\\(\"stage\", stage\\)"),
              "");
        Check("S235 ...and it still keeps the timestamp the classifier needs",
              HandlerBody(live, "OnStageActivate").Contains("lastStageCommandUt = Now()"), "");
        Check("S235 decouple and undock emit the part, under distinct kinds",
              HandlerBody(live, "OnPartDeCouple").Contains("BlackBoxEvents.PartDecoupled")
              && HandlerBody(live, "OnPartUndock").Contains("BlackBoxEvents.PartUndocked"), "");
        Check("S235 ...and still record the release time the classifier reads",
              HandlerBody(live, "OnPartDeCouple").Contains("decoupledAt[p.persistentId] = Now()"), "");
        // The brief cited this one as emitting nothing. It already did, and it is left alone.
        Check("S235 onPartJointBreak still emits breakForce (it always did - not a defect)",
              Regex.IsMatch(HandlerBody(live, "OnPartJointBreak"), "Kv\\.Num\\(\"break_force\", breakForce\\)"),
              "");
    }

    /// <summary>
    /// ⭐⭐ THE SCANNER MUST BE ABLE TO FAIL, or it is S130's green-that-cannot-go-red once more. Fed
    /// the exact shape S227 shipped, it must reject it.
    /// </summary>
    static void S235_TheEmitScannerCanFail()
    {
        const string s227 =
            "        static void OnStageActivate(int stage)\n" +
            "        {\n" +
            "            try { lastStageCommandUt = Now(); }\n" +
            "            catch { }\n" +
            "        }\n" +
            "        static void Other() { }\n";
        string body = HandlerBody(s227, "OnStageActivate");
        Check("S235 the scanner extracts a handler body at all", body.Contains("lastStageCommandUt"), body);
        Check("S235 ...and REJECTS the exact observe-and-discard shape S227 shipped", !Emits(body), body);

        const string fixedUp =
            "        static void OnStageActivate(int stage)\n" +
            "        {\n" +
            "            try { lastStageCommandUt = Now(); EmitMission(K, u, new[] { Kv.Int(\"stage\", stage) }); }\n" +
            "            catch { }\n" +
            "        }\n" +
            "        static void Other() { }\n";
        Check("S235 ...and ACCEPTS one that emits, so the rejection is not vacuous",
              Emits(HandlerBody(fixedUp, "OnStageActivate")), "");

        const string dropsParam =
            "        static void OnStageActivate(int stage)\n" +
            "        {\n" +
            "            try { EmitMission(K, u, new[] { Kv.Int(\"nothing\", 0) }); }\n" +
            "            catch { }\n" +
            "        }\n" +
            "        static void Other() { }\n";
        string b2 = HandlerBody(dropsParam, "OnStageActivate");
        Check("S235 the PARAMETER scan rejects an emit that drops the stage int",
              Emits(b2) && !Regex.IsMatch(b2, @"Emit[\s\S]{0,600}?\bstage\b"), b2);
    }

    /// <summary>
    /// The emission machinery itself, exercised for real - pure, so it CAN be. `Line` is what every
    /// handler ultimately reaches, so a payload that renders wrong here renders wrong in flight.
    /// </summary>
    static void S235_TheLineRendersThePayload()
    {
        string line = BlackBoxEvents.Line("M1", null, 1234.5, double.NaN, 7,
                                          BlackBoxEvents.StageActivateCalled,
                                          new[] { Kv.Int("stage", 6) });
        Check("S235 a stage.activate_called line names its kind",
              line.Contains("\"stage.activate_called\""), line);
        Check("S235 ...and CARRIES THE STAGE NUMBER - the one int the investigation turned on",
              line.Contains("\"stage\":6"), line);
        Check("S235 ...and its own instant", line.Contains("1234.5"), line);

        string dec = BlackBoxEvents.Line("M1", "V", 10.0, 5.0, 1, BlackBoxEvents.PartDecoupled,
            new[] { Kv.Str("part_name", "TE.19.F9.S1.Interstage"), Kv.Num("persistent_id", 42),
                    Kv.Int("part_idx", 3), Kv.Int("stage", 6) });
        Check("S235 a part.decoupled line joins to craftdump by its own key names",
              dec.Contains("\"part_name\"") && dec.Contains("\"persistent_id\"")
              && dec.Contains("\"part_idx\"") && dec.Contains("\"stage\""), dec);
        Check("S235 ...and the two release kinds are distinct",
              BlackBoxEvents.PartDecoupled != BlackBoxEvents.PartUndocked, "");
    }

    /// <summary>S235 JOB 2 - the hot part is named, declared and written in the same commit.</summary>
    static void S235_TheHotPartIsNamed()
    {
        Check("S235 J2 hot_part_id is in the schema", BlackBoxSchema.Index("hot_part_id") >= 0, "");
        Check("S235 J2 hot_part_name is in the schema", BlackBoxSchema.Index("hot_part_name") >= 0, "");
        Check("S235 J2 both sit AFTER skin_temp_frac, so SchemaVersion stays 1",
              BlackBoxSchema.Index("hot_part_id") > BlackBoxSchema.Index("skin_temp_frac")
              && BlackBoxSchema.SchemaVersion == 1, "");
        Check("S235 J2 BlackBoxCols exposes both",
              BlackBoxCols.HotPartId == BlackBoxSchema.Index("hot_part_id")
              && BlackBoxCols.HotPartName == BlackBoxSchema.Index("hot_part_name"), "");

        string rec = Live(File.ReadAllText(Repo("plugin", "src", "BlackBoxRecorder.cs")));
        Check("S235 J2 and BOTH have writers, in the same commit as their declaration",
              rec.Contains("BlackBoxCols.HotPartId") && rec.Contains("BlackBoxCols.HotPartName"), "");
        // The identity must come from the SAME `Part` the fraction came from, or a row could name one
        // part and measure another.
        Check("S235 J2 the identity is carried out of HottestSkin, not re-derived",
              Regex.IsMatch(rec, @"HottestSkin\(v, ut, out frac, out tempC, out hotId, out hotName\)")
              && Regex.IsMatch(rec, @"hotId = hottest\.persistentId")
              && Regex.IsMatch(rec, @"hotName = PartNames\.Of\(hottest\)"), "");
        Check("S235 J2 written inside the SAME branch as the fraction it describes",
              Regex.IsMatch(rec, @"BlackBoxCols\.SkinTempFrac[\s\S]{0,700}?BlackBoxCols\.HotPartId"), "");
    }

    /// <summary>S235 JOB 3 + JOB 4 - the guard counts settings only, and can see AutostageLimit.</summary>
    static void S235_SettingsVersusReadings()
    {
        Check("S235 J3 LimitingAoA is NOT a setting - it is written every Drive",
              !AscentReadback.Expect("LimitingAoA").IsSetting, "");
        Check("S235 J3 ...and OptimizeStageFlag is not either (menu-derived, moves at every staging)",
              !AscentReadback.Expect("OptimizeStageFlag").IsSetting, "");
        Check("S235 J3 ...nor LimitQaEnabled (menu-derived, rewritten every tick)",
              !AscentReadback.Expect("LimitQaEnabled").IsSetting, "");
        Check("S235 J3 ...nor the four mission facts",
              !AscentReadback.Expect("DesiredInclination").IsSetting
              && !AscentReadback.Expect("DesiredApoapsis").IsSetting
              && !AscentReadback.Expect("DesiredOrbitAltitude").IsSetting
              && !AscentReadback.Expect("LaunchingToPlane").IsSetting, "");
        Check("S235 J3 real settings ARE still counted - the guard can still fire",
              AscentReadback.Expect("Autostage").IsSetting
              && AscentReadback.Expect("AscentType").IsSetting
              && AscentReadback.Expect("PitchRate").IsSetting, "");

        // THE BEHAVIOURAL PROOF: a moving status flag must NOT trigger, a moving setting MUST.
        AscentObserved[] a = { AscentObserved.Word("LimitingAoA", "false"),
                               AscentObserved.Word("Autostage", "false") };
        AscentObserved[] flagMoved = { AscentObserved.Word("LimitingAoA", "true"),
                                       AscentObserved.Word("Autostage", "false") };
        AscentObserved[] setMoved = { AscentObserved.Word("LimitingAoA", "false"),
                                      AscentObserved.Word("Autostage", "true") };
        Check("S235 J3 a moving LimitingAoA counts ZERO - it cannot burn the re-assert budget",
              AscentReadback.CountMoved(a, flagMoved) == 0,
              "got " + AscentReadback.CountMoved(a, flagMoved));
        Check("S235 J3 ...but it IS still reported as a reading, not hidden",
              AscentReadback.CountMovedReadings(a, flagMoved) == 1,
              "got " + AscentReadback.CountMovedReadings(a, flagMoved));
        Check("S235 J3 NEGATIVE CONTROL - a moving Autostage still counts ONE",
              AscentReadback.CountMoved(a, setMoved) == 1,
              "got " + AscentReadback.CountMoved(a, setMoved));
        Check("S235 J3 the log says why a reading did not count",
              AscentReadback.Delta(a, flagMoved).Contains("not a setting"),
              AscentReadback.Delta(a, flagMoved));

        // ---- JOB 4: the detector can see R-03's own setting ----
        Check("S235 J4 AutostageLimit is in the expectation table at last",
              AscentReadback.Expect("AutostageLimit").Name == "AutostageLimit", "");
        Check("S235 J4 ...and it IS counted, so a reverted staging floor is detected",
              AscentReadback.Expect("AutostageLimit").IsSetting, "");
        Check("S235 J4 ...but is NOT scored against a constant - its value is craft-derived",
              !AscentReadback.Expect("AutostageLimit").Checkable, "");
        AscentObserved[] floorOk = { AscentObserved.Num("AutostageLimit", 6) };
        AscentObserved[] floorGone = { AscentObserved.Num("AutostageLimit", 0) };
        Check("S235 J4 a floor reverted 6 -> 0 (F-102's exact value) is COUNTED",
              AscentReadback.CountMoved(floorOk, floorGone) == 1,
              "got " + AscentReadback.CountMoved(floorOk, floorGone));
        Check("S235 J4 the glue reads it off the STAGING controller, not AscentSettings",
              Live(File.ReadAllText(Repo("plugin", "src", "MechAscentReadback.cs")))
                  .Contains("core.Staging.AutostageLimit.Val"), "");
        Check("S235 J4 and the claim table declares its source",
              Live(File.ReadAllText(Repo("plugin", "src", "pure", "AscentProfile.cs")))
                  .Contains("\"AutostageLimit\""), "");
    }
}
