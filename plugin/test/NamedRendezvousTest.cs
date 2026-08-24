/*
 * Tests for the named-burn co-elliptic rendezvous (pure/NamedRendezvous.cs). Numbers checked against
 * the headless sim (scratchpad/rdv_sim.py) for the Crew-2 geometry: chaser 200 km, ISS 420 km.
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
        double rIns = Re + 200000.0;     // chaser insertion
        double rTgt = Re + 420000.0;     // ISS
        double rCo = NamedRendezvous.CoellipticRadius(rTgt);

        // ---- co-elliptic radius is 15 km below the target ----
        Check("co-elliptic radius is 15 km below the target",
              Near(rCo, rTgt - 15000.0, 1.0), (rCo - Re).ToString("F0"));

        // ---- transfer time insertion -> co-elliptic ~ 45 min (sim: 45.2) ----
        double tH = NamedRendezvous.TransferTimeS(rIns, rCo, mu);
        Check("insertion->co-elliptic transfer ~45 min", Near(tH / 60.0, 45.2, 1.0), (tH / 60.0).ToString("F1"));

        // ---- NC lead angle: the transfer sweeps almost a full target orbit (~175 deg), so NC fires with
        //      the target only a few degrees ahead (sim: ~4.6 deg + arrive-ahead) ----
        double lead = NamedRendezvous.NcLeadAngleDeg(rIns, rCo, rTgt, mu,
                                                     NamedRendezvous.CoellipticArriveAheadDeg);
        Check("NC lead angle is small (a few degrees)", lead > 2.0 && lead < 10.0, lead.ToString("F2"));

        // ---- Ti elevation geometry: at 27.5 deg with dH 15 km, the target leads ~28.8 km (sim: 28.81) ----
        double along = NamedRendezvous.AlongTrackForElevation(NamedRendezvous.CoellipticDhM,
                                                              NamedRendezvous.TiElevationDeg);
        Check("Ti along-track ~28.8 km at 27.5 deg", Near(along / 1000.0, 28.8, 0.3), (along / 1000.0).ToString("F2"));
        // inverse: elevation at that along-track is back to 27.5
        double elev = NamedRendezvous.ElevationDeg(NamedRendezvous.CoellipticDhM, along);
        Check("elevation round-trips to 27.5 deg", Near(elev, 27.5, 0.2), elev.ToString("F2"));
        // closer in (smaller along-track) => higher elevation
        Check("closer target reads a higher elevation",
              NamedRendezvous.ElevationDeg(NamedRendezvous.CoellipticDhM, along * 0.5) > elev, "");

        // ---- floor guard: a RAISE never breaches the floor; a big lower would ----
        RdvInputs s = new RdvInputs();
        s.Mu = mu; s.ChaserRadiusM = rIns; s.ChaserSmaM = rIns; s.TargetRadiusM = rTgt;
        s.FloorM = Re + 145000.0; s.PeriapsisM = rIns;
        double aRaise = Hohmann.TransferSma(rIns, rCo);
        Check("raise to co-elliptic does not breach the 145 km floor",
              !NamedRendezvous.BreachesFloor(s, aRaise, rIns), "");
        double aLower = Hohmann.TransferSma(rIns, Re + 100000.0);   // lower toward 100 km
        Check("a lower below the floor IS blocked",
              NamedRendezvous.BreachesFloor(s, aLower, rIns), "");

        // ---- state machine: PHASING waits until the lead angle, then fires NC prograde ----
        s.PhaseAngleDeg = 40.0;                       // target well ahead - still closing
        RdvPlan far = NamedRendezvous.Plan(s, RdvLeg.Phasing);
        Check("far from the lead angle: phasing, no burn", far.Leg == RdvLeg.Phasing && !far.FireNow, far.Note);
        s.PhaseAngleDeg = lead;                        // exactly at the lead angle
        RdvPlan at = NamedRendezvous.Plan(s, RdvLeg.Phasing);
        Check("at the lead angle: NC fires prograde", at.FireNow && at.Burn == "NC" && at.DvMps > 0.0,
              at.Burn + " " + at.DvMps.ToString("F1"));

        // ---- Coelliptic leg: drifts until the Ti along-track, then fires Ti ----
        RdvInputs c = s; c.ChaserRadiusM = rCo; c.ChaserSmaM = rCo;
        c.PhaseAngleDeg = (along + 20000.0) / rTgt * 180.0 / Math.PI;   // a bit beyond the Ti point
        RdvPlan drift = NamedRendezvous.Plan(c, RdvLeg.Coelliptic);
        Check("co-elliptic before the Ti point: drift, no burn", !drift.FireNow && drift.Leg == RdvLeg.Coelliptic, drift.Note);
        c.PhaseAngleDeg = along / rTgt * 180.0 / Math.PI;              // at the Ti elevation point
        RdvPlan ti = NamedRendezvous.Plan(c, RdvLeg.Coelliptic);
        Check("at the Ti point: Ti fires prograde", ti.FireNow && ti.Burn == "Ti" && ti.DvMps > 0.0,
              ti.Burn + " " + ti.DvMps.ToString("F1"));

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
