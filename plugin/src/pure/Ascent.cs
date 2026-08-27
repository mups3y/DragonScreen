// DragonScreen — Ascent  (autopilot rebuild L3: first-stage ascent guidance + the ascent FSM)
// ============================================================================================
// The first stage flies a PITCH-PROGRAMMED zero-AoA gravity turn (load relief), following the REAL
// Crew Dragon pitch-vs-speed profile derived from DM-1 telemetry (data/dm1_ascent_template.json). The
// commanded flight-path pitch tracks that curve, so the emergent trajectory matches the real flight;
// staying on the velocity vector keeps the aero side-loads ~0. Throttle is full with the L2 max-Q
// bucket + crew g-limit overlaid (ControlLaw.ThrottleLimit). At the staging energy it commands MECO;
// after the ~8 s coast the SECOND stage flies CLOSED-LOOP UPFG (pure/Upfg.cs) to the insertion target —
// this FSM manages the phases and the throttle, UPFG owns the S2 steering + the SECO decision.
// Sources: LAUNCH_AND_ASCENT_RESEARCH.md §4–6, the DM-1 profile, §5.0 vehicle numbers.
// ============================================================================================
using System;

namespace DragonScreen
{
    public enum AscentPhase : byte { Idle, VerticalRise, GravityTurn, Meco, Coast, S2Burn, Seco, Done }

    public struct AscentInputs
    {
        public bool Valid;
        public double AltitudeM;
        public double SurfaceSpeedMps;      // surface-relative speed (the pitch program keys on it)
        public double ApoapsisM;
        public double TargetApoapsisM;      // orbit target radius-altitude (runaway backstop)
        public double DynamicPressurePa;    // measured q (max-Q bucket)
        public double MassKg;
        public double FullThrustN;
        public double GLimitG;              // crew axial-g cap for this stage
        public bool   SecondStage;          // the MVac is lit (S1 gone)
    }

    public struct AscentCommand
    {
        public AscentPhase Phase;           // fed back next tick (stateless FSM)
        public double PitchDeg;             // commanded flight-path pitch, 90 = straight up (S1 only)
        public double Throttle;             // final commanded throttle (bucket + g-limit already applied)
        public bool Stage;                  // command staging THIS tick
        public bool Cutoff;                 // engines to zero (MECO / SECO)
    }

    public static class Ascent
    {
        // ---- the S1 pitch-vs-surface-speed program (commanded flight-path angle from horizon, degrees) ----
        // ⛔ MATCHES THE REAL DM-1 / Crew-2 ASCENT (data/dm1_ascent_template.json) and is FULLY ADJUSTABLE like
        // MechJeb's ascent (user 2026-08-27). Vertical to TurnStartVMps (the pitch kick), then a shaped gravity
        // turn to FinalPitchDeg by TurnEndVMps:
        //     pitch = 90 − (90 − FinalPitch)·frac^TurnShape ,  frac = (v − TurnStartV)/(TurnEndV − TurnStartV)
        // This is MechJeb's turnShapeExponent model. TurnShape≈0.6 reproduces the DM-1 telemetry within ~2°
        // (79° plateau near 235 m/s → 47° at the 1881 m/s staging speed). Tune TurnStartVMps (when to start the
        // turn), TurnEndVMps + FinalPitchDeg (how far over at staging), and TurnShape (how fast it turns).
        [Tunable] public static double TurnStartVMps = 55.0;    // hold vertical below this (clear the tower, then kick)
        [Tunable] public static double TurnEndVMps   = 1881.0;  // reach FinalPitchDeg by this surface speed (DM-1 MECO)
        // ⛔ FLATTENED 47→30 / shape 0.6→0.5 (user 2026-08-27, flight 090123). The 47° final pitch OVER-LOFTED:
        // MECO came at fpa 51° (velocity STEEPER than the nose), the trajectory arced to a 228 km apoapsis with
        // only 3355 m/s, then the S2 burned while DESCENDING and ran dry SUBORBITAL (Pe −720 km). A shallower
        // final pitch + a faster early turn (lower shape) build horizontal velocity sooner and hold apoapsis
        // near the 200 km target, so the S2 spends its Δv on orbital (horizontal) velocity, not on climbing.
        [Tunable] public static double FinalPitchDeg = 30.0;    // flight-path pitch at the end of the turn (was 47°, lofted)
        [Tunable] public static double TurnShape     = 0.5;     // MechJeb turnShapeExponent — lower = pitch over sooner

        public const double KickSpeedMps = 55.0;      // vertical-rise → gravity-turn phase threshold (= TurnStartVMps)
        [Tunable] public static double MecoSurfaceSpeedMps = 1900.0;   // DM-1 staging energy (couples to booster reserve)
        public const double ApoapsisRunawayFactor = 1.5;               // safety: cut if apoapsis runs away

        // max-Q bucket shape (Pa) — hold q under the ceiling through the transonic region.
        public const double QSoftPa = 20000.0, QLimitPa = 35000.0, QBucketFloor = 0.7;

        public static double PitchAtSpeed(double v)
        {
            if (v <= TurnStartVMps) return 90.0;
            if (v >= TurnEndVMps) return FinalPitchDeg;
            double frac = (v - TurnStartVMps) / (TurnEndVMps - TurnStartVMps);
            if (frac < 0.0) frac = 0.0; else if (frac > 1.0) frac = 1.0;
            double shaped = Math.Pow(frac, TurnShape);
            double p = 90.0 - (90.0 - FinalPitchDeg) * shaped;
            return p < 0.0 ? 0.0 : (p > 90.0 ? 90.0 : p);
        }

        static double Throttle(AscentInputs s, double baseT)
        {
            return ControlLaw.ThrottleLimit(baseT, s.DynamicPressurePa, QSoftPa, QLimitPa, QBucketFloor,
                                            s.GLimitG, s.MassKg, s.FullThrustN);
        }

        public static AscentCommand Guide(AscentInputs s, AscentPhase phase)
        {
            AscentCommand c = new AscentCommand();
            c.Phase = phase; c.PitchDeg = 90.0; c.Throttle = 0.0;

            if (!s.Valid) { c.Phase = AscentPhase.Idle; return c; }

            // ---- runaway backstop: if the apoapsis has blown past the target while still climbing, cut. ----
            if ((phase == AscentPhase.GravityTurn || phase == AscentPhase.S2Burn)
                && s.TargetApoapsisM > 0.0 && s.ApoapsisM > s.TargetApoapsisM * ApoapsisRunawayFactor)
            { c.Phase = AscentPhase.Done; c.Cutoff = true; c.Throttle = 0.0; return c; }

            switch (phase)
            {
                case AscentPhase.Idle:
                case AscentPhase.VerticalRise:
                    c.Phase = AscentPhase.VerticalRise;
                    c.PitchDeg = 90.0;
                    c.Throttle = Throttle(s, 1.0);
                    if (s.SurfaceSpeedMps > KickSpeedMps) c.Phase = AscentPhase.GravityTurn;
                    break;

                case AscentPhase.GravityTurn:
                    c.Phase = AscentPhase.GravityTurn;
                    c.PitchDeg = PitchAtSpeed(s.SurfaceSpeedMps);
                    c.Throttle = Throttle(s, 1.0);
                    if (s.SurfaceSpeedMps >= MecoSurfaceSpeedMps) c.Phase = AscentPhase.Meco;
                    break;

                case AscentPhase.Meco:
                    // cut the engines, hold the separation attitude, command staging.
                    c.Phase = AscentPhase.Coast;
                    c.PitchDeg = PitchAtSpeed(MecoSurfaceSpeedMps);
                    c.Throttle = 0.0; c.Cutoff = true; c.Stage = true;
                    break;

                case AscentPhase.Coast:
                    c.Phase = AscentPhase.Coast;
                    c.Throttle = 0.0;
                    if (s.SecondStage) c.Phase = AscentPhase.S2Burn;   // MVac lit → UPFG takes over
                    break;

                case AscentPhase.S2Burn:
                    // UPFG owns the S2 pitch + the SECO call; here we only meter the throttle (g-limit near cutoff).
                    c.Phase = AscentPhase.S2Burn;
                    c.Throttle = Throttle(s, 1.0);
                    break;

                case AscentPhase.Seco:
                    c.Phase = AscentPhase.Done; c.Throttle = 0.0; c.Cutoff = true;
                    break;

                default:
                    c.Phase = AscentPhase.Done; c.Throttle = 0.0;
                    break;
            }
            return c;
        }
    }
}
