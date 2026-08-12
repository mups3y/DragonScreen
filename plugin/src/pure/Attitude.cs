/*
 * DragonScreen - Attitude
 *
 * PURE. The attitude control law, written for THIS mod.
 *
 * ---- ⛔ WHY THIS REPLACED THE kOS PORT, AND WHY IT WILL NOT BE PUT BACK ----
 * What was here was kOS's steering manager - `KosPid`, `TorquePI`, `maxstoppingtime`, `rollts`,
 * `rolltorquefactor` - taken in through MechJeb's C# port of it. It is a good controller IN kOS.
 * Inside this DLL it was a machine whose knobs we did not own, and every attempt to tune it was a
 * guess about what a kOS parameter means in a system that is not kOS. Eleven changes were made to
 * the booster's flip in one session, three of them had the DIRECTION of the effect backwards, and
 * the last one cost a vehicle: `min(pos,neg)` on the torque accounting dropped yaw authority from
 * 513 kN.m to 3.51, the booster could not rotate, and it sat in FLIP for 209 seconds.
 *
 * This law has no kOS in it. Every term is a physical quantity KSP already reports, and every
 * limit is DERIVED from the vehicle rather than dialled in:
 *
 *      error  ->  rate command   (bounded by what can actually be arrested)
 *             ->  torque command (arrest the rate error in one time constant)
 *             ->  actuation      (fraction of the torque the vehicle really has)
 *
 * ---- THE ONE IDEA THAT MATTERS: THE RATE BOUND IS A BRAKING CURVE ----
 * Never ask for a rate you could not stop before the error closes. With angular acceleration
 * alpha = torque / MoI and an error theta, the fastest arrestable rate is
 *
 *      omega_max = sqrt(2 * alpha * theta)
 *
 * which is the SAME law the landing burn already flies (`Landing.cs`, MechJeb's hoverslam
 * `0.9*sqrt(2*(a-g)*h)`), and for the same reason. It is not a tuning knob: it falls out of the
 * vehicle's own torque and inertia, so it is automatically right for a full stack, a spent booster
 * and a capsule without anybody choosing a number. `maxstoppingtime` was kOS's answer to this
 * question and it had to be retuned by hand for every phase; this does not.
 *
 * The margin is the same 0.9 the hoverslam uses, and it does the same job: a controller that solves
 * the limit exactly has no authority left to correct with.
 *
 * ---- STATELESS ON PURPOSE ----
 * No integrators. A PID carried between ticks is state that goes stale across a staging event, a
 * vessel switch or a scene reload, and this project has already lost a flight to exactly that
 * ("a PID recreated every frame is just a P controller with extra steps" was the argument FOR
 * keeping the state; the counter-argument is that the state was wrong after the vehicle changed).
 * There is no steady-state error to integrate away here: gravity does not pull a spacecraft off a
 * commanded attitude, so proportional-plus-damping is sufficient and cannot wind up.
 */
namespace DragonScreen
{
    public static class Attitude
    {
        /// <summary>Fraction of the arrestable rate we will actually command. See the header.</summary>
        public const double RateMargin = 0.9;

        /// <summary>
        /// Seconds to arrest a rate error. The single agility knob, and it is a TIME, not a gain -
        /// so it means the same thing on every vehicle.
        /// </summary>
        public const double DefaultTimeConstantS = 1.0;

        /// <summary>
        /// Seconds over which an attitude error is nulled. `rate = error / SettleTimeS`.
        ///
        /// 1 s reproduces the MEASURED behaviour of a gravity turn that reached orbit: 0.44 deg of
        /// error commanding 0.44 deg/s, against a flown average of 0.45. It is not a fitted gain -
        /// it is the observation that a working ascent nulls its error in about a second.
        /// </summary>
        public const double SettleTimeS = 1.0;

        // ---- MAXIMUM COMMANDED RATE PER PHASE, degrees/second. ALL MEASURED. ----
        // Ascent from flight_0813_005927 (max 1.02 observed); the booster figures are F9I's own
        // peaks over bb_booster_001..008, the vehicle that lands 0.34-0.56 m from the pad, in
        // docs/F9I_BOOSTER_TARGETS.md. Each carries headroom over what was actually flown.
        public const double AscentMaxRateDps = 2.0;      // flown max 1.02
        public const double FlipMaxRateDps = 15.0;       // F9I flip+boostback peak 14.9
        public const double CoastMaxRateDps = 4.0;       // F9I coast peak 2.9
        public const double EntryMaxRateDps = 25.0;      // F9I entry burn peak 24.0
        public const double DescentMaxRateDps = 10.0;    // F9I descent peak 9.4
        public const double LandingMaxRateDps = 3.0;     // F9I landing burn peak 0.1

        /// <summary>
        /// The capsule alone, after S2 separation. MEASURED on flight_0813_005927, which docked and
        /// landed: 1.85 deg/s average docking, 2.62 approach, 1.87 de-orbit, 0.43 entry. Roughly
        /// four times the flown average, and far below the 40-125 transients in that same file -
        /// which were the controller misbehaving, not a requirement.
        /// </summary>
        public const double CapsuleMaxRateDps = 10.0;

        /// <summary>Below this the axis is left alone, radians. Stops thruster chatter at the null.</summary>
        public const double DeadbandRad = 0.0015;          // ~0.09 degrees

        /// <summary>Most an actuation may change in one tick, so a step demand cannot slam.</summary>
        /// ⚠ 0.25 reached full deflection in four ticks - 0.08 s - which is a slam, not a slew.
        public const double MaxSlewPerTick = 0.08;

        /// <summary>
        /// The fastest rate that can still be arrested before the error closes, rad/s.
        ///
        /// `torque` kN.m, `moi` t.m^2 - the units cancel to rad/s^2, which is why no conversion
        /// appears here. Zero inertia or zero torque means no rate is safe, and it says so.
        /// </summary>
        public static double ArrestableRate(double errorRad, double torque, double moi)
        {
            if (moi <= 0.0 || torque <= 0.0) return 0.0;
            double e = (errorRad < 0.0) ? -errorRad : errorRad;
            double alpha = torque / moi;
            return RateMargin * System.Math.Sqrt(2.0 * alpha * e);
        }

        /// <summary>
        /// Rate to command for a given attitude error, rad/s. Signed with the error.
        ///
        /// Inside the deadband the answer is zero - not a small number, zero. A controller that
        /// keeps asking for a millidegree of correction burns monopropellant for ever, which is
        /// what "pulsing the RCS non-stop" looks like from the cockpit.
        /// </summary>
        public static double RateCommand(double errorRad, double torque, double moi,
                                         double maxRateRad)
        {
            if (errorRad > -DeadbandRad && errorRad < DeadbandRad) return 0.0;

            // ---- ⛔ PROPORTIONAL, CLAMPED. THE BRAKING CURVE ALONE WAS A DISASTER. ----
            // The first version returned `ArrestableRate` outright - 0.9*sqrt(2*alpha*theta) - and
            // it flipped the vehicle on the pad. That law commands MORE rate for MORE error, which
            // is right for a translation arriving at a wall and exactly backwards for a launch
            // vehicle: at 10.9 deg of error it asked for 9.19 deg/s.
            //
            // MEASURED, a gravity turn that reached orbit (flight_0813_005927, 415 samples):
            //
            //      pitch rate   avg 0.45 deg/s   max 1.02
            //      attitude err avg 0.10 deg     max 0.44
            //
            // So a working ascent never exceeds about one degree per second, and holds a tenth of
            // a degree of error. Nine degrees per second is not a tuning error, it is a different
            // manoeuvre. `theta / SettleTimeS` reproduces the measured behaviour - 0.44 deg of
            // error asks for 0.44 deg/s - and the clamp stops a large error ever demanding a slew
            // the airframe cannot survive.
            double w = System.Math.Abs(errorRad) / SettleTimeS;
            if (maxRateRad > 0.0 && w > maxRateRad) w = maxRateRad;

            // ...and never ask for a rate that could not be arrested. This bound is still correct;
            // it was only wrong as the PRIMARY command. It almost never binds now.
            double stop = ArrestableRate(errorRad, torque, moi);
            if (stop > 0.0 && w > stop) w = stop;

            return (errorRad < 0.0) ? -w : w;
        }

        /// <summary>
        /// Torque needed to remove a rate error in one time constant, kN.m.
        ///
        /// tau = I * dOmega / dt, which is Newton's second law for rotation and nothing more.
        /// </summary>
        public static double TorqueCommand(double rateErrorRad, double moi, double timeConstantS)
        {
            if (timeConstantS <= 0.0) return 0.0;
            return moi * rateErrorRad / timeConstantS;
        }

        /// <summary>
        /// Torque demand to a control-axis fraction in [-1, 1], slew-limited.
        ///
        /// ⚠ `torqueAvail` must be the authority in the direction being COMMANDED. A vehicle whose
        /// nozzles are lopsided has more of it one way than the other, and using the weaker figure
        /// for both throws away an axis that is perfectly usable - measured on the booster, whose
        /// yaw is 513 kN.m one way and 3.51 the other.
        /// </summary>
        public static double Actuate(double torqueCmd, double torqueAvail, double previous)
        {
            if (torqueAvail <= 0.0) return 0.0;
            double a = torqueCmd / torqueAvail;
            if (a > 1.0) a = 1.0;
            if (a < -1.0) a = -1.0;

            double d = a - previous;
            if (d > MaxSlewPerTick) a = previous + MaxSlewPerTick;
            else if (d < -MaxSlewPerTick) a = previous - MaxSlewPerTick;
            return a;
        }

        /// <summary>
        /// One axis, end to end: attitude error and measured rate in, actuation out.
        /// </summary>
        public static double Axis(double errorRad, double rateRad, double torque, double moi,
                                  double timeConstantS, double previous)
        {
            double want = RateCommand(errorRad, torque, moi, 0.0);
            double tq = TorqueCommand(want - rateRad, moi, timeConstantS);
            return Actuate(tq, torque, previous);
        }

        /// <summary>
        /// Total attitude error beyond which roll is not worth fighting for, degrees.
        ///
        /// Kept as a POLICY - get the nose where it is going, then worry about which way up. It is
        /// not a kOS parameter; it is the observation that roll authority spent while the vehicle is
        /// still slewing is authority the slew does not get.
        /// </summary>
        public const double RollControlRangeDeg = 45.0;
    }
}
