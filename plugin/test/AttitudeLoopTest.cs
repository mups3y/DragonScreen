/*
 * Tests for the ported MechJeb BetterController attitude law (pure/AttitudeLoop.cs + Pid2).
 *
 * The important one is CONVERGENCE: a closed-loop sim of a rigid body (error' = -omega, omega' = achieved
 * alpha) must drive a large pointing error to ~0 without diverging or wild overshoot — the property the
 * three max-Q RUDs lacked. Also pins: no-authority safety, the arrestable-rate cap, the MechJeb negative
 * actuation sign convention, and the roll-gate rate damping.
 */
using DragonScreen;
using System;

public static class AttitudeLoopTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    // A representative axis: full stack ~ MOI 3e5 kg·m², gimbal torque ~1e7 N·m (ATTITUDE_CONTROL_RESEARCH).
    const double MOI = 3.0e5, CTRL = 1.0e7, DT = 0.02;

    public static int Run()
    {
        Console.WriteLine("DragonScreen attitude-loop (BetterController port) tests");

        // ---- no authority is safe: zero torque / zero MOI → zero actuation, no throw ----
        Pid2 p = new Pid2(), q = new Pid2();
        Check("zero control torque -> 0 actuation", AttitudeLoop.Axis(0.3, 0, MOI, 0.0, DT, false, p, q).Actuation == 0.0, "");
        Check("zero MOI -> 0 actuation", AttitudeLoop.Axis(0.3, 0, 0.0, CTRL, DT, false, p, q).Actuation == 0.0, "");
        Check("NaN error -> 0 actuation", AttitudeLoop.Axis(double.NaN, 0, MOI, CTRL, DT, false, p, q).Actuation == 0.0, "");

        // ---- at target, at rest → ~no command ----
        p.Reset(); q.Reset();
        double a0 = AttitudeLoop.Axis(0.0, 0.0, MOI, CTRL, DT, false, p, q).Actuation;
        Check("at target + at rest -> ~0 actuation", Math.Abs(a0) < 1e-6, "act=" + a0);

        // ---- MechJeb sign convention: positive error -> negative actuation ----
        p.Reset(); q.Reset();
        AttitudeAxisResult big = AttitudeLoop.Axis(0.5, 0.0, MOI, CTRL, DT, false, p, q);
        Check("positive error -> positive targetOmega", big.TargetOmega > 0.0, "w=" + big.TargetOmega);
        Check("positive error -> NEGATIVE actuation (KSP orientation)", big.Actuation < 0.0, "act=" + big.Actuation);
        Check("maxAlpha = ctrl/MOI", Math.Abs(big.MaxAlpha - CTRL / MOI) < 1e-9, "");

        // ---- arrestable-rate cap: targetOmega never exceeds maxAlpha·MaxStoppingTime ----
        p.Reset(); q.Reset();
        double maxOmega = (CTRL / MOI) * AttitudeLoop.MaxStoppingTime;
        AttitudeAxisResult huge = AttitudeLoop.Axis(2.0, 0.0, MOI, CTRL, DT, false, p, q);
        Check("targetOmega clamped to arrestable cap", huge.TargetOmega <= maxOmega + 1e-9, "w=" + huge.TargetOmega + " cap=" + maxOmega);

        // ---- roll gate: suppressOmega damps the rate to zero (actuation opposes omega) ----
        p.Reset(); q.Reset();
        AttitudeAxisResult damp = AttitudeLoop.Axis(0.4, +0.05, MOI, CTRL, DT, true, p, q);
        Check("roll-gate: targetOmega forced 0", damp.TargetOmega == 0.0, "");
        Check("roll-gate: positive rate -> positive actuation (opposes rate)", damp.Actuation > 0.0, "act=" + damp.Actuation);

        // ---- CONVERGENCE: a rigid body driven by the loop reaches the target and stays ----
        Check("30° slew converges to <0.5° without divergence", ConvergesFrom(30.0 * Math.PI / 180.0), "");
        Check("5° slew converges to <0.5°", ConvergesFrom(5.0 * Math.PI / 180.0), "");
        Check("120° flip converges to <0.5°", ConvergesFrom(120.0 * Math.PI / 180.0), "");

        // ============ S2 + DEORBIT DIVERGENCE — headless root-cause proof (flights DS-ASC-001/002 + deorbit) ============
        // ROOT CAUSE = a ×1000 UNITS BUG in the RCS geometric estimate (ControlTorque computed thruster power as
        // thrusterPower*1000 = N, so the geometric came out in N·m while stock, the gimbal, and MOI are kN·m/t·m²;
        // MAX(stock,geometric) then always picked the 1000×-inflated geometric). Plant from the recorded S2+Dragon
        // (DragonScreen_capture/Crew-2_20260831_102133.csv): MOI_pitch ≈ 1650 t·m²; the loop's ctrl_tq ≈ 62000.
        //   • Decomposed: ctrl_tq − rcs_geo = the gimbal stock report gx ≈ 464 kN·m, which MATCHES the delivered
        //     445 kN·m (regression below) — the stock GIMBAL is accurate. The remaining 62000 is the N·m-bugged
        //     RCS geometric; in correct units it is 62 kN·m (real S2 RCS ≈ 0 — the gimbal carries pitch/yaw).
        //   • The loop scales EVERYTHING off maxAlpha = controlTorque/MOI, so the 1000×-inflated 62000 made
        //     maxAlpha read 37.6 rad/s² (real 0.27) → commanded rates the stage can't achieve → tumble.
        const double MoiS1 = 75508.0, CtS1 = 11846.0;   // S1 full stack (recorded) — flew, stable
        const double MoiS2 = 1650.0;                     // S2+Dragon MOI (recorded, t·m²)
        const double CtS2Est = 62000.0;                  // S2 loop ESTIMATE as-flown (N·m-bugged geometric)
        const double CtS2Fixed = 526.0;                  // units-FIXED estimate: gimbal 464 + RCS-geo 62 (kN·m)
        // CtS2Real: the S2's ACTUAL per-unit control torque, measured from the flight by regressing net torque
        // (MOI·Δω) against commanded actuation over the S2 window (DS-ASC-002): K ≈ 445-451, intercept ≈ 0 (NO
        // disturbance). In the loop's own units this is 451; the as-flown over-read is ~137×.
        const double CtS2Real = 451.0;
        // Deorbit capsule (Dragon alone, no gimbal; DragonScreen_capture/Crew-2_20260831_141924.csv + geometry
        // dump manual_2500s): MOI ≈ 14 t·m²; stock RCS 2, N·m-bugged geometric 12870 (→ 12.9 kN·m fixed),
        // DELIVERED 7 kN·m (regression). As-flown maxAlpha read 919 rad/s² (real 0.5) → the capsule spun.
        const double MoiCap = 14.0, CtCapEst = 12870.0, CtCapFixed = 12.87, CtCapReal = 7.0;

        // (a) OPEN-LOOP: the loop's own maxAlpha from the RECORDED numbers (over-read authority estimate).
        p.Reset(); q.Reset();
        double maxAlphaS1 = AttitudeLoop.Axis(0.7, 0.0, MoiS1, CtS1, DT, false, p, q).MaxAlpha;
        p.Reset(); q.Reset();
        double maxAlphaS2 = AttitudeLoop.Axis(0.7, 0.0, MoiS2, CtS2Est, DT, false, p, q).MaxAlpha;
        Check("recorded S1 authority → gentle maxAlpha (<0.3 rad/s²)", maxAlphaS1 < 0.3, "S1=" + maxAlphaS1.ToString("F3"));
        Check("recorded S2 over-read → absurd maxAlpha (>20 rad/s², ≈2000°/s²)", maxAlphaS2 > 20.0, "S2=" + maxAlphaS2.ToString("F1"));

        // (b) CLOSED-LOOP root-cause PROOF: SAME plant (MOI 1650, flight-measured real authority 451, NO
        // disturbance); only the loop's torque ESTIMATE differs. The recorded 137× over-read LIMIT-CYCLES
        // (reproduces the tumble); a correct estimate CONVERGES. Note the instability is a THRESHOLD effect:
        // a small over-read is sluggish-but-stable, but past ~10× the loop commands rates the tiny real
        // authority can't achieve → overshoot → divergent limit-cycle. This is why my earlier stand-in
        // (real≈11846, only 5× over-read) wrongly looked stable; the flight regression put real at ~451.
        double wM, fM, wI, fI, wX, fX;
        bool convMatched  = SimMismatch(60.0 * Math.PI / 180.0, CtS2Real,  CtS2Real, MoiS2, out wM, out fM);
        bool convInflated = SimMismatch(60.0 * Math.PI / 180.0, CtS2Est,   CtS2Real, MoiS2, out wI, out fI);
        bool convFixed    = SimMismatch(60.0 * Math.PI / 180.0, CtS2Fixed, CtS2Real, MoiS2, out wX, out fX);
        Check("S2 ideal (estimate = real authority) → CONVERGES", convMatched, "worst=" + Deg(wM) + "° final=" + Deg(fM) + "°");
        Check("S2 as-flown 137x over-read (est 62000, real 451) LIMIT-CYCLES — reproduces the tumble",
              !convInflated, "worst=" + Deg(wI) + "° final=" + Deg(fI) + "°");
        Check("S2 UNITS FIX (est 526 = gimbal 464 + RCS-geo 62, real 445) → CONVERGES", convFixed,
              "worst=" + Deg(wX) + "° final=" + Deg(fX) + "°");

        // Deorbit capsule: same mechanism, no gimbal. As-flown (12870, N·m bug) spins; units-fixed (12.9) holds.
        double wc, fc, wcf, fcf;
        bool capBroken = SimMismatch(60.0 * Math.PI / 180.0, CtCapEst,   CtCapReal, MoiCap, out wc,  out fc);
        bool capFixed  = SimMismatch(60.0 * Math.PI / 180.0, CtCapFixed, CtCapReal, MoiCap, out wcf, out fcf);
        Check("DEORBIT capsule as-flown (est 12870, real 7) LIMIT-CYCLES — reproduces the spin",
              !capBroken, "worst=" + Deg(wc) + "° final=" + Deg(fc) + "°");
        Check("DEORBIT capsule UNITS FIX (est 12.9, real 7) → CONVERGES", capFixed,
              "worst=" + Deg(wcf) + "° final=" + Deg(fcf) + "°");

        // ===== DS-ASC-005: the geometric OVER-READ drives Draco SATURATION = the terminal fuel drain =====
        // Applied-actuation regression (correct technique) measured the capsule DELIVERED RCS authority at
        // pitch 0.6 / roll 1.3 / yaw 2.5 kN·m; the geometric (which won the OLD Max) reads 13.5 / 10.7 / 10.3 →
        // 4-22× over. Over-reading maxAlpha over-drives the gimballess capsule → the Dracos saturate → ~85% of the
        // terminal MMH burns on attitude (rendezvous ran dry). The FIX trusts STOCK (2.2 / 3.1 / 1.3), far closer.
        // Proof: actuator EFFORT (∫|act|, a fuel proxy) from a 10° hold is far higher for the geometric than stock.
        const double MoiCapAtt = 41.0;
        double effGeoP = ActEffort(10.0 * Math.PI / 180.0, 13.5, 0.6, MoiCapAtt);   // geometric (as-flown, 22× over)
        double effStkP = ActEffort(10.0 * Math.PI / 180.0,  2.2, 0.6, MoiCapAtt);   // stock (the fix)
        double effGeoY = ActEffort(10.0 * Math.PI / 180.0, 10.3, 2.5, MoiCapAtt);
        double effStkY = ActEffort(10.0 * Math.PI / 180.0,  1.3, 2.5, MoiCapAtt);
        Check("DS-ASC-005: geometric over-read spends >2× the pitch actuator effort of stock (fuel)",
              effGeoP > 2.0 * effStkP, "geo=" + effGeoP.ToString("F1") + " stock=" + effStkP.ToString("F1"));
        Check("DS-ASC-005: geometric over-read spends more yaw actuator effort than stock",
              effGeoY > effStkY, "geo=" + effGeoY.ToString("F1") + " stock=" + effStkY.ToString("F1"));

        // (the phase-plane deadband / hold-scale / lag-comp inventions were removed 2026-09-01 — the loop is now a
        //  faithful MechJeb BetterController port, so there is nothing extra to test beyond the port above.)

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // Closed-loop sim of one rigid axis: error' = -omega, omega' = achieved angular accel. The effector
    // produces torque = -actuation·controlTorque (the MechJeb sign), so achieved alpha = that / MOI. Verify
    // the pointing error settles small and never blows up.
    static bool ConvergesFrom(double error0)
    {
        Pid2 pos = new Pid2(), vel = new Pid2();
        double error = error0, omega = 0.0;
        double worst = 0.0;
        int steps = (int)(20.0 / DT);   // 20 s
        for (int i = 0; i < steps; i++)
        {
            AttitudeAxisResult r = AttitudeLoop.Axis(error, omega, MOI, CTRL, DT, false, pos, vel);
            double alpha = -r.Actuation * CTRL / MOI;   // physical angular accel achieved (full authority)
            omega += alpha * DT;
            error += -omega * DT;
            double mag = Math.Abs(error);
            if (mag > worst) worst = mag;
            if (double.IsNaN(error) || mag > 4.0 * Math.PI) return false;   // diverged
        }
        // settled within 0.5°, and never overshot past ~2× the initial excursion
        return Math.Abs(error) < 0.5 * Math.PI / 180.0 && worst < 2.0 * Math.Abs(error0) + 0.05;
    }

    // Closed-loop sim with a possible authority MISMATCH: the loop is given `ctEst` (what ControlTorque
    // reports), but the physical plant produces torque from `ctReal` (what the effectors actually deliver).
    // error' = -omega ; omega' = -actuation·ctReal/MOI. Returns whether it settled (did not diverge/limit-cycle).
    static bool SimMismatch(double error0, double ctEst, double ctReal, double moi,
                            out double worst, out double finalErr)
    {
        Pid2 pos = new Pid2(), vel = new Pid2();
        double error = error0, omega = 0.0;
        worst = Math.Abs(error0);
        int steps = (int)(20.0 / DT);   // 20 s
        for (int i = 0; i < steps; i++)
        {
            AttitudeAxisResult r = AttitudeLoop.Axis(error, omega, moi, ctEst, DT, false, pos, vel);
            double alpha = -r.Actuation * ctReal / moi;   // PHYSICAL accel = actuation × the REAL authority
            omega += alpha * DT;
            error += -omega * DT;
            double mag = Math.Abs(error);
            if (mag > worst) worst = mag;
            if (double.IsNaN(error) || mag > 4.0 * Math.PI) { finalErr = mag; return false; }   // diverged
        }
        finalErr = Math.Abs(error);
        return finalErr < 2.0 * Math.PI / 180.0;   // settled within 2°
    }

    // Closed-loop actuator EFFORT (∫|actuation|dt) from an initial error — a propellant proxy for an on/off RCS.
    // A too-high ctEst over-drives the loop (aggressive targetOmega the tiny real authority can't arrest) → the
    // actuation saturates → more effort spent. Same plant as SimMismatch (alpha = -actuation·ctReal/MOI).
    static double ActEffort(double error0, double ctEst, double ctReal, double moi)
    {
        Pid2 pos = new Pid2(), vel = new Pid2();
        double error = error0, omega = 0.0, effort = 0.0;
        int steps = (int)(30.0 / DT);
        for (int i = 0; i < steps; i++)
        {
            AttitudeAxisResult r = AttitudeLoop.Axis(error, omega, moi, ctEst, DT, false, pos, vel);
            effort += Math.Abs(r.Actuation) * DT;
            double alpha = -r.Actuation * ctReal / moi;
            omega += alpha * DT; error += -omega * DT;
        }
        return effort;
    }

    static string Deg(double rad) { return (rad * 180.0 / Math.PI).ToString("F1"); }
}
