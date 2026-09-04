// DragonScreen — BlackBox / LATENCY DISTRIBUTION  (register BB7; spec: docs/BLACKBOX_RESEARCH.md §4.7)
// ============================================================================================
// PURE. BB7 found that `max_rec_build_us` alone misreports a well-behaved recorder as 23x over its own
// frame budget: the confirm flight's median `rec_build_us` was 63 us against a 2000 us budget (32x
// UNDER), and the reported 47544 us max was ONE row (seq=2848 of 3400) that correlates with nothing in
// `events.jsonl` and is not a flush boundary — a GC pause, not a sustained cost. A single MAX cannot
// tell a reader a spike from a regression; a DISTRIBUTION can.
//
// ---- WHY A HISTOGRAM, NOT THE RAW SAMPLES ----
// `rec_build_us` is measured on every row for the life of the mission — thousands of samples on a
// short flight, and there is no bound on mission length. Storing every value (as `BlackBoxRecorder`
// already deliberately does NOT do for its CSV text, which is flushed on a fixed cadence rather than
// held for the whole mission) would make the manifest's memory footprint scale with flight duration.
// A log-bucketed histogram is O(1) per sample and O(buckets) total regardless of how long the mission
// runs, at the cost of resolution — which BB7's own question ("spike or sustained?") does not need
// exact percentiles to answer.
//
// ---- BUCKET WIDTH ----
// 8 buckets per octave (~9% width, i.e. `Percentile` is accurate to within ~9% of the true value) —
// checked against the overseer's own relayed numbers: median 63 us buckets to 64, p90 176 us buckets to
// ~181, p99 360 us buckets to ~362. Coarser (power-of-2, 1 bucket/octave) rounded p90/p99 by ~40-45%,
// which is too coarse for a reader to trust the reported curve; finer buys accuracy this question does
// not need. 32 octaves (up to 2^32 us, ~71 minutes on a single row) at 8/octave is 256 `long` counters
// — 2 KB, allocated once at stream open.
//
// ---- THE ROW-1 QUESTION (BB7's "decide and state") ----
// Row 1 is warm-up (JIT/first-allocation) and decays immediately — the confirm flight's own rows 2 and
// 3 were 692 us and 392 us, an order of magnitude down already. It is NOT excluded here. Two reasons:
// (1) `MaxRecBuildUs` already does not need it excluded — on the confirm flight the max was row 2848's
// 47544 us, not row 1's 7267 us, so the existing worst-case figure was never row-1's artefact to begin
// with. (2) a percentile's sensitivity to any one sample is bounded by 1/N; with N in the thousands
// (3400 on the confirm flight), row 1 can move a reported percentile by at most one bucket, usually not
// even that. Special-casing "skip sample #1" would be a hardcoded exception earning less than a rounding
// error — so row 1 is recorded like every other row, and this paragraph is the record of that decision.
// ============================================================================================
using System;

namespace DragonScreen.BlackBox
{
    /// <summary>
    /// Online, bounded-memory percentile estimate for a per-row cost distribution. `Record` is the only
    /// per-tick cost (a log2 and an array increment); `Percentile`/`Max`/`Count` are read once, at close.
    /// </summary>
    public struct LatencyHistogram
    {
        const int BucketsPerOctave = 8;
        const int Octaves = 32;
        public const int Buckets = BucketsPerOctave * Octaves;

        long[] count;
        long n;
        double max;

        public long Count { get { return n; } }
        public double Max { get { return max; } }

        public static LatencyHistogram Fresh()
        {
            LatencyHistogram h = new LatencyHistogram();
            h.count = new long[Buckets];
            return h;
        }

        public void Record(double us)
        {
            if (count == null) count = new long[Buckets]; // defensive: a default-constructed struct
            n++;
            if (us > max) max = us;
            count[BucketOf(us)]++;
        }

        static int BucketOf(double us)
        {
            if (us <= 0.0) return 0;
            int b = (int)Math.Floor(Math.Log(us, 2.0) * BucketsPerOctave);
            if (b < 0) b = 0;
            if (b >= Buckets) b = Buckets - 1;
            return b;
        }

        /// <summary>The upper edge of the bucket a value of exactly this many microseconds falls in.</summary>
        static double UpperBoundOf(int bucket)
        {
            return Math.Pow(2.0, (bucket + 1.0) / BucketsPerOctave);
        }

        /// <summary>
        /// The smallest bucket upper bound such that at least a `p` fraction (0..1) of recorded samples
        /// are at or below it. Returns 0 on an empty histogram — there is nothing to report, not a
        /// fabricated zero cost (§14.4(e)'s "blank, never a guess", applied to an aggregate rather than
        /// a cell).
        /// </summary>
        public double Percentile(double p)
        {
            if (n <= 0 || count == null) return 0.0;
            if (p < 0.0) p = 0.0;
            if (p > 1.0) p = 1.0;
            long target = (long)Math.Ceiling(p * n);
            if (target < 1) target = 1;
            long running = 0;
            for (int i = 0; i < Buckets; i++)
            {
                running += count[i];
                // The bucket's UPPER edge is an ESTIMATE and can round past the true max for a sample
                // that lands near its bucket's top — clamped here so "p99" can never read as a bigger
                // number than "max" in the same manifest, which would read as a contradiction to a
                // reader even though both are honest.
                if (running >= target) return Math.Min(UpperBoundOf(i), max);
            }
            return max;
        }
    }
}
