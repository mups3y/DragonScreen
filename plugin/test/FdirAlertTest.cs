/*
 * Tests for the FDIR → crew-alert-channel mapping (pure/Alarms.FdirSeverity + Fdir.FaultName).
 *
 * Pins the §4.2 fix: the REAL fault spine (pure/Fdir.cs) reaches the screen's alert severity, instead of
 * the display inventing alerts. Degraded/abort recovery = ALARM; local recovery = CAUTION; no fault = nominal.
 */
using DragonScreen;
using System;

public static class FdirAlertTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen FDIR->alert (fault spine to crew channel) tests");

        // no fault → nominal
        Check("no fault is nominal",
              Alarms.FdirSeverity(FaultKind.None, Recovery.Continue) == Severity.Nominal, "");

        // a fault still handled locally → caution
        Check("retry is caution",
              Alarms.FdirSeverity(FaultKind.ThrustShortfall, Recovery.Retry) == Severity.Caution, "");
        Check("reconfigure is caution",
              Alarms.FdirSeverity(FaultKind.ThrustShortfall, Recovery.Reconfigure) == Severity.Caution, "");
        Check("replan is caution",
              Alarms.FdirSeverity(FaultKind.TrajectoryDivergence, Recovery.Replan) == Severity.Caution, "");

        // degraded / abort → alarm
        Check("downmode is alarm",
              Alarms.FdirSeverity(FaultKind.ResourceCritical, Recovery.Downmode) == Severity.Alarm, "");
        Check("abort is alarm",
              Alarms.FdirSeverity(FaultKind.KeepOutBreach, Recovery.Abort) == Severity.Alarm, "");
        Check("safemode is alarm",
              Alarms.FdirSeverity(FaultKind.ResourceCritical, Recovery.SafeMode) == Severity.Alarm, "");

        // a present fault is never dismissed as nominal
        Check("a present fault is never nominal",
              Alarms.FdirSeverity(FaultKind.ConvergenceStall, Recovery.Continue) == Severity.Caution, "");

        // fault names (crew-facing)
        Check("thrust name", Fdir.FaultName(FaultKind.ThrustShortfall) == "THRUST SHORTFALL", "");
        Check("loss-of-control name", Fdir.FaultName(FaultKind.NoControlSolution) == "LOSS OF CONTROL", "");
        Check("keep-out name", Fdir.FaultName(FaultKind.KeepOutBreach) == "KEEP-OUT BREACH", "");
        Check("none name is NOMINAL", Fdir.FaultName(FaultKind.None) == "NOMINAL", "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
