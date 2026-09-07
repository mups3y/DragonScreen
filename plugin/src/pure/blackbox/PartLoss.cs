// DragonScreen — BlackBox / PART LOSS  (register S227; spec: §2.9's event log, §1.4's source rule)
// ============================================================================================
// PURE. The classifier behind the part-loss events. Written pure because the ONE question this
// channel exists to answer is a JUDGEMENT — "was that part supposed to go?" — and a judgement that
// can only be exercised by crashing a rocket is a judgement nobody ever checks.
//
// ---- WHY THIS FILE EXISTS AT ALL: THE OWNER'S QUESTION, 2026-09-08 ----
// Verbatim: "we need to know exactly if or when a part failed and which failed first. For instance if
// a separator suddenly disappears and was not commanded to do anything it is probably because it
// exploded or failed. This would lead to a cascade of failures that could be confusing without
// analysing the very first failure point that lead to a instant RUD."
//
// Before S227 the recorder could not answer it in any form: no `GameEvents` subscription anywhere in
// `plugin/src`, no column that NAMES a part (`skin_temp_frac`/`hull_temp_c` are the hottest part's
// temperature and never say which part), and no part event among the 1,091 events of run `034133` —
// `stage.staged`, `stage.engine_ignite` and `stage.engine_shutdown` are all COMMANDED actions. That
// run produced a third stream, `New_Crew-2_Probe_Debris`, that the flight before it did not: something
// came apart, and the recording cannot say what went first.
//
// ---- ⛔ THE WHOLE DIFFICULTY, IN ONE SENTENCE ----
// KSP's `onPartDie` FIRES ON NORMAL STAGING. Every decoupler, every fairing, every spent stage dies
// legitimately, and an event that cannot tell a separator firing on command from a separator exploding
// is worthless for the purpose it was built for — it would bury the one real failure under a hundred
// routine ones, which is the S225 flood defect wearing a different hat.
//
// ---- ⭐⭐ AND THE DELIBERATE WEAKNESS THAT MAKES IT HONEST: `Unclassified` IS A VERDICT ----
// The tempting rule is "a stage command was given a moment ago, therefore this death was commanded".
// ⛔ THAT RULE IS WRONG IN EXACTLY THE CASE THIS CHANNEL EXISTS FOR. Staging is the most violent
// moment in the flight and the likeliest instant for something to let go; a part that explodes DURING
// a staging event would be labelled `commanded` and disappear from the analysis. So a recent stage
// command WITHOUT a decouple naming this part yields `Unclassified`, never `Commanded` — §1.4's rule
// that an honest "cannot classify" beats a confident wrong label, applied to a verdict rather than to
// a readout.
//
// `Commanded` is asserted on ONE piece of evidence only: KSP itself raised `onPartDeCouple`,
// `onPartDeCoupleComplete`, `onPartUndock` or `onPartUndockComplete` FOR THIS EXACT PART. That is the
// game saying "this part was released on purpose", about this part, not about the vessel.
//
// ---- WHAT THE VERDICT CARRIES, AND WHY IT IS NOT JUST A LABEL ----
// The verdict returns the RAW MEASUREMENTS beside the class — seconds since the last stage command,
// seconds since this part's own decouple — so an analyst reads the reasoning rather than trusting the
// label. A classifier whose inputs are invisible is one that can only be argued with, never checked.
// ============================================================================================

namespace DragonScreen.BlackBox
{
    /// <summary>
    /// What the recorder is willing to say about WHY a part left the vessel. ⛔ The default is
    /// <see cref="Unclassified"/> and that is deliberate: an unset verdict must never read as a
    /// confident one, so the zero value is the one that claims nothing.
    /// </summary>
    public enum PartLossClass : byte
    {
        /// <summary>⭐ The honest default. Something happened near a command and we cannot separate them.</summary>
        Unclassified = 0,
        /// <summary>KSP raised a decouple/undock FOR THIS PART. The game said it was released on purpose.</summary>
        Commanded = 1,
        /// <summary>No command anywhere near it, the vessel is not being torn down, and a part is gone.</summary>
        Uncommanded = 2,
        /// <summary>The whole vessel is going away (destroyed/unloaded). Not a failure — and at RUD or a
        /// scene change this is what hundreds of simultaneous deaths actually are.</summary>
        Teardown = 3,
    }

    /// <summary>Everything the glue could observe about one part's death, with no KSP type in sight.</summary>
    public struct PartLossFacts
    {
        /// <summary>UT the loss was observed.</summary>
        public double Ut;
        /// <summary>UT of the last staging command seen on this vessel; NaN if none has been seen.</summary>
        public double LastStageCommandUt;
        /// <summary>UT this EXACT part raised a decouple/undock; NaN if it never did.</summary>
        public double LastDecoupleUt;
        /// <summary>The vessel is being destroyed or unloaded around this part.</summary>
        public bool VesselTeardown;
        /// <summary>The loss arrived on KSP's own failure channel (`onPartFailure`).</summary>
        public bool ArrivedAsFailure;
        /// <summary>The loss arrived as a structural joint break (`onPartJointBreak`).</summary>
        public bool ArrivedAsJointBreak;
    }

    /// <summary>The class, plus the raw numbers it was decided from, plus a one-word reason token.</summary>
    public struct PartLossVerdict
    {
        public PartLossClass Class;
        /// <summary>Seconds since the last staging command; NaN when none has been seen.</summary>
        public double SinceStageS;
        /// <summary>Seconds since this part's own decouple; NaN when it never decoupled.</summary>
        public double SinceDecoupleS;
        /// <summary>The rule that fired, as a stable token an analyser can group on.</summary>
        public string Why;
    }

    public static class PartLoss
    {
        /// <summary>
        /// How long after a part's OWN decouple/undock its death still counts as that decouple's doing.
        /// Generous on purpose: KSP raises the decouple and destroys the part within a frame or two of
        /// each other, so anything inside a couple of seconds is the same event, and stretching it
        /// costs nothing — this window can only ever turn an `Uncommanded` into a `Commanded` for a
        /// part the game explicitly said it released.
        /// </summary>
        public const double DecoupleWindowS = 2.0;

        /// <summary>
        /// How long after a STAGING command a death is treated as ambiguous rather than uncommanded.
        /// ⛔ This window does NOT produce `Commanded` — see the header. It produces `Unclassified`,
        /// which is a weaker claim than the evidence would allow and is meant to be.
        /// </summary>
        public const double StageWindowS = 5.0;

        // ---- the reason tokens, named so a typo is a compile error and not a lost grouping ----
        // Same argument as `BlackBoxEvents`' kind constants: a misspelled token does not fail, it just
        // makes one bucket invisible to the reader's filter.
        public const string WhyTeardown     = "vessel_teardown";
        public const string WhyDecoupled    = "decoupled";
        public const string WhyKspFailure   = "ksp_failure";
        public const string WhyJointBreak   = "joint_break";
        public const string WhyStageWindow  = "staging_window";
        public const string WhyNoCommand    = "no_command";

        /// <summary>
        /// Classify one part loss. ⛔ Order matters and each rung is here for a reason:
        ///
        ///  1. TEARDOWN first, and unconditionally. When a vessel is destroyed or unloaded every one of
        ///     its parts dies in the same frame. If teardown were checked last, a scene change would
        ///     emit hundreds of `uncommanded` verdicts — the exact false cascade this channel exists to
        ///     let an analyst see through.
        ///  2. THIS PART'S OWN DECOUPLE. The only evidence strong enough for `Commanded`.
        ///  3. KSP'S OWN FAILURE/BREAK CHANNELS. The game said it failed; nothing else to weigh.
        ///  4. A RECENT STAGE COMMAND → `Unclassified`. ⭐ Deliberately NOT `Commanded`.
        ///  5. Otherwise `Uncommanded`: no command near it, vessel intact, part gone.
        /// </summary>
        public static PartLossVerdict Classify(PartLossFacts f)
        {
            PartLossVerdict v;
            v.SinceStageS    = Elapsed(f.Ut, f.LastStageCommandUt);
            v.SinceDecoupleS = Elapsed(f.Ut, f.LastDecoupleUt);

            if (f.VesselTeardown)
            {
                v.Class = PartLossClass.Teardown; v.Why = WhyTeardown; return v;
            }
            if (Within(v.SinceDecoupleS, DecoupleWindowS))
            {
                v.Class = PartLossClass.Commanded; v.Why = WhyDecoupled; return v;
            }
            if (f.ArrivedAsFailure)
            {
                v.Class = PartLossClass.Uncommanded; v.Why = WhyKspFailure; return v;
            }
            if (f.ArrivedAsJointBreak)
            {
                v.Class = PartLossClass.Uncommanded; v.Why = WhyJointBreak; return v;
            }
            if (Within(v.SinceStageS, StageWindowS))
            {
                v.Class = PartLossClass.Unclassified; v.Why = WhyStageWindow; return v;
            }
            v.Class = PartLossClass.Uncommanded; v.Why = WhyNoCommand; return v;
        }

        /// <summary>The stable lowercase token for a class, for the event payload and for grouping.</summary>
        public static string Name(PartLossClass c)
        {
            switch (c)
            {
                case PartLossClass.Commanded:   return "commanded";
                case PartLossClass.Uncommanded: return "uncommanded";
                case PartLossClass.Teardown:    return "teardown";
                default:                        return "unclassified";
            }
        }

        /// <summary>
        /// Seconds from `then` to `now`. NaN in (an unseen command) stays NaN out — ⛔ never 0, which
        /// would read as "it happened exactly now" and is the §4.6 blank-not-zero rule in arithmetic
        /// form. A `then` in the future (a clock that moved backwards on a revert) also yields NaN
        /// rather than a negative age no rule below knows how to weigh.
        /// </summary>
        public static double Elapsed(double now, double then)
        {
            if (double.IsNaN(now) || double.IsNaN(then)) return double.NaN;
            double d = now - then;
            return d < 0.0 ? double.NaN : d;
        }

        static bool Within(double elapsed, double window)
        {
            return !double.IsNaN(elapsed) && elapsed <= window;
        }
    }

    /// <summary>
    /// ⭐⭐ THE FLOOD GUARD, AND WHY IT KEEPS THE **FIRST** N RATHER THAN THE LAST.
    ///
    /// At a RUD hundreds of parts die in one frame — the pad craft carries 7,083 rows in
    /// `craftdump.csv` — and the value of the record is ENTIRELY IN THE ORDER. The first line of that
    /// sequence is the finding; everything after it is the cascade the owner described as "confusing".
    /// So the budget admits events until the cap and then stops, which keeps the head of the sequence.
    /// A ring buffer would keep the tail and throw away the answer.
    ///
    /// ⛔ AND IT IS NEVER SILENT. Crossing the cap raises a marker EXACTLY ONCE carrying the count, so
    /// a reader is told the sequence was cut instead of inferring a short cascade from a truncated
    /// file. Silent truncation would destroy the very evidence this channel exists to capture — the
    /// same argument `crew.press_dropped` is in the recording rather than in a log line.
    /// </summary>
    public sealed class PartLossBudget
    {
        readonly int cap;
        int seen;
        bool markerDue;
        bool markerTaken;

        public PartLossBudget(int cap) { this.cap = cap < 0 ? 0 : cap; }

        /// <summary>How many losses have been offered to this budget, admitted or not.</summary>
        public int Seen { get { return seen; } }
        /// <summary>The cap this budget was built with.</summary>
        public int Cap { get { return cap; } }

        /// <summary>
        /// Offer one loss. True while under the cap. The FIRST rejection arms the marker; every later
        /// one does not, so the marker is emitted once however long the cascade runs.
        /// </summary>
        public bool Admit()
        {
            seen++;
            if (seen <= cap) return true;
            if (!markerTaken) markerDue = true;
            return false;
        }

        /// <summary>
        /// True exactly once, on the first call after the cap was first exceeded. ⛔ Consuming: a
        /// second call returns false even if the cascade is still running, which is what makes
        /// "exactly one marker" a property of this class rather than of its caller's discipline.
        /// </summary>
        public bool TakeMarker()
        {
            if (!markerDue) return false;
            markerDue = false; markerTaken = true; return true;
        }
    }
}
