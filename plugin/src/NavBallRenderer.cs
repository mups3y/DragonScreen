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
 * ---- THE FOUR MARKERS ARE MAS's TOO, PORTED VERBATIM ----
 * PROGRADE, RETROGRADE, TARGET and ANTI-TARGET only - the docking page already carries range, rate
 * and the attitude numbers, so radial/normal/maneuver would be clutter the reference navball does
 * not show here. Each is a flat quad on layer 29, textured from the STOCK atlas
 * `Squad/Props/IVANavBall/ManeuverNode_vectors` (white glyphs, shape in the alpha channel, verified
 * over the file), tinted the stock colours and faded as its direction rotates to the far side.
 *
 * MASPageNavBall.cs:InitMarkers/MakeMarker/CameraPrerender and its markerUV/markerColor tables are
 * the source for every constant. The one adaptation: MAS toggles its markers inside camera callbacks
 * because its layer 29 is shared with other MAS cameras; ours is a private RenderTexture that nothing
 * else renders, so the quads simply stay enabled and are re-placed each frame, exactly like the ball.
 * The placement uses navballAttitudeGimbal - the SAME `attitude` quaternion Orient() already builds
 * for the ball - so markers and ball cannot drift apart.
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

        // ---- THE MARKERS. Every number below is MAS's (MASPageNavBall.cs), not chosen. ----
        private enum Marker { Prograde, Retrograde, Target, AntiTarget }
        private const int MarkerCount = 4;

        /// <summary>orthographicSize, AND the radius directions are scaled to so a marker sits on the
        /// limb. MASPageNavBall.cs:218 and the *navballExtents at :337.</summary>
        private const float BallExtent = 1.01f;

        /// <summary>Icon half-size as a fraction of the ball. MASPageNavBall.cs:478 (0.18 * extent).</summary>
        private const float IconExtent = BallExtent * 0.18f;

        /// <summary>Fixed z in FRONT of the ball toward the camera, so a marker is never inside it.
        /// MASPageNavBall.cs:98.</summary>
        private const float IconDepth = 1.4f - BallExtent - 0.01f;

        /// <summary>Maps the scaled direction's z to alpha, so a marker on the far side fades out.
        /// MASPageNavBall.cs:99, applied at :279.</summary>
        private const float IconAlphaScalar = 0.6f / BallExtent;

        private static GameObject[] markerObj;
        private static Material[] markerMat;
        private static MeshRenderer[] markerRend;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int TintId = Shader.PropertyToID("_TintColor");

        /// <summary>Stock marker colours, MASPageNavBall.cs:436: prograde yellow, target magenta.</summary>
        private static readonly Color[] MarkerColour =
        {
            new Color(1f, 0.796f, 0f, 1f),   // Prograde    255,203,0
            new Color(1f, 0.796f, 0f, 1f),   // Retrograde
            new Color(1f, 0f, 1f, 1f),       // Target      255,0,255
            new Color(1f, 0f, 1f, 1f),       // AntiTarget
        };

        /// <summary>Lower-left origin of each glyph in the 3x3 stock atlas. MASPageNavBall.cs:419.</summary>
        private static readonly Vector2[] MarkerUV =
        {
            new Vector2(0f / 3f, 2f / 3f),   // Prograde
            new Vector2(1f / 3f, 2f / 3f),   // Retrograde
            new Vector2(2f / 3f, 2f / 3f),   // Target +
            new Vector2(2f / 3f, 1f / 3f),   // Target -
        };

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
                // The heading origin is applied as a U-offset that runs the mesh UVs past 1.0, so the
                // skin must WRAP horizontally for the closed seam to sample the same texel. navball.png
                // is used by nothing else, so setting it here is safe.
                skin.wrapMode = TextureWrapMode.Repeat;

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
                cam.orthographicSize = BallExtent; // 1% margin so the limb is never clipped
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

                BuildMarkers();

                Debug.Log("[DragonScreen] navball ready: " + Size + "x" + Size + " on layer " + Layer
                          + ", shader '" + (s != null ? s.name : "none") + "'"
                          + (markerObj != null ? ", markers on" : ", NO markers"));
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

            // The markers ride the SAME `attitude` quaternion, so they cannot drift off the ball.
            OrientMarkers(attitude, v);

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

        // ------------------------------------------------------------------ the four markers

        /// <summary>
        /// Place the markers for this frame. Ported from MASPageNavBall.CameraPrerender: a marker sits
        /// at (attitude * worldDirection) scaled to the limb, at a fixed depth in front of the ball,
        /// and its retrograde/anti-target twin is the antipode - MAS's UpdateVectorPair.
        ///
        /// Prograde follows the STOCK speed mode (Orbit/Surface/Target) so the ball reads the same as
        /// the game's own: on a docking approach that is Target mode - velocity relative to the station
        /// - which is the vector the pilot flies onto the magenta target marker.
        /// </summary>
        private static void OrientMarkers(Quaternion attitude, Vessel v)
        {
            if (markerObj == null) return;

            Vector3 prograde;
            switch (FlightGlobals.speedDisplayMode)
            {
                case FlightGlobals.SpeedDisplayModes.Surface:
                    prograde = v.srf_velocity.normalized; break;
                case FlightGlobals.SpeedDisplayModes.Target:
                    prograde = ((Vector3)FlightGlobals.ship_tgtVelocity).normalized; break;
                default:
                    prograde = v.obt_velocity.normalized; break;
            }
            Place(Marker.Prograde, attitude, prograde);
            Place(Marker.Retrograde, attitude, -prograde);

            ITargetable tgt = v.targetObject;
            Transform tt = (tgt != null) ? tgt.GetTransform() : null;
            if (tt != null)
            {
                Vector3 toTarget = (tt.position - v.transform.position).normalized;
                Place(Marker.Target, attitude, toTarget);
                Place(Marker.AntiTarget, attitude, -toTarget);
            }
            else
            {
                markerRend[(int)Marker.Target].enabled = false;
                markerRend[(int)Marker.AntiTarget].enabled = false;
            }

            // ---- CONFIRM ON LOAD, THEN QUIET ----
            // Markers cannot be settled offline the way the ball's orientation was (that needs the
            // world-frame vectors the preview does not model), so log where the two primary markers
            // land the first few frames: pointing AT the target, its x/y should be ~0 and FRONT.
            if (markerLogsLeft > 0 && Time.realtimeSinceStartup - markerLastLog > 2f)
            {
                markerLastLog = Time.realtimeSinceStartup;
                markerLogsLeft--;
                LogMarker("prograde", attitude, prograde);
                if (tt != null)
                    LogMarker("target", attitude, (tt.position - v.transform.position).normalized);
            }
        }

        private static void Place(Marker id, Quaternion attitude, Vector3 worldDir)
        {
            int i = (int)id;
            if (worldDir.sqrMagnitude < 1e-8f) { markerRend[i].enabled = false; return; }

            Vector3 scaled = (attitude * worldDir) * BallExtent;
            markerObj[i].transform.localPosition = new Vector3(scaled.x, scaled.y, IconDepth);

            Color c = MarkerColour[i];
            c.a = Mathf.Clamp01(scaled.z * IconAlphaScalar + 0.4f);   // MASPageNavBall.cs:279
            SetMarkerColour(markerMat[i], c);
            markerRend[i].enabled = true;
        }

        private static float markerLastLog = -999f;
        private static int markerLogsLeft = 3;

        private static void LogMarker(string name, Quaternion attitude, Vector3 dir)
        {
            Vector3 s = (attitude * dir) * BallExtent;
            Debug.Log("[DragonScreen] navball marker " + name + " x/y = " + s.x.ToString("F2")
                      + " / " + s.y.ToString("F2") + " (limb +/-" + BallExtent.ToString("F2")
                      + "), alpha " + Mathf.Clamp01(s.z * IconAlphaScalar + 0.4f).ToString("F2")
                      + (s.z >= 0f ? " FRONT" : " BACK"));
        }

        /// <summary>
        /// The four direction markers, as flat quads parented to the camera and lit from the stock
        /// navball atlas. A missing atlas or shader is a state, not a crash: the ball keeps working
        /// without markers, exactly as it keeps working without its own texture.
        /// </summary>
        private static void BuildMarkers()
        {
            Texture atlas = (GameDatabase.Instance != null)
                ? GameDatabase.Instance.GetTexture("Squad/Props/IVANavBall/ManeuverNode_vectors", false)
                : null;
            if (atlas == null)
            {
                Debug.LogWarning("[DragonScreen] no navball marker atlas - prograde/target markers off");
                return;
            }

            // The glyphs are WHITE with the shape in the alpha channel, so an alpha-blended shader that
            // MULTIPLIES by a tint turns them the marker colour. Sprites/Default does exactly that.
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("KSP/Alpha/Unlit Transparent");
            if (s == null) s = Shader.Find("Unlit/Transparent");
            if (s == null)
            {
                Debug.LogWarning("[DragonScreen] no alpha shader for navball markers - markers off");
                return;
            }

            markerObj = new GameObject[MarkerCount];
            markerMat = new Material[MarkerCount];
            markerRend = new MeshRenderer[MarkerCount];
            for (int i = 0; i < MarkerCount; i++) markerObj[i] = MakeMarker(i, s, atlas);
        }

        /// <summary>One marker quad. MASPageNavBall.cs:MakeMarker - same UVs, same winding.</summary>
        private static GameObject MakeMarker(int i, Shader shader, Texture atlas)
        {
            GameObject o = new GameObject("DragonScreenNavBallMarker" + i);
            o.layer = Layer;
            o.transform.parent = camObject.transform;
            o.transform.localPosition = new Vector3(0f, 0f, IconDepth);

            Material m = new Material(shader);
            m.mainTexture = atlas;
            SetMarkerColour(m, MarkerColour[i]);

            MeshFilter mf = o.AddComponent<MeshFilter>();
            MeshRenderer mr = o.AddComponent<MeshRenderer>();
            mr.material = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            Vector2 uv0 = MarkerUV[i];
            Vector2 uv1 = uv0 + new Vector2(1f / 3f, 1f / 3f);
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-IconExtent,  IconExtent, 0f),
                new Vector3( IconExtent,  IconExtent, 0f),
                new Vector3(-IconExtent, -IconExtent, 0f),
                new Vector3( IconExtent, -IconExtent, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(uv0.x, uv1.y),
                uv1,
                uv0,
                new Vector2(uv1.x, uv0.y),
            };
            mesh.triangles = new[] { 0, 3, 2, 0, 1, 3 };
            mesh.RecalculateBounds();
            mf.mesh = mesh;

            markerMat[i] = m;
            markerRend[i] = mr;
            return o;
        }

        /// <summary>Set whichever tint property the chosen shader exposes - _Color on Sprites/Default,
        /// _TintColor on the particle shaders - so the colour and its alpha take either way.</summary>
        private static void SetMarkerColour(Material m, Color c)
        {
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
            if (m.HasProperty(TintId)) m.SetColor(TintId, c);
        }

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

                    // ---- STOCK KSP NAVBALL: STANDARD EQUIRECTANGULAR, LONGITUDE FLIPPED ----
                    // art/navball.png is now Squad's IVANavBall (the brown-ground/blue-sky ball), which
                    // IS an ordinary equirectangular navball - blue in the top rows, brown in the
                    // bottom, heading uniform across. Latitude maps straight through (v), so the sky
                    // sits at the +Y pole and the ground at -Y.
                    //
                    // Longitude is flipped (1 - u): our generated sphere winds theta the opposite way
                    // to the texture, so the plain (u, v) unwrap renders every glyph MIRRORED. Flipping
                    // u un-mirrors the digits and leaves sky up where it is. Chosen by rendering all
                    // candidates offline in `navball_preview.py` (the "stock" mode) and reading them:
                    // it is the only one with sky up, the ladder upright, glyphs readable, and the
                    // markings sweeping LEFT under a right yaw. See that file for the sheets.
                    //
                    // ---- HEADING ORIGIN: +0.75 in U, CALIBRATED AGAINST A REAL LOGGED HEADING ----
                    // The plain (1-u) mapping put the heading tape 90 deg out - a vessel logged level
                    // on the pad at heading 90.0 read "N" at the centre of the ball, not "E". Rotating
                    // the heading axis by +0.75 (of a full wrap) lines it up: the same attitude now
                    // reads "90". Measured with `navball_preview.py` against the on-pad log line
                    // `surface pitch/heading/roll = 359.2 / 90.0 / 0.0`. A U-offset only rotates the
                    // heading origin - it cannot touch the hemispheres, the ladder, or the sweep, which
                    // stay as verified. No modulo: U runs 1.75 -> 0.75 across the mesh with no jump, and
                    // the wrapped skin (set in Build) makes the closed seam sample the same texel.
                    uvs[i] = new Vector2(1f - u + 0.75f, 1f - v);   // (1 - lon + heading origin, lat)
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
            if (markerObj != null)
            {
                for (int i = 0; i < markerObj.Length; i++)
                {
                    if (markerMat != null && markerMat[i] != null)
                        UnityEngine.Object.Destroy(markerMat[i]);
                    if (markerObj[i] != null) UnityEngine.Object.Destroy(markerObj[i]);
                }
            }
            markerObj = null; markerMat = null; markerRend = null;

            if (cam != null) cam.targetTexture = null;
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); target = null; }
            if (ballObject != null) { UnityEngine.Object.Destroy(ballObject); ballObject = null; }
            if (camObject != null) { UnityEngine.Object.Destroy(camObject); camObject = null; }
            cam = null;
            tried = false; failed = false;
        }
    }
}
