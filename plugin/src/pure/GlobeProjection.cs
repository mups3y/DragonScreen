/*
 * DragonScreen - GlobeProjection
 *
 * PURE. Orthographic projection onto the 3D globe NavPage.Globe draws - so the orbit overlay lands
 * exactly on the textured sphere it is drawn over. The globe is viewed edge-on from the equatorial
 * plane, centred on a chosen longitude (the hemisphere within +/-90 deg of it faces the viewer), north
 * up. That is the one view the strip-textured globe supports, and it is the view the crew already liked.
 *
 * A point is (lat, lon, ratio): ratio is its radius as a multiple of the body radius, so an orbit
 * floats above the surface (1 = on the surface). Project gives the screen position AND whether the
 * solid globe hides it - the far side of an orbit correctly disappears behind the planet.
 *
 * The occlusion is the orthographic ray/sphere test: along the viewing ray through the point, the unit
 * globe's near surface sits at depth sqrt(1 - perp^2) (in body radii); the point is hidden when it lies
 * inside the silhouette (perp < 1) and behind that surface (its own depth is smaller). For a surface
 * point this reduces to "hidden iff on the far hemisphere", exactly as a globe should read.
 */
using System;

namespace DragonScreen
{
    public static class GlobeProjection
    {
        private const double Deg2Rad = Math.PI / 180.0;

        /// <summary>
        /// Project a body-fixed point onto the globe centred at <paramref name="lonCentre"/> (equatorial
        /// view, north up), drawn at pixel centre (cx,cy) and pixel radius rPx.
        /// <paramref name="sx"/>/<paramref name="sy"/> are screen pixels; <paramref name="front"/> is
        /// true on the near hemisphere; <paramref name="occluded"/> is true when the solid globe hides it.
        /// </summary>
        public static void Project(double lat, double lon, double ratio, double lonCentre,
                                   float cx, float cy, float rPx,
                                   out float sx, out float sy, out bool front, out bool occluded)
        {
            if (ratio < 0.0) ratio = 0.0;
            double dlon = MapProjection.Wrap180(lon - lonCentre) * Deg2Rad;
            double la = lat * Deg2Rad;
            double cosLat = Math.Cos(la);

            double right = cosLat * Math.Sin(dlon);   // screen +x  (east)
            double upc = Math.Sin(la);                 // screen up   (north)
            double viewer = cosLat * Math.Cos(dlon);   // toward the viewer (depth)

            sx = cx + (float)(ratio * right * rPx);
            sy = cy - (float)(ratio * upc * rPx);
            front = viewer >= 0.0;

            double perp2 = ratio * ratio * (1.0 - viewer * viewer);   // (dist from view axis)^2, body radii
            double depth = ratio * viewer;
            occluded = (perp2 < 1.0) && (depth < Math.Sqrt(1.0 - perp2));
        }

        /// <summary>Convenience: visible = projected and not hidden behind the globe.</summary>
        public static bool Visible(double lat, double lon, double ratio, double lonCentre,
                                   float cx, float cy, float rPx, out float sx, out float sy)
        {
            bool front, occluded;
            Project(lat, lon, ratio, lonCentre, cx, cy, rPx, out sx, out sy, out front, out occluded);
            return !occluded;
        }
    }
}
