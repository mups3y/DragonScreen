// DragonScreen — KerData  (the mirrored Kerbal Engineer per-stage data + pure selection; see MOD_INTEGRATION_RESEARCH)
// ============================================================================================
// KER runs a RealFuels/RO-accurate fuel-flow simulation and exposes per-stage Δv/TWR/thrust/Isp/mass/burn-time.
// We SOFT-integrate it: src/KerBridge.cs reads it by reflection (no compile-time reference to KerbalEngineer)
// and mirrors each stage into this POCO, in SI units, so NOTHING else in the codebase touches KER. This pure
// file holds the struct + the stage-selection logic (which stage is current, remaining Δv, reserve check) so
// that part is headless-tested; when KER is absent the arrays are empty and every consumer falls back to our
// own pure StageStats/Hoverslam. ⛔ KER is a soft input + cross-check, NEVER a hard dependency (user policy).
// ============================================================================================
namespace DragonScreen
{
    public struct KerStage
    {
        public int Number;             // KSP stage number (counts UP toward the currently-burning stage)
        public double DeltaVMps;       // vacuum Δv of THIS stage
        public double TotalDeltaVMps;  // cumulative Δv from this stage down (= remaining, for the current stage)
        public double ThrustN;         // max vacuum thrust (KerBridge converts KER kN → N)
        public double ActualThrustN;   // current thrust
        public double Twr, ActualTwr, MaxTwr;
        public double IspS;            // vacuum Isp
        public double MassKg;          // stage mass (KerBridge converts KER t → kg)
        public double TotalMassKg;     // cumulative
        public double ResourceMassKg;  // propellant mass
        public double BurnTimeS;       // this stage's burn duration
        public bool Valid;
    }

    public static class KerData
    {
        // The CURRENT (actively burning) stage = the HIGHEST-numbered stage in the sim (KSP counts the current
        // stage up; stage 0 is the final one). Empty array → Valid=false, so callers fall back to our own code.
        public static KerStage Current(KerStage[] stages)
        {
            if (stages == null || stages.Length == 0) return new KerStage();
            KerStage best = stages[0];
            for (int i = 1; i < stages.Length; i++) if (stages[i].Number > best.Number) best = stages[i];
            return best;
        }

        // The FINAL stage (number 0) — the last burn the payload will make (e.g. the deorbit / upper stage).
        public static KerStage Final(KerStage[] stages)
        {
            if (stages == null || stages.Length == 0) return new KerStage();
            KerStage best = stages[0];
            for (int i = 1; i < stages.Length; i++) if (stages[i].Number < best.Number) best = stages[i];
            return best;
        }

        // Remaining Δv from the current stage down = the current stage's cumulative TotalDeltaV.
        public static double RemainingDeltaV(KerStage[] stages)
        {
            KerStage c = Current(stages);
            return c.Valid ? c.TotalDeltaVMps : 0.0;
        }

        // A MECO recovery reserve holds when the remaining Δv still exceeds what the booster needs to fly home.
        public static bool HasRecoveryReserve(KerStage[] stages, double reserveMps)
        {
            return RemainingDeltaV(stages) >= reserveMps;
        }
    }
}
