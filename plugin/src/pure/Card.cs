/*
 * DragonScreen - Card
 *
 * PURE. The rounded panel every subview page sits inside, and the tab strip in the notch at the
 * bottom of it.
 *
 * ---- THIS IS STRUCTURE, NOT DECORATION ----
 * `Third.vue` and `Fifth.vue` are 126 and 195 lines and contain almost NOTHING except this: a
 * `#white-border` card and a row of tab buttons. The actual content is a `<component :is="...">`
 * swapped inside it. Two of the reference's five pages are the shell; the subviews live in it.
 *
 * We had no card and no tabs, so VEHICLE could only ever show one of its two subviews and SETTINGS
 * had three columns crammed side by side where the reference has three tabs. Building this first is
 * what stops every subview after it landing in the wrong container.
 *
 * ---- QUOTED FROM Third.vue's STYLE BLOCK ----
 *      #back-box     background #020738, height 95vh, radius 0 0 30px 30px
 *      #white-border background #111b52, 98.5vw x 92vh, centred, radius 25px
 *                    clip-path: polygon(0 0, 0 100%, 42.5% 100%, 45% 95%,
 *                                       55% 95%, 57.5% 100%, 100% 100%, 100% 0)
 *      button        top 94%, height 6%, width 3.5%, at left 46.5% and 50.25%
 *      indicator     a white pill, height 0.75%, width 2.5%, sliding between those two
 *
 * #020738 and #111b52 are our Background and Panel exactly - a third independent confirmation of
 * those two hexes, on top of the SVG counts and the Vue literals.
 *
 * ---- THE NOTCH IS A RECTANGLE, AND THAT IS AN APPROXIMATION ----
 * The clip-path chamfers from 42.5% to 45% and back from 55% to 57.5%. We have no polygon primitive
 * and adding one for a 2.5%-wide bevel is not worth a shape in both renderers, so the notch is
 * square-sided. Stated rather than hidden: it is the one place this card knowingly differs.
 */
namespace DragonScreen
{
    public static class Card
    {
        /// <summary>Card size as a fraction of the page - 98.5vw x 92vh in the source.</summary>
        private const float WidthFrac = 0.985f, HeightFrac = 0.92f;

        /// <summary>Corner radius, 25 px in the source, scaled to our smaller panel.</summary>
        private const float Radius = 18f;

        /// <summary>
        /// The notch. The source is 42.5%..57.5% - 15% of the card - because it holds exactly TWO
        /// tabs. SETTINGS has four, and at 15% the labels ran into each other: "VIDEODISPLAY".
        ///
        /// So the notch keeps the source's 7.5% PER TAB and widens with the count, staying centred.
        /// Two tabs reproduce the reference exactly; more get the room they need instead of the
        /// layout quietly failing.
        /// </summary>
        private const float NotchPerTab = 0.075f, NotchDepth = 0.05f;

        private static void Notch(int count, out float left, out float right)
        {
            if (count < 2) count = 2;
            float wide = NotchPerTab * count;
            if (wide > 0.6f) wide = 0.6f;
            left = 0.5f - wide * 0.5f;
            right = 0.5f + wide * 0.5f;
        }

        /// <summary>Commands the card itself costs, before tabs.</summary>
        public const int Commands = 12;

        /// <summary>The card rectangle, centred in the page body above the chrome bar.</summary>
        public static void Rect(int w, int h, out float x, out float y, out float cw, out float ch)
        {
            float body = h - ChromeBar.Height;
            cw = w * WidthFrac;
            ch = body * HeightFrac;
            x = (w - cw) * 0.5f;
            y = (body - ch) * 0.5f;
        }

        /// <summary>
        /// The usable area INSIDE the card - what a subview may draw in.
        ///
        /// Inset by the corner radius so nothing lands in a rounded corner, and short of the notch at
        /// the bottom so no content hides behind the tabs.
        /// </summary>
        public static void Body(int w, int h, out float x, out float y, out float bw, out float bh)
        {
            float cx, cy, cw, ch;
            Rect(w, h, out cx, out cy, out cw, out ch);
            x = cx + Radius;
            y = cy + Radius * 0.5f;
            bw = cw - Radius * 2f;
            bh = ch - Radius * 0.5f - ch * NotchDepth - 6f;
        }

        /// <summary>
        /// Tab <paramref name="i"/> of <paramref name="count"/>, in the notch. ONE source for
        /// drawing and hit-testing, as everywhere else in this project.
        /// </summary>
        public static void TabRect(int i, int count, int w, int h,
                                   out float x, out float y, out float tw, out float th)
        {
            float cx, cy, cw, ch;
            Rect(w, h, out cx, out cy, out cw, out ch);

            // The notch is a fixed slice of the card; the tabs share it evenly. The source hard-codes
            // two positions because it has exactly two tabs - dividing the notch generalises that to
            // however many a page needs without moving the notch.
            float nl, nr;
            Notch(count, out nl, out nr);
            float nx = cx + cw * nl;
            float nw = cw * (nr - nl);
            th = ch * NotchDepth + 12f;
            y = cy + ch - ch * NotchDepth - 6f;

            if (count < 1) count = 1;
            tw = nw / count;
            x = nx + tw * i;
        }

        /// <summary>Which tab is at this point, or -1. Page coordinates.</summary>
        public static int HitTest(float px, float py, int count, int w, int h)
        {
            for (int i = 0; i < count; i++)
            {
                float x, y, tw, th;
                TabRect(i, count, w, h, out x, out y, out tw, out th);
                if (px >= x && px < x + tw && py >= y && py < y + th) return i;
            }
            return -1;
        }

        /// <summary>
        /// Draw the card and its tabs. Call FIRST - it is the background the subview draws on.
        /// </summary>
        public static void Build(DisplayList dl, int w, int h, string[] tabs, int active)
        {
            float cx, cy, cw, ch;
            Rect(w, h, out cx, out cy, out cw, out ch);

            // The page behind the card.
            dl.Rect(0f, 0f, w, h - ChromeBar.Height, DragonPalette.Background);

            // ---- THE CARD, WITH ROUNDED CORNERS ----
            // Three rects and four quarter-discs. There is no rounded-rect primitive and this is the
            // only place that wants one, so it is composed rather than added to DisplayList.
            dl.Rect(cx + Radius, cy, cw - Radius * 2f, ch, DragonPalette.Panel);
            dl.Rect(cx, cy + Radius, Radius, ch - Radius * 2f, DragonPalette.Panel);
            dl.Rect(cx + cw - Radius, cy + Radius, Radius, ch - Radius * 2f, DragonPalette.Panel);
            dl.ArcBand(cx + Radius, cy + Radius, 0f, Radius, -90.0, 0.0, DragonPalette.Panel);
            dl.ArcBand(cx + cw - Radius, cy + Radius, 0f, Radius, 0.0, 90.0, DragonPalette.Panel);
            dl.ArcBand(cx + cw - Radius, cy + ch - Radius, 0f, Radius, 90.0, 180.0,
                       DragonPalette.Panel);
            dl.ArcBand(cx + Radius, cy + ch - Radius, 0f, Radius, 180.0, 270.0, DragonPalette.Panel);

            // ---- THE NOTCH ----
            // Cut back out in the page colour. Square-sided; see the header.
            float nl, nr;
            Notch(tabs == null ? 2 : tabs.Length, out nl, out nr);
            float nx = cx + cw * nl;
            float nw = cw * (nr - nl);
            float nd = ch * NotchDepth;
            dl.Rect(nx, cy + ch - nd, nw, nd, DragonPalette.Background);

            if (tabs == null || tabs.Length == 0) return;


            // ---- TABS, IN THE NOTCH ----
            for (int i = 0; i < tabs.Length; i++)
            {
                float x, y, tw, th;
                TabRect(i, tabs.Length, w, h, out x, out y, out tw, out th);
                bool on = (i == active);

                dl.Text(tabs[i], x + tw * 0.5f, y + 2f, Typography.Caption, TextAlign.Centre,
                        on ? DragonPalette.Text0 : DragonPalette.Text6);

                // The sliding indicator: a white pill under the active tab, with a soft riser above
                // it. The source animates it between fixed positions; ours simply sits under
                // whichever tab is live, which is the same information without the tween.
                if (on)
                {
                    float pw = tw * 0.5f;
                    dl.Rect(x + (tw - pw) * 0.5f, y + th - 4f, pw, 3f, DragonPalette.Text0);
                    dl.Rect(x + (tw - pw * 0.6f) * 0.5f, y + th - 10f, pw * 0.6f, 6f,
                            DragonPalette.Hairline);
                }
            }
        }
    }
}
