/*
 * Bridge protocol tests. Headless - no KSP, no Unity, game closed.
 *
 * These matter more than the layout tests. A layout bug is visible the moment you look at the screen;
 * a parser bug shows a plausible-looking wrong number and keeps doing it for the whole flight. The
 * cases below are the ones that would actually happen: a truncated push, a version skew after a
 * partial update, and a message line that happens to contain the separator.
 */
using System;
using F9IScreen;

public static class BridgeTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    static BridgeState Sample()
    {
        BridgeState s = new BridgeState();
        s.Message1 = "Docking to Docking Port..";
        s.Message2 = "range 187 m";
        s.Message3 = "closing 1.4 m/s";
        s.Program = "AutoDocking";
        s.Page = "flight";
        s.DragonPhase = "PLANE MATCH";
        s.StationPhase = "DIRECT APPROACH";
        s.DockingMode = "APPR";
        s.FlightDirectorGo = false;
        s.LandProfile = 2;
        s.MissionName = "Ghidorah 9 - Crew Rodan";
        s.MissionTimer = 1234.5;
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("F9IScreen bridge tests");

        // ---- round trip ----
        BridgeState src = Sample();
        BridgeState dst = new BridgeState();
        Check("well-formed payload is accepted", BridgeProtocol.Unpack(BridgeProtocol.Pack(src), dst), "");
        Check("message1 survives", dst.Message1 == src.Message1, dst.Message1);
        Check("message3 survives", dst.Message3 == src.Message3, dst.Message3);
        Check("program survives", dst.Program == src.Program, dst.Program);
        Check("docking mode survives", dst.DockingMode == src.DockingMode, dst.DockingMode);
        Check("station phase survives", dst.StationPhase == src.StationPhase, dst.StationPhase);
        Check("GO flag survives as false", dst.FlightDirectorGo == false, "");
        Check("land profile survives", dst.LandProfile == 2, dst.LandProfile.ToString());
        Check("mission name survives", dst.MissionName == src.MissionName, dst.MissionName);
        Check("mission timer survives", Math.Abs(dst.MissionTimer - 1234.5) < 0.05,
              dst.MissionTimer.ToString());
        Check("valid flag set after good payload", dst.Valid, "");
        Check("nothing rejected on the happy path", dst.RejectCount == 0, dst.RejectCount.ToString());

        // ---- the failure that matters: a bad payload must NOT corrupt good state ----
        // This is the whole reason Unpack is written to be all-or-nothing. Mid-docking, a stale but
        // correct reading is safe; a half-applied one is not.
        BridgeState keep = new BridgeState();
        BridgeProtocol.Unpack(BridgeProtocol.Pack(src), keep);
        string before1 = keep.Message1, before3 = keep.Message3;

        Check("truncated payload rejected", !BridgeProtocol.Unpack("1~|~only~|~three", keep), "");
        Check("  ... and message1 untouched", keep.Message1 == before1, keep.Message1);
        Check("  ... and message3 untouched", keep.Message3 == before3, keep.Message3);

        Check("empty payload rejected", !BridgeProtocol.Unpack("", keep), "");
        Check("null payload rejected", !BridgeProtocol.Unpack(null, keep), "");
        Check("garbage payload rejected", !BridgeProtocol.Unpack("hello world", keep), "");
        Check("  ... still untouched after garbage", keep.Message1 == before1, keep.Message1);

        // Version skew: kOS updated but the DLL was not (or the reverse). Must refuse, not misparse -
        // a DLL change needs a full KSP restart while a .ks edit only needs a CPU reboot, so the two
        // sides being out of step is not hypothetical, it is the normal development state.
        string wrongVer = BridgeProtocol.Pack(src).Replace("1~|~", "99~|~");
        Check("version skew rejected", !BridgeProtocol.Unpack(wrongVer, keep), "");
        Check("  ... still untouched after skew", keep.Message1 == before1, keep.Message1);

        // Five rejecting calls above: truncated, empty, null, garbage, version skew.
        // (This asserted 6 on the first run and failed - the count was my arithmetic, not a code bug.
        //  Worth keeping exact rather than ">= 1": the point is that EVERY bad payload is counted, and
        //  a loose assertion would not notice one silently passing.)
        Check("every rejected payload was counted", keep.RejectCount == 5, keep.RejectCount.ToString());

        // ---- separator injection ----
        // A message line is free text built from flight data. If one ever contained the separator it
        // would shift every later field by one - the panel would show the docking mode in the mission
        // name slot and nobody would immediately know why.
        BridgeState evil = Sample();
        evil.Message1 = "range~|~187 m";
        BridgeState got = new BridgeState();
        Check("payload with injected separator still parses",
              BridgeProtocol.Unpack(BridgeProtocol.Pack(evil), got), "");
        Check("  ... separator was neutralised", !got.Message1.Contains(BridgeProtocol.Separator),
              got.Message1);
        Check("  ... and later fields did NOT shift", got.DockingMode == "APPR", got.DockingMode);
        Check("  ... mission name intact", got.MissionName == evil.MissionName, got.MissionName);

        // ---- empty strings are legal, not malformed ----
        BridgeState blank = new BridgeState();
        BridgeState blankOut = new BridgeState();
        Check("all-empty payload is still well formed",
              BridgeProtocol.Unpack(BridgeProtocol.Pack(blank), blankOut), "");
        Check("  ... empty message stays empty", blankOut.Message1 == "", blankOut.Message1);

        // ---- land profile naming, which audit issue #55 showed is easy to get wrong ----
        Check("profile 1 is RTLS", BridgeProtocol.LandProfileName(1) == "RTLS", "");
        Check("profile 2 is droneship", BridgeProtocol.LandProfileName(2) == "DRONESHIP", "");
        Check("profile 3 is EXPENDABLE, not a landing profile",
              BridgeProtocol.LandProfileName(3) == "EXPENDABLE", BridgeProtocol.LandProfileName(3));
        Check("profile 6 is EXPENDABLE", BridgeProtocol.LandProfileName(6) == "EXPENDABLE", "");
        Check("unknown profile is flagged, not silently defaulted",
              BridgeProtocol.LandProfileName(4).StartsWith("?"), BridgeProtocol.LandProfileName(4));

        // ---- culture safety ----
        // The mission timer is formatted invariant on purpose. On a comma-decimal locale a plain
        // ToString() would emit "1234,5", which would then fail to parse and silently freeze the clock.
        Check("timer packs with a dot decimal",
              BridgeProtocol.Pack(src).Contains("1234.5"), "");

        // ---- THE PAYLOAD kOS ACTUALLY SENDS ----
        // Everything above tests Pack against Unpack, which is C# talking to itself: a field order
        // swapped in BOTH would pass every one of them. The real sender is hand-written kOS in
        // falcon9.ks (F9BridgePush) and it never runs here, so this literal is the ONLY place the two
        // sides are checked against each other. Transcribed field for field from the concatenation in
        // that function - if you change one, change the other and expect this test to tell you.
        //
        // Note the shape of the values: rich-text markup in the messages, a phase name from
        // dragon_deorbit.ks, "None" for an idle program, and a T-0 EPOCH in the last field rather than
        // an elapsed count. That last one is why on-change pushing works at all.
        const string fromKos =
            "1~|~<b><color=red>ABORT</color></b>~|~second line~|~~|~" +
            "Falcon Ascent~|~flight~|~coast~|~idle~|~None~|~1~|~2~|~CRS-30~|~1234.5";

        BridgeState kos = new BridgeState();
        Check("the literal kOS payload parses", BridgeProtocol.Unpack(fromKos, kos), fromKos);
        Check("  ... rich text survives intact",
              kos.Message1 == "<b><color=red>ABORT</color></b>", kos.Message1);
        Check("  ... an EMPTY message is a field, not a missing one", kos.Message3 == "", kos.Message3);
        Check("  ... program", kos.Program == "Falcon Ascent", kos.Program);
        Check("  ... page", kos.Page == "flight", kos.Page);
        Check("  ... dragon phase", kos.DragonPhase == "coast", kos.DragonPhase);
        Check("  ... station phase", kos.StationPhase == "idle", kos.StationPhase);
        Check("  ... docking mode defaults to None before AutoDocking runs",
              kos.DockingMode == "None", kos.DockingMode);
        Check("  ... land profile 2 reads as droneship",
              kos.LandProfile == 2 && BridgeProtocol.LandProfileName(kos.LandProfile) == "DRONESHIP",
              kos.LandProfile.ToString());
        Check("  ... mission name", kos.MissionName == "CRS-30", kos.MissionName);
        Check("  ... T-0 epoch", Math.Abs(kos.MissionTimer - 1234.5) < 1e-6, kos.MissionTimer.ToString());
        Check("  ... nothing was rejected", kos.RejectCount == 0, kos.RejectCount.ToString());

        // ---- a comma-decimal timer, which is what kOS emits on a European locale ----
        // This is the failure this parser is most likely to actually meet in the wild, and the one it
        // would hide best: the clock just stops, everything else keeps updating, nothing is rejected.
        BridgeState euro = new BridgeState();
        euro.MissionTimer = 999.0;
        Check("comma-decimal timer is accepted",
              BridgeProtocol.Unpack(fromKos.Replace("1234.5", "1234,5"), euro), "");
        Check("  ... and reads as the same number a dot would give",
              Math.Abs(euro.MissionTimer - 1234.5) < 1e-6, euro.MissionTimer.ToString());
        Check("  ... without being counted as a reject", euro.RejectCount == 0,
              euro.RejectCount.ToString());

        // Exponent notation - kOS renders a large game time this way, and a mission clock is exactly
        // the field big enough to hit it.
        BridgeState expo = new BridgeState();
        Check("exponent-notation timer is accepted",
              BridgeProtocol.Unpack(fromKos.Replace("1234.5", "1.2345E+07"), expo), "");
        Check("  ... reads as 12345000", Math.Abs(expo.MissionTimer - 12345000.0) < 1e-3,
              expo.MissionTimer.ToString());

        // A timer that is genuine nonsense must NOT overwrite a good reading. Freezing a clock is bad;
        // replacing it with zero mid-flight is worse.
        BridgeState keepTimer = new BridgeState();
        BridgeProtocol.Unpack(fromKos, keepTimer);
        Check("junk timer leaves the last good value in place",
              BridgeProtocol.Unpack(fromKos.Replace("1234.5", "nope"), keepTimer)
              && Math.Abs(keepTimer.MissionTimer - 1234.5) < 1e-6, keepTimer.MissionTimer.ToString());

        // kOS counts the separators, we count the fields; a mismatch here means one side grew a field.
        int seps = 0, at = 0;
        while ((at = fromKos.IndexOf(BridgeProtocol.Separator, at)) >= 0)
        { seps++; at += BridgeProtocol.Separator.Length; }
        Check("kOS emits exactly FieldCount-1 separators", seps == BridgeProtocol.FieldCount - 1,
              seps + " separators for " + BridgeProtocol.FieldCount + " fields");

        Console.WriteLine(failures == 0
            ? "  " + checks + " checks, all passed"
            : "  " + checks + " checks, " + failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
