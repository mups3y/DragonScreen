// DragonScreen - PodEngines
// ---- ⛔ A NODE EXECUTOR CANNOT EXECUTE ANYTHING WITHOUT THRUST ----
// ---- ⚠ IDENTIFY BY WHAT IT IS BOLTED TO, NOT BY THE MODULE ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class PodEngines
    {
        private const string Tag = "[DragonScreen] ";

        public const double IgnitionTimeoutS = 10.0;
        public const double ThrustFloorKn = 0.1;

        public static bool Present(Vessel v)
        {
            if (v == null) return false;
            List<Part> ours = DockedSide.Ours(v);
            for (int i = 0; i < ours.Count; i++)
                if (IsPodEngine(ours[i])) return true;
            return false;
        }

        public static bool Available(Vessel v)
        {
            return v != null && v.GetTotalMass() > 0.0
                && ThrustKn(v) > ThrustFloorKn;
        }

        public static double ThrustKn(Vessel v)
        {
            if (v == null) return 0.0;
            double t = 0.0;
            List<Part> ours = DockedSide.Ours(v);
            for (int i = 0; i < ours.Count; i++)
            {
                List<ModuleEngines> es = ours[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (es[m].EngineIgnited && !es[m].flameout)
                        t += es[m].MaxThrustOutputVac(true);
            }
            return t;
        }

        public static int On(Vessel v) { return Set(v, "activate engine", true); }
        public static int Off(Vessel v) { return Set(v, "shutdown engine", false); }

        private static int Set(Vessel v, string actionName, bool on)
        {
            if (v == null) return 0;
            int n = 0;
            // ---- ⛔ OUR SIDE ONLY. A MERGED VESSEL'S ENGINES ARE THE STATION'S TOO. ----
            List<Part> ours = DockedSide.Ours(v);
            for (int i = 0; i < ours.Count; i++)
            {
                if (!IsPodEngine(ours[i])) continue;
                List<ModuleEnginesFX> es = ours[i].Modules.GetModules<ModuleEnginesFX>();
                for (int m = 0; m < es.Count; m++)
                {
                    BaseAction a = Find(es[m], actionName);
                    if (a == null) continue;
                    a.Invoke(new KSPActionParam(a.actionGroup, KSPActionType.Activate));
                    n++;
                }
            }
            if (n > 0)
                Debug.Log(Tag + "pod engines " + (on ? "LIT" : "shut down") + " - " + n + " engine(s)");
            else
                Debug.LogWarning(Tag + "no pod engine found for '" + actionName + "'");
            return n;
        }

        private static BaseAction Find(PartModule pm, string actionName)
        {
            for (int a = 0; a < pm.Actions.Count; a++)
            {
                BaseAction ba = pm.Actions[a];
                if (ba == null || ba.guiName == null) continue;
                if (string.Equals(ba.guiName, actionName, StringComparison.OrdinalIgnoreCase))
                    return ba;
            }
            return null;
        }

        private static bool IsPodEngine(Part p)
        {
            if (p == null) return false;
            if (p.Modules.GetModules<ModuleEnginesFX>().Count == 0) return false;
            string n = p.name;
            if (VehicleParts.IsBooster(n) || VehicleParts.IsSecondStage(n)) return false;
            return true;
        }
    }
}
