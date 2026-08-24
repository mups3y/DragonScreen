/*
 * DragonScreen - CapsuleRcs
 *
 * GLUE. Per-TASK strength for the Crew Dragon's Draco RCS.
 *
 * ---- ⛔ WHY: FULL REAL THRUST, DIALLED TO THE TASK (user 2026-08-24) ----
 * The Draco now carries its REAL total thrust (16 x 400 N ~= 6.4 kN, config patch), which is ~5x what
 * this craft flew before. At full strength that would over-drive the docking servo (tuned for the old
 * weak RCS) and waste propellant holding attitude. So we do NOT fly full thrust all the time: this dials
 * `ModuleRCS.thrustPercentage` to the job - gentle for docking (back to the old effective authority, so
 * the docking control is unchanged), full for the rendezvous and de-orbit BURNS (real Crew-2 flies those
 * on the Dracos), a middle setting to just HOLD attitude on a coast without spending fuel on it.
 *
 * "We have the full real thrust when needed, then tune the thruster strength for the task at hand."
 *
 * The per-task percentages are [Tunable] so they can be dialled in flight without a rebuild.
 */
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class CapsuleRcs
    {
        private const string Tag = "[DragonScreen] ";

        // ---- PER-TASK STRENGTH, percent of the full real Draco thrust. [Tunable] ----
        /// <summary>Docking: back to the OLD effective authority the docking servo is tuned for. With the
        /// Draco now ~5x stronger, ~20 % reproduces what docking flew on before - so it is unchanged.</summary>
        [Tunable] public static double DockPct = 20.0;
        /// <summary>The L-approach / close terminal: a little more than docking, still gentle.</summary>
        [Tunable] public static double ApproachPct = 30.0;
        /// <summary>Rendezvous + de-orbit BURNS: the full real Draco, so a ~100 m/s de-orbit takes ~12 min
        /// like the real vehicle instead of ~an hour.</summary>
        [Tunable] public static double BurnPct = 100.0;
        /// <summary>Just HOLDING or slewing attitude on a coast - enough to hold, not to burn fuel chasing.</summary>
        [Tunable] public static double AttitudePct = 40.0;
        /// <summary>Undock back-away burst - a firm, brief push.</summary>
        [Tunable] public static double UndockPct = 60.0;

        /// <summary>The strength last written, so we only touch the modules on a real change.</summary>
        private static double lastPct = -1.0;

        /// <summary>The Draco strength currently commanded, percent (for the recorder). -1 = not yet set.</summary>
        public static double CurrentPct { get { return lastPct; } }

        /// <summary>Last strength band we logged, so the smooth de-orbit ease does not spam the log.</summary>
        private static double loggedBand = -1.0;

        /// <summary>
        /// Set OUR capsule's Draco thrustPercentage to <paramref name="percent"/> of full (1..100).
        /// Only our side of any docking joint (DockedSide.Ours), never the station's RCS. Idempotent -
        /// writes only when the setting actually changes, so it is safe to call every tick.
        /// </summary>
        public static void Set(Vessel v, double percent)
        {
            if (v == null) return;
            float p = (float)System.Math.Max(1.0, System.Math.Min(100.0, percent));
            if (System.Math.Abs(percent - lastPct) < 0.5) return;
            lastPct = percent;

            List<Part> ours = DockedSide.Ours(v);
            for (int i = 0; i < ours.Count; i++)
            {
                List<ModuleRCS> rcss = ours[i].Modules.GetModules<ModuleRCS>();
                for (int k = 0; k < rcss.Count; k++) rcss[k].thrustPercentage = p;
            }
            // Not logged per-change: the de-orbit varies the strength smoothly (would spam), and it is
            // already in the recorder as d_rcsPct. Log only the big task-to-task steps, rounded to a band.
            double band = System.Math.Round(percent / 10.0) * 10.0;
            if (System.Math.Abs(band - loggedBand) >= 10.0)
            {
                loggedBand = band;
                Debug.Log(Tag + "Draco strength ~" + band.ToString("F0") + "% for the current task");
            }
        }

        /// <summary>Forget the last setting - call on disengage so the next engage re-asserts its strength.</summary>
        public static void Forget() { lastPct = -1.0; }
    }
}
