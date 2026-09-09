/*
 * LaunchToRendezvousTest — S252. ⭐ THE WHOLE CHAIN AS A STATE MACHINE, DRIVEN, NOT ASSERTED AT.
 *
 * 🟢 OWNER, 2026-09-09: *"LOCK ISS AS TARGET! USE ACCENT GUIDANCE LAUNCH TO RENDEZVOUS! SET IT TO THE
 * LAUNCH INTO PLANE OF TARGET! CLICK ENGAGE AUTOPILOT!"*, then *"make it work"*.
 *
 * ⛔⛔ WHY THIS FILE EXISTS AND NOT ANOTHER SOURCE-TEXT CHECK. The rendezvous autopilot never flew,
 * and the reason was a DEFAULT WRITTEN TWICE: `MechConductor`'s field initialiser said
 * `MechJebAutopilot` and its `Reset()` — which runs from `FlightDriver.Start` on every flight scene —
 * said `Conductor`. The fork in `Engage` therefore never took its branch and a complete, tested
 * 100-line runner was unreachable code on every launch.
 * ⚠ AND THE TEST THAT SHOULD HAVE CAUGHT IT PASSED, because it searched the source for the string
 * `SelectRendezvousDrive` — present because the METHOD is defined, whether or not anyone calls it.
 * ⛔ A name-presence check cannot see a caller-less method. So the decisions moved into `src/pure`
 * (`RendezvousDrives.Default`, `AutopilotGating.For`) where a headless test can DRIVE them, and this
 * file walks the transitions the owner named, in order, changing one input at a time.
 *
 * ⚠ WHAT IT DOES NOT CLAIM. It proves the DECISIONS, not the flight: whether MechJeb's autopilot
 * actually reaches the station from 215 km in five phasing orbits is a flight question and the answer
 * comes back from the glass, not from here.
 */
using DragonScreen;
using System;
using System.IO;

public static class LaunchToRendezvousTest
{
    static int checks, failures;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + (detail == "" ? "" : "   " + detail)); }
    }

    public static int Run()
    {
        Console.WriteLine("LaunchToRendezvousTest (S252: the chain from insertion to docking, driven)");
        checks = 0; failures = 0;

        TheDefaultDriveIsOneExpression();
        TheGateWalksTheWholeChain();
        ALiveNodeBurnHoldsTheEngage();
        TheHandOffReadsTheModuleNotTheRange();
        TheCrewCanTakeItManually();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    // ---- 1. the defect itself, as a property ---------------------------------------------------
    static void TheDefaultDriveIsOneExpression()
    {
        // 🟢 The owner's standing choice. ⛔ If this ever reads `Conductor` again, the rendezvous
        // autopilot is unreachable on every flight and nothing else in the suite would say so.
        Check("S252: a fresh flight scene starts on MechJeb's rendezvous autopilot",
              RendezvousDrives.Default == RendezvousDrive.MechJebAutopilot,
              "" + RendezvousDrives.Default);
        // ⭐ AND THE CONDUCTOR'S OWN PATH IS NOT RETIRED — C1.16. It keeps its enum value, so
        // `SelectRendezvousDrive` can still hand it back.
        Check("S252/C1.16: ...and the conductor's own chain is still a drive that can be selected",
              (byte)RendezvousDrive.Conductor == 0 && RendezvousDrive.Conductor != RendezvousDrives.Default,
              "");
    }

    // ---- 2. the walk ---------------------------------------------------------------------------
    static void TheGateWalksTheWholeChain()
    {
        // ⛔ ONE INPUT CHANGES PER STEP. A test that rebuilt the whole state between assertions would
        // prove each verdict and none of the transitions.
        bool hasTarget = false, nodeLive = false, engaged = false, moduleDone = false;
        double rangeM = 900000.0, relSpeedMps = 300.0;

        // (a) insertion complete, no target locked yet -> there is nothing to fly to.
        Check("S252 (a) no target: the autopilot is not engaged and says why",
              AutopilotGating.For(hasTarget, nodeLive, engaged, moduleDone, rangeM, relSpeedMps)
              == AutopilotGate.NoTarget, "");

        // (b) the ISS is locked (step 1 of the owner's four) -> engage.
        hasTarget = true;
        Check("S252 (b) target locked, no node live: ENGAGE",
              AutopilotGating.For(hasTarget, nodeLive, engaged, moduleDone, rangeM, relSpeedMps)
              == AutopilotGate.Engage, "");

        // (c) engaged, still 900 km out -> keep flying. ⛔ NOT complete: range alone must not end it.
        engaged = true;
        Check("S252 (c) engaged and far out: RUNNING",
              AutopilotGating.For(hasTarget, nodeLive, engaged, moduleDone, rangeM, relSpeedMps)
              == AutopilotGate.Running, "");

        // (d) closed to the keep-out sphere but still moving -> STILL running. This is the case a
        //     range-only test would have called done, and it is the one that flies through the KOS.
        rangeM = RendezvousOps.AutopilotHandoffRangeM;
        relSpeedMps = 3.0;
        Check("S252 (d) at the KOS but still closing at 3 m/s: RUNNING, not complete",
              AutopilotGating.For(hasTarget, nodeLive, engaged, moduleDone, rangeM, relSpeedMps)
              == AutopilotGate.Running, "");

        // (e) matched velocities at the sphere -> COMPLETE, and the hand-off begins.
        relSpeedMps = 0.4;
        Check("S252 (e) at the KOS with the relative velocity killed: COMPLETE",
              AutopilotGating.For(hasTarget, nodeLive, engaged, moduleDone, rangeM, relSpeedMps)
              == AutopilotGate.Complete, "");

        // (f) losing the target mid-flight outranks everything — a rendezvous with nothing to
        //     rendezvous with is a stand-down, not a completion.
        Check("S252 (f) losing the target while engaged stands it down",
              AutopilotGating.For(false, nodeLive, engaged, moduleDone, rangeM, relSpeedMps)
              == AutopilotGate.NoTarget, "");
    }

    // ---- 3. the node-deletion hazard, as a property ---------------------------------------------
    static void ALiveNodeBurnHoldsTheEngage()
    {
        // ⛔⛔ `MechJebModuleRendezvousAutopilot.OnModuleEnabled` calls `Vessel.RemoveAllManeuverNodes()`.
        // Engaging it while the conductor's Node Executor is flying a burn deletes the node out from
        // under the burn — two drivers, one set of controls.
        Check("S252 a live conductor node HOLDS the engage",
              AutopilotGating.For(true, true, false, false, 50000.0, 20.0)
              == AutopilotGate.HoldNodeLive, "");
        // ⭐ AND THE HOLD IS ONE TICK, NOT A DEADLOCK. The glue stands the executor down on this
        // verdict; with the burn aborted the same inputs engage.
        Check("S252 ...and once the executor is stood down the very next tick ENGAGES",
              AutopilotGating.For(true, false, false, false, 50000.0, 20.0)
              == AutopilotGate.Engage, "");
        // ⛔ It cannot fire once the autopilot is already flying: the autopilot owns the nodes then,
        // and re-aborting them every tick would be the same fault in the other direction.
        Check("S252 ...and a node existing while it is ALREADY engaged is its own, not ours",
              AutopilotGating.For(true, true, true, false, 50000.0, 20.0)
              == AutopilotGate.Running, "");
    }

    // ---- 4. the module reports its own completion ------------------------------------------------
    static void TheHandOffReadsTheModuleNotTheRange()
    {
        // ⛔ "IT REPORTS ITS OWN COMPLETION ... Read that, do not infer it from range." The module
        // clears its own users when it finishes, so `!ap.Enabled` is the report.
        Check("S252 the module's own 'done' ends the leg even at 40 km",
              AutopilotGating.For(true, false, true, true, 40000.0, 12.0)
              == AutopilotGate.Complete, "");
        // ...and the two signals are genuinely independent: same range and speed, no report, running.
        Check("S252 ...and without that report the same state is still RUNNING",
              AutopilotGating.For(true, false, true, false, 40000.0, 12.0)
              == AutopilotGate.Running, "");
        // ⭐ The arrival test uses MechJeb's OWN 1 m/s, not §B11's 0.2 m/s nulled criterion — two
        // different things, and using the tighter one would leave the leg unfinished at the sphere.
        Check("S252 the arrival speed is MechJeb's 1 m/s, not the nulled 0.2",
              RendezvousOps.HandoffRelativeSpeedMps == 1.0
              && RendezvousOps.HandoffRelativeSpeedMps != RendezvousOps.NulledRelativeSpeedMps, "");
        Check("S252 ...and the hand-off range is the published Keep-Out Sphere",
              RendezvousOps.AutopilotHandoffRangeM == RendezvousOps.KeepOutSphereM, "");
    }

    // ---- 5. the docking hand-off, and the crew's override ----------------------------------------
    static void TheCrewCanTakeItManually()
    {
        // The other half of the chain is already pure: `Conductor.Decide` is what routes the docking
        // autopilot, so the walk continues in the same file rather than in a second vocabulary.
        ConductorInputs s = new ConductorInputs();
        s.Phase = MissionPhase.Approach;
        s.InKeepOutSphere = false;
        ConductorAction a = Conductor.Decide(s);
        Check("S252 (g) outside the KOS the approach chain still owns the vehicle",
              a.Module != ConductorModule.DockingAutopilot, "" + a.Module);

        // (h) crossing the sphere hands to the docking autopilot — §B12.3's own default (O6).
        s.InKeepOutSphere = true;
        a = Conductor.Decide(s);
        Check("S252 (h) inside the KOS the DOCKING AUTOPILOT is engaged",
              a.Verb == ConductorVerb.Engage && a.Module == ConductorModule.DockingAutopilot,
              a.Verb + "/" + a.Module);

        // (i) the crew take it manually -> the autopilot stands down. ⛔ An INPUT: the core never
        //     initiates this, and §B12.5's front-end is what will one day set it.
        s.ManualDockingRequested = true;
        a = Conductor.Decide(s);
        Check("S252 (i) ManualDockingRequested stands the docking autopilot down",
              a.Module != ConductorModule.DockingAutopilot, "" + a.Module);
        Check("S252 ...and it idles rather than re-engaging something else",
              a.Verb == ConductorVerb.Idle, "" + a.Verb);

        // (j) and it is genuinely the input doing it: clear it and the autopilot comes back.
        s.ManualDockingRequested = false;
        a = Conductor.Decide(s);
        Check("S252 (j) clearing the request hands it back — the input is what decides",
              a.Module == ConductorModule.DockingAutopilot, "" + a.Module);
    }
}
