// DragonScreen — BASE SCREEN, TABBED (the ICON shell)  (register S242; overseer PROMPT 1 v2)
// ============================================================================================
// PURE. `BaseScreen` plus the nine-tab strip that sits in the window's notch, and the selector line
// that slides under the active tab.
//
// ---- ⛔ ONE GEOMETRY SOURCE FOR THE DRAW AND THE HIT MAP ----
// `TabRect(i)` is the only place a tab's box is computed. `Draw` and `HitTest` both call it, exactly
// as `BottomBar` does for the nav icons — and for the same reason its own header gives
// (`BottomBar.cs:68-77`): *"THE HIT MAP AND THE MARKER MOVE WITH THE DRAW OR NOT AT ALL … Changing one
// without the other slides every nav icon's touch target off its icon on all 35 pages, silently."*
// ⛔ Nine tabs is the same trap with nine chances to fall into it.
//
// ---- ⭐ EVERY Y IS DERIVED, NOT CHOSEN ----
//   shelf 893 + 5.1 pad + 37 icon + 4 gap + 12.3 label + 16 gap + 4.8 selector + 5.3 pad = 977.5,
// which is the border's inner face (border bottom 979 less its 3px stroke, centred: 979 - 1.5). Move
// the shelf and the whole stack moves with it — which is why `Shelf` lives in `BaseScreen` and every
// number here is written as an offset from it rather than as a literal.
// ⭐ The selector's 4.8 height, 81.5 width and 16px standoff are SCALED FROM THE REAL NASA SHEET's own
// line (6 tall, 102 wide, 20 below the label, at that sheet's pitch of 113 against our 90.3) — they
// were measured there and divided, not invented here.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class BaseScreenTabbed
    {
        /// <summary>
        /// ⚠ NINE, not eight. `VehicleTabBar.cs:201` and `docs/reference/NASA_REFERENCE_ART.md:265` both
        /// still assert *"T9's eight tabs are confirmed-real … and are not changed to suit an icon"* —
        /// the owner has OVERRIDDEN that, and the NASA sheet's own tab row shows nine clusters with the
        /// ninth captioned "Comms". ⛔ Those two claims are RAISED AND LEFT ALONE by this task, not
        /// edited; see the register. This array is the rebuild's own list.
        /// </summary>
        public static readonly string[] Tabs =
            { "All", "Crew", "Comms", "Prop", "Mech", "Power", "Avionics", "GNC", "Thermal" };

        public const int TabCount = 9;

        // ---- the strip, in BaseScreen's 1920 x 1054 reference frame -------------------------------
        public const float Pitch = 90.3f;
        public const float FirstCentre = 599.3f;
        public const float IconSize = 37f;
        public const float IconTop = BaseScreen.Shelf + 5.1f;          // 898.1
        public const float LabelPx = 12.3f;
        public const float LabelTop = IconTop + IconSize + 4f;         // 939.1
        public const float SelectorTop = LabelTop + LabelPx + 16f;     // 967.4
        public const float SelectorH = 4.8f;
        public const float SelectorW = 81.5f;

        /// <summary>The centre x of tab `i`, in reference units. ⛔ The ONE place the pitch is applied.</summary>
        public static float TabCentre(int i) { return FirstCentre + i * Pitch; }

        /// <summary>
        /// Tab `i`'s touch box in PANEL pixels — the single geometry source. ⛔ `Draw` and `HitTest`
        /// both go through here so they cannot drift apart.
        /// ⚠ The box is the full stack height (icon through selector), not just the icon: a crew member
        /// aiming at the caption is aiming at the tab.
        /// </summary>
        public static void TabRect(int i, int w, out float x, out float y, out float bw, out float bh)
        {
            float sc = BaseScreen.Sc(w);
            float cx = TabCentre(i);
            bw = Pitch * sc;
            x = (cx - Pitch * 0.5f) * sc;
            y = BaseScreen.Shelf * sc;
            bh = (SelectorTop + SelectorH - BaseScreen.Shelf) * sc;
        }

        /// <summary>Which tab a touch landed on, or -1. ⛔ Reads `TabRect`, never its own arithmetic.</summary>
        public static int HitTest(int w, int h, float px, float py)
        {
            for (int i = 0; i < TabCount; i++)
            {
                float x, y, bw, bh;
                TabRect(i, w, out x, out y, out bw, out bh);
                if (px >= x && px < x + bw && py >= y && py < y + bh) return i;
            }
            return -1;
        }

        // ============================================================================================
        //  THE NOTCHED WINDOW — `BaseScreen`'s window with the tab shelf cut out of its bottom edge
        // ============================================================================================

        /// <summary>
        /// The ICON window's fill. The locked path's bottom edge reads
        /// `… L 1453.5 960.5 L 1386 893 L 534 893 L 466.5 960.5 L 29.5 960.5 …`, so below the shelf the
        /// window survives only at its two ends, each ending in a 45 degree bevel.
        /// </summary>
        public static void WindowFill(DisplayList dl, int w)
        {
            float sc = BaseScreen.Sc(w);
            Rgba c = BasePalette.Ground;
            float L = BaseScreen.WinL, R = BaseScreen.WinR, T = BaseScreen.WinT;
            float B = BaseScreen.WinB, r = BaseScreen.WinR10, sh = BaseScreen.Shelf;

            // The body above the shelf, with the two rounded TOP corners.
            dl.Rect((L + r) * sc, T * sc, (R - L - 2f * r) * sc, r * sc, c);
            dl.Rect(L * sc, (T + r) * sc, (R - L) * sc, (sh - T - r) * sc, c);
            BaseScreen.Corner(dl, sc, L + r, T + r, r, 270.0, 360.0, c);
            BaseScreen.Corner(dl, sc, R - r, T + r, r,   0.0,  90.0, c);

            // The two feet below the shelf, each a rect plus its bevel triangle, plus a rounded
            // BOTTOM corner. ⭐ Left foot: solid lies LEFT of the hypotenuse (534,893)->(466.5,960.5).
            dl.Rect(L * sc, sh * sc, (BaseScreen.NotchOuterL - L) * sc, (B - r - sh) * sc, c);
            dl.Rect((L + r) * sc, (B - r) * sc, (BaseScreen.NotchOuterL - L - r) * sc, r * sc, c);
            BaseScreen.Corner(dl, sc, L + r, B - r, r, 180.0, 270.0, c);
            dl.Tri(BaseScreen.NotchOuterL * sc, sh * sc,
                   BaseScreen.NotchInnerL * sc, sh * sc,
                   BaseScreen.NotchOuterL * sc, B * sc, c);

            // ⭐ Right foot: solid lies RIGHT of the hypotenuse (1386,893)->(1453.5,960.5).
            dl.Rect(BaseScreen.NotchOuterR * sc, sh * sc,
                    (R - BaseScreen.NotchOuterR) * sc, (B - r - sh) * sc, c);
            dl.Rect(BaseScreen.NotchOuterR * sc, (B - r) * sc,
                    (R - r - BaseScreen.NotchOuterR) * sc, r * sc, c);
            BaseScreen.Corner(dl, sc, R - r, B - r, r, 90.0, 180.0, c);
            dl.Tri(BaseScreen.NotchInnerR * sc, sh * sc,
                   BaseScreen.NotchOuterR * sc, sh * sc,
                   BaseScreen.NotchOuterR * sc, B * sc, c);
        }

        /// <summary>The ICON window's 2px white @0.55 outline, following the notch.</summary>
        public static void WindowStroke(DisplayList dl, int w)
        {
            float sc = BaseScreen.Sc(w);
            Rgba c = BasePalette.WindowStroke;
            float px = BaseScreen.WinStrokePx;
            float L = BaseScreen.WinL, R = BaseScreen.WinR, T = BaseScreen.WinT;
            float B = BaseScreen.WinB, r = BaseScreen.WinR10, sh = BaseScreen.Shelf;

            BaseScreen.Seg(dl, sc, L + r, T, R - r, T, px, c);                       // top
            BaseScreen.Seg(dl, sc, R, T + r, R, B - r, px, c);                       // right
            BaseScreen.Seg(dl, sc, R - r, B, BaseScreen.NotchOuterR, B, px, c);      // bottom, right of notch
            BaseScreen.Seg(dl, sc, BaseScreen.NotchOuterR, B, BaseScreen.NotchInnerR, sh, px, c);  // bevel R
            BaseScreen.Seg(dl, sc, BaseScreen.NotchInnerR, sh, BaseScreen.NotchInnerL, sh, px, c); // the shelf
            BaseScreen.Seg(dl, sc, BaseScreen.NotchInnerL, sh, BaseScreen.NotchOuterL, B, px, c);  // bevel L
            BaseScreen.Seg(dl, sc, BaseScreen.NotchOuterL, B, L + r, B, px, c);      // bottom, left of notch
            BaseScreen.Seg(dl, sc, L, B - r, L, T + r, px, c);                       // left
            BaseScreen.CornerStroke(dl, sc, L + r, T + r, r, 270.0, 360.0, px, c);
            BaseScreen.CornerStroke(dl, sc, R - r, T + r, r,   0.0,  90.0, px, c);
            BaseScreen.CornerStroke(dl, sc, R - r, B - r, r,  90.0, 180.0, px, c);
            BaseScreen.CornerStroke(dl, sc, L + r, B - r, r, 180.0, 270.0, px, c);
        }

        // ============================================================================================
        //  THE STRIP
        // ============================================================================================

        /// <summary>
        /// The nine tabs and the selector. ⛔ EVERY PIECE IS LIVE: the icons are placed by code, the
        /// captions are typed, and the selector's x comes from `TabCentre(active)` every frame — none
        /// of it is baked, which is the owner's rule.
        /// ⭐ `active` outside 0..8 draws the tabs with NO selector, which is a real state (a tabbed
        /// page reached before a tab is chosen) and not an error.
        /// </summary>
        public static void DrawTabs(DisplayList dl, int w, int active)
        {
            float sc = BaseScreen.Sc(w);
            for (int i = 0; i < TabCount; i++)
            {
                float cx = TabCentre(i);
                dl.Asset(IconKey(i), (cx - IconSize * 0.5f) * sc, IconTop * sc,
                         IconSize * sc, IconSize * sc, BasePalette.Stroke);
                // ⭐ LINE-HEIGHT PINNED TO THE TYPE SIZE. Left unpinned the text box renders about
                // 2.5 px taller than its ink and the whole block sits low in its slot.
                dl.Text(Tabs[i], cx * sc, LabelTop * sc, LabelPx * sc,
                        TextAlign.Centre, BasePalette.Stroke);
            }
            if (active < 0 || active >= TabCount) return;
            float sx = TabCentre(active) - SelectorW * 0.5f;
            dl.Rect(sx * sc, SelectorTop * sc, SelectorW * sc, SelectorH * sc, BasePalette.Stroke);
        }

        /// <summary>
        /// The asset key for tab `i`'s icon. ⚠ The eight existing keys are reused as-is; the NINTH,
        /// Comms, has no harvested glyph — `NASA_REFERENCE_ART.md:265` records that its wifi glyph
        /// *"was NOT harvested"* because the old strip had no Comms tab to put it on. ⛔ Rather than
        /// invent one, this returns the tab-all key so the slot draws SOMETHING recognisable and the
        /// gap is visible in the register instead of being silently filled with a wrong picture.
        /// </summary>
        public static string IconKey(int i)
        {
            switch (i)
            {
                case 0: return "ic_tab_all";
                case 1: return "ic_tab_crew";
                case 2: return "ic_tab_all";        // ⚠ Comms — no harvested glyph. See the register.
                case 3: return "ic_tab_prop";
                case 4: return "ic_tab_mech";
                case 5: return "ic_tab_power";
                case 6: return "ic_tab_avionics";
                case 7: return "ic_tab_gnc";
                default: return "ic_tab_thermal";
            }
        }

        /// <summary>
        /// The whole ICON shell. Same order as `BaseScreen.Draw` — ground, border, window, strokes —
        /// then the strip, which sits in the band the notch opened.
        /// </summary>
        public static void Draw(DisplayList dl, int w, int h, int active)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            dl.Rect(0f, 0f, w, h, BasePalette.Ground);
            BaseScreen.BorderFill(dl, w);
            WindowFill(dl, w);
            BaseScreen.BorderStroke(dl, w);
            WindowStroke(dl, w);
            DrawTabs(dl, w, active);
        }
    }
}
