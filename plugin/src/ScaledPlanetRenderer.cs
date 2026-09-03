/*
 * DragonScreen - ScaledPlanetRenderer
 *
 * THE LIVE 3D PLANET: a dedicated camera on SCALED SPACE, rendered into a RenderTexture the NAV
 * page's 3D PLANET view draws. docs/MAP_MFD_RESEARCH.md §2; the register calls it S10b.
 *
 * ---- WHY THIS FILE COULD NOT BE WRITTEN BEFORE ----
 * S10a built everything about this view that a closed game can decide: the placement arithmetic
 * (pure/PlanetGeom.cs, 66 headless checks), the seam (ImageId.ScaledPlanetLive ->
 * ImageStore.ScaledPlanetTexture -> PageState.PlanetCamLive), and the honest LIVE 3D - NO SIGNAL
 * state for a view with no camera behind it. What it could not do is RUN a Camera: build.py compiles
 * this glue on every `test`, but nothing headless renders. So the camera waits for install + glass
 * time, which is S18's gate - and that gate is still HELD.
 *
 * ⛔ SO READ THIS FILE AS WRITTEN, NOT AS VERIFIED. It compiles, and everything it decides was
 * decided and tested in PlanetGeom, but no line below has ever rendered a frame: S10b's three in-sim
 * questions - does the globe render, does the orbit line occlude against true geometry, does the 3/4
 * framing read at cabin distance - are all still open, and only the owner opens the gate that answers
 * them (C1.12). It is deliberately thin - it holds a transform to apply and a texture to hand over,
 * and every decision it makes was already made and tested in PlanetGeom.
 *
 * ---- IT IS THE DOCKING CAM, POINTED SOMEWHERE ELSE ----
 * Same lifetime rules as src/DockingCamRenderer.cs, for the same reasons, and read that file's
 * header for the long form of each: build once and VALIDATE-NOT-REMEMBER across scene loads (Unity's
 * overloaded null destroys the GameObject while our static flags survive); re-aim in OnPreCull so the
 * picture is never a frame stale; and switch the camera off when nobody is looking, because a full
 * scene camera is not free and most of a mission is not spent on this page.
 *
 * The ONE difference is what it renders - scaled space (the planets), not the near scene:
 *
 * ---- COPY THE GAME'S OWN SCALED CAMERA. DO NOT INVENT A BITMASK. ----
 * Scaled scenery is a KSP layer whose number drifts between versions, and a guessed mask renders
 * either nothing or the wrong thing, silently. cam.CopyFrom(ScaledCamera.Instance.cam) inherits the
 * exact culling mask, clip planes and projection the map draws planets with, so what we render IS
 * what the map's planet render is. Everything after the CopyFrom is an override we mean: our target
 * texture, our clear, our depth, our framing. The near/far clip planes are NOT overridden - they are
 * the ones scaled space needs, and they are the reason the copy exists.
 *
 * ---- COORDINATES ----
 * Scaled space is the world uniformly shrunk about a moving origin, with no rotation, so a POSITION
 * has to be converted (ScaledSpace.LocalToScaledSpace) but a DIRECTION does not - a world-space
 * orbit normal is already a scaled-space orbit normal. The body's centre is not converted at all: we
 * take its scaledBody transform, because that transform IS where the render puts the globe, and a
 * converted world position could only agree with it.
 *
 * ---- WHAT THIS DOES NOT DO YET, STATED UP FRONT ----
 * §2.2's overlay RE-PROJECTION is not built. The orbit line over this view is still S10a's
 * ORTHOGRAPHIC GlobeProjection, which is exactly right over the textured disc and is NOT right over a
 * perspective render from a 3/4 chase angle - the two look at the globe from different places. So a
 * page that turns this feed on gets a real globe with an overlay that does not sit on it. That is why
 * the only caller is the NAV page's own 3D PLANET view, and why the register carries the
 * re-projection as its own line rather than it being smuggled in here.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    internal static class ScaledPlanetRenderer
    {
        /// <summary>Render target. Wider than tall because it is a page background - the map well.</summary>
        private const int Width = 640, Height = 360;

        /// <summary>Frames of grace before an unwatched camera switches itself off.</summary>
        private const int IdleFrames = 30;

        /// <summary>
        /// Frames a claim stays good for. PageState.PlanetCamLive is read out of VesselData, which
        /// runs at the TOP of the frame - before the painter has claimed anything this frame - so a
        /// claim has to outlive the frame it was made in or the flag would flicker off every frame.
        /// Two is enough for that and short enough that leaving the page drops the feed at once.
        /// </summary>
        private const int ClaimFrames = 2;

        private static GameObject camObject;
        private static Camera cam;
        private static RenderTexture target;
        private static bool failed;
        private static int lastWanted = -999;
        private static double wantRotDeg;
        private static int wantZoom;

        private static bool blankReported, liveReported;
        private static double liveDistance, liveFill;

        /// <summary>Reported alongside the other camera resolutions. Built once, never per frame.</summary>
        internal static string Resolution = Width + " x " + Height;

        /// <summary>Range from the body centre at the last successful aim, scaled units.</summary>
        internal static double Range { get { return liveDistance; } }

        /// <summary>
        /// Vertical field of view of the scaled-space lens, degrees. OURS, not the map's: CopyFrom
        /// brings ScaledCamera's fov across, but the game moves that one when the player zooms the
        /// map, and a camera whose lens changes under a crew that never touched it is not an
        /// instrument. The globe stays the SAME SIZE whatever this is - PlanetGeom.Distance re-solves
        /// the range from it - so what this actually sets is how much PERSPECTIVE the view has: how
        /// open the orbit ellipse reads, and how much space sits around the limb. Live-tunable so the
        /// framing can be judged on the glass without a rebuild (see Tuning).
        /// </summary>
        [Tunable] public static double FovDeg = 60.0;

        /// <summary>
        /// Framing nudges, degrees, added to PlanetGeom's DefaultAzimuthDeg / DefaultPitchDeg.
        ///
        /// THE DEFAULT 3/4 VIEW (-55 / +30) IS OURS - CHOSEN, NOT MEASURED, and PlanetGeom says so at
        /// the constant. Whether it READS at cabin distance is the one thing a PNG cannot settle, so
        /// these two exist to settle it: they are live-tunable, so the framing can be dialled in the
        /// capsule and the answer read straight off the file rather than guessed between restarts.
        /// Zero means "the default, unchanged", and the code default remains the authority - Tuning
        /// never edits the source (see its header).
        /// </summary>
        [Tunable] public static double AzimuthTrimDeg = 0.0;
        [Tunable] public static double PitchTrimDeg = 0.0;

        /// <summary>
        /// Claim the camera for this frame, and say what the crew's view state is.
        ///
        /// Called by the PAINTER, which is the only thing that knows which page is about to draw -
        /// ImageStore does not and should not. This is NOT the docking cam's arrangement, where
        /// asking for the texture is itself the request: PageState.PlanetCamLive is read out of the
        /// image store EVERY frame by VesselData, whatever page is up, so a self-claiming Texture()
        /// would keep a scaled-space camera alive for a page nobody is looking at.
        /// </summary>
        internal static void Request(double rotDeg, int zoomStep)
        {
            wantRotDeg = rotDeg;
            wantZoom = zoomStep;
            lastWanted = Time.frameCount;
        }

        /// <summary>
        /// The live render, or null when there is nothing to show - which is the honest answer
        /// whenever no page has claimed the camera, the vessel or body is not there to frame, or the
        /// scaled camera we copy has not come up yet. NavPage draws the textured disc and says
        /// LIVE 3D - NO SIGNAL for every one of those.
        /// </summary>
        internal static Texture Texture()
        {
            if (failed) return null;
            if (Time.frameCount - lastWanted > ClaimFrames) return null;   // nobody is looking

            // Same lifetime rule as the navball and the docking cam: a scene change destroys the
            // GameObjects while the flags and the RenderTexture survive, so validate rather than
            // remember.
            if (camObject == null || cam == null || target == null)
            {
                if (camObject != null || cam != null || target != null)
                {
                    Debug.Log("[DragonScreen] scaled-planet cam was torn down with the scene - rebuilding");
                    Clear();
                }
                if (!Build()) return null;
            }

            if (!Aim())
            {
                // ---- SAY WHY THERE IS NO PICTURE, ONCE. ----
                // A black rectangle and a camera aimed at nothing look identical on the glass, and
                // the crew has reported "the cameras are not working" for both. Latched so an
                // unframeable state says so once rather than every frame.
                if (!blankReported)
                {
                    blankReported = true;
                    liveReported = false;
                    Debug.LogWarning("[DragonScreen] scaled-planet cam has no picture - no vessel, "
                                   + "no main body, or no orbit plane to frame it in");
                }
                cam.enabled = false;
                return null;
            }
            blankReported = false;
            cam.enabled = true;

            // ---- ONE LINE WHEN IT COMES ALIVE, so a black render can be told from a dead camera. ----
            if (!liveReported)
            {
                liveReported = true;
                Debug.Log("[DragonScreen] scaled-planet cam live at "
                          + camObject.transform.position.ToString("F1")
                          + " facing " + camObject.transform.forward.ToString("F2")
                          + ", range " + liveDistance.ToString("F1") + " scaled units"
                          + ", fill " + liveFill.ToString("F2")
                          + ", fov " + cam.fieldOfView.ToString("F0")
                          + ", mask 0x" + cam.cullingMask.ToString("X"));
            }
            return target;
        }

        /// <summary>
        /// Switch the camera off when no page has asked for a while. Called from the painter's
        /// update, which runs whether or not the 3D PLANET view is on screen.
        /// </summary>
        internal static void Idle()
        {
            if (cam != null && cam.enabled && Time.frameCount - lastWanted > IdleFrames)
                cam.enabled = false;
        }

        /// <summary>
        /// Build the camera, or say it is not buildable YET.
        ///
        /// NOT LATCHED WITH A `tried` FLAG, unlike the docking cam. This one depends on ScaledCamera
        /// being up, which it is not during a scene load, and latching would mean a camera that
        /// happened to be asked for one frame early never existed again for the rest of the session.
        /// A missing scaled camera is a NOT YET, so we return false and are asked again next frame.
        /// `failed` is reserved for a real exception, which is a NEVER.
        /// </summary>
        private static bool Build()
        {
            ScaledCamera sc = ScaledCamera.Instance;
            Camera src = (sc != null) ? sc.cam : null;
            if (src == null) return false;

            try
            {
                target = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32);
                target.antiAliasing = 2;
                target.Create();

                camObject = new GameObject("DragonScreenScaledPlanetCam");
                cam = camObject.AddComponent<Camera>();

                // ---- THE COPY IS THE WHOLE POINT - see the header. Everything below it is an
                // override we mean; the culling mask and the clip planes are deliberately left alone.
                cam.CopyFrom(src);

                cam.enabled = false;
                cam.targetTexture = target;
                cam.clearFlags = CameraClearFlags.SolidColor;
                // Black, not the skybox: the galaxy is the OTHER camera in KSP's rig and is not ours
                // to render. Space behind the globe is black, which is honest and is what the NAV
                // well's dark inset already is.
                cam.backgroundColor = Color.black;
                cam.eventMask = 0;
                cam.depth = -103f;               // after the navball (-101) and the docking cam
                                                 // (-102), before the screen cameras that draw this
                // CopyFrom brings the source camera's viewport, aspect and any explicit projection
                // matrix with it. Ours renders a whole RenderTexture through our own lens, so all
                // three go back to the plain case before the fov is set - otherwise a fov change
                // would be silently ignored under an inherited projection matrix.
                cam.rect = new Rect(0f, 0f, 1f, 1f);
                cam.ResetProjectionMatrix();
                cam.ResetAspect();
                cam.fieldOfView = (float)FovDeg;

                // Re-aim immediately before this camera culls, so the view is never a frame stale -
                // the docking cam's flight_0821_060847 lesson, which cost a visible slide on load.
                camObject.AddComponent<ScaledPlanetAimer>();

                Debug.Log("[DragonScreen] scaled-planet cam ready: " + Width + "x" + Height
                          + ", copied from ScaledCamera, cullingMask 0x" + cam.cullingMask.ToString("X")
                          + ", clip " + cam.nearClipPlane.ToString("F3")
                          + ".." + cam.farClipPlane.ToString("F0"));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] scaled-planet cam setup failed: " + e.Message);
                failed = true;
                return false;
            }
        }

        /// <summary>
        /// Put the camera where PlanetGeom says. Returns false when the geometry cannot describe a
        /// placement, which the caller reports once rather than leaving a black rectangle.
        ///
        /// EVERY DECISION HERE WAS MADE IN PURE CODE. This reads KSP, converts at the boundary, and
        /// applies the transform PlanetGeom.Frame hands back; the azimuth, the pitch, the distance
        /// solve and the up vector are all its, and all tested headlessly.
        /// </summary>
        private static bool Aim()
        {
            // The vessel the numbers on the page describe - FlightGlobals.ActiveVessel, the same
            // source VesselData reads, so the picture and the readouts cannot disagree about which
            // vehicle's orbit is being drawn.
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return false;

            CelestialBody b = v.mainBody;
            if (b == null || b.Radius <= 0.0) return false;
            if (b.scaledBody == null) return false;
            Transform sb = b.scaledBody.transform;
            if (sb == null) return false;

            // The globe's centre is the scaled body's OWN transform - that is where the render puts
            // it - and its radius is the world radius at the same scale.
            ScaledVec centre = Vec(sb.position);
            double radius = b.Radius * ScaledSpace.InverseScaleFactor;
            if (radius <= 0.0) return false;

            ScaledVec vessel = Vec(ScaledSpace.LocalToScaledSpace(v.CoMD));

            // The orbit plane, from world-space position and velocity about the body. A DIRECTION
            // needs no scaling - scaled space is a uniform shrink with no rotation - so this is the
            // scaled-space normal as it stands. Landed, or with no orbit at all, it is degenerate and
            // PlanetGeom falls back to the body's north axis, which is the honest stand-in.
            ScaledVec normal = new ScaledVec(0.0, 0.0, 0.0);
            if (v.orbit != null)
            {
                Vector3d r = v.CoMD - b.position;
                normal = Vec(Vector3d.Cross(r, v.obt_velocity));
            }

            PlanetCamFrame f;
            if (!PlanetGeom.Frame(centre, radius, vessel, normal, Vec(b.RotationAxis),
                                  wantRotDeg + AzimuthTrimDeg, PitchTrimDeg,
                                  wantZoom, FovDeg, out f))
                return false;

            // A tunable fov can move under us between frames; the lens follows it here rather than
            // only at Build, so the distance solve above and the projection always agree.
            cam.fieldOfView = (float)FovDeg;

            camObject.transform.position = V3(f.Eye);
            camObject.transform.rotation = Quaternion.LookRotation(V3(f.Forward), V3(f.Up));
            liveDistance = f.Distance;
            liveFill = f.Fill;
            return true;
        }

        private static ScaledVec Vec(Vector3 v) { return new ScaledVec(v.x, v.y, v.z); }
        private static ScaledVec Vec(Vector3d v) { return new ScaledVec(v.x, v.y, v.z); }
        private static Vector3 V3(ScaledVec v)
        { return new Vector3((float)v.X, (float)v.Y, (float)v.Z); }

        /// <summary>
        /// Re-point the camera immediately before it renders - see Build. Only runs when the camera
        /// is live; a disabled camera never culls.
        /// </summary>
        internal static void AimForRender()
        {
            if (cam == null || camObject == null || !cam.enabled) return;
            try { Aim(); }
            catch (Exception e)
            { Debug.LogWarning("[DragonScreen] scaled-planet cam re-aim failed: " + e.Message); }
        }

        internal static void Clear()
        {
            if (cam != null) cam.targetTexture = null;
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); target = null; }
            if (camObject != null) { UnityEngine.Object.Destroy(camObject); camObject = null; }
            cam = null;
            failed = false;
            blankReported = false; liveReported = false;
        }
    }

    /// <summary>
    /// Sits on the scaled-planet camera's GameObject and re-points it in OnPreCull - the last
    /// callback before that camera culls and renders - so the view is never a frame stale. The same
    /// component the docking cam carries, for the same reason. Destroyed with the camObject in
    /// Clear().
    /// </summary>
    internal sealed class ScaledPlanetAimer : MonoBehaviour
    {
        private void OnPreCull() { ScaledPlanetRenderer.AimForRender(); }
    }
}
