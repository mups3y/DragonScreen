// DragonScreen — BlackBox / COVERAGE  (register BB1; the S76 ghost-column defect, stated as a check)
// ============================================================================================
// ⭐ BB6 (2026-09-05). `column_never_written` fires on a column written NEVER — `alt_m` IS written,
// sometimes, so a hole eaten INTO an otherwise-live column was invisible to it by construction: the
// confirm flight behind [[BB5]] reported "0 coverage defects" over exactly such a column. Quoting this
// file's own words back at itself, one level up: "The failure is not that the cells were blank. It is
// that NOTHING IN THE FILE SAID SO." — here, the file said "0 defects" over a column with holes in it.
//
// So `Note` now also tracks, per column, how many rows it was ELIGIBLE to be written on, not just
// whether it EVER was — and `Findings` compares filled-count to eligible-count. "Eligible" is NOT "every
// row": a Tier.R2/R3 column is only due on the rows its own period reaches (BlackBoxRate.cs's decimation
// ladder), and a `BlackBoxVoid.Control` column is legitimately blank on a rails-warp row (§4.6) — firing
// on either would trade this blind spot for a false alarm on every normal flight, which is no defect
// report anybody would read. Both are read from the SAME declarations the writer itself obeys, not
// re-guessed, so the eligibility check can never drift from what BuildRow actually does. A column that
// is eligible and still comes up short IS the hole this line exists to catch.
// ============================================================================================
// PURE. ⭐ THE DEFECT THIS FILE EXISTS FOR, quoted from BB1's register line:
//
//   "(1) `torque_cmd` was declared in the schema and never written in any file — a column that exists
//    and is always empty fakes coverage; BB1 must fail loudly ... if a declared column is never
//    populated across a mission. (2) `mode_holding` / `mode_flying` carried no state — the same defect,
//    twice more; every declared column needs a real writer or it does not go in the schema."
//
// The failure is not that the cells were blank. It is that NOTHING IN THE FILE SAID SO. A reader
// looking for the commanded torque found the column, found it empty, and had no way to tell "the loop
// commanded nothing" from "nobody ever wrote this". Three columns of the last flown corpus were in that
// state and it took a separate audit (S76) to discover it — after the flights.
//
// ---- HOW THIS ANSWERS IT, AND WHY IT IS NOT JUST "WARN ON ANY EMPTY COLUMN" ----
// A blanket empty-column warning would fire on every `Fit.Unfitted` guidance column on every flight
// until Part B is finished, which is a warning that gets ignored, which is no warning at all. §2.5
// wants those columns present and blank — that IS how a real recorder reports an unfitted system.
//
// So the check is against the column's DECLARED fitness (`BlackBoxSchema.Col.Fit`):
//   • Live        — never written across the whole mission  ⇒  DEFECT. `rec.column_never_written`.
//   • Conditional — never written  ⇒  reported as a NOTE with the declared condition, not a defect.
//                   ("no target was ever selected" is a fact about the flight, not about the recorder.)
//   • Unfitted    — never written  ⇒  silent. That is its declared state, and the manifest says so.
// And symmetrically, the one case nobody thought to check for:
//   • Unfitted    — WRITTEN  ⇒  DEFECT, the other way round. A column declared to have no source
//                   produced values, so either a writer was added without updating the declaration or
//                   the value came from somewhere unaccounted for. Either way the manifest is now lying
//                   about provenance, which is the one thing the manifest exists to prevent.
//
// ---- COST ----
// One `bool[]` of schema width, one pass over the row at write time comparing each cell to "". At 10 Hz
// over ~150 columns that is ~1500 reference compares per second against a frame of KSP part-tree work.
// It is inside §4.7's < 0.3 ms row budget and is measured by `rec_build_us` like everything else.
// ============================================================================================
using System.Collections.Generic;

namespace DragonScreen.BlackBox
{
    /// <summary>One finding from the close-out coverage pass.</summary>
    public struct CoverageFinding
    {
        public string Column;
        /// <summary>
        /// "never_written" (a Live column produced nothing), "unexpected_writer" (an Unfitted one did),
        /// BB2's "capsule_leak" (a `Scope.Capsule` column carried a value on a stream whose vessel never
        /// held the camera — the capsule's state filed under another vessel), or BB6's
        /// "partially_written" (a Live column was written on SOME of the rows it was eligible for and
        /// blank on the rest — the hole `column_never_written` cannot see because the column is not
        /// never written, just not written enough).
        /// </summary>
        public string Kind;
        /// <summary>True for the two DEFECT cases; false for the Conditional note.</summary>
        public bool Defect;
        /// <summary>The declared condition or the register line, verbatim from the schema.</summary>
        public string Declared;
    }

    /// <summary>
    /// Tracks, per column, whether ANY row of this stream ever carried a non-blank cell there — AND
    /// (BB6) how many rows it was actually ELIGIBLE for versus how many of those it filled, so a column
    /// that is written SOMETIMES but not on every row its own tier reaches still shows up.
    /// One instance per stream (a two-vessel mission has two — BB2), because a column can legitimately
    /// be live on one vessel and absent on the other.
    /// </summary>
    public sealed class BlackBoxCoverage
    {
        readonly bool[] written;
        readonly int[] filled;      // BB6: rows where the cell was non-blank
        readonly int[] eligible;    // BB6: rows where the cell was DUE to be non-blank (see Eligible)

        public BlackBoxCoverage()
        {
            written = new bool[BlackBoxSchema.Width];
            filled = new int[BlackBoxSchema.Width];
            eligible = new int[BlackBoxSchema.Width];
        }

        /// <summary>Rows counted, so a finding can say "never, across N rows" rather than just "never".</summary>
        public int Rows { get; private set; }

        /// <summary>
        /// Call once per row, AFTER every filler has run and AFTER the warp void (§4.6). `fillR2`/`fillR3`
        /// are the SAME `RowPlan` flags BuildRow used to decide whether to fill this row's R2/R3 block —
        /// read from the plan, not re-derived, so eligibility can never disagree with what was actually
        /// asked to be written. `rails` is `warp_rails` for this row (§4.6's control-column void).
        /// </summary>
        public void Note(string[] cells, bool fillR2, bool fillR3, bool rails)
        {
            Rows++;
            Col[] cols = BlackBoxSchema.Columns;
            int n = cells.Length < written.Length ? cells.Length : written.Length;
            for (int i = 0; i < n; i++)
            {
                if (Eligible(cols[i], i, fillR2, fillR3, rails)) eligible[i]++;
                if (!string.IsNullOrEmpty(cells[i]))
                {
                    written[i] = true;
                    filled[i]++;
                }
            }
        }

        /// <summary>
        /// The BB1 shape, kept for every existing caller that only cares about never-vs-ever-written:
        /// every row counts as eligible for every column, which is exactly today's behaviour and matches
        /// a fixture/test that has no R2/R3/rails cadence to report in the first place.
        /// </summary>
        public void Note(string[] cells) { Note(cells, true, true, false); }

        /// <summary>
        /// Is column `i` DUE on this row? Tier.R2/R3 are due only when the SAME rate-ladder flag that
        /// gated the writer says so (BlackBoxRate.cs); everything else is due every row UNLESS it is one
        /// of `BlackBoxVoid`'s control columns on a rails-warp row, where §4.6 blanks it on purpose.
        /// </summary>
        static bool Eligible(Col c, int i, bool fillR2, bool fillR3, bool rails)
        {
            if (c.Tier == Tier.R2) return fillR2;
            if (c.Tier == Tier.R3) return fillR3;
            if (rails && BlackBoxVoid.IsControlColumn(i)) return false;
            return true;
        }

        public bool WasWritten(int col)
        {
            return col >= 0 && col < written.Length && written[col];
        }

        /// <summary>
        /// The close-out pass for a stream that held the camera for at least one row. Kept so a
        /// single-vessel mission reads exactly as it did under BB1.
        /// </summary>
        public List<CoverageFinding> Findings() { return Findings(true); }

        /// <summary>
        /// The close-out pass. Empty list = every Live column produced values and no Unfitted one did.
        /// A stream with no rows at all returns nothing: "the recorder never ran" is a different fault
        /// and is already reported by `rows_written = 0` in the manifest.
        ///
        /// ⭐ BB2's `everFocused`. A tracked UNFOCUSED stream (the §B16 booster) correctly writes NO
        /// `Scope.Capsule` column — those are the Dragon's singletons, and copying them onto the
        /// booster's rows is the fabrication the scope declaration exists to prevent. Reporting each of
        /// those ~27 blanks as a ghost-column DEFECT would fire on every two-vessel flight, and a
        /// defect that always fires is a defect nobody reads — the exact failure mode this file's
        /// header rejects for the blanket empty-column warning. So on a stream that never held the
        /// camera they are NOTES carrying the reason, and the symmetric check takes over instead:
        /// a capsule value that DID reach a never-focused stream is a defect in its own right.
        /// </summary>
        public List<CoverageFinding> Findings(bool everFocused)
        {
            var found = new List<CoverageFinding>();
            if (Rows == 0) return found;

            Col[] cols = BlackBoxSchema.Columns;
            for (int i = 0; i < cols.Length; i++)
            {
                Col c = cols[i];
                bool capsuleOnOther = c.Scope == Scope.Capsule && !everFocused;

                if (!written[i])
                {
                    if (capsuleOnOther)
                        found.Add(Make(c.Name, "never_written", false,
                            "declared Scope.Capsule and this stream's vessel never held the camera, so "
                            + "the capsule singleton was withheld rather than copied (BB2): " + c.Source));
                    else if (c.Fit == Fit.Live)
                        found.Add(Make(c.Name, "never_written", true, "declared Live: " + c.Source));
                    else if (c.Fit == Fit.Conditional)
                        found.Add(Make(c.Name, "never_written", false, c.Note));
                    // Unfitted + never written is its declared state. Silence is correct.
                }
                else if (capsuleOnOther)
                {
                    // ⛔ THE LEAK. A stream whose vessel never held the camera carries a value in a
                    // column sourced from the capsule's singletons — so somebody's bus voltage, gate or
                    // mission phase is filed under the wrong vessel. That is §4.8's NEVER FABRICATE
                    // breached, and it is silent in the file: the cell looks like every other cell.
                    found.Add(Make(c.Name, "capsule_leak", true,
                        "declared Scope.Capsule but written on a stream that never held the camera — "
                        + "the capsule's state is filed under another vessel: " + c.Source));
                }
                else if (c.Fit == Fit.Unfitted)
                {
                    found.Add(Make(c.Name, "unexpected_writer", true,
                        "declared Unfitted, pending " + c.Note + " — the manifest's provenance is now wrong"));
                }
                // ⭐ BB6: written[i] is true (the column is not a ghost) but that alone was the blind
                // spot — check it FILLED EVERY ROW IT WAS ELIGIBLE FOR, not merely "at least once".
                // Fit.Conditional is deliberately EXEMPT, same as `never_written` above: a conditional
                // source (no target, screens not running, KER absent) legitimately comes and goes across
                // a flight, and firing on that would be the false alarm this line was built to avoid —
                // see S85/S94/BB9's columns, which are ALL Conditional for exactly this reason.
                else if (c.Fit == Fit.Live && eligible[i] > 0 && filled[i] < eligible[i])
                {
                    found.Add(Make(c.Name, "partially_written", true,
                        string.Format("declared Live: filled {0}/{1} eligible row(s) — {2}",
                            filled[i], eligible[i], c.Source)));
                }
            }
            return found;
        }

        static CoverageFinding Make(string col, string kind, bool defect, string declared)
        {
            CoverageFinding f = new CoverageFinding();
            f.Column = col; f.Kind = kind; f.Defect = defect; f.Declared = declared;
            return f;
        }
    }
}
