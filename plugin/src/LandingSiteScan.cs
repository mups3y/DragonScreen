// DragonScreen — LandingSiteScan  (KSP glue: shared ground-track → safe-water splashdown site scan)
// ============================================================================================
// Both the on-orbit ABORT (AbortControl) and the NOMINAL return (ReturnControl) must come home to OPEN WATER
// (the user's rule — a splashdown capsule never lands on a mountainside). The pure selector lives in
// SafeLandingSite; THIS is the one shared copy of the body-sampling glue that feeds it — so the hard-won
// water-gate detail (the F4 TerrainAltitude(lat,lon,true) fix that reads the real seabed height, without which
// the RSS scan reads 0 water and never commits) lives in exactly ONE place and cannot rot out of sync.
//
// Samples the orbit's ground track ahead (body-fixed lat/lon, rotation-corrected), tags each point Water/land,
// and returns the nearest reachable open-water site. Callers own their own committing + logging.
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    public struct SiteScanResult
    {
        public bool Found;       // a water site was selected
        public double LatDeg;    // its body-fixed latitude / longitude
        public double LonDeg;
        public int Idx;          // the chosen sample index (−1 = none)
        public int WaterCount;   // how many of the samples read as open water (instrumentation)
        public int SampleCount;  // total samples taken
    }

    public static class LandingSiteScan
    {
        // Scan `samples` points, `stepS` apart, along the orbit ground track ahead of the vehicle. Pick the
        // nearest open-water splashdown inside the reachable glide window [minGlideM, maxGlideM]; fall back to
        // the nearest water at least minGlideM ahead. Returns Found=false when the body has no ocean model
        // handled by the caller (this only samples an oceaned body). Deterministic; no side effects.
        public static SiteScanResult FindWaterSite(Vessel v, int samples, double stepS,
                                                   double minGlideM, double maxGlideM)
        {
            SiteScanResult r = new SiteScanResult();
            r.Idx = -1;
            CelestialBody body = v != null ? v.mainBody : null;
            if (body == null || v.orbit == null || samples < 1) return r;

            double now = Planetarium.GetUniversalTime();
            double vGround = v.srfSpeed > 50.0 ? v.srfSpeed : 7000.0;   // downrange-from-time speed
            GroundSample[] gs = new GroundSample[samples];
            for (int i = 0; i < samples; i++)
            {
                double dt = (i + 1) * stepS;
                double ut = now + dt;
                Vector3d p = v.orbit.getPositionAtUT(ut);
                double lat = body.GetLatitude(p);
                // longitude in the body-fixed frame at the FUTURE ut: subtract the body's rotation over dt.
                double rot = body.rotationPeriod > 1.0 ? 360.0 * (dt / body.rotationPeriod) : 0.0;
                double lon = NormLon(body.GetLongitude(p) - rot);
                gs[i].DownrangeM = vGround * dt;
                gs[i].LatDeg = lat; gs[i].LonDeg = lon;
                // ⭐ F4 (2026-08-29): the DEFAULT TerrainAltitude(lat,lon) CLAMPS ocean depth to 0, so "< 0" is
                // NEVER true over water. The THREE-ARG overload returns the real (negative) seabed height under
                // the ocean — confirmed against MechJeb (KSP 1.12). A body-ocean point below the datum = water.
                gs[i].Water = body.ocean && body.TerrainAltitude(lat, lon, true) < 0.0;
            }

            int idx = SafeLandingSite.PickDeorbitTarget(gs, minGlideM, maxGlideM);
            if (idx < 0) idx = SafeLandingSite.PickNearestWater(gs, minGlideM);

            int water = 0; for (int i = 0; i < samples; i++) if (gs[i].Water) water++;
            r.WaterCount = water; r.SampleCount = samples; r.Idx = idx;
            if (idx >= 0) { r.Found = true; r.LatDeg = gs[idx].LatDeg; r.LonDeg = gs[idx].LonDeg; }
            return r;
        }

        public static double NormLon(double lon)
        {
            while (lon > 180.0) lon -= 360.0;
            while (lon < -180.0) lon += 360.0;
            return lon;
        }
    }
}
