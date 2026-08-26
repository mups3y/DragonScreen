// DragonScreen — Hohmann  (autopilot rebuild L3 rendezvous: the orbit raises + phasing)
// ============================================================================================
// The big catch-up burns (Phase / Boost / Close) are Hohmann-family apsis burns; the phase-lead timing
// says WHEN to fire so the transfer arrives with the station still safely ahead. Built fresh from the
// standard vis-viva / Hohmann equations (PHASE_3_RENDEZVOUS_RESEARCH §4.3/§4b). Coarse phasing here,
// CW two-impulse (pure/Cw.cs) for the terminal legs.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class Hohmann
    {
        public static double CircularSpeed(double r, double mu)
        {
            if (r <= 0.0 || mu <= 0.0) return 0.0;
            return Math.Sqrt(mu / r);
        }

        // vis-viva: v² = μ(2/r − 1/a)
        public static double SpeedAt(double r, double a, double mu)
        {
            if (r <= 0.0 || a <= 0.0 || mu <= 0.0) return 0.0;
            double v2 = mu * (2.0 / r - 1.0 / a);
            return v2 > 0.0 ? Math.Sqrt(v2) : 0.0;
        }

        public static double TransferSma(double r1, double r2) { return 0.5 * (r1 + r2); }

        // First burn at r1 to raise/lower the opposite apsis to r2 (prograde +, retrograde −).
        public static double Dv1(double r1, double r2, double mu)
        {
            double at = TransferSma(r1, r2);
            return SpeedAt(r1, at, mu) - CircularSpeed(r1, mu);
        }
        // Second burn at r2 to circularise from the transfer ellipse.
        public static double Dv2(double r1, double r2, double mu)
        {
            double at = TransferSma(r1, r2);
            return CircularSpeed(r2, mu) - SpeedAt(r2, at, mu);
        }
        public static double Total(double r1, double r2, double mu)
        {
            return Math.Abs(Dv1(r1, r2, mu)) + Math.Abs(Dv2(r1, r2, mu));
        }

        public static double TransferTimeS(double r1, double r2, double mu)
        {
            double at = TransferSma(r1, r2);
            if (at <= 0.0 || mu <= 0.0) return 0.0;
            return Math.PI * Math.Sqrt(at * at * at / mu);
        }

        // Phase-lead: the target sweeps ω₂·t_H during the transfer, so fire when the target is AHEAD by
        // φ = π − ω₂·t_H (normalised to [0,2π)). The chaser (lower/faster) closes the phase at ω₁−ω₂.
        public static double PhaseLeadRad(double r1, double r2, double mu)
        {
            double tH = TransferTimeS(r1, r2, mu);
            double w2 = Math.Sqrt(mu / (r2 * r2 * r2));
            double phi = Math.PI - w2 * tH;
            phi = phi % (2.0 * Math.PI);
            if (phi < 0.0) phi += 2.0 * Math.PI;
            return phi;
        }

        // Wait time until the phase angle (target-ahead-of-chaser) reaches the lead angle.
        public static double WaitTimeS(double phaseNowRad, double phaseLeadRad, double omega1, double omega2)
        {
            double rel = omega1 - omega2;   // closing rate of the phase angle (chaser lower → faster → rel>0)
            if (Math.Abs(rel) < 1e-12) return 0.0;
            double dphi = (phaseNowRad - phaseLeadRad) % (2.0 * Math.PI);
            if (rel > 0.0) { while (dphi < 0.0) dphi += 2.0 * Math.PI; }
            else { while (dphi > 0.0) dphi -= 2.0 * Math.PI; }
            return dphi / rel;
        }
    }
}
