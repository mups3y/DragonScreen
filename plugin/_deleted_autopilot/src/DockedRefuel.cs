// DragonScreen - DockedRefuel
using UnityEngine;

namespace DragonScreen
{
    public static class DockedRefuel
    {
        private const string Tag = "[DragonScreen] ";

        private static bool refuelling, announcedFull;
        private static double lastUt;

        public static void Tick()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || !DockedSide.Docked(v))
            {
                refuelling = false;
                announcedFull = false;
                lastUt = 0.0;
                return;
            }

            if (!refuelling)
            {
                refuelling = true;
                announcedFull = false;
                lastUt = Planetarium.GetUniversalTime();
                Refuel.Begin();
                Debug.Log(Tag + "docked - auto-refuelling the capsule from the station ("
                          + (Refuel.Fraction(v) * 100.0).ToString("F0") + "% to start)");
                return;
            }

            if (Refuel.Full(v))
            {
                if (!announcedFull)
                {
                    announcedFull = true;
                    Debug.Log(Tag + "capsule tank full while docked - " + Refuel.Report(v));
                }
                return;
            }

            double now = Planetarium.GetUniversalTime();
            double dt = (lastUt > 0.0) ? now - lastUt : 0.0;
            lastUt = now;
            if (dt > 0.0) Refuel.Tick(v, dt);
        }

        public static void Reset()
        {
            refuelling = false;
            announcedFull = false;
            lastUt = 0.0;
        }
    }
}
