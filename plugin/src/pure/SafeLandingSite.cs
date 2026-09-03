// DragonScreen — SafeLandingSite  (PURE: pick a SAFE splashdown along the ground track for a deorbit abort)
// ============================================================================================
// An on-orbit abort must come home to the NEAREST SAFE spot — for a splashdown capsule that means OPEN
// WATER, never a mountainside (the user's rule). The glue samples the orbit's ground track ahead and tags
// each point Water/notWater (body ocean + terrain below sea level); this pure selector then chooses which
// point to target, so the choice is HEADLESS-TESTED and deterministic.
//
// Two selectors:
//   • PickDeorbitTarget — the one the abort uses: the nearest WATER sample whose downrange from the vehicle
//     falls inside the reachable entry-glide window [minGlide, maxGlide] (a deorbit burn now lands ~that far
//     ahead). If none is in-window yet, returns -1 and the glue coasts one step; the window slides forward
//     each tick so a site is found within at most one orbit (Earth is ~71% ocean).
//   • PickNearestWater — a looser fallback: the nearest water sample at least minLead ahead, ignoring the
//     upper bound (used if the glide window never lands on water, e.g. a degenerate track).
//
// ⚠ RESTORED BY W15, 2026-09-04, from `8b81816^` (3,023 B, byte-identical — exactly R1 §5.1's figure), with
// ONLY this provenance comment added. R1 §5.1: RECOVER-CODE, regime RSS, flown ❌ NO.
// ✅ THIS FILE DEFINES NO CONSTANT — deliberately. Every threshold it selects against (`minGlideM`,
// `maxGlideM`, `minLeadM`) is an ARGUMENT, so §B16.8 ruling 2's [UN-CONVERGED] marking lands on the caller
// that supplies them (`src/LandingSiteScan.cs`, and above it W18/W19), not here. Stated so a later chat
// does not read a marking-free pure file as a converged one. Its selection RULE — nearest water inside the
// window, else -1 — is exact arithmetic and is what `test/SafeLandingSiteTest.cs` proves.
// ============================================================================================
namespace DragonScreen
{
    public struct GroundSample
    {
        public double DownrangeM;   // distance along the ground track ahead of the vehicle (monotonic ↑)
        public double LatDeg;       // for the glue to hand to the entry footprint target
        public double LonDeg;
        public bool Water;          // this point is over open water (safe splashdown)
    }

    public static class SafeLandingSite
    {
        // The abort's target: nearest water within the reachable entry-glide window. -1 = none in-window yet.
        public static int PickDeorbitTarget(GroundSample[] samples, double minGlideM, double maxGlideM)
        {
            if (samples == null) return -1;
            int best = -1; double bestD = double.MaxValue;
            for (int i = 0; i < samples.Length; i++)
            {
                if (!samples[i].Water) continue;
                double d = samples[i].DownrangeM;
                if (d < minGlideM || d > maxGlideM) continue;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // Fallback: nearest water at least minLead ahead (no upper bound).
        public static int PickNearestWater(GroundSample[] samples, double minLeadM)
        {
            if (samples == null) return -1;
            int best = -1; double bestD = double.MaxValue;
            for (int i = 0; i < samples.Length; i++)
            {
                if (!samples[i].Water) continue;
                double d = samples[i].DownrangeM;
                if (d < minLeadM) continue;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
    }
}
