/*
 * CrewPressTest — the headless half of register S85 (the CVR press channel).
 *
 * ---- WHAT THIS SUITE CAN AND CANNOT PROVE, STATED UP FRONT ----
 * S85 has two halves and only one of them is testable here.
 *
 *  ✔ THE ID NAMESPACE AND THE BUFFER ARE PURE, and they are where the channel can actually go wrong:
 *    an id that changes name between builds silently renames a channel in every recording that
 *    follows; an id that collides merges two controls; a buffer that overflows quietly loses crew
 *    interactions the way S76's ghost columns quietly lost data. All three are asserted below,
 *    exhaustively over every value of every one of the seven dispatch types.
 *    ⚠ EIGHT SINCE S135 (2026-09-06), which added the audio page's ± buttons. The sentence above
 *    is kept as written because it is the rule, not a running total; this line carries the count.
 *
 *    ⛔ SUPERSEDED IN PLACE 2026-09-06 by S164 (C1.16 / G12) — BOTH LINES ABOVE ARE KEPT BECAUSE
 *    THEY ARE THE DEFECT. "Seven", then "eight", each written into prose by the task that added a
 *    surface; by the time S164 opened there were ELEVEN, and the two most recent — the Video page's
 *    camera rows (S134b) and the audio page's five scope illustrations (S134c) — had no pin in this
 *    file AT ALL. Nothing failed, because the suite's coverage was a hand-written list of calls in
 *    `Run` and a hand-written list of eight prefixes in `NamespaceIsFlatAndUnique`; a namer nobody
 *    listed was simply not tested. ⭐ The coverage is now ENUMERATED FROM `CrewSurface` — see
 *    `SurfacesAreAllNamed` and `IdsOf` — so a surface with no namer is a build failure and the count
 *    is computed rather than claimed. There is no running total left to keep current.
 *
 *  ⛔ IT PROVES NOTHING ABOUT THE WIRING. That the choke points are reached, that `acted` carries the
 *    dispatcher's real answer, that the record reaches `events.jsonl` — that is `ScreenPainter.cs`,
 *    `PanelButtons.cs` and `BlackBoxRecorder.cs`, all three of them `src/` GLUE, which
 *    `build.py test` does not execute at all (it compiles `src/pure` + `test` into the suite exe —
 *    `build.py:199-213`). Confirming the wiring needs the capsule and belongs to register **BB4**.
 *
 * ---- THE PINS ARE THE POINT ----
 * Every id is derived from an enum's own NAME (`CrewControlIds`, rule 2), which is the only way a
 * hand-kept table cannot drift from the code. The cost of that choice is that RENAMING an enum member
 * would silently rename a recorded channel. The pinned tables below are what turns that into a build
 * failure: they hold every member name as a literal string, in ordinal order, and assert
 *
 *      the COUNT   — so an added control fails the build until it is pinned,
 *      the NAME    — so a renamed control fails the build naming what changed,
 *      the ORDINAL — so a member INSERTED in the middle fails even if the names all still exist,
 *      the ID      — so a change to a prefix or to a mapper fails.
 *
 * A pin computed from the enum would assert nothing. These are typed out on purpose.
 */
using System;
using System.Collections.Generic;
using DragonScreen;

public static class CrewPressTest
{
    static int bad;
    static int checks;

    static void Check(bool ok, string what)
    {
        // Counted as well as asserted — a suite that prints "ok" having run zero checks is the fake
        // coverage this whole recorder exists to make impossible, one level up.
        checks++;
        if (ok) return;
        bad++;
        Console.WriteLine("  FAIL: " + what);
    }

    static void Eq(string got, string want, string what)
    {
        Check(got == want, what + " — got " + (got ?? "<null>") + ", want " + (want ?? "<null>"));
    }

    public static int Run()
    {
        bad = 0; checks = 0;
        // ⭐ S164: COUNTED, NOT CLAIMED. Three tasks in a row wrote this number into prose and the
        // fourth did not, so it read EIGHT over eleven surfaces. `CrewSurface` is the list.
        Console.WriteLine("CrewPressTest (S85 CVR press channel: the control_id namespace, exhaustively "
                          + "pinned over all " + (Enum.GetValues(typeof(CrewSurface)).Length - 1)
                          + " dispatch surfaces, + the publish-side press buffer)");

        NavIds();
        CoverIds();
        SuitIds();
        DockIds();
        AudioIds();
        VideoIds();
        HudTimerIds();
        PanelIds();
        TreeIds();
        SubsysTabIds();
        ChuteIds();
        SurfacesAreAllNamed();
        NamespaceIsFlatAndUnique();
        Buffer();

        Console.WriteLine(bad == 0 ? "  ok (" + checks + " checks)" : "  " + bad + " FAILED");
        return bad == 0 ? 0 : 1;
    }

    // =============================================================================================
    //  the pinned member tables — one per enum, ordinal order, literal strings
    // =============================================================================================

    static readonly string[] PinNavAct = { "None", "Goto", "Back", "Forward" };

    static readonly string[] PinUiPage = {
        "Cover", "Hud", "Audio", "Procedure", "Cabin",
        "Menu", "PhaseDeport", "PhaseCoast", "PhaseClaw", "PhaseManual",
        "ActOnSpaceX", "ActDeorbitBrief", "ActReview", "ActAcknowledge", "Entry",
        "Vehicle", "SuitCheck", "VehicleMech", "AudioVideo",
        "VrioTest",
        "VehicleCrew", "VehiclePropulsion", "VehiclePower",
        "VehicleAvionics", "VehicleGnc", "VehicleThermal",
        "ManualChute", "Docking",
        "Rendezvous",
        "DeorbitBurnPrep",
        "EntryProcedure",
        "SystemsTree", "SystemsPid",
        "Ascent",
        "NavOrbitPlot",
        "CrewGate" };

    static readonly string[] PinCoverButton = {
        "None", "Menu", "Back", "Forward",
        "PhaseDeport", "PhaseCoast", "PhaseClaw", "PhaseProcedure", "PhaseProcedure2",
        "PhaseReference", "PhaseManual",
        "Settings", "ActOnSpaceX", "ActDeorbitBrief", "ActReview", "ActAcknowledge",
        "EntryTrue", "EntryFalse",
        "NextView",
        "MapPanUp", "MapPanDown", "MapPanLeft", "MapPanRight", "MapCentre", "MapZoomIn", "MapZoomOut" };

    static readonly string[] PinSuitAct = { "None", "Start", "Halt", "Close", "Finish", "Retime", "Troubleshoot" };

    /// <summary>S132: Frame 58's stopwatch. Two acts and a miss.</summary>
    static readonly string[] PinHudTimer = { "None", "StartStop", "Reset" };
    static readonly string[] PinGateAct = {
        "None", "Initiate", "Step", "Go", "NoGo", "Halt", "AutoGates" };

    static readonly string[] PinAudioAct = {
        "None",
        "GroundMinus", "GroundPlus",
        "AuxMinus", "AuxPlus",
        "IntercomMinus", "IntercomPlus",
        "AlertsMinus", "AlertsPlus" };

    static readonly string[] PinDockAct = {
        "None",
        "RotRollCcw", "RotRollCw", "RotPitchUp", "RotPitchDown", "RotYawLeft", "RotYawRight", "RotMagnitude",
        "TransFwd", "TransBack", "TransUp", "TransDown", "TransLeft", "TransRight", "TransMagnitude",
        "Instructions", "ResetPositions", "Settings" };

    static readonly string[] PinPanelCommand = {
        "None",
        "Cancel", "WaterDeorbit", "DeorbitNow", "Breakout", "Execute",
        "DepressResponse", "SuppressFire", "FireResponse",
        "Power1", "String1A", "String1B", "String1C", "Reset1",
        "Power2", "String2A", "String2B", "String2C", "Reset2",
        "EnableBackupPyros", "JettisonNoseCone", "MainsOnly", "DroguesAndMains",
        "EnableEntryReboot", "CutMains", "FirePyro",
        "EnableBackupEntry", "SwapString1", "SwapString2", "SwapString3", "EnableNormalEntry",
        "Abort" };

    /// <summary>
    /// The chute procedure's twelve action rows, by the `PanelCommand` each one dispatches, in the
    /// order the crew reads them (`High` first, then `Standard`).
    ///
    /// ⭐ THIS TABLE IS ALSO THE EVIDENCE FOR WHY `chute.N` HAS TO BE AN INT. "EnableBackupPyros"
    /// appears FOUR times and "FirePyro" THREE, each with the same label and the same `Act` word — so
    /// no name in a row distinguishes it from its twin, and only the position does. Insert a step and
    /// this pin fails, naming every id that moved, which is the whole guard the int form needs.
    /// </summary>
    static readonly string[] PinChuteCommand = {
        "EnableBackupPyros", "DroguesAndMains", "FirePyro", "EnableBackupPyros", "MainsOnly", "FirePyro",
        "None", "EnableBackupPyros", "DroguesAndMains", "FirePyro", "EnableBackupPyros", "MainsOnly" };

    /// <summary>
    /// S164: the SURFACES themselves, pinned like every enum here — because until now they were the
    /// one list nothing checked. ⚠ Append-only: these ordinals go into recordings.
    /// </summary>
    static readonly string[] PinSurface = {
        "None",
        "Nav", "Cover", "Suit", "SubsysTab", "Chute", "Tree", "Dock", "Panel",
        "Audio", "Hud", "Video", "CrewGate" };

    /// <summary>
    /// Assert an enum is exactly the pinned member list: same count, same names, contiguous ordinals
    /// starting at 0. Contiguity matters because the pin is indexed BY ordinal — without it a member
    /// inserted with an explicit value would slide the whole table and still "match".
    /// </summary>
    static void PinEnum(Type t, string[] pin, string what)
    {
        Array vals = Enum.GetValues(t);
        Check(vals.Length == pin.Length,
              what + ": " + vals.Length + " members, " + pin.Length + " pinned — a control was added or "
              + "removed and the recorded channel list changed with it");
        int n = Math.Min(vals.Length, pin.Length);
        for (int i = 0; i < n; i++)
        {
            int ord = Convert.ToInt32(vals.GetValue(i));
            Check(ord == i, what + "[" + i + "] has ordinal " + ord + ", not " + i + " — the pin is "
                  + "indexed by ordinal and this enum is no longer contiguous");
            Eq(Enum.GetName(t, vals.GetValue(i)), pin[i], what + "[" + i + "] name");
        }
    }

    // =============================================================================================
    //  1-7. the seven dispatch types, exhaustively
    //  8.   — and audio since S135, which is why the banner above now says EIGHT.
    // =============================================================================================

    static void NavIds()
    {
        PinEnum(typeof(NavAct), PinNavAct, "NavAct");
        PinEnum(typeof(UiPage), PinUiPage, "UiPage");

        Check(PinUiPage.Length == FigmaUI.PageCount,
              "FigmaUI.PageCount (" + FigmaUI.PageCount + ") disagrees with the pinned UiPage list ("
              + PinUiPage.Length + ")");

        // Goto names its DESTINATION, so it is exhaustive over UiPage, not over the widgets that
        // produce it. Every page must have an id, including the ones only the Menu grid reaches.
        for (int i = 0; i < PinUiPage.Length; i++)
            Eq(CrewControlIds.Nav(NavAct.Goto, (UiPage)i), "nav.goto." + PinUiPage[i],
               "nav goto " + PinUiPage[i]);

        Eq(CrewControlIds.Nav(NavAct.Back, UiPage.Cover), "nav.back", "nav back");
        Eq(CrewControlIds.Nav(NavAct.Forward, UiPage.Cover), "nav.forward", "nav forward");
        // The history steps carry no destination, so the id must not vary with one — otherwise a
        // recording would have 35 different names for the same back chevron.
        Eq(CrewControlIds.Nav(NavAct.Back, UiPage.NavOrbitPlot), "nav.back", "nav back ignores target");
        Eq(CrewControlIds.Nav(NavAct.Forward, UiPage.Ascent), "nav.forward", "nav forward ignores target");

        Check(CrewControlIds.Nav(NavAct.None, UiPage.Cover) == null, "NavAct.None must map to null (a miss)");
        // §2.7's example, spelled out because the spec spells it out.
        Eq(CrewControlIds.Nav(NavAct.Goto, UiPage.NavOrbitPlot), "nav.goto.NavOrbitPlot",
           "§2.7's own worked example");
    }

    static void CoverIds()
    {
        PinEnum(typeof(CoverPage.CoverButton), PinCoverButton, "CoverButton");
        for (int i = 1; i < PinCoverButton.Length; i++)
            Eq(CrewControlIds.Cover((CoverPage.CoverButton)i), "cover." + PinCoverButton[i],
               "cover " + PinCoverButton[i]);
        Check(CrewControlIds.Cover(CoverPage.CoverButton.None) == null,
              "CoverButton.None must map to null (a miss)");

        // The turntable has no CoverButton of its own, and must not borrow one.
        Eq(CrewControlIds.CoverCapsule, "cover.capsule", "the Cover turntable's own id");
        for (int i = 0; i < PinCoverButton.Length; i++)
            Check(CrewControlIds.Cover((CoverPage.CoverButton)i) != CrewControlIds.CoverCapsule,
                  "cover.capsule collides with CoverButton." + PinCoverButton[i]);

        Eq(CrewControlIds.Cover(CoverPage.CoverButton.ActDeorbitBrief), "cover.ActDeorbitBrief",
           "§2.7's own worked example");

        // The seven rail rows must all be ids, because the rail is the Cover's main control and a
        // phase re-selection (acted:false) is one of the presses this channel exists to record.
        for (int ph = 0; ph < CoverPage.PhaseCount; ph++)
        {
            bool found = false;
            for (int i = 0; i < PinCoverButton.Length; i++)
                if (CoverPage.PhaseOf((CoverPage.CoverButton)i) == ph) found = true;
            Check(found, "phase rail row " + ph + " has no CoverButton, so no control_id");
        }
    }

    static void SuitIds()
    {
        PinEnum(typeof(SuitCheckPage.SuitAct), PinSuitAct, "SuitAct");
        for (int i = 1; i < PinSuitAct.Length; i++)
            Eq(CrewControlIds.Suit((SuitCheckPage.SuitAct)i), "suit." + PinSuitAct[i], "suit " + PinSuitAct[i]);
        Check(CrewControlIds.Suit(SuitCheckPage.SuitAct.None) == null, "SuitAct.None must map to null (a miss)");
        Eq(CrewControlIds.Suit(SuitCheckPage.SuitAct.Troubleshoot), "suit.Troubleshoot",
           "§2.7's own worked example");
    }

    /// <summary>
    /// S132: Frame 58's RESET / START, the eighth dispatch type.
    ///
    /// ⚠ AND ADDING IT EXPOSED A GAP IN THIS SUITE. The header says the namespace is pinned
    /// "exhaustively over every value of every one of the seven dispatch types" - but the SEVEN is a
    /// hardcoded list of calls in `Run`, and nothing checks that it is still the whole set. A new
    /// `CrewSurface` can be added with no namer at all and this suite stays green. That is logged as
    /// its own register line rather than fixed here (C1.1); this method is the pin the new channel
    /// needs either way.
    /// </summary>
    static void HudTimerIds()
    {
        PinEnum(typeof(TimerAct), PinHudTimer, "TimerAct");
        for (int i = 1; i < PinHudTimer.Length; i++)
            Eq(CrewControlIds.HudTimer((TimerAct)i), "hud." + PinHudTimer[i],
               "hud " + PinHudTimer[i]);
        Check(CrewControlIds.HudTimer(TimerAct.None) == null,
              "TimerAct.None must map to null (a miss)");
    }

    static void AudioIds()
    {
        PinEnum(typeof(SettingsAudioPage.AudioAct), PinAudioAct, "AudioAct");
        for (int i = 1; i < PinAudioAct.Length; i++)
            Eq(CrewControlIds.Audio((SettingsAudioPage.AudioAct)i), "audio." + PinAudioAct[i],
               "audio " + PinAudioAct[i]);
        Check(CrewControlIds.Audio(SettingsAudioPage.AudioAct.None) == null,
              "AudioAct.None must map to null (a miss)");

        // ⚠ THE MAP IS TOTAL OVER EIGHT BUTTONS THOUGH ONLY FOUR CAN ACT, and that is deliberate -
        // the same reason DockAct.Settings stays nameable above. The enum is the page's GEOMETRY and
        // `Available` is the owner's MAPPING; a partial map is how a control silently loses its name
        // when the mapping later widens. GROUND being a channel at all is a §1.4 question (see
        // SettingsAudioPage's header), so its two ids exist and simply never fire today.
        int names = 0;
        foreach (SettingsAudioPage.AudioAct a in Enum.GetValues(typeof(SettingsAudioPage.AudioAct)))
            if (a != SettingsAudioPage.AudioAct.None && CrewControlIds.Audio(a) != null) names++;
        Check(names == 8, "all eight audio buttons must be nameable, got " + names);

        // And the four that CAN act are exactly the owner's mapping, asked through the same predicate
        // the page tints from and the glue gates on.
        AudioLevels lv = new AudioLevels();
        lv.Valid = true; lv.Master = 0.5f; lv.Ambience = 0.5f; lv.Voice = 0.5f; lv.Ship = 0.5f;
        int live = 0;
        foreach (SettingsAudioPage.AudioAct a in Enum.GetValues(typeof(SettingsAudioPage.AudioAct)))
            if (SettingsAudioPage.Available(a, lv)) live++;
        Check(live == 4, "exactly four audio buttons are live under the owner's mapping, got " + live);

        // ---- ⛔ S164: THE FIVE SCOPE ILLUSTRATIONS, WHICH HAD NO PIN IN THIS FILE AT ALL --------
        // `CrewControlIds.AudioScope` was added by S134c and this suite never called it. Same surface
        // as the ± buttons on purpose (one page, one channel), so the ids have to be distinguishable
        // from them by their own text and not by a prefix — which is what `scope` in the id is for.
        for (int i = 0; i < SettingsAudioPage.Scopes; i++)
            Eq(CrewControlIds.AudioScope(i), "audio.scope" + i, "audio scope " + i);
        Check(CrewControlIds.AudioScope(-1) == null, "a negative scope must map to null (a miss)");
        // ⚠ THE INDEX, NOT A SEAT NUMBER — `audio.scope2` is the CABIN, and the file's own comment
        // says so. A pin that read "seat2" would be wrong for exactly one of the five.
        Eq(CrewControlIds.AudioScope(SettingsAudioPage.CabinScope), "audio.scope2",
           "the cabin scope is scope2, not a seat");
    }

    // =============================================================================================
    //  ⛔ S164: the Video page's camera rows — added by S134b, pinned by NOBODY until now
    // =============================================================================================
    static void VideoIds()
    {
        // ⚠ The row INDEX, not the camera's name: the name comes from a vessel scan and changes with
        // the craft, so `video.cam2` stays readable against a recording made on another vehicle.
        for (int i = 0; i < SettingsVideoPage.MaxRows; i++)
            Eq(CrewControlIds.VideoCam(i), "video.cam" + i, "video cam " + i);
        Check(CrewControlIds.VideoCam(-1) == null, "a negative row must map to null (a miss)");
        // The page draws at most `MaxRows`; the namespace must reach every row it can draw.
        Check(SettingsVideoPage.MaxRows > 0, "the video page draws no rows at all");
    }

    static void DockIds()
    {
        PinEnum(typeof(DockingSimPage.DockAct), PinDockAct, "DockAct");
        for (int i = 1; i < PinDockAct.Length; i++)
            Eq(CrewControlIds.Dock((DockingSimPage.DockAct)i), "dock." + PinDockAct[i], "dock " + PinDockAct[i]);
        Check(CrewControlIds.Dock(DockingSimPage.DockAct.None) == null, "DockAct.None must map to null (a miss)");
        Eq(CrewControlIds.Dock(DockingSimPage.DockAct.TransFwd), "dock.TransFwd", "§2.7's own worked example");

        // ⚠ TOTAL, BUT ONE MEMBER IS UNREACHABLE THROUGH THIS SURFACE, AND THAT IS RECORDED HERE
        // RATHER THAN LEFT TO BE DISCOVERED. `FigmaUI.HitTest` claims the Docking page's Settings rect
        // as NAVIGATION before the painter asks the page, so a real press on it is `nav.goto.Audio`
        // and `dock.Settings` can never appear in a recording. The map stays total because a partial
        // map is how a control silently loses its name later; the asymmetry is stated, not hidden.
        Check(CrewControlIds.Dock(DockingSimPage.DockAct.Settings) != null,
              "the Dock map must stay total even over the member this surface cannot reach");

        // The twelve §14.4(a) actuation pads must all be nameable: "the crew pressed a pad and nothing
        // flew" is the exact finding this channel exists to make provable.
        int act = 0;
        for (int i = 1; i < PinDockAct.Length; i++)
            if (DockingSimPage.IsActuation((DockingSimPage.DockAct)i))
            {
                act++;
                Check(CrewControlIds.Dock((DockingSimPage.DockAct)i) != null,
                      "actuation pad " + PinDockAct[i] + " has no control_id");
            }
        Check(act == 13, "expected 13 actuation acts (12 pads + Reset Positions), found " + act);
    }

    static void PanelIds()
    {
        PinEnum(typeof(PanelCommand), PinPanelCommand, "PanelCommand");
        for (int i = 1; i < PinPanelCommand.Length; i++)
            Eq(CrewControlIds.Panel((PanelCommand)i), "panel." + PinPanelCommand[i],
               "panel " + PinPanelCommand[i]);
        Check(CrewControlIds.Panel(PanelCommand.None) == null, "PanelCommand.None must map to null (a miss)");
        Eq(CrewControlIds.Panel(PanelCommand.FirePyro), "panel.FirePyro", "§2.7's own worked example");

        // An UNDEFINED value must not render as a bare number: `panel.42` would be shaped exactly like
        // `chute.7` and mean something else entirely. `PanelEntry.Command` is parsed from cfg, so this
        // is a real input, not a hypothetical one.
        Eq(CrewControlIds.Panel((PanelCommand)9999), "panel.enum_9999", "an undefined PanelCommand");

        // The six §14.4(b) inert controls must still be nameable — a press that clicks and does nothing
        // leaves no other trace anywhere in the vehicle, so the id IS the record of it.
        int inert = 0;
        for (int i = 1; i < PinPanelCommand.Length; i++)
            if (PanelPolicy.IsInert((PanelCommand)i))
            {
                inert++;
                Check(CrewControlIds.Panel((PanelCommand)i) != null,
                      "inert control " + PinPanelCommand[i] + " has no control_id");
            }
        Check(inert > 0, "no inert controls found — §14.4(b)'s list has gone missing");
    }

    static void TreeIds()
    {
        // Same enum, DIFFERENT prefix, and the difference is load-bearing: POWER 1 pressed on the glass
        // and POWER 1 pressed on the plate are two different crew acts on two different surfaces, and a
        // recording that could not tell them apart would answer "where was the crew" with a guess.
        for (int i = 1; i < PinPanelCommand.Length; i++)
        {
            Eq(CrewControlIds.Tree((PanelCommand)i), "tree." + PinPanelCommand[i], "tree " + PinPanelCommand[i]);
            Check(CrewControlIds.Tree((PanelCommand)i) != CrewControlIds.Panel((PanelCommand)i),
                  "tree and panel must not share an id for " + PinPanelCommand[i]);
        }
        Check(CrewControlIds.Tree(PanelCommand.None) == null, "PanelCommand.None must map to null on the tree too");
    }

    static void SubsysTabIds()
    {
        Eq(CrewControlIds.SubsysTab(0), "subsys.tab.0", "FUNCTIONS");
        Eq(CrewControlIds.SubsysTab(1), "subsys.tab.1", "ALERTS");
        Eq(CrewControlIds.SubsysTab(1), "subsys.tab.1", "§2.7's own worked example");
        // -1 is `ToggleHit`'s own "neither", and everything else is not a tab at all.
        Check(CrewControlIds.SubsysTab(-1) == null, "tab -1 (ToggleHit's miss) must map to null");
        Check(CrewControlIds.SubsysTab(2) == null, "there is no third tab");
        Check(CrewControlIds.SubsysTab(int.MaxValue) == null, "an absurd tab index must map to null");
    }

    static void ChuteIds()
    {
        Check(ManualChuteDeployPage.Actions.Length == PinChuteCommand.Length,
              "the chute procedure has " + ManualChuteDeployPage.Actions.Length + " action rows, "
              + PinChuteCommand.Length + " pinned — every `chute.N` after the change now names a "
              + "different row than it did in every recording already written");

        int n = Math.Min(ManualChuteDeployPage.Actions.Length, PinChuteCommand.Length);
        for (int i = 0; i < n; i++)
        {
            Eq(CrewControlIds.Chute(i), "chute." + i, "chute row " + i);
            Eq(ManualChuteDeployPage.Actions[i].Command.ToString(), PinChuteCommand[i],
               "chute row " + i + " dispatches");
        }
        Eq(CrewControlIds.Chute(7), "chute.7", "§2.7's own worked example");

        Check(CrewControlIds.Chute(-1) == null, "a chute miss (-1) must map to null");
        Check(CrewControlIds.Chute(ManualChuteDeployPage.Actions.Length) == null,
              "one past the last chute row must map to null, not name a row that does not exist");

        // The claim `CrewControlIds.Chute` makes in its own doc comment — that no NAME in a row is
        // unique, so only the position can identify one — asserted rather than asserted-in-prose.
        int dupes = 0;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (ManualChuteDeployPage.Actions[i].Label == ManualChuteDeployPage.Actions[j].Label
                    && ManualChuteDeployPage.Actions[i].Command == ManualChuteDeployPage.Actions[j].Command)
                    dupes++;
        Check(dupes > 0, "no duplicate chute rows — if every row is now unique, `chute.N` should stop "
              + "being an index and start being a name");
    }

    // =============================================================================================
    //  8. the namespace as a whole
    // =============================================================================================

    /// <summary>
    /// Every id from every surface, in one bag: flat, non-empty, and unique. A collision would merge
    /// two controls into one channel, and — unlike a rename, which the pins catch — a collision is
    /// invisible in the recording itself, because both controls would produce well-formed lines.
    /// </summary>
    // =============================================================================================
    //  ⭐ S164 — EVERY SURFACE HAS A NAMER, AND THE LIST IS `CrewSurface` ITSELF
    // =============================================================================================
    /// <summary>
    /// Every `control_id` surface <paramref name="s"/> can produce, appended to
    /// <paramref name="into"/>. Returns FALSE when this file knows no producer for the surface —
    /// which is the whole of S164: a surface added with no namer, or with a namer nobody wired into
    /// this suite, used to leave the build green.
    ///
    /// ⛔ TYPED OUT, LIKE THE PINS, AND FOR THE SAME REASON. A producer found by reflection would
    /// pass for any surface whose namer merely EXISTS; naming each one here means the person adding
    /// a surface has to say which function names it and what its full id set is, and the compiler
    /// plus the `default` below make that unavoidable. The file's own rule: *"a pin computed from
    /// the enum would assert nothing"*.
    /// </summary>
    static bool IdsOf(CrewSurface s, List<string> into)
    {
        switch (s)
        {
            case CrewSurface.Nav:
                for (int i = 0; i < PinUiPage.Length; i++) Add(into, CrewControlIds.Nav(NavAct.Goto, (UiPage)i));
                Add(into, CrewControlIds.Nav(NavAct.Back, UiPage.Cover));
                Add(into, CrewControlIds.Nav(NavAct.Forward, UiPage.Cover));
                return true;
            case CrewSurface.Cover:
                for (int i = 0; i < PinCoverButton.Length; i++) Add(into, CrewControlIds.Cover((CoverPage.CoverButton)i));
                Add(into, CrewControlIds.CoverCapsule);
                return true;
            case CrewSurface.Suit:
                for (int i = 0; i < PinSuitAct.Length; i++) Add(into, CrewControlIds.Suit((SuitCheckPage.SuitAct)i));
                return true;
            case CrewSurface.SubsysTab:
                Add(into, CrewControlIds.SubsysTab(0)); Add(into, CrewControlIds.SubsysTab(1));
                return true;
            case CrewSurface.Chute:
                for (int i = 0; i < ManualChuteDeployPage.Actions.Length; i++) Add(into, CrewControlIds.Chute(i));
                return true;
            case CrewSurface.Tree:
                for (int i = 0; i < PinPanelCommand.Length; i++) Add(into, CrewControlIds.Tree((PanelCommand)i));
                return true;
            case CrewSurface.Dock:
                for (int i = 0; i < PinDockAct.Length; i++) Add(into, CrewControlIds.Dock((DockingSimPage.DockAct)i));
                return true;
            case CrewSurface.Panel:
                for (int i = 0; i < PinPanelCommand.Length; i++) Add(into, CrewControlIds.Panel((PanelCommand)i));
                return true;
            case CrewSurface.Audio:
                // ⚠ TWO NAMERS, ONE SURFACE — the ± buttons and the five scope illustrations. A
                // surface is not one function, which is why this returns a SET rather than a name.
                for (int i = 0; i < PinAudioAct.Length; i++) Add(into, CrewControlIds.Audio((SettingsAudioPage.AudioAct)i));
                for (int i = 0; i < SettingsAudioPage.Scopes; i++) Add(into, CrewControlIds.AudioScope(i));
                return true;
            case CrewSurface.Hud:
                for (int i = 0; i < PinHudTimer.Length; i++) Add(into, CrewControlIds.HudTimer((TimerAct)i));
                return true;
            case CrewSurface.Video:
                for (int i = 0; i < SettingsVideoPage.MaxRows; i++) Add(into, CrewControlIds.VideoCam(i));
                return true;
            case CrewSurface.CrewGate:
                // ⚠ THE STEP TICK CARRIES AN INDEX, so this surface's id set is not one-per-enum-value
                // like most of the others - it is five fixed ids plus one per step the procedure can
                // show. A Go/No-Go poll is only reconstructable if the recording says WHICH line the
                // crew acknowledged, so the index is part of the id and therefore part of this pin.
                for (int i = 0; i < PinGateAct.Length; i++)
                    if ((GateAct)i != GateAct.Step) Add(into, CrewControlIds.Gate((GateAct)i, 0));
                for (int i = 0; i < CrewGatePage.MaxSteps; i++)
                    Add(into, CrewControlIds.Gate(GateAct.Step, i));
                return true;
        }
        return false;   // ⛔ including CrewSurface.None, which must have no namer at all
    }

    static void Add(List<string> into, string id) { if (id != null) into.Add(id); }

    /// <summary>The prefix a surface's ids all carry. ⛔ Typed out for the same reason as `IdsOf`,
    /// and checked against what the namers ACTUALLY produce rather than trusted.</summary>
    static string PrefixOf(CrewSurface s)
    {
        switch (s)
        {
            case CrewSurface.Nav:       return CrewControlIds.NavPrefix;
            case CrewSurface.Cover:     return CrewControlIds.CoverPrefix;
            case CrewSurface.Suit:      return CrewControlIds.SuitPrefix;
            case CrewSurface.SubsysTab: return CrewControlIds.TabPrefix;
            case CrewSurface.Chute:     return CrewControlIds.ChutePrefix;
            case CrewSurface.Tree:      return CrewControlIds.TreePrefix;
            case CrewSurface.Dock:      return CrewControlIds.DockPrefix;
            case CrewSurface.Panel:     return CrewControlIds.PanelPrefix;
            case CrewSurface.Audio:     return CrewControlIds.AudioPrefix;
            case CrewSurface.Hud:       return CrewControlIds.HudPrefix;
            case CrewSurface.Video:     return CrewControlIds.VideoPrefix;
            case CrewSurface.CrewGate:  return CrewControlIds.GatePrefix;
        }
        return null;
    }

    /// <summary>
    /// ⭐ THE S164 CHECK. Walk `CrewSurface` — not a list in this file — and require of every value
    /// except `None` that it has a producer, that the producer yields at least one id, and that every
    /// id it yields carries the prefix the surface declares. `None` must have neither.
    /// </summary>
    static void SurfacesAreAllNamed()
    {
        PinEnum(typeof(CrewSurface), PinSurface, "CrewSurface");

        Array vals = Enum.GetValues(typeof(CrewSurface));
        string unnamed = "", unprefixed = "", empty = "";
        int named = 0;
        foreach (CrewSurface sf in vals)
        {
            List<string> ids = new List<string>();
            bool has = IdsOf(sf, ids);
            if (sf == CrewSurface.None)
            {
                Check(!has, "CrewSurface.None must have NO namer — it is the `none` id and nothing else");
                Check(PrefixOf(sf) == null, "CrewSurface.None must have no prefix");
                continue;
            }
            if (!has) { unnamed += (unnamed.Length > 0 ? ", " : "") + sf; continue; }
            named++;
            if (ids.Count == 0) empty += (empty.Length > 0 ? ", " : "") + sf;
            string pre = PrefixOf(sf);
            if (string.IsNullOrEmpty(pre)) { unprefixed += (unprefixed.Length > 0 ? ", " : "") + sf; continue; }
            for (int i = 0; i < ids.Count; i++)
                Check(ids[i].StartsWith(pre, StringComparison.Ordinal),
                      "control_id '" + ids[i] + "' comes from surface " + sf + " but does not carry its "
                      + "prefix '" + pre + "' — a reader grouping by prefix would file it elsewhere");
        }
        Check(unnamed.Length == 0,
              "CrewSurface value(s) with NO namer: " + unnamed + " — a press on that surface would be "
              + "recorded with no control_id, which is exactly what S164 exists to stop");
        Check(empty.Length == 0, "CrewSurface value(s) whose namer produces no ids: " + empty);
        Check(unprefixed.Length == 0, "CrewSurface value(s) with no declared prefix: " + unprefixed);
        Check(named == vals.Length - 1,
              "every surface but None must be named: " + named + " of " + (vals.Length - 1));

        // ⚠ AND NO TWO SURFACES MAY SHARE A PREFIX, OR BE A PREFIX OF EACH OTHER. Sharing merges two
        // surfaces in every recording that groups by prefix, and the ids themselves need not collide
        // for that to happen — so the id-collision check below cannot see it.
        foreach (CrewSurface a in vals)
        {
            if (a == CrewSurface.None) continue;
            foreach (CrewSurface b in vals)
            {
                if (b == CrewSurface.None || b == a) continue;
                string pa = PrefixOf(a), pb = PrefixOf(b);
                if (pa == null || pb == null) continue;
                Check(!pa.StartsWith(pb, StringComparison.Ordinal),
                      "surface prefixes '" + pa + "' (" + a + ") and '" + pb + "' (" + b
                      + ") are not distinguishable from each other");
            }
        }
    }

    static void NamespaceIsFlatAndUnique()
    {
        List<string> all = new List<string>();
        List<string> from = new List<string>();

        // ⭐ S164: GATHERED BY SURFACE, so a surface added later joins the collision and prefix checks
        // WITHOUT anyone remembering to add a line here. That is what went wrong: `audio.`, `hud.`,
        // `video.` and the five `audio.scopeN` ids were absent from this function entirely, so a
        // collision involving any of them was invisible.
        foreach (CrewSurface sf in Enum.GetValues(typeof(CrewSurface)))
        {
            if (sf == CrewSurface.None) continue;
            List<string> ids = new List<string>();
            if (!IdsOf(sf, ids)) continue;              // reported by SurfacesAreAllNamed
            for (int i = 0; i < ids.Count; i++) { all.Add(ids[i]); from.Add(sf.ToString()); }
        }

        // 35 goto + 2 history + 25 cover + capsule + 6 suit + 17 dock + 31 panel + 31 tree + 2 tab + 12 chute
        //   — that is 162, and it was the whole count until S164.
        // ⭐ S164 adds the four sets nothing here was gathering: 8 audio ± + 5 audio scopes + 2 hud
        //   + 8 video = 23 more, so 185.
        // ⭐ S213 adds 14: ONE more `nav.goto` (the mission-sequence page joins `UiPage`) and THIRTEEN
        //   `gate.` ids — five fixed controls (Initiate, Go, NoGo, Halt, AutoGates) plus one per step the
        //   procedure can show (`CrewGatePage.MaxSteps` = 8). So 199.
        //   ⚠ The step ids are the reason this number moves when `MaxSteps` does, and that is deliberate:
        //   a procedure that can show a row the recorder cannot name is the defect S164 exists to catch.
        Check(all.Count == 199, "the namespace should hold 199 ids, it holds " + all.Count);

        Dictionary<string, string> seen = new Dictionary<string, string>();
        for (int i = 0; i < all.Count; i++)
        {
            string id = all[i];
            Check(!string.IsNullOrEmpty(id), "an empty control_id came out of " + from[i]);
            Check(id.IndexOf(' ') < 0 && id.IndexOf('"') < 0,
                  "control_id '" + id + "' has a space or a quote in it — it goes into JSON and into a "
                  + "reader's filter expressions");
            Check(id != CrewControlIds.Miss,
                  "a real control produced '" + CrewControlIds.Miss + "', which is the id reserved for a "
                  + "touch that hit NOTHING — the two would be indistinguishable in a recording");
            if (seen.ContainsKey(id)) Check(false, "control_id collision: '" + id + "' from " + seen[id]
                                                   + " and from " + from[i]);
            else seen[id] = from[i];
        }

        // Every id must be attributable to exactly one surface by its prefix alone, so a reader can
        // group a recording without a lookup table it does not have.
        // ⛔ S164: THIS LIST WAS EIGHT LONG WHILE `CrewSurface` HELD ELEVEN, so `audio.`, `hud.` and
        // `video.` ids each matched ZERO prefixes — and the check below asks for exactly one, which
        // means it would have caught them the moment they were gathered. They never were. Derived
        // from the surfaces now, so the two lists cannot drift apart again.
        List<string> prefixes = new List<string>();
        foreach (CrewSurface sf in Enum.GetValues(typeof(CrewSurface)))
        {
            if (sf == CrewSurface.None) continue;
            string pre = PrefixOf(sf);
            if (!string.IsNullOrEmpty(pre)) prefixes.Add(pre);
        }
        for (int i = 0; i < all.Count; i++)
        {
            int hits = 0;
            for (int k = 0; k < prefixes.Count; k++) if (all[i].StartsWith(prefixes[k], StringComparison.Ordinal)) hits++;
            Check(hits == 1, "control_id '" + all[i] + "' matches " + hits + " surface prefixes, not 1");
        }
    }

    // =============================================================================================
    //  9. the publish-side buffer
    // =============================================================================================

    static CrewPress P(string id)
    {
        CrewPress p = CrewPressLog.Blank();
        p.ControlId = id;
        p.Surface = CrewSurface.Panel;
        return p;
    }

    static void Buffer()
    {
        CrewPressLog.Reset();
        CrewPress[] into = new CrewPress[CrewPressLog.Capacity];

        // ---- Blank() is all-absent, not all-zero ----
        CrewPress b = CrewPressLog.Blank();
        Check(double.IsNaN(b.Ut), "Blank().Ut must be NaN — 0.0 is a real UT and would date a press to the epoch");
        Check(float.IsNaN(b.Px) && float.IsNaN(b.Py), "Blank() px/py must be NaN off-glass");
        Check(b.Screen == -1 && b.Page == -1 && b.EnumValue == -1 && b.Cmd == -1,
              "Blank() must use -1 for absent, not 0");
        Check(b.PressKind == -1 && b.Lamp == -1,
              "Blank() press_kind/lamp must be -1 — PanelPressKind.Inert and PanelLight.Dark are BOTH 0, "
              + "so a zero here would read as a real verdict on a surface that has none");
        Check(b.AlarmMask == -1 && b.SevSystem == -1,
              "Blank() alarm context must be -1 — 0 reads as 'no alarms' on a feed that was not answering");
        Check(!b.Acted, "Blank() must not claim a press acted");
        Check(b.Surface == CrewSurface.None && b.ControlId == CrewControlIds.Miss,
              "Blank() must start as a miss, so a branch that fills nothing in records a miss");

        // ---- FIFO, and every press kept ----
        CrewPressLog.Append(P("panel.FirePyro"));
        CrewPressLog.Append(P("panel.Cancel"));
        CrewPressLog.Append(P("panel.Abort"));
        Check(CrewPressLog.Count == 3, "three appends should leave three");
        int n = CrewPressLog.Drain(into);
        Check(n == 3, "drain should hand back three, handed back " + n);
        Eq(into[0].ControlId, "panel.FirePyro", "order is the order they were pressed [0]");
        Eq(into[1].ControlId, "panel.Cancel", "order is the order they were pressed [1]");
        Eq(into[2].ControlId, "panel.Abort", "order is the order they were pressed [2]");
        Check(CrewPressLog.Count == 0, "a drain empties the buffer");
        Check(CrewPressLog.Dropped == 0, "nothing was dropped");

        // A drain of an empty buffer is not an error and not a phantom record.
        Check(CrewPressLog.Drain(into) == 0, "draining an empty buffer yields nothing");

        // ---- a null control_id becomes a MISS, and loses its surface with it ----
        CrewPressLog.Reset();
        CrewPress miss = CrewPressLog.Blank();
        miss.ControlId = null;
        miss.Surface = CrewSurface.Cover;      // a branch that identified a surface but no control
        CrewPressLog.Append(miss);
        CrewPressLog.Drain(into);
        Eq(into[0].ControlId, CrewControlIds.Miss, "a null id is normalised, never appended as null");
        Check(into[0].Surface == CrewSurface.None,
              "a miss must lose its surface too — the recorder splits crew.touch from crew.press on it, "
              + "and a miss that still claimed a surface would be written as a press");

        // ---- OVERFLOW IS COUNTED, NEVER SILENT (BB1's philosophy; S76 is the counter-example) ----
        CrewPressLog.Reset();
        for (int i = 0; i < CrewPressLog.Capacity + 7; i++) CrewPressLog.Append(P("panel.Power1"));
        Check(CrewPressLog.Count == CrewPressLog.Capacity, "the buffer must not grow past Capacity");
        Check(CrewPressLog.Dropped == 7, "7 presses over capacity should count 7 dropped, counted "
              + CrewPressLog.Dropped);
        Check(CrewPressLog.Drain(into) == CrewPressLog.Capacity, "a full buffer drains Capacity entries");
        Check(CrewPressLog.Dropped == 7, "a drain must not clear the drop count — it is cumulative, so a "
              + "reader that misses one report still sees the total");

        // The OLDEST survive: what is kept is a contiguous prefix, not a window with a hole in it.
        CrewPressLog.Reset();
        CrewPressLog.Append(P("panel.Cancel"));
        for (int i = 0; i < CrewPressLog.Capacity + 3; i++) CrewPressLog.Append(P("panel.Power1"));
        CrewPressLog.Drain(into);
        Eq(into[0].ControlId, "panel.Cancel", "overflow refuses the NEWEST press, it does not evict the oldest");

        // ---- a destination too small still loses nothing SILENTLY ----
        CrewPressLog.Reset();
        for (int i = 0; i < 5; i++) CrewPressLog.Append(P("panel.Power2"));
        CrewPress[] small = new CrewPress[2];
        Check(CrewPressLog.Drain(small) == 2, "a short destination takes what fits");
        Check(CrewPressLog.Dropped == 3, "and COUNTS the rest as dropped rather than leaving them to be "
              + "re-drained out of order, counted " + CrewPressLog.Dropped);
        Check(CrewPressLog.Count == 0, "a short drain still empties the buffer");

        // A null destination is the degenerate case of the same rule.
        CrewPressLog.Reset();
        CrewPressLog.Append(P("panel.Reset1"));
        Check(CrewPressLog.Drain(null) == 0, "a null destination takes nothing");
        Check(CrewPressLog.Dropped == 1, "and counts what it could not take");

        CrewPressLog.Reset();
        Check(CrewPressLog.Count == 0 && CrewPressLog.Dropped == 0, "Reset clears both");
    }
}
