// DragonScreen - ChromeBar
// ---- THIS IS THE FIRST REAL UI ON PURPOSE ----
// ---- THE ALERT ROUTING IS THE POINT, NOT THE DECORATION ----
// ---- EVERY STRING ARRIVES PRE-BUILT ----
namespace DragonScreen
{
    public struct ChromeState
    {
        public string Met;
        public string VehicleState;
        public string LinkName;
        public string LinkTimer;
        public bool LinkUp;
        public int SelectedPage;
        public int AlertMask;
    }

    public static class ChromeBar
    {
        public static readonly string[] PageNames = { "FLIGHT", "VEHICLE", "NAV", "DOCKING", "SETTINGS" };

        public const float Height = 64f;

        public const int Commands = 40;

        private const float Pad = 24f;
        private const float Hairline = 2f;
        private const float SelectBar = 3f;
        private const float Pitch = 112f;

        public static float TopY(int screenHeight) { return screenHeight - Height; }

        public static bool LinkRect(int i, int w, int h,
                                    out float x, out float y, out float rw, out float rh)
        {
            x = Pad + Pitch * i;
            y = TopY(h);
            rw = Pitch;
            rh = Height;
            return i >= 0 && i < PageNames.Length;
        }

        public static int HitTest(float px, float py, int w, int h)
        {
            for (int i = 0; i < PageNames.Length; i++)
            {
                float x, y, rw, rh;
                if (!LinkRect(i, w, h, out x, out y, out rw, out rh)) continue;
                if (px >= x && px < x + rw && py >= y && py < y + rh) return i;
            }
            return -1;
        }

        public static void Build(DisplayList dl, int w, int h, ChromeState s)
        {
            if (dl == null || w <= 0 || h <= 0) return;

            float top = TopY(h);

            dl.Rect(0f, top, w, Height, DragonPalette.Panel);
            dl.Rect(0f, top, w, Hairline, DragonPalette.Hairline);

            // ---- page links, left ----
            float linkY = top + 22f;
            for (int i = 0; i < PageNames.Length; i++)
            {
                float lx, ly, lw, lh;
                LinkRect(i, w, h, out lx, out ly, out lw, out lh);
                float cx = lx + lw * 0.5f;
                bool selected = (i == s.SelectedPage);
                bool alert = ((s.AlertMask >> i) & 1) != 0;

                Rgba c = alert ? DragonPalette.Alarm
                       : selected ? DragonPalette.Accent
                       : DragonPalette.Text5;

                dl.Text(PageNames[i], cx, linkY, Typography.Caption, TextAlign.Centre, c);

                if (selected)
                    dl.Rect(lx + 12f, top + Height - SelectBar - 8f, lw - 24f, SelectBar, c);
            }

            // ---- right-hand readouts ----
            float capY = top + 12f;
            float valY = top + 32f;

            float metX = w - Pad;
            dl.Text("MET", metX, capY, Typography.Caption, TextAlign.Right, DragonPalette.Text6);
            dl.Text(s.Met ?? "-", metX, valY, Typography.Body, TextAlign.Right, DragonPalette.Text0);

            float linkX = w - Pad - 260f;
            Rgba linkColour = s.LinkUp ? DragonPalette.Text0 : DragonPalette.Alarm;
            dl.Text(s.LinkName ?? "LINK", linkX, capY, Typography.Caption, TextAlign.Right,
                    s.LinkUp ? DragonPalette.Text6 : DragonPalette.Alarm);
            dl.Text(s.LinkTimer ?? "-", linkX, valY, Typography.Body, TextAlign.Right, linkColour);

            float stateX = w - Pad - 520f;
            dl.Text("STATE", stateX, capY, Typography.Caption, TextAlign.Right, DragonPalette.Text6);
            dl.Text(s.VehicleState ?? "-", stateX, valY, Typography.Body, TextAlign.Right,
                    DragonPalette.Text0);
        }
    }
}
