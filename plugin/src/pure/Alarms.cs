// DragonScreen - Alarms
// ---- WHY THIS EXISTS: THE GAUGES STOPPED CARRYING ALARM ----
// ---- ONE FUNCTION, BOTH CALLERS, AGAIN ----
namespace DragonScreen
{
    public enum Severity : byte
    {
        Nominal = 0,
        Caution = 1,
        Alarm = 2
    }

    public static class Alarms
    {
        public const double LowCaution = 0.25, LowAlarm = 0.10;
        public const double HighCaution = 0.75, HighAlarm = 0.90;

        public static Severity Low(double value01)
        {
            if (double.IsNaN(value01)) return Severity.Nominal;
            if (value01 <= LowAlarm) return Severity.Alarm;
            if (value01 <= LowCaution) return Severity.Caution;
            return Severity.Nominal;
        }

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

        // ---- PROPELLANT CAUTION: DRAGON'S OWN RETURN BUDGET, NOT WHICHEVER STAGE IS LIT (S5 fix) ----
        // s.Propellant01 correctly shows what the LIT engines are drinking (SCREEN_INVENTORY audit U2) -
        // that is right for the FLIGHT page's own dial, which is captioned with the stage it is reading.
        // But a near-spent second stage at SECO is NOMINAL, not a crew emergency: Dragon's own tanks are
        // what the crew's abort / RCS / deorbit budget actually depends on, and those are full at that
        // point. So every ALARM/severity read of "is propellant low" uses s.DragonProp01 (the Dragon-only
        // fraction VehicleSources already computes for the PROP/GNC pages) instead - one function, so the
        // status dot, the vehicle tab strip and the subsystem page banner can never disagree.
        public static Severity PropellantSeverity(PageState s)
        {
            return Low(s.DragonProp01);
        }

        public static Rgba Colour(Severity s)
        {
            if (s == Severity.Alarm) return DragonPalette.Alarm;
            if (s == Severity.Caution) return DragonPalette.Caution;
            return DragonPalette.Go;
        }

        /// <summary>
        /// A GAUGE RING's colour (S104 / QC V-01 + S-01).
        ///
        /// The Vehicle pages used to pass a CONSTANT as every gauge's colour — CABIN TEMP was `Red` at
        /// any temperature, so it read alarm-red at a nominal 21.8 °C while the Systems P&ID, computing
        /// the same value through <see cref="Band"/> in the same frame, drew it green. A ring colour is
        /// a safety verdict and S31/S32 say a verdict is computed or it is not shown.
        ///
        /// ⚠ An INVALID feed is not a nominal one — the same rule the FLIGHT page's status dots follow.
        /// Grey says "no reading"; green says "fine", and asserting green on no data is the confident
        /// zero this project refuses.
        ///
        /// ⛔ Use this ONLY where the model actually bands the quantity. A gauge whose quantity has no
        /// threshold (net power, body rates, array output, hull temperature) must be drawn in
        /// `DragonPalette.Accent` — the neutral "this is a reading, not a verdict" colour — NOT given an
        /// invented band to justify a colour.
        /// </summary>
        public static Rgba GaugeColour(Severity sev, bool valid)
        {
            return valid ? Colour(sev) : DragonPalette.Text6;
        }

        public static string Word(Severity s)
        {
            if (s == Severity.Alarm) return "ALARM";
            if (s == Severity.Caution) return "CAUTION";
            return "NOMINAL";
        }

        // ---- FDIR → the crew alert channel (§4.2 fix) ----
        // The REAL fault spine (pure/Fdir.cs) reaching the screen, instead of the display inventing alerts.
        // A tripped fault that has DEGRADED (downmode) or gone to abort/safe-mode is an ALARM; one still being
        // handled locally (retry/reconfigure/replan) is a CAUTION; no fault is nominal.
        public static Severity FdirSeverity(FaultKind fault, Recovery response)
        {
            if (fault == FaultKind.None) return Severity.Nominal;
            if (response == Recovery.Downmode || response == Recovery.Abort || response == Recovery.SafeMode)
                return Severity.Alarm;
            return Severity.Caution;
        }
        public static Severity FdirSeverity(PageState s)
        {
            return s.Valid ? FdirSeverity(s.Fault, s.FaultResponse) : Severity.Nominal;
        }
        // The overall crew-facing severity: the worse of the vehicle/crew-environment alarms and the FDIR spine.
        public static Severity SystemSeverity(PageState s)
        {
            return Worst(VehicleSeverity(s), FdirSeverity(s));
        }

        /// ---- THIS IS THE ALARM CHANNEL ----
        public static int Mask(PageState s)
        {
            if (!s.Valid) return 0;

            int mask = 0;

            Severity flight = Worst(High(s.GForce01), PropellantSeverity(s));
            flight = Worst(flight, Low(s.Power01));
            flight = Worst(flight, FdirSeverity(s));   // FDIR faults escalate the FLIGHT tab (real spine, not invented)
            if (flight >= Severity.Caution) mask |= 1 << 0;

            if (VehicleSeverity(s) >= Severity.Caution) mask |= 1 << 1;

            if (s.HasTarget && s.ClosingFast) mask |= 1 << 3;

            return mask;
        }

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

        public static Severity VehicleSeverity(PageState s)
        {
            Severity v = Worst(LifeSupport(s.Cabin), Thermal(s.Cabin));
            v = Worst(v, PropellantSeverity(s));
            v = Worst(v, Low(s.Power01));
            return v;
        }

        public static Severity LifeSupport(CabinReadout c)
        {
            Severity v = Band(c.Ppo2Psia, CabinLimits.Ppo2Caution, CabinLimits.Ppo2Alarm);
            v = Worst(v, Band(c.Co2MmHg, CabinLimits.Co2Caution, CabinLimits.Co2Alarm));
            v = Worst(v, Band(c.PressPsia, CabinLimits.PressCaution, CabinLimits.PressAlarm));
            return v;
        }

        public static Severity Thermal(CabinReadout c)
        {
            Severity v = Band(c.CabinTempC, CabinLimits.CabinTempCaution, CabinLimits.CabinTempAlarm);
            v = Worst(v, Band(c.LoopAC, CabinLimits.LoopCaution, CabinLimits.LoopAlarm));
            v = Worst(v, Band(c.LoopBC, CabinLimits.LoopCaution, CabinLimits.LoopAlarm));
            return v;
        }
    }

    /// ---- WHY THIS REPLACED A SET OF DIAL FRACTIONS ----
    public static class CabinLimits
    {
        public const double Ppo2Caution = 2.5, Ppo2Alarm = 2.0;
        public const double Co2Caution = 4.0, Co2Alarm = 6.0;
        public const double PressCaution = 13.0, PressAlarm = 11.0;
        public const double CabinTempCaution = 30.0, CabinTempAlarm = 35.0;
        public const double LoopCaution = 45.0, LoopAlarm = 55.0;
    }
}
