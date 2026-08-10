/*
 * DragonScreen - CabinEnvironment
 *
 * PURE. The life-support readouts the real VEHICLE page shows and stock KSP does not model:
 * PPO2, CO2, cabin pressure, cabin temperature, the two coolant loops, and the two power buses.
 *
 * ---- SIMULATED, NOT FAKED, AND THE DIFFERENCE IS THE WHOLE POINT ----
 * User's call, 2026-08-05: *"we should 'fake' things like ppo2 cabin temp cabin pressure etc. It
 * would look more realistic if they acted as if they were functioning rather than no display or
 * reading all zero."* Right, and my earlier objection was aimed at the wrong target.
 *
 *     A CONSTANT or a random number is a lie - it is indistinguishable from a dead sensor, which is
 *     what "never draw invented telemetry" exists to prevent.
 *
 *     A value DERIVED FROM REAL STATE by a stated model is a simulation, which is what this whole
 *     mod is. A cabin that warms during entry and a CO2 reading that climbs with four crew aboard
 *     are not pretending - they are modelling, visibly and consistently.
 *
 * So every number here MOVES, and moves BECAUSE OF SOMETHING. Nothing is a constant with noise on
 * top. Where a real KSP input exists it is used; where none does, the driver is crew count, hull
 * temperature or mission time, and the relationship is written down beside it.
 *
 * ---- WHAT IS REAL AND WHAT IS MODELLED ----
 *     REAL      hull temperature, crew count and capacity, electric charge and its flow
 *     MODELLED  the mapping from those to PPO2 / CO2 / pressure / cabin temp / loop temps
 *
 * ---- DETERMINISTIC ----
 * No random numbers. Two screens showing the same instant must agree, and a value that jitters
 * independently per frame reads as a broken sensor rather than a live one. Everything is a function
 * of the inputs, so the same state always gives the same reading.
 */
using System;

namespace DragonScreen
{
    /// <summary>What the model is driven by. All of it is real vessel state.</summary>
    public struct CabinInputs
    {
        public int Crew;
        public int CrewCapacity;
        /// <summary>Hull temperature in Celsius. REAL - KSP models part heating.</summary>
        public double HullTempC;
        /// <summary>Seconds. Drives slow drift so nothing sits perfectly still.</summary>
        public double MissionTime;
        /// <summary>Stored electric charge, 0..1 of capacity. REAL.</summary>
        public double Power01;
        /// <summary>Net charge flow, units/sec, positive = charging. REAL.</summary>
        public double PowerFlow;
        /// <summary>False when the vessel has no electricity - the cabin systems then fail visibly.</summary>
        public bool Powered;
    }

    /// <summary>Values and their gauge fractions. Fractions are what the dials need; values print.</summary>
    public struct CabinReadout
    {
        public double Ppo2Psia,  Ppo201;
        public double Co2MmHg,   Co201;
        public double PressPsia, Press01;
        public double CabinTempC, CabinTemp01;
        public double LoopAC,    LoopA01;
        public double LoopBC,    LoopB01;
        public double NetPwr1W,  NetPwr2W;
    }

    public static class Cabin
    {
        // ---- FULL-SCALE RANGES ----
        // Chosen so the NOMINAL reading sits in the middle third of the dial. A gauge whose needle
        // never leaves one end tells the crew nothing; one that pegs at nominal cannot show trouble.
        public const double Ppo2FullScale  = 5.0;    // psia. Nominal ~3.0
        public const double Co2FullScale   = 8.0;    // mmHg. Nominal <1, alarm above ~5
        public const double PressFullScale = 20.0;   // psia. Nominal 14.7 (sea level)
        public const double TempFullScale  = 40.0;   // deg C. Nominal ~22
        // A dial's full scale MUST sit above its alarm limit, or the needle pegs before the alarm it
        // is supposed to warn you about can fire - the reading stops carrying information exactly
        // when it matters. CabinLimits.LoopAlarm is 55 C, so the original 50 was wrong.
        //
        // 60 was ALSO wrong, and only flying it showed why: clearing the alarm limit is necessary but
        // not sufficient - the scale has to clear the highest reading the MODEL can produce. Loop A
        // hit 63.3 C after an abort and pegged. The loops now saturate near 67 (see Compute), so 80
        // leaves the needle on the dial at peak entry with room to spare.
        // Pinned by a headless test that checks every limit against its own dial.
        public const double LoopFullScale  = 80.0;   // deg C
        /// <summary>W. Each of the two buses; the dial pegs at a bus running flat out.</summary>
        public const double NetPwrFullScale = 2000.0;

        // Nominal set points, from the real vehicle where known.
        private const double PressNominal = 14.7;    // psia, sea-level equivalent
        private const double TempNominal  = 22.0;    // deg C, a habitable cabin
        private const double Ppo2Nominal  = 3.0;     // psia partial pressure of oxygen

        public static CabinReadout Compute(CabinInputs s)
        {
            CabinReadout r = new CabinReadout();

            // A slow, smooth wander so the dials are alive without twitching. Two periods that do
            // not divide into each other, so the readouts never all move together and look coupled.
            double slow = Math.Sin(s.MissionTime / 47.0);
            double slower = Math.Sin(s.MissionTime / 113.0);

            // ---- UNPOWERED IS A REAL FAILURE AND MUST LOOK LIKE ONE ----
            // With no electricity the scrubbers and the thermal loops stop. Rather than freezing the
            // display, the readings DEGRADE: CO2 climbs, oxygen falls, and the cabin drifts toward
            // the hull. That is what the crew would actually see, and it is the strongest argument
            // for simulating rather than faking - a fake cannot fail convincingly.
            double fail = s.Powered ? 0.0 : 1.0;

            // CO2 rises with the number of people breathing and falls with a working scrubber.
            // 0.35 mmHg per crew member is not a measured constant - it is a slope chosen so a full
            // capsule sits comfortably under the caution band and an unpowered one climbs out of it.
            double crew = s.Crew;
            r.Co2MmHg = 0.25 + crew * 0.35 + slow * 0.04 + fail * 4.5;

            // Oxygen partial pressure: nominal, drawn down slightly by crew, collapsing if unpowered.
            r.Ppo2Psia = Ppo2Nominal - crew * 0.04 + slower * 0.05 - fail * 1.2;

            // Cabin pressure holds; a leak is not modelled, so this is the steadiest reading on the
            // page and should be - a pressure gauge that wanders is alarming for the wrong reason.
            r.PressPsia = PressNominal + slower * 0.06;

            // ---- CABIN TEMPERATURE FOLLOWS THE HULL, WHICH IS REAL ----
            // KSP heats parts during entry, so this rises when the vehicle actually gets hot. The
            // blend is deliberately weak (8%) because an active thermal system is fighting it; when
            // power is lost the blend goes to 45% and the cabin starts tracking the hull properly.
            double blend = s.Powered ? 0.08 : 0.45;
            r.CabinTempC = TempNominal * (1.0 - blend) + s.HullTempC * blend + slow * 0.3;

            // ---- COOLANT LOOPS SATURATE. THEY DO NOT TRACK THE HULL FOREVER ----
            // These were LINEAR in hull temperature: `26.5 + (hull - 22) * 0.25`. Found in flight
            // 2026-08-06 reading LOOP A 63.3 C - past its own 60 C full scale, dial pegged, STATE
            // ALARM latched - after an abort had warmed the pod to roughly 170 C.
            //
            // Extrapolate that and it is worse than a wrong number: KSP hulls reach 1000 C+ on entry,
            // which the linear form turned into a 270 C coolant loop and a red alarm for the whole of
            // the most interesting phase of the mission. An alarm that is always on is not an alarm.
            //
            // A loop with an active thermal system behind it holds near setpoint, degrades as its
            // authority is used up, and levels off - it cannot follow the skin. Exponential approach
            // to a ceiling gives exactly that shape from one line, with no clamping discontinuity:
            //
            //      hull   22 C -> A 26.5   nominal, on the pad
            //      hull  170 C -> A 43.7   hot vehicle, caution band, no alarm
            //      hull 1000 C -> A 65.1   peak entry: alarms, as it should, and stays on the dial
            //
            // A is the warm loop, B the cold one, and B has both a lower ceiling and a slower
            // approach because it is the one being protected.
            double excess = s.HullTempC - TempNominal;
            if (excess < 0.0) excess = 0.0;
            r.LoopAC = 26.5 + 40.0 * (1.0 - System.Math.Exp(-excess / 300.0)) + slow * 0.4;
            r.LoopBC = 20.0 + 28.0 * (1.0 - System.Math.Exp(-excess / 340.0)) + slower * 0.3;

            // ---- POWER BUSES ARE REAL ----
            // Net charge flow, split across two buses. Bus 1 carries the larger share, as it would
            // with avionics on it. Scaled to watts by a nominal factor - KSP's electric charge has
            // no defined wattage, so the SCALE is arbitrary but the SIGN and the shape are real:
            // negative when draining, positive when the arrays are making more than the load.
            double watts = s.PowerFlow * 120.0;
            r.NetPwr1W = watts * 0.55;
            r.NetPwr2W = watts * 0.45;

            r.Ppo201     = Frac(r.Ppo2Psia,   Ppo2FullScale);
            r.Co201      = Frac(r.Co2MmHg,    Co2FullScale);
            r.Press01    = Frac(r.PressPsia,  PressFullScale);
            r.CabinTemp01 = Frac(r.CabinTempC, TempFullScale);
            r.LoopA01    = Frac(r.LoopAC,     LoopFullScale);
            r.LoopB01    = Frac(r.LoopBC,     LoopFullScale);
            return r;
        }

        private static double Frac(double v, double full)
        {
            if (full <= 0.0 || double.IsNaN(v)) return 0.0;
            double f = v / full;
            return (f < 0.0) ? 0.0 : (f > 1.0) ? 1.0 : f;
        }
    }
}
