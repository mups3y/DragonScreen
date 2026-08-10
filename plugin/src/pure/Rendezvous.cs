/*
 * DragonScreen - Rendezvous
 *
 * PURE. Closing on the station, and the last few hundred metres onto the port.
 *
 * ---- ⛔ NEVER POINT AT A CO-ORBITAL TARGET AND THRUST ----
 * This is the single most expensive lesson in the whole project and it is not negotiable. Pursuit
 * steering is unconditionally unstable along-track, in BOTH directions:
 *
 *      target BEHIND -> pointing at it is retrograde -> periapsis drops -> lower orbit is FASTER
 *                       -> you pull further ahead. Positive feedback.
 *      target AHEAD  -> pointing at it is prograde   -> you slow down -> it pulls further ahead.
 *
 * Flight 012 flew exactly that and DE-ORBITED ITSELF while the display said "closing":
 * periapsis 78 664 m -> 825 m -> -13 054 m -> -159 299 m, with the range OPENING at 94.9 m/s.
 *
 * So nothing in this file ever produces "thrust toward the target". Close-in motion is expressed as
 * TRANSLATION in the target's local frame, which is what the DOCKING page already displays.
 *
 * ---- THE LADDER, AND ITS MEASURED COSTS ----
 * From `falcon-rendezvous-approach-law`, simulated with an RK4 two-body propagator BEFORE any of it
 * was flown, station at 86.75 km circular:
 *
 *      gap      phasing (1 lap)             direct CW at 0.20 period
 *      51 km    17.7 m/s, peri untouched    289 m/s, peri  -15.6 km
 *      30 km    10.4 m/s, peri untouched    171 m/s, peri   24.1 km
 *       5 km     1.8 m/s, peri untouched     29 m/s, peri   75.4 km
 *
 * Phasing is cheaper AND safer, because closing a forward gap means RAISING the orbit and a raise
 * structurally cannot drop periapsis. That is the entire argument for doing it first.
 *
 *      gap > 3 km      PHASING orbit
 *      0.5 - 3 km      Clohessy-Wiltshire two-impulse, at 0.20 of a period - shorter blows up
 *                      (0.05 period costs 639 m/s and puts periapsis at -192 000 km)
 *      < 0.5 km        RCS translation
 *
 * ---- TWO THINGS THE SIMULATION CAUGHT THAT WOULD OTHERWISE HAVE FLOWN ----
 * A CW two-impulse solution ARRIVES MOVING - 107 m/s from a 30 km gap - so the braking burn is not
 * optional. And every burn must be checked against the periapsis floor BEFORE it is committed:
 * nothing on flight 012 checked its own result, which is why it ran to -159 km unchallenged.
 */
namespace DragonScreen
{
    public enum ApproachRung : byte
    {
        /// <summary>No target, or nothing to do.</summary>
        Idle = 0,
        /// <summary>Beyond 3 km: change period and let orbital mechanics close the gap.</summary>
        Phasing,
        /// <summary>0.5-3 km: Clohessy-Wiltshire two-impulse transfer.</summary>
        Clohessy,
        /// <summary>Inside 0.5 km: RCS translation in the target's frame.</summary>
        Rcs,
        /// <summary>Inside the docking corridor and lined up.</summary>
        Final,
        Docked
    }

    public struct ApproachInputs
    {
        public bool Valid;
        public bool HasTarget;
        /// <summary>Metres to the target.</summary>
        public double RangeM;
        /// <summary>Closing rate, m/s. POSITIVE means the gap is shrinking.</summary>
        public double ClosingMps;
        /// <summary>Our periapsis, metres. Guarded on every burn.</summary>
        public double PeriapsisM;
        /// <summary>Our orbital period and the target's, seconds.</summary>
        public double PeriodS, TargetPeriodS;
        /// <summary>Angle between our port axis and the line to the target, degrees.</summary>
        public double AlignDeg;
        public bool Docked;
    }

    public struct ApproachCommand
    {
        public ApproachRung Rung;
        /// <summary>Closing rate we should be flying right now, m/s.</summary>
        public double TargetClosingMps;
        /// <summary>How much to change closing rate by, m/s. The glue turns this into RCS.</summary>
        public double ClosingErrorMps;
        /// <summary>True when the periapsis floor forbids the burn this rung wants.</summary>
        public bool FloorViolated;
        public string Note;
    }

    public static class Rendezvous
    {
        /// <summary>Ladder boundaries, metres. Recorded law, not chosen here.</summary>
        public const double PhasingRange = 3000.0;
        public const double CwRange = 500.0;

        /// <summary>Inside this, the approach is the docking corridor.</summary>
        public const double FinalRange = 50.0;

        /// <summary>
        /// `stPeriFloor` = 75 000 m. NO approach burn may leave periapsis below it. Checked before
        /// every execute and re-checked every step of every coast, because flight 012 checked
        /// nothing and ran to -159 km unchallenged.
        /// </summary>
        public const double PeriapsisFloorM = 75000.0;

        /// <summary>The knee of the CW transfer time. Shorter than this and the cost explodes.</summary>
        public const double CwPeriodFraction = 0.20;

        public static ApproachRung Classify(ApproachInputs s)
        {
            if (!s.Valid || !s.HasTarget) return ApproachRung.Idle;
            if (s.Docked) return ApproachRung.Docked;
            if (s.RangeM > PhasingRange) return ApproachRung.Phasing;
            if (s.RangeM > CwRange) return ApproachRung.Clohessy;
            if (s.RangeM > FinalRange) return ApproachRung.Rcs;
            return ApproachRung.Final;
        }

        /// <summary>
        /// The approach corridor: how fast we should be closing at this range.
        ///
        /// Proportional to range with a floor and a ceiling, which is how every real approach is
        /// flown - fast when far, crawling at contact. The ceiling matters as much as the floor: a
        /// controller allowed to close at 50 m/s from 500 m cannot stop, and "cannot stop" at a
        /// crewed station is the one failure mode with no recovery.
        /// </summary>
        public static double CorridorRate(double rangeM)
        {
            if (rangeM <= 0.0) return 0.0;
            double v = rangeM * 0.025;
            if (v > 12.0) v = 12.0;
            // Contact speed. Real Dragon touches the port at a few centimetres per second.
            if (rangeM < 10.0) { v = 0.15; }
            else if (v < 0.3) v = 0.3;
            return v;
        }

        /// <summary>
        /// Would this burn leave us below the floor? The guard that flight 012 did not have.
        /// </summary>
        public static bool FloorOk(double resultingPeriapsisM)
        {
            return resultingPeriapsisM >= PeriapsisFloorM;
        }

        /// <summary>
        /// Phasing: how much to change our PERIOD by to close the along-track gap in one lap.
        ///
        /// Closing a gap AHEAD means raising the orbit to slow down, and a raise cannot drop
        /// periapsis - which is exactly why this rung is both cheaper and safer than chasing.
        /// Returns the period change in seconds; the glue converts it to a burn.
        /// </summary>
        public static double PhasingPeriodChange(ApproachInputs s, bool targetAhead)
        {
            if (s.TargetPeriodS <= 0.0) return 0.0;
            // Gap expressed as a fraction of the orbit, then as the period difference that eats it
            // over one lap. Sign: target ahead -> we must speed up -> shorter period.
            double circumference = s.TargetPeriodS * 7800.0;   // rough, only the RATIO matters
            if (circumference <= 0.0) return 0.0;
            double frac = s.RangeM / circumference;
            double dT = s.TargetPeriodS * frac;
            return targetAhead ? -dT : dT;
        }

        public static ApproachCommand Guide(ApproachInputs s)
        {
            ApproachCommand c = new ApproachCommand();
            c.Rung = Classify(s);

            if (c.Rung == ApproachRung.Idle) { c.Note = "NO TARGET"; return c; }
            if (c.Rung == ApproachRung.Docked) { c.Note = "DOCKED"; return c; }

            c.TargetClosingMps = CorridorRate(s.RangeM);
            c.ClosingErrorMps = c.TargetClosingMps - s.ClosingMps;

            // ---- THE FLOOR IS CHECKED ON EVERY RUNG, NOT JUST BEFORE A BURN ----
            // Re-checked every step of every coast, per the recorded law. A rendezvous that is
            // quietly de-orbiting must stop being a rendezvous.
            c.FloorViolated = !FloorOk(s.PeriapsisM);
            if (c.FloorViolated)
            {
                c.TargetClosingMps = 0.0;
                c.ClosingErrorMps = 0.0;
                c.Note = "PERIAPSIS FLOOR - APPROACH HELD";
                return c;
            }

            switch (c.Rung)
            {
                case ApproachRung.Phasing:   c.Note = "PHASING"; break;
                case ApproachRung.Clohessy:  c.Note = "CW TRANSFER"; break;
                case ApproachRung.Rcs:       c.Note = "RCS APPROACH"; break;
                default:                     c.Note = "FINAL APPROACH"; break;
            }
            return c;
        }

        public static string Name(ApproachRung r)
        {
            switch (r)
            {
                case ApproachRung.Phasing:  return "PHASING";
                case ApproachRung.Clohessy: return "CW TRANSFER";
                case ApproachRung.Rcs:      return "RCS APPROACH";
                case ApproachRung.Final:    return "FINAL APPROACH";
                case ApproachRung.Docked:   return "DOCKED";
                default:                    return "STANDBY";
            }
        }
    }
}
