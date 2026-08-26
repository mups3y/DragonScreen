// DragonScreen - Deorbit
// ---- ⛔ EVERY CONSTANT HERE IS FITTED FLIGHT DATA FROM `F9I/dragon_deorbit.ks`. ----
// ---- VARIABLE THRUST, WHICH IS THE WHOLE POINT ----
namespace DragonScreen
{
    public enum DeorbitPhase : byte
    {
        Idle = 0,
        Burn,
        Trim,
        Entry,
        Done
    }

    public struct DeorbitInputs
    {
        public bool Valid;
        public double PeriapsisM;
        public double PredictedRangeM;
        public double CrossTrackM;
        public double MonoUnits;
        public bool Crewed;
        public bool OnDraco;

        public double OrbitAltM;
        public bool ChuteLanding;
    }

    public struct DeorbitCommand
    {
        public DeorbitPhase Phase;
        public double Throttle;
        public double Fore;
        public string Note;
        public double PeriapsisTargetM, AimRangeM;
    }

    public static class Deorbit
    {
        // ---- TARGETS ----
        public const double PeriapsisTargetS2 = -40800.0;
        public const double PeriapsisTargetDraco = -31800.0;

        public const double AimS2Crew = 286000.0;
        public const double AimS2Cargo = 315450.0;
        [Tunable] public static double AimDracoCrew = 221500.0;

        public const double OvershootM = 35000.0;

        public const double LzToleranceM = 50.0;

        // ---- THE VARIABLE-THROTTLE SPANS ----
        public const double PeSpanM = 30000.0;
        public const double RangeSpanM = 150000.0;
        public const double ThrottleMin = 0.02;
        public const double ThrottleMax = 0.70;

        public const double CutLeadS = 0.35;

        // ---- TRIM ----
        public const double CrossToleranceM = 300.0;
        public const double TrimToleranceM = 2000.0;
        [Tunable] public static double AimGain = 0.67;

        // ---- RESERVES ----
        public const double MonoReservePropulsive = 50.0;
        public const double MonoReserveChute = 12.0;

        public static double PeriapsisTarget(DeorbitInputs s)
        {
            return s.OnDraco ? PeriapsisTargetDraco : PeriapsisTargetS2;
        }

        public const double AimFitAltM = 86000.0;

        /// ---- ⛔ TUNED 2026-08-13 FROM THE ONE RETURN THAT EVER COMPLETED, AND MADE TO SCALE ----
        /// ---- THE ALTITUDE SCALING, AND WHAT IT IS NOT ----
        public static double AimRange(DeorbitInputs s)
        {
            if (!s.OnDraco) return s.Crewed ? AimS2Crew : AimS2Cargo;

            double aim = AimDracoCrew;
            if (s.OrbitAltM > 0.0 && s.OrbitAltM != AimFitAltM)
                aim *= InterfaceEnergyRatio(s.OrbitAltM);
            return aim;
        }

        public static double InterfaceEnergyRatio(double orbitAltM)
        {
            double vNow = InterfaceSpeed(orbitAltM);
            double vFit = InterfaceSpeed(AimFitAltM);
            if (vFit <= 0.0) return 1.0;
            double k = vNow / vFit;
            return k * k;
        }

        public static double InterfaceSpeed(double orbitAltM)
        {
            const double R = 600000.0, MU = 3.5316e12, INTERFACE = 70000.0;
            double ra = R + orbitAltM;
            double rp = R + PeriapsisTargetDraco;
            double ri = R + INTERFACE;
            double a = (ra + rp) / 2.0;
            double t = MU * (2.0 / ri - 1.0 / a);
            return (t > 0.0) ? System.Math.Sqrt(t) : 0.0;
        }

        public static double MonoReserve(DeorbitInputs s)
        {
            return s.ChuteLanding ? MonoReserveChute : MonoReservePropulsive;
        }

        public static double BurnThrottle(double periapsisErrorM, double rangeErrorM)
        {
            double tp = (periapsisErrorM > 0.0) ? periapsisErrorM / PeSpanM : 0.0;
            double tr = (rangeErrorM > 0.0) ? rangeErrorM / RangeSpanM : 0.0;
            if (tp > 1.0) tp = 1.0;
            if (tr > 1.0) tr = 1.0;
            double t = (tp > tr) ? tp : tr;
            if (t <= 0.0) return 0.0;
            if (t < ThrottleMin) t = ThrottleMin;
            if (t > ThrottleMax) t = ThrottleMax;
            return t;
        }

        public static double TrimThrottle(double missM, bool coarse)
        {
            double span = coarse ? 400000.0 : 100000.0;
            double cap = coarse ? 0.60 : 0.35;
            if (missM <= 0.0) return 0.0;
            double t = System.Math.Sqrt(missM / span);
            if (t < 0.01) t = 0.01;
            if (t > cap) t = cap;
            return t;
        }

        public static double LandingThrottle(double trueRadarM, double stopDistM)
        {
            double floor = trueRadarM / 40.0;
            if (floor > 0.05) floor = 0.05;
            double h = (trueRadarM > 1.0) ? trueRadarM : 1.0;
            double t = stopDistM / h;
            if (t < floor) t = floor;
            if (t > 1.0) t = 1.0;
            return t;
        }

        public static DeorbitCommand Guide(DeorbitInputs s, DeorbitPhase phase)
        {
            DeorbitCommand c = new DeorbitCommand();
            c.Phase = phase;
            if (!s.Valid) { c.Phase = DeorbitPhase.Idle; c.Note = "no vessel"; return c; }

            double peTgt = PeriapsisTarget(s);
            double aim = AimRange(s);
            c.PeriapsisTargetM = peTgt;
            c.AimRangeM = aim;

            double peErr = s.PeriapsisM - peTgt;
            double rgErr = aim - s.PredictedRangeM;

            if (phase == DeorbitPhase.Idle) phase = DeorbitPhase.Burn;

            if (phase == DeorbitPhase.Burn && peErr <= 0.0 && rgErr <= 0.0)
                phase = DeorbitPhase.Trim;

            double cross = (s.CrossTrackM < 0.0) ? -s.CrossTrackM : s.CrossTrackM;
            double miss = (rgErr < 0.0) ? -rgErr : rgErr;
            if (phase == DeorbitPhase.Trim && miss < TrimToleranceM && cross < CrossToleranceM)
                phase = DeorbitPhase.Entry;

            c.Phase = phase;

            switch (phase)
            {
                case DeorbitPhase.Burn:
                    c.Throttle = BurnThrottle(peErr, rgErr);
                    c.Note = "DEORBIT BURN";
                    break;

                case DeorbitPhase.Trim:
                    // ---- RCS TRIMS BOTH WAYS; THE ENGINES ONLY SHORTEN ----
                    c.Throttle = 0.0;
                    if (miss > TrimToleranceM) c.Fore = (rgErr < 0.0) ? 1.0 : -1.0;
                    c.Note = "VACUUM TRIM";
                    break;

                case DeorbitPhase.Entry:
                    c.Note = "HANDOVER TO ENTRY";
                    break;

                default:
                    c.Note = "STANDBY";
                    break;
            }
            return c;
        }

        public static string Name(DeorbitPhase p)
        {
            switch (p)
            {
                case DeorbitPhase.Burn:  return "DEORBIT BURN";
                case DeorbitPhase.Trim:  return "VACUUM TRIM";
                case DeorbitPhase.Entry: return "ENTRY";
                case DeorbitPhase.Done:  return "DONE";
                default:                 return "STANDBY";
            }
        }
    }

    public static class StationOps
    {
        public const double DeorbitApM = 85100.0, DeorbitPeM = 79200.0;

        public const double DockHandoverM = 300.0;

        public const double SafeDistanceM = 150.0;

        public static bool SafeToBurn(double rangeM) { return rangeM >= SafeDistanceM; }
    }
}
