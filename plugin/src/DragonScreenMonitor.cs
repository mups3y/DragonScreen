/*
 * DragonScreen - DragonScreenMonitor
 *
 * THE GLUE. One instance per physical display. It owns a RenderTexture, hangs it on the IVA screen
 * mesh's material, and points an off-screen camera at it so something is allowed to draw there.
 * It does no arithmetic - anything that decides where a pixel lands belongs in src/pure, headless
 * tested, because "THE BUG IS IN THE GLUE, AND THE GLUE IS THE PART WITH NO TESTS."
 *
 * ---- PORTED FROM MAS, NOT INVENTED ----
 * assets/reference/AvionicsSystems-master/Source/MASMonitor.cs:212-222 is the integration, verbatim
 * in shape:
 *      Transform t = internalProp.FindModelTransform(screenTransform);
 *      Material m = t.GetComponent<Renderer>().material;
 *      m.SetTexture(slot, renderTexture);
 * MIT licensed. RasterPropMonitor does the same thing. This is the known-good mechanism for putting
 * live content on a KSP prop, and it is why the Tundra prop being `internalGeneric` costs us nothing:
 * the geometry is already correct, it just has a dead texture in it.
 *
 * ---- WHY A CAMERA AND NOT A BARE RenderTexture.active ----
 * GL immediate mode is only legal where Unity has a render target bound and a frame in flight.
 * Setting RenderTexture.active from Update and hoping works until it doesn't. A Camera whose
 * targetTexture IS our RenderTexture gives us OnPostRender - a callback that fires every frame, with
 * the RenderTexture already active, at a point Unity documents as valid for GL. MAS uses a camera for
 * the same reason (it renders real GameObjects on a private layer; we render nothing and paint by
 * hand, which is the IMGUI+GL decision applied).
 * cullingMask = 0 on purpose: this camera must never pick up scene geometry. It exists to clear the
 * texture and to give ScreenPainter a legal place to stand.
 *
 * ---- WHAT THIS FILE DELIBERATELY DOES NOT DO ----
 * No page logic, no state, no input. This is the render path and nothing else. Adding "just one"
 * value read here is how the glue grows past the point where it can be reasoned about by eye.
 */
using System;
using System.Text;
using UnityEngine;

namespace DragonScreen
{
    public class DragonScreenMonitor : InternalModule
    {
        /// <summary>Name of the transform inside the prop model whose material we take over.</summary>
        [KSPField] public string screenTransform = "";

        /// <summary>
        /// Material texture slot. The Tundra screens are shader KSP/Diffuse, so _MainTex - NOT
        /// _Emissive, which is what MAS defaults to because ASET's props are emissive. Wrong slot
        /// means a silently unchanged screen, so it is a cfg field and it is logged.
        /// </summary>
        [KSPField] public string textureSlot = "_MainTex";

        /// <summary>Render target width in pixels. THE ONLY RESOLUTION NUMBER THAT IS CHOSEN.</summary>
        [KSPField] public int screenWidth = 1280;

        /// <summary>
        /// Render target height. LEAVE IT AT 0 - it is MEASURED from the screen mesh.
        ///
        /// 1024x640 (1.6:1, the Figma reference aspect) flew first and the proof pattern's circle came
        /// out a visibly wide ellipse: these screens are nearer 2:1. A hand-typed height is a second
        /// number that has to be kept in step with a shape nobody can measure by eye - the same trap
        /// ScreenLayout.PanelHeight already exists to close. Set it non-zero only to OVERRIDE the
        /// measurement, e.g. if the mesh bounds turn out to include a bezel.
        /// </summary>
        [KSPField] public int screenHeight = 0;

        /// <summary>
        /// IDENTITY, NOT ROLE. 1, 2 or 3.
        ///
        /// All three screens are the same screen (CLAUDE.md, "ONE SCREEN, FOUR SURFACES"), so nothing
        /// about a screen's BEHAVIOUR may branch on this. It exists to be the key the crew's page
        /// selection is stored under, and - for now - to let the proof pattern tell the displays apart.
        /// </summary>
        [KSPField] public int screenIndex = 0;

        /// <summary>
        /// Page this screen shows on a vessel that has never been touched. A STARTING SELECTION, not
        /// a fixed assignment - the crew can put any page on any screen, exactly as the real capsule
        /// does. See docs/REAL_DRAGON_SCREENS.md for the three defaults and the reasoning.
        ///
        /// NOT YET WIRED TO ANYTHING - there is no page router. It is here because the choice is
        /// decided and the cfg is where it belongs; the router will read it on first run and then the
        /// persisted selection takes over. Logged at startup so the cfg can be verified before any of
        /// it can draw.
        /// </summary>
        [KSPField] public string defaultPage = "";

        /// <summary>
        /// Log every transform in the prop, with its local position, once at startup.
        ///
        /// The buttons live in this same prop, and their names (`TE_CD2_PROP_BUTTON_1..8`,
        /// `CD2_PROP_BUT1..10` with `_2.._5` variants) do not say which physical button they are.
        /// The POSITION does: sort by X and the order matches the labels read off the console, left
        /// to right. That turns a guess into one game start.
        ///
        /// Set it on ONE module only - three copies of the same dump is just noise.
        /// </summary>
        [KSPField] public bool dumpTransforms = false;

        /// <summary>
        /// MSAA samples on the render target: 1 (off), 2, 4 or 8.
        ///
        /// NOT COSMETIC POLISH - this design is arcs and hairlines and almost nothing else, and an
        /// aliased arc is the single most obvious way for it to look wrong. Measured off the in-game
        /// close-ups 2026-08-05: the stair-steps on the proof arc are exactly ONE render-target pixel.
        ///
        /// **THE TESSELLATION IS NOT THE PROBLEM AND MUST NOT BE "FIXED".** At 2 degrees per segment
        /// and r = 150 px the arc's sagitta - how far the flat chord sits from the true curve - is
        /// 0.02 px. Subdividing further would cost vertices every frame on three screens and change
        /// nothing visible. The jagged edge is the rasteriser, so the fix is samples, not geometry.
        ///
        /// MSAA works here because a camera renders into this target; it would not if we were
        /// blitting. Drop to 1 if it ever costs frames.
        /// </summary>
        [KSPField] public int antiAliasing = 4;

        /// <summary>
        /// Write each screen's render target to
        /// <c>&lt;KSP&gt;/DragonScreen_capture/screenN.png</c> shortly after it goes live.
        ///
        /// THE CHEAPEST WAY TO SEE WHAT THE SCREEN ACTUALLY DREW. A photograph of the cabin costs the
        /// user framing, cropping and uploading, and then shows the page through perspective, cabin
        /// lighting and Deferred. This is the texture itself. Load in, and the PNG is on disk.
        /// </summary>
        [KSPField] public bool captureOnLoad = false;

        /// <summary>
        /// Optional key that captures the screens at any moment - for the frames that only happen
        /// during a burn, an approach or entry, which no auto-capture can be scheduled for.
        ///
        /// EMPTY BY DEFAULT AND IT STAYS THAT WAY. KSP has very few genuinely free keys and silently
        /// stealing one from the player's muscle memory is a rotten thing for a mod to do. A name
        /// that is not a UnityEngine.KeyCode is ignored with a warning rather than throwing.
        /// </summary>
        [KSPField] public string captureKey = "";

        /// <summary>
        /// Font family, by its WINDOWS-INSTALLED name. D-DIN is the family the real capsule uses and
        /// is bundled under the OFL in assets/d-din, but Unity's CreateDynamicFontFromOSFont only
        /// sees installed fonts - so this defaults to something that certainly exists, and the
        /// painter LOGS what was actually resolved. An uninstalled name silently substitutes.
        /// </summary>
        [KSPField] public string fontName = "Arial";

        /// <summary>
        /// Attach logging probes to the console collider and to one button, then report every mouse
        /// event. Development scaffolding - ONE MODULE ONLY, and off by default.
        ///
        /// It answers the one question the input design turns on: Tundra's single console collider
        /// ENCLOSES every button, and Unity delivers OnMouseDown to whatever a ray strikes first, so
        /// a nested per-button collider may never receive an event at all. See InputProbe.
        /// </summary>
        [KSPField] public bool probeInput = false;

        /// <summary>
        /// Button to fit a test collider to. Defaults to the LEFT panel's CANCEL - an unmistakable
        /// position on the console, so the log and the mouse pointer cannot be confused about which
        /// thing was clicked.
        /// </summary>
        [KSPField] public string probeButton = "CD2_PROP_BUT1";

        private KeyCode ParseKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return KeyCode.None;
            try
            {
                return (KeyCode)Enum.Parse(typeof(KeyCode), name.Trim(), true);
            }
            catch
            {
                Debug.LogWarning(Tag + "captureKey '" + name + "' is not a UnityEngine.KeyCode name "
                                     + "- no capture key bound");
                return KeyCode.None;
            }
        }

        private RenderTexture screen;
        private GameObject cameraObject;
        private Camera screenCamera;

        // Resolution actually used, after measurement. screenWidth/screenHeight stay as the cfg said.
        private int rtW, rtH;

        private string Tag { get { return "[DragonScreen:" + screenTransform + "] "; } }

        public void Start()
        {
            // Props exist in the editor's IVA preview too. Only flight has a vessel worth drawing.
            if (!HighLogic.LoadedSceneIsFlight) return;

            // ---- BRING THE TUNABLES UP (S44) ----
            // Tuning catalogues every [Tunable] field, writes PluginData/tuning.reference.cfg and
            // applies PluginData/tuning.cfg. It has to be CALLED, and until S44 nothing called it -
            // so tuning.cfg was silently inert and every [Tunable] sat pinned to its code default.
            // Here because this is the first DragonScreen code to run in a flight scene. Build()
            // opens with `if (built) return;`, so the three screen modules on the prop cost one
            // build, and it catches its own exceptions - a tuning failure must never take a screen
            // with it, which is also why it sits outside the try below rather than inside it.
            Tuning.Build();

            // ---- REGISTER WITH KERBAL ENGINEER (S46) ----
            // KER computes nothing unless its processor is BOTH registered with the flight scene's
            // FlightEngineerCore and asked to update; VesselData.Refresh() does the asking at 5 Hz, and this
            // is the registration. Here for the same reason Tuning.Build() is: first DragonScreen code in a
            // flight scene. It is idempotent by core identity, so the three screen modules on the prop cost
            // one registration - and if FlightEngineerCore has not woken yet, this is a no-op and the 5 Hz
            // path retries until it has. Outside the try below, and catching its own exceptions, because a
            // missing optional mod must never take a screen with it.
            KerBridge.Attach();

            // FAIL SOFT AND SAY SO. A throw here takes the prop - and possibly the whole IVA - with
            // it, and the failure mode we actually expect (a transform name that is subtly wrong) is
            // one where the game is otherwise perfectly healthy. Log, dump what IS there, carry on.
            try
            {
                Init();
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "init failed, screen left as static art: " + e);
            }
        }

        private void Init()
        {
            if (dumpTransforms)
                Debug.Log(Tag + "prop contents dump (dumpTransforms=true):\n" + DumpTransforms());

            if (probeInput) AttachProbes();

            // The lower console. Guarded internally so it is built once per PROP even though this
            // Init runs once per SCREEN module on that prop.
            PanelButtons.Attach(internalProp);

            if (string.IsNullOrEmpty(screenTransform))
            {
                Debug.LogError(Tag + "no screenTransform in the cfg - nothing to draw on");
                return;
            }

            Transform t = internalProp.FindModelTransform(screenTransform);
            if (t == null)
            {
                Debug.LogError(Tag + "transform not found. What the prop actually contains:\n"
                                   + DumpTransforms());
                return;
            }

            Renderer r = t.GetComponent<Renderer>();
            if (r == null)
            {
                Debug.LogError(Tag + "transform found but it has no Renderer - wrong node?");
                return;
            }

            // Report the material BEFORE touching it. Deferred (zzz_Deferred) and TexturesUnlimited
            // are both installed and both rewrite shaders on load; if the slot we are about to write
            // no longer exists, this line is the difference between a two-minute fix and an evening.
            Material mat = r.material;   // NOTE: .material instantiates a per-renderer copy, which is
                                         // what we want - the three screens share one source material
                                         // and must not share one texture.
            Debug.Log(Tag + "material '" + mat.name + "' shader '" + mat.shader.name
                          + "' hasSlot(" + textureSlot + ")=" + mat.HasProperty(textureSlot));

            ResolveResolution(t);

            screen = new RenderTexture(rtW, rtH, 0, RenderTextureFormat.ARGB32);
            screen.name = "DragonScreenRT_" + screenIndex;
            // Only 1/2/4/8 are legal; anything else throws or is silently ignored depending on the
            // platform, so clamp rather than trust the cfg.
            screen.antiAliasing = (antiAliasing == 2 || antiAliasing == 4 || antiAliasing == 8)
                                  ? antiAliasing : 1;
            screen.wrapMode = TextureWrapMode.Clamp;
            // Bilinear, not point: the RT is sampled by a 3D mesh at an angle, so point sampling
            // would alias every hairline the moment the head moves.
            screen.filterMode = FilterMode.Bilinear;
            screen.autoGenerateMips = false;
            if (!screen.IsCreated()) screen.Create();

            cameraObject = new GameObject("DragonScreenCam_" + screenIndex);
            // Parked far from the origin and from anything KSP cares about. It renders nothing, but a
            // camera sitting inside the vessel is the kind of thing another mod's OnPreCull hook will
            // trip over.
            cameraObject.transform.position = new Vector3(0f, 0f, 0f);

            screenCamera = cameraObject.AddComponent<Camera>();
            screenCamera.enabled = true;                 // enabled == "render me every frame"
            screenCamera.orthographic = true;
            screenCamera.orthographicSize = 1f;
            screenCamera.aspect = (float)rtW / (float)rtH;
            screenCamera.cullingMask = 0;                // see header: draws NO scene geometry
            screenCamera.eventMask = 0;                  // and receives no mouse events
            screenCamera.clearFlags = CameraClearFlags.SolidColor;
            screenCamera.backgroundColor = ColorBridge.To(DragonPalette.Background);
            screenCamera.nearClipPlane = 0.1f;
            screenCamera.farClipPlane = 10f;
            screenCamera.depth = -100f;                  // renders before the flight cameras
            screenCamera.targetTexture = screen;

            ScreenPainter painter = cameraObject.AddComponent<ScreenPainter>();
            // Persistence lives on the PART, not here - see DragonScreenState for why.
            DragonScreenState st = DragonScreenState.FindOn(internalProp.part);
            if (st == null)
                Debug.LogWarning(Tag + "no DragonScreenState on this part - screens will work but "
                                     + "will FORGET the selected page. Is the ModuleManager patch "
                                     + "landing on this pod?");

            int defIdx = PageSelection.IndexOfName(ChromeBar.PageNames, defaultPage);
            if (defIdx < 0)
            {
                if (!string.IsNullOrEmpty(defaultPage))
                    Debug.LogWarning(Tag + "defaultPage '" + defaultPage + "' is not a page name - "
                                         + "using FLIGHT");
                defIdx = 0;
            }

            painter.Configure(rtW, rtH, screenIndex, captureOnLoad, ParseKey(captureKey), fontName,
                              st, defIdx);

            // TOUCH. On the SCREEN's own GameObject, so it inherits the prop layer exactly as the
            // probe proved works (layer 16, and a nested collider wins the hit over Tundra's
            // enclosing console MeshCollider).
            ScreenTouch touch = t.gameObject.AddComponent<ScreenTouch>();
            touch.Configure(painter, rtW, rtH, screenIndex);

            mat.SetTexture(textureSlot, screen);

            Debug.Log(Tag + "live: " + rtW + "x" + rtH + " RenderTexture, MSAA x" + screen.antiAliasing
                          + ", slot " + textureSlot + ", screen index " + screenIndex
                          + ", default page '" + defaultPage + "'");
        }

        /// <summary>
        /// Decide the render target size. WIDTH IS CHOSEN, HEIGHT IS MEASURED OFF THE MESH.
        ///
        /// The screen is a flat quad, so its bounding box has one near-zero extent and two real ones,
        /// and their ratio is the shape the texture has to be to come out undistorted. Measuring beats
        /// typing a number because the first typed number - 1.6:1, taken from the Figma reference art -
        /// was wrong, and the way it was wrong (everything subtly stretched) is invisible in a layout
        /// and obvious only in a circle.
        ///
        /// The arithmetic is in src/pure and headless tested. This method only reads Unity types and
        /// hands them over, which is the whole pure/glue split.
        /// </summary>
        private void ResolveResolution(Transform t)
        {
            if (screenWidth < 16) screenWidth = 16;
            rtW = screenWidth;

            // An explicit cfg height wins. It exists so a bad measurement can be corrected without a
            // rebuild - but it is off by default, because a default that needs maintaining is the bug.
            if (screenHeight > 0)
            {
                rtH = screenHeight;
                Debug.Log(Tag + "height " + rtH + " taken from the cfg, mesh NOT measured");
                return;
            }

            float aspect = 0f;
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 size = mf.sharedMesh.bounds.size;
                Vector3 ls = t.lossyScale;
                aspect = ScreenLayout.AspectFromExtents(size.x * ls.x, size.y * ls.y, size.z * ls.z);
                Debug.Log(Tag + "mesh extents " + size.ToString("F4") + " scale " + ls.ToString("F3")
                              + " -> aspect " + aspect.ToString("F4"));
            }
            else
            {
                Debug.LogWarning(Tag + "no MeshFilter/sharedMesh - cannot measure the screen shape");
            }

            rtH = ScreenLayout.PixelHeightFor(rtW, aspect);
            if (rtH <= 0)
            {
                // Fall back to the reference art's aspect rather than refusing to draw. A stretched
                // screen is a bad screen; no screen is a broken mod.
                rtH = ScreenLayout.PixelHeightFor(rtW, ScreenLayout.RefAspect);
                Debug.LogWarning(Tag + "measurement failed, falling back to the reference aspect "
                                     + ScreenLayout.RefAspect.ToString("F4") + " -> " + rtW + "x" + rtH);
            }
        }

        /// <summary>
        /// THE DECISIVE EXPERIMENT, and it is deliberately two probes, not one.
        ///
        /// Probe A goes on Tundra's existing console collider. Probe B goes on one button, which has
        /// no collider, so a BoxCollider is added first. Click that button in game and the log says
        /// which one received it - or whether both did.
        ///
        ///     only A fires  -> the enclosing box steals every hit; per-button colliders are dead and
        ///                      the input path must raycast ourselves, or disable/shrink that box
        ///     B fires       -> nested colliders work; a collider per button is the design
        ///
        /// AddComponent&lt;BoxCollider&gt; auto-fits the mesh on a GameObject that has a MeshFilter, so
        /// the size is not hand-derived - but it IS logged, because "auto-fits" is a claim about
        /// Unity's behaviour and this project does not take those on trust.
        /// </summary>
        private void AttachProbes()
        {
            // A: whatever Tundra already ships. Found by walking up to the collider rather than
            // assuming which node carries it - the dump says it is the prop clone root, but a probe
            // that silently attached to nothing would look exactly like "no events reached us".
            Collider root = internalProp.transform.GetComponentInChildren<Collider>();
            if (root == null)
            {
                Debug.LogWarning(Tag + "probe: no existing collider found in the prop at all");
            }
            else
            {
                InputProbe pa = root.gameObject.AddComponent<InputProbe>();
                pa.label = "CONSOLE:" + root.gameObject.name + " (" + root.GetType().Name + ")";
                Debug.Log(Tag + "probe A on '" + root.gameObject.name
                              + "' type=" + root.GetType().Name
                              + " isTrigger=" + root.isTrigger
                              + " enabled=" + root.enabled
                              + " layer=" + root.gameObject.layer
                              + " (" + LayerMask.LayerToName(root.gameObject.layer) + ")");
            }

            // B: one button, given a collider it does not currently have.
            Transform b = internalProp.FindModelTransform(probeButton);
            if (b == null)
            {
                Debug.LogWarning(Tag + "probe: button '" + probeButton + "' not found");
                return;
            }

            Collider had = b.GetComponent<Collider>();
            if (had == null)
            {
                BoxCollider bc = b.gameObject.AddComponent<BoxCollider>();
                Debug.Log(Tag + "probe B added BoxCollider to '" + probeButton
                              + "' centre=" + bc.center.ToString("F4")
                              + " size=" + bc.size.ToString("F4")
                              + " layer=" + b.gameObject.layer
                              + " (" + LayerMask.LayerToName(b.gameObject.layer) + ")");
            }
            else
            {
                Debug.Log(Tag + "probe B: '" + probeButton + "' ALREADY had a "
                              + had.GetType().Name + " - the dump was wrong, do not add one");
            }

            InputProbe pb = b.gameObject.AddComponent<InputProbe>();
            pb.label = "BUTTON:" + probeButton;
        }

        /// <summary>
        /// Every transform in the prop, one per line, SORTED LEFT TO RIGHT BY LOCAL X.
        ///
        /// This is the "look, do not guess" tool. The names alone
        /// (TE_CD2_PROP_BUTTON_1..8, CD2_PROP_BUT1..10 with _2.._5 suffixes) do not say which
        /// physical button is which; the position does, because sorting by X puts them in the same
        /// order as the labels printed on the console. Printing the name without the position would
        /// produce a list that looks like an answer and is not one.
        ///
        /// Runs on a bad transform name, or on demand via the dumpTransforms cfg flag.
        /// </summary>
        private string DumpTransforms()
        {
            Transform[] all = internalProp.transform.GetComponentsInChildren<Transform>(true);
            int n = all.Length;
            Vector3[] pos = new Vector3[n];
            Vector3[] size = new Vector3[n];
            bool[] measured = new bool[n];

            for (int i = 0; i < n; i++)
            {
                // ---- WHERE A THING IS, NOT WHERE ITS ORIGIN IS ----
                // The first version of this dump used transform.localPosition and was WORTHLESS, in a
                // way that looked like data. Two faults at once:
                //   1. localPosition is relative to each node's OWN parent, so values from different
                //      branches were being sorted against each other as if they shared a space.
                //   2. This model does not carry placement in its transforms at all. The proof is in
                //      its own output: TT_CD2_IVA_SCREEN1/2/3 - three screens sitting visibly side by
                //      side - ALL reported x=0 y=0.0285 z=0. The geometry is baked into the meshes.
                // The renderer's world bounds ARE where the thing is, whatever the hierarchy does, and
                // converting the centre into prop space gives one comparable frame for everything.
                Renderer r = all[i].GetComponent<Renderer>();
                if (r != null)
                {
                    pos[i] = internalProp.transform.InverseTransformPoint(r.bounds.center);
                    size[i] = r.bounds.size;
                    measured[i] = true;
                }
                else
                {
                    pos[i] = internalProp.transform.InverseTransformPoint(all[i].position);
                }
            }

            // Insertion sort by prop-space X, carrying the parallel arrays. Short list, runs once.
            for (int i = 1; i < n; i++)
            {
                Transform t = all[i]; Vector3 p = pos[i], s = size[i]; bool m = measured[i];
                int j = i - 1;
                while (j >= 0 && pos[j].x > p.x)
                {
                    all[j + 1] = all[j]; pos[j + 1] = pos[j]; size[j + 1] = size[j];
                    measured[j + 1] = measured[j]; j--;
                }
                all[j + 1] = t; pos[j + 1] = p; size[j + 1] = s; measured[j + 1] = m;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("    ").Append(n).Append(" transforms, left to right by PROP-SPACE X.\n");
            sb.Append("    pos = renderer bounds CENTRE in prop space; (origin) = no renderer, ")
              .Append("transform origin instead and NOT comparable.\n");
            for (int i = 0; i < n; i++)
            {
                sb.Append("    x=").Append(pos[i].x.ToString("F4"))
                  .Append(" y=").Append(pos[i].y.ToString("F4"))
                  .Append(" z=").Append(pos[i].z.ToString("F4"));
                if (measured[i])
                    sb.Append("  size=").Append(size[i].x.ToString("F4"))
                      .Append("x").Append(size[i].y.ToString("F4"))
                      .Append("x").Append(size[i].z.ToString("F4"));
                else
                    sb.Append("  (origin)                       ");
                sb.Append("  ").Append(all[i].name);
                // Collider TYPE and layer, not just "has one". Whether the console collider is a Box
                // or a Mesh decides whether it can steal clicks from things inside it, and the layer
                // decides which cameras raycast it at all.
                Collider col = all[i].GetComponent<Collider>();
                if (col != null)
                    sb.Append("   [").Append(col.GetType().Name)
                      .Append(col.isTrigger ? " trigger" : "")
                      .Append(col.enabled ? "" : " DISABLED")
                      .Append(" layer=").Append(all[i].gameObject.layer)
                      .Append(']');
                Transform par = all[i].parent;
                if (par != null) sb.Append("   parent=").Append(par.name);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public void OnDestroy()
        {
            // The IVA is torn down and rebuilt on every camera-mode change, so this runs often. A
            // RenderTexture that is not Released leaks VRAM every single time.
            if (screenCamera != null) screenCamera.targetTexture = null;
            if (cameraObject != null) Destroy(cameraObject);
            if (screen != null)
            {
                screen.Release();
                Destroy(screen);
                screen = null;
            }
        }
    }
}
