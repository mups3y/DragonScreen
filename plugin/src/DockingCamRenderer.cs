/*
 * DragonScreen - DockingCamRenderer
 *
 * THE LIVE VIEW OUT OF THE DOCKING PORT, rendered into a RenderTexture the DOCKING page draws as its
 * background.
 *
 * ---- WHY A CAMERA AND NOT THEIR VIDEO ----
 * `Second.vue` puts a live THREE.js scene behind the HUD - an ISS model you fly toward - and ships
 * `dragon_video.webp` plus three `*_camera.jpg` stills. User's call, 2026-08-06: "we want live
 * docking cam not their interactive docking video thing". A still photograph presented as a camera
 * feed is the same lie as a volume slider bound to nothing, and here the honest version is no harder:
 * this is the mechanism NavBallRenderer already uses.
 *
 * ---- WHAT IT DOES NOT SHOW, STATED UP FRONT ----
 * KSP renders a scene with a CAMERA RIG - galaxy, scaled space, then the near scene - because planets
 * beyond a few kilometres live in scaled space at 1/6000 scale. This is ONE camera on the near scene,
 * so it shows the target vessel, the sun and the stars, and NOT the planet behind them. For an
 * approach that is the useful half; a black sky where Kerbin should be is a known limit, not a bug,
 * and the fix is a second scaled-space camera (MASCamera.cs:324-345 does exactly that).
 *
 * ---- IT ONLY RENDERS WHEN SOMEONE IS LOOKING ----
 * A full scene camera is not free, and for most of a mission no screen is showing DOCKING. The page
 * marks itself wanted each frame it draws; after a short grace period with nobody asking, the camera
 * is disabled and costs nothing. Three screens on DOCKING still cost one camera.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    internal static class DockingCamRenderer
    {
        /// <summary>Render target. Wider than tall because it is a page background.</summary>
        private const int Width = 640, Height = 360;

        /// <summary>Frames of grace before an unwatched camera switches itself off.</summary>
        private const int IdleFrames = 30;

        private static GameObject camObject;
        private static Camera cam;
        private static RenderTexture target;
        private static bool tried, failed;
        /// <summary>Reported on the VIDEO tab. Built once, never formatted per frame.</summary>
        internal static string Resolution = Width + " x " + Height;
        private static int lastWanted = -999;

        // ---- ONE CAMERA, TWO CONSUMERS ----
        // DOCKING wants the forward view; the VIDEO tab wants whichever direction the crew picked.
        // Rendering a second full scene camera so both can look different ways at once is real cost
        // for a rare case, so DOCKING OUTRANKS VIDEO and the VIDEO tab says so on the glass rather
        // than quietly showing the wrong direction. Priority is re-armed every frame.
        private static int wantView, wantPriority, priorityFrame = -999;

        /// <summary>Direction currently rendered. 0 Front, 1 Rear, 2 Left, 3 Right.</summary>
        internal static int View { get { return wantView; } }

        /// <summary>True when DOCKING has claimed the forward view this frame.</summary>
        internal static bool HeldByDocking
        {
            get { return wantPriority > 0 && Time.frameCount - priorityFrame <= 2; }
        }

        /// <summary>
        /// Claim the camera for a direction this frame. Called by the PAINTER, which knows which page
        /// is about to draw - ImageStore does not and should not.
        /// Highest priority wins: DOCKING passes 1, the VIDEO tab passes 0.
        /// </summary>
        internal static void Request(int view, int priority)
        {
            if (Time.frameCount != priorityFrame)
            {
                priorityFrame = Time.frameCount;
                wantPriority = -1;
            }
            if (priority > wantPriority) { wantPriority = priority; wantView = view; }
        }

        /// <summary>
        /// The live view, or null when there is nothing to look through.
        ///
        /// Calling this IS the request to render: the page asks every frame it draws, and the camera
        /// stays on while it is being asked.
        /// </summary>
        internal static Texture Texture()
        {
            // Same lifetime rule as the navball: a scene change destroys the GameObjects while the
            // flags and the RenderTexture survive, so validate rather than remember.
            if (tried && !failed && (camObject == null || target == null))
            {
                Debug.Log("[DragonScreen] docking cam was torn down with the scene - rebuilding");
                Clear();
            }

            if (failed) return null;
            if (!tried) { tried = true; Build(); }
            if (cam == null || target == null) return null;

            lastWanted = Time.frameCount;
            if (!Aim()) { cam.enabled = false; return null; }
            cam.enabled = true;
            return target;
        }

        /// <summary>
        /// Switch the camera off when no page has asked for a while. Called from the painter's
        /// update, which runs whether or not DOCKING is on screen.
        /// </summary>
        internal static void Idle()
        {
            if (cam != null && cam.enabled && Time.frameCount - lastWanted > IdleFrames)
                cam.enabled = false;
        }

        private static void Build()
        {
            try
            {
                target = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32);
                target.antiAliasing = 2;
                target.Create();

                camObject = new GameObject("DragonScreenDockingCam");
                cam = camObject.AddComponent<Camera>();
                cam.enabled = false;
                cam.targetTexture = target;
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.05f;
                // Far enough for a station on approach. Not scaled space - see the header.
                cam.farClipPlane = 25000f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.eventMask = 0;
                cam.depth = -102f;               // before the screen cameras that draw the result

                // ---- COPY THE MASK, DO NOT INVENT ONE ----
                // KSP's layer assignments are its own business and change between versions. Taking
                // the flight camera's mask means we see what the player sees; guessing a bitmask
                // would show either nothing or the inside of the capsule.
                cam.cullingMask = FlightMask();

                Resolution = Width + " x " + Height;
                Debug.Log("[DragonScreen] docking cam ready: " + Width + "x" + Height
                          + ", cullingMask 0x" + cam.cullingMask.ToString("X"));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] docking cam setup failed: " + e.Message);
                failed = true;
            }
        }

        private static int FlightMask()
        {
            try
            {
                if (FlightCamera.fetch != null && FlightCamera.fetch.mainCamera != null)
                {
                    // Drop the navball's private layer so the attitude ball can never appear in the
                    // window behind the HUD.
                    return FlightCamera.fetch.mainCamera.cullingMask & ~(1 << 29);
                }
            }
            catch (Exception) { }
            // Default | TransparentFX | Water | Local Scenery | Disconnected Parts - the usual near
            // scene, used only if the flight camera is not up yet.
            return (1 << 0) | (1 << 1) | (1 << 4) | (1 << 15) | (1 << 19);
        }

        /// <summary>
        /// Put the camera at the controlling docking port, looking out along its axis.
        ///
        /// KSP's ReferenceTransform has UP pointing out of the control point, which is exactly the
        /// docking axis when a port is controlling - the same fact VesselData.Docking() relies on for
        /// the offsets, so the picture and the numbers cannot disagree about which way is forward.
        ///
        /// Returns false when there is nothing to look through.
        /// </summary>
        private static bool Aim()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return false;
            Transform rt = v.ReferenceTransform;
            if (rt == null) return false;

            // Forward is the control point's UP - the docking axis, the same fact the offsets use.
            Vector3 fwd = rt.up, upRef = rt.forward;
            if (wantView == 1) { fwd = -rt.up; upRef = rt.forward; }
            else if (wantView == 2) { fwd = -rt.right; upRef = rt.up; }
            else if (wantView == 3) { fwd = rt.right; upRef = rt.up; }

            // ---- THE CAMERA HAS TO CLEAR THE HULL, AND 0.5 m DOES NOT ----
            // This was `rt.position + fwd * 0.5f`. The control point is the middle of the capsule and
            // the capsule is about 4 m across, so every camera sat INSIDE it: the forward view looked
            // at the back face of the docking port and the side views looked out through the Draco
            // pods. Found in flight 2026-08-06.
            //
            // A bigger constant would only be a better guess, and it would be wrong again on the next
            // vehicle. Measure the hull instead: how far the part's own geometry actually reaches
            // along this direction, then stand off from that. It costs one bounds sweep twice a
            // second and it is right for any capsule, any variant, any control point.
            camObject.transform.position = rt.position + fwd * (HullExtent(rt.position, fwd) + Standoff);
            camObject.transform.rotation = Quaternion.LookRotation(fwd, upRef);
            return true;
        }

        /// <summary>
        /// Clear of the skin by this much, so the hull is never in frame.
        ///
        /// SMALL ON PURPOSE, and the figure is borrowed rather than picked. Two docking-port camera
        /// mods are installed here and both anchor on the PORT'S OWN part transform and then nudge by
        /// centimetres - `HullCameraVDS/MM_Scripts/DockingPortCameraPatch.cfg` uses
        /// `cameraPosition = 0, 0.07, 0` and `0, 0.12, 0`, and DockingCamKURS parents to
        /// `_moduleDockingNodeGameObject` with a `cameraPosition` offset. Their offsets are tiny
        /// because their anchor is already at the outside face.
        ///
        /// Ours cannot use that anchor - the control point is often the POD, not a port - so the hull
        /// sweep finds the outside face first and this only has to clear it. Both patches also set
        /// `cameraForward = 0, 1, 0`, independently confirming that +Y is the docking axis, which is
        /// what `rt.up` above relies on.
        /// </summary>
        private const float Standoff = 0.15f;

        private static float cachedExtent;
        private static int cachedView = -999;
        private static Part cachedPart;
        private static float cachedAt = -999f;

        /// <summary>
        /// How far the controlling part's geometry reaches from the control point along <paramref
        /// name="dir"/>, in metres.
        ///
        /// Deliberately measured over the CONTROL PART ALONE, not the whole vessel. Off the pad the
        /// stack runs forty metres down to the booster's engine bells, so a whole-vessel extent would
        /// throw the rear camera below the rocket. One part gives the capsule's own skin, which is
        /// what each of these four views is looking out through.
        ///
        /// Only geometry the camera can actually SEE is counted - anything outside its culling mask
        /// cannot block the view, and that also keeps the IVA interior out of the measurement without
        /// hard-coding a layer number.
        /// </summary>
        private static float HullExtent(Vector3 origin, Vector3 dir)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return 0f;

            // MechJeb's own comment on this call: it is null after undocking. Fall back to the part
            // carrying the screen, then to the root, rather than returning a zero standoff.
            Part p = v.GetReferenceTransformPart();
            if (p == null) p = ScreenPart(v);
            if (p == null) p = v.rootPart;
            if (p == null) return 0f;

            if (p == cachedPart && wantView == cachedView
                && Time.realtimeSinceStartup - cachedAt < 0.5f)
                return cachedExtent;

            float best = 0f;
            Renderer[] rs = p.transform.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null || !r.enabled) continue;
                if (((1 << r.gameObject.layer) & cam.cullingMask) == 0) continue;

                // Farthest corner of a world-space AABB along dir, without walking all eight.
                Bounds b = r.bounds;
                float d = Vector3.Dot(b.center - origin, dir)
                          + Mathf.Abs(dir.x) * b.extents.x
                          + Mathf.Abs(dir.y) * b.extents.y
                          + Mathf.Abs(dir.z) * b.extents.z;
                if (d > best) best = d;
            }

            cachedPart = p;
            cachedView = wantView;
            cachedAt = Time.realtimeSinceStartup;
            cachedExtent = best;
            return best;
        }

        /// <summary>The part carrying the screens, used only when there is no control part.</summary>
        private static Part ScreenPart(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
                if (DragonScreenState.FindOn(v.parts[i]) != null) return v.parts[i];
            return null;
        }

        internal static void Clear()
        {
            if (cam != null) cam.targetTexture = null;
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); target = null; }
            if (camObject != null) { UnityEngine.Object.Destroy(camObject); camObject = null; }
            cam = null;
            tried = false; failed = false;
        }
    }
}
