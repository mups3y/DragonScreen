/*
 * DragonScreen headless tests - the lifting-entry controller.
 *
 * Every check here is a flight. These four traps are the whole file:
 *   045  the schedule asked for 11.7 km of overshoot while the capsule was 0.4 km from the target
 *   048/053  differentiating the RANGE instead of the ERROR switched the loop off from 32 km down
 *   025  an unbounded lead term flipped the command's sign at 41 km; splashed 49.3 km short
 *   023  extend has no authority below ~36 km, so commanding it just wastes the descent
 */
using System;
using DragonScreen;

public static class EntryGuidanceTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    static EntryGuideInputs At(double altM, double downErrM, double lzRangeM)
    {
        EntryGuideInputs s = new EntryGuideInputs();
        s.AltitudeM = altM;
        s.DownrangeErrM = downErrM;
        s.LzRangeM = lzRangeM;
        s.LzBearingDeg = 90.0;
        s.TrackBearingDeg = 90.0;       // straight at it
        s.MissM = Math.Abs(downErrM);
        s.DtS = 1.0;
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen entry guidance tests");
        EntryMemory mem = new EntryMemory();

        // ---- TRAP 1: THE MARGIN IS CLAMPED TO THE RANGE STILL AHEAD ----
        // 045: 11.7 km of overshoot demanded while 0.4 km from the target.
        EntryGuideInputs close = At(12038.0, -900.0, 400.0);
        EntryGuideCommand c = EntryGuidance.Update(close, ref mem);
        Check("the schedule cannot ask for more overshoot than there is ground left",
              c.WantLongM <= 400.0 + 1e-6, c.WantLongM.ToString("F1"));
        // Past the target: ahead goes negative, so the profile asks for nothing at all.
        EntryGuideInputs past = At(12038.0, -900.0, 4800.0);
        past.TrackBearingDeg = 270.0;                 // target is behind us
        EntryMemory m2 = new EntryMemory();
        Check("and asks for nothing once the target is behind",
              Math.Abs(EntryGuidance.Update(past, ref m2).WantLongM) < 1e-9, "");
        Check("the ahead projection is signed - positive approaching",
              EntryGuidance.AheadM(close) > 0.0, "");
        Check("negative once past", EntryGuidance.AheadM(past) < 0.0, "");

        // ---- TRAP 4: SHORTEN OR COAST. NEVER EXTEND. ----
        // 023 proved extend does nothing below ~36 km, so asking for it wastes the descent.
        EntryMemory m3 = new EntryMemory();
        EntryGuideInputs shortOfTarget = At(30000.0, 40000.0, 60000.0);   // predicted impact SHORT
        EntryGuideCommand ex = EntryGuidance.Update(shortOfTarget, ref m3);
        Check("being short never commands extend", ex.VerticalCmd <= 0.0,
              ex.VerticalCmd.ToString("F3"));
        Check("and it is reported as below profile instead", ex.BelowProfile, ex.Note);
        Check("with a note that names the cause", ex.Note.Contains("de-orbit aim"), ex.Note);

        // ⚠ "LONG" MEANS LONG OF THE SCHEDULE, NOT OF THE TARGET. At 30 km the profile WANTS about
        // 60 km of overshoot, so a capsule 60 km long is exactly ON profile and the correct command
        // is zero. The first version of this fixture asserted a shorten there and was wrong about
        // what the controller is for - which is the same confusion the whole file exists to prevent.
        EntryMemory onProfile = new EntryMemory();
        EntryGuideInputs nominal = At(30000.0, -60000.0, 60000.0);
        Check("a capsule sitting ON the profile is not corrected",
              Math.Abs(EntryGuidance.Update(nominal, ref onProfile).VerticalCmd) < 1e-9,
              EntryGuidance.Update(nominal, ref onProfile).VerticalCmd.ToString("F3"));

        EntryMemory m4 = new EntryMemory();
        EntryGuideInputs longOfTarget = At(30000.0, -150000.0, 60000.0);  // long BEYOND the schedule
        EntryGuideCommand sh = EntryGuidance.Update(longOfTarget, ref m4);
        Check("long of the SCHEDULE commands shorten", sh.VerticalCmd < 0.0,
              sh.VerticalCmd.ToString("F3"));
        Check("and it saturates at -1, not beyond", sh.VerticalCmd >= -1.0,
              sh.VerticalCmd.ToString("F3"));

        // ---- TRAP 3: THE LEAD IS CAPPED AT HALF THE ERROR ----
        // CrewDragon_025: errNow -7502 with 20*rate = +5596 must still command SHORTEN. Unbounded,
        // the next tick's +5887 flipped the sign and the loop went quiet with 5.7 km correctable.
        EntryMemory m5 = new EntryMemory();
        EntryGuideInputs e25 = At(41000.0, -7502.0, 100000.0);
        e25.LzBearingDeg = 90.0; e25.TrackBearingDeg = 90.0;
        EntryGuidance.Update(e25, ref m5);                   // seed the rate
        // Force a strongly positive rate, as 025 had.
        m5.FilteredRate = 5596.0 / EntryGuidance.LeadS;
        m5.LastError = -9000.0;
        e25.DtS = 1.0;
        EntryGuideCommand c25 = EntryGuidance.Update(e25, ref m5);
        Check("a large positive rate cannot flip the command's sign",
              c25.VerticalCmd <= 0.0, c25.VerticalCmd.ToString("F3"));
        Check("the lead is capped at half the error, so the error still wins",
              Math.Abs(c25.LeadErrorM) >= Math.Abs(-7502.0) * (1.0 - EntryGuidance.LeadFrac) - 1.0,
              c25.LeadErrorM.ToString("F0"));
        Check("and the cap is F9I's half", Math.Abs(EntryGuidance.LeadFrac - 0.5) < 1e-9, "");

        // ---- TRAP 2: THE RATE IS OF THE ERROR, AND IT IS FILTERED ----
        EntryMemory m6 = new EntryMemory();
        EntryGuideInputs r1 = At(40000.0, -20000.0, 100000.0);
        EntryGuidance.Update(r1, ref m6);
        Check("the first call seeds the rate rather than differentiating from nothing",
              m6.HaveRate && Math.Abs(m6.FilteredRate) < 1e-9, m6.FilteredRate.ToString("F3"));
        double before = m6.FilteredRate;
        EntryGuideInputs r2 = At(38000.0, -18000.0, 95000.0);
        r2.DtS = 1.0;
        EntryGuidance.Update(r2, ref m6);
        Check("a later call updates it", Math.Abs(m6.FilteredRate - before) > 1e-9,
              m6.FilteredRate.ToString("F2"));
        Check("but only partly - the filter is 0.7 old, 0.3 new",
              Math.Abs(EntryGuidance.RateFilterOld - 0.7) < 1e-9, "");
        // A sub-interval tick must NOT update the rate: dividing by a tiny dt is how noise becomes
        // a command.
        EntryMemory m7 = new EntryMemory();
        EntryGuidance.Update(At(40000.0, -20000.0, 100000.0), ref m7);
        double keep = m7.FilteredRate;
        EntryGuideInputs quick = At(39990.0, -19990.0, 99990.0);
        quick.DtS = 0.02;
        EntryGuidance.Update(quick, ref m7);
        Check("a 20 ms tick does not update the rate",
              Math.Abs(m7.FilteredRate - keep) < 1e-12, "");

        // ---- LATERAL OUTLIVES VERTICAL ----
        // 025 finished 4.3 km off in cross, 2.4 km of it after the range loop latched.
        EntryMemory m8 = new EntryMemory();
        EntryGuideInputs low = At(8000.0, -500.0, 3000.0);         // below the 12 km range latch
        low.CrossTrackM = 4000.0; low.MissM = 4000.0;
        EntryGuideCommand lc = EntryGuidance.Update(low, ref m8);
        Check("below the range latch the vertical loop is quiet",
              Math.Abs(lc.VerticalCmd) < 1e-9, lc.VerticalCmd.ToString("F3"));
        Check("but the cross-track loop is still working",
              Math.Abs(lc.LateralCmd) > 0.0, lc.LateralCmd.ToString("F3"));
        Check("and it stops at the lateral floor",
              Math.Abs(EntryGuidance.Update(Below(), ref m8).LateralCmd) < 1e-9, "");
        // Nothing to correct means no steering - steering on a good prediction can only spoil it.
        EntryGuideInputs onTarget = At(8000.0, 0.0, 100.0);
        onTarget.CrossTrackM = 4000.0; onTarget.MissM = 10.0;
        EntryMemory m9 = new EntryMemory();
        Check("a prediction already on the LZ is left alone",
              Math.Abs(EntryGuidance.Update(onTarget, ref m9).LateralCmd) < 1e-9, "");

        // ---- THE OPEN-LOOP SIGNATURE ----
        EntryMemory fresh = new EntryMemory();
        Check("a loop that never commanded anything is reported as open loop",
              EntryGuidance.FlewOpenLoop(fresh), "");
        EntryMemory worked = new EntryMemory(); worked.LiftMin = -0.4;
        Check("and one that did is not", !EntryGuidance.FlewOpenLoop(worked), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    static EntryGuideInputs Below()
    {
        EntryGuideInputs s = At(2000.0, -500.0, 3000.0);
        s.CrossTrackM = 4000.0; s.MissM = 4000.0;
        return s;
    }
}
