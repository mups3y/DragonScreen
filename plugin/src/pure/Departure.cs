// DragonScreen — Departure  (autopilot rebuild L3 return: the undock + departure-burn FSM, Phase 5)
// ============================================================================================
// The real Crew Dragon return begins with the MIRROR of the approach (PHASE_5_UNDOCKING_DEPARTURE_
// RESEARCH): on GO for undock the hooks retract and TWO tiny separation burns push Dragon straight off
// the port; then FOUR autonomous departure burns (Burn 0 up-and-over out of the keep-out sphere, Burns
// 1-3 walking down to a stable co-elliptic point ~10 km BELOW and behind the ISS), then a departure
// PHASING burn (Hohmann-class lower) that lines the ground track up with the splashdown zone for the
// chosen deorbit opportunity. Each hop is a CW two-impulse solve (pure/Cw.cs) to an OFFSET aim so a
// missed burn drifts CLEAR of the 200 m KOS (corridor-safe, the passive-abort rule). The phasing burn is
// a Hohmann apsis-lower (pure/Hohmann.cs). All on the 16 Dracos.
//
// ⛔ FULL CONTROL AT ALL TIMES (user). Guide() ALWAYS returns a definite unit AimLvlh — never floating.
// No reaction wheels: the Dracos SHARE rotation and translation, so the glue does ATTITUDE FIRST (rotate
// onto the burn vector and HOLD), THEN translate — `AttitudeReady` gates every burn. The nose shroud is
// OPEN for the whole rendezvous+departure (forward Dracos exposed) and only CLOSED again before entry.
// ============================================================================================
using System;

namespace DragonScreen
{
    public enum DepPhase : byte { Idle, Undock, Depart0, Depart1, Depart2, Depart3, Phasing, Departed }

    public struct DepartureInputs
    {
        public bool Valid;
        public LvlhState Rel;          // Dragon relative to the station, LVLH (x radial, y along-track, z cross)
        public double N;               // station mean motion (rad/s)
        public bool AttitudeReady;     // pointed on the commanded burn vector and holding
        public bool AllNominal;        // GO / systems-nominal gate (aim-to-miss until true)
        public double KosRadiusM;      // 200 — the keep-out sphere the departure path must clear

        // the co-elliptic point the 4 departure burns walk out to (mirror of the rendezvous start).
        public double CoEllipticBelowM;   // ~10 000 (stable height BELOW the ISS)
        public double CoEllipticBehindM;  // ~20 000 (behind)

        // departure phasing burn — lower the orbit to sync the ground track with the splashdown zone.
        public double OrbitRadiusM;       // current orbital radius r1 (R + alt)
        public double PhasingLowerM;       // how much to lower it (r1 → r1 − this) for the phasing target
        public double Mu;
    }

    public struct DepartureCommand
    {
        public DepPhase Phase;
        public Vec3 AimLvlh;           // ALWAYS a unit vector — the burn axis / hold attitude
        public Vec3 BurnLvlh;          // the departure Δv to apply, LVLH frame (glue rotates then fires)
        public double BurnDvMps;
        public bool Burn;              // a burn is commanded (fired only once AttitudeReady)
        public double TofS;            // transfer time to the aim
        public bool Departed;          // reached the stable co-elliptic point + phased → coast to deorbit
    }

    public static class Departure
    {
        // the two separation burns push straight OFF the port (−along, away from the docking axis) to a
        // safe standoff before the first CW hop; small, gentle, spring-plus-Draco.
        [Tunable] public static double SepStandoffM = 40.0;     // back off to here before Burn 0
        [Tunable] public static double SepDvMps = 0.2;          // each tiny separation push
        [Tunable] public static double TofFrac = 0.25;          // hop transfer time as a fraction of the period
        public const double ArriveTolFrac = 0.15;               // "reached the aim" tolerance = frac of the hop distance

        static Vec3 Unit(Vec3 v, Vec3 fallback)
        {
            return v.Magnitude > 1e-6 ? v.Normalized : fallback;
        }

        // The OFFSET aim point (LVLH) for each departure hop — progressively OUT of the corridor and DOWN,
        // ending co-elliptic ~10 km below / 20 km behind. Every intermediate point sits OUTSIDE the KOS.
        static void AimPoint(DepPhase ph, DepartureInputs s, out double xf, out double yf, out double zf)
        {
            double below = s.CoEllipticBelowM, behind = s.CoEllipticBehindM;
            xf = 0; yf = 0; zf = 0;
            switch (ph)
            {
                case DepPhase.Depart0:                                   // up-and-over: clear the KOS behind
                    xf = 0.05 * below; yf = -Math.Max(behind * 0.05, s.KosRadiusM * 2.0); break;
                case DepPhase.Depart1:                                   // start dropping below and behind
                    xf = -0.15 * below; yf = -0.20 * behind; break;
                case DepPhase.Depart2:
                    xf = -0.50 * below; yf = -0.55 * behind; break;
                case DepPhase.Depart3:                                   // the stable co-elliptic point
                    xf = -below; yf = -behind; break;
            }
        }

        static double DistTo(LvlhState r, double xf, double yf, double zf)
        {
            double dx = r.Rx - xf, dy = r.Ry - yf, dz = r.Rz - zf;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        static DepPhase Next(DepPhase p)
        {
            switch (p)
            {
                case DepPhase.Depart0: return DepPhase.Depart1;
                case DepPhase.Depart1: return DepPhase.Depart2;
                case DepPhase.Depart2: return DepPhase.Depart3;
                default: return DepPhase.Phasing;
            }
        }

        public static DepartureCommand Guide(DepartureInputs s, DepPhase phase)
        {
            DepartureCommand c = new DepartureCommand();
            c.Phase = phase;
            // default hold attitude = AWAY from the station (+relative position) — we are leaving, and the
            // capsule always points somewhere definite (full control).
            c.AimLvlh = Unit(new Vec3(s.Rel.Rx, s.Rel.Ry, s.Rel.Rz), new Vec3(0, -1, 0));

            if (!s.Valid) { c.Phase = DepPhase.Idle; return c; }

            if (phase == DepPhase.Idle) phase = DepPhase.Undock;

            // ---- Undock: two tiny separation burns straight off the port, to the sep standoff ----
            if (phase == DepPhase.Undock)
            {
                c.Phase = DepPhase.Undock;
                // push AWAY from the station (+relative position, off the docking axis); hold that attitude.
                Vec3 off = Unit(new Vec3(s.Rel.Rx, s.Rel.Ry, s.Rel.Rz), new Vec3(0, -1, 0));
                c.AimLvlh = off;
                c.BurnLvlh = off * SepDvMps;
                c.BurnDvMps = SepDvMps;
                c.Burn = true;                                    // the sep push always fires (crew already GO)
                if (s.Rel.RangeM >= SepStandoffM) return Guide(s, DepPhase.Depart0);   // clear of the port → first hop
                return c;
            }

            // ---- Phasing: Hohmann-class apsis-lower to set the ground track for the splashdown zone ----
            if (phase == DepPhase.Phasing)
            {
                c.Phase = DepPhase.Phasing;
                // retrograde (−along-track / −V-bar) lowers the orbit; magnitude from the Hohmann first burn.
                double r1 = s.OrbitRadiusM, r2 = s.OrbitRadiusM - s.PhasingLowerM;
                double dv = (r2 > 0 && s.Mu > 0 && s.PhasingLowerM > 0) ? Math.Abs(Hohmann.Dv1(r1, r2, s.Mu)) : 0.0;
                c.AimLvlh = new Vec3(0, -1, 0);                   // retrograde in LVLH (−along-track)
                c.BurnLvlh = new Vec3(0, -dv, 0);
                c.BurnDvMps = dv;
                c.Burn = dv > 0.01 && s.AllNominal;
                c.TofS = 0.0;
                if (dv <= 0.01) { c.Phase = DepPhase.Departed; c.Departed = true; }
                return c;
            }

            if (phase == DepPhase.Departed)
            {
                c.Phase = DepPhase.Departed; c.Departed = true;
                c.AimLvlh = new Vec3(0, -1, 0);                   // hold retrograde-ish, coasting to deorbit
                return c;
            }

            // ---- Depart0..3: CW two-impulse to the next OFFSET aim (corridor-safe) ----
            double xf, yf, zf; AimPoint(phase, s, out xf, out yf, out zf);
            double hopDist = DistTo(s.Rel, xf, yf, zf);
            double period = s.N > 0 ? 2.0 * Math.PI / s.N : 0.0;
            double tof = period * TofFrac; if (tof < 60.0) tof = 60.0;

            // reached this hop's aim? advance to the next.
            double startDist = Math.Sqrt(xf * xf + yf * yf + zf * zf);   // aim distance from the port (scale)
            if (hopDist <= Math.Max(ArriveTolFrac * startDist, 5.0)) { c.Phase = Next(phase); phase = c.Phase; }
            if (phase == DepPhase.Phasing || phase == DepPhase.Departed) return Guide(s, phase);

            AimPoint(phase, s, out xf, out yf, out zf);
            CwSolution sol = Cw.TwoImpulse(s.Rel.Rx, s.Rel.Ry, s.Rel.Rz, s.Rel.Vx, s.Rel.Vy, s.Rel.Vz,
                                           xf, yf, zf, s.N, tof);
            c.Phase = phase;
            if (!sol.Ok) return c;

            Vec3 burn = new Vec3(sol.Dvx1, sol.Dvy1, sol.Dvz1);
            c.BurnLvlh = burn;
            c.BurnDvMps = burn.Magnitude;
            c.TofS = tof;
            c.AimLvlh = Unit(burn, c.AimLvlh);                    // point along the burn (attitude first)
            c.Burn = c.BurnDvMps > 0.01 && s.AllNominal;
            return c;
        }
    }
}
