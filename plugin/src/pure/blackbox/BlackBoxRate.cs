// DragonScreen — BlackBox / RATE LADDER  (register BB1; spec: docs/BLACKBOX_RESEARCH.md §2.0 + §4.6)
// ============================================================================================
// PURE, and deliberately the ONLY place in the recorder that decides WHEN a row is due and WHICH tiers
// fill it. Everything else — the schema, the fillers, the glue — asks this file and does as it is told.
//
// ---- ⚠ THE OPEN OWNER QUESTION LIVES HERE, AND IS NOT DECIDED HERE (C1.14) ----
// `REGISTER.md` BB1-Q1 (= `docs/BLACKBOX_RESEARCH.md` §6.1 Q2) asks FIXED or ADAPTIVE row rate:
//   (a) adaptive, as specified — 10 Hz while a dynamic phase is active, 2 Hz otherwise
//   (b) fixed 10 Hz throughout — ~5x the file size
//   (c) fixed 5 Hz throughout — smallest, halves the §B8 AoA/Q resolution the whole tune depends on
// §6.1 files it "no gate; a design preference with a size consequence", and it is still OPEN.
//
// What is built here is (a) — because (a) is what §2.0 SPECIFIES and what BB1's own done-criteria
// require ("the A-I columns at their specified rates"). Building the written spec is not deciding the
// question; the question is whether to CHANGE the spec, and only the owner does that.
//
// So the policy is one struct with one `Mode`, and (b)/(c) are a value, not a rewrite:
//   `Policy.Adaptive()` / `Policy.Fixed(10.0)` / `Policy.Fixed(5.0)`.
// The glue picks it from a single `[Tunable]`, so an owner ruling for (b) or (c) is a one-line change
// — or a `PluginData/tuning.cfg` edit with no rebuild at all. Whichever way the ruling goes, no other
// file in the recorder changes, and the manifest records what was actually flown either way.
//
// ---- WHY THE WARP RULE IS IN THIS FILE AND NOT IN THE GLUE ----
// §4.6: rates are defined in UT, but under on-rails warp UT advances up to 1000x per FRAME, so a
// UT-only rule would write ~50 rows per WALL second of nothing happening. The rule is therefore
// `interval = max(tier interval, 1.0 s of WALL clock)` while on rails, and the file stays proportional
// to TIME SPENT rather than to GAME TIME ELAPSED. That is a decision about cadence, so it belongs
// beside the cadence, where it is headless-testable — and S76's evidence is exactly why: warp rows
// polluted every statistic in the old corpus badly enough that `is_warp()` filtering had to be
// retrofitted into `assess_flight.py` after the fact.
// ============================================================================================
using System;

namespace DragonScreen.BlackBox
{
    public enum RateMode : byte
    {
        /// <summary>§2.0 as written: R1 (10 Hz) while a dynamic phase is active, R2 (2 Hz) otherwise.</summary>
        Adaptive = 0,
        /// <summary>One rate throughout, whatever the phase. BB1-Q1 options (b) and (c).</summary>
        Fixed = 1
    }

    public struct RatePolicy
    {
        public RateMode Mode;
        /// <summary>Row period while a dynamic phase is active (Adaptive), or always (Fixed). UT seconds.</summary>
        public double DynamicPeriodS;
        /// <summary>Row period otherwise. Equals DynamicPeriodS under Fixed.</summary>
        public double QuiescentPeriodS;
        /// <summary>§4.6's on-rails floor, in WALL seconds. 0 disables the floor.</summary>
        public double WarpWallFloorS;

        public static RatePolicy Adaptive()
        {
            RatePolicy p;
            p.Mode = RateMode.Adaptive;
            p.DynamicPeriodS = BlackBoxSchema.R1IntervalS;    // 10 Hz
            p.QuiescentPeriodS = BlackBoxSchema.R2IntervalS;  //  2 Hz
            p.WarpWallFloorS = 1.0;
            return p;
        }

        public static RatePolicy Fixed(double hz)
        {
            RatePolicy p;
            p.Mode = RateMode.Fixed;
            double period = (hz > 1e-6) ? 1.0 / hz : BlackBoxSchema.R1IntervalS;
            p.DynamicPeriodS = period;
            p.QuiescentPeriodS = period;
            p.WarpWallFloorS = 1.0;
            return p;
        }

        public double RowRateDynamicHz { get { return DynamicPeriodS > 1e-9 ? 1.0 / DynamicPeriodS : 0.0; } }
        public double RowRateQuiescentHz { get { return QuiescentPeriodS > 1e-9 ? 1.0 / QuiescentPeriodS : 0.0; } }
    }

    /// <summary>What the glue knows about the tick, reduced to the facts the cadence depends on.</summary>
    public struct RateInputs
    {
        public double Ut;
        public double Wall;
        /// <summary>§2.0's dynamic-phase rule, already evaluated by the glue (see <see cref="IsDynamic"/>).</summary>
        public bool Dynamic;
        /// <summary>On-RAILS warp specifically — physics is off, so §4.6 both voids control and floors the rate.</summary>
        public bool RailsWarp;
    }

    /// <summary>The last emission times a stream carries between ticks.</summary>
    public struct RateState
    {
        public double LastRowUt, LastRowWall;
        public double LastR2Ut, LastR3Ut;
        public bool Started;

        public static RateState Fresh()
        {
            RateState s;
            // ⛔ NOT 0. A revert moves UT BACKWARDS and a fresh scene starts at whatever UT the save
            // holds, so "never emitted" has to be a sentinel no real clock can equal — Recorder B used
            // -1e9 for the same reason and for the same failure (the first sample not firing).
            s.LastRowUt = -1e9; s.LastRowWall = -1e9; s.LastR2Ut = -1e9; s.LastR3Ut = -1e9;
            s.Started = false;
            return s;
        }
    }

    /// <summary>Which decimated tiers this row carries. Every/R0/R1 ride every row by construction.</summary>
    public struct RowPlan
    {
        public bool Due;
        /// <summary>True when the row's own period is the dynamic one — recorded so a reader can see the switch.</summary>
        public bool Dynamic;
        public bool FillR2, FillR3;
    }

    public static class BlackBoxRate
    {
        /// <summary>
        /// §2.0's dynamic-phase rule, in one place: "Ascent, Entry, Drogues, Mains, any abort, any
        /// powered burn (thrust_n > 0 or RCS translation commanded), and Approach inside 1 km".
        ///
        /// It is pure and takes plain values rather than a Vessel so the whole ladder is headless
        /// testable — the glue supplies the four facts and nothing else.
        /// </summary>
        public static bool IsDynamic(MissionPhase phase, bool aborting, double thrustN,
                                     bool transCommanded, double targetRangeM, bool hasTarget)
        {
            if (aborting) return true;
            if (thrustN > 0.0) return true;
            if (transCommanded) return true;
            switch (phase)
            {
                case MissionPhase.Ascent:
                case MissionPhase.Entry:
                case MissionPhase.Drogues:
                case MissionPhase.Mains:
                    return true;
                case MissionPhase.Approach:
                    return hasTarget && targetRangeM <= 1000.0;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Is a row due, and which decimated tiers does it carry? Pure: state in, plan out, no mutation.
        /// <para/>
        /// The R2/R3 decision is made INDEPENDENTLY of the row decision and against each tier's own
        /// elapsed UT, which is what makes §4.6's forward-fill unambiguous: a decimated column is
        /// written on the rows where its own period has elapsed and blank on every other row, and the
        /// manifest's `period_s` tells a reader exactly how far forward to fill.
        /// </summary>
        public static RowPlan Plan(RatePolicy policy, RateState state, RateInputs now)
        {
            RowPlan p = new RowPlan();
            p.Dynamic = (policy.Mode == RateMode.Fixed) || now.Dynamic;

            double period = p.Dynamic ? policy.DynamicPeriodS : policy.QuiescentPeriodS;

            // First row of a stream always fires — LastRowUt's sentinel guarantees it, but a revert can
            // also put `now.Ut` BEHIND `LastRowUt`, and then `now.Ut - LastRowUt` is negative and the
            // row would never come due again. Treat backwards time as "due": the glue is about to
            // detect the revert and branch the mission anyway, and a missing row while it does would be
            // the one row that shows the discontinuity.
            double dUt = now.Ut - state.LastRowUt;
            bool utDue = !state.Started || dUt < 0.0 || dUt >= period - 1e-9;

            if (now.RailsWarp && policy.WarpWallFloorS > 0.0)
            {
                // §4.6: on rails, UT runs up to 1000x per frame. Gate on the WALL clock instead, so a
                // phasing coast at 100x writes ~1 row per wall-second and the file stays proportional
                // to time SPENT. The UT condition still has to hold — the floor lengthens the interval,
                // it never shortens it.
                double dWall = now.Wall - state.LastRowWall;
                bool wallDue = !state.Started || dWall < 0.0 || dWall >= policy.WarpWallFloorS - 1e-9;
                p.Due = utDue && wallDue;
            }
            else
            {
                p.Due = utDue;
            }

            if (!p.Due) return p;

            double d2 = now.Ut - state.LastR2Ut;
            double d3 = now.Ut - state.LastR3Ut;
            p.FillR2 = !state.Started || d2 < 0.0 || d2 >= BlackBoxSchema.R2IntervalS - 1e-9;
            p.FillR3 = !state.Started || d3 < 0.0 || d3 >= BlackBoxSchema.R3IntervalS - 1e-9;
            return p;
        }

        /// <summary>Advance the state after a row has actually been WRITTEN — never before.</summary>
        public static RateState Advance(RateState state, RowPlan plan, RateInputs now)
        {
            state.LastRowUt = now.Ut;
            state.LastRowWall = now.Wall;
            if (plan.FillR2) state.LastR2Ut = now.Ut;
            if (plan.FillR3) state.LastR3Ut = now.Ut;
            state.Started = true;
            return state;
        }
    }
}
