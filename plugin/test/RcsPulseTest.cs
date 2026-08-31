// Tests for pure/RcsPulse.cs (Tier-2) — PWPF / delta-sigma RCS pulse modulation.
// The modulator must: (1) command NOTHING inside the deadband (kill the limit cycle), (2) pass a near-full
// command through as CONTINUOUS thrust (don't chop a sustained burn), (3) have a TIME-AVERAGE that tracks a
// mid-range command in both signs, (4) never fire opposing thrusters across a sign flip, (5) only emit the
// three valid states {−1,0,+1}.
using System;
using DragonScreen;

public static class RcsPulseTest
{
    static int checks = 0, failures = 0;
    static void Near(string what, double got, double want, double tol)
    { checks++; if (Math.Abs(got - want) > tol) { failures++; Console.WriteLine("  FAIL  " + what + "   got " + got.ToString("F4") + " want " + want.ToString("F4")); } }
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }

    // run a held command for N ticks, return the average output
    static double AvgHold(double cmd, int n, double dt, double db, double mn, double mf, double full)
    {
        var st = RcsPulseState.Fresh;
        long sum = 0; bool onlyValid = true;
        for (int i = 0; i < n; i++)
        {
            int o = RcsPulse.Step(ref st, cmd, dt, db, mn, mf, full);
            if (o < -1 || o > 1) onlyValid = false;
            sum += o;
        }
        if (!onlyValid) Check("outputs are {-1,0,1}", false, "cmd=" + cmd);
        return (double)sum / n;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen Tier-2 RcsPulse (PWPF) tests");
        double dt = 0.02, db = 0.05, mn = 0.06, mf = 0.06, full = 0.9;
        int N = 6000;

        // (1) DEADBAND: a tiny command commands nothing at all.
        Near("deadband: |cmd|<db → average 0", AvgHold(0.03, N, dt, db, mn, mf, full), 0.0, 1e-9);

        // (1b) LIMIT-CYCLE KILL: a jittering sub-deadband command never fires.
        {
            var st = RcsPulseState.Fresh; long fires = 0;
            for (int i = 0; i < N; i++) { int o = RcsPulse.Step(ref st, (i % 2 == 0 ? 0.03 : -0.03), dt, db, mn, mf, full); if (o != 0) fires++; }
            Check("sub-deadband jitter never fires the Dracos", fires == 0, "fires=" + fires);
        }

        // (2) FULL passthrough: a near-full command is continuous ON every tick (sustained burn intact).
        {
            var st = RcsPulseState.Fresh; bool allOn = true;
            for (int i = 0; i < 500; i++) if (RcsPulse.Step(ref st, 0.95, dt, db, mn, mf, full) != 1) allOn = false;
            Check("full command → continuous +1 (burn not chopped)", allOn, "");
            Near("full command average = 1", AvgHold(1.0, N, dt, db, mn, mf, full), 1.0, 1e-9);
        }

        // (3) AVERAGE TRACKS the command, both signs.
        Near("average tracks +0.40", AvgHold(0.40, N, dt, db, mn, mf, full), 0.40, 0.06);
        Near("average tracks +0.70", AvgHold(0.70, N, dt, db, mn, mf, full), 0.70, 0.06);
        Near("average tracks -0.55", AvgHold(-0.55, N, dt, db, mn, mf, full), -0.55, 0.06);
        Near("average tracks +0.15", AvgHold(0.15, N, dt, db, mn, mf, full), 0.15, 0.06);

        // (4) SIGN FLIP: must pass through 0 — never jump +1 → −1 in one tick (no opposing-thruster fire).
        // It legitimately honours min-on (a live + pulse is not chopped mid-dwell), then coasts to 0, then fires −.
        {
            var st = RcsPulseState.Fresh;
            for (int i = 0; i < 50; i++) RcsPulse.Step(ref st, 0.6, dt, db, mn, mf, full);  // establish +firing
            int prev = st.Output; bool badJump = false; long sum = 0;
            for (int i = 0; i < 60; i++)
            {
                int o = RcsPulse.Step(ref st, -0.6, dt, db, mn, mf, full);
                if (prev == 1 && o == -1) badJump = true;   // a direct +→− = opposing fire in one tick
                prev = o; sum += o;
            }
            Check("sign flip passes through 0 (no +→− opposing fire)", !badJump, "");
            Check("after the flip the average goes negative", sum < 0, "sum=" + sum);
        }

        // (5) MIN dwell: a pulse, once started, lasts at least minOn (no 1-tick buzzing) — check the on-run length.
        {
            var st = RcsPulseState.Fresh; int run = 0, minRun = int.MaxValue; int prev = 0;
            for (int i = 0; i < N; i++)
            {
                int o = RcsPulse.Step(ref st, 0.5, dt, db, mn, mf, full);
                if (o == 1) run++;
                else { if (prev == 1 && run > 0 && run < minRun) minRun = run; run = 0; }
                prev = o;
            }
            int minTicks = (int)Math.Floor(mn / dt);  // 0.06/0.02 = 3
            Check("on-pulses last >= minOn (no buzzing)", minRun == int.MaxValue || minRun >= minTicks, "minRun=" + minRun + " need>=" + minTicks);
        }

        // (6) SATURATED demand still burns: a full-magnitude command that only flips sign occasionally passes
        // through as (near-)continuous thrust — PWPF's FULL threshold does NOT throttle a saturated command, it only
        // rescues sub-full trim. So a limit cycle that SATURATES the controller is NOT killed by the deadband
        // (contrast the sub-deadband jitter in (1b), which fired 0). This is the DS-ASC-004 OPEN QUESTION the
        // instrumented re-fly must answer: is the terminal attitude DEMAND saturated (→ burns despite PWPF) or
        // sub-full (→ pulsed)? The recorded pre-pulse act_* cannot distinguish it from delivered firing.
        {
            var st = RcsPulseState.Fresh; long on = 0; int flip = 40;   // hold ±1 for `flip` ticks, then reverse
            for (int i = 0; i < N; i++) { double cmd = ((i / flip) % 2 == 0) ? 1.0 : -1.0; if (RcsPulse.Step(ref st, cmd, dt, db, mn, mf, full) != 0) on++; }
            Check("saturated ±1 demand fires most ticks (PWPF does NOT throttle a full command)", on > (long)(0.85 * N), "on=" + on + "/" + N);
        }

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
