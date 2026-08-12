/*
 * DragonScreen - Entry
 *
 * PURE. Crew Dragon's lifting entry: the angle-of-attack schedule, and the chutes.
 *
 * ---- ⛔ THE SCHEDULE IS MEASURED FLIGHT DATA. DO NOT DERIVE IT FROM THEORY. ----
 * `docs/FLIGHT_SYSTEMS.md` says this in as many words, and it is the standing instruction: read the
 * numbers off the recordings. They come from the `aoaRetro` column of `bb_dragon_CrewDragon_072` (⛔ NOT IN OUR ARCHIVE - the numbers are quoted from F9I's own comment at `dragon_deorbit.ks:38-44`, which IS verifiable; the recording behind them is not),
 * binned by altitude, on a flight that landed 6.3 km from its target:
 *
 *      70-55 km    0.07 - 0.11 deg    pure retrograde, the shield-forward coast in   -> 0.00
 *      50-30 km    14.6 - 15.0 deg    the lifting phase, full trim                   -> 15.00
 *      25-15 km     8.5 -  4.6 deg    lift bleeding off                              ->  8.25
 *      10-0  km     0.2 -  1.8 deg    essentially retrograde again                   ->  1.95
 *
 * Held as FRACTIONS of the capsule's trim angle - (0.00, 1.00, 0.55, 0.13) x 15 deg - so changing
 * the trim scales all four together. L/D is about 0.27.
 *
 * ---- ⛔ AND THE TRAP THAT COST A FLIGHT ----
 * The angles are measured AGAINST RETROGRADE, heat shield leading. Getting that backwards cost
 * CargoDragon_012: the guide vector sat 134.9 deg off the nose for the ENTIRE entry, correction
 * pinned at its 45 deg limit, with both navball markers drawn on the prograde side where a
 * heat-shield-first capsule never points. The source comment read the wrong way round for months.
 *
 * So `Retrograde` is true on every band here, it is not configurable, and the reason is written next
 * to it rather than left to be rediscovered.
 *
 * ---- WHAT THIS IS AND IS NOT ----
 * Dragon 2 steers a lifting entry by ROLLING the lift vector - bank-angle modulation, the
 * Apollo/Shuttle/Orion method, and the single most Dragon-specific piece of the whole flight
 * software. This file schedules the ANGLE OF ATTACK, which is what our own flown solution actually
 * commanded and what the recordings actually contain. Closed-loop bank steering to a splashdown
 * target is step 7 of docs/FLIGHT_SOFTWARE_PLAN.md and is NOT here. Saying so matters: an AoA
 * schedule flies the right shape of entry, it does not steer to a point.
 */
namespace DragonScreen
{
    public enum EntryBand : byte
    {
        /// <summary>Above the interface - nothing to do yet.</summary>
        Coast = 0,
        /// <summary>70-55 km. Shield-forward, pure retrograde.</summary>
        Interface,
        /// <summary>50-30 km. The lifting phase, full trim.</summary>
        High,
        /// <summary>25-15 km. Lift bleeding off.</summary>
        Low,
        /// <summary>10-0 km. Essentially retrograde again.</summary>
        Final,
        Drogues,
        Mains,
        Splashdown
    }

    public struct EntryInputs
    {
        public bool Valid;
        /// <summary>Altitude above the surface, metres.</summary>
        public double AltitudeM;
        public double SurfaceSpeed;
        public double VerticalSpeed;
        public bool Splashed;
        public bool DroguesOut, MainsOut;
        /// <summary>The trunk must be gone before entry - it is not a re-entry vehicle.</summary>
        public bool TrunkAttached;
    }

    public struct EntryCommand
    {
        public EntryBand Band;
        /// <summary>Degrees of angle of attack, measured off RETROGRADE.</summary>
        public double AngleOfAttackDeg;
        /// <summary>Always true. The heat shield leads. See the header.</summary>
        public bool Retrograde;
        public bool DeployDrogues, DeployMains;
        public string Note;
    }

    public static class Entry
    {
        /// <summary>Capsule trim off retrograde, degrees. L/D about 0.27.</summary>
        public const double TrimAngleDeg = 15.0;

        /// <summary>The four measured bands, as fractions of the trim angle.</summary>
        public static readonly double[] Fractions = { 0.00, 1.00, 0.55, 0.13 };

        /// <summary>Band ceilings in metres, from the binning above.</summary>
        public const double InterfaceTop = 70000.0;
        public const double HighTop = 50000.0;
        public const double LowTop = 25000.0;
        public const double FinalTop = 10000.0;

        /// <summary>Real Dragon numbers, already used by MissionPhase: 18 000 ft and 6 000 ft.</summary>
        public const double DrogueAltitude = 5486.0;
        public const double MainAltitude = 1830.0;

        public static double AngleFor(EntryBand b)
        {
            switch (b)
            {
                case EntryBand.Interface: return TrimAngleDeg * Fractions[0];
                case EntryBand.High:      return TrimAngleDeg * Fractions[1];
                case EntryBand.Low:       return TrimAngleDeg * Fractions[2];
                case EntryBand.Final:     return TrimAngleDeg * Fractions[3];
                default:                  return 0.0;
            }
        }

        public static EntryCommand Guide(EntryInputs s)
        {
            EntryCommand c = new EntryCommand();
            // NOT configurable. See the header - the flight this cost is named there.
            c.Retrograde = true;

            if (!s.Valid) { c.Note = "no vessel"; return c; }

            if (s.Splashed) { c.Band = EntryBand.Splashdown; c.Note = "SPLASHDOWN"; return c; }
            if (s.MainsOut) { c.Band = EntryBand.Mains; c.Note = "UNDER MAINS"; return c; }
            if (s.DroguesOut)
            {
                c.Band = EntryBand.Drogues;
                c.Note = "UNDER DROGUES";
                // Mains at 6 000 ft, once the drogues have done their job.
                c.DeployMains = s.AltitudeM <= MainAltitude;
                return c;
            }

            // Chutes come before any aerodynamic band: a schedule that says "FINAL, hold 1.95 deg"
            // while the vehicle is at 4 km and accelerating is worse than useless.
            if (s.AltitudeM <= DrogueAltitude)
            {
                c.Band = EntryBand.Final;
                c.AngleOfAttackDeg = AngleFor(EntryBand.Final);
                c.DeployDrogues = true;
                c.Note = "DROGUE DEPLOY";
                return c;
            }

            if (s.AltitudeM > InterfaceTop) { c.Band = EntryBand.Coast; c.Note = "COAST TO ENTRY"; }
            else if (s.AltitudeM > HighTop)  c.Band = EntryBand.Interface;
            else if (s.AltitudeM > LowTop)   c.Band = EntryBand.High;
            else if (s.AltitudeM > FinalTop) c.Band = EntryBand.Low;
            else                             c.Band = EntryBand.Final;

            c.AngleOfAttackDeg = AngleFor(c.Band);
            if (c.Note == null) c.Note = Name(c.Band);
            return c;
        }

        public static string Name(EntryBand b)
        {
            switch (b)
            {
                case EntryBand.Interface:  return "ENTRY INTERFACE";
                case EntryBand.High:       return "LIFTING ENTRY";
                case EntryBand.Low:        return "LIFT BLEED OFF";
                case EntryBand.Final:      return "TERMINAL DESCENT";
                case EntryBand.Drogues:    return "DROGUES";
                case EntryBand.Mains:      return "MAINS";
                case EntryBand.Splashdown: return "SPLASHDOWN";
                default:                   return "COAST TO ENTRY";
            }
        }
    }
}
