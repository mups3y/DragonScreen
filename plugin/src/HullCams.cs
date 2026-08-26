// DragonScreen - HullCams
// ---- WHY THIS EXISTS ----
// ---- ⛔ NO COMPILE-TIME DEPENDENCY ON HullCameraVDS. FOUND BY NAME, AT RUNTIME. ----
// ---- WHAT IS READ, AND WHAT IS IGNORED ----
// ---- THE BOOSTER IS A DIFFERENT VESSEL AFTER SEPARATION ----
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DragonScreen
{
    internal struct HullCam
    {
        internal string Label;
        internal Part Host;
        internal Transform Anchor;
        internal Vector3 Offset;
        internal Vector3 Forward;
        internal Vector3 Up;
        internal float Fov;
    }

    internal static class HullCams
    {
        private const string Tag = "[DragonScreen] ";
        private const string ModuleName = "MuMechModuleHullCameraZoom";

        private const double RescanS = 2.0;

        private static readonly List<HullCam> cams = new List<HullCam>();
        private static double lastScanAt = -999.0;
        private static int lastCount = -1;

        internal static List<HullCam> All { get { Scan(); return cams; } }

        internal static int Count { get { Scan(); return cams.Count; } }

        internal static string[] Labels()
        {
            Scan();
            string[] a = new string[cams.Count];
            for (int i = 0; i < cams.Count; i++) a[i] = cams[i].Label;
            return a;
        }

        internal static bool TryGet(int index, out HullCam c)
        {
            Scan();
            if (index < 0 || index >= cams.Count) { c = new HullCam(); return false; }
            c = cams[index];
            return true;
        }

        internal static void Reset()
        {
            cams.Clear();
            lastScanAt = -999.0;
            lastCount = -1;
        }

        // ---------------------------------------------------------------- the scan

        private static void Scan()
        {
            double now = Time.realtimeSinceStartup;
            if (now - lastScanAt < RescanS) return;
            lastScanAt = now;

            cams.Clear();

            Vessel active = FlightGlobals.ActiveVessel;
            Harvest(active);

            Vessel booster = BoosterRecovery.Tracked;
            if (booster != null && booster != active && booster.loaded) Harvest(booster);

            if (cams.Count != lastCount)
            {
                lastCount = cams.Count;
                if (cams.Count == 0)
                {
                    Debug.Log(Tag + "no hull cameras on this vehicle - the VIDEO tab offers the "
                              + "four hull-swept views only");
                }
                else
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    for (int i = 0; i < cams.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(cams[i].Label);
                    }
                    Debug.Log(Tag + "hull cameras found: " + cams.Count + " - " + sb);
                }
            }
        }

        private static void Harvest(Vessel v)
        {
            if (v == null || !v.loaded || v.parts == null) return;

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p == null) continue;
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    PartModule pm = p.Modules[m];
                    if (pm == null) continue;
                    if (pm.GetType().Name != ModuleName) continue;

                    HullCam c;
                    if (Read(p, pm, out c)) cams.Add(c);
                }
            }
        }

        private static bool Read(Part p, PartModule pm, out HullCam c)
        {
            c = new HullCam();
            try
            {
                Type t = pm.GetType();
                string name = Str(t, pm, "cameraName");
                string xform = Str(t, pm, "cameraTransformName");

                Transform anchor = null;
                if (!string.IsNullOrEmpty(xform) && p.transform != null)
                    anchor = p.FindModelTransform(xform);
                if (anchor == null) anchor = p.transform;
                if (anchor == null) return false;

                c.Host = p;
                c.Anchor = anchor;
                c.Offset = Vec(t, pm, "cameraPosition");
                c.Forward = Vec(t, pm, "cameraForward");
                c.Up = Vec(t, pm, "cameraUp");

                float fov = Flt(t, pm, "cameraFovMax", 0f);
                if (fov <= 1f) fov = Flt(t, pm, "cameraFoVMax", 0f);
                c.Fov = (fov > 1f && fov < 170f) ? fov : 60f;

                c.Label = Clean(!string.IsNullOrEmpty(name) ? name : p.partInfo != null
                                ? p.partInfo.title : "CAM");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "could not read a hull camera on '" + p.name + "': " + e.Message);
                return false;
            }
        }

        private static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "CAM";
            string s = raw;
            if (s.StartsWith("#autoLOC"))
            {
                try { s = KSP.Localization.Localizer.Format(raw); }
                catch (Exception) { }
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (i > 0 && char.IsUpper(ch) && !char.IsUpper(s[i - 1]) && s[i - 1] != ' ')
                    sb.Append(' ');
                sb.Append(ch);
            }
            s = sb.ToString().ToUpperInvariant().Trim();

            foreach (string junk in new string[] { "GHIDORAH9 ", "GHIDORAH 9 ", "GHIDORAH ", "TE " })
                if (s.StartsWith(junk)) s = s.Substring(junk.Length);
            if (s.EndsWith(" CAM")) s = s.Substring(0, s.Length - 4);
            if (s.EndsWith("CAM") && s.Length > 3) s = s.Substring(0, s.Length - 3).TrimEnd();

            s = s.Trim();
            return (s.Length == 0) ? "CAM" : s;
        }

        // ---------------------------------------------------------------- reflection helpers

        private static string Str(Type t, object o, string field)
        {
            FieldInfo f = t.GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (f == null || f.FieldType != typeof(string)) return null;
            return (string)f.GetValue(o);
        }

        private static float Flt(Type t, object o, string field, float fallback)
        {
            FieldInfo f = t.GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (f == null) return fallback;
            if (f.FieldType == typeof(float)) return (float)f.GetValue(o);
            if (f.FieldType == typeof(double)) return (float)(double)f.GetValue(o);
            return fallback;
        }

        private static Vector3 Vec(Type t, object o, string field)
        {
            FieldInfo f = t.GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (f == null || f.FieldType != typeof(Vector3)) return Vector3.zero;
            return (Vector3)f.GetValue(o);
        }
    }
}
