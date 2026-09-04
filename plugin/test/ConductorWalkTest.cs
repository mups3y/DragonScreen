/*
 * ConductorWalkTest — W10, 2026-09-05.
 *
 * WHAT THIS PROVES, AND WHAT IT CANNOT. `src/CrewProcedureOps.cs` is GLUE: its tick takes a live `Vessel`,
 * so no headless suite can execute it (`build.py test` compiles it via `build_plugin()` and runs `src/pure`
 * + `test` only). What IS decidable without the game is the COMPOSITION its tick performs — satisfy the
 * checklist, run `CrewGate.Step` exactly once, and on a cleared gate run `ModeManager.Advance` — and that
 * composition is what W10's done-criteria are about. This suite walks it step for step against the REAL
 * gate catalog and the REAL mission plan, so the contract is pinned:
 *
 *   • AUTO SEQUENCE engages a conductor that ACTUALLY ADVANCES ITS GATES — seven GO presses walk G1..G7.
 *   • GO IS CONSUMED ON THE FRAME IT IS PRESSED — one press clears exactly one gate; the next tick with no
 *     press clears nothing; and a GO on an unsatisfied checklist is discarded, never remembered.
 *   • NOTHING CLAIMS A PHASE THE VEHICLE IS NOT IN — once the plan reaches a Fly step with no controller
 *     behind it, rule T4's resolver falls back to the live classifier.
 *   • WHY `AutoAdvanceGates` SHIPS FALSE — with the crew's taps and GO synthesised, the whole countdown
 *     clears itself in seven consecutive ticks with no crew input at all. That is the runaway the flag
 *     causes, asserted rather than described.
 *
 * ⛔ This is a CONTRACT test, not an execution of the glue. If `CrewProcedureOps.Tick` is ever changed so
 * that it no longer composes these pieces in this order, this suite will still pass — read the glue.
 */
using System;
using DragonScreen;

public static class ConductorWalkTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL " + what + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]")); }
    }

    // The decidable half of one CrewProcedureOps.Tick at a GATE step, in the glue's own order.
    // `satisfied` is mutated exactly as the glue mutates it. Returns the (possibly advanced) plan index.
    static int TickGate(MissionProfile m, MissionStep[] plan, int index, ref bool[] satisfied,
                        ref GatePhase phase, bool autoAdvance, bool goPressed, bool noGoPressed,
                        out bool cleared, out bool launchPending)
    {
        cleared = false; launchPending = false;
        if (plan[index].Kind != StepKind.Gate) return index;

        Gate gate = CrewGates.ById(m, plan[index].Gate);

        // AUTO items satisfy from vessel state. Headless there is no vessel, and the glue's own fallback for
        // an unmeasurable confirmation is `true` (nominal on a healthy pad) — the same value used here.
        if (gate.Items != null)
            for (int i = 0; i < gate.Items.Length && i < satisfied.Length; i++)
                if (gate.Items[i].Kind == ItemKind.Auto) satisfied[i] = true;

        // the hands-off flag: synthesise the crew's taps AND the GO press
        if (autoAdvance)
        {
            if (gate.Items != null)
                for (int i = 0; i < gate.Items.Length && i < satisfied.Length; i++)
                    if (gate.Items[i].Kind == ItemKind.CrewAck) satisfied[i] = true;
            goPressed = true;
        }

        CrewGateInputs gi;
        gi.Gate = gate; gi.Satisfied = satisfied;
        gi.GoPressed = goPressed; gi.NoGoPressed = noGoPressed;
        gi.AbortPressed = false;   // W10: PressAbort is an honest no-op until W19 — never latch a red ABORT
        CrewGateStep step = CrewGate.Step(gi, phase);
        phase = step.Phase;
        // the press is consumed here, unconditionally, after exactly one Step call — the caller's `goPressed`
        // is a by-value parameter, which is precisely the glue's "cleared every tick" behaviour.

        if (!step.Cleared) return index;

        cleared = true;
        if (gate.Id == GateId.LaunchGoG7) launchPending = true;
        ModeStep ms = ModeManager.Advance(plan, index, new ModeInputs { GateGo = true });
        int next = ms.Index;
        LoadGate(m, plan, next, out satisfied, out phase);
        return next;
    }

    static void LoadGate(MissionProfile m, MissionStep[] plan, int index, out bool[] satisfied, out GatePhase phase)
    {
        phase = GatePhase.Holding;
        if (index < plan.Length && plan[index].Kind == StepKind.Gate)
        {
            Gate g = CrewGates.ById(m, plan[index].Gate);
            satisfied = new bool[(g.Items == null) ? 0 : g.Items.Length];
        }
        else satisfied = null;
    }

    // Tap every CrewAck item of the gate at `index` — the crew working the checklist.
    static void CrewTapAll(MissionProfile m, MissionStep[] plan, int index, bool[] satisfied)
    {
        Gate g = CrewGates.ById(m, plan[index].Gate);
        if (g.Items == null) return;
        for (int i = 0; i < g.Items.Length && i < satisfied.Length; i++)
            if (g.Items[i].Kind == ItemKind.CrewAck) satisfied[i] = true;
    }

    public static int Run()
    {
        Console.WriteLine("ConductorWalkTest (W10: the conductor's gate walk, and why AutoAdvanceGates ships false)");

        MissionProfile iss = Missions.Resolve("Crew-2");
        Check("the test mission resolves", iss.Valid, iss.Name);
        MissionStep[] plan = ModeManager.Plan(iss);

        // ---- the first Fly step: where the plan parks when no controller can complete a phase ----
        int firstFly = -1;
        for (int i = 0; i < plan.Length; i++) if (plan[i].Kind == StepKind.Fly) { firstFly = i; break; }
        Check("the plan opens with gates, then a Fly step", firstFly == 7, "firstFly=" + firstFly);
        Check("that first Fly step is Ascent", plan[firstFly].Phase == MissionPhase.Ascent, "");

        // ================= 1. THE INTERACTIVE WALK — the shipped configuration =================
        {
            int index = 0;
            bool[] sat; GatePhase phase;
            LoadGate(iss, plan, index, out sat, out phase);
            bool cleared, launch;
            int gatesCleared = 0, launchSignals = 0;

            for (int press = 0; press < 7; press++)
            {
                int before = index;

                // (a) ONCE, at the first gate: a GO with the checklist NOT complete must not clear it, and
                //     must not be remembered into the next tick.
                if (press == 0)
                {
                    index = TickGate(iss, plan, index, ref sat, ref phase, false, true, false, out cleared, out launch);
                    Check("GO on an unsatisfied checklist does not clear the gate", !cleared && index == before, "");
                    index = TickGate(iss, plan, index, ref sat, ref phase, false, false, false, out cleared, out launch);
                    Check("the discarded GO is not remembered on the next tick", !cleared && index == before, "");
                }

                // (b) the crew works the checklist. A COMPLETE checklist alone does not clear the gate — and
                //     this tick is also where a GO carried over from the previous gate would show itself.
                CrewTapAll(iss, plan, index, sat);
                index = TickGate(iss, plan, index, ref sat, ref phase, false, false, false, out cleared, out launch);
                Check("a complete checklist with no GO does not clear gate " + press,
                      !cleared && index == before, "before=" + before + " after=" + index);
                Check("the previous gate's GO did not carry into gate " + press, index == before, "");

                // (c) one GO press, one gate cleared.
                index = TickGate(iss, plan, index, ref sat, ref phase, false, true, false, out cleared, out launch);
                if (cleared) gatesCleared++;
                if (launch) launchSignals++;
                Check("GO on a complete checklist clears gate " + press, cleared && index == before + 1,
                      "before=" + before + " after=" + index);
            }

            Check("seven GO presses clear the seven countdown gates", gatesCleared == 7, "cleared=" + gatesCleared);
            Check("the walk lands on the first Fly step", index == firstFly, "index=" + index);
            Check("clearing G7 raises exactly one launch signal", launchSignals == 1, "signals=" + launchSignals);
        }

        // ================= 2. THE RUNAWAY — why AutoAdvanceGates ships FALSE =================
        // Same plan, same gates, but the crew's taps and GO are synthesised: seven consecutive ticks with NO
        // crew input at all clear the whole countdown. That is what `true` did, and why the interactive gates
        // it makes decorative are restored by shipping it false.
        {
            int index = 0;
            bool[] sat; GatePhase phase;
            LoadGate(iss, plan, index, out sat, out phase);
            bool cleared, launch;
            int ticks = 0;
            while (index < plan.Length && plan[index].Kind == StepKind.Gate && ticks < 50)
            {
                index = TickGate(iss, plan, index, ref sat, ref phase, true, false, false, out cleared, out launch);
                ticks++;
            }
            Check("AutoAdvanceGates=true clears all seven gates with zero crew input", index == firstFly, "index=" + index);
            Check("...and does it in seven ticks", ticks == 7, "ticks=" + ticks);
        }

        // ================= 3. NO-GO HOLDS, AND A FRESH GO RESUMES =================
        {
            int index = 0;
            bool[] sat; GatePhase phase;
            LoadGate(iss, plan, index, out sat, out phase);
            bool cleared, launch;
            CrewTapAll(iss, plan, index, sat);
            index = TickGate(iss, plan, index, ref sat, ref phase, false, false, true, out cleared, out launch);
            Check("NO-GO holds the mission at the gate", !cleared && phase == GatePhase.NoGo && index == 0, "");
            index = TickGate(iss, plan, index, ref sat, ref phase, false, true, false, out cleared, out launch);
            Check("a fresh GO resumes from the NO-GO hold", cleared && index == 1, "index=" + index);
        }

        // ================= 4. NOTHING CLAIMS A PHASE THE VEHICLE IS NOT IN =================
        // At the parked Fly step the host has no controller, so the conductor publishes Unknown and rule T4's
        // resolver hands the phase word back to the live classifier. Asserting the resolver's half here keeps
        // the two ends of that contract in one place.
        Check("engaged + Unknown active phase -> the live classifier wins",
              Mission.AuthoritativePhase(true, MissionPhase.Unknown, MissionPhase.Prelaunch) == MissionPhase.Prelaunch, "");
        Check("...so a conductor parked on Ascent over a pad-bound vehicle still reads PRELAUNCH",
              Mission.AuthoritativePhase(true, MissionPhase.Unknown, MissionPhase.Prelaunch) != MissionPhase.Ascent, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
