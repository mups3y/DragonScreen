// DragonScreen - ReturnBudget
// ---- WHY THIS IS ANSWERED BEFORE ANYTHING IS COMMITTED ----
// ---- ⛔ IF THE S2 IS STILL ATTACHED, THE DE-ORBIT COSTS ZERO MONOPROPELLANT ----
// ---- THE ROCKET EQUATION IS A SERIES EXPANSION, DELIBERATELY ----
namespace DragonScreen
{
    public enum LandingMode : byte
    {
        Parachute = 0,
        Propulsive
    }

    public struct BudgetInputs
    {
        public double MonoUnits;
        public double MassT;
        public double ApoapsisM;
        public double SmaM;
        public double BodyRadiusM;
        public double Mu;
        public bool S2Attached;
        public LandingMode Mode;
    }

    public struct BudgetReport
    {
        public double HaveUnits, NeedUnits, MarginUnits;
        public double DeorbitUnits, EntryUnits, LandingUnits;
        public double EntryInterfaceUnits;
        public bool S2Deorbit;
        public bool Sufficient;
        public string Line;
    }

    public static class ReturnBudget
    {
        // ---- F9I's CONSTANTS. station_ops.ks:695-708, dragon_deorbit.ks:96-104. ----
        public const double MonoIsp = 184.0;
        public const double MonoKgPerUnit = 4.0;
        public const double EntryUnits = 60.0;
        public const double LandingUnits = 12.0;
        public const double PropulsiveReserve = 50.0;
        public const double ChuteReserve = 12.0;

        public const double DeorbitPeriapsisM = -31800.0;

        public const double G0 = 9.80665;

        public static double ReserveFor(LandingMode mode)
        {
            return (mode == LandingMode.Propulsive) ? PropulsiveReserve : ChuteReserve;
        }

        public static double VisViva(double radiusM, double smaM, double mu)
        {
            if (radiusM <= 0.0 || smaM <= 0.0) return 0.0;
            double t = mu * ((2.0 / radiusM) - (1.0 / smaM));
            return (t > 0.0) ? System.Math.Sqrt(t) : 0.0;
        }

        public static double DeorbitMonoUnits(BudgetInputs b)
        {
            if (b.S2Attached) return 0.0;
            if (b.Mu <= 0.0 || b.MassT <= 0.0) return 0.0;

            double ra = b.BodyRadiusM + b.ApoapsisM;
            double rp = b.BodyRadiusM + DeorbitPeriapsisM;
            double vNow = VisViva(ra, b.SmaM, b.Mu);
            double vAfter = VisViva(ra, (ra + rp) / 2.0, b.Mu);
            double dv = vNow - vAfter;
            if (dv < 0.0) dv = -dv;

            double x = dv / (MonoIsp * G0);
            double kg = b.MassT * 1000.0 * (x - (x * x / 2.0));
            return kg / MonoKgPerUnit;
        }

        public const double EntryInterfacePeriapsisM = 40000.0;

        /// ---- ⛔ WHY THE TWO FIGURES MUST BE SEPARATE ----
        public static double EntryInterfaceMonoUnits(BudgetInputs b)
        {
            if (b.S2Attached) return 0.0;
            if (b.Mu <= 0.0 || b.MassT <= 0.0) return 0.0;

            double ra = b.BodyRadiusM + b.ApoapsisM;
            double rp = b.BodyRadiusM + EntryInterfacePeriapsisM;
            double peM = 2.0 * b.SmaM - ra - b.BodyRadiusM;
            if (peM <= EntryInterfacePeriapsisM) return 0.0;

            double vNow = VisViva(ra, b.SmaM, b.Mu);
            double vAfter = VisViva(ra, (ra + rp) / 2.0, b.Mu);
            double dv = vNow - vAfter;
            if (dv < 0.0) dv = -dv;

            double x = dv / (MonoIsp * G0);
            double kg = b.MassT * 1000.0 * (x - (x * x / 2.0));
            return kg / MonoKgPerUnit;
        }

        public static BudgetReport Report(BudgetInputs b)
        {
            BudgetReport r = new BudgetReport();
            r.HaveUnits = b.MonoUnits;
            r.DeorbitUnits = DeorbitMonoUnits(b);
            r.EntryInterfaceUnits = EntryInterfaceMonoUnits(b);
            r.EntryUnits = EntryUnits;
            r.LandingUnits = ReserveFor(b.Mode);
            r.NeedUnits = r.DeorbitUnits + r.EntryUnits + r.LandingUnits;
            r.MarginUnits = r.HaveUnits - r.NeedUnits;
            r.S2Deorbit = b.S2Attached;
            r.Sufficient = r.MarginUnits >= 0.0;

            string how = b.S2Attached ? "S2 (liquid fuel - no monoprop)" : "Draco";
            r.Line = "have " + F(r.HaveUnits) + ", need " + F(r.NeedUnits)
                   + " = de-orbit " + F(r.DeorbitUnits) + " on " + how
                   + " + entry " + F(r.EntryUnits) + " + landing " + F(r.LandingUnits)
                   + "  ->  margin " + F(r.MarginUnits) + " units";
            return r;
        }

        public static bool ReturnAllowed(bool landedOrSplashed, double altitudeM,
                                         double periapsisM, double atmosphereDepthM,
                                         out string why)
        {
            if (landedOrSplashed)
            {
                why = "already down - there is no orbit to leave";
                return false;
            }
            if (altitudeM < atmosphereDepthM || periapsisM < 0.0)
            {
                why = "not in orbit - periapsis " + F(periapsisM / 1000.0)
                    + " km; the return sequence needs a stable orbit";
                return false;
            }
            why = "";
            return true;
        }

        private static string F(double d)
        {
            return d.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
