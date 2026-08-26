/*
 * Tests for the rebuilt named-burn rendezvous (pure/NamedRendezvous.cs) - the CLIMB state machine and
 * geometry, plus a Clohessy-Wiltshire terminal-phase sanity check (the solver the glue flies past the
 * co-elliptic). NO simulation: closed-form vis-viva + the CW STM, checked against hand geometry.
 *
 * The regression these lock in is the 2026-08-25 zero-burn flight: a robust trigger FIRES at the lead
 * angle AND fires when a warp nudges slightly past it (gap gone negative), never waiting a synodic period.
 */
using System;
using DragonScreen;

public static class NamedRendezvousTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }
    static bool Near(double a, double b, double tol) { return Math.Abs(a - b) <= tol; }

    public static int Run()
    {
        Console.WriteLine("DragonScreen named-burn rendezvous tests");

        double mu = 3.9860044e14, Re = 6.371010e6;
        double rIns = Re + 208000.0;     // chaser insertion (Crew-2 low insertion)
        double rTgt = Re + 419000.0;     // ISS
        double rCo = NamedRendezvous.CoellipticRadius(rTgt);

        // ---- co-elliptic radius is 10 km below the target ----
        Check("co-elliptic radius is 10 km below the target",
              Near(rCo, rTgt - 10000.0, 1.0), (rCo - Re).ToString("F0"));

        // ---- transfer time insertion -> co-elliptic ~ 45 min ----
        double tH = NamedRendezvous.TransferTimeS(rIns, rCo, mu);
        Check("insertion->co-elliptic transfer ~45 min", Near(tH / 60.0, 45.3, 1.0), (tH / 60.0).ToString("F1"));

        // ---- BOOST lead angle: the transfer sweeps ~176 deg of the target orbit, so BOOST fires with the
        //      target only a few degrees ahead (plus the 1.5 deg arrive-behind margin) ----
        double lead = NamedRendezvous.LeadAngleDeg(rIns, rCo, rTgt, mu, NamedRendezvous.BoostArriveAheadDeg);
        Check("BOOST lead angle is small (a few degrees)", lead > 2.0 && lead < 12.0, lead.ToString("F2"));

        // ---- closing rate: the lower chaser gains ~11 deg/hr on the ISS ----
        double rateDegS = NamedRendezvous.ClosingRateDegS(rIns, rTgt, mu);
        Check("closing rate ~11 deg/hr, positive", rateDegS > 0.0 && Near(rateDegS * 3600.0, 11.3, 1.5),
              (rateDegS * 3600.0).ToString("F1"));

        // ---- gap: robust firing. Positive gap = still closing; zero or slightly-past = FIRE ----
        Check("gap far from lead is large positive",
              Near(NamedRendezvous.GapToLeadDeg(40.0, lead), 40.0 - lead, 0.01), "");
        Check("gap AT the lead is zero",
              Near(NamedRendezvous.GapToLeadDeg(lead, lead), 0.0, 1e-6), "");
        Check("gap SLIGHTLY PAST the lead is small negative (still fires)",
              NamedRendezvous.GapToLeadDeg(lead - 0.8, lead) < 0.0
              && NamedRendezvous.GapToLeadDeg(lead - 0.8, lead) > -1.0,
              NamedRendezvous.GapToLeadDeg(lead - 0.8, lead).ToString("F2"));

        // ---- elevation geometry round-trips ----
        double along = NamedRendezvous.AlongTrackForElevation(10000.0, 27.5);
        double elev = NamedRendezvous.ElevationDeg(10000.0, along);
        Check("elevation round-trips to 27.5 deg", Near(elev, 27.5, 0.2), elev.ToString("F2"));
        Check("closer target reads a higher elevation",
              NamedRendezvous.ElevationDeg(10000.0, along * 0.5) > elev, "");

        // ---- floor guard: a RAISE never breaches; a big lower does ----
        RdvInputs s = MakeInputs(mu, Re, rIns, rTgt);
        s.ChaserApoapsisM = rIns; s.ChaserPeriapsisM = rIns;    // circular phasing orbit
        double aRaise = Hohmann.TransferSma(rIns, rCo);
        Check("raise to co-elliptic does not breach the floor",
              !NamedRendezvous.BreachesFloor(s, aRaise, rIns), "");
        double aLower = Hohmann.TransferSma(rIns, Re + 100000.0);
        Check("a lower below the floor IS blocked",
              NamedRendezvous.BreachesFloor(s, aLower, rIns), "");

        // ================= THE CLIMB STATE MACHINE =================

        // ---- PHASE: a dispersed insertion circularises; an already-circular one skips to BOOST ----
        RdvInputs disp = MakeInputs(mu, Re, rIns, rTgt);
        disp.ChaserApoapsisM = rIns + 9000.0; disp.ChaserPeriapsisM = rIns - 9000.0;   // 18 km spread
        disp.ChaserSmaM = rIns;
        RdvPlan ph = NamedRendezvous.Plan(disp, RdvLeg.Idle);
        Check("dispersed insertion: PHASE circularises at apoapsis, then BOOST",
              ph.FireNow && ph.Burn == "PHASE" && ph.FireAt == RdvFire.Apoapsis
              && ph.NextLeg == RdvLeg.Boost && ph.DvMps > 0.0,
              ph.Burn + " dv=" + ph.DvMps.ToString("F2") + " next=" + ph.NextLeg);

        RdvInputs circ = MakeInputs(mu, Re, rIns, rTgt);
        circ.ChaserApoapsisM = rIns; circ.ChaserPeriapsisM = rIns; circ.ChaserSmaM = rIns;
        RdvPlan skip = NamedRendezvous.Plan(circ, RdvLeg.Idle);
        Check("circular insertion: no PHASE burn, advance straight to BOOST",
              !skip.FireNow && skip.NextLeg == RdvLeg.Boost, skip.Note);

        // ---- BOOST: waits far from the lead (with a warp hint), fires AT the lead, fires PAST the lead ----
        RdvInputs b = MakeInputs(mu, Re, rIns, rTgt);
        b.ChaserApoapsisM = rIns; b.ChaserPeriapsisM = rIns; b.ChaserSmaM = rIns;
        b.PhaseAngleDeg = 40.0;
        RdvPlan far = NamedRendezvous.Plan(b, RdvLeg.Boost);
        Check("BOOST far from lead: waits, no burn, offers a warp hint",
              !far.FireNow && far.WarpWaitS > 0.0 && far.GapDeg > 1.0, far.Note);

        b.PhaseAngleDeg = lead;
        RdvPlan at = NamedRendezvous.Plan(b, RdvLeg.Boost);
        Check("BOOST at the lead: fires the raise prograde, next leg CLOSE",
              at.FireNow && at.Burn == "BOOST" && at.FireAt == RdvFire.Now
              && at.DvMps > 0.0 && at.NextLeg == RdvLeg.Close,
              at.Burn + " dv=" + at.DvMps.ToString("F1"));

        // ⛔ THE REGRESSION: a warp overshoot leaves the phase just PAST the lead. The old 0.6 deg window
        // would then wait a full synodic period; the rebuilt trigger fires immediately.
        b.PhaseAngleDeg = lead - 0.5;
        RdvPlan past = NamedRendezvous.Plan(b, RdvLeg.Boost);
        Check("BOOST just PAST the lead: STILL fires (no synodic-period wait)",
              past.FireNow && past.Burn == "BOOST", past.Note);

        // ---- CLOSE: circularises at the boost apoapsis onto the co-elliptic, then DRIFT ----
        RdvInputs c = MakeInputs(mu, Re, rIns, rTgt);
        c.ChaserApoapsisM = rCo; c.ChaserPeriapsisM = rIns;      // the boost transfer ellipse
        c.ChaserSmaM = Hohmann.TransferSma(rIns, rCo);
        c.ChaserRadiusM = rCo;                                   // at apoapsis
        RdvPlan close = NamedRendezvous.Plan(c, RdvLeg.Close);
        Check("CLOSE: circularise at apoapsis, next leg DRIFT",
              close.FireNow && close.Burn == "CLOSE" && close.FireAt == RdvFire.Apoapsis
              && close.DvMps > 0.0 && close.NextLeg == RdvLeg.Drift,
              close.Burn + " dv=" + close.DvMps.ToString("F1"));

        // ================= THE CW TERMINAL SOLVER (what the glue flies past the co-elliptic) =================
        // A chaser on the co-elliptic 10 km below and 20 km behind the station, drifting forward. Ask the
        // solver for the TRANSFER intercept to the 7.5 km AI point - it must find a real, modest burn.
        double nStn = NamedRendezvous.MeanMotion(rTgt, mu);
        double period = 2.0 * Math.PI / nStn;
        CwState cw = new CwState();
        cw.Rx = -10000.0;                 // 10 km below (radial)
        cw.Ry = -20000.0;                 // 20 km behind (along-track)
        cw.Rz = 0.0;
        cw.Vx = 0.0;
        cw.Vy = 1.5 * nStn * 10000.0;     // the co-elliptic along-track drift (~1.5 n dH), closing
        cw.Vz = 0.0;
        cw.N = nStn;
        double bestTof;
        CwSolution sol = CwTargeting.Best(cw, 300.0, 0.9 * period, 60, NamedRendezvous.AiPointM, out bestTof);
        Check("CW TRANSFER solve succeeds from the co-elliptic", sol.Ok, sol.Note);
        Check("CW TRANSFER impulse is modest (< 50 m/s)",
              sol.Ok && CwTargeting.DvMagnitude(sol) < 50.0, CwTargeting.DvMagnitude(sol).ToString("F2"));
        Check("CW TRANSFER arrival speed is finite and modest (< 50 m/s)",
              sol.Ok && sol.ArrivalRelSpeed >= 0.0 && sol.ArrivalRelSpeed < 50.0,
              sol.ArrivalRelSpeed.ToString("F2"));
        // The cheapest transfer is the slowest one the sweep allows (a gentle, fuel-optimal intercept) -
        // realistic for terminal phase, which is ~90 min per leg on the real vehicle.
        Check("CW TRANSFER time-of-flight is a sane sub-orbit time",
              bestTof >= 300.0 && bestTof <= period, (bestTof / 60.0).ToString("F1") + " min");

        // ================= PASSIVE ABORT (free-drift after a missed arrival burn) =================
        // FreeDrift at t=0 is exactly r0.
        {
            double x0, y0, z0;
            CwTargeting.FreeDrift(cw.Rx, cw.Ry, cw.Rz, 1.0, 2.0, 3.0, nStn, 0.0, out x0, out y0, out z0);
            Check("free-drift at t=0 returns r0",
                  Near(x0, cw.Rx, 1e-6) && Near(y0, cw.Ry, 1e-6) && Near(z0, cw.Rz, 1e-6),
                  x0.ToString("F1") + "," + y0.ToString("F1"));
        }

        // A solved transfer's free-drift (on v0+ = Vx1..) reproduces the aim point (0, -aim, 0) at tof.
        {
            double x1, y1, z1;
            CwTargeting.FreeDrift(cw.Rx, cw.Ry, cw.Rz, sol.Vx1, sol.Vy1, sol.Vz1, nStn, bestTof,
                                  out x1, out y1, out z1);
            Check("solved transfer's free-drift reaches the aim point at tof",
                  Near(x1, 0.0, 5.0) && Near(y1, -NamedRendezvous.AiPointM, 5.0) && Near(z1, 0.0, 5.0),
                  x1.ToString("F1") + "," + y1.ToString("F1") + "," + z1.ToString("F1"));
        }

        // A trajectory heading straight through the origin has a small min range; one parked far does not.
        {
            // Start 5 km "below", drifting up toward the station at 5 m/s radial - it will pass close.
            double minToward = CwTargeting.FreeDriftMinRangeM(-5000.0, 0.0, 0.0, 5.0, 0.0, 0.0,
                                                              nStn, period, 240);
            Check("a free-drift aimed at the station reports a small min range",
                  minToward < 5000.0, minToward.ToString("F0"));
            // A stationary co-elliptic point 5 km below never approaches - min stays near 5 km.
            double minSafe = CwTargeting.FreeDriftMinRangeM(-5000.0, 0.0, 0.0, 0.0, 1.5 * nStn * 5000.0, 0.0,
                                                            nStn, period, 240);
            Check("a co-elliptic free-drift keeps its distance (min range large)",
                  minSafe > 1000.0, minSafe.ToString("F0"));
        }

        // The passive-abort Best sets the margin + flag, and a returned SAFE solution really clears it.
        {
            double safeM = WaypointApproach.KeepOutRadiusM + 50.0;
            double tof2;
            CwSolution safe = CwTargeting.Best(cw, 300.0, 0.9 * period, 60, NamedRendezvous.AiPointM,
                                               out tof2, safeM, period, CwTargeting.DefaultCoastSamples);
            Check("passive-abort Best returns a solution", safe.Ok, safe.Note);
            Check("passive-abort margin is populated", safe.MinFreeDriftRangeM > 0.0,
                  safe.MinFreeDriftRangeM.ToString("F0"));
            Check("a solution flagged passive-abort-SAFE really clears the margin",
                  !safe.PassiveAbortSafe || safe.MinFreeDriftRangeM >= safeM - 1e-6,
                  "safe=" + safe.PassiveAbortSafe + " margin=" + safe.MinFreeDriftRangeM.ToString("F0"));
            Check("passive-abort aim behind (7.5 km) is comfortably safe here",
                  safe.PassiveAbortSafe && safe.MinFreeDriftRangeM >= safeM,
                  safe.MinFreeDriftRangeM.ToString("F0"));
        }

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    static RdvInputs MakeInputs(double mu, double Re, double rChaser, double rTgt)
    {
        RdvInputs s = new RdvInputs();
        s.Mu = mu;
        s.BodyRadiusM = Re;
        s.ChaserRadiusM = rChaser;
        s.ChaserSmaM = rChaser;
        s.ChaserApoapsisM = rChaser;
        s.ChaserPeriapsisM = rChaser;
        s.TargetRadiusM = rTgt;
        s.FloorM = Re + 145000.0;
        s.RangeM = rTgt - rChaser;
        s.PhaseAngleDeg = 0.0;
        return s;
    }
}
