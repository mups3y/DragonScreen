/*
 * Tests for the authoritative-phase resolver (pure/MissionPhase.cs Mission.AuthoritativePhase).
 *
 * Pins rule T4: while the autopilot flies a KNOWN phase, the DISPLAY shows the FSM's phase, never the
 * independent Classify() shadow — so the screen and the autopilot can't disagree. Disengaged, or between
 * phases (ActivePhase == Unknown), the live classifier is the honest fallback.
 */
using DragonScreen;
using System;

public static class MissionPhaseTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen MissionPhase (authoritative phase) tests");

        // engaged + a known FSM phase → the FSM phase wins, never the classifier
        Check("engaged flying Approach beats classifier Phasing",
              Mission.AuthoritativePhase(true, MissionPhase.Approach, MissionPhase.Phasing) == MissionPhase.Approach, "");
        Check("engaged flying Entry beats classifier Coast",
              Mission.AuthoritativePhase(true, MissionPhase.Entry, MissionPhase.Coast) == MissionPhase.Entry, "");

        // engaged but at a gate (ActivePhase == Unknown) → classifier fallback
        Check("engaged at a gate falls back to the classifier",
              Mission.AuthoritativePhase(true, MissionPhase.Unknown, MissionPhase.Ascent) == MissionPhase.Ascent, "");

        // disengaged → classifier (manual/idle: no FSM phase to trust)
        Check("disengaged uses the classifier",
              Mission.AuthoritativePhase(false, MissionPhase.Docked, MissionPhase.Prelaunch) == MissionPhase.Prelaunch, "");
        Check("disengaged uses the classifier even with a stale FSM phase",
              Mission.AuthoritativePhase(false, MissionPhase.Approach, MissionPhase.Coast) == MissionPhase.Coast, "");

        // agreement passes through
        Check("agreement (both Ascent) passes through",
              Mission.AuthoritativePhase(true, MissionPhase.Ascent, MissionPhase.Ascent) == MissionPhase.Ascent, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
