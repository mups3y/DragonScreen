/*
 * DragonScreen - DockedRefuel
 *
 * GLUE. Fills the capsule's monopropellant from the station FOR AS LONG AS IT IS DOCKED, not just in
 * the moment before undocking. The mission is a ferry: the capsule launches light, spends what it
 * needs on ascent/rendezvous/docking, and is meant to leave the berth FULL - see `Refuel`'s header.
 *
 * ⛔ WHY THIS EXISTS: THE TOP-UP WAS ONLY AT UNDOCK. `Refuel.Tick` was called from `UndockOps` alone,
 * so nothing moved propellant while berthed - the crew watched a docked capsule sit at whatever the
 * docking had left it and had to transfer by hand (2026-08-17). Refuelling starts the instant the
 * ports mate now, so by the time anyone thinks about undocking the tank is already full. The undock
 * top-up stays as the backstop - it simply finds nothing to do.
 *
 * Ticked by `FlightDriver`, like every other authority. `Refuel.Tick` yields when not docked, when
 * full, or when the station has nothing to give, so this is safe to call every frame.
 */
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
                // Not docked (or just undocked): stand down and re-arm for the next berthing.
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

            // Warp-safe: drive the transfer on the universal clock so a berthed warp still fills it.
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
