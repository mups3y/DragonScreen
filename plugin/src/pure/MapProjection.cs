/*
 * DragonScreen - MapProjection
 *
 * PURE. Equirectangular latitude/longitude -> page pixels, with pan and zoom, and the texture
 * coordinates that put the body's own map behind it.
 *
 * ---- WHY EQUIRECTANGULAR AND NOT A GLOBE ----
 * The live demo's NAV panel is a flat scrolling world map with pan arrows and +/- zoom, not a 3D
 * globe (docs/REFERENCE_PAGES.md, "THE LIVE DEMO", finding 1). It is also the projection KSP's own
 * scaled-space body textures are stored in, so the map can be the REAL body rather than bundled art -
 * which is what makes this work unchanged on Kerbin, on the Mun, and on Earth under RSS.
 *
 * ---- THERE IS NO CLIPPING, SO THE GEOMETRY MUST DO IT ----
 * Neither renderer has a scissor rect in its draw path, and adding one to both is a second thing to
 * keep in step. Instead the visible window is turned into an image quad plus UVs that are EXACTLY the
 * map rect: nothing is drawn outside it because nothing outside it is ever emitted. That is why this
 * file computes UVs at all rather than just positions.
 *
 * ---- SCALE IS ONE NUMBER FOR BOTH AXES ----
 * pixels-per-degree is shared by latitude and longitude, so the map can never be stretched. At zoom
 * step 0 the whole body fits inside the rect and letterboxes; every step doubles the scale. Deriving
 * the two axes independently would fill the rect at zoom 0 and squash every coastline, which is the
 * same class of mistake as the 1.62 aspect that made the proof circle an ellipse.
 *
 * ---- THE SEAM IS REAL AND IS HANDLED HERE ----
 * Panning across +/-180 makes the visible longitude window straddle the edge of the texture. The
 * honest fixes are to set the texture to wrap, or to draw two quads. We do NOT own the scaled-space
 * texture - it is the planet the player is looking at - so changing its wrap mode is out. Two quads
 * it is, and the split is computed here where it can be tested.
 */
namespace DragonScreen
{
    /// <summary>Which NAV view is on screen. Cycled by the NEXT VIEW control, as in the demo.</summary>
    public enum NavMode : byte
    {
        /// <summary>Body map with the ground track. The demo's default.</summary>
        Map = 0,
        /// <summary>Orbit plotted side-on against the body. Frame 67's right-hand panel.</summary>
        Orbit = 1
    }

    /// <summary>
    /// Where the map is looking. Per SCREEN, not per vessel - two crew can be looking at different
    /// parts of the world at once, which is the point of three independent displays.
    ///
    /// Deliberately NOT persisted: a page selection is a decision the crew made, a scroll position is
    /// where they happened to have dragged to. Restoring the second on load would be surprising, and
    /// it is one more thing in the save file that can be malformed.
    /// </summary>
    public struct MapView
    {
        public double CentreLon;
        public double CentreLat;
        /// <summary>0 = whole body. Each step doubles the scale.</summary>
        public int ZoomStep;
        public NavMode Mode;
        /// <summary>True while the map follows the vessel. Panning by hand turns it off.</summary>
        public bool Follow;
    }

    /// <summary>One textured quad of the body map: where it goes and which part of the texture.</summary>
    public struct MapQuad
    {
        public float X, Y, W, H;
        /// <summary>Texture coordinates. VMin is the SOUTH edge - see MapProjection.BodyQuads.</summary>
        public float UMin, UMax, VMin, VMax;
    }

    public static class MapProjection
    {
        public const int MaxZoom = 5;

        /// <summary>Degrees the pan controls move per press, at zoom step 0.</summary>
        private const double PanDegrees = 30.0;

        public static MapView Default()
        {
            MapView v = new MapView();
            v.CentreLon = 0.0;
            v.CentreLat = 0.0;
            v.ZoomStep = 0;
            v.Mode = NavMode.Map;
            v.Follow = true;
            return v;
        }

        /// <summary>
        /// Pixels per degree. One number for both axes, so nothing can be stretched; at zoom 0 the
        /// whole 360 x 180 fits, which is why this is a MIN and not a width-driven scale.
        /// </summary>
        public static float Scale(float rectW, float rectH, int zoomStep)
        {
            if (rectW <= 0f || rectH <= 0f) return 0f;
            float baseScale = rectW / 360f;
            float byHeight = rectH / 180f;
            if (byHeight < baseScale) baseScale = byHeight;
            return baseScale * (float)Pow2(Clamp(zoomStep, 0, MaxZoom));
        }

        /// <summary>Longitude difference folded into -180..180, so the short way round always wins.</summary>
        public static double Wrap180(double deg)
        {
            while (deg > 180.0) deg -= 360.0;
            while (deg < -180.0) deg += 360.0;
            return deg;
        }

        /// <summary>Longitude folded into 0..360, which is the form KSP hands back.</summary>
        public static double Wrap360(double deg)
        {
            while (deg >= 360.0) deg -= 360.0;
            while (deg < 0.0) deg += 360.0;
            return deg;
        }

        /// <summary>
        /// The centre the map is ACTUALLY drawn about, which is not always the one in the view.
        ///
        /// ---- A BUG CAUGHT IN THE PNG PREVIEW, AND IT WOULD HAVE BEEN NASTY IN GAME ----
        /// When the whole world fits inside the panel - which is exactly the default zoom 0 view -
        /// the texture has nowhere to scroll to, so it is drawn centred on the panel whatever the
        /// view says. But the MARKERS were still being placed relative to view.CentreLon, so the
        /// vessel sat in the middle of the panel while the map underneath it showed longitude 0. A
        /// marker confidently drawn over the wrong ocean is worse than no map at all, and nothing
        /// about it looks broken.
        ///
        /// Both the quads and the markers go through here, so they cannot disagree. Same one-source
        /// rule as ChromeBar.LinkRect, applied to a projection instead of a hit box.
        /// </summary>
        public static void EffectiveCentre(MapView view, float rw, float rh,
                                           out double lat, out double lon)
        {
            float ppd = Scale(rw, rh, view.ZoomStep);
            lon = (ppd > 0f && 360f * ppd <= rw) ? 0.0 : view.CentreLon;
            lat = (ppd > 0f && 180f * ppd <= rh) ? 0.0 : view.CentreLat;
        }

        /// <summary>
        /// A point on the body to a page pixel. Always returns a coordinate; whether it is INSIDE
        /// the map rect is a separate question, asked with Inside(), because a caller that wants to
        /// clip a track and a caller that wants to place a label off the edge want different answers.
        /// </summary>
        public static void Project(double lat, double lon, MapView view,
                                   float rx, float ry, float rw, float rh,
                                   out float px, out float py)
        {
            float ppd = Scale(rw, rh, view.ZoomStep);
            double clat, clon;
            EffectiveCentre(view, rw, rh, out clat, out clon);
            float cx = rx + rw * 0.5f, cy = ry + rh * 0.5f;
            px = cx + (float)(Wrap180(lon - clon)) * ppd;
            py = cy - (float)(lat - clat) * ppd;
        }

        public static bool Inside(float px, float py, float rx, float ry, float rw, float rh)
        {
            return px >= rx && px <= rx + rw && py >= ry && py <= ry + rh;
        }

        /// <summary>
        /// The body texture, as one or two quads that exactly fill the visible part of the map rect.
        ///
        /// Returns the number of quads written (0, 1 or 2). Two happens only when the view straddles
        /// the +/-180 seam - see the header for why that is not solved with a wrap mode.
        ///
        /// V CONVENTION: VMin is the SOUTH edge and VMax the NORTH one, matching the texture's own
        /// v = 0 at the bottom. The renderers already map a rect's TOP edge to the larger v (see
        /// ScreenPainter.DrawImage), so this hands them exactly what they expect and neither of them
        /// gets to own a flip.
        /// </summary>
        public static int BodyQuads(MapView view, float rx, float ry, float rw, float rh,
                                    out MapQuad a, out MapQuad b)
        {
            a = new MapQuad(); b = new MapQuad();
            float ppd = Scale(rw, rh, view.ZoomStep);
            if (ppd <= 0f) return 0;

            float cx = rx + rw * 0.5f, cy = ry + rh * 0.5f;

            // The centre the markers will use. See EffectiveCentre - the two must not diverge.
            double centreLat, centreLon;
            EffectiveCentre(view, rw, rh, out centreLat, out centreLon);

            // ---- LATITUDE: clamp to the poles and shrink the quad, do not stretch it ----
            // At zoom 0 on a 1.82:1 rect the visible latitude window is far taller than 180 degrees,
            // so the map letterboxes. Clamping the DATA and deriving the rect from it is what keeps
            // the scale honest; clamping the rect and stretching the UVs would fill the panel and lie
            // about every latitude on it.
            double latTop = centreLat + (rh * 0.5f) / ppd;
            double latBot = centreLat - (rh * 0.5f) / ppd;
            if (latTop > 90.0) latTop = 90.0;
            if (latBot < -90.0) latBot = -90.0;
            if (latTop <= latBot) return 0;

            float yTop = cy - (float)(latTop - centreLat) * ppd;
            float yBot = cy - (float)(latBot - centreLat) * ppd;
            float vMax = (float)((latTop + 90.0) / 180.0);
            float vMin = (float)((latBot + 90.0) / 180.0);

            // ---- LONGITUDE ----
            double lonSpan = rw / ppd;
            if (lonSpan >= 360.0)
            {
                // The whole world fits across the rect. One quad, centred, letterboxed horizontally.
                float fullW = 360f * ppd;
                a.X = cx - fullW * 0.5f; a.W = fullW;
                a.Y = yTop; a.H = yBot - yTop;
                a.UMin = 0f; a.UMax = 1f; a.VMin = vMin; a.VMax = vMax;
                return 1;
            }

            double lonMin = centreLon - lonSpan * 0.5;
            double uMin = (lonMin + 180.0) / 360.0;
            double uSpan = lonSpan / 360.0;
            double uMax = uMin + uSpan;

            if (uMin < 0.0)
            {
                // Left part comes from the RIGHT end of the texture.
                float split = (float)((-uMin / uSpan) * rw);
                a.X = rx; a.W = split; a.Y = yTop; a.H = yBot - yTop;
                a.UMin = (float)(uMin + 1.0); a.UMax = 1f; a.VMin = vMin; a.VMax = vMax;

                b.X = rx + split; b.W = rw - split; b.Y = yTop; b.H = yBot - yTop;
                b.UMin = 0f; b.UMax = (float)uMax; b.VMin = vMin; b.VMax = vMax;
                return 2;
            }

            if (uMax > 1.0)
            {
                float split = (float)(((1.0 - uMin) / uSpan) * rw);
                a.X = rx; a.W = split; a.Y = yTop; a.H = yBot - yTop;
                a.UMin = (float)uMin; a.UMax = 1f; a.VMin = vMin; a.VMax = vMax;

                b.X = rx + split; b.W = rw - split; b.Y = yTop; b.H = yBot - yTop;
                b.UMin = 0f; b.UMax = (float)(uMax - 1.0); b.VMin = vMin; b.VMax = vMax;
                return 2;
            }

            a.X = rx; a.W = rw; a.Y = yTop; a.H = yBot - yTop;
            a.UMin = (float)uMin; a.UMax = (float)uMax; a.VMin = vMin; a.VMax = vMax;
            return 1;
        }

        // ---- the controls, as pure state changes ----
        // Kept here rather than in the painter so that "what does pressing zoom actually do" is
        // headless testable, and so the clamps live next to the projection that depends on them.

        public static MapView Zoom(MapView v, int delta)
        {
            v.ZoomStep = Clamp(v.ZoomStep + delta, 0, MaxZoom);
            return v;
        }

        /// <summary>
        /// Pan by one step. The step SHRINKS as you zoom in, so a press always moves the view by the
        /// same fraction of the screen rather than flinging it off the map at high zoom.
        ///
        /// Panning by hand clears Follow: the crew has said where they want to look, and a map that
        /// snapped back to the vessel a moment later would be the same failure as a page changing
        /// itself - the software deciding it knows better.
        /// </summary>
        public static MapView Pan(MapView v, double dLon, double dLat)
        {
            double step = PanDegrees / Pow2(Clamp(v.ZoomStep, 0, MaxZoom));
            v.CentreLon = Wrap180(v.CentreLon + dLon * step);
            v.CentreLat = v.CentreLat + dLat * step;
            if (v.CentreLat > 90.0) v.CentreLat = 90.0;
            if (v.CentreLat < -90.0) v.CentreLat = -90.0;
            if (dLon != 0.0 || dLat != 0.0) v.Follow = false;
            return v;
        }

        /// <summary>Re-centre on the vessel and resume following it.</summary>
        public static MapView Centre(MapView v, double lat, double lon)
        {
            v.CentreLat = lat;
            v.CentreLon = Wrap180(lon);
            v.Follow = true;
            return v;
        }

        public static MapView NextMode(MapView v)
        {
            v.Mode = (v.Mode == NavMode.Map) ? NavMode.Orbit : NavMode.Map;
            return v;
        }

        /// <summary>Keep the map on the vessel while Follow is set. Called every rebuild.</summary>
        public static MapView Track(MapView v, bool haveFix, double lat, double lon)
        {
            if (!v.Follow || !haveFix) return v;
            v.CentreLat = lat;
            v.CentreLon = Wrap180(lon);
            return v;
        }

        private static double Pow2(int n)
        {
            double r = 1.0;
            for (int i = 0; i < n; i++) r *= 2.0;
            return r;
        }

        private static int Clamp(int v, int lo, int hi)
        {
            return (v < lo) ? lo : (v > hi) ? hi : v;
        }
    }
}
