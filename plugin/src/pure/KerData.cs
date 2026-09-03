// DragonScreen - KerData  (the mirrored Kerbal Engineer per-stage data + pure selection + the readout group)
// ============================================================================================
// KER runs a RealFuels/RO-accurate fuel-flow simulation and exposes per-stage Δv/TWR/thrust/Isp/mass/burn-time.
// We SOFT-integrate it: src/KerBridge.cs reads it by reflection (no compile-time reference to KerbalEngineer)
// and mirrors each stage into this POCO, in SI units, so NOTHING else in the codebase touches KER. This pure
// file holds the struct + the stage-selection logic (which stage is current, remaining Δv, reserve check) so
// that part is headless-tested; when KER is absent the arrays are empty and every consumer falls back to our
// own pure StageStats. ⛔ KER is a soft input, NEVER a hard dependency (user policy 2026-08-28).
// Access method, inventory and guarding: docs/KER_DATA_RESEARCH.md (§1.6 the drive recipe, §5.2 the three
// guard levels, §5.3 the tier-2 marking this file carries).
//
// ---- WHAT IS REAL AND WHAT IS MODELLED ----
//     REAL      every number below. KER solves the REAL part tree (RealFuels/RO engine models, crossfeed,
//               per-engine grouping); this file only SELECTS a stage and FORMATS it.
//     MODELLED  nothing. There is no fallback value here and there deliberately is not one: with no KER
//               result the group is empty, every string is null, and the page draws its dash.
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

        /// <summary>
        /// The whole propulsion-performance group for the CURRENT stage, in SI and pre-formatted, or an EMPTY
        /// group whose every string is null. Everything a KER-sourced row needs is decided here so it is decided
        /// once and headless-tested: the stage choice, the docked guard, the finite check and the formatting.
        ///
        /// <para>Empty (HasResult false, all text null) when ANY of:</para>
        /// <list type="bullet">
        /// <item>KER is absent or is not driving — the bridge hands us null (guard levels 1 and 2, §5.2).</item>
        /// <item><paramref name="docked"/> — KSP MERGES both craft into one Vessel when berthed, so KER
        /// simulates the STACK. A Δv/TWR/thrust number read while docked is the station's as much as ours, and
        /// DockedSide cannot help because the merge happens inside KER's own simulation, before we see the
        /// number (§3.1). Dashing is the only honest answer.</item>
        /// <item>a non-finite value anywhere in the group — one NaN makes the whole group untrustworthy, so it
        /// all dashes rather than some of it reading confidently (registry rule N2).</item>
        /// </list>
        /// </summary>
        public static KerPerformance Performance(KerStage[] stages, bool docked)
        {
            KerPerformance p = new KerPerformance();
            if (docked) return p;

            KerStage c = Current(stages);
            if (!c.Valid) return p;
            if (!Finite(c.DeltaVMps) || !Finite(c.TotalDeltaVMps) || !Finite(c.ThrustN) || !Finite(c.ActualThrustN)
                || !Finite(c.Twr) || !Finite(c.ActualTwr) || !Finite(c.MaxTwr) || !Finite(c.IspS)
                || !Finite(c.MassKg) || !Finite(c.TotalMassKg) || !Finite(c.ResourceMassKg) || !Finite(c.BurnTimeS))
                return p;

            p.HasResult          = true;
            p.DeltaVMps          = c.DeltaVMps;
            p.RemainingDeltaVMps = c.TotalDeltaVMps;
            p.ThrustN            = c.ThrustN;
            p.ActualThrustN      = c.ActualThrustN;
            p.Twr                = c.Twr;
            p.ActualTwr          = c.ActualTwr;
            p.MaxTwr             = c.MaxTwr;
            p.IspS               = c.IspS;
            p.StageMassKg        = c.MassKg;
            p.TotalMassKg        = c.TotalMassKg;
            p.ResourceMassKg     = c.ResourceMassKg;
            p.BurnTimeS          = c.BurnTimeS;

            // Unit-carrying, like every other detail-ROW string (Pages.cs): the page prints ONE right-aligned
            // string per row rather than a value and a unit in separate columns. kN, not our internal newtons -
            // KER's own ToForce() formats thrust in kN and so does every launch commentary in the world.
            p.DeltaVText          = c.DeltaVMps.ToString("F0") + " m/s";
            p.RemainingDeltaVText = c.TotalDeltaVMps.ToString("F0") + " m/s";
            p.ThrustAvailText     = (c.ThrustN / 1000.0).ToString("F1") + " kN";
            p.ActualThrustText    = (c.ActualThrustN / 1000.0).ToString("F1") + " kN";
            p.TwrText             = c.Twr.ToString("F2");
            p.IspText             = c.IspS.ToString("F0") + " s";
            p.BurnTimeText        = c.BurnTimeS.ToString("F0") + " s";
            p.StageMassText       = c.MassKg.ToString("F0") + " kg";
            p.TotalMassText       = c.TotalMassKg.ToString("F0") + " kg";
            return p;
        }

        static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
    }

    /// <summary>
    /// The propulsion-performance readout group, for the CURRENT stage: Δv, TWR, thrust, Isp, burn time and
    /// mass, produced by <see cref="KerData.Performance"/> from Kerbal Engineer's fuel-flow simulation.
    ///
    /// <para><b>SOURCE: KER, tier-2 under BUILD_PLAN §14.4(e) step (1)</b> — an installed mod's value, MARKED
    /// (§5.3 mechanism 1: this comment, src/KerBridge.cs's header note, and the KER rows in
    /// docs/TELEMETRY_REGISTRY.md). Nothing here is simulated: KER solves the real part tree and this is that
    /// solve, converted to SI at the bridge boundary and formatted here.</para>
    ///
    /// <para><b>UNITS.</b> Raw fields are SI — m/s, newtons, kilograms, seconds; TWR and Isp are unitless and
    /// seconds. The text fields carry their own printed unit and are NOT SI: thrust prints kN, mass prints kg,
    /// Δv m/s, burn time s, TWR bare.</para>
    ///
    /// <para><b>NULL MEANS DASH.</b> Every text field is null exactly when <see cref="HasResult"/> is false — KER
    /// absent, no result yet for this vessel, DOCKED (KER simulates the merged stack), or a non-finite value in
    /// the group. There is no per-field flag and no fallback number: the page's T() turns null into the dash,
    /// the same contract WaterText uses for TAC-LS.</para>
    ///
    /// <para>⚠ UNVERIFIED IN FLIGHT. This is KER's first consumer in this build, so the kN→N / t→kg
    /// conversions and the stage-number ordering have never been cross-checked against a live game
    /// (docs/KER_DATA_RESEARCH.md §6.2 V1/V2/V4). Held for a glass go; the numbers are believed right, not
    /// proven right.</para>
    /// </summary>
    public struct KerPerformance
    {
        /// <summary>A KER result exists for THIS vessel and it is safe to show. False =&gt; every text is null.</summary>
        public bool HasResult;
        public double DeltaVMps;            // this stage alone
        public double RemainingDeltaVMps;   // this stage and everything below it
        public double ThrustN;              // MAXIMUM thrust at the sim's atmosphere/Mach inputs
        public double ActualThrustN;        // thrust at the current throttle
        public double Twr, ActualTwr, MaxTwr;
        public double IspS;
        public double StageMassKg, TotalMassKg, ResourceMassKg;
        public double BurnTimeS;

        /// <summary>PROPULSION tab, "Thrust Avail" — the MAXIMUM thrust this stage can make, in kN. Maximum and
        /// not <see cref="ActualThrustN"/>, because the label asks what is AVAILABLE, not what is being used.</summary>
        public string ThrustAvailText;
        public string ActualThrustText;
        public string DeltaVText, RemainingDeltaVText, TwrText, IspText, BurnTimeText;
        public string StageMassText, TotalMassText;
    }
}
