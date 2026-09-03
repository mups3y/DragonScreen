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

        // NAMED assets (the Figma-exported PNGs the rebuilt pages place by key) live in art/cover/ and
        // are cached the same way, on their string key rather than an ImageId. See DrawCmd.AssetKey.
        private static readonly Dictionary<string, Texture2D> assetCache =
            new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> assetFailed = new HashSet<string>();

        internal static Texture Resolve(ImageId id)
        {
            if (id == ImageId.BodyMap) return BodyMap();
            if (id == ImageId.NavBallLive) return NavBallRenderer.Texture();
            if (id == ImageId.DockingCamLive) return DockingCamRenderer.Texture();
            if (id == ImageId.ScaledPlanetLive) return ScaledPlanetTexture();
            return Get(id);
        }

        // ---- THE LIVE 3D PLANET SEAM (S10a), NOW CLOSED (S10b). ----
        //
        // docs/MAP_MFD_RESEARCH.md §2 renders scaled space into a RenderTexture through a camera
        // built with CopyFrom(ScaledCamera.Instance.cam), aimed by the pure PlanetGeom. S10a wired
        // everything downstream of this line for BOTH answers and left the renderer itself to S10b,
        // because a Unity camera cannot be exercised with the game closed. That renderer exists now -
        // src/ScaledPlanetRenderer.cs, written for the owner's 2026-09-03 install + glass session -
        // and this is the one line that was S10b's whole hook-up. S10b's three in-sim criteria are
        // not recorded as answered yet, so the LINE stays HELD; see REGISTER.md.
        //
        // It still returns null most of the time, honestly: the renderer hands back a texture only
        // while a page has actually claimed the camera and the geometry could be framed. Everything
        // downstream is unchanged and already handles that - PageState.PlanetCamLive goes false,
        // NavPage draws the textured disc under PlanetGeom.NoSignalLabel, and the PNG preview, which
        // never links this file at all, does the same for ever.
        private static Texture ScaledPlanetTexture() { return ScaledPlanetRenderer.Texture(); }

        /// <summary>Is there a live scaled-space render this frame? See ScaledPlanetTexture. Read by
        /// VesselData into PageState.PlanetCamLive, so the PAGE never asks about textures and the
        /// GLUE never decides what a page says.</summary>
        internal static bool ScaledPlanetLive() { return ScaledPlanetTexture() != null; }

        /// <summary>
        /// The texture for a NAMED asset (art/cover/&lt;key&gt;.png), loaded once and cached. Same rules
        /// as Get: a missing or undecodable file logs once and is remembered as failed so it is not
        /// retried every frame, and the page simply draws without it rather than crashing.
        /// </summary>
        internal static Texture2D ResolveAsset(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            Texture2D t;
            if (assetCache.TryGetValue(key, out t))
            {
                if (t != null) return t;
                assetCache.Remove(key);
            }
            if (assetFailed.Contains(key)) return null;

            try
            {
                string path = System.IO.Path.Combine(KSPUtil.ApplicationRootPath,
                                                     "GameData/DragonScreen/art/cover/" + key + ".png");
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning("[DragonScreen] missing asset: " + path);
                    assetFailed.Add(key); return null;
                }

                byte[] data = System.IO.File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!tex.LoadImage(data))
                {
                    Debug.LogWarning("[DragonScreen] could not decode asset " + key);
                    UnityEngine.Object.Destroy(tex);
                    assetFailed.Add(key); return null;
                }
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;

                assetCache[key] = tex;
                Debug.Log("[DragonScreen] loaded asset " + key + " " + tex.width + "x" + tex.height);
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] asset load threw for " + key + ": " + e.Message);
                assetFailed.Add(key);
                return null;
            }
        }

        // ---- THE CAPSULE TURNTABLE: A BOUNDED RESIDENT SET (T11b item 6) ----
        //
        // Every other asset here obeys "load once, keep for ever", which is right for the couple of
        // dozen page PNGs and very wrong for the turntable: 36 frames at 512x1024 RGBA is ~75 MB of
        // texture, and one drag round the vehicle touches all of them. So the sequence is the ONE
        // asset with a residency policy, and the policy itself is pure - Turntable.IsResident decides
        // what may be held, this file only does what it says. That split is deliberate: what is left
        // here is a load, a Destroy and a loop, which is all the glue is allowed to be.
        //
        // WHAT THIS DOES PER CALL, AND WHY IT IS CHEAP: nothing at all unless the centre frame moved
        // (the steady state - the crew is looking, not dragging), and at most ONE decode when it did.
        // The rest is a 36-iteration integer sweep over precomputed keys, no allocation.

        /// <summary>Frame each screen's capsule view is centred on, or Turntable.NotShowing. Sized
        /// and indexed exactly as ScreenPainter's livePage: screenIndex 1..3, slot 0 unused.</summary>
        private static readonly int[] turnCentre = { Turntable.NotShowing, Turntable.NotShowing,
                                                     Turntable.NotShowing, Turntable.NotShowing };

        /// <summary>
        /// This screen is showing the capsule view, centred on <paramref name="frame"/>: keep the
        /// window around it warm and let go of everything outside every screen's window. Called from
        /// the painter's build path, once per screen per frame.
        /// </summary>
        internal static void WarmTurntable(int screen, int frame)
        {
            if (screen < 0 || screen >= turnCentre.Length) return;
            int f = Turntable.WrapFrame(frame);
            if (turnCentre[screen] == f) { WarmOne(); return; }
            turnCentre[screen] = f;
            Sweep();
            WarmOne();
        }

        /// <summary>This screen is not showing the capsule view. Frames no OTHER screen wants are
        /// released - so leaving the page from the last screen showing it frees the sequence.</summary>
        internal static void ReleaseTurntable(int screen)
        {
            if (screen < 0 || screen >= turnCentre.Length) return;
            if (turnCentre[screen] == Turntable.NotShowing) return;
            turnCentre[screen] = Turntable.NotShowing;
            Sweep();
        }

        /// <summary>Evict every frame the policy no longer allows.</summary>
        private static void Sweep()
        {
            for (int i = 0; i < Turntable.Count; i++)
            {
                if (Turntable.IsResident(i, turnCentre)) continue;
                string key = Turntable.Key(i);
                Texture2D t;
                if (!assetCache.TryGetValue(key, out t)) continue;
                if (t != null) UnityEngine.Object.Destroy(t);
                assetCache.Remove(key);
            }
        }

        /// <summary>
        /// Load AT MOST ONE not-yet-resident frame, nearest to a centre first. One per call rather
        /// than the whole window in one go: opening the view would otherwise be five file reads and
        /// five decodes inside a single frame, which is the hitch this exists to remove, just moved.
        /// </summary>
        private static void WarmOne()
        {
            for (int s = 0; s < turnCentre.Length; s++)
            {
                int c = turnCentre[s];
                if (c < 0) continue;
                for (int i = 0; i <= Turntable.WarmSteps; i++)
                {
                    // The window nearest-first, and then the pinned front - which the reset tap lands
                    // on however far away it is, so it is worth having ready even when the drag is
                    // nowhere near it. Normally already loaded: the view opens on the front.
                    int f = (i < Turntable.WarmSteps) ? c + Turntable.WarmOffset(i) : Turntable.FrontFrame;
                    string key = Turntable.Key(f);
                    if (assetCache.ContainsKey(key) || assetFailed.Contains(key)) continue;
                    ResolveAsset(key);
                    return;
                }
            }
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

            foreach (KeyValuePair<string, Texture2D> kv in assetCache)
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value);
            assetCache.Clear();
            assetFailed.Clear();

            // The turntable's residency goes with the textures it describes; leaving a stale centre
            // behind would have the next Sweep believe frames are held that were just destroyed.
            for (int i = 0; i < turnCentre.Length; i++) turnCentre[i] = Turntable.NotShowing;

            mapBody = null;
            mapTexture = null;
        }
    }
}
