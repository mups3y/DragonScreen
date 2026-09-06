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

            // ---- ⚠ BIT 2 (NAV) IS NEVER SET, AND THAT IS DELIBERATE ([[S130]], 2026-09-06) --------
            // Bits 0 (FLIGHT), 1 (VEHICLE) and 3 (DOCKING) are set above. Bit 2 is the NAV page, and it
            // stays clear because **nothing this build models is a NAV alarm**. NAV draws the orbit, the
            // ground track and the map: a bad orbit is a FLIGHT condition and already lights bit 0, and
            // there is no navigation-system fault modelled that belongs to the page itself.
            // ⛔ Inventing one to fill the gap would be a FAKE ALARM — the worst possible member of the
            // class this file exists to keep honest, because an alarm channel is only worth anything if
            // every light in it means something. The audit (H7) flags the empty bit as "a silent gap the
            // moment the channel is reconnected"; the answer is that it is reconnected NOW, by
            // `BottomBar.StateInk`, and bit 2 is empty because the condition does not exist yet.
            // ⭐ When one does — a lost fix, a stale ephemeris, a map with no body — set it here and the
            // whole channel picks it up with no other change.

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

        // ==========================================================================================
        //  S137b — THE DISCRETE EMERGENCIES THE ALARM CHANNEL COULD NOT SEE
        // ==========================================================================================
        // ⛔ THE FINDING. `VehicleSystems` models three emergencies — `SystemsState.Fire`, `.Leaking`,
        // and six power `StringState`s that can read `Tripped` — and `SystemsPidPage` draws all three.
        // Nothing in this file read any of them. `VehicleSeverity` below was life support + thermal +
        // propellant + power, and `Mask` added only FDIR and a closing-rate term.
        //
        // So a CABIN FIRE raised no severity ANYWHERE: not the VehicleTabBar's red-nav, not the chrome
        // bar's STATE, not `SystemSeverity`, not the ALERTS list [[S137]] had just built. A crew on any
        // page but the P&ID would not have been told. Found by S137 while scoping that list, and NOT
        // patched there — a fire row under a green summary word is the "one panel, two answers" defect
        // S137 spent its own build removing. The fix belongs here, where one addition reaches every
        // surface that already reads a severity.
        //
        // ⚠ THE TWO BOOLEAN EVENTS ARE STATED AS ALARMS; THE POWER ONE IS DERIVED, NOT COUNTED.
        // A fire and a cabin leak are alarms by their own naming and need no threshold. For power the
        // obvious rule — "count the tripped strings" — would have been an invented threshold (is three
        // worse than two?), so the model answers it instead: a string tripping is a CAUTION because the
        // bus behind it is redundant, and a bus the crew has POWERED with ZERO online strings is an
        // ALARM because that redundancy is gone. Both facts come from `Systems.OnlineCount`; neither is
        // a number chosen here.

        /// <summary>Cabin emergencies: fire and a hull/cabin leak. Both ALARM — they are named as
        /// emergencies by the model that produces them and carry no band to argue about.</summary>
        public static Severity CabinEvents(SystemsState y)
        {
            return (y.Fire || y.Leaking) ? Severity.Alarm : Severity.Nominal;
        }

        /// <summary>Power-string events. CAUTION on any trip; ALARM on a bus the crew has switched ON
        /// that has no online string left, because that is the redundancy actually being gone rather
        /// than a count of how many trips feels bad.</summary>
        public static Severity PowerEvents(SystemsState y)
        {
            if ((y.Bus1On && Systems.OnlineCount(y, 1) == 0)
                || (y.Bus2On && Systems.OnlineCount(y, 2) == 0)) return Severity.Alarm;
            bool tripped = y.A1 == StringState.Tripped || y.B1 == StringState.Tripped
                        || y.C1 == StringState.Tripped || y.A2 == StringState.Tripped
                        || y.B2 == StringState.Tripped || y.C2 == StringState.Tripped;
            return tripped ? Severity.Caution : Severity.Nominal;
        }

        /// <summary>The CREW / life-support subsystem's severity, events included. ⛔ Deliberately a
        /// NEW function rather than a change to `LifeSupport(CabinReadout)`: the black box records
        /// `sev_ls` through that signature and `BlackBoxSchema` names it as the source, so widening it
        /// in place would silently change what a recorded column means. See REGISTER.md S137b.</summary>
        public static Severity CrewSeverity(PageState s)
        {
            if (!s.Valid) return Severity.Nominal;
            return Worst(LifeSupport(s.Cabin), CabinEvents(s.Systems));
        }

        /// <summary>The POWER subsystem's severity, events included. Same reasoning as CrewSeverity.</summary>
        public static Severity PowerSeverity(PageState s)
        {
            if (!s.Valid) return Severity.Nominal;
            return Worst(Low(s.Power01), PowerEvents(s.Systems));
        }

        public static Severity VehicleSeverity(PageState s)
        {
            Severity v = Worst(LifeSupport(s.Cabin), Thermal(s.Cabin));
            v = Worst(v, PropellantSeverity(s));
            v = Worst(v, Low(s.Power01));
            // ⭐ S137b: and the three discrete emergencies, which nothing here could see before. This
            // one line is what reaches the tab strip, the chrome bar and `Mask` - all of them already
            // read this function, so none of them needed changing.
            v = Worst(v, CabinEvents(s.Systems));
            v = Worst(v, PowerEvents(s.Systems));
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
