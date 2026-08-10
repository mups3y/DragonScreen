/*
 * DragonScreen - PodEngines
 *
 * GLUE. Light and shut down the capsule's own engines - the SuperDracos on the pod, as distinct from
 * the Merlins on either Falcon stage. Ported from `F9I/dragon_deorbit.ks:548 DgLandEngines`,
 * `DgHasLandingEngines`.
 *
 * ---- ⛔ A NODE EXECUTOR CANNOT EXECUTE ANYTHING WITHOUT THRUST ----
 * This exists because of one failure that has now happened twice on two different phases. The first
 * station return ran its entire phase-down with `availablethrust = 0.0` - nothing had ever been
 * ignited - so the burn fell back to shoving on RCS and pushed the WRONG WAY: 120.3 × 119.5 km became
 * 159.1 × 138.0 instead of 85.1 × 79.2, and it spent 34.5 units of monopropellant doing it. The same
 * failure had already happened once with an unlit MVac during the rendezvous.
 *
 * So: light the engines, then WAIT AND CHECK that thrust actually appeared, before planning a burn.
 * `Available` is the check; a caller that skips it is repeating a flight.
 *
 * ---- ⚠ IDENTIFY BY WHAT IT IS BOLTED TO, NOT BY THE MODULE ----
 * Every engine on the vehicle is a `ModuleEnginesFX`, so the module alone does not distinguish a
 * SuperDraco from a Merlin. F9I excludes anything whose part name marks it as a Falcon stage, and we
 * use the same `VehicleParts` predicates the rest of the plugin already trusts. `falcon-detect-by-
 * capability`: the part-name test here is a NEGATIVE filter on two known stages, not a positive test
 * for a pod - which is why it survives a second crew Dragon variant that the positive test did not.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class PodEngines
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>Seconds to allow between commanding ignition and giving up on it.</summary>
        public const double IgnitionTimeoutS = 10.0;
        /// <summary>Thrust below which we consider nothing to be lit, kN.</summary>
        public const double ThrustFloorKn = 0.1;

        /// <summary>Does this vehicle have pod engines at all? `DgHasLandingEngines`.</summary>
        public static bool Present(Vessel v)
        {
            if (v == null) return false;
            for (int i = 0; i < v.parts.Count; i++)
                if (IsPodEngine(v.parts[i])) return true;
            return false;
        }

        /// <summary>Is there thrust available right now? This is the question that matters.</summary>
        public static bool Available(Vessel v)
        {
            return v != null && v.GetTotalMass() > 0.0
                && ThrustKn(v) > ThrustFloorKn;
        }

        /// <summary>Total thrust the vessel could produce at full throttle, kN.</summary>
        public static double ThrustKn(Vessel v)
        {
            if (v == null) return 0.0;
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleEngines> es = v.parts[i].Modules.GetModules<ModuleEngines>();
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
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!IsPodEngine(v.parts[i])) continue;
                List<ModuleEnginesFX> es = v.parts[i].Modules.GetModules<ModuleEnginesFX>();
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

        /// <summary>
        /// An engine that belongs to the capsule rather than to a Falcon stage.
        ///
        /// ⚠ The exclusion is by stage, not by pod: see the header. A part with no engine module is
        /// never a pod engine however it is named.
        /// </summary>
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
