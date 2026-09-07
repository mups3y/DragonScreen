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

    // ⭐⭐ S222b — **WHAT `ApplyRODefaults()` SEEDS**, transcribed by hand from the vendored source
    // (`plugin/mech/MechJeb2/MechJebModuleAscentSettings.cs:317-395`), restricted to the names this
    // audit carries. ⛔ THIS IS THE OWNER'S RULE MADE EXECUTABLE — 2026-09-08: *"return everything back
    // to default settings … No guesses, no invented methods or 'tuning'"*, which the brief states as
    // **"If RO sets it, we do not."** A second, independent copy of the question, exactly like
    // `ExposedByTheMenus`: if a later task quietly re-adds a write for one of these, this fails.
    static readonly string[] RoSeeded =
    {
        "PitchStartHeight", "PitchRate", "DesiredAttachAlt", "DesiredAttachAltFixed", "DesiredFPA",
        "AttachAltFlag", "DesiredArgP", "DesiredArgPFlag", "LimitQa", "LimitQaEnabled", "MinDeltaV",
        "MaxCoast", "MinCoast", "LaunchLANDifference", "PreStageTime", "OptimizerPauseTime",
        "SpinupStageFlag", "SpinupStageInternal", "CoastStageFlag", "CoastStageInternal",
        "UnguidedStagesFlag", "FixedStagesFlag", "DesiredOrbitAltitude", "AscentType", "Autostage",
        "Core.Thrust.LimitToPreventUnstableIgnition", "Core.Thrust.AutoRCSUllaging",
        "Core.Thrust.MinThrottle", "Core.Thrust.LimiterMinThrottle", "Core.Thrust.LimitThrottle",
        "Core.Thrust.LimitAcceleration", "Core.Thrust.LimitToPreventOverheats",
        "Core.Thrust.LimitDynamicPressure", "Core.Thrust.MaxDynamicPressure",
    };

    // ⛔ THE ONLY THREE RO-SEEDED BOXES WE ARE STILL ALLOWED TO WRITE, AND THE OWNER NAMED ALL THREE
    // ON 2026-09-08. A fourth entry here is a build chat deciding it may deviate, which is exactly what
    // C1.8/C1.12 forbid — so the list is pinned by LENGTH as well as by content.
    static readonly string[] OwnerNamedExemptions =
    {
        "DesiredOrbitAltitude",   // "we can set the orbit to 215km"
        "AscentType",             // the KEEP list: "ascent type PSG"
        "Autostage",              // "the only change should be the auto stage being our way"
    };

    // ⭐ THE EXACT SET OF BOXES `MechConductor.Configure` MAY WRITE. Pinned by name AND by count, so
    // that adding a write — the failure mode this whole task exists to end — cannot pass silently.
    // ⚠ S235 — THIS LIST WAS `TheEightWrites` AND HELD EIGHT NAMES. SUPERSEDED IN PLACE
    // (C1.16 / G12): the eight are unchanged and a NINTH is named, `AutostageLimit`.
    // ⛔ THIS IS NOT A BUILD CHAT GRANTING ITSELF A NEW DEVIATION, and the distinction is the
    // whole reason this comment is long. The write ALREADY EXISTS: `MechConductor
    // .ApplyStagingFloor` has written `MechJebModuleStagingController.AutostageLimit` since
    // commit `5b222da` ([[S228]] R-03, authorised by the owner's 2026-09-08 *"option 2"*
    // ruling on NTSB-2026-002), and that build is INSTALLED and awaiting flight ([[S234]]).
    // ⭐⭐ What was wrong was the AUDIT: it said EIGHT while the code wrote NINE, and
    // `grep -c AutostageLimit pure/AscentProfile.cs` returned 0. [[S235]] JOB 4 exists because
    // the instrument built to catch re-seeds was blind to the one setting that bounds a
    // runaway staging cascade. Raising this pin makes an existing authorised write VISIBLE;
    // it does not add one. ⛔ A TENTH still fails here, exactly as a ninth did.
    static readonly string[] TheNineWrites =
    {
        "AscentType", "Autostage", "WarpCountDown", "SkipCircularization",
        "AutoDeploySolarPanels", "AutoDeployAntennas",
        "Core.Node.Autowarp", "Core.Warp.activateSASOnWarp",
        "AutostageLimit",   // ⭐ S228 R-03 / S235 JOB 4 — the same §B8 deviation as `Autostage`
    };

    public static int Run()
    {
        Console.WriteLine("AscentProfileTest (S219 + S222b: every ascent setting accounted for, and only eight of them written)");

        CompletenessTests();
        RoBaselineTests();
        LoadBearingValueTests();
        MenuDerivationTests();
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

        // ⛔ S222b REPLACED S219's VACUITY GUARD, AND SAYS SO. S219 asserted `Write >= 30` — "an audit
        // of all-RoDefault rows would pass every check above while deciding nothing". That guard was
        // right about the risk and became, on 2026-09-08, a ratchet holding the defect in place: the
        // owner's ruling is that 47 writes is NOT running RO's defaults. The vacuity risk is now
        // answered by NAMING the writes instead of counting them upward — an all-defaults table fails
        // `TheNineWrites` below just as loudly, and an over-writing one fails it too.
        // ⚠ S235: 77 -> 78. The added row is `AutostageLimit` (JOB 4) — a box that was always
        // being written and was simply not in the table. The pin still catches a LOST row.
        Check("S222b/S235: the audit did not lose rows while shedding writes (78 boxes)",
              AscentProfile.Audit.Length == 78, "rows=" + AscentProfile.Audit.Length);

        // ⭐ THE HEADLINE, PINNED AS A NUMBER. 47 -> 8.
        // ⚠ S235 — THIS PIN READ `== 8`. SUPERSEDED IN PLACE (C1.16 / G12). See `TheNineWrites`
        // above for why 9 is not a new deviation: the ninth has been written by deployed code
        // since `5b222da` and the audit did not know. ⛔ The headline 47 -> 8 shed is intact.
        Check("S222b/S235: exactly NINE boxes are written (47 before the 2026-09-08 directive; the 9th was already being written)",
              AscentProfile.CountOf(AscentDisposition.Write) == 9,
              "written=" + AscentProfile.CountOf(AscentDisposition.Write));
        Check("S222b: ...and it is not still 47",
              AscentProfile.CountOf(AscentDisposition.Write) != 47, "");

        // ...and they are THESE eight, by name. Adding a ninth fails here even if it is plausible.
        for (int i = 0; i < TheNineWrites.Length; i++)
            Check("S222b: '" + TheNineWrites[i] + "' is one of the eight surviving writes",
                  AscentProfile.Row(TheNineWrites[i]).How == AscentDisposition.Write,
                  "how=" + AscentProfile.Row(TheNineWrites[i]).How);

        int unexpected = 0; string first = "";
        for (int i = 0; i < AscentProfile.Audit.Length; i++)
        {
            if (AscentProfile.Audit[i].How != AscentDisposition.Write) continue;
            bool named = false;
            for (int j = 0; j < TheNineWrites.Length; j++)
                if (AscentProfile.Audit[i].Name == TheNineWrites[j]) named = true;
            if (!named) { unexpected++; if (first == "") first = AscentProfile.Audit[i].Name; }
        }
        Check("S222b: ⛔ and NOTHING ELSE is written — a new write is a new deviation (C1.8/C1.12)",
              unexpected == 0, "unexpected=" + unexpected + " first=" + first);

        // ⛔ EVERY SURVIVING WRITE MUST NAME ITS REASON, and only three reasons are admissible.
        for (int i = 0; i < AscentProfile.Audit.Length; i++)
        {
            AscentSetting r = AscentProfile.Audit[i];
            if (r.How != AscentDisposition.Write && r.How != AscentDisposition.RuntimeMission) continue;
            string w = r.Why;
            bool justified = w.Contains("MISSION FACT") || w.Contains("UI WORKFLOW")
                          || w.Contains("UI control") || w.Contains("deviation") || w.Contains("§B12.7")
                          || w.Contains("BUTTON") || w.Contains("box");
            Check("S222b: the write of '" + r.Name + "' names a control or an admissible reason",
                  justified, "why=" + (w.Length > 70 ? w.Substring(0, 70) : w));
        }

        // The destination still has to be written at runtime — four rows, no fewer.
        Check("S222b: the four mission facts are still written at runtime",
              AscentProfile.CountOf(AscentDisposition.RuntimeMission) == 4,
              "runtime=" + AscentProfile.CountOf(AscentDisposition.RuntimeMission));

        // ⭐ The two deviations that are the OWNER's to make, not a build chat's (C1.8/C1.12/C1.14).
        // ⛔ S222b did NOT close either of them: the 2026-09-08 directive settles the interim answer
        // (fly RO's default) and defers the question to T22, so they stay surfaced.
        Check("S219: PitchRate is an owner question, not a build chat's tune",
              AscentProfile.Row("PitchRate").How == AscentDisposition.OwnerQuestion, "");
        Check("S219: max-Q throttle-down is an owner question, not a build chat's tune",
              AscentProfile.Row("Core.Thrust.LimitDynamicPressure").How == AscentDisposition.OwnerQuestion, "");
        Check("S222b: there are still exactly TWO owner questions — neither was quietly closed",
              AscentProfile.CountOf(AscentDisposition.OwnerQuestion) == 2,
              "open=" + AscentProfile.CountOf(AscentDisposition.OwnerQuestion));
        Check("S219: and the render surfaces every owner question by name",
              AscentProfile.Render().Contains("PitchRate")
              && AscentProfile.Render().Contains("LimitDynamicPressure"), "");
        Check("S222b: the render also reports the UI-derived count, so the log answers the owner's question",
              AscentProfile.Render().Contains("UI-derived"), "");
    }

    // =====================================================================================
    // 2. ⭐⭐ S222b — "IF RO SETS IT, WE DO NOT." The owner's rule, executable.
    // =====================================================================================
    static void RoBaselineTests()
    {
        // Every name RO seeds must be accounted for — otherwise the transcription above has drifted
        // from the vendored source and the rule below would be silently checking nothing.
        for (int i = 0; i < RoSeeded.Length; i++)
            Check("S222b: RO-seeded '" + RoSeeded[i] + "' is a row in the audit",
                  AscentProfile.Accounted(RoSeeded[i]), "");

        // ⛔ THE RULE ITSELF.
        for (int i = 0; i < RoSeeded.Length; i++)
        {
            bool exempt = false;
            for (int j = 0; j < OwnerNamedExemptions.Length; j++)
                if (RoSeeded[i] == OwnerNamedExemptions[j]) exempt = true;
            if (exempt) continue;
            Check("S222b: ⛔ RO seeds '" + RoSeeded[i] + "', so we do NOT write it",
                  AscentProfile.Row(RoSeeded[i]).How != AscentDisposition.Write,
                  "how=" + AscentProfile.Row(RoSeeded[i]).How);
        }

        // ...and the exemption list may not grow. Three, named by the owner on 2026-09-08.
        Check("S222b: exactly THREE RO-seeded boxes are exempt, and the owner named all three",
              OwnerNamedExemptions.Length == 3, "n=" + OwnerNamedExemptions.Length);
        for (int j = 0; j < OwnerNamedExemptions.Length; j++)
            Check("S222b: the exempt '" + OwnerNamedExemptions[j] + "' really is one RO seeds",
                  System.Array.IndexOf(RoSeeded, OwnerNamedExemptions[j]) >= 0, "");

        // ⭐ The guard is not vacuous: it must actually be catching things. Most RO-seeded boxes were
        // `Write` rows before S222b, so the count of RO-seeded rows we now leave alone must be large.
        int left = 0;
        for (int i = 0; i < RoSeeded.Length; i++)
            if (AscentProfile.Row(RoSeeded[i]).How != AscentDisposition.Write) left++;
        Check("S222b: ...and the rule bites — at least 30 RO-seeded boxes are now left alone",
              left >= 30, "left=" + left);
    }

    // =====================================================================================
    // 3. ⭐ THE ROWS THE RESEARCH NAMES — each pinned to the disposition its source demands
    // =====================================================================================
    static void LoadBearingValueTests()
    {
        // ⛔ SUPERSEDED IN PLACE (C1.16), S222b. S219 asserted `LaunchLANDifference` was WRITTEN, on
        // §7.5's "0 for the exact plane". The §7.5 citation is unchanged and still right; what changed
        // is that `ApplyRODefaults()` seeds `LAUNCH_LAN_DIFFERENCE = 0` itself, so writing it was us
        // re-asserting RO's own number — the exact pattern the 2026-09-08 directive ends. The value
        // still has to BE zero, and the constant still records why, so that is what is checked now.
        Check("S222b/§7.5: LaunchLANDifference is left at RO's own zero, not re-asserted",
              AscentProfile.Row("LaunchLANDifference").How == AscentDisposition.RoDefault
              && AscentProfile.LaunchLanDifferenceDeg == 0.0, "");

        // ⛔ SUPERSEDED IN PLACE, S222b. S219 asserted `LimitQaEnabled` was WRITTEN because "the source
        // calls it mandatory". It is mandatory — and the source that says so is the MENU, which asserts
        // it every frame it draws (`MechJebModuleAscentMenu.cs:374`). That makes it UI-derived, which is
        // what it always was; the conductor copies the expression, not a chosen value.
        Check("S222b/§7.5: LimitQaEnabled is UI-DERIVED (the menu asserts it every frame)",
              AscentProfile.Row("LimitQaEnabled").How == AscentDisposition.UiDerived
              && AscentProfile.Row("LimitQaEnabled").Value.Contains("PSG"), "");

        // ⭐⭐ THE ATTACH ALTITUDE — S222b's central finding, and the reversal of an S219 assertion.
        // S219 checked that the attach altitude followed the MISSION apsis, because it believed
        // `OptimizeStageFlag` was false "by default". It is not a setting with a default at all; the
        // PSG-settings window recomputes it every frame, and S219's own `= false` write is what made
        // RO's 110 km live. Both writes are gone and the constant records the whole argument.
        Check("S222b/§7.2: the attach altitude is LEFT at RO's 110 km, because a user leaves the toggle off",
              AscentProfile.Row("DesiredAttachAltFixed").How == AscentDisposition.RoDefault
              && AscentProfile.Row("DesiredAttachAlt").How == AscentDisposition.RoDefault
              && AscentProfile.Row("AttachAltFlag").How == AscentDisposition.RoDefault
              && !AscentProfile.AttachAltFollowsMissionApsis, "");
        Check("S222b/§7.2: ...and the reversal is EXPLAINED, not merely made",
              AscentProfile.Row("AttachAltFlag").Why.Contains("AscentBuilder")
              && AscentProfile.Row("AttachAltFlag").Why.Contains("circular"), "");
        Check("S222b/§7.2: ...and OptimizeStageFlag is UI-DERIVED, not written false",
              AscentProfile.Row("OptimizeStageFlag").How == AscentDisposition.UiDerived, "");

        // ⛔ SUPERSEDED IN PLACE, S222b. S219 wrote `OverrideWarpToPlane = false` because
        // "StartCountdown branches on it; a stale true means launch NOW". The branch is real. The
        // staleness is not: it sits in MechJeb's own `/* some non-persisted values */` block, so it is
        // false at every scene load and only its own toggle can set it.
        Check("S222b: OverrideWarpToPlane is left alone — it is non-persisted and cannot be stale",
              AscentProfile.Row("OverrideWarpToPlane").How == AscentDisposition.RoDefault
              && AscentProfile.Row("OverrideWarpToPlane").Why.Contains("non-persisted"), "");

        // ⭐ ONE autowarp flag, and all three phases read it. UNCHANGED by S222b — the owner's KEEP.
        Check("S219: the autowarp flag is Core.Node.Autowarp, written true",
              AscentProfile.Row("Core.Node.Autowarp").How == AscentDisposition.Write
              && AscentProfile.Row("Core.Node.Autowarp").Value == "true", "");
        Check("S219: ...and its reason names all three phases, because it is one field",
              AscentProfile.Row("Core.Node.Autowarp").Why.Contains("ascent")
              && AscentProfile.Row("Core.Node.Autowarp").Why.Contains("node executor")
              && AscentProfile.Row("Core.Node.Autowarp").Why.Contains("rendezvous")
              && AscentProfile.Row("Core.Node.Autowarp").Why.Contains("Docking"), "");

        // §B12.7 boundary lines — MechJeb must not actuate what we own. UNCHANGED: these are the
        // second half of the owner's sanctioned deviation ("our direct part activation").
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

        // ⛔ SUPERSEDED IN PLACE, S222b. S219 wrote MinDeltaV and LastStage "because PvgPreflight reads
        // them". Reading is not a reason to write: `PvgPreflight` reads the LIVE fields, and RO seeds
        // MinDeltaV. Both are left alone, and the rows say so.
        Check("S222b/S214: MinDeltaV and LastStage are LEFT ALONE — reading a field is not writing it",
              AscentProfile.Row("MinDeltaV").How == AscentDisposition.RoDefault
              && AscentProfile.Row("LastStage").How == AscentDisposition.RoDefault, "");
        Check("S222b: ...and MinDeltaV's row says the readers take the live field",
              AscentProfile.Row("MinDeltaV").Why.Contains("LIVE"), "");

        // ⭐ The inclination: written on the free-flyer path only, and the row must say which.
        Check("S222b: DesiredInclination is a runtime mission fact with TWO paths named",
              AscentProfile.Row("DesiredInclination").How == AscentDisposition.RuntimeMission
              && AscentProfile.Row("DesiredInclination").Why.Contains("FREE-FLYER")
              && AscentProfile.Row("DesiredInclination").Why.Contains("RENDEZVOUS"), "");
    }

    // =====================================================================================
    // 4. ⭐⭐ S222b — THE PSG-SETTINGS MENU's OWN `OptimizeStageFlag` LOOP
    // =====================================================================================
    //
    // ⛔ WHY THIS MATTERS MORE THAN IT LOOKS. `OptimizeStageFlag` false is what routes
    // `SetTarget:101,109` to force `attachAltFlag` on and read RO's 110 km `DesiredAttachAltFixed` as a
    // hard terminal constraint — a "periapsis insertion" against a 215 km orbit. Derived TRUE (which is
    // what any RO user's open PSG window produces), the attach altitude is not read at all and
    // MechJebLib forces attach = periapsis for a circular target.
    static void MenuDerivationTests()
    {
        AscentProfile.PsgStage[] f9 = TwoStages(deltaV1: 3500.0, deltaV2: 5200.0);

        // The Falcon 9 + Dragon case: two real stages, RO's FixedStagesFlag false so nothing is fixed.
        Check("S222b: a two-stage vehicle above the ΔV floor derives OptimizeStageFlag TRUE",
              AscentProfile.OptimizeStageFlagFor(f9, -1, 40.0), "");
        Check("S222b: ...and the outer guard applies, so the conductor is allowed to write it",
              AscentProfile.OptimizeStageFlagApplies(f9, -1), "");

        // ⛔ EVERY WAY THE MENU WOULD SAY FALSE, one at a time — the mutation surface.
        AscentProfile.PsgStage[] tiny = TwoStages(10.0, 20.0);
        Check("S222b: every stage below MinDeltaV is filtered out -> FALSE (the menu's `continue`)",
              !AscentProfile.OptimizeStageFlagFor(tiny, -1, 40.0), "");

        AscentProfile.PsgStage[] fixedBoth = TwoStages(3500.0, 5200.0);
        fixedBoth[0].Fixed = true; fixedBoth[1].Fixed = true;
        Check("S222b: every stage pinned to a fixed burn time -> FALSE (the menu's else-branch)",
              !AscentProfile.OptimizeStageFlagFor(fixedBoth, -1, 40.0), "");

        AscentProfile.PsgStage[] oneFixed = TwoStages(3500.0, 5200.0);
        oneFixed[0].Fixed = true;
        Check("S222b: ...but ONE non-fixed stage is enough -> TRUE",
              AscentProfile.OptimizeStageFlagFor(oneFixed, -1, 40.0), "");

        // The outer guard: the menu touches the flag only when the table has rows.
        Check("S222b: an EMPTY stage table means the menu writes nothing, so neither do we",
              !AscentProfile.OptimizeStageFlagApplies(new AscentProfile.PsgStage[0], -1), "");
        Check("S222b: ...and a null table is the same answer, not a crash",
              !AscentProfile.OptimizeStageFlagApplies(null, -1), "");
        Check("S222b: LastStage above the top stage also stops the menu writing (its own outer `if`)",
              !AscentProfile.OptimizeStageFlagApplies(f9, 99), "");

        // ⭐ THE CLAMP, READ BUT NOT WRITTEN. The menu clamps LastStage into [0, top] before comparing;
        // -1 and 0 must therefore give the same answer, and a floor above a stage must exclude it.
        Check("S222b: LastStage -1 and 0 agree, because the menu clamps before it compares",
              AscentProfile.OptimizeStageFlagFor(f9, -1, 40.0)
              == AscentProfile.OptimizeStageFlagFor(f9, 0, 40.0), "");
        AscentProfile.PsgStage[] split = TwoStages(3500.0, 5200.0);
        split[1].Fixed = true;                       // stage 1 fixed, stage 0 not
        Check("S222b: a LastStage floor really does exclude the stages below it",
              !AscentProfile.OptimizeStageFlagFor(split, 1, 40.0), "");

        // ⛔ AND THE ΔV FLOOR IS THE ONE THE CALLER PASSES, not a constant baked in here.
        Check("S222b: the ΔV floor is a parameter — a higher floor changes the answer",
              AscentProfile.OptimizeStageFlagFor(f9, -1, 40.0)
              && !AscentProfile.OptimizeStageFlagFor(f9, -1, 99999.0), "");
    }

    /// <summary>Two KSP stages, 0 and 1, neither fixed — the Falcon 9 + Dragon shape.</summary>
    static AscentProfile.PsgStage[] TwoStages(double deltaV1, double deltaV2)
    {
        var s = new AscentProfile.PsgStage[2];
        s[0].KspStage = 0; s[0].DeltaVMps = deltaV1; s[0].Fixed = false;
        s[1].KspStage = 1; s[1].DeltaVMps = deltaV2; s[1].Fixed = false;
        return s;
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
