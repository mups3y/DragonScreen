// DragonScreen - ThrustDeliveryMonitor
// ---- IT REPLACES THREE SCATTERED SEEDS WITH ONE RESIDUAL ----
// ---- WHY IT IS SAFE WHERE A NAIVE THRESHOLD IS NOT ----
namespace DragonScreen
{
    public struct ThrustSample
    {
        public double ExpectedAccel;
        public double DeliveredAccel;
        public bool Commanding;
    }

    public static class ThrustDeliveryMonitor
    {
        // ---- THE BANDS. Fractions of the expected delivery. ----
        public const double DegradeShortfall = 0.5;
        public const double FailShortfall = 0.9;
        public const double ClearShortfall = 0.3;

        public const double ConfirmS = 1.5;
        public const double ClearS = 0.5;

        public static MonitorConfig DefaultConfig()
        {
            return HealthMonitor.Config(DegradeShortfall, FailShortfall, ClearShortfall, ConfirmS, ClearS);
        }

        public static double Residual(ThrustSample x)
        {
            if (!x.Commanding || x.ExpectedAccel <= 0.0) return 0.0;
            double frac = x.DeliveredAccel / x.ExpectedAccel;
            double residual = 1.0 - frac;
            return residual < 0.0 ? 0.0 : residual;
        }

        public static FaultKind Kind(ThrustSample x, HealthVerdict verdict)
        {
            if (verdict == HealthVerdict.Nominal || !x.Commanding || x.ExpectedAccel <= 0.0)
                return FaultKind.None;
            return x.DeliveredAccel < 0.0 ? FaultKind.ThrustReversed : FaultKind.ThrustShortfall;
        }

        public static MonitorState Step(MonitorState prev, ThrustSample x, double dt)
        {
            return HealthMonitor.Step(prev, DefaultConfig(), Residual(x), dt);
        }
    }
}
