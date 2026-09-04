// ─────────────────────────────────────────────────────────────────────────────────────────
//  MechProfile — the HEADLESS half of hosting the embedded MechJeb (register T15b, §B12.1).
//
//  WHY A PURE FILE FOR WHAT LOOKS LIKE A CONFIG STRING. `MechJebCore.blacklist` is a
//  [KSPField] tested as `!blacklist.Contains(t.Name)` — a SUBSTRING match on the type name,
//  not a list and not a wildcard (plugin/mech/MechJeb2/MechJebCore.cs:753). A substring rule
//  refuses more than it names: any module whose name is a substring of ANOTHER entry is
//  refused too, silently, at construction. Measured on the pinned tree, the naive
//  "blacklist every window" list would ALSO have refused four modules nobody named:
//
//      MechJebModuleAscentSettingsMenu       also refuses  MechJebModuleAscentSettings
//      MechJebModuleRCSBalancerWindow        also refuses  MechJebModuleRCSBalancer
//      MechJebModuleFlightRecorderGraph      also refuses  MechJebModuleFlightRecorder
//      MechJebModuleRendezvousAutopilotWindow also refuses MechJebModuleRendezvousAutopilot
//
//  Two of those four (AscentSettings, RCSBalancer) are assigned straight into MechJebCore's
//  own fields at the end of LoadComputerModules — `core.AscentSettings`, `core.Rcsbal` —
//  so refusing them leaves the core holding nulls that the PVG ascent then dereferences.
//  That failure is invisible in a diff and only shows up in the capsule, so the rule lives
//  here where `build.py test` can prove it every build, against the ACTUAL pinned tree
//  rather than against a list somebody typed. See test/MechHostTest.cs.
//
//  NOTHING KSP OR UNITY IN THIS FILE — pure/glue split. The glue is src/MechHost.cs.
// ─────────────────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace DragonScreen.Pure
{
    /// <summary>
    /// The headless, testable rules for the one embedded <c>MechJebCore</c>: which modules are
    /// refused at construction, which must never be, and how MechJeb's substring blacklist
    /// actually behaves.
    /// </summary>
    public static class MechProfile
    {
        // ── THE BLACKLIST, and why each of the six is on it ──────────────────────────────
        //
        // ⚠ THIS IS NOT "every window". It CANNOT be — see MustSurvive below: four of the
        // window modules are dereferenced without a null check from code that runs every
        // frame, so refusing them turns a cosmetic problem into a NullReferenceException.
        // GUI suppression is therefore done at the ENABLE level (MechHost.GoHeadless), and
        // this list is the narrower, provable job: refuse the modules that are (a) safe to
        // refuse — nothing dereferences an instance of them — (b) collision-free under the
        // substring rule, and (c) carry no values in the shipped tune, so refusing them
        // drops no tuning.
        //
        //   MechJebModuleDebugArrows       ctor sets Enabled = true, so it RUNS by default,
        //                                  and it draws world-space debug arrows in the
        //                                  flight scene — visible output, not a window.
        //                                  Its two external uses (MechJebModuleDockingAutopilot
        //                                  :243-244) are STATIC field writes and need no
        //                                  instance. It also renders with shaders from the
        //                                  asset bundle this build deliberately does not load
        //                                  (see plugin/mech/_dragonscreen/_BundlesManager.cs),
        //                                  so leaving it constructed means magenta arrows.
        //   MechJebModuleAttitudeAdjustment  PID tuning window. No external instance use.
        //   MechJebModuleDockingGuidance     window over MechJebModuleDockingAutopilot, which
        //                                  is a separate module and STAYS (§B12.3 makes the
        //                                  docking AP the default). Only the window goes.
        //   MechJebModuleNodeEditor          manoeuvre-node editing window. The conductor edits
        //                                  nodes through the planner API, not this.
        //   MechJebModuleRendezvousGuidance  window over the rendezvous autopilot, which §B1
        //                                  rules out for RSS/RO anyway. No external use.
        //   MechJebModuleSmartRcs            RCS window. No external use.
        //
        // NOT here, deliberately: MechJebModuleWarpHelper. It is a window and would otherwise
        // qualify, but it is one of the eleven nodes carrying actual values in the shipped
        // Crew-Dragon tune — refusing it would silently drop them (§B12.1: the tune is the
        // point). Suppressed by enable, like the rest.
        public const string Blacklist =
            "MechJebModuleDebugArrows,MechJebModuleAttitudeAdjustment,MechJebModuleDockingGuidance," +
            "MechJebModuleNodeEditor,MechJebModuleRendezvousGuidance,MechJebModuleSmartRcs";

        /// <summary>The six modules <see cref="Blacklist"/> is meant to refuse — and, per the
        /// test, the ONLY six it does refuse on the pinned tree.</summary>
        public static readonly string[] Refused =
        {
            "MechJebModuleAttitudeAdjustment",
            "MechJebModuleDebugArrows",
            "MechJebModuleDockingGuidance",
            "MechJebModuleNodeEditor",
            "MechJebModuleRendezvousGuidance",
            "MechJebModuleSmartRcs"
        };

        // ── WHAT MUST NEVER BE REFUSED ──────────────────────────────────────────────────
        //
        // Two separate reasons, kept in one list because the consequence is identical (a
        // null dereference in the capsule, invisible here):
        //
        // (a) MechJebCore assigns it to one of its own fields at the tail of
        //     LoadComputerModules (MechJebCore.cs:766-786). A refused module leaves that
        //     field null and every user of it throws.
        // (b) Something dereferences GetComputerModule<T>() WITHOUT a null check from a path
        //     that runs whether or not any GUI is shown:
        //       MechJebModuleMenu               MechJebCore.Update():680 — every frame, editor included
        //       MechJebModuleCustomWindowEditor MechJebCore.OnLoad():911 — every part load
        //       MechJebModuleThrustWindow       MechJebModuleThrustController.cs:357,792
        //       MechJebModuleAscentMenu         MechJebModuleThrustController.cs:357,792
        //     ⚠ Those four are why "blacklist the GUI" is not a strategy. The thrust
        //     controller is live flight code and reads `.Hidden` off two window modules.
        public static readonly string[] MustSurvive =
        {
            // (a) core fields
            "MechJebModuleAttitudeController", "MechJebModuleStagingController",
            "MechJebModuleThrustController", "MechJebModuleTargetController",
            "MechJebModuleWarpController", "MechJebModuleRCSController",
            "MechJebModuleRCSBalancer", "MechJebModuleRoverController",
            "MechJebModuleNodeExecutor", "MechJebModuleSolarPanelController",
            "MechJebModuleDeployableAntennaController", "MechJebModuleLandingAutopilot",
            "MechJebModuleSettings", "MechJebModuleGuidanceController",
            "MechJebModulePSGGlueBall", "MechJebModuleStageStats",
            "MechJebModuleAscentSettings", "MechJebModuleSpinupController",
            "MechJebModuleHoverslamSimulation", "MechJebModuleSmartASS",
            // (b) unguarded dereferences from non-GUI paths
            "MechJebModuleMenu", "MechJebModuleCustomWindowEditor",
            "MechJebModuleThrustWindow", "MechJebModuleAscentMenu",
            // near-misses the substring rule would eat if the list above grew carelessly
            "MechJebModuleFlightRecorder", "MechJebModuleRendezvousAutopilot"
        };

        // ── THE SHIPPED TUNE ────────────────────────────────────────────────────────────
        //
        // ⚠ WHICH PROFILE THIS IS, stated once so T22 cannot inherit a confusion.
        // `mechjeb_settings_type_Crew-Dragon.cfg` is the TUNED Crew-2 profile — §B5's
        // TUNING TARGET, NOT the flight-1 baseline. §B5's two-profile split is explicit:
        // "flight 1 loads RO's OWN shipped MechJeb defaults for every ascent-shaping /
        // attitude / throttle / staging KNOB … NOT the Crew-2 cfg's already-researched
        // values … those are DEMOTED to the §B5 tune's TARGET". The one exception §B5 names
        // is the target-orbit values (inclination, altitude), which are MISSION FACTS and
        // correct under either profile.
        //
        // T15b builds the LOADER §B12.1 asks for ("the conductor loads it, never the user")
        // and ships the file inside the mod. It does not decide which profile flight 1 flies
        // — that is T22's, and MechHost.tuneFile is the seam it turns. Nothing flies today
        // either way: §14.4(a) holds, the screens' flight commands are still an honest no-op.
        public const string TuneFileName = "mechjeb_settings_type_Crew-Dragon.cfg";

        // ── HOW MANY MODULES THE TUNE ACTUALLY TOUCHES ──────────────────────────────────
        //
        // ⚠ T15d, RESOLVING T15b's GLASS ROW 3. That row said the log line
        // "MechJeb tune applied from the mod: … N module(s)" should read N = 11. On the glass it
        // read 51, and the CHECKLIST was the thing that was wrong, not the loader: 11 and 51 are
        // two different quantities and the row asked for the one MechHost was not printing.
        //
        //   MechHost.ApplyTune counts a module when `type.HasNode(module.GetType().Name)` - i.e.
        //   every CONSTRUCTED module whose type name appears as a node, empty node or not. The
        //   shipped cfg has 64 top-level nodes; only ELEVEN of them carry any values at all, and
        //   the other 53 are empty shells an older MechJeb wrote out.
        //
        //   51 = 41 + 10, and both halves are derived, not guessed:
        //     41  distinct node names that resolve to a non-abstract ComputerModule in the pinned
        //         tree, minus the six the Blacklist refuses construction to, minus
        //         MechJebModuleCustomInfoWindow - which LoadComputerModules hard-excludes from
        //         auto-construction alongside the four abstract bases (MechJebCore.cs:751-753);
        //     10  MechJebModuleCustomInfoWindow INSTANCES, which are not auto-constructed but are
        //         created one per CreateWindowFromSharingString call in
        //         MechJebModuleCustomWindowEditor.AddDefaultWindows (MechJebModuleCustomInfoWindow
        //         .cs:697-712), reached from MechJebCore.OnLoad:911-914. Each is a separate module
        //         with the same type name, so each matches the cfg's node and each is counted.
        //
        // Both numbers are re-derived from the cfg and from the vendored tree by
        // test/MechHostTest.cs on every build, so a re-pin that adds a module, renames one, or
        // changes AddDefaultWindows fails the suite instead of quietly moving the number the
        // capsule is asked to check.

        /// <summary>
        /// Nodes in the shipped tune that carry values - THE number that means "the tune landed".
        /// The other 53 nodes are empty and change nothing.
        /// </summary>
        public const int TuneNodesCarryingValues = 11;

        /// <summary>
        /// <c>MechJebModuleCustomInfoWindow</c> instances <c>AddDefaultWindows</c> creates. Not
        /// auto-constructed modules - windows, made one per sharing string, all disabled on
        /// creation and held disabled by MechHost.GoHeadless.
        /// </summary>
        public const int DefaultCustomWindows = 10;

        /// <summary>
        /// What <c>MechHost.ApplyTune</c>'s "N module(s) matched a node" should read: 41 distinct
        /// constructed module types named by the cfg, plus one per
        /// <see cref="DefaultCustomWindows"/>. This is a COVERAGE number, not a success number -
        /// <see cref="TuneNodesCarryingValues"/> is the one to read as success.
        /// </summary>
        public const int ExpectedTuneModulesApplied = 51;

        /// <summary>
        /// Node names in the shipped Crew-Dragon tune that match no module in the pinned
        /// tree, with the reason. ALL EIGHT ARE EMPTY NODES — checked value by value — so
        /// nothing is lost today. They are pinned here so that a re-pin which renames a
        /// module that DOES carry values fails the test instead of silently dropping them.
        /// </summary>
        public static readonly string[] KnownOrphanTuneNodes =
        {
            // The cfg was written by an older MechJeb that called the powered-guidance
            // modules PVG; the pinned dev commit renames them PSG. Empty in this cfg.
            "MechJebModuleAscentPVGAutopilot",     // now MechJebModuleAscentPSGAutopilot
            "MechJebModuleAscentPVGSettingsMenu",  // now MechJebModuleAscentPSGSettingsMenu
            "MechJebModulePVGGlueBall",            // now MechJebModulePSGGlueBall
            // Modules upstream has since removed outright. Empty in this cfg.
            "MechJebModuleAirplaneAutopilot", "MechJebModuleAirplaneGuidance",
            "MechJebModuleSpaceplaneAutopilot", "MechJebModuleSpaceplaneGuidance",
            "MechJebModuleSuicideTimer"
        };

        // ── THE RULE ITSELF ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Exactly what <c>MechJebCore.LoadComputerModules</c> does at :753 —
        /// <c>blacklist.Contains(t.Name)</c> — evaluated over a set of type names. Returns the
        /// names that would be REFUSED CONSTRUCTION, sorted, including any the author did not
        /// intend to name.
        /// </summary>
        public static List<string> CaughtBy(string blacklist, IEnumerable<string> typeNames)
        {
            var hit = new List<string>();
            if (typeNames == null) return hit;
            string bl = blacklist ?? "";
            foreach (string n in typeNames)
            {
                // MechJeb also hard-excludes the four abstract bases + the custom info window
                // before it consults the blacklist; an empty name can never be meant.
                if (string.IsNullOrEmpty(n)) continue;
                if (bl.Length > 0 && bl.IndexOf(n, StringComparison.Ordinal) >= 0) hit.Add(n);
            }
            hit.Sort(StringComparer.Ordinal);
            return hit;
        }
    }
}
