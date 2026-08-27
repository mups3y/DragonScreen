/*
 * DragonScreen - PlanetOverlay
 *
 * PURE. The data the NAV page draws over the 3D globe: the vessel orbit, the target orbit and the
 * markers (vessel, target, apoapsis, periapsis), each carried as BODY-FIXED (lat, lon, radius-ratio) -
 * the same frame the flat map's ground track already uses, rotation-corrected so a future orbit point
 * sits where the vehicle will actually pass over the ground.
 *
 * ---- WHY THIS, NOT A CAMERA ----
 * The 3D globe is the SAME textured disc the ORBIT view already draws from the game's own scaled-space
 * map (NavPage.Globe) - the real, correctly-aligned Earth, which looks right and needs no download or
 * render-to-texture. So the overlay is not projected by a Unity camera; NavPage places each point with
 * a pure orthographic globe projection (GlobeProjection), the twin of that disc. The GLUE (VesselData)
 * fills these arrays from the live orbit; the page projects them - the same split the flat map uses.
 *
 * The orbit lat/lon arrays are SHARED REFERENCES to VesselData's ground-track buffers (no copy); the
 * radius ratio floats each point above the surface so an orbit reads as an orbit and a higher target
 * sits visibly higher. Reused every frame, so nothing here allocates in the draw path.
 */
namespace DragonScreen
{
    /// <summary>A body-fixed point for the globe: latitude/longitude in degrees and radius as a
    /// multiple of the body radius (1 = on the surface). Has=false when there is nothing to place.</summary>
    public struct GlobePoint
    {
        public double Lat, Lon, Ratio;
        public bool Has;
    }

    public sealed class PlanetOverlay
    {
        /// <summary>Ground-track sample count, matching VesselData's buffers. Pure so the page capacity
        /// test can size worst-case arrays without the glue.</summary>
        public const int DefaultSamples = 90;

        /// <summary>The glue filled a valid orbit this frame; else the page shows only the globe.</summary>
        public bool Ready;
        /// <summary>Body present but no orbit to plot (on the surface); the page says so.</summary>
        public bool OnSurface;

        // ---- the vessel orbit (shared refs to VesselData's ground-track buffers) + per-sample ratio ----
        public double[] OrbitLat, OrbitLon, OrbitRatio;
        public int OrbitCount;

        // ---- the target orbit ----
        public double[] TgtLat, TgtLon, TgtRatio;
        public int TgtCount;

        // ---- point markers ----
        public GlobePoint Vessel, Target, Ap, Pe;

        public void Reset()
        {
            Ready = false;
            OnSurface = false;
            OrbitCount = 0;
            TgtCount = 0;
            Vessel.Has = false; Target.Has = false; Ap.Has = false; Pe.Has = false;
        }
    }
}
