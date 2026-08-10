/*
 * DragonScreen - Alarms
 *
 * PURE. Where "is this reading in trouble?" is decided, and the ONE place alarm becomes a colour.
 *
 * ---- WHY THIS EXISTS: THE GAUGES STOPPED CARRYING ALARM ----
 * Until 2026-08-06 the dials were coloured by threshold - green, amber, red by value - and
 * Gauge.LowIsBad / Gauge.HighIsBad returned those colours directly. That was wrong about the real
 * vehicle, confirmed by three independent sources agreeing (the Figma mock, the user's photograph of
 * the real screen, and the live demo at neeldandiwala.com/SpaceX-Dragon2-UI): every dial has a FIXED
 * colour of its own - PPO2 mustard, CABIN TEMP red, CO2 blue - and it keeps that colour whatever it
 * reads. See DragonPalette's gauge block for the measured hexes.
 *
 * Alarm is not thrown away, it MOVES: it is routed through ONE channel - the page link in the chrome
 * bar, plus the status list - so "something is wrong" has a single place to look instead of being
 * smeared across eight dials that are all coloured anyway. User's call, 2026-08-06.
 *
 * So the threshold logic did not disappear; it was separated from the drawing. This file answers
 * "how bad is it", the page decides what to DO about that, and the dial just draws itself.
 *
 * ---- ONE FUNCTION, BOTH CALLERS, AGAIN ----
 * The bit that lights a page link and the condition a page might print must be the same test. Two
 * copies of a threshold drift exactly like the perigee guard did - one screen saying NOMINAL while
 * another shows the same value in red.
 */
namespace DragonScreen
{
    /// <summary>How bad a reading is. Ordered, so Worst() is a max.</summary>
    public enum Severity : byte
    {
        Nominal = 0,
        Caution = 1,
        Alarm = 2
    }

    public static class Alarms
    {
        // 25% / 10% are the conventional caution and warning breaks for a consumable, and 75% / 90%
        // their mirror for something that must stay low. Stated once here rather than repeated at
        // every call site, which is how they would drift apart.
        public const double LowCaution = 0.25, LowAlarm = 0.10;
        public const double HighCaution = 0.75, HighAlarm = 0.90;

        /// <summary>Severity for a value where LOW is bad - propellant, power, oxygen.</summary>
        public static Severity Low(double value01)
        {
            if (double.IsNaN(value01)) return Severity.Nominal;
            if (value01 <= LowAlarm) return Severity.Alarm;
            if (value01 <= LowCaution) return Severity.Caution;
            return Severity.Nominal;
        }

        /// <summary>Severity for a value where HIGH is bad - g-force, temperature, CO2.</summary>
        public static Severity High(double value01)
        {
            if (double.IsNaN(value01)) return Severity.Nominal;
            if (value01 >= HighAlarm) return Severity.Alarm;
            if (value01 >= HighCaution) return Severity.Caution;
            return Severity.Nominal;
        }

        public static Severity Worst(Severity a, Severity b)
        {
            return (a > b) ? a : b;
        }

        /// <summary>
        /// The colour alarm is allowed to use. Nominal returns the NOMINAL GREEN rather than a
        /// neutral, because this is used for status dots and state words, where "fine" is a reading
        /// in its own right and a grey dot is indistinguishable from a dead one.
        /// </summary>
        public static Rgba Colour(Severity s)
        {
            if (s == Severity.Alarm) return DragonPalette.Alarm;
            if (s == Severity.Caution) return DragonPalette.Caution;
            return DragonPalette.Go;
        }

        public static string Word(Severity s)
        {
            if (s == Severity.Alarm) return "ALARM";
            if (s == Severity.Caution) return "CAUTION";
            return "NOMINAL";
        }

        /// <summary>
        /// Worst condition held by each page, as the bitmask the chrome bar draws.
        ///
        /// ---- THIS IS THE ALARM CHANNEL ----
        /// Bit i set turns page link i alarm-coloured on EVERY screen, so a crew member looking at
        /// NAV still learns that VEHICLE has a problem and is one touch from it. That routing is the
        /// reason the bar exists, and now that the dials are identity-coloured it is the ONLY thing
        /// carrying alarm - so a condition missing from here is a condition nothing shows.
        ///
        /// Deliberately conservative: only conditions we can actually stand behind. A simulated cabin
        /// value going amber is a real signal about a real model (see CabinEnvironment) and belongs
        /// here; an invented one would not.
        /// </summary>
        public static int Mask(PageState s)
        {
            if (!s.Valid) return 0;

            int mask = 0;

            // FLIGHT (0) - the vehicle's own stress and consumables.
            Severity flight = Worst(High(s.GForce01), Low(s.Propellant01));
            flight = Worst(flight, Low(s.Power01));
            if (flight >= Severity.Caution) mask |= 1 << 0;

            // VEHICLE (1) - life support, plus the same consumables it displays.
            if (VehicleSeverity(s) >= Severity.Caution) mask |= 1 << 1;

            // NAV (2) - nothing yet. A trajectory alert needs the predictor, and a page link that
            // lights for a reason we cannot name would be worse than one that never lights.

            // DOCKING (3) - only meaningful with a target. Closing fast inside 100 m is the one
            // condition worth shouting about; alignment error alone is a flying quality, not an
            // alarm, and lighting the bar for it would cry wolf on every approach.
            if (s.HasTarget && s.ClosingFast) mask |= 1 << 3;

            return mask;
        }

        /// <summary>
        /// Severity from a reading IN ITS OWN UNITS against two stated limits.
        ///
        /// <paramref name="caution"/> and <paramref name="alarm"/> are real values - degrees, psia,
        /// mmHg - and alarm may be either above or below caution; the direction is inferred, so a
        /// falling limit like PPO2 and a rising one like CO2 use the same function without a flag
        /// anyone can pass the wrong way round.
        /// </summary>
        public static Severity Band(double value, double caution, double alarm)
        {
            if (double.IsNaN(value)) return Severity.Nominal;
            if (alarm > caution)
            {
                if (value >= alarm) return Severity.Alarm;
                if (value >= caution) return Severity.Caution;
            }
            else
            {
                if (value <= alarm) return Severity.Alarm;
                if (value <= caution) return Severity.Caution;
            }
            return Severity.Nominal;
        }

        /// <summary>Worst life-support or consumable condition. Also drives the VEHICLE status list.</summary>
        public static Severity VehicleSeverity(PageState s)
        {
            Severity v = Worst(LifeSupport(s.Cabin), Thermal(s.Cabin));
            v = Worst(v, Low(s.Propellant01));
            v = Worst(v, Low(s.Power01));
            return v;
        }

        /// <summary>Breathable-atmosphere conditions. One function, so the dot and the bar agree.</summary>
        public static Severity LifeSupport(CabinReadout c)
        {
            Severity v = Band(c.Ppo2Psia, CabinLimits.Ppo2Caution, CabinLimits.Ppo2Alarm);
            v = Worst(v, Band(c.Co2MmHg, CabinLimits.Co2Caution, CabinLimits.Co2Alarm));
            v = Worst(v, Band(c.PressPsia, CabinLimits.PressCaution, CabinLimits.PressAlarm));
            return v;
        }

        /// <summary>Cabin and coolant-loop temperatures.</summary>
        public static Severity Thermal(CabinReadout c)
        {
            Severity v = Band(c.CabinTempC, CabinLimits.CabinTempCaution, CabinLimits.CabinTempAlarm);
            v = Worst(v, Band(c.LoopAC, CabinLimits.LoopCaution, CabinLimits.LoopAlarm));
            v = Worst(v, Band(c.LoopBC, CabinLimits.LoopCaution, CabinLimits.LoopAlarm));
            return v;
        }
    }

    /// <summary>
    /// Cabin redlines, IN REAL UNITS.
    ///
    /// ---- WHY THIS REPLACED A SET OF DIAL FRACTIONS ----
    /// The first version (CabinScale) held 0..1 dial fractions that were divided into Alarms.High,
    /// whose own breaks are 0.75 and 0.90. Composing two layers of fractions put the real alarm
    /// somewhere nobody had chosen: for the coolant loops it worked out at 0.72 of a 50 degree dial,
    /// so THERMAL went to ALARM at 36 C.
    ///
    /// The cost showed up on a flown mission, 2026-08-06: the chrome bar read STATE ALARM for the
    /// whole ascent, every ascent, because aerodynamic heating puts the hull near 95 C and the loops
    /// follow it. An alarm that is always on is not an alarm - and this file's own Mask() comment
    /// says alignment error is kept out precisely so the bar does not cry wolf. It was crying wolf
    /// anyway, one layer down.
    ///
    /// So the limits are now the numbers themselves. 45 C on a coolant loop is warm and worth a
    /// glance; 55 C means the rejection is not keeping up. Those are reviewable claims. "0.8 of a
    /// dial fraction" was not, which is exactly how it stayed wrong.
    /// </summary>
    public static class CabinLimits
    {
        /// <summary>psia. Falling: nominal is 3.0, hypoxia risk below about 2.</summary>
        public const double Ppo2Caution = 2.5, Ppo2Alarm = 2.0;
        /// <summary>mmHg. Rising. Crewed spacecraft work to keep this under about 4.</summary>
        public const double Co2Caution = 4.0, Co2Alarm = 6.0;
        /// <summary>psia. Falling from a 14.7 sea-level cabin.</summary>
        public const double PressCaution = 13.0, PressAlarm = 11.0;
        /// <summary>deg C. Rising. A cabin above 30 is uncomfortable, above 35 a problem.</summary>
        public const double CabinTempCaution = 30.0, CabinTempAlarm = 35.0;
        /// <summary>deg C. Rising. Peak ascent heating reaches the caution band and should.</summary>
        public const double LoopCaution = 45.0, LoopAlarm = 55.0;
    }
}
