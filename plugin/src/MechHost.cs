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
 * ══ THE SEVEN THINGS THIS FILE DOES, AND WHY EACH IS NOT OPTIONAL ═══════════════════════════
 *
 * ⚠ (6) AND (7) WERE ADDED BY T15d, AFTER T15b's HEADLESS CLAIM FAILED ON THE GLASS.
 * The owner installed this build and ran T15b's checklist on 2026-09-05 (08:06-08:13); the
 * overseer read KSP.log and relayed the result. THIS CHAT MEASURED NONE OF IT and does not claim
 * to have. What came back: 6,935 `ArgumentOutOfRangeException`s out of `MechJebCore.Drive ->
 * VesselState.Update -> AnalyzeParts -> ModuleGimbal.GetPotentialTorque`; a MechJeb window of
 * Hoverslam fields ON SCREEN; and `mechjeb_settings_global.cfg` + `mechjeb_settings_type_New
 * Crew-2.cfg` written into our own PluginData. The cfg REDIRECT held, and is the only reason the
 * user's own MechJeb settings were not overwritten - do not regress it.
 *
 * (1) A DISTINCT PartModule NAME.  §B3's private namespace stops a CLR TYPE clashing with a
 *     user's MechJeb2.dll. It does NOT stop the OTHER collision, which is one level up and was
 *     found by T15b: KSP resolves `MODULE { name = … }` in a part cfg by CLASS NAME across every
 *     loaded assembly, and after the namespace wrap our class is still called `MechJebCore`. A
 *     cfg node naming `MechJebCore` on a machine that has the real MechJeb2 is therefore
 *     ambiguous, and whichever assembly the loader reaches first wins - which for a user with
 *     MechJeb installed could hand our Dragon THEIR core, full UI and all. That is the exact
 *     failure §B12.1a forbids, arriving by a route the namespace does not cover.
 *     So the shipped cfg names `DragonMechJebCore`, a subclass, and OUR node is unambiguous:
 *     nothing of theirs can satisfy it.
 *     ⚠ T15d - THE OTHER DIRECTION WAS NEVER CLOSED, AND THIS HEADER CLAIMED IT WAS. The
 *     original text read "the ambiguity is gone in both directions: nothing of theirs can satisfy
 *     our node and nothing of ours can satisfy one of theirs". The second half is FALSE. KSP
 *     resolves a `MODULE { name = MechJebCore }` node by `Type.Name` across EVERY loaded assembly
 *     and takes the first hit, and after the rename shell our assembly still exports a PartModule
 *     called `MechJebCore`. So a node WE do not ship - a user's MechJeb2 patch, an RO/RP-1 patch,
 *     or a `MechJebCore` module already persisted in a craft or save file - can be answered by OUR
 *     class, producing a BARE `MuMech.MechJebCore` carrying NONE of the overrides below: it
 *     drives, it draws, and it writes settings. That is the one mechanism that explains all three
 *     glass failures at once, and (7) closes it.
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
 *     ⚠ T15d - THIS IS ONE LAYER TOO HIGH, AND THE GLASS PROVED IT. A MechJeb window drew
 *     anyway. `Enabled = false` is asserted from `PartModule.OnUpdate`, which the game does not
 *     call in the EDITOR and does not call on an unloaded vessel, and it can only reach modules
 *     belonging to OUR OWN instance - so it cannot touch a window owned by the bare core (1)
 *     describes. The gate that actually decides whether ANY window of a core draws is one level
 *     up, in `MechJebCore.OnGUI` itself (:1132): `if (!ShowGui || this != vessel.GetMasterMechJeb()
 *     || …) return;`. (6) takes that gate. This enable sweep is KEPT as the inner layer,
 *     because it costs ~25 comparisons and it is the one that still holds during the frames when
 *     T18 has deliberately made us master.
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
 * (6) NO DRIVE AUTHORITY - THE MASTER GATE, AND THE FIRST THING T15d ADDS.  ⛔ 6,935 exceptions
 *     came out of `MechJebCore.Drive`, and (3)'s enable sweep could never have stopped them:
 *         MechJebCore.Drive        (:1077)  _ready = VesselState.Update();          <- :1080
 *                                           if (this == vessel.GetMasterMechJeb())  <- :1091
 *                                               foreach (...) if (module.Enabled) module.Drive(s);
 *     `VesselState.Update()` - which is where `AnalyzeParts` reaches
 *     `ModuleGimbal.GetPotentialTorque` and throws - runs UNCONDITIONALLY, BEFORE any
 *     module-enable check. Disabling every module in the tree cannot stop it. Read in the
 *     vendored source here, not taken on report.
 *     THE REAL GATE is one level up, `MechJebCore.OnFlyByWire` (:1062):
 *         if (_deactivateControl || !CheckControlledVessel() || this != vessel.GetMasterMechJeb())
 *             return;
 *         Drive(s);
 *     and mastership is decided by ONE public field: `VesselExtensions.GetMasterMechJeb`
 *     (:92-104) is `vessel.GetModule<MechJebCore>(p => p.running)`, over `public bool running =
 *     true` (MechJebCore.cs:68). So `running = false` on OUR instance is the whole lever - and it
 *     is not one gate but FIVE, because every entry point MechJeb has is gated on that same test:
 *         OnFlyByWire :1062  -> no Drive -> no VesselState.Update -> NO EXCEPTION STORM  (1)
 *         OnGUI       :1132  -> nothing draws at all, ours or not -> NO WINDOW           (2)
 *         Update      :633   -> no 5-second OnSave(null)          -> NO SETTINGS WRITE   (3)
 *                            -> no OnMenuUpdate                   -> no toolbar button
 *         FixedUpdate :540   -> returns
 *         OnSave      :938   "Only Masters can save"              -> second lock on      (3)
 *     ⛔ WHY NOT `DeactivateControl` (MechJebCore.cs:80-95), the other early-return in
 *     OnFlyByWire: its getter AND its setter both operate on
 *     `vessel.GetMasterMechJeb()._deactivateControl` - somebody ELSE's field. If a user's own
 *     core were ever the master, setting it would silently disable THEIR autopilot. That is a
 *     user-facing side effect we must never have. `running` is per-instance and touches nothing
 *     outside this object.
 *     ⛔ AND THE PAW TOGGLE IS HIDDEN, because `running` is not a plain field: it is
 *     `[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName =
 *     "MechJeb")]` with a `UI_Toggle` - a switch in the part's right-click menu, in flight AND in
 *     the editor, whose value PERSISTS into craft and save files. Left visible, one click by a
 *     curious player hands this core mastership and every failure above comes back. So the two
 *     `gui*` flags are cleared on our own instance's `BaseField` (no vendored edit), and
 *     `HoldDriveAuthority()` re-asserts the field from `DriveAuthorized` on load, on start and
 *     every OnUpdate - so a stale `running = True` already sitting in a save cannot resurrect it.
 *     ⚠ IT DOES NOT BREAK THE TUNE. Module construction (`LoadComputerModules`), `OnLoad`,
 *     `OnStart` and (4)'s ApplyTune are all reached without reference to the master - checked
 *     line by line at MechJebCore.cs:431-510, :747-786 and :795-916. A non-master core is fully
 *     BUILT and fully CONFIGURED; it simply is never asked to fly.
 *     ⭐ THE T18 SEAM, NAMED NOW SO IT IS NOT DISCOVERED LATER. `AuthorizeDrive(bool)` is the
 *     one and only way this core is ever handed the vessel. T18 calls `AuthorizeDrive(true)` when
 *     the conductor engages and `AuthorizeDrive(false)` the moment it disengages; nothing else -
 *     no cfg field, no PAW toggle, no persisted value - can set `running`. Nothing in the tree
 *     calls it today, which is exactly §14.4(a): the screens' flight commands stay an honest
 *     no-op. ⚠ Turning it ON re-arms every path listed above, INCLUDING `VesselState.Update`
 *     and its stock-KSP `GetPotentialTorque` throw - T18 owns that, and it is the reason T18 must
 *     engage deliberately rather than us leaving the core master "so the telemetry stays warm".
 *
 * (7) NOTHING ELSE MAY ANSWER TO THE NAME `MechJebCore` OUT OF OUR ASSEMBLY.  This is (1)'s
 *     unclosed direction, and it is the one mechanism that explains all three glass failures
 *     together - including the one (6) alone does NOT explain: `mechjeb_settings_global.cfg` and
 *     `mechjeb_settings_type_New Crew-2.cfg` were written even though (2)'s override was live.
 *     THE DEDUCTION, labelled as a deduction: the ONLY code in this assembly that writes those
 *     two filenames is `MechJebCore.OnSave` at :1003 and :1010, on the `sfsNode == null` path,
 *     and (2) intercepts that path on every instance of OUR subclass. The same session's log
 *     carries (4)'s `MechJeb tune applied from the mod` line, so our subclass WAS instantiated
 *     and its overrides WERE live. Therefore a second core existed whose `OnSave` was the BASE
 *     one: an instance of `MuMech.MechJebCore` itself. We ship no node that names it - so KSP
 *     built it from somebody else's node, by the `Type.Name` resolution (1) describes. It would
 *     also have been `running = true`, hence master, hence Drive, hence the storm and the window.
 *     ⚠ NOT PROVEN, AND SAID SO: deduced from the vendored source plus a relayed log, not
 *     measured here. T15b's rewritten checklist has the rows that confirm or refute it.
 *     THE CLOSE, in two layers, neither of which edits a vendored file:
 *       (a) PREVENTION - `MechCoreNameGuard`, a [KSPAddon(Instantly)] that removes OUR
 *           `MuMech.MechJebCore` from the PartModule name table KSP resolves `MODULE { name = … }`
 *           against, before PartLoader compiles a single part. Our `DragonMechJebCore` entry is a
 *           different name and is untouched, so our own cfg still works. A user who has the real
 *           MechJeb2 gets THEIR core for their node - what they actually asked for; a user who
 *           has none gets KSP's ordinary "module not found", for a node they never meant us to
 *           answer. Non-fatal and logged either way.
 *       (b) CONTAINMENT - `NeuterForeignCores()`, which sets `running = false` on any
 *           `MuMech.MechJebCore` that is NOT a `DragonMechJebCore`, on our part or our vessel.
 *           ⛔ TYPE-EXACT, AND THAT IS THE SAFETY PROPERTY: a user's own core is
 *           `MuMech.MechJebCore` from THEIR assembly - a different CLR type in a differently
 *           named assembly - so the `is` test below is simply false for it and we can never touch
 *           their autopilot. Anything this catches came out of OUR dll, and nobody else ships
 *           our type, so it is by definition a mis-binding.
 *     ⚠ RESIDUAL, stated rather than hidden: (b) only sees vessels our Dragon is on. If (a) is
 *     refused - it says so in the log - a stray core on a vessel with no Dragon is not covered.
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

        /// <summary>
        /// The earliest hook this subclass has. <c>MechJebCore.running</c> initialises to
        /// <c>true</c> at field-declaration time, so between construction and <c>OnLoad</c> there
        /// is a window in which this core would answer <c>GetMasterMechJeb</c>. It is a frame or
        /// two wide and no gate should rest on how wide - so it is closed here as well.
        /// <c>OnAwake</c> is PartModule's virtual, not a Unity message, so this extends
        /// MechJebCore's own rather than replacing it (see the ⛔ note in the file header).
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();
            HoldDriveAuthority();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            // `running` is [KSPField(isPersistant = true)], so base.OnLoad has just restored
            // whatever the craft or save file happens to carry. Take it back before anything can
            // read it - OnLoad runs before OnStart and before the first FixedUpdate, and
            // OnFlyByWire/OnGUI/Update all consult it through GetMasterMechJeb.
            HoldDriveAuthority();
            // base.OnLoad has also just read mechjeb_settings_global.cfg if one exists, which is
            // where a DisplayModule's `enabledFlight` comes from. Undo that too.
            GoHeadless();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            HoldDriveAuthority();
            HideDriveToggle();
            NeuterForeignCores();
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
            HoldDriveAuthority();
            NeuterOn(part);
            GoHeadless();
        }

        // ── (6) drive authority: the master gate ────────────────────────────────────────

        /// <summary>
        /// Whether this core is allowed to fly the vessel. FALSE for the whole of Part A and for
        /// every Part-B step before T18 - see item (6) in the file header for the five separate
        /// MechJeb entry points this one field closes.
        /// </summary>
        public bool DriveAuthorized { get; private set; }

        /// <summary>
        /// ⭐ THE T18 SEAM. The one and only way this core is ever handed the vessel, and the one
        /// and only way it is taken back. T18's conductor calls <c>AuthorizeDrive(true)</c> when it
        /// engages and <c>AuthorizeDrive(false)</c> the moment it disengages.
        ///
        /// Nothing calls it today, and that is deliberate: §14.4(a) holds until Part B replaces the
        /// screens' flight commands one controller at a time (§B12.5). It is public, named and
        /// documented HERE rather than left to be discovered, because a core that can never be
        /// turned on is useless to Part B - and because the alternative (leaving the core master
        /// "so the telemetry stays warm") is exactly what produced 6,935 exceptions on the glass.
        ///
        /// ⚠ WHAT TURNING IT ON RE-ARMS, so T18 cannot be surprised by it: MechJeb becomes the
        /// vessel's master core, so <c>Drive</c> runs, so <c>VesselState.Update()</c> runs every
        /// FixedUpdate - including <c>AnalyzeParts</c>, which is where stock KSP's
        /// <c>ModuleGimbal.GetPotentialTorque</c> threw. <c>OnGUI</c>, <c>Update</c>'s five-second
        /// settings save and the app-launcher button all re-arm with it; (2)'s write ban and (3)'s
        /// enable sweep are what stand behind them from that point on, which is why both are kept.
        /// </summary>
        public void AuthorizeDrive(bool authorized)
        {
            DriveAuthorized = authorized;
            HoldDriveAuthority();
            Debug.Log("[DragonScreen] MechJeb drive authority -> " + (authorized ? "ENGAGED" : "off")
                      + " (running=" + running + ")");
        }

        /// <summary>
        /// Force <c>running</c> to match <see cref="DriveAuthorized"/>. Re-asserted on load, on
        /// start and every frame because the field is persistent AND player-settable: a stale
        /// <c>running = True</c> in an existing save, or one click on the PAW toggle, would
        /// otherwise hand this core mastership. One bool comparison per frame.
        /// </summary>
        private void HoldDriveAuthority()
        {
            if (running != DriveAuthorized) running = DriveAuthorized;
        }

        /// <summary>
        /// Take MechJeb's own "MechJeb: Enabled/Disabled" toggle out of the Dragon's right-click
        /// menu, in flight and in the editor. It is <c>MechJebCore.running</c> behind a
        /// <c>UI_Toggle</c>, i.e. the very field <see cref="HoldDriveAuthority"/> holds down;
        /// leaving it on screen offers the player a switch whose only effect is to undo this file.
        /// Clearing the flags on our own instance's BaseField is not a vendored edit.
        /// </summary>
        private void HideDriveToggle()
        {
            try
            {
                BaseField f = Fields["running"];
                if (f == null) return;
                f.guiActive       = false;
                f.guiActiveEditor = false;
            }
            catch (Exception e)
            {
                Debug.LogError("[DragonScreen] MechJeb drive-toggle hide failed: " + e);
            }
        }

        // ── (7) containment: no bare core of OUR type may run ───────────────────────────

        /// <summary>Log the first sighting only - it would otherwise repeat every frame.</summary>
        private static bool _foreignLogged;

        /// <summary>
        /// Sweep this part and this vessel for a <c>MuMech.MechJebCore</c> that is NOT one of ours
        /// and take its mastership away. See item (7) in the file header: such an instance can only
        /// have come from KSP resolving somebody else's <c>MODULE { name = MechJebCore }</c> node
        /// against our assembly, and it carries none of this file's overrides.
        /// ⛔ The type test is exact and that is the safety property - a user's own core is a
        /// different CLR type in a different assembly, so this can never touch their autopilot.
        /// </summary>
        private void NeuterForeignCores()
        {
            try
            {
                int hit = NeuterOn(part);
                Vessel v = vessel;
                if (v != null && v.parts != null)
                    for (int i = 0; i < v.parts.Count; i++) hit += NeuterOn(v.parts[i]);

                if (hit > 0 && !_foreignLogged)
                {
                    _foreignLogged = true;
                    Debug.LogError("[DragonScreen] " + hit + " BARE MechJebCore(s) of our own "
                        + "assembly were found on this vessel and have been stopped (running=false). "
                        + "They are not ours to create: KSP answered somebody else's "
                        + "MODULE{name=MechJebCore} node with DragonScreen.Mech's class. "
                        + "MechCoreNameGuard should have prevented it - check its log line above.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[DragonScreen] MechJeb foreign-core sweep failed: " + e);
            }
        }

        /// <summary>One part's worth of <see cref="NeuterForeignCores"/>. Returns how many it
        /// stopped, so the caller can log a sighting exactly once.</summary>
        private static int NeuterOn(Part p)
        {
            if (p == null || p.Modules == null) return 0;
            int n = 0;
            for (int i = 0; i < p.Modules.Count; i++)
            {
                var mj = p.Modules[i] as MuMech.MechJebCore;
                if (mj == null || mj is DragonMechJebCore) continue;
                if (!mj.running) continue;
                mj.running = false;
                n++;
            }
            return n;
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
                int carrying = 0;
                List<MuMech.ComputerModule> all = GetComputerModules<MuMech.ComputerModule>();
                for (int i = 0; i < all.Count; i++)
                {
                    MuMech.ComputerModule m = all[i];
                    string name = m.GetType().Name;
                    if (!type.HasNode(name)) continue;
                    ConfigNode n = type.GetNode(name);
                    try
                    {
                        m.OnLoad(null, n, null);
                        applied++;
                        // ⚠ T15d. The two numbers are NOT the same number, and T15b's glass row 3
                        // asked for the wrong one. `applied` counts every constructed module whose
                        // TYPE NAME appears as a node - 53 of the cfg's 64 nodes are EMPTY, and
                        // MechJebModuleCustomInfoWindow contributes once per instance, of which
                        // AddDefaultWindows makes ten. `carrying` counts the nodes that actually
                        // moved a setting, and it is the only one worth reading as "the tune
                        // landed". Both expectations are pinned in MechProfile and re-derived from
                        // the cfg + the vendored tree by build.py test, so neither can rot.
                        if (n.CountValues > 0 || n.CountNodes > 0) carrying++;
                    }
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
                              " - " + applied + " module(s) matched a node (expected "
                              + MechProfile.ExpectedTuneModulesApplied + "), of which " + carrying
                              + " carried values (expected " + MechProfile.TuneNodesCarryingValues
                              + "). This is the TUNED Crew-2 profile "
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

    /// <summary>
    /// Stops KSP answering ANYBODY ELSE's <c>MODULE { name = MechJebCore }</c> node with OUR
    /// class. Register task T15d; see item (7) in this file's header for the measurement that
    /// made it necessary.
    ///
    /// ---- THE HOLE ----
    /// §B3's private namespace closes a CLR TYPE clash with a user's MechJeb2.dll. The part-cfg
    /// layer is not type-based: KSP resolves a MODULE name by <c>Type.Name</c> across every loaded
    /// assembly and takes the first hit, and after the rename shell this assembly still exports a
    /// PartModule called <c>MechJebCore</c>. T15b closed the direction that mattered to OUR cfg -
    /// our node names <c>DragonMechJebCore</c>, which nothing else can answer - and its header
    /// then claimed the reverse direction was closed too. It was not. A node we do not ship (a
    /// user's MechJeb2 patch, an RO/RP-1 patch, or a <c>MechJebCore</c> module already persisted
    /// in a craft or save file) can be answered by OUR class, and what KSP builds then is a BARE
    /// <c>MuMech.MechJebCore</c>: no headless sweep, no write ban, no cleared action list, and
    /// <c>running = true</c>, so it wins mastership and flies the vessel.
    ///
    /// ---- WHAT THIS DOES INSTEAD ----
    /// Removes exactly one entry - our own <c>MuMech.MechJebCore</c> - from the PartModule type
    /// tables KSP resolves names against, before PartLoader compiles a part. Consequences, all
    /// intended:
    ///   • our own <c>DragonMechJebCore</c> is a different name, a different entry, untouched;
    ///   • a user WITH MechJeb2 gets THEIR MechJebCore for their node, which is what they asked
    ///     for and what §B12.1a wants;
    ///   • a user WITHOUT MechJeb2 gets KSP's ordinary "module not found" for a node they never
    ///     meant this mod to answer - noisier than before, and correct.
    ///
    /// ---- WHY THIS SHAPE ----
    /// Same rules as <see cref="MechCfgRedirect"/>: §B12.1's rename shell forbids editing the
    /// vendored tree, and nothing here does - the class keeps its name, we only stop KSP indexing
    /// it. It runs <c>Instantly</c> so it lands before part compilation in the loading scene, it
    /// is wrapped so a KSP-version change makes it a NO-OP rather than a crash, and it says in the
    /// log which of the two it was, because <see cref="DragonMechJebCore.NeuterForeignCores"/> -
    /// the containment layer behind it - can only cover vessels our Dragon is on.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class MechCoreNameGuard : MonoBehaviour
    {
        /// <summary>True once no part cfg can name our embedded core.</summary>
        public static bool Guarded;

        /// <summary>What happened, for the log and for the in-sim walkthrough.</summary>
        public static string Note = "not attempted";

        private void Start()
        {
            try
            {
                Type ours = typeof(MuMech.MechJebCore);
                Type pm   = typeof(PartModule);
                int removed = 0;

                for (int i = 0; i < AssemblyLoader.loadedAssemblies.Count; i++)
                {
                    AssemblyLoader.LoadedAssembly la = AssemblyLoader.loadedAssemblies[i];
                    if (la == null || la.assembly != ours.Assembly) continue;

                    // Both tables, because which one a given KSP build reads for a MODULE name is
                    // not ours to assume, and leaving either populated leaves the hole open.
                    List<Type> byBase;
                    if (la.types != null && la.types.TryGetValue(pm, out byBase) && byBase != null
                        && byBase.Remove(ours)) removed++;

                    Dictionary<string, Type> byName;
                    if (la.typesDictionary != null && la.typesDictionary.TryGetValue(pm, out byName)
                        && byName != null && byName.Remove(ours.Name)) removed++;
                }

                Guarded = removed > 0;
                Note = Guarded
                    ? "our 'MechJebCore' is out of KSP's PartModule name table (" + removed
                      + " entr" + (removed == 1 ? "y" : "ies") + "); only DragonMechJebCore can be named"
                    // ⚠ NOT the same as "safe". It means the tables were reachable and did not
                    // hold our type under the key we looked under. If a bare core still turns up
                    // (DragonMechJebCore logs one), this is the line to suspect first: a KSP
                    // version that indexes PartModules somewhere else would read exactly like this.
                    : "our 'MechJebCore' was not under KSP's PartModule key - nothing removed; if a "
                      + "bare core still appears, this guard is the thing to re-check";
                Debug.Log("[DragonScreen] MechJeb core name guard: " + Note);
            }
            catch (Exception e)
            {
                Note = e.GetType().Name + ": " + e.Message;
                Debug.LogWarning("[DragonScreen] MechJeb core name guard FAILED, continuing: " + Note
                    + " - a foreign MODULE{name=MechJebCore} node could still be answered by this "
                    + "mod. DragonMechJebCore.NeuterForeignCores is the remaining defence, and it "
                    + "only covers vessels the Dragon is on.");
            }
        }
    }
}
