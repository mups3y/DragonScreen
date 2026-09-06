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

        // ---- 6. AND ON INTO THE DOCKING (T20) ---------------------------------------------------
        WalkTheDocking(plan, ref index);

        // ---- 7. AND HOME (T21) ------------------------------------------------------------------
        WalkTheReturn(plan, ref index);
    }

    // ---- 7. UNDOCK, DEPART, DEORBIT, ENTER, SPLASH ------------------------------------------------
    // T21's DONE-when is "return + splash in-sim". This walks the whole return closed-loop against a
    // capsule that answers back, and asserts the four things that would end a mission:
    //   ⛔ the trunk is never fired while docked, and never before the vehicle is clear;
    //   ⛔ the deorbit is entered ONLY through the crew's G15 GO, never by walking;
    //   ⛔ the chutes come out on the way DOWN, in order, at the real altitudes;
    //   ⛔ and the whole thing TERMINATES, with control released.
    static void WalkTheReturn(MissionStep[] plan, ref int index)
    {
        MissionProfile m = Missions.Resolve("Crew-2");

        // ⭐ THE CREW PRESS UNDOCK. In the glue that is `CrewProcedureOps.MarkDockedThisMission`, which
        // DISENGAGES AUTO SEQUENCE; the next engage resumes at the departure step past G14. Here the
        // same jump is made directly, which is what that resume computes.
        int g14 = -1;
        for (int i = 0; i < plan.Length; i++)
            if (plan[i].Kind == StepKind.Gate && plan[i].Gate == GateId.UndockGoG14) g14 = i;
        Check(g14 >= 0, "the plan has a G14 undock gate");
        int departure = -1;
        for (int j = g14 + 1; j < plan.Length && g14 >= 0; j++)
            if (plan[j].Kind == StepKind.Fly) { departure = j; break; }
        Check(departure >= 0 && plan[departure].Phase == MissionPhase.Phasing,
              "...and the step after it is the departure Phasing leg");
        if (departure < 0) return;
        index = departure;

        // ⛔ The crew have ALREADY undocked by the time this leg runs — see `Capsule`.
        Capsule c = new Capsule();
        ReturnStep at = ReturnStep.Idle;
        int ticks = 0, gatesCleared = 0, trunkWhileDocked = 0, trunkWhileClose = 0;
        int deorbitBeforeGo = 0, chuteWhileClimbing = 0;
        bool go15 = false, released = false;
        ReturnAct[] order = new ReturnAct[24]; int acts = 0;

        for (; ticks < 20000 && index < plan.Length; ticks++)
        {
            if (plan[index].Kind == StepKind.Gate)
            {
                Gate g = CrewGates.ById(m, plan[index].Gate);
                bool[] sat = new bool[g.Items == null ? 0 : g.Items.Length];
                for (int i = 0; i < sat.Length; i++) sat[i] = true;
                CrewGateInputs gi;
                gi.Gate = g; gi.Satisfied = sat;
                gi.GoPressed = true; gi.NoGoPressed = false; gi.AbortPressed = false;
                if (!CrewGate.Step(gi, GatePhase.Holding).Cleared)
                { Check(false, "gate " + g.Id + " would not clear"); break; }
                if (g.Id == GateId.DeorbitGoG15) go15 = true;
                index = ModeManager.Advance(plan, index, new ModeInputs { GateGo = true }).Index;
                gatesCleared++;
                continue;
            }

            MissionPhase phase = plan[index].Phase;

            // The crew's G15 GO arrives as the plan reaching the Entry phase — the glue's own rule.
            if (phase == MissionPhase.Entry || phase == MissionPhase.Drogues
                || phase == MissionPhase.Mains)
            {
                ReturnStep began = ReturnSequence.BeginDeorbit(at);
                if (began != at && !go15) deorbitBeforeGo++;
                at = began;
            }

            ReturnInputs s = c.Sense();
            ReturnDecision d = ReturnSequence.Step(s, at);

            if (d.Act == ReturnAct.JettisonTrunk)
            {
                if (c.Docked) trunkWhileDocked++;
                if (c.RangeM < RendezvousOps.ApproachEllipsoidM) trunkWhileClose++;
            }
            if ((d.Act == ReturnAct.DeployDrogues || d.Act == ReturnAct.DeployMains) && !c.Descending)
                chuteWhileClimbing++;
            if (d.Act == ReturnAct.ReleaseControl) released = true;

            if (d.Act != ReturnAct.None)
            {
                if (acts == 0 || order[acts - 1] != d.Act)
                { if (acts < order.Length) { order[acts] = d.Act; acts++; } }
                c.Apply(d.Act);
            }
            at = d.Next;

            // the plan advances when this leg reports itself finished
            bool legDone =
                (phase == MissionPhase.Phasing && ReturnSequence.DepartureComplete(at))
             || ((phase == MissionPhase.Drogues || phase == MissionPhase.Mains
                  || phase == MissionPhase.Splashdown) && ReturnSequence.ReturnComplete(at))
             || (phase == MissionPhase.Entry && (at == ReturnStep.Drogues || at == ReturnStep.Mains
                                                 || at == ReturnStep.Complete));
            if (legDone)
            { index = ModeManager.Advance(plan, index, new ModeInputs { PhaseComplete = true }).Index; }

            c.Advance();
        }

        Check(released, "⭐ T21's OWN DONE-WHEN: the return reaches splashdown and RELEASES control "
              + "(ended at " + at + " after " + ticks + " ticks)");
        Check(at == ReturnStep.Complete, "...from ReturnStep.Complete");
        Check(index >= plan.Length, "and the mission plan is finished (step " + index + " of "
              + plan.Length + ")");
        Check(gatesCleared >= 1, "the G15 deorbit poll was worked on the way (got " + gatesCleared + ")");

        Check(trunkWhileDocked == 0,
              "⛔ the trunk decoupler was NEVER commanded while hard-mated (" + trunkWhileDocked + ")");
        Check(trunkWhileClose == 0,
              "⛔ ...nor inside §B11's 4 km Approach Ellipsoid (" + trunkWhileClose + ")");
        Check(deorbitBeforeGo == 0,
              "⛔ the deorbit was never entered before the crew's G15 GO (" + deorbitBeforeGo + ")");
        Check(chuteWhileClimbing == 0,
              "⛔ no chute was ever commanded while not descending (" + chuteWhileClimbing + ")");
        Check(c.DroguesOut && c.MainsOut, "both chute stages deployed");
        Check(c.DrogueAltM <= Mission.DrogueAltitude && c.DrogueAltM > Mission.MainAltitude,
              "the drogues came out at/below 5486 m and above the main altitude (got "
              + c.DrogueAltM.ToString("F0") + " m)");
        Check(c.MainAltM <= Mission.MainAltitude,
              "the mains came out at/below 1830 m (got " + c.MainAltM.ToString("F0") + " m)");

        ReturnAct[] want = {
            ReturnAct.BackAway, ReturnAct.JettisonTrunk, ReturnAct.PlanDeorbit, ReturnAct.BurnDeorbit,
            ReturnAct.CloseNoseCone, ReturnAct.HoldHeatShieldForward, ReturnAct.DeployDrogues,
            ReturnAct.DeployMains, ReturnAct.ReleaseControl
        };
        Check(acts == want.Length, "the return issued " + want.Length + " distinct commands in order "
              + "(got " + acts + ")");
        for (int i = 0; i < want.Length && i < acts; i++)
            Check(order[i] == want[i], "return command " + (i + 1) + " is " + want[i]
                  + " (got " + order[i] + ")");

        // ⭐ THE ABORT, ASSERTED AS THE HONEST NO-OP IT STILL IS. T21's title includes "abort wiring";
        // §B13.4 routes it through `AbortControl`, which is register W19 and W19 is HELD. So the core
        // can DECIDE an abort and the glue's only response is to hand the vehicle back — and the abort
        // decision must still outrank everything, including a gate hold, so it is checked here.
        {
            ConductorInputs ab = new ConductorInputs();
            ab.Phase = MissionPhase.Entry; ab.Aborted = true; ab.Holding = true; ab.Complete = true;
            ConductorAction aa = Conductor.Decide(ab);
            Check(aa.Verb == ConductorVerb.Abort && aa.Module == ConductorModule.None,
                  "⛔ an abort outranks a gate hold and a finished plan, and engages NO module — the "
                  + "conductor's only honest response while W19 is HELD (got " + aa + ")");
            // ⚠ `AbortControl` itself cannot be reached from here — it lives in `src/_AutopilotStub.cs`,
            // which `build.py test` COMPILES but does not link into the test runner (that build is
            // `src/pure` + `test` only). Its constant-`AbortMode.None` state is a glue fact, stated on
            // T21's register line rather than asserted where it cannot be.
        }
    }

    // ============================================================================================
    //  THE CAPSULE — the return's vehicle, and it answers back
    // ============================================================================================
    class Capsule
    {
        // ⛔ STARTS UNDOCKED, AND THAT IS A FINDING, NOT A CONVENIENCE. **The conductor never undocks.**
        // Gate G14 is "GO FOR UNDOCK", the crew press the screen's UNDOCK button, and that button calls
        // `MissionOps.Undock()` (`ScreenPainter.cs:1188`) — which is still the demolition stub's
        // log-only no-op. The documented flow in `CrewProcedureOps`'s own comment is *"press UNDOCK,
        // then press AUTO SEQUENCE"*, so by the time the departure leg runs the hooks are already open.
        // ⚠ Wiring that button is the UI COMMAND SURFACE, which this batch put out of scope — logged,
        // not built, and it is a numbered row on the flight checklist with its manual workaround.
        public bool Docked;
        public double RangeM = 5.0;
        public bool TrunkAttached = true;
        public double AltitudeM = 420000.0;
        public bool Descending;
        public bool DroguesOut, MainsOut, Splashed;
        public bool NodeExists, NodeBurned, NoseClosed, HeatShieldForward;
        public double DrogueAltM = -1.0, MainAltM = -1.0;
        int backAwayTicks, burnTicks, trunkTries;

        public ReturnInputs Sense()
        {
            ReturnInputs s = ReturnInputs.Nominal();
            s.RangeM = RangeM; s.Docked = Docked; s.TrunkAttached = TrunkAttached;
            s.AltitudeM = AltitudeM; s.Descending = Descending;
            s.DroguesOut = DroguesOut; s.MainsOut = MainsOut; s.Splashed = Splashed;
            s.NodeExists = NodeExists; s.NodeBurned = NodeBurned;
            return s;
        }

        public void Apply(ReturnAct a)
        {
            switch (a)
            {
                case ReturnAct.BackAway:      Docked = false; backAwayTicks++; break;
                // ⭐ A DECOUPLER THAT SOMETIMES DOES NOT FIRE — the whole reason the sequencer
                // re-commands it. It takes on the second try.
                case ReturnAct.JettisonTrunk:
                    trunkTries++;
                    if (trunkTries >= 2) TrunkAttached = false;   // takes on the second command
                    break;
                case ReturnAct.PlanDeorbit:   NodeExists = true; break;
                case ReturnAct.BurnDeorbit:   burnTicks++; if (burnTicks > 3) NodeBurned = true; break;
                case ReturnAct.CloseNoseCone: NoseClosed = true; break;
                case ReturnAct.HoldHeatShieldForward: HeatShieldForward = true; break;
                case ReturnAct.DeployDrogues: DroguesOut = true; if (DrogueAltM < 0.0) DrogueAltM = AltitudeM; break;
                case ReturnAct.DeployMains:   MainsOut = true;   if (MainAltM < 0.0) MainAltM = AltitudeM; break;
            }
        }

        public void Advance()
        {
            if (!Docked && RangeM < 20000.0) RangeM += 60.0;          // backing away
            if (NodeBurned)
            {
                Descending = true;
                double rate = MainsOut ? 200.0 : DroguesOut ? 1200.0 : 4000.0;
                AltitudeM -= rate;
                if (AltitudeM <= 0.0) { AltitudeM = 0.0; Splashed = true; }
            }
        }
    }

    // ---- 6. CAPTURE, THE BERTH, AND THE CREW'S OVERRIDE ------------------------------------------
    // T20's DONE-when is "dock in-sim". Everything decidable without the game: that the CAPTURE leg
    // engages the Docking Autopilot and ends on a measured dock, that the BERTHED leg holds attitude
    // and never walks the plan through the undock gate on its own, and that the crew's manual override
    // takes the vehicle off the autopilot at any point on the way in.
    static void WalkTheDocking(MissionStep[] plan, ref int index)
    {
        Check(index < plan.Length && plan[index].Kind == StepKind.Fly
              && plan[index].Phase == MissionPhase.Docked,
              "the rendezvous handed the plan to the first Fly(Docked) step");
        if (index >= plan.Length) return;

        MissionProfile m = Missions.Resolve("Crew-2");
        bool docked = false;
        int captureTicks = 0, dockingAsked = 0, killRotAsked = 0, gatesCleared = 0;
        DockingLeg sawCapture = DockingLeg.None, sawBerthed = DockingLeg.None;
        double capAtKos = -1.0, capAtContact = -1.0;
        int offKillRot = 0, capViolations = 0;
        bool ended = false;

        for (int tick = 0; tick < 3000 && index < plan.Length; tick++)
        {
            if (plan[index].Kind == StepKind.Gate)
            {
                Gate g = CrewGates.ById(m, plan[index].Gate);
                bool[] sat = new bool[g.Items == null ? 0 : g.Items.Length];
                for (int i = 0; i < sat.Length; i++) sat[i] = true;
                CrewGateInputs gi;
                gi.Gate = g; gi.Satisfied = sat;
                gi.GoPressed = true; gi.NoGoPressed = false; gi.AbortPressed = false;
                if (!CrewGate.Step(gi, GatePhase.Holding).Cleared)
                { Check(false, "gate " + g.Id + " would not clear"); break; }
                index = ModeManager.Advance(plan, index, new ModeInputs { GateGo = true }).Index;
                gatesCleared++;
                continue;
            }

            if (plan[index].Phase != MissionPhase.Docked) { ended = true; break; }

            GateId next = GateId.None;
            for (int i = index; i < plan.Length; i++)
                if (plan[i].Kind == StepKind.Gate) { next = plan[i].Gate; break; }
            DockingLeg dleg = DockingLadder.LegFor(next);
            if (dleg == DockingLeg.Capture) sawCapture = dleg;
            if (dleg == DockingLeg.Berthed) sawBerthed = dleg;

            ConductorInputs s = new ConductorInputs();
            s.Phase = MissionPhase.Docked;
            s.PhaseComplete = DockingLadder.LegComplete(dleg, docked);
            ConductorAction a = Conductor.Decide(s);

            if (a.Verb == ConductorVerb.Advance)
            {
                index = ModeManager.Advance(plan, index, new ModeInputs { PhaseComplete = true }).Index;
                continue;
            }

            // Counted, not asserted per tick - a check inside a 3000-tick loop drowns the count.
            if (a.Verb != ConductorVerb.Engage || a.Module != ConductorModule.SmartAss
                || a.Op != ConductorOp.KillRot) offKillRot++;

            // ⭐ The glue's own redirect, mirrored: KILL-ROT on the CAPTURE leg means the Docking AP.
            if (DockingLadder.AutopilotFlies(dleg))
            {
                dockingAsked++;
                captureTicks++;
                // the corridor closes: 200 m -> contact, one metre per tick
                double range = Math.Max(0.2, 200.0 - captureTicks);
                double cap = DockingLadder.SpeedLimitFor(range);
                if (range <= RendezvousOps.KeepOutSphereM && capAtKos < 0.0) capAtKos = cap;
                if (range <= DockingLadder.ContactRangeM && capAtContact < 0.0) capAtContact = cap;
                if (!DockingLadder.Conforms(range)) capViolations++;
                if (range <= 0.25) docked = true;          // MechJeb's own acquireRange
                continue;
            }

            killRotAsked++;
            // ⛔ THE BERTHED LEG NEVER COMPLETES ITSELF. If it ever did, this loop would walk the plan
            // straight through the UNDOCK gate with the hooks closed — so it is bounded and asserted.
            if (killRotAsked > 200) { ended = true; break; }
        }

        Check(offKillRot == 0, "every Docked tick asked for SmartASS KILL-ROT and nothing else ("
              + offKillRot + " did not)");
        Check(capViolations == 0, "⛔ the speedLimit cap honoured §B11's rule on every metre of the "
              + "corridor (" + capViolations + " violation(s))");
        Check(sawCapture == DockingLeg.Capture, "the walk stood on the CAPTURE leg");
        Check(docked, "⭐ T20's OWN DONE-WHEN: the capture leg ended on a MEASURED dock");
        Check(dockingAsked > 0, "the Docking Autopilot was engaged for the capture, " + dockingAsked + " tick(s)");
        Check(gatesCleared >= 1, "gate G13 (DOCKING COMPLETE) was worked after capture");
        Check(sawBerthed == DockingLeg.Berthed, "...and the plan then reached the BERTHED leg");
        Check(killRotAsked > 0, "which holds attitude on SmartASS KILL-ROT (" + killRotAsked + " tick(s))");
        Check(ended && index < plan.Length && plan[index].Kind == StepKind.Fly
              && plan[index].Phase == MissionPhase.Docked,
              "⛔ and it is STILL on the berthed leg after 200 ticks — a berthed step that completed "
              + "itself would walk the plan through the UNDOCK gate with the hooks closed");

        Check(capAtKos == DockingLadder.CorridorSpeedMps,
              "the speedLimit at the Keep-Out Sphere is the corridor rung (got " + capAtKos + ")");
        Check(capAtContact == DockingLadder.ContactSpeedMps,
              "...and the contact rung inside 5 m (got " + capAtContact + ")");
        Check(capAtContact < DockingLadder.ContactRateLimitMps,
              "⭐ which is under §B11's documented '< 0.2 m/s inside 5 m' the whole way in");

        // ⭐ THE CREW'S OVERRIDE, PROVED DIRECTLY. §B12.3: the manual docking button shuts the Docking
        // Autopilot down. A nominal walk never presses it, so it is asserted rather than hoped for.
        {
            ConductorInputs mo = new ConductorInputs();
            mo.Phase = MissionPhase.Approach;
            mo.InKeepOutSphere = true;
            ConductorAction auto = Conductor.Decide(mo);
            Check(auto.Verb == ConductorVerb.Engage && auto.Module == ConductorModule.DockingAutopilot,
                  "inside the KOS the Docking Autopilot is the default (O6)");
            mo.ManualDockingRequested = true;
            ConductorAction man = Conductor.Decide(mo);
            Check(man.Verb == ConductorVerb.Idle && man.Module == ConductorModule.None,
                  "⭐ ...and the crew's manual-docking request takes it off, engaging nothing (got "
                  + man + ")");
        }
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
