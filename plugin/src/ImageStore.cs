/*
 * DragonScreen - ImageStore
 *
 * Loads the page bitmaps once and hands them out by ID.
 *
 * ---- WHY OFF DISK AND NOT THROUGH GameDatabase ----
 * Same reasoning as the F9I-era page loader: these are UI images, not part assets. KSP's texture
 * cache mipmaps and DXT-compresses what it loads, and DXT on flat navy with hairlines produces
 * exactly the blocking this design cannot afford. Reading the file ourselves keeps it pixel-exact.
 *
 * ---- A MISSING FILE IS A STATE, NOT A CRASH ----
 * A failed load leaves the slot null and logs ONCE. The page then draws without it - the renderer
 * skips an image it cannot resolve - rather than taking the screen down. An art file going missing
 * should cost a picture, not a flight.
 *
 * ---- ONE COPY, SHARED BY THREE SCREENS ----
 * Static, because the three painters would otherwise each load their own copy of a 1.3 MB texture
 * for the same picture. Released on the last painter's teardown.
 */
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

        /// <summary>
        /// The texture for an ID, or null if it is missing or unreadable.
        ///
        /// The FAILED set is what makes a negative result cacheable - keying the retry off
        /// `texture == null` would re-read a missing file from disk every frame, on three screens.
        /// That is UI trap #3 in CLAUDE.md, in a different costume.
        /// </summary>
        /// <summary>
        /// The texture for an ID, whether it comes off disk or out of the running game.
        ///
        /// Returns the base Texture, not Texture2D, because the body map is whatever KSP built its
        /// scaled-space planet from and we have no business insisting on a concrete type for
        /// something we only ever hand to a material.
        /// </summary>
        internal static Texture Resolve(ImageId id)
        {
            if (id == ImageId.BodyMap) return BodyMap();
            if (id == ImageId.NavBallLive) return NavBallRenderer.Texture();
            if (id == ImageId.DockingCamLive) return DockingCamRenderer.Texture();
            return Get(id);
        }

        // ---- THE BODY MAP ----
        // KSP's scaled-space planet is a sphere with an equirectangular colour map on it, which is
        // exactly the projection NAV needs - so the map is the REAL body and nothing is bundled. It
        // also means the page is correct over Kerbin, over the Mun, and over Earth under RSS with no
        // code change and no asset to keep in step. Confirmed 2026-08-06: there is not one loose
        // planet colour map anywhere in this GameData tree. Every copy - stock's and Parallax's
        // alike - lives inside an AssetBundle, and the ONLY place to get at it is the live material.
        //
        // ---- WHICH SLOT, AND WHY THIS IS A LIST ----
        // Asking for _MainTex is what the first version did, and on this install it "worked": it
        // returned a 4x4 DUMMY, because Parallax Continued replaces the scaled shader with
        // Custom/ParallaxScaled and keeps the real map in _ColorMap. A successful lookup returning a
        // 4-pixel texture is the silent-failure pattern this project keeps paying for - so now the
        // candidates are tried in order AND anything placeholder-sized is rejected.
        //
        //      _ColorMap   Parallax Continued        (Parallax_StockPlanetTextures/_Configs)
        //      _MainTex    stock, Kopernicus, RSS
        //      mainTexture whatever the shader calls its main slot
        //
        // And the log names EVERY texture property the material has, so the next unfamiliar shader
        // is one log line away from being handled rather than a guessing game.
        private static CelestialBody mapBody;
        private static Texture mapTexture;

        /// <summary>Below this, a texture is a placeholder rather than a map. Parallax's is 4x4.</summary>
        private const int MinMapPixels = 64;

        private static readonly string[] MapSlots = { "_ColorMap", "_MainTex" };

        private static Texture BodyMap()
        {
            CelestialBody b = FlightGlobals.currentMainBody;
            if (b == null) return null;

            // SAME BODY IS NOT THE SAME TEXTURE. Kerbin is the same object across a scene change, so
            // the body check alone said "cached, done" while the texture it cached had been released
            // - the NAV map came back blank after a revert to the VAB. Validate the texture too;
            // Unity's == null reports a destroyed object, and asking is free.
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
                    // Name every candidate the material actually has. Cheap, once per body, and it
                    // turns "the map is blank" into "add this slot to MapSlots".
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

        /// <summary>
        /// A real surface map, not a stand-in.
        ///
        /// The width test is the whole point: Parallax leaves a 4x4 in _MainTex, and a null check
        /// alone called that a success. Anything a planet is genuinely drawn from is at least 1024
        /// across, so 64 rejects placeholders without any risk of rejecting a real map.
        /// </summary>
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
                // A cached texture that Unity has destroyed is worse than no cache: it draws
                // nothing, silently, forever. Same lesson as the navball - validate, do not
                // remember. Dropping it here means the next call simply reloads from disk.
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
                // mipChain false: drawn at roughly 1:1 into the render target, and mips would only
                // soften edges the design depends on.
                Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!tex.LoadImage(data))
                {
                    Debug.LogWarning("[DragonScreen] could not decode " + file);
                    UnityEngine.Object.Destroy(tex);
                    failed.Add(id); return null;
                }
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;

                // The declared size in src/pure drives every layout decision on both renderers, so a
                // file that does not match it would place correctly in the preview and wrongly in
                // game. Report it rather than letting the two quietly diverge.
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

            // DROPPED, NEVER DESTROYED. The body map belongs to KSP - it is the planet the player is
            // looking at. Destroying it here because it happens to sit in our cache field would take
            // the texture off the scaled-space planet for the rest of the session.
            mapBody = null;
            mapTexture = null;
        }
    }
}
