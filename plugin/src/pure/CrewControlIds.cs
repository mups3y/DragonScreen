/*
 * DragonScreen — CrewControlIds  (register S85; spec: docs/BLACKBOX_RESEARCH.md §2.7, §2.9)
 * =============================================================================================
 * PURE. §2.7's one real gap, closed: **a flat, stable `control_id` string namespace.**
 *
 * ---- THE PROBLEM THIS FILE EXISTS FOR ----
 * There is no unified command identifier in this tree, and there should not be one: the dispatch
 * inside `ScreenPainter.TouchDown` produces SEVEN disjoint types, each correct for its own surface —
 *
 *      NavHit/NavAct + UiPage              (FigmaUI)                 the bottom bar, tabs, chevrons
 *      CoverPage.CoverButton               (CoverPage)               the hub's rail, arrows, camera
 *      SuitCheckPage.SuitAct               (SuitCheckPage)           the leak check's five plates
 *      an int 0/1                          (VehicleSubsystemPage)    FUNCTIONS | ALERTS
 *      an int index into Actions[]         (ManualChuteDeployPage)   the chute procedure's rows
 *      PanelCommand                        (SystemsTreePage)         the tree's POWER/STRING nodes
 *      DockingSimPage.DockAct              (DockingSimPage)          the manual-docking clusters
 *
 * — plus `PanelCommand` again from the console plate (`PanelButtons`). A recording has to name a
 * press with ONE string, or the CVR channel is eight incompatible channels and no query spans them.
 *
 * ---- WHAT MAKES AN ID STABLE, WHICH IS THE WHOLE POINT ----
 * These ids go into recordings that OUTLIVE the code. Three rules, and all three are enforced:
 *
 *  1. **Every id is derived from an enum NAME, never an ordinal.** An ordinal moves the moment a
 *     member is inserted, and a recording written before the insert would then decode to a
 *     different control with no way to tell. This is the same rule BB9's `boost_block`/`boost_phase`
 *     follow. The one exception is `chute.N` — see `Chute()`, which says why it can only be an int.
 *  2. **The name is taken from the enum itself** (`ToString()`), never from a second table here. A
 *     hand-kept table drifts from the enum silently; deriving cannot drift.
 *  3. **Every id is PINNED, character for character, in `test/CrewPressTest.cs`.** Rule 2 makes a
 *     RENAME of an enum member silently rename a recorded channel; the pin is what makes it a build
 *     failure instead. The pin also asserts the member COUNT per enum, so an ADDED control fails the
 *     build until it is pinned too. So: rename a control and the test tells you a channel changed
 *     name; add one and the test tells you a channel is unpinned. Neither can happen quietly.
 *
 * ---- ONE SURFACE, ONE PREFIX ----
 * The prefix is the SURFACE, not the command, because "which surface did the crew use" is a question
 * a recording must answer. `PanelCommand.Power1` is reachable from two places — the console plate and
 * the SYSTEMS TREE page — and they are `panel.Power1` and `tree.Power1`, deliberately not one id.
 * The shared `PanelCommand` travels in the event payload (`cmd`), so a query that wants "every
 * POWER 1 press, whatever the crew touched" still has one field to group on.
 *
 * ---- NOT AN ACT: null ----
 * Every mapper returns **null** for its surface's "nothing was hit" value (`None`, or a negative
 * index). The choke point turns that into §2.9's `crew.touch` (a touch that resolved to no control),
 * which is a different fact from a press that did nothing — and both are facts §0's misdiagnoses
 * needed. A mapper never invents an id for a miss.
 * =============================================================================================
 */
using System;
using System.Globalization;

namespace DragonScreen
{
    /// <summary>
    /// Which surface a press landed on. Carried in the event alongside the `control_id` so a reader
    /// can filter by surface without parsing the id string, and so the surface-specific enum ORDINAL
    /// travelling beside it (§2.7: "the surface-specific enum value carried alongside") can be
    /// decoded against the right enum.
    ///
    /// Values are append-only for the same reason `UiPage`'s are: they are written into recordings.
    /// </summary>
    public enum CrewSurface : byte
    {
        /// <summary>A touch that hit no control at all.</summary>
        None = 0,
        Nav = 1,          // FigmaUI  -> NavAct + UiPage
        Cover = 2,        // CoverPage.CoverButton  (+ the capsule turntable)
        Suit = 3,         // SuitCheckPage.SuitAct
        SubsysTab = 4,    // VehicleSubsystemPage.ToggleHit -> 0/1
        Chute = 5,        // ManualChuteDeployPage.Actions index
        Tree = 6,         // SystemsTreePage.HitTest -> PanelCommand
        Dock = 7,         // DockingSimPage.DockAct
        Panel = 8         // PanelButtons / PanelMap -> PanelCommand  (the console plate, not glass)
    }

    public static class CrewControlIds
    {
        // ---- the prefixes, named once so a typo is a compile error and not a split channel ----
        public const string NavPrefix   = "nav.";
        public const string CoverPrefix = "cover.";
        public const string SuitPrefix  = "suit.";
        public const string TabPrefix   = "subsys.tab.";
        public const string ChutePrefix = "chute.";
        public const string TreePrefix  = "tree.";
        public const string DockPrefix  = "dock.";
        public const string PanelPrefix = "panel.";

        /// <summary>
        /// The `control_id` a touch that hit nothing carries. NOT null — the event is written with a
        /// real value so a reader never has to distinguish "field absent" from "field null", and
        /// §2.9's `crew.touch` is precisely the record of a press that found no control.
        /// </summary>
        public const string Miss = "none";

        /// <summary>
        /// The Cover hub's capsule sprite, which is a DRAG target rather than a button — it has no
        /// `CoverButton` value at all (`CoverPage.CapsuleHit` answers it separately, after every
        /// button has declined the touch). It still gets an id, because "the crew grabbed the
        /// turntable" is a real interaction and leaving it out would make the Cover page's press
        /// record silently incomplete.
        /// </summary>
        public const string CoverCapsule = CoverPrefix + "capsule";

        // =========================================================================================
        //  the seven types
        // =========================================================================================

        /// <summary>
        /// FigmaUI navigation. `Goto` names its DESTINATION (`nav.goto.NavOrbitPlot`) rather than the
        /// widget that was touched, because the bottom bar, the vehicle tab strip, the Menu grid, the
        /// deep-view links and the letterbox affordances all produce the same act and a recording
        /// wants "where did the crew go". The two history steps have no target, so they are bare.
        /// </summary>
        public static string Nav(NavAct act, UiPage target)
        {
            switch (act)
            {
                case NavAct.Goto:    return NavPrefix + "goto." + Name((int)target, typeof(UiPage));
                case NavAct.Back:    return NavPrefix + "back";
                case NavAct.Forward: return NavPrefix + "forward";
                default:             return null;   // NavAct.None — nothing was claimed
            }
        }

        /// <summary>The Cover hub's own buttons: the seven-slot phase rail, the ◄/► arrows, MENU,
        /// SETTINGS, the four action rows, the two entry plates, NEXT VIEW and the map cluster.</summary>
        public static string Cover(CoverPage.CoverButton b)
        {
            if (b == CoverPage.CoverButton.None) return null;
            return CoverPrefix + Name((int)b, typeof(CoverPage.CoverButton));
        }

        /// <summary>The Suit Leak Check's plates. `Troubleshoot` is the one whose press can be
        /// REFUSED by the model (S32's `SuitCheckPage.Available`) — the id is the same either way and
        /// the event's `acted` carries the refusal, which is exactly the distinction §2.9 wants.</summary>
        public static string Suit(SuitCheckPage.SuitAct a)
        {
            if (a == SuitCheckPage.SuitAct.None) return null;
            return SuitPrefix + Name((int)a, typeof(SuitCheckPage.SuitAct));
        }

        /// <summary>
        /// The FUNCTIONS | ALERTS toggle shared by the six subsystem sub-pages.
        ///
        /// ⚠ AN INT, AND STABLE ANYWAY. `VehicleSubsystemPage.ToggleHit` returns 0 or 1 and its own
        /// doc-comment fixes the meaning — "0 FUNCTIONS, 1 ALERTS". Those two words are the tab strip
        /// the reference draws; there is no third and no reordering that would not also be a redesign
        /// of the page. So the ordinal here is not a position in a list that can grow, it is a
        /// two-valued state with named halves, and the id spells the number the page speaks.
        /// </summary>
        public static string SubsysTab(int tab)
        {
            if (tab != 0 && tab != 1) return null;
            return TabPrefix + tab.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A row of the Manual Chute Deploy procedure.
        ///
        /// ⛔ THE ONE ID THAT CAN ONLY BE AN INT, AND WHY. `ManualChuteDeployPage.Actions` is built at
        /// class-init by walking the two step tables in the order the crew READS them, and nothing in
        /// a row is unique: "ENABLE BACKUP PYROS" appears FOUR times and "FIRE PYRO" THREE, each with
        /// the same label, the same `Act` word and the same `PanelCommand` (the pinned table in
        /// `test/CrewPressTest.cs` is the proof of those counts). The only thing that tells the
        /// second ENABLE BACKUP PYROS from the first is WHERE IN THE PROCEDURE IT IS — which is the
        /// index. So `chute.7` names a position, and a step inserted above it renumbers everything
        /// after it.
        ///
        /// That is a real stability limit and it is handled rather than hidden: the pinned test
        /// asserts `Actions.Length` and every id, so inserting a step is a BUILD FAILURE that says
        /// which ids moved; and the event payload carries the row's `PanelCommand` (`cmd`) beside the
        /// id, so an old recording is still readable against the source revision the manifest names.
        /// </summary>
        public static string Chute(int actionIndex)
        {
            if (actionIndex < 0 || actionIndex >= ManualChuteDeployPage.Actions.Length) return null;
            return ChutePrefix + actionIndex.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The SYSTEMS TREE page's eight POWER/STRING nodes. Same `PanelCommand` the plate
        /// dispatches, deliberately a DIFFERENT id — see the "one surface, one prefix" note above.</summary>
        public static string Tree(PanelCommand c)
        {
            if (c == PanelCommand.None) return null;
            return TreePrefix + Name((int)c, typeof(PanelCommand));
        }

        /// <summary>
        /// The manual-docking page: six rotation pads, six translation pads, the two magnitude
        /// toggles, INSTRUCTIONS, RESET POSITIONS and SETTINGS.
        ///
        /// ⚠ `dock.Settings` is defined here because the map must be total over the enum, but it can
        /// never be REACHED through this surface: `FigmaUI.HitTest` claims that rect as navigation
        /// before the painter ever asks the page (`FigmaUI.cs`, the Docking branch), so the press is
        /// recorded as `nav.goto.Audio`. Stated rather than left to be discovered, because a channel
        /// that can never fire is exactly the defect this register line exists to stop shipping.
        /// </summary>
        public static string Dock(DockingSimPage.DockAct a)
        {
            if (a == DockingSimPage.DockAct.None) return null;
            return DockPrefix + Name((int)a, typeof(DockingSimPage.DockAct));
        }

        /// <summary>The console plate — the physical buttons, `PanelButton.OnMouseDown`.</summary>
        public static string Panel(PanelCommand c)
        {
            if (c == PanelCommand.None) return null;
            return PanelPrefix + Name((int)c, typeof(PanelCommand));
        }

        // =========================================================================================

        /// <summary>
        /// The enum member's own NAME, or `enum_N` when the value is not a defined member.
        ///
        /// The fallback is not decoration. `PanelCommand` reaches `Panel()` from `PanelEntry`, which
        /// is parsed from cfg, and `Enum.ToString()` on an undefined value renders the NUMBER — which
        /// would produce `panel.42`, an id indistinguishable in shape from `chute.7` and meaning
        /// something entirely different. Naming the unknown case keeps every id parseable and keeps
        /// the one legitimately-numeric namespace (`chute.`) unambiguous.
        /// </summary>
        static string Name(int value, Type t)
        {
            string n = Enum.GetName(t, value);
            if (n != null) return n;
            return "enum_" + value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
