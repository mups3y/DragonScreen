// DragonScreen - MapProjection
// ---- WHY EQUIRECTANGULAR AND NOT A GLOBE ----
// ---- THERE IS NO CLIPPING, SO THE GEOMETRY MUST DO IT ----
// ---- SCALE IS ONE NUMBER FOR BOTH AXES ----
// ---- THE SEAM IS REAL AND IS HANDLED HERE ----
namespace DragonScreen
{
    public enum NavMode : byte
    {
        Map = 0,
        Orbit = 1,
        /// <summary>Live 3D scaled-planet view - the real globe with the orbit drawn over it.</summary>
        Planet = 2
    }

    public struct MapView
    {
        public double CentreLon;
        public double CentreLat;
        public int ZoomStep;
        public NavMode Mode;
        public bool Follow;

        // ---- PLANET (3D globe) mode: the globe spins about its axis, drawn edge-on from the equator ----
        /// <summary>Extra longitude the globe is rotated by, degrees, ON TOP of following the vehicle -
        /// left/right pan spins it so the crew can bring the far side of the orbit into view.</summary>
        public double PlanetRotDeg;
        /// <summary>Zoom step for the 3D globe - SEPARATE from the flat map's ZoomStep so the map's
        /// 0..MaxZoom clamp cannot cap it. NEGATIVE shrinks the globe (whole orbit fits), positive
        /// enlarges it. 0 is the default framing.</summary>
        public int PlanetZoom;
    }

    public struct MapQuad
    {
        public float X, Y, W, H;
        public float UMin, UMax, VMin, VMax;
    }

    public static class MapProjection
    {
        public const int MaxZoom = 5;

        private const double PanDegrees = 30.0;

        public static MapView Default()
        {
            MapView v = new MapView();
            v.CentreLon = 0.0;
            v.CentreLat = 0.0;
            v.ZoomStep = 0;
            v.Mode = NavMode.Map;
            v.Follow = true;
            v.PlanetRotDeg = 0.0;
            v.PlanetZoom = 0;
            return v;
        }

        /// <summary>Degrees the globe spins per left/right pan press.</summary>
        private const double PlanetPanStepDeg = 20.0;

        /// <summary>Globe zoom range. Negative shrinks it (whole orbit fits), positive enlarges it.</summary>
        public const int PlanetZoomMin = -5;
        public const int PlanetZoomMax = 6;

        public static float Scale(float rectW, float rectH, int zoomStep)
        {
            if (rectW <= 0f || rectH <= 0f) return 0f;
            float baseScale = rectW / 360f;
            float byHeight = rectH / 180f;
            if (byHeight < baseScale) baseScale = byHeight;
            return baseScale * (float)Pow2(Clamp(zoomStep, 0, MaxZoom));
        }

        public static double Wrap180(double deg)
        {
            while (deg > 180.0) deg -= 360.0;
            while (deg < -180.0) deg += 360.0;
            return deg;
        }

        public static double Wrap360(double deg)
        {
            while (deg >= 360.0) deg -= 360.0;
            while (deg < 0.0) deg += 360.0;
            return deg;
        }

        /// ---- A BUG CAUGHT IN THE PNG PREVIEW, AND IT WOULD HAVE BEEN NASTY IN GAME ----
        public static void EffectiveCentre(MapView view, float rw, float rh,
                                           out double lat, out double lon)
        {
            float ppd = Scale(rw, rh, view.ZoomStep);
            lon = (ppd > 0f && 360f * ppd <= rw) ? 0.0 : view.CentreLon;
            lat = (ppd > 0f && 180f * ppd <= rh) ? 0.0 : view.CentreLat;
        }

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

        public static int BodyQuads(MapView view, float rx, float ry, float rw, float rh,
                                    out MapQuad a, out MapQuad b)
        {
            a = new MapQuad(); b = new MapQuad();
            float ppd = Scale(rw, rh, view.ZoomStep);
            if (ppd <= 0f) return 0;

            float cx = rx + rw * 0.5f, cy = ry + rh * 0.5f;

            double centreLat, centreLon;
            EffectiveCentre(view, rw, rh, out centreLat, out centreLon);

            // ---- LATITUDE: clamp to the poles and shrink the quad, do not stretch it ----
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

        public static MapView Zoom(MapView v, int delta)
        {
            // PLANET mode has its OWN zoom (camera distance) with a much wider range than the flat
            // map's texture-window zoom, so pulling the 3D view right out is possible.
            if (v.Mode == NavMode.Planet)
            {
                v.PlanetZoom = Clamp(v.PlanetZoom + delta, PlanetZoomMin, PlanetZoomMax);
                return v;
            }
            v.ZoomStep = Clamp(v.ZoomStep + delta, 0, MaxZoom);
            return v;
        }

        public static MapView Pan(MapView v, double dLon, double dLat)
        {
            // PLANET (3D globe) mode: left/right spins the globe about its axis. The equatorial view has
            // no up/down, so dLat is a no-op here.
            if (v.Mode == NavMode.Planet)
            {
                v.PlanetRotDeg = Wrap180(v.PlanetRotDeg + dLon * PlanetPanStepDeg);
                return v;
            }

            double step = PanDegrees / Pow2(Clamp(v.ZoomStep, 0, MaxZoom));
            v.CentreLon = Wrap180(v.CentreLon + dLon * step);
            v.CentreLat = v.CentreLat + dLat * step;
            if (v.CentreLat > 90.0) v.CentreLat = 90.0;
            if (v.CentreLat < -90.0) v.CentreLat = -90.0;
            if (dLon != 0.0 || dLat != 0.0) v.Follow = false;
            return v;
        }

        public static MapView Centre(MapView v, double lat, double lon)
        {
            // PLANET (3D globe) mode: "centre" resets the spin and zoom back to following the vehicle.
            if (v.Mode == NavMode.Planet)
            {
                v.PlanetRotDeg = 0.0;
                v.PlanetZoom = 0;
                return v;
            }
            v.CentreLat = lat;
            v.CentreLon = Wrap180(lon);
            v.Follow = true;
            return v;
        }

        public static MapView NextMode(MapView v)
        {
            // Map -> Orbit -> Planet -> Map.
            switch (v.Mode)
            {
                case NavMode.Map:   v.Mode = NavMode.Orbit;  break;
                case NavMode.Orbit: v.Mode = NavMode.Planet; break;
                default:            v.Mode = NavMode.Map;     break;
            }
            return v;
        }

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
