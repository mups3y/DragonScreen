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
            launchLatched = false; configured = false; ascentEngaged = false;
            note = "idle";
            ResetLeg();
            leg = RendezvousLeg.None;
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
                    Engage(v, a);
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
            leg = OnOrbit(s.Phase) ? RendezvousOps.LegFor(CrewProcedureOps.NextGateId)
                                   : RendezvousLeg.None;

            // ---- the measured rendezvous state ----
            double rangeM = 0.0, relSpeedMps = 0.0;
            Measure(v, out rangeM, out relSpeedMps);
            TrackOpeningRate(rangeM);

            double target = RendezvousOps.TargetRangeM(leg);
            s.InKeepOutSphere = RendezvousOps.InsideKeepOutSphere(rangeM);
            // ⛔ T20's, not T19's. Represented as an input the core reads and never as something the
            // conductor initiates (`pure/Conductor.cs` says so in its own header).
            s.ManualDockingRequested = false;
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
            s.PhaseComplete =
                (s.Phase == MissionPhase.Ascent && AscentSequence.CanAdvancePlan(ascentStep))
             || (OnOrbit(s.Phase) && RendezvousOps.LegComplete(leg, rangeM, relSpeedMps,
                                                               RendezvousOps.NulledRelativeSpeedMps));

            approachNote = leg == RendezvousLeg.None ? null
                         : leg.ToString() + " — " + (rangeM / 1000.0).ToString("F2") + " km, "
                           + relSpeedMps.ToString("F2") + " m/s";
            return s;
        }

        /// <summary>The two phases the on-orbit executor flies. `Docked` is T20/T21's.</summary>
        static bool OnOrbit(MissionPhase p)
        {
            return p == MissionPhase.Phasing || p == MissionPhase.Coast || p == MissionPhase.Approach;
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
                case ConductorModule.ManeuverPlanner:
                    PlanOperation(v, a);
                    return;
                case ConductorModule.NodeExecutor:
                    BurnNode(v, a);
                    return;
                case ConductorModule.DockingAutopilot:
                    StandDown(v, "no docking executor yet (T20) — " + a.Reason);
                    return;
                case ConductorModule.SmartAss:
                    StandDown(v, "no attitude executor yet (T20/T21) — " + a.Reason);
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

                TickAscentSequence(v);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] MechConductor ascent tick failed: " + e.Message);
            }
        }

        /// <summary>
        /// The three things T18 writes into MechJeb, and nothing else. Each carries its own authority;
        /// none of them is a tune. The full argument — including why this is profile-NEUTRAL and why
        /// T18 may not answer register S195 — is in `pure/AscentSequence.cs`'s header.
        /// </summary>
        static void Configure(Vessel v)
        {
            MuMech.MechJebModuleAscentSettings a = core.AscentSettings;
            if (a == null) { note = "the core has no AscentSettings module"; return; }

            // (1) §B8's owner directive, 2026-09-03. BOTH profiles ship this ON — RO's own
            //     `ApplyRODefaults()` has `Autostage = true;` and the shipped cfg has
            //     `_autostage = True` — so it must be written, and writing it is not a tune.
            //     ⛔ Set through the PROPERTY, never the `_autostage` field: the property is what
            //     removes the ascent autopilot from `Core.Staging.Users`, and the field alone would
            //     leave the StagingController still holding a user and still able to actuate.
            a.Autostage = false;

            // (2) §B8: "AscentType — CLASSIC(0)/PVG(1). Target PVG(1)." Already RO's default
            //     (`ApplyRODefaults` ends with `AscentType = AscentType.PSG`), asserted because a
            //     persisted craft or type value could still be CLASSIC.
            a.AscentType = MuMech.AscentType.PSG;

            // (2b) MechJeb's own ascent menu asserts this with the comment "this is mandatory for PSG"
            //      (MechJebModuleAscentMenu.cs:374). A no-op under every profile in this tree — RO's
            //      `LIMIT_QA_ENABLED_DEFAULT` is true and the shipped cfg has `LimitQaEnabled = True` —
            //      kept as a belt against a persisted false. ⚠ The LimitQa VALUE is a tune and is NOT
            //      touched; only the enable, which the source calls mandatory.
            a.LimitQaEnabled = true;

            // (3) §B5's named exception: the destination is a MISSION FACT, not a tune. The SIGN of the
            //     inclination is preserved from whatever is loaded — see `AscentTargets.For`.
            AscentTarget t = AscentTargets.For(CrewProcedureOps.Profile, a.DesiredInclination.Val);
            a.DesiredInclination.Val   = t.InclinationDeg;
            a.DesiredOrbitAltitude.Val = t.PeriapsisM;
            a.DesiredApoapsis.Val      = t.ApoapsisM;

            configured = true;
            Debug.Log("[DragonScreen] conductor: PVG configured — autostage OFF (§B8), AscentType PSG, "
                      + "target " + (t.PeriapsisM / 1000.0).ToString("F0") + " x "
                      + (t.ApoapsisM / 1000.0).ToString("F0") + " km @ "
                      + t.InclinationDeg.ToString("F4") + "° "
                      + (t.FromProfileApsides ? "(mission apsides)" : "(standard ISS insertion)")
                      + ". ⚠ No ascent-SHAPING value was written: pitch rate, pitch-start velocity, "
                      + "LimitQa, MaxAoA and the attitude PID are whatever the loaded profile set "
                      + "(register S195 — which profile flight 1 flies is a §B5/T22 question).");
        }

        static void TickAscentSequence(Vessel v)
        {
            double now = Now();

            AscentInputs s = AscentInputs.Nominal();
            s.LaunchCommanded = launchLatched;
            s.SinceStepS = now - stepStartUT;

            double thrust, max; int lit;
            Actuator.EngineThrust(v, EngineRole.OctawebAll, out thrust, out max, out lit);
            s.S1ThrustN = thrust; s.S1MaxThrustN = max; s.S1LitCount = lit;
            s.S1PropellantFrac = BoosterPropellantFraction(v);
            s.S1Flameout = BoosterFlamedOut(v);

            Actuator.EngineThrust(v, EngineRole.SecondStage, out thrust, out max, out lit);
            s.S2ThrustN = thrust; s.S2LitCount = lit;

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
