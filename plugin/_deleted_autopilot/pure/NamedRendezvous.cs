// DragonScreen - NamedRendezvous (PURE)
// ---- WHY THIS IS A REBUILD, AND WHAT THE OLD ONE GOT WRONG ----
// ---- THE SPLIT: pure owns the CLIMB, glue owns the CW TERMINAL ----
namespace DragonScreen
{
    public enum RdvFire { None, Now, Apoapsis }

    public enum RdvLeg
    {
        Idle,
        Phase,
        Boost,
        Close,
        Drift,
        Transfer,
        Coelliptic,
        ApproachInit,
        Midcourse,
        Arrived
    }

    public struct RdvInputs
    {
        public double Mu;
        public double BodyRadiusM;
        public double ChaserRadiusM;
        public double ChaserSmaM;
        public double ChaserApoapsisM;
        public double ChaserPeriapsisM;
        public double TargetRadiusM;
        public double PhaseAngleDeg;
        public double RangeM;
        public double FloorM;
    }

    public struct RdvPlan
    {
        public RdvLeg Leg;
        public string Burn;
        public double DvMps;
        public RdvFire FireAt;
        public bool FireNow;
        public RdvLeg NextLeg;
        public bool FloorBlocked;
        public double WarpWaitS;
        // ---- instrumentation (rv_ columns) ----
        public double LeadDeg;
        public double GapDeg;
        public double CoAltKm;
        public string Note;
    }

    public static class NamedRendezvous
    {
        // ---- CLIMB profile constants ----

        public const double CoellipticDhM = 10000.0;

        public const double BoostArriveAheadDeg = 1.5;

        public const double PhaseGateTolDeg = 0.15;

        public const double CircularTolM = 1500.0;

        // ---- TERMINAL (CW) trigger constants, read by NamedRendezvousOps ----

        public const double TransferRangeM = 20000.0;

        public const double AiPointM = 7500.0;

        public const double AeEntryM = 2000.0;

        // ---- geometry helpers (all pure, all tested) ----

        public static double MeanMotion(double r, double mu)
        {
            if (r <= 0.0 || mu <= 0.0) return 0.0;
            return System.Math.Sqrt(mu / (r * r * r));
        }

        public static double TransferTimeS(double rFrom, double rTo, double mu)
        {
            double a = 0.5 * (rFrom + rTo);
            if (a <= 0.0 || mu <= 0.0) return 0.0;
            return System.Math.PI * System.Math.Sqrt(a * a * a / mu);
        }

        public static double LeadAngleDeg(double rFrom, double rTo, double rTarget, double mu,
                                          double arriveAheadDeg)
        {
            double tH = TransferTimeS(rFrom, rTo, mu);
            double targetSweepDeg = MeanMotion(rTarget, mu) * tH * 180.0 / System.Math.PI;
            double lead = 180.0 - targetSweepDeg + arriveAheadDeg;
            while (lead < 0.0) lead += 360.0;
            while (lead >= 360.0) lead -= 360.0;
            return lead;
        }

        public static double ClosingRateDegS(double rChaser, double rTarget, double mu)
        {
            double d = (MeanMotion(rChaser, mu) - MeanMotion(rTarget, mu)) * 180.0 / System.Math.PI;
            return d;
        }

        public static double AlongTrackForElevation(double dhM, double elevDeg)
        {
            double t = System.Math.Tan(elevDeg * System.Math.PI / 180.0);
            return (t > 1e-6) ? dhM / t : 0.0;
        }

        public static double ElevationDeg(double dhM, double alongTrackM)
        {
            return System.Math.Atan2(dhM, System.Math.Max(1.0, alongTrackM)) * 180.0 / System.Math.PI;
        }

        public static double CoellipticRadius(double rTarget) { return rTarget - CoellipticDhM; }

        public static bool BreachesFloor(RdvInputs s, double aNew, double rBurn)
        {
            double other = 2.0 * aNew - rBurn;
            double newPeri = System.Math.Min(rBurn, other);
            return newPeri < s.FloorM;
        }

        public static double GapToLeadDeg(double phaseDeg, double leadDeg)
        {
            double phase = phaseDeg; if (phase < 0.0) phase += 360.0;
            double gap = phase - leadDeg;
            while (gap <= -180.0) gap += 360.0;
            while (gap > 180.0) gap -= 360.0;
            return gap;
        }

        public static RdvPlan Plan(RdvInputs s, RdvLeg leg)
        {
            RdvPlan p = new RdvPlan();
            p.Leg = leg;
            p.NextLeg = leg;
            p.FireAt = RdvFire.None;

            double mu = s.Mu;
            double rTgt = s.TargetRadiusM;
            double rCo = CoellipticRadius(rTgt);
            p.CoAltKm = 0.0;

            switch (leg)
            {
                case RdvLeg.Idle:
                case RdvLeg.Phase:
                {
                    p.Leg = RdvLeg.Phase;
                    double spread = s.ChaserApoapsisM - s.ChaserPeriapsisM;
                    if (spread <= CircularTolM)
                    {
                        p.NextLeg = RdvLeg.Boost;
                        p.Note = "insertion already circular - to BOOST phasing";
                        return p;
                    }
                    double dv = Hohmann.CirculariseDv(s.ChaserApoapsisM, s.ChaserSmaM, mu);
                    p.Burn = "PHASE"; p.DvMps = dv; p.FireAt = RdvFire.Apoapsis; p.FireNow = true;
                    p.NextLeg = RdvLeg.Boost;
                    p.Note = "PHASE - circularise the insertion orbit";
                    return p;
                }

                case RdvLeg.Boost:
                {
                    p.Leg = RdvLeg.Boost;
                    double lead = LeadAngleDeg(s.ChaserRadiusM, rCo, rTgt, mu, BoostArriveAheadDeg);
                    double gap = GapToLeadDeg(s.PhaseAngleDeg, lead);
                    p.LeadDeg = lead;
                    p.GapDeg = gap;
                    p.CoAltKm = (rCo - s.BodyRadiusM) / 1000.0;

                    if (gap <= PhaseGateTolDeg)
                    {
                        double aNew = Hohmann.TransferSma(s.ChaserRadiusM, rCo);
                        if (BreachesFloor(s, aNew, s.ChaserRadiusM))
                        { p.FloorBlocked = true; p.Note = "BOOST blocked - periapsis floor"; return p; }
                        double dv = Hohmann.RaiseOppositeApsisDv(s.ChaserRadiusM, s.ChaserSmaM, rCo, mu);
                        p.Burn = "BOOST"; p.DvMps = dv; p.FireAt = RdvFire.Now; p.FireNow = true;
                        p.NextLeg = RdvLeg.Close;
                        p.Note = "BOOST - Hohmann raise to the co-elliptic";
                        return p;
                    }
                    double rateDegS = ClosingRateDegS(s.ChaserRadiusM, rTgt, mu);
                    p.WarpWaitS = (rateDegS > 1e-9) ? gap / rateDegS : 0.0;
                    p.Note = "phasing - " + gap.ToString("F2") + " deg to the BOOST lead angle";
                    return p;
                }

                case RdvLeg.Close:
                {
                    p.Leg = RdvLeg.Close;
                    p.CoAltKm = (rCo - s.BodyRadiusM) / 1000.0;
                    double dv = Hohmann.CirculariseDv(s.ChaserApoapsisM, s.ChaserSmaM, mu);
                    if (System.Math.Abs(dv) < 0.05)
                    {
                        p.NextLeg = RdvLeg.Drift;
                        p.Note = "already circular at the co-elliptic - to DRIFT";
                        return p;
                    }
                    p.Burn = "CLOSE"; p.DvMps = dv; p.FireAt = RdvFire.Apoapsis; p.FireNow = true;
                    p.NextLeg = RdvLeg.Drift;
                    p.Note = "CLOSE - circularise onto the co-elliptic";
                    return p;
                }

                default:
                    p.Note = "glue-driven leg";
                    return p;
            }
        }
    }
}
