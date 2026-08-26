// DragonScreen - CapsuleRcs
// ---- ⛔ WHY: FULL REAL THRUST, DIALLED TO THE TASK (user 2026-08-24) ----
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class CapsuleRcs
    {
        private const string Tag = "[DragonScreen] ";

        // ---- PER-TASK STRENGTH, percent of the full real Draco thrust. [Tunable] ----
        [Tunable] public static double DockPct = 20.0;
        [Tunable] public static double ApproachPct = 30.0;
        [Tunable] public static double BurnPct = 100.0;
        [Tunable] public static double AttitudePct = 40.0;
        [Tunable] public static double UndockPct = 60.0;

        private static double lastPct = -1.0;

        public static double CurrentPct { get { return lastPct; } }

        private static double loggedBand = -1.0;

        public static void Set(Vessel v, double percent)
        {
            if (v == null) return;
            float p = (float)System.Math.Max(1.0, System.Math.Min(100.0, percent));
            if (System.Math.Abs(percent - lastPct) < 0.5) return;
            lastPct = percent;

            VehicleControl.SetRcsThrust(DockedSide.Ours(v), p);
            double band = System.Math.Round(percent / 10.0) * 10.0;
            if (System.Math.Abs(band - loggedBand) >= 10.0)
            {
                loggedBand = band;
                Debug.Log(Tag + "Draco strength ~" + band.ToString("F0") + "% for the current task");
            }
        }

        public static void Forget() { lastPct = -1.0; }
    }
}
