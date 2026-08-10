/*
 * DragonScreen - ChromeBar
 *
 * PURE. The global nav bar: the strip along the bottom of EVERY screen, on every page, in every
 * flight phase. Not a page - the thing pages are drawn inside.
 *
 * ---- THIS IS THE FIRST REAL UI ON PURPOSE ----
 * docs/REAL_DRAGON_SCREENS.md: the global nav bar and its alert routing are "the interface's spine",
 * and the research says build the chrome before any page. Four sections, taken from the most
 * detailed public reconstruction of the real displays:
 *
 *     page links  |  vehicle state  |  comms link + timer  |  MET
 *
 * ---- THE ALERT ROUTING IS THE POINT, NOT THE DECORATION ----
 * A page link turns ALARM-coloured when that page holds a non-nominal condition, so any screen can
 * say "the problem is over there" and be one touch from it. That behaviour is what makes the
 * interface flyable rather than merely accurate, and it is nearly free - which is why it is here in
 * the first version rather than deferred.
 *
 * ---- EVERY STRING ARRIVES PRE-BUILT ----
 * ChromeState carries strings, never numbers to format. "T+ " + t.ToString() inside a draw path
 * allocates every frame on three screens. The caller formats when the VALUE changes, not when the
 * frame changes. Same rule as DisplayList.Text, and this is the surface most likely to break it,
 * because a clock looks like something you format on the spot.
 */
namespace DragonScreen
{
    /// <summary>
    /// What the chrome needs to draw itself. Plain data, no engine types, no formatting.
    /// A struct so passing it costs nothing and nobody is tempted to cache a reference to it.
    /// </summary>
    public struct ChromeState
    {
        /// <summary>Mission elapsed time, PRE-FORMATTED, e.g. "T+ 00:12:34".</summary>
        public string Met;
        /// <summary>Vehicle summary, e.g. "NOMINAL" or "ABORT ARMED".</summary>
        public string VehicleState;
        /// <summary>Comms link name, e.g. "COM1/TLM".</summary>
        public string LinkName;
        /// <summary>Time on the current link, PRE-FORMATTED.</summary>
        public string LinkTimer;
        /// <summary>False draws the link section as an alarm - loss of signal is not a detail.</summary>
        public bool LinkUp;
        /// <summary>Index into PageNames of the page this screen is showing.</summary>
        public int SelectedPage;
        /// <summary>
        /// Bit i set = page i holds an alert. A BITMASK, not a bool[], because an array parameter
        /// would either allocate per frame or have to be owned and kept in step by every caller.
        /// </summary>
        public int AlertMask;
    }

    public static class ChromeBar
    {
        /// <summary>
        /// The page set. Order is the order they appear in the bar, so it is also the order of the
        /// bits in AlertMask. See docs/REAL_DRAGON_SCREENS.md for what each page holds.
        /// </summary>
        public static readonly string[] PageNames = { "FLIGHT", "VEHICLE", "NAV", "DOCKING", "SETTINGS" };

        /// <summary>Height of the bar in render-target pixels.</summary>
        public const float Height = 64f;

        /// <summary>Worst-case command count, for sizing the display list.</summary>
        public const int Commands = 40;

        private const float Pad = 24f;
        private const float Hairline = 2f;
        /// <summary>Marker under the selected page link. Thick enough to read at a glance.</summary>
        private const float SelectBar = 3f;
        /// <summary>
        /// Horizontal pitch of the page links.
        ///
        /// 112, not 128. At 128 the five links run to x=664 and the STATE readout is right-aligned at
        /// x=736 - which looked like a 72 px gap in the PNG preview and was a COLLISION in game, with
        /// "SETTINGS" and "NOMINAL" touching. GDI+ and Unity's dynamic-font atlas do not measure
        /// D-DIN identically, so the preview's margins are approximate and a 72 px gap between two
        /// pieces of text is not a margin at all. Confirmed off a capture PNG, not a screenshot.
        /// </summary>
        private const float Pitch = 112f;

        /// <summary>Y of the top of the bar for a screen of this height.</summary>
        public static float TopY(int screenHeight) { return screenHeight - Height; }

        /// <summary>
        /// The touch target for page link <paramref name="i"/>. ONE SOURCE FOR DRAWING AND HITTING.
        ///
        /// Both Build and HitTest go through here, and that is the whole point: a button whose
        /// clickable area is computed separately from where its label is drawn will drift the first
        /// time either is edited, and the symptom - "it works but you have to click slightly left of
        /// the word" - is maddening to diagnose and trivial to prevent.
        /// </summary>
        public static bool LinkRect(int i, int w, int h,
                                    out float x, out float y, out float rw, out float rh)
        {
            x = Pad + Pitch * i;
            y = TopY(h);
            rw = Pitch;
            rh = Height;
            return i >= 0 && i < PageNames.Length;
        }

        /// <summary>
        /// Which page link is at this page-space point, or -1 for none.
        /// Page coordinates, top-left origin - the same space DisplayList uses.
        /// </summary>
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

            // Surface, then the hairline that separates chrome from page. The hairline is what stops
            // the bar reading as part of whatever page is above it.
            dl.Rect(0f, top, w, Height, DragonPalette.Panel);
            dl.Rect(0f, top, w, Hairline, DragonPalette.Hairline);

            // ---- page links, left ----
            // Laid out on a fixed pitch rather than measured widths, because measuring a string needs
            // a font and src/pure has none (see DisplayList.Text). A fixed pitch is honest about that
            // and stable across both renderers; proportional packing waits for a metrics interface.
            // Positions come from LinkRect, which HitTest also uses - see there for why.
            float linkY = top + 22f;
            for (int i = 0; i < PageNames.Length; i++)
            {
                float lx, ly, lw, lh;
                LinkRect(i, w, h, out lx, out ly, out lw, out lh);
                float cx = lx + lw * 0.5f;
                bool selected = (i == s.SelectedPage);
                bool alert = ((s.AlertMask >> i) & 1) != 0;

                // ALERT BEATS SELECTED. A selected page that is also in alarm must read as ALARM -
                // the colour is carrying "something is wrong here", and losing that to a highlight
                // would defeat the one behaviour this bar exists for.
                Rgba c = alert ? DragonPalette.Alarm
                       : selected ? DragonPalette.Accent
                       : DragonPalette.Text5;

                dl.Text(PageNames[i], cx, linkY, Typography.Caption, TextAlign.Centre, c);

                if (selected)
                    dl.Rect(lx + 12f, top + Height - SelectBar - 8f, lw - 24f, SelectBar, c);
            }

            // ---- right-hand readouts ----
            // Anchored from the RIGHT EDGE inward, so they stay put on a screen 1% narrower than its
            // neighbour - which the centre display is. Anchoring these from the left would drift the
            // whole group by a few pixels between screens for no reason.
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

            // "STATE", not "VEHICLE". THE FIRST VERSION SAID VEHICLE AND THAT WAS A REAL BUG:
            // VEHICLE is also one of the PAGE LINKS, 500 px away on the same bar, meaning something
            // else entirely. A headless test caught it by picking up the wrong one - which is exactly
            // what a pilot glancing at the bar would do. STATE is also the more accurate caption for
            // the value underneath it, which reads NOMINAL / ABORT ARMED and is not a vehicle.
            float stateX = w - Pad - 520f;
            dl.Text("STATE", stateX, capY, Typography.Caption, TextAlign.Right, DragonPalette.Text6);
            dl.Text(s.VehicleState ?? "-", stateX, valY, Typography.Body, TextAlign.Right,
                    DragonPalette.Text0);
        }
    }
}
