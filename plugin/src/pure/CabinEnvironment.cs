/*
 * DragonScreen - CabinEnvironment
 *
 * PURE. The life-support readouts the real VEHICLE page shows:
 * PPO2, CO2, cabin pressure, cabin temperature, the two coolant loops, and the two power buses.
 *
 * ---- PPO2 AND CO2 ARE NOW DRIVEN BY REAL TAC LIFE SUPPORT ----
 * The install runs TAC Life Support v0.18 and the Crew Dragon carries its LifeSupportModule, so the O2
 * SUPPLY really depletes and CO2 really accumulates on the vessel. LifeSupportBridge reads those (the
 * Dragon's own tanks, isolated from the station when docked), and this file turns the real supply and
 * accumulator FRACTIONS into the ppO2 and CO2 gauge readings. TAC's Oxygen/CarbonDioxide are STORED
 * consumables, not cabin air, so the gauge mapping is a stated MODEL keyed on real depletion - the number
 * is real, "what a cabin partial pressure would read at that supply level" is the model. See
 * dragonscreen-tac-life-support. When TAC is absent (HasLifeSupport == false) the old crew-count model is
 * the fallback, so the display still lives without a life-support mod.
 *
 * ---- WHAT IS REAL AND WHAT IS MODELLED ----
 *     REAL      O2 supply + CO2 accumulator (TAC), hull temperature, crew, electric charge and its flow
 *     MODELLED  ppO2/CO2 gauge mapping from the real TAC fractions; cabin pressure; cabin + loop temps
 *               (TAC models no cabin atmosphere, pressure or temperature, and no compatible mod does)
 *
 * A stated model driven by real state is a simulation, not a fake: a value DERIVED from real depletion by
 * a written-down rule is what this whole mod is; a bare constant with noise would be a dead sensor, which
 * is what "never draw invented telemetry" exists to prevent. Every number here MOVES, and moves because
 * of something real.
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

        // ---- REAL TAC LIFE SUPPORT (filled from LifeSupportBridge). ----
        /// <summary>TAC is modelling this vessel's life support. False = use the crew-count fallback model.</summary>
        public bool HasLifeSupport;
        /// <summary>Breathing-O2 SUPPLY remaining, 0..1. REAL - TAC consumes it. Drives ppO2 when HasLifeSupport.</summary>
        public double OxygenFrac;
        /// <summary>Captured-CO2 ACCUMULATOR fill, 0..1. REAL - TAC fills it. Drives CO2 when HasLifeSupport.</summary>
        public double Co2Frac;
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

            double crew = s.Crew;
            if (s.HasLifeSupport)
            {
                // ---- DRIVEN BY REAL TAC STATE ----
                // TAC's Oxygen/CarbonDioxide are STORED consumables, not cabin air, so these are a stated
                // MODEL keyed on the real supply/accumulator fractions - not a literal partial pressure.
                //
                // ppO2: the cabin is regulated from the O2 supply and holds near nominal until the supply
                // is nearly exhausted, then falls. 3.0 at full supply -> caution (2.5) as it drops through
                // ~17% -> below the 2.0 alarm at empty. Unpowered collapses it (circulation/scrubbers down).
                double o2health = Clamp01(s.OxygenFrac / 0.20);           // 1.0 while supply >= 20%
                r.Ppo2Psia = 1.5 + 1.5 * o2health + slower * 0.05 - fail * 1.2;

                // CO2: the Dragon has no active scrubber in TAC, so captured CO2 accumulates toward the
                // tank cap over the mission - the gauge climbs with that real fraction (and with crew, and
                // spikes unpowered). A full accumulator with a full crew reaches the ~6 mmHg alarm.
                r.Co2MmHg = 0.4 + crew * 0.15 + Clamp01(s.Co2Frac) * 5.5 + slow * 0.04 + fail * 4.5;
            }
            else
            {
                // ---- FALLBACK MODEL (TAC absent) ----
                // The original crew-count model, kept so the display still lives without a life-support
                // mod. 0.35 mmHg per crew is a slope chosen so a full capsule sits under the caution band.
                r.Co2MmHg = 0.25 + crew * 0.35 + slow * 0.04 + fail * 4.5;
                r.Ppo2Psia = Ppo2Nominal - crew * 0.04 + slower * 0.05 - fail * 1.2;
            }

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

        private static double Clamp01(double x) { return (x < 0.0) ? 0.0 : (x > 1.0) ? 1.0 : x; }
    }
}
