/*
 * DragonScreen headless tests - pages, projection, controls, alarms.
 *
 * ---- WHAT THESE ARE FOR ----
 * The PNG preview answers "does it look right". It cannot answer "does the button you can see do the
 * thing its label says", and it cannot answer "is the map telling the truth about where the vehicle
 * is". Both of those are arithmetic, and both would otherwise be found in the capsule at the cost of
 * a restart each.
 *
 * The control checks all have the same shape on purpose: take the rectangle the page DRAWS, aim at
 * its centre, and assert the page's own hit test returns the action that rectangle is labelled with.
 * That is the ChromeBar.LinkRect rule - one source for drawing and hitting - turned into something
 * that fails the build rather than something a comment asks people to remember.
 */
using System;
using DragonScreen;

public static class PageTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok)
        {
            failures++;
            Console.WriteLine("  FAIL  " + what + "   " + detail);
        }
    }

    static void Eq(string what, double got, double want, double tol)
    {
        Check(what, Math.Abs(got - want) <= tol,
              "got " + got.ToString("F4") + " want " + want.ToString("F4"));
    }

    // The two real screen shapes. Anything laid out from the bottom or the right has to hold on both,
    // and the centre display is 7 px taller than its neighbours - see CLAUDE.md.
    const int W = 1280, H1 = 703, H2 = 710;

    public static int Run()
    {

        MissionButtonsTest();
        Console.WriteLine("DragonScreen page tests");

        Selection();
        Projection();
        Quads();
        ViewControls();
        NavControls();
        SettingsControls();
        Scales();
        AlarmRouting();
        Conic();
        Chrome();
        Velocity();
        Propellant();
        DockingBall();
        Capacity();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------ persisted page selection

    /// <summary>
    /// A REGRESSION SUITE FOR A BUG FOUND IN KSP.log, 2026-08-06.
    ///
    /// One touch on screen 3 wrote "0,0,0". Screens 1 and 2 had never been touched and were showing
    /// their cfg defaults, VEHICLE and FLIGHT - and that write pinned both to FLIGHT permanently,
    /// because Set filled every other field with a DEFAULTED value as though it had been chosen.
    ///
    /// The symptom a player sees is "my other two displays changed page by themselves", which is
    /// about as far from the cause as a symptom gets. Hence the coverage.
    /// </summary>
    static void Selection()
    {
        const int Pages = 5;
        const int Fallback = 9;   // an unmistakable value, so a fallback is never mistaken for data

        // ---- THE BUG ----
        string s = PageSelection.Set("", 3, 0, Pages);
        Check("setting one screen leaves the others unset (1)",
              PageSelection.Get(s, 1, Pages, Fallback) == Fallback, "got '" + s + "'");
        Check("setting one screen leaves the others unset (2)",
              PageSelection.Get(s, 2, Pages, Fallback) == Fallback, "got '" + s + "'");
        Check("the touched screen is remembered",
              PageSelection.Get(s, 3, Pages, Fallback) == 0, "got '" + s + "'");

        // A DELIBERATE zero must still survive, or the fix has just moved the bug: page 0 is FLIGHT
        // and choosing it is a perfectly ordinary thing to do.
        s = PageSelection.Set(s, 1, 0, Pages);
        Check("a chosen page 0 is kept", PageSelection.Get(s, 1, Pages, Fallback) == 0,
              "got '" + s + "'");
        Check("...without disturbing screen 2",
              PageSelection.Get(s, 2, Pages, Fallback) == Fallback, "got '" + s + "'");

        // Round-tripping every screen.
        s = "";
        for (int i = 1; i <= 3; i++) s = PageSelection.Set(s, i, i, Pages);
        for (int i = 1; i <= 3; i++)
            Check("round trip screen " + i, PageSelection.Get(s, i, Pages, Fallback) == i,
                  "got '" + s + "'");

        // ---- OLD SAVES ----
        // "0,0,0" was written by the previous version and must still read as three zeroes. Reading is
        // unchanged, so this is really a check that the fix did not need a version flag.
        Check("an old save still parses", PageSelection.Get("0,0,0", 2, Pages, Fallback) == 0, "");

        // ---- MALFORMED INPUT NEVER THROWS ----
        // A hand-edited save, a truncated string, a page index from a version with more pages.
        string[] junk = { null, "", ",,", "x,y,z", "-,-,-", "1", "1,2", "99,99,99", "-1,-1,-1",
                          "1,2,3,4,5", "   ", "1, 2 ,3" };
        foreach (string j in junk)
        {
            for (int i = 1; i <= 3; i++)
            {
                int v = PageSelection.Get(j, i, Pages, Fallback);
                Check("malformed '" + (j ?? "null") + "' screen " + i + " is in range",
                      v == Fallback || (v >= 0 && v < Pages), "got " + v);
            }
            // And Set on top of junk must produce something Get can read back.
            string fixedUp = PageSelection.Set(j, 2, 4, Pages);
            Check("repaired '" + (j ?? "null") + "'",
                  PageSelection.Get(fixedUp, 2, Pages, Fallback) == 4, "got '" + fixedUp + "'");
        }

        // Out-of-range writes are refused rather than clamped: a screen id of 7 is a caller bug, and
        // silently writing it to screen 3 would hide it.
        Check("screen 0 is refused", PageSelection.Set("1,1,1", 0, 2, Pages) == "1,1,1", "");
        Check("screen 4 is refused", PageSelection.Set("1,1,1", 4, 2, Pages) == "1,1,1", "");
        Check("page out of range is refused",
              PageSelection.Set("1,1,1", 2, Pages, Pages) == "1,1,1", "");
    }

    // ------------------------------------------------------------------ projection

    static void Projection()
    {
        MapView v = MapProjection.Default();
        float x = 24f, y = 52f, w = 932f, h = 563f;

        // The view centre lands on the rect centre. If this is wrong, everything else is decoration.
        float px, py;
        MapProjection.Project(0.0, 0.0, v, x, y, w, h, out px, out py);
        Eq("centre lon -> rect centre x", px, x + w * 0.5f, 0.01);
        Eq("centre lat -> rect centre y", py, y + h * 0.5f, 0.01);

        // NORTH IS UP. Page y increases downward, so a positive latitude must give a SMALLER y. This
        // is the flip most likely to be got wrong and least likely to be noticed - a mirrored map
        // still looks like a map.
        float nx, ny;
        MapProjection.Project(45.0, 0.0, v, x, y, w, h, out nx, out ny);
        Check("north is up", ny < py, "lat +45 gave y " + ny + " vs centre " + py);

        // EAST IS RIGHT.
        float ex, ey;
        MapProjection.Project(0.0, 45.0, v, x, y, w, h, out ex, out ey);
        Check("east is right", ex > px, "lon +45 gave x " + ex + " vs centre " + px);

        // Scale is one number for both axes, so a degree of latitude and a degree of longitude are
        // the same number of pixels. Two independent scales would fill the rect and squash the map.
        Eq("isotropic scale", Math.Abs(ny - py) / 45.0, Math.Abs(ex - px) / 45.0, 0.001);

        // Wrapping takes the short way round: 350 degrees east of us is 10 degrees WEST.
        Eq("wrap +350", MapProjection.Wrap180(350.0), -10.0, 1e-9);
        Eq("wrap -350", MapProjection.Wrap180(-350.0), 10.0, 1e-9);
        Eq("wrap 180", MapProjection.Wrap180(180.0), 180.0, 1e-9);
        Eq("wrap360 -10", MapProjection.Wrap360(-10.0), 350.0, 1e-9);

        // A point 179 east of centre must project to the RIGHT, and 181 east to the LEFT - the seam
        // has to be crossed by the projection, not clamped at.
        MapProjection.Project(0.0, 179.0, v, x, y, w, h, out ex, out ey);
        Check("179 E is to the right", ex > px, "x " + ex);
        MapProjection.Project(0.0, 181.0, v, x, y, w, h, out ex, out ey);
        Check("181 E wraps to the left", ex < px, "x " + ex);
    }

    // ------------------------------------------------------------------ the body texture quads

    static void Quads()
    {
        float x = 24f, y = 52f, w = 932f, h = 563f;
        MapQuad a, b;

        // Zoom 0: the whole world, one quad, LETTERBOXED rather than stretched.
        MapView v = MapProjection.Default();
        int n = MapProjection.BodyQuads(v, x, y, w, h, out a, out b);
        Check("zoom 0 is one quad", n == 1, "got " + n);
        Eq("zoom 0 uses the whole texture (u)", a.UMax - a.UMin, 1.0, 1e-6);
        Eq("zoom 0 uses the whole texture (v)", a.VMax - a.VMin, 1.0, 1e-6);
        Check("zoom 0 letterboxes", a.H <= h + 0.01f, "quad h " + a.H + " rect h " + h);
        Eq("zoom 0 quad is 2:1", a.W / a.H, 2.0, 1e-4);

        // Zoom 2 on the prime meridian: one quad, filling the rect exactly. Nothing may be drawn
        // outside the map panel, because there is no clipping to save us.
        v = MapProjection.Zoom(MapProjection.Default(), 2);
        n = MapProjection.BodyQuads(v, x, y, w, h, out a, out b);
        Check("zoom 2 is one quad", n == 1, "got " + n);
        Eq("zoom 2 fills the rect width", a.W, w, 0.01);
        Check("zoom 2 stays inside the rect", a.X >= x - 0.01f && a.X + a.W <= x + w + 0.01f,
              "x " + a.X + " w " + a.W);

        // ---- THE SEAM ----
        // Centred near +180, the visible window straddles the edge of the texture. Two quads, laid
        // side by side with no gap and no overlap, together exactly the rect.
        v = MapProjection.Centre(MapProjection.Zoom(MapProjection.Default(), 3), 0.0, 178.0);
        n = MapProjection.BodyQuads(v, x, y, w, h, out a, out b);
        Check("seam splits into two quads", n == 2, "got " + n);
        if (n == 2)
        {
            Eq("seam quads are contiguous", a.X + a.W, b.X, 0.01);
            Eq("seam quads span the rect", (a.W + b.W), w, 0.01);
            Eq("left quad ends at the texture edge", a.UMax, 1.0, 1e-6);
            Eq("right quad starts at the texture edge", b.UMin, 0.0, 1e-6);
            // The u width of each piece must match its pixel width, or the map shears at the seam -
            // which looks like a wrong coastline rather than like a bug.
            double ppdU = (a.UMax - a.UMin) / a.W;
            double ppdU2 = (b.UMax - b.UMin) / b.W;
            Eq("seam pieces share a scale", ppdU, ppdU2, 1e-7);
        }

        // ---- THE POLE ----
        // Panned north, the top of the window is off the map. The quad must SHRINK and stop at v = 1,
        // never stretch the last row of pixels up to the panel edge.
        v = MapProjection.Centre(MapProjection.Zoom(MapProjection.Default(), 2), 85.0, 0.0);
        n = MapProjection.BodyQuads(v, x, y, w, h, out a, out b);
        Check("polar view still draws", n >= 1, "got " + n);
        Eq("clamped at the north pole", a.VMax, 1.0, 1e-6);
        Check("polar quad is inside the rect", a.Y >= y - 0.01f, "y " + a.Y + " rect y " + y);
    }

    // ------------------------------------------------------------------ view state

    static void ViewControls()
    {
        MapView v = MapProjection.Default();
        Check("default follows the vehicle", v.Follow, "Follow was false");
        Check("default is the map view", v.Mode == NavMode.Map, "mode " + v.Mode);

        v = MapProjection.Zoom(v, -5);
        Check("zoom clamps at 0", v.ZoomStep == 0, "got " + v.ZoomStep);
        v = MapProjection.Zoom(v, 99);
        Check("zoom clamps at max", v.ZoomStep == MapProjection.MaxZoom, "got " + v.ZoomStep);

        // PANNING BY HAND STOPS THE MAP FOLLOWING. A map that snapped back to the vehicle a moment
        // after the crew dragged it would be the same failure as a page changing itself.
        v = MapProjection.Default();
        v = MapProjection.Pan(v, 1.0, 0.0);
        Check("panning clears follow", !v.Follow, "Follow stayed true");
        Check("panning moved the centre", Math.Abs(v.CentreLon) > 1.0, "lon " + v.CentreLon);

        v = MapProjection.Centre(v, 10.0, 20.0);
        Check("centre restores follow", v.Follow, "Follow stayed false");
        Eq("centre sets latitude", v.CentreLat, 10.0, 1e-9);

        // Track() must do NOTHING while the crew has it panned by hand.
        v = MapProjection.Pan(v, 0.0, 1.0);
        double heldLat = v.CentreLat, heldLon = v.CentreLon;
        v = MapProjection.Track(v, true, -45.0, 90.0);
        Eq("track leaves a manual pan alone (lat)", v.CentreLat, heldLat, 1e-9);
        Eq("track leaves a manual pan alone (lon)", v.CentreLon, heldLon, 1e-9);

        v = MapProjection.Centre(v, 0.0, 0.0);
        v = MapProjection.Track(v, true, -45.0, 90.0);
        Eq("track follows when following", v.CentreLat, -45.0, 1e-9);

        // Latitude cannot pan past the pole.
        v = MapProjection.Default();
        for (int i = 0; i < 20; i++) v = MapProjection.Pan(v, 0.0, 1.0);
        Check("latitude clamps at the pole", v.CentreLat <= 90.0, "lat " + v.CentreLat);

        v = MapProjection.Default();
        Check("next view goes to orbit", MapProjection.NextMode(v).Mode == NavMode.Orbit, "");
        Check("next view comes back",
              MapProjection.NextMode(MapProjection.NextMode(v)).Mode == NavMode.Map, "");
    }

    // ------------------------------------------------------------------ NAV controls

    static void NavControls()
    {
        foreach (int h in new int[] { H1, H2 })
        {
            HitIs("nav next view", PageAct.NavNextView, h, NavRect(NavCtl.Next, h));
            HitIs("nav zoom in", PageAct.NavZoomIn, h, NavRect(NavCtl.ZoomIn, h));
            HitIs("nav zoom out", PageAct.NavZoomOut, h, NavRect(NavCtl.ZoomOut, h));
            HitIs("nav centre", PageAct.NavCentre, h, NavRect(NavCtl.Centre, h));
            HitIs("nav up", PageAct.NavPanUp, h, NavRect(NavCtl.Up, h));
            HitIs("nav down", PageAct.NavPanDown, h, NavRect(NavCtl.Down, h));
            HitIs("nav left", PageAct.NavPanLeft, h, NavRect(NavCtl.Left, h));
            HitIs("nav right", PageAct.NavPanRight, h, NavRect(NavCtl.Right, h));

            // ---- NOTHING MAY SIT UNDER THE CHROME BAR ----
            // The bar is drawn over every page and tested before every page, so a control beneath it
            // is a control that can never be pressed. Cheap to assert, invisible in a screenshot.
            float barTop = ChromeBar.TopY(h);
            foreach (NavCtl c in AllNav())
            {
                float[] r = NavRect(c, h);
                Check("nav " + c + " clears the chrome bar", r[1] + r[3] <= barTop,
                      "bottom " + (r[1] + r[3]) + " bar top " + barTop);
            }

            // The map panel and the control column must not overlap either, or a touch meant for the
            // map would land on a button.
            float mx, my, mw, mh;
            NavPage.MapRect(W, h, out mx, out my, out mw, out mh);
            foreach (NavCtl c in AllNav())
            {
                float[] r = NavRect(c, h);
                Check("nav " + c + " is outside the map", r[0] >= mx + mw,
                      "x " + r[0] + " map right " + (mx + mw));
            }

            // A touch in the middle of the map is not a control.
            PageHit none = NavPage.HitTest(mx + mw * 0.5f, my + mh * 0.5f, W, h);
            Check("map body is not a button", none.Act == PageAct.None, "got " + none.Act);
        }
    }

    enum NavCtl { Next, ZoomIn, ZoomOut, Centre, Up, Down, Left, Right }

    static NavCtl[] AllNav()
    {
        return new NavCtl[] { NavCtl.Next, NavCtl.ZoomIn, NavCtl.ZoomOut, NavCtl.Centre,
                              NavCtl.Up, NavCtl.Down, NavCtl.Left, NavCtl.Right };
    }

    static float[] NavRect(NavCtl c, int h)
    {
        float x, y, w, hh;
        switch (c)
        {
            case NavCtl.Next: NavPage.NextViewRect(W, h, out x, out y, out w, out hh); break;
            case NavCtl.ZoomIn: NavPage.ZoomRect(W, h, true, out x, out y, out w, out hh); break;
            case NavCtl.ZoomOut: NavPage.ZoomRect(W, h, false, out x, out y, out w, out hh); break;
            case NavCtl.Centre: NavPage.PadRect(W, h, 0, 0, out x, out y, out w, out hh); break;
            case NavCtl.Up: NavPage.PadRect(W, h, 0, -1, out x, out y, out w, out hh); break;
            case NavCtl.Down: NavPage.PadRect(W, h, 0, 1, out x, out y, out w, out hh); break;
            case NavCtl.Left: NavPage.PadRect(W, h, -1, 0, out x, out y, out w, out hh); break;
            default: NavPage.PadRect(W, h, 1, 0, out x, out y, out w, out hh); break;
        }
        return new float[] { x, y, w, hh };
    }

    static void HitIs(string what, PageAct want, int h, float[] r)
    {
        PageHit got = NavPage.HitTest(r[0] + r[2] * 0.5f, r[1] + r[3] * 0.5f, W, h);
        Check(what + " (h" + h + ")", got.Act == want, "got " + got.Act);
    }

    // ------------------------------------------------------------------ SETTINGS controls

    static void SettingsControls()
    {
        foreach (int h in new int[] { H1, H2 })
        {
            float x, y, w2, hh;
            float barTop = ChromeBar.TopY(h);
            PageHit got;

            // ---- TABS ----
            // SETTINGS is a card with four tabs now, so every control below is reachable only on its
            // own tab. Tabs are tested BEFORE the page, so they must win wherever they overlap.
            for (int t = 0; t < SettingsPage.Tabs.Length; t++)
            {
                Card.TabRect(t, SettingsPage.Tabs.Length, W, h, out x, out y, out w2, out hh);
                got = Pages.HitTest(4, x + w2 * 0.5f, y + hh * 0.5f, W, h, 0);
                Check("settings tab " + SettingsPage.Tabs[t] + " selects itself",
                      got.Act == PageAct.SetSubview && got.Arg == t,
                      "got " + got.Act + " arg " + got.Arg);
                Check("settings tab " + t + " clears the chrome bar", y + hh <= barTop,
                      "bottom " + (y + hh));
            }

            // ---- CABIN ----
            SettingsPage.LightsRect(W, h, out x, out y, out w2, out hh);
            got = SettingsPage.HitTest(x + w2 * 0.5f, y + hh * 0.5f, W, h, SettingsPage.Cabin);
            Check("lights toggle", got.Act == PageAct.ToggleLights, "got " + got.Act);

            // ...and the SAME point on another tab must do nothing. A control that stayed live on a
            // tab that does not draw it would be an invisible button.
            got = SettingsPage.HitTest(x + w2 * 0.5f, y + hh * 0.5f, W, h, SettingsPage.Video);
            Check("lights is dead on the VIDEO tab", got.Act == PageAct.None, "got " + got.Act);

            for (int seat = 0; seat < 4; seat++)
            {
                SettingsPage.SeatRect(seat, W, h, out x, out y, out w2, out hh);
                got = SettingsPage.HitTest(x + w2 * 0.5f, y + hh * 0.5f, W, h, SettingsPage.Cabin);
                Check("seat " + seat + " selects itself",
                      got.Act == PageAct.ViewFromSeat && got.Arg == seat,
                      "got " + got.Act + " arg " + got.Arg);
                Check("seat " + seat + " clears the chrome bar", y + hh <= barTop,
                      "bottom " + (y + hh));
                if (seat > 0)
                {
                    float px2, py2, pw2, ph2;
                    SettingsPage.SeatRect(seat - 1, W, h, out px2, out py2, out pw2, out ph2);
                    Check("seat " + seat + " does not overlap seat " + (seat - 1),
                          px2 + pw2 <= x, "prev right " + (px2 + pw2));
                }
            }

            // ---- VIDEO ----
            for (int c = 0; c < SettingsPage.CamNames.Length; c++)
            {
                SettingsPage.CamRect(c, W, h, out x, out y, out w2, out hh);
                got = SettingsPage.HitTest(x + w2 * 0.5f, y + hh * 0.5f, W, h, SettingsPage.Video);
                Check("camera " + SettingsPage.CamNames[c] + " selects itself",
                      got.Act == PageAct.SetCamera && got.Arg == c,
                      "got " + got.Act + " arg " + got.Arg);
                Check("camera " + c + " clears the chrome bar", y + hh <= barTop,
                      "bottom " + (y + hh));
            }

            // ---- DISPLAY ----
            SettingsPage.BrightRect(true, W, h, out x, out y, out w2, out hh);
            got = SettingsPage.HitTest(x + w2 * 0.5f, y + hh * 0.5f, W, h, SettingsPage.Display);
            Check("brightness up", got.Act == PageAct.BrightUp, "got " + got.Act);
            SettingsPage.BrightRect(false, W, h, out x, out y, out w2, out hh);
            got = SettingsPage.HitTest(x + w2 * 0.5f, y + hh * 0.5f, W, h, SettingsPage.Display);
            Check("brightness down", got.Act == PageAct.BrightDown, "got " + got.Act);

            SettingsPage.CaptureRect(W, h, out x, out y, out w2, out hh);
            got = SettingsPage.HitTest(x + w2 * 0.5f, y + hh * 0.5f, W, h, SettingsPage.Display);
            Check("capture", got.Act == PageAct.Capture, "got " + got.Act);

            for (int screen = 1; screen <= 3; screen++)
            {
                for (int page = 0; page < ChromeBar.PageNames.Length; page++)
                {
                    SettingsPage.PageRect(screen, page, W, h, out x, out y, out w2, out hh);
                    got = SettingsPage.HitTest(x + w2 * 0.5f, y + hh * 0.5f, W, h,
                                               SettingsPage.Display);
                    int gs, gp;
                    PageHit.UnpackScreenPage(got.Arg, out gs, out gp);
                    Check("grid " + screen + "/" + ChromeBar.PageNames[page],
                          got.Act == PageAct.SetScreenPage && gs == screen && gp == page,
                          "got " + got.Act + " screen " + gs + " page " + gp);
                    Check("grid " + screen + "/" + page + " clears the chrome bar",
                          y + hh <= barTop, "bottom " + (y + hh));
                    Check("grid " + screen + "/" + page + " is on screen", x + w2 <= W,
                          "right " + (x + w2));
                }
            }
        }

        // The packing, independent of any layout.
        for (int s2 = 0; s2 <= 3; s2++)
            for (int p2 = 0; p2 < 8; p2++)
            {
                int gs, gp;
                PageHit.UnpackScreenPage(PageHit.PackScreenPage(s2, p2), out gs, out gp);
                Check("pack " + s2 + "/" + p2, gs == s2 && gp == p2, "got " + gs + "/" + gp);
            }
    }

    // ------------------------------------------------------------------ bar scales

    static void Scales()
    {
        // Kerbin.
        const double atmo = 70000.0, radius = 600000.0, circ = 2426.0;

        Eq("velocity at rest", BarScale.Velocity(0.0, circ), 0.0, 1e-9);
        Check("velocity pegs", BarScale.Velocity(99999.0, circ) <= 1.0, "over 1");
        Eq("velocity unknown scale", BarScale.Velocity(1000.0, 0.0), -1.0, 1e-9);

        Eq("altitude on the deck", BarScale.Altitude(0.0, atmo, radius), 0.0, 1e-9);
        // A 123 km orbit against a 350 km full scale is about a third of the bar. If this ever comes
        // out near zero or near full, the scale has stopped being useful even though it still "works".
        double alt = BarScale.Altitude(123400.0, atmo, radius);
        Check("a station orbit sits mid-bar", alt > 0.2 && alt < 0.6, "got " + alt);

        // Airless bodies have no atmosphere to scale from and must still produce something sane.
        double mun = BarScale.Altitude(50000.0, 0.0, 200000.0);
        Check("airless body has a scale", mun > 0.0 && mun <= 1.0, "got " + mun);

        Eq("equatorial inclination", BarScale.Inclination(0.0), 0.0, 1e-9);
        Eq("polar inclination is full", BarScale.Inclination(90.0), 1.0, 1e-9);
        // A retrograde orbit is as inclined as its prograde mirror - 170 degrees is 10 off the plane,
        // not nearly polar.
        Eq("retrograde folds back", BarScale.Inclination(170.0), BarScale.Inclination(10.0), 1e-9);
        Eq("negative inclination", BarScale.Inclination(-45.0), BarScale.Inclination(45.0), 1e-9);

        Eq("no range without a target", BarScale.Range(0.0), -1.0, 1e-9);
        Check("range pegs beyond full scale", BarScale.Range(5e6) <= 1.0, "over 1");
    }

    // ------------------------------------------------------------------ alarms

    static void AlarmRouting()
    {
        Check("low nominal", Alarms.Low(0.9) == Severity.Nominal, "");
        Check("low caution", Alarms.Low(0.2) == Severity.Caution, "");
        Check("low alarm", Alarms.Low(0.05) == Severity.Alarm, "");
        Check("high nominal", Alarms.High(0.1) == Severity.Nominal, "");
        Check("high caution", Alarms.High(0.8) == Severity.Caution, "");
        Check("high alarm", Alarms.High(0.95) == Severity.Alarm, "");
        Check("worst takes the worse", Alarms.Worst(Severity.Caution, Severity.Alarm)
              == Severity.Alarm, "");

        // AN INVALID FEED RAISES NOTHING. A page full of dashes must not also light every alarm on
        // the bar - "no data" and "everything is broken" are different states and the crew has to be
        // able to tell them apart.
        PageState dead = new PageState();
        dead.Valid = false;
        Check("no vessel raises no alarms", Alarms.Mask(dead) == 0, "mask " + Alarms.Mask(dead));

        // A healthy vessel lights nothing.
        PageState ok = Healthy();
        Check("healthy raises no alarms", Alarms.Mask(ok) == 0, "mask " + Alarms.Mask(ok));

        // Propellant in the warning band lights FLIGHT and VEHICLE, which are the two pages that
        // show it. That is the routing working: the alarm appears where the value lives.
        PageState low = Healthy();
        low.Propellant01 = 0.05;
        int m = Alarms.Mask(low);
        Check("low propellant lights FLIGHT", (m & 1) != 0, "mask " + m);
        Check("low propellant lights VEHICLE", (m & 2) != 0, "mask " + m);
        Check("low propellant leaves NAV alone", (m & 4) == 0, "mask " + m);

        // Alignment error alone must NOT light DOCKING - every approach has some, and a bar that
        // cries wolf on every approach is a bar nobody reads.
        PageState app = Healthy();
        app.HasTarget = true; app.Align01 = 0.95; app.ClosingFast = false;
        Check("alignment error is not an alarm", (Alarms.Mask(app) & 8) == 0, "");
        app.ClosingFast = true;
        Check("closing fast is an alarm", (Alarms.Mask(app) & 8) != 0, "");

        CabinBands();
    }

    /// <summary>
    /// A REGRESSION SUITE FOR "STATE ALARM FOR THE WHOLE ASCENT", flown 2026-08-06.
    ///
    /// The cabin limits used to be 0..1 DIAL FRACTIONS divided into Alarms.High, whose own breaks are
    /// 0.75/0.90. Composing two layers of fractions put the coolant-loop alarm at 0.72 of a 50 degree
    /// dial - 36 C - so every ascent lit the bar, because aerodynamic heating puts the hull near 95 C
    /// and the loops follow it.
    ///
    /// An alarm that is always on is not an alarm. These checks are written as FLIGHT PHASES rather
    /// than as numbers, because the numbers are what was wrong and phases are what anyone can judge.
    /// </summary>
    static void CabinBands()
    {
        // ---- EVERY LIMIT MUST FIT ON ITS OWN DIAL ----
        // Found while writing the ascent case: LoopFullScale was 50 and LoopAlarm 55, so the needle
        // pegged before the alarm could fire. A dial that saturates below its own redline stops
        // carrying information exactly when it matters.
        Check("PPO2 alarm is on the dial", CabinLimits.Ppo2Alarm < Cabin.Ppo2FullScale, "");
        Check("CO2 alarm is on the dial", CabinLimits.Co2Alarm < Cabin.Co2FullScale, "");
        Check("pressure alarm is on the dial", CabinLimits.PressAlarm < Cabin.PressFullScale, "");
        Check("cabin temp alarm is on the dial",
              CabinLimits.CabinTempAlarm < Cabin.TempFullScale, "");
        Check("loop alarm is on the dial", CabinLimits.LoopAlarm < Cabin.LoopFullScale,
              "alarm " + CabinLimits.LoopAlarm + " full scale " + Cabin.LoopFullScale);

        // Caution must come before alarm on every reading, in whichever direction it runs.
        Check("PPO2 cautions before it alarms", CabinLimits.Ppo2Caution > CabinLimits.Ppo2Alarm, "");
        Check("CO2 cautions before it alarms", CabinLimits.Co2Caution < CabinLimits.Co2Alarm, "");
        Check("pressure cautions before it alarms",
              CabinLimits.PressCaution > CabinLimits.PressAlarm, "");
        Check("loop cautions before it alarms", CabinLimits.LoopCaution < CabinLimits.LoopAlarm, "");

        // ---- AND EVERY READING THE MODEL CAN PRODUCE MUST FIT ON ITS DIAL TOO ----
        // Clearing the alarm limit turned out to be necessary but not sufficient. In flight
        // 2026-08-06 the loops were linear in hull temperature, LOOP A read 63.3 C against a 60 C
        // scale after an abort, and the needle pegged with a latched alarm. The check above passed
        // the whole time, because it only compared two constants to each other.
        //
        // So bound the MODEL, at the worst input KSP can hand it. A re-entering hull goes past
        // 1000 C; the loops must saturate below full scale rather than run off the end.
        double hottest = LoopA(2000.0);
        Check("loop A saturates on the dial", hottest < Cabin.LoopFullScale,
              "hull 2000 C gives " + hottest.ToString("F1") + " vs scale " + Cabin.LoopFullScale);
        Check("loop A still alarms at entry heating", LoopA(1000.0) > CabinLimits.LoopAlarm,
              "hull 1000 C gives " + LoopA(1000.0).ToString("F1"));
        Check("loop A quiet on the pad", LoopA(20.0) < CabinLimits.LoopCaution,
              "hull 20 C gives " + LoopA(20.0).ToString("F1"));
        // The abort case that started this: a hot pod must NOT alarm the coolant loop.
        Check("loop A quiet after an abort warms the pod", LoopA(170.0) < CabinLimits.LoopAlarm,
              "hull 170 C gives " + LoopA(170.0).ToString("F1"));

        // ---- THE PHASES ----
        // Hull temperatures are the real driver, so each case is just "what is the hull doing".
        Check("sitting on the pad is nominal", Phase(20.0, true) == Severity.Nominal,
              "got " + Phase(20.0, true));
        Check("orbital cruise is nominal", Phase(30.0, true) == Severity.Nominal,
              "got " + Phase(30.0, true));

        // THE BUG: a 95 C hull is an ordinary ascent, not an emergency.
        Check("ascent heating is not an alarm", Phase(95.0, true) < Severity.Alarm,
              "hull 95 C gave " + Phase(95.0, true));

        // ...but entry genuinely is. If this ever passes at Nominal the model has gone inert.
        Check("entry heating IS an alarm", Phase(1200.0, true) == Severity.Alarm,
              "hull 1200 C gave " + Phase(1200.0, true));

        // Losing power degrades the cabin and must show up - the whole argument for simulating
        // rather than faking is that a model can fail convincingly and a constant cannot.
        Check("unpowered raises a condition", Worst2(Unpowered()) >= Severity.Caution,
              "got " + Worst2(Unpowered()));
    }

    /// <summary>Worst cabin severity for a given hull temperature.</summary>
    /// <summary>Loop A at a given hull temperature, through the real model.</summary>
    static double LoopA(double hullC)
    {
        CabinInputs ci = new CabinInputs();
        ci.Crew = 4; ci.CrewCapacity = 4; ci.HullTempC = hullC;
        ci.MissionTime = 600.0; ci.Power01 = 0.8; ci.PowerFlow = 0.0; ci.Powered = true;
        return Cabin.Compute(ci).LoopAC;
    }

    static Severity Phase(double hullC, bool powered)
    {
        CabinInputs ci = new CabinInputs();
        ci.Crew = 4; ci.CrewCapacity = 4; ci.HullTempC = hullC;
        ci.MissionTime = 600.0; ci.Power01 = 0.8; ci.PowerFlow = 0.0; ci.Powered = powered;
        CabinReadout c = Cabin.Compute(ci);
        return Alarms.Worst(Alarms.LifeSupport(c), Alarms.Thermal(c));
    }

    static CabinReadout Unpowered()
    {
        CabinInputs ci = new CabinInputs();
        ci.Crew = 4; ci.CrewCapacity = 4; ci.HullTempC = 25.0;
        ci.MissionTime = 600.0; ci.Power01 = 0.0; ci.PowerFlow = -2.0; ci.Powered = false;
        return Cabin.Compute(ci);
    }

    static Severity Worst2(CabinReadout c)
    {
        return Alarms.Worst(Alarms.LifeSupport(c), Alarms.Thermal(c));
    }

    static PageState Healthy()
    {
        PageState s = new PageState();
        s.Valid = true;
        s.Propellant01 = 0.8; s.Power01 = 0.8; s.GForce01 = 0.1;
        CabinInputs ci = new CabinInputs();
        ci.Crew = 4; ci.CrewCapacity = 4; ci.HullTempC = 21.0;
        ci.MissionTime = 500.0; ci.Power01 = 0.8; ci.PowerFlow = 0.0; ci.Powered = true;
        s.Cabin = Cabin.Compute(ci);
        return s;
    }

    // ------------------------------------------------------------------ the orbit plot's conic

    static void Conic()
    {
        // A 100 x 200 km orbit of Kerbin.
        double rP = 700000.0, rA = 800000.0;
        double a = (rA + rP) * 0.5;
        double e = (rA - rP) / (rA + rP);

        Eq("periapsis is anomaly 0", OrbitPlot.TrueAnomaly(rP, a, e, true), 0.0, 1e-6);
        Eq("apoapsis is anomaly pi", Math.Abs(OrbitPlot.TrueAnomaly(rA, a, e, true)), Math.PI, 1e-6);

        // Same radius, opposite sign, depending on whether we are climbing or falling. That sign is
        // the entire reason the ascending flag exists - the conic cannot supply it.
        double mid = (rA + rP) * 0.5;
        double up = OrbitPlot.TrueAnomaly(mid, a, e, true);
        double down = OrbitPlot.TrueAnomaly(mid, a, e, false);
        Eq("ascending and descending mirror", up, -down, 1e-9);
        Check("ascending is the positive half", up > 0.0, "got " + up);

        // A circular orbit has no periapsis to speak of and must not divide by a zero eccentricity.
        Eq("circular orbit is safe", OrbitPlot.TrueAnomaly(700000.0, 700000.0, 0.0, true), 0.0, 1e-9);
    }

    // ------------------------------------------------------------------ chrome crowding

    /// <summary>
    /// The page links must not run into the right-hand readouts.
    ///
    /// Found on a capture PNG, 2026-08-06: "SETTINGS" and "NOMINAL" were touching in game while the
    /// preview showed a 72 px gap - GDI+ and Unity's font atlas measure D-DIN differently, so a small
    /// margin in the preview is not evidence of a margin on the glass.
    ///
    /// src/pure cannot measure a string (no font, by design), so this asserts a GENEROUS RESERVE
    /// instead of a real bounding box: whatever the widest page name turns out to be, there has to be
    /// room for it to overrun its cell and still clear the readouts.
    /// </summary>
    static void Chrome()
    {
        const float Reserve = 60f;
        foreach (int h in new int[] { H1, H2 })
        {
            int last = ChromeBar.PageNames.Length - 1;
            float x, y, rw, rh;
            ChromeBar.LinkRect(last, W, h, out x, out y, out rw, out rh);

            // STATE is right-aligned at w - 24 - 520; see ChromeBar.Build.
            float stateRight = W - 24f - 520f;
            Check("page links clear the STATE readout (h" + h + ")",
                  x + rw + Reserve <= stateRight,
                  "links end " + (x + rw) + ", state right edge " + stateRight);

            // And every link is still inside the screen.
            Check("last link is on screen (h" + h + ")", x + rw <= W, "right " + (x + rw));

            // The centre of every link still hits itself - the pitch change must not break the
            // one-source rule between LinkRect and HitTest.
            for (int i = 0; i < ChromeBar.PageNames.Length; i++)
            {
                ChromeBar.LinkRect(i, W, h, out x, out y, out rw, out rh);
                Check("chrome link " + ChromeBar.PageNames[i] + " hits itself",
                      ChromeBar.HitTest(x + rw * 0.5f, y + rh * 0.5f, W, h) == i, "");
            }
        }
    }

    // ------------------------------------------------------------------ which velocity

    /// <summary>
    /// A REGRESSION SUITE FOR THE 175 m/s BUG, WHICH HAS NOW HAPPENED TWICE.
    ///
    /// Orbital speed on the launchpad is Kerbin's rotation. Fixed on FLIGHT when the user reported
    /// it; reappeared on VEHICLE's new velocity bar because that bar read the orbital value directly.
    /// Both pages now go through OrbitReadout.Velocity, and these checks are on the RULE rather than
    /// on either page, so a third page gets it right by construction.
    /// </summary>
    static void Velocity()
    {
        PageState s = Healthy();
        s.Velocity = "175 m/s"; s.VelocityMps = 174.6;
        s.SurfaceVelocity = "0 m/s"; s.SurfaceVelocityMps = 0.0;

        string cap, val; double mps;

        s.Regime = FlightRegime.Ground;
        OrbitReadout.Velocity(s, out cap, out val, out mps);
        Check("on the pad the value is surface speed", val == "0 m/s", "got " + val);
        Check("on the pad the caption says surface", cap == "SURFACE VELOCITY", "got " + cap);
        Check("on the pad the BAR uses surface speed too", mps == 0.0, "got " + mps);

        s.Regime = FlightRegime.Atmosphere;
        OrbitReadout.Velocity(s, out cap, out val, out mps);
        Check("in atmosphere the value is surface speed", val == "0 m/s", "got " + val);

        s.Regime = FlightRegime.Space;
        OrbitReadout.Velocity(s, out cap, out val, out mps);
        Check("in space the value is orbital speed", val == "175 m/s", "got " + val);
        Check("in space the caption says orbital", cap == "ORBITAL VELOCITY", "got " + cap);
        Check("in space the bar uses orbital speed", mps == 174.6, "got " + mps);

        // ---- AND THE PAGES THEMSELVES ----
        // The rule being right is not enough; the question is whether every page calls it. Build each
        // page on the pad and assert the orbital number never reaches the glass.
        DisplayList dl = new DisplayList(Pages.Commands + ChromeBar.Commands + 4);
        s.Regime = FlightRegime.Ground;
        s.ApogeeShown = false; s.PerigeeShown = false;
        for (int p = 0; p < ChromeBar.PageNames.Length; p++)
        {
            dl.Clear();
            Pages.Build(dl, p, W, H1, s, MapProjection.Default(), 1);
            bool leaked = false, sawOrbitalCaption = false;
            for (int i = 0; i < dl.Count; i++)
            {
                if (dl.At(i).Kind != DrawKind.Text) continue;
                if (dl.At(i).Str == "175 m/s") leaked = true;
                if (dl.At(i).Str == "ORBITAL VELOCITY") sawOrbitalCaption = true;
            }
            Check("page " + ChromeBar.PageNames[p] + " hides orbital speed on the pad", !leaked, "");
            Check("page " + ChromeBar.PageNames[p] + " does not say ORBITAL on the pad",
                  !sawOrbitalCaption, "");
        }
    }

    // ------------------------------------------------------------------ propellant

    /// <summary>
    /// A REGRESSION SUITE FOR "THE PROPELLANT GAUGE READ 100% ALL THE WAY TO ORBIT", flown
    /// 2026-08-06.
    ///
    /// It was reading MonoPropellant - the Dracos - which is untouched during ascent. Nothing was
    /// broken; the gauge was answering a question nobody was asking, exactly like orbital speed on
    /// the launchpad. It now follows the engines that are actually burning.
    /// </summary>
    static void Propellant()
    {
        double[] f = new double[PropellantReadout.MaxSources];
        string[] n = new string[PropellantReadout.MaxSources];

        // ---- THE MINIMUM, NOT THE AVERAGE ----
        // A bipropellant stage is finished when the FIRST propellant runs out. Averaging would read
        // 50% at the instant the oxidiser hit zero and the engines died.
        f[0] = 1.0; f[1] = 0.0;
        Eq("empty oxidiser empties the gauge", PropellantReadout.Fraction(f, 2), 0.0, 1e-9);
        f[0] = 0.80; f[1] = 0.62;
        Eq("the gauge follows the lower tank", PropellantReadout.Fraction(f, 2), 0.62, 1e-9);

        // No sources is NOT zero - "no tank fitted" and "you are out" must look different.
        Eq("no engines means no reading", PropellantReadout.Fraction(f, 0), -1.0, 1e-9);
        Eq("a null array is safe", PropellantReadout.Fraction(null, 2), -1.0, 1e-9);

        // Out-of-range input clamps rather than escaping the dial.
        f[0] = 1.7; f[1] = 2.0;
        Eq("over-full clamps", PropellantReadout.Fraction(f, 2), 1.0, 1e-9);
        f[0] = -0.5;
        Eq("negative clamps", PropellantReadout.Fraction(f, 2), 0.0, 1e-9);

        // ---- THE CAPTION NAMES THE SOURCE ----
        // The dial means booster LF/OX, then the second stage, then Draco monopropellant. A number
        // that silently changes meaning is the velocity bug in another costume.
        n[0] = "LiquidFuel"; n[1] = "Oxidizer";
        Check("ascent names the booster propellant",
              PropellantReadout.Caption(n, 2) == "PROPELLANT LF/OX",
              "got '" + PropellantReadout.Caption(n, 2) + "'");

        n[0] = "MonoPropellant";
        Check("a separated capsule names the Dracos",
              PropellantReadout.Caption(n, 1) == "PROPELLANT MONOPROP",
              "got '" + PropellantReadout.Caption(n, 1) + "'");

        Check("no source falls back to the plain caption",
              PropellantReadout.Caption(n, 0) == "PROPELLANT",
              "got '" + PropellantReadout.Caption(n, 0) + "'");

        // An unfamiliar resource must read as itself rather than vanish.
        n[0] = "Karbonite";
        Check("an unknown propellant still names itself",
              PropellantReadout.Caption(n, 1).Contains("KARBONIT"),
              "got '" + PropellantReadout.Caption(n, 1) + "'");

        // The caption sits under a dial, so it cannot grow without limit.
        for (int i = 0; i < PropellantReadout.MaxSources; i++) n[i] = "LiquidFuel";
        Check("a long caption is capped",
              PropellantReadout.Caption(n, PropellantReadout.MaxSources).Length <= 30,
              "got '" + PropellantReadout.Caption(n, PropellantReadout.MaxSources) + "'");

        Check("nulls in the name list are skipped",
              PropellantReadout.Join(new string[] { null, "Oxidizer" }, 2) == "OX",
              "got '" + PropellantReadout.Join(new string[] { null, "Oxidizer" }, 2) + "'");

        // ---- AND THE PAGE USES IT ----
        PageState s = Healthy();
        s.PropellantCaption = "PROPELLANT LF/OX";
        DisplayList dl = new DisplayList(Pages.Commands + ChromeBar.Commands + 4);
        Pages.Build(dl, 0, W, H1, s, MapProjection.Default(), 1);
        // The caption WRAPS at its last space - a single-line "PROPELLANT LF/OX" was wide enough to
        // reach the dial's lower arc ends. Both halves must reach the glass.
        bool head = false, tail = false;
        for (int i = 0; i < dl.Count; i++)
        {
            if (dl.At(i).Kind != DrawKind.Text) continue;
            if (dl.At(i).Str == "PROPELLANT") head = true;
            if (dl.At(i).Str == "LF/OX") tail = true;
        }
        Check("FLIGHT prints the propellant caption", head, "");
        Check("FLIGHT prints the propellant source on its own line", tail, "");
    }

    // ------------------------------------------------------------------ the attitude ball

    /// <summary>
    /// The navball has to sit CONCENTRIC with the HUD and clear of the ALIGN sweep.
    ///
    /// First version sized it at 0.52 of the ring height against an ALIGN ring at 0.30 radius, which
    /// left about nine pixels. In game the two touched and it read as misplaced rather than tight.
    /// The diameter is now derived from the ring it must fit inside, and this asserts the CLEARANCE
    /// rather than the constant - so the two cannot be edited apart.
    /// </summary>
    static void DockingBall()
    {
        foreach (int h in new int[] { H1, H2 })
        {
            float body = DockingPage.BodyHeight(h);
            float d = DockingPage.RingDiameter(h);
            float r = d * 0.5f;

            // ---- THE FOUR CORNERS SIT ON THE SOURCE'S SHARED INSET ----
            // Second.vue: left/right 14.59%, top 3%, bottom 8%. Asserting the INSET rather than the
            // pixel means the layout stays quotable against the reference.
            float ulx, uly, urx, ury, llx, lly, lrx, lry;
            DockingPage.CornerCentre(0, W, h, out ulx, out uly);
            DockingPage.CornerCentre(1, W, h, out urx, out ury);
            DockingPage.CornerCentre(2, W, h, out llx, out lly);
            DockingPage.CornerCentre(3, W, h, out lrx, out lry);

            Eq("upper-left is at 14.59% (h" + h + ")", ulx - r, W * 0.1459f, 0.01);
            Eq("upper-right is at 14.59% (h" + h + ")", W - (urx + r), W * 0.1459f, 0.01);
            Eq("upper row is at 3% (h" + h + ")", uly - r, body * 0.03f, 0.01);
            Eq("lower row is at 8% (h" + h + ")", body - (lly + r), body * 0.08f, 0.01);
            Eq("the two upper corners share a row", uly, ury, 0.01);
            Eq("the two lower corners share a row", lly, lry, 0.01);
            Eq("the two left corners share a column", ulx, llx, 0.01);

            // Nothing may sit under the chrome bar, and nothing may leave the page.
            Check("lower corners clear the chrome bar (h" + h + ")",
                  lly + r <= ChromeBar.TopY(h), "bottom " + (lly + r));
            Check("upper corners are on the page (h" + h + ")", uly - r >= 0f, "top " + (uly - r));
            Check("right corners are on the page (h" + h + ")", lrx + r <= W, "right " + (lrx + r));

            // The corner rings must not collide with the central HUD ring, or the page reads as a
            // pile rather than a layout.
            float hudR = DockingPage.HudSize(W, h) * 0.25f;   // #hud-ring is HALF the darken size
            float cx = W * 0.5f, cy = body * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float qx, qy;
                DockingPage.CornerCentre(i, W, h, out qx, out qy);
                double dist = Math.Sqrt((qx - cx) * (qx - cx) + (qy - cy) * (qy - cy));
                Check("corner " + i + " clears the HUD ring (h" + h + ")", dist > hudR + r,
                      "distance " + dist.ToString("F1") + " needs > " + (hudR + r).ToString("F1"));
            }
        }

        // ---- THE ATTITUDE BALL IS IN THE LOWER-LEFT RING, NOT THE MIDDLE ----
        // This is the correction the audit forced. The old page put it dead centre inside the HUD
        // rings; Second.vue puts it at `#navball { left: 14.59%; bottom: 8% }`, in its own corner.
        PageState s = Healthy();
        s.HasTarget = true; s.TargetName = "STATION";
        s.RangeText = "100 m"; s.RateText = "-0.2 m/s";
        DisplayList dl = new DisplayList(Pages.Commands + ChromeBar.Commands + 4);
        Pages.Build(dl, 3, W, H1, s, MapProjection.Default(), 1);

        float wantX, wantY;
        DockingPage.CornerCentre(2, W, H1, out wantX, out wantY);
        bool found = false;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Image || c.Image != ImageId.NavBallLive) continue;
            found = true;
            Eq("ball sits in the lower-left ring (x)", c.A + c.C * 0.5f, wantX, 0.01);
            Eq("ball sits in the lower-left ring (y)", c.B + c.D * 0.5f, wantY, 0.01);
            Eq("ball is square", c.C, c.D, 0.01);
            Check("ball fits inside its ring", c.C <= DockingPage.RingDiameter(H1),
                  "ball " + c.C + " ring " + DockingPage.RingDiameter(H1));
        }
        Check("DOCKING draws an attitude ball", found, "no NavBallLive command emitted");

        // The live view is the BACKGROUND: full bleed, behind everything, and drawn before the HUD.
        int camAt = -1, hudAt = -1;
        for (int i = 0; i < dl.Count; i++)
        {
            if (dl.At(i).Kind != DrawKind.Image) continue;
            if (dl.At(i).Image == ImageId.DockingCamLive && camAt < 0) camAt = i;
            if (dl.At(i).Image == ImageId.HudRing && hudAt < 0) hudAt = i;
        }
        Check("the docking view is drawn", camAt >= 0, "no DockingCamLive command");
        Check("the view is behind the HUD", camAt >= 0 && hudAt > camAt,
              "cam at " + camAt + ", hud at " + hudAt);
        if (camAt >= 0)
        {
            DrawCmd c = dl.At(camAt);
            Eq("the view is full bleed (x)", c.A, 0f, 0.01);
            Eq("the view is full bleed (w)", c.C, W, 0.01);
            Eq("the view covers the body", c.D, DockingPage.BodyHeight(H1), 0.01);
        }

        // ---- NO TARGET STILL SHOWS THE CONTROL RINGS ----
        // Deliberate, and a change from the old page: attitude and RCS demand are useful whether or
        // not anything is selected. Only the target-relative half is withheld.
        PageState none = Healthy();
        none.HasTarget = false;
        dl.Clear();
        Pages.Build(dl, 3, W, H1, none, MapProjection.Default(), 1);
        bool ball = false, range = false;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Image && c.Image == ImageId.NavBallLive) ball = true;
            if (c.Kind == DrawKind.Text && c.Str == "RANGE") range = true;
        }
        Check("no target keeps the attitude ball", ball, "");
        Check("no target withholds RANGE", !range, "");
    }

    // ------------------------------------------------------------------ display list capacity

    /// <summary>
    /// Every page, at both real screen sizes, must fit the command buffer.
    ///
    /// An overflow DROPS commands (DisplayList.Add), so the symptom in game is a page missing part of
    /// itself with nothing but a one-line warning in the log - and NAV's track is 90 commands on its
    /// own, so this is a live risk rather than a theoretical one.
    /// </summary>
    static void Capacity()
    {
        PageState s = Healthy();
        s.TrackLat = new double[90];
        s.TrackLon = new double[90];
        for (int i = 0; i < 90; i++)
        {
            s.TrackLat[i] = 45.0 * Math.Sin(i * 0.07);
            s.TrackLon[i] = MapProjection.Wrap180(i * 4.0 - 180.0);
        }
        s.TrackCount = 90;
        s.HasFix = true; s.HasTarget = true; s.HasTargetGround = true;
        s.ApogeeShown = true; s.PerigeeShown = true;
        s.BodyRadiusM = 600000.0; s.ApogeeM = 124000.0; s.PerigeeM = 121900.0;
        s.AltitudeM = 123400.0; s.AtmosphereDepthM = 70000.0; s.CircularSpeedMps = 2426.0;
        s.ScreenPages = new int[] { -1, 1, 0, 2 };

        DisplayList dl = new DisplayList(Pages.Commands + ChromeBar.Commands + 4);
        ChromeState cs = new ChromeState();

        foreach (int h in new int[] { H1, H2 })
        {
            for (int p = 0; p < ChromeBar.PageNames.Length; p++)
            {
                // Both NAV views, because the orbit plot's 72 dots are a different budget from the
                // map's 90 and only one of them can be on screen at a time.
                foreach (NavMode mode in new NavMode[] { NavMode.Map, NavMode.Orbit })
                {
                    MapView v = MapProjection.Default();
                    v = MapProjection.Zoom(v, 2);
                    if (mode == NavMode.Orbit) v = MapProjection.NextMode(v);

                    dl.Clear();
                    Pages.Build(dl, p, W, h, s, v, 2);
                    ChromeBar.Build(dl, W, h, cs);
                    Check("page " + ChromeBar.PageNames[p] + " (" + mode + ", h" + h + ") fits",
                          !dl.Overflowed,
                          "used " + dl.Count + " of " + dl.Capacity);
                }
            }
        }

        // And with no vessel at all, which is the state on the loading screen and between scenes.
        PageState dead = new PageState();
        for (int p = 0; p < ChromeBar.PageNames.Length; p++)
        {
            dl.Clear();
            Pages.Build(dl, p, W, H1, dead, MapProjection.Default(), 1);
            Check("page " + ChromeBar.PageNames[p] + " survives no vessel", !dl.Overflowed,
                  "used " + dl.Count);
        }
    }
    /// <summary>
    /// The FLIGHT page's mission controls: AUTO SEQUENCE and the three phase buttons.
    ///
    /// ---- ⛔ A CONTROL SITTING ON TOP OF ANOTHER ONE HAS HAPPENED TWICE ON THIS PAGE. ----
    /// AutoRect's own comment records the first: it was placed at the bottom of the sidebar, straight
    /// on top of DRAGON SEPARATION and NOSE CONE OPEN. Adding three more buttons underneath it without
    /// a round-trip check would be inviting the third. Every rect must hit-test back to ITS OWN
    /// action, at the centre and at all four corners, and none may overlap or leave the screen.
    /// </summary>
    static void MissionButtonsTest()
    {
        int[] sizes = { 1280, 1024, 800 };
        int[] heights = { 703, 710, 600 };

        for (int si = 0; si < sizes.Length; si++)
        {
            int w = sizes[si], h = heights[si];
            float ax, ay, aw, ah;
            Pages.AutoRect(w, h, out ax, out ay, out aw, out ah);

            PageAct[] want = { PageAct.Rendezvous, PageAct.AutoDock, PageAct.UndockAndLand };
            float[] xs = new float[Pages.MissionButtons + 1];
            float[] ys = new float[Pages.MissionButtons + 1];
            float[] ws = new float[Pages.MissionButtons + 1];
            float[] hs = new float[Pages.MissionButtons + 1];
            xs[0] = ax; ys[0] = ay; ws[0] = aw; hs[0] = ah;

            for (int i = 0; i < Pages.MissionButtons; i++)
            {
                float x, y, rw, rh;
                Pages.MissionRect(i, w, h, out x, out y, out rw, out rh);
                xs[i + 1] = x; ys[i + 1] = y; ws[i + 1] = rw; hs[i + 1] = rh;

                // Centre and all four corners, inset by a pixel so a shared edge is not a failure.
                Check("mission " + i + " centre round-trips at " + w + "x" + h,
                      Pages.HitTest(0, x + rw * 0.5f, y + rh * 0.5f, w, h).Act == want[i], "");
                Check("mission " + i + " top-left round-trips at " + w + "x" + h,
                      Pages.HitTest(0, x + 1f, y + 1f, w, h).Act == want[i], "");
                Check("mission " + i + " bottom-right round-trips at " + w + "x" + h,
                      Pages.HitTest(0, x + rw - 1f, y + rh - 1f, w, h).Act == want[i], "");

                Check("mission " + i + " is on screen at " + w + "x" + h,
                      x >= 0f && y >= 0f && x + rw <= w && y + rh <= h,
                      x.ToString("F0") + "," + y.ToString("F0"));
            }

            // AUTO SEQUENCE must still answer for itself - the new buttons sit under it.
            Check("AUTO SEQUENCE still round-trips at " + w + "x" + h,
                  Pages.HitTest(0, ax + aw * 0.5f, ay + ah * 0.5f, w, h).Act
                      == PageAct.ToggleAuto, "");

            for (int a = 0; a < xs.Length; a++)
                for (int b = a + 1; b < xs.Length; b++)
                {
                    bool apart = xs[a] + ws[a] <= xs[b] || xs[b] + ws[b] <= xs[a]
                              || ys[a] + hs[a] <= ys[b] || ys[b] + hs[b] <= ys[a];
                    Check("button " + a + " does not overlap button " + b + " at " + w + "x" + h,
                          apart, "");
                }
        }

        // The labels must say what the button does, and change when it is running.
        PageState s = new PageState();
        s.Valid = true;
        Check("the rendezvous button names itself when idle",
              Pages.MissionLabel(s, 0) == "RENDEZVOUS", Pages.MissionLabel(s, 0));
        s.RendezvousEngaged = true; s.RendezvousNote = "PHASING - 51 km gap";
        Check("and reports what it is doing when running",
              Pages.MissionLabel(s, 0).StartsWith("RNDZ") && Pages.MissionLabel(s, 0).Length > 6,
              Pages.MissionLabel(s, 0));

        // ⚠ Greyed, not hidden: a live-looking button that does nothing reads as a dead screen.
        PageState d = new PageState();
        d.Valid = true; d.Docked = true;
        Check("docked, there is nothing left to rendezvous with", !Pages.MissionUsable(d, 0), "");
        Check("docked, auto-dock is spent", !Pages.MissionUsable(d, 1), "");
        Check("docked, UNDOCK is the live one", Pages.MissionUsable(d, 2), "");
        PageState f = new PageState();
        f.Valid = true;
        Check("free-flying, UNDOCK is meaningless", !Pages.MissionUsable(f, 2), "");
        Check("free-flying, rendezvous is available", Pages.MissionUsable(f, 0), "");
    }

}
