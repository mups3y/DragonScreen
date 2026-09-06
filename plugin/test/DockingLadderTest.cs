// DockingLadderTest — register T20. §B10.3's speedLimit ladder, §B11's contact-rate rule, and the
// discrimination between the mission plan's two `Fly(Docked)` steps.
//
// ---- WHAT THIS SUITE IS FOR ----
// T20's DONE-when is IN-SIM ("dock in-sim"). Everything about that which is decidable without the game
// is here: that the ladder walks DOWN and never up, that it never exceeds §B11's documented "< 0.2 m/s
// inside 5 m" anywhere inside 5 m, that the Docking Autopilot flies the CAPTURE step and not the
// BERTHED one, and that a berthed step never completes itself and walks the plan through the undock
// gate with the hooks still closed.
//
// ⛔ WHAT IT CANNOT DO. It does not engage MechJeb's docking autopilot and does not dock anything.
// `MechJebModuleDockingAutopilot` needs a target, a bounding box and RCS; that is the sim's to prove.
using System;
using DragonScreen;

public static class DockingLadderTest
{
    static int checks = 0, failures = 0;
    static void Check(bool ok, string what)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL: " + what); } }

    public static int Run()
    {
        Console.WriteLine("DockingLadderTest (T20: §B10.3's speedLimit ladder + the two Docked legs)");
        checks = 0; failures = 0;

        PublishedRungs();
        TheLadder();
        TheHardRule();
        WhichDockedStep();
        Completion();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    // ---- 1. THE RUNGS, WRITTEN OUT BY HAND -------------------------------------------------------
    // ⛔ The literal pin: these are NOT read from the constants they check.
    static void PublishedRungs()
    {
        Check(DockingLadder.FarSpeedMps == 1.0,
              "the far rung is 1.0 m/s - §B10.3 'keep-out approach ~1', and the shipped cfg's own "
              + "persisted speedLimit = 1");
        Check(DockingLadder.CorridorSpeedMps == 0.3,
              "the corridor rung is 0.3 m/s - the SLOWER end of §B10.3's 'waypoints ~0.3-0.5' band");
        Check(DockingLadder.ContactSpeedMps == 0.1,
              "the contact rung is 0.1 m/s - §B11 '[DOC] Crew Dragon final contact ~0.1 m/s'");
        Check(DockingLadder.ContactRangeM == 5.0,
              "the contact range is 5 m - §B11 'rate must stay < 0.2 m/s inside 5 m range'");
        Check(DockingLadder.ContactRateLimitMps == 0.2,
              "...and 0.2 m/s is that rule's LIMIT, which nothing is ever set to");

        // ⭐ AND THE SHIPPED CFG STILL SAYS 1, which is the second in-repo source for the far rung.
        // (Checked as a value, not a file read: MechHostTest owns the cfg's integrity.)
        Check(DockingLadder.SpeedLimitFor(1000.0) == 1.0,
              "a kilometre out, the cap is the cfg's own value and nothing has quietly re-tuned it");
    }

    // ---- 2. THE LADDER WALKS DOWN ----------------------------------------------------------------
    static void TheLadder()
    {
        Check(DockingLadder.SpeedLimitFor(50000.0) == 1.0, "50 km out: 1.0 m/s");
        Check(DockingLadder.SpeedLimitFor(201.0) == 1.0, "just outside the Keep-Out Sphere: still 1.0");
        Check(DockingLadder.SpeedLimitFor(200.0) == 0.3,
              "⭐ AT the Keep-Out Sphere the ladder steps down to the corridor rung - the same 200 m "
              + "boundary §B12.3 hands the vehicle to the Docking AP at");
        Check(DockingLadder.SpeedLimitFor(20.0) == 0.3, "at WP2 (20 m): still the corridor rung");
        Check(DockingLadder.SpeedLimitFor(5.1) == 0.3, "just outside 5 m: still 0.3");
        Check(DockingLadder.SpeedLimitFor(5.0) == 0.1, "at 5 m: down to the contact rung");
        Check(DockingLadder.SpeedLimitFor(0.25) == 0.1, "at MechJeb's own 0.25 m acquire range: 0.1");

        // ⛔ AN UNKNOWN RANGE TAKES THE SLOWEST RUNG, NOT THE FASTEST. A zero range means no target
        // acquired, and the safe reading of "I do not know how far away the station is" is 0.1 m/s.
        Check(DockingLadder.SpeedLimitFor(0.0) == 0.1,
              "⛔ an unknown (zero) range takes the SLOWEST rung, never the fastest");
        Check(DockingLadder.SpeedLimitFor(-3.0) == 0.1, "...as does a negative one");

        // ⭐ MONOTONE over a full sweep from 50 km to 1 cm.
        double last = 0.0; int rises = 0, samples = 0;
        for (double r = 0.01; r < 50000.0; r *= 1.02)
        {
            double s = DockingLadder.SpeedLimitFor(r);
            if (s < last) rises++;              // sweeping OUTWARD, the cap must never fall
            last = s; samples++;
        }
        Check(samples > 500 && rises == 0,
              "⛔ over " + samples + " ranges the ladder never rises as the range closes ("
              + rises + " violation(s))");
        Check(DockingLadder.Monotone(10.0, 100.0) && DockingLadder.Monotone(1.0, 10.0)
              && DockingLadder.Monotone(100.0, 1000.0),
              "the monotone helper agrees at each rung boundary");
    }

    // ---- 3. §B11's HARD RULE, CHECKED AS A PROPERTY OF THE WHOLE LADDER --------------------------
    // ⛔ Not of one constant. A later tune could satisfy "the contact rung is under 0.2" and still
    // break the rule at 4.9 m by moving a boundary.
    static void TheHardRule()
    {
        int violations = 0, inside = 0;
        for (double r = 0.01; r <= DockingLadder.ContactRangeM; r += 0.01)
        {
            inside++;
            if (!DockingLadder.Conforms(r)) violations++;
            if (DockingLadder.SpeedLimitFor(r) >= DockingLadder.ContactRateLimitMps) violations++;
        }
        Check(inside > 400 && violations == 0,
              "⭐ across " + inside + " ranges inside 5 m the cap is ALWAYS under §B11's 0.2 m/s "
              + "documented limit (" + violations + " violation(s))");
        Check(DockingLadder.Conforms(50.0),
              "outside 5 m the rule does not apply and Conforms says so rather than failing");

        // ⭐ AND THE GUARD ITSELF REJECTS SOMETHING — the check a mutation walked through when
        // `Conforms` computed its own input. Handed a cap the real ladder would never produce, it must
        // say no, or it is not a guard at all.
        Check(!DockingLadder.Conforms(1.0, 0.2),
              "⛔ a cap AT §B11's 0.2 m/s limit is refused at 1 m — the rule is '< 0.2', not '<= 0.2'");
        Check(!DockingLadder.Conforms(4.9, 0.5),
              "⛔ the corridor rung would be refused inside 5 m, which is why the ladder steps down");
        Check(!DockingLadder.Conforms(0.25, 1.0),
              "⛔ and the far rung is refused at MechJeb's own acquire range");
        Check(DockingLadder.Conforms(4.9, 0.1),
              "...while the contact rung is accepted there");
        Check(DockingLadder.Conforms(5.1, 1.0),
              "...and outside 5 m any cap is accepted, because the rule does not reach there");
        Check(DockingLadder.SpeedLimitFor(4.99) < DockingLadder.ContactRateLimitMps,
              "at 4.99 m the cap is under the limit, with margin");
    }

    // ---- 4. WHICH `Fly(Docked)` STEP -------------------------------------------------------------
    // ⛔ The mission plan has TWO, and the phase enum cannot tell them apart. This is the check that
    // stops the Docking Autopilot being engaged on a vehicle that is already hard-mated.
    static void WhichDockedStep()
    {
        Check(DockingLadder.LegFor(GateId.DockingCompleteG13) == DockingLeg.Capture,
              "the step walking toward G13 is the CAPTURE leg");
        Check(DockingLadder.LegFor(GateId.UndockGoG14) == DockingLeg.Berthed,
              "the step walking toward G14 is the BERTHED leg");

        // Swept over the whole enum so a new gate cannot silently acquire a docking leg.
        GateId[] all = (GateId[])Enum.GetValues(typeof(GateId));
        int named = 0;
        for (int i = 0; i < all.Length; i++)
            if (DockingLadder.LegFor(all[i]) != DockingLeg.None) named++;
        Check(named == 2, "exactly two gates name a docking leg (got " + named + " of " + all.Length + ")");
        Check(DockingLadder.LegFor(GateId.WP2DockGoG12) == DockingLeg.None,
              "⛔ G12 is an APPROACH leg's gate, not a docking one - that is T19's");

        Check(DockingLadder.AutopilotFlies(DockingLeg.Capture),
              "the Docking Autopilot flies the capture leg");
        Check(!DockingLadder.AutopilotFlies(DockingLeg.Berthed),
              "⛔ ...and NOT the berthed one - engaging it on a hard-mated vehicle would try to re-dock it");
        Check(!DockingLadder.AutopilotFlies(DockingLeg.None), "...nor anywhere else");

        // ⭐ AND THE TWO STEPS REALLY DO EXIST IN THE REAL PLAN, in this order, with these gates
        // between them. If `ModeManager` is ever restructured, this suite says so.
        MissionStep[] plan = ModeManager.Plan(Missions.Resolve("Crew-2"));
        int capture = -1, berthed = -1, g13 = -1, g14 = -1;
        for (int i = 0; i < plan.Length; i++)
        {
            if (plan[i].Kind == StepKind.Gate && plan[i].Gate == GateId.DockingCompleteG13) g13 = i;
            if (plan[i].Kind == StepKind.Gate && plan[i].Gate == GateId.UndockGoG14) g14 = i;
        }
        for (int i = 0; i < plan.Length; i++)
            if (plan[i].Kind == StepKind.Fly && plan[i].Phase == MissionPhase.Docked)
            { if (g13 >= 0 && i < g13) capture = i; else if (berthed < 0) berthed = i; }

        Check(capture >= 0 && berthed >= 0 && g13 >= 0 && g14 >= 0,
              "the real plan has both Docked steps and both gates");
        Check(capture < g13 && g13 < berthed && berthed < g14,
              "...in the order capture -> G13 -> berthed -> G14 (got " + capture + "/" + g13 + "/"
              + berthed + "/" + g14 + ")");
    }

    // ---- 5. WHEN A DOCKING STEP IS FINISHED ------------------------------------------------------
    static void Completion()
    {
        Check(DockingLadder.LegComplete(DockingLeg.Capture, true),
              "the capture leg ends on a MEASURED dock");
        Check(!DockingLadder.LegComplete(DockingLeg.Capture, false),
              "...and not before");

        // ⛔ THE ONE THAT MATTERS MOST. A berthed step that completed itself would walk the plan
        // straight through the UNDOCK gate with the hooks still closed.
        Check(!DockingLadder.LegComplete(DockingLeg.Berthed, true),
              "⛔ the BERTHED leg NEVER completes itself, docked or not - the crew's UNDOCK press is "
              + "what ends it (CrewProcedureOps.MarkDockedThisMission)");
        Check(!DockingLadder.LegComplete(DockingLeg.Berthed, false), "...in either state");
        Check(!DockingLadder.LegComplete(DockingLeg.None, true), "and a non-docking step never completes");
    }
}
