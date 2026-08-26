/*
 * DragonScreen headless tests - RendezvousProgress, the Layer-3 "is the rendezvous STUCK?" monitor.
 *
 * Proves the detector before it is given authority: healthy activity (warp / delivering / slewing /
 * leg-advance / closing) never accumulates a stall, and a genuinely frozen rendezvous latches Degraded
 * then Failed on the real-time clock - the exact fault flight_0826_014654 had no autonomous answer to.
 */
using System;
using DragonScreen;

public static class RendezvousProgressTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    static RvProgressSample S(bool engaged, bool warp, bool node, double remDv, double pErr,
                              double rangeM, int leg)
    {
        RvProgressSample x;
        x.Engaged = engaged; x.WarpActive = warp; x.NodeActive = node;
        x.RemainingDvMps = remDv; x.PointingErrorDeg = pErr; x.RangeM = rangeM; x.LegIndex = leg;
        return x;
    }

    static RvProgressState StepN(RvProgressState st, RvProgressSample x, RvProgressCfg cfg,
                                 double dt, int n)
    {
        for (int i = 0; i < n; i++) st = RendezvousProgress.Step(st, x, cfg, dt);
        return st;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen rendezvous-progress (FDIR stall) tests");
        RvProgressCfg cfg = RendezvousProgress.Default();   // 90 s degrade, 300 s fail

        // ---- not engaged is always healthy and unseeded ----
        RvProgressState off = RendezvousProgress.Step(RendezvousProgress.Fresh(),
                                  S(false, false, false, 0, 0, 4000000, 0), cfg, 1.0);
        Check("not engaged -> Nominal, unseeded",
              off.Verdict == HealthVerdict.Nominal && !off.Seeded, HealthMonitor.Name(off.Verdict));

        // ---- FROZEN: engaged, no warp, no node, static range/leg -> Degraded then Failed on the clock ----
        RvProgressSample frozen = S(true, false, false, 0, 0, 4200000, 3);
        RvProgressState st = RendezvousProgress.Step(RendezvousProgress.Fresh(), frozen, cfg, 1.0);
        Check("first frozen tick is still Nominal (not yet confirmed)",
              st.Verdict == HealthVerdict.Nominal, "");
        st = StepN(st, frozen, cfg, 1.0, 95);   // ~96 s frozen > 90 s
        Check("frozen past RetryStallS latches Degraded",
              st.Verdict == HealthVerdict.Degraded, "stall=" + st.StallS.ToString("F0"));
        Check("Kind is ConvergenceStalled when degraded",
              RendezvousProgress.Kind(st) == FaultKind.ConvergenceStalled, "");
        st = StepN(st, frozen, cfg, 1.0, 220);  // ~316 s total > 300 s
        Check("frozen past AbortStallS latches Failed",
              st.Verdict == HealthVerdict.Failed, "stall=" + st.StallS.ToString("F0"));

        // ---- FaultResponse drives the ladder: rendezvous stall Degraded->Replan, Failed->Downmode ----
        Check("degraded rendezvous stall -> REPLAN",
              FaultResponse.Decide(FaultKind.ConvergenceStalled, HealthVerdict.Degraded,
                                   FaultDomain.Rendezvous) == Recovery.Replan, "");
        Check("failed rendezvous stall -> DOWNMODE (abort-to-home)",
              FaultResponse.Decide(FaultKind.ConvergenceStalled, HealthVerdict.Failed,
                                   FaultDomain.Rendezvous) == Recovery.Downmode, "");

        // ---- WARP never stalls (the phasing wait / drift is warped) ----
        RvProgressState w = StepN(RendezvousProgress.Fresh(),
                                  S(true, true, false, 0, 0, 4200000, 3), cfg, 1.0, 400);
        Check("a warping rendezvous never stalls",
              w.Verdict == HealthVerdict.Nominal && w.StallS == 0.0, "stall=" + w.StallS.ToString("F0"));

        // ---- DELIVERING never stalls: a node whose remaining dv keeps falling ----
        RvProgressState d = RendezvousProgress.Fresh();
        double rem = 60.0;
        for (int i = 0; i < 400; i++)
        {
            rem -= 0.05;   // slow but real delivery
            d = RendezvousProgress.Step(d, S(true, false, true, rem, 5.0, 4200000, 3), cfg, 1.0);
        }
        Check("a slowly-delivering burn never stalls",
              d.Verdict == HealthVerdict.Nominal, "stall=" + d.StallS.ToString("F0"));

        // ---- SLEWING never stalls: a node aligning, pErr falling ----
        RvProgressState sl = RendezvousProgress.Fresh();
        double p = 176.0;
        for (int i = 0; i < 200; i++)
        {
            p -= 0.7;   // reorienting
            if (p < 1.0) p = 1.0;
            sl = RendezvousProgress.Step(sl, S(true, false, true, 60.0, p, 4200000, 3), cfg, 1.0);
        }
        Check("a slewing (aligning) node never stalls",
              sl.Verdict == HealthVerdict.Nominal, "stall=" + sl.StallS.ToString("F0"));

        // ---- CLOSING never stalls: new closest range each step ----
        RvProgressState c = RendezvousProgress.Fresh();
        double rng = 4200000.0;
        for (int i = 0; i < 400; i++)
        {
            rng -= 1000.0;   // 1 km closer each tick
            c = RendezvousProgress.Step(c, S(true, false, false, 0, 0, rng, 3), cfg, 1.0);
        }
        Check("a steadily-closing rendezvous never stalls",
              c.Verdict == HealthVerdict.Nominal, "stall=" + c.StallS.ToString("F0"));

        // ---- RECOVERY: a frozen->failed monitor clears to Nominal the instant progress resumes ----
        RvProgressState rec = StepN(RendezvousProgress.Fresh(), frozen, cfg, 1.0, 320);
        Check("frozen is Failed before recovery", rec.Verdict == HealthVerdict.Failed, "");
        // one warping tick = progress -> stall resets -> verdict clears (ClearS 0)
        rec = RendezvousProgress.Step(rec, S(true, true, false, 0, 0, 4200000, 3), cfg, 1.0);
        Check("progress resuming clears the stall verdict to Nominal",
              rec.Verdict == HealthVerdict.Nominal && rec.StallS == 0.0, HealthMonitor.Name(rec.Verdict));

        // ---- LEG ADVANCE is progress even with nothing else moving ----
        RvProgressState lg = RendezvousProgress.Fresh();
        lg = RendezvousProgress.Step(lg, S(true, false, false, 0, 0, 4200000, 3), cfg, 1.0);
        lg = StepN(lg, S(true, false, false, 0, 0, 4200000, 3), cfg, 1.0, 80);   // ~80 s frozen at leg 3
        lg = RendezvousProgress.Step(lg, S(true, false, false, 0, 0, 4200000, 4), cfg, 1.0); // leg steps
        Check("a leg advance resets the stall clock",
              lg.StallS == 0.0 && lg.Verdict == HealthVerdict.Nominal, "stall=" + lg.StallS.ToString("F0"));

        // ---- disengaging resets the monitor completely ----
        RvProgressState dis = StepN(RendezvousProgress.Fresh(), frozen, cfg, 1.0, 320);
        dis = RendezvousProgress.Step(dis, S(false, false, false, 0, 0, 4200000, 3), cfg, 1.0);
        Check("disengaging resets to a fresh monitor",
              !dis.Seeded && dis.Verdict == HealthVerdict.Nominal && dis.StallS == 0.0, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }
}
