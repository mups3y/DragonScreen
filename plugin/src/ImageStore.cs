// DragonScreen - ImageStore
// ---- WHY OFF DISK AND NOT THROUGH GameDatabase ----
// ---- A MISSING FILE IS A STATE, NOT A CRASH ----
// ---- ONE COPY, SHARED BY THREE SCREENS ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    internal static class ImageStore
    {
        private static readonly Dictionary<ImageId, Texture2D> cache =
            new Dictionary<ImageId, Texture2D>();
        private static readonly HashSet<ImageId> failed = new HashSet<ImageId>();

        internal static Texture Resolve(ImageId id)
        {
            if (id == ImageId.BodyMap) return BodyMap();
            if (id == ImageId.NavBallLive) return NavBallRenderer.Texture();
            if (id == ImageId.DockingCamLive) return DockingCamRenderer.Texture();
            return Get(id);
        }

        // ---- THE BODY MAP ----
        // ---- WHICH SLOT, AND WHY THIS IS A LIST ----
        private static CelestialBody mapBody;
        private static Texture mapTexture;

        private const int MinMapPixels = 64;

        private static readonly string[] MapSlots = { "_ColorMap", "_MainTex" };

        private static Texture BodyMap()
        {
            CelestialBody b = FlightGlobals.currentMainBody;
            if (b == null) return null;

            if (ReferenceEquals(b, mapBody) && mapTexture != null) return mapTexture;

            mapBody = b;
            mapTexture = null;

            try
            {
                if (b.scaledBody == null) return null;
                Renderer r = b.scaledBody.GetComponent<Renderer>();
                if (r == null || r.sharedMaterial == null) return null;
                Material m = r.sharedMaterial;
                string shader = (m.shader != null) ? m.shader.name : "?";

                Texture best = null;
                string bestSlot = null;
                for (int i = 0; i < MapSlots.Length && best == null; i++)
                {
                    if (!m.HasProperty(MapSlots[i])) continue;
                    Texture t = m.GetTexture(MapSlots[i]);
                    if (Usable(t)) { best = t; bestSlot = MapSlots[i]; }
                }
                if (best == null && Usable(m.mainTexture))
                {
                    best = m.mainTexture; bestSlot = "mainTexture";
                }

                mapTexture = best;

                if (best != null)
                {
                    Debug.Log("[DragonScreen] body map " + b.bodyName + " " + best.width + "x"
                              + best.height + " from " + bestSlot + " on '" + shader + "'");
                }
                else
                {
                    string names = "";
                    try
                    {
                        string[] props = m.GetTexturePropertyNames();
                        for (int i = 0; i < props.Length; i++)
                        {
                            Texture t = m.GetTexture(props[i]);
                            names += (i > 0 ? ", " : "") + props[i]
                                   + (t != null ? ("=" + t.width + "x" + t.height) : "=null");
                        }
                    }
                    catch (Exception) { names = "(could not enumerate)"; }

                    Debug.LogWarning("[DragonScreen] no usable scaled-space map for " + b.bodyName
                                     + " on shader '" + shader + "' - NAV draws the grid and track "
                                     + "only. Texture slots: " + names);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] body map lookup failed: " + e.Message);
                mapTexture = null;
            }
            return mapTexture;
        }

        private static bool Usable(Texture t)
        {
            return t != null && t.width >= MinMapPixels && t.height >= MinMapPixels;
        }

        internal static Texture2D Get(ImageId id)
        {
            if (id == ImageId.None) return null;

            Texture2D t;
            if (cache.TryGetValue(id, out t))
            {
                if (t != null) return t;
                cache.Remove(id);
            }
            if (failed.Contains(id)) return null;

            string file = Images.FileName(id);
            if (string.IsNullOrEmpty(file)) { failed.Add(id); return null; }

            try
            {
                string path = System.IO.Path.Combine(KSPUtil.ApplicationRootPath,
                                                     "GameData/DragonScreen/art/" + file);
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning("[DragonScreen] missing art: " + path);
                    failed.Add(id); return null;
                }

                byte[] data = System.IO.File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!tex.LoadImage(data))
                {
                    Debug.LogWarning("[DragonScreen] could not decode " + file);
                    UnityEngine.Object.Destroy(tex);
                    failed.Add(id); return null;
                }
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;

                int dw, dh;
                Images.Size(id, out dw, out dh);
                if (tex.width != dw || tex.height != dh)
                    Debug.LogWarning("[DragonScreen] " + file + " is " + tex.width + "x" + tex.height
                                     + " but Images.Size declares " + dw + "x" + dh
                                     + " - layout will disagree with the preview");

                cache[id] = tex;
                Debug.Log("[DragonScreen] loaded art " + file + " " + tex.width + "x" + tex.height);
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] art load threw for " + file + ": " + e.Message);
                failed.Add(id);
                return null;
            }
        }

        internal static void Clear()
        {
            foreach (KeyValuePair<ImageId, Texture2D> kv in cache)
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value);
            cache.Clear();
            failed.Clear();

            mapBody = null;
            mapTexture = null;
        }
    }
}
