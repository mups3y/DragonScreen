// DragonScreen — Control  (autopilot rebuild L2: the attitude / throttle / translation laws)
// ============================================================================================
// Built from the research (TRUE_AUTOPILOT_ARCHITECTURE §4), NOT from the deleted tree. Pure, per-body-
// axis so it maps straight onto FlightCtrlState (s.pitch/yaw/roll, s.mainThrottle, s.X/Y/Z); the glue
// computes each axis error from the target vs current attitude and the live authority (L1 Authority),
// and actuates by capability from the craft dump (gimbal / RCS / grid fins / engine-mode switch).
//
// THE ONE IDEA (research §4.1): never command a rate you cannot arrest. The commanded rate is bounded
// by the braking curve ω_max = √(2·α·θ) built from the vehicle's OWN angular authority, so the SAME law
// flies the full stack, a spent booster, or a bare capsule with NO per-phase gain tuning. RO strips
// reaction wheels, so torque is gimbal + RCS + grid fins only; where authority is momentarily zero the
// law commands nothing and the glue turns RCS on.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class ControlLaw
    {
        // ---- attitude constants: standard control values, not mission-tuned ----
        public const double RateMarginK   = 0.9;     // command 90% of the arrestable rate — leave brake room
        public const double LinearSettleS = 1.0;     // small-error region: ω = θ/τ (avoids braking-curve chatter at θ→0)
        public const double RateLoopTauS  = 0.5;     // rate loop: torque = I·Δω/τ (saturates for large Δω regardless)
        public const double DeadbandRad   = 0.0017;  // ~0.1° — no limit cycle about the target
        public const double MaxSlewPerTick = 0.1;    // actuation may move at most this per tick (no slamming)
        public const double G0 = 9.80665;

        // ------------------------------------------------------------------ ATTITUDE (per axis)

        // Commanded body rate (rad/s) for an axis pointing error, bounded so it can always be arrested.
        // min( linear terminal region θ/τ , braking curve k·√(2αθ) ): proportional near zero, arrestable
        // for large errors. angAccel α = torque/inertia (from L1 Authority). maxRateRad ≤ 0 = no extra cap.
        public static double RateCommand(double errorRad, double angAccel, double maxRateRad)
        {
            if (errorRad > -DeadbandRad && errorRad < DeadbandRad) return 0.0;
            double e = errorRad < 0.0 ? -errorRad : errorRad;
            double linear = e / LinearSettleS;
            double brake = RateMarginK * Authority.ArrestableRate(angAccel, e);   // 0 if no authority
            double w = brake > 0.0 && brake < linear ? brake : linear;
            if (maxRateRad > 0.0 && w > maxRateRad) w = maxRateRad;
            return errorRad < 0.0 ? -w : w;
        }

        // Actuation fraction [-1,1] for a rate error. Torque-based so it scales with the vehicle's own
        // authority (I·Δω/τ), clamped, and slew-limited against the previous command. No authority → 0.
        public static double Actuate(double rateErrorRad, double inertiaKgM2, double torqueAvailNm,
                                     double previous)
        {
            if (torqueAvailNm <= 0.0 || inertiaKgM2 <= 0.0) return 0.0;
            double torque = inertiaKgM2 * rateErrorRad / RateLoopTauS;
            double a = torque / torqueAvailNm;
            if (a > 1.0) a = 1.0; else if (a < -1.0) a = -1.0;
            double d = a - previous;
            if (d > MaxSlewPerTick) a = previous + MaxSlewPerTick;
            else if (d < -MaxSlewPerTick) a = previous - MaxSlewPerTick;
            return a;
        }

        // One axis end-to-end: error + measured rate + authority → actuation fraction.
        public static double AxisCommand(double errorRad, double rateRad, double inertiaKgM2,
                                         double torqueAvailNm, double maxRateRad, double previous)
        {
            double angAccel = Authority.AngularAccel(torqueAvailNm, inertiaKgM2);
            double wCmd = RateCommand(errorRad, angAccel, maxRateRad);
            return Actuate(wCmd - rateRad, inertiaKgM2, torqueAvailNm, previous);
        }

        // ------------------------------------------------------------------ THROTTLE effector

        // The guidance sets the base throttle; two limiters overlay it and the MORE RESTRICTIVE wins
        // (research §4.2). Returns a throttle in [0,1] (the glue clamps to the engine's min throttle).
        //  • CREW g-LIMIT (exact physics): axial accel ≤ gLimit ⇒ ENGINE throttle ≤ gLimit·g0·m / F_full.
        //    As a stage lightens near MECO/SECO this caps the g the crew feels (~3.2 g S1, ~4 g S2 —
        //    LAUNCH_AND_ASCENT_RESEARCH §5.2; DM-1 peaked 3.26/3.57 g so it rarely bites, correctly).
        //    ⛔ minThrottle FLOOR: RealFuels maps the vessel (MAIN) throttle onto [minThrottle, 1] for the
        //    engine — engineThrottle = minThrottle + mainThrottle·(1−minThrottle). The cap above is the
        //    ENGINE throttle; returning it AS a main throttle makes the engine run HOTTER by that floor and
        //    the felt g overshoots by exactly (minThr + tgEng·(1−minThr))/tgEng. MEASURED: S2 held 4.53 g at
        //    setpoint 4.1 (ratio 1.107) across flights 134620/144114/155116, MVac minThrottle 0.3854. So map
        //    the engine-throttle target back to the main throttle the guidance commands (Campaign 5).
        //  • MAX-Q BUCKET: hold dynamic pressure under a ceiling — as q rises through [qSoft, qLimit]
        //    the throttle ramps down toward bucketFloor, then back up as q falls (the real Merlin
        //    throttle-down through max-Q). q is MEASURED, so the bucket is autonomous, not scheduled.
        public static double ThrottleLimit(double baseThrottle,
                                           double qPa, double qSoftPa, double qLimitPa, double bucketFloor,
                                           double gLimitG, double massKg, double fullThrustN, double minThrottle)
        {
            double t = baseThrottle;
            if (t < 0.0) t = 0.0; else if (t > 1.0) t = 1.0;

            if (gLimitG > 0.0 && fullThrustN > 0.0 && massKg > 0.0)
            {
                double tgEng = gLimitG * G0 * massKg / fullThrustN;      // the ENGINE throttle that holds g
                // Invert the RealFuels floor to the MAIN throttle that DELIVERS tgEng. Below the floor the
                // engine cannot go lower while lit, so a negative result clamps to 0 (best effort short of
                // shutdown). minThrottle ≤ 0 (unknown / stock) leaves the old behaviour.
                double tg = (minThrottle > 0.0 && minThrottle < 1.0)
                    ? (tgEng - minThrottle) / (1.0 - minThrottle)
                    : tgEng;
                if (tg < t) t = tg;
            }

            if (qPa > qSoftPa && qLimitPa > qSoftPa && bucketFloor >= 0.0 && bucketFloor <= 1.0)
            {
                double frac = (qPa - qSoftPa) / (qLimitPa - qSoftPa);
                if (frac > 1.0) frac = 1.0;
                double tb = 1.0 - (1.0 - bucketFloor) * frac;
                if (tb < t) t = tb;
            }

            if (t < 0.0) t = 0.0; else if (t > 1.0) t = 1.0;
            return t;
        }

        // ------------------------------------------------------------------ RCS TRANSLATION (Draco)

        // A capsule with no main engine burns on RCS translation (s.X/Y/Z). Map a desired translational
        // acceleration on one axis to the [-1,1] demand, scaled by the RCS acceleration available on it.
        public static double TranslateAxis(double desiredAccelMps2, double rcsAccelAvailMps2)
        {
            if (rcsAccelAvailMps2 <= 1e-9) return 0.0;
            double a = desiredAccelMps2 / rcsAccelAvailMps2;
            if (a > 1.0) a = 1.0; else if (a < -1.0) a = -1.0;
            return a;
        }
    }
}
