/*
 * DragonScreen - NavBallRenderer
 *
 * A REAL 3D NAVBALL, rendered by its own camera into its own RenderTexture, which the pages then draw
 * as an ordinary image.
 *
 * ---- WHY NOT REUSE NavPage.Globe ----
 * I claimed on 2026-08-06 that the globe's strip renderer was already a navball renderer. It is not,
 * and the difference is worth stating so nobody tries again. The strip technique paints an
 * equirectangular texture onto a disc using horizontal quads, which is exact ONLY when the sphere is
 * viewed from its equator: parallels are horizontal lines and the pole is straight up. A navball is
 * rotated about all THREE axes - heading, pitch and roll - and under pitch the parallels become
 * ellipse arcs, while roll tilts the whole grid. Neither survives axis-aligned quads.
 *
 * An attitude instrument that is geometrically approximate is an instrument that lies, so the answer
 * is a real sphere.
 *
 * ---- PORTED FROM MAS, WITH THE VOODOO INTACT ----
 * MASPageNavBall.cs:205-240 and MASVesselComputer.cs:611-651. Every constant here was read out of
 * that source, not guessed, because the orientation maths is the kind that looks plausible while
 * being wrong by 90 degrees:
 *
 *      layer 29                        MASPageNavBall.cs:56 - a layer KSP leaves free
 *      orthographic, aspect 1          :213-216
 *      model 2.4 in front, far 13      :218, :232
 *      Euler(90,0,0) correction        MASVesselComputer.cs:635 - the vessel transform is rotated
 *                                      90 degrees about x, so "forward" is really "up"
 *      MirrorXAxis + Euler(0,180,0)    :611-614, :650 - MAS's own comment calls this voodoo, and
 *                                      copying the REASON is impossible because there is not one;
 *                                      it is what makes a rendered ball behave like the stock one
 *
 * ---- ONE BALL, THREE SCREENS ----
 * Static and shared, like ImageStore: the ball is a property of the vessel, not of a panel, and
 * three cameras rendering three identical spheres every frame would be three times the cost for
 * pixel-identical output.
 *
 * ---- WHERE THE SPHERE COMES FROM ----
 * MAS loads a .mu model from GameDatabase. We have no navball model to load and adding one would
 * mean shipping a binary asset that a dozen lines of code can produce exactly - so the mesh is
 * generated here. It also means the UV layout is OURS and is guaranteed to match the equirectangular
 * texture, rather than depending on how someone else unwrapped their model.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    internal static class NavBallRenderer
    {
        /// <summary>The layer MAS uses for exactly this, and therefore one KSP leaves alone.</summary>
        private const int Layer = 29;

        /// <summary>Render target size. The ball is drawn at roughly 300 px; 512 leaves headroom.</summary>
        private const int Size = 512;

        /// <summary>Sphere tessellation. 32x24 is smooth at 512 px and is built once, ever.</summary>
        private const int Segments = 32, Rings = 24;

        private static GameObject ballObject, camObject;
        private static Camera cam;
        private static RenderTexture target;
        private static bool tried, failed;

        /// <summary>
        /// The navball texture, or null if it could not be set up. Called from the draw path, so
        /// everything after the first frame is a field read.
        /// </summary>
        internal static Texture Texture()
        {
            // ---- A SCENE CHANGE DESTROYS THE BALL, AND THE FLAGS DO NOT KNOW ----
            // Reported 2026-08-06: revert to the VAB, launch again, and the navball is gone.
            // ballObject and camObject are GameObjects, so Unity destroys them with the scene - but
            // `tried` stayed true and `target` stayed non-null, so this kept handing out a
            // RenderTexture that nothing was rendering into any more. A blank texture drawn
            // confidently, which is the same class of failure as the 4x4 body map.
            //
            // The fix is to VALIDATE rather than remember. Unity's == null is true for a destroyed
            // object, so asking costs nothing and is the only answer that cannot go stale. Statics
            // that outlive a scene must never trust a flag about a Unity object.
            if (tried && !failed && (ballObject == null || camObject == null || target == null))
            {
                Debug.Log("[DragonScreen] navball was torn down with the scene - rebuilding");
                Clear();
            }

            if (failed) return null;
            if (!tried) { tried = true; Build(); }
            if (target == null) return null;
            Orient();
            return target;
        }

        private static void Build()
        {
            try
            {
                Texture2D skin = ImageStore.Get(ImageId.NavBall);
                if (skin == null)
                {
                    // No texture is a real state, not a crash: DOCKING falls back to its rings.
                    Debug.LogWarning("[DragonScreen] no navball texture - the attitude ball is off");
                    failed = true;
                    return;
                }

                target = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32);
                target.antiAliasing = 4;          // same reasoning as the screens: this is all curves
                target.Create();

                // ---- THE BALL ----
                ballObject = new GameObject("DragonScreenNavBall");
                ballObject.layer = Layer;
                MeshFilter mf = ballObject.AddComponent<MeshFilter>();
                mf.mesh = Sphere(1f, Segments, Rings);

                // KSP/Unlit for the same reason the screens use it: an attitude ball must not dim
                // with the cabin, and there is no lighting rig on a private layer anyway.
                Shader s = Shader.Find("KSP/Unlit");
                if (s == null) s = Shader.Find("Unlit/Texture");
                if (s == null) s = Shader.Find("Sprites/Default");
                MeshRenderer mr = ballObject.AddComponent<MeshRenderer>();
                mr.material = new Material(s);
                mr.material.mainTexture = skin;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                // ---- THE CAMERA ----
                // Orthographic so the ball does not distort toward the edge of the frame, and
                // cullingMask limited to layer 29 so it can never pick up the world - the same
                // isolation trick the screen cameras use with cullingMask = 0.
                camObject = new GameObject("DragonScreenNavBallCam");
                camObject.layer = Layer;
                cam = camObject.AddComponent<Camera>();
                cam.enabled = true;
                cam.orthographic = true;
                cam.orthographicSize = 1.01f;      // 1% margin so the limb is never clipped
                cam.aspect = 1f;
                cam.cullingMask = 1 << Layer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                // Alpha 0 so the page shows through around the ball rather than a black square.
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 13f;
                cam.eventMask = 0;
                cam.targetTexture = target;
                cam.depth = -101f;                 // before the screen cameras, which draw it

                // MAS puts the model 2.4 in front of the camera (MASPageNavBall.cs:232).
                ballObject.transform.position = camObject.transform.position + new Vector3(0f, 0f, 2.4f);
                cam.transform.LookAt(ballObject.transform, Vector3.up);

                Debug.Log("[DragonScreen] navball ready: " + Size + "x" + Size + " on layer " + Layer
                          + ", shader '" + (s != null ? s.name : "none") + "'");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] navball setup failed: " + e.Message);
                failed = true;
            }
        }

        private static int lastFrame = -1;

        /// <summary>
        /// Point the ball the way the vessel is pointing. Once per frame however many screens ask -
        /// same guard as VesselData, and for the same reason: three screens must agree.
        /// </summary>
        private static void Orient()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || ballObject == null) return;
            Transform rt = v.ReferenceTransform;
            CelestialBody body = v.mainBody;
            if (rt == null || body == null) return;

            // MASVesselComputer.UpdateAttitude, :639-650. See the header - these lines are ported
            // verbatim because the corrections are empirical.
            Quaternion attitude = VesselOrientationCorrection * Quaternion.Inverse(rt.rotation);
            Vector3 up = (rt.position - body.position).normalized;
            Quaternion relative = attitude * Quaternion.LookRotation(
                Vector3.ProjectOnPlane(up + body.transform.up, up), up);

            ballObject.transform.rotation = NavballYRotate * MirrorXAxis(relative);

            // ---- INSTRUMENT, DO NOT THEORISE ----
            // The ball is up and turning, but whether it is turning the RIGHT WAY cannot be settled
            // from a screenshot alone: at any single attitude several wrong orientations look
            // plausible. The suspect is that MAS's quaternion was tuned against MAS's navball MODEL,
            // and ours is a mesh generated here - if that model's poles or seam differ from ours, the
            // ported voodoo lands the ball 90 degrees out.
            //
            // So log the truth beside the result: surface pitch/heading/roll (what the ball SHOULD
            // read) next to the euler angles actually applied. One screenshot plus these two lines
            // settles it, instead of guessing a correction and spending a restart per guess.
            //
            // Rate limited hard - this is diagnosis, not telemetry. Delete once the orientation is
            // confirmed.
            if (logsLeft > 0 && Time.realtimeSinceStartup - lastLog > 2f)
            {
                lastLog = Time.realtimeSinceStartup;
                logsLeft--;
                Vector3 surface = Quaternion.Inverse(relative).eulerAngles;
                Vector3 applied = ballObject.transform.rotation.eulerAngles;
                Debug.Log("[DragonScreen] navball  surface pitch/heading/roll = "
                          + surface.x.ToString("F1") + " / " + surface.y.ToString("F1") + " / "
                          + surface.z.ToString("F1")
                          + "   ball euler = " + applied.x.ToString("F1") + " / "
                          + applied.y.ToString("F1") + " / " + applied.z.ToString("F1"));
            }
        }

        private static float lastLog = -999f;

        /// <summary>
        /// Three lines, then quiet. The orientation is settled offline now, so this is a confirmation
        /// on each load rather than a running diagnosis - and it was filling the log at 0.5 Hz.
        /// </summary>
        private static int logsLeft = 3;

        private static readonly Quaternion VesselOrientationCorrection = Quaternion.Euler(90f, 0f, 0f);
        private static readonly Quaternion NavballYRotate = Quaternion.Euler(0f, 180f, 0f);

        /// <summary>MASVesselComputer.cs:611-614, unchanged.</summary>
        private static Quaternion MirrorXAxis(Quaternion q)
        {
            return new Quaternion(q.x, -q.y, -q.z, q.w);
        }

        /// <summary>
        /// A UV sphere whose texture coordinates are EQUIRECTANGULAR - u around, v pole to pole -
        /// which is the layout the navball texture is authored in and the same one the NAV map uses.
        ///
        /// Generated rather than shipped: a dozen lines beat a binary asset whose unwrap we would
        /// have to trust, and it guarantees the UVs match the texture.
        /// </summary>
        /// <summary>
        /// The texture carries a light border about 6 px wide on all four sides. Latitude now runs
        /// along that axis, so the border lands ON the poles and smears across the whole ball as a
        /// bright streak. Costs a third of a degree of ladder to stay off it.
        /// </summary>
        private const float PoleInset = 8f / 2048f;

        private static Mesh Sphere(float radius, int segments, int rings)
        {
            int vertCount = (segments + 1) * (rings + 1);
            Vector3[] verts = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            Vector3[] normals = new Vector3[vertCount];

            for (int y = 0; y <= rings; y++)
            {
                float v = (float)y / rings;
                float phi = v * Mathf.PI;                 // 0 at the north pole
                for (int x = 0; x <= segments; x++)
                {
                    float u = (float)x / segments;
                    float theta = u * Mathf.PI * 2f;
                    Vector3 p = new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta),
                        Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta));
                    int i = y * (segments + 1) + x;
                    verts[i] = p * radius;
                    normals[i] = p;

                    // ---- THE TEXTURE IS STORED TRANSPOSED, SO THE UNWRAP IS TOO ----
                    // navball_edited.png is NOT laid out like an ordinary equirectangular navball.
                    // Measured over the file rather than eyeballed (plugin/build/navball_preview.py):
                    //
                    //      light/dark halves split across its U axis, not its V   (48.9 vs 117.5 mean
                    //          left/right, 82.7 vs 83.7 top/bottom - no latitude split at all)
                    //      rows y=16..2032 are pixel-identical, so content barely varies with V
                    //      every glyph is rotated 90 degrees
                    //
                    // That is a standard navball stored on its side: its U axis is PITCH and its V
                    // axis is HEADING. Feeding it the ordinary (longitude, latitude) unwrap put the
                    // horizon on a meridian, so the ball split vertically and rolled with heading -
                    // which is exactly what the first flight showed.
                    //
                    // Longitude is mirrored as well as swapped. Un-mirroring by flipping LATITUDE
                    // instead also turns the ball upside down; flipping longitude fixes the glyph
                    // handedness and leaves up where it is. All four combinations were rendered
                    // offline and this is the only one with sky up, the ladder upright, readable
                    // glyphs, and the markings sweeping the correct way under yaw.
                    float lat01 = 1f - v;                       // 1 at the +Y pole, 0 at -Y
                    uvs[i] = new Vector2(PoleInset + lat01 * (1f - 2f * PoleInset), 1f - u);
                }
            }

            int[] tris = new int[segments * rings * 6];
            int t = 0;
            for (int y = 0; y < rings; y++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int a = y * (segments + 1) + x;
                    int b = a + segments + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            }

            Mesh m = new Mesh();
            m.vertices = verts;
            m.uv = uvs;
            m.normals = normals;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        internal static void Clear()
        {
            if (cam != null) cam.targetTexture = null;
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); target = null; }
            if (ballObject != null) { UnityEngine.Object.Destroy(ballObject); ballObject = null; }
            if (camObject != null) { UnityEngine.Object.Destroy(camObject); camObject = null; }
            cam = null;
            tried = false; failed = false;
        }
    }
}
