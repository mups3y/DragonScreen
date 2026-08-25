/*
 * DragonScreen - LifeSupport (PURE)
 *
 * The TAC Life Support consumables model, as maths: given how much Food/Water/Oxygen is aboard and how
 * many crew are breathing it, how many DAYS does each last, and how long until the crew is lost. Pure, so
 * the crew-ops gates (CrewGates) and the ECLSS readout can test the margins headless.
 *
 * ---- THE NUMBERS ARE TAC'S OWN, NOT OURS ----
 * Every rate and limit here is copied from the installed TAC Life Support v0.18 config,
 * GameData/ThunderAerospace/TacLifeSupport/LifeSupport.cfg (TACLSGlobalSettings). Consumption is per
 * kerbal per second; the "MaxTimeWithout" limits are how long after a resource hits zero before a kerbal
 * dies. Time-to-depletion = amount / (rate x crew); time-to-crew-loss = that + the survival limit - which
 * is exactly how TAC's own display computes it. See dragonscreen-tac-life-support.
 *
 * ---- WHAT DRIVES THE GATES ----
 * The launch and de-orbit commit checks ask "are there enough consumables for the mission plus a reserve".
 * That is LimitingDays (the shortest of the three) against the mission duration - a REAL out-of-limits, so
 * a NO-GO here means the vehicle genuinely cannot make it, not a script.
 */
using System;

namespace DragonScreen
{
    /// <summary>Consumable margins for one vessel's crew. Days are +inf when nobody is aboard.</summary>
    public struct LsMargins
    {
        /// <summary>TAC is modelling this vessel. False = the numbers are meaningless, show "-".</summary>
        public bool Present;
        public int Crew;
        public double FoodDays, WaterDays, OxygenDays;
        /// <summary>Hours from now until oxygen runs out AND the survival window closes.</summary>
        public double OxygenHoursToLoss;
        /// <summary>The shortest of Food/Water/Oxygen - the one that ends the mission first.</summary>
        public double LimitingDays;
    }

    public static class LifeSupport
    {
        // ---- TAC-LS v0.18, from LifeSupport.cfg (TACLSGlobalSettings). Per kerbal, per second. ----
        public const double FoodPerKerbalSec   = 1.6927083333e-05;
        public const double WaterPerKerbalSec  = 1.1188078704e-05;
        public const double OxygenPerKerbalSec = 0.001713537562385;
        // Seconds a kerbal survives after a resource reaches zero.
        public const double MaxSecWithoutOxygen = 7200.0;    // 2 h
        public const double MaxSecWithoutWater  = 129600.0;  // 1.5 d
        public const double MaxSecWithoutFood   = 1296000.0; // 15 d
        public const double MaxSecWithoutPower  = 7200.0;    // 2 h
        public const double SecPerDay = 86400.0;

        /// <summary>
        /// Days a consumable lasts at TAC's rate for this crew. +inf if nobody is aboard (nothing is being
        /// consumed) or the rate is non-positive - an empty capsule never runs a tank dry.
        /// </summary>
        public static double Days(double amount, double perKerbalSec, int crew)
        {
            if (crew <= 0 || perKerbalSec <= 0.0) return double.PositiveInfinity;
            if (amount <= 0.0) return 0.0;
            return (amount / (perKerbalSec * crew)) / SecPerDay;
        }

        /// <summary>Compute all margins from the amounts aboard. Amounts are TAC resource units.</summary>
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

        /// <summary>
        /// True when consumables cover the mission plus a reserve. The commit check behind the launch and
        /// de-orbit gates. Absent TAC -> we cannot prove it short, so do not block on it (return true).
        /// </summary>
        public static bool SufficientFor(LsMargins m, double missionDays, double reserveDays)
        {
            if (!m.Present) return true;
            return m.LimitingDays >= missionDays + reserveDays;
        }
    }
}
