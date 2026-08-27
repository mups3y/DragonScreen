// DragonScreen — StageStats  (autopilot rebuild B1: per-stage Δv / TWR / burn-time + the MECO recovery reserve)
// ============================================================================================
// The propulsive budget maths, ported from MechJeb's PROVEN FuelFlowSimulation (MechJebLib,
// FuelFlowSimulation.cs FinishSegment): for a burn that takes a stage from start mass m0 to end mass
// m1 at thrust F and exhaust velocity ve = Isp·g0,
//     ve       = F·dt / (m0 − m1)          (effective exhaust velocity from the actual burn)
//     ΔV       = ve · ln(m0 / m1)          (Tsiolkovsky)
//     ṁ        = F / ve ,  burn = mprop/ṁ  (= mprop·ve/F)
//     TWR      = F / (m · g)               (at a reference gravity)
// This is the exact form MechJeb uses — not a self-invented model. Pure (no KSP): the glue supplies the
// live per-stage mass / propellant / thrust / Isp from the vessel; this turns them into the budget.
//
// PRIMARY USE (B1): the booster MECO recovery reserve. A recoverable booster must MECO while it still holds
// enough propellant to fly boostback + entry burn + landing burn. `RemainingDeltaV` says how much Δv the
// booster would have if it cut off now and burned the rest; `PropMassForDeltaV` inverts the rocket equation
// to the propellant that must REMAIN at MECO for a required recovery Δv. The ascent reads these to decide
// when to stage for a recoverable booster (Movement I-B, Stage 1). Also feeds the Tier-4 validator's budgets.
// ============================================================================================
using System;

namespace DragonScreen
{
    // One burn's / stage's propulsive stats — the subset of MechJeb's FuelStats we use.
    public struct StageStat
    {
        public double StartMassKg;   // m0
        public double EndMassKg;     // m1 = m0 − propellant burned
        public double PropMassKg;    // propellant consumed over the burn
        public double ThrustN;
        public double IspS;
        public double VeMps;         // Isp·g0
        public double DeltaVMps;     // ve·ln(m0/m1)
        public double BurnTimeS;     // mprop / (F/ve)
        public double StartTwr;      // at the supplied reference gravity
        public double EndTwr;
    }

    public static class StageStats
    {
        public const double G0 = 9.80665;   // standard gravity (Isp → ve)

        // One burn's stats. refGravityMps2 is the gravity to size TWR against (e.g. surface g ≈ 9.81 for
        // liftoff TWR, or the local g at altitude). Degenerate inputs return zeroed fields (MechJeb's own
        // `m0 > m1 ? … : 0` convention — a guarded zero, never a NaN).
        public static StageStat Compute(double startMassKg, double propMassKg, double thrustN,
                                        double ispS, double refGravityMps2)
        {
            StageStat s = new StageStat();
            s.StartMassKg = startMassKg;
            s.PropMassKg = propMassKg;
            s.ThrustN = thrustN;
            s.IspS = ispS;
            s.VeMps = ispS * G0;
            s.EndMassKg = startMassKg - propMassKg;

            double m0 = startMassKg, m1 = s.EndMassKg, ve = s.VeMps;
            if (m0 > m1 && m1 > 0.0 && ve > 0.0)
            {
                s.DeltaVMps = ve * Math.Log(m0 / m1);
                double mdot = thrustN / ve;                 // F = ṁ·ve
                s.BurnTimeS = mdot > 0.0 ? propMassKg / mdot : 0.0;
            }
            if (refGravityMps2 > 0.0)
            {
                if (m0 > 0.0) s.StartTwr = thrustN / (m0 * refGravityMps2);
                if (m1 > 0.0) s.EndTwr = thrustN / (m1 * refGravityMps2);
            }
            return s;
        }

        // Total Δv over an ordered set of stages (each already Computed).
        public static double TotalDeltaV(StageStat[] stages)
        {
            if (stages == null) return 0.0;
            double dv = 0.0;
            for (int i = 0; i < stages.Length; i++) dv += stages[i].DeltaVMps;
            return dv;
        }

        // Δv still available if the vehicle cut off NOW and burned everything down to dryMass (ve·ln(m/mdry)).
        // Used at any instant of the S1 burn: "if I MECO now, how much recovery Δv does the booster have?"
        public static double RemainingDeltaV(double currentMassKg, double dryMassKg, double ispS)
        {
            double ve = ispS * G0;
            if (currentMassKg > dryMassKg && dryMassKg > 0.0 && ve > 0.0)
                return ve * Math.Log(currentMassKg / dryMassKg);
            return 0.0;
        }

        // Inverse rocket equation: the PROPELLANT mass that must remain above dryMass to achieve deltaV.
        //   deltaV = ve·ln((mdry+mprop)/mdry)  ⇒  mprop = mdry·(exp(deltaV/ve) − 1)
        // So the ascent knows the booster propellant to keep in reserve at MECO for the recovery budget.
        public static double PropMassForDeltaV(double dryMassKg, double deltaVMps, double ispS)
        {
            double ve = ispS * G0;
            if (dryMassKg > 0.0 && deltaVMps > 0.0 && ve > 0.0)
                return dryMassKg * (Math.Exp(deltaVMps / ve) - 1.0);
            return 0.0;
        }

        // Does the booster, at its current mass, still hold the recovery Δv (boostback + entry + landing) plus
        // a safety margin, if it cut off now? The MECO-for-recovery gate (Movement I-B ascent reads this).
        public static bool HasRecoveryReserve(double currentMassKg, double dryMassKg, double ispS,
                                              double requiredRecoveryDvMps, double marginMps)
        {
            return RemainingDeltaV(currentMassKg, dryMassKg, ispS) >= requiredRecoveryDvMps + marginMps;
        }
    }
}
