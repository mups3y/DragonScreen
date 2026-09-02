// DragonScreen — SuitLeakSim  (PURE: the 4.011 Suit Leak Check's suit model)
// ============================================================================================
// ---- THIS IS A SIMULATION, AND IT IS MARKED AS ONE (BUILD_PLAN §14.4(e), owner, 2026-09-02) ----
// The four SUIT n DELTA PRESSURE rows and the four SUIT n STATUS words used to be, in order: a
// representative constant ("0.01psi"), then a permanent dash (T13c — nothing modelled a suit), with
// STATUS reading a confident green "Nominal" the whole time (S31/S22: a screen stating a verdict it
// could not know). §14.4(e) superseded the dash: a physically-real quantity that is simply not
// modelled yet gets an installed mod's value if one exists, else a COHERENT simulation driven off
// real vessel state and MARKED in code. No mod models a pressure suit, so this file is the model.
//
// ---- WHAT IS REAL AND WHAT IS SIMULATED ----
//     REAL       CABIN PRESSURE. It comes in through PageState.Cabin (pure/CabinEnvironment), which
//                is itself driven by real TAC Life Support state via LifeSupportBridge. Every
//                differential below is measured against it, so all four rows MOVE when the cabin
//                moves — the §14.4(e) test, and the reason this is not a constant with a unit on it.
//     SIMULATED  The SUIT side: one regulated suit-loop pressure (SuitLoopPsia), a small stated
//                per-suit fit offset so four suits are four readings rather than one repeated four
//                times (the same idiom as CabinEnvironment's 55/45 bus split), and the bleed-down of
//                a leaking suit through the timed check.
//     ROLLED     Whether this run finds a leak at all: the owner's 5% per-run chance. It is not a
//                loose RNG — LeakingSuit() is a pure function of the run's SEED, so the same run
//                always reaches the same verdict, two screens showing one run agree, and the preview
//                and the headless tests DRIVE both branches instead of waiting for one (SeedForLeak).
//                The glue mints the seed from the real clock at the moment a run BEGINS — INITIATE,
//                TRY ADDITIONAL TIMER, or (S32) a TROUBLESHOOT repair.
//
// ---- THE VERDICT IS COMPUTED, NEVER STATED (§14.4(e) GUARDRAIL) ----
// SuitCheckState.Failed() is a threshold on the simulated differential, so a STATUS word can only say
// "Nominal" while the model actually shows a suit holding pressure. That is the whole point of S31:
// a safety verdict must follow the simulation honestly, and there is no path here that writes one in.
// With no feed at all (Valid false) there is no verdict either — the page dashes, as it does anywhere
// else the vessel cannot be read.
// ============================================================================================
namespace DragonScreen
{
    /// <summary>What the suit model is driven by. CabinPressPsia is the real (TAC-LS-driven) one.</summary>
    public struct SuitSimInputs
    {
        /// <summary>There is a vessel to read. False = no feed, so no reading and no verdict.</summary>
        public bool Valid;
        /// <summary>Cabin pressure, psia, from CabinEnvironment. REAL state; the differential is measured
        /// against it, which is what makes these rows move.</summary>
        public double CabinPressPsia;
        /// <summary>The real procedure countdown, 5..0. How far through the timed check this run is.</summary>
        public int Countdown;
        /// <summary>The run has produced its result (the popup is up, by countdown or by FINISH).</summary>
        public bool Complete;
        /// <summary>This run's roll seed. 0 = no run has been made, so nothing has been found.</summary>
        public uint RunSeed;
    }

    /// <summary>The four differentials and the verdict they support. No strings: the page formats.</summary>
    public struct SuitCheckState
    {
        /// <summary>A cabin was readable, so there are readings to show.</summary>
        public bool Valid;
        /// <summary>The run has finished. The page uses it for nothing today; kept because the leak
        /// popup and the table are two surfaces on one run and must not disagree about that.</summary>
        public bool Complete;
        /// <summary>Which suit this run found a leak in: 1..4, or 0 for a clean run.</summary>
        public int LeakSuit;
        /// <summary>Suit-minus-cabin differential per suit, psi.</summary>
        public double D0, D1, D2, D3;

        public bool Leak { get { return LeakSuit >= 1 && LeakSuit <= 4; } }

        /// <summary>Suit i's differential, i = 0..3.</summary>
        public double Delta(int i)
        {
            switch (i) { case 0: return D0; case 1: return D1; case 2: return D2; case 3: return D3; }
            return 0.0;
        }

        /// <summary>THE VERDICT, and it is computed rather than stated: a suit whose differential has
        /// fallen below the pass threshold did not hold pressure. No feed = no verdict at all.</summary>
        public bool Failed(int i) { return Valid && Delta(i) < SuitLeak.PassPsi; }

        /// <summary>Did ANY suit fail? The fail branch's own printed question ("Did any suit fail the
        /// leak check?") answered from the model, and since S32 it is what makes TROUBLESHOOT live: the
        /// page lights the control from it, SuitCheckPage.Available gates the press on it and the glue
        /// acts only when it agrees, so a lit control and a live control are the same control and
        /// neither can appear while the model says all four suits are holding.</summary>
        public bool AnyFailed { get { return Failed(0) || Failed(1) || Failed(2) || Failed(3); } }
    }

    public static class SuitLeak
    {
        // ---- THE MODEL'S CONSTANTS. Stated here, once, so the numbers are arguable. ----
        /// <summary>SIMULATED. The absolute pressure the suit loop is regulated to while the check runs.
        /// Above a nominal 14.7 psia cabin it gives a ~0.3 psi differential — enough for the check to
        /// have something to lose, and clear of the cabin's own +-0.06 psi wander so a healthy suit
        /// cannot drift into a failure.</summary>
        public const double SuitLoopPsia = 15.00;
        /// <summary>The pass threshold. Below this the suit did not hold pressure: "Failed Low", the
        /// wording the page's reconstructed fail branch already uses (§14.4(d)).</summary>
        public const double PassPsi = 0.10;
        /// <summary>SIMULATED. How far a leaking suit's differential bleeds down over a full check —
        /// far enough to end well under PassPsi, gradually enough that step 2.4's "monitor suit delta
        /// pressure" is a real instruction: the row falls while the crew watches it.</summary>
        public const double LeakFallPsi = 0.28;
        /// <summary>The owner's per-run chance that the check finds a leak (2026-09-02, via the
        /// overseer). Rolled from the run seed, never from a loose RNG — see LeakingSuit.</summary>
        public const double LeakChance = 0.05;
        /// <summary>The countdown's own step count (5..0), which is what "how far through" is measured in.</summary>
        public const int RunSteps = 5;

        /// <summary>SIMULATED. Per-suit fit/volume differences, psi. Four suits on one loop in one cabin
        /// would otherwise read four identical numbers, which looks like one value copied four times
        /// rather than four sensors. Small, fixed, and stated — the READING still moves only because
        /// the cabin does.</summary>
        static readonly double[] Fit = { 0.000, -0.021, +0.014, -0.009 };

        /// <summary>Build the state from a page's vessel feed plus the run state the painter owns.</summary>
        public static SuitCheckState From(PageState s, int countdown, bool complete, uint seed)
        {
            SuitSimInputs i = new SuitSimInputs();
            i.Valid = s.Valid;
            i.CabinPressPsia = s.Cabin.PressPsia;
            i.Countdown = countdown;
            i.Complete = complete;
            i.RunSeed = seed;
            return Compute(i);
        }

        public static SuitCheckState Compute(SuitSimInputs i)
        {
            SuitCheckState r = new SuitCheckState();
            // A cabin pressure of zero is not a cabin, it is an unfilled struct. Guarding on the value
            // as well as on Valid keeps a half-built feed from producing a 15 psi differential and a
            // confident "Nominal" beside it.
            r.Valid = i.Valid && i.CabinPressPsia > 1.0;
            r.Complete = i.Complete;
            if (!r.Valid) return r;

            r.LeakSuit = LeakingSuit(i.RunSeed);

            // How far through the timed check this run is, 0..1. A finished run is fully bled down
            // whatever the countdown says — FINISH ends a run early, and the table behind the result
            // popup must agree with the verdict on it rather than showing a suit still holding.
            double p;
            if (i.RunSeed == 0) p = 0.0;
            else if (i.Complete) p = 1.0;
            else
            {
                int c = i.Countdown;
                if (c < 0) c = 0;
                if (c > RunSteps) c = RunSteps;
                p = (RunSteps - c) / (double)RunSteps;
            }

            double bleed = LeakFallPsi * p;
            double d = SuitLoopPsia - i.CabinPressPsia;
            r.D0 = d + Fit[0] - (r.LeakSuit == 1 ? bleed : 0.0);
            r.D1 = d + Fit[1] - (r.LeakSuit == 2 ? bleed : 0.0);
            r.D2 = d + Fit[2] - (r.LeakSuit == 3 ? bleed : 0.0);
            r.D3 = d + Fit[3] - (r.LeakSuit == 4 ? bleed : 0.0);
            return r;
        }

        /// <summary>
        /// THE ROLL. Which suit this run finds a leak in (1..4), or 0 for a clean run.
        ///
        /// A pure function of the seed, deliberately: the outcome must be stable for the whole of one
        /// run (a value re-rolled per frame is a flickering verdict, not a leak check), two screens
        /// showing the same run have to agree, and both branches have to be reachable from a test. The
        /// glue mints one seed per run from the real clock; a test or the preview passes its own.
        /// </summary>
        public static int LeakingSuit(uint seed)
        {
            if (seed == 0) return 0;                    // no run has been made
            uint h = Mix(seed);
            // One hash, two fields: the high bits decide IF, the low two decide WHICH. After the
            // avalanche below those ends are independent enough to be used as two draws.
            double roll = (h >> 8) / 16777216.0;        // 0..1
            if (roll >= LeakChance) return 0;
            return (int)(h & 3u) + 1;
        }

        /// <summary>
        /// The lowest seed whose roll finds a leak in this suit (1..4), or 0 if asked for a suit that
        /// does not exist. This exists so the PREVIEW and the headless tests can drive the 5% branch
        /// directly instead of rolling until it happens — the roll being injectable is the point.
        /// </summary>
        public static uint SeedForLeak(int suit)
        {
            if (suit < 1 || suit > 4) return 0;
            for (uint s = 1; s != 0; s++) if (LeakingSuit(s) == suit) return s;
            return 0;
        }

        /// <summary>Mint a run seed. The glue passes the real clock at the moment a run began —
        /// INITIATE, TRY ADDITIONAL TIMER, or (S32) a TROUBLESHOOT repair — plus the run's index, so
        /// re-running re-rolls and a repair's re-run is rolled like any other; pure so the mint itself
        /// is testable. Never returns 0, which means "no run".</summary>
        public static uint SeedFrom(double clockSeconds, int runIndex)
        {
            unchecked
            {
                ulong t = (ulong)(long)(clockSeconds * 1000.0);
                uint s = Mix((uint)t ^ Mix((uint)(t >> 32) + (uint)runIndex * 2654435761u));
                return s == 0u ? 1u : s;
            }
        }

        /// <summary>What a differential prints as. Two decimals and a bare "psi", which is how the
        /// reference's own row renders it (docs/UI_AUDIT.md: "0.01psi").</summary>
        public static string Text(double psi) { return psi.ToString("F2") + "psi"; }

        /// <summary>A 32-bit avalanche (splitmix-style). Not cryptography — it just has to spread a
        /// clock-derived seed across every bit so neighbouring runs do not roll alike.</summary>
        static uint Mix(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7FEB352Du;
                x ^= x >> 15; x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x;
            }
        }
    }
}
