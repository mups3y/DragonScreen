// DragonScreen — Rendezvous  (autopilot rebuild L3: the named-burn rendezvous FSM)
// ============================================================================================
// The real Crew Dragon co-elliptic rendezvous as a NAMED-BURN sequence (PHASE_3 + telemetry DB
// data/crew_missions.json): Phase → Boost → Close → Transfer → Coelliptic → 30 km rendezvous-complete →
// Approach Initiation (AI, from 7.5 km — the DB burn 90 s / 0.72 m/s) → Midcourse → hand to the docking
// L-approach at the corridor. Coarse raises are Hohmann-family (pure/Hohmann.cs); the terminal legs are
// CW two-impulse transfers (pure/Cw.cs) to OFFSET aim points so a missed burn drifts clear of the KOS.
//
// ⛔ FULL CONTROL AT ALL TIMES (user). Guide() ALWAYS returns a definite unit AimLvlh — the capsule is
// never floating aimlessly. All Dragon manoeuvres are on the 16 Dracos (no reaction wheels), which SHARE
// rotation and translation, so the glue does ATTITUDE FIRST — rotate onto the burn vector and HOLD it —
// THEN translate (fire); it never rotates and translates at once (that over-subscribes the Dracos and
// drifts off-axis). `AttitudeReady` gates the burn: translate only once pointed.
// ============================================================================================
using System;

namespace DragonScreen
{
    public enum RvPhase : byte { Idle, Phasing, CoElliptic, ApproachInit, Midcourse, Arrived }

    public struct RendezvousInputs
    {
        public bool Valid;
        public LvlhState Rel;          // current relative state in the station LVLH frame
        public double N;               // target mean motion
        public bool AttitudeReady;     // the capsule is pointed on the commanded burn vector and holding
        public bool AllNominal;        // GO / aim-to-miss gate

        // targets (from the DB / research; the profile supplies them)
        public double CoEllipticBelowM;  // ~10 000 (stable co-elliptic height below the station)
        public double CoEllipticBehindM; // ~20 000
        public double AiRangeM;          // 7 500 (Approach Initiation standoff)
        public double CorridorRangeM;    // ~2 000 (hand to docking)
    }

    public struct RendezvousCommand
    {
        public RvPhase Phase;
        public Vec3 AimLvlh;           // ALWAYS a unit vector — the direction to point (burn axis / V-bar hold)
        public Vec3 BurnLvlh;          // the departure Δv to apply, LVLH frame (glue rotates then fires)
        public double BurnDvMps;
        public bool Burn;              // a burn is commanded this leg (only fired once AttitudeReady)
        public double TofS;            // transfer time to the aim
    }

    public static class Rendezvous
    {
        public const double RendezvousCompleteM = 30000.0;
        [Tunable] public static double TerminalTofFrac = 0.25;   // transfer time as a fraction of the period
        // ⛔ CW VALIDITY LIMIT. Clohessy-Wiltshire is a linearisation about the target — beyond this range it
        // is meaningless and its two-impulse inverse explodes (13,000 km → 28 km/s, flight 214827). Past it,
        // Guide REFUSES to compute a CW burn and just holds attitude; the far field is flown by pure/Phasing
        // (prograde co-elliptic raise) in the glue. Set well above the near-field regime so the terminal legs
        // (tens of km) are unaffected — this is a defence-in-depth guard, not the primary far/near split.
        [Tunable] public static double CwMaxRangeM = 200000.0;   // 200 km

        static Vec3 Unit(Vec3 v, Vec3 fallback)
        {
            return v.Magnitude > 1e-6 ? v.Normalized : fallback;
        }

        // The offset aim point (LVLH) for the current phase — never the station itself.
        static void AimPoint(RvPhase ph, RendezvousInputs s, out double xf, out double yf, out double zf)
        {
            xf = 0; yf = 0; zf = 0;
            switch (ph)
            {
                case RvPhase.Phasing:
                case RvPhase.CoElliptic:
                    xf = -s.CoEllipticBelowM; yf = -s.CoEllipticBehindM; break;   // 10 km below, 20 km behind
                case RvPhase.ApproachInit:
                    xf = -s.AiRangeM * 0.3; yf = -s.AiRangeM; break;              // ~7.5 km behind & below
                case RvPhase.Midcourse:
                    xf = -s.CorridorRangeM * 0.2; yf = -s.CorridorRangeM; break;  // ~2 km corridor (offset)
            }
        }

        public static RendezvousCommand Guide(RendezvousInputs s, RvPhase phase)
        {
            RendezvousCommand c = new RendezvousCommand();
            c.Phase = phase;
            // default hold attitude = along-track (V-bar), so the capsule is ALWAYS pointed, never drifting.
            c.AimLvlh = new Vec3(0, s.Rel.Ry >= 0 ? -1 : 1, 0);
            c.AimLvlh = Unit(c.AimLvlh, new Vec3(0, -1, 0));

            if (!s.Valid) { c.Phase = RvPhase.Idle; return c; }

            double range = s.Rel.RangeM;

            // phase progression on MEASURED range (the named-burn timing emerges from the physics).
            if (phase == RvPhase.Idle) phase = RvPhase.Phasing;
            if (phase == RvPhase.Phasing && range <= RendezvousCompleteM) phase = RvPhase.CoElliptic;
            if (phase == RvPhase.CoElliptic && range <= s.AiRangeM * 1.05) phase = RvPhase.ApproachInit;
            if (phase == RvPhase.ApproachInit && range <= s.CorridorRangeM * 1.3) phase = RvPhase.Midcourse;
            if (phase == RvPhase.Midcourse && range <= s.CorridorRangeM) phase = RvPhase.Arrived;
            c.Phase = phase;

            if (phase == RvPhase.Arrived) return c;   // hand to docking; hold V-bar attitude

            // ⛔ CW-VALIDITY GUARD: beyond the linearisation's range, do NOT run the two-impulse solve (it would
            // explode to a garbage Δv). Hold the along-track attitude, command NO burn — the glue's far-field
            // prograde co-elliptic raise (pure/Phasing) flies this regime. Cannot deorbit: no burn is emitted.
            if (range > CwMaxRangeM) { c.Burn = false; c.BurnDvMps = 0.0; return c; }

            // CW two-impulse to the phase's OFFSET aim point.
            double xf, yf, zf; AimPoint(phase, s, out xf, out yf, out zf);
            double period = s.N > 0 ? 2.0 * Math.PI / s.N : 0.0;
            double tof = period * TerminalTofFrac;
            if (tof < 60.0) tof = 60.0;

            CwSolution sol = Cw.TwoImpulse(s.Rel.Rx, s.Rel.Ry, s.Rel.Rz, s.Rel.Vx, s.Rel.Vy, s.Rel.Vz,
                                           xf, yf, zf, s.N, tof);
            if (!sol.Ok) return c;

            Vec3 burn = new Vec3(sol.Dvx1, sol.Dvy1, sol.Dvz1);
            c.BurnLvlh = burn;
            c.BurnDvMps = burn.Magnitude;
            c.TofS = tof;
            // point along the burn (full control); the burn only fires once the glue reports AttitudeReady.
            c.AimLvlh = Unit(burn, c.AimLvlh);
            c.Burn = c.BurnDvMps > 0.01 && s.AllNominal;
            return c;
        }
    }
}
