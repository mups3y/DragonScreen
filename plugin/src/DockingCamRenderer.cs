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
 *
 * ---- WHERE THE DOCKING VIEW COMES FROM: THE PORT ITSELF, KURS-STYLE ----
 * DOCKING mounts the camera on OUR controlling docking node's own `nodeTransform` and looks out along
 * its forward - the port's OUTWARD axis, the same fact `DockingOps` steers on, so the picture and the
 * numbers cannot disagree about which way the target is. This is exactly how the installed KURS mod
 * (`DockingCamKURS`) does it: its `MM_KSPCamera.cfg` bolts a `DockingCameraModule` onto every part
 * `@MODULE[ModuleDockingNode]` and the DLL parents a camera to the node's game object.
 *
 * An earlier attempt put the camera at the vessel CONTROL POINT (the pod centre) and swept a measured
 * hull extent to stand off FRONT/REAR/LEFT/RIGHT. It never produced a usable docking picture - the
 * control point is the middle of the capsule, not the port - and it was removed 2026-08-21 along with
 * its hull-sweep. The node transform is where a docking camera actually belongs, and it needs no sweep
 * because it is already at the outside face.
 *
 * The VIDEO tab is unchanged: it still offers the vehicle's OWN cameras (HullCams, e.g. the interstage
 * views). DOCKING outranks it for the one shared scene camera.
 */
using System;
using System.Collections.Generic;
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
        /// <summary>View last reported as blank, so the warning is not per-frame.</summary>
        private static int blankReportedFor = -999;

        // ---- ONE CAMERA, TWO CONSUMERS ----
        // DOCKING wants the forward view; the VIDEO tab wants whichever direction the crew picked.
        // Rendering a second full scene camera so both can look different ways at once is real cost
        // for a rare case, so DOCKING OUTRANKS VIDEO and the VIDEO tab says so on the glass rather
        // than quietly showing the wrong direction. Priority is re-armed every frame.
        private static int wantView, wantPriority, priorityFrame = -999;

        /// <summary>
        /// View currently rendered. 0-3 are the hull-swept directions - Front, Rear, Left, Right -
        /// and <see cref="HullCamBase"/> upward are the vehicle's own cameras, in HullCams order.
        /// </summary>
        internal static int View { get { return wantView; } }

        /// <summary>
        /// The DOCKING view: the camera on our controlling docking node, looking out along its axis.
        /// A negative sentinel so it can never collide with a hull-camera index, and so the VIDEO
        /// tab's bounds (0 .. HullCams.Count-1) exclude it - DOCKING claims it directly.
        /// </summary>
        internal const int DockingPortView = -1;

        /// <summary>
        /// First view index that means "a real camera on the vehicle". Zero since 2026-08-13, when the
        /// four synthetic hull-swept directions (FRONT/REAR/LEFT/RIGHT) were removed for never
        /// producing a usable picture. The VIDEO tab enumerates HullCams from here; view 0 is the
        /// FIRST REAL CAMERA on the vehicle. DOCKING no longer uses view 0 - it uses
        /// <see cref="DockingPortView"/>, mounted on the port itself (see the header).
        /// </summary>
        internal const int HullCamBase = 0;

        /// <summary>How far in front of the port face to sit, metres. Small: the node transform is
        /// already at the outside face, this only clears the ring off the near plane. Live-tunable so
        /// the framing can be nudged (pull it back toward 0 to bring the docking ring into shot).</summary>
        [Tunable] public static double PortStandoffM = 0.15;

        /// <summary>
        /// Field of view of the docking-port camera, degrees. WIDE on purpose: a docking approach is
        /// rarely dead-aligned, and at 60 deg the target fell outside the frame whenever the port axis
        /// was more than 30 deg off it - flight 2026-08-21 sat ~45 deg off and showed black. 90 deg
        /// keeps the target in shot while the pilot lines up, the way a real docking camera is wide.
        /// Live-tunable for framing.
        /// </summary>
        [Tunable] public static double PortFovDeg = 90.0;

        /// <summary>Label for the view now on screen, for the page to print under the picture.</summary>
        internal static string ViewLabel
        {
            get
            {
                if (wantView == DockingPortView) return "DOCKING PORT";
                HullCam hc;
                return HullCams.TryGet(wantView - HullCamBase, out hc) ? hc.Label : "-";
            }
        }

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
            if (!Aim())
            {
                // ---- SAY WHY THERE IS NO PICTURE, ONCE PER VIEW. ----
                // On 2026-08-12 the crew reported "none of the cameras are working on the camera
                // selection list" and the log had nothing to say about it - a black rectangle is
                // indistinguishable from a camera pointed at space, and neither the selection log
                // line nor the page could tell them apart. Latched on the view so a genuinely dead
                // camera says so once rather than every frame.
                if (blankReportedFor != wantView)
                {
                    blankReportedFor = wantView;
                    aimReportedFor = -999;
                    Debug.LogWarning("[DragonScreen] camera view " + wantView + " ("
                                   + ViewLabel + ") has no picture - "
                                   + (wantView == DockingPortView
                                      ? "no usable docking port: none fitted, or the only one is docked or shielded"
                                      : "its part is gone or its transform was destroyed"));
                }
                cam.enabled = false;
                return null;
            }
            if (blankReportedFor == wantView) blankReportedFor = -999;
            cam.enabled = true;

            // ---- ONE LINE PER VIEW, SO A BLACK RECTANGLE CAN BE TOLD FROM A DEAD CAMERA. ----
            // A picture of empty space and a camera rendering nothing look identical on the glass,
            // and the crew has reported "the cameras are not working" for both. This says where the
            // camera actually IS and what it is looking at, once per view rather than every frame.
            if (aimReportedFor != wantView)
            {
                aimReportedFor = wantView;
                Debug.Log("[DragonScreen] camera view " + wantView + " (" + ViewLabel
                          + ") live at " + camObject.transform.position.ToString("F1")
                          + " facing " + camObject.transform.forward.ToString("F2")
                          + ", fov " + cam.fieldOfView.ToString("F0")
                          + ", mask 0x" + cam.cullingMask.ToString("X"));
            }
            return target;
        }

        /// <summary>
        /// Switch the camera off when no page has asked for a while. Called from the painter's
        /// update, which runs whether or not DOCKING is on screen.
        /// </summary>
        private static int aimReportedFor = -999;

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
        /// Point the camera for the view now claimed: DOCKING looks out our docking port; every other
        /// view is a real camera the vehicle carries. Returns false when there is nothing to look
        /// through, which the caller reports once rather than leaving a black rectangle.
        /// </summary>
        private static bool Aim()
        {
            if (wantView == DockingPortView) return AimDockingPort();
            return AimHullCam(wantView - HullCamBase);
        }

        /// <summary>
        /// Put the camera on our controlling docking node, looking OUT along its axis - the KURS
        /// technique (see the header). The node transform is already at the outside face and its
        /// forward is the OUTWARD axis, the same one `DockingOps` steers on, so no hull sweep and no
        /// guessed standoff are needed - only a small clearance off the near plane.
        ///
        /// Returns false when the vehicle has no usable port (none fitted, or the only one is docked
        /// or shielded), in which case DOCKING shows its dark background and the HUD, honestly.
        /// </summary>
        private static bool AimDockingPort()
        {
            Vessel v = OurVessel();
            if (v == null || v.parts == null) return false;

            ModuleDockingNode node = PickPort(v);
            if (node == null || node.nodeTransform == null) return false;

            Transform nt = node.nodeTransform;
            Vector3 fwd = nt.forward;                    // OUTWARD - toward the target on approach
            if (fwd.sqrMagnitude < 1e-8f) return false;
            fwd = fwd.normalized;

            // The node's own up is a stable perpendicular; fall back only if it is somehow degenerate.
            Vector3 up = nt.up;
            if (up.sqrMagnitude < 1e-8f || Mathf.Abs(Vector3.Dot(fwd, up.normalized)) > 0.999f)
                up = nt.right;

            camObject.transform.position = nt.position + fwd * (float)PortStandoffM;
            camObject.transform.rotation = Quaternion.LookRotation(fwd, up);
            // A hull camera may have left its own field of view behind. Restore ours.
            cam.fieldOfView = (float)PortFovDeg;

            // ---- IS IT ACTUALLY POINTED AT THE TARGET? A few lines, then quiet. ----
            // A black docking view could be a mis-aimed camera OR just empty/dark space in shot; the
            // angle off boresight tells them apart. Small angle + still black => lighting or range,
            // not aim. This is why flight 2026-08-21 read black: the target sat ~45 deg off a 60 deg
            // FOV. Delete once the framing is confirmed good.
            if (portLogsLeft > 0 && Time.realtimeSinceStartup - portLastLog > 2f)
            {
                ITargetable tgt = v.targetObject;
                Transform tt = (tgt != null) ? tgt.GetTransform() : null;
                if (tt != null)
                {
                    portLastLog = Time.realtimeSinceStartup;
                    portLogsLeft--;
                    Vector3 toTgt = (Vector3)tt.position - camObject.transform.position;
                    float ang = Vector3.Angle(fwd, toTgt);
                    Debug.Log("[DragonScreen] docking cam: target " + ang.ToString("F0")
                              + " deg off boresight at " + toTgt.magnitude.ToString("F0") + " m - "
                              + (ang < PortFovDeg * 0.5 ? "IN frame" : "OUT of frame")
                              + " (FOV " + PortFovDeg.ToString("F0") + ")");
                }
            }
            return true;
        }

        private static float portLastLog = -999f;
        private static int portLogsLeft = 4;

        /// <summary>
        /// The docking node to look out of: the FREE node nearest the target (so a multi-port vehicle
        /// looks out the one actually being used), else the first free node, else the first node that
        /// has a transform at all. The Dragon carries one, so this is usually unambiguous; the ranking
        /// only bites on a vehicle with several. Same free/shielded test as `DockingOps.OpenPorts`.
        /// </summary>
        private static ModuleDockingNode PickPort(Vessel v)
        {
            Vector3d tgtPos = Vector3d.zero;
            bool haveTgt = false;
            ITargetable tgt = v.targetObject;
            if (tgt != null && tgt.GetTransform() != null)
            {
                tgtPos = tgt.GetTransform().position;
                haveTgt = true;
            }

            ModuleDockingNode firstAny = null, firstFree = null, nearest = null;
            double best = double.MaxValue;

            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleDockingNode> ns = v.parts[i].Modules.GetModules<ModuleDockingNode>();
                for (int m = 0; m < ns.Count; m++)
                {
                    ModuleDockingNode n = ns[m];
                    if (n.nodeTransform == null) continue;
                    if (firstAny == null) firstAny = n;
                    if (n.otherNode != null) continue;                    // already docked
                    if (!string.IsNullOrEmpty(n.state)
                        && n.state.ToLowerInvariant().Contains("disabled")) continue;  // shielded
                    if (firstFree == null) firstFree = n;
                    if (haveTgt)
                    {
                        double d = ((Vector3d)n.nodeTransform.position - tgtPos).sqrMagnitude;
                        if (d < best) { best = d; nearest = n; }
                    }
                }
            }
            return nearest ?? firstFree ?? firstAny;
        }

        /// <summary>
        /// Point the camera through one of the vehicle's own hull cameras.
        ///
        /// ⚠ A CAMERA CAN LEAVE MID-FLIGHT. The interstage cameras go with the first stage and the
        /// trunk's go at jettison, so the transform is re-validated every frame rather than
        /// remembered - a destroyed Transform compares null in Unity and we simply report no picture,
        /// which is the truth. The same reasoning as the scene-teardown check in Texture().
        ///
        /// The config's `cameraForward` and `cameraUp` are PART-LOCAL and are used only when
        /// non-zero. Every Tundra camera ships them as zero and relies on the named transform's own
        /// orientation; HullCameraVDS's docking-port patch does the opposite. Both are live here.
        /// </summary>
        private static bool AimHullCam(int index)
        {
            HullCam c;
            if (!HullCams.TryGet(index, out c)) return false;
            // Unity's overloaded null: a destroyed transform or a jettisoned part both land here.
            if (c.Anchor == null || c.Host == null || c.Host.vessel == null) return false;

            Transform ht = c.Host.transform;
            if (ht == null) return false;

            Vector3 pos = c.Anchor.position;
            if (c.Offset != Vector3.zero) pos += ht.TransformDirection(c.Offset);

            Vector3 fwd = (c.Forward != Vector3.zero)
                        ? ht.TransformDirection(c.Forward) : c.Anchor.forward;
            Vector3 up = (c.Up != Vector3.zero)
                       ? ht.TransformDirection(c.Up) : c.Anchor.up;

            if (fwd.sqrMagnitude < 1e-8f) return false;
            // Parallel forward and up give Unity no rotation to build; fall back rather than warn.
            if (up.sqrMagnitude < 1e-8f
                || Mathf.Abs(Vector3.Dot(fwd.normalized, up.normalized)) > 0.999f)
                up = c.Anchor.up;

            camObject.transform.position = pos;
            camObject.transform.rotation = Quaternion.LookRotation(fwd, up);
            cam.fieldOfView = c.Fov;
            return true;
        }

        /// <summary>
        /// THE VESSEL THESE SCREENS ARE IN - not whichever one the camera is following.
        ///
        /// ⛔ THIS WAS `FlightGlobals.ActiveVessel` AND THAT IS WRONG DURING A RECOVERY.
        /// `BoosterRecovery:256` calls `ForceSetActiveVessel(booster)` so the crew can watch the
        /// landing, and MechJeb does the same thing for the same reason. With the active vessel as
        /// the source, the Dragon's own docking camera renders a view out of the BOOSTER - and the
        /// screens are in the capsule, showing the capsule's crew a picture from a stage three
        /// hundred kilometres away.
        ///
        /// Same rule the crew card already follows: our own `DragonScreenState` is the marker for
        /// "this is our capsule", and it beats anything about focus. Falls back to the active
        /// vessel so a craft without the module still gets a picture.
        /// </summary>
        private static Vessel OurVessel()
        {
            List<Vessel> all = FlightGlobals.Vessels;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    Vessel v = all[i];
                    if (v == null || !v.loaded || v.parts == null) continue;
                    if (ScreenPart(v) != null) return v;
                }
            }
            return FlightGlobals.ActiveVessel;
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
