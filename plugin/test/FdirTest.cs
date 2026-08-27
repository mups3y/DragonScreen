// Tests for L5 FDIR: the debounced monitor primitive (pure/FaultMonitor.cs), the fault-detection spine +
// phase-aware recovery table (pure/Fdir.cs), and the phase-correct abort responder (pure/AbortResponder.cs).
using System;
using DragonScreen;

public static class FdirTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    static FdirInputs Healthy(MissionPhase phase)
    {
        FdirInputs s = new FdirInputs();
        s.Valid = true; s.Dt = 1.0; s.Phase = phase; s.GateHolding = false; s.Powered = false;
        s.ThrustDeliveredFrac = 1.0; s.TrajErrorM = 0.0; s.PlanProgressRate = 1.0;
        s.ResourceMargin01 = 1.0; s.ControlSolutionOk = true;
        s.KosRadiusM = 0.0; s.KosRangeM = 1e9; s.CorridorOk = true;
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen L5 FDIR tests");

        // ============================ FAULT MONITOR (detect + debounce) ============================
        MonitorState m = new MonitorState();
        Check("does not trip before the confirmation time",
              !FaultMonitor.Update(ref m, true, false, 1.0, 2.0, 3.0), "");
        Check("trips once the fault has persisted for confirmS",
              FaultMonitor.Update(ref m, true, false, 1.5, 2.0, 3.0), "");   // total 2.5s ≥ 2
        Check("stays tripped inside the hysteresis deadband (neither over nor under)",
              FaultMonitor.Update(ref m, false, false, 5.0, 2.0, 3.0), "");
        Check("does not clear before the clear time",
              FaultMonitor.Update(ref m, false, true, 1.0, 2.0, 3.0), "");   // 1s < 3
        Check("clears after the clear time below the hysteresis threshold",
              !FaultMonitor.Update(ref m, false, true, 3.0, 2.0, 3.0), "");
        MonitorState m2 = new MonitorState();
        FaultMonitor.Update(ref m2, true, false, 5.0, 2.0, 3.0);
        Check("dt <= 0 does not change the state", FaultMonitor.Update(ref m2, false, true, 0.0, 2.0, 3.0), "");

        // ============================ FDIR spine ============================
        // healthy → no fault
        FdirState st = new FdirState();
        FdirReport r = Fdir.Update(ref st, Healthy(MissionPhase.Ascent));
        Check("healthy flight → no fault, Continue", r.Fault == FaultKind.None && r.Response == Recovery.Continue && !r.Abort, "");

        // KOS breach trips fast → abort
        FdirState stk = new FdirState();
        FdirInputs kos = Healthy(MissionPhase.Approach);
        kos.KosRadiusM = 200; kos.KosRangeM = 100; kos.CorridorOk = false; kos.Dt = 0.5;
        FdirReport rk = Fdir.Update(ref stk, kos);
        Check("unplanned KOS breach → KeepOutBreach → Abort", rk.Fault == FaultKind.KeepOutBreach && rk.Abort, rk.Fault.ToString());

        // thrust shortfall on ascent → abort (launch)
        FdirState stt = new FdirState();
        FdirInputs thr = Healthy(MissionPhase.Ascent);
        thr.Powered = true; thr.ThrustDeliveredFrac = 0.4; thr.Dt = 2.5;
        FdirReport rt = Fdir.Update(ref stt, thr);
        Check("sustained thrust shortfall on ascent → ThrustShortfall → Abort", rt.Fault == FaultKind.ThrustShortfall && rt.Abort, rt.Fault.ToString());
        // not while unpowered
        FdirState stu = new FdirState();
        FdirInputs unp = Healthy(MissionPhase.Coast); unp.Powered = false; unp.ThrustDeliveredFrac = 0.0; unp.Dt = 5.0;
        Check("no thrust fault when no burn is expected", Fdir.Update(ref stu, unp).Fault == FaultKind.None, "");

        // resource critical → downmode (or safe mode at zero)
        FdirState str = new FdirState();
        FdirInputs res = Healthy(MissionPhase.Coast); res.ResourceMargin01 = 0.03; res.Dt = 2.5;
        Check("low resource margin → ResourceCritical → Downmode", Fdir.Update(ref str, res).Response == Recovery.Downmode, "");
        FdirState str0 = new FdirState();
        FdirInputs res0 = Healthy(MissionPhase.Coast); res0.ResourceMargin01 = 0.0; res0.Dt = 2.5;
        Check("no resource margin → SafeMode (abort floor)", Fdir.Update(ref str0, res0).Response == Recovery.SafeMode, "");

        // trajectory divergence → replan
        FdirState std = new FdirState();
        FdirInputs div = Healthy(MissionPhase.Phasing); div.TrajErrorM = 8000; div.Dt = 2.5;
        Check("growing trajectory error → TrajectoryDivergence → Replan",
              Fdir.Update(ref std, div).Fault == FaultKind.TrajectoryDivergence, "");

        // convergence stall is SUPPRESSED during an intended crew hold
        FdirState sth = new FdirState();
        FdirInputs holdStall = Healthy(MissionPhase.Approach);
        holdStall.GateHolding = true; holdStall.PlanProgressRate = -1.0; holdStall.Dt = 10.0;
        Check("a plan not progressing during a crew HOLD is NOT a fault",
              Fdir.Update(ref sth, holdStall).Fault == FaultKind.None, "");
        // but a real stall (not holding) trips
        FdirState sts = new FdirState();
        FdirInputs stall = Healthy(MissionPhase.Phasing);
        stall.GateHolding = false; stall.PlanProgressRate = -1.0; stall.Dt = 7.0;
        Check("a real convergence stall (not holding) → ConvergenceStall",
              Fdir.Update(ref sts, stall).Fault == FaultKind.ConvergenceStall, "");

        // priority: KOS breach beats a simultaneous thrust shortfall
        FdirState stp = new FdirState();
        FdirInputs both = Healthy(MissionPhase.Approach);
        both.KosRadiusM = 200; both.KosRangeM = 100; both.CorridorOk = false;
        both.Powered = true; both.ThrustDeliveredFrac = 0.4; both.Dt = 2.5;
        Check("KOS breach outranks thrust shortfall", Fdir.Update(ref stp, both).Fault == FaultKind.KeepOutBreach, "");

        // recovery table spot checks (pure)
        Check("Recover: KOS breach → Abort in any prox phase", Fdir.Recover(FaultKind.KeepOutBreach, MissionPhase.Approach, 1.0) == Recovery.Abort, "");
        Check("Recover: thrust shortfall on ascent → Abort", Fdir.Recover(FaultKind.ThrustShortfall, MissionPhase.Ascent, 1.0) == Recovery.Abort, "");
        Check("Recover: thrust shortfall on orbit → Replan", Fdir.Recover(FaultKind.ThrustShortfall, MissionPhase.Phasing, 1.0) == Recovery.Replan, "");
        Check("Recover: resource at zero → SafeMode", Fdir.Recover(FaultKind.ResourceCritical, MissionPhase.Coast, 0.0) == Recovery.SafeMode, "");

        // ============================ ABORT RESPONDER (SELF-AWARE, regime-correct) ============================
        Check("no trigger → no abort action, but attitude is always held",
              AbortResponder.Respond(new AbortInputs { Triggered = false }).Mode == AbortMode.None
              && AbortResponder.Respond(new AbortInputs { Triggered = false }).HoldAttitude, "");

        // ascent, deep sub-orbital (like flight 030705 at 129 km, 1900 m/s vs ~7700 orbital) → LAUNCH ESCAPE,
        // and it MUST include the trunk jettison + shield-forward reorient that the tumble was missing.
        AbortCommand asc = AbortResponder.Respond(new AbortInputs {
            Triggered = true, Phase = MissionPhase.Ascent, LesArmed = true,
            AltitudeM = 129000, AtmTopM = 140000, SurfaceSpeedMps = 1900, OrbitalSpeedMps = 7700, PeriapsisAltM = -200000 });
        Check("sub-orbital ascent abort → LAUNCH ESCAPE (SuperDracos, separate, TRUNK JETTISON, shield-forward, chutes)",
              asc.Mode == AbortMode.LaunchEscape && asc.FireSuperDracos && asc.Separate
              && asc.JettisonTrunk && asc.HoldShieldForward && asc.DeployChutes, "");
        Check("prelaunch abort with LES armed → LAUNCH ESCAPE",
              AbortResponder.Respond(new AbortInputs { Triggered = true, Phase = MissionPhase.Prelaunch, LesArmed = true }).Mode == AbortMode.LaunchEscape, "");
        Check("ascent abort WITHOUT armed LES → SafeHold (no escape available)",
              AbortResponder.Respond(new AbortInputs { Triggered = true, Phase = MissionPhase.Ascent, LesArmed = false }).Mode == AbortMode.SafeHold, "");

        // very late ascent, near orbital energy → ABORT-TO-ORBIT (fly to a safe orbit, don't splash).
        AbortCommand ato = AbortResponder.Respond(new AbortInputs {
            Triggered = true, Phase = MissionPhase.Ascent, LesArmed = true,
            AltitudeM = 190000, AtmTopM = 140000, SurfaceSpeedMps = 7500, OrbitalSpeedMps = 7700, ApoapsisAltM = 210000 });
        Check("near-orbital ascent abort → ABORT-TO-ORBIT (raise orbit, do not escape)",
              ato.Mode == AbortMode.AbortToOrbit && ato.RaiseToOrbit && !ato.FireSuperDracos, "");

        // crew hits ABORT in orbit, NOT near the station → DEORBIT RETURN (trunk jettison + deorbit + chutes).
        AbortCommand orb = AbortResponder.Respond(new AbortInputs {
            Triggered = true, Phase = MissionPhase.Coast, AtmTopM = 140000, PeriapsisAltM = 200000, NearStation = false });
        Check("on-orbit abort (not near station) → DEORBIT RETURN to a safe splashdown",
              orb.Mode == AbortMode.DeorbitReturn && orb.DeorbitBurn && orb.JettisonTrunk && orb.HoldShieldForward, "");

        // prox-ops NEAR the station → KOS RETREAT (back out, not a SuperDraco escape, not a deorbit).
        AbortCommand prox = AbortResponder.Respond(new AbortInputs { Triggered = true, Phase = MissionPhase.Approach, NearStation = true });
        Check("prox-ops abort near the station → KOS RETREAT",
              prox.Mode == AbortMode.KosRetreat && prox.Retreat && !prox.FireSuperDracos, "");
        Check("phasing abort near the station → KOS RETREAT",
              AbortResponder.Respond(new AbortInputs { Triggered = true, Phase = MissionPhase.Phasing, NearStation = true }).Mode == AbortMode.KosRetreat, "");

        Check("docked abort → EMERGENCY UNDOCK then deorbit home",
              AbortResponder.Respond(new AbortInputs { Triggered = true, Phase = MissionPhase.Docked }).Mode == AbortMode.EmergencyUndock, "");
        AbortCommand ent = AbortResponder.Respond(new AbortInputs { Triggered = true, Phase = MissionPhase.Entry });
        Check("entry abort → RIDE IT DOWN (past escape; shield-forward + chutes)",
              ent.Mode == AbortMode.RideItDown && ent.DeployChutes && ent.HoldShieldForward, "");
        Check("every abort holds attitude (never floats)",
              AbortResponder.Respond(new AbortInputs { Triggered = true, Phase = MissionPhase.Approach, NearStation = true }).HoldAttitude, "");

        // ---- SafeLandingSite: pick the nearest OCEAN splashdown in the reachable glide window ----
        GroundSample[] gs = new GroundSample[] {
            new GroundSample { DownrangeM = 0.5e6, Water = true },    // too near (before the window)
            new GroundSample { DownrangeM = 2.0e6, Water = false },   // land
            new GroundSample { DownrangeM = 3.0e6, Water = true },    // ← nearest water in window
            new GroundSample { DownrangeM = 5.0e6, Water = true },
        };
        Check("safe site = nearest water inside the glide window (not the too-near one)",
              SafeLandingSite.PickDeorbitTarget(gs, 1.0e6, 9.0e6) == 2, "");
        Check("no water in window → -1 (glue coasts a step)",
              SafeLandingSite.PickDeorbitTarget(new GroundSample[]{ new GroundSample{ DownrangeM=2e6, Water=false } }, 1e6, 9e6) == -1, "");
        Check("fallback = nearest water beyond the min lead",
              SafeLandingSite.PickNearestWater(gs, 1.0e6) == 2, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
