// DragonScreen - Tuning
// ---- HOW IT WORKS ----
// ---- ⛔ THE CODE DEFAULT STAYS THE AUTHORITY ----
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DragonScreen
{

    public static class Tuning
    {
        private const string Tag = "[DragonScreen] ";
        private const string RootName = "DRAGONSCREEN_TUNING";

        private static readonly Dictionary<string, FieldInfo> fields =
            new Dictionary<string, FieldInfo>();
        private static string overridePath, referencePath;
        private static DateTime lastApplied;
        private static bool built;
        private static float lastPoll;

        public static void Build()
        {
            if (built) return;
            built = true;

            try
            {
                foreach (Type t in Assembly.GetExecutingAssembly().GetTypes())
                {
                    FieldInfo[] fs = t.GetFields(BindingFlags.Public | BindingFlags.Static);
                    for (int i = 0; i < fs.Length; i++)
                    {
                        FieldInfo f = fs[i];
                        if (f.IsLiteral || f.IsInitOnly) continue;
                        if (!f.IsDefined(typeof(TunableAttribute), false)) continue;
                        if (!Supported(f.FieldType)) continue;
                        fields[t.Name + "." + f.Name] = f;
                    }
                }

                string dir = PluginDataDir();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                overridePath = Path.Combine(dir, "tuning.cfg");
                referencePath = Path.Combine(dir, "tuning.reference.cfg");

                WriteReference();
                Apply(true);
                Debug.Log(Tag + "tuning ready - " + fields.Count + " tunable field(s); catalogue at "
                          + "PluginData/tuning.reference.cfg, overrides read from PluginData/tuning.cfg");
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "tuning setup failed (defaults stand): " + e.Message);
            }
        }

        public static void Poll()
        {
            if (!built || overridePath == null) return;
            float now = Time.realtimeSinceStartup;
            if (now - lastPoll < 1.0f) return;
            lastPoll = now;
            try
            {
                if (!File.Exists(overridePath)) return;
                DateTime w = File.GetLastWriteTimeUtc(overridePath);
                if (w == lastApplied) return;
                Apply(false);
            }
            catch (Exception e) { Debug.LogWarning(Tag + "tuning poll: " + e.Message); }
        }

        private static void Apply(bool silent)
        {
            if (overridePath == null || !File.Exists(overridePath)) return;
            lastApplied = File.GetLastWriteTimeUtc(overridePath);

            ConfigNode wrap = ConfigNode.Load(overridePath);
            if (wrap == null) return;
            ConfigNode root = wrap.GetNode(RootName);
            if (root == null) root = wrap;

            int changed = 0, unknown = 0;
            for (int i = 0; i < root.values.Count; i++)
            {
                ConfigNode.Value v = root.values[i];
                FieldInfo f;
                if (!fields.TryGetValue(v.name, out f)) { unknown++; continue; }
                object parsed;
                if (!TryParse(f.FieldType, v.value, out parsed)) continue;
                object old = f.GetValue(null);
                if (Equals(parsed, old)) continue;
                f.SetValue(null, parsed);
                changed++;
                Debug.Log(Tag + "tuning  " + v.name + "  " + Str(old) + " -> " + Str(parsed));
            }
            if (!silent || changed > 0)
                Debug.Log(Tag + "tuning applied - " + changed + " override(s)"
                          + (unknown > 0 ? ", " + unknown + " unknown key(s) ignored" : ""));
        }

        private static void WriteReference()
        {
            ConfigNode root = new ConfigNode(RootName);
            List<string> keys = new List<string>(fields.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < keys.Count; i++)
                root.AddValue(keys[i], Str(fields[keys[i]].GetValue(null)));
            root.Save(referencePath, "DragonScreen tunable parameters and their code defaults. "
                    + "Copy any line into tuning.cfg (same folder) to override it; edits there apply "
                    + "LIVE in flight within ~1 s. This file is regenerated every flight and is not read.");
        }

        // ------------------------------------------------------------------ helpers

        private static bool Supported(Type t)
        {
            return t == typeof(double) || t == typeof(float) || t == typeof(int) || t == typeof(bool);
        }

        private static bool TryParse(Type t, string s, out object result)
        {
            result = null;
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim();
            try
            {
                if (t == typeof(double)) { result = double.Parse(s, CultureInfo.InvariantCulture); return true; }
                if (t == typeof(float)) { result = float.Parse(s, CultureInfo.InvariantCulture); return true; }
                if (t == typeof(int)) { result = int.Parse(s, CultureInfo.InvariantCulture); return true; }
                if (t == typeof(bool)) { result = bool.Parse(s); return true; }
            }
            catch { return false; }
            return false;
        }

        private static string Str(object o)
        {
            if (o is double) return ((double)o).ToString("R", CultureInfo.InvariantCulture);
            if (o is float) return ((float)o).ToString("R", CultureInfo.InvariantCulture);
            return Convert.ToString(o, CultureInfo.InvariantCulture);
        }

        private static string PluginDataDir()
        {
            string here = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(here, "PluginData");
        }
    }
}
