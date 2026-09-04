/*
 * DragonScreen - MechHost
 *
 * ONE embedded MechJebCore, on the Dragon part, HEADLESS. Register task T15b; §B12.1 / §B12.1a.
 *
 * §B12.1: "Drive HEADLESS: attach/find ONE MechJebCore on the Dragon part, no GUI, enable only
 * the modules the conductor uses. Reach modules via MechJebCore.GetComputerModule<T>()."
 * §B12.1a: "HEADLESS IS MANDATORY EVEN THOUGH THE UI IS PORTED … a user who already runs MechJeb
 * would otherwise get TWO MechJeb UIs, one of which they cannot configure and must not touch …
 * Ported != enabled."
 *
 * ══ THE FIVE THINGS THIS FILE DOES, AND WHY EACH IS NOT OPTIONAL ═══════════════════════════
 *
 * (1) A DISTINCT PartModule NAME.  §B3's private namespace stops a CLR TYPE clashing with a
 *     user's MechJeb2.dll. It does NOT stop the OTHER collision, which is one level up and was
 *     found by T15b: KSP resolves `MODULE { name = … }` in a part cfg by CLASS NAME across every
 *     loaded assembly, and after the namespace wrap our class is still called `MechJebCore`. A
 *     cfg node naming `MechJebCore` on a machine that has the real MechJeb2 is therefore
 *     ambiguous, and whichever assembly the loader reaches first wins - which for a user with
 *     MechJeb installed could hand our Dragon THEIR core, full UI and all. That is the exact
 *     failure §B12.1a forbids, arriving by a route the namespace does not cover.
 *     So the shipped cfg names `DragonMechJebCore`, a subclass, and the ambiguity is gone in
 *     both directions: nothing of theirs can satisfy our node and nothing of ours can satisfy
 *     one of theirs.
 *
 * (2) NO SETTINGS WRITE, EVER.  MuUtils hardcodes MechJeb's settings directory to
 *     <KSP>/GameData/MechJeb2/Plugins/PluginData/MechJeb2 (MuUtils.cs:19), and MechJebCore.Update
 *     calls OnSave(null) EVERY FIVE SECONDS (MechJebCore.cs:648-651), which writes
 *     mechjeb_settings_global.cfg and mechjeb_settings_type_<vessel>.cfg to that path. Left
 *     alone, an embedded core would rewrite a user's real MechJeb configuration every five
 *     seconds of flight, and create a phantom GameData/MechJeb2 tree for a user who has none.
 *     §B12.1's "the conductor loads it, never the user" is about reading; writing over their
 *     settings is worse. The override below drops exactly the file-writing call and keeps the
 *     stock craft-file save, because in MechJebCore.OnSave the `type`/`global` nodes are null
 *     whenever sfsNode is non-null (MechJebCore.cs:961-963) - so base.OnSave(node) with a real
 *     node cannot touch a file.
 *
 * (3) SUPPRESSION AT THE ENABLE LEVEL, not (only) the blacklist.  The obvious lever is
 *     MechJebCore's `blacklist` [KSPField], which refuses modules at CONSTRUCTION. It is real and
 *     it is used - see pure/MechProfile.cs - but it CANNOT carry the GUI job, and T15b's central
 *     measurement is why: the four modules that actually produce the visible UI are dereferenced
 *     with no null check from paths that run whether or not anything is drawn.
 *         MechJebModuleMenu               MechJebCore.Update():680          - every frame
 *         MechJebModuleCustomWindowEditor MechJebCore.OnLoad():911          - every part load
 *         MechJebModuleThrustWindow       MechJebModuleThrustController:357 - live flight code
 *         MechJebModuleAscentMenu         MechJebModuleThrustController:357 - live flight code
 *     Blacklisting those turns "a window appears" into "a NullReferenceException every frame".
 *     So they are CONSTRUCTED and then held OFF: MechJebCore.OnGUI draws a DisplayModule only
 *     `if (module.Enabled)` (MechJebCore.cs:1153), and the app-launcher button is gated on the
 *     menu's own `useAppLauncher` (MechJebModuleMenu.cs:257). Both are reachable through
 *     MechJeb's supported API, so nothing here reaches inside a vendored file.
 *
 * (4) THE TUNE, LOADED FROM THE MOD.  §B12.1: ship mechjeb_settings_type_Crew-Dragon.cfg inside
 *     the mod and have the conductor load it, never the user. It ships at
 *     GameData/DragonScreen/PluginData/ - under PluginData deliberately, because KSP does not
 *     parse .cfg files there into the game database, so a MechJeb settings file cannot be
 *     mistaken for a part patch.
 *     ⚠ WHICH PROFILE IT IS: the TUNED Crew-2 profile - §B5's TUNING TARGET - NOT the flight-1
 *     baseline, which §B5 says is RSS-RO's own shipped MechJeb defaults. See MechProfile's note.
 *     T15b builds the loader; T22 decides what flight 1 applies, and `tuneFile` is that seam.
 *
 * (5) NO INHERITED ACTION-GROUP ENTRIES.  Task T15b's glass row 7 found that MechJeb's own
 *     `[KSPAction]` methods - orbit prograde, translatron, land at KSC and the rest - appear in
 *     the Dragon's VAB action-group assignment list, because KSP builds that list by reflection
 *     over every PartModule on the part, and DragonMechJebCore inherits all of them from
 *     MechJebCore. Owner ruling, 2026-09-05, verbatim: "remove vab action group list".
 *     ⚠ NOT a vendored-file edit. There are exactly 18 `[KSPAction(` declarations in the whole
 *     vendored tree (grep `\[KSPAction\(`, not `ActionGroups[KSPActionGroup.X]` - the latter
 *     matches ten files and is a different thing entirely), all 18 in MechJebCore.cs from :112,
 *     and MechJebCore never reads its own `Actions` field anywhere - it only declares the marked
 *     methods. So emptying `Actions` on OUR subclass instance changes nothing MechJeb depends on;
 *     it only removes KSP's UI-side record of which methods a player may bind. Removing the
 *     attributes themselves would mean editing MechJebCore.cs, which §B12.1's rename-shell rule
 *     forbids - clearing the inherited list on `DragonMechJebCore` needs no such edit and touches
 *     no file under `plugin/mech/`.
 *     Done in `OnStart`, not `OnAwake`: KSP populates a module instance's `Actions` from its
 *     `[KSPAction]` methods when the instance is attached to the part, which happens before that
 *     instance's `OnStart` runs - so by `OnStart` the list already exists and is safe to clear.
 *     `OnStart` runs once per module INSTANCE, and a fresh instance is created for every context
 *     that matters here - a part dragged into the VAB, a vessel launched to flight, a save
 *     reloaded - each with its own `Actions` list, so doing it alongside `GoHeadless`/`ApplyTune`
 *     (already called from every such `OnStart`) covers the VAB editor and flight alike. Unlike
 *     `GoHeadless`, this needs no per-frame re-assertion in `OnUpdate`: nothing regenerates
 *     `Actions` after the instance is built, so clearing it once holds for the instance's life.
 *
 * ⛔ WHAT THIS FILE DOES NOT DO. It does not fly anything. §14.4(a) still holds and the screens'
 * flight commands are still an honest no-op - Part B replaces them one controller at a time
 * (§B12.5). Nothing here engages a module; hosting a core and commanding one are different jobs
 * and the second is T17 onward.
 *
 * ⛔ DO NOT DECLARE Update / FixedUpdate / OnGUI / OnDestroy IN THE SUBCLASS. MechJebCore
 * declares all four as Unity messages (MechJebCore.cs:531, 631, 1023, 1130). Unity dispatches a
 * message to the most-derived declaration only, so a same-named method here would REPLACE the
 * core's own loop rather than extend it. The per-frame hook used below is PartModule.OnUpdate,
 * which is KSP's virtual and which MechJebCore does not override.
 */
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DragonScreen.Pure;
using MuMech = DragonScreen.Mech.MuMech;

namespace DragonScreen
{
    /// <summary>
    /// The one embedded <c>MechJebCore</c>, hosted on the Dragon part under a name that cannot
    /// collide with a user's own MechJeb2, with its GUI held off and its settings kept inside
    /// this mod.
    /// </summary>
    public class DragonMechJebCore : MuMech.MechJebCore
    {
        /// <summary>
        /// The tune loaded out of GameData/DragonScreen/PluginData/. Empty means "load nothing"
        /// - a real state, and the one §B5's two-profile split will want when T22 decides that
        /// flight 1 flies RSS-RO defaults rather than this Crew-2 target.
        /// </summary>
        [KSPField]
        public string tuneFile = MechProfile.TuneFileName;

        /// <summary>Log the tune once per core, not once per part load.</summary>
        private bool _tuneLogged;

        // ── (2) never write MechJeb's settings files ────────────────────────────────────
        public override void OnSave(ConfigNode node)
        {
            // sfsNode == null is MechJeb's own "save my global/type cfg files now" call - from
            // the five-second timer in Update and from OnDestroy. It is the ONLY path in
            // MechJebCore.OnSave that touches a file, and it is not ours to take.
            if (node == null) return;
            base.OnSave(node);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            // base.OnLoad has just read the user's mechjeb_settings_global.cfg if one exists,
            // which is where a DisplayModule's `enabledFlight` comes from. Undo it immediately -
            // before OnStart, before the first frame.
            GoHeadless();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            GoHeadless();
            ApplyTune();
            RemoveInheritedActions();
        }

        /// <summary>
        /// PartModule's per-frame hook - NOT a Unity message, so it extends the core instead of
        /// replacing its own <c>Update</c>. Re-asserts the headless state, because a vessel
        /// reload, an undock or a re-entered flight scene can each run OnLoad again.
        /// </summary>
        public override void OnUpdate()
        {
            base.OnUpdate();
            GoHeadless();
        }

        // ── (3) the suppression itself ──────────────────────────────────────────────────

        /// <summary>
        /// Hold every window closed and keep the toolbar button from ever being created.
        /// Cheap enough to run every frame: <c>GetComputerModules&lt;T&gt;()</c> is cached
        /// (MechJebCore.cs:360) and <c>Enabled</c>'s setter returns immediately when the value
        /// is unchanged (ComputerModule.cs:52), so the steady state is ~25 comparisons.
        /// </summary>
        private void GoHeadless()
        {
            try
            {
                // (a) NOTHING DRAWS. MechJebCore.OnGUI iterates DisplayModules and calls DrawGUI
                //     only on the enabled ones, so this is the whole window surface - including
                //     the sliding menu, including any MechJebModuleCustomInfoWindow that
                //     AddDefaultWindows created, and including anything a user's own
                //     mechjeb_settings_global.cfg tried to switch on.
                List<MuMech.ComputerModule> windows = GetComputerModules<MuMech.DisplayModule>();
                for (int i = 0; i < windows.Count; i++) windows[i].Enabled = false;

                // (b) NO TOOLBAR BUTTON. MechJebModuleMenu.SetupAppLauncher() is called from
                //     OnMenuUpdate(), which MechJebCore.Update() calls unconditionally, and it
                //     adds an ApplicationLauncher button when `useAppLauncher` is true. That
                //     field - and `hideButton` - are `readonly`, but they are also
                //     [Persistent(pass = GLOBAL)], so MechJeb's OWN loader sets them from a
                //     ConfigNode. Using its supported path rather than reflection of our own.
                //     ⚠ mjButton is a STATIC on the menu module: a user running the real MechJeb
                //     has their own, in their own assembly, so without this there would be two
                //     MechJeb buttons in one toolbar - the §B12.1a failure, exactly.
                MuMech.MechJebModuleMenu menu = GetComputerModule<MuMech.MechJebModuleMenu>();
                if (menu != null && menu.useAppLauncher)
                {
                    var g = new ConfigNode("MechJebModuleMenu");
                    g.AddValue("useAppLauncher", "False");
                    g.AddValue("hideButton", "True");
                    menu.OnLoad(null, null, g);
                }
            }
            catch (Exception e)
            {
                // A throw here would be the one failure that matters, so it is never silent.
                Debug.LogError("[DragonScreen] MechJeb GUI suppression failed: " + e);
            }
        }

        // ── (4) the tune ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Apply the shipped per-vessel-type tune, read from this mod's own PluginData and fed
        /// through MechJeb's TYPE pass - the same pass <c>MechJebCore.OnLoad</c> uses for
        /// <c>mechjeb_settings_type_*.cfg</c>, so the values land exactly where they would have.
        /// Applied by NAME, not by vessel name, which is what makes it independent of what the
        /// craft happens to be called.
        /// </summary>
        private void ApplyTune()
        {
            if (string.IsNullOrEmpty(tuneFile)) return;
            try
            {
                string path = TunePath(tuneFile);
                if (!File.Exists(path))
                {
                    Debug.LogWarning("[DragonScreen] MechJeb tune not found, core left at defaults: " + path);
                    return;
                }

                ConfigNode type = ConfigNode.Load(path);
                if (type == null)
                {
                    Debug.LogWarning("[DragonScreen] MechJeb tune would not parse: " + path);
                    return;
                }

                int applied = 0;
                List<MuMech.ComputerModule> all = GetComputerModules<MuMech.ComputerModule>();
                for (int i = 0; i < all.Count; i++)
                {
                    MuMech.ComputerModule m = all[i];
                    string name = m.GetType().Name;
                    if (!type.HasNode(name)) continue;
                    try { m.OnLoad(null, type.GetNode(name), null); applied++; }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[DragonScreen] MechJeb tune: " + name + " rejected its node: " + e.Message);
                    }
                }

                if (!_tuneLogged)
                {
                    _tuneLogged = true;
                    // Say WHICH profile, every time, so a log read six months from now cannot
                    // mistake the Crew-2 target for the RSS-RO flight-1 baseline (§B5).
                    Debug.Log("[DragonScreen] MechJeb tune applied from the mod: " + tuneFile +
                              " - " + applied + " module(s). This is the TUNED Crew-2 profile "
                              + "(§B5 TUNING TARGET), not the RSS-RO default baseline.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[DragonScreen] MechJeb tune load failed: " + e);
            }
        }

        /// <summary>
        /// GameData/DragonScreen/PluginData/&lt;file&gt;, derived from this assembly's own
        /// location rather than from KSPUtil.ApplicationRootPath, so it is right whatever the
        /// install is called - and so it can never resolve into a user's MechJeb2 folder.
        /// </summary>
        internal static string TunePath(string file)
        {
            string dll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string dir = Path.GetDirectoryName(dll);
            return Path.Combine(Path.Combine(dir, "PluginData"), file);
        }

        // ── (5) no inherited action-group entries ───────────────────────────────────────

        /// <summary>
        /// Empty the action list KSP built from MechJebCore's own <c>[KSPAction]</c> methods, so
        /// none of the 18 inherited actions (orbit prograde, translatron, land at KSC…) appear in
        /// the Dragon's VAB action-group list. Called once from <c>OnStart</c> - see (5) in the
        /// file header for why that point is the right one and why, unlike <see cref="GoHeadless"/>,
        /// this does not need re-asserting every frame.
        /// </summary>
        private void RemoveInheritedActions()
        {
            try
            {
                if (Actions != null) Actions.Clear();
            }
            catch (Exception e)
            {
                Debug.LogError("[DragonScreen] MechJeb action-group removal failed: " + e);
            }
        }
    }

    /// <summary>
    /// Points the embedded MechJeb's settings directory at DragonScreen's own PluginData, before
    /// any core exists.
    ///
    /// ---- WHY THIS IS HERE AND NOT LEFT ALONE ----
    /// MuUtils hardcodes it (MuUtils.cs:19):
    ///     _cfgPath = KSPUtil.ApplicationRootPath + "GameData/MechJeb2/Plugins/PluginData/MechJeb2"
    /// - which is a USER'S MechJeb install, not ours. MechJebCore.OnLoad reads
    /// mechjeb_settings_global.cfg from there (MechJebCore.cs:821-829), so an embedded core would
    /// inherit the GLOBAL-pass settings of whatever MechJeb the player has configured. §B12.1 is
    /// the opposite of that: the tune ships inside the mod and "the conductor loads it, never the
    /// user". FileExistsCreateDirectory also CREATES that tree (MuUtils.cs:23-29), so on a machine
    /// with no MechJeb at all we would leave a phantom GameData/MechJeb2 behind.
    ///
    /// The matching WRITE half is already closed without reflection, in
    /// DragonMechJebCore.OnSave - that one mattered too much to leave resting on this.
    ///
    /// ---- WHY REFLECTION IS THE RIGHT TOOL HERE AND NOT A LICENCE FOR MORE ----
    /// §B12.1 is "rename shell only": the vendored tree may not be edited, and it has not been.
    /// Reading a private field of a compiled assembly from our own glue is not an edit to it, and
    /// it is the same technique the repo already uses to read RealFuels state. If it fails, it
    /// fails NON-FATALLY and says so: nothing else in the headless path depends on it.
    ///
    /// [KSPAddon(Instantly)] because it must land before the first MechJebCore.OnLoad, which
    /// happens during part compilation in the loading scene.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class MechCfgRedirect : MonoBehaviour
    {
        /// <summary>True once the embedded MechJeb's settings directory is ours.</summary>
        public static bool Redirected;

        /// <summary>What happened, for the log and for the in-sim walkthrough.</summary>
        public static string Note = "not attempted";

        private void Start()
        {
            try
            {
                string dir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                string want = Path.Combine(dir, "PluginData");

                System.Reflection.FieldInfo f = typeof(MuMech.MuUtils).GetField(
                    "_cfgPath", System.Reflection.BindingFlags.NonPublic
                             | System.Reflection.BindingFlags.Static);
                if (f == null)
                {
                    Note = "MuUtils._cfgPath not found - a re-pin has renamed it";
                    Debug.LogWarning("[DragonScreen] MechJeb cfg redirect: " + Note);
                    return;
                }

                // Read first: that forces the static initialiser to run, so our value is written
                // AFTER it rather than being overwritten by it later.
                object was = f.GetValue(null);
                f.SetValue(null, want);
                object now = f.GetValue(null);

                Redirected = want.Equals(now as string, StringComparison.Ordinal);
                Note = Redirected ? "ours: " + want : "REFUSED, still " + now;
                if (Redirected)
                    Debug.Log("[DragonScreen] embedded MechJeb settings directory -> " + want +
                              "  (was " + was + ")");
                else
                    Debug.LogWarning("[DragonScreen] MechJeb cfg redirect did not take: " + Note +
                                     " - the embedded core may READ a user's MechJeb settings. It "
                                     + "still cannot write them (DragonMechJebCore.OnSave).");
            }
            catch (Exception e)
            {
                Note = e.GetType().Name + ": " + e.Message;
                Debug.LogWarning("[DragonScreen] MechJeb cfg redirect failed, continuing: " + Note);
            }
        }
    }
}
