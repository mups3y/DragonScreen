// DragonScreen - Card
// ---- THIS IS STRUCTURE, NOT DECORATION ----
// ---- QUOTED FROM Third.vue's STYLE BLOCK ----
// ---- THE NOTCH IS A RECTANGLE, AND THAT IS AN APPROXIMATION ----
namespace DragonScreen
{
    public static class Card
    {
        private const float WidthFrac = 0.985f, HeightFrac = 0.92f;

        private const float Radius = 18f;

        private const float NotchPerTab = 0.075f, NotchDepth = 0.05f;

        private static void Notch(int count, out float left, out float right)
        {
            if (count < 2) count = 2;
            float wide = NotchPerTab * count;
            if (wide > 0.6f) wide = 0.6f;
            left = 0.5f - wide * 0.5f;
            right = 0.5f + wide * 0.5f;
        }

        public const int Commands = 12;

        public static void Rect(int w, int h, out float x, out float y, out float cw, out float ch)
        {
            float body = h - ChromeBar.Height;
            cw = w * WidthFrac;
            ch = body * HeightFrac;
            x = (w - cw) * 0.5f;
            y = (body - ch) * 0.5f;
        }

        public static void Body(int w, int h, out float x, out float y, out float bw, out float bh)
        {
            float cx, cy, cw, ch;
            Rect(w, h, out cx, out cy, out cw, out ch);
            x = cx + Radius;
            y = cy + Radius * 0.5f;
            bw = cw - Radius * 2f;
            bh = ch - Radius * 0.5f - ch * NotchDepth - 6f;
        }

        public static void TabRect(int i, int count, int w, int h,
                                   out float x, out float y, out float tw, out float th)
        {
            float cx, cy, cw, ch;
            Rect(w, h, out cx, out cy, out cw, out ch);

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

        public static void Build(DisplayList dl, int w, int h, string[] tabs, int active)
        {
            float cx, cy, cw, ch;
            Rect(w, h, out cx, out cy, out cw, out ch);

            dl.Rect(0f, 0f, w, h - ChromeBar.Height, DragonPalette.Background);

            // ---- THE CARD, WITH ROUNDED CORNERS ----
            dl.Rect(cx + Radius, cy, cw - Radius * 2f, ch, DragonPalette.Panel);
            dl.Rect(cx, cy + Radius, Radius, ch - Radius * 2f, DragonPalette.Panel);
            dl.Rect(cx + cw - Radius, cy + Radius, Radius, ch - Radius * 2f, DragonPalette.Panel);
            dl.ArcBand(cx + Radius, cy + Radius, 0f, Radius, -90.0, 0.0, DragonPalette.Panel);
            dl.ArcBand(cx + cw - Radius, cy + Radius, 0f, Radius, 0.0, 90.0, DragonPalette.Panel);
            dl.ArcBand(cx + cw - Radius, cy + ch - Radius, 0f, Radius, 90.0, 180.0,
                       DragonPalette.Panel);
            dl.ArcBand(cx + Radius, cy + ch - Radius, 0f, Radius, 180.0, 270.0, DragonPalette.Panel);

            // ---- THE NOTCH ----
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
