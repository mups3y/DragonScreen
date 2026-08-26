// DragonScreen - HealthMonitor
// ---- THIS IS TEXTBOOK FDIR, AND THE SHAPE IS DELIBERATE ----
// ---- WHY TIME, NOT A TICK COUNT ----
// ---- STATELESS LAW, CALLER-OWNED STATE (the Attitude.cs discipline) ----
namespace DragonScreen
{
    public enum HealthVerdict : byte { Nominal = 0, Degraded = 1, Failed = 2 }

    public struct MonitorConfig
    {
        public double DegradeThreshold;
        public double FailThreshold;
        public double ClearThreshold;
        public double ConfirmS;
        public double ClearS;
    }

    public struct MonitorState
    {
        public HealthVerdict Verdict;
        public HealthVerdict Raw;
        public double Residual;
        public double OverS;
        public double UnderS;
    }

    public static class HealthMonitor
    {
        public static MonitorState Fresh() { return new MonitorState(); }

        public static MonitorConfig Config(double degrade, double fail, double clear,
                                           double confirmS, double clearS)
        {
            MonitorConfig c = new MonitorConfig();
            c.DegradeThreshold = degrade;
            c.FailThreshold = fail;
            c.ClearThreshold = clear;
            c.ConfirmS = confirmS;
            c.ClearS = clearS;
            return c;
        }

        public static HealthVerdict Classify(double absResidual, MonitorConfig cfg)
        {
            if (cfg.FailThreshold > 0.0 && absResidual >= cfg.FailThreshold) return HealthVerdict.Failed;
            if (cfg.DegradeThreshold > 0.0 && absResidual >= cfg.DegradeThreshold) return HealthVerdict.Degraded;
            return HealthVerdict.Nominal;
        }

        public static MonitorState Step(MonitorState prev, MonitorConfig cfg, double residual, double dt)
        {
            if (dt < 0.0) dt = 0.0;

            MonitorState s = prev;
            s.Residual = residual;
            double mag = residual < 0.0 ? -residual : residual;
            HealthVerdict raw = Classify(mag, cfg);
            s.Raw = raw;

            bool clear = mag < cfg.ClearThreshold;

            if ((byte)raw > (byte)s.Verdict)
            {
                s.OverS += dt;
                s.UnderS = 0.0;
                if (s.OverS >= cfg.ConfirmS) { s.Verdict = raw; s.OverS = 0.0; }
            }
            else if (clear && s.Verdict != HealthVerdict.Nominal)
            {
                s.UnderS += dt;
                s.OverS = 0.0;
                if (s.UnderS >= cfg.ClearS) { s.Verdict = HealthVerdict.Nominal; s.UnderS = 0.0; }
            }
            else
            {
                s.OverS = 0.0;
                if (!clear) s.UnderS = 0.0;
            }
            return s;
        }

        public static HealthVerdict Worst(HealthVerdict a, HealthVerdict b)
        {
            return (byte)a >= (byte)b ? a : b;
        }

        public static string Name(HealthVerdict v)
        {
            switch (v)
            {
                case HealthVerdict.Degraded: return "DEGRADED";
                case HealthVerdict.Failed:   return "FAILED";
                default:                     return "NOMINAL";
            }
        }
    }
}
