// Tests for pure/ThrustBalance.cs (B3) — the shared thrust-limiter balancing solver (TCA method).
// Ground-truth cases: a symmetric 4-engine octaweb with one engine OUT must throttle the opposite engine down
// to null the torque (keeping the other two); a symmetric RCS translation already has zero torque; an
// asymmetric-but-balanceable layout nulls torque while keeping translation; and a layout with no torque
// authority reports infeasible.
using System;
using DragonScreen;

public static class ThrustBalanceTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    static Vec3 Cr(double px, double py, double pz, Vec3 f)   // torque = r × f
    { return Vec3.Cross(new Vec3(px, py, pz), f); }

    public static int Run()
    {
        Console.WriteLine("DragonScreen B3 ThrustBalance tests");
        Vec3 up = new Vec3(0, 0, 1000);   // engine thrust (N), +z
        Vec3 fwdX = new Vec3(1, 0, 0);    // RCS thrust (N), +x

        // ---- CASE 1: octaweb, engine at (+1,0,0) FAILED → pass the 3 live engines. The one opposite the gap,
        // at (−1,0,0), must throttle to ~0 to null the residual torque; the other two stay near full. ----
        Vec3[] f1 = { up, up, up };
        Vec3[] t1 = { Cr(-1, 0, 0, up), Cr(0, 1, 0, up), Cr(0, -1, 0, up) };  // [opposite, +y, −y]
        double[] nom1 = { 1, 1, 1 };
        BalanceResult r1 = ThrustBalance.Solve(f1, t1, nom1, Vec3.Zero);
        Check("engine-out: torque nulled (feasible)", r1.Feasible, "err=" + r1.TorqueErrNm.ToString("F2"));
        Check("engine-out: |net torque| < cutoff", r1.NetTorqueNm.Magnitude < ThrustBalance.TorqueCutoffNm, r1.NetTorqueNm.Magnitude.ToString("F3"));
        Check("engine-out: opposite engine throttled ~off", r1.Limits[0] < 0.05, "lim0=" + r1.Limits[0].ToString("F3"));
        Check("engine-out: the +y engine stays near full", r1.Limits[1] > 0.9, "lim1=" + r1.Limits[1].ToString("F3"));
        Check("engine-out: the −y engine stays near full", r1.Limits[2] > 0.9, "lim2=" + r1.Limits[2].ToString("F3"));
        Check("engine-out: retains ~2 engines of axial thrust", Math.Abs(r1.NetForceN.Z - 2000.0) < 100.0, "Fz=" + r1.NetForceN.Z.ToString("F0"));

        // ---- CASE 2: symmetric RCS translation (+x), thrusters at ±y → torques already cancel; keep both full. ----
        Vec3[] f2 = { fwdX, fwdX };
        Vec3[] t2 = { Cr(0, 1, 0, fwdX), Cr(0, -1, 0, fwdX) };
        double[] nom2 = { 1, 1 };
        BalanceResult r2 = ThrustBalance.Solve(f2, t2, nom2, Vec3.Zero);
        Check("symmetric RCS: feasible", r2.Feasible, "");
        Check("symmetric RCS: both thrusters stay full", r2.Limits[0] > 0.99 && r2.Limits[1] > 0.99, "");
        Check("symmetric RCS: full +x translation", Math.Abs(r2.NetForceN.X - 2.0) < 1e-6, "Fx=" + r2.NetForceN.X.ToString("F3"));
        Check("symmetric RCS: zero net torque", r2.NetTorqueNm.Magnitude < 1e-6, "");

        // ---- CASE 3: asymmetric but balanceable (+x thrusters at +1,−1,+3 y). The far +3 thruster over-torques;
        // the balancer nulls torque while keeping meaningful +x translation, and the opposing (−1) thruster stays up. ----
        Vec3[] f3 = { fwdX, fwdX, fwdX };
        Vec3[] t3 = { Cr(0, 1, 0, fwdX), Cr(0, -1, 0, fwdX), Cr(0, 3, 0, fwdX) };
        double[] nom3 = { 1, 1, 1 };
        BalanceResult r3 = ThrustBalance.Solve(f3, t3, nom3, Vec3.Zero);
        Check("asymmetric RCS: torque nulled (feasible)", r3.Feasible, "err=" + r3.TorqueErrNm.ToString("F3"));
        Check("asymmetric RCS: keeps +x translation", r3.NetForceN.X > 1.0, "Fx=" + r3.NetForceN.X.ToString("F3"));
        Check("asymmetric RCS: opposing thruster stays up (reduce-only)", r3.Limits[1] > 0.9, "lim1=" + r3.Limits[1].ToString("F3"));

        // ---- CASE 4: no torque authority (single thruster through CoM, τ=0) but a torque is demanded → infeasible. ----
        Vec3[] f4 = { fwdX };
        Vec3[] t4 = { Vec3.Zero };
        double[] nom4 = { 1 };
        BalanceResult r4 = ThrustBalance.Solve(f4, t4, nom4, new Vec3(0, 0, 5));
        Check("no authority + demanded torque → infeasible", !r4.Feasible, "err=" + r4.TorqueErrNm.ToString("F2"));

        // ---- CASE 5: empty effector set → trivially feasible, no limits. ----
        BalanceResult r5 = ThrustBalance.Solve(new Vec3[0], new Vec3[0], new double[0], Vec3.Zero);
        Check("empty set → feasible, no limits", r5.Feasible && r5.Limits.Length == 0, "");

        // ---- DiffThrottle wrapper: same engine-out case, all-nominal-1 → nulls torque like CASE 1 ----
        BalanceResult d1 = DiffThrottle.Solve(f1, t1, Vec3.Zero);
        Check("DiffThrottle: engine-out torque nulled", d1.Feasible && d1.NetTorqueNm.Magnitude < ThrustBalance.TorqueCutoffNm, "");
        Check("DiffThrottle: opposite engine throttled ~off", d1.Limits[0] < 0.05, "lim0=" + d1.Limits[0].ToString("F3"));

        // ---- RcsBalance wrapper: +x demand with thrusters at ±y (+x) and one pointing −x. Selects only the +x
        // thrusters (−x thruster not fired), balances to zero torque, translates +x. ----
        Vec3 backX = new Vec3(-1, 0, 0);
        Vec3[] fr = { fwdX, fwdX, backX };
        Vec3[] tr = { Cr(0, 1, 0, fwdX), Cr(0, -1, 0, fwdX), Cr(0, 1, 0, backX) };
        BalanceResult rc = RcsBalance.Translate(fr, tr, new Vec3(1, 0, 0));
        Check("RcsBalance: +x thrusters selected & full", rc.Limits[0] > 0.99 && rc.Limits[1] > 0.99, "");
        Check("RcsBalance: wrong-way (−x) thruster NOT fired", rc.Limits[2] == 0.0, "lim2=" + rc.Limits[2].ToString("F3"));
        Check("RcsBalance: net translation is +x", rc.NetForceN.X > 1.5 && System.Math.Abs(rc.NetForceN.Y) < 1e-6, "F=" + rc.NetForceN.X.ToString("F2"));
        Check("RcsBalance: zero net torque", rc.NetTorqueNm.Magnitude < 1e-6, "");

        // ---- bounded iterations ----
        Check("solver stays within MaxIterations", r1.Iterations <= ThrustBalance.MaxIterations, "iters=" + r1.Iterations);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
