// DragonScreen — MechConductor  (KSP glue: the MechJeb-FACING half of the conductor)
// ============================================================================================
// Register T18 (§B12.2 / §B12.6 build-order step (4)). This is the file §B12.2's corrected entry
// promised and W10 deliberately did not deliver: *"The MechJeb-FACING half described above — reading a
// `ConductorAction` and executing it against `MechJebCore` modules — is NEW work arriving with T18
// onward; W10's recovery does not by itself deliver it."*
//
// ---- WHERE IT SITS, AND WHY IT IS ITS OWN FILE ----
// §B12.8 rider (c): *"every later Wave E / T-series line GROWS THIS SAME HOST by exactly the dispatch
// its own controller needs, one increment at a time."* The HOST is `src/FlightDriver.cs`, and it grows
// by exactly one line for this increment — `MechConductor.Tick(v)` — the same shape W9 used for
// `MissionConductor.Tick(v)`. The executor itself lives here so the host stays readable.
//
//     FlightDriver.FixedUpdate (physics rate, flight scene)
//         └─ CrewProcedureOps.Tick(v)        walks the mission plan, holds at the crew gates   (W10)
//         └─ MechConductor.Tick(v)           THIS FILE
//              ├─ Conductor.Decide(...)      PURE: which module, which op, advance/hold/re-plan (T16)
//              ├─ the embedded MechJebCore   PVG steers and throttles                     (T15a/T15b)
//              └─ AscentSequence.Step(...)   PURE: the stage events MechJeb is forbidden to do (T18)
//
// ---- ⛔ WHAT IS LIVE AFTER T18, AND WHAT IS STILL AN HONEST NO-OP (§14.4(a), stated per §B12.5) ----
// LIVE, and only inside `MissionPhase.Ascent` with AUTO SEQUENCE engaged:
//     • MechJeb drive authority (`DragonMechJebCore.AuthorizeDrive`) — PVG steers and throttles;
//     • octaweb ignition, hold-down release, MECO shutdown, booster separation, MVac ignition,
//       Dragon separation, nose-cone open — all through the recovered `src/Actuator.cs` (§B12.7).
// STILL A NO-OP after T18:
//     • every flight button on every screen — `FlightCommands.Run` is untouched by this increment;
//     • `StationApproach` / `DockingOps` / `UndockOps` / `DeorbitOps` — T19/T20/T21 own those;
//     • ABORT — `Conductor` can DECIDE `Abort`, and all this file does with it is hand the vehicle
//       BACK to the crew. There is no abort executor in the tree (register W19, HELD).
//
// ---- ⚠⚠ THE HAZARD T15d HANDED TO T18 BY NAME, AND WHAT IS DONE ABOUT IT ----
// `src/MechHost.cs` item (6) is explicit: *"⚠ WHAT TURNING IT ON RE-ARMS, so T18 cannot be surprised
// by it: MechJeb becomes the vessel's master core, so `Drive` runs, so `VesselState.Update()` runs
// every FixedUpdate — including `AnalyzeParts`, which is where stock KSP's
// `ModuleGimbal.GetPotentialTorque` threw."* On the 2026-09-05 glass that produced **6,935
// `ArgumentOutOfRangeException`s**. T15d closed it by never being master; T18 has to be master, so the
// storm is back on the table.
//
// ⚠ THE MITIGATION BELOW IS A HYPOTHESIS, LABELLED AS ONE. `VesselState.cs:958-971` calls
// `gimbal.CreateEngineList()` **only when `engineMultsList` is null** and then indexes it; a list that
// EXISTS but is STALE — which is exactly what a decouple or an RF engine reconfigure leaves behind —
// is the shape that indexes past the end. So `RefreshGimbalEngineLists` calls stock KSP's own public
// `ModuleGimbal.CreateEngineList()` whenever the part tree has just changed under us. That is a stock
// API call on a stock module: no vendored file is edited (§B12.1's rename-shell rule), it costs eight
// calls across a whole ascent, and if the hypothesis is wrong it costs nothing but those calls.
// ⛔ IT IS NOT CLAIMED AS A FIX. Whether the storm recurs is a row on T18's flight checklist, and the
// honest answer today is that nobody has measured it since the core became master.
// ============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using MuMech = DragonScreen.Mech.MuMech;
// S215: the vendored plane-crossing maths. ⭐ THE VALUE THIS FILE WARPS TO IS **THIS** ONE — the
// pure `LaunchWindow` mirror is computed alongside it and only ever used to CHECK it (see
// `SolveWindow`). One source of truth in flight; a tested one on the bench.
using MechAstro = DragonScreen.Mech.MechJebLib.Functions.Astro;

namespace DragonScreen
{
    /// <summary>
    /// Turns the pure conductor's decisions into calls on the ONE embedded <c>MechJebCore</c> and on
    /// the recovered <c>Actuator</c>. Ticked by <see cref="FlightDriver"/> at physics rate, and by
    /// nothing else.
    /// </summary>
    public static class MechConductor
    {
        // ── the bound core (§B12.7: resolve at the phase boundary, never search per frame) ────────
        static DragonMechJebCore core;
        static uint boundVesselId;

        /// <summary>True once an embedded core has been found on the active vessel. `FlightDriver.
        /// HasControllerFor` reads this, so the glass never names a phase with nothing behind it.</summary>
        public static bool Available { get { return core != null; } }

        /// <summary>True while MechJeb actually holds the vehicle. The honest "something is flying".</summary>
        public static bool Flying { get { return core != null && core.DriveAuthorized; } }

        // ── conductor state ───────────────────────────────────────────────────────────────────────
        static AscentStep ascentStep = AscentStep.Idle;
        static double stepStartUT;
        static bool launchLatched;
        static bool configured;
        static string note = "idle";
        static bool ascentEngaged;
        /// <summary>S214: latches the PVG engage-hold log so a per-tick wait cannot flood `KSP.log`.</summary>
        static bool stageStatsWaitLogged;

        // ── S215: the launch window (§B9 Phase 0/1, docs/MECHJEB_MASTER_MAP.md §7.5) ─────────────
        /// <summary>The solved window. `Verdict != Armed` while there is nothing to count down to.</summary>
        static WindowPlan window;
        /// <summary>UT of the window's T-0. 0 = none solved.</summary>
        static double launchWindowUT;
        /// <summary>Latches the window-hold log so a per-tick refusal cannot flood `KSP.log` (S214's rule).</summary>
        // S220: one attempt per flight scene to put the station on the target, on the pad only.
        // Set when the attempt has been MADE, not when it succeeded - a refusal must not retry every
        // frame and re-log. Cleared by Reset() with everything else.
        static bool stationTargetTried;

        static bool windowHoldLogged;
        /// <summary>
        /// The warp has been commanded once. ⛔ ONCE IS CORRECT, not a shortcut:
        /// `MechJebModuleWarpController.OnFixedUpdate` re-issues its own `warpToUT` every frame until
        /// the UT passes, so it re-establishes the warp by itself even if the player cancels it.
        /// Re-commanding here each tick would fight that module rather than help it.
        /// </summary>
        static bool warpArmed;                // ⛔ S219: unused — see the superseded warp block below

        /// <summary>S219: MechJeb's own countdown was armed at the commit (§7.5's `StartCountdown`).
        /// False means the arm failed and there is no autowarp; the committed T-0 still stands.</summary>
        static bool countdownArmed;

        /// <summary>S219: `TimedLaunch` has been cleared, so MechJeb no longer has a T-0 of any kind and
        /// `StageManager.ActivateNextStage()` is unreachable. One-shot, at T-`TerminalCountS`.</summary>
        static bool terminalCountTaken;

        // ── T19: the on-orbit leg (§B9 P2/P3, §B10.2, §B12.4) ────────────────────────────────────
        static RendezvousLeg leg;            // which leg, from the gate it walks to
        static int approachStepsDone;        // how far down §B12.3's chain this pass has got
        static bool nodePlanned;             // a node was BUILT for this chain step (a latch, not a count)
        static bool nodeBurned;              // ...and it has been flown to completion
        static bool nodeExecuting;           // the Node Executor has been handed the node
        static double lastNodeDvLeft;        // Δv still on the node LAST tick — the cutoff residual
        static double nodeResidualMps;       // what was left when the node disappeared
        static double lastRangeM, lastRangeUT, openingRateMps;
        static int approachPasses;           // how many times this leg has walked the chain
        static string approachNote;          // what the far-field lamp says

        /// <summary>⭐ S219 JOB 2 — WHICH rendezvous driver flies the target-relative ops. Defaults to
        /// MechJeb's own autopilot on the owner's `OVERRIDE` of 2026-09-07; `pure/RendezvousOps.cs`'s
        /// `RendezvousDrive` carries his words and what each mode costs. ⛔ The conductor's own
        /// node-composing path is intact and one assignment away.</summary>
        static RendezvousDrive rendezvousMode = RendezvousDrive.MechJebAutopilot;

        /// <summary>S219: MechJeb's rendezvous autopilot currently holds the vehicle through us.</summary>
        static bool rendezvousEngaged;

        /// <summary>S219: which driver is flying, for a screen or a log. Read-only — changing it is
        /// `SelectRendezvousDrive`, which stands the running one down first.</summary>
        public static RendezvousDrive RendezvousMode { get { return rendezvousMode; } }

        /// <summary>S219: switch rendezvous drivers. Stands the outgoing one down rather than leaving it
        /// holding the attitude/thrust user pools — two drivers on one vehicle is the failure this
        /// whole file's `Owner` sentinel exists to prevent.</summary>
        public static void SelectRendezvousDrive(RendezvousDrive drive)
        {
            if (drive == rendezvousMode) return;
            if (rendezvousEngaged) ReleaseRendezvous("the crew selected " + drive);
            rendezvousMode = drive;
            Debug.Log("[DragonScreen] conductor: rendezvous driver -> " + drive + " (S219)");
        }

        // ── T20: the docking leg (§B9 P4, §B10.3, O6) ────────────────────────────────
        static DockingLeg dockLeg;
        static bool dockingEngaged;
        static bool manualDockingRequested;
        static string dockingNote;
        static MuMech.MechJebModuleSmartASS.Target smartAssTarget =
            MuMech.MechJebModuleSmartASS.Target.OFF;

        /// <summary>Which docking step is being flown. `None` when the conductor is not on one.</summary>
        public static DockingLeg Dock { get { return dockLeg; } }

        /// <summary>⭐ THE §B12.5a FACADE READ FOR `DockingOps`. True exactly while the Docking Autopilot
        /// holds the vehicle on the CAPTURE leg — dark when berthed, dark when the crew have taken
        /// manual docking, and dark whenever the core is not driving.</summary>
        public static bool DockingEngaged { get { return Flying && dockingEngaged; } }

        /// <summary>The docking lamp's caption. Null when nothing is engaged.</summary>
        public static string DockingNote { get { return DockingEngaged ? dockingNote : null; } }

        // ── T21: the return leg (§B9 P6-P10) ─────────────────────────────────────────
        static ReturnStep returnStep = ReturnStep.Idle;
        static double returnStepStartUT;
        static string returnNote;

        /// <summary>Where the return has got to.</summary>
        public static ReturnStep Return { get { return returnStep; } }

        /// <summary>⭐ THE §B12.5a FACADE READ FOR `UndockOps` — T21 increment 1. True while the
        /// conductor is flying §B9 Phase 6: the back-away and the trunk jettison, out to §B11's
        /// Approach Ellipsoid. ⛔ Dark once the departure is done and dark on the deorbit side.</summary>
        public static bool UndockEngaged
        {
            get
            {
                return Flying && (returnStep == ReturnStep.Backout
                               || returnStep == ReturnStep.TrunkJettison);
            }
        }

        /// <summary>⭐ THE §B12.5a FACADE READ FOR `DeorbitOps` — T21 increment 2. True from the moment
        /// the crew's G15 GO starts the deorbit plan through to splashdown. ⛔ Dark on the departure
        /// leg — §B12.5a is explicit that these are ONE task and TWO increments, never both at once.</summary>
        public static bool DeorbitEngaged
        {
            get { return Flying && ReturnSequence.Beyond(returnStep)
                                && returnStep != ReturnStep.Complete; }
        }

        /// <summary>The return lamps' caption.</summary>
        public static string ReturnNote
        {
            get { return (UndockEngaged || DeorbitEngaged) ? returnNote : null; }
        }

        /// <summary>Which on-orbit leg is being flown. `None` when the conductor is not on one.</summary>
        public static RendezvousLeg Leg { get { return leg; } }

        /// <summary>⭐ THE §B12.5a FACADE READ FOR `StationApproach`. True exactly while the conductor
        /// holds the vehicle on a far-field rendezvous leg — never while it is idle, holding at a gate,
        /// or inside the Keep-Out Sphere (that is `DockingOps`, T20).</summary>
        public static bool ApproachEngaged
        {
            get { return Flying && leg != RendezvousLeg.None; }
        }

        /// <summary>The far-field lamp's caption. Null when nothing is engaged.</summary>
        public static string ApproachNote { get { return ApproachEngaged ? approachNote : null; } }

        /// <summary>Where the ascent chain has got to. Read by the tests' glass checklist and the log.</summary>
        public static AscentStep Ascent { get { return ascentStep; } }

        /// <summary>The last decision's reason, in words — what a lamp or a log line prints.</summary>
        public static string Note { get { return note; } }

        /// <summary>The object MechJeb's `UserPool` sees as the owner of every module we engage. A
        /// plain sentinel: MechJeb only ever compares it, never calls it.</summary>
        static readonly object Owner = new object();

        // ⛔ A NEW FLIGHT SCENE MUST START COLD. Everything here is static and survives a scene change,
        // so without this the last flight's ascent step and drive authority carry onto the next vehicle.
        // Called from `FlightDriver.Start` alongside the other resets.
        public static void Reset()
        {
            core = null; boundVesselId = 0;
            ascentStep = AscentStep.Idle; stepStartUT = 0.0;
            launchLatched = false; configured = false; ascentEngaged = false; stageStatsWaitLogged = false;
            window = new WindowPlan(); launchWindowUT = 0.0;
            windowHoldLogged = false; warpArmed = false;
            stationTargetTried = false;
            countdownArmed = false; terminalCountTaken = false;   // S219
            rendezvousEngaged = false; rendezvousMode = RendezvousDrive.Conductor;   // S219
            note = "idle";
            ResetLeg();
            leg = RendezvousLeg.None;
            dockLeg = DockingLeg.None; dockingEngaged = false; dockingNote = null;
            returnStep = ReturnStep.Idle; returnStepStartUT = 0.0; returnNote = null;
            manualDockingRequested = false;
            smartAssTarget = MuMech.MechJebModuleSmartASS.Target.OFF;
            lastRangeM = 0.0; lastRangeUT = 0.0; openingRateMps = 0.0;
        }

        // ============================ THE TICK ============================

        /// <summary>
        /// One conductor tick. Called at physics rate from <see cref="FlightDriver"/>, ONLY while
        /// `CrewProcedureOps.Engaged`. Defensive throughout — the glue is where bugs live, and a
        /// throw here would take the flight with it.
        /// </summary>
        public static void Tick(Vessel v)
        {
            if (v == null) return;
            Bind(v);
            if (core == null) { note = "no embedded MechJeb core on this vessel"; return; }

            AutoTargetStation(v);

            // The crew's LAUNCH GO (gate G7). ⭐ CONSUMED HERE NOW, NOT IN THE HOST. W10's FlightDriver
            // consumed it and logged that nothing could act on it (§14.4(a), correct at the time); T18
            // is the increment that gives it somewhere to go, so the consumption moves with the caller.
            if (CrewProcedureOps.ConsumeLaunch())
            {
                launchLatched = true;
                Debug.Log("[DragonScreen] LAUNCH GO latched (G7) — the ascent sequence has it.");
            }

            ConductorInputs snap = Snapshot(v);
            ConductorAction a = Conductor.Decide(snap);
            note = a.Reason;

            switch (a.Verb)
            {
                // ⛔ ABORT IS NOT EXECUTED HERE AND MUST NOT BE. The abort executor is register W19
                // (`src/AbortControl.cs`), which is HELD — blocked on `src/Steering.cs`, which §B12.8
                // rider (b) forbids recovering. All this increment can honestly do with an abort
                // decision is stop flying and give the vehicle back, which is what it does. §14.4(a):
                // no light, no action, and NO RED.
                case ConductorVerb.Abort:
                    StandDown(v, "abort decided — control handed back (no abort executor: W19 is HELD)");
                    return;

                case ConductorVerb.Release:
                    StandDown(v, "mission complete — control released to the crew");
                    return;

                // A crew gate is up. The conductor does not fly past a question nobody has answered,
                // and it does not hold the vehicle while it waits: authority goes back until the GO.
                case ConductorVerb.Hold:
                    StandDown(v, "holding at a crew gate");
                    return;

                // ⭐⭐ `Advance` MEANS TWO DIFFERENT THINGS AND THE GLUE HAS TO TELL THEM APART.
                // `Conductor.Decide` returns it BOTH from its top-level `PhaseComplete` check ("the
                // mission plan may move on") AND from inside the phase table, where `PlanThenBurn` and
                // the approach chain use it for "this STEP of the operation chain is finished". They
                // are distinguishable with certainty and without guessing, because the top-level check
                // runs FIRST: if we did not feed `PhaseComplete = true`, the Advance came from the
                // chain. ⛔ Conflating them would advance the mission plan every time one burn ended —
                // a WP0 hold raised from four kilometres out, or a deorbit gate raised in orbit.
                case ConductorVerb.Advance:
                    if (snap.PhaseComplete)
                    {
                        CrewProcedureOps.PhaseComplete();
                        Debug.Log("[DragonScreen] conductor: phase complete — plan advanced ("
                                  + CrewProcedureOps.PhaseName + ")");
                        ResetLeg();
                    }
                    else ChainStepFinished(v, a);
                    return;

                // §B12.4's re-plan: the burn did not achieve its intent, so rebuild the operation.
                case ConductorVerb.Replan:
                    Replan(v, a);
                    return;

                case ConductorVerb.Engage:
                    // ⭐ T21: the RETURN owns its own phases outright. `Conductor.Decide` is still the
                    // safety order above (abort ⟩ complete ⟩ hold ⟩ advance) and its Entry row still
                    // names the Node Executor and SmartASS — but the ORDER of the return's events (back
                    // away, trunk, plan, burn, nose cone, attitude, drogues, mains, splash) is
                    // `ReturnSequence`'s, exactly as the ascent's is `AscentSequence`'s.
                    if (OnReturnLeg(snap.Phase)) { RunReturn(v, a, snap.Phase); return; }
                    Engage(v, a);
                    return;

                // ⛔ `Idle` IS NOT "DO NOTHING" ON THE RETURN, AND THIS IS THE CASE THAT SAYS SO.
                // `pure/Conductor.cs` returns Idle for `Drogues`/`Mains` — "under chutes, the conductor
                // flies nothing here" — which is true about MECHJEB and not about the vehicle: §B12.3's
                // own Drogues/Mains row is "chute triggering", which is an actuation, not a module. So
                // the return sequencer keeps running through the Idle decision, exactly as
                // `AscentSequence` runs alongside `Engage AscentPvg`.
                case ConductorVerb.Idle:
                    if (OnReturnLeg(snap.Phase)) { RunReturn(v, a, snap.Phase); return; }
                    StandDown(v, a.Reason);
                    return;

                default:
                    StandDown(v, a.Reason);
                    return;
            }
        }

        // ============================ THE DECISION SNAPSHOT ============================

        /// <summary>
        /// Fill the pure core's inputs from the live conductor and the live vessel. ⛔ READS ONLY.
        /// Every §B12.4 threshold comes from `pure/RendezvousOps.cs`, which cites §B10.1/§B11 for each
        /// one; nothing is typed here.
        /// </summary>
        static ConductorInputs Snapshot(Vessel v)
        {
            ConductorInputs s = new ConductorInputs();
            s.Phase    = CrewProcedureOps.ActivePhase;
            s.Holding  = CrewProcedureOps.CrewActionNeeded();
            s.Complete = CrewProcedureOps.PlanComplete;
            s.Aborted  = CrewProcedureOps.AbortActive;

            // Which leg, from the gate this step walks toward (`NextGateId` exists for exactly this).
            GateId nextGate = CrewProcedureOps.NextGateId;
            leg = OnOrbit(s.Phase) ? RendezvousOps.LegFor(nextGate) : RendezvousLeg.None;
            // ⭐ AND WHICH OF THE TWO `Fly(Docked)` STEPS — the phase enum cannot tell them apart, and
            // engaging the Docking Autopilot on the berthed one would try to re-dock a hard-mated
            // vehicle. See `pure/DockingLadder.cs`'s header for the two plan sections this reconciles.
            dockLeg = s.Phase == MissionPhase.Docked ? DockingLadder.LegFor(nextGate) : DockingLeg.None;

            // ---- the measured rendezvous state ----
            double rangeM = 0.0, relSpeedMps = 0.0;
            Measure(v, out rangeM, out relSpeedMps);
            TrackOpeningRate(rangeM);

            double target = RendezvousOps.TargetRangeM(leg);
            s.InKeepOutSphere = RendezvousOps.InsideKeepOutSphere(rangeM);
            // ⭐ T20: the crew's manual-docking override, an INPUT the core reads and never something
            // the conductor initiates — `pure/Conductor.cs` says so in its own header, and this is the
            // shape that keeps it true.
            s.ManualDockingRequested = manualDockingRequested;
            s.ApproachStepsDone = approachStepsDone;
            // ⛔⛔ `NodeExists` IS A LATCH ON "A NODE WAS BUILT FOR THIS STEP", NOT A LIVE COUNT OF
            // MANEUVER NODES — AND IT HAS TO BE, OR THE CHAIN CANNOT TERMINATE. `Conductor.PlanThenBurn`
            // reads: plan when `!NodeExists`, burn when `!NodeBurned`, otherwise Advance. The Node
            // Executor DELETES the node the instant the burn finishes (`ShouldTerminateStock` calls
            // `node.RemoveSelf()`), so a live count would read false again one tick later and the core
            // would plan the same operation forever, never reaching Advance. Found by
            // `MissionWalkTest`'s closed-loop rendezvous, which failed to terminate until this was a
            // latch. Cleared by `ChainStepFinished`, `Replan` and `ResetLeg` — the three places a step
            // genuinely ends.
            // ⚠ One live read is kept, and only as a CORRECTION: if the node was planned, not yet burned,
            // not being executed and no longer on the flight plan, somebody deleted it by hand — so drop
            // the latch and let the operation be rebuilt rather than waiting for a burn that cannot come.
            if (nodePlanned && !nodeBurned && !nodeExecuting && NodeCount(v) == 0)
            {
                nodePlanned = false;
                LogOnce("node-vanished", "[DragonScreen] conductor: the planned node is gone from the "
                        + "flight plan and no burn was running - rebuilding the operation.");
            }
            s.NodeExists = nodePlanned;
            s.NodeBurned = nodeBurned;

            // ---- §B12.4: `closestApproachErr > εd OR residual > tol OR drift` ----
            s.ClosestApproachTolM = RendezvousOps.ClosestApproachToleranceM(leg);
            s.ClosestApproachErrM = ClosestApproachErrorM(v, rangeM);
            s.NodeResidualTolMps  = RendezvousOps.NodeResidualToleranceFor(leg);
            s.NodeResidualMps     = nodeResidualMps;
            s.Drifting = RendezvousOps.Drifting(openingRateMps, rangeM, target,
                                                RendezvousOps.NulledRelativeSpeedMps);

            // Only a phase this increment actually flies can report itself complete.
            // ⭐ T21: the RETURN Phasing step is the same `MissionPhase.Phasing` as the outbound
            // rendezvous — `CrewProcedureOps.IsReturn` is what tells them apart, and it is set the
            // moment gate G14 clears. Without this the departure leg would be handed to the rendezvous
            // executor and asked to close on a station it is trying to leave.
            bool onReturn = CrewProcedureOps.IsReturn;
            if (onReturn && s.Phase == MissionPhase.Phasing) leg = RendezvousLeg.None;

            s.PhaseComplete =
                (s.Phase == MissionPhase.Ascent && AscentSequence.CanAdvancePlan(ascentStep))
             // T21: the departure leg ends when the trunk is away and the vehicle is clear; the
             // descent leg ends on the splash. ⛔ Neither ends on a timer.
             || (onReturn && s.Phase == MissionPhase.Phasing
                 && ReturnSequence.DepartureComplete(returnStep))
             || ((s.Phase == MissionPhase.Drogues || s.Phase == MissionPhase.Mains
                  || s.Phase == MissionPhase.Splashdown)
                 && ReturnSequence.ReturnComplete(returnStep))
             || (OnOrbit(s.Phase) && RendezvousOps.LegComplete(leg, rangeM, relSpeedMps,
                                                               RendezvousOps.NulledRelativeSpeedMps))
             // ⛔ The CAPTURE leg ends on a MEASURED dock; the BERTHED leg never ends on its own — the
             // crew's UNDOCK press does that, via `CrewProcedureOps.MarkDockedThisMission`.
             || (dockLeg != DockingLeg.None
                 && DockingLadder.LegComplete(dockLeg, DockedSide.Docked(v)));

            approachNote = leg == RendezvousLeg.None ? null
                         : leg.ToString() + " — " + (rangeM / 1000.0).ToString("F2") + " km, "
                           + relSpeedMps.ToString("F2") + " m/s";
            return s;
        }

        /// <summary>The phases the T19 on-orbit executor flies. `Docked` is T20's, the return T21's.</summary>
        static bool OnOrbit(MissionPhase p)
        {
            if (CrewProcedureOps.IsReturn && p == MissionPhase.Phasing) return false;   // T21's
            return p == MissionPhase.Phasing || p == MissionPhase.Coast || p == MissionPhase.Approach;
        }

        /// <summary>
        /// The phases `ReturnSequence` owns. ⚠ `Phasing` appears in BOTH this and <see cref="OnOrbit"/>
        /// and is disambiguated by `CrewProcedureOps.IsReturn` in each — the same flag, read once per
        /// question, so the two can never both claim a step.
        /// </summary>
        static bool OnReturnLeg(MissionPhase p)
        {
            if (p == MissionPhase.Entry || p == MissionPhase.Drogues || p == MissionPhase.Mains
                || p == MissionPhase.Splashdown) return true;
            return CrewProcedureOps.IsReturn && p == MissionPhase.Phasing;
        }

        // ============================ ENGAGE ============================

        static void Engage(Vessel v, ConductorAction a)
        {
            switch (a.Module)
            {
                case ConductorModule.AscentPvg:
                    RunAscent(v);
                    return;

                // ⛔ EVERY OTHER MODULE IS AN HONEST NO-OP, NAMED. `Conductor.Decide` can return these
                // today — its phase table is complete — and the executor for each arrives with its own
                // register line. Standing down (rather than silently doing nothing) means the vehicle
                // is never left under an authority that is not steering it.
                // T19: §B9 Phase 2-3 — the planner composes the operation, the Node Executor flies it,
                // and §B12.4 re-plans when the burn did not achieve its intent.
                // ⭐ S219 JOB 2 — THE RENDEZVOUS FORK. Only the TARGET-RELATIVE ops fork: a
                // circularisation or a deorbit periapsis burn is not a rendezvous and stays on the
                // conductor's own planner whichever driver is selected. `NeedsTarget` is already the
                // predicate that names them, so the fork uses it rather than inventing a second list.
                case ConductorModule.ManeuverPlanner:
                    if (rendezvousMode == RendezvousDrive.MechJebAutopilot && NeedsTarget(a.Op))
                    { RunRendezvousAutopilot(v, a); return; }
                    PlanOperation(v, a);
                    return;
                case ConductorModule.NodeExecutor:
                    if (rendezvousMode == RendezvousDrive.MechJebAutopilot && NeedsTarget(a.Op))
                    { RunRendezvousAutopilot(v, a); return; }
                    BurnNode(v, a);
                    return;
                // T20: §B9 Phase 4 — the Docking Autopilot, the DEFAULT inside the Keep-Out Sphere (O6).
                case ConductorModule.DockingAutopilot:
                    RunDocking(v, a);
                    return;

                // ⭐ THE ONE PLACE THE GLUE READS THE PLAN AND NOT ONLY THE PHASE. §B12.3's table has ONE
                // `Docked` row — "idle/KILL-ROT" — but `ModeManager`'s plan has TWO `Fly(Docked)` steps,
                // and the FIRST of them is §B9 Phase 4's capture, which the same §B12.3 sentence gives to
                // the Docking AP. So a KILL-ROT decision on the CAPTURE leg is redirected, and only
                // there. The discrimination itself is pure and tested (`pure/DockingLadder.cs`).
                case ConductorModule.SmartAss:
                    if (a.Op == ConductorOp.KillRot && DockingLadder.AutopilotFlies(dockLeg))
                    { RunDocking(v, a); return; }
                    if (a.Op == ConductorOp.KillRot) { RunAttitudeHold(v, a); return; }
                    StandDown(v, "no attitude executor for " + a.Op + " yet (T21) — " + a.Reason);
                    return;
                default:
                    StandDown(v, a.Reason);
                    return;
            }
        }

        // ============================ THE ASCENT ============================

        static void RunAscent(Vessel v)
        {
            try
            {
                if (!configured) Configure(v);

                if (!ascentEngaged)
                {
                    // ⭐ PICK THE ASCENT UP WHERE THE VEHICLE ACTUALLY IS. Same rule `CrewProcedureOps.
                    // Engage` applies to the mission plan, one level down: a conductor engaged after
                    // liftoff must not sit at `Idle` waiting for a launch GO that was given ten minutes
                    // ago, because then nothing would ever command MECO. ⛔ `ResumeFrom` can never return
                    // `Ignition`, so this can never re-light an octaweb or touch a clamp.
                    if (ascentStep == AscentStep.Idle && !launchLatched)
                    {
                        bool onPad = v.situation == Vessel.Situations.PRELAUNCH
                                  || v.situation == Vessel.Situations.LANDED
                                  || v.situation == Vessel.Situations.SPLASHED;
                        ModuleEngines s2 = Actuator.FindEngine(v, EngineRole.SecondStage);
                        AscentStep resume = AscentSequence.ResumeFrom(
                            onPad, HasBooster(v), s2 != null, s2 != null && s2.EngineIgnited);
                        if (resume != AscentStep.Idle)
                        {
                            ascentStep = resume; stepStartUT = Now();
                            Debug.Log("[DragonScreen] conductor: ascent RESUMED at " + resume
                                      + " (situation " + v.situation + ") — not restarting the launch.");
                        }
                    }

                    // ⛔⛔ S214 (2026-09-07): **PUMP THE STAGE TABLE, AND DO NOT ENABLE THE MODULE UNTIL
                    // IT CAN BE BUILT FROM.** `MechJebModuleStageStats` never starts a fuel-flow
                    // simulation of its own — only `RequestUpdate()` does — and in stock MechJeb the GUI
                    // pumps that. **T15b suppressed the GUI and nothing replaced the pump**, so on
                    // 2026-09-07 the first `Drive` after enabling saw `VacStats` EMPTY, the glue ball
                    // added zero phases, and `AscentBuilder.Build()` threw on `_phases[0]`. The full
                    // proof, with line numbers, is `pure/PvgPreflight.cs`'s header §1.
                    // ⚠ THE PUMP MUST COME FIRST AND MUST KEEP RUNNING: the simulation is asynchronous,
                    // so the first call only STARTS it. We hold here, ticking it, until it answers.
                    if (!PumpStageStatsAndCheck()) return;

                    // ⭐⭐ S222b — **SET THE OPTIONS, THEN ENGAGE.** The two menu-derived boxes are
                    // written HERE, before `Users.Add`, so the module's very first `Drive` reads them.
                    // The stage table has just been proven non-empty by the pump above, which is
                    // exactly the guard the PSG-settings window's own outer `if` applies.
                    MirrorTheMenus(v);

                    // ⭐ THE ORDER MATTERS. Authority first, then the module: a module enabled on a core
                    // that is not master would have `OnModuleEnabled` run (grabbing the attitude and
                    // thrust user pools) while `Drive` never fires, so it would hold the vehicle's
                    // controls without steering them.
                    core.AuthorizeDrive(true);
                    RefreshGimbalEngineLists(v, "drive authority granted");
                    MuMech.MechJebModuleAscentBaseAutopilot ap = core.Ascent;
                    if (ap != null) ap.Users.Add(Owner);
                    ascentEngaged = true;
                    if (stepStartUT <= 0.0) stepStartUT = Now();
                    Debug.Log("[DragonScreen] conductor: PVG ascent ENGAGED — MechJeb has the vehicle "
                              + "(ascent step " + ascentStep + ")");
                }

                // ⭐ S222b — and re-derived EVERY tick thereafter, because the PSG-settings window
                // recomputes it every frame and the stage table changes as stages burn away.
                MirrorTheMenus(v);

                TickAscentSequence(v);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] MechConductor ascent tick failed: " + e.Message);
            }
        }

        /// <summary>
        /// S214. Tick MechJeb's stage-stats simulation and report whether it has yet produced a table
        /// the PSG builder could make at least one phase from.
        ///
        /// ⛔ **RETURNING FALSE IS A HOLD, NOT A FAILURE.** The fuel-flow simulation is asynchronous;
        /// the honest answer for the first few frames is "not yet". Nothing is lit, nothing is
        /// released, and the caller simply comes back next tick.
        /// ⚠ `RequestUpdate()` is a PUBLIC method on an always-enabled vendored module
        /// (`MechJebModuleStageStats.cs:41` sets `Enabled = true` in its constructor), and calling it is
        /// exactly what MechJeb's own GUI does. **Nothing in `plugin/mech/` is edited** (§B12.1).
        /// </summary>
        static bool PumpStageStatsAndCheck()
        {
            MuMech.MechJebModuleStageStats st = core == null ? null : core.StageStats;
            if (st == null) { note = "the core has no StageStats module"; return false; }

            st.RequestUpdate();

            int n = st.VacStats.Count;
            if (n == 0)
            {
                if (!stageStatsWaitLogged)
                {
                    stageStatsWaitLogged = true;
                    Debug.Log("[DragonScreen] conductor: HOLDING the PVG engage — MechJeb's stage table "
                              + "is still empty (the fuel-flow simulation is asynchronous). Nothing is "
                              + "lit and no clamp is touched while we wait. (S214)");
                }
                return false;
            }

            int[] stages = new int[n];
            double[] dv = new double[n];
            for (int i = 0; i < n; i++) { stages[i] = st.VacStats[i].KSPStage; dv[i] = st.VacStats[i].DeltaV; }

            MuMech.MechJebModuleAscentSettings a = core.AscentSettings;
            int lastStage = a == null ? -1 : a.LastStage.Val;
            double minDv  = a == null ? 0.0 : a.MinDeltaV.Val;

            if (!PvgPreflight.WouldBuildAPhase(stages, dv, lastStage, minDv))
            {
                if (!stageStatsWaitLogged)
                {
                    stageStatsWaitLogged = true;
                    Debug.Log("[DragonScreen] conductor: HOLDING the PVG engage — MechJeb's stage table "
                              + "has " + n + " row(s) but none survives its own filters (LastStage "
                              + lastStage + ", MinDeltaV " + minDv.ToString("F1")
                              + " m/s), so AscentBuilder.Build() would throw on _phases[0]. (S214)");
                }
                return false;
            }

            if (stageStatsWaitLogged)
                Debug.Log("[DragonScreen] conductor: MechJeb's stage table is usable ("
                          + n + " row(s)) — releasing the PVG engage hold. (S214)");
            stageStatsWaitLogged = false;
            return true;
        }

        /// <summary>
        /// S214. Does MechJeb hold a guidance SOLUTION — i.e. will anything raise the throttle?
        ///
        /// ⭐ THIS READS `HandleThrottle`'s OWN PRECONDITION, deliberately and literally:
        /// `MechJebModuleGuidanceController.HandleThrottle` opens `if (Solution == null) return;`
        /// (`:292-296`). Asking the same question the throttle path asks is the only way to be sure the
        /// answer means what we need it to mean. `pure/PvgPreflight.cs`'s header §3 is the argument.
        /// </summary>
        static bool GuidanceHasSolution()
        {
            MuMech.MechJebModuleGuidanceController g = core == null ? null : core.Guidance;
            return g != null && g.Solution != null;
        }

        /// <summary>
        /// ⭐⭐ S219 JOB 2 — **SET THE OPTIONS THE WAY A USER OF THE UI WOULD, THEN ENGAGE.**
        ///
        /// The owner, 2026-09-07, verbatim: *"How can the conductor act like it's a user using mechjebs
        /// UI if it does not know what setting/options to set"* and *"No value may be left as 'whatever
        /// the profile set'"*. Before this line, this method wrote FOUR values and then logged, in as
        /// many words, that every ascent-SHAPING value was *"whatever the loaded profile set"*.
        ///
        /// ⭐ **THE TABLE IS `pure/AscentProfile.cs` AND IT IS THE SPECIFICATION FOR THIS METHOD.**
        /// Every box the vendored ascent menus put on screen has a row there, carrying our decision and
        /// the reason for it; this method writes the rows marked `Write`, and the rows marked
        /// `RuntimeMission` are written where their value becomes known (the destination here, the plane
        /// at the crew's GO). `RoDefault`, `ClassicOnly` and `OwnerQuestion` rows are deliberately NOT
        /// written — which is a decision recorded in the table, not an omission.
        ///
        /// ⛔ **NOTHING HERE IS A TUNE.** The Part-B gate is *"RSS-RO DEFAULT settings as the baseline to
        /// tune from"*, so every value written below is either a MISSION FACT, or something the vendored
        /// source itself calls mandatory, or **RO's own default asserted explicitly** so the flown value
        /// is ours on the record instead of inherited from a persisted cfg. The two places where a
        /// deviation looks warranted are `OwnerQuestion` rows and are asked at the end of the register
        /// line, never decided here (C1.8 / C1.12).
        /// </summary>
        // ============================================================================================
        //  ⭐⭐ S222b, 2026-09-08 — **CONFIGURE WRITES EIGHT BOXES. IT USED TO WRITE 47.**
        // ============================================================================================
        //  The owner, verbatim: *"We need to make sure the chat fully understands how to use mechjeb
        //  correctly and return everything back to default settings … No guesses, no invented methods or
        //  'tuning' truely stock mechjeb methods and settings set for auto accent, auto rendezvous and
        //  auto docking!"* and *"the only change should be the auto stage being our way"*.
        //
        //  ⛔ THE RULE: **if `ApplyRODefaults()` sets it, we do not.** A write survives only if a user
        //  would open the MechJeb UI and TYPE it to fly THIS mission, and only for one of three reasons:
        //     MISSION FACT (where we are going) · UI WORKFLOW (a control this mission needs) ·
        //     the owner's ONE sanctioned deviation (autostage off + our own part activation).
        //  The whole table, one row per box with its reason, is `pure/AscentProfile.cs` — and
        //  `AscentProfileTest` re-derives RO's seed list from the vendored source and FAILS the build if
        //  a fourth exemption ever appears.
        //
        //  ⚠ EVERY DROPPED WRITE WAS RO's OWN NUMBER ALREADY. Nothing here changes what the vehicle
        //  flies except the three things the owner named; what changes is whose value it is on the
        //  record, which is the entire point (T22 tunes from a flight, not from a chat's judgement).
        // ============================================================================================
        static void Configure(Vessel v)
        {
            MuMech.MechJebModuleAscentSettings a = core.AscentSettings;
            if (a == null) { note = "the core has no AscentSettings module"; return; }

            // ---- (1) THE PATH — a UI WORKFLOW write and the OWNER'S ONE DEVIATION -----------------
            // ⛔ `Autostage` goes through the PROPERTY, never the `_autostage` field: the property is
            // what removes the ascent autopilot from `Core.Staging.Users`, and the field alone would
            // leave the StagingController still holding a user and still able to actuate.
            a.AscentType = MuMech.AscentType.PSG;                      // the ascent-path dropdown
            a.Autostage = false;                                       // §B8 — the sanctioned deviation

            // ---- (2) WHAT MECHJEB WOULD OTHERWISE ACTUATE ON OUR VEHICLE --------------------------
            // ⛔ NOT TUNING — the second half of the same deviation. §B12.7: direct part control is
            // ours. `SkipCircularization` stops `DriveCircularizationBurn` PLACING A MANEUVER NODE on
            // exit (it would collide with T19's own node executor); the two auto-deploys stop MechJeb
            // extending real hardware the crew procedure owns. RO seeds none of these three.
            a.SkipCircularization = true;
            a.AutoDeploySolarPanels = false;
            a.AutoDeployAntennas = false;

            // ---- (3) MECHJEB'S OWN "Launch countdown:" BOX ----------------------------------------
            // A UI box RO does not seed, and 32 rather than stock's 11 for a reason that IS the
            // deviation: our T-0 is `IgnitionGate`, which refuses to light a stage with no guidance
            // solution (S214), so PSG must have converged BEFORE our terminal count. It shapes no part
            // of the trajectory. See `AscentProfile.WarpCountDownS` for the two composed numbers.
            a.WarpCountDown.Val = AscentProfile.WarpCountDownS;

            // ---- (4) ⭐ AUTOWARP — ONE FLAG, AND ALL THREE PHASES READ IT --------------------------
            // The owner: *"It must also select auto warp for all modes."* Established from the vendored
            // source rather than assumed: the ascent countdown warps only `if (Core.Node.Autowarp)`
            // (`MechJebModuleAscentBaseAutopilot.cs:132`); the node executor gates both of its warps on
            // the same field (`:242`, `:293`), which is what the rendezvous autopilot flies through; and
            // the rendezvous autopilot NARROWS that same field rather than owning one of its own
            // (`Core.Node.Autowarp = Core.Node.Autowarp && Core.Target.Distance > 1000`). The docking
            // autopilot never warps at all — it is pure RCS from the keep-out sphere inward.
            // ⇒ ONE WARP OWNER PER PHASE: ascent = the ascent autopilot's countdown; rendezvous = the
            //   node executor; docking = nobody, by design.
            // ⚠ `activateSASOnWarp` is §B12.7 again: left on, `SetTimeWarpRate` calls
            //   `ActionGroups.SetGroup(SAS, true)` on the way into warp — an action group on our
            //   vehicle, and SAS fighting MechJeb's own attitude controller besides.
            try
            {
                if (core.Node != null) core.Node.Autowarp = true;
                if (core.Warp != null) core.Warp.activateSASOnWarp = false;
            }
            catch (Exception e)
            { Debug.LogWarning("[DragonScreen] conductor: could not set the autowarp flags: " + e.Message); }

            // ---- (5) ⛔⛔ EVERYTHING THAT USED TO BE HERE IS GONE, AND IT IS ALL RO's ---------------
            // Dropped by S222b, every one of them a value `ApplyRODefaults()` already seeds to exactly
            // what we were writing: PitchStartHeight · PitchRate (never written; Q1) · LimitQa ·
            // LimitQaEnabled (now UI-derived, below) · DesiredFPA · DesiredArgP/Flag · MinDeltaV ·
            // MaxCoast · MinCoast · CoastStageFlag/Internal · SpinupStageFlag/Internal ·
            // UnguidedStagesFlag · FixedStagesFlag · PreStageTime · OptimizerPauseTime ·
            // LaunchLANDifference · AttachAltFlag · DesiredAttachAlt · DesiredAttachAltFixed · and the
            // whole `Core.Thrust` block (nine fields, including Q2's `LimitDynamicPressure`).
            // Also dropped: ForceRoll · VerticalRoll · TurnRoll · RollAltitude · LastStage ·
            // CoastLocation · Cd · Aref · RelativeLAN · OverrideWarpToPlane · LaunchingToMatchLan ·
            // LaunchingToLan — field defaults identical to what we wrote, or non-persisted flags that
            // cannot be stale in the first place.
            // ⛔ AND `OptimizeStageFlag = false` IS GONE, WHICH IS THE ONE THAT MATTERED. It is not a
            // setting at all — the PSG-settings window recomputes it every frame — and writing `false`
            // is what made RO's 110 km attach altitude a live terminal constraint on a 215 km orbit.
            // The full unwind is `pure/AscentProfile.AttachAltFollowsMissionApsis`. It is now derived
            // from the live stage table by `MirrorTheMenus`, below.

            // ---- (6) THE DESTINATION — §B5's named exception, a MISSION FACT ----------------------
            AscentTarget t = AscentTargets.For(CrewProcedureOps.Profile, a.DesiredInclination.Val);

            // ⛔ S214: **NEVER HAND THE SOLVER AN INCLINATION IT CANNOT TAKE.** The domain is the
            // vendored source's own — FINITE, |inc| ≤ 180 — and the SIGN is deliberately allowed
            // through (`pure/PvgPreflight.cs` §2 proves all seven terminals `Abs()` it, so the
            // 2026-09-07 `-51.6°` was innocent). This guard is not what fixed that flight; it is here so
            // that if a profile ever DOES carry a poisoned value, we refuse it here with a name attached
            // rather than letting it surface as an anonymous solver exception 145 ms later.
            if (!PvgPreflight.InclinationInDomain(t.InclinationDeg))
            {
                note = "target inclination out of domain";
                Debug.LogError("[DragonScreen] conductor: REFUSING to configure PVG — target inclination "
                               + t.InclinationDeg + "° is outside the solver's domain (finite, |inc| ≤ "
                               + PvgPreflight.MaxInclinationDeg + "°). Nothing was written to MechJeb "
                               + "and the ascent will not engage. (S214)");
                return;
            }

            a.DesiredOrbitAltitude.Val = t.PeriapsisM;
            a.DesiredApoapsis.Val      = t.ApoapsisM;

            // ⭐⭐ THE INCLINATION IS WRITTEN ON EXACTLY ONE OF THE TWO PATHS, AND THE UI IS WHY.
            // Owner, 2026-09-08: *"⛔ DO NOT WRITE THE INCLINATION. `LaunchingToPlane` overrides it from
            // the target (§7.5) and did so correctly last flight — −51.6316°, matching the ISS's own
            // 51.6316°. Writing it fights the feature."* Exactly so — on a RENDEZVOUS mission the box is
            // filled by pressing "Launch into plane of target", and `UpdateLaunchWindow` reproduces that
            // press (§7.5, `MechJebModuleAscentMenu.cs:245-258`), writing `DesiredInclination` LAST,
            // after `StartCountdown`, from `MinimumTimeToPlane`'s own signed answer.
            // ⚠ A FREE-FLYER HAS NO PLANE TO LAUNCH INTO AND NO BUTTON TO PRESS. `WindowRequired()` is
            // false for Inspiration4 / Polaris Dawn / Fram2, so nothing downstream would ever write the
            // box — and RO's own seed leaves `DesiredInclination` at its field default 0.0, i.e. a
            // 28.6°-pad EQUATORIAL ascent (`pure/AscentSequence.cs`, the AscentTargets block). There the
            // mission fact IS what a user types into "Orbit inc.", so we type it.
            if (!WindowRequired())
            {
                a.DesiredInclination.Val = t.InclinationDeg;
                Debug.Log("[DragonScreen] conductor: no rendezvous on this profile — the mission "
                          + "inclination " + t.InclinationDeg.ToString("F4") + "° is written into the "
                          + "'Orbit inc.' box, because no plane launch will fill it. (S222b)");
            }

            configured = true;
            Debug.Log("[DragonScreen] conductor: PVG configured — autostage OFF (§B8), AscentType PSG, "
                      + "target " + (t.PeriapsisM / 1000.0).ToString("F0") + " x "
                      + (t.ApoapsisM / 1000.0).ToString("F0") + " km "
                      + (t.FromProfileApsides ? "(mission apsides)" : "(standard ISS insertion)")
                      + ", inclination "
                      + (WindowRequired() ? "LEFT TO §7.5's plane launch (S222b)"
                                          : "written " + t.InclinationDeg.ToString("F4") + "° (no rendezvous)")
                      + ", attach altitude LEFT AT RO's 110 km — unread, because OptimizeStageFlag is "
                      + "UI-derived and AttachAltFlag is RO's false, so MechJebLib forces attach = "
                      + "periapsis for a circular target (S222b). Countdown " + AscentProfile.WarpCountDownS
                      + " s, autowarp ON. " + AscentProfile.Render());
        }

        // ============================================================================================
        //  ⭐⭐ S222b — **THE TWO BOXES MECHJEB'S OWN MENUS WRITE, AND WE SUPPRESSED THE MENUS.**
        // ============================================================================================
        //  T15b stops MechJeb drawing its windows. Two ascent settings are written by those windows'
        //  DRAW code rather than by a user, so with the windows gone they would sit at an at-rest value
        //  no user of MechJeb is ever in. This is the conductor standing in for the window — the menus'
        //  own expressions, re-evaluated every tick exactly as `WindowGUI` does, not values we picked.
        //
        //  ⛔ THIS IS THE FIX FOR THE ATTACH-ALTITUDE DEFECT S219 COMPENSATED FOR RATHER THAN FOUND.
        //  `MechJebModuleAscentPSGSettingsMenu.cs:63,83` recomputes `OptimizeStageFlag` from the live
        //  stage list, and `ApplyRODefaults()` OPENS that window for RO users — so a real RO user's flag
        //  is continuously TRUE. Ours read FALSE because S219 wrote FALSE, and false is precisely the
        //  state in which `SetTarget:101,109` forces `attachAltFlag` on and reads RO's 110 km
        //  `DesiredAttachAltFixed` as a hard terminal constraint against a 215 km orbit. With the flag
        //  derived, `AttachAltFlag` stays RO's false and `AscentBuilder.Build():146-149` sets the attach
        //  radius to the periapsis itself for a near-circular target. Full argument:
        //  `pure/AscentProfile.AttachAltFollowsMissionApsis`.
        //
        //  ⚠ CALLED TWICE ON PURPOSE — once BEFORE the engage (so the module's very first `Drive` sees
        //  the derived values, "set the options, THEN engage") and once per tick thereafter (because
        //  the stage table changes as stages burn away, and so does the menu's answer).
        // ============================================================================================
        static void MirrorTheMenus(Vessel v)
        {
            try
            {
                MuMech.MechJebModuleAscentSettings a = core == null ? null : core.AscentSettings;
                if (a == null) return;

                // (a) `MechJebModuleAscentMenu.cs:374`, asserted every frame the ascent window draws:
                //     `_ascentSettings.LimitQaEnabled = _ascentSettings.AscentType == AscentType.PSG;`
                //     with the source's own comment `// this is mandatory for PSG`. The EXPRESSION is
                //     copied, not its current answer.
                a.LimitQaEnabled = a.AscentType == MuMech.AscentType.PSG;

                // (b) `MechJebModuleAscentPSGSettingsMenu.cs:57-85`. The loop is pure and lives in
                //     `AscentProfile.OptimizeStageFlagFor`; this only projects MechJeb's `FuelStats`
                //     onto it. ⛔ When the menu's own outer guard does not hold — an empty stage table —
                //     the menu writes NOTHING, and neither do we: a momentarily-empty `VacStats` must
                //     not clear a flag the last good frame derived.
                MuMech.MechJebModuleStageStats st = core.StageStats;
                if (st == null || st.VacStats == null || st.VacStats.Count == 0) return;

                var stages = new AscentProfile.PsgStage[st.VacStats.Count];
                for (int i = 0; i < st.VacStats.Count; i++)
                {
                    stages[i].KspStage  = st.VacStats[i].KSPStage;
                    stages[i].DeltaVMps = st.VacStats[i].DeltaV;
                    stages[i].Fixed     = a.FixedStages != null
                                          && a.FixedStages.Contains(st.VacStats[i].KSPStage);
                }

                if (!AscentProfile.OptimizeStageFlagApplies(stages, a.LastStage.Val)) return;

                bool want = AscentProfile.OptimizeStageFlagFor(stages, a.LastStage.Val, a.MinDeltaV.Val);
                if (a.OptimizeStageFlag == want) return;

                a.OptimizeStageFlag = want;
                Debug.Log("[DragonScreen] conductor: OptimizeStageFlag -> " + want
                          + " — derived from " + stages.Length + " stage(s) exactly as the PSG settings "
                          + "window does (MechJebModuleAscentPSGSettingsMenu.cs:63,83), because T15b "
                          + "suppressed the window that would otherwise write it. (S222b)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor: could not mirror the ascent menus' own "
                                 + "derived settings: " + e.Message);
            }
        }


        // ============================ S215 — THE LAUNCH WINDOW ============================

        /// <summary>
        /// ⭐ How early to leave time warp, seconds before T-0. **COMPOSED FROM TWO IN-REPO NUMBERS,
        /// not chosen:**
        ///   • **20 s** — `plugin/mech/MechJebKos/AscentPSGBinding.cs:113-115`, the vendored tree's own
        ///     note: *"PSG can take ~20s to converge an initial solution from a cold start. Staging
        ///     before there is a solution drops the rocket on the pad, so a launch script should wait on
        ///     HASSOLUTION … before releasing the clamps."* That is precisely S214's `GuidanceReady`
        ///     hold, which sits between the crew's GO and ignition — so the countdown must LEAVE ROOM
        ///     for it, or the solver would still be converging when the window arrived.
        ///   • **12 s** — `pure/WarpPlan.BurnLeadS`, this repo's existing "be at 1× this long before an
        ///     event" margin, which exists for the warp-exit transition itself.
        /// ⚠ Both halves are MARGINS, not physics, and neither is converged (`WarpPlan`'s own header
        /// says so of its four). Safe in one direction only: larger = more real-time seconds on the pad.
        /// ⛔ It is deliberately NOT MechJeb's `AscentSettings.WarpCountDown` (11 s). Stock can afford
        /// that because stock starts guidance DURING its countdown — `MechJebModuleAscentPSGAutopilot.cs:47-52`
        /// calls `SetTarget` + `AssertStart(false)` once `TMinus &lt;= WarpCountDown` — and we do not arm
        /// MechJeb's countdown at all (Q1), so nothing would ever trigger that for us.
        /// </summary>
        const double WarpLeadSeconds = 20.0 + 12.0;

        /// <summary>
        /// Solve the launch window from live state, and CHECK the answer against the pure mirror.
        ///
        /// ⭐ **THE FLOWN NUMBER IS THE VENDORED ONE.** `MechAstro.MinimumTimeToPlane` is what
        /// `MechJebKos/AscentBindingBase.cs:120-126` calls, and it is what this returns. `LaunchWindow`
        /// — the pure, headless-tested, mutation-proven mirror — is computed from the SAME inputs and
        /// compared. They are the same double arithmetic, so they must agree to `MirrorToleranceS`;
        /// **if they do not, the mirror has drifted from the pin and the launch HOLDS with both numbers
        /// printed**, rather than the mirror being quietly right on the bench and wrong in flight.
        ///
        /// ⛔ **THE NAV LAYER MUST BE CURRENT, AND ON THIS VEHICLE IT IS NOT ALWAYS.** `VesselState` is
        /// refreshed inside `MechJebCore.FixedUpdate`, which returns early unless this core is
        /// `GetMasterMechJeb()` — and that resolves by `p.running`, which `MechHost.HoldDriveAuthority`
        /// pins to `DriveAuthorized`. So before the conductor grants drive authority the latitude and
        /// celestial longitude below are STALE (zero on a fresh scene), and a window solved from them
        /// would be confident nonsense. The `VesselState.Time` check is that staleness MEASURED, rather
        /// than assumed away by an ordering argument that a later edit could quietly invalidate.
        /// </summary>
        static WindowPlan SolveWindow(Vessel v)
        {
            WindowPlan bad = new WindowPlan();
            bad.Verdict = WindowVerdict.NotComputable;

            MuMech.VesselState vs = core.VesselState;
            if (vs == null) { bad.Reason = "no VesselState on the core"; return bad; }

            double now = Now();
            double age = vs.Time - now; if (age < 0.0) age = -age;
            if (vs.Time <= 0.0 || age > 1.0)
            {
                bad.Reason = "MechJeb's nav state is not current (VesselState.Time is "
                           + age.ToString("F1") + " s off) — no drive authority yet";
                return bad;
            }

            Orbit to = CrewProcedureOps.SameSoiTargetOrbit(v);
            CelestialBody b = v.mainBody;

            WindowInputs wi = new WindowInputs();
            wi.TargetInSameSoi        = to != null && b != null;
            wi.BodyRotationPeriodS    = b != null ? b.rotationPeriod : 0.0;
            wi.LatitudeDeg            = vs.Latitude;
            wi.CelestialLongitudeDeg  = vs.CelestialLongitude;
            wi.TargetLanDeg           = to != null ? to.LAN : 0.0;
            wi.TargetInclinationDeg   = to != null ? to.inclination : 0.0;
            wi.LanDifferenceDeg       = core.AscentSettings != null
                                      ? core.AscentSettings.LaunchLANDifference.Val : 0.0;
            wi.NowUT                  = now;
            wi.MissionInclinationDeg  = AscentTargets.For(CrewProcedureOps.Profile, 0.0).InclinationDeg;

            WindowPlan p = LaunchWindow.Solve(wi);
            if (!p.Armed) return p;   // NoTarget / disagreement / not computable — it already said why.

            // ⭐ THE CROSS-CHECK. Same inputs, vendored implementation, and the answer we actually fly.
            var vendored = MechAstro.MinimumTimeToPlane(wi.BodyRotationPeriodS, wi.LatitudeDeg,
                                                        wi.CelestialLongitudeDeg,
                                                        wi.TargetLanDeg - wi.LanDifferenceDeg,
                                                        wi.TargetInclinationDeg);
            double vTime = vendored.time, vInc = vendored.inclination;

            if (!LaunchWindow.MirrorAgrees(p.TimeToWindowS, vTime, LaunchWindow.MirrorToleranceS))
            {
                WindowPlan drift = new WindowPlan();
                drift.Verdict = WindowVerdict.NotComputable;
                drift.Reason = "the pure launch-window mirror DISAGREES with the vendored Astro — mirror "
                             + p.TimeToWindowS.ToString("F6") + " s vs vendored " + vTime.ToString("F6")
                             + " s. `pure/LaunchWindow.cs` has drifted from the pin (S215)";
                return drift;
            }

            // Fly the VENDORED numbers, now that the mirror has vouched for them.
            p.TimeToWindowS = vTime;
            p.InclinationDeg = vInc;
            p.LaunchUT = now + vTime;
            return p;
        }

        /// <summary>
        /// S215. Keep the window solution fresh before the crew's GO, and LATCH it at the GO.
        ///
        /// ⭐ **Q3 — "GO ARMS THE COUNTDOWN, THEN IT WARPS", and the latch is what makes that true.**
        /// Before the GO this re-solves every tick: the planet is turning, the target is moving, and the
        /// crew may still change either. At the GO the answer is FROZEN, the plane is written into
        /// MechJeb, and everything after it is execution — which is the owner's own words for what a
        /// crew GO must mean.
        /// ⛔ **AND THE LATCH IS NOT MERELY TIDY — RE-SOLVING PAST T-0 WOULD JUMP A WHOLE REVOLUTION.**
        /// `TimeToPlaneS` ends in `Clamp2Pi`, so the instant the site passes the plane the answer wraps
        /// to the NEXT crossing, hours away. A conductor that re-solved during the final seconds would
        /// watch its own countdown leap from T-1 s to T+11 h on a single overshoot.
        /// </summary>
        static void UpdateLaunchWindow(Vessel v)
        {
            if (launchWindowUT > 0.0) return;   // committed; it is a fixed UT now. `Reset` clears it.

            // ⛔ A FREE-FLYER SOLVES NO WINDOW AT ALL — not even a valid one. Inspiration4, Polaris Dawn
            // and Fram2 carry their own apsides and chase nothing, and a target may still be selected on
            // one for a hundred incidental reasons. Without this line such a mission would arm, commit
            // and then WARP HOURS to a plane crossing it has no interest in — a launch delayed by a
            // feature that was not asked for, which is worse than the feature being absent.
            if (!WindowRequired()) return;

            window = SolveWindow(v);

            if (!launchLatched)
            {
                // Pre-GO: keep it fresh, say so ONCE if it is unflyable, and command nothing.
                if (window.Armed) windowHoldLogged = false;
                else if (!windowHoldLogged && WindowRequired())
                {
                    windowHoldLogged = true;
                    Debug.LogWarning("[DragonScreen] conductor: NO LAUNCH WINDOW — " + window.Reason
                                     + ". The pad holds; nothing is committed. (S215)");
                }
                return;
            }

            if (!window.Armed)
            {
                // ⛔ The GO is latched and the window is NOT flyable. HOLD. `AscentSequence` is told the
                // window is REQUIRED and not armed, and holds at Idle without lighting anything.
                if (!windowHoldLogged)
                {
                    windowHoldLogged = true;
                    Debug.LogError("[DragonScreen] conductor: LAUNCH GO is latched but there is NO FLYABLE "
                                   + "WINDOW — " + window.Reason + ". Holding on the pad: nothing is lit "
                                   + "and no clamp is released. (S215)");
                }
                return;
            }

            // ---- THE COMMIT. One-shot, at the GO. ----
            launchWindowUT = window.LaunchUT;

            MuMech.MechJebModuleAscentSettings a = core.AscentSettings;
            if (a != null)
            {
                // ⚠ S219: THE INCLINATION IS **NOT** WRITTEN HERE ANY MORE. §7.5's sequence
                // (`MechJebModuleAscentMenu.cs:245-258`) writes it LAST, after `StartCountdown`, and
                // following that order literally is the whole of the owner's 2026-09-07 directive. It
                // is written below, once, with Q2's argument attached to it there.
                //
                // ⭐ THE ONE FLAG THAT MAKES THE ASCENT REACH THE TARGET'S **PLANE** AND NOT MERELY ITS
                // INCLINATION. `MechJebModuleAscentPSGAutopilot.SetTarget` (`:103-116`) reads it: with
                // `LaunchingToPlane` set it passes `lanflag = true` and `Core.Target.TargetOrbit.LAN`
                // into the glue ball, so PVG targets the RAAN as well as the inclination. Without it,
                // `docs/MECHJEB_MASTER_MAP.md` §7.5's exact failure — "right inclination, wrong RAAN,
                // and the rendezvous autopilot then needs an unaffordable plane change."
                a.LaunchingToPlane = true;
            }

            // MechJeb's own target controller only syncs inside `OnFixedUpdate`, so set it explicitly
            // rather than race it: `SetTarget` is about to read `Core.Target.TargetOrbit`.
            try
            {
                if (core.Target != null && v.targetObject != null) core.Target.Set(v.targetObject);
            }
            catch (Exception e)
            { Debug.LogWarning("[DragonScreen] conductor: could not sync MechJeb's target: " + e.Message); }

            // ============================================================================
            // ⭐⭐ S219 JOB 2 — **ARM MECHJEB'S OWN COUNTDOWN.** §7.5, step for step.
            // ============================================================================
            // `docs/MECHJEB_MASTER_MAP.md` §7.5 documents the rendezvous button as ONE sequence, and
            // `MechJebModuleAscentMenu.cs:245-258` is that sequence in source:
            //
            //     _launchingToPlane = true;
            //     (timeToPlane, inclination) = Astro.MinimumTimeToPlane(rotationPeriod, lat, lon,
            //                                      TargetOrbit.LAN - LaunchLANDifference,
            //                                      TargetOrbit.inclination);
            //     _autopilot.StartCountdown(VesselState.Time + timeToPlane);
            //     _ascentSettings.DesiredInclination.Val = inclination;
            //
            // All four steps are now here, in that order: the flag above, the solve in `SolveWindow`
            // (which calls the same vendored `Astro.MinimumTimeToPlane` and cross-checks it against the
            // pure mirror), the countdown here, and the inclination — which the vendored order writes
            // LAST, after the countdown, and so do we.
            //
            // ⛔⛔ [[S215]]'s Q1 REFUSED TO CALL THIS, AND WAS OVERTURNED BY THE OWNER, 2026-09-07:
            //     "The overseer put this file in the T18 prompt's READ-FIRST list and then ruled against
            //      StartCountdown without opening §7.5. ⭐ THE MAP IS THE SPECIFICATION."
            // Its stated fear was real and is answered rather than dismissed — see `TickTerminalCount`
            // below, which removes `StageManager.ActivateNextStage()` from reach instead of racing it.
            try
            {
                MuMech.MechJebModuleAscentBaseAutopilot ap = core.Ascent;
                MuMech.VesselState vs = core.VesselState;
                if (ap != null && vs != null)
                {
                    ap.StartCountdown(vs.Time + window.TimeToWindowS);
                    countdownArmed = true;
                }
                else
                {
                    Debug.LogWarning("[DragonScreen] conductor: the ascent autopilot or VesselState was "
                                     + "null at the commit — MechJeb's countdown was NOT armed, so there "
                                     + "is no autowarp. The committed T-0 still stands. (S219)");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor: StartCountdown failed: " + e.Message
                                 + " — no autowarp; the committed T-0 still stands.");
            }

            if (a != null)
            {
                // ⭐ Q2: THE COMPUTED INCLINATION WINS AT LAUNCH — and by here it has already been proven
                // to agree with the §B5 mission fact inside `LaunchWindow.Solve`, or we would not be
                // holding an `Armed` plan. ⭐ THIS ALSO SETTLES T18's Q1 (the inclination SIGN): the sign
                // is the northgoing/southgoing choice `MinimumTimeToPlane` just made on TIMING grounds,
                // not a value inherited from whichever cfg happened to be loaded.
                // ⚠ WRITTEN AFTER `StartCountdown`, because that is the order the menu writes them in.
                a.DesiredInclination.Val = window.InclinationDeg;
            }

            Debug.Log("[DragonScreen] conductor: LAUNCH WINDOW COMMITTED — T-0 in "
                      + window.TimeToWindowS.ToString("F1") + " s, plane "
                      + window.InclinationDeg.ToString("F4") + "° (LAN-targeted: LaunchingToPlane ON). "
                      + (countdownArmed
                            ? "MechJeb's own countdown is ARMED (§7.5) — it warps to T-"
                              + AscentProfile.WarpCountDownS + " s on Core.Node.Autowarp and starts PSG "
                              + "converging there."
                            : "⛔ MechJeb's countdown could NOT be armed — no autowarp.")
                      + " The conductor takes the terminal count at T-"
                      + AscentProfile.TerminalCountS.ToString("F0")
                      + " s and IgnitionGate keeps T-0 (§B8/§B12.7). (S219)");
        }

        /// <summary>
        /// ⭐⭐ S219 JOB 2/3 — **TAKE THE TERMINAL COUNT, AND WITH IT MECHJEB'S T-0.**
        ///
        /// `MechJebModuleAscentBaseAutopilot.OnFixedUpdate:122-137` runs its whole T-0 block only
        /// `if (TimedLaunch)`, and inside it:
        ///     if (TMinus &lt; 3 * DeltaT || (TMinus &gt; 10.0 &amp;&amp; _lastTMinus &lt; 1.0))
        ///     {
        ///         if (Enabled &amp;&amp; VesselState.ThrustAvailable &lt; 10E-4) StageManager.ActivateNextStage();
        ///         TimedLaunch = false;
        ///     }
        /// ⛔ **THAT `ActivateNextStage()` IS NOT HARMLESS ON THIS CRAFT.** `docs/reference/Crew-2.craft`
        /// puts the octaweb alone in KSP stage 8 and **the Ghidorah erector — the hold-downs — alone in
        /// stage 7**. And the case where it fires is precisely our worst one: `IgnitionGate` safing the
        /// pad at T-1 s leaves `ThrustAvailable` at ZERO, so MechJeb would re-light the octaweb we just
        /// shut down, outside the gate that shut it.
        ///
        /// ⭐ **SO THE BRANCH IS REMOVED FROM REACH, NOT RACED.** `TimedLaunch` is a public field and
        /// MechJeb's own ascent window clears it from its **Abort** button
        /// (`MechJebModuleAscentMenu.cs:305`) — so this is a documented UI action, not a patch
        /// (`plugin/mech/` untouched, §B12.1). From T-<see cref="AscentProfile.TerminalCountS"/> onward
        /// `TimedLaunch` is false, MechJeb has no T-0 of any kind, and `IgnitionGate` owns the pad
        /// exactly as §B8/§B12.7 require.
        ///
        /// ⚠ AND IT IS LATE ENOUGH TO COST NOTHING. By T-10 s the autowarp has already landed (it ends
        /// at T-32 s) and PSG has already been handed the target and told to converge
        /// (`MechJebModuleAscentPSGAutopilot.Drive:47-53`, at `TMinus &lt;= WarpCountDown`). Clearing the
        /// flag then simply moves `Drive` onto its `else` branch, which is the SAME `SetTarget()` plus a
        /// full `AssertStart()` — guidance keeps converging, and `DriveAscent` stops answering "Awaiting
        /// liftoff" and starts flying the vertical ascent the moment the vehicle actually moves.
        /// </summary>
        static void TickTerminalCount()
        {
            if (!countdownArmed || terminalCountTaken || launchWindowUT <= 0.0) return;
            if (SecondsToWindow() > AscentProfile.TerminalCountS) return;

            try
            {
                MuMech.MechJebModuleAscentBaseAutopilot ap = core.Ascent;
                if (ap == null) { terminalCountTaken = true; return; }
                ap.TimedLaunch = false;
                terminalCountTaken = true;
                Debug.Log("[DragonScreen] conductor: TERMINAL COUNT — the conductor has T-0. MechJeb's "
                          + "TimedLaunch is CLEARED at T-" + SecondsToWindow().ToString("F1")
                          + " s, so `StageManager.ActivateNextStage()` is now unreachable and IgnitionGate "
                          + "owns the pad. Guidance keeps converging on the untimed path. (S219)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor: could not clear TimedLaunch: " + e.Message
                                 + " — ⛔ MechJeb may still stage at T-0.");
            }
        }

        /// <summary>
        /// ⭐ S215 — **THE COUNTDOWN, ON THE GLASS.** Null except while a launch window is committed and
        /// T-0 has not arrived; the screens prefer it over the mission plan's step label
        /// (`src/VesselData.cs`, `state.AutoPhase`).
        ///
        /// ⛔ IT EXISTS BECAUSE THE ALTERNATIVE IS A LIE OF EXACTLY THE KIND W10 REMOVED. Once G7
        /// clears, the plan is standing on its Ascent step, so `CrewProcedureOps.PhaseName` says
        /// "Ascent to orbit" — over a vehicle still bolted to the pad, possibly for hours of warp.
        /// W10's own change (3) rejected precisely that sentence ("'AUTO  Ascent to orbit' on the pad
        /// is the same lie"), and the owner's report on the S213 flight — *"You can hear something
        /// activate but we sit on the pad doing nothing"* — is what an unexplained pad hold reads like
        /// from the seat. A counting clock is the difference between waiting and being stuck.
        /// </summary>
        public static string CountdownNote
        {
            get
            {
                if (launchWindowUT <= 0.0) return null;
                double t = SecondsToWindow();
                if (t <= 0.0) return null;                    // T-0 has passed; the ascent speaks for itself
                int total = (int)(t + 0.5);
                int hh = total / 3600, mm = (total / 60) % 60, ss = total % 60;
                string clock = (hh > 0 ? hh.ToString() + ":" : "")
                             + (hh > 0 ? mm.ToString("00") : mm.ToString()) + ":" + ss.ToString("00");
                return "T-" + clock + " to launch window";
            }
        }

        /// <summary>Does this mission have a plane to launch into at all? A free-flyer does not (Q4).</summary>
        static bool WindowRequired() { return CrewProcedureOps.Profile.HasRendezvous; }

        /// <summary>Seconds to the committed T-0. Meaningless unless <see cref="launchWindowUT"/> is set.</summary>
        static double SecondsToWindow() { return launchWindowUT - Now(); }

        // =====================================================================================
        // ⛔ SUPERSEDED IN PLACE — 2026-09-07, register S219 JOB 2 (C1.16 / G12: reasoning is never
        // deleted, and neither is the code a rule was learned on).
        // =====================================================================================
        // WHAT IT CLAIMED. `WarpLeadSeconds` + `TickLaunchWarp` were [[S215]]'s OWN warp controller:
        // having declined to arm MechJeb's countdown (its Q1), it had to warp to the launch window
        // itself, so it commanded `Core.Warp.WarpToUT` once at the commit and let the warp controller
        // sustain and end it.
        //
        // WHAT REPLACED IT, AND WHY. The owner, 2026-09-07, verbatim: *"S215 built a custom launch-window
        // calculator instead. ⭐ THE MAP IS THE SPECIFICATION. Follow it section by section. Do not
        // re-derive it, and do not invent a parallel mechanism again."* MechJeb's own countdown now warps
        // — `MechJebModuleAscentBaseAutopilot.cs:133`, `Core.Warp.WarpToUT(_launchTime - WarpCountDown)`,
        // re-issued every fixed update while `TimedLaunch` and gated on `Core.Node.Autowarp`, which
        // `Configure` now sets. `AscentProfile.WarpCountDownS` carries this block's composed 20 s + 12 s
        // lead forward into MechJeb's own `WarpCountDown` box, so the REASONING survives its mechanism.
        //
        // ⚠ NOTHING CALLS `TickLaunchWarp` ANY MORE. It is kept, unreferenced, because C1.16's
        // extension says reasoning is marked superseded in place and not removed — and because the two
        // paragraphs below it (why `WarpToUT` is issued once, why `activateSASOnWarp` must be off) are
        // both still TRUE and both still load-bearing: `Configure` step (6) writes that SAS flag for
        // exactly the reason recorded here.
        // =====================================================================================
        /// <summary>
        /// S215. Warp to the committed window, and stop warping in time to fly it.
        ///
        /// ⛔ **`WarpToUT` IS ISSUED ONCE, NOT EVERY TICK.** `MechJebModuleWarpController.OnFixedUpdate`
        /// re-issues its own `warpToUT` each frame and clears it once the UT passes
        /// (`MechJebModuleWarpController.cs:76-78, 122-127`), so the module sustains and ends the warp
        /// itself. Commanding it again every tick would fight that.
        /// ⚠ `activateSASOnWarp` is turned OFF first. Left on, `SetTimeWarpRate` calls
        /// `Part.vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true)` on the way into warp — an
        /// ACTION GROUP on our vehicle, which §B12.7 rules out, and SAS fighting the attitude controller
        /// besides. It is a settings FIELD, not vendored logic: writing it is allowed exactly as writing
        /// `Autostage` is; patching the module would not be (§B12.1).
        /// </summary>
        static void TickLaunchWarp()
        {
            if (launchWindowUT <= 0.0 || warpArmed) return;
            double toGo = SecondsToWindow();
            if (toGo <= WarpLeadSeconds) { warpArmed = true; return; }   // already too close to warp

            try
            {
                MuMech.MechJebModuleWarpController w = core.Warp;
                if (w == null) { warpArmed = true; return; }
                w.activateSASOnWarp = false;
                w.WarpToUT(launchWindowUT - WarpLeadSeconds);
                warpArmed = true;
                Debug.Log("[DragonScreen] conductor: AUTO-WARP armed to T-" + WarpLeadSeconds.ToString("F0")
                          + " s (" + (toGo / 60.0).ToString("F1") + " min of warp). The warp controller "
                          + "sustains and ends it itself. (S215)");
            }
            catch (Exception e)
            {
                warpArmed = true;
                Debug.LogWarning("[DragonScreen] conductor: auto-warp to the launch window failed: "
                                 + e.Message + " — the countdown still runs, at 1x.");
            }
        }

        /// <summary>
        /// ⛔ S215. If the crew deselect the target after the plane was committed, CLEAR
        /// `LaunchingToPlane` — because `MechJebModuleAscentPSGAutopilot.SetTarget:104-105` dereferences
        /// `Core.Target.TargetOrbit.LAN` unconditionally when that flag is set, and a null there is a
        /// `NullReferenceException` inside `Drive`, on every frame of the ascent. The already-written
        /// `DesiredInclination` is KEPT: it is the plane we committed to and it is still the best answer
        /// available; only the live LAN lookup goes away.
        /// </summary>
        static void HoldPlaneTargetOrClear()
        {
            try
            {
                MuMech.MechJebModuleAscentSettings a = core.AscentSettings;
                if (a == null || !a.LaunchingToPlane) return;
                if (core.Target != null && core.Target.NormalTargetExists
                    && core.Target.TargetOrbit != null) return;

                a.LaunchingToPlane = false;
                Debug.LogWarning("[DragonScreen] conductor: the target was LOST after the plane was "
                                 + "committed — LaunchingToPlane cleared so PVG does not dereference a "
                                 + "null target orbit every frame. The committed inclination "
                                 + a.DesiredInclination.Val.ToString("F4") + "° still stands, but the "
                                 + "RAAN is no longer being targeted. (S215)");
            }
            catch { }
        }

        static void TickAscentSequence(Vessel v)
        {
            double now = Now();

            // ⭐ S215, IN THIS ORDER AND FOR A REASON. The window is solved/committed FIRST, because the
            // commit is what writes the plane into MechJeb and fixes T-0; the warp is armed off that
            // commit; and the plane target is re-checked every tick because losing it would fault PVG's
            // own `SetTarget` on the next frame.
            // ⭐ S219: the window is solved/committed FIRST (the commit is what writes the plane into
            // MechJeb, fixes T-0 and ARMS MECHJEB'S OWN COUNTDOWN, which is what warps); the terminal
            // count is taken off that commit; and the plane target is re-checked every tick because
            // losing it would fault PVG's own `SetTarget` on the next frame.
            // ⛔ `TickLaunchWarp` is GONE FROM THIS ORDER and superseded in place below — MechJeb's
            // countdown owns the warp now (§7.5).
            UpdateLaunchWindow(v);
            TickTerminalCount();
            HoldPlaneTargetOrClear();

            AscentInputs s = AscentInputs.Nominal();
            s.LaunchCommanded = launchLatched;
            s.SinceStepS = now - stepStartUT;

            // S215: the countdown. ⛔ `WindowRequired` WITHOUT `WindowArmed` is a HOLD, not a launch —
            // that pairing is what stops a rendezvous mission whose window failed to solve from simply
            // lighting the stage as though no window had ever been wanted.
            s.WindowRequired    = WindowRequired();
            s.WindowArmed       = launchWindowUT > 0.0;
            s.SecondsToWindowS  = s.WindowArmed ? SecondsToWindow() : 0.0;

            double thrust, max; int lit;
            Actuator.EngineThrust(v, EngineRole.OctawebAll, out thrust, out max, out lit);
            s.S1ThrustN = thrust; s.S1MaxThrustN = max; s.S1LitCount = lit;
            s.S1PropellantFrac = BoosterPropellantFraction(v);
            s.S1Flameout = BoosterFlamedOut(v);

            Actuator.EngineThrust(v, EngineRole.SecondStage, out thrust, out max, out lit);
            s.S2ThrustN = thrust; s.S2LitCount = lit;

            // S214: the octaweb is not lit until somebody will raise the throttle. See
            // `pure/PvgPreflight.cs` §3 — with no solution, NOBODY owns the throttle in the ignition
            // window, and the stage lights into a commanded zero.
            s.GuidanceReady = GuidanceHasSolution();
            s.PvgFinished = PvgFinished();
            Orbit o = v.orbit;
            if (o != null)
            {
                s.PeriapsisM = o.PeA;
                CelestialBody b = v.mainBody;
                double atm = (b != null && b.atmosphere) ? b.atmosphereDepth : 0.0;
                s.OrbitClosed = o.PeA > atm;
            }

            AscentDecision d = AscentSequence.Step(s, ascentStep);

            if (d.Act != AscentAct.None) Perform(v, d.Act);

            if (d.Next != ascentStep)
            {
                Debug.Log("[DragonScreen] ASCENT " + ascentStep + " -> " + d.Next + "  (" + d.Reason
                          + ")  [S1 " + (s.S1ThrustN / 1000.0).ToString("F0") + "/"
                          + (s.S1MaxThrustN / 1000.0).ToString("F0") + " kN, " + s.S1LitCount + " lit, prop "
                          + (s.S1PropellantFrac * 100.0).ToString("F1") + "%]");
                ascentStep = d.Next;
                stepStartUT = now;
                note = d.Reason;
            }
        }

        /// <summary>
        /// §B12.7's direct part control — every one of these reaches a live PART MODULE through the
        /// recovered <c>Actuator</c>. ⛔ No `StageManager.ActivateNextStage`, no `KSPActionGroup`.
        /// </summary>
        static void Perform(Vessel v, AscentAct act)
        {
            switch (act)
            {
                case AscentAct.IgniteStageOne:   Actuator.IgniteOctawebLiftoff(v); break;
                case AscentAct.ReleaseHoldDowns: Actuator.ReleaseHoldDowns(v);     break;
                case AscentAct.ShutdownStageOne: Actuator.ShutdownBoosterEngines(v); break;
                case AscentAct.SeparateBooster:  Actuator.SeparateBooster(v);      break;
                case AscentAct.IgniteStageTwo:   Actuator.IgniteSecondStage(v);    break;
                case AscentAct.SeparateDragon:   Actuator.SeparateDragon(v);       break;
                case AscentAct.OpenNoseCone:     Actuator.OpenNoseShroud(v);       break;

                // ⛔ THE ONE THAT MUST NEVER RELEASE A CLAMP. A pad that failed to make thrust is shut
                // down and left bolted down; the crew work the problem. `IgnitionGate` decided this,
                // and it is the reason that gate exists.
                case AscentAct.SafeAbort:
                    Actuator.ShutdownBoosterEngines(v);
                    Debug.LogWarning("[DragonScreen] ⛔ PAD SAFED — ignition did not reach "
                                     + (IgnitionGate.ReleaseThrustFrac * 100.0).ToString("F0")
                                     + "% thrust inside " + IgnitionGate.MaxHoldS
                                     + " s. Engines shut, HOLD-DOWNS STILL HELD. Crew action required.");
                    break;
            }

            // Anything above may have changed the part tree (two decouples) or the engine set (two
            // ignitions). See the hazard note at the top of this file.
            if (act != AscentAct.None) RefreshGimbalEngineLists(v, act.ToString());
        }

        // ============================ THE ON-ORBIT EXECUTOR (T19) ============================
        //
        //  §B9 Phase 2-3, driven exactly as §B1 and §B12.4 require: **the conductor composes MechJeb's
        //  Maneuver-Planner operations itself and re-plans live**, because MechJeb's own RENDEZVOUS
        //  AUTOPILOT is unreliable in RSS/RO. ⛔ `MechJebModuleRendezvousAutopilot` exists in the pinned
        //  tree, is deliberately absent from `ConductorOp`, and is never touched here.
        //
        //      plan    -> Operation.MakeNodes(orbit, UT, target) -> Vessel.PlaceManeuverNode
        //      burn    -> MechJebModuleNodeExecutor.ExecuteOneNode(owner)
        //      judge   -> §B12.4's three disjuncts, measured, against RendezvousOps' cited numbers
        //      re-plan -> abort the executor, clear the node, build the operation again
        //
        //  ⛔ `ExecuteOneNode`, NEVER `ExecuteAllNodes` — §B10.1: *"Drive via `ExecuteOneNode()` (one
        //  burn, re-plan after) — the conductor's default so it can re-plan between rendezvous burns"*.

        /// <summary>Build the operation the pure core asked for and drop its node(s) on the flight plan.</summary>
        static void PlanOperation(Vessel v, ConductorAction a)
        {
            try
            {
                core.AuthorizeDrive(true);

                MuMech.MechJebModuleTargetController tc = core.Target;
                if (NeedsTarget(a.Op) && (tc == null || !tc.NormalTargetExists))
                {
                    // ⛔ NOT AN ERROR, AND NOT A GUESS. Every target-relative operation throws an
                    // `OperationException` without one. The conductor says so and waits; it never
                    // invents a target (§1.4) and never plans a burn it cannot aim.
                    note = "no target selected - the rendezvous needs the station targeted";
                    LogOnce("no-target", "[DragonScreen] conductor: " + a.Op + " needs a TARGET and "
                            + "none is selected. Target the station and the approach resumes.");
                    return;
                }

                MuMech.Operation op = Build(v, a.Op);
                if (op == null) { StandDown(v, "no operation backs " + a.Op); return; }

                Orbit o = v.orbit;
                double ut = Now();
                List<MuMech.ManeuverParameters> nodes = op.MakeNodes(o, ut, tc);
                if (nodes == null || nodes.Count == 0)
                {
                    // MechJeb's own error text, surfaced rather than swallowed.
                    string why = op.GetErrorMessage();
                    note = a.Op + " could not be planned" + (string.IsNullOrEmpty(why) ? "" : ": " + why);
                    LogOnce("plan-fail-" + a.Op, "[DragonScreen] conductor: " + note);
                    return;
                }

                ClearNodes(v);
                for (int i = 0; i < nodes.Count; i++)
                    MuMech.VesselExtensions.PlaceManeuverNode(v, o, nodes[i].dV, nodes[i].UT);

                nodePlanned = true; nodeBurned = false; nodeExecuting = false;
                lastNodeDvLeft = 0.0; nodeResidualMps = 0.0;
                Debug.Log("[DragonScreen] conductor: planned " + a.Op + " (" + MechOps.ClassFor(a.Op)
                          + ") - " + nodes.Count + " node(s), " + a.Reason);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor: planning " + a.Op + " failed: " + e.Message);
                note = a.Op + " planning failed: " + e.Message;
            }
        }

        /// <summary>Hand the node to the Node Executor, and watch it to the end.</summary>
        static void BurnNode(Vessel v, ConductorAction a)
        {
            try
            {
                core.AuthorizeDrive(true);
                MuMech.MechJebModuleNodeExecutor ne = core.Node;
                if (ne == null) { StandDown(v, "the core has no Node Executor"); return; }

                // ⭐ S219 — §8's `RCSOnly`, on EVERY node burn and not only the rendezvous ones. The
                // deorbit burn goes through this same executor and the free-flying Dragon has no main
                // engine either; see `RendezvousOps.NodeBurnsOnRcs` for the craft-file evidence.
                SetNodeRcsOnly(v);

                int count = NodeCount(v);

                // ⭐ THE RESIDUAL IS CAPTURED WHILE THE NODE STILL EXISTS. `_dvLeft` is private and the
                // executor DELETES the node when it terminates, so the only moment the cutoff residual
                // can be read is the tick before it disappears - which is why it is tracked every tick
                // rather than asked for afterwards. §B12.4 compares this against §B10.1's tolerance.
                if (count > 0)
                {
                    ManeuverNode n = v.patchedConicSolver.maneuverNodes[0];
                    lastNodeDvLeft = n.GetBurnVector(v.orbit).magnitude;
                }

                if (!nodeExecuting)
                {
                    ne.ExecuteOneNode(Owner);
                    nodeExecuting = true;
                    Debug.Log("[DragonScreen] conductor: burning the " + a.Op + " node ("
                              + count + " on the plan, tolerance "
                              + RendezvousOps.NodeResidualToleranceFor(leg).ToString("F2") + " m/s)");
                    return;
                }

                // The node list emptying is the burn finishing: `ShouldTerminateStock` removes the node
                // and calls `Abort()`, which clears the executor's users and returns it to IDLE.
                if (count == 0)
                {
                    nodeBurned = true; nodeExecuting = false;
                    nodeResidualMps = lastNodeDvLeft;
                    Debug.Log("[DragonScreen] conductor: " + a.Op + " node flown - residual "
                              + nodeResidualMps.ToString("F3") + " m/s (tolerance "
                              + RendezvousOps.NodeResidualToleranceFor(leg).ToString("F2") + ")");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor: burning " + a.Op + " failed: " + e.Message);
            }
        }

        /// <summary>
        /// §B12.4: the executed node did not achieve its intent - rebuild the operation and burn again.
        /// ⛔ The chain index is NOT advanced: a step whose burn missed is not a step that is done, and
        /// `pure/Conductor.cs` says so at the point it makes this decision.
        /// </summary>
        static void Replan(Vessel v, ConductorAction a)
        {
            try
            {
                if (core != null && core.Node != null && nodeExecuting) core.Node.Abort();
                ClearNodes(v);
                nodePlanned = false; nodeBurned = false; nodeExecuting = false;
                nodeResidualMps = 0.0; lastNodeDvLeft = 0.0;
                Debug.LogWarning("[DragonScreen] conductor: RE-PLAN - " + a.Reason
                                 + "  [leg " + leg + ", pass " + approachPasses + "]");
                note = a.Reason;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor: re-plan failed: " + e.Message);
            }
        }

        /// <summary>
        /// One step of the operation chain finished (a CHAIN-level `Advance`, not a plan-level one -
        /// see the note at the `Advance` case).
        ///
        /// ⭐ When the whole chain has run out, `RendezvousOps.OnChainComplete` decides what that means,
        /// and the two answers differ for a reason: a PHASING leg ends on its orbit (its chain IS the
        /// leg, so the G9 poll comes up), while an APPROACH leg ends on a RANGE and one pass does not
        /// get there from a phasing orbit - §B10.2's *"Run 1-2x to walk the closest approach down"*.
        /// </summary>
        static void ChainStepFinished(Vessel v, ConductorAction a)
        {
            ClearNodes(v);
            nodePlanned = false; nodeBurned = false; nodeExecuting = false; nodeResidualMps = 0.0;

            if (leg != RendezvousLeg.Phasing && approachStepsDone < Conductor.ApproachChain.Length)
            {
                approachStepsDone++;
                Debug.Log("[DragonScreen] conductor: chain step " + approachStepsDone + "/"
                          + Conductor.ApproachChain.Length + " done on leg " + leg + " - " + a.Reason);
                return;
            }

            if (RendezvousOps.OnChainComplete(leg) == RendezvousOps.ChainEnd.CompleteLeg)
            {
                CrewProcedureOps.PhaseComplete();
                Debug.Log("[DragonScreen] conductor: " + leg + " leg complete - plan advanced ("
                          + CrewProcedureOps.PhaseName + ")");
                ResetLeg();
                return;
            }

            approachStepsDone = 0; approachPasses++;
            Debug.Log("[DragonScreen] conductor: approach chain pass " + approachPasses
                      + " finished short of " + leg + " - walking the closest approach down again "
                      + "(B10.2). The next intercept target follows the ladder as the range closes.");
        }

        static void ResetLeg()
        {
            approachStepsDone = 0; approachPasses = 0;
            nodePlanned = false; nodeBurned = false; nodeExecuting = false;
            nodeResidualMps = 0.0; lastNodeDvLeft = 0.0;
            approachNote = null;
        }

        // ---- building an operation (§B10.2: per-invocation, never persisted) ----------------------

        /// <summary>Does this operation throw without a target selected?</summary>
        static bool NeedsTarget(ConductorOp op)
        {
            return op == ConductorOp.MatchPlane || op == ConductorOp.Transfer
                || op == ConductorOp.CourseCorrection || op == ConductorOp.KillRelVel;
        }

        /// <summary>
        /// A fresh <c>Operation</c> per invocation - §B10.2: *"per-invocation (never persisted); set
        /// fields then MakeNodes()"*.
        ///
        /// ⛔ THE CLASS NAMES COME FROM `pure/RendezvousOps.cs`'s `MechOps`, WHICH IS PINNED AGAINST THE
        /// VENDORED TREE, and one of them is not what the plan calls it: the transfer is
        /// <c>OperationGeneric</c>, in a file called <c>OperationTransfer.cs</c>. §B10.2's own warning
        /// ("verify exact C# class names vs the pinned MechJeb source") asked for exactly this check.
        ///
        /// ⚠ AND ONLY THE PARAMETERS §B10.2 GIVES A VALUE FOR ARE SET. `OperationGeneric`'s flags
        /// (`Capture`, `PlanCapture`, `MatchOrbit`, `Coplanar`) are left at MechJeb's own defaults:
        /// §B10.2 names them in kRPC's vocabulary (`intercept_only`, `simple_transfer`) and this repo
        /// has no source mapping those onto these fields, so choosing values would be invention (§1.4).
        /// §B5's discipline says begin from defaults; the mapping is an open question on T19's line.
        /// </summary>
        static MuMech.Operation Build(Vessel v, ConductorOp op)
        {
            switch (op)
            {
                case ConductorOp.Circularize:
                    return new MuMech.OperationCircularize();

                case ConductorOp.MatchPlane:
                    return new MuMech.OperationPlane();

                case ConductorOp.Transfer:
                    return new MuMech.OperationGeneric();   // ⛔ NOT `OperationTransfer` - see above

                case ConductorOp.KillRelVel:
                    return new MuMech.OperationKillRelVel();

                case ConductorOp.CourseCorrection:
                {
                    MuMech.OperationCourseCorrection cc = new MuMech.OperationCourseCorrection();
                    double range, rel;
                    Measure(v, out range, out rel);
                    // §B10.2 / §B11's ladder: 4 km -> 1 km -> the leg's own waypoint.
                    cc.InterceptDistance.Val = RendezvousOps.InterceptDistanceM(leg, range, approachPasses);
                    return cc;
                }

                case ConductorOp.Periapsis:
                {
                    MuMech.OperationPeriapsis pe = new MuMech.OperationPeriapsis();
                    pe.NewPeA.Val = ReturnPeriapsisM(v);
                    return pe;
                }

                case ConductorOp.Apoapsis:
                {
                    MuMech.OperationApoapsis ap = new MuMech.OperationApoapsis();
                    ap.NewApA.Val = v.orbit != null ? v.orbit.ApA : 0.0;   // shape-preserving default
                    return ap;
                }

                default:
                    return null;   // KillRot / HeatShieldForward are SmartASS, not planner operations
            }
        }

        /// <summary>
        /// The deorbit periapsis. ⛔ T21's, and NOT decided here - this returns the CURRENT periapsis,
        /// so an `OperationPeriapsis` built before T21 lands is a no-op burn rather than a guessed
        /// deorbit. T21 replaces it in the same diff as its own entry corridor.
        /// </summary>
        static double ReturnPeriapsisM(Vessel v)
        {
            return v.orbit != null ? v.orbit.PeA : 0.0;
        }

        // ---- measurement -------------------------------------------------------------------------

        /// <summary>
        /// Range and relative speed to the targeted station, read off the VESSEL rather than off
        /// MechJeb. ⭐ Deliberately: `MechJebModuleTargetController` only syncs its target inside
        /// `OnFixedUpdate`, which `MechJebCore` skips whenever the core is not master - so a reading
        /// taken through it would be stale exactly when the conductor had just stood down.
        /// </summary>
        static void Measure(Vessel v, out double rangeM, out double relSpeedMps)
        {
            rangeM = 0.0; relSpeedMps = 0.0;
            try
            {
                ITargetable tgt = v.targetObject;
                if (tgt == null) return;
                Vessel tv = tgt.GetVessel();
                if (tv == null || tv == v) return;
                rangeM = (tv.GetWorldPos3D() - v.GetWorldPos3D()).magnitude;
                relSpeedMps = (tv.obt_velocity - v.obt_velocity).magnitude;
            }
            catch { rangeM = 0.0; relSpeedMps = 0.0; }
        }

        /// <summary>
        /// §B12.4's third disjunct needs a RATE, not an instant. The range is differenced over time;
        /// positive means opening. ⛔ Smoothed hard (a 5 s lag) because an unsmoothed range rate on an
        /// orbital approach oscillates through zero every few seconds, and re-planning a whole
        /// rendezvous on that noise is exactly what §B10.1 warns about for the burn tolerance.
        /// </summary>
        static void TrackOpeningRate(double rangeM)
        {
            double now = Now();
            if (rangeM <= 0.0) { lastRangeM = 0.0; lastRangeUT = now; openingRateMps = 0.0; return; }
            if (lastRangeM <= 0.0 || now <= lastRangeUT) { lastRangeM = rangeM; lastRangeUT = now; return; }

            double dt = now - lastRangeUT;
            if (dt < 1.0) return;                       // one sample a second is plenty
            double raw = (rangeM - lastRangeM) / dt;
            double k = dt / (5.0 + dt);                 // first-order lag, 5 s
            openingRateMps += k * (raw - openingRateMps);
            lastRangeM = rangeM; lastRangeUT = now;
        }

        /// <summary>
        /// §B12.4's `closestApproachErr`: how far the PREDICTED closest approach misses the distance
        /// this pass is aiming at. Uses MechJeb's own `NextClosestApproachDistance`, which is what the
        /// planner's own course correction solves against. Zero (= no error) when there is no target,
        /// which `Conductor` then cannot trip.
        /// </summary>
        static double ClosestApproachErrorM(Vessel v, double rangeM)
        {
            try
            {
                if (leg == RendezvousLeg.None || leg == RendezvousLeg.Phasing) return 0.0;
                ITargetable tgt = v.targetObject;
                if (tgt == null || v.orbit == null) return 0.0;
                Orbit to = tgt.GetOrbit();
                if (to == null) return 0.0;
                double want = RendezvousOps.InterceptDistanceM(leg, rangeM, approachPasses);
                double got = MuMech.OrbitExtensions.NextClosestApproachDistance(v.orbit, to, Now());
                double err = got - want;
                return err < 0.0 ? -err : err;
            }
            catch { return 0.0; }
        }

        static int NodeCount(Vessel v)
        {
            try
            {
                return (v.patchedConicSolver == null || v.patchedConicSolver.maneuverNodes == null)
                     ? 0 : v.patchedConicSolver.maneuverNodes.Count;
            }
            catch { return 0; }
        }

        static void ClearNodes(Vessel v)
        {
            try
            {
                if (v.patchedConicSolver == null || v.patchedConicSolver.maneuverNodes == null) return;
                while (v.patchedConicSolver.maneuverNodes.Count > 0)
                    v.patchedConicSolver.maneuverNodes[0].RemoveSelf();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor: clearing nodes failed: " + e.Message);
            }
        }

        /// <summary>A log line that repeats every physics frame is a log line nobody reads. One per key.</summary>
        static readonly HashSet<string> logged = new HashSet<string>();
        static void LogOnce(string key, string message)
        {
            if (logged.Contains(key)) return;
            logged.Add(key);
            Debug.Log(message);
        }

        // ============================ §8 — MECHJEB'S RENDEZVOUS AUTOPILOT (S219) ============================
        //
        //  Owner, 2026-09-07, verbatim — an `OVERRIDE` of §B1/§B12.4's default (C1.8):
        //      "mechjeb rendezvous autopilot just for now to get things moving… Then we move to the more
        //       complicated, mission accurate fidelity way"
        //
        //  `docs/MECHJEB_MASTER_MAP.md` §8 is the specification, and its verdict is the reason this is
        //  now viable at all: the eight-branch tree is SOUND, and the reason it "doesn't work for us" was
        //  never the autopilot — it was **branch 8**. Two 51.64° orbits at different RAAN have a large
        //  RELATIVE inclination, and the LEO plane-match burn is hundreds of m/s the Dragon cannot afford,
        //  so it churns and runs dry. §8's own answer: *"Launch coplanar (§7.5) and it drops into branch 6
        //  (cheap Hohmann phasing) instead."* Job 2's ascent half is what makes that true, so the two
        //  halves of this register line are one change, not two.
        //
        //  ⛔ THE CONDUCTOR'S OWN PATH IS NOT DELETED. `PlanOperation` / `BurnNode` / `Replan` — §B12.4's
        //  compose-fly-judge-replan loop and §B11's waypoint ladder — are untouched and one assignment
        //  away (`SelectRendezvousDrive`). They are marked SUPERSEDED-FOR-NOW in `RendezvousDrive`, which
        //  is where the owner's words live.

        /// <summary>Set MechJeb's rendezvous autopilot up the way §8 says, engage it, and watch for the
        /// hand-off. ⭐ SET, **THEN ENGAGE** — the owner's central point: *"otherwise it will sit there
        /// ready to go but do nothing."*</summary>
        static void RunRendezvousAutopilot(Vessel v, ConductorAction a)
        {
            try
            {
                MuMech.MechJebModuleRendezvousAutopilot ap =
                    core.GetComputerModule<MuMech.MechJebModuleRendezvousAutopilot>();
                if (ap == null) { StandDown(v, "the core has no Rendezvous Autopilot"); return; }

                MuMech.MechJebModuleTargetController tc = core.Target;
                if (tc == null || !tc.NormalTargetExists)
                {
                    if (rendezvousEngaged) ReleaseRendezvous("the target was lost");
                    StandDown(v, "no target for the rendezvous autopilot - " + a.Reason);
                    return;
                }

                double rangeM, relSpeedMps;
                Measure(v, out rangeM, out relSpeedMps);

                core.AuthorizeDrive(true);

                // ---- ⭐ THE OPTIONS, PER §8's DECISION TREE. Re-asserted every tick, like the docking
                // ---- ladder, because they are what the tree branches on and a stale one changes the branch.
                //
                // (1) `desiredDistance` — WHERE IT STOPS. MechJeb's default is 100 m, which is INSIDE
                //     §B11's 200 m Keep-Out Sphere; left alone the autopilot would fly the Dragon through
                //     the KOS on maneuver nodes. The hand-off range is the published KOS (a MISSION FACT,
                //     `RendezvousOps.AutopilotHandoffRangeM`), and inside it the Docking Autopilot is the
                //     DEFAULT (O6 / §B10.3 / §B12.3).
                if (ap.desiredDistance.Val != RendezvousOps.AutopilotHandoffRangeM)
                    ap.desiredDistance.Val = RendezvousOps.AutopilotHandoffRangeM;

                // (2) `maxClosingSpeed` — §8's branch 4/5 clamp on the approach. Left at MechJeb's own
                //     100 m/s: it only ever caps a closing speed the tree already computed, it is never a
                //     commanded speed, and the speed that actually matters to the crew is the docking
                //     ladder's cap inside the KOS, which `RunDocking` owns. Lowering it is a §B5/T22 tune.
                // (3) `maxPhasingOrbits` — 5, MechJeb's own. It is how many revolutions the tree may
                //     spend catching up before it gives up and phases; there is no in-repo source for a
                //     different number, so RO's baseline stands (the Part-B gate's rule).

                // (4) ⭐⭐ `Core.Node.RCSOnly` — §8's closing note, and on this craft the difference
                //     between a burn and a hang. The rule and the craft-file evidence are in
                //     `RendezvousOps.NodeBurnsOnRcs`. Measured from the vessel every tick, because the
                //     answer changes at Dragon separation and a phase label is not a part.
                SetNodeRcsOnly(v);

                // (5) AUTO-WARP. §8: the autopilot does not own a warp flag — it NARROWS `Core.Node`'s
                //     (`Core.Node.Autowarp = Core.Node.Autowarp && Core.Target.Distance > 1000`), so the
                //     one `Configure` sets is the one it reads. Re-asserted here because that narrowing
                //     is a WRITE: once inside 1 km the autopilot latches it false, and it must come back
                //     for the next mission phase that wants a warp.
                try { if (core.Node != null && rangeM > 1000.0) core.Node.Autowarp = true; } catch { }

                // ---- ⭐ THEN ENGAGE ---------------------------------------------------------------
                if (!rendezvousEngaged)
                {
                    ClearNodes(v);          // §8: `OnModuleEnabled` removes them anyway; do it visibly
                    ap.Users.Add(Owner);
                    rendezvousEngaged = true;
                    Debug.Log("[DragonScreen] conductor: ⭐ RENDEZVOUS AUTOPILOT ENGAGED (owner OVERRIDE "
                              + "2026-09-07) at " + rangeM.ToString("F0") + " m, rel "
                              + relSpeedMps.ToString("F1") + " m/s. Stops at "
                              + RendezvousOps.AutopilotHandoffRangeM.ToString("F0")
                              + " m (§B11 Keep-Out Sphere) and hands to the Docking Autopilot. "
                              + "⚠ It does NOT walk §B11's WP0/WP1/WP2 ladder - that is the conductor's own "
                              + "path, superseded-for-now, not retired. " + a.Reason);
                }

                // ---- THE HAND-OFF. §8 branch 2: within `desiredDistance` with relvel < 1 is DONE, and
                // ---- the module clears its own users when it gets there. Either signal ends the leg.
                bool moduleFinished = !ap.Enabled;
                bool arrived = rangeM <= RendezvousOps.AutopilotHandoffRangeM
                               && relSpeedMps < 1.0;
                if (moduleFinished || arrived)
                {
                    ReleaseRendezvous(moduleFinished ? "the autopilot reported done"
                                                     : "arrived at the Keep-Out Sphere");
                    ClearNodes(v);
                    CrewProcedureOps.PhaseComplete();
                    ResetLeg();
                    Debug.Log("[DragonScreen] conductor: rendezvous leg complete at "
                              + rangeM.ToString("F0") + " m, rel " + relSpeedMps.ToString("F2")
                              + " m/s - plan advanced (" + CrewProcedureOps.PhaseName + "). (S219)");
                    return;
                }

                approachNote = "RNDZ " + (rangeM / 1000.0).ToString("F2") + " km, rel "
                             + relSpeedMps.ToString("F1") + " m/s"
                             + (string.IsNullOrEmpty(ap.status) ? "" : " - " + ap.status);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor: rendezvous autopilot tick failed: " + e.Message);
            }
        }

        /// <summary>Take MechJeb's rendezvous autopilot off, for any reason, and say which.</summary>
        static void ReleaseRendezvous(string why)
        {
            try
            {
                MuMech.MechJebModuleRendezvousAutopilot ap =
                    core == null ? null : core.GetComputerModule<MuMech.MechJebModuleRendezvousAutopilot>();
                if (ap != null) ap.Users.Remove(Owner);
            }
            catch { }
            rendezvousEngaged = false; approachNote = null;
            Debug.Log("[DragonScreen] conductor: rendezvous autopilot released - " + why);
        }

        /// <summary>
        /// ⭐⭐ S219 — `Core.Node.RCSOnly`, decided from the PARTS and re-checked every tick.
        /// See `RendezvousOps.NodeBurnsOnRcs` for the argument and the craft-file evidence: after Dragon
        /// separation the only `ModuleEngines` left is the SuperDraco ABORT motor, so a node burn that
        /// commands `mainThrottle` has no thrust at all and the executor hangs.
        /// ⚠ It governs BOTH rendezvous drivers and the deorbit burn, because all three go through the
        /// same `MechJebModuleNodeExecutor`.
        /// </summary>
        static void SetNodeRcsOnly(Vessel v)
        {
            try
            {
                MuMech.MechJebModuleNodeExecutor ne = core == null ? null : core.Node;
                if (ne == null) return;
                bool hasMain = Actuator.FindEngine(v, EngineRole.SecondStage) != null;
                bool want = RendezvousOps.NodeBurnsOnRcs(hasMain);
                if (ne.RCSOnly == want) return;
                ne.RCSOnly = want;
                Debug.Log("[DragonScreen] conductor: Node Executor RCSOnly -> " + want
                          + (want ? " - no main engine on this vessel; the only ModuleEngines left is the "
                                  + "SuperDraco abort motor, so a mainThrottle burn would have NO thrust "
                                  + "and hang (§8). Burning on Dracos."
                                  : " - a main engine is present; the executor may throttle it.")
                          + " (S219)");
            }
            catch (Exception e)
            { Debug.LogWarning("[DragonScreen] conductor: could not set Node RCSOnly: " + e.Message); }
        }

        // ============================ THE DOCKING EXECUTOR (T20) ============================
        //
        //  §B9 Phase 4 / §B10.3 / O6. The Docking Autopilot is the DEFAULT from the Keep-Out Sphere
        //  inward — owner decision O6, 2026-09-03 via the overseer — and the conductor's job here is
        //  three things and no more: engage it on the CAPTURE leg only, walk `speedLimit` down the
        //  §B10.3 ladder as the range closes, and shut it down the moment the crew take manual docking.
        //
        //  ⛔ IT IS NOT RE-IMPLEMENTED. MechJeb's docking autopilot flies the corridor, the wrong-side
        //  recovery and the axis alignment; every one of those is code we would otherwise be writing
        //  from scratch next to a station. The conductor sets the cap and gets out of the way.

        /// <summary>Engage (or keep) the Docking Autopilot, with the ladder's cap for this range.</summary>
        static void RunDocking(Vessel v, ConductorAction a)
        {
            try
            {
                MuMech.MechJebModuleDockingAutopilot ap = core.GetComputerModule<MuMech.MechJebModuleDockingAutopilot>();
                if (ap == null) { StandDown(v, "the core has no Docking Autopilot"); return; }

                // ⭐ THE CREW'S OVERRIDE OUTRANKS EVERYTHING HERE. §B12.3 / §B10.3: "Pressing the manual
                // docking button switches to the Manual ISS Docking screen and SHUTS DOWN the Docking
                // Autopilot." That is a crew command, so it is only ever an INPUT — this file never
                // initiates it — and when it arrives the autopilot goes off and stays off.
                if (manualDockingRequested)
                {
                    if (dockingEngaged) ReleaseDocking("crew took manual docking");
                    StandDown(v, "manual docking - the crew have the vehicle");
                    return;
                }

                double rangeM, relSpeedMps;
                Measure(v, out rangeM, out relSpeedMps);

                core.AuthorizeDrive(true);

                // §B10.3's ladder. Re-asserted every tick, not set once: the whole point is that it
                // walks DOWN as the corridor narrows, and `speedLimit` is a live field MechJeb clamps
                // its own computed approach speed against (`FixSpeed`), never a commanded speed.
                double cap = DockingLadder.SpeedLimitFor(rangeM);
                if (ap.speedLimit.Val != cap)
                {
                    ap.speedLimit.Val = cap;
                    Debug.Log("[DragonScreen] conductor: docking speedLimit -> " + cap.ToString("F2")
                              + " m/s at " + rangeM.ToString("F1") + " m (B10.3 ladder)");
                }

                // ⭐⭐ S219 JOB 2 — **EVERYTHING ELSE §9's MODULE NEEDS, SET BEFORE THE ENGAGE.**
                // Before this line the conductor set `speedLimit` and `forceRol` and nothing more, and
                // the brief is precise about why that is not enough: *"§9 says what else the module
                // needs. Set it, then engage."* Read off `MechJebModuleDockingAutopilot` itself:
                //
                // (a) `rol` — the roll ANGLE `forceRol` aligns to (`Drive:227-229` builds
                //     `AngleAxis(-(float)rol, back)` in `TARGET_ORIENTATION`). It is `Pass.LOCAL`
                //     PERSISTED, so "its own default of 0" is only true on a vessel that has never been
                //     docked with before. §B10.3 wants the port's own orientation, which is rol = 0.
                // (b/c) `overrideSafeDistance` / `overrideTargetSize` — also `Pass.LOCAL` persisted
                //     booleans, and when either is true the module uses `overridenSafeDistance` /
                //     `overridenTargetSize` (5 m / 10 m) INSTEAD of the measured bounding boxes
                //     (`OnFixedUpdate:256-263`). A stale true would fly the corridor against a 5 m safe
                //     distance next to a station, or back the Dragon away from one forever. T20's
                //     reasoning for not FORCING them (§B10.3's "safe-distance = the Keep-Out Sphere" is a
                //     tuning target, and forcing 200 m would pin the module in WRONG_SIDE_BACKING_UP) is
                //     unchanged and still right — but "leave the module's own default" means WRITE the
                //     default, not inherit whatever a previous save left behind.
                // (d) `drawBoundingBox` — the module registers `DrawBoundingBox` on the core's
                //     post-draw queue in `OnStart` regardless of T15b's GUI suppression; this flag is
                //     the only thing that stops it drawing over the IVA.
                ap.rol.Val = 0.0;
                ap.forceRol = true;
                ap.overrideSafeDistance = false;
                ap.overrideTargetSize = false;
                ap.drawBoundingBox = false;

                if (!dockingEngaged)
                {
                    // (e) ⛔ THE TARGET MUST BE A DOCKING PORT, NOT MERELY THE STATION. `InitDocking:349-352`
                    //     reads `acquireRange` off the target only `if (Core.Target.Target is
                    //     ModuleDockingNode)` and otherwise falls back to 0.25 m, and every alignment in
                    //     `Drive` is expressed in `AttitudeReference.TARGET_ORIENTATION` — which is the
                    //     PORT's frame when a port is targeted and the whole vessel's when it is not.
                    // ⚠ SELECTING WHICH PORT IS NOT THIS FILE'S TO DO. §B10.3 names IDA-2, but nothing in
                    //     the repo maps a station's ports to that name (§1.4: no invented source), and the
                    //     crew's own target selection is the authority. So this ANNUNCIATES rather than
                    //     picks — loudly, once, because a docking flown against a vessel centre instead of
                    //     a port looks almost right until the last two metres.
                    bool portTargeted = core.Target != null
                                        && core.Target.Target is ModuleDockingNode;
                    if (!portTargeted)
                        Debug.LogWarning("[DragonScreen] conductor: ⚠ DOCKING AUTOPILOT ENGAGING WITHOUT A "
                                         + "PORT TARGET - the crew have targeted the vessel, not a docking "
                                         + "node. MechJeb will align to the vessel's frame and use a 0.25 m "
                                         + "acquire range instead of the port's own (§9 / InitDocking:349). "
                                         + "Target the PORT for a nominal approach. (S219)");

                    // ⛔ `overrideSafeDistance` IS DELIBERATELY LEFT ALONE, AND THIS IS THE ONE PLACE
                    // T20 DOES NOT FOLLOW §B10.3's WORDING. §B10.3 says "safe-distance = the Keep-Out
                    // Sphere", but read in the vendored source, `safeDistance` is NOT an operational
                    // keep-out radius: `OnFixedUpdate` computes it as
                    // `vesselBoundingBox.size.magnitude + targetSize + 0.5f` and `Drive` uses it as the
                    // HULL-clearance radius for the wrong-side recovery. Forcing it to 200 m would put
                    // the autopilot permanently in WRONG_SIDE_BACKING_UP and back the Dragon away from
                    // the station for as long as it was engaged.
                    // ⚠ NOT a decision to deviate: §B10.3's line is a TUNING target, and §0's standing
                    // gate defers the one-parameter-at-a-time tune until after the first recorded
                    // flight (T22). Leaving MechJeb's own default IS what that gate says to do. Raised
                    // as an owner question on T20's register line rather than settled here.

                    ap.Users.Add(Owner);
                    dockingEngaged = true;
                    Debug.Log("[DragonScreen] conductor: DOCKING AUTOPILOT engaged (O6 default) at "
                              + rangeM.ToString("F1") + " m, cap " + cap.ToString("F2")
                              + " m/s, roll-align ON. " + a.Reason);
                }

                dockingNote = "DOCK " + rangeM.ToString("F1") + " m, cap " + cap.ToString("F2") + " m/s"
                            + (string.IsNullOrEmpty(ap.status) ? "" : " - " + ap.status);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor: docking tick failed: " + e.Message);
            }
        }

        /// <summary>Attitude hold while berthed - §B12.3's "Docked -> idle/KILL-ROT" (§B10.5).</summary>
        static void RunAttitudeHold(Vessel v, ConductorAction a)
        {
            try
            {
                MuMech.MechJebModuleSmartASS sa = core.SmartASS;
                if (sa == null) { StandDown(v, "the core has no SmartASS"); return; }
                core.AuthorizeDrive(true);

                if (smartAssTarget != MuMech.MechJebModuleSmartASS.Target.KILLROT)
                {
                    SetSmartAss(sa, MuMech.MechJebModuleSmartASS.Target.KILLROT);
                    Debug.Log("[DragonScreen] conductor: SmartASS KILL-ROT engaged - " + a.Reason);
                }
                dockingNote = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor: attitude hold failed: " + e.Message);
            }
        }

        /// <summary>Take the Docking Autopilot off, for any reason, and say which.</summary>
        static void ReleaseDocking(string why)
        {
            try
            {
                MuMech.MechJebModuleDockingAutopilot ap =
                    core == null ? null : core.GetComputerModule<MuMech.MechJebModuleDockingAutopilot>();
                if (ap != null) ap.Users.Remove(Owner);
            }
            catch { }
            dockingEngaged = false; dockingNote = null;
            Debug.Log("[DragonScreen] conductor: docking autopilot released - " + why);
        }

        /// <summary>
        /// Point SmartASS at one of its targets. ⭐ BOTH `target` AND `mode` are set, from MechJeb's own
        /// `Target2Mode` table, because the two are a pair: `Engage()` resolves the attitude from
        /// `target`, while `mode` is what the module's own update path reads back. Setting one without
        /// the other leaves the module internally inconsistent.
        /// </summary>
        static void SetSmartAss(MuMech.MechJebModuleSmartASS sa, MuMech.MechJebModuleSmartASS.Target tgt)
        {
            sa.target = tgt;
            sa.mode = MuMech.MechJebModuleSmartASS.Target2Mode[(int)tgt];
            sa.Engage();
            smartAssTarget = tgt;
        }

        /// <summary>Take SmartASS off. `Target.OFF` is MechJeb's own "deactivate the attitude hold".</summary>
        static void ReleaseAttitude()
        {
            try
            {
                MuMech.MechJebModuleSmartASS sa = core == null ? null : core.SmartASS;
                if (sa != null && smartAssTarget != MuMech.MechJebModuleSmartASS.Target.OFF)
                    SetSmartAss(sa, MuMech.MechJebModuleSmartASS.Target.OFF);
            }
            catch { }
            smartAssTarget = MuMech.MechJebModuleSmartASS.Target.OFF;
        }

        // ---- the crew's manual-docking override --------------------------------------------------

        /// <summary>
        /// ⭐ THE CREW TAKE MANUAL DOCKING. §B12.3 / §B10.3 / O6: pressing the manual docking button
        /// switches to the Manual ISS Docking screen and SHUTS THE DOCKING AUTOPILOT DOWN.
        ///
        /// ⛔ THE BUTTON THAT CALLS THIS IS **NOT** WIRED BY T20, AND THAT IS THE BATCH'S OWN SCOPE
        /// LINE: "THE SCREENS' FLIGHT BUTTONS ARE NOT IN SCOPE. These wire the autopilot, not the UI's
        /// command surface." So the MECHANISM is here, tested and live, and the screen-side press is
        /// §B12.5's front-end - logged on T20's register line, not built. Nothing in the tree calls
        /// this today, which is exactly §14.4(a).
        ///
        /// ⚠ IT IS ONE-WAY WITHIN A FLIGHT SCENE. Once the crew have taken the vehicle, the conductor
        /// does not take it back on its own; `Resume` is an explicit, separate act.
        /// </summary>
        public static void RequestManualDocking()
        {
            manualDockingRequested = true;
            Debug.Log("[DragonScreen] MANUAL DOCKING requested - the docking autopilot stands down.");
        }

        /// <summary>Give the docking autopilot back the approach after a manual take-over.</summary>
        public static void ResumeAutoDocking()
        {
            manualDockingRequested = false;
            Debug.Log("[DragonScreen] auto docking resumed.");
        }

        /// <summary>True while the crew hold the docking approach by hand.</summary>
        public static bool ManualDocking { get { return manualDockingRequested; } }

        // ============================ THE RETURN EXECUTOR (T21) ============================
        //
        //  §B9 Phases 6-10. Undock, back away, drop the trunk, burn the deorbit, close the nose cone,
        //  ride it down heat-shield-forward (O8: NO commanded bank), drogues, mains, splash, release.
        //
        //  ⛔ THE ABORT HALF OF T21 IS **NOT** HERE, AND THAT IS DELIBERATE. T21's title includes
        //  "abort wiring", and §B13.4 routes it through `FlightDriver.RequestAbort` / `AbortControl` -
        //  which is register **W19**, and W19 is **HELD**: blocked on `src/Steering.cs`, which §B12.8
        //  rider (b) forbids recovering. Building an abort executor here would be executing a held line
        //  sideways. So the abort path remains exactly what it was: `Conductor` can DECIDE `Abort`, and
        //  all this file does with that is stop flying and hand the vehicle back (see the `Abort` case
        //  in `Tick`). §14.4(a): no light, no action, and NO RED. Logged on T21's register line.

        /// <summary>The return, one tick. Driven by `pure/ReturnSequence.cs`; actuated through
        /// `Actuator` (§B12.7) and MechJeb's SmartASS / Node Executor.</summary>
        static void RunReturn(Vessel v, ConductorAction a, MissionPhase phase)
        {
            try
            {
                core.AuthorizeDrive(true);

                double now = Now();
                ReturnInputs s = ReturnInputs.Nominal();
                double rel;
                Measure(v, out s.RangeM, out rel);
                s.Docked         = DockedSide.Docked(v);
                s.TrunkAttached  = HasTrunk(v);
                s.AltitudeM      = v.altitude;
                s.Descending     = v.verticalSpeed < -1.0;
                s.DroguesOut     = ChutesOut(v, true);
                s.MainsOut       = ChutesOut(v, false);
                s.Splashed       = v.situation == Vessel.Situations.SPLASHED
                                || v.situation == Vessel.Situations.LANDED;
                s.NodeExists     = nodePlanned;
                s.NodeBurned     = nodeBurned;
                s.SinceStepS     = now - returnStepStartUT;

                // ⭐ THE CREW'S G15 GO IS WHAT STARTS THE DEORBIT, AND IT ARRIVES AS A PHASE CHANGE.
                // `ReturnSequence.Step` never walks from `Departed` into the deorbit by itself; the plan
                // reaching `MissionPhase.Entry` is the crew having cleared the gate, and `BeginDeorbit`
                // is the separate entry point that turns that into a step.
                if (phase == MissionPhase.Entry || phase == MissionPhase.Drogues
                    || phase == MissionPhase.Mains)
                {
                    ReturnStep began = ReturnSequence.BeginDeorbit(returnStep);
                    if (began != returnStep)
                    {
                        Debug.Log("[DragonScreen] conductor: DEORBIT authorised (crew cleared G15) - "
                                  + returnStep + " -> " + began);
                        returnStep = began; returnStepStartUT = now;
                    }
                }

                ReturnDecision d = ReturnSequence.Step(s, returnStep);
                if (d.Act != ReturnAct.None) PerformReturn(v, d.Act, s);

                if (d.Next != returnStep)
                {
                    Debug.Log("[DragonScreen] RETURN " + returnStep + " -> " + d.Next + "  (" + d.Reason
                              + ")  [range " + (s.RangeM / 1000.0).ToString("F2") + " km, alt "
                              + (s.AltitudeM / 1000.0).ToString("F1") + " km, "
                              + (s.Descending ? "descending" : "not descending") + "]");
                    returnStep = d.Next; returnStepStartUT = now;
                }
                returnNote = d.Reason;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor: return tick failed: " + e.Message);
            }
        }

        /// <summary>§B12.7 direct part control for the return. ⛔ No staging, no action groups.</summary>
        static void PerformReturn(Vessel v, ReturnAct act, ReturnInputs s)
        {
            switch (act)
            {
                // §B9 P6: "SmartASS (retrograde/target) for the backout". TARGET_MINUS points away from
                // the station, which is the direction the RCS translation has to push.
                case ReturnAct.BackAway:
                    if (core.SmartASS != null && v.targetObject != null)
                        SetSmartAssIfChanged(MuMech.MechJebModuleSmartASS.Target.TARGET_MINUS);
                    else if (core.SmartASS != null)
                        SetSmartAssIfChanged(MuMech.MechJebModuleSmartASS.Target.RETROGRADE);
                    Actuator.EnableRcs(v);
                    break;

                // ⛔ THE TRUNK DECOUPLER BY ROLE, NEVER THE DRAGON ONE. `Actuator.JettisonTrunk` fires
                // `DecouplerRole.TrunkJettison`; `SeparateDragon` fires `DragonSep` and is the ascent's.
                case ReturnAct.JettisonTrunk:
                    Actuator.JettisonTrunk(v);
                    break;

                case ReturnAct.PlanDeorbit:
                    PlanDeorbitNode(v, s);
                    break;

                case ReturnAct.BurnDeorbit:
                    BurnNode(v, ConductorAction.Of(ConductorVerb.Engage, ConductorModule.NodeExecutor,
                                                   ConductorOp.Periapsis, "deorbit burn"));
                    break;

                case ReturnAct.CloseNoseCone:
                    Actuator.CloseNoseShroud(v);
                    break;

                // §B10.5 / O8: "entry (P8) = surface_retrograde (heat-shield forward), attitude-hold
                // baseline - `force_roll` is NOT engaged at baseline (O8: pure ballistic, no commanded
                // bank; `force_roll` is reserved for the later off-target-steering increment)."
                case ReturnAct.HoldHeatShieldForward:
                    SetSmartAssIfChanged(MuMech.MechJebModuleSmartASS.Target.SURFACE_RETROGRADE);
                    break;

                // §B12.3's "chute triggering (the real 5486/1830 m constants already in
                // MissionPhase.cs)". ⚠ MechJeb's Landing Autopilot also has a `DeployChutes` flag
                // (§B10.4), and it is INERT here because nothing ever adds that module to a user pool -
                // so there is exactly one thing deploying chutes and it is this.
                case ReturnAct.DeployDrogues:
                    Actuator.DeployChutes(v, true);
                    break;
                case ReturnAct.DeployMains:
                    Actuator.DeployChutes(v, false);
                    break;

                // §B9 P10: "MechJeb done; conductor releases control."
                case ReturnAct.ReleaseControl:
                    StandDown(v, "splashdown - control released to the crew");
                    break;
            }
        }

        /// <summary>
        /// The deorbit node: `OperationPeriapsis` with the entry-corridor periapsis (§B10.2/§B12.3).
        /// ⛔ THE PERIAPSIS IS THE ONE ENGINEERING ESTIMATE IN THIS BATCH - see
        /// `pure/ReturnSequence.cs`'s header and T21's Q1. It is passed in rather than typed here.
        /// </summary>
        static void PlanDeorbitNode(Vessel v, ReturnInputs s)
        {
            try
            {
                MuMech.OperationPeriapsis pe = new MuMech.OperationPeriapsis();
                pe.NewPeA.Val = s.DeorbitPeriapsisM;
                Orbit o = v.orbit;
                List<MuMech.ManeuverParameters> nodes = pe.MakeNodes(o, Now(), core.Target);
                if (nodes == null || nodes.Count == 0)
                {
                    string why = pe.GetErrorMessage();
                    LogOnce("deorbit-plan-fail", "[DragonScreen] conductor: the deorbit burn would not "
                            + "plan" + (string.IsNullOrEmpty(why) ? "" : ": " + why));
                    return;
                }
                ClearNodes(v);
                for (int i = 0; i < nodes.Count; i++)
                    MuMech.VesselExtensions.PlaceManeuverNode(v, o, nodes[i].dV, nodes[i].UT);
                nodePlanned = true; nodeBurned = false; nodeExecuting = false;
                lastNodeDvLeft = 0.0; nodeResidualMps = 0.0;
                Debug.Log("[DragonScreen] conductor: deorbit node planned - target periapsis "
                          + (s.DeorbitPeriapsisM / 1000.0).ToString("F0")
                          + " km (⛔ a T21 ENGINEERING ESTIMATE, not a sourced value - see T21 Q1)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor: deorbit planning failed: " + e.Message);
            }
        }

        static void SetSmartAssIfChanged(MuMech.MechJebModuleSmartASS.Target tgt)
        {
            MuMech.MechJebModuleSmartASS sa = core == null ? null : core.SmartASS;
            if (sa == null || smartAssTarget == tgt) return;
            SetSmartAss(sa, tgt);
            Debug.Log("[DragonScreen] conductor: SmartASS -> " + tgt);
        }

        /// <summary>Is a trunk part still on this vessel? `VehicleParts.IsTrunk` is the classifier.</summary>
        static bool HasTrunk(Vessel v)
        {
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                    if (VehicleParts.IsTrunk(PartNames.Of(v.parts[i]))) return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Have the drogues (or mains) actually deployed? Read off the parts, not off a flag we set -
        /// the whole point of the re-command path is that a deploy can silently not happen.
        /// </summary>
        static bool ChutesOut(Vessel v, bool drogue)
        {
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    string nm = PartNames.Of(p);
                    if (drogue ? !VehicleParts.IsDrogues(nm) : !VehicleParts.IsMains(nm)) continue;
                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        PartModule pm = p.Modules[m];
                        // RealChute and the stock chute both expose a deployment state as a string
                        // field; neither type can be referenced here without a hard dependency, so the
                        // field is read by name and a miss is simply "not deployed yet".
                        BaseField f = pm.Fields["depState"];
                        if (f == null) continue;
                        string st = f.GetValue(pm) as string;
                        if (!string.IsNullOrEmpty(st)
                            && st.IndexOf("DEPLOYED", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        // ============================ STAND DOWN ============================

        /// <summary>
        /// Give the vehicle back: disengage the ascent module, then drop drive authority. ⭐ IN THAT
        /// ORDER — `OnModuleDisabled` deactivates the attitude controller and turns the thrust off,
        /// and it can only do that while the core is still master.
        /// </summary>
        static void StandDown(Vessel v, string why)
        {
            note = why;
            if (core == null) return;
            try
            {
                if (ascentEngaged)
                {
                    MuMech.MechJebModuleAscentBaseAutopilot ap = core.Ascent;
                    if (ap != null) ap.Users.Remove(Owner);
                    ascentEngaged = false;
                    Debug.Log("[DragonScreen] conductor: PVG ascent disengaged — " + why);
                }
                // T19: an on-orbit stand-down releases the Node Executor too, or it keeps the
                // attitude and thrust user pools on a core that is about to stop driving.
                if (nodeExecuting && core.Node != null) { core.Node.Abort(); nodeExecuting = false; }
                // T20: and the Docking Autopilot and SmartASS, for the same reason — both hold the
                // RCS/attitude user pools, and a pool held by a core that is no longer master is a
                // vehicle nobody is steering and nobody has given back.
                if (dockingEngaged) ReleaseDocking(why);
                if (rendezvousEngaged) ReleaseRendezvous(why);   // S219 - it holds the same user pools
                ReleaseAttitude();
                if (core.DriveAuthorized) core.AuthorizeDrive(false);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] MechConductor stand-down failed: " + e.Message);
            }
        }

        // ============================ READERS ============================

        static double Now()
        {
            try { return Planetarium.GetUniversalTime(); } catch { return 0.0; }
        }

        /// <summary>PVG's own "the insertion is flown" report — `PSGStatus.FINISHED`. THE SECO signal.</summary>
        static bool PvgFinished()
        {
            try
            {
                MuMech.MechJebModuleGuidanceController g = core.Guidance;
                return g != null && g.Status == MuMech.PSGStatus.FINISHED;
            }
            catch { return false; }
        }

        /// <summary>
        /// Usable propellant remaining in the FIRST STAGE, 0..1. Summed over the booster's OWN parts —
        /// deliberately not `GetConnectedResourceTotals`, which would fold the second stage's tanks in
        /// and never read empty. A resource counts only if it has mass (`density > 0`), so electric
        /// charge and ablator cannot hold the reading up.
        /// </summary>
        static double BoosterPropellantFraction(Vessel v)
        {
            double amount = 0.0, capacity = 0.0;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    if (!VehicleParts.IsBooster(PartNames.Of(p))) continue;
                    if (p.Resources == null) continue;
                    for (int r = 0; r < p.Resources.Count; r++)
                    {
                        PartResource res = p.Resources[r];
                        if (res == null || res.info == null || res.info.density <= 0f) continue;
                        amount += res.amount; capacity += res.maxAmount;
                    }
                }
            }
            catch { return 1.0; }

            // ⛔ FAIL FULL, NOT EMPTY. No booster tanks found means the booster is gone (post-sep) or
            // was never resolvable; reading that as 0 would command a MECO on a healthy vehicle.
            return capacity > 0.0 ? amount / capacity : 1.0;
        }

        /// <summary>Is a first-stage part still on this vessel? The resume's booster test.</summary>
        static bool HasBooster(Vessel v)
        {
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                    if (VehicleParts.IsBooster(PartNames.Of(v.parts[i]))) return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Every lit octaweb engine reports stock KSP's own <c>flameout</c>. A measured boolean, not a
        /// threshold — the second of `AscentSequence`'s two MECO signals.
        /// </summary>
        static bool BoosterFlamedOut(Vessel v)
        {
            bool anyLit = false;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    string nm = PartNames.Of(p);
                    if (!VehicleParts.IsBooster(nm)) continue;
                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        ModuleEngines e = p.Modules[m] as ModuleEngines;
                        if (e == null || !e.EngineIgnited) continue;
                        anyLit = true;
                        if (!e.flameout) return false;   // one healthy lit engine = not a flameout
                    }
                }
            }
            catch { return false; }
            return anyLit;
        }

        // ============================ THE GIMBAL-LIST REFRESH ============================

        /// <summary>
        /// ⚠ A HYPOTHESIS-DRIVEN MITIGATION, NOT A PROVEN FIX — the full argument is in this file's
        /// header. Rebuilds every <c>ModuleGimbal</c>'s engine list through stock KSP's own public
        /// <c>CreateEngineList()</c>, at the moments the part tree or the engine set has just changed
        /// under MechJeb's `VesselState.AnalyzeParts`. No vendored file is touched.
        /// </summary>
        static void RefreshGimbalEngineLists(Vessel v, string why)
        {
            int n = 0;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        ModuleGimbal g = p.Modules[m] as ModuleGimbal;
                        if (g == null) continue;
                        g.CreateEngineList();
                        n++;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] gimbal engine-list refresh failed (" + why + "): "
                                 + e.Message);
                return;
            }
            if (n > 0)
                Debug.Log("[DragonScreen] refreshed " + n + " gimbal engine list(s) after " + why
                          + " — the MechJeb VesselState.AnalyzeParts hazard T15d handed to T18.");
        }

        // ============================ BINDING ============================

        /// <summary>
        /// Find the ONE embedded core on the active vessel, once per vessel (§B12.7: "Resolve ONCE at
        /// the phase boundary into a named table; never re-search per frame"). A handover to a
        /// different vessel re-resolves and, finding no core, simply stops flying.
        /// </summary>
        // ================== S220: AUTO-TARGET THE STATION ON THE PAD ==================
        //
        //  Owner, 2026-09-07: "we also need to auto target the iss as soon as the vehicle is on the pad".
        //
        //  ⭐ WHY THIS IS NOT A CONVENIENCE. Three things already require a target and all three currently
        //  require the crew to remember: G7 goes NO-GO without a target in the same SoI (S215 Q4);
        //  `LaunchWindow` reads `Core.Target.TargetOrbit.LAN` and `.inclination`; and MECHJEB_MASTER_MAP
        //  §7.5's launch-to-plane reads the same orbit. No target, no window, no launch.
        //
        //  ⛔ SELECTS ON `VesselType.Station`, NEVER ON A NAME. §1.4 forbids inventing a mapping, and a
        //  name match ("ISS") breaks the moment the station is renamed. The vessel type is KSP's own
        //  classification. Verified in saves/test/persistent.sfs on 2026-09-07: exactly ONE Station
        //  ("ISS USOS Real Size") against 37 Crew, 13 Debris, 8 SpaceObject and 3 ServiceModule.
        //
        //  ⛔ REFUSES RATHER THAN GUESSES ON 0 OR 2+. This is S219 job 1's finding, one day old: a
        //  classifier that accepted any `.S1.` part picked up the SPENT UPPER STAGE, and "Select refuses to
        //  pick between two candidates, so a qualifying spent upper stage standing beside the real booster
        //  binds NEITHER". A silent wrong pick is worse than a refusal - and a refusal is already handled
        //  gracefully here, because G7 then goes NO-GO with a reason the crew can read.
        //
        //  ⛔ NEVER STOMPS A MANUAL SELECTION. If the crew has already chosen a target, that is the
        //  authority (§1.4) and this leaves it alone. It sets a target that is UNSET; it does not fight.
        //
        //  ⚠ PAD ONLY, AND ONCE. After separation the scene carries debris and a spent upper stage, and
        //  re-running a vessel scan mid-mission is exactly the shape of the defect S219 just fixed.
        static void AutoTargetStation(Vessel v)
        {
            if (stationTargetTried) return;
            if (v == null || core == null || core.Target == null) return;
            if (!FlightGlobals.ready) return;                       // a hold, not a release (S219)
            if (v.situation != Vessel.Situations.PRELAUNCH) return; // the pad, and nowhere else

            stationTargetTried = true;

            if (core.Target.NormalTargetExists)
            {
                Debug.Log("[DragonScreen] auto-target: a target is already set — leaving the crew's "
                        + "selection alone (S220).");
                return;
            }

            Vessel found = null;
            int n = 0;
            List<Vessel> all = FlightGlobals.Vessels;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                Vessel o = all[i];
                if (o == null || o == v) continue;
                if (o.vesselType != VesselType.Station) continue;
                n++;
                found = o;
            }

            if (n == 1)
            {
                core.Target.Set(found);
                Debug.Log("[DragonScreen] auto-target: station \"" + found.vesselName
                        + "\" targeted on the pad — the one VesselType.Station in the scene. (S220)");
                return;
            }

            Debug.LogWarning("[DragonScreen] auto-target REFUSED: " + n + " vessel(s) of "
                    + "VesselType.Station in the scene, and this picks only when there is exactly one. "
                    + "Target by hand — G7 will read NO-GO until you do. (S220)");
        }

        static void Bind(Vessel v)
        {
            if (core != null && boundVesselId == v.persistentId && core.vessel == v) return;

            core = null;
            boundVesselId = v.persistentId;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        DragonMechJebCore c = p.Modules[m] as DragonMechJebCore;
                        if (c == null) continue;
                        core = c;
                        Debug.Log("[DragonScreen] conductor bound the embedded MechJeb core on "
                                  + PartNames.Of(p) + " (" + v.vesselName + ")");
                        return;
                    }
                }
                Debug.LogWarning("[DragonScreen] conductor found NO embedded MechJeb core on "
                                 + v.vesselName + " — nothing will fly. Check the Dragon part's "
                                 + "MODULE { name = DragonMechJebCore } node.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor core bind failed: " + e.Message);
            }
        }
    }
}
