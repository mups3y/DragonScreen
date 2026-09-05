// DragonScreen — AlertList  (PURE: the ALERTS view's actual list of what is wrong)
// ============================================================================================
// The ALERTS half of every subsystem page showed ONE WORD — `Alarms.Word(sev)` at 110 design px —
// and nothing else. S49 H14 / QC: *"No enumerated list, no timestamps, no acknowledgement."* A crew
// reading `CAUTION` learned that something was wrong and not one thing about WHAT.
//
// ⭐ AND THE EVENTS WERE ALREADY THERE, WHICH IS WHY THIS IS A HARVEST AND NOT A MODEL. `Alarms`
// computes the summary by taking `Worst()` over a set of per-quantity bands — PPO2, CO2, cabin
// pressure, cabin temperature, both coolant loops, propellant, power, g-force, FDIR — and then throws
// the components away and keeps the maximum. This file keeps the components. Every row below is a
// band that `Alarms.VehicleSeverity` / `Alarms.Mask` already evaluate; not one threshold is new, and
// there is no threshold in this file at all.
//
// ---- THE (A)/(B) CEILING, WHICH IS THE REASON THIS IS BUILDABLE AT ALL ----
// ⛔ §1.2: the FDIR CHANNEL is Part B's. An alert list assembled from `Alarms` and `VehicleSystems`
// is (A) and may be built now; one that expects real faults to arrive from a fault manager is (B) and
// may not. So the FDIR row here reports what `PageState.Fault` ALREADY carries — `FlightDriver`'s own
// published report, which reads `None` / "NOMINAL" while nothing is flying — and never waits for or
// invents a fault. When Part B fills that channel this list gains rows without changing.
//
// ---- WHAT IS DELIBERATELY NOT HERE ----
// ⛔ NO FIRE, NO CABIN LEAK, NO TRIPPED-STRING ROW — AND THAT IS A FINDING, NOT AN OMISSION.
// `VehicleSystems` models all three (`SystemsState.Fire`, `.Leaking`, and six `StringState`s), and
// `SystemsPidPage` draws them. But `Alarms` NEVER SEES THEM: `VehicleSeverity` is life support +
// thermal + propellant + power, and `Mask` adds only FDIR and a closing-rate term. So a CABIN FIRE
// raises no severity anywhere — not the tab strip's red-nav, not the chrome bar, not the word above
// this list. Putting them in a scoped list here would have printed an ALARM row under a green word,
// which is the very defect this scoping exists to prevent. **The fix belongs in `Alarms`, not here**,
// and it is logged as its own register line rather than smuggled in.
//
// ⚠ NO TIMESTAMPS and NO ACKNOWLEDGEMENT, though the finding names both. A timestamp needs a clock
// and a latch — state that outlives a frame — and an acknowledgement is a control with a persistence
// question behind it (does it survive a page change? a scene change? who clears it?). Both are real
// work with real decisions in them; neither is required by this line's own DONE-when, and inventing
// the persistence rules for them here would be exactly the kind of quiet decision C1.1 exists to stop.
// Logged rather than guessed at.
// ============================================================================================
namespace DragonScreen
{
    /// <summary>One line of the alert list: what it is, how bad, and what it currently reads.</summary>
    public struct AlertItem
    {
        public string Label;
        public string Value;
        public Severity Sev;
    }

    /// <summary>Which severity this list is the components of. ⭐ EXACTLY the five distinct answers
    /// `VehicleSubsystemPage.LiveSeverity` can give, because the list has to be the WORKING of the word
    /// printed above it.</summary>
    public enum AlertScope : byte { LifeSupport, Thermal, Power, Propellant, Fdir }

    public static class AlertList
    {
        /// <summary>The most rows any one scope can produce. Callers size their buffer from this
        /// rather than allocating in the draw path.</summary>
        public const int Max = 4;

        /// <summary>
        /// Fill <paramref name="into"/> with the components of <paramref name="scope"/>'s severity that
        /// are actually saying something, worst first, and return how many were written. **0 on a dead
        /// feed** — with no vessel there is nothing to report, and a list that showed rows anyway would
        /// be inventing them.
        ///
        /// ⛔ THE LIST IS SCOPED, AND THAT IS NOT A DETAIL — IT IS THE WHOLE CORRECTNESS OF THE PANEL.
        /// The first version listed the WHOLE VEHICLE's alerts under a word that reports ONE SUBSYSTEM,
        /// and the preview showed it immediately: the Crew tab printed a green `NOMINAL` (life support
        /// is fine) over an amber `POWER 18%` row. **One panel, two answers** — the exact defect S51
        /// fixed on this same panel when a dead feed printed `NOMINAL` beside `NO DATA`. Scoping the
        /// list to the same severity the word reports makes that impossible by construction, and
        /// `Worst(everything this returns) == that severity` is a property a test can hold it to.
        ///
        /// ⛔ ORDERING IS WORST-FIRST AND STABLE. Rows of equal severity keep the order they are added
        /// in, so a crew glancing twice sees the same list in the same order.
        /// </summary>
        public static int Build(PageState s, AlertScope scope, AlertItem[] into)
        {
            if (into == null || !s.Valid) return 0;
            int n = 0;

            // ⛔ EVERY ROW'S VALUE IS FORMATTED FROM THE SAME NUMBER ITS SEVERITY IS BANDED FROM, and
            // that is not tidiness - it was a defect caught in the preview. The first version banded
            // `s.Cabin.Ppo2Psia` and PRINTED `s.Ppo2Text`, two different fields. The live glue fills
            // both from one reading so they agree in flight; a fixture that moved one and not the
            // other produced a render reading "PPO2 2.86" in ALARM RED - a row disagreeing with
            // itself about why it was there. One field per row means the row cannot lie about its own
            // reason, whoever fills the state.
            // ⚠ The unit strings below are the ones CabinLimits' own thresholds are expressed in.
            switch (scope)
            {
                // Alarms.LifeSupport takes the worst of exactly these three.
                case AlertScope.LifeSupport:
                    Add(into, ref n, "PPO2", Num(s.Cabin.Ppo2Psia, 2, " psia"),
                        Alarms.Band(s.Cabin.Ppo2Psia, CabinLimits.Ppo2Caution, CabinLimits.Ppo2Alarm));
                    Add(into, ref n, "CO2", Num(s.Cabin.Co2MmHg, 1, " mmHg"),
                        Alarms.Band(s.Cabin.Co2MmHg, CabinLimits.Co2Caution, CabinLimits.Co2Alarm));
                    Add(into, ref n, "CABIN PRESSURE", Num(s.Cabin.PressPsia, 2, " psia"),
                        Alarms.Band(s.Cabin.PressPsia, CabinLimits.PressCaution, CabinLimits.PressAlarm));
                    break;

                // Alarms.Thermal takes the worst of exactly these three.
                case AlertScope.Thermal:
                    Add(into, ref n, "CABIN TEMP", Num(s.Cabin.CabinTempC, 1, " C"),
                        Alarms.Band(s.Cabin.CabinTempC, CabinLimits.CabinTempCaution, CabinLimits.CabinTempAlarm));
                    Add(into, ref n, "LOOP A", Num(s.Cabin.LoopAC, 1, " C"),
                        Alarms.Band(s.Cabin.LoopAC, CabinLimits.LoopCaution, CabinLimits.LoopAlarm));
                    Add(into, ref n, "LOOP B", Num(s.Cabin.LoopBC, 1, " C"),
                        Alarms.Band(s.Cabin.LoopBC, CabinLimits.LoopCaution, CabinLimits.LoopAlarm));
                    break;

                // ⚠ These three scopes are ONE band each, so their list is one row - which is still
                // strictly more than the page had, because the row names the QUANTITY and its reading
                // where the word named only how bad it was.
                case AlertScope.Power:
                    Add(into, ref n, "BATTERY", Pct(s.Power01), Alarms.Low(s.Power01));
                    break;
                case AlertScope.Propellant:
                    Add(into, ref n, "PROPELLANT", Pct(s.Propellant01), Alarms.PropellantSeverity(s));
                    break;
                case AlertScope.Fdir:
                    Add(into, ref n, "FDIR", s.FaultText, Alarms.FdirSeverity(s));
                    break;
            }
            return Sort(into, n);
        }

        /// <summary>The severity this scope's list is the working of — `Alarms`' own function for it,
        /// called through one name so the page cannot pick a different one for the word than the list
        /// was built from.</summary>
        public static Severity SeverityOf(PageState s, AlertScope scope)
        {
            if (!s.Valid) return Severity.Nominal;
            switch (scope)
            {
                case AlertScope.LifeSupport: return Alarms.LifeSupport(s.Cabin);
                case AlertScope.Thermal:     return Alarms.Thermal(s.Cabin);
                case AlertScope.Power:       return Alarms.Low(s.Power01);
                case AlertScope.Propellant:  return Alarms.PropellantSeverity(s);
                case AlertScope.Fdir:        return Alarms.FdirSeverity(s);
            }
            return Severity.Nominal;
        }

        /// <summary>A row is only a row if it is actually saying something. ⛔ A NOMINAL band is
        /// dropped, and that is the difference between a LIST OF ALERTS and a list of readouts with
        /// most of them green — the second teaches a crew to stop reading it.</summary>
        static void Add(AlertItem[] into, ref int n, string label, string value, Severity sev)
        {
            if (sev < Severity.Caution || n >= into.Length || n >= Max) return;
            into[n].Label = label;
            into[n].Value = string.IsNullOrEmpty(value) ? Dashes.None : value;
            into[n].Sev = sev;
            n++;
        }

        /// <summary>Worst first, STABLE within a severity (an insertion sort, which is stable by
        /// construction and is the whole reason it is used on a list this short).</summary>
        static int Sort(AlertItem[] a, int n)
        {
            for (int i = 1; i < n; i++)
            {
                AlertItem k = a[i];
                int j = i - 1;
                while (j >= 0 && a[j].Sev < k.Sev) { a[j + 1] = a[j]; j--; }
                a[j + 1] = k;
            }
            return n;
        }

        static int Tripped(SystemsState y)
        {
            int t = 0;
            if (y.A1 == StringState.Tripped) t++;
            if (y.B1 == StringState.Tripped) t++;
            if (y.C1 == StringState.Tripped) t++;
            if (y.A2 == StringState.Tripped) t++;
            if (y.B2 == StringState.Tripped) t++;
            if (y.C2 == StringState.Tripped) t++;
            return t;
        }

        /// <summary>A physical reading at its own precision, with the unit CabinLimits states its
        /// thresholds in. ⚠ Plain "C" rather than "°C": the degree sign is not in every font this
        /// project renders through, and a row that reads "33.0 ?C" is worse than one without it.</summary>
        static string Num(double v, int dp, string unit)
        {
            if (double.IsNaN(v)) return Dashes.None;
            return v.ToString(dp == 1 ? "F1" : "F2") + unit;
        }

        static string Pct(double v01)
        {
            if (double.IsNaN(v01)) return Dashes.None;
            int p = (int)(v01 * 100.0 + 0.5);
            if (p < 0) p = 0; else if (p > 100) p = 100;
            return p + "%";
        }
    }
}
