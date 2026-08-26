// DragonScreen — DeorbitGuidance  (autopilot rebuild L3 return: trunk jettison + the deorbit burn)
// ============================================================================================
// The de-orbit sequence (PHASE_6_DEORBIT_ENTRY_SPLASHDOWN_RESEARCH §1/§5b): shed the TRUNK first (no
// heat shield, burns up — done BEFORE the burn to save propellant), then a LONG low-thrust retrograde
// Draco burn (~12–16.5 min; Crew-1 Resilience 987 s) that lowers periapsis to the entry-interface
// radius (R + ~120 km) targeting the corridor for the splashdown zone, then orient HEAT-SHIELD-FORWARD
// for entry. The Δv is the Hohmann first-burn magnitude lowering the apsis to r_p = R + h_EI:
//     Δv = √(μ/r_c) − √( 2μ·r_p / ( r_c·(r_c + r_p) ) )      (retrograde at r_c)  == |Hohmann.Dv1|.
// The burn is CLOSED-LOOP on the MEASURED periapsis (cut when Pe ≤ target), with the planned Δv as a
// backstop — not an open-loop clock.
//
// ⛔ FULL CONTROL: Guide() ALWAYS returns a definite unit AimForward — retrograde while burning, and
// heat-shield-forward (into the oncoming flow) once the burn is complete. Named DeorbitGuidance because
// the kept Terminal screen already owns the `Deorbit` stub (a landing-throttle readout).
// ============================================================================================
using System;

namespace DragonScreen
{
    public enum DeorbitPhase : byte { Idle, TrunkJettison, Settle, Burn, Complete, OrientEntry }

    public struct DeorbitInputs
    {
        public bool Valid;
        public Vec3 Velocity;          // orbital velocity, world frame (retrograde = −this)
        public Vec3 Up;                // local radial-up, world frame (fallback aim)
        public double PeriapsisAltM;   // MEASURED current periapsis altitude (closed-loop cutoff)
        public double EntryInterfaceAltM;  // target periapsis = entry interface (~120 000)
        public double DvAppliedMps;    // cumulative Δv delivered so far this burn (backstop cutoff)
        public bool TrunkAttached;     // true until the trunk is jettisoned
        public bool AttitudeReady;     // pointed retrograde and holding (Dracos: attitude before thrust)
        public bool AllNominal;        // GO for the burn
        public double SettleS;         // ullage/settle dwell after trunk sep before the burn (glue clock)
        public double SettleElapsedS;
    }

    public struct DeorbitCommand
    {
        public DeorbitPhase Phase;
        public Vec3 AimForward;        // ALWAYS unit — retrograde (burn) or shield-forward (post-burn)
        public double Throttle;        // 0..1 on the Dracos (long low-thrust burn = full open)
        public bool JettisonTrunk;     // pulse the trunk decoupler this tick
        public bool Burning;
        public bool Complete;          // periapsis is on the entry corridor → hand to Entry
    }

    public static class DeorbitGuidance
    {
        // Retrograde Δv to lower periapsis from the circular radius r_c to the entry-interface radius r_p.
        public static double DeorbitDvMps(double orbitRadiusM, double entryInterfaceRadiusM, double mu)
        {
            if (orbitRadiusM <= 0 || entryInterfaceRadiusM <= 0 || mu <= 0) return 0.0;
            if (entryInterfaceRadiusM >= orbitRadiusM) return 0.0;
            return Math.Abs(Hohmann.Dv1(orbitRadiusM, entryInterfaceRadiusM, mu));
        }

        static Vec3 Retro(Vec3 v, Vec3 up)
        {
            return v.Magnitude > 1.0 ? (-v).Normalized : up.Normalized;   // never undefined
        }
        static Vec3 ShieldForward(Vec3 v, Vec3 up)
        {
            // heat shield into the oncoming flow: the shield normal points ALONG the velocity (into the air).
            return v.Magnitude > 1.0 ? v.Normalized : up.Normalized;
        }

        public static DeorbitCommand Guide(DeorbitInputs s, DeorbitPhase phase)
        {
            DeorbitCommand c = new DeorbitCommand();
            c.Phase = phase;
            c.AimForward = s.Valid ? Retro(s.Velocity, s.Up) : new Vec3(0, 0, 1);
            c.Throttle = 0.0;

            if (!s.Valid) { c.Phase = DeorbitPhase.Idle; return c; }

            bool peReached = s.PeriapsisAltM <= s.EntryInterfaceAltM;

            if (phase == DeorbitPhase.Idle) phase = DeorbitPhase.TrunkJettison;

            switch (phase)
            {
                case DeorbitPhase.TrunkJettison:
                    // Trunk goes FIRST (mass save; it has no shield and burns up). Point retrograde, drop it.
                    c.Phase = DeorbitPhase.TrunkJettison;
                    c.AimForward = Retro(s.Velocity, s.Up);
                    if (s.TrunkAttached) { c.JettisonTrunk = true; }
                    else c.Phase = DeorbitPhase.Settle;
                    break;

                case DeorbitPhase.Settle:
                    // brief dwell to let the trunk clear and the props settle before ignition (attitude held).
                    c.Phase = DeorbitPhase.Settle;
                    c.AimForward = Retro(s.Velocity, s.Up);
                    if (s.SettleElapsedS >= s.SettleS) c.Phase = DeorbitPhase.Burn;
                    break;

                case DeorbitPhase.Burn:
                    // long low-thrust retrograde Draco burn, held retrograde; cut on MEASURED periapsis.
                    if (peReached) return Guide(s, DeorbitPhase.OrientEntry);   // Pe on the corridor → done
                    c.Phase = DeorbitPhase.Burn;
                    c.AimForward = Retro(s.Velocity, s.Up);
                    c.Throttle = (s.AttitudeReady && s.AllNominal) ? 1.0 : 0.0;
                    c.Burning = c.Throttle > 0.0;
                    break;

                case DeorbitPhase.Complete:
                case DeorbitPhase.OrientEntry:
                    // periapsis on the corridor: swing to HEAT-SHIELD-FORWARD and hold for Entry Interface.
                    c.Phase = DeorbitPhase.OrientEntry;
                    c.AimForward = ShieldForward(s.Velocity, s.Up);
                    c.Complete = true;
                    break;
            }
            return c;
        }
    }
}
