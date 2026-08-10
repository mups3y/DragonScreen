/*
 * DragonScreen - AttitudePid
 *
 * PURE. kOS's steering-manager PID, ported from MechJeb's own C# port of it:
 * `Desktop/mechjeb_src/MechJeb2/AttitudeControllers/KosPIDLoop.cs` and `TorquePI.cs`.
 *
 * ---- WHY THIS EXISTS: WE WERE STEERING WITH STOCK SAS AND F9I NEVER WAS ----
 * F9I flies `lock steering to lookdirup(dir, up)`, which is kOS's STEERING MANAGER - a cascaded
 * controller with torque feed-forward that F9I then tunes per phase (`maxstoppingtime` 0.05 / 1 / 10,
 * `rollts`, `rolltorquefactor`). We were calling `SAS.SetTargetOrientation(dir, false)`, which is
 * stock SAS: built to hold a fixed navball marker, no roll reference, no feed-forward, nothing
 * tunable. That is the architectural reason our ascent wandered and F9I's does not.
 *
 * MechJeb already ported kOS's controller to C#, so this is a port of a port rather than a
 * reimplementation - which matters, because the details below are exactly the sort of thing that
 * gets "simplified" out by someone rewriting from the idea.
 *
 * ---- THE THREE DETAILS THAT ARE NOT ORDINARY PID ----
 *
 * 1. **Gains are set from the MOMENT OF INERTIA every tick.** `TorquePI` does `Ki = Kp = 4 * MoI`.
 *    So the controller retunes itself as the vehicle burns propellant and stages - which is the
 *    whole reason one set of gains can fly a full stack, a spent booster and a capsule. This is what
 *    "per-configuration gains" in the flight-software plan actually turns out to mean.
 *
 * 2. **Integral windup is corrected by BACK-CALCULATION, not by clamping.** When the output
 *    saturates, the I-term is recomputed as `output - (p + d)` so it holds exactly the amount that
 *    keeps the output at the limit. A plain clamp leaves the integral carrying authority it cannot
 *    deliver, which then dumps the moment the error flips.
 *
 * 3. **ExtraUnwind DOUBLES Ki while the error and the accumulated sum disagree in sign.** When the
 *    vehicle has overshot, the integral is pushing the wrong way, so unwinding it faster is the
 *    correct response. It reverts the instant the signs agree again.
 */
namespace DragonScreen
{
    /// <summary>
    /// kOS's PID loop. Ported from `KosPIDLoop.cs`; dt is a parameter here rather than read from
    /// `TimeWarp.fixedDeltaTime`, which is the only change and is what keeps it testable.
    /// </summary>
    public class KosPid
    {
        public double Kp, Kd;

        private double ki, loopKi;
        public double Ki
        {
            get { return ki; }
            set { ki = value; loopKi = value; }
        }

        /// <summary>Doubles Ki while unwinding an overshoot. See the header.</summary>
        public bool ExtraUnwind;

        public double Input, Setpoint, Error, Output, ErrorSum;
        public double PTerm, ITerm, DTerm, ChangeRate;
        public bool UnWinding;

        public KosPid() { Kp = 1.0; }

        public KosPid(double kp, double kiIn, double kd, bool extraUnwind)
        {
            Kp = kp; Ki = kiIn; Kd = kd; ExtraUnwind = extraUnwind;
        }

        public void ResetI() { ErrorSum = 0.0; ITerm = 0.0; }

        public double Update(double input, double setpoint, double maxOutput, double dt)
        {
            return Update(input, setpoint, -maxOutput, maxOutput, dt);
        }

        public double Update(double input, double setpoint, double minOutput, double maxOutput,
                             double dt)
        {
            Setpoint = setpoint;
            if (dt <= 0.0) return Output;

            double error = Setpoint - input;
            double pTerm = error * Kp;
            double iTerm = 0.0;
            double dTerm = 0.0;

            if (loopKi != 0.0)
            {
                // ---- EXTRA UNWIND ----
                // Error and accumulated sum disagreeing in sign means we have overshot and the
                // integral is now pushing the wrong way. Double Ki until they agree again.
                if (ExtraUnwind)
                {
                    if (System.Math.Sign(error) != System.Math.Sign(ErrorSum))
                    {
                        if (!UnWinding) { loopKi *= 2.0; UnWinding = true; }
                    }
                    else if (UnWinding) { loopKi = ki; UnWinding = false; }
                }
                iTerm = ITerm + error * dt * loopKi;
            }

            ChangeRate = (input - Input) / dt;
            if (Kd != 0.0) dTerm = -ChangeRate * Kd;

            Output = pTerm + iTerm + dTerm;

            // ---- BACK-CALCULATION, NOT A CLAMP ----
            // On saturation the I-term is rebuilt as exactly the amount that holds the output at the
            // limit, so it carries no authority it cannot deliver and has nothing to dump later.
            if (Output > maxOutput)
            {
                Output = maxOutput;
                if (loopKi != 0.0)
                    iTerm = Output - System.Math.Min(pTerm + dTerm, maxOutput);
            }
            if (Output < minOutput)
            {
                Output = minOutput;
                if (loopKi != 0.0)
                    iTerm = Output - System.Math.Max(pTerm + dTerm, minOutput);
            }

            Input = input;
            Error = error;
            PTerm = pTerm;
            ITerm = iTerm;
            DTerm = dTerm;
            ErrorSum = (loopKi != 0.0) ? iTerm / loopKi : 0.0;
            return Output;
        }
    }

    /// <summary>
    /// The torque stage. `TorquePI.cs`, whole: gains ARE the moment of inertia, refreshed every
    /// call, which is what lets one controller fly every configuration of the vehicle.
    /// </summary>
    public class TorquePi
    {
        private readonly KosPid loop = new KosPid();

        public double Update(double input, double setpoint, double momentOfInertia,
                             double maxOutput, double dt)
        {
            loop.Ki = 4.0 * momentOfInertia;
            loop.Kp = 4.0 * momentOfInertia;
            return loop.Update(input, setpoint, maxOutput, dt);
        }

        public void ResetI() { loop.ResetI(); }
    }

    /// <summary>
    /// The cascade, minus the vector maths the glue has to do.
    ///
    /// Per axis: attitude error (phi) -> a target ANGULAR RATE, limited by how fast we could still
    /// stop in `MaxStoppingTime` -> a target TORQUE -> an actuation fraction of available torque.
    ///
    /// The rate limit is the interesting one and it is the same idea as the hoverslam: never ask for
    /// a rate you could not arrest with the torque you have.
    /// </summary>
    public static class AttitudeCascade
    {
        /// <summary>Seconds allowed to stop the rotation. F9I tunes this per phase.</summary>
        public const double DefaultMaxStoppingTime = 2.0;

        /// <summary>
        /// Beyond this total attitude error, roll is NOT controlled - `RollControlRange`, 5 degrees.
        /// Rolling while still slewing wastes authority and couples the axes; get the nose there
        /// first, then worry about which way up.
        /// </summary>
        public const double RollControlRangeDeg = 5.0;

        /// <summary>Fastest rate we could still stop in MaxStoppingTime with the torque available.</summary>
        public static double MaxOmega(double controlTorque, double momentOfInertia,
                                      double maxStoppingTime)
        {
            if (momentOfInertia <= 0.0) return 0.0;
            return controlTorque * maxStoppingTime / momentOfInertia;
        }

        /// <summary>
        /// Torque demand to an actuation fraction, with kOS's rate limiter: each axis may only move
        /// to twice its previous magnitude (floored at 0.005) in one tick. That is what stops a
        /// step change in the target snapping the controls hard over.
        /// </summary>
        public static double Actuation(double targetTorque, double controlTorque, double previous)
        {
            if (controlTorque == 0.0) return 0.0;
            double a = targetTorque / controlTorque;
            if (double.IsNaN(a) || System.Math.Abs(a) < 1e-16) return 0.0;

            double prev = (previous < 0.0) ? -previous : previous;
            double clamp = ((prev > 0.005) ? prev : 0.005) * 2.0;
            if (a > clamp) a = clamp;
            if (a < -clamp) a = -clamp;
            return a;
        }
    }
}
