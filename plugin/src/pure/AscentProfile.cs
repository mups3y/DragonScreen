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
// PURE: no Unity, no KSP, no MechJeb reference. `MechConductor.Configure` is the only caller.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>What the conductor does about one exposed setting.</summary>
    public enum AscentDisposition : byte
    {
        /// <summary>We WRITE it, at configure time, to the value in the row. Either it is a mission fact,
        /// or the vendored source calls it mandatory, or it is load-bearing enough that inheriting it
        /// from a persisted cfg is a risk we decline to take.</summary>
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
        OwnerQuestion
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

        /// <summary>§7.5: *"`LaunchLANDifference` = 0 for the exact plane."* Written, not assumed: it is
        /// subtracted from the target's LAN inside `Astro.MinimumTimeToPlane`, so a stale non-zero would
        /// aim the whole launch at a plane offset from the station's by that many degrees.</summary>
        public const double LaunchLanDifferenceDeg = 0.0;

        /// <summary>⭐ RO's own default is **110 km** and the flown orbit is **210 km**, and
        /// `docs/MECHJEB_MASTER_MAP.md` §7.2 names that gap as a BUG ALREADY FOUND ON THIS CRAFT:
        /// *"attach = orbit alt gives a clean circular insertion; attach &lt; peR = 'periapsis insertion'
        /// (elliptical), which is the bug found in the Crew-2 cfg (110 km attach vs 210 km orbit →
        /// fixed to 210)."* `MechJebModuleAscentPSGAutopilot.SetTarget:100-101` reads
        /// `DesiredAttachAltFixed` whenever `OptimizeStageFlag` is false — which is its default — so
        /// leaving this at the RO default would fly the documented elliptical insertion. It is therefore
        /// a MISSION FACT (the destination), written from the same `AscentTarget` as the apsides, and
        /// NOT a tune. `AttachAltFlag` is written with it for the same reason.</summary>
        public const bool AttachAltFollowsMissionApsis = true;

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
            // ---- THE FOUR THAT WERE ALREADY WRITTEN (S214/T18), restated with their authority --------
            R("AscentType",          AscentDisposition.Write,     "PSG",
              "§B8: 'AscentType — CLASSIC(0)/PVG(1). Target PVG(1).' Asserted because a persisted craft value could be CLASSIC."),
            R("Autostage",           AscentDisposition.Write,     "false",
              "§B8 owner directive 2026-09-03. Through the PROPERTY, never the `_autostage` field — only the property removes the ascent autopilot from `Core.Staging.Users`."),
            R("LimitQaEnabled",      AscentDisposition.Write,     "true",
              "The vendored menu asserts it every frame with the comment 'this is mandatory for PSG' (MechJebModuleAscentMenu.cs:374). §7.5 repeats it: mandatory for PVG."),
            R("DesiredInclination",  AscentDisposition.RuntimeMission, "the plane solve",
              "§7.5: the plane launch SETS it from `Astro.MinimumTimeToPlane`'s second return value, which carries the northgoing/southgoing SIGN. Written at the commit, not at configure."),

            // ---- THE DESTINATION -------------------------------------------------------------------
            R("DesiredOrbitAltitude",   AscentDisposition.RuntimeMission, "mission periapsis (210 km ISS)",
              "§B5's named exception: the destination is a MISSION FACT, not a tune. From `AscentTargets.For`."),
            R("DesiredApoapsis",        AscentDisposition.RuntimeMission, "mission apoapsis (210 km ISS)",
              "Same source, same authority."),
            R("DesiredAttachAltFixed",  AscentDisposition.RuntimeMission, "= the mission apoapsis, NOT RO's 110 km",
              "⭐ §7.2 names RO's 110 km-vs-210 km attach as a bug already found on this craft: attach < peR is an ELLIPTICAL 'periapsis insertion'. SetTarget:100-101 reads this field whenever OptimizeStageFlag is false, which is its default."),
            R("DesiredAttachAlt",       AscentDisposition.RuntimeMission, "= the mission apoapsis",
              "The OptimizeStageFlag=true twin of the row above. Written together so the two can never disagree."),
            R("AttachAltFlag",          AscentDisposition.Write,     "true",
              "With OptimizeStageFlag false, SetTarget:109 computes attachAltFlag = true regardless; written so the attach altitude is honoured if OptimizeStageFlag is ever turned on."),
            R("DesiredFPA",             AscentDisposition.Write,     "0",
              "RO default. A zero terminal flight-path angle IS the circular-insertion constraint; written because a stale non-zero would insert climbing or descending."),
            R("DesiredArgPFlag",        AscentDisposition.Write,     "false",
              "No argument-of-periapsis constraint on a circular orbit; over-constraining the terminal state is a classic PVG infeasibility."),
            R("DesiredArgP",            AscentDisposition.RoDefault, "0",
              "Unread while DesiredArgPFlag is false."),

            // ---- §7.5's PLANE LAUNCH ---------------------------------------------------------------
            R("LaunchingToPlane",     AscentDisposition.RuntimeMission, "true at the crew's GO",
              "⭐ THE flag that makes PVG target the target's RAAN and not merely its inclination (SetTarget:103-105 passes lanflag + Core.Target.TargetOrbit.LAN). Without it: §7.5's 'right inclination, wrong RAAN'."),
            R("LaunchLANDifference",  AscentDisposition.Write,     "0",
              "§7.5: '0 for the exact plane'. Subtracted from the target LAN inside MinimumTimeToPlane, so a stale value aims at an offset plane."),
            R("LaunchingToMatchLan",  AscentDisposition.Write,     "false",
              "The three launch modes are mutually exclusive in SetTarget:103-106; we fly the plane one."),
            R("LaunchingToLan",       AscentDisposition.Write,     "false",
              "Same exclusivity rule: SetTarget:103 ORs all three into `lanflag`, and :104 picks the LAN source from the first two, so a stale LaunchingToLan would target a manual LAN nobody set."),
            R("DesiredLan",           AscentDisposition.RoDefault, "0",
              "Read only by the manual 'Launch to LAN' mode, which we do not use."),
            R("RelativeLAN",          AscentDisposition.Write,     "false",
              "'Use LAN relative to launch site' — we target the STATION's absolute LAN, which is what MinimumTimeToPlane was given."),
            R("OverrideWarpToPlane",  AscentDisposition.Write,     "false",
              "⛔ LOAD-BEARING. StartCountdown:102-107 branches on it: true means 'launch NOW' (TimedLaunch=false, _launchTime=now). A stale true would turn the plane launch into an immediate one."),
            R("WarpCountDown",        AscentDisposition.Write,     "32 s",
              "MechJeb's own 'Launch countdown' box. Composed from the vendored tree's own '~20 s to converge' note + WarpPlan.BurnLeadS's 12 s. Stock's 11 s is too short for us because our T-0 waits on a guidance SOLUTION."),

            // ---- THE PITCH PROGRAM -----------------------------------------------------------------
            R("PitchStartHeight",   AscentDisposition.Write,     "100 m (RO default)",
              "Written rather than inherited: it is where the vertical rise ends, and a stale value from another craft's profile changes the whole trajectory."),
            R("PitchRate",          AscentDisposition.OwnerQuestion, "5.0 °/s (RO default) — the owner's own flown cfg says 0.75",
              "⛔ Q1. docs/reference/mechjeb_settings_type_Crew-Dragon.cfg:53 records PitchRate 0.75 on this very craft. Deviating from the RO baseline is a §B5/T22 tune and needs the owner (C1.8). We fly RO's 5.0 and ask."),
            R("CorrectiveSteering",     AscentDisposition.RoDefault, "false",
              "CLASSIC-path steering correction; MechJebModuleAscentPSGAutopilot never reads it."),
            R("CorrectiveSteeringGain",AscentDisposition.RoDefault, "3.0",
              "The gain for the row above; unread under PSG for the same reason."),

            // ---- ROLL ------------------------------------------------------------------------------
            R("ForceRoll",     AscentDisposition.Write,     "true (RO default)",
              "Written so the roll program is ours on the record. ⚠ register W7 is HELD on a roll-trim question and this row is not an answer to it."),
            R("VerticalRoll",  AscentDisposition.Write,     "0 (RO default)", "The roll held during the vertical rise."),
            R("TurnRoll",      AscentDisposition.Write,     "0 (RO default)", "The roll held through the turn."),
            R("RollAltitude",  AscentDisposition.Write,     "50 m (RO default)", "Where one becomes the other."),

            // ---- THE AoA / q-alpha LIMITERS --------------------------------------------------------
            R("LimitQa",   AscentDisposition.Write,     "2000 Pa·rad (RO default)",
              "The PVG q-alpha cap. Written because LimitQaEnabled is mandatory, so the VALUE is always live and must not be inherited."),
            R("LimitAoA",  AscentDisposition.RoDefault, "true",
              "Its own source comment says '/* classic AoA limiter */'; PSG uses LimitQa instead."),
            R("MaxAoA",    AscentDisposition.RoDefault, "5°", "Classic limiter's cap — unread under PSG."),
            R("AOALimitFadeoutPressure", AscentDisposition.RoDefault, "2500 Pa", "Classic limiter — unread under PSG."),
            R("LimitingAoA", AscentDisposition.RoDefault, "false (a status flag, not a setting)",
              "Written by the autopilot itself every Drive; ours to read, never to set."),

            // ---- THE PSG STAGE MODEL ---------------------------------------------------------------
            R("MinDeltaV",  AscentDisposition.Write, "40 m/s (RO default)",
              "The filter that drops trivial stages from the phase table. `pure/PvgPreflight.WouldBuildAPhase` reads it to decide whether the engage may proceed at all (S214), so it must be a known value."),
            R("LastStage",  AscentDisposition.Write, "-1 (all stages)",
              "Same reason: PvgPreflight reads it. -1 means 'no floor'."),
            R("MaxCoast",   AscentDisposition.Write, "450 s (RO default)",
              "The longest coast the optimiser may insert between burns; it bounds the search, so an inherited value changes which trajectories are reachable."),
            R("MinCoast",   AscentDisposition.Write, "0 s (RO default)",
              "The coast's lower bound. Zero lets the optimiser choose no coast at all, which is the right answer for a direct ISS insertion."),
            R("CoastStageFlag",     AscentDisposition.Write, "false", "No fixed coast stage; the optimiser places it."),
            R("CoastStageInternal", AscentDisposition.Write, "-1",
              "The stage a fixed coast would follow; unread while the flag is false, and written so a stale index cannot appear if it is ever turned on."),
            R("CoastLocation",      AscentDisposition.Write, "-1",
              "Which inter-stage boundary a fixed coast would sit at; -1 means none, and it is written so a persisted index cannot pin one."),
            R("SpinupStageFlag",    AscentDisposition.Write, "false", "RO default. No spin-stabilised stage on a Falcon 9."),
            R("SpinupStageInternal",AscentDisposition.Write, "-1",
              "Which stage would be spun up; unread while SpinupStageFlag is false, and written for the same anti-stale reason."),
            R("SpinupLeadTime",     AscentDisposition.RoDefault, "50 s", "Unread while SpinupStageFlag is false."),
            R("SpinupAngularVelocity", AscentDisposition.RoDefault, "τ/6 rad", "Unread while SpinupStageFlag is false."),
            R("UnguidedStagesFlag", AscentDisposition.Write, "false", "Every stage of this vehicle is guided."),
            R("UnguidedStagesInternal", AscentDisposition.RoDefault, "empty", "Unread while the flag is false."),
            R("UnguidedStages",     AscentDisposition.RoDefault, "(derived)", "A read-only projection of the two rows above."),
            R("FixedStagesFlag",    AscentDisposition.Write, "false", "No stage is pinned to a fixed burn time."),
            R("FixedStagesInternal",AscentDisposition.RoDefault, "empty", "Unread while the flag is false."),
            R("FixedStages",        AscentDisposition.RoDefault, "(derived)", "A read-only projection of the two rows above."),
            R("OptimizeStageFlag",  AscentDisposition.Write, "false",
              "⛔ Chosen deliberately, not inherited: it is what routes SetTarget:100-101 to DesiredAttachAltFixed, which is the field §7.2's attach-altitude bug lives in."),
            R("PreStageTime",       AscentDisposition.Write, "10 s (RO default)", "How long before a stage event the glue ball stops re-solving."),
            R("OptimizerPauseTime", AscentDisposition.Write, "5 s (RO default)",  "How long after one it stays paused."),
            R("Cd",                 AscentDisposition.Write, "0.5 (RO default)",  "The drag coefficient the solver's atmosphere model fits to."),
            R("Aref",               AscentDisposition.Write, "0 (RO default = auto)", "Reference area; 0 lets the glue ball derive it."),

            // ---- WHAT MECHJEB WOULD OTHERWISE ACTUATE ON OUR VEHICLE -------------------------------
            R("SkipCircularization", AscentDisposition.Write, "true (RO default is false)",
              "⛔ NOT A TUNE — it stops an ACTUATION we own. On EXIT, DriveCircularizationBurn:244-286 PLACES A MANEUVER NODE and hands it to Core.Node, which would collide with T19's own node executor and with the conductor's ClearNodes. PVG already inserts at the target orbit, so the node is redundant as well as unowned. In-repo evidence it is also the owner's own setting: docs/reference/mechjeb_settings_type_Crew-Dragon.cfg:34 `SkipCircularization = True`."),
            R("AutoDeploySolarPanels", AscentDisposition.Write, "false (RO default is true)",
              "⛔ §B12.7: direct part control is OURS. Left true, DrivePrelaunch:202-214 retracts panels and HOLDS the prelaunch mode until they are all retracted, and DriveDeployableComponents extends them above the atmosphere — part actuations the crew procedure owns."),
            R("AutoDeployAntennas",    AscentDisposition.Write, "false (RO default is true)",
              "Same rule, and RealAntennas is installed (docs/reference/INSTALLED_MODS.md), so this one really would deploy hardware."),

            // ---- AUTOWARP: ONE FLAG, THREE PHASES --------------------------------------------------
            R("Core.Node.Autowarp", AscentDisposition.Write, "true",
              "⭐ THE owner's 'auto warp for all modes'. It is ONE field and all three phases read it: the ascent countdown warps only `if (Core.Node.Autowarp)` (MechJebModuleAscentBaseAutopilot.cs:132); the node executor gates both of its warps on it (:242, :293); and the rendezvous autopilot NARROWS the same field rather than owning one (`Core.Node.Autowarp = Core.Node.Autowarp && Core.Target.Distance > 1000`). Docking never warps at all."),
            R("Core.Warp.activateSASOnWarp", AscentDisposition.Write, "false",
              "Left on, SetTimeWarpRate calls ActionGroups.SetGroup(SAS, true) on the way into warp — an action group on our vehicle (§B12.7) and SAS fighting the attitude controller besides."),

            // ---- THE THRUST CONTROLLER, INCLUDING THE OWNER'S NAMED ITEM ---------------------------
            R("Core.Thrust.LimitDynamicPressure", AscentDisposition.OwnerQuestion,
              "false (RO default) — the owner asked for max-Q throttle-down",
              "⛔ Q2. Owner, verbatim: 'We should also be ticking/selecting max q throttle down etc.' RO's ApplyRODefaults sets this FALSE twice, deliberately, and turning it on needs a Q magnitude — which is precisely the §B5/T22 one-parameter-at-a-time tune the Part-B gate defers. A build chat may not open that (C1.8/C1.12). We fly RO's default and ask, with a recommended value."),
            R("Core.Thrust.MaxDynamicPressure", AscentDisposition.Write, "50000 Pa (RO default)",
              "Written so the number is on the record even while the limiter above is off — if the owner turns it on, this is what it turns on AT."),
            R("Core.Thrust.MinThrottle",        AscentDisposition.Write, "0.05 (RO default)", "The floor RO gives an RO/RF engine."),
            R("Core.Thrust.LimiterMinThrottle", AscentDisposition.Write, "true (RO default)",  "Clamp to that floor rather than command below it."),
            R("Core.Thrust.LimitToPreventUnstableIgnition", AscentDisposition.Write, "false (RO default)",
              "RO turns this off; RealFuels' own ullage model owns ignition stability, and `src/Ullage.cs` is what reads it."),
            R("Core.Thrust.AutoRCSUllaging",    AscentDisposition.Write, "true (RO default)",  "RCS settling before an RO relight."),
            R("Core.Thrust.LimitThrottle",      AscentDisposition.Write, "false (RO default)", "No blanket throttle cap."),
            R("Core.Thrust.LimitAcceleration",  AscentDisposition.Write, "false (RO default)", "No g-limit; the crew limit is not modelled here."),
            R("Core.Thrust.LimitToPreventOverheats", AscentDisposition.Write, "false (RO default)", "RealHeat owns heating."),

            // ---- CLASSIC-PATH ONLY: on the screen, never read while AscentType is PSG --------------
            R("TurnStartAltitude", AscentDisposition.ClassicOnly, "500 m",   "CLASSIC gravity turn."),
            R("TurnStartVelocity", AscentDisposition.ClassicOnly, "50 m/s",  "CLASSIC gravity turn."),
            R("TurnEndAltitude",   AscentDisposition.ClassicOnly, "60 km",   "CLASSIC gravity turn."),
            R("TurnEndAngle",      AscentDisposition.ClassicOnly, "0°",      "CLASSIC gravity turn."),
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

        /// <summary>One screen/log-ready line per row — the table, rendered where a human will see it.
        /// `MechConductor.Configure` writes this to `KSP.log` once per configure.</summary>
        public static string Render()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("ASCENT SETTINGS AUDIT — ").Append(Audit.Length).Append(" boxes: ")
              .Append(CountOf(AscentDisposition.Write)).Append(" written, ")
              .Append(CountOf(AscentDisposition.RuntimeMission)).Append(" written at runtime, ")
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
