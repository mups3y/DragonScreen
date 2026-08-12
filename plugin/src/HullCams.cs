/*
 * DragonScreen - HullCams
 *
 * GLUE. The REAL cameras bolted to this vehicle, found on the vehicle rather than listed by us.
 *
 * ---- WHY THIS EXISTS ----
 * User, 2026-08-11: *"the tundra parts should have cameras attached by default. The crew capsule,
 * trunk and booster I think. I would like these cameras to be the camera views we can pick on the
 * dragonscreen."*
 *
 * The VIDEO tab already offered four views - FRONT, REAR, LEFT, RIGHT - and those are honest: they
 * are derived from the control point and a live sweep of the hull, not from a picture. But they are
 * SYNTHETIC. They are directions we chose. The cameras the vehicle actually carries are somewhere
 * else entirely, pointed at things the designer thought were worth looking at, and the interstage
 * cameras in particular look at exactly what a Falcon 9 webcast looks at.
 *
 * ---- ⛔ NO COMPILE-TIME DEPENDENCY ON HullCameraVDS. FOUND BY NAME, AT RUNTIME. ----
 * `MuMechModuleHullCameraZoom` lives in `HullCameraVDS/Plugins/HullcamVDSContinued.dll`. The MuMech
 * prefix is historical - it is NOT MechJeb, which is not installed here at all - and it is worth
 * writing down because the name misleads.
 *
 * We do not reference that assembly. `build.py`'s MODS list is empty on purpose, and adding a mod
 * reference would mean the plugin fails to load for anyone without it. Instead the module is matched
 * by type NAME and its fields read by reflection, so:
 *
 *      HullCameraVDS installed and cameras on the craft  ->  they appear
 *      HullCameraVDS installed, no cameras on the craft  ->  nothing appears
 *      HullCameraVDS not installed at all                ->  nothing appears, no error
 *
 * In all three cases the list contains exactly the cameras that exist. `CLAUDE.md`: never build a
 * control bound to nothing.
 *
 * ---- WHAT IS READ, AND WHAT IS IGNORED ----
 * From the part config (`TundraExploration/Patches/Extra_Hullcam.cfg` is the reference):
 *
 *      cameraName            the label the crew sees - the mod author's own name for the view
 *      cameraTransformName   the transform on the part model the camera sits at
 *      cameraForward/Up      orientation overrides, IN PART-LOCAL SPACE
 *      cameraPosition        a small offset, also part-local
 *      cameraFovMax/Min      the zoom range; we take Max as the resting field of view
 *
 * ⚠ A ZERO `cameraForward` IS NOT A DIRECTION, IT IS "USE THE TRANSFORM". Every Tundra camera
 * ships `cameraForward = 0, 0, 0` and relies on `cameraTransformName` pointing the right way, while
 * HullCameraVDS's own docking-port patch ships `cameraForward = 0, 1, 0` with no transform name at
 * all. Both forms are live in this install, so both are handled, and a zero vector is treated as
 * absent rather than as a direction - normalising it would aim every Tundra camera at nothing.
 *
 * ---- THE BOOSTER IS A DIFFERENT VESSEL AFTER SEPARATION ----
 * The interstage cameras leave with the first stage. `BoosterRecovery` already raises the booster's
 * ranges so it stays loaded through its landing, and a loaded vessel's transforms are real, so its
 * cameras keep working - which is the whole point of having them. An UNLOADED vessel has no parts at
 * all, the same fact that made the docking refusal wrong on 2026-08-11, so it contributes nothing
 * and is skipped rather than reported as an empty camera.
 */
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DragonScreen
{
    /// <summary>One real camera on one real part.</summary>
    internal struct HullCam
    {
        /// <summary>The mod author's own name for the view. What the crew sees on the button.</summary>
        internal string Label;
        /// <summary>The part it is bolted to. Held so a jettisoned camera can be noticed.</summary>
        internal Part Host;
        /// <summary>Where it sits and which way it looks.</summary>
        internal Transform Anchor;
        /// <summary>Part-local offset from the anchor. Zero for every Tundra camera.</summary>
        internal Vector3 Offset;
        /// <summary>Part-local look direction, or zero meaning "use the anchor's forward".</summary>
        internal Vector3 Forward;
        /// <summary>Part-local up, or zero meaning "use the anchor's up".</summary>
        internal Vector3 Up;
        /// <summary>Resting field of view, degrees.</summary>
        internal float Fov;
    }

    internal static class HullCams
    {
        private const string Tag = "[DragonScreen] ";
        private const string ModuleName = "MuMechModuleHullCameraZoom";

        /// <summary>Re-scan at most this often. Parts do not appear mid-flight except at staging.</summary>
        private const double RescanS = 2.0;

        private static readonly List<HullCam> cams = new List<HullCam>();
        private static double lastScanAt = -999.0;
        private static int lastCount = -1;

        /// <summary>The cameras on the vehicle right now. Never null; often empty.</summary>
        internal static List<HullCam> All { get { Scan(); return cams; } }

        internal static int Count { get { Scan(); return cams.Count; } }

        /// <summary>The labels, in the same order, for the page. Empty array when there are none.</summary>
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

            // The booster, once it is its own vessel and still loaded. See the header.
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

        /// <summary>
        /// Pull one camera's configuration out of the module by reflection.
        ///
        /// Every field is optional. A module missing all of them still yields a usable camera at the
        /// part's own transform, which is better than dropping a camera the crew can see on the hull.
        /// </summary>
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
                if (fov <= 1f) fov = Flt(t, pm, "cameraFoVMax", 0f);   // the cfgs use both spellings
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

        /// <summary>
        /// A button label, not a config string.
        ///
        /// Camera names are written for a right-click menu: "GHidorah9OuterInterstageCam",
        /// "#autoLOC_HULL_PM_007". Localisation tokens are resolved and the vendor prefix and the
        /// trailing "Cam" are dropped, because the crew already knows whose rocket they are on and
        /// that they are looking at a camera. What is left is the part that distinguishes one view
        /// from another, which is the only reason the label exists.
        /// </summary>
        private static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "CAM";
            string s = raw;
            if (s.StartsWith("#autoLOC"))
            {
                try { s = KSP.Localization.Localizer.Format(raw); }
                catch (Exception) { }
            }

            // Split camelCase so "OuterInterstage" becomes two words before it is upper-cased.
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
