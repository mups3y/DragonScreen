/*
 * S228 — NTSB-2026-002. THE CONFIGURATION WIPE, THE SCRUB THAT DID NOT EXIST, AND THE STAGING FLOOR.
 *
 * ⛔ WHAT THIS EXISTS TO STOP, AND IT HAS ALREADY HAPPENED ONCE, WITH CREW ABOARD.
 * `MechJebCore.FixedUpdate:552-566` clears the module cache and forces `OnLoad(null)` on its first
 * frame as master-and-focus — 68 ms after `Configure()` finished — reverting 17 boxes including
 * `_autostage -> True`, §B8's one sanctioned deviation. `MechConductor` configured once and never
 * re-checked. At MET 124.08 five stages fired in one frame (6 -> 1) at 33.9 km and Mach 5.6 with
 * 11,456 m/s of dv still owed; splashdown at ~134 m/s with no parachutes, because the drogues sit at
 * istg 2 and went with the cascade.
 *
 * ⭐⭐ THE CHECK THAT MATTERS MOST IS `ScrubRefusesTheIgnition`. The conductor ALREADY MEASURED this
 * failure — it printed "17 box(es) CHANGED ... Something re-seeded the module after the conductor
 * configured it" at the terminal count — and lit the engines 69 seconds later, because the finding was
 * wired to a log line and nothing else. Every assertion here about the scrub is paired with a NEGATIVE
 * CONTROL that ignites on the same inputs with the flag clear, so "it did not light" can never pass by
 * the sequence being broken in some other way.
 *
 * ⚠ WHAT THIS DOES *NOT* PROVE, AND IT IS THE HALF THAT KILLED THE VEHICLE.
 * ⛔ **No `MechJebCore` is instantiated here and no `OnLoad(null)` is ever called.** Both need KSP.
 * J1's real proof — "force the reload, watch the delta come back empty" — is a FLIGHT test, and this
 * suite deliberately does not pretend otherwise. What is proven headless is: the comparison that
 * detects a wipe is correct and cannot silently return zero; the re-assert is WIRED into the ascent
 * tick and rewrites through `Configure` (and therefore through the `Autostage` PROPERTY, not the
 * field); the scrub latch stops the sequence before any actuation; and the staging floor derived from
 * this repo's own `Crew-2.craft` lands exactly on the stage the recording says was fired.
 */
using DragonScreen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public static class ConfigWipeTest
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

    // ⛔ Text assertions match commented-out code. (Idiom: AutoTargetTest.Live, S220 — two of its
    // mutants survived for exactly this reason.) Strip line comments before asserting a statement
    // EXISTS. This file leans on it hard: the conductor's headers QUOTE the very code they replace.
    static string Live(string src)
    {
        string[] lines = src.Replace("\r\n", "\n").Split(new char[] { (char)10 });
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].TrimStart();
            if (t.StartsWith("//") || t.StartsWith("///")) continue;
            sb.Append(lines[i]).Append((char)10);
        }
        return sb.ToString();
    }

    public static int Run()
    {
        Console.WriteLine("ConfigWipeTest (S228 / NTSB-2026-002: the wipe is detected, the count can scrub, the cascade is floored)");
        checks = 0; failures = 0;

        J1_TheWipeIsDetectable();
        J1_TheReassertIsWired();
        J2_ScrubRefusesTheIgnition();
        J2_ScrubIsTerminalAndWired();
        J3_TheFloorFromTheRealCraft();
        J3_TheFloorIsWiredAndIndependent();
        TheAutostagePropertyContradictionStaysResolved();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + (failures == 0
                             ? "   (every scrub assertion is paired with an igniting negative control)"
                             : ""));
        return failures;
    }

    // ---- small builders -------------------------------------------------------------------------
    static AscentObserved[] Boxes(params string[] nameThenValue)
    {
        var list = new List<AscentObserved>();
        for (int i = 0; i + 1 < nameThenValue.Length; i += 2)
            list.Add(AscentObserved.Word(nameThenValue[i], nameThenValue[i + 1]));
        return list.ToArray();
    }

    // ============================================================================================
    //  J1 (R-01) — THE WIPE IS DETECTABLE, AND THE DETECTOR CANNOT SILENTLY RETURN ZERO
    // ============================================================================================
    static void J1_TheWipeIsDetectable()
    {
        // Use real box names from the expectation table, so this exercises the same lookup the flight
        // path does rather than a private fixture vocabulary.
        string n1 = AscentReadback.Expected[0].Name;
        string n2 = AscentReadback.Expected[1].Name;

        AscentObserved[] wrote = Boxes(n1, "false", n2, "PSG");
        AscentObserved[] same  = Boxes(n1, "false", n2, "PSG");
        AscentObserved[] wiped = Boxes(n1, "true",  n2, "PSG");   // the `_autostage -> True` shape

        Check("S228 J1 an unchanged read-back counts ZERO moved boxes",
              AscentReadback.CountMoved(wrote, same) == 0,
              "got " + AscentReadback.CountMoved(wrote, same));
        // ⭐ THE FAILING CASE. A detector that always answered 0 would pass the line above and would
        // have let 2026-09-08 happen exactly as it did.
        Check("S228 J1 ⭐ a reverted box IS counted — the detector can go non-zero",
              AscentReadback.CountMoved(wrote, wiped) == 1,
              "got " + AscentReadback.CountMoved(wrote, wiped));
        Check("...and two reverted boxes count two",
              AscentReadback.CountMoved(wrote, Boxes(n1, "true", n2, "CLASSIC")) == 2,
              "got " + AscentReadback.CountMoved(wrote, Boxes(n1, "true", n2, "CLASSIC")));

        // ⛔ THE COUNT AND ITS OWN EXPLANATION SHARE ONE COMPARISON. Two copies of "did this box move"
        // is how a log that says 17 ends up beside a guard that says 0.
        string delta = AscentReadback.Delta(wrote, wiped);
        Check("S228 J1 ⛔ the count and the rendered Delta agree, because they share one loop",
              delta.Contains("1 SETTING(S) CHANGED") && delta.Contains(n1), delta);
        Check("...and an empty delta says nothing moved",
              AscentReadback.Delta(wrote, same).Contains("NOTHING MOVED"), "");
        // A box neither reading could read is not a change — it is an absent measurement.
        Check("S228 J1 an unread box on both sides is not counted as moved",
              AscentReadback.CountMoved(Boxes(), Boxes()) == 0, "");
    }

    static void J1_TheReassertIsWired()
    {
        string src = Live(File.ReadAllText(Repo("plugin", "src", "MechConductor.cs")));

        // ⛔ THE DEFECT ITSELF: `Configure` ran once and nothing re-checked. The `else` is the fix.
        Check("S228 J1 ⛔ the ascent tick re-checks instead of configuring once and forgetting",
              Regex.IsMatch(src, @"if \(!configured\) Configure\(v\);\s*\n\s*else ReassertIfWiped\(v\);"),
              "");
        Check("S228 J1 the re-assert measures with the read-back, not with a guess",
              Regex.IsMatch(src, @"ReassertIfWiped[\s\S]{0,2000}?AscentReadback\.CountMoved\("), "");
        // ⭐ It rewrites through the FULL Configure — so every box the reload touched goes back, not a
        // hand-picked subset, and `AtConfigure` is re-taken so R-02 measures the right question.
        Check("S228 J1 ⭐ it re-runs the FULL Configure rather than patching selected boxes",
              Regex.IsMatch(src, @"ReassertIfWiped[\s\S]{0,2500}?configured = false;\s*\n\s*Configure\(v\);"),
              "");
        // ⛔ Bounded: something re-seeding every frame must not be papered over forever.
        Check("S228 J1 ⛔ the re-assert is BOUNDED, so a continuous re-seed reaches R-02",
              Regex.IsMatch(src, @"reassertCount >= MaxReasserts") && src.Contains("const int MaxReasserts"),
              "");
        // ⛔ Non-fatal: this runs every ascent tick.
        Check("S228 J1 ⛔ a failure to check cannot throw the conductor out of a live ascent",
              Regex.IsMatch(src, @"ReassertIfWiped[\s\S]{0,3000}?catch \(Exception e\)"), "");
        // The state is reset per engagement, or a scrub would outlive the flight that caused it.
        Check("S228 J1 the re-assert state is cleared when the conductor resets",
              src.Contains("configScrub = false; reassertCount = 0;"), "");
    }

    // ============================================================================================
    //  J2 (R-02) — ⭐⭐ THE SCRUB. INJECT A DELTA, ASSERT NOTHING LIGHTS AND NO CLAMP MOVES.
    // ============================================================================================

    /// <summary>The pad, one tick before ignition, with everything the sequence needs to say GO.</summary>
    static AscentInputs ReadyToLight()
    {
        AscentInputs s = AscentInputs.Nominal();
        s.LaunchCommanded = true;
        s.GuidanceReady = true;
        s.WindowRequired = false;
        s.WindowArmed = false;
        s.SecondsToWindowS = 0.0;
        s.SinceStepS = 0.0;
        return s;
    }

    static void J2_ScrubRefusesTheIgnition()
    {
        // ⭐ THE NEGATIVE CONTROL FIRST. If this does not ignite, every "it did not light" below is
        // worthless — the sequence could be refusing for some entirely different reason.
        AscentDecision go = AscentSequence.Step(ReadyToLight(), AscentStep.Idle);
        Check("S228 J2 ⭐ NEGATIVE CONTROL — with no delta these inputs DO light the octaweb",
              go.Next == AscentStep.Ignition && go.Act == AscentAct.IgniteStageOne,
              go.Next + "/" + go.Act);

        // ⛔ NOW INJECT THE DELTA. Same inputs, one flag.
        AscentInputs scrub = ReadyToLight();
        scrub.ConfigScrub = true;
        AscentDecision d = AscentSequence.Step(scrub, AscentStep.Idle);
        Check("S228 J2 ⛔⛔ a remaining delta SCRUBS the count",
              d.Next == AscentStep.Scrubbed, d.Next.ToString());
        Check("S228 J2 ⛔ NOTHING IS LIT — no ignition act is emitted",
              d.Act != AscentAct.IgniteStageOne, d.Act.ToString());
        Check("S228 J2 ⛔ NO CLAMP IS RELEASED",
              d.Act != AscentAct.ReleaseHoldDowns, d.Act.ToString());
        Check("S228 J2 ⛔ ...in fact no act at all is emitted",
              d.Act == AscentAct.None, d.Act.ToString());
        Check("...and the reason says why, so the log is not a mystery",
              d.Reason.Contains("SCRUB"), d.Reason);

        // ⛔ THE SCRUB OUTRANKS EVERY OTHER PAD HOLD, and the order is the point: a misconfigured
        // vehicle is not something a crew GO or an arriving launch window can make safe.
        AscentInputs noGo = ReadyToLight();
        noGo.LaunchCommanded = false; noGo.ConfigScrub = true;
        Check("S228 J2 the scrub is decided AHEAD of the crew's GO",
              AscentSequence.Step(noGo, AscentStep.Idle).Next == AscentStep.Scrubbed, "");
        AscentInputs counting = ReadyToLight();
        counting.WindowRequired = true; counting.WindowArmed = true;
        counting.SecondsToWindowS = 600.0; counting.ConfigScrub = true;
        Check("...and ahead of the launch-window countdown",
              AscentSequence.Step(counting, AscentStep.Idle).Next == AscentStep.Scrubbed, "");
        AscentInputs noGuide = ReadyToLight();
        noGuide.GuidanceReady = false; noGuide.ConfigScrub = true;
        Check("...and ahead of the S214 guidance hold",
              AscentSequence.Step(noGuide, AscentStep.Idle).Next == AscentStep.Scrubbed, "");
    }

    static void J2_ScrubIsTerminalAndWired()
    {
        // ⛔ ABSORBING, and it does NOT clear itself if the delta goes away: a configuration that
        // silently repaired itself is evidence something is still writing to it.
        AscentInputs clean = ReadyToLight();          // ConfigScrub deliberately FALSE
        AscentDecision d = AscentSequence.Step(clean, AscentStep.Scrubbed);
        Check("S228 J2 ⛔ Scrubbed is absorbing even once the delta is gone",
              d.Next == AscentStep.Scrubbed && d.Act == AscentAct.None, d.Next + "/" + d.Act);

        // ⛔ No act is EVER emitted from Scrubbed, under any inputs. Swept rather than argued.
        int acted = 0;
        for (int k = 0; k < 32; k++)
        {
            AscentInputs s = ReadyToLight();
            s.ConfigScrub = (k & 1) != 0;
            s.LaunchCommanded = (k & 2) != 0;
            s.GuidanceReady = (k & 4) != 0;
            s.S1ThrustN = (k & 8) != 0 ? 8227000.0 : 0.0;
            s.S1MaxThrustN = 8227000.0;
            s.S1LitCount = (k & 16) != 0 ? 9 : 0;
            s.SinceStepS = k;
            if (AscentSequence.Step(s, AscentStep.Scrubbed).Act != AscentAct.None) acted++;
        }
        Check("S228 J2 ⛔ across 32 input combinations Scrubbed emits NO act, ever",
              acted == 0, acted + " emitted an act");

        // ⛔ And the mission plan cannot walk past it, exactly as it cannot past Safed.
        Check("S228 J2 the mission plan cannot advance past a scrubbed count",
              !AscentSequence.CanAdvancePlan(AscentStep.Scrubbed), "");
        // ⚠ Scrubbed and Safed are DIFFERENT FACTS and must not be merged: one was never lit, the
        // other was lit and failed.
        Check("S228 J2 ⚠ Scrubbed is a distinct step from Safed",
              AscentStep.Scrubbed != AscentStep.Safed, "");

        string src = Live(File.ReadAllText(Repo("plugin", "src", "MechConductor.cs")));
        Check("S228 J2 the terminal count sets the latch from a MEASURED delta",
              Regex.IsMatch(src, @"AtTerminalCount[\s\S]{0,600}?CountMoved\([\s\S]{0,200}?moved > 0[\s\S]{0,200}?configScrub = true"),
              "");
        Check("S228 J2 ⛔ and the latch actually reaches the sequence",
              src.Contains("s.ConfigScrub = configScrub;"), "");
    }

    // ============================================================================================
    //  J3 (R-03) — THE FLOOR, AGAINST THIS REPO'S OWN CRAFT FILE
    // ============================================================================================
    static void J3_TheFloorFromTheRealCraft()
    {
        // ⭐ NOT A FIXTURE. `docs/reference/Crew-2.craft` is the tier-1 in-repo source, and the ladder
        // it produces is the one the recording flew.
        string craft = File.ReadAllText(Repo("docs", "reference", "Crew-2.craft"));
        var parts = new List<StagePart>();
        foreach (string block in Regex.Split(craft, "\nPART\\s*\n\\{"))
        {
            Match nm = Regex.Match(block, @"\bpart\s*=\s*([^\s_]+)");
            Match ist = Regex.Match(block, @"\bistg\s*=\s*(-?\d+)");
            if (nm.Success && ist.Success)
                parts.Add(StagePart.Of(int.Parse(ist.Groups[1].Value), nm.Groups[1].Value));
        }
        Check("S228 J3 the craft file parsed", parts.Count > 20, "got " + parts.Count + " parts");

        int floor = StagingFloor.For(parts.ToArray());
        // The interstage sits at istg 6 — and the recording's fatal line is `from: 6 -> to: 1`.
        Check("S228 J3 ⭐ the floor derived from Crew-2.craft is the S1/S2 separation stage (6)",
              floor == 6, "got " + floor);

        // ⛔ THE ACTUAL CLAIM: the stage the recording says was fired cannot be fired.
        Check("S228 J3 ⛔⛔ stage 6 — the S1/S2 separation that discarded the launch vehicle — is BLOCKED",
              !StagingFloor.WouldStage(6, floor), "");
        Check("...and so is everything below it: S2 engine (5)",
              !StagingFloor.WouldStage(5, floor), "");
        Check("...the S2 tank and Dragon decoupler (4)", !StagingFloor.WouldStage(4, floor), "");
        Check("...the trunk (3)", !StagingFloor.WouldStage(3, floor), "");
        // ⭐ THE ONES THAT KILLED THE CREW. Drogues at istg 2, mains at istg 1, both expended at 33.9 km.
        Check("S228 J3 ⭐⭐ the DROGUES (2) cannot be fired by a cascade",
              !StagingFloor.WouldStage(2, floor), "");
        Check("S228 J3 ⭐⭐ nor the MAINS (1) — the recording splashed down at ~134 m/s with neither",
              !StagingFloor.WouldStage(1, floor), "");

        // ⛔ AND IT MUST NOT STRAND THE VEHICLE ON THE PAD. Ignition and liftoff stay reachable.
        Check("S228 J3 ⛔ NEGATIVE CONTROL — octaweb ignition (8) is still permitted",
              StagingFloor.WouldStage(8, floor), "");
        Check("...and the hold-down release / liftoff (7) is still permitted",
              StagingFloor.WouldStage(7, floor), "");

        // ⛔ UNDERIVABLE CLAMPS SHUT, IT DOES NOT OPEN.
        Check("S228 J3 ⛔ no interstage -> autostaging forbidden outright, not permitted",
              StagingFloor.For(new[] { StagePart.Of(3, "TE.18.DRAGONV2.POD") }) == StagingFloor.ForbidAll,
              "");
        Check("...an empty part list too", StagingFloor.For(new StagePart[0]) == StagingFloor.ForbidAll, "");
        Check("...and a null list", StagingFloor.For(null) == StagingFloor.ForbidAll, "");
        Check("...and ForbidAll really does forbid the top of any real ladder",
              !StagingFloor.WouldStage(20, StagingFloor.ForbidAll), "");
        Check("S228 J3 a null part name does not throw",
              StagingFloor.For(new[] { StagePart.Of(2, null) }) == StagingFloor.ForbidAll, "");
        // ⚠ Several interstages -> the SAFEST reading, which is the highest (stops earliest).
        Check("S228 J3 ⚠ with two interstages the HIGHER stage wins — stop the cascade earliest",
              StagingFloor.For(new[] { StagePart.Of(2, "TE.19.F9.S1.Interstage"),
                                       StagePart.Of(6, "TE.19.F9.S1.Interstage") }) == 6, "");
        // A negative stage cannot bound anything, so it is underivable rather than a limit.
        Check("S228 J3 a negative interstage stage is treated as underivable, not as a floor",
              StagingFloor.For(new[] { StagePart.Of(-1, "TE.19.F9.S1.Interstage") }) == StagingFloor.ForbidAll,
              "");
    }

    static void J3_TheFloorIsWiredAndIndependent()
    {
        string src = Live(File.ReadAllText(Repo("plugin", "src", "MechConductor.cs")));
        Check("S228 J3 the floor is applied from Configure, beside the Autostage write",
              Regex.IsMatch(src, @"a\.Autostage = false;[\s\S]{0,200}?ApplyStagingFloor\(v\);"), "");
        Check("S228 J3 ⛔ it writes MechJeb's own AutostageLimit",
              Regex.IsMatch(src, @"AutostageLimit\.Val = floor"), "");
        Check("S228 J3 the floor is DERIVED from the live parts, never a literal",
              src.Contains("StagingFloor.For(parts.ToArray())")
              && Regex.IsMatch(src, @"StagePart\.Of\(p\.inverseStage, PartNames\.Of\(p\)\)"), "");
        // ⛔ A literal 6 in the glue would be right for Crew-2 and silently wrong for the next craft.
        Check("S228 J3 ⛔ no hardcoded stage number is written into AutostageLimit",
              !Regex.IsMatch(src, @"AutostageLimit\.Val\s*=\s*\d"), "");
        Check("S228 J3 ⛔ setting the floor cannot throw the conductor out of the ascent",
              Regex.IsMatch(src, @"ApplyStagingFloor[\s\S]{0,2000}?catch \(Exception e\)"), "");
        // ⭐ Independent of Autostage by construction: R-03 must survive R-01 and R-02 both being wrong.
        Check("S228 J3 ⭐ the floor does not read Autostage at all",
              !Regex.IsMatch(src, @"ApplyStagingFloor[\s\S]{0,1500}?\bAutostage\b\s*[=!]"), "");
    }

    // ============================================================================================
    //  THE CONTRADICTION THE BRIEF RESOLVED — AND IT MUST STAY RESOLVED
    // ============================================================================================
    static void TheAutostagePropertyContradictionStaysResolved()
    {
        // NTSB asked for the `_autostage` FIELD; `MechConductor` says the PROPERTY. Both are right, and
        // the property is what we use, because its setter also fixes `Core.Staging.Users`.
        string src = File.ReadAllText(Repo("plugin", "src", "MechConductor.cs"));
        string live = Live(src);
        Check("S228 ⛔ the conductor writes the Autostage PROPERTY",
              Regex.IsMatch(live, @"a\.Autostage = false;"), "");
        Check("S228 ⛔ ...and never assigns the _autostage FIELD directly",
              !Regex.IsMatch(live, @"\b_autostage\s*="), "");
        // The existing comment is load-bearing and was not to be edited away.
        Check("S228 the reason the property is used is still recorded in the file",
              src.Contains("`Autostage` goes through the PROPERTY, never the `_autostage` field"), "");

        // ⛔ AND THE VENDORED TREE IS UNTOUCHED (§B12.1). The fix is entirely on our side of the seam.
        Check("S228 ⛔ the vendored MechJeb source still carries the property with its side effects",
              File.ReadAllText(Repo("plugin", "mech", "MechJeb2", "MechJebModuleAscentSettings.cs"))
                  .Contains("Core.Staging.Users.Remove(AscentAutopilot)"), "");
    }
}
