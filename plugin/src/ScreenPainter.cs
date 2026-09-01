/*
 * DragonScreen - ScreenPainter
 *
 * EXECUTES A DISPLAY LIST WITH GL, INTO A RENDERTEXTURE. It decides nothing about what a page looks
 * like - that is src/pure, headless and previewable. This file's whole job is to turn DrawCmds into
 * GL calls, and it should stay small enough to be checked by eye.
 *
 * Its twin is preview/PreviewMain.cs, which walks the SAME list with System.Drawing and writes a
 * PNG. If those two ever disagree the preview is worthless, so anything that decides geometry -
 * notably the arc's y-flip - lives in ArcGeometry where both call it, never here.
 *
 * ---- THIS IS THE HALF OF THE RENDERER THAT IS NOT IMGUI, AND THAT IS NOT A CONTRADICTION ----
 * The renderer decision (IMGUI + GL, settled from MechJeb) governs WINDOWS AND INPUT. It cannot
 * govern page content, because GUI.* draws to the screen and cannot be pointed at a RenderTexture -
 * see docs/IVA_TARGET.md, "THE ONE TECHNICAL CATCH".
 *
 * ---- WHERE THIS IS ALLOWED TO RUN ----
 * OnPostRender only fires on a component attached to the SAME GameObject as a Camera, and only when
 * that camera renders. Ours has targetTexture set to the screen's RenderTexture, so when this runs
 * the render target is already bound and GL is legal. That is the entire reason
 * DragonScreenMonitor creates a camera that shows nothing.
 *
 * ---- COORDINATES: TOP-LEFT ORIGIN, PIXELS ----
 * GL.LoadPixelMatrix(0, w, h, 0) puts (0,0) at the TOP LEFT with y increasing downward, matching
 * the reference art and IMGUI. The cost is mirrored triangle winding, which is why the material
 * forces _Cull Off - without it a flipped Y silently culls every filled shape and leaves a black
 * screen with no error.
 *
 * ---- NO ALLOCATION IN THE DRAW PATH ----
 * The display list, the vertex scratch and the material are built once. OnPostRender runs every
 * frame, per screen, and there are three screens.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    public class ScreenPainter : MonoBehaviour
    {
        private int w, h, index;
        private bool configured;

        private Material mat;
        private DisplayList page;
        private readonly DisplayList overlay = new DisplayList(8);   // abort alert, drawn over the page
        private bool overlayActive;
        private bool overflowLogged;

        // ---- NEW FIGMA UI NAVIGATION ----
        // The screens are being rebuilt from the Figma design. This switch drives the new page map
        // (FigmaUI: Cover hub -> HUD / Audio / procedure / cabin / placeholders) instead of the old
        // FLIGHT/VEHICLE/NAV/DOCKING/SETTINGS tabs + ChromeBar. The old path is kept intact under the
        // `else` branches so the build stays valid and the change is reversible while the rebuild runs.
        // `selectedPage` doubles as the current UiPage int (it already persists per screen); the only
        // difference is the page COUNT, so persistence and bounds use PageCount below.
        private const bool FigmaMode = true;
        private readonly System.Collections.Generic.List<int> uiHistory = new System.Collections.Generic.List<int>();
        private int uiHistIndex = -1;

        // Suit Leak Check live state (only meaningful while that page is shown). suitStart is the
        // realtime second START was pressed (<0 = idle); the countdown falls 5->0 over ~0.9s a step and
        // at 5s the completion popup shows (one-shot, matching the reference Fourth.vue). Reset on any
        // page change so the page is fresh each time it is opened.
        private float suitStart = -1f;
        private bool suitPopup;

        // The Cover's selected deorbit phase (0..4). The rail + ◄/► arrows change it IN-PAGE — it drives
        // the rail highlight + centre heading, and unlike the suit state it persists across visits.
        private int coverPhase = 1;   // default: Coast to Trunk Jettison (as the reference defaults)

        /// <summary>How many pages the current model has — the new Figma set or the old tab set.</summary>
        private static int PageCount { get { return FigmaMode ? FigmaUI.PageCount : ChromeBar.PageNames.Length; } }

        // Which screen keeps the abort alert after SUPPRESS RESPONSE — the CENTRE display (DragonScreen.cfg:
        // screenIndex 1 = LEFT, 2 = CENTRE "shared by both seats", 3 = RIGHT).
        private const int CentreScreenIndex = 2;

        // ---- TEXT ----
        // PORTED FROM MAS/RPM, NOT INVENTED. MdVTextMesh.cs:667-679 and 938-962: request the glyphs
        // into the font's dynamic atlas, then read CharacterInfo for the UV corners and the advance,
        // and build one quad per glyph. MAS writes those into a Mesh; we feed them straight to GL,
        // which wants exactly the same numbers.
        //
        // That is why there is no TextMesh GameObject here, no private culling layer, no AssetBundle
        // and no atlas generator: Unity builds the atlas, and the glyph metrics come with it.
        private Font font;
        private string fontName = "";
        private Material textMat;
        private bool fontLogged;

        // ---- IMAGES ----
        // A third pass. Same shape as the text pass: its own material, its own texture, switched when
        // the walk reaches a command that needs it.
        private Material imageMat;

        // Arc scratch. 256 points is a full circle at well under a degree per segment; ArcGeometry
        // clamps anything sillier. Sized once because this is the draw path.
        private const int MaxArcPoints = 256;
        private readonly float[] arcInner = new float[MaxArcPoints * 2];
        private readonly float[] arcOuter = new float[MaxArcPoints * 2];

        // ---- CAPTURE: the render target, written to a PNG, exactly as drawn ----
        // Cheaper and STRICTLY better evidence than a screenshot of the cabin: pixel-exact, no
        // perspective, no cabin lighting, no cropping, and no work for the user beyond loading in.
        // A screenshot is still the only way to judge how it LOOKS on the glass at a distance; this
        // is for judging what was actually drawn.
        private bool autoCapture;
        private KeyCode captureKey = KeyCode.None;
        private bool captureRequested;
        private bool captureDone;
        private int frames;

        /// <summary>Caption, built ONCE here so the page never concatenates in the draw path.</summary>
        private string label = "";

        // ---- TOUCH ----
        // There WAS a marker here: a cross drawn where we believed a click landed, so the
        // normalised-to-page mapping could prove itself in one look rather than be argued about.
        // It worked - the axis handedness was right first time - and its own comment said to delete
        // it once page content responded to touch on its own. That day came, and left in it started
        // being read as a rendering defect on screenshots. The coordinate is still logged, which is
        // the part that was ever diagnostic.

        /// <summary>
        /// Selected page. The AUTHORITY is DragonScreenState on the pod - this is a cached copy so
        /// the draw path does not walk the module list every frame. Every change goes through
        /// Touch(), which writes the module first and mirrors it here second, so the cache can never
        /// be the thing that is right.
        /// </summary>
        private int selectedPage;
        private DragonScreenState persist;
        /// <summary>Last seen persist.Version - see Update() for why a poll and not an event.</summary>
        private int lastStateVersion = -1;

        /// <summary>
        /// Where this screen's NAV map is looking. PER SCREEN and deliberately not persisted - see
        /// MapView. Three displays can be looking at three different parts of the world, which is
        /// the entire reason the pages are per-screen in the first place.
        /// </summary>
        private MapView mapView = MapProjection.Default();

        /// <summary>
        /// Which subview tab this screen is on, PER PAGE. Not persisted, for the same reason the map
        /// scroll position is not: a tab is where you happen to be looking, not a decision to
        /// restore. Indexed by page so switching away and back does not lose it.
        /// </summary>
        private readonly int[] subview = new int[8];

        /// <summary>
        /// What each screen is ACTUALLY showing, by screen id. The SETTINGS grid reads it.
        ///
        /// ---- WHY NOT JUST ASK THE PERSISTED STATE ----
        /// Because the persisted state does not know. A screen nobody has touched has no entry - it
        /// is showing its cfg default, which lives in the InternalModule and nowhere else. Asking
        /// DragonScreenState would answer "unset" and the grid would highlight nothing for a display
        /// that is plainly showing something.
        ///
        /// Static, and each painter writes only its own slot, so the three of them assemble the
        /// answer between them without needing references to each other.
        /// </summary>
        private static readonly int[] livePage = new int[4];

        /// <summary>
        /// Screen brightness in tenths, SHARED BY ALL THREE DISPLAYS.
        ///
        /// Static because it is a cabin setting, not a property of one panel - dimming for a night
        /// pass and having only the display you touched go dark would be a bug, not a feature. The
        /// persisted copy lives on DragonScreenState; this is the cached value the draw path reads.
        /// </summary>
        private static int brightness = SettingsPage.MaxBright;

        /// <summary>
        /// Chrome values. PLACEHOLDERS until the data layer exists, and cached rather than formatted
        /// per frame even though they are constant - because the moment they become live, formatting
        /// in the draw path is the bug that would appear, and the shape of the code should not have
        /// to change to avoid it.
        /// </summary>
        private ChromeState chrome;

        public void Configure(int width, int height, int screenIndex,
                              bool captureOnLoad, KeyCode key, string font,
                              DragonScreenState state, int defaultPageIndex)
        {
            persist = state;
            w = width; h = height; index = screenIndex;
            autoCapture = captureOnLoad;
            captureKey = key;
            fontName = font;
            label = "SCREEN " + screenIndex + "   " + width + "x" + height;
            // Headroom over the worst page plus the chrome.
            page = new DisplayList(Pages.Commands + ChromeBar.Commands + 4);

            chrome = new ChromeState();
            chrome.Met = "T+ 00:00:00";
            chrome.VehicleState = "NOMINAL";
            chrome.LinkName = "COM1/TLM";
            chrome.LinkTimer = "00:00:00";
            chrome.LinkUp = true;
            // The saved selection wins; the cfg default is only what a never-touched vessel shows.
            selectedPage = (persist != null)
                ? persist.GetPage(screenIndex, PageCount, FigmaMode ? 0 : defaultPageIndex)
                : (FigmaMode ? 0 : defaultPageIndex);
            chrome.SelectedPage = selectedPage;
            // Seed the back/forward history with the page the screen opens on.
            uiHistory.Clear(); uiHistory.Add(selectedPage); uiHistIndex = 0;
            chrome.AlertMask = 0;
            Publish();

            if (persist != null)
            {
                brightness = persist.GetBrightness();
                lastStateVersion = persist.Version;
            }

            configured = (w > 0 && h > 0);
        }

        /// <summary>
        /// Adopt a page selection made SOMEWHERE ELSE - the SETTINGS grid on another display, or a
        /// reload. Polled against a version counter rather than pushed by an event, because the three
        /// painters have no reference to each other and giving them one would be a web of wiring for
        /// a comparison that costs an int.
        /// </summary>
        private void SyncFromState()
        {
            if (persist == null || persist.Version == lastStateVersion) return;
            lastStateVersion = persist.Version;

            int page = persist.GetPage(index, PageCount, selectedPage);
            if (page != selectedPage)
            {
                selectedPage = page;
                chrome.SelectedPage = page;
                // A page adopted from elsewhere restarts local history at that page.
                uiHistory.Clear(); uiHistory.Add(page); uiHistIndex = 0;
                Publish();
            }
            brightness = persist.GetBrightness();
        }

        /// <summary>Tell the other screens what this one is showing. See livePage.</summary>
        private void Publish()
        {
            if (index >= 0 && index < livePage.Length) livePage[index] = selectedPage;
        }

        /// <summary>
        /// Resolve the font once.
        ///
        /// CreateDynamicFontFromOSFont only sees fonts INSTALLED IN WINDOWS. D-DIN - the family the
        /// real capsule uses, sitting in assets/d-din under the OFL - is downloaded but not installed
        /// here, so it is a cfg field and the fallback is logged rather than silently taken. Installing
        /// it is a two-click user action needing no code change.
        ///
        /// FOR RELEASE this will not do: a mod cannot ask users to install a font by hand. The route
        /// then is MAS's other path - a bitmap font built from a texture plus CharacterInfo
        /// (MASLoader.cs:376-474) - which needs no OS install and no AssetBundle. Not built yet
        /// because it is not needed yet.
        /// </summary>
        private bool EnsureFont()
        {
            if (font != null) return true;

            string want = string.IsNullOrEmpty(fontName) ? "Arial" : fontName.Trim();
            font = Font.CreateDynamicFontFromOSFont(want, 32);
            if (font == null)
            {
                if (!fontLogged)
                {
                    fontLogged = true;
                    Debug.LogError("[DragonScreen] no font at all for '" + want + "' - no text");
                }
                return false;
            }

            if (!fontLogged)
            {
                fontLogged = true;
                // font.name reports what Unity ACTUALLY resolved. Asking for a font that is not
                // installed returns a substitute rather than null, so comparing is the only way to
                // know whether the page is being drawn in the font it was designed for.
                Debug.Log("[DragonScreen] screen " + index + " font requested '" + want
                          + "', resolved '" + font.name + "', dynamic=" + font.dynamic
                          + ", ascent=" + font.ascent + ", baseSize=" + font.fontSize);
            }

            Shader s = Shader.Find("GUI/Text Shader");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Unlit/Transparent");
            if (s == null) { Debug.LogError("[DragonScreen] no text shader"); return false; }

            textMat = new Material(s);
            textMat.hideFlags = HideFlags.HideAndDontSave;

            // Images need a plain textured shader that respects alpha and vertex colour. "GUI/Text
            // Shader" samples only the ALPHA channel - correct for a font atlas, useless for
            // artwork - so this is a separate material with a different shader, not a second use of
            // the same one.
            Shader si = Shader.Find("Unlit/Transparent");
            if (si == null) si = Shader.Find("Sprites/Default");
            if (si == null) si = Shader.Find("Unlit/Texture");
            if (si != null)
            {
                imageMat = new Material(si);
                imageMat.hideFlags = HideFlags.HideAndDontSave;
                Debug.Log("[DragonScreen] screen " + index + " image shader '" + si.name + "'");
            }
            else Debug.LogWarning("[DragonScreen] no image shader - bitmaps will not draw");

            return true;
        }

        /// <summary>
        /// A touch, in PAGE pixels. Called from ScreenTouch on the screen's own collider.
        /// </summary>
        public void Touch(float px, float py)
        {
            // ---- NEW FIGMA UI: the design's own navigation, no chrome bar ----
            // The Cover hub's buttons + each page's back chevron ARE the navigation, so touch routes
            // straight through FigmaUI (pure) to a NavHit, which ApplyNav carries out.
            if (FigmaMode)
            {
                // Nav (bottom bar / back chevron) wins; anything it does not claim on the Suit Leak
                // Check page drives its START / HALT / popup-close.
                NavHit nh = FigmaUI.HitTest((UiPage)selectedPage, px, py, w, h);
                if (nh.Act != NavAct.None) { ApplyNav(nh); return; }
                UiPage cur = (UiPage)selectedPage;
                if (cur == UiPage.SuitCheck)
                {
                    switch (SuitCheckPage.HitTest(px, py, w, h, suitPopup))
                    {
                        case SuitCheckPage.SuitAct.Start: suitStart = Time.realtimeSinceStartup; suitPopup = false; break;
                        case SuitCheckPage.SuitAct.Halt:  suitStart = -1f; suitPopup = false; break;
                        case SuitCheckPage.SuitAct.Close: suitPopup = false; break;
                    }
                }
                else if (cur == UiPage.Cover)
                {
                    // The rail selects a phase; the ◄/► arrows step through them (wrapping over all 7).
                    CoverPage.CoverButton cb = CoverPage.HitTest(px, py, w, h);
                    int ph = CoverPage.PhaseOf(cb);
                    if (ph >= 0) coverPhase = ph;
                    else if (cb == CoverPage.CoverButton.Back)
                        coverPhase = (coverPhase + CoverPage.PhaseCount - 1) % CoverPage.PhaseCount;
                    else if (cb == CoverPage.CoverButton.Forward)
                        coverPhase = (coverPhase + 1) % CoverPage.PhaseCount;
                }
                return;
            }

            // ---- CHROME FIRST, ALWAYS ----
            // The nav bar is drawn over every page, so it must be TESTED over every page too. If a
            // page were asked first, a control that happened to overlap the bar would silently eat
            // the one touch the crew can always rely on.
            int page = ChromeBar.HitTest(px, py, w, h);
            if (page >= 0)
            {
                SelectPage(index, page);
                return;
            }

            // ---- THE CREW CHECKLIST CARD IS MODAL OVER FLIGHT ----
            // When the conductor needs the crew, the card is drawn over the FLIGHT page; a touch on it
            // (an item, GO, NO-GO, ABORT) drives CrewProcedureOps directly and wins over the page beneath.
            // A touch that misses the card falls through to the normal page controls (so AUTO SEQUENCE,
            // still reachable, can cancel), and the chrome bar was already tested above.
            if (selectedPage == 0 && CrewProcedureOps.CrewActionNeeded())
            {
                Gate g = CrewProcedureOps.CurrentGate();
                int n = (g.Items == null) ? 0 : g.Items.Length;
                GateHit gh = GateCard.HitTest(px, py, w, h, n);
                switch (gh.Kind)
                {
                    case GateHitKind.Item:  CrewProcedureOps.ToggleItem(gh.Item); return;
                    case GateHitKind.Go:    CrewProcedureOps.PressGo();  return;
                    case GateHitKind.NoGo:  CrewProcedureOps.PressNoGo(); return;
                    case GateHitKind.Abort: CrewProcedureOps.PressAbort(); return;
                }
            }

            Apply(Pages.HitTest(selectedPage, px, py, w, h, subview[selectedPage & 7],
                                HullCams.Count));
        }

        /// <summary>
        /// Put a page on a screen, persistently. Screen may be THIS one or another - the SETTINGS
        /// grid can move a page onto a display the crew is not touching.
        ///
        /// Module first, cache second. If the module is missing the screens still work, they just
        /// forget - the honest degradation, and it is logged once at startup.
        /// </summary>
        private void SelectPage(int screen, int page)
        {
            if (page < 0 || page >= PageCount) return;
            if (persist != null) persist.SetPage(screen, page, PageCount);
            if (screen == index)
            {
                selectedPage = page;
                chrome.SelectedPage = page;
                suitStart = -1f; suitPopup = false;   // the Suit Leak Check opens fresh each visit
                Publish();
            }
            else if (screen >= 0 && screen < livePage.Length)
            {
                // The target screen's own painter will pick this up through its version poll; this
                // just stops the grid showing the old highlight for the frame in between.
                livePage[screen] = page;
            }
            if (persist != null) lastStateVersion = persist.Version;
            string pn = FigmaMode ? FigmaUI.Name((UiPage)page)
                      : (page >= 0 && page < ChromeBar.PageNames.Length ? ChromeBar.PageNames[page] : "?");
            Debug.Log("[DragonScreen] screen " + screen + " page -> " + pn);
        }

        /// <summary>
        /// Carry out a Figma-UI navigation touch: go to a page (pushing history), or step the
        /// back/forward history the Cover's arrows drive. Going to a new page truncates any forward
        /// history, the standard browser rule.
        /// </summary>
        private void ApplyNav(NavHit nh)
        {
            switch (nh.Act)
            {
                case NavAct.Goto:
                    int p = (int)nh.Target;
                    if (p == selectedPage) return;
                    if (uiHistIndex >= 0 && uiHistIndex < uiHistory.Count - 1)
                        uiHistory.RemoveRange(uiHistIndex + 1, uiHistory.Count - uiHistIndex - 1);
                    uiHistory.Add(p);
                    uiHistIndex = uiHistory.Count - 1;
                    SelectPage(index, p);
                    break;
                case NavAct.Back:
                    if (uiHistIndex > 0) { uiHistIndex--; SelectPage(index, uiHistory[uiHistIndex]); }
                    break;
                case NavAct.Forward:
                    if (uiHistIndex >= 0 && uiHistIndex < uiHistory.Count - 1)
                    { uiHistIndex++; SelectPage(index, uiHistory[uiHistIndex]); }
                    break;
            }
        }

        /// <summary>
        /// Carry out what a touch resolved to. THE ONLY PLACE A PAGE ACTION MEETS KSP - the pages
        /// themselves are pure and return a value, which is what lets a headless test press every
        /// button on every page and assert what came back.
        /// </summary>
        private void Apply(PageHit hit)
        {
            switch (hit.Act)
            {
                case PageAct.None: return;

                case PageAct.NavPanLeft:  mapView = MapProjection.Pan(mapView, -1.0, 0.0); break;
                case PageAct.NavPanRight: mapView = MapProjection.Pan(mapView, 1.0, 0.0); break;
                case PageAct.NavPanUp:    mapView = MapProjection.Pan(mapView, 0.0, 1.0); break;
                case PageAct.NavPanDown:  mapView = MapProjection.Pan(mapView, 0.0, -1.0); break;
                case PageAct.NavZoomIn:   mapView = MapProjection.Zoom(mapView, 1); break;
                case PageAct.NavZoomOut:  mapView = MapProjection.Zoom(mapView, -1); break;
                case PageAct.NavNextView: mapView = MapProjection.NextMode(mapView); break;
                case PageAct.NavCentre:
                {
                    PageState st = VesselData.State;
                    mapView = MapProjection.Centre(mapView, st.Latitude, st.Longitude);
                    break;
                }

                case PageAct.ToggleLights: VesselData.ToggleLights(); break;

                case PageAct.BrightUp:   SetBrightness(brightness + 1); break;
                case PageAct.BrightDown: SetBrightness(brightness - 1); break;

                case PageAct.SetScreenPage:
                {
                    int screen, page;
                    PageHit.UnpackScreenPage(hit.Arg, out screen, out page);
                    SelectPage(screen, page);
                    break;
                }

                case PageAct.ViewFromSeat: VesselData.ViewFromSeat(hit.Arg); break;

                case PageAct.SetCamera:
                    VesselData.SetCameraView(hit.Arg);
                    break;

                case PageAct.SetSubview:
                    subview[selectedPage & 7] = hit.Arg;
                    break;

                case PageAct.Capture: captureRequested = true; break;

                case PageAct.ToggleBoosterRecovery:
                    MissionConductor.AutoRecoverBooster = !MissionConductor.AutoRecoverBooster;
                    Debug.Log("[DragonScreen] AUTO BOOSTER RECOVERY "
                              + (MissionConductor.AutoRecoverBooster ? "ARMED — after MECO the booster is focused + landed (Dragon orbit sacrificed this flight)"
                                                                     : "OFF — full Dragon mission to orbit"));
                    break;

                // Tick a crew step. Held in VesselData rather than per screen: the countdown is the
                // VEHICLE's, so a check made on the left display is made for everyone.
                case PageAct.AckStep: VesselData.AcknowledgeStep(hit.Arg); break;

                // AUTO SEQUENCE is the crew-in-the-loop mission conductor (CrewProcedureOps): it runs the
                // real gate sequence - countdown checklists, GO/NO-GO, the L-approach holds, GO for undock,
                // GO for deorbit - flying each phase only after the crew's GO. The manual ascent-only
                // toggle is still on the physical STRING 1A button.
                case PageAct.ToggleAuto: CrewProcedureOps.Toggle(); break;

                // ---- UNDOCK (the one manual mission control). The painter only DISPATCHES: MissionOps is a
                // static on a flight-software class, so the action outlives this IVA widget being destroyed.
                // Rendezvous + docking are flown by AUTO SEQUENCE (CrewProcedureOps), not by a button.
                case PageAct.Undock: MissionOps.Undock(); break;
            }
        }

        private void SetBrightness(int tenths)
        {
            if (tenths < SettingsPage.MinBright) tenths = SettingsPage.MinBright;
            if (tenths > SettingsPage.MaxBright) tenths = SettingsPage.MaxBright;
            brightness = tenths;
            if (persist != null) persist.SetBrightness(tenths);
        }

        public void Update()
        {
            if (captureKey != KeyCode.None && Input.GetKeyDown(captureKey)) captureRequested = true;
            // Switch the docking camera off when no screen has asked for it lately - a full
            // scene camera is not free and most of a mission is not spent on DOCKING.
            DockingCamRenderer.Idle();

            // ---- ⛔ THE AUTOPILOT USED TO BE TICKED FROM HERE. IT MUST NOT BE. ----
            // FlightCommands, AutoPilot and FlightRecorder now live in FlightDriver, a flight-scene
            // KSPAddon. This object belongs to the IVA, and the IVA is destroyed whenever the Dragon
            // stops being the active vessel - which is precisely what a booster handover does. Every
            // tick driven from here silently stopped at the moment the recovery began. See
            // FlightDriver.cs for the full account.
            //
            // Nothing frame-critical to the DISPLAY belongs here either: this Update runs once per
            // SCREEN, so anything vehicle-wide would run three times a frame.
        }

        /// <summary>
        /// Read the render target back and write it out.
        ///
        /// MUST be called from inside OnPostRender: ReadPixels reads whatever RenderTexture is
        /// ACTIVE, and ours is only bound while our camera is rendering.
        ///
        /// It allocates a full-size Texture2D and a PNG buffer, which every other rule in this file
        /// forbids in the draw path - permitted here ONLY because it is one-shot developer tooling
        /// that runs on a keypress or once per scene, never per frame.
        /// </summary>
        private void Capture()
        {
            try
            {
                Texture2D t = new Texture2D(w, h, TextureFormat.ARGB32, false);
                t.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
                t.Apply();
                byte[] png = t.EncodeToPNG();
                Destroy(t);

                string dir = System.IO.Path.Combine(KSPUtil.ApplicationRootPath,
                                                    "DragonScreen_capture");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, "screen" + index + ".png");
                System.IO.File.WriteAllBytes(path, png);
                Debug.Log("[DragonScreen] captured screen " + index + " -> " + path
                          + "  (" + w + "x" + h + ", " + (png.Length / 1024) + " KB)");
            }
            catch (Exception e)
            {
                // Never take the screen out over a debug capture.
                Debug.LogWarning("[DragonScreen] capture of screen " + index + " failed: " + e.Message);
            }
        }

        /// <summary>
        /// The GL material, built once.
        ///
        /// "Hidden/Internal-Colored" is Unity's own immediate-mode shader and is the right tool, but
        /// it is a built-in a game CAN strip, so there are fallbacks and the choice is logged.
        /// MechJeb proves "Legacy Shaders/Particles/Additive" exists in KSP (MechJeb2/GLUtils.cs:12);
        /// it is last because additive blending makes overlapping shapes brighten, which is wrong for
        /// a screen but still visible - a usable degraded mode rather than a black panel.
        /// (In practice Internal-Colored resolved on all three screens, confirmed in KSP.log.)
        /// </summary>
        private bool EnsureMaterial()
        {
            if (mat != null) return true;

            Shader s = Shader.Find("Hidden/Internal-Colored");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Legacy Shaders/Particles/Additive");
            if (s == null)
            {
                Debug.LogError("[DragonScreen] no usable GL shader found - screen " + index
                               + " cannot draw");
                return false;
            }

            mat = new Material(s);
            mat.hideFlags = HideFlags.HideAndDontSave;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);   // see header: flipped Y
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

            Debug.Log("[DragonScreen] screen " + index + " GL shader '" + s.name + "'");
            return true;
        }

        public void OnDestroy()
        {
            if (mat != null) { Destroy(mat); mat = null; }
            if (textMat != null) { Destroy(textMat); textMat = null; }
            if (imageMat != null) { Destroy(imageMat); imageMat = null; }
        }

        public void OnPostRender()
        {
            if (!configured || !EnsureMaterial()) return;
            EnsureFont();   // fails soft: no font just means no text, never no page

            // Rebuild the page, then draw it. Two phases on purpose: the build is pure and could run
            // anywhere, the draw is the only part that needs to be here.
            page.Clear();

            // One read per frame for the whole vessel, not one per screen - see VesselData.
            VesselData.Refresh();
            SyncFromState();
            chrome.Met = VesselData.Met;

            // ---- THE THREE PER-SCREEN FIELDS ----
            // Everything else in PageState is the vessel and is shared. These are display state, so
            // they are stamped onto a COPY here rather than pushed into VesselData, which exists to
            // read KSP and would otherwise have to know about panels.
            PageState ps = VesselData.State;
            ps.Brightness = brightness;
            ps.BoosterRecoveryOn = MissionConductor.AutoRecoverBooster;
            ps.ScreenPages = livePage;

            // The map follows the vehicle until the crew pans it by hand - see MapProjection.Pan.
            mapView = MapProjection.Track(mapView, ps.HasFix, ps.Latitude, ps.Longitude);

            // ---- ALARM ROUTING ----
            // Since the dials became identity-coloured this bitmask is the alarm channel: it is what
            // turns a page link red on every screen, so a crew member reading NAV still learns that
            // VEHICLE has a problem. See Alarms.Mask.
            chrome.AlertMask = Alarms.Mask(ps);
            // STATE folds in the authoritative FDIR spine (Alarms.SystemSeverity), not just crew-environment alarms.
            chrome.VehicleState = ps.Valid ? Alarms.Word(Alarms.SystemSeverity(ps)) : "NO DATA";

            int sub = subview[selectedPage & 7];

            if (FigmaMode)
            {
                UiPage up = (UiPage)selectedPage;
                // The HUD overlays the forward docking camera when the nose cone is open; claim it here,
                // exactly as the old DOCKING page did, before the disc is drawn.
                if (FigmaUI.WantsDockingCam(up, ps))
                    DockingCamRenderer.Request(DockingCamRenderer.DockingPortView, 1);
                ps.CameraHeldByDocking = DockingCamRenderer.HeldByDocking;
                ps.CameraResText = DockingCamRenderer.Resolution;

                // Suit Leak Check countdown: advance from the START moment; at 5s raise the popup once.
                int suitCount = 5;
                if (up == UiPage.SuitCheck && suitStart >= 0f)
                {
                    float el = Time.realtimeSinceStartup - suitStart;
                    if (el >= 5f) { suitPopup = true; suitStart = -1f; }
                    else { suitCount = 5 - (int)(el / 0.9f); if (suitCount < 0) suitCount = 0; }
                }

                // The new Figma pages carry their own chrome (each has its bottom bar), so no ChromeBar.
                FigmaUI.Build(page, up, w, h, ps, mapView, suitCount, suitPopup, coverPhase);
            }
            else
            {
                // ---- CLAIM THE CAMERA BEFORE DRAWING ----
                // One camera, two consumers: DOCKING must have the forward view, the VIDEO tab wants
                // whatever the crew picked. The painter is the only place that knows which page is about
                // to draw, so the claim is made here rather than inside ImageStore.
                if (selectedPage == 3) DockingCamRenderer.Request(DockingCamRenderer.DockingPortView, 1);
                else if (selectedPage == 4 && sub == SettingsPage.Video)
                {
                    VesselData.ValidateCameraView();
                    DockingCamRenderer.Request(VesselData.CameraView, 0);
                }
                ps.CameraHeldByDocking = DockingCamRenderer.HeldByDocking;
                ps.CameraResText = DockingCamRenderer.Resolution;

                Pages.Build(page, selectedPage, w, h, ps, mapView, index, sub);

                // Chrome last, so it draws over the page. STRINGS ARE CACHED IN Configure - nothing here
                // formats, because this runs every frame on three screens. Real vessel values replace
                // these when the data layer lands; the layout does not change when they do.
                ChromeBar.Build(page, w, h, chrome);
            }

            // ---- THE TOUCH MARKER IS GONE ----
            // It was an INSTRUMENT: a cross drawn where we believed a click landed, so the
            // normalised-to-page mapping could prove itself in one look. It did its job - the axes
            // were right first time and every control since has been hit-tested against its own
            // rect. Its own comment said "delete the marker once page content responds to touch on
            // its own", and it now does; left in, it reads as a rendering defect on a screenshot.

            // Glyphs must be in the atlas BEFORE the draw pass reads their UVs, and requesting can
            // REBUILD the atlas - which invalidates every UV previously read. So every string is
            // requested first, in one go, and only then is anything drawn. Interleaving request and
            // draw is the classic dynamic-font corruption bug.
            // ---- ABORT OVERLAY: built here so its glyphs are atlased alongside the page's, drawn after it ----
            overlay.Clear();
            // The abort alert shows on every screen; once SUPPRESS RESPONSE is pressed it stays on the CENTRE
            // screen (screenIndex 2, "shared by both seats") only — the side screens return to their page.
            overlayActive = FlightDriver.Aborting
                            && (!FlightDriver.AbortFxSuppressed || index == CentreScreenIndex);
            if (overlayActive)
            {
                bool flashOn = ((int)(Time.time * 3f) & 1) == 0;   // ~1.5 Hz square flash — ABORTING + red frame
                Texture2D dp = ImageStore.Get(ImageId.DontPanic);
                float aspect = (dp != null && dp.height > 0) ? (float)dp.width / dp.height : 1.5f;
                AbortOverlay.Build(overlay, w, h, flashOn, dp != null, aspect);   // the art itself stays solid
            }

            RequestGlyphs(page);
            if (overlayActive) RequestGlyphs(overlay);

            // Once, not every frame - an overflowing page overflows on all of them.
            if (page.Overflowed && !overflowLogged)
            {
                overflowLogged = true;
                Debug.LogWarning("[DragonScreen] screen " + index + " display list overflowed at "
                                 + page.Capacity + " commands - part of the page is MISSING");
            }

            GL.PushMatrix();
            mat.SetPass(0);
            GL.LoadPixelMatrix(0f, w, h, 0f);
            Execute(page);
            if (overlayActive) Execute(overlay);
            GL.PopMatrix();

            // AFTER drawing, so the capture holds a finished frame rather than a half-built one.
            // The auto-capture waits a beat: at frame 0 the IVA is still settling and a capture taken
            // then would show a page that had not finished being anything yet.
            frames++;
            if (autoCapture && !captureDone && frames == 60) captureRequested = true;
            if (captureRequested)
            {
                captureRequested = false;
                captureDone = true;
                Capture();
            }
        }

        /// <summary>
        /// Put every glyph the page needs into the font atlas, before any UV is read.
        /// See the call site for why this cannot be interleaved with drawing.
        /// </summary>
        private void RequestGlyphs(DisplayList dl)
        {
            if (font == null) return;
            int n = dl.Count;
            for (int i = 0; i < n; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind == DrawKind.Text && c.Str != null)
                    font.RequestCharactersInTexture(c.Str, Mathf.RoundToInt(c.C));
            }
        }

        /// <summary>
        /// Walk the list IN ORDER, switching shader pass only when the next command needs a different
        /// one.
        ///
        /// ORDER IS NOT NEGOTIABLE. Sorting the list into "all shapes, then all text" would batch
        /// more efficiently and would silently break every page that puts a panel behind a label -
        /// which is most of them. This is a painter's-algorithm UI; last drawn wins.
        /// </summary>
        private void Execute(DisplayList dl)
        {
            int n = dl.Count;
            // -1 nothing set, 0 solid, 1 text, 2 image. An int rather than a bool now that there are
            // three; the two-way version would have needed a second flag and an implicit rule about
            // which wins.
            int pass = -1;
            // Which texture the image material currently holds. Reset per walk, because the material
            // is shared and another screen's painter may have bound something else since.
            Texture boundImage = null;

            for (int i = 0; i < n; i++)
            {
                DrawCmd c = dl.At(i);

                int want;
                Texture img = null;
                if (c.Kind == DrawKind.Text)
                {
                    if (font == null || textMat == null) continue;
                    want = 1;
                }
                else if (c.Kind == DrawKind.Image)
                {
                    // A NAMED asset (Figma-exported PNG placed by key) resolves off its string; otherwise
                    // Resolve, not Get: ImageId.BodyMap has no file behind it and comes out of the
                    // running game. See ImageStore.BodyMap / ImageStore.ResolveAsset.
                    img = c.AssetKey != null ? ImageStore.ResolveAsset(c.AssetKey)
                                             : ImageStore.Resolve(c.Image);
                    // A missing image is skipped, not substituted. ImageStore has already logged it
                    // once; drawing a placeholder rectangle here would put a shape on the glass that
                    // no page asked for.
                    if (img == null || imageMat == null) continue;
                    want = 2;
                }
                else want = 0;

                // An image re-sets the pass only when its TEXTURE actually differs from the one
                // already bound - not on every image command, which is what this used to do.
                //
                // That was fine when a page drew two or three pictures. NAV's globe draws up to 96
                // strips of the SAME body map in a row, and re-binding a 4096x2048 texture 96 times
                // a frame, on three screens, is real cost for no change of state. The correctness
                // requirement is only that the bound texture matches the command; comparing them
                // directly says that, where "is it an image?" merely implied it.
                if (want != pass || (want == 2 && !ReferenceEquals(img, boundImage)))
                {
                    pass = want;
                    if (pass == 1)
                    {
                        // Re-assign every switch: a dynamic font atlas can be rebuilt into a NEW
                        // texture object, and a material still pointing at the old one draws blanks.
                        textMat.mainTexture = font.material.mainTexture;
                        textMat.SetPass(0);
                    }
                    else if (pass == 2)
                    {
                        imageMat.mainTexture = img;
                        imageMat.SetPass(0);
                        boundImage = img;
                    }
                    else mat.SetPass(0);
                }

                if (c.Kind == DrawKind.Rect) DrawRect(c);
                else if (c.Kind == DrawKind.ArcBand) DrawArcBand(c);
                else if (c.Kind == DrawKind.Line) DrawLine(c);
                else if (c.Kind == DrawKind.Image)
                {
                    DrawImage(c);
                    if (c.CircleClip)
                    {
                        // Mask the square-minus-circle corners with the solid material, so an opaque
                        // feed (the docking camera) reads as a ball. Switching passes here leaves the
                        // solid material bound; tell the state machine so the next command rebinds.
                        mat.SetPass(0);
                        DrawImageCircleMask(c);
                        pass = 0;
                    }
                }
                else DrawText(c);
            }
        }

        /// <summary>
        /// One textured quad.
        ///
        /// ---- THE V FLIP ----
        /// Texture space has v = 0 at the BOTTOM; this render target has y increasing DOWNWARD. So
        /// the rect's top edge takes v = 1 and its bottom edge v = 0. Get it the other way round and
        /// the artwork renders upside down while its position stays perfectly correct, which reads as
        /// a broken asset rather than a wrong UV.
        /// </summary>
        private void DrawImage(DrawCmd c)
        {
            float x0 = c.A, y0 = c.B, x1 = c.A + c.C, y1 = c.B + c.D;
            // The rect's TOP edge takes the LARGER v. That is the whole flip, and it is stated once
            // here and once in MapProjection, which is why NAV's panned map does not need its own.
            GL.Begin(GL.QUADS);
            GL.Color(Tint(c.Colour));
            GL.TexCoord2(c.UMin, c.VMax); GL.Vertex3(x0, y0, 0f);
            GL.TexCoord2(c.UMax, c.VMax); GL.Vertex3(x1, y0, 0f);
            GL.TexCoord2(c.UMax, c.VMin); GL.Vertex3(x1, y1, 0f);
            GL.TexCoord2(c.UMin, c.VMin); GL.Vertex3(x0, y1, 0f);
            GL.End();
        }

        /// <summary>
        /// Paint the corners of a square image with its background colour, leaving the inscribed circle.
        ///
        /// GL immediate mode here has no scissor or stencil, so a circular clip is done by covering the
        /// square-minus-circle region rather than by clipping. The strip walks the circle (inner edge)
        /// and the square boundary (outer edge) in lockstep: the two coincide at the four cardinal
        /// points (zero width) and open to the full corner at the diagonals, so it masks exactly the
        /// cusps and nothing of the disc. Must be called with the SOLID material bound.
        /// </summary>
        private void DrawImageCircleMask(DrawCmd c)
        {
            float cx = c.A + c.C * 0.5f, cy = c.B + c.D * 0.5f;
            float r = (c.C < c.D ? c.C : c.D) * 0.5f;   // circle radius = half the shorter side
            const int N = 96;
            GL.Begin(GL.TRIANGLE_STRIP);
            GL.Color(Tint(c.ClipBg));
            for (int i = 0; i <= N; i++)
            {
                double a = (double)i / N * 2.0 * Math.PI;
                float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
                // point on the circle
                GL.Vertex3(cx + dx * r, cy + dy * r, 0f);
                // point on the square boundary in the same direction (scale so the larger |component| = r)
                float t = Math.Abs(dx) > Math.Abs(dy) ? Math.Abs(dx) : Math.Abs(dy);
                GL.Vertex3(cx + dx / t * r, cy + dy / t * r, 0f);
            }
            GL.End();
        }

        /// <summary>
        /// Colour, dimmed by the cabin brightness setting.
        ///
        /// RGB only - alpha is untouched, because dimming a translucent element by fading it out
        /// would make it VANISH at low brightness rather than get dark, and the bar-gauge tracks are
        /// translucent by design. A real display dims its backlight; it does not become transparent.
        /// </summary>
        private static Color Tint(Rgba c)
        {
            Color u = ColorBridge.To(c);
            if (brightness >= SettingsPage.MaxBright) return u;
            float k = brightness * 0.1f;
            return new Color(u.r * k, u.g * k, u.b * k, u.a);
        }

        /// <summary>
        /// One textured quad per glyph, from the atlas UVs.
        ///
        /// ---- THE Y FLIP, AGAIN ----
        /// CharacterInfo is in font space: y UP from the baseline, so maxY is the top of the glyph.
        /// This render target is y DOWN. So the baseline sits at (top + ascent) and a glyph spans
        /// from (baseline - maxY) to (baseline - minY). Getting this wrong renders text upside down
        /// per-glyph while the line still advances correctly, which looks like a font bug and is not.
        /// </summary>
        private void DrawText(DrawCmd c)
        {
            int size = Mathf.RoundToInt(c.C);
            if (size < 1) return;

            string s = c.Str;
            CharacterInfo ci;

            // Measure first: alignment needs the width, and the width is the sum of the advances.
            float width = 0f;
            for (int i = 0; i < s.Length; i++)
                if (font.GetCharacterInfo(s[i], out ci, size)) width += ci.advance;

            float pen = c.A;
            if (c.Align == TextAlign.Centre) pen -= width * 0.5f;
            else if (c.Align == TextAlign.Right) pen -= width;

            // font.ascent is quoted for font.fontSize, so it scales with the size actually drawn.
            float ascent = (font.fontSize > 0)
                ? font.ascent * ((float)size / font.fontSize)
                : size * 0.8f;
            float baseline = c.B + ascent;

            GL.Begin(GL.QUADS);
            GL.Color(Tint(c.Colour));
            for (int i = 0; i < s.Length; i++)
            {
                if (!font.GetCharacterInfo(s[i], out ci, size)) continue;

                float x0 = pen + ci.minX, x1 = pen + ci.maxX;
                float y0 = baseline - ci.maxY, y1 = baseline - ci.minY;

                GL.TexCoord(ci.uvTopLeft);     GL.Vertex3(x0, y0, 0f);
                GL.TexCoord(ci.uvTopRight);    GL.Vertex3(x1, y0, 0f);
                GL.TexCoord(ci.uvBottomRight); GL.Vertex3(x1, y1, 0f);
                GL.TexCoord(ci.uvBottomLeft);  GL.Vertex3(x0, y1, 0f);

                pen += ci.advance;
            }
            GL.End();
        }

        /// <summary>
        /// Rects are QUADS rather than GL.LINES even when they are one pixel tall: GL has no line
        /// width control, so a "1 px line" from GL.LINES is whatever the driver feels like, while a
        /// 1 px quad is exactly 1 px. Trap #4 in CLAUDE.md is the same problem from the IMGUI side.
        /// </summary>
        private static void DrawRect(DrawCmd c)
        {
            float x = c.A, y = c.B, w = c.C, h = c.D;
            GL.Begin(GL.QUADS);
            GL.Color(Tint(c.Colour));
            GL.Vertex3(x, y, 0f);
            GL.Vertex3(x + w, y, 0f);
            GL.Vertex3(x + w, y + h, 0f);
            GL.Vertex3(x, y + h, 0f);
            GL.End();
        }

        /// <summary>
        /// One line as a rotated quad, so its width is exact (GL.LINES width is driver-dependent - the
        /// same reason DrawRect is a quad). The quad is the segment swept sideways by half the stroke
        /// along the perpendicular of its direction.
        /// </summary>
        private static void DrawLine(DrawCmd c)
        {
            float x0 = c.A, y0 = c.B, x1 = c.C, y1 = c.D;
            float dx = x1 - x0, dy = y1 - y0;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4f) return;
            float hw = c.StartDeg * 0.5f;
            // Perpendicular unit vector, scaled to half the stroke width.
            float px = -dy / len * hw, py = dx / len * hw;
            GL.Begin(GL.QUADS);
            GL.Color(Tint(c.Colour));
            GL.Vertex3(x0 + px, y0 + py, 0f);
            GL.Vertex3(x1 + px, y1 + py, 0f);
            GL.Vertex3(x1 - px, y1 - py, 0f);
            GL.Vertex3(x0 - px, y0 - py, 0f);
            GL.End();
        }

        /// <summary>
        /// Two arcs at different radii zipped into a triangle strip. Vertices come from ArcGeometry -
        /// pure and headless tested, and shared with the PNG preview - so this only uploads them.
        /// </summary>
        private void DrawArcBand(DrawCmd c)
        {
            int n = ArcGeometry.VertexCount(c.EndDeg - c.StartDeg, 2.0);
            if (n > MaxArcPoints) n = MaxArcPoints;
            if (n < 2) return;

            ArcGeometry.ArcScreen(arcInner, n, c.A, c.B, c.C, c.StartDeg, c.EndDeg);
            ArcGeometry.ArcScreen(arcOuter, n, c.A, c.B, c.D, c.StartDeg, c.EndDeg);

            GL.Begin(GL.TRIANGLE_STRIP);
            GL.Color(Tint(c.Colour));
            for (int i = 0; i < n; i++)
            {
                GL.Vertex3(arcInner[i * 2], arcInner[i * 2 + 1], 0f);
                GL.Vertex3(arcOuter[i * 2], arcOuter[i * 2 + 1], 0f);
            }
            GL.End();
        }
    }
}
