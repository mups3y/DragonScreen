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
