// DragonScreen - LifeSupport (PURE)
// ---- THE NUMBERS ARE TAC'S OWN, NOT OURS ----
// ---- WHAT DRIVES THE GATES ----
using System;

namespace DragonScreen
{
    public struct LsMargins
    {
        public bool Present;
        public int Crew;
        public double FoodDays, WaterDays, OxygenDays;
        public double OxygenHoursToLoss;
        public double LimitingDays;
    }

    public static class LifeSupport
    {
        // ---- TAC-LS v0.18, from LifeSupport.cfg (TACLSGlobalSettings). Per kerbal, per second. ----
        public const double FoodPerKerbalSec   = 1.6927083333e-05;
        public const double WaterPerKerbalSec  = 1.1188078704e-05;
        public const double OxygenPerKerbalSec = 0.001713537562385;
        public const double MaxSecWithoutOxygen = 7200.0;
        public const double MaxSecWithoutWater  = 129600.0;
        public const double MaxSecWithoutFood   = 1296000.0;
        public const double MaxSecWithoutPower  = 7200.0;
        public const double SecPerDay = 86400.0;

        public static double Days(double amount, double perKerbalSec, int crew)
        {
            if (crew <= 0 || perKerbalSec <= 0.0) return double.PositiveInfinity;
            if (amount <= 0.0) return 0.0;
            return (amount / (perKerbalSec * crew)) / SecPerDay;
        }

        public static LsMargins Margins(bool present, int crew, double food, double water, double oxygen)
        {
            LsMargins m = new LsMargins();
            m.Present = present;
            m.Crew = crew;
            m.FoodDays   = Days(food,   FoodPerKerbalSec,   crew);
            m.WaterDays  = Days(water,  WaterPerKerbalSec,  crew);
            m.OxygenDays = Days(oxygen, OxygenPerKerbalSec, crew);
            m.OxygenHoursToLoss = m.OxygenDays * 24.0 + MaxSecWithoutOxygen / 3600.0;
            m.LimitingDays = Math.Min(m.FoodDays, Math.Min(m.WaterDays, m.OxygenDays));
            return m;
        }

        public static bool SufficientFor(LsMargins m, double missionDays, double reserveDays)
        {
            if (!m.Present) return true;
            return m.LimitingDays >= missionDays + reserveDays;
        }
    }
}
