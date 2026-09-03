// Tests for Vec3 (pure/Vec3.cs) and the universal-variable conic propagator (pure/Conic.cs),
// checked against known conic cases — the propagator is asserted against arithmetic, not itself.
using System;
using DragonScreen;

public static class ConicTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, got.ToString("F4") + " vs " + want.ToString("F4")); }

    public static int Run()
    {
        Console.WriteLine("DragonScreen conic + vector tests");

        // ---- Vec3 ----
        Vec3 x = new Vec3(1, 0, 0), y = new Vec3(0, 1, 0), z = new Vec3(0, 0, 1);
        Near("dot of orthogonals is 0", Vec3.Dot(x, y), 0.0, 1e-12);
        Vec3 cx = Vec3.Cross(x, y);
        Check("x cross y = z (right-handed)", Math.Abs(cx.X) < 1e-12 && Math.Abs(cx.Y) < 1e-12 && Math.Abs(cx.Z - 1) < 1e-12, "");
        Near("magnitude of (3,4,0) is 5", new Vec3(3, 4, 0).Magnitude, 5.0, 1e-12);
        Near("normalized has unit length", new Vec3(3, 4, 0).Normalized.Magnitude, 1.0, 1e-12);
        Near("angle between x and y is 90 deg", Vec3.Angle(x, y), Math.PI / 2, 1e-9);
        Vec3 ex = Vec3.ExcludeUnit(new Vec3(2, 3, 0), x);   // remove the x-component
        Check("ExcludeUnit removes the along-axis part", Math.Abs(ex.X) < 1e-12 && Math.Abs(ex.Y - 3) < 1e-12, "");

        // ---- Conic propagation (Earth), against analytic answers ----
        double mu = 3.986004418e14, R = 6771000.0;         // 200 km circular
        double vc = Math.Sqrt(mu / R);
        double T = 2.0 * Math.PI * Math.Sqrt(R * R * R / mu);
        Vec3 r0 = new Vec3(R, 0, 0), v0 = new Vec3(0, vc, 0);
        Vec3 r, v;

        Check("quarter-orbit converges", Conic.Propagate(r0, v0, mu, T / 4.0, out r, out v), "");
        Near("...r rotates to (0,R,0): x", r.X, 0.0, 2.0);
        Near("...r rotates to (0,R,0): y", r.Y, R, 2.0);
        Near("...|r| preserved (circular)", r.Magnitude, R, 0.5);
        Near("...v rotates to (-vc,0,0): x", v.X, -vc, 0.01);
        Near("...|v| preserved (circular)", v.Magnitude, vc, 0.001);

        Check("full-period converges", Conic.Propagate(r0, v0, mu, T, out r, out v), "");
        Near("...returns to start r.x", r.X, R, 2.0);
        Near("...returns to start r.y", r.Y, 0.0, 2.0);
        Near("...returns to start v.y", v.Y, vc, 0.01);

        // elliptical: energy + angular momentum conserved over a step
        Vec3 rE = new Vec3(R, 0, 0), vE = new Vec3(0, vc * 1.15, 0);
        double e0 = vE.SqrMagnitude / 2.0 - mu / rE.Magnitude;
        double h0 = Vec3.Cross(rE, vE).Magnitude;
        Check("elliptical step converges", Conic.Propagate(rE, vE, mu, 1500.0, out r, out v), "");
        double e1 = v.SqrMagnitude / 2.0 - mu / r.Magnitude;
        double h1 = Vec3.Cross(r, v).Magnitude;
        Check("...specific energy conserved", Math.Abs(e1 - e0) / Math.Abs(e0) < 1e-8, (e1 - e0).ToString("E3"));
        Check("...angular momentum conserved", Math.Abs(h1 - h0) / h0 < 1e-8, (h1 - h0).ToString("E3"));

        // reversibility: forward then back returns to start
        Conic.Propagate(rE, vE, mu, 1234.0, out r, out v);
        Vec3 rb, vb;
        Conic.Propagate(r, v, mu, -1234.0, out rb, out vb);
        Near("propagate-then-reverse returns r.x", rb.X, R, 1.0);
        Near("propagate-then-reverse returns v.y", vb.Y, vc * 1.15, 0.02);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
