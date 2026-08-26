/*
 * DragonScreen - ThrustDeliveryMonitor
 *
 * PURE. The first concrete Layer-3 monitor (docs/LAYER3_AUTONOMY_PLAN.md), built on HealthMonitor. It
 * is the most universal one: EVERY powered phase - the ascent, the booster burns, and every orbital
 * burn through NodeExecutor - asks the same question, "is the engine actually delivering the Δv the
 * guidance is asking for?", and until now each answered it with its own ad-hoc guard. This unifies them.
 *
 * ---- IT REPLACES THREE SCATTERED SEEDS WITH ONE RESIDUAL ----
 * The residual is the along-axis DELIVERY FRACTION: how much of the acceleration the guidance intends
 * is actually being produced ALONG the intended direction. Shaped so positive magnitude = worse, it
 * catches all three existing failure signatures at once:
 *
 *   dead / starved engine   delivered accel ~ 0    -> shortfall ~ 1.0   (the lit-but-no-thrust crash,
 *                                                                          flight_0825_173123)
 *   off-axis "burning"       along-axis accel ~ 0   -> shortfall ~ 1.0   (the no-burn rendezvous, where
 *                                                                          1414 "Burning" rows delivered
 *                                                                          nothing - flight_0825_184857)
 *   inverted / wrong-way     delivered accel  < 0   -> residual  > 1.0   (the reversed Draco burn that
 *                                                                          drove periapsis to -18 km -
 *                                                                          flight_0825_163535)
 *
 * A healthy burn delivers ~100% along the aim -> residual ~ 0 -> Nominal. A coast (the guidance is not
 * commanding thrust) yields residual 0, so an engine that is OFF on purpose never trips it - the
 * detector only judges delivery when delivery was asked for.
 *
 * ---- WHY IT IS SAFE WHERE A NAIVE THRESHOLD IS NOT ----
 * A real burn spends its first second or two aligning and spooling, delivering little - so an
 * instantaneous "delivered < expected" would fire on every good burn. HealthMonitor's confirmation
 * time is what makes this usable: a shortfall must PERSIST for ConfirmS before it is a fault, which is
 * exactly the spool/align window the seeds each worked around by hand (NodeExecutor only counts stalled
 * time while on-axis; the landing burn budgets a dead-time lead). One confirmation clock, in one place.
 *
 * Isolation (which FaultKind) comes straight from the delivery sign: a NEGATIVE along-axis delivery is
 * ThrustReversed (flip the sign / re-plan); a positive-but-short delivery is ThrustShortfall (relight /
 * re-plan). FaultResponse then chooses the recovery per regime.
 *
 * This monitor detects; it does not fly anything. The glue samples ExpectedAccel and DeliveredAccel
 * each tick (Δv-rate the guidance intends vs the finalThrust/velocity delivery it already measures for
 * the burn accounting), Steps this, records the fd_ block, and hands the verdict to FaultResponse.
 */
namespace DragonScreen
{
    /// <summary>One tick of powered-flight delivery, as the glue measures it.</summary>
    public struct ThrustSample
    {
        /// <summary>The acceleration the guidance INTENDS this tick, m/s² - commanded throttle × the
        /// thrust available at that throttle, over mass. Zero when the guidance is not asking for thrust.</summary>
        public double ExpectedAccel;
        /// <summary>The acceleration actually being produced ALONG the intended axis, m/s² (signed):
        /// positive is toward the aim, negative is the wrong way. Measured, not commanded - the same
        /// finalThrust / velocity-delta the burn accounting already uses, projected on the aim.</summary>
        public double DeliveredAccel;
        /// <summary>Is the guidance actually commanding thrust on-axis this tick? False during a coast,
        /// a hold, or while still slewing to the aim - times when a shortfall is expected and not a fault.</summary>
        public bool Commanding;
    }

    public static class ThrustDeliveryMonitor
    {
        // ---- THE BANDS. Fractions of the expected delivery. ----
        /// <summary>Delivering below (1 - this) of what is expected is DEGRADED. 0.5 = under half.</summary>
        public const double DegradeShortfall = 0.5;
        /// <summary>Delivering below (1 - this) of expected is FAILED. 0.9 = under a tenth (a dead /
        /// off-axis / reversed engine). A reversed delivery scores &gt;1, well past this.</summary>
        public const double FailShortfall = 0.9;
        /// <summary>Recovered once delivery climbs back above (1 - this) of expected. 0.3 = over 70%.
        /// Below DegradeShortfall - that gap is the hysteresis.</summary>
        public const double ClearShortfall = 0.3;

        /// <summary>
        /// Seconds a shortfall must persist before it is a fault - the spool + align window a real burn
        /// legitimately spends delivering little. ~1.5 s covers the RO Merlin ramp and the Draco slew;
        /// the reversed Draco ran 8m39s, so catching any of this within ~1.5 s is a vast improvement over
        /// every seed it replaces, while never firing on a clean spool. [Tunable].
        /// </summary>
        public const double ConfirmS = 1.5;
        /// <summary>Seconds of healthy delivery before the fault clears - short, so a genuinely recovered
        /// burn is trusted quickly, but non-zero so one good tick does not clear a real failure.</summary>
        public const double ClearS = 0.5;

        /// <summary>The monitor's config, as HealthMonitor consumes it.</summary>
        public static MonitorConfig DefaultConfig()
        {
            return HealthMonitor.Config(DegradeShortfall, FailShortfall, ClearShortfall, ConfirmS, ClearS);
        }

        /// <summary>
        /// The along-axis delivery residual, shaped so positive magnitude means "worse":
        ///   coast / not commanding        -> 0        (no delivery expected, so no fault)
        ///   delivering the wrong way      -> 1 - frac (frac negative, so &gt;1: a reversed fault)
        ///   delivering short              -> 1 - frac (0 = full, 1 = nothing)
        ///   delivering at or above target -> 0        (over-delivery is never a fault)
        /// where frac = DeliveredAccel / ExpectedAccel.
        /// </summary>
        public static double Residual(ThrustSample x)
        {
            if (!x.Commanding || x.ExpectedAccel <= 0.0) return 0.0;
            double frac = x.DeliveredAccel / x.ExpectedAccel;
            double residual = 1.0 - frac;              // reversed frac<0 -> >1; dead frac=0 -> 1; full ->0
            return residual < 0.0 ? 0.0 : residual;    // over-delivering is fine
        }

        /// <summary>
        /// Which fault this is, once the verdict says there IS one - the ISOLATION step. Nominal, or a
        /// tick that was not commanding thrust, is no fault. A negative along-axis delivery is a reversal;
        /// anything else non-nominal is a shortfall.
        /// </summary>
        public static FaultKind Kind(ThrustSample x, HealthVerdict verdict)
        {
            if (verdict == HealthVerdict.Nominal || !x.Commanding || x.ExpectedAccel <= 0.0)
                return FaultKind.None;
            return x.DeliveredAccel < 0.0 ? FaultKind.ThrustReversed : FaultKind.ThrustShortfall;
        }

        /// <summary>Advance the monitor one tick: compute the residual from the sample and step
        /// HealthMonitor with this monitor's config. The caller owns the returned state and resets it
        /// (HealthMonitor.Fresh) at the start of each new burn / powered phase.</summary>
        public static MonitorState Step(MonitorState prev, ThrustSample x, double dt)
        {
            return HealthMonitor.Step(prev, DefaultConfig(), Residual(x), dt);
        }
    }
}
