/*
 * DragonScreen - DockShroud
 *
 * GLUE. Opens the Dragon's docking shroud (nose cone) before docking and closes it after undocking,
 * by firing the same part event F9I fires - `FalconOpenDockingShroud` / `StCloseDockingShroud`
 * (falcon9.ks, station_ops.ks). The event lives on the nose part; its gui name is "open shroud" /
 * "close shroud" (older parts: "open/close docking hatch").
 *
 * ⚠ THE BUTTON READS "JETTISON" BUT THE PART OPENS AND SHUTS - it is not a one-way decouple. So this
 * must fire the toggle EVENT, not stage a decoupler. Firing "open" when already open is harmless: the
 * event is inactive in that state and simply is not found.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class DockShroud
    {
        private const string Tag = "[DragonScreen] ";

        private static readonly string[] OpenNames  = { "open shroud", "open docking hatch" };
        private static readonly string[] CloseNames = { "close shroud", "close docking hatch" };

        /// <summary>Open the docking shroud. True if an open event was found and fired.</summary>
        public static bool Open(Vessel v) { return Fire(v, OpenNames, "opened"); }

        /// <summary>Close the docking shroud. True if a close event was found and fired.</summary>
        public static bool Close(Vessel v) { return Fire(v, CloseNames, "closed"); }

        private static bool Fire(Vessel v, string[] names, string what)
        {
            if (v == null) return false;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        foreach (BaseEvent ev in p.Modules[m].Events)
                        {
                            if (ev == null || !ev.active || string.IsNullOrEmpty(ev.guiName)) continue;
                            string g = ev.guiName.Trim().ToLowerInvariant();
                            for (int k = 0; k < names.Length; k++)
                            {
                                if (g != names[k]) continue;
                                ev.Invoke();
                                Debug.Log(Tag + "docking shroud " + what + " - fired '" + ev.guiName
                                          + "' on '" + p.partInfo.title + "'");
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning(Tag + "shroud " + what + " failed: " + e.Message); }
            return false;
        }
    }
}
