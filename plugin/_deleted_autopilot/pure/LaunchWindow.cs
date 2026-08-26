// DragonScreen - LaunchWindow
// ---- ⛔ THIS IS NOT THE REAL CREW DRAGON PROCEDURE, AND THAT IS DELIBERATE ----
// ---- CO-ORBITAL PARK: THERE IS NO TRANSFER, SO THERE IS NO TRANSFER LEAD ----
// ---- AND THE TWO NUMBERS THAT MUST COME FROM THE LAST FLIGHT, NOT FROM A CONSTANT ----
namespace DragonScreen
{
    public struct WindowInputs
    {
        public double PadLonDeg;
        public double StationLonAtInsertionDeg;
        public double StationSmaM;
        public double StationPeriodS;
        public double ParkingPeriodS;

        // ---- MEASURED, NOT CHOSEN. Read back from the last flight. ----
        public double AscentTimeS;
        public double AscentLonDeg;
        public double PhaseBiasDeg;
        public double TrailDistM;
    }

    public static class LaunchWindow
    {
        public static double PhaseAtLaunch(WindowInputs w)
        {
            double vehLon = Wrap360(w.PadLonDeg + w.AscentLonDeg);
            double stnLon = Wrap360(w.StationLonAtInsertionDeg);
            return Wrap360(stnLon - vehLon);
        }

        public static double RequiredLead(WindowInputs w)
        {
            if (w.StationSmaM <= 0.0) return w.PhaseBiasDeg;
            return (w.TrailDistM / w.StationSmaM) * (180.0 / System.Math.PI) + w.PhaseBiasDeg;
        }

        public static double PhaseErrorDeg(WindowInputs w)
        {
            return Wrap180(PhaseAtLaunch(w) - RequiredLead(w));
        }

        public static double SecondsToWindow(WindowInputs w, double bodyRotationPeriodS)
        {
            double err = PhaseErrorDeg(w);
            if (System.Math.Abs(err) < WindowToleranceDeg) return 0.0;

            double stnRate = (w.StationPeriodS > 0.0) ? 360.0 / w.StationPeriodS : 0.0;
            double ourRate = (bodyRotationPeriodS > 0.0) ? 360.0 / bodyRotationPeriodS : 0.0;
            double closing = stnRate - ourRate;
            if (System.Math.Abs(closing) < 1e-9) return -1.0;

            double wait = -err / closing;
            double period = 360.0 / System.Math.Abs(closing);
            while (wait < 0.0) wait += period;
            return wait;
        }

        public const double WindowToleranceDeg = 0.25;

        private static double Wrap360(double d)
        {
            d = d % 360.0;
            return (d < 0.0) ? d + 360.0 : d;
        }

        private static double Wrap180(double d)
        {
            d = Wrap360(d);
            return (d > 180.0) ? d - 360.0 : d;
        }
    }
}
