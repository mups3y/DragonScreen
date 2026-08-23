/*
 * DragonScreen - UllageProbe
 *
 * GLUE. Reads RealFuels' LIVE ullage state off an engine so the flight computer knows EXACTLY when a
 * propellant-settling engine can be lit, instead of guessing with a fixed timer.
 *
 * ---- WHY THIS EXISTS ----
 * The MVac (and every booster relight) carries `ullage = True`: RealFuels will not let it build thrust
 * until the propellant is settled over the intake, and lighting into unsettled propellant flames it out
 * on "No propellants" AND spends one of a handful of ignitions. Our sequence ullaged for a fixed 6 s and
 * then lit blind - on flight_0822_211853 the propellant never settled, so it lit, flamed out, and the
 * stage never reached orbit, with nothing on the glass saying why. RealFuels already computes the
 * answer every tick; this reads it.
 *
 * ---- WHAT REALFUELS EXPOSES (github.com/KSP-RO/RealFuels, ModuleEnginesRF) ----
 *   public Ullage.UllageSet ullageSet;                 // the per-engine ullage simulation
 *   ullageSet.GetUllageStability()  -> double 0..1     // how settled the propellant is, NOW
 *   ullageSet.GetUllageState(out Color) -> string      // the PAW status ("Very Stable (100%)" ...)
 *   public bool ullage;                                // does this engine even require settling
 *   public string propellantStatus;                    // the KSPField the PAW shows
 * DragonScreen does not reference RealFuels (stock must build without it), so all of this is read by
 * REFLECTION, cached per engine type. On a stock engine (no `ullageSet`) every call returns "not
 * applicable" and callers fall back to their previous behaviour.
 */
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class UllageProbe
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>Stability at or above which we treat the propellant as settled enough to light.
        /// RealFuels' own ignition roll uses the stability as a success probability, so 0.90 means
        /// ~10% residual risk - high enough to commit an ignition, not so high we wait on noise.</summary>
        [Tunable] public static double SettledStability = 0.90;

        // Reflection is resolved once per engine CLR type and cached - a GetField per tick on every
        // engine is exactly the kind of cost the recorder's own notes warn about.
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
                // Prefer the raw "how settled is it" reading; fall back to the probability form.
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

        /// <summary>True if this engine requires settled propellant (RealFuels ullage). Stock: false.</summary>
        public static bool RequiresUllage(ModuleEngines e)
        {
            if (e == null) return false;
            Rf rf = Resolve(e);
            if (rf.UllageBool == null) return false;
            try { return (bool)rf.UllageBool.GetValue(e); }
            catch { return false; }
        }

        /// <summary>
        /// The engine's LIVE ullage stability, 0 (floating) .. 1 (fully settled). Returns -1 when the
        /// engine has no RealFuels ullage (stock build) or the reading is unavailable, so a caller can
        /// tell "not applicable" from "genuinely unsettled" and keep its old path.
        /// </summary>
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

        /// <summary>Settled enough to commit an ignition? False also when unreadable, so we never light
        /// blind on an engine we cannot see the state of - EXCEPT stock (no ullage), handled by callers.</summary>
        public static bool Settled(ModuleEngines e)
        {
            double s = Stability(e);
            return s >= SettledStability;
        }

        /// <summary>
        /// The worst (lowest) ullage stability across the engines of a vessel that actually require it,
        /// and how many require it. The worst engine is the one that will flame out, so it governs the
        /// go/no-go. `count` = 0 means no ullage-limited engine here (stock, or none live).
        /// </summary>
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
