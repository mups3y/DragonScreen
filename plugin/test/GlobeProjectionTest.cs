// Tests for GlobeProjection (pure/GlobeProjection.cs) - the orthographic projection + occlusion that
// lays the orbit overlay onto the 3D globe. Equatorial view centred on longitude 0, globe at the
// origin, radius 100 px; every expected screen position and hidden/visible answer is hand-computed.
using System;
using DragonScreen;

public static class GlobeProjectionTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, got.ToString("F3") + " vs " + want.ToString("F3")); }

    static void P(double lat, double lon, double ratio,
                  out float sx, out float sy, out bool front, out bool occ)
    {
        GlobeProjection.Project(lat, lon, ratio, 0.0, 0f, 0f, 100f, out sx, out sy, out front, out occ);
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen globe-projection tests");
        float sx, sy; bool front, occ;

        // Dead centre of the near face.
        P(0, 0, 1, out sx, out sy, out front, out occ);
        Near("centre x", sx, 0, 1e-4); Near("centre y", sy, 0, 1e-4);
        Check("centre is front", front, ""); Check("centre not occluded", !occ, "");

        // East limb: right edge, on the silhouette, still visible.
        P(0, 90, 1, out sx, out sy, out front, out occ);
        Near("east limb x", sx, 100, 1e-3); Near("east limb y", sy, 0, 1e-3);
        Check("east limb not occluded", !occ, "");

        // West limb via negative longitude: left edge.
        P(0, -90, 1, out sx, out sy, out front, out occ);
        Near("west limb x", sx, -100, 1e-3);

        // North pole: top of the globe, north is screen-up (negative y).
        P(90, 0, 1, out sx, out sy, out front, out occ);
        Near("north pole y", sy, -100, 1e-3); Near("north pole x", sx, 0, 1e-3);
        Check("north pole not occluded", !occ, "");

        // Far side, on the surface: hidden behind the globe.
        P(0, 180, 1, out sx, out sy, out front, out occ);
        Check("far-side surface is back", !front, "");
        Check("far-side surface is occluded", occ, "");

        // Far side but high (2x radius) directly behind centre: still occluded.
        P(0, 180, 2, out sx, out sy, out front, out occ);
        Check("far-side high behind centre is occluded", occ, "");

        // Far-ish but wide of the globe (135 deg, 2x radius): peeks past the limb, visible.
        P(0, 135, 2, out sx, out sy, out front, out occ);
        Check("high point wide of the limb is NOT occluded", !occ, "");
        Check("...and reads as back-hemisphere", !front, "");

        // Ratio floats the point outward: same direction, twice the offset.
        float sx1, sy1; bool f1, o1;
        P(0, 30, 1, out sx1, out sy1, out f1, out o1);
        float sx2, sy2; bool f2, o2;
        P(0, 30, 2, out sx2, out sy2, out f2, out o2);
        Near("ratio 2 doubles the x offset", sx2, sx1 * 2f, 1e-3);

        // A near-side point above the surface is never hidden.
        P(0, 0, 1.05, out sx, out sy, out front, out occ);
        Check("near-side orbit point visible", !occ, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }
}
