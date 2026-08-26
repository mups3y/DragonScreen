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
        // ---- the real DM-1 pitch-vs-surface-speed program (flight-path angle, degrees) ----
        // Vertical to the pitch-kick speed, then the measured turn 79°→47° through to the staging speed.
        static readonly double[] Sp = { 0,   55,  60,  235, 300, 430, 630, 880, 1180, 1530, 1881 };
        static readonly double[] Pd = { 90,  90,  79,  79,  75,  73,  68,  63,  57,   51,   47   };

        public const double KickSpeedMps = 55.0;      // clear the tower, then start the turn
        [Tunable] public static double MecoSurfaceSpeedMps = 1900.0;   // DM-1 staging energy (couples to booster reserve)
        public const double ApoapsisRunawayFactor = 1.5;               // safety: cut if apoapsis runs away

        // max-Q bucket shape (Pa) — hold q under the ceiling through the transonic region.
        public const double QSoftPa = 20000.0, QLimitPa = 35000.0, QBucketFloor = 0.7;

        public static double PitchAtSpeed(double v)
        {
            if (v <= Sp[0]) return Pd[0];
            int n = Sp.Length;
            if (v >= Sp[n - 1]) return Pd[n - 1];
            for (int i = 1; i < n; i++)
                if (v <= Sp[i])
                {
                    double f = (v - Sp[i - 1]) / (Sp[i] - Sp[i - 1]);
                    return Pd[i - 1] + (Pd[i] - Pd[i - 1]) * f;
                }
            return Pd[n - 1];
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
