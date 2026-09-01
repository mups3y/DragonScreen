// DragonScreen — MenuPage  (PURE: UiPage.Menu, the navigation index)
// ============================================================================================
// §14.4(c) (owner-resolved 2026-09-02, reconstruct-from-function, tier-3 -> tier-2): the Figma set
// has no Menu frame, so this page's LAYOUT is ours. Its CONTENT is not invented — every card is a
// real UiPage and its real title (FigmaUI.Name, the same string every other page's chrome uses), so
// the only new copy on the whole page is the "MENU" heading. It fills the real 25-30-page need the
// 5-icon bottom bar cannot reach: every built (and not-yet-built) page, one tap away.
//
// Every card is wired now, the same rule FigmaUI's header states for the whole UI: a page that
// exists gets a real destination, a page that does not yet exist still routes there and lands on the
// honest PlaceholderPage (FigmaUI.Build's default case) rather than a dead tap.
// ============================================================================================
using System;
using System.Collections.Generic;

namespace DragonScreen
{
    public static class MenuPage
    {
        public const int Commands = 180;   // background + heading + 27 cards (rect+box+text) + bottom bar
        const float RefW = 3427f, RefH = 2112f;

        const int Cols = 3, Rows = 9;
        const float Margin = 90f, Top = 210f, Bottom = 1830f, Gap = 24f;

        /// <summary>Every page but Menu itself, in enum order (the same order FigmaUI.Name reads).</summary>
        public static readonly UiPage[] Entries = BuildEntries();

        static UiPage[] BuildEntries()
        {
            var list = new List<UiPage>(FigmaUI.PageCount - 1);
            for (int i = 0; i < FigmaUI.PageCount; i++)
                if ((UiPage)i != UiPage.Menu) list.Add((UiPage)i);
            return list.ToArray();
        }

        /// <summary>Card i's rect in DESIGN space. The one source of truth Build, HitTest and the
        /// headless nav test all share, so the drawn grid and the hit grid can never drift apart.</summary>
        public static void CellRect(int i, out float x, out float y, out float w, out float h)
        {
            float gridW = RefW - Margin * 2f, gridH = Bottom - Top;
            w = (gridW - Gap * (Cols - 1)) / Cols;
            h = (gridH - Gap * (Rows - 1)) / Rows;
            int col = i % Cols, row = i / Cols;
            x = Margin + col * (w + Gap);
            y = Top + row * (h + Gap);
        }

        public static void Build(DisplayList dl, int w, int h)
        {
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            int St(float rs) { int p = (int)Math.Round(rs * sy); return p < 1 ? 1 : p; }

            dl.Rect(0, 0, w, h, DragonPalette.Background);
            dl.Text("MENU", w * 0.5f, PY(60), SZ(56), TextAlign.Centre, DragonPalette.White);

            for (int i = 0; i < Entries.Length; i++)
            {
                float cx, cy, cw, ch;
                CellRect(i, out cx, out cy, out cw, out ch);

                dl.Rect(PX(cx), PY(cy), cw * sx, ch * sy, DragonPalette.Panel);
                dl.Box(PX(cx), PY(cy), cw * sx, ch * sy, St(3), DragonPalette.Hairline);
                dl.Text(FigmaUI.Name(Entries[i]), PX(cx + cw * 0.5f), PY(cy + ch * 0.5f - 18f),
                        SZ(32), TextAlign.Centre, DragonPalette.White);
            }

            dl.Asset("component_48", 0f, PY(1877), w, SZ(235), DragonPalette.White);
        }

        /// <summary>Which entry (an index into Entries) a touch hit, or -1.</summary>
        public static int HitTest(float px, float py, int w, int h)
        {
            float dx0 = px * RefW / w, dy0 = py * RefH / h;
            if (dx0 < Margin || dy0 < Top || dy0 > Bottom) return -1;

            for (int i = 0; i < Entries.Length; i++)
            {
                float cx, cy, cw, ch;
                CellRect(i, out cx, out cy, out cw, out ch);
                if (dx0 >= cx && dx0 < cx + cw && dy0 >= cy && dy0 < cy + ch) return i;
            }
            return -1;
        }
    }
}
