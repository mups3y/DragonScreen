// DragonScreen — Phasing  (autopilot rebuild L3 rendezvous: the FAR-FIELD phase-timed transfer + safety floor)
// ============================================================================================
// The terminal rendezvous is Clohessy-Wiltshire (pure/Cw.cs) — but CW is a LINEARISATION about the target and
// is only valid within tens of km. Right after insertion the chaser is on a LOW orbit, thousands of km away in
// along-track phase, where CW is meaningless (flight 214827: CW at 13,000 km demanded a ~28 km/s burn → the glue
// fired the Dracos retrograde → the capsule SELF-DEORBITED). THIS module is the far-field law that gets the
// chaser from insertion to within CW's regime, safely.
//
// ⛔ THE OLD "continuous co-elliptic raise" WAS WRONG (flight 103303, data-confirmed): it burned prograde
// CONTINUOUSLY to "walk both apses up", but a continuous prograde burn near periapsis only pumps APOAPSIS — it
// raised ap 200→772 km (target was ~409) while periapsis crawled, never coasted (so warp never armed) and never
// closed the half-orbit phase gap. Replaced by the PHASE-TIMED HOHMANN TRANSFER that DELIVERS the chaser into
// CW's regime — the far field does NOT try to rendezvous, it just gets the chaser within tens of km of the
// station and hands to CW (which does the co-elliptic aim / AI standoff / velocity-match / terminal approach,
// all in its valid regime — PHASE_3_RENDEZVOUS_RESEARCH §3):
//   1. PHASE  — stay on the low (fast) insertion orbit and COAST until the phase angle reaches the Hohmann lead
//               angle (Hohmann.PhaseLeadRad); the low orbit closes the along-track phase quickly, warp-compressed.
//   2. TRANSFER — at the aligned phase, burn prograde near PERIAPSIS to raise APOAPSIS to just below the station
//               altitude, then STOP (bounded — the fix for the 200→772 over-raise). One finite prograde burn.
//   3. COAST  — coast up to apoapsis, where the phase timing puts the chaser NEAR the station → the range drops
//               into CW's hand-off regime and the glue hands to CW.
// ⛔ AN EARLIER "CIRCULARIZE at apoapsis" step was REMOVED (flight 165302, data-confirmed): raising pe with the
// low-thrust Dracos took ~27 near-apoapsis orbits, during which the chaser drifted 246→6,000 km away — it never
// completed and never handed off. The transfer already brings the chaser to ~80 km at apoapsis (131412: 86 km,
// 165302: 79 km); the real fix was the CW HAND-OFF RANGE being too tight (50 km never caught the ~80 km closest
// approach) — raised to CwHandoffRangeM so CW takes over on approach and does the terminal rendezvous itself.
// A HARD PERIAPSIS FLOOR (PeSafe) gates every burn — prograde-only + the floor mean the far field can never
// deorbit. Pure + headless-tested; the Hohmann timing math lives in the (tested) pure/Hohmann.cs.
// ============================================================================================
namespace DragonScreen
{
    public enum FarPhase { Phase = 0, Transfer = 1, Coast = 2 }

    // Everything FarGuide needs, all computed by the glue from the live chaser + station orbits.
    public struct FarInputs
    {
        public double PhaseNowRad;   // current phase angle: target AHEAD of chaser, [0, 2π)
        public double PhaseLeadRad;  // required Hohmann lead angle (Hohmann.PhaseLeadRad(r1, r2, μ))
        public double Omega1;        // chaser mean motion √(μ/r1³)  (rad/s)
        public double Omega2;        // station mean motion √(μ/r2³) (rad/s)
        public double ApAltM;        // chaser current apoapsis altitude
        public double TargetAltM;    // raise APOAPSIS to here (~just below the station) so the coast-up carries the
                                     // chaser into CW's hand-off regime near the station
        public double RaiseTolM;     // "ap reached target" tolerance
        public double PeAltM;        // chaser periapsis altitude (for the floor)
        public double FloorM;        // the crew-safety periapsis floor
    }

    public struct FarCommand
    {
        public FarPhase Phase;   // the state after this tick
        public bool Burn;        // burn prograde this tick (TRANSFER: raise ap toward the station altitude)
        public bool PeHeld;      // a burn was suppressed by the pe floor (surface to the log/recorder)
        public double WaitS;     // seconds until the phase aligns (glue warps toward it); 0 outside PHASE
    }

    public static class Phasing
    {
        // Within this many seconds of the aligned phase, stop waiting and start the transfer burn.
        public const double PhaseAlignTolS = 15.0;

        // The far-field FSM: PHASE (coast+warp to the lead angle) → TRANSFER (bounded raise of ap) → COAST.
        // Pure decision; the glue supplies the live angles/altitudes and executes (point prograde, translate,
        // warp). The handoff to CW is the caller's FarField(range) check, so COAST just holds prograde-or-coast.
        public static FarCommand FarGuide(FarInputs f, FarPhase cur)
        {
            FarCommand c = new FarCommand();
            bool peSafe = PeSafe(f.PeAltM, f.FloorM);

            if (cur == FarPhase.Phase)
            {
                double waitS = Hohmann.WaitTimeS(f.PhaseNowRad, f.PhaseLeadRad, f.Omega1, f.Omega2);
                if (waitS > PhaseAlignTolS) { c.Phase = FarPhase.Phase; c.WaitS = waitS; return c; }
                cur = FarPhase.Transfer;   // aligned → begin the transfer this same tick
            }

            if (cur == FarPhase.Transfer)
            {
                if (f.ApAltM < f.TargetAltM - f.RaiseTolM)
                {
                    // burn (near periapsis): raise ap toward the station altitude (gated by the pe floor).
                    c.Phase = FarPhase.Transfer; c.Burn = peSafe; c.PeHeld = !peSafe; return c;
                }
                cur = FarPhase.Coast;   // ap reached → stop; coast up to it (never over-raise). CW takes it from here.
            }

            c.Phase = FarPhase.Coast;   // coast toward the CW hand-off (glue warps via the range ETA); the chaser
            return c;                   // arrives near the station at apoapsis → range drops into CW's regime.
        }

        // The co-elliptic parking altitude = a set height BELOW the target's (mean) altitude. Still used by the
        // near-field CW aim offsets (RendezvousControl.FlyNearFieldCw), not by the far-field transfer.
        public static double CoEllipticTargetAltM(double targetMeanAltM, double belowM)
        {
            return targetMeanAltM - belowM;
        }

        // ⛔ THE HARD SAFETY FLOOR (crew-safety, independent of any guidance solve). No rendezvous / docking /
        // departure translation burn may fire while periapsis is at or below the floor — so even a garbage
        // command can never walk the orbit down into re-entry. (The intentional ABORT deorbit does not go here.)
        public static bool PeSafe(double peAltM, double floorM)
        {
            return peAltM > floorM;
        }

        // Is the chaser far enough that CW is invalid and the far-field law must fly it instead?
        public static bool FarField(double rangeM, double cwHandoffRangeM)
        {
            return rangeM > cwHandoffRangeM;
        }
    }
}
