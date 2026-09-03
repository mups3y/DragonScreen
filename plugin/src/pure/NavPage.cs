/*
 * DragonScreen - NavPage
 *
 * PURE. NAV: where the vehicle is over the world, and where its orbit goes.
 *
 * ---- BUILT FROM THE LIVE DEMO, NOT INVENTED ----
 * docs/REFERENCE_PAGES.md, "THE LIVE DEMO", finding 1: the right panel of the real deorbit page is an
 * INTERACTIVE world map - four pan arrows, a centre reset, +/- zoom, and a NEXT VIEW button that
 * cycles views, labelled `CAMERA / Auto - Map IO`. It is a map with controls, not a static plot, and
 * that is why this page has a control cluster at all. The arrow/centre/zoom arrangement is
 * NavEarth.vue's: a centre button that reveals four arrows around it, with zoom to one side.
 *
 * Frame 67's right-hand panel is the OTHER view - the orbit drawn as an arc with markers, and TARGET
 * LATITUDE / TARGET LONGITUDE beneath it. Rather than choose, NEXT VIEW switches between them, which
 * is what the demo's button does anyway.
 *
 * ---- THE MAP IS THE BODY'S OWN TEXTURE, AND THE PAGE WORKS WITHOUT IT ----
 * ImageId.BodyMap resolves at runtime to KSP's scaled-space texture for whatever body the vessel is
 * at, so this is correct over Kerbin, over the Mun, and over Earth under RSS with nothing bundled.
 * But it is drawn as a LAYER: the graticule, the ground track, the markers and every readout are
 * emitted whether or not the texture resolved. If the lookup ever fails the page degrades to a
 * coordinate grid with a correct track on it, which is still flyable - rather than to a blank panel,
 * which is what a page built around the image would give.
 *
 * ---- WHY THE TRACK IS DOTS ----
 * There is no line primitive: GL has no trustworthy line width and a thick-line quad would put a
 * piece of geometry in each renderer for the two to disagree about. Sampling the track densely and
 * drawing a small square per sample costs one command each, is exactly the same in both renderers,
 * and reads as the dotted track the reference art uses anyway.
 */
namespace DragonScreen
{
    public static class NavPage
    {
        private const float Pad = 24f;
        private const float ColumnW = 276f;
        /// <summary>26, not 18: at 18 the headers sat hard against the top frame line.</summary>
        private const float HeaderY = 26f;
        private const float MapTop = 58f;

        /// <summary>
        /// Horizontal bands the ORBIT view's globe is built from. See Globe().
        ///
        /// ---- THIS NO LONGER CONTROLS ROUNDNESS ----
        /// It used to: 48 square-ended bands gave a visibly stepped limb in game. The fringe trim in
        /// Globe() now bounds the disc with a real arc, so the silhouette is circular at any strip
        /// count. What this number still controls is how smoothly the TEXTURE flows across bands -
        /// each band has its own horizontal scale, so too few leaves coastlines jogging sideways near
        /// the poles. 64 is where that stops showing.
        ///
        /// Each band is one command, or two when the visible hemisphere straddles the texture seam,
        /// so this is the single biggest term in NAV's command budget - hence the capacity test
        /// covering both views.
        /// </summary>
        private const int GlobeStrips = 64;

        // ---- control geometry ----
        // Every one of these is computed by a *Rect below and used by BOTH Build and HitTest. See
        // PageAction's header: a control drawn from one calculation and hit from another drifts the
        // first time either is touched.
        private const float BtnW = 56f, BtnH = 40f, BtnGap = 4f;

        /// <summary>The map panel, in page pixels.</summary>
        public static void MapRect(int w, int h, out float x, out float y, out float rw, out float rh)
        {
            x = Pad;
            y = MapTop;
            rw = w - Pad * 2f - ColumnW - Pad;
            if (rw < 64f) rw = 64f;
            rh = (h - ChromeBar.Height - Pad) - MapTop;
            if (rh < 64f) rh = 64f;
        }

        /// <summary>Left edge of the readout / control column.</summary>
        private static float ColumnX(int w) { return w - Pad - ColumnW; }

        /// <summary>Bottom of the map, which the control cluster is anchored to.</summary>
        private static float ColumnBottom(int h) { return h - ChromeBar.Height - Pad; }

        public static void NextViewRect(int w, int h, out float x, out float y,
                                        out float rw, out float rh)
        {
            x = ColumnX(w); rw = ColumnW; rh = BtnH;
            y = ColumnBottom(h) - BtnH;
        }

        public static void ZoomRect(int w, int h, bool inward, out float x, out float y,
                                    out float rw, out float rh)
        {
            rw = BtnW; rh = BtnH;
            y = ColumnBottom(h) - BtnH * 2f - BtnGap * 2f;
            float right = ColumnX(w) + ColumnW;
            x = inward ? (right - BtnW) : (right - BtnW * 2f - BtnGap);
        }

        /// <summary>
        /// The pan/centre cluster. dx,dy of (0,0) is the centre button; the four arrows are the unit
        /// steps around it. One function for all five so the cluster cannot come apart.
        /// </summary>
        public static void PadRect(int w, int h, int dx, int dy, out float x, out float y,
                                   out float rw, out float rh)
        {
            rw = BtnW; rh = BtnH;
            float cx = ColumnX(w) + ColumnW * 0.5f - BtnW * 0.5f;
            float cy = ColumnBottom(h) - BtnH * 3f - BtnGap * 4f - BtnH;
            x = cx + dx * (BtnW + BtnGap);
            y = cy + dy * (BtnH + BtnGap);
        }

        public static PageHit HitTest(float px, float py, int w, int h)
        {
            float x, y, rw, rh;

            NextViewRect(w, h, out x, out y, out rw, out rh);
            if (Control.Hit(px, py, x, y, rw, rh)) return PageHit.Of(PageAct.NavNextView);

            ZoomRect(w, h, true, out x, out y, out rw, out rh);
            if (Control.Hit(px, py, x, y, rw, rh)) return PageHit.Of(PageAct.NavZoomIn);
            ZoomRect(w, h, false, out x, out y, out rw, out rh);
            if (Control.Hit(px, py, x, y, rw, rh)) return PageHit.Of(PageAct.NavZoomOut);

            PadRect(w, h, 0, 0, out x, out y, out rw, out rh);
            if (Control.Hit(px, py, x, y, rw, rh)) return PageHit.Of(PageAct.NavCentre);
            PadRect(w, h, 0, -1, out x, out y, out rw, out rh);
            if (Control.Hit(px, py, x, y, rw, rh)) return PageHit.Of(PageAct.NavPanUp);
            PadRect(w, h, 0, 1, out x, out y, out rw, out rh);
            if (Control.Hit(px, py, x, y, rw, rh)) return PageHit.Of(PageAct.NavPanDown);
            PadRect(w, h, -1, 0, out x, out y, out rw, out rh);
            if (Control.Hit(px, py, x, y, rw, rh)) return PageHit.Of(PageAct.NavPanLeft);
            PadRect(w, h, 1, 0, out x, out y, out rw, out rh);
            if (Control.Hit(px, py, x, y, rw, rh)) return PageHit.Of(PageAct.NavPanRight);

            return PageHit.None;
        }

        public static void Build(DisplayList dl, int w, int h, PageState s, MapView view)
        {
            float mx, my, mw, mh;
            MapRect(w, h, out mx, out my, out mw, out mh);

            string title = (view.Mode == NavMode.Map) ? "GROUND TRACK"
                         : (view.Mode == NavMode.Orbit) ? "ORBIT" : "3D PLANET";
            dl.Text(title, Pad, HeaderY, Typography.Body, TextAlign.Left, DragonPalette.Text1);

            // The demo labels the current view `CAMERA / Auto - Map IO`; ours says which of the three
            // views is up and, on the map, whether it is following the vehicle, because that is the
            // state a crew member would otherwise have to work out by watching whether it drifts.
            // ⛔ THE PLANET SUB-LABEL IS CONDITIONAL, AND THAT IS THE POINT (S10a).
            // It used to say LIVE CAMERA unconditionally, which was a claim the page could not back:
            // there is no scaled-space camera in the build yet (S10b), so what the crew was looking at
            // was the textured disc. It now says so, and goes back to LIVE CAMERA the moment one is
            // actually rendering - see PlanetGeom.NoSignalLabel for the rest of the marking.
            string sub = (view.Mode == NavMode.Map)
                ? (view.Follow ? "TRACKING VEHICLE" : "MANUAL PAN")
                : (view.Mode == NavMode.Orbit) ? "PLANE VIEW"
                : (s.PlanetCamLive ? "LIVE CAMERA" : "GLOBE + ORBIT");
            dl.Text(sub, mx + mw, HeaderY, Typography.Caption, TextAlign.Right, DragonPalette.Text6);

            // The well the map sits in. Drawn first and always, so the panel has a shape even before
            // anything resolves inside it.
            dl.Rect(mx, my, mw, mh, DragonPalette.Inset2);

            if (view.Mode == NavMode.Map) Map(dl, s, view, mx, my, mw, mh);
            else if (view.Mode == NavMode.Orbit) Orbit(dl, s, mx, my, mw, mh);
            else Planet(dl, s, view, mx, my, mw, mh, true);   // NAV owns the live 3D view (S10)

            dl.Box(mx, my, mw, mh, 2f, DragonPalette.Hairline);

            Column(dl, w, h, s, view);
            Controls(dl, w, h, view);
        }

        // ------------------------------------------------------------------ the map view

        // Public for the same reason Planet is: the Figma Cover page's camera has a 2D MAP view
        // (First.vue's `view-01` / NavEarth), and a second flat-map renderer over there would drift
        // from this one the first time either was touched. One map, two pages.
        public static void Map(DisplayList dl, PageState s, MapView view,
                               float mx, float my, float mw, float mh)
        {
            // ---- the body itself ----
            MapQuad a, b;
            int quads = MapProjection.BodyQuads(view, mx, my, mw, mh, out a, out b);
            if (quads > 0) Quad(dl, a);
            if (quads > 1) Quad(dl, b);

            // ---- graticule ----
            // Every 30 degrees, with the equator and the prime meridian brighter. Drawn OVER the
            // texture so it survives when the texture is dark, and drawn at all so the page still
            // means something when there is no texture.
            //
            // COLOURS ARE Hairline / Text5, NOT the inset greys. The first version used Inset1 on an
            // Inset2 well - two near-blacks one step apart - and the grid was invisible in the
            // preview. On a bright daylit map it would have been invisible the other way. A grid line
            // has to beat both the darkest and the brightest thing it can lie on top of.
            // ---- MERIDIANS SPAN THE MAP, NOT THE WELL ----
            // The body is letterboxed inside the well whenever their aspects differ, and they always
            // do: an equirectangular map is 2:1 and this panel is about 1.68:1, so there is an empty
            // band above and below. Meridians drawn over the full well height ran on past the poles
            // into that emptiness - visible in the 05:42 capture. Bound them to +/-90 instead, which
            // also keeps them honest at zoom, where the poles can be off-panel entirely.
            float gx, gTop, gBot;
            MapProjection.Project(90.0, 0.0, view, mx, my, mw, mh, out gx, out gTop);
            MapProjection.Project(-90.0, 0.0, view, mx, my, mw, mh, out gx, out gBot);
            if (gTop < my) gTop = my;
            if (gBot > my + mh) gBot = my + mh;
            float gh = gBot - gTop;

            if (gh > 0f)
                for (int lon = -180; lon <= 180; lon += 30)
                {
                    float px, py;
                    MapProjection.Project(0.0, lon, view, mx, my, mw, mh, out px, out py);
                    if (px < mx || px > mx + mw) continue;
                    dl.Rect(px, gTop, 1f, gh,
                            (lon == 0) ? DragonPalette.Text5 : DragonPalette.Hairline);
                }
            for (int lat = -60; lat <= 60; lat += 30)
            {
                float px, py;
                MapProjection.Project(lat, 0.0, view, mx, my, mw, mh, out px, out py);
                if (py < my || py > my + mh) continue;
                dl.Rect(mx, py, mw, 1f, (lat == 0) ? DragonPalette.Text5 : DragonPalette.Hairline);
            }

            if (!s.Valid) return;

            // ---- the ground track ----
            // Points come from the glue, which propagates the orbit and takes the body's rotation off
            // the longitude - see VesselData.GroundTrack. Everything here does is connect them.
            if (s.TrackCount > 1 && s.TrackLat != null && s.TrackLon != null)
            {
                int n = s.TrackCount;
                if (n > s.TrackLat.Length) n = s.TrackLat.Length;

                // ---- SOLID LINE, SEAM-AWARE ----
                // Consecutive samples are joined by a real line (user 2026-08-27: solid, not dotted).
                // The one trap is the DATELINE: when the track crosses +/-180 the two samples land on
                // opposite edges of an equirectangular map, and a straight line between them would
                // streak clean across the panel. So a segment wider than half the map is dropped - the
                // track simply breaks at the seam, which is the honest thing an unwrapped map can show.
                float lineW = 2.5f + view.ZoomStep * 0.4f;
                float seam = mw * 0.5f;
                float pax, pay;
                MapProjection.Project(s.TrackLat[0], s.TrackLon[0], view, mx, my, mw, mh, out pax, out pay);
                for (int i = 1; i < n; i++)
                {
                    float pbx, pby;
                    MapProjection.Project(s.TrackLat[i], s.TrackLon[i], view, mx, my, mw, mh, out pbx, out pby);
                    // The track FADES along its length, so which way the vehicle is going is legible
                    // without an arrowhead. Near samples bright, far samples dim.
                    Rgba c = (i < n / 3) ? DragonPalette.Accent
                           : (i < (2 * n) / 3) ? DragonPalette.AccentDim
                           : DragonPalette.Text7;
                    // Skip the dateline wrap (a segment wider than half the map), then CLIP to the panel
                    // so a zoomed-in track never streaks across the readout column - there is no scissor.
                    if (System.Math.Abs(pbx - pax) <= seam)
                    {
                        float ax = pax, ay = pay, bx = pbx, by = pby;
                        if (ClipSeg(ref ax, ref ay, ref bx, ref by, mx, my, mx + mw, my + mh))
                            dl.Line(ax, ay, bx, by, lineW, c);
                    }
                    pax = pbx; pay = pby;
                }
            }

            // ---- the target on the ground ----
            if (s.HasTargetGround)
            {
                float px, py;
                MapProjection.Project(s.TargetLat, s.TargetLon, view, mx, my, mw, mh, out px, out py);
                if (MapProjection.Inside(px, py, mx, my, mw, mh))
                {
                    dl.Box(px - 7f, py - 7f, 14f, 14f, 2f, DragonPalette.Caution);
                    dl.Text("TGT", px, py + 10f, Typography.Dense, TextAlign.Centre,
                            DragonPalette.Caution);
                }
            }

            // ---- us ----
            // Last, so nothing can be drawn over the one marker that must always be findable.
            if (s.HasFix)
            {
                float px, py;
                MapProjection.Project(s.Latitude, s.Longitude, view, mx, my, mw, mh, out px, out py);
                if (MapProjection.Inside(px, py, mx, my, mw, mh))
                {
                    dl.Rect(px - 9f, py - 1f, 18f, 2f, DragonPalette.Go);
                    dl.Rect(px - 1f, py - 9f, 2f, 18f, DragonPalette.Go);
                    dl.Box(px - 5f, py - 5f, 10f, 10f, 2f, DragonPalette.Go);
                }
            }
        }

        private static void Quad(DisplayList dl, MapQuad q)
        {
            if (q.W <= 0f || q.H <= 0f) return;
            // ⛔ U SWAPPED (user 2026-08-27: the flat map read as a MIRROR image of a real map). KSP's scaled-
            // space _ColorMap is wound east-west opposite to our lon→U assumption, so sampling it straight drew
            // the continents mirrored while the grid/track (MapProjection, east-on-the-right) stayed correct.
            // Swapping uMin/uMax reverses the texture horizontally per quad → the land un-mirrors and lines up
            // with the projection. (V/latitude is unchanged, so it does NOT re-mirror.)
            // ⛔ BRIGHTENED (user: the map looked dark). The scaled-space _ColorMap is the DAY albedo (not the
            // EVE night map), but a realistic albedo reads dark; the >1 tint multiplies it brighter (GL.Color
            // multiplies the texture, so dark texels lift while already-bright ones just clamp at white).
            dl.ImageUV(ImageId.BodyMap, q.X, q.Y, q.W, q.H,
                       q.UMax, q.UMin, q.VMin, q.VMax, MapTint);
        }

        // Brighten the dark real-Earth albedo for the nav display (see Quad).
        private static readonly Rgba MapTint = new Rgba(1.7f, 1.7f, 1.7f, 1f);

        // ------------------------------------------------------------------ the 3D globe view

        /// <summary>Stroke width of the projected orbit line, in panel pixels.</summary>
        private const float GlobeLineW = 2.5f;

        /// <summary>
        /// The globe's radius as a fraction of the SMALLER side of the map well, at zoom 0.
        ///
        /// ⛔ NOT A FREE NUMBER ANY MORE (S10a). It is PlanetGeom.DiscFillOfHalfHeight halved -
        /// the same 0.44 it always was, now stated once. The live scaled-space camera solves its
        /// DISTANCE from that same fill fraction (PlanetGeom.Distance), so the rendered globe and this
        /// textured disc put their limbs in exactly the same place and the view does not jump when a
        /// camera appears behind it. Change it here and the camera follows; change it there and this
        /// follows. That only holds while the well is wider than it is tall, so the smaller side is
        /// the height the camera's vertical field of view is measured against - asserted in PageTest.
        /// </summary>
        private const float GlobeDiscFrac = (float)(PlanetGeom.DiscFillOfHalfHeight * 0.5);

        /// <summary>
        /// The 3D globe view: the SAME textured, correctly-aligned Earth the ORBIT view draws
        /// (NavPage.Globe, from the game's own scaled-space map), with the orbit, target orbit and
        /// markers projected ONTO it by the pure orthographic GlobeProjection - so the far side of the
        /// orbit disappears behind the planet. No render-to-texture, no downloaded model: it is all the
        /// textured disc plus 2D drawing, so it works in the PNG preview too.
        ///
        /// The globe follows the vehicle's longitude (left/right pan spins it; +/- zooms the globe);
        /// the overlay is placed against the SAME centre longitude and pixel radius, so it cannot drift.
        /// </summary>
        // Public so the Figma-built pages (CoverPage) can reuse the exact same live globe + orbit
        // overlay inside their own layout, instead of a second globe renderer that could drift.
        public static void Planet(DisplayList dl, PageState s, MapView view,
                                   float mx, float my, float mw, float mh)
        { Planet(dl, s, view, mx, my, mw, mh, false); }

        /// <summary>
        /// As Planet, but able to show the LIVE scaled-space render (S10, MAP_MFD_RESEARCH §2).
        ///
        /// <paramref name="live"/> asks for the RT camera feed; NAV's own 3D PLANET view passes true,
        /// the Cover globe and the Manual Chute globe deliberately do NOT - they are small decorative
        /// slots showing the body, not a camera view, and putting a camera feed and a no-signal notice
        /// into them would change two finished pages for nothing.
        ///
        /// Even with live asked for, the render only appears when one EXISTS (s.PlanetCamLive). It
        /// does not today: the camera is S10b. Until then this draws the textured disc and the
        /// projected orbit - which are both real - and Planet() marks them as not being the live
        /// render. See PlanetGeom.NoSignalLabel.
        /// </summary>
        public static void Planet(DisplayList dl, PageState s, MapView view,
                                   float mx, float my, float mw, float mh, bool live)
        {
            PlanetBody(dl, s, view, mx, my, mw, mh, live);

            // Drawn LAST and outside PlanetBody so it survives every early return in there - a state
            // with no orbit to plot is exactly the state where an unmarked disc would read as a feed.
            if (live && !s.PlanetCamLive) NoSignalMark(dl, mx, my);
        }

        /// <summary>The globe and its overlay. Split from Planet only so the no-signal marking cannot
        /// be skipped by an early return - see Planet.</summary>
        private static void PlanetBody(DisplayList dl, PageState s, MapView view,
                                       float mx, float my, float mw, float mh, bool live)
        {
            float cx = mx + mw * 0.5f, cy = my + mh * 0.5f;
            float r = System.Math.Min(mw, mh) * GlobeDiscFrac
                      * (float)System.Math.Pow(PlanetGeom.ZoomBase, view.PlanetZoom);
            if (r < 24f) r = 24f;

            // Follow the vehicle's longitude, plus the crew's manual spin.
            double lonCentre = (s.Valid && s.HasFix ? s.Longitude : 0.0) + view.PlanetRotDeg;

            // ---- the globe: the live render if there is one, else the textured disc ----
            // The feed FILLS THE WELL, the way every other camera view on these screens does
            // (DockingPage draws its cam over the whole panel). Its globe still lands on the disc's
            // limb, because PlanetGeom solved the camera distance from the same fill fraction the disc
            // is sized by - so the orbit overlay below is projected against one radius either way.
            if (live && s.PlanetCamLive)
                dl.Image(ImageId.ScaledPlanetLive, mx, my, mw, mh, DragonPalette.White);
            else
                Globe(dl, cx, cy, r, lonCentre);   // the real Earth - the same disc the ORBIT view uses

            PlanetOverlay ov = s.Planet;
            if (ov == null || !ov.Ready)
            {
                if (!s.Valid)
                    dl.Text("NO DATA", cx, my + mh - 26f, Typography.Caption, TextAlign.Centre,
                            DragonPalette.Text7);
                return;
            }

            // ---- the target orbit, first so our own orbit and markers sit over it ----
            if (ov.TgtCount > 1)
                ProjPolyline(dl, ov.TgtLat, ov.TgtLon, ov.TgtRatio, ov.TgtCount, lonCentre,
                             cx, cy, r, mx, my, mw, mh,
                             DragonPalette.Caution, DragonPalette.Caution, DragonPalette.Caution, GlobeLineW);

            // ---- our orbit, fading along its length so the direction of travel reads (but kept cyan
            // and visible against the darker real Earth - the far tail is AccentDim, not a grey) ----
            if (ov.OrbitCount > 1)
                ProjPolyline(dl, ov.OrbitLat, ov.OrbitLon, ov.OrbitRatio, ov.OrbitCount, lonCentre,
                             cx, cy, r, mx, my, mw, mh,
                             DragonPalette.Accent, DragonPalette.AccentDim, DragonPalette.AccentDim, GlobeLineW);

            if (ov.OnSurface)
                dl.Text("ON SURFACE - NO ORBIT", cx, my + mh - 26f, Typography.Caption,
                        TextAlign.Centre, DragonPalette.Text6);

            // ---- markers ----
            float sx, sy;
            if (ProjMarker(ov.Ap, lonCentre, cx, cy, r, mx, my, mw, mh, out sx, out sy))
            {
                dl.Box(sx - 5f, sy - 5f, 10f, 10f, 2f, DragonPalette.Text3);
                dl.Text("AP", sx, sy + 10f, Typography.Dense, TextAlign.Centre, DragonPalette.Text5);
            }
            if (ProjMarker(ov.Pe, lonCentre, cx, cy, r, mx, my, mw, mh, out sx, out sy))
            {
                dl.Box(sx - 5f, sy - 5f, 10f, 10f, 2f, DragonPalette.Text3);
                dl.Text("PE", sx, sy + 10f, Typography.Dense, TextAlign.Centre, DragonPalette.Text5);
            }
            if (ProjMarker(ov.Target, lonCentre, cx, cy, r, mx, my, mw, mh, out sx, out sy))
            {
                dl.Box(sx - 7f, sy - 7f, 14f, 14f, 2f, DragonPalette.Caution);
                dl.Text("TGT", sx, sy + 10f, Typography.Dense, TextAlign.Centre, DragonPalette.Caution);
            }
            // Us last, so nothing is drawn over the one marker that must always be findable.
            if (ProjMarker(ov.Vessel, lonCentre, cx, cy, r, mx, my, mw, mh, out sx, out sy))
            {
                dl.Rect(sx - 9f, sy - 1f, 18f, 2f, DragonPalette.Go);
                dl.Rect(sx - 1f, sy - 9f, 2f, 18f, DragonPalette.Go);
                dl.Box(sx - 5f, sy - 5f, 10f, 10f, 2f, DragonPalette.Go);
            }
        }

        /// <summary>
        /// The honest "this is not the live render" marking, in the well's top-left corner - the one
        /// part of the well the centred disc never reaches, and clear of the ON SURFACE / NO DATA
        /// notices at the bottom.
        ///
        /// Caution amber, not alarm red: nothing has failed. The scaled-space camera simply is not
        /// built yet, and what is on the glass beneath this - the body's own texture, the real orbit,
        /// the real markers - is correct. That is §14.4(e)'s marked stand-in, and §14.4(a)'s "no red
        /// for something that is not a fault". The wording lives in PlanetGeom so the label naming
        /// S10b cannot drift from the code that would clear it.
        /// </summary>
        private static void NoSignalMark(DisplayList dl, float mx, float my)
        {
            dl.Text(PlanetGeom.NoSignalLabel, mx + 12f, my + 10f, Typography.Caption,
                    TextAlign.Left, DragonPalette.Caution);
            dl.Text(PlanetGeom.NoSignalDetail, mx + 12f, my + 30f, Typography.Dense,
                    TextAlign.Left, DragonPalette.Text6);
        }

        /// <summary>
        /// Project a (lat, lon, ratio) polyline onto the globe and draw it as SOLID line segments,
        /// clipped to the panel. A segment is drawn only when BOTH endpoints are visible - a point the
        /// globe hides (its far side) breaks the line, so the orbit correctly vanishes behind the
        /// planet. The three colours fade near -> far along the array to show the direction of travel.
        /// </summary>
        private static void ProjPolyline(DisplayList dl, double[] lat, double[] lon, double[] ratio,
                                         int n, double lonCentre, float cx, float cy, float r,
                                         float mx, float my, float mw, float mh,
                                         Rgba near, Rgba mid, Rgba far, float width)
        {
            if (lat == null || lon == null || ratio == null) return;
            if (n > lat.Length) n = lat.Length;
            float pax = 0f, pay = 0f; bool pvis = false;
            for (int i = 0; i < n; i++)
            {
                float x, y; bool front, occ;
                GlobeProjection.Project(lat[i], lon[i], ratio[i], lonCentre, cx, cy, r,
                                        out x, out y, out front, out occ);
                bool vis = !occ;
                if (i > 0 && vis && pvis)
                {
                    float ax = pax, ay = pay, bx = x, by = y;
                    if (ClipSeg(ref ax, ref ay, ref bx, ref by, mx, my, mx + mw, my + mh))
                    {
                        Rgba c = (i < n / 3) ? near : (i < (2 * n) / 3) ? mid : far;
                        dl.Line(ax, ay, bx, by, width, c);
                    }
                }
                pax = x; pay = y; pvis = vis;
            }
        }

        /// <summary>Project one marker onto the globe. False when it has no fix, is hidden behind the
        /// globe, or falls outside the panel.</summary>
        private static bool ProjMarker(GlobePoint p, double lonCentre, float cx, float cy, float r,
                                       float mx, float my, float mw, float mh, out float sx, out float sy)
        {
            sx = sy = 0f;
            if (!p.Has) return false;
            bool front, occ;
            GlobeProjection.Project(p.Lat, p.Lon, p.Ratio, lonCentre, cx, cy, r, out sx, out sy, out front, out occ);
            if (occ) return false;
            return sx >= mx && sx <= mx + mw && sy >= my && sy <= my + mh;
        }

        /// <summary>
        /// Clip a segment to a rectangle (Liang-Barsky), allocation-free. Returns false if the segment
        /// misses the rect entirely; otherwise trims the endpoints in place to the rect boundary. Used
        /// to keep the flat-map ground track inside its panel, since neither renderer has a scissor.
        /// </summary>
        private static bool ClipSeg(ref float x0, ref float y0, ref float x1, ref float y1,
                                    float xmin, float ymin, float xmax, float ymax)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float u0 = 0f, u1 = 1f;
            if (!ClipT(-dx, x0 - xmin, ref u0, ref u1)) return false;
            if (!ClipT( dx, xmax - x0, ref u0, ref u1)) return false;
            if (!ClipT(-dy, y0 - ymin, ref u0, ref u1)) return false;
            if (!ClipT( dy, ymax - y0, ref u0, ref u1)) return false;
            float ox = x0, oy = y0;
            x0 = ox + u0 * dx; y0 = oy + u0 * dy;
            x1 = ox + u1 * dx; y1 = oy + u1 * dy;
            return true;
        }

        private static bool ClipT(float p, float q, ref float u0, ref float u1)
        {
            if (System.Math.Abs(p) < 1e-6f) return q >= 0f;      // parallel: in only if on the inside
            float t = q / p;
            if (p < 0f) { if (t > u1) return false; if (t > u0) u0 = t; }
            else        { if (t < u0) return false; if (t < u1) u1 = t; }
            return true;
        }

        // ------------------------------------------------------------------ the orbit view

        /// <summary>
        /// The orbit drawn side-on: the body as a disc, the orbit as a dotted ellipse, with apoapsis,
        /// periapsis and the vehicle marked.
        ///
        /// Geometry is the conic itself - r = a(1-e^2)/(1+e cos v) - from apogee and perigee, which
        /// are numbers the page already has and already trusts. The vehicle's position comes from its
        /// CURRENT RADIUS solved back through the same equation rather than from an anomaly supplied
        /// by the engine: it needs no unit convention to be agreed with KSP, and it cannot disagree
        /// with the apogee and perigee printed beside it.
        /// </summary>
        public static void Orbit(DisplayList dl, PageState s, float mx, float my, float mw, float mh)
        { Orbit(dl, s, mx, my, mw, mh, false); }

        /// <summary>
        /// As Orbit, plus an optional approach chord from the vehicle to periapsis (T6's rendezvous
        /// ellipse plot). Public for the same reason Planet/Map are: one conic calculation, reused
        /// by the plain NAV page (chord off) and RendezvousPage (chord on), not two that could drift.
        /// </summary>
        public static void Orbit(DisplayList dl, PageState s, float mx, float my, float mw, float mh,
                                 bool showApproachChord)
        {
            float cx = mx + mw * 0.5f, cy = my + mh * 0.5f;

            if (!s.Valid || s.BodyRadiusM <= 0.0)
            {
                dl.Text("NO ORBIT", cx, cy - 10f, Typography.Body, TextAlign.Centre,
                        DragonPalette.Text7);
                return;
            }

            double rA = s.BodyRadiusM + s.ApogeeM;
            double rP = s.BodyRadiusM + s.PerigeeM;
            if (rP < 0.0) rP = 0.0;
            if (rA <= 0.0) { dl.Text("NO ORBIT", cx, cy, Typography.Body, TextAlign.Centre,
                                     DragonPalette.Text7); return; }

            double aAxis = (rA + rP) * 0.5;
            double ecc = (aAxis > 0.0) ? (rA - rP) / (rA + rP) : 0.0;
            if (ecc < 0.0) ecc = 0.0;
            if (ecc > 0.98) ecc = 0.98;      // a hyperbola is not this plot's problem

            // Fit the WHOLE orbit in the panel with a margin, and let the body scale with it. A fixed
            // body size with a scaled orbit looks right in low orbit and absurd on a transfer.
            //
            // ---- THE BODY IS PART OF THE EXTENT, AND LEAVING IT OUT OVERFLOWED THE PANEL ----
            // On the pad the "orbit" is the degenerate ellipse that also makes periapsis read
            // -598.4 km: apoapsis at the surface, periapsis at the centre of the planet. That gives a
            // very flat ellipse, so scaling to its minor axis blew the globe up to 790 px inside a
            // 520 px panel - seen in game 2026-08-06. Taking the max with the body radius means the
            // planet always fits, whatever the orbit is doing.
            double halfMinor = aAxis * System.Math.Sqrt(1.0 - ecc * ecc);
            double extentX = System.Math.Max(aAxis * (1.0 + ecc), s.BodyRadiusM);
            double extentY = System.Math.Max(halfMinor, s.BodyRadiusM);
            if (extentX <= 0.0 || extentY <= 0.0) return;

            float scale = (float)System.Math.Min((mw * 0.42f) / extentX, (mh * 0.42f) / extentY);
            if (scale <= 0f) return;

            // Focus at the panel centre: the body sits at the focus, periapsis to the RIGHT.
            Globe(dl, cx, cy, (float)(s.BodyRadiusM * scale), s.Longitude);

            // ---- ON THE GROUND THERE IS NO ORBIT TO PLOT ----
            // Same rule as the dashed apogee and perigee, and the same reason: the conic through a
            // landed vessel is a real solution to the maths and a meaningless thing to draw. Show the
            // planet, say so, and draw no ellipse rather than a confident wrong one.
            if (!s.ApogeeShown)
            {
                dl.Text("ON SURFACE - NO ORBIT", cx, my + mh - 34f, Typography.Caption,
                        TextAlign.Centre, DragonPalette.Text6);
                return;
            }

            // ---- THE CONIC IS CLIPPED AT THE SURFACE, AND MOST OF A FLIGHT NEEDS THAT (S41) ----
            // Everything above assumed the trajectory CLOSES. On the 2026-09-03 orbit flight it did
            // not for the first several minutes - the ascent's periapsis was thousands of kilometres
            // below the surface, which is what every ascent looks like until the circularisation
            // burn - and this plot drew the whole ellipse anyway. The preview of that state (see
            // page2_nav_orbit_suborbital.png, rendered BEFORE this fix) is unambiguous: a dotted
            // ellipse painted entirely INSIDE the globe, a PE box sitting near the planet's core,
            // and the vehicle - which is 148 km above the ground - drawn underneath its own planet.
            // A closed ellipse there is not a rounding error; it is a picture of a place the vehicle
            // cannot be. The SAME defect covers a deorbit, where periapsis is deliberately negative.
            //
            // So the arc drawn is the part of the conic at or above the surface, and nothing else.
            // r(nu) >= R exactly when cos(nu) <= (p/R - 1)/e, with p = a(1-e^2) the semi-latus
            // rectum; that threshold is the whole rule:
            //   >= +1          the periapsis itself clears the surface -> it closes, draw all 360
            //   in (-1, +1)    an OPEN arc through apoapsis, from +nuSurf round to -nuSurf
            //   <= -1          no part of the conic clears the surface -> there is no arc to draw
            // The globe is drawn from the SAME radius at the SAME focus, so the arc's ends meet the
            // limb exactly rather than approximately, and the picture cannot disagree with itself.
            double semiLatus = aAxis * (1.0 - ecc * ecc);
            double cosNuSurf;
            if (ecc < 1e-9) cosNuSurf = (aAxis >= s.BodyRadiusM) ? 1.0 : -1.0;   // circular: all or none
            else cosNuSurf = (semiLatus / s.BodyRadiusM - 1.0) / ecc;

            bool closed = cosNuSurf >= 1.0;
            bool anyArc = cosNuSurf > -1.0;

            const int Steps = 72;
            if (closed)
            {
                for (int i = 0; i < Steps; i++)
                {
                    double nu = (i * 360.0 / Steps) * System.Math.PI / 180.0;
                    double r = aAxis * (1.0 - ecc * ecc) / (1.0 + ecc * System.Math.Cos(nu));
                    float px = cx + (float)(r * System.Math.Cos(nu)) * scale;
                    float py = cy - (float)(r * System.Math.Sin(nu)) * scale;
                    dl.Rect(px - 1.5f, py - 1.5f, 3f, 3f, DragonPalette.AccentDim);
                }
            }
            else if (anyArc)
            {
                // Both ends included, so the arc visibly TOUCHES the limb it is cut off by.
                double nuSurf = System.Math.Acos(cosNuSurf);
                double span = 2.0 * (System.Math.PI - nuSurf);
                for (int i = 0; i <= Steps; i++)
                {
                    double nu = nuSurf + span * i / Steps;
                    double r = aAxis * (1.0 - ecc * ecc) / (1.0 + ecc * System.Math.Cos(nu));
                    float px = cx + (float)(r * System.Math.Cos(nu)) * scale;
                    float py = cy - (float)(r * System.Math.Sin(nu)) * scale;
                    dl.Rect(px - 1.5f, py - 1.5f, 3f, 3f, DragonPalette.AccentDim);
                }
            }

            // Apsides. Periapsis is true anomaly 0 by definition, apoapsis 180 - no search needed.
            //
            // PE IS DRAWN ONLY WHERE PE EXISTS. On an open trajectory the periapsis is under the
            // ground, and a box labelled PE inside the planet is the same lie in miniature as the
            // buried ellipse was. It is dropped, not moved and not relabelled - the readout column
            // beside the plot already dashes PERIGEE in this state for its own reason (OrbitReadout
            // rejects a periapsis that far below the surface as the body-radius artefact it is), so
            // the page says one thing twice rather than two different things. On a DEORBIT the
            // number is meaningful and is shown, and the marker is still absent, because the point
            // is still underground: the number is a target, the marker would be a location.
            float pxA = cx - (float)(rA * scale);
            if (closed)
            {
                float pxP = cx + (float)(rP * scale);
                dl.Box(pxP - 5f, cy - 5f, 10f, 10f, 2f, DragonPalette.Text3);
                dl.Text("PE", pxP, cy + 10f, Typography.Dense, TextAlign.Centre, DragonPalette.Text5);
            }
            if (anyArc)
            {
                dl.Box(pxA - 5f, cy - 5f, 10f, 10f, 2f, DragonPalette.Text3);
                dl.Text("AP", pxA, cy + 10f, Typography.Dense, TextAlign.Centre, DragonPalette.Text5);
            }

            // ---- SAY WHY THE LINE STOPS ----
            // An arc that simply ends could be a drawing fault. Caption it, in the same slot and the
            // same weight as ON SURFACE - NO ORBIT, and in wording that is true of an ascent and a
            // deorbit alike: no phase is claimed, only the geometry that is on the screen.
            if (!closed)
            {
                dl.Text(anyArc ? "TRAJECTORY INTERSECTS SURFACE" : "NO TRAJECTORY ABOVE SURFACE",
                        cx, my + mh - 34f, Typography.Caption, TextAlign.Centre, DragonPalette.Text6);
            }

            // Us, from the current radius. Ascending puts us on the near half, descending the far one.
            // Skipped when there is no arc at all: with nothing above the surface to sit on, the tick
            // would land wherever the clamped acos happened to fall, which is a guess wearing a marker.
            if (!anyArc) return;
            double rNow = s.BodyRadiusM + s.AltitudeM;
            double nuNow = OrbitPlot.TrueAnomaly(rNow, aAxis, ecc, s.Ascending);
            float vx = cx + (float)(rNow * System.Math.Cos(nuNow)) * scale;
            float vy = cy - (float)(rNow * System.Math.Sin(nuNow)) * scale;
            dl.Rect(vx - 9f, vy - 1f, 18f, 2f, DragonPalette.Go);
            dl.Rect(vx - 1f, vy - 9f, 2f, 18f, DragonPalette.Go);

            // ---- approach chord (T6, made target-relative by T13c) ----
            // Only drawn with a target actually set, so the plain NAV view (chord always off) and an
            // idle rendezvous plot never grow an unexplained line.
            //
            // T6 ran it to PERIAPSIS and said why: the target's own orbital state was not in PageState,
            // so the chord pointed at the next real apsis as a stated stand-in. It is in PageState now
            // (HasTargetOrbit / TargetRadiusM / TargetPhaseRad, see VesselData.TargetPlot), so the chord
            // runs to WHERE THE TARGET ACTUALLY IS. The phase angle is measured from US, so the far end
            // is placed relative to the same marker the near end is drawn at - the two cannot disagree
            // however the conic came out. Endpoint marked with a small diamond in the chord's own
            // colour: a line has to end somewhere identifiable, and no label is invented for it.
            //
            // A target with no orbit around our body (another SOI, or a body itself) leaves
            // HasTargetOrbit false and draws NO chord - the plot says nothing rather than something
            // it had to make up.
            if (showApproachChord && s.HasTarget && s.HasTargetOrbit && s.TargetRadiusM > 0.0)
            {
                double nuT = nuNow + s.TargetPhaseRad;
                float tx = cx + (float)(s.TargetRadiusM * System.Math.Cos(nuT)) * scale;
                float ty = cy - (float)(s.TargetRadiusM * System.Math.Sin(nuT)) * scale;
                dl.Line(vx, vy, tx, ty, 2f, DragonPalette.Caution);
                const float d = 7f;
                dl.Line(tx, ty - d, tx + d, ty, 2f, DragonPalette.Caution);
                dl.Line(tx + d, ty, tx, ty + d, 2f, DragonPalette.Caution);
                dl.Line(tx, ty + d, tx - d, ty, 2f, DragonPalette.Caution);
                dl.Line(tx - d, ty, tx, ty - d, 2f, DragonPalette.Caution);
            }
        }

        /// <summary>
        /// The planet, as a textured disc built from horizontal strips of the real body map.
        ///
        /// ---- WHY THIS REPLACED A PLAIN CIRCLE ----
        /// The first version filled the disc with DragonPalette.Inset1 - a near-black on a near-black
        /// background - so on the glass all that survived was the hairline rim and the user's report
        /// was, correctly, "NAV is missing the globe". Same mistake as the invisible graticule, in the
        /// same page, two days running: a shape drawn in a colour one step from the background is not
        /// a drawn shape.
        ///
        /// Fixing the colour alone was on the table. Drawing the real thing costs about sixty more
        /// commands and we already have a 4096x2048 equirectangular Kerbin sitting in VRAM, so the
        /// globe is the actual planet, correct under Parallax and RSS alike, and it turns as the
        /// vehicle moves because it is centred on the longitude we are currently over.
        ///
        /// ---- HOW, AND WHAT IS APPROXIMATE ABOUT IT ----
        /// Each strip is one quad: its VERTICAL extent maps to a latitude band through the true
        /// orthographic relation (y = -sin(lat) * r), and its half-width is the circle's chord,
        /// sqrt(r^2 - y^2), so the silhouette and every parallel are geometrically right.
        ///
        /// The approximation is HORIZONTAL: inside a strip, screen x maps linearly to longitude,
        /// where orthographic wants sin. Detail near the limb is therefore stretched rather than
        /// compressed. Doing it properly needs each strip subdivided into cells, which is the same
        /// picture for several times the commands. Stated rather than hidden: this reads as a globe,
        /// it is not a projection anyone should measure off. The MAP view is the one for that.
        ///
        /// The base disc is drawn FIRST and unconditionally, so a body whose texture did not resolve
        /// - and the PNG preview, which has no game to ask - still gets a planet rather than a hole.
        /// </summary>
        private static void Globe(DisplayList dl, float cx, float cy, float r, double lonCentre)
        {
            if (r <= 1f) return;

            dl.ArcBand(cx, cy, 0f, r, 0.0, 360.0, DragonPalette.Panel);

            // The visible hemisphere: 180 degrees of longitude centred on where we are. Computed
            // ONCE - every strip shares it, and so does the seam split, which is why this is not the
            // per-strip mess it looks like it should be.
            double uMin = (lonCentre - 90.0 + 180.0) / 360.0;
            while (uMin < 0.0) uMin += 1.0;
            while (uMin >= 1.0) uMin -= 1.0;
            double uMax = uMin + 0.5;
            bool split = uMax > 1.0;
            float splitFrac = split ? (float)((1.0 - uMin) / 0.5) : 1f;

            for (int i = 0; i < GlobeStrips; i++)
            {
                float y0 = cy - r + (2f * r * i) / GlobeStrips;
                float y1 = cy - r + (2f * r * (i + 1)) / GlobeStrips;

                // Latitude at each edge, from the orthographic relation. asin of the clamped offset.
                double latTop = System.Math.Asin(Unit(-(y0 - cy) / r)) * 180.0 / System.Math.PI;
                double latBot = System.Math.Asin(Unit(-(y1 - cy) / r)) * 180.0 / System.Math.PI;
                float vMax = (float)((latTop + 90.0) / 180.0);
                float vMin = (float)((latBot + 90.0) / 180.0);

                // ---- HALF-WIDTH FROM THE EQUATOR-SIDE EDGE, WHICH IS THE ROUNDNESS TRICK ----
                // Taking it at the strip's middle leaves each band alternately too narrow and too
                // wide against the true circle, and the result is the square stair-stepping seen in
                // game on 2026-08-06. Taking the WIDEST edge instead makes every strip cover at
                // least the circle and overhang it by at most one strip height - so the union is the
                // disc plus a ragged fringe, and a fringe can be trimmed. A too-narrow strip cannot
                // be filled back in.
                float yNear = (System.Math.Abs(y0 - cy) < System.Math.Abs(y1 - cy)) ? y0 : y1;
                double d = Unit((yNear - cy) / r);
                float halfW = (float)(r * System.Math.Sqrt(1.0 - d * d));
                if (halfW <= 0.5f) continue;

                // ⛔ NOT SWAPPED, unlike NavPage.Quad — and that is CORRECT, confirmed in the PNG preview
                // (Campaign 4, 2026-08-29). The globe strips assign u to screen in the OPPOSITE order to the
                // flat map's quad: the strip runs uMin(west) on the LEFT to uMax(east) on the RIGHT, which is
                // already the mirror image of Quad's uLeft=UMax draw. So it needs NO swap to read the same
                // way. An earlier pass mirrored this to match Quad on the false assumption the two share a
                // u->screen convention; the preview showed that put India/east on the LEFT and S.America/west
                // on the RIGHT. Left as-is: west-left, east-right, matching the east-on-right GlobeProjection.
                if (!split)
                {
                    dl.ImageUV(ImageId.BodyMap, cx - halfW, y0, halfW * 2f, y1 - y0,
                               (float)uMin, (float)uMax, vMin, vMax, DragonPalette.White);
                }
                else
                {
                    float wA = halfW * 2f * splitFrac;
                    dl.ImageUV(ImageId.BodyMap, cx - halfW, y0, wA, y1 - y0,
                               (float)uMin, 1f, vMin, vMax, DragonPalette.White);
                    dl.ImageUV(ImageId.BodyMap, cx - halfW + wA, y0, halfW * 2f - wA, y1 - y0,
                               0f, (float)(uMax - 1.0), vMin, vMax, DragonPalette.White);
                }
            }

            // ---- TRIM THE FRINGE, AND THE LIMB BECOMES EXACTLY A CIRCLE ----
            // The strips deliberately overhang (see above), by at most one strip height. Painting
            // that fringe out with an annulus in the WELL's own colour leaves the disc bounded by the
            // annulus's INNER edge - and that edge is an arc from ArcGeometry at 2 degrees a segment,
            // i.e. a proper curve, not a stack of quads. So the roundness no longer depends on the
            // strip count at all; the strip count only affects how smoothly the texture flows.
            //
            // It must be Inset2 because that is what NavPage.Build fills the map well with. If the
            // well's colour ever changes, this changes with it - a hard-coded background here would
            // draw a grey ring round the planet and look like a rendering fault.
            float fringe = (2f * r) / GlobeStrips * 1.3f;
            dl.ArcBand(cx, cy, r, r + fringe, 0.0, 360.0, DragonPalette.Inset2);

            // The rim last of all, sitting exactly on the trimmed edge.
            dl.ArcBand(cx, cy, r - 2f, r, 0.0, 360.0, DragonPalette.Hairline);
        }

        /// <summary>Clamp to -1..1 so asin and sqrt cannot be handed a value off the disc.</summary>
        private static double Unit(double v)
        {
            return (v < -1.0) ? -1.0 : (v > 1.0) ? 1.0 : v;
        }

        // ------------------------------------------------------------------ readouts and controls

        private static void Column(DisplayList dl, int w, int h, PageState s, MapView view)
        {
            float x = ColumnX(w);
            float y = MapTop + 4f;
            const float Pitch = 42f;

            if (view.Mode == NavMode.Map)
            {
                Row(dl, x, y + Pitch * 0, "LATITUDE", s.Valid ? s.LatText : "-");
                Row(dl, x, y + Pitch * 1, "LONGITUDE", s.Valid ? s.LonText : "-");
                Row(dl, x, y + Pitch * 2, "ALTITUDE", s.Valid ? s.Altitude : "-");
                Row(dl, x, y + Pitch * 3, "INCLINATION", s.Valid ? s.InclinationText : "-");
                Row(dl, x, y + Pitch * 4, "PERIOD", s.Valid ? s.PeriodText : "-");
                Row(dl, x, y + Pitch * 5, "TARGET LAT",
                    (s.Valid && s.HasTargetGround) ? s.TargetLatText : "-");
                Row(dl, x, y + Pitch * 6, "TARGET LON",
                    (s.Valid && s.HasTargetGround) ? s.TargetLonText : "-");
            }
            else
            {
                Row(dl, x, y + Pitch * 0, "APOGEE",
                    (s.Valid && s.ApogeeShown) ? s.Apoapsis : "-");
                Row(dl, x, y + Pitch * 1, "PERIGEE",
                    (s.Valid && s.PerigeeShown) ? s.Periapsis : "-");
                Row(dl, x, y + Pitch * 2, "ALTITUDE", s.Valid ? s.Altitude : "-");
                Row(dl, x, y + Pitch * 3, "PERIOD", s.Valid ? s.PeriodText : "-");
                Row(dl, x, y + Pitch * 4, "TIME TO AP", s.Valid ? s.TimeToApText : "-");
                Row(dl, x, y + Pitch * 5, "TIME TO PE", s.Valid ? s.TimeToPeText : "-");
                Row(dl, x, y + Pitch * 6, "INCLINATION", s.Valid ? s.InclinationText : "-");
            }
        }

        private static void Row(DisplayList dl, float x, float y, string caption, string value)
        {
            Readouts.Row(dl, x, y, ColumnW, caption, value, null, Typography.Body);
            dl.Rect(x, y + 28f, ColumnW, 1f, DragonPalette.Inset1);
        }

        private static void Controls(DisplayList dl, int w, int h, MapView view)
        {
            float x, y, rw, rh;
            // The pan / zoom / centre cluster drives the flat MAP and the 3D PLANET camera alike (on
            // PLANET, pan swings and tilts the camera, zoom is distance, CTR resets the view); only the
            // side-on ORBIT plot has nothing for them to do, so they read inactive there.
            bool active = (view.Mode == NavMode.Map || view.Mode == NavMode.Planet);

            // Arrows as words rather than glyphs: the font is whatever Windows resolved, and a
            // triangle drawn from three rects is not a triangle. LEFT/RIGHT/UP/DOWN cannot be
            // mis-rendered, and at 16 px they are as fast to read as an arrow would be.
            PadRect(w, h, 0, -1, out x, out y, out rw, out rh);
            Control.Button(dl, x, y, rw, rh, "UP", false, active);
            PadRect(w, h, -1, 0, out x, out y, out rw, out rh);
            Control.Button(dl, x, y, rw, rh, "LEFT", false, active);
            PadRect(w, h, 0, 0, out x, out y, out rw, out rh);
            Control.Button(dl, x, y, rw, rh, "CTR", view.Follow && view.Mode == NavMode.Map, active);
            PadRect(w, h, 1, 0, out x, out y, out rw, out rh);
            Control.Button(dl, x, y, rw, rh, "RIGHT", false, active);
            PadRect(w, h, 0, 1, out x, out y, out rw, out rh);
            Control.Button(dl, x, y, rw, rh, "DOWN", false, active);

            ZoomRect(w, h, false, out x, out y, out rw, out rh);
            Control.Button(dl, x, y, rw, rh, "-", false, active);
            ZoomRect(w, h, true, out x, out y, out rw, out rh);
            Control.Button(dl, x, y, rw, rh, "+", false, active);
            // The PLANET camera zoom is a signed step (distance), not a power-of-two texture window, so
            // it reads as a step number rather than "x2/x4".
            string zoomLabel = (view.Mode == NavMode.Planet)
                ? "ZOOM " + (view.PlanetZoom >= 0 ? "+" : "") + view.PlanetZoom
                : "ZOOM x" + (1 << view.ZoomStep);
            dl.Text(zoomLabel, ColumnX(w), y + 12f, Typography.Caption, TextAlign.Left,
                    DragonPalette.Text6);

            NextViewRect(w, h, out x, out y, out rw, out rh);
            Control.Button(dl, x, y, rw, rh, "NEXT VIEW", false, true);
        }
    }

    /// <summary>
    /// The one bit of conic maths the orbit plot needs, kept apart so it can be tested on its own.
    /// </summary>
    public static class OrbitPlot
    {
        /// <summary>
        /// True anomaly, in radians, for a body at radius r on an orbit of semi-major axis a and
        /// eccentricity e.
        ///
        /// The conic gives cos(v) directly; the SIGN is the half that the equation cannot supply, and
        /// it is exactly the difference between climbing and falling. Hence the ascending flag - a
        /// vertical speed the caller already has, rather than an anomaly whose units would have to be
        /// agreed with the engine.
        /// </summary>
        public static double TrueAnomaly(double r, double a, double e, bool ascending)
        {
            if (a <= 0.0 || r <= 0.0) return 0.0;
            if (e < 1e-6) return 0.0;                 // circular: any point will do, pick periapsis
            double cosv = (a * (1.0 - e * e) / r - 1.0) / e;
            if (cosv > 1.0) cosv = 1.0;
            if (cosv < -1.0) cosv = -1.0;
            double v = System.Math.Acos(cosv);
            return ascending ? v : -v;
        }
    }
}
