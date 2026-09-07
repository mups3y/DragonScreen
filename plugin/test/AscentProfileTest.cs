/*
 * S219 JOB 2 — the ASCENT SETTINGS AUDIT (pure/AscentProfile.cs).
 *
 * WHAT THIS EXISTS TO STOP. The owner, 2026-09-07, verbatim:
 *     "How can the conductor act like it's a user using mechjebs UI if it does not know what
 *      setting/options to set"
 *     "⛔ Enumerate EVERY setting the ascent menu exposes and state, one by one, whether we set it and
 *      why. That table is the deliverable. No value may be left as 'whatever the profile set'"
 *
 * Before S219 `MechConductor.Configure` wrote four values and logged, in as many words, that every
 * ascent-SHAPING value was "whatever the loaded profile set". A table in a comment would have fixed the
 * sentence and not the problem: a comment cannot fail. These checks can.
 *
 * ⛔ THE SHARPEST ONES ARE THE COMPLETENESS AND ORDERING CHECKS. An audit that silently loses a row is
 * exactly the state the owner objected to, wearing a table for a hat; and `TerminalCountS` sitting on
 * the wrong side of either of its two neighbours quietly kills one of the two countdown mechanisms.
 *
 * ⚠ WHAT THIS DOES *NOT* PROVE. That `MechConductor.Configure` actually writes what the table says —
 * `src/MechConductor.cs` is KSP glue and cannot be compiled headlessly at all. What it proves is that
 * the SPECIFICATION is complete, unambiguous, self-consistent, and that its load-bearing values are the
 * ones the research names. The engage half is `ConductorEngageTest` (S219 JOB 3).
 */
using DragonScreen;
using System;

public static class AscentProfileTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    // ⭐ EVERY BOX THE VENDORED ASCENT MENUS PUT ON SCREEN, enumerated from the menus themselves
    // (`MechJebModuleAscentMenu.cs`, `MechJebModuleAscentSettingsMenu.cs`,
    // `MechJebModuleAscentPSGSettingsMenu.cs`, `MechJebModuleAscentClassicPathMenu.cs`) — every
    // `_ascentSettings.<X>` they touch. ⛔ THIS LIST IS THE POINT OF THE SUITE: it is an INDEPENDENT
    // copy of the question, so a row quietly dropped from `AscentProfile.Audit` fails here instead of
    // becoming a setting nobody decided about.
    static readonly string[] ExposedByTheMenus =
    {
        "AOALimitFadeoutPressure", "Aref", "AscentType", "AttachAltFlag", "AutoDeployAntennas",
        "AutoDeploySolarPanels", "Autostage", "Cd", "CoastLocation", "CoastStageFlag",
        "CoastStageInternal", "CorrectiveSteering", "CorrectiveSteeringGain", "DesiredApoapsis",
        "DesiredArgP", "DesiredArgPFlag", "DesiredAttachAlt", "DesiredAttachAltFixed", "DesiredFPA",
        "DesiredInclination", "DesiredLan", "DesiredOrbitAltitude", "FixedStages", "FixedStagesFlag",
        "FixedStagesInternal", "ForceRoll", "LastStage", "LaunchLANDifference", "LaunchingToLan",
        "LaunchingToMatchLan", "LaunchingToPlane", "LimitAoA", "LimitQa", "LimitQaEnabled",
        "LimitingAoA", "MaxAoA", "MaxCoast", "MinCoast", "MinDeltaV", "OptimizeStageFlag",
        "OverrideWarpToPlane", "PitchRate", "PitchStartHeight", "RelativeLAN", "RollAltitude",
        "SkipCircularization", "SpinupAngularVelocity", "SpinupLeadTime", "SpinupStageFlag",
        "SpinupStageInternal", "TurnRoll", "UnguidedStages", "UnguidedStagesFlag",
        "UnguidedStagesInternal", "VerticalRoll", "WarpCountDown",
        // the CLASSIC path menu
        "TurnStartAltitude", "TurnStartVelocity", "TurnEndAltitude", "TurnEndAngle",
        "TurnShapeExponent", "AutoPath", "AutoTurnPerc", "AutoTurnSpdFactor",
    };

    public static int Run()
    {
        Console.WriteLine("AscentProfileTest (S219: every ascent setting accounted for, and the countdown ordering)");

        CompletenessTests();
        LoadBearingValueTests();
        CountdownTests();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures == 0 ? 0 : 1;
    }

    // =====================================================================================
    // 1. ⛔ NO VALUE LEFT AS "WHATEVER THE PROFILE SET"
    // =====================================================================================
    static void CompletenessTests()
    {
        for (int i = 0; i < ExposedByTheMenus.Length; i++)
            Check("S219: '" + ExposedByTheMenus[i] + "' is accounted for in the audit",
                  AscentProfile.Accounted(ExposedByTheMenus[i]), "");

        Check("S219: no setting is decided about twice",
              AscentProfile.FirstDuplicate() == null, "dup=" + AscentProfile.FirstDuplicate());

        Check("S219: every row carries a name, a value AND a reason",
              AscentProfile.FirstUnexplained() == null, "bad=" + AscentProfile.FirstUnexplained());

        // The audit also covers the Core.Thrust / Core.Node / Core.Warp fields on the same screens,
        // so it is legitimately LONGER than the menu list — but never shorter.
        Check("S219: the audit covers at least every menu box",
              AscentProfile.Audit.Length >= ExposedByTheMenus.Length,
              AscentProfile.Audit.Length + " rows vs " + ExposedByTheMenus.Length + " boxes");

        // ⛔ A vacuity guard: an audit of all-RoDefault rows would pass every check above while
        // deciding nothing. Something must actually be written.
        Check("S219: the audit actually WRITES things (it is not an all-defaults table)",
              AscentProfile.CountOf(AscentDisposition.Write) >= 30,
              "written=" + AscentProfile.CountOf(AscentDisposition.Write));

        Check("S219: ...and it writes mission facts at runtime too",
              AscentProfile.CountOf(AscentDisposition.RuntimeMission) >= 5,
              "runtime=" + AscentProfile.CountOf(AscentDisposition.RuntimeMission));

        // ⭐ The two deviations that are the OWNER's to make, not a build chat's (C1.8/C1.12/C1.14).
        Check("S219: PitchRate is an owner question, not a build chat's tune",
              AscentProfile.Row("PitchRate").How == AscentDisposition.OwnerQuestion, "");
        Check("S219: max-Q throttle-down is an owner question, not a build chat's tune",
              AscentProfile.Row("Core.Thrust.LimitDynamicPressure").How == AscentDisposition.OwnerQuestion, "");
        Check("S219: and the render surfaces every owner question by name",
              AscentProfile.Render().Contains("PitchRate")
              && AscentProfile.Render().Contains("LimitDynamicPressure"), "");
    }

    // =====================================================================================
    // 2. ⭐ THE ROWS THE RESEARCH NAMES — each pinned to the disposition its source demands
    // =====================================================================================
    static void LoadBearingValueTests()
    {
        // §7.5's plane launch. "LaunchLANDifference = 0 for the exact plane."
        Check("S219/§7.5: LaunchLANDifference is written, and it is zero",
              AscentProfile.Row("LaunchLANDifference").How == AscentDisposition.Write
              && AscentProfile.LaunchLanDifferenceDeg == 0.0, "");

        // The vendored menu asserts it every frame with "this is mandatory for PSG".
        Check("S219/§7.5: LimitQaEnabled is WRITTEN (the source calls it mandatory)",
              AscentProfile.Row("LimitQaEnabled").How == AscentDisposition.Write
              && AscentProfile.Row("LimitQaEnabled").Value == "true", "");

        // ⭐ §7.2's attach-altitude bug: RO's 110 km default against a 210 km orbit is an ELLIPTICAL
        // insertion, and `SetTarget` reads the *Fixed* field while OptimizeStageFlag is false.
        Check("S219/§7.2: the attach altitude follows the MISSION apsis, not RO's 110 km",
              AscentProfile.Row("DesiredAttachAltFixed").How == AscentDisposition.RuntimeMission
              && AscentProfile.Row("DesiredAttachAltFixed").Value.Contains("110")
              && AscentProfile.AttachAltFollowsMissionApsis, "");
        Check("S219/§7.2: ...and OptimizeStageFlag is WRITTEN, because it is what selects that field",
              AscentProfile.Row("OptimizeStageFlag").How == AscentDisposition.Write
              && AscentProfile.Row("OptimizeStageFlag").Value == "false", "");

        // ⛔ StartCountdown branches on this; a stale true means "launch NOW".
        Check("S219: OverrideWarpToPlane is written false (a stale true launches immediately)",
              AscentProfile.Row("OverrideWarpToPlane").How == AscentDisposition.Write
              && AscentProfile.Row("OverrideWarpToPlane").Value == "false", "");

        // ⭐ ONE autowarp flag, and all three phases read it.
        Check("S219: the autowarp flag is Core.Node.Autowarp, written true",
              AscentProfile.Row("Core.Node.Autowarp").How == AscentDisposition.Write
              && AscentProfile.Row("Core.Node.Autowarp").Value == "true", "");
        Check("S219: ...and its reason names all three phases, because it is one field",
              AscentProfile.Row("Core.Node.Autowarp").Why.Contains("ascent")
              && AscentProfile.Row("Core.Node.Autowarp").Why.Contains("node executor")
              && AscentProfile.Row("Core.Node.Autowarp").Why.Contains("rendezvous")
              && AscentProfile.Row("Core.Node.Autowarp").Why.Contains("Docking"), "");

        // §B12.7 boundary lines — MechJeb must not actuate what we own.
        Check("S219/§B12.7: SkipCircularization is written TRUE (it would place an unowned node)",
              AscentProfile.Row("SkipCircularization").How == AscentDisposition.Write
              && AscentProfile.Row("SkipCircularization").Value.Contains("true"), "");
        Check("S219/§B12.7: the two auto-deploys are written FALSE (they actuate real hardware)",
              AscentProfile.Row("AutoDeploySolarPanels").Value.Contains("false")
              && AscentProfile.Row("AutoDeployAntennas").Value.Contains("false"), "");

        // The CLASSIC path is on the screen and is never read under PSG — a decision, not an omission.
        Check("S219: the classic gravity-turn boxes are marked ClassicOnly, not silently ignored",
              AscentProfile.Row("TurnShapeExponent").How == AscentDisposition.ClassicOnly
              && AscentProfile.Row("AutoPath").How == AscentDisposition.ClassicOnly
              && AscentProfile.Row("TurnStartAltitude").How == AscentDisposition.ClassicOnly, "");

        // S214's preflight reads these two, so they cannot be inherited.
        Check("S219/S214: MinDeltaV and LastStage are written (PvgPreflight reads them)",
              AscentProfile.Row("MinDeltaV").How == AscentDisposition.Write
              && AscentProfile.Row("LastStage").How == AscentDisposition.Write, "");
    }

    // =====================================================================================
    // 3. ⭐⭐ THE COUNTDOWN ORDERING — the two numbers that must straddle our ignition lead
    // =====================================================================================
    static void CountdownTests()
    {
        // ⛔ THE ORDERING IS THE WHOLE MECHANISM:
        //   T-32 s  MechJeb's autowarp lands AND PSG is handed the target (WarpCountDown)
        //   T-10 s  the conductor clears TimedLaunch — MechJeb loses T-0 entirely (TerminalCountS)
        //   T-3  s  IgnitionGate lights the octaweb (AscentInputs.IgnitionLeadSeconds)
        // Put TerminalCountS above WarpCountDown and the warp/guidance start never happens; put it
        // below the ignition lead and MechJeb still owns T-0 when we light.
        Check("S219: WarpCountDown > TerminalCount > IgnitionLead > 0",
              AscentProfile.CountdownOrderingHolds(AscentInputs.IgnitionLeadSeconds),
              "warp=" + AscentProfile.WarpCountDownS + " terminal=" + AscentProfile.TerminalCountS
              + " ignition=" + AscentInputs.IgnitionLeadSeconds);

        // ...and the guard is not vacuous: it must REJECT each way of getting the order wrong.
        Check("S219: the ordering guard rejects an ignition lead above the terminal count",
              !AscentProfile.CountdownOrderingHolds(AscentProfile.TerminalCountS + 1.0), "");
        Check("S219: the ordering guard rejects a zero ignition lead",
              !AscentProfile.CountdownOrderingHolds(0.0), "");

        // The composed lead: 20 s (the vendored tree's own PSG cold-start note) + 12 s (WarpPlan's
        // warp-exit margin). Pinned as a NUMBER so a silent edit is a failing check, not a slower pad.
        Check("S219: WarpCountDown is the composed 20 + 12 s, not stock's 11 s",
              AscentProfile.WarpCountDownS == 32 && AscentProfile.WarpCountDownS != 11, "");

        // ⭐ It must also leave room for the solver AFTER the terminal count is taken: PSG starts
        // converging at T-WarpCountDown and IgnitionGate will not light without a solution, so the
        // convergence window is WarpCountDown - IgnitionLead and must clear the documented ~20 s.
        Check("S219: the solver gets at least the vendored tree's own ~20 s before ignition",
              AscentProfile.WarpCountDownS - AscentInputs.IgnitionLeadSeconds >= 20.0,
              "window=" + (AscentProfile.WarpCountDownS - AscentInputs.IgnitionLeadSeconds) + " s");
    }
}
