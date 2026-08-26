/*
 * DragonScreen headless tests - the Layer-3 FDIR framework (docs/LAYER3_AUTONOMY_PLAN.md).
 *
 * Three pure pieces, tested here before any of them is wired to a live vehicle (the plan's P0: prove
 * the primitives headless, wire nothing yet):
 *   - HealthMonitor      the residual -> verdict debounce core (confirmation time + hysteresis)
 *   - FaultResponse      the (fault, domain, verdict) -> recovery decision table
 *   - ThrustDeliveryMonitor  the first concrete monitor, checked against the real corpus failures it
 *                            is meant to catch (lit-but-no-thrust, no-burn rendezvous, reversed Draco)
 */
using System;
using DragonScreen;

public static class HealthMonitorTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok)
        {
            failures++;
            Console.WriteLine("  FAIL  " + what + "   " + detail);
        }
    }

    // Drive a monitor n times with a constant residual, dt each. Returns the final state.
    static MonitorState StepN(MonitorState s, MonitorConfig cfg, double residual, double dt, int n)
    {
        for (int i = 0; i < n; i++) s = HealthMonitor.Step(s, cfg, residual, dt);
        return s;
    }

    static MonitorState StepSamples(MonitorState s, ThrustSample x, double dt, int n)
    {
        for (int i = 0; i < n; i++) s = ThrustDeliveryMonitor.Step(s, x, dt);
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen FDIR framework tests");

        // ============================ HealthMonitor ============================
        // degrade at 0.5, fail at 0.9, clear at 0.3; confirm 1.0 s, clear 1.0 s.
        MonitorConfig cfg = HealthMonitor.Config(0.5, 0.9, 0.3, 1.0, 1.0);

        Check("a fresh monitor is Nominal", HealthMonitor.Fresh().Verdict == HealthVerdict.Nominal, "");

        // ---- classification bands (the instantaneous evaluation, before debounce) ----
        Check("below the degrade band is Nominal",
              HealthMonitor.Classify(0.4, cfg) == HealthVerdict.Nominal, "");
        Check("into the degrade band is Degraded",
              HealthMonitor.Classify(0.6, cfg) == HealthVerdict.Degraded, "");
        Check("into the fail band is Failed",
              HealthMonitor.Classify(1.0, cfg) == HealthVerdict.Failed, "");

        // ---- confirmation time: a transient must NOT latch a fault ----
        // One bad tick (dt 0.2) is nowhere near the 1.0 s confirm.
        MonitorState one = HealthMonitor.Step(HealthMonitor.Fresh(), cfg, 1.0, 0.2);
        Check("a single bad tick does not latch a fault",
              one.Verdict == HealthVerdict.Nominal, HealthMonitor.Name(one.Verdict));
        Check("...but the RAW reading still shows it, for the recorder",
              one.Raw == HealthVerdict.Failed, HealthMonitor.Name(one.Raw));
        // A failed residual held past the confirm time DOES latch. 6 x 0.2 = 1.2 s > 1.0.
        MonitorState held = StepN(HealthMonitor.Fresh(), cfg, 1.0, 0.2, 6);
        Check("a fault held past the confirmation time latches Failed",
              held.Verdict == HealthVerdict.Failed, HealthMonitor.Name(held.Verdict));
        // A shortfall just short of the confirm time then recovering never latches.
        MonitorState blip = StepN(HealthMonitor.Fresh(), cfg, 1.0, 0.2, 4);   // 0.8 s < 1.0
        blip = StepN(blip, cfg, 0.0, 0.2, 3);                                 // recovered
        Check("a fault that clears before confirming leaves the verdict Nominal",
              blip.Verdict == HealthVerdict.Nominal, HealthMonitor.Name(blip.Verdict));

        // ---- hysteresis: a latched fault HOLDS in the band, clears only below the clear threshold ----
        // Sit in the band (0.6: Degraded raw, above clear 0.3) - a latched Failed must not clear here.
        MonitorState band = StepN(held, cfg, 0.6, 0.2, 20);
        Check("a latched fault holds while the residual sits in the hysteresis band",
              band.Verdict == HealthVerdict.Failed, HealthMonitor.Name(band.Verdict));
        // Drop clearly below the clear threshold, past the clear time -> recovers to Nominal.
        MonitorState cleared = StepN(band, cfg, 0.1, 0.2, 6);
        Check("a residual below the clear threshold, held past the clear time, recovers to Nominal",
              cleared.Verdict == HealthVerdict.Nominal, HealthMonitor.Name(cleared.Verdict));
        // ...and one good tick alone does NOT clear it (recovery is confirmed too).
        MonitorState oneGood = HealthMonitor.Step(band, cfg, 0.1, 0.2);
        Check("one good tick does not clear a latched fault",
              oneGood.Verdict == HealthVerdict.Failed, HealthMonitor.Name(oneGood.Verdict));

        // ---- escalation: Nominal -> Degraded -> Failed, each confirmed ----
        MonitorState deg = StepN(HealthMonitor.Fresh(), cfg, 0.6, 0.2, 6);
        Check("a sustained degrade latches Degraded, not Failed",
              deg.Verdict == HealthVerdict.Degraded, HealthMonitor.Name(deg.Verdict));
        MonitorState worse = StepN(deg, cfg, 1.0, 0.2, 6);
        Check("a degrade that worsens past the fail band escalates to Failed",
              worse.Verdict == HealthVerdict.Failed, HealthMonitor.Name(worse.Verdict));

        Check("Worst takes the more severe verdict",
              HealthMonitor.Worst(HealthVerdict.Degraded, HealthVerdict.Failed) == HealthVerdict.Failed, "");

        // ============================ FaultResponse ============================
        // None / Nominal is always Continue, whatever the domain.
        Check("no fault is Continue",
              FaultResponse.Decide(FaultKind.None, HealthVerdict.Failed, FaultDomain.Ascent) == Recovery.Continue, "");
        Check("a Nominal verdict is Continue even with a named fault",
              FaultResponse.Decide(FaultKind.ThrustShortfall, HealthVerdict.Nominal, FaultDomain.Ascent) == Recovery.Continue, "");

        // ThrustShortfall: ascent retries then re-plans; the booster reconfigures then re-plans.
        Check("ascent thrust shortfall (degraded) retries the light",
              FaultResponse.Decide(FaultKind.ThrustShortfall, HealthVerdict.Degraded, FaultDomain.Ascent) == Recovery.Retry, "");
        Check("ascent thrust shortfall (failed) re-plans on the thrust it has",
              FaultResponse.Decide(FaultKind.ThrustShortfall, HealthVerdict.Failed, FaultDomain.Ascent) == Recovery.Replan, "");
        Check("booster thrust shortfall (degraded) reconfigures the engine mode",
              FaultResponse.Decide(FaultKind.ThrustShortfall, HealthVerdict.Degraded, FaultDomain.BoosterRecovery) == Recovery.Reconfigure, "");

        // ThrustReversed: reconfigure (flip the sign) then re-plan.
        Check("reversed thrust (degraded) reconfigures - flip the sign",
              FaultResponse.Decide(FaultKind.ThrustReversed, HealthVerdict.Degraded, FaultDomain.OrbitCoast) == Recovery.Reconfigure, "");
        Check("reversed thrust (failed) re-plans the orbit",
              FaultResponse.Decide(FaultKind.ThrustReversed, HealthVerdict.Failed, FaultDomain.OrbitCoast) == Recovery.Replan, "");

        // TrajectoryDiverging: ascent aborts (escape trajectory); entry downmodes; else re-plans.
        Check("a diverging ascent (failed) ABORTS - the escape-trajectory case",
              FaultResponse.Decide(FaultKind.TrajectoryDiverging, HealthVerdict.Failed, FaultDomain.Ascent) == Recovery.Abort, "");
        Check("a diverging ascent (degraded) re-plans before it runs away",
              FaultResponse.Decide(FaultKind.TrajectoryDiverging, HealthVerdict.Degraded, FaultDomain.Ascent) == Recovery.Replan, "");
        Check("a diverging entry footprint (failed) downmodes to the reachable point",
              FaultResponse.Decide(FaultKind.TrajectoryDiverging, HealthVerdict.Failed, FaultDomain.Entry) == Recovery.Downmode, "");
        Check("a diverging deorbit re-plans the aim",
              FaultResponse.Decide(FaultKind.TrajectoryDiverging, HealthVerdict.Failed, FaultDomain.Deorbit) == Recovery.Replan, "");

        // ConvergenceStalled: retry while trying, re-plan once genuinely stalled.
        Check("a stalling burn (degraded) retries",
              FaultResponse.Decide(FaultKind.ConvergenceStalled, HealthVerdict.Degraded, FaultDomain.OrbitCoast) == Recovery.Retry, "");
        Check("a stalled burn (failed) re-plans",
              FaultResponse.Decide(FaultKind.ConvergenceStalled, HealthVerdict.Failed, FaultDomain.OrbitCoast) == Recovery.Replan, "");

        // NoControlSolution: physical descent downmodes; a planning refusal re-plans.
        Check("no landing solution (failed) downmodes - best-effort",
              FaultResponse.Decide(FaultKind.NoControlSolution, HealthVerdict.Failed, FaultDomain.BoosterRecovery) == Recovery.Downmode, "");
        Check("no landing solution (degraded) reconfigures to max authority",
              FaultResponse.Decide(FaultKind.NoControlSolution, HealthVerdict.Degraded, FaultDomain.BoosterRecovery) == Recovery.Reconfigure, "");
        Check("an unsafe planned burn re-plans a safe one",
              FaultResponse.Decide(FaultKind.NoControlSolution, HealthVerdict.Failed, FaultDomain.Deorbit) == Recovery.Replan, "");

        // KeepOutBreach: drift holds, breach aborts.
        Check("drifting toward the keep-out edge (degraded) downmodes - hold",
              FaultResponse.Decide(FaultKind.KeepOutBreach, HealthVerdict.Degraded, FaultDomain.Rendezvous) == Recovery.Downmode, "");
        Check("a keep-out breach (failed) ABORTS - the automatic retreat",
              FaultResponse.Decide(FaultKind.KeepOutBreach, HealthVerdict.Failed, FaultDomain.Rendezvous) == Recovery.Abort, "");

        // ResourceCritical: thin margin downmodes, below floor aborts.
        Check("a thin resource margin (degraded) downmodes",
              FaultResponse.Decide(FaultKind.ResourceCritical, HealthVerdict.Degraded, FaultDomain.Deorbit) == Recovery.Downmode, "");
        Check("resources below the floor (failed) safe-hold aborts",
              FaultResponse.Decide(FaultKind.ResourceCritical, HealthVerdict.Failed, FaultDomain.Deorbit) == Recovery.Abort, "");

        // SensorInvalid: degraded retries, failed aborts.
        Check("a degraded state estimate retries / re-acquires",
              FaultResponse.Decide(FaultKind.SensorInvalid, HealthVerdict.Degraded, FaultDomain.OrbitCoast) == Recovery.Retry, "");
        Check("a failed state estimate safe-holds",
              FaultResponse.Decide(FaultKind.SensorInvalid, HealthVerdict.Failed, FaultDomain.OrbitCoast) == Recovery.Abort, "");

        // the severity ordering the conductor relies on to escalate concurrent faults
        Check("recoveries are ordered by severity",
              (byte)Recovery.Continue < (byte)Recovery.Retry
              && (byte)Recovery.Retry < (byte)Recovery.Reconfigure
              && (byte)Recovery.Reconfigure < (byte)Recovery.Replan
              && (byte)Recovery.Replan < (byte)Recovery.Downmode
              && (byte)Recovery.Downmode < (byte)Recovery.Abort, "");
        Check("Worst escalates to the more severe recovery",
              FaultResponse.Worst(Recovery.Retry, Recovery.Abort) == Recovery.Abort, "");

        // ============================ ThrustDeliveryMonitor ============================
        // dt 0.2 (5 Hz). ConfirmS 1.5 -> need >7.5 ticks; 9 x 0.2 = 1.8 s latches.
        double dt = 0.2;
        int over = 9;

        // A healthy burn: delivering ~all of ~15 m/s^2 expected, on axis.
        ThrustSample healthy = new ThrustSample();
        healthy.ExpectedAccel = 15.0; healthy.DeliveredAccel = 14.5; healthy.Commanding = true;
        MonitorState hs = StepSamples(HealthMonitor.Fresh(), healthy, dt, over);
        Check("a healthy burn stays Nominal", hs.Verdict == HealthVerdict.Nominal, HealthMonitor.Name(hs.Verdict));
        Check("...and reports no fault", ThrustDeliveryMonitor.Kind(healthy, hs.Verdict) == FaultKind.None, "");
        Check("a healthy burn residual is ~0", Math.Abs(ThrustDeliveryMonitor.Residual(healthy)) < 0.05,
              ThrustDeliveryMonitor.Residual(healthy).ToString("F3"));

        // A coast: not commanding thrust. Delivered 0 must NOT be read as a fault.
        ThrustSample coast = new ThrustSample();
        coast.ExpectedAccel = 0.0; coast.DeliveredAccel = 0.0; coast.Commanding = false;
        MonitorState cs = StepSamples(HealthMonitor.Fresh(), coast, dt, 30);
        Check("a coast (engine off on purpose) never trips the monitor",
              cs.Verdict == HealthVerdict.Nominal, HealthMonitor.Name(cs.Verdict));
        Check("a coast residual is 0", ThrustDeliveryMonitor.Residual(coast) == 0.0, "");

        // The lit-but-no-thrust crash: commanding hard, delivering nothing.
        ThrustSample dead = new ThrustSample();
        dead.ExpectedAccel = 15.0; dead.DeliveredAccel = 0.0; dead.Commanding = true;
        MonitorState ds = StepSamples(HealthMonitor.Fresh(), dead, dt, over);
        Check("a dead / starved engine (lit-but-no-thrust) latches Failed",
              ds.Verdict == HealthVerdict.Failed, HealthMonitor.Name(ds.Verdict));
        Check("...isolated as a thrust SHORTFALL",
              ThrustDeliveryMonitor.Kind(dead, ds.Verdict) == FaultKind.ThrustShortfall, "");

        // The no-burn rendezvous: "Burning" but off-axis, so along-axis delivery is ~0.
        ThrustSample offAxis = new ThrustSample();
        offAxis.ExpectedAccel = 15.0; offAxis.DeliveredAccel = 0.3; offAxis.Commanding = true;
        MonitorState os = StepSamples(HealthMonitor.Fresh(), offAxis, dt, over);
        Check("an off-axis burn delivering nothing along the aim latches Failed",
              os.Verdict == HealthVerdict.Failed, HealthMonitor.Name(os.Verdict));

        // The reversed Draco: delivering the WRONG way (negative along the aim).
        ThrustSample reversed = new ThrustSample();
        reversed.ExpectedAccel = 15.0; reversed.DeliveredAccel = -8.0; reversed.Commanding = true;
        Check("a reversed delivery residual exceeds 1",
              ThrustDeliveryMonitor.Residual(reversed) > 1.0, ThrustDeliveryMonitor.Residual(reversed).ToString("F2"));
        MonitorState rs = StepSamples(HealthMonitor.Fresh(), reversed, dt, over);
        Check("a wrong-way delivery latches Failed",
              rs.Verdict == HealthVerdict.Failed, HealthMonitor.Name(rs.Verdict));
        Check("...isolated as REVERSED, not merely short",
              ThrustDeliveryMonitor.Kind(reversed, rs.Verdict) == FaultKind.ThrustReversed, "");

        // The spool: short for the first second, then it catches. Must NOT fire - this is every good burn.
        ThrustSample spool = new ThrustSample();
        spool.ExpectedAccel = 15.0; spool.DeliveredAccel = 0.0; spool.Commanding = true;   // dead-ish while spooling
        MonitorState sp = StepSamples(HealthMonitor.Fresh(), spool, dt, 5);                 // 1.0 s < 1.5 confirm
        sp = StepSamples(sp, healthy, dt, over);                                            // engine catches
        Check("a normal spool (short then catching) never trips the monitor",
              sp.Verdict == HealthVerdict.Nominal, HealthMonitor.Name(sp.Verdict));

        // A half-delivering engine sits at Degraded, not Failed.
        ThrustSample half = new ThrustSample();
        half.ExpectedAccel = 15.0; half.DeliveredAccel = 6.0; half.Commanding = true;       // frac 0.4 -> residual 0.6
        MonitorState hf = StepSamples(HealthMonitor.Fresh(), half, dt, over);
        Check("an engine delivering ~40% latches Degraded, not Failed",
              hf.Verdict == HealthVerdict.Degraded, HealthMonitor.Name(hf.Verdict));

        // Over-delivery is never a fault.
        ThrustSample overDeliver = new ThrustSample();
        overDeliver.ExpectedAccel = 15.0; overDeliver.DeliveredAccel = 18.0; overDeliver.Commanding = true;
        Check("over-delivering is not a fault", ThrustDeliveryMonitor.Residual(overDeliver) == 0.0, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
