// DragonScreen - RendezvousProgress (PURE)
// ---- WHAT COUNTS AS PROGRESS (and why the definition is what it is) ----
// ---- SCOPE: FROZEN, NOT MERELY SLOW ----
// ---- THE VERDICT IS HealthMonitor's, FED A STALL-SECONDS RESIDUAL ----
namespace DragonScreen
{
    public struct RvProgressCfg
    {
        public double RangeProgressM;
        public double PErrProgressDeg;
        public double RetryStallS;
        public double AbortStallS;
    }

    public struct RvProgressSample
    {
        public bool Engaged;
        public bool WarpActive;
        public bool NodeActive;
        public double RemainingDvMps;
        public double PointingErrorDeg;
        public double RangeM;
        public int LegIndex;
    }

    public struct RvProgressState
    {
        public bool Seeded;
        public double BestRangeM;
        public int BestLeg;
        public double DeliverBaselineDv;
        public double SlewBaselineDeg;
        public double StallS;
        public MonitorState Health;

        public HealthVerdict Verdict { get { return Health.Verdict; } }
    }

    public static class RendezvousProgress
    {
        public static RvProgressCfg Default()
        {
            RvProgressCfg c;
            c.RangeProgressM = 500.0;
            c.PErrProgressDeg = 1.0;
            c.RetryStallS = 90.0;
            c.AbortStallS = 300.0;
            return c;
        }

        public static RvProgressState Fresh() { return new RvProgressState(); }

        public static MonitorConfig HealthCfg(RvProgressCfg cfg)
        {
            return HealthMonitor.Config(cfg.RetryStallS, cfg.AbortStallS, 1.0, 0.0, 0.0);
        }

        public static FaultKind Kind(RvProgressState st)
        {
            return st.Verdict == HealthVerdict.Nominal ? FaultKind.None : FaultKind.ConvergenceStalled;
        }

        public static RvProgressState Step(RvProgressState prev, RvProgressSample s, RvProgressCfg cfg,
                                           double dt)
        {
            if (!s.Engaged) return Fresh();
            if (dt < 0.0) dt = 0.0;

            RvProgressState st = prev;
            if (!st.Seeded)
            {
                st.Seeded = true;
                st.BestRangeM = s.RangeM;
                st.BestLeg = s.LegIndex;
                st.DeliverBaselineDv = s.RemainingDvMps;
                st.SlewBaselineDeg = s.PointingErrorDeg;
                st.StallS = 0.0;
                st.Health = HealthMonitor.Fresh();
            }

            // ---- re-baseline the delta signals when they WORSEN (a new burn re-arms remaining dv, a slew
            if (s.NodeActive && s.RemainingDvMps > st.DeliverBaselineDv) st.DeliverBaselineDv = s.RemainingDvMps;
            if (s.NodeActive && s.PointingErrorDeg > st.SlewBaselineDeg) st.SlewBaselineDeg = s.PointingErrorDeg;
            if (!s.NodeActive) { st.DeliverBaselineDv = 0.0; st.SlewBaselineDeg = 0.0; }

            bool newMin = s.RangeM < st.BestRangeM - cfg.RangeProgressM;
            bool legAdv = s.LegIndex > st.BestLeg;
            bool delivering = s.NodeActive && s.RemainingDvMps < st.DeliverBaselineDv - 0.01;
            bool slewing = s.NodeActive && s.PointingErrorDeg < st.SlewBaselineDeg - cfg.PErrProgressDeg;
            bool progress = s.WarpActive || newMin || legAdv || delivering || slewing;

            if (progress)
            {
                st.StallS = 0.0;
                if (delivering) st.DeliverBaselineDv = s.RemainingDvMps;
                if (slewing) st.SlewBaselineDeg = s.PointingErrorDeg;
            }
            else st.StallS += dt;

            if (s.RangeM < st.BestRangeM) st.BestRangeM = s.RangeM;
            if (s.LegIndex > st.BestLeg) st.BestLeg = s.LegIndex;

            st.Health = HealthMonitor.Step(st.Health, HealthCfg(cfg), st.StallS, dt);
            return st;
        }
    }
}
