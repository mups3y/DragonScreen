// Tests for the NAV/Cover 3D globe's LONGITUDE - the vessel marker against a known lat/lon, and the
// textured disc against the marker. Written for register line S197.
//
// ---- WHAT S197 ASKED, AND WHY A PICTURE COULD NOT ANSWER IT ----
// On the pad (Cape Canaveral, lat 28.620268, lon -80.604992 - the run's own black box, row 1) the
// 05:12 capture put the green vessel marker 12 px off the centreline of an 863-px disc, i.e. ON THE
// VIEW'S CENTRE MERIDIAN, while the continents under it placed that meridian near 10 deg E - about
// 91 deg from where the vehicle actually was. S197 recorded TWO readings and deliberately chose
// neither:
//   (1) the marker ignores longitude and is drawn on whatever meridian faces the camera;
//   (2) the marker is right and the MAP is rotated - the texture's u origin is a quarter turn out.
// Both are ~90 deg, and THE CAPTURE CANNOT TELL THEM APART, because the globe deliberately follows
// the vehicle (NavPage.PlanetBody: lonCentre = s.Longitude + view.PlanetRotDeg). A correct marker at
// the view centre and a longitude-blind marker at the view centre are the same pixel. Reading the
// picture harder would never have decided it; separating the camera from the vehicle does.
//
// ---- HOW THESE CHECKS SEPARATE THEM ----
// PlanetRotDeg is the crew's manual spin, and it enters lonCentre ADDITIVELY. Setting it to
// -s.Longitude pins the globe at longitude 0 while the vehicle stays where it is - so the vessel is
// no longer at the view centre, and a marker that honours longitude MUST leave the centreline. That
// is MarkerHonoursLongitude(), and it is the whole discriminator.
//
// Then MapCentre()/Handedness() ask the other half: where does the DISC put a longitude? Both are
// recovered from the emitted BodyMap quads, so the map and the marker are compared AS DRAWN rather
// than as intended, against the one stated convention in TextureOriginLonDeg.
//
// ⚠ WHAT THESE CHECKS CANNOT DO, STATED SO NOBODY READS MORE INTO A GREEN RUN. They prove the two
// PURE paths agree with each other and with the code's assumption that texel u holds longitude
// 360u-180. They CANNOT prove that assumption against KSP's real scaled-space texture - there is no
// game here, and the PNG preview's stand-in is an ordinary equirectangular Earth that satisfies the
// assumption by construction (PreviewMain.LoadStandIn), so the preview cannot catch an origin error
// either. That last step is the glass, and it is S42's.
using System;
using DragonScreen;

public static class NavGlobeLongitudeTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, got.ToString("F4") + " vs " + want.ToString("F4")); }

    // ---- the pad, from the run's own black box (row 1). The one known lat/lon this line turns on.
    const double PadLat = 28.620268, PadLon = -80.604992;

    // The well the globe is drawn into. Wider than tall, as every caller's map well is, so the
    // radius comes off the HEIGHT - the case NavPage.GlobeDiscFrac is defined for.
    const float MX = 0f, MY = 0f, MW = 800f, MH = 600f, SC = 1f;
    const float CX = MX + MW * 0.5f, CY = MY + MH * 0.5f;

    static float R { get { return (float)(Math.Min(MW, MH) * PlanetGeom.DiscFillOfHalfHeight * 0.5); } }

    /// <summary>
    /// The longitude the DRAWING CODE assumes texel u=0 holds - NavPage.Globe's
    /// uMin = (lonCentre - 90 + 180)/360 says -180, i.e. an ordinary equirectangular map.
    ///
    /// ⚠ THIS IS A LOCK ON TWO THINGS AGREEING, NOT A FENCE AROUND THE CURRENT VALUE. It is the one
    /// place the convention is written down on the test side, so if S42 establishes that KSP's real
    /// scaled-space texture starts somewhere else (the surviving hypothesis is -90 - see
    /// QuarterTurnArithmetic) the fix changes NavPage.Globe and this constant IN THE SAME COMMIT, and
    /// the suite goes green again on the new convention. What it will not let anyone do is move one
    /// of them and leave the other behind.
    /// </summary>
    const double TextureOriginLonDeg = -180.0;

    static PageState Pad(double lat, double lon)
    {
        PageState s = new PageState();
        s.Valid = true;
        s.HasFix = true;
        s.Latitude = lat;
        s.Longitude = lon;

        PlanetOverlay ov = new PlanetOverlay();
        ov.Ready = true;
        ov.Vessel = new GlobePoint { Lat = lat, Lon = lon, Ratio = 1.0, Has = true };
        s.Planet = ov;
        return s;
    }

    static MapView View(double rotDeg)
    {
        MapView v = MapProjection.Default();
        v.PlanetRotDeg = rotDeg;
        v.PlanetZoom = 0;
        return v;
    }

    static DisplayList Draw(PageState s, MapView v)
    {
        DisplayList dl = new DisplayList(4096);
        NavPage.Planet(dl, s, v, MX, MY, MW, MH, SC);
        return dl;
    }

    static bool SameColour(Rgba a, Rgba b)
    { return Math.Abs(a.R - b.R) < 1e-4f && Math.Abs(a.G - b.G) < 1e-4f && Math.Abs(a.B - b.B) < 1e-4f; }

    /// <summary>
    /// The vessel marker's screen position, recovered from its HORIZONTAL BAR - the one Go-coloured
    /// rect that is 18*sc wide and 2*sc tall (NavPage.PlanetBody). The surrounding box's rects are
    /// 10x2 and 2x10, so this is unambiguous, and finding exactly one is itself asserted.
    /// </summary>
    static bool Marker(DisplayList dl, out float sx, out float sy)
    {
        sx = sy = 0f; int hits = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Rect) continue;
            if (!SameColour(c.Colour, DragonPalette.Go)) continue;
            if (Math.Abs(c.C - 18f * SC) > 1e-3f || Math.Abs(c.D - 2f * SC) > 1e-3f) continue;
            sx = c.A + 9f * SC; sy = c.B + 1f * SC; hits++;
        }
        return hits == 1;
    }

    /// <summary>
    /// The longitude the DISC paints at a given screen x, read out of the BodyMap quads the globe
    /// actually emitted - under the drawing code's own convention that texel u holds 360u-180.
    /// Picks the widest quad covering that x, which is the equator strip, so it is never a
    /// degenerate sliver near the poles. Handles the seam split, where two quads share a strip.
    /// </summary>
    static bool MapLonAt(DisplayList dl, float x, out double lon)
    {
        lon = 0.0; float bestW = -1f; bool found = false;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Image || c.Image != ImageId.BodyMap) continue;
            if (c.C <= 0f) continue;
            if (x < c.A - 1e-3f || x > c.A + c.C + 1e-3f) continue;
            if (c.C <= bestW) continue;
            bestW = c.C;
            double f = (x - c.A) / c.C;
            double u = c.UMin + f * (c.UMax - c.UMin);
            lon = MapProjection.Wrap180(360.0 * u + TextureOriginLonDeg);
            found = true;
        }
        return found;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen NAV globe longitude tests (S197)");
        MarkerHonoursLongitude();
        MarkerFollowsTheView();
        MapCentre();
        Handedness();
        QuarterTurnArithmetic();
        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    // ---------------------------------------------------------------- reading (1), put to the test

    /// <summary>
    /// THE DISCRIMINATOR. Pin the globe at longitude 0 and leave the vehicle at the Cape: a marker
    /// that honours longitude lands where the orthographic projection puts -80.604992, which is
    /// cos(lat)*sin(lon) = -0.8663 of the radius WEST of centre, close to the limb. A marker that
    /// ignored longitude would still be on the centreline, and every check here would fail.
    /// </summary>
    static void MarkerHonoursLongitude()
    {
        PageState s = Pad(PadLat, PadLon);
        DisplayList dl = Draw(s, View(-PadLon));      // lonCentre = PadLon + (-PadLon) = 0

        float sx, sy;
        Check("the pad marker is drawn exactly once", Marker(dl, out sx, out sy), "");

        double dlon = PadLon * Math.PI / 180.0, la = PadLat * Math.PI / 180.0;
        double wantX = CX + Math.Cos(la) * Math.Sin(dlon) * R;
        double wantY = CY - Math.Sin(la) * R;

        Near("marker x is the projected longitude, not the centreline", sx, wantX, 0.75);
        Near("marker y is the projected latitude", sy, wantY, 0.75);

        // The point of the whole line, stated as its own check so a regression names itself.
        Check("marker is WEST of the disc centre, not on it",
              CX - sx > 0.5f * R, "sx " + sx.ToString("F1") + " cx " + CX.ToString("F1"));

        // ---- and it TRACKS longitude, rather than merely being offset by a constant ----
        // Swept with the globe pinned, so every change in x is the longitude's doing: monotonic
        // across the near hemisphere, and each position matching the projection outright.
        float prev = float.NaN; bool mono = true;
        for (double lon = -80.0; lon <= 80.0 + 1e-9; lon += 20.0)
        {
            float tx, ty;
            if (!Marker(Draw(Pad(0.0, lon), View(-lon)), out tx, out ty)) { mono = false; break; }
            double want = CX + Math.Sin(lon * Math.PI / 180.0) * R;
            if (Math.Abs(tx - want) > 0.75) { mono = false; break; }
            if (!float.IsNaN(prev) && tx <= prev) { mono = false; break; }
            prev = tx;
        }
        Check("marker x rises with longitude across the near hemisphere", mono, "");

        // East and west of the SAME view centre land on opposite sides, equidistant.
        float ex, ey, wx, wy;
        Check("east marker drawn", Marker(Draw(Pad(0.0, 45.0), View(-45.0)), out ex, out ey), "");
        Check("west marker drawn", Marker(Draw(Pad(0.0, -45.0), View(45.0)), out wx, out wy), "");
        Near("east and west are mirrored about the centre", (ex - CX) + (wx - CX), 0.0, 0.75);
        Check("east is right of west", ex > wx, "");
    }

    // ---------------------------------------------------------- why the capture looked like it did

    /// <summary>
    /// The capture's 12-px-off-centre marker is the DESIGN, not the defect (NavPage.cs:439-440: "the
    /// globe follows the vehicle's longitude"). With no manual spin the vessel marker sits on the
    /// disc centre for EVERY longitude - which is why S197 could not decide from the picture, and is
    /// exactly the observation that has to stop being read as evidence of a longitude bug.
    /// </summary>
    static void MarkerFollowsTheView()
    {
        bool centred = true;
        foreach (double lon in new double[] { PadLon, 0.0, 45.0, -179.0, 175.0 })
        {
            float sx, sy;
            if (!Marker(Draw(Pad(0.0, lon), View(0.0)), out sx, out sy)) { centred = false; break; }
            if (Math.Abs(sx - CX) > 0.5f) { centred = false; break; }
        }
        Check("with no manual spin the marker is centred at any longitude", centred, "");

        // ...and the LATITUDE still reads, which is what the capture independently confirmed (210 px
        // north of centre on a 428-px radius => 29.4 deg N, against the black box's 28.620268).
        float px, py;
        Check("pad marker drawn", Marker(Draw(Pad(PadLat, PadLon), View(0.0)), out px, out py), "");
        Near("pad marker latitude", CY - py, Math.Sin(PadLat * Math.PI / 180.0) * R, 0.75);
    }

    // ---------------------------------------------------------------------------- the map, as drawn

    /// <summary>
    /// The disc paints the view's own centre longitude at the disc's centre - the "it cannot drift"
    /// half of NavPage.cs:439-440, checked against the emitted quads instead of trusted. Across the
    /// seam too, where the strip is split into two quads.
    /// </summary>
    static void MapCentre()
    {
        foreach (double lonCentre in new double[] { 0.0, PadLon, 45.0, 175.0, -175.0 })
        {
            DisplayList dl = Draw(Pad(0.0, lonCentre), View(0.0));
            double lon;
            Check("disc is textured at lonCentre=" + lonCentre, MapLonAt(dl, CX, out lon), "");
            Near("disc centre longitude at lonCentre=" + lonCentre,
                 MapProjection.Wrap180(lon - lonCentre), 0.0, 0.5);
        }

        // ---- THE SEAM IS REALLY EXERCISED, whatever the texture origin turns out to be ----
        // WHICH longitude straddles u=1 depends on the convention under test, so it is FOUND rather
        // than assumed. Hard-coding one (175 was the obvious pick) quietly stops testing the seam the
        // moment S42 moves the origin: the split lands on a longitude nobody looked at and the check
        // passes on the unsplit case forever. Caught by running the quarter-turn fix against this
        // suite before shipping it.
        int plain = int.MaxValue, seamQuads = 0; double seamLon = double.NaN;
        for (double lonCentre = -180.0; lonCentre < 180.0; lonCentre += 5.0)
        {
            int n = 0;
            DisplayList d = Draw(Pad(0.0, lonCentre), View(0.0));
            for (int i = 0; i < d.Count; i++)
                if (d.At(i).Kind == DrawKind.Image && d.At(i).Image == ImageId.BodyMap) n++;
            if (n < plain) plain = n;
            if (n > seamQuads) { seamQuads = n; seamLon = lonCentre; }
        }
        Check("some centre longitude splits its strips at the seam", seamQuads > plain,
              seamQuads + " vs " + plain);

        // ...and the split view still centres on its own longitude, which is the case the two-quad
        // branch exists to get right.
        if (!double.IsNaN(seamLon))
        {
            DisplayList d = Draw(Pad(0.0, seamLon), View(0.0));
            double slon;
            Check("the seam view is textured (lonCentre=" + seamLon + ")", MapLonAt(d, CX, out slon), "");
            Near("seam view centres on its own longitude",
                 MapProjection.Wrap180(slon - seamLon), 0.0, 0.5);
        }

        // AND THE TWO AGREE. The marker and the map are placed by the same lonCentre, so a vessel at
        // the view centre is painted on the meridian the map calls its own centre. This is what makes
        // the ~90 deg unattributable to a disagreement BETWEEN these two paths.
        foreach (double lon in new double[] { PadLon, 0.0, 100.0 })
        {
            DisplayList dl = Draw(Pad(0.0, lon), View(0.0));
            float sx, sy; double mlon;
            if (Marker(dl, out sx, out sy) && MapLonAt(dl, sx, out mlon))
                Near("map and marker agree on the meridian at lon=" + lon,
                     MapProjection.Wrap180(mlon - lon), 0.0, 0.5);
            else Check("map and marker both present at lon=" + lon, false, "");
        }
    }

    /// <summary>
    /// EAST IS ON THE RIGHT, on the disc as well as in the projection - so the globe is not MIRRORED.
    /// This is the check that separates a quarter turn from a flip, and the capture agrees with it:
    /// South America (west) was on the left and India (east) on the right. A mirrored disc would have
    /// put them the other way round, and the ~90 deg would have been a handedness fault instead.
    /// (PageTest.NavTexture pins the flat map's OPPOSITE convention; that the two cannot both suit one
    /// texture is QC C-09 / Q2's, not this line's.)
    /// </summary>
    static void Handedness()
    {
        DisplayList dl = Draw(Pad(0.0, 0.0), View(0.0));
        double west, east;
        Check("west limb textured", MapLonAt(dl, CX - R + 1f, out west), "");
        Check("east limb textured", MapLonAt(dl, CX + R - 1f, out east), "");
        Check("the visible hemisphere runs west-left to east-right", east > west,
              "west " + west.ToString("F1") + " east " + east.ToString("F1"));
        Near("left edge is lonCentre-90", west, -90.0, 1.5);
        Near("right edge is lonCentre+90", east, 90.0, 1.5);
    }

    // ------------------------------------------------------------------- the surviving hypothesis

    /// <summary>
    /// The arithmetic of reading (2), pinned so the number cannot drift while S42 carries it.
    ///
    /// The code paints texel u = (lonCentre+180)/360 at the disc centre. If KSP's scaled-space
    /// texture actually starts at longitude -90 rather than -180 - a quarter turn, u shifted by
    /// 0.25 - then that same texel holds lonCentre+90, and the pad view (lonCentre = -80.604992)
    /// would paint 9.4 deg E at its centre. The capture's continents - Africa centred, South America
    /// at the left limb, India at the right - put it near 10 deg E.
    ///
    /// ⚠ THAT LAST NUMBER IS AN IDENTIFICATION, NOT A PIXEL MEASUREMENT (S197 records it as measured
    /// off a PNG by a build chat and not confirmed in the capsule), so it is checked to 5 deg and
    /// stands as corroboration, not proof. This check is pure arithmetic about the hypothesis and
    /// stays true whether or not the hypothesis is ever adopted - it asserts nothing about how the
    /// globe is currently drawn, so fixing the globe does not falsify it.
    /// </summary>
    static void QuarterTurnArithmetic()
    {
        const double ObservedCentreLonDeg = 10.0;   // from the capture's continents (S197)

        double uCentre = (PadLon + 180.0) / 360.0;
        double underQuarterTurn = MapProjection.Wrap180(360.0 * uCentre - 90.0);

        Near("a -90 origin puts lonCentre+90 at the disc centre", underQuarterTurn, PadLon + 90.0, 1e-9);
        Near("...which matches the capture's continents", underQuarterTurn, ObservedCentreLonDeg, 5.0);
        Near("...and the discrepancy is a quarter turn",
             MapProjection.Wrap180(ObservedCentreLonDeg - PadLon), 90.0, 5.0);
    }
}
