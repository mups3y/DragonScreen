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

        // ---- EVERY NUMBER BELOW IS A PANEL PIXEL MEASURED AT Typography.RefPanelW (1280) ----
        // ⛔ AND THAT IS WHY NONE OF THEM MAY BE USED DIRECTLY. This is R-02's shape on the one
        // component that appears on EVERY legacy page, and it is [[S120]].
        //
        // THE DEFECT, FOR ANYONE WHO FINDS A ScaleFor() CALL BELOW AND WONDERS WHY: TopY used to be
        // `screenHeight - Height`, subtracting a DEVICE-PIXEL CONSTANT from a dimension that scales.
        // When S115 doubled the shipped panel 1280 -> 2560 the bar stayed 64 px on a panel that had
        // become 1406 high - 4.55% of the height where it had been measured as 9.1% - and its labels
        // stayed 16 device px on glass twice as wide. The bar HALVED physically, on every page, on
        // the day the cfg changed, and page2_nav_planet.png shows it: after S117 scaled NAV, the bar
        // underneath it is conspicuously smaller than the content above it.
        //
        // So each constant is kept AS THE MEASURED NUMBER - exactly as Typography.Min is - and every
        // use goes through Sc(panelW). Read them as "px at 1280".

        /// <summary>Bar height AT RefPanelW. ⛔ NOT the height on an arbitrary panel: use HeightFor(w).
        /// Kept as a named constant because it is the measured number and HeightFor derives from it,
        /// the same contract Typography.Min has with Typography.MinFor.</summary>
        public const float Height = 64f;

        public const int Commands = 40;

        private const float Pad = 24f;
        private const float Hairline = 2f;
        private const float SelectBar = 3f;
        private const float Pitch = 112f;

        /// <summary>The design scale for a panel panelW device px wide. 1.0 at 1280, 2.0 at 2560.</summary>
        private static float Sc(float panelW) { return Typography.ScaleFor(panelW); }

        /// <summary>The bar's height ON A PANEL panelW DEVICE PX WIDE - the honest form of Height, and
        /// the one every caller wants. 64 at 1280, 128 at 2560, the same fraction of the glass at
        /// both, which is what was actually designed.</summary>
        public static float HeightFor(float panelW) { return Height * Sc(panelW); }

        /// <summary>Top edge of the bar. ⛔ TAKES THE WIDTH TOO, and it did not always: the one-argument
        /// TopY(h) was removed rather than kept as an overload, deliberately, so that the compiler
        /// finds every caller instead of letting one keep the un-scaled behaviour silently. That is
        /// the whole failure mode R-02 is about - a stale call that still compiles.</summary>
        public static float TopY(int w, int h) { return h - HeightFor(w); }

        public static bool LinkRect(int i, int w, int h,
                                    out float x, out float y, out float rw, out float rh)
        {
            float sc = Sc(w);
            x = (Pad + Pitch * i) * sc;
            y = TopY(w, h);
            rw = Pitch * sc;
            rh = HeightFor(w);
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

            float sc = Sc(w);
            float top = TopY(w, h);

            dl.Rect(0f, top, w, HeightFor(w), DragonPalette.Panel);
            // ⛔ Strokes.Px, not Hairline * sc: a rule's thickness rounds UP to a whole device pixel
            // so it keeps its physical weight instead of smearing (the R-02 family's stroke half,
            // landed by job 3 of the 2026-09-06 batch). One rule for strokes, in Strokes.cs.
            dl.Rect(0f, top, w, Strokes.Px(Hairline, sc), DragonPalette.Hairline);

            // ---- page links, left ----
            float linkY = top + 22f * sc;
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

                dl.Text(PageNames[i], cx, linkY, Typography.Caption * sc, TextAlign.Centre, c);

                if (selected)
                    dl.Rect(lx + 12f * sc, top + HeightFor(w) - (SelectBar + 8f) * sc,
                            lw - 24f * sc, Strokes.Px(SelectBar, sc), c);
            }

            // ---- right-hand readouts ----
            // ⛔ These three columns are RIGHT-ALIGNED and their offsets from the right edge scale
            // too. Leaving 260/520 un-scaled would have kept the columns at 1280 spacing while the
            // type inside them doubled, so the STATE and LINK blocks would have collided at 2560 -
            // the same trap S117 records for NAV: scale the type and the boxes together, or neither.
            float capY = top + 12f * sc;
            float valY = top + 32f * sc;

            float metX = w - Pad * sc;
            dl.Text("MET", metX, capY, Typography.Caption * sc, TextAlign.Right, DragonPalette.Text6);
            dl.Text(s.Met ?? "-", metX, valY, Typography.Body * sc, TextAlign.Right, DragonPalette.Text0);

            float linkX = w - (Pad + 260f) * sc;
            Rgba linkColour = s.LinkUp ? DragonPalette.Text0 : DragonPalette.Alarm;
            dl.Text(s.LinkName ?? "LINK", linkX, capY, Typography.Caption * sc, TextAlign.Right,
                    s.LinkUp ? DragonPalette.Text6 : DragonPalette.Alarm);
            dl.Text(s.LinkTimer ?? "-", linkX, valY, Typography.Body * sc, TextAlign.Right, linkColour);

            float stateX = w - (Pad + 520f) * sc;
            dl.Text("STATE", stateX, capY, Typography.Caption * sc, TextAlign.Right, DragonPalette.Text6);
            dl.Text(s.VehicleState ?? "-", stateX, valY, Typography.Body * sc, TextAlign.Right,
                    DragonPalette.Text0);
        }
    }
}
