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
// closed the half-orbit phase gap. Replaced by the standard PHASE-TIMED HOHMANN TRANSFER + CO-ELLIPTIC PARK:
//   1. PHASE  — stay on the low (fast) insertion orbit and COAST until the phase angle reaches the Hohmann lead
//               angle (Hohmann.PhaseLeadRad); the low orbit closes the along-track phase quickly, and the coast
//               is warp-compressed. No burn.
//   2. TRANSFER — at the aligned phase, burn 1 (near PERIAPSIS): raise APOAPSIS to the co-elliptic parking
//               altitude (~CoEllipticBelowM under the station), then STOP (bounded — the fix for the 200→772
//               over-raise). One finite prograde burn.
//   3. CIRCULARIZE — coast the half-orbit up to APOAPSIS, then burn 2 (AT apoapsis only): raise PERIAPSIS up to
//               the parking altitude → a near-circular CO-ELLIPTIC orbit just below the station. ⛔ THIS WAS THE
//               MISSING STEP (flight 131412, data-confirmed): TRANSFER alone left the chaser on a 420×172  km
//               ELLIPSE that only touches the station's altitude for an instant at apoapsis, so it sailed past
//               and oscillated 1,000–13,000 km for days and the CW hand-off never latched. A co-elliptic orbit
//               DWELLS just below/near the station, giving CW a stable regime to close the last tens of km.
//   4. COAST  — on the co-elliptic orbit the range drifts slowly into CW's regime → the glue hands off to CW.
// A HARD PERIAPSIS FLOOR (PeSafe) gates every burn independently — prograde-only + the floor mean the far field
// can never deorbit. Pure + headless-tested; the Hohmann timing math lives in the (tested) pure/Hohmann.cs.
// ============================================================================================
namespace DragonScreen
{
    public enum FarPhase { Phase = 0, Transfer = 1, Circularize = 2, Coast = 3 }

    // Everything FarGuide needs, all computed by the glue from the live chaser + station orbits.
    public struct FarInputs
    {
        public double PhaseNowRad;   // current phase angle: target AHEAD of chaser, [0, 2π)
        public double PhaseLeadRad;  // required Hohmann lead angle (Hohmann.PhaseLeadRad(r1, r2, μ))
        public double Omega1;        // chaser mean motion √(μ/r1³)  (rad/s)
        public double Omega2;        // station mean motion √(μ/r2³) (rad/s)
        public double ApAltM;        // chaser current apoapsis altitude
        public double TargetAltM;    // co-elliptic PARKING altitude (raise BOTH apses to here; ~just below the station)
        public double RaiseTolM;     // "apse reached target" tolerance (used for both ap-raise and pe-circularize)
        public double PeAltM;        // chaser periapsis altitude (for the circularize target + the floor)
        public bool   NearApoapsis;  // glue: chaser is within a small time window of apoapsis (so a prograde burn raises PE)
        public double FloorM;        // the crew-safety periapsis floor
    }

    public struct FarCommand
    {
        public FarPhase Phase;   // the state after this tick
        public bool Burn;        // burn prograde this tick (TRANSFER: raise ap · CIRCULARIZE: raise pe at apoapsis)
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
                    // burn 1 (near periapsis): raise ap toward the parking altitude (gated by the pe floor).
                    c.Phase = FarPhase.Transfer; c.Burn = peSafe; c.PeHeld = !peSafe; return c;
                }
                cur = FarPhase.Circularize;   // ap reached → coast the half-orbit to apoapsis, then circularize
            }

            if (cur == FarPhase.Circularize)
            {
                if (f.PeAltM < f.TargetAltM - f.RaiseTolM)
                {
                    // burn 2: raise pe to the parking altitude, but ONLY at apoapsis (there a prograde burn
                    // raises pe, not ap). Off apoapsis the glue warps the coast toward it and we don't burn.
                    c.Phase = FarPhase.Circularize;
                    c.Burn = f.NearApoapsis && peSafe;
                    c.PeHeld = f.NearApoapsis && !peSafe;
                    return c;
                }
                cur = FarPhase.Coast;   // co-elliptic orbit achieved → drift/coast into CW's regime, hand off
            }

            c.Phase = FarPhase.Coast;   // coast toward the CW hand-off; glue warps via the range ETA
            return c;
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
