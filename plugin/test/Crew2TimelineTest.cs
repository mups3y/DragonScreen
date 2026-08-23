// Tests for the Crew-2 mission clock - the reference our flight is synced against. See pure/Crew2Timeline.cs.
using System;
using DragonScreen;

public static class Crew2TimelineTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen Crew-2 timeline tests");

        Check("before liftoff there is no current event", Crew2Timeline.Current(-5.0).Name == "-", "");
        Check("liftoff at T+0", Crew2Timeline.Current(0.0).Name == "LIFTOFF", "");
        Check("at T+2:37 (157 s) the current event is MECO (T+2:36)",
              Crew2Timeline.Current(157.0).Name == "MECO", Crew2Timeline.Current(157.0).Name);
        Check("...and the next event is STAGE SEP (T+2:39)",
              Crew2Timeline.Next(157.0, out Crew2Event n2) && n2.Name == "STAGE SEP", "");
        Check("at T+2:40 (160 s) the current event has advanced to STAGE SEP",
              Crew2Timeline.Current(160.0).Name == "STAGE SEP", Crew2Timeline.Current(160.0).Name);

        // next + time-to-next
        Crew2Event n;
        Check("just after liftoff the next event is MAX Q", Crew2Timeline.Next(1.0, out n) && n.Name == "MAX Q", "");
        Check("time-to-next MAX Q from T+2 is 60 s",
              Math.Abs(Crew2Timeline.TimeToNext(2.0) - 60.0) < 1e-9, Crew2Timeline.TimeToNext(2.0).ToString());
        Check("after the last event there is no next",
              !Crew2Timeline.Next(9999.0, out n), "");
        Check("...and time-to-next is NaN", double.IsNaN(Crew2Timeline.TimeToNext(9999.0)), "");

        // the real marks are the flown ones
        Check("MECO is at T+2:36 (156 s)", Math.Abs(Crew2Timeline.Current(156.0).TPlusS - 156.0) < 1e-9, "");
        Check("SECO-1 is at T+8:47 (527 s)",
              Crew2Timeline.Current(527.0).Name.StartsWith("SECO"), Crew2Timeline.Current(527.0).Name);
        Check("Dragon sep is at T+11:58 (718 s)",
              Crew2Timeline.Current(718.0).Name == "DRAGON SEPARATION", "");

        // sync error: late = positive, early = negative
        Check("reaching MECO 4 s late reads +4 s",
              Math.Abs(Crew2Timeline.SyncErrorS("MECO", 160.0) - 4.0) < 1e-9, "");
        Check("reaching MECO 6 s early reads -6 s",
              Math.Abs(Crew2Timeline.SyncErrorS("MECO", 150.0) + 6.0) < 1e-9, "");
        Check("an unknown event name is NaN", double.IsNaN(Crew2Timeline.SyncErrorS("nope", 0.0)), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
