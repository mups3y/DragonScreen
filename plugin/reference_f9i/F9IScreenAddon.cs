/*
 * F9IScreen - F9IScreenAddon
 *
 * THE GLUE. Everything that touches KSP or Unity lives in src/; nothing here does arithmetic that
 * could be tested headless - that belongs in src/pure. This is the F9_LOP lesson applied:
 *     "THE BUG IS IN THE GLUE, AND THE GLUE IS THE PART WITH NO TESTS."
 * So keep this file boring and obvious. If a method here starts calculating something, move it.
 *
 * WHY KSPAddon AND NOT PartModule: a PartModule needs a ModuleManager patch, attaches per part, and
 * would put the window's lifetime in the hands of whichever part happened to survive staging. The
 * screen belongs to the FLIGHT SCENE, not to a part. KSPAddon(Flight) loads itself with no cfg at
 * all - one less file and one less thing to get wrong.
 *
 * CLICK-THROUGH IS NOT HAND-ROLLED. ClickThroughBlocker is already installed (000_ClickThroughBlocker)
 * and ClickThruBlocker.GUILayoutWindow is a drop-in for GUILayout.Window that stops clicks landing on
 * the game world behind the panel. Writing our own InputLockManager juggling would be a worse version
 * of a solved problem.
 */
using System;
using UnityEngine;
using ClickThroughFix;

namespace F9IScreen
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class F9IScreenAddon : MonoBehaviour
    {
        // A window id that will not collide with another mod's. Arbitrary but fixed.
        private const int WindowId = 0x0F91;

        private Rect windowRect;
        private bool visible = true;
        private bool stylesBuilt;
        private bool rectLogged;

        // Textures are created ONCE and disposed in OnDestroy. Building a texture inside OnGUI would
        // allocate every frame and leak - a classic KSP mod performance bug, and the whole point of
        // this rewrite is to stop paying frame cost for the interface.
        private Texture2D texBackground, texPanel, texHairline, texAccent;

        // Cached swatch strip. Built once in Start, disposed in OnDestroy.
        // An earlier version of DrawWindow built these inside the draw call and relied on
        // Destroy(t, 0.1f) to clean up - which is the exact per-frame allocation the comment above
        // forbids. OnGUI runs at least TWICE per frame (Layout then Repaint), so that was ~20
        // textures created and queued for destruction every frame, in the one file whose whole
        // purpose is to stop the interface costing frame time.
        // ---- the Dragon page image ----
        // A rasterised Figma reference frame, loaded from GameData at startup and drawn as the page
        // background. This is a LOOK PREVIEW, not the final mechanism: the real pages will be drawn
        // from geometry so the values can move. It exists now because seeing the actual thing in game
        // settles proportions, palette and legibility questions that no amount of discussion will.
        private Texture2D texPage;
        private bool pageTried;

        private Texture2D[] texSwatches;
        private static readonly Rgba[] SwatchColours = {
            DragonPalette.Background, DragonPalette.Panel, DragonPalette.Hairline,
            DragonPalette.Accent, DragonPalette.Go, DragonPalette.Caution,
            DragonPalette.Alarm, DragonPalette.Text0, DragonPalette.Text4,
            DragonPalette.Text8 };

        private GUIStyle stBackground, stTitle, stLabel, stValue, stValueDim, stGo, stNoGo;

        // ---- the bridge, and why it is re-found rather than cached forever ----
        // The module handle is cached against the vessel it came from. Docking MERGES two vessels and
        // undocking SPLITS one, so a handle held across either can end up pointing at a part that is
        // no longer on the vessel being flown. That is exactly audit issue #68 - a stale part handle
        // that stayed valid just long enough to look fine and act on the wrong craft. Cheap to avoid:
        // compare the vessel each frame, re-search only when it differs.
        private F9IBridgeModule bridge;
        private Vessel bridgeVessel;

        // ---- "WE HAVE ALREADY LOOKED" IS NOT THE SAME AS "WE FOUND ONE" ----
        // The first version keyed the re-search off `bridge == null`, which meant that on any vessel
        // WITHOUT a bridge - i.e. everything that is not a Dragon - the test was true every single call
        // and FindOnActiveVessel walked the whole part list again. OnGUI runs at least twice per frame,
        // so that was two full part scans per frame, forever, on every craft in the game. Exactly the
        // trap the kOS sender guards against with its compare-before-scan ordering.
        // A separate "searched" flag is what makes a NEGATIVE result cacheable.
        private bool bridgeSearched;

        private F9IBridgeModule Bridge()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) { bridge = null; bridgeVessel = null; bridgeSearched = false; return null; }
            // Re-search when the vessel changed, or when a handle we hold no longer belongs to it -
            // docking MERGES two vessels and undocking SPLITS one, and a handle surviving either is
            // audit issue #68 all over again.
            if (!bridgeSearched || bridgeVessel != v || (bridge != null && bridge.vessel != v))
            {
                bridge = F9IBridgeModule.FindOnActiveVessel();
                bridgeVessel = v;
                bridgeSearched = true;
            }
            return bridge;
        }

        public void Start()
        {
            float w = ScreenLayout.PanelWidth;
            float h = ScreenLayout.PanelHeight;
            // ---- LEFT SIDE ON PURPOSE, WHILE THIS IS A DEVELOPMENT BUILD. ----
            // The panel's FINAL home is top right. That is exactly where the live kOS F9I window
            // already is - `GUI(600)` at x=-130, about 616x195 drawn - so putting this there now would
            // drop an 870 px panel straight on top of the working interface. During the port both have
            // to be visible at once: the kOS window is the reference being copied AND the thing still
            // flying the rocket.
            // Move this to `Screen.width - w - 40f` on the day the kOS window retires for Crew Dragon,
            // and not before.
            windowRect = new Rect(40f, 40f, w, h);

            texBackground = SolidTexture(DragonPalette.Background);
            texPanel      = SolidTexture(DragonPalette.Panel);
            texHairline   = SolidTexture(DragonPalette.Hairline);
            texAccent     = SolidTexture(DragonPalette.Accent);

            texSwatches = new Texture2D[SwatchColours.Length];
            for (int i = 0; i < SwatchColours.Length; i++)
                texSwatches[i] = SolidTexture(SwatchColours[i]);

            LoadPageTexture();

            Debug.Log("[F9IScreen] flight addon started, panel "
                      + w.ToString("F0") + "x" + h.ToString("F0")
                      + ", hairline " + ScreenLayout.StrokePx(ScreenLayout.RefStroke, w) + " px");
        }

        public void OnDestroy()
        {
            Destroy(texBackground);
            Destroy(texPanel);
            Destroy(texHairline);
            Destroy(texAccent);
            if (texSwatches != null)
                for (int i = 0; i < texSwatches.Length; i++) Destroy(texSwatches[i]);
            if (texPage != null) Destroy(texPage);
        }

        /// <summary>
        /// Load the page image from GameData.
        ///
        /// Read off disk rather than through KSP's GameDatabase because the texture is not a part
        /// asset and does not want to sit in KSP's texture cache being mipmapped and compressed - it
        /// is a UI image that must stay pixel-exact, and DXT compression on flat navy with hairlines
        /// produces exactly the blocking this design cannot afford.
        ///
        /// FAILS SOFT AND SAYS SO. A missing or unreadable image leaves texPage null and the panel
        /// falls back to the plain layout; it does not throw, and it logs once. An addon that dies in
        /// Start takes the whole window with it.
        /// </summary>
        private void LoadPageTexture()
        {
            if (pageTried) return;
            pageTried = true;
            try
            {
                string path = System.IO.Path.Combine(
                    KSPUtil.ApplicationRootPath, "GameData/F9IScreen/dragon_page_58.png");
                if (!System.IO.File.Exists(path))
                {
                    Debug.Log("[F9IScreen] no page image at " + path + " - using plain layout");
                    return;
                }
                byte[] data = System.IO.File.ReadAllBytes(path);
                // mipChain false: this is drawn at roughly 1:1 and mips would only soften the hairlines.
                Texture2D t = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!t.LoadImage(data))
                {
                    Debug.LogWarning("[F9IScreen] page image failed to decode - using plain layout");
                    Destroy(t);
                    return;
                }
                t.wrapMode = TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Bilinear;
                texPage = t;
                Debug.Log("[F9IScreen] page image loaded, " + t.width + "x" + t.height);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[F9IScreen] page image load threw, using plain layout: " + e.Message);
            }
        }

        /// <summary>Convert a pure Rgba to a Unity colour. The ONLY place the two type systems meet.</summary>
        private static Color ToColor(Rgba c)
        {
            return new Color(c.R, c.G, c.B, c.A);
        }

        private static Texture2D SolidTexture(Rgba c)
        {
            Texture2D t = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            t.SetPixel(0, 0, ToColor(c));
            // Clamp, not repeat: a stretched 1x1 with repeat can sample its own edge and shimmer.
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Point;
            t.Apply();
            return t;
        }

        /// <summary>
        /// Styles must be built inside OnGUI - Unity's GUI.skin is not valid before the first GUI
        /// pass, and building them in Start() silently yields null fonts.
        /// </summary>
        private void BuildStyles()
        {
            stBackground = new GUIStyle(GUI.skin.box);
            stBackground.normal.background = texBackground;
            stBackground.border = new RectOffset(0, 0, 0, 0);
            stBackground.margin = new RectOffset(0, 0, 0, 0);
            stBackground.padding = new RectOffset(0, 0, 0, 0);

            stTitle = new GUIStyle(GUI.skin.label);
            stTitle.normal.textColor = ToColor(DragonPalette.Accent);
            stTitle.fontSize = 15;
            stTitle.alignment = TextAnchor.MiddleLeft;

            // Dimmed label / bright value is the pairing the reference art uses everywhere: the
            // caption sits well down the grey ladder so the number carries the eye.
            stLabel = new GUIStyle(GUI.skin.label);
            stLabel.normal.textColor = ToColor(DragonPalette.Text6);
            stLabel.fontSize = 11;

            stValue = new GUIStyle(GUI.skin.label);
            stValue.normal.textColor = ToColor(DragonPalette.Text0);
            stValue.fontSize = 13;

            // Used when the bridge has never delivered a payload. Dimmed rather than hidden, because
            // an empty slot and a slot showing nothing-yet look identical, and only one of them is a
            // problem worth investigating.
            stValueDim = new GUIStyle(stValue);
            stValueDim.normal.textColor = ToColor(DragonPalette.Text7);

            // GO / NO-GO get their own cached styles. Building a GUIStyle inside the draw call to
            // recolour one word is the same per-frame allocation as building a texture there - and I
            // did exactly that here before catching it, having already caught it once on the swatch
            // strip. If a style depends on state, make one style per state up front; never mutate or
            // clone inside OnGUI.
            stGo = new GUIStyle(stValue);
            stGo.normal.textColor = ToColor(DragonPalette.Go);
            stNoGo = new GUIStyle(stValue);
            stNoGo.normal.textColor = ToColor(DragonPalette.Alarm);

            // ---- kOS SENDS RICH TEXT, SO THE STYLES HAVE TO EXPECT IT ----
            // The message lines are the interface's own GUI labels, and F9I writes markup straight into
            // them: "<b><color=red>ABORT</color></b>", "<color=green>GO</color> for Catch". Unity's IMGUI
            // renders those tags only when the style opts in; without it the pilot reads the tags
            // instead of the word, and the one string on this panel that has to be legible in a hurry is
            // the one that arrives wrapped in the most markup.
            // Set on every text style rather than just the strip, because any of them can end up
            // carrying a message field as the panel grows.
            stTitle.richText = true;
            stLabel.richText = true;
            stValue.richText = true;
            stValueDim.richText = true;
            stGo.richText = true;
            stNoGo.richText = true;

            stylesBuilt = true;
        }

        public void OnGUI()
        {
            if (!visible) return;

            // ---- NO BRIDGE, NO PANEL. THIS IS THE WHOLE "IT SHOWS ON EVERY VESSEL" FIX. ----
            // KSPAddon(Flight) loads into EVERY flight scene - that is what makes it independent of any
            // part - so the addon itself cannot be scoped by the ModuleManager patch. The cfg correctly
            // puts F9IBridgeModule on the V2 crew pods only, but nothing was stopping the WINDOW from
            // drawing on a booster, a fairing, a probe or the station; it just drew "no bridge on this
            // vessel" over whatever the player was actually flying.
            // The bridge module IS the scope test: it exists exactly on the pods the screen is for.
            // Ask before drawing anything, and the panel becomes Dragon-only without the cfg or the
            // addon needing to know each other's part names.
            if (Bridge() == null) return;

            if (!stylesBuilt) BuildStyles();

            // ---- GUIWindow, NOT GUILayoutWindow. THIS EXACT LINE MADE THE PANEL INVISIBLE. ----
            // GUILayoutWindow wraps GUILayout.Window, which OVERRIDES the supplied Rect's size using
            // whatever GUILayout content the window function emits. DrawWindow lays everything out with
            // absolute GUI.Label / GUI.DrawTexture calls and emits NO GUILayout elements at all, so the
            // computed size was zero and the window rendered as nothing - no error, no exception, the
            // addon logging a clean start and simply drawing a 0x0 box.
            // GUIWindow wraps GUI.Window, which honours the Rect exactly. That is the right pairing for
            // absolute layout, and absolute layout is what a fixed-geometry instrument panel wants.
            windowRect = ClickThruBlocker.GUIWindow(
                WindowId, windowRect, DrawWindow, "", stBackground);

            // One-shot: report the rect the window ACTUALLY ended up with, on the first repaint.
            // The GUILayoutWindow bug above was invisible precisely because everything logged fine and
            // the failure was a silently computed zero size. If the panel is ever missing again, this
            // line says whether it is a geometry problem (rect wrong) or a drawing one (rect right,
            // nothing in it) without needing another restart to find out.
            if (!rectLogged && Event.current != null && Event.current.type == EventType.Repaint)
            {
                rectLogged = true;
                Debug.Log("[F9IScreen] first repaint, window rect = "
                          + windowRect.x.ToString("F0") + "," + windowRect.y.ToString("F0")
                          + " " + windowRect.width.ToString("F0") + "x" + windowRect.height.ToString("F0")
                          + "  screen " + Screen.width + "x" + Screen.height);
            }
        }

        /// <summary>Draw a pixel-snapped hairline. Horizontal if h is the thin axis.</summary>
        private void HLine(float x, float y, float w)
        {
            int s = ScreenLayout.StrokePx(ScreenLayout.RefStroke, windowRect.width);
            GUI.DrawTexture(new Rect(x, ScreenLayout.SnapToPixel(y, s), w, s), texHairline);
        }

        /// <summary>
        /// One caption/value row inside a COLUMN. Returns the next y.
        /// x is the column's left edge and colW its width - not the panel's. The portrait version took
        /// the panel width and subtracted the panel padding, which silently overflows the moment the
        /// row is placed in a two-column layout: the right column's value would have run off the panel.
        /// Row height is 22 with the text inset 2, because at 16 the 13 px font was clipping its
        /// ascenders - visible in the first in-game screenshot.
        /// </summary>
        private float StateRow(float x, float y, float colW, string caption, string value, bool live)
        {
            GUI.Label(new Rect(x, y + 2f, 96f, 18f), caption, stLabel);
            bool has = live && value != null && value.Length > 0;
            GUI.Label(new Rect(x + 100f, y + 2f, colW - 108f, 18f),
                      has ? value : "-", has ? stValue : stValueDim);
            return y + 22f;
        }

        private void DrawWindow(int id)
        {
            // ---- LANDSCAPE LAYOUT ----
            // The panel is now the reference aspect (1.62:1), so there is width to use and not much
            // height. Messages get the full width because they are sentences; the state fields are
            // short pairs and go in two columns underneath. Laying them out in one tall column, as the
            // portrait version did, would waste half the panel and push the useful rows off the bottom.
            float pad = 16f;
            float w = windowRect.width;
            float h = windowRect.height;
            float inner = w - pad * 2f;

            F9IBridgeModule b = Bridge();
            BridgeState s = (b != null) ? b.State : null;
            bool live = (s != null && s.Valid);

            // ---- the Dragon page, drawn FIRST so everything else sits on top of it ----
            // Filling the whole window: the panel now carries the reference aspect exactly, so the
            // image maps corner to corner with no letterboxing and no distortion. That is what
            // deriving PanelHeight from RefAspect bought us.
            bool page = (texPage != null);
            if (page) GUI.DrawTexture(new Rect(0f, 0f, w, h), texPage, ScaleMode.StretchToFill);

            // ---- TWO EXCLUSIVE VIEWS ----
            // PAGE view: the rasterised Dragon reference, clean, with one compact status strip so the
            //            bridge state is still visible without covering the art.
            // DATA view: the plain rows, used when there is no page image.
            // The first version drew the data rows on top of the page - it suppressed only the title
            // and the swatches - so the reference art was covered in debug furniture. Two views that
            // share a surface have to be exclusive, not layered.
            if (page)
            {
                DrawStatusStrip(pad, h - 26f, inner, b, s, live);
            }
            else
            {
                DrawDataView(pad, inner, w, h, b, s, live);
            }

            // Drag by the whole panel. The Dragon screen has no title bar to grab.
            GUI.DragWindow();
        }

        /// <summary>
        /// Compact one-line status, drawn over the PAGE view. Deliberately tiny: the point of the page
        /// view is to see the art, but a panel that cannot tell you the bridge is dead is a trap.
        /// </summary>
        private void DrawStatusStrip(float pad, float y, float inner,
                                     F9IBridgeModule b, BridgeState s, bool live)
        {
            string txt;
            if (b == null)     txt = "no bridge on this vessel";
            else if (!live)    txt = "bridge found, waiting for kOS";
            else               txt = s.Program + "   |   " + s.DragonPhase + "   |   " + s.Message1;
            if (live && s.RejectCount > 0) txt += "   |   " + s.RejectCount + " REJECTED";
            GUI.Label(new Rect(pad, y, inner, 20f), txt,
                      (b != null && live && s.RejectCount == 0) ? stLabel : stValueDim);
        }

        /// <summary>
        /// The plain data view - used when no page image is present. This is the fallback that keeps
        /// the panel useful if the art is missing, and it is what the real pages will eventually
        /// replace field by field.
        /// </summary>
        private void DrawDataView(float pad, float inner, float w, float h,
                                  F9IBridgeModule b, BridgeState s, bool live)
        {
            GUI.Label(new Rect(pad, 10f, inner - 260f, 22f), "FALCON 9 INTERFACE", stTitle);
            string mission = (live && s.MissionName.Length > 0) ? s.MissionName : "no mission set";
            GUI.Label(new Rect(w - pad - 260f, 12f, 260f, 20f), mission, live ? stValue : stValueDim);
            HLine(pad, 36f, inner);

            float y = 46f;
            for (int i = 0; i < 3; i++)
            {
                string text = live ? MessageAt(s, i) : "";
                GUI.DrawTexture(new Rect(pad, y, inner, 24f), texPanel);
                GUI.Label(new Rect(pad + 8f, y + 3f, inner - 16f, 20f),
                          (text.Length > 0) ? text : "-",
                          (text.Length > 0) ? stValue : stValueDim);
                y += 28f;
            }

            y += 6f; HLine(pad, y, inner); y += 12f;

            float colW = inner / 2f;
            float colR = pad + colW;
            float yl = y, yr = y;
            yl = StateRow(pad,  yl, colW, "PROGRAM", live ? s.Program      : "", live);
            yl = StateRow(pad,  yl, colW, "PAGE",    live ? s.Page         : "", live);
            yl = StateRow(pad,  yl, colW, "DRAGON",  live ? s.DragonPhase  : "", live);
            yr = StateRow(colR, yr, colW, "STATION", live ? s.StationPhase : "", live);
            yr = StateRow(colR, yr, colW, "DOCKING", live ? s.DockingMode  : "", live);
            yr = StateRow(colR, yr, colW, "PROFILE",
                          live ? BridgeProtocol.LandProfileName(s.LandProfile) : "", live);
            y = (yl > yr) ? yl : yr;

            GUI.Label(new Rect(pad, y, 96f, 20f), "DIRECTOR", stLabel);
            if (live)
                GUI.Label(new Rect(pad + 100f, y, 200f, 20f),
                          s.FlightDirectorGo ? "GO" : "NO-GO", s.FlightDirectorGo ? stGo : stNoGo);
            else
                GUI.Label(new Rect(pad + 100f, y, 200f, 20f), "-", stValueDim);
            y += 26f;

            HLine(pad, y, inner); y += 12f;

            string health;
            if (b == null)     health = "no bridge on this vessel (not a Dragon V2 pod?)";
            else if (!s.Valid) health = "bridge found, waiting for first push from kOS";
            else               health = "bridge live" + (s.RejectCount > 0
                                        ? "   -   " + s.RejectCount + " payload(s) REJECTED" : "");
            bool healthy = (b != null && s != null && s.Valid && s.RejectCount == 0);
            GUI.Label(new Rect(pad, y, inner, 20f), health, healthy ? stLabel : stValueDim);

            // Palette / hairline proof. Scaffolding - delete with the data view once real pages land.
            float py = h - 46f;
            GUI.Label(new Rect(pad, py - 18f, 340f, 16f),
                      "palette / hairline proof   -   hairline "
                      + ScreenLayout.StrokePx(ScreenLayout.RefStroke, w) + " px at panel "
                      + w.ToString("F0") + "x" + h.ToString("F0"), stLabel);
            float sw = inner / texSwatches.Length;
            for (int i = 0; i < texSwatches.Length; i++)
                GUI.DrawTexture(new Rect(pad + i * sw, py, sw - 2f, 24f), texSwatches[i]);
        }

        /// <summary>Message line by index, without allocating an array every frame.</summary>
        private static string MessageAt(BridgeState s, int i)
        {
            string v = (i == 0) ? s.Message1 : (i == 1) ? s.Message2 : s.Message3;
            return v ?? "";
        }
    }
}
