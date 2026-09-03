// DragonScreen — CoastEta  (mission-conductor: how long a coast lasts, so warp-to-maneuvers has a target UT)
// ============================================================================================
// The warp orchestration (MissionConductor + pure/WarpPlan) warps toward a future EVENT UT and drops out with
// lead. A range-closing coast (the rendezvous co-elliptic chase closing to the CW hand-off; a departure drift)
// has no scheduled burn UT — the "event" is the moment the relative RANGE crosses a threshold. This turns the
// live range + range-rate into that ETA so the conductor can warp the long coast instead of waiting in realtime.
//
// It is a deliberately COARSE, linear, SELF-CORRECTING estimate: orbital range does not close linearly, so the
// controller re-issues this every realtime tick and the estimate refines as the geometry updates. It is bounded
// by maxHorizonS (typically one target orbital period) so a single warp step can never leap far past the event —
// WarpPlan's lead + the conductor's universal burn-guard + the periapsis floor are the hard safety layers; this
// only decides HOW FAR to warp. Errs SHORT by design (drop out early, re-warp) rather than overshoot.
//
// Units: metres, metres/second, seconds. rangeRate SIGN convention: + = SEPARATING (range growing), − = CLOSING.
// Pure + allocation-free + headless-tested.
//
// ---- RESTORED BY W4 (Wave D, §B12.8), 2026-09-04, from `8b81816^` — 3,077 B, byte-for-byte R1 §5.1's row.
// ⚠ IT HAS NO CALLER. Its consumers were the coast controllers (`RendezvousControl` / `ReturnControl`,
// R1 §5.2 RECOVER-CODE) feeding `MissionConductor.WarpToEvent`; none of the three is in the tree, and none
// belongs to a recovery wave yet (registers **W9** / the un-waved §5.2 glue). Nothing coasts or warps today.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class CoastEta
    {
        // Seconds to coast until rangeM falls to targetRangeM, given the current signed rangeRate (+ = opening).
        //  • already at/inside the target      → 0     (don't warp; the event is here)
        //  • closing                           → (range − target)/closing, capped at maxHorizonS
        //  • not closing (holding / opening)   → maxHorizonS  (warp a bounded chunk; orbital motion brings it back)
        // maxHorizonS bounds every case so the conductor never warps an unbounded span off a single estimate.
        public static double TimeToRange(double rangeM, double rangeRateMps, double targetRangeM, double maxHorizonS)
        {
            if (maxHorizonS <= 0.0) return 0.0;
            if (rangeM <= targetRangeM) return 0.0;

            double closingMps = -rangeRateMps;                 // + = approaching the target
            if (closingMps <= ClosingEpsMps) return maxHorizonS;   // not closing now → bounded look-ahead, re-check

            double etaS = (rangeM - targetRangeM) / closingMps;
            if (etaS < 0.0) etaS = 0.0;
            return etaS < maxHorizonS ? etaS : maxHorizonS;
        }

        // Below this closing speed the range is treated as "not closing" (a co-elliptic hold, or opening) — warp a
        // bounded chunk rather than divide by a near-zero rate and produce a giant ETA. 0.1 m/s is well below any
        // real orbital closing rate that matters for warp scheduling.
        public const double ClosingEpsMps = 0.1;
    }
}
