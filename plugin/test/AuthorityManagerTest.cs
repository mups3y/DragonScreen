/*
 * Tests for the control-authority arbitration layer (pure/AuthorityManager.cs).
 *
 * Pins the rules the dual-vessel + manual-docking work depends on: priority (Abort pre-empts all,
 * Manual outranks Autopilot), the Holds() gate a controller checks before it actuates, per-vehicle
 * isolation (the booster and Dragon never steal each other's control), and the crew-mode derivation.
 */
using DragonScreen;
using System;

public static class AuthorityManagerTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen AuthorityManager (control arbitration) tests");

        // ---- claim / grant basics ----
        var a = new VehicleAuthority();
        Check("unclaimed axis is None", a.Granted(AuthAxis.Attitude) == AuthSource.None, "");
        Check("fresh vehicle mode is Idle", a.Mode == ControlMode.Idle, "");
        a.Claim(AuthAxis.Attitude, AuthSource.Autopilot);
        Check("autopilot claim grants autopilot", a.Granted(AuthAxis.Attitude) == AuthSource.Autopilot, "");
        Check("autopilot Holds after claim", a.Holds(AuthAxis.Attitude, AuthSource.Autopilot), "");
        Check("mode Auto with an autopilot claim", a.Mode == ControlMode.Auto, "");
        a.Release(AuthAxis.Attitude, AuthSource.Autopilot);
        Check("release returns to None", a.Granted(AuthAxis.Attitude) == AuthSource.None, "");

        // ---- priority: Manual outranks Autopilot; the autopilot yields ----
        a.Clear();
        a.Claim(AuthAxis.Translation, AuthSource.Autopilot);
        a.Claim(AuthAxis.Translation, AuthSource.Manual);
        Check("Manual outranks Autopilot", a.Granted(AuthAxis.Translation) == AuthSource.Manual, "");
        Check("autopilot no longer Holds (yields to manual)", !a.Holds(AuthAxis.Translation, AuthSource.Autopilot), "");
        Check("manual Holds", a.Holds(AuthAxis.Translation, AuthSource.Manual), "");
        Check("mode is Manual", a.Mode == ControlMode.Manual, "");
        a.Release(AuthAxis.Translation, AuthSource.Manual);
        Check("releasing manual falls back to autopilot", a.Granted(AuthAxis.Translation) == AuthSource.Autopilot, "");

        // ---- abort pre-empts everything ----
        a.Clear();
        a.SetAutopilot(true, true, true, true);
        a.ClaimAll(AuthSource.Manual);
        a.ClaimAll(AuthSource.Abort);
        Check("abort pre-empts on every axis", a.Granted(AuthAxis.Throttle) == AuthSource.Abort
              && a.Granted(AuthAxis.Attitude) == AuthSource.Abort, "");
        Check("no lower source Holds during abort", !a.Holds(AuthAxis.Attitude, AuthSource.Autopilot)
              && !a.Holds(AuthAxis.Attitude, AuthSource.Manual), "");
        Check("mode is Abort", a.Mode == ControlMode.Abort, "");
        a.ReleaseSource(AuthSource.Abort);
        Check("clearing abort drops back to manual (still claimed)", a.Mode == ControlMode.Manual, "");

        // ---- recovery outranks manual/autopilot but not abort ----
        a.Clear();
        a.Claim(AuthAxis.Throttle, AuthSource.Autopilot);
        a.ClaimAll(AuthSource.Recovery);
        Check("recovery outranks autopilot", a.Granted(AuthAxis.Throttle) == AuthSource.Recovery, "");
        Check("mode is Recovery", a.Mode == ControlMode.Recovery, "");
        a.Claim(AuthAxis.Throttle, AuthSource.Abort);
        Check("abort still beats recovery", a.Granted(AuthAxis.Throttle) == AuthSource.Abort, "");

        // ---- SetAutopilot maps per-axis latches exactly ----
        a.SetAutopilot(true, false, true, false);
        Check("SetAutopilot: throttle owned", a.Holds(AuthAxis.Throttle, AuthSource.Autopilot), "");
        Check("SetAutopilot: translation NOT owned", a.Granted(AuthAxis.Translation) == AuthSource.None, "");
        Check("SetAutopilot: attitude owned", a.Holds(AuthAxis.Attitude, AuthSource.Autopilot), "");
        Check("SetAutopilot: roll NOT owned", a.Granted(AuthAxis.Roll) == AuthSource.None, "");
        a.SetWhole(AuthSource.None);
        Check("SetWhole(None) clears to Idle", a.Mode == ControlMode.Idle, "");

        // ---- per-vehicle isolation via the static manager ----
        AuthorityManager.Reset();
        AuthorityManager.Dragon.SetAutopilot(true, true, true, true);
        AuthorityManager.Booster.SetWhole(AuthSource.None);
        Check("Dragon Auto does not light the Booster", AuthorityManager.Dragon.Mode == ControlMode.Auto
              && AuthorityManager.Booster.Mode == ControlMode.Idle, "");
        AuthorityManager.Booster.SetAutopilot(true, false, true, true);
        AuthorityManager.Dragon.SetWhole(AuthSource.Abort);
        Check("Booster stays Auto while Dragon aborts (independent)",
              AuthorityManager.Booster.Mode == ControlMode.Auto && AuthorityManager.Dragon.Mode == ControlMode.Abort, "");
        Check("Of(Booster) returns the booster slot", AuthorityManager.Of(AuthVehicle.Booster) == AuthorityManager.Booster, "");
        AuthorityManager.Reset();
        Check("Reset clears both vehicles", AuthorityManager.Dragon.Mode == ControlMode.Idle
              && AuthorityManager.Booster.Mode == ControlMode.Idle, "");

        // ---- mode naming ----
        Check("Name(Auto)=AUTO", AuthorityManager.Name(ControlMode.Auto) == "AUTO", "");
        Check("Name(Abort)=ABORT", AuthorityManager.Name(ControlMode.Abort) == "ABORT", "");
        Check("ModeOf(Recovery)=Recovery", AuthorityManager.ModeOf(AuthSource.Recovery) == ControlMode.Recovery, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
