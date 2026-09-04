// DragonScreen — BoosterSteer  (§B16 booster recovery: the STEERING LAW — register W24)
// ============================================================================================
// `pure/BoosterDescent.Guide()` returns a definite unit `AimForward` every tick, in every phase, and
// `src/BoosterHost.cs` reported it but executed NONE of it — `s.pitch`/`s.yaw`/`s.roll` were never
// written. This file is the missing loop: it turns "where to point" (AimForward, as an attitude ERROR
// the glue has already resolved into per-axis degrees) into a bounded, normalised FlightCtrlState-ready
// command. Nothing else in the tree does this; R1 (`docs/AUTOPILOT_RECOVERY_AUDIT.md`) §3.2 files the
// only prior attempt — `AttitudePilot.cs` / `AttitudeController.cs` / `pure/AttitudeLoop.cs` — as
// ⛔ RECOVER-REFERENCE ONLY, never live code (owner directive). NO BYTE of those three files is here.
//
// ============================================================================================
// ⛔ THE FAILURE THIS LAW IS DESIGNED AGAINST — read `docs/FLIGHT_CORPUS_ASSESSMENT.md` §3 before
// touching a gain. The inherited diagnosis ("a limit cycle") is WRONG for the ascent failure that
// actually happened:
//   • §3.1 — the S2 tumble is a DIVERGENCE, not an oscillation: body rates reverse only 2-3 times while
//     `rate_yaw_dps` climbs MONOTONICALLY to 90 dps. The commanded rate (`rate_cmd_rads`) reached
//     3.41-7.61 rad/s against a MEASURED rate that never exceeded 1.18 rad/s — the loop asked for a rate
//     the stage could never achieve, throughout the whole burn.
//   • The cause was a LIVE AUTHORITY ESTIMATE gone physically impossible: `angacc_pitch_auth` read
//     37-39 rad/s^2 where the same metric, post-fix, reads 0.43 — a ~90x error, and the deleted law
//     multiplied its rate command by that estimate.
//   • §3.2 — the limit cycle the folklore blamed for the failure is real, but it lives somewhere else
//     entirely: the TERMINAL RENDEZVOUS actuation (81-89% duty, ~1 reversal/s at a 3.0-3.8 degree
//     pointing error) — a DIFFERENT vehicle regime (Dragon RCS hold, not booster gimbal/fin steering).
//
// ⇒ THE STRUCTURAL FIX: this law NEVER converts an angle error into a rate command via a live authority
// estimate. `MaxRateDegPerS` is a FIXED CONSTANT ceiling — the outer (angle -> desired-rate) stage cannot
// demand a rate faster than this NO MATTER HOW LARGE THE ERROR IS. A wrong or absent authority estimate
// therefore cannot reproduce the divergence: the worst case is "converges slower than it could", never
// "asks for 90 dps and gets it wrong". This is a difference in STRUCTURE, not merely in tuning.
//
// ============================================================================================
// RULINGS THIS FILE IMPLEMENTS (owner, via the overseer, 2026-09-04 — `docs/BOOSTER_STEERING_MOD_SEARCH.md`)
//   Q1 — OURS, METHOD BORROWED, NO DEPENDENCY. TCA's T-SAS idea ("close the loop on the THRUST AXIS, not
//        the nose") is already this tree's convention (`BoosterDescent`'s own header: "AimForward is the
//        direction thrust pushes the vehicle"). Taking the CONCEPT costs no dependency and is the same
//        move `pure/ThrustBalance.cs` already made with TCA's `EngineOptimizer` — precedent, not novelty.
//   Q2 — A MARKED, [UN-CONVERGED], DEFAULT-ZERO deadband seam. `DeadbandDeg = 0.0` is BEHAVIOURALLY
//        IDENTICAL to no deadband at all — `70dc239`'s strip directive is honoured. It must NOT be seeded
//        from DS-ASC-008 (0.4 kN Dragon-RCS terminal-rendezvous evidence — R1 §7.5: "a different plant").
//        ⭐ OWNER REFINEMENT: the seam must be OBSERVABLE. `Steer()` reports, per axis, whether the
//        deadband suppressed the tick's error and at what value it ran — `BoosterHost` surfaces these as
//        read-only properties so a future BlackBox column (register BB1) can read them without this file
//        inventing a recording channel of its own.
//
// ============================================================================================
// WHAT THIS FILE DOES NOT DO (by design, not omission)
//   • It does not compute the attitude error itself. Frame conversion (world AimForward -> per-axis
//     pitch/yaw/roll degrees) needs `UnityEngine.Quaternion`/`Transform` and lives in `src/BoosterHost.cs`
//     — reusing, freshly written, the ONE piece of the deleted law R1 §3.2 names as reusable independent
//     of the gains: "current = ReferenceTransform.rotation * Euler(-90,0,0) ... yaw NEGATED". No number,
//     gain or line of `AttitudeController.cs` is copied — only the documented FORMULA.
//   • It defines no ROLL TARGET. A single `AimForward` vector cannot define roll about itself, and the
//     guidance never supplies one. The glue's frame conversion uses the vehicle's OWN current roll
//     reference as `LookRotation`'s "up" (the deleted law's documented convention too), which makes the
//     roll ERROR this file receives ~0 by construction — so the roll axis here is a pure RATE DAMPER.
//     This matches the evidence: `act_roll` never exceeded 0.09 in the corpus (§3.1) — roll was never
//     the ascent problem, and inventing a roll target here would be manufacturing a new failure mode.
//   • It never touches `pure/BoosterDescent.cs`'s AoA cap. Contract (2) there (`|AoaDeg| <= AoaCapDeg`)
//     is enforced entirely by the guidance that BUILDS `AimForward`; this law only tracks whatever
//     direction it is handed, so it cannot manufacture an angle beyond what the guidance already capped.
//   • THE PER-AXIS SIGN is UNVERIFIED. This is a fresh control law with no recorded flight of its own —
//     there is no anchor to derive `PitchSign`/`YawSign`/`RollSign` from, and the deleted law's own final
//     sign-application line is not recoverable (R1 §3.2 verdicts it code, not reference). Each defaults to
//     +1.0 and is `[Tunable]`, flippable from `PluginData/tuning.cfg` with NO recompile — see the open
//     question at the foot of `src/BoosterHost.cs`.
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct BoosterSteerInputs
    {
        /// <summary>Per-axis pointing ERROR, degrees, in the vehicle's own control frame — the glue's
        /// frame conversion, NOT computed here. Convention matches `vessel.angularVelocity`
        /// (pitch=x/roll=y/yaw=z, `VesselData.cs` T13b): positive pitch/yaw error means the axis needs a
        /// positive rotation to close it. Roll error is ~0 by construction (see header) unless the glue
        /// supplies otherwise.</summary>
        public double PitchErrDeg, YawErrDeg, RollErrDeg;

        /// <summary>Measured body rates, deg/s, SAME frame and axis convention as the errors above.</summary>
        public double PitchRateDps, YawRateDps, RollRateDps;
    }

    public struct BoosterSteerCommand
    {
        /// <summary>FlightCtrlState-ready, ALWAYS in [-1, 1], never NaN/Infinite — contract, enforced
        /// unconditionally on every axis regardless of input (see `Axis()`).</summary>
        public double Pitch, Yaw, Roll;

        /// <summary>⭐ OBSERVABILITY (the owner's Q2 refinement) — was EACH axis' error inside the
        /// deadband this tick, and what deadband VALUE was in force. A future BlackBox column reads these
        /// two fields; this file invents no recording mechanism of its own.</summary>
        public bool PitchDeadbanded, YawDeadbanded, RollDeadbanded;
        public double DeadbandDegApplied;
    }

    public static class BoosterSteer
    {
        // ---- OURS. Fresh gains — never AttitudeLoop's / MechJeb's. Every one [UN-CONVERGED] (§B16.8
        // ruling 2): there is no recorded booster attitude flight to converge them from (R1 §4.2 — the
        // booster was never recovered), and the ascent evidence (§3 above) is a DIFFERENT vehicle/plant.
        // ------------------------------------------------------------------------------------------

        /// <summary>THE STRUCTURAL FIX. The outer (angle->rate) stage can never demand more than this,
        /// no matter how large the error — the divergence in §3.1 came from an UNBOUNDED rate demand
        /// driven by a bad authority estimate; this makes that failure mode structurally unreachable
        /// rather than merely unlikely. [UN-CONVERGED] — a conservative starting ceiling, not a measured
        /// vehicle limit.</summary>
        [Tunable] public static double MaxRateDegPerS = 5.0;

        /// <summary>Angle error (deg) -> desired rate (deg/s), BEFORE the ceiling above clamps it.
        /// [UN-CONVERGED].</summary>
        [Tunable] public static double AngleToRateKp = 1.0;

        /// <summary>Rate error (desired − measured, deg/s) -> normalised command. [UN-CONVERGED].</summary>
        [Tunable] public static double RateKp = 0.15;

        /// <summary>Q2's seam. A remaining error below this is treated as ZERO before the rate law sees
        /// it — kills small-error chatter IF the booster regime ever shows it. **DEFAULT ZERO —
        /// behaviourally identical to no deadband at all**, so `70dc239`'s strip directive is honoured
        /// until a RECORDED BOOSTER flight (never a Dragon-RCS one — R1 §7.5) justifies a value.
        /// [UN-CONVERGED], [Tunable] — enable from `PluginData/tuning.cfg`, no recompile.</summary>
        [Tunable] public static double DeadbandDeg = 0.0;

        // ---- per-axis SIGN. [UN-CONVERGED] and UNVERIFIED — see the header's last bullet and the open
        // question at the foot of `src/BoosterHost.cs`. Flip exactly one from tuning.cfg if a first flight
        // shows an axis driving its own error the wrong way (positive feedback = accelerating divergence
        // in that axis alone, distinguishable from a merely-undertuned gain within a tick or two).
        [Tunable] public static double PitchSign = 1.0;
        [Tunable] public static double YawSign = 1.0;
        [Tunable] public static double RollSign = 1.0;

        /// <summary>
        /// One axis, end to end: deadband -> rate-ceiling angle-to-rate -> rate-error-to-command -> sign
        /// -> clamp. Pure, stateless (no integral term — nothing here can wind up), and DEFINED for any
        /// input: NaN/Infinity in either argument reads as zero rather than propagating.
        /// </summary>
        public static double Axis(double errDeg, double rateDps, double signMult, out bool deadbanded)
        {
            double e = Finite(errDeg);
            double r = Finite(rateDps);
            deadbanded = false;

            if (DeadbandDeg > 0.0 && Math.Abs(e) < DeadbandDeg) { e = 0.0; deadbanded = true; }

            double desiredRate = AngleToRateKp * e;
            if (desiredRate > MaxRateDegPerS) desiredRate = MaxRateDegPerS;
            else if (desiredRate < -MaxRateDegPerS) desiredRate = -MaxRateDegPerS;

            double cmd = RateKp * (desiredRate - r) * Finite(signMult);
            if (double.IsNaN(cmd) || double.IsInfinity(cmd)) return 0.0;
            if (cmd > 1.0) return 1.0;
            if (cmd < -1.0) return -1.0;
            return cmd;
        }

        /// <summary>All three axes, one call — what `src/BoosterHost.cs` calls every tick.</summary>
        public static BoosterSteerCommand Steer(BoosterSteerInputs s)
        {
            BoosterSteerCommand c = new BoosterSteerCommand();
            bool pd, yd, rd;
            c.Pitch = Axis(s.PitchErrDeg, s.PitchRateDps, PitchSign, out pd);
            c.Yaw   = Axis(s.YawErrDeg,   s.YawRateDps,   YawSign,   out yd);
            c.Roll  = Axis(s.RollErrDeg,  s.RollRateDps,  RollSign,  out rd);
            c.PitchDeadbanded = pd; c.YawDeadbanded = yd; c.RollDeadbanded = rd;
            c.DeadbandDegApplied = DeadbandDeg;
            return c;
        }

        static double Finite(double v)
        {
            return (double.IsNaN(v) || double.IsInfinity(v)) ? 0.0 : v;
        }
    }
}
