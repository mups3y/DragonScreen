/*
 * DragonScreen - RendezvousProgress (PURE)
 *
 * The concrete Layer-3 monitor that answers "is the rendezvous actually getting anywhere, or is it
 * STUCK?" - the fault the last flight had no autonomous answer to. On flight_0826_014654 the CLOSE burn
 * could not reorient (the capsule was capped at the ascent slew rate), the orbit sat frozen, and a HUMAN
 * had to notice and press CANCEL. A true autopilot notices that itself and acts. This is the detector;
 * RendezvousFdir is the responder that drives FaultResponse's ladder from its verdict.
 *
 * ---- WHAT COUNTS AS PROGRESS (and why the definition is what it is) ----
 * The rendezvous is a long sequence of warps, burns and coasts, so "not closing THIS tick" is normal for
 * almost every tick - a naive range check would false-alarm constantly. Progress is therefore ANY of the
 * real ways the autopilot advances the mission, and the stall clock resets on any of them:
 *
 *   WARP ACTIVE        rails warp toward a phase/arrival gate IS the coast closing - the drift and the
 *                      phasing wait are warped, and the slant range oscillates hugely (200<->13000 km at
 *                      419 km) while the phase closes, so range alone is useless there; the warp is the tell.
 *   DELIVERING         a node is active and its remaining dv is falling - a burn is putting in real dv.
 *   SLEWING            a node is aligning and its pointing error is falling - the turn is happening.
 *   LEG ADVANCED       Phase->Boost->Close->... the sequence stepped forward.
 *   NEW CLOSEST RANGE  a new minimum slant range - genuine net closing.
 *
 * A FROZEN rendezvous is the absence of all five for a sustained REAL-TIME window: engaged, but not
 * warping, no burn delivering, no slew, the leg static and no new closest range. That is exactly the
 * stuck CLOSE-burn state, and it is what a watching human calls "it is not doing anything".
 *
 * ---- SCOPE: FROZEN, NOT MERELY SLOW ----
 * This flags a rendezvous that has STOPPED, not one that is progressing slowly (a slow-but-falling pErr
 * still counts as slewing). Slowness is a performance question the slew-rate fix already answers; the
 * FDIR is the safety net for a HUNG or diverging plan, so it must not fire on healthy-but-unhurried work.
 *
 * ---- THE VERDICT IS HealthMonitor's, FED A STALL-SECONDS RESIDUAL ----
 * The stall clock (seconds frozen) IS the residual: Degraded at RetryStallS, Failed at AbortStallS, with
 * no extra confirmation delay (the clock already IS the persistence filter) and an immediate clear when
 * progress resumes. So this reuses the tested HealthMonitor debounce rather than re-implementing it.
 * Caller-owned state, pure Step - the Attitude.cs / HealthMonitor discipline.
 */
namespace DragonScreen
{
    /// <summary>Thresholds for the rendezvous-progress monitor. Real-time seconds frozen.</summary>
    public struct RvProgressCfg
    {
        /// <summary>A new closest range must beat the best by this to count as progress, metres. Filters
        /// the per-orbit slant-range oscillation so only genuine net closing resets the clock.</summary>
        public double RangeProgressM;
        /// <summary>Pointing error must fall by this from its recent worst to count as slewing, degrees.</summary>
        public double PErrProgressDeg;
        /// <summary>Seconds frozen before the verdict is Degraded - the first, local recovery (re-plan).</summary>
        public double RetryStallS;
        /// <summary>Seconds frozen before the verdict is Failed - escalate to abort-to-home.</summary>
        public double AbortStallS;
    }

    /// <summary>One tick of rendezvous state for the monitor. The glue fills it from the engaged
    /// controllers; all pure so a test can drive a whole stall/recover cycle deterministically.</summary>
    public struct RvProgressSample
    {
        /// <summary>A rendezvous is being flown (any of the approach controllers engaged).</summary>
        public bool Engaged;
        /// <summary>Rails time-warp is running - coasting toward a gate, which is progress.</summary>
        public bool WarpActive;
        /// <summary>A node burn is active (aligning, holding or burning).</summary>
        public bool NodeActive;
        /// <summary>Remaining dv of the active node, m/s - falling means delivering.</summary>
        public double RemainingDvMps;
        /// <summary>Pointing error of the active node, deg - falling means slewing toward the aim.</summary>
        public double PointingErrorDeg;
        /// <summary>Slant range to the station, metres - a new minimum is net closing.</summary>
        public double RangeM;
        /// <summary>Monotonic-ish leg index (Phase..Arrived, terminal high) - an increase is a step forward.</summary>
        public int LegIndex;
    }

    /// <summary>Carried state. default(RvProgressState) / Fresh() is a healthy, unseeded monitor - the
    /// reset the glue uses when the rendezvous disengages. Baselines track the recent worst of each signal
    /// so cumulative delivery/slew over the stall window counts, not just a single-tick delta.</summary>
    public struct RvProgressState
    {
        public bool Seeded;
        /// <summary>Best (smallest) slant range seen, metres.</summary>
        public double BestRangeM;
        /// <summary>Highest leg index reached.</summary>
        public int BestLeg;
        /// <summary>Recent-worst remaining dv, the baseline delivery is measured down from.</summary>
        public double DeliverBaselineDv;
        /// <summary>Recent-worst pointing error, the baseline a slew is measured down from.</summary>
        public double SlewBaselineDeg;
        /// <summary>Seconds frozen (no progress) - the residual fed to the verdict.</summary>
        public double StallS;
        /// <summary>The debounced verdict (HealthMonitor).</summary>
        public MonitorState Health;

        public HealthVerdict Verdict { get { return Health.Verdict; } }
    }

    public static class RendezvousProgress
    {
        /// <summary>Frozen 90 s -> re-plan; frozen 300 s -> abort-to-home. Generous: a truly frozen
        /// rendezvous for a minute and a half is unambiguous, and no healthy activity (warp/burn/slew/
        /// advance) ever accumulates it. Range 500 m clears slant noise; pErr 1 deg clears jitter.</summary>
        public static RvProgressCfg Default()
        {
            RvProgressCfg c;
            c.RangeProgressM = 500.0;
            c.PErrProgressDeg = 1.0;
            c.RetryStallS = 90.0;
            c.AbortStallS = 300.0;
            return c;
        }

        public static RvProgressState Fresh() { return new RvProgressState(); }

        /// <summary>The monitor's HealthMonitor config from the stall thresholds. Degrade/Fail on the
        /// stall seconds; clear the instant progress drops the residual to 0 (no confirmation lag - the
        /// stall clock is itself the persistence filter).</summary>
        public static MonitorConfig HealthCfg(RvProgressCfg cfg)
        {
            return HealthMonitor.Config(cfg.RetryStallS, cfg.AbortStallS, 1.0, 0.0, 0.0);
        }

        public static FaultKind Kind(RvProgressState st)
        {
            return st.Verdict == HealthVerdict.Nominal ? FaultKind.None : FaultKind.ConvergenceStalled;
        }

        public static RvProgressState Step(RvProgressState prev, RvProgressSample s, RvProgressCfg cfg,
                                           double dt)
        {
            if (!s.Engaged) return Fresh();
            if (dt < 0.0) dt = 0.0;

            RvProgressState st = prev;
            if (!st.Seeded)
            {
                st.Seeded = true;
                st.BestRangeM = s.RangeM;
                st.BestLeg = s.LegIndex;
                st.DeliverBaselineDv = s.RemainingDvMps;
                st.SlewBaselineDeg = s.PointingErrorDeg;
                st.StallS = 0.0;
                st.Health = HealthMonitor.Fresh();
            }

            // ---- re-baseline the delta signals when they WORSEN (a new burn re-arms remaining dv, a slew
            // diverges) so subsequent good delivery/slew is measured from the fresh worst, not an old low.
            if (s.NodeActive && s.RemainingDvMps > st.DeliverBaselineDv) st.DeliverBaselineDv = s.RemainingDvMps;
            if (s.NodeActive && s.PointingErrorDeg > st.SlewBaselineDeg) st.SlewBaselineDeg = s.PointingErrorDeg;
            if (!s.NodeActive) { st.DeliverBaselineDv = 0.0; st.SlewBaselineDeg = 0.0; }

            bool newMin = s.RangeM < st.BestRangeM - cfg.RangeProgressM;
            bool legAdv = s.LegIndex > st.BestLeg;
            bool delivering = s.NodeActive && s.RemainingDvMps < st.DeliverBaselineDv - 0.01;
            bool slewing = s.NodeActive && s.PointingErrorDeg < st.SlewBaselineDeg - cfg.PErrProgressDeg;
            bool progress = s.WarpActive || newMin || legAdv || delivering || slewing;

            if (progress)
            {
                st.StallS = 0.0;
                // A good move re-baselines the delta signals to the new value, so the NEXT bit of
                // delivery/slew has to be fresh progress rather than crediting the same drop twice.
                if (delivering) st.DeliverBaselineDv = s.RemainingDvMps;
                if (slewing) st.SlewBaselineDeg = s.PointingErrorDeg;
            }
            else st.StallS += dt;

            if (s.RangeM < st.BestRangeM) st.BestRangeM = s.RangeM;
            if (s.LegIndex > st.BestLeg) st.BestLeg = s.LegIndex;

            st.Health = HealthMonitor.Step(st.Health, HealthCfg(cfg), st.StallS, dt);
            return st;
        }
    }
}
