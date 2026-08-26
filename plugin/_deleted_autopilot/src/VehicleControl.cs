// DragonScreen - VehicleControl
// ---- WHAT IS ALREADY DIRECT (this file UNIFIES the primitives; it does not replace working code) ----
// ---- WHAT THIS ADDS (new levers the dump revealed, not previously controlled) ----
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class VehicleControl
    {
        private const string Tag = "[DragonScreen] ";

        // ------------------------------------------------------------------ RCS thrust limit

        public static int SetRcsThrust(List<Part> parts, double pct)
        {
            if (parts == null) return 0;
            float p = Clamp01to100(pct);
            int n = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == null) continue;
                List<ModuleRCS> rcss = parts[i].Modules.GetModules<ModuleRCS>();
                for (int k = 0; k < rcss.Count; k++) { rcss[k].thrustPercentage = p; n++; }
            }
            return n;
        }

        // ------------------------------------------------------------------ gimbal

        public static int SetGimbalLimit(Vessel v, double pct)
        {
            if (v == null) return 0;
            float p = Clamp01to100(pct);
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleGimbal> gs = v.parts[i].Modules.GetModules<ModuleGimbal>();
                for (int k = 0; k < gs.Count; k++) { gs[k].gimbalLimiter = p; n++; }
            }
            return n;
        }

        public static int SetGimbalLock(Vessel v, bool locked)
        {
            if (v == null) return 0;
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleGimbal> gs = v.parts[i].Modules.GetModules<ModuleGimbal>();
                for (int k = 0; k < gs.Count; k++) { gs[k].gimbalLock = locked; n++; }
            }
            return n;
        }

        // ------------------------------------------------------------------ control-surface (grid fin) authority

        public static int SetFinAuthority(Vessel v, double pct)
        {
            if (v == null) return 0;
            float p = Clamp01to100(pct);
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                for (int m = 0; m < v.parts[i].Modules.Count; m++)
                    if (SetField(v.parts[i].Modules[m], "authorityLimiter", p)) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ radiators

        public static int SetRadiators(Vessel v, bool on)
        {
            if (v == null) return 0;
            string ev = on ? "Activate" : "Shutdown";
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleActiveRadiator> rs = v.parts[i].Modules.GetModules<ModuleActiveRadiator>();
                for (int k = 0; k < rs.Count; k++)
                    if (InvokeEvent(rs[k], ev)) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ CoM shifter (lifting-entry trim)

        public static bool SetDescentMode(Vessel v, bool on)
        {
            if (v == null) return false;
            bool found = false;
            for (int i = 0; i < v.parts.Count; i++)
                for (int m = 0; m < v.parts[i].Modules.Count; m++)
                {
                    PartModule pm = v.parts[i].Modules[m];
                    if (pm.GetType().Name != "AdjustableCoMShifter") continue;
                    found = true;
                    bool isOn = ReadBool(pm, "IsDescentMode");
                    if (isOn != on) InvokeEvent(pm, "ToggleMode");
                }
            return found;
        }

        public static bool SetComOffset(Vessel v, double fraction)
        {
            if (v == null) return false;
            float f = fraction < 0.0 ? 0f : (fraction > 1.0 ? 1f : (float)fraction);
            bool found = false;
            for (int i = 0; i < v.parts.Count; i++)
                for (int m = 0; m < v.parts[i].Modules.Count; m++)
                {
                    PartModule pm = v.parts[i].Modules[m];
                    if (pm.GetType().Name != "AdjustableCoMShifter") continue;
                    found = true;
                    SetField(pm, "offsetPercent", f);
                }
            return found;
        }

        public static bool ReadBool(PartModule pm, string fieldName)
        {
            try
            {
                BaseField bf = pm.Fields[fieldName];
                if (bf == null) return false;
                object o = bf.GetValue(pm);
                return o is bool && (bool)o;
            }
            catch (System.Exception) { return false; }
        }

        // ------------------------------------------------------------------ decouple

        public static bool Decouple(Part p)
        {
            if (p == null) return false;
            for (int m = 0; m < p.Modules.Count; m++)
                if (InvokeEvent(p.Modules[m], "Decouple")) return true;
            return false;
        }

        // ------------------------------------------------------------------ primitives

        public static bool InvokeEvent(PartModule pm, string eventName)
        {
            if (pm == null || pm.Events == null) return false;
            foreach (BaseEvent ev in pm.Events)
            {
                if (ev == null || !ev.active) continue;
                if (!string.Equals(ev.name, eventName, System.StringComparison.OrdinalIgnoreCase)) continue;
                try { ev.Invoke(); return true; }
                catch (System.Exception e)
                { Debug.LogWarning(Tag + "event '" + eventName + "' threw: " + e.Message); return false; }
            }
            return false;
        }

        public static bool FireByGuiName(Part p, string guiName)
        {
            if (p == null) return false;
            for (int m = 0; m < p.Modules.Count; m++)
            {
                PartModule pm = p.Modules[m];
                if (pm == null) continue;

                if (pm.Events != null)
                    foreach (BaseEvent ev in pm.Events)
                        if (ev != null && ev.active
                            && string.Equals(ev.guiName, guiName, System.StringComparison.OrdinalIgnoreCase))
                        { try { ev.Invoke(); return true; } catch (System.Exception) { } }

                if (pm.Actions != null)
                    for (int a = 0; a < pm.Actions.Count; a++)
                    {
                        BaseAction ba = pm.Actions[a];
                        if (ba == null || !string.Equals(ba.guiName, guiName, System.StringComparison.OrdinalIgnoreCase))
                            continue;
                        try { ba.Invoke(new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate)); return true; }
                        catch (System.Exception) { }
                    }
            }
            return false;
        }

        public static bool SetField(PartModule pm, string fieldName, float value)
        {
            if (pm == null || pm.Fields == null) return false;
            try
            {
                BaseField bf = pm.Fields[fieldName];
                if (bf == null) return false;
                bf.SetValue(value, pm);
                return true;
            }
            catch (System.Exception) { return false; }
        }

        private static float Clamp01to100(double pct)
        {
            if (pct < 0.0) pct = 0.0;
            if (pct > 100.0) pct = 100.0;
            return (float)pct;
        }
    }
}
