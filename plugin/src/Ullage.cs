// DragonScreen — Ullage  (KSP glue: read RealFuels propellant-settling state via reflection)
// ============================================================================================
// RealFuels models ullage: after a coast in free-fall the propellant floats off the engine intake and an
// ignition can fail (and there is NO retry budget — one ignition per engine mode). Before every relight the
// autopilot settles the propellant (fire the aft RCS) and only lights when the stability ≥ 0.996.
//
// We cannot reference the RealFuels assembly, so this reads the same fields MechJeb does (VesselState):
//   RealFuels.ModuleEnginesRF.ullage      (bool)   — is this engine ullage-modelled?
//   RealFuels.ModuleEnginesRF.ullageSet   (object) — the UllageSet
//   RealFuels.Ullage.UllageSet.GetUllageStability() (double 0..1) — propellant stability
// Without RealFuels (or on an engine it does not model) stability is 1.0 (always stable) — so the ascent
// logic degrades to "ignite immediately", which is correct for a stock build.
// ============================================================================================
using System;
using System.Reflection;

namespace DragonScreen
{
    public static class Ullage
    {
        public const double StableThreshold = IgnitionGate.UllageStable;   // 0.996

        static bool inited, ok;
        static Type rfEngineType;
        static FieldInfo ullageField, ullageSetField;
        static MethodInfo getStability;

        static void Init()
        {
            if (inited) return;
            inited = true;
            try
            {
                Assembly rf = null;
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string n = a.GetName().Name;
                    if (n != null && n.IndexOf("RealFuels", StringComparison.OrdinalIgnoreCase) >= 0) { rf = a; break; }
                }
                if (rf == null) { UnityEngine.Debug.Log("[DragonScreen] Ullage: RealFuels not loaded — stability = stable"); return; }

                rfEngineType = rf.GetType("RealFuels.ModuleEnginesRF");
                Type ullageSetType = rf.GetType("RealFuels.Ullage.UllageSet");
                if (rfEngineType == null || ullageSetType == null) return;

                const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                ullageField = rfEngineType.GetField("ullage", F);
                ullageSetField = rfEngineType.GetField("ullageSet", F);
                getStability = ullageSetType.GetMethod("GetUllageStability", BindingFlags.Instance | BindingFlags.Public);

                ok = ullageField != null && ullageSetField != null && getStability != null;
                UnityEngine.Debug.Log("[DragonScreen] Ullage reflection " + (ok ? "READY (RealFuels)" : "INCOMPLETE — treating as stable"));
            }
            catch (Exception e) { ok = false; UnityEngine.Debug.LogWarning("[DragonScreen] Ullage init failed: " + e.Message); }
        }

        // Propellant stability [0..1] for the engine about to be lit. 1.0 = fully settled / not modelled.
        public static double Stability(ModuleEngines e)
        {
            Init();
            if (!ok || e == null || !rfEngineType.IsInstanceOfType(e)) return 1.0;
            try
            {
                bool ullage = (bool)ullageField.GetValue(e);
                if (!ullage) return 1.0;                       // engine not ullage-modelled → stable
                object us = ullageSetField.GetValue(e);
                if (us == null) return 1.0;
                object r = getStability.Invoke(us, null);
                return r is double ? (double)r : 1.0;
            }
            catch { return 1.0; }
        }

        public static bool Stable(ModuleEngines e) { return Stability(e) >= StableThreshold; }
    }
}
