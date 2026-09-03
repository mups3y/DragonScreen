// DragonScreen — CourseCorrect  (autopilot rebuild B8: finite-difference impact-point divert solve)
// ============================================================================================
// Steers a PREDICTED impact/splashdown point onto a target by measuring how the impact moves when the
// control is perturbed, then solving for the control change that nulls the miss — MechJeb's
// `ComputeCourseCorrection` idea (perturb by a small amount, build the sensitivity, solve), adapted to an
// atmospheric powered/lifting descent where the "control" is an aim/bank, not an orbital Δv.
//
// It is deliberately PURE: the CALLER owns the predictor (L1 Trajectory.Solve) and supplies three predicted
// impact ERRORS — the nominal e0, and e1/e2 after perturbing control axis 1 / axis 2 by du1 / du2. This
// class only does the linear algebra: the 2×2 finite-difference Jacobian and the divert that drives the
// error → 0 to first order, so it is trivially headless-testable and reused by BOTH the booster grid-fin
// targeting (down + cross, a true 2×2) and the capsule entry range channel (bank |σ|, a 1×1 Newton step).
//
// Error frame (caller's choice, consistent across e0/e1/e2): DownrangeM (+ = long / overshoot the target)
// and CrossrangeM (+ = right of the approach). The returned divert is in the caller's control units.
// A DAMPING gain (< 1) applies only a fraction of the first-order step so the closed loop can't ring, and a
// MaxStep clamps a single correction; when the Jacobian is not observable (near-singular) the solve reports
// ok=false and the caller falls back to its heuristic rather than diverting on noise.
// ============================================================================================
//
// ⚠ PROVENANCE + STATUS (W6, 2026-09-04). RESTORED from `8b81816^` (6,857 B, byte-for-byte as R1 §5.1
// records it). R1 §5.1 verdict: **RECOVER-CODE — §B16**; generation **2**, and there is NO gen-1 copy of
// this file anywhere in history (`git log --all --follow` gives exactly `a266420` → `8b81816`), so the
// two-generation rule is satisfied trivially — nothing from gen 1 was consulted or restored.
//
// ⛔ ONE SUBSTANTIVE EDIT, and it is a DELETION, stated here rather than made quietly (§B12.8 rider (b)).
// The restored file declared its own `public struct ImpactError { DownrangeM; CrossrangeM; }`. W8 has since
// written an `ImpactError` into `pure/BoosterDescent.cs:168` — same namespace, same two field names, same
// `+ = LONG` downrange sign, plus `Valid` and `GreatCircleM`. Two declarations do not compile, and they
// MUST NOT: `BoosterDescent.ErrorTo` is the PRODUCER of the error this class CONSUMES (method §5), so the
// producer and the consumer have to name one type. **The duplicate here is removed; the live struct is
// used.** Nothing else changed — a comment-stripped diff against `8b81816^` is identical apart from that
// struct. This class never CONSTRUCTS an `ImpactError`, it only reads two fields, so the deletion is
// mechanical.
//   ↳ One wording consequence: the deleted struct said crossrange `+ = right of the approach`; the live
//     one says `positive toward up × approach` (`ErrorTo`'s `crossHat`), which is the opposite hand. It
//     changes NOTHING here — the frame is the caller's and the solve only requires it be CONSISTENT across
//     e0/e1/e2, as the header above says — but the two sentences disagreed and the live file wins (C7.1).
//
// ⛔ UN-CONVERGED FOR RSS-RO (§B16.8 ruling 2). R1 §5.1 records this module **Flown? ❌ NO**, and
// `e90a63f` grades it *"○ — no lifting-entry flight in the corpus"*. All THREE constants it declares
// (`DampingGain`, `MinSensitivityM`, `MinDetFrac`) are therefore un-converged placeholders that make the
// solve run; none is attributed to a regime and none has been flown. Re-converge them from a recorded
// RSS-RO flight before trusting a commanded divert (§B16.8 ruling 3 — which needs glass time, a SEPARATE
// owner gate). The suite proves the ARITHMETIC against a known linear model; green means nothing about
// tuning.
//
// ℹ R1 §7.4 files this file under *"the 4-band L/D schedule"*. **That schedule is NOT in this file** — it
// is `pure/Trajectory.cs`'s `EntryLdBand` + `LdAtmosEntry`/`LdHighAltitude`/`LdLowAltitude`/`LdFinalApproach`
// (restored by W1, and still unmarked there). `a266420` shipped both, which is how the audit came to name
// them together. W6 did not widen its scope to another wave's file; the gap is logged as its own register
// line (C1.1). This file's own numbers are the three above.
// ============================================================================================
using System;

namespace DragonScreen
{
    // ⛔ W6: the `ImpactError` this class consumes is `pure/BoosterDescent.cs:168`'s — the one
    // `BoosterDescent.ErrorTo` produces. The restored duplicate was deleted here; see the header.

    public struct DivertResult
    {
        public bool Ok;             // false = Jacobian not observable → do NOT divert (fall back)
        public double Du1;          // control-axis-1 change (caller units), damped + clamped
        public double Du2;          // control-axis-2 change
        public double Det;          // Jacobian determinant (observability diagnostic)
    }

    public static class CourseCorrect
    {
        // Damping: apply this fraction of the raw first-order step (Newton would be 1.0). < 1 keeps the
        // closed loop from ringing when the sensitivity is only approximately linear. TCA/MechJeb use a
        // similarly conservative gain on the perturbation solve.
        [Tunable] public static double DampingGain = 0.7;        // [UN-CONVERGED] never flown (R1 §5.1)
        // A Jacobian axis is "observable" only if a perturbation moved the impact at least this far (metres);
        // below it the finite-difference is dominated by predictor noise and the solve is refused.
        [Tunable] public static double MinSensitivityM = 1.0;    // [UN-CONVERGED] a predictor-noise floor, unmeasured
        // Relative determinant floor: |det| must exceed this × the product of the two column norms, else the
        // two control axes push the impact in nearly the same direction (rank-deficient) → refuse.
        [Tunable] public static double MinDetFrac = 1.0e-3;      // [UN-CONVERGED] rank floor, unattributed

        // ---- 2×2 finite-difference divert (booster: axis1 = down-control, axis2 = cross-control) ----
        // J = [ (e1 - e0)/du1 , (e2 - e0)/du2 ]  (columns are the per-axis impact sensitivity vectors).
        // Solve  J · [x1;x2] = −e0  for the control change that zeros the miss to first order, then damp+clamp.
        public static DivertResult Solve2x2(ImpactError e0, ImpactError e1, ImpactError e2,
                                            double du1, double du2, double maxStep)
        {
            DivertResult r = new DivertResult();
            if (du1 == 0.0 || du2 == 0.0) return r;   // no perturbation → nothing to measure

            // Jacobian columns: c1 = d(impact)/d(u1), c2 = d(impact)/d(u2).
            double c1d = (e1.DownrangeM - e0.DownrangeM) / du1;
            double c1c = (e1.CrossrangeM - e0.CrossrangeM) / du1;
            double c2d = (e2.DownrangeM - e0.DownrangeM) / du2;
            double c2c = (e2.CrossrangeM - e0.CrossrangeM) / du2;

            // observability: each perturbation must actually move the impact.
            double m1 = Math.Sqrt((e1.DownrangeM - e0.DownrangeM) * (e1.DownrangeM - e0.DownrangeM)
                                + (e1.CrossrangeM - e0.CrossrangeM) * (e1.CrossrangeM - e0.CrossrangeM));
            double m2 = Math.Sqrt((e2.DownrangeM - e0.DownrangeM) * (e2.DownrangeM - e0.DownrangeM)
                                + (e2.CrossrangeM - e0.CrossrangeM) * (e2.CrossrangeM - e0.CrossrangeM));
            if (m1 < MinSensitivityM || m2 < MinSensitivityM) return r;

            double det = c1d * c2c - c2d * c1c;
            r.Det = det;
            double col1 = Math.Sqrt(c1d * c1d + c1c * c1c);
            double col2 = Math.Sqrt(c2d * c2d + c2c * c2c);
            if (Math.Abs(det) < MinDetFrac * col1 * col2) return r;   // rank-deficient → refuse

            // Cramer's rule on  J·x = −e0 :
            double x1 = (-e0.DownrangeM * c2c + e0.CrossrangeM * c2d) / det;
            double x2 = (-c1d * e0.CrossrangeM + c1c * e0.DownrangeM) / det;

            x1 *= DampingGain; x2 *= DampingGain;
            r.Du1 = Clamp(x1, maxStep);
            r.Du2 = Clamp(x2, maxStep);
            r.Ok = true;
            return r;
        }

        // ---- 1×1 finite-difference divert (entry range: control = bank |σ|; only downrange matters) ----
        // Newton step  du = −err0 · du / (err1 − err0),  damped + clamped. Cross is handled separately by the
        // entry S-turn sign logic, so this channel is scalar.
        public static DivertResult Solve1x1(double downErr0, double downErr1, double du, double maxStep)
        {
            DivertResult r = new DivertResult();
            if (du == 0.0) return r;
            double slope = (downErr1 - downErr0) / du;          // d(downrange)/d(control)
            if (Math.Abs(downErr1 - downErr0) < MinSensitivityM) return r;   // control didn't move the impact
            double x = -downErr0 / slope;
            x *= DampingGain;
            r.Du1 = Clamp(x, maxStep);
            r.Det = slope;
            r.Ok = true;
            return r;
        }

        private static double Clamp(double v, double maxAbs)
        {
            if (maxAbs <= 0.0) return v;
            if (v > maxAbs) return maxAbs;
            if (v < -maxAbs) return -maxAbs;
            return v;
        }
    }
}
