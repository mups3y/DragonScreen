// Tests for L4: the crew-gate state machine (pure/CrewGate.cs), the real gate catalog
// (pure/CrewGates.cs), and the mission conductor / phase sequencer (pure/ModeManager.cs).
using System;
using DragonScreen;

public static class CrewGateTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    static Gate TwoItem()
    {
        Gate g; g.Id = GateId.LaunchGoG7; g.Title = "T";
        g.Items = new[] { ChecklistItem.Crew("a"), ChecklistItem.Sys("b") };
        return g;
    }
    static CrewGateInputs In(Gate g, bool[] sat, bool go = false, bool nogo = false, bool abort = false)
    {
        CrewGateInputs s; s.Gate = g; s.Satisfied = sat;
        s.GoPressed = go; s.NoGoPressed = nogo; s.AbortPressed = abort; return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen L4 crew-gate + conductor tests");

        // ============================ CREW GATE state machine ============================
        Gate g = TwoItem();
        Check("all-satisfied is false with a null bit array", !CrewGate.AllSatisfied(g, null), "");
        Check("all-satisfied is false when one item is unchecked", !CrewGate.AllSatisfied(g, new[] { true, false }), "");
        Check("all-satisfied is true when every item is checked", CrewGate.AllSatisfied(g, new[] { true, true }), "");

        Check("incomplete checklist → Holding (complete the checklist)",
              CrewGate.Step(In(g, new[] { true, false }), GatePhase.Holding).Phase == GatePhase.Holding, "");
        Check("complete checklist, no press → GoReady (crew GO required)",
              CrewGate.Step(In(g, new[] { true, true }), GatePhase.Holding).Phase == GatePhase.GoReady, "");

        CrewGateStep cleared = CrewGate.Step(In(g, new[] { true, true }, go: true), GatePhase.GoReady);
        Check("crew GO on a ready gate clears it", cleared.Phase == GatePhase.Go && cleared.Cleared, "");
        Check("crew GO on an INCOMPLETE checklist does NOT clear",
              CrewGate.Step(In(g, new[] { true, false }, go: true), GatePhase.Holding).Phase != GatePhase.Go, "");

        CrewGateStep held = CrewGate.Step(In(g, new[] { true, true }, nogo: true), GatePhase.GoReady);
        Check("NO-GO holds the mission", held.Phase == GatePhase.NoGo && held.Holding, "");
        Check("a NO-GO hold persists", CrewGate.Step(In(g, new[] { true, true }), GatePhase.NoGo).Phase == GatePhase.NoGo, "");
        Check("GO on a satisfied gate RESUMES from NO-GO",
              CrewGate.Step(In(g, new[] { true, true }, go: true), GatePhase.NoGo).Phase == GatePhase.Go, "");

        CrewGateStep ab = CrewGate.Step(In(g, new[] { true, true }, abort: true), GatePhase.GoReady);
        Check("ABORT is commanded", ab.Phase == GatePhase.Abort && ab.Aborted, "");
        Check("ABORT is absorbing", CrewGate.Step(In(g, new[] { true, true }), GatePhase.Abort).Phase == GatePhase.Abort, "");
        Check("a cleared gate stays cleared", CrewGate.Step(In(g, new[] { true, true }), GatePhase.Go).Cleared, "");

        // ============================ CREW GATE CATALOG ============================
        Gate[] cd = CrewGates.Countdown();
        Check("seven countdown gates", cd.Length == 7, cd.Length.ToString());
        Check("countdown starts at G1 ingress/comm", cd[0].Id == GateId.IngressCommG1, "");
        Check("countdown ends at G7 launch GO", cd[6].Id == GateId.LaunchGoG7, "");
        // G5 arms the launch-escape system (a crew action)
        bool lesArm = false;
        foreach (ChecklistItem it in cd[4].Items) if (it.Kind == ItemKind.CrewAck && it.Label.Contains("ARM")) lesArm = true;
        Check("G5 has a crew ARM-the-LES action", cd[4].Id == GateId.LesArmG5 && lesArm, "");
        // G7 has the crew's GO
        bool crewGo = false;
        foreach (ChecklistItem it in cd[6].Items) if (it.Kind == ItemKind.CrewAck && it.Label.Contains("crew")) crewGo = true;
        Check("G7 has 'Dragon crew — GO'", crewGo, "");

        Gate[] apr = CrewGates.Approach();
        Check("five prox-ops gates G9..G13", apr.Length == 5 && apr[0].Id == GateId.ApproachInitGoG9 && apr[4].Id == GateId.DockingCompleteG13, "");

        Check("ISS return has undock + deorbit gates", CrewGates.Return(true).Length == 2, "");
        Check("free-flyer return has only the deorbit gate",
              CrewGates.Return(false).Length == 1 && CrewGates.Return(false)[0].Id == GateId.DeorbitGoG15, "");

        MissionProfile iss = Missions.Resolve("Crew-2");
        MissionProfile free = Missions.Resolve("Inspiration4");
        Check("catalog resolves Crew-2 (rendezvous) and Inspiration4 (free-flyer)", iss.HasRendezvous && !free.HasRendezvous, "");
        Check("ById finds a countdown gate", CrewGates.ById(iss, GateId.LesArmG5).Id == GateId.LesArmG5, "");
        Check("ById finds an approach gate for an ISS mission", CrewGates.ById(iss, GateId.WP0HoldG10).Items != null, "");
        Check("ById returns none for an approach gate on a FREE-FLYER", CrewGates.ById(free, GateId.WP0HoldG10).Items == null, "");

        // ============================ MODE MANAGER (conductor) ============================
        MissionStep[] planIss = ModeManager.Plan(iss);
        MissionStep[] planFree = ModeManager.Plan(free);

        Check("ISS plan opens with the 7 countdown gates then ascent",
              planIss[0].Gate == GateId.IngressCommG1 && planIss[6].Gate == GateId.LaunchGoG7
              && planIss[7].Kind == StepKind.Fly && planIss[7].Phase == MissionPhase.Ascent, "");
        Check("ISS plan contains the approach holds + undock gate", HasGate(planIss, GateId.WP0HoldG10) && HasGate(planIss, GateId.UndockGoG14), "");
        Check("ISS plan ends with the chute/splashdown fly step",
              planIss[planIss.Length - 1].Kind == StepKind.Fly && planIss[planIss.Length - 1].Phase == MissionPhase.Drogues, "");

        Check("free-flyer plan has NO rendezvous/undock gates",
              !HasGate(planFree, GateId.ApproachInitGoG9) && !HasGate(planFree, GateId.UndockGoG14), "");
        Check("free-flyer plan has a free-flight coast + the deorbit gate",
              HasFly(planFree, MissionPhase.Coast) && HasGate(planFree, GateId.DeorbitGoG15), "");
        Check("GateAt reports the first gate", ModeManager.GateAt(planIss, 0) == GateId.IngressCommG1, "");

        // a gate does not advance without the crew's GO
        ModeStep hold = ModeManager.Advance(planIss, 0, new ModeInputs());
        Check("a gate HOLDS without GO (index unchanged)", hold.Holding && hold.Index == 0, hold.Index.ToString());
        ModeStep adv = ModeManager.Advance(planIss, 0, new ModeInputs { GateGo = true });
        Check("crew GO advances past the gate", adv.Index == 1, adv.Index.ToString());

        // a fly phase advances only when the phase reports complete
        int ascentIdx = 7;
        Check("a fly phase holds until PhaseComplete",
              ModeManager.Advance(planIss, ascentIdx, new ModeInputs()).Index == ascentIdx, "");
        Check("PhaseComplete advances the fly phase",
              ModeManager.Advance(planIss, ascentIdx, new ModeInputs { PhaseComplete = true }).Index == ascentIdx + 1, "");

        // abort is absorbing
        Check("abort at any step is flagged", ModeManager.Advance(planIss, 5, new ModeInputs { GateAbort = true }).Aborted, "");

        // a full walk (clear every gate, complete every fly) reaches Complete without skipping a gate
        Check("a full mission walk reaches Complete", FullWalk(planIss), "");
        Check("a full free-flyer walk reaches Complete", FullWalk(planFree), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    static bool HasGate(MissionStep[] plan, GateId id)
    { foreach (MissionStep s in plan) if (s.Kind == StepKind.Gate && s.Gate == id) return true; return false; }
    static bool HasFly(MissionStep[] plan, MissionPhase p)
    { foreach (MissionStep s in plan) if (s.Kind == StepKind.Fly && s.Phase == p) return true; return false; }

    static bool FullWalk(MissionStep[] plan)
    {
        int idx = 0, guard = 0;
        while (guard++ < 200)
        {
            ModeInputs mi = new ModeInputs();
            if (idx < plan.Length && plan[idx].Kind == StepKind.Gate) mi.GateGo = true;
            else mi.PhaseComplete = true;
            ModeStep r = ModeManager.Advance(plan, idx, mi);
            if (r.Aborted) return false;
            idx = r.Index;
            if (r.Complete) return true;
        }
        return false;
    }
}
