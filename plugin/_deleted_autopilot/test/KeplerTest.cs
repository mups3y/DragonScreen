// Tests for the universal-variable Kepler propagator against known conic cases. See pure/Kepler.cs.
using System;
using DragonScreen;
using MechJebLib.Primitives;

public static class KeplerTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }
    static void Near(string what, double got, double want, double tol)
    {
        Check(what, Math.Abs(got - want) <= tol, got.ToString("F3") + " vs " + want.ToString("F3"));
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen Kepler propagator tests");

        double mu = 3.986004418e14;         // Earth
        double R = 6771000.0;               // 200 km circular orbit radius
        double vc = Math.Sqrt(mu / R);      // circular speed
        double T = 2.0 * Math.PI * Math.Sqrt(R * R * R / mu);

        V3 r0 = new V3(R, 0.0, 0.0);
        V3 v0 = new V3(0.0, vc, 0.0);       // prograde in the XY plane

        // ---- quarter period: a 90-degree rotation in-plane ----
        V3 r, v;
        Check("quarter-orbit propagation converges", Kepler.Propagate(r0, v0, mu, T / 4.0, out r, out v), "");
        Near("...r rotates to (0, R, 0): x", r.x, 0.0, 1.0);
        Near("...r rotates to (0, R, 0): y", r.y, R, 1.0);
        Near("...|r| is preserved", r.magnitude, R, 0.5);
        Near("...v rotates to (-vc, 0, 0): x", v.x, -vc, 0.01);
        Near("...v rotates to (-vc, 0, 0): y", v.y, 0.0, 0.01);
        Near("...|v| is preserved (circular)", v.magnitude, vc, 0.001);

        // ---- full period: identity ----
        Check("full-period propagation converges", Kepler.Propagate(r0, v0, mu, T, out r, out v), "");
        Near("...returns to the start: r.x", r.x, R, 2.0);
        Near("...returns to the start: r.y", r.y, 0.0, 2.0);
        Near("...returns to the start: v.y", v.y, vc, 0.01);

        // ---- an elliptical orbit conserves energy and angular momentum over a step ----
        V3 rE = new V3(R, 0.0, 0.0);
        V3 vE = new V3(0.0, vc * 1.15, 0.0);          // faster than circular -> ellipse
        double e0 = vE.sqrMagnitude / 2.0 - mu / rE.magnitude;         // specific energy
        double h0 = V3.Cross(rE, vE).magnitude;                        // specific ang. momentum
        Check("elliptical step converges", Kepler.Propagate(rE, vE, mu, 900.0, out r, out v), "");
        double e1 = v.sqrMagnitude / 2.0 - mu / r.magnitude;
        double h1 = V3.Cross(r, v).magnitude;
        Check("...specific energy conserved", Math.Abs(e1 - e0) / Math.Abs(e0) < 1e-6,
              (e1 - e0).ToString("E3"));
        Check("...angular momentum conserved", Math.Abs(h1 - h0) / h0 < 1e-6, (h1 - h0).ToString("E3"));

        // ---- backward then forward returns to start ----
        Kepler.Propagate(rE, vE, mu, 1234.0, out r, out v);
        V3 rb, vb;
        Kepler.Propagate(r, v, mu, -1234.0, out rb, out vb);
        Near("propagate-then-reverse returns r.x", rb.x, R, 1.0);
        Near("propagate-then-reverse returns v.y", vb.y, vc * 1.15, 0.02);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
