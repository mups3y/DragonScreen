// DragonScreen - UllageProbe
// ---- WHY THIS EXISTS ----
// ---- WHAT REALFUELS EXPOSES (github.com/KSP-RO/RealFuels, ModuleEnginesRF) ----
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class UllageProbe
    {
        private const string Tag = "[DragonScreen] ";

        [Tunable] public static double SettledStability = 0.90;

        private struct Rf { public FieldInfo UllageSet; public FieldInfo UllageBool; public MethodInfo Stability; }
        private static readonly Dictionary<Type, Rf> cache = new Dictionary<Type, Rf>();
        private static bool warned;

        private static Rf Resolve(object engine)
        {
            Type t = engine.GetType();
            Rf rf;
            if (cache.TryGetValue(t, out rf)) return rf;

            rf = new Rf();
            rf.UllageSet = t.GetField("ullageSet", BindingFlags.Public | BindingFlags.Instance);
            rf.UllageBool = t.GetField("ullage", BindingFlags.Public | BindingFlags.Instance);
            if (rf.UllageSet != null)
            {
                Type us = rf.UllageSet.FieldType;
                rf.Stability = FindNoArgDouble(us, "GetUllageStability")
                            ?? FindNoArgDouble(us, "GetUllageProbability");
            }
            cache[t] = rf;
            return rf;
        }

        private static MethodInfo FindNoArgDouble(Type t, string name)
        {
            if (t == null) return null;
            MethodInfo m = t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance,
                                       null, Type.EmptyTypes, null);
            return (m != null && (m.ReturnType == typeof(double) || m.ReturnType == typeof(float))) ? m : null;
        }

        public static bool RequiresUllage(ModuleEngines e)
        {
            if (e == null) return false;
            Rf rf = Resolve(e);
            if (rf.UllageBool == null) return false;
            try { return (bool)rf.UllageBool.GetValue(e); }
            catch { return false; }
        }

        public static double Stability(ModuleEngines e)
        {
            if (e == null) return -1.0;
            Rf rf = Resolve(e);
            if (rf.UllageSet == null || rf.Stability == null) return -1.0;
            try
            {
                object us = rf.UllageSet.GetValue(e);
                if (us == null) return -1.0;
                object r = rf.Stability.Invoke(us, null);
                return Convert.ToDouble(r);
            }
            catch (Exception ex)
            {
                if (!warned) { warned = true; Debug.LogWarning(Tag + "ullage read failed: " + ex.Message); }
                return -1.0;
            }
        }

        public static bool Settled(ModuleEngines e)
        {
            double s = Stability(e);
            return s >= SettledStability;
        }

        public static double VesselWorst(Vessel v, Predicate<Part> which, out int count)
        {
            count = 0;
            double worst = 2.0;
            if (v == null) return -1.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (which != null && !which(p)) continue;
                List<ModuleEngines> es = p.Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                {
                    if (!RequiresUllage(es[m])) continue;
                    double s = Stability(es[m]);
                    if (s < 0.0) continue;
                    count++;
                    if (s < worst) worst = s;
                }
            }
            return count > 0 ? worst : -1.0;
        }
    }
}
