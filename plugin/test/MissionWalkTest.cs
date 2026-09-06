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

        // ⚠ AND THAT IS AS FAR AS THIS BATCH'S FIRST PHASE GOES. T19/T20/T21 continue the walk from
        // here; until each lands, the phase after Ascent has no controller and the conductor idles.
    }
}
