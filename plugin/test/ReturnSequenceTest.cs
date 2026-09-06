// ReturnSequenceTest — register T21. The return leg: undock → departure → trunk → deorbit → entry →
// chutes → splash, walked on measured state.
//
// ---- WHAT THIS SUITE IS FOR ----
// T21's DONE-when is IN-SIM ("return + splash in-sim"). Everything decidable without the game is here,
// and four of the checks are the ones that would cost the vehicle if they were wrong:
//   ⛔ nothing on the return acts while the vehicle is still HARD-MATED — above all not the trunk
//      decoupler on a Dragon attached to a space station;
//   ⛔ the chute gates need altitude AND DESCENT, because a vehicle can be below 5486 m on the way UP;
//   ⛔ a resume can never land on a step that fires a decoupler on its first tick;
//   ⛔ the drogue and main altitudes are the REAL ones and are not redefined by this file.
//
// ⛔ WHAT IT CANNOT DO. Nothing here actuates. `ReturnSequence.Step` returns a VALUE.
using System;
using DragonScreen;

public static class ReturnSequenceTest
{
    static int checks = 0, failures = 0;
    static void Check(bool ok, string what)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL: " + what); } }

    static void Is(ReturnDecision d, ReturnStep next, ReturnAct act, string what)
    {
        Check(d.Next == next && d.Act == act, what + "   (got " + d + ")");
        Check(!string.IsNullOrEmpty(d.Reason), what + " — carries a reason");
    }

    public static int Run()
    {
        Console.WriteLine("ReturnSequenceTest (T21: undock -> departure -> deorbit -> entry -> chutes)");
        checks = 0; failures = 0;

        TheNumbers();
        DockedIsAbsolute();
        Departure();
        Deorbit();
        ChuteGates();
        Resume();
        WholeWalk();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    static ReturnInputs Free()
    {
        ReturnInputs s = ReturnInputs.Nominal();
        s.RangeM = 50.0; s.TrunkAttached = true;
        return s;
    }

    // ---- 1. THE NUMBERS, AND WHERE EACH ONE LIVES ------------------------------------------------
    static void TheNumbers()
    {
        // ⭐ THE CHUTE ALTITUDES ARE NOT REDEFINED HERE, AND THIS IS THE CHECK THAT SAYS SO. They live
        // in `pure/MissionPhase.cs` with "REAL NUMBERS, from NASA/SpaceX sources", and the sequencer
        // reads them from there. The literals below are the pin.
        Check(Mission.DrogueAltitude == 5486.0, "drogues at 5486 m - the real Mark-3 figure (§8)");
        Check(Mission.MainAltitude == 1830.0, "mains at 1830 m - the real Mark-3 figure (§8)");
        ReturnInputs n = ReturnInputs.Nominal();
        Check(n.DrogueAltitudeM == Mission.DrogueAltitude && n.MainAltitudeM == Mission.MainAltitude,
              "...and ReturnSequence reads them from Mission, never from a second copy");

        // The departure clearance is §B11's own Approach Ellipsoid - the boundary it arrived through.
        Check(n.DepartureClearanceM == RendezvousOps.ApproachEllipsoidM
              && n.DepartureClearanceM == 4000.0,
              "the departure clearance is §B11's 4 km Approach Ellipsoid, shared with the approach");

        // ⛔ THE ONE ESTIMATE, PINNED AS A LITERAL SO IT CANNOT DRIFT SILENTLY.
        Check(ReturnInputs.DeorbitPeriapsisEstimateM == 50000.0,
              "⛔ the deorbit target periapsis is 50 km - a TIER-3 ENGINEERING ESTIMATE by T21, below "
              + "§B11's 122 km entry interface so the trajectory cannot skip, well above zero so the "
              + "entry is not needlessly steep. NOT a sourced value; T21's Q1 and T22's to pin");
        Check(ReturnInputs.DeorbitPeriapsisEstimateM < 122000.0,
              "...and it IS below the documented entry interface, which is the half of it that is a fact");
        Check(n.DeorbitPeriapsisM == ReturnInputs.DeorbitPeriapsisEstimateM,
              "Nominal() seeds it from the named constant");
    }

    // ---- 2. ⛔ NOTHING ACTS WHILE HARD-MATED ------------------------------------------------------
    // The single most expensive mistake available on this leg: firing the trunk decoupler, or backing
    // away on RCS, while the hooks are still closed on a space station.
    static void DockedIsAbsolute()
    {
        ReturnInputs s = Free();
        s.Docked = true;

        Is(ReturnSequence.Step(s, ReturnStep.Idle), ReturnStep.Idle, ReturnAct.None,
           "docked at Idle: nothing happens");

        // Swept over EVERY step, including the ones that fire hardware, with every trigger satisfied.
        ReturnStep[] all = (ReturnStep[])Enum.GetValues(typeof(ReturnStep));
        s.RangeM = 100000.0; s.TrunkAttached = true; s.AltitudeM = 100.0;
        s.Descending = true; s.NodeExists = true; s.NodeBurned = true;
        int acted = 0, moved = 0;
        for (int i = 0; i < all.Length; i++)
        {
            ReturnDecision d = ReturnSequence.Step(s, all[i]);
            if (d.Act != ReturnAct.None) acted++;
            if (d.Next != all[i]) moved++;
        }
        Check(acted == 0,
              "⛔ across ALL " + all.Length + " return steps, with every trigger satisfied, a DOCKED "
              + "vehicle actuates NOTHING (" + acted + " would have)");
        Check(moved == 0, "...and the sequence does not advance either (" + moved + " would have)");

        // Undocked, the very same inputs do act - so the guard is the dock flag and nothing else.
        s.Docked = false;
        Check(ReturnSequence.Step(s, ReturnStep.Idle).Act == ReturnAct.BackAway,
              "...while undocked, the same state starts the back-away");
    }

    // ---- 3. THE DEPARTURE -------------------------------------------------------------------------
    static void Departure()
    {
        ReturnInputs s = Free();
        Is(ReturnSequence.Step(s, ReturnStep.Idle), ReturnStep.Backout, ReturnAct.BackAway,
           "undocked -> back away");

        s.RangeM = 3999.0;
        Is(ReturnSequence.Step(s, ReturnStep.Backout), ReturnStep.Backout, ReturnAct.BackAway,
           "3999 m: still inside the Approach Ellipsoid, still backing away");
        s.RangeM = 4000.0;
        Is(ReturnSequence.Step(s, ReturnStep.Backout), ReturnStep.TrunkJettison, ReturnAct.JettisonTrunk,
           "at 4 km: clear, and the trunk goes");

        // ⭐ A DECOUPLER THAT DID NOT FIRE IS RE-COMMANDED. The same idiom as the MVac relight - one
        // Decouple() that silently does nothing would strand the return with the trunk on.
        Is(ReturnSequence.Step(s, ReturnStep.TrunkJettison), ReturnStep.TrunkJettison,
           ReturnAct.JettisonTrunk, "the trunk is still attached -> re-command the decoupler");
        s.TrunkAttached = false;
        Is(ReturnSequence.Step(s, ReturnStep.TrunkJettison), ReturnStep.Departed, ReturnAct.None,
           "trunk away -> departure complete");

        // ⛔ AND IT HOLDS THERE. The deorbit is the one thing on the return the crew authorise, and
        // nothing may walk into it by itself.
        Is(ReturnSequence.Step(s, ReturnStep.Departed), ReturnStep.Departed, ReturnAct.None,
           "⛔ Departed HOLDS for the crew's G15 GO - the deorbit is never entered by walking");
        Check(ReturnSequence.DepartureComplete(ReturnStep.Departed),
              "and the plan may raise G15 from there");
        Check(!ReturnSequence.DepartureComplete(ReturnStep.Backout),
              "...but not while still backing away");
        Check(!ReturnSequence.DepartureComplete(ReturnStep.TrunkJettison),
              "...nor with the trunk still to go");

        // The G15 GO is what starts the deorbit, through a SEPARATE entry point.
        Check(ReturnSequence.BeginDeorbit(ReturnStep.Departed) == ReturnStep.DeorbitPlan,
              "the crew's GO moves Departed -> DeorbitPlan");
        Check(ReturnSequence.BeginDeorbit(ReturnStep.Drogues) == ReturnStep.Drogues,
              "...and never drags a vehicle already under chutes back to a deorbit plan");
    }

    // ---- 4. THE DEORBIT ---------------------------------------------------------------------------
    static void Deorbit()
    {
        ReturnInputs s = Free();
        s.TrunkAttached = false; s.RangeM = 0.0;

        Is(ReturnSequence.Step(s, ReturnStep.DeorbitPlan), ReturnStep.DeorbitPlan, ReturnAct.PlanDeorbit,
           "no node -> plan the deorbit");
        s.NodeExists = true;
        Is(ReturnSequence.Step(s, ReturnStep.DeorbitPlan), ReturnStep.DeorbitBurn, ReturnAct.BurnDeorbit,
           "node built -> burn it");
        Is(ReturnSequence.Step(s, ReturnStep.DeorbitBurn), ReturnStep.DeorbitBurn, ReturnAct.BurnDeorbit,
           "burn running -> keep the executor on it");
        s.NodeBurned = true;
        Is(ReturnSequence.Step(s, ReturnStep.DeorbitBurn), ReturnStep.NoseCone, ReturnAct.CloseNoseCone,
           "burn complete -> nose cone closed and locked");
        Is(ReturnSequence.Step(s, ReturnStep.NoseCone), ReturnStep.EntryAttitude,
           ReturnAct.HoldHeatShieldForward,
           "nose cone closed -> entry attitude (O8: heat shield forward, no commanded bank)");
    }

    // ---- 5. ⛔ THE CHUTE GATES NEED ALTITUDE **AND** DESCENT --------------------------------------
    // A vehicle can be below 5486 m on the way UP - an abort, a lofted trajectory - and a drogue
    // deployed into that is a drogue destroyed.
    static void ChuteGates()
    {
        ReturnInputs s = Free();
        s.TrunkAttached = false; s.RangeM = 0.0; s.NodeExists = true; s.NodeBurned = true;

        s.AltitudeM = 5000.0; s.Descending = false;
        Is(ReturnSequence.Step(s, ReturnStep.EntryAttitude), ReturnStep.EntryAttitude,
           ReturnAct.HoldHeatShieldForward,
           "⛔ 5000 m and CLIMBING: no drogues - altitude alone is not a chute gate");

        s.AltitudeM = 5487.0; s.Descending = true;
        Is(ReturnSequence.Step(s, ReturnStep.EntryAttitude), ReturnStep.EntryAttitude,
           ReturnAct.HoldHeatShieldForward, "5487 m descending: not yet");
        s.AltitudeM = 5486.0;
        Is(ReturnSequence.Step(s, ReturnStep.EntryAttitude), ReturnStep.Drogues, ReturnAct.DeployDrogues,
           "5486 m descending: drogues");

        s.DroguesOut = false;
        Is(ReturnSequence.Step(s, ReturnStep.Drogues), ReturnStep.Drogues, ReturnAct.DeployDrogues,
           "the drogues did not come out -> re-command");
        s.DroguesOut = true; s.AltitudeM = 4000.0;
        Is(ReturnSequence.Step(s, ReturnStep.Drogues), ReturnStep.Drogues, ReturnAct.None,
           "under drogues at 4 km: nothing to do");

        s.AltitudeM = 1831.0;
        Is(ReturnSequence.Step(s, ReturnStep.Drogues), ReturnStep.Drogues, ReturnAct.None,
           "1831 m: the mains are not due yet");
        s.AltitudeM = 1830.0;
        Is(ReturnSequence.Step(s, ReturnStep.Drogues), ReturnStep.Mains, ReturnAct.DeployMains,
           "1830 m descending: mains");

        s.AltitudeM = 1000.0; s.Descending = false;
        Is(ReturnSequence.Step(s, ReturnStep.Drogues), ReturnStep.Drogues, ReturnAct.None,
           "⛔ 1000 m but not descending: NO mains");

        s.MainsOut = false; s.Descending = true; s.AltitudeM = 900.0;
        Is(ReturnSequence.Step(s, ReturnStep.Mains), ReturnStep.Mains, ReturnAct.DeployMains,
           "the mains did not come out -> re-command");
        s.MainsOut = true;
        Is(ReturnSequence.Step(s, ReturnStep.Mains), ReturnStep.Mains, ReturnAct.None, "under mains");

        s.Splashed = true;
        Is(ReturnSequence.Step(s, ReturnStep.Mains), ReturnStep.Splashdown, ReturnAct.ReleaseControl,
           "splashed -> control released to the crew");
        Is(ReturnSequence.Step(s, ReturnStep.Splashdown), ReturnStep.Complete, ReturnAct.None,
           "-> complete");
        Check(ReturnSequence.ReturnComplete(ReturnStep.Complete), "and the plan may end");
        Check(!ReturnSequence.ReturnComplete(ReturnStep.Mains), "...but not under the mains");

        // ⛔ A splash under DROGUES still ends the mission - the mains failing is not a reason to hold
        // the conductor on a floating capsule.
        ReturnInputs sp = Free();
        sp.TrunkAttached = false; sp.Splashed = true;
        Is(ReturnSequence.Step(sp, ReturnStep.Drogues), ReturnStep.Splashdown, ReturnAct.ReleaseControl,
           "a splash under drogues alone still ends the return");
    }

    // ---- 6. RESUMING A RETURN ALREADY UNDER WAY ---------------------------------------------------
    // ⛔ The property that matters: a resume can never land on a step that fires hardware on its first
    // tick. Sweeping every phase and both flags.
    static void Resume()
    {
        Check(ReturnSequence.ResumeFrom(MissionPhase.Phasing, true, true) == ReturnStep.Idle,
              "docked -> Idle, whatever the phase says");
        Check(ReturnSequence.ResumeFrom(MissionPhase.Phasing, false, true) == ReturnStep.Backout,
              "undocked with the trunk on -> back away");
        Check(ReturnSequence.ResumeFrom(MissionPhase.Phasing, false, false) == ReturnStep.Departed,
              "undocked with the trunk gone -> departed, holding for the deorbit GO");
        Check(ReturnSequence.ResumeFrom(MissionPhase.Entry, false, false) == ReturnStep.DeorbitPlan,
              "already in the Entry phase -> the deorbit plan");
        Check(ReturnSequence.ResumeFrom(MissionPhase.Drogues, false, false) == ReturnStep.EntryAttitude,
              "⭐ under chutes -> EntryAttitude, so the chute GATES are re-evaluated rather than "
              + "assumed already fired");
        Check(ReturnSequence.ResumeFrom(MissionPhase.Splashdown, false, false) == ReturnStep.Complete,
              "splashed -> complete");

        int firesHardware = 0, cases = 0;
        MissionPhase[] phases = (MissionPhase[])Enum.GetValues(typeof(MissionPhase));
        for (int i = 0; i < phases.Length; i++)
            for (int d = 0; d < 2; d++)
                for (int tr = 0; tr < 2; tr++)
                {
                    ReturnStep r = ReturnSequence.ResumeFrom(phases[i], d == 1, tr == 1);
                    cases++;
                    if (r == ReturnStep.TrunkJettison || r == ReturnStep.DeorbitBurn
                        || r == ReturnStep.Drogues || r == ReturnStep.Mains) firesHardware++;
                }
        Check(cases == phases.Length * 4 && firesHardware == 0,
              "⛔ across all " + cases + " resume cases, NONE lands on a step that fires hardware on "
              + "its first tick (" + firesHardware + " did)");
    }

    // ---- 7. THE WHOLE RETURN, IN ORDER, ONCE ------------------------------------------------------
    static void WholeWalk()
    {
        ReturnInputs s = ReturnInputs.Nominal();
        s.Docked = true; s.TrunkAttached = true; s.RangeM = 5.0;
        ReturnStep at = ReturnStep.Idle;
        ReturnAct[] seen = new ReturnAct[16];
        int acts = 0;

        // undock
        s.Docked = false;
        at = Drive(ref s, at, ref acts, seen);      // -> Backout (BackAway)
        s.RangeM = 6000.0;
        at = Drive(ref s, at, ref acts, seen);      // -> TrunkJettison (JettisonTrunk)
        s.TrunkAttached = false;
        at = Drive(ref s, at, ref acts, seen);      // -> Departed

        Check(at == ReturnStep.Departed, "the departure leg ends at Departed (got " + at + ")");

        // the crew's G15 GO
        at = ReturnSequence.BeginDeorbit(at);
        Check(at == ReturnStep.DeorbitPlan, "the GO starts the deorbit plan");

        at = Drive(ref s, at, ref acts, seen);      // -> DeorbitPlan (PlanDeorbit), stays
        s.NodeExists = true;
        at = Drive(ref s, at, ref acts, seen);      // -> DeorbitBurn (BurnDeorbit)
        s.NodeBurned = true;
        at = Drive(ref s, at, ref acts, seen);      // -> NoseCone (CloseNoseCone)
        at = Drive(ref s, at, ref acts, seen);      // -> EntryAttitude (HoldHeatShieldForward)
        s.Descending = true; s.AltitudeM = 5000.0;
        at = Drive(ref s, at, ref acts, seen);      // -> Drogues (DeployDrogues)
        s.DroguesOut = true; s.AltitudeM = 1500.0;
        at = Drive(ref s, at, ref acts, seen);      // -> Mains (DeployMains)
        s.MainsOut = true; s.Splashed = true;
        at = Drive(ref s, at, ref acts, seen);      // -> Splashdown (ReleaseControl)
        at = Drive(ref s, at, ref acts, seen);      // -> Complete

        Check(at == ReturnStep.Complete, "the whole return ends at Complete (got " + at + ")");
        Check(ReturnSequence.ReturnComplete(at), "and reports itself complete");

        // ⭐ THE ACTUATION ORDER IS THE FLIGHT ORDER, and the trunk never precedes the back-away.
        ReturnAct[] want = {
            ReturnAct.BackAway, ReturnAct.JettisonTrunk, ReturnAct.PlanDeorbit, ReturnAct.BurnDeorbit,
            ReturnAct.CloseNoseCone, ReturnAct.HoldHeatShieldForward, ReturnAct.DeployDrogues,
            ReturnAct.DeployMains, ReturnAct.ReleaseControl
        };
        Check(acts == want.Length, "the return actuates exactly " + want.Length + " commands (got "
              + acts + ")");
        for (int i = 0; i < want.Length && i < acts; i++)
            Check(seen[i] == want[i], "return command " + (i + 1) + " is " + want[i]
                  + " (got " + seen[i] + ")");
    }

    // One tick, recording whatever it commanded. ⛔ ONE, not "until it moves": several return steps
    // legitimately re-command the same thing while they wait (a decoupler that did not fire, a node not
    // yet built), so a loop-until-it-moves helper would spin on exactly the behaviour the sequencer is
    // designed to have. The walk supplies the state change between calls, which is what a vehicle does.
    static ReturnStep Drive(ref ReturnInputs s, ReturnStep at, ref int acts, ReturnAct[] seen)
    {
        ReturnDecision d = ReturnSequence.Step(s, at);
        if (d.Act != ReturnAct.None && acts < seen.Length) { seen[acts] = d.Act; acts++; }
        return d.Next;
    }
}
