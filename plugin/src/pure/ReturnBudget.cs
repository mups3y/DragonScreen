/*
 * DragonScreen - ReturnBudget
 *
 * PURE. Can this capsule still get home? Ported from `F9I/station_ops.ks:839 StMonoForDeorbit`,
 * `:876 StMonoReport`, `:2645 StReturnAllowed` and `dragon_deorbit.ks:1895 DgLandingReserve`.
 *
 * ---- WHY THIS IS ANSWERED BEFORE ANYTHING IS COMMITTED ----
 * F9I's own reason, and it is a flight: "Flight 026 undocked 53 units short and nothing noticed until
 * the de-orbit burn died on the reserve floor." The whole return budget is one line, computed while
 * the capsule is still attached to a station that can refuel it - which is the only moment the answer
 * is actionable.
 *
 * ---- ⛔ IF THE S2 IS STILL ATTACHED, THE DE-ORBIT COSTS ZERO MONOPROPELLANT ----
 * The S2 de-orbits on LIQUID FUEL. It does not touch the mono. F9I's version computed the Draco cost
 * unconditionally and therefore announced "MONOPROP SHORT by 125 units - the de-orbit burn will not
 * finish and the landing will miss" on a vehicle that had 175 units AND a full second stage to
 * de-orbit with. Both statements were false, and acting on the second one skipped a phase-down that
 * costs real landing accuracy. Flight 009 spent 1.19 units on its entire de-orbit burn.
 *
 * ---- THE ROCKET EQUATION IS A SERIES EXPANSION, DELIBERATELY ----
 * `1 - exp(-x)` expanded to second order, `x - x^2/2`. F9I's reasoning, kept because it is the kind
 * of thing that gets "corrected" back to an exact exp() by someone who does not know why: our x is
 * about 113/1805 = 0.063, where the expansion is accurate to 0.07% - far inside the error on Isp
 * itself - and stays under 1% out to dv = 450 m/s, well past anything a Draco de-orbit asks for.
 */
namespace DragonScreen
{
    /// <summary>How the capsule intends to arrive. It changes the reserve, not the de-orbit.</summary>
    public enum LandingMode : byte
    {
        Parachute = 0,
        /// <summary>SuperDraco touchdown. Needs four times the reserve a chute landing does.</summary>
        Propulsive
    }

    public struct BudgetInputs
    {
        /// <summary>Monopropellant on board, units.</summary>
        public double MonoUnits;
        /// <summary>Vehicle mass, tonnes.</summary>
        public double MassT;
        /// <summary>Apoapsis the de-orbit burns from, metres ASL.</summary>
        public double ApoapsisM;
        /// <summary>Semi-major axis of the orbit we are burning from, metres.</summary>
        public double SmaM;
        /// <summary>Body radius, metres.</summary>
        public double BodyRadiusM;
        /// <summary>Body gravitational parameter.</summary>
        public double Mu;
        /// <summary>True while the second stage is still attached - it de-orbits on liquid fuel.</summary>
        public bool S2Attached;
        public LandingMode Mode;
    }

    public struct BudgetReport
    {
        public double HaveUnits, NeedUnits, MarginUnits;
        public double DeorbitUnits, EntryUnits, LandingUnits;
        public bool S2Deorbit;
        public bool Sufficient;
        /// <summary>One line, in the shape F9I logs it. Says WHICH de-orbit is budgeted for.</summary>
        public string Line;
    }

    public static class ReturnBudget
    {
        // ---- F9I's CONSTANTS. station_ops.ks:695-708, dragon_deorbit.ks:96-104. ----
        /// <summary>Draco specific impulse, seconds. MEASURED, not assumed.</summary>
        public const double MonoIsp = 184.0;
        /// <summary>Kilograms per unit of MonoPropellant in KSP.</summary>
        public const double MonoKgPerUnit = 4.0;
        /// <summary>Units the entry steering needs. `stMonoEntry`.</summary>
        public const double EntryUnits = 60.0;
        /// <summary>Units held back for the parachute descent. `stMonoLand`. TUNABLE.</summary>
        public const double LandingUnits = 12.0;
        /// <summary>Reserve for a PROPULSIVE landing. `dgMonoReserve`.</summary>
        public const double PropulsiveReserve = 50.0;
        /// <summary>Reserve for a PARACHUTE landing, which only needs attitude. `dgMonoReserveChute`.</summary>
        public const double ChuteReserve = 12.0;

        /// <summary>
        /// Periapsis the de-orbit burn drives to, metres. NEGATIVE - it is a depth, and it is what
        /// sets the entry angle. `dgPeriTgtDraco`: no trim authority on Dracos, so aim the entry
        /// directly rather than at the shallower S2 figure the trim would lift out of.
        /// </summary>
        public const double DeorbitPeriapsisM = -31800.0;

        /// <summary>Standard gravity, for the rocket equation. `constant:g0`.</summary>
        public const double G0 = 9.80665;

        /// <summary>Reserve for the chosen landing mode. `DgLandingReserve`.</summary>
        public static double ReserveFor(LandingMode mode)
        {
            return (mode == LandingMode.Propulsive) ? PropulsiveReserve : ChuteReserve;
        }

        /// <summary>Vis-viva. `StVisViva` - and its parameters are named uniquely for a reason.</summary>
        public static double VisViva(double radiusM, double smaM, double mu)
        {
            if (radiusM <= 0.0 || smaM <= 0.0) return 0.0;
            double t = mu * ((2.0 / radiusM) - (1.0 / smaM));
            return (t > 0.0) ? System.Math.Sqrt(t) : 0.0;
        }

        /// <summary>
        /// Monopropellant the DE-ORBIT BURN costs, units. Vis-viva for the Δv, rocket equation for
        /// the mass, /4 for units. The burn is at apoapsis, which is where the de-orbit runs.
        ///
        /// Zero when the S2 is attached - see the header. That is a correct answer, not a failure.
        /// </summary>
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

            // 1 - exp(-x) to second order. See the header before "fixing" this.
            double x = dv / (MonoIsp * G0);
            double kg = b.MassT * 1000.0 * (x - (x * x / 2.0));
            return kg / MonoKgPerUnit;
        }

        /// <summary>
        /// The whole return budget, BEFORE anything is committed.
        ///
        /// The line says WHICH de-orbit is being budgeted for, because a zero de-orbit figure with the
        /// S2 attached is correct and should not read like a failed calculation.
        /// </summary>
        public static BudgetReport Report(BudgetInputs b)
        {
            BudgetReport r = new BudgetReport();
            r.HaveUnits = b.MonoUnits;
            r.DeorbitUnits = DeorbitMonoUnits(b);
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

        /// <summary>
        /// Is a return sequence even meaningful from here? `StReturnAllowed`.
        ///
        /// Above the ground but inside the air is not an orbit either - that is a vessel already on
        /// its way down, and the de-orbit guidance would be planning a burn for a trajectory it no
        /// longer controls.
        /// </summary>
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
