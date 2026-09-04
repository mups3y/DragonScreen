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
        Console.WriteLine("CrewPressTest (S85 CVR press channel: the control_id namespace, exhaustively "
                          + "pinned over all seven dispatch types, + the publish-side press buffer)");

        NavIds();
        CoverIds();
        SuitIds();
        DockIds();
        PanelIds();
        TreeIds();
        SubsysTabIds();
        ChuteIds();
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
        "NavOrbitPlot" };

    static readonly string[] PinCoverButton = {
        "None", "Menu", "Back", "Forward",
        "PhaseDeport", "PhaseCoast", "PhaseClaw", "PhaseProcedure", "PhaseProcedure2",
        "PhaseReference", "PhaseManual",
        "Settings", "ActOnSpaceX", "ActDeorbitBrief", "ActReview", "ActAcknowledge",
        "EntryTrue", "EntryFalse",
        "NextView",
        "MapPanUp", "MapPanDown", "MapPanLeft", "MapPanRight", "MapCentre", "MapZoomIn", "MapZoomOut" };

    static readonly string[] PinSuitAct = { "None", "Start", "Halt", "Close", "Finish", "Retime", "Troubleshoot" };

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
    static void NamespaceIsFlatAndUnique()
    {
        List<string> all = new List<string>();
        List<string> from = new List<string>();

        Action<string, string> add = delegate(string id, string src)
        {
            if (id == null) return;
            all.Add(id); from.Add(src);
        };

        for (int i = 0; i < PinUiPage.Length; i++) add(CrewControlIds.Nav(NavAct.Goto, (UiPage)i), "nav.goto");
        add(CrewControlIds.Nav(NavAct.Back, UiPage.Cover), "nav");
        add(CrewControlIds.Nav(NavAct.Forward, UiPage.Cover), "nav");
        for (int i = 0; i < PinCoverButton.Length; i++) add(CrewControlIds.Cover((CoverPage.CoverButton)i), "cover");
        add(CrewControlIds.CoverCapsule, "cover");
        for (int i = 0; i < PinSuitAct.Length; i++) add(CrewControlIds.Suit((SuitCheckPage.SuitAct)i), "suit");
        for (int i = 0; i < PinDockAct.Length; i++) add(CrewControlIds.Dock((DockingSimPage.DockAct)i), "dock");
        for (int i = 0; i < PinPanelCommand.Length; i++) add(CrewControlIds.Panel((PanelCommand)i), "panel");
        for (int i = 0; i < PinPanelCommand.Length; i++) add(CrewControlIds.Tree((PanelCommand)i), "tree");
        add(CrewControlIds.SubsysTab(0), "subsys"); add(CrewControlIds.SubsysTab(1), "subsys");
        for (int i = 0; i < ManualChuteDeployPage.Actions.Length; i++) add(CrewControlIds.Chute(i), "chute");

        // 35 goto + 2 history + 25 cover + capsule + 6 suit + 17 dock + 31 panel + 31 tree + 2 tab + 12 chute
        Check(all.Count == 162, "the namespace should hold 162 ids, it holds " + all.Count);

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
        string[] prefixes = { CrewControlIds.NavPrefix, CrewControlIds.CoverPrefix, CrewControlIds.SuitPrefix,
                              CrewControlIds.TabPrefix, CrewControlIds.ChutePrefix, CrewControlIds.TreePrefix,
                              CrewControlIds.DockPrefix, CrewControlIds.PanelPrefix };
        for (int i = 0; i < all.Count; i++)
        {
            int hits = 0;
            for (int k = 0; k < prefixes.Length; k++) if (all[i].StartsWith(prefixes[k])) hits++;
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
