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
        // S32: the countdown is a FIELD, and it STAYS where the run left it (0 once a run has ended)
        // rather than springing back to 5 the moment suitStart goes idle. The suit model reads it as
        // "how far through the check this run got", so a run that found a leak has to keep reading 0
        // after its result box is closed - otherwise closing that box un-bleeds the leaking suit, the
        // table goes back to four green Nominals, and TROUBLESHOOT (which responds to exactly that
        // verdict, and is only reachable once the box is out of the way) could never be pressed. A new
        // run, HALT, or a page change puts it back to 5.
        private int suitCountdown = 5;
        // S31: this run's leak roll. The 5% chance is decided ONCE per run, from a seed minted at the
        // moment a run began (INITIATE, TRY ADDITIONAL TIMER, or S32's TROUBLESHOOT repair - see
        // StartSuitRun, the one place that begins one) - a verdict re-rolled every frame would
        // flicker, and both screens showing one run have to agree. The seed is all this glue owns; the
        // model itself is pure (SuitLeak). 0 = no run has been made, so nothing has been found.
        private uint suitSeed;
        private int suitRuns;

        // The Cover's selected deorbit phase (0..4). The rail + ◄/► arrows change it IN-PAGE — it drives
        // the rail highlight + centre heading, and unlike the suit state it persists across visits.
        private int coverPhase = 1;   // default: Coast to Trunk Jettison (as the reference defaults)

        // Which of the Cover's three camera views its right-hand slot is showing (T4). NEXT VIEW cycles
        // it; it opens on EARTH because that is what Frame 67 bakes - see CoverPage.CoverCam. Per screen
        // and not persisted, for the same reason the map scroll position is not: it is where you happen
        // to be looking, not a decision to restore.
        private CoverPage.CoverCam coverCam = CoverPage.CoverCam.Earth;

        // Where this screen's capsule turntable is pointing, and the press that is turning it (T11b).
        // Per screen and not persisted, for the same reason coverCam is not: it is where you happen to
        // be looking. Both are VALUES out of pure/Turntable - this file holds them and forwards touch
        // samples; every decision about what a sample MEANS is made in there, headlessly tested.
        private TurntableState turn = Turntable.Front();
        private TurntableTouch turnTouch = Turntable.Idle();

        // The controls a touch FLIPS on this screen (T14): the subsystem pages' FUNCTIONS|ALERTS tab and
        // the two docking cluster magnitude toggles. Same footing as coverCam and the turntable - per
        // screen, not persisted, because it is what this crew member is looking at rather than a decision
        // to restore. Every DECISION about them is pure (PageControls, and each page's own HitTest); this
        // file only holds the value and forwards the touch.
        private PageControls controls = PageControls.Default;

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
        // Starts in PLANET mode, not the default MAP: under the Figma UI this view belongs to the
        // Cover's camera, which opens on the live globe (coverCam above), and Pan/Zoom/Centre read the
        // mode to decide what they mean.
        private MapView mapView = MapProjection.WithMode(MapProjection.Default(), NavMode.Planet);

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
        /// S86: read-only so the BlackBox (`src/BlackBoxRecorder.cs`) can record the cabin brightness
        /// setting without the screens taking a dependency on it — the arrow stays BlackBox -> tree,
        /// exactly as it does for `livePage`. Shared by all three displays (see `brightness` above), so
        /// the three `brightness_l/c/r` columns legitimately carry the same value every tick.
        /// </summary>
        internal static int Brightness { get { return brightness; } }

        /// <summary>
        /// S94 (S86-Q1, answered by the overseer 2026-09-05: split, not shared). What each screen's
        /// Cover page is showing, mirroring `livePage`'s shape rather than `brightness`'s: `coverCam`
        /// (`:90` below) and `coverPhase` (`:84`) are genuinely PER-`ScreenPainter`-INSTANCE — S86
        /// verified no cross-instance write exists, unlike `livePage`/`Publish()` — so a shared static
        /// would misrepresent two of the three screens the moment they diverge on Cover, which every
        /// screen can do simultaneously (`Configure` opens every screen on Cover with no prior save).
        /// One array slot per screen index, same as `livePage`; written by `PublishCover()`.
        /// </summary>
        private static readonly CoverPage.CoverCam[] liveCoverCam = new CoverPage.CoverCam[4];
        private static readonly int[] liveCoverPhase = new int[4];

        /// <summary>
        /// S94: read-only, same reason as `Brightness` above — lets the BlackBox record which Cover
        /// camera view / deorbit phase EACH screen is showing without the screens depending on it. Per
        /// screen, not shared, per `liveCoverCam`/`liveCoverPhase`'s own header.
        /// </summary>
        internal static CoverPage.CoverCam CoverCamL { get { return liveCoverCam[1]; } }
        internal static CoverPage.CoverCam CoverCamC { get { return liveCoverCam[2]; } }
        internal static CoverPage.CoverCam CoverCamR { get { return liveCoverCam[3]; } }
        internal static int CoverPhaseL { get { return liveCoverPhase[1]; } }
        internal static int CoverPhaseC { get { return liveCoverPhase[2]; } }
        internal static int CoverPhaseR { get { return liveCoverPhase[3]; } }

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
            PublishCover();   // S94: publish the field defaults before any touch, so a BlackBox read
                               // between Configure and the first Cover touch sees the real starting state

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
        /// S94: publish THIS screen's Cover camera/phase, mirroring `Publish()`'s array-per-screen-index
        /// shape. A separate method rather than folded into `Publish()`, because the trigger is
        /// different: `Publish()` fires on a PAGE change, but coverCam/coverPhase can change while the
        /// screen stays on Cover (the rail arrows, NEXT VIEW) — publishing only on page change would
        /// leave the array reading stale the whole time a crew member is actually using the page.
        /// </summary>
        private void PublishCover()
        {
            if (index < 0 || index >= liveCoverCam.Length) return;
            liveCoverCam[index] = coverCam;
            liveCoverPhase[index] = coverPhase;
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
        /// A press, in PAGE pixels. Called from ScreenTouch on the screen's own collider. Every
        /// control on every page acts HERE, on the press - only the capsule turntable has anything
        /// to say about the drag and the release below.
        /// </summary>
        public void TouchDown(float px, float py)
        {
            // ---- NEW FIGMA UI: the design's own navigation, no chrome bar ----
            // The Cover hub's buttons + each page's back chevron ARE the navigation, so touch routes
            // straight through FigmaUI (pure) to a NavHit, which ApplyNav carries out.
            if (FigmaMode)
            {
                // ---- S85: THE ONE APPEND, AT THE ONE PLACE EVERY GLASS PRESS PASSES THROUGH ----
                // Every control on every page on all three screens comes through here, so the CVR
                // press channel (§2.9) is one record built here and one `Append` — not a call per
                // branch that a new page could forget to make. `FigmaTouch` below is the dispatch
                // exactly as it was; all it gained is a `ref` it fills in with what the press WAS.
                //
                // ⛔ THE ARROW POINTS ONE WAY. This names `CrewPressLog` (a pure screen-side queue,
                // the `livePage` idiom in queue form) and NOTHING in the BlackBox. Delete
                // `pure/blackbox/` and `BlackBoxRecorder.cs` and this file still compiles — which is
                // the excisable-by-design constraint (owner, 2026-09-03) the buffer exists to keep.
                CrewPress rec = CrewPressLog.Blank();
                rec.Ut = VesselData.NowUt();
                rec.Screen = index;
                rec.Page = selectedPage;      // the page the crew was LOOKING at, before the press moved it
                rec.Px = px; rec.Py = py;
                // BEFORE the dispatch: this is the CVR's area-microphone context — what was lit when
                // the press was MADE. A SYSTEMS TREE press can change the mask itself, and stamping
                // afterwards would report the consequence as if it were the reason.
                StampAlarms(ref rec);
                FigmaTouch(px, py, ref rec);
                CrewPressLog.Append(rec);
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
        /// THE LIVE TOUCH PATH — the dispatch that used to sit inline in `TouchDown`'s `FigmaMode`
        /// branch, lifted out unchanged by S85 so the press record has exactly one place to be built
        /// and exactly one place to be appended.
        ///
        /// ⛔ THIS is the branch that runs. `FigmaMode` is `private const bool = true`, so
        /// `TouchDown` always returns here and the chrome-bar path below it is compiled-but-dead
        /// (§2.7's second finding). Instrumenting that path instead would have produced a CVR channel
        /// that can never fire, which is the defect this register line exists to end — so the
        /// instrumentation is HERE and there is none down there.
        ///
        /// `rec` is filled in as the dispatch resolves: which surface, which control, and — the field
        /// no poll can ever see — whether the press ACTED. Nothing here reads `rec`, so its presence
        /// cannot change what a press does.
        /// </summary>
        private void FigmaTouch(float px, float py, ref CrewPress rec)
        {
            // Nav (bottom bar / back chevron) wins; anything it does not claim on the Suit Leak
            // Check page drives its START / HALT / popup-close.
            NavHit nh = FigmaUI.HitTest((UiPage)selectedPage, px, py, w, h);
            if (nh.Act != NavAct.None)
            {
                rec.Surface = CrewSurface.Nav;
                rec.EnumValue = (int)nh.Act;
                rec.ControlId = CrewControlIds.Nav(nh.Act, nh.Target);
                // ApplyNav's own return, not a second copy of its three guards: a re-selection of the
                // page already shown, a Back with no history and a Forward at the end of it all move
                // nothing, and all three are presses that a page-column poll cannot distinguish from
                // no press at all.
                rec.Acted = ApplyNav(nh);
                return;
            }
            UiPage cur = (UiPage)selectedPage;
            if (cur == UiPage.SuitCheck)
            {
                // T14 added FINISH (end at step 2.5, raise the result popup) and TRY ADDITIONAL
                // TIMER (re-run the countdown), and left TROUBLESHOOT out because nothing modelled
                // a suit, so nothing could fail. S31 made a suit able to fail and S32 (owner, via
                // the overseer) gave the control its action: it is the fail branch's RECOVERY, so
                // it acts only while the model says a suit failed, and what it does is repair that
                // suit and re-run the check - the same state change TRY ADDITIONAL TIMER makes, so
                // it goes through that same path rather than a second one. The gate is
                // SuitCheckPage.Available, which the page also lights the control from: a dimmed
                // TROUBLESHOOT cannot act, and a live one cannot look unavailable.
                SuitCheckState suits = SuitLeak.From(VesselData.State, suitCountdown, suitPopup, suitSeed);
                SuitCheckPage.SuitAct sa = SuitCheckPage.HitTest(px, py, w, h, suitPopup);
                rec.Surface = CrewSurface.Suit;
                rec.EnumValue = (int)sa;
                rec.ControlId = CrewControlIds.Suit(sa);
                // S85: `acted` by OBSERVATION, not by a second copy of the switch's rules. The four
                // fields below are the whole of this page's state, so comparing them across the
                // dispatch answers "did the press do anything" exactly, and cannot drift when a case
                // is added. It is the only honest answer for the refused TROUBLESHOOT — a press the
                // model declines changes nothing, so nothing anywhere else can see it happened.
                float s0 = suitStart; bool p0 = suitPopup; int c0 = suitCountdown; uint d0 = suitSeed;
                switch (sa)
                {
                    // START and TRY ADDITIONAL TIMER each begin a run, so each mints a fresh seed:
                    // re-running re-rolls, which is what a second timed run of a leak check is.
                    // HALT abandons the run, so its roll goes with it. FINISH ends the run at step
                    // 2.5 and reports what THAT run found, so it keeps the seed it already has -
                    // and parks the countdown at 0, because a finished run is a finished run and
                    // the table must go on agreeing with the verdict the crew was just shown.
                    case SuitCheckPage.SuitAct.Start:  StartSuitRun(); break;
                    case SuitCheckPage.SuitAct.Halt:   suitStart = -1f; suitPopup = false; suitSeed = 0u;
                                                       suitCountdown = 5; break;
                    case SuitCheckPage.SuitAct.Close:  suitPopup = false; break;
                    case SuitCheckPage.SuitAct.Finish: suitStart = -1f; suitPopup = true;
                                                       suitCountdown = 0; break;
                    case SuitCheckPage.SuitAct.Retime: StartSuitRun(); break;
                    // S32: REPAIR + RERUN. The repair is what clears the failure the crew is looking
                    // at; the rerun is the same fresh-seed run the timer control makes, so the next
                    // verdict is ROLLED honestly rather than declared clean by the press.
                    case SuitCheckPage.SuitAct.Troubleshoot:
                        if (SuitCheckPage.Available(SuitCheckPage.SuitAct.Troubleshoot, suits)) StartSuitRun();
                        break;
                }
                rec.Acted = (suitStart != s0 || suitPopup != p0 || suitCountdown != c0 || suitSeed != d0);
            }
            else if (IsSubsystemPage(cur))
            {
                // FUNCTIONS | ALERTS. T5 drew the toggle and left it inert; this is the tap. It is
                // NOT navigation - the page does not change, its body does - so it lands here rather
                // than in FigmaUI, which is why FigmaUI.HitTest above did not claim it.
                int t = VehicleSubsystemPage.ToggleHit(px, py, w, h);
                rec.Surface = CrewSurface.SubsysTab;
                rec.EnumValue = t;
                rec.ControlId = CrewControlIds.SubsysTab(t);
                // Re-tapping the tab already showing is the textbook press with no edge: the body
                // does not change, so nothing a poll can watch moves. `acted` is where it shows up.
                rec.Acted = (t >= 0 && controls.Alerts != (t == 1));
                if (t >= 0) controls.Alerts = (t == 1);
            }
            else if (cur == UiPage.ManualChute)
            {
                // The chute procedure's ACTION buttons. Each one IS a console command (see the page),
                // so it goes through the SAME dispatcher the lower panel's own button does and gets
                // the SAME answer - there is no second policy here and there must never be one.
                int a = ManualChuteDeployPage.HitTest(px, py, w, h);
                rec.Surface = CrewSurface.Chute;
                rec.EnumValue = a;
                rec.ControlId = CrewControlIds.Chute(a);
                if (a >= 0) ChuteAction(a, ref rec);
            }
            else if (cur == UiPage.SystemsTree)
            {
                // S56 / audit H32. The tree's eight POWER + STRING boxes are the plate's own
                // buttons, so they go through the SAME dispatcher and are read back by the SAME
                // policy - there is no second policy here and there must never be one (T14's rule).
                // Nothing on this page flies the vehicle: SystemsState is local display state.
                PanelCommand tc = SystemsTreePage.HitTest(px, py, w, h);
                rec.Surface = CrewSurface.Tree;
                rec.EnumValue = (int)tc;
                rec.ControlId = CrewControlIds.Tree(tc);
                SystemsAction(tc, ref rec);
            }
            else if (cur == UiPage.Docking)
            {
                DockingSimPage.DockAct da = DockingSimPage.HitTest(px, py, w, h);
                rec.Surface = CrewSurface.Dock;
                rec.EnumValue = (int)da;
                rec.ControlId = CrewControlIds.Dock(da);
                DockAction(da, ref rec);
            }
            else if (cur == UiPage.Cover)
            {
                // The rail selects a phase; the ◄/► arrows step through them (wrapping over all 7);
                // NEXT VIEW + the map cluster drive the camera (ApplyCoverCam).
                // S54: the SELECTED PHASE goes in too. On the Reference Content phase the six
                // action/entry rows are not drawn, so they must not be touchable either — this is
                // the one caller that can ever dispatch them, so this is where the real phase belongs.
                CoverPage.CoverButton cb = CoverPage.HitTest(px, py, w, h, coverCam, coverPhase);
                rec.Surface = CrewSurface.Cover;
                rec.EnumValue = (int)cb;
                rec.ControlId = CrewControlIds.Cover(cb);
                int ph = CoverPage.PhaseOf(cb);
                if (ph >= 0) { rec.Acted = (ph != coverPhase); coverPhase = ph; }
                else if (cb == CoverPage.CoverButton.Back)
                {
                    coverPhase = (coverPhase + CoverPage.PhaseCount - 1) % CoverPage.PhaseCount;
                    rec.Acted = true;   // the arrows WRAP, so a step always lands somewhere new
                }
                else if (cb == CoverPage.CoverButton.Forward)
                {
                    coverPhase = (coverPhase + 1) % CoverPage.PhaseCount;
                    rec.Acted = true;
                }
                else if (cb == CoverPage.CoverButton.None
                         && CoverPage.CapsuleHit(px, py, w, h, coverCam))
                {
                    // LAST, and only on what no button claimed: the capsule fills most of the
                    // slot, so testing it any earlier would swallow the NEXT VIEW pill it sits
                    // under. Nothing turns yet - see Turntable.Press.
                    turnTouch = Turntable.Press(px);
                    // S85: the turntable is a DRAG target with no `CoverButton` of its own, so it
                    // gets its own id and the enum slot goes back to "absent" — writing 0 there would
                    // read as `CoverButton.None`, a real member, and claim a button was pressed.
                    rec.ControlId = CrewControlIds.CoverCapsule;
                    rec.EnumValue = -1;
                    rec.Acted = turnTouch.Dragging;
                }
                else rec.Acted = ApplyCoverCam(cb);
                // S94: coverPhase (three branches above) and coverCam (inside ApplyCoverCam) are
                // the only two places either field is ever written - republish unconditionally here
                // rather than at each branch, since at most one of them changed this touch anyway.
                PublishCover();
            }
        }

        /// <summary>
        /// S85: §2.9's area-microphone context — the alarm channel AS IT STOOD when the press was made.
        ///
        /// This is what turns a press record into evidence rather than a keystroke log: "FIRE PYRO,
        /// while sev_system was Alarm and the mask had the propulsion bit lit" is a different fact from
        /// the same press on a quiet board, and §0's misdiagnoses are exactly the kind that need it.
        ///
        /// Both fields stay at -1 when the screen state is not valid. §4.6's rule, in a struct: a blank
        /// is honest, and a zero here would read as "no alarms" on a feed that was not answering.
        /// </summary>
        private static void StampAlarms(ref CrewPress rec)
        {
            PageState ps = VesselData.State;
            if (!ps.Valid) return;
            rec.AlarmMask = Alarms.Mask(ps);
            rec.SevSystem = (int)Alarms.SystemSeverity(ps);
        }

        /// <summary>
        /// A Manual Chute Deploy action was pressed (T14).
        ///
        /// The whole of the decision is elsewhere and that is the point: the command comes from the
        /// page's own step→command map, the dispatch is `FlightCommands.Run` (the one the console plate
        /// uses), and what the outcome MEANS is `PanelPolicy.ResolveImmediate` - which is where
        /// BUILD_PLAN §14.4(a)+(b) live. So pressing DEPLOY DROGUES here and pressing DROGUES & MAINS on
        /// the plate cannot come to different answers. Today three of the four click into silence
        /// (§14.4(a) flight actuation, no flight software yet); ENABLE BACKUP PYROS arms, and lights on
        /// BOTH surfaces because both read the one flag.
        ///
        /// There is no lamp to set from here: the page reads its own lit state from PageState each frame
        /// (ManualChuteDeployPage.Lit), so nothing can latch a light the vehicle state does not support.
        /// </summary>
        /// S85 fills `rec` with the dispatch verdict on the way past - the command, `FlightCommands.Run`'s
        /// own bool, and the `PanelPolicy` outcome. It reads nothing back, so the press is unaffected.
        private void ChuteAction(int action, ref CrewPress rec)
        {
            PanelCommand c = ManualChuteDeployPage.Actions[action].Command;
            if (c == PanelCommand.None)
            {
                // "Monitor altitude" - the crew watching a number, not a command. Nothing to dispatch.
                // `rec` keeps its blanks: no command, not acted, no press kind. A row that commands
                // nothing did nothing, and saying so is different from saying it was refused.
                Debug.Log("[DragonScreen] chute action '" + ManualChuteDeployPage.Actions[action].Act
                          + "' names no command - nothing to do");
                return;
            }
            bool acted = FlightCommands.Run(c);
            PanelPressKind k = PanelPolicy.ResolveImmediate(c, acted, ModeOn(c));
            rec.Cmd = (int)c; rec.Acted = acted; rec.PressKind = (int)k;
            Debug.Log("[DragonScreen] chute " + c + " -> " + k);
        }

        /// <summary>
        /// S56: a SYSTEMS TREE node press. The node IS a console button - POWER 1/2 or STRING nX - so it
        /// is dispatched through `FlightCommands.Run` and resolved by `PanelPolicy` exactly as the plate's
        /// own press is. Pressing POWER 1 on the glass and POWER 1 on the plate therefore cannot come to
        /// different answers, because neither surface owns the answer; the one `SystemsState` does.
        ///
        /// There is no lamp to latch from here either: the tree reads every node's colour and word out of
        /// `PageState.Systems` on the next frame, so a press that the model REFUSED (an isolate on a
        /// tripped string - `Systems.ToggleString` returns false) simply leaves the node reading TRIP.
        /// That is §14.4(a)'s click-no-light-no-action applied to a display-state command, and it is the
        /// same answer the plate gives for the same press.
        /// </summary>
        /// S85 fills `rec` on the way past, exactly as `ChuteAction` does. A REFUSED isolate is
        /// `acted:false` here, and that refusal is invisible everywhere else - the node just goes on
        /// reading TRIP - which is precisely the press a poll of the screen state cannot see.
        private void SystemsAction(PanelCommand c, ref CrewPress rec)
        {
            if (c == PanelCommand.None) return;
            // Charge01 is NOT set here: VesselData.cs:335 already keeps it current every frame for the
            // plate, and a second writer of the same field is how the two surfaces would start to differ.
            bool acted = FlightCommands.Run(c);
            PanelPressKind k = PanelPolicy.ResolveImmediate(c, acted, false);
            rec.Cmd = (int)c; rec.Acted = acted; rec.PressKind = (int)k;
            Debug.Log("[DragonScreen] systems tree " + c + " -> " + k);
        }

        /// <summary>A mode command's state AFTER the press, for the outcome above. Only the one this page
        /// can reach is a mode; anything else is not asked about.</summary>
        private static bool ModeOn(PanelCommand c)
        { return c == PanelCommand.EnableBackupPyros && FlightCommands.BackupPyros; }

        /// <summary>
        /// A manual-docking control was pressed (T14).
        ///
        /// The two cluster magnitude toggles act - they choose how big a nudge the cluster means, which
        /// is screen state and flies nothing. The twelve direction pads (and Reset Positions) would MOVE
        /// the vehicle, so §14.4(a) makes them an honest no-op until Part B: they resolve, they log, and
        /// nothing happens - no light, no action, no red. `Settings` never arrives here; FigmaUI claims it
        /// as navigation before the page is asked.
        /// </summary>
        private void DockAction(DockingSimPage.DockAct a, ref CrewPress rec)
        {
            if (a == DockingSimPage.DockAct.None) return;
            // S85: the two magnitude toggles are the only acts on this page that change anything, and
            // they change SCREEN state, not the vehicle. Everything else falls through to `acted:false`
            // - which is §14.4(a)'s honest no-op stated as data rather than only as a log line, and it
            // is the record that lets a flight prove a direction pad was pressed and flew nothing.
            if (a == DockingSimPage.DockAct.RotMagnitude)   { controls.DockRotLarge = !controls.DockRotLarge; rec.Acted = true; return; }
            if (a == DockingSimPage.DockAct.TransMagnitude) { controls.DockTransLarge = !controls.DockTransLarge; rec.Acted = true; return; }
            Debug.Log("[DragonScreen] docking " + a
                      + (DockingSimPage.IsActuation(a) ? " - no flight software installed (screens-only build)"
                                                       : " - nothing behind this control yet"));
        }

        /// <summary>The six subsystem sub-tabs, which share the FUNCTIONS | ALERTS toggle. Vehicle and
        /// VehicleMech are their own pages and draw no toggle, so they are not here.</summary>
        private static bool IsSubsystemPage(UiPage p)
        {
            switch (p)
            {
                case UiPage.VehicleCrew: case UiPage.VehiclePropulsion: case UiPage.VehiclePower:
                case UiPage.VehicleAvionics: case UiPage.VehicleGnc: case UiPage.VehicleThermal:
                    return true;
                default: return false;
            }
        }

        // ---- THE TURNTABLE DRAG (T11b) ----
        // The only gesture on the screens that is more than a press, so it is the only thing that
        // needs the two entry points below. Both are forwards: the slot the drag is happening in
        // comes from the SAME CoverPage.CapsuleRect the sprite is drawn from (PageAction's one-rect
        // rule), and what a sample means is Turntable's to say.

        /// <summary>The pointer moved while held, in PAGE pixels.</summary>
        public void TouchDrag(float px, float py)
        {
            if (!turnTouch.Dragging) return;
            float sx, sy, sw, sh;
            CoverPage.CapsuleRect(w, h, out sx, out sy, out sw, out sh);
            turn = Turntable.Move(turn, turnTouch, px, sw, out turnTouch);
        }

        /// <summary>The press ended. A press that barely travelled is a TAP, and a tap on the capsule
        /// is §5 C4's reset - the vehicle goes back to the authored front.</summary>
        public void TouchUp()
        {
            if (!turnTouch.Dragging) return;
            float sx, sy, sw, sh;
            CoverPage.CapsuleRect(w, h, out sx, out sy, out sw, out sh);
            turn = Turntable.Release(turn, turnTouch, sw, out turnTouch);
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
                suitStart = -1f; suitPopup = false; suitSeed = 0u;   // the Suit Leak Check opens fresh each visit
                suitCountdown = 5;
                turnTouch = Turntable.Idle();         // a press cannot survive the page under it
                controls = PageControls.Default;      // and neither do the page's own toggles (T14)
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
        /// <returns>
        /// S85: whether the navigation actually MOVED. The three `false` cases - a Goto to the page
        /// already shown, a Back with no history behind it, a Forward with none ahead - are real
        /// presses that change no state, so no poll of `PageState.ScreenPages` can ever see them.
        /// Returned rather than re-derived at the call site, so the answer and the guards that decide
        /// it can never drift apart.
        /// </returns>
        private bool ApplyNav(NavHit nh)
        {
            switch (nh.Act)
            {
                case NavAct.Goto:
                    int p = (int)nh.Target;
                    if (p == selectedPage) return false;
                    if (uiHistIndex >= 0 && uiHistIndex < uiHistory.Count - 1)
                        uiHistory.RemoveRange(uiHistIndex + 1, uiHistory.Count - uiHistIndex - 1);
                    uiHistory.Add(p);
                    uiHistIndex = uiHistory.Count - 1;
                    SelectPage(index, p);
                    return true;
                case NavAct.Back:
                    if (uiHistIndex > 0) { uiHistIndex--; SelectPage(index, uiHistory[uiHistIndex]); return true; }
                    return false;
                case NavAct.Forward:
                    if (uiHistIndex >= 0 && uiHistIndex < uiHistory.Count - 1)
                    { uiHistIndex++; SelectPage(index, uiHistory[uiHistIndex]); return true; }
                    return false;
            }
            return false;
        }

        /// <summary>
        /// The Cover's camera controls (T4). NEXT VIEW cycles First.vue's three views; the MAP view's
        /// pan/centre/zoom cluster drives the SAME MapView the globe already uses, through the same pure
        /// MapProjection calls the NAV page's cluster does - one map state, two front ends.
        /// </summary>
        /// <returns>
        /// S85: whether this button is one this method CARRIES OUT. The Cover's action rows, entry
        /// plates and MENU/SETTINGS all arrive here too (FigmaUI claims only the last two as
        /// navigation, and the four `Act*` rows are display-only by design) - they match no case, so
        /// they are `false`: pressed, and did nothing. That is the §14.4(a)-shaped fact for the glass,
        /// and it is invisible to anything that watches state instead of presses.
        /// </returns>
        private bool ApplyCoverCam(CoverPage.CoverButton cb)
        {
            switch (cb)
            {
                case CoverPage.CoverButton.NextView:
                    coverCam = CoverPage.NextCam(coverCam);
                    // The mode FOLLOWS the camera rather than being a second copy of which view is up:
                    // Pan/Zoom/Centre branch on it, and the two disagreeing would pan the globe.
                    mapView = MapProjection.WithMode(mapView, CoverPage.CamMapMode(coverCam));
                    return true;

                case CoverPage.CoverButton.MapPanLeft:  mapView = MapProjection.Pan(mapView, -1.0, 0.0); return true;
                case CoverPage.CoverButton.MapPanRight: mapView = MapProjection.Pan(mapView, 1.0, 0.0); return true;
                case CoverPage.CoverButton.MapPanUp:    mapView = MapProjection.Pan(mapView, 0.0, 1.0); return true;
                case CoverPage.CoverButton.MapPanDown:  mapView = MapProjection.Pan(mapView, 0.0, -1.0); return true;
                case CoverPage.CoverButton.MapZoomIn:   mapView = MapProjection.Zoom(mapView, 1); return true;
                case CoverPage.CoverButton.MapZoomOut:  mapView = MapProjection.Zoom(mapView, -1); return true;
                case CoverPage.CoverButton.MapCentre:
                {
                    PageState st = VesselData.State;
                    mapView = MapProjection.Centre(mapView, st.Latitude, st.Longitude);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Begin a Suit Leak Check run: the countdown from the top, no result yet, and a FRESH
        /// seed so the 5% roll is made again (S31). INITIATE, TRY ADDITIONAL TIMER and S32's TROUBLESHOOT
        /// repair all begin a run, and they must begin the SAME one - a second copy of this is how the
        /// repair path would quietly drift away from the timer path.</summary>
        private void StartSuitRun()
        {
            suitStart = Time.realtimeSinceStartup;
            suitPopup = false;
            suitCountdown = 5;
            suitSeed = SuitLeak.SeedFrom(suitStart, ++suitRuns);
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
            // Same for the scaled-space planet camera (S10b): only NAV's 3D PLANET view asks for it,
            // and that is a small fraction of a mission.
            ScaledPlanetRenderer.Idle();

            // Re-read PluginData/tuning.cfg so a [Tunable] can be dialled while looking at the glass
            // (S44 - nothing called this before, so the file was inert and the trims were dead).
            // The note below is right that this Update runs once per SCREEN, three times a frame -
            // and Poll is the same shape as the two Idle() calls above it: a global static that is
            // safe to call redundantly because it guards itself, in this case to one file-stat per
            // second. Three calls a frame therefore still cost one stat a second, not three.
            Tuning.Poll();

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
                // S32: it lands on 0 and STAYS there (the field, not a local reset to 5 every frame), so
                // the table behind and after the result box keeps reading the verdict that run reached.
                if (up == UiPage.SuitCheck && suitStart >= 0f)
                {
                    float el = Time.realtimeSinceStartup - suitStart;
                    if (el >= 5f) { suitPopup = true; suitStart = -1f; suitCountdown = 0; }
                    else { suitCountdown = 5 - (int)(el / 0.9f); if (suitCountdown < 0) suitCountdown = 0; }
                }

                // The capsule turntable is the one asset set with a MEMORY BUDGET (T11b): 36 frames
                // at 2 MB each is ~75 MB if every frame a drag touches is kept. Say what this screen
                // is looking at and ImageStore keeps the window around it - and only that window. The
                // claim is made here, in the one place that knows which page and which camera is up.
                if (up == UiPage.Cover && coverCam == CoverPage.CoverCam.Capsule)
                    ImageStore.WarmTurntable(index, Turntable.FrameOf(turn));
                else
                    ImageStore.ReleaseTurntable(index);

                // The new Figma pages carry their own chrome (each has its bottom bar), so no ChromeBar.
                FigmaUI.Build(page, up, w, h, ps, mapView, suitCountdown, suitPopup, coverPhase, coverCam, turn,
                              controls, suitSeed);
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

                // ---- AND CLAIM THE SCALED-SPACE CAMERA THE SAME WAY (S10b) ----
                // NAV's 3D PLANET view is the one page that draws the live globe, so it is the one
                // page that keeps a second full camera alive. The crew's spin and zoom go with the
                // claim because the painter owns the MapView; the camera never reads display state
                // for itself.
                //
                // PlanetCamLive is then re-read HERE rather than trusted from VesselData: that runs
                // at the top of the frame, before this claim, and on a 0.2 s cadence besides, so the
                // flag it left is up to five frames stale. Re-reading is per-screen and same-frame,
                // which is what the page needs - screen 2 on NAV must not make screen 1 claim a feed.
                if (selectedPage == 2 && mapView.Mode == NavMode.Planet)
                    ScaledPlanetRenderer.Request(mapView.PlanetRotDeg, mapView.PlanetZoom);
                ps.PlanetCamLive = ImageStore.ScaledPlanetLive();

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
