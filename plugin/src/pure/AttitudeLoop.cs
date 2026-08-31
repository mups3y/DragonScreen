// DragonScreen — AttitudeLoop  (PURE L2: the per-axis gimbal/RCS attitude law, ported from MechJeb)
// ============================================================================================
// The direct-control replacement for stock SAS — the fix for the max-Q loss of control (SAS's PID is too
// slow for FAR's transonic divergence; this fast loop uses the ample gimbal authority). Ported EXACTLY
// from MechJeb2 `AttitudeControllers/BetterController.cs` (read in full — docs/ATTITUDE_CONTROL_RESEARCH.md),
// which flies RO rockets on gimbal alone. This is the pure per-axis half (doubles in, actuation out), so it
// is headless-tested; the frame conversion (quaternion error, the (pitch,roll,yaw) reorder + negated yaw,
// the negative actuation sign, and summing GetPotentialTorque for the live authority) is the glue's job
// (src/AttitudePilot.cs).
//
// The cascade (per axis, i = 0 pitch / 1 roll / 2 yaw):
//   maxAlpha = controlTorque/MOI               (max angular accel the effectors can produce)
//   effLD    = soften²·maxAlpha/(2·posKp²)      (linear/braking blend half-width; soften 0.5, posKp 2.03)
//   |error| ≤ 2·effLD → targetOmega = positionPID(error)              (near target: PI on the angle error)
//   else             → targetOmega = soften·√(2·maxAlpha·(|e|−effLD))·sign(e)  (ARRESTABLE-RATE braking curve)
//   clamp targetOmega to ±maxAlpha·2s (MaxStoppingTime), floor ±π/120 (MinFlipTime)
//   targetAlpha = velocityPID(targetOmega, ω)  (Kp 7.98, clamped ±maxAlpha)
//   actuation   = −MOI·targetAlpha / controlTorque   (⚠ NEGATIVE — KSP actuation orientation)
// ============================================================================================
using System;

namespace DragonScreen
{
    // A faithful reduction of MechJeb's PIDLoop2 (MechJebLib/Control/PIDLoop2.cs) to the config
    // BetterController ships: 2-DOF setpoint weighting (B,C), derivative filter (N), trapezoidal integral,
    // output saturation + back-calculation anti-windup. The pass-through extras (input/output smoothing,
    // deadbands, Clegg) are at their defaults and omitted. Stateful → one instance per axis, Reset on handover.
    public class Pid2
    {
        public double Kp = 1.0, Ti = 0.0, Td = 0.0, N = 1.0, Ts = 0.02, B = 1.0, C = 1.0;
        public double MinOutput = double.MinValue, MaxOutput = double.MaxValue;

        public double PTerm, ITerm, DTerm;
        double ei1, ed1;

        public double Update(double r, double y)
        {
            double ep = B * r - y;
            double ei = r - y;
            double ed = C * r - y;

            PTerm = Kp * ep;

            // trapezoidal integrator (÷Ti); MechJeb computes then NaN-guards, which also handles Ti==0.
            double k = Kp == 0.0 ? 1.0 : Kp;
            ITerm += 0.5 * k * Ts * (ei + ei1) / Ti;

            // trapezoidal derivative with first-order filter (N). Td==0 → DTerm holds 0.
            double den = 2.0 * Td + N * Ts;
            DTerm = (2.0 * Td - N * Ts) / den * DTerm + 2.0 * N * Kp * Td / den * (ed - ed1);

            if (double.IsNaN(ITerm) || double.IsInfinity(ITerm)) ITerm = 0.0;
            if (double.IsNaN(DTerm) || double.IsInfinity(DTerm)) DTerm = 0.0;

            double z = PTerm + ITerm + DTerm;
            double u = z < MinOutput ? MinOutput : (z > MaxOutput ? MaxOutput : z);

            // back-calculation anti-windup (only when integrating)
            if (Ti != 0.0)
            {
                double tr = Td == 0.0 ? Ti : Math.Sqrt(Ti * Td);
                ITerm += Ts / tr * (u - z);
            }

            ei1 = ei; ed1 = ed;
            return u;
        }

        public void Reset() { ITerm = DTerm = ei1 = ed1 = 0.0; }
    }

    public struct AttitudeAxisResult
    {
        public double Actuation;    // −1..1, straight onto s.pitch/roll/yaw
        public double TargetOmega;  // commanded body rate (rad/s)
        public double TargetAlpha;  // commanded angular accel (rad/s²)
        public double MaxAlpha;     // available angular accel (rad/s²) — the live authority readout
    }

    public static class AttitudeLoop
    {
        public const double Soften = 0.5;               // (0,1] — reduce overshoot on large slews
        public const double PosKp0 = 2.03, PosTi = 1.97; // position PI (Td=0)
        public const double VelKp = 7.98;               // velocity P (Ti=Td=0)
        public const double MaxStoppingTime = 2.0;      // targetOmega ≤ maxAlpha·this
        public const double MinFlipTime = 120.0;        // targetOmega floor π/this (so a flip still starts)
        public const double RollControlRangeDeg = 5.0;  // don't fight roll until pointed within this
        const double Eps = 1e-10;

        // One axis end-to-end. `suppressOmega` = the roll-control-range gate (targetOmega forced 0 so the
        // velocity loop just damps the rate to zero while the nose is still slewing). posPid/velPid carry
        // the per-axis integrator state across ticks. No authority → actuation 0 and the PIDs reset.
        public static AttitudeAxisResult Axis(double errorRad, double omegaRad, double moi,
                                              double controlTorqueNm, double dt,
                                              bool suppressOmega, Pid2 posPid, Pid2 velPid,
                                              double holdDbRad = 0.0, double holdRateDbRadps = 0.0,
                                              double posKpScale = 1.0)
        {
            AttitudeAxisResult r = new AttitudeAxisResult();
            if (!(controlTorqueNm > 0.0) || !(moi > 0.0) || !(dt > 0.0)
                || double.IsNaN(errorRad) || double.IsNaN(omegaRad))
            { posPid.Reset(); velPid.Reset(); return r; }

            double warpFactor = dt / 0.02; if (warpFactor <= 0.0) warpFactor = 1.0;
            double maxAlpha = controlTorqueNm / moi;
            r.MaxAlpha = maxAlpha;

            // ⭐ PHASE-PLANE DEADBAND (RCS attitude-hold fuel fix, DS-ASC-007): within a small (angle, rate) box,
            // COAST — command nothing, let the vehicle DRIFT — instead of chattering the on/off Dracos to null a
            // tiny error. It bites ONLY when BOTH |error| and |rate| are tiny (a gentle hold); any slew / abort /
            // detumble (large error OR rate) is OUTSIDE the box and fires the normal law. Off by default (0) → the
            // flight-proven gimbal ascent is unchanged; the glue passes a nonzero band ONLY when RCS is the
            // attitude actuator (no gimbal). The classic on-orbit RCS law real spacecraft use (SAS/MechJeb do NOT).
            if (!suppressOmega && holdDbRad > 0.0
                && errorRad > -holdDbRad && errorRad < holdDbRad
                && omegaRad > -holdRateDbRadps && omegaRad < holdRateDbRadps)
            {
                posPid.Reset(); velPid.Reset();
                return r;   // actuation 0, targetOmega/Alpha 0 → drift within the deadband
            }

            double posKp = PosKp0 / warpFactor * (posKpScale > 0.0 ? posKpScale : 1.0);   // ⭐ hold-authority scale (owner 1.5×)
            double effLD = Soften * Soften * maxAlpha / (2.0 * posKp * posKp);
            double maxOmega = maxAlpha * MaxStoppingTime;
            double flip = Math.PI / MinFlipTime;
            if (flip > maxOmega) maxOmega = flip;

            double targetOmega;
            if (suppressOmega)
            {
                targetOmega = 0.0;
                posPid.Reset();
            }
            else if (Math.Abs(errorRad) <= 2.0 * effLD)
            {
                posPid.Kp = posKp; posPid.Ti = PosTi; posPid.Td = 0.0; posPid.N = 1.0;
                posPid.B = 1.0; posPid.C = 1.0; posPid.Ts = dt;
                posPid.MinOutput = -maxOmega; posPid.MaxOutput = maxOmega;
                targetOmega = posPid.Update(errorRad, 0.0);   // setpoint=error, measurement=0 → PID sees `error`
            }
            else
            {
                posPid.Reset();
                targetOmega = Soften * Math.Sqrt(2.0 * maxAlpha * (Math.Abs(errorRad) - effLD)) * Math.Sign(errorRad);
                if (targetOmega > maxOmega) targetOmega = maxOmega;
                else if (targetOmega < -maxOmega) targetOmega = -maxOmega;
            }
            r.TargetOmega = targetOmega;

            velPid.Kp = VelKp; velPid.Ti = 0.0; velPid.Td = 0.0; velPid.N = 1.0;
            velPid.B = 1.0; velPid.C = 1.0; velPid.Ts = dt;
            velPid.MinOutput = -maxAlpha; velPid.MaxOutput = maxAlpha;
            double targetAlpha = velPid.Update(targetOmega, omegaRad);
            r.TargetAlpha = targetAlpha;

            double act = -(moi * targetAlpha) / controlTorqueNm;
            if (double.IsNaN(act) || Math.Abs(act) < Eps) act = 0.0;
            if (act > 1.0) act = 1.0; else if (act < -1.0) act = -1.0;
            r.Actuation = act;
            return r;
        }

        // Total pointing error (rad), roll excluded — MechJeb's distance = acos(cos(pitch)·cos(yaw)).
        public static double PointingDistanceRad(double pitchErrRad, double yawErrRad)
        {
            double c = Math.Cos(pitchErrRad) * Math.Cos(yawErrRad);
            if (c > 1.0) c = 1.0; else if (c < -1.0) c = -1.0;
            return Math.Acos(c);
        }
    }
}
