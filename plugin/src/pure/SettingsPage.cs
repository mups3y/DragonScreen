/*
 * DragonScreen - SettingsPage
 *
 * PURE. SETTINGS: a CARD WITH TABS, which is what `Fifth.vue` actually is.
 *
 *      Fifth.vue = Audio + Cabin + Video, inside #white-border, tabs in the notch
 *
 * Ours adds a fourth, DISPLAY, for the things that are ours rather than the reference's: screen
 * brightness, the capture button, and the grid that moves a page onto another display. Those have no
 * counterpart in the reference because the reference has one screen and we have three.
 *
 * ---- WHAT EACH SUBVIEW IS, FROM THE SOURCE ----
 *      Cabin.vue   CABIN SETTINGS / LIGHTING, four columns of zone buttons around a seat diagram.
 *                  Labels: Back, Left, Right, Up, Down, Front, Tip, Outside.
 *      Audio.vue   five slots at left 10/30/50/70/90% - Seat 1, Seat 2, Cabin, Seat 3, Seat 4 -
 *                  with roles PASSENGER / PILOT / GROUND, over a box of channel slots:
 *                  dB, AUX, MAIN, Vox, INTERCOM, ALERTS.
 *      Video.vue   a camera box with a left column: Front, Rear, Left, Right, and Resolution.
 *
 * ---- THE EIGHT LIGHTING ZONES ARE NOT BUILDABLE, AND THAT IS A FACT ABOUT THE VEHICLE ----
 * Checked in `TundraExploration/Parts/RodanV2/TE_CD2_POD.cfg`: the pod carries exactly ONE
 * ModuleColorChanger, on the Light action group. There is no Back light, no Tip light, no per-zone
 * anything to bind to. Drawing eight buttons where seven do nothing is the dead-control failure this
 * project refuses, so the panel shows the master toggle plus whatever light modules are ACTUALLY
 * found on the vessel, by name. If a variant or another mod adds more, they appear on their own.
 *
 * ---- NO VOLUME SLIDERS ---- (user's call, 2026-08-06)
 * Audio shows per-seat ROLE and occupancy, and the intercom/alert state. KSP has no cabin audio, so
 * a fader would be a control bound to nothing. Simulate a reading, never simulate a control.
 */
namespace DragonScreen
{
    public static class SettingsPage
    {
        /// <summary>Tab order, matching Fifth.vue's Audio | Cabin | Video, plus ours.</summary>
        public static readonly string[] Tabs = { "CABIN", "AUDIO", "VIDEO", "DISPLAY" };

        public const int Cabin = 0, Audio = 1, Video = 2, Display = 3;

        private const float BtnH = 36f;
        private const float PageBtnW = 100f, PageBtnGap = 6f;
        private const float RowPitch = 74f;

        /// <summary>Brightness runs 3..10 in tenths. Below 0.3 the page is unreadable, not dim.</summary>
        public const int MinBright = 3, MaxBright = 10;

        /// <summary>Seats across the CABIN and AUDIO panels.</summary>
        private const int Seats = 4;
        private const float SeatW = 78f, SeatH = 99f, SeatGap = 12f;

        /// <summary>Height of AUDIO's extra per-seat ROLE line, which CABIN does not draw.</summary>
        private const float RoleLine = 22f;

        // ---------------------------------------------------------------- geometry

        private static void Body(int w, int h, out float x, out float y, out float bw, out float bh)
        {
            Card.Body(w, h, out x, out y, out bw, out bh);
        }

        public static void LightsRect(int w, int h, out float x, out float y,
                                      out float rw, out float rh)
        {
            float bx, by, bw, bh;
            Body(w, h, out bx, out by, out bw, out bh);
            x = bx; y = by + 42f; rw = 170f; rh = BtnH;
        }

        public static void SeatRect(int i, int w, int h, out float x, out float y,
                                    out float rw, out float rh)
        {
            float bx, by, bw, bh;
            Body(w, h, out bx, out by, out bw, out bh);
            rw = SeatW; rh = SeatH + 20f;
            x = bx + i * (SeatW + SeatGap);
            y = by + 42f + BtnH + 26f;
        }

        public static void BrightRect(bool up, int w, int h, out float x, out float y,
                                      out float rw, out float rh)
        {
            float bx, by, bw, bh;
            Body(w, h, out bx, out by, out bw, out bh);
            rw = 56f; rh = BtnH;
            y = by + 42f;
            x = up ? (bx + 240f) : bx;
        }

        public static void CaptureRect(int w, int h, out float x, out float y,
                                       out float rw, out float rh)
        {
            float bx, by, bw, bh;
            Body(w, h, out bx, out by, out bw, out bh);
            x = bx; y = by + 42f + BtnH + 26f; rw = 296f; rh = BtnH;
        }

        public static void PageRect(int screen, int page, int w, int h,
                                    out float x, out float y, out float rw, out float rh)
        {
            float bx, by, bw, bh;
            Body(w, h, out bx, out by, out bw, out bh);
            rw = PageBtnW; rh = BtnH;
            x = bx + bw - (ChromeBar.PageNames.Length * (PageBtnW + PageBtnGap))
                + page * (PageBtnW + PageBtnGap);
            // 76, not 42. Each row draws its name at (y - 22); at 42 that landed on by+20, in the
            // same band as this column's own header at by+26 AND the page title at by+4. Three
            // strings in one 22 px band, all illegible. Reported from a screenshot, 2026-08-06.
            y = by + 76f + (screen - 1) * RowPitch;
        }

        /// <summary>Camera direction button on the VIDEO tab. 0 Front, 1 Rear, 2 Left, 3 Right.</summary>
        public static void CamRect(int i, int w, int h, out float x, out float y,
                                   out float rw, out float rh)
        {
            float bx, by, bw, bh;
            Body(w, h, out bx, out by, out bw, out bh);
            rw = bw * 0.15f; rh = BtnH;          // .column left 0%, width 15%
            x = bx;
            y = by + bh * 0.1225f + i * (BtnH + 8f);
        }

        /// <summary>
        /// The four hull-swept directions, which every vehicle has because they are derived from its
        /// control point rather than from any part. Real cameras are appended after these.
        /// </summary>
        public static readonly string[] CamNames = { "FRONT", "REAR", "LEFT", "RIGHT" };

        /// <summary>
        /// How many camera buttons this screen has room for.
        ///
        /// ⛔ THE COLUMN IS NOT INFINITE AND THE CAMERAS ARE NOT COUNTED BY US. Four fitted on
        /// every screen size by inspection, so nobody had to ask; a craft carrying six hull cameras
        /// on top of those would run the column off the bottom of the body and out through the tab
        /// bar - which is exactly the failure the layout sweep exists to catch, and exactly the
        /// failure a photograph of the cockpit showed on 2026-08-11 with AUTO DOCK. So the count is
        /// derived from the space, and anything that does not fit is not drawn.
        /// </summary>
        public static int CamSlots(int w, int h)
        {
            float bx, by, bw, bh;
            Body(w, h, out bx, out by, out bw, out bh);
            float top = bh * 0.1225f;
            float room = bh - top;
            int n = (int)((room + 8f) / (BtnH + 8f));
            if (n < 1) n = 1;
            return n;
        }

        /// <summary>
        /// The views actually on offer: the four directions plus whatever real cameras were found,
        /// clipped to what fits. Never null.
        /// </summary>
        public static string[] CamList(PageState s, int w, int h)
        {
            string[] extra = s.CamLabels;
            int have = CamNames.Length + ((extra != null) ? extra.Length : 0);
            int slots = CamSlots(w, h);
            int n = (have < slots) ? have : slots;

            string[] a = new string[n];
            for (int i = 0; i < n; i++)
                a[i] = (i < CamNames.Length) ? CamNames[i] : extra[i - CamNames.Length];
            return a;
        }

        // ---------------------------------------------------------------- input

        /// <param name="extraCams">
        /// Real hull cameras found on the vehicle, beyond the four hull-swept directions. An int
        /// rather than a PageState so the hit path stays free of display state - the painter is the
        /// only thing that knows the count, and it is the only caller that has to supply it.
        /// Defaults to none, which is what every existing caller and every layout test means.
        /// </param>
        public static PageHit HitTest(float px, float py, int w, int h, int tab, int extraCams = 0)
        {
            float x, y, rw, rh;

            if (tab == Cabin)
            {
                LightsRect(w, h, out x, out y, out rw, out rh);
                if (Control.Hit(px, py, x, y, rw, rh)) return PageHit.Of(PageAct.ToggleLights);
            }

            if (tab == Cabin || tab == Audio)
            {
                for (int i = 0; i < Seats; i++)
                {
                    SeatRect(i, w, h, out x, out y, out rw, out rh);
                    if (Control.Hit(px, py, x, y, rw, rh))
                        return PageHit.Of(PageAct.ViewFromSeat, i);
                }
            }

            if (tab == Video)
            {
                // ⚠ THE SAME LIST THE PAINTER DREW, not CamNames. A hit test over a longer list
                // than was painted is a button bound to nothing wearing the shape of one that works.
                int have = CamNames.Length + extraCams;
                int slots = CamSlots(w, h);
                int n = (have < slots) ? have : slots;
                for (int i = 0; i < n; i++)
                {
                    CamRect(i, w, h, out x, out y, out rw, out rh);
                    if (Control.Hit(px, py, x, y, rw, rh))
                        return PageHit.Of(PageAct.SetCamera, i);
                }
            }

            if (tab == Display)
            {
                BrightRect(false, w, h, out x, out y, out rw, out rh);
                if (Control.Hit(px, py, x, y, rw, rh)) return PageHit.Of(PageAct.BrightDown);
                BrightRect(true, w, h, out x, out y, out rw, out rh);
                if (Control.Hit(px, py, x, y, rw, rh)) return PageHit.Of(PageAct.BrightUp);

                CaptureRect(w, h, out x, out y, out rw, out rh);
                if (Control.Hit(px, py, x, y, rw, rh)) return PageHit.Of(PageAct.Capture);

                for (int screen = 1; screen <= 3; screen++)
                    for (int page = 0; page < ChromeBar.PageNames.Length; page++)
                    {
                        PageRect(screen, page, w, h, out x, out y, out rw, out rh);
                        if (Control.Hit(px, py, x, y, rw, rh))
                            return PageHit.Of(PageAct.SetScreenPage,
                                              PageHit.PackScreenPage(screen, page));
                    }
            }

            return PageHit.None;
        }

        // ---------------------------------------------------------------- drawing

        public static void Build(DisplayList dl, int w, int h, PageState s, int thisScreen, int tab)
        {
            float bx, by, bw, bh;
            Body(w, h, out bx, out by, out bw, out bh);

            string title = (tab == Cabin) ? "CABIN SETTINGS"
                         : (tab == Audio) ? "AUDIO SETTINGS"
                         : (tab == Video) ? "VIDEO SETTINGS" : "DISPLAY SETTINGS";
            dl.Text(title, bx + bw * 0.5f, by + 4f, Typography.Body, TextAlign.Centre,
                    DragonPalette.Text1);

            if (tab == Cabin) CabinTab(dl, w, h, s, bx, by, bw, bh);
            else if (tab == Audio) AudioTab(dl, w, h, s, bx, by, bw, bh);
            else if (tab == Video) VideoTab(dl, w, h, s, bx, by, bw, bh);
            else DisplayTab(dl, w, h, s, thisScreen, bx, by, bw, bh);
        }

        private static void CabinTab(DisplayList dl, int w, int h, PageState s,
                                     float bx, float by, float bw, float bh)
        {
            dl.Text("LIGHTING", bx, by + 26f, Typography.Caption, TextAlign.Left,
                    DragonPalette.Text5);

            float x, y, rw, rh;
            LightsRect(w, h, out x, out y, out rw, out rh);
            Control.Button(dl, x, y, rw, rh, s.LightsOn ? "LIGHTS ON" : "LIGHTS OFF",
                           s.LightsOn, s.Valid);

            // ---- WHY THERE IS ONE BUTTON AND NOT EIGHT ----
            // Cabin.vue names eight zones. TE_CD2_POD.cfg carries one ModuleColorChanger on the Light
            // group and nothing else, so seven of those buttons would do nothing at all. The count of
            // lights actually found is printed instead - it is the honest version of the same
            // information, and it grows by itself if the vehicle ever gains more.
            dl.Text(s.LightCount > 1 ? (s.LightCount + " LIGHT GROUPS") : "SINGLE CABIN LIGHT GROUP",
                    bx + rw + 16f, y + 10f, Typography.Dense, TextAlign.Left, DragonPalette.Text7);

            Seats4(dl, w, h, s, "CREW - TOUCH A SEAT TO LOOK FROM IT", bx, by);

            float ry = by + 42f + BtnH + 26f + SeatH + 54f;
            Row(dl, bx, ry, 360f, "CABIN TEMP", s.Valid ? s.CabinTempText : "-", "deg C");
            Row(dl, bx, ry + 34f, 360f, "CABIN PRESSURE", s.Valid ? s.PressText : "-", "psia");
            Row(dl, bx, ry + 68f, 360f, "PPO2", s.Valid ? s.Ppo2Text : "-", "psia");
            Row(dl, bx, ry + 102f, 360f, "CO2", s.Valid ? s.Co2Text : "-", "mmHg");
            Row(dl, bx, ry + 136f, 360f, "LOOP A", s.Valid ? s.LoopAText : "-", "deg C");
            Row(dl, bx, ry + 170f, 360f, "LOOP B", s.Valid ? s.LoopBText : "-", "deg C");
        }

        /// <summary>
        /// Audio.vue's five slots - Seat 1, Seat 2, Cabin, Seat 3, Seat 4 - with the roles it names.
        /// State only: KSP has no cabin audio, so there is nothing for a fader to move.
        /// </summary>
        private static void AudioTab(DisplayList dl, int w, int h, PageState s,
                                     float bx, float by, float bw, float bh)
        {
            Seats4(dl, w, h, s, "STATIONS", bx, by);

            // The roles Audio.vue assigns, in its own order.
            string[] roles = { "PILOT", "PASSENGER", "PASSENGER", "GROUND" };
            for (int i = 0; i < Seats; i++)
            {
                float x, y, rw, rh;
                SeatRect(i, w, h, out x, out y, out rw, out rh);
                dl.Text(roles[i], x + rw * 0.5f, y + SeatH + 18f, Typography.Dense,
                        TextAlign.Centre, DragonPalette.Text6);
            }

            // ---- AUDIO'S BLOCK IS ONE LINE TALLER THAN CABIN'S ----
            // Seats4 draws the crew name at SeatH + 2, and the role line above sits at SeatH + 18.
            // CABIN has only the name, so it can start its rows at SeatH + 54 and the CHANNELS-style
            // caption 26 above that clears everything. AUDIO reused the same figure and the caption
            // landed at SeatH + 28, straight through the role line - visible in the first flight.
            // The extra line has to be paid for; the panel below is empty, so it costs nothing.
            float ry = by + 42f + BtnH + 26f + SeatH + 54f + RoleLine;
            dl.Text("CHANNELS", bx, ry - 26f, Typography.Caption, TextAlign.Left,
                    DragonPalette.Text5);
            // INTERCOM is crew aboard; ALERTS mirrors the one alarm channel the whole UI uses, so it
            // cannot disagree with the chrome bar. Both are real. dB, AUX, MAIN and Vox are not, and
            // are not drawn.
            Severity sev = s.Valid ? Alarms.VehicleSeverity(s) : Severity.Nominal;
            Row(dl, bx, ry, 360f, "INTERCOM", s.Valid ? s.CrewText : "-", "crew");
            dl.Text("ALERTS", bx, ry + 34f, Typography.Caption, TextAlign.Left, DragonPalette.Text6);
            dl.Text(s.Valid ? Alarms.Word(sev) : "-", bx + 360f, ry + 34f, Typography.Body,
                    TextAlign.Right, s.Valid ? Alarms.Colour(sev) : DragonPalette.Text7);
            dl.Rect(bx, ry + 62f, 360f, 1f, DragonPalette.Inset2);

            dl.Text("no cabin audio in stock KSP - state only, no faders",
                    bx, ry + 76f, Typography.Dense, TextAlign.Left, DragonPalette.Text8);
        }

        /// <summary>
        /// Video.vue's camera box plus its left column of directions. The picture is the SAME live
        /// camera the docking page uses, pointed a different way - never their stills.
        /// </summary>
        private static void VideoTab(DisplayList dl, int w, int h, PageState s,
                                     float bx, float by, float bw, float bh)
        {
            float x, y, rw, rh;
            string[] cams = CamList(s, w, h);
            for (int i = 0; i < cams.Length; i++)
            {
                CamRect(i, w, h, out x, out y, out rw, out rh);
                Control.Button(dl, x, y, rw, rh, cams[i], s.CameraView == i, true);
            }
            dl.Text("CAMERA", bx, by + bh * 0.1225f - 22f, Typography.Caption, TextAlign.Left,
                    DragonPalette.Text5);

            // The view itself, in the box. 979 x 487.5 in the source - a 2:1 letterbox.
            float vx = bx + bw * 0.18f;
            float vw = bw * 0.80f;
            float vh = vw * 0.5f;
            float vy = by + (bh - vh) * 0.45f;
            dl.Rect(vx, vy, vw, vh, DragonPalette.Inset2);
            dl.Image(ImageId.DockingCamLive, vx, vy, vw, vh, DragonPalette.White);
            dl.Box(vx, vy, vw, vh, 2f, DragonPalette.Hairline);

            dl.Text("RESOLUTION", vx, vy + vh + 10f, Typography.Caption, TextAlign.Left,
                    DragonPalette.Text6);
            dl.Text(s.CameraResText ?? "-", vx + vw, vy + vh + 10f, Typography.Caption,
                    TextAlign.Right, DragonPalette.Text0);

            // ONE CAMERA, AND DOCKING OUTRANKS THIS PAGE. Rendering a second full scene camera to let
            // two pages look different ways at once is real cost for a rare case; saying so is
            // better than quietly showing the wrong direction.
            if (s.CameraHeldByDocking)
                dl.Text("FORWARD VIEW IN USE BY DOCKING", vx + vw * 0.5f, vy + vh * 0.5f,
                        Typography.Caption, TextAlign.Centre, DragonPalette.Caution);
        }

        private static void DisplayTab(DisplayList dl, int w, int h, PageState s, int thisScreen,
                                       float bx, float by, float bw, float bh)
        {
            float x, y, rw, rh;
            BrightRect(false, w, h, out x, out y, out rw, out rh);
            Control.Button(dl, x, y, rw, rh, "-", false, true);
            BrightRect(true, w, h, out x, out y, out rw, out rh);
            Control.Button(dl, x, y, rw, rh, "+", false, true);
            dl.Text("BRIGHTNESS", bx, by + 26f, Typography.Caption, TextAlign.Left,
                    DragonPalette.Text5);
            dl.Text(s.Brightness * 10 + "%", bx + 148f, y + 10f, Typography.Body,
                    TextAlign.Centre, DragonPalette.Text0);

            CaptureRect(w, h, out x, out y, out rw, out rh);
            Control.Button(dl, x, y, rw, rh, "CAPTURE SCREEN", false, true);

            dl.Text("THIS DISPLAY", bx, y + BtnH + 22f, Typography.Caption, TextAlign.Left,
                    DragonPalette.Text6);
            dl.Text("SCREEN " + thisScreen, bx + 296f, y + BtnH + 22f, Typography.Body,
                    TextAlign.Right, DragonPalette.Accent);
            dl.Text("RESOLUTION", bx, y + BtnH + 54f, Typography.Caption, TextAlign.Left,
                    DragonPalette.Text6);
            dl.Text(w + " x " + h, bx + 296f, y + BtnH + 54f, Typography.Body, TextAlign.Right,
                    DragonPalette.Text0);

            dl.Text("PAGE ON EACH DISPLAY", bx + bw - 530f, by + 34f, Typography.Caption,
                    TextAlign.Left, DragonPalette.Text5);
            for (int screen = 1; screen <= 3; screen++)
            {
                float lx, ly, lw, lh;
                PageRect(screen, 0, w, h, out lx, out ly, out lw, out lh);
                string name = (screen == 1) ? "SCREEN 1  LEFT"
                            : (screen == 2) ? "SCREEN 2  CENTRE" : "SCREEN 3  RIGHT";
                dl.Text(name, lx, ly - 22f, Typography.Caption, TextAlign.Left,
                        (screen == thisScreen) ? DragonPalette.Accent : DragonPalette.Text6);

                int current = -1;
                if (s.ScreenPages != null && screen < s.ScreenPages.Length)
                    current = s.ScreenPages[screen];

                for (int page = 0; page < ChromeBar.PageNames.Length; page++)
                {
                    PageRect(screen, page, w, h, out x, out y, out rw, out rh);
                    Control.Button(dl, x, y, rw, rh, ChromeBar.PageNames[page],
                                   page == current, true);
                }
            }
        }

        private static void Seats4(DisplayList dl, int w, int h, PageState s, string caption,
                                   float bx, float by)
        {
            dl.Text(caption, bx, by + 42f + BtnH + 8f, Typography.Caption, TextAlign.Left,
                    DragonPalette.Text6);
            for (int i = 0; i < Seats; i++)
            {
                float sx, sy, sw, sh;
                SeatRect(i, w, h, out sx, out sy, out sw, out sh);

                bool occupied = (s.SeatNames != null && i < s.SeatNames.Length
                                 && !string.IsNullOrEmpty(s.SeatNames[i]));
                bool exists = (i < s.SeatCount);

                float ix, iy, iw, ih;
                if (Images.FitHeight(ImageId.Seat, sx + sw * 0.5f, sy + SeatH * 0.5f, SeatH,
                                     out ix, out iy, out iw, out ih))
                {
                    Rgba tint = !exists ? DragonPalette.Text8
                              : occupied ? DragonPalette.White : DragonPalette.Text7;
                    dl.Image(ImageId.Seat, ix, iy, iw, ih, tint);
                }

                string label = !exists ? "-" : occupied ? s.SeatNames[i] : "EMPTY";
                dl.Text(label, sx + sw * 0.5f, sy + SeatH + 2f, Typography.Dense, TextAlign.Centre,
                        occupied ? DragonPalette.Text1 : DragonPalette.Text7);
            }
        }

        private static void Row(DisplayList dl, float x, float y, float w,
                                string caption, string value, string unit)
        {
            Readouts.Row(dl, x, y, w, caption, value, unit, Typography.Body);
            dl.Rect(x, y + 26f, w, 1f, DragonPalette.Inset2);
        }
    }
}
