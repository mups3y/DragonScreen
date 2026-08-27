// DragonScreen — WarpPlan  (mission-conductor: safe time-warp decisions so the autopilot NEVER overshoots a burn)
// ============================================================================================
// The pure decision layer for the conductor's time-warp handling (MechJeb WarpController, harvest §M). During
// a long ballistic coast the autopilot warps toward the next event (a burn, a launch/plane window), but it MUST
// drop back to real time with enough LEAD that the transition out of warp can never carry it PAST the burn —
// overshooting a maneuver node is a mission-ender. The rule:
//   • Aim to be at 1× a fixed LEAD before the event (DropOutUT = eventUT − BurnLeadS).
//   • Choose the on-rails warp rate so that, at that rate, one decision window advances LESS than the time left
//     to the drop-out point — so we can always step the rate down and stop cleanly (never jump past it).
//   • As the drop-out point nears, the safe rate ladders DOWN monotonically to 1× (never speeds up on approach).
//   • Inside the lead window, force real time and hand to the burn executor.
// Pure + allocation-free + headless-tested (the decisive property: the chosen rate can never overshoot the
// drop-out point in one window, and the ladder is monotone). The glue applies it via TimeWarp.WarpTo / SetRate,
// and benefits further from BetterTimeWarpContinued's smoother transitions + lossless physics warp when present.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class WarpPlan
    {
        [Tunable] public static double BurnLeadS      = 12.0;   // be at 1× this long before a burn/event starts
        [Tunable] public static double SettleMarginS  = 3.0;    // extra margin for the warp-exit transition
        [Tunable] public static double MinWarpGapS    = 30.0;   // don't bother warping for gaps shorter than this
        [Tunable] public static double LookaheadTicks = 3.0;    // never advance more than this many decision windows
                                                                // past the target in one step (the overshoot guard)

        // The UT to drop out of warp at, so an event at eventUT is met at 1× with the lead margin.
        public static double DropOutUT(double eventUT) { return eventUT - BurnLeadS; }

        // Should the conductor be warping now? Only when the gap to the event (beyond the lead) is worth it.
        public static bool ShouldWarp(double timeToEventS) { return timeToEventS - BurnLeadS > MinWarpGapS; }

        // Inside the lead window (or past it): the autopilot MUST be at real time and flying the burn approach.
        public static bool MustBeRealtime(double timeToEventS) { return timeToEventS <= BurnLeadS + SettleMarginS; }

        // The maximum ON-RAILS warp rate that cannot overshoot the drop-out point. ratesAscending is the game's
        // allowed rate table (1, 5, 10, 50, …); tickS is the real-time length of one decision window (a physics
        // frame). At rate r, one window advances r·tickS of game time — require LookaheadTicks windows of headroom
        // so the deceleration always has room to stop before the target. Returns 1.0 when essentially there.
        public static double SafeRate(double timeToDropoutS, double[] ratesAscending, double tickS)
        {
            if (ratesAscending == null || ratesAscending.Length == 0 || tickS <= 0.0) return 1.0;
            if (timeToDropoutS <= SettleMarginS) return 1.0;

            double best = 1.0;
            for (int i = 0; i < ratesAscending.Length; i++)
            {
                double r = ratesAscending[i];
                if (r <= 1.0) { best = 1.0; continue; }
                if (timeToDropoutS > LookaheadTicks * r * tickS) best = r;   // this rate has headroom to stop
                else break;                                                 // ascending → higher rates need more
            }
            return best;
        }
    }
}
