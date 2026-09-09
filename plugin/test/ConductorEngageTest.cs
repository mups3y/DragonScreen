/*
 * S219 JOB 3 — PROVE THE ENGAGE, PROVE THE ACTIVATION, AND ESTABLISH THE T-0 STAGING.
 *
 * THE THREE DEFECTS THIS SUITE EXISTS TO CATCH, in the owner's own words (2026-09-07):
 *
 *   (1) "otherwise it will sit there ready to go but do nothing."
 *       ⛔ A MODULE CONFIGURED AND NEVER ENGAGED. The brief: *"A test that FAILS when a module is
 *       configured but not engaged. That is the defect he described and it must not be discoverable
 *       only in flight."*
 *
 *   (2) "or mechjeb will throttle up but never activate the engines."
 *       ⛔ THE IGNITION CHAIN. bind → named parts resolve → activation commanded → thrust observed,
 *       end to end, headless.
 *
 *   (3) `StageManager.ActivateNextStage()` at T-0 — ESTABLISHED, NOT RACED.
 *
 * ---- ⭐ HOW (1) IS TESTABLE AT ALL, GIVEN THE GLUE CANNOT BE COMPILED HEADLESSLY ----
 * `src/MechConductor.cs` needs KSP to compile, so no headless test can CALL it. But "this module is
 * configured and never engaged" is a claim about TEXT, and this repo already proves claims about text
 * against real files — `MechHostTest` reads the pinned MechJeb tree, `RendezvousOpsTest` reads the
 * vendored Operation classes. Same idiom, same reason: these are exactly the claims that rot silently.
 *
 * ⚠ WHAT THIS DOES NOT PROVE. That the engage WORKS in the game — that a `Users.Add` on a live core
 * takes the vehicle. That is glass time, and it is the checklist on this register line, not here.
 */
using DragonScreen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public static class ConductorEngageTest
{
    static int checks, failures;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + (detail == "" ? "" : "   " + detail)); }
    }

    // plugin/build/DragonScreenTest.exe -> "../.." = the repo root. The MechHostTest idiom.
    static string Repo(params string[] parts)
    {
        var bits = new List<string> {
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", ".." };
        bits.AddRange(parts);
        return Path.GetFullPath(Path.Combine(bits.ToArray()));
    }

    // ⛔ TEXT ASSERTIONS MATCH COMMENTED-OUT CODE, AND THAT IS NOT A THEORETICAL FLAW. Two of
    // `AutoTargetTest`'s own mutants SURVIVED on first run for exactly this reason: commenting a
    // statement out left the text in place and every match still passed. It cuts both ways here —
    // S222b's checks assert that writes are ABSENT, and `Configure`'s own comment block NAMES every
    // write it dropped, so an un-stripped search would fail on the explanation of the fix. Strip line
    // comments before matching, for presence and for absence alike. (Idiom: AutoTargetTest, S220.)
    static string Live(string src)
    {
        string[] lines = src.Replace(((char)13).ToString() + ((char)10).ToString(),
                                     ((char)10).ToString()).Split(new char[] { (char)10 });
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].TrimStart();
            if (t.StartsWith("//")) continue;
            sb.Append(lines[i]).Append((char)10);
        }
        return sb.ToString();
    }

    /// <summary>
    /// ⭐ S222b — does this code ASSIGN to a MechJeb setting called <paramref name="field"/>? Matches the
    /// two shapes a settings write takes — `a.Name = …` and `a.Name.Val = …` — and deliberately NOT a
    /// read, a comparison, or a mention in a log string. ⛔ The distinction is the point: `MirrorTheMenus`
    /// READS `MinDeltaV` and `LastStage` because the PSG-settings window's own loop reads them, and S219
    /// wrote both "because PvgPreflight reads them", which is the confusion this separates.
    /// </summary>
    static bool Assigns(string code, string field)
    {
        return Regex.IsMatch(code, @"\." + Regex.Escape(field) + @"(\.Val)?\s*=[^=]");
    }

    public static int Run()
    {
        Console.WriteLine("ConductorEngageTest (S219 JOB 3: configured-AND-engaged, the ignition chain, the T-0 staging)");
        checks = failures = 0;

        ConfiguredMeansEngaged();
        TheIgnitionChain();
        TheTZeroStaging();
        NoTuningWrites();               // S222b
        EveryPhaseEngagesAndWarps();    // S222b

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures == 0 ? 0 : 1;
    }

    // =====================================================================================
    // 1. ⛔ "IT WILL SIT THERE READY TO GO BUT DO NOTHING" — CONFIGURED **AND** ENGAGED
    // =====================================================================================
    //
    // Every autopilot module the conductor writes settings into must ALSO be handed the vehicle, and
    // must ALSO be given back. Three separate failures, each real:
    //   • configured, never engaged  -> the owner's sentence: ready to go, doing nothing;
    //   • engaged, never released    -> the module keeps MechJeb's attitude/thrust/RCS user pools on a
    //                                   core that has stopped driving — a vehicle nobody is steering
    //                                   and nobody has given back;
    //   • engaged without authority  -> `OnModuleEnabled` grabs those pools while `Drive` never fires,
    //                                   because `MechJebCore.FixedUpdate` only drives the master core.
    static void ConfiguredMeansEngaged()
    {
        string src = File.ReadAllText(Repo("plugin", "src", "MechConductor.cs"));

        // ⭐⭐ THE CORE CHECK, ONE MODULE AT A TIME AND **SCOPED TO ITS OWN RUNNER**. For each: it is
        // CONFIGURED, it is ENGAGED, and the configure happens FIRST — "set the options, then ENGAGE",
        // which is the owner's central point. Scoping matters: `ap.Users.Add(Owner)` is the idiom for
        // three different modules, so a file-wide search would happily match the wrong one's engage.
        //
        // ⛔ ADDING A MODULE HERE IS THE POINT. A fifth autopilot that is configured and never engaged
        // fails this suite instead of being discovered on the pad.
        SetThenEngage(src, "the rendezvous autopilot", "RunRendezvousAutopilot",
                      "ap.desiredDistance.Val = RendezvousOps.AutopilotHandoffRangeM",
                      "ap.Users.Add(Owner)");
        SetThenEngage(src, "the docking autopilot", "RunDocking",
                      "ap.forceRol = true", "ap.Users.Add(Owner)");
        SetThenEngage(src, "SmartASS", "SetSmartAss",
                      "sa.mode = MuMech.MechJebModuleSmartASS.Target2Mode", "sa.Engage()");
        SetThenEngage(src, "the node executor", "BurnNode",
                      "SetNodeRcsOnly(v)", "ne.ExecuteOneNode(Owner)");

        // The ascent is the one whose settings live in their own method, so the ordering claim is
        // "Configure is CALLED before the engage", which is the same guarantee one level up.
        string ascent = Body(src, "RunAscent");
        int cfgCall = ascent.IndexOf("Configure(v)", StringComparison.Ordinal);
        int engAsc = ascent.IndexOf("ap.Users.Add(Owner)", StringComparison.Ordinal);
        Check("S219: the PVG ascent autopilot is CONFIGURED", cfgCall >= 0, "");
        Check("S219: the PVG ascent autopilot is ENGAGED (not left ready-to-go doing nothing)",
              engAsc >= 0, "");
        Check("S219: ...and Configure runs before the engage", cfgCall >= 0 && engAsc > cfgCall,
              "configure@" + cfgCall + " engage@" + engAsc);
        // ⚠ S250: ~~Configure writes the boxes a user WOULD type (AscentType, Autostage)~~ —
        // both are RO's own defaults and are no longer written. What must still be true is that
        // Configure writes SOMETHING before the engage, and the owner-ruled ascent numbers are it.
        Check("S250: ...and Configure still writes the owner-ruled boxes before the engage",
              Body(src, "Configure").Contains("a.PitchRate.Val = AscentProfile.PitchRateDegPerS")
              && Body(src, "Configure").Contains("core.Thrust.LimitDynamicPressure = true"), "");

        // ⭐⭐ S222b — **THE TWO MENU-DERIVED BOXES ARE SET BEFORE THE ENGAGE, NOT ONLY AFTER IT.**
        // "Set the options, THEN engage" applies to these as much as to the module's own settings:
        // `LimitQaEnabled` and `OptimizeStageFlag` are written by MechJeb's own window draw, which T15b
        // suppressed, so the conductor has to stand in — and the module's FIRST `Drive` must already
        // see them, or the first `SetTarget` goes out with `OptimizeStageFlag` false, which is exactly
        // the state that makes RO's 110 km attach altitude a live terminal constraint.
        int mirror = ascent.IndexOf("MirrorTheMenus(v)", StringComparison.Ordinal);
        Check("S222b: the menu-derived settings are mirrored in RunAscent", mirror >= 0, "");
        Check("S222b: ...BEFORE the engage, so the module's first Drive sees them",
              mirror >= 0 && engAsc > mirror, "mirror@" + mirror + " engage@" + engAsc);
        Check("S222b: ...and re-derived every tick, because the menu recomputes it every frame",
              Count(ascent, "MirrorTheMenus(v)") == 2,
              "found " + Count(ascent, "MirrorTheMenus(v)") + " call(s); want one pre-engage + one per tick");
        Check("S222b: ...and it copies the menu's EXPRESSION for LimitQaEnabled, not a chosen value",
              Body(Live(src), "MirrorTheMenus")
                  .Contains("a.LimitQaEnabled = a.AscentType == MuMech.AscentType.PSG"), "");
        // ⛔ THESE THREE MATCH ON LIVE CODE ONLY, AND A MUTANT PROVED WHY. `MirrorTheMenus`' own
        // comments NAME `AscentProfile.OptimizeStageFlagFor` while explaining what it does, so an
        // un-stripped search passed even with the call replaced by a hardcoded `true` — the exact
        // failure S220 found ("a check that passes on code that has been switched off proves
        // nothing"). The surrounding checks deliberately keep the raw `src`: several of them assert
        // that a SUPERSEDED-IN-PLACE comment is still present, which stripping would delete.
        string mirrorBody = Body(Live(src), "MirrorTheMenus");
        Check("S222b: ...and derives OptimizeStageFlag from the live stage table, via the pure loop",
              mirrorBody.Contains("AscentProfile.OptimizeStageFlagFor")
              && mirrorBody.Contains("AscentProfile.OptimizeStageFlagApplies"), "");
        Check("S222b: ⛔ ...and it is DERIVED, never asserted as a literal",
              !mirrorBody.Contains("bool want = true") && !mirrorBody.Contains("bool want = false"), "");
        Check("S222b: ⛔ ...and writes NOTHING when the stage table is empty, exactly as the menu does",
              mirrorBody.Contains("VacStats.Count == 0"), "");

        // ⛔ AUTHORITY BEFORE ENGAGEMENT. `MechJebCore.FixedUpdate` only drives the MASTER core, so a
        // module enabled on a core without drive authority has `OnModuleEnabled` run — taking the
        // attitude and thrust pools — while `Drive` never fires. Every runner must authorise first.
        string[] runners = { "RunAscent", "RunRendezvousAutopilot", "RunDocking", "RunAttitudeHold", "BurnNode" };
        for (int i = 0; i < runners.Length; i++)
            Check("S219: " + runners[i] + " takes drive authority before it engages anything",
                  Body(src, runners[i]).Contains("AuthorizeDrive(true)"), "");

        // ⛔ AND STAND-DOWN MUST RELEASE EVERY ONE OF THEM, or a phase change leaves a module holding
        // the vehicle. `StandDown` is the single funnel; these are the four exits it must name.
        string standDown = Body(src, "StandDown");
        Check("S219: StandDown releases the ascent autopilot",
              standDown.Contains("ap.Users.Remove(Owner)"), "");
        Check("S219: StandDown releases the docking autopilot",
              standDown.Contains("ReleaseDocking"), "");
        Check("S219: StandDown releases the rendezvous autopilot",
              standDown.Contains("ReleaseRendezvous"), "");
        Check("S219: StandDown releases SmartASS and aborts the node executor",
              standDown.Contains("ReleaseAttitude") && standDown.Contains("Node.Abort"), "");
        Check("S219: ...and gives the drive authority back",
              standDown.Contains("AuthorizeDrive(false)"), "");

        // ⭐ §7.5's OWN SEQUENCE, in the order the menu writes it. Not a style point: the menu writes
        // `DesiredInclination` AFTER `StartCountdown`, and following the source is the whole directive.
        int plane = src.IndexOf("a.LaunchingToPlane = true", StringComparison.Ordinal);
        int countdown = src.IndexOf("ap.StartCountdown(", StringComparison.Ordinal);
        int inclination = src.IndexOf("a.DesiredInclination.Val = window.InclinationDeg", StringComparison.Ordinal);
        Check("S219/§7.5: LaunchingToPlane, then StartCountdown, then DesiredInclination",
              plane > 0 && countdown > plane && inclination > countdown,
              "plane=" + plane + " countdown=" + countdown + " inc=" + inclination);

        // ⛔ AND THE COUNTDOWN IS ARMED AT ALL. This is the single line S215 refused to write, and the
        // one the owner's directive is about. A silent revert to a hand-rolled warp fails here.
        Check("S219/§7.5: MechJeb's own countdown IS armed (the call S215 refused)",
              Count(src, "ap.StartCountdown(") == 1,
              "found " + Count(src, "ap.StartCountdown(") + " call(s)");
        Check("S219: ...and the conductor's replaced warp controller is NOT called any more",
              Count(src, "TickLaunchWarp()") == 1, "kept as a superseded definition, never invoked");

        // ⛔ AND IT IS SUPERSEDED IN PLACE, NOT DELETED (C1.16 / G12).
        Check("S219/C1.16: the superseded warp controller is still in the file, marked",
              src.Contains("SUPERSEDED IN PLACE") && src.Contains("TickLaunchWarp"), "");
        string lw = File.ReadAllText(Repo("plugin", "src", "pure", "LaunchWindow.cs"));
        Check("S219/C1.16: LaunchWindow's Q1 is marked superseded in place, not removed",
              lw.Contains("SUPERSEDED IN PLACE") && lw.Contains("Q1"), "");

        // ⛔ AND THE NODE-COMPOSING PATH IS STILL THERE. The brief: "DO NOT DELETE the node-composing
        // path — add this as a selectable mode."
        Check("S219: the conductor's own node-composing path survives the rendezvous OVERRIDE",
              src.Contains("static void PlanOperation") && src.Contains("static void BurnNode")
              && src.Contains("static void Replan"), "");
        Check("S219: ...and it is selectable, not orphaned",
              src.Contains("SelectRendezvousDrive")
              && src.Contains("RendezvousDrive.Conductor"), "");
    }

    /// <summary>The set-then-engage pairing for one module, INSIDE ITS OWN RUNNER: both present, and
    /// in that order. Scoped, because the `Users.Add(Owner)` idiom is shared by three modules and a
    /// file-wide search would match whichever one happens to appear first.</summary>
    static void SetThenEngage(string src, string what, string runner, string configure, string engage)
    {
        string body = Body(src, runner);
        Check("S219: " + runner + " is a method this file still has", body.Length > 0, "");
        int c = body.IndexOf(configure, StringComparison.Ordinal);
        int e = body.IndexOf(engage, StringComparison.Ordinal);
        Check("S219: " + what + " is CONFIGURED", c >= 0, "looked for: " + configure);
        Check("S219: " + what + " is ENGAGED (not left ready-to-go doing nothing)", e >= 0,
              "looked for: " + engage);
        Check("S219: " + what + " is configured BEFORE it is engaged", c >= 0 && e > c,
              "configure@" + c + " engage@" + e);
    }

    static int Count(string s, string needle)
    {
        int n = 0, i = 0;
        while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    /// <summary>The body of a method, from its signature to the next method at the same indent. Crude
    /// on purpose: it only has to be good enough to answer "does this method mention X".</summary>
    static string Body(string src, string method)
    {
        Match m = Regex.Match(src, @"(static|public)[^\n]*\b" + Regex.Escape(method) + @"\s*\(");
        if (!m.Success) return "";
        int start = m.Index;
        Match next = Regex.Match(src.Substring(start + 1), @"\n        (static|public|///|// =)");
        int len = next.Success ? next.Index : Math.Min(6000, src.Length - start - 1);
        return src.Substring(start, len);
    }

    // =====================================================================================
    // 2. ⭐⭐ THE FULL IGNITION CHAIN, HEADLESS — bind → resolve → command → thrust
    // =====================================================================================
    //
    // Owner: *"or mechjeb will throttle up but never activate the engines."*
    //
    // Every link is pure and is walked here in order, from the REAL part names, with each link's
    // output feeding the next. ⛔ A break anywhere fails a NAMED link rather than the whole thing, so
    // a regression says which link went.
    static void TheIgnitionChain()
    {
        // The real vessel, from `docs/reference/craftdump.csv`'s own names.
        const string OCTAWEB = "TE.19.F9.S1.Engine";
        const string S1TANK = "TE.19.F9.S1.Tank";
        const string POD = "TE.18.DRAGONV2.POD";
        string[] partNames = { POD, S1TANK, OCTAWEB, "TE.Ghidorah.Erector" };

        // ---- LINK 1: BIND. The §B16.4 guard accepts this vessel and names the octaweb part. --------
        string bound;
        OctawebBind verdict = OctawebBinding.Bind(partNames, out bound);
        Check("S219 chain 1/5 BIND: the guard accepts the real vessel",
              verdict == OctawebBind.Ok && bound == OCTAWEB, "verdict=" + verdict + " bound=" + bound);

        // ---- LINK 2: RESOLVE. The three engineID modes resolve to three distinct indices. ----------
        OctawebEngineRef[] refs =
        {
            Ref(OCTAWEB, OctawebBinding.EngineIdAll),
            Ref(OCTAWEB, OctawebBinding.EngineIdThreeLanding),
            Ref(OCTAWEB, OctawebBinding.EngineIdCenterOnly),
            Ref(POD, "SuperDraco"),
        };
        OctawebTable t = OctawebResolve.Build(partNames, refs);
        Check("S219 chain 2/5 RESOLVE: the named table binds",
              t.Ok, "plan=" + t.Plan + " guard=" + t.Guard);
        Check("S219 chain 2/5 RESOLVE: ...to three DISTINCT modules, by engineID and nothing else",
              t.Ok && t.AllIndex != t.ThreeIndex && t.ThreeIndex != t.CentreIndex
              && t.AllIndex != t.CentreIndex,
              "all=" + t.AllIndex + " three=" + t.ThreeIndex + " centre=" + t.CentreIndex);
        Check("S219 chain 2/5 RESOLVE: ...and the all-engines mode is the one liftoff names",
              t.Ok && refs[t.AllIndex].EngineId == OctawebBinding.EngineIdAll, "");

        // ---- LINK 3: COMMAND. `IgniteOctawebLiftoff` lights `EngineRole.OctawebAll`, and ONLY it. ---
        // ⛔ REGRESSION GUARD, flight_0822_201219: lighting Three/Centre as well COOKED THE S1 TANK.
        int lit = 0;
        for (int i = 0; i < refs.Length; i++)
            if (Actuation.EngineLightsFor(refs[i].PartName, refs[i].EngineId, EngineRole.OctawebAll)) lit++;
        Check("S219 chain 3/5 COMMAND: exactly ONE module lights for the liftoff command",
              lit == 1, "lit=" + lit);
        Check("S219 chain 3/5 COMMAND: ...and the SuperDraco abort motor is NOT one of them",
              !Actuation.EngineLightsFor(POD, "SuperDraco", EngineRole.OctawebAll)
              && Actuation.EngineRoleOf(POD, "SuperDraco") == EngineRole.PodAbort, "");

        // ---- LINK 4: THE SEQUENCE COMMANDS IT. Idle + GO + a solution + T-0 arrived => IgniteStageOne.
        AscentInputs s = AscentInputs.Nominal();
        s.LaunchCommanded = true;
        s.GuidanceReady = true;                       // S214: nobody owns the throttle without one
        s.WindowRequired = true; s.WindowArmed = true;
        s.SecondsToWindowS = AscentInputs.IgnitionLeadSeconds;   // T-3 s exactly
        AscentDecision d = AscentSequence.Step(s, AscentStep.Idle);
        Check("S219 chain 4/5 SEQUENCE: at T-3 s with a solution, the octaweb is COMMANDED alight",
              d.Act == AscentAct.IgniteStageOne && d.Next == AscentStep.Ignition,
              "act=" + d.Act + " next=" + d.Next);

        // ...and NOT one second earlier, and NOT without a guidance solution. Both are S214's rules and
        // both are what stop a cold octaweb lighting into a commanded zero.
        AscentInputs early = s; early.SecondsToWindowS = AscentInputs.IgnitionLeadSeconds + 1.0;
        Check("S219 chain 4/5 SEQUENCE: ...but not a second early",
              AscentSequence.Step(early, AscentStep.Idle).Act != AscentAct.IgniteStageOne, "");
        AscentInputs blind = s; blind.GuidanceReady = false;
        Check("S219 chain 4/5 SEQUENCE: ...and never without a guidance solution (S214)",
              AscentSequence.Step(blind, AscentStep.Idle).Act != AscentAct.IgniteStageOne, "");

        // ---- LINK 5: THRUST OBSERVED => THE HOLD-DOWNS RELEASE. -------------------------------------
        // `IgnitionGate` is the only thing that may release a clamp, and it releases on MEASURED thrust.
        double max = 8227000.0;   // the flown octaweb's own available thrust, from the S214 log line
        Check("S219 chain 5/5 THRUST: 99% of available on one lit module releases the hold-downs",
              IgnitionGate.Evaluate(max * IgnitionGate.ReleaseThrustFrac, max, 1, 0.5) == ClampAction.Release, "");
        Check("S219 chain 5/5 THRUST: a COMMANDED but unlit octaweb holds them",
              IgnitionGate.Evaluate(0.0, max, 0, 0.5) == ClampAction.Hold, "");
        Check("S219 chain 5/5 THRUST: ⭐ and thrust that never arrives SAFES the pad, clamps still held",
              IgnitionGate.Evaluate(0.0, max, 1, IgnitionGate.MaxHoldS + 0.1) == ClampAction.SafeAbort, "");

        // ⛔ THE WHOLE CHAIN, AS ONE STATEMENT. This is what "bind → named parts resolve → activation
        // commanded → thrust observed" means, and it is the line that fails if ANY link is cut.
        bool chain = verdict == OctawebBind.Ok && t.Ok && lit == 1
                     && d.Act == AscentAct.IgniteStageOne
                     && IgnitionGate.Evaluate(max, max, 1, 0.5) == ClampAction.Release;
        Check("S219: ⭐ THE FULL IGNITION CHAIN HOLDS END TO END", chain, "");
    }

    static OctawebEngineRef Ref(string part, string id)
    {
        OctawebEngineRef r = new OctawebEngineRef();
        r.PartName = part; r.EngineId = id;
        return r;
    }

    // =====================================================================================
    // 3. ⭐⭐ THE T-0 STAGING — ESTABLISHED FROM SOURCE AND FROM THE CRAFT, NOT RACED
    // =====================================================================================
    //
    // THE HAZARD, verbatim from `MechJebModuleAscentBaseAutopilot.cs:122-137`:
    //     if (TimedLaunch) {
    //         if (TMinus < 3 * DeltaT || (TMinus > 10.0 && _lastTMinus < 1.0)) {
    //             if (Enabled && VesselState.ThrustAvailable < 10E-4) StageManager.ActivateNextStage();
    //             TimedLaunch = false;
    //         } else { if (Core.Node.Autowarp) Core.Warp.WarpToUT(_launchTime - WarpCountDown); }
    //     }
    //
    // The brief offered two ways it might be harmless and demanded we prove WHICH:
    //   (1) our octaweb is lit before T-0 so it never fires;
    //   (2) with autostage OFF and direct part control, the staging list is inert.
    //
    // ⛔ (2) IS FALSE ON THIS CRAFT, AND THE CRAFT FILE SAYS SO — asserted below. The staging list is
    //    live: stage 8 is the octaweb ALONE and stage 7 is the Ghidorah erector — THE HOLD-DOWNS —
    //    ALONE. `AscentSettings.Autostage` governs only `Core.Staging.Users` (`:82`, `:128-133`); it
    //    does nothing whatever to a direct `StageManager.ActivateNextStage()` call.
    //
    // ⚠ (1) IS TRUE **ON THE NOMINAL PATH ONLY**, and [[S215]] already said why that is not enough:
    //    *"a safety property that holds only because we win a race is not a safety property."* And the
    //    case it loses is the important one: `IgnitionGate` SAFING the pad at ~T-1 s shuts the engines,
    //    so `ThrustAvailable` is back to ZERO at T-0 and MechJeb would re-light the octaweb the gate
    //    just shut — outside the gate, with the clamps held.
    //
    // ⭐ SO NEITHER IS RELIED ON. The branch is made UNREACHABLE: `TimedLaunch` is cleared at T-10 s
    //    by `MechConductor.TickTerminalCount`, using the same write MechJeb's own Abort button makes
    //    (`MechJebModuleAscentMenu.cs:305`). The whole `if (TimedLaunch)` block, staging included, is
    //    dead from then on. That is establishment, not a race.
    static void TheTZeroStaging()
    {
        // ---- (a) THE VENDORED SOURCE STILL SAYS WHAT WE READ IT TO SAY ----------------------------
        // A re-pin that moved the staging call out from under `if (TimedLaunch)` would silently break
        // the entire argument above, so it is pinned against the tree rather than remembered.
        string ap = File.ReadAllText(Repo("plugin", "mech", "MechJeb2", "MechJebModuleAscentBaseAutopilot.cs"));
        Check("S219: the vendored T-0 staging call still exists",
              ap.Contains("StageManager.ActivateNextStage()"), "");
        Check("S219: ...still gated on `Enabled && VesselState.ThrustAvailable < 10E-4`",
              ap.Contains("if (Enabled && VesselState.ThrustAvailable < 10E-4)"), "");

        int timed = ap.IndexOf("if (TimedLaunch)", StringComparison.Ordinal);
        int stage = ap.IndexOf("StageManager.ActivateNextStage()", StringComparison.Ordinal);
        Check("S219: ⭐ ...and still sits INSIDE `if (TimedLaunch)` — the whole basis of the fix",
              timed > 0 && stage > timed, "timed@" + timed + " stage@" + stage);
        Check("S219: TimedLaunch is a PUBLIC field, so clearing it is a UI action, not a patch",
              ap.Contains("public bool TimedLaunch;"), "");

        string menu = File.ReadAllText(Repo("plugin", "mech", "MechJeb2", "MechJebModuleAscentMenu.cs"));
        Check("S219: ...and MechJeb's own Abort button makes exactly that write",
              menu.Contains("_autopilot.TimedLaunch = false"), "");

        // ---- (b) WE ACTUALLY MAKE IT, AND BEFORE WE LIGHT ANYTHING --------------------------------
        string src = File.ReadAllText(Repo("plugin", "src", "MechConductor.cs"));
        Check("S219: the conductor clears TimedLaunch",
              src.Contains("ap.TimedLaunch = false"), "");
        Check("S219: ...from TickTerminalCount, which is on the ascent tick",
              Body(src, "TickTerminalCount").Contains("ap.TimedLaunch = false")
              && src.Contains("TickTerminalCount();"), "");
        Check("S219: ⭐ ...and it happens BEFORE the ignition lead, so MechJeb never owns T-0",
              AscentProfile.TerminalCountS > AscentInputs.IgnitionLeadSeconds,
              "terminal=" + AscentProfile.TerminalCountS + " ignition=" + AscentInputs.IgnitionLeadSeconds);

        // ---- (c) ⛔ AND THE STAGING LIST IS **NOT** INERT — the craft file, read here ---------------
        // This is the check that disproves the brief's option (2). If a future craft revision made the
        // staging list genuinely harmless this would fail and someone would have to re-establish it.
        string craft = File.ReadAllText(Repo("docs", "reference", "Crew-2.craft"));
        int octawebStage = StageOf(craft, "TE.19.F9.S1.Engine_");
        int erectorStage = StageOf(craft, "TE.Ghidorah.Erector_");
        Check("S219: the octaweb has a real KSP stage (the list is NOT inert)",
              octawebStage >= 0, "istg=" + octawebStage);
        Check("S219: ⛔ so do the HOLD-DOWNS — the erector is a staged decoupler",
              erectorStage >= 0, "istg=" + erectorStage);
        Check("S219: ⭐ and the erector stages IMMEDIATELY AFTER the octaweb — two ActivateNextStage "
              + "calls on a cold pad would release the clamps",
              octawebStage >= 0 && erectorStage == octawebStage - 1,
              "octaweb istg=" + octawebStage + " erector istg=" + erectorStage);

        // ---- (d) THE CASE THE RACE WOULD HAVE LOST, spelled out as a check -------------------------
        // A pad-safe shuts the engines. If MechJeb still owned T-0 at that moment, `ThrustAvailable`
        // would be zero and it would re-light what the gate just shut.
        double max = 8227000.0;
        Check("S219: a pad-safe leaves the stage SHUT — which is zero ThrustAvailable at T-0",
              IgnitionGate.Evaluate(0.0, max, 1, IgnitionGate.MaxHoldS + 0.1) == ClampAction.SafeAbort, "");
        Check("S219: ⭐ ...and that is exactly why the branch is removed rather than out-run",
              AscentProfile.TerminalCountS > AscentInputs.IgnitionLeadSeconds
              && AscentProfile.TerminalCountS > IgnitionGate.MaxHoldS, "");

        // ---- (e) ⛔ AND IgnitionGate ITSELF IS UNTOUCHED. The brief: "Do not weaken IgnitionGate." ---
        Check("S219: IgnitionGate's release fraction is still 99%",
              IgnitionGate.ReleaseThrustFrac == 0.99, "");
        Check("S219: IgnitionGate's hold window is still 2 s",
              IgnitionGate.MaxHoldS == 2.0, "");
    }

    /// <summary>The `istg` of the first part whose `part =` line starts with <paramref name="prefix"/>,
    /// read straight out of the .craft file. −1 if absent.</summary>
    static int StageOf(string craft, string prefix)
    {
        Match m = Regex.Match(craft,
            @"part = " + Regex.Escape(prefix) + @"\d+\r?\n(?:.*\r?\n)*?\tistg = (-?\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : -1;
    }
    // =====================================================================================
    // 4. ⭐⭐ S222b — **THE WRITES ARE GONE, AND THEY STAY GONE.**
    // =====================================================================================
    //
    // The owner, 2026-09-08, verbatim: *"return everything back to default settings and using the
    // methods I described for achieving each stage. No guesses, no invented methods or 'tuning' truely
    // stock mechjeb methods and settings set for auto accent, auto rendezvous and auto docking!"* and
    // *"the only change should be the auto stage being our way"*.
    //
    // ⛔ WHY IT IS CHECKED AS TEXT AND NOT AS BEHAVIOUR. `src/MechConductor.cs` needs KSP to compile, so
    // nothing headless can CALL `Configure`. But "this file no longer writes MechJeb's pitch rate" is a
    // claim about TEXT, and it is exactly the claim that rots: the next task to touch this file will be
    // fixing something else, and re-adding one line to "make it deterministic" is a two-second edit that
    // nobody would notice. This suite is what notices.
    //
    // ⚠ SCOPED TO `Configure` + `MirrorTheMenus`, not to the file. `RunDocking` legitimately writes
    // `speedLimit`, the booster host has its own settings, and a file-wide search would fail on both.
    static void NoTuningWrites()
    {
        // ⛔ LIVE CODE ONLY. `Configure`'s own comment block names every write S222b dropped, so an
        // un-stripped search would fail on the very paragraph that explains the fix — and, the other
        // way round, a commented-out write would pass the presence checks below (S220's mutants).
        string src = Live(File.ReadAllText(Repo("plugin", "src", "MechConductor.cs")));
        string cfg = Body(src, "Configure") + Body(src, "MirrorTheMenus");

        // ⛔ EVERY BOX `ApplyRODefaults()` SEEDS, by the FIELD NAME the write would have to use. This
        // is the same independent-copy idiom as `AscentProfileTest.RoSeeded`, one level down: that one
        // proves the TABLE says we do not write them, this one proves the CODE does not.
        string[] roSeededWrites =
        {
            // 🟢 S250 — ~~"PitchStartHeight", "PitchRate"~~ REMOVED FROM THIS LIST: the owner
            // closed Q1 and ruled both values on 2026-09-09. They are now WRITES and are asserted as
            // such below. ⛔ Everything else on this list stays, and the rule is unchanged.
            "DesiredAttachAlt", "DesiredAttachAltFixed", "DesiredFPA",
            "AttachAltFlag", "DesiredArgP", "DesiredArgPFlag", "LimitQa", "MinDeltaV", "MaxCoast",
            "MinCoast", "LaunchLANDifference", "PreStageTime", "OptimizerPauseTime",
            "SpinupStageFlag", "SpinupStageInternal", "CoastStageFlag", "CoastStageInternal",
            "UnguidedStagesFlag", "FixedStagesFlag",
        };
        for (int i = 0; i < roSeededWrites.Length; i++)
            Check("S222b: ⛔ Configure no longer ASSIGNS '" + roSeededWrites[i] + "' — RO seeds it",
                  !Assigns(cfg, roSeededWrites[i]), "an assignment is still present");

        // ⚠⚠ S250 — ~~the entire Core.Thrust baseline block is gone (RO seeds all
        // nine)~~ SUPERSEDED IN PLACE (C1.16). THREE of the nine are back, by OWNER RULING, and the
        // check is now the precise one rather than the blanket one: the three he named are present
        // and the other six are still absent. ⛔ A blanket `!cfg.Contains("Thrust")` would have
        // to be deleted to let any of them through, and deleting a check is exactly how the next six
        // creep back in.
        string[] thrustWrites =
        {
            "core.Thrust.LimitDynamicPressure = true",
            "core.Thrust.MaxDynamicPressure.Val = AscentProfile.MaxDynamicPressurePa",
            "core.Thrust.LimitToPreventOverheats = true",
        };
        for (int i = 0; i < thrustWrites.Length; i++)
            Check("S250: the owner-ruled thrust write '" + thrustWrites[i] + "' is present",
                  cfg.Contains(thrustWrites[i]), "");
        string[] thrustStillAbsent =
        {
            "LimitAcceleration", "LimitAcceleration.Val", "LimitToTerminalVelocity",
            "ElectricThrottle", "DifferentialThrottle", "SmoothThrottle",
        };
        for (int i = 0; i < thrustStillAbsent.Length; i++)
            Check("S250: ⛔ and the rest of the Core.Thrust baseline is STILL not written: '"
                  + thrustStillAbsent[i] + "'",
                  !cfg.Contains(thrustStillAbsent[i]), "a write is present");
        // ⛔ MinThrottle and LimiterMinThrottle are RO's and stay RO's — the read-back expects
        // 0.05 / true and reported them WRONG before this task, which is one of the two rows the
        // owner is checking after the restart.
        Check("S250: ⛔ MinThrottle is still RO's, never ours",
              !cfg.Contains("MinThrottle"), "a write is present");

        // ...and the field defaults we used to re-assert for no gain. ⚠ `MinDeltaV` and `LastStage` are
        // deliberately NOT in the absence list as bare names: `MirrorTheMenus` READS both, exactly as
        // the PSG-settings window's own loop does. Reading a field is not writing it — which is the
        // distinction S219 collapsed when it wrote them "because PvgPreflight reads them".
        string[] fieldDefaultWrites =
        {
            "ForceRoll", "VerticalRoll", "TurnRoll", "RollAltitude", "LastStage", "CoastLocation",
            "Cd", "Aref", "RelativeLAN", "OverrideWarpToPlane", "LaunchingToMatchLan", "LaunchingToLan",
        };
        for (int i = 0; i < fieldDefaultWrites.Length; i++)
            Check("S222b: ⛔ Configure no longer re-asserts '" + fieldDefaultWrites[i] + "'",
                  !Assigns(cfg, fieldDefaultWrites[i]), "an assignment is still present");

        // ⛔ AND THE ABSENCE CHECKS ARE NOT VACUOUS. `Assigns` must actually FIND the writes that DO
        // survive, or every line above would pass against a matcher that never matches anything.
        // ⚠ S250: the four names this probe used are all WITHDRAWN writes now, so it had to move
        // to writes that survive — otherwise the absence checks above would be passing against a
        // matcher that never matches anything, which is the one way this whole block could go quiet.
        Check("S250: the assignment matcher is not vacuous — it finds the writes that survive",
              Assigns(cfg, "PitchRate") && Assigns(cfg, "PitchStartHeight")
              && Assigns(cfg, "MaxDynamicPressure"), "");
        Check("S222b: ...and it does not fire on a READ of the same field",
              !Assigns("x = a.MinDeltaV.Val;", "MinDeltaV")
              && !Assigns("if (a.LastStage.Val > 0) { }", "LastStage"), "");
        Check("S222b: ...and it DOES fire on both assignment shapes MechJeb settings take",
              Assigns("a.PitchRate.Val = 0.75;", "PitchRate")
              && Assigns("a.AttachAltFlag = true;", "AttachAltFlag"), "");

        // 🟢🟢 S250 — ~~the withdrawn PitchRate = 0.75 is NOT written anywhere in Configure~~
        // SUPERSEDED IN PLACE (C1.16), AND THE DISTINCTION IS THE WHOLE POINT. S222b was right to
        // withdraw it: an overseer prompt directing a tuning value with no provenance is invented
        // tuning, whoever types it. What changed is not the number but its AUTHORITY — the OWNER
        // ruled it on 2026-09-09 from real Falcon 9 + Dragon mission telemetry, and it is now a named
        // constant carrying that provenance rather than a literal in the glue.
        // ⛔ SO THE CHECK INVERTS RATHER THAN DISAPPEARING: the value must reach MechJeb THROUGH
        // `AscentProfile`, and must still never be a bare literal here.
        Check("S250: PitchRate is written from the named constant, not from a literal",
              cfg.Contains("a.PitchRate.Val = AscentProfile.PitchRateDegPerS")
              && !cfg.Contains("0.75"), "a bare 0.75 is in the glue");
        Check("S250: ...and so are the other two ascent numbers",
              cfg.Contains("a.PitchStartHeight.Val = AscentProfile.PitchStartHeightM")
              && !cfg.Contains("24000"), "a bare magnitude is in the glue");

        // ⭐ AND `OptimizeStageFlag = false` — the write that CREATED the attach-altitude defect S219
        // then compensated for. It must never be written as a literal again; it is derived.
        Check("S222b: ⛔⛔ OptimizeStageFlag is never written as a literal false",
              !cfg.Contains("OptimizeStageFlag = false"), "the S219 write is back");
        Check("S222b: ...nor as a literal true — it is DERIVED from the stage table, not asserted",
              !cfg.Contains("OptimizeStageFlag = true"), "hardcoded rather than derived");

        // ⛔ THE INCLINATION. The owner: "DO NOT WRITE THE INCLINATION — LaunchingToPlane overrides it
        // from the target (§7.5). Writing it fights the feature." It survives on ONE path only: a
        // free-flyer, which has no plane launch and no button to press, and there it is guarded by
        // `WindowRequired()`.
        string configureOnly = Body(src, "Configure");
        int incWrite = configureOnly.IndexOf("a.DesiredInclination.Val = t.InclinationDeg", StringComparison.Ordinal);
        int guard = configureOnly.IndexOf("if (!WindowRequired())", StringComparison.Ordinal);
        Check("S222b: Configure still writes the inclination for a FREE-FLYER (nothing else would)",
              incWrite >= 0, "");
        Check("S222b: ⛔ ...but ONLY behind the no-rendezvous guard, so it cannot fight §7.5",
              guard >= 0 && incWrite > guard, "guard@" + guard + " write@" + incWrite);
        Check("S222b: ...and the plane launch is still the one that writes it on a rendezvous",
              src.Contains("a.DesiredInclination.Val = window.InclinationDeg"), "");

        // =================================================================================
        // ⭐ 215 km REACHES THE MODULE — end to end, and pinned as a number.
        // =================================================================================
        // Owner, 2026-09-08: *"we can set the orbit to 215km"*. Three links in the chain, each of which
        // could break independently: the constant, the resolver, and the write into MechJeb.
        Check("S222b: the constant is 215 km",
              AscentTargets.IssInsertionAltitudeM == 215000.0,
              "got " + AscentTargets.IssInsertionAltitudeM);

        MissionProfile crew = Missions.Resolve("Crew-2");
        AscentTarget t = AscentTargets.For(crew, -51.6316);
        Check("S222b: ...and the resolver hands an ISS mission 215 x 215 km",
              t.PeriapsisM == 215000.0 && t.ApoapsisM == 215000.0,
              "got " + t.PeriapsisM + " x " + t.ApoapsisM);
        Check("S222b: ⛔ ...not the superseded 210, and not RO's own 145",
              t.PeriapsisM != 210000.0 && t.PeriapsisM != 145000.0, "");

        Check("S222b: ...and Configure writes BOTH apsides into MechJeb from that resolver",
              configureOnly.Contains("a.DesiredOrbitAltitude.Val = t.PeriapsisM")
              && configureOnly.Contains("a.DesiredApoapsis.Val      = t.ApoapsisM"), "");
        Check("S222b: ⛔ ...and the destination is never a literal in the glue — it comes from the profile",
              !configureOnly.Contains("215000") && !configureOnly.Contains("210000"),
              "a hardcoded altitude in the glue would outlive the mission catalogue");

        // =================================================================================
        // ⭐ THE EIGHT SURVIVING WRITES ARE ALL PRESENT — the other half of "stays removed".
        // =================================================================================
        // Without this, deleting a KEEP would pass every check above. Each is the owner's, by name.
        // ⛔⛔ S250 — THE KEEP LIST IS NOW THE OWNER'S 2026-09-09 LIST, AND THE OLD ONE IS
        // KEPT ABOVE IT SO THE SHED IS VISIBLE. ~~AscentType — Autostage — WarpCountDown —
        // SkipCircularization — AutoDeploySolarPanels — AutoDeployAntennas — Core.Node.Autowarp
        // — Core.Warp.activateSASOnWarp~~ all WITHDRAWN by option (b).
        string[] mustSurvive =
        {
            "a.PitchRate.Val = AscentProfile.PitchRateDegPerS",
            "a.PitchStartHeight.Val = AscentProfile.PitchStartHeightM",
            "core.Thrust.LimitDynamicPressure = true",
            "core.Thrust.MaxDynamicPressure.Val = AscentProfile.MaxDynamicPressurePa",
            "core.Thrust.LimitToPreventOverheats = true",
            "ApplyStagingFloor(v)",
        };
        for (int i = 0; i < mustSurvive.Length; i++)
            Check("S250: the KEEP '" + mustSurvive[i] + "' is still written",
                  configureOnly.Contains(mustSurvive[i]), "");
        // ...and the eight withdrawals really are gone from the live code, not merely renamed.
        string[] mustBeGone =
        {
            "a.AscentType = MuMech.AscentType.PSG",
            "a.Autostage = false",
            "a.WarpCountDown.Val = AscentProfile.WarpCountDownS",
            "a.SkipCircularization = true",
            "a.AutoDeploySolarPanels = false",
            "a.AutoDeployAntennas = false",
            "core.Node.Autowarp = true",
            "core.Warp.activateSASOnWarp = false",
        };
        for (int i = 0; i < mustBeGone.Length; i++)
            Check("S250: the WITHDRAWN write '" + mustBeGone[i] + "' is gone from Configure",
                  !configureOnly.Contains(mustBeGone[i]), "still written");

        // ⛔ AND THE AUDIT IS STILL LOGGED, so the flight's own KSP.log answers "what did we fly".
        Check("S222b: Configure still logs the audit, so the count is on the flight record",
              configureOnly.Contains("AscentProfile.Render()"), "");
        Check("S222b: ...and the log names the attach altitude it is NOT writing",
              configureOnly.Contains("attach altitude LEFT AT RO's 110 km"), "");
    }

    // =====================================================================================
    // 5. ⭐ S222b — RENDEZVOUS AND DOCKING: CONFIGURED **AND ENGAGED**, AND AUTOWARP OWNED
    // =====================================================================================
    //
    // The owner's central complaint, verbatim: *"otherwise it will sit there ready to go but do
    // nothing."* `ConfiguredMeansEngaged` already pins the set-then-engage pairing for all four
    // modules; this extends it to the two things S222b touched around them — that the §8 and §9
    // options are still MechJeb's own, and that exactly one thing owns the warp in each phase.
    static void EveryPhaseEngagesAndWarps()
    {
        string src = Live(File.ReadAllText(Repo("plugin", "src", "MechConductor.cs")));

        // ⭐ ONE WARP OWNER PER PHASE, established from the vendored source (§8) rather than assumed:
        //   ascent      -> the ascent autopilot's countdown, gated on Core.Node.Autowarp
        //   rendezvous  -> the node executor, gated on the SAME field, which the rendezvous autopilot
        //                  NARROWS (`Autowarp && Target.Distance > 1000`) rather than replacing
        //   docking     -> nobody. Pure RCS from the keep-out sphere inward.
        // ⚠ S250: ~~Configure sets the one flag the countdown reads~~ — WITHDRAWN by option (b),
        // and the VALUE does not move: `MechJebModuleNodeExecutor.cs:24`'s field default is already
        // true. ⭐ The rendezvous re-assert below is the one that still matters, because the
        // rendezvous autopilot LATCHES the flag false and nothing else puts it back.
        Check("S250: ascent — the countdown's flag is left at MechJeb's own default true",
              !Body(src, "Configure").Contains("core.Node.Autowarp = true"), "");
        Check("S222b/warp: rendezvous — the runner RE-ASSERTS it, because the autopilot latches it false",
              Body(src, "RunRendezvousAutopilot").Contains("core.Node.Autowarp = true"), "");
        Check("S222b/warp: ...and only outside 1 km, which is where the autopilot's own narrowing bites",
              Body(src, "RunRendezvousAutopilot").Contains("rangeM > 1000.0"), "");
        Check("S222b/warp: docking — nothing in RunDocking touches a warp flag, by design",
              !Body(src, "RunDocking").Contains("Autowarp")
              && !Body(src, "RunDocking").Contains("WarpToUT"), "");

        // ⛔ AND ALL THREE ARE ENGAGED, not merely configured. Restated here as one block so the
        // owner's sentence has a single place to fail.
        Check("S222b: ⭐ the ascent autopilot is ENGAGED",
              Body(src, "RunAscent").Contains("ap.Users.Add(Owner)"), "");
        Check("S222b: ⭐ the rendezvous autopilot is ENGAGED",
              Body(src, "RunRendezvousAutopilot").Contains("ap.Users.Add(Owner)"), "");
        Check("S222b: ⭐ the docking autopilot is ENGAGED",
              Body(src, "RunDocking").Contains("ap.Users.Add(Owner)"), "");

        // ⛔ THE NODE-COMPOSING PATH IS SUPERSEDED-FOR-NOW, NOT DELETED (C1.16/G12). The owner is
        // returning to it: *"mechjeb rendezvous autopilot just for now… Then we move to the more
        // complicated, mission accurate fidelity way"*.
        Check("S222b/C1.16: the conductor's own node-composing rendezvous path is still here",
              src.Contains("static void PlanOperation") && src.Contains("static void Replan"), "");
        Check("S222b/C1.16: ...and still selectable, so it is superseded-for-now and not orphaned",
              src.Contains("RendezvousDrive.Conductor"), "");
    }

}
