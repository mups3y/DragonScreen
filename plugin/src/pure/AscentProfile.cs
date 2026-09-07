// DragonScreen — AscentProfile  (PURE: EVERY setting MechJeb's ascent stack exposes, and our decision on it)
// ============================================================================================
// Register S219 JOB 2. The owner's directive, 2026-09-07, verbatim:
//     "this is all the research I ordered to be completed, I should not have to explain step by step
//      how to use mechjeb if the research has been done. How can the conductor act like it's a user
//      using mechjebs UI if it does not know what setting/options to set"
//     "⛔ Enumerate EVERY setting the ascent menu exposes and state, one by one, whether we set it and
//      why. That table is the deliverable. No value may be left as 'whatever the profile set'"
//
// ---- ⛔ WHAT THIS FILE REPLACES, AND WHY IT HAD TO ----
// Before S219, `MechConductor.Configure` wrote FOUR values and then logged, verbatim:
//     "⚠ No ascent-SHAPING value was written: pitch rate, pitch-start velocity, LimitQa, MaxAoA and the
//      attitude PID are whatever the loaded profile set (register S195 …)"
// That sentence is the thing the owner is objecting to. A conductor that "acts like a user using
// MechJeb's UI" has an answer for every box on the screen — including "leave it alone", which is a
// decision and not an omission. This file is that answer, one row per box, and it is PURE so the answer
// is headless-testable and mutation-provable rather than a comment in the glue.
//
// ---- ⭐ THE ACCOUNTING RULE IS THE OWNER'S OWN, AND IT IS WHAT MAKES "LEAVE IT" LEGITIMATE ----
// *"he is resetting MechJeb to its own defaults on the next load, so anything we do not set is a
// default, not a tune."* So a `RoDefault` row is not an unknown: it is Realism Overhaul's own considered
// value, `MechJebModuleAscentSettings.ApplyRODefaults()`, which the Part-B gate (REGISTER.md banner,
// owner 2026-09-03) names as **the baseline to tune from**. ⚠ Where a value is load-bearing enough that
// a stale persisted cfg could change the flight, we WRITE RO's own number anyway, so the flown value is
// ours on the record rather than inherited — that is the difference between `Write` and `RoDefault`
// below, and it is the only difference.
//
// ---- ⛔ WHAT THIS FILE MAY NOT DO ----
// • **It may not TUNE.** The Part-B gate is *"RSS-RO DEFAULT settings as the baseline to tune from"*,
//   with the one-parameter-at-a-time fine tune DEFERRED until after the first recorded flight (T22).
//   Any row whose value would DEVIATE from RO's default on shaping grounds is `OwnerQuestion`, never a
//   build chat's call (C1.8/C1.12/C1.14). There are exactly two, and both are stated below with the
//   owner's own flown cfg as the evidence.
// • **It may not edit `plugin/mech/`** (§B12.1). Every row here is a value written INTO the vendored
//   module through its own public field, exactly as its GUI writes it.
//
// ============================================================================================
// ⭐⭐ S222b, 2026-09-08 — **THE OWNER READ THIS TABLE AND SAID 47 WRITES IS NOT "RO's DEFAULTS".**
// ============================================================================================
// Verbatim:
//     "what I do not understand is if we are truely setting mechjebs default launch to rendezvous
//      settings for rss/ro mods, then why did it pitch over early at all? We are obviously using what is
//      thought to be 'tuned' when they are clearly wrong. We need to make sure the chat fully understands
//      how to use mechjeb correctly and return everything back to default settings and using the methods
//      I described for achieving each stage. No guesses, no invented methods or 'tuning' truely stock
//      mechjeb methods and settings set for auto accent, auto rendezvous and auto docking!"
//     "no you miss understand, we can set the orbit to 215km. Just do not mess with the tuning etc, let
//      native mechjeb do it AND THEN WE TUNE FROM TRUSTED CAPTURED VALUES!!!"
//     "the only change should be the auto stage being our way"
//
// ⛔ **THE RULE THAT REPLACES S219's ACCOUNTING RULE, AND IT IS NARROWER IN EXACTLY ONE WAY.**
// S219's rule said: where a value is load-bearing enough that a stale persisted cfg could change the
// flight, WRITE RO's own number anyway, so the flown value is ours on the record. That reasoning is
// still coherent — and the owner has overruled it, because it is indistinguishable, from the outside,
// from tuning. **If `ApplyRODefaults()` sets it, we do not.** The test for every surviving write is one
// sentence: *would a user open the MechJeb UI and type this to fly this mission?* A `Write` row must now
// name the control, and its reason must be a MISSION FACT, a UI WORKFLOW, or the autostage deviation.
// Nothing else is a reason.
//
// ⭐ **AND A THIRD CATEGORY HAD TO EXIST, BECAUSE THE MENUS THEMSELVES WRITE TWO OF THESE BOXES.**
// `LimitQaEnabled` and `OptimizeStageFlag` are not typed by anyone: the ascent window asserts the first
// every frame (`MechJebModuleAscentMenu.cs:374`) and the PSG-settings window RECOMPUTES the second every
// frame from the live stage list (`MechJebModuleAscentPSGSettingsMenu.cs:63,83`). We draw no windows
// (T15b), so those two boxes would sit at their at-rest values forever — which is not "RO's defaults",
// it is a state no user of MechJeb is ever in. `UiDerived` is that case, and its rows reproduce the
// menu's own expression rather than a value we picked.
//
// ⚠ **THIS MAY FLY WORSE THAN THE TUNED VERSION, AND THAT IS THE POINT.** Owner: *"AND THEN WE TUNE FROM
// TRUSTED CAPTURED VALUES"*. A bad ascent on true RO defaults is a REFERENCE (§B11 has never had one);
// a good ascent on invented numbers is not evidence of anything. Tuning is T22's, from the black box,
// after a flight.
// ============================================================================================
//
// PURE: no Unity, no KSP, no MechJeb reference. `MechConductor.Configure` is the only caller.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>What the conductor does about one exposed setting.</summary>
    public enum AscentDisposition : byte
    {
        /// <summary>We WRITE it, at configure time, to the value in the row.
        /// ⭐ **S222b NARROWED THIS TO THREE ADMISSIBLE REASONS AND NO OTHERS** (owner, 2026-09-08): the
        /// row is a MISSION FACT (where we are going), a UI WORKFLOW (a control a user would actually
        /// open the window and use for this mission), or the OWNER'S ONE SANCTIONED DEVIATION (autostage
        /// off + our direct part activation). Every `Write` row must NAME its control.
        /// ⚠ **SUPERSEDED IN PLACE (C1.16):** it used to also mean *"load-bearing enough that inheriting
        /// it from a persisted cfg is a risk we decline to take"*, and *"the vendored source calls it
        /// mandatory"*. Those produced 47 of 77 rows and the owner ruled that is not "running RO's
        /// defaults". The anti-stale reason is retired outright; the "mandatory" reason became
        /// <see cref="UiDerived"/>, which is what it always actually was.</summary>
        Write,

        /// <summary>We deliberately leave RO's own default. The row still carries the value, so "what
        /// did we fly" is answerable without opening MechJeb.</summary>
        RoDefault,

        /// <summary>Belongs to the CLASSIC gravity-turn path. `AscentType` is PSG, so the vendored code
        /// never reads it. Writing it would be noise that looks like a decision.</summary>
        ClassicOnly,

        /// <summary>Written at RUNTIME rather than at configure time, because its value is not known
        /// until the plane is solved or the mission profile is read. The row says which.</summary>
        RuntimeMission,

        /// <summary>⛔ A DEVIATION FROM THE RO BASELINE THAT ONLY THE OWNER MAY AUTHORISE (C1.8's
        /// `OVERRIDE`, C1.14's carve-out for taste). We fly RO's default and ASK.</summary>
        OwnerQuestion,

        /// <summary>⭐ S222b. **NOT A BOX ANYONE TYPES — MechJeb's OWN MENU COMPUTES IT, every frame it
        /// draws.** There are exactly two, and both would otherwise sit at an at-rest value no user of
        /// MechJeb is ever in, because T15b suppressed the windows that write them. The conductor stands
        /// in for the window and reproduces the menu's expression VERBATIM — it does not pick a value.
        /// ⚠ Distinct from `Write` (a value we choose) and from `RoDefault` (a value we leave).</summary>
        UiDerived
    }

    /// <summary>One row of the audit: the setting, our decision, the value it flies at, and why.</summary>
    public struct AscentSetting
    {
        public string Name;
        public AscentDisposition How;
        public string Value;
        public string Why;
    }

    public static class AscentProfile
    {
        // =========================================================================================
        // 1. THE VALUES WE WRITE — each with its provenance, so none of them is a preference
        // =========================================================================================

        /// <summary>
        /// ⭐ MechJeb's OWN "Launch countdown" box (`MechJebModuleAscentMenu.cs:230`), and the number
        /// that governs three things at once: when the autowarp lands
        /// (`MechJebModuleAscentBaseAutopilot.cs:133`, `WarpToUT(_launchTime - WarpCountDown)`), when
        /// PSG is handed the target and told to start converging
        /// (`MechJebModuleAscentPSGAutopilot.cs:49-53`, `TMinus &lt;= WarpCountDown` → `SetTarget()` +
        /// `AssertStart(false)`), and therefore how much time the solver has before T-0.
        ///
        /// ⭐ **COMPOSED FROM TWO IN-REPO NUMBERS, NOT CHOSEN** — carried forward verbatim from
        /// [[S215]]'s `WarpLeadSeconds`, whose reasoning survives its mechanism:
        ///   • **20 s** — `plugin/mech/MechJebKos/AscentPSGBinding.cs:113-115`, the vendored tree's own
        ///     note: *"PSG can take ~20s to converge an initial solution from a cold start. Staging
        ///     before there is a solution drops the rocket on the pad, so a launch script should wait on
        ///     HASSOLUTION … before releasing the clamps."*
        ///   • **12 s** — `pure/WarpPlan.BurnLeadS`, this repo's "be at 1× this long before an event"
        ///     margin, for the warp-exit transition itself.
        /// ⛔ **STOCK'S 11 s IS NOT ENOUGH FOR US.** Stock can afford it because stock's T-0 is
        /// `StageManager.ActivateNextStage()`, which does not care whether guidance converged. Ours is
        /// `IgnitionGate`, which will not light a cold octaweb into a commanded zero (S214), so the
        /// solver must be done BEFORE our terminal count, not before liftoff.
        /// ⚠ Both halves are MARGINS, not physics. Safe in one direction only: larger = more real-time
        /// seconds on the pad.
        /// </summary>
        public const int WarpCountDownS = 32;

        /// <summary>
        /// ⭐⭐ WHEN THE CONDUCTOR TAKES THE TERMINAL COUNT — and the one write that makes
        /// `StageManager.ActivateNextStage()` UNREACHABLE instead of merely unlikely.
        ///
        /// `MechJebModuleAscentBaseAutopilot.OnFixedUpdate:122-137` runs its T-0 block only
        /// `if (TimedLaunch)`. `TimedLaunch` is a **public field**, and MechJeb's own ascent window
        /// clears it from the **Abort button** (`MechJebModuleAscentMenu.cs:305`:
        /// `_launchingToPlane = _launchingToMatchLan = _launchingToLan = _autopilot.TimedLaunch = false`).
        /// So clearing it is a documented UI action, not a patch (§B12.1 intact).
        ///
        /// ⛔ **THIS IS THE ANSWER [[S215]] LOOKED FOR AND DID NOT FIND.** Its Q1 rejected
        /// `StartCountdown` outright, and its reasoning was right as far as it went:
        ///     *"The guard happens to align with our design — our octaweb is lit ~3 s before T-0, so
        ///      `ThrustAvailable` is not small and the branch would not fire — but a safety property
        ///      that holds only because we win a race is not a safety property."*
        /// Exactly so. **The fix is not to win the race; it is to delete the branch.** At T-`this` the
        /// conductor clears `TimedLaunch`, and from that instant MechJeb has no T-0 at all — whatever
        /// happens to the ignition, including `IgnitionGate` safing the pad and leaving `ThrustAvailable`
        /// at zero, which is the case that would otherwise have re-lit a just-shut octaweb.
        ///
        /// ⚠ IT MUST SIT BETWEEN THE TWO: LATER than `WarpCountDownS` (or the warp and the guidance
        /// start never happen) and EARLIER than `AscentSequence.IgnitionLeadSeconds` = 3 s (or MechJeb
        /// still owns T-0 when we light). 10 s is the midpoint with margin at both ends, and
        /// `AscentProfileTest` asserts the ordering rather than trusting it.
        /// </summary>
        public const double TerminalCountS = 10.0;

        /// <summary>
        /// §7.5: *"`LaunchLANDifference` = 0 for the exact plane."* It is subtracted from the target's
        /// LAN inside `Astro.MinimumTimeToPlane`, so a non-zero value aims the whole launch at a plane
        /// offset from the station's by that many degrees.
        ///
        /// ⛔ **SUPERSEDED IN PLACE — S222b, 2026-09-08 (C1.16/G12). THE VALUE IS UNCHANGED; WE NO LONGER
        /// WRITE IT.** What it claimed: that a stale non-zero could aim the launch at the wrong plane, so
        /// the conductor should assert 0. What replaced it: `ApplyRODefaults()` seeds
        /// `LaunchLANDifference.Val = LAUNCH_LAN_DIFFERENCE`, and `LAUNCH_LAN_DIFFERENCE = 0`
        /// (`MechJebModuleAscentSettings.cs:397`) — so RO already asserts exactly this, and the owner's
        /// rule is *"if RO sets it, we do not"*. ⭐ **AND THE STALENESS FEAR WAS ALREADY ANSWERED
        /// ELSEWHERE:** `MechConductor.SolveWindow` reads the LIVE field
        /// (`core.AscentSettings.LaunchLANDifference.Val`) into `WindowInputs.LanDifferenceDeg` rather
        /// than this constant, exactly as `MechJebModuleAscentMenu.cs:249` does — so whatever the box
        /// holds is what the window is solved with, which is the faithful behaviour either way.
        /// ⚠ The constant is KEPT, unreferenced, because the §7.5 citation above is the reasoning and
        /// C1.16's extension forbids deleting reasoning along with the code it described.
        /// </summary>
        public const double LaunchLanDifferenceDeg = 0.0;

        /// <summary>
        /// ⛔⛔ **SUPERSEDED IN PLACE — S222b, 2026-09-08 (C1.16/G12). THE ANSWER IS `false`, AND THE
        /// REASON IT WAS `true` WAS A DEFECT WE INTRODUCED OURSELVES ONE LINE EARLIER.**
        ///
        /// ---- WHAT IT CLAIMED (S219, and it followed correctly from its own premise) ----
        /// RO's own default is **110 km** and the flown orbit is **210 km**, and
        /// `docs/MECHJEB_MASTER_MAP.md` §7.2 names that gap as a BUG ALREADY FOUND ON THIS CRAFT:
        /// *"attach = orbit alt gives a clean circular insertion; attach &lt; peR = 'periapsis insertion'
        /// (elliptical), which is the bug found in the Crew-2 cfg (110 km attach vs 210 km orbit →
        /// fixed to 210)."* `MechJebModuleAscentPSGAutopilot.SetTarget:100-101` reads
        /// `DesiredAttachAltFixed` whenever `OptimizeStageFlag` is false — *"which is its default"* — so
        /// leaving this at the RO default would fly the documented elliptical insertion; therefore write
        /// the mission apoapsis into both fields and set `AttachAltFlag`.
        ///
        /// ---- ⭐ WHAT THE MENU ACTUALLY SAYS, READ END TO END (the brief's "establish, do not assume") ----
        /// **The premise is false. `OptimizeStageFlag` is not a setting with a default at all** — it is
        /// **NOT `[Persistent]`** (`MechJebModuleAscentSettings.cs:263`, a bare `public bool`), it is not
        /// touched by `ApplyRODefaults()`, and **no user ever types it**. The PSG-settings window
        /// RECOMPUTES it from the live stage list every frame it draws
        /// (`MechJebModuleAscentPSGSettingsMenu.cs:63` clears it, `:83` sets it for every listed stage
        /// that is not in `FixedStages`) — and `ApplyRODefaults()` **opens that very window for RO users**
        /// (`ascentMenu._lastPSGSettingsEnabled = true`, *"open the PSG ascent settings windows for new
        /// users"*). So for any vehicle with a non-fixed stage above `MinDeltaV`, **an RO user's
        /// `OptimizeStageFlag` is TRUE, continuously.** Ours read `false` only because S219 wrote `false`.
        ///
        /// **And with it TRUE the attach altitude is not on the mission path at all.** The ascent window
        /// then shows the attach altitude as a **`ToggledTextBox`** (`MechJebModuleAscentMenu.cs:123-124`)
        /// whose toggle is `AttachAltFlag` — RO default **`false`** (`ATTACH_ALT_FLAG_DEFAULT`, re-asserted
        /// by `ApplyRODefaults()`). A user launching to a 215 km circular orbit **leaves that toggle
        /// alone**; nothing in the launch-to-rendezvous workflow asks for it. `SetTarget:109` then passes
        /// `attachAltFlag = !OptimizeStageFlag || AttachAltFlag` = **false**, and `DesiredAttachAlt`
        /// is never read as a constraint.
        ///
        /// ⭐⭐ **AND MechJebLib THEN SETS THE ATTACH ALTITUDE ITSELF, TO EXACTLY THE RIGHT VALUE.**
        /// `MechJebLib/PSG/AscentBuilder.Build():146-149`:
        ///     `if (!_attachAltFlag &amp;&amp; eccT &lt; 1e-4) { _attachAltFlag = true; _attR = _peR; }`
        /// — *"for nearly circular orbits, force periapsis attachment"*. Our target is 215 × 215 km, so
        /// `eccT` is 0 and **attach = periapsis = 215 km, computed by the solver.** The clean circular
        /// insertion §7.2 wanted is what native MechJeb produces on its own defaults; RO's 110 km is
        /// never consulted. (For the three free-flyers, whose orbits are genuinely elliptic, the same
        /// `false` routes to the `Kepler3`/`Kepler4` terminal — sma + ecc + inc — which is again exactly
        /// what a user gets by leaving the toggle off.)
        ///
        /// ---- ⛔ SO THE 110-vs-210 "BUG" WAS OURS ----
        /// `attachAltFlag` is forced TRUE only when `OptimizeStageFlag` is false, and the only thing
        /// making it false on this craft was our own `Configure` line. Writing `OptimizeStageFlag = false`
        /// CREATED the elliptical-insertion hazard, and writing 210 km into `DesiredAttachAltFixed`
        /// compensated for it. Both writes are gone (S222b); the flag is now `UiDerived` from the menu's
        /// own expression, and the attach altitude is left at RO's 110 km, unread.
        /// ⚠ **§7.2's warning is NOT wrong and is NOT retired** — it describes a real hazard, and it is
        /// exactly the hazard a *tuned cfg* with `OptimizeStageFlag` unset would hit. It simply does not
        /// apply to a vehicle whose stage list the menu is deriving from, which is every RO user's.
        /// </summary>
        public const bool AttachAltFollowsMissionApsis = false;

        // =========================================================================================
        // 2. THE AUDIT — every box the ascent stack puts on the screen, and our decision on it
        // =========================================================================================
        // Enumerated from the vendored menus themselves, not from memory:
        //   `MechJebModuleAscentMenu.cs`, `MechJebModuleAscentSettingsMenu.cs`,
        //   `MechJebModuleAscentPSGSettingsMenu.cs`, `MechJebModuleAscentClassicPathMenu.cs`
        // (every `_ascentSettings.<X>` they touch), plus the `Core.Thrust` / `Core.Node` / `Core.Warp`
        // fields `ApplyRODefaults()` reaches into, because those are on the same screens and the owner's
        // named item — max-Q throttle-down — is one of them.
        //
        // ⛔ THE COMMENTED-OUT ONE IS NOT INCLUDED. `ExtendIfRequired` appears only inside a commented
        // line (`MechJebModuleAscentPSGSettingsMenu.cs:98`), so it is not a box on the screen.
        public static readonly AscentSetting[] Audit =
        {
            // =====================================================================================
            //  ⭐ THE EIGHT THAT SURVIVE THE OWNER'S TEST — "would a user open the MechJeb UI and TYPE
            //  this to fly THIS mission?" Each names its control and one of the three admissible
            //  reasons: MISSION FACT · UI WORKFLOW · the autostage deviation. (S222b)
            // =====================================================================================
            R("AscentType",          AscentDisposition.Write,     "PSG",
              "UI WORKFLOW — the ascent-path dropdown (MechJebModuleAscentMenu.cs `_ascentPathList`, 'PSG (RSS/RO)'). The owner's KEEP list names it. RO seeds PSG too, so this write only asserts what a persisted craft value could have made CLASSIC."),
            R("Autostage",           AscentDisposition.Write,     "false (RO seeds TRUE)",
              "⭐ THE OWNER'S ONE SANCTIONED DEVIATION, 2026-09-08 verbatim: 'the only change should be the auto stage being our way'. UI control: the 'Autostage' toggle in the Ascent Settings menu. Through the PROPERTY, never the `_autostage` field — only the property removes the ascent autopilot from `Core.Staging.Users`. §B8/§B12.7, with IgnitionGate owning the pad."),
            R("AutostageLimit",      AscentDisposition.Write,     "the S1/S2 separation stage, DERIVED from the live part list (6 on Crew-2)",
              "⭐⭐ S235 JOB 4 — ADDED BECAUSE THE AUDIT COULD NOT SEE IT. [[S228]] R-03 wrote this box as the cascade backstop and `grep -c AutostageLimit pure/AscentProfile.cs` returned **0**: the instrument built to catch re-seeds was blind to the one setting that bounds a runaway staging cascade. ⛔ It is not on `MechJebModuleAscentSettings` at all — it lives on `MechJebModuleStagingController` (`:35`, `readonly EditableInt AutostageLimit = 0`), which is why every earlier pass missed it: this table had only ever read one module. THE SAME SANCTIONED DEVIATION as `Autostage` above (§B8/§B12.7) — MechJeb actuates no separation, so a floor costs a nominal flight nothing. Value DERIVED per craft by `pure/StagingFloor.For` (the stage carrying the interstage; `ForbidAll` when none is found), never a literal — sixteen .craft files, and a hardcoded 6 would be silently wrong for fifteen of them. `MechJebModuleStagingController.cs:284` refuses to stage while `currentStage <= AutostageLimit`. ⚠ Its default of **0** is exactly the value that let flight 002 cascade 6 -> 1 unbounded (F-102), expending the drogues at 33.9 km."),
            R("WarpCountDown",       AscentDisposition.Write,     "32 s (MechJeb's own box defaults to 11)",
              "UI WORKFLOW + the autostage deviation. It is MechJeb's own 'Launch countdown:' box (MechJebModuleAscentMenu.cs:230) and RO does NOT seed it. It is 32 rather than 11 for a reason that IS the deviation: our T-0 is `IgnitionGate`, which refuses to light a stage with no guidance solution (S214), so the solver must finish BEFORE our terminal count — 20 s (the vendored tree's own PSG cold-start note, MechJebKos/AscentPSGBinding.cs:113-115) + 12 s (WarpPlan.BurnLeadS). ⛔ It shapes NO part of the trajectory; it is a ground-ops lead time."),
            R("SkipCircularization", AscentDisposition.Write,     "true (MechJeb's field default is false)",
              "The autostage deviation's family — it stops an ACTUATION we own, it does not shape a trajectory. UI control: the 'Skip Circularization' toggle in the Ascent Settings menu. On EXIT, DriveCircularizationBurn:244-286 PLACES A MANEUVER NODE and hands it to Core.Node, which would collide with T19's own node executor and with the conductor's ClearNodes. RO does not seed it; the owner's own flown cfg has it on (docs/reference/mechjeb_settings_type_Crew-Dragon.cfg:34 `SkipCircularization = True`)."),
            R("AutoDeploySolarPanels", AscentDisposition.Write,   "false (MechJeb's field default is true)",
              "§B12.7 — direct part activation is OURS, which is the second half of the owner's sanctioned deviation. UI control: the 'Auto-deploy solar panels' toggle. Left true, DrivePrelaunch:202-214 retracts panels and HOLDS the prelaunch mode until they are all retracted — a pad hold nobody commanded — and DriveDeployableComponents extends them above the atmosphere. RO does not seed it."),
            R("AutoDeployAntennas",    AscentDisposition.Write,   "false (MechJeb's field default is true)",
              "UI control: the 'Auto-deploy antennas' toggle — same rule and same §B12.7 control family as the panels, and RealAntennas is installed (docs/reference/INSTALLED_MODS.md), so this one really would extend hardware the crew procedure owns. RO does not seed it."),
            R("Core.Node.Autowarp", AscentDisposition.Write, "true",
              "⭐ THE owner's 'auto warp for all modes'. UI control: the ascent window's own 'Auto-warp' toggle. It is ONE field and all three phases read it: the ascent countdown warps only `if (Core.Node.Autowarp)` (MechJebModuleAscentBaseAutopilot.cs:132); the node executor gates both of its warps on it (:242, :293); and the rendezvous autopilot NARROWS the same field rather than owning one (`Core.Node.Autowarp = Core.Node.Autowarp && Core.Target.Distance > 1000`). Docking never warps at all."),
            R("Core.Warp.activateSASOnWarp", AscentDisposition.Write, "false",
              "§B12.7 again — an ACTION GROUP on our vehicle is direct part control. Left on, SetTimeWarpRate calls ActionGroups.SetGroup(SAS, true) on the way into warp, and SAS then fights MechJeb's own attitude controller. RO does not seed it."),

            // =====================================================================================
            //  ⭐ THE DESTINATION — §B5's ONE named exception. A destination is data, not a knob.
            // =====================================================================================
            R("DesiredOrbitAltitude",   AscentDisposition.RuntimeMission, "mission periapsis (215 km ISS)",
              "MISSION FACT. Owner, 2026-09-08 verbatim: 'we can set the orbit to 215km'. From `AscentTargets.For`. ⛔ RO seeds 145000 here, which is why this is one of only three exemptions to 'if RO sets it, we do not' — the other two are AscentType and Autostage, and the owner named all three."),
            R("DesiredApoapsis",        AscentDisposition.RuntimeMission, "mission apoapsis (215 km ISS)",
              "MISSION FACT, same source. ⚠ RO does NOT seed this one at all and its field default is 0, so an unwritten apoapsis would leave the glue ball to clamp `apR` up to `peR` — the right answer, reached by accident. Writing the destination is the exception the owner named."),
            R("DesiredInclination",  AscentDisposition.RuntimeMission, "the plane solve — or the mission fact when there is no plane to solve",
              "⭐ TWO PATHS, BOTH THE UI's OWN. On a RENDEZVOUS mission §7.5's button writes it: `MinimumTimeToPlane`'s second return value, carrying the northgoing/southgoing SIGN, written AFTER StartCountdown exactly as MechJebModuleAscentMenu.cs:257 does. ⛔ S222b: `Configure` no longer writes it on that path — the owner, 2026-09-08: writing it fights `LaunchingToPlane`, which overrode it correctly last flight (-51.6316 deg, matching the ISS's own 51.6316). On a FREE-FLYER there is no plane launch and no button, so the mission fact is typed into the 'Orbit inc.' box, which is what `Configure` still does there."),
            R("LaunchingToPlane",     AscentDisposition.RuntimeMission, "true at the crew's GO",
              "⭐ THE flag that makes PVG target the target's RAAN and not merely its inclination (SetTarget:103-105 passes lanflag + Core.Target.TargetOrbit.LAN). UI control: the 'Launch into plane of target' BUTTON. Without it: §7.5's 'right inclination, wrong RAAN'."),

            // =====================================================================================
            //  ⭐⭐ UI-DERIVED — the two boxes MechJeb's OWN MENUS write, every frame they draw. Nobody
            //  types these. We draw no windows (T15b), so the conductor reproduces the menu's expression.
            // =====================================================================================
            R("LimitQaEnabled",      AscentDisposition.UiDerived,  "true, because AscentType is PSG",
              "MechJebModuleAscentMenu.cs:374 asserts it every frame: `LimitQaEnabled = _ascentSettings.AscentType == AscentType.PSG; // this is mandatory for PSG`. The conductor writes that same EXPRESSION, not a chosen value. (RO also seeds true, so the flown value is unchanged either way — but the reason is the menu's, not ours.)"),
            R("OptimizeStageFlag",  AscentDisposition.UiDerived, "recomputed from the live stage list, every tick",
              "⭐⭐ NOT A SETTING — it is not `[Persistent]` (MechJebModuleAscentSettings.cs:263), ApplyRODefaults never touches it, and the PSG-settings window RECOMPUTES it every frame (`:63` clears it; `:83` sets it for each listed stage not in FixedStages). ApplyRODefaults OPENS that window for RO users, so a real RO user's flag is continuously TRUE on a vehicle with any non-fixed stage. ⛔ S219 wrote FALSE, which is the state that made RO's 110 km attach altitude live — see `AttachAltFollowsMissionApsis` for the full unwind. Now derived by `OptimizeStageFlagFor`, the menu's own loop."),

            // =====================================================================================
            //  ⛔ AWAITING THE OWNER — unchanged by S222b, and deliberately still surfaced (2 rows).
            //  Both fly RO's default TODAY, which is also what the 2026-09-08 directive asks for; what
            //  is still open is whether they should EVER deviate, and that is T22's, from a flight.
            // =====================================================================================
            R("PitchRate",          AscentDisposition.OwnerQuestion, "5.0 deg/s (RO default) — the owner's own flown cfg says 0.75",
              "⛔ Q1, STILL OPEN. docs/reference/mechjeb_settings_type_Crew-Dragon.cfg:53 records PitchRate 0.75 on this very craft against ApplyRODefaults' PITCH_RATE_DEFAULT = 5.0. ⭐ S222b: an earlier overseer prompt directed 'set PitchRate to 0.75' and it was WITHDRAWN as invented tuning — it is not written. The 2026-09-08 directive settles the INTERIM answer (fly RO's 5.0, unwritten) and defers the question itself to T22, from captured values."),
            R("Core.Thrust.LimitDynamicPressure", AscentDisposition.OwnerQuestion,
              "false (RO default) — the owner asked for max-Q throttle-down",
              "⛔ Q2, STILL OPEN. Owner, verbatim: 'We should also be ticking/selecting max q throttle down etc.' RO's ApplyRODefaults sets this FALSE twice, deliberately, and turning it on needs a Q magnitude — which is precisely the T22 tune the Part-B gate defers. S222b drops the redundant write of RO's own false; the flown value is unchanged and the question is unchanged."),

            // =====================================================================================
            //  EVERYTHING BELOW IS LEFT AT RO's / MechJeb's OWN DEFAULT. ⛔ "If RO sets it, we do not."
            //  The row still carries the value, so "what did we fly" is answerable without opening
            //  MechJeb. A row marked (RO) is seeded by `ApplyRODefaults()`; the rest are field defaults
            //  no user flying this mission would touch.
            // =====================================================================================

            // ---- the attach altitude: the whole S222b unwind, in four rows --------------------------
            R("DesiredAttachAltFixed",  AscentDisposition.RoDefault, "110 km (RO)",
              "⭐ UNREAD ON OUR PATH. SetTarget:100-101 reads this field only when OptimizeStageFlag is FALSE, and the menu derives that flag TRUE for any vehicle with a non-fixed stage. S219 wrote the mission apoapsis here to escape an elliptical insertion its own `OptimizeStageFlag = false` had created; both writes are gone. See `AttachAltFollowsMissionApsis`."),
            R("DesiredAttachAlt",       AscentDisposition.RoDefault, "110 km (RO)",
              "The OptimizeStageFlag=true twin. Read as a CONSTRAINT only when AttachAltFlag is on, and RO seeds AttachAltFlag off — so on our path it is not read either."),
            R("AttachAltFlag",          AscentDisposition.RoDefault, "false (RO)",
              "⭐ UI control: the toggle on the 'Attach Alt' ToggledTextBox (MechJebModuleAscentMenu.cs:123). A user setting a 215 km launch-to-rendezvous LEAVES IT OFF — nothing in that workflow asks for it. With it off, AscentBuilder.Build():146-149 forces periapsis attachment itself for a near-circular target ('for nearly circular orbits, force periapsis attachment'), giving attach = 215 km from the solver rather than from us."),
            R("DesiredFPA",             AscentDisposition.RoDefault, "0 (RO)",
              "A zero terminal flight-path angle IS the circular-insertion constraint, and DESIRED_FPA_DEFAULT is 0. ⚠ Its box is only shown when NOT launching with a plane control (MechJebModuleAscentMenu.cs:129) — so on a launch-to-rendezvous a user cannot type it at all."),
            R("DesiredArgPFlag",        AscentDisposition.RoDefault, "false (RO)",
              "No argument-of-periapsis constraint on a circular orbit; over-constraining the terminal state is a classic PVG infeasibility. RO seeds false."),
            R("DesiredArgP",            AscentDisposition.RoDefault, "0 (RO)",
              "Unread while DesiredArgPFlag is false. RO seeds it anyway."),

            // ---- §7.5's plane launch: the flags around the button ------------------------------------
            R("LaunchLANDifference",  AscentDisposition.RoDefault,     "0 (RO)",
              "§7.5: '0 for the exact plane', and LAUNCH_LAN_DIFFERENCE = 0 is what ApplyRODefaults seeds. ⭐ The staleness fear that used to justify writing it is answered better: `MechConductor.SolveWindow` reads the LIVE field into WindowInputs.LanDifferenceDeg, exactly as MechJebModuleAscentMenu.cs:249 does, so whatever the box holds is what the window is solved with."),
            R("LaunchingToMatchLan",  AscentDisposition.RoDefault,     "false (non-persisted)",
              "One of MechJeb's own 'some non-persisted values' (MechJebModuleAscentSettings.cs:273-278) — false at every scene load, and only its own button sets it. It CANNOT be stale, so writing it asserted nothing."),
            R("LaunchingToLan",       AscentDisposition.RoDefault,     "false (non-persisted)",
              "Same block, same reason. ⚠ And harmless besides while LaunchingToPlane is true: SetTarget:103 ORs all three into `lanflag` and :104 takes the LAN from the target whenever LaunchingToPlane or LaunchingToMatchLan is set, so DesiredLan is not reachable on our path."),
            R("DesiredLan",           AscentDisposition.RoDefault, "0",
              "Read only by the manual 'Launch to LAN' mode, which we do not use."),
            R("RelativeLAN",          AscentDisposition.RoDefault, "false",
              "UI control: the 'Use LAN relative to launch site' toggle. Field default false, RO does not seed it, and a launch-to-plane user leaves it — the target's ABSOLUTE LAN is what MinimumTimeToPlane was given."),
            R("OverrideWarpToPlane",  AscentDisposition.RoDefault, "false (non-persisted)",
              "UI control: the 'Override Warp to Plane' toggle. StartCountdown:102-107 branches on it — true means 'launch NOW' — but it is in the same non-persisted block, so it is false at every scene load and only that toggle can set it. Nothing we do can leave it stale."),

            // ---- the pitch program ------------------------------------------------------------------
            R("PitchStartHeight",   AscentDisposition.RoDefault,     "100 m (RO)",
              "PITCH_START_HEIGHT_DEFAULT. Where the vertical rise ends — an ascent-SHAPING value, which is exactly the class the 2026-09-08 directive returns to RO."),
            R("CorrectiveSteering",     AscentDisposition.RoDefault, "false",
              "CLASSIC-path steering correction; MechJebModuleAscentPSGAutopilot never reads it."),
            R("CorrectiveSteeringGain",AscentDisposition.RoDefault, "3.0",
              "The gain for the row above; unread under PSG for the same reason."),

            // ---- roll ------------------------------------------------------------------------------
            R("ForceRoll",     AscentDisposition.RoDefault,     "true (MechJeb's field default)",
              "UI control: the 'Force Roll' toggle. Our old write was byte-identical to the field default, so dropping it changes no flown value. ⚠ register W7 is HELD on a roll-trim question and this row is not an answer to it."),
            R("VerticalRoll",  AscentDisposition.RoDefault, "0 (field default)", "The roll held during the vertical rise. Identical to what we used to write."),
            R("TurnRoll",      AscentDisposition.RoDefault, "0 (field default)", "The roll held through the turn. Identical to what we used to write."),
            R("RollAltitude",  AscentDisposition.RoDefault, "50 m (field default)", "Where one becomes the other. Identical to what we used to write."),

            // ---- the AoA / q-alpha limiters ---------------------------------------------------------
            R("LimitQa",   AscentDisposition.RoDefault,     "2000 Pa-rad (RO)",
              "LIMIT_QA_DEFAULT. The PVG q-alpha cap, and the most ascent-shaping number on the screen after the pitch program — so it is RO's, not ours. The PSG-settings window itself calls 1000-4000 the recommended band, and 2000 sits in it."),
            R("LimitAoA",  AscentDisposition.RoDefault, "true",
              "Its own source comment says '/* classic AoA limiter */'; PSG uses LimitQa instead."),
            R("MaxAoA",    AscentDisposition.RoDefault, "5 deg", "Classic limiter's cap — unread under PSG."),
            R("AOALimitFadeoutPressure", AscentDisposition.RoDefault, "2500 Pa", "Classic limiter — unread under PSG."),
            R("LimitingAoA", AscentDisposition.RoDefault, "false (a status flag, not a setting)",
              "Written by the autopilot itself every Drive; ours to read, never to set."),

            // ---- the PSG stage model ----------------------------------------------------------------
            R("MinDeltaV",  AscentDisposition.RoDefault, "40 m/s (RO)",
              "MIN_DELTAV_DEFAULT — the filter that drops trivial stages from the phase table. ⚠ `pure/PvgPreflight.WouldBuildAPhase` and `OptimizeStageFlagFor` both READ it; both read the LIVE field, so leaving it at RO's value is safe and is what the menu's own loop compares against."),
            R("LastStage",  AscentDisposition.RoDefault, "-1 (field default; the menu clamps it to 0)",
              "UI box: 'Last Stage:'. ⚠ The PSG-settings window CLAMPS it to [0, top KSP stage] when it draws (`:61`), so a user's is 0 rather than -1 — behaviourally identical here, because the glue ball's filter is `kspStage < LastStage` and KSP stage numbers are never negative. Left alone rather than half-reproduced."),
            R("MaxCoast",   AscentDisposition.RoDefault, "450 s (RO)",
              "MAX_COAST_DEFAULT. The longest coast the optimiser may insert between burns — it bounds the search, which makes it ascent-shaping and therefore RO's."),
            R("MinCoast",   AscentDisposition.RoDefault, "0 s (RO)",
              "MIN_COAST_DEFAULT. Zero lets the optimiser choose no coast at all."),
            R("CoastStageFlag",     AscentDisposition.RoDefault, "false (RO)", "No fixed coast stage; the optimiser places it."),
            R("CoastStageInternal", AscentDisposition.RoDefault, "-1 (RO)",
              "The stage a fixed coast would follow; unread while the flag is false, and RO seeds both together."),
            R("CoastLocation",      AscentDisposition.RoDefault, "-1 (field default = 'Coast Before')",
              "UI radio: Coast Before / During / After. RO does not seed it; -1 is the field default and the radio a user finds already selected."),
            R("SpinupStageFlag",    AscentDisposition.RoDefault, "false (RO)",
              "⚠ MechJeb's FIELD default is true; RO's seed is false. No spin-stabilised stage on a Falcon 9, and RO already says so."),
            R("SpinupStageInternal",AscentDisposition.RoDefault, "-1 (RO)", "Unread while SpinupStageFlag is false; RO seeds both."),
            R("SpinupLeadTime",     AscentDisposition.RoDefault, "50 s", "Unread while SpinupStageFlag is false."),
            R("SpinupAngularVelocity", AscentDisposition.RoDefault, "tau/6 rad", "Unread while SpinupStageFlag is false."),
            R("UnguidedStagesFlag", AscentDisposition.RoDefault, "false (RO)", "Every stage of this vehicle is guided, and RO's seed already says so."),
            R("UnguidedStagesInternal", AscentDisposition.RoDefault, "empty", "Unread while the flag is false."),
            R("UnguidedStages",     AscentDisposition.RoDefault, "(derived)", "A read-only projection of the two rows above."),
            R("FixedStagesFlag",    AscentDisposition.RoDefault, "false (RO)",
              "No stage is pinned to a fixed burn time, and RO seeds it. ⚠ It is also the input the menu's OptimizeStageFlag loop tests against, so leaving it at RO's false is what makes that loop derive TRUE."),
            R("FixedStagesInternal",AscentDisposition.RoDefault, "empty", "Unread while the flag is false."),
            R("FixedStages",        AscentDisposition.RoDefault, "(derived)", "A read-only projection of the two rows above."),
            R("PreStageTime",       AscentDisposition.RoDefault, "10 s (RO)", "How long before a stage event the glue ball stops re-solving. PRE_STAGE_TIME_DEFAULT."),
            R("OptimizerPauseTime", AscentDisposition.RoDefault, "5 s (RO)",  "How long after one it stays paused. OPTIMIZER_PAUSE_TIME_DEFAULT."),
            R("Cd",                 AscentDisposition.RoDefault, "0.5 (field default)",  "UI box. The drag coefficient the solver's atmosphere model fits to — ascent-shaping, so it is left alone."),
            R("Aref",               AscentDisposition.RoDefault, "0 (field default = auto)", "UI box. Reference area; 0 lets the glue ball derive it."),

            // ---- the thrust controller: RO seeds every one of these ---------------------------------
            R("Core.Thrust.MaxDynamicPressure", AscentDisposition.RoDefault, "50000 Pa (RO)",
              "⚠ ApplyRODefaults sets it TWICE — 20000 early, then 50000 at the end ('a less restrictive initial limit on maxQ'), so RO's real seed is 50000. Unread while Q2's limiter is off."),
            R("Core.Thrust.MinThrottle",        AscentDisposition.RoDefault, "0.05 (RO)", "The floor RO gives an RO/RF engine."),
            R("Core.Thrust.LimiterMinThrottle", AscentDisposition.RoDefault, "true (RO)",  "Clamp to that floor rather than command below it."),
            R("Core.Thrust.LimitToPreventUnstableIgnition", AscentDisposition.RoDefault, "false (RO)",
              "RO turns this off; RealFuels' own ullage model owns ignition stability, and `src/Ullage.cs` is what reads it."),
            R("Core.Thrust.AutoRCSUllaging",    AscentDisposition.RoDefault, "true (RO)",  "RCS settling before an RO relight."),
            R("Core.Thrust.LimitThrottle",      AscentDisposition.RoDefault, "false (RO)", "No blanket throttle cap."),
            R("Core.Thrust.LimitAcceleration",  AscentDisposition.RoDefault, "false (RO)", "No g-limit; the crew limit is not modelled here."),
            R("Core.Thrust.LimitToPreventOverheats", AscentDisposition.RoDefault, "false (RO)", "RealHeat owns heating."),

            // ---- CLASSIC-PATH ONLY: on the screen, never read while AscentType is PSG --------------
            R("TurnStartAltitude", AscentDisposition.ClassicOnly, "500 m",   "CLASSIC gravity turn."),
            R("TurnStartVelocity", AscentDisposition.ClassicOnly, "50 m/s",  "CLASSIC gravity turn."),
            R("TurnEndAltitude",   AscentDisposition.ClassicOnly, "60 km",   "CLASSIC gravity turn."),
            R("TurnEndAngle",      AscentDisposition.ClassicOnly, "0 deg",   "CLASSIC gravity turn."),
            R("TurnShapeExponent", AscentDisposition.ClassicOnly, "0.4",     "CLASSIC gravity turn."),
            R("AutoPath",          AscentDisposition.ClassicOnly, "true",    "CLASSIC gravity turn."),
            R("AutoTurnPerc",      AscentDisposition.ClassicOnly, "0.05",    "CLASSIC gravity turn."),
            R("AutoTurnSpdFactor", AscentDisposition.ClassicOnly, "18.5",    "CLASSIC gravity turn."),
        };

        static AscentSetting R(string name, AscentDisposition how, string value, string why)
        {
            AscentSetting s;
            s.Name = name; s.How = how; s.Value = value; s.Why = why;
            return s;
        }

        // =========================================================================================
        // 3. THE AUDIT'S OWN INVARIANTS — so "no value is left as whatever the profile set" is TESTED
        // =========================================================================================

        /// <summary>Is this setting accounted for at all? A name the audit does not carry is a box on
        /// MechJeb's screen nobody has decided about, which is the exact state S219 exists to end.</summary>
        public static bool Accounted(string name)
        {
            for (int i = 0; i < Audit.Length; i++)
                if (string.Equals(Audit[i].Name, name, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>The row for a setting, or a row whose `Name` is null when there is none.</summary>
        public static AscentSetting Row(string name)
        {
            for (int i = 0; i < Audit.Length; i++)
                if (string.Equals(Audit[i].Name, name, StringComparison.Ordinal)) return Audit[i];
            return default(AscentSetting);
        }

        /// <summary>How many rows carry the given disposition.</summary>
        public static int CountOf(AscentDisposition how)
        {
            int n = 0;
            for (int i = 0; i < Audit.Length; i++) if (Audit[i].How == how) n++;
            return n;
        }

        /// <summary>The first name that appears twice, or null. A duplicated row means two decisions
        /// about one box, and the later one wins silently.</summary>
        public static string FirstDuplicate()
        {
            for (int i = 0; i < Audit.Length; i++)
                for (int j = i + 1; j < Audit.Length; j++)
                    if (string.Equals(Audit[i].Name, Audit[j].Name, StringComparison.Ordinal))
                        return Audit[i].Name;
            return null;
        }

        /// <summary>The first row with no name, no value or no reason, or null. A reason-less row is an
        /// entry in a table, not a decision.</summary>
        public static string FirstUnexplained()
        {
            for (int i = 0; i < Audit.Length; i++)
            {
                AscentSetting s = Audit[i];
                if (string.IsNullOrEmpty(s.Name)) return "<row " + i + " has no name>";
                if (string.IsNullOrEmpty(s.Value)) return s.Name + " (no value)";
                if (string.IsNullOrEmpty(s.Why) || s.Why.Length < 20) return s.Name + " (no reason)";
            }
            return null;
        }

        /// <summary>⭐ The ordering `TerminalCountS` MUST satisfy, asserted rather than trusted: the
        /// conductor takes the terminal count AFTER MechJeb's warp+guidance start and BEFORE our own
        /// ignition lead, or one of the two mechanisms is dead.</summary>
        public static bool CountdownOrderingHolds(double ignitionLeadS)
        {
            return WarpCountDownS > TerminalCountS && TerminalCountS > ignitionLeadS && ignitionLeadS > 0.0;
        }

        // =========================================================================================
        // 4. ⭐⭐ S222b — THE PSG SETTINGS MENU's OWN `OptimizeStageFlag` LOOP, TRANSCRIBED
        // =========================================================================================
        //
        // ⛔ THIS IS NOT A MECHANISM OF OURS AND IT IS NOT A CHOICE. It is 20 lines of
        // `MechJebModuleAscentPSGSettingsMenu.WindowGUI` (`:57-85`), which every RO user runs every
        // frame because `ApplyRODefaults()` opens that window for them. We suppress MechJeb's GUI
        // (T15b), so without this the flag sits at its at-rest `false` forever — a state no user of
        // MechJeb is ever in, and the state that made RO's 110 km attach altitude a live constraint on
        // a 215 km orbit. See `AttachAltFollowsMissionApsis` for the full unwind.
        //
        // The vendored loop, verbatim:
        //     if (vacStats.Count > 0 && _ascentSettings.LastStage <= vacStats[vacStats.Count-1].KSPStage)
        //     {
        //         _ascentSettings.LastStage.Val = Clamp(_ascentSettings.LastStage.Val, 0, vacStats[last].KSPStage);
        //         _ascentSettings.OptimizeStageFlag = false;
        //         foreach (FuelStats stats in Core.StageStats.VacStats)
        //         {
        //             if (stats.KSPStage < _ascentSettings.LastStage) continue;
        //             if (stats.DeltaV < _ascentSettings.MinDeltaV.Val) continue;
        //             ...
        //             if (_ascentSettings.FixedStages.Contains(stats.KSPStage)) { /* label only */ }
        //             else _ascentSettings.OptimizeStageFlag = true;
        //         }
        //     }
        //
        // ⚠ ONE FAITHFUL DIFFERENCE, AND IT IS THE CLAMP. The menu also CLAMPS `LastStage` into
        // [0, topKSPStage] as a side effect of drawing. We do NOT reproduce that write — `LastStage` is
        // a `RoDefault` row and -1 vs 0 is behaviourally identical downstream (the glue ball's filter is
        // `kspStage < LastStage`, and KSP stage numbers are never negative). ⛔ But the CLAMPED value is
        // what the menu's own `continue` compares against, so `Applies`/`OptimizeStageFlagFor` take the
        // clamp into account when reading, exactly as the menu does — reproducing the read without
        // reproducing the write.

        /// <summary>One row of MechJeb's `Core.StageStats.VacStats`, reduced to the three fields the
        /// menu's loop actually reads. PURE: the glue projects `FuelStats` onto this.</summary>
        public struct PsgStage
        {
            /// <summary>`FuelStats.KSPStage`.</summary>
            public int KspStage;
            /// <summary>`FuelStats.DeltaV`, m/s.</summary>
            public double DeltaVMps;
            /// <summary>Is this stage in `AscentSettings.FixedStages`? (Empty whenever
            /// `FixedStagesFlag` is false, which is RO's own seed — so normally always false.)</summary>
            public bool Fixed;
        }

        /// <summary>
        /// The menu's OUTER guard: it only touches `OptimizeStageFlag` at all when the stage table has
        /// rows and `LastStage` is not above the top stage. ⛔ When this is false the menu writes
        /// NOTHING, so neither do we — a momentarily-empty `VacStats` must not clear a flag the last
        /// good frame derived.
        /// </summary>
        public static bool OptimizeStageFlagApplies(PsgStage[] stages, int lastStage)
        {
            if (stages == null || stages.Length == 0) return false;
            return lastStage <= stages[stages.Length - 1].KspStage;
        }

        /// <summary>
        /// The menu's INNER loop. True when at least one listed stage above the ΔV floor is not pinned
        /// to a fixed burn time — which, with RO's own `FixedStagesFlag = false`, is every stage the
        /// filter keeps. Only meaningful when <see cref="OptimizeStageFlagApplies"/> is true.
        /// </summary>
        public static bool OptimizeStageFlagFor(PsgStage[] stages, int lastStage, double minDeltaVMps)
        {
            if (!OptimizeStageFlagApplies(stages, lastStage)) return false;

            // The menu clamps `LastStage` into [0, top] before comparing; we read the clamped value
            // without writing it back (see the block above).
            int top = stages[stages.Length - 1].KspStage;
            int floor = lastStage < 0 ? 0 : (lastStage > top ? top : lastStage);

            bool optimize = false;
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i].KspStage < floor) continue;
                if (stages[i].DeltaVMps < minDeltaVMps) continue;
                if (!stages[i].Fixed) optimize = true;
            }
            return optimize;
        }

        // =========================================================================================
        // 5. THE RENDER — the table, where a human will see it
        // =========================================================================================

        /// <summary>One screen/log-ready line per row — the table, rendered where a human will see it.
        /// `MechConductor.Configure` writes this to `KSP.log` once per configure.
        /// ⭐ S222b added the two counts that make the owner's question answerable at a glance: how many
        /// boxes we WRITE (8, down from 47) and how many the MENU writes for us (2).
        /// ⛔⛔ **S223: EVERY NUMBER IN THIS SENTENCE IS COMPUTED FROM OUR OWN INTENT.** Nothing in it
        /// was ever read out of MechJeb, and the owner's 2026-09-08 brief is blunt about the
        /// consequence: the mod logged `ApplyTune` success against a cfg carrying `PitchRate 0.75` while
        /// the vehicle flew RO's 5.0, and both statements sat in the same log with nothing able to
        /// reconcile them. The MEASUREMENT is `pure/AscentReadback.cs` + `src/MechAscentReadback.cs`, and
        /// `MechConductor.Configure` appends its verdict to this very line — so the claim can no longer
        /// appear without the read-back beside it. ⚠ No row below changed; what changed is what stands
        /// next to them.</summary>
        public static string Render()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("ASCENT SETTINGS AUDIT — ").Append(Audit.Length).Append(" boxes: ")
              .Append(CountOf(AscentDisposition.Write)).Append(" written, ")
              .Append(CountOf(AscentDisposition.RuntimeMission)).Append(" written at runtime, ")
              .Append(CountOf(AscentDisposition.UiDerived)).Append(" UI-derived (MechJeb's own menus compute these), ")
              .Append(CountOf(AscentDisposition.RoDefault)).Append(" left at the RO default, ")
              .Append(CountOf(AscentDisposition.ClassicOnly)).Append(" CLASSIC-only (unread under PSG), ")
              .Append(CountOf(AscentDisposition.OwnerQuestion)).Append(" ⛔ AWAITING THE OWNER");
            for (int i = 0; i < Audit.Length; i++)
                if (Audit[i].How == AscentDisposition.OwnerQuestion)
                    sb.Append("\n    ⛔ ").Append(Audit[i].Name).Append(" = ").Append(Audit[i].Value);
            return sb.ToString();
        }
    }
}
