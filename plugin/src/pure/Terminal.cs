/*
 * DragonScreen - Terminal
 *
 * PURE. The last 7.5 km: which way this capsule lands, when the chutes come out, and - if it is
 * coming down on SuperDracos - when to cut them and light the engines. Ported from
 * `F9I/dragon_deorbit.ks` - `DgTerminal:2411`, `DgTerminalParachute:2356`, `DgTerminalPropulsive:2372`.
 *
 * ---- ⛔ THE MODE IS CHOSEN ON WHAT IS ABOARD, NOT ON WHAT WAS PLANNED ----
 * Three things must all be true to land propulsively: the crew asked for it, there are SuperDracos on
 * the pod, and there are at least `MonoGateUnits` of monopropellant left. Any one of them missing and
 * it is parachutes - and the crew are TOLD WHICH ONE, because "propulsive unavailable" with no reason
 * is the kind of message that gets ignored until the flight it mattered.
 *
 * `falcon-detect-by-capability`: F9I used to test the vehicle's NAME here and changed to asking
 * whether the engines exist, because there are two crew Dragon pods in this install and only one of
 * them has the parts. We inherit the capability test, not the name test.
 *
 * ---- ⛔ THE CHUTES ARE CUT ONLY AFTER THE ENGINES ARE PROVEN LIT ----
 * The propulsive sequence deploys drogues FIRST, lights the SuperDracos underneath them, and only then
 * cuts. If the engines will not light, the chutes stay out and the capsule lands on them - a soft
 * landing nobody planned instead of a fast one nobody survives. That ordering is the whole safety
 * argument of the propulsive path and it must not be re-ordered for tidiness.
 *
 * ---- ⚠ THESE ALTITUDES ARE KERBIN'S, NOT THE REAL DRAGON'S ----
 * `pure/Entry.cs` carries the real vehicle's chute altitudes (5 486 m / 1 830 m - 18 000 and 6 000
 * feet) because it describes the real procedure. The numbers HERE are F9I's, and they are what has
 * actually flown: Kerbin's scale height is about 5.6 km against Earth's 8.5, so the real altitudes
 * arrive at a dynamic pressure the drogues were never sized for. Flown beats authentic. The two files
 * disagree on purpose; do not reconcile them.
 */
namespace DragonScreen
{
    public enum LandingMethod : byte
    {
        Parachute = 0,
        Propulsive = 1
    }

    public static class Terminal
    {
        // ---- F9I's CONSTANTS. dragon_deorbit.ks:475, 505-507, 2372-2410. ----

        /// <summary>Radar altitude at which entry guidance hands over, metres. `dgDrogueAlt`.</summary>
        public const double HandoverAltM = 7500.0;
        /// <summary>Drogues come out no later than here on a PARACHUTE landing, metres.</summary>
        public const double DrogueFloorChuteM = 4500.0;
        /// <summary>
        /// ...and here on a PROPULSIVE one, metres. Higher on purpose: everything after it - light the
        /// engines, prove they lit, cut the chutes - has to happen before the landing burn is due.
        /// </summary>
        public const double DrogueFloorPropulsiveM = 5000.0;
        /// <summary>Fastest airspeed the drogues may be deployed into, m/s. `dgDrogueMaxV`.</summary>
        public const double DrogueMaxSpeedMps = 560.0;
        /// <summary>Mains, metres. `dgMainAlt`.</summary>
        public const double MainAltM = 2000.0;
        /// <summary>Monopropellant needed to attempt a SuperDraco landing, units. `dgMonoGate`.</summary>
        public const double MonoGateUnits = 40.0;

        /// <summary>Landing gear/bell offset below the radar altimeter, metres. F9I's `dgH`.</summary>
        public const double HeightOffsetM = 4.0;
        /// <summary>Arm the burn when the stopping distance is within this factor of the height left.</summary>
        public const double ArmFactor = 2.2;
        /// <summary>...but never above this radar altitude, metres.</summary>
        public const double ArmFloorM = 120.0;
        /// <summary>Cut the chutes and commit at this factor of the stopping distance.</summary>
        public const double BurnFactor = 1.6;
        /// <summary>Hover thrust carries this much margin over weight.</summary>
        public const double HoverMargin = 1.05;
        /// <summary>Hand the throttle to the hover law once descent has been arrested to this, m/s.</summary>
        public const double HoverHandoverMps = -5.0;
        /// <summary>Give up waiting for touchdown after this long, seconds.</summary>
        public const double TouchdownTimeoutS = 45.0;

        /// <summary>
        /// Which way this capsule is going to land, and why. `DgTerminal`.
        ///
        /// `why` is only meaningful when the crew asked for propulsive and are not getting it.
        /// </summary>
        public static LandingMethod Choose(bool wantPropulsive, bool hasLandingEngines,
                                           double monoUnits, out string why)
        {
            why = "";
            if (!wantPropulsive) { why = "parachute landing selected"; return LandingMethod.Parachute; }
            if (!hasLandingEngines)
            {
                why = "no pod engines";
                return LandingMethod.Parachute;
            }
            if (monoUnits < MonoGateUnits)
            {
                why = "mono " + monoUnits.ToString("F0") + " < " + MonoGateUnits.ToString("F0");
                return LandingMethod.Parachute;
            }
            return LandingMethod.Propulsive;
        }

        /// <summary>
        /// Time to put the drogues out?
        ///
        /// Either the air has slowed us to a speed they survive, or we have run out of altitude to
        /// wait in. The floor is the one that fires on a steep entry, and it is not optional: a capsule
        /// that waits for `DrogueMaxSpeedMps` all the way down waits until there is no descent left.
        /// </summary>
        public static bool DrogueReady(double airspeedMps, double radarAltM, LandingMethod m)
        {
            double floor = (m == LandingMethod.Propulsive)
                         ? DrogueFloorPropulsiveM : DrogueFloorChuteM;
            return airspeedMps < DrogueMaxSpeedMps || radarAltM < floor;
        }

        /// <summary>Mains out.</summary>
        public static bool MainsReady(double radarAltM) { return radarAltM < MainAltM; }

        /// <summary>Height above the bells rather than above the altimeter.</summary>
        public static double TrueRadarM(double radarAltM) { return radarAltM - HeightOffsetM; }

        /// <summary>
        /// The deceleration the engines can actually deliver, m/s². Gravity is subtracted because it
        /// is still pulling down while they push up; a solve that forgets it lands early and hard.
        /// </summary>
        public static double MaxDecelMps2(double availableThrustKn, double massT, double gravityMps2)
        {
            if (massT <= 0.0) return 0.001;
            double a = (availableThrustKn / massT) - gravityMps2;
            return (a > 0.001) ? a : 0.001;
        }

        /// <summary>Distance needed to stop from this vertical speed. `dgStopDist`.</summary>
        public static double StopDistanceM(double verticalSpeedMps, double maxDecelMps2)
        {
            if (maxDecelMps2 <= 0.0) return double.MaxValue;
            return (verticalSpeedMps * verticalSpeedMps) / (2.0 * maxDecelMps2);
        }

        /// <summary>Arm the engines and drop the gear: the burn is close enough to be worth being ready for.</summary>
        public static bool ArmGate(double trueRadarM, double stopDistM)
        {
            double gate = stopDistM * ArmFactor;
            if (gate < ArmFloorM) gate = ArmFloorM;
            return trueRadarM <= gate;
        }

        /// <summary>
        /// Commit: cut the chutes and start the burn.
        ///
        /// ⚠ 1.6× the stopping distance, not 1.0. The margin is what the chutes are traded for - once
        /// they are cut the capsule accelerates again, and a gate at the bare stopping distance would
        /// already be late by the time the engines came up.
        /// </summary>
        public static bool BurnGate(double trueRadarM, double stopDistM)
        {
            return trueRadarM <= (stopDistM * BurnFactor) + HeightOffsetM;
        }

        /// <summary>Throttle for the landing burn. The same `StopDist/TrueRadar` ratio the booster uses.</summary>
        public static double LandingThrottle(double trueRadarM, double stopDistM)
        {
            return Deorbit.LandingThrottle(trueRadarM, stopDistM);
        }

        /// <summary>Descent arrested - hold it there rather than climbing back up.</summary>
        public static bool HoverHandover(double verticalSpeedMps)
        {
            return verticalSpeedMps > HoverHandoverMps;
        }

        /// <summary>Throttle that holds the capsule up, plus 5%.</summary>
        public static double HoverThrottle(double massT, double gravityMps2, double availableThrustKn)
        {
            double t = (availableThrustKn > 0.001) ? availableThrustKn : 0.001;
            double h = HoverMargin * ((massT * gravityMps2) / t);
            return (h > 1.0) ? 1.0 : h;
        }
    }
}
