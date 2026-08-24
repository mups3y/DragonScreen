/*
 * Tests for the mission conductor's pure decision logic (pure/AutoSequenceCore.cs). Drives whole
 * missions through Begin/Advance with a struct and asserts every hand-off, the return-leg selection,
 * that the REFUEL gate is the capsule-full signal (not aggregate propellant), and that a dropped phase
 * bails back to manual.
 */
using System;
using DragonScreen;

public static class AutoSequenceTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    // A clean in-orbit, undocked, S2-separated state; tweak per test.
    static SeqInputs InOrbit()
    {
        SeqInputs s = new SeqInputs();
        s.InStableOrbit = true;
        s.S2Gone = true;
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen mission-conductor tests");

        // ---- Begin picks the right leg from state ----
        SeqInputs pad = new SeqInputs();                                  // no orbit, on the pad
        Check("Begin on the pad -> Ascent", AutoSequenceCore.Begin(pad) == SeqStep.Ascent, "");

        SeqInputs orbit = InOrbit();
        Check("Begin in orbit, undocked, not armed -> Rendezvous",
              AutoSequenceCore.Begin(orbit) == SeqStep.Rendezvous, "");

        SeqInputs docked = InOrbit(); docked.Docked = true;
        Check("Begin already docked -> Refuel",
              AutoSequenceCore.Begin(docked) == SeqStep.Refuel, "");

        SeqInputs armed = InOrbit(); armed.ReturnArmed = true;
        Check("Begin in orbit + ReturnArmed -> Deorbit (the ride home)",
              AutoSequenceCore.Begin(armed) == SeqStep.Deorbit, "");

        SeqInputs armedDocked = InOrbit(); armedDocked.ReturnArmed = true; armedDocked.Docked = true;
        Check("Begin ReturnArmed but still docked -> Refuel, NOT Deorbit",
              AutoSequenceCore.Begin(armedDocked) == SeqStep.Refuel, "");

        SeqInputs ascentInOrbitOnly = InOrbit(); ascentInOrbitOnly.S2Gone = false;
        Check("Begin in orbit but S2 still on -> Ascent (insertion not finished)",
              AutoSequenceCore.Begin(ascentInOrbitOnly) == SeqStep.Ascent, "");

        // ---- Full OUTBOUND run, step by step ----
        // Ascent holds while engaged and sub-orbital.
        SeqInputs climbing = new SeqInputs(); climbing.AscentEngaged = true;
        Check("Ascent holds while climbing under power",
              AutoSequenceCore.Advance(SeqStep.Ascent, climbing).Step == SeqStep.Ascent, "");

        // Ascent -> Rendezvous at insertion.
        SeqInputs inserted = InOrbit(); inserted.AscentEngaged = true;
        Check("Ascent -> Rendezvous at insertion (orbit + S2 gone)",
              AutoSequenceCore.Advance(SeqStep.Ascent, inserted).Step == SeqStep.Rendezvous, "");

        // Rendezvous holds while its controller runs, -> Refuel at dock.
        SeqInputs closing = InOrbit(); closing.RendezvousEngaged = true;
        Check("Rendezvous holds while closing",
              AutoSequenceCore.Advance(SeqStep.Rendezvous, closing).Step == SeqStep.Rendezvous, "");
        SeqInputs berthed = InOrbit(); berthed.RendezvousEngaged = true; berthed.Docked = true;
        Check("Rendezvous -> Refuel at dock",
              AutoSequenceCore.Advance(SeqStep.Rendezvous, berthed).Step == SeqStep.Refuel, "");

        // ⛔ NO ISS REFUEL (real Crew-2 carries its own return propellant): DOCKED = outbound complete,
        // the conductor goes idle at once and arms the return - it does NOT wait to "fill" a tank.
        SeqInputs berthedDone = InOrbit(); berthedDone.Docked = true;
        SeqResult done = AutoSequenceCore.Advance(SeqStep.Refuel, berthedDone);
        Check("docked -> Done AND arms the return, immediately (no refuel wait)",
              done.Done && !done.Bail && done.ArmReturn, "");
        // It does NOT depend on any fill/stall signal - a tank reading not-full still completes at dock.
        SeqInputs notFull = InOrbit(); notFull.Docked = true; notFull.RefuelFull = false;
        SeqResult notFullDone = AutoSequenceCore.Advance(SeqStep.Refuel, notFull);
        Check("docked but tank not 'full' -> still Done + arm (no ISS refuel to wait on)",
              notFullDone.Done && notFullDone.ArmReturn, "");
        // Falls off the port before completing -> bail back to manual.
        SeqInputs fellOff = InOrbit(); fellOff.Docked = false;
        Check("undocked mid-Refuel -> Bail",
              AutoSequenceCore.Advance(SeqStep.Refuel, fellOff).Bail, "");

        // ---- RETURN leg ----
        SeqInputs deorbiting = InOrbit(); deorbiting.ReturnActive = true;
        Check("Deorbit holds while the return controllers run",
              AutoSequenceCore.Advance(SeqStep.Deorbit, deorbiting).Step == SeqStep.Deorbit, "");
        SeqInputs home = new SeqInputs(); home.Landed = true;
        SeqResult splashed = AutoSequenceCore.Advance(SeqStep.Deorbit, home);
        Check("Deorbit -> Done at splashdown (and does NOT re-arm)",
              splashed.Done && !splashed.Bail && !splashed.ArmReturn, "");

        // ---- BAIL: a dropped phase hands back to manual ----
        SeqInputs ascentCancelled = new SeqInputs();   // climbing, but ascent no longer engaged
        Check("Ascent cancelled before orbit -> Bail",
              AutoSequenceCore.Advance(SeqStep.Ascent, ascentCancelled).Bail, "");
        SeqInputs rndzCancelled = InOrbit();           // in orbit, nothing engaged, not docked
        Check("Rendezvous cancelled -> Bail",
              AutoSequenceCore.Advance(SeqStep.Rendezvous, rndzCancelled).Bail, "");
        SeqInputs cameOff = InOrbit();                 // in the Refuel step but no longer docked
        Check("Refuel loses the dock -> Bail",
              AutoSequenceCore.Advance(SeqStep.Refuel, cameOff).Bail, "");
        SeqInputs returnCancelled = InOrbit();         // in Deorbit, return controllers dropped, not landed
        Check("Return cancelled before entry -> Bail",
              AutoSequenceCore.Advance(SeqStep.Deorbit, returnCancelled).Bail, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
