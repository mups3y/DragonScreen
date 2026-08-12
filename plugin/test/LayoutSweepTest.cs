/*
 * DragonScreen headless tests - EVERY control, on EVERY page, at EVERY screen size.
 *
 * ---- ⛔ WHY THIS EXISTS, AND WHY IT IS A SWEEP RATHER THAN A LIST OF CASES ----
 * A control has now been placed on top of, or underneath, something else THREE times:
 *
 *   1. AUTO SEQUENCE, first placed at the bottom of the sidebar directly over DRAGON SEPARATION and
 *      NOSE CONE OPEN. `Pages.AutoRect`'s own comment records it.
 *   2. The step list, which ran off the bottom of the page at a 30 px row pitch until it was cut to
 *      18. `Pages.StepRect`'s comment records that one.
 *   3. The three MISSION buttons, 2026-08-11: stacked downward from AUTO SEQUENCE they landed at
 *      623-657 and 665-699 on a 703-high screen whose tab bar starts at 639 - so AUTO-DOCK was half
 *      covered and UNDOCK & LAND was completely invisible. The crew photographed it.
 *
 * The third one had a test. The test asserted `y + height <= h` - against the PAGE bottom, not
 * against the tab bar - so it passed while the buttons sat under the chrome. Writing a per-control
 * assertion by hand is exactly how that happened, so this does not do that: it enumerates every rect
 * the pages expose and applies the same three rules to all of them.
 *
 * ---- THE THREE RULES ----
 *   INSIDE   a control must lie within the page and clear of the chrome bar (the bar's own links are
 *            the sole exception - they ARE the bar)
 *   APART    no two controls on one page may overlap
 *   REACHES  a press at a control's centre and at each of its corners must hit-test back to that
 *            control's own action, not to a neighbour and not to nothing
 *
 * ⚠ ADD NEW CONTROLS TO `Sweep` BELOW WHEN YOU ADD THEM TO A PAGE. A control this file does not know
 * about is a control with no coverage, which is precisely the state the mission buttons were in.
 */
using System;
using System.Collections.Generic;
using DragonScreen;

public static class LayoutSweepTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    private struct Ctl
    {
        public string Name;
        public float X, Y, W, H;
        public PageAct Act;
        public int Arg;
        /// <summary>Chrome-bar links live IN the bar; everything else must clear it.</summary>
        public bool InChrome;
        /// <summary>Some controls are drawn but hit-tested elsewhere (tabs) - skip the round trip.</summary>
        public bool NoRoundTrip;
        /// <summary>
        /// Vertical inset for the corner probes.
        ///
        /// ⚠ NOT slack. A step row's tappable band is deliberately SHORTER than the row it draws
        /// (`FlightHitTest` uses `y-3 .. y+rh-4`) so that adjacent rows on an 18 px pitch cannot
        /// steal each other's presses. Probing its drawn corners tests a rule the page does not
        /// make. Everything else is probed a pixel in from its true edge.
        /// </summary>
        public float InsetY;
        /// <summary>Smallest height this control is allowed to shrink to.</summary>
        public float MinH;
    }

    /// <summary>
    /// More hull cameras than any real craft carries, so what is being tested is the clip in
    /// CamSlots rather than a particular camera count. Both the control list and the hit test use
    /// this, because the bug it caught was the two of them disagreeing.
    /// </summary>
    static int SweepExtraCams(int w, int h)
    {
        int extra = SettingsPage.CamSlots(w, h) - SettingsPage.CamNames.Length;
        return (extra > 0) ? extra : 0;
    }

    static Ctl C(string n, float x, float y, float w, float h, PageAct a, int arg)
    {
        Ctl c = new Ctl();
        c.Name = n; c.X = x; c.Y = y; c.W = w; c.H = h; c.Act = a; c.Arg = arg;
        c.InsetY = 1f;
        c.MinH = 16f;
        return c;
    }

    /// <summary>Every control a page exposes, in page coordinates.</summary>
    static List<Ctl> Sweep(int page, int w, int h, int subview)
    {
        List<Ctl> list = new List<Ctl>();
        float x, y, rw, rh;

        if (page == 0)
        {
            Pages.AutoRect(w, h, out x, out y, out rw, out rh);
            list.Add(C("AUTO SEQUENCE", x, y, rw, rh, PageAct.ToggleAuto, 0));

            PageAct[] mission = { PageAct.Rendezvous, PageAct.AutoDock, PageAct.UndockAndLand };
            for (int i = 0; i < Pages.MissionButtons; i++)
            {
                Pages.MissionRect(i, w, h, out x, out y, out rw, out rh);
                list.Add(C("MISSION[" + i + "]", x, y, rw, rh, mission[i], 0));
            }

            // Only the rows the page actually draws - see Pages.StepVisible. A row outside the
            // window is not clipped, it is not there.
            int visible = Pages.StepVisible(h);
            for (int i = 0; i < visible; i++)
            {
                Pages.StepRect(i, w, h, out x, out y, out rw, out rh);
                // ⚠ The step rows are drawn on an 18 px pitch with a taller tappable box, so they
                // deliberately abut. Overlap is checked; the round trip is what matters here.
                Ctl c = C("STEP[" + i + "]", x, y, rw, rh, PageAct.AckStep,
                          Pages.StepIdAt(i, h));
                c.InsetY = 5f;                 // see Ctl.InsetY - the tappable band is inset
                // A step row may shrink to the pitch floor on a short screen; a BUTTON may not.
                c.MinH = Pages.StepPitchMin;
                list.Add(c);
            }
        }
        else if (page == 2)
        {
            NavPage.NextViewRect(w, h, out x, out y, out rw, out rh);
            list.Add(C("NAV NEXT VIEW", x, y, rw, rh, PageAct.NavNextView, 0));
            NavPage.ZoomRect(w, h, true, out x, out y, out rw, out rh);
            list.Add(C("NAV ZOOM IN", x, y, rw, rh, PageAct.NavZoomIn, 0));
            NavPage.ZoomRect(w, h, false, out x, out y, out rw, out rh);
            list.Add(C("NAV ZOOM OUT", x, y, rw, rh, PageAct.NavZoomOut, 0));
            NavPage.PadRect(w, h, 0, 0, out x, out y, out rw, out rh);
            list.Add(C("NAV CENTRE", x, y, rw, rh, PageAct.NavCentre, 0));
            NavPage.PadRect(w, h, 0, -1, out x, out y, out rw, out rh);
            list.Add(C("NAV UP", x, y, rw, rh, PageAct.NavPanUp, 0));
            NavPage.PadRect(w, h, 0, 1, out x, out y, out rw, out rh);
            list.Add(C("NAV DOWN", x, y, rw, rh, PageAct.NavPanDown, 0));
            NavPage.PadRect(w, h, -1, 0, out x, out y, out rw, out rh);
            list.Add(C("NAV LEFT", x, y, rw, rh, PageAct.NavPanLeft, 0));
            NavPage.PadRect(w, h, 1, 0, out x, out y, out rw, out rh);
            list.Add(C("NAV RIGHT", x, y, rw, rh, PageAct.NavPanRight, 0));

            NavPage.MapRect(w, h, out x, out y, out rw, out rh);
            Ctl m = C("NAV MAP", x, y, rw, rh, PageAct.None, 0);
            m.NoRoundTrip = true;              // the map is a display, not a control
            list.Add(m);
        }
        else if (page == 4)
        {
            if (subview == SettingsPage.Cabin)
            {
                SettingsPage.LightsRect(w, h, out x, out y, out rw, out rh);
                list.Add(C("LIGHTS", x, y, rw, rh, PageAct.ToggleLights, 0));
            }
            if (subview == SettingsPage.Cabin || subview == SettingsPage.Audio)
                for (int i = 0; i < 4; i++)
                {
                    SettingsPage.SeatRect(i, w, h, out x, out y, out rw, out rh);
                    list.Add(C("SEAT[" + i + "]", x, y, rw, rh, PageAct.ViewFromSeat, i));
                }
            if (subview == SettingsPage.Video)
            {
                // ⚠ SWEEP THE FULL COLUMN, NOT THE FOUR IT USED TO HAVE. The list grows with
                // whatever real cameras are bolted to the craft, so the sweep asks for MORE than any
                // vehicle could carry and checks that CamSlots clips it to what fits. A sweep that
                // only ever saw four buttons would pass while a six-camera craft ran its column out
                // through the tab bar.
                int slots = SettingsPage.CamSlots(w, h);
                for (int i = 0; i < slots; i++)
                {
                    SettingsPage.CamRect(i, w, h, out x, out y, out rw, out rh);
                    list.Add(C("CAM[" + i + "]", x, y, rw, rh, PageAct.SetCamera, i));
                }
            }
            if (subview == SettingsPage.Display)
            {
                SettingsPage.BrightRect(false, w, h, out x, out y, out rw, out rh);
                list.Add(C("BRIGHT DOWN", x, y, rw, rh, PageAct.BrightDown, 0));
                SettingsPage.BrightRect(true, w, h, out x, out y, out rw, out rh);
                list.Add(C("BRIGHT UP", x, y, rw, rh, PageAct.BrightUp, 0));
                SettingsPage.CaptureRect(w, h, out x, out y, out rw, out rh);
                list.Add(C("CAPTURE", x, y, rw, rh, PageAct.Capture, 0));
                for (int screen = 1; screen <= 3; screen++)
                    for (int pg = 0; pg < ChromeBar.PageNames.Length; pg++)
                    {
                        SettingsPage.PageRect(screen, pg, w, h, out x, out y, out rw, out rh);
                        list.Add(C("PAGE[" + screen + "," + pg + "]", x, y, rw, rh,
                                   PageAct.SetScreenPage, PageHit.PackScreenPage(screen, pg)));
                    }
            }
        }
        return list;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen layout sweep");

        // The first two are the screens the Dragon actually has (measured from the prop mesh in
        // the 2026-08-11 log). The other two are deliberately awkward: they are here to keep the
        // layout HONEST about deriving from the screen rather than assuming one, which is what
        // hard-coding 18 px of step pitch and 703 px of height stopped doing.
        int[] ws = { 1280, 1280, 1280, 1024 };
        int[] hs = { 703, 710, 600, 768 };

        for (int si = 0; si < ws.Length; si++)
        {
            int w = ws[si], h = hs[si];
            string at = " @" + w + "x" + h;
            float barTop = ChromeBar.TopY(h);

            // The chrome bar's own links, on every page.
            for (int i = 0; i < ChromeBar.PageNames.Length; i++)
            {
                float lx, ly, lw, lh;
                ChromeBar.LinkRect(i, w, h, out lx, out ly, out lw, out lh);
                Check("chrome link " + ChromeBar.PageNames[i] + " is on screen" + at,
                      lx >= 0f && lx + lw <= w && ly >= barTop && ly + lh <= h,
                      lx + "," + ly);
                Check("chrome link " + ChromeBar.PageNames[i] + " round-trips" + at,
                      ChromeBar.HitTest(lx + lw * 0.5f, ly + lh * 0.5f, w, h) == i, "");
            }

            for (int page = 0; page < ChromeBar.PageNames.Length; page++)
            {
                string[] tabs = Pages.Tabs(page);
                int subviews = (tabs == null) ? 1 : tabs.Length;

                for (int sub = 0; sub < subviews; sub++)
                {
                    List<Ctl> all = Sweep(page, w, h, sub);
                    string tag = "page " + page + (subviews > 1 ? " tab " + sub : "") + at;

                    for (int i = 0; i < all.Count; i++)
                    {
                        Ctl c = all[i];

                        // RULE 1 - INSIDE. This is the one the mission buttons broke: the bound is
                        // the CHROME BAR, not the page.
                        Check(c.Name + " clears the tab bar, " + tag,
                              c.X >= 0f && c.Y >= 0f && c.X + c.W <= w
                              && (c.InChrome ? c.Y + c.H <= h : c.Y + c.H <= barTop),
                              "bottom " + (c.Y + c.H).ToString("F0") + " vs barTop "
                              + barTop.ToString("F0"));

                        Check(c.Name + " has a usable size, " + tag,
                              c.W >= 20f && c.H >= c.MinH,
                              c.W.ToString("F0") + "x" + c.H.ToString("F0"));

                        // RULE 3 - REACHES. Centre and all four corners, inset so a shared edge is
                        // not counted as a miss.
                        if (c.NoRoundTrip) continue;
                        float cxp = c.X + c.W * 0.5f, cyp = c.Y + c.H * 0.5f;
                        float[] px = { cxp, c.X + 1f, c.X + c.W - 1f, cxp, cxp };
                        float[] py = { cyp, cyp, cyp, c.Y + c.InsetY, c.Y + c.H - c.InsetY };
                        bool all5 = true;
                        for (int k = 0; k < 5; k++)
                        {
                            // The same camera count the control list was built with. The hit path
                            // and the painter must agree about how many buttons exist - they did
                            // not, and this line is what proved it.
                            PageHit hit = Pages.HitTest(page, px[k], py[k], w, h, sub,
                                                        SweepExtraCams(w, h));
                            if (hit.Act != c.Act || hit.Arg != c.Arg) all5 = false;
                        }
                        Check(c.Name + " reaches its own action from all five points, " + tag,
                              all5, "");
                    }

                    // RULE 2 - APART.
                    for (int a = 0; a < all.Count; a++)
                        for (int b = a + 1; b < all.Count; b++)
                        {
                            Ctl p = all[a], q = all[b];
                            bool apart = p.X + p.W <= q.X || q.X + q.W <= p.X
                                      || p.Y + p.H <= q.Y || q.Y + q.H <= p.Y;
                            Check(p.Name + " does not overlap " + q.Name + ", " + tag, apart,
                                  "");
                        }
                }
            }
        }

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
