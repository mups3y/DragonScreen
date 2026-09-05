// ConductorTest — register T16. The pure conductor core's decisions, over §B12.3's phase table and
// §B12.4's re-plan rule.
//
// ---- WHAT THIS SUITE IS FOR ----
// T16's DONE-when: "headless tests green over ConductorAction output (module / Operation /
// advance-hold-replan) for representative phase transitions". Every check below is one of those.
//
// ⛔ AND WHAT IT IS NOT. Nothing here executes anything. `Conductor.Decide` returns a VALUE; the glue
// that turns that value into a MechJeb call is T18 onward. A test that "engages the docking AP" here
// is asserting that the core DECIDED to, not that anything moved.
using System;
using DragonScreen;

public static class ConductorTest
{
    static int checks = 0, failures = 0;
    static void Check(bool ok, string what)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL: " + what); } }

    static void Is(ConductorAction a, ConductorVerb v, ConductorModule m, ConductorOp o, string what)
    {
        Check(a.Verb == v && a.Module == m && a.Op == o, what + "   (got " + a + ")");
        // ⭐ EVERY decision must be able to say why. Not decoration: §0's three misdiagnoses were all
        // "the vehicle did something and nothing said why", and a reasonless action reproduces that.
        Check(!string.IsNullOrEmpty(a.Reason), what + " — carries a reason");
    }

    public static int Run()
    {
        Console.WriteLine("ConductorTest (T16: the pure ConductorAction core — §B12.3 phases, §B12.4 re-plan)");
        checks = 0; failures = 0;

        Precedence();
        PhaseTable();
        PlanThenBurn();
        ApproachChain();
        ReplanRule();
        NoMagicNumbers();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    static ConductorInputs Flying(MissionPhase p)
    {
        ConductorInputs s = new ConductorInputs();
        s.Phase = p;
        return s;
    }

    // ---- 1. THE SAFETY ORDER. This is the half most likely to be "tidied" into the wrong order. ----
    static void Precedence()
    {
        // Abort outranks EVERYTHING, including a gate hold. An abort raised while the crew are being
        // asked for a GO must not be swallowed by the hold.
        ConductorInputs s = Flying(MissionPhase.Ascent);
        s.Aborted = true; s.Holding = true; s.Complete = true; s.PhaseComplete = true;
        Is(Conductor.Decide(s), ConductorVerb.Abort, ConductorModule.None, ConductorOp.None,
           "T16 abort outranks hold, complete and phase-complete together");

        // Complete outranks the phase table: a finished plan cannot re-engage a module.
        s = Flying(MissionPhase.Ascent); s.Complete = true; s.PhaseComplete = true;
        Is(Conductor.Decide(s), ConductorVerb.Release, ConductorModule.None, ConductorOp.None,
           "T16 a completed plan releases control and does not re-engage ascent");

        // A crew HOLD outranks every fly decision - that is what a gate is.
        s = Flying(MissionPhase.Ascent); s.Holding = true;
        Is(Conductor.Decide(s), ConductorVerb.Hold, ConductorModule.None, ConductorOp.None,
           "T16 a crew gate holds, and does not fly the phase underneath it");

        // ...and a hold outranks phase-complete too: the plan may not advance past an unanswered gate.
        s = Flying(MissionPhase.Ascent); s.Holding = true; s.PhaseComplete = true;
        Is(Conductor.Decide(s), ConductorVerb.Hold, ConductorModule.None, ConductorOp.None,
           "T16 ...including over a completed phase");

        // Advance is decided BEFORE the phase table, so a finished step cannot engage its own module
        // for one more tick.
        s = Flying(MissionPhase.Coast); s.PhaseComplete = true;
        Is(Conductor.Decide(s), ConductorVerb.Advance, ConductorModule.None, ConductorOp.None,
           "T16 a completed phase advances rather than re-engaging");
    }

    // ---- 2. §B12.3's TABLE, phase by phase --------------------------------------------------------
    static void PhaseTable()
    {
        Is(Conductor.Decide(Flying(MissionPhase.Prelaunch)),
           ConductorVerb.Engage, ConductorModule.AscentPvg, ConductorOp.None,
           "T16 Prelaunch arms PVG");
        Is(Conductor.Decide(Flying(MissionPhase.Ascent)),
           ConductorVerb.Engage, ConductorModule.AscentPvg, ConductorOp.None,
           "T16 Ascent flies PVG");
        Is(Conductor.Decide(Flying(MissionPhase.Docked)),
           ConductorVerb.Engage, ConductorModule.SmartAss, ConductorOp.KillRot,
           "T16 Docked holds attitude with KILL-ROT");
        Is(Conductor.Decide(Flying(MissionPhase.Splashdown)),
           ConductorVerb.Release, ConductorModule.None, ConductorOp.None,
           "T16 Splashdown releases control");
        Is(Conductor.Decide(Flying(MissionPhase.Landed)),
           ConductorVerb.Release, ConductorModule.None, ConductorOp.None,
           "T16 Landed releases control");

        // ⛔ Under chutes the conductor flies NOTHING - §B12.3 gives those phases to chute triggering
        // and the Manual Chute page. An Engage here would be the conductor taking a phase it was
        // never given.
        Is(Conductor.Decide(Flying(MissionPhase.Drogues)),
           ConductorVerb.Idle, ConductorModule.None, ConductorOp.None,
           "T16 Drogues is not the conductor's to fly");
        Is(Conductor.Decide(Flying(MissionPhase.Mains)),
           ConductorVerb.Idle, ConductorModule.None, ConductorOp.None,
           "T16 Mains is not the conductor's to fly");

        // ⛔ Unknown is the NORMAL reading for a phase no controller claims (W10 made ActivePhase
        // report it), so it must idle rather than error or engage.
        Is(Conductor.Decide(Flying(MissionPhase.Unknown)),
           ConductorVerb.Idle, ConductorModule.None, ConductorOp.None,
           "T16 Unknown idles - it is not an error");

        // Entry: the deorbit node first, then the entry attitude. Both halves, in order.
        ConductorInputs e = Flying(MissionPhase.Entry);
        e.NodeExists = true;
        Is(Conductor.Decide(e), ConductorVerb.Engage, ConductorModule.NodeExecutor, ConductorOp.Periapsis,
           "T16 Entry burns the deorbit node while one exists");
        e.NodeBurned = true;
        Is(Conductor.Decide(e), ConductorVerb.Engage, ConductorModule.SmartAss, ConductorOp.HeatShieldForward,
           "T16 Entry then holds heat-shield-forward");
        ConductorInputs e2 = Flying(MissionPhase.Entry);   // no node at all
        Is(Conductor.Decide(e2), ConductorVerb.Engage, ConductorModule.SmartAss, ConductorOp.HeatShieldForward,
           "T16 ...and with no node at all, goes straight to the entry attitude");
    }

    // ---- 3. PLAN, THEN BURN — two modules, in order ----------------------------------------------
    static void PlanThenBurn()
    {
        ConductorInputs s = Flying(MissionPhase.Coast);
        Is(Conductor.Decide(s), ConductorVerb.Engage, ConductorModule.ManeuverPlanner, ConductorOp.Circularize,
           "T16 Coast plans a circularise node when there is none");
        s.NodeExists = true;
        Is(Conductor.Decide(s), ConductorVerb.Engage, ConductorModule.NodeExecutor, ConductorOp.Circularize,
           "T16 ...then executes it");
        s.NodeBurned = true;
        Is(Conductor.Decide(s), ConductorVerb.Advance, ConductorModule.None, ConductorOp.None,
           "T16 ...then advances");

        // Phasing takes the same path - §B12.3 lists them together.
        ConductorInputs p = Flying(MissionPhase.Phasing);
        Is(Conductor.Decide(p), ConductorVerb.Engage, ConductorModule.ManeuverPlanner, ConductorOp.Circularize,
           "T16 Phasing takes the same plan-then-burn path as Coast");
    }

    // ---- 4. §B12.3's APPROACH CHAIN, in the plan's own order --------------------------------------
    static void ApproachChain()
    {
        ConductorOp[] want = { ConductorOp.MatchPlane, ConductorOp.Transfer,
                               ConductorOp.CourseCorrection, ConductorOp.KillRelVel };
        Check(Conductor.ApproachChain.Length == want.Length, "T16 the approach chain has four steps");
        for (int i = 0; i < want.Length && i < Conductor.ApproachChain.Length; i++)
            Check(Conductor.ApproachChain[i] == want[i],
                  "T16 approach step " + i + " is " + want[i] + " (Plane->Transfer->CourseCorrection->KillRelVel)");

        // Walk it: each step plans, then burns, then the next step begins.
        for (int k = 0; k < want.Length; k++)
        {
            ConductorInputs s = Flying(MissionPhase.Approach);
            s.ApproachStepsDone = k;
            Is(Conductor.Decide(s), ConductorVerb.Engage, ConductorModule.ManeuverPlanner, want[k],
               "T16 approach step " + (k + 1) + " plans " + want[k]);
            s.NodeExists = true;
            Is(Conductor.Decide(s), ConductorVerb.Engage, ConductorModule.NodeExecutor, want[k],
               "T16 approach step " + (k + 1) + " executes " + want[k]);
        }

        // Chain finished, still outside the sphere: advance and wait, do not re-plan forever.
        {
            ConductorInputs s = Flying(MissionPhase.Approach);
            s.ApproachStepsDone = want.Length;
            Is(Conductor.Decide(s), ConductorVerb.Advance, ConductorModule.None, ConductorOp.None,
               "T16 a finished approach chain advances and awaits the keep-out sphere");
        }

        // ⛔ THE KEEP-OUT SPHERE OUTRANKS THE CHAIN. Inside the KOS the Docking AP is the DEFAULT
        // (O6/§B10.3), and planning a transfer burn next to a station because a chain step is
        // unfinished would be exactly wrong.
        {
            ConductorInputs s = Flying(MissionPhase.Approach);
            s.ApproachStepsDone = 0;            // chain barely started
            s.InKeepOutSphere = true;
            Is(Conductor.Decide(s), ConductorVerb.Engage, ConductorModule.DockingAutopilot, ConductorOp.None,
               "T16 inside the KOS the docking AP takes over, even mid-chain");
        }

        // ...and the crew's manual docking override shuts it down. The core never initiates this.
        {
            ConductorInputs s = Flying(MissionPhase.Approach);
            s.InKeepOutSphere = true; s.ManualDockingRequested = true;
            Is(Conductor.Decide(s), ConductorVerb.Idle, ConductorModule.None, ConductorOp.None,
               "T16 the crew's manual docking override stands the docking AP down");
        }
    }

    // ---- 5. §B12.4's RE-PLAN RULE ----------------------------------------------------------------
    // "if closestApproachErr > εd OR residual > tol OR drift -> rebuild Operation k"
    static void ReplanRule()
    {
        // Each disjunct, alone, rebuilds the step just executed - not the next one.
        {
            ConductorInputs s = Flying(MissionPhase.Approach);
            s.ApproachStepsDone = 2;                       // Transfer was the last executed
            s.ClosestApproachTolM = 500.0; s.ClosestApproachErrM = 900.0;
            Is(Conductor.Decide(s), ConductorVerb.Replan, ConductorModule.ManeuverPlanner, ConductorOp.Transfer,
               "T16 closest-approach error over tolerance rebuilds the LAST executed op");
        }
        {
            ConductorInputs s = Flying(MissionPhase.Approach);
            s.ApproachStepsDone = 1;                       // Plane was the last executed
            s.NodeResidualTolMps = 0.2; s.NodeResidualMps = 1.5;
            Is(Conductor.Decide(s), ConductorVerb.Replan, ConductorModule.ManeuverPlanner, ConductorOp.MatchPlane,
               "T16 node residual over tolerance rebuilds the last executed op");
        }
        {
            ConductorInputs s = Flying(MissionPhase.Approach);
            s.ApproachStepsDone = 3;
            s.Drifting = true;
            Is(Conductor.Decide(s), ConductorVerb.Replan, ConductorModule.ManeuverPlanner,
               ConductorOp.CourseCorrection, "T16 drift alone rebuilds the last executed op");
        }

        // ⛔ INSIDE the tolerance is NOT a re-plan. A rule that always fires is not a rule.
        {
            ConductorInputs s = Flying(MissionPhase.Approach);
            s.ApproachStepsDone = 2;
            s.ClosestApproachTolM = 500.0; s.ClosestApproachErrM = 100.0;
            s.NodeResidualTolMps = 0.2; s.NodeResidualMps = 0.05;
            Is(Conductor.Decide(s), ConductorVerb.Engage, ConductorModule.ManeuverPlanner,
               ConductorOp.CourseCorrection, "T16 within tolerance, the chain continues rather than re-planning");
        }

        // ⛔ NOTHING RE-PLANS BEFORE THE FIRST STEP HAS RUN. With ApproachStepsDone = 0 there is no
        // "Operation k" to rebuild, and a rule that fired here would rebuild the step it is about to
        // plan anyway - an infinite hold at step one.
        {
            ConductorInputs s = Flying(MissionPhase.Approach);
            s.ApproachStepsDone = 0;
            s.ClosestApproachTolM = 500.0; s.ClosestApproachErrM = 9999.0;
            s.Drifting = true;
            Is(Conductor.Decide(s), ConductorVerb.Engage, ConductorModule.ManeuverPlanner, ConductorOp.MatchPlane,
               "T16 with no step executed there is nothing to re-plan - it plans step 1");
        }

        // ⛔ AND THE KOS STILL OUTRANKS A RE-PLAN. Next to the station, the answer is the docking AP.
        {
            ConductorInputs s = Flying(MissionPhase.Approach);
            s.ApproachStepsDone = 2; s.InKeepOutSphere = true;
            s.ClosestApproachTolM = 500.0; s.ClosestApproachErrM = 9999.0;
            Is(Conductor.Decide(s), ConductorVerb.Engage, ConductorModule.DockingAutopilot, ConductorOp.None,
               "T16 inside the KOS, a blown tolerance does not restart the transfer chain");
        }
    }

    // ---- 6. ⛔ THE THRESHOLDS ARE THE CALLER'S, NOT THIS FILE'S (§1.4 / C1.15) --------------------
    // §B12.4 gives the RULE; the NUMBERS are §B7-B11 locked params and T22's tune. If a future chat
    // hard-codes an εd or a tol in Conductor.cs, these fail - because the same measurements would
    // then produce the same verdict whatever the caller asked for.
    static void NoMagicNumbers()
    {
        ConductorInputs s = Flying(MissionPhase.Approach);
        s.ApproachStepsDone = 2;
        s.ClosestApproachErrM = 300.0;

        s.ClosestApproachTolM = 100.0;      // caller says 300 is too far
        Check(Conductor.Decide(s).Verb == ConductorVerb.Replan,
              "T16 a TIGHT caller tolerance makes 300 m a re-plan");

        s.ClosestApproachTolM = 1000.0;     // same error, looser caller policy
        Check(Conductor.Decide(s).Verb != ConductorVerb.Replan,
              "T16 ...and a LOOSE one does not - the threshold is the caller's, not the core's");

        // A zero/unset tolerance disables that disjunct rather than firing on everything: an unset
        // policy must not be read as "zero metres of error allowed".
        s.ClosestApproachTolM = 0.0;
        Check(Conductor.Decide(s).Verb != ConductorVerb.Replan,
              "T16 an UNSET tolerance disables the check rather than failing everything");

        s = Flying(MissionPhase.Approach);
        s.ApproachStepsDone = 2; s.NodeResidualMps = 5.0; s.NodeResidualTolMps = 0.0;
        Check(Conductor.Decide(s).Verb != ConductorVerb.Replan,
              "T16 ...the same for an unset residual tolerance");
    }
}
