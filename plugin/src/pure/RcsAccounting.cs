// DragonScreen — RcsAccounting  (pure: PHYSICS-RATE RCS actuation accounting; sampled + reset each recorder interval)
// ============================================================================================
// Instrumentation ONLY — measures nothing about guidance, changes nothing about control. It answers, at PHYSICS
// rate (so the 0.06 s PWPF min-on/off dwell is NOT aliased by the slower recorder), the questions the reviewer
// posed for the terminal-fuel problem:
//   • how much TIME the vehicle spends with ATTITUDE-only / TRANSLATION-only / SIMULTANEOUS(both) / NEITHER
//     commands actually applied to FlightCtrlState;
//   • the delivered RCS IMPULSE (N·s) in each of those categories — an un-aliased propellant proxy, so the
//     per-category propellant can be attributed as (category impulse / total impulse) × (MMH+NTO consumed);
//   • REQUESTED vs APPLIED command-seconds per group (Σ|cmd|·dt), to see how much the PWPF stage actually
//     removes vs merely re-shapes.
//
// ⚠ NAMING DISCIPLINE (do not conflate):
//     REQUESTED command  ≠  APPLIED FlightCtrlState command  ≠  DELIVERED thruster force.
// This struct measures the first two per-group and the aggregate delivered force (Σ thrustForces·power from the
// live ModuleRCS). It does NOT split delivered force per-thruster or per-cause — KSP's own RCS solver decides
// which thrusters fire from the combined command, and that split is not exposed. So the SIMULTANEOUS ("both")
// bucket is the honest home for any command overlap; do not manufacture a finer per-thruster attribution.
//
// VALIDATED LIMITATIONS (do not overclaim beyond these):
//  • REQUESTED ≠ APPLIED FlightCtrlState command ≠ KSP-selected thrusters ≠ vehicle net force/torque. This
//    measures the first two per-group + the aggregate delivered force; not the last two.
//  • TICK LAG: the caller reads Actuator.RcsThrustN inside OnFlyByWire, BEFORE KSP applies this tick's ctrlState,
//    so the force reflects the PREVIOUS physics tick while the category is this tick's applied command → a
//    ≤1-tick (0.02 s) boundary smear at each category transition. Negligible for the seconds-long mutually-
//    exclusive windows the focused flight uses; do NOT trust it for rapidly-flipping sub-0.1 s categorisation.
//  • THRESHOLD: |cmd|>0.5 is correct because the caller gates accumulation to the RCS-pulsing phases where the
//    applied command is the PWPF output {−1,0,+1}; it is not used on the continuous (gimbal-ascent) path.
//  • RESET: the caller resets on recorder stream-open (new flight / vessel / abort / mission-reset) and after
//    every sample. Residual: a mid-flight ACTIVE-vessel switch without a re-open would carry one interval of the
//    prior vessel's accumulation — rare in the terminal; treat a vessel-switch interval with caution.
//  • PROPELLANT: per-category propellant = category_imp/total_imp × (MMH+NTO consumed) is valid because all Dracos
//    share one Isp and the single MMH/NTO pair (delivered impulse ∝ propellant mass); NOT valid for mixed-Isp
//    thrusters firing in different categories (not the case for this vehicle).
// Pure + headless-tested (plugin/test/RcsAccountingTest.cs).
// ============================================================================================
namespace DragonScreen
{
    public struct RcsAccounting
    {
        public double IntervalS;                                   // total accumulated time this interval
        public double AttOnlyS, TransOnlyS, BothS, NoneS;          // category TIME (s)
        public double AttOnlyImpNs, TransOnlyImpNs, BothImpNs;     // delivered RCS IMPULSE (N·s) by category
        public double ReqAttCs, AppAttCs, ReqTransCs, AppTransCs;  // requested/applied command-seconds (Σ|cmd|·dt)

        // One physics tick. attApplied/transApplied = a post-PWPF FlightCtrlState command is active on that group.
        // rcsForceN = the actual delivered RCS force this tick (Σ thrustForces·thrusterPower). req*/app* = Σ|cmd|.
        public void Add(double dt, bool attApplied, bool transApplied, double rcsForceN,
                        double reqAtt, double appAtt, double reqTrans, double appTrans)
        {
            if (!(dt > 0.0)) return;
            IntervalS += dt;
            double imp = (rcsForceN > 0.0 ? rcsForceN : 0.0) * dt;
            ReqAttCs   += (reqAtt   > 0.0 ? reqAtt   : 0.0) * dt;
            AppAttCs   += (appAtt   > 0.0 ? appAtt   : 0.0) * dt;
            ReqTransCs += (reqTrans > 0.0 ? reqTrans : 0.0) * dt;
            AppTransCs += (appTrans > 0.0 ? appTrans : 0.0) * dt;
            if (attApplied && transApplied) { BothS += dt; BothImpNs += imp; }
            else if (attApplied)            { AttOnlyS += dt; AttOnlyImpNs += imp; }
            else if (transApplied)          { TransOnlyS += dt; TransOnlyImpNs += imp; }
            else                            { NoneS += dt; }
        }

        public void Reset() { this = new RcsAccounting(); }
    }
}
