/*
 * DragonScreen - BurnExec
 *
 * PURE. Executing a burn: how hard to push, where to point, and when to stop.
 * `COMMON/GNC.ks:917` ExecNode - tranche 3 of the port map.
 *
 * ---- THIS IS THE MOST CAREFUL THROTTLE LAW IN THE WHOLE TREE ----
 * Three ideas, each of which fixes a specific way a burn goes wrong:
 *
 *      throttle = min(dvRemaining / maxAccel, 1) * angleMult
 *
 * 1. **Throttle by SECONDS OF BURN REMAINING, not by dv.** `dv/accel` is how long is left; clamped
 *    to 1 that is full throttle until the final second and then a proportional taper. F9I's own
 *    comment: "that is what lands the residual on 0.085 m/s instead of overshooting past it."
 *    A dv-proportional law cannot do that, because the same dv means different burn times on
 *    different vehicles.
 *
 * 2. **angleMult goes NEGATIVE past 5 degrees.** `(5 - angle)/5` is 1 aligned, 0 at five degrees off,
 *    and negative beyond - which clamps the throttle to zero. "Thrusting 10 degrees off is worse
 *    than not thrusting", because the off-axis component is pure waste AND it pushes the orbit
 *    somewhere nobody asked for.
 *
 * 3. **The floor-lift.** Demands below 0.5 are DOUBLED, so a small request stays above the engine's
 *    minimum useful throttle instead of dribbling.
 *
 * ---- AND TWO THINGS ABOUT THE BURN THAT ARE NOT THE THROTTLE ----
 * **The burn is CENTRED on the node, not started at it.** Half the burn duration is the lead. That is
 * what makes a long finite burn approximate the instantaneous impulse the node solver assumed.
 *
 * **Steer at the RESIDUAL dv, never the original.** kOS locks `nv` to `nd:deltav:normalized`, which
 * re-evaluates each read - so as the burn proceeds the vessel keeps pointing at what is LEFT. That is
 * what makes it self-correcting when the engine under-performs or the attitude drifts.
 *
 * ---- ⚠ AND THE ONE THAT REVERSES A BURN IF YOU GET IT WRONG ----
 * A Dragon burning on RCS points at MINUS the burn vector, because the Dracos thrust out of the back
 * of the trunk and the vessel's forward runs the other way. `GNC.ks:1051`. Get it wrong and the burn
 * is applied backwards - which on a de-orbit is not a small error.
 */
namespace DragonScreen
{
    /// <summary>Which way the vehicle must point to push along a burn vector.</summary>
    public enum ThrustSense : byte
    {
        /// <summary>Nose along the burn vector. Engines out of the back, the normal case.</summary>
        Forward = 0,
        /// <summary>Nose at MINUS the burn vector - a capsule on Dracos. See the header.</summary>
        Reversed
    }

    public struct BurnState
    {
        public bool Valid;
        /// <summary>Magnitude of the dv still owed, m/s. Shrinks as the burn proceeds.</summary>
        public double RemainingDvMps;
        /// <summary>Full-throttle acceleration at the CURRENT mass, m/s^2.</summary>
        public double MaxAccel;
        /// <summary>Angle between where we point and where the burn needs to go, degrees.</summary>
        public double PointingErrorDeg;
        /// <summary>Seconds until the node. Negative once past it.</summary>
        public double NodeEtaS;
        /// <summary>Burning on RCS rather than the main engine.</summary>
        public bool OnRcs;
        /// <summary>A capsule whose thrusters face the other way. See ThrustSense.</summary>
        public bool CapsuleThrusters;
    }

    public static class BurnExec
    {
        /// <summary>Beyond this pointing error the throttle is cut entirely. GNC.ks uses 5.</summary>
        public const double PointingLimitDeg = 5.0;

        /// <summary>Demands below this are doubled to clear the engine's minimum useful throttle.</summary>
        public const double FloorLiftBelow = 0.5;

        /// <summary>Burn duration is capped so a near-zero-thrust vehicle cannot plan a lead
        /// longer than its own orbit. GNC.ks:954.</summary>
        public const double MaxBurnDurationS = 1800.0;

        /// <summary>Residual below which the burn is finished, m/s.</summary>
        public const double ResidualToleranceMps = 0.1;

        /// <summary>
        /// How long the burn will take. Capped, and floored on acceleration so a stage whose engine
        /// has not lit yet gives a pessimistically long plan rather than an infinite one.
        /// </summary>
        public static double BurnDuration(double dvMps, double maxAccel)
        {
            double a = (maxAccel > 0.001) ? maxAccel : 0.001;
            double t = dvMps / a;
            return (t > MaxBurnDurationS) ? MaxBurnDurationS : t;
        }

        /// <summary>
        /// When to light the engine: half the burn BEFORE the node, so the burn straddles it.
        /// Returns seconds from now; negative means we are already late.
        /// </summary>
        public static double IgnitionEtaS(double nodeEtaS, double burnDurationS)
        {
            return nodeEtaS - burnDurationS * 0.5;
        }

        /// <summary>
        /// The alignment multiplier. 1 pointing straight at the burn, 0 at the limit, and CLAMPED to
        /// zero beyond - never negative out of this function, because a negative throttle is not a
        /// thing and letting the sign escape invites someone downstream to multiply by it.
        /// </summary>
        public static double AlignmentMultiplier(double pointingErrorDeg)
        {
            double m = (PointingLimitDeg - pointingErrorDeg) / PointingLimitDeg;
            if (m < 0.0) m = 0.0;
            if (m > 1.0) m = 1.0;
            return m;
        }

        /// <summary>
        /// The throttle. See the header for why each of the three parts is there.
        /// </summary>
        public static double Throttle(BurnState s)
        {
            if (!s.Valid) return 0.0;
            if (s.RemainingDvMps <= ResidualToleranceMps) return 0.0;

            double accel = (s.MaxAccel > 0.0001) ? s.MaxAccel : 0.0001;

            // Seconds of burn still owed, clamped to one. Full throttle until the last second.
            double secondsLeft = s.RemainingDvMps / accel;
            if (secondsLeft > 1.0) secondsLeft = 1.0;

            // ---- ⚠ THE FLOOR-LIFT GOES BEFORE THE ALIGNMENT TERM, NOT AFTER. DELIBERATE CHANGE. ----
            // GNC.ks:1085 computes `reqThrot = min(dv/accel, 1) * angleMult` and THEN doubles it if
            // it is under 0.5. Because angleMult can push a full-throttle demand under 0.5 on its
            // own, the lift fires on MISPOINTING as well as on a small demand - and the result is
            // not monotonic in pointing error:
            //
            //      1 deg off -> 1.0 * 0.8 = 0.80                     (not lifted)
            //      2 deg off -> 1.0 * 0.6 = 0.60                     (not lifted)
            //      3 deg off -> 1.0 * 0.4 = 0.40 -> lifted -> 0.80   (MORE than at 2 deg)
            //
            // So a vessel drifting off-axis gets a throttle KICK exactly as its pointing degrades,
            // which is the opposite of what the alignment term exists to do. Caught by the headless
            // monotonicity check, not in flight - it would show up as a burn that wanders.
            //
            // The comment in GNC.ks says the lift is for "small demands ... rather than dribbling",
            // i.e. the END of a burn, so lifting the TIME-BASED demand and then applying alignment
            // keeps that intent and removes the artifact. Aligned burns are unchanged.
            double demand = secondsLeft;
            if (demand <= FloorLiftBelow) demand *= 2.0;

            double req = demand * AlignmentMultiplier(s.PointingErrorDeg);

            if (req < 0.0) req = 0.0;
            if (req > 1.0) req = 1.0;
            return req;
        }

        /// <summary>
        /// Which way to point. The capsule-on-RCS case is the one that reverses a burn if missed.
        /// </summary>
        public static ThrustSense Sense(BurnState s)
        {
            return (s.CapsuleThrusters && s.OnRcs) ? ThrustSense.Reversed : ThrustSense.Forward;
        }

        /// <summary>
        /// Is the burn finished?
        ///
        /// Two ways, and BOTH are needed. The residual falling inside tolerance is the clean one.
        /// The other is the dv REVERSING - `vdot(remaining, original) < 0` in kOS - which means we
        /// have burned past the target and any further thrust undoes it. Without the reversal test a
        /// burn that overshoots between ticks turns around and chases itself.
        /// </summary>
        public static bool Complete(BurnState s, bool dvReversed)
        {
            if (!s.Valid) return true;
            if (s.RemainingDvMps <= ResidualToleranceMps) return true;
            return dvReversed;
        }

        /// <summary>
        /// Should we still be coasting rather than orienting and warping?
        ///
        /// ⚠ `>`, NOT `-`. GNC.ks had `if (nd:eta - ((burnDuration / 2) + 120))` - an arithmetic
        /// expression where a boolean was wanted. kOS reads any non-zero scalar as true, so the
        /// branch was taken unless the difference was EXACTLY zero, i.e. always. Fixed there
        /// 2026-08-04; written as a real comparison here so it cannot come back.
        /// </summary>
        public static bool HasCoastTime(double nodeEtaS, double burnDurationS)
        {
            return nodeEtaS > (burnDurationS * 0.5) + 120.0;
        }

        /// <summary>
        /// Settled time, not iterations. GNC.ks counts SECONDS of continuous alignment inside one
        /// degree, so a vessel that keeps drifting in and out never satisfies it - which an
        /// iteration count would.
        /// </summary>
        public static double UpdateSettled(double settledS, double pointingErrorDeg, double dt)
        {
            if (pointingErrorDeg <= 1.0) return settledS + dt;
            return 0.0;
        }
    }
}
