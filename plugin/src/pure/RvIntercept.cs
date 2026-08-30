// DragonScreen — RvIntercept  (autopilot rebuild: the Lambert two-impulse rendezvous INTERCEPT planner)
// ============================================================================================
// Wires the tested Lambert BVP solver (pure/Lambert.cs) into a rendezvous-usable planner: given the chaser
// state now, the target state now, mu and the target period, find the CHEAPEST pe-SAFE intercept — the
// departure Δv that puts the chaser on a transfer orbit reaching where the target WILL BE after a chosen
// time-of-flight. This is the arbitrary-geometry intercept the coarse phase-timed Hohmann raise + CW
// linearisation cannot do; it is what MechJeb's rendezvous uses (Lambert to a lead point, then close).
//
// ⛔ CREW-SAFETY: every candidate transfer's PERIAPSIS is computed from its own (r1, V1) and rejected if it
// dips below the pe floor. A Lambert intercept can otherwise route through a low perigee (cheap, but it grazes
// re-entry). The floor gate is what makes Lambert safe to fly where the raw CW two-impulse was not — a
// returned plan is guaranteed pe-safe. The cost cap (MaxDvMps) rejects garbage geometry (near-180° transfers
// blow up the same way CW did at long range).
//
// PURE + headless-tested: self-inversion (propagate the solution over tof → reaches the target-future point),
// known co-orbital intercept recovery, the pe-floor gate, and the cost cap. Target-future propagation uses our
// OWN Conic.Propagate so the planner is self-consistent with the solver's conic core.
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct InterceptPlan
    {
        public bool Ok;               // a pe-safe, in-budget intercept was found
        public Vec3 DepartureDv;      // Δv to apply NOW (world frame): V1_transfer − v_chaser
        public double DepartMagMps;   // |DepartureDv|
        public double TofS;           // chosen time-of-flight to the intercept
        public double TransferPeM;    // the transfer orbit's periapsis ALTITUDE (radius − bodyRadius)
        public bool PeSafe;           // TransferPeM ≥ the floor (always true for a returned Ok plan)
        public bool ShortWay;         // the transfer-angle branch that won
    }

    public static class RvIntercept
    {
        [Tunable] public static double MaxDvMps = 300.0;    // reject an intercept costing more than this (bad geometry)
        [Tunable] public static int TofSamples = 24;        // tof candidates scanned across the band
        [Tunable] public static double TofMinFrac = 0.15;   // tof search band as a fraction of the target period
        [Tunable] public static double TofMaxFrac = 1.10;

        // ---- ONE intercept solve to a FIXED target-future point + tof (the closed-loop executor calls this
        // each tick with the latched arrival state, so the residual departure Δv shrinks as the burn is flown).
        public static InterceptPlan Plan(Vec3 rChaser, Vec3 vChaser, Vec3 rTargetFuture,
                                         double mu, double bodyRadiusM, double tofS,
                                         double peFloorAltM, bool shortWay)
        {
            InterceptPlan p = new InterceptPlan();
            if (mu <= 0.0 || tofS <= 0.0) return p;
            // reuse the tested intercept primitive (pure/Maneuver): the Lambert departure Δv. Reconstruct the
            // transfer velocity V1 = v_chaser + Δv so we can size the transfer orbit's periapsis for the floor gate.
            bool ok; Vec3 dv = Maneuver.InterceptDv(rChaser, vChaser, rTargetFuture, tofS, mu, shortWay, out ok);
            if (!ok || !dv.IsFinite) return p;

            Vec3 v1 = vChaser + dv;
            double rp = PeriapsisRadius(rChaser, v1, mu);
            p.Ok = true;
            p.ShortWay = shortWay;
            p.DepartureDv = dv;
            p.DepartMagMps = dv.Magnitude;
            p.TofS = tofS;
            p.TransferPeM = rp - bodyRadiusM;
            p.PeSafe = p.TransferPeM >= peFloorAltM;
            return p;
        }

        // ---- SCAN the tof band (both transfer-angle branches), return the cheapest pe-safe intercept under the
        // cost cap. Ok=false when nothing qualifies → the caller falls back to the phase-timed co-elliptic raise.
        // The target-future position for each candidate tof is propagated from the target's OWN conic (pure).
        public static InterceptPlan Best(Vec3 rChaser, Vec3 vChaser, Vec3 rTarget, Vec3 vTarget,
                                         double mu, double bodyRadiusM, double targetPeriodS,
                                         double peFloorAltM)
        {
            InterceptPlan best = new InterceptPlan();
            if (targetPeriodS <= 0.0 || TofSamples < 1) return best;
            double lo = TofMinFrac * targetPeriodS, hi = TofMaxFrac * targetPeriodS;
            for (int i = 0; i < TofSamples; i++)
            {
                double f = TofSamples > 1 ? (double)i / (TofSamples - 1) : 0.0;
                double tof = lo + (hi - lo) * f;
                Vec3 rTf, vTf;
                if (!Conic.Propagate(rTarget, vTarget, mu, tof, out rTf, out vTf)) continue;
                for (int s = 0; s < 2; s++)
                {
                    InterceptPlan p = Plan(rChaser, vChaser, rTf, mu, bodyRadiusM, tof, peFloorAltM, s == 0);
                    if (!p.Ok || !p.PeSafe || p.DepartMagMps > MaxDvMps) continue;
                    if (!best.Ok || p.DepartMagMps < best.DepartMagMps) best = p;
                }
            }
            return best;
        }

        // Periapsis RADIUS of the orbit through (r, v): sma from vis-viva, ecc from the eccentricity vector.
        static double PeriapsisRadius(Vec3 r, Vec3 v, double mu)
        {
            double rm = r.Magnitude;
            if (rm < 1e-6 || mu <= 0.0) return 0.0;
            double vm2 = v.SqrMagnitude;
            double invA = 2.0 / rm - vm2 / mu;                                   // 1 / sma
            Vec3 evec = ((vm2 - mu / rm) * r - Vec3.Dot(r, v) * v) * (1.0 / mu); // eccentricity vector
            double ecc = evec.Magnitude;
            if (Math.Abs(invA) < 1e-15) return rm * (1.0 - ecc);                 // ~parabolic guard
            double sma = 1.0 / invA;
            return sma * (1.0 - ecc);                                            // rp = a(1 − e)
        }
    }
}
