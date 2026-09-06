/*
 * MissionWalkTest — the T18..T21 batch, 2026-09-07.
 *
 * ONE MISSION, WALKED END TO END, WITHOUT THE GAME. Every other suite in this tree proves one decision in
 * isolation. This one composes them the way the flight will: the crew gates advance the plan, the plan
 * hands a phase to `Conductor.Decide`, the decision hands work to that phase's sequencer, the sequencer
 * commands a VEHICLE, and the vehicle answers back. The point is the loop — a chain of individually
 * correct decisions can still fail to terminate, and a flight is a very expensive place to find that out.
 *
 * ---- ⭐ IT IS CLOSED-LOOP, WHICH IS WHY IT IS WORTH MORE THAN A DRIVE ----
 * `Rocket` below is a crude but HONEST vehicle: engines take time to spool, propellant drains, a
 * pressure-fed MVac refuses its first ignition command, PVG finishes when it has burned long enough, and
 * a decoupled booster stops answering. The sequencer is never told what to do next — it is told what the
 * vehicle IS, and it has to decide. So this suite can fail in the two ways a real flight fails: it can
 * end in the wrong state, and it can never end at all.
 *
 * ---- ⛔ WHAT IT IS NOT, SAID PLAINLY (the same caveat `ConductorWalkTest` carries) ----
 * This is a CONTRACT test over the pure layer, not an execution of the glue. `src/CrewProcedureOps.cs`
 * and `src/MechConductor.cs` take a live `Vessel`, so no headless suite can run them; what is walked here
 * is the COMPOSITION they perform, in their own order. If either glue file is later changed so that it no
 * longer composes these pieces this way, this suite will still pass — read the glue. And `Rocket` is a
 * fixture, not RSS-RO: green here says the LOGIC closes, never that the trajectory does.
 *
 * ---- HOW IT GROWS ----
 * T18 walks the countdown gates and the ascent. T19/T20/T21 each append their own leg to `Run()` and
 * their own responses to `Rocket`, so by the end of the batch this is one continuous mission.
 */
using System;
using DragonScreen;

public static class MissionWalkTest
{
    static int checks = 0, failures = 0;
    static void Check(bool ok, string what)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL: " + what); } }

    public static int Run()
    {
        Console.WriteLine("MissionWalkTest (T18-T21: one mission, gates + phases + vehicle, closed-loop)");
        checks = 0; failures = 0;

        WalkTheMission();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    // ============================================================================================
    //  THE VEHICLE — crude, but it answers back
    // ============================================================================================
    class Rocket
    {
        public double T;                         // seconds since the walk started
        public bool Clamped = true;              // hold-downs
        public bool BoosterGone, DragonSeparated, NoseOpen;
        public bool S1Commanded, S1Shut;
        public double S1Prop = 1.0;              // 0..1
        public double S1Thrust;                  // N
        public const double S1Max = 7600000.0;   // a Falcon-9-scale number
        public bool S2Commanded;
        public double S2Thrust;
        public int S2IgnitionAttempts;
        public double S2BurnS;
        public bool PvgFinished;
        public bool ClampReleasedWhileCold;      // ⛔ the pad failure this walk must never produce

        // ---- how long the first stage burns, and how long PVG takes. Fixture values, and they are
        // ---- deliberately NOT the documented MET numbers: nothing in the sequencer may depend on them.
        const double S1BurnS = 137.0;
        const double S2NeedS = 361.0;

        public void Apply(AscentAct a)
        {
            switch (a)
            {
                case AscentAct.IgniteStageOne:   S1Commanded = true; break;
                case AscentAct.ReleaseHoldDowns:
                    if (S1Thrust < 0.5 * S1Max) ClampReleasedWhileCold = true;
                    Clamped = false;
                    break;
                case AscentAct.ShutdownStageOne: S1Shut = true; break;
                case AscentAct.SeparateBooster:  BoosterGone = true; break;
                // ⭐ A PRESSURE-FED ENGINE REFUSES THE FIRST COMMAND. This is the whole reason
                // `Actuator.IgniteSecondStage` is written to be called every tick.
                case AscentAct.IgniteStageTwo:
                    S2IgnitionAttempts++;
                    if (S2IgnitionAttempts >= 3) S2Commanded = true;
                    break;
                case AscentAct.SeparateDragon:   DragonSeparated = true; break;
                case AscentAct.OpenNoseCone:     NoseOpen = true; break;
                case AscentAct.SafeAbort:        S1Commanded = false; S1Shut = true; break;
            }
        }

        public void Advance(double dt)
        {
            T += dt;

            // stage one: spools in ~0.4 s, drains while lit and unshut, dies when shut
            if (S1Commanded && !S1Shut && !BoosterGone && S1Prop > 0.0)
            {
                S1Thrust = Math.Min(S1Max, S1Thrust + S1Max * dt / 0.4);
                if (!Clamped || S1Thrust >= S1Max) S1Prop = Math.Max(0.0, S1Prop - dt / S1BurnS);
            }
            else
            {
                S1Thrust = Math.Max(0.0, S1Thrust - S1Max * dt / 0.5);
            }
            if (S1Prop <= 0.0 && !S1Shut) S1Thrust = 0.0;   // flamed out

            // stage two
            if (S2Commanded && !DragonSeparated)
            {
                S2Thrust = Math.Min(981000.0, S2Thrust + 981000.0 * dt / 0.3);
                S2BurnS += dt;
                if (S2BurnS >= S2NeedS) PvgFinished = true;
            }
        }

        public AscentInputs Sense(bool launchGo, double sinceStep)
        {
            AscentInputs s = AscentInputs.Nominal();
            s.LaunchCommanded = launchGo;
            s.SinceStepS = sinceStep;
            s.S1MaxThrustN = BoosterGone ? 0.0 : S1Max;
            s.S1ThrustN    = BoosterGone ? 0.0 : S1Thrust;
            s.S1LitCount   = (!BoosterGone && S1Commanded && !S1Shut && S1Prop > 0.0) ? 9 : 0;
            // ⛔ Mirrors `MechConductor.BoosterPropellantFraction`'s FAIL-FULL rule: with no booster
            // tanks left to read, the fraction is 1.0, never 0.
            s.S1PropellantFrac = BoosterGone ? 1.0 : S1Prop;
            s.S1Flameout   = !BoosterGone && S1Commanded && !S1Shut && S1Prop <= 0.0;
            s.S2ThrustN    = S2Thrust;
            s.S2LitCount   = S2Thrust > 0.0 ? 1 : 0;
            s.PvgFinished  = PvgFinished;
            return s;
        }
    }

    // ============================================================================================
    //  THE WALK
    // ============================================================================================
    static void WalkTheMission()
    {
        MissionProfile m = Missions.Resolve("Crew-2");
        Check(m.Valid, "the walk flies a real catalog mission (Crew-2)");
        MissionStep[] plan = ModeManager.Plan(m);
        Check(plan.Length > 10, "...against the real mission plan (" + plan.Length + " steps)");

        int index = 0;

        // ---- 1. THE COUNTDOWN. Seven gates, seven crew GOs, and nothing flies through one. --------
        int gatesCleared = 0;
        bool launchGo = false;
        for (int guard = 0; guard < 40 && index < plan.Length; guard++)
        {
            if (plan[index].Kind != StepKind.Gate) break;
            GateId id = plan[index].Gate;
            Gate g = CrewGates.ById(m, id);

            // ⛔ A GO ON AN UNSATISFIED CHECKLIST IS DISCARDED. Prove it on the FIRST gate before
            // satisfying anything: the crew cannot skip the list by pressing the button.
            if (gatesCleared == 0 && g.Items != null && g.Items.Length > 0)
            {
                bool[] none = new bool[g.Items.Length];
                CrewGateInputs bad;
                bad.Gate = g; bad.Satisfied = none;
                bad.GoPressed = true; bad.NoGoPressed = false; bad.AbortPressed = false;
                Check(!CrewGate.Step(bad, GatePhase.Holding).Cleared,
                      "⛔ a GO on an unsatisfied checklist does not clear the gate");
            }

            bool[] sat = new bool[g.Items == null ? 0 : g.Items.Length];
            for (int i = 0; i < sat.Length; i++) sat[i] = true;   // auto-items + the crew's taps
            CrewGateInputs gi;
            gi.Gate = g; gi.Satisfied = sat;
            gi.GoPressed = true; gi.NoGoPressed = false; gi.AbortPressed = false;
            CrewGateStep st = CrewGate.Step(gi, GatePhase.Holding);
            Check(st.Cleared, "gate " + id + " clears on a GO over a satisfied checklist");

            if (id == GateId.LaunchGoG7) launchGo = true;   // the ignition INTENT the host latches
            index = ModeManager.Advance(plan, index, new ModeInputs { GateGo = true }).Index;
            gatesCleared++;
        }
        Check(gatesCleared == 7, "the countdown is seven gates, G1..G7 (got " + gatesCleared + ")");
        Check(launchGo, "and G7 raised the launch GO");
        Check(index < plan.Length && plan[index].Kind == StepKind.Fly
              && plan[index].Phase == MissionPhase.Ascent,
              "the plan is now at the ASCENT fly step");

        // ---- 2. THE ASCENT, CLOSED-LOOP. --------------------------------------------------------
        Rocket r = new Rocket();
        AscentStep at = AscentStep.Idle;
        double stepStart = 0.0;
        const double dt = 0.1;
        int ticks = 0, actuations = 0;
        bool everAdvanced = false;
        int offPvg = 0;   // ticks on which the core did not ask for PVG
        double sepThrust = -1.0;      // stage-1 thrust at the instant the decoupler fired

        // ⛔ The conductor is asked for a decision every tick, exactly as `MechConductor.Tick` does.
        for (; ticks < 20000; ticks++)
        {
            // (a) the pure mission core decides what this phase is for
            ConductorInputs ci = new ConductorInputs();
            ci.Phase = MissionPhase.Ascent;
            ci.PhaseComplete = AscentSequence.CanAdvancePlan(at);
            ConductorAction ca = Conductor.Decide(ci);

            if (ca.Verb == ConductorVerb.Advance)
            {
                everAdvanced = true;
                index = ModeManager.Advance(plan, index, new ModeInputs { PhaseComplete = true }).Index;
                break;
            }
            // ⛔ Counted, not asserted per tick: a check inside a 4000-tick loop would drown the suite's
            // own count and make a single real failure invisible in the total. Asserted once, below.
            if (ca.Verb != ConductorVerb.Engage || ca.Module != ConductorModule.AscentPvg) offPvg++;

            // (b) the ascent sequencer decides what to actuate
            AscentInputs s = r.Sense(launchGo, r.T - stepStart);
            AscentDecision d = AscentSequence.Step(s, at);
            if (d.Act != AscentAct.None)
            {
                if (d.Act == AscentAct.SeparateBooster) sepThrust = r.S1Thrust;
                r.Apply(d.Act);
                actuations++;
            }
            if (d.Next != at) { at = d.Next; stepStart = r.T; }

            // (c) the vehicle answers
            r.Advance(dt);
        }

        Check(offPvg == 0, "every tick of the ascent asked for PVG and nothing else (" + offPvg
              + " tick(s) did not)");
        Check(everAdvanced, "the ascent TERMINATES and the conductor advances the plan "
              + "(ended at " + at + " after " + ticks + " ticks)");
        Check(at == AscentStep.Complete, "...from AscentStep.Complete, not from anywhere else");

        // ---- 3. WHAT THE VEHICLE LOOKS LIKE AFTERWARDS ------------------------------------------
        Check(!r.ClampReleasedWhileCold,
              "⛔ the hold-downs were NEVER released on a cold stage");
        Check(!r.Clamped, "the hold-downs did release once thrust was good");
        Check(r.BoosterGone, "the booster separated");
        Check(sepThrust == 0.0,
              "⛔ and it separated at ZERO stage-1 thrust, never under power (flight 194334) — "
              + "thrust at sep was " + sepThrust + " N");
        Check(r.S2IgnitionAttempts >= 3,
              "the MVac needed " + r.S2IgnitionAttempts + " ignition commands and got every one of them "
              + "(the RealFuels settle/retry cycle)");
        Check(r.PvgFinished, "PVG flew the second stage to its finish");
        Check(r.DragonSeparated, "the Dragon separated from S2");
        Check(r.NoseOpen, "the nose cone opened");
        Check(actuations == 9,
              "nine commands were issued across the ascent — seven distinct events plus the two refused "
              + "MVac ignitions (got " + actuations + ")");

        // ---- 4. AND THE PLAN IS ON THE NEXT LEG -------------------------------------------------
        Check(index < plan.Length, "the plan did not run off its end at insertion");
        Check(index < plan.Length && plan[index].Kind == StepKind.Fly
              && plan[index].Phase == MissionPhase.Phasing,
              "the plan is now at PHASING — the outbound rendezvous leg (T19's)");

        // ---- 5. ON INTO THE RENDEZVOUS (T19) ----------------------------------------------------
        WalkTheRendezvous(plan, ref index);
    }
    // ============================================================================================
    //  THE CHASE — a station to close on, and a planner that does not always hit what it aims at
    // ============================================================================================
    // ⭐ THE MISS IS THE POINT. A fixture whose burns always land exactly where they were aimed can
    // never exercise §B12.4, which is the ONE genuinely new piece of logic in the whole rendezvous
    // (§B12.4: "the main NEW logic"). So `Miss` starts at 1.6 — every planned intercept overshoots by
    // 60% — and tightens only when the conductor re-plans, which is how a real solver behaves and is
    // exactly the loop that has to be proved to converge rather than to oscillate.
    class Chase
    {
        public double RangeM = 250000.0;      // behind and below, straight out of insertion
        public double RelSpeedMps = 120.0;
        public bool NodeExists, NodeBurned;
        public double AimedAtM;               // the intercept this node was planned for
        public double Miss = 1.6;             // how badly the planner is missing, this pass
        public int Planned, Burned, Replans;

        public void Plan(double interceptM)
        { NodeExists = true; NodeBurned = false; AimedAtM = interceptM; Planned++; }

        /// <summary>What §B12.4 measures: the predicted closest approach against what was aimed at.</summary>
        public double ClosestApproachErrM
        { get { return NodeExists ? AimedAtM * (Miss - 1.0) : 0.0; } }

        // ⛔ `NodeExists` STAYS TRUE. It is a LATCH on "a node was built for this step", which is the
        // only reading under which `Conductor.PlanThenBurn` can ever reach Advance — the Node Executor
        // deletes the node the moment the burn ends, so a live count would send the core back to
        // planning the same operation forever. `src/MechConductor.cs` latches it the same way, and says
        // so at the point it does. This walk is what found it.
        public void Burn(ConductorOp op)
        {
            NodeBurned = true; Burned++;
            switch (op)
            {
                case ConductorOp.MatchPlane:                       break;   // plane only, no range change
                case ConductorOp.Transfer:
                case ConductorOp.CourseCorrection:
                    double got = AimedAtM * Miss;
                    if (got < RangeM) RangeM = got;                          // never further away
                    break;
                case ConductorOp.KillRelVel:  RelSpeedMps = 0.05;  break;   // velocities matched
                case ConductorOp.Circularize:                      break;   // shapes the orbit, not the range
            }
        }

        /// <summary>A re-plan is a better solution, not the same one again — otherwise the loop could
        /// only ever oscillate, and a test that cannot converge proves nothing about one that does.</summary>
        public void Replan()
        {
            NodeExists = false; NodeBurned = false; Replans++;
            Miss = 1.0 + (Miss - 1.0) * 0.35;
        }
    }

    // ---- 5. THE RENDEZVOUS, CLOSED-LOOP, FROM INSERTION TO THE KEEP-OUT SPHERE ------------------
    // T19's DONE-when is "rendezvous to the KOS in-sim". This is everything about that which is
    // decidable without the game: that the phasing leg raises G9, that each approach leg terminates
    // and raises its own gate, that the intercept ladder actually walks down, that §B12.4 re-plans a
    // missed burn WITHOUT advancing the chain past it, and that the whole thing ENDS at the Keep-Out
    // Sphere by asking for the Docking Autopilot — which is T20's, not T19's.
    static void WalkTheRendezvous(MissionStep[] plan, ref int index)
    {
        Chase c = new Chase();
        int stepsDone = 0, passes = 0, gatesCleared = 0, dockingHandoffs = 0;
        double lastIntercept = double.MaxValue;
        int ladderRises = 0, replanAdvancedChain = 0, plannedInsideKos = 0;
        RendezvousLeg reachedKosOn = RendezvousLeg.None, lastLeg = RendezvousLeg.None;
        MissionPhase endedOn = MissionPhase.Unknown;
        bool ended = false;

        for (int tick = 0; tick < 4000; tick++)
        {
            if (index >= plan.Length) break;

            // ---- a GATE: the crew work the checklist and press GO, as in the countdown -----------
            if (plan[index].Kind == StepKind.Gate)
            {
                Gate g = CrewGates.ById(Missions.Resolve("Crew-2"), plan[index].Gate);
                bool[] sat = new bool[g.Items == null ? 0 : g.Items.Length];
                for (int i = 0; i < sat.Length; i++) sat[i] = true;
                CrewGateInputs gi;
                gi.Gate = g; gi.Satisfied = sat;
                gi.GoPressed = true; gi.NoGoPressed = false; gi.AbortPressed = false;
                if (!CrewGate.Step(gi, GatePhase.Holding).Cleared) { Check(false, "gate " + g.Id + " would not clear"); break; }
                index = ModeManager.Advance(plan, index, new ModeInputs { GateGo = true }).Index;
                gatesCleared++;
                stepsDone = 0; passes = 0; lastIntercept = double.MaxValue;
                continue;
            }

            // ---- a FLY step. Which leg? The gate it walks toward, exactly as the glue does. ------
            MissionPhase phase = plan[index].Phase;
            if (phase != MissionPhase.Phasing && phase != MissionPhase.Approach)
            { endedOn = phase; ended = true; break; }

            GateId next = GateId.None;
            for (int i = index; i < plan.Length; i++)
                if (plan[i].Kind == StepKind.Gate) { next = plan[i].Gate; break; }
            RendezvousLeg leg = RendezvousOps.LegFor(next);
            if (leg != RendezvousLeg.None) lastLeg = leg;

            ConductorInputs s = new ConductorInputs();
            s.Phase = phase;
            s.NodeExists = c.NodeExists;
            s.NodeBurned = c.NodeBurned;
            s.ApproachStepsDone = stepsDone;
            s.InKeepOutSphere = RendezvousOps.InsideKeepOutSphere(c.RangeM);
            s.ClosestApproachTolM = RendezvousOps.ClosestApproachToleranceM(leg);
            s.ClosestApproachErrM = c.ClosestApproachErrM;
            s.NodeResidualTolMps = RendezvousOps.NodeResidualToleranceFor(leg);
            s.PhaseComplete = RendezvousOps.LegComplete(leg, c.RangeM, c.RelSpeedMps,
                                                        RendezvousOps.NulledRelativeSpeedMps);
            ConductorAction a = Conductor.Decide(s);

            if (a.Verb == ConductorVerb.Advance)
            {
                if (s.PhaseComplete)
                {
                    index = ModeManager.Advance(plan, index, new ModeInputs { PhaseComplete = true }).Index;
                    stepsDone = 0; passes = 0; lastIntercept = double.MaxValue;
                    c.NodeExists = false; c.NodeBurned = false;
                    continue;
                }
                // a CHAIN-level advance
                c.NodeExists = false; c.NodeBurned = false;
                if (leg != RendezvousLeg.Phasing && stepsDone < Conductor.ApproachChain.Length)
                { stepsDone++; continue; }
                if (RendezvousOps.OnChainComplete(leg) == RendezvousOps.ChainEnd.CompleteLeg)
                {
                    index = ModeManager.Advance(plan, index, new ModeInputs { PhaseComplete = true }).Index;
                    stepsDone = 0; passes = 0; lastIntercept = double.MaxValue;
                    continue;
                }
                stepsDone = 0; passes++; lastIntercept = double.MaxValue;
                continue;
            }

            if (a.Verb == ConductorVerb.Replan)
            {
                int before = stepsDone;
                c.Replan();
                // ⛔ §B12.4 / `Conductor`: "a step whose burn missed is not a step that is done".
                if (stepsDone != before) replanAdvancedChain++;
                continue;
            }

            if (a.Verb == ConductorVerb.Engage && a.Module == ConductorModule.DockingAutopilot)
            {
                dockingHandoffs++;
                if (reachedKosOn == RendezvousLeg.None) reachedKosOn = leg;
                ended = true; break;                     // ⭐ T19's terminal: the KOS. T20 takes it on.
            }

            if (a.Verb == ConductorVerb.Engage && a.Module == ConductorModule.ManeuverPlanner)
            {
                if (s.InKeepOutSphere) plannedInsideKos++;
                double d = a.Op == ConductorOp.CourseCorrection || a.Op == ConductorOp.Transfer
                         ? RendezvousOps.InterceptDistanceM(leg, c.RangeM, passes)
                         : c.RangeM;
                if (a.Op == ConductorOp.CourseCorrection)
                {
                    if (d > lastIntercept) ladderRises++;
                    lastIntercept = d;
                }
                c.Plan(d);
                continue;
            }

            if (a.Verb == ConductorVerb.Engage && a.Module == ConductorModule.NodeExecutor)
            { c.Burn(a.Op); continue; }

            Check(false, "the rendezvous reached an unexpected decision: " + a);
            break;
        }

        Check(ended, "the rendezvous TERMINATES (it did not run out of ticks)");
        Check(endedOn == MissionPhase.Docked,
              "⭐ it ends by handing the plan to the DOCKED step — the leg after G12 'HOLD — WP2 (20 m) "
              + "— GO FOR DOCKING', which is T20's (got " + endedOn + ")");
        Check(lastLeg == RendezvousLeg.ToWaypoint2,
              "...having walked all the way in to the WP2 leg (got " + lastLeg + ")");
        Check(c.RangeM <= RendezvousOps.KeepOutSphereM,
              "⭐ T19's OWN DONE-WHEN: the range is inside the 200 m Keep-Out Sphere when the conductor "
              + "hands over (got " + c.RangeM.ToString("F0") + " m)");
        Check(gatesCleared >= 4,
              "G9, G10, G11 and G12 were all worked on the way in (got " + gatesCleared + ")");
        Check(c.Replans > 0,
              "⭐ §B12.4 ACTUALLY FIRED — the planner missed and the conductor re-planned "
              + c.Replans + " time(s) rather than flying a burn it knew was wrong");
        Check(replanAdvancedChain == 0,
              "⛔ and a re-plan NEVER advanced the chain past the step whose burn missed");
        Check(ladderRises == 0,
              "⛔ the intercept ladder never aimed further out than the pass before it within a leg");
        Check(plannedInsideKos == 0,
              "⛔ NO transfer burn was ever planned inside the Keep-Out Sphere — `Conductor` tests the "
              + "hand-off BEFORE the operation chain, and this is what that ordering is for");
        Check(dockingHandoffs == 0,
              "⛔ and the Docking Autopilot was never engaged on the way in: the approach ARRIVES at "
              + "WP2 and raises the docking gate, which is the real operating concept (§B14.2). The "
              + "hand-off is the SAFETY path for being inside the KOS with the leg unfinished — proved "
              + "directly below, not by hoping this walk stumbles into it");

        // ⭐ THE HAND-OFF, PROVED DIRECTLY. It is a SAFETY property, not a nominal one: if the vehicle
        // is inside the Keep-Out Sphere on an unfinished approach leg, the planner chain must stop dead
        // and the Docking Autopilot must take it (O6 / §B10.3 / §B12.3). A nominal walk arrives at WP2
        // and never exercises it, so it is asserted here rather than left to chance.
        {
            ConductorInputs kos = new ConductorInputs();
            kos.Phase = MissionPhase.Approach;
            kos.ApproachStepsDone = 1;                       // mid-chain, deliberately
            kos.InKeepOutSphere = RendezvousOps.InsideKeepOutSphere(120.0);
            kos.ClosestApproachErrM = 5000.0;                // and badly off, deliberately
            kos.ClosestApproachTolM = RendezvousOps.ClosestApproachToleranceM(RendezvousLeg.ToWaypoint1);
            ConductorAction ka = Conductor.Decide(kos);
            Check(ka.Verb == ConductorVerb.Engage && ka.Module == ConductorModule.DockingAutopilot,
                  "⭐ inside the KOS mid-chain: the DOCKING AUTOPILOT, not another transfer burn (got "
                  + ka + ")");
            Check(RendezvousOps.InsideKeepOutSphere(120.0) && !RendezvousOps.InsideKeepOutSphere(220.0),
                  "...and 120 m is inside the sphere while 220 m (WP1) is outside it");
        }

        Check(c.Burned > 0 && c.Planned > 0,
              "the chain planned " + c.Planned + " node(s) and flew " + c.Burned);
    }

}
